using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Source-side fail-closed screening for the critical defect "visible primitive-placeholder geometry".
/// The gate projects every actively rendered Unity stock solid primitive under the final quality-block
/// root into the exact hero/oblique/grazing benchmark framing. Large projected stock solids block the
/// authoritative review packet before reflection rendering or Camera.Render work begins.
///
/// This is deliberately not a visual scorer: a PASS only removes an obvious source-side risk. Actual
/// native 3840x2160 pixels and 100% crops still decide whether the critical visual defect is present.
/// </summary>
public static class QualityBlockBenchmarkPrimitiveExposureQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string RootName = "QualityBlock1990s";
    private const string ContractPath = "Assets/QA/benchmark_primitive_exposure_contract.json";
    private const string CaptureSourcePath = "Assets/Editor/QualityBlock4KCapture.cs";
    private const string RuntimeReportPath = "Assets/QA/benchmark_primitive_exposure_runtime_report.json";
    private const int Width = 3840;
    private const int Height = 2160;
    private const float Aspect = Width / (float)Height;
    private const float NearPlaneGuard = 0.05f;
    private const float MinimumVisibleWidth = 48f;
    private const float MinimumVisibleHeight = 18f;
    private const float MinimumVisibleArea = 1200f;
    private const float LargeAreaFailure = 4096f;

    private static readonly HashSet<string> StockSolidMeshNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "Cube", "Cylinder", "Sphere", "Capsule"
    };

    // These must remain byte-for-byte coordinated with QualityBlock4KCapture. ValidateContractConfigOnly
    // additionally inspects that source file so camera drift cannot silently weaken this screen-space gate.
    private static readonly ViewRequirement[] Views =
    {
        new ViewRequirement("hero", new Vector3(2.8f, 2.2f, 18.5f), new Vector3(-7.6f, 4.0f, -7.8f), 44f),
        new ViewRequirement("oblique", new Vector3(18.0f, 3.4f, 12.5f), new Vector3(-6.0f, 4.0f, -8.3f), 42f),
        new ViewRequirement("grazing", new Vector3(-22.0f, 3.0f, 7.0f), new Vector3(-8.2f, 3.8f, -7.8f), 38f),
    };

    [MenuItem("NewTown/QA/Validate Benchmark Primitive Exposure Contract")]
    public static void ValidateContractConfigOnly()
    {
        if (!File.Exists(ContractPath))
            throw new InvalidOperationException($"Missing required benchmark primitive exposure contract: {ContractPath}");

        string json = File.ReadAllText(ContractPath);
        string[] requiredContractTokens =
        {
            "\"criticalDefectRiskReduced\": \"visible_primitive_placeholder_geometry\"",
            "\"authoritativeViewSource\": \"QualityBlock4KCapture.Views; source-token lock must match exact 3840x2160 hero/oblique/grazing positions, targets and FOV before projection QA can run\"",
            "\"requiredViews\": [\"hero\", \"oblique\", \"grazing\"]",
            "\"nativeResolution\": [3840, 2160]",
            "\"solidBuiltInMeshes\": [\"Cube\", \"Cylinder\", \"Sphere\", \"Capsule\"]",
            "\"minimumVisibleWidthPixels\": 48",
            "\"minimumVisibleHeightPixels\": 18",
            "\"minimumVisibleAreaPixels\": 1200",
            "\"largeAreaAutomaticFailurePixels\": 4096",
            "\"visualFidelityPointsAwarded\": 0",
            "\"criticalDefectClearedBySourcePass\": false",
            "PENDING_NATIVE_4K_PIXEL_REVIEW"
        };
        foreach (string token in requiredContractTokens)
            if (json.IndexOf(token, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException($"Benchmark primitive exposure contract missing required token: {token}");

        if (!File.Exists(CaptureSourcePath))
            throw new InvalidOperationException($"Cannot verify authoritative benchmark framing source: {CaptureSourcePath}");

        string captureSource = File.ReadAllText(CaptureSourcePath);
        string[] requiredCaptureTokens =
        {
            "private const int Width = 3840;",
            "private const int Height = 2160;",
            "\"hero\"",
            "new Vector3(2.8f, 2.2f, 18.5f)",
            "new Vector3(-7.6f, 4.0f, -7.8f)",
            "44f",
            "\"oblique\"",
            "new Vector3(18.0f, 3.4f, 12.5f)",
            "new Vector3(-6.0f, 4.0f, -8.3f)",
            "42f",
            "\"grazing\"",
            "new Vector3(-22.0f, 3.0f, 7.0f)",
            "new Vector3(-8.2f, 3.8f, -7.8f)",
            "38f"
        };
        foreach (string token in requiredCaptureTokens)
            if (captureSource.IndexOf(token, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException(
                    $"Benchmark primitive exposure framing no longer matches QualityBlock4KCapture; missing authoritative token: {token}. " +
                    "Update the gate and contract intentionally rather than evaluating a stale camera projection.");

        Debug.Log("Benchmark primitive exposure contract valid: exact hero/oblique/grazing source framing, fail-closed stock-solid thresholds, automatic Visual Fidelity points=0.");
    }

    [MenuItem("NewTown/QA/Validate Benchmark Primitive Exposure")]
    public static void ValidateOpenScene()
    {
        ValidateContractConfigOnly();

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
            throw new InvalidOperationException($"Benchmark primitive exposure QA requires the persisted quality scene: {ScenePath}");

        GameObject root = Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x.scene == scene && x.name == RootName);
        if (root == null)
            throw new InvalidOperationException($"Benchmark primitive exposure QA could not find scene root {RootName}.");

        MeshFilter[] stockSolidFilters = root.GetComponentsInChildren<MeshFilter>(true)
            .Where(IsActivelyRendered)
            .Where(x => x.sharedMesh != null && StockSolidMeshNames.Contains(x.sharedMesh.name))
            .ToArray();

        var violations = new List<PrimitiveViolation>();
        foreach (MeshFilter filter in stockSolidFilters)
        {
            Renderer renderer = filter.GetComponent<Renderer>();
            foreach (ViewRequirement view in Views)
            {
                Projection projection = ProjectBounds(renderer.bounds, view);
                if (!projection.intersectsFrame) continue;

                bool significant = projection.crossesCameraPlane ||
                    (projection.visibleWidthPixels >= MinimumVisibleWidth &&
                     projection.visibleHeightPixels >= MinimumVisibleHeight &&
                     projection.visibleAreaPixels >= MinimumVisibleArea) ||
                    projection.visibleAreaPixels >= LargeAreaFailure;
                if (!significant) continue;

                violations.Add(new PrimitiveViolation
                {
                    objectName = filter.gameObject.name,
                    hierarchyPath = HierarchyPath(filter.transform, root.transform),
                    meshName = filter.sharedMesh.name,
                    viewId = view.id,
                    visibleWidthPixels = projection.visibleWidthPixels,
                    visibleHeightPixels = projection.visibleHeightPixels,
                    visibleAreaPixels = projection.visibleAreaPixels,
                    crossesCameraPlane = projection.crossesCameraPlane,
                    correctiveAction =
                        "Reconstruct the real manufactured/installed assembly with dimensioned physical geometry or authored art. " +
                        "Do not rename, hide, shrink or threshold-exempt a required visible component."
                });
            }
        }

        WriteRuntimeReport(stockSolidFilters.Length, violations.ToArray());

        if (violations.Count > 0)
        {
            string summary = string.Join(", ", violations.Take(20).Select(v =>
                $"{v.viewId}:{v.hierarchyPath}[{v.meshName}]={v.visibleWidthPixels:F0}x{v.visibleHeightPixels:F0}px/{v.visibleAreaPixels:F0}px2"));
            throw new InvalidOperationException(
                $"Critical primitive-placeholder source risk: {violations.Count} benchmark projection(s) expose a visibly significant Unity stock solid. " +
                summary + ". Source preflight cannot clear the critical defect; replace the physical geometry, then render and inspect native 4K evidence.");
        }

        Debug.Log(
            $"Benchmark primitive exposure source preflight passed: active stock solid renderers inspected={stockSolidFilters.Length}, " +
            "visibly significant hero/oblique/grazing exposures=0. Visual Fidelity remains UNSCORED and native-4K pixel review is still mandatory.");
    }

    private static Projection ProjectBounds(Bounds bounds, ViewRequirement view)
    {
        Vector3 forward = (view.target - view.position).normalized;
        if (forward.sqrMagnitude < 0.99f)
            throw new InvalidOperationException($"Benchmark view {view.id} has an invalid camera target.");

        Quaternion inverseRotation = Quaternion.Inverse(Quaternion.LookRotation(forward, Vector3.up));
        float tanHalfVertical = Mathf.Tan(view.fieldOfView * Mathf.Deg2Rad * 0.5f);
        if (tanHalfVertical <= 0f)
            throw new InvalidOperationException($"Benchmark view {view.id} has an invalid field of view.");

        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;
        int frontCorners = 0;
        int behindCorners = 0;

        foreach (Vector3 worldCorner in BoundsCorners(bounds))
        {
            Vector3 local = inverseRotation * (worldCorner - view.position);
            if (local.z <= NearPlaneGuard)
            {
                behindCorners++;
                continue;
            }

            frontCorners++;
            float viewportX = 0.5f + local.x / (2f * local.z * tanHalfVertical * Aspect);
            float viewportY = 0.5f + local.y / (2f * local.z * tanHalfVertical);
            float pixelX = viewportX * Width;
            float pixelY = viewportY * Height;
            minX = Mathf.Min(minX, pixelX);
            minY = Mathf.Min(minY, pixelY);
            maxX = Mathf.Max(maxX, pixelX);
            maxY = Mathf.Max(maxY, pixelY);
        }

        if (frontCorners == 0)
            return Projection.NotVisible;

        bool crossesCameraPlane = behindCorners > 0;
        if (crossesCameraPlane)
            return new Projection(true, true, Width, Height, Width * (float)Height);

        float clippedMinX = Mathf.Max(0f, minX);
        float clippedMinY = Mathf.Max(0f, minY);
        float clippedMaxX = Mathf.Min(Width, maxX);
        float clippedMaxY = Mathf.Min(Height, maxY);
        float visibleWidth = clippedMaxX - clippedMinX;
        float visibleHeight = clippedMaxY - clippedMinY;
        if (visibleWidth <= 0f || visibleHeight <= 0f)
            return Projection.NotVisible;

        return new Projection(true, false, visibleWidth, visibleHeight, visibleWidth * visibleHeight);
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

    private static bool IsActivelyRendered(MeshFilter filter)
    {
        Renderer renderer = filter.GetComponent<Renderer>();
        return renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy;
    }

    private static string HierarchyPath(Transform transform, Transform root)
    {
        var names = new List<string>();
        Transform cursor = transform;
        while (cursor != null)
        {
            names.Add(cursor.name);
            if (cursor == root) break;
            cursor = cursor.parent;
        }
        names.Reverse();
        return string.Join("/", names);
    }

    private static void WriteRuntimeReport(int inspectedStockSolidRendererCount, PrimitiveViolation[] violations)
    {
        var report = new PrimitiveExposureReport
        {
            schemaVersion = "1.0",
            generatedUtc = DateTime.UtcNow.ToString("O"),
            scenePath = ScenePath,
            sourceResolution = $"{Width}x{Height}",
            inspectedStockSolidRendererCount = inspectedStockSolidRendererCount,
            violationCount = violations.Length,
            status = violations.Length == 0
                ? "SOURCE_PREFLIGHT_PASS_NATIVE_4K_REVIEW_REQUIRED"
                : "SOURCE_PREFLIGHT_FAIL_VISIBLE_STOCK_PRIMITIVE_RISK",
            visualFidelityStatus = "UNSCORED_REVIEW_REQUIRED",
            visualFidelityPointsAwarded = 0,
            criticalDefectCleared = false,
            violations = violations,
            note =
                "Projection is source-side risk screening only. Even an empty violation list cannot clear visible primitive-placeholder geometry without actual native 4K review."
        };

        string absolute = AbsolutePath(RuntimeReportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute));
        File.WriteAllText(absolute, JsonUtility.ToJson(report, true));
        AssetDatabase.Refresh();
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

        public ViewRequirement(string id, Vector3 position, Vector3 target, float fieldOfView)
        {
            this.id = id;
            this.position = position;
            this.target = target;
            this.fieldOfView = fieldOfView;
        }
    }

    private readonly struct Projection
    {
        public static readonly Projection NotVisible = new Projection(false, false, 0f, 0f, 0f);
        public readonly bool intersectsFrame;
        public readonly bool crossesCameraPlane;
        public readonly float visibleWidthPixels;
        public readonly float visibleHeightPixels;
        public readonly float visibleAreaPixels;

        public Projection(bool intersectsFrame, bool crossesCameraPlane, float visibleWidthPixels, float visibleHeightPixels, float visibleAreaPixels)
        {
            this.intersectsFrame = intersectsFrame;
            this.crossesCameraPlane = crossesCameraPlane;
            this.visibleWidthPixels = visibleWidthPixels;
            this.visibleHeightPixels = visibleHeightPixels;
            this.visibleAreaPixels = visibleAreaPixels;
        }
    }

    [Serializable]
    private sealed class PrimitiveExposureReport
    {
        public string schemaVersion;
        public string generatedUtc;
        public string scenePath;
        public string sourceResolution;
        public int inspectedStockSolidRendererCount;
        public int violationCount;
        public string status;
        public string visualFidelityStatus;
        public int visualFidelityPointsAwarded;
        public bool criticalDefectCleared;
        public PrimitiveViolation[] violations;
        public string note;
    }

    [Serializable]
    private sealed class PrimitiveViolation
    {
        public string objectName;
        public string hierarchyPath;
        public string meshName;
        public string viewId;
        public float visibleWidthPixels;
        public float visibleHeightPixels;
        public float visibleAreaPixels;
        public bool crossesCameraPlane;
        public string correctiveAction;
    }
}
