using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Protects the generated ground micro-normal and metallic/smoothness physical-data maps from
/// block-compression quantization before formal native-4K evidence is captured.
///
/// The ground albedo maps are deliberately outside this gate: their low-frequency colour signal may
/// remain normally compressed. The eight maps covered here encode millimetre-scale tangent-space
/// orientation plus bounded metallic/smoothness data, so the benchmark path keeps them Uncompressed.
/// Preparation may repair importer state. Formal camera pre-cull is read-only and fail-closed.
/// No Visual Fidelity points are awarded by this gate.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockGroundMicrotextureImportPrecisionQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/ground_microdetail_contract.json";
    private const string TextureRoot = "Assets/Art/GeneratedGroundMaterials/Microdetail";
    private const int TextureSize = 1024;
    private const int MinimumAnisotropy = 8;

    private readonly struct Target
    {
        public readonly string Path;
        public readonly TextureImporterType TextureType;
        public readonly string Label;

        public Target(string path, TextureImporterType textureType, string label)
        {
            Path = path;
            TextureType = textureType;
            Label = label;
        }
    }

    private static readonly Target[] Targets =
    {
        Normal("Ground_GalvanizedSteel"),
        Mask("Ground_GalvanizedSteel"),
        Normal("Ground_CastIron"),
        Mask("Ground_CastIron"),
        Normal("Ground_MineralJoint"),
        Mask("Ground_MineralJoint"),
        Normal("Ground_DampConcrete"),
        Mask("Ground_DampConcrete")
    };

    private static bool applying;
    private static bool scheduled;

    static QualityBlockGroundMicrotextureImportPrecisionQA()
    {
        EditorSceneManager.sceneSaved -= OnSceneSaved;
        EditorSceneManager.sceneSaved += OnSceneSaved;
        Camera.onPreCull -= OnCameraPreCull;
        Camera.onPreCull += OnCameraPreCull;
        ScheduleApply();
    }

    [MenuItem("NewTown/Materials/Apply Ground Microtexture Import Precision")]
    public static void ApplyAndValidate()
    {
        ValidateContractConfigOnly();
        RequireGeneratedSet();
        ApplyImporterState();
        ValidatePreparedState();
        Debug.Log(
            "Ground microtexture import precision valid: eight 1024 physical-data textures are uncompressed, mipmapped, trilinear and anisotropic. " +
            "Ground albedo remains outside this lossless requirement. Visual Fidelity remains UNSCORED pending native 4K review.");
    }

    [MenuItem("NewTown/QA/Validate Ground Microtexture Import Precision")]
    public static void ValidateFromMenu()
    {
        ValidateContractConfigOnly();
        ValidatePreparedState();
        Debug.Log(
            "Ground microtexture import precision gate passed. This is implementation evidence only; Visual Fidelity remains UNSCORED.");
    }

    public static void ValidateContractConfigOnly()
    {
        if (!File.Exists(ContractPath))
            throw new InvalidOperationException("Missing ground microdetail contract: " + ContractPath);

        string json = File.ReadAllText(ContractPath);
        string[] required =
        {
            "\"schemaVersion\": \"1.1.0\"",
            "\"textureCompression\": \"Uncompressed\"",
            "\"crunchedCompression\": false",
            "\"standalonePlatformOverrideAllowed\": false",
            "\"minimumMaxTextureSize\": 1024",
            "\"mipmapsRequired\": true",
            "\"filterMode\": \"Trilinear\"",
            "\"wrapMode\": \"Repeat\"",
            "\"minimumAnisotropy\": 8",
            "\"sRGB\": false",
            "\"normalTextureType\": \"NormalMap\"",
            "\"metallicSmoothnessTextureType\": \"Default\"",
            "\"alphaSource\": \"FromInput\"",
            "\"visualFidelityPointsAwarded\": 0",
            "\"runtimeRenderVerification\": \"PENDING_UNITY_RUNTIME\""
        };

        foreach (string token in required)
            if (json.IndexOf(token, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException(
                    "Ground microtexture import precision contract missing/changed token: " + token);
    }

    internal static void NotifyImportedAssets(string[] importedAssets)
    {
        if (applying || importedAssets == null) return;
        for (int i = 0; i < importedAssets.Length; i++)
        {
            string imported = importedAssets[i];
            for (int t = 0; t < Targets.Length; t++)
            {
                if (!string.Equals(imported, Targets[t].Path, StringComparison.Ordinal)) continue;
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
        if (applying || !GeneratedSetExists()) return;
        try
        {
            ValidateContractConfigOnly();
            ApplyImporterState();
        }
        catch (Exception ex)
        {
            Debug.LogError("Ground microtexture import precision preparation failed: " + ex);
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

        // Deliberately read-only. Formal evidence must fail rather than repair importer state in-flight.
        ValidateContractConfigOnly();
        ValidatePreparedState();
    }

    private static void ApplyImporterState()
    {
        RequireGeneratedSet();
        applying = true;
        try
        {
            for (int i = 0; i < Targets.Length; i++)
            {
                Target target = Targets[i];
                TextureImporter importer = AssetImporter.GetAtPath(target.Path) as TextureImporter;
                if (importer == null)
                    throw new InvalidOperationException("TextureImporter unavailable for ground " + target.Label + ": " + target.Path);

                bool changed = false;
                if (importer.textureType != target.TextureType)
                {
                    importer.textureType = target.TextureType;
                    changed = true;
                }
                if (importer.sRGBTexture)
                {
                    importer.sRGBTexture = false;
                    changed = true;
                }
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
                if (importer.alphaSource != TextureImporterAlphaSource.FromInput)
                {
                    importer.alphaSource = TextureImporterAlphaSource.FromInput;
                    changed = true;
                }
                if (importer.maxTextureSize < TextureSize)
                {
                    importer.maxTextureSize = TextureSize;
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
        RequireGeneratedSet();
        for (int i = 0; i < Targets.Length; i++)
            ValidateTexture(Targets[i]);
    }

    private static void ValidateTexture(Target target)
    {
        TextureImporter importer = AssetImporter.GetAtPath(target.Path) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException("TextureImporter unavailable for ground " + target.Label + ": " + target.Path);

        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            throw new InvalidOperationException(
                "Ground " + target.Label + " must remain Uncompressed for formal evidence; block compression can alter the approved physical-data signal.");
        if (importer.crunchedCompression)
            throw new InvalidOperationException("Ground " + target.Label + " may not use Crunch compression.");
        if (importer.textureType != target.TextureType)
            throw new InvalidOperationException("Ground " + target.Label + " texture type drifted from the canonical physical-data interpretation.");
        if (importer.sRGBTexture)
            throw new InvalidOperationException("Ground " + target.Label + " is physical data and must remain linear (sRGB off).");
        if (importer.alphaSource != TextureImporterAlphaSource.FromInput)
            throw new InvalidOperationException(
                "Ground " + target.Label + " must preserve source alpha; metallic/smoothness masks carry canonical smoothness in alpha.");
        if (importer.maxTextureSize < TextureSize)
            throw new InvalidOperationException("Ground " + target.Label + " maxTextureSize is below " + TextureSize + ".");
        if (!importer.mipmapEnabled || importer.wrapMode != TextureWrapMode.Repeat ||
            importer.filterMode != FilterMode.Trilinear || importer.anisoLevel < MinimumAnisotropy)
            throw new InvalidOperationException(
                "Ground " + target.Label + " must remain mipmapped, Repeat, Trilinear and anisotropy >= " + MinimumAnisotropy + ".");

        TextureImporterPlatformSettings standalone = importer.GetPlatformTextureSettings("Standalone");
        if (standalone.overridden)
            throw new InvalidOperationException(
                "Standalone platform override may not replace the canonical uncompressed ground " + target.Label + " import state.");

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(target.Path);
        if (texture == null || texture.width != TextureSize || texture.height != TextureSize || texture.mipmapCount <= 1)
            throw new InvalidOperationException(
                "Ground " + target.Label + " runtime texture must remain " + TextureSize + "x" + TextureSize + " with mipmaps after import.");
    }

    private static bool GeneratedSetExists()
    {
        for (int i = 0; i < Targets.Length; i++)
            if (!File.Exists(Targets[i].Path)) return false;
        return true;
    }

    private static void RequireGeneratedSet()
    {
        if (GeneratedSetExists()) return;
        throw new InvalidOperationException(
            "Ground physical-data microtexture set is incomplete. Build metric-scale ground microdetail before applying import-precision QA.");
    }

    private static Target Normal(string stem)
    {
        return new Target(TextureRoot + "/" + stem + "_Normal.png", TextureImporterType.NormalMap, stem + " micro-normal");
    }

    private static Target Mask(string stem)
    {
        return new Target(TextureRoot + "/" + stem + "_MetallicSmoothness.png", TextureImporterType.Default, stem + " metallic/smoothness");
    }
}

/// <summary>
/// Reasserts canonical import precision after any generated ground physical-data texture is created
/// or reimported. Formal camera pre-cull remains validation-only.
/// </summary>
public sealed class QualityBlockGroundMicrotextureImportPrecisionPostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        QualityBlockGroundMicrotextureImportPrecisionQA.NotifyImportedAssets(importedAssets);
    }
}
