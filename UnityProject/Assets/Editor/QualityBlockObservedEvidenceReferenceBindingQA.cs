using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Fail-closed identity binding between the vocabulary used by human Visual Fidelity reviews and the
/// canonical still/temporal evidence that the Unity capture pipeline actually produces.
///
/// This validator awards no visual points. It prevents configuration drift where an observability
/// contract could accept plausible-looking references after the canonical capture manifest changed,
/// or where a crop id could silently point at a differently named file. Actual pixel quality still
/// requires sealed Unity renders and direct review.
/// </summary>
public static class QualityBlockObservedEvidenceReferenceBindingQA
{
    private const string BindingContractPath = "Assets/QA/observed_evidence_reference_binding_contract.json";
    private const string ObservabilityPath = "Assets/QA/visual_evidence_observability_contract.json";
    private const string ManifestPath = "Assets/QA/4k_capture_manifest.json";
    private const string TemporalContractPath = "Assets/QA/temporal_stability_contract.json";

    [MenuItem("NewTown/QA/Validate Observed Evidence Reference Binding Config")]
    public static void ValidateContractConfigOnly()
    {
        BindingContract binding = LoadJson<BindingContract>(BindingContractPath);
        ObservabilityContract observability = LoadJson<ObservabilityContract>(ObservabilityPath);
        CaptureManifest manifest = LoadJson<CaptureManifest>(ManifestPath);
        TemporalContract temporal = LoadJson<TemporalContract>(TemporalContractPath);

        ValidateBindingContract(binding);
        ValidateStillIdentityBinding(observability, manifest);
        ValidateTemporalIdentityBinding(observability, temporal);
        ValidateRequirementReferences(observability);

        Debug.Log(
            "Observed evidence reference binding VALID: every allowed still view/crop resolves exactly to the canonical 4K manifest, " +
            "every temporal reference resolves to a canonical probe, and all coverage requirements remain inside those bound sets. " +
            "No Visual Fidelity points were awarded.");
    }

    /// <summary>
    /// Scoring-time convenience variant. The core Visual Fidelity evaluator already performs sealed
    /// byte provenance independently; this method additionally rejects the source template state.
    /// </summary>
    public static void ValidateForScoring()
    {
        ValidateContractConfigOnly();
        CaptureManifest manifest = LoadJson<CaptureManifest>(ManifestPath);
        Require(manifest.renderProducedByUnity,
            "Observed evidence reference binding cannot be used for scoring while 4k_capture_manifest.json is still an unrendered template.");
        Require(string.Equals(manifest.visualFidelityStatus, "UNSCORED_REVIEW_REQUIRED", StringComparison.Ordinal),
            "Runtime 4K manifest must remain UNSCORED_REVIEW_REQUIRED before the numeric Visual Fidelity Gate runs.");
    }

    private static void ValidateBindingContract(BindingContract contract)
    {
        Require(contract != null, "Observed evidence reference binding contract is null.");
        Require(string.Equals(contract.schemaVersion, "1.0", StringComparison.Ordinal),
            $"Observed evidence reference binding schema must remain 1.0, got '{contract.schemaVersion}'.");
        Require(string.Equals(contract.status, "PENDING_REAL_UNITY_4K_RENDER", StringComparison.Ordinal),
            "Observed evidence reference binding contract must remain render-pending until real Unity evidence exists.");
        Require(contract.bindingRules != null, "Observed evidence reference binding contract is missing bindingRules.");
        Require(contract.bindingRules.requireExactViewSetEquality,
            "Binding policy was weakened: exact still-view set equality must be required.");
        Require(contract.bindingRules.requireExactCropReferenceSetEquality,
            "Binding policy was weakened: exact crop-reference set equality must be required.");
        Require(contract.bindingRules.requireCanonicalCropPathDerivedFromViewAndCropId,
            "Binding policy was weakened: canonical crop path derivation must be required.");
        Require(contract.bindingRules.requireExactTemporalProbeSetEquality,
            "Binding policy was weakened: exact temporal-probe set equality must be required.");
        Require(contract.bindingRules.rejectDuplicateReferences && contract.bindingRules.rejectOrphanRequirementReferences,
            "Binding policy was weakened: duplicate and orphan references must be rejected.");
        Require(contract.verification != null, "Observed evidence reference binding contract is missing verification.");
        Require(contract.verification.visualFidelityPointsAwarded == 0,
            "Reference binding QA must never award Visual Fidelity points.");
        Require(!contract.verification.native4kRenderVerified,
            "Source-side reference binding contract must not claim native 4K render verification.");
    }

