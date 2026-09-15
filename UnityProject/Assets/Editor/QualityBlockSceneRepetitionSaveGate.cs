using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persists and enforces the anti-clone pass whenever the high-detail generated benchmark and
/// facade-optics stack are being saved. This deliberately runs through the sceneSaving gate so the
/// existing 4K preparation path cannot silently save an otherwise valid scene with thirty identical
/// interior cards. Authored danchi art remains authoritative and is skipped by the occupancy pass.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockSceneRepetitionSaveGate
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private static bool applying;

    static QualityBlockSceneRepetitionSaveGate()
    {
        EditorSceneManager.sceneSaving -= OnSceneSaving;
        EditorSceneManager.sceneSaving += OnSceneSaving;
    }

    private static void OnSceneSaving(Scene scene, string path)
    {
        if (applying || path != ScenePath || !scene.IsValid()) return;

        bool detailedGroundReady = Resources.FindObjectsOfTypeAll<GameObject>()
            .Any(x => x.scene == scene && x.name == "GroundHighDetail");
        bool facadeOpticsReady = Resources.FindObjectsOfTypeAll<GameObject>()
            .Any(x => x.scene == scene && x.name == "DanchiFacadeOptics");
        if (!detailedGroundReady || !facadeOpticsReady) return;

        applying = true;
        try
        {
            QualityBlockFacadeOccupancyVariationUpgrade.ApplyToOpenScene();
            QualityBlockFacadeOccupancyVariationUpgrade.ValidateOpenScene();
            QualityBlockSceneRepetitionQA.ValidateOpenScene();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Benchmark save blocked: apartment occupancy variation or static repeated-pattern QA failed.", ex);
        }
        finally
        {
            applying = false;
        }
    }
}