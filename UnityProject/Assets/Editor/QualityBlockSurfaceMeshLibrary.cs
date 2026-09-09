using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Deterministic dimension-baked meshes for the benchmark's retained structural fallback masses.
/// Unlike Unity's raw Cube primitive, these boxes carry a real millimetric edge break so concrete,
/// painted steel and equipment casings can produce a narrow geometric highlight at grazing angles.
/// Dimensions are baked into the mesh to avoid scale-dependent bevel width.
/// </summary>
public static class QualityBlockSurfaceMeshLibrary
{
    private const string MeshRoot = "Assets/Art/GeneratedSurfaceMeshes";

    public static Mesh GetChamferedBox(Vector3 size, float maximumBevelMetres)
    {
        size = Abs(size);
        if (size.x <= 0f || size.y <= 0f || size.z <= 0f)
            throw new ArgumentOutOfRangeException(nameof(size), "Structural box dimensions must be positive.");
        if (maximumBevelMetres <= 0f)
            throw new ArgumentOutOfRangeException(nameof(maximumBevelMetres), "Bevel cap must be positive.");

        float smallest = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
        float bevel = Mathf.Min(maximumBevelMetres, smallest * 0.18f);
        bevel = Mathf.Max(0.0004f, bevel);
        bevel = Mathf.Min(bevel, smallest * 0.45f);

        string path = $"{MeshRoot}/GM_SURF_ChamferBox_{Key(size.x)}_{Key(size.y)}_{Key(size.z)}_B{Key(bevel)}.asset";
        Mesh cached = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (cached != null) return cached;

        Directory.CreateDirectory(MeshRoot);
        Mesh mesh = BuildChamferedBox(size, bevel);
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

        // Broad faces stop before each edge. Real bevel strips bridge them; no highlight is painted.
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

        for (int sx = -1; sx <= 1; sx += 2)
        for (int sz = -1; sz <= 1; sz += 2)
            AddQuad(vertices, triangles, uv,
                new Vector3(sx * ix, -iy, sz * hz), new Vector3(sx * ix, iy, sz * hz),
                new Vector3(sx * hx, iy, sz * iz), new Vector3(sx * hx, -iy, sz * iz));

        for (int sy = -1; sy <= 1; sy += 2)
        for (int sz = -1; sz <= 1; sz += 2)
            AddQuad(vertices, triangles, uv,
                new Vector3(-ix, sy * hy, sz * iz), new Vector3(ix, sy * hy, sz * iz),
                new Vector3(ix, sy * iy, sz * hz), new Vector3(-ix, sy * iy, sz * hz));

        for (int sx = -1; sx <= 1; sx += 2)
        for (int sy = -1; sy <= 1; sy += 2)
            AddQuad(vertices, triangles, uv,
                new Vector3(sx * hx, sy * iy, -iz), new Vector3(sx * hx, sy * iy, iz),
                new Vector3(sx * ix, sy * hy, iz), new Vector3(sx * ix, sy * hy, -iz));

        for (int sx = -1; sx <= 1; sx += 2)
        for (int sy = -1; sy <= 1; sy += 2)
        for (int sz = -1; sz <= 1; sz += 2)
            AddTriangle(vertices, triangles, uv,
                new Vector3(sx * hx, sy * iy, sz * iz),
                new Vector3(sx * ix, sy * hy, sz * iz),
                new Vector3(sx * ix, sy * iy, sz * hz));

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
        AddOrientedTriangle(vertices, triangles, start, start + 1, start + 2);
        AddOrientedTriangle(vertices, triangles, start, start + 2, start + 3);
    }

    private static void AddTriangle(List<Vector3> vertices, List<int> triangles, List<Vector2> uv,
        Vector3 a, Vector3 b, Vector3 c)
    {
        int start = vertices.Count;
        vertices.Add(a); vertices.Add(b); vertices.Add(c);
        uv.Add(new Vector2(0f, 0f)); uv.Add(new Vector2(1f, 0f)); uv.Add(new Vector2(0.5f, 1f));
        AddOrientedTriangle(vertices, triangles, start, start + 1, start + 2);
    }

    private static void AddOrientedTriangle(List<Vector3> vertices, List<int> triangles, int a, int b, int c)
    {
        Vector3 normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
        Vector3 center = (vertices[a] + vertices[b] + vertices[c]) / 3f;
        if (Vector3.Dot(normal, center) >= 0f)
        {
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
        }
        else
        {
            triangles.Add(a); triangles.Add(c); triangles.Add(b);
        }
    }

    private static Vector3 Abs(Vector3 v) =>
        new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    private static int Key(float metres) => Mathf.RoundToInt(Mathf.Abs(metres) * 10000f);
}
