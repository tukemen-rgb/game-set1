using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Fail-closed runtime evidence for the benchmark HDR -> LDR display transform.
/// Source-side camera/HDR flags are not enough: the authoritative native-4K capture must prove that
/// OnRenderImage actually received a linear HDR buffer, executed the filmic material, and wrote an sRGB
/// LDR destination for all three 3840x2160 stills. This receipt is provenance only and awards zero points.
/// </summary>
public static class QualityBlockHdrTonemapRuntimeQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/hdr_tonemap_capture_contract.json";
    private const string ReceiptPath = "Assets/QA/hdr_tonemap_runtime_receipt.json";
    private const int NativeWidth = 3840;
    private const int NativeHeight = 2160;
    private const int RequiredStillCount = 3;

    [Serializable]
    private sealed class RuntimeReceipt
    {
        public string schemaVersion = "1.2";
        public string generatedUtc;
        public string unityVersion;
        public string graphicsDeviceName;
        public string graphicsDeviceType;
        public string scenePath;
        public string cameraName;
        public string shaderName;
        public bool imageEffectTransformsToLdr;
        public bool systemSupportsImageEffects;
        public bool cameraAllowHdr;
        public string cameraRenderingPath;
        public bool linearColorSpace;
        public int renderInvocationCount;
        public int tonemapAppliedInvocationCount;
        public int native4KInvocationCount;
        public int native4KTonemapAppliedCount;
        public int fallbackInvocationCount;
        public int hdrSourceInvocationCount;
        public int ldrDestinationInvocationCount;
        public int linearHdrSourceInvocationCount;
        public int srgbLdrDestinationInvocationCount;
        public string lastSourceFormat;
        public string lastDestinationFormat;
        public int lastSourceWidth;
        public int lastSourceHeight;
        public int lastDestinationWidth;
        public int lastDestinationHeight;
        public bool lastDestinationWasNull;
        public bool lastSourceSrgb;
        public bool lastDestinationSrgb;
        public bool everyStillHadHdrSource;
        public bool everyStillHadLdrDestination;
        public bool everyStillHadLinearHdrSource;
        public bool everyStillHadSrgbLdrDestination;
        public int automaticVisualPoints = 0;
        public bool visualVerificationStillRequired = true;
        public string status = "RUNTIME_LINEAR_HDR_TO_SRGB_LDR_PROVEN_FOR_EVERY_NATIVE_STILL";
    }

    [MenuItem("NewTown/QA/Validate HDR Tonemap Capture Contract")]
    public static void ValidateContractConfigOnly()
    {
        if (!File.Exists(ContractPath))
            throw new InvalidOperationException("HDR-tonemap capture contract is missing: " + ContractPath);

        string contract = File.ReadAllText(ContractPath);
        foreach (string token in new[]
        {
            "ImageEffectTransformsToLDR",
            "hdrSourceRequiredForEveryStill",
            "ldrDestinationRequiredForEveryStill",
            "linearHdrSourceRequiredForEveryStill",
            "srgbLdrDestinationRequiredForEveryStill",
            "3840",
            "2160",
            "authoritativeStillCount",
            "actualRenderRequired",
            "runtimeReceiptRequired",
            "automaticVisualPoints"
        })
        {
            if (!contract.Contains(token))
                throw new InvalidOperationException("HDR-tonemap capture contract missing token: " + token);
        }

        MethodInfo onRenderImage = typeof(QualityBlockFilmicTonemap).GetMethod(
            "OnRenderImage",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (onRenderImage == null || !Attribute.IsDefined(onRenderImage, typeof(ImageEffectTransformsToLDR), true))
            throw new InvalidOperationException("QualityBlockFilmicTonemap.OnRenderImage must carry ImageEffectTransformsToLDR so the benchmark tonemap explicitly terminates the HDR image-effect chain in LDR.");

        Debug.Log("HDR-tonemap contract QA passed. Every still must individually contribute linear-HDR-source and sRGB-LDR-destination telemetry; actual execution remains runtime-verification pending.");
    }

    /// <summary>
    /// Called by the authoritative native review packet after the scene and reflection probes are fully
    /// prepared, immediately before the three synchronous native still captures. This only resets
    /// runtime counters and validates non-mutating prerequisites; it does not rebuild/reopen the scene.
    /// </summary>
    public static void ResetBeforeNative4KCapture()
    {
        ValidateContractConfigOnly();
        QualityBlockFilmicTonemap tonemap = RequirePreparedTonemap();
        tonemap.ResetRuntimeTelemetry();
    }

    [MenuItem("NewTown/QA/Validate Latest Native 4K HDR Tonemap Receipt")]
    public static void ValidateLatestNative4KCapture()
    {
        ValidateContractConfigOnly();
        QualityBlockFilmicTonemap tonemap = RequirePreparedTonemap();

        if (tonemap.RenderInvocationCount != RequiredStillCount)
            throw new InvalidOperationException($"Expected exactly {RequiredStillCount} benchmark-camera image-effect invocations after telemetry reset, got {tonemap.RenderInvocationCount}. Evidence may include an unexpected render or a missing still.");
        if (tonemap.Native4KInvocationCount != RequiredStillCount)
            throw new InvalidOperationException($"Expected exactly {RequiredStillCount} 3840x2160 image-effect invocations, got {tonemap.Native4KInvocationCount}.");
        if (tonemap.TonemapAppliedInvocationCount != RequiredStillCount || tonemap.Native4KTonemapAppliedCount != RequiredStillCount)
            throw new InvalidOperationException("The filmic tonemap material did not execute for every authoritative native-4K still.");
        if (tonemap.FallbackInvocationCount != 0)
            throw new InvalidOperationException("At least one authoritative still bypassed the filmic tonemap through its fallback blit; evidence must not be sealed or scored.");
        if (tonemap.HdrSourceInvocationCount != RequiredStillCount)
            throw new InvalidOperationException($"Only {tonemap.HdrSourceInvocationCount}/{RequiredStillCount} authoritative stills observed a floating-point HDR image-effect source.");
        if (tonemap.LdrDestinationInvocationCount != RequiredStillCount)
            throw new InvalidOperationException($"Only {tonemap.LdrDestinationInvocationCount}/{RequiredStillCount} authoritative stills observed a non-HDR image-effect destination.");
        if (tonemap.LinearHdrSourceInvocationCount != RequiredStillCount)
            throw new InvalidOperationException($"Only {tonemap.LinearHdrSourceInvocationCount}/{RequiredStillCount} authoritative stills observed a scene-linear HDR source (HDR + sRGB=false). Display-transfer provenance is invalid.");
        if (tonemap.SrgbLdrDestinationInvocationCount != RequiredStillCount)
            throw new InvalidOperationException($"Only {tonemap.SrgbLdrDestinationInvocationCount}/{RequiredStillCount} authoritative stills observed an sRGB LDR destination. A missing Linear-to-sRGB write would invalidate exposure/color review.");

        if (tonemap.LastSourceWidth != NativeWidth || tonemap.LastSourceHeight != NativeHeight)
            throw new InvalidOperationException($"Last tonemap source was not native 3840x2160: {tonemap.LastSourceWidth}x{tonemap.LastSourceHeight}.");
        if (tonemap.LastDestinationWasNull)
            throw new InvalidOperationException("Native benchmark tonemap wrote to a null/backbuffer destination instead of the evidence render target.");
        if (tonemap.LastDestinationWidth != NativeWidth || tonemap.LastDestinationHeight != NativeHeight)
            throw new InvalidOperationException($"Last tonemap destination was not native 3840x2160: {tonemap.LastDestinationWidth}x{tonemap.LastDestinationHeight}.");

        bool hdrSource = IsHdrFormat(tonemap.LastSourceFormat);
        bool ldrDestination = !IsHdrFormat(tonemap.LastDestinationFormat);
        if (!hdrSource)
            throw new InvalidOperationException($"Benchmark tonemap source was not an observed floating-point HDR format: {tonemap.LastSourceFormat}. HDR highlights may have been truncated before the display transform.");
        if (!ldrDestination)
            throw new InvalidOperationException($"Benchmark tonemap destination remained HDR ({tonemap.LastDestinationFormat}) despite ImageEffectTransformsToLDR; PNG evidence provenance is ambiguous.");
        if (tonemap.LastSourceSrgb)
            throw new InvalidOperationException("The last authoritative HDR image-effect source reported sRGB=true. Scene-linear HDR input is required before the filmic transform.");
        if (!tonemap.LastDestinationSrgb)
            throw new InvalidOperationException("The last authoritative LDR image-effect destination reported sRGB=false. The intended single Linear-to-sRGB display transfer is missing.");

        Scene activeScene = SceneManager.GetActiveScene();
        RuntimeReceipt receipt = new RuntimeReceipt
        {
            generatedUtc = DateTime.UtcNow.ToString("O"),
            unityVersion = Application.unityVersion,
            graphicsDeviceName = SystemInfo.graphicsDeviceName,
            graphicsDeviceType = SystemInfo.graphicsDeviceType.ToString(),
            scenePath = activeScene.path,
            cameraName = Camera.main != null ? Camera.main.name : string.Empty,
            shaderName = tonemap.FilmicShader != null ? tonemap.FilmicShader.name : string.Empty,
            imageEffectTransformsToLdr = HasTransformsToLdrAttribute(),
            systemSupportsImageEffects = SystemInfo.supportsImageEffects,
            cameraAllowHdr = Camera.main != null && Camera.main.allowHDR,
            cameraRenderingPath = Camera.main != null ? Camera.main.renderingPath.ToString() : string.Empty,
            linearColorSpace = QualitySettings.activeColorSpace == ColorSpace.Linear,
            renderInvocationCount = tonemap.RenderInvocationCount,
            tonemapAppliedInvocationCount = tonemap.TonemapAppliedInvocationCount,
            native4KInvocationCount = tonemap.Native4KInvocationCount,
            native4KTonemapAppliedCount = tonemap.Native4KTonemapAppliedCount,
            fallbackInvocationCount = tonemap.FallbackInvocationCount,
            hdrSourceInvocationCount = tonemap.HdrSourceInvocationCount,
            ldrDestinationInvocationCount = tonemap.LdrDestinationInvocationCount,
            linearHdrSourceInvocationCount = tonemap.LinearHdrSourceInvocationCount,
            srgbLdrDestinationInvocationCount = tonemap.SrgbLdrDestinationInvocationCount,
            lastSourceFormat = tonemap.LastSourceFormat.ToString(),
            lastDestinationFormat = tonemap.LastDestinationFormat.ToString(),
            lastSourceWidth = tonemap.LastSourceWidth,
            lastSourceHeight = tonemap.LastSourceHeight,
            lastDestinationWidth = tonemap.LastDestinationWidth,
            lastDestinationHeight = tonemap.LastDestinationHeight,
            lastDestinationWasNull = tonemap.LastDestinationWasNull,
            lastSourceSrgb = tonemap.LastSourceSrgb,
            lastDestinationSrgb = tonemap.LastDestinationSrgb,
            everyStillHadHdrSource = tonemap.HdrSourceInvocationCount == RequiredStillCount,
            everyStillHadLdrDestination = tonemap.LdrDestinationInvocationCount == RequiredStillCount,
            everyStillHadLinearHdrSource = tonemap.LinearHdrSourceInvocationCount == RequiredStillCount,
            everyStillHadSrgbLdrDestination = tonemap.SrgbLdrDestinationInvocationCount == RequiredStillCount,
        };

        ValidateRuntimeReceipt(receipt);
        File.WriteAllText(ReceiptPath, JsonUtility.ToJson(receipt, true));
        AssetDatabase.Refresh();

        Debug.Log(
            "Native-4K HDR->LDR runtime QA passed: all three benchmark stills invoked the filmic tonemap, " +
            $"all {RequiredStillCount} observed scene-linear HDR sources and sRGB LDR destinations, last source={tonemap.LastSourceFormat}, destination={tonemap.LastDestinationFormat}, no fallback blit. " +
            "This proves display-transfer execution only; Visual Fidelity remains UNSCORED until actual pixels are reviewed.");
    }

    [MenuItem("NewTown/QA/Validate Latest Native 4K Display Transfer Receipt File")]
    public static void ValidateLatestRuntimeReceiptFile()
    {
        ValidateContractConfigOnly();
        if (!File.Exists(ReceiptPath))
            throw new InvalidOperationException("HDR-tonemap runtime receipt is missing: " + ReceiptPath);

        RuntimeReceipt receipt = JsonUtility.FromJson<RuntimeReceipt>(File.ReadAllText(ReceiptPath));
        ValidateRuntimeReceipt(receipt);
        Debug.Log("Latest native-4K display-transfer receipt is internally valid. This does not award Visual Fidelity points.");
    }

    private static void ValidateRuntimeReceipt(RuntimeReceipt receipt)
    {
        if (receipt == null)
            throw new InvalidOperationException("HDR-tonemap runtime receipt is null or unreadable.");
        if (!string.Equals(receipt.schemaVersion, "1.2", StringComparison.Ordinal))
            throw new InvalidOperationException($"HDR-tonemap runtime receipt schema must be 1.2, got '{receipt.schemaVersion}'.");
        if (string.IsNullOrWhiteSpace(receipt.generatedUtc) ||
            !DateTime.TryParse(receipt.generatedUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out _))
            throw new InvalidOperationException("HDR-tonemap runtime receipt generatedUtc is missing or invalid.");
        if (string.IsNullOrWhiteSpace(receipt.unityVersion) || string.IsNullOrWhiteSpace(receipt.graphicsDeviceName) ||
            string.IsNullOrWhiteSpace(receipt.graphicsDeviceType))
            throw new InvalidOperationException("HDR-tonemap runtime receipt is missing Unity/graphics runtime identity.");
        if (!string.Equals(receipt.scenePath, ScenePath, StringComparison.Ordinal))
            throw new InvalidOperationException($"HDR-tonemap receipt scene path drifted: '{receipt.scenePath}'.");
        if (string.IsNullOrWhiteSpace(receipt.cameraName) ||
            !string.Equals(receipt.shaderName, "Hidden/NewTown/FilmicTonemap", StringComparison.Ordinal))
            throw new InvalidOperationException("HDR-tonemap runtime receipt camera/shader identity is invalid.");
        if (!receipt.imageEffectTransformsToLdr || !receipt.systemSupportsImageEffects || !receipt.cameraAllowHdr ||
            !receipt.linearColorSpace || !string.Equals(receipt.cameraRenderingPath, RenderingPath.Forward.ToString(), StringComparison.Ordinal))
            throw new InvalidOperationException("HDR-tonemap runtime receipt lost required Forward/HDR/Linear image-effect state.");
        if (receipt.renderInvocationCount != RequiredStillCount || receipt.tonemapAppliedInvocationCount != RequiredStillCount ||
            receipt.native4KInvocationCount != RequiredStillCount || receipt.native4KTonemapAppliedCount != RequiredStillCount ||
            receipt.fallbackInvocationCount != 0)
            throw new InvalidOperationException("HDR-tonemap runtime receipt invocation counts do not represent exactly three successful authoritative stills.");
        if (receipt.hdrSourceInvocationCount != RequiredStillCount || receipt.ldrDestinationInvocationCount != RequiredStillCount ||
            receipt.linearHdrSourceInvocationCount != RequiredStillCount || receipt.srgbLdrDestinationInvocationCount != RequiredStillCount ||
            !receipt.everyStillHadHdrSource || !receipt.everyStillHadLdrDestination ||
            !receipt.everyStillHadLinearHdrSource || !receipt.everyStillHadSrgbLdrDestination)
            throw new InvalidOperationException("HDR-tonemap runtime receipt does not prove linear-HDR -> sRGB-LDR transfer for every authoritative still.");
        if (receipt.lastSourceWidth != NativeWidth || receipt.lastSourceHeight != NativeHeight ||
            receipt.lastDestinationWidth != NativeWidth || receipt.lastDestinationHeight != NativeHeight || receipt.lastDestinationWasNull)
            throw new InvalidOperationException("HDR-tonemap runtime receipt dimensions/destination identity are invalid.");
        if (receipt.lastSourceSrgb || !receipt.lastDestinationSrgb)
            throw new InvalidOperationException("HDR-tonemap runtime receipt last-buffer color-transfer state is invalid (source must be linear, destination must be sRGB). ");
        if (!Enum.TryParse(receipt.lastSourceFormat, out RenderTextureFormat sourceFormat) || !IsHdrFormat(sourceFormat))
            throw new InvalidOperationException("HDR-tonemap runtime receipt last source format is not a recognized HDR format.");
        if (!Enum.TryParse(receipt.lastDestinationFormat, out RenderTextureFormat destinationFormat) || IsHdrFormat(destinationFormat))
            throw new InvalidOperationException("HDR-tonemap runtime receipt last destination format is not a recognized LDR format.");
        if (receipt.automaticVisualPoints != 0 || !receipt.visualVerificationStillRequired ||
            !string.Equals(receipt.status, "RUNTIME_LINEAR_HDR_TO_SRGB_LDR_PROVEN_FOR_EVERY_NATIVE_STILL", StringComparison.Ordinal))
            throw new InvalidOperationException("HDR-tonemap runtime receipt scoring-separation/status fields are invalid.");
    }

    private static QualityBlockFilmicTonemap RequirePreparedTonemap()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || activeScene.path != ScenePath)
            throw new InvalidOperationException("HDR-tonemap runtime QA must operate on the already-prepared QualityBlock1990s scene without rebuilding or reopening it.");

        if (QualitySettings.activeColorSpace != ColorSpace.Linear)
            throw new InvalidOperationException("Native benchmark HDR-tonemap QA requires Linear project color space.");
        if (!SystemInfo.supportsImageEffects)
            throw new InvalidOperationException("Current Unity runtime reports image effects unsupported; authoritative filmic evidence cannot be produced.");
        if (!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf) &&
            !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGB111110Float))
            throw new InvalidOperationException("Current graphics runtime reports no supported floating-point HDR camera buffer format required by the benchmark.");

        Camera camera = Camera.main;
        if (camera == null)
            throw new InvalidOperationException("MainCamera missing from prepared benchmark scene.");
        if (camera.renderingPath != RenderingPath.Forward || !camera.allowHDR || camera.allowDynamicResolution)
            throw new InvalidOperationException("MainCamera must remain Forward + HDR with dynamic resolution disabled before native HDR-tonemap evidence capture.");

        QualityBlockFilmicTonemap[] effects = camera.GetComponents<QualityBlockFilmicTonemap>();
        if (effects.Length != 1)
            throw new InvalidOperationException($"MainCamera must contain exactly one QualityBlockFilmicTonemap, found {effects.Length}.");

        QualityBlockFilmicTonemap tonemap = effects[0];
        if (!tonemap.enabled)
            throw new InvalidOperationException("QualityBlockFilmicTonemap is disabled on MainCamera.");
        if (tonemap.FilmicShader == null || !tonemap.FilmicShader.isSupported || tonemap.FilmicShader.name != "Hidden/NewTown/FilmicTonemap")
            throw new InvalidOperationException("MainCamera filmic shader is missing, unsupported, or not Hidden/NewTown/FilmicTonemap.");
        if (!HasTransformsToLdrAttribute())
            throw new InvalidOperationException("QualityBlockFilmicTonemap.OnRenderImage lost ImageEffectTransformsToLDR.");

        return tonemap;
    }

    private static bool HasTransformsToLdrAttribute()
    {
        MethodInfo onRenderImage = typeof(QualityBlockFilmicTonemap).GetMethod(
            "OnRenderImage",
            BindingFlags.Instance | BindingFlags.NonPublic);
        return onRenderImage != null && Attribute.IsDefined(onRenderImage, typeof(ImageEffectTransformsToLDR), true);
    }

    private static bool IsHdrFormat(RenderTextureFormat format)
    {
        return format == RenderTextureFormat.ARGBHalf ||
               format == RenderTextureFormat.ARGBFloat ||
               format == RenderTextureFormat.RGB111110Float ||
               format == RenderTextureFormat.DefaultHDR;
    }
}
