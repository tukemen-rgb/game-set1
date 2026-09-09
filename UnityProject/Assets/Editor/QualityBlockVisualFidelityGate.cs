using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Numeric release gate for the 4K-film visual target.
/// IMPORTANT: this class never converts implementation coverage into a visual-fidelity score.
/// Visual PASS requires real 3840x2160 Unity still renders, pixel-exact 100% crops, sealed native-4K
/// temporal probes for shimmer/LOD review, provenance validation, explicit category scores tied to
/// observable evidence, and explicit review of every critical defect.
/// </summary>
public static class QualityBlockVisualFidelityGate
{
    private const string ConfigPath = "Assets/QA/visual_fidelity_gate.json";
    private const string ObservabilityPath = "Assets/QA/visual_evidence_observability_contract.json";
    private const string EvidencePath = "Assets/QA/visual_fidelity_evidence.json";
    private const string ResultPath = "Assets/QA/visual_fidelity_result.json";
    private const string ReadinessPath = "Assets/QA/implementation_readiness.json";

    private static readonly string[] RequiredTemporalRefs = { "subpixel_grazing", "lod_walk_oblique" };

    [MenuItem("NewTown/QA/Validate Visual Fidelity Gate Config")]
    public static void ValidateGateConfig()
    {
        GateConfig config = LoadJson<GateConfig>(ConfigPath);
        ObservabilityConfig observability = LoadJson<ObservabilityConfig>(ObservabilityPath);
        ValidateConfig(config);
        ValidateObservabilityConfig(config, observability);
        QualityBlockTemporalStabilityCapture.ValidateContractConfigOnly();
        Debug.Log(
            $"Visual fidelity gate config valid: threshold={config.visualPassThreshold}/100, " +
            $"categories={config.categories.Length}, criticalDefects={config.criticalDefects.Length}, " +
            $"still+temporal evidence coverage contract={observability.schemaVersion}.");
    }

    [MenuItem("NewTown/QA/Evaluate 4K Visual Fidelity Gate")]
    public static void EvaluateVisualGate()
    {
        GateConfig config = LoadJson<GateConfig>(ConfigPath);
        ObservabilityConfig observability = LoadJson<ObservabilityConfig>(ObservabilityPath);
        ValidateConfig(config);
        ValidateObservabilityConfig(config, observability);
        QualityBlockTemporalStabilityCapture.ValidateContractConfigOnly();

        // Intrinsic still-evidence provenance. A direct CLI/reflection call cannot bypass SHA-256
        // capture/receipt and pixel-exact 100% crop checks by skipping the evidence-bound wrapper.
        QualityBlockRenderEvidenceProvenanceQA.ValidateEvidenceProvenance();

        if (!File.Exists(AbsolutePath(EvidencePath)))
            throw new InvalidOperationException(
                "Visual Fidelity is UNSCORED: no real-render evidence exists. " +
                "Do not infer a visual score from source code or implementation readiness.");

        VisualEvidence evidence = LoadJson<VisualEvidence>(EvidencePath);
        var failures = new List<string>();

        if (!evidence.renderVerified)
            failures.Add("renderVerified=false; visual scoring requires an actual Unity render.");
        if (string.IsNullOrWhiteSpace(evidence.unityVersion))
            failures.Add("Unity version was not recorded with the evidence.");

        ValidateCaptureEvidence(config, evidence, failures);
        ValidateTemporalEvidence(evidence, failures);
        ValidateCategoryEvidence(config, observability, evidence, failures, out int totalScore);
        ValidateCriticalDefects(config, observability, evidence, failures);

        bool pass = failures.Count == 0 && totalScore >= config.visualPassThreshold;
        var result = new VisualGateResult
        {
            gateVersion = config.gateVersion,
            evaluatedUtc = DateTime.UtcNow.ToString("O"),
            visualFidelityStatus = pass ? "PASS" : "FAIL",
            visualScore = totalScore,
            threshold = config.visualPassThreshold,
            failures = failures.ToArray(),
            note =
                "A PASS is valid only for the exact sealed Unity still-render and temporal evidence referenced by visual_fidelity_evidence.json. " +
                "Observed view/crop/temporal references are mandatory and implementation readiness never contributes visual points."
        };
        WriteJson(ResultPath, result);

        if (!pass)
            throw new InvalidOperationException(
                $"4K Visual Fidelity Gate FAILED: {totalScore}/100. " + string.Join(" | ", failures));

        Debug.Log($"4K Visual Fidelity Gate PASSED: {totalScore}/100 with no critical defects and valid sealed temporal evidence.");
    }

