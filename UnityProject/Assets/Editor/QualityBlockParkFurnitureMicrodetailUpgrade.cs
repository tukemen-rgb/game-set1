using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Deterministic 4K micro-surface pass for benchmark-visible park/street furniture.
/// This pass does not invent macro dirt or lighting. It encodes only manufacture-scale surface structure:
/// paint orange-peel/chalking, travel-direction stainless abrasion, timber grain/checking, concrete
/// aggregate/pinholes, backing-board fibre and diffuser frosting. Macro weathering remains cause-based.
///
/// Furniture meshes produced by QualityBlockDetailMeshLibrary use dimension-baked geometry but legacy
/// 0..1 face UVs. This pass rewrites only the generated meshes referenced by HD furniture to metre-scale
/// planar UVs. PhysicalChute and PhysicalDiffuser retain their authored UV topology and receive explicit
/// physical tiling derived from the 920 mm chute width / 4.2 m run and 430 x 255 mm globe envelope.
/// </summary>
public static class QualityBlockParkFurnitureMicrodetailUpgrade
{
    private const string MaterialRoot = "Assets/Art/GeneratedParkFurnitureMaterials";
    private const string TextureRoot = MaterialRoot + "/Microdetail";
    private const string ContractPath = "Assets/QA/park_furniture_microdetail_contract.json";
    private const int TextureSize = 1024;
    private const string GeneratorVersion = "park-furniture-microdetail-v1.0.0";

    private enum SurfaceKind
    {
        PaintedSteel,
        StainlessChute,
        ExposedSteel,
        Timber,
        PrecastConcrete,
        ContactConcrete,
        NoticeBacking,
        AgedDiffuser,
    }

    private readonly struct Profile
    {
        public readonly string MaterialName;
        public readonly string TextureStem;
        public readonly SurfaceKind Kind;
        public readonly Color BaseLinear;
        public readonly float ColorVariation;
        public readonly float RoughnessMin;
        public readonly float RoughnessMax;
        public readonly float Metallic;
        public readonly float NormalAmplitudeMm;
        public readonly float PhysicalTileMeters;
        public readonly float NormalScale;
        public readonly int Seed;

        public Profile(string materialName, string textureStem, SurfaceKind kind, Color baseLinear,
            float colorVariation, float roughnessMin, float roughnessMax, float metallic,
            float normalAmplitudeMm, float physicalTileMeters, float normalScale, int seed)
        {
            MaterialName = materialName;
            TextureStem = textureStem;
            Kind = kind;
            BaseLinear = baseLinear;
            ColorVariation = colorVariation;
            RoughnessMin = roughnessMin;
            RoughnessMax = roughnessMax;
            Metallic = metallic;
            NormalAmplitudeMm = normalAmplitudeMm;
            PhysicalTileMeters = physicalTileMeters;
            NormalScale = normalScale;
            Seed = seed;
        }
    }

    private static readonly Profile[] Profiles =
    {
        new Profile("PBR_ParkPaintedSteel", "Park_PaintedSteel", SurfaceKind.PaintedSteel,
            new Color(0.105f, 0.305f, 0.39f), 0.035f, 0.60f, 0.76f, 0.0f, 0.055f, 0.045f, 0.25f, 1801),
        new Profile("PBR_SlideStainless", "Park_SlideStainless", SurfaceKind.StainlessChute,
            new Color(0.49f, 0.515f, 0.52f), 0.018f, 0.18f, 0.30f, 1.0f, 0.028f, 0.060f, 0.18f, 2927),
        new Profile("PBR_ParkExposedSteel", "Park_ExposedSteel", SurfaceKind.ExposedSteel,
            new Color(0.34f, 0.355f, 0.36f), 0.030f, 0.26f, 0.42f, 1.0f, 0.045f, 0.050f, 0.22f, 4073),
        new Profile("PBR_BenchTimber", "Park_BenchTimber", SurfaceKind.Timber,
            new Color(0.255f, 0.145f, 0.072f), 0.085f, 0.66f, 0.84f, 0.0f, 0.28f, 0.180f, 0.45f, 5531),
        new Profile("PBR_ParkPrecastConcrete", "Park_PrecastConcrete", SurfaceKind.PrecastConcrete,
            new Color(0.49f, 0.485f, 0.46f), 0.055f, 0.80f, 0.93f, 0.0f, 0.48f, 0.120f, 0.55f, 6679),
        new Profile("PBR_ParkContactConcrete", "Park_ContactConcrete", SurfaceKind.ContactConcrete,
            new Color(0.395f, 0.39f, 0.37f), 0.065f, 0.86f, 0.96f, 0.0f, 0.62f, 0.140f, 0.60f, 7817),
        new Profile("PBR_NoticeBoardBacking", "Park_NoticeBacking", SurfaceKind.NoticeBacking,
            new Color(0.205f, 0.16f, 0.105f), 0.070f, 0.72f, 0.88f, 0.0f, 0.22f, 0.160f, 0.40f, 8951),
        new Profile("PBR_LampDiffuserAged", "Park_LampDiffuser", SurfaceKind.AgedDiffuser,
            new Color(0.71f, 0.73f, 0.68f), 0.022f, 0.36f, 0.52f, 0.0f, 0.030f, 0.090f, 0.18f, 10091),
    };

