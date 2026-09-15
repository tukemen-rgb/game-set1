using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps the period-authentic rooftop receiving assembly as a persisted benchmark invariant once
/// DanchiHighDetail exists. The initial primitive scaffold save is intentionally ignored; later
/// quality saves rebuild and validate the year-2000 service silhouette before the scene is written.
/// The same save now also blocks a nominally-correct rooftop asset that falls outside or becomes too
/// small in the exact native 4K hero/oblique benchmark framing.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockPeriodAuthenticitySaveGate
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private static bool applying;

    static QualityBlockPeriodAuthenticitySaveGate()
    {
        EditorSceneManager.sceneSaving -= OnSceneSaving;
        EditorSceneManager.sceneSaving += OnSceneSaving;
    }

    private static void OnSceneSaving(Scene scene, string path)
    {
        if (applying || path != ScenePath || !scene.IsValid()) return;

        bool detailReady = Resources.FindObjectsOfTypeAll<GameObject>()
            .Any(x => x.scene == scene && x.name == "DanchiHighDetail");
        if (!detailReady) return;

        applying = true;
        try
        {
            QualityBlockPeriodAuthenticityUpgrade.ApplyToOpenScene();
            QualityBlockPeriodAuthenticityUpgrade.ValidateOpenScene();
            QualityBlockBenchmarkObservabilityQA.ValidateOpenScene();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Benchmark save blocked: year-2000 period-authentic rooftop service/observability QA failed.", ex);
        }
        finally
        {
            applying = false;
        }
    }
}
