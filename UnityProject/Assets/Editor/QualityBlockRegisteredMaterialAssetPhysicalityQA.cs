using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Fail-closed bridge between the authored construction/material lookdev registry and the actual Unity
/// Material assets used by active benchmark renderers. The registry can describe physically plausible
/// ranges while a generated/bound Material silently drifts outside them; this QA prevents such a state
/// from entering formal native-4K evidence.
///
/// This is implementation/evidence preflight only. It awards zero Visual Fidelity points and cannot clear
/// impossible-material, baked-highlight, or metadata critical defects without sealed rendered pixels.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockRegisteredMaterialAssetPhysicalityQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/registered_material_asset_physicality_contract.json";
    private const string RegistryPath = "Assets/QA/material_construction_lookdev.json";
    private const string ReportPath = "Assets/QA/registered_material_asset_physicality_report.json";
    private const int Width = 3840;
    private const int Height = 2160;
    private const float RangeTolerance = 0.02f;
    private const float OpaqueAlphaMinimum = 0.98f;
    private const float MaximumEmission = 0.01f;

    private static readonly string[] FormalTargetPrefixes = { "QA4K_", "QAPreparedTemporal_" };
    private static readonly string[] RequiredMaterialIds =
    {
        "painted_rc_exterior",
        "anodized_aluminum",
        "galvanized_steel",
        "epdm_rubber",
        "aged_abs_ac",
        "pipe_insulation_cover",
        "pvc_drain_hose"
    };

    private static readonly string[] CanonicalCriticalRisks =
    {
        "impossible_material_physics",
        "missing_construction_material_metadata",
        "baked_or_painted_highlights"
    };

    private static bool validatingFormalFrame;

    static QualityBlockRegisteredMaterialAssetPhysicalityQA()
    {
        Camera.onPreCull -= OnCameraPreCull;
        Camera.onPreCull += OnCameraPreCull;
    }

    [MenuItem("NewTown/QA/Validate Registered Material Asset Physicality Contract")]
    public static void ValidateContractConfigOnly()
    {
        MaterialAssetContract contract = LoadJson<MaterialAssetContract>(ContractPath);
        if (contract == null || !string.Equals(contract.schemaVersion, "1.0", StringComparison.Ordinal))
            throw new InvalidOperationException("Registered material asset physicality contract is null/unparseable or not schema 1.0.");
        if (!string.Equals(contract.scenePath, ScenePath, StringComparison.Ordinal) ||
            !string.Equals(contract.registryPath, RegistryPath, StringComparison.Ordinal) ||
            !string.Equals(contract.reportPath, ReportPath, StringComparison.Ordinal))
            throw new InvalidOperationException("Registered material asset physicality contract path identity drifted.");

        RequireExactSet(contract.formalTargetPrefixes, FormalTargetPrefixes, "formalTargetPrefixes");
        RequireExactSet(contract.requiredMaterialIds, RequiredMaterialIds, "requiredMaterialIds");
        RequireExactSet(contract.criticalDefectRisksReduced, CanonicalCriticalRisks, "criticalDefectRisksReduced");

        MaterialAssetRequirements r = contract.requirements;
        if (r == null ||
            !r.validateEveryRegistryMaterial ||
            !r.requireUniqueMaterialIdsAndAssetPaths ||
            !r.requireMaterialAssetBeforeFormalRender ||
            !r.requireInspectableMetallicScalar ||
            !r.requireInspectableRoughnessOrSmoothnessScalar ||
            !r.requireMetallicInsideDeclaredRange ||
            !r.requireRoughnessInsideDeclaredRange ||
            !r.requireFiniteOpaqueBaseColor ||
            !r.activeEmissionForbidden ||
            !r.transparentRegistryMaterialsForbidden ||
            !r.requireAtLeastOneActiveBenchmarkRendererBindingPerRegistryMaterial ||
            !r.validateOnEveryFormalPreCull ||
            !r.actualRenderRequiredForVisualPoints ||
            !r.humanGrazingPixelReviewRequired)
            throw new InvalidOperationException("Registered material asset physicality contract requirements were weakened or are incomplete.");

        if (contract.runtimeRenderVerified || contract.visualFidelityPointsAwarded != 0)
            throw new InvalidOperationException("Registered material asset physicality preflight cannot claim runtime render verification or award Visual Fidelity points.");
    }

    [MenuItem("NewTown/QA/Validate Registered Material Asset Physicality")]
    public static void ValidateRegisteredMaterialAssets()
    {
        ValidateContractConfigOnly();
        ValidationReport report = ValidateRegisteredMaterialAssetsInternal(writeReport: true);
        Debug.Log(
            $"Registered material asset physicality valid: {report.materials.Length} registry materials are present, " +
            "inside declared metallic/roughness ranges, opaque/non-emissive, and bound by active benchmark renderers. " +
            "This is source/runtime preflight only and awards 0 Visual Fidelity points.");
    }

    /// <summary>
    /// Used by other fail-closed packet/integrity entry points without mutating report files.
    /// </summary>
    public static void ValidateForFormalEvidence()
    {
        ValidateContractConfigOnly();
        ValidateRegisteredMaterialAssetsInternal(writeReport: false);
    }

    private static void OnCameraPreCull(Camera camera)
    {
        if (!IsFormalEvidenceCamera(camera))
            return;
        if (validatingFormalFrame)
            throw new InvalidOperationException("Registered material asset physicality QA re-entered during a formal render pre-cull.");

        validatingFormalFrame = true;
        try
        {
            ValidateForFormalEvidence();
        }
        finally
        {
            validatingFormalFrame = false;
        }
    }

    private static ValidationReport ValidateRegisteredMaterialAssetsInternal(bool writeReport)
    {
        MaterialConstructionRegistry registry = LoadJson<MaterialConstructionRegistry>(RegistryPath);
        if (registry == null || registry.materials == null || registry.materials.Length == 0)
            throw new InvalidOperationException("Material construction registry is null or contains no material specifications.");

        var errors = new List<string>();
        MaterialSpec[] specs = registry.materials.Where(x => x != null).ToArray();
        if (specs.Length != registry.materials.Length)
            errors.Add("Material construction registry contains a null material entry.");

        string[] ids = specs.Select(x => x.id).ToArray();
        string[] paths = specs.Select(x => x.assetPath).ToArray();
        if (ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
            errors.Add("Registry material IDs must be non-empty and unique.");
        if (paths.Any(string.IsNullOrWhiteSpace) || paths.Distinct(StringComparer.Ordinal).Count() != paths.Length)
            errors.Add("Registry material asset paths must be non-empty and unique.");

        var idSet = new HashSet<string>(ids.Where(x => !string.IsNullOrWhiteSpace(x)), StringComparer.Ordinal);
        foreach (string requiredId in RequiredMaterialIds)
            if (!idSet.Contains(requiredId))
                errors.Add("Required registry material ID is missing: " + requiredId);

        Renderer[] activeRenderers = Resources.FindObjectsOfTypeAll<Renderer>()
            .Where(x => x != null && x.gameObject.scene.IsValid() && x.gameObject.scene.path == ScenePath)
            .Where(x => x.enabled && x.gameObject.activeInHierarchy)
            .ToArray();
        if (activeRenderers.Length == 0)
            errors.Add("No active benchmark renderers are available for registry-to-material binding validation.");

        var observations = new List<MaterialObservation>();
        foreach (MaterialSpec spec in specs.OrderBy(x => x.id, StringComparer.Ordinal))
            ValidateMaterialSpec(spec, activeRenderers, observations, errors);

        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Registered material asset physicality QA FAILED:\n - " + string.Join("\n - ", errors));

        var report = new ValidationReport
        {
            generatedUtc = DateTime.UtcNow.ToString("O"),
            registryVersion = registry.registryVersion,
            scenePath = ScenePath,
            materials = observations.ToArray(),
            runtimeRenderVerified = false,
            visualFidelityPointsAwarded = 0,
            note =
                "Actual Unity Material assets were checked against the authored registry and active renderer bindings. " +
                "Texture-modulated BRDF response and final angular appearance remain unverified until sealed native-4K grazing pixels are reviewed."
        };

        if (writeReport)
        {
            string absolute = AbsolutePath(ReportPath);
            string directory = Path.GetDirectoryName(absolute);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(absolute, JsonUtility.ToJson(report, true));
            AssetDatabase.Refresh();
        }

        return report;
    }

    private static void ValidateMaterialSpec(MaterialSpec spec, Renderer[] activeRenderers,
        List<MaterialObservation> observations, List<string> errors)
    {
        string label = "material[" + (spec?.id ?? "<null>") + "]";
        if (spec == null) return;

        ValidateDeclaredRange(spec.metallicMin, spec.metallicMax, label + " metallic", errors);
        ValidateDeclaredRange(spec.roughnessMin, spec.roughnessMax, label + " roughness", errors);
        if (string.IsNullOrWhiteSpace(spec.assetPath))
        {
            errors.Add(label + " has no assetPath.");
            return;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(spec.assetPath);
        if (material == null)
        {
            errors.Add(label + " Material asset is missing before formal render: " + spec.assetPath);
            return;
        }
        if (material.shader == null || string.Equals(material.shader.name, "Hidden/InternalErrorShader", StringComparison.Ordinal))
        {
            errors.Add(label + " has a missing/error shader: " + spec.assetPath);
            return;
        }

        if (!TryReadMetallic(material, out float metallic, out bool metallicMapAssigned))
        {
            errors.Add(label + " has no inspectable metallic scalar property: " + spec.assetPath);
            return;
        }
        if (!Finite(metallic) || metallic < spec.metallicMin - RangeTolerance || metallic > spec.metallicMax + RangeTolerance)
            errors.Add($"{label} actual metallic={metallic:0.###} is outside declared [{spec.metallicMin:0.###}, {spec.metallicMax:0.###}] (tol {RangeTolerance:0.##}).");

        if (!TryReadRoughness(material, metallicMapAssigned, out float roughness, out string roughnessSource))
        {
            errors.Add(label + " has no inspectable roughness/smoothness scalar property: " + spec.assetPath);
            return;
        }
        if (!Finite(roughness) || roughness < spec.roughnessMin - RangeTolerance || roughness > spec.roughnessMax + RangeTolerance)
            errors.Add($"{label} actual roughness={roughness:0.###} ({roughnessSource}) is outside declared [{spec.roughnessMin:0.###}, {spec.roughnessMax:0.###}] (tol {RangeTolerance:0.##}).");

        if (!material.HasProperty("_Color"))
        {
            errors.Add(label + " has no inspectable base-color property _Color: " + spec.assetPath);
        }
        else
        {
            Color c = material.GetColor("_Color");
            if (!Finite(c.r) || !Finite(c.g) || !Finite(c.b) || !Finite(c.a) ||
                c.r < 0f || c.r > 1f || c.g < 0f || c.g > 1f || c.b < 0f || c.b > 1f ||
                c.a < OpaqueAlphaMinimum || c.a > 1.0001f)
                errors.Add($"{label} has invalid/non-opaque base color {c}: {spec.assetPath}");
        }

        if (material.renderQueue > (int)UnityEngine.Rendering.RenderQueue.GeometryLast)
            errors.Add($"{label} is unexpectedly transparent/late-queued (renderQueue={material.renderQueue}): {spec.assetPath}");
        if (material.HasProperty("_Mode") && material.GetFloat("_Mode") > 0.1f)
            errors.Add($"{label} Standard _Mode={material.GetFloat("_Mode"):0.###} is not opaque: {spec.assetPath}");

        float emissionMax = 0f;
        if (material.HasProperty("_EmissionColor"))
        {
            Color e = material.GetColor("_EmissionColor");
            emissionMax = Mathf.Max(e.r, Mathf.Max(e.g, e.b));
            if (!Finite(emissionMax) || emissionMax > MaximumEmission)
                errors.Add($"{label} has active emission {e}; daylight benchmark registry materials may not self-light.");
        }

        int bindingCount = activeRenderers.Count(r =>
            r.sharedMaterials != null && r.sharedMaterials.Any(m => m == material));
        if (bindingCount <= 0)
            errors.Add(label + " is declared in the material registry but is not bound by any active benchmark renderer: " + spec.assetPath);

        observations.Add(new MaterialObservation
        {
            id = spec.id,
            assetPath = spec.assetPath,
            shader = material.shader.name,
            actualMetallic = metallic,
            actualRoughness = roughness,
            roughnessSource = roughnessSource,
            metallicMapAssigned = metallicMapAssigned,
            activeRendererBindings = bindingCount,
            renderQueue = material.renderQueue,
            emissionMax = emissionMax
        });
    }

    private static bool TryReadMetallic(Material material, out float metallic, out bool metallicMapAssigned)
    {
        metallic = 0f;
        metallicMapAssigned = material.HasProperty("_MetallicGlossMap") && material.GetTexture("_MetallicGlossMap") != null;
        if (!material.HasProperty("_Metallic")) return false;
        metallic = material.GetFloat("_Metallic");
        return true;
    }

    private static bool TryReadRoughness(Material material, bool metallicMapAssigned,
        out float roughness, out string source)
    {
        roughness = 0f;
        source = null;

        if (material.HasProperty("_Roughness"))
        {
            roughness = material.GetFloat("_Roughness");
            source = "_Roughness";
            return true;
        }

        if (metallicMapAssigned && material.HasProperty("_GlossMapScale"))
        {
            roughness = 1f - material.GetFloat("_GlossMapScale");
            source = "1-_GlossMapScale (metallic/smoothness map assigned)";
            return true;
        }

        if (material.HasProperty("_Glossiness"))
        {
            roughness = 1f - material.GetFloat("_Glossiness");
            source = "1-_Glossiness";
            return true;
        }

        if (material.HasProperty("_Smoothness"))
        {
            roughness = 1f - material.GetFloat("_Smoothness");
            source = "1-_Smoothness";
            return true;
        }

        if (material.HasProperty("_GlossMapScale"))
        {
            roughness = 1f - material.GetFloat("_GlossMapScale");
            source = "1-_GlossMapScale";
            return true;
        }

        return false;
    }

    private static void ValidateDeclaredRange(float min, float max, string label, List<string> errors)
    {
        if (!Finite(min) || !Finite(max) || min < 0f || max > 1f || min > max)
            errors.Add($"{label} declared range [{min}, {max}] is invalid; expected finite 0..1 with min <= max.");
    }

    private static bool IsFormalEvidenceCamera(Camera camera)
    {
        if (camera == null || camera != Camera.main || !camera.gameObject.scene.IsValid() ||
            camera.gameObject.scene.path != ScenePath)
            return false;

        RenderTexture target = camera.targetTexture;
        if (target == null || target.width != Width || target.height != Height || string.IsNullOrEmpty(target.name))
            return false;
        return FormalTargetPrefixes.Any(prefix => target.name.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static T LoadJson<T>(string assetPath)
    {
        string absolute = AbsolutePath(assetPath);
        if (!File.Exists(absolute))
            throw new FileNotFoundException("Required QA file not found: " + assetPath, absolute);
        T value = JsonUtility.FromJson<T>(File.ReadAllText(absolute));
        if (value == null)
            throw new InvalidOperationException("Could not parse QA JSON: " + assetPath);
        return value;
    }

    private static string AbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrWhiteSpace(projectRoot))
            throw new InvalidOperationException("Could not resolve Unity project root.");
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private static bool Finite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static void RequireExactSet(string[] actual, string[] expected, string label)
    {
        if (actual == null || actual.Any(string.IsNullOrWhiteSpace) ||
            actual.Distinct(StringComparer.Ordinal).Count() != actual.Length ||
            !new HashSet<string>(actual, StringComparer.Ordinal).SetEquals(expected ?? Array.Empty<string>()))
            throw new InvalidOperationException(
                $"{label} must be exactly [{string.Join(", ", expected ?? Array.Empty<string>())}].");
    }

    [Serializable]
    private sealed class MaterialAssetContract
    {
        public string schemaVersion;
        public string scenePath;
        public string registryPath;
        public string reportPath;
        public string[] formalTargetPrefixes;
        public string[] requiredMaterialIds;
        public MaterialAssetRequirements requirements;
        public string[] criticalDefectRisksReduced;
        public int visualFidelityPointsAwarded;
        public bool runtimeRenderVerified;
    }

    [Serializable]
    private sealed class MaterialAssetRequirements
    {
        public bool validateEveryRegistryMaterial;
        public bool requireUniqueMaterialIdsAndAssetPaths;
        public bool requireMaterialAssetBeforeFormalRender;
        public bool requireInspectableMetallicScalar;
        public bool requireInspectableRoughnessOrSmoothnessScalar;
        public bool requireMetallicInsideDeclaredRange;
        public bool requireRoughnessInsideDeclaredRange;
        public bool requireFiniteOpaqueBaseColor;
        public bool activeEmissionForbidden;
        public bool transparentRegistryMaterialsForbidden;
        public bool requireAtLeastOneActiveBenchmarkRendererBindingPerRegistryMaterial;
        public bool validateOnEveryFormalPreCull;
        public bool actualRenderRequiredForVisualPoints;
        public bool humanGrazingPixelReviewRequired;
    }

    [Serializable]
    private sealed class MaterialConstructionRegistry
    {
        public string registryVersion;
        public MaterialSpec[] materials;
    }

    [Serializable]
    private sealed class MaterialSpec
    {
        public string id;
        public string assetPath;
        public float roughnessMin;
        public float roughnessMax;
        public float metallicMin;
        public float metallicMax;
    }

    [Serializable]
    private sealed class ValidationReport
    {
        public string generatedUtc;
        public string registryVersion;
        public string scenePath;
        public MaterialObservation[] materials;
        public bool runtimeRenderVerified;
        public int visualFidelityPointsAwarded;
        public string note;
    }

    [Serializable]
    private sealed class MaterialObservation
    {
        public string id;
        public string assetPath;
        public string shader;
        public float actualMetallic;
        public float actualRoughness;
        public string roughnessSource;
        public bool metallicMapAssigned;
        public int activeRendererBindings;
        public int renderQueue;
        public float emissionMax;
    }
}
