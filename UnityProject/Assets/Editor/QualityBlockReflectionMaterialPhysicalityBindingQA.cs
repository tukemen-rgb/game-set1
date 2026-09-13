using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Binds registered-material physicality and high-value construction state into the formal
/// reflection-lighting fingerprint path. QualityBlockReflectionProbeAwaiter evaluates that fingerprint
/// before RenderProbe(), on every observed Editor poll, at completion and immediately before still capture;
/// therefore source/material/construction drift cannot enter or survive the formal cubemap baseline merely
/// because MainCamera pre-cull has not run yet.
///
/// The entrypoint chains manufacture-scale detail UVs, facade formal-build state, generated foliage,
/// balcony drainage/floor fall, dry waterproof microstructure, glazing-gasket construction, facade sill
/// drainage construction, the smooth LOD0 crescent-lever asset set, the curve-preserving LOD1 proxy set,
/// the curve-preserving LOD2/LOD3 far proxy set, and the period-plausible metal crescent-latch/receiver
/// assembly. Sill and sash-latch roots are wrapped in SessionState + stable-GlobalObjectId evidence epochs,
/// so script/assembly reloads cannot silently reinterpret replacement geometry as a fresh baseline. This is
/// evidence-integrity infrastructure only: it awards zero Visual Fidelity points and cannot clear a rendered
/// critical defect without sealed native-4K evidence.
/// </summary>
public static class QualityBlockReflectionMaterialPhysicalityBindingQA
{
    private const string ContractPath = "Assets/QA/reflection_material_physicality_binding_contract.json";
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string MaterialPhysicalityContractPath = "Assets/QA/registered_material_asset_physicality_contract.json";
    private const string FacadeGlazingGasketContractPath = "Assets/QA/facade_glazing_gasket_contract.json";
    private const string FacadeSillDrainageReflectionContractPath = "Assets/QA/facade_sill_drainage_reflection_binding_contract.json";
    private const string FacadeSashLatchContractPath = "Assets/QA/facade_sash_latch_contract.json";
    private const string FacadeSashLatchReflectionContractPath = "Assets/QA/facade_sash_latch_reflection_binding_contract.json";
    private const string FacadeSashLatchCurveContractPath = "Assets/QA/facade_sash_latch_curve_refinement_contract.json";
    private const string FacadeSashLatchLodContinuityContractPath = "Assets/QA/facade_sash_latch_lod_continuity_contract.json";
    private const string FacadeSashLatchFarLodContinuityContractPath = "Assets/QA/facade_sash_latch_far_lod_continuity_contract.json";
    private const string ContractSchema = "1.7";

    private static readonly string[] CanonicalCriticalRisks =
    {
        "impossible_material_physics",
        "missing_construction_material_metadata",
        "baked_or_painted_highlights"
    };