    [MenuItem("NewTown/Materials/Build Park Furniture 4K Microdetail")]
    public static void BuildAndApply()
    {
        ValidateContractTokens();
        Directory.CreateDirectory(TextureRoot);

        int rewritten = RewriteFurnitureMeshesToMetricUv();
        foreach (Profile profile in Profiles)
        {
            EnsureProfileTextures(profile);
            ApplyProfileToMaterial(profile);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Validate();
        Debug.Log(
            $"Park furniture microdetail applied: metric-UV referenced meshes={rewritten}, material sets={Profiles.Length}. " +
            "Actual 4K visual quality remains unscored until native Unity renders and 100% crops exist.");
    }

    [MenuItem("NewTown/QA/Validate Park Furniture 4K Microdetail")]
    public static void Validate()
    {
        ValidateContractTokens();
        RequireFurnitureRoots();

        foreach (Profile profile in Profiles)
        {
            string materialPath = $"{MaterialRoot}/{profile.MaterialName}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
                throw new InvalidOperationException($"Park furniture microdetail material missing: {materialPath}");

            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(profile, "Albedo"));
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(profile, "Normal"));
            Texture2D mask = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(profile, "MetallicSmoothness"));
            if (albedo == null || normal == null || mask == null)
                throw new InvalidOperationException($"Incomplete park furniture microdetail texture set: {profile.MaterialName}");
            if (albedo.width < TextureSize || normal.width < TextureSize || mask.width < TextureSize)
                throw new InvalidOperationException($"Park furniture texture below {TextureSize}px: {profile.MaterialName}");

            if (material.GetTexture("_MainTex") != albedo || material.GetTexture("_BumpMap") != normal ||
                material.GetTexture("_MetallicGlossMap") != mask)
                throw new InvalidOperationException($"PBR texture binding incomplete: {profile.MaterialName}");
            if (!material.IsKeywordEnabled("_NORMALMAP") || !material.IsKeywordEnabled("_METALLICGLOSSMAP"))
                throw new InvalidOperationException($"PBR keywords missing: {profile.MaterialName}");

            if (profile.Metallic < 0.5f && material.GetFloat("_Metallic") > 0.05f)
                throw new InvalidOperationException($"Dielectric material became metallic: {profile.MaterialName}");
            if (profile.Metallic > 0.5f && material.GetFloat("_Metallic") < 0.95f)
                throw new InvalidOperationException($"Bare metal must remain fully metallic in the final microdetail pass: {profile.MaterialName}");

            TextureImporter normalImporter = AssetImporter.GetAtPath(TexturePath(profile, "Normal")) as TextureImporter;
            TextureImporter maskImporter = AssetImporter.GetAtPath(TexturePath(profile, "MetallicSmoothness")) as TextureImporter;
            if (normalImporter == null || normalImporter.textureType != TextureImporterType.NormalMap)
                throw new InvalidOperationException($"Normal importer type invalid: {profile.MaterialName}");
            if (maskImporter == null || maskImporter.sRGBTexture)
                throw new InvalidOperationException($"Metallic/smoothness map must be linear: {profile.MaterialName}");
            if (normalImporter.anisoLevel < 8)
                throw new InvalidOperationException($"Insufficient anisotropic filtering for 4K grazing view: {profile.MaterialName}");

