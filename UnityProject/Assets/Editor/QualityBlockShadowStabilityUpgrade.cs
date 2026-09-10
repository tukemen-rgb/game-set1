using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Configures the Built-in Render Pipeline for stable, high-density midsummer shadows and
/// thin-geometry edge quality. This pass does not fake contact shadows or soften the image in post;
/// it concentrates real shadow-map precision around the benchmark cameras and keeps polygon edges
/// multisampled. Actual acne, peter-panning, shimmer and edge quality still require rendered evidence.
/// </summary>
public static class QualityBlockShadowStabilityUpgrade
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/shadow_stability_contract.json";

    private const float ShadowDistance = 72f;
    private static readonly Vector3 CascadeSplit = new Vector3(0.10f, 0.25f, 0.50f);
    private const float ShadowBias = 0.025f;
    private const float ShadowNormalBias = 0.38f;
    private const float ShadowNearPlane = 0.10f;
    private const float GlobalShadowNearPlaneOffset = 2.0f;
    private const float LodBias = 2.0f;
    private const int RequestedMsaa = 8;

    [MenuItem("NewTown/Lighting/Build Shadow + Thin Geometry Stability")]
    public static void BuildAndApply()
    {
        EnsureSceneOpen();
        ApplyToOpenScene();
        ValidateOpenScene();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Shadow/edge stability configured. Actual 4K acne, contact separation, foliage shimmer and LOD pop remain render-verification pending.");
    }

    public static void ApplyToOpenScene()
    {
        EnsureSceneOpen();

        if (GraphicsSettings.currentRenderPipeline != null)
            throw new InvalidOperationException("Shadow-stability benchmark is authored for the Built-in Render Pipeline.");

        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
        QualitySettings.shadowProjection = ShadowProjection.StableFit;
        QualitySettings.shadowCascades = 4;
        QualitySettings.shadowCascade4Split = CascadeSplit;
        QualitySettings.shadowDistance = ShadowDistance;
        QualitySettings.shadowNearPlaneOffset = GlobalShadowNearPlaneOffset;
        QualitySettings.antiAliasing = RequestedMsaa;
        QualitySettings.lodBias = LodBias;
        QualitySettings.maximumLODLevel = 0;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
        QualitySettings.realtimeReflectionProbes = true;

        Camera camera = Camera.main;
        if (camera == null)
            throw new InvalidOperationException("MainCamera missing before shadow-stability pass.");
        camera.renderingPath = RenderingPath.Forward;
        camera.allowHDR = true;
        camera.allowMSAA = true;
        camera.allowDynamicResolution = false;

        Light sun = FindSceneObject("SummerSun")?.GetComponent<Light>();
        if (sun == null || sun.type != LightType.Directional)
            throw new InvalidOperationException("SummerSun directional light missing before shadow-stability pass.");
        if (RenderSettings.sun != sun)
            RenderSettings.sun = sun;

        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.92f;
        sun.shadowResolution = LightShadowResolution.VeryHigh;
        sun.shadowBias = ShadowBias;
        sun.shadowNormalBias = ShadowNormalBias;
        sun.shadowNearPlane = ShadowNearPlane;

        // Hero construction must cast and receive the same physical sun shadow. Transparent glazing
        // remains excluded from this blanket enforcement so the facade optics pass can stay physically
        // plausible instead of casting opaque window-card shadows.
        Renderer[] renderers = Resources.FindObjectsOfTypeAll<Renderer>()
            .Where(x => x.gameObject.scene.IsValid())
            .ToArray();
        foreach (Renderer renderer in renderers)
        {
            bool transparentOptics = renderer.gameObject.name.StartsWith("FO_Glass_", StringComparison.Ordinal) ||
                                     renderer.gameObject.name.StartsWith("FO_StairGlass_", StringComparison.Ordinal);
            if (transparentOptics)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                continue;
            }

            if (renderer.enabled)
            {
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
        }

        // Preserve existing physically-authored LOD thresholds, but enforce cross-fade where a group
        // has multiple levels. StableFit shadows plus cross-fade remove two independent sources of
        // camera-motion shimmer without resorting to blur or temporal ghosting.
        LODGroup[] lodGroups = Resources.FindObjectsOfTypeAll<LODGroup>()
            .Where(x => x.gameObject.scene.IsValid())
            .ToArray();
        foreach (LODGroup lodGroup in lodGroups)
        {
            if (lodGroup.lodCount < 2) continue;
            lodGroup.fadeMode = LODFadeMode.CrossFade;
            lodGroup.animateCrossFading = true;
        }

        EditorUtility.SetDirty(camera);
        EditorUtility.SetDirty(sun);
        foreach (Renderer renderer in renderers) EditorUtility.SetDirty(renderer);
        foreach (LODGroup lodGroup in lodGroups) EditorUtility.SetDirty(lodGroup);
    }

    [MenuItem("NewTown/QA/Validate Shadow + Thin Geometry Stability")]
    public static void ValidateOpenScene()
    {
        EnsureSceneOpen();

        if (GraphicsSettings.currentRenderPipeline != null)
            throw new InvalidOperationException("Shadow-stability QA expects Built-in Render Pipeline.");
        if (QualitySettings.shadows != ShadowQuality.All)
            throw new InvalidOperationException("Hard+soft realtime shadows must be enabled.");
        if (QualitySettings.shadowResolution != ShadowResolution.VeryHigh)
            throw new InvalidOperationException("Global shadow resolution must be VeryHigh.");
        if (QualitySettings.shadowProjection != ShadowProjection.StableFit)
            throw new InvalidOperationException("Directional shadows must use StableFit to reduce camera-motion shimmer.");
        if (QualitySettings.shadowCascades != 4)
            throw new InvalidOperationException("Directional sun must use four shadow cascades.");
        if ((QualitySettings.shadowCascade4Split - CascadeSplit).sqrMagnitude > 0.000001f)
            throw new InvalidOperationException("Four-cascade distribution drifted from the benchmark precision contract.");
        if (Mathf.Abs(QualitySettings.shadowDistance - ShadowDistance) > 0.01f)
            throw new InvalidOperationException("Shadow distance drifted from the benchmark/fog handoff distance.");
        if (Mathf.Abs(QualitySettings.shadowNearPlaneOffset - GlobalShadowNearPlaneOffset) > 0.01f)
            throw new InvalidOperationException("Global shadow near-plane offset drifted from the anti-pancaking contract.");
        if (QualitySettings.antiAliasing < 4)
            throw new InvalidOperationException("Realtime edge quality requires at least 4x configured MSAA; benchmark target is 8x.");
        if (QualitySettings.lodBias < 1.9f || QualitySettings.maximumLODLevel != 0)
            throw new InvalidOperationException("LOD quality is too low for the 4K benchmark framing.");

        Camera camera = Camera.main;
        if (camera == null || camera.renderingPath != RenderingPath.Forward || !camera.allowMSAA || !camera.allowHDR)
            throw new InvalidOperationException("MainCamera must use Forward HDR rendering with MSAA allowed.");
        if (camera.allowDynamicResolution)
            throw new InvalidOperationException("Dynamic resolution is forbidden for stable 4K evidence.");

        Light sun = FindSceneObject("SummerSun")?.GetComponent<Light>();
        if (sun == null || RenderSettings.sun != sun || sun.type != LightType.Directional)
            throw new InvalidOperationException("SummerSun must remain the coherent directional shadow source.");

        Light[] activeDirectional = Resources.FindObjectsOfTypeAll<Light>()
            .Where(x => x.gameObject.scene.IsValid() && x.enabled && x.gameObject.activeInHierarchy && x.type == LightType.Directional)
            .ToArray();
        if (activeDirectional.Length != 1 || activeDirectional[0] != sun)
        {
            string names = string.Join(", ", activeDirectional.Select(x => x.name));
            throw new InvalidOperationException(
                $"Critical sun/shadow direction risk: exactly one active directional light is required; found {activeDirectional.Length}: {names}");
        }

        if (sun.shadows != LightShadows.Soft || sun.shadowResolution != LightShadowResolution.VeryHigh)
            throw new InvalidOperationException("SummerSun must use soft VeryHigh-resolution realtime shadows.");
        if (Mathf.Abs(sun.shadowBias - ShadowBias) > 0.001f ||
            Mathf.Abs(sun.shadowNormalBias - ShadowNormalBias) > 0.001f ||
            Mathf.Abs(sun.shadowNearPlane - ShadowNearPlane) > 0.001f)
            throw new InvalidOperationException("SummerSun shadow bias/normal-bias/near-plane drifted from the benchmark contract.");

        Renderer[] glazing = Resources.FindObjectsOfTypeAll<Renderer>()
            .Where(x => x.gameObject.scene.IsValid() &&
                (x.gameObject.name.StartsWith("FO_Glass_", StringComparison.Ordinal) ||
                 x.gameObject.name.StartsWith("FO_StairGlass_", StringComparison.Ordinal)))
            .ToArray();
        Renderer wrongGlass = glazing.FirstOrDefault(x => x.shadowCastingMode != ShadowCastingMode.Off);
        if (wrongGlass != null)
            throw new InvalidOperationException($"Transparent glazing is casting an opaque realtime shadow: {wrongGlass.gameObject.name}");

        LODGroup[] lodGroups = Resources.FindObjectsOfTypeAll<LODGroup>()
            .Where(x => x.gameObject.scene.IsValid() && x.lodCount >= 2)
            .ToArray();
        LODGroup hardPopping = lodGroups.FirstOrDefault(x => x.fadeMode != LODFadeMode.CrossFade || !x.animateCrossFading);
        if (hardPopping != null)
            throw new InvalidOperationException($"LOD group lacks animated cross-fade: {hardPopping.gameObject.name}");

        if (!File.Exists(ContractPath))
            throw new InvalidOperationException("Shadow-stability machine-readable contract is missing.");
        string contract = File.ReadAllText(ContractPath);
        foreach (string token in new[]
        {
            "stableFit",
            "shadowCascade4Split",
            "thinGeometry",
            "severeAliasingOrShimmering",
            "visibleLodPop",
            "actualRenderRequired"
        })
            if (!contract.Contains(token))
                throw new InvalidOperationException($"Shadow-stability contract missing token: {token}");

        // The native-4K packet already calls this validator before its generated-scene rebuild. Binding
        // the new machine contract here ensures missing/relaxed solar-coherence metadata fails early,
        // while QualityBlockSolarShadowPreRenderGuard independently re-validates the rebuilt state at
        // MainCamera pre-cull immediately before actual still/temporal rendering.
        QualityBlockSolarShadowCaptureCoherenceQA.ValidateContractConfigOnly();

        Debug.Log("Shadow/edge implementation QA passed: one active SummerSun, StableFit 4-cascade VeryHigh shadows, constrained bias, forward MSAA, high LOD bias and cross-fading. Render review remains mandatory.");
    }

    private static void EnsureSceneOpen()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x.name == name && x.scene.IsValid());
    }
}
