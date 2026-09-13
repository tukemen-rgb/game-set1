using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Enforces the material-side physical constraints of generated dry midsummer foliage.
///
/// This pass does not claim visual quality. It constrains the custom leaf shader/materials to a
/// measured-order dielectric F0, explicit normal amplitude, broad roughness, modest transmission and
/// two-sided response so the first native-4K render is not starting from an arbitrary game-art
/// specular model. Actual highlight shape, translucency balance and shimmer still require sealed
/// Unity render evidence.
/// </summary>
public static class QualityBlockFoliagePhysicalityQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/foliage_optical_physicality_contract.json";
    private const string ShaderName = "NewTown/FoliageTransmission";
    private const string DarkMaterialPath = "Assets/Art/GeneratedPBR/PBR_LeafDark_Transmission.mat";
    private const string MidMaterialPath = "Assets/Art/GeneratedPBR/PBR_LeafMid_Transmission.mat";

    private const float DielectricF0 = 0.03f;
    private const float NormalScale = 0.72f;
    private const float DarkSmoothnessScale = 0.24f;
    private const float MidSmoothnessScale = 0.23f;
    private const float DiffuseWrap = 0.27f;
    private const float DarkTransmission = 0.33f;
    private const float MidTransmission = 0.35f;

    private static readonly int DielectricF0Id = Shader.PropertyToID("_DielectricF0");
    private static readonly int NormalScaleId = Shader.PropertyToID("_NormalScale");
    private static readonly int SmoothnessScaleId = Shader.PropertyToID("_SmoothnessScale");
    private static readonly int WrapId = Shader.PropertyToID("_Wrap");
    private static readonly int TransmissionStrengthId = Shader.PropertyToID("_TransmissionStrength");

    [MenuItem("NewTown/Materials/Apply Foliage Dielectric BRDF Physicality")]
    public static void ApplyAndValidateOpenScene()
    {
        RequireQualityScene();
        ValidateContractConfigOnly();

        Shader shader = Shader.Find(ShaderName);
        if (shader == null || !shader.isSupported)
            throw new InvalidOperationException($"Required foliage shader is missing, unsupported or failed import: {ShaderName}");

        Material dark = RequireMaterial(DarkMaterialPath, shader);
        Material mid = RequireMaterial(MidMaterialPath, shader);
        ConfigureMaterial(dark, DarkSmoothnessScale, DarkTransmission);
        ConfigureMaterial(mid, MidSmoothnessScale, MidTransmission);

        ValidateOpenScene();
        Debug.Log(
            "Foliage material physicality applied: dielectric F0=0.03, explicit normal scale, broad dry-leaf roughness, " +
            "two-sided GGX/Fresnel shader and bounded transmission. Native 4K visual verification remains pending.");
    }

    [MenuItem("NewTown/QA/Validate Foliage Dielectric BRDF Physicality")]
    public static void ValidateOpenScene()
    {
        RequireQualityScene();
        ValidateContractConfigOnly();

        Shader shader = Shader.Find(ShaderName);
        if (shader == null || !shader.isSupported)
            throw new InvalidOperationException($"Required foliage shader is missing, unsupported or failed import: {ShaderName}");

        var errors = new List<string>();
        Material dark = LoadMaterial(DarkMaterialPath);
        Material mid = LoadMaterial(MidMaterialPath);
        ValidateMaterial(dark, "dark", DarkSmoothnessScale, DarkTransmission, shader, errors);
        ValidateMaterial(mid, "mid", MidSmoothnessScale, MidTransmission, shader, errors);

        QualityBlockArtSlot[] generatedTreeSlots = Resources.FindObjectsOfTypeAll<QualityBlockArtSlot>()
            .Where(x => x.gameObject.scene.IsValid() &&
                        x.SlotId.StartsWith("vegetation.tree.", StringComparison.Ordinal) &&
                        !x.IsUsingAuthoredArt)
            .ToArray();

        MeshRenderer[] leafRenderers = Resources.FindObjectsOfTypeAll<MeshRenderer>()
            .Where(r => r.gameObject.scene.IsValid() &&
                        r.enabled && r.gameObject.activeInHierarchy &&
                        r.gameObject.name.Contains("LeafCluster_"))
            .ToArray();

        if (generatedTreeSlots.Length > 0 && leafRenderers.Length < generatedTreeSlots.Length * 24)
            errors.Add(
                $"Generated foliage renderer coverage unexpectedly low: trees={generatedTreeSlots.Length}, leafRenderers={leafRenderers.Length}. " +
                "Expected at least 24 explicit source clusters per generated tree before LOD proxies.");

        foreach (MeshRenderer renderer in leafRenderers)
        {
            Material material = renderer.sharedMaterial;
            if (material == null || material.shader != shader)
            {
                errors.Add($"Leaf renderer {renderer.gameObject.name} is not bound to {ShaderName}.");
                continue;
            }

            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            float transmission = block.GetFloat(TransmissionStrengthId);
            if (transmission < 0.20f || transmission > 0.50f)
                errors.Add(
                    $"Leaf renderer {renderer.gameObject.name} has out-of-contract transmitted-light strength {transmission:F3}; expected 0.20-0.50.");
        }

        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Foliage dielectric BRDF physicality QA FAILED:\n - " + string.Join("\n - ", errors));

        Debug.Log(
            $"Foliage dielectric BRDF physicality source-QA passed: generatedTreeSlots={generatedTreeSlots.Length}, " +
            $"activeLeafRenderers={leafRenderers.Length}, F0={DielectricF0:F3}, normalScale={NormalScale:F2}. " +
            "This does not award Visual Fidelity points; native 4K still/temporal evidence is still required.");
    }

    [MenuItem("NewTown/QA/Validate Foliage Optical Physicality Contract")]
    public static void ValidateContractConfigOnly()
    {
        if (!File.Exists(ContractPath))
            throw new InvalidOperationException($"Missing foliage optical physicality contract: {ContractPath}");

        string json = File.ReadAllText(ContractPath);
        string[] requiredTokens =
        {
            "\"contractVersion\": \"foliage-optical-physicality-v1.0.0\"",
            "\"selectedDielectricF0\": 0.03",
            "\"selectedNormalScale\": 0.72",
            "\"normalIncidenceF0Formula\": \"((n - 1) / (n + 1))^2\"",
            "\"Schlick Fresnel angular response\"",
            "\"GGX direct specular with Schlick-Smith masking\"",
            "\"requireNoEmission\": true",
            "\"visualFidelityPointsAwarded\": 0",
            "\"runtimeRenderVerification\": \"PENDING_UNITY_RUNTIME\""
        };

        foreach (string token in requiredTokens)
            if (json.IndexOf(token, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException($"Foliage optical physicality contract missing token: {token}");
    }

    private static Material RequireMaterial(string path, Shader shader)
    {
        Material material = LoadMaterial(path);
        if (material == null)
            throw new InvalidOperationException(
                $"Generated foliage transmission material is missing: {path}. Run the foliage optics build before physicality QA.");
        if (material.shader != shader)
            material.shader = shader;
        return material;
    }

    private static Material LoadMaterial(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Material>(path);
    }

    private static void ConfigureMaterial(Material material, float smoothnessScale, float baseTransmission)
    {
        bool changed = false;
        changed |= SetFloatIfNeeded(material, DielectricF0Id, DielectricF0);
        changed |= SetFloatIfNeeded(material, NormalScaleId, NormalScale);
        changed |= SetFloatIfNeeded(material, SmoothnessScaleId, smoothnessScale);
        changed |= SetFloatIfNeeded(material, WrapId, DiffuseWrap);
        changed |= SetFloatIfNeeded(material, TransmissionStrengthId, baseTransmission);

        if (!material.enableInstancing)
        {
            material.enableInstancing = true;
            changed = true;
        }
        if (!material.doubleSidedGI)
        {
            material.doubleSidedGI = true;
            changed = true;
        }

        if (changed)
            EditorUtility.SetDirty(material);
    }

    private static bool SetFloatIfNeeded(Material material, int propertyId, float value)
    {
        if (!material.HasProperty(propertyId))
            throw new InvalidOperationException(
                $"Material {material.name} is missing required shader property id {propertyId}; shader/property contract drifted.");
        if (Mathf.Abs(material.GetFloat(propertyId) - value) <= 0.0001f)
            return false;
        material.SetFloat(propertyId, value);
        return true;
    }

    private static void ValidateMaterial(Material material, string label, float expectedSmoothnessScale,
        float expectedTransmission, Shader shader, List<string> errors)
    {
        if (material == null)
        {
            errors.Add($"Missing {label} foliage material.");
            return;
        }
        if (material.shader != shader)
            errors.Add($"{material.name} uses {material.shader?.name ?? "<null>"} instead of {ShaderName}.");

        ValidateFloat(material, DielectricF0Id, 0.028f, 0.032f, "dielectric F0", errors);
        ValidateFloat(material, NormalScaleId, 0.65f, 0.80f, "normal scale", errors);
        ValidateFloat(material, SmoothnessScaleId, 0.20f, 0.26f, "smoothness scale", errors);
        ValidateFloat(material, WrapId, 0.24f, 0.30f, "diffuse wrap", errors);
        ValidateFloat(material, TransmissionStrengthId, 0.20f, 0.50f, "base transmission", errors);

        if (material.HasProperty(SmoothnessScaleId) &&
            Mathf.Abs(material.GetFloat(SmoothnessScaleId) - expectedSmoothnessScale) > 0.001f)
            errors.Add(
                $"{material.name} smoothness scale drifted: {material.GetFloat(SmoothnessScaleId):F3} != expected {expectedSmoothnessScale:F3}.");
        if (material.HasProperty(TransmissionStrengthId) &&
            Mathf.Abs(material.GetFloat(TransmissionStrengthId) - expectedTransmission) > 0.001f)
            errors.Add(
                $"{material.name} base transmission drifted: {material.GetFloat(TransmissionStrengthId):F3} != expected {expectedTransmission:F3}.");
        if (!material.enableInstancing)
            errors.Add($"{material.name} has GPU instancing disabled.");
        if (!material.doubleSidedGI)
            errors.Add($"{material.name} has doubleSidedGI disabled despite two-sided leaf geometry.");
        if (material.HasProperty("_EmissionColor") && material.GetColor("_EmissionColor").maxColorComponent > 0.0001f)
            errors.Add($"{material.name} has non-zero emission; dry foliage may not self-illuminate.");
    }

    private static void ValidateFloat(Material material, int propertyId, float minimum, float maximum,
        string label, List<string> errors)
    {
        if (!material.HasProperty(propertyId))
        {
            errors.Add($"{material.name} is missing required {label} property.");
            return;
        }

        float value = material.GetFloat(propertyId);
        if (float.IsNaN(value) || float.IsInfinity(value) || value < minimum || value > maximum)
            errors.Add($"{material.name} {label}={value:F4} is outside {minimum:F3}-{maximum:F3}.");
    }

    private static void RequireQualityScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            throw new InvalidOperationException(
                $"Foliage physicality QA requires the persisted benchmark scene to be open: {ScenePath}");
    }
}
