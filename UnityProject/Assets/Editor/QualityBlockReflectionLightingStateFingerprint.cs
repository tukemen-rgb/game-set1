using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Produces a deterministic SHA-256 fingerprint of the physical lighting state that is allowed to
/// illuminate realtime reflection probes and native-4K benchmark stills. The payload also folds in
/// separately validated renderable-scene/material, global render-policy/texture-residency,
/// renderer/lightmap-GI binding and ReflectionProbe-configuration fingerprints so cubemaps cannot be
/// accepted from one material/geometry/GI/pipeline/quality state and then reused for a still from another.
/// This is evidence-integrity infrastructure only: matching hashes prove configuration coherence, not
/// visual quality.
/// </summary>
public static class QualityBlockReflectionLightingStateFingerprint
{
    public const string Algorithm = "SHA-256";
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";

    /// <summary>
    /// Validates the complete solar/sky/reflection contract first, then hashes the exact scene lighting
    /// state plus independently validated renderable-scene/material, global render policy/texture residency,
    /// renderer/lightmap-GI-binding and reflection-probe-state fingerprints. The hash intentionally includes
    /// ambient SH coefficients because DynamicGI.UpdateEnvironment can change diffuse sky fill after the sky
    /// material was assigned. It also binds rendering policy that can change output without changing any
    /// scene object, such as Built-in graphics-tier/quality settings and effective texture mip residency.
    /// </summary>
    public static string BuildCurrentSha256()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            throw new InvalidOperationException($"Lighting fingerprint requires the persisted benchmark scene: {ScenePath}");

        QualityBlockReflectionLightingCoverageQA.ValidateContractConfigOnly();
        QualityBlockSolarShadowCaptureCoherenceQA.ValidateOpenScene();
        QualityBlockEnvironmentLightingUpgrade.ValidateOpenScene();
        QualityBlockLightEvidencePurityQA.ValidateOpenScene();
        QualityBlockReflectionMaterialPhysicalityBindingQA.ValidateOpenScene();

        Light[] directionals = Resources.FindObjectsOfTypeAll<Light>()
            .Where(x => x.gameObject.scene.IsValid() && x.enabled && x.gameObject.activeInHierarchy && x.type == LightType.Directional)
            .ToArray();
        if (directionals.Length != 1 || directionals[0].name != "SummerSun" || RenderSettings.sun != directionals[0])
            throw new InvalidOperationException("Lighting fingerprint requires exactly one active SummerSun directional light bound to RenderSettings.sun.");

        Light sun = directionals[0];
        Material sky = RenderSettings.skybox;
        if (sky == null || sky.shader == null)
            throw new InvalidOperationException("Lighting fingerprint requires the physical benchmark sky material.");

        var sb = new StringBuilder(8192);
        Append(sb, "schema", "reflection-lighting-state-v6");
        Append(sb, "scene", EditorSceneManager.GetActiveScene().path);
        Append(sb, "unityVersion", Application.unityVersion);

        // A realtime cubemap rendered before a material/texture/renderer/mesh change is invalid evidence
        // even when SummerSun and the sky stay identical. The source validator also rejects arbitrary
        // MaterialPropertyBlock override payloads that cannot yet be deterministically fingerprinted.
        QualityBlockReflectionMaterialOverrideGuard.ValidateOpenScene();
        string renderStateSha256 = QualityBlockReflectionRenderStateFingerprint.BuildValidatedCurrentSha256();
        Append(sb, "renderState.algorithm", QualityBlockReflectionRenderStateFingerprint.Algorithm);
        Append(sb, "renderState.sha256", renderStateSha256);

        // Global render policy is independent of scene objects. A quality-level/tier switch, SRP assignment,
        // global mip limit or camera-dependent texture streaming can alter reflection/still pixels while the
        // renderer/material hash remains unchanged. Bind the exact fail-closed policy to the same asynchronous
        // request/poll/completion/pre-still proof rather than relying on a later MainCamera preflight.
        string renderPolicySha256 = QualityBlockRenderPolicyEvidenceQA.BuildValidatedCurrentSha256();
        Append(sb, "renderPolicy.algorithm", QualityBlockRenderPolicyEvidenceQA.Algorithm);
        Append(sb, "renderPolicy.sha256", renderPolicySha256);

