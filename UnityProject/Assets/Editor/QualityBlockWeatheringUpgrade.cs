using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Adds deterministic, cause-based weathering and a solar-context lighting pass to the benchmark.
/// Stains are anchored to physical sources (sills, balcony steel, grade and drainage), never to
/// free-form noise. The generated fallback remains replaceable through the existing art slots.
/// </summary>
public static class QualityBlockWeatheringUpgrade
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string WeatheringRoot = "Assets/Art/GeneratedWeathering";

    [MenuItem("NewTown/Environment/Build Physical Weathering + Lighting")]
    public static void BuildWeatheredQualityBlock()
    {
        QualityBlockArtReplacement.BuildReplacementReadyQualityBlock();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        ApplyToOpenScene();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("Applied physical summer sun and source-driven weathering. Render verification is still required in Unity.");
    }

    public static void ApplyToOpenScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() ||
            EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var sceneRoot = GameObject.Find("QualityBlock1990s");
        if (sceneRoot == null)
            throw new InvalidOperationException("QualityBlock1990s root not found.");

        var previous = GameObject.Find("PhysicalEnvironmentContext");
        if (previous != null) UnityEngine.Object.DestroyImmediate(previous);

        var contextGo = new GameObject("PhysicalEnvironmentContext");
        contextGo.transform.SetParent(sceneRoot.transform, false);
        var context = contextGo.AddComponent<QualityBlockEnvironmentContext>();
        // Representative Kanto new-town latitude; Aug 1, 14:00 apparent solar time, dry benchmark.
        context.Configure(35.6f, 213, 14f, false, 0f);

        var sun = GameObject.Find("SummerSun")?.GetComponent<Light>();
        if (sun == null) throw new InvalidOperationException("SummerSun not found.");
        context.ApplyToDirectionalLight(sun);

        // Preserve strong summer contrast without the old flat ambient wash.
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.52f, 0.62f, 0.72f);
        RenderSettings.ambientEquatorColor = new Color(0.42f, 0.43f, 0.41f);
        RenderSettings.ambientGroundColor = new Color(0.23f, 0.22f, 0.19f);
        RenderSettings.ambientIntensity = 0.82f;
        RenderSettings.reflectionIntensity = 0.72f;

        Directory.CreateDirectory(WeatheringRoot);
        Material rainMat = GetOrCreateTransparentMaterial("MAT_RainRunoff",
            new Color(0.12f, 0.145f, 0.13f, 0.13f), 0.06f, 0f);
        Material splashMat = GetOrCreateTransparentMaterial("MAT_GroundSplash",
            new Color(0.17f, 0.125f, 0.075f, 0.18f), 0.04f, 0f);
        Material rustMat = GetOrCreateTransparentMaterial("MAT_RustBleed",
            new Color(0.34f, 0.115f, 0.035f, 0.15f), 0.10f, 0f);

        BuildWeatheringMetadata();
        BuildCauseBasedOverlays(rainMat, splashMat, rustMat);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void BuildWeatheringMetadata()
    {
        ConfigureSurface("MainBlock",
            NewTownSurfaceExposure.SunExposed | NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.GroundContact,
            NewTownStainSource.GroundSplash | NewTownStainSource.UVExposure,
            0.92f, 0.82f, 0.95f, 0f);
        ConfigureSurface("StairTower",
            NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.Recessed,
            NewTownStainSource.RainLedge | NewTownStainSource.RecessGrime,
            0.72f, 0.38f, 0.15f, 0f);
        ConfigureSurface("RainGutter",
            NewTownSurfaceExposure.RainExposed,
            NewTownStainSource.DrainRunoff,
            1f, 0.68f, 0.15f, 0f);
        ConfigureSurface("DanchiPlaza",
            NewTownSurfaceExposure.SunExposed | NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.GroundContact | NewTownSurfaceExposure.UpwardFacing,
            NewTownStainSource.FootTraffic | NewTownStainSource.UVExposure,
            1f, 0.95f, 0.35f, 0f);
        ConfigureSurface("ParkPath",
            NewTownSurfaceExposure.SunExposed | NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.GroundContact | NewTownSurfaceExposure.UpwardFacing,
            NewTownStainSource.FootTraffic | NewTownStainSource.UVExposure,
            1f, 0.92f, 0.32f, 0f);
        ConfigureSurface("GrassField",
            NewTownSurfaceExposure.SunExposed | NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.GroundContact | NewTownSurfaceExposure.UpwardFacing,
            NewTownStainSource.UVExposure,
            1f, 0.95f, 0.25f, 0f);

        var all = Resources.FindObjectsOfTypeAll<GameObject>()
            .Where(x => x.scene.IsValid())
            .ToArray();
        foreach (var go in all)
        {
            string n = go.name;
            if (n.StartsWith("BalconyFloor_", StringComparison.Ordinal))
            {
                ConfigureSurface(go,
                    NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.SunExposed | NewTownSurfaceExposure.UpwardFacing,
                    NewTownStainSource.RainLedge | NewTownStainSource.UVExposure,
                    0.94f, 0.78f, 0.18f, 0.05f);
            }
            else if (n.StartsWith("Window_", StringComparison.Ordinal))
            {
                ConfigureSurface(go,
                    NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.Recessed,
                    NewTownStainSource.RainLedge | NewTownStainSource.RecessGrime,
                    0.52f, 0.42f, 0.02f, 0.18f);
            }
            else if (n.StartsWith("Rail", StringComparison.Ordinal))
            {
                ConfigureSurface(go,
                    NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.SunExposed,
                    NewTownStainSource.FerrousFixture | NewTownStainSource.HumanContact | NewTownStainSource.UVExposure,
                    0.96f, 0.84f, 0.08f, 0.62f);
            }
            else if (n.StartsWith("AC_", StringComparison.Ordinal))
            {
                ConfigureSurface(go,
                    NewTownSurfaceExposure.Sheltered | NewTownSurfaceExposure.Recessed,
                    NewTownStainSource.DrainRunoff | NewTownStainSource.RecessGrime,
                    0.34f, 0.32f, 0.01f, 0.08f);
            }
            else if (n.StartsWith("StairWindow_", StringComparison.Ordinal))
            {
                ConfigureSurface(go,
                    NewTownSurfaceExposure.Sheltered | NewTownSurfaceExposure.Recessed,
                    NewTownStainSource.RecessGrime,
                    0.25f, 0.20f, 0f, 0.12f);
            }
            else if (n.StartsWith("Slide", StringComparison.Ordinal))
            {
                ConfigureSurface(go,
                    NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.SunExposed,
                    NewTownStainSource.FerrousFixture | NewTownStainSource.HumanContact | NewTownStainSource.UVExposure,
                    1f, 0.94f, 0.12f, 0.72f);
            }
            else if (n.StartsWith("Trunk_", StringComparison.Ordinal))
            {
                ConfigureSurface(go,
                    NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.GroundContact,
                    NewTownStainSource.GroundSplash,
                    1f, 0.45f, 0.76f, 0f);
            }
        }
    }

    private static void BuildCauseBasedOverlays(Material rainMat, Material splashMat, Material rustMat)
    {
        var danchi = GameObject.Find("Danchi");
        if (danchi == null) throw new InvalidOperationException("Danchi fallback root not found for weathering.");

        var oldRoot = GameObject.Find("WeatheringOverlays");
        if (oldRoot != null) UnityEngine.Object.DestroyImmediate(oldRoot);

        var root = new GameObject("WeatheringOverlays");
        root.transform.SetParent(danchi.transform, true);

        // Grade splash: soil-laden droplets reach the lowest facade band, strongest within ~0.4 m.
        AddOverlay("Weathering_GroundSplash_MainBlock", root.transform,
            new Vector3(-8f, 0.24f, -7.286f), new Vector3(25.55f, 0.46f, 0.012f), splashMat);

        // Concentrated runoff behind the full-height drain. It is deliberately narrow and source-aligned.
        AddOverlay("Weathering_DrainRunoff_RainGutter", root.transform,
            new Vector3(4.45f, 3.3f, -7.284f), new Vector3(0.30f, 6.35f, 0.010f), rainMat);

        var windows = Resources.FindObjectsOfTypeAll<GameObject>()
            .Where(x => x.scene.IsValid() && x.name.StartsWith("Window_", StringComparison.Ordinal))
            .OrderBy(x => x.name, StringComparer.Ordinal)
            .ToArray();

        foreach (var window in windows)
        {
            int hash = StableNameHash(window.name) & 0x7fffffff;
            float lateral = Mathf.Lerp(-0.62f, 0.62f, (hash % 1000) / 999f);
            float length = Mathf.Lerp(0.34f, 0.82f, ((hash / 31) % 1000) / 999f);
            float width = Mathf.Lerp(0.045f, 0.085f, ((hash / 97) % 1000) / 999f);
            Vector3 p = window.transform.position;
            AddOverlay("Weathering_RainSill_" + window.name, root.transform,
                new Vector3(p.x + lateral, p.y - 0.78f - length * 0.5f, -7.282f),
                new Vector3(width, length, 0.009f), rainMat);
        }

        // Rust bleed is allowed only where a ferrous balcony rail physically meets the slab edge.
        var balconyFloors = Resources.FindObjectsOfTypeAll<GameObject>()
            .Where(x => x.scene.IsValid() && x.name.StartsWith("BalconyFloor_", StringComparison.Ordinal))
            .OrderBy(x => x.name, StringComparer.Ordinal)
            .ToArray();
        foreach (var floor in balconyFloors)
        {
            Vector3 p = floor.transform.position;
            AddOverlay("Weathering_RustRailBase_" + floor.name, root.transform,
                new Vector3(p.x - 1.43f, p.y - 0.055f, -6.194f),
                new Vector3(0.055f, 0.16f, 0.008f), rustMat);
            AddOverlay("Weathering_RustRailBaseR_" + floor.name, root.transform,
                new Vector3(p.x + 1.43f, p.y - 0.055f, -6.194f),
                new Vector3(0.055f, 0.16f, 0.008f), rustMat);
        }
    }

    private static void ConfigureSurface(string objectName, NewTownSurfaceExposure exposure,
        NewTownStainSource sources, float rain, float sun, float splash, float contact)
    {
        var go = GameObject.Find(objectName);
        if (go != null) ConfigureSurface(go, exposure, sources, rain, sun, splash, contact);
    }

    private static void ConfigureSurface(GameObject go, NewTownSurfaceExposure exposure,
        NewTownStainSource sources, float rain, float sun, float splash, float contact)
    {
        var metadata = go.GetComponent<QualityBlockWeatheringSurface>();
        if (metadata == null) metadata = go.AddComponent<QualityBlockWeatheringSurface>();
        metadata.Configure(exposure, sources, rain, sun, splash, contact);
    }

    private static GameObject AddOverlay(string name, Transform parent, Vector3 position,
        Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, true);
        go.transform.position = position;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        var collider = go.GetComponent<Collider>();
        if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
        return go;
    }

    private static Material GetOrCreateTransparentMaterial(string name, Color color,
        float smoothness, float metallic)
    {
        var shader = Shader.Find("Standard");
        if (shader == null) throw new InvalidOperationException("Standard shader not found for weathering overlays.");

        string path = $"{WeatheringRoot}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = shader;
        }

        mat.color = color;
        mat.SetFloat("_Mode", 3f);
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = (int)RenderQueue.Transparent;
        mat.SetFloat("_Glossiness", smoothness);
        mat.SetFloat("_Metallic", metallic);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static int StableNameHash(string value)
    {
        unchecked
        {
            int h = 17;
            for (int i = 0; i < value.Length; i++) h = h * 31 + value[i];
            return h;
        }
    }
}
