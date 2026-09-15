using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 4K-oriented micro-surface pass for the generated ground infrastructure.
///
/// The structural ground pass intentionally starts from simple scalar materials so geometry and
/// construction can be validated independently. This pass removes two remaining close-up tells:
/// 1) generated detail meshes used 0..1 UVs per face regardless of real dimensions, so a 24 mm lip
///    and a 588 mm grate bar could show the same texture scale;
/// 2) galvanized steel, cast iron and damp mineral recesses had scalar smoothness only.
///
/// This upgrade rewrites generated-detail UV0 into metre-scaled planar coordinates and creates
/// deterministic tileable micro-normal / albedo / metallic-smoothness maps. Macro stains remain in
/// the cause-based weathering system; these textures represent manufacturing/oxidation microstructure
/// only and contain no painted highlights, sun direction or arbitrary dirt.
/// </summary>
public static class QualityBlockGroundMicrodetailUpgrade
{
    private const string MeshRoot = "Assets/Art/GeneratedDetailMeshes";
    private const string MaterialRoot = "Assets/Art/GeneratedGroundMaterials";
    private const string TextureRoot = MaterialRoot + "/Microdetail";
    private const string ContractPath = "Assets/QA/ground_microdetail_contract.json";
    private const int TextureSize = 1024;
    private const string GeneratorVersion = "ground-microdetail-v1.0.0";

    private enum MicroSurfaceKind
    {
        GalvanizedSteel,
        CastIron,
        MineralJoint,
        DampConcrete,
    }

    private readonly struct Profile
    {
        public readonly string MaterialName;
        public readonly string TextureStem;
        public readonly MicroSurfaceKind Kind;
        public readonly Color BaseColor;
        public readonly float ColorVariation;
        public readonly float RoughnessMin;
        public readonly float RoughnessMax;
        public readonly float MetallicMin;
        public readonly float MetallicMax;
        public readonly float NormalAmplitudeMm;
        public readonly float PhysicalTileMeters;
        public readonly int Seed;

        public Profile(string materialName, string textureStem, MicroSurfaceKind kind, Color baseColor,
            float colorVariation, float roughnessMin, float roughnessMax,
            float metallicMin, float metallicMax, float normalAmplitudeMm,
            float physicalTileMeters, int seed)
        {
            MaterialName = materialName;
            TextureStem = textureStem;
            Kind = kind;
            BaseColor = baseColor;
            ColorVariation = colorVariation;
            RoughnessMin = roughnessMin;
            RoughnessMax = roughnessMax;
            MetallicMin = metallicMin;
            MetallicMax = metallicMax;
            NormalAmplitudeMm = normalAmplitudeMm;
            PhysicalTileMeters = physicalTileMeters;
            Seed = seed;
        }
    }

    private static readonly Profile[] Profiles =
    {
        new Profile(
            "MAT_GalvanizedDrainGrate", "Ground_GalvanizedSteel", MicroSurfaceKind.GalvanizedSteel,
            new Color(0.30f, 0.32f, 0.32f), 0.035f,
            0.48f, 0.70f, 0.75f, 1.00f, 0.12f, 0.064f, 9137),
        new Profile(
            "MAT_CastIronUtilityCover", "Ground_CastIron", MicroSurfaceKind.CastIron,
            new Color(0.15f, 0.16f, 0.15f), 0.075f,
            0.55f, 0.78f, 0.65f, 0.95f, 0.35f, 0.120f, 5081),
        new Profile(
            "MAT_PavingJointRecess", "Ground_MineralJoint", MicroSurfaceKind.MineralJoint,
            new Color(0.11f, 0.105f, 0.095f), 0.040f,
            0.88f, 0.98f, 0.00f, 0.00f, 0.30f, 0.080f, 2219),
        new Profile(
            "MAT_DampDrainChannel", "Ground_DampConcrete", MicroSurfaceKind.DampConcrete,
            new Color(0.24f, 0.25f, 0.22f), 0.065f,
            0.58f, 0.82f, 0.00f, 0.00f, 0.90f, 0.160f, 7451),
    };