        // Baked/realtime GI assignments are renderer state too, but are not represented by mesh/material
        // identity. Freeze renderer lightmap indices/scale-offsets and the persisted LightmapSettings array.
        string lightmapBindingSha256 = QualityBlockReflectionLightmapBindingFingerprint.BuildCurrentSha256();
        Append(sb, "lightmapBinding.algorithm", QualityBlockReflectionLightmapBindingFingerprint.Algorithm);
        Append(sb, "lightmapBinding.sha256", lightmapBindingSha256);

        // Moving a probe or changing influence/capture/clipping/culling/clear policy changes the evidence.
        string probeStateSha256 = QualityBlockReflectionProbeStateCoherenceQA.BuildValidatedCurrentSha256();
        Append(sb, "probeState.algorithm", QualityBlockReflectionProbeStateCoherenceQA.Algorithm);
        Append(sb, "probeState.sha256", probeStateSha256);

        Append(sb, "sun.name", sun.name);
        Append(sb, "sun.type", sun.type.ToString());
        Append(sb, "sun.enabled", sun.enabled);
        Append(sb, "sun.active", sun.gameObject.activeInHierarchy);
        Append(sb, "sun.renderMode", sun.renderMode.ToString());
        Append(sb, "sun.bounceIntensity", sun.bounceIntensity);
        Quaternion q = sun.transform.rotation;
        Append(sb, "sun.rotation.x", q.x);
        Append(sb, "sun.rotation.y", q.y);
        Append(sb, "sun.rotation.z", q.z);
        Append(sb, "sun.rotation.w", q.w);
        Vector3 forward = sun.transform.forward.normalized;
        Append(sb, "sun.forward.x", forward.x);
        Append(sb, "sun.forward.y", forward.y);
        Append(sb, "sun.forward.z", forward.z);
        Append(sb, "sun.intensity", sun.intensity);
        Append(sb, "sun.useColorTemperature", sun.useColorTemperature);
        Append(sb, "sun.colorTemperature", sun.colorTemperature);
        AppendColor(sb, "sun.color", sun.color);
        Append(sb, "sun.shadows", sun.shadows.ToString());
        Append(sb, "sun.shadowResolution", sun.shadowResolution.ToString());
        Append(sb, "sun.shadowStrength", sun.shadowStrength);
        Append(sb, "sun.shadowBias", sun.shadowBias);
        Append(sb, "sun.shadowNormalBias", sun.shadowNormalBias);
        Append(sb, "sun.shadowNearPlane", sun.shadowNearPlane);
        Append(sb, "sun.cullingMask", sun.cullingMask);

        string skyPath = AssetDatabase.GetAssetPath(sky);
        Append(sb, "sky.assetPath", skyPath ?? string.Empty);
        Append(sb, "sky.assetGuid", string.IsNullOrEmpty(skyPath) ? string.Empty : AssetDatabase.AssetPathToGUID(skyPath));
        Append(sb, "sky.shader", sky.shader.name);
        AppendMaterialFloat(sb, sky, "_SunDisk");
        AppendMaterialFloat(sb, sky, "_SunSize");
        AppendMaterialFloat(sb, sky, "_SunSizeConvergence");
        AppendMaterialFloat(sb, sky, "_AtmosphereThickness");
        AppendMaterialColor(sb, sky, "_SkyTint");
        AppendMaterialColor(sb, sky, "_GroundColor");
        AppendMaterialFloat(sb, sky, "_Exposure");

        Append(sb, "render.ambientMode", RenderSettings.ambientMode.ToString());
        Append(sb, "render.ambientIntensity", RenderSettings.ambientIntensity);
        Append(sb, "render.defaultReflectionMode", RenderSettings.defaultReflectionMode.ToString());
        Append(sb, "render.defaultReflectionResolution", RenderSettings.defaultReflectionResolution);
        Append(sb, "render.reflectionIntensity", RenderSettings.reflectionIntensity);
        Append(sb, "render.reflectionBounces", RenderSettings.reflectionBounces);
        Append(sb, "render.customReflectionAssigned", RenderSettings.customReflection != null);
        AppendColor(sb, "render.subtractiveShadowColor", RenderSettings.subtractiveShadowColor);
        Append(sb, "render.fog", RenderSettings.fog);
        Append(sb, "render.fogMode", RenderSettings.fogMode.ToString());
        AppendColor(sb, "render.fogColor", RenderSettings.fogColor);
        Append(sb, "render.fogStartDistance", RenderSettings.fogStartDistance);
        Append(sb, "render.fogEndDistance", RenderSettings.fogEndDistance);

        Append(sb, "dynamicGI.indirectScale", DynamicGI.indirectScale);

