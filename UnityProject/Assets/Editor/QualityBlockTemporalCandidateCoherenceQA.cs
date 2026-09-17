using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Fail-closed bridge between temporal/LOD evidence and the exact visual candidate that is eligible
/// for native-4K still scoring. The temporal runtime receipt already proves frame bytes, lighting and
/// tonemap execution; this guard closes a different gap: those valid temporal frames must not survive
/// a later scene/material/shader/settings/capture-recipe mutation while new still evidence is captured.
///
/// A coherence seal is provenance only. It awards zero Visual Fidelity points and cannot decide whether
/// shimmer, aliasing or visible LOD pop is present in the actual pixels.
/// </summary>
public static class QualityBlockTemporalCandidateCoherenceQA
{
    private const string ContractPath = "Assets/QA/temporal_candidate_coherence_contract.json";
    private const string TemporalRuntimeReceiptPath = "Assets/QA/temporal_runtime_evidence_receipt.json";
    private const string CandidateSealPath = "Assets/QA/render_candidate_seal.json";
    private const string CandidateEpochPath = "Assets/QA/render_candidate_epoch.json";
    private const string CoherenceSealPath = "Assets/QA/temporal_candidate_coherence_seal.json";
    private const string InvalidationPath = "Assets/QA/temporal_candidate_coherence_invalidation.json";
    private const string ReviewedIntegritySourcePath = "Assets/Editor/QualityBlockReviewedVisualEvidenceIntegrityQA.cs";
    private const string RequiredScoringInvocation = "QualityBlockTemporalCandidateCoherenceQA.ValidateForScoring();";

    [MenuItem("NewTown/QA/Validate Temporal Candidate Coherence Contract")]
    public static void ValidateContractConfigOnly()
    {
        Contract contract = LoadJson<Contract>(ContractPath);
        Require(contract != null && contract.schemaVersion == "1.0",
            "Temporal candidate coherence contract is missing or unsupported; schema 1.0 is required.");
        Require(contract.temporalRuntimeReceiptPath == TemporalRuntimeReceiptPath &&
                contract.renderCandidateSealPath == CandidateSealPath &&
                contract.renderCandidateEpochPath == CandidateEpochPath &&
                contract.coherenceSealPath == CoherenceSealPath &&
                contract.scoringIntegrationSource == ReviewedIntegritySourcePath,
            "Temporal candidate coherence contract paths drifted.");
        Require(contract.rules != null &&
                contract.rules.requireCurrentStillCandidateFreshness &&
                contract.rules.requireTemporalRuntimeReceiptFromUnity &&
                contract.rules.requireCandidateCombinedSha256Match &&
                contract.rules.requireCandidateEpochMatch &&
                contract.rules.requireSceneSha256Match &&
                contract.rules.requireTemporalReceiptHashMatch &&
                contract.rules.requireTemporalGeneratedUtcNotBeforeCandidateMutation &&
                contract.rules.requireTemporalGeneratedUtcNotBeforeLatestTrackedInputWrite &&
                contract.rules.invalidateSealWhenBindingCannotBeProven &&
                contract.rules.manualPixelReviewStillRequired &&
                contract.rules.automaticVisualPoints == 0,
            "Temporal candidate coherence rules were weakened or are incomplete.");

        string reviewedSource = ReadProjectText(ReviewedIntegritySourcePath);
        Require(reviewedSource.Contains(RequiredScoringInvocation, StringComparison.Ordinal),
            "Reviewed Visual Fidelity scoring path lost the temporal-candidate coherence invocation.");

        Debug.Log("Temporal candidate coherence contract valid: current still candidate + temporal runtime evidence must share candidate epoch/scene identity; zero automatic visual points.");
    }

    [MenuItem("NewTown/QA/Seal Current Temporal Candidate Coherence")]
    public static void SealCurrentTemporalCandidateCoherence()
    {
        try
        {
            SealCurrentReceiptInternal();
            Debug.Log("Temporal candidate coherence seal written. Visual Fidelity remains UNSCORED until direct pixel review and the numeric gate.");
        }
        catch (Exception ex)
        {
            Invalidate("manual_seal_failed: " + ex.Message);
            throw;
        }
    }

