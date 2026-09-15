using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Source-side observability gate for benchmark details that are easy to build correctly yet place
/// outside the pixels that will actually be reviewed. This does not award Visual Fidelity points.
/// It proves only that the generated period-authentic rooftop receiving assembly is large enough and
/// positioned inside the native 3840x2160 hero/oblique evidence frames and their planned 100% crops.
///
/// Once real Unity full-frame captures exist, an AssetPostprocessor below extracts the planned roof
/// crops from decoded source texels without resampling. Those crops remain evidence only; human/render
/// review is still required before any Visual Fidelity score can be entered.
/// </summary>
public static class QualityBlockBenchmarkObservabilityQA
{
    private const string RootName = "PeriodAuthenticity2000";
    private const string OutputDir = "Assets/QA/Captures4K";
    private const string EvidenceManifestPath = "Assets/QA/rooftop_observability_evidence.json";
    private const int Width = 3840;
    private const int Height = 2160;
    private const float Aspect = Width / (float)Height;
    private const float FullFrameMarginPx = 8f;
    private const float CropMarginPx = 16f;

    private static readonly ViewRequirement[] Requirements =
    {
        new ViewRequirement(
            "hero",
            new Vector3(2.8f, 2.2f, 18.5f),
            new Vector3(-7.6f, 4.0f, -7.8f),
            44f,
            new RectInt(1280, 1440, 1280, 720),
            "rooftop_period_hardware",
            120f,
            240f),
        new ViewRequirement(
            "oblique",
            new Vector3(18.0f, 3.4f, 12.5f),
            new Vector3(-6.0f, 4.0f, -8.3f),
            42f,
            new RectInt(1280, 1440, 1280, 720),
            "rooftop_reception_oblique",
            130f,
            220f),
    };

