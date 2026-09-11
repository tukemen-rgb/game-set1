using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Binds the current generated-foliage morphology, lamina geometry, analytic silhouette and dry-leaf
/// optical state into the formal reflection-lighting lifecycle. ReflectionProbe rendering does not rely
/// on MainCamera pre-cull, so the newer foliage guards must be evaluated explicitly before RenderProbe,
/// through every observed poll, at completion and immediately before still capture.
///
/// This validator is intentionally read-only once reflection evidence begins. It does not repair or
/// rebuild foliage state in-flight, awards zero Visual Fidelity points, and cannot clear repetition,
/// shimmer, LOD-pop or material defects without actual native-4K/temporal pixels.
/// </summary>
public static class QualityBlockFoliageFormalEvidenceBindingQA
{
    private const string ContractPath = "Assets/QA/foliage_formal_evidence_binding_contract.json";
    private const string GatePath = "Assets/QA/visual_fidelity_gate.json";
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ReflectionBindingSource = "Assets/Editor/QualityBlockReflectionMaterialPhysicalityBindingQA.cs";

    private static readonly string[] UpstreamContracts =
    {
        "Assets/QA/foliage_morphology_diversity_contract.json",
        "Assets/QA/foliage_lamina_geometry_contract.json",
        "Assets/QA/foliage_silhouette_optics_contract.json",
        "Assets/QA/foliage_optical_physicality_contract.json",
    };

    private static readonly string[] CanonicalCriticalRisks =
    {
        "obvious_repetition",
        "impossible_material_physics",
        "severe_aliasing_or_shimmer",
        "visible_lod_pop",
    };

    private static readonly string[] RequiredViews = { "hero", "oblique", "grazing" };
    private static readonly string[] RequiredCrops =
    {
        "oblique/vegetation_grounding",
        "grazing/tree_shadow_contact",
    };

    [MenuItem("NewTown/QA/Validate Foliage Formal Evidence Binding Contract")]
    public static void ValidateContractConfigOnly()
    {
        BindingContract contract = LoadJson<BindingContract>(ContractPath);
        if (contract == null || !string.Equals(contract.schemaVersion, "1.0", StringComparison.Ordinal))
            throw new InvalidOperationException("Foliage formal evidence binding contract is null/unparseable or not schema 1.0.");
        if (!string.Equals(contract.status, "IMPLEMENTATION_READY_RENDER_PENDING", StringComparison.Ordinal) ||
            !string.Equals(contract.scenePath, ScenePath, StringComparison.Ordinal) ||
            !string.Equals(contract.reflectionBindingSource, ReflectionBindingSource, StringComparison.Ordinal))
            throw new InvalidOperationException("Foliage formal evidence binding identity/status drifted.");

        RequireExactSet(contract.upstreamContracts, UpstreamContracts, "upstreamContracts");

        Requirements r = contract.requirements;
        if (r == null ||
            !r.validateBeforeEveryReflectionLightingFingerprint ||
            !r.reflectionFingerprintUsedBeforeRenderProbeRequest ||
            !r.reflectionFingerprintRevalidatedOnEveryEditorPoll ||
            !r.reflectionFingerprintRevalidatedAtCompletion ||
            !r.reflectionFingerprintRevalidatedImmediatelyBeforeStillCapture ||
            !r.validationMustBeReportFreeAndMutationFree ||
            !r.sourceStateMayNotAutoRepairInFlight ||
            !r.requireMorphologyAssignmentAndLodIdentity ||
            !r.requireFoldedBaseAndDerivedLamina ||
            !r.requireVisibleShadowSilhouetteCoherence ||
            !r.requireDryDielectricLeafPhysicality ||
            !r.requireMidsummerSolarDirectionAndSoftShadowCoherence ||
            !r.actualRenderRequiredForVisualPoints)
            throw new InvalidOperationException("Foliage formal evidence binding requirements were weakened or are incomplete.");

        RequireExactSet(contract.criticalDefectRisksReduced, CanonicalCriticalRisks, "criticalDefectRisksReduced");
        ValidateCriticalIdsExistInGate(CanonicalCriticalRisks);

        FormalEvidence evidence = contract.formalEvidenceRequirements;
        if (evidence == null || evidence.nativeResolution == null || evidence.nativeResolution.Length != 2 ||
            evidence.nativeResolution[0] != 3840 || evidence.nativeResolution[1] != 2160 ||
            !evidence.humanReviewRequired || !evidence.actualRenderRequiredForVisualPoints ||
            !string.Equals(evidence.requiredTemporalTarget, "temporal/subpixel_grazing", StringComparison.Ordinal))
            throw new InvalidOperationException("Foliage formal evidence requirements were weakened.");
        RequireExactSet(evidence.requiredViews, RequiredViews, "requiredViews");
        RequireExactSet(evidence.required100PercentCrops, RequiredCrops, "required100PercentCrops");
        if (evidence.reviewFor == null || evidence.reviewFor.Length < 8 || evidence.reviewFor.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Foliage formal evidence rendered-review checklist is incomplete.");

        if (contract.scoreTargets == null || contract.scoreTargets.Length != 3 ||
            contract.scoreTargets.Any(x => x == null || x.autoPoints != 0 || x.weight <= 0 || string.IsNullOrWhiteSpace(x.categoryId)))
            throw new InvalidOperationException("Foliage formal evidence score targets must remain three zero-auto-point implementation targets.");
        var expectedWeights = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "vegetation_natural_complexity", 8 },
            { "lighting_shadows_reflections", 15 },
            { "temporal_lod_aliasing", 5 },
        };
        if (!new HashSet<string>(contract.scoreTargets.Select(x => x.categoryId), StringComparer.Ordinal).SetEquals(expectedWeights.Keys))
            throw new InvalidOperationException("Foliage formal evidence score-target categories drifted.");
        foreach (ScoreTarget target in contract.scoreTargets)
        {
            if (!expectedWeights.TryGetValue(target.categoryId, out int expectedWeight) || target.weight != expectedWeight ||
                string.IsNullOrWhiteSpace(target.expectedImpactOnly))
                throw new InvalidOperationException("Foliage formal evidence score-target weight/impact metadata drifted.");
        }

