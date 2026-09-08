using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persists the vegetation-ecology invariant through the benchmark rebuild/capture chain. Once the
/// detailed tree masters and ground infrastructure exist, every subsequent quality-scene save must
/// include maintained tree pits/understory and pass their structural QA. This keeps 4K evidence from
/// silently reverting to trunks intersecting undifferentiated paving or a vegetation layer without LODs.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockVegetationEcologySaveGate
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private static bool applying;

    static QualityBlockVegetationEcologySaveGate()
    {
        EditorSceneManager.sceneSaving -= OnSceneSaving;
        EditorSceneManager.sceneSaving += OnSceneSaving;
    }

    private static void OnSceneSaving(Scene scene, string path)
    {
        if (applying || path != ScenePath || !scene.IsValid())
            return;

        bool detailedTreesReady = Resources.FindObjectsOfTypeAll<GameObject>()
            .Any(x => x.scene == scene && x.name.StartsWith("HD_TreeMaster_", StringComparison.Ordinal));
        bool detailedGroundReady = Resources.FindObjectsOfTypeAll<GameObject>()
            .Any(x => x.scene == scene && x.name == "GroundHighDetail");
        if (!detailedTreesReady || !detailedGroundReady)
            return;

        applying = true;
        try
        {
            QualityBlockVegetationEcologyUpgrade.ApplyToOpenScene();
            QualityBlockVegetationEcologyUpgrade.ValidateOpenScene();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Benchmark save blocked: vegetation ecology detail failed its required structural QA.", ex);
        }
        finally
        {
            applying = false;
        }
    }
}
