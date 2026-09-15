using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.Rendering;

// Import-time authored surfaces, not runtime placeholders or scene-open side effects.
// All native Unity compilation, import parity, lighting and LOD verification remain pending.
[ScriptedImporter(1, "ntprop")]
public sealed class NewTownGardenPropImporter : ScriptedImporter
{
    [Serializable] private sealed class ProfilePoint { public float r, y; }
    [Serializable] private sealed class Surface
    {
        public string name, material, kind, axis;
        public Vector3 offset;
        public ProfilePoint[] profile;
        public float radius, centreHeight, topHeight, tubeRadius;
        public float y0, y1, r0, r1, width, thickness, angleDegrees;
    }
    [Serializable] private sealed class MaterialSpec
    {
        public string id;
        public Vector3 albedoSRGB;
        public float metallic, roughness, normalScale, wetness, tileMetres;
        public float microRoughness, microAlbedo;
        public int textureSize;
    }
    [Serializable] private sealed class Model
    {
        public int schema, masterSegments;
        public string name, units;
        public int[] lodSegments;
        public float[] lodTransitions;
        public MaterialSpec[] materials;
        public Surface[] components;
    }

    public override void OnImportAsset(AssetImportContext ctx)
    {
        string source = File.ReadAllText(ctx.assetPath);
        Model data = JsonUtility.FromJson<Model>(source);
        if (data == null || data.schema != 1 || data.units != "metres" ||
            data.lodSegments == null || data.lodSegments.Length != 4 ||
            data.lodTransitions == null || data.lodTransitions.Length != 4 ||
            data.components == null || data.components.Length == 0 ||
            data.materials == null || data.materials.Length == 0)
            throw new InvalidDataException("Incomplete ntprop assembly.");
        Shader shader = Shader.Find("Standard");
        if (shader == null) throw new InvalidOperationException("This pack requires the existing Built-in Standard pipeline; no silent shader fallback.");

        var materials = new Material[data.materials.Length];
        var materialIds = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < materials.Length; i++)
        {
            MaterialSpec s = data.materials[i];
            if ((s.metallic != 0f && s.metallic != 1f) || s.roughness < .02f || s.roughness > 1f ||
                s.normalScale != 0f || s.wetness != 0f || s.tileMetres <= 0f || s.textureSize < 16 || s.textureSize > 1024)
                throw new InvalidDataException("Invalid dry material " + s.id);
            materialIds.Add(s.id, i);
            Texture2D albedo = MakeTexture(s, false);
            Texture2D physical = MakeTexture(s, true);
            ctx.AddObjectToAsset("albedo_" + s.id, albedo);
            ctx.AddObjectToAsset("metallic_smoothness_" + s.id, physical);
            var m = new Material(shader) { name = s.id, color = Color.white, enableInstancing = true };
            m.SetTexture("_MainTex", albedo);
            m.SetTexture("_MetallicGlossMap", physical);
            m.SetFloat("_Metallic", s.metallic);
            m.SetFloat("_Glossiness", 1f - s.roughness);
            m.SetFloat("_GlossMapScale", 1f);
            m.SetFloat("_SmoothnessTextureChannel", 0f);
            m.SetFloat("_BumpScale", 0f);
            m.SetColor("_EmissionColor", Color.black);
            m.DisableKeyword("_EMISSION");
            m.EnableKeyword("_METALLICGLOSSMAP");
            m.SetTextureScale("_MainTex", Vector2.one / s.tileMetres);
            m.SetTextureScale("_MetallicGlossMap", Vector2.one / s.tileMetres);
            ctx.AddObjectToAsset("material_" + s.id, m);
            materials[i] = m;
        }

