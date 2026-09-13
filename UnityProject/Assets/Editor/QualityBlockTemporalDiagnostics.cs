using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Non-scoring diagnostics for the sealed native-4K temporal probes.
/// The analyzer never clears severe_aliasing_or_shimmer or visible_lod_pop and never awards Visual Fidelity points.
/// It ranks frame transitions that deserve 100%-pixel manual inspection by measuring luminance histograms,
/// edge/high-frequency energy and a small translation-compensated luma error for the subpixel grazing probe.
/// </summary>
public static class QualityBlockTemporalDiagnostics
{
    private const string ContractPath = "Assets/QA/temporal_diagnostics_contract.json";
    private const string ManifestPath = "Assets/QA/temporal_stability_manifest.json";
    private const string OutputPath = "Assets/QA/temporal_stability_diagnostics.json";

    [MenuItem("NewTown/QA/Validate Temporal Diagnostics Contract")]
    public static void ValidateContractConfigOnly()
    {
        DiagnosticsContract contract = LoadJson<DiagnosticsContract>(ContractPath);
        ValidateContract(contract);
        Debug.Log("Temporal diagnostics contract valid. Metrics are advisory triage only and award zero Visual Fidelity points.");
    }

    [MenuItem("NewTown/QA/Analyze Latest Temporal Stability Evidence")]
    public static void AnalyzeLatestEvidence()
    {
        DiagnosticsContract contract = LoadJson<DiagnosticsContract>(ContractPath);
        ValidateContract(contract);

        // Refuse to analyze unsealed, edited or incomplete evidence. The capture validator recomputes
        // manifest/receipt/frame/crop hashes and verifies the required native-4K probe set first.
        QualityBlockTemporalStabilityCapture.ValidateLatestTemporalEvidence();

        TemporalManifest manifest = LoadJson<TemporalManifest>(ManifestPath);
        if (manifest == null || string.IsNullOrWhiteSpace(manifest.captureSessionId) || manifest.probes == null)
            throw new InvalidOperationException("Temporal diagnostics refused: latest temporal manifest is incomplete.");

        var probeDiagnostics = new List<ProbeDiagnostics>();
        var reviewCandidates = new List<ReviewCandidate>();

        foreach (TemporalProbe probe in manifest.probes)
        {
            if (probe == null || probe.frames == null || probe.frames.Length < 2)
                throw new InvalidOperationException("Temporal diagnostics refused: a probe contains fewer than two frames.");

            FrameData[] frames = probe.frames
                .OrderBy(x => x.index)
                .Select(x => LoadFrameData(x, contract.sampleStride))
                .ToArray();

            try
            {
                ProbeDiagnostics diagnostics = AnalyzeProbe(probe.id, frames, contract);
                probeDiagnostics.Add(diagnostics);
                reviewCandidates.AddRange(diagnostics.reviewCandidates ?? Array.Empty<ReviewCandidate>());
            }
            finally
            {
                foreach (FrameData frame in frames)
                    frame.Dispose();
            }
        }

        var output = new DiagnosticsOutput
        {
            schemaVersion = "1.0",
            generatedUtc = DateTime.UtcNow.ToString("O"),
            captureSessionId = manifest.captureSessionId,
            sourceManifestAssetPath = ManifestPath,
            source = "sealed native-4K temporal 100% crops; no resampling or interpolation",
            manualReviewRequired = true,
            automaticVisualPoints = 0,
            criticalDefectsCleared = false,
            probeDiagnostics = probeDiagnostics.ToArray(),
            reviewCandidates = reviewCandidates
                .OrderByDescending(x => x.advisorySpikeRatio)
                .ThenBy(x => x.probeId, StringComparer.Ordinal)
                .ThenBy(x => x.fromFrame)
                .ToArray(),
            note = "Diagnostics only prioritize exact frame transitions for manual 100%-pixel review. A low metric does not prove absence of shimmer or LOD pop, and a high metric is not itself a critical-defect finding."
        };

        WriteJson(OutputPath, output);
        AssetDatabase.Refresh();
        Debug.Log(
            $"Temporal diagnostics generated for session {manifest.captureSessionId}: probes={probeDiagnostics.Count}, " +
            $"reviewCandidates={output.reviewCandidates.Length}. Visual Fidelity remains UNSCORED_REVIEW_REQUIRED.");
    }

