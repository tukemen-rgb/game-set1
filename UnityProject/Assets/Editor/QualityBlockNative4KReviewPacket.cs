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
        QualityBlockNative4KCleanSessionPreflightQA.ValidateContractConfigOnly();
        QualityBlockSolarShadowCaptureCoherenceQA.ValidateContractConfigOnly();
        QualityBlockLightEvidencePurityQA.ValidateContractConfigOnly();
        QualityBlockRenderedImageDiagnostics.ValidateContractConfigOnly();
        QualityBlockRenderedRepetitionDiagnostics.ValidateContractConfigOnly();
        QualityBlockRenderedLightLeakDiagnostics.ValidateContractConfigOnly();
        QualityBlockTemporalStabilityCapture.ValidateContractConfigOnly();
        QualityBlockPreparedTemporalCapture.ValidateContractConfigOnly();
        QualityBlockTemporalRuntimeEvidenceGuard.ValidateContractConfigOnly();
        QualityBlockTemporalDiagnostics.ValidateContractConfigOnly();
        QualityBlockStructuralSurfaceSaveGate.ValidateContract();
        QualityBlockBenchmarkPrimitiveExposureQA.ValidateContractConfigOnly();
        QualityBlockSceneMaterialPhysicalityUpgrade.ValidateContract();
        QualityBlockSceneMetadataCoverageQA.ValidateContractConfigOnly();
        QualityBlockPeriodAuthenticityUpgrade.ValidateContractConfigOnly();
        QualityBlockFacadeApertureConstructionQA.ValidateContractConfigOnly();
        QualityBlockFacadeAperturePhysicalUvQA.ValidateContractConfigOnly();
        QualityBlockFacadeWeatheringOpticalRefinementQA.ValidateContractConfigOnly();
        QualityBlockStairTowerApertureInstallationQA.ValidateContractConfigOnly();
        QualityBlockBalconyConstructionInterfaceQA.ValidateContractConfigOnly();
        QualityBlockBalconyGuardrailInstallationQA.ValidateContractConfigOnly();
        QualityBlockBalconySeparationPanelFormalIntegrationQA.ValidateContractConfigOnly();
        QualityBlockAcOutdoorUnitInstallationQA.ValidateContractConfigOnly();
        QualityBlockAcFanPhysicalRefinement.ValidateContractConfigOnly();
        QualityBlockAcFanGuardRoundWireRefinement.ValidateContractConfigOnly();
        QualityBlockAcFanGuardMountInterfaceRefinement.ValidateContractConfigOnly();
        QualityBlockRainwaterDownpipeInstallationQA.ValidateContractConfigOnly();
        QualityBlockFutonBalconyDrapeQA.ValidateContractConfigOnly();
        QualityBlockGroundPathPlazaTerminationQA.ValidateContractConfigOnly();
        QualityBlockGroundWornPathBTransitionQA.ValidateContractConfigOnly();
        QualityBlockParkFurnitureSaveGate.ValidateContract();
        QualityBlockSlideAccessInstallationQA.ValidateContractConfigOnly();
        QualityBlockSlideChuteFabricationQA.ValidateContractConfigOnly();
        QualityBlockBenchSeatConstructionInterfaceQA.ValidateContractConfigOnly();
        QualityBlockParkLampInstallationQA.ValidateContractConfigOnly();
        QualityBlockTextureSamplingUpgrade.ValidateContractConfigOnly();
        QualityBlockPhysicalTexelDensityQA.ValidateContractConfigOnly();
        QualityBlockAlbedoLightingNeutralityQA.ValidateContractConfigOnly();
        QualityBlockTreeWoodyContinuityQA.ValidateContractConfigOnly();
        QualityBlockFoliagePhysicalityQA.ValidateContractConfigOnly();
        QualityBlockVegetationEcologyContractQA.Validate();
        QualityBlockVegetationRootZoneInterfaceQA.ValidateContractConfigOnly();
        QualityBlockGrassBladeNativePacketQA.ValidateContractConfigOnly();
        QualityBlockGrassBladeFormalPersistenceQA.ValidateContractConfigOnly();
        QualityBlockHdrTonemapRuntimeQA.ValidateContractConfigOnly();
        QualityBlockReflectionProbeCaptureSyncQA.ValidateContractConfigOnly();
        QualityBlock4KCapture.ValidateCaptureContract();

        QualityBlock4KCapture.PrepareSceneForSynchronizedCapture();
        QualityBlockNative4KCleanSessionPreflightQA.ValidatePreparedBaseline();
        QualityBlockPeriodAuthenticityUpgrade.ApplyToOpenScene();
        QualityBlockPeriodAuthenticityUpgrade.ValidateOpenScene();
        QualityBlockGroundPathPlazaTerminationQA.ApplyAndPersist();
        QualityBlockGroundPathPlazaTerminationQA.ValidateOpenScene();
        QualityBlockGroundWornPathBTransitionQA.ApplyAndPersist();
        QualityBlockGroundWornPathBTransitionQA.ValidateOpenScene();
        QualityBlockSceneMaterialPhysicalityUpgrade.ApplyAndValidate();

        QualityBlockTreeWoodyContinuityQA.ApplyAndPersist();
        QualityBlockTreeDetailUpgrade.ValidateOpenScene();
        QualityBlockTreeWoodyContinuityQA.ValidateOpenScene();
        QualityBlockFoliagePhysicalityQA.ValidateOpenScene();
        QualityBlockVegetationRootZoneInterfaceQA.ApplyAndPersist();
        QualityBlockVegetationRootZoneInterfaceQA.ValidateOpenScene();

        QualityBlockFacadeApertureConstructionQA.ApplyAndPersist();
        QualityBlockFacadeAperturePhysicalUvQA.ApplyAndPersist();
        QualityBlockFacadeApertureConstructionQA.ValidateOpenScene();
        QualityBlockFacadeAperturePhysicalUvQA.ValidateOpenScene();

        QualityBlockStairTowerApertureInstallationQA.ApplyAndPersist();
        QualityBlockStairTowerShellJointQA.ApplyAndPersist();
        QualityBlockStairTowerApertureInstallationQA.ValidateOpenScene();
        QualityBlockStairTowerShellJointQA.ValidateOpenScene();

        QualityBlockBalconyConstructionInterfaceQA.ApplyAndPersist();
        QualityBlockBalconyConstructionInterfaceQA.ValidateOpenScene();
        QualityBlockBalconyGuardrailInstallationQA.ApplyAndPersist();
        QualityBlockBalconyGuardrailInstallationQA.ValidateOpenScene();
        QualityBlockBalconySeparationPanelFormalIntegrationQA.ApplyAndPersist();
        QualityBlockBalconySeparationPanelFormalIntegrationQA.ValidateOpenScene();

        QualityBlockAcOutdoorUnitInstallationQA.ValidateOpenScene();
        QualityBlockAcFanPhysicalRefinement.ValidateOpenScene();
        QualityBlockAcFanGuardRoundWireRefinement.ValidateOpenScene();
        QualityBlockAcFanGuardMountInterfaceRefinement.ValidateOpenScene();
        QualityBlockRainwaterDownpipeInstallationQA.ApplyAndPersist();
        QualityBlockRainwaterDownpipeInstallationQA.ValidateOpenScene();
        QualityBlockFutonBalconyDrapeQA.ApplyAndPersist();
        QualityBlockFutonBalconyDrapeQA.ValidateOpenScene();
        QualityBlockFacadeWeatheringOpticalRefinementQA.ApplyAndPersist();
        QualityBlockFacadeWeatheringOpticalRefinementQA.ValidateOpenScene();

        QualityBlockGroundPathPlazaTerminationQA.ValidateOpenScene();
        QualityBlockGroundWornPathBTransitionQA.ValidateOpenScene();
        QualityBlockPeriodAuthenticityUpgrade.ValidateOpenScene();
        QualityBlockParkFurnitureUpgrade.ValidateOpenScene();
        QualityBlockParkFurniturePhysicalRefinement.ValidateOpenScene();
        QualityBlockParkFurnitureLodRebind.ValidateOpenScene();
        QualityBlockSlideAccessInstallationQA.ValidateOpenScene();
        QualityBlockSlideChuteFabricationQA.ValidateOpenScene();
        QualityBlockParkFurnitureMicrodetailUpgrade.Validate();
        QualityBlockBenchSeatConstructionInterfaceQA.ValidateOpenScene();
        QualityBlockParkLampInstallationQA.ValidateOpenScene();
        QualityBlockTreeWoodyContinuityQA.ValidateOpenScene();
        QualityBlockFoliagePhysicalityQA.ValidateOpenScene();
        QualityBlockVegetationRootZoneInterfaceQA.ValidateOpenScene();
        QualityBlockFacadeWeatheringOpticalRefinementQA.ValidateOpenScene();
        QualityBlockPhysicalTexelDensityQA.ValidateOpenScene();
        QualityBlockAlbedoLightingNeutralityQA.ValidateGeneratedBaseAlbedos();
        QualityBlockSceneMetadataCoverageQA.ValidateOpenScene();
        QualityBlockBenchmarkPrimitiveExposureQA.ValidateOpenScene();

        // Grass is deliberately rebuilt once at the end of all mutating construction passes. Persisting
        // here makes the exact blade geometry/material/LOD state part of the same scene that reflection
        // probes will see. From the reflection request onward the grass path is validation-only.
        QualityBlockGrassBladeFieldUpgrade.ApplyToOpenScene(true);
        QualityBlockGrassBladeFieldUpgrade.ValidateOpenScene(false);
        QualityBlockGrassBladeFormalPersistenceQA.ValidatePersistedEvidenceBinding();
        QualityBlockSceneMetadataCoverageQA.ValidateOpenScene();
        QualityBlockBenchmarkPrimitiveExposureQA.ValidateOpenScene();

        QualityBlockVegetationRootZoneInterfaceQA.ValidateOpenScene();
        QualityBlockFacadeWeatheringOpticalRefinementQA.ValidateOpenScene();
        QualityBlockAcOutdoorUnitInstallationQA.ValidateOpenScene();
        QualityBlockAcFanPhysicalRefinement.ValidateOpenScene();
        QualityBlockAcFanGuardRoundWireRefinement.ValidateOpenScene();
        QualityBlockAcFanGuardMountInterfaceRefinement.ValidateOpenScene();
        QualityBlockBalconySeparationPanelFormalIntegrationQA.ValidateOpenScene();
        QualityBlockGrassBladeFieldUpgrade.ValidateOpenScene(false);
        QualityBlockGrassBladeFormalPersistenceQA.ValidatePersistedEvidenceBinding();
        QualityBlockSolarShadowCaptureCoherenceQA.ValidateOpenScene();
        QualityBlockLightEvidencePurityQA.ValidateOpenScene();
        QualityBlockReflectionProbeAwaiter.Begin(FinishAfterReflectionSynchronization);

        Debug.Log(
            "Native-4K review packet entered reflection synchronization after final material/construction binding, including persisted balcony separation panels and a persisted physical grass-blade field with four LODs. " +
            "Balcony panels, grass, geometry, materials and lighting are read-only from this point; Visual Fidelity remains UNSCORED until actual rendered evidence is reviewed.");
    }

    private static void FinishAfterReflectionSynchronization()
    {
        QualityBlockSolarShadowCaptureCoherenceQA.ValidateOpenScene();
        QualityBlockLightEvidencePurityQA.ValidateOpenScene();
        QualityBlockReflectionProbeCaptureSyncQA.ValidateRuntimeReceipt();
        QualityBlockReflectionProbeAwaiter.ValidateLatestWaitProof();
        QualityBlockGroundPathPlazaTerminationQA.ValidateOpenScene();
        QualityBlockGroundWornPathBTransitionQA.ValidateOpenScene();
        QualityBlockPeriodAuthenticityUpgrade.ValidateOpenScene();
        QualityBlockFacadeApertureConstructionQA.ValidateOpenScene();
        QualityBlockFacadeAperturePhysicalUvQA.ValidateOpenScene();
        QualityBlockFacadeWeatheringOpticalRefinementQA.ValidateOpenScene();
        QualityBlockStairTowerApertureInstallationQA.ValidateOpenScene();
        QualityBlockStairTowerShellJointQA.ValidateOpenScene();
        QualityBlockBalconyGuardrailInstallationQA.ValidateOpenScene();
        QualityBlockBalconySeparationPanelFormalIntegrationQA.ValidateOpenScene();
        QualityBlockAcOutdoorUnitInstallationQA.ValidateOpenScene();
        QualityBlockAcFanPhysicalRefinement.ValidateOpenScene();
        QualityBlockAcFanGuardRoundWireRefinement.ValidateOpenScene();
        QualityBlockAcFanGuardMountInterfaceRefinement.ValidateOpenScene();
        QualityBlockRainwaterDownpipeInstallationQA.ValidateOpenScene();
        QualityBlockFutonBalconyDrapeQA.ValidateOpenScene();
        QualityBlockParkFurnitureUpgrade.ValidateOpenScene();
        QualityBlockParkFurniturePhysicalRefinement.ValidateOpenScene();
        QualityBlockParkFurnitureLodRebind.ValidateOpenScene();
        QualityBlockSlideAccessInstallationQA.ValidateOpenScene();
        QualityBlockSlideChuteFabricationQA.ValidateOpenScene();
        QualityBlockBenchSeatConstructionInterfaceQA.ValidateOpenScene();
        QualityBlockParkLampInstallationQA.ValidateOpenScene();
        QualityBlockTreeWoodyContinuityQA.ValidateOpenScene();
        QualityBlockFoliagePhysicalityQA.ValidateOpenScene();
        QualityBlockVegetationRootZoneInterfaceQA.ValidateOpenScene();
        QualityBlockSceneMetadataCoverageQA.ValidateOpenScene();
        QualityBlockBenchmarkPrimitiveExposureQA.ValidateOpenScene();
        QualityBlockGrassBladeFieldUpgrade.ValidateOpenScene(false);
        QualityBlockGrassBladeFormalPersistenceQA.ValidatePersistedEvidenceBinding();

        QualityBlock4KCapture.CapturePreparedSceneAfterProbeSync();
        QualityBlockBenchmarkObservabilityQA.ExtractPeriodAuthenticityCropsFromExistingFrames();
        QualityBlockRenderEvidenceProvenanceQA.SealCurrentCapture();
        QualityBlockRenderEvidenceProvenanceQA.WriteBoundEvidenceTemplate();
        AssetDatabase.Refresh();

        QualityBlockSolarShadowCaptureCoherenceQA.ValidateOpenScene();
        QualityBlockLightEvidencePurityQA.ValidateOpenScene();
        QualityBlockReflectionProbeCaptureSyncQA.ValidateRuntimeReceipt();
        QualityBlockReflectionProbeAwaiter.ValidateLatestWaitProof();
        QualityBlockStructuralSurfaceRefinement.ValidateOpenScene();
        QualityBlockGroundPathPlazaTerminationQA.ValidateOpenScene();
        QualityBlockGroundWornPathBTransitionQA.ValidateOpenScene();
        QualityBlockPeriodAuthenticityUpgrade.ValidateOpenScene();
        QualityBlockFacadeApertureConstructionQA.ValidateOpenScene();
        QualityBlockFacadeAperturePhysicalUvQA.ValidateOpenScene();
        QualityBlockFacadeWeatheringOpticalRefinementQA.ValidateOpenScene();
        QualityBlockStairTowerApertureInstallationQA.ValidateOpenScene();
        QualityBlockStairTowerShellJointQA.ValidateOpenScene();
        QualityBlockBalconyGuardrailInstallationQA.ValidateOpenScene();
        QualityBlockBalconySeparationPanelFormalIntegrationQA.ValidateOpenScene();
        QualityBlockAcOutdoorUnitInstallationQA.ValidateOpenScene();
        QualityBlockAcFanPhysicalRefinement.ValidateOpenScene();
        QualityBlockAcFanGuardRoundWireRefinement.ValidateOpenScene();
        QualityBlockAcFanGuardMountInterfaceRefinement.ValidateOpenScene();
        QualityBlockRainwaterDownpipeInstallationQA.ValidateOpenScene();
        QualityBlockFutonBalconyDrapeQA.ValidateOpenScene();
        QualityBlockParkFurnitureUpgrade.ValidateOpenScene();
        QualityBlockParkFurniturePhysicalRefinement.ValidateOpenScene();
        QualityBlockParkFurnitureLodRebind.ValidateOpenScene();
        QualityBlockSlideAccessInstallationQA.ValidateOpenScene();
        QualityBlockSlideChuteFabricationQA.ValidateOpenScene();
        QualityBlockParkFurnitureMicrodetailUpgrade.Validate();
        QualityBlockBenchSeatConstructionInterfaceQA.ValidateOpenScene();
        QualityBlockParkLampInstallationQA.ValidateOpenScene();
        QualityBlockTreeWoodyContinuityQA.ValidateOpenScene();
        QualityBlockVegetationRootZoneInterfaceQA.ValidateOpenScene();
        QualityBlockBenchmarkPrimitiveExposureQA.ValidateOpenScene();
        QualityBlockSceneMaterialPhysicalityUpgrade.ValidateOpenScene();
        QualityBlockSceneMetadataCoverageQA.ValidateOpenScene();
        QualityBlockTextureSamplingUpgrade.ValidateOpenScene();
        QualityBlockPhysicalTexelDensityQA.ValidateOpenScene();
        QualityBlockAlbedoLightingNeutralityQA.ValidateGeneratedBaseAlbedos();
        QualityBlockFoliagePhysicalityQA.ValidateOpenScene();
        QualityBlockSceneRepetitionQA.ValidateOpenScene();
        QualityBlockGrassBladeFieldUpgrade.ValidateOpenScene(false);
        QualityBlockGrassBladeFormalPersistenceQA.ValidatePersistedEvidenceBinding();

        QualityBlockTemporalRuntimeEvidenceGuard.Begin();
        try
        {
            QualityBlockPreparedTemporalCapture.CaptureAndSealPreparedScene();
            QualityBlockPreparedTemporalCapture.ValidateLatestPreparedBinding();
            QualityBlockTemporalRuntimeEvidenceGuard.CompleteAndSeal();
        }
        catch
        {
            QualityBlockTemporalRuntimeEvidenceGuard.Abort();
            throw;
        }

        QualityBlockRenderedImageDiagnostics.AnalyzeExistingCapture();
        QualityBlockRenderedRepetitionDiagnostics.AnalyzeExistingCapture();
        QualityBlockRenderedLightLeakDiagnostics.AnalyzeExistingCapture();
        QualityBlockTemporalDiagnostics.AnalyzeLatestEvidence();
        AssetDatabase.Refresh();

        Debug.Log(
            "Complete native-4K review packet prepared: sealed hero/oblique/grazing 3840x2160 stills and 100% crops plus bound temporal evidence. " +
            "Persisted balcony separation panels and the physical grass-blade field are explicitly validated before probe request, still capture and temporal capture; pixel diagnostics remain warning-only and Visual Fidelity remains UNSCORED until manual review against the 100-point gate.");
    }
}
