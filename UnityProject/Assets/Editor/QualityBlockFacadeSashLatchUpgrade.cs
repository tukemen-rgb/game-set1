using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Replaces the generated fallback's pair of rubber-looking handle blocks with one physically assembled
/// crescent-style sliding-sash lock and receiver per two-panel apartment window. The circa-2000 benchmark
/// uses a conservative, period-plausible reconstruction: metal base plate, 50 mm mounting pitch, pivot,
/// curved lever, receiver and fasteners are geometry; fine extrusion/oxidation/contact polish remain PBR.
///
/// This pass never touches gameplay collision and never awards Visual Fidelity points. Native 3840x2160
/// frontal/oblique/grazing pixels remain authoritative for scale, seating, highlight response, aliasing and
/// LOD behavior. The first formal reflection fingerprint may create a genuinely missing pass; once armed,
/// root replacement or deletion fails closed rather than mutating evidence while a probe is in flight.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockFacadeSashLatchUpgrade
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string RootName = "FacadeSashLatchHardware";
    private const string ContractPath = "Assets/QA/facade_sash_latch_contract.json";
    private const string SourcePath = "Assets/Editor/QualityBlockFacadeSashLatchUpgrade.cs";
    private const string MeshRoot = "Assets/Art/GeneratedFacadeHardwareMeshes/SashLatch";
    private const string AluminumPath = "Assets/Art/GeneratedDetailMaterials/MAT_AgedAluminum.mat";
    private const string FastenerPath = "Assets/Art/GeneratedDetailMaterials/MAT_DarkGalvanizedSteel.mat";

    private const int ExpectedWindowCount = 30;
    private const int ExpectedLegacyHandleCount = 60;
    private const int Lod0RenderersPerWindow = 6;
    private const int Lod1RenderersPerWindow = 1;
    private const int Lod2RenderersPerWindow = 1;
    private const int Lod3RenderersPerWindow = 1;
    private const int PhaseBins = 8;
    private const float PhaseStepMeters = 0.011f;

    // Local to each HD_BayAssembly. These coordinates inherit the existing 2.36 x 1.76 m sash layout.
    private const float HardwareY = -0.060f;
    private const float HardwareZ = -7.205f;
    private const float LockX = -0.038f;
    private const float KeeperX = 0.030f;
    private const float MountingPitch = 0.050f;

    private const float Lod0Transition = 0.0060f;
    private const float Lod1Transition = 0.0030f;
    private const float Lod2Transition = 0.0012f;
    private const float Lod3Cull = 0.0004f;

    private static bool validating;
    private static int lastValidatedFrame = -1;
    private static bool formalEpochArmed;
    private static int armedRootInstanceId;

    static QualityBlockFacadeSashLatchUpgrade()
    {
        EditorSceneManager.sceneSaving -= OnSceneSaving;
        EditorSceneManager.sceneSaving += OnSceneSaving;
        Camera.onPreCull -= OnCameraPreCull;
        Camera.onPreCull += OnCameraPreCull;
    }

    [MenuItem("NewTown/Geometry/Rebuild Facade Sash Latch Hardware")]
    public static void RebuildForOpenScene()
    {
        EnsureSceneOpen();
        ValidateContractConfigOnly();

        if (IsAuthoredDanchiActive())
        {
            RemoveGeneratedRootIfPresent();
            Debug.Log("Authored danchi replacement is active; generated sash-latch hardware was skipped.");
            return;
        }

        RemoveGeneratedRootIfPresent();
        BuildPreparedHardware();
        ValidateOpenScene();
        formalEpochArmed = false;
        armedRootInstanceId = 0;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "Facade sash-latch hardware rebuilt for 30 apartment windows with physical metal crescent locks, receivers, fasteners and LOD0/1/2/3. " +
            "Visual Fidelity remains UNSCORED pending actual native 4K evidence.");
    }

    /// <summary>
    /// Formal reflection preparation entrypoint. Creation is allowed only for the first missing pass in a
    /// new Editor-domain evidence epoch. After the instance is armed, all later calls are read-only and a
    /// missing/replaced root is a hard failure.
    /// </summary>
    public static void EnsurePreparedForFormalEvidence()
    {
        ValidateContractConfigOnly();
        EnsureBenchmarkSceneIsActive();

        if (IsAuthoredDanchiActive())
            return;

        GameObject root = FindSceneObject(RootName);
        if (!formalEpochArmed)
        {
            if (root == null)
            {
                BuildPreparedHardware();
                root = FindSceneObject(RootName);
            }

            ValidateOpenScene();
            if (root == null)
                throw new InvalidOperationException("Sash-latch root is still missing after formal pre-probe preparation.");
            armedRootInstanceId = root.GetInstanceID();
            formalEpochArmed = true;
            return;
        }

        if (root == null || root.GetInstanceID() != armedRootInstanceId)
            throw new InvalidOperationException(
                "Facade sash-latch hardware changed after the formal reflection epoch was armed. In-flight evidence may not auto-repair missing/replaced latch geometry.");

        ValidateOpenScene();
    }

    [MenuItem("NewTown/QA/Validate Facade Sash Latch Hardware")]
    public static void ValidateOpenScene()
    {
        if (validating) return;
        validating = true;
        try
        {
            EnsureBenchmarkSceneIsActive();
            ValidateContractConfigOnly();

            if (IsAuthoredDanchiActive())
            {
                if (FindSceneObject(RootName) != null)
                    throw new InvalidOperationException("Generated sash-latch hardware must not remain active when authored danchi art is authoritative.");
                return;
            }

            GameObject root = FindSceneObject(RootName);
            if (root == null)
                throw new InvalidOperationException("FacadeSashLatchHardware is missing from the prepared benchmark scene.");
            GameObject danchi = FindSceneObject("Danchi");
            if (danchi == null || root.transform.parent != danchi.transform)
                throw new InvalidOperationException("Facade sash-latch root must be parented directly under Danchi.");

            Material aluminum = AssetDatabase.LoadAssetAtPath<Material>(AluminumPath);
            Material fastener = AssetDatabase.LoadAssetAtPath<Material>(FastenerPath);
            ValidateMaterials(aluminum, fastener);

            MeshRenderer[] legacyHandles = FindLegacyHandleRenderers();
            if (legacyHandles.Length != ExpectedLegacyHandleCount)
                throw new InvalidOperationException($"Expected {ExpectedLegacyHandleCount} legacy rubber handle renderers, found {legacyHandles.Length}.");
            if (legacyHandles.Any(x => x.enabled))
                throw new InvalidOperationException("Legacy rubber handle renderers must remain disabled once the physical sash latch is installed.");

            QualityBlockFacadeSashLatchManifest manifest = root.GetComponent<QualityBlockFacadeSashLatchManifest>();
            if (manifest == null)
                throw new InvalidOperationException("Facade sash-latch manifest is missing.");
            if (manifest.WindowCount != ExpectedWindowCount || manifest.LegacyHandleCount != ExpectedLegacyHandleCount ||
                manifest.Lod0RendererCount != ExpectedWindowCount * Lod0RenderersPerWindow ||
                manifest.Lod1RendererCount != ExpectedWindowCount * Lod1RenderersPerWindow ||
                manifest.Lod2RendererCount != ExpectedWindowCount * Lod2RenderersPerWindow ||
                manifest.Lod3RendererCount != ExpectedWindowCount * Lod3RenderersPerWindow ||
                Mathf.Abs(manifest.MountingPitchMeters - MountingPitch) > 0.0001f)
                throw new InvalidOperationException("Facade sash-latch manifest counts/dimensions drifted from the construction contract.");

            LODGroup[] groups = root.GetComponentsInChildren<LODGroup>(true);
            if (groups.Length != ExpectedWindowCount)
                throw new InvalidOperationException($"Expected {ExpectedWindowCount} per-window sash-latch LODGroups, found {groups.Length}.");

            int lod0 = 0, lod1 = 0, lod2 = 0, lod3 = 0;
            foreach (LODGroup group in groups)
            {
                LOD[] levels = group.GetLODs();
                if (levels.Length != 4)
                    throw new InvalidOperationException("Every sash-latch assembly must have exactly four LOD levels: " + HierarchyPath(group.transform));
                if (levels[0].renderers.Length != Lod0RenderersPerWindow || levels[1].renderers.Length != Lod1RenderersPerWindow ||
                    levels[2].renderers.Length != Lod2RenderersPerWindow || levels[3].renderers.Length != Lod3RenderersPerWindow)
                    throw new InvalidOperationException("Sash-latch LOD renderer assignment drifted: " + HierarchyPath(group.transform));
                if (Mathf.Abs(levels[0].screenRelativeTransitionHeight - Lod0Transition) > 0.0001f ||
                    Mathf.Abs(levels[1].screenRelativeTransitionHeight - Lod1Transition) > 0.0001f ||
                    Mathf.Abs(levels[2].screenRelativeTransitionHeight - Lod2Transition) > 0.0001f ||
                    Mathf.Abs(levels[3].screenRelativeTransitionHeight - Lod3Cull) > 0.0001f)
                    throw new InvalidOperationException("Sash-latch LOD transition thresholds drifted from policy.");
                if (group.fadeMode != LODFadeMode.CrossFade || !group.animateCrossFading)
                    throw new InvalidOperationException("Sash-latch LODs must use animated cross-fade.");
                lod0 += levels[0].renderers.Length;
                lod1 += levels[1].renderers.Length;
                lod2 += levels[2].renderers.Length;
                lod3 += levels[3].renderers.Length;
            }

            if (lod0 != ExpectedWindowCount * Lod0RenderersPerWindow || lod1 != ExpectedWindowCount ||
                lod2 != ExpectedWindowCount || lod3 != ExpectedWindowCount)
                throw new InvalidOperationException("Aggregate sash-latch LOD counts are inconsistent.");

            if (root.GetComponentsInChildren<Collider>(true).Length != 0)
                throw new InvalidOperationException("Sash-latch detail must remain render-only and may not add gameplay colliders.");

            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            int expectedTotal = ExpectedWindowCount * (Lod0RenderersPerWindow + Lod1RenderersPerWindow + Lod2RenderersPerWindow + Lod3RenderersPerWindow);
            if (renderers.Length != expectedTotal)
                throw new InvalidOperationException($"Unexpected sash-latch renderer total: {renderers.Length}, expected {expectedTotal}.");

            foreach (MeshRenderer renderer in renderers)
            {
                if (renderer.sharedMaterial != aluminum && renderer.sharedMaterial != fastener)
                    throw new InvalidOperationException("Sash-latch renderer uses a non-canonical material: " + HierarchyPath(renderer.transform));
                if (renderer.HasPropertyBlock())
                    throw new InvalidOperationException("Sash-latch renderer may not hide material overrides in a MaterialPropertyBlock: " + HierarchyPath(renderer.transform));
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                Mesh mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null || !mesh.name.StartsWith("GM_SashLatch_", StringComparison.Ordinal))
                    throw new InvalidOperationException("Sash-latch renderer uses missing/non-authored geometry: " + HierarchyPath(renderer.transform));
                if (mesh.name == "Cube" || mesh.name == "Cylinder" || mesh.name == "Plane" || mesh.name == "Quad")
                    throw new InvalidOperationException("Stock primitive geometry is forbidden in the sash-latch evidence path.");
                if (mesh.uv == null || mesh.uv.Length != mesh.vertexCount || mesh.normals == null || mesh.normals.Length != mesh.vertexCount ||
                    mesh.tangents == null || mesh.tangents.Length != mesh.vertexCount)
                    throw new InvalidOperationException("Sash-latch mesh is missing UV/normal/tangent data: " + mesh.name);
            }

            int weatheredLod0 = root.GetComponentsInChildren<QualityBlockWeatheringSurface>(true).Length;
            if (weatheredLod0 != ExpectedWindowCount * Lod0RenderersPerWindow)
                throw new InvalidOperationException(
                    $"Cause-based weathering metadata must exist on every LOD0 latch renderer: {weatheredLod0}/{ExpectedWindowCount * Lod0RenderersPerWindow}.");

            Debug.Log(
                "Facade sash-latch QA passed structurally: 30 one-lock-per-two-panel-window assemblies, 50 mm mounting pitch, " +
                "180 LOD0 renderers, metric UV phase variation, 30 per-window 4-level LODGroups, and 60 legacy rubber handles disabled. " +
                "This awards zero Visual Fidelity points; native 4K material/contact/aliasing review is still required.");
        }
        finally
        {
            validating = false;
        }
    }

    [MenuItem("NewTown/QA/Validate Facade Sash Latch Contract")]
    public static void ValidateContractConfigOnly()
    {
        SashLatchContract contract = LoadJson<SashLatchContract>(ContractPath);
        if (contract == null || contract.geometry == null || contract.material == null || contract.qa == null)
            throw new InvalidOperationException("Facade sash-latch contract is null or incomplete.");

        var errors = new List<string>();
        Require(string.Equals(contract.schemaVersion, "1.0", StringComparison.Ordinal), "schemaVersion must be 1.0", errors);
        Require(string.Equals(contract.assemblyId, "apartment_sliding_sash_crescent_latch", StringComparison.Ordinal), "assemblyId mismatch", errors);
        Require(contract.expectedWindowCount == ExpectedWindowCount, $"expectedWindowCount must be {ExpectedWindowCount}", errors);
        Require(contract.geometry.locksPerTwoPanelWindow == 1, "two-panel window must use one crescent lock", errors);
        RequireNear(contract.geometry.mountingScrewPitchM, MountingPitch, 0.0001f, "mountingScrewPitchM", errors);
        RequireNear(contract.geometry.basePlateWidthM, 0.030f, 0.0001f, "basePlateWidthM", errors);
        RequireNear(contract.geometry.basePlateHeightM, 0.082f, 0.0001f, "basePlateHeightM", errors);
        RequireNear(contract.geometry.crescentRadiusM, 0.034f, 0.0001f, "crescentRadiusM", errors);
        Require(string.Equals(contract.material.primaryAssetPath, AluminumPath, StringComparison.Ordinal), "primaryAssetPath mismatch", errors);
        Require(string.Equals(contract.material.fastenerAssetPath, FastenerPath, StringComparison.Ordinal), "fastenerAssetPath mismatch", errors);
        Require(contract.material.primaryMetallicMin >= 0.45f && contract.material.primaryMetallicMax <= 1.0f,
            "primary metal metallic range is invalid", errors);
        Require(contract.material.wetness == 0f, "benchmark sash latch must remain dry", errors);
        Require(contract.qa.lodCount == 4, "qa.lodCount must be four", errors);
        Require(contract.qa.phaseBins == PhaseBins, "qa.phaseBins mismatch", errors);
        RequireNear(contract.qa.phaseStepM, PhaseStepMeters, 0.0001f, "qa.phaseStepM", errors);
        Require(contract.qa.automaticVisualPoints == 0, "qa.automaticVisualPoints must remain zero", errors);
        Require(contract.qa.renderVerificationPending, "qa.renderVerificationPending must remain true", errors);
        Require(contract.qa.disableLegacyRubberHandles, "legacy rubber handles must be disabled", errors);
        Require(contract.qa.requireReflectionFingerprintBinding, "reflection fingerprint binding must remain required", errors);

        foreach (string value in new[]
        {
            contract.manufacture, contract.dimensionsThickness, contract.materialsFinish, contract.mounting,
            contract.interfacesGapsSeals, contract.orientationExposure, contract.aging, contract.geometryVsMaterial,
            contract.lodPolicy, contract.weatheringCausality, contract.lookdevBrief, contract.sourceBasis
        })
            Require(!string.IsNullOrWhiteSpace(value), "mandatory construction/material reasoning field is empty", errors);

        string[] evidence = { "hero/facade_center", "oblique/construction_depth", "grazing/sash_rail_response", "crop/sash_latch_100pct" };
        RequireExactSet(contract.requiredEvidenceRefs, evidence, "requiredEvidenceRefs", errors);
        string[] defects =
        {
            "visible_primitive_placeholder",
            "impossible_material_physics",
            "obvious_repetition",
            "hero_geometry_intersection",
            "severe_aliasing_or_shimmer",
            "visible_lod_pop",
            "missing_construction_material_metadata",
            "unverified_render_claim"
        };
        RequireExactSet(contract.criticalDefectIds, defects, "criticalDefectIds", errors);

        string sourceAbsolute = AbsolutePath(SourcePath);
        if (!File.Exists(sourceAbsolute))
            errors.Add("facade sash-latch source file is missing");
        else
        {
            string source = File.ReadAllText(sourceAbsolute);
            Require(source.Contains("EditorSceneManager.sceneSaving += OnSceneSaving"), "sceneSaving binding token is missing", errors);
            Require(source.Contains("Camera.onPreCull += OnCameraPreCull"), "native-4K pre-cull QA binding token is missing", errors);
            Require(source.Contains("EnsurePreparedForFormalEvidence"), "formal reflection preparation token is missing", errors);
            Require(source.Contains("Visual Fidelity remains UNSCORED"), "source must preserve non-scoring render-pending language", errors);
        }

        if (errors.Count > 0)
            throw new InvalidOperationException("Facade sash-latch contract FAILED:\n - " + string.Join("\n - ", errors));

        Debug.Log("Facade sash-latch contract valid. This is implementation metadata only and awards zero Visual Fidelity points.");
    }

    private static void OnSceneSaving(Scene scene, string path)
    {
        if (!scene.IsValid() || !string.Equals(path, ScenePath, StringComparison.Ordinal) || IsAuthoredDanchiActive())
            return;

        ValidateContractConfigOnly();
        GameObject existing = FindSceneObject(RootName);
        if (existing == null)
            BuildPreparedHardware();
        else
            ValidateOpenScene();
    }

    private static void OnCameraPreCull(Camera camera)
    {
        if (camera == null || camera.targetTexture == null ||
            !camera.targetTexture.name.StartsWith("QA4K_", StringComparison.Ordinal) ||
            !EditorSceneManager.GetActiveScene().IsValid() ||
            !string.Equals(EditorSceneManager.GetActiveScene().path, ScenePath, StringComparison.Ordinal))
            return;
        if (lastValidatedFrame == Time.frameCount)
            return;
        ValidateOpenScene();
        lastValidatedFrame = Time.frameCount;
    }

    private static void BuildPreparedHardware()
    {
        EnsureBenchmarkSceneIsActive();
        if (IsAuthoredDanchiActive()) return;
        ValidateContractConfigOnly();

        GameObject danchi = FindSceneObject("Danchi");
        if (danchi == null)
            throw new InvalidOperationException("Danchi fallback root is missing before sash-latch construction.");
        GameObject detailRoot = FindSceneObject("DanchiHighDetail");
        if (detailRoot == null)
            throw new InvalidOperationException("DanchiHighDetail is missing. Build the detailed sash/frame assembly before installing crescent locks.");

        Material aluminum = AssetDatabase.LoadAssetAtPath<Material>(AluminumPath);
        Material fastener = AssetDatabase.LoadAssetAtPath<Material>(FastenerPath);
        ValidateMaterials(aluminum, fastener);
        Directory.CreateDirectory(MeshRoot);

        MeshRenderer[] legacy = FindLegacyHandleRenderers();
        if (legacy.Length != ExpectedLegacyHandleCount)
            throw new InvalidOperationException($"Cannot replace sash handles: expected {ExpectedLegacyHandleCount} legacy renderers, found {legacy.Length}.");
        foreach (MeshRenderer renderer in legacy)
            renderer.enabled = false;

        var root = new GameObject(RootName);
        root.transform.SetParent(danchi.transform, false);
        int windows = 0, lod0Count = 0, lod1Count = 0, lod2Count = 0, lod3Count = 0;

        for (int floor = 0; floor < 5; floor++)
        for (int bay = 0; bay < 6; bay++)
        {
            GameObject bayRoot = FindSceneObject($"HD_BayAssembly_{floor}_{bay}");
            if (bayRoot == null)
                throw new InvalidOperationException($"Detailed facade bay is missing for sash latch: floor={floor}, bay={bay}.");

            int phaseBin = PositiveHash($"sash-latch:{floor}:{bay}") % PhaseBins;
            GameObject assembly = new GameObject($"SashLatch_{floor}_{bay}");
            assembly.transform.SetParent(root.transform, false);
            assembly.transform.position = bayRoot.transform.position;
            assembly.transform.rotation = bayRoot.transform.rotation;
            assembly.transform.localScale = Vector3.one;

            Transform lod0Root = NewChild(assembly.transform, "LOD0");
            Transform lod1Root = NewChild(assembly.transform, "LOD1");
            Transform lod2Root = NewChild(assembly.transform, "LOD2");
            Transform lod3Root = NewChild(assembly.transform, "LOD3");

            var l0 = new List<Renderer>(Lod0RenderersPerWindow);
            MeshRenderer basePlate = AddRenderer("LatchBase", lod0Root,
                new Vector3(LockX, HardwareY, HardwareZ), Quaternion.identity,
                GetMetricBox(new Vector3(0.030f, 0.082f, 0.010f), phaseBin, "Base"), aluminum, true);
            AttachHardwareWeathering(basePlate.gameObject, 0.28f);
            l0.Add(basePlate);

            MeshRenderer pivot = AddRenderer("LatchPivot", lod0Root,
                new Vector3(LockX, HardwareY, HardwareZ + 0.008f), Quaternion.Euler(90f, 0f, 0f),
                GetMetricCylinder(new Vector3(0.022f, 0.005f, 0.022f), false, phaseBin, "Pivot"), aluminum, true);
            AttachHardwareWeathering(pivot.gameObject, 0.45f);
            l0.Add(pivot);

            MeshRenderer lever = AddRenderer("CrescentLever", lod0Root,
                new Vector3(LockX, HardwareY, HardwareZ + 0.014f), Quaternion.identity,
                GetCrescentLeverMesh(phaseBin), aluminum, true);
            AttachHardwareWeathering(lever.gameObject, 0.82f);
            l0.Add(lever);

            MeshRenderer keeper = AddRenderer("LatchKeeper", lod0Root,
                new Vector3(KeeperX, HardwareY, HardwareZ + 0.002f), Quaternion.identity,
                GetKeeperMesh(phaseBin), aluminum, true);
            AttachHardwareWeathering(keeper.gameObject, 0.24f);
            l0.Add(keeper);

            for (int screw = -1; screw <= 1; screw += 2)
            {
                MeshRenderer fast = AddRenderer($"LatchFastener_{(screw < 0 ? "Low" : "High")}", lod0Root,
                    new Vector3(LockX, HardwareY + screw * MountingPitch * 0.5f, HardwareZ + 0.010f),
                    Quaternion.Euler(90f, 0f, 0f),
                    GetMetricCylinder(new Vector3(0.010f, 0.0025f, 0.010f), true, phaseBin, "Fastener"), fastener, true);
                AttachFastenerWeathering(fast.gameObject);
                l0.Add(fast);
            }

            MeshRenderer l1Renderer = AddRenderer("Latch_LOD1", lod1Root, Vector3.zero, Quaternion.identity,
                GetProxyMesh("L1", phaseBin), aluminum, true);
            MeshRenderer l2Renderer = AddRenderer("Latch_LOD2", lod2Root, Vector3.zero, Quaternion.identity,
                GetProxyMesh("L2", phaseBin), aluminum, false);
            MeshRenderer l3Renderer = AddRenderer("Latch_LOD3", lod3Root, Vector3.zero, Quaternion.identity,
                GetProxyMesh("L3", phaseBin), aluminum, false);

            var group = assembly.AddComponent<LODGroup>();
            group.SetLODs(new[]
            {
                new LOD(Lod0Transition, l0.ToArray()),
                new LOD(Lod1Transition, new Renderer[] { l1Renderer }),
                new LOD(Lod2Transition, new Renderer[] { l2Renderer }),
                new LOD(Lod3Cull, new Renderer[] { l3Renderer })
            });
            group.fadeMode = LODFadeMode.CrossFade;
            group.animateCrossFading = true;
            group.RecalculateBounds();

            windows++;
            lod0Count += l0.Count;
            lod1Count++;
            lod2Count++;
            lod3Count++;
        }

        var manifest = root.AddComponent<QualityBlockFacadeSashLatchManifest>();
        manifest.Configure(windows, legacy.Length, lod0Count, lod1Count, lod2Count, lod3Count,
            MountingPitch, PhaseBins, PhaseStepMeters);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
    }

    private static Mesh GetMetricBox(Vector3 size, int phaseBin, string id)
    {
        string path = $"{MeshRoot}/GM_SashLatch_{id}_Box_{Key(size.x)}_{Key(size.y)}_{Key(size.z)}_P{phaseBin}.asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;
        Mesh mesh = UnityEngine.Object.Instantiate(QualityBlockDetailMeshLibrary.GetChamferedBox(size));
        mesh.name = Path.GetFileNameWithoutExtension(path);
        BakeMetricUv(mesh, phaseBin * PhaseStepMeters);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static Mesh GetMetricCylinder(Vector3 legacyScale, bool fastener, int phaseBin, string id)
    {
        string path = $"{MeshRoot}/GM_SashLatch_{id}_Cylinder_{Key(legacyScale.x)}_{Key(legacyScale.y)}_P{phaseBin}.asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;
        Mesh mesh = UnityEngine.Object.Instantiate(QualityBlockDetailMeshLibrary.GetBeveledCylinder(legacyScale, fastener));
        mesh.name = Path.GetFileNameWithoutExtension(path);
        BakeMetricUv(mesh, phaseBin * PhaseStepMeters);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static Mesh GetCrescentLeverMesh(int phaseBin)
    {
        string path = $"{MeshRoot}/GM_SashLatch_CrescentLever_P{phaseBin}.asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;

        Mesh segment = GetMetricBox(new Vector3(0.010f, 0.028f, 0.010f), phaseBin, "LeverSegment");
        var combines = new List<CombineInstance>();
        float[] angles = { -65f, -39f, -13f, 13f, 39f, 65f };
        const float radius = 0.034f;
        foreach (float angle in angles)
        {
            float r = angle * Mathf.Deg2Rad;
            Vector3 p = new Vector3(Mathf.Cos(r) * radius, Mathf.Sin(r) * radius, 0f);
            combines.Add(new CombineInstance
            {
                mesh = segment,
                transform = Matrix4x4.TRS(p, Quaternion.Euler(0f, 0f, angle), Vector3.one)
            });
        }

        Mesh mesh = new Mesh { name = Path.GetFileNameWithoutExtension(path) };
        mesh.CombineMeshes(combines.ToArray(), true, true, false);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static Mesh GetKeeperMesh(int phaseBin)
    {
        string path = $"{MeshRoot}/GM_SashLatch_Keeper_P{phaseBin}.asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;

        Mesh back = GetMetricBox(new Vector3(0.020f, 0.070f, 0.010f), phaseBin, "KeeperBack");
        Mesh arm = GetMetricBox(new Vector3(0.030f, 0.012f, 0.024f), phaseBin, "KeeperArm");
        var combines = new[]
        {
            new CombineInstance { mesh = back, transform = Matrix4x4.identity },
            new CombineInstance { mesh = arm, transform = Matrix4x4.Translate(new Vector3(0.004f, 0.028f, 0.008f)) },
            new CombineInstance { mesh = arm, transform = Matrix4x4.Translate(new Vector3(0.004f, -0.028f, 0.008f)) }
        };
        Mesh mesh = new Mesh { name = Path.GetFileNameWithoutExtension(path) };
        mesh.CombineMeshes(combines, true, true, false);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static Mesh GetProxyMesh(string lodId, int phaseBin)
    {
        string path = $"{MeshRoot}/GM_SashLatch_{lodId}_Proxy_P{phaseBin}.asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;

        var combines = new List<CombineInstance>();
        if (lodId == "L1")
        {
            Mesh basePlate = GetMetricBox(new Vector3(0.030f, 0.080f, 0.008f), phaseBin, "L1Base");
            Mesh lever = GetMetricBox(new Vector3(0.012f, 0.070f, 0.008f), phaseBin, "L1Lever");
            Mesh keeper = GetMetricBox(new Vector3(0.024f, 0.066f, 0.010f), phaseBin, "L1Keeper");
            combines.Add(new CombineInstance { mesh = basePlate, transform = Matrix4x4.Translate(new Vector3(LockX, HardwareY, HardwareZ)) });
            combines.Add(new CombineInstance { mesh = lever, transform = Matrix4x4.TRS(new Vector3(-0.014f, HardwareY + 0.006f, HardwareZ + 0.012f), Quaternion.Euler(0f, 0f, -32f), Vector3.one) });
            combines.Add(new CombineInstance { mesh = keeper, transform = Matrix4x4.Translate(new Vector3(KeeperX, HardwareY, HardwareZ + 0.002f)) });
        }
        else if (lodId == "L2")
        {
            Mesh lockProxy = GetMetricBox(new Vector3(0.070f, 0.075f, 0.007f), phaseBin, "L2Body");
            Mesh keeperProxy = GetMetricBox(new Vector3(0.020f, 0.060f, 0.007f), phaseBin, "L2Keeper");
            combines.Add(new CombineInstance { mesh = lockProxy, transform = Matrix4x4.Translate(new Vector3(-0.012f, HardwareY, HardwareZ + 0.006f)) });
            combines.Add(new CombineInstance { mesh = keeperProxy, transform = Matrix4x4.Translate(new Vector3(KeeperX, HardwareY, HardwareZ + 0.004f)) });
        }
        else
        {
            Mesh sliver = GetMetricBox(new Vector3(0.080f, 0.060f, 0.004f), phaseBin, "L3Sliver");
            combines.Add(new CombineInstance { mesh = sliver, transform = Matrix4x4.Translate(new Vector3(-0.002f, HardwareY, HardwareZ + 0.006f)) });
        }

        Mesh mesh = new Mesh { name = Path.GetFileNameWithoutExtension(path) };
        mesh.CombineMeshes(combines.ToArray(), true, true, false);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
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

    private static MeshRenderer AddRenderer(string name, Transform parent, Vector3 localPosition,
        Quaternion localRotation, Mesh mesh, Material material, bool castsShadows)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = localRotation;
        go.transform.localScale = Vector3.one;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = castsShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        renderer.receiveShadows = true;
        renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbesAndSkybox;
        return renderer;
    }

    private static void AttachHardwareWeathering(GameObject go, float contact)
    {
        var metadata = go.AddComponent<QualityBlockWeatheringSurface>();
        metadata.Configure(
            NewTownSurfaceExposure.Sheltered | NewTownSurfaceExposure.Recessed,
            NewTownStainSource.RecessGrime | NewTownStainSource.UVExposure,
            0.08f, 0.22f, 0f, contact);
    }

    private static void AttachFastenerWeathering(GameObject go)
    {
        var metadata = go.AddComponent<QualityBlockWeatheringSurface>();
        metadata.Configure(
            NewTownSurfaceExposure.Sheltered | NewTownSurfaceExposure.Recessed,
            NewTownStainSource.RecessGrime | NewTownStainSource.FerrousFixture,
            0.06f, 0.18f, 0f, 0.36f);
    }

    private static void ValidateMaterials(Material aluminum, Material fastener)
    {
        if (aluminum == null || fastener == null)
            throw new InvalidOperationException("Canonical sash-latch metal materials are missing. Build Danchi detail/PBR materials first.");
        foreach (Material mat in new[] { aluminum, fastener })
        {
            if (mat.shader == null || mat.shader.name != "Standard")
                throw new InvalidOperationException("Sash-latch metal must use the canonical Standard-shader material: " + mat.name);
            if (mat.GetFloat("_Metallic") < 0.45f || mat.GetFloat("_Metallic") > 1.0f)
                throw new InvalidOperationException("Sash-latch metal metallic value is physically implausible: " + mat.name);
            if (mat.GetFloat("_Glossiness") < 0.18f || mat.GetFloat("_Glossiness") > 0.72f)
                throw new InvalidOperationException("Sash-latch metal smoothness is outside the aged hardware range: " + mat.name);
            if (mat.IsKeywordEnabled("_EMISSION") || mat.GetColor("_EmissionColor").maxColorComponent > 0.001f)
                throw new InvalidOperationException("Sash-latch hardware may not use emission or painted highlight energy: " + mat.name);
        }
    }

    private static MeshRenderer[] FindLegacyHandleRenderers()
    {
        return Resources.FindObjectsOfTypeAll<MeshRenderer>()
            .Where(x => x != null && x.gameObject.scene.IsValid() &&
                        string.Equals(x.gameObject.scene.path, ScenePath, StringComparison.Ordinal) &&
                        x.gameObject.name.StartsWith("HD_WindowHandle_", StringComparison.Ordinal))
            .ToArray();
    }

    private static bool IsAuthoredDanchiActive()
    {
        QualityBlockArtSlot slot = Resources.FindObjectsOfTypeAll<QualityBlockArtSlot>()
            .FirstOrDefault(x => x != null && x.gameObject.scene.IsValid() && x.SlotId == "danchi.main");
        return slot != null && slot.IsUsingAuthoredArt;
    }

    private static void RemoveGeneratedRootIfPresent()
    {
        GameObject old = FindSceneObject(RootName);
        if (old != null)
            UnityEngine.Object.DestroyImmediate(old);
    }

    private static Transform NewChild(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x != null && x.scene.IsValid() &&
                                 string.Equals(x.scene.path, ScenePath, StringComparison.Ordinal) &&
                                 string.Equals(x.name, name, StringComparison.Ordinal));
    }

    private static void EnsureSceneOpen()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() ||
            !string.Equals(EditorSceneManager.GetActiveScene().path, ScenePath, StringComparison.Ordinal))
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static void EnsureBenchmarkSceneIsActive()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() ||
            !string.Equals(EditorSceneManager.GetActiveScene().path, ScenePath, StringComparison.Ordinal))
            throw new InvalidOperationException("Facade sash-latch evidence may run only in the already-prepared QualityBlock1990s scene.");
    }

    private static T LoadJson<T>(string assetPath)
    {
        string absolute = AbsolutePath(assetPath);
        if (!File.Exists(absolute))
            throw new FileNotFoundException("Required sash-latch QA file not found: " + assetPath);
        T value = JsonUtility.FromJson<T>(File.ReadAllText(absolute));
        if (value == null)
            throw new InvalidOperationException("Could not parse sash-latch QA JSON: " + assetPath);
        return value;
    }

    private static string AbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrWhiteSpace(projectRoot))
            throw new InvalidOperationException("Could not resolve Unity project root for sash-latch QA.");
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private static int PositiveHash(string value)
    {
        unchecked
        {
            int h = 17;
            for (int i = 0; i < value.Length; i++) h = h * 31 + value[i];
            return h & int.MaxValue;
        }
    }

    private static int Key(float meters) => Mathf.RoundToInt(Mathf.Abs(meters) * 10000f);

    private static string HierarchyPath(Transform transform)
    {
        var names = new Stack<string>();
        Transform t = transform;
        while (t != null)
        {
            names.Push(t.name);
            t = t.parent;
        }
        return string.Join("/", names.ToArray());
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
        if (actual == null || actual.Any(string.IsNullOrWhiteSpace) || actual.Distinct(StringComparer.Ordinal).Count() != actual.Length ||
            !new HashSet<string>(actual, StringComparer.Ordinal).SetEquals(expected))
            errors.Add(label + " must be exactly [" + string.Join(", ", expected) + "]");
    }

    [Serializable]
    private sealed class SashLatchContract
    {
        public string schemaVersion;
        public string assemblyId;
        public int expectedWindowCount;
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
        public MaterialPolicy material;
        public QaPolicy qa;
        public string[] requiredEvidenceRefs;
        public string[] criticalDefectIds;
    }

    [Serializable]
    private sealed class Geometry
    {
        public int locksPerTwoPanelWindow;
        public float mountingScrewPitchM;
        public float basePlateWidthM;
        public float basePlateHeightM;
        public float crescentRadiusM;
    }

    [Serializable]
    private sealed class MaterialPolicy
    {
        public string primaryAssetPath;
        public string fastenerAssetPath;
        public float primaryMetallicMin;
        public float primaryMetallicMax;
        public float roughnessMin;
        public float roughnessMax;
        public float normalScale;
        public float microstructureMm;
        public float wetness;
        public string uvAging;
        public string angularFresnelResponse;
    }

    [Serializable]
    private sealed class QaPolicy
    {
        public int lodCount;
        public int phaseBins;
        public float phaseStepM;
        public int automaticVisualPoints;
        public bool renderVerificationPending;
        public bool disableLegacyRubberHandles;
        public bool requireReflectionFingerprintBinding;
    }
}

