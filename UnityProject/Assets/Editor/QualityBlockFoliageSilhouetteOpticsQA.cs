using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Fail-closed source/runtime-state QA for the analytic healthy-leaf margin used by the generated
/// fallback trees. This validates implementation state only; native 3840x2160 pixels remain the
/// sole basis for Vegetation, Lighting or Temporal/LOD Visual Fidelity points.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockFoliageSilhouetteOpticsQA
{
    private const string ContractPath = "Assets/QA/foliage_silhouette_optics_contract.json";
    private const string GatePath = "Assets/QA/visual_fidelity_gate.json";
    private const string ShaderPath = "Assets/Shaders/NewTownFoliageTransmission.shader";
    private const string ShaderName = "NewTown/FoliageTransmission";
    private const string MasterPrefix = "HD_TreeMaster_";
    private const int Width = 3840;
    private const int Height = 2160;

    private static readonly int LeafEdgeInsetId = Shader.PropertyToID("_LeafEdgeInset");
    private static readonly int LeafSerrationId = Shader.PropertyToID("_LeafSerration");
    private static readonly int LeafEdgeAAScaleId = Shader.PropertyToID("_LeafEdgeAAScale");
    private static readonly int LeafVariationId = Shader.PropertyToID("_LeafVariation");

    private static readonly string[] FormalTargetPrefixes = { "QA4K_", "QAPreparedTemporal_" };
    private static readonly string[] CriticalRisks =
    {
        "visible_primitive_placeholder",
        "obvious_repetition",
        "severe_aliasing_or_shimmer",
    };
    private static readonly string[] RequiredViews = { "hero", "oblique", "grazing" };
    private static readonly string[] RequiredCrops =
    {
        "oblique/vegetation_grounding",
        "grazing/tree_shadow_contact",
    };
    private static readonly string[] RequiredShaderTokens =
    {
        "half LeafEdgeCoverage(float2 rawUv, half variation)",
        "o.rawLeafUv = v.uv;",
        "AlphaToMask On",
        "clip(edgeCoverage - 0.01h);",
        "clip(LeafEdgeCoverage(i.rawLeafUv, _LeafVariation) - 0.5h);",
    };

    private static bool validating;

    static QualityBlockFoliageSilhouetteOpticsQA()
    {
        Camera.onPreCull -= OnCameraPreCull;
        Camera.onPreCull += OnCameraPreCull;
    }

    [MenuItem("NewTown/QA/Validate Foliage Silhouette Optics Contract")]
    public static void ValidateContractConfigOnly()
    {
        Contract contract = LoadJson<Contract>(ContractPath);
        if (contract == null || !string.Equals(contract.schemaVersion, "1.0", StringComparison.Ordinal))
            throw new InvalidOperationException("Foliage silhouette optics contract is null/unparseable or not schema 1.0.");
        if (!string.Equals(contract.status, "IMPLEMENTATION_READY_RENDER_PENDING", StringComparison.Ordinal) ||
            !string.Equals(contract.scenePath, QualityBlockFoliageMorphologyVariationUpgrade.ScenePath, StringComparison.Ordinal) ||
            !string.Equals(contract.shaderPath, ShaderPath, StringComparison.Ordinal))
            throw new InvalidOperationException("Foliage silhouette contract identity/status drifted.");

        AssemblyReasoning assembly = contract.assemblyReasoning;
        if (assembly == null || string.IsNullOrWhiteSpace(assembly.component) ||
            string.IsNullOrWhiteSpace(assembly.constructionModel) || string.IsNullOrWhiteSpace(assembly.orientation) ||
            string.IsNullOrWhiteSpace(assembly.interfaces) || string.IsNullOrWhiteSpace(assembly.geometryVsMaterial) ||
            string.IsNullOrWhiteSpace(assembly.aging))
            throw new InvalidOperationException("Foliage silhouette manufacturing/installation reasoning is incomplete.");

        EdgeModel edge = contract.edgeModel;
        if (edge == null || edge.leafEdgeInset < 0.040f || edge.leafEdgeInset > 0.080f ||
            edge.leafSerration < 0.010f || edge.leafSerration > 0.030f ||
            edge.leafEdgeAAScale < 1.0f || edge.leafEdgeAAScale > 1.8f ||
            Mathf.Abs(edge.forwardClipThreshold - 0.01f) > 0.0001f ||
            Mathf.Abs(edge.shadowClipThreshold - 0.50f) > 0.0001f ||
            !edge.alphaToCoverageRequired || !edge.rawCanonicalUvRequired ||
            !edge.tiledPbrUvMustNotDriveMacroSilhouette ||
            !edge.clusterVariationMayOnlyPhaseMarginIrregularity || !edge.runtimeRandomnessForbidden)
            throw new InvalidOperationException("Foliage silhouette edge policy was weakened or drifted.");

        RequireExactSet(contract.criticalDefectRisksReduced, CriticalRisks, "foliage silhouette critical-defect risks");
        ValidateCriticalIdsExistInGate(CriticalRisks);

        ScoreTarget[] scoreTargets = contract.scoreTargets;
        if (scoreTargets == null || scoreTargets.Length != 3 || scoreTargets.Any(x => x == null || x.autoPoints != 0))
            throw new InvalidOperationException("Foliage silhouette score targets must remain three zero-auto-point implementation targets.");
        var expectedWeights = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "vegetation_natural_complexity", 8 },
            { "lighting_shadows_reflections", 15 },
            { "temporal_lod_aliasing", 5 },
        };
        if (!new HashSet<string>(scoreTargets.Select(x => x.categoryId), StringComparer.Ordinal).SetEquals(expectedWeights.Keys))
            throw new InvalidOperationException("Foliage silhouette score-target categories drifted.");
        foreach (ScoreTarget target in scoreTargets)
        {
            if (!expectedWeights.TryGetValue(target.categoryId, out int expectedWeight) || target.weight != expectedWeight ||
                string.IsNullOrWhiteSpace(target.potentialImpact))
                throw new InvalidOperationException("Foliage silhouette score-target weight/impact metadata drifted.");
        }

        FormalEvidence formal = contract.formalEvidenceRequirements;
        if (formal == null || formal.nativeResolution == null || formal.nativeResolution.Length != 2 ||
            formal.nativeResolution[0] != Width || formal.nativeResolution[1] != Height ||
            !formal.humanReviewRequired || !formal.actualRenderRequiredForVisualPoints ||
            !string.Equals(formal.requiredTemporalTarget, "temporal/subpixel_grazing", StringComparison.Ordinal))
            throw new InvalidOperationException("Foliage silhouette formal evidence requirements were weakened.");
        RequireExactSet(formal.requiredViews, RequiredViews, "foliage silhouette required views");
        RequireExactSet(formal.required100PercentCrops, RequiredCrops, "foliage silhouette required crops");
        if (formal.reviewFor == null || formal.reviewFor.Length < 7 || formal.reviewFor.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Foliage silhouette rendered-review checklist is incomplete.");

        Lookdev lookdev = contract.lookdev;
        if (lookdev == null || string.IsNullOrWhiteSpace(lookdev.illustration) || lookdev.isRenderEvidence ||
            !File.Exists(ToAbsolutePath(lookdev.illustration)))
            throw new InvalidOperationException("Foliage silhouette lookdev illustration is missing or incorrectly marked as render evidence.");

        if (contract.hardFailRules == null || contract.hardFailRules.Length < 10 || contract.hardFailRules.Any(string.IsNullOrWhiteSpace) ||
            contract.limitations == null || contract.limitations.Length < 4 || contract.limitations.Any(string.IsNullOrWhiteSpace) ||
            contract.implementationReadinessScore != 93 || contract.runtimeRenderVerified || contract.autoVisualPoints != 0 ||
            !string.Equals(contract.visualFidelityStatus, "UNSCORED_UNTIL_REAL_4K_RENDER", StringComparison.Ordinal))
            throw new InvalidOperationException("Foliage silhouette readiness/limitations may not claim rendered completion or Visual Fidelity points.");

        string shaderSource = File.ReadAllText(ToAbsolutePath(ShaderPath));
        foreach (string token in RequiredShaderTokens)
        {
            if (shaderSource.IndexOf(token, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("Foliage shader no longer contains required silhouette/shadow coherence token: " + token);
        }
        if (shaderSource.IndexOf("rawLeafUv : TEXCOORD5", StringComparison.Ordinal) < 0 ||
            shaderSource.IndexOf("o.uv = TRANSFORM_TEX(v.uv, _MainTex);", StringComparison.Ordinal) < 0)
            throw new InvalidOperationException("Foliage shader must keep canonical silhouette UV separate from tiled PBR UV.");
    }

    [MenuItem("NewTown/QA/Validate Foliage Silhouette Optics Formal State")]
    public static void ValidateFormalState()
    {
        ValidateContractConfigOnly();
        ValidateSceneState();
        Debug.Log("Foliage silhouette optics source state passed. Native 4K/temporal pixels remain mandatory before any visual points or PASS claim.");
    }

    public static void ValidateSceneState()
    {
        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
            throw new InvalidOperationException("Required foliage shader was not found/compiled: " + ShaderName);

        QualityBlockArtSlot[] slots = Resources.FindObjectsOfTypeAll<QualityBlockArtSlot>()
            .Where(x => x.gameObject.scene.IsValid() && x.SlotId.StartsWith("vegetation.tree.", StringComparison.Ordinal))
            .OrderBy(x => x.SlotId, StringComparer.Ordinal)
            .ToArray();
        if (slots.Length != 6)
            throw new InvalidOperationException($"Expected six tree art slots during foliage silhouette QA, got {slots.Length}.");

        int validatedTrees = 0;
        foreach (QualityBlockArtSlot slot in slots)
        {
            if (slot.IsUsingAuthoredArt)
                continue;

            int treeIndex = ParseTreeIndex(slot.SlotId);
            Transform master = slot.FallbackRoot != null ? slot.FallbackRoot.transform.Find(MasterPrefix + treeIndex) : null;
            if (master == null)
                throw new InvalidOperationException($"Tree {treeIndex} detailed master missing during foliage silhouette QA.");

            MeshRenderer[] leaves = master.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r => r.gameObject.name.Contains("LeafCluster_"))
                .ToArray();
            if (leaves.Length < 24)
                throw new InvalidOperationException($"Tree {treeIndex} has too few explicit leaf renderers for formal silhouette QA: {leaves.Length}.");

            var variationBins = new HashSet<int>();
            foreach (MeshRenderer renderer in leaves)
            {
                Material material = renderer.sharedMaterial;
                if (material == null || material.shader != shader)
                    throw new InvalidOperationException($"Tree {treeIndex} leaf {renderer.name} does not use {ShaderName}.");
                if (!material.HasProperty(LeafEdgeInsetId) || !material.HasProperty(LeafSerrationId) || !material.HasProperty(LeafEdgeAAScaleId))
                    throw new InvalidOperationException($"Tree {treeIndex} leaf material {material.name} lacks canonical margin properties.");

                float inset = material.GetFloat(LeafEdgeInsetId);
                float serration = material.GetFloat(LeafSerrationId);
                float aaScale = material.GetFloat(LeafEdgeAAScaleId);
                if (!FiniteInRange(inset, 0.040f, 0.080f) || !FiniteInRange(serration, 0.010f, 0.030f) ||
                    !FiniteInRange(aaScale, 1.0f, 1.8f))
                    throw new InvalidOperationException($"Tree {treeIndex} leaf material {material.name} has non-canonical margin values inset/serration/AA={inset:F4}/{serration:F4}/{aaScale:F3}.");

                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                float variation = block.GetFloat(LeafVariationId);
                if (!FiniteInRange(variation, -0.20f, 0.20f))
                    throw new InvalidOperationException($"Tree {treeIndex} leaf {renderer.name} has invalid deterministic edge phase source {variation:F4}.");
                variationBins.Add(Mathf.RoundToInt(variation * 10000f));
            }

            if (variationBins.Count < 4)
                throw new InvalidOperationException($"Tree {treeIndex} exposes only {variationBins.Count} foliage variation phases; canopy edge repetition risk is structurally under-controlled.");
            validatedTrees++;
        }

        if (validatedTrees == 0)
            Debug.Log("All tree slots use authored replacements; generated foliage silhouette QA was not applicable.");
    }

    private static void OnCameraPreCull(Camera camera)
    {
        if (!IsFormalEvidenceCamera(camera))
            return;
        if (validating)
            throw new InvalidOperationException("Foliage silhouette QA re-entered during formal pre-cull.");

        validating = true;
        try
        {
            ValidateContractConfigOnly();
            ValidateSceneState();
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

    private static int ParseTreeIndex(string slotId)
    {
        int dot = slotId.LastIndexOf('.');
        if (dot < 0 || !int.TryParse(slotId.Substring(dot + 1), out int value))
            throw new InvalidOperationException("Invalid tree slot id: " + slotId);
        return value;
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
                throw new InvalidOperationException("Foliage silhouette contract references non-canonical Visual Fidelity critical defect id: " + id);
        }
    }

    private static void RequireExactSet(string[] actual, string[] expected, string label)
    {
        if (actual == null || actual.Any(string.IsNullOrWhiteSpace) ||
            actual.Distinct(StringComparer.Ordinal).Count() != actual.Length ||
            !new HashSet<string>(actual, StringComparer.Ordinal).SetEquals(expected ?? Array.Empty<string>()))
            throw new InvalidOperationException(label + " must be exactly [" + string.Join(", ", expected ?? Array.Empty<string>()) + "].");
    }

    private static bool FiniteInRange(float value, float min, float max)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value >= min && value <= max;
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
        public string shaderPath;
        public AssemblyReasoning assemblyReasoning;
        public EdgeModel edgeModel;
        public string[] criticalDefectRisksReduced;
        public ScoreTarget[] scoreTargets;
        public FormalEvidence formalEvidenceRequirements;
        public Lookdev lookdev;
        public string[] hardFailRules;
        public int implementationReadinessScore;
        public string visualFidelityStatus;
        public bool runtimeRenderVerified;
        public int autoVisualPoints;
        public string[] limitations;
    }

    [Serializable]
    private sealed class AssemblyReasoning
    {
        public string component;
        public string constructionModel;
        public string orientation;
        public string interfaces;
        public string geometryVsMaterial;
        public string aging;
    }

    [Serializable]
    private sealed class EdgeModel
    {
        public float leafEdgeInset;
        public float leafSerration;
        public float leafEdgeAAScale;
        public float forwardClipThreshold;
        public float shadowClipThreshold;
        public bool alphaToCoverageRequired;
        public bool rawCanonicalUvRequired;
        public bool tiledPbrUvMustNotDriveMacroSilhouette;
        public bool clusterVariationMayOnlyPhaseMarginIrregularity;
        public bool runtimeRandomnessForbidden;
    }

    [Serializable]
    private sealed class ScoreTarget
    {
        public string categoryId;
        public int weight;
        public string potentialImpact;
        public int autoPoints;
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
