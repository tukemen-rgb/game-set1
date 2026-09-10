using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Warning-only pixel-domain triage for the critical major_light_leak defect.
///
/// Source geometry and shadow-state QA cannot establish whether a bright seam, hole or leak is actually
/// visible after the real Unity render and filmic display transform. This diagnostic therefore measures
/// spatially clustered bright-on-dark discontinuities in selected canonical 100% crops. The metric is
/// intentionally non-authoritative: legitimate sun glints, sky apertures and reflective edges can look
/// similar numerically, so it never awards points, never clears the defect and never asserts the defect.
/// Human inspection of the exact SHA-256-bound pixels remains mandatory.
/// </summary>
public static class QualityBlockRenderedLightLeakDiagnostics
{
    private const string ManifestPath = "Assets/QA/4k_capture_manifest.json";
    private const string ContractPath = "Assets/QA/rendered_light_leak_diagnostics_contract.json";
    private const string ReportPath = "Assets/QA/rendered_light_leak_diagnostics.json";
    private const int NativeWidth = 3840;
    private const int NativeHeight = 2160;

    private static readonly string[] CanonicalCropRefs =
    {
        "hero/facade_center",
        "hero/ground_contact",
        "oblique/construction_depth",
        "oblique/balcony_services",
        "grazing/material_grazing"
    };

