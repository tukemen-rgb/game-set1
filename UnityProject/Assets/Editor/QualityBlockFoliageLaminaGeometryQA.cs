using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Fail-closed source/evidence binding for folded generated leaf laminae. This validator proves only
/// that the required non-planar geometry is installed; it never converts source geometry into Visual
/// Fidelity points. Native 4K still and temporal pixels remain mandatory.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockFoliageLaminaGeometryQA
{
    private const string GatePath = "Assets/QA/visual_fidelity_gate.json";
    private const int Width = 3840;
    private const int Height = 2160;

    private static readonly string[] FormalTargetPrefixes = { "QA4K_", "QAPreparedTemporal_" };
    private static readonly string[] CriticalRisks =
    {
        "visible_primitive_placeholder",
        "obvious_repetition",
        "severe_aliasing_or_shimmer",
        "visible_lod_pop",
    };
    private static readonly string[] RequiredViews = { "hero", "oblique", "grazing" };
    private static readonly string[] RequiredCrops =
    {
        "oblique/vegetation_grounding",
        "grazing/tree_shadow_contact",
    };

    private static bool validating;

    static QualityBlockFoliageLaminaGeometryQA()
    {
        Camera.onPreCull -= OnCameraPreCull;
        Camera.onPreCull += OnCameraPreCull;
    }

    [MenuItem("NewTown/QA/Validate Foliage Lamina Geometry Contract")]
    public static void ValidateContractConfigOnly()
    {
        Contract contract = LoadJson<Contract>(QualityBlockFoliageLaminaGeometryUpgrade.ContractPath);
        if (contract == null || !string.Equals(contract.schemaVersion, "1.0", StringComparison.Ordinal))
            throw new InvalidOperationException("Foliage lamina contract is null/unparseable or not schema 1.0.");
        if (!string.Equals(contract.status, "IMPLEMENTATION_READY_RENDER_PENDING", StringComparison.Ordinal) ||
            !string.Equals(contract.scenePath, QualityBlockFoliageLaminaGeometryUpgrade.ScenePath, StringComparison.Ordinal) ||
            !string.Equals(contract.generator, "Assets/Editor/QualityBlockFoliageLaminaGeometryUpgrade.cs", StringComparison.Ordinal))
            throw new InvalidOperationException("Foliage lamina contract identity/status drifted.");

        MeshPolicy mesh = contract.meshPolicy;
        if (mesh == null || mesh.baseMeshes == null || mesh.baseMeshes.Length != 3 ||
            !new HashSet<string>(mesh.baseMeshes, StringComparer.Ordinal).SetEquals(new[] { "GM_LeafSpray_A", "GM_LeafSpray_B", "GM_LeafSpray_C" }) ||
            mesh.derivedMorphologyCount != QualityBlockFoliageMorphologyVariationUpgrade.VariantCount ||
            mesh.verticesPerLeaf != QualityBlockFoliageLaminaGeometryUpgrade.VerticesPerLeaf ||
            mesh.triangleIndicesPerLeaf != QualityBlockFoliageLaminaGeometryUpgrade.TriangleIndicesPerLeaf ||
            !mesh.frontBackCoincidenceRequired || !mesh.deterministicOnly || !mesh.sceneSaveConvergenceBinding ||
            Mathf.Abs(mesh.minimumFoldDegrees - QualityBlockFoliageLaminaGeometryUpgrade.MinimumFoldDegrees) > 0.001f ||
            Mathf.Abs(mesh.maximumFoldDegrees - QualityBlockFoliageLaminaGeometryUpgrade.MaximumFoldDegrees) > 0.001f)
            throw new InvalidOperationException("Foliage lamina mesh policy was weakened or drifted.");

        InstallationPolicy installation = contract.installationPolicy;
        if (installation == null || !installation.treePlacementMutationForbidden ||
            !installation.woodyConstructionMutationForbidden || !installation.clusterTransformMutationForbidden ||
            !installation.colliderMutationForbidden || !installation.materialMutationForbidden ||
            !installation.authoredArtSupersedesGeneratedFallback)
            throw new InvalidOperationException("Foliage lamina installation boundaries were weakened.");

        LodPolicy lod = contract.lodPolicy;
        if (lod == null || lod.levels != 4 || !lod.retainedMorphologyIdentityRequired ||
            !lod.foldMustBePresentInBaseAndDerivedMeshes || !lod.temporalReviewRequired)
            throw new InvalidOperationException("Foliage lamina LOD policy is incomplete.");

        RequireExactSet(contract.criticalDefectRisksReduced, CriticalRisks, "foliage lamina critical risks");
        ValidateCriticalIdsExistInGate(CriticalRisks);

        FormalEvidence formal = contract.formalEvidenceRequirements;
        if (formal == null || formal.nativeResolution == null || formal.nativeResolution.Length != 2 ||
            formal.nativeResolution[0] != Width || formal.nativeResolution[1] != Height ||
            !formal.humanReviewRequired || !formal.actualRenderRequiredForVisualPoints ||
            !string.Equals(formal.requiredTemporalTarget, "temporal/subpixel_grazing", StringComparison.Ordinal))
            throw new InvalidOperationException("Foliage lamina formal evidence requirements were weakened.");
        RequireExactSet(formal.requiredViews, RequiredViews, "foliage lamina views");
        RequireExactSet(formal.required100PercentCrops, RequiredCrops, "foliage lamina crops");
        if (formal.reviewFor == null || formal.reviewFor.Length < 8 || formal.reviewFor.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Foliage lamina rendered-review checklist is incomplete.");

        if (contract.hardFailRules == null || contract.hardFailRules.Length < 11 || contract.hardFailRules.Any(string.IsNullOrWhiteSpace) ||
            contract.limitations == null || contract.limitations.Length < 4 || contract.limitations.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Foliage lamina hard-fail/limitation metadata is incomplete.");
        if (contract.implementationReadinessScore != 93 ||
            !string.Equals(contract.visualFidelityStatus, "UNSCORED_UNTIL_REAL_4K_RENDER", StringComparison.Ordinal) ||
            contract.runtimeRenderVerified || contract.autoVisualPoints != 0)
            throw new InvalidOperationException("Foliage lamina contract may not inflate readiness or claim rendered quality.");
    }

    [MenuItem("NewTown/QA/Validate Foliage Lamina Formal State")]
    public static void ValidateFormalState()
    {
        ValidateContractConfigOnly();
        QualityBlockFoliageLaminaGeometryUpgrade.ValidateGeneratedMeshLibrary(true);
        Debug.Log("Foliage lamina formal source state valid. Native-4K still/temporal review remains required before any visual points.");
    }

    private static void OnCameraPreCull(Camera camera)
    {
        if (!IsFormalEvidenceCamera(camera))
            return;
        if (validating)
            throw new InvalidOperationException("Foliage lamina QA re-entered during formal pre-cull.");

        validating = true;
        try
        {
            ValidateContractConfigOnly();
            QualityBlockFoliageLaminaGeometryUpgrade.ValidateGeneratedMeshLibrary(true);
        }
        finally
        {
            validating = false;
        }
    }

    private static bool IsFormalEvidenceCamera(Camera camera)
    {
        if (camera == null || camera.targetTexture == null ||
            camera.targetTexture.width != Width || camera.targetTexture.height != Height)
            return false;
        string name = camera.targetTexture.name ?? string.Empty;
        return FormalTargetPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static void ValidateCriticalIdsExistInGate(IEnumerable<string> ids)
    {
        VisualGate gate = LoadJson<VisualGate>(GatePath);
        if (gate == null || gate.criticalDefects == null)
            throw new InvalidOperationException("Visual Fidelity gate critical-defect registry is unavailable.");
        var gateIds = new HashSet<string>(gate.criticalDefects.Where(x => x != null).Select(x => x.id), StringComparer.Ordinal);
        foreach (string id in ids)
            if (!gateIds.Contains(id))
                throw new InvalidOperationException("Foliage lamina contract references non-canonical critical defect id: " + id);
    }

    private static void RequireExactSet(string[] actual, string[] expected, string label)
    {
        if (actual == null || actual.Any(string.IsNullOrWhiteSpace) ||
            actual.Distinct(StringComparer.Ordinal).Count() != actual.Length ||
            !new HashSet<string>(actual, StringComparer.Ordinal).SetEquals(expected ?? Array.Empty<string>()))
            throw new InvalidOperationException(label + " must be exactly [" + string.Join(", ", expected ?? Array.Empty<string>()) + "].");
    }

    private static T LoadJson<T>(string assetPath)
    {
        string absolute = Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath));
        if (!File.Exists(absolute))
            throw new FileNotFoundException("Required QA file not found: " + assetPath);
        T value = JsonUtility.FromJson<T>(File.ReadAllText(absolute));
        if (value == null)
            throw new InvalidOperationException("Could not parse QA JSON: " + assetPath);
        return value;
    }

    [Serializable]
    private sealed class Contract
    {
        public string schemaVersion;
        public string status;
        public string scenePath;
        public string generator;
        public MeshPolicy meshPolicy;
        public InstallationPolicy installationPolicy;
        public LodPolicy lodPolicy;
        public string[] criticalDefectRisksReduced;
        public string[] hardFailRules;
        public FormalEvidence formalEvidenceRequirements;
        public int implementationReadinessScore;
        public string visualFidelityStatus;
        public bool runtimeRenderVerified;
        public int autoVisualPoints;
        public string[] limitations;
    }

    [Serializable]
    private sealed class MeshPolicy
    {
        public string[] baseMeshes;
        public int derivedMorphologyCount;
        public int verticesPerLeaf;
        public int triangleIndicesPerLeaf;
        public bool frontBackCoincidenceRequired;
        public float minimumFoldDegrees;
        public float maximumFoldDegrees;
        public bool deterministicOnly;
        public bool sceneSaveConvergenceBinding;
    }

    [Serializable]
    private sealed class InstallationPolicy
    {
        public bool treePlacementMutationForbidden;
        public bool woodyConstructionMutationForbidden;
        public bool clusterTransformMutationForbidden;
        public bool colliderMutationForbidden;
        public bool materialMutationForbidden;
        public bool authoredArtSupersedesGeneratedFallback;
    }

    [Serializable]
    private sealed class LodPolicy
    {
        public int levels;
        public bool retainedMorphologyIdentityRequired;
        public bool foldMustBePresentInBaseAndDerivedMeshes;
        public bool temporalReviewRequired;
    }

    [Serializable]
    private sealed class FormalEvidence
    {
        public int[] nativeResolution;
        public string[] requiredViews;
        public string[] required100PercentCrops;
        public string requiredTemporalTarget;
        public bool humanReviewRequired;
        public bool actualRenderRequiredForVisualPoints;
        public string[] reviewFor;
    }

    [Serializable]
    private sealed class VisualGate
    {
        public CriticalDefect[] criticalDefects;
    }

    [Serializable]
    private sealed class CriticalDefect
    {
        public string id;
    }
}
