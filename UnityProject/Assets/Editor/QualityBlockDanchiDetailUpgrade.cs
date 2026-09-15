using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// High-granularity construction pass for the generated danchi fallback.
/// It does not replace the existing art slot: it enriches the fallback with dimensioned parts
/// so the benchmark reads as an assembled building instead of a textured block.
/// </summary>
public static class QualityBlockDanchiDetailUpgrade
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string DetailMaterialRoot = "Assets/Art/GeneratedDetailMaterials";
    private const string DetailRootName = "DanchiHighDetail";

    [MenuItem("NewTown/Geometry/Build Detailed Weathered Danchi")]
    public static void BuildDetailedWeatheredQualityBlock()
    {
        QualityBlockWeatheringUpgrade.BuildWeatheredQualityBlock();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        ApplyToOpenScene();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("Detailed danchi fallback built. Unity render/compile verification remains required.");
    }

    [MenuItem("NewTown/Geometry/Apply Danchi Detail Pass Only")]
    public static void ApplyToOpenScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() ||
            EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var slot = Resources.FindObjectsOfTypeAll<QualityBlockArtSlot>()
            .FirstOrDefault(x => x.gameObject.scene.IsValid() && x.SlotId == "danchi.main");
        if (slot != null && slot.IsUsingAuthoredArt)
        {
            Debug.Log("Danchi authored replacement is active; generated high-detail fallback was not added.");
            return;
        }

        var danchi = FindSceneObject("Danchi");
        if (danchi == null)
            throw new InvalidOperationException("Danchi fallback root not found.");

        var old = FindSceneObject(DetailRootName);
        if (old != null) UnityEngine.Object.DestroyImmediate(old);

        Directory.CreateDirectory(DetailMaterialRoot);
        Material aluminum = GetOrCreateMaterial("MAT_AgedAluminum", new Color(0.55f, 0.57f, 0.56f), 0.38f, 0.55f);
        Material darkMetal = GetOrCreateMaterial("MAT_DarkGalvanizedSteel", new Color(0.30f, 0.31f, 0.30f), 0.30f, 0.62f);
        Material rubber = GetOrCreateMaterial("MAT_WindowRubber", new Color(0.075f, 0.078f, 0.075f), 0.20f, 0f);
        Material acPlastic = GetOrCreateMaterial("MAT_AgedACPlastic", new Color(0.72f, 0.71f, 0.66f), 0.24f, 0f);
        Material pipeCover = GetOrCreateMaterial("MAT_PipeInsulation", new Color(0.68f, 0.66f, 0.60f), 0.18f, 0f);
        Material drainHose = GetOrCreateMaterial("MAT_DrainHose", new Color(0.36f, 0.36f, 0.33f), 0.12f, 0f);

        var root = new GameObject(DetailRootName);
        root.transform.SetParent(danchi.transform, false);

        int bayCount = 0;
        int acDetailCount = 0;
        int fastenerCount = 0;

        for (int floor = 0; floor < 5; floor++)
        {
            float y = 1.55f + floor * 2.55f;
            for (int bay = 0; bay < 6; bay++)
            {
                float x = -18.3f + bay * 4.15f;
                var bayRoot = new GameObject($"HD_BayAssembly_{floor}_{bay}");
                bayRoot.transform.SetParent(root.transform, false);
                bayRoot.transform.localPosition = new Vector3(x, y, 0f);
                BuildBalconyAssembly(bayRoot.transform, aluminum, darkMetal, ref fastenerCount);
                BuildSlidingWindowAssembly(bayRoot.transform, aluminum, rubber);
                BuildClothesDryingHardware(bayRoot.transform, darkMetal, bay);

                if (FindSceneObject($"AC_{floor}_{bay}") != null)
                {
                    BuildAcAssembly(bayRoot.transform, acPlastic, darkMetal, pipeCover, drainHose, ref fastenerCount);
                    acDetailCount++;
                }

                bayCount++;
            }
        }

        BuildStairWindowFrames(root.transform, aluminum, rubber);
        BuildDownpipeHardware(root.transform, darkMetal, ref fastenerCount);

        var manifest = root.AddComponent<QualityBlockDanchiDetailManifest>();
        manifest.Configure(bayCount, acDetailCount, fastenerCount);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    [MenuItem("NewTown/QA/Validate Detailed Danchi")]
    public static void ValidateOpenScene()
    {
        var root = FindSceneObject(DetailRootName);
        if (root == null) throw new InvalidOperationException("DanchiHighDetail is missing.");

        var manifest = root.GetComponent<QualityBlockDanchiDetailManifest>();
        if (manifest == null) throw new InvalidOperationException("Danchi detail manifest is missing.");
        if (manifest.BayCount != 30)
            throw new InvalidOperationException($"Expected 30 detailed balcony bays, got {manifest.BayCount}.");
        if (manifest.AcDetailCount != 15)
            throw new InvalidOperationException($"Expected 15 detailed AC assemblies from the deterministic fallback pattern, got {manifest.AcDetailCount}.");
        if (manifest.FastenerCount < 450)
            throw new InvalidOperationException($"High-detail fastener count unexpectedly low: {manifest.FastenerCount}.");

        int frames = Resources.FindObjectsOfTypeAll<GameObject>()
            .Count(x => x.scene.IsValid() && x.name.StartsWith("HD_WindowFrame_", StringComparison.Ordinal));
        int railPlates = Resources.FindObjectsOfTypeAll<GameObject>()
            .Count(x => x.scene.IsValid() && x.name.StartsWith("HD_RailBasePlate_", StringComparison.Ordinal));
        if (frames < 120) throw new InvalidOperationException($"Window frame parts unexpectedly low: {frames}.");
        if (railPlates != 210) throw new InvalidOperationException($"Expected 210 rail base plates, got {railPlates}.");

        Debug.Log($"Detailed danchi validation passed structurally: bays={manifest.BayCount}, AC={manifest.AcDetailCount}, fasteners={manifest.FastenerCount}. Runtime render still requires Unity.");
    }

    private static void BuildBalconyAssembly(Transform parent, Material aluminum, Material darkMetal, ref int fastenerCount)
    {
        // Existing balcony slab top is y=-0.79 in bay-local space. The added fascia/lip gives the
        // slab believable thickness at the camera-facing edge rather than a single flat plane.
        var lip = AddBox("HD_BalconySlabLip", parent, new Vector3(0f, -0.79f, -6.19f),
            new Vector3(3.65f, 0.18f, 0.10f), aluminum);
        ConfigureWeathering(lip,
            NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.SunExposed,
            NewTownStainSource.RainLedge | NewTownStainSource.UVExposure, 0.94f, 0.80f, 0.10f, 0f);

        // Seven manufactured vertical rail posts exist in the base block. Add the actual base
        // plates, paired anchor bolts, lower/mid rails and wall brackets around those posts.
        for (int r = -3; r <= 3; r++)
        {
            float px = r * 0.48f;
            var plate = AddBox($"HD_RailBasePlate_{r}", parent, new Vector3(px, -0.875f, -6.18f),
                new Vector3(0.13f, 0.025f, 0.14f), darkMetal);
            ConfigureWeathering(plate,
                NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.SunExposed,
                NewTownStainSource.FerrousFixture | NewTownStainSource.UVExposure,
                0.98f, 0.84f, 0.08f, 0f);

            AddCylinder($"HD_RailBolt_{r}_A", parent, new Vector3(px - 0.035f, -0.852f, -6.18f),
                new Vector3(0.018f, 0.010f, 0.018f), Quaternion.identity, darkMetal);
            AddCylinder($"HD_RailBolt_{r}_B", parent, new Vector3(px + 0.035f, -0.852f, -6.18f),
                new Vector3(0.018f, 0.010f, 0.018f), Quaternion.identity, darkMetal);
            fastenerCount += 2;
        }

        AddBox("HD_RailMid", parent, new Vector3(0f, -0.45f, -6.18f),
            new Vector3(3.55f, 0.045f, 0.045f), aluminum);
        AddBox("HD_RailLower", parent, new Vector3(0f, -0.82f, -6.18f),
            new Vector3(3.55f, 0.040f, 0.040f), aluminum);

        // Divider-to-slab brackets and four visible fasteners on the camera-facing side.
        AddBox("HD_DividerBracketLow", parent, new Vector3(-1.78f, -0.70f, -6.24f),
            new Vector3(0.11f, 0.16f, 0.08f), darkMetal);
        AddBox("HD_DividerBracketHigh", parent, new Vector3(-1.78f, 0.52f, -6.24f),
            new Vector3(0.11f, 0.16f, 0.08f), darkMetal);
        for (int i = 0; i < 4; i++)
        {
            float yy = i < 2 ? -0.70f : 0.52f;
            float xx = -1.78f + (i % 2 == 0 ? -0.028f : 0.028f);
            AddCylinder($"HD_DividerFastener_{i}", parent, new Vector3(xx, yy, -6.195f),
                new Vector3(0.014f, 0.008f, 0.014f), Quaternion.Euler(90f, 0f, 0f), darkMetal);
            fastenerCount++;
        }
    }

    private static void BuildSlidingWindowAssembly(Transform parent, Material aluminum, Material rubber)
    {
        const float z = -7.275f;
        const float w = 2.36f;
        const float h = 1.76f;
        const float frame = 0.065f;
        const float depth = 0.085f;

        var left = AddBox("HD_WindowFrame_Left", parent, new Vector3(-w * 0.5f + frame * 0.5f, 0f, z),
            new Vector3(frame, h, depth), aluminum);
        var right = AddBox("HD_WindowFrame_Right", parent, new Vector3(w * 0.5f - frame * 0.5f, 0f, z),
            new Vector3(frame, h, depth), aluminum);
        var top = AddBox("HD_WindowFrame_Top", parent, new Vector3(0f, h * 0.5f - frame * 0.5f, z),
            new Vector3(w, frame, depth), aluminum);
        var bottom = AddBox("HD_WindowFrame_Bottom", parent, new Vector3(0f, -h * 0.5f + frame * 0.5f, z),
            new Vector3(w, frame, depth), aluminum);

        foreach (var part in new[] { left, right, top, bottom })
            ConfigureWeathering(part,
                NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.Recessed,
                NewTownStainSource.RainLedge | NewTownStainSource.RecessGrime,
                0.52f, 0.38f, 0f, 0.12f);

        // Two overlapping center stiles communicate a real sliding sash instead of a dark rectangle.
        AddBox("HD_WindowSashStile_L", parent, new Vector3(-0.055f, 0f, z - 0.018f),
            new Vector3(0.055f, 1.61f, 0.060f), aluminum);
        AddBox("HD_WindowSashStile_R", parent, new Vector3(0.055f, 0f, z + 0.018f),
            new Vector3(0.055f, 1.61f, 0.060f), aluminum);
        AddBox("HD_WindowTrackTop", parent, new Vector3(0f, 0.73f, z - 0.025f),
            new Vector3(2.18f, 0.032f, 0.040f), rubber);
        AddBox("HD_WindowTrackBottom", parent, new Vector3(0f, -0.73f, z - 0.025f),
            new Vector3(2.18f, 0.032f, 0.040f), rubber);
        AddBox("HD_WindowHandle_L", parent, new Vector3(-0.105f, -0.06f, z + 0.065f),
            new Vector3(0.025f, 0.17f, 0.028f), darkLike(rubber));
        AddBox("HD_WindowHandle_R", parent, new Vector3(0.105f, -0.06f, z + 0.065f),
            new Vector3(0.025f, 0.17f, 0.028f), darkLike(rubber));

        // Projection beyond the facade creates a physical rain drip edge that the weathering pass
        // can legitimately use as a runoff source.
        var sill = AddBox("HD_WindowSillDrip", parent, new Vector3(0f, -0.90f, -7.22f),
            new Vector3(2.46f, 0.055f, 0.20f), aluminum);
        ConfigureWeathering(sill,
            NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.UpwardFacing,
            NewTownStainSource.RainLedge | NewTownStainSource.UVExposure,
            0.90f, 0.62f, 0f, 0f);
    }

    private static void BuildClothesDryingHardware(Transform parent, Material metal, int bay)
    {
        // Period-typical balcony clothes pole receivers. Slight deterministic bay variation only
        // changes the resting angle; the manufactured hardware dimensions remain identical.
        float tilt = (bay % 3 - 1) * 4f;
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * 1.47f;
            AddBox($"HD_ClothesBracket_{side}", parent, new Vector3(x, 0.47f, -6.79f),
                new Vector3(0.07f, 0.34f, 0.08f), metal,
                Quaternion.Euler(tilt, 0f, side * 3f));
            AddCylinder($"HD_ClothesReceiver_{side}", parent, new Vector3(x, 0.60f, -6.72f),
                new Vector3(0.045f, 0.055f, 0.045f), Quaternion.Euler(90f, 0f, 0f), metal);
        }
    }

    private static void BuildAcAssembly(Transform parent, Material plastic, Material metal,
        Material pipeCover, Material drainHose, ref int fastenerCount)
    {
        // Base fallback casing is 0.68 x 0.48 x 0.26 m centered at (1.15,-0.53,-7.12).
        // Add fan/grille, feet, pipe pairs, wall collar and clips as distinct physical components.
        AddCylinder("HD_AC_FanDisc", parent, new Vector3(1.15f, -0.53f, -6.978f),
            new Vector3(0.17f, 0.012f, 0.17f), Quaternion.Euler(90f, 0f, 0f), metal);
        AddCylinder("HD_AC_FanHub", parent, new Vector3(1.15f, -0.53f, -6.962f),
            new Vector3(0.035f, 0.010f, 0.035f), Quaternion.Euler(90f, 0f, 0f), plastic);

        for (int i = -3; i <= 3; i++)
        {
            float o = i * 0.043f;
            AddBox($"HD_AC_GrilleH_{i}", parent, new Vector3(1.15f, -0.53f + o, -6.952f),
                new Vector3(0.38f, 0.010f, 0.010f), metal);
            AddBox($"HD_AC_GrilleV_{i}", parent, new Vector3(1.15f + o, -0.53f, -6.951f),
                new Vector3(0.010f, 0.38f, 0.010f), metal);
        }

        for (int side = -1; side <= 1; side += 2)
        {
            float x = 1.15f + side * 0.22f;
            AddBox($"HD_AC_Foot_{side}", parent, new Vector3(x, -0.785f, -7.12f),
                new Vector3(0.13f, 0.045f, 0.20f), plastic);
            AddBox($"HD_AC_MountPlate_{side}", parent, new Vector3(x, -0.817f, -7.12f),
                new Vector3(0.16f, 0.018f, 0.22f), metal);
            for (int bolt = -1; bolt <= 1; bolt += 2)
            {
                AddCylinder($"HD_AC_MountBolt_{side}_{bolt}", parent,
                    new Vector3(x + bolt * 0.045f, -0.802f, -7.06f),
                    new Vector3(0.014f, 0.008f, 0.014f), Quaternion.identity, metal);
                fastenerCount++;
            }
        }

        Vector3 p0 = new Vector3(1.49f, -0.56f, -7.10f);
        Vector3 p1 = new Vector3(1.54f, -0.56f, -7.26f);
        Vector3 p2 = new Vector3(1.54f, -0.18f, -7.26f);
        AddPipe("HD_AC_Refrigerant_H", parent, p0, p1, 0.020f, pipeCover);
        AddPipe("HD_AC_Refrigerant_V", parent, p1, p2, 0.020f, pipeCover);
        AddPipe("HD_AC_SecondLine_H", parent, p0 + new Vector3(0f, 0.045f, 0f),
            p1 + new Vector3(0f, 0.045f, 0f), 0.014f, pipeCover);
        AddPipe("HD_AC_SecondLine_V", parent, p1 + new Vector3(0f, 0.045f, 0f),
            p2 + new Vector3(0f, 0.045f, 0f), 0.014f, pipeCover);

        AddPipe("HD_AC_DrainHose", parent,
            new Vector3(1.45f, -0.61f, -7.08f), new Vector3(1.42f, -0.78f, -6.88f), 0.012f, drainHose);
        var collar = AddCylinder("HD_AC_WallCollar", parent, new Vector3(1.54f, -0.18f, -7.305f),
            new Vector3(0.060f, 0.018f, 0.060f), Quaternion.Euler(90f, 0f, 0f), pipeCover);
        ConfigureWeathering(collar,
            NewTownSurfaceExposure.Sheltered | NewTownSurfaceExposure.Recessed,
            NewTownStainSource.DrainRunoff | NewTownStainSource.RecessGrime,
            0.32f, 0.18f, 0f, 0f);

        for (int i = 0; i < 3; i++)
        {
            float yy = -0.48f + i * 0.14f;
            AddBox($"HD_AC_PipeClip_{i}", parent, new Vector3(1.54f, yy, -7.235f),
                new Vector3(0.065f, 0.025f, 0.035f), metal);
            fastenerCount++;
        }
    }

    private static void BuildStairWindowFrames(Transform root, Material aluminum, Material rubber)
    {
        for (int floor = 0; floor < 5; floor++)
        {
            float y = 1.55f + floor * 2.55f;
            var assembly = new GameObject($"HD_StairWindowAssembly_{floor}");
            assembly.transform.SetParent(root, false);
            assembly.transform.localPosition = new Vector3(-8f, y, 0f);
            const float z = -5.255f;
            AddBox("HD_StairFrameL", assembly.transform, new Vector3(-0.70f, 0f, z), new Vector3(0.06f, 1.48f, 0.07f), aluminum);
            AddBox("HD_StairFrameR", assembly.transform, new Vector3(0.70f, 0f, z), new Vector3(0.06f, 1.48f, 0.07f), aluminum);
            AddBox("HD_StairFrameTop", assembly.transform, new Vector3(0f, 0.71f, z), new Vector3(1.46f, 0.06f, 0.07f), aluminum);
            AddBox("HD_StairFrameBottom", assembly.transform, new Vector3(0f, -0.71f, z), new Vector3(1.46f, 0.06f, 0.07f), aluminum);
            AddBox("HD_StairFrameMullion", assembly.transform, new Vector3(0f, 0f, z + 0.015f), new Vector3(0.05f, 1.36f, 0.05f), aluminum);
            AddBox("HD_StairSeal", assembly.transform, new Vector3(0f, -0.61f, z + 0.025f), new Vector3(1.32f, 0.02f, 0.025f), rubber);
        }
    }

    private static void BuildDownpipeHardware(Transform root, Material metal, ref int fastenerCount)
    {
        // The fallback downpipe runs full height at x=4.45, z=-6.55. Add stand-off clamps at
        // roughly 1.5 m spacing so it reads as attached plumbing rather than a floating cylinder.
        for (int i = 0; i < 8; i++)
        {
            float y = 0.80f + i * 1.55f;
            AddBox($"HD_DownpipeClamp_{i}", root, new Vector3(4.45f, y, -6.51f),
                new Vector3(0.18f, 0.045f, 0.055f), metal);
            AddCylinder($"HD_DownpipeClampBolt_{i}", root, new Vector3(4.45f, y, -6.475f),
                new Vector3(0.014f, 0.008f, 0.014f), Quaternion.Euler(90f, 0f, 0f), metal);
            fastenerCount++;
        }
    }

    private static GameObject AddBox(string name, Transform parent, Vector3 localPosition,
        Vector3 localScale, Material material, Quaternion? localRotation = null)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = localScale;
        go.transform.localRotation = localRotation ?? Quaternion.identity;
        go.GetComponent<Renderer>().sharedMaterial = material;
        RemoveCollider(go);
        return go;
    }

    private static GameObject AddCylinder(string name, Transform parent, Vector3 localPosition,
        Vector3 localScale, Quaternion localRotation, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = localScale;
        go.transform.localRotation = localRotation;
        go.GetComponent<Renderer>().sharedMaterial = material;
        RemoveCollider(go);
        return go;
    }

    private static GameObject AddPipe(string name, Transform parent, Vector3 localA, Vector3 localB,
        float radius, Material material)
    {
        Vector3 delta = localB - localA;
        var go = AddCylinder(name, parent, (localA + localB) * 0.5f,
            new Vector3(radius, delta.magnitude * 0.5f, radius), Quaternion.identity, material);
        go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
        return go;
    }

    private static void ConfigureWeathering(GameObject go, NewTownSurfaceExposure exposure,
        NewTownStainSource sources, float rain, float sun, float splash, float contact)
    {
        var metadata = go.GetComponent<QualityBlockWeatheringSurface>();
        if (metadata == null) metadata = go.AddComponent<QualityBlockWeatheringSurface>();
        metadata.Configure(exposure, sources, rain, sun, splash, contact);
    }

    private static Material GetOrCreateMaterial(string name, Color color, float smoothness, float metallic)
    {
        var shader = Shader.Find("Standard");
        if (shader == null) throw new InvalidOperationException("Standard shader not found for danchi detail pass.");

        string path = $"{DetailMaterialRoot}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(mat, path);
        }
        else mat.shader = shader;

        mat.color = color;
        mat.SetFloat("_Glossiness", smoothness);
        mat.SetFloat("_Metallic", metallic);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // Keeps the two handles on the same physically dark gasket/hardware palette while remaining
    // an explicit helper for future replacement with a dedicated handle material.
    private static Material darkLike(Material source) => source;

    private static void RemoveCollider(GameObject go)
    {
        var collider = go.GetComponent<Collider>();
        if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
    }

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x.scene.IsValid() && x.name == name);
    }
}

[DisallowMultipleComponent]
public sealed class QualityBlockDanchiDetailManifest : MonoBehaviour
{
    [SerializeField] private int bayCount;
    [SerializeField] private int acDetailCount;
    [SerializeField] private int fastenerCount;

    public int BayCount => bayCount;
    public int AcDetailCount => acDetailCount;
    public int FastenerCount => fastenerCount;

    public void Configure(int bays, int acAssemblies, int fasteners)
    {
        bayCount = bays;
        acDetailCount = acAssemblies;
        fastenerCount = fasteners;
    }
}
