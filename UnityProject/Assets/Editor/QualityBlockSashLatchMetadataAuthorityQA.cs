using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Cross-contract authority guard for the generated apartment sash hardware.
///
/// The fallback scene still contains the original HD_WindowHandle_ renderers because the physical
/// crescent-latch upgrade disables them rather than deleting them. The central construction registry
/// therefore remains useful as provenance for that legacy fallback, but it must never be interpreted
/// as the active material/construction specification once FacadeSashLatchHardware is present.
///
/// This QA binds the legacy registry entry, the physical latch contract, and the canonical aluminum /
/// galvanized material records into one machine-checkable authority chain. It validates metadata only;
/// the formal camera hook also delegates to the physical latch scene QA so disabled legacy handles,
/// purpose-built geometry, LOD0/1/2/3, seating and material assignments are checked before evidence.
/// No Visual Fidelity points are awarded without native 4K rendered evidence.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockSashLatchMetadataAuthorityQA
{
    private const string BenchmarkScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string CentralRegistryPath = "Assets/QA/material_construction_lookdev.json";
    private const string LatchContractPath = "Assets/QA/facade_sash_latch_contract.json";
    private const string AuthorityContractPath = "Assets/QA/sash_latch_metadata_authority_contract.json";

    private const string SlidingWindowAssemblyId = "sliding_window";
    private const string LegacyHandlePrefix = "HD_WindowHandle_";
    private const string LegacyHandleMaterialId = "epdm_rubber";
    private const string PrimaryMaterialId = "anodized_aluminum";
    private const string FastenerMaterialId = "galvanized_steel";
    private const string LatchAssemblyId = "apartment_sliding_sash_crescent_latch";
    private const string LatchRootName = "FacadeSashLatchHardware";
    private const string PrimaryMaterialPath = "Assets/Art/GeneratedDetailMaterials/MAT_AgedAluminum.mat";
    private const string FastenerMaterialPath = "Assets/Art/GeneratedDetailMaterials/MAT_DarkGalvanizedSteel.mat";
    private const int ExpectedWindowCount = 30;
    private const int ExpectedLegacyRendererCount = 60;
    private const int ExpectedLodCount = 4;
    private const float MountingPitchM = 0.050f;

    private static readonly string[] RequiredCriticalDefects =
    {
        "visible_primitive_placeholder",
        "impossible_material_physics",
        "hero_geometry_intersection",
        "severe_aliasing_or_shimmer",
        "visible_lod_pop",
        "missing_construction_material_metadata",
        "unverified_render_claim"
    };

    private static bool validating;

    static QualityBlockSashLatchMetadataAuthorityQA()
    {
        Camera.onPreCull -= OnCameraPreCull;
        Camera.onPreCull += OnCameraPreCull;
    }

    [MenuItem("NewTown/QA/Validate Sash Latch Metadata Authority")]
    public static void ValidateConfigOnly()
    {
        CentralRegistry registry = LoadJson<CentralRegistry>(CentralRegistryPath);
        LatchContract latch = LoadJson<LatchContract>(LatchContractPath);
        AuthorityContract authority = LoadJson<AuthorityContract>(AuthorityContractPath);
        var errors = new List<string>();

        ValidateAuthorityContract(authority, errors);
        ValidateCentralRegistry(registry, latch, authority, errors);
        ValidateLatchContract(latch, authority, errors);

        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Sash-latch metadata authority QA FAILED:\n - " + string.Join("\n - ", errors));

        Debug.Log(
            "Sash-latch metadata authority is coherent: HD_WindowHandle_ / EPDM is explicitly legacy-disabled fallback provenance; " +
            "formal crescent hardware is governed by the physical latch contract and canonical aged aluminum + galvanized fastener material records. " +
            "This validates metadata only and awards zero Visual Fidelity points.");
    }

    [MenuItem("NewTown/QA/Validate Sash Latch Metadata + Formal Scene")]
    public static void ValidateFormalScene()
    {
        if (validating) return;
        validating = true;
        try
        {
            ValidateConfigOnly();
            QualityBlockFacadeSashLatchUpgrade.ValidateOpenScene();
        }
        finally
        {
            validating = false;
        }
    }

    private static void OnCameraPreCull(Camera camera)
    {
        if (camera == null || validating || !IsFormalEvidenceCamera(camera))
            return;

        ValidateFormalScene();
    }

    private static bool IsFormalEvidenceCamera(Camera camera)
    {
        string name = camera.name ?? string.Empty;
        if (name.StartsWith("QA4K_", StringComparison.Ordinal) ||
            name.StartsWith("QATemporal_", StringComparison.Ordinal))
            return true;

        return string.Equals(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path, BenchmarkScenePath, StringComparison.Ordinal) &&
               (name.IndexOf("Benchmark", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Evidence", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static void ValidateAuthorityContract(AuthorityContract authority, List<string> errors)
    {
        if (authority == null)
        {
            errors.Add("Authority contract is null/unparseable.");
            return;
        }

        Require(string.Equals(authority.schemaVersion, "1.0", StringComparison.Ordinal), "authority.schemaVersion must be 1.0.", errors);
        Require(string.Equals(authority.centralRegistryPath, CentralRegistryPath, StringComparison.Ordinal), "centralRegistryPath mismatch.", errors);
        Require(string.Equals(authority.physicalLatchContractPath, LatchContractPath, StringComparison.Ordinal), "physicalLatchContractPath mismatch.", errors);
        Require(string.Equals(authority.centralAssemblyId, SlidingWindowAssemblyId, StringComparison.Ordinal), "centralAssemblyId mismatch.", errors);
        Require(string.Equals(authority.legacyObjectPrefix, LegacyHandlePrefix, StringComparison.Ordinal), "legacyObjectPrefix mismatch.", errors);
        Require(string.Equals(authority.legacyMaterialId, LegacyHandleMaterialId, StringComparison.Ordinal), "legacyMaterialId mismatch.", errors);
        Require(authority.legacyFallbackOnly, "legacyFallbackOnly must remain true.", errors);
        Require(authority.mustRemainDisabledForFormalEvidence, "mustRemainDisabledForFormalEvidence must remain true.", errors);
        Require(authority.expectedLegacyRendererCount == ExpectedLegacyRendererCount,
            $"expectedLegacyRendererCount must be {ExpectedLegacyRendererCount}.", errors);
        Require(string.Equals(authority.supersedingAssemblyId, LatchAssemblyId, StringComparison.Ordinal), "supersedingAssemblyId mismatch.", errors);
        Require(string.Equals(authority.supersedingRootName, LatchRootName, StringComparison.Ordinal), "supersedingRootName mismatch.", errors);
        Require(string.Equals(authority.primaryMaterialId, PrimaryMaterialId, StringComparison.Ordinal), "primaryMaterialId mismatch.", errors);
        Require(string.Equals(authority.fastenerMaterialId, FastenerMaterialId, StringComparison.Ordinal), "fastenerMaterialId mismatch.", errors);
        Require(string.Equals(authority.primaryMaterialAssetPath, PrimaryMaterialPath, StringComparison.Ordinal), "primaryMaterialAssetPath mismatch.", errors);
        Require(string.Equals(authority.fastenerMaterialAssetPath, FastenerMaterialPath, StringComparison.Ordinal), "fastenerMaterialAssetPath mismatch.", errors);
        Require(authority.expectedWindowCount == ExpectedWindowCount, $"expectedWindowCount must be {ExpectedWindowCount}.", errors);
        Require(authority.expectedLodCount == ExpectedLodCount, $"expectedLodCount must be {ExpectedLodCount}.", errors);
        RequireNear(authority.mountingScrewPitchM, MountingPitchM, 0.0001f, "mountingScrewPitchM", errors);
        Require(authority.automaticVisualPoints == 0, "automaticVisualPoints must remain zero.", errors);
        Require(authority.renderVerificationPending, "renderVerificationPending must remain true until real Unity render evidence exists.", errors);
        Require(!string.IsNullOrWhiteSpace(authority.authorityRule), "authorityRule is required.", errors);
        Require(!string.IsNullOrWhiteSpace(authority.geometryVsMaterialRule), "geometryVsMaterialRule is required.", errors);
        Require(!string.IsNullOrWhiteSpace(authority.lookdevBrief), "lookdevBrief is required.", errors);
        RequireExactSet(authority.criticalDefectIds, RequiredCriticalDefects, "criticalDefectIds", errors);
    }

    private static void ValidateCentralRegistry(
        CentralRegistry registry,
        LatchContract latch,
        AuthorityContract authority,
        List<string> errors)
    {
        if (registry == null)
        {
            errors.Add("Central material/construction registry is null/unparseable.");
            return;
        }

        AssemblySpec sliding = (registry.assemblies ?? Array.Empty<AssemblySpec>())
            .FirstOrDefault(x => x != null && string.Equals(x.id, SlidingWindowAssemblyId, StringComparison.Ordinal));
        if (sliding == null)
        {
            errors.Add("Central registry is missing sliding_window assembly.");
            return;
        }

        ComponentSpec legacy = (sliding.components ?? Array.Empty<ComponentSpec>())
            .FirstOrDefault(x => x != null && string.Equals(x.objectPrefix, LegacyHandlePrefix, StringComparison.Ordinal));
        if (legacy == null)
            errors.Add($"Central registry no longer records legacy fallback prefix {LegacyHandlePrefix}; update the authority bridge deliberately rather than silently losing provenance.");
        else
        {
            Require(string.Equals(legacy.materialId, LegacyHandleMaterialId, StringComparison.Ordinal),
                $"Legacy {LegacyHandlePrefix} provenance must remain {LegacyHandleMaterialId}; it is disabled, not reclassified as the physical latch.", errors);
            Require((legacy.attachment ?? string.Empty).IndexOf("fallback", StringComparison.OrdinalIgnoreCase) >= 0,
                "Legacy handle component must remain explicitly described as fallback provenance.", errors);
        }

        MaterialSpec primary = FindMaterial(registry, PrimaryMaterialId);
        MaterialSpec fastener = FindMaterial(registry, FastenerMaterialId);
        ValidatePrimaryMaterial(primary, errors);
        ValidateFastenerMaterial(fastener, errors);

        if (latch?.material != null)
        {
            if (primary != null)
                Require(string.Equals(primary.assetPath, latch.material.primaryAssetPath, StringComparison.Ordinal),
                    "Central anodized-aluminum asset path and physical latch primaryAssetPath disagree.", errors);
            if (fastener != null)
                Require(string.Equals(fastener.assetPath, latch.material.fastenerAssetPath, StringComparison.Ordinal),
                    "Central galvanized-steel asset path and physical latch fastenerAssetPath disagree.", errors);
        }

        if (authority != null)
        {
            Require(authority.legacyFallbackOnly && authority.mustRemainDisabledForFormalEvidence,
                "Authority bridge must explicitly prevent the central EPDM fallback from becoming active formal hardware metadata.", errors);
        }
    }

    private static void ValidateLatchContract(LatchContract latch, AuthorityContract authority, List<string> errors)
    {
        if (latch == null)
        {
            errors.Add("Physical sash-latch contract is null/unparseable.");
            return;
        }

        Require(string.Equals(latch.schemaVersion, "1.0", StringComparison.Ordinal), "Latch schemaVersion must be 1.0.", errors);
        Require(string.Equals(latch.assemblyId, LatchAssemblyId, StringComparison.Ordinal), "Latch assemblyId mismatch.", errors);
        Require(latch.expectedWindowCount == ExpectedWindowCount, $"Latch expectedWindowCount must be {ExpectedWindowCount}.", errors);
        RequireText(latch.manufacture, "latch.manufacture", errors);
        RequireText(latch.dimensionsThickness, "latch.dimensionsThickness", errors);
        RequireText(latch.materialsFinish, "latch.materialsFinish", errors);
        RequireText(latch.mounting, "latch.mounting", errors);
        RequireText(latch.interfacesGapsSeals, "latch.interfacesGapsSeals", errors);
        RequireText(latch.orientationExposure, "latch.orientationExposure", errors);
        RequireText(latch.aging, "latch.aging", errors);
        RequireText(latch.geometryVsMaterial, "latch.geometryVsMaterial", errors);
        RequireText(latch.weatheringCausality, "latch.weatheringCausality", errors);
        RequireText(latch.lookdevBrief, "latch.lookdevBrief", errors);
        RequireText(latch.sourceBasis, "latch.sourceBasis", errors);

        if (latch.geometry == null)
            errors.Add("Latch geometry block is missing.");
        else
        {
            Require(latch.geometry.locksPerTwoPanelWindow == 1, "Latch must use one crescent lock per two-panel window.", errors);
            RequireNear(latch.geometry.mountingScrewPitchM, MountingPitchM, 0.0001f, "latch.geometry.mountingScrewPitchM", errors);
            Require(latch.geometry.basePlateWidthM > 0f && latch.geometry.basePlateHeightM > 0f && latch.geometry.crescentRadiusM > 0f,
                "Latch geometry dimensions must be positive.", errors);
        }

        if (latch.material == null)
            errors.Add("Latch material block is missing.");
        else
        {
            Require(string.Equals(latch.material.primaryAssetPath, PrimaryMaterialPath, StringComparison.Ordinal), "Latch primary material asset mismatch.", errors);
            Require(string.Equals(latch.material.fastenerAssetPath, FastenerMaterialPath, StringComparison.Ordinal), "Latch fastener material asset mismatch.", errors);
            Require(latch.material.baseColorSrgb != null && latch.material.baseColorSrgb.Length == 3,
                "Latch material baseColorSrgb must have three channels.", errors);
            Require(latch.material.primaryMetallicMin >= 0.45f && latch.material.primaryMetallicMax <= 1f &&
                    latch.material.primaryMetallicMin <= latch.material.primaryMetallicMax,
                "Latch primary metallic range is implausible/inverted.", errors);
            Require(latch.material.roughnessMin >= 0.2f && latch.material.roughnessMax <= 0.9f &&
                    latch.material.roughnessMin < latch.material.roughnessMax,
                "Latch roughness range is implausible/inverted.", errors);
            Require(latch.material.normalScale > 0f, "Latch normalScale must be positive.", errors);
            Require(latch.material.microstructureMm > 0f, "Latch microstructureMm must be positive.", errors);
            RequireNear(latch.material.wetness, 0f, 0.0001f, "latch.material.wetness", errors);
            RequireText(latch.material.uvAging, "latch.material.uvAging", errors);
            RequireText(latch.material.angularFresnelResponse, "latch.material.angularFresnelResponse", errors);
        }

        if (latch.qa == null)
            errors.Add("Latch qa block is missing.");
        else
        {
            Require(latch.qa.lodCount == ExpectedLodCount, $"Latch qa.lodCount must be {ExpectedLodCount}.", errors);
            Require(latch.qa.automaticVisualPoints == 0, "Latch automaticVisualPoints must remain zero.", errors);
            Require(latch.qa.renderVerificationPending, "Latch renderVerificationPending must remain true before actual native 4K review.", errors);
            Require(latch.qa.disableLegacyRubberHandles, "Latch contract must disable legacy rubber handles.", errors);
            Require(latch.qa.requireReflectionFingerprintBinding, "Latch contract must require reflection fingerprint binding.", errors);
        }

        if (authority != null && latch.material != null)
        {
            Require(!string.Equals(authority.primaryMaterialId, LegacyHandleMaterialId, StringComparison.Ordinal),
                "Formal latch primaryMaterialId may not resolve to the legacy EPDM fallback.", errors);
        }
    }

    private static MaterialSpec FindMaterial(CentralRegistry registry, string id)
    {
        return (registry?.materials ?? Array.Empty<MaterialSpec>())
            .FirstOrDefault(x => x != null && string.Equals(x.id, id, StringComparison.Ordinal));
    }

    private static void ValidatePrimaryMaterial(MaterialSpec material, List<string> errors)
    {
        if (material == null)
        {
            errors.Add($"Central registry is missing {PrimaryMaterialId} material metadata.");
            return;
        }

        Require(string.Equals(material.assetPath, PrimaryMaterialPath, StringComparison.Ordinal), "Primary material assetPath mismatch.", errors);
        Require(material.baseColorSrgb != null && material.baseColorSrgb.Length == 3, "Primary material baseColorSrgb must have three channels.", errors);
        Require(material.roughnessMin >= 0.25f && material.roughnessMax <= 0.8f && material.roughnessMin < material.roughnessMax,
            "Primary aluminum roughness range is implausible/inverted.", errors);
        Require(material.metallicMin >= 0.65f && material.metallicMax <= 1f && material.metallicMin <= material.metallicMax,
            "Primary aluminum metallic range is below the central conductor requirement.", errors);
        Require(material.specularF0 >= 0.40f && material.specularF0 <= 1f, "Primary aluminum specularF0 is implausible.", errors);
        Require(material.normalAmplitudeMm > 0f && material.microstructureScaleMm > 0f,
            "Primary aluminum normal/microstructure metadata must be positive.", errors);
        Require(material.wetAlbedoMultiplier > 0f && material.wetAlbedoMultiplier <= 1f &&
                material.wetRoughnessMultiplier > 0f && material.wetRoughnessMultiplier < 1f,
            "Primary aluminum wet-response metadata is invalid.", errors);
        Require(material.uvFadeMax >= 0f && material.uvFadeMax <= 0.20f, "Primary aluminum UV aging range is invalid.", errors);
        RequireText(material.frontLightResponse, "anodized_aluminum.frontLightResponse", errors);
        RequireText(material.grazingLightResponse, "anodized_aluminum.grazingLightResponse", errors);
        RequireText(material.shadeResponse, "anodized_aluminum.shadeResponse", errors);
    }

    private static void ValidateFastenerMaterial(MaterialSpec material, List<string> errors)
    {
        if (material == null)
        {
            errors.Add($"Central registry is missing {FastenerMaterialId} material metadata.");
            return;
        }

        Require(string.Equals(material.assetPath, FastenerMaterialPath, StringComparison.Ordinal), "Fastener material assetPath mismatch.", errors);
        Require(material.metallicMin >= 0.65f && material.metallicMax <= 1f, "Fastener metallic range is implausible.", errors);
        Require(material.specularF0 >= 0.40f, "Fastener specularF0 is implausibly weak.", errors);
        RequireText(material.grazingLightResponse, "galvanized_steel.grazingLightResponse", errors);
    }

    private static T LoadJson<T>(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string absolutePath = Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        if (!File.Exists(absolutePath))
            throw new FileNotFoundException("Required sash-latch metadata file is missing: " + assetPath);

        T parsed = JsonUtility.FromJson<T>(File.ReadAllText(absolutePath));
        if (parsed == null)
            throw new InvalidOperationException("Could not parse sash-latch metadata file: " + assetPath);
        return parsed;
    }

    private static void Require(bool condition, string message, List<string> errors)
    {
        if (!condition) errors.Add(message);
    }

    private static void RequireText(string value, string field, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) errors.Add(field + " is required.");
    }

    private static void RequireNear(float actual, float expected, float tolerance, string field, List<string> errors)
    {
        if (Mathf.Abs(actual - expected) > tolerance)
            errors.Add($"{field}={actual:0.####} expected {expected:0.####} +/- {tolerance:0.####}.");
    }

    private static void RequireExactSet(string[] actual, string[] expected, string field, List<string> errors)
    {
        var actualSet = new HashSet<string>(actual ?? Array.Empty<string>(), StringComparer.Ordinal);
        var expectedSet = new HashSet<string>(expected ?? Array.Empty<string>(), StringComparer.Ordinal);
        if (!actualSet.SetEquals(expectedSet) || (actual?.Length ?? 0) != expectedSet.Count)
            errors.Add(field + " must match the canonical set exactly.");
    }

    [Serializable]
    private sealed class CentralRegistry
    {
        public MaterialSpec[] materials;
        public AssemblySpec[] assemblies;
    }

    [Serializable]
    private sealed class MaterialSpec
    {
        public string id;
        public string assetPath;
        public float[] baseColorSrgb;
        public float roughnessMin;
        public float roughnessMax;
        public float metallicMin;
        public float metallicMax;
        public float specularF0;
        public float normalAmplitudeMm;
        public float microstructureScaleMm;
        public float wetAlbedoMultiplier;
        public float wetRoughnessMultiplier;
        public float uvFadeMax;
        public string frontLightResponse;
        public string grazingLightResponse;
        public string shadeResponse;
    }

    [Serializable]
    private sealed class AssemblySpec
    {
        public string id;
        public ComponentSpec[] components;
    }

    [Serializable]
    private sealed class ComponentSpec
    {
        public string partId;
        public string objectPrefix;
        public string materialId;
        public string attachment;
    }

    [Serializable]
    private sealed class LatchContract
    {
        public string schemaVersion;
        public string assemblyId;
        public int expectedWindowCount;
        public string manufacture;
        public string dimensionsThickness;
        public string materialsFinish;
        public string mounting;
        public string interfacesGapsSeals;
        public string orientationExposure;
        public string aging;
        public string geometryVsMaterial;
        public string weatheringCausality;
        public string lookdevBrief;
        public string sourceBasis;
        public LatchGeometry geometry;
        public LatchMaterial material;
        public LatchQa qa;
    }

    [Serializable]
    private sealed class LatchGeometry
    {
        public int locksPerTwoPanelWindow;
        public float mountingScrewPitchM;
        public float basePlateWidthM;
        public float basePlateHeightM;
        public float crescentRadiusM;
    }

    [Serializable]
    private sealed class LatchMaterial
    {
        public string primaryAssetPath;
        public string fastenerAssetPath;
        public float[] baseColorSrgb;
        public float primaryMetallicMin;
        public float primaryMetallicMax;
        public float roughnessMin;
        public float roughnessMax;
        public float normalScale;
        public float microstructureMm;
        public float wetness;
        public string uvAging;
        public string angularFresnelResponse;
    }

    [Serializable]
    private sealed class LatchQa
    {
        public int lodCount;
        public int automaticVisualPoints;
        public bool renderVerificationPending;
        public bool disableLegacyRubberHandles;
        public bool requireReflectionFingerprintBinding;
    }

    [Serializable]
    private sealed class AuthorityContract
    {
        public string schemaVersion;
        public string centralRegistryPath;
        public string physicalLatchContractPath;
        public string centralAssemblyId;
        public string legacyObjectPrefix;
        public string legacyMaterialId;
        public bool legacyFallbackOnly;
        public bool mustRemainDisabledForFormalEvidence;
        public int expectedLegacyRendererCount;
        public string supersedingAssemblyId;
        public string supersedingRootName;
        public string primaryMaterialId;
        public string fastenerMaterialId;
        public string primaryMaterialAssetPath;
        public string fastenerMaterialAssetPath;
        public int expectedWindowCount;
        public int expectedLodCount;
        public float mountingScrewPitchM;
        public string authorityRule;
        public string geometryVsMaterialRule;
        public string lookdevBrief;
        public int automaticVisualPoints;
        public bool renderVerificationPending;
        public string[] criticalDefectIds;
    }
}
