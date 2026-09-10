using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persists the manufactured park/street-furniture state into the benchmark scene. Construction passes
/// are ordered so new renderers are rebound to LOD sets, shared microdetail is applied, and the final
/// stainless chute is then replaced with its topology-aware watertight sheet solid so the generic planar
/// UV rewrite cannot destroy the chute's authored path topology.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockParkFurnitureSaveGate
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/park_street_furniture_contract.json";
    private const string MicrodetailContractPath = "Assets/QA/park_furniture_microdetail_contract.json";
    private const string SlideInterfaceContractPath = "Assets/QA/slide_access_installation_contract.json";
    private const string SlideLookdevPath = "Assets/QA/slide_access_installation_lookdev.svg";
    private const string SlideChuteContractPath = "Assets/QA/slide_chute_fabrication_contract.json";
    private const string SlideChuteLookdevPath = "Assets/QA/slide_chute_fabrication_lookdev.svg";
    private const string BenchInterfaceContractPath = "Assets/QA/bench_seat_construction_interface_contract.json";
    private const string BenchLookdevPath = "Assets/QA/bench_seat_construction_lookdev.svg";
    private const string NoticeBoardContractPath = "Assets/QA/notice_board_display_case_contract.json";
    private const string NoticeBoardLookdevPath = "Assets/QA/notice_board_display_case_lookdev.svg";
    private static bool applying;

    static QualityBlockParkFurnitureSaveGate()
    {
        EditorSceneManager.sceneSaving -= OnSceneSaving;
        EditorSceneManager.sceneSaving += OnSceneSaving;
    }

    private static void OnSceneSaving(Scene scene, string path)
    {
        if (applying || path != ScenePath || !scene.IsValid()) return;

        bool detailChainReady = Resources.FindObjectsOfTypeAll<GameObject>()
            .Any(x => x.scene == scene && x.name == "GroundHighDetail");
        if (!detailChainReady) return;

        applying = true;
        try
        {
            ValidateContract();
            QualityBlockParkFurnitureUpgrade.BuildAndApply();
            QualityBlockParkFurniturePhysicalRefinement.ApplyAndValidate();

            // Benchmark-visible subassemblies exist before rebind so their renderers join the matching LOD.
            QualityBlockNoticeBoardDisplayCaseQA.ApplyToOpenScene();
            QualityBlockSlideAccessInstallationQA.ApplyToOpenScene();
            QualityBlockParkFurnitureLodRebind.RebindAndValidate();

            // Apply shared material microdetail first. The physical-refinement chute still has its legacy
            // authored-mesh prefix here and is therefore protected from the generic metre-planar rewrite.
            // Replace it afterwards with the final watertight U-section sheet solid; the existing renderer
            // remains in the LOD set, while the final mesh keeps longitudinal fabrication UV continuity.
            QualityBlockParkFurnitureMicrodetailUpgrade.BuildAndApply();
            QualityBlockSlideChuteFabricationQA.ApplyToOpenScene();
            QualityBlockNoticeBoardPrintedUvQA.ApplyAndValidate();
            QualityBlockBenchSeatConstructionInterfaceQA.ApplyToOpenScene();

            QualityBlockParkFurnitureUpgrade.ValidateOpenScene();
            QualityBlockParkFurniturePhysicalRefinement.ValidateOpenScene();
            QualityBlockParkFurnitureLodRebind.ValidateOpenScene();
            QualityBlockNoticeBoardDisplayCaseQA.ValidateOpenScene();
            QualityBlockNoticeBoardPrintedUvQA.ValidateOpenScene();
            QualityBlockSlideAccessInstallationQA.ValidateOpenScene();
            QualityBlockSlideChuteFabricationQA.ValidateOpenScene();
            QualityBlockParkFurnitureMicrodetailUpgrade.Validate();
            QualityBlockBenchSeatConstructionInterfaceQA.ValidateOpenScene();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Benchmark save blocked: park/street-furniture manufacture, material, physical-profile, " +
                "notice-board display-case/printed-UV, slide access/support, watertight chute fabrication, " +
                "LOD, microdetail or bench load-path contract failed.", ex);
        }
        finally
        {
            applying = false;
        }
    }

    [MenuItem("NewTown/QA/Validate Park + Street Furniture Contract")]
    public static void ValidateContract()
    {
        RequireFile(ContractPath, "construction/material metadata");
        RequireFile(MicrodetailContractPath, "park-furniture microdetail metadata");
        RequireFile(SlideInterfaceContractPath, "slide access/support metadata");
        RequireFile(SlideLookdevPath, "slide access/support lookdev illustration");
        RequireFile(SlideChuteContractPath, "slide chute fabrication metadata");
        RequireFile(SlideChuteLookdevPath, "slide chute fabrication lookdev illustration");
        RequireFile(BenchInterfaceContractPath, "bench construction-interface metadata");
        RequireFile(BenchLookdevPath, "bench construction lookdev illustration");
        RequireFile(NoticeBoardContractPath, "notice-board display-case metadata");
        RequireFile(NoticeBoardLookdevPath, "notice-board display-case lookdev illustration");

        RequireTokens(ContractPath, "Furniture metadata contract", new[]
        {
            "\"slide\"", "\"bench\"", "\"park_lamp\"", "\"notice_board\"",
            "\"albedo_linear_rgb\"", "\"roughness\"", "\"metallic\"",
            "\"normalScale\"", "\"microstructure\"", "\"wetness\"", "\"uvAging\"",
            "\"angularResponse\"", "\"levels\": 4", "\"generatedColliderCount\": 0",
            "\"modeled_chute_thickness_mm\": 2.0", "\"municipal_minimum_chute_thickness_mm\": 1.5",
            "\"visualFidelityPointsAwarded\": 0", "PENDING_UNITY_RUNTIME"
        });

        RequireTokens(MicrodetailContractPath, "Furniture microdetail metadata contract", new[]
        {
            "\"textureResolution\": 1024", "\"stainless_chute\"", "\"bench_timber\"",
            "\"precast_concrete\"", "\"normalAmplitudeMm\"", "\"physicalTileMeters\"",
            "\"bareMetalMetallicMin\": 0.95", "\"visualFidelityPointsAwarded\": 0",
            "PENDING_UNITY_RUNTIME"
        });

        RequireTokens(SlideInterfaceContractPath, "Slide access/support metadata", new[]
        {
            "\"id\": \"slide_access_installation_interface\"",
            "\"treadCount\": 10", "\"treadDepth\": 0.18", "\"treadThickness\": 0.0032",
            "\"treadRise\": 0.19", "\"treadBracketWidth\": 0.06",
            "\"minimumAccessAngleDegrees\": 50.0", "\"maximumAccessAngleDegrees\": 75.0",
            "\"maximumStepRiseMetres\": 0.22", "\"minimumTreadDepthMetres\": 0.17",
            "\"minimumStringerToTreadLateralClearanceMetres\": 0.015",
            "\"maximumBracketStringerAxisMissMetres\": 0.004",
            "\"requiredLodCount\": 4", "\"generatedColliderCount\": 0",
            "\"visualFidelityPointsAwarded\": 0", "PENDING_UNITY_RUNTIME"
        });

        RequireTokens(SlideChuteContractPath, "Slide chute fabrication metadata", new[]
        {
            "\"id\": \"slide_chute_fabricated_sheet_solid\"",
            "\"chuteWidth\": 0.92", "\"sheetThickness\": 0.002", "\"sideWallHeight\": 0.14",
            "\"minimumAllowedCurveRadius\": 0.75", "\"lodSegments\": [96, 48, 24, 12]",
            "\"maximumBoundaryEdgeCount\": 0", "\"maximumNonManifoldEdgeCount\": 0",
            "\"requiredMaterialName\": \"PBR_SlideStainless\"",
            "\"visualFidelityPointsAwarded\": 0", "PENDING_UNITY_RUNTIME"
        });

        RequireTokens(BenchInterfaceContractPath, "Bench construction-interface metadata", new[]
        {
            "\"id\": \"bench_seat_construction_interface\"", "\"slatCount\": 5",
            "\"steelBearerCenterY\": 0.5165", "\"highDetailSupportCenterY\": 0.214",
            "\"proxySupportCenterY\": 0.2275", "\"boltHeadCenterY\": 0.583",
            "\"outerSlatCenterAbsZ\": 0.235", "\"boltEmbedTarget\": 0.002",
            "\"contactToleranceMetres\": 0.0025", "\"visualFidelityPointsAwarded\": 0",
            "PENDING_UNITY_RUNTIME"
        });

        RequireTokens(NoticeBoardContractPath, "Notice-board display-case metadata", new[]
        {
            "\"id\": \"notice_board_display_case_installation\"",
            "\"clearCoverThickness\": 0.003", "\"requiredHingeCount\": 3",
            "\"requiredNoticeCount\": 5", "\"minimumDistinctNoticeMaterialCount\": 3",
            "\"requiredLodCount\": 4", "\"generatedColliderCount\": 0",
            "\"visualFidelityPointsAwarded\": 0", "PENDING_UNITY_RUNTIME"
        });

        QualityBlockSlideAccessInstallationQA.ValidateContractConfigOnly();
        QualityBlockSlideChuteFabricationQA.ValidateContractConfigOnly();
        QualityBlockBenchSeatConstructionInterfaceQA.ValidateContractConfigOnly();
        QualityBlockNoticeBoardDisplayCaseQA.ValidateContractConfigOnly();
    }

    private static void RequireFile(string path, string description)
    {
        if (!File.Exists(path))
            throw new InvalidOperationException($"Missing required {description}: {path}");
    }

    private static void RequireTokens(string path, string label, string[] requiredTokens)
    {
        string json = File.ReadAllText(path);
        foreach (string token in requiredTokens)
            if (!json.Contains(token, StringComparison.Ordinal))
                throw new InvalidOperationException($"{label} missing required token: {token}");
    }
}
