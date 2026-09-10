using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Single runner entry point for the first real Unity verification session. The packet builds and
/// persists the benchmark, proves all source/runtime construction gates, waits across Editor updates
/// for realtime reflection probes, then captures sealed native 4K still and temporal evidence.
/// It deliberately stops before Visual Fidelity scoring: actual pixels must be reviewed first.
/// </summary>
public static class QualityBlockNative4KReviewPacket
{
    [MenuItem("NewTown/QA/Prepare Complete Native 4K Review Packet")]
    public static void Prepare()
    {
        if (QualityBlockReflectionProbeAwaiter.IsRunning)
            throw new InvalidOperationException("The native-4K review packet is already waiting for reflection-probe completion.");

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
        QualityBlockStairTowerApertureInstallationQA.ValidateContractConfigOnly();
        QualityBlockBalconyConstructionInterfaceQA.ValidateContractConfigOnly();
        QualityBlockBalconyGuardrailInstallationQA.ValidateContractConfigOnly();
        QualityBlockAcOutdoorUnitInstallationQA.ValidateContractConfigOnly();
        QualityBlockRainwaterDownpipeInstallationQA.ValidateContractConfigOnly();
        QualityBlockFutonBalconyDrapeQA.ValidateContractConfigOnly();
        QualityBlockParkFurnitureSaveGate.ValidateContract();
        QualityBlockSlideAccessInstallationQA.ValidateContractConfigOnly();
        QualityBlockBenchSeatConstructionInterfaceQA.ValidateContractConfigOnly();
        QualityBlockTextureSamplingUpgrade.ValidateContractConfigOnly();
        QualityBlockPhysicalTexelDensityQA.ValidateContractConfigOnly();
        QualityBlockAlbedoLightingNeutralityQA.ValidateContractConfigOnly();
        QualityBlockTreeWoodyContinuityQA.ValidateContractConfigOnly();
        QualityBlockFoliagePhysicalityQA.ValidateContractConfigOnly();
        QualityBlockHdrTonemapRuntimeQA.ValidateContractConfigOnly();
        QualityBlockReflectionProbeCaptureSyncQA.ValidateContractConfigOnly();
        QualityBlock4KCapture.ValidateCaptureContract();

        // Build/save/reopen the highest-quality generated scene first. Subsequent installation passes
        // are deterministic corrections against that persisted state before reflection capture begins.
        QualityBlock4KCapture.PrepareSceneForSynchronizedCapture();
        QualityBlockSceneMaterialPhysicalityUpgrade.ApplyAndValidate();

        // The tree builder intentionally remains replaceable by authored slots. For generated fallbacks,
        // correct the source-observable segmented radius jumps and normalized bark-UV restarts only after
        // the benchmark rebuild/PBR generation has finished, then persist exactly that state for probes.
        QualityBlockTreeWoodyContinuityQA.ApplyAndPersist();
        QualityBlockTreeDetailUpgrade.ValidateOpenScene();
        QualityBlockTreeWoodyContinuityQA.ValidateOpenScene();
        QualityBlockFoliagePhysicalityQA.ValidateOpenScene();

        // Main apartment facade: true openings + physical metric UV field.
        QualityBlockFacadeApertureConstructionQA.ApplyAndPersist();
        QualityBlockFacadeAperturePhysicalUvQA.ApplyAndPersist();
        QualityBlockFacadeApertureConstructionQA.ValidateOpenScene();
        QualityBlockFacadeAperturePhysicalUvQA.ValidateOpenScene();

        // Projecting stair tower: replace the opaque render cuboid with a 0.22 m RC shell around five
        // rough openings, then make shell returns butt-jointed instead of coplanar overlaps.
        QualityBlockStairTowerApertureInstallationQA.ApplyAndPersist();
        QualityBlockStairTowerShellJointQA.ApplyAndPersist();
        QualityBlockStairTowerApertureInstallationQA.ValidateOpenScene();
        QualityBlockStairTowerShellJointQA.ValidateOpenScene();

        // Establish the slab/base-plate/anchor relationship while the original seven Rail_* renderer
        // datums still exist. Then replace those benchmark-visible stock Cube rails with a period-plausible
        // 1.10 m vertical-lattice assembly. Its final-state QA takes over the same fascia/base/anchor checks.
        QualityBlockBalconyConstructionInterfaceQA.ApplyAndPersist();
        QualityBlockBalconyConstructionInterfaceQA.ValidateOpenScene();
        QualityBlockBalconyGuardrailInstallationQA.ApplyAndPersist();
        QualityBlockBalconyGuardrailInstallationQA.ValidateOpenScene();

        // Remaining building construction interfaces. Futon drapes deliberately run after guardrail
        // reconstruction so the final 1.10 m top rail is their physical installation datum.
        QualityBlockAcOutdoorUnitInstallationQA.ApplyAndPersist();
        QualityBlockAcOutdoorUnitInstallationQA.ValidateOpenScene();
        QualityBlockRainwaterDownpipeInstallationQA.ApplyAndPersist();
        QualityBlockRainwaterDownpipeInstallationQA.ValidateOpenScene();
        QualityBlockFutonBalconyDrapeQA.ApplyAndPersist();
        QualityBlockFutonBalconyDrapeQA.ValidateOpenScene();

        // Park/street furniture, vegetation continuity and texture/material/primitive preflight.
        QualityBlockParkFurnitureUpgrade.ValidateOpenScene();
        QualityBlockParkFurniturePhysicalRefinement.ValidateOpenScene();
        QualityBlockParkFurnitureLodRebind.ValidateOpenScene();
        QualityBlockSlideAccessInstallationQA.ValidateOpenScene();
        QualityBlockParkFurnitureMicrodetailUpgrade.Validate();
        QualityBlockBenchSeatConstructionInterfaceQA.ValidateOpenScene();
        QualityBlockTreeWoodyContinuityQA.ValidateOpenScene();
        QualityBlockFoliagePhysicalityQA.ValidateOpenScene();
        QualityBlockPhysicalTexelDensityQA.ValidateOpenScene();
        QualityBlockAlbedoLightingNeutralityQA.ValidateGeneratedBaseAlbedos();
        QualityBlockSceneMetadataCoverageQA.ValidateOpenScene();
        QualityBlockBenchmarkPrimitiveExposureQA.ValidateOpenScene();

        // Realtime probes are rendered only after final persisted geometry/material state exists.
        QualityBlockReflectionProbeAwaiter.Begin(FinishAfterReflectionSynchronization);

        Debug.Log(
            "Native-4K review packet entered reflection synchronization after final material binding, corrected continuous tree taper/metric bark UVs, " +
            "true facade/stair apertures, corrected balcony slab/base interfaces, 30 reconstructed four-LOD vertical-lattice guardrails, " +
            "AC/rainwater interfaces and two rail-bound textile drapes. Visual Fidelity remains UNSCORED.");
    }

