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
/// balcony drainage/floor fall, dry waterproof microstructure, glazing-gasket construction and the
/// period-plausible metal crescent-latch/receiver assembly. The first latch call may create a genuinely
/// missing generated pass before the reflection epoch is armed; subsequent calls are read-only and fail
/// closed on root replacement/deletion. This is evidence-integrity infrastructure only: it awards zero
/// Visual Fidelity points and cannot clear a rendered critical defect without sealed native-4K evidence.
/// </summary>
public static class QualityBlockReflectionMaterialPhysicalityBindingQA
{
    private const string ContractPath = "Assets/QA/reflection_material_physicality_binding_contract.json";
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string MaterialPhysicalityContractPath = "Assets/QA/registered_material_asset_physicality_contract.json";
    private const string FacadeGlazingGasketContractPath = "Assets/QA/facade_glazing_gasket_contract.json";
    private const string FacadeSashLatchContractPath = "Assets/QA/facade_sash_latch_contract.json";

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
        if (contract == null || !string.Equals(contract.schemaVersion, "1.2", StringComparison.Ordinal))
            throw new InvalidOperationException("Reflection material physicality binding contract is null/unparseable or not schema 1.2.");
        if (!string.Equals(contract.scenePath, ScenePath, StringComparison.Ordinal))
            throw new InvalidOperationException("Reflection material physicality binding scene identity drifted.");
        if (!string.Equals(contract.materialPhysicalityContractPath, MaterialPhysicalityContractPath, StringComparison.Ordinal))
            throw new InvalidOperationException("Reflection material physicality binding no longer targets the canonical registered-material contract.");
        if (!string.Equals(contract.facadeGlazingGasketContractPath, FacadeGlazingGasketContractPath, StringComparison.Ordinal))
            throw new InvalidOperationException("Reflection material physicality binding no longer targets the canonical facade glazing-gasket contract.");
        if (!string.Equals(contract.facadeSashLatchContractPath, FacadeSashLatchContractPath, StringComparison.Ordinal))
            throw new InvalidOperationException("Reflection material physicality binding no longer targets the canonical facade sash-latch contract.");

        Requirements r = contract.requirements;
        if (r == null ||
            !r.validateMaterialPhysicalityBeforeEveryReflectionLightingFingerprint ||
            !r.validateFacadeGlazingGasketBeforeEveryReflectionLightingFingerprint ||
            !r.ensureFacadeSashLatchBeforeFirstReflectionBaseline ||
            !r.validateFacadeSashLatchBeforeEveryReflectionLightingFingerprint ||
            !r.reflectionFingerprintUsedBeforeRenderProbeRequest ||
            !r.reflectionFingerprintRevalidatedOnEveryEditorPoll ||
            !r.reflectionFingerprintRevalidatedAtCompletion ||
            !r.reflectionFingerprintRevalidatedImmediatelyBeforeStillCapture ||
            !r.physicalityValidationMustBeReportFreeDuringFingerprinting ||
            !r.facadeGlazingGasketValidationMustBeReadOnlyDuringFingerprinting ||
            !r.facadeSashLatchValidationMustBeReadOnlyAfterEpochArm ||
            !r.physicalityFailureAbortsReflectionEvidence ||
            !r.facadeGlazingGasketFailureAbortsReflectionEvidence ||
            !r.facadeSashLatchFailureAbortsReflectionEvidence ||
            !r.actualRenderRequiredForVisualPoints)
            throw new InvalidOperationException("Reflection material physicality binding requirements were weakened or are incomplete.");

        RequireExactSet(contract.criticalDefectRisksReduced, CanonicalCriticalRisks, "criticalDefectRisksReduced");
        if (contract.visualFidelityPointsAwarded != 0 || contract.runtimeRenderVerified)
            throw new InvalidOperationException("Reflection material physicality binding may not award Visual Fidelity points or claim runtime render verification.");
        if (contract.limitations == null || contract.limitations.Length < 5 || contract.limitations.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Reflection material physicality binding limitations are missing or incomplete.");

        QualityBlockRegisteredMaterialAssetPhysicalityQA.ValidateContractConfigOnly();
        QualityBlockFacadeGlazingGasketUpgrade.ValidateContractConfigOnly();
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
    /// report-free during an in-flight probe stage. The facade formal binding and sash-latch binding may
    /// create genuinely missing generated passes only on their first pre-RenderProbe evaluation; after each
    /// epoch is armed, missing/replaced geometry aborts instead of mutating evidence. Foliage, balcony,
    /// microsurface and glazing-gasket checks remain read-only throughout.
    /// </summary>
    public static void ValidateOpenScene()
    {
        ValidateContractConfigOnly();
        QualityBlockFacadeFormalBuildBinding.EnsurePreparedForFormalEvidence();
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
        // Redundant read-only validation makes the post-arm intent explicit at the central fingerprint
        // boundary and catches legacy-handle re-enablement, mesh/material drift or LOD assignment changes.
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
        public string facadeSashLatchContractPath;
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
        public bool ensureFacadeSashLatchBeforeFirstReflectionBaseline;
        public bool validateFacadeSashLatchBeforeEveryReflectionLightingFingerprint;
        public bool reflectionFingerprintUsedBeforeRenderProbeRequest;
        public bool reflectionFingerprintRevalidatedOnEveryEditorPoll;
        public bool reflectionFingerprintRevalidatedAtCompletion;
        public bool reflectionFingerprintRevalidatedImmediatelyBeforeStillCapture;
        public bool physicalityValidationMustBeReportFreeDuringFingerprinting;
        public bool facadeGlazingGasketValidationMustBeReadOnlyDuringFingerprinting;
        public bool facadeSashLatchValidationMustBeReadOnlyAfterEpochArm;
        public bool physicalityFailureAbortsReflectionEvidence;
        public bool facadeGlazingGasketFailureAbortsReflectionEvidence;
        public bool facadeSashLatchFailureAbortsReflectionEvidence;
        public bool actualRenderRequiredForVisualPoints;
    }
}
