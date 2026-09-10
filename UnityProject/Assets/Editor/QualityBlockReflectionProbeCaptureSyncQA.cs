using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Machine-enforced integrity checks for realtime reflection-probe state used by native-4K evidence.
/// The production review packet writes the runtime receipt only after QualityBlockReflectionProbeAwaiter
/// has observed completion on later Editor updates. Schema 1.1 additionally requires one SHA-256-identical
/// physical sun/sky/ambient/shadow state across probe request, completion and immediate pre-still review.
/// Passing this QA proves synchronization/coherence only; it never awards Visual Fidelity points.
/// </summary>
public static class QualityBlockReflectionProbeCaptureSyncQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/reflection_probe_capture_sync_contract.json";
    private const string ReceiptPath = "Assets/QA/reflection_probe_refresh_receipt.json";
    private const string WaitProofPath = "Assets/QA/reflection_probe_async_wait_receipt.json";
    private const int RequiredProbeCount = 2;
    private const int RequiredResolution = 512;
    private const int RequiredTimeoutSeconds = 30;
    private const int RequiredMinimumEditorPollCount = 1;
    private const int RequiredMaximumProofAgeMinutes = 2;
    private const string ContractSchema = "1.1";
    private const string WaitProofSchema = "1.1";

    private static readonly string[] RequiredProbeNames =
    {
        "ReflectionProbe_DanchiFacade",
        "ReflectionProbe_ParkGround",
    };

    private static readonly string[] RequiredWaitProofFields =
    {
        "generatedUtc",
        "unityVersion",
        "scenePath",
        "observationMechanism",
        "editorPollCount",
        "elapsedSeconds",
        "timeoutSeconds",
        "renderIds[]",
        "allFinishedAndTextureReady",
        "reflectionReceiptSha256",
        "lightingFingerprintAlgorithm",
        "requestLightingStateSha256",
        "completionLightingStateSha256",
        "lightingStateStableAcrossProbeRender",
    };

    private static readonly string[] RequiredLightingAbortReasons =
    {
        "solar_shadow_contract_invalid_before_probe_request",
        "lighting_state_changed_during_probe_render",
        "ambient_probe_changed_during_probe_render",
        "lighting_state_changed_between_probe_completion_and_still_capture",
    };

    [MenuItem("NewTown/QA/Validate Reflection Probe Capture Sync Contract")]
    public static void ValidateContractConfigOnly()
    {
        string absolute = AbsolutePath(ContractPath);
        if (!File.Exists(absolute))
            throw new InvalidOperationException($"Reflection-probe capture synchronization contract missing: {ContractPath}");

        ReflectionProbeSyncContract contract = JsonUtility.FromJson<ReflectionProbeSyncContract>(File.ReadAllText(absolute));
        if (contract == null || contract.schemaVersion != ContractSchema)
            throw new InvalidOperationException($"Reflection-probe synchronization contract schema must remain {ContractSchema}.");
        if (contract.status != "PENDING_REAL_UNITY_4K_RENDER")
            throw new InvalidOperationException("Reflection-probe synchronization contract must remain render-pending until actual 4K evidence is reviewed.");
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

        LightingStateBinding lighting = contract.lightingStateBinding;
        if (lighting == null)
            throw new InvalidOperationException("Reflection-probe synchronization contract lightingStateBinding is missing.");
        if (lighting.validator != "QualityBlockSolarShadowCaptureCoherenceQA.ValidateOpenScene" ||
            lighting.fingerprintImplementation != "QualityBlockReflectionLightingStateFingerprint.BuildCurrentSha256" ||
            lighting.fingerprintAlgorithm != QualityBlockReflectionLightingStateFingerprint.Algorithm ||
            !lighting.validateBeforeProbeRequest ||
            !lighting.validateAfterProbeCompletion ||
            !lighting.validateImmediatelyBeforeStillCapture ||
            !lighting.requireRequestCompletionMatch ||
            !lighting.requireCompletionStillMatch ||
            !lighting.includeSummerSunTransformAndPbrLightState ||
            !lighting.includeProceduralSkyMaterialState ||
            !lighting.includeAmbientProbeCoefficients ||
            !lighting.includeRenderSettingsReflectionAndFogState ||
            !lighting.includeGlobalShadowAndLodSettings ||
            !lighting.actualRenderRequiredForVisualPoints)
            throw new InvalidOperationException(
                "Reflection lighting-state binding was weakened. Probe request, completion and immediate pre-still state must remain SHA-256-identical across sun, sky, ambient SH, reflection/fog and shadow/LOD state.");

        AsyncWaitProofConfig waitProof = contract.asyncWaitProof;
        if (waitProof == null)
            throw new InvalidOperationException("Reflection-probe synchronization contract asyncWaitProof is missing.");
        if (waitProof.path != WaitProofPath ||
            waitProof.schemaVersion != WaitProofSchema ||
            waitProof.requiredObservationMechanism != "EditorApplication.update" ||
            waitProof.minimumEditorPollCount != RequiredMinimumEditorPollCount ||
            waitProof.maximumAgeMinutesAtStillCapture != RequiredMaximumProofAgeMinutes ||
            waitProof.bindsRuntimeReceiptWith != "SHA-256")
            throw new InvalidOperationException(
                "Reflection async-wait proof contract drifted; the 4K gate requires schema-1.1 later-Editor polling, <=2 minute freshness, receipt SHA-256 binding and lighting-state hashes.");
        if (waitProof.requiredFields == null ||
            RequiredWaitProofFields.Any(required => !waitProof.requiredFields.Contains(required)))
            throw new InvalidOperationException("Reflection async-wait proof requiredFields no longer contains every render-completion and lighting-coherence field.");

        if (contract.criticalFailurePolicy == null || contract.criticalFailurePolicy.captureMustAbortWhen == null ||
            RequiredLightingAbortReasons.Any(required => !contract.criticalFailurePolicy.captureMustAbortWhen.Contains(required)))
            throw new InvalidOperationException("Reflection critical-failure policy no longer fails closed on solar/ambient lighting drift.");

        Debug.Log(
            "Reflection-probe capture synchronization contract valid: later-Editor completion, receipt hash binding and request/completion/pre-still physical-lighting SHA-256 coherence are mandatory. Visual Fidelity remains render-evidence dependent.");
    }

    [MenuItem("NewTown/QA/Validate Latest Reflection Probe Refresh Receipt")]
    public static void ValidateRuntimeReceipt()
    {
        ValidateContractConfigOnly();

        // A completion receipt is only meaningful while the current scene still satisfies the same
        // physical sun/sky contract. The stronger request/completion/current hash identity is checked
        // by QualityBlockReflectionProbeAwaiter.ValidateLatestWaitProof immediately before stills.
        QualityBlockSolarShadowCaptureCoherenceQA.ValidateOpenScene();
        QualityBlockEnvironmentLightingUpgrade.ValidateOpenScene();

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
            $"Reflection probe refresh receipt valid: {RequiredProbeCount}/{RequiredProbeCount} current-Unity 512px HDR cubemaps were proven complete before benchmark still capture. Lighting-state identity is additionally required by the schema-1.1 async proof. No visual points awarded.");
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
        public string status;
        public RequiredSceneState requiredSceneState;
        public CompletionObservation completionObservation;
        public LightingStateBinding lightingStateBinding;
        public AsyncWaitProofConfig asyncWaitProof;
        public CriticalFailurePolicy criticalFailurePolicy;
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

    [Serializable]
    private sealed class LightingStateBinding
    {
        public string validator;
        public string fingerprintImplementation;
        public string fingerprintAlgorithm;
        public bool validateBeforeProbeRequest;
        public bool validateAfterProbeCompletion;
        public bool validateImmediatelyBeforeStillCapture;
        public bool requireRequestCompletionMatch;
        public bool requireCompletionStillMatch;
        public bool includeSummerSunTransformAndPbrLightState;
        public bool includeProceduralSkyMaterialState;
        public bool includeAmbientProbeCoefficients;
        public bool includeRenderSettingsReflectionAndFogState;
        public bool includeGlobalShadowAndLodSettings;
        public bool actualRenderRequiredForVisualPoints;
    }

    [Serializable]
    private sealed class AsyncWaitProofConfig
    {
        public string path;
        public string schemaVersion;
        public string requiredObservationMechanism;
        public int minimumEditorPollCount;
        public int maximumAgeMinutesAtStillCapture;
        public string bindsRuntimeReceiptWith;
        public string[] requiredFields;
    }

    [Serializable]
    private sealed class CriticalFailurePolicy
    {
        public string[] captureMustAbortWhen;
        public string[] mapsToVisualGateCriticalDefects;
    }
}
