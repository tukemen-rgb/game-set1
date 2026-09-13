using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Binds generated foliage morphology/lamina/optics plus the cause-based ecology and dimensioned
/// tree-to-ground root-zone construction into the same formal reflection/still/temporal evidence state.
/// ReflectionProbe rendering does not rely on MainCamera pre-cull, so these validators are invoked
/// explicitly before the reflection-lighting fingerprint and throughout its evidence lifecycle.
///
/// Once formal evidence begins this path is validation-only: it never builds, repairs or saves scene
/// state, awards zero Visual Fidelity points, and cannot clear intersection/repetition/shimmer/LOD or
/// material defects without actual native-4K still and temporal pixels.
/// </summary>
public static class QualityBlockFoliageFormalEvidenceBindingQA
{
    private const string ContractPath = "Assets/QA/foliage_formal_evidence_binding_contract.json";
    private const string GatePath = "Assets/QA/visual_fidelity_gate.json";
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ReflectionBindingSource = "Assets/Editor/QualityBlockReflectionMaterialPhysicalityBindingQA.cs";
    private const string FormalBuildSource = "Assets/Editor/QualityBlockFoliageOpticsUpgrade.cs";
    private const string FormalBuildIntegrationPath = "Assets/QA/vegetation_formal_build_integration_contract.json";

    private static readonly string[] UpstreamContracts =
    {
        "Assets/QA/foliage_morphology_diversity_contract.json",
        "Assets/QA/foliage_lamina_geometry_contract.json",
        "Assets/QA/foliage_silhouette_optics_contract.json",
        "Assets/QA/foliage_optical_physicality_contract.json",
        "Assets/QA/vegetation_ecology_contract.json",
        "Assets/QA/vegetation_root_zone_interface_contract.json",
        FormalBuildIntegrationPath,
    };

    private static readonly string[] CanonicalCriticalRisks =
    {
        "obvious_repetition",
        "impossible_material_physics",
        "hero_geometry_intersection",
        "severe_aliasing_or_shimmer",
        "visible_lod_pop",
        "missing_construction_material_metadata",
    };

    private static readonly string[] RequiredViews = { "hero", "oblique", "grazing" };
    private static readonly string[] RequiredCrops =
    {
        "oblique/vegetation_grounding",
        "grazing/tree_shadow_contact",
    };

    private static readonly string[] RequiredFormalBuildOrder =
    {
        "QualityBlockTreeDetailUpgrade.BuildDetailedTrees",
        "QualityBlockTreeWoodyContinuityQA.ApplyToOpenScene",
        "QualityBlockFoliageMorphologyVariationUpgrade.ApplyToOpenScene",
        "QualityBlockFoliageOpticsUpgrade.EnsureMaterials",
        "QualityBlockFoliageOpticsUpgrade.ApplyToOpenScene",
        "QualityBlockVegetationEcologyContractQA.Validate",
        "QualityBlockVegetationEcologyUpgrade.ApplyToOpenScene",
        "QualityBlockVegetationRootZoneInterfaceQA.ApplyToOpenScene",
        "QualityBlockFoliageOpticsUpgrade.ValidateOpenScene",
    };

    [MenuItem("NewTown/QA/Validate Foliage Formal Evidence Binding Contract")]
    public static void ValidateContractConfigOnly()
    {
        BindingContract contract = LoadJson<BindingContract>(ContractPath);
        if (contract == null || !string.Equals(contract.schemaVersion, "1.1", StringComparison.Ordinal))
            throw new InvalidOperationException("Foliage formal evidence binding contract is null/unparseable or not schema 1.1.");
        if (!string.Equals(contract.status, "IMPLEMENTATION_READY_RENDER_PENDING", StringComparison.Ordinal) ||
            !string.Equals(contract.scenePath, ScenePath, StringComparison.Ordinal) ||
            !string.Equals(contract.reflectionBindingSource, ReflectionBindingSource, StringComparison.Ordinal) ||
            !string.Equals(contract.formalBuildSource, FormalBuildSource, StringComparison.Ordinal))
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
            !r.requireVegetationEcologyContractAndScene ||
            !r.requireRootZoneConstructionAndGrounding ||
            !r.requireFormalBuildIntegrationContract ||
            !r.requireCauseBasedVegetationGroundContact ||
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
        if (evidence.reviewFor == null || evidence.reviewFor.Length < 12 || evidence.reviewFor.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Foliage formal evidence rendered-review checklist is incomplete.");

        var expectedWeights = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "geometry_construction", 20 },
            { "weathering_causality", 10 },
            { "vegetation_natural_complexity", 8 },
            { "lighting_shadows_reflections", 15 },
            { "temporal_lod_aliasing", 5 },
        };
        if (contract.scoreTargets == null || contract.scoreTargets.Length != expectedWeights.Count ||
            contract.scoreTargets.Any(x => x == null || x.autoPoints != 0 || x.weight <= 0 || string.IsNullOrWhiteSpace(x.categoryId)) ||
            !new HashSet<string>(contract.scoreTargets.Select(x => x.categoryId), StringComparer.Ordinal).SetEquals(expectedWeights.Keys))
            throw new InvalidOperationException("Foliage formal evidence score targets must remain the five zero-auto-point implementation targets.");
        foreach (ScoreTarget target in contract.scoreTargets)
        {
            if (!expectedWeights.TryGetValue(target.categoryId, out int expectedWeight) || target.weight != expectedWeight ||
                string.IsNullOrWhiteSpace(target.expectedImpactOnly))
                throw new InvalidOperationException("Foliage formal evidence score-target weight/impact metadata drifted.");
        }