    [MenuItem("NewTown/QA/Write Implementation Readiness Scorecard")]
    public static void WriteImplementationReadinessScorecard()
    {
        ReadinessCheck[] checks =
        {
            CheckAsset("danchi_high_granularity", 12, "Assets/Editor/QualityBlockDanchiDetailUpgrade.cs",
                "Dimensioned apartment subassemblies, fasteners, sash/rail/AC detail tooling exists."),
            CheckAsset("dimensioned_bevel_meshes", 6, "Assets/Editor/QualityBlockDetailBevelUpgrade.cs",
                "Generated detail geometry has a non-razor-edge replacement pass."),
            CheckAsset("danchi_lod0_lod3", 8, "Assets/Editor/QualityBlockDanchiLodUpgrade.cs",
                "Danchi master geometry has explicit LOD staging."),
            CheckAsset("tree_physical_hierarchy", 10, "Assets/Editor/QualityBlockTreeDetailUpgrade.cs",
                "Root/trunk/branch/twig/leaf hierarchy tooling exists."),
            CheckAllAssets("foliage_optics", 8,
                new[] { "Assets/Editor/QualityBlockFoliageOpticsUpgrade.cs", "Assets/Shaders/NewTownFoliageTransmission.shader" },
                "Two-sided foliage optics and transmission shader are present."),
            CheckAsset("cause_based_weathering", 8, "Assets/Editor/QualityBlockWeatheringUpgrade.cs",
                "Weathering is driven by rain/splash/drain/contact/UV context rather than arbitrary grime."),
            CheckAsset("pbr_generation", 8, "Assets/Editor/QualityBlockPbrUpgrade.cs",
                "PBR material generation pipeline is present."),
            CheckAsset("physical_solar_context", 5, "Assets/Scripts/Art/QualityBlockEnvironmentContext.cs",
                "Solar direction/elevation context exists for coherent midsummer lighting."),
            CheckAsset("replacement_art_slots", 5, "Assets/Editor/QualityBlockArtReplacement.cs",
                "Generated fallbacks remain replaceable by authored FBX/GLB art."),
            CheckAsset("material_construction_lookdev_registry", 10, "Assets/QA/material_construction_lookdev.json",
                "Per-component manufacture/material/angular-light-response registry exists."),
            CheckAsset("native_4k_capture_pipeline", 8, "Assets/Editor/QualityBlock4KCapture.cs",
                "Dedicated 3840x2160 multi-view capture pipeline exists."),
            CheckAsset("multi_angle_capture_manifest", 5, "Assets/QA/4k_capture_manifest.json",
                "Hero/oblique/grazing capture manifest exists."),
            CheckAsset("unity_compile_verification", 7, "Assets/QA/unity_compile_verified.json",
                "Unity 6.3 compile verification has been recorded by an actual runner/editor session.")
        };

        int score = checks.Where(x => x.passed).Sum(x => x.weight);
        int possible = checks.Sum(x => x.weight);
        var report = new ImplementationReadinessReport
        {
            generatedUtc = DateTime.UtcNow.ToString("O"),
            readinessScore = score,
            possibleScore = possible,
            visualFidelityStatus = "UNSCORED_UNTIL_REAL_4K_RENDER",
            checks = checks,
            blockers = checks.Where(x => !x.passed).Select(x => x.id).ToArray(),
            note = "Implementation Readiness measures pipeline coverage only. It must never be presented as the 4K Visual Fidelity score."
        };
        WriteJson(ReadinessPath, report);
        Debug.Log($"Implementation Readiness: {score}/{possible}. Visual Fidelity remains UNSCORED until real 4K still and temporal render evidence is reviewed.");
    }

