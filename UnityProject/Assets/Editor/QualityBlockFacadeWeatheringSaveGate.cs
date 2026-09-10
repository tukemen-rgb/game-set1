using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Makes cause-based facade weathering a persisted benchmark invariant. The quality build chain
/// saves the benchmark several times; once the detailed danchi exists, every subsequent save must
/// contain the non-primitive causal weathering pass AND the optically feathered dry-dielectric
/// refinement. This prevents a later parent rebuild from silently restoring uniform-alpha Standard
/// materials before native 4K evidence is captured.
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
            QualityBlockFacadeWeatheringOpticalRefinementQA.ValidateContractConfigOnly();
            QualityBlockFacadeWeatheringDetailUpgrade.ApplyToOpenScene();
            QualityBlockFacadeWeatheringOpticalRefinementQA.ApplyToOpenScene();
            QualityBlockFacadeWeatheringDetailUpgrade.ValidateOpenScene();
            QualityBlockFacadeWeatheringOpticalRefinementQA.ValidateOpenScene();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Benchmark save blocked: facade weathering failed causal-geometry and/or optical-refinement QA. " +
                "Hard-edged Standard-alpha residue is not a valid persisted benchmark state.", ex);
        }
        finally
        {
            applying = false;
        }
    }
}
