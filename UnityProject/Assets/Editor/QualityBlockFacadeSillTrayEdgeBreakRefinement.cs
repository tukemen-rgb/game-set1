using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Replaces the razor-sharp fallback sill-tray front corners with a small physical extrusion-style
/// edge break, then rebuilds the already-referenced combined LOD proxy meshes from the same LOD0
/// geometry. The refinement is armed once per Editor session before formal reflection evidence and
/// becomes strictly read-only afterward.
///
/// The 2 mm break is a conservative benchmark reconstruction assumption, not a dimension attributed
/// to an identified historic sash product. It exists to avoid mathematically sharp CG highlights while
/// preserving the established sill envelope, drain openings and lower-frame interface.
///
/// This is implementation-readiness work only. It awards zero Visual Fidelity points; native 4K
/// frontal/oblique/grazing pixels and temporal evidence remain authoritative.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockFacadeSillTrayEdgeBreakRefinement
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string SillRootName = "FacadeSillDrainageInterfaces";
    private const string ContractPath = "Assets/QA/facade_sill_tray_edge_break_contract.json";
    private const string LookdevPath = "Assets/QA/lookdev/facade_sill_tray_edge_break_lookdev.svg";
    private const string SourcePath = "Assets/Editor/QualityBlockFacadeSillTrayEdgeBreakRefinement.cs";
    private const string MeshRoot = "Assets/Art/GeneratedFacadeHardwareMeshes/SillDrainage";

    private const int ExpectedWindowCount = 30;
    private const int PhaseBins = 8;
    private const float PhaseStepMeters = 0.017f;
    private const float SillWidth = 2.460f;
    private const float TrayBackZ = -0.100f;
    private const float TrayFrontZ = 0.095f;
    private const float TrayTopBackY = 0.023f;
    private const float TrayTopFrontY = 0.015f;
    private const float TrayBottomBackY = 0.003f;
    private const float TrayBottomFrontY = -0.005f;
    private const float EdgeBreak = 0.002f;
    private const float OriginY = -0.895f;
    private const float OriginZ = -7.235f;
    private const int RefinedTrayVertexCount = 60;
    private const int RefinedTrayTriangleIndexCount = 72;

    private const string SessionPrefix = "QualityBlock.FacadeSillTrayEdgeBreak.";
    private const string EpochArmedKey = SessionPrefix + "EpochArmed";
    private const string AuthoredEpochKey = SessionPrefix + "AuthoredEpoch";
    private const string FingerprintKey = SessionPrefix + "Fingerprint";

    private static bool EpochArmed => SessionState.GetBool(EpochArmedKey, false);
    private static bool AuthoredEpoch => SessionState.GetBool(AuthoredEpochKey, false);
    private static string ArmedFingerprint => SessionState.GetString(FingerprintKey, string.Empty);

    static QualityBlockFacadeSillTrayEdgeBreakRefinement()
    {
        Camera.onPreCull -= OnCameraPreCull;
        Camera.onPreCull += OnCameraPreCull;
    }

    [MenuItem("NewTown/QA/Validate Facade Sill Tray Edge-Break Contract")]
    public static void ValidateContractConfigOnly()
    {
        EdgeBreakContract contract = LoadJson<EdgeBreakContract>(ContractPath);
        if (contract == null || contract.geometry == null || contract.material == null || contract.qa == null)
            throw new InvalidOperationException("Facade sill tray edge-break contract is null or incomplete.");

        var errors = new List<string>();
        Require(contract.schemaVersion == "1.0", "schemaVersion must be 1.0", errors);
        Require(contract.assemblyId == "apartment_sliding_sash_sill_tray_front_edge_break",
            "assemblyId mismatch", errors);
        RequireNear(contract.geometry.edgeBreakM, EdgeBreak, 0.00001f, "geometry.edgeBreakM", errors);
        RequireNear(contract.geometry.sillWidthM, SillWidth, 0.0001f, "geometry.sillWidthM", errors);
        Require(contract.geometry.profilePointCount == 6, "geometry.profilePointCount must be 6", errors);
        Require(contract.geometry.preserveOuterEnvelope, "outer sill envelope must be preserved", errors);
        Require(contract.geometry.preserveDrainOpeningTopology, "drain opening topology must be preserved", errors);
        Require(contract.material.inheritCanonicalAgedAluminum,
            "edge break must inherit canonical aged aluminum", errors);
        Require(contract.material.wetness == 0f, "dry benchmark wetness must remain zero", errors);
        Require(!string.IsNullOrWhiteSpace(contract.material.albedo), "material albedo metadata missing", errors);
        Require(!string.IsNullOrWhiteSpace(contract.material.roughness), "material roughness metadata missing", errors);
        Require(!string.IsNullOrWhiteSpace(contract.material.metallicSpecular), "material metallic/specular metadata missing", errors);
        Require(!string.IsNullOrWhiteSpace(contract.material.normalScale), "material normal metadata missing", errors);
        Require(!string.IsNullOrWhiteSpace(contract.material.microstructure), "material microstructure metadata missing", errors);
        Require(!string.IsNullOrWhiteSpace(contract.material.uvAging), "material UV-aging metadata missing", errors);
        Require(!string.IsNullOrWhiteSpace(contract.material.angularFresnelResponse), "material Fresnel metadata missing", errors);
        Require(contract.qa.phaseBins == PhaseBins, "qa.phaseBins mismatch", errors);
        RequireNear(contract.qa.phaseStepM, PhaseStepMeters, 0.0001f, "qa.phaseStepM", errors);
        Require(contract.qa.rebuildCombinedLodProxyFromRefinedLod0,
            "combined LOD proxies must be rebuilt from refined LOD0", errors);
        Require(contract.qa.freezeMeshFingerprintAfterFirstReflectionBaseline,
            "mesh fingerprint must freeze after the first reflection baseline", errors);
        Require(contract.qa.postArmValidationReadOnly,
            "post-arm validation must remain read-only", errors);
        Require(contract.qa.automaticVisualPoints == 0,
            "automaticVisualPoints must remain zero", errors);
        Require(contract.qa.renderVerificationPending,
            "render verification must remain pending until actual Unity pixels exist", errors);

        foreach (string value in new[]
        {
            contract.manufacture, contract.dimensionsThickness, contract.materialsFinish,
            contract.mounting, contract.interfacesGapsSeals, contract.orientationExposure,
            contract.aging, contract.geometryVsMaterial, contract.lodPolicy,
            contract.weatheringCausality, contract.lookdevBrief, contract.sourceBasis
        })
            Require(!string.IsNullOrWhiteSpace(value),
                "mandatory construction/material reasoning field is empty", errors);

        if (!File.Exists(AbsolutePath(LookdevPath)))
            errors.Add("facade sill tray edge-break lookdev illustration is missing");
        if (!File.Exists(AbsolutePath(SourcePath)))
            errors.Add("facade sill tray edge-break source file is missing");

        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Facade sill tray edge-break contract FAILED:\n - " + string.Join("\n - ", errors));
    }

    /// <summary>
    /// May mutate deterministic generated mesh assets only before this refinement's epoch is armed.
    /// Subsequent calls are read-only and fail closed on any geometry/proxy drift.
    /// </summary>
    public static void EnsurePreparedForFormalEvidence()
    {
        ValidateContractConfigOnly();
        EnsureBenchmarkSceneIsActive();

        if (EpochArmed)
        {
            ValidateGeneratedAssets();
            return;
        }

        QualityBlockFacadeSillDrainageUpgrade.EnsurePreparedForFormalEvidence();
        bool authored = IsAuthoredDanchiActive();
        if (authored)
        {
            SessionState.SetBool(AuthoredEpochKey, true);
            SessionState.SetString(FingerprintKey, "AUTHORED_DANCHI");
            SessionState.SetBool(EpochArmedKey, true);
            return;
        }

        GameObject root = FindSceneObject(SillRootName);
        if (root == null)
            throw new InvalidOperationException("Facade sill root is missing before edge-break refinement.");

        RefineUsedTrayAndProxyMeshes(root);
        AssetDatabase.SaveAssets();
        QualityBlockFacadeSillDrainageUpgrade.ValidateOpenScene();
        ValidateFallbackState(root);

        string fingerprint = BuildMeshFingerprint(root);
        if (string.IsNullOrWhiteSpace(fingerprint))
            throw new InvalidOperationException("Could not build sill tray edge-break mesh fingerprint.");

        SessionState.SetBool(AuthoredEpochKey, false);
        SessionState.SetString(FingerprintKey, fingerprint);
        SessionState.SetBool(EpochArmedKey, true);

        Debug.Log(
            "Facade sill tray edge-break refinement armed: 2 mm physical front-corner breaks, " +
            "combined LOD proxies rebuilt from the same refined LOD0 geometry, fingerprint=" + fingerprint +
            ". Visual Fidelity remains UNSCORED pending actual native 4K evidence.");
    }

    /// <summary>
    /// Strictly read-only after arming. Used by reflection request/poll/completion/pre-still binding and
    /// native-4K/temporal camera pre-cull so a stale razor-edge mesh cannot silently enter evidence.
    /// </summary>
    public static void ValidateGeneratedAssets()
    {
        ValidateContractConfigOnly();
        EnsureBenchmarkSceneIsActive();
        if (!EpochArmed)
            throw new InvalidOperationException(
                "Facade sill tray edge-break epoch is not armed. Formal preparation must run before evidence capture.");

        bool authored = IsAuthoredDanchiActive();
        if (authored != AuthoredEpoch)
            throw new InvalidOperationException(
                "Danchi authored/fallback mode changed after sill tray edge-break epoch arm.");
        if (authored)
        {
            if (!string.Equals(ArmedFingerprint, "AUTHORED_DANCHI", StringComparison.Ordinal))
                throw new InvalidOperationException("Authored sill edge-break epoch fingerprint is inconsistent.");
            return;
        }

        GameObject root = FindSceneObject(SillRootName);
        if (root == null)
            throw new InvalidOperationException("Facade sill root was deleted after edge-break epoch arm.");

        ValidateFallbackState(root);
        string current = BuildMeshFingerprint(root);
        if (string.IsNullOrWhiteSpace(ArmedFingerprint) ||
            !string.Equals(current, ArmedFingerprint, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Facade sill tray/LOD proxy mesh fingerprint changed after edge-break epoch arm. " +
                "Do not repair in place; restart a clean Editor session and capture a fresh reflection baseline.");
    }

    [MenuItem("NewTown/Geometry/Prepare Facade Sill Tray Edge Break")]
    public static void PrepareFromMenu()
    {
        EnsurePreparedForFormalEvidence();
        Debug.Log("Facade sill tray edge-break preparation complete; rendered verification is still pending.");
    }

    private static void OnCameraPreCull(Camera camera)
    {
        if (camera == null || camera.targetTexture == null ||
            !(camera.targetTexture.name.StartsWith("QA4K_", StringComparison.Ordinal) ||
              camera.targetTexture.name.StartsWith("QATemporal_", StringComparison.Ordinal)) ||
            !EditorSceneManager.GetActiveScene().IsValid() ||
            !string.Equals(EditorSceneManager.GetActiveScene().path, ScenePath, StringComparison.Ordinal))
            return;
        ValidateGeneratedAssets();
    }

    private static void RefineUsedTrayAndProxyMeshes(GameObject root)
    {
        LODGroup[] groups = root.GetComponentsInChildren<LODGroup>(true);
        if (groups.Length != ExpectedWindowCount)
            throw new InvalidOperationException(
                $"Expected {ExpectedWindowCount} sill LODGroups before edge refinement, found {groups.Length}.");

        var representativeByPhase = new Dictionary<int, LODGroup>();
        var refinedTrayPaths = new HashSet<string>(StringComparer.Ordinal);

        foreach (LODGroup group in groups.OrderBy(g => HierarchyPath(g.transform), StringComparer.Ordinal))
        {
            Transform lod0 = group.transform.Find("LOD0");
            Transform trayTransform = lod0 != null ? lod0.Find("SillTray") : null;
            MeshFilter trayFilter = trayTransform != null ? trayTransform.GetComponent<MeshFilter>() : null;
            Mesh tray = trayFilter != null ? trayFilter.sharedMesh : null;
            if (tray == null)
                throw new InvalidOperationException("Sill LOD0 tray mesh missing: " + HierarchyPath(group.transform));

            int phase = ParsePhase(tray.name, "GM_FacadeSill_Tray_P");
            string trayPath = AssetDatabase.GetAssetPath(tray);
            if (string.IsNullOrWhiteSpace(trayPath) ||
                !trayPath.StartsWith(MeshRoot + "/", StringComparison.Ordinal))
                throw new InvalidOperationException("Sill tray is not a generated project mesh asset: " + tray.name);

            if (refinedTrayPaths.Add(trayPath))
                OverwriteWithRefinedTray(tray, phase);
            if (!representativeByPhase.ContainsKey(phase))
                representativeByPhase.Add(phase, group);
        }

        foreach (KeyValuePair<int, LODGroup> pair in representativeByPhase.OrderBy(x => x.Key))
            RebuildCombinedProxy(pair.Value, pair.Key);
    }

    private static void OverwriteWithRefinedTray(Mesh target, int phase)
    {
        Mesh refined = BuildRefinedTrayMesh(phase);
        string originalName = target.name;
        CopyMeshData(target, refined);
        target.name = originalName;
        EditorUtility.SetDirty(target);
        UnityEngine.Object.DestroyImmediate(refined);
    }

    private static void RebuildCombinedProxy(LODGroup group, int phase)
    {
        Transform lod0 = group.transform.Find("LOD0");
        if (lod0 == null)
            throw new InvalidOperationException("LOD0 root missing while rebuilding sill combined proxy.");

        string[] names = { "SillTray", "Skirt_Left", "Skirt_Center", "Skirt_Right", "DripNose" };
        var combines = new List<CombineInstance>(names.Length);
        foreach (string name in names)
        {
            Transform child = lod0.Find(name);
            MeshFilter filter = child != null ? child.GetComponent<MeshFilter>() : null;
            if (filter == null || filter.sharedMesh == null)
                throw new InvalidOperationException("Cannot rebuild sill combined proxy; missing LOD0 component " + name + ".");
            combines.Add(new CombineInstance
            {
                mesh = filter.sharedMesh,
                transform = group.transform.worldToLocalMatrix * child.localToWorldMatrix
            });
        }

        LOD[] lods = group.GetLODs();
        if (lods.Length != 4 || lods[1].renderers.Length != 1 ||
            lods[2].renderers.Length != 1 || lods[3].renderers.Length != 1)
            throw new InvalidOperationException("Sill LOD topology changed before combined-proxy rebuild.");

        Mesh target = GetRendererMesh(lods[1].renderers[0]);
        Mesh l2 = GetRendererMesh(lods[2].renderers[0]);
        Mesh l3 = GetRendererMesh(lods[3].renderers[0]);
        if (target == null || l2 != target || l3 != target)
            throw new InvalidOperationException(
                "Sill LOD1/2/3 must share the same combined proxy asset before edge refinement.");
        if (ParsePhase(target.name, "GM_FacadeSill_CombinedProxy_P") != phase)
            throw new InvalidOperationException("Combined sill proxy phase does not match its LOD0 tray phase.");

        var rebuilt = new Mesh { name = target.name };
        rebuilt.CombineMeshes(combines.ToArray(), true, true, false);
        rebuilt.RecalculateNormals();
        rebuilt.RecalculateTangents();
        rebuilt.RecalculateBounds();
        CopyMeshData(target, rebuilt);
        target.name = rebuilt.name;
        EditorUtility.SetDirty(target);
        UnityEngine.Object.DestroyImmediate(rebuilt);
    }

    private static Mesh BuildRefinedTrayMesh(int phase)
    {
        float bottomInsetY = Mathf.Lerp(TrayBottomBackY, TrayBottomFrontY,
            (TrayFrontZ - EdgeBreak - TrayBackZ) / (TrayFrontZ - TrayBackZ));
        float topInsetY = Mathf.Lerp(TrayTopBackY, TrayTopFrontY,
            (TrayFrontZ - EdgeBreak - TrayBackZ) / (TrayFrontZ - TrayBackZ));

        var profile = new[]
        {
            new Vector2(TrayBackZ, TrayBottomBackY),
            new Vector2(TrayFrontZ - EdgeBreak, bottomInsetY),
            new Vector2(TrayFrontZ, TrayBottomFrontY + EdgeBreak),
            new Vector2(TrayFrontZ, TrayTopFrontY - EdgeBreak),
            new Vector2(TrayFrontZ - EdgeBreak, topInsetY),
            new Vector2(TrayBackZ, TrayTopBackY)
        };

        float half = SillWidth * 0.5f;
        float cy = profile.Average(p => p.y);
        float cz = profile.Average(p => p.x);
        Vector3 center = new Vector3(0f, cy, cz);
        var vertices = new List<Vector3>(RefinedTrayVertexCount);
        var triangles = new List<int>(RefinedTrayTriangleIndexCount);
        var uv = new List<Vector2>(RefinedTrayVertexCount);

        for (int i = 0; i < profile.Length; i++)
        {
            int j = (i + 1) % profile.Length;
            Vector3 a = new Vector3(-half, profile[i].y, profile[i].x);
            Vector3 b = new Vector3(-half, profile[j].y, profile[j].x);
            Vector3 c = new Vector3(half, profile[j].y, profile[j].x);
            Vector3 d = new Vector3(half, profile[i].y, profile[i].x);
            AddQuadFacingOut(vertices, triangles, uv, a, b, c, d, center);
        }

        AddEndCap(vertices, triangles, uv, profile, -half, center);
        AddEndCap(vertices, triangles, uv, profile, half, center);

        var mesh = new Mesh { name = $"GM_FacadeSill_Tray_P{phase}" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uv);
        mesh.RecalculateNormals();
        BakeMetricUv(mesh, phase * PhaseStepMeters);
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddEndCap(List<Vector3> vertices, List<int> triangles, List<Vector2> uv,
        Vector2[] profile, float x, Vector3 meshCenter)
    {
        float cy = profile.Average(p => p.y);
        float cz = profile.Average(p => p.x);
        Vector3 capCenter = new Vector3(x, cy, cz);
        for (int i = 0; i < profile.Length; i++)
        {
            int j = (i + 1) % profile.Length;
            Vector3 a = capCenter;
            Vector3 b = new Vector3(x, profile[i].y, profile[i].x);
            Vector3 c = new Vector3(x, profile[j].y, profile[j].x);
            AddTriangleFacingOut(vertices, triangles, uv, a, b, c, meshCenter);
        }
    }

    private static void AddQuadFacingOut(List<Vector3> vertices, List<int> triangles, List<Vector2> uv,
        Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 meshCenter)
    {
        int start = vertices.Count;
        vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
        uv.Add(Vector2.zero); uv.Add(Vector2.right); uv.Add(Vector2.one); uv.Add(Vector2.up);
        Vector3 normal = Vector3.Cross(b - a, c - a);
        Vector3 faceCenter = (a + b + c + d) * 0.25f;
        if (Vector3.Dot(normal, faceCenter - meshCenter) >= 0f)
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

    private static void AddTriangleFacingOut(List<Vector3> vertices, List<int> triangles, List<Vector2> uv,
        Vector3 a, Vector3 b, Vector3 c, Vector3 meshCenter)
    {
        int start = vertices.Count;
        vertices.Add(a); vertices.Add(b); vertices.Add(c);
        uv.Add(Vector2.zero); uv.Add(Vector2.right); uv.Add(Vector2.up);
        Vector3 normal = Vector3.Cross(b - a, c - a);
        Vector3 faceCenter = (a + b + c) / 3f;
        if (Vector3.Dot(normal, faceCenter - meshCenter) >= 0f)
        {
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
        }
        else
        {
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
        }
    }

    private static void BakeMetricUv(Mesh mesh, float phaseMeters)
    {
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        var uv = new Vector2[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 n = normals[i];
            Vector3 v = vertices[i];
            float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
            Vector2 metric = az >= ax && az >= ay
                ? new Vector2(v.x, v.y)
                : ax >= ay ? new Vector2(v.z, v.y) : new Vector2(v.x, v.z);
            uv[i] = metric + Vector2.one * phaseMeters;
        }
        mesh.uv = uv;
    }

    private static void CopyMeshData(Mesh target, Mesh source)
    {
        target.Clear();
        target.indexFormat = source.indexFormat;
        target.vertices = source.vertices;
        target.normals = source.normals;
        target.tangents = source.tangents;
        target.uv = source.uv;
        target.triangles = source.triangles;
        target.RecalculateBounds();
    }

    private static void ValidateFallbackState(GameObject root)
    {
        LODGroup[] groups = root.GetComponentsInChildren<LODGroup>(true);
        if (groups.Length != ExpectedWindowCount)
            throw new InvalidOperationException(
                $"Expected {ExpectedWindowCount} sill LODGroups during edge-break validation, found {groups.Length}.");

        foreach (LODGroup group in groups)
        {
            Transform lod0 = group.transform.Find("LOD0");
            Transform trayTransform = lod0 != null ? lod0.Find("SillTray") : null;
            Mesh tray = trayTransform != null ? trayTransform.GetComponent<MeshFilter>()?.sharedMesh : null;
            if (tray == null)
                throw new InvalidOperationException("Refined sill tray mesh is missing: " + HierarchyPath(group.transform));
            int phase = ParsePhase(tray.name, "GM_FacadeSill_Tray_P");
            ValidateRefinedTray(tray, phase);

            LOD[] lods = group.GetLODs();
            if (lods.Length != 4 || lods[1].renderers.Length != 1 ||
                lods[2].renderers.Length != 1 || lods[3].renderers.Length != 1)
                throw new InvalidOperationException("Sill LOD topology drifted during edge-break validation.");
            Mesh proxy = GetRendererMesh(lods[1].renderers[0]);
            if (proxy == null || GetRendererMesh(lods[2].renderers[0]) != proxy ||
                GetRendererMesh(lods[3].renderers[0]) != proxy)
                throw new InvalidOperationException("LOD1/2/3 no longer share one refined sill proxy asset.");
            if (ParsePhase(proxy.name, "GM_FacadeSill_CombinedProxy_P") != phase)
                throw new InvalidOperationException("Refined combined-proxy phase no longer matches LOD0 tray phase.");
            ValidateCombinedProxy(proxy);
        }
    }

    private static void ValidateRefinedTray(Mesh mesh, int phase)
    {
        if (mesh.vertexCount != RefinedTrayVertexCount || mesh.triangles.Length != RefinedTrayTriangleIndexCount)
            throw new InvalidOperationException(
                $"Refined tray P{phase} topology drifted: vertices={mesh.vertexCount}, indices={mesh.triangles.Length}.");
        if (mesh.uv == null || mesh.uv.Length != mesh.vertexCount ||
            mesh.normals == null || mesh.normals.Length != mesh.vertexCount ||
            mesh.tangents == null || mesh.tangents.Length != mesh.vertexCount)
            throw new InvalidOperationException("Refined sill tray is missing UV/normal/tangent data: " + mesh.name);
        if (Mathf.Abs(mesh.bounds.size.x - SillWidth) > 0.0002f ||
            Mathf.Abs(mesh.bounds.min.z - TrayBackZ) > 0.0002f ||
            Mathf.Abs(mesh.bounds.max.z - TrayFrontZ) > 0.0002f)
            throw new InvalidOperationException("Refined sill tray outer envelope drifted: " + mesh.name);

        float insetZ = TrayFrontZ - EdgeBreak;
        float t = (insetZ - TrayBackZ) / (TrayFrontZ - TrayBackZ);
        float bottomInsetY = Mathf.Lerp(TrayBottomBackY, TrayBottomFrontY, t);
        float topInsetY = Mathf.Lerp(TrayTopBackY, TrayTopFrontY, t);
        Vector3[] v = mesh.vertices;
        if (!HasProfilePoint(v, insetZ, bottomInsetY) ||
            !HasProfilePoint(v, TrayFrontZ, TrayBottomFrontY + EdgeBreak) ||
            !HasProfilePoint(v, TrayFrontZ, TrayTopFrontY - EdgeBreak) ||
            !HasProfilePoint(v, insetZ, topInsetY))
            throw new InvalidOperationException(
                "Physical 2 mm front-corner edge-break vertices are missing from " + mesh.name + ".");
    }

    private static void ValidateCombinedProxy(Mesh mesh)
    {
        if (mesh.uv == null || mesh.uv.Length != mesh.vertexCount ||
            mesh.normals == null || mesh.normals.Length != mesh.vertexCount ||
            mesh.tangents == null || mesh.tangents.Length != mesh.vertexCount)
            throw new InvalidOperationException("Refined combined sill proxy lacks UV/normal/tangent data: " + mesh.name);

        float insetZ = TrayFrontZ - EdgeBreak;
        float t = (insetZ - TrayBackZ) / (TrayFrontZ - TrayBackZ);
        float bottomInsetY = Mathf.Lerp(TrayBottomBackY, TrayBottomFrontY, t);
        float topInsetY = Mathf.Lerp(TrayTopBackY, TrayTopFrontY, t);
        Vector3[] v = mesh.vertices;
        if (!HasProfilePoint(v, OriginZ + insetZ, OriginY + bottomInsetY, true) ||
            !HasProfilePoint(v, OriginZ + TrayFrontZ, OriginY + TrayTopFrontY - EdgeBreak, true) ||
            !HasProfilePoint(v, OriginZ + insetZ, OriginY + topInsetY, true))
            throw new InvalidOperationException(
                "Combined sill proxy does not carry the refined tray edge-break geometry through far LODs: " + mesh.name);
    }

    private static bool HasProfilePoint(Vector3[] vertices, float z, float y, bool combined = false)
    {
        const float tolerance = 0.00025f;
        return vertices.Any(p => Mathf.Abs(p.z - z) <= tolerance && Mathf.Abs(p.y - y) <= tolerance);
    }

    private static string BuildMeshFingerprint(GameObject root)
    {
        var paths = new SortedDictionary<string, Mesh>(StringComparer.Ordinal);
        foreach (LODGroup group in root.GetComponentsInChildren<LODGroup>(true))
        {
            Transform trayTransform = group.transform.Find("LOD0/SillTray");
            Mesh tray = trayTransform != null ? trayTransform.GetComponent<MeshFilter>()?.sharedMesh : null;
            LOD[] lods = group.GetLODs();
            Mesh proxy = lods.Length > 1 && lods[1].renderers.Length == 1 ? GetRendererMesh(lods[1].renderers[0]) : null;
            AddMeshByPath(paths, tray);
            AddMeshByPath(paths, proxy);
        }

        var sb = new StringBuilder();
        foreach (KeyValuePair<string, Mesh> pair in paths)
        {
            sb.Append(pair.Key).Append('|');
            AppendMesh(sb, pair.Value);
        }
        using (SHA256 sha = SHA256.Create())
        {
            byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
            return string.Concat(digest.Select(b => b.ToString("x2")));
        }
    }

    private static void AddMeshByPath(IDictionary<string, Mesh> map, Mesh mesh)
    {
        if (mesh == null)
            throw new InvalidOperationException("Cannot fingerprint a missing sill edge-break mesh.");
        string path = AssetDatabase.GetAssetPath(mesh);
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException("Sill edge-break mesh is not a persisted asset: " + mesh.name);
        map[path] = mesh;
    }

    private static void AppendMesh(StringBuilder sb, Mesh mesh)
    {
        sb.Append(mesh.name).Append('|').Append(mesh.vertexCount).Append('|').Append(mesh.triangles.Length).Append('|');
        foreach (Vector3 p in mesh.vertices)
            sb.Append(p.x.ToString("R")).Append(',').Append(p.y.ToString("R")).Append(',').Append(p.z.ToString("R")).Append(';');
        sb.Append('|');
        foreach (int i in mesh.triangles) sb.Append(i).Append(',');
        sb.Append('|');
        foreach (Vector2 p in mesh.uv) sb.Append(p.x.ToString("R")).Append(',').Append(p.y.ToString("R")).Append(';');
    }

    private static Mesh GetRendererMesh(Renderer renderer)
    {
        MeshFilter filter = renderer != null ? renderer.GetComponent<MeshFilter>() : null;
        return filter != null ? filter.sharedMesh : null;
    }

    private static int ParsePhase(string meshName, string prefix)
    {
        if (string.IsNullOrWhiteSpace(meshName) || !meshName.StartsWith(prefix, StringComparison.Ordinal) ||
            !int.TryParse(meshName.Substring(prefix.Length), out int phase) || phase < 0 || phase >= PhaseBins)
            throw new InvalidOperationException("Unexpected phase-specific sill mesh name: " + meshName);
        return phase;
    }

    private static bool IsAuthoredDanchiActive()
    {
        QualityBlockArtSlot slot = Resources.FindObjectsOfTypeAll<QualityBlockArtSlot>()
            .FirstOrDefault(x => x != null && x.gameObject.scene.IsValid() &&
                                 string.Equals(x.gameObject.scene.path, ScenePath, StringComparison.Ordinal) &&
                                 x.SlotId == "danchi.main");
        return slot != null && slot.IsUsingAuthoredArt;
    }

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x != null && x.scene.IsValid() &&
                                 string.Equals(x.scene.path, ScenePath, StringComparison.Ordinal) &&
                                 string.Equals(x.name, name, StringComparison.Ordinal));
    }

    private static void EnsureBenchmarkSceneIsActive()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !string.Equals(scene.path, ScenePath, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Facade sill tray edge-break refinement may run only in the active QualityBlock1990s benchmark scene.");
    }

    private static string HierarchyPath(Transform transform)
    {
        if (transform == null) return "<null>";
        var names = new Stack<string>();
        for (Transform t = transform; t != null; t = t.parent) names.Push(t.name);
        return string.Join("/", names);
    }

    private static T LoadJson<T>(string assetPath)
    {
        string absolute = AbsolutePath(assetPath);
        if (!File.Exists(absolute))
            throw new FileNotFoundException("Required QA file not found: " + assetPath);
        T value = JsonUtility.FromJson<T>(File.ReadAllText(absolute));
        if (value == null)
            throw new InvalidOperationException("Could not parse QA JSON: " + assetPath);
        return value;
    }

    private static string AbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrWhiteSpace(projectRoot))
            throw new InvalidOperationException("Could not resolve Unity project root for sill edge-break refinement.");
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private static void Require(bool condition, string message, ICollection<string> errors)
    {
        if (!condition) errors.Add(message);
    }

    private static void RequireNear(float actual, float expected, float tolerance, string label,
        ICollection<string> errors)
    {
        if (Mathf.Abs(actual - expected) > tolerance)
            errors.Add($"{label} must be {expected:R}, got {actual:R}");
    }

    [Serializable]
    private sealed class EdgeBreakContract
    {
        public string schemaVersion;
        public string status;
        public string assemblyId;
        public string manufacture;
        public string dimensionsThickness;
        public string materialsFinish;
        public string mounting;
        public string interfacesGapsSeals;
        public string orientationExposure;
        public string aging;
        public string geometryVsMaterial;
        public string lodPolicy;
        public string weatheringCausality;
        public string lookdevBrief;
        public string sourceBasis;
        public Geometry geometry;
        public MaterialDefinition material;
        public Qa qa;
        public int implementationReadinessScore;
        public string visualFidelityStatus;
        public bool runtimeRenderVerified;
    }

    [Serializable]
    private sealed class Geometry
    {
        public float edgeBreakM;
        public float sillWidthM;
        public int profilePointCount;
        public bool preserveOuterEnvelope;
        public bool preserveDrainOpeningTopology;
    }

    [Serializable]
    private sealed class MaterialDefinition
    {
        public bool inheritCanonicalAgedAluminum;
        public string albedo;
        public string roughness;
        public string metallicSpecular;
        public string normalScale;
        public string microstructure;
        public float wetness;
        public string uvAging;
        public string angularFresnelResponse;
    }

    [Serializable]
    private sealed class Qa
    {
        public int phaseBins;
        public float phaseStepM;
        public bool rebuildCombinedLodProxyFromRefinedLod0;
        public bool freezeMeshFingerprintAfterFirstReflectionBaseline;
        public bool postArmValidationReadOnly;
        public int automaticVisualPoints;
        public bool renderVerificationPending;
    }
}
