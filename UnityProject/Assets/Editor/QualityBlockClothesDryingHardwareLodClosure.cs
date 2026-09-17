using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Closes the refined balcony clothes-pole support over the actual four-level Danchi LOD chain.
/// The physical refinement can be installed after an existing LODGroup has already been generated;
/// additionally, the current generic "Bracket" classifier retains HD_ClothesBracket_* only through
/// LOD1. This pass rebuilds stale ownership and preserves only the 340 mm support-arm silhouette in
/// LOD2. Formal evidence cameras are validation-only. Native rendered evidence is still required.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockClothesDryingHardwareLodClosure
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string DetailRootName = "DanchiHighDetail";
    private const string Lod2RootName = "HD_LOD2_Proxy";
    private const string ContractPath = "Assets/QA/clothes_drying_hardware_lod_closure_contract.json";
    private const string LookdevPath = "Assets/QA/clothes_drying_hardware_lookdev.svg";

    private const int ExpectedArms = 60;
    private const int ExpectedReceivers = 60;
    private const int ExpectedBasePlates = 60;
    private const int ExpectedPivots = 60;
    private const int ExpectedAnchors = 120;
    private const int ExpectedSourceRenderers = 360;

    private static bool repairingOrValidating;
    private static int lastFormalValidationFrame = -1;

    static QualityBlockClothesDryingHardwareLodClosure()
    {
        EditorSceneManager.sceneSaved -= OnSceneSaved;
        EditorSceneManager.sceneSaved += OnSceneSaved;
        Camera.onPreCull -= OnCameraPreCull;
        Camera.onPreCull += OnCameraPreCull;
    }

    [MenuItem("NewTown/Geometry/Close Balcony Clothes Hardware LOD Chain")]
    public static void ApplyAndPersist()
    {
        if (repairingOrValidating) return;
        repairingOrValidating = true;
        try
        {
            EnsureBenchmarkSceneOpen();
            ValidateContractConfigOnly();
            if (IsAuthoredDanchiActive())
            {
                Debug.Log("Generated clothes-hardware LOD closure skipped because authored danchi art is authoritative.");
                return;
            }

            GameObject root = FindSceneObject(DetailRootName);
            if (root == null)
                throw new InvalidOperationException("DanchiHighDetail is missing before clothes-hardware LOD closure.");

            EnsureCanonicalClosure(root, true);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveOpenScenes();
            ValidateClosureState(root);
            QualityBlockClothesDryingHardwareRefinement.ValidateOpenScene();
            Debug.Log("Balcony clothes-hardware LOD closure persisted. Visual Fidelity remains UNSCORED pending native 4K and temporal evidence.");
        }
        finally
        {
            repairingOrValidating = false;
        }
    }

    [MenuItem("NewTown/QA/Validate Balcony Clothes Hardware LOD Closure Contract")]
    public static void ValidateContractConfigOnly()
    {
        string absolute = AbsolutePath(ContractPath);
        if (!File.Exists(absolute))
            throw new InvalidOperationException("Missing clothes-hardware LOD closure contract: " + ContractPath);

        string json = File.ReadAllText(absolute);
        string[] required =
        {
            "\"schemaVersion\": \"1.0\"",
            "\"assemblyId\": \"balcony_clothes_pole_receiver_support_lod_closure\"",
            "\"dependsOnAssemblyId\": \"balcony_clothes_pole_receiver_support\"",
            "\"sceneSavedClosureRequired\": true",
            "\"formalCameraReadOnlyValidation\": true",
            "\"rebuildParentLodBeforePromotion\": true",
            "\"compatibleWithFutureUpstreamClassifierFix\": true",
            "\"duplicateLod2ArmProxiesForbidden\": true",
            "\"sourceRendererCount\": 360",
            "\"supportArm\": { \"sourceCount\": 60, \"lodRendererCounts\": [60, 60, 60, 0] }",
            "\"annularReceiver\": { \"sourceCount\": 60, \"lodRendererCounts\": [60, 60, 0, 0] }",
            "\"basePlate\": { \"sourceCount\": 60, \"lodRendererCounts\": [60, 60, 0, 0] }",
            "\"pivot\": { \"sourceCount\": 60, \"lodRendererCounts\": [60, 60, 0, 0] }",
            "\"anchorHead\": { \"sourceCount\": 120, \"lodRendererCounts\": [120, 0, 0, 0] }",
            "\"supportArmPersistsThroughLod2\": true",
            "\"receiverPlatePivotCullBeforeLod2\": true",
            "\"anchorsCullAfterLod0\": true",
            "\"lod3ClothesHardwareCount\": 0",
            "\"visualFidelityPointsAwarded\": 0",
            "\"implementationReadinessOnly\": true",
            "\"runtimeRenderVerification\": \"PENDING_UNITY_RUNTIME\"",
            "\"forbiddenThemes\": [\"earthquake\", \"disaster\", \"reconstruction\"]"
        };
        foreach (string token in required)
            if (json.IndexOf(token, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("Clothes-hardware LOD closure contract missing canonical token: " + token);

        if (!File.Exists(AbsolutePath(LookdevPath)))
            throw new InvalidOperationException("Clothes-hardware lookdev authority is missing: " + LookdevPath);
    }

    [MenuItem("NewTown/QA/Validate Balcony Clothes Hardware LOD Closure")]
    public static void ValidateOpenScene()
    {
        if (repairingOrValidating) return;
        repairingOrValidating = true;
        try
        {
            ValidateContractConfigOnly();
            Scene scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != ScenePath)
                throw new InvalidOperationException("Clothes-hardware LOD closure QA requires the persisted benchmark scene.");
            if (IsAuthoredDanchiActive()) return;

            GameObject root = FindSceneObject(DetailRootName);
            if (root == null)
                throw new InvalidOperationException("DanchiHighDetail is missing during clothes-hardware LOD closure QA.");

            ValidateClosureState(root);
            QualityBlockClothesDryingHardwareRefinement.ValidateOpenScene();
            QualityBlockDanchiLodUpgrade.ValidateOpenScene();
            Debug.Log(
                "Clothes-hardware LOD closure passed: all 360 refined source renderers remain in LOD0; " +
                "arm/receiver/plate/pivot tiers are 60/60/60/60 in LOD1, only 60 support arms persist in LOD2, " +
                "and no clothes hardware survives into LOD3. Native 4K/temporal evidence remains mandatory and Visual Fidelity is UNSCORED.");
        }
        finally
        {
            repairingOrValidating = false;
        }
    }

    private static bool EnsureCanonicalClosure(GameObject root, bool repairAllowed)
    {
        bool changed = false;
        if (root.GetComponent<QualityBlockClothesDryingHardwareManifest>() == null)
        {
            if (!repairAllowed)
                throw new InvalidOperationException("Clothes-hardware manifest is missing; repair is forbidden in formal validation.");
            QualityBlockClothesDryingHardwareRefinement.ApplyToOpenScene();
            root = FindSceneObject(DetailRootName);
            if (root == null || root.GetComponent<QualityBlockClothesDryingHardwareManifest>() == null)
                throw new InvalidOperationException("Clothes-hardware refinement did not install its required manifest.");
            changed = true;
        }

        List<string> errors = CollectClosureErrors(root);
        if (errors.Count > 0)
        {
            if (!repairAllowed)
                throw new InvalidOperationException("Clothes-hardware LOD closure FAILED:\n - " + string.Join("\n - ", errors));

            // Rebuild from every current physical source renderer first. This guarantees that newly added
            // plate/pivot/anchor geometry enters LOD0 and that stale proxies cannot survive the refinement.
            QualityBlockDanchiLodUpgrade.ApplyToOpenScene();
            PromoteSupportArmsIntoLod2(root);
            changed = true;
        }

        QualityBlockDanchiLodUpgrade.ValidateOpenScene();
        ValidateClosureState(root);
        QualityBlockClothesDryingHardwareRefinement.ValidateOpenScene();
        return changed;
    }

    private static void PromoteSupportArmsIntoLod2(GameObject root)
    {
        LODGroup group = root.GetComponent<LODGroup>();
        if (group == null)
            throw new InvalidOperationException("Danchi LODGroup missing after canonical rebuild.");
        LOD[] lods = group.GetLODs();
        if (lods.Length != 4)
            throw new InvalidOperationException("Danchi LODGroup must contain exactly four levels before clothes-arm promotion.");

        MeshRenderer[] sourceArms = SourceRenderers(root, "HD_ClothesBracket_");
        if (sourceArms.Length != ExpectedArms)
            throw new InvalidOperationException($"Expected {ExpectedArms} refined clothes support arms, got {sourceArms.Length}.");

        int alreadyPromoted = CountToken(lods[2].renderers, "HD_ClothesBracket_");
        if (alreadyPromoted == ExpectedArms)
            return; // Future upstream classifier fix: accept it without duplicating proxies.
        if (alreadyPromoted != 0)
            throw new InvalidOperationException($"Partial/duplicate clothes-arm LOD2 ownership detected before promotion: {alreadyPromoted}/{ExpectedArms}.");

        Transform lod2Root = root.transform.Find(Lod2RootName);
        if (lod2Root == null)
            throw new InvalidOperationException("HD_LOD2_Proxy root missing after Danchi LOD rebuild.");

        var lod2Renderers = lods[2].renderers.Where(x => x != null).ToList();
        foreach (MeshRenderer source in sourceArms.OrderBy(x => HierarchyPath(x.transform, root.transform), StringComparer.Ordinal))
            lod2Renderers.Add(CreateProxyRenderer(source, lod2Root));

        LOD lod2 = lods[2];
        lod2.renderers = lod2Renderers.ToArray();
        lods[2] = lod2;
        group.SetLODs(lods);
        group.fadeMode = LODFadeMode.CrossFade;
        group.animateCrossFading = true;
        group.RecalculateBounds();
        EditorUtility.SetDirty(group);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    private static MeshRenderer CreateProxyRenderer(MeshRenderer source, Transform proxyRoot)
    {
        MeshFilter sourceFilter = source.GetComponent<MeshFilter>();
        if (sourceFilter == null || sourceFilter.sharedMesh == null)
            throw new InvalidOperationException("Clothes support arm lacks a source MeshFilter/sharedMesh: " + HierarchyPath(source.transform, null));

        var go = new GameObject("LOD2_" + source.gameObject.name);
        go.layer = source.gameObject.layer;
        go.transform.SetParent(proxyRoot, false);
        go.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        go.transform.localScale = DivideLossyScale(source.transform.lossyScale, proxyRoot.lossyScale);
        GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(source.gameObject));

        go.AddComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = source.sharedMaterials;
        renderer.shadowCastingMode = source.shadowCastingMode;
        renderer.receiveShadows = source.receiveShadows;
        renderer.lightProbeUsage = source.lightProbeUsage;
        renderer.reflectionProbeUsage = source.reflectionProbeUsage;
        renderer.probeAnchor = source.probeAnchor;
        return renderer;
    }

    private static void ValidateClosureState(GameObject root)
    {
        List<string> errors = CollectClosureErrors(root);
        if (errors.Count > 0)
            throw new InvalidOperationException("Clothes-hardware LOD closure FAILED:\n - " + string.Join("\n - ", errors));
    }

    private static List<string> CollectClosureErrors(GameObject root)
    {
        var errors = new List<string>();
        if (root.GetComponent<QualityBlockClothesDryingHardwareManifest>() == null)
            errors.Add("required clothes-hardware manifest is missing");

        MeshRenderer[] arms = SourceRenderers(root, "HD_ClothesBracket_");
        MeshRenderer[] receivers = SourceRenderers(root, "HD_ClothesReceiver_");
        MeshRenderer[] plates = SourceRenderers(root, "HD_ClothesBasePlate_");
        MeshRenderer[] pivots = SourceRenderers(root, "HD_ClothesPivot_");
        MeshRenderer[] anchors = SourceRenderers(root, "HD_ClothesAnchorBolt_");
        int sourceCount = arms.Length + receivers.Length + plates.Length + pivots.Length + anchors.Length;
        if (arms.Length != ExpectedArms) errors.Add($"source support-arm count {arms.Length}/{ExpectedArms}");
        if (receivers.Length != ExpectedReceivers) errors.Add($"source receiver count {receivers.Length}/{ExpectedReceivers}");
        if (plates.Length != ExpectedBasePlates) errors.Add($"source base-plate count {plates.Length}/{ExpectedBasePlates}");
        if (pivots.Length != ExpectedPivots) errors.Add($"source pivot count {pivots.Length}/{ExpectedPivots}");
        if (anchors.Length != ExpectedAnchors) errors.Add($"source anchor count {anchors.Length}/{ExpectedAnchors}");
        if (sourceCount != ExpectedSourceRenderers) errors.Add($"source clothes-hardware renderer total {sourceCount}/{ExpectedSourceRenderers}");

        LODGroup group = root.GetComponent<LODGroup>();
        if (group == null)
        {
            errors.Add("Danchi LODGroup is missing");
            return errors;
        }
        LOD[] lods = group.GetLODs();
        if (lods.Length != 4)
        {
            errors.Add("Danchi LODGroup must have exactly four levels");
            return errors;
        }
        if (group.fadeMode != LODFadeMode.CrossFade || !group.animateCrossFading)
            errors.Add("parent Danchi LODGroup must use animated CrossFade");

        RequireTier(lods[0].renderers, 60, 60, 60, 60, 120, 0, errors);
        RequireTier(lods[1].renderers, 60, 60, 60, 60, 0, 1, errors);
        RequireTier(lods[2].renderers, 60, 0, 0, 0, 0, 2, errors);
        RequireTier(lods[3].renderers, 0, 0, 0, 0, 0, 3, errors);

        var lod0 = new HashSet<Renderer>(lods[0].renderers.Where(x => x != null));
        foreach (Renderer source in arms.Cast<Renderer>().Concat(receivers).Concat(plates).Concat(pivots).Concat(anchors))
            if (!lod0.Contains(source)) errors.Add("refined source renderer missing from LOD0: " + HierarchyPath(source.transform, root.transform));

        Transform lod2Root = root.transform.Find(Lod2RootName);
        if (lod2Root == null)
            errors.Add("HD_LOD2_Proxy root is missing");
        else
        {
            int hierarchyArmProxies = lod2Root.GetComponentsInChildren<MeshRenderer>(true)
                .Count(x => x.gameObject.name.IndexOf("HD_ClothesBracket_", StringComparison.Ordinal) >= 0);
            if (hierarchyArmProxies != ExpectedArms)
                errors.Add($"LOD2 clothes-arm proxy hierarchy count {hierarchyArmProxies}/{ExpectedArms}; partial or duplicate proxy set detected");
        }
        return errors;
    }

    private static void RequireTier(Renderer[] renderers, int arms, int receivers, int plates, int pivots, int anchors, int level, List<string> errors)
    {
        int actualArms = CountToken(renderers, "HD_ClothesBracket_");
        int actualReceivers = CountToken(renderers, "HD_ClothesReceiver_");
        int actualPlates = CountToken(renderers, "HD_ClothesBasePlate_");
        int actualPivots = CountToken(renderers, "HD_ClothesPivot_");
        int actualAnchors = CountToken(renderers, "HD_ClothesAnchorBolt_");
        if (actualArms != arms || actualReceivers != receivers || actualPlates != plates || actualPivots != pivots || actualAnchors != anchors)
            errors.Add(
                $"LOD{level} clothes tier mismatch: arm={actualArms}/{arms}, receiver={actualReceivers}/{receivers}, " +
                $"plate={actualPlates}/{plates}, pivot={actualPivots}/{pivots}, anchor={actualAnchors}/{anchors}");
    }

    private static int CountToken(Renderer[] renderers, string token)
    {
        return (renderers ?? Array.Empty<Renderer>())
            .Count(r => r != null && r.gameObject.name.IndexOf(token, StringComparison.Ordinal) >= 0);
    }

    private static MeshRenderer[] SourceRenderers(GameObject root, string prefix)
    {
        return root.GetComponentsInChildren<MeshRenderer>(true)
            .Where(r => !IsUnderProxyRoot(r.transform, root.transform))
            .Where(r => r.gameObject.name.StartsWith(prefix, StringComparison.Ordinal))
            .ToArray();
    }

    private static bool IsUnderProxyRoot(Transform transform, Transform root)
    {
        for (Transform current = transform; current != null && current != root; current = current.parent)
            if (current.name == "HD_LOD1_Proxy" || current.name == "HD_LOD2_Proxy" || current.name == "HD_LOD3_Proxy")
                return true;
        return false;
    }

    private static void OnSceneSaved(Scene scene)
    {
        if (repairingOrValidating || !scene.IsValid() || scene.path != ScenePath || IsAuthoredDanchiActive()) return;
        GameObject root = FindSceneObject(DetailRootName);
        if (root == null) return;

        repairingOrValidating = true;
        try
        {
            ValidateContractConfigOnly();
            bool changed = EnsureCanonicalClosure(root, true);
            if (!changed) return;

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            // sceneSaved fires after serialization. Persist the repaired LOD ownership in one guarded
            // follow-up save so the next reflection/still/temporal stage sees the exact same hierarchy.
            EditorSceneManager.SaveOpenScenes();
        }
        finally
        {
            repairingOrValidating = false;
        }
    }

    private static void OnCameraPreCull(Camera camera)
    {
        if (camera == null || camera.targetTexture == null || repairingOrValidating ||
            !EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath ||
            !IsFormalEvidenceTarget(camera.targetTexture.name) || lastFormalValidationFrame == Time.frameCount)
            return;

        // Formal evidence is read-only: never rebuild/proxy-promote inside Camera.onPreCull.
        ValidateOpenScene();
        lastFormalValidationFrame = Time.frameCount;
    }

    private static bool IsFormalEvidenceTarget(string name)
    {
        return !string.IsNullOrEmpty(name) &&
               (name.StartsWith("QA4K_", StringComparison.Ordinal) ||
                name.StartsWith("QATemporal_", StringComparison.Ordinal) ||
                name.StartsWith("QAPreparedTemporal_", StringComparison.Ordinal));
    }

    private static bool IsAuthoredDanchiActive()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        return Resources.FindObjectsOfTypeAll<QualityBlockArtSlot>()
            .Any(x => x != null && x.gameObject.scene == scene && x.SlotId == "danchi.main" && x.IsUsingAuthoredArt);
    }

    private static void EnsureBenchmarkSceneOpen()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x != null && x.scene.IsValid() && x.scene.path == ScenePath && x.name == name);
    }

    private static string HierarchyPath(Transform transform, Transform stopExclusive)
    {
        var parts = new Stack<string>();
        for (Transform current = transform; current != null && current != stopExclusive; current = current.parent)
            parts.Push(current.name);
        return string.Join("/", parts.ToArray());
    }

    private static Vector3 DivideLossyScale(Vector3 source, Vector3 parent)
    {
        return new Vector3(SafeDivide(source.x, parent.x), SafeDivide(source.y, parent.y), SafeDivide(source.z, parent.z));
    }

    private static float SafeDivide(float value, float divisor)
    {
        return Mathf.Abs(divisor) < 0.000001f ? value : value / divisor;
    }

    private static string AbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }
}
