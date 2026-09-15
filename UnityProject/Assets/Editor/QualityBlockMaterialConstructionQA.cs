using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Structural/material QA for the danchi construction + look-development registry.
/// This validates authored intent only; it never claims that Unity has rendered the intent correctly.
/// Real angular-light response still requires the 4K render gate.
/// </summary>
public static class QualityBlockMaterialConstructionQA
{
    private const string RegistryPath = "Assets/QA/material_construction_lookdev.json";

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

    private static readonly string[] RequiredAssemblyIds =
    {
        "danchi_facade_wall",
        "sliding_window",
        "balcony_railing",
        "outdoor_ac",
        "clothes_drying_hardware",
        "downpipe_clamps"
    };

    private static readonly HashSet<string> DielectricMaterialIds = new(StringComparer.Ordinal)
    {
        "painted_rc_exterior",
        "epdm_rubber",
        "aged_abs_ac",
        "pipe_insulation_cover",
        "pvc_drain_hose"
    };

    private static readonly HashSet<string> ConductiveMaterialIds = new(StringComparer.Ordinal)
    {
        "anodized_aluminum",
        "galvanized_steel"
    };

    [MenuItem("NewTown/QA/Validate Construction + Material LookDev Registry")]
    public static void ValidateRegistry()
    {
        MaterialConstructionRegistry registry = LoadRegistry();
        var errors = new List<string>();

        ValidateHeader(registry, errors);
        ValidateMaterials(registry, errors);
        ValidateAssemblies(registry, errors);
        ValidateRequiredCoverage(registry, errors);

        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Construction/material LookDev QA FAILED:\n - " + string.Join("\n - ", errors));

        Debug.Log(
            $"Construction/material LookDev registry structurally valid: " +
            $"materials={registry.materials.Length}, assemblies={registry.assemblies.Length}. " +
            "Actual shader response and visual quality remain unverified until real Unity 4K renders exist.");
    }

    private static void ValidateHeader(MaterialConstructionRegistry registry, List<string> errors)
    {
        if (registry == null)
        {
            errors.Add("Registry is null/unparseable.");
            return;
        }

        RequireText(registry.registryVersion, "registryVersion", errors);
        RequireText(registry.targetPeriod, "targetPeriod", errors);
        RequireText(registry.targetRegion, "targetRegion", errors);
        RequireText(registry.researchStatus, "researchStatus", errors);
        RequireText(registry.visualTarget, "visualTarget", errors);

        if (registry.materials == null || registry.materials.Length == 0)
            errors.Add("No material specifications exist.");
        if (registry.assemblies == null || registry.assemblies.Length == 0)
            errors.Add("No assembly construction specifications exist.");
    }

