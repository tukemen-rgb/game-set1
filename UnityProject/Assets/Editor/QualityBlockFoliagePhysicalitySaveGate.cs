using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Prevents the persisted 4K benchmark from drifting back to an arbitrary foliage specular model.
/// Once generated high-detail tree masters exist, every quality-scene save must retain the measured-
/// order dielectric leaf material constraints. This is source/pipeline enforcement only; it does not
/// clear foliage visual quality without actual sealed 4K still and temporal evidence.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockFoliagePhysicalitySaveGate
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private static bool applying;

    static QualityBlockFoliagePhysicalitySaveGate()
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
        if (!detailedTreesReady)
            return;

        applying = true;
        try
        {
            QualityBlockFoliagePhysicalityQA.ApplyAndValidateOpenScene();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Benchmark save blocked: foliage dielectric BRDF/material physicality failed its required source QA.", ex);
        }
        finally
        {
            applying = false;
        }
    }
}
