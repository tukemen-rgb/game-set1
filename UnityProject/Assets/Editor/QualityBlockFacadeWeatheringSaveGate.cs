using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Makes cause-based facade weathering a persisted benchmark invariant. The quality build chain
/// saves the benchmark several times; once the detailed danchi exists, every subsequent save must
/// contain the non-primitive weathering pass and pass its structural QA. This also covers the native
/// 4K capture preparation path without relying on a human to remember an extra menu command.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockFacadeWeatheringSaveGate
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private static bool applying;

    static QualityBlockFacadeWeatheringSaveGate()
    {
        EditorSceneManager.sceneSaving -= OnSceneSaving;
        EditorSceneManager.sceneSaving += OnSceneSaving;
    }

    private static void OnSceneSaving(Scene scene, string path)
    {
        if (applying || path != ScenePath || !scene.IsValid())
            return;

        bool detailedDanchiReady = Resources.FindObjectsOfTypeAll<GameObject>()
            .Any(x => x.scene == scene && x.name == "DanchiHighDetail");
        if (!detailedDanchiReady)
            return;

        applying = true;
        try
        {
            QualityBlockFacadeWeatheringDetailUpgrade.ApplyToOpenScene();
            QualityBlockFacadeWeatheringDetailUpgrade.ValidateOpenScene();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Benchmark save blocked: cause-based facade weathering failed its required structural QA.", ex);
        }
        finally
        {
            applying = false;
        }
    }
}