    private static void ValidateConfig(GateConfig config)
    {
        if (config == null) throw new InvalidOperationException("Visual fidelity gate config is null.");
        if (config.visualPassThreshold != 92)
            throw new InvalidOperationException($"Visual pass threshold must remain 92, got {config.visualPassThreshold}.");
        if (config.requiredRender == null || config.requiredRender.width != 3840 || config.requiredRender.height != 2160)
            throw new InvalidOperationException("Visual gate must require native 3840x2160 evidence.");
        if (!config.requiredRender.require100PercentCrops)
            throw new InvalidOperationException("Visual gate must require 100% crops.");
        if (config.categories == null || config.categories.Length != 9)
            throw new InvalidOperationException("Visual gate must contain exactly nine weighted categories.");
        if (config.categories.Sum(x => x.weight) != 100)
            throw new InvalidOperationException("Visual fidelity category weights must sum to 100.");

        string[] expectedIds =
        {
            "geometry_construction", "material_pbr", "lighting_shadows_reflections",
            "texture_microdetail", "weathering_causality", "vegetation_natural_complexity",
            "period_authenticity", "cinematic_image", "temporal_lod_aliasing"
        };
        foreach (string id in expectedIds)
            if (config.categories.Count(x => x.id == id) != 1)
                throw new InvalidOperationException($"Missing or duplicated visual-fidelity category: {id}");

        if (config.criticalDefects == null || config.criticalDefects.Length < 10)
            throw new InvalidOperationException("Critical automatic-fail list is unexpectedly incomplete.");
    }

    private static void ValidateObservabilityConfig(GateConfig gate, ObservabilityConfig coverage)
    {
        if (coverage == null)
            throw new InvalidOperationException("Visual evidence observability contract is null.");
        if (coverage.categoryRequirements == null || coverage.categoryRequirements.Length != gate.categories.Length)
            throw new InvalidOperationException("Observability contract must define every Visual Fidelity category exactly once.");
        if (coverage.criticalDefectRequirements == null || coverage.criticalDefectRequirements.Length != gate.criticalDefects.Length)
            throw new InvalidOperationException("Observability contract must define every critical defect exactly once.");

        string[] allowedViews = coverage.allowedViews ?? Array.Empty<string>();
        string[] allowedCropRefs = coverage.allowedCropRefs ?? Array.Empty<string>();
        string[] allowedTemporalRefs = coverage.allowedTemporalRefs ?? Array.Empty<string>();
        if (!new HashSet<string>(allowedViews, StringComparer.Ordinal).SetEquals(gate.requiredRender.requiredViews))
            throw new InvalidOperationException("Observability allowedViews must exactly match the required render views.");
        if (allowedCropRefs.Distinct(StringComparer.Ordinal).Count() != allowedCropRefs.Length)
            throw new InvalidOperationException("Observability allowedCropRefs contains duplicates.");
        if (allowedTemporalRefs.Distinct(StringComparer.Ordinal).Count() != allowedTemporalRefs.Length ||
            !new HashSet<string>(allowedTemporalRefs, StringComparer.Ordinal).SetEquals(RequiredTemporalRefs))
            throw new InvalidOperationException("Observability allowedTemporalRefs must be exactly subpixel_grazing and lod_walk_oblique.");

        foreach (GateCategory category in gate.categories)
        {
            CoverageRequirement[] matches = coverage.categoryRequirements
                .Where(x => x != null && x.id == category.id).ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException($"Observability contract missing/duplicates category requirement: {category.id}");
            ValidateCoverageRequirement(matches[0], allowedViews, allowedCropRefs, allowedTemporalRefs, $"category/{category.id}");
        }

        foreach (CriticalDefect defect in gate.criticalDefects)
        {
            CoverageRequirement[] matches = coverage.criticalDefectRequirements
                .Where(x => x != null && x.id == defect.id).ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException($"Observability contract missing/duplicates critical requirement: {defect.id}");
            ValidateCoverageRequirement(matches[0], allowedViews, allowedCropRefs, allowedTemporalRefs, $"critical/{defect.id}");
        }

        CoverageRequirement temporalCategory = coverage.categoryRequirements.Single(x => x.id == "temporal_lod_aliasing");
        if (!new HashSet<string>(temporalCategory.requiredTemporalRefs ?? Array.Empty<string>(), StringComparer.Ordinal)
                .SetEquals(RequiredTemporalRefs))
            throw new InvalidOperationException("Temporal/LOD/aliasing category must require both sealed temporal probes.");
        CoverageRequirement shimmer = coverage.criticalDefectRequirements.Single(x => x.id == "severe_aliasing_or_shimmer");
        if (!new HashSet<string>(shimmer.requiredTemporalRefs ?? Array.Empty<string>(), StringComparer.Ordinal)
                .SetEquals(RequiredTemporalRefs))
            throw new InvalidOperationException("severe_aliasing_or_shimmer must require both sealed temporal probes.");
        CoverageRequirement lodPop = coverage.criticalDefectRequirements.Single(x => x.id == "visible_lod_pop");
        if (!new HashSet<string>(lodPop.requiredTemporalRefs ?? Array.Empty<string>(), StringComparer.Ordinal)
                .SetEquals(new[] { "lod_walk_oblique" }))
            throw new InvalidOperationException("visible_lod_pop must require lod_walk_oblique temporal evidence.");
    }

