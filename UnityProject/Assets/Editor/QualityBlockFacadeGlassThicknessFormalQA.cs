using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Formal-render binding for the generated facade glass-thickness construction.
/// Reflection/environment preparation saves the scene after the facade optics rebuild; the companion
/// upgrade's sceneSaving hook materializes the 4 mm edge shells at that boundary. This guard then
/// revalidates the persisted construction before every benchmark MainCamera cull so deletion, material
/// substitution or transform drift cannot produce scoreable-looking evidence.
///
/// Passing this guard is implementation/evidence-integrity proof only. It awards zero Visual Fidelity points.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockFacadeGlassThicknessFormalQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";

    static QualityBlockFacadeGlassThicknessFormalQA()
    {
        Camera.onPreCull -= ValidateBeforeBenchmarkCameraCull;
        Camera.onPreCull += ValidateBeforeBenchmarkCameraCull;
    }

    [MenuItem("NewTown/QA/Validate Facade Glass Thickness Formal Binding")]
    public static void ValidateOpenScene()
    {
        QualityBlockFacadeGlassThicknessUpgrade.ValidateOpenScene();
        Debug.Log(
            "Facade glass-thickness formal binding passed. This is source/scene implementation evidence only; " +
            "actual native-4K oblique/grazing pixels and temporal review remain required, and Visual Fidelity is UNSCORED.");
    }

    private static void ValidateBeforeBenchmarkCameraCull(Camera camera)
    {
        if (camera == null || camera.gameObject == null || !camera.gameObject.scene.IsValid())
            return;
        if (!string.Equals(camera.gameObject.scene.path, ScenePath, StringComparison.Ordinal))
            return;
        if (!string.Equals(camera.name, "MainCamera", StringComparison.Ordinal))
            return;

        try
        {
            QualityBlockFacadeGlassThicknessUpgrade.ValidateOpenScene();
        }
        catch (Exception ex)
        {
            // Fail closed before the authoritative camera renders. In particular, do not allow the
            // old broad optical plane by itself to masquerade as verified 4 mm construction.
            throw new InvalidOperationException(
                "Formal benchmark render blocked by facade glass-thickness construction QA: " + ex.Message, ex);
        }
    }
}
