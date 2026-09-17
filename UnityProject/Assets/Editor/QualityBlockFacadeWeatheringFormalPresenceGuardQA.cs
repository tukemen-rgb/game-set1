using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Closes the formal-render absence case for facade weathering causality QA.
///
/// The detailed spatial-causality validator owns per-vertex/source checks once the weathering root
/// exists. This independent pre-cull guard makes the complementary state fail closed: a formal
/// benchmark MainCamera may not render if the required facade-weathering root is missing entirely.
/// Passing this guard awards zero Visual Fidelity points.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockFacadeWeatheringFormalPresenceGuardQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string RootName = "FacadeWeatheringDetail";

    static QualityBlockFacadeWeatheringFormalPresenceGuardQA()
    {
        Camera.onPreCull -= ValidateBeforeBenchmarkCameraCull;
        Camera.onPreCull += ValidateBeforeBenchmarkCameraCull;
    }

    [MenuItem("NewTown/QA/Validate Facade Weathering Formal Presence")]
    public static void ValidateOpenScene()
    {
        RequireWeatheringRoot();
        Debug.Log(
            "Facade weathering formal-presence QA passed. This is implementation/readiness evidence only; " +
            "native 4K pixels remain required and Visual Fidelity is UNSCORED.");
    }

    private static void ValidateBeforeBenchmarkCameraCull(Camera camera)
    {
        if (camera == null || camera.gameObject == null || !camera.gameObject.scene.IsValid())
            return;
        if (!string.Equals(camera.gameObject.scene.path, ScenePath, StringComparison.Ordinal))
            return;
        if (!string.Equals(camera.name, "MainCamera", StringComparison.Ordinal))
            return;

        RequireWeatheringRoot();
    }

    private static void RequireWeatheringRoot()
    {
        GameObject root = Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x.scene.IsValid() && string.Equals(x.name, RootName, StringComparison.Ordinal));
        if (root == null)
            throw new InvalidOperationException(
                "Formal benchmark render blocked: FacadeWeatheringDetail is missing, so weathering/context causality cannot be verified. " +
                "Rebuild the benchmark scene and run facade-weathering spatial-causality QA before rendering.");
    }
}
