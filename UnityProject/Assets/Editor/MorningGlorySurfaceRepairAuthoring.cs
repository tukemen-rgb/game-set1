using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Repairs two concrete surface defects in MorningGloryTrellisAuthoring output:
// 1) leaf side walls were rebuilt at flat heights, leaving visible cracks against the arched leaf skins;
// 2) flower funnels only had an outward skin, so their interiors could disappear with back-face culling.
//
// This command deliberately runs after the existing deterministic authoring command and mutates only
// its generated mesh assets in place. It does not modify scenes, Godot content, lighting, benchmark
// evidence, or the central Visual Fidelity Gate. Real Unity compile/import/render verification remains pending.
public static class MorningGlorySurfaceRepairAuthoring
{
    private const string Root = "Assets/Art/GardenProps/MorningGloryTrellis/Generated";
    private const int LeafMaterial = 3;
    private const int FlowerBlueMaterial = 5;
    private const int FlowerThroatMaterial = 6;
    private const int LeafSegments = 16;
    private const int SourceLeafTrianglesPerSegment = 4; // top, bottom, two legacy side triangles
    private const float PetalWallThickness = 0.00015f; // explicit modeling assumption: 0.15 mm

    private static readonly string[] Levels = { "MASTER", "LOD0", "LOD1", "LOD2", "LOD3" };
    private static readonly int[] ExpectedLeaves = { 56, 48, 36, 24, 14 };
    private static readonly int[] ExpectedFlowers = { 10, 8, 6, 4, 2 };

