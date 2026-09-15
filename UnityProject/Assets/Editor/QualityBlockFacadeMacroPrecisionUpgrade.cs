using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Preserves the subtle 7.9 m facade anti-repetition field through Unity import. The approved source
/// spans only about 6.6 8-bit sRGB code values from minimum to maximum, so block compression can erase
/// or quantize the signal even when the source PNG itself is exact. Formal evidence therefore requires
/// an uncompressed runtime texture while keeping mipmaps, trilinear filtering and anisotropy.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockFacadeMacroPrecisionUpgrade
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string TexturePath = "Assets/Art/GeneratedFacadeOptics/FacadeRC_AntiRepeatAlbedo.png";
    private const string ContractPath = "Assets/QA/facade_macro_import_precision_contract.json";
    private const int TextureSize = 1024;
    private const int MinimumAnisotropy = 8;

    private static bool applying;
    private static bool scheduled;

    static QualityBlockFacadeMacroPrecisionUpgrade()
    {
        EditorSceneManager.sceneSaved -= OnSceneSaved;
        EditorSceneManager.sceneSaved += OnSceneSaved;
        Camera.onPreCull -= OnCameraPreCull;
        Camera.onPreCull += OnCameraPreCull;
        ScheduleApply();
    }

    [MenuItem("NewTown/Materials/Apply Facade Macro Import Precision")]
    public static void ApplyAndValidate()
    {
        ValidateContractConfigOnly();
        if (!File.Exists(TexturePath))
            throw new InvalidOperationException(
                $"Facade macro source is missing: {TexturePath}. Run the facade anti-repetition preparation first.");
        ApplyImporterState();
        ValidatePreparedState();
        Debug.Log("Facade macro import precision valid: uncompressed 1024 source with mipmaps/trilinear/aniso preserved. Visual Fidelity remains UNSCORED.");
    }

    [MenuItem("NewTown/QA/Validate Facade Macro Import Precision")]
    public static void ValidateFromMenu()
    {
        ValidateContractConfigOnly();
        ValidatePreparedState();
        Debug.Log("Facade macro import precision gate passed. Visual Fidelity remains UNSCORED pending native 4K review.");
    }

    public static void ValidateContractConfigOnly()
    {
        if (!File.Exists(ContractPath))
            throw new InvalidOperationException($"Missing facade macro import precision contract: {ContractPath}");

        string json = File.ReadAllText(ContractPath);
        string[] required =
        {
            "\"schemaVersion\": \"1.0.0\"",
            "\"texturePath\": \"Assets/Art/GeneratedFacadeOptics/FacadeRC_AntiRepeatAlbedo.png\"",
            "\"sourceDynamicRange8BitCodes\": 6.63",
            "\"requiredTextureCompression\": \"Uncompressed\"",
            "\"minimumAnisotropy\": 8",
            "\"mipmapsRequired\": true",
            "\"trilinearRequired\": true",
            "\"visualFidelityPointsAwarded\": 0",
            "\"runtimeRenderVerification\": \"PENDING_UNITY_RUNTIME\""
        };

        foreach (string token in required)
            if (json.IndexOf(token, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException($"Facade macro import precision contract missing token: {token}");
    }

    internal static void NotifyImportedAssets(string[] importedAssets)
    {
        if (importedAssets == null) return;
        for (int i = 0; i < importedAssets.Length; i++)
        {
            if (string.Equals(importedAssets[i], TexturePath, StringComparison.Ordinal))
            {
                ScheduleApply();
                return;
            }
        }
    }

    internal static void ScheduleApply()
    {
        if (scheduled) return;
        scheduled = true;
        EditorApplication.delayCall += ApplyDeferred;
    }

    private static void ApplyDeferred()
    {
        scheduled = false;
        if (applying || !File.Exists(TexturePath)) return;
        try
        {
            ValidateContractConfigOnly();
            ApplyImporterState();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Facade macro import precision preparation failed: {ex}");
        }
    }

    private static void OnSceneSaved(Scene scene)
    {
        if (!scene.IsValid() || scene.path != ScenePath) return;
        ScheduleApply();
    }

    private static void OnCameraPreCull(Camera camera)
    {
        if (camera == null || camera.targetTexture == null) return;
        string targetName = camera.targetTexture.name ?? string.Empty;
        if (!targetName.StartsWith("QA4K_", StringComparison.Ordinal) &&
            !targetName.StartsWith("QATemporal_", StringComparison.Ordinal) &&
            !targetName.StartsWith("QAPreparedTemporal_", StringComparison.Ordinal)) return;

        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath) return;

        ValidateContractConfigOnly();
        ValidatePreparedState();
    }

    private static void ApplyImporterState()
    {
        TextureImporter importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException($"TextureImporter unavailable for facade macro source: {TexturePath}");

        bool changed = false;
        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            changed = true;
        }
        if (importer.crunchedCompression)
        {
            importer.crunchedCompression = false;
            changed = true;
        }
        if (importer.maxTextureSize < TextureSize)
        {
            importer.maxTextureSize = TextureSize;
            changed = true;
        }
        if (!importer.sRGBTexture)
        {
            importer.sRGBTexture = true;
            changed = true;
        }
        if (!importer.mipmapEnabled)
        {
            importer.mipmapEnabled = true;
            changed = true;
        }
        if (importer.wrapMode != TextureWrapMode.Repeat)
        {
            importer.wrapMode = TextureWrapMode.Repeat;
            changed = true;
        }
        if (importer.filterMode != FilterMode.Trilinear)
        {
            importer.filterMode = FilterMode.Trilinear;
            changed = true;
        }
        if (importer.anisoLevel < MinimumAnisotropy)
        {
            importer.anisoLevel = MinimumAnisotropy;
            changed = true;
        }
        if (importer.alphaSource != TextureImporterAlphaSource.None)
        {
            importer.alphaSource = TextureImporterAlphaSource.None;
            changed = true;
        }

        TextureImporterPlatformSettings standalone = importer.GetPlatformTextureSettings("Standalone");
        if (standalone.overridden)
        {
            standalone.overridden = false;
            importer.SetPlatformTextureSettings(standalone);
            changed = true;
        }

        if (!changed) return;

        applying = true;
        try
        {
            importer.SaveAndReimport();
        }
        finally
        {
            applying = false;
        }
    }

    private static void ValidatePreparedState()
    {
        if (!File.Exists(TexturePath))
            throw new InvalidOperationException($"Facade macro source texture is missing: {TexturePath}");

        TextureImporter importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException($"TextureImporter unavailable for facade macro source: {TexturePath}");

        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            throw new InvalidOperationException(
                "Facade macro source must be imported Uncompressed. Its approved modulation spans only about 6.6 8-bit code values, so block compression is not formal-evidence safe.");
        if (importer.crunchedCompression)
            throw new InvalidOperationException("Facade macro source may not use Crunch compression.");
        if (importer.maxTextureSize < TextureSize)
            throw new InvalidOperationException($"Facade macro source maxTextureSize must be at least {TextureSize}.");
        if (!importer.sRGBTexture || !importer.mipmapEnabled || importer.wrapMode != TextureWrapMode.Repeat ||
            importer.filterMode != FilterMode.Trilinear || importer.anisoLevel < MinimumAnisotropy)
            throw new InvalidOperationException(
                "Facade macro source must remain sRGB, mipmapped, Repeat, Trilinear and anisotropy >= 8 after precision upgrade.");

        TextureImporterPlatformSettings standalone = importer.GetPlatformTextureSettings("Standalone");
        if (standalone.overridden)
            throw new InvalidOperationException("Standalone platform override may not replace the canonical uncompressed facade macro import state.");

        OverrideTextureCompression globalOverride = EditorUserBuildSettings.overrideTextureCompression;
        if (globalOverride == OverrideTextureCompression.ForceFastCompressor)
            throw new InvalidOperationException(
                "Formal capture forbids ForceFastCompressor because project-wide texture-import overrides can change evidence pixels.");

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (texture == null || texture.width != TextureSize || texture.height != TextureSize || texture.mipmapCount <= 1)
            throw new InvalidOperationException(
                $"Facade macro runtime texture must remain {TextureSize}x{TextureSize} with mipmaps after import.");
    }
}

/// <summary>
/// Reasserts precision after the source is created or reimported. The actual formal pre-cull path stays
/// read-only: it validates the prepared importer/runtime state rather than silently mutating evidence.
/// </summary>
public sealed class QualityBlockFacadeMacroPrecisionPostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        QualityBlockFacadeMacroPrecisionUpgrade.NotifyImportedAssets(importedAssets);
    }
}
