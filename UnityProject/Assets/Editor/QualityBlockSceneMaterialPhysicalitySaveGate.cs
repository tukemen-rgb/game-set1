using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ensures the persisted scored benchmark cannot retain material-family mistakes after the full
/// high-detail/facade chain exists. This is deliberately a save-time implementation gate; it does
/// not clear any visual critical defect without the sealed native 4K review.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockSceneMaterialPhysicalitySaveGate
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private static bool applying;

    static QualityBlockSceneMaterialPhysicalitySaveGate()
    {
        EditorSceneManager.sceneSaving -= OnSceneSaving;
        EditorSceneManager.sceneSaving += OnSceneSaving;
    }

    private static void OnSceneSaving(Scene scene, string path)
    {
        if (applying || path != ScenePath || !scene.IsValid()) return;

        bool qualityChainReady = HasSceneObject(scene, "DanchiHighDetail") &&
                                 HasSceneObject(scene, "DanchiFacadeOptics") &&
                                 HasSceneObject(scene, "GroundHighDetail");
        if (!qualityChainReady) return;

        applying = true;
        try
        {
            QualityBlockSceneMaterialPhysicalityUpgrade.ApplyAndValidate();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Benchmark save blocked: actual scene material bindings or physical metallic/specular ranges are inconsistent with construction metadata.", ex);
        }
        finally
        {
            applying = false;
        }
    }

    private static bool HasSceneObject(Scene scene, string objectName)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .Any(x => x != null && x.scene == scene && x.name == objectName);
    }
}
