using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds a deterministic, tileable micro-normal + metallic/smoothness pair for the dry balcony
/// waterproof finish. The macro 1:50 fall remains geometry; this pass only represents the sub-centimetre
/// coating/topcoat texture that would otherwise leave a broad, perfectly uniform CG-looking plane.
///
/// This is intentionally a formal-preparation pass rather than an always-on scene mutation. It must run
/// after the drainage/waterproofing assembly has created MAT_BalconyWaterproofingDry and before reflection
/// synchronization. Once a probe cycle begins, QA is read-only and any drift fails closed.
/// </summary>
public static class QualityBlockBalconySurfaceMicrostructureUpgrade
{
    public const string NormalTexturePath =
        QualityBlockBalconyDrainageWaterproofingUpgrade.GeneratedRoot + "/TX_BalconyWaterproofing_MicroNormal.png";
    public const string MetallicSmoothnessTexturePath =
        QualityBlockBalconyDrainageWaterproofingUpgrade.GeneratedRoot + "/TX_BalconyWaterproofing_MetallicSmoothness.png";

    public const int TextureSize = 1024;
    public const float TextureWorldPeriodM = 0.50f;
    public const float TextureScalePerMeterUv = 1f / TextureWorldPeriodM;
    public const float RuntimeNormalScale = 0.18f;
    public const float BaseSmoothness = 0.28f;
    public const float SmoothnessMin = 0.22f;
    public const float SmoothnessMax = 0.34f;

    // Encoding strength only shapes the tangent-space normal stored in the texture. RuntimeNormalScale is
    // the deliberately conservative perceptual amplitude used by the Standard shader.
    private const float NormalEncodingStrength = 1.65f;
    private const int NoiseSeed = 1998;

    [MenuItem("NewTown/Materials/Build Balcony Waterproof Microstructure")]
    public static void BuildAndApply()
    {
        QualityBlockBalconySurfaceMicrostructureQA.ValidateContractConfigOnly();
        Directory.CreateDirectory(QualityBlockBalconyDrainageWaterproofingUpgrade.GeneratedRoot);

        GenerateTextures();

        Material material = AssetDatabase.LoadAssetAtPath<Material>(
            QualityBlockBalconyDrainageWaterproofingUpgrade.WaterproofMaterialPath);
        if (material == null)
            throw new InvalidOperationException(
                "Balcony waterproof material does not exist. Build the drainage/waterproofing assembly first.");
        if (material.shader == null || !string.Equals(material.shader.name, "Standard", StringComparison.Ordinal))
            throw new InvalidOperationException("Balcony waterproof microstructure currently requires the built-in Standard shader.");

        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalTexturePath);
        Texture2D mask = AssetDatabase.LoadAssetAtPath<Texture2D>(MetallicSmoothnessTexturePath);
        if (normal == null || mask == null)
            throw new InvalidOperationException("Generated balcony microstructure textures did not import.");

        // Keep base colour uniform so no illumination/highlight is baked into albedo. The texture pair only
        // contributes microsurface orientation and physically bounded dielectric roughness variation.
        material.mainTexture = null;
        material.mainTextureScale = Vector2.one * TextureScalePerMeterUv;
        material.mainTextureOffset = Vector2.zero;
        material.SetTexture("_BumpMap", normal);
        material.SetFloat("_BumpScale", RuntimeNormalScale);
        material.EnableKeyword("_NORMALMAP");
        material.SetTexture("_MetallicGlossMap", mask);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Glossiness", BaseSmoothness);
        material.SetFloat("_GlossMapScale", 1f);
        material.EnableKeyword("_METALLICGLOSSMAP");
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
        material.DisableKeyword("_EMISSION");
        EditorUtility.SetDirty(material);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        QualityBlockBalconySurfaceMicrostructureQA.ValidateAssetsAndMaterial();
    }

    private static void GenerateTextures()
    {
        int count = TextureSize * TextureSize;
        float[] height = new float[count];
        Color32[] normals = new Color32[count];
        Color32[] masks = new Color32[count];

        for (int y = 0; y < TextureSize; y++)
        {
            float v = y / (float)TextureSize;
            for (int x = 0; x < TextureSize; x++)
            {
                float u = x / (float)TextureSize;
                int i = y * TextureSize + x;
                float h = PeriodicMicroHeight(u, v, NoiseSeed);
                height[i] = h;

                float roughNoise = PeriodicMicroHeight(u + 0.173f, v + 0.319f, NoiseSeed + 73);
                float smoothness = Mathf.Clamp(
                    BaseSmoothness + (h - 0.5f) * 0.050f + (roughNoise - 0.5f) * 0.030f,
                    SmoothnessMin, SmoothnessMax);
                byte smooth = (byte)Mathf.RoundToInt(smoothness * 255f);
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
                float dx = height[y * TextureSize + xp] - height[y * TextureSize + xm];
                float dy = height[yp * TextureSize + x] - height[ym * TextureSize + x];
                Vector3 n = new Vector3(-dx * NormalEncodingStrength, -dy * NormalEncodingStrength, 1f).normalized;
                normals[y * TextureSize + x] = new Color32(
                    EncodeNormalChannel(n.x), EncodeNormalChannel(n.y), EncodeNormalChannel(n.z), 255);
            }
        }

        WriteTexture(NormalTexturePath, normals, true);
        WriteTexture(MetallicSmoothnessTexturePath, masks, false);
    }

    private static byte EncodeNormalChannel(float value)
    {
        return (byte)Mathf.Clamp(Mathf.RoundToInt((value * 0.5f + 0.5f) * 255f), 0, 255);
    }

    /// <summary>
    /// Seamless integer-frequency field. It intentionally avoids directional streaks: runoff/staining is
    /// cause-based weathering and must not be baked into a generic waterproof material.
    /// </summary>
    private static float PeriodicMicroHeight(float u, float v, int seed)
    {
        float phase = (seed % 997) * 0.0173f;
        float sum = 0f;
        float weight = 0f;
        int[] frequencies = { 7, 11, 19, 31, 47, 71 };
        for (int i = 0; i < frequencies.Length; i++)
        {
            int f = frequencies[i];
            float a = Mathf.Sin(Mathf.PI * 2f * (u * f + v * (f + 2)) + phase + i * 0.91f);
            float b = Mathf.Cos(Mathf.PI * 2f * (u * (f + 3) - v * f) - phase * 0.73f + i * 1.37f);
            float amp = 1f / (1f + i * 0.85f);
            sum += (a * 0.57f + b * 0.43f) * amp;
            weight += amp;
        }
        float normalized = 0.5f + 0.5f * (sum / Mathf.Max(weight, 0.0001f));
        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(normalized));
    }

    private static void WriteTexture(string assetPath, Color32[] pixels, bool normalMap)
    {
        string absolute = AbsolutePath(assetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute));

        Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false, true);
        try
        {
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            File.WriteAllBytes(absolute, texture.EncodeToPNG());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException("Could not load TextureImporter for " + assetPath);
        importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = false;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Trilinear;
        importer.mipmapEnabled = true;
        importer.anisoLevel = 8;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.SaveAndReimport();
    }

    private static string AbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }
}
