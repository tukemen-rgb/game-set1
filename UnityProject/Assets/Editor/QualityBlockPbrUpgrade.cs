using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Generates a deterministic, tileable PBR starter library for the Unity quality block and
/// applies it to the 1990s new-town benchmark scene. The generated textures are a baseline
/// that can later be replaced by authored/photogrammetry assets without changing scene code.
///
/// Menu: NewTown > Materials > Build PBR Library and Rebuild Quality Block
/// Output: Assets/Art/GeneratedPBR/*.{png,mat}
/// </summary>
public static class QualityBlockPbrUpgrade
{
    const string Root = "Assets/Art/GeneratedPBR";
    const int TextureSize = 1024;

    enum SurfaceKind
    {
        Grass,
        Dirt,
        Concrete,
        WashedConcrete,
        Paving,
        Leaf,
        Bark,
    }

    readonly struct Profile
    {
        public readonly string Name;
        public readonly SurfaceKind Kind;
        public readonly Color BaseColor;
        public readonly float ColorVariation;
        public readonly float NormalStrength;
        public readonly float Roughness;
        public readonly float TileScale;
        public readonly int Seed;

        public Profile(string name, SurfaceKind kind, Color baseColor, float colorVariation,
            float normalStrength, float roughness, float tileScale, int seed)
        {
            Name = name;
            Kind = kind;
            BaseColor = baseColor;
            ColorVariation = colorVariation;
            NormalStrength = normalStrength;
            Roughness = roughness;
            TileScale = tileScale;
            Seed = seed;
        }
    }

    static readonly Profile[] Profiles =
    {
        new("PBR_GrassWorn", SurfaceKind.Grass, new Color(0.24f, 0.34f, 0.12f), 0.24f, 3.2f, 0.91f, 5.0f, 1975),
        new("PBR_DrySoil", SurfaceKind.Dirt, new Color(0.39f, 0.29f, 0.18f), 0.20f, 2.5f, 0.96f, 4.0f, 302),
        new("PBR_DanchiConcrete", SurfaceKind.Concrete, new Color(0.63f, 0.62f, 0.58f), 0.13f, 1.6f, 0.88f, 3.0f, 905),
        new("PBR_WashedConcrete", SurfaceKind.WashedConcrete, new Color(0.56f, 0.55f, 0.51f), 0.18f, 2.2f, 0.90f, 3.5f, 621),
        new("PBR_WarmPaving", SurfaceKind.Paving, new Color(0.53f, 0.52f, 0.48f), 0.11f, 1.4f, 0.91f, 4.5f, 77),
        new("PBR_LeafDark", SurfaceKind.Leaf, new Color(0.12f, 0.27f, 0.075f), 0.28f, 2.7f, 0.86f, 2.8f, 7),
        new("PBR_LeafMid", SurfaceKind.Leaf, new Color(0.16f, 0.33f, 0.09f), 0.27f, 2.7f, 0.85f, 2.8f, 19),
        new("PBR_Bark", SurfaceKind.Bark, new Color(0.25f, 0.16f, 0.095f), 0.25f, 3.0f, 0.95f, 2.8f, 444),
    };

    [MenuItem("NewTown/Materials/Build PBR Library and Rebuild Quality Block")]
    public static void BuildPbrQualityBlock()
    {
        GenerateLibrary();
        BuildQualityBlock1990s.Build();
        ApplyToOpenScene();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("PBR quality block rebuilt. Capture with NewTown > QA > Capture Quality Block PNG.");
    }

    [MenuItem("NewTown/Materials/Generate PBR Library Only")]
    public static void GenerateLibrary()
    {
        Directory.CreateDirectory(Root);
        foreach (var p in Profiles)
            BuildProfile(p);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Generated {Profiles.Length} PBR materials in {Root}");
    }