    [MenuItem("NewTown/QA/Validate Temporal Candidate Coherence For Scoring")]
    public static void ValidateForScoring()
    {
        ValidateContractConfigOnly();

        // This proves the active persisted scene, recursive dependencies, project render settings and
        // formal still-capture recipe still equal the currently sealed native-4K candidate.
        QualityBlockRenderCandidateFreshnessQA.ValidateCurrentCandidateFreshness();

        TemporalRuntimeReceipt temporal = LoadJson<TemporalRuntimeReceipt>(TemporalRuntimeReceiptPath);
        CandidateSeal candidate = LoadJson<CandidateSeal>(CandidateSealPath);
        CandidateEpoch epoch = LoadJson<CandidateEpoch>(CandidateEpochPath);
        CoherenceSeal coherence = LoadJson<CoherenceSeal>(CoherenceSealPath);

        ValidateSourceRecords(temporal, candidate, epoch);

        string temporalReceiptSha = Sha256ProjectFile(TemporalRuntimeReceiptPath);
        Require(coherence != null && coherence.schemaVersion == "1.0",
            "Temporal candidate coherence seal is missing/unsupported; recapture or reseal only after current-candidate temporal rendering.");
        Require(coherence.automaticVisualPoints == 0 && coherence.visualFidelityStatus == "UNSCORED_REVIEW_REQUIRED",
            "Temporal candidate coherence seal may not award points or claim PASS.");
        Require(EqualsSha(coherence.temporalRuntimeReceiptSha256, temporalReceiptSha),
            "Temporal runtime receipt bytes changed after candidate coherence was sealed.");
        Require(string.Equals(coherence.temporalCaptureSessionId, temporal.captureSessionId, StringComparison.Ordinal),
            "Temporal candidate coherence seal belongs to a different temporal capture session.");
        Require(EqualsSha(coherence.candidateCombinedSha256, candidate.candidateCombinedSha256) &&
                EqualsSha(coherence.candidateCombinedSha256, epoch.candidateCombinedSha256),
            "Temporal evidence is not sealed to the current render-candidate combined SHA-256.");
        Require(string.Equals(coherence.candidateEpochUtc, epoch.lastCandidateMutationUtc, StringComparison.Ordinal) &&
                string.Equals(coherence.candidateEpochUtc, candidate.candidateEpochUtc, StringComparison.Ordinal),
            "Temporal evidence belongs to an older render-candidate epoch.");
        Require(EqualsSha(coherence.sceneSha256, candidate.sceneSha256) && EqualsSha(temporal.sceneSha256, candidate.sceneSha256),
            "Temporal evidence scene SHA-256 differs from the current native-4K still candidate.");

        DateTime temporalUtc = ParseUtc(temporal.generatedUtc, "temporal runtime generatedUtc");
        DateTime mutationUtc = ParseUtc(epoch.lastCandidateMutationUtc, "render candidate epoch");
        DateTime latestInputUtc = ParseUtc(candidate.latestInputWriteUtc, "render candidate latestInputWriteUtc");
        Require(temporalUtc >= mutationUtc && temporalUtc >= latestInputUtc,
            "Temporal frames predate the current candidate mutation/input epoch. Fresh temporal rendering is required before scoring temporal/LOD/aliasing evidence.");

        Debug.Log(
            $"Temporal candidate coherence VALID: temporalSession={temporal.captureSessionId}, candidateSHA256={candidate.candidateCombinedSha256}, " +
            $"epoch={epoch.lastCandidateMutationUtc}. This QA awards 0 Visual Fidelity points; shimmer/LOD/aliasing still require direct pixel review.");
    }

    internal static void ProcessImportedTemporalRuntimeReceipt()
    {
        try
        {
            SealCurrentReceiptInternal();
        }
        catch (Exception ex)
        {
            Invalidate("receipt_import_seal_failed: " + ex.Message);
            Debug.LogError("Temporal candidate coherence sealing failed closed: " + ex.Message);
        }
    }

