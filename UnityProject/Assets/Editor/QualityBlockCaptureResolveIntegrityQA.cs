using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Fail-closed guard for the final native-4K MSAA resolve/readback path.
/// Formal evidence may not insert a shader-based copy after the approved filmic display transform:
/// the camera renders into the canonical MSAA target, Unity performs a fixed-function resolve, and
/// ReadPixels consumes that resolved surface directly. This guard awards zero Visual Fidelity points.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockCaptureResolveIntegrityQA
{
    private const string ContractPath = "Assets/QA/capture_resolve_integrity_contract.json";
    private const string CaptureSourcePath = "Assets/Editor/QualityBlock4KCapture.cs";
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string FormalTargetPrefix = "QA4K_";

    private static readonly Regex GraphicsBlitCall = new Regex(
        @"Graphics\s*\.\s*Blit\s*\(",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    static QualityBlockCaptureResolveIntegrityQA()
    {
        Camera.onPreCull -= OnCameraPreCull;
        Camera.onPreCull += OnCameraPreCull;
    }

    [MenuItem("NewTown/QA/Validate Capture Resolve Integrity Contract")]
    public static void ValidateContractConfigOnly()
    {
        if (!File.Exists(ContractPath))
            throw new FileNotFoundException("Capture-resolve integrity contract missing: " + ContractPath);
        if (!File.Exists(CaptureSourcePath))
            throw new FileNotFoundException("Canonical native-4K capture source missing: " + CaptureSourcePath);

        CaptureResolveContract contract = JsonUtility.FromJson<CaptureResolveContract>(File.ReadAllText(ContractPath));
        if (contract == null || !string.Equals(contract.schemaVersion, "1.0", StringComparison.Ordinal))
            throw new InvalidOperationException("Capture-resolve integrity contract is null/unparseable or not schema 1.0.");
        if (!string.Equals(contract.captureSourcePath, CaptureSourcePath, StringComparison.Ordinal) ||
            !string.Equals(contract.scenePath, ScenePath, StringComparison.Ordinal) ||
            !string.Equals(contract.formalTargetPrefix, FormalTargetPrefix, StringComparison.Ordinal))
            throw new InvalidOperationException("Capture-resolve integrity contract source/scene/target identity drifted.");

        ResolveRequirements r = contract.requirements;
        if (r == null ||
            !r.bindTextureMSMustBeFalse ||
            !r.fixedFunctionResolveRequiredWhenMsaaGreaterThanOne ||
            !r.postTonemapGraphicsBlitForbidden ||
            !r.readPixelsFromCanonicalResolvedSurface ||
            !r.pixelExactCropPolicyRequired ||
            !r.actualUnityRenderRequiredForVisualPoints ||
            !r.visualPointsAwardedByThisGateMustRemainZero)
            throw new InvalidOperationException("Capture-resolve integrity requirements were weakened or are incomplete.");
        if (contract.runtimeVerified || contract.visualFidelityPointsAwarded != 0)
            throw new InvalidOperationException("Source capture-resolve configuration cannot claim runtime verification or award visual points.");

        string source = File.ReadAllText(CaptureSourcePath);
        RequireSource(source, "bindMS = false", "canonical 4K RenderTexture descriptor must explicitly disable raw multisample binding");
        RequireSource(source, "ResolveAntiAliasedSurface()", "canonical 4K capture must use Unity fixed-function in-place MSAA resolve");
        RequireSource(source, "RenderTexture.active = msaaTarget", "ReadPixels must consume the canonical resolved camera target directly");
        RequireSource(source, "no post-tonemap Graphics.Blit", "runtime manifest must disclose the no-shader-copy evidence path");

        if (GraphicsBlitCall.IsMatch(source))
            throw new InvalidOperationException(
                "Canonical 4K capture contains a shader-based Graphics.Blit call after/beside the approved filmic transform. " +
                "Formal evidence requires fixed-function MSAA resolve plus direct ReadPixels.");
    }

    private static void OnCameraPreCull(Camera camera)
    {
        if (camera == null || !camera.gameObject.scene.IsValid() ||
            !string.Equals(camera.gameObject.scene.path, ScenePath, StringComparison.Ordinal))
            return;

        RenderTexture target = camera.targetTexture;
        if (target == null || string.IsNullOrEmpty(target.name) ||
            !target.name.StartsWith(FormalTargetPrefix, StringComparison.Ordinal))
            return;

        ValidateContractConfigOnly();
        if (target.bindTextureMS)
            throw new InvalidOperationException(
                $"Formal native-4K target {target.name} has bindTextureMS=true. Raw multisample binding is forbidden because the evidence path requires a deterministic resolved surface.");
        if (target.useMipMap || target.autoGenerateMips)
            throw new InvalidOperationException(
                $"Formal native-4K target {target.name} unexpectedly enables mipmaps; 1:1 evidence readback requires the native resolved surface.");
    }

    private static void RequireSource(string source, string token, string reason)
    {
        if (string.IsNullOrEmpty(source) || source.IndexOf(token, StringComparison.Ordinal) < 0)
            throw new InvalidOperationException("Capture-resolve source guard failed: " + reason + $". Missing token '{token}'.");
    }

    [Serializable]
    private sealed class CaptureResolveContract
    {
        public string schemaVersion;
        public string captureSourcePath;
        public string scenePath;
        public string formalTargetPrefix;
        public ResolveRequirements requirements;
        public bool runtimeVerified;
        public int visualFidelityPointsAwarded;
    }

    [Serializable]
    private sealed class ResolveRequirements
    {
        public bool bindTextureMSMustBeFalse;
        public bool fixedFunctionResolveRequiredWhenMsaaGreaterThanOne;
        public bool postTonemapGraphicsBlitForbidden;
        public bool readPixelsFromCanonicalResolvedSurface;
        public bool pixelExactCropPolicyRequired;
        public bool actualUnityRenderRequiredForVisualPoints;
        public bool visualPointsAwardedByThisGateMustRemainZero;
    }
}