    static void BuildProfile(Profile p)
    {
        int n = TextureSize * TextureSize;
        var heights = new float[n];
        var albedo = new Color32[n];
        var normals = new Color32[n];
        var masks = new Color32[n];

        for (int y = 0; y < TextureSize; y++)
        {
            float v = y / (float)TextureSize;
            for (int x = 0; x < TextureSize; x++)
            {
                float u = x / (float)TextureSize;
                int i = y * TextureSize + x;
                float h = SurfaceHeight(p.Kind, u, v, p.Seed);
                heights[i] = h;

                Color c = SurfaceColor(p, u, v, h);
                albedo[i] = c;

                float rough = Mathf.Clamp01(p.Roughness + (h - 0.5f) * 0.12f);
                byte smooth = (byte)Mathf.RoundToInt((1f - rough) * 255f);
                masks[i] = new Color32(0, 0, 0, smooth); // Standard shader: R metallic, A smoothness.
            }
        }

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
                Vector3 normal = new Vector3(-dx * p.NormalStrength, -dy * p.NormalStrength, 1f).normalized;
                normals[y * TextureSize + x] = new Color32(
                    (byte)Mathf.RoundToInt((normal.x * 0.5f + 0.5f) * 255f),
                    (byte)Mathf.RoundToInt((normal.y * 0.5f + 0.5f) * 255f),
                    (byte)Mathf.RoundToInt((normal.z * 0.5f + 0.5f) * 255f), 255);
            }
        }

        string albedoPath = $"{Root}/{p.Name}_Albedo.png";
        string normalPath = $"{Root}/{p.Name}_Normal.png";
        string maskPath = $"{Root}/{p.Name}_MetallicSmoothness.png";
        WriteTexture(albedoPath, albedo, true, false);
        WriteTexture(normalPath, normals, false, true);
        WriteTexture(maskPath, masks, false, false);

        var shader = Shader.Find("Standard");
        if (shader == null)
            throw new InvalidOperationException("Standard shader not found. This project currently targets Unity built-in rendering.");

        string matPath = $"{Root}/{p.Name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(shader) { name = p.Name };
            AssetDatabase.CreateAsset(mat, matPath);
        }
        else
        {
            mat.shader = shader;
        }

        mat.color = Color.white;
        mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
        mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
        mat.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath));
        mat.EnableKeyword("_NORMALMAP");
        mat.EnableKeyword("_METALLICGLOSSMAP");
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_GlossMapScale", 1f);
        mat.mainTextureScale = Vector2.one * p.TileScale;
        EditorUtility.SetDirty(mat);
    }

    static void WriteTexture(string path, Color32[] pixels, bool sRgb, bool normalMap)
    {
        var tex = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false, !sRgb);
        tex.SetPixels32(pixels);
        tex.Apply(false, false);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = sRgb && !normalMap;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Trilinear;
        importer.mipmapEnabled = true;
        importer.anisoLevel = 8;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.SaveAndReimport();
    }

    static float SurfaceHeight(SurfaceKind kind, float u, float v, int seed)
    {
        float baseNoise = FbmPeriodic(u, v, seed);
        switch (kind)
        {
            case SurfaceKind.Grass:
                return Mathf.Clamp01(baseNoise * 0.68f + BladeNoise(u, v, seed) * 0.32f);
            case SurfaceKind.Dirt:
                return Mathf.Clamp01(baseNoise * 0.82f + PebbleNoise(u, v, seed) * 0.18f);
            case SurfaceKind.Concrete:
                return Mathf.Clamp01(baseNoise * 0.72f + StreakNoise(u, seed) * 0.28f);
            case SurfaceKind.WashedConcrete:
                return Mathf.Clamp01(baseNoise * 0.45f + AggregateNoise(u, v, seed) * 0.55f);
            case SurfaceKind.Paving:
                return Mathf.Clamp01(baseNoise * 0.72f + JointNoise(u, v) * 0.28f);
            case SurfaceKind.Leaf:
                return Mathf.Clamp01(baseNoise * 0.75f + VeinNoise(u, v) * 0.25f);
            case SurfaceKind.Bark:
                return Mathf.Clamp01(baseNoise * 0.58f + BarkNoise(u, v, seed) * 0.42f);
            default:
                return baseNoise;
        }
    }

    static Color SurfaceColor(Profile p, float u, float v, float h)
    {
        float centered = (h - 0.5f) * 2f;
        Color c = p.BaseColor * (1f + centered * p.ColorVariation);

        if (p.Kind == SurfaceKind.Grass)
        {
            // Daily wear: sparse dry/bare flecks, not damage or disaster marks.
            float bare = Mathf.SmoothStep(0.80f, 0.98f, FbmPeriodic(u + 0.17f, v + 0.31f, p.Seed + 91));
            c = Color.Lerp(c, new Color(0.34f, 0.25f, 0.13f), bare * 0.42f);
        }
        else if (p.Kind == SurfaceKind.Concrete)
        {
            float streak = StreakNoise(u, p.Seed + 13);
            c *= Mathf.Lerp(0.86f, 1.03f, streak);
        }
        else if (p.Kind == SurfaceKind.WashedConcrete)
        {
            float aggregate = AggregateNoise(u, v, p.Seed + 7);
            c = Color.Lerp(c * 0.88f, c * 1.11f, aggregate);
        }
        else if (p.Kind == SurfaceKind.Paving)
        {
            float joint = JointNoise(u, v);
            c *= Mathf.Lerp(0.73f, 1.02f, joint);
        }
        else if (p.Kind == SurfaceKind.Leaf)
        {
            float sunVariation = FbmPeriodic(u + 0.4f, v + 0.1f, p.Seed + 121);
            c *= Mathf.Lerp(0.82f, 1.12f, sunVariation);
        }

        c.a = 1f;
        return new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), 1f);
    }

    static float FbmPeriodic(float u, float v, int seed)
    {
        float phase = (seed % 997) * 0.0137f;
        float sum = 0f;
        float weight = 0f;
        int[] frequencies = { 1, 2, 4, 8, 16, 32 };
        float amp = 1f;
        foreach (int f in frequencies)
        {
            float a = Mathf.Sin(Mathf.PI * 2f * (u * f + v * (f + 1)) + phase);
            float b = Mathf.Cos(Mathf.PI * 2f * (u * (f + 2) - v * f) + phase * 1.73f);
            sum += ((a + b) * 0.25f + 0.5f) * amp;
            weight += amp;
            amp *= 0.52f;
        }
        return Mathf.Clamp01(sum / weight);
    }

    static float BladeNoise(float u, float v, int seed)
    {
        float a = Mathf.Abs(Mathf.Sin(Mathf.PI * 2f * (u * 73f + v * 17f) + seed));
        float b = Mathf.Abs(Mathf.Cos(Mathf.PI * 2f * (u * 29f - v * 61f) + seed * 0.37f));
        return Mathf.Pow(Mathf.Clamp01((a + b) * 0.5f), 2.2f);
    }

    static float PebbleNoise(float u, float v, int seed)
    {
        float a = Mathf.Sin(Mathf.PI * 2f * (u * 37f + v * 41f) + seed * 0.17f);
        float b = Mathf.Cos(Mathf.PI * 2f * (u * 53f - v * 31f) + seed * 0.23f);
        return Mathf.Clamp01((a * b) * 0.5f + 0.5f);
    }

    static float StreakNoise(float u, int seed)
    {
        float a = Mathf.Sin(Mathf.PI * 2f * u * 7f + seed * 0.13f);
        float b = Mathf.Sin(Mathf.PI * 2f * u * 19f + seed * 0.07f);
        return Mathf.Clamp01(0.5f + a * 0.24f + b * 0.13f);
    }

    static float AggregateNoise(float u, float v, int seed)
    {
        float a = Mathf.Sin(Mathf.PI * 2f * (u * 43f + v * 47f) + seed * 0.11f);
        float b = Mathf.Cos(Mathf.PI * 2f * (u * 59f - v * 37f) + seed * 0.19f);
        return Mathf.Pow(Mathf.Clamp01((a * b) * 0.5f + 0.5f), 0.72f);
    }

    static float JointNoise(float u, float v)
    {
        float gu = Mathf.Abs(Mathf.Sin(Mathf.PI * u * 8f));
        float gv = Mathf.Abs(Mathf.Sin(Mathf.PI * v * 8f));
        return Mathf.SmoothStep(0.08f, 0.22f, Mathf.Min(gu, gv));
    }

    static float VeinNoise(float u, float v)
    {
        float center = 1f - Mathf.Clamp01(Mathf.Abs(v - 0.5f) * 12f);
        float side = Mathf.Abs(Mathf.Sin(Mathf.PI * 2f * (u * 11f + v * 3f)));
        return Mathf.Clamp01(center * 0.7f + side * 0.3f);
    }

    static float BarkNoise(float u, float v, int seed)
    {
        float vertical = Mathf.Abs(Mathf.Sin(Mathf.PI * 2f * (u * 17f + FbmPeriodic(u, v, seed) * 0.8f)));
        float knots = FbmPeriodic(u * 0.5f, v, seed + 33);
        return Mathf.Clamp01(vertical * 0.72f + knots * 0.28f);
    }

    static void ApplyToOpenScene()
    {
        var mats = new Dictionary<string, Material>(StringComparer.Ordinal);
        foreach (var p in Profiles)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>($"{Root}/{p.Name}.mat");
            if (m == null)
                throw new InvalidOperationException($"Missing generated material: {p.Name}");
            mats[p.Name] = m;
        }

        foreach (var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            string n = renderer.gameObject.name;
            Material replacement = null;

            if (n == "GrassField") replacement = mats["PBR_GrassWorn"];
            else if (n.StartsWith("WornPath", StringComparison.Ordinal)) replacement = mats["PBR_DrySoil"];
            else if (n == "DanchiPlaza" || n == "ParkPath") replacement = mats["PBR_WarmPaving"];
            else if (n == "MainBlock" || n == "StairTower" || n.StartsWith("BalconyFloor_", StringComparison.Ordinal)) replacement = mats["PBR_DanchiConcrete"];
            else if (n.StartsWith("BenchLeg", StringComparison.Ordinal) || n == "LampPole") replacement = mats["PBR_WashedConcrete"];
            else if (n.StartsWith("Trunk_", StringComparison.Ordinal)) replacement = mats["PBR_Bark"];
            else if (n.StartsWith("Crown_", StringComparison.Ordinal))
            {
                int hash = StableNameHash(n);
                replacement = (hash & 1) == 0 ? mats["PBR_LeafDark"] : mats["PBR_LeafMid"];
            }

            if (replacement != null)
                renderer.sharedMaterial = replacement;
        }
    }

    static int StableNameHash(string value)
    {
        unchecked
        {
            int h = 17;
            for (int i = 0; i < value.Length; i++)
                h = h * 31 + value[i];
            return h;
        }
    }
}
