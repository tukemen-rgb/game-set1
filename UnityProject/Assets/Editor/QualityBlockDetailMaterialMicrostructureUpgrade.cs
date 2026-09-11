using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Deterministic, lighting-neutral microstructure for the generated apartment-detail materials.
/// This upgrades the scalar-only fallback materials without baking highlights or weathering into albedo.
/// Metallic/smoothness live in Standard's R/A map and micro-relief lives in a tangent-space normal map.
/// Visual Fidelity points remain unavailable until the native 4K evidence is rendered and reviewed.
/// </summary>
public static class QualityBlockDetailMaterialMicrostructureUpgrade
{
    private const string Root = "Assets/Art/GeneratedDetailMaterials";
    private const int TextureSize = 1024;
    private const float Epsilon = 0.0001f;

    private enum SurfaceKind
    {
        AnodizedAluminum,
        GalvanizedSteel,
        EpdmRubber,
        AgedAbs,
        PipeWrap,
        DrainHose,
    }

    private sealed class Profile
    {
        public readonly string materialName;
        public readonly Color baseColor;
        public readonly float metallicMin;
        public readonly float metallicMax;
        public readonly float roughnessMin;
        public readonly float roughnessMax;
        public readonly float uvTiling;
        public readonly float normalStrength;
        public readonly int seed;
        public readonly SurfaceKind kind;

        public Profile(string materialName, Color baseColor,
            float metallicMin, float metallicMax, float roughnessMin, float roughnessMax,
            float uvTiling, float normalStrength, int seed, SurfaceKind kind)
        {
            this.materialName = materialName;
            this.baseColor = baseColor;
            this.metallicMin = metallicMin;
            this.metallicMax = metallicMax;
            this.roughnessMin = roughnessMin;
            this.roughnessMax = roughnessMax;
            this.uvTiling = uvTiling;
            this.normalStrength = normalStrength;
            this.seed = seed;
            this.kind = kind;
        }
    }

    // The ranges intentionally sit inside material_construction_lookdev.json rather than touching
    // its hard edges. That leaves room for texture filtering/mip averaging while keeping physically
    // impossible metallic or mirror-like response out of the source material.
    private static readonly Profile[] Profiles =
    {
        new Profile("MAT_AgedAluminum", new Color(0.55f, 0.57f, 0.56f),
            0.82f, 0.94f, 0.43f, 0.55f, 14f, 0.52f, 311, SurfaceKind.AnodizedAluminum),
        new Profile("MAT_DarkGalvanizedSteel", new Color(0.30f, 0.31f, 0.30f),
            0.82f, 0.94f, 0.54f, 0.70f, 11f, 0.68f, 509, SurfaceKind.GalvanizedSteel),
        new Profile("MAT_WindowRubber", new Color(0.075f, 0.078f, 0.075f),
            0f, 0f, 0.82f, 0.92f, 18f, 0.42f, 719, SurfaceKind.EpdmRubber),
        new Profile("MAT_AgedACPlastic", new Color(0.72f, 0.71f, 0.66f),
            0f, 0f, 0.63f, 0.76f, 12f, 0.38f, 907, SurfaceKind.AgedAbs),
        new Profile("MAT_PipeInsulation", new Color(0.68f, 0.66f, 0.60f),
            0f, 0f, 0.76f, 0.88f, 8f, 0.50f, 1117, SurfaceKind.PipeWrap),
        new Profile("MAT_DrainHose", new Color(0.36f, 0.36f, 0.33f),
            0f, 0f, 0.73f, 0.86f, 7f, 0.62f, 1321, SurfaceKind.DrainHose),
    };

    [DidReloadScripts]
    private static void BootstrapAfterScriptReload()
    {
        // In batch/runner execution this must happen before an executeMethod can build the scene.
        // In the interactive Editor a delay avoids competing with the tail of the script import pass.
        if (Application.isBatchMode)
        {
            TryBootstrap();
            return;
        }

        EditorApplication.delayCall += TryBootstrap;
    }

    private static void TryBootstrap()
    {
        try
        {
            EnsureGeneratedAssets(false);
        }
        catch (Exception ex)
        {
            Debug.LogError("Detail-material microstructure bootstrap failed; formal material QA must fail closed until corrected.\n" + ex);
        }
    }

