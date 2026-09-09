using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Single runner entry point for the first real Unity verification session. Once a Unity editor/runner
/// is available, this command should be preferred over speculative source expansion. The still path
/// deliberately spans Editor updates while realtime reflection probes render; only after their returned
/// RenderIDs are proven finished does it capture/seal hero, oblique and grazing frames. It then creates
/// objective display diagnostics and captures/seals the temporal shimmer/LOD probes.
///
/// The packet intentionally stops before Visual Fidelity scoring because a reviewer must inspect the
/// actual pixels and record evidence/deductions/corrective actions.
/// </summary>
public static class QualityBlockNative4KReviewPacket
{
    [MenuItem("NewTown/QA/Prepare Complete Native 4K Review Packet")]
    public static void Prepare()
    {
        if (QualityBlockReflectionProbeAwaiter.IsRunning)
            throw new InvalidOperationException("The native-4K review packet is already waiting for reflection-probe completion.");

        // Freeze the owner-approved 100-point geometry before any render work starts. A balanced 100-point
        // file is not sufficient if individual weights/minima or a critical defect were silently relaxed.
        QualityBlockVisualGateIntegrityQA.ValidateContract();
        QualityBlockVisualFidelityGate.ValidateGateConfig();
        QualityBlockShadowStabilityUpgrade.ValidateOpenScene();
        QualityBlockRenderedImageDiagnostics.ValidateContractConfigOnly();
        QualityBlockTemporalStabilityCapture.ValidateContractConfigOnly();
        QualityBlockStructuralSurfaceSaveGate.ValidateContract();
        QualityBlockSceneMaterialPhysicalityUpgrade.ValidateContract();
        QualityBlockSceneMetadataCoverageQA.ValidateContractConfigOnly();
        QualityBlockTextureSamplingUpgrade.ValidateContractConfigOnly();
        QualityBlockFoliagePhysicalityQA.ValidateContractConfigOnly();
        QualityBlockReflectionProbeCaptureSyncQA.ValidateContractConfigOnly();
        QualityBlock4KCapture.ValidateCaptureContract();

        // Build/save/reopen first. Realtime probes are rendered only after the final persisted geometry,
        // materials, foliage, weathering and light environment exist. The awaiter then yields back to the
        // Editor until IsFinishedRendering(RenderID) and the actual 512px Cube textures are both proven.
        QualityBlock4KCapture.PrepareSceneForSynchronizedCapture();
        QualityBlockReflectionProbeAwaiter.Begin(FinishAfterReflectionSynchronization);

        Debug.Log(
            "Native-4K review packet entered reflection synchronization. Still capture is intentionally deferred across Editor updates; " +
            "Visual Fidelity remains UNSCORED and no benchmark frame is accepted until both realtime probe RenderIDs are complete.");
    }

    private static void FinishAfterReflectionSynchronization()
    {
        // Still path: the reflection receipt is accepted only when it is SHA-256-bound to a fresh
        // async-wait proof produced after at least one later EditorApplication.update callback.
        // CapturePreparedSceneAfterProbeSync must not rebuild the scene, otherwise the just-proven
        // cubemaps would become stale relative to the benchmark geometry/material state.
        QualityBlockReflectionProbeCaptureSyncQA.ValidateRuntimeReceipt();
        QualityBlockReflectionProbeAwaiter.ValidateLatestWaitProof();
        QualityBlock4KCapture.CapturePreparedSceneAfterProbeSync();
        QualityBlockBenchmarkObservabilityQA.ExtractPeriodAuthenticityCropsFromExistingFrames();
        QualityBlockRenderEvidenceProvenanceQA.SealCurrentCapture();
        QualityBlockRenderEvidenceProvenanceQA.WriteBoundEvidenceTemplate();
        AssetDatabase.Refresh();

        // Reconfirm source/runtime invariants after the exact still set has been written and sealed.
        // Repetition QA runs here as well as on scene save: a fine exact hash alone is insufficient, so
        // generated trees/ecology must also pass the coarse near-clone signature before evidence review.
        QualityBlockReflectionProbeCaptureSyncQA.ValidateRuntimeReceipt();
        QualityBlockReflectionProbeAwaiter.ValidateLatestWaitProof();
        QualityBlockStructuralSurfaceRefinement.ValidateOpenScene();
        QualityBlockSceneMaterialPhysicalityUpgrade.ValidateOpenScene();
        QualityBlockSceneMetadataCoverageQA.ValidateOpenScene();
        QualityBlockTextureSamplingUpgrade.ValidateOpenScene();
        QualityBlockFoliagePhysicalityQA.ValidateOpenScene();
        QualityBlockSceneRepetitionQA.ValidateOpenScene();

        // Objective triage only. This cannot award Cinematic Image or Lighting points.
        QualityBlockRenderedImageDiagnostics.AnalyzeExistingCapture();

        // Motion path: native-4K subpixel grazing and LOD-walk sequences with sealed manifests.
        QualityBlockTemporalStabilityCapture.CaptureAndSeal();

        AssetDatabase.Refresh();
        Debug.Log(
            "Complete native-4K review packet prepared: sealed hero/oblique/grazing stills + 100% crops, " +
            "immutable 92/100 category/minimum/critical-defect gate integrity checked before capture, " +
            "reflection cubemaps proven complete on later Editor updates before capture with a SHA-256-bound wait proof, retained structural geometry and actual material physicality validated, " +
            "scene-wide construction/material metadata coverage, texture sampling, foliage dielectric constraints and fine+coarse anti-repetition preflight checked, " +
            "cinematic diagnostics generated, and temporal probes sealed. Visual Fidelity remains UNSCORED until the exact evidence is reviewed and " +
            "the evidence-bound 100-point gate is evaluated.");
    }
}
