using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Source-side QA against directional illumination being painted into generated base-color textures.
///
/// This deliberately inspects the baseline GeneratedPBR *_Albedo source files rather than judging rendered
/// highlights. The test is conservative: it detects broad directional ramps, broad centre hot-spots and
/// tile-edge discontinuities in linear luminance. It does not claim that a passing texture is visually
/// correct, and it awards no Visual Fidelity points. Native frontal/oblique/grazing 4K evidence remains the
/// authority for the critical defect "obviously painted/baked highlights".
/// </summary>
public static class QualityBlockAlbedoLightingNeutralityQA
{
    private const string ContractPath = "Assets/QA/albedo_lighting_neutrality_contract.json";
    private const string GeneratedPbrRoot = "Assets/Art/GeneratedPBR";
    private const string AlbedoSuffix = "_Albedo.png";
    private const int GridSize = 8;
    private const int MinimumAlbedoTextures = 6;

    // These are intentionally conservative source-preflight limits. They reject obvious broad illumination
    // patterns without pretending to determine final material appearance from source statistics alone.
    private const float MaximumNormalizedDirectionalRamp = 0.38f;
    private const float MaximumNormalizedCenterEdgeContrast = 0.30f;
    private const float MaximumNormalizedTileSeamRmse = 0.12f;

    private static readonly string[] RequiredBaselineAlbedos =
    {
        "PBR_GrassWorn_Albedo.png",
        "PBR_DrySoil_Albedo.png",
        "PBR_DanchiConcrete_Albedo.png",
        "PBR_WashedConcrete_Albedo.png",
        "PBR_WarmPaving_Albedo.png",
        "PBR_Bark_Albedo.png"
    };

    private sealed class Metrics
    {
        public string Path;
        public float MeanLinearLuminance;
        public float NormalizedDirectionalRamp;
        public float NormalizedCenterEdgeContrast;
        public float NormalizedTileSeamRmse;
    }

    [MenuItem("NewTown/QA/Validate Generated Base Albedo Lighting Neutrality")]
    public static void ValidateGeneratedBaseAlbedos()
    {
        ValidateContractConfigOnly();

        if (!AssetDatabase.IsValidFolder(GeneratedPbrRoot))
            throw new InvalidOperationException(
                $"Generated base PBR library is missing: {GeneratedPbrRoot}. Build the final quality scene before albedo-neutrality QA.");

        string[] paths = AssetDatabase.FindAssets("t:Texture2D", new[] { GeneratedPbrRoot })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.EndsWith(AlbedoSuffix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToArray();

        var errors = new List<string>();
        if (paths.Length < MinimumAlbedoTextures)
            errors.Add($"Too few generated baseline albedos for neutrality QA: {paths.Length} < {MinimumAlbedoTextures}.");

        var fileNames = new HashSet<string>(paths.Select(Path.GetFileName), StringComparer.Ordinal);
        foreach (string required in RequiredBaselineAlbedos)
            if (!fileNames.Contains(required))
                errors.Add($"Required baseline albedo is missing: {required}.");

        var metrics = new List<Metrics>();
        foreach (string path in paths)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                errors.Add($"Could not resolve TextureImporter for generated albedo: {path}.");
                continue;
            }
            if (!importer.sRGBTexture)
                errors.Add($"Generated base albedo must be imported as sRGB: {path}.");

            Metrics m;
            try
            {
                m = AnalyzeSourceBytes(path);
                metrics.Add(m);
            }
            catch (Exception ex)
            {
                errors.Add($"Could not inspect generated albedo source pixels {path}: {ex.Message}");
                continue;
            }

            if (!FinitePositive(m.MeanLinearLuminance))
                errors.Add($"Generated base albedo has invalid/black mean luminance: {path} = {m.MeanLinearLuminance:0.####}.");

            if (m.NormalizedDirectionalRamp > MaximumNormalizedDirectionalRamp)
                errors.Add(
                    $"Generated base albedo contains an excessive broad directional luminance ramp: {path}, " +
                    $"normalizedRamp={m.NormalizedDirectionalRamp:0.###} > {MaximumNormalizedDirectionalRamp:0.##}. " +
                    "Do not paint sun/key-light response into base color; put angular highlight energy in PBR response and causal surface variation only.");

            if (m.NormalizedCenterEdgeContrast > MaximumNormalizedCenterEdgeContrast)
                errors.Add(
                    $"Generated base albedo contains an excessive broad centre-vs-edge luminance bias: {path}, " +
                    $"normalizedContrast={m.NormalizedCenterEdgeContrast:0.###} > {MaximumNormalizedCenterEdgeContrast:0.##}. " +
                    "A broad baked hot-spot/vignette is not valid base-color information.");

            if (m.NormalizedTileSeamRmse > MaximumNormalizedTileSeamRmse)
                errors.Add(
                    $"Generated base albedo has an excessive opposite-edge luminance seam: {path}, " +
                    $"normalizedSeamRmse={m.NormalizedTileSeamRmse:0.###} > {MaximumNormalizedTileSeamRmse:0.##}. " +
                    "Repair the tileable source instead of hiding the seam with grading or repetition-breaking overlays.");
        }

        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Generated base-albedo lighting-neutrality QA FAILED:\n - " + string.Join("\n - ", errors));

