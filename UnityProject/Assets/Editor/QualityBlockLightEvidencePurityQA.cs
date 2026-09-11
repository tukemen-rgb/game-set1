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
/// Fail-closed light/shadow injection guard for authoritative native-4K still and prepared-temporal evidence.
///
/// Camera-side purity is not sufficient in the Built-in Render Pipeline: Light.AddCommandBuffer can inject
/// work around shadow-map and screenspace-shadow events without attaching anything to the evidence camera.
/// This guard therefore audits every scene Light, including disabled lights, and rejects any Light-attached
/// CommandBuffer before a canonical frame can be accepted. It also locks the complete benchmark light/global
/// shadow state from Camera.onPreCull through onPreRender/onPostRender.
///
/// This class is evidence-integrity infrastructure only. It awards zero Visual Fidelity points and cannot
/// clear inconsistent-lighting, light-leak, shimmer or any other critical defect without actual rendered pixels.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockLightEvidencePurityQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/light_evidence_purity_contract.json";
    private const string SunName = "SummerSun";
    private const int Width = 3840;
    private const int Height = 2160;
    private const float EmittingIntensityThreshold = 0.001f;

    private static readonly string[] FormalTargetPrefixes = { "QA4K_", "QAPreparedTemporal_" };
    private static readonly Dictionary<int, FrameTrace> ActiveFrames = new Dictionary<int, FrameTrace>();

    static QualityBlockLightEvidencePurityQA()
    {
        Camera.onPreCull -= OnCameraPreCull;
        Camera.onPreRender -= OnCameraPreRender;
        Camera.onPostRender -= OnCameraPostRender;
        Camera.onPreCull += OnCameraPreCull;
        Camera.onPreRender += OnCameraPreRender;
        Camera.onPostRender += OnCameraPostRender;
    }

    [MenuItem("NewTown/QA/Validate Light Evidence Purity Contract")]
    public static void ValidateContractConfigOnly()
    {
        if (!File.Exists(ContractPath))
            throw new FileNotFoundException("Light evidence purity contract missing: " + ContractPath);

        LightPurityContract contract = JsonUtility.FromJson<LightPurityContract>(File.ReadAllText(ContractPath));
        if (contract == null || !string.Equals(contract.schemaVersion, "1.0", StringComparison.Ordinal))
            throw new InvalidOperationException("Light evidence purity contract is null/unparseable or not schema 1.0.");
        if (!string.Equals(contract.status, "PENDING_REAL_UNITY_4K_RENDER", StringComparison.Ordinal) ||
            !string.Equals(contract.scenePath, ScenePath, StringComparison.Ordinal) ||
            !string.Equals(contract.authoritativeSunName, SunName, StringComparison.Ordinal) ||
            contract.width != Width || contract.height != Height)
            throw new InvalidOperationException("Light evidence purity contract scene/sun/native dimensions/status drifted.");

        if (contract.formalTargetPrefixes == null ||
            !new HashSet<string>(contract.formalTargetPrefixes, StringComparer.Ordinal).SetEquals(FormalTargetPrefixes))
            throw new InvalidOperationException("Light evidence purity formal render-target prefixes drifted.");

        LightPurityRequirements r = contract.requirements;
        if (r == null ||
            !r.builtInRenderPipelineOnly ||
            !r.exactlyOneActiveDirectionalLight ||
            !r.renderSettingsSunMustMatch ||
            !r.activeArtificialLightsForbidden ||
            !r.lightCommandBuffersForbidden ||
            !r.includeDisabledLightsInCommandBufferAudit ||
            !r.validateAtPreCullPreRenderAndPostRender ||
            !r.lightStateMustRemainStableAcrossFrame ||
            !r.actualRenderRequiredForVisualPoints ||
            !r.manualPixelReviewRequired)
            throw new InvalidOperationException("Light evidence purity requirements were weakened or are incomplete.");

        string[] requiredCritical =
        {
            "inconsistent_sun_shadow_direction",
            "major_light_leak",
            "severe_aliasing_or_shimmering",
            "claiming_render_quality_without_actual_render"
        };
        foreach (string id in requiredCritical)
            if (contract.criticalFailMappings == null || !contract.criticalFailMappings.Contains(id))
                throw new InvalidOperationException("Light evidence purity contract missing critical-fail mapping: " + id);

        if (contract.runtimeRenderVerified || contract.visualFidelityPointsAwarded != 0 ||
            !string.Equals(contract.visualFidelityStatus, "UNSCORED_REVIEW_REQUIRED", StringComparison.Ordinal))
            throw new InvalidOperationException("Source light-purity configuration cannot claim runtime verification or award visual points.");
    }

    [MenuItem("NewTown/QA/Validate Light Evidence Purity In Open Scene")]
    public static void ValidateOpenScene()
    {
        ValidateContractConfigOnly();
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            throw new InvalidOperationException("Light evidence purity QA requires the already-open persisted benchmark scene: " + ScenePath);

        ValidateLightCore("source preflight");
    }

    private static void OnCameraPreCull(Camera camera)
    {
        if (!IsFormalEvidenceCamera(camera, out RenderTexture target))
            return;

        ValidateContractConfigOnly();
        ValidateLightCore("formal pre-cull");

        int key = target.GetInstanceID();
        if (ActiveFrames.ContainsKey(key))
        {
            ActiveFrames.Remove(key);
            throw new InvalidOperationException("Light evidence purity observed a nested/stale formal render-target lifecycle: " + target.name);
        }

        ActiveFrames.Add(key, new FrameTrace
        {
            preCullStateSha256 = BuildLightStateSha256(),
            preRenderSeen = false
        });
    }

    private static void OnCameraPreRender(Camera camera)
    {
        if (!IsFormalEvidenceCamera(camera, out RenderTexture target))
            return;

        int key = target.GetInstanceID();
        if (!ActiveFrames.TryGetValue(key, out FrameTrace trace))
            throw new InvalidOperationException("Formal 4K camera reached pre-render without a matching light-purity pre-cull: " + target.name);

        ValidateLightCore("formal pre-render");
        string currentSha = BuildLightStateSha256();
        if (!string.Equals(currentSha, trace.preCullStateSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Formal light/global-shadow state changed between pre-cull and pre-render for " + target.name +
                ". Evidence capture is rejected rather than accepting mixed illumination state.");

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
                throw new InvalidOperationException("Formal 4K camera reached post-render without a complete light-purity lifecycle: " + target.name);

            ValidateLightCore("formal post-render");
            string currentSha = BuildLightStateSha256();
            if (!string.Equals(currentSha, trace.preCullStateSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "Formal light/global-shadow state changed during scene rendering for " + target.name +
                    ". Evidence capture is rejected rather than sealing a mixed-lighting frame.");
        }
        finally
        {
            ActiveFrames.Remove(key);
        }
    }

    private static bool IsFormalEvidenceCamera(Camera camera, out RenderTexture target)
    {
        target = null;
        if (camera == null || !camera.gameObject.scene.IsValid() || camera.gameObject.scene.path != ScenePath || camera != Camera.main)
            return false;

        target = camera.targetTexture;
        if (target == null || target.width != Width || target.height != Height || string.IsNullOrEmpty(target.name))
            return false;

        return FormalTargetPrefixes.Any(prefix => target.name.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static void ValidateLightCore(string phase)
    {
        if (GraphicsSettings.currentRenderPipeline != null)
            throw new InvalidOperationException(phase + ": formal benchmark lighting requires the Built-in Render Pipeline.");

        Light[] allLights = SceneLights();
        if (allLights.Length == 0)
            throw new InvalidOperationException(phase + ": benchmark scene contains no Light components.");

        foreach (Light light in allLights)
        {
            int eventCount = CountLightCommandBuffers(light);
            if (light.commandBufferCount != eventCount)
                throw new InvalidOperationException(
                    $"{phase}: Light command-buffer accounting mismatch on {HierarchyPath(light.transform)}: property={light.commandBufferCount}, per-event={eventCount}.");
            if (eventCount != 0)
                throw new InvalidOperationException(
                    $"{phase}: Light {HierarchyPath(light.transform)} has {eventCount} attached CommandBuffer(s). " +
                    "Formal evidence forbids hidden shadow/light injection on every scene Light, including disabled lights.");
        }

        Light[] activeEmitting = allLights
            .Where(IsActiveEmittingLight)
            .ToArray();
        Light[] activeDirectionals = activeEmitting
            .Where(x => x.type == LightType.Directional)
            .ToArray();
        if (activeDirectionals.Length != 1 || !string.Equals(activeDirectionals[0].name, SunName, StringComparison.Ordinal))
        {
            string found = string.Join(", ", activeDirectionals.Select(x => HierarchyPath(x.transform)));
            throw new InvalidOperationException(
                $"{phase}: expected exactly one active emitting directional light named {SunName}; found {activeDirectionals.Length} [{found}].");
        }

        Light sun = activeDirectionals[0];
        if (RenderSettings.sun != sun)
            throw new InvalidOperationException(phase + ": RenderSettings.sun is not the sole active SummerSun directional light.");

        Light[] artificial = activeEmitting.Where(x => x != sun).ToArray();
        if (artificial.Length != 0)
        {
            string found = string.Join(", ", artificial.Select(x => $"{HierarchyPath(x.transform)}({x.type},I={x.intensity:R})"));
            throw new InvalidOperationException(
                phase + ": daytime benchmark evidence forbids active artificial/non-key lights; found " + found + ".");
        }
    }

    private static Light[] SceneLights()
    {
        return Resources.FindObjectsOfTypeAll<Light>()
            .Where(x => x != null && x.gameObject.scene.IsValid() && x.gameObject.scene.path == ScenePath)
            .OrderBy(x => HierarchyPath(x.transform), StringComparer.Ordinal)
            .ThenBy(x => x.GetInstanceID())
            .ToArray();
    }

    private static bool IsActiveEmittingLight(Light light)
    {
        return light != null && light.enabled && light.gameObject.activeInHierarchy && light.intensity > EmittingIntensityThreshold;
    }

    private static int CountLightCommandBuffers(Light light)
    {
        int count = 0;
        foreach (LightEvent evt in Enum.GetValues(typeof(LightEvent)).Cast<LightEvent>().Distinct())
            count += light.GetCommandBuffers(evt).Length;
        return count;
    }

    private static string BuildLightStateSha256()
    {
        var sb = new StringBuilder(8192);
        Append(sb, "schema", "formal-light-state-v1");
        Append(sb, "scene", EditorSceneManager.GetActiveScene().path);
        Append(sb, "pipeline", GraphicsSettings.currentRenderPipeline == null ? "BuiltIn" : GraphicsSettings.currentRenderPipeline.name);
        Append(sb, "renderSettings.sun", RenderSettings.sun != null ? HierarchyPath(RenderSettings.sun.transform) : string.Empty);
        Append(sb, "renderSettings.ambientMode", RenderSettings.ambientMode.ToString());
        Append(sb, "renderSettings.ambientIntensity", RenderSettings.ambientIntensity);
        Append(sb, "renderSettings.reflectionIntensity", RenderSettings.reflectionIntensity);
        Append(sb, "renderSettings.fog", RenderSettings.fog);
        Append(sb, "renderSettings.fogMode", RenderSettings.fogMode.ToString());
        AppendColor(sb, "renderSettings.fogColor", RenderSettings.fogColor);
        Append(sb, "renderSettings.fogStartDistance", RenderSettings.fogStartDistance);
        Append(sb, "renderSettings.fogEndDistance", RenderSettings.fogEndDistance);

        Append(sb, "quality.shadows", QualitySettings.shadows.ToString());
        Append(sb, "quality.shadowResolution", QualitySettings.shadowResolution.ToString());
        Append(sb, "quality.shadowProjection", QualitySettings.shadowProjection.ToString());
        Append(sb, "quality.shadowDistance", QualitySettings.shadowDistance);
        Append(sb, "quality.shadowCascades", QualitySettings.shadowCascades);
        Append(sb, "quality.shadowCascade2Split", QualitySettings.shadowCascade2Split);
        AppendVector3(sb, "quality.shadowCascade4Split", QualitySettings.shadowCascade4Split);
        Append(sb, "quality.shadowNearPlaneOffset", QualitySettings.shadowNearPlaneOffset);

        Light[] lights = SceneLights();
        Append(sb, "light.count", lights.Length);
        for (int i = 0; i < lights.Length; ++i)
        {
            Light light = lights[i];
            string p = "light[" + i.ToString(CultureInfo.InvariantCulture) + "].";
            Append(sb, p + "path", HierarchyPath(light.transform));
            Append(sb, p + "instanceId", light.GetInstanceID());
            Append(sb, p + "activeInHierarchy", light.gameObject.activeInHierarchy);
            Append(sb, p + "enabled", light.enabled);
            Append(sb, p + "type", light.type.ToString());
            Append(sb, p + "intensity", light.intensity);
            Append(sb, p + "bounceIntensity", light.bounceIntensity);
            AppendColor(sb, p + "color", light.color);
            Append(sb, p + "range", light.range);
            Append(sb, p + "spotAngle", light.spotAngle);
            Append(sb, p + "cullingMask", light.cullingMask);
            Append(sb, p + "shadows", light.shadows.ToString());
            Append(sb, p + "shadowStrength", light.shadowStrength);
            Append(sb, p + "shadowResolution", light.shadowResolution.ToString());
            Append(sb, p + "shadowBias", light.shadowBias);
            Append(sb, p + "shadowNormalBias", light.shadowNormalBias);
            Append(sb, p + "shadowNearPlane", light.shadowNearPlane);
            AppendVector3(sb, p + "position", light.transform.position);
            AppendQuaternion(sb, p + "rotation", light.transform.rotation);
            Append(sb, p + "commandBufferCount", CountLightCommandBuffers(light));

            Texture cookie = light.cookie;
            string cookiePath = cookie != null ? AssetDatabase.GetAssetPath(cookie) : string.Empty;
            Append(sb, p + "cookiePath", cookiePath);
            Append(sb, p + "cookieInstanceId", cookie != null ? cookie.GetInstanceID() : 0);
            if (!string.IsNullOrEmpty(cookiePath))
                Append(sb, p + "cookieDependencyHash", AssetDatabase.GetAssetDependencyHash(cookiePath).ToString());
        }

        using (SHA256 sha = SHA256.Create())
        {
            byte[] payload = Encoding.UTF8.GetBytes(sb.ToString());
            return BitConverter.ToString(sha.ComputeHash(payload)).Replace("-", string.Empty).ToLowerInvariant();
        }
    }

    private static string HierarchyPath(Transform transform)
    {
        if (transform == null)
            return string.Empty;

        var segments = new List<string>();
        Transform cursor = transform;
        while (cursor != null)
        {
            segments.Add(cursor.name + "#" + cursor.GetSiblingIndex().ToString(CultureInfo.InvariantCulture));
            cursor = cursor.parent;
        }
        segments.Reverse();
        return string.Join("/", segments);
    }

    private static void AppendColor(StringBuilder sb, string key, Color value)
    {
        Append(sb, key + ".r", value.r);
        Append(sb, key + ".g", value.g);
        Append(sb, key + ".b", value.b);
        Append(sb, key + ".a", value.a);
    }

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
        public string preCullStateSha256;
        public bool preRenderSeen;
    }

    [Serializable]
    private sealed class LightPurityContract
    {
        public string schemaVersion;
        public string status;
        public string scenePath;
        public string authoritativeSunName;
        public int width;
        public int height;
        public string[] formalTargetPrefixes;
        public LightPurityRequirements requirements;
        public string[] criticalFailMappings;
        public bool runtimeRenderVerified;
        public int visualFidelityPointsAwarded;
        public string visualFidelityStatus;
    }

    [Serializable]
    private sealed class LightPurityRequirements
    {
        public bool builtInRenderPipelineOnly;
        public bool exactlyOneActiveDirectionalLight;
        public bool renderSettingsSunMustMatch;
        public bool activeArtificialLightsForbidden;
        public bool lightCommandBuffersForbidden;
        public bool includeDisabledLightsInCommandBufferAudit;
        public bool validateAtPreCullPreRenderAndPostRender;
        public bool lightStateMustRemainStableAcrossFrame;
        public bool actualRenderRequiredForVisualPoints;
        public bool manualPixelReviewRequired;
    }
}