        // Capture the actual sky-derived diffuse lighting, not only settings that requested it.
        SphericalHarmonicsL2 ambient = RenderSettings.ambientProbe;
        for (int rgb = 0; rgb < 3; ++rgb)
            for (int coefficient = 0; coefficient < 9; ++coefficient)
                Append(sb, $"ambientSH.{rgb}.{coefficient}", ambient[rgb, coefficient]);

        // Keep explicit high-impact values in the physical-lighting payload as well as the serialized
        // render-policy proof. This makes diagnostics readable while the policy hash catches remaining
        // project-level state that can change Built-in rendering behavior.
        Append(sb, "quality.realtimeReflectionProbes", QualitySettings.realtimeReflectionProbes);
        Append(sb, "quality.pixelLightCount", QualitySettings.pixelLightCount);
        Append(sb, "quality.shadowmaskMode", QualitySettings.shadowmaskMode.ToString());
        Append(sb, "quality.shadowProjection", QualitySettings.shadowProjection.ToString());
        Append(sb, "quality.shadows", QualitySettings.shadows.ToString());
        Append(sb, "quality.shadowResolution", QualitySettings.shadowResolution.ToString());
        Append(sb, "quality.shadowDistance", QualitySettings.shadowDistance);
        Append(sb, "quality.shadowCascades", QualitySettings.shadowCascades);
        Append(sb, "quality.shadowCascade2Split", QualitySettings.shadowCascade2Split);
        Vector3 split = QualitySettings.shadowCascade4Split;
        Append(sb, "quality.shadowCascade4Split.x", split.x);
        Append(sb, "quality.shadowCascade4Split.y", split.y);
        Append(sb, "quality.shadowCascade4Split.z", split.z);
        Append(sb, "quality.shadowNearPlaneOffset", QualitySettings.shadowNearPlaneOffset);
        Append(sb, "quality.antiAliasing", QualitySettings.antiAliasing);
        Append(sb, "quality.lodBias", QualitySettings.lodBias);
        Append(sb, "quality.maximumLODLevel", QualitySettings.maximumLODLevel);
        Append(sb, "quality.anisotropicFiltering", QualitySettings.anisotropicFiltering.ToString());
        Append(sb, "quality.globalTextureMipmapLimit", QualitySettings.globalTextureMipmapLimit);
        Append(sb, "quality.streamingMipmapsActive", QualitySettings.streamingMipmapsActive);

        byte[] payload = Encoding.UTF8.GetBytes(sb.ToString());
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(payload)).Replace("-", string.Empty).ToLowerInvariant();
    }

    public static void RequireCurrentMatch(string expectedSha256, string phase)
    {
        if (string.IsNullOrWhiteSpace(expectedSha256) || expectedSha256.Length != 64)
            throw new InvalidOperationException($"{phase} lighting fingerprint is missing or malformed.");
        string current = BuildCurrentSha256();
        if (!string.Equals(current, expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Physical lighting, global render policy/texture residency, probe configuration, renderer/lightmap GI binding or renderable scene/material state drifted before {phase}: expected {expectedSha256}, current {current}. Reflection evidence is invalid and capture must abort.");
    }

    private static void AppendMaterialFloat(StringBuilder sb, Material material, string property)
    {
        Append(sb, $"sky.{property}.present", material.HasProperty(property));
        if (material.HasProperty(property))
            Append(sb, $"sky.{property}", material.GetFloat(property));
    }

    private static void AppendMaterialColor(StringBuilder sb, Material material, string property)
    {
        Append(sb, $"sky.{property}.present", material.HasProperty(property));
        if (material.HasProperty(property))
            AppendColor(sb, $"sky.{property}", material.GetColor(property));
    }

    private static void AppendColor(StringBuilder sb, string key, Color value)
    {
        Append(sb, key + ".r", value.r);
        Append(sb, key + ".g", value.g);
        Append(sb, key + ".b", value.b);
        Append(sb, key + ".a", value.a);
    }

    private static void Append(StringBuilder sb, string key, float value)
    {
        Append(sb, key, value.ToString("R", CultureInfo.InvariantCulture));
    }

    private static void Append(StringBuilder sb, string key, int value)
    {
        Append(sb, key, value.ToString(CultureInfo.InvariantCulture));
    }

    private static void Append(StringBuilder sb, string key, bool value)
    {
        Append(sb, key, value ? "true" : "false");
    }

    private static void Append(StringBuilder sb, string key, string value)
    {
        sb.Append(key).Append('=').Append(value ?? string.Empty).Append('\n');
    }
}
