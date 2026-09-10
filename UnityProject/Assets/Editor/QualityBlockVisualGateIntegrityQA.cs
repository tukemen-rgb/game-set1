using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Guards the non-negotiable shape of the 100-point Visual Fidelity Gate.
///
/// The primary gate already checks that weights sum to 100, but a merely balanced configuration can
/// still be weakened by moving points between categories, lowering hard minima, or replacing a critical
/// defect. This QA freezes the exact owner-approved threshold, per-category weights/minima, required
/// native-4K views and critical-defect IDs. It awards zero Visual Fidelity points.
/// </summary>
public static class QualityBlockVisualGateIntegrityQA
{
    private const string GatePath = "Assets/QA/visual_fidelity_gate.json";
    private const string IntegrityPath = "Assets/QA/visual_gate_integrity_contract.json";

    private const int CanonicalThreshold = 92;
    private const int CanonicalWidth = 3840;
    private const int CanonicalHeight = 2160;

    private static readonly string[] CanonicalViews =
    {
        "hero",
        "oblique",
        "grazing"
    };

    private static readonly CanonicalCategory[] CanonicalCategories =
    {
        new CanonicalCategory("geometry_construction", 20, 18),
        new CanonicalCategory("material_pbr", 20, 18),
        new CanonicalCategory("lighting_shadows_reflections", 15, 13),
        new CanonicalCategory("texture_microdetail", 10, 9),
        new CanonicalCategory("weathering_causality", 10, 9),
        new CanonicalCategory("vegetation_natural_complexity", 8, 7),
        new CanonicalCategory("period_authenticity", 7, 6),
        new CanonicalCategory("cinematic_image", 5, 4),
        new CanonicalCategory("temporal_lod_aliasing", 5, 4)
    };

    private static readonly string[] CanonicalCriticalDefectIds =
    {
        "visible_primitive_placeholder",
        "baked_or_painted_highlights",
        "impossible_material_physics",
        "obvious_repetition",
        "hero_geometry_intersection",
        "sun_shadow_inconsistency",
        "forbidden_disaster_theme",
        "severe_aliasing_or_shimmer",
        "visible_lod_pop",
        "major_light_leak",
        "missing_construction_material_metadata",
        "unverified_render_claim"
    };

    [MenuItem("NewTown/QA/Validate Visual Fidelity Gate Integrity")]
    public static void ValidateContract()
    {
        GateConfig gate = LoadJson<GateConfig>(GatePath);
        IntegrityContract integrity = LoadJson<IntegrityContract>(IntegrityPath);

        ValidateCanonicalGate(gate, "visual_fidelity_gate.json");
        ValidateCanonicalIntegrityContract(integrity);
        ValidateCrossFileAgreement(gate, integrity);
        QualityBlockReviewedVisualEvidenceIntegrityQA.ValidateContractConfigOnly();

        Debug.Log(
            "Visual Fidelity Gate integrity valid: exact 92/100 threshold, immutable per-category weights/minima, " +
            "exact 12 critical defects, native 3840x2160 hero/oblique/grazing evidence, 100% crops, and reviewed-evidence " +
            "schema hardening are preserved. This is source-side gate integrity only and awards 0 Visual Fidelity points.");
    }

