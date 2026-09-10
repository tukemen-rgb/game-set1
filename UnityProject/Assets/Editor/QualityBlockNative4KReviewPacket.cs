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
        QualityBlockSolarShadowCaptureCoherenceQA.ValidateContractConfigOnly();
        QualityBlockRenderedImageDiagnostics.ValidateContractConfigOnly();
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
        QualityBlockStairTowerApertureInstallationQA.ValidateContractConfigOnly();
        QualityBlockBalconyConstructionInterfaceQA.ValidateContractConfigOnly();
        QualityBlockBalconyGuardrailInstallationQA.ValidateContractConfigOnly();
        QualityBlockAcOutdoorUnitInstallationQA.ValidateContractConfigOnly();
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
        QualityBlockHdrTonemapRuntimeQA.ValidateContractConfigOnly();
        QualityBlockReflectionProbeCaptureSyncQA.ValidateContractConfigOnly();
        QualityBlock4KCapture.ValidateCaptureContract();

        // Build/save/reopen the highest-quality generated scene first. The period marker is then
        // explicitly installed here rather than relying on an indirect quality-chain side effect:
        // the two locked rooftop 100% crops must never point at an empty roof while still appearing
        // valid in the capture manifest. Both worn-path corrections run immediately after this step.
        QualityBlock4KCapture.PrepareSceneForSynchronizedCapture();
        QualityBlockPeriodAuthenticityUpgrade.ApplyToOpenScene();
        QualityBlockPeriodAuthenticityUpgrade.ValidateOpenScene();
        QualityBlockGroundPathPlazaTerminationQA.ApplyAndPersist();
        QualityBlockGroundPathPlazaTerminationQA.ValidateOpenScene();
        QualityBlockGroundWornPathBTransitionQA.ApplyAndPersist();
        QualityBlockGroundWornPathBTransitionQA.ValidateOpenScene();
        QualityBlockSceneMaterialPhysicalityUpgrade.ApplyAndValidate();

        // Generated fallback trees: correct segmented radius jumps and normalized bark-UV restarts only
        // after the benchmark/PBR rebuild, then persist exactly that state for reflection capture.
        QualityBlockTreeWoodyContinuityQA.ApplyAndPersist();
        QualityBlockTreeDetailUpgrade.ValidateOpenScene();
        QualityBlockTreeWoodyContinuityQA.ValidateOpenScene();
        QualityBlockFoliagePhysicalityQA.ValidateOpenScene();

        // Main apartment facade: true openings + physical metric UV field.
        QualityBlockFacadeApertureConstructionQA.ApplyAndPersist();
        QualityBlockFacadeAperturePhysicalUvQA.ApplyAndPersist();
        QualityBlockFacadeApertureConstructionQA.ValidateOpenScene();
        QualityBlockFacadeAperturePhysicalUvQA.ValidateOpenScene();

        // Projecting stair tower: true rough openings and non-coplanar shell butt joints.
        QualityBlockStairTowerApertureInstallationQA.ApplyAndPersist();
        QualityBlockStairTowerShellJointQA.ApplyAndPersist();
        QualityBlockStairTowerApertureInstallationQA.ValidateOpenScene();
        QualityBlockStairTowerShellJointQA.ValidateOpenScene();

        // Establish slab/base-plate/anchor relationships, then replace the source rail placeholders.
        QualityBlockBalconyConstructionInterfaceQA.ApplyAndPersist();
        QualityBlockBalconyConstructionInterfaceQA.ValidateOpenScene();
        QualityBlockBalconyGuardrailInstallationQA.ApplyAndPersist();
        QualityBlockBalconyGuardrailInstallationQA.ValidateOpenScene();

        // Remaining building construction interfaces. Futon drapes run after guardrail reconstruction so
        // the actual final top rail is their physical installation datum.
        QualityBlockAcOutdoorUnitInstallationQA.ApplyAndPersist();
        QualityBlockAcOutdoorUnitInstallationQA.ValidateOpenScene();
        QualityBlockRainwaterDownpipeInstallationQA.ApplyAndPersist();
        QualityBlockRainwaterDownpipeInstallationQA.ValidateOpenScene();
        QualityBlockFutonBalconyDrapeQA.ApplyAndPersist();
        QualityBlockFutonBalconyDrapeQA.ValidateOpenScene();

        // Park/street furniture and scene-wide source preflight. The park-lamp validator is deliberately
        // explicit here: existence of its source file is insufficient unless the prepared scene actually
        // contains the continuous pole/service cover/head assembly rebound into all four LODs. The chute
        // fabrication validator likewise runs after microdetail and LOD rebind because that is the exact
        // mesh/material state sent to probes. Both worn-path corrections and the rooftop receiving system
        // are re-proved here as seals against later passes deleting or reverting benchmark-facing geometry.
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
        QualityBlockPhysicalTexelDensityQA.ValidateOpenScene();
        QualityBlockAlbedoLightingNeutralityQA.ValidateGeneratedBaseAlbedos();
        QualityBlockSceneMetadataCoverageQA.ValidateOpenScene();
        QualityBlockBenchmarkPrimitiveExposureQA.ValidateOpenScene();

        // The reflection request is made only after the final persisted scene and its one physical
        // SummerSun/sky/shadow state are valid. The awaiter fingerprints that state before RenderProbe,
        // after asynchronous completion and again immediately before the still is accepted.
        QualityBlockSolarShadowCaptureCoherenceQA.ValidateOpenScene();
        QualityBlockReflectionProbeAwaiter.Begin(FinishAfterReflectionSynchronization);

        Debug.Log(
            "Native-4K review packet entered reflection synchronization after final material binding, an explicitly installed four-way-stayed year-2000 rooftop reception assembly, " +
            "corrected WornPathA/plaza termination, a corrected curved/tapered WornPathB branch through a two-module ParkPathEast opening, continuous tree taper/metric bark UVs, " +
            "true facade/stair apertures, corrected balcony slab/base interfaces, four-LOD guardrails, AC/rainwater/futon interfaces, a verified continuous-taper park-lamp installation, " +
            "a watertight 2 mm fabricated stainless slide chute, and a SHA-256-bound physical sun/sky/ambient/shadow state. Visual Fidelity remains UNSCORED.");
    }

    private static void FinishAfterReflectionSynchronization()
    {
        // Immediately before Camera.Render, re-prove synchronized probes, the exact lighting state
        // used by those probes, and final construction state. ValidateLatestWaitProof recomputes the
        // full physical-lighting fingerprint and aborts if it differs from probe completion.
        QualityBlockSolarShadowCaptureCoherenceQA.ValidateOpenScene();
        QualityBlockReflectionProbeCaptureSyncQA.ValidateRuntimeReceipt();
        QualityBlockReflectionProbeAwaiter.ValidateLatestWaitProof();
        QualityBlockGroundPathPlazaTerminationQA.ValidateOpenScene();
        QualityBlockGroundWornPathBTransitionQA.ValidateOpenScene();
        QualityBlockPeriodAuthenticityUpgrade.ValidateOpenScene();
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
        QualityBlockSlideChuteFabricationQA.ValidateOpenScene();
        QualityBlockBenchSeatConstructionInterfaceQA.ValidateOpenScene();
        QualityBlockParkLampInstallationQA.ValidateOpenScene();
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
        // share the exact already-proven still geometry/material/reflection and solar/shadow state.
        QualityBlockSolarShadowCaptureCoherenceQA.ValidateOpenScene();
        QualityBlockReflectionProbeCaptureSyncQA.ValidateRuntimeReceipt();
        QualityBlockReflectionProbeAwaiter.ValidateLatestWaitProof();
        QualityBlockStructuralSurfaceRefinement.ValidateOpenScene();
        QualityBlockGroundPathPlazaTerminationQA.ValidateOpenScene();
        QualityBlockGroundWornPathBTransitionQA.ValidateOpenScene();
        QualityBlockPeriodAuthenticityUpgrade.ValidateOpenScene();
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
        QualityBlockSlideChuteFabricationQA.ValidateOpenScene();
        QualityBlockParkFurnitureMicrodetailUpgrade.Validate();
        QualityBlockBenchSeatConstructionInterfaceQA.ValidateOpenScene();
        QualityBlockParkLampInstallationQA.ValidateOpenScene();
        QualityBlockTreeWoodyContinuityQA.ValidateOpenScene();
        QualityBlockBenchmarkPrimitiveExposureQA.ValidateOpenScene();
        QualityBlockSceneMaterialPhysicalityUpgrade.ValidateOpenScene();
        QualityBlockSceneMetadataCoverageQA.ValidateOpenScene();
        QualityBlockTextureSamplingUpgrade.ValidateOpenScene();
        QualityBlockPhysicalTexelDensityQA.ValidateOpenScene();
        QualityBlockAlbedoLightingNeutralityQA.ValidateGeneratedBaseAlbedos();
        QualityBlockFoliagePhysicalityQA.ValidateOpenScene();
        QualityBlockSceneRepetitionQA.ValidateOpenScene();

        // Temporal evidence is now guarded at runtime, not just source-bound. The guard resets the
        // filmic telemetry after the three stills, verifies the invariant physical-lighting fingerprint
        // in MainCamera.onPreCull for every temporal Camera.Render, and seals exact HDR->LDR/fallback
        // counts to the generated temporal manifest/receipt and the already-accepted reflection proofs.
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
        QualityBlockTemporalDiagnostics.AnalyzeLatestEvidence();
        AssetDatabase.Refresh();

        Debug.Log(
            "Complete native-4K review packet prepared: sealed hero/oblique/grazing 3840x2160 stills and 100% crops plus bound temporal evidence. " +
            "The year-2000 rooftop reception assembly is explicitly present and retains four-way mast restraint through every LOD before reflection, still and temporal evidence; " +
            "WornPathA terminates its modular curb runs before the paved plaza, while WornPathB uses a two-module curb opening, curved compacted core and feathered turf shoulders; " +
            "generated fallback trees retain continuous woody taper and metric bark; legacy balcony rails are excluded from evidence; the two futon drapes resolve the final rail datum; " +
            "the park lamp retains its continuous installed assembly through every evidence phase; every slide LOD retains the watertight fabricated stainless chute; reflection cubemaps/stills " +
            "share one hash-identical physical lighting state; and every temporal MainCamera frame must prove that same physical-lighting fingerprint plus the filmic HDR->LDR path with zero fallback. " +
            "Visual Fidelity remains UNSCORED until the actual pixels are manually reviewed against the locked 100-point gate.");
    }
}
