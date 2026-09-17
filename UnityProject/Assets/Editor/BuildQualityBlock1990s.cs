using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class BuildQualityBlock1990s
{
    [MenuItem("NewTown/Build 1990s Quality Block")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("QualityBlock1990s");

        BuildEnvironment(root.transform);
        BuildGround(root.transform);
        BuildDanchi(root.transform);
        BuildPark(root.transform);
        BuildTrees(root.transform);
        BuildStreetFurniture(root.transform);
        BuildCamera(root.transform);

        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/QualityBlock1990s.unity");
        Debug.Log("Saved Assets/Scenes/QualityBlock1990s.unity");
    }

    static Material Mat(string name, Color color, float smoothness = 0.15f, float metallic = 0f)
    {
        var shader = Shader.Find("Standard");
        var m = new Material(shader) { name = name, color = color };
        m.SetFloat("_Glossiness", smoothness);
        m.SetFloat("_Metallic", metallic);
        return m;
    }

    static GameObject Cube(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    static GameObject Cylinder(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    static GameObject Sphere(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    static void BuildEnvironment(Transform root)
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.70f, 0.68f, 0.62f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.72f, 0.82f, 0.91f);
        RenderSettings.fogDensity = 0.003f;

        var sun = new GameObject("SummerSun").AddComponent<Light>();
        sun.transform.SetParent(root);
        sun.type = LightType.Directional;
        sun.intensity = 1.15f;
        sun.color = new Color(1.0f, 0.96f, 0.88f);
        sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
    }

    static void BuildGround(Transform root)
    {
        var g = new GameObject("Ground").transform;
        g.SetParent(root);
        var grass = Mat("GrassBase", new Color(0.28f, 0.39f, 0.15f));
        var paving = Mat("WarmPaving", new Color(0.55f, 0.54f, 0.50f));
        var soil = Mat("DrySoil", new Color(0.43f, 0.34f, 0.22f));

        Cube("GrassField", g, new Vector3(0, -0.08f, 0), new Vector3(54, 0.16f, 36), grass);
        Cube("DanchiPlaza", g, new Vector3(-8, 0.02f, 1), new Vector3(24, 0.06f, 13), paving);
        Cube("ParkPath", g, new Vector3(8.7f, 0.025f, 0), new Vector3(5.2f, 0.07f, 22), paving);
        Cube("WornPathA", g, new Vector3(2.5f, 0.03f, -5.8f), new Vector3(4.4f, 0.03f, 8.5f), soil);
        Cube("WornPathB", g, new Vector3(13f, 0.03f, 7.5f), new Vector3(2f, 0.03f, 6f), soil);
    }

    static void BuildDanchi(Transform root)
    {
        var d = new GameObject("Danchi").transform;
        d.SetParent(root);
        var concrete = Mat("Concrete", new Color(0.68f, 0.67f, 0.63f));
        var concreteDark = Mat("ConcreteDark", new Color(0.48f, 0.48f, 0.45f));
        var rail = Mat("Rail", new Color(0.54f, 0.56f, 0.56f), 0.4f, 0.2f);
        var glass = Mat("GlassDark", new Color(0.18f, 0.24f, 0.28f), 0.6f, 0f);
        var acMat = Mat("AgedAC", new Color(0.76f, 0.75f, 0.70f));
        var futonMat = Mat("FutonBlue", new Color(0.53f, 0.63f, 0.73f));

        Cube("MainBlock", d, new Vector3(-8f, 6.6f, -11.5f), new Vector3(26f, 13.2f, 8.4f), concrete);

        for (int floor = 0; floor < 5; floor++)
        {
            float y = 1.55f + floor * 2.55f;
            for (int bay = 0; bay < 6; bay++)
            {
                float x = -18.3f + bay * 4.15f;
                Cube($"BalconyFloor_{floor}_{bay}", d, new Vector3(x, y - 0.85f, -6.78f), new Vector3(3.65f, 0.12f, 1.15f), concreteDark);
                Cube($"Window_{floor}_{bay}", d, new Vector3(x, y, -7.34f), new Vector3(2.15f, 1.55f, 0.10f), glass);
                Cube($"RailTop_{floor}_{bay}", d, new Vector3(x, y - 0.02f, -6.18f), new Vector3(3.55f, 0.09f, 0.09f), rail);
                for (int r = -3; r <= 3; r++)
                    Cube($"Rail_{floor}_{bay}_{r}", d, new Vector3(x + r * 0.48f, y - 0.46f, -6.18f), new Vector3(0.045f, 0.88f, 0.045f), rail);

                if ((floor + bay) % 2 == 0)
                    Cube($"AC_{floor}_{bay}", d, new Vector3(x + 1.15f, y - 0.53f, -7.12f), new Vector3(0.68f, 0.48f, 0.26f), acMat);

                if ((floor == 2 && bay == 1) || (floor == 3 && bay == 4))
                    Cube($"Futon_{floor}_{bay}", d, new Vector3(x - 0.55f, y - 0.48f, -6.12f), new Vector3(1.15f, 0.82f, 0.035f), futonMat);
            }
        }

        Cube("StairTower", d, new Vector3(-8f, 6.9f, -6.35f), new Vector3(3.2f, 13.8f, 2.0f), concreteDark);
        for (int floor = 0; floor < 5; floor++)
        {
            float y = 1.55f + floor * 2.55f;
            Cube($"StairWindow_{floor}", d, new Vector3(-8f, y, -5.31f), new Vector3(1.28f, 1.35f, 0.08f), glass);
        }
        Cylinder("RainGutter", d, new Vector3(4.45f, 6.55f, -6.55f), new Vector3(0.07f, 6.55f, 0.07f), concreteDark);
    }

    static void BuildPark(Transform root)
    {
        var p = new GameObject("ParkEntrance").transform;
        p.SetParent(root);
        var blue = Mat("AgedBlueSteel", new Color(0.19f, 0.48f, 0.62f), 0.45f, 0.35f);
        var metal = Mat("SlideMetal", new Color(0.54f, 0.62f, 0.64f), 0.65f, 0.5f);
        var wood = Mat("BenchWood", new Color(0.36f, 0.24f, 0.13f));
        var concrete = Mat("BenchConcrete", new Color(0.48f, 0.48f, 0.45f));

        Cylinder("SlideLegL", p, new Vector3(12.0f, 1.05f, -5f), new Vector3(0.055f, 1.05f, 0.055f), blue);
        Cylinder("SlideLegR", p, new Vector3(13.25f, 1.05f, -5f), new Vector3(0.055f, 1.05f, 0.055f), blue);
        Cube("SlidePlatform", p, new Vector3(12.6f, 2.05f, -5.2f), new Vector3(1.9f, 0.09f, 1.5f), blue);
        var chute = Cube("SlideChute", p, new Vector3(12.6f, 1.05f, -2.8f), new Vector3(1f, 0.09f, 4.4f), metal);
        chute.transform.rotation = Quaternion.Euler(-24f, 0f, 0f);

        Cube("BenchSeat", p, new Vector3(6.4f, 0.52f, -1.6f), new Vector3(3.1f, 0.16f, 0.62f), wood);
        Cube("BenchLegL", p, new Vector3(5.3f, 0.26f, -1.6f), new Vector3(0.10f, 0.52f, 0.52f), concrete);
        Cube("BenchLegR", p, new Vector3(7.5f, 0.26f, -1.6f), new Vector3(0.10f, 0.52f, 0.52f), concrete);
    }

    static void BuildTrees(Transform root)
    {
        var t = new GameObject("Trees").transform;
        t.SetParent(root);
        var trunk = Mat("TreeTrunk", new Color(0.28f, 0.19f, 0.12f));
        var leafA = Mat("LeafA", new Color(0.16f, 0.31f, 0.11f));
        var leafB = Mat("LeafB", new Color(0.20f, 0.37f, 0.13f));
        Vector3[] positions = {
            new(-20f,0f,-1.5f), new(-15f,0f,5.5f), new(-3.5f,0f,7.5f),
            new(3.5f,0f,-2f), new(15.5f,0f,2.2f), new(19f,0f,9.5f)
        };

        for (int i = 0; i < positions.Length; i++)
        {
            float h = 5.6f + (i % 3) * 0.9f;
            Cylinder($"Trunk_{i}", t, positions[i] + new Vector3(0, h / 2f, 0), new Vector3(0.22f, h / 2f, 0.22f), trunk);
            var leaf = i % 2 == 0 ? leafA : leafB;
            for (int c = 0; c < 4; c++)
            {
                float ox = (c - 1.5f) * 0.75f;
                float oz = (((c * 7) % 3) - 1) * 0.72f;
                Sphere($"Crown_{i}_{c}", t, positions[i] + new Vector3(ox, h + 0.55f + 0.36f * (c % 2), oz), new Vector3(2.0f, 1.55f, 1.8f), leaf);
            }
        }
    }

    static void BuildStreetFurniture(Transform root)
    {
        var f = new GameObject("StreetFurniture").transform;
        f.SetParent(root);
        var concrete = Mat("StreetConcrete", new Color(0.57f, 0.57f, 0.54f));
        var metal = Mat("StreetMetal", new Color(0.38f, 0.40f, 0.40f), 0.4f, 0.3f);
        var board = Mat("NoticeBoard", new Color(0.31f, 0.26f, 0.18f));

        Cylinder("LampPole", f, new Vector3(4.7f, 2.1f, 5.2f), new Vector3(0.09f, 2.1f, 0.09f), concrete);
        var lampHead = new GameObject("LampHead");
        lampHead.transform.SetParent(f);
        lampHead.transform.position = new Vector3(4.7f, 4.25f, 5.2f);
        var light = lampHead.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 8f;
        light.intensity = 0f;
        light.color = new Color(1f, 0.77f, 0.48f);
        Sphere("LampGlobe", f, lampHead.transform.position, new Vector3(0.26f, 0.26f, 0.26f), metal);

        Cube("NoticeBoardPosts", f, new Vector3(9.3f, 0.9f, 7.2f), new Vector3(0.14f, 1.8f, 0.14f), metal);
        Cube("NoticeBoardPanel", f, new Vector3(9.3f, 1.6f, 7.2f), new Vector3(2.4f, 1.35f, 0.10f), board);
    }

    static void BuildCamera(Transform root)
    {
        var camGo = new GameObject("QualityCamera");
        camGo.transform.SetParent(root);
        camGo.transform.position = new Vector3(2.8f, 2.2f, 18.5f);
        camGo.transform.rotation = Quaternion.Euler(7f, 181f, 0f);
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 48f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 150f;
        camGo.tag = "MainCamera";
    }
}
