using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Machine-checks the vegetation ecology lookdev contract before scene persistence/capture.
/// This validates source intent and physical parameter bounds only; it never substitutes for render review.
/// </summary>
public static class QualityBlockVegetationEcologyContractQA
{
    private const string ContractPath = "Assets/QA/vegetation_ecology_contract.json";

    [MenuItem("NewTown/QA/Validate Vegetation Ecology Contract")]
    public static void Validate()
    {
        if (!File.Exists(ContractPath))
            throw new InvalidOperationException($"Vegetation ecology contract missing: {ContractPath}");

        string json = File.ReadAllText(ContractPath);
        Contract contract = JsonUtility.FromJson<Contract>(json);
        if (contract == null)
            throw new InvalidOperationException("Vegetation ecology contract could not be parsed.");
        if (contract.runtimeRenderVerified)
            throw new InvalidOperationException("Vegetation ecology contract must not claim runtime render verification before real Unity evidence exists.");
        if (!string.Equals(contract.visualFidelityStatus, "UNSCORED_UNTIL_REAL_4K_RENDER", StringComparison.Ordinal))
            throw new InvalidOperationException("Vegetation ecology contract must remain visually unscored until native 4K evidence is reviewed.");
        if (contract.sceneAssumptions == null || contract.sceneAssumptions.treeAnchorCount != 6 ||
            contract.sceneAssumptions.pavedTreePitCount != 4 || contract.sceneAssumptions.grassGroundTreeCount != 2)
            throw new InvalidOperationException("Vegetation ecology benchmark anchor/pit counts are inconsistent with the quality scene.");
        if (!contract.sceneAssumptions.noGameplayColliderChanges)
            throw new InvalidOperationException("Vegetation ecology must preserve gameplay collider separation.");
        if (contract.assemblies == null || contract.assemblies.Length < 2)
            throw new InvalidOperationException("Vegetation ecology contract requires tree-pit and understory assemblies.");
        if (contract.materials == null || contract.materials.Length < 3)
            throw new InvalidOperationException("Vegetation ecology contract requires soil, edging and foliage material specs.");

        foreach (MaterialSpec material in contract.materials)
        {
            Require(material.id, "material id");
            Require(material.sourceAsset, $"sourceAsset for {material.id}");
            Require(material.angularFresnelResponse, $"angular Fresnel response for {material.id}");
            if (material.roughnessRange == null || material.roughnessRange.Length != 2 ||
                material.roughnessRange[0] < 0f || material.roughnessRange[1] > 1f ||
                material.roughnessRange[0] > material.roughnessRange[1])
                throw new InvalidOperationException($"Invalid roughness range for vegetation material {material.id}.");
            if (material.metallicRange == null || material.metallicRange.Length != 2 ||
                material.metallicRange[0] < 0f || material.metallicRange[1] > 1f ||
                material.metallicRange[0] > material.metallicRange[1])
                throw new InvalidOperationException($"Invalid metallic range for vegetation material {material.id}.");
            if (material.metallicRange[1] > 0.02f)
                throw new InvalidOperationException($"Vegetation ecology material {material.id} must remain dielectric/non-metallic.");
        }

        foreach (AssemblySpec assembly in contract.assemblies)
        {
            Require(assembly.id, "assembly id");
            Require(assembly.establishment, $"establishment for {assembly.id}");
            Require(assembly.nominalDimensions, $"nominalDimensions for {assembly.id}");
            Require(assembly.materialsFinish, $"materialsFinish for {assembly.id}");
            Require(assembly.mounting, $"mounting for {assembly.id}");
            Require(assembly.interfaces, $"interfaces for {assembly.id}");
            Require(assembly.orientationExposure, $"orientationExposure for {assembly.id}");
            Require(assembly.aging, $"aging for {assembly.id}");
            Require(assembly.geometryVsMaterial, $"geometryVsMaterial for {assembly.id}");
            Require(assembly.lodPolicy, $"lodPolicy for {assembly.id}");
            Require(assembly.lookdevBrief, $"lookdevBrief for {assembly.id}");
        }

        if (contract.qa == null || contract.qa.requiredPatchCount != 6 || contract.qa.requiredPavedPitCount != 4 ||
            contract.qa.requiredEdgingModulesPerPit != 12 || contract.qa.requiredUnderstoryLodCount != 4 ||
            !contract.qa.requireAnimatedCrossFade || !contract.qa.requireNoGeneratedColliders ||
            !contract.qa.requireNonPrimitiveMeshes || !contract.qa.requireNonMetallicSoilAndConcrete)
            throw new InvalidOperationException("Vegetation ecology QA contract is missing required structural invariants.");

        Debug.Log("Vegetation ecology contract QA passed. Runtime/render verification remains pending and Visual Fidelity remains unscored.");
    }

    private static void Require(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Vegetation ecology contract missing {label}.");
    }

    [Serializable]
    private sealed class Contract
    {
        public bool runtimeRenderVerified;
        public string visualFidelityStatus;
        public SceneAssumptions sceneAssumptions;
        public AssemblySpec[] assemblies;
        public MaterialSpec[] materials;
        public QaSpec qa;
    }

    [Serializable]
    private sealed class SceneAssumptions
    {
        public int treeAnchorCount;
        public int pavedTreePitCount;
        public int grassGroundTreeCount;
        public bool noGameplayColliderChanges;
    }

    [Serializable]
    private sealed class AssemblySpec
    {
        public string id;
        public string establishment;
        public string nominalDimensions;
        public string materialsFinish;
        public string mounting;
        public string interfaces;
        public string orientationExposure;
        public string aging;
        public string geometryVsMaterial;
        public string lodPolicy;
        public string lookdevBrief;
    }

    [Serializable]
    private sealed class MaterialSpec
    {
        public string id;
        public string sourceAsset;
        public float[] roughnessRange;
        public float[] metallicRange;
        public string angularFresnelResponse;
    }

    [Serializable]
    private sealed class QaSpec
    {
        public int requiredPatchCount;
        public int requiredPavedPitCount;
        public int requiredEdgingModulesPerPit;
        public int requiredUnderstoryLodCount;
        public bool requireAnimatedCrossFade;
        public bool requireNoGeneratedColliders;
        public bool requireNonPrimitiveMeshes;
        public bool requireNonMetallicSoilAndConcrete;
    }
}