    [MenuItem("NewTown/QA/Analyze Native 4K Light-Leak Risk")]
    public static void AnalyzeExistingCapture()
    {
        LightLeakContract contract = LoadJson<LightLeakContract>(ContractPath);
        ValidateContract(contract);
        CaptureManifest manifest = LoadJson<CaptureManifest>(ManifestPath);
        ValidateRuntimeManifest(manifest);

        var images = new List<CropDiagnostics>();
        foreach (string cropRef in contract.requiredCropRefs)
        {
            ResolveCrop(manifest, cropRef, out CaptureRecord capture, out CropRecord crop);
            Texture2D texture = LoadPng(crop.assetPath, crop.width, crop.height);
            try
            {
                images.Add(AnalyzeCrop(texture, capture.viewId, crop, contract));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        string[] warnings = images
            .Where(x => x.warning)
            .Select(x =>
                $"{x.viewId}/{x.cropId}: bright-on-dark candidates={x.candidatePercent:0.####}% " +
                $"clusterBlocks={x.suspiciousBlockCount}, strongestBlockCandidates={x.strongestBlockCandidateCount}; " +
                "inspect geometry closure/shadowing/depth/transparent sorting at 100%.")
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
            imageCount = images.Count,
            images = images.ToArray(),
            warningCount = warnings.Length,
            warnings = warnings,
            humanReviewRequired = true,
            visualFidelityPointsAwarded = 0,
            criticalDefectAutomaticallyCleared = false,
            criticalDefectAutomaticallyAsserted = false,
            criticalDefectId = "major_light_leak",
            note =
                "Bright-on-dark clustering is triage evidence only. A warning is not an automatic defect finding, " +
                "and zero warnings cannot clear major_light_leak. Review the exact sealed native-4K pixels and physical construction context."
        };

        WriteJson(ReportPath, report);
        AssetDatabase.Refresh();
        Debug.Log(
            $"Native 4K light-leak diagnostics written: crops={images.Count}, warnings={warnings.Length}. " +
            "Visual Fidelity remains UNSCORED and major_light_leak remains human-reviewed.");
    }

    [MenuItem("NewTown/QA/Validate Rendered Light-Leak Diagnostics Contract")]
    public static void ValidateContractConfigOnly()
    {
        ValidateContract(LoadJson<LightLeakContract>(ContractPath));
        Debug.Log("Rendered light-leak diagnostics contract valid. Configuration awards 0 Visual Fidelity points.");
    }

    /// <summary>
    /// Scoring precondition only. Proves that the latest warning-only report was generated from the
    /// current runtime manifest and current bytes of every required canonical 100% crop.
    /// </summary>
    public static void ValidateLatestReportForScoring()
    {
        LightLeakContract contract = LoadJson<LightLeakContract>(ContractPath);
        ValidateContract(contract);
        CaptureManifest manifest = LoadJson<CaptureManifest>(ManifestPath);
        ValidateRuntimeManifest(manifest);

        if (!File.Exists(AbsolutePath(ReportPath)))
            throw new FileNotFoundException(
                "Rendered light-leak diagnostics are missing. Analyze the actual native-4K capture before Visual Fidelity scoring.",
                ReportPath);

        DiagnosticsReport report = LoadJson<DiagnosticsReport>(ReportPath);
        if (!string.Equals(report.schemaVersion, "1.0", StringComparison.Ordinal) ||
            !string.Equals(report.status, "TRIAGE_COMPLETE_HUMAN_REVIEW_REQUIRED", StringComparison.Ordinal))
            throw new InvalidOperationException("Rendered light-leak diagnostics report has an unexpected schema/status.");
        if (!report.renderProducedByUnity || !report.humanReviewRequired || report.visualFidelityPointsAwarded != 0 ||
            report.criticalDefectAutomaticallyCleared || report.criticalDefectAutomaticallyAsserted)
            throw new InvalidOperationException(
                "Rendered light-leak diagnostics weakened actual-render/human-review/zero-point/non-authoritative rules.");
        if (!string.Equals(report.criticalDefectId, "major_light_leak", StringComparison.Ordinal))
            throw new InvalidOperationException("Rendered light-leak diagnostics report is bound to the wrong critical defect.");
        if (!string.Equals(report.sourceManifestPath, ManifestPath, StringComparison.Ordinal) ||
            !string.Equals(report.sourceManifestSha256, Sha256File(ManifestPath), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Rendered light-leak diagnostics do not bind the current capture manifest bytes.");
        if (!string.Equals(report.unityVersion, manifest.unityVersion, StringComparison.Ordinal) ||
            !string.Equals(report.graphicsDevice, manifest.graphicsDevice, StringComparison.Ordinal) ||
            !string.Equals(report.graphicsApi, manifest.graphicsApi, StringComparison.Ordinal) ||
            !string.Equals(report.projectColorSpace, manifest.projectColorSpace, StringComparison.Ordinal))
            throw new InvalidOperationException("Rendered light-leak diagnostics runtime provenance differs from the current capture manifest.");
        DateTimeOffset generated;
        if (!DateTimeOffset.TryParse(report.generatedUtc, out generated))
            throw new InvalidOperationException("Rendered light-leak diagnostics generatedUtc is missing/invalid.");
        if (report.images == null || report.images.Length != contract.requiredCropRefs.Length ||
            report.imageCount != report.images.Length)
            throw new InvalidOperationException(
                $"Rendered light-leak diagnostics must contain exactly {contract.requiredCropRefs.Length} required crops.");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string cropRef in contract.requiredCropRefs)
        {
            ResolveCrop(manifest, cropRef, out CaptureRecord capture, out CropRecord crop);
            CropDiagnostics diagnostic = report.images.SingleOrDefault(x =>
                x != null && string.Equals(x.viewId, capture.viewId, StringComparison.Ordinal) &&
                string.Equals(x.cropId, crop.id, StringComparison.Ordinal));
            if (diagnostic == null)
                throw new InvalidOperationException($"Rendered light-leak diagnostics are missing required crop {cropRef}.");
            if (!seen.Add(cropRef))
                throw new InvalidOperationException($"Rendered light-leak diagnostics duplicate crop {cropRef}.");
            if (!string.Equals(diagnostic.assetPath, crop.assetPath, StringComparison.Ordinal) ||
                diagnostic.width != crop.width || diagnostic.height != crop.height)
                throw new InvalidOperationException($"Rendered light-leak diagnostics path/dimensions drifted for {cropRef}.");
            if (!string.Equals(diagnostic.assetSha256, Sha256File(crop.assetPath), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Rendered light-leak diagnostics crop bytes changed after analysis: {cropRef}.");
            if (!diagnostic.requiresHumanReview || diagnostic.analyzedSampleCount <= 0 ||
                diagnostic.candidateSampleCount < 0 || diagnostic.suspiciousBlockCount < 0)
                throw new InvalidOperationException($"Rendered light-leak diagnostics are structurally incomplete for {cropRef}.");
        }

        Debug.Log(
            "Rendered light-leak diagnostics are current for the exact manifest/crop bytes. Warnings remain triage only; " +
            "major_light_leak still requires explicit human pixel review.");
    }

    private static CropDiagnostics AnalyzeCrop(Texture2D texture, string viewId, CropRecord crop, LightLeakContract contract)
    {
        Color32[] pixels = texture.GetPixels32();
        byte[] luma = BuildLuma(pixels);
        int width = texture.width;
        int height = texture.height;
        int blockCols = Mathf.CeilToInt(width / (float)contract.blockSizePx);
        int blockRows = Mathf.CeilToInt(height / (float)contract.blockSizePx);
        int[] blockCandidates = new int[blockCols * blockRows];

        int analyzed = 0;
        int candidates = 0;
        int border = Mathf.Max(contract.borderExclusionPx, contract.outerRadiusPx + 1);

        for (int y = border; y < height - border; y += contract.sampleStridePx)
        {
            for (int x = border; x < width - border; x += contract.sampleStridePx)
            {
                analyzed++;
                int center = luma[y * width + x];
                if (center < contract.brightCodeMinimum)
                    continue;

                RingStats ring = MeasureRing(luma, width, height, x, y, contract);
                if (ring.sampleCount <= 0)
                    continue;
                if (ring.medianLuma > contract.darkRingMedianMaximum)
                    continue;
                if (center - ring.medianLuma < contract.minimumBrightDarkContrastCode)
                    continue;
                if (ring.darkFraction < contract.minimumDarkRingFraction)
                    continue;

                candidates++;
                int bx = x / contract.blockSizePx;
                int by = y / contract.blockSizePx;
                blockCandidates[by * blockCols + bx]++;
            }
        }

        var clusters = new List<BlockCluster>();
        int strongest = 0;
        for (int by = 0; by < blockRows; by++)
        {
            for (int bx = 0; bx < blockCols; bx++)
            {
                int count = blockCandidates[by * blockCols + bx];
                if (count < contract.minimumCandidatePixelsPerBlock)
                    continue;
                strongest = Mathf.Max(strongest, count);
                clusters.Add(new BlockCluster
                {
                    x = bx * contract.blockSizePx,
                    y = by * contract.blockSizePx,
                    width = Mathf.Min(contract.blockSizePx, width - bx * contract.blockSizePx),
                    height = Mathf.Min(contract.blockSizePx, height - by * contract.blockSizePx),
                    candidateSampleCount = count
                });
            }
        }

        BlockCluster[] strongestClusters = clusters
            .OrderByDescending(x => x.candidateSampleCount)
            .Take(16)
            .ToArray();
        float candidatePercent = analyzed <= 0 ? 0f : (float)(100.0 * candidates / analyzed);
        bool warning = candidatePercent >= contract.candidatePercentWarning || clusters.Count > 0;

        return new CropDiagnostics
        {
            viewId = viewId,
            cropId = crop.id,
            assetPath = crop.assetPath,
            assetSha256 = Sha256File(crop.assetPath),
            width = width,
            height = height,
            analyzedSampleCount = analyzed,
            candidateSampleCount = candidates,
            candidatePercent = candidatePercent,
            suspiciousBlockCount = clusters.Count,
            strongestBlockCandidateCount = strongest,
            strongestBlocks = strongestClusters,
            warning = warning,
            requiresHumanReview = true
        };
    }

    private static RingStats MeasureRing(byte[] luma, int width, int height, int cx, int cy, LightLeakContract contract)
    {
        var values = new List<int>(64);
        int dark = 0;
        int inner2 = contract.innerRadiusPx * contract.innerRadiusPx;
        int outer2 = contract.outerRadiusPx * contract.outerRadiusPx;
        int ringStep = Mathf.Max(2, contract.sampleStridePx * 2);

        for (int dy = -contract.outerRadiusPx; dy <= contract.outerRadiusPx; dy += ringStep)
        {
            for (int dx = -contract.outerRadiusPx; dx <= contract.outerRadiusPx; dx += ringStep)
            {
                int d2 = dx * dx + dy * dy;
                if (d2 <= inner2 || d2 > outer2)
                    continue;
                int value = luma[(cy + dy) * width + (cx + dx)];
                values.Add(value);
                if (value <= contract.darkNeighborCodeMaximum)
                    dark++;
            }
        }

        if (values.Count == 0)
            return new RingStats { sampleCount = 0, medianLuma = 255, darkFraction = 0f };

        values.Sort();
        int middle = values.Count / 2;
        int median = (values.Count & 1) == 1
            ? values[middle]
            : (values[middle - 1] + values[middle]) / 2;
        return new RingStats
        {
            sampleCount = values.Count,
            medianLuma = median,
            darkFraction = dark / (float)values.Count
        };
    }

    private static byte[] BuildLuma(Color32[] pixels)
    {
        var result = new byte[pixels.Length];
        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 p = pixels[i];
            int code = (54 * p.r + 183 * p.g + 19 * p.b + 128) >> 8;
            result[i] = (byte)Mathf.Clamp(code, 0, 255);
        }
        return result;
    }

    private static void ValidateContract(LightLeakContract contract)
    {
        if (contract == null)
            throw new InvalidOperationException("Rendered light-leak diagnostics contract is null/unparseable.");
        if (!string.Equals(contract.schemaVersion, "1.0", StringComparison.Ordinal))
            throw new InvalidOperationException($"Unexpected rendered light-leak diagnostics schemaVersion '{contract.schemaVersion}'.");
        if (contract.nativeWidth != NativeWidth || contract.nativeHeight != NativeHeight)
            throw new InvalidOperationException("Rendered light-leak diagnostics must remain native 3840x2160.");
        if (!contract.actualUnityRenderRequired || !contract.analyzePixelExact100PercentCrops)
            throw new InvalidOperationException("Rendered light-leak diagnostics require actual Unity pixel-exact 100% crops.");
        if (contract.visualFidelityPointsAwarded != 0 || contract.automaticallyClearsCriticalDefect || contract.automaticallyAssertsCriticalDefect)
            throw new InvalidOperationException("Light-leak diagnostics may not award points, clear the defect or assert the defect automatically.");
        if (!string.Equals(contract.criticalDefectId, "major_light_leak", StringComparison.Ordinal))
            throw new InvalidOperationException("Rendered light-leak diagnostics must remain bound to major_light_leak.");
        RequireExactSet(contract.requiredCropRefs, CanonicalCropRefs, "light-leak requiredCropRefs");

        if (contract.sampleStridePx < 1 || contract.sampleStridePx > 8 ||
            contract.borderExclusionPx < 8 || contract.borderExclusionPx > 64 ||
            contract.innerRadiusPx < 1 || contract.outerRadiusPx <= contract.innerRadiusPx || contract.outerRadiusPx > 32)
            throw new InvalidOperationException("Light-leak spatial sampling/radius settings are outside the guarded range.");
        if (contract.brightCodeMinimum < 144 || contract.brightCodeMinimum > 240 ||
            contract.darkRingMedianMaximum < 24 || contract.darkRingMedianMaximum > 144 ||
            contract.minimumBrightDarkContrastCode < 48 || contract.minimumBrightDarkContrastCode > 160)
            throw new InvalidOperationException("Light-leak display-luma thresholds are outside the guarded range.");
        if (contract.minimumDarkRingFraction < 0.5f || contract.minimumDarkRingFraction > 0.95f ||
            contract.darkNeighborCodeMaximum < contract.darkRingMedianMaximum || contract.darkNeighborCodeMaximum > 160)
            throw new InvalidOperationException("Light-leak dark-neighborhood thresholds are inconsistent.");
        if (contract.blockSizePx < 16 || contract.blockSizePx > 96 ||
            contract.minimumCandidatePixelsPerBlock < 2 || contract.minimumCandidatePixelsPerBlock > 64 ||
            contract.candidatePercentWarning <= 0f || contract.candidatePercentWarning > 1f)
            throw new InvalidOperationException("Light-leak clustering/warning settings are outside the guarded range.");
    }

    private static void ValidateRuntimeManifest(CaptureManifest manifest)
    {
        if (manifest == null || !manifest.renderProducedByUnity)
            throw new InvalidOperationException("No actual Unity 4K render exists; light-leak diagnostics cannot run on the template manifest.");
        if (manifest.width != NativeWidth || manifest.height != NativeHeight)
            throw new InvalidOperationException("Light-leak diagnostics require native 3840x2160 evidence.");
        if (string.IsNullOrWhiteSpace(manifest.unityVersion) || string.IsNullOrWhiteSpace(manifest.graphicsDevice) ||
            string.IsNullOrWhiteSpace(manifest.graphicsApi))
            throw new InvalidOperationException("Runtime manifest is missing Unity/graphics provenance.");
        if (!string.Equals(manifest.projectColorSpace, "Linear", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Light-leak diagnostics require a Linear-color-space Unity capture.");
        if (manifest.captures == null || manifest.captures.Length != 3)
            throw new InvalidOperationException("Light-leak diagnostics require exactly hero, oblique and grazing captures.");
    }

    private static void ResolveCrop(CaptureManifest manifest, string cropRef, out CaptureRecord capture, out CropRecord crop)
    {
        string[] parts = cropRef.Split('/');
        if (parts.Length != 2)
            throw new InvalidOperationException($"Invalid crop reference '{cropRef}'. Expected viewId/cropId.");
        capture = manifest.captures.SingleOrDefault(x => x != null && string.Equals(x.viewId, parts[0], StringComparison.Ordinal));
        if (capture == null)
            throw new InvalidOperationException($"Capture manifest is missing required view '{parts[0]}'.");
        crop = (capture.cropRecords ?? Array.Empty<CropRecord>()).SingleOrDefault(x =>
            x != null && string.Equals(x.id, parts[1], StringComparison.Ordinal));
        if (crop == null)
            throw new InvalidOperationException($"Capture manifest is missing required crop '{cropRef}'.");
        if (crop.resampled)
            throw new InvalidOperationException($"Light-leak diagnostic crop must be pixel-exact and unresampled: {cropRef}.");
    }

    private static Texture2D LoadPng(string assetPath, int expectedWidth, int expectedHeight)
    {
        string absolute = AbsolutePath(assetPath);
        if (!File.Exists(absolute))
            throw new FileNotFoundException("Rendered light-leak diagnostic image is missing.", assetPath);
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
                $"Rendered light-leak image has wrong dimensions: {assetPath} = {texture.width}x{texture.height}, expected {expectedWidth}x{expectedHeight}.");
        }
        return texture;
    }

    private static string Sha256File(string assetPath)
    {
        string absolute = AbsolutePath(assetPath);
        if (!File.Exists(absolute))
            throw new FileNotFoundException("SHA-256 source file is missing.", assetPath);
        using (SHA256 sha = SHA256.Create())
        using (FileStream stream = File.OpenRead(absolute))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static void RequireExactSet(string[] actual, string[] expected, string label)
    {
        if (actual == null || actual.Length != expected.Length || actual.Any(string.IsNullOrWhiteSpace) ||
            actual.Distinct(StringComparer.Ordinal).Count() != actual.Length)
            throw new InvalidOperationException($"{label} is missing, duplicated or has the wrong count.");
        var set = new HashSet<string>(actual, StringComparer.Ordinal);
        if (!set.SetEquals(expected))
            throw new InvalidOperationException($"{label} drifted from the canonical evidence set.");
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

    private static string AbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private struct RingStats
    {
        public int sampleCount;
        public int medianLuma;
        public float darkFraction;
    }

    [Serializable]
    private sealed class LightLeakContract
    {
        public string schemaVersion;
        public int nativeWidth;
        public int nativeHeight;
        public bool actualUnityRenderRequired;
        public bool analyzePixelExact100PercentCrops;
        public int visualFidelityPointsAwarded;
        public bool automaticallyClearsCriticalDefect;
        public bool automaticallyAssertsCriticalDefect;
        public string criticalDefectId;
        public int sampleStridePx;
        public int borderExclusionPx;
        public int innerRadiusPx;
        public int outerRadiusPx;
        public int brightCodeMinimum;
        public int darkRingMedianMaximum;
        public int minimumBrightDarkContrastCode;
        public float minimumDarkRingFraction;
        public int darkNeighborCodeMaximum;
        public int blockSizePx;
        public int minimumCandidatePixelsPerBlock;
        public float candidatePercentWarning;
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
        public bool criticalDefectAutomaticallyAsserted;
        public string criticalDefectId;
        public string note;
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
        public int analyzedSampleCount;
        public int candidateSampleCount;
        public float candidatePercent;
        public int suspiciousBlockCount;
        public int strongestBlockCandidateCount;
        public BlockCluster[] strongestBlocks;
        public bool warning;
        public bool requiresHumanReview;
    }

    [Serializable]
    private sealed class BlockCluster
    {
        public int x;
        public int y;
        public int width;
        public int height;
        public int candidateSampleCount;
    }
}