    private static void ValidateStillIdentityBinding(ObservabilityContract observability, CaptureManifest manifest)
    {
        Require(observability != null, "Visual evidence observability contract is null.");
        Require(manifest != null, "4K capture manifest is null.");
        Require(observability.allowedViews != null, "Observability contract is missing allowedViews.");
        Require(observability.allowedCropRefs != null, "Observability contract is missing allowedCropRefs.");
        Require(manifest.captures != null, "4K capture manifest is missing captures.");

        RequireNoDuplicates(observability.allowedViews, "observability allowedViews");
        string[] manifestViews = manifest.captures.Select(RequireCaptureAndReturnViewId).ToArray();
        RequireNoDuplicates(manifestViews, "manifest capture viewId");
        Require(SetEquals(observability.allowedViews, manifestViews),
            "Observability allowedViews are not exactly the canonical 4K manifest view set.");

        var manifestCropRefs = new List<string>();
        var manifestCropPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (CaptureRecord capture in manifest.captures)
        {
            Require(capture.cropRecords != null && capture.cropRecords.Length > 0,
                $"Canonical manifest view '{capture.viewId}' has no crop records.");

            var cropIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (CropRecord crop in capture.cropRecords)
            {
                Require(crop != null, $"Canonical manifest view '{capture.viewId}' contains a null crop record.");
                Require(!string.IsNullOrWhiteSpace(crop.id),
                    $"Canonical manifest view '{capture.viewId}' contains a crop with no id.");
                Require(cropIds.Add(crop.id),
                    $"Canonical manifest view '{capture.viewId}' contains duplicate crop id '{crop.id}'.");
                Require(crop.width > 0 && crop.height > 0,
                    $"Canonical crop '{capture.viewId}/{crop.id}' has invalid dimensions {crop.width}x{crop.height}.");

                string cropRef = capture.viewId + "/" + crop.id;
                manifestCropRefs.Add(cropRef);

                string expectedPath =
                    $"Assets/QA/Captures4K/{capture.viewId}_crop_{crop.id}_{crop.width}x{crop.height}_100pct.png";
                Require(string.Equals(crop.assetPath, expectedPath, StringComparison.Ordinal),
                    $"Canonical crop identity/path mismatch for '{cropRef}': expected '{expectedPath}', got '{crop.assetPath}'.");
                Require(manifestCropPaths.Add(crop.assetPath),
                    $"Canonical manifest reuses crop path '{crop.assetPath}'.");
                Require(!crop.resampled,
                    $"Canonical crop '{cropRef}' is marked resampled=true and cannot be a 100% review reference.");
            }
        }

        RequireNoDuplicates(observability.allowedCropRefs, "observability allowedCropRefs");
        RequireNoDuplicates(manifestCropRefs.ToArray(), "manifest viewId/cropId references");
        Require(SetEquals(observability.allowedCropRefs, manifestCropRefs.ToArray()),
            "Observability allowedCropRefs are not exactly the canonical 4K manifest viewId/cropId set.");
    }

    private static void ValidateTemporalIdentityBinding(ObservabilityContract observability, TemporalContract temporal)
    {
        Require(observability.allowedTemporalRefs != null,
            "Observability contract is missing allowedTemporalRefs.");
        Require(temporal != null && temporal.probes != null,
            "Temporal stability contract is missing probes.");

        string[] probeIds = temporal.probes.Select(x =>
        {
            Require(x != null && !string.IsNullOrWhiteSpace(x.id),
                "Temporal stability contract contains a probe with no id.");
            return x.id;
        }).ToArray();

        RequireNoDuplicates(observability.allowedTemporalRefs, "observability allowedTemporalRefs");
        RequireNoDuplicates(probeIds, "temporal probe ids");
        Require(SetEquals(observability.allowedTemporalRefs, probeIds),
            "Observability allowedTemporalRefs are not exactly the canonical temporal-stability probe id set.");

        Require(temporal.requiredProbeRefsForTemporalCategory != null,
            "Temporal stability contract is missing requiredProbeRefsForTemporalCategory.");
        RequireNoDuplicates(temporal.requiredProbeRefsForTemporalCategory,
            "temporal requiredProbeRefsForTemporalCategory");
        Require(SetEquals(temporal.requiredProbeRefsForTemporalCategory, probeIds),
            "Temporal category-required probe refs must remain the complete canonical temporal probe set.");
    }

    private static void ValidateRequirementReferences(ObservabilityContract observability)
    {
        var viewSet = new HashSet<string>(observability.allowedViews ?? new string[0], StringComparer.Ordinal);
        var cropSet = new HashSet<string>(observability.allowedCropRefs ?? new string[0], StringComparer.Ordinal);
        var temporalSet = new HashSet<string>(observability.allowedTemporalRefs ?? new string[0], StringComparer.Ordinal);

        ValidateRequirements(observability.categoryRequirements, "category", viewSet, cropSet, temporalSet);
        ValidateRequirements(observability.criticalDefectRequirements, "critical defect", viewSet, cropSet, temporalSet);
    }

