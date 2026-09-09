using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persists the retained structural-surface refinement once the high-detail ground chain exists.
/// The base builder remains simple/reviewable, while any quality-scene save after that point must
/// replace targeted active Cube/Cylinder renderer meshes with dimension-baked physical edge geometry.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockStructuralSurfaceSaveGate
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/structural_surface_geometry_contract.json";
    private static bool applying;

    static QualityBlockStructuralSurfaceSaveGate()
    {
        EditorSceneManager.sceneSaving -= OnSceneSaving;
        EditorSceneManager.sceneSaving += OnSceneSaving;
    }

    private static void OnSceneSaving(Scene scene, string path)
    {
        if (applying || path != ScenePath || !scene.IsValid()) return;

        bool qualityChainReady = Resources.FindObjectsOfTypeAll<GameObject>()
            .Any(x => x.scene == scene && x.name == "GroundHighDetail");
        if (!qualityChainReady) return;

        applying = true;
        try
        {
            ValidateContract();
            QualityBlockStructuralSurfaceRefinement.ApplyToOpenScene();
            QualityBlockStructuralSurfaceRefinement.ValidateOpenScene();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Benchmark save blocked: retained structural surface geometry failed primitive-removal or footprint-preservation QA.", ex);
        }
        finally
        {
            applying = false;
        }
    }

    [MenuItem("NewTown/QA/Validate Structural Surface Geometry Contract")]
    public static void ValidateContract()
    {
        if (!File.Exists(ContractPath))
            throw new InvalidOperationException($"Missing required construction metadata: {ContractPath}");

        string json = File.ReadAllText(ContractPath);
        string[] requiredTokens =
        {
            "\"criticalDefectRiskReduced\": \"visible_primitive_placeholder_geometry\"",
            "\"visualFidelityPointsAwarded\": 0",
            "\"MainBlock\"", "\"BalconyFloor_*\"", "\"Rail_*\"", "\"RainGutter\"",
            "\"edgeConstruction\"", "\"mountingAndInterfaces\"", "\"exposureAndAging\"",
            "\"geometryVsMaterialDetail\"", "\"paintedHighlightsForbidden\": true",
            "\"rendererBoundsMaximumDeltaMetres\": 0.002",
            "\"colliderBoundsMaximumDeltaMetres\": 0.002",
            "\"minimumChamferedBoxes\": 300",
            "\"activeBuiltInPrimitiveMeshCountForTargetedStructuralRenderers\": 0",
            "\"levelsRequiredWhereAssemblyHasLod\": 4",
            "PENDING_UNITY_RUNTIME"
        };

        foreach (string token in requiredTokens)
            if (json.IndexOf(token, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException(
                    $"Structural surface geometry contract missing required token: {token}");
    }
}