    private static ProbeDiagnostics AnalyzeProbe(string probeId, FrameData[] frames, DiagnosticsContract contract)
    {
        var frameMetrics = frames.Select(x => x.metrics).ToArray();
        var transitions = new List<TransitionMetrics>();
        var indicators = new List<float>();

        for (int i = 0; i < frames.Length - 1; i++)
        {
            FrameData a = frames[i];
            FrameData b = frames[i + 1];
            float histogramL1 = HistogramL1(a.histogram, b.histogram);
            float edgeRelativeDelta = RelativeDelta(a.metrics.edgeEnergy, b.metrics.edgeEnergy);
            float laplacianRelativeDelta = RelativeDelta(a.metrics.laplacianEnergy, b.metrics.laplacianEnergy);

            int shiftRadius = probeId == "subpixel_grazing" ? contract.subpixelAlignmentSearchRadiusPixels : 0;
            AlignmentResult alignment = ComputeBestAlignment(a, b, shiftRadius, contract.alignmentSampleStride);

            float indicator = probeId == "subpixel_grazing"
                ? 0.70f * alignment.meanAbsoluteError + 0.30f * laplacianRelativeDelta
                : 0.60f * histogramL1 + 0.40f * edgeRelativeDelta;
            indicators.Add(indicator);

            transitions.Add(new TransitionMetrics
            {
                fromFrame = a.metrics.index,
                toFrame = b.metrics.index,
                meanLumaDelta = Mathf.Abs(a.metrics.meanLuma - b.metrics.meanLuma),
                histogramL1 = histogramL1,
                edgeEnergyRelativeDelta = edgeRelativeDelta,
                laplacianEnergyRelativeDelta = laplacianRelativeDelta,
                compensatedLumaMae = alignment.meanAbsoluteError,
                bestShiftX = alignment.shiftX,
                bestShiftY = alignment.shiftY,
                diagnosticIndicator = indicator,
                advisorySpikeRatio = 0f
            });
        }

        float medianIndicator = Median(indicators);
        var candidates = new List<ReviewCandidate>();
        for (int i = 0; i < transitions.Count; i++)
        {
            TransitionMetrics transition = transitions[i];
            transition.advisorySpikeRatio = transition.diagnosticIndicator / Mathf.Max(medianIndicator, 0.0001f);
            transitions[i] = transition;

            bool spike = transition.advisorySpikeRatio >= contract.advisoryTransitionSpikeRatio;
            bool highFrequencyJump = transition.laplacianEnergyRelativeDelta >= contract.advisoryHighFrequencyDeltaRatio;
            if (spike || highFrequencyJump)
            {
                candidates.Add(new ReviewCandidate
                {
                    probeId = probeId,
                    fromFrame = transition.fromFrame,
                    toFrame = transition.toFrame,
                    advisorySpikeRatio = transition.advisorySpikeRatio,
                    reason = BuildCandidateReason(spike, highFrequencyJump),
                    manualFindingRequired = true
                });
            }
        }

        return new ProbeDiagnostics
        {
            probeId = probeId,
            frameCount = frames.Length,
            medianTransitionIndicator = medianIndicator,
            frameMetrics = frameMetrics,
            transitions = transitions.ToArray(),
            reviewCandidates = candidates.ToArray(),
            automaticPassFail = "NONE_MANUAL_REVIEW_REQUIRED"
        };
    }

