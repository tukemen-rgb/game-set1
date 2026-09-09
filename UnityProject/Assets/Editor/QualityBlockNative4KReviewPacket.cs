using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Single runner entry point for the first real Unity verification session. Once a Unity editor/runner
/// is available, this command should be preferred over speculative source expansion. The still path
/// deliberately spans Editor updates while realtime reflection probes render; only after their returned
/// RenderIDs are proven finished does it capture/seal hero, oblique and grazing frames. The native capture
/// method itself then proves that all three stills actually traversed an HDR source -> filmic transform ->
/// LDR destination before they can be sealed. Temporal probes are captured from that exact prepared scene
/// without rebuilding/reopening, and diagnostic tools only triage the resulting evidence for manual review.
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
        QualityBlockPreparedTemporalCapture.ValidateContractConfigOnly();
        QualityBlockTemporalDiagnostics.ValidateContractConfigOnly();
        QualityBlockStructuralSurfaceSaveGate.ValidateContract();
        QualityBlockBenchmarkPrimitiveExposureQA.ValidateContractConfigOnly();
        QualityBlockSceneMaterialPhysicalityUpgrade.ValidateContract();
        QualityBlockSceneMetadataCoverageQA.ValidateContractConfigOnly();
        QualityBlockTextureSamplingUpgrade.ValidateContractConfigOnly();
        QualityBlockPhysicalTexelDensityQA.ValidateContractConfigOnly();
        QualityBlockAlbedoLightingNeutralityQA.ValidateContractConfigOnly();
        QualityBlockFoliagePhysicalityQA.ValidateContractConfigOnly();
        QualityBlockHdrTonemapRuntimeQA.ValidateContractConfigOnly();
        QualityBlockReflectionProbeCaptureSyncQA.ValidateContractConfigOnly();
        QualityBlock4KCapture.ValidateCaptureContract();

        // Build/save/reopen first. Source adequacy checks must run against this final persisted
        // renderer/material/mesh state before we spend a reflection render or write any benchmark pixels.
        // Low physical texel density is corrected at texture/UV/tiling level. Broad illumination patterns
        // found in generated base albedo are corrected in the source/PBR split; they are never hidden by
        // sharpening, grading, painted highlights or by weakening the native 4K gate. Any visibly significant
        // active Unity stock solid in the exact hero/oblique/grazing framing fails here as a primitive-placeholder
        // risk and must be reconstructed rather than renamed or threshold-exempted.
        QualityBlock4KCapture.PrepareSceneForSynchronizedCapture();
        QualityBlockPhysicalTexelDensityQA.ValidateOpenScene();
        QualityBlockAlbedoLightingNeutralityQA.ValidateGeneratedBaseAlbedos();
        QualityBlockBenchmarkPrimitiveExposureQA.ValidateOpenScene();

        // Realtime probes are rendered only after the final persisted geometry, materials, foliage,
        // weathering and light environment exist. The awaiter yields back to the Editor until
        // IsFinishedRendering(RenderID) and the actual 512px Cube textures are both proven.
        QualityBlockReflectionProbeAwaiter.Begin(FinishAfterReflectionSynchronization);

        Debug.Log(
            "Native-4K review packet entered reflection synchronization after the final prepared scene passed physical texel-density, generated base-albedo lighting-neutrality, and exact-framing stock-solid primitive exposure source preflight. " +
            "Still and temporal capture are deferred until later Editor updates prove both realtime probe RenderIDs complete. Visual Fidelity remains UNSCORED.");
    }

    private static void FinishAfterReflectionSynchronization()
    {
        // Still path: the reflection receipt is accepted only when it is SHA-256-bound to a fresh
        // async-wait proof produced after at least one later EditorApplication.update callback.
        // CapturePreparedSceneAfterProbeSync must not rebuild the scene, otherwise the just-proven
        // cubemaps would become stale relative to the benchmark geometry/material state. That capture
        // method also owns the HDR-tonemap telemetry reset and fail-closed runtime proof so alternate
        // callers cannot bypass the display-transform evidence requirement.
        QualityBlockReflectionProbeCaptureSyncQA.ValidateRuntimeReceipt();
        QualityBlockReflectionProbeAwaiter.ValidateLatestWaitProof();
        QualityBlockBenchmarkPrimitiveExposureQA.ValidateOpenScene();
        QualityBlock4KCapture.CapturePreparedSceneAfterProbeSync();

        QualityBlockBenchmarkObservabilityQA.ExtractPeriodAuthenticityCropsFromExistingFrames();
        QualityBlockRenderEvidenceProvenanceQA.SealCurrentCapture();
        QualityBlockRenderEvidenceProvenanceQA.WriteBoundEvidenceTemplate();
        AssetDatabase.Refresh();

        // Reconfirm source/runtime invariants after the exact still set has been written and sealed.
        // No build/apply/open operation is allowed below this point before temporal capture.
        QualityBlockReflectionProbeCaptureSyncQA.ValidateRuntimeReceipt();
        QualityBlockReflectionProbeAwaiter.ValidateLatestWaitProof();
        QualityBlockStructuralSurfaceRefinement.ValidateOpenScene();
        QualityBlockBenchmarkPrimitiveExposureQA.ValidateOpenScene();
        QualityBlockSceneMaterialPhysicalityUpgrade.ValidateOpenScene();
        QualityBlockSceneMetadataCoverageQA.ValidateOpenScene();
        QualityBlockTextureSamplingUpgrade.ValidateOpenScene();
        QualityBlockPhysicalTexelDensityQA.ValidateOpenScene();
        QualityBlockAlbedoLightingNeutralityQA.ValidateGeneratedBaseAlbedos();
        QualityBlockFoliagePhysicalityQA.ValidateOpenScene();
        QualityBlockSceneRepetitionQA.ValidateOpenScene();

        // Motion path must use the exact already-prepared scene/probe state. This explicitly replaces the
        // legacy temporal menu path that rebuilds/reopens and attempts an immediate same-call-stack probe refresh.
        QualityBlockPreparedTemporalCapture.CaptureAndSealPreparedScene();
        QualityBlockPreparedTemporalCapture.ValidateLatestPreparedBinding();

        // Objective triage only. Neither still nor temporal diagnostics can award visual points or clear
        // critical defects; they only direct the reviewer to the most informative exact evidence.
        QualityBlockRenderedImageDiagnostics.AnalyzeExistingCapture();
        QualityBlockTemporalDiagnostics.AnalyzeLatestEvidence();

        AssetDatabase.Refresh();
        Debug.Log(
            "Complete native-4K review packet prepared: sealed hero/oblique/grazing stills + 100% crops, " +
            "immutable 92/100 category/minimum/critical-defect gate integrity checked before capture, " +
            "final persisted generated textured geometry proven above the conservative physical texel-density floor, generated baseline albedos checked for broad baked-lighting patterns, and exact benchmark projections checked for visibly significant active Unity stock solid primitive risk before reflection/capture work, " +
            "reflection cubemaps proven complete on later Editor updates before capture with a SHA-256-bound wait proof, retained structural geometry and actual material physicality validated, " +
            "all three native stills proven at runtime by the capture method itself to execute the filmic HDR-source to LDR-destination display transform with no fallback blit, " +
            "scene-wide construction/material metadata coverage, texture sampling, foliage dielectric constraints and fine+coarse anti-repetition preflight checked, " +
            "temporal probes captured from the same prepared scene without rebuild/reopen and SHA-256-bound to the persisted scene plus reflection completion/wait proofs, " +
            "and non-scoring still/temporal diagnostics generated for manual 100%-pixel review. Visual Fidelity remains UNSCORED until the exact evidence is reviewed and the evidence-bound 100-point gate is evaluated.");
    }
}