    [MenuItem("NewTown/QA/Validate Benchmark Detail Observability")]
    public static void ValidateOpenScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject root = Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x.scene == scene && x.name == RootName);

        if (root == null)
        {
            var slot = Resources.FindObjectsOfTypeAll<QualityBlockArtSlot>()
                .FirstOrDefault(x => x.gameObject.scene == scene && x.SlotId == "danchi.main");
            if (slot != null && slot.IsUsingAuthoredArt)
            {
                Debug.Log("Benchmark observability QA skipped for generated rooftop detail because authored danchi art is active.");
                return;
            }
            throw new InvalidOperationException("Benchmark observability QA requires PeriodAuthenticity2000.");
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true)
            .Where(r => r != null && r.sharedMaterial != null)
            .ToArray();
        if (renderers.Length == 0)
            throw new InvalidOperationException("PeriodAuthenticity2000 has no renderable geometry for observability QA.");

        foreach (ViewRequirement requirement in Requirements)
        {
            PixelBounds projected = ProjectRenderers(renderers, requirement);
            ValidateProjectedBounds(requirement, projected);
        }

        Debug.Log(
            "Benchmark observability QA passed source-side: period rooftop detail is projected into native hero/oblique frames " +
            "and the planned pixel-exact 100% roof crops. Visual Fidelity remains UNSCORED until actual Unity renders are reviewed.");
    }

    /// <summary>
    /// Called automatically after native full-frame PNGs are imported. Exact decoded Color32 texels
    /// are copied to the crop; no scaling, filtering, sharpening, or interpolation is performed.
    /// </summary>
    public static void ExtractPeriodAuthenticityCropsFromExistingFrames()
    {
        var records = new List<RooftopCropRecord>();
        bool foundAnySource = false;

        foreach (ViewRequirement requirement in Requirements)
        {
            string sourceAssetPath = $"{OutputDir}/{requirement.id}_3840x2160.png";
            string sourceAbsolute = AbsolutePath(sourceAssetPath);
            if (!File.Exists(sourceAbsolute))
                continue;

            foundAnySource = true;
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            try
            {
                if (!source.LoadImage(File.ReadAllBytes(sourceAbsolute), false))
                    throw new InvalidOperationException($"Could not decode native benchmark frame {sourceAssetPath}.");
                if (source.width != Width || source.height != Height)
                    throw new InvalidOperationException(
                        $"Rooftop evidence source {sourceAssetPath} must be {Width}x{Height}, got {source.width}x{source.height}.");

                string cropAssetPath =
                    $"{OutputDir}/{requirement.id}_crop_{requirement.cropId}_{requirement.crop.width}x{requirement.crop.height}_100pct.png";
                CopyDecodedTexelsWithoutResampling(source, requirement.crop, cropAssetPath);
                records.Add(new RooftopCropRecord
                {
                    viewId = requirement.id,
                    sourceAssetPath = sourceAssetPath,
                    cropAssetPath = cropAssetPath,
                    x = requirement.crop.x,
                    y = requirement.crop.y,
                    width = requirement.crop.width,
                    height = requirement.crop.height,
                    coordinateOrigin = "bottom-left, matching Unity Texture2D/GetPixels convention",
                    resampled = false,
                });
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        if (!foundAnySource)
            return;
        if (records.Count != Requirements.Length)
            throw new InvalidOperationException(
                $"Rooftop crop extraction found only {records.Count}/{Requirements.Length} required native full frames. " +
                "Do not treat partial files as complete benchmark evidence.");

        var manifest = new RooftopEvidenceManifest
        {
            schemaVersion = "1.0",
            generatedUtc = DateTime.UtcNow.ToString("O"),
            sourceResolution = $"{Width}x{Height}",
            sourceRequirement = "Native Unity benchmark full frames produced by QualityBlock4KCapture",
            cropPolicy = "Decoded source Color32 texels copied 1:1; no resampling, filtering, sharpening, or interpolation.",
            visualFidelityStatus = "UNSCORED_REVIEW_REQUIRED",
            records = records.ToArray(),
            note = "Crop generation proves evidence observability only. It cannot award Period Accuracy or any other Visual Fidelity points."
        };

        string manifestAbsolute = AbsolutePath(EvidenceManifestPath);
        Directory.CreateDirectory(Path.GetDirectoryName(manifestAbsolute));
        File.WriteAllText(manifestAbsolute, JsonUtility.ToJson(manifest, true));
        AssetDatabase.Refresh();
    }

    private static PixelBounds ProjectRenderers(Renderer[] renderers, ViewRequirement requirement)
    {
        Vector3 forward = (requirement.target - requirement.position).normalized;
        if (forward.sqrMagnitude < 0.99f)
            throw new InvalidOperationException($"Benchmark view {requirement.id} has an invalid camera target.");

        Quaternion cameraRotation = Quaternion.LookRotation(forward, Vector3.up);
        Quaternion inverseRotation = Quaternion.Inverse(cameraRotation);
        float tanHalfVertical = Mathf.Tan(requirement.fieldOfView * Mathf.Deg2Rad * 0.5f);
        if (tanHalfVertical <= 0f)
            throw new InvalidOperationException($"Benchmark view {requirement.id} has an invalid field of view.");

        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;
        int projectedCornerCount = 0;

        foreach (Renderer renderer in renderers)
        {
            foreach (Vector3 worldCorner in BoundsCorners(renderer.bounds))
            {
                Vector3 local = inverseRotation * (worldCorner - requirement.position);
                if (local.z <= 0.01f)
                    throw new InvalidOperationException(
                        $"Period rooftop geometry crosses/backs the {requirement.id} camera plane; observability cannot be trusted.");

                float viewportX = 0.5f + local.x / (2f * local.z * tanHalfVertical * Aspect);
                float viewportY = 0.5f + local.y / (2f * local.z * tanHalfVertical);
                float pixelX = viewportX * Width;
                float pixelY = viewportY * Height;

                minX = Mathf.Min(minX, pixelX);
                minY = Mathf.Min(minY, pixelY);
                maxX = Mathf.Max(maxX, pixelX);
                maxY = Mathf.Max(maxY, pixelY);
                projectedCornerCount++;
            }
        }

        if (projectedCornerCount == 0)
            throw new InvalidOperationException($"No period rooftop corners could be projected for {requirement.id}.");
        return new PixelBounds(minX, minY, maxX, maxY);
    }

    private static void ValidateProjectedBounds(ViewRequirement requirement, PixelBounds bounds)
    {
        if (bounds.minX < FullFrameMarginPx || bounds.minY < FullFrameMarginPx ||
            bounds.maxX > Width - FullFrameMarginPx || bounds.maxY > Height - FullFrameMarginPx)
            throw new InvalidOperationException(
                $"Period rooftop detail is clipped/too close to the {requirement.id} frame edge: {bounds}. " +
                "Move the benchmark camera/target or the physically installed assembly; do not fake visibility by enlarging thin components.");

        if (bounds.Width < requirement.minimumWidthPx || bounds.Height < requirement.minimumHeightPx)
            throw new InvalidOperationException(
                $"Period rooftop detail is not sufficiently observable in {requirement.id}: {bounds.Width:F1}x{bounds.Height:F1}px, " +
                $"required >= {requirement.minimumWidthPx:F0}x{requirement.minimumHeightPx:F0}px.");

        RectInt crop = requirement.crop;
        if (bounds.minX < crop.x + CropMarginPx || bounds.maxX > crop.xMax - CropMarginPx ||
            bounds.minY < crop.y + CropMarginPx || bounds.maxY > crop.yMax - CropMarginPx)
            throw new InvalidOperationException(
                $"Planned 100% crop {requirement.id}/{requirement.cropId} no longer contains the full period rooftop detail with " +
                $"{CropMarginPx:F0}px inspection margin. Projected={bounds}, crop={crop}.");
    }

    private static IEnumerable<Vector3> BoundsCorners(Bounds bounds)
    {
        Vector3 c = bounds.center;
        Vector3 e = bounds.extents;
        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
        for (int z = -1; z <= 1; z += 2)
            yield return c + Vector3.Scale(e, new Vector3(x, y, z));
    }

    private static void CopyDecodedTexelsWithoutResampling(Texture2D source, RectInt crop, string cropAssetPath)
    {
        Color32[] src = source.GetPixels32();
        Color32[] dst = new Color32[crop.width * crop.height];
        for (int y = 0; y < crop.height; y++)
            Array.Copy(src, (crop.y + y) * source.width + crop.x, dst, y * crop.width, crop.width);

        var output = new Texture2D(crop.width, crop.height, TextureFormat.RGBA32, false, false);
        try
        {
            output.SetPixels32(dst);
            output.Apply(false, false);
            string absolute = AbsolutePath(cropAssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute));
            File.WriteAllBytes(absolute, output.EncodeToPNG());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(output);
        }
    }

    private static string AbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private sealed class ViewRequirement
    {
        public readonly string id;
        public readonly Vector3 position;
        public readonly Vector3 target;
        public readonly float fieldOfView;
        public readonly RectInt crop;
        public readonly string cropId;
        public readonly float minimumWidthPx;
        public readonly float minimumHeightPx;

        public ViewRequirement(
            string id, Vector3 position, Vector3 target, float fieldOfView, RectInt crop, string cropId,
            float minimumWidthPx, float minimumHeightPx)
        {
            this.id = id;
            this.position = position;
            this.target = target;
            this.fieldOfView = fieldOfView;
            this.crop = crop;
            this.cropId = cropId;
            this.minimumWidthPx = minimumWidthPx;
            this.minimumHeightPx = minimumHeightPx;
        }
    }

    private readonly struct PixelBounds
    {
        public readonly float minX;
        public readonly float minY;
        public readonly float maxX;
        public readonly float maxY;
        public float Width => maxX - minX;
        public float Height => maxY - minY;

        public PixelBounds(float minX, float minY, float maxX, float maxY)
        {
            this.minX = minX;
            this.minY = minY;
            this.maxX = maxX;
            this.maxY = maxY;
        }

        public override string ToString() =>
            $"x={minX:F1}..{maxX:F1}, y={minY:F1}..{maxY:F1}, size={Width:F1}x{Height:F1}px";
    }

    [Serializable]
    private sealed class RooftopEvidenceManifest
    {
        public string schemaVersion;
        public string generatedUtc;
        public string sourceResolution;
        public string sourceRequirement;
        public string cropPolicy;
        public string visualFidelityStatus;
        public RooftopCropRecord[] records;
        public string note;
    }

    [Serializable]
    private sealed class RooftopCropRecord
    {
        public string viewId;
        public string sourceAssetPath;
        public string cropAssetPath;
        public int x;
        public int y;
        public int width;
        public int height;
        public string coordinateOrigin;
        public bool resampled;
    }
}

/// <summary>
/// Hooks the existing native 4K pipeline without changing its render path. When the hero/oblique
/// full-frame PNGs are imported after QualityBlock4KCapture refreshes the AssetDatabase, exact roof
/// inspection crops are produced from those same rendered frames.
/// </summary>
public sealed class QualityBlockBenchmarkEvidencePostprocessor : AssetPostprocessor
{
    private static bool scheduled;

    private static void OnPostprocessAllAssets(
        string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        bool importedRequiredFrame = importedAssets.Any(path =>
            path == "Assets/QA/Captures4K/hero_3840x2160.png" ||
            path == "Assets/QA/Captures4K/oblique_3840x2160.png");
        if (!importedRequiredFrame || scheduled) return;

        scheduled = true;
        EditorApplication.delayCall += () =>
        {
            scheduled = false;
            QualityBlockBenchmarkObservabilityQA.ExtractPeriodAuthenticityCropsFromExistingFrames();
        };
    }
}