    private static void ValidateMaterials(MaterialConstructionRegistry registry, List<string> errors)
    {
        if (registry?.materials == null) return;

        foreach (IGrouping<string, MaterialSpec> duplicate in registry.materials
                     .Where(x => x != null)
                     .GroupBy(x => x.id, StringComparer.Ordinal)
                     .Where(x => string.IsNullOrWhiteSpace(x.Key) || x.Count() > 1))
            errors.Add($"Missing or duplicated material id: '{duplicate.Key}'.");

        foreach (MaterialSpec material in registry.materials.Where(x => x != null))
        {
            string p = $"material[{material.id}]";
            RequireText(material.id, p + ".id", errors);
            RequireText(material.label, p + ".label", errors);
            RequireText(material.assetPath, p + ".assetPath", errors);
            RequireText(material.materialFamily, p + ".materialFamily", errors);
            RequireText(material.finish, p + ".finish", errors);
            RequireText(material.frontLightResponse, p + ".frontLightResponse", errors);
            RequireText(material.grazingLightResponse, p + ".grazingLightResponse", errors);
            RequireText(material.shadeResponse, p + ".shadeResponse", errors);

            if (material.baseColorSrgb == null || material.baseColorSrgb.Length != 3 ||
                material.baseColorSrgb.Any(x => x < 0.015f || x > 0.92f))
                errors.Add($"{p}.baseColorSrgb must contain three plausible non-clipped values in [0.015, 0.92].");

            ValidateUnitRange(material.roughnessMin, p + ".roughnessMin", errors);
            ValidateUnitRange(material.roughnessMax, p + ".roughnessMax", errors);
            if (material.roughnessMin > material.roughnessMax)
                errors.Add($"{p} roughnessMin exceeds roughnessMax.");
            if (material.roughnessMax - material.roughnessMin < 0.05f)
                errors.Add($"{p} roughness range is too uniform for the aged benchmark target.");

            ValidateUnitRange(material.metallicMin, p + ".metallicMin", errors);
            ValidateUnitRange(material.metallicMax, p + ".metallicMax", errors);
            if (material.metallicMin > material.metallicMax)
                errors.Add($"{p} metallicMin exceeds metallicMax.");

            if (DielectricMaterialIds.Contains(material.id))
            {
                if (material.metallicMax > 0.08f)
                    errors.Add($"{p} is dielectric but metallicMax={material.metallicMax:0.###}.");
                if (material.specularF0 < 0.02f || material.specularF0 > 0.08f)
                    errors.Add($"{p} dielectric F0={material.specularF0:0.###} is outside plausible QA range [0.02, 0.08].");
            }

            if (ConductiveMaterialIds.Contains(material.id))
            {
                if (material.metallicMin < 0.65f)
                    errors.Add($"{p} conductor metallicMin={material.metallicMin:0.###} is too low for the defined exposed-metal fallback.");
                if (material.specularF0 < 0.40f)
                    errors.Add($"{p} conductor F0={material.specularF0:0.###} is unexpectedly weak.");
            }

            if (material.normalAmplitudeMm < 0.01f || material.normalAmplitudeMm > 2.5f)
                errors.Add($"{p}.normalAmplitudeMm={material.normalAmplitudeMm:0.###} is outside [0.01, 2.5] mm.");
            if (material.microstructureScaleMm < 0.1f || material.microstructureScaleMm > 12f)
                errors.Add($"{p}.microstructureScaleMm={material.microstructureScaleMm:0.###} is outside [0.1, 12] mm.");

            if (material.wetAlbedoMultiplier < 0.65f || material.wetAlbedoMultiplier > 0.98f)
                errors.Add($"{p}.wetAlbedoMultiplier must modestly darken, expected [0.65, 0.98].");
            if (material.wetRoughnessMultiplier < 0.35f || material.wetRoughnessMultiplier >= 1f)
                errors.Add($"{p}.wetRoughnessMultiplier must reduce roughness without collapsing it, expected [0.35, 1.0).");
            if (material.uvFadeMax < 0f || material.uvFadeMax > 0.20f)
                errors.Add($"{p}.uvFadeMax must remain subtle, expected [0, 0.20].");

            RequireArray(material.agingCauses, 3, p + ".agingCauses", errors);
            RequireArray(material.forbidden, 2, p + ".forbidden", errors);
            RequireArray(material.lookdevViews, 4, p + ".lookdevViews", errors);

            if (ContainsBakedHighlightLanguage(material.finish) ||
                ContainsBakedHighlightLanguage(string.Join(" ", material.agingCauses ?? Array.Empty<string>())))
                errors.Add($"{p} suggests painted/baked highlight information, which is forbidden.");
        }
    }

    private static void ValidateAssemblies(MaterialConstructionRegistry registry, List<string> errors)
    {
        if (registry?.assemblies == null) return;
        var materialIds = new HashSet<string>(
            (registry.materials ?? Array.Empty<MaterialSpec>()).Where(x => x != null).Select(x => x.id),
            StringComparer.Ordinal);

        foreach (IGrouping<string, AssemblySpec> duplicate in registry.assemblies
                     .Where(x => x != null)
                     .GroupBy(x => x.id, StringComparer.Ordinal)
                     .Where(x => string.IsNullOrWhiteSpace(x.Key) || x.Count() > 1))
            errors.Add($"Missing or duplicated assembly id: '{duplicate.Key}'.");

        foreach (AssemblySpec assembly in registry.assemblies.Where(x => x != null))
        {
            string p = $"assembly[{assembly.id}]";
            RequireText(assembly.id, p + ".id", errors);
            RequireText(assembly.label, p + ".label", errors);
            RequireText(assembly.nominalDimensions, p + ".nominalDimensions", errors);
            RequireText(assembly.manufacture, p + ".manufacture", errors);
            RequireText(assembly.mounting, p + ".mounting", errors);
            RequireText(assembly.interfaces, p + ".interfaces", errors);
            RequireText(assembly.orientationExposure, p + ".orientationExposure", errors);
            RequireText(assembly.aging, p + ".aging", errors);
            RequireText(assembly.geometryVsMaterial, p + ".geometryVsMaterial", errors);
            RequireText(assembly.lookdevBrief, p + ".lookdevBrief", errors);

            if (assembly.components == null || assembly.components.Length < 2)
            {
                errors.Add($"{p} must be decomposed into at least two materially/construction-distinct components.");
                continue;
            }

            foreach (ComponentSpec component in assembly.components.Where(x => x != null))
            {
                string cp = $"{p}.component[{component.partId}]";
                RequireText(component.partId, cp + ".partId", errors);
                RequireText(component.objectPrefix, cp + ".objectPrefix", errors);
                RequireText(component.materialId, cp + ".materialId", errors);
                RequireText(component.attachment, cp + ".attachment", errors);
                RequireText(component.detailScale, cp + ".detailScale", errors);
                if (!materialIds.Contains(component.materialId))
                    errors.Add($"{cp} references unknown material '{component.materialId}'.");
            }
        }
    }

