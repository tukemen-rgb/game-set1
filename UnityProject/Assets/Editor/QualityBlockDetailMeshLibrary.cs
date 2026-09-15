using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Deterministic authored mesh cache for the high-detail fallback. The detail pass previously used
/// Unity Cube/Cylinder primitives, which left razor-sharp edges and unmistakable primitive shading.
/// This library bakes real dimensions into reusable mesh assets so bevel widths stay millimetric
/// even on long rails, sills and fascia pieces.
/// </summary>
public static class QualityBlockDetailMeshLibrary
{
    private const string MeshRoot = "Assets/Art/GeneratedDetailMeshes";

    public static Mesh GetChamferedBox(Vector3 size)
    {
        size = Abs(size);
        float bevel = PreferredBevel(size);
        string path = $"{MeshRoot}/GM_HD_ChamferBox_{Key(size.x)}_{Key(size.y)}_{Key(size.z)}_B{Key(bevel)}.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh != null) return mesh;

        EnsureRoot();
        mesh = BuildChamferedBox(size, bevel);
        mesh.name = Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    /// <summary>
    /// Preserves the rendered dimensions of Unity's built-in cylinder convention:
    /// x/z scale are the diameter and y scale is half the final height.
    /// Fasteners use six sides so bolt heads read as manufactured hex hardware rather than pegs.
    /// </summary>
    public static Mesh GetBeveledCylinder(Vector3 legacyPrimitiveScale, bool fastener)
    {
        Vector3 scale = Abs(legacyPrimitiveScale);
        float diameter = Mathf.Max(scale.x, scale.z);
        float radius = Mathf.Max(0.0005f, diameter * 0.5f);
        float height = Mathf.Max(0.001f, scale.y * 2f);
        int sides = fastener ? 6 : 20;
        float bevel = Mathf.Min(0.0035f, radius * 0.22f, height * 0.18f);
        bevel = Mathf.Max(0.00035f, bevel);
        bevel = Mathf.Min(bevel, radius * 0.45f, height * 0.45f);

        string path = $"{MeshRoot}/GM_HD_BevelCylinder_D{Key(diameter)}_H{Key(height)}_S{sides}_B{Key(bevel)}.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh != null) return mesh;

        EnsureRoot();
        mesh = BuildBeveledCylinder(radius, height, bevel, sides);
        mesh.name = Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static Mesh BuildChamferedBox(Vector3 size, float bevel)
    {
        float hx = size.x * 0.5f;
        float hy = size.y * 0.5f;
        float hz = size.z * 0.5f;
        float ix = Mathf.Max(0.0001f, hx - bevel);
        float iy = Mathf.Max(0.0001f, hy - bevel);
        float iz = Mathf.Max(0.0001f, hz - bevel);

        var vertices = new List<Vector3>(144);
        var triangles = new List<int>(180);
        var uv = new List<Vector2>(144);

        // Six broad faces. Each stops short of the physical edge; bevel strips and corner facets
        // bridge those gaps, producing a real highlight band instead of a shader-only fake.
        AddQuad(vertices, triangles, uv,
            new Vector3(-ix, -iy, hz), new Vector3(ix, -iy, hz),
            new Vector3(ix, iy, hz), new Vector3(-ix, iy, hz));
        AddQuad(vertices, triangles, uv,
            new Vector3(ix, -iy, -hz), new Vector3(-ix, -iy, -hz),
            new Vector3(-ix, iy, -hz), new Vector3(ix, iy, -hz));
        AddQuad(vertices, triangles, uv,
            new Vector3(hx, -iy, iz), new Vector3(hx, -iy, -iz),
            new Vector3(hx, iy, -iz), new Vector3(hx, iy, iz));
        AddQuad(vertices, triangles, uv,
            new Vector3(-hx, -iy, -iz), new Vector3(-hx, -iy, iz),
            new Vector3(-hx, iy, iz), new Vector3(-hx, iy, -iz));
        AddQuad(vertices, triangles, uv,
            new Vector3(-ix, hy, iz), new Vector3(ix, hy, iz),
            new Vector3(ix, hy, -iz), new Vector3(-ix, hy, -iz));
        AddQuad(vertices, triangles, uv,
            new Vector3(-ix, -hy, -iz), new Vector3(ix, -hy, -iz),
            new Vector3(ix, -hy, iz), new Vector3(-ix, -hy, iz));

        // Four vertical edge bevels.
        for (int sx = -1; sx <= 1; sx += 2)
        for (int sz = -1; sz <= 1; sz += 2)
            AddQuad(vertices, triangles, uv,
                new Vector3(sx * ix, -iy, sz * hz), new Vector3(sx * ix, iy, sz * hz),
                new Vector3(sx * hx, iy, sz * iz), new Vector3(sx * hx, -iy, sz * iz));

        // Four x-axis edge bevels.
        for (int sy = -1; sy <= 1; sy += 2)
        for (int sz = -1; sz <= 1; sz += 2)
            AddQuad(vertices, triangles, uv,
                new Vector3(-ix, sy * hy, sz * iz), new Vector3(ix, sy * hy, sz * iz),
                new Vector3(ix, sy * iy, sz * hz), new Vector3(-ix, sy * iy, sz * hz));

        // Four z-axis edge bevels.
        for (int sx = -1; sx <= 1; sx += 2)
        for (int sy = -1; sy <= 1; sy += 2)
            AddQuad(vertices, triangles, uv,
                new Vector3(sx * hx, sy * iy, -iz), new Vector3(sx * hx, sy * iy, iz),
                new Vector3(sx * ix, sy * hy, iz), new Vector3(sx * ix, sy * hy, -iz));

        // Eight clipped corners complete the convex shell.
        for (int sx = -1; sx <= 1; sx += 2)
        for (int sy = -1; sy <= 1; sy += 2)
        for (int sz = -1; sz <= 1; sz += 2)
            AddTriangle(vertices, triangles, uv,
                new Vector3(sx * hx, sy * iy, sz * iz),
                new Vector3(sx * ix, sy * hy, sz * iz),
                new Vector3(sx * ix, sy * iy, sz * hz));

        return FinishMesh(vertices, triangles, uv);
    }

    private static Mesh BuildBeveledCylinder(float radius, float height, float bevel, int sides)
    {
        float halfHeight = height * 0.5f;
        float innerRadius = Mathf.Max(radius * 0.55f, radius - bevel);
        float sideBottom = -halfHeight + bevel;
        float sideTop = halfHeight - bevel;
        var vertices = new List<Vector3>(sides * 22);
        var triangles = new List<int>(sides * 30);
        var uv = new List<Vector2>(sides * 22);

        for (int i = 0; i < sides; i++)
        {
            float a0 = Mathf.PI * 2f * i / sides;
            float a1 = Mathf.PI * 2f * (i + 1) / sides;
            Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
            Vector3 d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));

            Vector3 bottomInner0 = d0 * innerRadius + Vector3.down * halfHeight;
            Vector3 bottomInner1 = d1 * innerRadius + Vector3.down * halfHeight;
            Vector3 bottomOuter0 = d0 * radius + Vector3.up * sideBottom;
            Vector3 bottomOuter1 = d1 * radius + Vector3.up * sideBottom;
            Vector3 topOuter0 = d0 * radius + Vector3.up * sideTop;
            Vector3 topOuter1 = d1 * radius + Vector3.up * sideTop;
            Vector3 topInner0 = d0 * innerRadius + Vector3.up * halfHeight;
            Vector3 topInner1 = d1 * innerRadius + Vector3.up * halfHeight;

            AddTriangle(vertices, triangles, uv, Vector3.down * halfHeight, bottomInner1, bottomInner0);
            AddQuad(vertices, triangles, uv, bottomInner0, bottomInner1, bottomOuter1, bottomOuter0);
            AddQuad(vertices, triangles, uv, bottomOuter0, bottomOuter1, topOuter1, topOuter0);
            AddQuad(vertices, triangles, uv, topOuter0, topOuter1, topInner1, topInner0);
            AddTriangle(vertices, triangles, uv, Vector3.up * halfHeight, topInner0, topInner1);
        }

        return FinishMesh(vertices, triangles, uv);
    }

