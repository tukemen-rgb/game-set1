using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Adds a second, deliberately incommensurate neutral coating-variation field to the formal painted-RC
/// facade material. The existing physical-UV pass correctly prevents each apartment bay from restarting
/// UV0, but the generated primary coating texture is itself exactly periodic every 2.4 m. On a 26 m hero
/// facade that can still expose about 10.8 identical macro cycles. This pass uses Standard's detail-albedo
/// channel as a low-amplitude coating/application variation at 7.9 m. The two periods do not exactly meet
/// again until 189.6 m, so the formal facade does not repeat the same combined macro appearance within the
/// benchmark block. No light direction, shadow, runoff or specular highlight is painted into this map.
///
/// This is source-side risk reduction for the critical repeated-pattern defect only. It never clears the
/// defect or awards Visual Fidelity points without sealed native-4K and temporal evidence.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockFacadeMacroAntiRepetitionUpgrade
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ApertureRootName = "DanchiFacadeApertureShell";
    private const string AssetRoot = "Assets/Art/GeneratedFacadeOptics";
    private const string TexturePath = AssetRoot + "/FacadeRC_AntiRepeatAlbedo.png";
    private const string FormalMaterialPath = AssetRoot + "/MAT_FacadePaintedRC_AperturePhysicalUV.mat";
    private const string ContractPath = "Assets/QA/facade_macro_anti_repetition_contract.json";
    private const int TextureSize = 1024;
    private const float PrimaryMacroRepeatMeters = 2.4f;
    private const float AntiRepeatMacroRepeatMeters = 7.9f;
    private const float ExpectedCombinedRepeatMeters = 189.6f;
    private const float NeutralDetailSrgb = 0.504f;
    private const float ModulationAmplitudeSrgb = 0.0115f;
    private const float MinimumCombinedRepeatMeters = 100f;
    private static readonly Vector2 ExpectedPhysicalUvScale = Vector2.one * (PrimaryMacroRepeatMeters / AntiRepeatMacroRepeatMeters);
    private static bool applyingAfterSave;

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
        EnsureSceneOpen();
        ValidateContractConfigOnly();
        RequireFormalApertureRoot();
        ApplyMaterialState();
        ValidatePreparedState();
        Debug.Log(
            $"Facade macro anti-repetition layer applied: primary={PrimaryMacroRepeatMeters:F1} m, secondary={AntiRepeatMacroRepeatMeters:F1} m, " +
            $"first exact combined repeat={ComputeCombinedRepeatMeters():F1} m. Visual Fidelity remains UNSCORED until native 4K/temporal review.");
    }

    [MenuItem("NewTown/QA/Validate Facade Macro Anti-Repetition Contract")]
    public static void ValidateContractConfigOnly()
    {
        if (!File.Exists(ContractPath))
            throw new InvalidOperationException($"Missing facade macro anti-repetition contract: {ContractPath}");

        string json = File.ReadAllText(ContractPath);
        string[] requiredTokens =
        {
            "\"schemaVersion\": \"1.0.0\"",
            "\"primaryMacroRepeatMeters\": 2.4",
            "\"antiRepeatMacroRepeatMeters\": 7.9",
            "\"combinedExactRepeatMeters\": 189.6",
            "\"formalMaterial\": \"Assets/Art/GeneratedFacadeOptics/MAT_FacadePaintedRC_AperturePhysicalUV.mat\"",
            "\"generatedTexture\": \"Assets/Art/GeneratedFacadeOptics/FacadeRC_AntiRepeatAlbedo.png\"",
            "\"detailAlbedoProperty\": \"_DetailAlbedoMap\"",
            "\"criticalDefectRiskReduced\": \"immediately_obvious_repeated_texture_or_module_pattern\"",
            "\"paintedHighlightsForbidden\": true",
            "\"directionalWeatheringForbidden\": true",
            "\"visualFidelityPointsAwarded\": 0",
            "\"runtimeRenderVerification\": \"PENDING_UNITY_RUNTIME\""
        };
        foreach (string token in requiredTokens)
            if (json.IndexOf(token, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException($"Facade macro anti-repetition contract missing required token: {token}");

        float combined = ComputeCombinedRepeatMeters();
        if (Mathf.Abs(combined - ExpectedCombinedRepeatMeters) > 0.001f)
            throw new InvalidOperationException(
                $"Facade macro repeat arithmetic drifted: calculated {combined:F3} m, expected {ExpectedCombinedRepeatMeters:F3} m.");
        if (combined < MinimumCombinedRepeatMeters)
            throw new InvalidOperationException(
                $"Facade combined macro repeat is too short: {combined:F1} m < {MinimumCombinedRepeatMeters:F1} m.");
    }

    [MenuItem("NewTown/QA/Validate Formal Facade Macro Anti-Repetition State")]
    public static void ValidateOpenScene()
    {
        EnsureSceneOpen();
        ValidateContractConfigOnly();
        RequireFormalApertureRoot();
        ValidatePreparedState();
        Debug.Log(
            $"Formal facade macro anti-repetition state valid: detail-albedo period={AntiRepeatMacroRepeatMeters:F1} m, " +
            $"combined exact repeat={ComputeCombinedRepeatMeters():F1} m, automatic visual points=0. Native 4K review still required.");
    }

    private static void OnSceneSaved(Scene scene)
    {
        if (applyingAfterSave || !scene.IsValid() || scene.path != ScenePath)
            return;
        if (FindSceneObject(ApertureRootName) == null)
            return;
        if (AssetDatabase.LoadAssetAtPath<Material>(FormalMaterialPath) == null)
            return;

        applyingAfterSave = true;
        try
        {
            ValidateContractConfigOnly();
            ApplyMaterialState();
            ValidatePreparedState();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Benchmark facade save failed macro anti-repetition QA. The formal painted-RC material may not proceed to reflection/capture with a short exact macro repeat.", ex);
        }
        finally
        {
            applyingAfterSave = false;
        }
    }

    private static void OnCameraPreCull(Camera camera)
    {
        if (camera == null || camera.targetTexture == null)
            return;
        string targetName = camera.targetTexture.name ?? string.Empty;
        if (!targetName.StartsWith("QA4K_", StringComparison.Ordinal) &&
            !targetName.StartsWith("QATemporal_", StringComparison.Ordinal))
            return;
        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
            return;

        // Read-only formal-evidence guard. Do not repair a changed material during capture.
        ValidateContractConfigOnly();
        RequireFormalApertureRoot();
        ValidatePreparedState();
    }

    private static void ApplyMaterialState()
    {
        Texture2D antiRepeat = EnsureTexture();
        Material material = AssetDatabase.LoadAssetAtPath<Material>(FormalMaterialPath);
        if (material == null)
            throw new InvalidOperationException($"Formal facade physical-UV material missing: {FormalMaterialPath}");
        if (material.shader == null || material.shader.name != "Standard")
            throw new InvalidOperationException(
                $"Formal facade anti-repeat layer requires built-in Standard shader, got {material.shader?.name ?? "<null>"}.");
        if (!material.HasProperty("_DetailAlbedoMap"))
            throw new InvalidOperationException("Standard facade material exposes no _DetailAlbedoMap property.");

        material.SetTexture("_DetailAlbedoMap", antiRepeat);
        material.SetTextureScale("_DetailAlbedoMap", ExpectedPhysicalUvScale);
        material.SetTextureOffset("_DetailAlbedoMap", Vector2.zero);
        material.EnableKeyword("_DETAIL_MULX2");
        if (material.HasProperty("_UVSec"))
            material.SetFloat("_UVSec", 0f); // Formal aperture UV0 is the physically scaled Danchi-local field.
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
    }

    private static Texture2D EnsureTexture()
    {
        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (existing != null)
            return existing;

        Directory.CreateDirectory(AssetRoot);
        var pixels = new Color32[TextureSize * TextureSize];
        const float tau = Mathf.PI * 2f;
        for (int y = 0; y < TextureSize; y++)
        {
            float v = y / (float)TextureSize;
            for (int x = 0; x < TextureSize; x++)
            {
                float u = x / (float)TextureSize;
                // Integer frequencies keep the 7.9 m map tileable. Mixed directions avoid a single
                // stripe orientation. This is coating/application/chalking variation only: no light,
                // ledge, runoff or horizon direction is encoded.
                float n =
                    Mathf.Sin(tau * (u + 2f * v) + 0.73f) * 0.31f +
                    Mathf.Cos(tau * (2f * u - 3f * v) + 1.91f) * 0.24f +
                    Mathf.Sin(tau * (4f * u + v) + 2.47f) * 0.19f +
                    Mathf.Cos(tau * (3f * u + 5f * v) + 0.28f) * 0.15f +
                    Mathf.Sin(tau * (7f * u - 4f * v) + 1.17f) * 0.11f;
                float value = Mathf.Clamp(NeutralDetailSrgb + n * ModulationAmplitudeSrgb, 0.490f, 0.516f);
                byte b = (byte)Mathf.RoundToInt(value * 255f);
                pixels[y * TextureSize + x] = new Color32(b, b, b, 255);
            }
        }

        var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false, false)
        {
            name = "FacadeRC_AntiRepeatAlbedo"
        };
        try
        {
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            File.WriteAllBytes(TexturePath, texture.EncodeToPNG());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }

        AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException($"TextureImporter unavailable for {TexturePath}");
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Trilinear;
        importer.mipmapEnabled = true;
        importer.anisoLevel = 8;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.alphaSource = TextureImporterAlphaSource.None;
        importer.SaveAndReimport();

        Texture2D generated = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (generated == null)
            throw new InvalidOperationException($"Failed to import generated facade anti-repeat texture: {TexturePath}");
        return generated;
    }

    private static void ValidatePreparedState()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(FormalMaterialPath);
        if (material == null)
            throw new InvalidOperationException($"Formal facade physical-UV material missing: {FormalMaterialPath}");
        if (material.shader == null || material.shader.name != "Standard")
            throw new InvalidOperationException("Formal facade anti-repeat material is no longer Standard shader.");
        if (!material.HasProperty("_DetailAlbedoMap"))
            throw new InvalidOperationException("Formal facade material lost _DetailAlbedoMap.");

        Texture2D detail = material.GetTexture("_DetailAlbedoMap") as Texture2D;
        if (detail == null)
            throw new InvalidOperationException("Formal facade material has no detail-albedo anti-repeat texture.");
        string detailPath = AssetDatabase.GetAssetPath(detail);
        if (!string.Equals(detailPath, TexturePath, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Formal facade detail-albedo path changed: {detailPath}; expected {TexturePath}.");

        Texture main = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
        if (main == detail)
            throw new InvalidOperationException("Facade anti-repeat detail map may not reuse the primary periodic albedo texture.");

        Vector2 scale = material.GetTextureScale("_DetailAlbedoMap");
        if ((scale - ExpectedPhysicalUvScale).sqrMagnitude > 0.000001f)
            throw new InvalidOperationException(
                $"Formal facade anti-repeat scale drifted: {scale}; expected {ExpectedPhysicalUvScale} for {AntiRepeatMacroRepeatMeters:F1} m physical period.");
        Vector2 offset = material.GetTextureOffset("_DetailAlbedoMap");
        if (offset.sqrMagnitude > 0.000001f)
            throw new InvalidOperationException($"Formal facade anti-repeat offset must remain zero, got {offset}.");
        if (!material.IsKeywordEnabled("_DETAIL_MULX2"))
            throw new InvalidOperationException("Formal facade material lost Standard detail-map keyword _DETAIL_MULX2.");
        if (material.HasProperty("_UVSec") && Mathf.Abs(material.GetFloat("_UVSec")) > 0.001f)
            throw new InvalidOperationException("Formal facade detail maps must sample the physically scaled UV0 field, not UV1.");
        if (material.HasProperty("_Metallic") && material.GetFloat("_Metallic") > 0.001f)
            throw new InvalidOperationException("Facade painted RC became metallic while applying anti-repeat detail.");
        if (material.IsKeywordEnabled("_EMISSION") ||
            (material.HasProperty("_EmissionColor") && material.GetColor("_EmissionColor").maxColorComponent > 0.001f))
            throw new InvalidOperationException("Facade anti-repeat material may not become emissive.");

        if (detail.width != TextureSize || detail.height != TextureSize)
            throw new InvalidOperationException(
                $"Facade anti-repeat texture resolution changed: {detail.width}x{detail.height}; expected {TextureSize}x{TextureSize}.");
        if (detail.mipmapCount <= 1)
            throw new InvalidOperationException("Facade anti-repeat texture must carry mipmaps for stable oblique/temporal sampling.");
        TextureImporter importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException("Facade anti-repeat TextureImporter is missing.");
        if (!importer.sRGBTexture || !importer.mipmapEnabled || importer.wrapMode != TextureWrapMode.Repeat ||
            importer.filterMode != FilterMode.Trilinear || importer.anisoLevel < 8)
            throw new InvalidOperationException(
                "Facade anti-repeat texture import policy drifted; require sRGB, mipmaps, Repeat, Trilinear and anisotropy >= 8.");

        float combined = ComputeCombinedRepeatMeters();
        if (combined < MinimumCombinedRepeatMeters || Mathf.Abs(combined - ExpectedCombinedRepeatMeters) > 0.001f)
            throw new InvalidOperationException(
                $"Facade macro anti-repeat arithmetic invalid: combined exact repeat={combined:F3} m.");
    }

    private static void RequireFormalApertureRoot()
    {
        if (FindSceneObject(ApertureRootName) == null)
            throw new InvalidOperationException(
                $"Formal facade aperture root {ApertureRootName} is missing; anti-repeat material evidence is not scoreable.");
    }

    private static float ComputeCombinedRepeatMeters()
    {
        int primaryMm = Mathf.RoundToInt(PrimaryMacroRepeatMeters * 1000f);
        int antiMm = Mathf.RoundToInt(AntiRepeatMacroRepeatMeters * 1000f);
        int gcd = GreatestCommonDivisor(primaryMm, antiMm);
        long lcmMm = (long)primaryMm / gcd * antiMm;
        return lcmMm / 1000f;
    }

    private static int GreatestCommonDivisor(int a, int b)
    {
        a = Mathf.Abs(a);
        b = Mathf.Abs(b);
        while (b != 0)
        {
            int t = a % b;
            a = b;
            b = t;
        }
        return Mathf.Max(1, a);
    }

    private static void EnsureSceneOpen()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x != null && x.scene.IsValid() && x.scene.path == ScenePath && x.name == name);
    }
}