[DisallowMultipleComponent]
public sealed class QualityBlockFacadeSashLatchManifest : MonoBehaviour
{
    [SerializeField] private int windowCount;
    [SerializeField] private int legacyHandleCount;
    [SerializeField] private int lod0RendererCount;
    [SerializeField] private int lod1RendererCount;
    [SerializeField] private int lod2RendererCount;
    [SerializeField] private int lod3RendererCount;
    [SerializeField] private float mountingPitchMeters;
    [SerializeField] private int phaseBins;
    [SerializeField] private float phaseStepMeters;

    public int WindowCount => windowCount;
    public int LegacyHandleCount => legacyHandleCount;
    public int Lod0RendererCount => lod0RendererCount;
    public int Lod1RendererCount => lod1RendererCount;
    public int Lod2RendererCount => lod2RendererCount;
    public int Lod3RendererCount => lod3RendererCount;
    public float MountingPitchMeters => mountingPitchMeters;

    public void Configure(int windows, int legacyHandles, int lod0, int lod1, int lod2, int lod3,
        float mountingPitch, int phases, float phaseStep)
    {
        windowCount = windows;
        legacyHandleCount = legacyHandles;
        lod0RendererCount = lod0;
        lod1RendererCount = lod1;
        lod2RendererCount = lod2;
        lod3RendererCount = lod3;
        mountingPitchMeters = mountingPitch;
        phaseBins = phases;
        phaseStepMeters = phaseStep;
    }
}