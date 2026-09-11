using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Binds manufacturing-scale detail UV validation into the reflection-lighting fingerprint path.
/// ReflectionProbe.RenderProbe does not invoke the formal MainCamera pre-cull callback, so the prepared
/// DanchiHighDetail source set must be checked explicitly before the probe request and at every existing
/// request/poll/completion/pre-still fingerprint boundary. This is evidence-integrity infrastructure only:
/// it awards zero Visual Fidelity points and cannot clear rendered repetition, seams or shimmer.
/// </summary>
public static class QualityBlockReflectionDetailPhysicalUvBindingQA
{
    private const string ContractPath = "Assets/QA/reflection_detail_physical_uv_binding_contract.json";
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string DetailPhysicalUvContractPath = "Assets/QA/detail_physical_uv_contract.json";

    private static readonly string[] CanonicalCriticalRisks =
    {
        "obvious_repetition",
        "severe_aliasing_or_shimmer"
    };

    [MenuItem("NewTown/QA/Validate Reflection Detail Physical UV Binding Contract")]
    public static void ValidateContractConfigOnly()
    {
        BindingContract contract = LoadJson<BindingContract>(ContractPath);
        if (contract == null || !string.Equals(contract.schemaVersion, "1.0", StringComparison.Ordinal))
            throw new InvalidOperationException("Reflection detail physical-UV binding contract is null/unparseable or not schema 1.0.");
        if (!string.Equals(contract.status, "IMPLEMENTATION_READY_RENDER_PENDING", StringComparison.Ordinal))
            throw new InvalidOperationException("Reflection detail physical-UV binding must remain implementation-ready/render-pending until real Unity evidence exists.");
        if (!string.Equals(contract.scenePath, ScenePath, StringComparison.Ordinal))
            throw new InvalidOperationException("Reflection detail physical-UV binding scene identity drifted.");
        if (!string.Equals(contract.detailPhysicalUvContractPath, DetailPhysicalUvContractPath, StringComparison.Ordinal))
            throw new InvalidOperationException("Reflection detail physical-UV binding no longer targets the canonical detail physical-UV contract.");

        Requirements r = contract.requirements;
        if (r == null ||
            !r.validateDetailPhysicalUvBeforeEveryReflectionLightingFingerprint ||
            !r.reflectionFingerprintUsedBeforeRenderProbeRequest ||
            !r.reflectionFingerprintRevalidatedOnEveryEditorPoll ||
            !r.reflectionFingerprintRevalidatedAtCompletion ||
            !r.reflectionFingerprintRevalidatedImmediatelyBeforeStillCapture ||
            !r.physicalUvValidationMustBeReportFreeDuringFingerprinting ||
            !r.physicalUvFailureAbortsReflectionEvidence ||
            !r.actualRenderRequiredForVisualPoints)
            throw new InvalidOperationException("Reflection detail physical-UV binding requirements were weakened or are incomplete.");

        RequireExactSet(contract.criticalDefectRisksReduced, CanonicalCriticalRisks, "criticalDefectRisksReduced");
        if (contract.visualFidelityPointsAwarded != 0 ||
            contract.implementationReadinessScore != 93 ||
            !string.Equals(contract.visualFidelityStatus, "UNSCORED_UNTIL_REAL_4K_RENDER", StringComparison.Ordinal) ||
            contract.runtimeRenderVerified)
            throw new InvalidOperationException("Reflection detail physical-UV binding must remain readiness=93, render-pending and worth zero automatic Visual Fidelity points.");
        if (contract.limitations == null || contract.limitations.Length < 4 || contract.limitations.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Reflection detail physical-UV binding limitations are missing or incomplete.");

        // Keep the upstream manufacturing-scale UV contract fail-closed before this reflection binding
        // is considered valid. The upstream config validator also preserves the exact formal crop/risk IDs.
        QualityBlockDetailPhysicalUvQA.ValidateContractConfigOnly();
    }

    /// <summary>
    /// Called from the reflection-lighting fingerprint path. The delegated lightweight scene validation
    /// performs no report generation or scene mutation, so repeated Editor-poll checks remain side-effect free.
    /// </summary>
    public static void ValidateOpenScene()
    {
        ValidateContractConfigOnly();
        QualityBlockDetailPhysicalUvQA.ValidateOpenScene(false);
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
        public string status;
        public string purpose;
        public string scenePath;
        public string detailPhysicalUvContractPath;
        public Requirements requirements;
        public string[] criticalDefectRisksReduced;
        public int visualFidelityPointsAwarded;
        public int implementationReadinessScore;
        public string visualFidelityStatus;
        public bool runtimeRenderVerified;
        public string[] limitations;
    }

    [Serializable]
    private sealed class Requirements
    {
        public bool validateDetailPhysicalUvBeforeEveryReflectionLightingFingerprint;
        public bool reflectionFingerprintUsedBeforeRenderProbeRequest;
        public bool reflectionFingerprintRevalidatedOnEveryEditorPoll;
        public bool reflectionFingerprintRevalidatedAtCompletion;
        public bool reflectionFingerprintRevalidatedImmediatelyBeforeStillCapture;
        public bool physicalUvValidationMustBeReportFreeDuringFingerprinting;
        public bool physicalUvFailureAbortsReflectionEvidence;
        public bool actualRenderRequiredForVisualPoints;
    }
}
