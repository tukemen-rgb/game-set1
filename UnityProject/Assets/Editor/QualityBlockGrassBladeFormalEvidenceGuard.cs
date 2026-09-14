using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Covers the canonical QualityCamera path used by QualityBlock4KCapture. The grass blade builder
/// already guards reflection and explicitly named QA/temporal cameras; this companion keeps the main
/// benchmark camera read-only/fail-closed as well without mutating the central capture pipeline.
/// It awards zero Visual Fidelity points and never repairs scene state from a render callback.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockGrassBladeFormalEvidenceGuard
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string MainCameraName = "QualityCamera";

    static QualityBlockGrassBladeFormalEvidenceGuard()
    {
        Camera.onPreCull -= OnCameraPreCull;
        Camera.onPreCull += OnCameraPreCull;
    }

    [MenuItem("NewTown/QA/Validate Grass Blade Main-Camera Evidence Guard")]
    public static void ValidateOpenScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() ||
            EditorSceneManager.GetActiveScene().path != ScenePath)
            throw new InvalidOperationException("Grass main-camera evidence guard requires the benchmark scene.");

        Camera main = Camera.main;
        if (main == null || !string.Equals(main.name, MainCameraName, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Canonical benchmark MainCamera must remain '{MainCameraName}' before grass evidence validation.");

        QualityBlockGrassBladeFieldUpgrade.ValidateOpenScene(false);
    }

    private static void OnCameraPreCull(Camera camera)
    {
        if (camera == null || !string.Equals(camera.name, MainCameraName, StringComparison.Ordinal))
            return;
        if (!EditorSceneManager.GetActiveScene().IsValid() ||
            EditorSceneManager.GetActiveScene().path != ScenePath)
            return;

        // Read-only evidence boundary: source generation is forbidden here.
        QualityBlockGrassBladeFieldUpgrade.ValidateOpenScene(false);
    }
}
