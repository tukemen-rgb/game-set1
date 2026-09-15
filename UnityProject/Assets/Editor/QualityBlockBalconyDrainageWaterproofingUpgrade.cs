using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class QualityBlockBalconyDrainageWaterproofingUpgrade
{
    public const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    public const string RootName = "BalconyDrainageWaterproofing";
    public const string GeneratedRoot = "Assets/Art/GeneratedBalconyDrainage";
    public const string WaterproofMeshPath = GeneratedRoot + "/GM_BalconyWaterproofingAssembly.asset";
    public const string FloorFallMeshPath = GeneratedRoot + "/GM_BalconyFloorFall.asset";
    public const string DrainMeshPath = GeneratedRoot + "/GM_BalconyDrainGrate.asset";
    public const string WaterproofMaterialPath = GeneratedRoot + "/MAT_BalconyWaterproofingDry.mat";
    public const string DrainMaterialPath = GeneratedRoot + "/MAT_BalconyDrainCoatedCast.mat";
    public const float ChannelZ = 0.43f;
    public const float DrainOffsetX = 1.45f;
    public const float UpstandHeight = 0.10f;

    // Main walking field reconstructed as a physical fall to the terminal channel. The sloped top starts
    // immediately beyond the rear threshold turn and finishes immediately before the 85 mm channel liner.
    // 1:50 (2%) is a conservative benchmark drainage assumption, not a claim about an original drawing.
    public const float FloorFallRearZ = -0.46f;
    public const float FloorFallFrontZ = 0.38f;
    public const float FloorFallWidth = 3.45f;
    public const float FloorFallRatio = 50f;
    public const float FloorFallBottomY = 0.0005f;
    public const float FloorFallFrontTopY = 0.0015f;
    public const float FloorFallRearTopY = FloorFallFrontTopY + (FloorFallFrontZ - FloorFallRearZ) / FloorFallRatio;

    private static bool saving;

    private struct BoxPart
    {
        public Vector3 size;
        public Vector3 center;
        public float bevel;
        public BoxPart(Vector3 size, Vector3 center, float bevel)
        { this.size = size; this.center = center; this.bevel = bevel; }
    }

    static QualityBlockBalconyDrainageWaterproofingUpgrade()
    {
        EditorSceneManager.sceneSaving -= OnSceneSaving;
        EditorSceneManager.sceneSaving += OnSceneSaving;
    }

    [MenuItem("NewTown/Geometry/Apply Balcony Drainage + Waterproofing")]
    public static void ApplyAndPersist()
    {
        Scene s = EditorSceneManager.GetActiveScene();
        if (!s.IsValid() || s.path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        ApplyToOpenScene();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        QualityBlockBalconyDrainageWaterproofingQA.ValidateOpenScene();
    }

    public static void ApplyToOpenScene()
    {
        QualityBlockBalconyDrainageWaterproofingQA.ValidateContractConfigOnly();
        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
            throw new InvalidOperationException("Benchmark scene must be open.");
        if (IsAuthoredDanchiActive(scene)) return;

        GameObject danchi = FindSceneObject(scene, "Danchi");
        if (danchi == null) throw new InvalidOperationException("Danchi fallback root not found.");
        for (int f = 0; f < 5; f++)
        for (int b = 0; b < 6; b++)
            RequireSlab(scene, f, b);

        Directory.CreateDirectory(GeneratedRoot);
        Mesh waterproofMesh = GetWaterproofMesh();
        Mesh floorFallMesh = GetFloorFallMesh();
        Mesh drainMesh = GetDrainMesh();
        Material waterproof = GetMaterial(WaterproofMaterialPath, "MAT_BalconyWaterproofingDry",
            new Color(0.245f, 0.255f, 0.245f, 1f), 0.28f);
        Material drain = GetMaterial(DrainMaterialPath, "MAT_BalconyDrainCoatedCast",
            new Color(0.20f, 0.22f, 0.20f, 1f), 0.34f);

        GameObject root = FindSceneObject(scene, RootName);
        if (root == null)
        {
            root = new GameObject(RootName);
            root.transform.SetParent(danchi.transform, false);
            for (int f = 0; f < 5; f++)
            for (int b = 0; b < 6; b++)
            {
                Renderer slab = RequireSlab(scene, f, b);
                Bounds sb = slab.bounds;
                GameObject bay = new GameObject($"BalconyDW_{f}_{b}");
                bay.transform.SetParent(root.transform, true);
                bay.transform.position = new Vector3(sb.center.x, sb.max.y, sb.center.z);
                bay.transform.rotation = Quaternion.identity;
                bay.transform.localScale = Vector3.one;
                AddRenderer("WaterproofingAssembly", bay.transform, waterproofMesh, waterproof, Vector3.zero);
                AddRenderer("FloorFallSurface", bay.transform, floorFallMesh, waterproof, Vector3.zero);
                float side = b % 2 == 0 ? -1f : 1f;
                AddRenderer("DrainGrate", bay.transform, drainMesh, drain,
                    new Vector3(side * DrainOffsetX, 0f, ChannelZ));
            }
        }
        else
        {
            // Migration path from the earlier terminal-interface-only assembly. Only the newly introduced
            // physical floor-fall surface is repaired automatically; any unrelated drift remains fail-closed
            // in ValidateOpenScene rather than being silently normalised during a formal evidence lifecycle.
            for (int f = 0; f < 5; f++)
            for (int b = 0; b < 6; b++)
            {
                Transform bay = root.transform.Find($"BalconyDW_{f}_{b}");
                if (bay == null) continue;
                if (bay.Find("FloorFallSurface") == null)
                    AddRenderer("FloorFallSurface", bay, floorFallMesh, waterproof, Vector3.zero);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorSceneManager.MarkSceneDirty(scene);
        QualityBlockBalconyDrainageWaterproofingQA.ValidateOpenScene();
    }

    private static Mesh GetWaterproofMesh()
    {
        Mesh m = AssetDatabase.LoadAssetAtPath<Mesh>(WaterproofMeshPath);
        if (m != null) return m;
        BoxPart[] parts = {
            new BoxPart(new Vector3(0.60f, 0.10f, 0.008f), new Vector3(-1.48f, 0.05f, -0.520f), 0.0015f),
            new BoxPart(new Vector3(0.60f, 0.10f, 0.008f), new Vector3( 1.48f, 0.05f, -0.520f), 0.0015f),
            new BoxPart(new Vector3(2.20f, 0.012f, 0.080f), new Vector3(0f, 0.006f, -0.505f), 0.0020f),
            new BoxPart(new Vector3(3.45f, 0.003f, 0.085f), new Vector3(0f, 0.0015f, ChannelZ), 0.0005f)
        };
        return Combine("GM_BalconyWaterproofingAssembly", WaterproofMeshPath, parts);
    }

    private static Mesh GetFloorFallMesh()
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(FloorFallMeshPath);
        if (existing != null) return existing;

        float half = FloorFallWidth * 0.5f;
        float zr = FloorFallRearZ;
        float zf = FloorFallFrontZ;
        float yb = FloorFallBottomY;
        float yr = FloorFallRearTopY;
        float yf = FloorFallFrontTopY;

        var vertices = new List<Vector3>(24);
        var uvs = new List<Vector2>(24);
        var triangles = new List<int>(36);

        // Each face owns vertices so RecalculateNormals produces construction-hard edges rather than a
        // falsely rounded slab. UVs are metric-like and deterministic even though the current dry material
        // is untextured; future microstructure maps can therefore use manufacture-scale coordinates.
        AddQuad(vertices, uvs, triangles,
            new Vector3(-half, yr, zr), new Vector3(-half, yf, zf),
            new Vector3( half, yf, zf), new Vector3( half, yr, zr),
            new Vector2(0f, 0f), new Vector2(0f, zf-zr), new Vector2(FloorFallWidth, zf-zr), new Vector2(FloorFallWidth, 0f));
        AddQuad(vertices, uvs, triangles,
            new Vector3(-half, yb, zr), new Vector3( half, yb, zr),
            new Vector3( half, yb, zf), new Vector3(-half, yb, zf),
            new Vector2(0f,0f), new Vector2(FloorFallWidth,0f), new Vector2(FloorFallWidth,zf-zr), new Vector2(0f,zf-zr));
        AddQuad(vertices, uvs, triangles,
            new Vector3(-half, yb, zr), new Vector3(-half, yr, zr),
            new Vector3( half, yr, zr), new Vector3( half, yb, zr),
            new Vector2(0f,0f), new Vector2(0f,yr-yb), new Vector2(FloorFallWidth,yr-yb), new Vector2(FloorFallWidth,0f));
        AddQuad(vertices, uvs, triangles,
            new Vector3(-half, yb, zf), new Vector3( half, yb, zf),
            new Vector3( half, yf, zf), new Vector3(-half, yf, zf),
            new Vector2(0f,0f), new Vector2(FloorFallWidth,0f), new Vector2(FloorFallWidth,yf-yb), new Vector2(0f,yf-yb));
        AddQuad(vertices, uvs, triangles,
            new Vector3(-half, yb, zr), new Vector3(-half, yb, zf),
            new Vector3(-half, yf, zf), new Vector3(-half, yr, zr),
            new Vector2(0f,0f), new Vector2(zf-zr,0f), new Vector2(zf-zr,yf-yb), new Vector2(0f,yr-yb));
        AddQuad(vertices, uvs, triangles,
            new Vector3(half, yb, zr), new Vector3(half, yr, zr),
            new Vector3(half, yf, zf), new Vector3(half, yb, zf),
            new Vector2(0f,0f), new Vector2(0f,yr-yb), new Vector2(zf-zr,yf-yb), new Vector2(zf-zr,0f));

        Mesh mesh = new Mesh { name = "GM_BalconyFloorFall" };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0, true);
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        AssetDatabase.CreateAsset(mesh, FloorFallMeshPath);
        return mesh;
    }

    private static void AddQuad(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles,
        Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
    {
        int start = vertices.Count;
        vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
        uvs.Add(ua); uvs.Add(ub); uvs.Add(uc); uvs.Add(ud);
        triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
        triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
    }

    private static Mesh GetDrainMesh()
    {
        Mesh m = AssetDatabase.LoadAssetAtPath<Mesh>(DrainMeshPath);
        if (m != null) return m;
        const float outer = 0.11f, fw = 0.009f, fh = 0.004f;
        float edge = (outer - fw) * 0.5f;
        var parts = new List<BoxPart> {
            new BoxPart(new Vector3(outer, fh, fw), new Vector3(0,0.0035f,-edge),0.0015f),
            new BoxPart(new Vector3(outer, fh, fw), new Vector3(0,0.0035f, edge),0.0015f),
            new BoxPart(new Vector3(fw,fh,outer-fw*2), new Vector3(-edge,0.0035f,0),0.0015f),
            new BoxPart(new Vector3(fw,fh,outer-fw*2), new Vector3( edge,0.0035f,0),0.0015f)
        };
        foreach (float x in new[] {-0.032f,-0.016f,0f,0.016f,0.032f})
            parts.Add(new BoxPart(new Vector3(0.009f,0.003f,0.080f), new Vector3(x,0.0035f,0),0.0012f));
        return Combine("GM_BalconyDrainGrate", DrainMeshPath, parts.ToArray());
    }

    private static Mesh Combine(string name, string path, BoxPart[] parts)
    {
        var ci = new CombineInstance[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            ci[i].mesh = QualityBlockSurfaceMeshLibrary.GetChamferedBox(parts[i].size, parts[i].bevel);
            ci[i].transform = Matrix4x4.TRS(parts[i].center, Quaternion.identity, Vector3.one);
        }
        Mesh m = new Mesh { name = name };
        m.CombineMeshes(ci, true, true, false);
        m.RecalculateBounds(); m.RecalculateNormals(); m.RecalculateTangents();
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    private static Material GetMaterial(string path, string name, Color color, float gloss)
    {
        Shader shader = Shader.Find("Standard");
        if (shader == null) throw new InvalidOperationException("Standard shader not found.");
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader) { name = name }; AssetDatabase.CreateAsset(m, path); }
        m.shader = shader; m.name = name; m.color = color;
        m.SetFloat("_Mode", 0f); m.SetFloat("_Metallic", 0f); m.SetFloat("_Glossiness", gloss);
        if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", 0f);
        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
        m.DisableKeyword("_EMISSION"); m.DisableKeyword("_ALPHATEST_ON");
        m.DisableKeyword("_ALPHABLEND_ON"); m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.SetOverrideTag("RenderType", "Opaque");
        m.SetInt("_SrcBlend", (int)BlendMode.One); m.SetInt("_DstBlend", (int)BlendMode.Zero); m.SetInt("_ZWrite", 1);
        m.renderQueue = -1; EditorUtility.SetDirty(m); return m;
    }

    private static void AddRenderer(string name, Transform parent, Mesh mesh, Material mat, Vector3 localPosition)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false); go.transform.localPosition = localPosition;
        MeshFilter mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = mesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.On; mr.receiveShadows = true;
        mr.lightProbeUsage = LightProbeUsage.BlendProbes;
        mr.reflectionProbeUsage = ReflectionProbeUsage.BlendProbesAndSkybox;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
    }

    private static Renderer RequireSlab(Scene scene, int floor, int bay)
    {
        GameObject go = FindSceneObject(scene, $"BalconyFloor_{floor}_{bay}");
        Renderer r = go != null ? go.GetComponent<Renderer>() : null;
        if (r == null) throw new InvalidOperationException($"BalconyFloor_{floor}_{bay} missing rendered slab.");
        return r;
    }

    private static void OnSceneSaving(Scene scene, string path)
    {
        if (saving || path != ScenePath || !scene.IsValid() || IsAuthoredDanchiActive(scene)) return;
        if (FindSceneObject(scene, "DanchiHighDetail") == null) return;
        saving = true;
        try { ApplyToOpenScene(); QualityBlockBalconyDrainageWaterproofingQA.ValidateOpenScene(); }
        catch (Exception ex) { throw new InvalidOperationException("Benchmark save blocked by balcony drainage/waterproofing QA: " + ex.Message, ex); }
        finally { saving = false; }
    }

    public static bool IsAuthoredDanchiActive(Scene scene) => Resources.FindObjectsOfTypeAll<QualityBlockArtSlot>()
        .Any(x => x != null && x.gameObject.scene == scene && x.SlotId == "danchi.main" && x.IsUsingAuthoredArt);

    public static GameObject FindSceneObject(Scene scene, string name) => Resources.FindObjectsOfTypeAll<GameObject>()
        .FirstOrDefault(x => x.scene == scene && string.Equals(x.name, name, StringComparison.Ordinal));
}