        var root = new GameObject(data.name);
        var group = root.AddComponent<LODGroup>();
        var lods = new LOD[4];
        int previous = int.MaxValue;
        for (int level = 0; level < 4; level++)
        {
            int n = data.lodSegments[level];
            if (n < 8 || n >= previous || data.lodTransitions[level] <= 0f ||
                data.lodTransitions[level] >= (level == 0 ? 1f : data.lodTransitions[level - 1]))
                throw new InvalidDataException("Invalid descending LOD sequence.");
            previous = n;
            Mesh mesh = Build(data, materialIds, n);
            mesh.name = data.name + "_LOD" + level;
            ctx.AddObjectToAsset("mesh_lod" + level, mesh);
            var child = new GameObject("LOD" + level);
            child.transform.SetParent(root.transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            lods[level] = new LOD(data.lodTransitions[level], new Renderer[] { renderer });
        }
        if (data.masterSegments <= data.lodSegments[0] || data.masterSegments > 1024)
            throw new InvalidDataException("Invalid master tessellation.");
        Mesh master = Build(data, materialIds, data.masterSegments);
        master.name = data.name + "_MASTER_NOT_RENDERED";
        ctx.AddObjectToAsset("master_mesh", master); // No extra master renderer or double geometry.
        group.fadeMode = LODFadeMode.CrossFade;
        group.animateCrossFading = true;
        group.SetLODs(lods);
        group.RecalculateBounds();
        ctx.AddObjectToAsset("construction_material_source", new TextAsset(source));
        ctx.AddObjectToAsset("prefab", root);
        ctx.SetMainObject(root);
    }

