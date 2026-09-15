using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Replaces the broad-box LOD2 and single-sliver LOD3 sash-latch proxies with decimated versions of the
/// same manufactured crescent lock used by LOD0/LOD1. The far meshes preserve plate, swept lever and keeper
/// envelopes while reducing only the lever arc-ring count (8 rings at LOD2, 5 at LOD3). Eight deterministic
/// metric-UV phase variants are retained so distance LODs do not collapse all windows onto one microdetail phase.
///
/// This is implementation/evidence-integrity infrastructure. It awards zero Visual Fidelity points and cannot
/// clear visible_lod_pop, severe_aliasing_or_shimmer or highlight discontinuity without actual native-4K temporal
/// evidence crossing the LOD1->LOD2 and LOD2->LOD3 transitions.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockSashLatchFarLodContinuityRefinement
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/facade_sash_latch_far_lod_continuity_contract.json";
    private const string ParentContractPath = "Assets/QA/facade_sash_latch_contract.json";
    private const string Lod1ContractPath = "Assets/QA/facade_sash_latch_lod_continuity_contract.json";
    private const string MeshRoot = "Assets/Art/GeneratedFacadeHardwareMeshes/SashLatch";

    private const int PhaseBins = 8;
    private const float PhaseStepMeters = 0.011f;
    private const int Lod0ArcRings = 32;
    private const int Lod1ArcRings = 16;
    private const int Lod2ArcRings = 8;
    private const int Lod3ArcRings = 5;
    private const int SectionSides = 8;
    private const float RadiusMeters = 0.034f;
    private const float SweepDegrees = 130f;
    private const float StartDegrees = -65f;
    private const float EndDegrees = 65f;
    private const float SectionWidthMeters = 0.010f;
    private const float SectionThicknessMeters = 0.010f;
    private const float CornerBreakMeters = 0.002f;

    // Must match QualityBlockFacadeSashLatchUpgrade/LOD1 continuity local construction coordinates.
    private const float HardwareY = -0.060f;
    private const float HardwareZ = -7.205f;
    private const float LockX = -0.038f;
    private const float KeeperX = 0.030f;
    private const float Lod1Transition = 0.0030f;
    private const float Lod2Transition = 0.0012f;
    private const float Lod3Cull = 0.0004f;

    private static bool formalEpochArmed;
    private static bool preparing;
    private static readonly string[] ArmedDependencyHashes = new string[PhaseBins * 2];

    static QualityBlockSashLatchFarLodContinuityRefinement()
    {
        EditorApplication.delayCall -= PrepareAtEditorIdle;
        EditorApplication.delayCall += PrepareAtEditorIdle;
        EditorSceneManager.sceneSaving -= OnSceneSaving;
        EditorSceneManager.sceneSaving += OnSceneSaving;
        Camera.onPreCull -= OnCameraPreCull;
        Camera.onPreCull += OnCameraPreCull;
    }

    [MenuItem("NewTown/Geometry/Rebuild Sash Latch Far-LOD Curve Continuity")]
    public static void RebuildGeneratedAssets()
    {
        if (formalEpochArmed)
            throw new InvalidOperationException(
                "Sash-latch far-LOD assets may not be rebuilt after the formal reflection epoch is armed. Start a new evidence epoch/domain reload first.");

        PrepareGeneratedAssets(forceRewrite: true);
        ValidateGeneratedAssets();
        Debug.Log(
            "Sash-latch far-LOD continuity rebuilt: LOD2=8-ring and LOD3=5-ring crescent proxies across 8 metric-UV phases. " +
            "Visual Fidelity remains UNSCORED pending native 4K temporal evidence.");
    }

    [MenuItem("NewTown/QA/Validate Sash Latch Far-LOD Curve Continuity")]
    public static void ValidateGeneratedAssets()
    {
        ValidateContractConfigOnly();
        for (int lod = 2; lod <= 3; lod++)
        for (int phase = 0; phase < PhaseBins; phase++)
        {
            string path = ProxyAssetPath(lod, phase);
            Mesh actual = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (actual == null)
                throw new InvalidOperationException("Sash-latch far-LOD continuity asset is missing: " + path);

            Mesh expected = BuildProxyMesh(lod, phase);
            try
            {
                if (!MeshMatchesExpected(actual, expected))
                    throw new InvalidOperationException(
                        "Sash-latch LOD" + lod + " proxy drifted from the continuous-curve construction contract: " + path);
                ValidateMeshPayload(actual, lod, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(expected);
            }

            if (formalEpochArmed)
            {
                int index = HashIndex(lod, phase);
                string current = AssetDatabase.GetAssetDependencyHash(path).ToString();
                if (!string.Equals(current, ArmedDependencyHashes[index], StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "Sash-latch LOD" + lod + " proxy changed after the formal reflection epoch was armed: " + path);
            }
        }
    }

    [MenuItem("NewTown/QA/Validate Sash Latch Far-LOD Continuity Contract")]
    public static void ValidateContractConfigOnly()
    {
        FarLodContract contract = LoadJson<FarLodContract>(ContractPath);
        if (contract == null || contract.geometry == null || contract.qa == null)
            throw new InvalidOperationException("Sash-latch far-LOD continuity contract is null or incomplete.");

        var errors = new List<string>();
        Require(string.Equals(contract.schemaVersion, "1.0", StringComparison.Ordinal), "schemaVersion must be 1.0", errors);
        Require(string.Equals(contract.assemblyId, "apartment_sliding_sash_crescent_latch_far_lod_continuity_refinement", StringComparison.Ordinal), "assemblyId mismatch", errors);
        Require(string.Equals(contract.parentContractPath, ParentContractPath, StringComparison.Ordinal), "parentContractPath mismatch", errors);
        Require(string.Equals(contract.lod1ContinuityContractPath, Lod1ContractPath, StringComparison.Ordinal), "lod1ContinuityContractPath mismatch", errors);
        Require(File.Exists(AbsolutePath(ParentContractPath)), "parent sash-latch contract is missing", errors);
        Require(File.Exists(AbsolutePath(Lod1ContractPath)), "LOD1 continuity contract is missing", errors);

        Require(contract.geometry.phaseBins == PhaseBins, "geometry.phaseBins mismatch", errors);
        RequireNear(contract.geometry.phaseStepM, PhaseStepMeters, 0.00001f, "geometry.phaseStepM", errors);
        Require(contract.geometry.lod0ArcRings == Lod0ArcRings, "geometry.lod0ArcRings mismatch", errors);
        Require(contract.geometry.lod1ArcRings == Lod1ArcRings, "geometry.lod1ArcRings mismatch", errors);
        Require(contract.geometry.lod2ArcRings == Lod2ArcRings, "geometry.lod2ArcRings mismatch", errors);
        Require(contract.geometry.lod3ArcRings == Lod3ArcRings, "geometry.lod3ArcRings mismatch", errors);
        Require(contract.geometry.sectionSides == SectionSides, "geometry.sectionSides mismatch", errors);
        RequireNear(contract.geometry.centerlineRadiusM, RadiusMeters, 0.00001f, "geometry.centerlineRadiusM", errors);
        RequireNear(contract.geometry.sweepDegrees, SweepDegrees, 0.001f, "geometry.sweepDegrees", errors);
        RequireNear(contract.geometry.sectionWidthM, SectionWidthMeters, 0.00001f, "geometry.sectionWidthM", errors);
        RequireNear(contract.geometry.sectionThicknessM, SectionThicknessMeters, 0.00001f, "geometry.sectionThicknessM", errors);
        RequireNear(contract.geometry.cornerBreakM, CornerBreakMeters, 0.00001f, "geometry.cornerBreakM", errors);
        RequireNear(contract.geometry.lod1Transition, Lod1Transition, 0.00001f, "geometry.lod1Transition", errors);
        RequireNear(contract.geometry.lod2Transition, Lod2Transition, 0.00001f, "geometry.lod2Transition", errors);
        RequireNear(contract.geometry.lod3Cull, Lod3Cull, 0.00001f, "geometry.lod3Cull", errors);

        Require(contract.qa.automaticVisualPoints == 0, "qa.automaticVisualPoints must remain zero", errors);
        Require(!contract.qa.runtimeRenderVerified, "qa.runtimeRenderVerified must remain false until real Unity evidence exists", errors);
        Require(contract.qa.prepareBeforeFirstReflectionBaseline, "far-LOD proxies must be prepared before first reflection baseline", errors);
        Require(contract.qa.readOnlyAfterFormalEpochArm, "far-LOD proxies must be read-only after formal epoch arm", errors);
        Require(contract.qa.requireNative4kTemporalTransitionEvidence, "native 4K temporal transition evidence must remain mandatory", errors);
        Require(contract.qa.require100PercentCrop, "100 percent crop evidence must remain mandatory", errors);
        Require(contract.qa.requireSamePhysicalCurveEnvelopeAcrossLod1Lod2Lod3, "LOD1/2/3 curve envelope continuity must remain mandatory", errors);
        Require(contract.qa.requirePerPhaseFarLodAssets, "per-phase far-LOD assets must remain mandatory", errors);

        foreach (string value in new[]
        {
            contract.manufacture, contract.dimensionsThickness, contract.materialsFinish, contract.mounting,
            contract.interfacesGapsSeals, contract.orientationExposure, contract.aging, contract.geometryVsMaterial,
            contract.lodPolicy, contract.weatheringCausality, contract.lookdevBrief
        })
            Require(!string.IsNullOrWhiteSpace(value), "mandatory construction/material reasoning field is empty", errors);

        RequireExactSet(contract.requiredEvidenceRefs, new[]
        {
            "oblique/construction_depth",
            "grazing/sash_rail_response",
            "crop/sash_latch_100pct",
            "temporal/oblique_native4k"
        }, "requiredEvidenceRefs", errors);

        RequireExactSet(contract.criticalDefectIds, new[]
        {
            "visible_primitive_placeholder",
            "baked_or_painted_highlights",
            "impossible_material_physics",
            "obvious_repetition",
            "hero_geometry_intersection",
            "severe_aliasing_or_shimmer",
            "visible_lod_pop",
            "missing_construction_material_metadata",
            "unverified_render_claim"
        }, "criticalDefectIds", errors);

        if (errors.Count > 0)
            throw new InvalidOperationException("Sash-latch far-LOD continuity contract failed:\n - " + string.Join("\n - ", errors));
    }

    /// <summary>
    /// Called by the formal reflection-lighting fingerprint before the parent sash-latch builder. The first
    /// call may replace deterministic LOD2/LOD3 asset payloads, then dependency hashes are frozen. Repeated
    /// probe polls are read-only and fail closed on missing/replaced/drifted far-LOD state.
    /// </summary>
    public static void EnsurePreparedForFormalEvidence()
    {
        ValidateContractConfigOnly();
        if (!formalEpochArmed)
        {
            PrepareGeneratedAssets(forceRewrite: false);
            ValidateGeneratedAssets();
            for (int lod = 2; lod <= 3; lod++)
            for (int phase = 0; phase < PhaseBins; phase++)
                ArmedDependencyHashes[HashIndex(lod, phase)] = AssetDatabase.GetAssetDependencyHash(ProxyAssetPath(lod, phase)).ToString();
            formalEpochArmed = true;
            return;
        }

        ValidateGeneratedAssets();
    }

    private static void PrepareAtEditorIdle()
    {
        if (formalEpochArmed) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall -= PrepareAtEditorIdle;
            EditorApplication.delayCall += PrepareAtEditorIdle;
            return;
        }

        try
        {
            PrepareGeneratedAssets(forceRewrite: false);
        }
        catch (Exception ex)
        {
            Debug.LogError("Could not prepare sash-latch far-LOD curve continuity at Editor idle: " + ex.Message);
        }
    }

    private static void OnSceneSaving(Scene scene, string path)
    {
        if (!string.Equals(path, ScenePath, StringComparison.Ordinal)) return;
        if (formalEpochArmed)
            ValidateGeneratedAssets();
        else
            PrepareGeneratedAssets(forceRewrite: false);
    }

    private static void OnCameraPreCull(Camera camera)
    {
        if (camera == null || camera.name != "MainCamera") return;
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !string.Equals(scene.path, ScenePath, StringComparison.Ordinal)) return;
        ValidateGeneratedAssets();
    }

    private static void PrepareGeneratedAssets(bool forceRewrite)
    {
        if (preparing) return;
        preparing = true;
        try
        {
            ValidateContractConfigOnly();
            Directory.CreateDirectory(AbsolutePath(MeshRoot));
            bool changed = false;

            for (int lod = 2; lod <= 3; lod++)
            for (int phase = 0; phase < PhaseBins; phase++)
            {
                string path = ProxyAssetPath(lod, phase);
                Mesh expected = BuildProxyMesh(lod, phase);
                try
                {
                    Mesh actual = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (actual != null && !forceRewrite && MeshMatchesExpected(actual, expected))
                        continue;

                    if (actual == null)
                    {
                        AssetDatabase.CreateAsset(expected, path);
                        expected = null;
                    }
                    else
                    {
                        CopyMeshPayload(expected, actual);
                        EditorUtility.SetDirty(actual);
                    }
                    changed = true;
                }
                finally
                {
                    if (expected != null)
                        UnityEngine.Object.DestroyImmediate(expected);
                }
            }

            if (changed)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }
        finally
        {
            preparing = false;
        }
    }

    private static Mesh BuildProxyMesh(int lod, int phaseBin)
    {
        if (lod != 2 && lod != 3)
            throw new ArgumentOutOfRangeException(nameof(lod));
        int arcRings = lod == 2 ? Lod2ArcRings : Lod3ArcRings;

        Mesh basePlate = BuildMetricChamferedBox(new Vector3(0.030f, 0.080f, 0.008f), phaseBin, lod);
        Mesh lever = BuildCurvedLever(phaseBin, arcRings, lod);
        Mesh keeper = BuildMetricChamferedBox(new Vector3(0.024f, 0.066f, 0.010f), phaseBin, lod);
        try
        {
            var combines = new[]
            {
                new CombineInstance
                {
                    mesh = basePlate,
                    transform = Matrix4x4.Translate(new Vector3(LockX, HardwareY, HardwareZ))
                },
                new CombineInstance
                {
                    mesh = lever,
                    transform = Matrix4x4.Translate(new Vector3(LockX, HardwareY, HardwareZ + 0.014f))
                },
                new CombineInstance
                {
                    mesh = keeper,
                    transform = Matrix4x4.Translate(new Vector3(KeeperX, HardwareY, HardwareZ + 0.002f))
                }
            };

            var mesh = new Mesh { name = Path.GetFileNameWithoutExtension(ProxyAssetPath(lod, phaseBin)) };
            mesh.CombineMeshes(combines, true, true, false);
            if (mesh.uv == null || mesh.uv.Length != mesh.vertexCount)
                throw new InvalidOperationException("LOD" + lod + " proxy combine lost metric UV payload.");
            if (mesh.normals == null || mesh.normals.Length != mesh.vertexCount)
                mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(basePlate);
            UnityEngine.Object.DestroyImmediate(lever);
            UnityEngine.Object.DestroyImmediate(keeper);
        }
    }

    private static Mesh BuildMetricChamferedBox(Vector3 size, int phaseBin, int lod)
    {
        Mesh mesh = UnityEngine.Object.Instantiate(QualityBlockDetailMeshLibrary.GetChamferedBox(size));
        mesh.name = "QB_SashLatchLod" + lod + "_BoxTemp";
        BakeMetricUv(mesh, phaseBin * PhaseStepMeters);
        return mesh;
    }

    private static Mesh BuildCurvedLever(int phaseBin, int arcRings, int lod)
    {
        if (phaseBin < 0 || phaseBin >= PhaseBins)
            throw new ArgumentOutOfRangeException(nameof(phaseBin));
        if (arcRings < 3)
            throw new ArgumentOutOfRangeException(nameof(arcRings));

        float halfW = SectionWidthMeters * 0.5f;
        float halfT = SectionThicknessMeters * 0.5f;
        float corner = CornerBreakMeters;
        var section = new[]
        {
            new Vector2( halfW - corner,  halfT),
            new Vector2( halfW,           halfT - corner),
            new Vector2( halfW,          -halfT + corner),
            new Vector2( halfW - corner, -halfT),
            new Vector2(-halfW + corner, -halfT),
            new Vector2(-halfW,          -halfT + corner),
            new Vector2(-halfW,           halfT - corner),
            new Vector2(-halfW + corner,  halfT)
        };

        var perimeter = new float[SectionSides + 1];
        for (int j = 1; j <= SectionSides; j++)
            perimeter[j] = perimeter[j - 1] + Vector2.Distance(section[j - 1], section[j % SectionSides]);

        float phaseMeters = phaseBin * PhaseStepMeters;
        float arcLength = RadiusMeters * SweepDegrees * Mathf.Deg2Rad;
        int ringStride = SectionSides + 1;
        var vertices = new List<Vector3>(arcRings * ringStride + 2 * ringStride);
        var uv = new List<Vector2>(arcRings * ringStride + 2 * ringStride);
        var triangles = new List<int>((arcRings - 1) * SectionSides * 6 + SectionSides * 6);

        for (int i = 0; i < arcRings; i++)
        {
            float t = i / (float)(arcRings - 1);
            float angle = Mathf.Lerp(StartDegrees, EndDegrees, t) * Mathf.Deg2Rad;
            Vector3 radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            Vector3 center = radial * RadiusMeters;
            for (int j = 0; j <= SectionSides; j++)
            {
                Vector2 p = section[j % SectionSides];
                vertices.Add(center + radial * p.x + Vector3.forward * p.y);
                uv.Add(new Vector2(phaseMeters + t * arcLength, phaseMeters + perimeter[j]));
            }
        }

        for (int i = 0; i < arcRings - 1; i++)
        for (int j = 0; j < SectionSides; j++)
        {
            int a = i * ringStride + j;
            int b = a + 1;
            int c = (i + 1) * ringStride + j;
            int d = c + 1;
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
            triangles.Add(b); triangles.Add(d); triangles.Add(c);
        }

        AddEndCap(vertices, uv, triangles, section, phaseMeters, StartDegrees, true);
        AddEndCap(vertices, uv, triangles, section, phaseMeters + arcLength, EndDegrees, false);

        var mesh = new Mesh { name = "QB_SashLatchLod" + lod + "_CurvedLeverTemp" };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(triangles, 0, true);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddEndCap(List<Vector3> vertices, List<Vector2> uv, List<int> triangles,
        Vector2[] section, float uOffset, float angleDegrees, bool reverseWinding)
    {
        float angle = angleDegrees * Mathf.Deg2Rad;
        Vector3 radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
        Vector3 center = radial * RadiusMeters;
        int centerIndex = vertices.Count;
        vertices.Add(center);
        uv.Add(new Vector2(uOffset, uOffset));

        int ringStart = vertices.Count;
        for (int j = 0; j < SectionSides; j++)
        {
            Vector2 p = section[j];
            vertices.Add(center + radial * p.x + Vector3.forward * p.y);
            uv.Add(new Vector2(uOffset + p.x, uOffset + p.y));
        }

        for (int j = 0; j < SectionSides; j++)
        {
            int current = ringStart + j;
            int next = ringStart + ((j + 1) % SectionSides);
            triangles.Add(centerIndex);
            triangles.Add(reverseWinding ? next : current);
            triangles.Add(reverseWinding ? current : next);
        }
    }

    private static void BakeMetricUv(Mesh mesh, float phaseMeters)
    {
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        if (normals == null || normals.Length != vertices.Length)
        {
            mesh.RecalculateNormals();
            normals = mesh.normals;
        }

        var uv = new Vector2[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 n = normals[i];
            Vector3 v = vertices[i];
            float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
            Vector2 metric = az >= ax && az >= ay
                ? new Vector2(v.x, v.y)
                : ax >= ay
                    ? new Vector2(v.z, v.y)
                    : new Vector2(v.x, v.z);
            uv[i] = metric + Vector2.one * phaseMeters;
        }
        mesh.uv = uv;
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
    }

    private static bool MeshMatchesExpected(Mesh actual, Mesh expected)
    {
        if (actual == null || expected == null) return false;
        if (actual.vertexCount != expected.vertexCount || actual.triangles.Length != expected.triangles.Length) return false;
        if (!string.Equals(actual.name, expected.name, StringComparison.Ordinal)) return false;

        Vector3[] av = actual.vertices;
        Vector3[] ev = expected.vertices;
        Vector2[] auv = actual.uv;
        Vector2[] euv = expected.uv;
        Vector3[] an = actual.normals;
        Vector3[] en = expected.normals;
        Vector4[] atg = actual.tangents;
        Vector4[] etg = expected.tangents;
        int[] at = actual.triangles;
        int[] et = expected.triangles;
        if (auv == null || auv.Length != av.Length || euv == null || euv.Length != ev.Length ||
            an == null || an.Length != av.Length || en == null || en.Length != ev.Length ||
            atg == null || atg.Length != av.Length || etg == null || etg.Length != ev.Length ||
            at.Length != et.Length)
            return false;

        for (int i = 0; i < av.Length; i++)
        {
            if ((av[i] - ev[i]).sqrMagnitude > 1e-12f) return false;
            if ((auv[i] - euv[i]).sqrMagnitude > 1e-12f) return false;
            if ((an[i] - en[i]).sqrMagnitude > 1e-10f) return false;
            if ((atg[i] - etg[i]).sqrMagnitude > 1e-10f) return false;
        }
        for (int i = 0; i < at.Length; i++)
            if (at[i] != et[i]) return false;
        return true;
    }

    private static void ValidateMeshPayload(Mesh mesh, int lod, string path)
    {
        int minimumTriangles = lod == 2 ? 180 : 130;
        if (mesh.vertexCount < 150 || mesh.triangles.Length / 3 < minimumTriangles)
            throw new InvalidOperationException("LOD" + lod + " continuity proxy is unexpectedly coarse at " + path);
        if (mesh.uv == null || mesh.uv.Length != mesh.vertexCount ||
            mesh.normals == null || mesh.normals.Length != mesh.vertexCount ||
            mesh.tangents == null || mesh.tangents.Length != mesh.vertexCount)
            throw new InvalidOperationException("LOD" + lod + " continuity proxy is missing UV/normal/tangent payload at " + path);

        Bounds b = mesh.bounds;
        if (b.size.x < 0.085f || b.size.x > 0.110f ||
            b.size.y < 0.075f || b.size.y > 0.095f ||
            b.size.z < 0.010f || b.size.z > 0.035f)
            throw new InvalidOperationException("LOD" + lod + " continuity proxy physical bounds drifted at " + path + ": " + b.size);
    }

    private static void CopyMeshPayload(Mesh source, Mesh destination)
    {
        destination.Clear(false);
        destination.name = source.name;
        destination.vertices = source.vertices;
        destination.uv = source.uv;
        destination.triangles = source.triangles;
        destination.normals = source.normals;
        destination.tangents = source.tangents;
        destination.bounds = source.bounds;
    }

    private static string ProxyAssetPath(int lod, int phaseBin)
    {
        return $"{MeshRoot}/GM_SashLatch_L{lod}_Proxy_P{phaseBin}.asset";
    }

    private static int HashIndex(int lod, int phaseBin)
    {
        return (lod - 2) * PhaseBins + phaseBin;
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
            throw new InvalidOperationException("Could not resolve Unity project root for sash-latch far-LOD continuity QA.");
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private static void Require(bool condition, string message, List<string> errors)
    {
        if (!condition) errors.Add(message);
    }

    private static void RequireNear(float actual, float expected, float tolerance, string label, List<string> errors)
    {
        if (Mathf.Abs(actual - expected) > tolerance)
            errors.Add($"{label} must be {expected}, got {actual}");
    }

    private static void RequireExactSet(string[] actual, string[] expected, string label, List<string> errors)
    {
        if (actual == null)
        {
            errors.Add(label + " is missing");
            return;
        }
        var a = new HashSet<string>(actual, StringComparer.Ordinal);
        var e = new HashSet<string>(expected, StringComparer.Ordinal);
        if (a.Count != actual.Length || !a.SetEquals(e))
            errors.Add(label + " must be exactly [" + string.Join(", ", expected) + "]");
    }

    [Serializable]
    private sealed class FarLodContract
    {
        public string schemaVersion;
        public string assemblyId;
        public string parentContractPath;
        public string lod1ContinuityContractPath;
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
        public GeometrySpec geometry;
        public QaSpec qa;
        public string[] requiredEvidenceRefs;
        public string[] criticalDefectIds;
    }

    [Serializable]
    private sealed class GeometrySpec
    {
        public int phaseBins;
        public float phaseStepM;
        public int lod0ArcRings;
        public int lod1ArcRings;
        public int lod2ArcRings;
        public int lod3ArcRings;
        public int sectionSides;
        public float centerlineRadiusM;
        public float sweepDegrees;
        public float sectionWidthM;
        public float sectionThicknessM;
        public float cornerBreakM;
        public float lod1Transition;
        public float lod2Transition;
        public float lod3Cull;
    }

    [Serializable]
    private sealed class QaSpec
    {
        public int automaticVisualPoints;
        public bool runtimeRenderVerified;
        public bool prepareBeforeFirstReflectionBaseline;
        public bool readOnlyAfterFormalEpochArm;
        public bool requireNative4kTemporalTransitionEvidence;
        public bool require100PercentCrop;
        public bool requireSamePhysicalCurveEnvelopeAcrossLod1Lod2Lod3;
        public bool requirePerPhaseFarLodAssets;
    }
}
