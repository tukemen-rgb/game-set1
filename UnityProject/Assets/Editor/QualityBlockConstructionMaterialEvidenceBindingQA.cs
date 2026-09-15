using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Cryptographically binds the persisted benchmark scene and every machine-readable construction/material
/// metadata contract to the exact Unity render-capture receipt. This closes the provenance gap where current
/// metadata could otherwise be edited after pixels were captured and then revalidated only at scoring time.
///
/// This QA awards zero Visual Fidelity points. It protects the admissibility of render evidence only.
/// If a bound source changes after capture, the authoritative render receipt is invalidated while the PNGs
/// are deliberately preserved for diagnosis. A fresh real Unity capture is then required before scoring.
/// </summary>
public static class QualityBlockConstructionMaterialEvidenceBindingQA
{
    public const string ContractPath = "Assets/QA/construction_material_evidence_binding_contract.json";
    public const string BindingReceiptPath = "Assets/QA/construction_material_capture_binding.json";
    public const string InvalidationReportPath = "Assets/QA/construction_material_capture_binding_invalidated.json";
    public const string RenderReceiptPath = "Assets/QA/render_capture_receipt.json";
    public const string CoverageContractPath = "Assets/QA/scene_metadata_coverage_contract.json";
    public const string MaterialRegistryPath = "Assets/QA/material_construction_lookdev.json";
    public const string ExpectedScenePath = "Assets/Scenes/QualityBlock1990s.unity";

    private const string SchemaVersion = "1.0.0";
    private const string CriticalDefectId = "missing_construction_material_metadata";
    private const double FreshReceiptAutoBindMinutes = 10.0;
    private const double SourceWriteClockToleranceSeconds = 2.0;
    private static bool invalidationScheduled;
    private static double nextPeriodicCheck;

    [InitializeOnLoadMethod]
    private static void InitializeMonitor()
    {
        EditorApplication.update -= PeriodicGuard;
        EditorApplication.update += PeriodicGuard;
    }

    [MenuItem("NewTown/QA/Seal Construction + Material Evidence Binding")]
    public static void SealCurrentCaptureBinding()
    {
        ValidateContractConfigOnly();
        Require(File.Exists(RenderReceiptPath),
            "Cannot bind construction/material metadata because no sealed Unity render receipt exists.");

        Scene active = SceneManager.GetActiveScene();
        Require(active.IsValid() && active.isLoaded,
            "Cannot bind construction/material evidence without a loaded benchmark scene.");
        Require(string.Equals(active.path, ExpectedScenePath, StringComparison.Ordinal),
            $"Construction/material evidence binding requires active scene '{ExpectedScenePath}', got '{active.path}'.");
        Require(!active.isDirty,
            "Benchmark scene is dirty. Save/reopen before capturing and binding evidence; unsaved runtime state is not admissible.");

        // Re-run the authoritative current-state coverage validator before hashing the persisted sources.
        // This validator is non-scoring and cannot clear any rendered defect.
        QualityBlockSceneMetadataCoverageQA.ValidateOpenScene();

        RenderReceiptRef renderReceipt = LoadJson<RenderReceiptRef>(RenderReceiptPath);
        ValidateRenderReceiptRef(renderReceipt);
        DateTime captureSealUtc = ParseUtcOrThrow(renderReceipt.sealedUtc, "render receipt sealedUtc");

        string[] sourcePaths = BuildRequiredSourcePaths();
        BoundFileProof[] proofs = sourcePaths.Select(path => BuildProof(path, captureSealUtc)).ToArray();
        string sourceBundleSha = ComputeBundleSha256(proofs);

        var receipt = new BindingReceipt
        {
            schemaVersion = SchemaVersion,
            captureSessionId = renderReceipt.captureSessionId,
            renderReceiptPath = RenderReceiptPath,
            renderReceiptSha256 = Sha256File(RenderReceiptPath),
            captureManifestSha256 = renderReceipt.manifestSha256,
            scenePath = ExpectedScenePath,
            sceneSha256 = ProofFor(proofs, ExpectedScenePath).sha256,
            sceneMetadataCoverageContractPath = CoverageContractPath,
            sceneMetadataCoverageContractSha256 = ProofFor(proofs, CoverageContractPath).sha256,
            materialRegistryPath = MaterialRegistryPath,
            materialRegistrySha256 = ProofFor(proofs, MaterialRegistryPath).sha256,
            metadataAndSceneBundleSha256 = sourceBundleSha,
            captureReceiptSealedUtc = renderReceipt.sealedUtc,
            bindingSealedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            criticalDefectId = CriticalDefectId,
            files = proofs,
            visualFidelityPointsAwardedAutomatically = false,
            renderVerificationClaimedByThisQA = false,
            note = "Exact persisted scene + construction/material metadata bytes bound to the same render capture session. This receipt awards 0 visual points."
        };

        WriteJson(BindingReceiptPath, receipt);
        DeleteIfExists(InvalidationReportPath);
        AssetDatabase.ImportAsset(BindingReceiptPath, ImportAssetOptions.ForceUpdate);
        ValidateLatestBinding();

        Debug.Log(
            $"Construction/material evidence binding sealed: session={receipt.captureSessionId}, " +
            $"bundleSHA256={receipt.metadataAndSceneBundleSha256}. Visual Fidelity remains unscored.");
    }

