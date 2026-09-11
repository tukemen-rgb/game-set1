using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Fail-closed formal-evidence binding for generated foliage morphology diversity.
/// Source diversity reduces clone risk but awards zero Visual Fidelity points; actual native-4K
/// still and temporal pixels remain authoritative for repetition, silhouette and shimmer.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockFoliageMorphologyDiversityQA
{
    private const string ContractPath = "Assets/QA/foliage_morphology_diversity_contract.json";
    private const string GatePath = "Assets/QA/visual_fidelity_gate.json";
    private const int Width = 3840;
    private const int Height = 2160;

    private static readonly string[] FormalTargetPrefixes = { "QA4K_", "QAPreparedTemporal_" };
    private static readonly string[] CriticalRisks =
    {
        "obvious_repetition",
        "severe_aliasing_or_shimmer",
        "visible_lod_pop",
    };
    private static readonly string[] RequiredViews = { "hero", "oblique", "grazing" };
    private static readonly string[] RequiredCrops =
    {
        "oblique/vegetation_grounding",
        "grazing/tree_shadow_contact",
    };

    private static bool validating;

    static QualityBlockFoliageMorphologyDiversityQA()
    {
        Camera.onPreCull -= OnCameraPreCull;
        Camera.onPreCull += OnCameraPreCull;
    }

    [MenuItem("NewTown/QA/Validate Foliage Morphology Diversity Contract")]
    public static void ValidateContractConfigOnly()
    {
        Contract contract = LoadJson<Contract>(ContractPath);
        if (contract == null || !string.Equals(contract.schemaVersion, "1.0", StringComparison.Ordinal))
            throw new InvalidOperationException("Foliage morphology diversity contract is null/unparseable or not schema 1.0.");
        if (!string.Equals(contract.status, "IMPLEMENTATION_READY_RENDER_PENDING", StringComparison.Ordinal))
            throw new InvalidOperationException("Foliage morphology contract may not claim rendered completion before evidence exists.");
        if (!string.Equals(contract.scenePath, QualityBlockFoliageMorphologyVariationUpgrade.ScenePath, StringComparison.Ordinal) ||
            !string.Equals(contract.generator, "Assets/Editor/QualityBlockFoliageMorphologyVariationUpgrade.cs", StringComparison.Ordinal))
            throw new InvalidOperationException("Foliage morphology scene/generator identity drifted.");

        SourceLibrary library = contract.sourceLibrary;
        if (library == null || library.derivedVariantCount != QualityBlockFoliageMorphologyVariationUpgrade.VariantCount ||
            !library.noRuntimeRandomness || library.baseSprays == null || library.baseSprays.Length != 3 ||
            !new HashSet<string>(library.baseSprays, StringComparer.Ordinal).SetEquals(new[] { "GM_LeafSpray_A", "GM_LeafSpray_B", "GM_LeafSpray_C" }))
            throw new InvalidOperationException("Foliage morphology source-library policy was weakened or drifted.");

        DiversityRules diversity = contract.diversityRules;
        if (diversity == null || diversity.expectedGeneratedTrees != 6 ||
            diversity.minimumSourceLeafClustersPerGeneratedTree < 24 ||
            diversity.minimumDistinctVariantsPerGeneratedTree != QualityBlockFoliageMorphologyVariationUpgrade.MinimumDistinctVariantsPerTree ||
            diversity.maximumExactVariantUsePerGeneratedTree != QualityBlockFoliageMorphologyVariationUpgrade.MaximumUsePerVariantPerTree ||
            !diversity.sourceAndRetainedLodMorphologyIdentityRequired ||
            !diversity.authoredArtSupersedesGeneratedFallback || !diversity.colliderMutationForbidden ||
            !diversity.treePlacementMutationForbidden || !diversity.woodyConstructionMutationForbidden)
            throw new InvalidOperationException("Foliage morphology diversity/installation policy was weakened.");

        LodPolicy lod = contract.lodPolicy;
        if (lod == null || lod.levels != 4 || !lod.animatedCrossFadeRequired || !lod.temporalReviewRequired ||
            string.IsNullOrWhiteSpace(lod.identityRule))
            throw new InvalidOperationException("Foliage morphology LOD policy is incomplete.");

        RequireExactSet(contract.criticalDefectRisksReduced, CriticalRisks, "foliage morphology critical-defect risks");
        ValidateCriticalIdsExistInGate(CriticalRisks);

        FormalEvidence formal = contract.formalEvidenceRequirements;
        if (formal == null || formal.nativeResolution == null || formal.nativeResolution.Length != 2 ||
            formal.nativeResolution[0] != Width || formal.nativeResolution[1] != Height ||
            !formal.humanReviewRequired || !formal.actualRenderRequiredForVisualPoints ||
            !string.Equals(formal.requiredTemporalTarget, "temporal/subpixel_grazing", StringComparison.Ordinal))
            throw new InvalidOperationException("Foliage morphology formal evidence requirements were weakened.");
        RequireExactSet(formal.requiredViews, RequiredViews, "foliage morphology required views");
        RequireExactSet(formal.required100PercentCrops, RequiredCrops, "foliage morphology required crops");
        if (formal.reviewFor == null || formal.reviewFor.Length < 7 || formal.reviewFor.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Foliage morphology rendered-review checklist is incomplete.");

        Lookdev lookdev = contract.lookdev;
        if (lookdev == null || string.IsNullOrWhiteSpace(lookdev.illustration) || lookdev.isRenderEvidence)
            throw new InvalidOperationException("Foliage morphology lookdev must exist as non-render evidence.");
        if (!File.Exists(ToAbsolutePath(lookdev.illustration)))
            throw new FileNotFoundException("Foliage morphology lookdev illustration is missing: " + lookdev.illustration);

        if (contract.implementationReadinessScore != 93 ||
            !string.Equals(contract.visualFidelityStatus, "UNSCORED_UNTIL_REAL_4K_RENDER", StringComparison.Ordinal) ||
            contract.runtimeRenderVerified || contract.autoVisualPoints != 0)
            throw new InvalidOperationException("Foliage morphology contract may not inflate readiness or claim Visual Fidelity/render verification.");
        if (contract.limitations == null || contract.limitations.Length < 4 || contract.limitations.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Foliage morphology limitations must remain explicit.");
        if (contract.hardFailRules == null || contract.hardFailRules.Length < 10 || contract.hardFailRules.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Foliage morphology hard-fail rules are incomplete.");
    }

    [MenuItem("NewTown/QA/Validate Foliage Morphology Formal State")]
    public static void ValidateFormalState()
    {
        ValidateContractConfigOnly();
        QualityBlockFoliageMorphologyVariationUpgrade.ValidateCurrentScene(false);
        Debug.Log("Foliage morphology formal source state valid. Actual native-4K still/temporal review remains required before Vegetation, Texture or Temporal/LOD points can be awarded.");
    }

    private static void OnCameraPreCull(Camera camera)
    {
        if (!IsFormalEvidenceCamera(camera))
            return;
        if (validating)
            throw new InvalidOperationException("Foliage morphology QA re-entered during formal pre-cull.");

        validating = true;
        try
        {
            ValidateContractConfigOnly();
            QualityBlockFoliageMorphologyVariationUpgrade.ValidateCurrentScene(false);
        }
        finally
        {
            validating = false;
        }
    }

    private static bool IsFormalEvidenceCamera(Camera camera)
    {
        if (camera == null || camera.targetTexture == null ||
            camera.targetTexture.width != Width || camera.targetTexture.height != Height)
            return false;
        string targetName = camera.targetTexture.name ?? string.Empty;
        return FormalTargetPrefixes.Any(prefix => targetName.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static void ValidateCriticalIdsExistInGate(IEnumerable<string> ids)
    {
        VisualGate gate = LoadJson<VisualGate>(GatePath);
        if (gate == null || gate.criticalDefects == null)
            throw new InvalidOperationException("Visual Fidelity gate critical-defect registry is unavailable.");
        var gateIds = new HashSet<string>(gate.criticalDefects.Where(x => x != null).Select(x => x.id), StringComparer.Ordinal);
        foreach (string id in ids)
        {
            if (!gateIds.Contains(id))
                throw new InvalidOperationException("Foliage morphology contract references non-canonical Visual Fidelity critical defect id: " + id);
        }
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
        string absolute = ToAbsolutePath(assetPath);
        if (!File.Exists(absolute))
            throw new FileNotFoundException("Required QA file not found: " + assetPath);
        T value = JsonUtility.FromJson<T>(File.ReadAllText(absolute));
        if (value == null)
            throw new InvalidOperationException("Could not parse QA JSON: " + assetPath);
        return value;
    }

    private static string ToAbsolutePath(string assetPath)
    {
        return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath));
    }

    [Serializable]
    private sealed class Contract
    {
        public string schemaVersion;
        public string status;
        public string scenePath;
        public string generator;
        public SourceLibrary sourceLibrary;
        public DiversityRules diversityRules;
        public LodPolicy lodPolicy;
        public string[] criticalDefectRisksReduced;
        public string[] hardFailRules;
        public FormalEvidence formalEvidenceRequirements;
        public Lookdev lookdev;
        public int implementationReadinessScore;
        public string visualFidelityStatus;
        public bool runtimeRenderVerified;
        public int autoVisualPoints;
        public string[] limitations;
    }

    [Serializable]
    private sealed class SourceLibrary
    {
        public string[] baseSprays;
        public int derivedVariantCount;
        public bool noRuntimeRandomness;
    }

    [Serializable]
    private sealed class DiversityRules
    {
        public int expectedGeneratedTrees;
        public int minimumSourceLeafClustersPerGeneratedTree;
        public int minimumDistinctVariantsPerGeneratedTree;
        public int maximumExactVariantUsePerGeneratedTree;
        public bool sourceAndRetainedLodMorphologyIdentityRequired;
        public bool authoredArtSupersedesGeneratedFallback;
        public bool colliderMutationForbidden;
        public bool treePlacementMutationForbidden;
        public bool woodyConstructionMutationForbidden;
    }

    [Serializable]
    private sealed class LodPolicy
    {
        public int levels;
        public bool animatedCrossFadeRequired;
        public string identityRule;
        public bool temporalReviewRequired;
    }

    [Serializable]
    private sealed class FormalEvidence
    {
        public int[] nativeResolution;
        public string[] requiredViews;
        public string[] required100PercentCrops;
        public string requiredTemporalTarget;
        public bool humanReviewRequired;
        public bool actualRenderRequiredForVisualPoints;
        public string[] reviewFor;
    }

    [Serializable]
    private sealed class Lookdev
    {
        public string illustration;
        public bool isRenderEvidence;
    }

    [Serializable]
    private sealed class VisualGate
    {
        public CriticalDefect[] criticalDefects;
    }

    [Serializable]
    private sealed class CriticalDefect
    {
        public string id;
    }
}
