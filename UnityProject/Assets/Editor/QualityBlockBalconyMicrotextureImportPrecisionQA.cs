using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Preserves the approved balcony waterproof micro-normal and metallic/smoothness signals through
/// Unity import. The source pair carries deliberately subtle millimetre-scale tangent-space structure
/// and dielectric roughness variation; block compression is therefore treated as a formal-evidence risk.
///
/// Preparation may repair importer state before a benchmark run. Formal camera pre-cull is read-only:
/// if the imported runtime textures are not canonical, capture is blocked rather than silently repaired.
/// This gate awards zero Visual Fidelity points; only native 4K pixels can establish visible quality.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockBalconyMicrotextureImportPrecisionQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/balcony_microtexture_import_precision_contract.json";
    private const int MinimumAnisotropy = 8;

    private static readonly string[] TexturePaths =
    {
        QualityBlockBalconySurfaceMicrostructureUpgrade.NormalTexturePath,
        QualityBlockBalconySurfaceMicrostructureUpgrade.MetallicSmoothnessTexturePath
    };

    private static bool applying;
    private static bool scheduled;

    static QualityBlockBalconyMicrotextureImportPrecisionQA()
    {
        EditorSceneManager.sceneSaved -= OnSceneSaved;
        EditorSceneManager.sceneSaved += OnSceneSaved;
        Camera.onPreCull -= OnCameraPreCull;
        Camera.onPreCull += OnCameraPreCull;
        ScheduleApply();
    }

    [MenuItem("NewTown/Materials/Apply Balcony Microtexture Import Precision")]
    public static void ApplyAndValidate()
    {
        ValidateContractConfigOnly();
        RequireGeneratedPair();
        ApplyImporterState();
        ValidatePreparedState();
        Debug.Log(
            "Balcony microtexture import precision valid: both 1024 physical-data textures are uncompressed, mipmapped, trilinear and anisotropic. " +
            "Visual Fidelity remains UNSCORED pending native 4K review.");
    }

    [MenuItem("NewTown/QA/Validate Balcony Microtexture Import Precision")]
    public static void ValidateFromMenu()
    {
        ValidateContractConfigOnly();
        ValidatePreparedState();
        Debug.Log(
            "Balcony microtexture import precision gate passed. This is implementation evidence only; Visual Fidelity remains UNSCORED.");
    }

    public static void ValidateContractConfigOnly()
    {
        if (!File.Exists(ContractPath))
            throw new InvalidOperationException("Missing balcony microtexture import precision contract: " + ContractPath);

        string json = File.ReadAllText(ContractPath);
        string[] required =
        {
            "\"schemaVersion\": \"1.0.0\"",
            "\"contractId\": \"balcony-microtexture-import-precision-v1\"",
            "\"textureCompression\": \"Uncompressed\"",
            "\"crunchedCompression\": false",
            "\"minimumMaxTextureSize\": 1024",
            "\"mipmapsRequired\": true",
            "\"filterMode\": \"Trilinear\"",
            "\"wrapMode\": \"Repeat\"",
            "\"minimumAnisotropy\": 8",
            "\"standalonePlatformOverrideAllowed\": false",
            "\"sRGB\": false",
            "\"visualFidelityPointsAwarded\": 0",
            "\"runtimeRenderVerification\": \"PENDING_UNITY_RUNTIME\""
        };

        foreach (string token in required)
            if (json.IndexOf(token, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException(
                    "Balcony microtexture import precision contract missing/changed token: " + token);
    }

    internal static void NotifyImportedAssets(string[] importedAssets)
    {
        if (applying || importedAssets == null) return;
        for (int i = 0; i < importedAssets.Length; i++)
        {
            string imported = importedAssets[i];
            for (int p = 0; p < TexturePaths.Length; p++)
            {
                if (!string.Equals(imported, TexturePaths[p], StringComparison.Ordinal)) continue;
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
        if (applying || !GeneratedPairExists()) return;
        try
        {
            ValidateContractConfigOnly();
            ApplyImporterState();
        }
        catch (Exception ex)
        {
            Debug.LogError("Balcony microtexture import precision preparation failed: " + ex);
        }
    }

    private static void OnSceneSaved(Scene scene)
    {
        if (!scene.IsValid() || scene.path != ScenePath) return;
        ScheduleApply();
    }

    private static void OnCameraPreCull(Camera camera)
    {
        if (camera == null || camera.targetTexture == null || camera.gameObject == null ||
            !camera.gameObject.scene.IsValid() || camera.gameObject.scene.path != ScenePath)
            return;

        string targetName = camera.targetTexture.name ?? string.Empty;
        if (!targetName.StartsWith("QA4K_", StringComparison.Ordinal) &&
            !targetName.StartsWith("QATemporal_", StringComparison.Ordinal) &&
            !targetName.StartsWith("QAPreparedTemporal_", StringComparison.Ordinal))
            return;

        if (QualityBlockBalconyDrainageWaterproofingUpgrade.IsAuthoredDanchiActive(camera.gameObject.scene))
            return;

        // Deliberately read-only. Do not repair import state while formal pixels are being produced.
        ValidateContractConfigOnly();
        ValidatePreparedState();
    }

    private static void ApplyImporterState()
    {
        RequireGeneratedPair();
        applying = true;
        try
        {
            for (int i = 0; i < TexturePaths.Length; i++)
            {
                string path = TexturePaths[i];
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                    throw new InvalidOperationException("TextureImporter unavailable for balcony microtexture: " + path);

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
                if (importer.maxTextureSize < QualityBlockBalconySurfaceMicrostructureUpgrade.TextureSize)
                {
                    importer.maxTextureSize = QualityBlockBalconySurfaceMicrostructureUpgrade.TextureSize;
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

                TextureImporterPlatformSettings standalone = importer.GetPlatformTextureSettings("Standalone");
                if (standalone.overridden)
                {
                    standalone.overridden = false;
                    importer.SetPlatformTextureSettings(standalone);
                    changed = true;
                }

                if (changed) importer.SaveAndReimport();
            }
        }
        finally
        {
            applying = false;
        }
    }

    private static void ValidatePreparedState()
    {
        RequireGeneratedPair();
        ValidateTexture(
            QualityBlockBalconySurfaceMicrostructureUpgrade.NormalTexturePath,
            TextureImporterType.NormalMap,
            "micro-normal");
        ValidateTexture(
            QualityBlockBalconySurfaceMicrostructureUpgrade.MetallicSmoothnessTexturePath,
            TextureImporterType.Default,
            "metallic/smoothness");
    }

    private static void ValidateTexture(string path, TextureImporterType expectedType, string label)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException("TextureImporter unavailable for balcony " + label + ": " + path);

        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            throw new InvalidOperationException(
                "Balcony " + label + " must remain Uncompressed for formal evidence; block compression can alter the approved subtle physical-data signal.");
        if (importer.crunchedCompression)
            throw new InvalidOperationException("Balcony " + label + " may not use Crunch compression.");
        if (importer.maxTextureSize < QualityBlockBalconySurfaceMicrostructureUpgrade.TextureSize)
            throw new InvalidOperationException(
                "Balcony " + label + " maxTextureSize is below " + QualityBlockBalconySurfaceMicrostructureUpgrade.TextureSize + ".");
        if (importer.textureType != expectedType)
            throw new InvalidOperationException("Balcony " + label + " texture type drifted from the canonical physical-data interpretation.");
        if (importer.sRGBTexture)
            throw new InvalidOperationException("Balcony " + label + " is physical data and must remain linear (sRGB off).");
        if (importer.alphaSource != TextureImporterAlphaSource.FromInput)
            throw new InvalidOperationException(
                "Balcony " + label + " must preserve source alpha; the mask alpha carries canonical smoothness data.");
        if (!importer.mipmapEnabled || importer.wrapMode != TextureWrapMode.Repeat ||
            importer.filterMode != FilterMode.Trilinear || importer.anisoLevel < MinimumAnisotropy)
            throw new InvalidOperationException(
                "Balcony " + label + " must remain mipmapped, Repeat, Trilinear and anisotropy >= " + MinimumAnisotropy + ".");

        TextureImporterPlatformSettings standalone = importer.GetPlatformTextureSettings("Standalone");
        if (standalone.overridden)
            throw new InvalidOperationException(
                "Standalone platform override may not replace the canonical uncompressed balcony " + label + " import state.");

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        int size = QualityBlockBalconySurfaceMicrostructureUpgrade.TextureSize;
        if (texture == null || texture.width != size || texture.height != size || texture.mipmapCount <= 1)
            throw new InvalidOperationException(
                "Balcony " + label + " runtime texture must remain " + size + "x" + size + " with mipmaps after import.");
    }

    private static bool GeneratedPairExists()
    {
        return File.Exists(TexturePaths[0]) && File.Exists(TexturePaths[1]);
    }

    private static void RequireGeneratedPair()
    {
        if (GeneratedPairExists()) return;
        throw new InvalidOperationException(
            "Balcony microtexture pair is missing. Build the drainage/waterproof microstructure before applying import-precision QA.");
    }
}

/// <summary>
/// Reasserts canonical import precision after either generated source texture is created/reimported.
/// Formal camera pre-cull remains validation-only.
/// </summary>
public sealed class QualityBlockBalconyMicrotextureImportPrecisionPostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        QualityBlockBalconyMicrotextureImportPrecisionQA.NotifyImportedAssets(importedAssets);
    }
}
