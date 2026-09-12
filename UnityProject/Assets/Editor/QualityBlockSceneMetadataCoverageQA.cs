using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Prevents a benchmark renderer from silently escaping the manufacture/installation/material
/// reasoning required by the 4K Visual Fidelity gate. This is source/scene metadata QA only:
/// passing it never awards visual points and never clears a rendered critical defect.
///
/// The named automatic-fail condition missing_construction_material_metadata is deliberately
/// machine-bound here: a formal scene is not metadata-complete unless both the detailed material/
/// construction registry and every active benchmark renderer's domain coverage validate.
/// </summary>
public static class QualityBlockSceneMetadataCoverageQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/scene_metadata_coverage_contract.json";
    private const string MaterialRegistryPath = "Assets/QA/material_construction_lookdev.json";
    private const string SceneRootName = "QualityBlock1990s";
    private const string MetadataCriticalDefectId = "missing_construction_material_metadata";

    private static readonly string[] MandatoryMetadataFields =
    {
        "manufacture",
        "dimensionsThickness",
        "materialsFinish",
        "mounting",
        "interfacesGapsSeals",
        "orientationExposure",
        "aging",
        "geometryVsMaterial",
        "lodPolicy"
    };

    private static readonly string[] MandatoryBaseDomains =
    {
        "Ground", "Danchi", "ParkEntrance", "Trees", "StreetFurniture"
    };

    [MenuItem("NewTown/QA/Validate Scene Metadata Coverage Contract")]
    public static void ValidateContractConfigOnly()
    {
        CoverageContract contract = LoadContract();
        List<string> errors = ValidateContract(contract);
        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Scene metadata coverage contract FAILED:\n - " + string.Join("\n - ", errors));

        // The domain contract is only half of the non-visual critical-defect proof. The detailed
        // material/construction registry must remain structurally and physically plausible too.
        QualityBlockMaterialConstructionQA.ValidateRegistry();

        Debug.Log(
            $"Scene metadata coverage contract valid: domains={contract.domains.Length}, " +
            $"mode={contract.coverageMode}, criticalBinding={contract.criticalDefectBinding.defectId}. " +
            "This validates metadata coverage only, not rendered quality.");
    }

    [MenuItem("NewTown/QA/Validate Active Renderer Metadata Coverage")]
    public static void ValidateOpenScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() ||
            EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        CoverageContract contract = LoadContract();
        List<string> errors = ValidateContract(contract);
        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Scene metadata coverage contract FAILED:\n - " + string.Join("\n - ", errors));

        // Fail closed on the registry before accepting any renderer-domain coverage. A scene cannot
        // claim to have construction/material metadata merely because each renderer sits under a named
        // root if the referenced physical-material/manufacture registry itself is incomplete.
        QualityBlockMaterialConstructionQA.ValidateRegistry();

        GameObject sceneRoot = FindSceneObject(SceneRootName);
        if (sceneRoot == null)
            throw new InvalidOperationException($"{SceneRootName} scene root is missing.");

        var domainsByRoot = contract.domains.ToDictionary(x => x.rootName, StringComparer.Ordinal);
        var rendererCountByDomain = contract.domains.ToDictionary(x => x.rootName, _ => 0, StringComparer.Ordinal);
        var coverageErrors = new List<string>();

        Renderer[] renderers = Resources.FindObjectsOfTypeAll<Renderer>()
            .Where(x => x != null && x.gameObject.scene.IsValid() && x.gameObject.scene.path == ScenePath)
            .Where(x => x.enabled && x.gameObject.activeInHierarchy)
            .Where(x => x.transform.IsChildOf(sceneRoot.transform))
            .OrderBy(x => HierarchyPath(x.transform), StringComparer.Ordinal)
            .ToArray();

        if (renderers.Length == 0)
            coverageErrors.Add("No active benchmark renderers were found; metadata coverage cannot be demonstrated.");

        foreach (Renderer renderer in renderers)
        {
            Transform domainRoot = DirectChildBelow(sceneRoot.transform, renderer.transform);
            if (domainRoot == null)
            {
                coverageErrors.Add($"Renderer is not below a scene-domain child: {HierarchyPath(renderer.transform)}");
                continue;
            }

            if (!domainsByRoot.TryGetValue(domainRoot.name, out DomainCoverage domain))
            {
                coverageErrors.Add(
                    $"Active renderer has no construction/material metadata scope: " +
                    $"{HierarchyPath(renderer.transform)} (scene-domain root '{domainRoot.name}').");
                continue;
            }

            rendererCountByDomain[domain.rootName]++;

            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                coverageErrors.Add($"Renderer has no material binding: {HierarchyPath(renderer.transform)}");
                continue;
            }

            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null)
                {
                    coverageErrors.Add(
                        $"Renderer has a null material slot {i}: {HierarchyPath(renderer.transform)}");
                    continue;
                }
                if (material.shader == null)
                    coverageErrors.Add(
                        $"Renderer material has no shader: {HierarchyPath(renderer.transform)} / {material.name}");
            }
        }

        foreach (DomainCoverage domain in contract.domains.Where(x => x.required))
        {
            GameObject root = FindDirectChild(sceneRoot.transform, domain.rootName);
            if (root == null)
            {
                coverageErrors.Add($"Required scene metadata domain root is missing: {domain.rootName}");
                continue;
            }

            if (!rendererCountByDomain.TryGetValue(domain.rootName, out int count) || count <= 0)
                coverageErrors.Add($"Required metadata domain has no active renderer evidence: {domain.rootName}");
        }

        if (coverageErrors.Count > 0)
            throw new InvalidOperationException(
                $"CRITICAL AUTO-FAIL ({MetadataCriticalDefectId}): active renderer construction/material metadata coverage FAILED:\n - " +
                string.Join("\n - ", coverageErrors));

        string counts = string.Join(", ", rendererCountByDomain
            .Where(x => x.Value > 0)
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => $"{x.Key}={x.Value}"));

        Debug.Log(
            $"Active renderer metadata coverage valid: renderers={renderers.Length}; {counts}. " +
            "Every rendered object inherits a registered manufacture/installation/material scope and the detailed material registry passed. " +
            "Visual Fidelity remains unscored until real sealed Unity renders are reviewed.");
    }

    private static List<string> ValidateContract(CoverageContract contract)
    {
        var errors = new List<string>();
        if (contract == null)
        {
            errors.Add("Contract is null or unparseable.");
            return errors;
        }

        RequireText(contract.schemaVersion, "schemaVersion", errors);
        RequireText(contract.scenePath, "scenePath", errors);
        RequireText(contract.sceneRootName, "sceneRootName", errors);
        RequireText(contract.coverageMode, "coverageMode", errors);
        RequireText(contract.policy, "policy", errors);

        if (contract.scenePath != ScenePath)
            errors.Add($"scenePath must be {ScenePath}, got '{contract.scenePath}'.");
        if (contract.sceneRootName != SceneRootName)
            errors.Add($"sceneRootName must be {SceneRootName}, got '{contract.sceneRootName}'.");
        if (contract.coverageMode != "all_active_renderers_in_benchmark")
            errors.Add("coverageMode must require all active benchmark renderers.");

        ValidateCriticalDefectBinding(contract.criticalDefectBinding, errors);

        var requiredFields = new HashSet<string>(contract.requiredMetadataFields ?? Array.Empty<string>(), StringComparer.Ordinal);
        foreach (string field in MandatoryMetadataFields)
            if (!requiredFields.Contains(field))
                errors.Add($"requiredMetadataFields is missing '{field}'.");

        if (contract.domains == null || contract.domains.Length == 0)
        {
            errors.Add("No metadata domains are defined.");
            return errors;
        }

        foreach (IGrouping<string, DomainCoverage> duplicate in contract.domains
                     .Where(x => x != null)
                     .GroupBy(x => x.rootName, StringComparer.Ordinal)
                     .Where(x => string.IsNullOrWhiteSpace(x.Key) || x.Count() > 1))
            errors.Add($"Missing or duplicated domain rootName: '{duplicate.Key}'.");

        foreach (IGrouping<string, DomainCoverage> duplicate in contract.domains
                     .Where(x => x != null)
                     .GroupBy(x => x.id, StringComparer.Ordinal)
                     .Where(x => string.IsNullOrWhiteSpace(x.Key) || x.Count() > 1))
            errors.Add($"Missing or duplicated domain id: '{duplicate.Key}'.");

        foreach (DomainCoverage domain in contract.domains.Where(x => x != null))
        {
            string p = $"domain[{domain.id}]";
            RequireText(domain.id, p + ".id", errors);
            RequireText(domain.rootName, p + ".rootName", errors);
            RequireText(domain.manufacture, p + ".manufacture", errors);
            RequireText(domain.dimensionsThickness, p + ".dimensionsThickness", errors);
            RequireText(domain.materialsFinish, p + ".materialsFinish", errors);
            RequireText(domain.mounting, p + ".mounting", errors);
            RequireText(domain.interfacesGapsSeals, p + ".interfacesGapsSeals", errors);
            RequireText(domain.orientationExposure, p + ".orientationExposure", errors);
            RequireText(domain.aging, p + ".aging", errors);
            RequireText(domain.geometryVsMaterial, p + ".geometryVsMaterial", errors);
            RequireText(domain.lodPolicy, p + ".lodPolicy", errors);

            if (domain.contractPaths == null || domain.contractPaths.Length == 0 ||
                domain.contractPaths.Any(string.IsNullOrWhiteSpace))
            {
                errors.Add($"{p}.contractPaths requires at least one linked machine-readable contract.");
            }
            else
            {
                foreach (string path in domain.contractPaths.Distinct(StringComparer.Ordinal))
                {
                    if (!path.StartsWith("Assets/QA/", StringComparison.Ordinal))
                        errors.Add($"{p} contract path must remain under Assets/QA: {path}");
                    else if (!File.Exists(AbsolutePath(path)))
                        errors.Add($"{p} linked contract is missing: {path}");
                }
            }
        }

        var requiredDomainRoots = new HashSet<string>(
            contract.domains.Where(x => x != null && x.required).Select(x => x.rootName),
            StringComparer.Ordinal);
        foreach (string root in MandatoryBaseDomains)
            if (!requiredDomainRoots.Contains(root))
                errors.Add($"Mandatory base renderer domain is not required by metadata coverage: {root}");

        return errors;
    }

    private static void ValidateCriticalDefectBinding(CriticalDefectBinding binding, List<string> errors)
    {
        if (binding == null)
        {
            errors.Add("criticalDefectBinding is required so missing metadata cannot be cleared by visual review alone.");
            return;
        }

        if (binding.defectId != MetadataCriticalDefectId)
            errors.Add($"criticalDefectBinding.defectId must be '{MetadataCriticalDefectId}'.");
        if (binding.materialRegistryPath != MaterialRegistryPath)
            errors.Add($"criticalDefectBinding.materialRegistryPath must be '{MaterialRegistryPath}'.");
        if (!binding.requireMaterialRegistryValidation)
            errors.Add("criticalDefectBinding must require material/construction registry validation.");
        if (!binding.requireActiveRendererCoverage)
            errors.Add("criticalDefectBinding must require active-renderer metadata coverage.");
        if (!binding.failClosedBeforeFormalRender)
            errors.Add("criticalDefectBinding must fail closed before formal render evidence.");
        if (!binding.failClosedBeforeVisualScoring)
            errors.Add("criticalDefectBinding must fail closed before Visual Fidelity scoring.");
        if (binding.sourcePassAwardsVisualPoints)
            errors.Add("Metadata source/scene QA may not award Visual Fidelity points.");
        if (binding.sourcePassClearsRenderedDefects)
            errors.Add("Metadata source/scene QA may not clear rendered critical defects.");

        RequireSourceToken(binding.formalRenderEntryPointPath, binding.requiredFormalRenderToken,
            binding.minimumFormalRenderOccurrences, "formal render entry point", errors);
        RequireSourceToken(binding.visualScoringEntryPointPath, binding.requiredVisualScoringToken,
            binding.minimumVisualScoringOccurrences, "visual scoring entry point", errors);
    }

    private static void RequireSourceToken(string assetPath, string token, int minimumOccurrences, string label,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            errors.Add($"criticalDefectBinding {label} path is required.");
            return;
        }
        if (string.IsNullOrWhiteSpace(token))
        {
            errors.Add($"criticalDefectBinding {label} token is required.");
            return;
        }
        if (minimumOccurrences < 1)
        {
            errors.Add($"criticalDefectBinding {label} minimum occurrence count must be >= 1.");
            return;
        }

        string absolute = AbsolutePath(assetPath);
        if (!File.Exists(absolute))
        {
            errors.Add($"criticalDefectBinding {label} source is missing: {assetPath}");
            return;
        }

        string source = File.ReadAllText(absolute);
        int count = CountOccurrences(source, token);
        if (count < minimumOccurrences)
            errors.Add(
                $"criticalDefectBinding {label} is not fail-closed: token '{token}' occurs {count} time(s), expected >= {minimumOccurrences} in {assetPath}.");
    }

    private static int CountOccurrences(string source, string token)
    {
        int count = 0;
        int offset = 0;
        while (offset <= source.Length - token.Length)
        {
            int index = source.IndexOf(token, offset, StringComparison.Ordinal);
            if (index < 0) break;
            count++;
            offset = index + token.Length;
        }
        return count;
    }

    private static CoverageContract LoadContract()
    {
        string path = AbsolutePath(ContractPath);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Scene metadata coverage contract missing: {ContractPath}");

        CoverageContract contract = JsonUtility.FromJson<CoverageContract>(File.ReadAllText(path));
        if (contract == null)
            throw new InvalidOperationException($"Could not parse {ContractPath}");
        return contract;
    }

    private static string AbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrWhiteSpace(projectRoot))
            throw new InvalidOperationException("Could not resolve Unity project root for metadata QA.");
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x.scene.IsValid() && x.scene.path == ScenePath && x.name == name);
    }

    private static GameObject FindDirectChild(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name) return child.gameObject;
        }
        return null;
    }

    private static Transform DirectChildBelow(Transform root, Transform descendant)
    {
        if (root == null || descendant == null || descendant == root || !descendant.IsChildOf(root))
            return null;

        Transform cursor = descendant;
        while (cursor.parent != null && cursor.parent != root)
            cursor = cursor.parent;
        return cursor.parent == root ? cursor : null;
    }

    private static string HierarchyPath(Transform transform)
    {
        var parts = new Stack<string>();
        Transform cursor = transform;
        while (cursor != null)
        {
            parts.Push(cursor.name);
            cursor = cursor.parent;
        }
        return string.Join("/", parts);
    }

    private static void RequireText(string value, string field, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) errors.Add($"{field} is required.");
    }

    [Serializable]
    private sealed class CoverageContract
    {
        public string schemaVersion;
        public string scenePath;
        public string sceneRootName;
        public string coverageMode;
        public string policy;
        public CriticalDefectBinding criticalDefectBinding;
        public string[] requiredMetadataFields;
        public DomainCoverage[] domains;
    }

    [Serializable]
    private sealed class CriticalDefectBinding
    {
        public string defectId;
        public string materialRegistryPath;
        public bool requireMaterialRegistryValidation;
        public bool requireActiveRendererCoverage;
        public bool failClosedBeforeFormalRender;
        public bool failClosedBeforeVisualScoring;
        public bool sourcePassAwardsVisualPoints;
        public bool sourcePassClearsRenderedDefects;
        public string formalRenderEntryPointPath;
        public string requiredFormalRenderToken;
        public int minimumFormalRenderOccurrences;
        public string visualScoringEntryPointPath;
        public string requiredVisualScoringToken;
        public int minimumVisualScoringOccurrences;
    }

    [Serializable]
    private sealed class DomainCoverage
    {
        public string id;
        public string rootName;
        public bool required;
        public string manufacture;
        public string dimensionsThickness;
        public string materialsFinish;
        public string mounting;
        public string interfacesGapsSeals;
        public string orientationExposure;
        public string aging;
        public string geometryVsMaterial;
        public string lodPolicy;
        public string[] contractPaths;
    }
}
