using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Fail-closed integrity gate for the DanchiHighDetail LOD proxies. A cross-fade is only visually useful
/// if the proxy is the same physical/material state as its source at the transition boundary; otherwise
/// probe sampling, per-renderer PBR overrides, transforms or shadow settings can visibly jump.
/// This class validates only. Native rendered evidence is still required for any Visual Fidelity score.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockDanchiLodProxyFidelityQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string DetailRootName = "DanchiHighDetail";
    private const string ContractPath = "Assets/QA/danchi_lod_proxy_fidelity_contract.json";
    private static readonly string[] ProxyRootNames = { "", "HD_LOD1_Proxy", "HD_LOD2_Proxy", "HD_LOD3_Proxy" };
    private const float PositionToleranceM = 0.0005f;
    private const float RotationToleranceDeg = 0.05f;
    private const float ScaleTolerance = 0.001f;
    private static int lastFormalValidationFrame = -1;
    private static bool validating;

    private static readonly int[] FloatPropertyIds =
    {
        Shader.PropertyToID("_Metallic"), Shader.PropertyToID("_Glossiness"), Shader.PropertyToID("_Smoothness"),
        Shader.PropertyToID("_BumpScale"), Shader.PropertyToID("_OcclusionStrength"), Shader.PropertyToID("_Wetness"),
        Shader.PropertyToID("_LeafVariation"), Shader.PropertyToID("_ExposureBias"), Shader.PropertyToID("_Cutoff")
    };

    private static readonly int[] ColorPropertyIds =
    {
        Shader.PropertyToID("_Color"), Shader.PropertyToID("_BaseColor"), Shader.PropertyToID("_EmissionColor")
    };

    private static readonly int[] TexturePropertyIds =
    {
        Shader.PropertyToID("_MainTex"), Shader.PropertyToID("_BaseMap"), Shader.PropertyToID("_BumpMap"),
        Shader.PropertyToID("_MetallicGlossMap"), Shader.PropertyToID("_OcclusionMap"), Shader.PropertyToID("_DetailAlbedoMap"),
        Shader.PropertyToID("_DetailNormalMap"), Shader.PropertyToID("_MacroVariationMap")
    };

    static QualityBlockDanchiLodProxyFidelityQA()
    {
        Camera.onPreCull -= OnCameraPreCull;
        Camera.onPreCull += OnCameraPreCull;
    }

    [MenuItem("NewTown/QA/Validate Danchi LOD Proxy Fidelity Contract")]
    public static void ValidateContractConfigOnly()
    {
        string absolute = AbsolutePath(ContractPath);
        if (!File.Exists(absolute))
            throw new InvalidOperationException("Missing Danchi LOD proxy-fidelity contract: " + ContractPath);

        string json = File.ReadAllText(absolute);
        string[] requiredTokens =
        {
            "\"schemaVersion\": \"1.0\"",
            "\"contractId\": \"danchi_lod_proxy_fidelity\"",
            "\"formalCameraReadOnlyValidation\": true",
            "\"repairDuringCaptureForbidden\": true",
            "\"clothesSupportRetainedThroughLod\": 2",
            "\"specificRuleMustPrecedeGenericBracketRule\": true",
            "\"worldPositionToleranceM\": 0.0005",
            "\"worldRotationToleranceDeg\": 0.05",
            "\"lossyScaleTolerance\": 0.001",
            "\"requireMaterialPropertyBlockParity\": true",
            "\"preventAlbedoRoughnessWetnessJump\": true",
            "\"preventReflectionProbeJump\": true",
            "\"visualFidelityPointsAwarded\": 0",
            "\"implementationReadinessOnly\": true",
            "\"runtimeRenderVerification\": \"PENDING_UNITY_RUNTIME\"",
            "\"forbiddenThemes\": [\"earthquake\", \"disaster\", \"reconstruction\"]"
        };
        foreach (string token in requiredTokens)
            if (json.IndexOf(token, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("Danchi LOD proxy-fidelity contract missing canonical token: " + token);
    }

    [MenuItem("NewTown/QA/Validate Danchi LOD Proxy Fidelity")]
    public static void ValidateOpenScene()
    {
        if (validating) return;
        validating = true;
        try
        {
            ValidateContractConfigOnly();
            Scene scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != ScenePath)
                throw new InvalidOperationException("Danchi LOD proxy-fidelity QA requires the persisted benchmark scene.");
            if (IsAuthoredDanchiActive()) return;

            GameObject root = FindSceneObject(DetailRootName);
            if (root == null)
                throw new InvalidOperationException("DanchiHighDetail is missing during LOD proxy-fidelity QA.");

            LODGroup group = root.GetComponent<LODGroup>();
            if (group == null)
                throw new InvalidOperationException("DanchiHighDetail LODGroup is missing during proxy-fidelity QA.");
            LOD[] lods = group.GetLODs();
            if (lods.Length != 4)
                throw new InvalidOperationException("Danchi LOD proxy-fidelity QA requires exactly four LOD levels.");

            MeshRenderer[] sources = SourceRenderers(root);
            var sourceSet = new HashSet<Renderer>(sources);
            var lod0Set = new HashSet<Renderer>(lods[0].renderers.Where(x => x != null));
            if (!sourceSet.SetEquals(lod0Set))
                throw new InvalidOperationException(
                    $"LOD0 must be the complete physical source set before proxy review: sources={sourceSet.Count}, LOD0={lod0Set.Count}.");

            var allProxyReferences = new HashSet<Renderer>();
            for (int level = 1; level <= 3; level++)
            {
                Transform proxyRoot = root.transform.Find(ProxyRootNames[level]);
                if (proxyRoot == null)
                    throw new InvalidOperationException($"Missing canonical LOD{level} proxy root {ProxyRootNames[level]}.");

                Renderer[] hierarchyRenderers = proxyRoot.GetComponentsInChildren<Renderer>(true);
                var declared = new HashSet<Renderer>(lods[level].renderers.Where(x => x != null));
                var hierarchy = new HashSet<Renderer>(hierarchyRenderers);
                if (!declared.SetEquals(hierarchy))
                    throw new InvalidOperationException(
                        $"LOD{level} declared/hierarchy renderer set mismatch: declared={declared.Count}, hierarchy={hierarchy.Count}.");

                foreach (Renderer renderer in declared)
                {
                    if (!allProxyReferences.Add(renderer))
                        throw new InvalidOperationException("A proxy Renderer is reused across multiple LOD levels: " + renderer.name);
                    MeshRenderer proxy = renderer as MeshRenderer;
                    if (proxy == null)
                        throw new InvalidOperationException($"LOD{level} contains a non-MeshRenderer proxy: {renderer.name}");
                    ValidateProxyAgainstSource(root, sources, proxy, level);
                }
            }

            int sourceClothesArms = sources.Count(x =>
                x.gameObject.name.IndexOf("HD_ClothesBracket_", StringComparison.OrdinalIgnoreCase) >= 0);
            if (sourceClothesArms > 0)
            {
                int lod2ClothesArms = lods[2].renderers.Count(x => x != null &&
                    x.gameObject.name.IndexOf("HD_ClothesBracket_", StringComparison.OrdinalIgnoreCase) >= 0);
                if (lod2ClothesArms != sourceClothesArms)
                    throw new InvalidOperationException(
                        $"Clothes support-arm source/classifier closure mismatch: source={sourceClothesArms}, LOD2={lod2ClothesArms}.");
            }

            QualityBlockDanchiLodUpgrade.ValidateOpenScene();
            Debug.Log(
                $"Danchi LOD proxy-fidelity QA passed structurally for {sources.Length} physical sources and {allProxyReferences.Count} proxies. " +
                "Transform/mesh/material/probe/shadow/MPB continuity is source-consistent; native temporal evidence is still required and Visual Fidelity remains UNSCORED.");
        }
        finally
        {
            validating = false;
        }
    }

    private static void ValidateProxyAgainstSource(GameObject root, MeshRenderer[] sources, MeshRenderer proxy, int level)
    {
        string prefix = $"LOD{level}_";
        if (!proxy.gameObject.name.StartsWith(prefix, StringComparison.Ordinal))
            throw new InvalidOperationException($"LOD{level} proxy name lacks canonical prefix: {proxy.gameObject.name}");
        string sourceName = proxy.gameObject.name.Substring(prefix.Length);

        MeshRenderer[] matches = sources.Where(source =>
                source.gameObject.name == sourceName &&
                Vector3.Distance(source.transform.position, proxy.transform.position) <= PositionToleranceM &&
                Quaternion.Angle(source.transform.rotation, proxy.transform.rotation) <= RotationToleranceDeg &&
                Vector3.Distance(source.transform.lossyScale, proxy.transform.lossyScale) <= ScaleTolerance)
            .ToArray();
        if (matches.Length != 1)
            throw new InvalidOperationException(
                $"LOD{level} proxy must resolve to exactly one physical source by name/world pose, got {matches.Length}: {proxy.gameObject.name}");

        MeshRenderer source = matches[0];
        MeshFilter sourceFilter = source.GetComponent<MeshFilter>();
        MeshFilter proxyFilter = proxy.GetComponent<MeshFilter>();
        if (sourceFilter == null || proxyFilter == null || sourceFilter.sharedMesh == null ||
            proxyFilter.sharedMesh != sourceFilter.sharedMesh)
            throw new InvalidOperationException($"LOD{level} mesh identity drift: {proxy.gameObject.name}");

        Material[] sourceMaterials = source.sharedMaterials;
        Material[] proxyMaterials = proxy.sharedMaterials;
        if (sourceMaterials.Length != proxyMaterials.Length)
            throw new InvalidOperationException($"LOD{level} material-slot count drift: {proxy.gameObject.name}");
        for (int i = 0; i < sourceMaterials.Length; i++)
            if (sourceMaterials[i] != proxyMaterials[i])
                throw new InvalidOperationException($"LOD{level} material reference drift at slot {i}: {proxy.gameObject.name}");

        if (source.gameObject.layer != proxy.gameObject.layer ||
            GameObjectUtility.GetStaticEditorFlags(source.gameObject) != GameObjectUtility.GetStaticEditorFlags(proxy.gameObject))
            throw new InvalidOperationException($"LOD{level} layer/static-state drift: {proxy.gameObject.name}");

        if (source.shadowCastingMode != proxy.shadowCastingMode ||
            source.receiveShadows != proxy.receiveShadows ||
            source.lightProbeUsage != proxy.lightProbeUsage ||
            source.reflectionProbeUsage != proxy.reflectionProbeUsage ||
            source.probeAnchor != proxy.probeAnchor ||
            source.motionVectorGenerationMode != proxy.motionVectorGenerationMode ||
            source.allowOcclusionWhenDynamic != proxy.allowOcclusionWhenDynamic ||
            source.sortingLayerID != proxy.sortingLayerID ||
            source.sortingOrder != proxy.sortingOrder)
            throw new InvalidOperationException($"LOD{level} renderer/probe/temporal state drift: {proxy.gameObject.name}");

        ValidatePropertyBlocks(source, proxy, level);
    }

    private static void ValidatePropertyBlocks(MeshRenderer source, MeshRenderer proxy, int level)
    {
        if (source.HasPropertyBlock() != proxy.HasPropertyBlock())
            throw new InvalidOperationException($"LOD{level} MaterialPropertyBlock presence drift: {proxy.gameObject.name}");

        var sourceBlock = new MaterialPropertyBlock();
        var proxyBlock = new MaterialPropertyBlock();
        source.GetPropertyBlock(sourceBlock);
        proxy.GetPropertyBlock(proxyBlock);
        CompareBlock(sourceBlock, proxyBlock, $"LOD{level}/{proxy.gameObject.name}/renderer-wide");

        int slots = source.sharedMaterials.Length;
        for (int i = 0; i < slots; i++)
        {
            sourceBlock.Clear();
            proxyBlock.Clear();
            source.GetPropertyBlock(sourceBlock, i);
            proxy.GetPropertyBlock(proxyBlock, i);
            CompareBlock(sourceBlock, proxyBlock, $"LOD{level}/{proxy.gameObject.name}/material[{i}]");
        }
    }

    private static void CompareBlock(MaterialPropertyBlock source, MaterialPropertyBlock proxy, string label)
    {
        if (source.isEmpty != proxy.isEmpty)
            throw new InvalidOperationException("MaterialPropertyBlock empty-state drift: " + label);

        foreach (int id in FloatPropertyIds)
            if (Mathf.Abs(source.GetFloat(id) - proxy.GetFloat(id)) > 0.0001f)
                throw new InvalidOperationException("MaterialPropertyBlock float PBR override drift: " + label);
        foreach (int id in ColorPropertyIds)
            if (Vector4.Distance((Vector4)source.GetColor(id), (Vector4)proxy.GetColor(id)) > 0.0001f)
                throw new InvalidOperationException("MaterialPropertyBlock color PBR override drift: " + label);
        foreach (int id in TexturePropertyIds)
            if (source.GetTexture(id) != proxy.GetTexture(id))
                throw new InvalidOperationException("MaterialPropertyBlock texture override drift: " + label);
    }

    private static MeshRenderer[] SourceRenderers(GameObject root)
    {
        return root.GetComponentsInChildren<MeshRenderer>(true)
            .Where(x => !IsUnderProxyRoot(x.transform, root.transform))
            .ToArray();
    }

    private static bool IsUnderProxyRoot(Transform transform, Transform detailRoot)
    {
        for (Transform current = transform; current != null && current != detailRoot; current = current.parent)
            if (current.name == "HD_LOD1_Proxy" || current.name == "HD_LOD2_Proxy" || current.name == "HD_LOD3_Proxy")
                return true;
        return false;
    }

    private static void OnCameraPreCull(Camera camera)
    {
        if (camera == null || camera.targetTexture == null || validating ||
            !EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath ||
            !IsFormalEvidenceTarget(camera.targetTexture.name) || lastFormalValidationFrame == Time.frameCount)
            return;
        lastFormalValidationFrame = Time.frameCount;
        ValidateOpenScene();
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

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x.scene.IsValid() && x.name == name);
    }

    private static string AbsolutePath(string assetPath)
    {
        string root = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
        return Path.Combine(root, assetPath.Replace('/', Path.DirectorySeparatorChar));
    }
}
