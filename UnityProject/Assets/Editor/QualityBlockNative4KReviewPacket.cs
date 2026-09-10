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
        QualityBlockAcOutdoorUnitInstallationQA.ValidateContractConfigOnly();
        QualityBlockRainwaterDownpipeInstallationQA.ValidateContractConfigOnly();
        QualityBlockFutonBalconyDrapeQA.ValidateContractConfigOnly();
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

        // Build/save/reopen the highest-quality generated scene first. Subsequent installation passes
        // are deterministic corrections against that persisted state before reflection capture begins.
        QualityBlock4KCapture.PrepareSceneForSynchronizedCapture();
        QualityBlockSceneMaterialPhysicalityUpgrade.ApplyAndValidate();

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

        // Remaining building construction interfaces. Futon drapes deliberately run after balcony
        // correction because the final rendered RailTop and slab bounds are their installation datums.
        QualityBlockBalconyConstructionInterfaceQA.ApplyAndPersist();
        QualityBlockBalconyConstructionInterfaceQA.ValidateOpenScene();
        QualityBlockAcOutdoorUnitInstallationQA.ApplyAndPersist();
        QualityBlockAcOutdoorUnitInstallationQA.ValidateOpenScene();
        QualityBlockRainwaterDownpipeInstallationQA.ApplyAndPersist();
        QualityBlockRainwaterDownpipeInstallationQA.ValidateOpenScene();
        QualityBlockFutonBalconyDrapeQA.ApplyAndPersist();
        QualityBlockFutonBalconyDrapeQA.ValidateOpenScene();

        // Park/street furniture, then texture/material/primitive preflight.
        QualityBlockParkFurnitureUpgrade.ValidateOpenScene();
        QualityBlockParkFurniturePhysicalRefinement.ValidateOpenScene();
        QualityBlockParkFurnitureLodRebind.ValidateOpenScene();
        QualityBlockSlideAccessInstallationQA.ValidateOpenScene();
        QualityBlockParkFurnitureMicrodetailUpgrade.Validate();
        QualityBlockBenchSeatConstructionInterfaceQA.ValidateOpenScene();
        QualityBlockPhysicalTexelDensityQA.ValidateOpenScene();
        QualityBlockAlbedoLightingNeutralityQA.ValidateGeneratedBaseAlbedos();
        QualityBlockSceneMetadataCoverageQA.ValidateOpenScene();
        QualityBlockBenchmarkPrimitiveExposureQA.ValidateOpenScene();

        // Realtime probes are rendered only after final persisted geometry/material state exists.
        QualityBlockReflectionProbeAwaiter.Begin(FinishAfterReflectionSynchronization);

        Debug.Log(
            "Native-4K review packet entered reflection synchronization after final material binding, apartment/stair true-aperture reconstruction, " +
            "balcony/AC/rainwater interfaces and two physical balcony futon rail-wrap drapes. Visual Fidelity remains UNSCORED.");
    }

    private static void FinishAfterReflectionSynchronization()
    {
        // Immediately before Camera.Render, re-prove synchronized probes and construction state that
        // can alter silhouette, contact, reflection or light transport.
        QualityBlockReflectionProbeCaptureSyncQA.ValidateRuntimeReceipt();
        QualityBlockReflectionProbeAwaiter.ValidateLatestWaitProof();
        QualityBlockFacadeApertureConstructionQA.ValidateOpenScene();
        QualityBlockFacadeAperturePhysicalUvQA.ValidateOpenScene();
        QualityBlockStairTowerApertureInstallationQA.ValidateOpenScene();
        QualityBlockStairTowerShellJointQA.ValidateOpenScene();
        QualityBlockBalconyConstructionInterfaceQA.ValidateOpenScene();
        QualityBlockAcOutdoorUnitInstallationQA.ValidateOpenScene();
        QualityBlockRainwaterDownpipeInstallationQA.ValidateOpenScene();
        QualityBlockFutonBalconyDrapeQA.ValidateOpenScene();
        QualityBlockParkFurnitureUpgrade.ValidateOpenScene();
        QualityBlockParkFurniturePhysicalRefinement.ValidateOpenScene();
        QualityBlockParkFurnitureLodRebind.ValidateOpenScene();
        QualityBlockSlideAccessInstallationQA.ValidateOpenScene();
        QualityBlockBenchSeatConstructionInterfaceQA.ValidateOpenScene();
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
        QualityBlockBalconyConstructionInterfaceQA.ValidateOpenScene();
        QualityBlockAcOutdoorUnitInstallationQA.ValidateOpenScene();
        QualityBlockRainwaterDownpipeInstallationQA.ValidateOpenScene();
        QualityBlockFutonBalconyDrapeQA.ValidateOpenScene();
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

        QualityBlockPreparedTemporalCapture.CaptureAndSealPreparedScene();
        QualityBlockPreparedTemporalCapture.ValidateLatestPreparedBinding();

        QualityBlockRenderedImageDiagnostics.AnalyzeExistingCapture();
        QualityBlockTemporalDiagnostics.AnalyzeLatestEvidence();
        AssetDatabase.Refresh();

        Debug.Log(
            "Complete native-4K review packet prepared: sealed hero/oblique/grazing 3840x2160 stills and 100% crops plus bound temporal evidence. " +
            "Legacy flat balcony Futon_* renderers are excluded from evidence and replaced by two closed-volume, non-identical, four-LOD textile drapes whose rail contact and slab clearance are revalidated before still and temporal capture. " +
            "Visual Fidelity remains UNSCORED until the actual pixels are manually reviewed against the locked 100-point gate.");
    }
}