    private static void ValidateCoverageRequirement(CoverageRequirement requirement, string[] allowedViews,
        string[] allowedCropRefs, string[] allowedTemporalRefs, string label)
    {
        string[] requiredViews = requirement.requiredViews ?? Array.Empty<string>();
        string[] requiredCrops = requirement.requiredCropRefs ?? Array.Empty<string>();
        string[] requiredTemporal = requirement.requiredTemporalRefs ?? Array.Empty<string>();
        if (requiredViews.Distinct(StringComparer.Ordinal).Count() != requiredViews.Length)
            throw new InvalidOperationException($"{label} contains duplicate requiredViews.");
        if (requiredCrops.Distinct(StringComparer.Ordinal).Count() != requiredCrops.Length)
            throw new InvalidOperationException($"{label} contains duplicate requiredCropRefs.");
        if (requiredTemporal.Distinct(StringComparer.Ordinal).Count() != requiredTemporal.Length)
            throw new InvalidOperationException($"{label} contains duplicate requiredTemporalRefs.");
        foreach (string view in requiredViews)
            if (!allowedViews.Contains(view))
                throw new InvalidOperationException($"{label} references unknown view '{view}'.");
        foreach (string crop in requiredCrops)
            if (!allowedCropRefs.Contains(crop))
                throw new InvalidOperationException($"{label} references unknown crop '{crop}'.");
        foreach (string temporal in requiredTemporal)
            if (!allowedTemporalRefs.Contains(temporal))
                throw new InvalidOperationException($"{label} references unknown temporal probe '{temporal}'.");
        if (requirement.minimumCropReferences < requiredCrops.Length)
            throw new InvalidOperationException(
                $"{label} minimumCropReferences={requirement.minimumCropReferences} is below its required crop count {requiredCrops.Length}.");
        if (requirement.minimumTemporalReferences < requiredTemporal.Length)
            throw new InvalidOperationException(
                $"{label} minimumTemporalReferences={requirement.minimumTemporalReferences} is below its required temporal count {requiredTemporal.Length}.");
    }

