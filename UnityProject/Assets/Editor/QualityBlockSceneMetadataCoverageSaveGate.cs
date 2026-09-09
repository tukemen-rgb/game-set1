using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Makes construction/material metadata coverage a persisted quality-scene invariant.
/// Any active Renderer parented outside a registered benchmark domain blocks a subsequent scene save.
/// The first save of a newly-created scene is intentionally allowed so the quality build chain can
/// establish the canonical scene path; all later saves of the persisted benchmark are gated.
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

        // During BuildQualityBlock1990s' first SaveScene, scene.path may still be empty while
        // the event already carries the target path. Opening/validating recursively at that point
        // would interfere with scene creation. The quality chain immediately reopens and saves the
        // persisted scene after its detail passes, where this gate becomes mandatory.
        if (string.IsNullOrEmpty(scene.path))
            return;

        GameObject root = GameObject.Find("QualityBlock1990s");
        if (root == null)
            return;

        QualityBlockSceneMetadataCoverageQA.ValidateOpenScene();
    }
}