    private static Texture2D MakeTexture(MaterialSpec s, bool physical)
    {
        int size = s.textureSize;
        var t = new Texture2D(size, size, TextureFormat.RGBA32, true, physical)
        {
            name = s.id + (physical ? "_MetallicSmoothness" : "_Albedo"),
            wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8
        };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            uint z = unchecked((uint)x * 73856093u ^ (uint)y * 19349663u);
            z = unchecked((z ^ (z >> 13)) * 1274126177u);
            float noise = ((z & 65535u) / 65535f - .5f) * 2f;
            Color c;
            if (physical)
                c = new Color(s.metallic, 0f, 0f, 1f - Mathf.Clamp(s.roughness + noise * s.microRoughness, .02f, 1f));
            else
                c = new Color(s.albedoSRGB.x, s.albedoSRGB.y, s.albedoSRGB.z, 1f) * (1f + noise * s.microAlbedo);
            if (!physical) c.a = 1f;
            pixels[y * size + x] = c;
        }
        t.SetPixels32(pixels);
        t.Apply(true, false);
        return t;
    }

    private static Mesh Build(Model data, Dictionary<string, int> ids, int n)
    {
        var b = new MeshWriter(data.materials.Length);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (Surface c in data.components)
        {
            if (!names.Add(c.name)) throw new InvalidDataException("Duplicate component " + c.name);
            b.material = ids[c.material];
            switch (c.kind)
            {
                case "lathe": Lathe(b, c, n); break;
                case "bail": Bail(b, c, n); break;
                case "seam": Seam(b, c); break;
                default: throw new InvalidDataException("Unknown authored surface " + c.kind);
            }
        }
        return b.Finish();
    }

    private static Vector3 Orient(Vector3 v, string axis)
    {
        switch (axis)
        {
            case "y": return v;
            case "x": return new Vector3(v.y, -v.x, v.z);
            case "-x": return new Vector3(-v.y, v.x, v.z);
            case "z": return new Vector3(v.x, -v.z, v.y);
            default: throw new InvalidDataException("Unknown axis " + axis);
        }
    }
    private static Vector2 ProfileNormal(Vector2[] edges, int point, int edge)
    {
        Vector2 a = edges[(point + edges.Length - 1) % edges.Length], z = edges[point];
        return Vector2.Dot(a, z) > .5f ? (a + z).normalized : edges[edge];
    }
    private static void Lathe(MeshWriter b, Surface c, int segments)
    {
        if (c.profile == null || c.profile.Length < 3) throw new InvalidDataException("Missing profile");
        int count = c.profile.Length;
        var p = new Vector2[count];
        var edges = new Vector2[count];
        var distance = new float[count + 1];
        float radius = 0f, area = 0f;
        for (int j = 0; j < count; j++)
        {
            p[j] = new Vector2(c.profile[j].r, c.profile[j].y);
            if (p[j].x < 0f) throw new InvalidDataException("Negative profile radius");
            radius = Mathf.Max(radius, p[j].x);
        }
        for (int j = 0; j < count; j++)
        {
            Vector2 next = p[(j + 1) % count], delta = next - p[j];
            if (delta.sqrMagnitude < 1e-14f) throw new InvalidDataException("Repeated profile point");
            Vector2 tangent = delta.normalized;
            edges[j] = new Vector2(tangent.y, -tangent.x);
            distance[j + 1] = distance[j] + delta.magnitude;
            area += p[j].x * next.y - next.x * p[j].y;
        }
        if (area <= 0f) throw new InvalidDataException("Profile must wind counterclockwise");
        for (int j = 0; j < count; j++) for (int i = 0; i < segments; i++)
        {
            int k = (j + 1) % count;
            int[] js = { j, k, k, j }, angles = { i, i, i + 1, i + 1 };
            var points = new Vector3[4]; var normals = new Vector3[4]; var uv = new Vector2[4];
            for (int q = 0; q < 4; q++)
            {
                float a = 2f * Mathf.PI * angles[q] / segments, cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                Vector2 s = p[js[q]], normal = ProfileNormal(edges, js[q], j);
                points[q] = Orient(new Vector3(s.x * cs, s.y, s.x * sn), c.axis) + c.offset;
                normals[q] = Orient(new Vector3(normal.x * cs, normal.y, normal.x * sn), c.axis);
                uv[q] = new Vector2(2f * Mathf.PI * radius * angles[q] / segments, distance[q == 0 || q == 3 ? j : j + 1]);
            }
            b.Quad(points, normals, uv);
        }
    }

    private static void Bail(MeshWriter b, Surface c, int segments)
    {
        if (c.radius <= 0f || c.tubeRadius <= 0f || c.topHeight <= c.centreHeight || c.topHeight >= c.centreHeight + c.radius)
            throw new InvalidDataException("Invalid bail radius/bridge");
        int steps = Mathf.Max(4, segments / 4), radial = Mathf.Max(6, segments / 8);
        float a = Mathf.Asin((c.topHeight - c.centreHeight) / c.radius);
        var path = new List<Vector3>();
        for (int side = 0; side < 2; side++) for (int i = 0; i <= steps; i++)
        {
            float t = side == 0 ? a * i / steps : Mathf.PI - a + a * i / steps;
            path.Add(new Vector3(c.radius * Mathf.Cos(t), Mathf.Min(c.topHeight, c.centreHeight + c.radius * Mathf.Sin(t)), 0f));
        }
        var frames = new Vector3[path.Count]; var distance = new float[path.Count];
        for (int i = 0; i < path.Count; i++)
        {
            Vector3 t = (path[Mathf.Min(i + 1, path.Count - 1)] - path[Mathf.Max(0, i - 1)]).normalized;
            frames[i] = new Vector3(-t.y, t.x, 0f);
            if (i > 0) distance[i] = distance[i - 1] + Vector3.Distance(path[i], path[i - 1]);
        }
        for (int i = 0; i < path.Count - 1; i++) for (int j = 0; j < radial; j++)
        {
            int[] ps = { i, i, i + 1, i + 1 }, rs = { j, j + 1, j + 1, j };
            var pp = new Vector3[4]; var nn = new Vector3[4]; var uv = new Vector2[4];
            for (int q = 0; q < 4; q++)
            {
                float angle = 2f * Mathf.PI * rs[q] / radial;
                nn[q] = frames[ps[q]] * Mathf.Cos(angle) + Vector3.forward * Mathf.Sin(angle);
                pp[q] = path[ps[q]] + nn[q] * c.tubeRadius;
                uv[q] = new Vector2(distance[ps[q]], angle * c.tubeRadius);
            }
            b.Quad(pp, nn, uv);
        }
        for (int end = 0; end < 2; end++)
        {
            int i = end == 0 ? 0 : path.Count - 1, other = end == 0 ? 1 : i - 1;
            Vector3 normal = (path[i] - path[other]).normalized;
            for (int j = 0; j < radial; j++)
            {
                float p = 2f * Mathf.PI * j / radial, q = 2f * Mathf.PI * (j + 1) / radial;
                Vector3 first = path[i] + (frames[i] * Mathf.Cos(p) + Vector3.forward * Mathf.Sin(p)) * c.tubeRadius;
                Vector3 second = path[i] + (frames[i] * Mathf.Cos(q) + Vector3.forward * Mathf.Sin(q)) * c.tubeRadius;
                b.Triangle(path[i], first, second, normal, normal, normal, Vector2.zero, Vector2.up, Vector2.one);
            }
        }
    }
    private static void Seam(MeshWriter b, Surface c)
    {
        float a = c.angleDegrees * Mathf.Deg2Rad;
        Vector3 radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        Vector3 across = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
        var p = new Vector3[8]; Vector3 centre = Vector3.zero;
        for (int row = 0; row < 2; row++) for (int i = 0; i < 4; i++)
        {
            float r = row == 0 ? c.r0 : c.r1, y = row == 0 ? c.y0 : c.y1;
            float dr = i == 1 || i == 2 ? c.thickness : 0f;
            p[row * 4 + i] = radial * (r + dr) + across * (i < 2 ? -.5f : .5f) * c.width + Vector3.up * y;
            centre += p[row * 4 + i] / 8f;
        }
        int[,] faces = { {0,1,2,3}, {4,7,6,5}, {0,4,5,1}, {1,5,6,2}, {2,6,7,3}, {3,7,4,0} };
        for (int f = 0; f < 6; f++)
        {
            var q = new Vector3[4]; Vector3 fc = Vector3.zero;
            for (int i = 0; i < 4; i++) { q[i] = p[faces[f, i]]; fc += q[i] / 4f; }
            Vector3 normal = Vector3.Cross(q[1] - q[0], q[2] - q[0]).normalized;
            if (Vector3.Dot(normal, fc - centre) < 0f) normal = -normal;
            b.Quad(q, new[] {normal,normal,normal,normal}, new[] {Vector2.zero, Vector2.up * (c.y1-c.y0), new Vector2(c.width,c.y1-c.y0), Vector2.right*c.width});
        }
    }

    private sealed class MeshWriter
    {
        private readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>();
        private readonly List<Vector2> uv = new List<Vector2>();
        private readonly List<int>[] faces;
        public int material;
        public MeshWriter(int count)
        {
            faces = new List<int>[count];
            for (int i = 0; i < count; i++) faces[i] = new List<int>();
        }
        public void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc, Vector2 ua, Vector2 ub, Vector2 uc)
        {
            Vector3 cross = Vector3.Cross(b - a, c - a);
            if (cross.sqrMagnitude < 1e-22f) return;
            if (Vector3.Dot(cross, na + nb + nc) < 0f)
            {
                Vector3 t = b; b = c; c = t; t = nb; nb = nc; nc = t;
                Vector2 u = ub; ub = uc; uc = u;
            }
            int start = v.Count;
            v.Add(a); v.Add(b); v.Add(c); n.Add(na); n.Add(nb); n.Add(nc); uv.Add(ua); uv.Add(ub); uv.Add(uc);
            faces[material].Add(start); faces[material].Add(start+1); faces[material].Add(start+2);
        }
        public void Quad(Vector3[] p, Vector3[] nn, Vector2[] u)
        {
            Triangle(p[0],p[1],p[2],nn[0],nn[1],nn[2],u[0],u[1],u[2]);
            Triangle(p[0],p[2],p[3],nn[0],nn[2],nn[3],u[0],u[2],u[3]);
        }
        public Mesh Finish()
        {
            var mesh = new Mesh { indexFormat = v.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0,uv); mesh.subMeshCount = faces.Length;
            for (int i = 0; i < faces.Length; i++) mesh.SetTriangles(faces[i],i,false);
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
            return mesh;
        }
    }
}