    [MenuItem("NewTown/QA/Validate Construction + Material Evidence Binding")]
    public static void ValidateLatestBinding()
    {
        ValidateContractConfigOnly();
        Require(File.Exists(RenderReceiptPath),
            "Authoritative render receipt is missing; construction/material evidence cannot be bound to a scoreable capture.");
        Require(File.Exists(BindingReceiptPath),
            "Construction/material capture binding is missing. Recapture or seal the fresh Unity capture before scoring.");

        RenderReceiptRef renderReceipt = LoadJson<RenderReceiptRef>(RenderReceiptPath);
        ValidateRenderReceiptRef(renderReceipt);
        BindingReceipt binding = LoadJson<BindingReceipt>(BindingReceiptPath);
        Require(binding != null, "Construction/material capture binding could not be parsed.");
        Require(string.Equals(binding.schemaVersion, SchemaVersion, StringComparison.Ordinal),
            $"Unexpected construction/material binding schemaVersion '{binding.schemaVersion}'. Expected '{SchemaVersion}'.");
        Require(!binding.visualFidelityPointsAwardedAutomatically,
            "Construction/material binding may not award Visual Fidelity points.");
        Require(!binding.renderVerificationClaimedByThisQA,
            "Construction/material binding is source provenance only and may not claim render verification.");
        Require(string.Equals(binding.criticalDefectId, CriticalDefectId, StringComparison.Ordinal),
            "Construction/material binding critical-defect identity was changed.");
        Require(string.Equals(binding.captureSessionId, renderReceipt.captureSessionId, StringComparison.Ordinal),
            "Construction/material binding belongs to a different render capture session.");
        Require(string.Equals(binding.renderReceiptPath, RenderReceiptPath, StringComparison.Ordinal),
            "Construction/material binding points at a non-canonical render receipt path.");
        Require(string.Equals(binding.renderReceiptSha256, Sha256File(RenderReceiptPath), StringComparison.OrdinalIgnoreCase),
            "Render capture receipt bytes changed after construction/material binding was sealed.");
        Require(string.Equals(binding.captureManifestSha256, renderReceipt.manifestSha256, StringComparison.OrdinalIgnoreCase),
            "Capture manifest identity differs between render receipt and construction/material binding.");
        Require(string.Equals(binding.scenePath, ExpectedScenePath, StringComparison.Ordinal),
            "Construction/material binding scene path is non-canonical.");
        Require(string.Equals(binding.sceneMetadataCoverageContractPath, CoverageContractPath, StringComparison.Ordinal),
            "Construction/material binding coverage-contract path is non-canonical.");
        Require(string.Equals(binding.materialRegistryPath, MaterialRegistryPath, StringComparison.Ordinal),
            "Construction/material binding material-registry path is non-canonical.");

        string[] requiredPaths = BuildRequiredSourcePaths();
        Require(binding.files != null && binding.files.Length == requiredPaths.Length,
            $"Construction/material binding file count mismatch: expected {requiredPaths.Length}, got {binding.files?.Length ?? 0}.");

        var boundByPath = new Dictionary<string, BoundFileProof>(StringComparer.Ordinal);
        foreach (BoundFileProof proof in binding.files)
        {
            Require(proof != null && !string.IsNullOrWhiteSpace(proof.assetPath),
                "Construction/material binding contains a null or blank file proof.");
            Require(boundByPath.TryAdd(proof.assetPath, proof),
                $"Construction/material binding contains duplicate source '{proof.assetPath}'.");
        }

        var currentProofs = new List<BoundFileProof>(requiredPaths.Length);
        foreach (string path in requiredPaths)
        {
            Require(boundByPath.TryGetValue(path, out BoundFileProof bound),
                $"Construction/material binding is missing required source '{path}'.");
            Require(File.Exists(path), $"Bound construction/material source no longer exists: {path}");
            string liveSha = Sha256File(path);
            Require(string.Equals(bound.sha256, liveSha, StringComparison.OrdinalIgnoreCase),
                $"Bound construction/material source changed after capture: {path}. Fresh Unity pixels are required.");
            currentProofs.Add(new BoundFileProof
            {
                assetPath = path,
                sha256 = liveSha,
                lastWriteUtc = File.GetLastWriteTimeUtc(path).ToString("O", CultureInfo.InvariantCulture)
            });
        }

        Require(boundByPath.Keys.All(path => requiredPaths.Contains(path, StringComparer.Ordinal)),
            "Construction/material binding contains sources no longer authorized by the current coverage contract.");

        string bundleSha = ComputeBundleSha256(currentProofs.ToArray());
        Require(string.Equals(binding.metadataAndSceneBundleSha256, bundleSha, StringComparison.OrdinalIgnoreCase),
            "Construction/material scene+metadata bundle SHA-256 changed after capture.");
        Require(string.Equals(binding.sceneSha256, Sha256File(ExpectedScenePath), StringComparison.OrdinalIgnoreCase),
            "Persisted benchmark scene bytes changed after capture.");
        Require(string.Equals(binding.sceneMetadataCoverageContractSha256, Sha256File(CoverageContractPath), StringComparison.OrdinalIgnoreCase),
            "Scene metadata coverage contract changed after capture.");
        Require(string.Equals(binding.materialRegistrySha256, Sha256File(MaterialRegistryPath), StringComparison.OrdinalIgnoreCase),
            "Material construction registry changed after capture.");
    }

