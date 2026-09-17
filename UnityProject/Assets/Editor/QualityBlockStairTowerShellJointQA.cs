using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Corrective joint pass for the generated stair-tower shell. The first aperture reconstruction owns
/// the opening/frame/optical installation; this pass makes the outer RC return pieces butt rather than
/// occupy the same coplanar exterior faces. That prevents self-inflicted z-fighting on the oblique/grazing
/// benchmark while preserving the exact tower envelope and front-opening geometry.
/// </summary>
public static class QualityBlockStairTowerShellJointQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string RootName = "StairTowerApertureShell";
    private const string MeshRoot = "Assets/Art/GeneratedStairTowerApertureMeshes";
    private const float XMin = -9.6f;
    private const float XMax = -6.4f;
    private const float YMin = 0.0f;
    private const float YMax = 13.8f;
    private const float ZFront = -5.35f;
    private const float ZBack = -7.35f;
    private const float FrontDepth = 0.22f;
    private const float FrontBackZ = ZFront - FrontDepth;
    private const float PerimeterThickness = 0.18f;
    private const float MacroTileMeters = 2.4f;
    private const float TouchTolerance = 0.0008f;

    [MenuItem("NewTown/Geometry/Resolve Stair Tower RC Shell Joints")]
    public static void ApplyAndPersist()
    {
        EnsureScene();
        GameObject root = FindSceneObject(RootName);
        if (root == null)
            throw new InvalidOperationException("StairTowerApertureShell missing; reconstruct stair-tower apertures first.");

        Directory.CreateDirectory(AbsolutePath(MeshRoot));
        float width = XMax - XMin;
        float height = YMax - YMin;
        float sideDepth = FrontBackZ - ZBack;
        float sideCenterZ = (ZBack + FrontBackZ) * 0.5f;
        float innerWidth = width - PerimeterThickness * 2f;
        float innerZMin = ZBack + PerimeterThickness;
        float innerDepth = FrontBackZ - innerZMin;
        float innerCenterZ = (innerZMin + FrontBackZ) * 0.5f;
        float cy = (YMin + YMax) * 0.5f;

        for (int lod = 0; lod < 4; lod++)
        {
            Transform tier = root.transform.Find($"LOD{lod}");
            if (tier == null) throw new InvalidOperationException($"{RootName}/LOD{lod} missing.");

            ReplacePart(tier, $"ST_RC_LOD{lod}_Back",
                new Vector3(-8.0f, cy, ZBack + PerimeterThickness * 0.5f),
                new Vector3(innerWidth, height, PerimeterThickness), lod);
            ReplacePart(tier, $"ST_RC_LOD{lod}_LeftEnd",
                new Vector3(XMin + PerimeterThickness * 0.5f, cy, sideCenterZ),
                new Vector3(PerimeterThickness, height, sideDepth), lod);
            ReplacePart(tier, $"ST_RC_LOD{lod}_RightEnd",
                new Vector3(XMax - PerimeterThickness * 0.5f, cy, sideCenterZ),
                new Vector3(PerimeterThickness, height, sideDepth), lod);
            ReplacePart(tier, $"ST_RC_LOD{lod}_Roof",
                new Vector3(-8.0f, YMax - PerimeterThickness * 0.5f, innerCenterZ),
                new Vector3(innerWidth, PerimeterThickness, innerDepth), lod);
            ReplacePart(tier, $"ST_RC_LOD{lod}_Base",
                new Vector3(-8.0f, YMin + PerimeterThickness * 0.5f, innerCenterZ),
                new Vector3(innerWidth, PerimeterThickness, innerDepth), lod);
        }

        LODGroup group = root.GetComponent<LODGroup>();
        if (group != null) group.RecalculateBounds();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ValidateOpenScene();
        Debug.Log("Stair tower RC outer-shell joints converted from overlapping coplanar faces to butt joints. Render verification remains pending.");
    }

    [MenuItem("NewTown/QA/Validate Stair Tower RC Shell Joints")]
    public static void ValidateOpenScene()
    {
        EnsureScene();
        GameObject root = FindSceneObject(RootName);
        if (root == null) throw new InvalidOperationException("StairTowerApertureShell missing.");

        for (int lod = 0; lod < 4; lod++)
        {
            Transform tier = root.transform.Find($"LOD{lod}");
            Renderer back = RequireRenderer(tier, $"ST_RC_LOD{lod}_Back");
            Renderer left = RequireRenderer(tier, $"ST_RC_LOD{lod}_LeftEnd");
            Renderer right = RequireRenderer(tier, $"ST_RC_LOD{lod}_RightEnd");
            Renderer roof = RequireRenderer(tier, $"ST_RC_LOD{lod}_Roof");
            Renderer basePart = RequireRenderer(tier, $"ST_RC_LOD{lod}_Base");
            Renderer frontLeft = RequireRenderer(tier, $"ST_RC_LOD{lod}_FrontLeftPier");
            Renderer frontRight = RequireRenderer(tier, $"ST_RC_LOD{lod}_FrontRightPier");

            AssertTouchNoPositiveOverlap(left.bounds.max.z, frontLeft.bounds.min.z, $"LOD{lod} left side/front");
            AssertTouchNoPositiveOverlap(right.bounds.max.z, frontRight.bounds.min.z, $"LOD{lod} right side/front");
            AssertTouchNoPositiveOverlap(left.bounds.max.x, back.bounds.min.x, $"LOD{lod} left side/back", reverse: true);
            AssertTouchNoPositiveOverlap(back.bounds.max.x, right.bounds.min.x, $"LOD{lod} back/right side");

            if (PositiveOverlapVolume(left.bounds, roof.bounds) > TouchTolerance ||
                PositiveOverlapVolume(right.bounds, roof.bounds) > TouchTolerance ||
                PositiveOverlapVolume(left.bounds, basePart.bounds) > TouchTolerance ||
                PositiveOverlapVolume(right.bounds, basePart.bounds) > TouchTolerance ||
                PositiveOverlapVolume(back.bounds, roof.bounds) > TouchTolerance ||
                PositiveOverlapVolume(back.bounds, basePart.bounds) > TouchTolerance)
                throw new InvalidOperationException($"LOD{lod} outer stair-tower RC shell still contains positive-volume joint overlap.");

            foreach (Renderer r in new[] { back, left, right, roof, basePart })
            {
                Mesh mesh = r.GetComponent<MeshFilter>()?.sharedMesh;
                string path = mesh != null ? AssetDatabase.GetAssetPath(mesh) : string.Empty;
                if (mesh == null || string.IsNullOrWhiteSpace(path) || !path.StartsWith(MeshRoot + "/", StringComparison.Ordinal))
                    throw new InvalidOperationException($"Joint-corrected stair shell part lacks persisted custom mesh: {r.gameObject.name}");
                if (mesh.name == "Cube" || mesh.name == "Cylinder" || mesh.name == "Sphere" || mesh.name == "Capsule")
                    throw new InvalidOperationException($"Joint-corrected stair shell part reverted to a stock primitive: {r.gameObject.name}");
            }
        }

        Debug.Log("Stair tower RC shell-joint QA passed: outer return pieces butt without positive-volume/coplanar exterior overlap. Visual Fidelity remains UNSCORED.");
    }

    private static void ReplacePart(Transform tier, string name, Vector3 center, Vector3 size, int lod)
    {
        Transform part = tier.Find(name);
        if (part == null) throw new InvalidOperationException($"Stair shell part missing: {tier.name}/{name}");
        MeshFilter filter = part.GetComponent<MeshFilter>();
        if (filter == null) throw new InvalidOperationException($"Stair shell part has no MeshFilter: {name}");
        part.localPosition = center;
        part.localScale = Vector3.one;
        filter.sharedMesh = GetOrCreateBoxMesh($"{name}_JointSafe", size, center);
        EditorUtility.SetDirty(part);
        EditorUtility.SetDirty(filter);
    }

    private static Mesh GetOrCreateBoxMesh(string name, Vector3 size, Vector3 center)
    {
        string path = $"{MeshRoot}/{Sanitize(name)}.asset";
        Mesh built = BuildBox(size, center);
        built.name = "GM_STAIR_APERTURE_JOINT_" + Sanitize(name);
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(built, path);
            return built;
        }
        EditorUtility.CopySerialized(built, existing);
        UnityEngine.Object.DestroyImmediate(built);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    private static Mesh BuildBox(Vector3 size, Vector3 center)
    {
        float hx = size.x * 0.5f, hy = size.y * 0.5f, hz = size.z * 0.5f;
        var v = new List<Vector3>(24);
        var t = new List<int>(36);
        var uv = new List<Vector2>(24);
        Face(v,t,uv,center,new Vector3(-hx,-hy,hz),new Vector3(hx,-hy,hz),new Vector3(hx,hy,hz),new Vector3(-hx,hy,hz),Vector3.forward);
        Face(v,t,uv,center,new Vector3(hx,-hy,-hz),new Vector3(-hx,-hy,-hz),new Vector3(-hx,hy,-hz),new Vector3(hx,hy,-hz),Vector3.back);
        Face(v,t,uv,center,new Vector3(hx,-hy,hz),new Vector3(hx,-hy,-hz),new Vector3(hx,hy,-hz),new Vector3(hx,hy,hz),Vector3.right);
        Face(v,t,uv,center,new Vector3(-hx,-hy,-hz),new Vector3(-hx,-hy,hz),new Vector3(-hx,hy,hz),new Vector3(-hx,hy,-hz),Vector3.left);
        Face(v,t,uv,center,new Vector3(-hx,hy,hz),new Vector3(hx,hy,hz),new Vector3(hx,hy,-hz),new Vector3(-hx,hy,-hz),Vector3.up);
        Face(v,t,uv,center,new Vector3(-hx,-hy,-hz),new Vector3(hx,-hy,-hz),new Vector3(hx,-hy,hz),new Vector3(-hx,-hy,hz),Vector3.down);
        var mesh = new Mesh(); mesh.SetVertices(v); mesh.SetTriangles(t,0); mesh.SetUVs(0,uv);
        mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds(); return mesh;
    }

    private static void Face(List<Vector3> vertices, List<int> triangles, List<Vector2> uv, Vector3 center,
        Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
    {
        int s = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
        foreach (Vector3 q in new[] { a,b,c,d })
        {
            Vector3 p = center + q; Vector3 n = new Vector3(Mathf.Abs(normal.x),Mathf.Abs(normal.y),Mathf.Abs(normal.z));
            if (n.z >= n.x && n.z >= n.y) uv.Add(new Vector2(p.x/MacroTileMeters,p.y/MacroTileMeters));
            else if (n.x >= n.y) uv.Add(new Vector2(p.z/MacroTileMeters,p.y/MacroTileMeters));
            else uv.Add(new Vector2(p.x/MacroTileMeters,p.z/MacroTileMeters));
        }
        bool forward = Vector3.Dot(Vector3.Cross(b-a,c-a),normal) >= 0f;
        if (forward) { triangles.Add(s);triangles.Add(s+1);triangles.Add(s+2);triangles.Add(s);triangles.Add(s+2);triangles.Add(s+3); }
        else { triangles.Add(s);triangles.Add(s+2);triangles.Add(s+1);triangles.Add(s);triangles.Add(s+3);triangles.Add(s+2); }
    }

    private static float PositiveOverlapVolume(Bounds a, Bounds b)
    {
        float x = Mathf.Max(0f, Mathf.Min(a.max.x,b.max.x)-Mathf.Max(a.min.x,b.min.x));
        float y = Mathf.Max(0f, Mathf.Min(a.max.y,b.max.y)-Mathf.Max(a.min.y,b.min.y));
        float z = Mathf.Max(0f, Mathf.Min(a.max.z,b.max.z)-Mathf.Max(a.min.z,b.min.z));
        return x*y*z;
    }

    private static void AssertTouchNoPositiveOverlap(float a, float b, string label, bool reverse = false)
    {
        float delta = reverse ? b-a : a-b;
        if (Mathf.Abs(delta) > TouchTolerance)
            throw new InvalidOperationException($"{label} does not form the locked butt joint: delta={delta:F6} m.");
    }

    private static Renderer RequireRenderer(Transform tier, string name)
    {
        Transform child = tier != null ? tier.Find(name) : null;
        Renderer r = child != null ? child.GetComponent<Renderer>() : null;
        if (r == null) throw new InvalidOperationException($"Required stair shell renderer missing: {name}");
        return r;
    }

    private static void EnsureScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static GameObject FindSceneObject(string name) => Resources.FindObjectsOfTypeAll<GameObject>()
        .FirstOrDefault(x => x != null && x.scene.IsValid() && x.scene.path == ScenePath && x.name == name);

    private static string AbsolutePath(string assetPath)
    {
        string root = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrWhiteSpace(root)) throw new InvalidOperationException("Could not resolve Unity project root.");
        return Path.GetFullPath(Path.Combine(root, assetPath));
    }

    private static string Sanitize(string value)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c,'_');
        return value.Replace('/','_').Replace('\\','_');
    }
}
