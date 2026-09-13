using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds four explicit component-level LOD representations for the generated high-detail danchi.
/// LOD0 keeps the complete construction master. LOD1-3 use renderer proxies that share the authored
/// bevel meshes/materials but progressively remove details according to their physical scale and
/// contribution to silhouette/parallax. The proxy renderers are distinct from LOD0 so Unity can
/// cross-fade without assigning the same Renderer to adjacent LOD levels.
/// </summary>
public static class QualityBlockDanchiLodUpgrade
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string DetailRootName = "DanchiHighDetail";
    private const string Lod1RootName = "HD_LOD1_Proxy";
    private const string Lod2RootName = "HD_LOD2_Proxy";
    private const string Lod3RootName = "HD_LOD3_Proxy";

    // Screen-relative heights are deliberately conservative for the benchmark camera: the detailed
    // apartment block should remain at LOD0 whenever it is a major composition element.
    private const float Lod0Transition = 0.18f;
    private const float Lod1Transition = 0.08f;
    private const float Lod2Transition = 0.03f;
    private const float Lod3Cull = 0.008f;

    [MenuItem("NewTown/Geometry/Build LOD Detailed Weathered Danchi")]
    public static void BuildLodDetailedQualityBlock()
    {
        QualityBlockDetailBevelUpgrade.BuildBeveledDetailedQualityBlock();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // The installation correction must run while the legacy casing-mounted fan/grille children
        // still exist: it seats slab -> plate -> foot -> chassis, moves those casing-mounted details,
        // and reconstructs the paired refrigerant lines and drain against the fixed wall/slab interfaces.
        // Persist and reopen that mechanically valid state before replacing the fan face, then generate
        // LOD proxies only after every outdoor-unit construction refinement is complete.
        QualityBlockAcOutdoorUnitInstallationQA.ApplyAndPersist();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        QualityBlockAcFanPhysicalRefinement.ApplyToOpenScene();
        QualityBlockAcFanGuardRoundWireRefinement.ApplyToOpenScene();
        QualityBlockAcFanGuardMountInterfaceRefinement.ApplyToOpenScene();
        ApplyToOpenScene();
        ValidateOpenScene();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Four-level danchi detail LOD hierarchy built after full outdoor-AC support/service installation correction, physical fan reconstruction, round-wire guard refinement and seated guard mounting. Runtime transition/render verification remains pending.");
    }

    [MenuItem("NewTown/Geometry/Apply Danchi Detail LOD Pass Only")]
    public static void ApplyToOpenScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() ||
            EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject root = FindSceneObject(DetailRootName);
        if (root == null)
            throw new InvalidOperationException("DanchiHighDetail is missing. Build the beveled detail pass first.");

        RemoveExistingLodArtifacts(root);

        MeshRenderer[] sourceRenderers = GetSourceRenderers(root);
        if (sourceRenderers.Length == 0)
            throw new InvalidOperationException("DanchiHighDetail contains no source renderers for LOD generation.");

        var lod1Root = CreateProxyRoot(Lod1RootName, root.transform);
        var lod2Root = CreateProxyRoot(Lod2RootName, root.transform);
        var lod3Root = CreateProxyRoot(Lod3RootName, root.transform);

        var lod0 = new List<Renderer>(sourceRenderers.Length);
        var lod1 = new List<Renderer>();
        var lod2 = new List<Renderer>();
        var lod3 = new List<Renderer>();

        foreach (MeshRenderer source in sourceRenderers)
        {
            lod0.Add(source);
            int retainedThrough = RetainedThroughLod(source.gameObject.name);
            if (retainedThrough >= 1)
                lod1.Add(CreateProxyRenderer(source, lod1Root.transform, 1));
            if (retainedThrough >= 2)
                lod2.Add(CreateProxyRenderer(source, lod2Root.transform, 2));
            if (retainedThrough >= 3)
                lod3.Add(CreateProxyRenderer(source, lod3Root.transform, 3));
        }

        if (lod1.Count == 0 || lod2.Count == 0 || lod3.Count == 0)
            throw new InvalidOperationException(
                $"LOD classification produced an empty level: LOD1={lod1.Count}, LOD2={lod2.Count}, LOD3={lod3.Count}.");

        var group = root.AddComponent<LODGroup>();
        group.SetLODs(new[]
        {
            new LOD(Lod0Transition, lod0.ToArray()),
            new LOD(Lod1Transition, lod1.ToArray()),
            new LOD(Lod2Transition, lod2.ToArray()),
            new LOD(Lod3Cull, lod3.ToArray())
        });
        group.fadeMode = LODFadeMode.CrossFade;
        group.animateCrossFading = true;
        group.RecalculateBounds();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    [MenuItem("NewTown/QA/Validate Danchi Detail LODs")]
    public static void ValidateOpenScene()
    {
        GameObject root = FindSceneObject(DetailRootName);
        if (root == null) throw new InvalidOperationException("DanchiHighDetail is missing.");

        var group = root.GetComponent<LODGroup>();
        if (group == null) throw new InvalidOperationException("DanchiHighDetail LODGroup is missing.");
        LOD[] lods = group.GetLODs();
        if (lods.Length != 4)
            throw new InvalidOperationException($"Expected exactly four danchi detail LOD levels, got {lods.Length}.");

        int lod0Count = lods[0].renderers.Length;
        int lod1Count = lods[1].renderers.Length;
        int lod2Count = lods[2].renderers.Length;
        int lod3Count = lods[3].renderers.Length;
        if (!(lod0Count > lod1Count && lod1Count >= lod2Count && lod2Count >= lod3Count && lod3Count > 0))
        {
            throw new InvalidOperationException(
                $"LOD renderer counts are not a valid progressive reduction: " +
                $"{lod0Count}/{lod1Count}/{lod2Count}/{lod3Count}.");
        }

        MeshRenderer[] sources = GetSourceRenderers(root);
        int microCount = sources.Count(r => RetainedThroughLod(r.gameObject.name) == 0);
        int fineCount = sources.Count(r => RetainedThroughLod(r.gameObject.name) == 1);
        int mediumCount = sources.Count(r => RetainedThroughLod(r.gameObject.name) == 2);
        int macroCount = sources.Count(r => RetainedThroughLod(r.gameObject.name) == 3);
        if (microCount <= 0 || macroCount <= 0)
            throw new InvalidOperationException(
                $"Physical-scale classification is incomplete: micro={microCount}, macro={macroCount}.");

        if (Mathf.Abs(lods[0].screenRelativeTransitionHeight - Lod0Transition) > 0.0001f ||
            Mathf.Abs(lods[1].screenRelativeTransitionHeight - Lod1Transition) > 0.0001f ||
            Mathf.Abs(lods[2].screenRelativeTransitionHeight - Lod2Transition) > 0.0001f ||
            Mathf.Abs(lods[3].screenRelativeTransitionHeight - Lod3Cull) > 0.0001f)
            throw new InvalidOperationException("Danchi detail LOD transition heights do not match the benchmark policy.");

        if (group.fadeMode != LODFadeMode.CrossFade || !group.animateCrossFading)
            throw new InvalidOperationException("Danchi detail LODs must use animated cross-fading to reduce visible component popping.");

        if (root.transform.Find(Lod1RootName) == null ||
            root.transform.Find(Lod2RootName) == null ||
            root.transform.Find(Lod3RootName) == null)
            throw new InvalidOperationException("One or more danchi LOD proxy roots are missing.");

        int proxyRendererCount = root.transform.Find(Lod1RootName).GetComponentsInChildren<Renderer>(true).Length +
                                 root.transform.Find(Lod2RootName).GetComponentsInChildren<Renderer>(true).Length +
                                 root.transform.Find(Lod3RootName).GetComponentsInChildren<Renderer>(true).Length;
        if (proxyRendererCount != lod1Count + lod2Count + lod3Count)
            throw new InvalidOperationException(
                $"LOD proxy renderer ownership mismatch: proxies={proxyRendererCount}, declared={lod1Count + lod2Count + lod3Count}.");

        // This validation method is also the final Danchi-specific entrypoint used by the formal 4K
        // preparation chain. Keep the complete outdoor-unit support/service installation, fan construction,
        // round-wire topology and physical shroud-mount interface as direct dependencies so later refactors
        // fail closed instead of accepting a visually refined but mechanically floating/disconnected unit.
        QualityBlockAcOutdoorUnitInstallationQA.ValidateOpenScene();
        QualityBlockAcFanPhysicalRefinement.ValidateOpenScene();
        QualityBlockAcFanGuardRoundWireRefinement.ValidateOpenScene();
        QualityBlockAcFanGuardMountInterfaceRefinement.ValidateOpenScene();

        Debug.Log(
            $"Danchi detail LOD validation passed structurally: renderers " +
            $"LOD0={lod0Count}, LOD1={lod1Count}, LOD2={lod2Count}, LOD3={lod3Count}; " +
            $"physical tiers micro/fine/medium/macro={microCount}/{fineCount}/{mediumCount}/{macroCount}; " +
            "outdoor-AC slab/plate/foot/chassis support, service-line/drain interfaces, rotor/shroud, round-wire guard and guard mounts are directly validated. " +
            "Actual cross-fade timing, wire aliasing, shadow continuity, contact read and silhouette transitions still require Unity render inspection.");
    }

    private static void RemoveExistingLodArtifacts(GameObject root)
    {
        foreach (string proxyName in new[] { Lod1RootName, Lod2RootName, Lod3RootName })
        {
            Transform child = root.transform.Find(proxyName);
            if (child != null) UnityEngine.Object.DestroyImmediate(child.gameObject);
        }

        var oldGroup = root.GetComponent<LODGroup>();
        if (oldGroup != null) UnityEngine.Object.DestroyImmediate(oldGroup);
    }

    private static GameObject CreateProxyRoot(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static MeshRenderer CreateProxyRenderer(MeshRenderer source, Transform proxyRoot, int lodLevel)
    {
        var go = new GameObject($"LOD{lodLevel}_{source.gameObject.name}");
        go.layer = source.gameObject.layer;
        go.transform.SetParent(proxyRoot, false);
        go.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        go.transform.localScale = DivideLossyScale(source.transform.lossyScale, proxyRoot.lossyScale);
        GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(source.gameObject));

        var sourceFilter = source.GetComponent<MeshFilter>();
        if (sourceFilter == null || sourceFilter.sharedMesh == null)
            throw new InvalidOperationException($"LOD source {source.gameObject.name} is missing a MeshFilter/sharedMesh.");

        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = sourceFilter.sharedMesh;

        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = source.sharedMaterials;
        renderer.shadowCastingMode = source.shadowCastingMode;
        renderer.receiveShadows = source.receiveShadows;
        renderer.lightProbeUsage = source.lightProbeUsage;
        renderer.reflectionProbeUsage = source.reflectionProbeUsage;
        return renderer;
    }

    private static MeshRenderer[] GetSourceRenderers(GameObject root)
    {
        return root.GetComponentsInChildren<MeshRenderer>(true)
            .Where(r => !IsUnderProxyRoot(r.transform, root.transform))
            .OrderBy(r => HierarchyPath(r.transform, root.transform), StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Returns the furthest LOD in which a physical component remains visible.
    /// 0=micro only, 1=fine, 2=medium, 3=macro/silhouette.
    /// </summary>
    private static int RetainedThroughLod(string objectName)
    {
        string n = objectName ?? string.Empty;

        // Millimetre/centimetre-scale hardware: useful at close range, but the first safe reduction.
        if (ContainsAny(n, "Bolt", "Fastener", "Washer", "Nut", "Seal", "Handle", "Clip", "GrilleBar"))
            return 0;

        // Small manufactured attachments and service hardware. The circular wire guard and its mounting
        // feet remain only through LOD1 so sub-pixel wire/hardware does not turn into shimmer at medium distance.
        if (ContainsAny(n, "BasePlate", "Bracket", "Receiver", "Track", "Collar", "Clamp", "Pipe", "Hose",
            "Refrigerant", "FanHub", "FanGuard", "Foot", "Feet", "Mullion", "Conduit", "Joint"))
            return 1;

        // Components that continue to create facade depth/parallax at medium distance.
        if (ContainsAny(n, "Divider", "SashStile", "FanDisc", "FanRotor", "ClothesBracket", "StairWindow"))
            return 2;

        // Large edges/openings and the condenser inlet shroud control the long-distance facade read.
        if (ContainsAny(n, "BalconySlabLip", "RailMid", "RailLower", "WindowFrame", "WindowSill", "FanShroud"))
            return 3;

        // Unknown future detail is retained to LOD1 rather than promoted to the far silhouette by accident.
        return 1;
    }

    private static bool ContainsAny(string value, params string[] needles)
    {
        foreach (string needle in needles)
            if (value.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        return false;
    }

    private static bool IsUnderProxyRoot(Transform transform, Transform detailRoot)
    {
        Transform current = transform;
        while (current != null && current != detailRoot)
        {
            if (current.name == Lod1RootName || current.name == Lod2RootName || current.name == Lod3RootName)
                return true;
            current = current.parent;
        }
        return false;
    }

    private static string HierarchyPath(Transform transform, Transform root)
    {
        var parts = new Stack<string>();
        Transform current = transform;
        while (current != null && current != root)
        {
            parts.Push(current.name);
            current = current.parent;
        }
        return string.Join("/", parts.ToArray());
    }

    private static Vector3 DivideLossyScale(Vector3 source, Vector3 parent)
    {
        return new Vector3(
            SafeDivide(source.x, parent.x),
            SafeDivide(source.y, parent.y),
            SafeDivide(source.z, parent.z));
    }

    private static float SafeDivide(float value, float divisor)
    {
        return Mathf.Abs(divisor) < 0.000001f ? value : value / divisor;
    }

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x.scene.IsValid() && x.name == name);
    }
}