    public static void ValidateContractConfigOnly()
    {
        Require(File.Exists(ContractPath), $"Construction/material evidence binding contract missing: {ContractPath}");
        BindingContract contract = LoadJson<BindingContract>(ContractPath);
        Require(contract != null, "Construction/material evidence binding contract could not be parsed.");
        Require(string.Equals(contract.schemaVersion, SchemaVersion, StringComparison.Ordinal),
            $"Unexpected evidence-binding schemaVersion '{contract.schemaVersion}'. Expected '{SchemaVersion}'.");
        Require(string.Equals(contract.criticalDefectId, CriticalDefectId, StringComparison.Ordinal),
            "Evidence-binding contract criticalDefectId was weakened or changed.");
        Require(string.Equals(contract.expectedScenePath, ExpectedScenePath, StringComparison.Ordinal),
            "Evidence-binding contract expectedScenePath is non-canonical.");
        Require(string.Equals(contract.renderReceiptPath, RenderReceiptPath, StringComparison.Ordinal),
            "Evidence-binding contract renderReceiptPath is non-canonical.");
        Require(string.Equals(contract.sceneMetadataCoverageContractPath, CoverageContractPath, StringComparison.Ordinal),
            "Evidence-binding contract coverage path is non-canonical.");
        Require(string.Equals(contract.materialRegistryPath, MaterialRegistryPath, StringComparison.Ordinal),
            "Evidence-binding contract material registry path is non-canonical.");
        Require(contract.requireExactRenderReceiptSha256 &&
                contract.requireCaptureSessionIdentity &&
                contract.requireExactPersistedSceneSha256 &&
                contract.requireAllCoverageContractSha256 &&
                contract.rejectSourceWritesAfterCaptureSeal &&
                contract.autoInvalidateRenderReceiptOnBoundSourceMutation &&
                contract.preserveRenderedPngsOnInvalidation &&
                contract.failClosedWithoutBinding,
            "Evidence-binding contract rules were weakened or are incomplete.");
        Require(!contract.visualFidelityPointsAwardedAutomatically,
            "Evidence-binding contract may not auto-award Visual Fidelity points.");

        CoverageContract coverage = LoadJson<CoverageContract>(CoverageContractPath);
        Require(coverage != null, "Scene metadata coverage contract could not be parsed by evidence binding QA.");
        Require(string.Equals(coverage.scenePath, ExpectedScenePath, StringComparison.Ordinal),
            $"Scene metadata coverage contract targets '{coverage.scenePath}', expected '{ExpectedScenePath}'.");
        Require(coverage.criticalDefectBinding != null &&
                string.Equals(coverage.criticalDefectBinding.defectId, CriticalDefectId, StringComparison.Ordinal) &&
                string.Equals(coverage.criticalDefectBinding.materialRegistryPath, MaterialRegistryPath, StringComparison.Ordinal),
            "Scene metadata coverage critical-defect/material-registry binding does not match the evidence binding contract.");

        string[] sources = BuildRequiredSourcePaths(coverage);
        Require(sources.Length >= 3,
            "Evidence-binding source bundle is unexpectedly empty.");
        foreach (string path in sources)
            Require(File.Exists(path), $"Required construction/material binding source missing: {path}");
    }