    private static void SealCurrentReceiptInternal()
    {
        ValidateContractConfigOnly();
        QualityBlockRenderCandidateFreshnessQA.ValidateCurrentCandidateFreshness();

        TemporalRuntimeReceipt temporal = LoadJson<TemporalRuntimeReceipt>(TemporalRuntimeReceiptPath);
        CandidateSeal candidate = LoadJson<CandidateSeal>(CandidateSealPath);
        CandidateEpoch epoch = LoadJson<CandidateEpoch>(CandidateEpochPath);
        ValidateSourceRecords(temporal, candidate, epoch);

        DateTime temporalUtc = ParseUtc(temporal.generatedUtc, "temporal runtime generatedUtc");
        DateTime mutationUtc = ParseUtc(epoch.lastCandidateMutationUtc, "render candidate epoch");
        DateTime latestInputUtc = ParseUtc(candidate.latestInputWriteUtc, "render candidate latestInputWriteUtc");
        Require(temporalUtc >= mutationUtc && temporalUtc >= latestInputUtc,
            "Refusing to bind stale temporal evidence: runtime receipt predates current candidate mutation/latest tracked input write.");

        var seal = new CoherenceSeal
        {
            schemaVersion = "1.0",
            sealedUtc = DateTime.UtcNow.ToString("O"),
            temporalCaptureSessionId = temporal.captureSessionId,
            temporalRuntimeReceiptPath = TemporalRuntimeReceiptPath,
            temporalRuntimeReceiptSha256 = Sha256ProjectFile(TemporalRuntimeReceiptPath),
            temporalGeneratedUtc = temporal.generatedUtc,
            candidateCombinedSha256 = candidate.candidateCombinedSha256,
            candidateEpochUtc = epoch.lastCandidateMutationUtc,
            candidateLatestInputWriteUtc = candidate.latestInputWriteUtc,
            sceneAssetPath = candidate.sceneAssetPath,
            sceneGuid = candidate.sceneGuid,
            sceneSha256 = candidate.sceneSha256,
            automaticVisualPoints = 0,
            visualFidelityStatus = "UNSCORED_REVIEW_REQUIRED",
            note = "Provenance bridge only. Current temporal runtime evidence is bound to the same render-candidate identity/epoch as current native-4K still evidence; direct pixel review remains mandatory."
        };
        WriteJsonWithoutRefresh(CoherenceSealPath, seal);
        DeleteIfExists(InvalidationPath);
    }

    private static void ValidateSourceRecords(TemporalRuntimeReceipt temporal, CandidateSeal candidate, CandidateEpoch epoch)
    {
        Require(temporal != null && temporal.schemaVersion == "1.0" && temporal.renderProducedByUnity,
            "Temporal runtime evidence receipt is missing, unsupported, or not produced by Unity.");
        Require(!string.IsNullOrWhiteSpace(temporal.captureSessionId) &&
                temporal.automaticVisualPoints == 0 &&
                temporal.visualFidelityStatus == "UNSCORED_REVIEW_REQUIRED",
            "Temporal runtime receipt identity/scoring policy is invalid.");
        Require(candidate != null && candidate.schemaVersion == "1.0" &&
                !string.IsNullOrWhiteSpace(candidate.candidateCombinedSha256) &&
                candidate.candidateCombinedSha256.Length == 64,
            "Current render candidate seal is missing or invalid.");
        Require(epoch != null && epoch.schemaVersion == "1.0" &&
                !string.IsNullOrWhiteSpace(epoch.candidateCombinedSha256) &&
                epoch.candidateCombinedSha256.Length == 64,
            "Current render candidate epoch is missing or invalid.");
        Require(EqualsSha(candidate.candidateCombinedSha256, epoch.candidateCombinedSha256),
            "Render candidate seal and epoch disagree on current candidate SHA-256.");
        Require(string.Equals(candidate.candidateEpochUtc, epoch.lastCandidateMutationUtc, StringComparison.Ordinal),
            "Render candidate seal and epoch disagree on latest candidate mutation time.");
        Require(EqualsSha(temporal.sceneSha256, candidate.sceneSha256),
            "Temporal runtime scene bytes do not match current still-candidate scene bytes.");
    }

    private static void Invalidate(string reason)
    {
        DeleteIfExists(CoherenceSealPath);
        try
        {
            var invalidation = new Invalidation
            {
                schemaVersion = "1.0",
                invalidatedUtc = DateTime.UtcNow.ToString("O"),
                reason = reason,
                automaticVisualPoints = 0,
                visualFidelityStatus = "UNSCORED_REVIEW_REQUIRED"
            };
            WriteJsonWithoutRefresh(InvalidationPath, invalidation);
        }
        catch (Exception writeEx)
        {
            Debug.LogError("Could not write temporal candidate coherence invalidation receipt: " + writeEx.Message);
        }
    }

    private static T LoadJson<T>(string assetPath) where T : class
    {
        string absolute = AbsolutePath(assetPath);
        if (!File.Exists(absolute))
            throw new FileNotFoundException($"Required QA file not found: {assetPath}");
        T value = JsonUtility.FromJson<T>(File.ReadAllText(absolute));
        if (value == null)
            throw new InvalidOperationException($"Could not parse QA JSON: {assetPath}");
        return value;
    }

    private static string ReadProjectText(string assetPath)
    {
        string absolute = AbsolutePath(assetPath);
        if (!File.Exists(absolute))
            throw new FileNotFoundException($"Required source file not found: {assetPath}");
        return File.ReadAllText(absolute);
    }