    [MenuItem("NewTown/QA/Validate Reflection Material Physicality Binding Contract")]
    public static void ValidateContractConfigOnly()
    {
        BindingContract contract = LoadJson<BindingContract>(ContractPath);
        if (contract == null || !string.Equals(contract.schemaVersion, ContractSchema, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Reflection material physicality binding contract is null/unparseable or not schema " + ContractSchema + ".");
        if (!string.Equals(contract.scenePath, ScenePath, StringComparison.Ordinal))
            throw new InvalidOperationException("Reflection material physicality binding scene identity drifted.");
        if (!string.Equals(contract.materialPhysicalityContractPath, MaterialPhysicalityContractPath, StringComparison.Ordinal))
            throw new InvalidOperationException("Reflection material physicality binding no longer targets the canonical registered-material contract.");
        if (!string.Equals(contract.facadeGlazingGasketContractPath, FacadeGlazingGasketContractPath, StringComparison.Ordinal))
            throw new InvalidOperationException("Reflection material physicality binding no longer targets the canonical facade glazing-gasket contract.");
        if (!string.Equals(contract.facadeSillDrainageReflectionContractPath, FacadeSillDrainageReflectionContractPath, StringComparison.Ordinal))
            throw new InvalidOperationException("Reflection material physicality binding no longer targets the canonical facade sill-drainage reflection contract.");
        if (!string.Equals(contract.facadeSashLatchContractPath, FacadeSashLatchContractPath, StringComparison.Ordinal))
            throw new InvalidOperationException("Reflection material physicality binding no longer targets the canonical facade sash-latch contract.");
        if (!string.Equals(contract.facadeSashLatchReflectionContractPath, FacadeSashLatchReflectionContractPath, StringComparison.Ordinal))
            throw new InvalidOperationException("Reflection material physicality binding no longer targets the reload-stable sash-latch reflection contract.");
        if (!string.Equals(contract.facadeSashLatchCurveContractPath, FacadeSashLatchCurveContractPath, StringComparison.Ordinal))
            throw new InvalidOperationException("Reflection material physicality binding no longer targets the canonical sash-latch curve-refinement contract.");
        if (!string.Equals(contract.facadeSashLatchLodContinuityContractPath, FacadeSashLatchLodContinuityContractPath, StringComparison.Ordinal))
            throw new InvalidOperationException("Reflection material physicality binding no longer targets the canonical sash-latch LOD-continuity contract.");
        if (!string.Equals(contract.facadeSashLatchFarLodContinuityContractPath, FacadeSashLatchFarLodContinuityContractPath, StringComparison.Ordinal))
            throw new InvalidOperationException("Reflection material physicality binding no longer targets the canonical sash-latch far-LOD-continuity contract.");

        Requirements r = contract.requirements;
        if (r == null ||
            !r.validateMaterialPhysicalityBeforeEveryReflectionLightingFingerprint ||
            !r.validateFacadeGlazingGasketBeforeEveryReflectionLightingFingerprint ||
            !r.ensureFacadeSillDrainageBeforeFirstReflectionBaseline ||
            !r.validateFacadeSillDrainageBeforeEveryReflectionLightingFingerprint ||
            !r.ensureFacadeSashLatchCurveBeforeFirstReflectionBaseline ||
            !r.validateFacadeSashLatchCurveBeforeEveryReflectionLightingFingerprint ||
            !r.ensureFacadeSashLatchLodContinuityBeforeFirstReflectionBaseline ||
            !r.validateFacadeSashLatchLodContinuityBeforeEveryReflectionLightingFingerprint ||
            !r.ensureFacadeSashLatchFarLodContinuityBeforeFirstReflectionBaseline ||
            !r.validateFacadeSashLatchFarLodContinuityBeforeEveryReflectionLightingFingerprint ||
            !r.ensureFacadeSashLatchBeforeFirstReflectionBaseline ||
            !r.validateFacadeSashLatchBeforeEveryReflectionLightingFingerprint ||
            !r.persistFacadeSashLatchEpochAcrossAssemblyReloads ||
            !r.freezeFacadeSashLatchStableGlobalObjectIdAfterEpochArm ||
            !r.reflectionFingerprintUsedBeforeRenderProbeRequest ||
            !r.reflectionFingerprintRevalidatedOnEveryEditorPoll ||
            !r.reflectionFingerprintRevalidatedAtCompletion ||
            !r.reflectionFingerprintRevalidatedImmediatelyBeforeStillCapture ||
            !r.physicalityValidationMustBeReportFreeDuringFingerprinting ||
            !r.facadeGlazingGasketValidationMustBeReadOnlyDuringFingerprinting ||
            !r.facadeSillDrainageValidationMustBeReadOnlyAfterEpochArm ||
            !r.facadeSashLatchCurveValidationMustBeReadOnlyAfterEpochArm ||
            !r.facadeSashLatchLodContinuityValidationMustBeReadOnlyAfterEpochArm ||
            !r.facadeSashLatchFarLodContinuityValidationMustBeReadOnlyAfterEpochArm ||
            !r.facadeSashLatchValidationMustBeReadOnlyAfterEpochArm ||
            !r.physicalityFailureAbortsReflectionEvidence ||
            !r.facadeGlazingGasketFailureAbortsReflectionEvidence ||
            !r.facadeSillDrainageFailureAbortsReflectionEvidence ||
            !r.facadeSashLatchCurveFailureAbortsReflectionEvidence ||
            !r.facadeSashLatchLodContinuityFailureAbortsReflectionEvidence ||
            !r.facadeSashLatchFarLodContinuityFailureAbortsReflectionEvidence ||
            !r.facadeSashLatchFailureAbortsReflectionEvidence ||
            !r.actualRenderRequiredForVisualPoints)
            throw new InvalidOperationException("Reflection material physicality binding requirements were weakened or are incomplete.");

        RequireExactSet(contract.criticalDefectRisksReduced, CanonicalCriticalRisks, "criticalDefectRisksReduced");
        if (contract.visualFidelityPointsAwarded != 0 || contract.runtimeRenderVerified)
            throw new InvalidOperationException("Reflection material physicality binding may not award visual points or claim runtime verification.");
        if (contract.limitations == null || contract.limitations.Length < 10 || contract.limitations.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Reflection material physicality binding limitations are missing or incomplete.");

        QualityBlockRegisteredMaterialAssetPhysicalityQA.ValidateContractConfigOnly();
        QualityBlockFacadeGlazingGasketUpgrade.ValidateContractConfigOnly();
        QualityBlockFacadeSillDrainageReflectionBindingQA.ValidateContractConfigOnly();
        QualityBlockFacadeSashLatchReflectionBindingQA.ValidateContractConfigOnly();
        QualityBlockSashLatchCurveRefinement.ValidateContractConfigOnly();
        QualityBlockSashLatchLodContinuityRefinement.ValidateContractConfigOnly();
        QualityBlockSashLatchFarLodContinuityRefinement.ValidateContractConfigOnly();
        QualityBlockFacadeSashLatchUpgrade.ValidateContractConfigOnly();
        QualityBlockReflectionLightingCoverageQA.ValidateContractConfigOnly();
        QualityBlockReflectionProbeStateCoherenceQA.ValidateContractConfigOnly();
        QualityBlockReflectionDetailPhysicalUvBindingQA.ValidateContractConfigOnly();
        QualityBlockFacadeFormalBuildBinding.ValidateContractConfigOnly();
        QualityBlockFoliageFormalEvidenceBindingQA.ValidateContractConfigOnly();
        QualityBlockBalconyDrainageWaterproofingQA.ValidateContractConfigOnly();
        QualityBlockBalconySurfaceMicrostructureQA.ValidateContractConfigOnly();
    }

    /// <summary>
    /// Called from every formal reflection-lighting fingerprint evaluation. Deterministic preparation is
    /// allowed only before each dedicated evidence epoch is armed. Thereafter every delegated construction
    /// path is read-only and any root/mesh/material/LOD drift aborts reflection evidence.
    /// </summary>
    public static void ValidateOpenScene()
    {
        ValidateContractConfigOnly();
        QualityBlockFacadeFormalBuildBinding.EnsurePreparedForFormalEvidence();
        QualityBlockFacadeSillDrainageReflectionBindingQA.EnsurePreparedForFormalEvidence();

        // Asset-cache refinements must precede parent latch construction so the first generated root binds
        // the intended continuous curved silhouettes at every LOD.
        QualityBlockSashLatchCurveRefinement.EnsurePreparedForFormalEvidence();
        QualityBlockSashLatchLodContinuityRefinement.EnsurePreparedForFormalEvidence();
        QualityBlockSashLatchFarLodContinuityRefinement.EnsurePreparedForFormalEvidence();

        // The reload-stable wrapper delegates parent creation only on the first clean-session baseline.
        // After SessionState epoch arm it bypasses the parent's static epoch fields and performs read-only
        // stable-identity + construction validation, surviving script/assembly reloads fail-closed.
        QualityBlockFacadeSashLatchReflectionBindingQA.EnsurePreparedForFormalEvidence();

        QualityBlockRegisteredMaterialAssetPhysicalityQA.ValidateForFormalEvidence();
        QualityBlockReflectionDetailPhysicalUvBindingQA.ValidateOpenScene();
        QualityBlockFoliageFormalEvidenceBindingQA.ValidateOpenScene();
        QualityBlockBalconyDrainageWaterproofingQA.ValidateOpenScene();
        QualityBlockBalconySurfaceMicrostructureQA.ValidateOpenScene();
        QualityBlockFacadeGlazingGasketUpgrade.ValidateOpenScene();

        // Redundant read-only validation at the central fingerprint boundary makes post-arm intent explicit.
        QualityBlockFacadeSillDrainageReflectionBindingQA.ValidateForReflectionEvidence();
        QualityBlockSashLatchCurveRefinement.ValidateGeneratedAssets();
        QualityBlockSashLatchLodContinuityRefinement.ValidateGeneratedAssets();
        QualityBlockSashLatchFarLodContinuityRefinement.ValidateGeneratedAssets();
        QualityBlockFacadeSashLatchReflectionBindingQA.ValidateForReflectionEvidence();
    }

    private static void RequireExactSet(string[] actual, string[] expected, string label)
    {
        if (actual == null)
            throw new InvalidOperationException(label + " is missing.");
        if (actual.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException(label + " contains a blank value.");
        if (actual.Distinct(StringComparer.Ordinal).Count() != actual.Length)
            throw new InvalidOperationException(label + " contains duplicates.");
        if (!new HashSet<string>(actual, StringComparer.Ordinal).SetEquals(expected ?? Array.Empty<string>()))
            throw new InvalidOperationException(
                label + " must be exactly [" + string.Join(", ", expected ?? Array.Empty<string>()) + "], got [" +
                string.Join(", ", actual) + "].");
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
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrWhiteSpace(projectRoot))
            throw new InvalidOperationException("Could not resolve Unity project root for reflection material binding.");
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    [Serializable]
    private sealed class BindingContract
    {
        public string schemaVersion;
        public string purpose;
        public string scenePath;
        public string materialPhysicalityContractPath;
        public string facadeGlazingGasketContractPath;
        public string facadeSillDrainageReflectionContractPath;
        public string facadeSashLatchContractPath;
        public string facadeSashLatchReflectionContractPath;
        public string facadeSashLatchCurveContractPath;
        public string facadeSashLatchLodContinuityContractPath;
        public string facadeSashLatchFarLodContinuityContractPath;
        public Requirements requirements;
        public string[] criticalDefectRisksReduced;
        public int visualFidelityPointsAwarded;
        public bool runtimeRenderVerified;
        public string[] limitations;
    }

    [Serializable]
    private sealed class Requirements
    {
        public bool validateMaterialPhysicalityBeforeEveryReflectionLightingFingerprint;
        public bool validateFacadeGlazingGasketBeforeEveryReflectionLightingFingerprint;
        public bool ensureFacadeSillDrainageBeforeFirstReflectionBaseline;
        public bool validateFacadeSillDrainageBeforeEveryReflectionLightingFingerprint;
        public bool ensureFacadeSashLatchCurveBeforeFirstReflectionBaseline;
        public bool validateFacadeSashLatchCurveBeforeEveryReflectionLightingFingerprint;
        public bool ensureFacadeSashLatchLodContinuityBeforeFirstReflectionBaseline;
        public bool validateFacadeSashLatchLodContinuityBeforeEveryReflectionLightingFingerprint;
        public bool ensureFacadeSashLatchFarLodContinuityBeforeFirstReflectionBaseline;
        public bool validateFacadeSashLatchFarLodContinuityBeforeEveryReflectionLightingFingerprint;
        public bool ensureFacadeSashLatchBeforeFirstReflectionBaseline;
        public bool validateFacadeSashLatchBeforeEveryReflectionLightingFingerprint;
        public bool persistFacadeSashLatchEpochAcrossAssemblyReloads;
        public bool freezeFacadeSashLatchStableGlobalObjectIdAfterEpochArm;
        public bool reflectionFingerprintUsedBeforeRenderProbeRequest;
        public bool reflectionFingerprintRevalidatedOnEveryEditorPoll;
        public bool reflectionFingerprintRevalidatedAtCompletion;
        public bool reflectionFingerprintRevalidatedImmediatelyBeforeStillCapture;
        public bool physicalityValidationMustBeReportFreeDuringFingerprinting;
        public bool facadeGlazingGasketValidationMustBeReadOnlyDuringFingerprinting;
        public bool facadeSillDrainageValidationMustBeReadOnlyAfterEpochArm;
        public bool facadeSashLatchCurveValidationMustBeReadOnlyAfterEpochArm;
        public bool facadeSashLatchLodContinuityValidationMustBeReadOnlyAfterEpochArm;
        public bool facadeSashLatchFarLodContinuityValidationMustBeReadOnlyAfterEpochArm;
        public bool facadeSashLatchValidationMustBeReadOnlyAfterEpochArm;
        public bool physicalityFailureAbortsReflectionEvidence;
        public bool facadeGlazingGasketFailureAbortsReflectionEvidence;
        public bool facadeSillDrainageFailureAbortsReflectionEvidence;
        public bool facadeSashLatchCurveFailureAbortsReflectionEvidence;
        public bool facadeSashLatchLodContinuityFailureAbortsReflectionEvidence;
        public bool facadeSashLatchFarLodContinuityFailureAbortsReflectionEvidence;
        public bool facadeSashLatchFailureAbortsReflectionEvidence;
        public bool actualRenderRequiredForVisualPoints;
    }
}