    internal static void NotifyAssetsChanged(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        if (!File.Exists(RenderReceiptPath))
            return;

        string[] protectedPaths;
        try
        {
            protectedPaths = BuildProtectedPathsBestEffort();
        }
        catch
        {
            ScheduleInvalidation("Construction/material source set could not be resolved while a render receipt existed.");
            return;
        }

        var changed = new HashSet<string>(StringComparer.Ordinal);
        AddAll(changed, importedAssets);
        AddAll(changed, deletedAssets);
        AddAll(changed, movedAssets);
        AddAll(changed, movedFromAssetPaths);

        string mutated = changed.FirstOrDefault(path => protectedPaths.Contains(path, StringComparer.Ordinal));
        if (!string.IsNullOrEmpty(mutated))
        {
            ScheduleInvalidation(
                $"Bound construction/material source '{mutated}' changed after the render receipt existed.");
            return;
        }

        if (changed.Contains(RenderReceiptPath))
            EditorApplication.delayCall += TryAutoBindFreshReceipt;
    }

    private static void PeriodicGuard()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;
        if (EditorApplication.timeSinceStartup < nextPeriodicCheck)
            return;
        nextPeriodicCheck = EditorApplication.timeSinceStartup + 5.0;

        if (!File.Exists(RenderReceiptPath))
            return;

        if (File.Exists(BindingReceiptPath))
        {
            try
            {
                ValidateLatestBinding();
            }
            catch (Exception ex)
            {
                ScheduleInvalidation("Bound capture provenance became stale: " + ex.Message);
            }
            return;
        }

