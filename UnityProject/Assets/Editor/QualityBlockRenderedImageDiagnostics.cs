using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Objective triage metrics for the actual post-tonemap native-4K benchmark PNGs and their exact
/// 100% crops. This class deliberately does not score Visual Fidelity. It exists to make clipping,
/// crushed shadows and suspiciously narrow display-range usage explicit before a reviewer assigns
/// Cinematic Image or Lighting points from the sealed render evidence.
/// </summary>
public static class QualityBlockRenderedImageDiagnostics
{
    private const string ManifestPath = "Assets/QA/4k_capture_manifest.json";
    private const string ContractPath = "Assets/QA/cinematic_render_diagnostics_contract.json";
    private const string ReportPath = "Assets/QA/cinematic_render_diagnostics.json";
    private const int NativeWidth = 3840;
    private const int NativeHeight = 2160;

    [MenuItem("NewTown/QA/Analyze Native 4K Cinematic Render")]
    public static void AnalyzeExistingCapture()
    {
        DiagnosticsContract contract = LoadJson<DiagnosticsContract>(ContractPath);
        ValidateContract(contract);

        CaptureManifest manifest = LoadJson<CaptureManifest>(ManifestPath);
        ValidateRuntimeManifest(manifest);

        var records = new List<ImageDiagnostics>();
        foreach (CaptureRecord capture in manifest.captures)
        {
            records.Add(AnalyzeImage(
                capture.assetPath,
                capture.viewId,
                "full_frame",
                string.Empty,
                capture.width,
                capture.height,
                contract));

            foreach (CropRecord crop in capture.cropRecords ?? Array.Empty<CropRecord>())
            {
                records.Add(AnalyzeImage(
                    crop.assetPath,
                    capture.viewId,
                    "100pct_crop",
                    crop.id,
                    crop.width,
                    crop.height,
                    contract));
            }
        }

        string[] warnings = records
            .Where(x => x.warningFlags != null)
            .SelectMany(x => x.warningFlags.Select(flag =>
                string.IsNullOrEmpty(x.cropId)
                    ? $"{x.viewId}: {flag}"
                    : $"{x.viewId}/{x.cropId}: {flag}"))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var report = new DiagnosticsReport
        {
            schemaVersion = "1.0",
            generatedUtc = DateTime.UtcNow.ToString("O"),
            unityVersion = manifest.unityVersion,
            graphicsDevice = manifest.graphicsDevice,
            graphicsApi = manifest.graphicsApi,
            projectColorSpace = manifest.projectColorSpace,
            sourceManifestPath = ManifestPath,
            renderProducedByUnity = true,
            imageCount = records.Count,
            images = records.ToArray(),
            warningCount = warnings.Length,
            warnings = warnings,
            humanReviewRequired = true,
            visualPointsAwarded = 0,
            visualFidelityStatus = "UNSCORED_DIAGNOSTICS_ONLY",
            note =
                "Display-space histogram/clipping diagnostics are triage evidence only. They cannot prove composition, " +
                "material realism, light transport, shadow quality, banding absence or filmic plausibility by themselves. " +
                "Review the exact sealed 3840x2160 frames and 100% crops before assigning any Visual Fidelity points."
        };

        WriteJson(ReportPath, report);
        AssetDatabase.Refresh();
        Debug.Log(
            $"Native 4K cinematic diagnostics written: images={records.Count}, warnings={warnings.Length}. " +
            "Visual Fidelity remains UNSCORED until actual render review.");
    }

    [MenuItem("NewTown/QA/Validate Cinematic Render Diagnostics Contract")]
    public static void ValidateContractConfigOnly()
    {
        DiagnosticsContract contract = LoadJson<DiagnosticsContract>(ContractPath);
        ValidateContract(contract);
        Debug.Log("Cinematic render diagnostics contract valid. No visual points are awarded by configuration.");
    }

