using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Makes the park/street-furniture construction pass part of the persisted benchmark scene whenever
/// the existing high-detail ground chain is present. The gate checks machine-readable manufacture/
/// material metadata, rebuilds the assembly, applies physical-profile refinement, reconstructs the
/// notice-board weatherproof display case and slide manufactured access/head/runout support path before
/// explicit LOD renderer rebinding, applies physical-scale microdetail, normalizes printed-notice UVs,
/// corrects the bench load path, then runs structural/material QA.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockParkFurnitureSaveGate
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/park_street_furniture_contract.json";
    private const string MicrodetailContractPath = "Assets/QA/park_furniture_microdetail_contract.json";
    private const string SlideInterfaceContractPath = "Assets/QA/slide_access_installation_contract.json";
    private const string SlideLookdevPath = "Assets/QA/slide_access_installation_lookdev.svg";
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

            // Build benchmark-visible subassemblies before LOD rebind so their new renderers are captured
            // by the corresponding LOD0/1/2/3 renderer sets instead of becoming all-distance renderers.
            QualityBlockNoticeBoardDisplayCaseQA.ApplyToOpenScene();
            QualityBlockSlideAccessInstallationQA.ApplyToOpenScene();
            QualityBlockParkFurnitureLodRebind.RebindAndValidate();

            // The shared furniture microdetail pass rewrites generated meshes to metre-space planar UVs.
            // Printed notices deliberately use one full unique sheet texture each, so normalize their
            // per-material tiling immediately afterward instead of allowing metre UVs to crop/repeat print.
            QualityBlockParkFurnitureMicrodetailUpgrade.BuildAndApply();
            QualityBlockNoticeBoardPrintedUvQA.ApplyAndValidate();
            QualityBlockBenchSeatConstructionInterfaceQA.ApplyToOpenScene();
            QualityBlockParkFurnitureUpgrade.ValidateOpenScene();
            QualityBlockParkFurniturePhysicalRefinement.ValidateOpenScene();
            QualityBlockParkFurnitureLodRebind.ValidateOpenScene();
            QualityBlockNoticeBoardDisplayCaseQA.ValidateOpenScene();
            QualityBlockNoticeBoardPrintedUvQA.ValidateOpenScene();
            QualityBlockSlideAccessInstallationQA.ValidateOpenScene();
            QualityBlockParkFurnitureMicrodetailUpgrade.Validate();
            QualityBlockBenchSeatConstructionInterfaceQA.ValidateOpenScene();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Benchmark save blocked: park/street-furniture manufacture, material, physical-profile, notice-board display-case/printed-UV, slide access/support, LOD, microdetail or bench load-path contract failed.", ex);
        }
        finally
        {
            applying = false;
        }
    }

    [MenuItem("NewTown/QA/Validate Park + Street Furniture Contract")]
    public static void ValidateContract()
    {
        if (!File.Exists(ContractPath))
            throw new InvalidOperationException($"Missing required construction/material metadata: {ContractPath}");
        if (!File.Exists(MicrodetailContractPath))
            throw new InvalidOperationException($"Missing required park-furniture microdetail metadata: {MicrodetailContractPath}");
        if (!File.Exists(SlideInterfaceContractPath))
            throw new InvalidOperationException($"Missing required slide access/support metadata: {SlideInterfaceContractPath}");
        if (!File.Exists(SlideLookdevPath))
            throw new InvalidOperationException($"Missing required slide access/support lookdev illustration: {SlideLookdevPath}");
        if (!File.Exists(BenchInterfaceContractPath))
            throw new InvalidOperationException($"Missing required bench construction-interface metadata: {BenchInterfaceContractPath}");
        if (!File.Exists(BenchLookdevPath))
            throw new InvalidOperationException($"Missing required bench construction lookdev illustration: {BenchLookdevPath}");
        if (!File.Exists(NoticeBoardContractPath))
            throw new InvalidOperationException($"Missing required notice-board display-case metadata: {NoticeBoardContractPath}");
        if (!File.Exists(NoticeBoardLookdevPath))
            throw new InvalidOperationException($"Missing required notice-board display-case lookdev illustration: {NoticeBoardLookdevPath}");

        string json = File.ReadAllText(ContractPath);
        string[] requiredTokens =
        {
            "\"slide\"", "\"bench\"", "\"park_lamp\"", "\"notice_board\"",
            "\"albedo_linear_rgb\"", "\"roughness\"", "\"metallic\"",
            "\"normalScale\"", "\"microstructure\"", "\"wetness\"", "\"uvAging\"",
            "\"angularResponse\"", "\"levels\": 4", "\"generatedColliderCount\": 0",
            "\"modeled_chute_thickness_mm\": 2.0", "\"municipal_minimum_chute_thickness_mm\": 1.5",
            "\"visualFidelityPointsAwarded\": 0", "PENDING_UNITY_RUNTIME"
        };
        foreach (string token in requiredTokens)
            if (!json.Contains(token, StringComparison.Ordinal))
                throw new InvalidOperationException($"Furniture metadata contract missing required token: {token}");

        string microJson = File.ReadAllText(MicrodetailContractPath);
        string[] microTokens =
        {
            "\"textureResolution\": 1024", "\"stainless_chute\"", "\"bench_timber\"",
            "\"precast_concrete\"", "\"normalAmplitudeMm\"", "\"physicalTileMeters\"",
            "\"bareMetalMetallicMin\": 0.95", "\"visualFidelityPointsAwarded\": 0",
            "PENDING_UNITY_RUNTIME"
        };
        foreach (string token in microTokens)
            if (!microJson.Contains(token, StringComparison.Ordinal))
                throw new InvalidOperationException($"Furniture microdetail metadata contract missing required token: {token}");

        string slideJson = File.ReadAllText(SlideInterfaceContractPath);
        string[] slideTokens =
        {
            "\"id\": \"slide_access_installation_interface\"",
            "\"treadCount\": 10",
            "\"treadDepth\": 0.18",
            "\"treadThickness\": 0.0032",
            "\"treadRise\": 0.19",
            "\"treadBracketWidth\": 0.06",
            "\"minimumAccessAngleDegrees\": 50.0",
            "\"maximumAccessAngleDegrees\": 75.0",
            "\"maximumStepRiseMetres\": 0.22",
            "\"minimumTreadDepthMetres\": 0.17",
            "\"minimumStringerToTreadLateralClearanceMetres\": 0.015",
            "\"maximumBracketStringerAxisMissMetres\": 0.004",
            "\"requiredLodCount\": 4",
            "\"generatedColliderCount\": 0",
            "\"visualFidelityPointsAwarded\": 0",
            "PENDING_UNITY_RUNTIME"
        };
        foreach (string token in slideTokens)
            if (!slideJson.Contains(token, StringComparison.Ordinal))
                throw new InvalidOperationException($"Slide access/support metadata missing required token: {token}");

        string benchJson = File.ReadAllText(BenchInterfaceContractPath);
        string[] benchTokens =
        {
            "\"id\": \"bench_seat_construction_interface\"",
            "\"slatCount\": 5",
            "\"steelBearerCenterY\": 0.5165",
            "\"highDetailSupportCenterY\": 0.214",
            "\"proxySupportCenterY\": 0.2275",
            "\"boltHeadCenterY\": 0.583",
            "\"outerSlatCenterAbsZ\": 0.235",
            "\"boltEmbedTarget\": 0.002",
            "\"contactToleranceMetres\": 0.0025",
            "\"visualFidelityPointsAwarded\": 0",
            "PENDING_UNITY_RUNTIME"
        };
        foreach (string token in benchTokens)
            if (!benchJson.Contains(token, StringComparison.Ordinal))
                throw new InvalidOperationException($"Bench construction-interface metadata missing required token: {token}");

        string noticeJson = File.ReadAllText(NoticeBoardContractPath);
        string[] noticeTokens =
        {
            "\"id\": \"notice_board_display_case_installation\"",
            "\"clearCoverThickness\": 0.003",
            "\"requiredHingeCount\": 3",
            "\"requiredNoticeCount\": 5",
            "\"minimumDistinctNoticeMaterialCount\": 3",
            "\"requiredLodCount\": 4",
            "\"generatedColliderCount\": 0",
            "\"visualFidelityPointsAwarded\": 0",
            "PENDING_UNITY_RUNTIME"
        };
        foreach (string token in noticeTokens)
            if (!noticeJson.Contains(token, StringComparison.Ordinal))
                throw new InvalidOperationException($"Notice-board display-case metadata missing required token: {token}");

        QualityBlockSlideAccessInstallationQA.ValidateContractConfigOnly();
        QualityBlockBenchSeatConstructionInterfaceQA.ValidateContractConfigOnly();
        QualityBlockNoticeBoardDisplayCaseQA.ValidateContractConfigOnly();
    }
}