    private static void FinishAfterReflectionSynchronization()
    {
        // Immediately before Camera.Render, re-prove synchronized probes and final construction state.
        // Do not call the legacy-post-dependent balcony-interface validator here: generated guardrail QA
        // rechecks its fascia/base/anchor invariants after intentionally disabling the old Rail_* renderers.
        QualityBlockReflectionProbeCaptureSyncQA.ValidateRuntimeReceipt();
        QualityBlockReflectionProbeAwaiter.ValidateLatestWaitProof();
        QualityBlockFacadeApertureConstructionQA.ValidateOpenScene();
        QualityBlockFacadeAperturePhysicalUvQA.ValidateOpenScene();
        QualityBlockStairTowerApertureInstallationQA.ValidateOpenScene();
        QualityBlockStairTowerShellJointQA.ValidateOpenScene();
        QualityBlockBalconyGuardrailInstallationQA.ValidateOpenScene();
        QualityBlockAcOutdoorUnitInstallationQA.ValidateOpenScene();
        QualityBlockRainwaterDownpipeInstallationQA.ValidateOpenScene();
        QualityBlockFutonBalconyDrapeQA.ValidateOpenScene();
        QualityBlockParkFurnitureUpgrade.ValidateOpenScene();
        QualityBlockParkFurniturePhysicalRefinement.ValidateOpenScene();
        QualityBlockParkFurnitureLodRebind.ValidateOpenScene();
        QualityBlockSlideAccessInstallationQA.ValidateOpenScene();
        QualityBlockBenchSeatConstructionInterfaceQA.ValidateOpenScene();
        QualityBlockTreeWoodyContinuityQA.ValidateOpenScene();
        QualityBlockFoliagePhysicalityQA.ValidateOpenScene();
        QualityBlockSceneMetadataCoverageQA.ValidateOpenScene();
        QualityBlockBenchmarkPrimitiveExposureQA.ValidateOpenScene();

        QualityBlock4KCapture.CapturePreparedSceneAfterProbeSync();
        QualityBlockBenchmarkObservabilityQA.ExtractPeriodAuthenticityCropsFromExistingFrames();
        QualityBlockRenderEvidenceProvenanceQA.SealCurrentCapture();
        QualityBlockRenderEvidenceProvenanceQA.WriteBoundEvidenceTemplate();
        AssetDatabase.Refresh();

        // Seal-to-temporal invariants. No rebuilding/reopening is allowed here: temporal evidence must
        // share the exact already-proven still geometry/material/reflection state.
        QualityBlockReflectionProbeCaptureSyncQA.ValidateRuntimeReceipt();
        QualityBlockReflectionProbeAwaiter.ValidateLatestWaitProof();
        QualityBlockStructuralSurfaceRefinement.ValidateOpenScene();
        QualityBlockFacadeApertureConstructionQA.ValidateOpenScene();
        QualityBlockFacadeAperturePhysicalUvQA.ValidateOpenScene();
        QualityBlockStairTowerApertureInstallationQA.ValidateOpenScene();
        QualityBlockStairTowerShellJointQA.ValidateOpenScene();
        QualityBlockBalconyGuardrailInstallationQA.ValidateOpenScene();
        QualityBlockAcOutdoorUnitInstallationQA.ValidateOpenScene();
        QualityBlockRainwaterDownpipeInstallationQA.ValidateOpenScene();
        QualityBlockFutonBalconyDrapeQA.ValidateOpenScene();
        QualityBlockParkFurnitureUpgrade.ValidateOpenScene();
        QualityBlockParkFurniturePhysicalRefinement.ValidateOpenScene();
        QualityBlockParkFurnitureLodRebind.ValidateOpenScene();
        QualityBlockSlideAccessInstallationQA.ValidateOpenScene();
        QualityBlockParkFurnitureMicrodetailUpgrade.Validate();
        QualityBlockBenchSeatConstructionInterfaceQA.ValidateOpenScene();
        QualityBlockTreeWoodyContinuityQA.ValidateOpenScene();
        QualityBlockBenchmarkPrimitiveExposureQA.ValidateOpenScene();
        QualityBlockSceneMaterialPhysicalityUpgrade.ValidateOpenScene();
        QualityBlockSceneMetadataCoverageQA.ValidateOpenScene();
        QualityBlockTextureSamplingUpgrade.ValidateOpenScene();
        QualityBlockPhysicalTexelDensityQA.ValidateOpenScene();
        QualityBlockAlbedoLightingNeutralityQA.ValidateGeneratedBaseAlbedos();
        QualityBlockFoliagePhysicalityQA.ValidateOpenScene();
        QualityBlockSceneRepetitionQA.ValidateOpenScene();

        QualityBlockPreparedTemporalCapture.CaptureAndSealPreparedScene();
        QualityBlockPreparedTemporalCapture.ValidateLatestPreparedBinding();

        QualityBlockRenderedImageDiagnostics.AnalyzeExistingCapture();
        QualityBlockTemporalDiagnostics.AnalyzeLatestEvidence();
        AssetDatabase.Refresh();

        Debug.Log(
            "Complete native-4K review packet prepared: sealed hero/oblique/grazing 3840x2160 stills and 100% crops plus bound temporal evidence. " +
            "Generated fallback trees retain continuous woody taper, metric non-restarting bark UVs and corrected meshes through retained LOD proxies; " +
            "legacy stock balcony RailTop_*/Rail_* renderers are excluded from evidence and replaced by 30 dense four-LOD guardrails; the two closed-volume futon drapes resolve that final rail datum before still and temporal capture. " +
            "Visual Fidelity remains UNSCORED until the actual pixels are manually reviewed against the locked 100-point gate.");
    }
}
