using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Removes the exact 2.4 m macro recurrence from the formal painted-RC facade while preserving the
/// intended 0.22 m detail-normal frequency. Unity's built-in Standard shader shares _DetailAlbedoMap_ST
/// between secondary albedo and secondary normal sampling, so using Standard's detail-albedo slot for a
/// 7.9 m anti-repeat field would also stretch the detail normal from 0.22 m to 7.9 m. The formal material
/// therefore switches, after physical UV construction, to a small Standard-lighting surface shader with
/// independent texture transforms for primary coating, detail normal and neutral macro variation.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockFacadeMacroAntiRepetitionUpgrade
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string RootName = "DanchiFacadeApertureShell";
    private const string AssetRoot = "Assets/Art/GeneratedFacadeOptics";
    private const string TexturePath = AssetRoot + "/FacadeRC_AntiRepeatAlbedo.png";
    private const string MaterialPath = AssetRoot + "/MAT_FacadePaintedRC_AperturePhysicalUV.mat";
    private const string ContractPath = "Assets/QA/facade_macro_anti_repetition_contract.json";
    private const string ShaderName = "NewTown/FacadePaintedRC";
    private const int TextureSize = 1024;
    private const float PrimaryMeters = 2.4f;
    private const float DetailNormalMeters = 0.22f;
    private const float AntiRepeatMeters = 7.9f;
    private const float ExpectedCombinedMeters = 189.6f;
    private const float MinCombinedMeters = 100f;
    private const float NeutralSrgb = 0.504f;
    private const float ModulationSrgb = 0.0115f;
    private static readonly Vector2 PrimaryScale = Vector2.one;
    private static readonly Vector2 DetailNormalScale = Vector2.one * (PrimaryMeters / DetailNormalMeters);
    private static readonly Vector2 MacroVariationScale = Vector2.one * (PrimaryMeters / AntiRepeatMeters);
    private static bool applying;

    static QualityBlockFacadeMacroAntiRepetitionUpgrade()
    {
        EditorSceneManager.sceneSaved -= OnSceneSaved;
        EditorSceneManager.sceneSaved += OnSceneSaved;
        Camera.onPreCull -= OnCameraPreCull;
        Camera.onPreCull += OnCameraPreCull;
    }

    [MenuItem("NewTown/Materials/Apply Facade Macro Anti-Repetition Layer")]
    public static void ApplyAndValidate()
    {
        EnsureScene();
        ValidateContractConfigOnly();
        RequireRoot();
        ApplyMaterialState();
        ValidatePreparedState();
        Debug.Log($"Facade anti-repeat valid: primary={PrimaryMeters:F2} m, detailNormal={DetailNormalMeters:F2} m, macroVariation={AntiRepeatMeters:F1} m, exactCombinedRepeat={CombinedRepeatMeters():F1} m. Visual Fidelity remains UNSCORED.");
    }

    [MenuItem("NewTown/QA/Validate Facade Macro Anti-Repetition Contract")]
    public static void ValidateContractConfigOnly()
    {
        if (!File.Exists(ContractPath))
            throw new InvalidOperationException($"Missing facade anti-repeat contract: {ContractPath}");
        string json = File.ReadAllText(ContractPath);
        string[] required =
        {
            "\"schemaVersion\": \"1.1.0\"",
            "\"shaderName\": \"NewTown/FacadePaintedRC\"",
            "\"primaryMacroRepeatMeters\": 2.4",
            "\"detailNormalRepeatMeters\": 0.22",
            "\"antiRepeatMacroRepeatMeters\": 7.9",
            "\"combinedExactRepeatMeters\": 189.6",
            "\"macroVariationProperty\": \"_MacroVariationMap\"",
            "\"criticalDefectRiskReduced\": \"immediately_obvious_repeated_texture_or_module_pattern\"",
            "\"sharedStandardDetailUvHazardRejected\": true",
            "\"paintedHighlightsForbidden\": true",
            "\"visualFidelityPointsAwarded\": 0",
            "\"runtimeRenderVerification\": \"PENDING_UNITY_RUNTIME\""
        };
        foreach (string token in required)
            if (json.IndexOf(token, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException($"Facade anti-repeat contract missing token: {token}");
        float combined = CombinedRepeatMeters();
        if (Mathf.Abs(combined - ExpectedCombinedMeters) > 0.001f || combined < MinCombinedMeters)
            throw new InvalidOperationException($"Facade repeat arithmetic invalid: {combined:F3} m.");
    }

    [MenuItem("NewTown/QA/Validate Formal Facade Macro Anti-Repetition State")]
    public static void ValidateOpenScene()
    {
        EnsureScene();
        ValidateContractConfigOnly();
        RequireRoot();
        ValidatePreparedState();
    }

    private static void OnSceneSaved(Scene scene)
    {
        if (applying || !scene.IsValid() || scene.path != ScenePath || FindSceneObject(RootName) == null ||
            AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) == null)
            return;
        applying = true;
        try
        {
            ValidateContractConfigOnly();
            ApplyMaterialState();
            ValidatePreparedState();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Benchmark save blocked by facade macro anti-repetition/detail-frequency QA.", ex);
        }
        finally { applying = false; }
    }

    private static void OnCameraPreCull(Camera camera)
    {
        if (camera == null || camera.targetTexture == null) return;
        string n = camera.targetTexture.name ?? string.Empty;
        if (!n.StartsWith("QA4K_", StringComparison.Ordinal) &&
            !n.StartsWith("QATemporal_", StringComparison.Ordinal) &&
            !n.StartsWith("QAPreparedTemporal_", StringComparison.Ordinal)) return;
        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath) return;
        ValidateContractConfigOnly();
        RequireRoot();
        ValidatePreparedState();
    }

    private static void ApplyMaterialState()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null) throw new InvalidOperationException($"Formal facade material missing: {MaterialPath}");
        Texture main = RequireTexture(material, "_MainTex");
        Texture bump = RequireTexture(material, "_BumpMap");
        Texture mask = RequireTexture(material, "_MetallicGlossMap");
        Texture detailNormal = RequireTexture(material, "_DetailNormalMap");
        float bumpStrength = material.HasProperty("_BumpScale") ? material.GetFloat("_BumpScale") : 0.82f;
        float detailStrength = material.HasProperty("_DetailNormalMapScale") ? material.GetFloat("_DetailNormalMapScale") : 0.42f;
        float glossScale = material.HasProperty("_GlossMapScale") ? material.GetFloat("_GlossMapScale") : 1f;
        Color color = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;

        Shader shader = Shader.Find(ShaderName);
        if (shader == null) throw new InvalidOperationException($"Required facade shader not found: {ShaderName}");
        Texture2D macro = EnsureTexture();
        material.shader = shader;
        material.name = "MAT_FacadePaintedRC_AperturePhysicalUV";
        material.SetColor("_Color", color);
        material.SetTexture("_MainTex", main);
        material.SetTexture("_BumpMap", bump);
        material.SetTexture("_MetallicGlossMap", mask);
        material.SetTexture("_DetailNormalMap", detailNormal);
        material.SetTexture("_MacroVariationMap", macro);
        SetTextureTransform(material, "_MainTex", PrimaryScale);
        SetTextureTransform(material, "_BumpMap", PrimaryScale);
        SetTextureTransform(material, "_MetallicGlossMap", PrimaryScale);
        SetTextureTransform(material, "_DetailNormalMap", DetailNormalScale);
        SetTextureTransform(material, "_MacroVariationMap", MacroVariationScale);
        material.SetFloat("_BumpScale", bumpStrength);
        material.SetFloat("_DetailNormalMapScale", detailStrength);
        material.SetFloat("_GlossMapScale", glossScale);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Glossiness", 0.14f);
        material.SetColor("_EmissionColor", Color.black);
        // Retain legacy validation keywords as compatibility metadata; this shader uses explicit texture paths.
        material.EnableKeyword("_NORMALMAP");
        material.EnableKeyword("_DETAIL_MULX2");
        material.DisableKeyword("_EMISSION");
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
    }

    private static Texture RequireTexture(Material material, string property)
    {
        if (!material.HasProperty(property))
            throw new InvalidOperationException($"Formal facade material lost required property {property} before shader handoff.");
        Texture value = material.GetTexture(property);
        if (value == null) throw new InvalidOperationException($"Formal facade material has no texture bound to {property}.");
        return value;
    }

    private static void SetTextureTransform(Material material, string property, Vector2 scale)
    {
        if (!material.HasProperty(property)) throw new InvalidOperationException($"Facade shader missing property {property}.");
        material.SetTextureScale(property, scale);
        material.SetTextureOffset(property, Vector2.zero);
    }

    private static Texture2D EnsureTexture()
    {
        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (existing != null) return existing;
        Directory.CreateDirectory(AssetRoot);
        var pixels = new Color32[TextureSize * TextureSize];
        float tau = Mathf.PI * 2f;
        for (int y = 0; y < TextureSize; y++)
        {
            float v = y / (float)TextureSize;
            for (int x = 0; x < TextureSize; x++)
            {
                float u = x / (float)TextureSize;
                float n =
                    Mathf.Sin(tau * (u + 2f * v) + 0.73f) * 0.31f +
                    Mathf.Cos(tau * (2f * u - 3f * v) + 1.91f) * 0.24f +
                    Mathf.Sin(tau * (4f * u + v) + 2.47f) * 0.19f +
                    Mathf.Cos(tau * (3f * u + 5f * v) + 0.28f) * 0.15f +
                    Mathf.Sin(tau * (7f * u - 4f * v) + 1.17f) * 0.11f;
                float value = Mathf.Clamp(NeutralSrgb + n * ModulationSrgb, 0.490f, 0.516f);
                byte b = (byte)Mathf.RoundToInt(value * 255f);
                pixels[y * TextureSize + x] = new Color32(b, b, b, 255);
            }
        }
        var temp = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false, false);
        try
        {
            temp.SetPixels32(pixels); temp.Apply(false, false);
            File.WriteAllBytes(TexturePath, temp.EncodeToPNG());
        }
        finally { UnityEngine.Object.DestroyImmediate(temp); }
        AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
        if (importer == null) throw new InvalidOperationException($"TextureImporter unavailable: {TexturePath}");
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Trilinear;
        importer.mipmapEnabled = true;
        importer.anisoLevel = 8;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.alphaSource = TextureImporterAlphaSource.None;
        importer.SaveAndReimport();
        Texture2D result = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (result == null) throw new InvalidOperationException($"Failed to import {TexturePath}");
        return result;
    }

    private static void ValidatePreparedState()
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (m == null || m.shader == null || m.shader.name != ShaderName)
            throw new InvalidOperationException($"Formal facade must use {ShaderName} after physical-UV construction.");
        ValidateTextureBinding(m, "_MainTex", null, PrimaryScale);
        ValidateTextureBinding(m, "_BumpMap", null, PrimaryScale);
        ValidateTextureBinding(m, "_MetallicGlossMap", null, PrimaryScale);
        ValidateTextureBinding(m, "_DetailNormalMap", null, DetailNormalScale);
        ValidateTextureBinding(m, "_MacroVariationMap", TexturePath, MacroVariationScale);
        Texture main = m.GetTexture("_MainTex");
        Texture macro = m.GetTexture("_MacroVariationMap");
        if (main == macro) throw new InvalidOperationException("Macro variation may not reuse primary albedo.");
        if (Mathf.Abs(DetailNormalScale.x - MacroVariationScale.x) < 0.01f)
            throw new InvalidOperationException("Detail-normal and macro-variation frequencies collapsed together.");
        if (m.GetFloat("_Metallic") > 0.001f)
            throw new InvalidOperationException("Painted RC scalar metallic must remain zero.");
        if (m.IsKeywordEnabled("_EMISSION") || m.GetColor("_EmissionColor").maxColorComponent > 0.001f)
            throw new InvalidOperationException("Painted RC may not emit light.");
        Texture2D macro2d = macro as Texture2D;
        if (macro2d == null || macro2d.width != TextureSize || macro2d.height != TextureSize || macro2d.mipmapCount <= 1)
            throw new InvalidOperationException("Macro variation texture resolution/mipmap contract failed.");
        TextureImporter importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
        if (importer == null || !importer.sRGBTexture || !importer.mipmapEnabled || importer.wrapMode != TextureWrapMode.Repeat || importer.filterMode != FilterMode.Trilinear || importer.anisoLevel < 8)
            throw new InvalidOperationException("Macro variation importer must be sRGB, mipmapped, Repeat, Trilinear, anisotropy >= 8.");
        float combined = CombinedRepeatMeters();
        if (combined < MinCombinedMeters || Mathf.Abs(combined - ExpectedCombinedMeters) > 0.001f)
            throw new InvalidOperationException($"Combined facade repeat arithmetic invalid: {combined:F3} m.");
    }

    private static void ValidateTextureBinding(Material m, string property, string expectedPath, Vector2 expectedScale)
    {
        if (!m.HasProperty(property) || m.GetTexture(property) == null)
            throw new InvalidOperationException($"Formal facade missing {property}.");
        if (expectedPath != null && !string.Equals(AssetDatabase.GetAssetPath(m.GetTexture(property)), expectedPath, StringComparison.Ordinal))
            throw new InvalidOperationException($"{property} uses unexpected asset: {AssetDatabase.GetAssetPath(m.GetTexture(property))}");
        if ((m.GetTextureScale(property) - expectedScale).sqrMagnitude > 0.000002f || m.GetTextureOffset(property).sqrMagnitude > 0.000001f)
            throw new InvalidOperationException($"{property} physical scale/offset drifted. Got scale={m.GetTextureScale(property)}, expected={expectedScale}.");
    }

    private static float CombinedRepeatMeters()
    {
        int a = Mathf.RoundToInt(PrimaryMeters * 1000f), b = Mathf.RoundToInt(AntiRepeatMeters * 1000f);
        int x = a, y = b;
        while (y != 0) { int t = x % y; x = y; y = t; }
        return ((long)a / Mathf.Max(1, Mathf.Abs(x)) * b) / 1000f;
    }

    private static void RequireRoot()
    {
        if (FindSceneObject(RootName) == null) throw new InvalidOperationException($"Formal facade root missing: {RootName}");
    }

    private static void EnsureScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static GameObject FindSceneObject(string name) => Resources.FindObjectsOfTypeAll<GameObject>()
        .FirstOrDefault(x => x != null && x.scene.IsValid() && x.scene.path == ScenePath && x.name == name);
}
