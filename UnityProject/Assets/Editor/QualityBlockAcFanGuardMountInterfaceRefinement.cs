using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Adds a manufacturer-neutral physical mounting interface between the generated galvanized fan guard
/// and the molded condenser inlet shroud. The visible 5.5 mm axial air gap is intentional, but the guard
/// must not read as a floating wire object: four formed/welded steel mounting feet bridge that gap and
/// seat slightly into the shroud face. This pass runs after round-wire refinement and before Danchi LODs.
///
/// Geometry is conservative benchmark reconstruction, not a claim about a specific manufacturer/model.
/// No Visual Fidelity points are assigned without actual native-4K and temporal rendered evidence.
/// </summary>
public static class QualityBlockAcFanGuardMountInterfaceRefinement
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string DetailRootName = "DanchiHighDetail";
    private const string ContractPath = "Assets/QA/ac_outdoor_fan_physical_refinement_contract.json";
    private const string MountMeshPath = "Assets/Art/GeneratedDetailMeshes/GM_ACFan_GuardMountStandoffs.asset";
    private const string GuardMaterialPath = "Assets/Art/GeneratedDetailMaterials/MAT_DarkGalvanizedSteel.mat";
    // Deliberately does not contain the exact HD_AC_FanGuard prefix: the physical fan validator counts
    // that prefix as the wire guard itself. "Feet" still classifies this object into Danchi LOD1.
    private const string MountObjectName = "HD_AC_GuardMountFeet";

    private const int ExpectedUnits = 15;
    private const int MountCountPerUnit = 4;
    private const int MountRadialSegments = 16;
    private const float MountCenterRadius = 0.188f;
    private const float MountRadius = 0.0075f;
    private const float MountBackZ = -0.0085f;
    private const float MountFrontZ = 0.0025f;
    private const int ExpectedMountMeshVertices = 136;
    private const int ExpectedMountMeshTriangles = 256;

    public static void ApplyToOpenScene()
    {
        ValidateContractConfigOnly();
        RequireScene();

        GameObject detailRoot = FindSceneObject(DetailRootName);
        if (detailRoot == null)
            throw new InvalidOperationException("DanchiHighDetail is missing before AC guard mount-interface refinement.");

        Transform[] bays = FindAcBays(detailRoot);
        if (bays.Length != ExpectedUnits)
            throw new InvalidOperationException($"Expected {ExpectedUnits} generated AC bays, got {bays.Length}.");

        Material guardMaterial = AssetDatabase.LoadAssetAtPath<Material>(GuardMaterialPath);
        if (guardMaterial == null)
            throw new InvalidOperationException("Galvanized AC guard material is missing: " + GuardMaterialPath);

        Mesh mountsMesh = SaveMesh(BuildMountMesh());
        var meshErrors = new List<string>();
        ValidateMountMesh(mountsMesh, meshErrors);
        if (meshErrors.Count > 0)
            throw new InvalidOperationException("Generated AC guard mount mesh failed self-validation:\n - " + string.Join("\n - ", meshErrors));

        foreach (Transform bay in bays)
        {
            Transform guard = bay.Find("HD_AC_FanGuard");
            if (guard == null)
                throw new InvalidOperationException($"{bay.name}: round-wire guard missing before mount-interface refinement.");

            Transform old = bay.Find(MountObjectName);
            if (old != null)
                UnityEngine.Object.DestroyImmediate(old.gameObject);

            var go = new GameObject(MountObjectName);
            go.transform.SetParent(bay, false);
            go.transform.localPosition = guard.localPosition;
            go.transform.localRotation = guard.localRotation;

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mountsMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = guardMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;

            var weather = go.AddComponent<QualityBlockWeatheringSurface>();
            weather.Configure(
                NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.SunExposed,
                NewTownStainSource.FerrousFixture | NewTownStainSource.UVExposure,
                0.72f,
                0.70f,
                0f,
                0f);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
    }

    [MenuItem("NewTown/QA/Validate Outdoor AC Guard Mount Interface")]
    public static void ValidateOpenScene()
    {
        ValidateContractConfigOnly();
        RequireScene();

        GameObject detailRoot = FindSceneObject(DetailRootName);
        if (detailRoot == null)
            throw new InvalidOperationException("DanchiHighDetail is missing.");

        var errors = new List<string>();
        Mesh canonicalMounts = AssetDatabase.LoadAssetAtPath<Mesh>(MountMeshPath);
        if (canonicalMounts == null)
            errors.Add("Canonical AC guard mount mesh is missing: " + MountMeshPath);
        else
            ValidateMountMesh(canonicalMounts, errors);

        Transform[] bays = FindAcBays(detailRoot);
        if (bays.Length != ExpectedUnits)
            errors.Add($"Expected {ExpectedUnits} generated AC bays, got {bays.Length}.");

        foreach (Transform bay in bays)
        {
            Transform guard = bay.Find("HD_AC_FanGuard");
            Transform shroud = bay.Find("HD_AC_FanShroud");
            Transform mounts = bay.Find(MountObjectName);
            if (guard == null || shroud == null || mounts == null)
            {
                errors.Add($"{bay.name}: guard/shroud/mount interface is incomplete.");
                continue;
            }

            if ((mounts.localPosition - guard.localPosition).sqrMagnitude > 0.00000001f ||
                Quaternion.Angle(mounts.localRotation, guard.localRotation) > 0.01f)
                errors.Add($"{bay.name}: guard mounts drifted from the guard datum.");

            MeshFilter mountFilter = mounts.GetComponent<MeshFilter>();
            MeshRenderer mountRenderer = mounts.GetComponent<MeshRenderer>();
            MeshFilter shroudFilter = shroud.GetComponent<MeshFilter>();
            if (mountFilter?.sharedMesh == null || shroudFilter?.sharedMesh == null)
            {
                errors.Add($"{bay.name}: mount/shroud mesh missing for seating validation.");
                continue;
            }
            if (AssetDatabase.GetAssetPath(mountFilter.sharedMesh) != MountMeshPath)
                errors.Add($"{bay.name}: mount interface is not bound to the canonical standoff mesh.");
            if (mountRenderer?.sharedMaterial == null || AssetDatabase.GetAssetPath(mountRenderer.sharedMaterial) != GuardMaterialPath)
                errors.Add($"{bay.name}: mount interface lost galvanized-steel material binding.");
            if (mounts.GetComponent<QualityBlockWeatheringSurface>() == null)
                errors.Add($"{bay.name}: mount interface lost cause-based weathering metadata.");
            if (mounts.GetComponent<Collider>() != null)
                errors.Add($"{bay.name}: generated mount interface must remain collider-free.");

            float mountBack = mounts.localPosition.z + mountFilter.sharedMesh.bounds.min.z;
            float mountFront = mounts.localPosition.z + mountFilter.sharedMesh.bounds.max.z;
            float shroudFront = shroud.localPosition.z + shroudFilter.sharedMesh.bounds.max.z;
            float seatEmbed = shroudFront - mountBack;
            float guardFront = guard.localPosition.z + 0.0025f;
            if (seatEmbed < 0f || seatEmbed > 0.0015f)
                errors.Add($"{bay.name}: mounting feet do not physically seat into shroud face; controlled embed={seatEmbed:F4} m.");
            if (mountFront + 0.0001f < guardFront)
                errors.Add($"{bay.name}: mounting feet do not reach the round-wire guard plane.");
        }

        LODGroup group = detailRoot.GetComponent<LODGroup>();
        if (group == null)
            errors.Add("Danchi LODGroup is missing while validating AC guard mounts.");
        else
        {
            LOD[] lods = group.GetLODs();
            if (lods.Length != 4)
                errors.Add($"Expected four Danchi LOD levels, got {lods.Length}.");
            else
            {
                ValidateLodMounts(lods[0], ExpectedUnits, "LOD0", errors);
                ValidateLodMounts(lods[1], ExpectedUnits, "LOD1", errors);
                ValidateLodMounts(lods[2], 0, "LOD2", errors);
                ValidateLodMounts(lods[3], 0, "LOD3", errors);
            }
        }

        if (errors.Count > 0)
            throw new InvalidOperationException("Outdoor AC guard mount-interface QA FAILED:\n - " + string.Join("\n - ", errors.Take(100)));

        Debug.Log(
            "Outdoor AC guard mounting interface passed source invariants: four galvanized standoff feet per unit bridge the guard/shroud air gap, " +
            "seat into the shroud face, carry weathering metadata, and remain LOD0/1 only. Native 4K evidence remains mandatory; Visual Fidelity is UNSCORED.");
    }

    [MenuItem("NewTown/QA/Validate Outdoor AC Guard Mount Contract Tokens")]
    public static void ValidateContractConfigOnly()
    {
        string absolute = Absolute(ContractPath);
        if (!File.Exists(absolute))
            throw new InvalidOperationException("Missing AC fan construction contract: " + ContractPath);
        string json = File.ReadAllText(absolute);
        string[] required =
        {
            "\"mountInterfaceRevision\": \"mount-2\"",
            "\"mountCountPerUnit\": 4",
            "\"mountCenterRadiusMetres\": 0.188",
            "\"mountRadiusMetres\": 0.0075",
            "\"mountBackFromGuardPlaneMetres\": -0.0085",
            "\"mountFrontFromGuardPlaneMetres\": 0.0025",
            "\"mountRadialSegments\": 16",
            "\"expectedMountMeshVertices\": 136",
            "\"expectedMountMeshTriangles\": 256",
            "\"mountsMustSeatIntoShroudFace\": true",
            "\"automaticVisualScore\": 0"
        };
        foreach (string token in required)
            if (!json.Contains(token))
                throw new InvalidOperationException("AC fan mount-interface contract missing token: " + token);
    }

    private static Transform[] FindAcBays(GameObject detailRoot)
    {
        return detailRoot.GetComponentsInChildren<Transform>(true)
            .Where(t => t.name.StartsWith("HD_BayAssembly_", StringComparison.Ordinal) && t.Find("HD_AC_FanHub") != null)
            .OrderBy(t => t.name, StringComparer.Ordinal)
            .ToArray();
    }

    private static void ValidateLodMounts(LOD lod, int expected, string label, List<string> errors)
    {
        Renderer[] mounts = lod.renderers
            .Where(r => r != null && r.name.IndexOf(MountObjectName, StringComparison.Ordinal) >= 0)
            .ToArray();
        if (mounts.Length != expected)
        {
            errors.Add($"{label} AC guard mount renderer count: expected {expected}, got {mounts.Length}.");
            return;
        }
        foreach (Renderer renderer in mounts)
        {
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter?.sharedMesh == null || AssetDatabase.GetAssetPath(filter.sharedMesh) != MountMeshPath)
                errors.Add($"{label}/{renderer.name}: guard mounts do not share canonical mesh.");
        }
    }

    private static void ValidateMountMesh(Mesh mesh, List<string> errors)
    {
        if (mesh.vertexCount != ExpectedMountMeshVertices)
            errors.Add($"AC guard mount mesh vertex count drifted: expected {ExpectedMountMeshVertices}, got {mesh.vertexCount}.");
        int triangles = mesh.triangles.Length / 3;
        if (triangles != ExpectedMountMeshTriangles)
            errors.Add($"AC guard mount mesh triangle count drifted: expected {ExpectedMountMeshTriangles}, got {triangles}.");
        if (mesh.uv == null || mesh.uv.Length != mesh.vertexCount)
            errors.Add("AC guard mount mesh UV0 coverage is incomplete.");
        if (mesh.normals == null || mesh.normals.Length != mesh.vertexCount)
            errors.Add("AC guard mount mesh normals are incomplete.");
        if (mesh.tangents == null || mesh.tangents.Length != mesh.vertexCount)
            errors.Add("AC guard mount mesh tangents are incomplete.");
        if (Mathf.Abs(mesh.bounds.min.z - MountBackZ) > 0.0002f || Mathf.Abs(mesh.bounds.max.z - MountFrontZ) > 0.0002f)
            errors.Add($"AC guard mount axial envelope drifted: [{mesh.bounds.min.z:F4},{mesh.bounds.max.z:F4}] m.");
    }

    private static Mesh BuildMountMesh()
    {
        var vertices = new List<Vector3>(ExpectedMountMeshVertices);
        var uvs = new List<Vector2>(ExpectedMountMeshVertices);
        var triangles = new List<int>(ExpectedMountMeshTriangles * 3);

        for (int mount = 0; mount < MountCountPerUnit; mount++)
        {
            float angle = (45f + mount * 90f) * Mathf.Deg2Rad;
            Vector2 center = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * MountCenterRadius;
            AddAxialCylinder(vertices, uvs, triangles, center, MountRadius, MountBackZ, MountFrontZ, MountRadialSegments);
        }

        var mesh = new Mesh { name = "GM_ACFan_GuardMountStandoffs" };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0, true);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddAxialCylinder(
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles,
        Vector2 center,
        float radius,
        float zBack,
        float zFront,
        int segments)
    {
        int start = vertices.Count;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector2 radial = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            vertices.Add(new Vector3(center.x + radial.x, center.y + radial.y, zBack));
            uvs.Add(new Vector2(i / (float)segments, 0f));
            vertices.Add(new Vector3(center.x + radial.x, center.y + radial.y, zFront));
            uvs.Add(new Vector2(i / (float)segments, 1f));
        }

        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            int a = start + i * 2;
            int b = a + 1;
            int c = start + next * 2 + 1;
            int d = start + next * 2;
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
            triangles.Add(a); triangles.Add(c); triangles.Add(d);
        }

        int backCenter = vertices.Count;
        vertices.Add(new Vector3(center.x, center.y, zBack));
        uvs.Add(new Vector2(0.5f, 0.5f));
        int frontCenter = vertices.Count;
        vertices.Add(new Vector3(center.x, center.y, zFront));
        uvs.Add(new Vector2(0.5f, 0.5f));
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            int backCurrent = start + i * 2;
            int backNext = start + next * 2;
            int frontCurrent = backCurrent + 1;
            int frontNext = backNext + 1;
            triangles.Add(backCenter); triangles.Add(backCurrent); triangles.Add(backNext);
            triangles.Add(frontCenter); triangles.Add(frontNext); triangles.Add(frontCurrent);
        }
    }

    private static Mesh SaveMesh(Mesh generated)
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(MountMeshPath);
        if (existing == null)
        {
            string directory = Path.GetDirectoryName(MountMeshPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            AssetDatabase.CreateAsset(generated, MountMeshPath);
            return generated;
        }
        EditorUtility.CopySerialized(generated, existing);
        UnityEngine.Object.DestroyImmediate(generated);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    private static void RequireScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x.scene.IsValid() && x.name == name);
    }

    private static string Absolute(string assetPath)
    {
        return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath));
    }
}
