using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Pixel-domain triage for the critical "obvious repetition" defect.
///
/// Static scene fingerprints can reject exact procedural clones before rendering, but they cannot tell
/// whether a texture tile, facade rhythm, vegetation cluster or weathering stamp is immediately obvious
/// in the final post-tonemap 4K image. This diagnostic therefore measures translation-local luma
/// similarity on selected pixel-exact 100% crops from the real Unity capture.
///
/// It is deliberately warning-only. Perspective, legitimate manufactured repetition and smooth regions
/// can all confound a numerical periodicity detector, so this class never awards Visual Fidelity points
/// and never clears the obvious_repetition critical defect. Human review of the exact sealed pixels
/// remains mandatory.
/// </summary>
public static class QualityBlockRenderedRepetitionDiagnostics
{
    private const string ManifestPath = "Assets/QA/4k_capture_manifest.json";
    private const string ContractPath = "Assets/QA/rendered_repetition_diagnostics_contract.json";
    private const string ReportPath = "Assets/QA/rendered_repetition_diagnostics.json";
    private const int NativeWidth = 3840;
    private const int NativeHeight = 2160;

    [MenuItem("NewTown/QA/Analyze Native 4K Repetition Risk")]
    public static void AnalyzeExistingCapture()
    {
        RepetitionContract contract = LoadJson<RepetitionContract>(ContractPath);
        ValidateContract(contract);

        CaptureManifest manifest = LoadJson<CaptureManifest>(ManifestPath);
        ValidateRuntimeManifest(manifest);

        var cropDiagnostics = new List<CropDiagnostics>();
        foreach (string cropRef in contract.requiredCropRefs)
        {
            ResolveCrop(manifest, cropRef, out CaptureRecord capture, out CropRecord crop);
            Texture2D texture = LoadPng(crop.assetPath, crop.width, crop.height);
            try
            {
                byte[] luma = BuildLuma(texture.GetPixels32());
                AxisDiagnostics horizontal = AnalyzeAxis(luma, texture.width, texture.height, true, contract);
                AxisDiagnostics vertical = AnalyzeAxis(luma, texture.width, texture.height, false, contract);

                var warnings = new List<string>();
                if (horizontal.warning)
                    warnings.Add($"horizontal local periodicity prominence {horizontal.strongestLocalProminence:0.###} at {horizontal.strongestShiftPx}px; inspect repeated texture/module rhythm at 100%.");
                if (vertical.warning)
                    warnings.Add($"vertical local periodicity prominence {vertical.strongestLocalProminence:0.###} at {vertical.strongestShiftPx}px; inspect repeated texture/module rhythm at 100%.");
                if (horizontal.evaluatedShiftCount < 3)
                    warnings.Add("horizontal periodicity sweep had fewer than three texture-qualified shifts; rely on direct pixel review for this axis.");
                if (vertical.evaluatedShiftCount < 3)
                    warnings.Add("vertical periodicity sweep had fewer than three texture-qualified shifts; rely on direct pixel review for this axis.");

                cropDiagnostics.Add(new CropDiagnostics
                {
                    viewId = capture.viewId,
                    cropId = crop.id,
                    assetPath = crop.assetPath,
                    assetSha256 = Sha256File(crop.assetPath),
                    width = texture.width,
                    height = texture.height,
                    horizontal = horizontal,
                    vertical = vertical,
                    warningFlags = warnings.ToArray(),
                    requiresHumanReview = true
                });
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        string[] warningsAll = cropDiagnostics
            .SelectMany(x => (x.warningFlags ?? Array.Empty<string>()).Select(flag => $"{x.viewId}/{x.cropId}: {flag}"))
            .ToArray();

        var report = new DiagnosticsReport
        {
            schemaVersion = "1.0",
            generatedUtc = DateTime.UtcNow.ToString("O"),
            status = "TRIAGE_COMPLETE_HUMAN_REVIEW_REQUIRED",
            sourceManifestPath = ManifestPath,
            sourceManifestSha256 = Sha256File(ManifestPath),
            unityVersion = manifest.unityVersion,
            graphicsDevice = manifest.graphicsDevice,
            graphicsApi = manifest.graphicsApi,
            projectColorSpace = manifest.projectColorSpace,
            renderProducedByUnity = true,
            imageCount = cropDiagnostics.Count,
            images = cropDiagnostics.ToArray(),
            warningCount = warningsAll.Length,
            warnings = warningsAll,
            humanReviewRequired = true,
            visualFidelityPointsAwarded = 0,
            criticalDefectAutomaticallyCleared = false,
            criticalDefectId = "obvious_repetition",
            note =
                "Translation-local luma periodicity is objective triage only. Every required 100% crop remains subject to human review; " +
                "zero warnings cannot clear obvious_repetition, and a warning is not an automatic defect finding."
        };

        WriteJson(ReportPath, report);
        AssetDatabase.Refresh();
        Debug.Log(
            $"Native 4K repetition diagnostics written: crops={cropDiagnostics.Count}, warnings={warningsAll.Length}. " +
            "Visual Fidelity remains UNSCORED and obvious_repetition remains human-reviewed.");
    }

    [MenuItem("NewTown/QA/Validate Rendered Repetition Diagnostics Contract")]
    public static void ValidateContractConfigOnly()
    {
        RepetitionContract contract = LoadJson<RepetitionContract>(ContractPath);
        ValidateContract(contract);
        Debug.Log("Rendered repetition diagnostics contract valid. Configuration awards 0 Visual Fidelity points.");
    }

    /// <summary>
    /// Scoring precondition only: proves that the latest triage report was computed from the current
    /// runtime manifest and the current bytes of every required 100% crop. It intentionally does not
    /// reject warning flags and does not clear the critical defect; the reviewed evidence still decides.
    /// </summary>
    public static void ValidateLatestReportForScoring()
    {
        RepetitionContract contract = LoadJson<RepetitionContract>(ContractPath);
        ValidateContract(contract);

        CaptureManifest manifest = LoadJson<CaptureManifest>(ManifestPath);
        ValidateRuntimeManifest(manifest);

        if (!File.Exists(AbsolutePath(ReportPath)))
            throw new FileNotFoundException(
                "Rendered repetition diagnostics are missing. Analyze the actual native-4K capture before Visual Fidelity scoring.",
                ReportPath);

        DiagnosticsReport report = LoadJson<DiagnosticsReport>(ReportPath);
        if (!string.Equals(report.schemaVersion, "1.0", StringComparison.Ordinal) ||
            !string.Equals(report.status, "TRIAGE_COMPLETE_HUMAN_REVIEW_REQUIRED", StringComparison.Ordinal))
            throw new InvalidOperationException("Rendered repetition diagnostics report has an unexpected schema/status.");
        if (!report.renderProducedByUnity || !report.humanReviewRequired || report.visualFidelityPointsAwarded != 0 ||
            report.criticalDefectAutomaticallyCleared)
            throw new InvalidOperationException(
                "Rendered repetition diagnostics attempted to weaken actual-render/human-review/zero-point critical-defect rules.");
        if (!string.Equals(report.criticalDefectId, "obvious_repetition", StringComparison.Ordinal))
            throw new InvalidOperationException("Rendered repetition diagnostics report is bound to the wrong critical defect.");
        if (!string.Equals(report.sourceManifestPath, ManifestPath, StringComparison.Ordinal) ||
            !string.Equals(report.sourceManifestSha256, Sha256File(ManifestPath), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Rendered repetition diagnostics do not bind the current 4K capture manifest bytes.");
        if (!string.Equals(report.unityVersion, manifest.unityVersion, StringComparison.Ordinal) ||
            !string.Equals(report.graphicsDevice, manifest.graphicsDevice, StringComparison.Ordinal) ||
            !string.Equals(report.graphicsApi, manifest.graphicsApi, StringComparison.Ordinal) ||
            !string.Equals(report.projectColorSpace, manifest.projectColorSpace, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Rendered repetition diagnostics runtime provenance differs from the current capture manifest.");
        if (!DateTimeOffset.TryParse(report.generatedUtc, out _))
            throw new InvalidOperationException("Rendered repetition diagnostics generatedUtc is missing/invalid.");
        if (report.images == null || report.images.Length != contract.requiredCropRefs.Length ||
            report.imageCount != report.images.Length)
            throw new InvalidOperationException(
                $"Rendered repetition diagnostics must contain exactly {contract.requiredCropRefs.Length} required crops.");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string cropRef in contract.requiredCropRefs)
        {
            ResolveCrop(manifest, cropRef, out CaptureRecord capture, out CropRecord crop);
            CropDiagnostics diagnostic = report.images.SingleOrDefault(x =>
                x != null && string.Equals(x.viewId, capture.viewId, StringComparison.Ordinal) &&
                string.Equals(x.cropId, crop.id, StringComparison.Ordinal));
            if (diagnostic == null)
                throw new InvalidOperationException($"Rendered repetition diagnostics are missing required crop {cropRef}.");
            if (!seen.Add(cropRef))
                throw new InvalidOperationException($"Rendered repetition diagnostics duplicate crop {cropRef}.");
            if (!string.Equals(diagnostic.assetPath, crop.assetPath, StringComparison.Ordinal) ||
                diagnostic.width != crop.width || diagnostic.height != crop.height)
                throw new InvalidOperationException($"Rendered repetition diagnostics path/dimensions drifted for {cropRef}.");
            if (!string.Equals(diagnostic.assetSha256, Sha256File(crop.assetPath), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Rendered repetition diagnostics crop bytes changed after analysis: {cropRef}.");
            if (!diagnostic.requiresHumanReview || diagnostic.horizontal == null || diagnostic.vertical == null)
                throw new InvalidOperationException(
                    $"Rendered repetition diagnostics are structurally incomplete for {cropRef}.");
        }

        Debug.Log(
            "Rendered repetition diagnostics are current for the exact manifest/crop bytes. Warnings remain triage only; " +
            "the obvious_repetition critical defect still requires explicit human pixel review.");
    }

    private static AxisDiagnostics AnalyzeAxis(byte[] luma, int width, int height, bool horizontal,
        RepetitionContract contract)
    {
        int dimension = horizontal ? width : height;
        int maxShift = Mathf.Min(contract.maximumPeriodPx, dimension - 2);
        var samples = new List<ShiftSample>();

        for (int shift = contract.minimumPeriodPx; shift <= maxShift; shift += contract.periodStepPx)
        {
            ShiftSample sample = MeasureShift(luma, width, height, horizontal, shift, contract);
            if (sample.qualifiedComparisons >= contract.minimumQualifiedComparisonsPerShift)
                samples.Add(sample);
        }

        if (samples.Count == 0)
        {
            return new AxisDiagnostics
            {
                axis = horizontal ? "horizontal" : "vertical",
                strongestShiftPx = 0,
                strongestMeanAbsLumaCode = 0f,
                localBaselineMeanAbsLumaCode = 0f,
                strongestLocalProminence = 0f,
                qualifiedComparisonsAtStrongestShift = 0,
                evaluatedShiftCount = 0,
                warning = false
            };
        }

        float bestProminence = float.NegativeInfinity;
        float bestBaseline = 0f;
        ShiftSample best = samples[0];

        for (int i = 0; i < samples.Count; i++)
        {
            var neighbors = new List<float>();
            int lo = Mathf.Max(0, i - contract.localNeighborhoodRadiusSteps);
            int hi = Mathf.Min(samples.Count - 1, i + contract.localNeighborhoodRadiusSteps);
            for (int n = lo; n <= hi; n++)
            {
                if (n != i) neighbors.Add(samples[n].meanAbsLumaCode);
            }
            if (neighbors.Count == 0) continue;

            float baseline = Median(neighbors);
            float prominence = baseline > 0.0001f
                ? Mathf.Clamp01((baseline - samples[i].meanAbsLumaCode) / baseline)
                : 0f;
            if (prominence > bestProminence)
            {
                bestProminence = prominence;
                bestBaseline = baseline;
                best = samples[i];
            }
        }

        if (float.IsNegativeInfinity(bestProminence)) bestProminence = 0f;

        return new AxisDiagnostics
        {
            axis = horizontal ? "horizontal" : "vertical",
            strongestShiftPx = best.shiftPx,
            strongestMeanAbsLumaCode = best.meanAbsLumaCode,
            localBaselineMeanAbsLumaCode = bestBaseline,
            strongestLocalProminence = bestProminence,
            qualifiedComparisonsAtStrongestShift = best.qualifiedComparisons,
            evaluatedShiftCount = samples.Count,
            warning = samples.Count >= 3 && bestProminence >= contract.localProminenceWarning
        };
    }

    private static ShiftSample MeasureShift(byte[] luma, int width, int height, bool horizontal, int shift,
        RepetitionContract contract)
    {
        long differenceSum = 0;
        int qualified = 0;
        int stride = contract.sampleStridePx;
        int maxX = horizontal ? width - shift - 1 : width - 1;
        int maxY = horizontal ? height - 1 : height - shift - 1;

        for (int y = 0; y < maxY; y += stride)
        {
            int row = y * width;
            for (int x = 0; x < maxX; x += stride)
            {
                int a = row + x;
                int b = horizontal ? a + shift : a + shift * width;
                if (!IsTextured(luma, width, height, a, contract.gradientThresholdCode) &&
                    !IsTextured(luma, width, height, b, contract.gradientThresholdCode))
                    continue;

                differenceSum += Math.Abs(luma[a] - luma[b]);
                qualified++;
            }
        }

        return new ShiftSample
        {
            shiftPx = shift,
            qualifiedComparisons = qualified,
            meanAbsLumaCode = qualified > 0 ? (float)(differenceSum / (double)qualified) : 255f
        };
    }

    private static bool IsTextured(byte[] luma, int width, int height, int index, int threshold)
    {
        int x = index % width;
        int y = index / width;
        int center = luma[index];
        int gx = x + 1 < width ? Math.Abs(center - luma[index + 1]) : 0;
        int gy = y + 1 < height ? Math.Abs(center - luma[index + width]) : 0;
        return Mathf.Max(gx, gy) >= threshold;
    }

    private static byte[] BuildLuma(Color32[] pixels)
    {
        var luma = new byte[pixels.Length];
        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 p = pixels[i];
            int code = (54 * p.r + 183 * p.g + 19 * p.b + 128) >> 8;
            luma[i] = (byte)Mathf.Clamp(code, 0, 255);
        }
        return luma;
    }

    private static float Median(List<float> values)
    {
        if (values == null || values.Count == 0) return 0f;
        values.Sort();
        int mid = values.Count / 2;
        return (values.Count & 1) == 1
            ? values[mid]
            : 0.5f * (values[mid - 1] + values[mid]);
    }

    private static void ValidateContract(RepetitionContract contract)
    {
        if (contract == null)
            throw new InvalidOperationException("Rendered repetition diagnostics contract is null/unparseable.");
        if (!string.Equals(contract.schemaVersion, "1.0", StringComparison.Ordinal))
            throw new InvalidOperationException($"Unexpected repetition diagnostics schemaVersion '{contract.schemaVersion}'.");
        if (contract.nativeWidth != NativeWidth || contract.nativeHeight != NativeHeight)
            throw new InvalidOperationException("Rendered repetition diagnostics must remain native 3840x2160.");
        if (!contract.actualUnityRenderRequired || !contract.analyzePixelExact100PercentCrops)
            throw new InvalidOperationException("Rendered repetition diagnostics must require actual Unity 100% crop evidence.");
        if (contract.visualFidelityPointsAwarded != 0 || contract.automaticallyClearsCriticalDefect)
            throw new InvalidOperationException("Rendered repetition diagnostics may not award points or automatically clear a critical defect.");
        if (!string.Equals(contract.criticalDefectId, "obvious_repetition", StringComparison.Ordinal))
            throw new InvalidOperationException("Rendered repetition diagnostics must remain bound to obvious_repetition.");
        if (contract.sampleStridePx < 1 || contract.sampleStridePx > 8 ||
            contract.gradientThresholdCode < 4 || contract.gradientThresholdCode > 32 ||
            contract.minimumQualifiedComparisonsPerShift < 500)
            throw new InvalidOperationException("Rendered repetition sampling thresholds are outside the guarded range.");
        if (contract.minimumPeriodPx < 16 || contract.maximumPeriodPx > 640 ||
            contract.maximumPeriodPx <= contract.minimumPeriodPx || contract.periodStepPx < 4 ||
            contract.periodStepPx > 32)
            throw new InvalidOperationException("Rendered repetition translation sweep was weakened or is invalid.");
        if (contract.localNeighborhoodRadiusSteps < 1 || contract.localNeighborhoodRadiusSteps > 8 ||
            contract.localProminenceWarning < 0.05f || contract.localProminenceWarning > 0.30f)
            throw new InvalidOperationException("Rendered repetition local-prominence guard is outside the intended diagnostic range.");

        string[] required =
        {
            "hero/facade_center",
            "hero/ground_contact",
            "oblique/vegetation_grounding",
            "grazing/material_grazing",
            "grazing/tree_shadow_contact"
        };
        RequireExactSet(contract.requiredCropRefs, required, "rendered repetition requiredCropRefs");
    }

    private static void ValidateRuntimeManifest(CaptureManifest manifest)
    {
        if (manifest == null || !manifest.renderProducedByUnity)
            throw new InvalidOperationException(
                "No actual Unity 4K render exists; rendered repetition diagnostics cannot run on the template manifest.");
        if (manifest.width != NativeWidth || manifest.height != NativeHeight)
            throw new InvalidOperationException("Rendered repetition diagnostics require native 3840x2160 capture evidence.");
        if (string.IsNullOrWhiteSpace(manifest.unityVersion) || string.IsNullOrWhiteSpace(manifest.graphicsDevice) ||
            string.IsNullOrWhiteSpace(manifest.graphicsApi))
            throw new InvalidOperationException("Runtime manifest is missing Unity/graphics provenance.");
        if (!string.Equals(manifest.projectColorSpace, "Linear", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Rendered repetition diagnostics require a Linear-color-space Unity capture.");
        if (manifest.captures == null || manifest.captures.Length != 3)
            throw new InvalidOperationException("Rendered repetition diagnostics require exactly hero, oblique and grazing captures.");

        RequireExactSet(manifest.captures.Select(x => x == null ? null : x.viewId).ToArray(),
            new[] { "hero", "oblique", "grazing" }, "runtime repetition capture viewIds");
    }

    private static void ResolveCrop(CaptureManifest manifest, string cropRef, out CaptureRecord capture, out CropRecord crop)
    {
        string[] parts = cropRef.Split('/');
        if (parts.Length != 2)
            throw new InvalidOperationException($"Invalid crop reference '{cropRef}'. Expected viewId/cropId.");

        capture = manifest.captures.SingleOrDefault(x => x != null && string.Equals(x.viewId, parts[0], StringComparison.Ordinal));
        if (capture == null)
            throw new InvalidOperationException($"Capture manifest is missing required view '{parts[0]}'.");
        crop = (capture.cropRecords ?? Array.Empty<CropRecord>())
            .SingleOrDefault(x => x != null && string.Equals(x.id, parts[1], StringComparison.Ordinal));
        if (crop == null)
            throw new InvalidOperationException($"Capture manifest is missing required 100% crop '{cropRef}'.");
        if (crop.resampled || crop.width <= 0 || crop.height <= 0 || string.IsNullOrWhiteSpace(crop.assetPath))
            throw new InvalidOperationException($"Capture manifest crop '{cropRef}' is not valid pixel-exact evidence.");
    }

    private static Texture2D LoadPng(string assetPath, int expectedWidth, int expectedHeight)
    {
        string absolute = AbsolutePath(assetPath);
        if (!File.Exists(absolute))
            throw new FileNotFoundException("Rendered repetition diagnostic image is missing.", assetPath);

        byte[] bytes = File.ReadAllBytes(absolute);
        var texture = new Texture2D(2, 2, TextureFormat.RGB24, false, false);
        if (!texture.LoadImage(bytes, false))
        {
            UnityEngine.Object.DestroyImmediate(texture);
            throw new InvalidOperationException($"Failed to decode PNG: {assetPath}");
        }
        if (texture.width != expectedWidth || texture.height != expectedHeight)
        {
            UnityEngine.Object.DestroyImmediate(texture);
            throw new InvalidOperationException(
                $"Rendered repetition image has wrong dimensions: {assetPath} = {texture.width}x{texture.height}, expected {expectedWidth}x{expectedHeight}.");
        }
        return texture;
    }

    private static void RequireExactSet(string[] actual, string[] expected, string label)
    {
        if (actual == null || actual.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException($"{label} is missing or contains blanks.");
        if (actual.Distinct(StringComparer.Ordinal).Count() != actual.Length)
            throw new InvalidOperationException($"{label} contains duplicates.");
        var a = new HashSet<string>(actual, StringComparer.Ordinal);
        var e = new HashSet<string>(expected, StringComparer.Ordinal);
        if (!a.SetEquals(e))
            throw new InvalidOperationException(
                $"{label} must be exactly [{string.Join(", ", e)}], got [{string.Join(", ", a)}].");
    }

    private static T LoadJson<T>(string assetPath)
    {
        string absolute = AbsolutePath(assetPath);
        if (!File.Exists(absolute))
            throw new FileNotFoundException($"Required QA file missing: {assetPath}");
        T value = JsonUtility.FromJson<T>(File.ReadAllText(absolute));
        if (value == null)
            throw new InvalidOperationException($"Failed to parse QA JSON: {assetPath}");
        return value;
    }

    private static void WriteJson<T>(string assetPath, T value)
    {
        string absolute = AbsolutePath(assetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute));
        File.WriteAllText(absolute, JsonUtility.ToJson(value, true));
    }

    private static string Sha256File(string assetPath)
    {
        using (SHA256 sha = SHA256.Create())
        using (FileStream stream = File.OpenRead(AbsolutePath(assetPath)))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static string AbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private struct ShiftSample
    {
        public int shiftPx;
        public int qualifiedComparisons;
        public float meanAbsLumaCode;
    }

    [Serializable]
    private sealed class RepetitionContract
    {
        public string schemaVersion;
        public int nativeWidth;
        public int nativeHeight;
        public bool actualUnityRenderRequired;
        public bool analyzePixelExact100PercentCrops;
        public int visualFidelityPointsAwarded;
        public bool automaticallyClearsCriticalDefect;
        public string criticalDefectId;
        public int sampleStridePx;
        public int gradientThresholdCode;
        public int minimumQualifiedComparisonsPerShift;
        public int minimumPeriodPx;
        public int maximumPeriodPx;
        public int periodStepPx;
        public int localNeighborhoodRadiusSteps;
        public float localProminenceWarning;
        public string[] requiredCropRefs;
    }

    [Serializable]
    private sealed class CaptureManifest
    {
        public string unityVersion;
        public string graphicsDevice;
        public string graphicsApi;
        public string projectColorSpace;
        public int width;
        public int height;
        public bool renderProducedByUnity;
        public CaptureRecord[] captures;
    }

    [Serializable]
    private sealed class CaptureRecord
    {
        public string viewId;
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
    private sealed class AxisDiagnostics
    {
        public string axis;
        public int strongestShiftPx;
        public float strongestMeanAbsLumaCode;
        public float localBaselineMeanAbsLumaCode;
        public float strongestLocalProminence;
        public int qualifiedComparisonsAtStrongestShift;
        public int evaluatedShiftCount;
        public bool warning;
    }

    [Serializable]
    private sealed class CropDiagnostics
    {
        public string viewId;
        public string cropId;
        public string assetPath;
        public string assetSha256;
        public int width;
        public int height;
        public AxisDiagnostics horizontal;
        public AxisDiagnostics vertical;
        public string[] warningFlags;
        public bool requiresHumanReview;
    }

    [Serializable]
    private sealed class DiagnosticsReport
    {
        public string schemaVersion;
        public string generatedUtc;
        public string status;
        public string sourceManifestPath;
        public string sourceManifestSha256;
        public string unityVersion;
        public string graphicsDevice;
        public string graphicsApi;
        public string projectColorSpace;
        public bool renderProducedByUnity;
        public int imageCount;
        public CropDiagnostics[] images;
        public int warningCount;
        public string[] warnings;
        public bool humanReviewRequired;
        public int visualFidelityPointsAwarded;
        public bool criticalDefectAutomaticallyCleared;
        public string criticalDefectId;
        public string note;
    }
}