    private static void ValidateRequirements(Requirement[] requirements, string label,
        HashSet<string> viewSet, HashSet<string> cropSet, HashSet<string> temporalSet)
    {
        Require(requirements != null && requirements.Length > 0,
            $"Observability contract is missing {label} requirements.");
        RequireNoDuplicates(requirements.Select(x => x != null ? x.id : null).ToArray(), $"{label} requirement ids");

        foreach (Requirement requirement in requirements)
        {
            Require(requirement != null && !string.IsNullOrWhiteSpace(requirement.id),
                $"Observability contract contains a {label} requirement with no id.");
            ValidateSubset(requirement.requiredViews, viewSet, $"{label} '{requirement.id}' requiredViews");
            ValidateSubset(requirement.requiredCropRefs, cropSet, $"{label} '{requirement.id}' requiredCropRefs");
            ValidateSubset(requirement.requiredTemporalRefs, temporalSet, $"{label} '{requirement.id}' requiredTemporalRefs");
        }
    }

    private static void ValidateSubset(string[] values, HashSet<string> allowed, string label)
    {
        string[] items = values ?? new string[0];
        RequireNoDuplicates(items, label);
        foreach (string item in items)
            Require(!string.IsNullOrWhiteSpace(item) && allowed.Contains(item),
                $"{label} contains orphan or unknown reference '{item}'.");
    }

    private static string RequireCaptureAndReturnViewId(CaptureRecord capture)
    {
        Require(capture != null && !string.IsNullOrWhiteSpace(capture.viewId),
            "4K capture manifest contains a capture with no viewId.");
        return capture.viewId;
    }

    private static bool SetEquals(string[] a, string[] b)
    {
        return new HashSet<string>(a ?? new string[0], StringComparer.Ordinal).SetEquals(b ?? new string[0]);
    }

    private static void RequireNoDuplicates(string[] values, string label)
    {
        string[] items = values ?? new string[0];
        if (items.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException($"{label} contains an empty reference.");
        if (items.Distinct(StringComparer.Ordinal).Count() != items.Length)
            throw new InvalidOperationException($"{label} contains duplicate references.");
    }

    private static T LoadJson<T>(string assetPath) where T : class
    {
        string absolute = AbsolutePath(assetPath);
        Require(File.Exists(absolute), $"Required QA file is missing: {assetPath}");
        T data = JsonUtility.FromJson<T>(File.ReadAllText(absolute));
        Require(data != null, $"Could not parse QA file: {assetPath}");
        return data;
    }

    private static string AbsolutePath(string assetPath)
    {
        return Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    [Serializable]
    private sealed class BindingContract
    {
        public string schemaVersion;
        public string status;
        public BindingRules bindingRules;
        public Verification verification;
    }

    [Serializable]
    private sealed class BindingRules
    {
        public bool requireExactViewSetEquality;
        public bool requireExactCropReferenceSetEquality;
        public bool requireCanonicalCropPathDerivedFromViewAndCropId;
        public bool requireExactTemporalProbeSetEquality;
        public bool rejectDuplicateReferences;
        public bool rejectOrphanRequirementReferences;
    }

    [Serializable]
    private sealed class Verification
    {
        public bool native4kRenderVerified;
        public int visualFidelityPointsAwarded;
    }

    [Serializable]
    private sealed class ObservabilityContract
    {
        public string schemaVersion;
        public string[] allowedViews;
        public string[] allowedCropRefs;
        public string[] allowedTemporalRefs;
        public Requirement[] categoryRequirements;
        public Requirement[] criticalDefectRequirements;
    }

    [Serializable]
    private sealed class Requirement
    {
        public string id;
        public string[] requiredViews;
        public string[] requiredCropRefs;
        public string[] requiredTemporalRefs;
    }

    [Serializable]
    private sealed class CaptureManifest
    {
        public bool renderProducedByUnity;
        public string visualFidelityStatus;
        public CaptureRecord[] captures;
    }

    [Serializable]
    private sealed class CaptureRecord
    {
        public string viewId;
        public string assetPath;
        public CropRecord[] cropRecords;
    }

    [Serializable]
    private sealed class CropRecord
    {
        public string id;
        public string assetPath;
        public int width;
        public int height;
        public bool resampled;
    }

    [Serializable]
    private sealed class TemporalContract
    {
        public TemporalProbe[] probes;
        public string[] requiredProbeRefsForTemporalCategory;
    }

    [Serializable]
    private sealed class TemporalProbe
    {
        public string id;
    }
}