    private static ImageDiagnostics AnalyzeImage(string assetPath, string viewId, string kind, string cropId,
        int expectedWidth, int expectedHeight, DiagnosticsContract contract)
    {
        Texture2D texture = LoadPng(assetPath, expectedWidth, expectedHeight);
        try
        {
            Color32[] pixels = texture.GetPixels32();
            if (pixels.Length != texture.width * texture.height)
                throw new InvalidOperationException($"Decoded pixel count mismatch for {assetPath}.");

            var lumaHistogram = new long[256];
            long blackClip = 0;
            long whiteClip = 0;
            long anyHighChannelClip = 0;
            long lowLuma = 0;
            long highLuma = 0;
            double lumaSum = 0.0;
            double saturationSum = 0.0;

            foreach (Color32 p in pixels)
            {
                int luma = Mathf.Clamp(Mathf.RoundToInt(0.2126f * p.r + 0.7152f * p.g + 0.0722f * p.b), 0, 255);
                lumaHistogram[luma]++;
                lumaSum += luma;

                int max = Math.Max(p.r, Math.Max(p.g, p.b));
                int min = Math.Min(p.r, Math.Min(p.g, p.b));
                saturationSum += max <= 0 ? 0.0 : (max - min) / (double)max;

                if (p.r <= contract.blackClipCode && p.g <= contract.blackClipCode && p.b <= contract.blackClipCode)
                    blackClip++;
                if (p.r >= contract.whiteClipCode && p.g >= contract.whiteClipCode && p.b >= contract.whiteClipCode)
                    whiteClip++;
                if (p.r >= contract.whiteClipCode || p.g >= contract.whiteClipCode || p.b >= contract.whiteClipCode)
                    anyHighChannelClip++;
                if (luma <= contract.lowLumaCode) lowLuma++;
                if (luma >= contract.highLumaCode) highLuma++;
            }

            long count = pixels.LongLength;
            float anyClipPct = Percent(anyHighChannelClip, count);
            float blackClipPct = Percent(blackClip, count);
            float whiteClipPct = Percent(whiteClip, count);
            float lowLumaPct = Percent(lowLuma, count);
            float highLumaPct = Percent(highLuma, count);
            int p01 = Percentile(lumaHistogram, count, 0.01f);
            int p50 = Percentile(lumaHistogram, count, 0.50f);
            int p99 = Percentile(lumaHistogram, count, 0.99f);

            var warnings = new List<string>();
            float clipWarning = string.Equals(kind, "full_frame", StringComparison.Ordinal)
                ? contract.fullFrameAnyChannelClipWarningPercent
                : contract.cropAnyChannelClipWarningPercent;
            float darkWarning = string.Equals(kind, "full_frame", StringComparison.Ordinal)
                ? contract.fullFrameLowLumaWarningPercent
                : contract.cropLowLumaWarningPercent;

            if (anyClipPct > clipWarning)
                warnings.Add($"any RGB channel reaches code {contract.whiteClipCode}+ on {anyClipPct:0.###}% of pixels (triage warning > {clipWarning:0.###}%).");
            if (lowLumaPct > darkWarning)
                warnings.Add($"luma is at/below code {contract.lowLumaCode} on {lowLumaPct:0.###}% of pixels (triage warning > {darkWarning:0.###}%).");
            if (p99 >= contract.p99HighlightWarningCode)
                warnings.Add($"99th-percentile display luma is {p99}/255; inspect highlight rolloff and channel clipping at 100%.");
            if (p50 <= contract.medianDarkWarningCode)
                warnings.Add($"median display luma is {p50}/255; inspect whether recesses/material separation are crushed.");

            return new ImageDiagnostics
            {
                viewId = viewId,
                kind = kind,
                cropId = cropId,
                assetPath = assetPath,
                width = texture.width,
                height = texture.height,
                pixelCount = count,
                meanDisplayLumaCode = (float)(lumaSum / count),
                p01DisplayLumaCode = p01,
                p50DisplayLumaCode = p50,
                p99DisplayLumaCode = p99,
                meanDisplaySaturation = (float)(saturationSum / count),
                blackClipPercent = blackClipPct,
                whiteClipPercent = whiteClipPct,
                anyHighChannelClipPercent = anyClipPct,
                lowLumaPercent = lowLumaPct,
                highLumaPercent = highLumaPct,
                warningFlags = warnings.ToArray(),
                requiresHumanReview = true
            };
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    private static void ValidateContract(DiagnosticsContract contract)
    {
        if (contract == null)
            throw new InvalidOperationException("Cinematic render diagnostics contract is null/unparseable.");
        if (contract.nativeWidth != NativeWidth || contract.nativeHeight != NativeHeight)
            throw new InvalidOperationException("Diagnostics contract must remain native 3840x2160.");
        if (!contract.actualRenderRequired || !contract.pixelExact100PercentCropsRequired)
            throw new InvalidOperationException("Diagnostics must require actual Unity renders and pixel-exact 100% crops.");
        if (contract.visualPointsAwarded != 0)
            throw new InvalidOperationException("Rendered-image diagnostics are forbidden from awarding Visual Fidelity points.");
        if (contract.blackClipCode < 0 || contract.blackClipCode > 8 ||
            contract.whiteClipCode < 247 || contract.whiteClipCode > 255)
            throw new InvalidOperationException("Clip-code diagnostics thresholds are outside the restrained display-space range.");
        if (contract.lowLumaCode < contract.blackClipCode || contract.lowLumaCode > 16 ||
            contract.highLumaCode > contract.whiteClipCode || contract.highLumaCode < 239)
            throw new InvalidOperationException("Low/high luma diagnostic codes are inconsistent.");
        if (contract.fullFrameAnyChannelClipWarningPercent <= 0f || contract.fullFrameAnyChannelClipWarningPercent > 10f ||
            contract.cropAnyChannelClipWarningPercent <= 0f || contract.cropAnyChannelClipWarningPercent > 15f)
            throw new InvalidOperationException("Highlight-clipping warning percentages are outside a reasonable triage range.");
        if (contract.fullFrameLowLumaWarningPercent <= 0f || contract.fullFrameLowLumaWarningPercent > 40f ||
            contract.cropLowLumaWarningPercent <= 0f || contract.cropLowLumaWarningPercent > 50f)
            throw new InvalidOperationException("Low-luma warning percentages are outside a reasonable triage range.");
    }

    private static void ValidateRuntimeManifest(CaptureManifest manifest)
    {
        if (manifest == null || !manifest.renderProducedByUnity)
            throw new InvalidOperationException("No actual Unity 4K render exists; cinematic diagnostics cannot run on the template manifest.");
        if (manifest.width != NativeWidth || manifest.height != NativeHeight)
            throw new InvalidOperationException("Cinematic diagnostics require native 3840x2160 capture evidence.");
        if (string.IsNullOrWhiteSpace(manifest.unityVersion) || string.IsNullOrWhiteSpace(manifest.graphicsDevice) ||
            string.IsNullOrWhiteSpace(manifest.graphicsApi))
            throw new InvalidOperationException("Runtime manifest is missing Unity/graphics provenance.");
        if (!string.Equals(manifest.projectColorSpace, "Linear", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Cinematic diagnostics require a Linear-color-space Unity capture.");
        if (manifest.captures == null || manifest.captures.Length != 3)
            throw new InvalidOperationException("Cinematic diagnostics require exactly hero, oblique and grazing captures.");

        string[] required = { "hero", "oblique", "grazing" };
        foreach (string view in required)
            if (manifest.captures.Count(x => x != null && x.viewId == view) != 1)
                throw new InvalidOperationException($"Runtime manifest is missing/duplicating required view '{view}'.");
    }

    private static Texture2D LoadPng(string assetPath, int expectedWidth, int expectedHeight)
    {
        string absolute = AbsolutePath(assetPath);
        if (!File.Exists(absolute))
            throw new FileNotFoundException("Rendered diagnostic image is missing.", assetPath);

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
                $"Rendered diagnostic image has wrong dimensions: {assetPath} = {texture.width}x{texture.height}, expected {expectedWidth}x{expectedHeight}.");
        }
        return texture;
    }

    private static int Percentile(long[] histogram, long count, float percentile)
    {
        long target = Math.Max(1, (long)Math.Ceiling(count * percentile));
        long cumulative = 0;
        for (int i = 0; i < histogram.Length; i++)
        {
            cumulative += histogram[i];
            if (cumulative >= target) return i;
        }
        return 255;
    }

    private static float Percent(long numerator, long denominator)
    {
        return denominator <= 0 ? 0f : (float)(100.0 * numerator / denominator);
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

    [Serializable]
    private sealed class DiagnosticsContract
    {
        public int nativeWidth;
        public int nativeHeight;
        public bool actualRenderRequired;
        public bool pixelExact100PercentCropsRequired;
        public int visualPointsAwarded;
        public int blackClipCode;
        public int whiteClipCode;
        public int lowLumaCode;
        public int highLumaCode;
        public float fullFrameAnyChannelClipWarningPercent;
        public float cropAnyChannelClipWarningPercent;
        public float fullFrameLowLumaWarningPercent;
        public float cropLowLumaWarningPercent;
        public int p99HighlightWarningCode;
        public int medianDarkWarningCode;
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
        public string assetPath;
        public int width;
        public int height;
        public CropRecord[] cropRecords;
    }

    [Serializable]
    private sealed class CropRecord
    {
        public string id;
        public string assetPath;
        public int width;
        public int height;
    }

    [Serializable]
    private sealed class DiagnosticsReport
    {
        public string schemaVersion;
        public string generatedUtc;
        public string unityVersion;
        public string graphicsDevice;
        public string graphicsApi;
        public string projectColorSpace;
        public string sourceManifestPath;
        public bool renderProducedByUnity;
        public int imageCount;
        public ImageDiagnostics[] images;
        public int warningCount;
        public string[] warnings;
        public bool humanReviewRequired;
        public int visualPointsAwarded;
        public string visualFidelityStatus;
        public string note;
    }

    [Serializable]
    private sealed class ImageDiagnostics
    {
        public string viewId;
        public string kind;
        public string cropId;
        public string assetPath;
        public int width;
        public int height;
        public long pixelCount;
        public float meanDisplayLumaCode;
        public int p01DisplayLumaCode;
        public int p50DisplayLumaCode;
        public int p99DisplayLumaCode;
        public float meanDisplaySaturation;
        public float blackClipPercent;
        public float whiteClipPercent;
        public float anyHighChannelClipPercent;
        public float lowLumaPercent;
        public float highLumaPercent;
        public string[] warningFlags;
        public bool requiresHumanReview;
    }
}
