using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Fail-closed camera/output guard for authoritative native-4K still and prepared-temporal renders.
///
/// Source-side cinematic QA proves the intended filmic transform is configured, but that alone cannot
/// exclude a second OnRenderImage effect, a camera command buffer, or camera/target state changing after
/// preflight. This guard validates the persisted MainCamera configuration and automatically watches the
/// canonical QA4K_/QAPreparedTemporal_ render targets at Camera.onPreCull/onPreRender/onPostRender.
///
/// For each formal frame it also revalidates the accepted asynchronous reflection wait proof before
/// culling and after scene rendering, preventing a render callback from silently changing the scene,
/// materials, textures, physical lighting or reflection-relevant state inside the still/temporal pass.
/// It is evidence-integrity infrastructure only and awards zero Visual Fidelity points.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockCameraEvidencePurityQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/camera_evidence_purity_contract.json";
    private const string CameraName = "MainCamera";
    private const string FilmicShaderName = "Hidden/NewTown/FilmicTonemap";
    private const int Width = 3840;
    private const int Height = 2160;
    private const float ExpectedExposureEV = -0.45f;
    private const float ExpectedContrast = 1.03f;
    private const float ExpectedSaturation = 0.97f;
    private const float ExpectedShadowSoftening = 0.025f;
    private const float FloatTolerance = 0.001f;

    private static readonly string[] FormalTargetPrefixes = { "QA4K_", "QAPreparedTemporal_" };
    private static readonly Dictionary<int, FrameTrace> ActiveFrames = new Dictionary<int, FrameTrace>();

    static QualityBlockCameraEvidencePurityQA()
    {
        Camera.onPreCull -= OnCameraPreCull;
        Camera.onPreRender -= OnCameraPreRender;
        Camera.onPostRender -= OnCameraPostRender;
        Camera.onPreCull += OnCameraPreCull;
        Camera.onPreRender += OnCameraPreRender;
        Camera.onPostRender += OnCameraPostRender;
    }

    [MenuItem("NewTown/QA/Validate Camera Evidence Purity Contract")]
    public static void ValidateContractConfigOnly()
    {
        if (!File.Exists(ContractPath))
            throw new FileNotFoundException("Camera evidence purity contract missing: " + ContractPath);

        CameraPurityContract contract = JsonUtility.FromJson<CameraPurityContract>(File.ReadAllText(ContractPath));
        if (contract == null || !string.Equals(contract.schemaVersion, "1.0", StringComparison.Ordinal))
            throw new InvalidOperationException("Camera evidence purity contract is null/unparseable or not schema 1.0.");
        if (!string.Equals(contract.scenePath, ScenePath, StringComparison.Ordinal) ||
            !string.Equals(contract.authoritativeCameraName, CameraName, StringComparison.Ordinal) ||
            contract.width != Width || contract.height != Height)
            throw new InvalidOperationException("Camera evidence purity contract scene/camera/native dimensions drifted.");

        if (contract.formalTargetPrefixes == null ||
            !new HashSet<string>(contract.formalTargetPrefixes, StringComparer.Ordinal).SetEquals(FormalTargetPrefixes))
            throw new InvalidOperationException("Camera evidence purity formal render-target prefixes drifted.");

        DisplayTransformSpec display = contract.displayTransform;
        if (display == null ||
            !string.Equals(display.componentType, nameof(QualityBlockFilmicTonemap), StringComparison.Ordinal) ||
            !string.Equals(display.shaderName, FilmicShaderName, StringComparison.Ordinal) ||
            Mathf.Abs(display.exposureEV - ExpectedExposureEV) > FloatTolerance ||
            Mathf.Abs(display.contrast - ExpectedContrast) > FloatTolerance ||
            Mathf.Abs(display.saturation - ExpectedSaturation) > FloatTolerance ||
            Mathf.Abs(display.shadowSoftening - ExpectedShadowSoftening) > FloatTolerance)
            throw new InvalidOperationException("Camera evidence purity display-transform contract drifted.");

        CameraPurityRequirements r = contract.requirements;
        if (r == null ||
            !r.builtInRenderPipelineOnly ||
            !r.perspectiveCameraRequired ||
            !r.forwardRenderingRequired ||
            !r.hdrRequired ||
            !r.msaaPermissionRequired ||
            !r.dynamicResolutionForbidden ||
            !r.skyboxClearRequired ||
            !r.fullViewportRequired ||
            !r.nativeTargetRequired ||
            !r.linearSrgbTargetRequired ||
            !r.singleApprovedImageEffectOnly ||
            !r.cameraCommandBuffersForbidden ||
            !r.validateAtPreCullPreRenderAndPostRender ||
            !r.requireReflectionStateMatchAtPreCullAndPostRender ||
            !r.actualRenderRequiredForVisualPoints ||
            !r.manualPixelReviewRequired)
            throw new InvalidOperationException("Camera evidence purity requirements were weakened or are incomplete.");

        if (contract.runtimeRenderVerified || contract.visualFidelityPointsAwarded != 0)
            throw new InvalidOperationException("Source camera-purity configuration cannot claim runtime verification or award visual points.");
    }

    [MenuItem("NewTown/QA/Validate Camera Evidence Purity In Open Scene")]
    public static void ValidateOpenScene()
    {
        ValidateContractConfigOnly();
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            throw new InvalidOperationException("Camera evidence purity QA requires the already-open persisted benchmark scene: " + ScenePath);

        Camera camera = Camera.main;
        if (camera == null)
            throw new InvalidOperationException("Camera evidence purity QA cannot find MainCamera.");
        ValidateCameraCore(camera, null, false, "source preflight");
    }

    private static void OnCameraPreCull(Camera camera)
    {
        if (!IsFormalEvidenceCamera(camera, out RenderTexture target))
            return;

        ValidateContractConfigOnly();
        ValidateCameraCore(camera, target, true, "formal pre-cull");

        // This is deliberately repeated for every formal render. The async waiter proof is bound to the
        // exact reflection/lighting/renderable state accepted before capture; a later frame may not silently
        // establish a new baseline after a callback changed geometry/material/lighting.
        QualityBlockReflectionProbeAwaiter.ValidateLatestWaitProof();

        int key = target.GetInstanceID();
        if (ActiveFrames.ContainsKey(key))
        {
            ActiveFrames.Remove(key);
            throw new InvalidOperationException("Camera evidence purity observed a nested/stale formal render-target lifecycle: " + target.name);
        }

        ActiveFrames.Add(key, new FrameTrace
        {
            targetName = target.name,
            preCullStateSha256 = BuildCameraStateSha256(camera, target),
            preRenderSeen = false
        });
    }

    private static void OnCameraPreRender(Camera camera)
    {
        if (!IsFormalEvidenceCamera(camera, out RenderTexture target))
            return;

        int key = target.GetInstanceID();
        if (!ActiveFrames.TryGetValue(key, out FrameTrace trace))
            throw new InvalidOperationException("Formal 4K camera reached pre-render without a matching guarded pre-cull: " + target.name);

        ValidateCameraCore(camera, target, true, "formal pre-render");
        string currentSha = BuildCameraStateSha256(camera, target);
        if (!string.Equals(currentSha, trace.preCullStateSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Formal camera/display/target state changed between pre-cull and pre-render for " + target.name +
                ". Evidence capture is rejected rather than accepting mixed camera state.");

        trace.preRenderSeen = true;
        ActiveFrames[key] = trace;
    }

    private static void OnCameraPostRender(Camera camera)
    {
        if (!IsFormalEvidenceCamera(camera, out RenderTexture target))
            return;

        int key = target.GetInstanceID();
        try
        {
            if (!ActiveFrames.TryGetValue(key, out FrameTrace trace) || !trace.preRenderSeen)
                throw new InvalidOperationException("Formal 4K camera reached post-render without a complete pre-cull/pre-render guard lifecycle: " + target.name);

            ValidateCameraCore(camera, target, true, "formal post-render");
            string currentSha = BuildCameraStateSha256(camera, target);
            if (!string.Equals(currentSha, trace.preCullStateSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "Formal camera/display/target state changed during scene rendering for " + target.name +
                    ". Evidence capture is rejected rather than sealing a mixed-state frame.");

            // Detect geometry/material/texture/lighting changes triggered during scene rendering. This is
            // intentionally a proof check rather than a new baseline: the accepted realtime cubemaps must
            // still correspond to the state whose pixels are about to be read back.
            QualityBlockReflectionProbeAwaiter.ValidateLatestWaitProof();
        }
        finally
        {
            ActiveFrames.Remove(key);
        }
    }

    private static bool IsFormalEvidenceCamera(Camera camera, out RenderTexture target)
    {
        target = null;
        if (camera == null || !camera.gameObject.scene.IsValid() || camera.gameObject.scene.path != ScenePath)
            return false;
        if (camera != Camera.main)
            return false;

        target = camera.targetTexture;
        if (target == null || target.width != Width || target.height != Height || string.IsNullOrEmpty(target.name))
            return false;

        return FormalTargetPrefixes.Any(prefix => target.name.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static void ValidateCameraCore(Camera camera, RenderTexture target, bool requireFormalTarget, string phase)
    {
        if (camera == null || camera.gameObject.name != CameraName || camera != Camera.main)
            throw new InvalidOperationException($"{phase}: authoritative camera identity drifted from {CameraName}.");
        if (GraphicsSettings.currentRenderPipeline != null)
            throw new InvalidOperationException($"{phase}: formal benchmark camera requires the Built-in Render Pipeline.");
        if (camera.cameraType != CameraType.Game)
            throw new InvalidOperationException($"{phase}: authoritative benchmark camera must remain CameraType.Game.");
        if (camera.orthographic)
            throw new InvalidOperationException($"{phase}: benchmark evidence must use a perspective camera.");
        if (camera.renderingPath != RenderingPath.Forward || camera.actualRenderingPath != RenderingPath.Forward)
            throw new InvalidOperationException($"{phase}: MainCamera must remain explicit Forward rendering.");
        if (!camera.allowHDR || !camera.allowMSAA || camera.allowDynamicResolution)
            throw new InvalidOperationException($"{phase}: MainCamera must remain HDR + MSAA-permitted with dynamic resolution disabled.");
        if (camera.clearFlags != CameraClearFlags.Skybox)
            throw new InvalidOperationException($"{phase}: MainCamera must clear from the physical skybox.");
        if (!Approximately(camera.rect.x, 0f) || !Approximately(camera.rect.y, 0f) ||
            !Approximately(camera.rect.width, 1f) || !Approximately(camera.rect.height, 1f))
            throw new InvalidOperationException($"{phase}: MainCamera viewport rect must remain full-frame 0,0,1,1.");
        if (!float.IsFinite(camera.nearClipPlane) || !float.IsFinite(camera.farClipPlane) ||
            camera.nearClipPlane < 0.01f || camera.nearClipPlane > 0.5f || camera.farClipPlane < 150f ||
            camera.farClipPlane <= camera.nearClipPlane)
            throw new InvalidOperationException($"{phase}: MainCamera clip planes are incompatible with the benchmark depth range (near={camera.nearClipPlane}, far={camera.farClipPlane}).");

        QualityBlockFilmicTonemap[] tonemaps = camera.GetComponents<QualityBlockFilmicTonemap>();
        if (tonemaps.Length != 1 || tonemaps[0] == null || !tonemaps[0].enabled)
            throw new InvalidOperationException($"{phase}: expected exactly one enabled QualityBlockFilmicTonemap, found {tonemaps.Length}.");
        QualityBlockFilmicTonemap tonemap = tonemaps[0];
        if (tonemap.FilmicShader == null || !tonemap.FilmicShader.isSupported ||
            tonemap.FilmicShader.name != FilmicShaderName ||
            Mathf.Abs(tonemap.ExposureEV - ExpectedExposureEV) > FloatTolerance ||
            Mathf.Abs(tonemap.Contrast - ExpectedContrast) > FloatTolerance ||
            Mathf.Abs(tonemap.Saturation - ExpectedSaturation) > FloatTolerance ||
            Mathf.Abs(tonemap.ShadowSoftening - ExpectedShadowSoftening) > FloatTolerance)
            throw new InvalidOperationException($"{phase}: filmic display-transform identity/parameters drifted.");

        MonoBehaviour[] behaviours = camera.GetComponents<MonoBehaviour>();
        MonoBehaviour[] imageEffects = behaviours
            .Where(x => x != null && x.isActiveAndEnabled && DeclaresOnRenderImage(x.GetType()))
            .ToArray();
        if (imageEffects.Length != 1 || imageEffects[0] != tonemap)
        {
            string names = string.Join(", ", imageEffects.Select(x => x.GetType().FullName));
            throw new InvalidOperationException(
                $"{phase}: formal evidence camera permits only QualityBlockFilmicTonemap as an enabled OnRenderImage effect; found [{names}]. " +
                "Bloom, sharpening, local contrast or other post effects may not hide benchmark defects.");
        }

        int commandBufferCount = 0;
        foreach (CameraEvent evt in Enum.GetValues(typeof(CameraEvent)).Cast<CameraEvent>().Distinct())
            commandBufferCount += camera.GetCommandBuffers(evt).Length;
        if (commandBufferCount != 0)
            throw new InvalidOperationException($"{phase}: MainCamera has {commandBufferCount} command buffer attachment(s); formal evidence forbids hidden camera compositing/injection.");

        if (!requireFormalTarget)
            return;
        if (target == null || !target.IsCreated() || target.width != Width || target.height != Height ||
            target.dimension != TextureDimension.Tex2D || target.volumeDepth != 1 || target.useMipMap || target.autoGenerateMips)
            throw new InvalidOperationException($"{phase}: formal evidence target must be a created native 3840x2160 non-mipmapped 2D RenderTexture.");
        if (target.format != RenderTextureFormat.ARGB32)
            throw new InvalidOperationException($"{phase}: formal display destination must remain ARGB32 LDR; got {target.format}.");
        if (QualitySettings.activeColorSpace != ColorSpace.Linear || !target.sRGB)
            throw new InvalidOperationException($"{phase}: formal ARGB32 evidence target must use sRGB encoding from the Linear-light project.");
        if (target.antiAliasing != 1 && target.antiAliasing != 2 && target.antiAliasing != 4 && target.antiAliasing != 8)
            throw new InvalidOperationException($"{phase}: unexpected formal target MSAA sample count {target.antiAliasing}.");
        if (!Approximately(camera.aspect, Width / (float)Height))
            throw new InvalidOperationException($"{phase}: formal camera aspect must remain exact 16:9 native-frame aspect; got {camera.aspect:R}.");
        if (!float.IsFinite(camera.fieldOfView) || camera.fieldOfView < 30f || camera.fieldOfView > 60f)
            throw new InvalidOperationException($"{phase}: formal perspective FOV {camera.fieldOfView:R} is outside the locked benchmark-safe range 30..60 degrees.");
    }

    private static bool DeclaresOnRenderImage(Type type)
    {
        MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (MethodInfo method in methods)
        {
            if (!string.Equals(method.Name, "OnRenderImage", StringComparison.Ordinal))
                continue;
            ParameterInfo[] p = method.GetParameters();
            if (p.Length == 2 && p[0].ParameterType == typeof(RenderTexture) && p[1].ParameterType == typeof(RenderTexture))
                return true;
        }
        return false;
    }

    private static string BuildCameraStateSha256(Camera camera, RenderTexture target)
    {
        var sb = new StringBuilder(4096);
        Append(sb, "schema", "formal-camera-state-v1");
        Append(sb, "scene", camera.gameObject.scene.path);
        Append(sb, "camera.name", camera.name);
        Append(sb, "camera.instanceId", camera.GetInstanceID());
        AppendVector3(sb, "camera.position", camera.transform.position);
        AppendQuaternion(sb, "camera.rotation", camera.transform.rotation);
        Append(sb, "camera.fieldOfView", camera.fieldOfView);
        Append(sb, "camera.aspect", camera.aspect);
        Append(sb, "camera.nearClip", camera.nearClipPlane);
        Append(sb, "camera.farClip", camera.farClipPlane);
        Append(sb, "camera.orthographic", camera.orthographic);
        Append(sb, "camera.renderingPath", camera.renderingPath.ToString());
        Append(sb, "camera.actualRenderingPath", camera.actualRenderingPath.ToString());
        Append(sb, "camera.allowHDR", camera.allowHDR);
        Append(sb, "camera.allowMSAA", camera.allowMSAA);
        Append(sb, "camera.allowDynamicResolution", camera.allowDynamicResolution);
        Append(sb, "camera.clearFlags", camera.clearFlags.ToString());
        Append(sb, "camera.cullingMask", camera.cullingMask);
        Append(sb, "camera.depthTextureMode", camera.depthTextureMode.ToString());
        Append(sb, "camera.useOcclusionCulling", camera.useOcclusionCulling);
        Append(sb, "camera.layerCullSpherical", camera.layerCullSpherical);
        Append(sb, "camera.rect.x", camera.rect.x);
        Append(sb, "camera.rect.y", camera.rect.y);
        Append(sb, "camera.rect.width", camera.rect.width);
        Append(sb, "camera.rect.height", camera.rect.height);
        Append(sb, "quality.antiAliasing", QualitySettings.antiAliasing);
        Append(sb, "quality.anisotropicFiltering", QualitySettings.anisotropicFiltering.ToString());
        Append(sb, "quality.lodBias", QualitySettings.lodBias);
        Append(sb, "quality.maximumLODLevel", QualitySettings.maximumLODLevel);
        Append(sb, "gl.invertCulling", GL.invertCulling);

        QualityBlockFilmicTonemap tonemap = camera.GetComponent<QualityBlockFilmicTonemap>();
        Append(sb, "filmic.shader", tonemap != null && tonemap.FilmicShader != null ? tonemap.FilmicShader.name : string.Empty);
        Append(sb, "filmic.exposureEV", tonemap != null ? tonemap.ExposureEV : float.NaN);
        Append(sb, "filmic.contrast", tonemap != null ? tonemap.Contrast : float.NaN);
        Append(sb, "filmic.saturation", tonemap != null ? tonemap.Saturation : float.NaN);
        Append(sb, "filmic.shadowSoftening", tonemap != null ? tonemap.ShadowSoftening : float.NaN);

        Append(sb, "target.name", target != null ? target.name : string.Empty);
        Append(sb, "target.instanceId", target != null ? target.GetInstanceID() : 0);
        Append(sb, "target.width", target != null ? target.width : 0);
        Append(sb, "target.height", target != null ? target.height : 0);
        Append(sb, "target.format", target != null ? target.format.ToString() : string.Empty);
        Append(sb, "target.sRGB", target != null && target.sRGB);
        Append(sb, "target.antiAliasing", target != null ? target.antiAliasing : 0);
        Append(sb, "target.depth", target != null ? target.depth : 0);
        Append(sb, "target.dimension", target != null ? target.dimension.ToString() : string.Empty);
        Append(sb, "target.volumeDepth", target != null ? target.volumeDepth : 0);
        Append(sb, "target.useMipMap", target != null && target.useMipMap);
        Append(sb, "target.autoGenerateMips", target != null && target.autoGenerateMips);

        using (SHA256 sha = SHA256.Create())
        {
            byte[] payload = Encoding.UTF8.GetBytes(sb.ToString());
            return BitConverter.ToString(sha.ComputeHash(payload)).Replace("-", string.Empty).ToLowerInvariant();
        }
    }

    private static bool Approximately(float a, float b) => Mathf.Abs(a - b) <= 0.0001f;

    private static void AppendVector3(StringBuilder sb, string key, Vector3 value)
    {
        Append(sb, key + ".x", value.x);
        Append(sb, key + ".y", value.y);
        Append(sb, key + ".z", value.z);
    }

    private static void AppendQuaternion(StringBuilder sb, string key, Quaternion value)
    {
        Append(sb, key + ".x", value.x);
        Append(sb, key + ".y", value.y);
        Append(sb, key + ".z", value.z);
        Append(sb, key + ".w", value.w);
    }

    private static void Append(StringBuilder sb, string key, float value) =>
        Append(sb, key, value.ToString("R", CultureInfo.InvariantCulture));

    private static void Append(StringBuilder sb, string key, int value) =>
        Append(sb, key, value.ToString(CultureInfo.InvariantCulture));

    private static void Append(StringBuilder sb, string key, bool value) =>
        Append(sb, key, value ? "true" : "false");

    private static void Append(StringBuilder sb, string key, string value) =>
        sb.Append(key).Append('=').Append(value ?? string.Empty).Append('\n');

    private sealed class FrameTrace
    {
        public string targetName;
        public string preCullStateSha256;
        public bool preRenderSeen;
    }

    [Serializable]
    private sealed class CameraPurityContract
    {
        public string schemaVersion;
        public string scenePath;
        public string authoritativeCameraName;
        public int width;
        public int height;
        public string[] formalTargetPrefixes;
        public DisplayTransformSpec displayTransform;
        public CameraPurityRequirements requirements;
        public bool runtimeRenderVerified;
        public int visualFidelityPointsAwarded;
    }

    [Serializable]
    private sealed class DisplayTransformSpec
    {
        public string componentType;
        public string shaderName;
        public float exposureEV;
        public float contrast;
        public float saturation;
        public float shadowSoftening;
    }

    [Serializable]
    private sealed class CameraPurityRequirements
    {
        public bool builtInRenderPipelineOnly;
        public bool perspectiveCameraRequired;
        public bool forwardRenderingRequired;
        public bool hdrRequired;
        public bool msaaPermissionRequired;
        public bool dynamicResolutionForbidden;
        public bool skyboxClearRequired;
        public bool fullViewportRequired;
        public bool nativeTargetRequired;
        public bool linearSrgbTargetRequired;
        public bool singleApprovedImageEffectOnly;
        public bool cameraCommandBuffersForbidden;
        public bool validateAtPreCullPreRenderAndPostRender;
        public bool requireReflectionStateMatchAtPreCullAndPostRender;
        public bool actualRenderRequiredForVisualPoints;
        public bool manualPixelReviewRequired;
    }
}