            Vector2 expectedScale = ExpectedTextureScale(profile);
            Vector2 actualScale = material.GetTextureScale("_MainTex");
            if (!ApproximatelyRelative(actualScale.x, expectedScale.x, 0.03f) ||
                !ApproximatelyRelative(actualScale.y, expectedScale.y, 0.03f))
                throw new InvalidOperationException(
                    $"Physical texture scale drift on {profile.MaterialName}: got {actualScale}, expected {expectedScale}.");
        }

        ValidateMetricUvEvidence();
        Debug.Log(
            "Park furniture 4K microdetail QA passed at source/asset level. This is implementation readiness only; render evidence is still required for Visual Fidelity points.");
    }

    private static int RewriteFurnitureMeshesToMetricUv()
    {
        GameObject[] roots = RequireFurnitureRoots();
        var seen = new HashSet<Mesh>();
        int changed = 0;

        foreach (GameObject root in roots)
        foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = mf.sharedMesh;
            if (mesh == null || !seen.Add(mesh)) continue;

            // These two meshes deliberately carry topology-aware UVs. Their physical scale is handled
            // by non-uniform material tiling rather than destroying the authored path/circumference axes.
            if (mesh.name.StartsWith("GM_ParkSlide_ContinuousSUS2mm_", StringComparison.Ordinal) ||
                mesh.name.StartsWith("GM_ParkLamp_LathedGlobe_", StringComparison.Ordinal))
                continue;

            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            if (vertices == null || normals == null || vertices.Length == 0 || normals.Length != vertices.Length)
                throw new InvalidOperationException($"Cannot build metric UVs for furniture mesh {mesh.name}.");

            var uv = new Vector2[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
                uv[i] = MetricPlanarUv(vertices[i], normals[i]);
            mesh.uv = uv;
            EditorUtility.SetDirty(mesh);
            changed++;
        }
        return changed;
    }

    private static Vector2 MetricPlanarUv(Vector3 p, Vector3 n)
    {
        Vector3 a = new Vector3(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z));
        if (a.y >= a.x && a.y >= a.z) return new Vector2(p.x, p.z);
        if (a.x >= a.z) return new Vector2(p.z, p.y);
        return new Vector2(p.x, p.y);
    }

    private static void ValidateMetricUvEvidence()
    {
        GameObject[] roots = RequireFurnitureRoots();
        var seen = new HashSet<Mesh>();
        int checkedMeshes = 0;
        int plausibleMetricMeshes = 0;

        foreach (GameObject root in roots)
        foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = mf.sharedMesh;
            if (mesh == null || !seen.Add(mesh)) continue;
            if (mesh.name.StartsWith("GM_ParkSlide_ContinuousSUS2mm_", StringComparison.Ordinal) ||
                mesh.name.StartsWith("GM_ParkLamp_LathedGlobe_", StringComparison.Ordinal))
                continue;

            Vector2[] uv = mesh.uv;
            if (uv == null || uv.Length != mesh.vertexCount)
                throw new InvalidOperationException($"Furniture metric UV missing: {mesh.name}");

            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (Vector2 p in uv)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            float uvSpan = Mathf.Max(max.x - min.x, max.y - min.y);
            float worldSpan = Mathf.Max(mesh.bounds.size.x, Mathf.Max(mesh.bounds.size.y, mesh.bounds.size.z));
            checkedMeshes++;
            if (worldSpan <= 0.01f || (uvSpan / worldSpan >= 0.40f && uvSpan / worldSpan <= 2.50f))
                plausibleMetricMeshes++;
        }

        if (checkedMeshes < 8 || plausibleMetricMeshes < Mathf.CeilToInt(checkedMeshes * 0.70f))
            throw new InvalidOperationException(
                $"Insufficient metre-scale UV evidence on park furniture: {plausibleMetricMeshes}/{checkedMeshes}.");
    }

    private static void EnsureProfileTextures(Profile profile)
    {
        string albedoPath = TexturePath(profile, "Albedo");
        string normalPath = TexturePath(profile, "Normal");
        string maskPath = TexturePath(profile, "MetallicSmoothness");
        if (IsCurrent(albedoPath) && IsCurrent(normalPath) && IsCurrent(maskPath)) return;

        int count = TextureSize * TextureSize;
        var height = new float[count];
        var albedo = new Color32[count];
        var normal = new Color32[count];
        var mask = new Color32[count];

        for (int y = 0; y < TextureSize; y++)
        {
            float v = y / (float)TextureSize;
            for (int x = 0; x < TextureSize; x++)
            {
                float u = x / (float)TextureSize;
                int index = y * TextureSize + x;
                float h = SurfaceHeight(profile.Kind, u, v, profile.Seed);
                height[index] = h;

                float colorSignal = SurfaceColorSignal(profile.Kind, u, v, profile.Seed, h);
                float multiplier = 1f + (colorSignal - 0.5f) * 2f * profile.ColorVariation;
                Color linear = new Color(
                    Mathf.Clamp01(profile.BaseLinear.r * multiplier),
                    Mathf.Clamp01(profile.BaseLinear.g * multiplier),
                    Mathf.Clamp01(profile.BaseLinear.b * multiplier), 1f);
                Color gamma = linear.gamma;
                albedo[index] = (Color32)gamma;

                float roughSignal = SurfaceRoughnessSignal(profile.Kind, u, v, profile.Seed);
                float roughness = Mathf.Lerp(profile.RoughnessMin, profile.RoughnessMax, roughSignal);
                byte metallicByte = (byte)Mathf.RoundToInt(Mathf.Clamp01(profile.Metallic) * 255f);
                byte smoothByte = (byte)Mathf.RoundToInt((1f - Mathf.Clamp01(roughness)) * 255f);
                mask[index] = new Color32(metallicByte, 0, 0, smoothByte);
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
                float dx = height[y * TextureSize + xp] - height[y * TextureSize + xm];
                float dy = height[yp * TextureSize + x] - height[ym * TextureSize + x];
                Vector3 n = new Vector3(-dx * slopeScale, -dy * slopeScale, 1f).normalized;
                normal[y * TextureSize + x] = new Color32(
                    ToByte(n.x * 0.5f + 0.5f), ToByte(n.y * 0.5f + 0.5f), ToByte(n.z * 0.5f + 0.5f), 255);
            }
        }

        WriteTexture(albedoPath, albedo, true, false);
        WriteTexture(normalPath, normal, false, true);
        WriteTexture(maskPath, mask, false, false);
    }

    private static void ApplyProfileToMaterial(Profile profile)
    {
        string path = $"{MaterialRoot}/{profile.MaterialName}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
            throw new InvalidOperationException(
                $"Missing {profile.MaterialName}; build park furniture construction detail before microdetail.");

        Shader shader = Shader.Find("Standard");
        if (shader == null) throw new InvalidOperationException("Standard shader unavailable.");

        Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(profile, "Albedo"));
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(profile, "Normal"));
        Texture2D mask = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(profile, "MetallicSmoothness"));
        if (albedo == null || normal == null || mask == null)
            throw new InvalidOperationException($"Generated microdetail failed to import: {profile.MaterialName}");

        material.shader = shader;
        material.color = Color.white;
        material.SetTexture("_MainTex", albedo);
        material.SetTexture("_BumpMap", normal);
        material.SetTexture("_MetallicGlossMap", mask);
        material.EnableKeyword("_NORMALMAP");
        material.EnableKeyword("_METALLICGLOSSMAP");
        material.SetFloat("_BumpScale", profile.NormalScale);
        material.SetFloat("_Metallic", profile.Metallic);
        material.SetFloat("_GlossMapScale", 1f);
        material.SetFloat("_Glossiness", 1f - (profile.RoughnessMin + profile.RoughnessMax) * 0.5f);

        Vector2 scale = ExpectedTextureScale(profile);
        material.SetTextureScale("_MainTex", scale);
        material.SetTextureScale("_BumpMap", scale);
        material.SetTextureScale("_MetallicGlossMap", scale);
        material.SetTextureOffset("_MainTex", Vector2.zero);
        material.SetTextureOffset("_BumpMap", Vector2.zero);
        material.SetTextureOffset("_MetallicGlossMap", Vector2.zero);
        EditorUtility.SetDirty(material);
    }

    private static Vector2 ExpectedTextureScale(Profile profile)
    {
        switch (profile.Kind)
        {
            case SurfaceKind.StainlessChute:
                // PhysicalChute UV0: U spans 0..1 across 0.92 m; V is already ~metres along the 4.2 m run.
                return new Vector2(0.92f / profile.PhysicalTileMeters, 1f / profile.PhysicalTileMeters);
            case SurfaceKind.AgedDiffuser:
                // Lathed globe UV0: U spans one circumference and V spans the 255 mm profile height.
                return new Vector2((Mathf.PI * 0.43f) / profile.PhysicalTileMeters, 0.255f / profile.PhysicalTileMeters);
            default:
                return Vector2.one * (1f / profile.PhysicalTileMeters);
        }
    }

    private static float SurfaceHeight(SurfaceKind kind, float u, float v, int seed)
    {
        float fine = PeriodicFbm(u, v, seed, 6);
        switch (kind)
        {
            case SurfaceKind.PaintedSteel:
            {
                float peel = PeriodicValueNoise(u, v, 42, seed + 101);
                return Mathf.Clamp01(fine * 0.42f + peel * 0.58f);
            }
            case SurfaceKind.StainlessChute:
            {
                // Grooves vary mostly across U so they run along the chute travel direction (V).
                float scratches = DirectionalScratch(u, v, seed + 211);
                return Mathf.Clamp01(0.58f + (fine - 0.5f) * 0.12f - scratches * 0.28f);
            }
            case SurfaceKind.ExposedSteel:
            {
                float machining = DirectionalScratch(u, v, seed + 307) * 0.55f;
                return Mathf.Clamp01(0.52f + (fine - 0.5f) * 0.34f - machining * 0.10f);
            }
            case SurfaceKind.Timber:
            {
                float wave = Mathf.Sin(Mathf.PI * 2f * (v * 19f + 0.18f * Mathf.Sin(Mathf.PI * 2f * u * 2f + seed * 0.001f)));
                float grain = 0.5f + 0.5f * wave;
                float checks = DirectionalScratch(v, u, seed + 401);
                return Mathf.Clamp01(grain * 0.58f + fine * 0.34f - checks * 0.12f + 0.10f);
            }
            case SurfaceKind.PrecastConcrete:
            {
                float aggregate = PeriodicValueNoise(u, v, 48, seed + 503);
                float pinhole = 1f - Mathf.SmoothStep(0.60f, 0.86f, PeriodicValueNoise(u, v, 72, seed + 541));
                return Mathf.Clamp01(fine * 0.52f + aggregate * 0.38f + pinhole * 0.10f);
            }
            case SurfaceKind.ContactConcrete:
            {
                float aggregate = PeriodicValueNoise(u, v, 38, seed + 607);
                float pits = 1f - Mathf.SmoothStep(0.56f, 0.83f, PeriodicValueNoise(u, v, 58, seed + 631));
                return Mathf.Clamp01(fine * 0.45f + aggregate * 0.40f + pits * 0.15f);
            }
            case SurfaceKind.NoticeBacking:
            {
                float fibre = 0.5f + 0.5f * Mathf.Sin(Mathf.PI * 2f * (v * 27f + 0.11f * Mathf.Sin(Mathf.PI * 2f * u * 3f)));
                return Mathf.Clamp01(fibre * 0.44f + fine * 0.56f);
            }
            case SurfaceKind.AgedDiffuser:
            {
                float frost = PeriodicValueNoise(u, v, 64, seed + 709);
                return Mathf.Clamp01(fine * 0.35f + frost * 0.65f);
            }
            default:
                return fine;
        }
    }

    private static float SurfaceColorSignal(SurfaceKind kind, float u, float v, int seed, float height)
    {
        float broad = PeriodicFbm(u, v, seed + 17011, 4);
        if (kind == SurfaceKind.StainlessChute || kind == SurfaceKind.ExposedSteel)
            return Mathf.Lerp(0.5f, broad, 0.35f);
        if (kind == SurfaceKind.Timber || kind == SurfaceKind.NoticeBacking)
            return Mathf.Lerp(broad, height, 0.52f);
        if (kind == SurfaceKind.AgedDiffuser)
            return Mathf.Lerp(0.5f, broad, 0.28f);
        return Mathf.Lerp(broad, height, 0.30f);
    }

    private static float SurfaceRoughnessSignal(SurfaceKind kind, float u, float v, int seed)
    {
        float baseSignal = PeriodicFbm(u, v, seed + 23003, 5);
        if (kind == SurfaceKind.StainlessChute)
            return Mathf.Clamp01(baseSignal * 0.58f + DirectionalScratch(u, v, seed + 233) * 0.42f);
        if (kind == SurfaceKind.Timber)
            return Mathf.Clamp01(baseSignal * 0.66f + PeriodicValueNoise(u, v, 24, seed + 271) * 0.34f);
        return baseSignal;
    }

    private static float DirectionalScratch(float across, float along, int seed)
    {
        float phaseA = Hash01(seed, 17, seed) * Mathf.PI * 2f;
        float phaseB = Hash01(seed, 31, seed + 7) * Mathf.PI * 2f;
        float wander = 0.018f * Mathf.Sin(Mathf.PI * 2f * along * 2f + phaseB);
        float a = Mathf.Abs(Mathf.Sin(Mathf.PI * 2f * ((across + wander) * 47f) + phaseA));
        float b = Mathf.Abs(Mathf.Sin(Mathf.PI * 2f * ((across - wander * 0.6f) * 83f) + phaseB));
        float ridgeA = Mathf.Pow(1f - a, 18f);
        float ridgeB = Mathf.Pow(1f - b, 26f);
        return Mathf.Clamp01(ridgeA * 0.62f + ridgeB * 0.38f);
    }

    private static float PeriodicFbm(float u, float v, int seed, int octaves)
    {
        float sum = 0f;
        float total = 0f;
        float weight = 0.5f;
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
        if (importer == null) throw new InvalidOperationException($"Texture importer unavailable: {path}");
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

    private static string TexturePath(Profile profile, string suffix) =>
        $"{TextureRoot}/{profile.TextureStem}_{suffix}.png";

    private static bool IsCurrent(string path)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        return texture != null && texture.width >= TextureSize && texture.height >= TextureSize &&
               importer != null && importer.userData == GeneratorVersion;
    }

    private static GameObject[] RequireFurnitureRoots()
    {
        string[] names = { "HD_Slide", "HD_Bench", "HD_Lamp", "HD_NoticeBoard" };
        var roots = new GameObject[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            roots[i] = GameObject.Find(names[i]);
            if (roots[i] == null)
                throw new InvalidOperationException($"Furniture microdetail requires manufactured assembly: {names[i]}");
        }
        return roots;
    }

    private static void ValidateContractTokens()
    {
        if (!File.Exists(ContractPath))
            throw new InvalidOperationException($"Missing required park furniture microdetail metadata: {ContractPath}");
        string json = File.ReadAllText(ContractPath);
        string[] tokens =
        {
            "\"textureResolution\": 1024", "\"painted_steel\"", "\"stainless_chute\"",
            "\"bench_timber\"", "\"precast_concrete\"", "\"aged_diffuser\"",
            "\"physicalTileMeters\"", "\"roughnessRange\"", "\"metallic\"",
            "\"normalAmplitudeMm\"", "\"wetness\"", "\"uvAging\"", "\"angularResponse\"",
            "\"visualFidelityPointsAwarded\": 0", "PENDING_UNITY_RUNTIME"
        };
        foreach (string token in tokens)
            if (!json.Contains(token, StringComparison.Ordinal))
                throw new InvalidOperationException($"Park furniture microdetail contract missing token: {token}");
    }

    private static bool ApproximatelyRelative(float actual, float expected, float tolerance)
    {
        float denom = Mathf.Max(0.0001f, Mathf.Abs(expected));
        return Mathf.Abs(actual - expected) / denom <= tolerance;
    }

    private static byte ToByte(float value) =>
        (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);
}
