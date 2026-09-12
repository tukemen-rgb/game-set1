using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Fail-closed color-transfer guard for authoritative native-4K evidence.
///
/// The benchmark shades in Linear color space, keeps scene highlights in a floating-point HDR image-effect
/// source, applies the filmic display transform in linear light, and must finally write those LDR values into
/// an sRGB render target before PNG readback. A valid-looking render with a linear-coded LDR target or a
/// manually gamma-encoded tonemap would invalidate exposure/color/material judgments across every category.
///
/// This guard awards zero Visual Fidelity points. It only makes the capture path fail closed when the actual
/// MainCamera target or the source bindings no longer describe the intended single linear-to-sRGB transfer.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockDisplayTransferIntegrityQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/display_transfer_integrity_contract.json";
    private const string CaptureSourcePath = "Assets/Editor/QualityBlock4KCapture.cs";
    private const string TonemapSourcePath = "Assets/Scripts/Art/QualityBlockFilmicTonemap.cs";
    private const string TonemapShaderPath = "Assets/Shaders/NewTownFilmicTonemap.shader";
    private const int NativeWidth = 3840;
    private const int NativeHeight = 2160;

    private static readonly string[] RequiredViews = { "hero", "oblique", "grazing" };
    private static int nextExpectedView;

    static QualityBlockDisplayTransferIntegrityQA()
    {
        Camera.onPreCull -= OnCameraPreCull;
        Camera.onPreCull += OnCameraPreCull;
        AssemblyReloadEvents.beforeAssemblyReload -= BeforeAssemblyReload;
        AssemblyReloadEvents.beforeAssemblyReload += BeforeAssemblyReload;
    }

    [MenuItem("NewTown/QA/Validate Display Transfer Integrity Contract")]
    public static void ValidateContractConfigOnly()
    {
        if (!File.Exists(ContractPath))
            throw new InvalidOperationException("Display-transfer integrity contract is missing: " + ContractPath);

        string contract = File.ReadAllText(ContractPath);
        foreach (string token in new[]
        {
            "display-transfer-integrity-v1.0.0",
            "Linear",
            "floatingPointHdrSourceMustBeLinear",
            "nativeLdrTargetMustUseSrgbReadWrite",
            "tonemapShaderMustOutputLinearLdr",
            "manualGammaEncodingForbidden",
            "resolvedTargetMustInheritSrgbDescriptor",
            "hero",
            "oblique",
            "grazing",
            "3840",
            "2160",
            "visualFidelityPointsAwarded",
            "PENDING_UNITY_RUNTIME"
        })
        {
            if (contract.IndexOf(token, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("Display-transfer integrity contract missing required token: " + token);
        }

        ValidateCaptureSourceBinding();
        ValidateTonemapSourceBinding();

        Debug.Log(
            "Display-transfer integrity contract valid: authoritative 4K targets are source-bound to sRGB read/write, " +
            "HDR image-effect input remains linear, and the filmic shader is forbidden from manually gamma-encoding. " +
            "Actual target properties still require Unity runtime verification; Visual Fidelity points awarded = 0.");
    }

    private static void OnCameraPreCull(Camera camera)
    {
        if (camera == null || camera.gameObject.scene.path != ScenePath)
            return;
        if (Camera.main != camera)
            return;

        RenderTexture target = camera.targetTexture;
        if (target == null)
            return;

        string targetName = target.name ?? string.Empty;
        string viewId = RequiredViews.FirstOrDefault(id =>
            string.Equals(targetName, $"QA4K_{id}_MSAA", StringComparison.Ordinal));
        if (string.IsNullOrEmpty(viewId))
            return;

        try
        {
            // A new canonical packet always starts at hero. Reset only after a complete prior packet so an
            // interrupted hero->oblique sequence cannot be silently accepted as a new epoch.
            if (nextExpectedView == RequiredViews.Length && string.Equals(viewId, RequiredViews[0], StringComparison.Ordinal))
                nextExpectedView = 0;

            if (nextExpectedView >= RequiredViews.Length ||
                !string.Equals(viewId, RequiredViews[nextExpectedView], StringComparison.Ordinal))
            {
                string expected = nextExpectedView < RequiredViews.Length ? RequiredViews[nextExpectedView] : "new hero epoch";
                throw new InvalidOperationException(
                    $"Authoritative 4K display-transfer sequence drifted: got '{viewId}', expected '{expected}'.");
            }

            ValidateContractConfigOnly();

            if (QualitySettings.activeColorSpace != ColorSpace.Linear)
                throw new InvalidOperationException("Authoritative 4K display transfer requires Linear project color space.");
            if (target.width != NativeWidth || target.height != NativeHeight)
                throw new InvalidOperationException(
                    $"Authoritative 4K target is not native {NativeWidth}x{NativeHeight}: {target.width}x{target.height}.");
            if (IsHdrFormat(target.format))
                throw new InvalidOperationException(
                    $"The final camera target must be LDR before PNG evidence; observed HDR target format {target.format}.");
            if (!target.sRGB)
                throw new InvalidOperationException(
                    "The native LDR MainCamera target is linear-coded (RenderTexture.sRGB=false). " +
                    "That would omit the required Linear->sRGB display transfer and invalidate exposure/color review.");
            if (!camera.allowHDR || camera.allowDynamicResolution)
                throw new InvalidOperationException("MainCamera must keep HDR enabled and dynamic resolution disabled for authoritative evidence.");

            QualityBlockFilmicTonemap[] effects = camera.GetComponents<QualityBlockFilmicTonemap>();
            if (effects.Length != 1 || !effects[0].enabled)
                throw new InvalidOperationException(
                    $"Expected one enabled QualityBlockFilmicTonemap on the authoritative MainCamera; found {effects.Length}.");

            MethodInfo onRenderImage = typeof(QualityBlockFilmicTonemap).GetMethod(
                "OnRenderImage", BindingFlags.Instance | BindingFlags.NonPublic);
            if (onRenderImage == null || !Attribute.IsDefined(onRenderImage, typeof(ImageEffectTransformsToLDR), true))
                throw new InvalidOperationException(
                    "QualityBlockFilmicTonemap.OnRenderImage lost ImageEffectTransformsToLDR; HDR->LDR termination is ambiguous.");

            nextExpectedView++;
        }
        catch
        {
            // Camera callback exceptions are not trusted to abort every Unity render backend. Disable the
            // mandatory tonemap so the existing exact-invocation HDR runtime receipt cannot later seal the
            // packet even if Camera.Render continues after this callback failure.
            DisableTonemap(camera);
            nextExpectedView = 0;
            throw;
        }
    }

    private static void ValidateCaptureSourceBinding()
    {
        string source = ReadRequiredSource(CaptureSourcePath);
        foreach (string token in new[]
        {
            "new RenderTextureDescriptor(Width, Height, RenderTextureFormat.ARGB32, 24)",
            "sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear",
            "var resolveDescriptor = msaaDescriptor;",
            "resolveDescriptor.msaaSamples = 1;",
            "new RenderTexture(resolveDescriptor)",
            "Graphics.Blit(msaaTarget, resolvedTarget);",
            "RenderTexture.active = resolvedTarget;",
            "TextureFormat.RGB24, false, false"
        })
        {
            if (source.IndexOf(token, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException(
                    "Native 4K capture source lost required display-transfer binding token: " + token);
        }
    }

    private static void ValidateTonemapSourceBinding()
    {
        string source = ReadRequiredSource(TonemapSourcePath);
        foreach (string token in new[]
        {
            "[ImageEffectTransformsToLDR]",
            "sceneLinear",
            "Graphics.Blit(source, destination, material, 0);",
            "LastSourceSrgb",
            "LastDestinationSrgb"
        })
        {
            if (source.IndexOf(token, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("Filmic-tonemap source lost required color-transfer token: " + token);
        }

        string shader = ReadRequiredSource(TonemapShaderPath);
        foreach (string forbidden in new[] { "LinearToGammaSpace", "GammaToLinearSpace", "pow(color,", "pow(color ," })
        {
            if (shader.IndexOf(forbidden, StringComparison.OrdinalIgnoreCase) >= 0)
                throw new InvalidOperationException(
                    "Filmic display shader contains an explicit gamma-transfer operation ('" + forbidden +
                    "'). The authoritative sRGB render target owns the one Linear->sRGB encoding step.");
        }
    }

    private static string ReadRequiredSource(string assetPath)
    {
        if (!File.Exists(assetPath))
            throw new InvalidOperationException("Required display-transfer source is missing: " + assetPath);
        return File.ReadAllText(assetPath);
    }

    private static void DisableTonemap(Camera camera)
    {
        if (camera == null) return;
        foreach (QualityBlockFilmicTonemap effect in camera.GetComponents<QualityBlockFilmicTonemap>())
            effect.enabled = false;
    }

    private static bool IsHdrFormat(RenderTextureFormat format)
    {
        return format == RenderTextureFormat.ARGBHalf ||
               format == RenderTextureFormat.ARGBFloat ||
               format == RenderTextureFormat.RGB111110Float ||
               format == RenderTextureFormat.DefaultHDR;
    }

    private static void BeforeAssemblyReload()
    {
        Camera.onPreCull -= OnCameraPreCull;
        AssemblyReloadEvents.beforeAssemblyReload -= BeforeAssemblyReload;
    }
}
