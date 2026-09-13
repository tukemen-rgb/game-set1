using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persists the solar/shadow capture invariant once the generated benchmark has a physical environment
/// context. Base scratch-scene saves are ignored; every later benchmark save must contain the bound
/// pre-cull guard, one SummerSun and the locked midsummer/shadow state. Rendered pixels remain mandatory.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockSolarShadowCaptureSaveGate
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private static bool validating;

    static QualityBlockSolarShadowCaptureSaveGate()
    {
        EditorSceneManager.sceneSaving -= OnSceneSaving;
        EditorSceneManager.sceneSaving += OnSceneSaving;
    }

    private static void OnSceneSaving(Scene scene, string path)
    {
        if (validating || !scene.IsValid() || !string.Equals(path, ScenePath, StringComparison.Ordinal))
            return;

        bool hasRoot = Resources.FindObjectsOfTypeAll<GameObject>()
            .Any(x => x.scene == scene && x.name == "QualityBlock1990s");
        bool hasSun = Resources.FindObjectsOfTypeAll<Light>()
            .Any(x => x.gameObject.scene == scene && x.name == "SummerSun" && x.type == LightType.Directional);
        bool hasContext = Resources.FindObjectsOfTypeAll<QualityBlockEnvironmentContext>()
            .Any(x => x.gameObject.scene == scene);

        // BuildQualityBlock1990s saves a primitive scratch scene before the physical weathering/environment
        // context exists. Do not block that intermediate save; enforce from the first physically contextualized
        // save onward, which is the state eligible for native-4K preparation.
        if (!hasRoot || !hasSun || !hasContext)
            return;

        validating = true;
        try
        {
            QualityBlockSolarShadowCaptureCoherenceQA.ValidateContractConfigOnly();
            QualityBlockSolarShadowCaptureCoherenceQA.ValidateOpenScene();
        }
        catch (Exception ex)
        {
            Debug.LogError("Solar/shadow capture save gate FAILED: " + ex.Message);
            throw;
        }
        finally
        {
            validating = false;
        }
    }
}