    private static string BuildCandidateReason(bool spike, bool highFrequencyJump)
    {
        if (spike && highFrequencyJump)
            return "transition metric spike plus high-frequency-energy jump; inspect both sealed 100% crops for shimmer/LOD discontinuity";
        if (spike)
            return "transition metric spike relative to the probe median; inspect both sealed 100% crops";
        return "high-frequency-energy jump; inspect both sealed 100% crops for unstable fine detail";
    }

    private static FrameData LoadFrameData(TemporalFrame frame, int sampleStride)
    {
        if (frame == null || frame.crop == null || string.IsNullOrWhiteSpace(frame.crop.assetPath))
            throw new InvalidOperationException("Temporal diagnostics refused: frame crop metadata is missing.");

        string absolute = AbsolutePath(frame.crop.assetPath);
        if (!File.Exists(absolute))
            throw new FileNotFoundException("Temporal diagnostics crop missing", frame.crop.assetPath);

        byte[] bytes = File.ReadAllBytes(absolute);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
        if (!ImageConversion.LoadImage(texture, bytes, false))
        {
            UnityEngine.Object.DestroyImmediate(texture);
            throw new InvalidOperationException($"Could not decode temporal crop {frame.crop.assetPath}.");
        }

        Color32[] pixels = texture.GetPixels32();
        int width = texture.width;
        int height = texture.height;
        if (width != frame.crop.width || height != frame.crop.height)
        {
            UnityEngine.Object.DestroyImmediate(texture);
            throw new InvalidOperationException(
                $"Temporal diagnostics crop dimensions changed: {frame.crop.assetPath} is {width}x{height}, manifest says {frame.crop.width}x{frame.crop.height}.");
        }

        float[] histogram = BuildHistogram(pixels, width, height, sampleStride, 16);
        FrameMetrics metrics = ComputeFrameMetrics(frame.index, pixels, width, height, sampleStride);
        return new FrameData(texture, pixels, width, height, histogram, metrics);
    }

    private static FrameMetrics ComputeFrameMetrics(int index, Color32[] pixels, int width, int height, int stride)
    {
        double sumLuma = 0.0;
        double sumEdge = 0.0;
        double sumLap = 0.0;
        int samples = 0;
        int edgeSamples = 0;
        int lapSamples = 0;

        for (int y = stride; y < height - stride; y += stride)
        {
            int row = y * width;
            for (int x = stride; x < width - stride; x += stride)
            {
                float c = Luma(pixels[row + x]);
                sumLuma += c;
                samples++;

                float right = Luma(pixels[row + x + stride]);
                float up = Luma(pixels[(y + stride) * width + x]);
                sumEdge += 0.5f * (Mathf.Abs(c - right) + Mathf.Abs(c - up));
                edgeSamples++;

                float left = Luma(pixels[row + x - stride]);
                float down = Luma(pixels[(y - stride) * width + x]);
                sumLap += Mathf.Abs(4f * c - left - right - up - down);
                lapSamples++;
            }
        }

        return new FrameMetrics
        {
            index = index,
            meanLuma = samples > 0 ? (float)(sumLuma / samples) : 0f,
            edgeEnergy = edgeSamples > 0 ? (float)(sumEdge / edgeSamples) : 0f,
            laplacianEnergy = lapSamples > 0 ? (float)(sumLap / lapSamples) : 0f
        };
    }

    private static float[] BuildHistogram(Color32[] pixels, int width, int height, int stride, int bins)
    {
        var histogram = new float[bins];
        int count = 0;
        for (int y = 0; y < height; y += stride)
        {
            int row = y * width;
            for (int x = 0; x < width; x += stride)
            {
                int bin = Mathf.Clamp(Mathf.FloorToInt(Luma(pixels[row + x]) * bins), 0, bins - 1);
                histogram[bin] += 1f;
                count++;
            }
        }

        if (count > 0)
            for (int i = 0; i < histogram.Length; i++)
                histogram[i] /= count;
        return histogram;
    }

