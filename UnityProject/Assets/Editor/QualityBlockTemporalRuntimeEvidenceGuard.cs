using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Runtime evidence guard for the authoritative prepared temporal sequence.
/// It proves two things that static LOD/capture contracts cannot prove:
/// 1) every temporal MainCamera render reached the same physical lighting state immediately before culling;
/// 2) every temporal native-4K render traversed the filmic HDR-source -> LDR-destination image effect with zero fallback.
/// The resulting receipt is evidence provenance only and awards zero Visual Fidelity points.
/// </summary>
public static class QualityBlockTemporalRuntimeEvidenceGuard
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/temporal_runtime_evidence_contract.json";
    private const string TemporalContractPath = "Assets/QA/temporal_stability_contract.json";
    private const string TemporalManifestPath = "Assets/QA/temporal_stability_manifest.json";
    private const string TemporalReceiptPath = "Assets/QA/temporal_stability_receipt.json";
    private const string PreparedBindingPath = "Assets/QA/temporal_prepared_scene_binding.json";
    private const string ReflectionReceiptPath = "Assets/QA/reflection_probe_refresh_receipt.json";
    private const string ReflectionWaitProofPath = "Assets/QA/reflection_probe_async_wait_receipt.json";
    private const string RuntimeReceiptPath = "Assets/QA/temporal_runtime_evidence_receipt.json";
    private const int Width = 3840;
    private const int Height = 2160;

    private static bool running;
    private static Camera guardedCamera;
    private static QualityBlockFilmicTonemap tonemap;
    private static int expectedFrameCount;
    private static int preCullLightingCheckCount;
    private static string lightingStateSha256;
    private static string sceneSha256;
    private static string reflectionReceiptSha256;
    private static string reflectionWaitProofSha256;
    private static bool lightingDriftObserved;
    private static string lightingDriftMessage;

    public static bool IsRunning => running;

    [MenuItem("NewTown/QA/Validate Temporal Runtime Evidence Contract")]
    public static void ValidateContractConfigOnly()
    {
        RuntimeContract contract = LoadJson<RuntimeContract>(ContractPath);
        if (contract == null || contract.schemaVersion != "1.0")
            throw new InvalidOperationException("Temporal runtime evidence contract is missing or unsupported; schema 1.0 is required.");
        if (contract.scenePath != ScenePath || contract.width != Width || contract.height != Height ||
            contract.authoritativeGuard != "QualityBlockTemporalRuntimeEvidenceGuard")
            throw new InvalidOperationException("Temporal runtime evidence contract identity/dimensions drifted.");
        if (contract.requirements == null ||
            !contract.requirements.requireSameLightingFingerprintBeforeEveryMainCameraCull ||
            !contract.requirements.requireOnePreCullLightingCheckPerTemporalFrame ||
            !contract.requirements.requireFilmicTonemapForEveryTemporalFrame ||
            !contract.requirements.requireHdrSourceForEveryTemporalSequence ||
            !contract.requirements.requireLdrDestinationForEveryTemporalSequence ||
            !contract.requirements.requireZeroFallbackBlits ||
            !contract.requirements.bindSceneSha256 ||
            !contract.requirements.bindTemporalManifestSha256 ||
            !contract.requirements.bindTemporalReceiptSha256 ||
            !contract.requirements.bindReflectionCompletionReceiptSha256 ||
            !contract.requirements.bindReflectionAsyncWaitProofSha256 ||
            !contract.requirements.manualPixelReviewRequired ||
            contract.requirements.automaticVisualPoints != 0)
            throw new InvalidOperationException("Temporal runtime evidence requirements were weakened or are incomplete.");

        int contractFrameCount = ResolveExpectedFrameCount();
        if (contractFrameCount < 18)
            throw new InvalidOperationException($"Temporal runtime evidence requires at least 18 native-4K frames across the two probes; contract resolves to {contractFrameCount}.");

        Debug.Log("Temporal runtime evidence contract valid: per-frame pre-cull lighting invariance + filmic HDR->LDR execution, zero automatic visual points.");
    }

    public static void Begin()
    {
        if (running)
            throw new InvalidOperationException("Temporal runtime evidence guard is already running.");

        ValidateContractConfigOnly();
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
            throw new InvalidOperationException($"Temporal runtime guard requires the persisted benchmark scene: {ScenePath}");
        if (scene.isDirty)
            throw new InvalidOperationException("Temporal runtime guard refused: benchmark scene has unsaved changes after still/reflection synchronization.");

        QualityBlockReflectionProbeCaptureSyncQA.ValidateRuntimeReceipt();
        QualityBlockReflectionProbeAwaiter.ValidateLatestWaitProof();
        QualityBlockSolarShadowCaptureCoherenceQA.ValidateOpenScene();

        string sceneAbsolute = AbsolutePath(ScenePath);
        string reflectionReceiptAbsolute = AbsolutePath(ReflectionReceiptPath);
        string reflectionWaitAbsolute = AbsolutePath(ReflectionWaitProofPath);
        if (!File.Exists(sceneAbsolute) || !File.Exists(reflectionReceiptAbsolute) || !File.Exists(reflectionWaitAbsolute))
            throw new InvalidOperationException("Temporal runtime guard cannot bind the prepared scene/reflection proof because a required file is missing.");

        guardedCamera = Camera.main;
        if (guardedCamera == null)
            throw new InvalidOperationException("Temporal runtime guard cannot find MainCamera.");
        if (guardedCamera.renderingPath != RenderingPath.Forward || !guardedCamera.allowHDR || guardedCamera.allowDynamicResolution)
            throw new InvalidOperationException("Temporal runtime MainCamera must remain Forward + HDR with dynamic resolution disabled.");

        QualityBlockFilmicTonemap[] effects = guardedCamera.GetComponents<QualityBlockFilmicTonemap>();
        if (effects.Length != 1 || effects[0] == null || !effects[0].enabled)
            throw new InvalidOperationException($"Temporal runtime MainCamera must contain exactly one enabled QualityBlockFilmicTonemap, found {effects.Length}.");
        tonemap = effects[0];
        if (tonemap.FilmicShader == null || !tonemap.FilmicShader.isSupported || tonemap.FilmicShader.name != "Hidden/NewTown/FilmicTonemap")
            throw new InvalidOperationException("Temporal runtime filmic shader is missing, unsupported, or has the wrong identity.");
        if (!HasTransformsToLdrAttribute())
            throw new InvalidOperationException("QualityBlockFilmicTonemap.OnRenderImage lost ImageEffectTransformsToLDR; temporal PNG provenance is invalid.");

        expectedFrameCount = ResolveExpectedFrameCount();
        preCullLightingCheckCount = 0;
        lightingDriftObserved = false;
        lightingDriftMessage = string.Empty;
        sceneSha256 = Sha256File(sceneAbsolute);
        reflectionReceiptSha256 = Sha256File(reflectionReceiptAbsolute);
        reflectionWaitProofSha256 = Sha256File(reflectionWaitAbsolute);
        lightingStateSha256 = QualityBlockReflectionLightingStateFingerprint.BuildCurrentSha256();
        QualityBlockReflectionLightingStateFingerprint.RequireCurrentMatch(lightingStateSha256, "prepared temporal sequence start");

        tonemap.ResetRuntimeTelemetry();
        Camera.onPreCull += OnCameraPreCull;
        running = true;

        Debug.Log($"Temporal runtime evidence guard armed for {expectedFrameCount} native-4K frames under lighting fingerprint {lightingStateSha256}.");
    }

    public static void CompleteAndSeal()
    {
        if (!running)
            throw new InvalidOperationException("Temporal runtime evidence guard was not started.");

        Camera.onPreCull -= OnCameraPreCull;
        running = false;

        try
        {
            if (lightingDriftObserved)
                throw new InvalidOperationException("Temporal runtime lighting drift was observed: " + lightingDriftMessage);

            QualityBlockReflectionLightingStateFingerprint.RequireCurrentMatch(
                lightingStateSha256,
                "prepared temporal sequence completion");

            if (preCullLightingCheckCount != expectedFrameCount)
                throw new InvalidOperationException(
                    $"Temporal sequence observed {preCullLightingCheckCount} MainCamera pre-cull lighting checks for {expectedFrameCount} required frames. " +
                    "An extra/missing render makes temporal provenance ambiguous.");

            ValidateTonemapTelemetry(expectedFrameCount);

            string temporalManifestAbsolute = AbsolutePath(TemporalManifestPath);
            string temporalReceiptAbsolute = AbsolutePath(TemporalReceiptPath);
            string preparedBindingAbsolute = AbsolutePath(PreparedBindingPath);
            if (!File.Exists(temporalManifestAbsolute) || !File.Exists(temporalReceiptAbsolute) || !File.Exists(preparedBindingAbsolute))
                throw new InvalidOperationException("Prepared temporal capture did not produce its manifest/receipt/binding before runtime sealing.");

            if (!EqualsSha(sceneSha256, Sha256File(AbsolutePath(ScenePath))))
                throw new InvalidOperationException("Persisted benchmark scene changed during the guarded temporal sequence.");
            if (!EqualsSha(reflectionReceiptSha256, Sha256File(AbsolutePath(ReflectionReceiptPath))) ||
                !EqualsSha(reflectionWaitProofSha256, Sha256File(AbsolutePath(ReflectionWaitProofPath))))
                throw new InvalidOperationException("Reflection completion/wait proof changed during the guarded temporal sequence.");

            TemporalManifest manifest = LoadJson<TemporalManifest>(TemporalManifestPath);
            if (manifest == null || !manifest.renderProducedByUnity || manifest.width != Width || manifest.height != Height ||
                string.IsNullOrWhiteSpace(manifest.captureSessionId))
                throw new InvalidOperationException("Temporal runtime guard cannot bind a valid native-4K Unity temporal manifest.");

            var receipt = new RuntimeReceipt
            {
                schemaVersion = "1.0",
                generatedUtc = DateTime.UtcNow.ToString("O"),
                captureSessionId = manifest.captureSessionId,
                unityVersion = Application.unityVersion,
                graphicsDeviceName = SystemInfo.graphicsDeviceName,
                graphicsDeviceType = SystemInfo.graphicsDeviceType.ToString(),
                scenePath = ScenePath,
                sceneSha256 = sceneSha256,
                width = Width,
                height = Height,
                expectedFrameCount = expectedFrameCount,
                mainCameraPreCullLightingCheckCount = preCullLightingCheckCount,
                lightingFingerprintAlgorithm = QualityBlockReflectionLightingStateFingerprint.Algorithm,
                lightingStateSha256 = lightingStateSha256,
                lightingStateStableAcrossTemporalSequence = true,
                renderInvocationCount = tonemap.RenderInvocationCount,
                tonemapAppliedInvocationCount = tonemap.TonemapAppliedInvocationCount,
                native4KInvocationCount = tonemap.Native4KInvocationCount,
                native4KTonemapAppliedCount = tonemap.Native4KTonemapAppliedCount,
                fallbackInvocationCount = tonemap.FallbackInvocationCount,
                lastSourceFormat = tonemap.LastSourceFormat.ToString(),
                lastDestinationFormat = tonemap.LastDestinationFormat.ToString(),
                lastSourceWidth = tonemap.LastSourceWidth,
                lastSourceHeight = tonemap.LastSourceHeight,
                lastDestinationWidth = tonemap.LastDestinationWidth,
                lastDestinationHeight = tonemap.LastDestinationHeight,
                lastDestinationWasNull = tonemap.LastDestinationWasNull,
                hdrSourceObserved = IsHdrFormat(tonemap.LastSourceFormat),
                ldrDestinationObserved = !IsHdrFormat(tonemap.LastDestinationFormat),
                temporalManifestPath = TemporalManifestPath,
                temporalManifestSha256 = Sha256File(temporalManifestAbsolute),
                temporalReceiptPath = TemporalReceiptPath,
                temporalReceiptSha256 = Sha256File(temporalReceiptAbsolute),
                preparedBindingPath = PreparedBindingPath,
                preparedBindingSha256 = Sha256File(preparedBindingAbsolute),
                reflectionCompletionReceiptPath = ReflectionReceiptPath,
                reflectionCompletionReceiptSha256 = reflectionReceiptSha256,
                reflectionAsyncWaitProofPath = ReflectionWaitProofPath,
                reflectionAsyncWaitProofSha256 = reflectionWaitProofSha256,
                renderProducedByUnity = true,
                manualPixelReviewRequired = true,
                automaticVisualPoints = 0,
                visualFidelityStatus = "UNSCORED_REVIEW_REQUIRED"
            };

            WriteJson(RuntimeReceiptPath, receipt);
            AssetDatabase.Refresh();
            ValidateLatestReceiptForScoring();

            Debug.Log(
                $"Temporal runtime evidence sealed: frames={expectedFrameCount}, preCullLightingChecks={preCullLightingCheckCount}, " +
                $"filmicInvocations={tonemap.Native4KTonemapAppliedCount}, fallback=0, lighting={lightingStateSha256}. " +
                "Visual Fidelity remains UNSCORED_REVIEW_REQUIRED.");
        }
        finally
        {
            ClearState();
        }
    }

    public static void Abort()
    {
        if (running)
            Camera.onPreCull -= OnCameraPreCull;
        running = false;
        ClearState();
    }

    [MenuItem("NewTown/QA/Validate Latest Temporal Runtime Evidence Receipt")]
    public static void ValidateLatestReceiptForScoring()
    {
        ValidateContractConfigOnly();
        if (!File.Exists(AbsolutePath(RuntimeReceiptPath)))
            throw new InvalidOperationException("Temporal runtime evidence receipt is missing. Real prepared Unity temporal rendering is required before scoring.");

        RuntimeReceipt receipt = LoadJson<RuntimeReceipt>(RuntimeReceiptPath);
        if (receipt == null || receipt.schemaVersion != "1.0")
            throw new InvalidOperationException("Temporal runtime evidence receipt is unreadable or unsupported.");
        if (!receipt.renderProducedByUnity || !receipt.manualPixelReviewRequired || receipt.automaticVisualPoints != 0 ||
            receipt.visualFidelityStatus != "UNSCORED_REVIEW_REQUIRED")
            throw new InvalidOperationException("Temporal runtime receipt may prove provenance only; it cannot award visual points or claim PASS.");
        if (receipt.scenePath != ScenePath || receipt.width != Width || receipt.height != Height || receipt.unityVersion != Application.unityVersion)
            throw new InvalidOperationException("Temporal runtime receipt does not match the current benchmark/Unity runtime identity.");

        int currentExpectedFrameCount = ResolveExpectedFrameCount();
        if (receipt.expectedFrameCount != currentExpectedFrameCount ||
            receipt.mainCameraPreCullLightingCheckCount != currentExpectedFrameCount ||
            receipt.renderInvocationCount != currentExpectedFrameCount ||
            receipt.tonemapAppliedInvocationCount != currentExpectedFrameCount ||
            receipt.native4KInvocationCount != currentExpectedFrameCount ||
            receipt.native4KTonemapAppliedCount != currentExpectedFrameCount ||
            receipt.fallbackInvocationCount != 0)
            throw new InvalidOperationException("Temporal runtime receipt does not prove one guarded filmic native-4K render for every required temporal frame.");

        if (receipt.lightingFingerprintAlgorithm != QualityBlockReflectionLightingStateFingerprint.Algorithm ||
            string.IsNullOrWhiteSpace(receipt.lightingStateSha256) || receipt.lightingStateSha256.Length != 64 ||
            !receipt.lightingStateStableAcrossTemporalSequence)
            throw new InvalidOperationException("Temporal runtime receipt does not prove one invariant physical-lighting fingerprint across the sequence.");
        if (!receipt.hdrSourceObserved || !receipt.ldrDestinationObserved || receipt.lastDestinationWasNull ||
            receipt.lastSourceWidth != Width || receipt.lastSourceHeight != Height ||
            receipt.lastDestinationWidth != Width || receipt.lastDestinationHeight != Height)
            throw new InvalidOperationException("Temporal runtime receipt does not prove a native-4K HDR source -> LDR destination display transform.");

        RequireBoundFile(receipt.scenePath, receipt.sceneSha256, "scene");
        RequireBoundFile(receipt.temporalManifestPath, receipt.temporalManifestSha256, "temporal manifest");
        RequireBoundFile(receipt.temporalReceiptPath, receipt.temporalReceiptSha256, "temporal receipt");
        RequireBoundFile(receipt.preparedBindingPath, receipt.preparedBindingSha256, "prepared temporal binding");
        RequireBoundFile(receipt.reflectionCompletionReceiptPath, receipt.reflectionCompletionReceiptSha256, "reflection completion receipt");
        RequireBoundFile(receipt.reflectionAsyncWaitProofPath, receipt.reflectionAsyncWaitProofSha256, "reflection async wait proof");

        TemporalManifest manifest = LoadJson<TemporalManifest>(receipt.temporalManifestPath);
        if (manifest == null || manifest.captureSessionId != receipt.captureSessionId || !manifest.renderProducedByUnity ||
            manifest.width != Width || manifest.height != Height)
            throw new InvalidOperationException("Temporal runtime receipt is not bound to the current native-4K Unity temporal session.");

        QualityBlockPreparedTemporalCapture.ValidateLatestPreparedBinding();
        Debug.Log("Temporal runtime evidence receipt valid for scoring provenance. Actual shimmer/LOD/aliasing quality still requires manual 100%-pixel review; zero points are automatic.");
    }

    private static void OnCameraPreCull(Camera camera)
    {
        if (!running || camera == null || camera != guardedCamera)
            return;

        preCullLightingCheckCount++;
        try
        {
            QualityBlockReflectionLightingStateFingerprint.RequireCurrentMatch(
                lightingStateSha256,
                $"prepared temporal MainCamera pre-cull #{preCullLightingCheckCount}");
        }
        catch (Exception ex)
        {
            lightingDriftObserved = true;
            lightingDriftMessage = ex.Message;
            throw;
        }
    }

    private static void ValidateTonemapTelemetry(int requiredFrames)
    {
        if (tonemap == null)
            throw new InvalidOperationException("Temporal filmic tonemap telemetry target was lost during capture.");
        if (tonemap.RenderInvocationCount != requiredFrames ||
            tonemap.TonemapAppliedInvocationCount != requiredFrames ||
            tonemap.Native4KInvocationCount != requiredFrames ||
            tonemap.Native4KTonemapAppliedCount != requiredFrames)
            throw new InvalidOperationException(
                $"Temporal filmic telemetry mismatch. expected={requiredFrames}, render={tonemap.RenderInvocationCount}, " +
                $"tonemap={tonemap.TonemapAppliedInvocationCount}, native4K={tonemap.Native4KInvocationCount}, nativeTonemap={tonemap.Native4KTonemapAppliedCount}.");
        if (tonemap.FallbackInvocationCount != 0)
            throw new InvalidOperationException($"Temporal capture used {tonemap.FallbackInvocationCount} fallback blits; evidence is not scoreable.");
        if (tonemap.LastSourceWidth != Width || tonemap.LastSourceHeight != Height ||
            tonemap.LastDestinationWidth != Width || tonemap.LastDestinationHeight != Height || tonemap.LastDestinationWasNull)
            throw new InvalidOperationException("Temporal filmic telemetry does not end on a native 3840x2160 source/destination pair.");
        if (!IsHdrFormat(tonemap.LastSourceFormat) || IsHdrFormat(tonemap.LastDestinationFormat))
            throw new InvalidOperationException(
                $"Temporal display transform is not proven HDR->LDR: source={tonemap.LastSourceFormat}, destination={tonemap.LastDestinationFormat}.");
    }

    private static int ResolveExpectedFrameCount()
    {
        TemporalContract contract = LoadJson<TemporalContract>(TemporalContractPath);
        if (contract == null || contract.width != Width || contract.height != Height || contract.probes == null || contract.probes.Length != 2)
            throw new InvalidOperationException("Temporal stability contract must define exactly two native-4K probes.");

        string[] ids = contract.probes.Where(x => x != null).Select(x => x.id).ToArray();
        if (ids.Length != 2 || !ids.Contains("subpixel_grazing") || !ids.Contains("lod_walk_oblique") || ids.Distinct().Count() != 2)
            throw new InvalidOperationException("Temporal runtime guard requires exactly subpixel_grazing and lod_walk_oblique probes.");
        if (contract.probes.Any(x => x == null || x.frameCount < 9))
            throw new InvalidOperationException("Each temporal runtime probe must contain at least 9 frames.");

        return contract.probes.Sum(x => x.frameCount);
    }

    private static bool HasTransformsToLdrAttribute()
    {
        MethodInfo method = typeof(QualityBlockFilmicTonemap).GetMethod("OnRenderImage", BindingFlags.Instance | BindingFlags.NonPublic);
        return method != null && Attribute.IsDefined(method, typeof(ImageEffectTransformsToLDR), true);
    }

    private static bool IsHdrFormat(RenderTextureFormat format)
    {
        return format == RenderTextureFormat.ARGBHalf ||
               format == RenderTextureFormat.ARGBFloat ||
               format == RenderTextureFormat.RGB111110Float ||
               format == RenderTextureFormat.DefaultHDR;
    }

    private static void RequireBoundFile(string assetPath, string expectedSha256, string label)
    {
        if (string.IsNullOrWhiteSpace(assetPath) || string.IsNullOrWhiteSpace(expectedSha256) || !File.Exists(AbsolutePath(assetPath)))
            throw new InvalidOperationException($"Temporal runtime receipt references a missing {label}.");
        if (!EqualsSha(expectedSha256, Sha256File(AbsolutePath(assetPath))))
            throw new InvalidOperationException($"Temporal runtime {label} SHA-256 no longer matches the sealed receipt.");
    }

    private static void ClearState()
    {
        guardedCamera = null;
        tonemap = null;
        expectedFrameCount = 0;
        preCullLightingCheckCount = 0;
        lightingStateSha256 = null;
        sceneSha256 = null;
        reflectionReceiptSha256 = null;
        reflectionWaitProofSha256 = null;
        lightingDriftObserved = false;
        lightingDriftMessage = null;
    }

    private static T LoadJson<T>(string assetPath)
    {
        string absolute = AbsolutePath(assetPath);
        if (!File.Exists(absolute))
            throw new FileNotFoundException($"Required QA file not found: {assetPath}");
        T value = JsonUtility.FromJson<T>(File.ReadAllText(absolute));
        if (value == null)
            throw new InvalidOperationException($"Could not parse QA JSON: {assetPath}");
        return value;
    }

    private static void WriteJson<T>(string assetPath, T value)
    {
        string absolute = AbsolutePath(assetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute));
        File.WriteAllText(absolute, JsonUtility.ToJson(value, true));
    }

    private static string Sha256File(string absolutePath)
    {
        using (SHA256 sha = SHA256.Create())
        using (FileStream stream = File.OpenRead(absolutePath))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static bool EqualsSha(string a, string b) =>
        !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b) &&
        string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string AbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    [Serializable]
    private sealed class RuntimeContract
    {
        public string schemaVersion;
        public string authoritativeGuard;
        public string scenePath;
        public int width;
        public int height;
        public Requirements requirements;
    }

    [Serializable]
    private sealed class Requirements
    {
        public bool requireSameLightingFingerprintBeforeEveryMainCameraCull;
        public bool requireOnePreCullLightingCheckPerTemporalFrame;
        public bool requireFilmicTonemapForEveryTemporalFrame;
        public bool requireHdrSourceForEveryTemporalSequence;
        public bool requireLdrDestinationForEveryTemporalSequence;
        public bool requireZeroFallbackBlits;
        public bool bindSceneSha256;
        public bool bindTemporalManifestSha256;
        public bool bindTemporalReceiptSha256;
        public bool bindReflectionCompletionReceiptSha256;
        public bool bindReflectionAsyncWaitProofSha256;
        public bool manualPixelReviewRequired;
        public int automaticVisualPoints;
    }

    [Serializable]
    private sealed class TemporalContract
    {
        public int width;
        public int height;
        public ProbeSpec[] probes;
    }

    [Serializable]
    private sealed class ProbeSpec
    {
        public string id;
        public int frameCount;
    }

    [Serializable]
    private sealed class TemporalManifest
    {
        public string captureSessionId;
        public bool renderProducedByUnity;
        public int width;
        public int height;
    }

    [Serializable]
    private sealed class RuntimeReceipt
    {
        public string schemaVersion;
        public string generatedUtc;
        public string captureSessionId;
        public string unityVersion;
        public string graphicsDeviceName;
        public string graphicsDeviceType;
        public string scenePath;
        public string sceneSha256;
        public int width;
        public int height;
        public int expectedFrameCount;
        public int mainCameraPreCullLightingCheckCount;
        public string lightingFingerprintAlgorithm;
        public string lightingStateSha256;
        public bool lightingStateStableAcrossTemporalSequence;
        public int renderInvocationCount;
        public int tonemapAppliedInvocationCount;
        public int native4KInvocationCount;
        public int native4KTonemapAppliedCount;
        public int fallbackInvocationCount;
        public string lastSourceFormat;
        public string lastDestinationFormat;
        public int lastSourceWidth;
        public int lastSourceHeight;
        public int lastDestinationWidth;
        public int lastDestinationHeight;
        public bool lastDestinationWasNull;
        public bool hdrSourceObserved;
        public bool ldrDestinationObserved;
        public string temporalManifestPath;
        public string temporalManifestSha256;
        public string temporalReceiptPath;
        public string temporalReceiptSha256;
        public string preparedBindingPath;
        public string preparedBindingSha256;
        public string reflectionCompletionReceiptPath;
        public string reflectionCompletionReceiptSha256;
        public string reflectionAsyncWaitProofPath;
        public string reflectionAsyncWaitProofSha256;
        public bool renderProducedByUnity;
        public bool manualPixelReviewRequired;
        public int automaticVisualPoints;
        public string visualFidelityStatus;
    }
}
