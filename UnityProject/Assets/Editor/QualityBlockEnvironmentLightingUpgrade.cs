using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds the outdoor light-transport context required by the benchmark's dielectric glass,
/// painted metal and rough concrete. The pass intentionally does not paint highlights into
/// textures: one coherent midsummer sun drives the procedural sky, ambient probe, fog and
/// local specular reflection probes.
/// </summary>
public static class QualityBlockEnvironmentLightingUpgrade
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string SkyMaterialPath = "Assets/Art/GeneratedPBR/MAT_MidsummerProceduralSky.mat";
    private const string ProbeRootName = "PhysicalReflectionEnvironment";
    private const string FacadeProbeName = "ReflectionProbe_DanchiFacade";
    private const string ParkProbeName = "ReflectionProbe_ParkGround";
    private const int ProbeResolution = 512;

    [MenuItem("NewTown/Lighting/Build Physical Sky + Reflection Environment")]
    public static void BuildAndApply()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        ApplyToOpenScene();
        ValidateOpenScene();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Physical sky/reflection environment built. Actual 4K highlight rolloff, probe balance and glass reflections remain render-verification pending.");
    }

    [MenuItem("NewTown/Lighting/Apply Physical Sky + Reflection Environment Only")]
    public static void ApplyToOpenScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject sceneRoot = FindSceneObject("QualityBlock1990s");
        if (sceneRoot == null)
            throw new InvalidOperationException("QualityBlock1990s root not found for environment-lighting pass.");

        QualityBlockEnvironmentContext context = Resources.FindObjectsOfTypeAll<QualityBlockEnvironmentContext>()
            .FirstOrDefault(x => x.gameObject.scene.IsValid());
        if (context == null)
            throw new InvalidOperationException("PhysicalEnvironmentContext is required before building sky/reflections.");

        Light sun = FindSceneObject("SummerSun")?.GetComponent<Light>();
        if (sun == null || sun.type != LightType.Directional)
            throw new InvalidOperationException("SummerSun directional light is required before building sky/reflections.");

        // Re-apply the same physical solar sample first. The skybox references this exact light,
        // eliminating the common CG failure where visible sky/sun and scene shadows disagree.
        context.ApplyToDirectionalLight(sun);
        RenderSettings.sun = sun;

        Material sky = GetOrCreateProceduralSky();
        RenderSettings.skybox = sky;
        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 0.78f;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        RenderSettings.defaultReflectionResolution = 1024;
        RenderSettings.reflectionIntensity = 0.86f;
        RenderSettings.reflectionBounces = 1;
        RenderSettings.customReflection = null;
        QualitySettings.realtimeReflectionProbes = true;

        // Humid midsummer air: atmosphere is subtle in the hero block and becomes visible mainly
        // toward the far clip. This is actual depth fog, not a blue wash painted onto materials.
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.63f, 0.73f, 0.82f, 1f);
        RenderSettings.fogStartDistance = 72f;
        RenderSettings.fogEndDistance = 148f;

        GameObject oldProbeRoot = FindSceneObject(ProbeRootName);
        if (oldProbeRoot != null)
            UnityEngine.Object.DestroyImmediate(oldProbeRoot);

        var probeRoot = new GameObject(ProbeRootName);
        probeRoot.transform.SetParent(sceneRoot.transform, false);

        ReflectionProbe facade = CreateProbe(
            FacadeProbeName,
            probeRoot.transform,
            new Vector3(-8f, 5.7f, -2.5f),
            new Vector3(34f, 17f, 27f),
            new Vector3(0f, 1.2f, -3.0f),
            100);

        ReflectionProbe park = CreateProbe(
            ParkProbeName,
            probeRoot.transform,
            new Vector3(10f, 4.2f, 2.5f),
            new Vector3(31f, 13f, 30f),
            new Vector3(-1.5f, 0.8f, -2.5f),
            90);

        // Outdoor objects must blend back to the skybox rather than reaching an abrupt probe edge.
        Renderer[] renderers = Resources.FindObjectsOfTypeAll<Renderer>()
            .Where(x => x.gameObject.scene.IsValid())
            .ToArray();
        foreach (Renderer renderer in renderers)
            renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbesAndSkybox;

        Camera camera = Camera.main;
        if (camera != null)
        {
            camera.allowHDR = true;
            camera.allowMSAA = true;
            camera.clearFlags = CameraClearFlags.Skybox;
        }

        // Update ambient SH after changing the sky. Local realtime probes are refreshed explicitly
        // by RefreshRealtimeProbesImmediately() immediately before benchmark capture.
        DynamicGI.UpdateEnvironment();

        EditorUtility.SetDirty(facade);
        EditorUtility.SetDirty(park);
        EditorUtility.SetDirty(sun);
        if (camera != null) EditorUtility.SetDirty(camera);
        EditorUtility.SetDirty(sky);
    }

    /// <summary>
    /// Refreshes all benchmark probes with no time slicing. This should run only after geometry,
    /// materials and facade optics are final for a capture. It is intentionally not an every-frame
    /// gameplay cost.
    /// </summary>
    public static void RefreshRealtimeProbesImmediately()
    {
        GameObject probeRoot = FindSceneObject(ProbeRootName);
        if (probeRoot == null)
            throw new InvalidOperationException("Physical reflection environment root missing before pre-capture refresh.");

        ReflectionProbe[] probes = probeRoot.GetComponentsInChildren<ReflectionProbe>(true);
        if (probes.Length != 2)
            throw new InvalidOperationException($"Expected exactly two physical reflection probes, found {probes.Length}.");

        foreach (ReflectionProbe probe in probes)
        {
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.RenderProbe();
        }
    }

    [MenuItem("NewTown/QA/Validate Physical Sky + Reflection Environment")]
    public static void ValidateOpenScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        QualityBlockEnvironmentContext context = Resources.FindObjectsOfTypeAll<QualityBlockEnvironmentContext>()
            .FirstOrDefault(x => x.gameObject.scene.IsValid());
        if (context == null)
            throw new InvalidOperationException("Physical environment context missing.");

        Light sun = FindSceneObject("SummerSun")?.GetComponent<Light>();
        if (sun == null || RenderSettings.sun != sun)
            throw new InvalidOperationException("Procedural sky must reference the same SummerSun that casts scene shadows.");

        SolarSample solar = context.CalculateSolarSample();
        if (Vector3.Dot(sun.transform.forward.normalized, solar.RayDirection.normalized) < 0.999f)
            throw new InvalidOperationException("SummerSun direction is inconsistent with the stored midsummer solar sample.");

        Material sky = RenderSettings.skybox;
        if (sky == null || sky.shader == null || sky.shader.name != "Skybox/Procedural")
            throw new InvalidOperationException("Benchmark must use the procedural physical sky, not a flat/background-only sky.");
        if (RenderSettings.ambientMode != AmbientMode.Skybox)
            throw new InvalidOperationException("Ambient fill must be derived from the skybox environment.");
        if (RenderSettings.defaultReflectionMode != DefaultReflectionMode.Skybox)
            throw new InvalidOperationException("Default specular reflection must be derived from the skybox.");
        if (RenderSettings.defaultReflectionResolution < 1024)
            throw new InvalidOperationException("Default sky reflection resolution is below the 1024 benchmark minimum.");
        if (RenderSettings.reflectionIntensity < 0.75f || RenderSettings.reflectionIntensity > 1.05f)
            throw new InvalidOperationException("Sky reflection intensity is outside the physically restrained benchmark range.");
        if (!QualitySettings.realtimeReflectionProbes)
            throw new InvalidOperationException("Realtime reflection probes are disabled by QualitySettings.");
        if (!RenderSettings.fog || RenderSettings.fogMode != FogMode.Linear ||
            RenderSettings.fogStartDistance < 60f || RenderSettings.fogEndDistance < 130f)
            throw new InvalidOperationException("Midsummer atmospheric-depth fog contract is not satisfied.");

        GameObject probeRoot = FindSceneObject(ProbeRootName);
        if (probeRoot == null)
            throw new InvalidOperationException("Physical reflection environment root missing.");
        ReflectionProbe[] probes = probeRoot.GetComponentsInChildren<ReflectionProbe>(true);
        if (probes.Length != 2)
            throw new InvalidOperationException($"Expected two local reflection probes, got {probes.Length}.");

        foreach (ReflectionProbe probe in probes)
        {
            if (probe.mode != ReflectionProbeMode.Realtime)
                throw new InvalidOperationException($"{probe.name} must be Realtime for deterministic pre-capture refresh.");
            if (probe.refreshMode != ReflectionProbeRefreshMode.ViaScripting)
                throw new InvalidOperationException($"{probe.name} must refresh ViaScripting, never EveryFrame.");
            if (probe.timeSlicingMode != ReflectionProbeTimeSlicingMode.NoTimeSlicing)
                throw new InvalidOperationException($"{probe.name} must use NoTimeSlicing for coherent benchmark capture.");
            if (!probe.boxProjection || probe.resolution < ProbeResolution)
                throw new InvalidOperationException($"{probe.name} lacks the required box projection or {ProbeResolution}px resolution.");
            if (!probe.hdr)
                throw new InvalidOperationException($"{probe.name} must retain HDR reflection energy.");
        }

        Camera camera = Camera.main;
        if (camera == null || !camera.allowHDR || !camera.allowMSAA || camera.clearFlags != CameraClearFlags.Skybox)
            throw new InvalidOperationException("Main camera must render HDR+MSAA against the physical skybox.");

        Renderer[] renderers = Resources.FindObjectsOfTypeAll<Renderer>()
            .Where(x => x.gameObject.scene.IsValid())
            .ToArray();
        Renderer wrong = renderers.FirstOrDefault(x => x.reflectionProbeUsage != ReflectionProbeUsage.BlendProbesAndSkybox);
        if (wrong != null)
            throw new InvalidOperationException($"Outdoor renderer does not blend local probes with skybox: {wrong.gameObject.name}");

        Debug.Log("Physical sky/reflection environment valid: one coherent solar source, sky-derived fill/specular, two scripted HDR local probes, outdoor probe/sky blending, HDR camera.");
    }

    private static Material GetOrCreateProceduralSky()
    {
        Shader shader = Shader.Find("Skybox/Procedural");
        if (shader == null)
            throw new InvalidOperationException("Built-in Skybox/Procedural shader not found.");

        Material sky = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
        if (sky == null)
        {
            sky = new Material(shader) { name = "MAT_MidsummerProceduralSky" };
            AssetDatabase.CreateAsset(sky, SkyMaterialPath);
        }
        else
        {
            sky.shader = shader;
        }

        SetFloatIfPresent(sky, "_SunDisk", 2f);            // High quality sun disk where supported.
        SetFloatIfPresent(sky, "_SunSize", 0.028f);        // Compact hard-summer source, softened by Light shadows.
        SetFloatIfPresent(sky, "_SunSizeConvergence", 7f);
        SetFloatIfPresent(sky, "_AtmosphereThickness", 1.08f);
        SetColorIfPresent(sky, "_SkyTint", new Color(0.43f, 0.58f, 0.78f, 1f));
        SetColorIfPresent(sky, "_GroundColor", new Color(0.34f, 0.32f, 0.28f, 1f));
        SetFloatIfPresent(sky, "_Exposure", 1.05f);
        return sky;
    }

    private static ReflectionProbe CreateProbe(string name, Transform parent, Vector3 position,
        Vector3 size, Vector3 center, int importance)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, true);
        go.transform.position = position;
        ReflectionProbe probe = go.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
        probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
        probe.size = size;
        probe.center = center;
        probe.blendDistance = 4.5f;
        probe.boxProjection = true;
        probe.resolution = ProbeResolution;
        probe.hdr = true;
        probe.intensity = 1.0f;
        probe.importance = importance;
        probe.nearClipPlane = 0.25f;
        probe.farClipPlane = 95f;
        probe.shadowDistance = 70f;
        return probe;
    }

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x.scene.IsValid() && x.name == name);
    }

    private static void SetFloatIfPresent(Material material, string property, float value)
    {
        if (material.HasProperty(property)) material.SetFloat(property, value);
    }

    private static void SetColorIfPresent(Material material, string property, Color value)
    {
        if (material.HasProperty(property)) material.SetColor(property, value);
    }
}
