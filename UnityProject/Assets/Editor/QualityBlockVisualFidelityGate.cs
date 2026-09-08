using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Numeric release gate for the 4K-film visual target.
/// IMPORTANT: this class never converts implementation coverage into a visual-fidelity score.
/// Visual PASS requires real 3840x2160 Unity renders, 100% crops, explicit category scores,
/// and explicit review of every critical automatic-fail defect.
/// </summary>
public static class QualityBlockVisualFidelityGate
{
    private const string ConfigPath = "Assets/QA/visual_fidelity_gate.json";
    private const string EvidencePath = "Assets/QA/visual_fidelity_evidence.json";
    private const string ResultPath = "Assets/QA/visual_fidelity_result.json";
    private const string ReadinessPath = "Assets/QA/implementation_readiness.json";

    [MenuItem("NewTown/QA/Validate Visual Fidelity Gate Config")]
    public static void ValidateGateConfig()
    {
        GateConfig config = LoadJson<GateConfig>(ConfigPath);
        ValidateConfig(config);
        Debug.Log($"Visual fidelity gate config valid: threshold={config.visualPassThreshold}/100, categories={config.categories.Length}, criticalDefects={config.criticalDefects.Length}.");
    }

    [MenuItem("NewTown/QA/Evaluate 4K Visual Fidelity Gate")]
    public static void EvaluateVisualGate()
    {
        GateConfig config = LoadJson<GateConfig>(ConfigPath);
        ValidateConfig(config);

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
        ValidateCategoryEvidence(config, evidence, failures, out int totalScore);
        ValidateCriticalDefects(config, evidence, failures);

        bool pass = failures.Count == 0 && totalScore >= config.visualPassThreshold;
        var result = new VisualGateResult
        {
            gateVersion = config.gateVersion,
            evaluatedUtc = DateTime.UtcNow.ToString("O"),
            visualFidelityStatus = pass ? "PASS" : "FAIL",
            visualScore = totalScore,
            threshold = config.visualPassThreshold,
            failures = failures.ToArray(),
            note = "A PASS is valid only for the exact render evidence referenced by visual_fidelity_evidence.json."
        };
        WriteJson(ResultPath, result);

        if (!pass)
            throw new InvalidOperationException(
                $"4K Visual Fidelity Gate FAILED: {totalScore}/100. " + string.Join(" | ", failures));

        Debug.Log($"4K Visual Fidelity Gate PASSED: {totalScore}/100 with no critical defects.");
    }

    [MenuItem("NewTown/QA/Write Implementation Readiness Scorecard")]
    public static void WriteImplementationReadinessScorecard()
    {
        // This is intentionally a source/pipeline readiness score, not a visual-quality score.
        // Missing runtime/render checks keep the score below 100 even when source coverage is broad.
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
        Debug.Log($"Implementation Readiness: {score}/{possible}. Visual Fidelity remains UNSCORED until real 4K render evidence is reviewed.");
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

    private static void ValidateCategoryEvidence(GateConfig config, VisualEvidence evidence,
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
        }
    }

    private static void ValidateCriticalDefects(GateConfig config, VisualEvidence evidence, List<string> failures)
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
            if (reviewed.present)
                failures.Add($"CRITICAL AUTO-FAIL: {definition.id} — {definition.description}. Evidence: {reviewed.evidence}");
        }
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
    public sealed class VisualEvidence
    {
        public bool renderVerified;
        public string unityVersion;
        public CaptureEvidence[] captures;
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
    public sealed class CategoryEvidence
    {
        public string id;
        public int score;
        public string evidence;
        public string deductions;
        public string correctiveAction;
    }

    [Serializable]
    public sealed class CriticalDefectEvidence
    {
        public string id;
        public bool present;
        public string evidence;
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
