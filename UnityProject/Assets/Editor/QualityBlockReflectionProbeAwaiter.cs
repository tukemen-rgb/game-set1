using System;
using System.IO;
using System.Linq;
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
/// Successful completion writes the same schema-1.0 runtime receipt consumed by
/// QualityBlockReflectionProbeCaptureSyncQA. The receipt proves synchronization only and cannot award
/// Visual Fidelity points.
/// </summary>
public static class QualityBlockReflectionProbeAwaiter
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ProbeRootName = "PhysicalReflectionEnvironment";
    private const string ReceiptPath = "Assets/QA/reflection_probe_refresh_receipt.json";
    private const int RequiredProbeCount = 2;
    private const int RequiredResolution = 512;
    private const double TimeoutSeconds = 30.0;

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
            QualityBlockReflectionProbeRefreshReceipt receipt = BuildReceipt(elapsedSeconds);
            WriteReceipt(receipt);
            AssetDatabase.Refresh();
            QualityBlockReflectionProbeCaptureSyncQA.ValidateRuntimeReceipt();

            Action next = continuation;
            Cleanup();
            Debug.Log($"Reflection synchronization completed after {editorPollCount} Editor polls / {elapsedSeconds:F3}s. Continuing native-4K capture.");
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
}