        TryAutoBindFreshReceipt();
    }

    private static void TryAutoBindFreshReceipt()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(RenderReceiptPath))
            return;
        if (File.Exists(BindingReceiptPath))
            return;

        try
        {
            RenderReceiptRef renderReceipt = LoadJson<RenderReceiptRef>(RenderReceiptPath);
            ValidateRenderReceiptRef(renderReceipt);
            DateTime sealedUtc = ParseUtcOrThrow(renderReceipt.sealedUtc, "render receipt sealedUtc");
            if ((DateTime.UtcNow - sealedUtc).TotalMinutes > FreshReceiptAutoBindMinutes)
            {
                ScheduleInvalidation(
                    "Render receipt has no construction/material binding and is no longer fresh enough for automatic binding.");
                return;
            }

            Scene active = SceneManager.GetActiveScene();
            if (!active.IsValid() || !active.isLoaded ||
                !string.Equals(active.path, ExpectedScenePath, StringComparison.Ordinal) || active.isDirty)
            {
                // Keep the short fresh window open so the formal review packet can finish save/reopen/import work.
                return;
            }

            SealCurrentCaptureBinding();
        }
        catch (Exception ex)
        {
            ScheduleInvalidation("Fresh render receipt could not be safely bound: " + ex.Message);
        }
    }

    private static void ScheduleInvalidation(string reason)
    {
        if (invalidationScheduled || !File.Exists(RenderReceiptPath))
            return;
        invalidationScheduled = true;
        EditorApplication.delayCall += () =>
        {
            try
            {
                InvalidateRenderReceipt(reason);
            }
            finally
            {
                invalidationScheduled = false;
            }
        };
    }

    private static void InvalidateRenderReceipt(string reason)
    {
        if (!File.Exists(RenderReceiptPath))
            return;

        RenderReceiptRef renderReceipt = null;
        try { renderReceipt = LoadJson<RenderReceiptRef>(RenderReceiptPath); }
        catch { }

        var report = new InvalidationReport
        {
            schemaVersion = SchemaVersion,
            invalidatedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            captureSessionId = renderReceipt == null ? string.Empty : renderReceipt.captureSessionId,
            reason = reason,
            renderReceiptRemoved = true,
            renderedPngsPreserved = true,
            visualFidelityPointsAwardedAutomatically = false,
            correctiveAction = "Run the formal Unity benchmark again, then seal/review the new native 3840x2160 evidence."
        };
        WriteJson(InvalidationReportPath, report);

        DeleteIfExists(BindingReceiptPath);
        if (!AssetDatabase.DeleteAsset(RenderReceiptPath))
        {
            File.Delete(RenderReceiptPath);
            if (File.Exists(RenderReceiptPath + ".meta"))
                File.Delete(RenderReceiptPath + ".meta");
        }
        AssetDatabase.Refresh();

        Debug.LogWarning(
            "Construction/material capture binding invalidated the authoritative render receipt. " +
            reason + " Existing PNGs were preserved for diagnosis; they are not scoreable until recaptured.");
    }

    private static string[] BuildProtectedPathsBestEffort()
    {
        var paths = new HashSet<string>(StringComparer.Ordinal)
        {
            ExpectedScenePath,
            CoverageContractPath,
            MaterialRegistryPath
        };

        if (File.Exists(CoverageContractPath))
        {
            CoverageContract coverage = LoadJson<CoverageContract>(CoverageContractPath);
            foreach (string path in BuildRequiredSourcePaths(coverage))
                paths.Add(path);
        }

        if (File.Exists(BindingReceiptPath))
        {
            BindingReceipt binding = LoadJson<BindingReceipt>(BindingReceiptPath);
            if (binding != null && binding.files != null)
                foreach (BoundFileProof proof in binding.files)
                    if (proof != null && !string.IsNullOrWhiteSpace(proof.assetPath))
                        paths.Add(proof.assetPath);
        }

        return paths.OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }

    private static string[] BuildRequiredSourcePaths()
    {
        return BuildRequiredSourcePaths(LoadJson<CoverageContract>(CoverageContractPath));
    }

    private static string[] BuildRequiredSourcePaths(CoverageContract coverage)
    {
        Require(coverage != null, "Scene metadata coverage contract is unavailable.");
        var paths = new HashSet<string>(StringComparer.Ordinal)
        {
            ExpectedScenePath,
            CoverageContractPath,
            MaterialRegistryPath
        };

        if (coverage.domains != null)
        {
            foreach (CoverageDomain domain in coverage.domains)
            {
                if (domain == null || domain.contractPaths == null)
                    continue;
                foreach (string path in domain.contractPaths)
                {
                    if (string.IsNullOrWhiteSpace(path))
                        continue;
                    Require(IsSafeProjectAssetPath(path),
                        $"Unsafe construction/material contract path in scene coverage: '{path}'.");
                    paths.Add(path);
                }
            }
        }

        return paths.OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }

    private static BoundFileProof BuildProof(string assetPath, DateTime captureSealUtc)
    {
        Require(IsSafeProjectAssetPath(assetPath), $"Unsafe bound asset path '{assetPath}'.");
        Require(File.Exists(assetPath), $"Required bound source missing: {assetPath}");
        DateTime writeUtc = File.GetLastWriteTimeUtc(assetPath);
        Require(writeUtc <= captureSealUtc.AddSeconds(SourceWriteClockToleranceSeconds),
            $"Source '{assetPath}' was written at {writeUtc:O}, after render receipt seal {captureSealUtc:O}. " +
            "Metadata edited after capture cannot be retroactively bound to old pixels.");
        return new BoundFileProof
        {
            assetPath = assetPath,
            sha256 = Sha256File(assetPath),
            lastWriteUtc = writeUtc.ToString("O", CultureInfo.InvariantCulture)
        };
    }

    private static BoundFileProof ProofFor(BoundFileProof[] proofs, string path)
    {
        BoundFileProof proof = proofs.SingleOrDefault(x => string.Equals(x.assetPath, path, StringComparison.Ordinal));
        Require(proof != null, $"Internal evidence-binding proof missing: {path}");
        return proof;
    }

    private static string ComputeBundleSha256(BoundFileProof[] proofs)
    {
        var canonical = new StringBuilder();
        foreach (BoundFileProof proof in proofs.OrderBy(x => x.assetPath, StringComparer.Ordinal))
            canonical.Append(proof.assetPath).Append('\n').Append(proof.sha256.ToLowerInvariant()).Append('\n');
        using (SHA256 sha = SHA256.Create())
            return ToHex(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static void ValidateRenderReceiptRef(RenderReceiptRef receipt)
    {
        Require(receipt != null, "Render capture receipt could not be parsed.");
        Require(!string.IsNullOrWhiteSpace(receipt.captureSessionId),
            "Render capture receipt is missing captureSessionId.");
        Require(IsSha256(receipt.manifestSha256),
            "Render capture receipt is missing a valid manifestSha256.");
        Require(receipt.renderProducedByUnity,
            "Render capture receipt does not prove renderProducedByUnity=true.");
        Require(string.Equals(receipt.visualFidelityStatus, "UNSCORED_REVIEW_REQUIRED", StringComparison.Ordinal),
            "Construction/material binding only accepts an unscored Unity render receipt.");
        ParseUtcOrThrow(receipt.sealedUtc, "render receipt sealedUtc");
    }

    private static DateTime ParseUtcOrThrow(string value, string label)
    {
        Require(DateTime.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime parsed),
            $"{label} is missing or invalid: '{value}'.");
        return parsed.ToUniversalTime();
    }

    private static bool IsSafeProjectAssetPath(string path)
    {
        return !string.IsNullOrWhiteSpace(path) &&
               path.StartsWith("Assets/", StringComparison.Ordinal) &&
               path.IndexOf("..", StringComparison.Ordinal) < 0 &&
               !Path.IsPathRooted(path);
    }

    private static string Sha256File(string path)
    {
        Require(File.Exists(path), $"Cannot hash missing file: {path}");
        using (SHA256 sha = SHA256.Create())
        using (FileStream stream = File.OpenRead(path))
            return ToHex(sha.ComputeHash(stream));
    }

    private static string ToHex(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (byte b in bytes)
            sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        return sb.ToString();
    }

    private static bool IsSha256(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 64)
            return false;
        foreach (char c in value)
            if (!Uri.IsHexDigit(c))
                return false;
        return true;
    }

    private static T LoadJson<T>(string path)
    {
        Require(File.Exists(path), $"Required JSON missing: {path}");
        T value = JsonUtility.FromJson<T>(File.ReadAllText(path));
        Require(value != null, $"JSON could not be parsed: {path}");
        return value;
    }

    private static void WriteJson<T>(string path, T value)
    {
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(path, JsonUtility.ToJson(value, true) + "\n", new UTF8Encoding(false));
    }

    private static void DeleteIfExists(string path)
    {
        if (!File.Exists(path))
            return;
        if (!AssetDatabase.DeleteAsset(path))
        {
            File.Delete(path);
            if (File.Exists(path + ".meta"))
                File.Delete(path + ".meta");
        }
    }

    private static void AddAll(HashSet<string> set, string[] values)
    {
        if (values == null)
            return;
        foreach (string value in values)
            if (!string.IsNullOrWhiteSpace(value))
                set.Add(value);
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
        public string criticalDefectId;
        public string expectedScenePath;
        public string renderReceiptPath;
        public string sceneMetadataCoverageContractPath;
        public string materialRegistryPath;
        public bool requireExactRenderReceiptSha256;
        public bool requireCaptureSessionIdentity;
        public bool requireExactPersistedSceneSha256;
        public bool requireAllCoverageContractSha256;
        public bool rejectSourceWritesAfterCaptureSeal;
        public bool autoInvalidateRenderReceiptOnBoundSourceMutation;
        public bool preserveRenderedPngsOnInvalidation;
        public bool failClosedWithoutBinding;
        public bool visualFidelityPointsAwardedAutomatically;
    }

    [Serializable]
    private sealed class CoverageContract
    {
        public string scenePath;
        public CoverageCriticalDefectBinding criticalDefectBinding;
        public CoverageDomain[] domains;
    }

    [Serializable]
    private sealed class CoverageCriticalDefectBinding
    {
        public string defectId;
        public string materialRegistryPath;
    }

    [Serializable]
    private sealed class CoverageDomain
    {
        public string id;
        public string[] contractPaths;
    }

    [Serializable]
    private sealed class RenderReceiptRef
    {
        public string schemaVersion;
        public string captureSessionId;
        public string sealedUtc;
        public string manifestSha256;
        public bool renderProducedByUnity;
        public string visualFidelityStatus;
    }

    [Serializable]
    private sealed class BindingReceipt
    {
        public string schemaVersion;
        public string captureSessionId;
        public string renderReceiptPath;
        public string renderReceiptSha256;
        public string captureManifestSha256;
        public string scenePath;
        public string sceneSha256;
        public string sceneMetadataCoverageContractPath;
        public string sceneMetadataCoverageContractSha256;
        public string materialRegistryPath;
        public string materialRegistrySha256;
        public string metadataAndSceneBundleSha256;
        public string captureReceiptSealedUtc;
        public string bindingSealedUtc;
        public string criticalDefectId;
        public BoundFileProof[] files;
        public bool visualFidelityPointsAwardedAutomatically;
        public bool renderVerificationClaimedByThisQA;
        public string note;
    }

    [Serializable]
    private sealed class BoundFileProof
    {
        public string assetPath;
        public string sha256;
        public string lastWriteUtc;
    }

    [Serializable]
    private sealed class InvalidationReport
    {
        public string schemaVersion;
        public string invalidatedUtc;
        public string captureSessionId;
        public string reason;
        public bool renderReceiptRemoved;
        public bool renderedPngsPreserved;
        public bool visualFidelityPointsAwardedAutomatically;
        public string correctiveAction;
    }
}

/// <summary>
/// Asset import boundary for the construction/material capture binding. Any mutation to a source that was
/// part of the captured metadata/scene bundle invalidates the authoritative render receipt fail-closed.
/// </summary>
internal sealed class QualityBlockConstructionMaterialEvidenceBindingPostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        QualityBlockConstructionMaterialEvidenceBindingQA.NotifyAssetsChanged(
            importedAssets, deletedAssets, movedAssets, movedFromAssetPaths);
    }
}