    [MenuItem("NewTown/Materials/Build Metric-Scale Ground Microdetail")]
    public static void BuildAndApply()
    {
        if (!File.Exists(ContractPath))
            throw new InvalidOperationException($"Ground microdetail contract missing: {ContractPath}");

        Directory.CreateDirectory(TextureRoot);
        int uvMeshCount = RewriteGeneratedDetailMeshesToMetricUv();
        foreach (Profile profile in Profiles)
        {
            EnsureProfileTextures(profile);
            ApplyProfileToMaterial(profile);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            $"Ground 4K microdetail applied: metric-UV meshes={uvMeshCount}, materials={Profiles.Length}. " +
            "This improves implementation readiness only; Visual Fidelity remains unscored until native Unity 4K renders are inspected.");
    }

    [MenuItem("NewTown/QA/Validate Ground Metric UV + Microdetail")]
    public static void Validate()
    {
        if (!File.Exists(ContractPath))
            throw new InvalidOperationException($"Ground microdetail contract missing: {ContractPath}");

        string[] meshGuids = AssetDatabase.FindAssets("t:Mesh", new[] { MeshRoot });
        Mesh[] meshes = meshGuids
            .Select(guid => AssetDatabase.LoadAssetAtPath<Mesh>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(mesh => mesh != null && mesh.name.StartsWith("GM_HD_", StringComparison.Ordinal))
            .ToArray();
        if (meshes.Length < 20)
            throw new InvalidOperationException($"Too few generated detail meshes for metric-UV validation: {meshes.Length}.");

        int metricEvidenceCount = 0;
        foreach (Mesh mesh in meshes)
        {
            Vector2[] uv = mesh.uv;
            Vector3[] vertices = mesh.vertices;
            if (uv == null || uv.Length != vertices.Length)
                throw new InvalidOperationException($"Metric UV missing/mismatched on generated mesh {mesh.name}.");

            Vector2 minUv = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maxUv = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < uv.Length; i++)
            {
                minUv = Vector2.Min(minUv, uv[i]);
                maxUv = Vector2.Max(maxUv, uv[i]);
            }

            float uvSpan = Mathf.Max(maxUv.x - minUv.x, maxUv.y - minUv.y);
            float worldSpan = Mathf.Max(mesh.bounds.size.x, Mathf.Max(mesh.bounds.size.y, mesh.bounds.size.z));
            if (worldSpan > 0.02f && uvSpan > 0.001f)
            {
                float ratio = uvSpan / worldSpan;
                // Dominant-axis planar metric UVs should stay in the same order of magnitude as
                // physical mesh dimensions. Old per-face 0..1 UVs fail this on most small hardware.
                if (ratio >= 0.45f && ratio <= 2.20f)
                    metricEvidenceCount++;
            }
        }
        if (metricEvidenceCount < Mathf.Min(20, meshes.Length / 3))
            throw new InvalidOperationException(
                $"Generated mesh UVs do not show enough real-metre scale evidence: {metricEvidenceCount}/{meshes.Length}.");

        foreach (Profile profile in Profiles)
        {
            string materialPath = $"{MaterialRoot}/{profile.MaterialName}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
                throw new InvalidOperationException($"Ground microdetail material missing: {materialPath}");

            string albedoPath = TexturePath(profile, "Albedo");
            string normalPath = TexturePath(profile, "Normal");
            string maskPath = TexturePath(profile, "MetallicSmoothness");
            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            Texture2D mask = AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);
            if (albedo == null || normal == null || mask == null)
                throw new InvalidOperationException($"Ground microdetail texture set incomplete for {profile.MaterialName}.");
            if (albedo.width < TextureSize || normal.width < TextureSize || mask.width < TextureSize)
                throw new InvalidOperationException($"Ground microdetail resolution below {TextureSize}px for {profile.MaterialName}.");
            if (material.GetTexture("_MainTex") != albedo || material.GetTexture("_BumpMap") != normal ||
                material.GetTexture("_MetallicGlossMap") != mask)
                throw new InvalidOperationException($"Ground material texture binding incomplete for {profile.MaterialName}.");
            if (!material.IsKeywordEnabled("_NORMALMAP") || !material.IsKeywordEnabled("_METALLICGLOSSMAP"))
                throw new InvalidOperationException($"Required Standard PBR keywords missing on {profile.MaterialName}.");

            float expectedTile = 1f / profile.PhysicalTileMeters;
            Vector2 scale = material.GetTextureScale("_MainTex");
            if (Mathf.Abs(scale.x - expectedTile) > expectedTile * 0.02f ||
                Mathf.Abs(scale.y - expectedTile) > expectedTile * 0.02f)
                throw new InvalidOperationException(
                    $"Physical texture scale drift on {profile.MaterialName}: got {scale}, expected ~{expectedTile:F2} repeats/m.");

            TextureImporter normalImporter = AssetImporter.GetAtPath(normalPath) as TextureImporter;
            TextureImporter maskImporter = AssetImporter.GetAtPath(maskPath) as TextureImporter;
            if (normalImporter == null || normalImporter.textureType != TextureImporterType.NormalMap)
                throw new InvalidOperationException($"Normal map importer type invalid for {profile.MaterialName}.");
            if (maskImporter == null || maskImporter.sRGBTexture)
                throw new InvalidOperationException($"Metallic/smoothness mask must be linear for {profile.MaterialName}.");
        }

