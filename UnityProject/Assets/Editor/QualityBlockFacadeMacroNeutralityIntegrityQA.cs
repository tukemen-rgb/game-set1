using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Fail-closed source integrity for the facade anti-repetition field. The 7.9 m layer is permitted to
/// modulate only neutral diffuse reflectance; it may not become a colored texture, a directional/baked
/// highlight, emission, or an unreviewed replacement image. This gate intentionally awards no visual
/// points: native 4K hero/oblique/grazing evidence remains authoritative.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockFacadeMacroNeutralityIntegrityQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string TexturePath = "Assets/Art/GeneratedFacadeOptics/FacadeRC_AntiRepeatAlbedo.png";
    private const string ShaderPath = "Assets/Shaders/NewTownFacadePaintedRC.shader";
    private const string ContractPath = "Assets/QA/facade_macro_neutrality_integrity_contract.json";
    private const int TextureSize = 1024;
    private const float NeutralSrgb = 0.504f;
    private const float ModulationSrgb = 0.0115f;
    private const float MinSrgb = 0.490f;
    private const float MaxSrgb = 0.516f;

    private static string cachedContentHash;
    private static bool cachedSourceValid;

    static QualityBlockFacadeMacroNeutralityIntegrityQA()
    {
        Camera.onPreCull -= OnCameraPreCull;
        Camera.onPreCull += OnCameraPreCull;
    }

    [MenuItem("NewTown/QA/Validate Facade Macro Neutrality Integrity")]
    public static void ValidateFromMenu()
    {
        ValidateContractConfigOnly();
        ValidateShaderSource();
        ValidateSourceTexture();
        Debug.Log("Facade macro neutrality/source integrity passed. Visual Fidelity remains UNSCORED pending native 4K review.");
    }

    public static void ValidateContractConfigOnly()
    {
        if (!File.Exists(ContractPath))
            throw new InvalidOperationException($"Missing facade macro neutrality contract: {ContractPath}");

        string json = File.ReadAllText(ContractPath);
        string[] required =
        {
            "\"schemaVersion\": \"1.0.0\"",
            "\"texturePath\": \"Assets/Art/GeneratedFacadeOptics/FacadeRC_AntiRepeatAlbedo.png\"",
            "\"shaderPath\": \"Assets/Shaders/NewTownFacadePaintedRC.shader\"",
            "\"neutralSrgb\": 0.504",
            "\"modulationSrgb\": 0.0115",
            "\"minimumSrgb\": 0.49",
            "\"maximumSrgb\": 0.516",
            "\"exactProceduralSourceRequired\": true",
            "\"coloredMacroVariationForbidden\": true",
            "\"directionalOrBakedHighlightForbidden\": true",
            "\"emissiveMacroContributionForbidden\": true",
            "\"visualFidelityPointsAwarded\": 0",
            "\"runtimeRenderVerification\": \"PENDING_UNITY_RUNTIME\""
        };

        foreach (string token in required)
            if (json.IndexOf(token, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException($"Facade macro neutrality contract missing token: {token}");
    }

    private static void OnCameraPreCull(Camera camera)
    {
        if (camera == null || camera.targetTexture == null) return;
        string targetName = camera.targetTexture.name ?? string.Empty;
        if (!targetName.StartsWith("QA4K_", StringComparison.Ordinal) &&
            !targetName.StartsWith("QATemporal_", StringComparison.Ordinal) &&
            !targetName.StartsWith("QAPreparedTemporal_", StringComparison.Ordinal)) return;

        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath) return;

        ValidateContractConfigOnly();
        ValidateShaderSource();
        ValidateSourceTexture();
    }

    private static void ValidateShaderSource()
    {
        if (!File.Exists(ShaderPath))
            throw new InvalidOperationException($"Facade macro shader source missing: {ShaderPath}");

        string shader = File.ReadAllText(ShaderPath);
        string[] required =
        {
            "Shader \"NewTown/FacadePaintedRC\"",
            "sampler2D _MacroVariationMap;",
            "half3 macroVariation = tex2D(_MacroVariationMap, IN.uv_MacroVariationMap).rgb * unity_ColorSpaceDouble.rgb;",
            "o.Albedo = saturate(baseSample.rgb * macroVariation);",
            "o.Emission = 0.0h;"
        };
        foreach (string token in required)
            if (shader.IndexOf(token, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException($"Facade macro shader lost required neutral-lighting semantic: {token}");

        if (shader.IndexOf("o.Emission = macroVariation", StringComparison.Ordinal) >= 0 ||
            shader.IndexOf("o.Emission=macroVariation", StringComparison.Ordinal) >= 0)
            throw new InvalidOperationException("Facade macro variation may not contribute emissive energy.");
    }

    private static void ValidateSourceTexture()
    {
        if (!File.Exists(TexturePath))
            throw new InvalidOperationException(
                $"Facade macro source texture is missing: {TexturePath}. Run the facade macro anti-repetition preparation before formal capture.");

        byte[] png = File.ReadAllBytes(TexturePath);
        string contentHash = Sha256Hex(png);
        if (cachedSourceValid && string.Equals(contentHash, cachedContentHash, StringComparison.Ordinal))
            return;

        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
        try
        {
            if (!ImageConversion.LoadImage(texture, png, false))
                throw new InvalidOperationException("Facade macro source PNG could not be decoded for neutrality verification.");
            if (texture.width != TextureSize || texture.height != TextureSize)
                throw new InvalidOperationException(
                    $"Facade macro source must be {TextureSize}x{TextureSize}; got {texture.width}x{texture.height}.");

            Color32[] pixels = texture.GetPixels32();
            if (pixels == null || pixels.Length != TextureSize * TextureSize)
                throw new InvalidOperationException("Facade macro source pixel count is invalid.");

            byte observedMin = 255;
            byte observedMax = 0;
            long sum = 0;
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
                    float value = Mathf.Clamp(NeutralSrgb + n * ModulationSrgb, MinSrgb, MaxSrgb);
                    byte expected = (byte)Mathf.RoundToInt(value * 255f);
                    Color32 actual = pixels[y * TextureSize + x];
                    if (actual.r != expected || actual.g != expected || actual.b != expected || actual.a != 255)
                        throw new InvalidOperationException(
                            $"Facade macro source differs from the approved neutral procedural field at ({x},{y}). " +
                            $"Expected RGBA=({expected},{expected},{expected},255), got ({actual.r},{actual.g},{actual.b},{actual.a}). " +
                            "Formal evidence is blocked because colored/baked-light content cannot be ruled out.");
                    if (expected < observedMin) observedMin = expected;
                    if (expected > observedMax) observedMax = expected;
                    sum += expected;
                }
            }

            if (observedMax - observedMin < 3)
                throw new InvalidOperationException("Facade macro source lost useful low-amplitude variation and became effectively flat.");

            double mean = sum / (double)pixels.Length / 255.0;
            if (mean < 0.495 || mean > 0.510)
                throw new InvalidOperationException($"Facade macro source mean sRGB drifted from neutral policy: {mean:F6}.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }

        cachedContentHash = contentHash;
        cachedSourceValid = true;
    }

    private static string Sha256Hex(byte[] bytes)
    {
        using (SHA256 sha = SHA256.Create())
        {
            byte[] digest = sha.ComputeHash(bytes);
            return BitConverter.ToString(digest).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
