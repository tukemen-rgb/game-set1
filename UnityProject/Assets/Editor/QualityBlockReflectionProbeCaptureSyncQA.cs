using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Machine-enforced integrity checks for realtime reflection-probe state used by native-4K evidence.
/// The production review packet writes the runtime receipt only after QualityBlockReflectionProbeAwaiter
/// has observed completion on later Editor updates. Passing this QA proves synchronization only; it
/// never awards Visual Fidelity points.
/// </summary>
public static class QualityBlockReflectionProbeCaptureSyncQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/reflection_probe_capture_sync_contract.json";
    private const string ReceiptPath = "Assets/QA/reflection_probe_refresh_receipt.json";
    private const int RequiredProbeCount = 2;
    private const int RequiredResolution = 512;
    private const int RequiredTimeoutSeconds = 30;

    private static readonly string[] RequiredProbeNames =
    {
        "ReflectionProbe_DanchiFacade",
        "ReflectionProbe_ParkGround",
    };

    [MenuItem("NewTown/QA/Validate Reflection Probe Capture Sync Contract")]
    public static void ValidateContractConfigOnly()
    {
        string absolute = AbsolutePath(ContractPath);
        if (!File.Exists(absolute))
            throw new InvalidOperationException($"Reflection-probe capture synchronization contract missing: {ContractPath}");

        ReflectionProbeSyncContract contract = JsonUtility.FromJson<ReflectionProbeSyncContract>(File.ReadAllText(absolute));
        if (contract == null || contract.schemaVersion != "1.0")
            throw new InvalidOperationException("Reflection-probe synchronization contract schema must remain 1.0.");
        if (contract.requiredSceneState == null)
            throw new InvalidOperationException("Reflection-probe synchronization contract requiredSceneState is missing.");
        if (contract.requiredSceneState.probeCount != RequiredProbeCount ||
            contract.requiredSceneState.resolution != RequiredResolution)
            throw new InvalidOperationException("Reflection-probe synchronization contract count/resolution drifted.");
        if (contract.requiredSceneState.probeNames == null ||
            !contract.requiredSceneState.probeNames.OrderBy(x => x, StringComparer.Ordinal)
                .SequenceEqual(RequiredProbeNames.OrderBy(x => x, StringComparer.Ordinal), StringComparer.Ordinal))
            throw new InvalidOperationException("Reflection-probe synchronization contract probe identities drifted.");
        if (contract.requiredSceneState.mode != "Realtime" ||
            contract.requiredSceneState.refreshMode != "ViaScripting" ||
            contract.requiredSceneState.timeSlicingMode != "NoTimeSlicing" ||
            contract.requiredSceneState.textureDimension != "Cube" ||
            !contract.requiredSceneState.hdrRequired || !contract.requiredSceneState.boxProjectionRequired)
            throw new InvalidOperationException("Reflection-probe synchronization contract physical/render-state requirements drifted.");

        CompletionObservation observation = contract.completionObservation;
        if (observation == null)
            throw new InvalidOperationException("Reflection-probe synchronization contract completionObservation is missing.");
        if (observation.mechanism != "EditorApplication.update" ||
            !observation.requireLaterEditorUpdate ||
            observation.timeoutSeconds != RequiredTimeoutSeconds ||
            !observation.requestPlayerLoopUpdateWhileWaiting ||
            !observation.repaintSceneViewWhileWaiting ||
            observation.sameCallStackIsFinishedRenderingCheckIsSufficient)
            throw new InvalidOperationException(
                "Reflection-probe completion must be observed on later EditorApplication.update callbacks with the 30s fail-closed wait policy; same-call-stack completion is not sufficient.");

        Debug.Log("Reflection-probe capture synchronization contract valid: later-Editor-update completion is mandatory. Visual Fidelity remains render-evidence dependent.");
    }

    [MenuItem("NewTown/QA/Validate Latest Reflection Probe Refresh Receipt")]
    public static void ValidateRuntimeReceipt()
    {
        ValidateContractConfigOnly();

        string absolute = AbsolutePath(ReceiptPath);
        if (!File.Exists(absolute))
            throw new InvalidOperationException(
                "No Unity-generated reflection probe refresh receipt exists. Native-4K reflection evidence is not synchronized yet.");

        QualityBlockReflectionProbeRefreshReceipt receipt =
            JsonUtility.FromJson<QualityBlockReflectionProbeRefreshReceipt>(File.ReadAllText(absolute));
        if (receipt == null)
            throw new InvalidOperationException("Reflection probe refresh receipt is unreadable.");
        if (receipt.schemaVersion != "1.0")
            throw new InvalidOperationException($"Unexpected reflection probe refresh receipt schema: {receipt.schemaVersion}");
        if (receipt.unityVersion != Application.unityVersion)
            throw new InvalidOperationException(
                $"Reflection receipt Unity version '{receipt.unityVersion}' does not match the running editor '{Application.unityVersion}'.");
        if (receipt.scenePath != ScenePath)
            throw new InvalidOperationException($"Reflection receipt scene mismatch: {receipt.scenePath}");
        if (!receipt.allFinishedBeforeBenchmarkCapture ||
            receipt.requestedProbeCount != RequiredProbeCount || receipt.completedProbeCount != RequiredProbeCount)
            throw new InvalidOperationException(
                $"Reflection receipt does not prove all probes completed: requested={receipt.requestedProbeCount}, completed={receipt.completedProbeCount}, allFinished={receipt.allFinishedBeforeBenchmarkCapture}.");
        if (receipt.probes == null || receipt.probes.Length != RequiredProbeCount)
            throw new InvalidOperationException("Reflection receipt probe record count is invalid.");

        string[] actualNames = receipt.probes.Select(x => x.name).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        if (!actualNames.SequenceEqual(RequiredProbeNames.OrderBy(x => x, StringComparer.Ordinal), StringComparer.Ordinal))
            throw new InvalidOperationException("Reflection receipt probe identities do not match the benchmark contract.");

        foreach (QualityBlockReflectionProbeRefreshRecord probe in receipt.probes)
        {
            if (probe.renderId < 0 || !probe.finished || !probe.textureReady)
                throw new InvalidOperationException(
                    $"Reflection receipt contains an unproven probe: {probe.name}, renderId={probe.renderId}, finished={probe.finished}, textureReady={probe.textureReady}.");
            if (probe.expectedResolution != RequiredResolution ||
                probe.actualWidth != RequiredResolution || probe.actualHeight != RequiredResolution ||
                probe.textureDimension != "Cube")
                throw new InvalidOperationException(
                    $"Reflection receipt texture state invalid for {probe.name}: expected={probe.expectedResolution}, actual={probe.actualWidth}x{probe.actualHeight}, dimension={probe.textureDimension}.");
            if (!probe.hdr || probe.timeSlicingMode != "NoTimeSlicing" || probe.refreshMode != "ViaScripting")
                throw new InvalidOperationException(
                    $"Reflection receipt render policy invalid for {probe.name}: hdr={probe.hdr}, timeSlicing={probe.timeSlicingMode}, refresh={probe.refreshMode}.");
        }

        if (!DateTime.TryParse(receipt.generatedUtc, null,
                System.Globalization.DateTimeStyles.RoundtripKind, out DateTime generatedUtc))
            throw new InvalidOperationException("Reflection receipt generatedUtc is invalid.");
        if (generatedUtc > DateTime.UtcNow.AddMinutes(1))
            throw new InvalidOperationException("Reflection receipt timestamp is implausibly in the future.");

        Debug.Log(
            $"Reflection probe refresh receipt valid: {RequiredProbeCount}/{RequiredProbeCount} current-Unity 512px HDR cubemaps were proven complete before benchmark still capture. No visual points awarded.");
    }

    private static string AbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    [Serializable]
    private sealed class ReflectionProbeSyncContract
    {
        public string schemaVersion;
        public RequiredSceneState requiredSceneState;
        public CompletionObservation completionObservation;
    }

    [Serializable]
    private sealed class RequiredSceneState
    {
        public string probeRoot;
        public int probeCount;
        public string[] probeNames;
        public string mode;
        public string refreshMode;
        public string timeSlicingMode;
        public int resolution;
        public string textureDimension;
        public bool hdrRequired;
        public bool boxProjectionRequired;
    }

    [Serializable]
    private sealed class CompletionObservation
    {
        public string mechanism;
        public bool requireLaterEditorUpdate;
        public int timeoutSeconds;
        public bool requestPlayerLoopUpdateWhileWaiting;
        public bool repaintSceneViewWhileWaiting;
        public bool sameCallStackIsFinishedRenderingCheckIsSufficient;
    }
}