    private static string Sha256ProjectFile(string assetPath)
    {
        string absolute = AbsolutePath(assetPath);
        if (!File.Exists(absolute))
            throw new FileNotFoundException($"Cannot hash missing QA file: {assetPath}");
        using (SHA256 sha = SHA256.Create())
        using (FileStream stream = File.OpenRead(absolute))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static DateTime ParseUtc(string value, string label)
    {
        if (!DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime parsed))
            throw new InvalidOperationException($"{label} is missing or not ISO-8601: '{value}'.");
        return parsed.ToUniversalTime();
    }

    private static bool EqualsSha(string a, string b) =>
        !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b) &&
        string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void WriteJsonWithoutRefresh<T>(string assetPath, T value)
    {
        string absolute = AbsolutePath(assetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute));
        File.WriteAllText(absolute, JsonUtility.ToJson(value, true));
    }

    private static void DeleteIfExists(string assetPath)
    {
        string absolute = AbsolutePath(assetPath);
        if (File.Exists(absolute))
            File.Delete(absolute);
    }

    private static string AbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    [Serializable]
    private sealed class Contract
    {
        public string schemaVersion;
        public string temporalRuntimeReceiptPath;
        public string renderCandidateSealPath;
        public string renderCandidateEpochPath;
        public string coherenceSealPath;
        public string scoringIntegrationSource;
        public Rules rules;
    }

    [Serializable]
    private sealed class Rules
    {
        public bool requireCurrentStillCandidateFreshness;
        public bool requireTemporalRuntimeReceiptFromUnity;
        public bool requireCandidateCombinedSha256Match;
        public bool requireCandidateEpochMatch;
        public bool requireSceneSha256Match;
        public bool requireTemporalReceiptHashMatch;
        public bool requireTemporalGeneratedUtcNotBeforeCandidateMutation;
        public bool requireTemporalGeneratedUtcNotBeforeLatestTrackedInputWrite;
        public bool invalidateSealWhenBindingCannotBeProven;
        public bool manualPixelReviewStillRequired;
        public int automaticVisualPoints;
    }

    [Serializable]
    private sealed class TemporalRuntimeReceipt
    {
        public string schemaVersion;
        public string generatedUtc;
        public string captureSessionId;
        public string scenePath;
        public string sceneSha256;
        public bool renderProducedByUnity;
        public int automaticVisualPoints;
        public string visualFidelityStatus;
    }

    [Serializable]
    private sealed class CandidateSeal
    {
        public string schemaVersion;
        public string captureSessionId;
        public string candidateEpochUtc;
        public string sceneAssetPath;
        public string sceneGuid;
        public string sceneSha256;
        public string candidateCombinedSha256;
        public string latestInputWriteUtc;
    }

    [Serializable]
    private sealed class CandidateEpoch
    {
        public string schemaVersion;
        public string lastCandidateMutationUtc;
        public string candidateCombinedSha256;
        public string sceneAssetPath;
        public string sceneGuid;
        public bool sceneDirtyObserved;
    }

    [Serializable]
    private sealed class CoherenceSeal
    {
        public string schemaVersion;
        public string sealedUtc;
        public string temporalCaptureSessionId;
        public string temporalRuntimeReceiptPath;
        public string temporalRuntimeReceiptSha256;
        public string temporalGeneratedUtc;
        public string candidateCombinedSha256;
        public string candidateEpochUtc;
        public string candidateLatestInputWriteUtc;
        public string sceneAssetPath;
        public string sceneGuid;
        public string sceneSha256;
        public int automaticVisualPoints;
        public string visualFidelityStatus;
        public string note;
    }

    [Serializable]
    private sealed class Invalidation
    {
        public string schemaVersion;
        public string invalidatedUtc;
        public string reason;
        public int automaticVisualPoints;
        public string visualFidelityStatus;
    }
}

/// <summary>
/// Seals temporal-candidate coherence when the authoritative runtime receipt is imported. A delayCall
/// avoids doing freshness/scene work inside the import callback itself. Any failure deletes the prior
/// coherence seal, so an old valid seal cannot survive a new unbound temporal receipt.
/// </summary>
public sealed class QualityBlockTemporalCandidateCoherencePostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        if (importedAssets == null || !importedAssets.Contains("Assets/QA/temporal_runtime_evidence_receipt.json", StringComparer.Ordinal))
            return;

        EditorApplication.delayCall += QualityBlockTemporalCandidateCoherenceQA.ProcessImportedTemporalRuntimeReceipt;
    }
}
