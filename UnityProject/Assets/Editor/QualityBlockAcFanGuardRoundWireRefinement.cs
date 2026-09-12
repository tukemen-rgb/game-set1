using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Refines the generated outdoor-condenser guard into a true round-wire assembly.
/// The original physical fan pass already replaces the legacy opaque disc and square lattice, but its
/// radial spokes were finite-thickness rectangular strips while the construction contract described
/// round wire. This pass closes that geometry/material-response mismatch before Danchi LOD proxies are
/// generated, so grazing highlights and silhouette response come from circular cross-sections at LOD0/1.
///
/// This is source/readiness work only. It awards zero Visual Fidelity points until native 3840x2160
/// balcony-services pixels and temporal evidence are actually rendered and reviewed.
/// </summary>
public static class QualityBlockAcFanGuardRoundWireRefinement
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string DetailRootName = "DanchiHighDetail";
    private const string ContractPath = "Assets/QA/ac_outdoor_fan_physical_refinement_contract.json";
    private const string GuardMeshPath = "Assets/Art/GeneratedDetailMeshes/GM_ACFan_CircularWireGuard.asset";
    private const int ExpectedUnits = 15;

    private const float GuardWireRadius = 0.0025f;
    private const int RingMajorSegments = 32;
    private const int WireCrossSectionSegments = 8;
    private const int ExpectedGuardVertices = 1168;
    private const int ExpectedGuardTriangles = 2304;

    private static readonly float[] RingRadii = { 0.052f, 0.096f, 0.140f, 0.184f };

    [MenuItem("NewTown/Geometry/Refine Outdoor AC Guard To Round Wire")]
    public static void ApplyAndPersist()
    {
        RequireScene();
        ApplyToOpenScene();
        QualityBlockDanchiLodUpgrade.ApplyToOpenScene();
        QualityBlockDanchiLodUpgrade.ValidateOpenScene();
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
        if (detailRoot == null)
            throw new InvalidOperationException("DanchiHighDetail is missing before AC guard round-wire refinement.");

        Transform[] bays = FindAcBays(detailRoot);
        if (bays.Length != ExpectedUnits)
            throw new InvalidOperationException($"Expected {ExpectedUnits} generated AC bays, got {bays.Length}.");

        Mesh refined = SaveMesh(BuildRoundWireGuardMesh());
        var meshErrors = new List<string>();
        ValidateGuardMesh(refined, meshErrors);
        if (meshErrors.Count > 0)
            throw new InvalidOperationException("Generated round-wire guard mesh failed self-validation:\n - " + string.Join("\n - ", meshErrors));

        foreach (Transform bay in bays)
        {
            Transform guard = bay.Find("HD_AC_FanGuard");
            if (guard == null)
                throw new InvalidOperationException($"{bay.name}: physical AC guard is missing before round-wire refinement.");
            MeshFilter filter = guard.GetComponent<MeshFilter>();
            if (filter == null)
                throw new InvalidOperationException($"{bay.name}/HD_AC_FanGuard: MeshFilter missing.");
            filter.sharedMesh = refined;
            EditorUtility.SetDirty(filter);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
    }

    [MenuItem("NewTown/QA/Validate Outdoor AC Guard Round-Wire Refinement")]
    public static void ValidateOpenScene()
    {
        ValidateContractConfigOnly();
        RequireScene();

        GameObject detailRoot = FindSceneObject(DetailRootName);
        if (detailRoot == null)
            throw new InvalidOperationException("DanchiHighDetail is missing.");

        var errors = new List<string>();
        Transform[] bays = FindAcBays(detailRoot);
        if (bays.Length != ExpectedUnits)
            errors.Add($"Expected {ExpectedUnits} generated AC bays, got {bays.Length}.");

        Mesh canonicalGuard = AssetDatabase.LoadAssetAtPath<Mesh>(GuardMeshPath);
        if (canonicalGuard == null)
            errors.Add("Canonical refined AC guard mesh asset is missing: " + GuardMeshPath);
        else
            ValidateGuardMesh(canonicalGuard, errors);

        foreach (Transform bay in bays)
        {
            Transform rotor = bay.Find("HD_AC_FanRotor");
            Transform shroud = bay.Find("HD_AC_FanShroud");
            Transform guard = bay.Find("HD_AC_FanGuard");
            if (rotor == null || shroud == null || guard == null)
            {
                errors.Add($"{bay.name}: rotor/shroud/guard construction stack is incomplete.");
                continue;
            }

            MeshFilter rotorFilter = rotor.GetComponent<MeshFilter>();
            MeshFilter shroudFilter = shroud.GetComponent<MeshFilter>();
            MeshFilter guardFilter = guard.GetComponent<MeshFilter>();
            if (guardFilter?.sharedMesh == null)
            {
                errors.Add($"{bay.name}: refined guard mesh is missing.");
                continue;
            }
            if (AssetDatabase.GetAssetPath(guardFilter.sharedMesh) != GuardMeshPath)
                errors.Add($"{bay.name}: guard is not bound to the canonical round-wire mesh asset.");

            if (rotorFilter?.sharedMesh == null || shroudFilter?.sharedMesh == null)
            {
                errors.Add($"{bay.name}: rotor/shroud mesh missing for axial-clearance validation.");
                continue;
            }

            float guardBack = guard.localPosition.z + guardFilter.sharedMesh.bounds.min.z;
            float rotorFront = rotor.localPosition.z + rotorFilter.sharedMesh.bounds.max.z;
            float shroudFront = shroud.localPosition.z + shroudFilter.sharedMesh.bounds.max.z;
            float rotorClearance = guardBack - rotorFront;
            float shroudClearance = guardBack - shroudFront;
            if (rotorClearance < 0.015f)
                errors.Add($"{bay.name}: rotor-to-guard axial clearance {rotorClearance:F4} m is below 15 mm; hero interpenetration risk.");
            if (shroudClearance < 0.003f)
                errors.Add($"{bay.name}: shroud-to-guard axial clearance {shroudClearance:F4} m is below 3 mm.");

            if (guard.GetComponent<QualityBlockWeatheringSurface>() == null)
                errors.Add($"{bay.name}: refined guard lost cause-based weathering metadata.");
        }

        LODGroup group = detailRoot.GetComponent<LODGroup>();
        if (group == null)
            errors.Add("Danchi LODGroup is missing while validating AC guard continuity.");
        else
        {
            LOD[] lods = group.GetLODs();
            if (lods.Length != 4)
                errors.Add($"Expected four Danchi LOD levels, got {lods.Length}.");
            else
            {
                ValidateLodGuardMesh(lods[0], ExpectedUnits, "LOD0", errors);
                ValidateLodGuardMesh(lods[1], ExpectedUnits, "LOD1", errors);
                ValidateLodGuardMesh(lods[2], 0, "LOD2", errors);
                ValidateLodGuardMesh(lods[3], 0, "LOD3", errors);
            }
        }

        if (errors.Count > 0)
            throw new InvalidOperationException("Outdoor AC guard round-wire QA FAILED:\n - " + string.Join("\n - ", errors.Take(100)));

        Debug.Log(
            "Outdoor AC guard round-wire refinement passed source invariants: 15 guards use a 5 mm circular cross-section, " +
            "round radial spokes, finite axial clearances, and LOD0/1-only wire retention. Native 4K/temporal pixels remain mandatory; Visual Fidelity is UNSCORED.");
    }

    [MenuItem("NewTown/QA/Validate Outdoor AC Round-Wire Contract Tokens")]
    public static void ValidateContractConfigOnly()
    {
        string absolute = Absolute(ContractPath);
        if (!File.Exists(absolute))
            throw new InvalidOperationException("Missing AC fan construction contract: " + ContractPath);
        string json = File.ReadAllText(absolute);
        string[] required =
        {
            "\"schemaVersion\": \"1.0\"",
            "\"refinementRevision\": \"round-wire-1\"",
            "\"expectedGeneratedUnits\": 15",
            "\"guardConcentricRingCount\": 4",
            "\"guardRadialSpokeCount\": 8",
            "\"guardWireDiameterMetres\": 0.005",
            "\"guardRingMajorSegments\": 32",
            "\"guardWireCrossSectionSegments\": 8",
            "\"radialSpokeCrossSection\": \"round\"",
            "\"expectedGuardVertices\": 1168",
            "\"expectedGuardTriangles\": 2304",
            "\"minimumRotorToGuardAxialClearanceMetres\": 0.015",
            "\"minimumShroudToGuardAxialClearanceMetres\": 0.003",
            "\"automaticVisualScore\": 0",
            "\"runtimeRenderVerification\": \"PENDING_UNITY_RUNTIME\""
        };
        foreach (string token in required)
            if (!json.Contains(token))
                throw new InvalidOperationException("AC fan round-wire contract missing token: " + token);
    }

    private static Transform[] FindAcBays(GameObject detailRoot)
    {
        return detailRoot.GetComponentsInChildren<Transform>(true)
            .Where(t => t.name.StartsWith("HD_BayAssembly_", StringComparison.Ordinal) && t.Find("HD_AC_FanHub") != null)
            .OrderBy(t => t.name, StringComparer.Ordinal)
            .ToArray();
    }

    private static void ValidateGuardMesh(Mesh mesh, List<string> errors)
    {
        if (mesh == null)
        {
            errors.Add("Refined guard mesh is null.");
            return;
        }
        if (mesh.vertexCount != ExpectedGuardVertices)
            errors.Add($"Refined guard vertex count drifted: expected {ExpectedGuardVertices}, got {mesh.vertexCount}.");
        int triangleCount = mesh.triangles.Length / 3;
        if (triangleCount != ExpectedGuardTriangles)
            errors.Add($"Refined guard triangle count drifted: expected {ExpectedGuardTriangles}, got {triangleCount}.");
        if (mesh.uv == null || mesh.uv.Length != mesh.vertexCount)
            errors.Add("Refined guard UV0 coverage is incomplete.");
        if (mesh.normals == null || mesh.normals.Length != mesh.vertexCount)
            errors.Add("Refined guard normals are incomplete.");
        if (mesh.tangents == null || mesh.tangents.Length != mesh.vertexCount)
            errors.Add("Refined guard tangents are incomplete.");

        Vector3 size = mesh.bounds.size;
        if (Mathf.Abs(size.x - 0.376f) > 0.0035f || Mathf.Abs(size.y - 0.376f) > 0.0035f)
            errors.Add($"Refined guard radial envelope drifted: bounds {size.x:F4} x {size.y:F4} m.");
        if (Mathf.Abs(size.z - 0.005f) > 0.0006f)
            errors.Add($"Refined guard wire depth drifted: bounds.z={size.z:F4} m; expected about 5 mm.");
    }

    private static void ValidateLodGuardMesh(LOD lod, int expected, string label, List<string> errors)
    {
        Renderer[] guards = lod.renderers
            .Where(r => r != null && r.name.IndexOf("HD_AC_FanGuard", StringComparison.Ordinal) >= 0)
            .ToArray();
        if (guards.Length != expected)
        {
            errors.Add($"{label} refined guard renderer count: expected {expected}, got {guards.Length}.");
            return;
        }
        foreach (Renderer renderer in guards)
        {
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter?.sharedMesh == null || AssetDatabase.GetAssetPath(filter.sharedMesh) != GuardMeshPath)
                errors.Add($"{label}/{renderer.name}: LOD guard does not share the canonical round-wire mesh.");
        }
    }

    private static Mesh BuildRoundWireGuardMesh()
    {
        var vertices = new List<Vector3>(ExpectedGuardVertices);
        var uvs = new List<Vector2>(ExpectedGuardVertices);
        var triangles = new List<int>(ExpectedGuardTriangles * 3);

        foreach (float radius in RingRadii)
            AddTorus(vertices, uvs, triangles, radius, GuardWireRadius, RingMajorSegments, WireCrossSectionSegments);

        for (int spoke = 0; spoke < 8; spoke++)
        {
            float angle = spoke * 45f * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            AddRoundWireSegment(
                vertices,
                uvs,
                triangles,
                direction * 0.030f,
                direction * 0.188f,
                GuardWireRadius,
                WireCrossSectionSegments);
        }

        var mesh = new Mesh { name = "GM_ACFan_CircularWireGuard" };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0, true);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddTorus(
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles,
        float majorRadius,
        float tubeRadius,
        int majorSegments,
        int tubeSegments)
    {
        int start = vertices.Count;
        for (int i = 0; i < majorSegments; i++)
        {
            float a = i * Mathf.PI * 2f / majorSegments;
            Vector3 radial = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            for (int j = 0; j < tubeSegments; j++)
            {
                float b = j * Mathf.PI * 2f / tubeSegments;
                vertices.Add(radial * (majorRadius + Mathf.Cos(b) * tubeRadius) + Vector3.forward * (Mathf.Sin(b) * tubeRadius));
                uvs.Add(new Vector2(i / (float)majorSegments, j / (float)tubeSegments));
            }
        }

        for (int i = 0; i < majorSegments; i++)
        {
            int nextI = (i + 1) % majorSegments;
            for (int j = 0; j < tubeSegments; j++)
            {
                int nextJ = (j + 1) % tubeSegments;
                int a = start + i * tubeSegments + j;
                int b = start + nextI * tubeSegments + j;
                int c = start + nextI * tubeSegments + nextJ;
                int d = start + i * tubeSegments + nextJ;
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
                triangles.Add(a); triangles.Add(c); triangles.Add(d);
            }
        }
    }

    private static void AddRoundWireSegment(
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles,
        Vector2 start2,
        Vector2 end2,
        float radius,
        int radialSegments)
    {
        Vector3 startPoint = new Vector3(start2.x, start2.y, 0f);
        Vector3 endPoint = new Vector3(end2.x, end2.y, 0f);
        Vector3 axis = (endPoint - startPoint).normalized;
        Vector3 basisA = Vector3.forward;
        Vector3 basisB = Vector3.Cross(axis, basisA).normalized;
        int start = vertices.Count;

        for (int side = 0; side < radialSegments; side++)
        {
            float theta = side * Mathf.PI * 2f / radialSegments;
            Vector3 offset = (basisA * Mathf.Cos(theta) + basisB * Mathf.Sin(theta)) * radius;
            vertices.Add(startPoint + offset);
            uvs.Add(new Vector2(0f, side / (float)radialSegments));
            vertices.Add(endPoint + offset);
            uvs.Add(new Vector2(1f, side / (float)radialSegments));
        }

        for (int side = 0; side < radialSegments; side++)
        {
            int next = (side + 1) % radialSegments;
            int a = start + side * 2;
            int b = a + 1;
            int c = start + next * 2 + 1;
            int d = start + next * 2;
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
            triangles.Add(a); triangles.Add(c); triangles.Add(d);
        }

        int startCap = vertices.Count;
        vertices.Add(startPoint);
        uvs.Add(new Vector2(0f, 0.5f));
        int endCap = vertices.Count;
        vertices.Add(endPoint);
        uvs.Add(new Vector2(1f, 0.5f));

        for (int side = 0; side < radialSegments; side++)
        {
            int next = (side + 1) % radialSegments;
            int startCurrent = start + side * 2;
            int startNext = start + next * 2;
            int endCurrent = startCurrent + 1;
            int endNext = startNext + 1;
            triangles.Add(startCap); triangles.Add(startNext); triangles.Add(startCurrent);
            triangles.Add(endCap); triangles.Add(endCurrent); triangles.Add(endNext);
        }
    }

    private static Mesh SaveMesh(Mesh generated)
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(GuardMeshPath);
        if (existing == null)
        {
            string directory = Path.GetDirectoryName(GuardMeshPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            AssetDatabase.CreateAsset(generated, GuardMeshPath);
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
