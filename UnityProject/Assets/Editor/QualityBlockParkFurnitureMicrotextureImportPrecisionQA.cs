using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Preserves benchmark-visible park-furniture normal and metallic/smoothness maps as high-precision
/// physical data. The microdetail generator authors sub-millimetre manufacture/use signals; block
/// compression can quantize normal direction and smoothness before native-4K grazing review.
///
/// Preparation may repair importer state. Formal benchmark camera pre-cull is strictly read-only and
/// fail-closed. This class awards zero Visual Fidelity points without real rendered evidence.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockParkFurnitureMicrotextureImportPrecisionQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/park_furniture_microtexture_import_precision_contract.json";
    private const string SourceContractPath = "Assets/QA/park_furniture_microdetail_contract.json";
    private const string TextureRoot = "Assets/Art/GeneratedParkFurnitureMaterials/Microdetail";
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

    private static readonly string[] TextureStems =
    {
        "Park_PaintedSteel",
        "Park_SlideStainless",
        "Park_ExposedSteel",
        "Park_BenchTimber",
        "Park_PrecastConcrete",
        "Park_ContactConcrete",
        "Park_NoticeBacking",
        "Park_LampDiffuser"
    };

    private static readonly Target[] Targets = BuildTargets();
    private static bool applying;
    private static bool scheduled;

    static QualityBlockParkFurnitureMicrotextureImportPrecisionQA()
    {
        EditorSceneManager.sceneSaved -= OnSceneSaved;
        EditorSceneManager.sceneSaved += OnSceneSaved;
        Camera.onPreCull -= OnCameraPreCull;
        Camera.onPreCull += OnCameraPreCull;
        ScheduleApply();
    }

    [MenuItem("NewTown/Materials/Apply Park Furniture Microtexture Import Precision")]
    public static void ApplyAndValidate()
    {
        ValidateContractConfigOnly();
        RequireGeneratedSet();
        ApplyImporterState();
        ValidatePreparedState();
        Debug.Log(
            "Park-furniture microtexture import precision valid: sixteen 1024 physical-data textures are uncompressed, mipmapped, trilinear and anisotropic. " +
            "Visual Fidelity remains UNSCORED pending native 4K review.");
    }

    [MenuItem("NewTown/QA/Validate Park Furniture Microtexture Import Precision")]
    public static void ValidateFromMenu()
    {
        ValidateContractConfigOnly();
        ValidatePreparedState();
        Debug.Log(
            "Park-furniture microtexture import precision gate passed. This is implementation evidence only; Visual Fidelity remains UNSCORED.");
    }

    public static void ValidateContractConfigOnly()
    {
        if (!File.Exists(ContractPath))
            throw new InvalidOperationException("Missing park-furniture microtexture import precision contract: " + ContractPath);
        if (!File.Exists(SourceContractPath))
            throw new InvalidOperationException("Missing park-furniture physical material contract: " + SourceContractPath);

        string json = File.ReadAllText(ContractPath);
        string[] required =
        {
            "\"schemaVersion\": \"1.0.0\"",
            "\"contractId\": \"park-furniture-microtexture-import-precision-v1\"",
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
            "\"preCullBehavior\": \"READ_ONLY_FAIL_CLOSED\"",
            "\"visualFidelityPointsAwarded\": 0",
            "\"runtimeRenderVerification\": \"PENDING_UNITY_RUNTIME\"",
            "\"passClaimAllowedWithoutRenderedEvidence\": false"
        };

        foreach (string token in required)
        {
            if (json.IndexOf(token, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException(
                    "Park-furniture microtexture import precision contract missing/changed token: " + token);
        }

        string source = File.ReadAllText(SourceContractPath);
        string[] sourceTokens =
        {
            "\"requiredMaterialSets\": 8",
            "\"normalAmplitudeMm\"",
            "\"roughnessRange\"",
            "\"metallic\"",
            "\"wetness\"",
            "\"angularResponse\"",
            "\"visualFidelityPointsAwarded\": 0",
            "PENDING_UNITY_RUNTIME"
        };
        foreach (string token in sourceTokens)
        {
            if (source.IndexOf(token, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException(
                    "Park-furniture physical material contract missing/changed token required by import precision: " + token);
        }
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
            Debug.LogError("Park-furniture microtexture import precision preparation failed: " + ex);
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

        // Formal evidence proves the already-prepared state. Never repair evidence in-flight.
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
                    throw new InvalidOperationException("TextureImporter unavailable for park furniture " + target.Label + ": " + target.Path);

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
            throw new InvalidOperationException("TextureImporter unavailable for park furniture " + target.Label + ": " + target.Path);

        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            throw new InvalidOperationException(
                "Park furniture " + target.Label + " must remain Uncompressed for formal evidence; block compression can alter the approved physical-data signal.");
        if (importer.crunchedCompression)
            throw new InvalidOperationException("Park furniture " + target.Label + " may not use Crunch compression.");
        if (importer.textureType != target.TextureType)
            throw new InvalidOperationException("Park furniture " + target.Label + " texture type drifted from the canonical physical-data interpretation.");
        if (importer.sRGBTexture)
            throw new InvalidOperationException("Park furniture " + target.Label + " is physical data and must remain linear (sRGB off).");
        if (importer.alphaSource != TextureImporterAlphaSource.FromInput)
            throw new InvalidOperationException(
                "Park furniture " + target.Label + " must preserve source alpha; metallic/smoothness maps carry canonical smoothness in alpha.");
        if (importer.maxTextureSize < TextureSize)
            throw new InvalidOperationException("Park furniture " + target.Label + " maxTextureSize is below " + TextureSize + ".");
        if (!importer.mipmapEnabled || importer.wrapMode != TextureWrapMode.Repeat ||
            importer.filterMode != FilterMode.Trilinear || importer.anisoLevel < MinimumAnisotropy)
            throw new InvalidOperationException(
                "Park furniture " + target.Label + " must remain mipmapped, Repeat, Trilinear and anisotropy >= " + MinimumAnisotropy + ".");

        TextureImporterPlatformSettings standalone = importer.GetPlatformTextureSettings("Standalone");
        if (standalone.overridden)
            throw new InvalidOperationException(
                "Standalone platform override may not replace the canonical uncompressed park-furniture " + target.Label + " import state.");

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(target.Path);
        if (texture == null || texture.width != TextureSize || texture.height != TextureSize || texture.mipmapCount <= 1)
            throw new InvalidOperationException(
                "Park furniture " + target.Label + " runtime texture must remain " + TextureSize + "x" + TextureSize + " with mipmaps after import.");
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
            "Park-furniture physical-data microtexture set is incomplete. Build park furniture 4K microdetail before applying import-precision QA.");
    }

    private static Target[] BuildTargets()
    {
        var targets = new Target[TextureStems.Length * 2];
        for (int i = 0; i < TextureStems.Length; i++)
        {
            string stem = TextureStems[i];
            targets[i * 2] = new Target(
                TextureRoot + "/" + stem + "_Normal.png",
                TextureImporterType.NormalMap,
                stem + " normal");
            targets[i * 2 + 1] = new Target(
                TextureRoot + "/" + stem + "_MetallicSmoothness.png",
                TextureImporterType.Default,
                stem + " metallic/smoothness mask");
        }
        return targets;
    }
}

/// <summary>
/// Reasserts canonical import precision after generated park-furniture physical-data textures are
/// created or reimported. Formal camera pre-cull remains validation-only.
/// </summary>
public sealed class QualityBlockParkFurnitureMicrotextureImportPrecisionPostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        QualityBlockParkFurnitureMicrotextureImportPrecisionQA.NotifyImportedAssets(importedAssets);
    }
}