    private static Mesh FinishMesh(List<Vector3> vertices, List<int> triangles, List<Vector2> uv)
    {
        var mesh = new Mesh();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uv);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddQuad(List<Vector3> vertices, List<int> triangles, List<Vector2> uv,
        Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        int start = vertices.Count;
        vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
        uv.Add(new Vector2(0f, 0f)); uv.Add(new Vector2(1f, 0f));
        uv.Add(new Vector2(1f, 1f)); uv.Add(new Vector2(0f, 1f));
        Vector3 normal = Vector3.Cross(b - a, c - a);
        Vector3 center = (a + b + c + d) * 0.25f;
        if (Vector3.Dot(normal, center) >= 0f)
        {
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
        }
        else
        {
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
            triangles.Add(start); triangles.Add(start + 3); triangles.Add(start + 2);
        }
    }

    private static void AddTriangle(List<Vector3> vertices, List<int> triangles, List<Vector2> uv,
        Vector3 a, Vector3 b, Vector3 c)
    {
        int start = vertices.Count;
        vertices.Add(a); vertices.Add(b); vertices.Add(c);
        uv.Add(new Vector2(0f, 0f)); uv.Add(new Vector2(1f, 0f)); uv.Add(new Vector2(0.5f, 1f));
        Vector3 normal = Vector3.Cross(b - a, c - a);
        Vector3 center = (a + b + c) / 3f;
        if (Vector3.Dot(normal, center) >= 0f)
        {
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
        }
        else
        {
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
        }
    }

    private static float PreferredBevel(Vector3 size)
    {
        float smallest = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
        float bevel = Mathf.Min(0.012f, smallest * 0.18f);
        bevel = Mathf.Max(0.0008f, bevel);
        return Mathf.Min(bevel, smallest * 0.45f);
    }

    private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    private static int Key(float meters) => Mathf.RoundToInt(Mathf.Abs(meters) * 10000f);

    private static void EnsureRoot()
    {
        if (!Directory.Exists(MeshRoot)) Directory.CreateDirectory(MeshRoot);
    }
}