    private static void ValidateCaptureEvidence(GateConfig config, VisualEvidence evidence, List<string> failures)
    {
        if (evidence.captures == null)
        {
            failures.Add("No capture evidence was supplied.");
            return;
        }

        foreach (string requiredView in config.requiredRender.requiredViews)
        {
            CaptureEvidence capture = evidence.captures.FirstOrDefault(x => x.viewId == requiredView);
            if (capture == null)
            {
                failures.Add($"Missing required 4K view: {requiredView}.");
                continue;
            }
            if (capture.width != config.requiredRender.width || capture.height != config.requiredRender.height)
                failures.Add($"{requiredView} is {capture.width}x{capture.height}; expected 3840x2160.");
            if (string.IsNullOrWhiteSpace(capture.assetPath) || !File.Exists(AbsolutePath(capture.assetPath)))
                failures.Add($"{requiredView} capture file is missing: {capture.assetPath}");
            if (config.requiredRender.require100PercentCrops)
            {
                if (capture.cropPaths == null || capture.cropPaths.Length == 0)
                    failures.Add($"{requiredView} has no 100% crop evidence.");
                else
                    foreach (string crop in capture.cropPaths)
                        if (string.IsNullOrWhiteSpace(crop) || !File.Exists(AbsolutePath(crop)))
                            failures.Add($"{requiredView} 100% crop is missing: {crop}");
            }
        }
    }

    private static void ValidateTemporalEvidence(VisualEvidence evidence, List<string> failures)
    {
        if (evidence.temporalEvidence == null)
        {
            failures.Add(
                "Sealed native-4K temporal evidence is missing. Temporal/LOD/aliasing stability and shimmer/LOD critical defects cannot be cleared from stills alone.");
            return;
        }

        TemporalEvidenceReference temporal = evidence.temporalEvidence;
        try
        {
            QualityBlockTemporalStabilityCapture.ValidateTemporalEvidenceForScoring(
                temporal.captureSessionId,
                temporal.manifestAssetPath,
                temporal.manifestSha256,
                temporal.receiptAssetPath,
                temporal.receiptSha256);
        }
        catch (Exception ex)
        {
            failures.Add("Temporal evidence provenance/pixel validation failed: " + ex.Message);
        }
    }

    private static void ValidateCategoryEvidence(GateConfig config, ObservabilityConfig coverage, VisualEvidence evidence,
        List<string> failures, out int totalScore)
    {
        totalScore = 0;
        if (evidence.categories == null)
        {
            failures.Add("No category scores were supplied.");
            return;
        }

        foreach (GateCategory category in config.categories)
        {
            CategoryEvidence scored = evidence.categories.FirstOrDefault(x => x.id == category.id);
            if (scored == null)
            {
                failures.Add($"Category is unscored: {category.id}.");
                continue;
            }
            if (scored.score < 0 || scored.score > category.weight)
            {
                failures.Add($"Category score is out of range: {category.id}={scored.score}/{category.weight}.");
                continue;
            }

            totalScore += scored.score;
            if (scored.score < category.hardMinimum)
                failures.Add($"Category below hard minimum: {category.id}={scored.score}, minimum={category.hardMinimum}.");
            if (string.IsNullOrWhiteSpace(scored.evidence))
                failures.Add($"Category lacks observed render evidence notes: {category.id}.");
            if (scored.score < category.weight && string.IsNullOrWhiteSpace(scored.deductions))
                failures.Add($"Category has deductions but no deduction rationale: {category.id}={scored.score}/{category.weight}.");

            CoverageRequirement requirement = coverage.categoryRequirements.Single(x => x.id == category.id);
            ValidateObservedReferences(requirement, scored.observedViews, scored.observedCropRefs, scored.observedTemporalRefs,
                coverage, failures, $"category {category.id}");
        }
    }