    // The legacy unbound evaluator already has a validator in QualityBlockRenderEvidenceProvenanceQA
    // that intentionally disables it. Guard the evidence-bound evaluator instead, avoiding duplicate
    // MenuItem validators while ensuring normal scoring UI cannot run with a relaxed gate. The Native
    // 4K packet also calls ValidateContract directly, so -executeMethod on that packet cannot bypass it.
    [MenuItem("NewTown/QA/Evaluate Evidence-Bound 4K Visual Fidelity Gate", true)]
    private static bool ValidateEvidenceBoundVisualGateMenu()
    {
        try
        {
            ValidateContract();
            QualityBlockReviewedVisualEvidenceIntegrityQA.ValidateReviewedEvidence();
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"Evaluate Evidence-Bound 4K Visual Fidelity Gate disabled because gate/review integrity failed: {ex.Message}");
            return false;
        }
    }

    [MenuItem("NewTown/QA/Prepare Complete Native 4K Review Packet", true)]
    private static bool ValidateNative4KPacketMenu()
    {
        return ValidateForMenu("Prepare Complete Native 4K Review Packet");
    }

    private static bool ValidateForMenu(string operation)
    {
        try
        {
            ValidateContract();
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"{operation} disabled because the non-negotiable Visual Fidelity Gate was altered: {ex.Message}");
            return false;
        }
    }

    private static void ValidateCanonicalGate(GateConfig gate, string label)
    {
        if (gate == null)
            throw new InvalidOperationException($"{label} is null.");
        if (gate.visualPassThreshold != CanonicalThreshold)
            throw new InvalidOperationException(
                $"{label} threshold drift: expected {CanonicalThreshold}, got {gate.visualPassThreshold}.");
        if (gate.requiredRender == null)
            throw new InvalidOperationException($"{label} requiredRender is missing.");
        if (gate.requiredRender.width != CanonicalWidth || gate.requiredRender.height != CanonicalHeight)
            throw new InvalidOperationException(
                $"{label} native render requirement drift: expected {CanonicalWidth}x{CanonicalHeight}, " +
                $"got {gate.requiredRender.width}x{gate.requiredRender.height}.");
        if (!gate.requiredRender.require100PercentCrops)
            throw new InvalidOperationException($"{label} must require pixel-exact 100% crops.");
        RequireExactSet(gate.requiredRender.requiredViews, CanonicalViews, $"{label} requiredViews");

        ValidateCategoryArray(gate.categories, $"{label} categories");

        string[] criticalIds = gate.criticalDefects == null
            ? null
            : gate.criticalDefects.Select(x => x == null ? null : x.id).ToArray();
        RequireExactSet(criticalIds, CanonicalCriticalDefectIds, $"{label} criticalDefects");

        if (gate.criticalDefects.Any(x => x == null || string.IsNullOrWhiteSpace(x.description)))
            throw new InvalidOperationException($"{label} contains a critical defect with missing description.");
    }

    private static void ValidateCanonicalIntegrityContract(IntegrityContract integrity)
    {
        if (integrity == null)
            throw new InvalidOperationException("visual_gate_integrity_contract.json is null.");
        if (integrity.schemaVersion != "1.0")
            throw new InvalidOperationException(
                $"Unexpected visual gate integrity schemaVersion '{integrity.schemaVersion}'. Expected 1.0.");
        if (integrity.visualPassThreshold != CanonicalThreshold)
            throw new InvalidOperationException(
                $"Integrity contract threshold drift: expected {CanonicalThreshold}, got {integrity.visualPassThreshold}.");
        if (integrity.requiredRender == null ||
            integrity.requiredRender.width != CanonicalWidth ||
            integrity.requiredRender.height != CanonicalHeight ||
            !integrity.requiredRender.require100PercentCrops)
            throw new InvalidOperationException(
                "Integrity contract must require native 3840x2160 renders plus pixel-exact 100% crops.");
        RequireExactSet(integrity.requiredRender.requiredViews, CanonicalViews, "integrity contract requiredViews");
        ValidateCategoryArray(integrity.categories, "integrity contract categories");
        RequireExactSet(integrity.criticalDefectIds, CanonicalCriticalDefectIds, "integrity contract criticalDefectIds");

        if (integrity.hardRules == null || integrity.hardRules.Length < 6 ||
            integrity.hardRules.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Integrity contract hardRules are missing or incomplete.");
    }

    private static void ValidateCrossFileAgreement(GateConfig gate, IntegrityContract integrity)
    {
        if (gate.visualPassThreshold != integrity.visualPassThreshold)
            throw new InvalidOperationException("Gate and integrity-contract thresholds disagree.");

        if (gate.requiredRender.width != integrity.requiredRender.width ||
            gate.requiredRender.height != integrity.requiredRender.height ||
            gate.requiredRender.require100PercentCrops != integrity.requiredRender.require100PercentCrops)
            throw new InvalidOperationException("Gate and integrity-contract native-render requirements disagree.");

        RequireExactSet(gate.requiredRender.requiredViews, integrity.requiredRender.requiredViews,
            "gate/integrity requiredViews agreement");

        foreach (CanonicalCategory canonical in CanonicalCategories)
        {
            CategoryRule gateRule = gate.categories.Single(x => x.id == canonical.id);
            CategoryRule contractRule = integrity.categories.Single(x => x.id == canonical.id);
            if (gateRule.weight != contractRule.weight || gateRule.hardMinimum != contractRule.hardMinimum)
                throw new InvalidOperationException(
                    $"Gate/integrity disagreement for {canonical.id}: " +
                    $"gate={gateRule.weight}/{gateRule.hardMinimum}, " +
                    $"contract={contractRule.weight}/{contractRule.hardMinimum}.");
        }

        RequireExactSet(
            gate.criticalDefects.Select(x => x.id).ToArray(),
            integrity.criticalDefectIds,
            "gate/integrity critical-defect agreement");
    }

    private static void ValidateCategoryArray(CategoryRule[] categories, string label)
    {
        if (categories == null || categories.Length != CanonicalCategories.Length)
            throw new InvalidOperationException(
                $"{label} must contain exactly {CanonicalCategories.Length} categories.");
        if (categories.Any(x => x == null || string.IsNullOrWhiteSpace(x.id)))
            throw new InvalidOperationException($"{label} contains a null/unnamed category.");
        if (categories.Select(x => x.id).Distinct(StringComparer.Ordinal).Count() != categories.Length)
            throw new InvalidOperationException($"{label} contains duplicate category IDs.");

        foreach (CanonicalCategory canonical in CanonicalCategories)
        {
            CategoryRule rule = categories.SingleOrDefault(x => x.id == canonical.id);
            if (rule == null)
                throw new InvalidOperationException($"{label} is missing '{canonical.id}'.");
            if (rule.weight != canonical.weight || rule.hardMinimum != canonical.hardMinimum)
                throw new InvalidOperationException(
                    $"{label} drift for {canonical.id}: expected weight/minimum " +
                    $"{canonical.weight}/{canonical.hardMinimum}, got {rule.weight}/{rule.hardMinimum}.");
            if (rule.hardMinimum < 0 || rule.hardMinimum > rule.weight)
                throw new InvalidOperationException(
                    $"{label} has invalid bounds for {canonical.id}: minimum={rule.hardMinimum}, weight={rule.weight}.");
        }

        int total = categories.Sum(x => x.weight);
        if (total != 100)
            throw new InvalidOperationException($"{label} weights must sum to exactly 100, got {total}.");
    }

    private static void RequireExactSet(string[] actual, string[] expected, string label)
    {
        if (actual == null)
            throw new InvalidOperationException($"{label} is missing.");
        if (actual.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException($"{label} contains a blank value.");
        if (actual.Distinct(StringComparer.Ordinal).Count() != actual.Length)
            throw new InvalidOperationException($"{label} contains duplicates.");
        if (!new HashSet<string>(actual, StringComparer.Ordinal)
            .SetEquals(expected ?? Array.Empty<string>()))
            throw new InvalidOperationException(
                $"{label} must be exactly [{string.Join(", ", expected ?? Array.Empty<string>())}], " +
                $"got [{string.Join(", ", actual)}].");
    }

    private static T LoadJson<T>(string assetPath)
    {
        string absolute = AbsolutePath(assetPath);
        if (!File.Exists(absolute))
            throw new FileNotFoundException($"Required QA file not found: {assetPath}");

        T value = JsonUtility.FromJson<T>(File.ReadAllText(absolute));
        if (value == null)
            throw new InvalidOperationException($"Could not parse QA JSON: {assetPath}");
        return value;
    }

    private static string AbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private readonly struct CanonicalCategory
    {
        public readonly string id;
        public readonly int weight;
        public readonly int hardMinimum;

        public CanonicalCategory(string id, int weight, int hardMinimum)
        {
            this.id = id;
            this.weight = weight;
            this.hardMinimum = hardMinimum;
        }
    }

    [Serializable]
    private sealed class GateConfig
    {
        public int visualPassThreshold;
        public RenderRequirement requiredRender;
        public CategoryRule[] categories;
        public CriticalDefectRule[] criticalDefects;
    }

    [Serializable]
    private sealed class IntegrityContract
    {
        public string schemaVersion;
        public int visualPassThreshold;
        public RenderRequirement requiredRender;
        public CategoryRule[] categories;
        public string[] criticalDefectIds;
        public string[] hardRules;
    }

    [Serializable]
    private sealed class RenderRequirement
    {
        public int width;
        public int height;
        public bool require100PercentCrops;
        public string[] requiredViews;
    }

    [Serializable]
    private sealed class CategoryRule
    {
        public string id;
        public int weight;
        public int hardMinimum;
    }

    [Serializable]
    private sealed class CriticalDefectRule
    {
        public string id;
        public string description;
    }
}
