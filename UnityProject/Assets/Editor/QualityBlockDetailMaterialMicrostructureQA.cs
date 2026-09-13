using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Fail-closed policy binding for the generated detail-material microstructure pass.
/// The actual mapped metallic/roughness ranges are independently enforced by
/// QualityBlockRegisteredMaterialAssetPhysicalityQA before reflection capture and every formal pre-cull.
/// This guard additionally freezes the microstructure source/importer policy and canonical evidence crops.
/// It awards zero Visual Fidelity points.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockDetailMaterialMicrostructureQA
{
    private const string ContractPath = "Assets/QA/detail_material_microstructure_contract.json";
    private const string GeneratorPath = "Assets/Editor/QualityBlockDetailMaterialMicrostructureUpgrade.cs";
    private const string RegistryPath = "Assets/QA/material_construction_lookdev.json";
    private const int Width = 3840;
    private const int Height = 2160;

    private static readonly string[] FormalTargetPrefixes = { "QA4K_", "QAPreparedTemporal_" };
    private static readonly string[] RequiredMaterialPaths =
    {
        "Assets/Art/GeneratedDetailMaterials/MAT_AgedAluminum.mat",
        "Assets/Art/GeneratedDetailMaterials/MAT_DarkGalvanizedSteel.mat",
        "Assets/Art/GeneratedDetailMaterials/MAT_WindowRubber.mat",
        "Assets/Art/GeneratedDetailMaterials/MAT_AgedACPlastic.mat",
        "Assets/Art/GeneratedDetailMaterials/MAT_PipeInsulation.mat",
        "Assets/Art/GeneratedDetailMaterials/MAT_DrainHose.mat",
    };

    private static readonly string[] RequiredCrops =
    {
        "grazing/material_grazing",
        "grazing/sash_rail_response",
        "oblique/balcony_services",
    };

    private static readonly string[] CanonicalCriticalRisks =
    {
        "baked_or_painted_highlights",
        "impossible_material_physics",
        "obvious_repetition",
        "severe_aliasing_or_shimmer",
    };

    private static bool validating;

    static QualityBlockDetailMaterialMicrostructureQA()
    {
        Camera.onPreCull -= OnCameraPreCull;
        Camera.onPreCull += OnCameraPreCull;
    }

    [MenuItem("NewTown/QA/Validate Detail Material Microstructure Contract")]
    public static void ValidateContractConfigOnly()
    {
        Contract contract = LoadJson<Contract>(ContractPath);
        if (contract == null || !string.Equals(contract.schemaVersion, "1.0", StringComparison.Ordinal))
            throw new InvalidOperationException("Detail-material microstructure contract is null/unparseable or not schema 1.0.");
        if (!string.Equals(contract.status, "IMPLEMENTATION_READY_RENDER_PENDING", StringComparison.Ordinal))
            throw new InvalidOperationException("Detail-material microstructure contract may not claim rendered completion before evidence exists.");
        if (!string.Equals(contract.generator, GeneratorPath, StringComparison.Ordinal) ||
            !string.Equals(contract.registry, RegistryPath, StringComparison.Ordinal))
            throw new InvalidOperationException("Detail-material microstructure generator/registry identity drifted.");

        Target target = contract.target;
        if (target == null || target.textureSize == null || target.textureSize.Length != 2 ||
            target.textureSize[0] != 1024 || target.textureSize[1] != 1024 ||
            !string.Equals(target.normalSpace, "tangent", StringComparison.Ordinal) ||
            !target.lightingNeutral || !target.albedoHighlightBakeForbidden ||
            !target.weatheringExcludedFromMicrostructure)
            throw new InvalidOperationException("Detail-material microstructure target policy was weakened or drifted.");

        Sampling sampling = contract.sampling;
        if (sampling == null || !string.Equals(sampling.wrap, "Repeat", StringComparison.Ordinal) ||
            !string.Equals(sampling.filter, "Trilinear", StringComparison.Ordinal) ||
            !sampling.mipmapsRequired || sampling.anisotropy < 8 || sampling.sRGBForDataMaps)
            throw new InvalidOperationException("Detail-material microstructure anti-aliasing/data-map sampling policy was weakened.");

        if (contract.materials == null || contract.materials.Length != RequiredMaterialPaths.Length)
            throw new InvalidOperationException("Detail-material microstructure contract must contain exactly six canonical material entries.");
        string[] paths = contract.materials.Select(x => x == null ? null : x.assetPath).ToArray();
        RequireExactSet(paths, RequiredMaterialPaths, "material asset paths");
        foreach (MaterialEntry entry in contract.materials)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.materialId) ||
                string.IsNullOrWhiteSpace(entry.normalMap) || string.IsNullOrWhiteSpace(entry.maskMap) ||
                entry.metallicRange == null || entry.metallicRange.Length != 2 ||
                entry.roughnessRange == null || entry.roughnessRange.Length != 2 ||
                !FiniteUnitRange(entry.metallicRange) || !FiniteUnitRange(entry.roughnessRange) ||
                string.IsNullOrWhiteSpace(entry.microstructure) || string.IsNullOrWhiteSpace(entry.manufactureReasoning))
                throw new InvalidOperationException("Detail-material microstructure material metadata is incomplete or non-physical.");
        }

        FormalEvidence formal = contract.formalEvidenceRequirements;
        if (formal == null || formal.nativeResolution == null || formal.nativeResolution.Length != 2 ||
            formal.nativeResolution[0] != Width || formal.nativeResolution[1] != Height ||
            !formal.humanReviewRequired || !formal.actualRenderRequiredForVisualPoints)
            throw new InvalidOperationException("Detail-material microstructure formal evidence requirements were weakened.");
        RequireExactSet(formal.requiredViews, new[] { "hero", "oblique", "grazing" }, "required views");
        RequireExactSet(formal.required100PercentCrops, RequiredCrops, "required 100% crops");
        if (formal.reviewFor == null || formal.reviewFor.Length < 6 || formal.reviewFor.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Detail-material microstructure rendered review checklist is incomplete.");

        RequireExactSet(contract.criticalDefectRisksReduced, CanonicalCriticalRisks, "critical defect risks");
        if (contract.implementationReadinessScore != 93 ||
            !string.Equals(contract.visualFidelityStatus, "UNSCORED_UNTIL_REAL_4K_RENDER", StringComparison.Ordinal) ||
            contract.runtimeRenderVerified)
            throw new InvalidOperationException("Detail-material microstructure contract may not inflate readiness or claim Visual Fidelity/render verification.");
        if (contract.limitations == null || contract.limitations.Length < 4 || contract.limitations.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Detail-material microstructure limitations must remain explicit.");

        // Keep the actual asset-to-registry physicality contract authoritative for mapped BRDF ranges.
        QualityBlockRegisteredMaterialAssetPhysicalityQA.ValidateContractConfigOnly();
    }

    [MenuItem("NewTown/QA/Validate Detail Material Microstructure Formal State")]
    public static void ValidateFormalState()
    {
        ValidateContractConfigOnly();
        QualityBlockDetailMaterialMicrostructureUpgrade.ValidateGeneratedAssets(false);
        Debug.Log("Detail-material microstructure formal source state valid. Actual native-4K pixel review is still required before any Visual Fidelity points can be awarded.");
    }

    private static void OnCameraPreCull(Camera camera)
    {
        if (!IsFormalEvidenceCamera(camera))
            return;
        if (validating)
            throw new InvalidOperationException("Detail-material microstructure QA re-entered during formal pre-cull.");

        validating = true;
        try
        {
            ValidateContractConfigOnly();
            QualityBlockDetailMaterialMicrostructureUpgrade.ValidateGeneratedAssets(false);
        }
        finally
        {
            validating = false;
        }
    }

    private static bool IsFormalEvidenceCamera(Camera camera)
    {
        if (camera == null || camera.targetTexture == null || camera.targetTexture.width != Width || camera.targetTexture.height != Height)
            return false;
        string targetName = camera.targetTexture.name ?? string.Empty;
        return FormalTargetPrefixes.Any(prefix => targetName.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static bool FiniteUnitRange(float[] range)
    {
        if (range == null || range.Length != 2)
            return false;
        float min = range[0];
        float max = range[1];
        return !float.IsNaN(min) && !float.IsInfinity(min) && !float.IsNaN(max) && !float.IsInfinity(max) &&
            min >= 0f && max <= 1f && min <= max;
    }

    private static void RequireExactSet(string[] actual, string[] expected, string label)
    {
        if (actual == null || actual.Any(string.IsNullOrWhiteSpace) ||
            actual.Distinct(StringComparer.Ordinal).Count() != actual.Length ||
            !new HashSet<string>(actual, StringComparer.Ordinal).SetEquals(expected ?? Array.Empty<string>()))
            throw new InvalidOperationException(label + " must be exactly [" + string.Join(", ", expected ?? Array.Empty<string>()) + "].");
    }

    private static T LoadJson<T>(string assetPath)
    {
        string absolute = Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath));
        if (!File.Exists(absolute))
            throw new FileNotFoundException("Required QA file not found: " + assetPath);
        T value = JsonUtility.FromJson<T>(File.ReadAllText(absolute));
        if (value == null)
            throw new InvalidOperationException("Could not parse QA JSON: " + assetPath);
        return value;
    }

    [Serializable]
    private sealed class Contract
    {
        public string schemaVersion;
        public string status;
        public string generator;
        public string registry;
        public Target target;
        public Sampling sampling;
        public MaterialEntry[] materials;
        public FormalEvidence formalEvidenceRequirements;
        public string[] criticalDefectRisksReduced;
        public string[] limitations;
        public int implementationReadinessScore;
        public string visualFidelityStatus;
        public bool runtimeRenderVerified;
    }

    [Serializable]
    private sealed class Target
    {
        public int[] textureSize;
        public string normalSpace;
        public bool lightingNeutral;
        public bool albedoHighlightBakeForbidden;
        public bool weatheringExcludedFromMicrostructure;
    }

    [Serializable]
    private sealed class Sampling
    {
        public string wrap;
        public string filter;
        public bool mipmapsRequired;
        public int anisotropy;
        public bool sRGBForDataMaps;
    }

    [Serializable]
    private sealed class MaterialEntry
    {
        public string materialId;
        public string assetPath;
        public string normalMap;
        public string maskMap;
        public float[] metallicRange;
        public float[] roughnessRange;
        public string microstructure;
        public string manufactureReasoning;
    }

    [Serializable]
    private sealed class FormalEvidence
    {
        public int[] nativeResolution;
        public string[] requiredViews;
        public string[] required100PercentCrops;
        public bool humanReviewRequired;
        public string[] reviewFor;
        public bool actualRenderRequiredForVisualPoints;
    }
}