        if (contract.implementationReadinessScore != 93 ||
            !string.Equals(contract.visualFidelityStatus, "UNSCORED_UNTIL_REAL_4K_RENDER", StringComparison.Ordinal) ||
            contract.runtimeRenderVerified || contract.visualFidelityPointsAwarded != 0 ||
            contract.limitations == null || contract.limitations.Length < 4 || contract.limitations.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Foliage formal evidence binding may not inflate readiness or claim rendered completion/Visual Fidelity points.");

        // Keep every upstream contract fail-closed at the same core gate-integrity entrypoint.
        QualityBlockFoliageMorphologyDiversityQA.ValidateContractConfigOnly();
        QualityBlockFoliageLaminaGeometryQA.ValidateContractConfigOnly();
        QualityBlockFoliageSilhouetteOpticsQA.ValidateContractConfigOnly();
        QualityBlockFoliagePhysicalityQA.ValidateContractConfigOnly();

        // The contract is only meaningful if the central reflection fingerprint actually invokes it.
        string reflectionSource = File.ReadAllText(AbsolutePath(ReflectionBindingSource));
        RequireSourceToken(reflectionSource, "QualityBlockFoliageFormalEvidenceBindingQA.ValidateContractConfigOnly();");
        RequireSourceToken(reflectionSource, "QualityBlockFoliageFormalEvidenceBindingQA.ValidateOpenScene();");
    }

    [MenuItem("NewTown/QA/Validate Foliage Formal Evidence State")]
    public static void ValidateOpenScene()
    {
        ValidateContractConfigOnly();

        if (!EditorSceneManager.GetActiveScene().IsValid() ||
            !string.Equals(EditorSceneManager.GetActiveScene().path, ScenePath, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Foliage formal evidence validation is read-only and requires the persisted benchmark scene to already be open: " + ScenePath);

        // All delegated methods are validation-only under the precondition above. In particular, do not
        // call any Apply/Build/scene-save path here: reflection polling must never repair evidence state.
        QualityBlockFoliageMorphologyVariationUpgrade.ValidateCurrentScene(false);
        QualityBlockFoliageLaminaGeometryUpgrade.ValidateGeneratedMeshLibrary(true);
        QualityBlockFoliageSilhouetteOpticsQA.ValidateSceneState();
        QualityBlockFoliageOpticsUpgrade.ValidateOpenScene();
        QualityBlockFoliagePhysicalityQA.ValidateOpenScene();
    }

    private static void ValidateCriticalIdsExistInGate(IEnumerable<string> ids)
    {
        VisualGate gate = LoadJson<VisualGate>(GatePath);
        if (gate == null || gate.criticalDefects == null)
            throw new InvalidOperationException("Visual Fidelity gate critical-defect registry is unavailable.");
        var gateIds = new HashSet<string>(gate.criticalDefects.Where(x => x != null).Select(x => x.id), StringComparer.Ordinal);
        foreach (string id in ids)
            if (!gateIds.Contains(id))
                throw new InvalidOperationException("Foliage formal evidence contract references non-canonical critical defect id: " + id);
    }

    private static void RequireSourceToken(string source, string token)
    {
        if (string.IsNullOrEmpty(source) || source.IndexOf(token, StringComparison.Ordinal) < 0)
            throw new InvalidOperationException("Central reflection foliage binding token is missing: " + token);
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
        public string reflectionBindingSource;
        public string[] upstreamContracts;
        public Requirements requirements;
        public string[] criticalDefectRisksReduced;
        public FormalEvidence formalEvidenceRequirements;
        public ScoreTarget[] scoreTargets;
        public int implementationReadinessScore;
        public string visualFidelityStatus;
        public bool runtimeRenderVerified;
        public int visualFidelityPointsAwarded;
        public string[] limitations;
    }

    [Serializable]
    private sealed class Requirements
    {
        public bool validateBeforeEveryReflectionLightingFingerprint;
        public bool reflectionFingerprintUsedBeforeRenderProbeRequest;
        public bool reflectionFingerprintRevalidatedOnEveryEditorPoll;
        public bool reflectionFingerprintRevalidatedAtCompletion;
        public bool reflectionFingerprintRevalidatedImmediatelyBeforeStillCapture;
        public bool validationMustBeReportFreeAndMutationFree;
        public bool sourceStateMayNotAutoRepairInFlight;
        public bool requireMorphologyAssignmentAndLodIdentity;
        public bool requireFoldedBaseAndDerivedLamina;
        public bool requireVisibleShadowSilhouetteCoherence;
        public bool requireDryDielectricLeafPhysicality;
        public bool requireMidsummerSolarDirectionAndSoftShadowCoherence;
        public bool actualRenderRequiredForVisualPoints;
    }

    [Serializable]
    private sealed class FormalEvidence
    {
        public int[] nativeResolution;
        public string[] requiredViews;
        public string[] required100PercentCrops;
        public string requiredTemporalTarget;
        public string[] reviewFor;
        public bool humanReviewRequired;
        public bool actualRenderRequiredForVisualPoints;
    }

    [Serializable]
    private sealed class ScoreTarget
    {
        public string categoryId;
        public int weight;
        public int autoPoints;
        public string expectedImpactOnly;
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
