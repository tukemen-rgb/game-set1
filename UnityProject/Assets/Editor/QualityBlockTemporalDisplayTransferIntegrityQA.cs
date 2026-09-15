using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Fail-closed display-transfer and resolve guard for native-4K temporal evidence.
/// Temporal shimmer/LOD evidence must use the same final-pixel contract as the still packet:
/// Linear scene/HDR camera -> filmic LDR transform -> canonical sRGB LDR target -> in-place
/// fixed-function MSAA resolve -> direct ReadPixels. No shader copy is allowed after tonemapping.
/// This gate awards zero Visual Fidelity points.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockTemporalDisplayTransferIntegrityQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/temporal_display_transfer_integrity_contract.json";
    private const string TemporalSourcePath = "Assets/Editor/QualityBlockTemporalStabilityCapture.cs";
    private const string TemporalTargetPrefix = "QATemporal_";
    private const int NativeWidth = 3840;
    private const int NativeHeight = 2160;

    private static readonly Regex TargetName = new Regex(
        @"^QATemporal_(subpixel_grazing|lod_walk_oblique)_\d{2}_MSAA$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex GraphicsBlitCall = new Regex(
        @"Graphics\s*\.\s*Blit\s*\(",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    static QualityBlockTemporalDisplayTransferIntegrityQA()
    {
        Camera.onPreCull -= OnCameraPreCull;
        Camera.onPreCull += OnCameraPreCull;
        AssemblyReloadEvents.beforeAssemblyReload -= BeforeAssemblyReload;
        AssemblyReloadEvents.beforeAssemblyReload += BeforeAssemblyReload;
    }

    [MenuItem("NewTown/QA/Validate Temporal Display Transfer Integrity")]
    public static void ValidateContractConfigOnly()
    {
        if (!File.Exists(ContractPath))
            throw new FileNotFoundException("Temporal display-transfer contract missing: " + ContractPath);
        if (!File.Exists(TemporalSourcePath))
            throw new FileNotFoundException("Temporal capture source missing: " + TemporalSourcePath);

        TemporalDisplayTransferContract contract = JsonUtility.FromJson<TemporalDisplayTransferContract>(File.ReadAllText(ContractPath));
        if (contract == null || !string.Equals(contract.schemaVersion, "1.0", StringComparison.Ordinal))
            throw new InvalidOperationException("Temporal display-transfer contract is null/unparseable or not schema 1.0.");
        if (!string.Equals(contract.temporalCaptureSourcePath, TemporalSourcePath, StringComparison.Ordinal) ||
            !string.Equals(contract.scenePath, ScenePath, StringComparison.Ordinal) ||
            !string.Equals(contract.formalTargetPrefix, TemporalTargetPrefix, StringComparison.Ordinal))
            throw new InvalidOperationException("Temporal display-transfer contract source/scene/target identity drifted.");

        Requirements r = contract.requirements;
        if (r == null ||
            !r.linearProjectColorSpaceRequired ||
            !r.hdrCameraRequired ||
            !r.filmicLdrTransformRequired ||
            !r.nativeLdrTargetMustUseSrgbReadWrite ||
            !r.bindTextureMSMustBeFalse ||
            !r.inPlaceFixedFunctionResolveRequired ||
            !r.postTonemapShaderCopyForbidden ||
            !r.directReadbackFromCanonicalResolvedSurface ||
            !r.native3840x2160Required ||
            !r.actualUnityRenderRequiredForVisualPoints ||
            !r.visualPointsAwardedByThisGateMustRemainZero)
            throw new InvalidOperationException("Temporal display-transfer requirements were weakened or are incomplete.");
        if (contract.runtimeVerified || contract.visualFidelityPointsAwarded != 0)
            throw new InvalidOperationException("Temporal display-transfer source contract cannot claim runtime verification or visual points.");

        string source = File.ReadAllText(TemporalSourcePath);
        foreach (string token in new[]
        {
            "new RenderTextureDescriptor(Width, Height, RenderTextureFormat.ARGB32, 24)",
            "bindMS = false",
            "sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear",
            "msaaTarget.ResolveAntiAliasedSurface();",
            "RenderTexture.active = msaaTarget;",
            "fullFrame.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);",
            "TextureFormat.RGB24, false, false",
            "in-place fixed-function resolve -> direct Texture2D.ReadPixels"
        })
        {
            if (source.IndexOf(token, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException(
                    "Temporal capture source lost required canonical display-transfer token: " + token);
        }

        if (GraphicsBlitCall.IsMatch(source) ||
            source.IndexOf("resolvedTarget", StringComparison.Ordinal) >= 0 ||
            source.IndexOf("resolveDescriptor", StringComparison.Ordinal) >= 0)
            throw new InvalidOperationException(
                "Temporal 4K capture contains a shader-copy/secondary-resolve path after the filmic transform. " +
                "Formal temporal evidence requires in-place fixed-function resolve and direct readback.");

        // Both still and temporal evidence share the same filmic shader contract. If the common display
        // transform is no longer valid, temporal evidence is not independently trustworthy.
        QualityBlockDisplayTransferIntegrityQA.ValidateContractConfigOnly();
    }

    private static void OnCameraPreCull(Camera camera)
    {
        if (camera == null || !camera.gameObject.scene.IsValid() ||
            !string.Equals(camera.gameObject.scene.path, ScenePath, StringComparison.Ordinal) ||
            Camera.main != camera)
            return;

        RenderTexture target = camera.targetTexture;
        if (target == null || string.IsNullOrEmpty(target.name) ||
            !target.name.StartsWith(TemporalTargetPrefix, StringComparison.Ordinal))
            return;

        try
        {
            ValidateContractConfigOnly();

            if (!TargetName.IsMatch(target.name))
                throw new InvalidOperationException("Unexpected formal temporal target name: " + target.name);
            if (QualitySettings.activeColorSpace != ColorSpace.Linear)
                throw new InvalidOperationException("Temporal 4K evidence requires Linear project color space.");
            if (target.width != NativeWidth || target.height != NativeHeight)
                throw new InvalidOperationException(
                    $"Temporal target {target.name} is {target.width}x{target.height}; expected {NativeWidth}x{NativeHeight}.");
            if (IsHdrFormat(target.format))
                throw new InvalidOperationException(
                    $"Temporal final camera target must be LDR before PNG readback; got {target.format}.");
            if (!target.sRGB)
                throw new InvalidOperationException(
                    "Temporal native LDR target has RenderTexture.sRGB=false. Exposure/color/aliasing evidence would not match the still packet.");
            if (target.bindTextureMS)
                throw new InvalidOperationException(
                    "Temporal target exposes raw multisample storage. Canonical evidence requires bindTextureMS=false and a resolved surface.");
            if (target.useMipMap || target.autoGenerateMips)
                throw new InvalidOperationException(
                    "Temporal target enables mipmaps. Native 1:1 temporal evidence must preserve level-zero pixels.");
            if (!camera.allowHDR || camera.allowDynamicResolution)
                throw new InvalidOperationException(
                    "Temporal MainCamera must keep HDR enabled and dynamic resolution disabled.");

            QualityBlockFilmicTonemap[] effects = camera.GetComponents<QualityBlockFilmicTonemap>();
            if (effects.Length != 1 || !effects[0].enabled)
                throw new InvalidOperationException(
                    $"Expected one enabled QualityBlockFilmicTonemap on temporal MainCamera; found {effects.Length}.");

            MethodInfo onRenderImage = typeof(QualityBlockFilmicTonemap).GetMethod(
                "OnRenderImage", BindingFlags.Instance | BindingFlags.NonPublic);
            if (onRenderImage == null || !Attribute.IsDefined(onRenderImage, typeof(ImageEffectTransformsToLDR), true))
                throw new InvalidOperationException(
                    "QualityBlockFilmicTonemap.OnRenderImage lost ImageEffectTransformsToLDR during temporal evidence capture.");
        }
        catch
        {
            // Callback exceptions are not trusted to stop every backend. Disable the mandatory transform so
            // downstream temporal sealing cannot accidentally certify a frame captured after this failure.
            foreach (QualityBlockFilmicTonemap effect in camera.GetComponents<QualityBlockFilmicTonemap>())
                effect.enabled = false;
            throw;
        }
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

    [Serializable]
    private sealed class TemporalDisplayTransferContract
    {
        public string schemaVersion;
        public string temporalCaptureSourcePath;
        public string scenePath;
        public string formalTargetPrefix;
        public Requirements requirements;
        public bool runtimeVerified;
        public int visualFidelityPointsAwarded;
    }

    [Serializable]
    private sealed class Requirements
    {
        public bool linearProjectColorSpaceRequired;
        public bool hdrCameraRequired;
        public bool filmicLdrTransformRequired;
        public bool nativeLdrTargetMustUseSrgbReadWrite;
        public bool bindTextureMSMustBeFalse;
        public bool inPlaceFixedFunctionResolveRequired;
        public bool postTonemapShaderCopyForbidden;
        public bool directReadbackFromCanonicalResolvedSurface;
        public bool native3840x2160Required;
        public bool actualUnityRenderRequiredForVisualPoints;
        public bool visualPointsAwardedByThisGateMustRemainZero;
    }
}
