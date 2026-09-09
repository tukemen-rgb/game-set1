using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Native 3840x2160 evidence capture for the visual-fidelity gate.
/// Produces three deterministic views with pixel-exact 100% crops while preserving one coherent
/// scene/sun/reflection state. A successful capture proves only that Unity rendered the requested
/// frames; it does not assign a visual-fidelity score.
/// </summary>
public static class QualityBlock4KCapture
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string OutputDir = "Assets/QA/Captures4K";
    private const string ManifestPath = "Assets/QA/4k_capture_manifest.json";
    private const int Width = 3840;
    private const int Height = 2160;

    private static readonly ViewSpec[] Views =
    {
        new ViewSpec(
            "hero",
            new Vector3(2.8f, 2.2f, 18.5f),
            new Vector3(-7.6f, 4.0f, -7.8f),
            44f,
            new[]
            {
                new CropSpec("facade_center", 1280, 500, 1280, 720),
                new CropSpec("ground_contact", 1280, 80, 1280, 720),
                new CropSpec("facade_right", 2100, 500, 1280, 720),
            }),
        new ViewSpec(
            "oblique",
            new Vector3(18.0f, 3.4f, 12.5f),
            new Vector3(-6.0f, 4.0f, -8.3f),
            42f,
            new[]
            {
                new CropSpec("construction_depth", 1180, 500, 1280, 720),
                new CropSpec("balcony_services", 1900, 560, 1280, 720),
                new CropSpec("vegetation_grounding", 1900, 80, 1280, 720),
            }),
        new ViewSpec(
            "grazing",
            new Vector3(-22.0f, 3.0f, 7.0f),
            new Vector3(-8.2f, 3.8f, -7.8f),
            38f,
            new[]
            {
                new CropSpec("material_grazing", 1280, 520, 1280, 720),
                new CropSpec("sash_rail_response", 2050, 560, 1280, 720),
                new CropSpec("tree_shadow_contact", 520, 100, 1280, 720),
            }),
    };

    /// <summary>
    /// Legacy one-call capture. It remains available for compatibility, but the complete review
    /// packet uses PrepareSceneForSynchronizedCapture + QualityBlockReflectionProbeAwaiter +
    /// CapturePreparedSceneAfterProbeSync so RenderProbe completion is observed across Editor frames.
    /// </summary>
    [MenuItem("NewTown/QA/Capture Native 4K Fidelity Evidence")]
    public static void CaptureAll()
    {
        PrepareSceneForSynchronizedCapture();

        // Compatibility path only. The production review packet does not rely on this same-call-stack
        // synchronization because RenderProbe completion may require Editor frame progression.
        QualityBlockEnvironmentLightingUpgrade.RefreshRealtimeProbesImmediately();
        CapturePreparedSceneAfterProbeSync();
    }

    /// <summary>
    /// Rebuilds, persists and reopens the benchmark scene, then validates the exact scene state from
    /// which an asynchronous reflection refresh may safely begin. This method performs no scoring.
    /// </summary>
    public static void PrepareSceneForSynchronizedCapture()
    {
        PrepareAndValidateScene();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        QualityBlockEnvironmentLightingUpgrade.ValidateOpenScene();
    }

    /// <summary>
    /// Captures hero/oblique/grazing only after a separate synchronization stage has proved both
    /// realtime reflection cubemaps complete and written a valid runtime receipt. Rebuilding scene
    /// content here is forbidden because it would invalidate the synchronized cubemaps.
    /// </summary>
    public static void CapturePreparedSceneAfterProbeSync()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            throw new InvalidOperationException($"Prepared native-4K capture requires the persisted benchmark scene: {ScenePath}");

        QualityBlockEnvironmentLightingUpgrade.ValidateOpenScene();
        QualityBlockReflectionProbeCaptureSyncQA.ValidateRuntimeReceipt();

        Camera cam = Camera.main;
        if (cam == null)
            throw new InvalidOperationException("4K capture failed: MainCamera not found.");

        Directory.CreateDirectory(AbsolutePath(OutputDir));

        Vector3 originalPosition = cam.transform.position;
        Quaternion originalRotation = cam.transform.rotation;
        float originalFov = cam.fieldOfView;
        float originalAspect = cam.aspect;
        RenderTexture originalTarget = cam.targetTexture;
        RenderTexture originalActive = RenderTexture.active;

        var captured = new List<CaptureRecord>();
        try
        {
            foreach (ViewSpec view in Views)
                captured.Add(CaptureView(cam, view));
        }
        finally
        {
            cam.transform.position = originalPosition;
            cam.transform.rotation = originalRotation;
            cam.fieldOfView = originalFov;
            cam.aspect = originalAspect;
            cam.targetTexture = originalTarget;
            RenderTexture.active = originalActive;
        }

        WriteManifest(captured.ToArray());
        AssetDatabase.Refresh();
        Debug.Log(
            "Native 4K evidence capture completed for hero/oblique/grazing views with pixel-exact crops and proven pre-capture physical reflections. " +
            "Visual Fidelity remains UNSCORED until the rendered files are reviewed and evidence is entered.");
    }

    [MenuItem("NewTown/QA/Validate 4K Capture Contract")]
    public static void ValidateCaptureContract()
    {
        if (Views.Length != 3)
            throw new InvalidOperationException($"Expected exactly three visual-gate views, got {Views.Length}.");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (ViewSpec view in Views)
        {
            if (!seen.Add(view.id))
                throw new InvalidOperationException($"Duplicate 4K capture view id: {view.id}");
            if (view.crops == null || view.crops.Length < 1)
                throw new InvalidOperationException($"View {view.id} has no 100% crops.");

            foreach (CropSpec crop in view.crops)
            {
                if (crop.x < 0 || crop.y < 0 || crop.width <= 0 || crop.height <= 0 ||
                    crop.x + crop.width > Width || crop.y + crop.height > Height)
                    throw new InvalidOperationException(
                        $"Crop {view.id}/{crop.id} is outside the {Width}x{Height} native frame.");
            }
        }

        string[] required = { "hero", "oblique", "grazing" };
        foreach (string id in required)
            if (!seen.Contains(id))
                throw new InvalidOperationException($"Required visual-gate capture view missing: {id}");

        Debug.Log("4K capture contract valid: 3840x2160, hero/oblique/grazing, pixel-exact 100% crops.");
    }

    private static void PrepareAndValidateScene()
    {
        // Rebuild the highest-quality generated fallback before capture. Ground detail is the final
        // structural generated-art pass and internally rebuilds the foliage/danchi/tree quality chain.
        // Metric microdetail and broad base-ground anti-repeat PBR are then layered without changing
        // gameplay collision. Physical grade/contact interfaces, facade optics and finally the saved
        // sky/reflection environment are applied before validation so every scored crop contains the
        // current construction/material/light-transport work. Authored replacement art still wins
        // through art slots. Godot/base stay untouched.
        QualityBlockGroundDetailUpgrade.BuildDetailedGround();
        QualityBlockGroundMicrodetailUpgrade.BuildAndApply();
        QualityBlockGroundBaseSurfaceUpgrade.BuildAndApply();
        QualityBlockGroundContactInterfaceUpgrade.ApplyToOpenScene();
        QualityBlockFacadeOpticsUpgrade.BuildAndApply();
        QualityBlockEnvironmentLightingUpgrade.BuildAndApply();
        QualityBlockDanchiDetailUpgrade.ValidateOpenScene();
        QualityBlockDetailBevelUpgrade.ValidateOpenScene();
        QualityBlockDanchiLodUpgrade.ValidateOpenScene();
        QualityBlockTreeDetailUpgrade.ValidateOpenScene();
        QualityBlockFoliageOpticsUpgrade.ValidateOpenScene();
        QualityBlockGroundDetailUpgrade.ValidateOpenScene();
        QualityBlockGroundMicrodetailUpgrade.Validate();
        QualityBlockGroundBaseSurfaceUpgrade.Validate();
        QualityBlockGroundContactInterfaceUpgrade.ValidateOpenScene();
        QualityBlockFacadeOpticsUpgrade.ValidateOpenScene();
        QualityBlockEnvironmentLightingUpgrade.ValidateOpenScene();
        QualityBlockMaterialConstructionQA.ValidateRegistry();
        QualityBlockVisualFidelityGate.ValidateGateConfig();
        ValidateCaptureContract();
    }

    private static CaptureRecord CaptureView(Camera cam, ViewSpec view)
    {
        cam.transform.position = view.position;
        Vector3 forward = view.target - view.position;
        if (forward.sqrMagnitude < 0.001f)
            throw new InvalidOperationException($"Capture view {view.id} has an invalid target.");
        cam.transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        cam.fieldOfView = view.fieldOfView;
        cam.aspect = Width / (float)Height;

        int msaaSamples = ResolveSupportedMsaaSamples();
        var msaaDescriptor = new RenderTextureDescriptor(Width, Height, RenderTextureFormat.ARGB32, 24)
        {
            msaaSamples = msaaSamples,
            useMipMap = false,
            autoGenerateMips = false,
            sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear,
        };
        var resolveDescriptor = msaaDescriptor;
        resolveDescriptor.msaaSamples = 1;

        var msaaTarget = new RenderTexture(msaaDescriptor) { name = $"QA4K_{view.id}_MSAA" };
        var resolvedTarget = new RenderTexture(resolveDescriptor) { name = $"QA4K_{view.id}_Resolved" };
        var fullFrame = new Texture2D(Width, Height, TextureFormat.RGB24, false, false)
        {
            name = $"QA4K_{view.id}_Readback"
        };

        string fullAssetPath = $"{OutputDir}/{view.id}_3840x2160.png";
        var cropRecords = new List<CropRecord>();
        RenderTexture previousActive = RenderTexture.active;

        try
        {
            msaaTarget.Create();
            resolvedTarget.Create();
            cam.targetTexture = msaaTarget;
            cam.Render();
            Graphics.Blit(msaaTarget, resolvedTarget);

            RenderTexture.active = resolvedTarget;
            fullFrame.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
            fullFrame.Apply(false, false);
            WritePng(fullAssetPath, fullFrame);

            foreach (CropSpec crop in view.crops)
            {
                string cropAssetPath = $"{OutputDir}/{view.id}_crop_{crop.id}_{crop.width}x{crop.height}_100pct.png";
                WritePixelExactCrop(fullFrame, cropAssetPath, crop);
                cropRecords.Add(new CropRecord
                {
                    id = crop.id,
                    assetPath = cropAssetPath,
                    x = crop.x,
                    y = crop.y,
                    width = crop.width,
                    height = crop.height,
                    resampled = false,
                });
            }
        }
        finally
        {
            cam.targetTexture = null;
            RenderTexture.active = previousActive;
            UnityEngine.Object.DestroyImmediate(fullFrame);
            msaaTarget.Release();
            resolvedTarget.Release();
            UnityEngine.Object.DestroyImmediate(msaaTarget);
            UnityEngine.Object.DestroyImmediate(resolvedTarget);
        }

        return new CaptureRecord
        {
            viewId = view.id,
            assetPath = fullAssetPath,
            width = Width,
            height = Height,
            cameraPosition = ToArray(view.position),
            cameraTarget = ToArray(view.target),
            fieldOfView = view.fieldOfView,
            msaaSamples = msaaSamples,
            cropRecords = cropRecords.ToArray(),
        };
    }

    private static int ResolveSupportedMsaaSamples()
    {
        var descriptor = new RenderTextureDescriptor(Width, Height, RenderTextureFormat.ARGB32, 24)
        {
            msaaSamples = 8,
            useMipMap = false,
            autoGenerateMips = false,
        };
        int supported = SystemInfo.GetRenderTextureSupportedMSAASampleCount(descriptor);
        if (supported >= 8) return 8;
        if (supported >= 4) return 4;
        if (supported >= 2) return 2;
        return 1;
    }

    private static void WritePixelExactCrop(Texture2D source, string assetPath, CropSpec crop)
    {
        // GetPixels uses the source texels directly. No scaling/filtering is applied, so the crop is
        // a true 100% inspection region from the native 4K frame.
        var cropTexture = new Texture2D(crop.width, crop.height, TextureFormat.RGB24, false, false);
        try
        {
            cropTexture.SetPixels(source.GetPixels(crop.x, crop.y, crop.width, crop.height));
            cropTexture.Apply(false, false);
            WritePng(assetPath, cropTexture);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(cropTexture);
        }
    }

    private static void WritePng(string assetPath, Texture2D texture)
    {
        string absolute = AbsolutePath(assetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute));
        File.WriteAllBytes(absolute, texture.EncodeToPNG());
    }

    private static void WriteManifest(CaptureRecord[] captures)
    {
        QualityBlockEnvironmentContext context = UnityEngine.Object.FindFirstObjectByType<QualityBlockEnvironmentContext>();
        SolarSample solar = context != null ? context.CalculateSolarSample() : default;

        var manifest = new CaptureManifest
        {
            schemaVersion = "1.1",
            generatedUtc = DateTime.UtcNow.ToString("O"),
            unityVersion = Application.unityVersion,
            graphicsDevice = SystemInfo.graphicsDeviceName,
            graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
            projectColorSpace = QualitySettings.activeColorSpace.ToString(),
            width = Width,
            height = Height,
            source = "Unity Camera.Render -> MSAA RenderTexture -> single-sample resolve -> Texture2D.ReadPixels",
            renderProducedByUnity = true,
            visualFidelityStatus = "UNSCORED_REVIEW_REQUIRED",
            cropPolicy = "Pixel-exact source-texel crops; no resampling or sharpening.",
            reflectionEnvironment = new ReflectionEnvironmentState
            {
                skyboxMaterial = RenderSettings.skybox != null ? RenderSettings.skybox.name : string.Empty,
                ambientMode = RenderSettings.ambientMode.ToString(),
                defaultReflectionMode = RenderSettings.defaultReflectionMode.ToString(),
                defaultReflectionResolution = RenderSettings.defaultReflectionResolution,
                reflectionIntensity = RenderSettings.reflectionIntensity,
                localRealtimeProbeCount = UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None).Length,
                probesRefreshedImmediatelyBeforeCapture = true,
            },
            sunState = new SunState
            {
                coherentAcrossAllViews = true,
                latitudeDegrees = context != null ? context.LatitudeDegrees : 0f,
                dayOfYear = context != null ? context.DayOfYear : 0,
                solarTimeHours = context != null ? context.SolarTimeHours : 0f,
                elevationDegrees = solar.ElevationDegrees,
                azimuthDegrees = solar.AzimuthDegrees,
                directionToSun = ToArray(solar.DirectionToSun),
            },
            captures = captures,
            note = "This manifest records actual capture production only. It is not visual_fidelity_evidence.json and cannot award a Visual Fidelity score."
        };

        string absolute = AbsolutePath(ManifestPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute));
        File.WriteAllText(absolute, JsonUtility.ToJson(manifest, true));
    }

    private static string AbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private static float[] ToArray(Vector3 v) => new[] { v.x, v.y, v.z };

    private sealed class ViewSpec
    {
        public readonly string id;
        public readonly Vector3 position;
        public readonly Vector3 target;
        public readonly float fieldOfView;
        public readonly CropSpec[] crops;

        public ViewSpec(string id, Vector3 position, Vector3 target, float fieldOfView, CropSpec[] crops)
        {
            this.id = id;
            this.position = position;
            this.target = target;
            this.fieldOfView = fieldOfView;
            this.crops = crops;
        }
    }

    private sealed class CropSpec
    {
        public readonly string id;
        public readonly int x;
        public readonly int y;
        public readonly int width;
        public readonly int height;

        public CropSpec(string id, int x, int y, int width, int height)
        {
            this.id = id;
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
        }
    }

    [Serializable]
    private sealed class CaptureManifest
    {
        public string schemaVersion;
        public string generatedUtc;
        public string unityVersion;
        public string graphicsDevice;
        public string graphicsApi;
        public string projectColorSpace;
        public int width;
        public int height;
        public string source;
        public bool renderProducedByUnity;
        public string visualFidelityStatus;
        public string cropPolicy;
        public ReflectionEnvironmentState reflectionEnvironment;
        public SunState sunState;
        public CaptureRecord[] captures;
        public string note;
    }

    [Serializable]
    private sealed class ReflectionEnvironmentState
    {
        public string skyboxMaterial;
        public string ambientMode;
        public string defaultReflectionMode;
        public int defaultReflectionResolution;
        public float reflectionIntensity;
        public int localRealtimeProbeCount;
        public bool probesRefreshedImmediatelyBeforeCapture;
    }

    [Serializable]
    private sealed class SunState
    {
        public bool coherentAcrossAllViews;
        public float latitudeDegrees;
        public int dayOfYear;
        public float solarTimeHours;
        public float elevationDegrees;
        public float azimuthDegrees;
        public float[] directionToSun;
    }

    [Serializable]
    private sealed class CaptureRecord
    {
        public string viewId;
        public string assetPath;
        public int width;
        public int height;
        public float[] cameraPosition;
        public float[] cameraTarget;
        public float fieldOfView;
        public int msaaSamples;
        public CropRecord[] cropRecords;
    }

    [Serializable]
    private sealed class CropRecord
    {
        public string id;
        public string assetPath;
        public int x;
        public int y;
        public int width;
        public int height;
        public bool resampled;
    }
}