    [MenuItem("Tools/New Town/Author/Morning Glory Trellis Assets - Surface Repaired")]
    public static void BuildAndRepair()
    {
        MorningGloryTrellisAuthoring.BuildAssets();
        for (int i = 0; i < Levels.Length; i++)
        {
            string path = Root + "/MorningGloryTrellis_" + Levels[i] + ".asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) throw new InvalidOperationException("Missing generated morning-glory mesh: " + path);
            RepairMesh(mesh, ExpectedLeaves[i], ExpectedFlowers[i]);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[GardenProps] Morning-glory surface repair authored. Leaf edge cracks and one-sided flower interiors repaired. Unity render/LOD verification still pending.");
    }

    private static void RepairMesh(Mesh mesh, int expectedLeaves, int expectedFlowers)
    {
        if (mesh.subMeshCount < 8) throw new InvalidOperationException(mesh.name + ": expected 8 material submeshes.");

        List<Vector3> vertices = new List<Vector3>(mesh.vertices);
        List<Vector3> normals = new List<Vector3>(mesh.normals);
        if (normals.Count != vertices.Count) throw new InvalidOperationException(mesh.name + ": authored normals are required.");

        List<int>[] sub = new List<int>[mesh.subMeshCount];
        for (int i = 0; i < sub.Length; i++) sub[i] = new List<int>(mesh.GetTriangles(i));

        RepairLeaves(mesh.name, vertices, normals, sub, expectedLeaves);
        RepairFlowers(mesh.name, vertices, normals, sub, expectedFlowers);

        mesh.Clear(false);
        mesh.indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.subMeshCount = sub.Length;
        for (int i = 0; i < sub.Length; i++) mesh.SetTriangles(sub[i], i, false);
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
    }

    private static void RepairLeaves(string meshName, List<Vector3> vertices, List<Vector3> normals, List<int>[] sub, int expectedLeaves)
    {
        List<int> source = sub[LeafMaterial];
        int sourceTrianglesPerLeaf = LeafSegments * SourceLeafTrianglesPerSegment;
        if (source.Count != expectedLeaves * sourceTrianglesPerLeaf * 3)
            throw new InvalidOperationException(meshName + ": leaf topology changed; refusing speculative repair. Expected " +
                                                (expectedLeaves * sourceTrianglesPerLeaf) + " triangles, got " + (source.Count / 3) + ".");

        List<int> repaired = new List<int>(expectedLeaves * LeafSegments * 4 * 3);
        for (int leaf = 0; leaf < expectedLeaves; leaf++)
        {
            int leafStart = leaf * sourceTrianglesPerLeaf * 3;
            int topCentre = FindMostFrequentVertex(source, leafStart, LeafSegments, 0);
            int bottomCentre = FindMostFrequentVertex(source, leafStart, LeafSegments, 1);
            Vector3 leafCentre = (vertices[topCentre] + vertices[bottomCentre]) * 0.5f;

            for (int seg = 0; seg < LeafSegments; seg++)
            {
                int triBase = leafStart + seg * SourceLeafTrianglesPerSegment * 3;
                int[] top = { source[triBase], source[triBase + 1], source[triBase + 2] };
                int[] bottom = { source[triBase + 3], source[triBase + 4], source[triBase + 5] };

                repaired.Add(top[0]); repaired.Add(top[1]); repaired.Add(top[2]);
                repaired.Add(bottom[0]); repaired.Add(bottom[1]); repaired.Add(bottom[2]);

                int[] topEdge = NonCentrePair(top, topCentre);
                int[] bottomEdge = NonCentrePair(bottom, bottomCentre);
                MatchByDistance(vertices, topEdge, bottomEdge);

                Vector3 p0 = vertices[topEdge[0]], p1 = vertices[topEdge[1]];
                Vector3 p2 = vertices[bottomEdge[1]], p3 = vertices[bottomEdge[0]];
                Vector3 sideNormal = Vector3.Cross(p1 - p0, p3 - p0).normalized;
                Vector3 sideMid = (p0 + p1 + p2 + p3) * 0.25f;
                if (Vector3.Dot(sideNormal, sideMid - leafCentre) < 0f) sideNormal = -sideNormal;

                int a = AppendVertex(vertices, normals, p0, sideNormal);
                int b = AppendVertex(vertices, normals, p1, sideNormal);
                int c = AppendVertex(vertices, normals, p2, sideNormal);
                int d = AppendVertex(vertices, normals, p3, sideNormal);
                AddTriangleOriented(repaired, vertices, normals, a, b, c);
                AddTriangleOriented(repaired, vertices, normals, a, c, d);
            }
        }
        sub[LeafMaterial] = repaired;
    }

    private static int FindMostFrequentVertex(List<int> source, int leafStart, int segments, int triangleOffsetWithinSegment)
    {
        Dictionary<int, int> counts = new Dictionary<int, int>();
        for (int seg = 0; seg < segments; seg++)
        {
            int start = leafStart + (seg * SourceLeafTrianglesPerSegment + triangleOffsetWithinSegment) * 3;
            for (int i = 0; i < 3; i++)
            {
                int v = source[start + i];
                counts.TryGetValue(v, out int count);
                counts[v] = count + 1;
            }
        }
        int best = -1, bestCount = -1;
        foreach (KeyValuePair<int, int> kv in counts)
            if (kv.Value > bestCount) { best = kv.Key; bestCount = kv.Value; }
        if (bestCount != segments) throw new InvalidOperationException("Unexpected leaf fan topology; centre occurrence=" + bestCount + ".");
        return best;
    }

    private static int[] NonCentrePair(int[] triangle, int centre)
    {
        int[] pair = new int[2]; int n = 0;
        for (int i = 0; i < 3; i++) if (triangle[i] != centre)
        {
            if (n >= 2) throw new InvalidOperationException("Leaf triangle contains no unique fan centre.");
            pair[n++] = triangle[i];
        }
        if (n != 2) throw new InvalidOperationException("Leaf fan edge could not be resolved.");
        return pair;
    }

    private static void MatchByDistance(List<Vector3> vertices, int[] top, int[] bottom)
    {
        float straight = Vector3.Distance(vertices[top[0]], vertices[bottom[0]]) + Vector3.Distance(vertices[top[1]], vertices[bottom[1]]);
        float crossed = Vector3.Distance(vertices[top[0]], vertices[bottom[1]]) + Vector3.Distance(vertices[top[1]], vertices[bottom[0]]);
        if (crossed < straight)
        {
            int t = bottom[0]; bottom[0] = bottom[1]; bottom[1] = t;
        }
        float maxGap = Mathf.Max(Vector3.Distance(vertices[top[0]], vertices[bottom[0]]), Vector3.Distance(vertices[top[1]], vertices[bottom[1]]));
        if (maxGap > 0.003f) throw new InvalidOperationException("Unexpected leaf top/bottom separation " + maxGap + " m.");
    }

    private static void RepairFlowers(string meshName, List<Vector3> vertices, List<Vector3> normals, List<int>[] sub, int expectedFlowers)
    {
        List<Face> faces = new List<Face>();
        AppendFaces(faces, sub[FlowerBlueMaterial], FlowerBlueMaterial);
        AppendFaces(faces, sub[FlowerThroatMaterial], FlowerThroatMaterial);
        if (faces.Count == 0) throw new InvalidOperationException(meshName + ": no flower faces.");

        List<List<int>> components = FaceComponentsBySharedVertex(faces);
        if (components.Count != expectedFlowers)
            throw new InvalidOperationException(meshName + ": expected " + expectedFlowers + " flower components, got " + components.Count + ".");

        foreach (List<int> component in components)
        {
            Dictionary<int, int> duplicate = new Dictionary<int, int>();
            Dictionary<EdgeKey, EdgeUse> edges = new Dictionary<EdgeKey, EdgeUse>();

            foreach (int faceIndex in component)
            {
                Face f = faces[faceIndex];
                EnsureInnerVertex(duplicate, vertices, normals, f.a);
                EnsureInnerVertex(duplicate, vertices, normals, f.b);
                EnsureInnerVertex(duplicate, vertices, normals, f.c);
                AccumulateEdge(edges, f.a, f.b, f.material);
                AccumulateEdge(edges, f.b, f.c, f.material);
                AccumulateEdge(edges, f.c, f.a, f.material);
            }

            foreach (int faceIndex in component)
            {
                Face f = faces[faceIndex];
                sub[f.material].Add(duplicate[f.c]);
                sub[f.material].Add(duplicate[f.b]);
                sub[f.material].Add(duplicate[f.a]);
            }

            int boundary = 0;
            foreach (KeyValuePair<EdgeKey, EdgeUse> kv in edges)
            {
                EdgeUse e = kv.Value;
                if (e.count != 1) continue;
                boundary++;
                sub[e.material].Add(e.b); sub[e.material].Add(e.a); sub[e.material].Add(duplicate[e.a]);
                sub[e.material].Add(e.b); sub[e.material].Add(duplicate[e.a]); sub[e.material].Add(duplicate[e.b]);
            }
            if (boundary == 0) throw new InvalidOperationException(meshName + ": flower component unexpectedly had no open lip boundary.");
        }
    }

    private static int EnsureInnerVertex(Dictionary<int, int> duplicate, List<Vector3> vertices, List<Vector3> normals, int source)
    {
        if (duplicate.TryGetValue(source, out int existing)) return existing;
        Vector3 n = normals[source].normalized;
        int index = AppendVertex(vertices, normals, vertices[source] - n * PetalWallThickness, -n);
        duplicate[source] = index;
        return index;
    }

    private static void AppendFaces(List<Face> faces, List<int> indices, int material)
    {
        if (indices.Count % 3 != 0) throw new InvalidOperationException("Triangle index count is not divisible by three.");
        for (int i = 0; i < indices.Count; i += 3) faces.Add(new Face(indices[i], indices[i + 1], indices[i + 2], material));
    }

    private static List<List<int>> FaceComponentsBySharedVertex(List<Face> faces)
    {
        Dictionary<int, List<int>> vertexFaces = new Dictionary<int, List<int>>();
        for (int i = 0; i < faces.Count; i++)
        {
            AddFaceRef(vertexFaces, faces[i].a, i); AddFaceRef(vertexFaces, faces[i].b, i); AddFaceRef(vertexFaces, faces[i].c, i);
        }
        bool[] seen = new bool[faces.Count];
        List<List<int>> result = new List<List<int>>();
        for (int seed = 0; seed < faces.Count; seed++)
        {
            if (seen[seed]) continue;
            List<int> component = new List<int>();
            Queue<int> q = new Queue<int>(); q.Enqueue(seed); seen[seed] = true;
            while (q.Count > 0)
            {
                int f = q.Dequeue(); component.Add(f); Face face = faces[f];
                EnqueueNeighbours(vertexFaces[face.a], seen, q);
                EnqueueNeighbours(vertexFaces[face.b], seen, q);
                EnqueueNeighbours(vertexFaces[face.c], seen, q);
            }
            result.Add(component);
        }
        return result;
    }

    private static void AddFaceRef(Dictionary<int, List<int>> map, int vertex, int face)
    {
        if (!map.TryGetValue(vertex, out List<int> list)) { list = new List<int>(); map[vertex] = list; }
        list.Add(face);
    }

    private static void EnqueueNeighbours(List<int> neighbours, bool[] seen, Queue<int> queue)
    {
        for (int i = 0; i < neighbours.Count; i++) if (!seen[neighbours[i]]) { seen[neighbours[i]] = true; queue.Enqueue(neighbours[i]); }
    }

    private static void AccumulateEdge(Dictionary<EdgeKey, EdgeUse> edges, int a, int b, int material)
    {
        EdgeKey key = new EdgeKey(a, b);
        if (edges.TryGetValue(key, out EdgeUse existing))
        {
            existing.count++;
            edges[key] = existing;
        }
        else edges[key] = new EdgeUse(a, b, material, 1);
    }

    private static int AppendVertex(List<Vector3> vertices, List<Vector3> normals, Vector3 position, Vector3 normal)
    {
        int index = vertices.Count; vertices.Add(position); normals.Add(normal.normalized); return index;
    }

    private static void AddTriangleOriented(List<int> indices, List<Vector3> vertices, List<Vector3> normals, int a, int b, int c)
    {
        Vector3 cross = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
        if (cross.sqrMagnitude < 1e-20f) throw new InvalidOperationException("Surface repair produced a degenerate triangle.");
        if (Vector3.Dot(cross, normals[a] + normals[b] + normals[c]) < 0f) { int t = b; b = c; c = t; }
        indices.Add(a); indices.Add(b); indices.Add(c);
    }

    private readonly struct Face
    {
        public readonly int a, b, c, material;
        public Face(int a, int b, int c, int material) { this.a = a; this.b = b; this.c = c; this.material = material; }
    }

    private readonly struct EdgeKey : IEquatable<EdgeKey>
    {
        private readonly int lo, hi;
        public EdgeKey(int a, int b) { lo = Math.Min(a, b); hi = Math.Max(a, b); }
        public bool Equals(EdgeKey other) { return lo == other.lo && hi == other.hi; }
        public override bool Equals(object obj) { return obj is EdgeKey other && Equals(other); }
        public override int GetHashCode() { unchecked { return (lo * 397) ^ hi; } }
    }

    private struct EdgeUse
    {
        public int a, b, material, count;
        public EdgeUse(int a, int b, int material, int count) { this.a = a; this.b = b; this.material = material; this.count = count; }
    }
}
