using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Fail-closed continuity guard for the DanchiHighDetail LOD chain. LOD1-3 are renderer-subset proxies,
/// so a proxy must retain the exact LOD0 metric-UV mesh, canonical material references and world/render
/// state. Otherwise cross-fading can introduce texture-phase, grain-scale, BRDF or shadow discontinuities
/// even when the LOD0 physical-UV source is valid. This guard awards zero Visual Fidelity points; actual
/// 3840x2160 temporal evidence remains authoritative for visible LOD pop and shimmer.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockDetailLodPhysicalUvBindingQA
{
    private const string ContractPath = "Assets/QA/detail_lod_physical_uv_binding_contract.json";
    private const string GatePath = "Assets/QA/visual_fidelity_gate.json";
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string DetailRootName = "DanchiHighDetail";
    private const string PhysicalUvContractPath = "Assets/QA/detail_physical_uv_contract.json";
    private const string PhysicalUvMeshRoot = "Assets/Art/GeneratedDetailPhysicalUvMeshes";
    private const int Width = 3840;
    private const int Height = 2160;
    private const float TransformEpsilon = 0.0005f;
    private const float RotationEpsilonDegrees = 0.01f;

    private static readonly string[] FormalTargetPrefixes = { "QA4K_", "QAPreparedTemporal_" };
    private static readonly string[] ProxyRoots = { "HD_LOD1_Proxy", "HD_LOD2_Proxy", "HD_LOD3_Proxy" };
    private static readonly float[] TransitionHeights = { 0.18f, 0.08f, 0.03f, 0.008f };
    private static readonly string[] RequiredCrops = { "grazing/sash_rail_response", "oblique/balcony_services" };
    private static readonly string[] RequiredCriticalRisks = { "visible_lod_pop", "severe_aliasing_or_shimmer" };
    private static bool validating;

    static QualityBlockDetailLodPhysicalUvBindingQA()
    {
        Camera.onPreCull -= OnCameraPreCull;
        Camera.onPreCull += OnCameraPreCull;
    }

    [MenuItem("NewTown/QA/Validate Detail LOD Physical UV Binding Contract")]
    public static void ValidateContractConfigOnly()
    {
        BindingContract contract = LoadJson<BindingContract>(ContractPath);
        if (contract == null || !string.Equals(contract.schemaVersion, "1.0", StringComparison.Ordinal))
            throw new InvalidOperationException("Detail LOD physical-UV binding contract is null/unparseable or not schema 1.0.");
        if (!string.Equals(contract.status, "IMPLEMENTATION_READY_RENDER_PENDING", StringComparison.Ordinal))
            throw new InvalidOperationException("Detail LOD physical-UV binding must remain implementation-ready/render-pending until real Unity evidence exists.");
        if (!string.Equals(contract.scenePath, ScenePath, StringComparison.Ordinal) ||
            !string.Equals(contract.detailRootName, DetailRootName, StringComparison.Ordinal) ||
            !string.Equals(contract.detailPhysicalUvContractPath, PhysicalUvContractPath, StringComparison.Ordinal) ||
            !string.Equals(contract.physicalUvMeshRoot, PhysicalUvMeshRoot, StringComparison.Ordinal))
            throw new InvalidOperationException("Detail LOD physical-UV binding canonical path/root identity drifted.");

        RequireExactOrdered(contract.proxyRootNames, ProxyRoots, "proxyRootNames");
        RequireFloatArray(contract.expectedTransitionHeights, TransitionHeights, 0.0001f, "expectedTransitionHeights");

        Requirements r = contract.requirements;
        if (r == null ||
            !r.requireExactlyFourLodLevels ||
            !r.requireAnimatedCrossFade ||
            !r.requireProgressiveRendererReduction ||
            !r.requireProxySharedMeshIdentityWithLod0Source ||
            !r.requireProxySharedMaterialIdentityWithLod0Source ||
            !r.forbidSourceAndProxyRendererPropertyBlocks ||
            !r.requireWorldTransformIdentityWithLod0Source ||
            !r.requireRendererLightingStateIdentityWithLod0Source ||
            !r.requireLayerIdentityWithLod0Source ||
            !r.requirePhysicalUvMeshAssetForCanonicalDetailMaterials ||
            !r.requireCompleteUv0AndTangents ||
            !r.validateBeforeFormalStillAndTemporalFrames ||
            !r.validateBeforeEveryReflectionLightingFingerprint ||
            !r.actualRenderRequiredForVisualPoints)
            throw new InvalidOperationException("Detail LOD physical-UV binding requirements were weakened or are incomplete.");

        if (contract.formalEvidence == null)
            throw new InvalidOperationException("Detail LOD physical-UV formalEvidence is missing.");
        RequireExactSet(contract.formalEvidence.required100PercentCrops, RequiredCrops, "formalEvidence.required100PercentCrops");
        if (contract.formalEvidence.reviewFor == null || contract.formalEvidence.reviewFor.Length < 4 ||
            contract.formalEvidence.reviewFor.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Detail LOD physical-UV review criteria are incomplete.");

        RequireExactSet(contract.criticalDefectRisksReduced, RequiredCriticalRisks, "criticalDefectRisksReduced");
        GateContract gate = LoadJson<GateContract>(GatePath);
        var gateIds = new HashSet<string>((gate.criticalDefects ?? Array.Empty<GateDefect>())
            .Where(x => x != null && !string.IsNullOrWhiteSpace(x.id)).Select(x => x.id), StringComparer.Ordinal);
        if (!RequiredCriticalRisks.All(gateIds.Contains))
            throw new InvalidOperationException("Detail LOD physical-UV binding references a non-canonical Visual Fidelity critical-defect ID.");

        if (contract.visualFidelityPointsAwarded != 0 || contract.implementationReadinessScore != 93 ||
            !string.Equals(contract.visualFidelityStatus, "UNSCORED_UNTIL_REAL_4K_RENDER", StringComparison.Ordinal) ||
            contract.runtimeRenderVerified)
            throw new InvalidOperationException("Detail LOD physical-UV binding must remain readiness=93, render-pending and worth zero automatic Visual Fidelity points.");
        if (contract.limitations == null || contract.limitations.Length < 4 || contract.limitations.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Detail LOD physical-UV binding limitations are missing or incomplete.");

        QualityBlockDetailPhysicalUvQA.ValidateContractConfigOnly();
    }

    [MenuItem("NewTown/QA/Validate Detail LOD Physical UV Binding In Open Scene")]
    public static void ValidateOpenScene()
    {
        ValidateOpenScene(true);
    }

    public static void ValidateOpenScene(bool validateSourcePhysicalUv)
    {
        ValidateContractConfigOnly();
        if (validateSourcePhysicalUv)
            QualityBlockDetailPhysicalUvQA.ValidateOpenScene(false);

        GameObject root = FindSceneObject(DetailRootName);
        if (root == null || !root.activeInHierarchy)
            throw new InvalidOperationException("Active DanchiHighDetail is missing for LOD physical-UV binding validation.");
        LODGroup group = root.GetComponent<LODGroup>();
        if (group == null || !group.enabled)
            throw new InvalidOperationException("Enabled DanchiHighDetail LODGroup is missing.");

        LOD[] lods = group.GetLODs();
        if (lods == null || lods.Length != 4)
            throw new InvalidOperationException("Detail LOD physical-UV binding requires exactly four LOD levels.");
        if (group.fadeMode != LODFadeMode.CrossFade || !group.animateCrossFading)
            throw new InvalidOperationException("Detail LOD physical-UV binding requires animated cross-fading.");
        for (int i = 0; i < TransitionHeights.Length; i++)
            if (Mathf.Abs(lods[i].screenRelativeTransitionHeight - TransitionHeights[i]) > 0.0001f)
                throw new InvalidOperationException($"LOD{i} transition height drifted from the canonical benchmark policy.");

        int[] counts = lods.Select(x => x.renderers == null ? 0 : x.renderers.Length).ToArray();
        if (!(counts[0] > counts[1] && counts[1] >= counts[2] && counts[2] >= counts[3] && counts[3] > 0))
            throw new InvalidOperationException("Detail LOD renderer counts no longer form a valid progressive reduction: " + string.Join("/", counts) + ".");

        MeshRenderer[] sources = RequireMeshRenderers(lods[0].renderers, "LOD0");
        if (sources.Length == 0)
            throw new InvalidOperationException("LOD0 has no source renderers.");
        foreach (MeshRenderer source in sources)
            ValidatePhysicalSource(source, "LOD0 source");

        for (int level = 1; level <= 3; level++)
        {
            Transform proxyRoot = root.transform.Find(ProxyRoots[level - 1]);
            if (proxyRoot == null || !proxyRoot.gameObject.activeInHierarchy)
                throw new InvalidOperationException($"Active {ProxyRoots[level - 1]} is missing.");

            MeshRenderer[] proxies = RequireMeshRenderers(lods[level].renderers, "LOD" + level);
            var pairedSources = new HashSet<MeshRenderer>();
            foreach (MeshRenderer proxy in proxies)
            {
                if (!IsDescendantOf(proxy.transform, proxyRoot))
                    throw new InvalidOperationException($"LOD{level} renderer {proxy.name} is not owned by {ProxyRoots[level - 1]}.");
                MeshRenderer source = FindUniqueSourceForProxy(proxy, sources, level);
                if (!pairedSources.Add(source))
                    throw new InvalidOperationException($"LOD{level} source {HierarchyPath(source.transform, root.transform)} is represented by more than one proxy.");
                ValidateProxyIdentity(source, proxy, root.transform, level);
            }

            int owned = proxyRoot.GetComponentsInChildren<MeshRenderer>(true).Length;
            if (owned != proxies.Length)
                throw new InvalidOperationException($"{ProxyRoots[level - 1]} contains {owned} MeshRenderers but LOD{level} declares {proxies.Length}.");
        }
    }

    private static void ValidatePhysicalSource(MeshRenderer renderer, string label)
    {
        if (renderer == null || !renderer.gameObject.activeInHierarchy || !renderer.enabled)
            throw new InvalidOperationException(label + " is null/inactive/disabled.");
        if (renderer.HasPropertyBlock())
            throw new InvalidOperationException(label + " uses a MaterialPropertyBlock; exact LOD material continuity cannot be proven.");
        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        Mesh mesh = filter != null ? filter.sharedMesh : null;
        if (mesh == null)
            throw new InvalidOperationException(label + " has no shared mesh.");
        Material[] materials = renderer.sharedMaterials;
        if (materials == null || materials.Length == 0 || materials.Any(x => x == null))
            throw new InvalidOperationException(label + " has missing materials.");
        if (materials.Any(QualityBlockDetailPhysicalUvUpgrade.IsCanonicalDetailMaterial))
        {
            string path = AssetDatabase.GetAssetPath(mesh) ?? string.Empty;
            if (!path.StartsWith(PhysicalUvMeshRoot + "/", StringComparison.Ordinal))
                throw new InvalidOperationException(label + " does not use a persisted metric physical-UV mesh: " + path);
            if (mesh.uv == null || mesh.uv.Length != mesh.vertexCount || mesh.tangents == null || mesh.tangents.Length != mesh.vertexCount)
                throw new InvalidOperationException(label + " physical-UV mesh lacks complete UV0/tangent data.");
        }
    }

    private static MeshRenderer FindUniqueSourceForProxy(MeshRenderer proxy, MeshRenderer[] sources, int level)
    {
        string prefix = "LOD" + level + "_";
        if (!proxy.gameObject.name.StartsWith(prefix, StringComparison.Ordinal))
            throw new InvalidOperationException($"LOD{level} proxy name is non-canonical: {proxy.gameObject.name}.");
        string sourceName = proxy.gameObject.name.Substring(prefix.Length);
        MeshFilter proxyFilter = proxy.GetComponent<MeshFilter>();
        Mesh proxyMesh = proxyFilter != null ? proxyFilter.sharedMesh : null;
        if (proxyMesh == null)
            throw new InvalidOperationException($"LOD{level} proxy {proxy.gameObject.name} has no shared mesh.");

        MeshRenderer[] candidates = sources.Where(source =>
        {
            if (!string.Equals(source.gameObject.name, sourceName, StringComparison.Ordinal)) return false;
            MeshFilter sf = source.GetComponent<MeshFilter>();
            if (sf == null || sf.sharedMesh != proxyMesh) return false;
            if (!MaterialsIdentical(source.sharedMaterials, proxy.sharedMaterials)) return false;
            return TransformIdentical(source.transform, proxy.transform);
        }).ToArray();

        if (candidates.Length != 1)
            throw new InvalidOperationException($"LOD{level} proxy {proxy.gameObject.name} resolves to {candidates.Length} exact LOD0 sources; expected exactly one.");
        return candidates[0];
    }

    private static void ValidateProxyIdentity(MeshRenderer source, MeshRenderer proxy, Transform detailRoot, int level)
    {
        Mesh sourceMesh = source.GetComponent<MeshFilter>()?.sharedMesh;
        Mesh proxyMesh = proxy.GetComponent<MeshFilter>()?.sharedMesh;
        if (sourceMesh == null || proxyMesh != sourceMesh)
            throw new InvalidOperationException($"LOD{level} proxy mesh identity drifted for {HierarchyPath(source.transform, detailRoot)}.");
        if (!MaterialsIdentical(source.sharedMaterials, proxy.sharedMaterials))
            throw new InvalidOperationException($"LOD{level} proxy material identity drifted for {HierarchyPath(source.transform, detailRoot)}.");
        if (source.HasPropertyBlock() || proxy.HasPropertyBlock())
            throw new InvalidOperationException($"LOD{level} source/proxy MaterialPropertyBlock would break exact material continuity for {HierarchyPath(source.transform, detailRoot)}.");
        if (!TransformIdentical(source.transform, proxy.transform))
            throw new InvalidOperationException($"LOD{level} proxy world transform drifted for {HierarchyPath(source.transform, detailRoot)}.");
        if (source.gameObject.layer != proxy.gameObject.layer)
            throw new InvalidOperationException($"LOD{level} proxy layer drifted for {HierarchyPath(source.transform, detailRoot)}.");
        if (source.enabled != proxy.enabled || !proxy.gameObject.activeInHierarchy)
            throw new InvalidOperationException($"LOD{level} proxy enable/active state drifted for {HierarchyPath(source.transform, detailRoot)}.");
        if (source.shadowCastingMode != proxy.shadowCastingMode ||
            source.receiveShadows != proxy.receiveShadows ||
            source.lightProbeUsage != proxy.lightProbeUsage ||
            source.reflectionProbeUsage != proxy.reflectionProbeUsage)
            throw new InvalidOperationException($"LOD{level} proxy lighting/probe state drifted for {HierarchyPath(source.transform, detailRoot)}.");

        ValidatePhysicalSource(proxy, "LOD" + level + " proxy " + proxy.gameObject.name);
    }

    private static bool MaterialsIdentical(Material[] a, Material[] b)
    {
        if (a == null || b == null || a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if (a[i] != b[i]) return false;
        return true;
    }

    private static bool TransformIdentical(Transform a, Transform b)
    {
        if (a == null || b == null) return false;
        if (Vector3.Distance(a.position, b.position) > TransformEpsilon) return false;
        if (Quaternion.Angle(a.rotation, b.rotation) > RotationEpsilonDegrees) return false;
        Vector3 sa = a.lossyScale;
        Vector3 sb = b.lossyScale;
        return Mathf.Abs(sa.x - sb.x) <= TransformEpsilon &&
               Mathf.Abs(sa.y - sb.y) <= TransformEpsilon &&
               Mathf.Abs(sa.z - sb.z) <= TransformEpsilon;
    }

    private static MeshRenderer[] RequireMeshRenderers(Renderer[] renderers, string label)
    {
        if (renderers == null || renderers.Any(x => x == null))
            throw new InvalidOperationException(label + " contains a null renderer reference.");
        if (renderers.Any(x => !(x is MeshRenderer)))
            throw new InvalidOperationException(label + " contains a non-MeshRenderer entry.");
        return renderers.Cast<MeshRenderer>().ToArray();
    }

    private static bool IsDescendantOf(Transform child, Transform ancestor)
    {
        for (Transform t = child; t != null; t = t.parent)
            if (t == ancestor) return true;
        return false;
    }

    private static string HierarchyPath(Transform transform, Transform root)
    {
        var parts = new Stack<string>();
        for (Transform t = transform; t != null && t != root; t = t.parent)
            parts.Push(t.name);
        return string.Join("/", parts.ToArray());
    }

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x.scene.IsValid() && x.scene.path == ScenePath && x.name == name);
    }

    private static void OnCameraPreCull(Camera camera)
    {
        if (!IsFormalEvidenceCamera(camera)) return;
        if (validating)
            throw new InvalidOperationException("Detail LOD physical-UV binding QA re-entered during formal pre-cull.");
        validating = true;
        try
        {
            ValidateOpenScene(true);
        }
        finally
        {
            validating = false;
        }
    }

    private static bool IsFormalEvidenceCamera(Camera camera)
    {
        if (camera == null || camera.targetTexture == null || camera.targetTexture.width != Width || camera.targetTexture.height != Height)
            return false;
        string name = camera.targetTexture.name ?? string.Empty;
        return FormalTargetPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static void RequireExactSet(string[] actual, string[] expected, string label)
    {
        if (actual == null || actual.Any(string.IsNullOrWhiteSpace) || actual.Distinct(StringComparer.Ordinal).Count() != actual.Length ||
            !new HashSet<string>(actual, StringComparer.Ordinal).SetEquals(expected ?? Array.Empty<string>()))
            throw new InvalidOperationException(label + " must be exactly [" + string.Join(", ", expected ?? Array.Empty<string>()) + "].");
    }

    private static void RequireExactOrdered(string[] actual, string[] expected, string label)
    {
        if (actual == null || expected == null || actual.Length != expected.Length)
            throw new InvalidOperationException(label + " length drifted.");
        for (int i = 0; i < expected.Length; i++)
            if (!string.Equals(actual[i], expected[i], StringComparison.Ordinal))
                throw new InvalidOperationException(label + " order/value drifted at index " + i + ".");
    }

    private static void RequireFloatArray(float[] actual, float[] expected, float epsilon, string label)
    {
        if (actual == null || expected == null || actual.Length != expected.Length)
            throw new InvalidOperationException(label + " length drifted.");
        for (int i = 0; i < expected.Length; i++)
            if (!float.IsFinite(actual[i]) || Mathf.Abs(actual[i] - expected[i]) > epsilon)
                throw new InvalidOperationException(label + " drifted at index " + i + ".");
    }

    private static T LoadJson<T>(string assetPath)
    {
        string absolute = AbsolutePath(assetPath);
        if (!File.Exists(absolute))
            throw new FileNotFoundException("Required QA file not found: " + assetPath);
        T value = JsonUtility.FromJson<T>(File.ReadAllText(absolute));
        if (value == null)
            throw new InvalidOperationException("Could not parse QA JSON: " + assetPath);
        return value;
    }

    private static string AbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    [Serializable]
    private sealed class BindingContract
    {
        public string schemaVersion;
        public string status;
        public string purpose;
        public string scenePath;
        public string detailRootName;
        public string detailPhysicalUvContractPath;
        public string physicalUvMeshRoot;
        public string[] proxyRootNames;
        public Requirements requirements;
        public float[] expectedTransitionHeights;
        public FormalEvidence formalEvidence;
        public string[] criticalDefectRisksReduced;
        public int implementationReadinessScore;
        public int visualFidelityPointsAwarded;
        public string visualFidelityStatus;
        public bool runtimeRenderVerified;
        public string[] limitations;
    }

    [Serializable]
    private sealed class Requirements
    {
        public bool requireExactlyFourLodLevels;
        public bool requireAnimatedCrossFade;
        public bool requireProgressiveRendererReduction;
        public bool requireProxySharedMeshIdentityWithLod0Source;
        public bool requireProxySharedMaterialIdentityWithLod0Source;
        public bool forbidSourceAndProxyRendererPropertyBlocks;
        public bool requireWorldTransformIdentityWithLod0Source;
        public bool requireRendererLightingStateIdentityWithLod0Source;
        public bool requireLayerIdentityWithLod0Source;
        public bool requirePhysicalUvMeshAssetForCanonicalDetailMaterials;
        public bool requireCompleteUv0AndTangents;
        public bool validateBeforeFormalStillAndTemporalFrames;
        public bool validateBeforeEveryReflectionLightingFingerprint;
        public bool actualRenderRequiredForVisualPoints;
    }

    [Serializable]
    private sealed class FormalEvidence
    {
        public string[] required100PercentCrops;
        public string[] reviewFor;
    }

    [Serializable]
    private sealed class GateContract
    {
        public GateDefect[] criticalDefects;
    }

    [Serializable]
    private sealed class GateDefect
    {
        public string id;
    }
}