    private static AlignmentResult ComputeBestAlignment(FrameData a, FrameData b, int radius, int sampleStride)
    {
        float best = float.PositiveInfinity;
        int bestX = 0;
        int bestY = 0;

        for (int sy = -radius; sy <= radius; sy++)
        {
            for (int sx = -radius; sx <= radius; sx++)
            {
                int minX = Mathf.Max(0, -sx);
                int maxX = Mathf.Min(a.width, b.width - sx);
                int minY = Mathf.Max(0, -sy);
                int maxY = Mathf.Min(a.height, b.height - sy);
                if (maxX <= minX || maxY <= minY)
                    continue;

                double sumA = 0.0;
                double sumB = 0.0;
                int count = 0;
                for (int y = minY; y < maxY; y += sampleStride)
                {
                    int rowA = y * a.width;
                    int rowB = (y + sy) * b.width;
                    for (int x = minX; x < maxX; x += sampleStride)
                    {
                        sumA += Luma(a.pixels[rowA + x]);
                        sumB += Luma(b.pixels[rowB + x + sx]);
                        count++;
                    }
                }
                if (count == 0)
                    continue;

                float meanA = (float)(sumA / count);
                float meanB = (float)(sumB / count);
                double error = 0.0;
                for (int y = minY; y < maxY; y += sampleStride)
                {
                    int rowA = y * a.width;
                    int rowB = (y + sy) * b.width;
                    for (int x = minX; x < maxX; x += sampleStride)
                    {
                        float la = Luma(a.pixels[rowA + x]) - meanA;
                        float lb = Luma(b.pixels[rowB + x + sx]) - meanB;
                        error += Mathf.Abs(la - lb);
                    }
                }

                float mae = (float)(error / count);
                if (mae < best)
                {
                    best = mae;
                    bestX = sx;
                    bestY = sy;
                }
            }
        }

        if (float.IsInfinity(best))
            best = 0f;
        return new AlignmentResult { meanAbsoluteError = best, shiftX = bestX, shiftY = bestY };
    }

    private static float HistogramL1(float[] a, float[] b)
    {
        if (a == null || b == null || a.Length != b.Length)
            throw new InvalidOperationException("Temporal histogram bins are inconsistent.");
        float sum = 0f;
        for (int i = 0; i < a.Length; i++)
            sum += Mathf.Abs(a[i] - b[i]);
        return 0.5f * sum;
    }

    private static float RelativeDelta(float a, float b) => Mathf.Abs(a - b) / Mathf.Max(Mathf.Min(a, b), 0.0001f);

    private static float Median(List<float> values)
    {
        if (values == null || values.Count == 0)
            return 0f;
        float[] sorted = values.OrderBy(x => x).ToArray();
        int middle = sorted.Length / 2;
        return sorted.Length % 2 == 0 ? 0.5f * (sorted[middle - 1] + sorted[middle]) : sorted[middle];
    }

    private static float Luma(Color32 p) => (0.2126f * p.r + 0.7152f * p.g + 0.0722f * p.b) / 255f;

