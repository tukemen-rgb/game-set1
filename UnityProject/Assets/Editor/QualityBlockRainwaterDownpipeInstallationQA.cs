using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Reconstructs the benchmark-facing rainwater leader as a physically installed drainage assembly.
/// The original generated RainGutter remains the macro pipe silhouette, but its diameter/stand-off and
/// material are corrected; manufactured hollow joints, wall restraint, roof-line offset and ground receiver
/// are explicit geometry. Detail pieces participate in the existing DanchiHighDetail LOD0/1 proxy policy,
/// while the macro pipe stays continuously rendered through longer-distance LOD2/3 states.
///
/// This is source/runtime construction QA only. It cannot clear a visual critical defect or award points
/// without sealed native 3840x2160 evidence and 100% crop inspection.
/// </summary>
public static class QualityBlockRainwaterDownpipeInstallationQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/rainwater_downpipe_installation_contract.json";
    private const string ReportPath = "Assets/QA/rainwater_downpipe_installation_runtime_report.json";
    private const string GeneratedMeshRoot = "Assets/Art/GeneratedDetailMeshes";
    private const string DetailRootName = "DanchiHighDetail";
    private const string AssemblyRootName = "HD_RainwaterDownpipeAssembly";
    private const string PipeName = "RainGutter";
    private const string PvcMaterialPath = "Assets/Art/GeneratedDetailMaterials/MAT_AgedDownpipePVC.mat";
    private const string MetalMaterialPath = "Assets/Art/GeneratedDetailMaterials/MAT_DarkGalvanizedSteel.mat";

    private const float PipeX = 4.45f;
    private const float FacadePlaneZ = -7.30f;
    private const float PipeZ = -7.18f;
    private const float PipeOuterDiameter = 0.075f;
    private const float PipeBottom = 0.16f;
    private const float PipeTop = 12.92f;
    private const float CouplerOuterDiameter = 0.084f;
    private const float CouplerInnerDiameter = 0.0765f;
    private const float CouplerHeight = 0.09f;
    private const float BandOuterDiameter = 0.084f;
    private const float BandInnerDiameter = 0.077f;
    private const float BandHeight = 0.032f;
    private const float PlateDepth = 0.008f;
    private const float StandoffInterfaceOverlap = 0.002f;
    private const float ReceiverOuterDiameter = 0.115f;
    private const float ReceiverInnerDiameter = 0.080f;
    private const float ReceiverHeight = 0.16f;
    private const float ReceiverCentreY = 0.10f;
    private const int RoundSides = 24;

    private static readonly float[] CouplerY = { 2.66f, 5.31f, 7.96f, 10.61f };
    private static readonly float[] ClampY = { 0.82f, 2.37f, 3.92f, 5.47f, 7.02f, 8.57f, 10.12f, 11.67f };

    [MenuItem("NewTown/Geometry/Reconstruct Rainwater Downpipe Installation")]
    public static void ApplyAndPersist()
    {
        ValidateContractConfigOnly();
        RequireQualityScene();

        Material pvc = RequireMaterial(PvcMaterialPath);
        Material metal = RequireMaterial(MetalMaterialPath);
        ValidateMaterialFamilies(pvc, metal);

        GameObject pipe = FindSceneObject(PipeName);
        GameObject detailRoot = FindSceneObject(DetailRootName);
        if (pipe == null) throw new InvalidOperationException("Legacy RainGutter macro pipe is missing.");
        if (detailRoot == null) throw new InvalidOperationException("DanchiHighDetail root is missing.");

        RemovePreviousAssembly(detailRoot.transform);
        RemoveLegacyDownpipeHardware();
        ConfigureMacroPipe(pipe, pvc);

        var assembly = new GameObject(AssemblyRootName);
        assembly.transform.SetParent(detailRoot.transform, false);

        for (int i = 0; i < CouplerY.Length; i++)
        {
            GameObject sleeve = CreateAnnularSleeve(
                $"HD_DownpipeJointSleeve_{i}", assembly.transform,
                new Vector3(PipeX, CouplerY[i], PipeZ), Quaternion.identity,
                CouplerOuterDiameter, CouplerInnerDiameter, CouplerHeight, pvc);
            ConfigureWeathering(sleeve,
                NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.SunExposed,
                NewTownStainSource.DrainRunoff | NewTownStainSource.UVExposure,
                1.0f, 0.72f, 0.10f, 0f);
        }

        for (int i = 0; i < ClampY.Length; i++)
            BuildWallRestraint(assembly.transform, i, ClampY[i], metal);

        BuildRoofOffset(assembly.transform, pvc, metal);
        BuildGroundReceiver(assembly.transform, pvc);

        // Rebuild the existing four-level detail LOD proxies after adding/repositioning drainage detail.
        // Joint sleeves/bands/standoffs/collars intentionally survive through LOD1; millimetre fasteners
        // disappear first. The macro pipe itself stays outside this accessory LODGroup so LOD2/3 cannot
        // introduce a disappearing-pipe silhouette swap.
        QualityBlockDanchiLodUpgrade.ApplyToOpenScene();
        QualityBlockDanchiLodUpgrade.ValidateOpenScene();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        ValidateOpenScene();
        Debug.Log(
            "Rainwater downpipe installation reconstructed and persisted. Native 4K inspection is still required; Visual Fidelity remains UNSCORED.");
    }

    [MenuItem("NewTown/QA/Validate Rainwater Downpipe Installation Contract")]
    public static void ValidateContractConfigOnly()
    {
        string absolute = AbsoluteAssetPath(ContractPath);
        if (!File.Exists(absolute))
            throw new InvalidOperationException($"Missing required rainwater construction metadata: {ContractPath}");

        string json = File.ReadAllText(absolute);
        string[] tokens =
        {
            "\"assemblyId\": \"danchi.rainwater.downpipe.east\"",
            "\"criticalDefectRiskReduced\": \"floating_interpenetrating_hero_geometry\"",
            "\"visualFidelityPointsAwarded\": 0",
            "\"pipeOuterDiameterMetres\": 0.075",
            "\"pipeCentreZMetres\": -7.18",
            "\"couplerInnerDiameterMetres\": 0.0765",
            "\"pipeBandInnerDiameterMetres\": 0.077",
            "\"groundReceiverInnerDiameterMetres\": 0.08",
            "\"hollowSleevesRequired\": true",
            "\"maximumBracketInterfaceGapMetres\": 0.003",
            "\"requiredPipeInsertionIntoReceiverMetres\": 0.02",
            "\"aged_pvc_u_downpipe\"",
            "\"aged_galvanized_bracket_hardware\"",
            "\"paintedOrBakedHighlightsForbidden\": true",
            "\"levelsRequired\": 4",
            "\"requirePipeBodyVisibleAtAllDistances\": true",
            "\"automaticVisualScore\": 0",
            "\"runtimeRenderVerification\": \"PENDING_UNITY_RUNTIME\"",
            "Assets/QA/Lookdev/rainwater_downpipe_installation.svg"
        };

        foreach (string token in tokens)
            if (json.IndexOf(token, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException($"Rainwater downpipe contract missing required token: {token}");

        string lookdev = AbsoluteAssetPath("Assets/QA/Lookdev/rainwater_downpipe_installation.svg");
        if (!File.Exists(lookdev))
            throw new InvalidOperationException("Rainwater downpipe lookdev illustration is missing.");
    }

    [MenuItem("NewTown/QA/Validate Rainwater Downpipe Installation")]
    public static void ValidateOpenScene()
    {
        ValidateContractConfigOnly();
        RequireQualityScene();

        var errors = new List<string>();
        Material pvc = AssetDatabase.LoadAssetAtPath<Material>(PvcMaterialPath);
        Material metal = AssetDatabase.LoadAssetAtPath<Material>(MetalMaterialPath);
        if (pvc == null) errors.Add($"Missing PVC material: {PvcMaterialPath}");
        if (metal == null) errors.Add($"Missing galvanized material: {MetalMaterialPath}");
        if (pvc != null && metal != null)
        {
            try { ValidateMaterialFamilies(pvc, metal); }
            catch (Exception ex) { errors.Add(ex.Message); }
        }

        GameObject pipe = FindSceneObject(PipeName);
        if (pipe == null) errors.Add("RainGutter macro pipe is missing.");
        else ValidatePipe(pipe, pvc, errors);

        GameObject detailRoot = FindSceneObject(DetailRootName);
        GameObject assembly = FindSceneObject(AssemblyRootName);
        if (detailRoot == null) errors.Add("DanchiHighDetail root is missing.");
        if (assembly == null) errors.Add("HD_RainwaterDownpipeAssembly is missing.");
        else if (detailRoot != null && !assembly.transform.IsChildOf(detailRoot.transform))
            errors.Add("Rainwater assembly is not parented beneath DanchiHighDetail.");

        ValidateCount("HD_DownpipeJointSleeve_", CouplerY.Length, errors);
        ValidateCount("HD_DownpipeClamp_", ClampY.Length, errors);
        ValidateCount("HD_DownpipeStandoff_", ClampY.Length, errors);
        ValidateCount("HD_DownpipeBracketPlate_", ClampY.Length, errors);
        ValidateCount("HD_DownpipeClampBolt_", ClampY.Length * 2, errors);
        ValidateExactCount("HD_DownpipeRoofOffset", 1, errors);
        ValidateExactCount("HD_DownpipeRoofCollar", 1, errors);
        ValidateExactCount("HD_DownpipeGroundReceiver", 1, errors);

        for (int i = 0; i < CouplerY.Length; i++)
        {
            GameObject sleeve = FindSceneObject($"HD_DownpipeJointSleeve_{i}");
            if (sleeve == null) continue;
            Vector3 c = sleeve.GetComponent<Renderer>()?.bounds.center ?? sleeve.transform.position;
            if (Mathf.Abs(c.x - PipeX) > 0.01f || Mathf.Abs(c.z - PipeZ) > 0.01f ||
                Mathf.Abs(c.y - CouplerY[i]) > 0.015f)
                errors.Add($"Joint sleeve {i} is off the manufactured pipe centreline: {c}.");
            ValidateAnnularMesh(sleeve, CouplerOuterDiameter, CouplerHeight, errors);
            ValidateBinding(sleeve, pvc, errors);
        }

        for (int i = 0; i < ClampY.Length; i++)
        {
            GameObject band = FindSceneObject($"HD_DownpipeClamp_{i}");
            GameObject plate = FindSceneObject($"HD_DownpipeBracketPlate_{i}");
            GameObject arm = FindSceneObject($"HD_DownpipeStandoff_{i}");
            if (band != null)
            {
                Vector3 c = band.GetComponent<Renderer>()?.bounds.center ?? band.transform.position;
                if (Mathf.Abs(c.x - PipeX) > 0.01f || Mathf.Abs(c.z - PipeZ) > 0.01f ||
                    Mathf.Abs(c.y - ClampY[i]) > 0.01f)
                    errors.Add($"Pipe band {i} is not centered on the leader: {c}.");
                ValidateAnnularMesh(band, BandOuterDiameter, BandHeight, errors);
                ValidateBinding(band, metal, errors);
            }
            if (plate != null)
            {
                Renderer r = plate.GetComponent<Renderer>();
                if (r != null)
                {
                    float rearFace = r.bounds.min.z;
                    if (Mathf.Abs(rearFace - FacadePlaneZ) > 0.006f)
                        errors.Add($"Wall plate {i} is not seated against facade plane: rearFaceZ={rearFace:F4}.");
                }
                ValidateBinding(plate, metal, errors);
            }
            if (arm != null) ValidateBinding(arm, metal, errors);
            ValidateBracketInterfaces(i, band, arm, plate, errors);
        }

        GameObject receiver = FindSceneObject("HD_DownpipeGroundReceiver");
        if (receiver != null && pipe != null)
        {
            Renderer pipeRenderer = pipe.GetComponent<Renderer>();
            Renderer receiverRenderer = receiver.GetComponent<Renderer>();
            if (pipeRenderer != null && receiverRenderer != null)
            {
                float insertion = receiverRenderer.bounds.max.y - pipeRenderer.bounds.min.y;
                if (insertion < 0.01f || insertion > 0.035f)
                    errors.Add($"Downpipe ground-receiver insertion={insertion:F4}m outside [0.010,0.035]m.");
            }
            ValidateAnnularMesh(receiver, ReceiverOuterDiameter, ReceiverHeight, errors);
            ValidateBinding(receiver, pvc, errors);
        }

        GameObject collar = FindSceneObject("HD_DownpipeRoofCollar");
        if (collar != null)
        {
            ValidateAnnularMesh(collar, 0.105f, 0.024f, errors);
            ValidateBinding(collar, metal, errors);
        }

        ValidateLodPolicy(detailRoot, errors);

        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Rainwater downpipe installation QA FAILED:\n - " + string.Join("\n - ", errors));

        WriteRuntimeReport(pipe, assembly);
        Debug.Log(
            "Rainwater downpipe installation QA passed: hollow manufactured joints/bands/receiver, physical wall restraint, roof connection and four-level accessory LOD policy are source/runtime-valid. Rendered critical defects remain uncleared until native 4K evidence review.");
    }

    private static void ConfigureMacroPipe(GameObject pipe, Material pvc)
    {
        MeshFilter filter = pipe.GetComponent<MeshFilter>();
        Renderer renderer = pipe.GetComponent<Renderer>();
        if (filter == null || renderer == null)
            throw new InvalidOperationException("RainGutter requires MeshFilter + Renderer.");

        float height = PipeTop - PipeBottom;
        filter.sharedMesh = QualityBlockDetailMeshLibrary.GetBeveledCylinder(
            new Vector3(PipeOuterDiameter, height * 0.5f, PipeOuterDiameter), false);
        pipe.transform.localScale = Vector3.one;
        pipe.transform.rotation = Quaternion.identity;
        pipe.transform.position = new Vector3(PipeX, (PipeTop + PipeBottom) * 0.5f, PipeZ);
        renderer.sharedMaterial = pvc;

        CylinderCollider collider = pipe.GetComponent<CylinderCollider>();
        if (collider != null)
        {
            collider.direction = 1;
            collider.center = Vector3.zero;
            collider.radius = PipeOuterDiameter * 0.5f;
            collider.height = height;
        }

        ConfigureWeathering(pipe,
            NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.SunExposed,
            NewTownStainSource.DrainRunoff | NewTownStainSource.UVExposure | NewTownStainSource.GroundSplash,
            1f, 0.70f, 0.52f, 0f);
        EditorUtility.SetDirty(pipe);
        EditorUtility.SetDirty(filter);
        EditorUtility.SetDirty(renderer);
    }

    private static void BuildWallRestraint(Transform root, int index, float y, Material metal)
    {
        GameObject band = CreateAnnularSleeve(
            $"HD_DownpipeClamp_{index}", root, new Vector3(PipeX, y, PipeZ), Quaternion.identity,
            BandOuterDiameter, BandInnerDiameter, BandHeight, metal);
        ConfigureWeathering(band,
            NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.SunExposed,
            NewTownStainSource.FerrousFixture | NewTownStainSource.DrainRunoff | NewTownStainSource.UVExposure,
            0.95f, 0.70f, 0.08f, 0f);

        // Build the arm from the wall plate's outward face to the pipe band's rear face with a controlled
        // 2 mm geometric overlap at each end. This deliberately removes the old detached/floating clamp gap.
        float plateFrontZ = FacadePlaneZ + PlateDepth;
        float bandBackZ = PipeZ - BandOuterDiameter * 0.5f;
        float armBackZ = plateFrontZ - StandoffInterfaceOverlap;
        float armFrontZ = bandBackZ + StandoffInterfaceOverlap;
        float armDepth = armFrontZ - armBackZ;
        if (armDepth <= 0f)
            throw new InvalidOperationException("Computed rainwater standoff arm depth is non-positive.");

        GameObject arm = CreateBox(
            $"HD_DownpipeStandoff_{index}", root,
            new Vector3(PipeX, y, (armBackZ + armFrontZ) * 0.5f),
            new Vector3(0.025f, 0.025f, armDepth), metal);
        ConfigureWeathering(arm,
            NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.Recessed,
            NewTownStainSource.FerrousFixture | NewTownStainSource.RecessGrime,
            0.82f, 0.36f, 0.04f, 0f);

        // 8 mm deep plate: rear face sits at the facade reference plane, projecting outward (+Z).
        GameObject plate = CreateBox(
            $"HD_DownpipeBracketPlate_{index}", root,
            new Vector3(PipeX, y, FacadePlaneZ + PlateDepth * 0.5f),
            new Vector3(0.075f, 0.065f, PlateDepth), metal);
        ConfigureWeathering(plate,
            NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.Recessed,
            NewTownStainSource.FerrousFixture | NewTownStainSource.RecessGrime,
            0.72f, 0.32f, 0.02f, 0f);

        for (int side = -1; side <= 1; side += 2)
        {
            GameObject bolt = CreateCylinder(
                $"HD_DownpipeClampBolt_{index}_{(side < 0 ? "A" : "B")}",
                root,
                new Vector3(PipeX + side * 0.020f, y, FacadePlaneZ + 0.011f),
                0.012f, 0.006f, Quaternion.Euler(90f, 0f, 0f), metal, true);
            ConfigureWeathering(bolt,
                NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.Recessed,
                NewTownStainSource.FerrousFixture | NewTownStainSource.RecessGrime,
                0.72f, 0.30f, 0.02f, 0f);
        }
    }

    private static void BuildRoofOffset(Transform root, Material pvc, Material metal)
    {
        Vector3 a = new Vector3(PipeX, PipeTop - 0.035f, PipeZ);
        Vector3 b = new Vector3(PipeX, PipeTop + 0.055f, FacadePlaneZ + 0.030f);
        GameObject offset = CreatePipeBetween("HD_DownpipeRoofOffset", root, a, b, PipeOuterDiameter, pvc);
        ConfigureWeathering(offset,
            NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.SunExposed,
            NewTownStainSource.DrainRunoff | NewTownStainSource.UVExposure,
            1f, 0.78f, 0.08f, 0f);

        GameObject collar = CreateAnnularSleeve(
            "HD_DownpipeRoofCollar", root,
            new Vector3(PipeX, PipeTop + 0.055f, FacadePlaneZ + 0.010f),
            Quaternion.Euler(90f, 0f, 0f), 0.105f, 0.078f, 0.024f, metal);
        ConfigureWeathering(collar,
            NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.Recessed,
            NewTownStainSource.DrainRunoff | NewTownStainSource.FerrousFixture | NewTownStainSource.RecessGrime,
            0.92f, 0.44f, 0.05f, 0f);
    }

    private static void BuildGroundReceiver(Transform root, Material pvc)
    {
        GameObject receiver = CreateAnnularSleeve(
            "HD_DownpipeGroundReceiver", root,
            new Vector3(PipeX, ReceiverCentreY, PipeZ), Quaternion.identity,
            ReceiverOuterDiameter, ReceiverInnerDiameter, ReceiverHeight, pvc);
        ConfigureWeathering(receiver,
            NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.GroundContact | NewTownSurfaceExposure.Recessed,
            NewTownStainSource.DrainRunoff | NewTownStainSource.GroundSplash | NewTownStainSource.RecessGrime,
            1f, 0.42f, 0.90f, 0f);
    }

    private static GameObject CreatePipeBetween(string name, Transform parent, Vector3 a, Vector3 b,
        float diameter, Material material)
    {
        Vector3 delta = b - a;
        if (delta.sqrMagnitude < 0.000001f)
            throw new InvalidOperationException($"Cannot build zero-length pipe segment {name}.");
        return CreateCylinder(name, parent, (a + b) * 0.5f, diameter, delta.magnitude,
            Quaternion.FromToRotation(Vector3.up, delta.normalized), material, false);
    }

    private static GameObject CreateCylinder(string name, Transform parent, Vector3 worldPosition,
        float diameter, float height, Quaternion worldRotation, Material material, bool fastener)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, true);
        go.transform.position = worldPosition;
        go.transform.rotation = worldRotation;
        go.transform.localScale = Vector3.one;
        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = QualityBlockDetailMeshLibrary.GetBeveledCylinder(
            new Vector3(diameter, height * 0.5f, diameter), fastener);
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        return go;
    }

    private static GameObject CreateAnnularSleeve(string name, Transform parent, Vector3 worldPosition,
        Quaternion worldRotation, float outerDiameter, float innerDiameter, float height, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, true);
        go.transform.position = worldPosition;
        go.transform.rotation = worldRotation;
        go.transform.localScale = Vector3.one;
        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = GetOrCreateAnnularSleeveMesh(outerDiameter, innerDiameter, height, RoundSides);
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        return go;
    }

    private static Mesh GetOrCreateAnnularSleeveMesh(float outerDiameter, float innerDiameter,
        float height, int sides)
    {
        if (outerDiameter <= innerDiameter || innerDiameter <= 0f || height <= 0f || sides < 8)
            throw new InvalidOperationException(
                $"Invalid annular sleeve dimensions OD={outerDiameter}, ID={innerDiameter}, H={height}, sides={sides}.");

        Directory.CreateDirectory(AbsoluteAssetPath(GeneratedMeshRoot));
        string path = $"{GeneratedMeshRoot}/GM_RAIN_Annular_OD{MmKey(outerDiameter)}_ID{MmKey(innerDiameter)}_H{MmKey(height)}_S{sides}.asset";
        Mesh cached = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (cached != null) return cached;

        float ro = outerDiameter * 0.5f;
        float ri = innerDiameter * 0.5f;
        float hy = height * 0.5f;
        var vertices = new List<Vector3>(sides * 16);
        var triangles = new List<int>(sides * 24);
        var uv = new List<Vector2>(sides * 16);

        for (int i = 0; i < sides; i++)
        {
            float a0 = Mathf.PI * 2f * i / sides;
            float a1 = Mathf.PI * 2f * (i + 1) / sides;
            Vector3 o0b = new Vector3(Mathf.Cos(a0) * ro, -hy, Mathf.Sin(a0) * ro);
            Vector3 o1b = new Vector3(Mathf.Cos(a1) * ro, -hy, Mathf.Sin(a1) * ro);
            Vector3 o0t = new Vector3(o0b.x, hy, o0b.z);
            Vector3 o1t = new Vector3(o1b.x, hy, o1b.z);
            Vector3 i0b = new Vector3(Mathf.Cos(a0) * ri, -hy, Mathf.Sin(a0) * ri);
            Vector3 i1b = new Vector3(Mathf.Cos(a1) * ri, -hy, Mathf.Sin(a1) * ri);
            Vector3 i0t = new Vector3(i0b.x, hy, i0b.z);
            Vector3 i1t = new Vector3(i1b.x, hy, i1b.z);

            AddQuad(vertices, triangles, uv, o0b, o1b, o1t, o0t);
            AddQuad(vertices, triangles, uv, i1b, i0b, i0t, i1t);
            AddQuad(vertices, triangles, uv, o0t, o1t, i1t, i0t);
            AddQuad(vertices, triangles, uv, o1b, o0b, i0b, i1b);
        }

        var mesh = new Mesh { name = Path.GetFileNameWithoutExtension(path) };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uv);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static void AddQuad(List<Vector3> vertices, List<int> triangles, List<Vector2> uv,
        Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        int start = vertices.Count;
        vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
        uv.Add(new Vector2(0f, 0f)); uv.Add(new Vector2(1f, 0f));
        uv.Add(new Vector2(1f, 1f)); uv.Add(new Vector2(0f, 1f));
        triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
        triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
    }

    private static GameObject CreateBox(string name, Transform parent, Vector3 worldPosition,
        Vector3 size, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, true);
        go.transform.position = worldPosition;
        go.transform.rotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = QualityBlockDetailMeshLibrary.GetChamferedBox(size);
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        return go;
    }

    private static void RemovePreviousAssembly(Transform detailRoot)
    {
        Transform prior = detailRoot.Find(AssemblyRootName);
        if (prior != null) UnityEngine.Object.DestroyImmediate(prior.gameObject);
    }

    private static void RemoveLegacyDownpipeHardware()
    {
        GameObject[] old = Resources.FindObjectsOfTypeAll<GameObject>()
            .Where(x => x.scene.IsValid())
            .Where(x => x.name.StartsWith("HD_DownpipeClamp_", StringComparison.Ordinal) ||
                        x.name.StartsWith("HD_DownpipeClampBolt_", StringComparison.Ordinal))
            .ToArray();
        foreach (GameObject go in old) UnityEngine.Object.DestroyImmediate(go);
    }

    private static void ConfigureWeathering(GameObject go, NewTownSurfaceExposure exposure,
        NewTownStainSource sources, float rain, float sun, float splash, float contact)
    {
        QualityBlockWeatheringSurface metadata = go.GetComponent<QualityBlockWeatheringSurface>();
        if (metadata == null) metadata = go.AddComponent<QualityBlockWeatheringSurface>();
        metadata.Configure(exposure, sources, rain, sun, splash, contact);
    }

    private static void ValidatePipe(GameObject pipe, Material pvc, List<string> errors)
    {
        Renderer renderer = pipe.GetComponent<Renderer>();
        MeshFilter filter = pipe.GetComponent<MeshFilter>();
        if (renderer == null || filter == null || filter.sharedMesh == null)
        {
            errors.Add("RainGutter requires active Renderer/MeshFilter geometry.");
            return;
        }
        if (!renderer.enabled || !pipe.activeInHierarchy)
            errors.Add("RainGutter macro silhouette must remain actively rendered at all distances.");
        if (!filter.sharedMesh.name.StartsWith("GM_HD_BevelCylinder_", StringComparison.Ordinal))
            errors.Add($"RainGutter must use dimension-baked beveled geometry, got {filter.sharedMesh.name}.");

        Bounds b = renderer.bounds;
        if (Mathf.Abs(b.size.x - PipeOuterDiameter) > 0.004f ||
            Mathf.Abs(b.size.z - PipeOuterDiameter) > 0.004f)
            errors.Add($"RainGutter outside diameter is not {PipeOuterDiameter:F3}m ±0.004m: bounds={b.size}.");
        if (Mathf.Abs(b.min.y - PipeBottom) > 0.01f || Mathf.Abs(b.max.y - PipeTop) > 0.01f)
            errors.Add($"RainGutter vertical extent is wrong: [{b.min.y:F3},{b.max.y:F3}]m.");
        if (Mathf.Abs(b.center.x - PipeX) > 0.01f || Mathf.Abs(b.center.z - PipeZ) > 0.01f)
            errors.Add($"RainGutter centreline is wrong: {b.center}.");

        float wallClearance = b.min.z - FacadePlaneZ;
        if (wallClearance < 0.05f || wallClearance > 0.15f)
            errors.Add($"RainGutter wall clearance={wallClearance:F4}m outside [0.05,0.15]m.");

        ValidateBinding(pipe, pvc, errors);
        if (pipe.transform.parent != null && pipe.transform.parent.name == DetailRootName)
            errors.Add("RainGutter macro body must remain outside DanchiHighDetail accessory LODGroup so LOD2/3 cannot cull the drainage silhouette.");
    }

    private static void ValidateAnnularMesh(GameObject go, float expectedOuterDiameter,
        float expectedHeight, List<string> errors)
    {
        MeshFilter filter = go.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null)
        {
            errors.Add($"{go.name} is missing annular mesh geometry.");
            return;
        }
        if (!filter.sharedMesh.name.StartsWith("GM_RAIN_Annular_", StringComparison.Ordinal))
            errors.Add($"{go.name} is not using hollow annular construction geometry: {filter.sharedMesh.name}.");
        Bounds local = filter.sharedMesh.bounds;
        if (Mathf.Abs(local.size.x - expectedOuterDiameter) > 0.0015f ||
            Mathf.Abs(local.size.z - expectedOuterDiameter) > 0.0015f ||
            Mathf.Abs(local.size.y - expectedHeight) > 0.0015f)
            errors.Add($"{go.name} annular mesh bounds {local.size} do not match OD/H {expectedOuterDiameter:F4}/{expectedHeight:F4}m.");
        if (filter.sharedMesh.vertexCount < RoundSides * 12)
            errors.Add($"{go.name} annular mesh vertex count is too low to contain inner/outer walls and end annuli.");
    }

    private static void ValidateBracketInterfaces(int index, GameObject band, GameObject arm,
        GameObject plate, List<string> errors)
    {
        if (band == null || arm == null || plate == null) return;
        Renderer br = band.GetComponent<Renderer>();
        Renderer ar = arm.GetComponent<Renderer>();
        Renderer pr = plate.GetComponent<Renderer>();
        if (br == null || ar == null || pr == null) return;

        float plateToArmGap = ar.bounds.min.z - pr.bounds.max.z;
        float armToBandGap = br.bounds.min.z - ar.bounds.max.z;
        if (plateToArmGap > 0.003f || plateToArmGap < -0.006f)
            errors.Add($"Bracket {index} plate-to-arm interface gap/overlap={plateToArmGap:F4}m outside [-0.006,0.003]m.");
        if (armToBandGap > 0.003f || armToBandGap < -0.006f)
            errors.Add($"Bracket {index} arm-to-band interface gap/overlap={armToBandGap:F4}m outside [-0.006,0.003]m.");
    }

    private static void ValidateLodPolicy(GameObject detailRoot, List<string> errors)
    {
        if (detailRoot == null) return;
        LODGroup group = detailRoot.GetComponent<LODGroup>();
        if (group == null)
        {
            errors.Add("DanchiHighDetail LODGroup is missing after rainwater reconstruction.");
            return;
        }
        LOD[] lods = group.GetLODs();
        if (lods.Length != 4)
        {
            errors.Add($"Rainwater accessory policy requires four Danchi LOD levels, got {lods.Length}.");
            return;
        }

        bool lod0Joint = lods[0].renderers.Any(x => x != null && x.gameObject.name == "HD_DownpipeJointSleeve_0");
        bool lod1Joint = lods[1].renderers.Any(x => x != null && x.gameObject.name == "LOD1_HD_DownpipeJointSleeve_0");
        bool lod2Joint = lods[2].renderers.Any(x => x != null && x.gameObject.name.Contains("DownpipeJointSleeve"));
        bool lod3Joint = lods[3].renderers.Any(x => x != null && x.gameObject.name.Contains("DownpipeJointSleeve"));
        if (!lod0Joint || !lod1Joint || lod2Joint || lod3Joint)
            errors.Add($"Downpipe joint LOD classification invalid: LOD0={lod0Joint}, LOD1={lod1Joint}, LOD2={lod2Joint}, LOD3={lod3Joint}.");

        bool lod0Bolt = lods[0].renderers.Any(x => x != null && x.gameObject.name == "HD_DownpipeClampBolt_0_A");
        bool lod1Bolt = lods[1].renderers.Any(x => x != null && x.gameObject.name.Contains("DownpipeClampBolt_0_A"));
        if (!lod0Bolt || lod1Bolt)
            errors.Add("Millimetre-scale downpipe fastener must exist in LOD0 and be removed before LOD1.");

        if (group.fadeMode != LODFadeMode.CrossFade || !group.animateCrossFading)
            errors.Add("DanchiHighDetail must retain animated cross-fade for rainwater accessory transitions.");
    }

    private static void ValidateCount(string prefix, int expected, List<string> errors)
    {
        int count = Resources.FindObjectsOfTypeAll<GameObject>()
            .Count(x => x.scene.IsValid() && x.name.StartsWith(prefix, StringComparison.Ordinal));
        if (count != expected) errors.Add($"Expected {expected} source objects with prefix {prefix}, found {count}.");
    }

    private static void ValidateExactCount(string name, int expected, List<string> errors)
    {
        int count = Resources.FindObjectsOfTypeAll<GameObject>()
            .Count(x => x.scene.IsValid() && x.name == name);
        if (count != expected) errors.Add($"Expected {expected} source object(s) named {name}, found {count}.");
    }

    private static void ValidateBinding(GameObject go, Material expected, List<string> errors)
    {
        if (go == null || expected == null) return;
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer == null)
        {
            errors.Add($"{go.name} has no Renderer.");
            return;
        }
        if (renderer.sharedMaterial != expected)
            errors.Add($"{go.name} uses {renderer.sharedMaterial?.name ?? "<null>"}; expected {expected.name}.");
    }

    private static void ValidateMaterialFamilies(Material pvc, Material metal)
    {
        ValidateStandardMaterial(pvc, "PVC-U downpipe", 0f, 0.05f, 0.08f, 0.35f);
        ValidateStandardMaterial(metal, "galvanized bracket", 0.75f, 1.0f, 0.28f, 0.52f);
    }

    private static void ValidateStandardMaterial(Material material, string label,
        float metallicMin, float metallicMax, float smoothnessMin, float smoothnessMax)
    {
        if (material == null || material.shader == null || material.shader.name != "Standard")
            throw new InvalidOperationException($"{label} must use the Standard shader.");
        float metallic = material.GetFloat("_Metallic");
        float smoothness = material.GetFloat("_Glossiness");
        if (metallic < metallicMin - 0.0001f || metallic > metallicMax + 0.0001f)
            throw new InvalidOperationException($"{label} metallic={metallic:F3} outside [{metallicMin:F2},{metallicMax:F2}].");
        if (smoothness < smoothnessMin - 0.0001f || smoothness > smoothnessMax + 0.0001f)
            throw new InvalidOperationException($"{label} smoothness={smoothness:F3} outside [{smoothnessMin:F2},{smoothnessMax:F2}].");
        if (material.HasProperty("_EmissionColor"))
        {
            Color e = material.GetColor("_EmissionColor");
            if (Mathf.Max(e.r, Mathf.Max(e.g, e.b)) > 0.01f)
                throw new InvalidOperationException($"{label} has active emission; daylight construction must not self-light.");
        }
    }

    private static Material RequireMaterial(string path)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
            throw new InvalidOperationException(
                $"Required material {path} is missing. Run the physical material binding pass before rainwater reconstruction.");
        return material;
    }

    private static void WriteRuntimeReport(GameObject pipe, GameObject assembly)
    {
        Renderer r = pipe != null ? pipe.GetComponent<Renderer>() : null;
        int sourceRendererCount = assembly != null ? assembly.GetComponentsInChildren<Renderer>(true).Length : 0;
        var report = new RuntimeReport
        {
            schemaVersion = "1.1.0",
            unityVersion = Application.unityVersion,
            scenePath = ScenePath,
            status = "SOURCE_RUNTIME_QA_PASS_RENDER_REVIEW_PENDING",
            visualFidelityPointsAwarded = 0,
            pipeBoundsCentre = r != null ? r.bounds.center : Vector3.zero,
            pipeBoundsSize = r != null ? r.bounds.size : Vector3.zero,
            sourceAccessoryRendererCount = sourceRendererCount,
            couplerCount = CouplerY.Length,
            clampCount = ClampY.Length,
            fastenerHeadCount = ClampY.Length * 2,
            hollowAnnularInterfaces = true,
            maximumBracketInterfaceGapMetres = 0.003f,
            lodLevels = 4,
            native4KReviewPending = true,
            criticalDefectClearance = "NOT_CLEARED_WITHOUT_NATIVE_4K_PIXELS"
        };

        File.WriteAllText(AbsoluteAssetPath(ReportPath), JsonUtility.ToJson(report, true));
        AssetDatabase.ImportAsset(ReportPath, ImportAssetOptions.ForceUpdate);
    }

    private static void RequireQualityScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() ||
            EditorSceneManager.GetActiveScene().path != ScenePath)
            throw new InvalidOperationException($"Open {ScenePath} before rainwater installation QA.");
    }

    private static string AbsoluteAssetPath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrWhiteSpace(projectRoot))
            throw new InvalidOperationException("Could not resolve Unity project root.");
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private static int MmKey(float metres) => Mathf.RoundToInt(metres * 10000f);

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x.scene.IsValid() && x.scene.path == ScenePath && x.name == name);
    }

    [Serializable]
    private sealed class RuntimeReport
    {
        public string schemaVersion;
        public string unityVersion;
        public string scenePath;
        public string status;
        public int visualFidelityPointsAwarded;
        public Vector3 pipeBoundsCentre;
        public Vector3 pipeBoundsSize;
        public int sourceAccessoryRendererCount;
        public int couplerCount;
        public int clampCount;
        public int fastenerHeadCount;
        public bool hollowAnnularInterfaces;
        public float maximumBracketInterfaceGapMetres;
        public int lodLevels;
        public bool native4KReviewPending;
        public string criticalDefectClearance;
    }
}
