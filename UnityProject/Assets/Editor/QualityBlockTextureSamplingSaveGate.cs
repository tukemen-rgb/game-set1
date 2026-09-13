using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persists texture sampling/map registration as a benchmark invariant once the detailed generated
/// scene chain exists. This may normalize generated material UV registration and texture importers;
/// it never claims the resulting pixels pass Visual Fidelity without native 4K review.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockTextureSamplingSaveGate
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private static bool applying;

    static QualityBlockTextureSamplingSaveGate()
    {
        EditorSceneManager.sceneSaving -= OnSceneSaving;
        EditorSceneManager.sceneSaving += OnSceneSaving;
    }

    private static void OnSceneSaving(Scene scene, string path)
    {
        if (applying || path != ScenePath || !scene.IsValid()) return;
        if (string.IsNullOrEmpty(scene.path)) return;

        bool qualityChainReady = HasSceneObject(scene, "DanchiHighDetail") &&
                                 HasSceneObject(scene, "DanchiFacadeOptics") &&
                                 HasSceneObject(scene, "GroundHighDetail");
        if (!qualityChainReady) return;

        applying = true;
        try
        {
            QualityBlockTextureSamplingUpgrade.ApplyAndValidate();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Benchmark save blocked: generated 4K texture sampling, color-space semantics, anisotropy or PBR map registration is invalid.", ex);
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
