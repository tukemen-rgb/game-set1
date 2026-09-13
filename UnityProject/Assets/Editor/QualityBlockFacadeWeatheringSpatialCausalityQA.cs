using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Fail-closed spatial causality guard for generated facade weathering.
///
/// The weathering generator already creates residue from named physical sources. This QA verifies
/// the resulting persisted meshes, not only the generator intent: every residue vertex must still
/// lie inside a defensible source catchment, and every mandatory source must have geometry witnesses.
/// It runs again before formal MainCamera rendering so later mesh edits cannot detach a stain from
/// its construction/exposure cause.
///
/// This is implementation/readiness QA. Passing it awards zero Visual Fidelity points and cannot
/// clear repetition, interpenetration, aliasing or any other critical defect without native 4K pixels.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockFacadeWeatheringSpatialCausalityQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string RootName = "FacadeWeatheringDetail";
    private const string ContractPath = "Assets/QA/facade_weathering_spatial_causality_contract.json";
    private const string LookdevPath = "Assets/QA/Lookdev/facade_weathering_spatial_causality.svg";
    private const string GatePath = "Assets/QA/visual_fidelity_gate.json";
    private const float FacadePlaneZ = -7.276f;

    private static readonly string[] CanonicalCriticalDefectIds =
    {
        "visible_primitive_placeholder",
        "obvious_repetition",
        "missing_construction_material_metadata",
        "unverified_render_claim"
    };

    static QualityBlockFacadeWeatheringSpatialCausalityQA()
    {
        Camera.onPreCull -= ValidateBeforeBenchmarkCameraCull;
        Camera.onPreCull += ValidateBeforeBenchmarkCameraCull;
    }

    [MenuItem("NewTown/QA/Validate Facade Weathering Spatial Causality")]
    public static void ValidateOpenScene()
    {
        EnsureBenchmarkScene();
        ValidateSceneInternal(true);
    }

    [MenuItem("NewTown/QA/Validate Facade Weathering Spatial Causality Contract")]
    public static void ValidateContractConfigOnly()
    {
        if (!File.Exists(ContractPath))
            throw new InvalidOperationException($"Missing weathering spatial-causality contract: {ContractPath}");
        if (!File.Exists(LookdevPath))
            throw new InvalidOperationException($"Missing weathering spatial-causality lookdev: {LookdevPath}");
        if (!File.Exists(GatePath))
            throw new InvalidOperationException($"Missing canonical Visual Fidelity Gate: {GatePath}");

        string contract = File.ReadAllText(ContractPath);
        string[] requiredTokens =
        {
            "\"contractVersion\": \"facade-weathering-spatial-causality-v1.0.0\"",
            "\"weathering_context_causality_weight\": 10",
            "\"weathering_context_causality_hardMinimum\": 9",
            "\"requiredCount\": 30",
            "\"requiredCount\": 210",
            "\"requiredCount\": 15",
            "\"minimumDownpipeSideTopHeightGainOverFarFacade_m\": 0.04",
            "\"requireAllWeatheringVerticesAttributed\": true",
            "\"requireAllMandatorySourcesWitnessed\": true",
            "\"formalMainCameraPreCullFailClosed\": true",
            "\"sourceQaCannotClearVisualCriticalDefects\": true",
            "\"runtimeRenderVerification\": \"PENDING_UNITY_RUNTIME\"",
            "\"visualFidelityPointsAwarded\": 0",
            "\"visualFidelityStatus\": \"UNSCORED\""
        };
        foreach (string token in requiredTokens)
            if (contract.IndexOf(token, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException($"Weathering spatial-causality contract missing required token: {token}");

        foreach (string id in CanonicalCriticalDefectIds)
        {
            string quoted = "\"" + id + "\"";
            if (contract.IndexOf(quoted, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException($"Weathering spatial-causality contract does not reference canonical critical defect {id}.");
        }

        string gate = File.ReadAllText(GatePath);
        foreach (string id in CanonicalCriticalDefectIds)
        {
            string quoted = "\"" + id + "\"";
            if (gate.IndexOf(quoted, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException($"Canonical Visual Fidelity Gate no longer contains critical defect {id} required by weathering causality QA.");
        }
    }

    private static void ValidateSceneInternal(bool logSuccess)
    {
        ValidateContractConfigOnly();

        GameObject root = FindSceneObject(RootName);
        if (root == null)
            throw new InvalidOperationException("FacadeWeatheringDetail is missing; cannot prove weathering causality.");

        var errors = new List<string>();
        ValidateGradeSplash(errors);
        ValidateRainRunoff(errors);
        ValidateBalconyDrip(errors);
        ValidateRailRust(errors);
        ValidateAcResidue(errors);

        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Facade weathering spatial-causality QA FAILED:\n - " + string.Join("\n - ", errors));

        if (logSuccess)
        {
            Debug.Log(
                "Facade weathering spatial-causality QA passed: persisted residue vertices remain inside modeled grade, sill, " +
                "downpipe, balcony-lip, rail-fixing and AC-drain catchments, with required source witnesses. " +
                "This is source/runtime-state QA only; native 4K review remains required and Visual Fidelity is UNSCORED.");
        }
    }

    private static void ValidateGradeSplash(List<string> errors)
    {
        MeshFilter filter = GetGroupFilter("Weathering_GradeSplash", errors);
        if (filter == null) return;

        Vector3[] world = WorldVertices(filter);
        Vector2[] uv = filter.sharedMesh.uv;
        int outside = world.Count(v =>
            v.x < -20.60f || v.x > 5.00f ||
            v.y < 0.015f || v.y > 0.47f ||
            Mathf.Abs(v.z - FacadePlaneZ) > 0.03f);
        if (outside > 0)
            errors.Add($"Weathering_GradeSplash has {outside}/{world.Length} vertices outside the grade-interface causal envelope.");

        if (uv == null || uv.Length != world.Length)
        {
            errors.Add("Weathering_GradeSplash lacks one UV0 value per vertex, so its causal upper-edge gradient cannot be verified.");
            return;
        }

        var nearTop = new List<float>();
        var farTop = new List<float>();
        for (int i = 0; i < world.Length; i++)
        {
            if (uv[i].y < 0.99f) continue;
            if (world[i].x >= 3.10f) nearTop.Add(world[i].y);
            if (world[i].x >= -15.0f && world[i].x <= -2.0f) farTop.Add(world[i].y);
        }
        if (nearTop.Count < 2 || farTop.Count < 4)
        {
            errors.Add($"Grade-splash upper-edge sampling is insufficient: nearDownpipe={nearTop.Count}, farFacade={farTop.Count}.");
            return;
        }

        float gain = nearTop.Average() - farTop.Average();
        if (gain < 0.04f)
            errors.Add($"Grade splash no longer strengthens near concentrated drainage: measured top-height gain={gain:0.###} m, required >=0.040 m.");
    }

    private static void ValidateRainRunoff(List<string> errors)
    {
        MeshFilter filter = GetGroupFilter("Weathering_RainRunoff", errors);
        if (filter == null) return;

        Vector3[] world = WorldVertices(filter);
        GameObject[] sills = FindSceneObjectsByPrefix("HD_WindowSillDrip")
            .OrderBy(x => x.transform.position.y)
            .ThenBy(x => x.transform.position.x)
            .ToArray();
        if (sills.Length != 30)
            errors.Add($"Rain-runoff source topology drift: expected 30 HD_WindowSillDrip sources, got {sills.Length}.");

        int unbound = world.Count(v =>
            !sills.Any(s => InSillCatchment(v, s.transform.position)) && !InDownpipeCatchment(v));
        if (unbound > 0)
            errors.Add($"Weathering_RainRunoff has {unbound}/{world.Length} vertices with no sill or downpipe source catchment.");

        foreach (GameObject sill in sills)
        {
            int witnesses = world.Count(v => InSillCatchment(v, sill.transform.position));
            if (witnesses < 8)
                errors.Add($"Sill {sill.name} at {sill.transform.position} has only {witnesses} rain-runoff witness vertices; required >=8.");
        }

        int downpipeWitnesses = world.Count(InDownpipeCatchment);
        if (downpipeWitnesses < 24)
            errors.Add($"Installed downpipe catchment has only {downpipeWitnesses} rain-runoff witness vertices; required >=24.");
    }

    private static bool InSillCatchment(Vector3 v, Vector3 source)
    {
        float below = source.y - v.y;
        return Mathf.Abs(v.x - source.x) <= 0.78f &&
               below >= 0.84f && below <= 1.95f &&
               Mathf.Abs(v.z - FacadePlaneZ) <= 0.04f;
    }

    private static bool InDownpipeCatchment(Vector3 v)
    {
        return Mathf.Abs(v.x - 4.425f) <= 0.19f &&
               v.y >= 0.35f && v.y <= 6.60f &&
               Mathf.Abs(v.z - FacadePlaneZ) <= 0.04f;
    }

    private static void ValidateBalconyDrip(List<string> errors)
    {
        MeshFilter filter = GetGroupFilter("Weathering_BalconyDrip", errors);
        if (filter == null) return;

        Vector3[] world = WorldVertices(filter);
        GameObject[] lips = FindSceneObjectsByPrefix("HD_BalconySlabLip")
            .OrderBy(x => x.transform.position.y)
            .ThenBy(x => x.transform.position.x)
            .ToArray();
        if (lips.Length != 30)
            errors.Add($"Balcony-drip source topology drift: expected 30 HD_BalconySlabLip sources, got {lips.Length}.");

        int unbound = world.Count(v => !lips.Any(l => InBalconyLipCatchment(v, l.transform.position)));
        if (unbound > 0)
            errors.Add($"Weathering_BalconyDrip has {unbound}/{world.Length} vertices outside every slab-lip catchment.");

        foreach (GameObject lip in lips)
        {
            int witnesses = world.Count(v => InBalconyLipCatchment(v, lip.transform.position));
            if (witnesses < 8)
                errors.Add($"Balcony lip {lip.name} at {lip.transform.position} has only {witnesses} drip witness vertices; required >=8.");
        }
    }

    private static bool InBalconyLipCatchment(Vector3 v, Vector3 source)
    {
        float below = source.y - v.y;
        return Mathf.Abs(v.x - source.x) <= 1.36f &&
               below >= 0.06f && below <= 0.45f &&
               Mathf.Abs(v.z - (source.z + 0.052f)) <= 0.025f;
    }

    private static void ValidateRailRust(List<string> errors)
    {
        MeshFilter filter = GetGroupFilter("Weathering_RailRust", errors);
        if (filter == null) return;

        Vector3[] world = WorldVertices(filter);
        GameObject[] plates = FindSceneObjectsByPrefix("HD_RailBasePlate_")
            .OrderBy(x => x.transform.position.y)
            .ThenBy(x => x.transform.position.x)
            .ToArray();
        if (plates.Length != 210)
            errors.Add($"Rail-rust source topology drift: expected 210 HD_RailBasePlate_ sources, got {plates.Length}.");

        int unbound = world.Count(v => !plates.Any(p => InRailFixingCatchment(v, p.transform.position)));
        if (unbound > 0)
            errors.Add($"Weathering_RailRust has {unbound}/{world.Length} vertices outside every rail-fixing catchment.");

        int activeFixings = plates.Count(p => world.Count(v => InRailFixingCatchment(v, p.transform.position)) >= 6);
        if (activeFixings < 20 || activeFixings > 60)
            errors.Add($"Rail oxide runoff is no longer sparse/credible: active source fixings={activeFixings}, required range=20..60 of 210.");
    }

    private static bool InRailFixingCatchment(Vector3 v, Vector3 source)
    {
        float below = source.y - v.y;
        return Mathf.Abs(v.x - source.x) <= 0.05f &&
               below >= 0.015f && below <= 0.23f &&
               Mathf.Abs(v.z - (source.z + 0.052f)) <= 0.018f;
    }

    private static void ValidateAcResidue(List<string> errors)
    {
        MeshFilter filter = GetGroupFilter("Weathering_ACResidue", errors);
        if (filter == null) return;

        Vector3[] world = WorldVertices(filter);
        GameObject[] drains = FindSceneObjectsByPrefix("HD_AC_DrainHose")
            .OrderBy(x => x.transform.position.y)
            .ThenBy(x => x.transform.position.x)
            .ToArray();
        if (drains.Length != 15)
            errors.Add($"AC-residue source topology drift: expected 15 HD_AC_DrainHose sources, got {drains.Length}.");

        var catchments = new List<AcCatchment>();
        foreach (GameObject drain in drains)
        {
            Renderer renderer = drain.GetComponent<Renderer>();
            if (renderer == null)
            {
                errors.Add($"AC drain source {drain.name} has no renderer bounds for termination causality.");
                continue;
            }
            catchments.Add(new AcCatchment(renderer.bounds.center.x, renderer.bounds.min.y - 0.003f, renderer.bounds.center.z + 0.02f, drain.name));
        }

        int unbound = world.Count(v => !catchments.Any(c => c.Contains(v)));
        if (unbound > 0)
            errors.Add($"Weathering_ACResidue has {unbound}/{world.Length} vertices outside every drain-termination catchment.");

        foreach (AcCatchment catchment in catchments)
        {
            int witnesses = world.Count(catchment.Contains);
            if (witnesses < 12)
                errors.Add($"AC drain {catchment.name} has only {witnesses} termination-residue witness vertices; required >=12.");
        }
    }

    private static MeshFilter GetGroupFilter(string groupName, List<string> errors)
    {
        GameObject group = FindSceneObject(groupName);
        GameObject root = FindSceneObject(RootName);
        if (group == null)
        {
            errors.Add($"Missing weathering group {groupName}.");
            return null;
        }
        if (root == null || !group.transform.IsChildOf(root.transform))
        {
            errors.Add($"Weathering group {groupName} is detached from {RootName}.");
            return null;
        }

        MeshFilter filter = group.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null || filter.sharedMesh.vertexCount == 0)
        {
            errors.Add($"Weathering group {groupName} has no persisted mesh vertices.");
            return null;
        }
        return filter;
    }

    private static Vector3[] WorldVertices(MeshFilter filter)
    {
        Vector3[] local = filter.sharedMesh.vertices;
        var world = new Vector3[local.Length];
        for (int i = 0; i < local.Length; i++)
            world[i] = filter.transform.TransformPoint(local[i]);
        return world;
    }

    private static void ValidateBeforeBenchmarkCameraCull(Camera camera)
    {
        if (camera == null || camera.gameObject == null || !camera.gameObject.scene.IsValid())
            return;
        if (!string.Equals(camera.gameObject.scene.path, ScenePath, StringComparison.Ordinal))
            return;
        if (!string.Equals(camera.name, "MainCamera", StringComparison.Ordinal))
            return;
        if (FindSceneObject(RootName) == null)
            return;

        ValidateSceneInternal(false);
    }

    private static void EnsureBenchmarkScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x.scene.IsValid() && string.Equals(x.name, name, StringComparison.Ordinal));
    }

    private static GameObject[] FindSceneObjectsByPrefix(string prefix)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .Where(x => x.scene.IsValid() && x.name.StartsWith(prefix, StringComparison.Ordinal))
            .ToArray();
    }

    private readonly struct AcCatchment
    {
        public readonly float x;
        public readonly float y;
        public readonly float z;
        public readonly string name;

        public AcCatchment(float x, float y, float z, string name)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.name = name;
        }

        public bool Contains(Vector3 v)
        {
            return Mathf.Abs(v.x - x) <= 0.12f &&
                   Mathf.Abs(v.y - y) <= 0.02f &&
                   Mathf.Abs(v.z - z) <= 0.10f;
        }
    }
}
