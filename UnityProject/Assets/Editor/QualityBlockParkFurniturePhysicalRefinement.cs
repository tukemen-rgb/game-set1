using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Physical refinement for benchmark-close park furniture. Replaces the deliberately simple first-pass
/// slide planes/globe proxy with manufactured-thickness swept/lathed meshes. This pass exists so the
/// benchmark cannot earn construction fidelity from oversized 'readability thickness' geometry.
/// </summary>
public static class QualityBlockParkFurniturePhysicalRefinement
{
    private const string MeshRoot = "Assets/Art/GeneratedParkFurnitureMeshes";
    private const float ChuteThickness = 0.002f; // 2 mm SUS304; above the 1.5 mm municipal minimum in the contract reference.
    private const float ChuteWidth = 0.92f;
    private const float ChuteSideHeight = 0.14f;

    [MenuItem("NewTown/Quality/Refine Park Furniture Physical Profiles")]
    public static void ApplyAndValidate()
    {
        EnsureMeshRoot();
        RefineSlide();
        RefineLampDiffuser();
        AssetDatabase.SaveAssets();
        ValidateOpenScene();
        Debug.Log("Park furniture physical refinement applied: continuous 2 mm slide skin/side sheets and lathed globe diffuser.");
    }

    [MenuItem("NewTown/QA/Validate Park Furniture Physical Profiles")]
    public static void ValidateOpenScene()
    {
        GameObject slide = GameObject.Find("HD_Slide");
        GameObject lamp = GameObject.Find("HD_Lamp");
        if (slide == null || lamp == null)
            throw new InvalidOperationException("Physical-refinement QA requires HD_Slide and HD_Lamp.");

        for (int lod = 0; lod < 4; lod++)
        {
            Transform tier = slide.transform.Find($"LOD{lod}");
            if (tier == null) throw new InvalidOperationException($"HD_Slide/LOD{lod} missing.");
            Transform refined = tier.Find("PhysicalChute");
            MeshFilter mf = refined != null ? refined.GetComponent<MeshFilter>() : null;
            if (mf == null || mf.sharedMesh == null)
                throw new InvalidOperationException($"HD_Slide/LOD{lod}/PhysicalChute missing.");

            Bounds b = mf.sharedMesh.bounds;
            if (b.size.y < 1.70f || b.size.z < 3.90f || b.size.x < 0.90f)
                throw new InvalidOperationException($"Physical chute silhouette/bounds invalid at LOD{lod}: {b.size}");

            AssertLegacyRendererDisabled(tier, "ChuteSheet");
            AssertLegacyRendererDisabled(tier, "ChuteLipL");
            AssertLegacyRendererDisabled(tier, "ChuteLipR");
            AssertLegacyRendererDisabled(tier, "ChuteRunout", allowMissing: lod != 0);
        }

        for (int lod = 0; lod < 4; lod++)
        {
            Transform tier = lamp.transform.Find($"LOD{lod}");
            if (tier == null) throw new InvalidOperationException($"HD_Lamp/LOD{lod} missing.");
            Transform globe = tier.Find("PhysicalDiffuser");
            MeshFilter mf = globe != null ? globe.GetComponent<MeshFilter>() : null;
            if (mf == null || mf.sharedMesh == null)
                throw new InvalidOperationException($"HD_Lamp/LOD{lod}/PhysicalDiffuser missing.");
            AssertLegacyRendererDisabled(tier, "Diffuser");
        }

        foreach (Collider c in slide.GetComponentsInChildren<Collider>(true))
            throw new InvalidOperationException($"Physical slide render shell added a gameplay collider: {c.name}");
        foreach (Collider c in lamp.GetComponentsInChildren<Collider>(true))
            throw new InvalidOperationException($"Physical lamp render shell added a gameplay collider: {c.name}");
    }

    private static void RefineSlide()
    {
        GameObject slide = GameObject.Find("HD_Slide");
        if (slide == null) throw new InvalidOperationException("HD_Slide missing; run park furniture construction pass first.");

        int[] segments = { 96, 48, 24, 12 };
        for (int lod = 0; lod < 4; lod++)
        {
            Transform tier = slide.transform.Find($"LOD{lod}");
            if (tier == null) throw new InvalidOperationException($"HD_Slide/LOD{lod} missing.");

            DisableChildRenderer(tier, "ChuteSheet");
            DisableChildRenderer(tier, "ChuteLipL");
            DisableChildRenderer(tier, "ChuteLipR");
            DisableChildRenderer(tier, "ChuteRunout");
            DestroyChildIfPresent(tier, "PhysicalChute");

            Material stainless = FindMaterialRecursive(tier, "PBR_SlideStainless");
            if (stainless == null) throw new InvalidOperationException("PBR_SlideStainless material missing from generated slide.");

            var go = new GameObject("PhysicalChute");
            go.transform.SetParent(tier, false);
            go.AddComponent<MeshFilter>().sharedMesh = GetSlideChuteMesh(lod, segments[lod]);
            go.AddComponent<MeshRenderer>().sharedMaterial = stainless;
        }
    }

