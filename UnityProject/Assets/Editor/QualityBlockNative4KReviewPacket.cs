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
        QualityBlockFacadeApertureConstructionQA.ValidateContractConfigOnly();
        QualityBlockFacadeAperturePhysicalUvQA.ValidateContractConfigOnly();
        QualityBlockBalconyConstructionInterfaceQA.ValidateContractConfigOnly();
        QualityBlockAcOutdoorUnitInstallationQA.ValidateContractConfigOnly();
        QualityBlockRainwaterDownpipeInstallationQA.ValidateContractConfigOnly();
        QualityBlockParkFurnitureSaveGate.ValidateContract();
        QualityBlockSlideAccessInstallationQA.ValidateContractConfigOnly();
        QualityBlockBenchSeatConstructionInterfaceQA.ValidateContractConfigOnly();
        QualityBlockTextureSamplingUpgrade.ValidateContractConfigOnly();
        QualityBlockPhysicalTexelDensityQA.ValidateContractConfigOnly();
        QualityBlockAlbedoLightingNeutralityQA.ValidateContractConfigOnly();
        QualityBlockFoliagePhysicalityQA.ValidateContractConfigOnly();
        QualityBlockHdrTonemapRuntimeQA.ValidateContractConfigOnly();
        QualityBlockReflectionProbeCaptureSyncQA.ValidateContractConfigOnly();
        QualityBlock4KCapture.ValidateCaptureContract();

        // Build/save/reopen first. The original generated MainBlock is one opaque cube, so apartment
        // glazing placed at/behind its front plane is not a physically valid opening. Reconstruct the
        // render shell around thirty real rough openings, keep the original collider only for gameplay,
        // seat perimeter seals around the existing aluminum sash/glass stack, and bake building-local
        // world-aligned physical UVs before any reflection/capture work. This prevents both opaque-wall
        // occlusion behind glass and the equally unacceptable workaround of restarting the same concrete
        // texture patch on every repeated wall module. Generated balcony and outdoor-AC construction
        // interfaces are then corrected against the actual persisted slab top. The full-height rainwater
        // leader is also reconstructed as a manufactured PVC-U drainage assembly with explicit hollow socket
        // joints, wall bands/standoffs/anchors, roof-line offset and a hollow ground receiver instead of a
        // floating cylinder with detached hardware. The benchmark-save gate also rebuilds park furniture,
        // reconstructs the approximately two-metre slide's missing climbable access stair plus chute-head and
        // runout support load paths before LOD renderer rebinding, and corrects the bench load path so timber
        // slats seat on the steel bearer, the bearer seats on both precast supports, and LOD0 bolt heads sit
        // over actual timber rather than floating in slat gaps. Low physical texel density is corrected at
        // texture/UV/tiling level. Broad illumination patterns found in generated base albedo are corrected in
        // the source/PBR split; they are never hidden by sharpening, grading, painted highlights or by weakening
        // the native 4K gate. Any visibly significant active Unity stock solid in the exact hero/oblique/grazing
        // framing fails here and must be reconstructed.
        QualityBlock4KCapture.PrepareSceneForSynchronizedCapture();

        // Apply the actual scene material bindings before any construction QA that consumes them. This is
        // fail-closed: the rainwater reconstruction must not depend on a material asset that is only created
        // by a later review-stage validator, and no component may enter reflection capture with stale
        // concrete/metal assignments.
        QualityBlockSceneMaterialPhysicalityUpgrade.ApplyAndValidate();

        QualityBlockFacadeApertureConstructionQA.ApplyAndPersist();
        QualityBlockFacadeAperturePhysicalUvQA.ApplyAndPersist();
        QualityBlockFacadeApertureConstructionQA.ValidateOpenScene();
        QualityBlockFacadeAperturePhysicalUvQA.ValidateOpenScene();
        QualityBlockBalconyConstructionInterfaceQA.ApplyAndPersist();
        QualityBlockBalconyConstructionInterfaceQA.ValidateOpenScene();
        QualityBlockAcOutdoorUnitInstallationQA.ApplyAndPersist();
        QualityBlockAcOutdoorUnitInstallationQA.ValidateOpenScene();
        QualityBlockRainwaterDownpipeInstallationQA.ApplyAndPersist();
        QualityBlockRainwaterDownpipeInstallationQA.ValidateOpenScene();
        QualityBlockParkFurnitureUpgrade.ValidateOpenScene();
        QualityBlockParkFurniturePhysicalRefinement.ValidateOpenScene();
        QualityBlockParkFurnitureLodRebind.ValidateOpenScene();
        QualityBlockSlideAccessInstallationQA.ValidateOpenScene();
        QualityBlockParkFurnitureMicrodetailUpgrade.Validate();
        QualityBlockBenchSeatConstructionInterfaceQA.ValidateOpenScene();
        QualityBlockPhysicalTexelDensityQA.ValidateOpenScene();
        QualityBlockAlbedoLightingNeutralityQA.ValidateGeneratedBaseAlbedos();
        QualityBlockBenchmarkPrimitiveExposureQA.ValidateOpenScene();

        // Realtime probes are rendered only after the final persisted geometry, materials, foliage,
        // weathering and light environment exist. The awaiter yields back to the Editor until
        // IsFinishedRendering(RenderID) and the actual 512px Cube textures are both proven.
        QualityBlockReflectionProbeAwaiter.Begin(FinishAfterReflectionSynchronization);

        Debug.Log(
            "Native-4K review packet entered reflection synchronization after the final prepared scene passed actual material binding/physicality, real apartment facade-aperture reconstruction, world-aligned physical facade UV anti-repeat QA, balcony rail/slab and outdoor-AC support/service-line correction, physically installed hollow-interface rainwater downpipe reconstruction, manufactured slide access/head/runout load-path QA, manufactured bench slat/bearer/support/bolt load-path QA, physical texel-density, generated base-albedo lighting-neutrality, and exact-framing stock-solid primitive exposure source preflight. " +
            "Still and temporal capture are deferred until later Editor updates prove both realtime probe RenderIDs complete. Visual Fidelity remains UNSCORED.");
    }

    private static void FinishAfterReflectionSynchronization()
    {
        // Still path: the reflection receipt is accepted only when it is SHA-256-bound to a fresh
        // async-wait proof produced after at least one later EditorApplication.update callback.
        // CapturePreparedSceneAfterProbeSync must not rebuild the scene, otherwise the just-proven
        // cubemaps would become stale relative to the benchmark geometry/material state. Construction
        // interfaces and the real facade opening/UV state are revalidated here, after probe completion
        // and immediately before Camera.Render. The capture method also owns the HDR-tonemap telemetry
        // reset and fail-closed runtime proof so alternate callers cannot bypass display-transform evidence.
        QualityBlockReflectionProbeCaptureSyncQA.ValidateRuntimeReceipt();
        QualityBlockReflectionProbeAwaiter.ValidateLatestWaitProof();
        QualityBlockFacadeApertureConstructionQA.ValidateOpenScene();
        QualityBlockFacadeAperturePhysicalUvQA.ValidateOpenScene();
        QualityBlockBalconyConstructionInterfaceQA.ValidateOpenScene();
        QualityBlockAcOutdoorUnitInstallationQA.ValidateOpenScene();
        QualityBlockRainwaterDownpipeInstallationQA.ValidateOpenScene();
        QualityBlockParkFurnitureUpgrade.ValidateOpenScene();
        QualityBlockParkFurniturePhysicalRefinement.ValidateOpenScene();
        QualityBlockParkFurnitureLodRebind.ValidateOpenScene();
        QualityBlockSlideAccessInstallationQA.ValidateOpenScene();
        QualityBlockBenchSeatConstructionInterfaceQA.ValidateOpenScene();
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
        QualityBlockFacadeApertureConstructionQA.ValidateOpenScene();
        QualityBlockFacadeAperturePhysicalUvQA.ValidateOpenScene();
        QualityBlockBalconyConstructionInterfaceQA.ValidateOpenScene();
        QualityBlockAcOutdoorUnitInstallationQA.ValidateOpenScene();
        QualityBlockRainwaterDownpipeInstallationQA.ValidateOpenScene();
        QualityBlockParkFurnitureUpgrade.ValidateOpenScene();
        QualityBlockParkFurniturePhysicalRefinement.ValidateOpenScene();
        QualityBlockParkFurnitureLodRebind.ValidateOpenScene();
        QualityBlockSlideAccessInstallationQA.ValidateOpenScene();
        QualityBlockParkFurnitureMicrodetailUpgrade.Validate();
        QualityBlockBenchSeatConstructionInterfaceQA.ValidateOpenScene();
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
            "actual scene material bindings normalized and validated before construction correction/reflection capture, " +
            "the opaque generated MainBlock render surface replaced by a persisted shell with thirty true apartment rough openings while the gameplay collider remains, sash/glass planes verified recessed inside the 0.22 m shell and four perimeter seal pieces required per opening, " +
            "aperture-shell concrete re-UVed from absolute Danchi-local metres at a 2.4 m macro scale with distinct repeated-cell phase and 0.22 m detail-normal scale so module repetition is not hidden by arbitrary texture restarts, " +
            "generated balcony slab fascia/material and rail base-plate/bolt/lower-rail/bracket interfaces corrected from the persisted slab-top plane, " +
            "generated outdoor AC units reconstructed as slab -> plate -> foot -> supported chassis stacks with moved casing detail, continuous paired refrigerant lines and a drain outlet above the slab, all revalidated before/after still capture, " +
            "the camera-right rainwater leader rebuilt as a 75 mm-class rough dielectric PVC-U macro body with hollow manufactured joint sleeves, eight hollow wall bands, physically continuous standoffs/anchor plates, roof-line offset/hollow collar and inserted hollow ground receiver, with small hardware participating in the existing four-level Danchi LOD policy while the macro pipe silhouette stays continuously rendered, " +
            "the benchmark-close municipal slide given a ten-tread 180 mm-deep access stair with 190 mm nominal rises at about 50.9 degrees, paired tangent-seated stringers, grade foot plates, access handrails bridged into the platform guard, a frame-tied chute-head bearing and a ground-supported runout bearing; the stair/support silhouette persists through all four LODs and only sub-pixel fastener heads disappear after LOD0, " +
            "the benchmark-close timber bench source load path corrected so five slats physically seat on the steel bearer, the bearer seats on both precast supports, LOD2/3 proxy undersides remain seated, and all four LOD0 bolt heads overlap outer timber slats by the locked installation range instead of floating in the gaps, " +
            "final persisted generated textured geometry proven above the conservative physical texel-density floor, generated baseline albedos checked for broad baked-lighting patterns, and exact benchmark projections checked for visibly significant active Unity stock solid primitive risk before reflection/capture work, " +
            "reflection cubemaps proven complete on later Editor updates before capture with a SHA-256-bound wait proof, retained structural geometry and actual material physicality validated, " +
            "all three native stills proven at runtime by the capture method itself to execute the filmic HDR-source to LDR-destination display transform with no fallback blit, " +
            "scene-wide construction/material metadata coverage, texture sampling, foliage dielectric constraints and fine+coarse anti-repetition preflight checked, " +
            "temporal probes captured from the same prepared scene without rebuild/reopen and SHA-256-bound to the persisted scene plus reflection completion/wait proofs, " +
            "and non-scoring still/temporal diagnostics generated for manual 100%-pixel review. Visual Fidelity remains UNSCORED until the exact evidence is reviewed and the evidence-bound 100-point gate is evaluated.");
    }
}
