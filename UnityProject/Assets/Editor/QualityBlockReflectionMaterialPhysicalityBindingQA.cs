using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Binds the registered-material physicality guard into the reflection-lighting fingerprint path.
/// QualityBlockReflectionProbeAwaiter evaluates that fingerprint before RenderProbe(), on every observed
/// Editor poll, at completion and immediately before still capture; therefore a material-registry drift
/// cannot enter or survive the formal cubemap baseline merely because MainCamera pre-cull has not run yet.
/// The same entrypoint also chains the manufacturing-scale detail-UV binding and the facade formal-build
/// binding, closing equivalent ReflectionProbe blind spots for normalized/stale UV meshes, missing
/// aperture construction and missing occupancy variation.
///
/// This is evidence-integrity infrastructure only. It awards zero Visual Fidelity points and cannot clear
/// any critical defect without sealed native-4K rendered evidence.
/// </summary>
public static class QualityBlockReflectionMaterialPhysicalityBindingQA
{
    private const string ContractPath = "Assets/QA/reflection_material_physicality_binding_contract.json";
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string MaterialPhysicalityContractPath = "Assets/QA/registered_material_asset_physicality_contract.json";

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
        if (contract == null || !string.Equals(contract.schemaVersion, "1.0", StringComparison.Ordinal))
            throw new InvalidOperationException("Reflection material physicality binding contract is null/unparseable or not schema 1.0.");
        if (!string.Equals(contract.scenePath, ScenePath, StringComparison.Ordinal))
            throw new InvalidOperationException("Reflection material physicality binding scene identity drifted.");
        if (!string.Equals(contract.materialPhysicalityContractPath, MaterialPhysicalityContractPath, StringComparison.Ordinal))
            throw new InvalidOperationException("Reflection material physicality binding no longer targets the canonical registered-material contract.");

        Requirements r = contract.requirements;
        if (r == null ||
            !r.validateMaterialPhysicalityBeforeEveryReflectionLightingFingerprint ||
            !r.reflectionFingerprintUsedBeforeRenderProbeRequest ||
            !r.reflectionFingerprintRevalidatedOnEveryEditorPoll ||
            !r.reflectionFingerprintRevalidatedAtCompletion ||
            !r.reflectionFingerprintRevalidatedImmediatelyBeforeStillCapture ||
            !r.physicalityValidationMustBeReportFreeDuringFingerprinting ||
            !r.physicalityFailureAbortsReflectionEvidence ||
            !r.actualRenderRequiredForVisualPoints)
            throw new InvalidOperationException("Reflection material physicality binding requirements were weakened or are incomplete.");

        RequireExactSet(contract.criticalDefectRisksReduced, CanonicalCriticalRisks, "criticalDefectRisksReduced");
        if (contract.visualFidelityPointsAwarded != 0 || contract.runtimeRenderVerified)
            throw new InvalidOperationException("Reflection material physicality binding may not award Visual Fidelity points or claim runtime render verification.");
        if (contract.limitations == null || contract.limitations.Length < 3 || contract.limitations.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Reflection material physicality binding limitations are missing or incomplete.");

        // Keep the upstream material contract itself fail-closed before declaring this binding valid.
        QualityBlockRegisteredMaterialAssetPhysicalityQA.ValidateContractConfigOnly();
        // QualityBlockVisualGateIntegrityQA already invokes this reflection pre-probe binding. Validate the
        // sibling reflection-lighting coverage contract here as well so a direct numeric-gate integrity path
        // cannot retain stale critical-defect aliases or omit legal-but-render-changing light policy state.
        QualityBlockReflectionLightingCoverageQA.ValidateContractConfigOnly();
        // The same core integrity path must also freeze the exact canonical ReflectionProbe configuration.
        // Otherwise a weakened influence/capture-volume contract could remain dormant until runtime and the
        // source-side 92-point gate integrity check would not notice it.
        QualityBlockReflectionProbeStateCoherenceQA.ValidateContractConfigOnly();
        // ReflectionProbe.RenderProbe does not invoke MainCamera pre-cull. Keep manufacture-scale UV and
        // phase-diversity policy bound to this already-central pre-probe integrity entrypoint as well.
        QualityBlockReflectionDetailPhysicalUvBindingQA.ValidateContractConfigOnly();
        // The same formal reflection boundary must not accept the facade-optics-only fallback when higher-
        // fidelity rough-opening, metric-UV and occupancy passes are present in source but absent from scene.
        QualityBlockFacadeFormalBuildBinding.ValidateContractConfigOnly();
    }

    /// <summary>
    /// Called from every formal reflection-lighting fingerprint evaluation. The delegated validations are
    /// deliberately report-free during the in-flight probe stage. The facade binding may create missing
    /// generated passes only on the first pre-RenderProbe evaluation; once present, it validates/fails
    /// closed rather than silently rebuilding drift during subsequent polls.
    /// </summary>
    public static void ValidateOpenScene()
    {
        ValidateContractConfigOnly();
        QualityBlockFacadeFormalBuildBinding.EnsurePreparedForFormalEvidence();
        QualityBlockRegisteredMaterialAssetPhysicalityQA.ValidateForFormalEvidence();
        QualityBlockReflectionDetailPhysicalUvBindingQA.ValidateOpenScene();
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
        public bool reflectionFingerprintUsedBeforeRenderProbeRequest;
        public bool reflectionFingerprintRevalidatedOnEveryEditorPoll;
        public bool reflectionFingerprintRevalidatedAtCompletion;
        public bool reflectionFingerprintRevalidatedImmediatelyBeforeStillCapture;
        public bool physicalityValidationMustBeReportFreeDuringFingerprinting;
        public bool physicalityFailureAbortsReflectionEvidence;
        public bool actualRenderRequiredForVisualPoints;
    }
}
