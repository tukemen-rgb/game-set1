using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Read-only lifecycle guard for raw QATemporal native-4K evidence.
/// Temporal shimmer/LOD judgement is only meaningful when one frame is rendered from one immutable
/// camera/target/physical-lighting/renderable state. This guard fingerprints that state at pre-cull and
/// requires byte-identical SHA-256 inputs again at pre-render and post-render. It awards zero visual points.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockRawTemporalEvidencePurityQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/raw_temporal_evidence_purity_contract.json";
    private const int Width = 3840;
    private const int Height = 2160;

    private static readonly string[] TargetFamilies =
    {
        "QATemporal_subpixel_grazing_<frame>_MSAA",
        "QATemporal_lod_walk_oblique_<frame>_MSAA"
    };

    private static readonly Dictionary<int, FrameTrace> ActiveFrames = new Dictionary<int, FrameTrace>();

    static QualityBlockRawTemporalEvidencePurityQA()
    {
        Camera.onPreCull -= OnPreCull;
        Camera.onPreRender -= OnPreRender;
        Camera.onPostRender -= OnPostRender;
        Camera.onPreCull += OnPreCull;
        Camera.onPreRender += OnPreRender;
        Camera.onPostRender += OnPostRender;
    }

    [MenuItem("NewTown/QA/Validate Raw Temporal Evidence Purity Contract")]
    public static void ValidateContractConfigOnly()
    {
        if (!File.Exists(ContractPath))
            throw new FileNotFoundException("Raw temporal evidence purity contract missing: " + ContractPath);

        RawTemporalContract contract = JsonUtility.FromJson<RawTemporalContract>(File.ReadAllText(ContractPath));
        if (contract == null || contract.schemaVersion != "1.0")
            throw new InvalidOperationException("Raw temporal evidence purity contract is unreadable or not schema 1.0.");
        if (contract.scenePath != ScenePath || contract.width != Width || contract.height != Height)
            throw new InvalidOperationException("Raw temporal evidence purity scene/native dimensions drifted.");
        if (contract.targetFamilies == null ||
            !new HashSet<string>(contract.targetFamilies, StringComparer.Ordinal).SetEquals(TargetFamilies))
            throw new InvalidOperationException("Raw temporal evidence target families drifted.");

        Requirements r = contract.requirements;
        int[] expectedSamples = { 1, 2, 4, 8 };
        if (r == null || !r.mainCameraOnly || !r.builtInForwardRequired || !r.perspectiveRequired ||
            !r.hdrRequired || !r.dynamicResolutionForbidden || !r.singleApprovedFilmicImageEffectRequired ||
            !r.cameraCommandBuffersForbidden || !r.native3840x2160Argb32Required ||
            !r.linearProjectSrgbTargetRequired || !r.mipmapsForbidden ||
            r.allowedMsaaSamples == null || !r.allowedMsaaSamples.OrderBy(x => x).SequenceEqual(expectedSamples) ||
            !r.preCullPreRenderPostRenderStateEqualityRequired || !r.physicalLightingFingerprintEqualityRequired ||
            !r.renderableSceneAndMaterialFingerprintEqualityRequired || !r.giAndReflectionStateFingerprintEqualityRequired ||
            !r.readOnlyDuringFormalLifecycle || !r.actualUnityRenderRequiredForVisualPoints || !r.manualPixelReviewRequired)
            throw new InvalidOperationException("Raw temporal evidence purity requirements were weakened or are incomplete.");

        if (contract.runtimeVerified || contract.visualFidelityPointsAwarded != 0)
            throw new InvalidOperationException("Source raw-temporal purity configuration cannot claim runtime verification or award visual points.");
    }

    private static void OnPreCull(Camera camera)
    {
        if (!TryGetRawTemporalTarget(camera, out RenderTexture target))
            return;

        ValidatePhase(camera, target, "raw temporal pre-cull");
        int key = target.GetInstanceID();
        if (ActiveFrames.ContainsKey(key))
        {
            ActiveFrames.Remove(key);
            throw new InvalidOperationException("Nested/stale raw temporal render-target lifecycle: " + target.name);
        }

        ActiveFrames.Add(key, new FrameTrace
        {
            cameraTargetSha256 = BuildCameraTargetSha256(camera, target),
            physicalStateSha256 = QualityBlockReflectionLightingStateFingerprint.BuildCurrentSha256(),
            preRenderSeen = false
        });
    }

    private static void OnPreRender(Camera camera)
    {
        if (!TryGetRawTemporalTarget(camera, out RenderTexture target))
            return;

        int key = target.GetInstanceID();
        if (!ActiveFrames.TryGetValue(key, out FrameTrace trace))
            throw new InvalidOperationException("Raw temporal camera reached pre-render without guarded pre-cull: " + target.name);

        ValidatePhase(camera, target, "raw temporal pre-render");
        RequireFingerprintMatch(camera, target, trace, "pre-cull to pre-render");
        trace.preRenderSeen = true;
        ActiveFrames[key] = trace;
    }

    private static void OnPostRender(Camera camera)
    {
        if (!TryGetRawTemporalTarget(camera, out RenderTexture target))
            return;

        int key = target.GetInstanceID();
        try
        {
            if (!ActiveFrames.TryGetValue(key, out FrameTrace trace) || !trace.preRenderSeen)
                throw new InvalidOperationException("Raw temporal camera reached post-render without a complete guarded lifecycle: " + target.name);

            ValidatePhase(camera, target, "raw temporal post-render");
            RequireFingerprintMatch(camera, target, trace, "pre-cull to post-render");
        }
        finally
        {
            ActiveFrames.Remove(key);
        }
    }

    private static void RequireFingerprintMatch(Camera camera, RenderTexture target, FrameTrace trace, string interval)
    {
        string cameraTarget = BuildCameraTargetSha256(camera, target);
        if (!string.Equals(cameraTarget, trace.cameraTargetSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Raw temporal camera/target state changed during {interval} for {target.name}; frame evidence is rejected.");

        string physical = QualityBlockReflectionLightingStateFingerprint.BuildCurrentSha256();
        if (!string.Equals(physical, trace.physicalStateSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Raw temporal physical lighting/renderable/material/GI/reflection state changed during {interval} for {target.name}; frame evidence is rejected.");
    }

    private static void ValidatePhase(Camera camera, RenderTexture target, string phase)
    {
        ValidateContractConfigOnly();
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            throw new InvalidOperationException(phase + ": wrong active benchmark scene.");

        // Reuse the established source-camera and physical-lighting guards. CameraEvidence is called in
        // source-preflight mode here because raw QATemporal targets intentionally permit 1/2/4/8 samples.
        QualityBlockCameraEvidencePurityQA.ValidateOpenScene();
        QualityBlockLightEvidencePurityQA.ValidateOpenScene();

        if (camera != Camera.main || camera.cameraType != CameraType.Game || camera.orthographic)
            throw new InvalidOperationException(phase + ": raw temporal evidence must use the perspective MainCamera Game camera.");
        if (GraphicsSettings.currentRenderPipeline != null || camera.renderingPath != RenderingPath.Forward ||
            camera.actualRenderingPath != RenderingPath.Forward)
            throw new InvalidOperationException(phase + ": raw temporal evidence requires Built-in Forward rendering.");
        if (!camera.allowHDR || camera.allowDynamicResolution)
            throw new InvalidOperationException(phase + ": HDR must remain enabled and dynamic resolution disabled.");
        if (target == null || !target.IsCreated() || target.width != Width || target.height != Height ||
            target.format != RenderTextureFormat.ARGB32 || target.dimension != TextureDimension.Tex2D ||
            target.volumeDepth != 1 || target.useMipMap || target.autoGenerateMips)
            throw new InvalidOperationException(phase + ": raw temporal target must remain a created native 3840x2160 ARGB32 non-mipmapped 2D RenderTexture.");
        if (QualitySettings.activeColorSpace != ColorSpace.Linear || !target.sRGB)
            throw new InvalidOperationException(phase + ": raw temporal target must be sRGB-encoded from the Linear-light project.");
        if (target.antiAliasing != 1 && target.antiAliasing != 2 && target.antiAliasing != 4 && target.antiAliasing != 8)
            throw new InvalidOperationException(phase + $": unsupported temporal MSAA sample count {target.antiAliasing}.");
    }

    private static bool TryGetRawTemporalTarget(Camera camera, out RenderTexture target)
    {
        target = null;
        if (camera == null || camera != Camera.main || !camera.gameObject.scene.IsValid() || camera.gameObject.scene.path != ScenePath)
            return false;
        target = camera.targetTexture;
        return target != null && IsApprovedRawTemporalName(target.name);
    }

    private static bool IsApprovedRawTemporalName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;
        string[] prefixes = { "QATemporal_subpixel_grazing_", "QATemporal_lod_walk_oblique_" };
        foreach (string prefix in prefixes)
        {
            if (!name.StartsWith(prefix, StringComparison.Ordinal) || !name.EndsWith("_MSAA", StringComparison.Ordinal))
                continue;
            string frame = name.Substring(prefix.Length, name.Length - prefix.Length - "_MSAA".Length);
            if (frame.Length == 2 && char.IsDigit(frame[0]) && char.IsDigit(frame[1]))
                return true;
        }
        return false;
    }

    private static string BuildCameraTargetSha256(Camera camera, RenderTexture target)
    {
        var sb = new StringBuilder(2048);
        Append(sb, "schema", "raw-temporal-camera-target-v1");
        Append(sb, "target.name", target.name);
        Append(sb, "target.instance", target.GetInstanceID());
        Append(sb, "target.width", target.width);
        Append(sb, "target.height", target.height);
        Append(sb, "target.format", target.format.ToString());
        Append(sb, "target.sRGB", target.sRGB);
        Append(sb, "target.aa", target.antiAliasing);
        Append(sb, "target.depth", target.depth);
        Append(sb, "target.dimension", target.dimension.ToString());
        Append(sb, "target.volumeDepth", target.volumeDepth);
        Append(sb, "target.useMipMap", target.useMipMap);
        Append(sb, "target.autoGenerateMips", target.autoGenerateMips);
        Append(sb, "target.filter", target.filterMode.ToString());
        Append(sb, "target.wrap", target.wrapMode.ToString());
        AppendVector3(sb, "camera.position", camera.transform.position);
        AppendQuaternion(sb, "camera.rotation", camera.transform.rotation);
        Append(sb, "camera.fov", camera.fieldOfView);
        Append(sb, "camera.aspect", camera.aspect);
        Append(sb, "camera.near", camera.nearClipPlane);
        Append(sb, "camera.far", camera.farClipPlane);
        Append(sb, "camera.cullingMask", camera.cullingMask);
        Append(sb, "camera.clearFlags", camera.clearFlags.ToString());
        Append(sb, "camera.allowHDR", camera.allowHDR);
        Append(sb, "camera.allowMSAA", camera.allowMSAA);
        Append(sb, "camera.allowDynamicResolution", camera.allowDynamicResolution);
        Append(sb, "camera.renderingPath", camera.renderingPath.ToString());
        Append(sb, "camera.actualRenderingPath", camera.actualRenderingPath.ToString());
        Append(sb, "camera.rect.x", camera.rect.x);
        Append(sb, "camera.rect.y", camera.rect.y);
        Append(sb, "camera.rect.w", camera.rect.width);
        Append(sb, "camera.rect.h", camera.rect.height);
        Append(sb, "camera.depthTextureMode", camera.depthTextureMode.ToString());
        Append(sb, "camera.useOcclusionCulling", camera.useOcclusionCulling);
        Append(sb, "quality.antiAliasing", QualitySettings.antiAliasing);
        Append(sb, "quality.lodBias", QualitySettings.lodBias);
        Append(sb, "quality.maximumLODLevel", QualitySettings.maximumLODLevel);
        Append(sb, "gl.invertCulling", GL.invertCulling);

        using (SHA256 sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()))).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static void AppendVector3(StringBuilder sb, string key, Vector3 v)
    {
        Append(sb, key + ".x", v.x); Append(sb, key + ".y", v.y); Append(sb, key + ".z", v.z);
    }

    private static void AppendQuaternion(StringBuilder sb, string key, Quaternion q)
    {
        Append(sb, key + ".x", q.x); Append(sb, key + ".y", q.y); Append(sb, key + ".z", q.z); Append(sb, key + ".w", q.w);
    }

    private static void Append(StringBuilder sb, string key, float value) => Append(sb, key, value.ToString("R", CultureInfo.InvariantCulture));
    private static void Append(StringBuilder sb, string key, int value) => Append(sb, key, value.ToString(CultureInfo.InvariantCulture));
    private static void Append(StringBuilder sb, string key, bool value) => Append(sb, key, value ? "true" : "false");
    private static void Append(StringBuilder sb, string key, string value) => sb.Append(key).Append('=').Append(value ?? string.Empty).Append('\n');

    private sealed class FrameTrace
    {
        public string cameraTargetSha256;
        public string physicalStateSha256;
        public bool preRenderSeen;
    }

    [Serializable]
    private sealed class RawTemporalContract
    {
        public string schemaVersion;
        public string scenePath;
        public int width;
        public int height;
        public string[] targetFamilies;
        public Requirements requirements;
        public bool runtimeVerified;
        public int visualFidelityPointsAwarded;
    }

    [Serializable]
    private sealed class Requirements
    {
        public bool mainCameraOnly;
        public bool builtInForwardRequired;
        public bool perspectiveRequired;
        public bool hdrRequired;
        public bool dynamicResolutionForbidden;
        public bool singleApprovedFilmicImageEffectRequired;
        public bool cameraCommandBuffersForbidden;
        public bool native3840x2160Argb32Required;
        public bool linearProjectSrgbTargetRequired;
        public bool mipmapsForbidden;
        public int[] allowedMsaaSamples;
        public bool preCullPreRenderPostRenderStateEqualityRequired;
        public bool physicalLightingFingerprintEqualityRequired;
        public bool renderableSceneAndMaterialFingerprintEqualityRequired;
        public bool giAndReflectionStateFingerprintEqualityRequired;
        public bool readOnlyDuringFormalLifecycle;
        public bool actualUnityRenderRequiredForVisualPoints;
        public bool manualPixelReviewRequired;
    }
}