        Debug.Log(
            $"Ground metric UV + microdetail validation passed: metric UV evidence on {metricEvidenceCount}/{meshes.Length} generated meshes; " +
            $"{Profiles.Length} physical-scale PBR texture sets bound. Actual 4K appearance remains render-unverified.");
    }

    private static int RewriteGeneratedDetailMeshesToMetricUv()
    {
        string[] guids = AssetDatabase.FindAssets("t:Mesh", new[] { MeshRoot });
        int changed = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null || !mesh.name.StartsWith("GM_HD_", StringComparison.Ordinal))
                continue;

            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            if (vertices == null || vertices.Length == 0 || normals == null || normals.Length != vertices.Length)
                continue;

            var uv = new Vector2[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
                uv[i] = MetricPlanarUv(vertices[i], normals[i]);
            mesh.uv = uv;
            EditorUtility.SetDirty(mesh);
            changed++;
        }
        return changed;
    }

    private static Vector2 MetricPlanarUv(Vector3 p, Vector3 normal)
    {
        Vector3 a = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
        // UV units are metres. This keeps 1 texture repeat per physical metre before material tiling,
        // independent of object dimensions and without relying on lossy transform scaling.
        if (a.y >= a.x && a.y >= a.z)
            return new Vector2(p.x, p.z);
        if (a.x >= a.z)
            return new Vector2(p.z, p.y);
        return new Vector2(p.x, p.y);
    }

    private static void EnsureProfileTextures(Profile profile)
    {
        string albedoPath = TexturePath(profile, "Albedo");
        string normalPath = TexturePath(profile, "Normal");
        string maskPath = TexturePath(profile, "MetallicSmoothness");

        bool current = IsCurrentGeneratedTexture(albedoPath) &&
                       IsCurrentGeneratedTexture(normalPath) &&
                       IsCurrentGeneratedTexture(maskPath);
        if (current)
            return;

        int pixelCount = TextureSize * TextureSize;
        var heights = new float[pixelCount];
        var albedo = new Color32[pixelCount];
        var normals = new Color32[pixelCount];
        var masks = new Color32[pixelCount];

        for (int y = 0; y < TextureSize; y++)
        {
            float v = y / (float)TextureSize;
            for (int x = 0; x < TextureSize; x++)
            {
                float u = x / (float)TextureSize;
                int index = y * TextureSize + x;
                float h = SurfaceHeight(profile.Kind, u, v, profile.Seed);
                heights[index] = h;

                float colorSignal = SurfaceColorSignal(profile.Kind, u, v, profile.Seed, h);
                float colorMultiplier = 1f + (colorSignal - 0.5f) * 2f * profile.ColorVariation;
                Color c = profile.BaseColor * colorMultiplier;
                c.a = 1f;
                albedo[index] = c;

                float roughSignal = PeriodicFbm(u, v, profile.Seed + 3001, 5);
                float roughness = Mathf.Lerp(profile.RoughnessMin, profile.RoughnessMax, roughSignal);
                float metallicSignal = PeriodicFbm(u, v, profile.Seed + 7001, 4);
                float metallic = Mathf.Lerp(profile.MetallicMin, profile.MetallicMax, metallicSignal);
                byte metallicByte = (byte)Mathf.RoundToInt(Mathf.Clamp01(metallic) * 255f);
                byte smoothByte = (byte)Mathf.RoundToInt((1f - Mathf.Clamp01(roughness)) * 255f);
                masks[index] = new Color32(metallicByte, 0, 0, smoothByte);
            }
        }

        float pixelMeters = profile.PhysicalTileMeters / TextureSize;
        float amplitudeMeters = profile.NormalAmplitudeMm * 0.001f;
        float slopeScale = amplitudeMeters / Mathf.Max(0.0000001f, 2f * pixelMeters);
        for (int y = 0; y < TextureSize; y++)
        {
            int ym = (y - 1 + TextureSize) % TextureSize;
            int yp = (y + 1) % TextureSize;
            for (int x = 0; x < TextureSize; x++)
            {
                int xm = (x - 1 + TextureSize) % TextureSize;
                int xp = (x + 1) % TextureSize;
                float dx = heights[y * TextureSize + xp] - heights[y * TextureSize + xm];
                float dy = heights[yp * TextureSize + x] - heights[ym * TextureSize + x];
                Vector3 n = new Vector3(-dx * slopeScale, -dy * slopeScale, 1f).normalized;
                normals[y * TextureSize + x] = new Color32(
                    (byte)Mathf.RoundToInt((n.x * 0.5f + 0.5f) * 255f),
                    (byte)Mathf.RoundToInt((n.y * 0.5f + 0.5f) * 255f),
                    (byte)Mathf.RoundToInt((n.z * 0.5f + 0.5f) * 255f), 255);
            }
        }

        WriteTexture(albedoPath, albedo, true, false);
        WriteTexture(normalPath, normals, false, true);
        WriteTexture(maskPath, masks, false, false);
    }

    private static void ApplyProfileToMaterial(Profile profile)
    {
        string path = $"{MaterialRoot}/{profile.MaterialName}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
            throw new InvalidOperationException(
                $"Ground material {profile.MaterialName} does not exist. Build high-detail ground infrastructure before microdetail.");

        Shader shader = Shader.Find("Standard");
        if (shader == null)
            throw new InvalidOperationException("Standard shader not found. Generated benchmark currently targets built-in rendering.");

        Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(profile, "Albedo"));
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(profile, "Normal"));
        Texture2D mask = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(profile, "MetallicSmoothness"));
        if (albedo == null || normal == null || mask == null)
            throw new InvalidOperationException($"Generated microdetail textures failed to import for {profile.MaterialName}.");

        material.shader = shader;
        material.color = Color.white;
        material.SetTexture("_MainTex", albedo);
        material.SetTexture("_BumpMap", normal);
        material.SetTexture("_MetallicGlossMap", mask);
        material.EnableKeyword("_NORMALMAP");
        material.EnableKeyword("_METALLICGLOSSMAP");
        material.SetFloat("_BumpScale", 1f);
        material.SetFloat("_Metallic", Mathf.Lerp(profile.MetallicMin, profile.MetallicMax, 0.5f));
        material.SetFloat("_GlossMapScale", 1f);

        float repeatsPerMeter = 1f / profile.PhysicalTileMeters;
        Vector2 scale = Vector2.one * repeatsPerMeter;
        material.SetTextureScale("_MainTex", scale);
        material.SetTextureScale("_BumpMap", scale);
        material.SetTextureScale("_MetallicGlossMap", scale);
        material.SetTextureOffset("_MainTex", Vector2.zero);
        material.SetTextureOffset("_BumpMap", Vector2.zero);
        material.SetTextureOffset("_MetallicGlossMap", Vector2.zero);
        EditorUtility.SetDirty(material);
    }

    private static string TexturePath(Profile profile, string suffix) =>
        $"{TextureRoot}/{profile.TextureStem}_{suffix}.png";

    private static bool IsCurrentGeneratedTexture(string path)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        return texture != null && texture.width >= TextureSize && texture.height >= TextureSize &&
               importer != null && importer.userData == GeneratorVersion;
    }

    private static void WriteTexture(string path, Color32[] pixels, bool sRgb, bool normalMap)
    {
        var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false, !sRgb);
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
            throw new InvalidOperationException($"Texture importer unavailable for generated microdetail texture {path}.");
        importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = sRgb && !normalMap;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Trilinear;
        importer.mipmapEnabled = true;
        importer.anisoLevel = 12;
        importer.maxTextureSize = TextureSize;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.userData = GeneratorVersion;
        importer.SaveAndReimport();
    }

    private static float SurfaceHeight(MicroSurfaceKind kind, float u, float v, int seed)
    {
        float fine = PeriodicFbm(u, v, seed, 6);
        switch (kind)
        {
            case MicroSurfaceKind.GalvanizedSteel:
            {
                // Low-amplitude zinc crystal/oxidation relief. It is deliberately subtle so the
                // surface reads as aged galvanizing rather than glitter or procedural camouflage.
                float coarse = PeriodicValueNoise(u, v, 12, seed + 101);
                return Mathf.Clamp01(fine * 0.58f + coarse * 0.42f);
            }
            case MicroSurfaceKind.CastIron:
            {
                float pits = PeriodicValueNoise(u, v, 46, seed + 211);
                pits = 1f - Mathf.SmoothStep(0.56f, 0.82f, pits);
                return Mathf.Clamp01(fine * 0.70f + pits * 0.30f);
            }
            case MicroSurfaceKind.MineralJoint:
            {
                float grains = PeriodicValueNoise(u, v, 64, seed + 307);
                return Mathf.Clamp01(fine * 0.76f + grains * 0.24f);
            }
            case MicroSurfaceKind.DampConcrete:
            {
                float aggregate = PeriodicValueNoise(u, v, 38, seed + 401);
                return Mathf.Clamp01(fine * 0.62f + aggregate * 0.38f);
            }
            default:
                return fine;
        }
    }

    private static float SurfaceColorSignal(MicroSurfaceKind kind, float u, float v, int seed, float height)
    {
        float broad = PeriodicFbm(u, v, seed + 11003, 4);
        switch (kind)
        {
            case MicroSurfaceKind.GalvanizedSteel:
                return Mathf.Lerp(broad, height, 0.35f);
            case MicroSurfaceKind.CastIron:
                return Mathf.Lerp(broad, height, 0.48f);
            case MicroSurfaceKind.MineralJoint:
                return Mathf.Lerp(broad, height, 0.24f);
            case MicroSurfaceKind.DampConcrete:
                return Mathf.Lerp(broad, height, 0.30f);
            default:
                return broad;
        }
    }

    private static float PeriodicFbm(float u, float v, int seed, int octaves)
    {
        float sum = 0f;
        float weight = 0.5f;
        float total = 0f;
        int cells = 4;
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
}