    private static void ValidateContract(DiagnosticsContract contract)
    {
        if (contract == null)
            throw new InvalidOperationException("Temporal diagnostics contract is null.");
        if (!contract.manualReviewRequired || contract.automaticVisualPoints != 0 || contract.allowAutomaticCriticalDefectClear)
            throw new InvalidOperationException("Temporal diagnostics must remain advisory: manual review required, zero automatic points, no automatic critical-defect clear.");
        if (contract.sampleStride < 1 || contract.sampleStride > 8)
            throw new InvalidOperationException($"Temporal diagnostics sampleStride out of range: {contract.sampleStride}.");
        if (contract.alignmentSampleStride < contract.sampleStride || contract.alignmentSampleStride > 16)
            throw new InvalidOperationException($"Temporal diagnostics alignmentSampleStride out of range: {contract.alignmentSampleStride}.");
        if (contract.subpixelAlignmentSearchRadiusPixels < 0 || contract.subpixelAlignmentSearchRadiusPixels > 6)
            throw new InvalidOperationException($"Temporal diagnostics alignment search radius out of range: {contract.subpixelAlignmentSearchRadiusPixels}.");
        if (contract.advisoryTransitionSpikeRatio < 1.5f || contract.advisoryHighFrequencyDeltaRatio < 1.0f)
            throw new InvalidOperationException("Temporal diagnostic advisory thresholds are implausibly permissive.");
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

    private sealed class FrameData : IDisposable
    {
        public readonly Texture2D texture;
        public readonly Color32[] pixels;
        public readonly int width;
        public readonly int height;
        public readonly float[] histogram;
        public readonly FrameMetrics metrics;

        public FrameData(Texture2D texture, Color32[] pixels, int width, int height, float[] histogram, FrameMetrics metrics)
        {
            this.texture = texture;
            this.pixels = pixels;
            this.width = width;
            this.height = height;
            this.histogram = histogram;
            this.metrics = metrics;
        }

        public void Dispose()
        {
            if (texture != null)
                UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    private struct AlignmentResult
    {
        public float meanAbsoluteError;
        public int shiftX;
        public int shiftY;
    }

    [Serializable]
    private sealed class DiagnosticsContract
    {
        public string schemaVersion;
        public bool manualReviewRequired;
        public int automaticVisualPoints;
        public bool allowAutomaticCriticalDefectClear;
        public int sampleStride;
        public int alignmentSampleStride;
        public int subpixelAlignmentSearchRadiusPixels;
        public float advisoryTransitionSpikeRatio;
        public float advisoryHighFrequencyDeltaRatio;
    }

    [Serializable]
    private sealed class TemporalManifest
    {
        public string captureSessionId;
        public TemporalProbe[] probes;
    }

    [Serializable]
    private sealed class TemporalProbe
    {
        public string id;
        public TemporalFrame[] frames;
    }

    [Serializable]
    private sealed class TemporalFrame
    {
        public int index;
        public TemporalCrop crop;
    }

    [Serializable]
    private sealed class TemporalCrop
    {
        public string assetPath;
        public int width;
        public int height;
    }

    [Serializable]
    private sealed class DiagnosticsOutput
    {
        public string schemaVersion;
        public string generatedUtc;
        public string captureSessionId;
        public string sourceManifestAssetPath;
        public string source;
        public bool manualReviewRequired;
        public int automaticVisualPoints;
        public bool criticalDefectsCleared;
        public ProbeDiagnostics[] probeDiagnostics;
        public ReviewCandidate[] reviewCandidates;
        public string note;
    }

    [Serializable]
    private sealed class ProbeDiagnostics
    {
        public string probeId;
        public int frameCount;
        public float medianTransitionIndicator;
        public FrameMetrics[] frameMetrics;
        public TransitionMetrics[] transitions;
        public ReviewCandidate[] reviewCandidates;
        public string automaticPassFail;
    }

    [Serializable]
    private sealed class FrameMetrics
    {
        public int index;
        public float meanLuma;
        public float edgeEnergy;
        public float laplacianEnergy;
    }

    [Serializable]
    private sealed class TransitionMetrics
    {
        public int fromFrame;
        public int toFrame;
        public float meanLumaDelta;
        public float histogramL1;
        public float edgeEnergyRelativeDelta;
        public float laplacianEnergyRelativeDelta;
        public float compensatedLumaMae;
        public int bestShiftX;
        public int bestShiftY;
        public float diagnosticIndicator;
        public float advisorySpikeRatio;
    }

    [Serializable]
    private sealed class ReviewCandidate
    {
        public string probeId;
        public int fromFrame;
        public int toFrame;
        public float advisorySpikeRatio;
        public string reason;
        public bool manualFindingRequired;
    }
}
