using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Geometry pass for the Unity benchmark block. The base builder intentionally stays simple;
/// this pass replaces the most visibly synthetic primitives with deterministic authored meshes
/// and adds a few period-appropriate architectural details. Generated assets are kept separate
/// so future hand-authored FBX/GLB replacements can use the same scene object names.
/// </summary>
public static class QualityBlockMeshUpgrade
{
    const string MeshRoot = "Assets/Art/GeneratedMeshes";
    const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";

    [MenuItem("NewTown/Geometry/Build Mesh Library and Rebuild Quality Block")]
    public static void BuildMeshQualityBlock()
    {
        QualityBlockPbrUpgrade.BuildPbrQualityBlock();
        GenerateMeshLibrary();
        ApplyToOpenScene();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("Geometry-upgraded quality block rebuilt. Capture with NewTown > QA > Capture Quality Block PNG.");
    }

    [MenuItem("NewTown/Geometry/Generate Mesh Library Only")]
    public static void GenerateMeshLibrary()
    {
        Directory.CreateDirectory(MeshRoot);
        SaveMesh("GM_TreeTrunk_A", BuildTaperedTrunk(8, 5, 11));
        SaveMesh("GM_TreeTrunk_B", BuildTaperedTrunk(9, 5, 37));
        SaveMesh("GM_FoliageClump_A", BuildFoliageClump(7, 12, 101));
        SaveMesh("GM_FoliageClump_B", BuildFoliageClump(7, 12, 211));
        SaveMesh("GM_FoliageClump_C", BuildFoliageClump(6, 11, 307));
        SaveMesh("GM_SlideChute", BuildSlideChute(1.02f, 4.4f, 0.055f, 0.19f, 0.065f));
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    public static void ApplyToOpenScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() ||
            EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var trunks = LoadMeshes("GM_TreeTrunk_A", "GM_TreeTrunk_B");
        var crowns = LoadMeshes("GM_FoliageClump_A", "GM_FoliageClump_B", "GM_FoliageClump_C");
        var slide = LoadMesh("GM_SlideChute");

        foreach (var mf in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
        {
            string n = mf.gameObject.name;
            if (n.StartsWith("Trunk_", StringComparison.Ordinal))
                mf.sharedMesh = trunks[Mathf.Abs(StableNameHash(n)) % trunks.Length];
            else if (n.StartsWith("Crown_", StringComparison.Ordinal))
                mf.sharedMesh = crowns[Mathf.Abs(StableNameHash(n)) % crowns.Length];
            else if (n == "SlideChute")
            {
                mf.sharedMesh = slide;
                mf.transform.localScale = Vector3.one;
            }
        }

        UpgradeSlide();
        UpgradeDanchiFacade();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    static void UpgradeSlide()
    {
        var park = GameObject.Find("ParkEntrance");
        var chute = GameObject.Find("SlideChute");
        if (park == null || chute == null) return;

        var blue = GameObject.Find("SlideLegL")?.GetComponent<Renderer>()?.sharedMaterial;
        if (blue == null) blue = new Material(Shader.Find("Standard")) { color = new Color(0.19f, 0.48f, 0.62f) };

        // The reference language is a simple steel-pipe neighborhood slide: ladder, rungs and
        // paired handrails rather than a contemporary molded-plastic play structure.
        AddPipeIfMissing("SlideLadderRailL", park.transform,
            new Vector3(12.12f, 0.12f, -5.88f), new Vector3(12.12f, 2.15f, -5.34f), 0.055f, blue);
        AddPipeIfMissing("SlideLadderRailR", park.transform,
            new Vector3(13.08f, 0.12f, -5.88f), new Vector3(13.08f, 2.15f, -5.34f), 0.055f, blue);
        for (int i = 0; i < 6; i++)
        {
            float t = (i + 1) / 7f;
            Vector3 left = Vector3.Lerp(new Vector3(12.12f, 0.12f, -5.88f), new Vector3(12.12f, 2.15f, -5.34f), t);
            Vector3 right = Vector3.Lerp(new Vector3(13.08f, 0.12f, -5.88f), new Vector3(13.08f, 2.15f, -5.34f), t);
            AddPipeIfMissing($"SlideLadderRung_{i}", park.transform, left, right, 0.043f, blue);
        }

        AddPipeIfMissing("SlideHandrailL", park.transform,
            new Vector3(12.02f, 2.08f, -5.02f), new Vector3(12.02f, 2.72f, -4.55f), 0.048f, blue);
        AddPipeIfMissing("SlideHandrailR", park.transform,
            new Vector3(13.18f, 2.08f, -5.02f), new Vector3(13.18f, 2.72f, -4.55f), 0.048f, blue);
    }

    static void UpgradeDanchiFacade()
    {
        var d = GameObject.Find("Danchi");
        if (d == null) return;

        var concrete = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/GeneratedPBR/PBR_DanchiConcrete.mat");
        if (concrete == null) concrete = GameObject.Find("MainBlock")?.GetComponent<Renderer>()?.sharedMaterial;
        if (concrete == null) return;

        // Balcony side partitions break the 30-bay repetition and create the deep shade typical
        // of older slab-block housing without changing the established footprint.
        for (int floor = 0; floor < 5; floor++)
        {
            float y = 1.55f + floor * 2.55f;
            for (int bay = 0; bay < 6; bay++)
            {
                float x = -18.3f + bay * 4.15f;
                AddCubeIfMissing($"BalconyDivider_{floor}_{bay}", d.transform,
                    new Vector3(x - 1.78f, y - 0.12f, -6.78f), new Vector3(0.10f, 1.85f, 1.10f), concrete);
            }
        }

        // Roof parapets and a modest stair entrance canopy improve the silhouette/read at distance.
        AddCubeIfMissing("RoofParapetFront", d.transform,
            new Vector3(-8f, 13.38f, -7.24f), new Vector3(26.1f, 0.42f, 0.18f), concrete);
        AddCubeIfMissing("RoofParapetRear", d.transform,
            new Vector3(-8f, 13.38f, -15.72f), new Vector3(26.1f, 0.42f, 0.18f), concrete);
        AddCubeIfMissing("StairEntranceCanopy", d.transform,
            new Vector3(-8f, 2.52f, -5.05f), new Vector3(2.55f, 0.16f, 1.35f), concrete);

        // Thin floor-edge bands catch sunlight and stop the main slab from reading as one giant cube.
        for (int floor = 1; floor < 5; floor++)
        {
            float y = floor * 2.55f + 0.26f;
            AddCubeIfMissing($"FacadeBand_{floor}", d.transform,
                new Vector3(-8f, y, -7.17f), new Vector3(25.9f, 0.09f, 0.16f), concrete);
        }
    }

    static void AddCubeIfMissing(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        if (GameObject.Find(name) != null) return;
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = position;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
    }

    static void AddPipeIfMissing(string name, Transform parent, Vector3 a, Vector3 b, float radius, Material material)
    {
        if (GameObject.Find(name) != null) return;
        Vector3 delta = b - a;
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = (a + b) * 0.5f;
        go.transform.localScale = new Vector3(radius, delta.magnitude * 0.5f, radius);
        go.transform.rotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
        go.GetComponent<Renderer>().sharedMaterial = material;
    }

    static Mesh BuildTaperedTrunk(int sides, int rings, int seed)
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        var uv = new List<Vector2>();

        for (int r = 0; r < rings; r++)
        {
            float t = r / (float)(rings - 1);
            float y = Mathf.Lerp(-1f, 1f, t);
            float radius = Mathf.Lerp(0.62f, 0.31f, t);
            Vector2 bend = new Vector2(
                Mathf.Sin(seed * 0.31f + t * 4.2f),
                Mathf.Cos(seed * 0.17f + t * 3.7f)) * 0.09f;
            for (int s = 0; s < sides; s++)
            {
                float a = Mathf.PI * 2f * s / sides;
                float irregular = 1f + 0.09f * Mathf.Sin(seed + s * 2.17f + r * 1.73f);
                vertices.Add(new Vector3(bend.x + Mathf.Cos(a) * radius * irregular, y,
                    bend.y + Mathf.Sin(a) * radius * irregular));
                uv.Add(new Vector2(s / (float)sides, t));
            }
        }

        for (int r = 0; r < rings - 1; r++)
        for (int s = 0; s < sides; s++)
        {
            int next = (s + 1) % sides;
            int a = r * sides + s;
            int b = r * sides + next;
            int c = (r + 1) * sides + s;
            int d = (r + 1) * sides + next;
            triangles.Add(a); triangles.Add(c); triangles.Add(b);
            triangles.Add(b); triangles.Add(c); triangles.Add(d);
        }

        var mesh = new Mesh { name = seed % 2 == 0 ? "GM_TreeTrunk_B" : "GM_TreeTrunk_A" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uv);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh BuildFoliageClump(int latitudeSegments, int longitudeSegments, int seed)
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        var uv = new List<Vector2>();

        vertices.Add(new Vector3(0f, 0.52f, 0f));
        uv.Add(new Vector2(0.5f, 1f));

        for (int lat = 1; lat < latitudeSegments; lat++)
        {
            float v = lat / (float)latitudeSegments;
            float phi = Mathf.PI * v;
            for (int lon = 0; lon < longitudeSegments; lon++)
            {
                float u = lon / (float)longitudeSegments;
                float theta = Mathf.PI * 2f * u;
                Vector3 dir = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
                float jitter = 0.84f + 0.20f * Hash01(seed, lat, lon);
                Vector3 squash = new Vector3(0.52f, 0.48f, 0.52f);
                Vector3 offset = new Vector3(
                    (Hash01(seed + 17, lat, lon) - 0.5f) * 0.06f,
                    (Hash01(seed + 29, lon, lat) - 0.5f) * 0.04f,
                    (Hash01(seed + 43, lat + lon, lon) - 0.5f) * 0.06f);
                vertices.Add(Vector3.Scale(dir * jitter, squash) + offset);
                uv.Add(new Vector2(u, 1f - v));
            }
        }

        int bottom = vertices.Count;
        vertices.Add(new Vector3(0f, -0.50f, 0f));
        uv.Add(new Vector2(0.5f, 0f));

        for (int lon = 0; lon < longitudeSegments; lon++)
        {
            int next = (lon + 1) % longitudeSegments;
            triangles.Add(0); triangles.Add(1 + lon); triangles.Add(1 + next);
        }

        for (int lat = 0; lat < latitudeSegments - 2; lat++)
        {
            int row = 1 + lat * longitudeSegments;
            int nextRow = row + longitudeSegments;
            for (int lon = 0; lon < longitudeSegments; lon++)
            {
                int next = (lon + 1) % longitudeSegments;
                int a = row + lon;
                int b = row + next;
                int c = nextRow + lon;
                int d = nextRow + next;
                triangles.Add(a); triangles.Add(c); triangles.Add(b);
                triangles.Add(b); triangles.Add(c); triangles.Add(d);
            }
        }

        int lastRow = 1 + (latitudeSegments - 2) * longitudeSegments;
        for (int lon = 0; lon < longitudeSegments; lon++)
        {
            int next = (lon + 1) % longitudeSegments;
            triangles.Add(lastRow + lon); triangles.Add(bottom); triangles.Add(lastRow + next);
        }

        var mesh = new Mesh { name = "GM_FoliageClump" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uv);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh BuildSlideChute(float width, float length, float bottomThickness, float sideHeight, float sideThickness)
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        var uv = new List<Vector2>();

        AddBox(vertices, triangles, uv, Vector3.zero, new Vector3(width, bottomThickness, length));
        AddBox(vertices, triangles, uv, new Vector3(-width * 0.5f + sideThickness * 0.5f, sideHeight * 0.5f, 0f),
            new Vector3(sideThickness, sideHeight, length));
        AddBox(vertices, triangles, uv, new Vector3(width * 0.5f - sideThickness * 0.5f, sideHeight * 0.5f, 0f),
            new Vector3(sideThickness, sideHeight, length));

        var mesh = new Mesh { name = "GM_SlideChute" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uv);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    static void AddBox(List<Vector3> vertices, List<int> triangles, List<Vector2> uv, Vector3 center, Vector3 size)
    {
        Vector3 h = size * 0.5f;
        int start = vertices.Count;
        Vector3[] p = {
            center + new Vector3(-h.x,-h.y,-h.z), center + new Vector3(h.x,-h.y,-h.z),
            center + new Vector3(h.x,h.y,-h.z), center + new Vector3(-h.x,h.y,-h.z),
            center + new Vector3(-h.x,-h.y,h.z), center + new Vector3(h.x,-h.y,h.z),
            center + new Vector3(h.x,h.y,h.z), center + new Vector3(-h.x,h.y,h.z)
        };
        vertices.AddRange(p);
        for (int i = 0; i < 8; i++) uv.Add(new Vector2((i & 1) == 0 ? 0f : 1f, (i & 2) == 0 ? 0f : 1f));
        int[] t = {
            0,2,1, 0,3,2, 4,5,6, 4,6,7,
            0,1,5, 0,5,4, 2,3,7, 2,7,6,
            1,2,6, 1,6,5, 3,0,4, 3,4,7
        };
        foreach (int i in t) triangles.Add(start + i);
    }

    static void SaveMesh(string name, Mesh mesh)
    {
        mesh.name = name;
        string path = $"{MeshRoot}/{name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
            AssetDatabase.CreateAsset(mesh, path);
        else
        {
            EditorUtility.CopySerialized(mesh, existing);
            UnityEngine.Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
        }
    }

    static Mesh LoadMesh(string name)
    {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshRoot}/{name}.asset");
        if (mesh == null) throw new InvalidOperationException($"Missing generated mesh: {name}");
        return mesh;
    }

    static Mesh[] LoadMeshes(params string[] names) => names.Select(LoadMesh).ToArray();

    static float Hash01(int seed, int a, int b)
    {
        unchecked
        {
            uint x = (uint)(seed * 374761393 + a * 668265263 + b * 2147483647);
            x = (x ^ (x >> 13)) * 1274126177u;
            x ^= x >> 16;
            return (x & 0x00FFFFFF) / 16777215f;
        }
    }

    static int StableNameHash(string value)
    {
        unchecked
        {
            int h = 17;
            for (int i = 0; i < value.Length; i++) h = h * 31 + value[i];
            return h;
        }
    }
}