    private static void ValidateRequiredCoverage(MaterialConstructionRegistry registry, List<string> errors)
    {
        var materialIds = new HashSet<string>(
            (registry?.materials ?? Array.Empty<MaterialSpec>()).Where(x => x != null).Select(x => x.id),
            StringComparer.Ordinal);
        foreach (string id in RequiredMaterialIds)
            if (!materialIds.Contains(id)) errors.Add($"Required material specification missing: {id}.");

        var assemblyIds = new HashSet<string>(
            (registry?.assemblies ?? Array.Empty<AssemblySpec>()).Where(x => x != null).Select(x => x.id),
            StringComparer.Ordinal);
        foreach (string id in RequiredAssemblyIds)
            if (!assemblyIds.Contains(id)) errors.Add($"Required assembly specification missing: {id}.");

        string[] requiredPrefixes =
        {
            "MainBlock",
            "HD_WindowFrame_",
            "HD_WindowTrack",
            "HD_WindowSillDrip",
            "HD_RailBasePlate_",
            "HD_RailBolt_",
            "HD_AC_Grille",
            "HD_AC_Mount",
            "HD_AC_Refrigerant",
            "HD_AC_DrainHose",
            "HD_DownpipeClamp_"
        };
        var coveredPrefixes = new HashSet<string>(
            (registry?.assemblies ?? Array.Empty<AssemblySpec>())
            .Where(x => x?.components != null)
            .SelectMany(x => x.components)
            .Where(x => x != null)
            .Select(x => x.objectPrefix),
            StringComparer.Ordinal);
        foreach (string prefix in requiredPrefixes)
            if (!coveredPrefixes.Contains(prefix)) errors.Add($"Hero construction/material prefix is not covered by the registry: {prefix}.");
    }

    private static MaterialConstructionRegistry LoadRegistry()
    {
        string absolute = Path.GetFullPath(Path.Combine(
            Directory.GetParent(Application.dataPath).FullName,
            RegistryPath));
        if (!File.Exists(absolute))
            throw new FileNotFoundException($"Required material/construction registry not found: {RegistryPath}");

        MaterialConstructionRegistry registry = JsonUtility.FromJson<MaterialConstructionRegistry>(File.ReadAllText(absolute));
        if (registry == null)
            throw new InvalidOperationException($"Could not parse {RegistryPath}");
        return registry;
    }

    private static void ValidateUnitRange(float value, string field, List<string> errors)
    {
        if (value < 0f || value > 1f) errors.Add($"{field}={value:0.###} is outside [0,1].");
    }

    private static void RequireText(string value, string field, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) errors.Add($"{field} is required.");
    }

    private static void RequireArray(string[] values, int minimum, string field, List<string> errors)
    {
        if (values == null || values.Length < minimum || values.Any(string.IsNullOrWhiteSpace))
            errors.Add($"{field} requires at least {minimum} non-empty entries.");
    }

    private static bool ContainsBakedHighlightLanguage(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        string v = value.ToLowerInvariant();
        return v.Contains("painted highlight") || v.Contains("baked highlight in albedo");
    }

    [Serializable]
    private sealed class MaterialConstructionRegistry
    {
        public string registryVersion;
        public string targetPeriod;
        public string targetRegion;
        public string researchStatus;
        public string visualTarget;
        public MaterialSpec[] materials;
        public AssemblySpec[] assemblies;
    }

    [Serializable]
    private sealed class MaterialSpec
    {
        public string id;
        public string label;
        public string assetPath;
        public string materialFamily;
        public string finish;
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
        public string[] agingCauses;
        public string[] forbidden;
        public string[] lookdevViews;
    }

    [Serializable]
    private sealed class AssemblySpec
    {
        public string id;
        public string label;
        public string nominalDimensions;
        public string manufacture;
        public string mounting;
        public string interfaces;
        public string orientationExposure;
        public string aging;
        public string geometryVsMaterial;
        public ComponentSpec[] components;
        public string lookdevBrief;
    }

    [Serializable]
    private sealed class ComponentSpec
    {
        public string partId;
        public string objectPrefix;
        public string materialId;
        public string attachment;
        public string detailScale;
    }
}
