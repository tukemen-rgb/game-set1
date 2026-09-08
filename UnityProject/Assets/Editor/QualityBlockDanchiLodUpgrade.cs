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
        ApplyToOpenScene();
        ValidateOpenScene();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Four-level danchi detail LOD hierarchy built. Runtime transition/render verification remains pending.");
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

        MeshRenderer[] sourceRenderers = root.GetComponentsInChildren<MeshRenderer>(true)
            .Where(r => !IsUnderProxyRoot(r.transform, root.transform))
            .OrderBy(r => HierarchyPath(r.transform, root.transform), StringComparer.Ordinal)
            .ToArray();
        if (sourceRenderers.Length == 0)
            throw new InvalidOperationException("DanchiHighDetail contains no source renderers for LOD generation.");

        var lod1Root = CreateProxyRoot(Lod1RootName, root.transform);
        var lod2Root = CreateProxyRoot(Lod2RootName, root.transform);
        var lod3Root = CreateProxyRoot(Lod3RootName, root.transform);

        var lod0 = new List<Renderer>(sourceRenderers.Length);
        var lod1 = new List<Renderer>();
        var lod2 = new List<Renderer>();
        var lod3 = new List<Renderer>();
        int microCount = 0;
        int fineCount = 0;
        int mediumCount = 0;
        int macroCount = 0;

        foreach (MeshRenderer source in sourceRenderers)
        {
            lod0.Add(source);
            int retainedThrough = RetainedThroughLod(source.gameObject.name);
            switch (retainedThrough)
            {
                case 0: microCount++; break;
                case 1: fineCount++; break;
                case 2: mediumCount++; break;
                default: macroCount++; break;
            }

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

        var group = root.GetComponent<LODGroup>();
        if (group == null) group = root.AddComponent<LODGroup>();
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

        var manifest = root.GetComponent<QualityBlockDanchiLodManifest>();
        if (manifest == null) manifest = root.AddComponent<QualityBlockDanchiLodManifest>();
        manifest.Configure(
            lod0.Count, lod1.Count, lod2.Count, lod3.Count,
            microCount, fineCount, mediumCount, macroCount,
            Lod0Transition, Lod1Transition, Lod2Transition, Lod3Cull);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    [MenuItem("NewTown/QA/Validate Danchi Detail LODs")]
    public static void ValidateOpenScene()
    {
        GameObject root = FindSceneObject(DetailRootName);
        if (root == null) throw new InvalidOperationException("DanchiHighDetail is missing.");

        var manifest = root.GetComponent<QualityBlockDanchiLodManifest>();
        if (manifest == null)
            throw new InvalidOperationException("Danchi LOD manifest is missing; LOD pass did not run.");

        var group = root.GetComponent<LODGroup>();
        if (group == null) throw new InvalidOperationException("DanchiHighDetail LODGroup is missing.");
        LOD[] lods = group.GetLODs();
        if (lods.Length != 4)
            throw new InvalidOperationException($"Expected exactly four danchi detail LOD levels, got {lods.Length}.");

        if (!(manifest.Lod0RendererCount > manifest.Lod1RendererCount &&
              manifest.Lod1RendererCount >= manifest.Lod2RendererCount &&
              manifest.Lod2RendererCount >= manifest.Lod3RendererCount &&
              manifest.Lod3RendererCount > 0))
        {
            throw new InvalidOperationException(
                $"LOD renderer counts are not a valid progressive reduction: " +
                $"{manifest.Lod0RendererCount}/{manifest.Lod1RendererCount}/" +
                $"{manifest.Lod2RendererCount}/{manifest.Lod3RendererCount}.");
        }

        if (manifest.MicroRendererCount <= 0 || manifest.MacroRendererCount <= 0)
            throw new InvalidOperationException(
                $"Physical-scale classification is incomplete: micro={manifest.MicroRendererCount}, macro={manifest.MacroRendererCount}.");

        if (Mathf.Abs(lods[0].screenRelativeTransitionHeight - Lod0Transition) > 0.0001f ||
            Mathf.Abs(lods[1].screenRelativeTransitionHeight - Lod1Transition) > 0.0001f ||
            Mathf.Abs(lods[2].screenRelativeTransitionHeight - Lod2Transition) > 0.0001f ||
            Mathf.Abs(lods[3].screenRelativeTransitionHeight - Lod3Cull) > 0.0001f)
            throw new InvalidOperationException("Danchi detail LOD transition heights do not match the benchmark policy.");

        if (group.fadeMode != LODFadeMode.CrossFade || !group.animateCrossFading)
            throw new InvalidOperationException("Danchi detail LODs must use animated cross-fading to reduce visible component popping.");

        int proxyCount = root.GetComponentsInChildren<QualityBlockDanchiLodProxyMarker>(true).Length;
        if (proxyCount != 3)
            throw new InvalidOperationException($"Expected three LOD proxy roots, got {proxyCount}.");

        Debug.Log(
            $"Danchi detail LOD validation passed structurally: renderers " +
            $"LOD0={manifest.Lod0RendererCount}, LOD1={manifest.Lod1RendererCount}, " +
            $"LOD2={manifest.Lod2RendererCount}, LOD3={manifest.Lod3RendererCount}; " +
            $"physical tiers micro/fine/medium/macro={manifest.MicroRendererCount}/" +
            $"{manifest.FineRendererCount}/{manifest.MediumRendererCount}/{manifest.MacroRendererCount}. " +
            "Actual cross-fade timing, shadow continuity and silhouette transitions still require Unity render inspection.");
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
        var oldManifest = root.GetComponent<QualityBlockDanchiLodManifest>();
        if (oldManifest != null) UnityEngine.Object.DestroyImmediate(oldManifest);
    }

    private static GameObject CreateProxyRoot(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<QualityBlockDanchiLodProxyMarker>();
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

        // Small manufactured attachments and service hardware.
        if (ContainsAny(n, "BasePlate", "Bracket", "Receiver", "Track", "Collar", "Clamp", "Pipe", "Hose",
            "Refrigerant", "FanHub", "Foot", "Feet", "Mullion", "Conduit", "Joint"))
            return 1;

        // Components that continue to create facade depth/parallax at medium distance.
        if (ContainsAny(n, "Divider", "SashStile", "FanDisc", "ClothesBracket", "StairWindow"))
            return 2;

        // Large edges and openings control the apartment block silhouette/read at long distance.
        if (ContainsAny(n, "BalconySlabLip", "RailMid", "RailLower", "WindowFrame", "WindowSill"))
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
            if (current.GetComponent<QualityBlockDanchiLodProxyMarker>() != null) return true;
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

[DisallowMultipleComponent]
public sealed class QualityBlockDanchiLodProxyMarker : MonoBehaviour
{
}

[DisallowMultipleComponent]
public sealed class QualityBlockDanchiLodManifest : MonoBehaviour
{
    [SerializeField] private int lod0RendererCount;
    [SerializeField] private int lod1RendererCount;
    [SerializeField] private int lod2RendererCount;
    [SerializeField] private int lod3RendererCount;
    [SerializeField] private int microRendererCount;
    [SerializeField] private int fineRendererCount;
    [SerializeField] private int mediumRendererCount;
    [SerializeField] private int macroRendererCount;
    [SerializeField] private float lod0Transition;
    [SerializeField] private float lod1Transition;
    [SerializeField] private float lod2Transition;
    [SerializeField] private float lod3Cull;

    public int Lod0RendererCount => lod0RendererCount;
    public int Lod1RendererCount => lod1RendererCount;
    public int Lod2RendererCount => lod2RendererCount;
    public int Lod3RendererCount => lod3RendererCount;
    public int MicroRendererCount => microRendererCount;
    public int FineRendererCount => fineRendererCount;
    public int MediumRendererCount => mediumRendererCount;
    public int MacroRendererCount => macroRendererCount;

    public void Configure(
        int lod0, int lod1, int lod2, int lod3,
        int micro, int fine, int medium, int macro,
        float transition0, float transition1, float transition2, float cull3)
    {
        lod0RendererCount = lod0;
        lod1RendererCount = lod1;
        lod2RendererCount = lod2;
        lod3RendererCount = lod3;
        microRendererCount = micro;
        fineRendererCount = fine;
        mediumRendererCount = medium;
        macroRendererCount = macro;
        lod0Transition = transition0;
        lod1Transition = transition1;
        lod2Transition = transition2;
        lod3Cull = cull3;
    }
}