        if (contract.implementationReadinessScore != 93 ||
            !string.Equals(contract.visualFidelityStatus, "UNSCORED_UNTIL_REAL_4K_RENDER", StringComparison.Ordinal) ||
            contract.runtimeRenderVerified || contract.visualFidelityPointsAwarded != 0 ||
            contract.limitations == null || contract.limitations.Length < 5 || contract.limitations.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Foliage formal evidence binding may not inflate readiness or claim rendered completion/Visual Fidelity points.");

        // Keep every upstream contract fail-closed at the same entrypoint used by the reflection binding.
        QualityBlockFoliageMorphologyDiversityQA.ValidateContractConfigOnly();
        QualityBlockFoliageLaminaGeometryQA.ValidateContractConfigOnly();
        QualityBlockFoliageSilhouetteOpticsQA.ValidateContractConfigOnly();
        QualityBlockFoliagePhysicalityQA.ValidateContractConfigOnly();
        QualityBlockVegetationEcologyContractQA.Validate();
        QualityBlockVegetationRootZoneInterfaceQA.ValidateContractConfigOnly();
        ValidateFormalBuildIntegrationContract();

        // Prove the formal build still constructs and validates ecology/root-zone directly. This makes the
        // evidence binding resilient to a later refactor that accidentally drops transitive validation.
        string formalBuildSource = File.ReadAllText(AbsolutePath(FormalBuildSource));
        RequireSourceToken(formalBuildSource, "QualityBlockVegetationEcologyContractQA.Validate();");
        RequireSourceToken(formalBuildSource, "QualityBlockVegetationEcologyUpgrade.ApplyToOpenScene();");
        RequireSourceToken(formalBuildSource, "QualityBlockVegetationRootZoneInterfaceQA.ApplyToOpenScene();");
        RequireSourceToken(formalBuildSource, "QualityBlockVegetationEcologyUpgrade.ValidateOpenScene();");
        RequireSourceToken(formalBuildSource, "QualityBlockVegetationRootZoneInterfaceQA.ValidateOpenScene();");

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

        // All delegated methods are validation-only under the precondition above. Never call Apply/Build/save
        // here: reflection polling and pre-still validation must not repair evidence state in flight.
        QualityBlockFoliageMorphologyVariationUpgrade.ValidateCurrentScene(false);
        QualityBlockFoliageLaminaGeometryUpgrade.ValidateGeneratedMeshLibrary(true);
        QualityBlockFoliageSilhouetteOpticsQA.ValidateSceneState();
        QualityBlockFoliageOpticsUpgrade.ValidateOpenScene();
        QualityBlockFoliagePhysicalityQA.ValidateOpenScene();

        // Direct redundant grounding checks are deliberate. Formal evidence must not rely solely on
        // QualityBlockFoliageOpticsUpgrade retaining these calls after a future refactor.
        QualityBlockVegetationEcologyUpgrade.ValidateOpenScene();
        QualityBlockVegetationRootZoneInterfaceQA.ValidateOpenScene();
    }

