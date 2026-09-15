using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Replaces broad primitive-ground fallback materials with physically scaled, two-frequency PBR.
/// The original fallback used one 0..1 primitive UV domain regardless of whether a surface was
/// 2 m or 54 m wide, making texture scale and tiling visibly synthetic at 4K. This pass keeps the
/// gameplay ground geometry/colliders unchanged, but assigns serialized per-surface materials whose
/// texture transforms are derived from real renderer bounds. A long-period macro layer carries only
/// material heterogeneity; a short-period Standard-shader detail layer restores sub-centimetre
/// breakup. Paving contains no painted joint grid because those joints are real geometry in the
/// high-detail ground assembly.
/// </summary>
public static class QualityBlockGroundBaseSurfaceUpgrade
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string Root = "Assets/Art/GeneratedGroundBase";
    private const string TextureRoot = Root + "/Textures";
    private const string ContractPath = "Assets/QA/ground_base_surface_contract.json";
    private const string GeneratorVersion = "ground-base-metric-v1.0.0";
    private const int MacroSize = 2048;
    private const int DetailSize = 1024;

    private enum SurfaceKind
    {
        Grass,
        Paving,
        Soil,
    }

    private readonly struct Profile
    {
        public readonly string Id;
        public readonly string Stem;
        public readonly SurfaceKind Kind;
        public readonly Color BaseColor;
        public readonly float MacroTileMeters;
        public readonly float DetailTileMeters;
        public readonly float RoughnessMin;
        public readonly float RoughnessMax;
        public readonly float MacroColorVariation;
        public readonly float DetailColorVariation;
        public readonly float MacroNormalMm;
        public readonly float DetailNormalMm;
        public readonly int Seed;

        public Profile(string id, string stem, SurfaceKind kind, Color baseColor,
            float macroTileMeters, float detailTileMeters, float roughnessMin, float roughnessMax,
            float macroColorVariation, float detailColorVariation,
            float macroNormalMm, float detailNormalMm, int seed)
        {
            Id = id;
            Stem = stem;
            Kind = kind;
            BaseColor = baseColor;
            MacroTileMeters = macroTileMeters;
            DetailTileMeters = detailTileMeters;
            RoughnessMin = roughnessMin;
            RoughnessMax = roughnessMax;
            MacroColorVariation = macroColorVariation;
            DetailColorVariation = detailColorVariation;
            MacroNormalMm = macroNormalMm;
            DetailNormalMm = detailNormalMm;
            Seed = seed;
        }
    }

    private readonly struct SurfaceBinding
    {
        public readonly string ObjectName;
        public readonly string ProfileId;

        public SurfaceBinding(string objectName, string profileId)
        {
            ObjectName = objectName;
            ProfileId = profileId;
        }
    }

    private static readonly Profile[] Profiles =
    {
        new Profile("grass_verge_metric", "GrassVerge", SurfaceKind.Grass,
            new Color(0.235f, 0.34f, 0.12f), 8.0f, 0.42f,
            0.86f, 0.96f, 0.10f, 0.08f, 3.0f, 8.0f, 1831),
        new Profile("warm_paving_metric", "WarmPaving", SurfaceKind.Paving,
            new Color(0.52f, 0.51f, 0.47f), 7.2f, 0.36f,
            0.80f, 0.93f, 0.055f, 0.045f, 0.8f, 1.6f, 4271),
        new Profile("compacted_soil_metric", "CompactedSoil", SurfaceKind.Soil,
            new Color(0.38f, 0.28f, 0.17f), 6.4f, 0.30f,
            0.90f, 0.98f, 0.09f, 0.07f, 4.0f, 5.5f, 7103),
    };

    private static readonly SurfaceBinding[] Bindings =
    {
        new SurfaceBinding("GrassField", "grass_verge_metric"),
        new SurfaceBinding("DanchiPlaza", "warm_paving_metric"),
        new SurfaceBinding("ParkPath", "warm_paving_metric"),
        new SurfaceBinding("WornPathA", "compacted_soil_metric"),
        new SurfaceBinding("WornPathB", "compacted_soil_metric"),
    };

    [MenuItem("NewTown/Materials/Build Metric Anti-Repeat Base Ground")]
    public static void BuildAndApply()
    {
        if (!File.Exists(ContractPath))
            throw new InvalidOperationException($"Base-ground contract missing: {ContractPath}");

        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(TextureRoot);

        foreach (Profile profile in Profiles)
            EnsureTextureSet(profile);

        foreach (SurfaceBinding binding in Bindings)
        {
            Profile profile = FindProfile(binding.ProfileId);
            Renderer renderer = FindSceneRenderer(binding.ObjectName);
            Material material = EnsureSurfaceMaterial(binding.ObjectName, profile, renderer.bounds);
            renderer.sharedMaterial = material;
            EditorUtility.SetDirty(renderer);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "Metric anti-repeat base-ground PBR applied to grass, paving and compacted soil. " +
            "Visual Fidelity remains unscored until native Unity 4K evidence is inspected.");
    }

    [MenuItem("NewTown/QA/Validate Metric Anti-Repeat Base Ground")]
    public static void Validate()
    {
        ValidateContract();

        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        foreach (Profile profile in Profiles)
        {
            ValidateTexture(TexturePath(profile, "MacroAlbedo"), MacroSize, true, false);
            ValidateTexture(TexturePath(profile, "MacroNormal"), MacroSize, false, true);
            ValidateTexture(TexturePath(profile, "MacroMask"), MacroSize, false, false);
            ValidateTexture(TexturePath(profile, "DetailAlbedo"), DetailSize, true, false);
            ValidateTexture(TexturePath(profile, "DetailNormal"), DetailSize, false, true);
        }

        foreach (SurfaceBinding binding in Bindings)
        {
            Profile profile = FindProfile(binding.ProfileId);
            Renderer renderer = FindSceneRenderer(binding.ObjectName);
            Material material = renderer.sharedMaterial;
            if (material == null || material.name != MaterialName(binding.ObjectName))
                throw new InvalidOperationException(
                    $"{binding.ObjectName} is not bound to its serialized metric material. Found {material?.name ?? "<null>"}.");
            if (material.shader == null || material.shader.name != "Standard")
                throw new InvalidOperationException($"{binding.ObjectName} metric ground material must use Standard PBR.");
            if (!material.IsKeywordEnabled("_NORMALMAP") ||
                !material.IsKeywordEnabled("_METALLICGLOSSMAP") ||
                !material.IsKeywordEnabled("_DETAIL_MULX2"))
                throw new InvalidOperationException($"{binding.ObjectName} is missing required base/detail PBR keywords.");

            if (material.GetTexture("_MainTex") == null || material.GetTexture("_BumpMap") == null ||
                material.GetTexture("_MetallicGlossMap") == null ||
                material.GetTexture("_DetailAlbedoMap") == null || material.GetTexture("_DetailNormalMap") == null)
                throw new InvalidOperationException($"{binding.ObjectName} metric material texture set is incomplete.");

            Bounds bounds = renderer.bounds;
            Vector2 expectedMacroScale = new Vector2(
                Mathf.Max(0.001f, bounds.size.x) / profile.MacroTileMeters,
                Mathf.Max(0.001f, bounds.size.z) / profile.MacroTileMeters);
            Vector2 expectedDetailScale = new Vector2(
                Mathf.Max(0.001f, bounds.size.x) / profile.DetailTileMeters,
                Mathf.Max(0.001f, bounds.size.z) / profile.DetailTileMeters);
            AssertNear(material.GetTextureScale("_MainTex"), expectedMacroScale, 0.015f,
                $"macro scale on {binding.ObjectName}");
            AssertNear(material.GetTextureScale("_DetailAlbedoMap"), expectedDetailScale, 0.015f,
                $"detail scale on {binding.ObjectName}");

            Vector2 expectedMacroOffset = WorldPhase(bounds.min, profile.MacroTileMeters);
            Vector2 expectedDetailOffset = WorldPhase(bounds.min, profile.DetailTileMeters);
            AssertWrappedNear(material.GetTextureOffset("_MainTex"), expectedMacroOffset, 0.015f,
                $"macro world phase on {binding.ObjectName}");
            AssertWrappedNear(material.GetTextureOffset("_DetailAlbedoMap"), expectedDetailOffset, 0.015f,
                $"detail world phase on {binding.ObjectName}");

            if (material.GetFloat("_Metallic") > 0.01f)
                throw new InvalidOperationException($"Impossible metallic base ground on {binding.ObjectName}.");
        }

        Debug.Log(
            "Metric anti-repeat base-ground validation passed structurally: 5 surfaces, 3 material families, " +
            "serialized metre-derived macro/detail scales, global phase offsets, no metallic ground. " +
            "Actual repetition, contact realism and 4K material response remain render-unverified.");
    }

    private static void ValidateContract()
    {
        if (!File.Exists(ContractPath))
            throw new InvalidOperationException($"Base-ground contract missing: {ContractPath}");

        GroundBaseContract contract = JsonUtility.FromJson<GroundBaseContract>(File.ReadAllText(ContractPath));
        if (contract == null || contract.profiles == null || contract.surfaces == null)
            throw new InvalidOperationException("Base-ground contract could not be parsed.");
        if (contract.runtimeRenderVerified)
            throw new InvalidOperationException(
                "Base-ground contract may not claim runtime render verification before real Unity evidence is recorded.");
        if (contract.profiles.Length != Profiles.Length || contract.surfaces.Length != Bindings.Length)
            throw new InvalidOperationException(
                $"Base-ground contract shape drift: profiles={contract.profiles.Length}, surfaces={contract.surfaces.Length}.");

        foreach (Profile profile in Profiles)
        {
            GroundBaseProfileSpec spec = contract.profiles.FirstOrDefault(x => x.id == profile.Id);
            if (spec == null)
                throw new InvalidOperationException($"Base-ground contract missing profile {profile.Id}.");
            if (Mathf.Abs(spec.macroTileMeters - profile.MacroTileMeters) > 0.001f ||
                Mathf.Abs(spec.detailTileMeters - profile.DetailTileMeters) > 0.001f)
                throw new InvalidOperationException($"Physical texture scale contract drift for {profile.Id}.");
            if (spec.metallic > 0.01f)
                throw new InvalidOperationException($"Impossible metallic ground contract for {profile.Id}.");
            if (string.IsNullOrWhiteSpace(spec.physicalReasoning) ||
                string.IsNullOrWhiteSpace(spec.frontLightResponse) ||
                string.IsNullOrWhiteSpace(spec.grazingLightResponse) ||
                string.IsNullOrWhiteSpace(spec.shadeResponse))
                throw new InvalidOperationException($"Incomplete material/light reasoning for {profile.Id}.");
        }

        foreach (SurfaceBinding binding in Bindings)
        {
            GroundBaseSurfaceSpec spec = contract.surfaces.FirstOrDefault(x => x.objectName == binding.ObjectName);
            if (spec == null || spec.profileId != binding.ProfileId)
                throw new InvalidOperationException($"Base-ground surface binding contract drift for {binding.ObjectName}.");
            if (string.IsNullOrWhiteSpace(spec.interfaceLogic) || string.IsNullOrWhiteSpace(spec.aging))
                throw new InvalidOperationException($"Missing interface/aging reasoning for {binding.ObjectName}.");
        }
    }

    private static Material EnsureSurfaceMaterial(string objectName, Profile profile, Bounds bounds)
    {
        Shader shader = Shader.Find("Standard");
        if (shader == null)
            throw new InvalidOperationException("Standard shader not found for metric base-ground material.");

        string path = $"{Root}/{MaterialName(objectName)}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = MaterialName(objectName) };
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.shader = shader;
            material.name = MaterialName(objectName);
        }

        Texture2D macroAlbedo = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(profile, "MacroAlbedo"));
        Texture2D macroNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(profile, "MacroNormal"));
        Texture2D macroMask = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(profile, "MacroMask"));
        Texture2D detailAlbedo = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(profile, "DetailAlbedo"));
        Texture2D detailNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(profile, "DetailNormal"));
        if (macroAlbedo == null || macroNormal == null || macroMask == null || detailAlbedo == null || detailNormal == null)
            throw new InvalidOperationException($"Generated texture set incomplete for {profile.Id}.");

        material.color = Color.white;
        material.SetTexture("_MainTex", macroAlbedo);
        material.SetTexture("_BumpMap", macroNormal);
        material.SetTexture("_MetallicGlossMap", macroMask);
        material.SetTexture("_DetailAlbedoMap", detailAlbedo);
        material.SetTexture("_DetailNormalMap", detailNormal);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_GlossMapScale", 1f);
        material.SetFloat("_BumpScale", 1f);
        material.SetFloat("_DetailNormalMapScale", 1f);
        material.SetFloat("_UVSec", 0f);
        material.EnableKeyword("_NORMALMAP");
        material.EnableKeyword("_METALLICGLOSSMAP");
        material.EnableKeyword("_DETAIL_MULX2");

        SetPhysicalTransform(material, "_MainTex", bounds, profile.MacroTileMeters);
        SetPhysicalTransform(material, "_BumpMap", bounds, profile.MacroTileMeters);
        SetPhysicalTransform(material, "_MetallicGlossMap", bounds, profile.MacroTileMeters);
        SetPhysicalTransform(material, "_DetailAlbedoMap", bounds, profile.DetailTileMeters);
        SetPhysicalTransform(material, "_DetailNormalMap", bounds, profile.DetailTileMeters);

        EditorUtility.SetDirty(material);
        return material;
    }

    private static void SetPhysicalTransform(Material material, string textureProperty, Bounds bounds, float tileMeters)
    {
        Vector2 scale = new Vector2(
            Mathf.Max(0.001f, bounds.size.x) / tileMeters,
            Mathf.Max(0.001f, bounds.size.z) / tileMeters);
        material.SetTextureScale(textureProperty, scale);
        material.SetTextureOffset(textureProperty, WorldPhase(bounds.min, tileMeters));
    }

    private static Vector2 WorldPhase(Vector3 boundsMin, float tileMeters)
    {
        return new Vector2(PositiveFraction(boundsMin.x / tileMeters), PositiveFraction(boundsMin.z / tileMeters));
    }

    private static float PositiveFraction(float value)
    {
        float floor = Mathf.Floor(value);
        return value - floor;
    }

    private static void EnsureTextureSet(Profile profile)
    {
        string[] required =
        {
            TexturePath(profile, "MacroAlbedo"),
            TexturePath(profile, "MacroNormal"),
            TexturePath(profile, "MacroMask"),
            TexturePath(profile, "DetailAlbedo"),
            TexturePath(profile, "DetailNormal"),
        };
        if (required.All(IsCurrentTexture))
            return;

        BuildMacroTextures(profile);
        BuildDetailTextures(profile);
    }

    private static void BuildMacroTextures(Profile profile)
    {
        int n = MacroSize * MacroSize;
        var heights = new float[n];
        var albedo = new Color32[n];
        var mask = new Color32[n];

        for (int y = 0; y < MacroSize; y++)
        {
            float v = y / (float)MacroSize;
            for (int x = 0; x < MacroSize; x++)
            {
                float u = x / (float)MacroSize;
                int i = y * MacroSize + x;
                float broad = PeriodicFbm(u, v, profile.Seed, 7, 2);
                float materialSignal = MacroMaterialSignal(profile.Kind, u, v, profile.Seed, broad);
                heights[i] = materialSignal;

                float centered = (materialSignal - 0.5f) * 2f;
                Color c = profile.BaseColor * (1f + centered * profile.MacroColorVariation);
                if (profile.Kind == SurfaceKind.Grass)
                {
                    float thinning = Mathf.SmoothStep(0.72f, 0.94f,
                        PeriodicFbm(u, v, profile.Seed + 601, 5, 3));
                    c = Color.Lerp(c, new Color(0.30f, 0.27f, 0.14f), thinning * 0.22f);
                }
                else if (profile.Kind == SurfaceKind.Soil)
                {
                    float fines = PeriodicFbm(u, v, profile.Seed + 811, 5, 3);
                    c *= Mathf.Lerp(0.94f, 1.055f, fines);
                }
                c.a = 1f;
                albedo[i] = ClampColor(c);

                float roughSignal = PeriodicFbm(u, v, profile.Seed + 1291, 5, 4);
                float roughness = Mathf.Lerp(profile.RoughnessMin, profile.RoughnessMax, roughSignal);
                mask[i] = new Color32(0, 0, 0,
                    (byte)Mathf.RoundToInt((1f - Mathf.Clamp01(roughness)) * 255f));
            }
        }

        Color32[] normals = BuildNormals(heights, MacroSize, profile.MacroTileMeters, profile.MacroNormalMm);
        WriteTexture(TexturePath(profile, "MacroAlbedo"), MacroSize, albedo, true, false);
        WriteTexture(TexturePath(profile, "MacroNormal"), MacroSize, normals, false, true);
        WriteTexture(TexturePath(profile, "MacroMask"), MacroSize, mask, false, false);
    }

    private static void BuildDetailTextures(Profile profile)
    {
        int n = DetailSize * DetailSize;
        var heights = new float[n];
        var albedo = new Color32[n];
        for (int y = 0; y < DetailSize; y++)
        {
            float v = y / (float)DetailSize;
            for (int x = 0; x < DetailSize; x++)
            {
                float u = x / (float)DetailSize;
                int i = y * DetailSize + x;
                float h = DetailMaterialSignal(profile.Kind, u, v, profile.Seed + 4001);
                heights[i] = h;

                // Standard detail albedo uses multiply x2, therefore neutral is 0.5.
                float multiplierSignal = (h - 0.5f) * 2f * profile.DetailColorVariation;
                float neutral = Mathf.Clamp01(0.5f * (1f + multiplierSignal));
                byte b = (byte)Mathf.RoundToInt(neutral * 255f);
                albedo[i] = new Color32(b, b, b, 255);
            }
        }

        Color32[] normals = BuildNormals(heights, DetailSize, profile.DetailTileMeters, profile.DetailNormalMm);
        WriteTexture(TexturePath(profile, "DetailAlbedo"), DetailSize, albedo, true, false);
        WriteTexture(TexturePath(profile, "DetailNormal"), DetailSize, normals, false, true);
    }

    private static float MacroMaterialSignal(SurfaceKind kind, float u, float v, int seed, float broad)
    {
        switch (kind)
        {
            case SurfaceKind.Grass:
                return Mathf.Clamp01(broad * 0.72f + PeriodicFbm(u, v, seed + 101, 4, 8) * 0.28f);
            case SurfaceKind.Paving:
                // Aggregate/batch variation only. No JointNoise or directional stain is permitted here.
                return Mathf.Clamp01(broad * 0.84f + PeriodicValueNoise(u, v, 28, seed + 211) * 0.16f);
            case SurfaceKind.Soil:
                return Mathf.Clamp01(broad * 0.66f + PeriodicValueNoise(u, v, 20, seed + 307) * 0.34f);
            default:
                return broad;
        }
    }

    private static float DetailMaterialSignal(SurfaceKind kind, float u, float v, int seed)
    {
        float fine = PeriodicFbm(u, v, seed, 6, 8);
        switch (kind)
        {
            case SurfaceKind.Grass:
            {
                float blades = Mathf.Abs(Mathf.Sin(Mathf.PI * 2f * (u * 89f + v * 23f + seed * 0.001f)));
                return Mathf.Clamp01(fine * 0.58f + blades * 0.42f);
            }
            case SurfaceKind.Paving:
            {
                float aggregate = PeriodicValueNoise(u, v, 72, seed + 521);
                return Mathf.Clamp01(fine * 0.68f + aggregate * 0.32f);
            }
            case SurfaceKind.Soil:
            {
                float grains = PeriodicValueNoise(u, v, 64, seed + 733);
                return Mathf.Clamp01(fine * 0.62f + grains * 0.38f);
            }
            default:
                return fine;
        }
    }

    private static Color32[] BuildNormals(float[] heights, int size, float physicalTileMeters, float amplitudeMm)
    {
        var normals = new Color32[heights.Length];
        float pixelMeters = physicalTileMeters / size;
        float amplitudeMeters = amplitudeMm * 0.001f;
        float slopeScale = amplitudeMeters / Mathf.Max(0.0000001f, 2f * pixelMeters);
        for (int y = 0; y < size; y++)
        {
            int ym = (y - 1 + size) % size;
            int yp = (y + 1) % size;
            for (int x = 0; x < size; x++)
            {
                int xm = (x - 1 + size) % size;
                int xp = (x + 1) % size;
                float dx = heights[y * size + xp] - heights[y * size + xm];
                float dy = heights[yp * size + x] - heights[ym * size + x];
                Vector3 normal = new Vector3(-dx * slopeScale, -dy * slopeScale, 1f).normalized;
                normals[y * size + x] = new Color32(
                    (byte)Mathf.RoundToInt((normal.x * 0.5f + 0.5f) * 255f),
                    (byte)Mathf.RoundToInt((normal.y * 0.5f + 0.5f) * 255f),
                    (byte)Mathf.RoundToInt((normal.z * 0.5f + 0.5f) * 255f), 255);
            }
        }
        return normals;
    }

    private static void WriteTexture(string path, int size, Color32[] pixels, bool sRgb, bool normalMap)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, !sRgb);
        try
        {
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException($"Texture importer unavailable for {path}.");
        importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = sRgb && !normalMap;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Trilinear;
        importer.mipmapEnabled = true;
        importer.anisoLevel = 12;
        importer.maxTextureSize = size;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.userData = GeneratorVersion;
        importer.SaveAndReimport();
    }

    private static void ValidateTexture(string path, int minSize, bool expectSrgb, bool expectNormal)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (texture == null || importer == null)
            throw new InvalidOperationException($"Generated base-ground texture/importer missing: {path}.");
        if (texture.width < minSize || texture.height < minSize)
            throw new InvalidOperationException($"Base-ground texture below {minSize}px: {path}.");
        if (importer.userData != GeneratorVersion)
            throw new InvalidOperationException($"Base-ground texture generator version drift: {path}.");
        if (expectNormal && importer.textureType != TextureImporterType.NormalMap)
            throw new InvalidOperationException($"Base-ground normal importer invalid: {path}.");
        if (!expectNormal && importer.textureType == TextureImporterType.NormalMap)
            throw new InvalidOperationException($"Non-normal base-ground texture imported as normal: {path}.");
        if (importer.sRGBTexture != expectSrgb)
            throw new InvalidOperationException($"Base-ground sRGB policy mismatch: {path}.");
        if (!importer.mipmapEnabled || importer.filterMode != FilterMode.Trilinear || importer.anisoLevel < 8)
            throw new InvalidOperationException($"Base-ground anti-shimmer import settings incomplete: {path}.");
    }

    private static bool IsCurrentTexture(string path)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        return texture != null && importer != null && importer.userData == GeneratorVersion;
    }

    private static Profile FindProfile(string id)
    {
        foreach (Profile profile in Profiles)
            if (profile.Id == id) return profile;
        throw new InvalidOperationException($"Unknown base-ground profile: {id}.");
    }

    private static Renderer FindSceneRenderer(string objectName)
    {
        GameObject go = Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x.scene.IsValid() && x.name == objectName);
        Renderer renderer = go != null ? go.GetComponent<Renderer>() : null;
        if (renderer == null)
            throw new InvalidOperationException($"Base-ground renderer missing: {objectName}.");
        return renderer;
    }

    private static string MaterialName(string objectName) => $"MAT_MetricGround_{objectName}";
    private static string TexturePath(Profile profile, string suffix) => $"{TextureRoot}/{profile.Stem}_{suffix}.png";

    private static Color32 ClampColor(Color color)
    {
        return new Color(
            Mathf.Clamp01(color.r), Mathf.Clamp01(color.g), Mathf.Clamp01(color.b), 1f);
    }

    private static void AssertNear(Vector2 actual, Vector2 expected, float tolerance, string label)
    {
        if (Mathf.Abs(actual.x - expected.x) > tolerance || Mathf.Abs(actual.y - expected.y) > tolerance)
            throw new InvalidOperationException($"Base-ground {label} drift: got {actual}, expected {expected}.");
    }

    private static void AssertWrappedNear(Vector2 actual, Vector2 expected, float tolerance, string label)
    {
        float dx = Mathf.Abs(actual.x - expected.x);
        float dy = Mathf.Abs(actual.y - expected.y);
        dx = Mathf.Min(dx, 1f - Mathf.Min(dx, 1f));
        dy = Mathf.Min(dy, 1f - Mathf.Min(dy, 1f));
        if (dx > tolerance || dy > tolerance)
            throw new InvalidOperationException($"Base-ground {label} drift: got {actual}, expected {expected}.");
    }

    private static float PeriodicFbm(float u, float v, int seed, int octaves, int startCells)
    {
        float sum = 0f;
        float total = 0f;
        float weight = 0.5f;
        int cells = Mathf.Max(1, startCells);
        for (int octave = 0; octave < octaves; octave++)
        {
            sum += PeriodicValueNoise(u, v, cells, seed + octave * 977) * weight;
            total += weight;
            weight *= 0.52f;
            cells *= 2;
        }
        return total > 0f ? sum / total : 0.5f;
    }

    private static float PeriodicValueNoise(float u, float v, int cells, int seed)
    {
        float x = u * cells;
        float y = v * cells;
        int x0 = Mathf.FloorToInt(x);
        int y0 = Mathf.FloorToInt(y);
        float tx = x - x0;
        float ty = y - y0;
        int ix0 = Mod(x0, cells);
        int iy0 = Mod(y0, cells);
        int ix1 = (ix0 + 1) % cells;
        int iy1 = (iy0 + 1) % cells;
        float sx = tx * tx * (3f - 2f * tx);
        float sy = ty * ty * (3f - 2f * ty);
        float a = Hash01(ix0, iy0, seed);
        float b = Hash01(ix1, iy0, seed);
        float c = Hash01(ix0, iy1, seed);
        float d = Hash01(ix1, iy1, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, sx), Mathf.Lerp(c, d, sx), sy);
    }

    private static int Mod(int value, int modulus)
    {
        int result = value % modulus;
        return result < 0 ? result + modulus : result;
    }

    private static float Hash01(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 69069);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return h / (float)uint.MaxValue;
        }
    }

    [Serializable]
    private sealed class GroundBaseContract
    {
        public bool runtimeRenderVerified;
        public GroundBaseProfileSpec[] profiles;
        public GroundBaseSurfaceSpec[] surfaces;
    }

    [Serializable]
    private sealed class GroundBaseProfileSpec
    {
        public string id;
        public float macroTileMeters;
        public float detailTileMeters;
        public float metallic;
        public string physicalReasoning;
        public string frontLightResponse;
        public string grazingLightResponse;
        public string shadeResponse;
    }

    [Serializable]
    private sealed class GroundBaseSurfaceSpec
    {
        public string objectName;
        public string profileId;
        public string interfaceLogic;
        public string aging;
    }
}
