using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Fail-closed source/runtime QA for manufacturing-scale UVs on the high-detail danchi fallback.
/// Deep validation proves metric chamfer-box edge mapping and deterministic phase diversity. Every
/// formal 4K/temporal pre-cull repeats the lightweight binding/scale/phase checks. No visual points
/// are awarded by this class; rendered repetition, seams and shimmer remain actual-pixel judgments.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockDetailPhysicalUvQA
{
    private const string ContractPath = "Assets/QA/detail_physical_uv_contract.json";
    private const string UpgradePath = "Assets/Editor/QualityBlockDetailPhysicalUvUpgrade.cs";
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const int Width = 3840;
    private const int Height = 2160;
    private const float Epsilon = 0.0005f;

    private static readonly string[] FormalTargetPrefixes = { "QA4K_", "QAPreparedTemporal_" };
    private static readonly string[] RequiredCriticalRisks = { "obvious_repetition", "severe_aliasing_or_shimmer" };
    private static readonly string[] RequiredCrops =
    {
        "grazing/material_grazing",
        "grazing/sash_rail_response",
        "oblique/balcony_services",
    };

    private static bool validating;

    static QualityBlockDetailPhysicalUvQA()
    {
        Camera.onPreCull -= OnCameraPreCull;
        Camera.onPreCull += OnCameraPreCull;
    }

    [MenuItem("NewTown/QA/Validate Detail Physical UV Contract")]
    public static void ValidateContractConfigOnly()
    {
        Contract contract = LoadContract();
        if (!string.Equals(contract.schemaVersion, "1.0", StringComparison.Ordinal) ||
            !string.Equals(contract.status, "IMPLEMENTATION_READY_RENDER_PENDING", StringComparison.Ordinal))
            throw new InvalidOperationException("Detail physical-UV contract schema/status drifted or claims rendered completion.");
        if (!string.Equals(contract.upgrade, UpgradePath, StringComparison.Ordinal) ||
            !string.Equals(contract.scenePath, ScenePath, StringComparison.Ordinal) ||
            !string.Equals(contract.detailRootName, QualityBlockDetailPhysicalUvUpgrade.DetailRoot, StringComparison.Ordinal) ||
            !string.Equals(contract.generatedMeshRoot, QualityBlockDetailPhysicalUvUpgrade.MeshAssetRoot, StringComparison.Ordinal))
            throw new InvalidOperationException("Detail physical-UV contract source/scene/root identity drifted.");

        if (contract.uvPolicy == null || !string.Equals(contract.uvPolicy.units, "meters", StringComparison.Ordinal) ||
            contract.uvPolicy.phaseBinsPerAxis != QualityBlockDetailPhysicalUvUpgrade.PhaseBins ||
            Mathf.Abs(contract.uvPolicy.phaseStepMeters - QualityBlockDetailPhysicalUvUpgrade.PhaseStep) > Epsilon ||
            !contract.uvPolicy.materialPropertyBlocksForbidden ||
            !contract.uvPolicy.normalAndMaskSharePrimaryMetricTransform ||
            string.IsNullOrWhiteSpace(contract.uvPolicy.boxProjection) ||
            string.IsNullOrWhiteSpace(contract.uvPolicy.cylinderProjection) ||
            string.IsNullOrWhiteSpace(contract.uvPolicy.phaseReasoning))
            throw new InvalidOperationException("Detail physical-UV projection/phase policy was weakened or drifted.");

        if (contract.materials == null || contract.materials.Length != 6)
            throw new InvalidOperationException("Detail physical-UV contract must define exactly six canonical materials.");
        if (contract.materials.Any(x => x == null || string.IsNullOrWhiteSpace(x.materialName) ||
            string.IsNullOrWhiteSpace(x.materialId) || string.IsNullOrWhiteSpace(x.physicalInterpretation) ||
            !IsFinitePositive(x.repeatsPerMeter) || !IsFinitePositive(x.tileMeters) ||
            Mathf.Abs(x.tileMeters - 1f / x.repeatsPerMeter) > 0.0002f))
            throw new InvalidOperationException("Detail physical-UV material scale metadata is incomplete or inconsistent.");
        if (contract.materials.Select(x => x.materialName).Distinct(StringComparer.Ordinal).Count() != 6)
            throw new InvalidOperationException("Detail physical-UV material names must be unique.");

        Qa qa = contract.qa;
        if (qa == null || qa.minimumSourceRenderers < 400 || qa.minimumDistinctPhasePairs < 16 ||
            qa.requiredCanonicalMaterials != 6 || qa.boxMetricEdgeErrorFractionMax <= 0f || qa.boxMetricEdgeErrorFractionMax > 0.05f ||
            qa.automaticVisualPoints != 0 || !qa.renderVerificationPending)
            throw new InvalidOperationException("Detail physical-UV QA thresholds were weakened or readiness was inflated.");
        RequireExactSet(qa.requiredTextureProperties, new[] { "_MainTex", "_BumpMap", "_MetallicGlossMap" }, "required texture properties");
        if (qa.requiredTextureOffset == null || qa.requiredTextureOffset.Length != 2 ||
            Mathf.Abs(qa.requiredTextureOffset[0]) > Epsilon || Mathf.Abs(qa.requiredTextureOffset[1]) > Epsilon)
            throw new InvalidOperationException("Detail physical-UV canonical texture offset must remain zero.");

        if (contract.formalEvidence == null || !contract.formalEvidence.actualRenderRequiredForVisualPoints ||
            contract.formalEvidence.reviewFor == null || contract.formalEvidence.reviewFor.Length < 6 ||
            contract.formalEvidence.reviewFor.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Detail physical-UV formal evidence requirements are incomplete.");
        RequireExactSet(contract.formalEvidence.required100PercentCrops, RequiredCrops, "required 100% crops");
        RequireExactSet(contract.criticalDefectRisksReduced, RequiredCriticalRisks, "critical defect risks");

        if (contract.limitations == null || contract.limitations.Length < 4 || contract.limitations.Any(string.IsNullOrWhiteSpace) ||
            contract.implementationReadinessScore != 93 ||
            !string.Equals(contract.visualFidelityStatus, "UNSCORED_UNTIL_REAL_4K_RENDER", StringComparison.Ordinal) ||
            contract.runtimeRenderVerified)
            throw new InvalidOperationException("Detail physical-UV contract must remain render-pending with explicit limitations and readiness=93.");
    }

    [MenuItem("NewTown/QA/Validate Detail Physical UV Scene")]
    public static void ValidateOpenScene()
    {
        ValidateOpenScene(true);
        Debug.Log("Detail physical-UV QA passed source/runtime geometry checks. Repetition, seams and shimmer remain UNSCORED until native 4K/temporal evidence exists.");
    }

    public static void ValidateOpenScene(bool deepMetricValidation)
    {
        ValidateContractConfigOnly();
        EnsureSceneOpen();
        Contract contract = LoadContract();
        Dictionary<string, MaterialEntry> materials = contract.materials.ToDictionary(x => x.materialName, StringComparer.Ordinal);

        GameObject root = FindSceneObject(QualityBlockDetailPhysicalUvUpgrade.DetailRoot);
        if (root == null)
            throw new InvalidOperationException("DanchiHighDetail is missing for physical-UV validation.");
        QualityBlockDetailPhysicalUvManifest manifest = root.GetComponent<QualityBlockDetailPhysicalUvManifest>();
        if (manifest == null)
            throw new InvalidOperationException("Detail physical-UV manifest is missing; metric phase conversion did not run.");

        MeshRenderer[] renderers = SourceRenderers(root);
        if (renderers.Length < contract.qa.minimumSourceRenderers)
            throw new InvalidOperationException($"Detail physical-UV source renderer count too low: {renderers.Length} < {contract.qa.minimumSourceRenderers}.");
        if (manifest.SourceRendererCount != renderers.Length ||
            manifest.CanonicalMaterialCount != contract.qa.requiredCanonicalMaterials ||
            manifest.DistinctPhasePairCount < contract.qa.minimumDistinctPhasePairs ||
            Mathf.Abs(manifest.PhaseStepMeters - contract.uvPolicy.phaseStepMeters) > Epsilon ||
            manifest.PhaseBinsPerAxis != contract.uvPolicy.phaseBinsPerAxis)
            throw new InvalidOperationException("Detail physical-UV manifest does not match the prepared source renderer/phase/material state.");

        var phasePairs = new HashSet<string>(StringComparer.Ordinal);
        var materialNames = new HashSet<string>(StringComparer.Ordinal);
        var deepValidatedMeshes = new HashSet<Mesh>();

        foreach (MeshRenderer renderer in renderers)
        {
            Material material = renderer.sharedMaterial;
            if (material == null || !materials.TryGetValue(material.name, out MaterialEntry materialEntry))
                throw new InvalidOperationException("Unregistered material on detail physical-UV renderer: " + HierarchyPath(renderer.transform));
            materialNames.Add(material.name);
            ValidateTextureTransform(material, materialEntry.repeatsPerMeter, contract.qa.requiredTextureProperties);

            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null || !mesh.isReadable)
                throw new InvalidOperationException("Detail physical-UV renderer has no readable mesh: " + HierarchyPath(renderer.transform));
            string assetPath = AssetDatabase.GetAssetPath(mesh);
            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith(QualityBlockDetailPhysicalUvUpgrade.MeshAssetRoot + "/", StringComparison.Ordinal))
                throw new InvalidOperationException("Detail mesh is not a persisted metric physical-UV asset: " + HierarchyPath(renderer.transform));
            if (mesh.uv == null || mesh.uv.Length != mesh.vertexCount || mesh.tangents == null || mesh.tangents.Length != mesh.vertexCount)
                throw new InvalidOperationException("Detail physical-UV mesh lacks complete UV0/tangent data: " + mesh.name);

            Phase expected = ResolvePhase(renderer.transform, contract.uvPolicy.phaseBinsPerAxis);
            string uToken = "_PU" + expected.uBin + "_";
            string vToken = "PV" + expected.vBin + "_";
            int repeatKey = Mathf.RoundToInt(materialEntry.repeatsPerMeter * 100f);
            string rToken = "_R" + repeatKey;
            if (mesh.name.IndexOf(uToken, StringComparison.Ordinal) < 0 ||
                mesh.name.IndexOf(vToken, StringComparison.Ordinal) < 0 ||
                !mesh.name.EndsWith(rToken, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Detail physical-UV mesh phase/material identity mismatch: {HierarchyPath(renderer.transform)} -> {mesh.name}, expected PU{expected.uBin}/PV{expected.vBin}/R{repeatKey}.");
            phasePairs.Add(expected.uBin + ":" + expected.vBin);

            if (deepMetricValidation && mesh.name.IndexOf("ChamferBox", StringComparison.Ordinal) >= 0 && deepValidatedMeshes.Add(mesh))
                ValidateChamferMetricEdges(mesh, contract.qa.boxMetricEdgeErrorFractionMax);
        }

        if (phasePairs.Count < contract.qa.minimumDistinctPhasePairs)
            throw new InvalidOperationException($"Detail physical-UV phase diversity too low: {phasePairs.Count} < {contract.qa.minimumDistinctPhasePairs}.");
        if (materialNames.Count != contract.qa.requiredCanonicalMaterials)
            throw new InvalidOperationException($"Detail physical-UV scene must use all six canonical materials; found {materialNames.Count}.");
    }

    private static void ValidateChamferMetricEdges(Mesh mesh, float maxErrorFraction)
    {
        Vector3[] vertices = mesh.vertices;
        Vector2[] uv = mesh.uv;
        int[] triangles = mesh.triangles;
        int tested = 0;
        for (int t = 0; t + 2 < triangles.Length; t += 3)
        {
            int a = triangles[t];
            int b = triangles[t + 1];
            int c = triangles[t + 2];
            ValidateEdge(mesh.name, vertices[a], vertices[b], uv[a], uv[b], maxErrorFraction, ref tested);
            ValidateEdge(mesh.name, vertices[b], vertices[c], uv[b], uv[c], maxErrorFraction, ref tested);
            ValidateEdge(mesh.name, vertices[c], vertices[a], uv[c], uv[a], maxErrorFraction, ref tested);
            if (tested >= 180) break;
        }
        if (tested < 6)
            throw new InvalidOperationException("Detail physical-UV mesh did not expose enough non-degenerate metric edges: " + mesh.name);
    }

    private static void ValidateEdge(string meshName, Vector3 a, Vector3 b, Vector2 ua, Vector2 ub,
        float maxErrorFraction, ref int tested)
    {
        float geometry = Vector3.Distance(a, b);
        if (geometry < 0.001f)
            return;
        float texture = Vector2.Distance(ua, ub);
        float error = Mathf.Abs(texture - geometry) / geometry;
        tested++;
        if (error > maxErrorFraction)
            throw new InvalidOperationException(
                $"Detail physical-UV metric edge error {error:P1} exceeds {maxErrorFraction:P1} on {meshName}; geometry={geometry:F5}m uv={texture:F5}m.");
    }

    private static void ValidateTextureTransform(Material material, float repeatsPerMeter, string[] properties)
    {
        Vector2 expectedScale = Vector2.one * repeatsPerMeter;
        foreach (string property in properties)
        {
            if (!material.HasProperty(property))
                throw new InvalidOperationException($"{material.name} is missing required texture property {property}.");
            if ((material.GetTextureScale(property) - expectedScale).sqrMagnitude > Epsilon * Epsilon ||
                material.GetTextureOffset(property).sqrMagnitude > Epsilon * Epsilon)
                throw new InvalidOperationException(
                    $"{material.name}/{property} must use {repeatsPerMeter:F2} repeats per metric UV metre with zero offset.");
        }
    }

    private static void OnCameraPreCull(Camera camera)
    {
        if (!IsFormalEvidenceCamera(camera))
            return;
        if (validating)
            throw new InvalidOperationException("Detail physical-UV QA re-entered during formal pre-cull.");
        validating = true;
        try
        {
            ValidateOpenScene(false);
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

    private static MeshRenderer[] SourceRenderers(GameObject root)
    {
        return root.GetComponentsInChildren<MeshRenderer>(true)
            .Where(r => r != null && r.gameObject.activeInHierarchy)
            .Where(r => !IsLodProxy(r.transform))
            .OrderBy(r => HierarchyPath(r.transform), StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsLodProxy(Transform transform)
    {
        for (Transform t = transform; t != null; t = t.parent)
            if (t.name.StartsWith("HD_LOD", StringComparison.Ordinal))
                return true;
        return false;
    }

    private struct Phase
    {
        public int uBin;
        public int vBin;
    }

    private static Phase ResolvePhase(Transform transform, int bins)
    {
        string path = HierarchyPath(transform);
        return new Phase
        {
            uBin = (int)(Fnv1a(path) % (uint)bins),
            vBin = (int)(Fnv1a(path + "|v") % (uint)bins),
        };
    }

    private static uint Fnv1a(string text)
    {
        uint hash = 2166136261u;
        for (int i = 0; i < text.Length; i++)
        {
            hash ^= text[i];
            hash *= 16777619u;
        }
        return hash;
    }

    private static string HierarchyPath(Transform transform)
    {
        var parts = new List<string>();
        for (Transform t = transform; t != null; t = t.parent)
            parts.Add(t.name);
        parts.Reverse();
        return string.Join("/", parts);
    }

    private static bool IsFinitePositive(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }

    private static void RequireExactSet(string[] actual, string[] expected, string label)
    {
        if (actual == null || actual.Any(string.IsNullOrWhiteSpace) ||
            actual.Distinct(StringComparer.Ordinal).Count() != actual.Length ||
            !new HashSet<string>(actual, StringComparer.Ordinal).SetEquals(expected))
            throw new InvalidOperationException(label + " must be exactly [" + string.Join(", ", expected) + "].");
    }

    private static Contract LoadContract()
    {
        string absolute = AbsolutePath(ContractPath);
        if (!File.Exists(absolute))
            throw new FileNotFoundException("Detail physical-UV contract missing: " + ContractPath);
        Contract contract = JsonUtility.FromJson<Contract>(File.ReadAllText(absolute));
        if (contract == null)
            throw new InvalidOperationException("Could not parse detail physical-UV contract.");
        return contract;
    }

    private static void EnsureSceneOpen()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static string AbsolutePath(string assetPath)
    {
        string root = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(root, assetPath));
    }

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x.scene.IsValid() && x.name == name);
    }

    [Serializable]
    private sealed class Contract
    {
        public string schemaVersion;
        public string status;
        public string purpose;
        public string upgrade;
        public string scenePath;
        public string detailRootName;
        public string generatedMeshRoot;
        public UvPolicy uvPolicy;
        public MaterialEntry[] materials;
        public Qa qa;
        public FormalEvidence formalEvidence;
        public string[] criticalDefectRisksReduced;
        public string[] limitations;
        public int implementationReadinessScore;
        public string visualFidelityStatus;
        public bool runtimeRenderVerified;
    }

    [Serializable]
    private sealed class UvPolicy
    {
        public string units;
        public string boxProjection;
        public string cylinderProjection;
        public string phaseSource;
        public float phaseStepMeters;
        public int phaseBinsPerAxis;
        public string phaseReasoning;
        public bool materialPropertyBlocksForbidden;
        public bool normalAndMaskSharePrimaryMetricTransform;
    }

    [Serializable]
    private sealed class MaterialEntry
    {
        public string materialName;
        public string materialId;
        public float repeatsPerMeter;
        public float tileMeters;
        public string physicalInterpretation;
    }

    [Serializable]
    private sealed class Qa
    {
        public int minimumSourceRenderers;
        public int minimumDistinctPhasePairs;
        public int requiredCanonicalMaterials;
        public float boxMetricEdgeErrorFractionMax;
        public string[] requiredTextureProperties;
        public float[] requiredTextureOffset;
        public int automaticVisualPoints;
        public bool renderVerificationPending;
    }

    [Serializable]
    private sealed class FormalEvidence
    {
        public string[] required100PercentCrops;
        public string[] reviewFor;
        public bool actualRenderRequiredForVisualPoints;
    }
}