    private static void ValidateCriticalDefects(GateConfig config, ObservabilityConfig coverage, VisualEvidence evidence,
        List<string> failures)
    {
        if (evidence.criticalDefects == null)
        {
            failures.Add("Critical-defect review is missing.");
            return;
        }

        foreach (CriticalDefect definition in config.criticalDefects)
        {
            CriticalDefectEvidence reviewed = evidence.criticalDefects.FirstOrDefault(x => x.id == definition.id);
            if (reviewed == null)
            {
                failures.Add($"Critical defect was not explicitly reviewed: {definition.id}.");
                continue;
            }
            if (string.IsNullOrWhiteSpace(reviewed.evidence))
                failures.Add($"Critical defect review has no evidence note: {definition.id}.");

            CoverageRequirement requirement = coverage.criticalDefectRequirements.Single(x => x.id == definition.id);
            ValidateObservedReferences(requirement, reviewed.observedViews, reviewed.observedCropRefs, reviewed.observedTemporalRefs,
                coverage, failures, $"critical defect {definition.id}");

            if (reviewed.present)
                failures.Add($"CRITICAL AUTO-FAIL: {definition.id} — {definition.description}. Evidence: {reviewed.evidence}");
        }
    }

    private static void ValidateObservedReferences(CoverageRequirement requirement, string[] observedViews,
        string[] observedCropRefs, string[] observedTemporalRefs, ObservabilityConfig coverage,
        List<string> failures, string label)
    {
        string[] views = observedViews ?? Array.Empty<string>();
        string[] crops = observedCropRefs ?? Array.Empty<string>();
        string[] temporal = observedTemporalRefs ?? Array.Empty<string>();
        var viewSet = new HashSet<string>(views, StringComparer.Ordinal);
        var cropSet = new HashSet<string>(crops, StringComparer.Ordinal);
        var temporalSet = new HashSet<string>(temporal, StringComparer.Ordinal);

        if (viewSet.Count != views.Length)
            failures.Add($"{label} contains duplicate observedViews.");
        if (cropSet.Count != crops.Length)
            failures.Add($"{label} contains duplicate observedCropRefs.");
        if (temporalSet.Count != temporal.Length)
            failures.Add($"{label} contains duplicate observedTemporalRefs.");

        foreach (string view in views)
            if (!(coverage.allowedViews ?? Array.Empty<string>()).Contains(view))
                failures.Add($"{label} references unknown view '{view}'.");
        foreach (string crop in crops)
            if (!(coverage.allowedCropRefs ?? Array.Empty<string>()).Contains(crop))
                failures.Add($"{label} references unknown crop '{crop}'.");
        foreach (string temporalRef in temporal)
            if (!(coverage.allowedTemporalRefs ?? Array.Empty<string>()).Contains(temporalRef))
                failures.Add($"{label} references unknown temporal probe '{temporalRef}'.");

        foreach (string requiredView in requirement.requiredViews ?? Array.Empty<string>())
            if (!viewSet.Contains(requiredView))
                failures.Add($"{label} did not observe required full-frame view '{requiredView}'.");
        foreach (string requiredCrop in requirement.requiredCropRefs ?? Array.Empty<string>())
            if (!cropSet.Contains(requiredCrop))
                failures.Add($"{label} did not observe required 100% crop '{requiredCrop}'.");
        foreach (string requiredTemporal in requirement.requiredTemporalRefs ?? Array.Empty<string>())
            if (!temporalSet.Contains(requiredTemporal))
                failures.Add($"{label} did not observe required sealed temporal probe '{requiredTemporal}'.");
        if (cropSet.Count < requirement.minimumCropReferences)
            failures.Add(
                $"{label} cites only {cropSet.Count} unique 100% crops; minimum is {requirement.minimumCropReferences}.");
        if (temporalSet.Count < requirement.minimumTemporalReferences)
            failures.Add(
                $"{label} cites only {temporalSet.Count} unique temporal probes; minimum is {requirement.minimumTemporalReferences}.");
    }

    private static ReadinessCheck CheckAsset(string id, int weight, string assetPath, string evidence)
    {
        bool exists = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null || File.Exists(AbsolutePath(assetPath));
        return new ReadinessCheck { id = id, weight = weight, passed = exists, evidence = evidence, assetPaths = new[] { assetPath } };
    }

    private static ReadinessCheck CheckAllAssets(string id, int weight, string[] assetPaths, string evidence)
    {
        bool exists = assetPaths.All(path => AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null || File.Exists(AbsolutePath(path)));
        return new ReadinessCheck { id = id, weight = weight, passed = exists, evidence = evidence, assetPaths = assetPaths };
    }