        string summary = string.Join(", ", metrics.Select(m =>
            $"{Path.GetFileName(m.Path)}: ramp={m.NormalizedDirectionalRamp:0.###}, centerEdge={m.NormalizedCenterEdgeContrast:0.###}, seam={m.NormalizedTileSeamRmse:0.###}"));
        Debug.Log(
            $"Generated base-albedo lighting-neutrality QA passed for {metrics.Count} textures. {summary}. " +
            "This is source preflight only: it awards 0 Visual Fidelity points and cannot clear painted/baked-highlight critical FAIL without sealed native 4K pixel review.");
    }

    [MenuItem("NewTown/QA/Validate Base Albedo Lighting Neutrality Contract")]
    public static void ValidateContractConfigOnly()
    {
        if (!File.Exists(ContractPath))
            throw new InvalidOperationException($"Missing required albedo-lighting-neutrality contract: {ContractPath}");

        string json = File.ReadAllText(ContractPath);
        string[] requiredTokens =
        {
            "\"contractVersion\": \"albedo-lighting-neutrality-v1.0.0\"",
            "\"criticalDefectRiskReduced\": \"obviously_painted_or_baked_highlights\"",
            "\"generatedBaseAlbedoRoot\": \"Assets/Art/GeneratedPBR\"",
            "\"minimumAlbedoTextures\": 6",
            "\"gridSize\": 8",
            "\"maximumNormalizedDirectionalRamp\": 0.38",
            "\"maximumNormalizedCenterEdgeContrast\": 0.30",
            "\"maximumNormalizedTileSeamRmse\": 0.12",
            "\"visualFidelityPointsAwarded\": 0",
            "\"runtimeRenderVerification\": \"PENDING_UNITY_RUNTIME\""
        };

        foreach (string token in requiredTokens)
            if (json.IndexOf(token, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException($"Albedo-lighting-neutrality contract missing required token: {token}");
    }

    private static Metrics AnalyzeSourceBytes(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrEmpty(projectRoot))
            throw new InvalidOperationException("Could not resolve Unity project root.");

        string absolutePath = Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        if (!File.Exists(absolutePath))
            throw new FileNotFoundException("Source image file does not exist.", absolutePath);

        byte[] bytes = File.ReadAllBytes(absolutePath);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
        try
        {
            if (!ImageConversion.LoadImage(texture, bytes, false))
                throw new InvalidOperationException("ImageConversion.LoadImage returned false.");

            Color32[] pixels = texture.GetPixels32();
            int width = texture.width;
            int height = texture.height;
            if (width < GridSize * 2 || height < GridSize * 2 || pixels.Length != width * height)
                throw new InvalidOperationException($"Texture resolution is too small/inconsistent for {GridSize}x{GridSize} analysis: {width}x{height}.");

            float[] blockMeans = BuildBlockMeans(pixels, width, height);
            float mean = blockMeans.Average();
            float epsilon = Mathf.Max(mean, 0.0005f);

            // On a symmetric grid, x/y/xy sums are zero, so the least-squares plane slopes reduce to
            // independent dot products. 2*gradientMagnitude estimates opposite-side broad ramp magnitude.
            double sumX2 = 0.0;
            double sumY2 = 0.0;
            double sumXL = 0.0;
            double sumYL = 0.0;
            double centerSum = 0.0;
            double edgeSum = 0.0;
            int centerCount = 0;
            int edgeCount = 0;

            for (int by = 0; by < GridSize; by++)
            for (int bx = 0; bx < GridSize; bx++)
            {
                float x = ((bx + 0.5f) / GridSize) * 2f - 1f;
                float y = ((by + 0.5f) / GridSize) * 2f - 1f;
                float l = blockMeans[by * GridSize + bx];
                sumX2 += x * x;
                sumY2 += y * y;
                sumXL += x * l;
                sumYL += y * l;

                float r = Mathf.Sqrt(x * x + y * y);
                if (r <= 0.55f)
                {
                    centerSum += l;
                    centerCount++;
                }
                else if (r >= 0.90f)
                {
                    edgeSum += l;
                    edgeCount++;
                }
            }

            double a = sumXL / Math.Max(sumX2, 1e-12);
            double b = sumYL / Math.Max(sumY2, 1e-12);
            float ramp = (float)(2.0 * Math.Sqrt(a * a + b * b) / epsilon);

            float centerMean = centerCount > 0 ? (float)(centerSum / centerCount) : mean;
            float edgeMean = edgeCount > 0 ? (float)(edgeSum / edgeCount) : mean;
            float centerEdge = Mathf.Abs(centerMean - edgeMean) / epsilon;
            float seam = ComputeNormalizedSeamRmse(pixels, width, height, epsilon);

            return new Metrics
            {
                Path = assetPath,
                MeanLinearLuminance = mean,
                NormalizedDirectionalRamp = ramp,
                NormalizedCenterEdgeContrast = centerEdge,
                NormalizedTileSeamRmse = seam
            };
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    private static float[] BuildBlockMeans(Color32[] pixels, int width, int height)
    {
        var means = new float[GridSize * GridSize];
        for (int by = 0; by < GridSize; by++)
        for (int bx = 0; bx < GridSize; bx++)
        {
            int x0 = bx * width / GridSize;
            int x1 = (bx + 1) * width / GridSize;
            int y0 = by * height / GridSize;
            int y1 = (by + 1) * height / GridSize;
            double sum = 0.0;
            int count = 0;
            for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
            {
                sum += LinearLuminance(pixels[y * width + x]);
                count++;
            }
            means[by * GridSize + bx] = count > 0 ? (float)(sum / count) : 0f;
        }
        return means;
    }

    private static float ComputeNormalizedSeamRmse(Color32[] pixels, int width, int height, float mean)
    {
        double squared = 0.0;
        int count = 0;

        for (int y = 0; y < height; y++)
        {
            float a = LinearLuminance(pixels[y * width]);
            float b = LinearLuminance(pixels[y * width + (width - 1)]);
            double d = a - b;
            squared += d * d;
            count++;
        }
        for (int x = 0; x < width; x++)
        {
            float a = LinearLuminance(pixels[x]);
            float b = LinearLuminance(pixels[(height - 1) * width + x]);
            double d = a - b;
            squared += d * d;
            count++;
        }

        return count > 0 ? (float)(Math.Sqrt(squared / count) / Math.Max(mean, 0.0005f)) : 0f;
    }

    private static float LinearLuminance(Color32 c)
    {
        float r = SrgbToLinear(c.r / 255f);
        float g = SrgbToLinear(c.g / 255f);
        float b = SrgbToLinear(c.b / 255f);
        return 0.2126f * r + 0.7152f * g + 0.0722f * b;
    }

    private static float SrgbToLinear(float value)
    {
        return value <= 0.04045f ? value / 12.92f : Mathf.Pow((value + 0.055f) / 1.055f, 2.4f);
    }

    private static bool FinitePositive(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }
}
