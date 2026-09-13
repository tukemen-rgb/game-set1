using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Strict machine-readable metadata validation for the generated 4 mm facade glazing construction.
/// This complements scene/topology validation by freezing the material/manufacture/installation
/// reasoning required by the visual-fidelity policy and binding its numeric material metadata to the
/// canonical fallback material asset. It never awards visual points.
/// </summary>
public static class QualityBlockFacadeGlassThicknessContractQA
{
    private const string ContractPath = "Assets/QA/facade_glass_edge_construction_contract.json";
    private const string GlassMaterialPath = "Assets/Art/GeneratedFacadeOptics/MAT_WindowClearGlass.mat";

    [MenuItem("NewTown/QA/Validate Facade Glass Full Material Metadata")]
    public static void ValidateContract()
    {
        string absolute = AbsolutePath(ContractPath);
        if (!File.Exists(absolute))
            throw new FileNotFoundException($"Required facade glass construction contract not found: {ContractPath}");

        Contract contract = JsonUtility.FromJson<Contract>(File.ReadAllText(absolute));
        if (contract == null)
            throw new InvalidOperationException("Could not parse facade glass construction contract.");
        if (!string.Equals(contract.schemaVersion, "1.0", StringComparison.Ordinal) ||
            !string.Equals(contract.assemblyId, "facade_clear_float_glazing_thickness", StringComparison.Ordinal))
            throw new InvalidOperationException("Facade glass construction identity/schema drifted.");
        if (contract.expectedPaneCount != 65 || Mathf.Abs(contract.nominalThicknessMm - 4f) > 0.001f || contract.edgeQuadsPerPane != 4)
            throw new InvalidOperationException("Facade glass dimensional/topology metadata drifted from 65 panes / 4 mm / four edge quads.");
        if (!contract.forbidBroadBackSurface || !contract.renderOnlyNoCollider || !contract.requireSameGlassMaterialAsFrontSurface)
            throw new InvalidOperationException("Facade glass hard construction flags were weakened.");

        RequireReasoning(contract.manufactureInstallation, 4, "manufactureInstallation");
        RequireReasoning(contract.mountingInterfaces, 4, "mountingInterfaces");
        RequireReasoning(contract.orientationExposureAging, 4, "orientationExposureAging");
        RequireReasoning(contract.geometryVsMaterialDetail, 4, "geometryVsMaterialDetail");
        RequireReasoning(contract.lodPolicy, 4, "lodPolicy");
        RequireReasoning(contract.requiredEvidence, 5, "requiredEvidence");

        MaterialPhysicality m = contract.materialPhysicality;
        if (m == null)
            throw new InvalidOperationException("Facade glass materialPhysicality metadata is missing.");
        if (!string.Equals(m.materialId, "clear_float_glass", StringComparison.Ordinal) ||
            !string.Equals(m.shader, "Standard", StringComparison.Ordinal))
            throw new InvalidOperationException("Facade glass material identity/shader metadata drifted.");
        RequireArray(m.albedoTintProxyRgba, 4, "albedoTintProxyRgba");
        RequireNear(m.albedoTintProxyRgba[0], 0.72f, 0.001f, "glass tint R");
        RequireNear(m.albedoTintProxyRgba[1], 0.81f, 0.001f, "glass tint G");
        RequireNear(m.albedoTintProxyRgba[2], 0.84f, 0.001f, "glass tint B");
        RequireNear(m.albedoTintProxyRgba[3], 0.18f, 0.001f, "glass tint alpha");
        RequireNear(m.iorReference, 1.52f, 0.001f, "glass IOR reference");
        RequireNear(m.specularF0Reference, 0.043f, 0.001f, "glass F0 reference");
        RequireNear(m.metallic, 0f, 0.0001f, "glass metallic");
        RequireArray(m.dryRoughnessRange, 2, "dryRoughnessRange");
        RequireArray(m.smoothnessProxyRange, 2, "smoothnessProxyRange");
        if (m.dryRoughnessRange[0] < 0.059f || m.dryRoughnessRange[1] > 0.181f ||
            m.dryRoughnessRange[0] > m.dryRoughnessRange[1])
            throw new InvalidOperationException("Facade glass dry roughness range drifted outside 0.06-0.18.");
        if (m.smoothnessProxyRange[0] < 0.819f || m.smoothnessProxyRange[1] > 0.941f ||
            m.smoothnessProxyRange[0] > m.smoothnessProxyRange[1])
            throw new InvalidOperationException("Facade glass smoothness proxy range drifted outside 0.82-0.94.");
        RequireNear(m.frontSurfaceAlphaProxy, 0.18f, 0.001f, "front-surface alpha proxy");
        RequireNear(m.normalScale, 0f, 0.0001f, "glass normal scale");
        RequireNear(m.wetness, 0f, 0.0001f, "dry benchmark wetness");
        RequireText(m.microstructure, "microstructure");
        RequireText(m.uvAging, "uvAging");
        RequireText(m.angularFresnelResponse, "angularFresnelResponse");

        BindMetadataToMaterialAsset(m);

        AutomaticScoring scoring = contract.automaticScoring;
        if (scoring == null || scoring.visualFidelityPoints != 0 || !scoring.implementationOnly || string.IsNullOrWhiteSpace(scoring.note))
            throw new InvalidOperationException("Facade glass contract must explicitly award zero automatic Visual Fidelity points.");

        string[] canonicalDefects =
        {
            "visible_primitive_placeholder",
            "impossible_material_physics",
            "hero_geometry_intersection",
            "severe_aliasing_or_shimmer",
            "missing_construction_material_metadata",
            "unverified_render_claim"
        };
        RequireExactSet(contract.criticalDefectIds, canonicalDefects, "criticalDefectIds");

        RequireEvidenceToken(contract.requiredEvidence, "oblique/construction_depth");
        RequireEvidenceToken(contract.requiredEvidence, "grazing/sash_rail_response");
        RequireEvidenceToken(contract.requiredEvidence, "grazing/material_grazing");
        RequireEvidenceToken(contract.requiredEvidence, "hero/facade_center");
        RequireEvidenceToken(contract.requiredEvidence, "Temporal evidence");

        Debug.Log(
            "Facade glass full construction/material metadata contract passed and is bound to the canonical material asset: dimensions, mounting/interfaces, exposure/aging, albedo/tint, roughness/smoothness, metallic/F0, normal scale, microstructure, wetness, UV aging, Fresnel, LOD and evidence policy are present. " +
            "This is implementation evidence only and awards 0 Visual Fidelity points.");
    }