    private static T LoadJson<T>(string assetPath)
    {
        string absolute = AbsolutePath(assetPath);
        if (!File.Exists(absolute)) throw new FileNotFoundException($"Required QA file not found: {assetPath}");
        T value = JsonUtility.FromJson<T>(File.ReadAllText(absolute));
        if (value == null) throw new InvalidOperationException($"Could not parse QA JSON: {assetPath}");
        return value;
    }

    private static void WriteJson<T>(string assetPath, T value)
    {
        string absolute = AbsolutePath(assetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute));
        File.WriteAllText(absolute, JsonUtility.ToJson(value, true));
        AssetDatabase.Refresh();
    }

    private static string AbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    [Serializable]
    public sealed class GateConfig
    {
        public string gateVersion;
        public int visualPassThreshold;
        public RequiredRender requiredRender;
        public GateCategory[] categories;
        public CriticalDefect[] criticalDefects;
    }

    [Serializable]
    public sealed class RequiredRender
    {
        public int width;
        public int height;
        public bool require100PercentCrops;
        public string[] requiredViews;
    }

    [Serializable]
    public sealed class GateCategory
    {
        public string id;
        public string label;
        public int weight;
        public int hardMinimum;
    }

    [Serializable]
    public sealed class CriticalDefect
    {
        public string id;
        public string description;
    }

    [Serializable]
    public sealed class ObservabilityConfig
    {
        public string schemaVersion;
        public string purpose;
        public string referenceFormat;
        public string[] allowedViews;
        public string[] allowedCropRefs;
        public string[] allowedTemporalRefs;
        public CoverageRequirement[] categoryRequirements;
        public CoverageRequirement[] criticalDefectRequirements;
        public string[] hardRules;
    }

    [Serializable]
    public sealed class CoverageRequirement
    {
        public string id;
        public string[] requiredViews;
        public string[] requiredCropRefs;
        public int minimumCropReferences;
        public string[] requiredTemporalRefs;
        public int minimumTemporalReferences;
        public string reason;
    }

    [Serializable]
    public sealed class VisualEvidence
    {
        public bool renderVerified;
        public string unityVersion;
        public CaptureEvidence[] captures;
        public TemporalEvidenceReference temporalEvidence;
        public CategoryEvidence[] categories;
        public CriticalDefectEvidence[] criticalDefects;
    }

    [Serializable]
    public sealed class CaptureEvidence
    {
        public string viewId;
        public string assetPath;
        public int width;
        public int height;
        public string[] cropPaths;
    }

    [Serializable]
    public sealed class TemporalEvidenceReference
    {
        public string captureSessionId;
        public string manifestAssetPath;
        public string manifestSha256;
        public string receiptAssetPath;
        public string receiptSha256;
    }

    [Serializable]
    public sealed class CategoryEvidence
    {
        public string id;
        public int score;
        public string evidence;
        public string deductions;
        public string correctiveAction;
        public string[] observedViews;
        public string[] observedCropRefs;
        public string[] observedTemporalRefs;
    }

    [Serializable]
    public sealed class CriticalDefectEvidence
    {
        public string id;
        public bool present;
        public string evidence;
        public string[] observedViews;
        public string[] observedCropRefs;
        public string[] observedTemporalRefs;
    }

    [Serializable]
    public sealed class VisualGateResult
    {
        public string gateVersion;
        public string evaluatedUtc;
        public string visualFidelityStatus;
        public int visualScore;
        public int threshold;
        public string[] failures;
        public string note;
    }

    [Serializable]
    public sealed class ImplementationReadinessReport
    {
        public string generatedUtc;
        public int readinessScore;
        public int possibleScore;
        public string visualFidelityStatus;
        public ReadinessCheck[] checks;
        public string[] blockers;
        public string note;
    }

    [Serializable]
    public sealed class ReadinessCheck
    {
        public string id;
        public int weight;
        public bool passed;
        public string evidence;
        public string[] assetPaths;
    }
}
