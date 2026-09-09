using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Waits for the two benchmark realtime reflection probes across real Editor updates before any
/// native-4K benchmark Camera.Render is allowed to run. This exists because RenderProbe() returns a
/// RenderID for later completion testing; checking IsFinishedRendering(RenderID) in the same call
/// stack can fail before Unity has advanced the frame that performs a NoTimeSlicing render.
///
/// Successful completion writes the schema-1.0 probe receipt consumed by
/// QualityBlockReflectionProbeCaptureSyncQA plus a SHA-256-bound async-wait proof. The latter prevents
/// a legacy same-call-stack refresh receipt from being mistaken for evidence that the Editor actually
/// advanced between RenderProbe() and benchmark Camera.Render. Neither artifact can award Visual
/// Fidelity points.
/// </summary>
public static class QualityBlockReflectionProbeAwaiter
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ProbeRootName = "PhysicalReflectionEnvironment";
    private const string ReceiptPath = "Assets/QA/reflection_probe_refresh_receipt.json";
    private const string WaitProofPath = "Assets/QA/reflection_probe_async_wait_receipt.json";
    private const int RequiredProbeCount = 2;
    private const int RequiredResolution = 512;
    private const double TimeoutSeconds = 30.0;
    private const double MaxProofAgeMinutes = 2.0;

    private static readonly string[] RequiredProbeNames =
    {
        "ReflectionProbe_DanchiFacade",
        "ReflectionProbe_ParkGround",
    };

    private static ReflectionProbe[] probes;
    private static int[] renderIds;
    private static double startedAt;
    private static int editorPollCount;
    private static Action continuation;
    private static bool running;

    public static bool IsRunning => running;

    /// <summary>
    /// Starts a fail-closed probe refresh and returns immediately. The supplied continuation is
    /// invoked only after a subsequent Editor update proves that every requested cubemap completed
    /// and its 512x512 Cube realtimeTexture exists.
    /// </summary>
    public static void Begin(Action onCompleted)
    {
        if (running)
            throw new InvalidOperationException("A benchmark reflection-probe synchronization wait is already running.");
        if (onCompleted == null)
            throw new ArgumentNullException(nameof(onCompleted));
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            throw new InvalidOperationException($"Reflection synchronization must start from the persisted benchmark scene: {ScenePath}");

        QualityBlockEnvironmentLightingUpgrade.ValidateOpenScene();

        GameObject root = Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x.scene.IsValid() && x.name == ProbeRootName);
        if (root == null)
            throw new InvalidOperationException($"Benchmark reflection root is missing: {ProbeRootName}");

        probes = root.GetComponentsInChildren<ReflectionProbe>(true)
            .OrderBy(x => x.name, StringComparer.Ordinal)
            .ToArray();
        if (probes.Length != RequiredProbeCount)
            throw new InvalidOperationException($"Expected exactly {RequiredProbeCount} benchmark reflection probes, found {probes.Length}.");

        string[] actualNames = probes.Select(x => x.name).ToArray();
        if (!actualNames.SequenceEqual(RequiredProbeNames.OrderBy(x => x, StringComparer.Ordinal), StringComparer.Ordinal))
            throw new InvalidOperationException("Benchmark reflection-probe identities drifted before synchronization.");

        renderIds = new int[probes.Length];
        for (int i = 0; i < probes.Length; ++i)
        {
            ReflectionProbe probe = probes[i];
            ValidateProbeState(probe);
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            int renderId = probe.RenderProbe();
            if (renderId < 0)
                throw new InvalidOperationException($"{probe.name} returned invalid RenderID {renderId}; native-4K capture is aborted.");
            renderIds[i] = renderId;
        }

        continuation = onCompleted;
        startedAt = EditorApplication.timeSinceStartup;
        editorPollCount = 0;
        running = true;
        EditorApplication.update += Poll;

        // NoTimeSlicing still means the cubemap is rendered in a frame; explicitly request Editor
        // progression rather than assuming the RenderProbe() call completed that frame synchronously.
        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
        Debug.Log("Reflection probe refresh requested. Native-4K capture is deferred until later Editor updates prove both RenderIDs complete.");
    }

    /// <summary>
    /// Validates that the current probe completion receipt was produced by the asynchronous waiter,
    /// not by the legacy same-call-stack compatibility path. This is required immediately before the
    /// benchmark stills are rendered.
    /// </summary>
    public static void ValidateLatestWaitProof()
    {
        string proofAbsolute = AbsolutePath(WaitProofPath);
        string receiptAbsolute = AbsolutePath(ReceiptPath);
        if (!File.Exists(proofAbsolute))
            throw new InvalidOperationException("No async reflection wait proof exists. Benchmark still capture must use the complete native-4K review packet.");
        if (!File.Exists(receiptAbsolute))
            throw new InvalidOperationException("Reflection probe refresh receipt is missing while validating async wait proof.");

        ReflectionProbeAsyncWaitProof proof = JsonUtility.FromJson<ReflectionProbeAsyncWaitProof>(File.ReadAllText(proofAbsolute));
        if (proof == null || proof.schemaVersion != "1.0")
            throw new InvalidOperationException("Async reflection wait proof is unreadable or has an unsupported schema.");
        if (proof.scenePath != ScenePath || proof.unityVersion != Application.unityVersion)
            throw new InvalidOperationException("Async reflection wait proof does not match the active benchmark scene/current Unity editor.");
        if (proof.observationMechanism != "EditorApplication.update" || proof.editorPollCount < 1)
            throw new InvalidOperationException("Async reflection wait proof does not demonstrate a later EditorApplication.update completion observation.");
        if (proof.timeoutSeconds != TimeoutSeconds || proof.elapsedSeconds < 0.0)
            throw new InvalidOperationException("Async reflection wait proof timing policy drifted.");
        if (proof.renderIds == null || proof.renderIds.Length != RequiredProbeCount || proof.renderIds.Any(x => x < 0))
            throw new InvalidOperationException("Async reflection wait proof does not contain the two valid benchmark RenderIDs.");
        if (!proof.allFinishedAndTextureReady)
            throw new InvalidOperationException("Async reflection wait proof does not assert completed and texture-ready probes.");

        string currentReceiptSha = Sha256(receiptAbsolute);
        if (!string.Equals(proof.reflectionReceiptSha256, currentReceiptSha, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Async reflection wait proof is not SHA-256-bound to the current reflection completion receipt.");

        if (!DateTime.TryParse(proof.generatedUtc, null,
                System.Globalization.DateTimeStyles.RoundtripKind, out DateTime generatedUtc))
            throw new InvalidOperationException("Async reflection wait proof generatedUtc is invalid.");
        TimeSpan age = DateTime.UtcNow - generatedUtc.ToUniversalTime();
        if (age.TotalMinutes < -1.0 || age.TotalMinutes > MaxProofAgeMinutes)
            throw new InvalidOperationException($"Async reflection wait proof is not fresh enough for immediate still capture (ageMinutes={age.TotalMinutes:F2}).");

        Debug.Log($"Async reflection wait proof valid: polls={proof.editorPollCount}, elapsed={proof.elapsedSeconds:F3}s, receipt SHA-256 bound. No visual points awarded.");
    }

    private static void Poll()
    {
        if (!running)
            return;

        try
        {
            editorPollCount++;
            bool allReady = true;
            for (int i = 0; i < probes.Length; ++i)
            {
                ReflectionProbe probe = probes[i];
                bool finished = probe.IsFinishedRendering(renderIds[i]);
                RenderTexture realtime = probe.realtimeTexture;
                bool textureReady = IsTextureReady(probe, realtime);
                allReady &= finished && textureReady;
            }

            if (!allReady)
            {
                double elapsed = EditorApplication.timeSinceStartup - startedAt;
                if (elapsed > TimeoutSeconds)
                    throw new TimeoutException(BuildTimeoutMessage(elapsed));

                EditorApplication.QueuePlayerLoopUpdate();
                SceneView.RepaintAll();
                return;
            }

            double elapsedSeconds = EditorApplication.timeSinceStartup - startedAt;
            int completedPollCount = editorPollCount;
            int[] completedRenderIds = (int[])renderIds.Clone();
            QualityBlockReflectionProbeRefreshReceipt receipt = BuildReceipt(elapsedSeconds);
            WriteReceipt(receipt);
            WriteWaitProof(elapsedSeconds, completedPollCount, completedRenderIds);
            AssetDatabase.Refresh();
            QualityBlockReflectionProbeCaptureSyncQA.ValidateRuntimeReceipt();
            ValidateLatestWaitProof();

            Action next = continuation;
            Cleanup();
            Debug.Log($"Reflection synchronization completed after {completedPollCount} Editor polls / {elapsedSeconds:F3}s. Continuing native-4K capture.");
            next();
        }
        catch (Exception ex)
        {
            Cleanup();
            Debug.LogException(ex);
            throw;
        }
    }

    private static void ValidateProbeState(ReflectionProbe probe)
    {
        if (probe == null)
            throw new InvalidOperationException("Null reflection probe encountered during synchronization.");
        if (!probe.isActiveAndEnabled)
            throw new InvalidOperationException($"{probe.name} is not active and enabled.");
        if (probe.mode != ReflectionProbeMode.Realtime)
            throw new InvalidOperationException($"{probe.name} must remain Realtime.");
        if (probe.refreshMode != ReflectionProbeRefreshMode.ViaScripting)
            throw new InvalidOperationException($"{probe.name} must remain ViaScripting.");
        if (probe.resolution != RequiredResolution)
            throw new InvalidOperationException($"{probe.name} resolution drifted to {probe.resolution}; expected {RequiredResolution}.");
        if (!probe.hdr || !probe.boxProjection)
            throw new InvalidOperationException($"{probe.name} must retain HDR and box projection.");
    }

    private static bool IsTextureReady(ReflectionProbe probe, RenderTexture realtime)
    {
        return realtime != null && realtime.IsCreated() &&
               realtime.dimension == TextureDimension.Cube &&
               realtime.width == probe.resolution && realtime.height == probe.resolution;
    }

    private static QualityBlockReflectionProbeRefreshReceipt BuildReceipt(double elapsedSeconds)
    {
        QualityBlockReflectionProbeRefreshRecord[] records = new QualityBlockReflectionProbeRefreshRecord[probes.Length];
        for (int i = 0; i < probes.Length; ++i)
        {
            ReflectionProbe probe = probes[i];
            RenderTexture realtime = probe.realtimeTexture;
            records[i] = new QualityBlockReflectionProbeRefreshRecord
            {
                name = probe.name,
                renderId = renderIds[i],
                finished = probe.IsFinishedRendering(renderIds[i]),
                textureReady = IsTextureReady(probe, realtime),
                expectedResolution = probe.resolution,
                actualWidth = realtime != null ? realtime.width : 0,
                actualHeight = realtime != null ? realtime.height : 0,
                textureDimension = realtime != null ? realtime.dimension.ToString() : string.Empty,
                hdr = probe.hdr,
                timeSlicingMode = probe.timeSlicingMode.ToString(),
                refreshMode = probe.refreshMode.ToString(),
            };
        }

        return new QualityBlockReflectionProbeRefreshReceipt
        {
            schemaVersion = "1.0",
            generatedUtc = DateTime.UtcNow.ToString("O"),
            unityVersion = Application.unityVersion,
            graphicsDevice = SystemInfo.graphicsDeviceName,
            scenePath = EditorSceneManager.GetActiveScene().path,
            requestedProbeCount = probes.Length,
            completedProbeCount = records.Count(x => x.finished && x.textureReady),
            allFinishedBeforeBenchmarkCapture = records.All(x => x.finished && x.textureReady),
            visualFidelityStatus = "UNSCORED_REVIEW_REQUIRED",
            probes = records,
            note = $"Synchronization receipt only. RenderProbe completion was observed on later Editor updates before still capture (polls={editorPollCount}, elapsedSeconds={elapsedSeconds:F3}); no Visual Fidelity points are awarded."
        };
    }

    private static string BuildTimeoutMessage(double elapsed)
    {
        string detail = string.Join(" | ", probes.Select((probe, i) =>
        {
            RenderTexture realtime = probe.realtimeTexture;
            return $"{probe.name}: renderId={renderIds[i]}, finished={probe.IsFinishedRendering(renderIds[i])}, " +
                   $"textureReady={IsTextureReady(probe, realtime)}, size={(realtime != null ? realtime.width : 0)}x{(realtime != null ? realtime.height : 0)}";
        }));
        return $"Timed out after {elapsed:F2}s waiting for realtime reflection probes across Editor updates. Native-4K capture aborted. {detail}";
    }

    private static void WriteReceipt(QualityBlockReflectionProbeRefreshReceipt receipt)
    {
        string absolute = AbsolutePath(ReceiptPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute));
        File.WriteAllText(absolute, JsonUtility.ToJson(receipt, true));
    }

    private static void WriteWaitProof(double elapsedSeconds, int pollCount, int[] completedRenderIds)
    {
        string receiptAbsolute = AbsolutePath(ReceiptPath);
        if (!File.Exists(receiptAbsolute))
            throw new InvalidOperationException("Cannot bind async wait proof because the reflection completion receipt was not written.");

        var proof = new ReflectionProbeAsyncWaitProof
        {
            schemaVersion = "1.0",
            generatedUtc = DateTime.UtcNow.ToString("O"),
            unityVersion = Application.unityVersion,
            scenePath = EditorSceneManager.GetActiveScene().path,
            observationMechanism = "EditorApplication.update",
            editorPollCount = pollCount,
            elapsedSeconds = elapsedSeconds,
            timeoutSeconds = TimeoutSeconds,
            renderIds = completedRenderIds,
            allFinishedAndTextureReady = true,
            reflectionReceiptPath = ReceiptPath,
            reflectionReceiptSha256 = Sha256(receiptAbsolute),
            visualFidelityStatus = "UNSCORED_REVIEW_REQUIRED",
            note = "Evidence-integrity proof only. It proves Editor-frame progression between RenderProbe requests and accepted completion; no image-quality points are implied."
        };

        string proofAbsolute = AbsolutePath(WaitProofPath);
        Directory.CreateDirectory(Path.GetDirectoryName(proofAbsolute));
        File.WriteAllText(proofAbsolute, JsonUtility.ToJson(proof, true));
    }

    private static string Sha256(string absolutePath)
    {
        using var sha = SHA256.Create();
        using FileStream stream = File.OpenRead(absolutePath);
        return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static string AbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private static void Cleanup()
    {
        EditorApplication.update -= Poll;
        probes = null;
        renderIds = null;
        continuation = null;
        startedAt = 0.0;
        editorPollCount = 0;
        running = false;
    }

    [Serializable]
    private sealed class ReflectionProbeAsyncWaitProof
    {
        public string schemaVersion;
        public string generatedUtc;
        public string unityVersion;
        public string scenePath;
        public string observationMechanism;
        public int editorPollCount;
        public double elapsedSeconds;
        public double timeoutSeconds;
        public int[] renderIds;
        public bool allFinishedAndTextureReady;
        public string reflectionReceiptPath;
        public string reflectionReceiptSha256;
        public string visualFidelityStatus;
        public string note;
    }
}
