using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Normalizes and validates texture sampling for generated benchmark materials.
///
/// Two source-side failure modes are addressed here:
/// 1) a generated Standard material can tile _MainTex while leaving its normal / metallic-smoothness
///    maps at 1x, making one manufactured surface describe different physical feature sizes per map;
/// 2) fine 4K microdetail can enter the benchmark without a mipmapped, trilinear, anisotropic import
///    policy and then crawl or sparkle under oblique motion.
///
/// This is implementation QA only. It cannot clear shimmer/moire or award Visual Fidelity points;
/// those decisions still require the sealed native 4K still and temporal evidence.
/// </summary>
public static class QualityBlockTextureSamplingUpgrade
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/texture_sampling_contract.json";
    private const string ManagedMaterialPrefix = "Assets/Art/Generated";
    private const int MinimumTextureDimension = 512;
    private const int MinimumAnisotropy = 8;
    private const float UvTolerance = 0.0001f;

    [Flags]
    private enum MapRole
    {
        None = 0,
        Albedo = 1,
        Normal = 2,
        MetallicSmoothness = 4,
    }

    private sealed class TextureUse
    {
        public Texture2D Texture;
        public string Path;
        public MapRole Roles;
        public readonly HashSet<string> Materials = new HashSet<string>(StringComparer.Ordinal);
    }

    [MenuItem("NewTown/Materials/Apply 4K Texture Sampling + Map Registration")]
    public static void ApplyAndValidate()
    {
        ValidateContractConfigOnly();
        RequireQualityScene();

        Material[] materials = CollectManagedMaterials();
        if (materials.Length < 6)
            throw new InvalidOperationException(
                $"Texture sampling pass found too few active generated materials ({materials.Length}); expected the detailed benchmark chain first.");

        int registeredMaterials = 0;
        foreach (Material material in materials)
            if (NormalizeStandardMapRegistration(material)) registeredMaterials++;

        Dictionary<string, TextureUse> uses = CollectTextureUses(materials);
        if (uses.Count < 10)
            throw new InvalidOperationException(
                $"Texture sampling pass found too few active generated texture assets ({uses.Count}); expected the PBR benchmark library first.");

        int reimported = 0;
        foreach (TextureUse use in uses.Values)
            if (NormalizeImporter(use)) reimported++;

        // Per-texture anisotropy is meaningful only when the quality setting is not globally disabled.
        // 'Enable' preserves per-texture levels instead of indiscriminately forcing anisotropy on every texture.
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ValidateOpenScene();
        Debug.Log(
            $"4K texture sampling applied: generatedMaterials={materials.Length}, mapRegistered={registeredMaterials}, " +
            $"managedTextures={uses.Count}, reimported={reimported}. Visual Fidelity remains UNSCORED until sealed native 4K evidence is reviewed.");
    }

    [MenuItem("NewTown/QA/Validate 4K Texture Sampling + Map Registration")]
    public static void ValidateOpenScene()
    {
        ValidateContractConfigOnly();
        RequireQualityScene();

        var errors = new List<string>();
        Material[] materials = CollectManagedMaterials();
        if (materials.Length < 6)
            errors.Add($"Too few active generated materials for meaningful sampling QA: {materials.Length} < 6.");

        foreach (Material material in materials)
            ValidateMapRegistration(material, errors);

        Dictionary<string, TextureUse> uses = CollectTextureUses(materials);
        if (uses.Count < 10)
            errors.Add($"Too few active generated texture assets for meaningful sampling QA: {uses.Count} < 10.");

        foreach (TextureUse use in uses.Values.OrderBy(x => x.Path, StringComparer.Ordinal))
            ValidateImporter(use, errors);

        if (QualitySettings.anisotropicFiltering == AnisotropicFiltering.Disable)
            errors.Add("QualitySettings.anisotropicFiltering is globally disabled; per-texture anisotropy cannot protect grazing 4K detail.");

        if (errors.Count > 0)
            throw new InvalidOperationException(
                "4K texture sampling / map-registration QA FAILED:\n - " + string.Join("\n - ", errors));

        Debug.Log(
            $"4K texture sampling QA passed for {materials.Length} active generated materials and {uses.Count} texture assets: " +
            $"mipmapped trilinear sampling, anisotropy >= {MinimumAnisotropy}, semantic color-space import and Standard-map UV registration are source-valid. " +
            "Actual moire/shimmer, perceived texel density and grazing response remain native-render checks.");
    }

    [MenuItem("NewTown/QA/Validate 4K Texture Sampling Contract")]
    public static void ValidateContractConfigOnly()
    {
        if (!File.Exists(ContractPath))
            throw new InvalidOperationException($"Missing required 4K texture sampling contract: {ContractPath}");

        string json = File.ReadAllText(ContractPath);
        string[] requiredTokens =
        {
            "\"contractVersion\": \"texture-sampling-v1.0.0\"",
            "\"minimumManagedTextureDimensionPx\": 512",
            "\"minimumAnisotropy\": 8",
            "\"requiredFilterMode\": \"Trilinear\"",
            "\"requiredWrapMode\": \"Repeat\"",
            "\"mipmapsRequired\": true",
            "\"shaderProperty\": \"_MainTex\"",
            "\"shaderProperty\": \"_BumpMap\"",
            "\"shaderProperty\": \"_MetallicGlossMap\"",
            "\"visualFidelityPointsAwarded\": 0",
            "\"runtimeRenderVerification\": \"PENDING_UNITY_RUNTIME\""
        };

        foreach (string token in requiredTokens)
            if (json.IndexOf(token, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException($"4K texture sampling contract missing token: {token}");
    }

    private static bool NormalizeStandardMapRegistration(Material material)
    {
        if (material == null || material.shader == null || material.shader.name != "Standard" ||
            !material.HasProperty("_MainTex") || material.GetTexture("_MainTex") == null)
            return false;

        Vector2 mainScale = material.GetTextureScale("_MainTex");
        Vector2 mainOffset = material.GetTextureOffset("_MainTex");
        bool changed = false;
        foreach (string property in new[] { "_BumpMap", "_MetallicGlossMap" })
        {
            if (!material.HasProperty(property) || material.GetTexture(property) == null)
                continue;
            Vector2 scale = material.GetTextureScale(property);
            Vector2 offset = material.GetTextureOffset(property);
            if (!Approximately(scale, mainScale, UvTolerance))
            {
                material.SetTextureScale(property, mainScale);
                changed = true;
            }
            if (!Approximately(offset, mainOffset, UvTolerance))
            {
                material.SetTextureOffset(property, mainOffset);
                changed = true;
            }
        }

        if (changed) EditorUtility.SetDirty(material);
        return changed;
    }

    private static void ValidateMapRegistration(Material material, List<string> errors)
    {
        if (material == null || material.shader == null)
        {
            errors.Add("Null material/shader reached texture sampling QA.");
            return;
        }

        if (!material.HasProperty("_MainTex") || material.GetTexture("_MainTex") == null)
            return;

        Vector2 mainScale = material.GetTextureScale("_MainTex");
        Vector2 mainOffset = material.GetTextureOffset("_MainTex");
        if (!FinitePositive(mainScale.x) || !FinitePositive(mainScale.y) || !Finite(mainOffset.x) || !Finite(mainOffset.y))
            errors.Add($"{material.name} has invalid _MainTex scale/offset: scale={mainScale}, offset={mainOffset}.");

        // The custom foliage shader intentionally applies _MainTex_ST once and samples all maps with
        // that transformed UV. Standard materials, however, expose independent ST values and therefore
        // require explicit registration for co-authored maps.
        if (material.shader.name != "Standard")
            return;

        foreach (string property in new[] { "_BumpMap", "_MetallicGlossMap" })
        {
            if (!material.HasProperty(property) || material.GetTexture(property) == null)
                continue;
            Vector2 scale = material.GetTextureScale(property);
            Vector2 offset = material.GetTextureOffset(property);
            if (!Approximately(scale, mainScale, UvTolerance) || !Approximately(offset, mainOffset, UvTolerance))
                errors.Add(
                    $"{material.name} map registration drift: {property} scale/offset {scale}/{offset} != _MainTex {mainScale}/{mainOffset}. " +
                    "Co-authored physical features would appear at inconsistent sizes.");
        }
    }

    private static Dictionary<string, TextureUse> CollectTextureUses(Material[] materials)
    {
        var uses = new Dictionary<string, TextureUse>(StringComparer.Ordinal);
        foreach (Material material in materials)
        {
            AddTextureUse(material, "_MainTex", MapRole.Albedo, uses);
            AddTextureUse(material, "_BumpMap", MapRole.Normal, uses);
            AddTextureUse(material, "_MetallicGlossMap", MapRole.MetallicSmoothness, uses);
        }
        return uses;
    }

    private static void AddTextureUse(Material material, string property, MapRole role,
        Dictionary<string, TextureUse> uses)
    {
        if (material == null || !material.HasProperty(property)) return;
        Texture2D texture = material.GetTexture(property) as Texture2D;
        if (texture == null) return;
        string path = AssetDatabase.GetAssetPath(texture);
        if (string.IsNullOrEmpty(path) || !path.StartsWith(ManagedMaterialPrefix, StringComparison.Ordinal))
            return;

        if (!uses.TryGetValue(path, out TextureUse use))
        {
            use = new TextureUse { Texture = texture, Path = path, Roles = MapRole.None };
            uses.Add(path, use);
        }
        use.Roles |= role;
        use.Materials.Add(material.name);
    }

    private static bool NormalizeImporter(TextureUse use)
    {
        ValidateNoSemanticConflict(use);
        TextureImporter importer = AssetImporter.GetAtPath(use.Path) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException($"Texture importer unavailable for managed benchmark texture {use.Path}.");

        bool isNormal = (use.Roles & MapRole.Normal) != 0;
        bool isLinearMask = (use.Roles & MapRole.MetallicSmoothness) != 0;
        bool isAlbedo = (use.Roles & MapRole.Albedo) != 0;
        bool desiredSrgb = isAlbedo && !isNormal && !isLinearMask;
        TextureImporterType desiredType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;

        bool changed = false;
        if (importer.textureType != desiredType) { importer.textureType = desiredType; changed = true; }
        if (importer.sRGBTexture != desiredSrgb) { importer.sRGBTexture = desiredSrgb; changed = true; }
        if (!importer.mipmapEnabled) { importer.mipmapEnabled = true; changed = true; }
        if (importer.filterMode != FilterMode.Trilinear) { importer.filterMode = FilterMode.Trilinear; changed = true; }
        if (importer.anisoLevel < MinimumAnisotropy) { importer.anisoLevel = MinimumAnisotropy; changed = true; }
        if (importer.wrapMode != TextureWrapMode.Repeat) { importer.wrapMode = TextureWrapMode.Repeat; changed = true; }
        if (importer.maxTextureSize < MinimumTextureDimension) { importer.maxTextureSize = MinimumTextureDimension; changed = true; }

        if (changed) importer.SaveAndReimport();
        return changed;
    }

    private static void ValidateImporter(TextureUse use, List<string> errors)
    {
        try
        {
            ValidateNoSemanticConflict(use);
        }
        catch (Exception ex)
        {
            errors.Add(ex.Message);
            return;
        }

        TextureImporter importer = AssetImporter.GetAtPath(use.Path) as TextureImporter;
        if (importer == null)
        {
            errors.Add($"Texture importer unavailable: {use.Path}.");
            return;
        }

        bool isNormal = (use.Roles & MapRole.Normal) != 0;
        bool isLinearMask = (use.Roles & MapRole.MetallicSmoothness) != 0;
        bool isAlbedo = (use.Roles & MapRole.Albedo) != 0;
        bool expectedSrgb = isAlbedo && !isNormal && !isLinearMask;
        TextureImporterType expectedType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;

        if (use.Texture.width < MinimumTextureDimension || use.Texture.height < MinimumTextureDimension)
            errors.Add($"Managed 4K texture below {MinimumTextureDimension}px: {use.Path} = {use.Texture.width}x{use.Texture.height}.");
        if (importer.textureType != expectedType)
            errors.Add($"Semantic texture type mismatch on {use.Path}: roles={use.Roles}, importer={importer.textureType}, expected={expectedType}.");
        if (importer.sRGBTexture != expectedSrgb)
            errors.Add($"Color-space mismatch on {use.Path}: roles={use.Roles}, sRGB={importer.sRGBTexture}, expected={expectedSrgb}.");
        if (!importer.mipmapEnabled)
            errors.Add($"Mipmaps disabled on managed 4K texture: {use.Path}.");
        if (importer.filterMode != FilterMode.Trilinear)
            errors.Add($"Managed 4K texture is not trilinear: {use.Path} = {importer.filterMode}.");
        if (importer.anisoLevel < MinimumAnisotropy)
            errors.Add($"Managed 4K texture anisotropy too low: {use.Path} = {importer.anisoLevel} < {MinimumAnisotropy}.");
        if (importer.wrapMode != TextureWrapMode.Repeat)
            errors.Add($"Managed generated repeat texture does not use Repeat wrap: {use.Path} = {importer.wrapMode}.");
    }

    private static void ValidateNoSemanticConflict(TextureUse use)
    {
        bool albedo = (use.Roles & MapRole.Albedo) != 0;
        bool linear = (use.Roles & (MapRole.Normal | MapRole.MetallicSmoothness)) != 0;
        bool normalAndMask = (use.Roles & MapRole.Normal) != 0 && (use.Roles & MapRole.MetallicSmoothness) != 0;
        if ((albedo && linear) || normalAndMask)
            throw new InvalidOperationException(
                $"Managed texture {use.Path} is reused across incompatible semantic roles ({use.Roles}) by {string.Join(", ", use.Materials)}; " +
                "one asset cannot simultaneously have correct color-space/import semantics for those roles.");
    }

    private static Material[] CollectManagedMaterials()
    {
        return ActiveRenderers()
            .SelectMany(r => r.sharedMaterials ?? Array.Empty<Material>())
            .Where(m => m != null)
            .Where(IsManagedMaterial)
            .Distinct()
            .OrderBy(m => m.name, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<Renderer> ActiveRenderers()
    {
        GameObject root = GameObject.Find("QualityBlock1990s");
        if (root == null)
            throw new InvalidOperationException("QualityBlock1990s root is missing.");
        return root.GetComponentsInChildren<Renderer>(true)
            .Where(r => r != null && r.enabled && r.gameObject.activeInHierarchy);
    }

    private static bool IsManagedMaterial(Material material)
    {
        string path = AssetDatabase.GetAssetPath(material);
        return !string.IsNullOrEmpty(path) && path.StartsWith(ManagedMaterialPrefix, StringComparison.Ordinal);
    }

    private static void RequireQualityScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            throw new InvalidOperationException(
                $"4K texture sampling QA requires the persisted benchmark scene to be open: {ScenePath}");
    }

    private static bool Approximately(Vector2 a, Vector2 b, float tolerance) =>
        Mathf.Abs(a.x - b.x) <= tolerance && Mathf.Abs(a.y - b.y) <= tolerance;

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool FinitePositive(float value) => Finite(value) && value > 0f;
}
