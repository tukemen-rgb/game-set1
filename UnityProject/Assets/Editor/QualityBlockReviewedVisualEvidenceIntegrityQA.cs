using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Fail-closed integrity checks for the manually reviewed Visual Fidelity evidence document.
///
/// The numeric gate validates score ranges and observability, while render provenance binds the review
/// to exact Unity pixels. This layer closes a different ambiguity class: duplicated/unknown entries,
/// missing corrective actions, and incomplete review records that could otherwise be silently consumed
/// through FirstOrDefault-style lookup. It awards zero Visual Fidelity points.
/// </summary>
public static class QualityBlockReviewedVisualEvidenceIntegrityQA
{
    private const string ContractPath = "Assets/QA/reviewed_visual_evidence_integrity_contract.json";
    private const string EvidencePath = "Assets/QA/visual_fidelity_evidence.json";

    private static readonly string[] CanonicalViews =
    {
        "hero",
        "oblique",
        "grazing"
    };

    private static readonly CategorySpec[] CanonicalCategories =
    {
        new CategorySpec("geometry_construction", 20, 18),
        new CategorySpec("material_pbr", 20, 18),
        new CategorySpec("lighting_shadows_reflections", 15, 13),
        new CategorySpec("texture_microdetail", 10, 9),
        new CategorySpec("weathering_causality", 10, 9),
        new CategorySpec("vegetation_natural_complexity", 8, 7),
        new CategorySpec("period_authenticity", 7, 6),
        new CategorySpec("cinematic_image", 5, 4),
        new CategorySpec("temporal_lod_aliasing", 5, 4)
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

    [MenuItem("NewTown/QA/Validate Reviewed Visual Evidence Integrity")]
    public static void ValidateReviewedEvidence()
    {
        ValidateContractConfigOnly();

        if (!File.Exists(EvidencePath))
            throw new FileNotFoundException(
                "Reviewed visual evidence does not exist yet. Capture, seal and review real Unity 4K evidence first.",
                EvidencePath);

        QualityBlockVisualFidelityGate.VisualEvidence evidence =
            JsonUtility.FromJson<QualityBlockVisualFidelityGate.VisualEvidence>(File.ReadAllText(EvidencePath));
        if (evidence == null)
            throw new InvalidOperationException("visual_fidelity_evidence.json could not be parsed.");
        if (!evidence.renderVerified)
            throw new InvalidOperationException(
                "Reviewed Visual Fidelity evidence must have renderVerified=true only after actual Unity pixels were reviewed.");

        ValidateCaptureEntries(evidence.captures);
        ValidateCategoryEntries(evidence.categories);
        ValidateCriticalDefectEntries(evidence.criticalDefects);

        // Temporal/LOD/aliasing review is not scoreable from PNG existence alone. Require the
        // authoritative prepared sequence to prove one MainCamera pre-cull lighting fingerprint per
        // temporal frame and one filmic native-4K HDR->LDR invocation per frame with zero fallback.
        // The runtime receipt is SHA-256-bound to the temporal manifest/receipt, prepared scene binding,
        // persisted scene and accepted reflection completion/wait proofs. It still awards zero points.
        QualityBlockTemporalRuntimeEvidenceGuard.ValidateLatestReceiptForScoring();

        Debug.Log(
            "Reviewed Visual Fidelity evidence integrity valid: exact hero/oblique/grazing entries, exact nine categories, " +
            "exact twelve critical-defect reviews, no duplicates/unknown IDs, complete evidence/corrective-action text, " +
            "and a sealed per-frame temporal lighting + filmic HDR->LDR runtime receipt. " +
            "This QA awards 0 Visual Fidelity points; provenance and the numeric gate still decide scoring eligibility/PASS.");
    }

    public static void ValidateContractConfigOnly()
    {
        if (!File.Exists(ContractPath))
            throw new FileNotFoundException($"Reviewed-evidence integrity contract missing: {ContractPath}");

        IntegrityContract contract = JsonUtility.FromJson<IntegrityContract>(File.ReadAllText(ContractPath));
        if (contract == null)
            throw new InvalidOperationException("Reviewed-evidence integrity contract could not be parsed.");
        if (!string.Equals(contract.schemaVersion, "1.0", StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Unexpected reviewed-evidence integrity schemaVersion '{contract.schemaVersion}'. Expected 1.0.");
        if (contract.runtimeRenderVerified)
            throw new InvalidOperationException(
                "Reviewed-evidence integrity contract may not claim runtime render verification.");
        if (contract.visualScoreAwardedByThisQA)
            throw new InvalidOperationException(
                "Reviewed-evidence integrity QA is structural only and may not award Visual Fidelity points.");

        RequireExactSet(contract.requiredViews, CanonicalViews, "integrity contract requiredViews");
        ValidateContractCategories(contract.categories);
        RequireExactSet(contract.criticalDefectIds, CanonicalCriticalDefectIds,
            "integrity contract criticalDefectIds");

        IntegrityRules rules = contract.rules;
        if (rules == null ||
            !rules.rejectDuplicateViewEntries ||
            !rules.rejectDuplicateCategoryEntries ||
            !rules.rejectDuplicateCriticalDefectEntries ||
            !rules.rejectUnknownEntries ||
            !rules.requireExactlyNineCategoryEntries ||
            !rules.requireExactlyTwelveCriticalDefectEntries ||
            !rules.requireCategoryEvidenceText ||
            !rules.requireCategoryCorrectiveActionText ||
            !rules.requireDeductionTextWhenScoreBelowWeight ||
            !rules.requireCriticalDefectEvidenceText ||
            !rules.requireAtLeastOneObservedReferencePerCategory ||
            !rules.requireAtLeastOneObservedReferencePerCriticalDefect ||
            !rules.requireRenderVerifiedTrueForReviewedEvidence)
            throw new InvalidOperationException(
                "Reviewed-evidence integrity rules were weakened or are incomplete.");
    }

    private static void ValidateCaptureEntries(QualityBlockVisualFidelityGate.CaptureEvidence[] captures)
    {
        if (captures == null || captures.Length != CanonicalViews.Length)
            throw new InvalidOperationException(
                $"Reviewed evidence must contain exactly {CanonicalViews.Length} capture entries, got {captures?.Length ?? 0}.");

        string[] ids = captures.Select(x => x == null ? null : x.viewId).ToArray();
        RequireExactSet(ids, CanonicalViews, "reviewed evidence capture viewIds");

        foreach (QualityBlockVisualFidelityGate.CaptureEvidence capture in captures)
        {
            if (capture.width != 3840 || capture.height != 2160)
                throw new InvalidOperationException(
                    $"Reviewed capture '{capture.viewId}' is not native 3840x2160: {capture.width}x{capture.height}.");
            if (string.IsNullOrWhiteSpace(capture.assetPath))
                throw new InvalidOperationException($"Reviewed capture '{capture.viewId}' is missing assetPath.");
            if (capture.cropPaths == null || capture.cropPaths.Length == 0 ||
                capture.cropPaths.Any(string.IsNullOrWhiteSpace))
                throw new InvalidOperationException(
                    $"Reviewed capture '{capture.viewId}' must reference its real 100% crop assets.");
            if (capture.cropPaths.Distinct(StringComparer.Ordinal).Count() != capture.cropPaths.Length)
                throw new InvalidOperationException(
                    $"Reviewed capture '{capture.viewId}' contains duplicate crop paths.");
        }
    }

    private static void ValidateCategoryEntries(QualityBlockVisualFidelityGate.CategoryEvidence[] categories)
    {
        if (categories == null || categories.Length != CanonicalCategories.Length)
            throw new InvalidOperationException(
                $"Reviewed evidence must contain exactly {CanonicalCategories.Length} category entries, got {categories?.Length ?? 0}.");

        string[] ids = categories.Select(x => x == null ? null : x.id).ToArray();
        RequireExactSet(ids, CanonicalCategories.Select(x => x.id).ToArray(),
            "reviewed evidence category IDs");

        foreach (CategorySpec spec in CanonicalCategories)
        {
            QualityBlockVisualFidelityGate.CategoryEvidence category =
                categories.Single(x => string.Equals(x.id, spec.id, StringComparison.Ordinal));

            if (category.score < 0 || category.score > spec.weight)
                throw new InvalidOperationException(
                    $"Category '{spec.id}' score {category.score} is outside 0..{spec.weight}.");
            if (string.IsNullOrWhiteSpace(category.evidence))
                throw new InvalidOperationException($"Category '{spec.id}' is missing observed evidence text.");
            if (string.IsNullOrWhiteSpace(category.correctiveAction))
                throw new InvalidOperationException(
                    $"Category '{spec.id}' must record correctiveAction text; use an explicit no-action rationale when none is required.");
            if (category.score < spec.weight && string.IsNullOrWhiteSpace(category.deductions))
                throw new InvalidOperationException(
                    $"Category '{spec.id}' lost points but has no deduction rationale.");
            if (!HasObservedReference(category.observedViews, category.observedCropRefs, category.observedTemporalRefs))
                throw new InvalidOperationException(
                    $"Category '{spec.id}' has no observed view/crop/temporal reference and cannot be treated as pixel-observed.");
        }
    }

    private static void ValidateCriticalDefectEntries(QualityBlockVisualFidelityGate.CriticalDefectEvidence[] criticalDefects)
    {
        if (criticalDefects == null || criticalDefects.Length != CanonicalCriticalDefectIds.Length)
            throw new InvalidOperationException(
                $"Reviewed evidence must contain exactly {CanonicalCriticalDefectIds.Length} critical-defect reviews, " +
                $"got {criticalDefects?.Length ?? 0}.");

        string[] ids = criticalDefects.Select(x => x == null ? null : x.id).ToArray();
        RequireExactSet(ids, CanonicalCriticalDefectIds, "reviewed evidence critical-defect IDs");

        foreach (QualityBlockVisualFidelityGate.CriticalDefectEvidence defect in criticalDefects)
        {
            if (string.IsNullOrWhiteSpace(defect.evidence))
                throw new InvalidOperationException(
                    $"Critical defect '{defect.id}' must contain explicit observed evidence for present/absent review.");
            if (!HasObservedReference(defect.observedViews, defect.observedCropRefs, defect.observedTemporalRefs))
                throw new InvalidOperationException(
                    $"Critical defect '{defect.id}' has no observed view/crop/temporal reference.");
        }
    }

    private static bool HasObservedReference(string[] views, string[] crops, string[] temporal)
    {
        return HasNonBlank(views) || HasNonBlank(crops) || HasNonBlank(temporal);
    }

    private static bool HasNonBlank(string[] values)
    {
        return values != null && values.Any(x => !string.IsNullOrWhiteSpace(x));
    }

    private static void ValidateContractCategories(CategoryRule[] categories)
    {
        if (categories == null || categories.Length != CanonicalCategories.Length)
            throw new InvalidOperationException(
                $"Reviewed-evidence integrity contract must contain exactly {CanonicalCategories.Length} categories.");
        if (categories.Any(x => x == null || string.IsNullOrWhiteSpace(x.id)))
            throw new InvalidOperationException("Reviewed-evidence integrity contract has a null/unnamed category.");
        if (categories.Select(x => x.id).Distinct(StringComparer.Ordinal).Count() != categories.Length)
            throw new InvalidOperationException("Reviewed-evidence integrity contract has duplicate category IDs.");

        foreach (CategorySpec spec in CanonicalCategories)
        {
            CategoryRule rule = categories.SingleOrDefault(x => string.Equals(x.id, spec.id, StringComparison.Ordinal));
            if (rule == null || rule.weight != spec.weight || rule.hardMinimum != spec.hardMinimum)
                throw new InvalidOperationException(
                    $"Reviewed-evidence integrity category drift for {spec.id}; expected {spec.weight}/{spec.hardMinimum}.");
        }
    }

    private static void RequireExactSet(string[] actual, string[] expected, string label)
    {
        if (actual == null)
            throw new InvalidOperationException($"{label} is missing.");
        if (actual.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException($"{label} contains a blank value.");
        if (actual.Distinct(StringComparer.Ordinal).Count() != actual.Length)
            throw new InvalidOperationException($"{label} contains duplicates.");

        var actualSet = new HashSet<string>(actual, StringComparer.Ordinal);
        var expectedSet = new HashSet<string>(expected ?? Array.Empty<string>(), StringComparer.Ordinal);
        if (!actualSet.SetEquals(expectedSet))
            throw new InvalidOperationException(
                $"{label} must be exactly [{string.Join(", ", expectedSet)}], got [{string.Join(", ", actualSet)}].");
    }

    private readonly struct CategorySpec
    {
        public readonly string id;
        public readonly int weight;
        public readonly int hardMinimum;

        public CategorySpec(string id, int weight, int hardMinimum)
        {
            this.id = id;
            this.weight = weight;
            this.hardMinimum = hardMinimum;
        }
    }

    [Serializable]
    private sealed class IntegrityContract
    {
        public string schemaVersion;
        public string[] requiredViews;
        public CategoryRule[] categories;
        public string[] criticalDefectIds;
        public IntegrityRules rules;
        public bool visualScoreAwardedByThisQA;
        public bool runtimeRenderVerified;
    }

    [Serializable]
    private sealed class CategoryRule
    {
        public string id;
        public int weight;
        public int hardMinimum;
    }

    [Serializable]
    private sealed class IntegrityRules
    {
        public bool rejectDuplicateViewEntries;
        public bool rejectDuplicateCategoryEntries;
        public bool rejectDuplicateCriticalDefectEntries;
        public bool rejectUnknownEntries;
        public bool requireExactlyNineCategoryEntries;
        public bool requireExactlyTwelveCriticalDefectEntries;
        public bool requireCategoryEvidenceText;
        public bool requireCategoryCorrectiveActionText;
        public bool requireDeductionTextWhenScoreBelowWeight;
        public bool requireCriticalDefectEvidenceText;
        public bool requireAtLeastOneObservedReferencePerCategory;
        public bool requireAtLeastOneObservedReferencePerCriticalDefect;
        public bool requireRenderVerifiedTrueForReviewedEvidence;
    }
}