    private static void RefineLampDiffuser()
    {
        GameObject lamp = GameObject.Find("HD_Lamp");
        if (lamp == null) throw new InvalidOperationException("HD_Lamp missing; run park furniture construction pass first.");

        int[] sides = { 32, 24, 16, 10 };
        for (int lod = 0; lod < 4; lod++)
        {
            Transform tier = lamp.transform.Find($"LOD{lod}");
            if (tier == null) throw new InvalidOperationException($"HD_Lamp/LOD{lod} missing.");

            DisableChildRenderer(tier, "Diffuser");
            DestroyChildIfPresent(tier, "PhysicalDiffuser");
            Material diffuser = FindMaterialRecursive(tier, "PBR_LampDiffuserAged");
            if (diffuser == null) throw new InvalidOperationException("PBR_LampDiffuserAged material missing from generated lamp.");

            var go = new GameObject("PhysicalDiffuser");
            go.transform.SetParent(tier, false);
            go.transform.localPosition = new Vector3(0f, 4.43f, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = GetDiffuserMesh(lod, sides[lod]);
            go.AddComponent<MeshRenderer>().sharedMaterial = diffuser;
        }
    }

    private static Mesh GetSlideChuteMesh(int lod, int segments)
    {
        string path = $"{MeshRoot}/GM_ParkSlide_ContinuousSUS2mm_LOD{lod}_S{segments}.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh != null) return mesh;

        mesh = BuildSlideChuteMesh(segments);
        mesh.name = Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static Mesh BuildSlideChuteMesh(int segments)
    {
        var v = new List<Vector3>((segments + 1) * 12);
        var t = new List<int>(segments * 60);
        var uv = new List<Vector2>((segments + 1) * 12);

        // Centerline is cubic Hermite in y(z): deck tangent ~26.6 degrees, smoothly flattening to
        // ~1.7 degrees at the runout. The sheet is a real 2 mm shell rather than a thick box.
        for (int i = 0; i <= segments; i++)
        {
            float u = i / (float)segments;
            float z = Mathf.Lerp(0.18f, 4.38f, u);
            float y = Hermite(u, 1.99f, 0.15f, -2.10f, -0.126f);
            float dydu = HermiteDerivative(u, 1.99f, 0.15f, -2.10f, -0.126f);
            float dzdu = 4.20f;
            Vector3 tangent = new Vector3(0f, dydu, dzdu).normalized;
            Vector3 surfaceNormal = Vector3.Cross(tangent, Vector3.right).normalized;
            if (surfaceNormal.y < 0f) surfaceNormal = -surfaceNormal;

            Vector3 center = new Vector3(0f, y, z);
            Vector3 top = center + surfaceNormal * (ChuteThickness * 0.5f);
            Vector3 bottom = center - surfaceNormal * (ChuteThickness * 0.5f);
            float half = ChuteWidth * 0.5f;
            float sideHalfThickness = ChuteThickness * 0.5f;

            // Main skin: top L/R then bottom L/R.
            v.Add(top + Vector3.left * half);
            v.Add(top + Vector3.right * half);
            v.Add(bottom + Vector3.left * half);
            v.Add(bottom + Vector3.right * half);

            // Folded side sheets: each side has inner/outer base and inner/outer cap.
            Vector3 leftBase = center + Vector3.left * half;
            Vector3 rightBase = center + Vector3.right * half;
            Vector3 capOffset = surfaceNormal * ChuteSideHeight;
            v.Add(leftBase + Vector3.right * sideHalfThickness);
            v.Add(leftBase + Vector3.left * sideHalfThickness);
            v.Add(leftBase + capOffset + Vector3.right * sideHalfThickness);
            v.Add(leftBase + capOffset + Vector3.left * sideHalfThickness);
            v.Add(rightBase + Vector3.left * sideHalfThickness);
            v.Add(rightBase + Vector3.right * sideHalfThickness);
            v.Add(rightBase + capOffset + Vector3.left * sideHalfThickness);
            v.Add(rightBase + capOffset + Vector3.right * sideHalfThickness);

            for (int k = 0; k < 12; k++) uv.Add(new Vector2(k % 2, u * 4.2f));
        }

        for (int i = 0; i < segments; i++)
        {
            int a = i * 12;
            int b = (i + 1) * 12;
            AddQuad(t, a + 0, b + 0, b + 1, a + 1); // top
            AddQuad(t, a + 3, b + 3, b + 2, a + 2); // bottom
            AddQuad(t, a + 4, b + 4, b + 6, a + 6); // left inner
            AddQuad(t, a + 7, b + 7, b + 5, a + 5); // left outer
            AddQuad(t, a + 6, b + 6, b + 7, a + 7); // left cap
            AddQuad(t, a + 8, b + 8, b + 10, a + 10); // right inner
            AddQuad(t, a + 11, b + 11, b + 9, a + 9); // right outer
            AddQuad(t, a + 10, b + 10, b + 11, a + 11); // right cap
        }

        // Seal front/rear exposed sheet edges; thinness is visible under grazing light without becoming a block.
        int first = 0;
        int last = segments * 12;
        AddQuad(t, first + 2, first + 3, first + 1, first + 0);
        AddQuad(t, last + 0, last + 1, last + 3, last + 2);

        var mesh = new Mesh();
        mesh.SetVertices(v);
        mesh.SetTriangles(t, 0);
        mesh.SetUVs(0, uv);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh GetDiffuserMesh(int lod, int sides)
    {
        string path = $"{MeshRoot}/GM_ParkLamp_LathedGlobe_LOD{lod}_S{sides}.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh != null) return mesh;

        mesh = BuildLathedDiffuser(sides);
        mesh.name = Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static Mesh BuildLathedDiffuser(int sides)
    {
        // Five-point milk-glass/polycarbonate globe profile. It is deliberately not a perfect sphere:
        // the narrower necks represent retained rings/collars found on older park lantern diffusers.
        Vector2[] profile =
        {
            new Vector2(0.105f, -0.125f),
            new Vector2(0.175f, -0.080f),
            new Vector2(0.215f,  0.000f),
            new Vector2(0.185f,  0.085f),
            new Vector2(0.112f,  0.130f),
        };

        var v = new List<Vector3>(profile.Length * sides + 2);
        var uv = new List<Vector2>(profile.Length * sides + 2);
        var t = new List<int>((profile.Length - 1) * sides * 6 + sides * 6);

        for (int p = 0; p < profile.Length; p++)
        {
            for (int s = 0; s < sides; s++)
            {
                float a = Mathf.PI * 2f * s / sides;
                v.Add(new Vector3(Mathf.Cos(a) * profile[p].x, profile[p].y, Mathf.Sin(a) * profile[p].x));
                uv.Add(new Vector2(s / (float)sides, p / (float)(profile.Length - 1)));
            }
        }
        for (int p = 0; p < profile.Length - 1; p++)
        for (int s = 0; s < sides; s++)
        {
            int sn = (s + 1) % sides;
            int a = p * sides + s;
            int b = p * sides + sn;
            int c = (p + 1) * sides + sn;
            int d = (p + 1) * sides + s;
            AddQuad(t, a, b, c, d);
        }

        int bottomCenter = v.Count;
        v.Add(new Vector3(0f, profile[0].y, 0f)); uv.Add(new Vector2(0.5f, 0f));
        int topCenter = v.Count;
        v.Add(new Vector3(0f, profile[profile.Length - 1].y, 0f)); uv.Add(new Vector2(0.5f, 1f));
        for (int s = 0; s < sides; s++)
        {
            int sn = (s + 1) % sides;
            t.Add(bottomCenter); t.Add(sn); t.Add(s);
            int topRing = (profile.Length - 1) * sides;
            t.Add(topCenter); t.Add(topRing + s); t.Add(topRing + sn);
        }

        var mesh = new Mesh();
        mesh.SetVertices(v);
        mesh.SetTriangles(t, 0);
        mesh.SetUVs(0, uv);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static float Hermite(float t, float y0, float y1, float m0, float m1)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return (2f * t3 - 3f * t2 + 1f) * y0 +
               (t3 - 2f * t2 + t) * m0 +
               (-2f * t3 + 3f * t2) * y1 +
               (t3 - t2) * m1;
    }

    private static float HermiteDerivative(float t, float y0, float y1, float m0, float m1)
    {
        float t2 = t * t;
        return (6f * t2 - 6f * t) * y0 +
               (3f * t2 - 4f * t + 1f) * m0 +
               (-6f * t2 + 6f * t) * y1 +
               (3f * t2 - 2f * t) * m1;
    }

    private static void AddQuad(List<int> triangles, int a, int b, int c, int d)
    {
        triangles.Add(a); triangles.Add(b); triangles.Add(c);
        triangles.Add(a); triangles.Add(c); triangles.Add(d);
    }

    private static Material FindMaterialRecursive(Transform root, string materialName)
    {
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            foreach (Material m in r.sharedMaterials)
                if (m != null && m.name == materialName) return m;
        return AssetDatabase.LoadAssetAtPath<Material>($"Assets/Art/GeneratedParkFurnitureMaterials/{materialName}.mat");
    }

    private static void DisableChildRenderer(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        Renderer r = child != null ? child.GetComponent<Renderer>() : null;
        if (r != null) r.enabled = false;
    }

    private static void AssertLegacyRendererDisabled(Transform parent, string name, bool allowMissing = false)
    {
        Transform child = parent.Find(name);
        if (child == null)
        {
            if (allowMissing) return;
            throw new InvalidOperationException($"Expected base generated child missing: {parent.name}/{name}");
        }
        Renderer r = child.GetComponent<Renderer>();
        if (r != null && r.enabled)
            throw new InvalidOperationException($"Oversized first-pass geometry still rendered after physical refinement: {parent.name}/{name}");
    }

    private static void DestroyChildIfPresent(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null) UnityEngine.Object.DestroyImmediate(child.gameObject);
    }

    private static void EnsureMeshRoot()
    {
        if (!Directory.Exists(MeshRoot)) Directory.CreateDirectory(MeshRoot);
    }
}