    [MenuItem("NewTown/Materials/Build Detail Material Microstructure")]
    public static void BuildFromMenu()
    {
        EnsureGeneratedAssets(true);
    }

    public static void EnsureGeneratedAssets(bool log = false)
    {
        Directory.CreateDirectory(Root);
        Shader standard = Shader.Find("Standard");
        if (standard == null)
            throw new InvalidOperationException("Unity Standard shader is unavailable for detail-material microstructure generation.");

        foreach (Profile profile in Profiles)
            BuildProfile(profile, standard);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ValidateGeneratedAssets(false);

        if (log)
            Debug.Log("Detail-material microstructure generated and validated. This is implementation readiness only; no Visual Fidelity points were assigned.");
    }

    [MenuItem("NewTown/QA/Validate Detail Material Microstructure")]
    public static void ValidateFromMenu()
    {
        ValidateGeneratedAssets(true);
    }

    public static void ValidateGeneratedAssets(bool log = false)
    {
        foreach (Profile profile in Profiles)
        {
            string materialPath = MaterialPath(profile);
            string normalPath = NormalPath(profile);
            string maskPath = MaskPath(profile);

            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            Texture2D mask = AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);

            if (material == null || normal == null || mask == null)
                throw new InvalidOperationException($"Microstructure asset set incomplete for {profile.materialName}.");
            if (material.shader == null || material.shader.name != "Standard")
                throw new InvalidOperationException($"{profile.materialName} must use Unity Standard for the inspected R/A metallic-smoothness semantics.");
            if (material.GetTexture("_BumpMap") != normal || material.GetTexture("_MetallicGlossMap") != mask)
                throw new InvalidOperationException($"{profile.materialName} is not bound to its canonical normal/mask assets.");
            if (!material.IsKeywordEnabled("_NORMALMAP") || !material.IsKeywordEnabled("_METALLICGLOSSMAP"))
                throw new InvalidOperationException($"{profile.materialName} is missing Standard normal/metallic map keywords.");
            if (Mathf.Abs(material.GetFloat("_GlossMapScale") - 1f) > Epsilon)
                throw new InvalidOperationException($"{profile.materialName} must keep _GlossMapScale=1 so source alpha remains the inspected smoothness value.");
            if (Mathf.RoundToInt(material.GetFloat("_SmoothnessTextureChannel")) != 0)
                throw new InvalidOperationException($"{profile.materialName} must source smoothness from metallic-map alpha, not albedo alpha.");
            if ((material.mainTextureScale - Vector2.one * profile.uvTiling).sqrMagnitude > Epsilon)
                throw new InvalidOperationException($"{profile.materialName} microstructure tiling drifted from {profile.uvTiling}.");
            if (material.renderQueue > 2500 || material.GetFloat("_Mode") > Epsilon || material.GetFloat("_ZWrite") < 0.999f)
                throw new InvalidOperationException($"{profile.materialName} must remain opaque with depth writes enabled.");
            if (material.IsKeywordEnabled("_EMISSION") || material.GetColor("_EmissionColor").maxColorComponent > Epsilon)
                throw new InvalidOperationException($"{profile.materialName} must not use emissive energy to fake highlights.");

            float fallbackMetallic = material.GetFloat("_Metallic");
            float fallbackRoughness = 1f - material.GetFloat("_Glossiness");
            if (fallbackMetallic < profile.metallicMin - Epsilon || fallbackMetallic > profile.metallicMax + Epsilon ||
                fallbackRoughness < profile.roughnessMin - Epsilon || fallbackRoughness > profile.roughnessMax + Epsilon)
                throw new InvalidOperationException($"{profile.materialName} fallback scalars are outside the same physically plausible range as its maps.");

            ValidateImporter(normalPath, true);
            ValidateImporter(maskPath, false);
        }

