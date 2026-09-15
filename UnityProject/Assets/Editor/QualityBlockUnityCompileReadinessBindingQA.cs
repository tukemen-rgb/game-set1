using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Fail-closed bridge between the legacy existence-based 7-point compile readiness check and the
/// stale-proof Unity compile verifier. Invalid evidence is removed synchronously on editor-domain load
/// and again whenever the project changes, so a stale/forged unity_compile_verified.json cannot survive
/// as a truthy file-presence signal for Implementation Readiness.
///
/// This bridge never creates compile evidence and awards zero Visual Fidelity points.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockUnityCompileReadinessBindingQA
{
    private const string ContractPath = "Assets/QA/unity_compile_readiness_binding_contract.json";
    private const string VerificationContractPath = "Assets/QA/unity_compile_verification_contract.json";
    private const string EvidencePath = "Assets/QA/unity_compile_verified.json";
    private const string ExpectedConsumer = "QualityBlockVisualFidelityGate.WriteImplementationReadinessScorecard";

    static QualityBlockUnityCompileReadinessBindingQA()
    {
        EditorApplication.projectChanged -= SanitizeCompileEvidence;
        EditorApplication.projectChanged += SanitizeCompileEvidence;

        // Synchronous by design: -executeMethod readiness scoring after a domain load must not observe a
        // stale file during a delayCall window.
        SanitizeCompileEvidence();
    }

    [MenuItem("NewTown/QA/Validate Unity Compile Readiness Binding")]
    public static void ValidateBindingContractConfigOnly()
    {
        string absolute = AbsoluteProjectPath(ContractPath);
        Require(File.Exists(absolute), "Unity compile readiness binding contract is missing: " + ContractPath);

        BindingContract contract = JsonUtility.FromJson<BindingContract>(File.ReadAllText(absolute));
        Require(contract != null, "Unity compile readiness binding contract is null/unparseable.");
        Require(string.Equals(contract.schemaVersion, "1.0", StringComparison.Ordinal),
            $"Compile readiness binding schema must remain 1.0, got '{contract.schemaVersion}'.");
        Require(string.Equals(contract.guardedEvidencePath, EvidencePath, StringComparison.Ordinal),
            "Compile readiness binding guarded evidence path drifted.");
        Require(string.Equals(contract.compileVerificationContractPath, VerificationContractPath, StringComparison.Ordinal),
            "Compile readiness binding lost the authoritative compile verification contract.");
        Require(string.Equals(contract.readinessConsumer, ExpectedConsumer, StringComparison.Ordinal),
            "Compile readiness binding consumer drifted away from the central readiness scorer.");
        Require(contract.rules != null &&
                contract.rules.validateImmediatelyOnEditorDomainLoad &&
                contract.rules.validateOnProjectChanged &&
                contract.rules.deleteEvidenceWhenValidationFails &&
                contract.rules.neverCreateEvidenceFromSourceSideQA &&
                contract.rules.missingEvidenceMeansNoCompileReadinessPoints &&
                contract.rules.staleOrForgedEvidenceMustNotSurviveToReadinessScoring,
            "Compile readiness binding fail-closed rules were weakened.");
        Require(contract.rules.visualFidelityPointsAwarded == 0,
            "Compile readiness binding must never award Visual Fidelity points.");
        Require(contract.verification != null &&
                !contract.verification.runtimeCompileVerified &&
                contract.verification.implementationReadinessScoreUntilFreshEvidence == 93 &&
                contract.verification.visualFidelityPointsAwarded == 0 &&
                string.Equals(contract.verification.visualFidelityStatus,
                    "UNSCORED_UNTIL_REAL_4K_RENDER", StringComparison.Ordinal),
            "Source-side compile readiness binding must remain render/compile-pending and zero-point for Visual Fidelity.");
    }

    [MenuItem("NewTown/QA/Sanitize Unity Compile Readiness Evidence")]
    public static void SanitizeCompileEvidence()
    {
        string absoluteEvidence = AbsoluteProjectPath(EvidencePath);
        if (!File.Exists(absoluteEvidence))
            return;

        string reason;
        try
        {
            ValidateBindingContractConfigOnly();
            if (QualityBlockUnityCompileVerificationQA.TryValidateEvidenceForReadiness(out reason))
                return;
        }
        catch (Exception ex)
        {
            reason = "binding validation failed: " + ex.Message;
        }

        try
        {
            File.Delete(absoluteEvidence);
            Debug.LogWarning(
                "Removed invalid/stale Unity compile evidence before Implementation Readiness could count it: " + reason +
                ". Readiness compile points remain unavailable until a new real CleanBuildCache verification completes.");
        }
        catch (Exception deleteEx)
        {
            throw new InvalidOperationException(
                "Fail-closed compile evidence sanitizer could not remove invalid evidence. " +
                "Do not run Implementation Readiness scoring until this is resolved. Validation reason: " + reason,
                deleteEx);
        }
    }

    private static string AbsoluteProjectPath(string projectRelativePath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot,
            projectRelativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    [Serializable]
    private sealed class BindingContract
    {
        public string schemaVersion;
        public string guardedEvidencePath;
        public string compileVerificationContractPath;
        public string readinessConsumer;
        public BindingRules rules;
        public BindingVerification verification;
    }

    [Serializable]
    private sealed class BindingRules
    {
        public bool validateImmediatelyOnEditorDomainLoad;
        public bool validateOnProjectChanged;
        public bool deleteEvidenceWhenValidationFails;
        public bool neverCreateEvidenceFromSourceSideQA;
        public bool missingEvidenceMeansNoCompileReadinessPoints;
        public bool staleOrForgedEvidenceMustNotSurviveToReadinessScoring;
        public int visualFidelityPointsAwarded;
    }

    [Serializable]
    private sealed class BindingVerification
    {
        public bool runtimeCompileVerified;
        public int implementationReadinessScoreUntilFreshEvidence;
        public int visualFidelityPointsAwarded;
        public string visualFidelityStatus;
    }
}
