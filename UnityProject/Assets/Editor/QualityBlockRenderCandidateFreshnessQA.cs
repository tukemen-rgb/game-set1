using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Fail-closed freshness binding between a sealed native-4K render receipt and the exact persisted
/// Unity candidate that produced it. Existing render provenance proves image/crop bytes, but byte-identical
/// old images must not be re-sealed after the scene, its recursive dependencies, project render settings,
/// or the formal capture/tonemap recipe changes.
///
/// Enforcement deliberately reuses the existing provenance invariant: a valid render receipt MUST have
/// renderProducedByUnity=true. If the candidate becomes dirty/stale, this guard flips that flag to false.
/// Therefore the existing QualityBlockRenderEvidenceProvenanceQA cannot validate or regenerate a bound
/// review template from stale pixels, even though this class itself awards zero Visual Fidelity points.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockRenderCandidateFreshnessQA
{
    internal const string ReceiptPath = "Assets/QA/render_capture_receipt.json";
    private const string ManifestPath = "Assets/QA/4k_capture_manifest.json";
    private const string ContractPath = "Assets/QA/render_candidate_freshness_contract.json";
    private const string EpochPath = "Assets/QA/render_candidate_epoch.json";
    private const string SealPath = "Assets/QA/render_candidate_seal.json";
    private const string InvalidationPath = "Assets/QA/render_candidate_invalidation.json";

    private static readonly string[] ProjectRenderSettingPaths =
    {
        "ProjectSettings/ProjectSettings.asset",
        "ProjectSettings/QualitySettings.asset",
        "ProjectSettings/GraphicsSettings.asset"
    };

    private static readonly string[] FormalRenderRecipePaths =
    {
        "Assets/Editor/QualityBlock4KCapture.cs",
        "Assets/Editor/QualityBlockNative4KReviewPacket.cs",
        "Assets/Scripts/Art/QualityBlockFilmicTonemap.cs",
        "Assets/Shaders/NewTownFilmicTonemap.shader",
        "Assets/Editor/QualityBlockEnvironmentLightingUpgrade.cs",
        "Assets/Scripts/Art/QualityBlockEnvironmentContext.cs"
    };

    private static double _nextPollTime;
    private static bool _forceCheck = true;
    private static bool _checking;

    static QualityBlockRenderCandidateFreshnessQA()
    {
        EditorApplication.update += Poll;
        EditorApplication.projectChanged += RequestImmediateCheck;
        EditorApplication.hierarchyChanged += RequestImmediateCheck;
        EditorApplication.delayCall += BootstrapAfterDomainReload;
    }

    [MenuItem("NewTown/QA/Validate Render Candidate Freshness")]
    public static void ValidateCurrentCandidateFreshness()
    {
        ValidateContractConfigOnly();
        EnsureEpochCurrent(out CandidateFingerprint current, out CandidateEpoch epoch, out bool sceneDirty);
        Require(!sceneDirty,
            "The active benchmark scene has unsaved changes. Existing render evidence is stale until the scene is saved and native 4K evidence is recaptured.");

        CaptureReceiptHeader receipt = LoadJson<CaptureReceiptHeader>(ReceiptPath);
        Require(receipt != null && receipt.renderProducedByUnity,
            "Render receipt is missing or already invalidated; no current-candidate render evidence is scoreable.");
        Require(!string.IsNullOrWhiteSpace(receipt.captureSessionId),
            "Render receipt has no captureSessionId.");

        CandidateSeal seal = LoadJson<CandidateSeal>(SealPath);
        Require(seal != null, "Render candidate seal is missing. Recapture native 4K evidence.");
        Require(string.Equals(seal.captureSessionId, receipt.captureSessionId, StringComparison.Ordinal),
            "Render candidate seal belongs to a different capture session.");
        Require(string.Equals(seal.captureReceiptSha256, Sha256ProjectFile(ReceiptPath), StringComparison.OrdinalIgnoreCase),
            "Render receipt bytes changed after the candidate seal was created.");
        Require(string.Equals(seal.candidateCombinedSha256, current.combinedSha256, StringComparison.OrdinalIgnoreCase),
            "Persisted scene/dependencies/render recipe no longer match the candidate sealed with the rendered pixels.");
        Require(string.Equals(seal.sceneAssetPath, current.sceneAssetPath, StringComparison.Ordinal) &&
                string.Equals(seal.sceneGuid, current.sceneGuid, StringComparison.Ordinal),
            "The active benchmark scene identity differs from the scene sealed with the rendered pixels.");

        CaptureManifestHeader manifest = LoadJson<CaptureManifestHeader>(ManifestPath);
        Require(manifest != null && manifest.renderProducedByUnity,
            "Runtime 4K manifest is missing or is still a non-runtime template.");
        Require(string.Equals(receipt.manifestSha256, Sha256ProjectFile(ManifestPath), StringComparison.OrdinalIgnoreCase),
            "Current 4K manifest bytes differ from the render receipt.");
        Require(string.Equals(seal.captureManifestSha256, receipt.manifestSha256, StringComparison.OrdinalIgnoreCase),
            "Candidate seal manifest hash differs from the render receipt.");

        DateTime manifestUtc = ParseUtc(manifest.generatedUtc, "4K manifest generatedUtc");
        DateTime mutationUtc = ParseUtc(epoch.lastCandidateMutationUtc, "candidate epoch lastCandidateMutationUtc");
        DateTime latestInputUtc = ParseUtc(current.latestInputWriteUtc, "candidate latestInputWriteUtc");
        Require(manifestUtc >= mutationUtc && manifestUtc >= latestInputUtc,
            "Native 4K frames predate the latest tracked visual-candidate mutation/input write. Recapture; re-sealing old PNGs is forbidden.");

        Debug.Log(
            $"Render candidate freshness VALID: session={receipt.captureSessionId}, scene={current.sceneAssetPath}, " +
            $"candidateSHA256={current.combinedSha256}. This QA awards 0 Visual Fidelity points.");
    }

    [MenuItem("NewTown/QA/Validate Render Candidate Freshness Contract")]
    public static void ValidateContractConfigOnly()
    {
        FreshnessContract contract = LoadJson<FreshnessContract>(ContractPath);
        Require(contract != null, "Render-candidate freshness contract could not be parsed.");
        Require(string.Equals(contract.schemaVersion, "1.0", StringComparison.Ordinal),
            $"Unexpected render-candidate freshness schemaVersion '{contract.schemaVersion}'. Expected 1.0.");
        Require(!contract.visualScoreAwardedByThisQA,
            "Render-candidate freshness QA may not award Visual Fidelity points.");
        Require(contract.rules != null &&
                contract.rules.invalidateReceiptWhenCandidateChanges &&
                contract.rules.invalidateReceiptWhenSceneIsDirty &&
                contract.rules.forbidResealingFramesOlderThanLatestCandidateMutation &&
                contract.rules.requireSceneByteHash &&
                contract.rules.requireRecursiveAssetDependencyHash &&
                contract.rules.requireProjectRenderSettingsHashes &&
                contract.rules.requireFormalRenderRecipeHashes &&
                contract.rules.requireCaptureSessionBinding &&
                contract.rules.requireReceiptByteHashBinding &&
                contract.rules.reuseExistingRenderProducedByUnityFailClosedGate,
            "Render-candidate freshness contract rules were weakened or are incomplete.");
        RequireExactSet(contract.projectRenderSettingPaths, ProjectRenderSettingPaths,
            "render-candidate projectRenderSettingPaths");
        RequireExactSet(contract.formalRenderRecipePaths, FormalRenderRecipePaths,
            "render-candidate formalRenderRecipePaths");
    }

    internal static void ProcessImportedRenderReceipt()
    {
        try
        {
            ValidateContractConfigOnly();
            if (!File.Exists(AbsoluteProjectPath(ReceiptPath)))
                return;

            CaptureReceiptHeader receipt = LoadJson<CaptureReceiptHeader>(ReceiptPath);
            if (receipt == null || !receipt.renderProducedByUnity)
                return;

            EnsureEpochCurrent(out CandidateFingerprint current, out CandidateEpoch epoch, out bool sceneDirty);
            if (sceneDirty)
            {
                InvalidateReceipt("active_scene_dirty_during_receipt_seal", current, epoch);
                return;
            }

            CaptureManifestHeader manifest = LoadJson<CaptureManifestHeader>(ManifestPath);
            if (manifest == null || !manifest.renderProducedByUnity)
            {
                InvalidateReceipt("runtime_manifest_missing_or_not_unity_render", current, epoch);
                return;
            }

            string manifestSha = Sha256ProjectFile(ManifestPath);
            if (string.IsNullOrWhiteSpace(receipt.manifestSha256) ||
                !string.Equals(receipt.manifestSha256, manifestSha, StringComparison.OrdinalIgnoreCase))
            {
                InvalidateReceipt("receipt_manifest_hash_mismatch", current, epoch);
                return;
            }

            DateTime manifestUtc = ParseUtc(manifest.generatedUtc, "4K manifest generatedUtc");
            DateTime mutationUtc = ParseUtc(epoch.lastCandidateMutationUtc, "candidate epoch lastCandidateMutationUtc");
            DateTime latestInputUtc = ParseUtc(current.latestInputWriteUtc, "candidate latestInputWriteUtc");
            if (manifestUtc < mutationUtc || manifestUtc < latestInputUtc)
            {
                InvalidateReceipt("capture_predates_candidate_mutation", current, epoch);
                return;
            }

            var seal = new CandidateSeal
            {
                schemaVersion = "1.0",
                sealedUtc = DateTime.UtcNow.ToString("O"),
                captureSessionId = receipt.captureSessionId,
                captureReceiptSha256 = Sha256ProjectFile(ReceiptPath),
                captureManifestSha256 = manifestSha,
                manifestGeneratedUtc = manifest.generatedUtc,
                candidateEpochUtc = epoch.lastCandidateMutationUtc,
                sceneAssetPath = current.sceneAssetPath,
                sceneGuid = current.sceneGuid,
                sceneSha256 = current.sceneSha256,
                recursiveDependencyHash = current.recursiveDependencyHash,
                candidateCombinedSha256 = current.combinedSha256,
                latestInputWriteUtc = current.latestInputWriteUtc,
                visualFidelityPointsAwarded = 0,
                visualFidelityStatus = "UNSCORED_REVIEW_REQUIRED",
                note = "Candidate freshness seal only. It binds existing render provenance to current persisted scene/dependencies/settings/recipe and awards zero visual points."
            };
            WriteJsonWithoutRefresh(SealPath, seal);
            DeleteIfExists(InvalidationPath);
            Debug.Log(
                $"Render candidate sealed for session={receipt.captureSessionId}, candidateSHA256={current.combinedSha256}. Visual Fidelity remains UNSCORED.");
        }
        catch (Exception ex)
        {
            Debug.LogError("Render-candidate receipt sealing failed closed: " + ex.Message);
            TryInvalidateWithoutFingerprint("candidate_seal_exception: " + ex.Message);
        }
    }

    internal static void RequestImmediateCheck()
    {
        _forceCheck = true;
    }

    private static void BootstrapAfterDomainReload()
    {
        try
        {
            ValidateContractConfigOnly();
            EnsureEpochCurrent(out _, out _, out _);
            CheckLiveReceiptFreshness();
        }
        catch (Exception ex)
        {
            Debug.LogError("Render-candidate freshness bootstrap failed closed: " + ex.Message);
            TryInvalidateWithoutFingerprint("freshness_bootstrap_exception: " + ex.Message);
        }
    }

    private static void Poll()
    {
        if (_checking || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;
        if (!_forceCheck && EditorApplication.timeSinceStartup < _nextPollTime)
            return;

        _forceCheck = false;
        _nextPollTime = EditorApplication.timeSinceStartup + 5.0;
        _checking = true;
        try
        {
            CheckLiveReceiptFreshness();
        }
        catch (Exception ex)
        {
            Debug.LogError("Render-candidate freshness poll failed closed: " + ex.Message);
            TryInvalidateWithoutFingerprint("freshness_poll_exception: " + ex.Message);
        }
        finally
        {
            _checking = false;
        }
    }

    private static void CheckLiveReceiptFreshness()
    {
        string receiptAbsolute = AbsoluteProjectPath(ReceiptPath);
        if (!File.Exists(receiptAbsolute))
        {
            EnsureEpochCurrent(out _, out _, out _);
            return;
        }

        CaptureReceiptHeader receipt = LoadJson<CaptureReceiptHeader>(ReceiptPath);
        if (receipt == null || !receipt.renderProducedByUnity)
        {
            EnsureEpochCurrent(out _, out _, out _);
            return;
        }

        EnsureEpochCurrent(out CandidateFingerprint current, out CandidateEpoch epoch, out bool sceneDirty);
        if (sceneDirty)
        {
            InvalidateReceipt("active_scene_dirty_after_capture", current, epoch);
            return;
        }

        CandidateSeal seal = LoadJsonOrNull<CandidateSeal>(SealPath);
        if (seal == null)
        {
            InvalidateReceipt("candidate_seal_missing", current, epoch);
            return;
        }

        string receiptSha = Sha256ProjectFile(ReceiptPath);
        string manifestSha = File.Exists(AbsoluteProjectPath(ManifestPath))
            ? Sha256ProjectFile(ManifestPath)
            : string.Empty;
        CaptureManifestHeader manifest = LoadJsonOrNull<CaptureManifestHeader>(ManifestPath);

        if (!string.Equals(seal.captureSessionId, receipt.captureSessionId, StringComparison.Ordinal) ||
            !string.Equals(seal.captureReceiptSha256, receiptSha, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(seal.captureManifestSha256, manifestSha, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(seal.candidateCombinedSha256, current.combinedSha256, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(seal.sceneAssetPath, current.sceneAssetPath, StringComparison.Ordinal) ||
            !string.Equals(seal.sceneGuid, current.sceneGuid, StringComparison.Ordinal))
        {
            InvalidateReceipt("candidate_or_seal_identity_changed", current, epoch);
            return;
        }

        if (manifest == null || !manifest.renderProducedByUnity)
        {
            InvalidateReceipt("runtime_manifest_missing_or_invalid", current, epoch);
            return;
        }

        DateTime manifestUtc = ParseUtc(manifest.generatedUtc, "4K manifest generatedUtc");
        DateTime mutationUtc = ParseUtc(epoch.lastCandidateMutationUtc, "candidate epoch lastCandidateMutationUtc");
        DateTime latestInputUtc = ParseUtc(current.latestInputWriteUtc, "candidate latestInputWriteUtc");
        if (manifestUtc < mutationUtc || manifestUtc < latestInputUtc)
            InvalidateReceipt("capture_older_than_current_candidate", current, epoch);
    }

    private static void EnsureEpochCurrent(out CandidateFingerprint current, out CandidateEpoch epoch, out bool sceneDirty)
    {
        current = BuildCurrentCandidateFingerprint(out sceneDirty);
        epoch = LoadJsonOrNull<CandidateEpoch>(EpochPath);

        if (epoch == null)
        {
            epoch = new CandidateEpoch
            {
                schemaVersion = "1.0",
                lastCandidateMutationUtc = DateTime.UnixEpoch.ToString("O"),
                candidateCombinedSha256 = current.combinedSha256,
                sceneAssetPath = current.sceneAssetPath,
                sceneGuid = current.sceneGuid,
                sceneDirtyObserved = sceneDirty,
                note = "Bootstrap epoch. First post-bootstrap candidate mutation advances lastCandidateMutationUtc."
            };
            WriteJsonWithoutRefresh(EpochPath, epoch);
        }

        if (sceneDirty)
        {
            if (!epoch.sceneDirtyObserved)
            {
                epoch.sceneDirtyObserved = true;
                epoch.lastCandidateMutationUtc = DateTime.UtcNow.ToString("O");
                WriteJsonWithoutRefresh(EpochPath, epoch);
            }
            return;
        }

        bool identityChanged =
            !string.Equals(epoch.candidateCombinedSha256, current.combinedSha256, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(epoch.sceneAssetPath, current.sceneAssetPath, StringComparison.Ordinal) ||
            !string.Equals(epoch.sceneGuid, current.sceneGuid, StringComparison.Ordinal);

        if (identityChanged || epoch.sceneDirtyObserved)
        {
            DateTime oldMutationUtc = ParseUtc(epoch.lastCandidateMutationUtc, "candidate epoch lastCandidateMutationUtc");
            DateTime latestInputUtc = ParseUtc(current.latestInputWriteUtc, "candidate latestInputWriteUtc");
            DateTime mutationUtc = latestInputUtc > oldMutationUtc ? latestInputUtc : oldMutationUtc;
            if (identityChanged && mutationUtc == oldMutationUtc)
                mutationUtc = DateTime.UtcNow;

            epoch.candidateCombinedSha256 = current.combinedSha256;
            epoch.sceneAssetPath = current.sceneAssetPath;
            epoch.sceneGuid = current.sceneGuid;
            epoch.sceneDirtyObserved = false;
            epoch.lastCandidateMutationUtc = mutationUtc.ToString("O");
            WriteJsonWithoutRefresh(EpochPath, epoch);
        }
    }

    private static CandidateFingerprint BuildCurrentCandidateFingerprint(out bool sceneDirty)
    {
        Scene scene = SceneManager.GetActiveScene();
        Require(scene.IsValid() && scene.isLoaded && !string.IsNullOrWhiteSpace(scene.path),
            "A persisted loaded benchmark scene must be active before candidate freshness can be evaluated.");
        sceneDirty = ReadSceneDirty(scene);

        string scenePath = scene.path.Replace('\\', '/');
        string sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);
        Require(!string.IsNullOrWhiteSpace(sceneGuid), $"Active scene has no AssetDatabase GUID: {scenePath}");
        Require(File.Exists(AbsoluteProjectPath(scenePath)), $"Persisted active scene file is missing: {scenePath}");

        string dependencyHash = AssetDatabase.GetAssetDependencyHash(scenePath).ToString().ToLowerInvariant();
        Require(IsHex(dependencyHash, 32),
            $"Active scene recursive dependency hash is invalid: '{dependencyHash}'.");

        var settings = ProjectRenderSettingPaths.Select(BuildFileHash).ToArray();
        var recipe = FormalRenderRecipePaths.Select(BuildFileHash).ToArray();

        string[] dependencies = AssetDatabase.GetDependencies(scenePath, true)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        DateTime latestUtc = DateTime.MinValue;
        foreach (string dependency in dependencies)
        {
            latestUtc = MaxUtc(latestUtc, FileWriteUtcOrMin(AbsoluteProjectPath(dependency)));
            latestUtc = MaxUtc(latestUtc, FileWriteUtcOrMin(AbsoluteProjectPath(dependency + ".meta")));
        }
        foreach (FileHash file in settings.Concat(recipe))
        {
            latestUtc = MaxUtc(latestUtc, FileWriteUtcOrMin(AbsoluteProjectPath(file.path)));
            latestUtc = MaxUtc(latestUtc, FileWriteUtcOrMin(AbsoluteProjectPath(file.path + ".meta")));
        }
        latestUtc = MaxUtc(latestUtc, FileWriteUtcOrMin(AbsoluteProjectPath(scenePath)));
        latestUtc = MaxUtc(latestUtc, FileWriteUtcOrMin(AbsoluteProjectPath(scenePath + ".meta")));
        Require(latestUtc != DateTime.MinValue, "Could not determine latest candidate input write time.");

        string sceneSha = Sha256ProjectFile(scenePath);
        var canonical = new StringBuilder();
        canonical.Append(scenePath).Append('|').Append(sceneGuid).Append('|')
            .Append(sceneSha).Append('|').Append(dependencyHash).Append('|');
        foreach (FileHash file in settings.OrderBy(x => x.path, StringComparer.Ordinal))
            canonical.Append(file.path).Append('=').Append(file.sha256).Append('|');
        foreach (FileHash file in recipe.OrderBy(x => x.path, StringComparer.Ordinal))
            canonical.Append(file.path).Append('=').Append(file.sha256).Append('|');

        return new CandidateFingerprint
        {
            schemaVersion = "1.0",
            sceneAssetPath = scenePath,
            sceneGuid = sceneGuid,
            sceneSha256 = sceneSha,
            recursiveDependencyHash = dependencyHash,
            recursiveDependencyCount = dependencies.Length,
            projectRenderSettings = settings,
            formalRenderRecipe = recipe,
            latestInputWriteUtc = latestUtc.ToUniversalTime().ToString("O"),
            combinedSha256 = Sha256Utf8(canonical.ToString())
        };
    }

    private static bool ReadSceneDirty(Scene scene)
    {
        var property = typeof(Scene).GetProperty("isDirty");
        Require(property != null && property.PropertyType == typeof(bool),
            "Unity Scene.isDirty API is unavailable; freshness guard cannot prove persisted-scene identity.");
        return (bool)property.GetValue(scene, null);
    }

    private static FileHash BuildFileHash(string projectRelativePath)
    {
        Require(File.Exists(AbsoluteProjectPath(projectRelativePath)),
            $"Tracked render-candidate input is missing: {projectRelativePath}");
        return new FileHash
        {
            path = projectRelativePath,
            sha256 = Sha256ProjectFile(projectRelativePath)
        };
    }

    private static void InvalidateReceipt(string reason, CandidateFingerprint current, CandidateEpoch epoch)
    {
        string receiptAbsolute = AbsoluteProjectPath(ReceiptPath);
        if (!File.Exists(receiptAbsolute))
            return;

        string raw = File.ReadAllText(receiptAbsolute);
        var pattern = new Regex("\\\"renderProducedByUnity\\\"\\s*:\\s*true", RegexOptions.CultureInvariant);
        if (!pattern.IsMatch(raw))
            return;

        raw = pattern.Replace(raw, "\"renderProducedByUnity\": false", 1);
        File.WriteAllText(receiptAbsolute, raw);

        var invalidation = new CandidateInvalidation
        {
            schemaVersion = "1.0",
            invalidatedUtc = DateTime.UtcNow.ToString("O"),
            reason = reason,
            candidateCombinedSha256 = current != null ? current.combinedSha256 : string.Empty,
            candidateEpochUtc = epoch != null ? epoch.lastCandidateMutationUtc : string.Empty,
            visualFidelityPointsAwarded = 0,
            effect = "render_capture_receipt.renderProducedByUnity=false; existing provenance and Visual Fidelity scoring must fail until native 4K evidence is recaptured and resealed."
        };
        WriteJsonWithoutRefresh(InvalidationPath, invalidation);
        DeleteIfExists(SealPath);
        Debug.LogError(
            $"Render evidence invalidated because the visual candidate is stale ({reason}). Native 4K recapture is required; no Visual Fidelity points were awarded.");
    }

    private static void TryInvalidateWithoutFingerprint(string reason)
    {
        try
        {
            string receiptAbsolute = AbsoluteProjectPath(ReceiptPath);
            if (!File.Exists(receiptAbsolute))
                return;
            string raw = File.ReadAllText(receiptAbsolute);
            var pattern = new Regex("\\\"renderProducedByUnity\\\"\\s*:\\s*true", RegexOptions.CultureInvariant);
            if (!pattern.IsMatch(raw))
                return;
            raw = pattern.Replace(raw, "\"renderProducedByUnity\": false", 1);
            File.WriteAllText(receiptAbsolute, raw);
            DeleteIfExists(SealPath);
            WriteJsonWithoutRefresh(InvalidationPath, new CandidateInvalidation
            {
                schemaVersion = "1.0",
                invalidatedUtc = DateTime.UtcNow.ToString("O"),
                reason = reason,
                candidateCombinedSha256 = string.Empty,
                candidateEpochUtc = string.Empty,
                visualFidelityPointsAwarded = 0,
                effect = "Fail-closed receipt invalidation after candidate-freshness exception. Recapture required."
            });
        }
        catch (Exception ex)
        {
            Debug.LogError("Could not invalidate stale render receipt after freshness failure: " + ex.Message);
        }
    }

    private static string Sha256ProjectFile(string projectRelativePath)
    {
        string absolute = AbsoluteProjectPath(projectRelativePath);
        Require(File.Exists(absolute), $"Cannot hash missing project file: {projectRelativePath}");
        using (SHA256 sha = SHA256.Create())
        using (FileStream stream = File.OpenRead(absolute))
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

    private static bool IsHex(string value, int expectedLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != expectedLength)
            return false;
        foreach (char c in value)
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                return false;
        return true;
    }

    private static DateTime ParseUtc(string value, string label)
    {
        Require(!string.IsNullOrWhiteSpace(value) &&
                DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime parsed),
            $"{label} is missing or invalid: '{value}'.");
        return parsed.ToUniversalTime();
    }

    private static DateTime FileWriteUtcOrMin(string absolutePath)
    {
        return File.Exists(absolutePath) ? File.GetLastWriteTimeUtc(absolutePath) : DateTime.MinValue;
    }

    private static DateTime MaxUtc(DateTime a, DateTime b)
    {
        return a >= b ? a : b;
    }

    private static string AbsoluteProjectPath(string projectRelativePath)
    {
        string root = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(root, projectRelativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static T LoadJson<T>(string projectRelativePath) where T : class
    {
        string absolute = AbsoluteProjectPath(projectRelativePath);
        if (!File.Exists(absolute))
            throw new FileNotFoundException($"Required QA file not found: {projectRelativePath}");
        T value = JsonUtility.FromJson<T>(File.ReadAllText(absolute));
        if (value == null)
            throw new InvalidOperationException($"Could not parse QA JSON: {projectRelativePath}");
        return value;
    }

    private static T LoadJsonOrNull<T>(string projectRelativePath) where T : class
    {
        string absolute = AbsoluteProjectPath(projectRelativePath);
        if (!File.Exists(absolute))
            return null;
        try
        {
            return JsonUtility.FromJson<T>(File.ReadAllText(absolute));
        }
        catch
        {
            return null;
        }
    }

    private static void WriteJsonWithoutRefresh<T>(string projectRelativePath, T value)
    {
        string absolute = AbsoluteProjectPath(projectRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute));
        File.WriteAllText(absolute, JsonUtility.ToJson(value, true));
    }

    private static void DeleteIfExists(string projectRelativePath)
    {
        string absolute = AbsoluteProjectPath(projectRelativePath);
        if (File.Exists(absolute))
            File.Delete(absolute);
    }

    private static void RequireExactSet(string[] actual, string[] expected, string label)
    {
        Require(actual != null && actual.All(x => !string.IsNullOrWhiteSpace(x)), $"{label} is missing/blank.");
        Require(actual.Distinct(StringComparer.Ordinal).Count() == actual.Length, $"{label} contains duplicates.");
        Require(new HashSet<string>(actual, StringComparer.Ordinal).SetEquals(expected),
            $"{label} differs from the fail-closed canonical set.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    [Serializable]
    private sealed class FreshnessContract
    {
        public string schemaVersion;
        public bool visualScoreAwardedByThisQA;
        public string[] projectRenderSettingPaths;
        public string[] formalRenderRecipePaths;
        public FreshnessRules rules;
    }

    [Serializable]
    private sealed class FreshnessRules
    {
        public bool invalidateReceiptWhenCandidateChanges;
        public bool invalidateReceiptWhenSceneIsDirty;
        public bool forbidResealingFramesOlderThanLatestCandidateMutation;
        public bool requireSceneByteHash;
        public bool requireRecursiveAssetDependencyHash;
        public bool requireProjectRenderSettingsHashes;
        public bool requireFormalRenderRecipeHashes;
        public bool requireCaptureSessionBinding;
        public bool requireReceiptByteHashBinding;
        public bool reuseExistingRenderProducedByUnityFailClosedGate;
    }

    [Serializable]
    private sealed class CaptureReceiptHeader
    {
        public string captureSessionId;
        public string manifestSha256;
        public bool renderProducedByUnity;
    }

    [Serializable]
    private sealed class CaptureManifestHeader
    {
        public string generatedUtc;
        public bool renderProducedByUnity;
    }

    [Serializable]
    private sealed class CandidateFingerprint
    {
        public string schemaVersion;
        public string sceneAssetPath;
        public string sceneGuid;
        public string sceneSha256;
        public string recursiveDependencyHash;
        public int recursiveDependencyCount;
        public FileHash[] projectRenderSettings;
        public FileHash[] formalRenderRecipe;
        public string latestInputWriteUtc;
        public string combinedSha256;
    }

    [Serializable]
    private sealed class FileHash
    {
        public string path;
        public string sha256;
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
        public string note;
    }

    [Serializable]
    private sealed class CandidateSeal
    {
        public string schemaVersion;
        public string sealedUtc;
        public string captureSessionId;
        public string captureReceiptSha256;
        public string captureManifestSha256;
        public string manifestGeneratedUtc;
        public string candidateEpochUtc;
        public string sceneAssetPath;
        public string sceneGuid;
        public string sceneSha256;
        public string recursiveDependencyHash;
        public string candidateCombinedSha256;
        public string latestInputWriteUtc;
        public int visualFidelityPointsAwarded;
        public string visualFidelityStatus;
        public string note;
    }

    [Serializable]
    private sealed class CandidateInvalidation
    {
        public string schemaVersion;
        public string invalidatedUtc;
        public string reason;
        public string candidateCombinedSha256;
        public string candidateEpochUtc;
        public int visualFidelityPointsAwarded;
        public string effect;
    }
}

/// <summary>
/// The existing receipt writer calls AssetDatabase.Refresh(), so this postprocessor seals candidate state
/// synchronously when a fresh render_capture_receipt.json is imported. It also requests a freshness check
/// after arbitrary project imports/moves/deletes; only tracked candidate-fingerprint changes invalidate.
/// </summary>
public sealed class QualityBlockRenderCandidateFreshnessPostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        bool receiptImported = (importedAssets ?? Array.Empty<string>())
            .Any(x => string.Equals(x, QualityBlockRenderCandidateFreshnessQA.ReceiptPath, StringComparison.Ordinal));

        QualityBlockRenderCandidateFreshnessQA.RequestImmediateCheck();
        if (receiptImported)
            QualityBlockRenderCandidateFreshnessQA.ProcessImportedRenderReceipt();
    }
}
