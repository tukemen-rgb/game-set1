using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Makes construction/material metadata coverage a persisted quality-scene invariant.
/// Any active Renderer parented outside a registered benchmark domain blocks the scene save.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockSceneMetadataCoverageSaveGate
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";

    static QualityBlockSceneMetadataCoverageSaveGate()
    {
        EditorSceneManager.sceneSaving -= OnSceneSaving;
        EditorSceneManager.sceneSaving += OnSceneSaving;
    }

    private static void OnSceneSaving(Scene scene, string path)
    {
        if (!scene.IsValid() || path != ScenePath)
            return;

        GameObject root = GameObject.Find("QualityBlock1990s");
        if (root == null)
            return;

        QualityBlockSceneMetadataCoverageQA.ValidateOpenScene();
    }
}