        if (log)
            Debug.Log("Detail-material microstructure source assets valid. Rendered grazing-light verification remains mandatory before Visual Fidelity scoring.");
    }

    private static void BuildProfile(Profile profile, Shader standard)
    {
        string normalPath = NormalPath(profile);
        string maskPath = MaskPath(profile);
        WriteTextureIfChanged(normalPath, BuildNormalPng(profile));
        WriteTextureIfChanged(maskPath, BuildMaskPng(profile));
        ConfigureImporter(normalPath, true);
        ConfigureImporter(maskPath, false);

        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
        Texture2D mask = AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);
        if (normal == null || mask == null)
            throw new InvalidOperationException($"Unity failed to import generated microstructure textures for {profile.materialName}.");

        string materialPath = MaterialPath(profile);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(standard) { name = profile.materialName };
            AssetDatabase.CreateAsset(material, materialPath);
        }
        else
        {
            material.shader = standard;
        }

        float fallbackMetallic = (profile.metallicMin + profile.metallicMax) * 0.5f;
        float fallbackRoughness = (profile.roughnessMin + profile.roughnessMax) * 0.5f;

        material.color = profile.baseColor;
        material.mainTextureScale = Vector2.one * profile.uvTiling;
        material.mainTextureOffset = Vector2.zero;
        material.SetTexture("_BumpMap", normal);
        material.SetFloat("_BumpScale", 1f);
        material.EnableKeyword("_NORMALMAP");
        material.SetTexture("_MetallicGlossMap", mask);
        material.SetFloat("_Metallic", fallbackMetallic);
        material.SetFloat("_Glossiness", 1f - fallbackRoughness);
        material.SetFloat("_GlossMapScale", 1f);
        material.SetFloat("_SmoothnessTextureChannel", 0f);
        material.EnableKeyword("_METALLICGLOSSMAP");
        material.SetColor("_EmissionColor", Color.black);
        material.DisableKeyword("_EMISSION");
        material.SetFloat("_Mode", 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.One);
        material.SetFloat("_DstBlend", (float)BlendMode.Zero);
        material.SetFloat("_ZWrite", 1f);
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.SetOverrideTag("RenderType", "Opaque");
        material.renderQueue = -1;
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
    }

    private static byte[] BuildNormalPng(Profile profile)
    {
        var height = new float[TextureSize * TextureSize];
        for (int y = 0; y < TextureSize; y++)
        {
            float v = y / (float)TextureSize;
            for (int x = 0; x < TextureSize; x++)
            {
                float u = x / (float)TextureSize;
                height[y * TextureSize + x] = SurfaceHeight(profile, u, v);
            }
        }

        var pixels = new Color32[height.Length];
        float derivativeGain = profile.normalStrength * 18f;
        for (int y = 0; y < TextureSize; y++)
        {
            int ym = (y + TextureSize - 1) % TextureSize;
            int yp = (y + 1) % TextureSize;
            for (int x = 0; x < TextureSize; x++)
            {
                int xm = (x + TextureSize - 1) % TextureSize;
                int xp = (x + 1) % TextureSize;
                float dx = height[y * TextureSize + xp] - height[y * TextureSize + xm];
                float dy = height[yp * TextureSize + x] - height[ym * TextureSize + x];
                Vector3 n = new Vector3(-dx * derivativeGain, -dy * derivativeGain, 1f).normalized;
                pixels[y * TextureSize + x] = new Color(
                    n.x * 0.5f + 0.5f,
                    n.y * 0.5f + 0.5f,
                    n.z * 0.5f + 0.5f,
                    1f);
            }
        }

        return EncodePng(pixels, true);
    }

    private static byte[] BuildMaskPng(Profile profile)
    {
        var pixels = new Color32[TextureSize * TextureSize];
        for (int y = 0; y < TextureSize; y++)
        {
            float v = y / (float)TextureSize;
            for (int x = 0; x < TextureSize; x++)
            {
                float u = x / (float)TextureSize;
                float rNoise = PeriodicNoise01(u, v, profile.seed + 37);
                float mNoise = PeriodicNoise01(u, v, profile.seed + 83);
                float roughness = Mathf.Lerp(profile.roughnessMin, profile.roughnessMax, rNoise);
                float metallic = Mathf.Lerp(profile.metallicMin, profile.metallicMax, mNoise);
                pixels[y * TextureSize + x] = new Color(metallic, 0f, 0f, 1f - roughness);
            }
        }

        return EncodePng(pixels, true);
    }

    private static float SurfaceHeight(Profile profile, float u, float v)
    {
        float n0 = PeriodicNoise01(u, v, profile.seed);
        float n1 = PeriodicNoise01(u, v, profile.seed + 191);
        float phase = (profile.seed % 997) * 0.0137f;
        float h;

        switch (profile.kind)
        {
            case SurfaceKind.AnodizedAluminum:
                // Extrusion grain: fine, directional, low amplitude. The secondary isotropic term keeps
                // cross-face response from becoming an obviously synthetic comb pattern.
                h = 0.50f + 0.055f * Mathf.Sin(2f * Mathf.PI * (u * 96f + phase)) +
                    0.030f * (n0 - 0.5f) + 0.018f * (n1 - 0.5f);
                break;
            case SurfaceKind.GalvanizedSteel:
                // Broad zinc-spangle-like mottling without glitter pixels or baked light direction.
                h = 0.50f + 0.105f * (n0 - 0.5f) + 0.075f * (n1 - 0.5f) +
                    0.025f * Mathf.Sin(2f * Mathf.PI * (u * 9f - v * 7f + phase));
                break;
            case SurfaceKind.EpdmRubber:
                h = 0.50f + 0.075f * (n0 - 0.5f) + 0.050f * (n1 - 0.5f);
                break;
            case SurfaceKind.AgedAbs:
                // Fine injection-mould orange peel; yellowing belongs in albedo/aging policy, not relief.
                h = 0.50f + 0.060f * (n0 - 0.5f) + 0.030f *
                    Mathf.Sin(2f * Mathf.PI * (u * 23f + v * 19f + phase));
                break;
            case SurfaceKind.PipeWrap:
                // Shallow wrap seam cadence with secondary cloth/foam skin texture.
                h = 0.50f + 0.045f * Mathf.Sin(2f * Mathf.PI * (u * 8f + v * 2f + phase)) +
                    0.050f * (n0 - 0.5f);
                break;
            case SurfaceKind.DrainHose:
                // Circumferential corrugation signal for cylindrical hose UVs, softened by fine polymer grain.
                h = 0.50f + 0.065f * Mathf.Sin(2f * Mathf.PI * (v * 28f + phase)) +
                    0.035f * (n0 - 0.5f);
                break;
            default:
                h = n0;
                break;
        }

        return Mathf.Clamp01(h);
    }

    private static float PeriodicNoise01(float u, float v, int seed)
    {
        // Integer frequencies guarantee continuity at texture borders; no non-periodic Unity Perlin seam.
        float p0 = seed * 0.0174532925f;
        float p1 = seed * 0.0314159265f;
        float p2 = seed * 0.0471238898f;
        float a = Mathf.Sin(2f * Mathf.PI * (u * 7f + v * 11f) + p0);
        float b = Mathf.Sin(2f * Mathf.PI * (u * 17f - v * 13f) + p1);
        float c = Mathf.Cos(2f * Mathf.PI * (u * 31f + v * 29f) + p2);
        float d = Mathf.Sin(2f * Mathf.PI * (u * 61f - v * 47f) + p0 + p2);
        return Mathf.Clamp01(0.5f + 0.22f * a + 0.14f * b + 0.09f * c + 0.05f * d);
    }

    private static byte[] EncodePng(Color32[] pixels, bool linear)
    {
        var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false, linear);
        try
        {
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture.EncodeToPNG();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    private static void WriteTextureIfChanged(string path, byte[] bytes)
    {
        bool changed = !File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(bytes);
        if (!changed)
            return;

        File.WriteAllBytes(path, bytes);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    }

    private static void ConfigureImporter(string path, bool normalMap)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException($"TextureImporter missing for generated texture: {path}");

        importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = false;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Trilinear;
        importer.mipmapEnabled = true;
        importer.anisoLevel = 16;
        importer.maxTextureSize = TextureSize;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }

    private static void ValidateImporter(string path, bool normalMap)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException($"TextureImporter missing for {path}.");
        TextureImporterType expected = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
        if (importer.textureType != expected || importer.sRGBTexture || !importer.mipmapEnabled ||
            importer.wrapMode != TextureWrapMode.Repeat || importer.filterMode != FilterMode.Trilinear || importer.anisoLevel < 8)
            throw new InvalidOperationException($"Generated microstructure importer drifted from the anti-aliasing contract: {path}");
    }

    private static string MaterialPath(Profile p) => $"{Root}/{p.materialName}.mat";
    private static string NormalPath(Profile p) => $"{Root}/{p.materialName}_MicroNormal.png";
    private static string MaskPath(Profile p) => $"{Root}/{p.materialName}_MetallicSmoothness.png";
}