    private static void ValidateFormalBuildIntegrationContract()
    {
        FormalBuildIntegrationContract integration = LoadJson<FormalBuildIntegrationContract>(FormalBuildIntegrationPath);
        if (integration == null || !string.Equals(integration.schema_version, "1.1", StringComparison.Ordinal) ||
            !string.Equals(integration.id, "vegetation_formal_build_integration", StringComparison.Ordinal) ||
            !string.Equals(integration.status, "RENDER_VERIFICATION_PENDING", StringComparison.Ordinal))
            throw new InvalidOperationException("Vegetation formal-build integration contract identity/schema drifted.");

        if (integration.scope == null ||
            !string.Equals(integration.scope.benchmark_scene, ScenePath, StringComparison.Ordinal) ||
            !string.Equals(integration.scope.required_validator, "QualityBlockFoliageOpticsUpgrade.ValidateOpenScene", StringComparison.Ordinal) ||
            !string.Equals(integration.scope.authoritative_ecology_contract, "Assets/QA/vegetation_ecology_contract.json", StringComparison.Ordinal) ||
            !string.Equals(integration.scope.authoritative_root_zone_contract, "Assets/QA/vegetation_root_zone_interface_contract.json", StringComparison.Ordinal))
            throw new InvalidOperationException("Vegetation formal-build integration scope drifted.");

        if (integration.required_execution_order == null ||
            !integration.required_execution_order.SequenceEqual(RequiredFormalBuildOrder, StringComparer.Ordinal))
            throw new InvalidOperationException("Vegetation formal-build execution order drifted.");

        ConstructionReasoning construction = integration.construction_reasoning;
        TreePitInterface pit = construction != null ? construction.tree_pit_interface : null;
        if (construction == null || construction.base_tree_count != 6 ||
            !string.Equals(construction.ecology_root, "VegetationEcologyDetail", StringComparison.Ordinal) ||
            pit == null || pit.required_reconstructed_count != 6 || pit.maintained_paved_pit_count != 4 ||
            pit.grass_ground_tree_count != 2 || pit.interior_paved_ring_count != 1 || pit.paving_boundary_ring_count != 3 ||
            pit.curb_modules_per_pit != 12 || pit.curb_modules_total != 48 ||
            pit.generated_root_flares_per_tree != 7 || pit.generated_root_flares_total != 42 ||
            !Near(pit.opening_radius_m, 1.16f, 0.0001f) || !Near(pit.opening_diameter_m, 2.32f, 0.0001f) ||
            !Near(pit.conservative_root_outer_radius_m, 1.0648f, 0.0002f) ||
            pit.opening_radius_m - pit.conservative_root_outer_radius_m < 0.09f ||
            !Near(pit.minimum_visible_soil_clearance_to_edging_m, 0.09f, 0.0001f))
            throw new InvalidOperationException("Vegetation formal-build construction counts/dimensions drifted from the authoritative ecology/root-zone contracts.");

        VisualGateMapping mapping = integration.visual_fidelity_gate_mapping;
        if (mapping == null || mapping.visual_fidelity_points_awarded != 0 ||
            !string.Equals(mapping.visual_fidelity_status, "UNSCORED_UNTIL_REAL_4K_RENDER", StringComparison.Ordinal) ||
            mapping.pass_claim_allowed_without_actual_render)
            throw new InvalidOperationException("Vegetation formal-build integration may not award Visual Fidelity points or claim PASS without render evidence.");

        RenderVerification render = integration.render_verification;
        if (render == null || !string.Equals(render.required_unity_version, "6000.3.0f1", StringComparison.Ordinal) ||
            !string.Equals(render.status, "PENDING_RUNTIME", StringComparison.Ordinal) ||
            render.required_stills == null || render.required_stills.Length != 3 ||
            render.required_crops == null || render.required_crops.Length < 2 || string.IsNullOrWhiteSpace(render.required_temporal))
            throw new InvalidOperationException("Vegetation formal-build render-verification requirements were weakened.");

        if (integration.implementation_readiness == null || integration.implementation_readiness.score_100 != 93 ||
            string.IsNullOrWhiteSpace(integration.implementation_readiness.reason_not_higher) ||
            integration.fail_closed_rules == null || integration.fail_closed_rules.Length < 8 ||
            integration.fail_closed_rules.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Vegetation formal-build readiness/fail-closed metadata is incomplete.");
    }

    private static bool Near(float a, float b, float epsilon)
    {
        return Mathf.Abs(a - b) <= epsilon;
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
            throw new InvalidOperationException("Required formal vegetation integration token is missing: " + token);
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
        public string formalBuildSource;
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
        public bool requireVegetationEcologyContractAndScene;
        public bool requireRootZoneConstructionAndGrounding;
        public bool requireFormalBuildIntegrationContract;
        public bool requireCauseBasedVegetationGroundContact;
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

    [Serializable]
    private sealed class FormalBuildIntegrationContract
    {
        public string schema_version;
        public string id;
        public string status;
        public IntegrationScope scope;
        public string[] required_execution_order;
        public ConstructionReasoning construction_reasoning;
        public string[] fail_closed_rules;
        public VisualGateMapping visual_fidelity_gate_mapping;
        public RenderVerification render_verification;
        public ImplementationReadiness implementation_readiness;
    }

    [Serializable]
    private sealed class IntegrationScope
    {
        public string benchmark_scene;
        public string required_validator;
        public string authoritative_ecology_contract;
        public string authoritative_root_zone_contract;
    }

    [Serializable]
    private sealed class ConstructionReasoning
    {
        public int base_tree_count;
        public string ecology_root;
        public TreePitInterface tree_pit_interface;
    }

    [Serializable]
    private sealed class TreePitInterface
    {
        public int required_reconstructed_count;
        public int maintained_paved_pit_count;
        public int grass_ground_tree_count;
        public int interior_paved_ring_count;
        public int paving_boundary_ring_count;
        public float opening_radius_m;
        public float opening_diameter_m;
        public int curb_modules_per_pit;
        public int curb_modules_total;
        public int generated_root_flares_per_tree;
        public int generated_root_flares_total;
        public float conservative_root_outer_radius_m;
        public float minimum_visible_soil_clearance_to_edging_m;
    }

    [Serializable]
    private sealed class VisualGateMapping
    {
        public int visual_fidelity_points_awarded;
        public string visual_fidelity_status;
        public bool pass_claim_allowed_without_actual_render;
    }

    [Serializable]
    private sealed class RenderVerification
    {
        public string required_unity_version;
        public string[] required_stills;
        public string[] required_crops;
        public string required_temporal;
        public string status;
    }

    [Serializable]
    private sealed class ImplementationReadiness
    {
        public int score_100;
        public string reason_not_higher;
    }
}
