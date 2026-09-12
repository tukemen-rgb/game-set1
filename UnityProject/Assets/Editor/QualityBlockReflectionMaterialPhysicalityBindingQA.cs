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
/// assembly. The first curve/LOD-continuity calls may create or repair deterministic mesh assets before the
/// first reflection baseline and arm their dependency hashes; the first parent latch and sill calls may create
/// genuinely missing generated passes. Subsequent calls are read-only and fail closed on curve/proxy/root
/// replacement or construction drift. This is evidence-integrity infrastructure only: it awards zero Visual
/// Fidelity points and cannot clear a rendered critical defect without sealed native-4K evidence.
/// </summary>
public static class QualityBlockReflectionMaterialPhysicalityBindingQA
{
    private const string ContractPath = "Assets/QA/reflection_material_physicality_binding_contract.json";
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string MaterialPhysicalityContractPath = "Assets/QA/registered_material_asset_physicality_contract.json";
    private const string FacadeGlazingGasketContractPath = "Assets/QA/facade_glazing_gasket_contract.json";
    private const string FacadeSillDrainageReflectionContractPath = "Assets/QA/facade_sill_drainage_reflection_binding_contract.json";
    private const string FacadeSashLatchContractPath = "Assets/QA/facade_sash_latch_contract.json";
    private const string FacadeSashLatchCurveContractPath = "Assets/QA/facade_sash_latch_curve_refinement_contract.json";
    private const string FacadeSashLatchLodContinuityContractPath = "Assets/QA/facade_sash_latch_lod_continuity_contract.json";
    private const string FacadeSashLatchFarLodContinuityContractPath = "Assets/QA/facade_sash_latch_far_lod_continuity_contract.json";

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
        if (contract == null || !string.Equals(contract.schemaVersion, "1.6", StringComparison.Ordinal))
            throw new InvalidOperationException("Reflection material physicality binding contract is null/unparseable or not schema 1.6.");
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
            throw new InvalidOperationException("Reflection material physicality binding may not award Visual Fidelity points or claim runtime render verification.");
        if (contract.limitations == null || contract.limitations.Length < 9 || contract.limitations.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Reflection material physicality binding limitations are missing or incomplete.");

        QualityBlockRegisteredMaterialAssetPhysicalityQA.ValidateContractConfigOnly();
        QualityBlockFacadeGlazingGasketUpgrade.ValidateContractConfigOnly();
        QualityBlockFacadeSillDrainageReflectionBindingQA.ValidateContractConfigOnly();
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
    /// Called from every formal reflection-lighting fingerprint evaluation. The delegated validations are
    /// report-free during an in-flight probe stage. The facade formal binding, sill binding, curve refinements,
    /// LOD-continuity refinements and sash-latch binding may create genuinely missing generated state only on
    /// their first pre-RenderProbe evaluation; after each epoch is armed, missing/replaced/drifted state aborts
    /// instead of mutating evidence. Foliage, balcony, microsurface and glazing-gasket checks remain read-only.
    /// </summary>
    public static void ValidateOpenScene()
    {
        ValidateContractConfigOnly();
        QualityBlockFacadeFormalBuildBinding.EnsurePreparedForFormalEvidence();

        // The sill pass contains real drain openings and metal surfaces that materially affect the cubemap.
        // Prepare it once before the first baseline, then freeze the generated-root identity so later
        // request/poll/completion/pre-still fingerprints cannot repair stale reflection evidence in place.
        QualityBlockFacadeSillDrainageReflectionBindingQA.EnsurePreparedForFormalEvidence();

        // Seed/repair the exact GM_SashLatch_CrescentLever_P* asset paths before the parent latch builder is
        // allowed to create or validate its root. This guarantees the first cubemap baseline cannot capture
        // the legacy six-box LOD0 curve while the still camera later sees the refined asset cache.
        QualityBlockSashLatchCurveRefinement.EnsurePreparedForFormalEvidence();

        // Preserve the manufactured crescent silhouette through the first cross-fade.
        QualityBlockSashLatchLodContinuityRefinement.EnsurePreparedForFormalEvidence();

        // Replace the broad rectangular LOD2 and single-sliver LOD3 payloads at the exact phase-specific asset
        // paths the parent builder already references. The first baseline therefore sees the same physical curve
        // envelope through all four LODs rather than allowing a later still/temporal pass to mutate far proxies.
        QualityBlockSashLatchFarLodContinuityRefinement.EnsurePreparedForFormalEvidence();

        // Replace the known low-fidelity twin rubber handle blocks with one physically assembled metal
        // crescent latch + receiver per two-panel window before the first cubemap baseline. The class itself
        // freezes the generated-root instance ID after this call, so repeated fingerprint polls are read-only.
        QualityBlockFacadeSashLatchUpgrade.EnsurePreparedForFormalEvidence();
        QualityBlockRegisteredMaterialAssetPhysicalityQA.ValidateForFormalEvidence();
        QualityBlockReflectionDetailPhysicalUvBindingQA.ValidateOpenScene();
        QualityBlockFoliageFormalEvidenceBindingQA.ValidateOpenScene();
        QualityBlockBalconyDrainageWaterproofingQA.ValidateOpenScene();
        QualityBlockBalconySurfaceMicrostructureQA.ValidateOpenScene();
        QualityBlockFacadeGlazingGasketUpgrade.ValidateOpenScene();

        // Redundant read-only validation at the central fingerprint boundary makes the post-arm intent
        // explicit and catches root replacement, old-sill re-enablement, curve/proxy-cache drift,
        // legacy-handle re-enablement, mesh/material drift or LOD changes.
        QualityBlockFacadeSillDrainageReflectionBindingQA.ValidateForReflectionEvidence();
        QualityBlockSashLatchCurveRefinement.ValidateGeneratedAssets();
        QualityBlockSashLatchLodContinuityRefinement.ValidateGeneratedAssets();
        QualityBlockSashLatchFarLodContinuityRefinement.ValidateGeneratedAssets();
        QualityBlockFacadeSashLatchUpgrade.ValidateOpenScene();
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
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
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
