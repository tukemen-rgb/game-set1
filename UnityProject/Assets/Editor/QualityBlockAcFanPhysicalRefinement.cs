using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Reconstructs the generated outdoor-unit fan face as manufactured geometry rather than the legacy
/// solid cylinder + square bar lattice. The reconstruction is deliberately manufacturer-neutral:
/// a five-blade axial rotor sits behind a circular wire guard and a finite-depth molded inlet shroud.
/// It runs after the generic bevel pass and before Danchi LOD proxy generation, so the physical parts
/// participate in all four building LOD tiers instead of being a post-LOD overlay.
///
/// Source/scene QA awards zero Visual Fidelity points. Native 3840x2160 evidence remains mandatory.
/// </summary>
public static class QualityBlockAcFanPhysicalRefinement
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string DetailRootName = "DanchiHighDetail";
    private const string ContractPath = "Assets/QA/ac_outdoor_fan_physical_refinement_contract.json";
    private const string LookdevPath = "Assets/QA/Lookdev/ac_outdoor_fan_physical_refinement.svg";
    private const string MeshRoot = "Assets/Art/GeneratedDetailMeshes";
    private const string DarkPlasticPath = "Assets/Art/GeneratedDetailMaterials/MAT_ACFanDarkPlastic.mat";
    private const string AgedPlasticPath = "Assets/Art/GeneratedDetailMaterials/MAT_AgedACPlastic.mat";
    private const string GuardMaterialPath = "Assets/Art/GeneratedDetailMaterials/MAT_DarkGalvanizedSteel.mat";
    private const int ExpectedUnits = 15;

    private const float RotorRadius = 0.165f;
    private const float HubRadius = 0.036f;
    private const float ShroudOuterRadius = 0.205f;
    private const float ShroudInnerRadius = 0.181f;
    private const float ShroudDepth = 0.018f;
    private const float GuardWireRadius = 0.0025f;

    [MenuItem("NewTown/Geometry/Reconstruct Outdoor AC Axial Fan Faces")]
    public static void ApplyAndPersist()
    {
        RequireScene();
        ApplyToOpenScene();
        QualityBlockDanchiLodUpgrade.ApplyToOpenScene();
        ValidateOpenScene();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    public static void ApplyToOpenScene()
    {
        ValidateContractConfigOnly();
        RequireScene();
        GameObject detailRoot = FindSceneObject(DetailRootName);
        if (detailRoot == null) throw new InvalidOperationException("DanchiHighDetail is missing before AC fan refinement.");

        Material darkPlastic = GetOrCreateDarkFanPlastic();
        Material guardMaterial = AssetDatabase.LoadAssetAtPath<Material>(GuardMaterialPath);
        if (guardMaterial == null) throw new InvalidOperationException("AC fan guard material is missing: " + GuardMaterialPath);

        Mesh rotorMesh = SaveMesh("GM_ACFan_Rotor5Blade", BuildRotorMesh());
        Mesh guardMesh = SaveMesh("GM_ACFan_CircularWireGuard", BuildGuardMesh());
        Mesh shroudMesh = SaveMesh("GM_ACFan_InletShroud", BuildAnnulusMesh(ShroudOuterRadius, ShroudInnerRadius, ShroudDepth, 48));

        Transform[] bays = detailRoot.GetComponentsInChildren<Transform>(true)
            .Where(t => t.name.StartsWith("HD_BayAssembly_", StringComparison.Ordinal) && t.Find("HD_AC_FanHub") != null)
            .OrderBy(t => t.name, StringComparer.Ordinal)
            .ToArray();
        if (bays.Length != ExpectedUnits)
            throw new InvalidOperationException($"Expected {ExpectedUnits} generated AC bays, got {bays.Length}.");

        for (int ordinal = 0; ordinal < bays.Length; ordinal++)
        {
            Transform bay = bays[ordinal];
            Transform legacyDisc = bay.Find("HD_AC_FanDisc");
            Transform hub = bay.Find("HD_AC_FanHub");
            if (legacyDisc == null || hub == null)
                throw new InvalidOperationException($"{bay.name}: legacy fan disc/hub missing before physical refinement.");

            Vector3 center = legacyDisc.localPosition;
            RemoveExistingPhysicalParts(bay);
            UnityEngine.Object.DestroyImmediate(legacyDisc.gameObject);
            for (int i = -3; i <= 3; i++)
            {
                Transform h = bay.Find($"HD_AC_GrilleH_{i}");
                Transform v = bay.Find($"HD_AC_GrilleV_{i}");
                if (h != null) UnityEngine.Object.DestroyImmediate(h.gameObject);
                if (v != null) UnityEngine.Object.DestroyImmediate(v.gameObject);
            }

            hub.GetComponent<Renderer>().sharedMaterial = darkPlastic;
            Weather(hub.gameObject, NewTownSurfaceExposure.Sheltered | NewTownSurfaceExposure.Recessed,
                NewTownStainSource.RecessGrime | NewTownStainSource.UVExposure, 0.18f, 0.32f, 0f, 0f);

            // A stopped condenser does not present the same blade clock angle on every balcony.
            // Five deterministic angles break the immediately obvious repeated-module read without
            // changing manufactured dimensions or inventing damage.
            float clockAngle = (ordinal * 47f + (ordinal / 5) * 13f) % 360f;
            GameObject rotor = MeshPart("HD_AC_FanRotor", bay, center + new Vector3(0f, 0f, 0.004f), rotorMesh, darkPlastic);
            rotor.transform.localRotation = Quaternion.Euler(0f, 0f, clockAngle);
            Weather(rotor, NewTownSurfaceExposure.Sheltered | NewTownSurfaceExposure.Recessed,
                NewTownStainSource.RecessGrime | NewTownStainSource.UVExposure, 0.16f, 0.28f, 0f, 0f);

            GameObject shroud = MeshPart("HD_AC_FanShroud", bay, center + new Vector3(0f, 0f, 0.017f), shroudMesh, darkPlastic);
            Weather(shroud, NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.SunExposed | NewTownSurfaceExposure.Recessed,
                NewTownStainSource.UVExposure | NewTownStainSource.RecessGrime, 0.48f, 0.68f, 0f, 0f);

            GameObject guard = MeshPart("HD_AC_FanGuard", bay, center + new Vector3(0f, 0f, 0.034f), guardMesh, guardMaterial);
            Weather(guard, NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.SunExposed,
                NewTownStainSource.FerrousFixture | NewTownStainSource.UVExposure, 0.74f, 0.72f, 0f, 0f);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
    }

    [MenuItem("NewTown/QA/Validate Outdoor AC Axial Fan Physical Refinement")]
    public static void ValidateOpenScene()
    {
        ValidateContractConfigOnly();
        RequireScene();
        GameObject detailRoot = FindSceneObject(DetailRootName);
        if (detailRoot == null) throw new InvalidOperationException("DanchiHighDetail is missing.");

        var errors = new List<string>();
        ValidateMaterial(AssetDatabase.LoadAssetAtPath<Material>(DarkPlasticPath), false, errors, "dark ABS rotor/shroud");
        ValidateMaterial(AssetDatabase.LoadAssetAtPath<Material>(GuardMaterialPath), true, errors, "galvanized wire guard");

        Transform[] bays = detailRoot.GetComponentsInChildren<Transform>(true)
            .Where(t => t.name.StartsWith("HD_BayAssembly_", StringComparison.Ordinal) && t.Find("HD_AC_FanHub") != null)
            .ToArray();
        if (bays.Length != ExpectedUnits) errors.Add($"Expected {ExpectedUnits} generated AC bays, got {bays.Length}.");

        foreach (Transform bay in bays)
        {
            if (bay.Find("HD_AC_FanDisc") != null) errors.Add($"{bay.name}: legacy solid fan disc remains.");
            if (Enumerable.Range(-3, 7).Any(i => bay.Find($"HD_AC_GrilleH_{i}") != null || bay.Find($"HD_AC_GrilleV_{i}") != null))
                errors.Add($"{bay.name}: legacy square grille lattice remains.");

            ValidatePart(bay, "HD_AC_FanRotor", "GM_ACFan_Rotor5Blade", darkPlastic: true, errors);
            ValidatePart(bay, "HD_AC_FanShroud", "GM_ACFan_InletShroud", darkPlastic: true, errors);
            ValidatePart(bay, "HD_AC_FanGuard", "GM_ACFan_CircularWireGuard", darkPlastic: false, errors);
        }

        LODGroup group = detailRoot.GetComponent<LODGroup>();
        if (group == null) errors.Add("Danchi four-level LODGroup is missing after AC fan refinement.");
        else
        {
            LOD[] lods = group.GetLODs();
            if (lods.Length != 4) errors.Add($"Expected four Danchi LOD levels, got {lods.Length}.");
            else
            {
                RequireLodPrefixCount(lods[0], "HD_AC_FanRotor", ExpectedUnits, "LOD0 rotor", errors);
                RequireLodPrefixCount(lods[1], "HD_AC_FanRotor", ExpectedUnits, "LOD1 rotor", errors);
                RequireLodPrefixCount(lods[2], "HD_AC_FanRotor", ExpectedUnits, "LOD2 rotor", errors);
                RequireLodPrefixCount(lods[3], "HD_AC_FanRotor", 0, "LOD3 rotor", errors);
                RequireLodPrefixCount(lods[0], "HD_AC_FanGuard", ExpectedUnits, "LOD0 guard", errors);
                RequireLodPrefixCount(lods[1], "HD_AC_FanGuard", ExpectedUnits, "LOD1 guard", errors);
                RequireLodPrefixCount(lods[2], "HD_AC_FanGuard", 0, "LOD2 guard", errors);
                RequireLodPrefixCount(lods[3], "HD_AC_FanGuard", 0, "LOD3 guard", errors);
                for (int i = 0; i < 4; i++) RequireLodPrefixCount(lods[i], "HD_AC_FanShroud", ExpectedUnits, $"LOD{i} shroud", errors);
            }
        }

        if (errors.Count > 0)
            throw new InvalidOperationException("Outdoor AC fan physical refinement QA FAILED:\n - " + string.Join("\n - ", errors.Take(100)));
        Debug.Log("Outdoor AC fan physical refinement passed source invariants for 15 units. Native 4K balcony-services pixels remain mandatory; Visual Fidelity is UNSCORED.");
    }

    [MenuItem("NewTown/QA/Validate Outdoor AC Fan Refinement Contract")]
    public static void ValidateContractConfigOnly()
    {
        string absolute = Absolute(ContractPath);
        if (!File.Exists(absolute)) throw new InvalidOperationException("Missing AC fan construction contract: " + ContractPath);
        string json = File.ReadAllText(absolute);
        string[] required =
        {
            "\"schemaVersion\": \"1.0\"", "\"expectedGeneratedUnits\": 15",
            "\"bladeCount\": 5", "\"guardConcentricRingCount\": 4", "\"guardRadialSpokeCount\": 8",
            "\"rotorOuterRadiusMetres\": 0.165", "\"guardWireDiameterMetres\": 0.005",
            "\"levelsRequired\": 4", "\"paintedOrBakedHighlightsForbidden\": true",
            "\"automaticVisualScore\": 0", "\"runtimeRenderVerification\": \"PENDING_UNITY_RUNTIME\""
        };
        foreach (string token in required)
            if (!json.Contains(token)) throw new InvalidOperationException("AC fan contract missing token: " + token);
        if (!File.Exists(Absolute(LookdevPath))) throw new InvalidOperationException("AC fan lookdev illustration is missing: " + LookdevPath);
    }

    private static void ValidatePart(Transform bay, string name, string meshPrefix, bool darkPlastic, List<string> errors)
    {
        Transform t = bay.Find(name);
        if (t == null) { errors.Add($"{bay.name}: {name} missing."); return; }
        if (t.GetComponent<Collider>() != null) errors.Add($"{bay.name}/{name}: generated visual detail must remain collider-free.");
        MeshFilter f = t.GetComponent<MeshFilter>(); MeshRenderer r = t.GetComponent<MeshRenderer>();
        if (f?.sharedMesh == null || !f.sharedMesh.name.StartsWith(meshPrefix, StringComparison.Ordinal))
            errors.Add($"{bay.name}/{name}: physical authored mesh missing or wrong.");
        if (r == null || r.sharedMaterial == null) errors.Add($"{bay.name}/{name}: material missing.");
        else if (darkPlastic && AssetDatabase.GetAssetPath(r.sharedMaterial) != DarkPlasticPath)
            errors.Add($"{bay.name}/{name}: dark dielectric fan material not bound.");
        else if (!darkPlastic && AssetDatabase.GetAssetPath(r.sharedMaterial) != GuardMaterialPath)
            errors.Add($"{bay.name}/{name}: galvanized guard material not bound.");
        if (t.GetComponent<QualityBlockWeatheringSurface>() == null) errors.Add($"{bay.name}/{name}: cause-based weathering metadata missing.");
    }

    private static void ValidateMaterial(Material material, bool metallic, List<string> errors, string label)
    {
        if (material == null) { errors.Add(label + " material missing."); return; }
        float m = material.HasProperty("_Metallic") ? material.GetFloat("_Metallic") : -1f;
        float s = material.HasProperty("_Glossiness") ? material.GetFloat("_Glossiness") : -1f;
        if (!metallic && (m < 0f || m > 0.02f)) errors.Add($"{label}: metallic={m:0.###} must remain dielectric <=0.02.");
        if (metallic && m < 0.65f) errors.Add($"{label}: metallic={m:0.###} is too low for exposed galvanized steel.");
        if (s < 0.15f || s > 0.55f) errors.Add($"{label}: smoothness={s:0.###} outside conservative aged range.");
    }

    private static void RequireLodPrefixCount(LOD lod, string prefix, int expected, string label, List<string> errors)
    {
        int count = lod.renderers.Count(r => r != null && r.name.IndexOf(prefix, StringComparison.Ordinal) >= 0);
        if (count != expected) errors.Add($"{label}: expected {expected}, got {count}.");
    }

    private static Material GetOrCreateDarkFanPlastic()
    {
        Material source = AssetDatabase.LoadAssetAtPath<Material>(AgedPlasticPath);
        if (source == null) throw new InvalidOperationException("Aged AC plastic source material is missing: " + AgedPlasticPath);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(DarkPlasticPath);
        if (material == null)
        {
            material = new Material(source) { name = "MAT_ACFanDarkPlastic" };
            AssetDatabase.CreateAsset(material, DarkPlasticPath);
        }
        material.shader = source.shader;
        material.color = new Color(0.16f, 0.17f, 0.16f, 1f);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.32f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static GameObject MeshPart(string name, Transform parent, Vector3 localPosition, Mesh mesh, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        var filter = go.AddComponent<MeshFilter>(); filter.sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
        return go;
    }

    private static void RemoveExistingPhysicalParts(Transform bay)
    {
        foreach (string name in new[] { "HD_AC_FanRotor", "HD_AC_FanShroud", "HD_AC_FanGuard" })
        {
            Transform old = bay.Find(name);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
        }
    }

    private static void Weather(GameObject go, NewTownSurfaceExposure exposure, NewTownStainSource source, float rain, float sun, float splash, float contact)
    {
        var metadata = go.GetComponent<QualityBlockWeatheringSurface>() ?? go.AddComponent<QualityBlockWeatheringSurface>();
        metadata.Configure(exposure, source, rain, sun, splash, contact);
    }

    private static Mesh BuildRotorMesh()
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        const float halfDepth = 0.004f;
        for (int blade = 0; blade < 5; blade++)
        {
            float baseA = blade * 72f * Mathf.Deg2Rad;
            Vector2[] p =
            {
                Polar(HubRadius + 0.012f, baseA - 12f * Mathf.Deg2Rad),
                Polar(HubRadius + 0.020f, baseA + 18f * Mathf.Deg2Rad),
                Polar(RotorRadius, baseA + 31f * Mathf.Deg2Rad),
                Polar(RotorRadius - 0.018f, baseA - 2f * Mathf.Deg2Rad),
            };
            AddExtrudedPolygon(v, uv, tri, p, halfDepth);
        }
        return FinishMesh("GM_ACFan_Rotor5Blade", v, uv, tri);
    }

    private static Mesh BuildGuardMesh()
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        foreach (float radius in new[] { 0.052f, 0.096f, 0.140f, 0.184f }) AddTorus(v, uv, tri, radius, GuardWireRadius, 32, 6);
        for (int spoke = 0; spoke < 8; spoke++)
        {
            float a = spoke * 45f * Mathf.Deg2Rad;
            Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); Vector2 n = new Vector2(-d.y, d.x);
            float r0 = 0.030f, r1 = 0.188f, w = GuardWireRadius;
            Vector2[] p = { d * r0 + n * w, d * r1 + n * w, d * r1 - n * w, d * r0 - n * w };
            AddExtrudedPolygon(v, uv, tri, p, GuardWireRadius);
        }
        return FinishMesh("GM_ACFan_CircularWireGuard", v, uv, tri);
    }

    private static Mesh BuildAnnulusMesh(float outer, float inner, float depth, int segments)
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        float hz = depth * 0.5f;
        for (int i = 0; i < segments; i++)
        {
            int j = (i + 1) % segments; float a0 = i * Mathf.PI * 2f / segments, a1 = j * Mathf.PI * 2f / segments;
            Vector2 o0 = Polar(outer, a0), o1 = Polar(outer, a1), n0 = Polar(inner, a0), n1 = Polar(inner, a1);
            AddQuad(v, uv, tri, V(o0, hz), V(o1, hz), V(n1, hz), V(n0, hz));
            AddQuad(v, uv, tri, V(o1, -hz), V(o0, -hz), V(n0, -hz), V(n1, -hz));
            AddQuad(v, uv, tri, V(o0, -hz), V(o1, -hz), V(o1, hz), V(o0, hz));
            AddQuad(v, uv, tri, V(n1, -hz), V(n0, -hz), V(n0, hz), V(n1, hz));
        }
        return FinishMesh("GM_ACFan_InletShroud", v, uv, tri);
    }

    private static void AddTorus(List<Vector3> v, List<Vector2> uv, List<int> tri, float major, float tube, int majorSeg, int tubeSeg)
    {
        int start = v.Count;
        for (int i = 0; i < majorSeg; i++)
        {
            float a = i * Mathf.PI * 2f / majorSeg; Vector3 radial = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            for (int j = 0; j < tubeSeg; j++)
            {
                float b = j * Mathf.PI * 2f / tubeSeg;
                v.Add(radial * (major + Mathf.Cos(b) * tube) + Vector3.forward * (Mathf.Sin(b) * tube));
                uv.Add(new Vector2(i / (float)majorSeg, j / (float)tubeSeg));
            }
        }
        for (int i = 0; i < majorSeg; i++) for (int j = 0; j < tubeSeg; j++)
        {
            int ni = (i + 1) % majorSeg, nj = (j + 1) % tubeSeg;
            int a = start + i * tubeSeg + j, b = start + ni * tubeSeg + j, c = start + ni * tubeSeg + nj, d = start + i * tubeSeg + nj;
            tri.Add(a); tri.Add(b); tri.Add(c); tri.Add(a); tri.Add(c); tri.Add(d);
        }
    }

    private static void AddExtrudedPolygon(List<Vector3> v, List<Vector2> uv, List<int> tri, Vector2[] p, float halfDepth)
    {
        int front = v.Count;
        foreach (Vector2 q in p) { v.Add(V(q, halfDepth)); uv.Add(q * 3f + Vector2.one * 0.5f); }
        int back = v.Count;
        foreach (Vector2 q in p) { v.Add(V(q, -halfDepth)); uv.Add(q * 3f + Vector2.one * 0.5f); }
        for (int i = 1; i < p.Length - 1; i++) { tri.Add(front); tri.Add(front + i); tri.Add(front + i + 1); tri.Add(back); tri.Add(back + i + 1); tri.Add(back + i); }
        for (int i = 0; i < p.Length; i++)
        {
            int j = (i + 1) % p.Length;
            tri.Add(front + i); tri.Add(back + i); tri.Add(back + j); tri.Add(front + i); tri.Add(back + j); tri.Add(front + j);
        }
    }

    private static void AddQuad(List<Vector3> v, List<Vector2> uv, List<int> tri, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        int s = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(d);
        uv.Add(Vector2.zero); uv.Add(Vector2.right); uv.Add(Vector2.one); uv.Add(Vector2.up);
        tri.Add(s); tri.Add(s + 1); tri.Add(s + 2); tri.Add(s); tri.Add(s + 2); tri.Add(s + 3);
    }

    private static Mesh FinishMesh(string name, List<Vector3> vertices, List<Vector2> uvs, List<int> triangles)
    {
        var mesh = new Mesh { name = name };
        mesh.SetVertices(vertices); mesh.SetUVs(0, uvs); mesh.SetTriangles(triangles, 0, true);
        mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh SaveMesh(string name, Mesh generated)
    {
        Directory.CreateDirectory(MeshRoot); string path = $"{MeshRoot}/{name}.asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null) { AssetDatabase.CreateAsset(generated, path); return generated; }
        EditorUtility.CopySerialized(generated, existing); UnityEngine.Object.DestroyImmediate(generated); EditorUtility.SetDirty(existing); return existing;
    }

    private static Vector2 Polar(float r, float a) => new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
    private static Vector3 V(Vector2 p, float z) => new Vector3(p.x, p.y, z);

    private static void RequireScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static GameObject FindSceneObject(string name) => Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(x => x.scene.IsValid() && x.name == name);
    private static string Absolute(string assetPath) => Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath));
}
