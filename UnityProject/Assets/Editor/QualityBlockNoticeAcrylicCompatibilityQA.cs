using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Resolves the overlap between the notice-board acrylic contract (roughness 0.08-0.15) and the aggregate
/// park-furniture physicality floor (roughness >= 0.12). The original 0.89 smoothness was legal for the
/// acrylic-specific contract but produced roughness 0.11, so the aggregate save gate could reject an
/// otherwise valid prepared scene. A 0.875 smoothness / 0.125 roughness target remains inside the acrylic
/// contract while preserving a clean dielectric grazing reflection with slight wiping haze.
///
/// Formal and reflection-camera callbacks are read-only. They never repair the material at render time;
/// drift is rejected so a stale or impossible acrylic state cannot be used to produce admissible evidence.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockNoticeAcrylicCompatibilityQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string MaterialPath =
        "Assets/Art/GeneratedParkFurnitureMaterials/NoticeBoard/PBR_NoticeClearAcrylic.mat";
    private const float TargetSmoothness = 0.875f;
    private static bool validatingCamera;

    static QualityBlockNoticeAcrylicCompatibilityQA()
    {
        Camera.onPreCull -= OnCameraPreCull;
        Camera.onPreCull += OnCameraPreCull;
    }

    [MenuItem("NewTown/Materials/Normalize Notice Acrylic Physical Range")]
    public static void ApplyAndValidate()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
            throw new InvalidOperationException(
                "Notice acrylic material missing; reconstruct the notice-board display case first.");

        if (!material.HasProperty("_Glossiness") || !material.HasProperty("_Metallic"))
            throw new InvalidOperationException("Notice acrylic shader lacks required Standard PBR properties.");

        material.SetFloat("_Glossiness", TargetSmoothness);
        material.SetFloat("_Metallic", 0f);
        material.DisableKeyword("_EMISSION");
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        Validate();
    }

    [MenuItem("NewTown/QA/Validate Notice Acrylic Aggregate Physical Range")]
    public static void Validate()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
            throw new InvalidOperationException("Notice acrylic material missing.");

        float smoothness = material.HasProperty("_Glossiness") ? material.GetFloat("_Glossiness") : -1f;
        float roughness = 1f - smoothness;
        float metallic = material.HasProperty("_Metallic") ? material.GetFloat("_Metallic") : 1f;
        if (Mathf.Abs(smoothness - TargetSmoothness) > 0.001f)
            throw new InvalidOperationException(
                $"Notice acrylic smoothness drifted from the cross-gate-compatible target: {smoothness:F4}.");
        if (roughness < 0.12f || roughness > 0.15f)
            throw new InvalidOperationException(
                $"Notice acrylic roughness must satisfy both aggregate and acrylic contracts: {roughness:F4}.");
        if (metallic > 0.01f)
            throw new InvalidOperationException($"Notice acrylic must remain dielectric; metallic={metallic:F4}.");
        if (material.IsKeywordEnabled("_EMISSION") ||
            (material.HasProperty("_EmissionColor") && material.GetColor("_EmissionColor").maxColorComponent > 0.001f))
            throw new InvalidOperationException("Notice acrylic cannot use emission to fake brightness.");

        Debug.Log(
            "Notice acrylic cross-gate physicality QA passed: smoothness=0.875, roughness=0.125, metallic=0. " +
            "This is source validation only and awards zero Visual Fidelity points.");
    }

    private static void OnCameraPreCull(Camera camera)
    {
        if (validatingCamera || camera == null) return;
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath) return;
        if (!IsFormalCamera(camera)) return;

        validatingCamera = true;
        try
        {
            Validate();
        }
        finally
        {
            validatingCamera = false;
        }
    }

    private static bool IsFormalCamera(Camera camera)
    {
        if (camera.cameraType == CameraType.Reflection) return true;
        string n = camera.name ?? string.Empty;
        return n.StartsWith("QA4K_", StringComparison.Ordinal) ||
               n.StartsWith("QATemporal_", StringComparison.Ordinal) ||
               n.StartsWith("QAPreparedTemporal_", StringComparison.Ordinal);
    }
}