    private static void BindMetadataToMaterialAsset(MaterialPhysicality m)
    {
        Material glass = AssetDatabase.LoadAssetAtPath<Material>(GlassMaterialPath);
        if (glass == null)
            throw new InvalidOperationException($"Canonical facade glass material is missing: {GlassMaterialPath}");
        if (glass.shader == null || !string.Equals(glass.shader.name, m.shader, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Canonical facade glass shader does not match metadata: actual='{(glass.shader != null ? glass.shader.name : "<null>")}', expected='{m.shader}'.");

        Color c = glass.color;
        RequireNear(c.r, m.albedoTintProxyRgba[0], 0.001f, "material tint R binding");
        RequireNear(c.g, m.albedoTintProxyRgba[1], 0.001f, "material tint G binding");
        RequireNear(c.b, m.albedoTintProxyRgba[2], 0.001f, "material tint B binding");
        RequireNear(c.a, m.albedoTintProxyRgba[3], 0.001f, "material tint alpha binding");
        RequireNear(glass.GetFloat("_Metallic"), m.metallic, 0.0001f, "material metallic binding");

        float smoothness = glass.GetFloat("_Glossiness");
        if (smoothness < m.smoothnessProxyRange[0] - 0.0001f || smoothness > m.smoothnessProxyRange[1] + 0.0001f)
            throw new InvalidOperationException(
                $"Canonical facade glass smoothness {smoothness:F4} is outside metadata range [{m.smoothnessProxyRange[0]:F4}, {m.smoothnessProxyRange[1]:F4}].");
        if (!glass.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON") || glass.IsKeywordEnabled("_ALPHABLEND_ON") || glass.GetInt("_ZWrite") != 0)
            throw new InvalidOperationException("Canonical facade glass must remain premultiplied transparent with ZWrite disabled.");
        if (glass.IsKeywordEnabled("_EMISSION") || glass.GetColor("_EmissionColor").maxColorComponent > 0.001f)
            throw new InvalidOperationException("Canonical facade glass cannot contain emissive/baked light response.");
    }

    private static void RequireReasoning(string[] values, int minimumCount, string label)
    {
        if (values == null || values.Length < minimumCount || values.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException($"Facade glass contract '{label}' is missing/incomplete.");
    }

    private static void RequireEvidenceToken(string[] evidence, string token)
    {
        if (evidence == null || !evidence.Any(x => x != null && x.IndexOf(token, StringComparison.Ordinal) >= 0))
            throw new InvalidOperationException($"Facade glass requiredEvidence is missing canonical observation '{token}'.");
    }

    private static void RequireArray(float[] values, int expectedLength, string label)
    {
        if (values == null || values.Length != expectedLength || values.Any(x => float.IsNaN(x) || float.IsInfinity(x)))
            throw new InvalidOperationException($"Facade glass material metadata '{label}' must contain {expectedLength} finite values.");
    }

    private static void RequireNear(float actual, float expected, float tolerance, string label)
    {
        if (float.IsNaN(actual) || float.IsInfinity(actual) || Mathf.Abs(actual - expected) > tolerance)
            throw new InvalidOperationException($"Facade glass {label} drifted: expected {expected}, got {actual}.");
    }

    private static void RequireText(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Facade glass material metadata '{label}' is missing.");
    }

    private static void RequireExactSet(string[] actual, string[] expected, string label)
    {
        if (actual == null || actual.Any(string.IsNullOrWhiteSpace) ||
            actual.Distinct(StringComparer.Ordinal).Count() != actual.Length ||
            !new HashSet<string>(actual, StringComparer.Ordinal).SetEquals(expected))
            throw new InvalidOperationException($"Facade glass {label} does not match the canonical defect set.");
    }

    private static string AbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    [Serializable]
    private sealed class Contract
    {
        public string schemaVersion;
        public string assemblyId;
        public int expectedPaneCount;
        public float nominalThicknessMm;
        public int edgeQuadsPerPane;
        public bool forbidBroadBackSurface;
        public bool renderOnlyNoCollider;
        public bool requireSameGlassMaterialAsFrontSurface;
        public string[] manufactureInstallation;
        public string[] mountingInterfaces;
        public string[] orientationExposureAging;
        public MaterialPhysicality materialPhysicality;
        public string[] geometryVsMaterialDetail;
        public string[] lodPolicy;
        public string[] requiredEvidence;
        public AutomaticScoring automaticScoring;
        public string[] criticalDefectIds;
    }

    [Serializable]
    private sealed class MaterialPhysicality
    {
        public string materialId;
        public string shader;
        public float[] albedoTintProxyRgba;
        public float iorReference;
        public float specularF0Reference;
        public float metallic;
        public float[] dryRoughnessRange;
        public float[] smoothnessProxyRange;
        public float frontSurfaceAlphaProxy;
        public float normalScale;
        public string microstructure;
        public float wetness;
        public string uvAging;
        public string angularFresnelResponse;
    }

    [Serializable]
    private sealed class AutomaticScoring
    {
        public int visualFidelityPoints;
        public bool implementationOnly;
        public string note;
    }
}
