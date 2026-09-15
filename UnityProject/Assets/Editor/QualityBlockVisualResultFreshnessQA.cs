using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Fail-closed guard for persisted Visual Fidelity PASS results.
///
/// The numeric gate already proves the 92/100 threshold, category minima, critical-defect review and
/// render provenance at evaluation time. This guard closes a separate lifecycle gap: an older PASS
/// result file must not remain a credible release assertion after reviewed evidence, still/temporal
/// provenance, candidate state, or gate contracts change and a later evaluation fails before it can
/// overwrite the old result.
///
/// This class never creates PASS and awards zero Visual Fidelity points. It only independently
/// revalidates an existing PASS or overwrites it with INVALIDATED_REEVALUATION_REQUIRED.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockVisualResultFreshnessQA
{
    private const string ContractPath = "Assets/QA/visual_result_freshness_contract.json";
    private const string ResultPath = "Assets/QA/visual_fidelity_result.json";
    private const string EvidencePath = "Assets/QA/visual_fidelity_evidence.json";
    private const string ReportPath = "Assets/QA/visual_fidelity_result_freshness.json";
    private const string GateConfigPath = "Assets/QA/visual_fidelity_gate.json";
    private const string ObservabilityPath = "Assets/QA/visual_evidence_observability_contract.json";
    private const string ReviewedIntegrityContractPath = "Assets/QA/reviewed_visual_evidence_integrity_contract.json";
    private const string RenderReceiptPath = "Assets/QA/render_capture_receipt.json";
    private const string CandidateSealPath = "Assets/QA/render_candidate_seal.json";
    private const string CandidateEpochPath = "Assets/QA/render_candidate_epoch.json";
    private const string StillManifestPath = "Assets/QA/4k_capture_manifest.json";
    private const string TemporalRuntimeReceiptPath = "Assets/QA/temporal_runtime_evidence_receipt.json";
    private const string TemporalCoherenceSealPath = "Assets/QA/temporal_candidate_coherence_seal.json";

    private const int RequiredThreshold = 92;

    private static readonly CategorySpec[] CanonicalCategories =
    {
        new CategorySpec("geometry_construction", 20, 18),
        new CategorySpec("material_pbr", 20, 18),
        new CategorySpec("lighting_shadows_reflections", 15, 13),
        new CategorySpec("texture_microdetail", 10, 9),
        new CategorySpec("weathering_causality", 10, 9),
        new CategorySpec("vegetation_natural_complexity", 8, 7),
        new CategorySpec("period_authenticity", 7, 6),
        new CategorySpec("cinematic_image", 5, 4),
        new CategorySpec("temporal_lod_aliasing", 5, 4)
    };

    private static readonly string[] TrackedInputPaths =
    {
        ContractPath,
        ResultPath,
        EvidencePath,
        GateConfigPath,
        ObservabilityPath,
        ReviewedIntegrityContractPath,
        RenderReceiptPath,
        CandidateSealPath,
        CandidateEpochPath,
        StillManifestPath,
        TemporalRuntimeReceiptPath,
        TemporalCoherenceSealPath
    };

    private static bool _checking;
    private static bool _projectCheckRequested = true;
    private static bool _hierarchyCheckRequested = true;
    private static double _nextPollTime;
    private static string _lastInputFingerprint = string.Empty;

    static QualityBlockVisualResultFreshnessQA()
    {
        EditorApplication.update += Poll;
        EditorApplication.projectChanged += RequestProjectCheck;
        EditorApplication.hierarchyChanged += RequestHierarchyCheck;
        EditorApplication.delayCall += BootstrapAfterDomainReload;
    }

    [MenuItem("NewTown/QA/Validate Visual Result Freshness Contract")]
    public static void ValidateContractConfigOnly()
    {
        Contract contract = LoadJson<Contract>(ContractPath);
        Require(contract != null && string.Equals(contract.schemaVersion, "1.0", StringComparison.Ordinal),
            "Visual-result freshness contract is missing or unsupported; schema 1.0 is required.");
        Require(string.Equals(contract.resultPath, ResultPath, StringComparison.Ordinal) &&
                string.Equals(contract.evidencePath, EvidencePath, StringComparison.Ordinal) &&
                string.Equals(contract.reportPath, ReportPath, StringComparison.Ordinal),
            "Visual-result freshness contract paths drifted.");
        Require(!contract.runtimeRenderVerified,
            "Visual-result freshness contract may not claim runtime render verification.");
        Require(!contract.visualScoreAwardedByThisQA,
            "Visual-result freshness guard may not award Visual Fidelity points.");

        Rules rules = contract.rules;
        Require(rules != null &&
                rules.protectOnlyPassAssertions &&
                rules.automaticEditorMonitoring &&
                rules.invalidatePassOnAnyValidationException &&
                rules.requireVisualGateIntegrityValidation &&
                rules.requireReviewedEvidenceIntegrityValidation &&
                rules.requireRenderEvidenceProvenanceValidation &&
                rules.requireCurrentCandidateFreshnessValidation &&
                rules.requireTemporalCandidateCoherenceValidation &&
                rules.independentlyRecomputeCategoryTotal &&
                rules.requireEveryCategoryHardMinimum &&
                rules.requireThreshold92 &&
                rules.requireNoCriticalDefects &&
                rules.requireResultScoreEqualsEvidenceTotal &&
                rules.requireResultFailuresEmptyForPass &&
                rules.invalidationCannotProducePass &&
                rules.automaticVisualPoints == 0,
            "Visual-result freshness rules were weakened or are incomplete.");
    }

    [MenuItem("NewTown/QA/Validate Current Visual Result Freshness")]
    public static void ValidateCurrentResultFreshness()
    {
        GuardCurrentPass(throwOnFailure: true);
    }

    private static void BootstrapAfterDomainReload()
    {
        _projectCheckRequested = true;
        _hierarchyCheckRequested = true;
        Poll();
    }

    private static void RequestProjectCheck()
    {
        _projectCheckRequested = true;
    }

    private static void RequestHierarchyCheck()
    {
        _hierarchyCheckRequested = true;
    }

    private static void Poll()
    {
        if (_checking || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        double now = EditorApplication.timeSinceStartup;
        if (!_projectCheckRequested && !_hierarchyCheckRequested && now < _nextPollTime)
            return;

        _nextPollTime = now + 2.0;
        bool hierarchyRequested = _hierarchyCheckRequested;
        _projectCheckRequested = false;
        _hierarchyCheckRequested = false;

        string fingerprint;
        try
        {
            fingerprint = ComputeTrackedInputFingerprint();
        }
        catch (Exception ex)
        {
            Debug.LogError("Visual-result freshness input fingerprint failed closed: " + ex.Message);
            return;
        }

        if (!hierarchyRequested &&
            string.Equals(fingerprint, _lastInputFingerprint, StringComparison.OrdinalIgnoreCase))
            return;

        _lastInputFingerprint = fingerprint;
        _checking = true;
        try
        {
            GuardCurrentPass(throwOnFailure: false);
        }
        finally
        {
            _checking = false;
        }
    }

    private static void GuardCurrentPass(bool throwOnFailure)
    {
        try
        {
            ValidateContractConfigOnly();

            if (!File.Exists(AbsolutePath(ResultPath)))
                return;

            QualityBlockVisualFidelityGate.VisualGateResult result =
                LoadJson<QualityBlockVisualFidelityGate.VisualGateResult>(ResultPath);
            if (result == null)
                throw new InvalidOperationException("visual_fidelity_result.json could not be parsed.");

            if (!string.Equals(result.visualFidelityStatus, "PASS", StringComparison.Ordinal))
            {
                WriteReport(
                    "NO_ACTIVE_PASS_ASSERTION",
                    result,
                    "Persisted result is not PASS; freshness guard has no positive release assertion to protect.");
                return;
            }

            ValidateExistingPass(result);
            WriteReport(
                "CURRENT_PASS_REVALIDATED",
                result,
                "Existing PASS independently revalidated against current gate, reviewed evidence, still/temporal provenance and candidate freshness. This guard awards zero points.");

            Debug.Log(
                $"Visual-result freshness VALID: current PASS={result.visualScore}/100, threshold={result.threshold}. " +
                "The guard awarded 0 Visual Fidelity points.");
        }
        catch (Exception ex)
        {
            bool invalidated = InvalidatePersistedPass(ex.Message);
            Debug.LogError(
                "Visual-result freshness failed closed: " + ex.Message +
                (invalidated ? " Previous PASS was invalidated." : string.Empty));

            if (throwOnFailure)
                throw;
        }
    }

    private static void ValidateExistingPass(QualityBlockVisualFidelityGate.VisualGateResult result)
    {
        // Re-run the exact non-scoring structural/provenance guards used by the numeric gate. These calls
        // deliberately include current still candidate freshness and temporal candidate coherence.
        QualityBlockVisualGateIntegrityQA.ValidateContract();
        QualityBlockReviewedVisualEvidenceIntegrityQA.ValidateReviewedEvidence();
        QualityBlockRenderEvidenceProvenanceQA.ValidateEvidenceProvenance();
        QualityBlockRenderCandidateFreshnessQA.ValidateCurrentCandidateFreshness();
        QualityBlockTemporalCandidateCoherenceQA.ValidateForScoring();

        GateHeader gate = LoadJson<GateHeader>(GateConfigPath);
        Require(gate != null && gate.visualPassThreshold == RequiredThreshold,
            $"Current Visual Fidelity threshold must remain {RequiredThreshold}.");
        Require(!string.IsNullOrWhiteSpace(gate.gateVersion),
            "Current visual_fidelity_gate.json is missing gateVersion.");
        Require(string.Equals(result.gateVersion, gate.gateVersion, StringComparison.Ordinal),
            $"Persisted PASS gateVersion '{result.gateVersion}' does not match current gate '{gate.gateVersion}'.");

        QualityBlockVisualFidelityGate.VisualEvidence evidence =
            LoadJson<QualityBlockVisualFidelityGate.VisualEvidence>(EvidencePath);
        Require(evidence != null && evidence.renderVerified,
            "Current reviewed evidence is missing or renderVerified is not true.");

        Require(evidence.categories != null && evidence.categories.Length == CanonicalCategories.Length,
            "Current reviewed evidence must contain exactly nine categories.");

        int total = 0;
        foreach (CategorySpec spec in CanonicalCategories)
        {
            QualityBlockVisualFidelityGate.CategoryEvidence[] matches =
                evidence.categories
                    .Where(x => x != null && string.Equals(x.id, spec.id, StringComparison.Ordinal))
                    .ToArray();
            Require(matches.Length == 1,
                $"Freshness revalidation expected exactly one category '{spec.id}', got {matches.Length}.");

            int score = matches[0].score;
            Require(score >= spec.hardMinimum && score <= spec.weight,
                $"Category '{spec.id}' is {score}/{spec.weight}; hard minimum is {spec.hardMinimum}.");
            total += score;
        }

        Require(total >= RequiredThreshold,
            $"Current reviewed category total is {total}/100, below PASS threshold {RequiredThreshold}.");
        Require(result.visualScore == total,
            $"Persisted PASS score {result.visualScore} does not equal current reviewed category total {total}.");
        Require(result.threshold == RequiredThreshold,
            $"Persisted PASS threshold {result.threshold} does not equal required threshold {RequiredThreshold}.");

        Require(evidence.criticalDefects != null && evidence.criticalDefects.Length == 12,
            "Current reviewed evidence must contain exactly twelve critical-defect decisions.");
        foreach (QualityBlockVisualFidelityGate.CriticalDefectEvidence defect in evidence.criticalDefects)
        {
            Require(defect != null, "Current critical-defect evidence contains a null entry.");
            Require(!defect.present,
                $"Critical automatic-fail defect '{defect.id}' is present; persisted PASS is invalid.");
        }

        Require(result.failures == null || result.failures.Length == 0,
            "A persisted PASS result may not contain gate failures.");
        Require(!string.IsNullOrWhiteSpace(result.evaluatedUtc) &&
                DateTime.TryParse(
                    result.evaluatedUtc,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out _),
            "Persisted PASS evaluatedUtc is missing or invalid.");

        // A revalidated PASS must still be backed by every expected provenance record. Hashes are copied
        // into the freshness report below so later mutation is machine-observable.
        foreach (string path in RequiredPassProvenancePaths())
            Require(File.Exists(AbsolutePath(path)), $"Required current PASS provenance file is missing: {path}");
    }

    private static bool InvalidatePersistedPass(string reason)
    {
        if (!File.Exists(AbsolutePath(ResultPath)))
            return false;

        QualityBlockVisualFidelityGate.VisualGateResult previous = null;
        try
        {
            previous = LoadJson<QualityBlockVisualFidelityGate.VisualGateResult>(ResultPath);
        }
        catch
        {
            // A malformed result cannot be trusted as PASS. Continue by replacing it with an invalidated record.
        }

        if (previous != null &&
            !string.Equals(previous.visualFidelityStatus, "PASS", StringComparison.Ordinal))
        {
            WriteReport(
                "NO_ACTIVE_PASS_ASSERTION",
                previous,
                "Validation failed, but the persisted result was already non-PASS. No positive assertion was rewritten.");
            return false;
        }

        var invalidated = new QualityBlockVisualFidelityGate.VisualGateResult
        {
            gateVersion = previous != null ? previous.gateVersion : "unknown",
            evaluatedUtc = DateTime.UtcNow.ToString("O"),
            visualFidelityStatus = "INVALIDATED_REEVALUATION_REQUIRED",
            visualScore = 0,
            threshold = RequiredThreshold,
            failures = new[]
            {
                "Previous PASS invalidated by visual-result freshness guard: " + SanitizeReason(reason)
            },
            note =
                "This file no longer contains a valid Visual Fidelity score. A new PASS may be written only by " +
                "QualityBlockVisualFidelityGate after current real Unity 4K still + temporal evidence passes every gate."
        };

        WriteJsonWithoutRefresh(ResultPath, invalidated);
        WriteReport(
            "PASS_INVALIDATED_REEVALUATION_REQUIRED",
            invalidated,
            SanitizeReason(reason));
        _lastInputFingerprint = ComputeTrackedInputFingerprint();
        return true;
    }

    private static void WriteReport(
        string status,
        QualityBlockVisualFidelityGate.VisualGateResult result,
        string note)
    {
        var report = new FreshnessReport
        {
            schemaVersion = "1.0",
            checkedUtc = DateTime.UtcNow.ToString("O"),
            status = status,
            protectedResultStatus = result != null ? result.visualFidelityStatus : string.Empty,
            visualScore = result != null ? result.visualScore : 0,
            threshold = RequiredThreshold,
            resultSha256 = HashIfPresent(ResultPath),
            evidenceSha256 = HashIfPresent(EvidencePath),
            renderReceiptSha256 = HashIfPresent(RenderReceiptPath),
            renderCandidateSealSha256 = HashIfPresent(CandidateSealPath),
            renderCandidateEpochSha256 = HashIfPresent(CandidateEpochPath),
            stillManifestSha256 = HashIfPresent(StillManifestPath),
            temporalRuntimeReceiptSha256 = HashIfPresent(TemporalRuntimeReceiptPath),
            temporalCoherenceSealSha256 = HashIfPresent(TemporalCoherenceSealPath),
            gateConfigSha256 = HashIfPresent(GateConfigPath),
            observabilityContractSha256 = HashIfPresent(ObservabilityPath),
            reviewedIntegrityContractSha256 = HashIfPresent(ReviewedIntegrityContractPath),
            visualFidelityPointsAwardedByThisQA = 0,
            runtimeRenderVerifiedByThisQA = false,
            note = note
        };
        WriteJsonWithoutRefresh(ReportPath, report);
    }

    private static IEnumerable<string> RequiredPassProvenancePaths()
    {
        yield return EvidencePath;
        yield return RenderReceiptPath;
        yield return CandidateSealPath;
        yield return CandidateEpochPath;
        yield return StillManifestPath;
        yield return TemporalRuntimeReceiptPath;
        yield return TemporalCoherenceSealPath;
    }

    private static string ComputeTrackedInputFingerprint()
    {
        var canonical = new StringBuilder();
        foreach (string path in TrackedInputPaths.OrderBy(x => x, StringComparer.Ordinal))
            canonical.Append(path).Append('=').Append(HashIfPresent(path)).Append('|');
        return Sha256Utf8(canonical.ToString());
    }

    private static string HashIfPresent(string projectRelativePath)
    {
        string absolute = AbsolutePath(projectRelativePath);
        return File.Exists(absolute) ? Sha256File(absolute) : "MISSING";
    }

    private static string Sha256File(string absolutePath)
    {
        using (SHA256 sha = SHA256.Create())
        using (FileStream stream = File.OpenRead(absolutePath))
            return BytesToHex(sha.ComputeHash(stream));
    }

    private static string Sha256Utf8(string value)
    {
        using (SHA256 sha = SHA256.Create())
            return BytesToHex(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty)));
    }

    private static string BytesToHex(byte[] bytes)
    {
        return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static string SanitizeReason(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "unspecified_validation_failure";
        string oneLine = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return oneLine.Length <= 1000 ? oneLine : oneLine.Substring(0, 1000);
    }

    private static T LoadJson<T>(string projectRelativePath) where T : class
    {
        string absolute = AbsolutePath(projectRelativePath);
        Require(File.Exists(absolute), $"Required QA file is missing: {projectRelativePath}");
        T value = JsonUtility.FromJson<T>(File.ReadAllText(absolute));
        Require(value != null, $"Could not parse QA JSON: {projectRelativePath}");
        return value;
    }

    private static void WriteJsonWithoutRefresh<T>(string projectRelativePath, T value)
    {
        string absolute = AbsolutePath(projectRelativePath);
        string directory = Path.GetDirectoryName(absolute);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(absolute, JsonUtility.ToJson(value, true) + Environment.NewLine);
    }

    private static string AbsolutePath(string projectRelativePath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(projectRoot, projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private readonly struct CategorySpec
    {
        public readonly string id;
        public readonly int weight;
        public readonly int hardMinimum;

        public CategorySpec(string id, int weight, int hardMinimum)
        {
            this.id = id;
            this.weight = weight;
            this.hardMinimum = hardMinimum;
        }
    }

    [Serializable]
    private sealed class GateHeader
    {
        public string gateVersion;
        public int visualPassThreshold;
    }

    [Serializable]
    private sealed class Contract
    {
        public string schemaVersion;
        public string resultPath;
        public string evidencePath;
        public string reportPath;
        public Rules rules;
        public bool visualScoreAwardedByThisQA;
        public bool runtimeRenderVerified;
    }

    [Serializable]
    private sealed class Rules
    {
        public bool protectOnlyPassAssertions;
        public bool automaticEditorMonitoring;
        public bool invalidatePassOnAnyValidationException;
        public bool requireVisualGateIntegrityValidation;
        public bool requireReviewedEvidenceIntegrityValidation;
        public bool requireRenderEvidenceProvenanceValidation;
        public bool requireCurrentCandidateFreshnessValidation;
        public bool requireTemporalCandidateCoherenceValidation;
        public bool independentlyRecomputeCategoryTotal;
        public bool requireEveryCategoryHardMinimum;
        public bool requireThreshold92;
        public bool requireNoCriticalDefects;
        public bool requireResultScoreEqualsEvidenceTotal;
        public bool requireResultFailuresEmptyForPass;
        public bool invalidationCannotProducePass;
        public int automaticVisualPoints;
    }

    [Serializable]
    private sealed class FreshnessReport
    {
        public string schemaVersion;
        public string checkedUtc;
        public string status;
        public string protectedResultStatus;
        public int visualScore;
        public int threshold;
        public string resultSha256;
        public string evidenceSha256;
        public string renderReceiptSha256;
        public string renderCandidateSealSha256;
        public string renderCandidateEpochSha256;
        public string stillManifestSha256;
        public string temporalRuntimeReceiptSha256;
        public string temporalCoherenceSealSha256;
        public string gateConfigSha256;
        public string observabilityContractSha256;
        public string reviewedIntegrityContractSha256;
        public int visualFidelityPointsAwardedByThisQA;
        public bool runtimeRenderVerifiedByThisQA;
        public string note;
    }
}
