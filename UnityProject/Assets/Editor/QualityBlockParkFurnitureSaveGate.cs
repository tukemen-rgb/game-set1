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
/// material metadata, rebuilds the assembly, applies physical-profile refinement, rebinds explicit
/// LOD renderer sets, applies physical-scale microdetail, then runs structural/material QA.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockParkFurnitureSaveGate
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/park_street_furniture_contract.json";
    private const string MicrodetailContractPath = "Assets/QA/park_furniture_microdetail_contract.json";
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
            QualityBlockParkFurnitureLodRebind.RebindAndValidate();
            QualityBlockParkFurnitureMicrodetailUpgrade.BuildAndApply();
            QualityBlockParkFurnitureUpgrade.ValidateOpenScene();
            QualityBlockParkFurniturePhysicalRefinement.ValidateOpenScene();
            QualityBlockParkFurnitureLodRebind.ValidateOpenScene();
            QualityBlockParkFurnitureMicrodetailUpgrade.Validate();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Benchmark save blocked: park/street-furniture manufacture, material, physical-profile, LOD or microdetail contract failed.", ex);
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
    }
}
