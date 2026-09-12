using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Replaces the coarse six-box LOD0 crescent-lever asset cache with a deterministic swept hard-surface mesh.
/// The parent sash-latch builder already loads GM_SashLatch_CrescentLever_P*.asset by exact path, so preparing
/// these assets before the first formal reflection baseline upgrades both an already-persisted latch root and a
/// newly generated one without changing renderer identity, material assignment, weathering metadata or LOD count.
///
/// Geometry is deliberately conservative rather than product-identification: 34 mm centerline radius, 130 degree
/// sweep, 10 x 10 mm rounded-octagonal section with a 2 mm corner break, and 32 arc rings. Fine oxidation/contact
/// polish remains material response. This class awards zero Visual Fidelity points and cannot clear faceting,
/// aliasing, highlight or intersection defects without native 3840x2160 rendered evidence.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockSashLatchCurveRefinement
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/facade_sash_latch_curve_refinement_contract.json";
    private const string ParentContractPath = "Assets/QA/facade_sash_latch_contract.json";
    private const string LookdevPath = "Assets/QA/lookdev/facade_sash_latch_curve_refinement.svg";
    private const string MeshRoot = "Assets/Art/GeneratedFacadeHardwareMeshes/SashLatch";

    private const int PhaseBins = 8;
    private const float PhaseStepMeters = 0.011f;
    private const int ArcRings = 32;
    private const int SectionSides = 8;
    private const int ExpectedVertexCount = ArcRings * (SectionSides + 1) + 2 * (SectionSides + 1); // 306
    private const int ExpectedTriangleCount = (ArcRings - 1) * SectionSides * 2 + SectionSides * 2; // 512

    private const float RadiusMeters = 0.034f;
    private const float SweepDegrees = 130f;
    private const float StartDegrees = -65f;
    private const float EndDegrees = 65f;
    private const float SectionWidthMeters = 0.010f;
    private const float SectionThicknessMeters = 0.010f;
    private const float CornerBreakMeters = 0.002f;

    private static bool formalEpochArmed;
    private static readonly string[] ArmedDependencyHashes = new string[PhaseBins];
    private static bool preparing;

    static QualityBlockSashLatchCurveRefinement()
    {
        EditorApplication.delayCall -= PrepareAtEditorIdle;
        EditorApplication.delayCall += PrepareAtEditorIdle;
        EditorSceneManager.sceneSaving -= OnSceneSaving;
        EditorSceneManager.sceneSaving += OnSceneSaving;
        Camera.onPreCull -= OnCameraPreCull;
        Camera.onPreCull += OnCameraPreCull;
    }

    [MenuItem("NewTown/Geometry/Rebuild Smooth Sash Latch Crescent Assets")]
    public static void RebuildGeneratedAssets()
    {
        if (formalEpochArmed)
            throw new InvalidOperationException(
                "Sash-latch curve assets may not be rebuilt after the formal reflection epoch is armed. Start a new evidence epoch/domain reload first.");

        PrepareGeneratedLeverAssets(forceRewrite: true);
        ValidateGeneratedAssets();
        Debug.Log(
            "Smooth sash-latch crescent assets rebuilt: 8 phase variants, 32 arc rings, 10 x 10 mm rounded section. " +
            "Visual Fidelity remains UNSCORED pending native 4K evidence.");
    }

    [MenuItem("NewTown/QA/Validate Smooth Sash Latch Crescent Assets")]
    public static void ValidateGeneratedAssets()
    {
        ValidateContractConfigOnly();
        for (int phase = 0; phase < PhaseBins; phase++)
        {
            string path = LeverAssetPath(phase);
            Mesh actual = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (actual == null)
                throw new InvalidOperationException("Smooth sash-latch crescent asset is missing: " + path);

            Mesh expected = BuildSmoothCrescentMesh(phase);
            try
            {
                if (!MeshMatchesExpected(actual, expected))
                    throw new InvalidOperationException(
                        "Sash-latch crescent asset drifted from the exact swept-curve construction contract: " + path);
                ValidateMeshPayload(actual, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(expected);
            }

            if (formalEpochArmed)
            {
                string current = AssetDatabase.GetAssetDependencyHash(path).ToString();
                if (!string.Equals(current, ArmedDependencyHashes[phase], StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "Sash-latch crescent asset changed after the formal reflection epoch was armed: " + path);
            }
        }
    }

    [MenuItem("NewTown/QA/Validate Sash Latch Curve Refinement Contract")]
    public static void ValidateContractConfigOnly()
    {
        CurveContract contract = LoadJson<CurveContract>(ContractPath);
        if (contract == null || contract.geometry == null || contract.material == null || contract.qa == null)
            throw new InvalidOperationException("Sash-latch curve refinement contract is null or incomplete.");

        var errors = new List<string>();
        Require(string.Equals(contract.schemaVersion, "1.0", StringComparison.Ordinal), "schemaVersion must be 1.0", errors);
        Require(string.Equals(contract.assemblyId, "apartment_sliding_sash_crescent_latch_lod0_curve_refinement", StringComparison.Ordinal), "assemblyId mismatch", errors);
        Require(string.Equals(contract.parentContractPath, ParentContractPath, StringComparison.Ordinal), "parentContractPath mismatch", errors);
        Require(string.Equals(contract.lookdevIllustrationPath, LookdevPath, StringComparison.Ordinal), "lookdevIllustrationPath mismatch", errors);
        Require(File.Exists(AbsolutePath(ParentContractPath)), "parent sash-latch contract is missing", errors);
        Require(File.Exists(AbsolutePath(LookdevPath)), "curve lookdev illustration is missing", errors);

        Require(contract.geometry.phaseBins == PhaseBins, "geometry.phaseBins mismatch", errors);
        Require(contract.geometry.arcRings == ArcRings, "geometry.arcRings mismatch", errors);
        Require(contract.geometry.sectionSides == SectionSides, "geometry.sectionSides mismatch", errors);
        Require(contract.geometry.expectedVertexCount == ExpectedVertexCount, "geometry.expectedVertexCount mismatch", errors);
        Require(contract.geometry.expectedTriangleCount == ExpectedTriangleCount, "geometry.expectedTriangleCount mismatch", errors);
        RequireNear(contract.geometry.phaseStepM, PhaseStepMeters, 0.00001f, "geometry.phaseStepM", errors);
        RequireNear(contract.geometry.centerlineRadiusM, RadiusMeters, 0.00001f, "geometry.centerlineRadiusM", errors);
        RequireNear(contract.geometry.sweepDegrees, SweepDegrees, 0.001f, "geometry.sweepDegrees", errors);
        RequireNear(contract.geometry.sectionWidthM, SectionWidthMeters, 0.00001f, "geometry.sectionWidthM", errors);
        RequireNear(contract.geometry.sectionThicknessM, SectionThicknessMeters, 0.00001f, "geometry.sectionThicknessM", errors);
        RequireNear(contract.geometry.cornerBreakM, CornerBreakMeters, 0.00001f, "geometry.cornerBreakM", errors);

        Require(contract.material.metallicMin >= 0.45f && contract.material.metallicMax <= 1f,
            "metallic range must remain physically metallic", errors);
        Require(contract.material.roughnessMin >= 0.2f && contract.material.roughnessMax <= 0.85f,
            "roughness range is outside the restrained aged-metal envelope", errors);
        RequireNear(contract.material.normalScale, 0.08f, 0.0001f, "material.normalScale", errors);
        RequireNear(contract.material.microstructureMm, 0.45f, 0.001f, "material.microstructureMm", errors);
        RequireNear(contract.material.wetness, 0f, 0.0001f, "material.wetness", errors);

        Require(contract.qa.automaticVisualPoints == 0, "qa.automaticVisualPoints must remain zero", errors);
        Require(!contract.qa.runtimeRenderVerified, "qa.runtimeRenderVerified must remain false until a real Unity render exists", errors);
        Require(contract.qa.prepareBeforeFirstReflectionBaseline, "curve assets must be prepared before the first reflection baseline", errors);
        Require(contract.qa.readOnlyAfterFormalEpochArm, "curve assets must become read-only after formal epoch arm", errors);
        Require(contract.qa.requireNative4kFrontalObliqueGrazing, "native 4K frontal/oblique/grazing evidence must remain mandatory", errors);
        Require(contract.qa.require100PercentCrop, "100 percent crop evidence must remain mandatory", errors);

        foreach (string value in new[]
        {
            contract.manufacture, contract.installation, contract.interfacesGapsSeals, contract.orientationExposure,
            contract.aging, contract.geometryVsMaterial, contract.weatheringCausality, contract.lookdevBrief,
            contract.dimensionsThickness, contract.materialsFinish
        })
            Require(!string.IsNullOrWhiteSpace(value), "mandatory construction/material reasoning field is empty", errors);

        RequireExactSet(contract.requiredEvidenceRefs, new[]
        {
            "hero/facade_center",
            "oblique/construction_depth",
            "grazing/sash_rail_response",
            "crop/sash_latch_100pct"
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
            throw new InvalidOperationException("Sash-latch curve refinement contract failed:\n - " + string.Join("\n - ", errors));
    }

    /// <summary>
    /// Called from the central reflection-lighting fingerprint before the parent latch pass is prepared.
    /// The first call may create/repair the deterministic curve assets and then arms their dependency hashes.
    /// Every later fingerprint is strictly read-only and fails if any asset changes.
    /// </summary>
    public static void EnsurePreparedForFormalEvidence()
    {
        ValidateContractConfigOnly();
        if (!formalEpochArmed)
        {
            PrepareGeneratedLeverAssets(forceRewrite: false);
            ValidateGeneratedAssets();
            for (int phase = 0; phase < PhaseBins; phase++)
                ArmedDependencyHashes[phase] = AssetDatabase.GetAssetDependencyHash(LeverAssetPath(phase)).ToString();
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
            PrepareGeneratedLeverAssets(forceRewrite: false);
        }
        catch (Exception ex)
        {
            Debug.LogError("Could not prepare smooth sash-latch crescent assets at Editor idle: " + ex.Message);
        }
    }

    private static void OnSceneSaving(Scene scene, string path)
    {
        if (!string.Equals(path, ScenePath, StringComparison.Ordinal)) return;
        if (formalEpochArmed)
            ValidateGeneratedAssets();
        else
            PrepareGeneratedLeverAssets(forceRewrite: false);
    }

    private static void OnCameraPreCull(Camera camera)
    {
        if (camera == null || camera.name != "MainCamera") return;
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !string.Equals(scene.path, ScenePath, StringComparison.Ordinal)) return;
        ValidateGeneratedAssets();
    }

    private static void PrepareGeneratedLeverAssets(bool forceRewrite)
    {
        if (preparing) return;
        preparing = true;
        try
        {
            ValidateContractConfigOnly();
            Directory.CreateDirectory(AbsolutePath(MeshRoot));

            bool changed = false;
            for (int phase = 0; phase < PhaseBins; phase++)
            {
                string path = LeverAssetPath(phase);
                Mesh expected = BuildSmoothCrescentMesh(phase);
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

    private static Mesh BuildSmoothCrescentMesh(int phaseBin)
    {
        if (phaseBin < 0 || phaseBin >= PhaseBins)
            throw new ArgumentOutOfRangeException(nameof(phaseBin));

        float halfW = SectionWidthMeters * 0.5f;
        float halfT = SectionThicknessMeters * 0.5f;
        float b = CornerBreakMeters;
        var section = new[]
        {
            new Vector2( halfW - b,  halfT),
            new Vector2( halfW,      halfT - b),
            new Vector2( halfW,     -halfT + b),
            new Vector2( halfW - b, -halfT),
            new Vector2(-halfW + b, -halfT),
            new Vector2(-halfW,     -halfT + b),
            new Vector2(-halfW,      halfT - b),
            new Vector2(-halfW + b,  halfT)
        };

        var perimeter = new float[SectionSides + 1];
        for (int j = 1; j <= SectionSides; j++)
            perimeter[j] = perimeter[j - 1] + Vector2.Distance(section[j - 1], section[j % SectionSides]);

        float phaseMeters = phaseBin * PhaseStepMeters;
        float sweepRad = SweepDegrees * Mathf.Deg2Rad;
        float arcLength = RadiusMeters * sweepRad;
        int ringStride = SectionSides + 1; // duplicated seam preserves metric UV continuity

        var vertices = new List<Vector3>(ExpectedVertexCount);
        var uv = new List<Vector2>(ExpectedVertexCount);
        var triangles = new List<int>(ExpectedTriangleCount * 3);

        for (int i = 0; i < ArcRings; i++)
        {
            float t = i / (float)(ArcRings - 1);
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

        for (int i = 0; i < ArcRings - 1; i++)
        for (int j = 0; j < SectionSides; j++)
        {
            int a = i * ringStride + j;
            int b0 = a + 1;
            int c = (i + 1) * ringStride + j;
            int d = c + 1;
            triangles.Add(a); triangles.Add(b0); triangles.Add(c);
            triangles.Add(b0); triangles.Add(d); triangles.Add(c);
        }

        AddEndCap(vertices, uv, triangles, section, phaseMeters, StartDegrees, reverseWinding: true);
        AddEndCap(vertices, uv, triangles, section, phaseMeters + arcLength, EndDegrees, reverseWinding: false);

        var mesh = new Mesh
        {
            name = Path.GetFileNameWithoutExtension(LeverAssetPath(phaseBin))
        };
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

    private static bool MeshMatchesExpected(Mesh actual, Mesh expected)
    {
        if (actual == null || expected == null) return false;
        if (actual.vertexCount != ExpectedVertexCount || actual.triangles.Length / 3 != ExpectedTriangleCount) return false;
        if (!string.Equals(actual.name, expected.name, StringComparison.Ordinal)) return false;

        Vector3[] av = actual.vertices;
        Vector3[] ev = expected.vertices;
        Vector2[] auv = actual.uv;
        Vector2[] euv = expected.uv;
        int[] at = actual.triangles;
        int[] et = expected.triangles;
        if (auv == null || auv.Length != av.Length || euv == null || euv.Length != ev.Length || at.Length != et.Length)
            return false;

        for (int i = 0; i < av.Length; i++)
        {
            if ((av[i] - ev[i]).sqrMagnitude > 1e-12f) return false;
            if ((auv[i] - euv[i]).sqrMagnitude > 1e-12f) return false;
        }
        for (int i = 0; i < at.Length; i++)
            if (at[i] != et[i]) return false;
        return true;
    }

    private static void ValidateMeshPayload(Mesh mesh, string path)
    {
        if (mesh.vertexCount != ExpectedVertexCount || mesh.triangles.Length / 3 != ExpectedTriangleCount)
            throw new InvalidOperationException("Unexpected smooth crescent topology at " + path);
        if (mesh.uv == null || mesh.uv.Length != mesh.vertexCount ||
            mesh.normals == null || mesh.normals.Length != mesh.vertexCount ||
            mesh.tangents == null || mesh.tangents.Length != mesh.vertexCount)
            throw new InvalidOperationException("Smooth crescent asset is missing UV/normal/tangent payload at " + path);

        Bounds bounds = mesh.bounds;
        if (bounds.size.x < 0.020f || bounds.size.x > 0.055f ||
            bounds.size.y < 0.060f || bounds.size.y > 0.085f ||
            bounds.size.z < 0.009f || bounds.size.z > 0.011f)
            throw new InvalidOperationException("Smooth crescent physical bounds drifted at " + path + ": " + bounds.size);
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

    private static string LeverAssetPath(int phaseBin)
    {
        return $"{MeshRoot}/GM_SashLatch_CrescentLever_P{phaseBin}.asset";
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
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
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
    private sealed class CurveContract
    {
        public string schemaVersion;
        public string assemblyId;
        public string parentContractPath;
        public string lookdevIllustrationPath;
        public string manufacture;
        public string dimensionsThickness;
        public string materialsFinish;
        public string installation;
        public string interfacesGapsSeals;
        public string orientationExposure;
        public string aging;
        public string geometryVsMaterial;
        public string weatheringCausality;
        public string lookdevBrief;
        public GeometrySpec geometry;
        public MaterialSpec material;
        public QaSpec qa;
        public string[] requiredEvidenceRefs;
        public string[] criticalDefectIds;
    }

    [Serializable]
    private sealed class GeometrySpec
    {
        public int phaseBins;
        public float phaseStepM;
        public int arcRings;
        public int sectionSides;
        public int expectedVertexCount;
        public int expectedTriangleCount;
        public float centerlineRadiusM;
        public float sweepDegrees;
        public float sectionWidthM;
        public float sectionThicknessM;
        public float cornerBreakM;
    }

    [Serializable]
    private sealed class MaterialSpec
    {
        public float metallicMin;
        public float metallicMax;
        public float roughnessMin;
        public float roughnessMax;
        public float normalScale;
        public float microstructureMm;
        public float wetness;
        public string uvAging;
        public string angularFresnelResponse;
    }

    [Serializable]
    private sealed class QaSpec
    {
        public int automaticVisualPoints;
        public bool runtimeRenderVerified;
        public bool prepareBeforeFirstReflectionBaseline;
        public bool readOnlyAfterFormalEpochArm;
        public bool requireNative4kFrontalObliqueGrazing;
        public bool require100PercentCrop;
    }
}
