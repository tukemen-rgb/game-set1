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
/// Replaces the fallback apartment window's single slab-like sill/drip block with a manufactured
/// aluminum sill tray, segmented exterior skirt, real drainage openings and a projecting drip nose.
/// The opening darkness must come from physical recess/occlusion, never a painted black highlight.
///
/// This is implementation-readiness work only. It awards zero Visual Fidelity points; native
/// 3840x2160 frontal/oblique/grazing renders and temporal evidence remain authoritative.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockFacadeSillDrainageUpgrade
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string RootName = "FacadeSillDrainageInterfaces";
    private const string ContractPath = "Assets/QA/facade_sill_drainage_contract.json";
    private const string LookdevPath = "Assets/QA/lookdev/facade_sill_drainage_lookdev.svg";
    private const string SourcePath = "Assets/Editor/QualityBlockFacadeSillDrainageUpgrade.cs";
    private const string MeshRoot = "Assets/Art/GeneratedFacadeHardwareMeshes/SillDrainage";
    private const string AluminumPath = "Assets/Art/GeneratedDetailMaterials/MAT_AgedAluminum.mat";

    private const int ExpectedWindowCount = 30;
    private const int ExpectedLegacySillCount = 30;
    private const int Lod0RenderersPerWindow = 5;
    private const int LodNRenderersPerWindow = 1;
    private const int PhaseBins = 8;
    private const float PhaseStepMeters = 0.017f;

    // All dimensions below are conservative benchmark dimensional assumptions, not a claim that
    // one identified historic product used these exact dimensions.
    private const float SillWidth = 2.460f;
    private const float TrayDepth = 0.195f;
    private const float TrayThickness = 0.020f;
    private const float TrayFall = 0.008f;
    private const float ExteriorSkirtHeight = 0.044f;
    private const float ExteriorSkirtDepth = 0.018f;
    private const float DripNoseHeight = 0.012f;
    private const float DripNoseDepth = 0.032f;
    private const float DrainSlotWidth = 0.050f;
    private const float DrainSlotCenterX = 0.620f;
    private const float DrainRecessDepth = 0.023f;
    private const float ControlledFrameSeat = 0.008f;

    // Local coordinates in each HD_BayAssembly. The existing bottom frame finishes near y=-0.88.
    // The tray is seated into that interface and slopes eight millimetres toward the exterior (+Z).
    private const float OriginY = -0.895f;
    private const float OriginZ = -7.235f;
    private const float TrayBackZ = -0.100f;
    private const float TrayFrontZ = 0.095f;
    private const float TrayTopBackY = 0.023f;
    private const float TrayTopFrontY = 0.015f;
    private const float TrayBottomBackY = 0.003f;
    private const float TrayBottomFrontY = -0.005f;
    private const float SkirtCenterZ = 0.128f;
    private const float SkirtCenterY = -0.006f;
    private const float NoseCenterZ = 0.142f;
    private const float NoseCenterY = -0.032f;

    // Same manufactured silhouette through all levels; LOD1/2/3 consolidate five renderers into one.
    // This deliberately spends a small amount of triangle budget to avoid topology snap in the benchmark.
    private const float Lod0Transition = 0.055f;
    private const float Lod1Transition = 0.025f;
    private const float Lod2Transition = 0.010f;
    private const float Lod3Cull = 0.004f;

    private static bool validating;
    private static int lastValidatedFrame = -1;

    static QualityBlockFacadeSillDrainageUpgrade()
    {
        EditorSceneManager.sceneSaving -= OnSceneSaving;
        EditorSceneManager.sceneSaving += OnSceneSaving;
        Camera.onPreCull -= OnCameraPreCull;
        Camera.onPreCull += OnCameraPreCull;
    }

    [MenuItem("NewTown/Geometry/Rebuild Facade Sill Drainage Interfaces")]
    public static void RebuildForOpenScene()
    {
        EnsureSceneOpen();
        ValidateContractConfigOnly();

        if (IsAuthoredDanchiActive())
        {
            RemoveGeneratedRootIfPresent();
            Debug.Log("Authored danchi replacement is active; generated sill-drainage interfaces were skipped.");
            return;
        }

        RemoveGeneratedRootIfPresent();
        BuildPreparedAssembly();
        ValidateOpenScene();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.Refresh();

        Debug.Log(
            "Facade sill-drainage interfaces rebuilt for 30 apartment windows with sloped aluminum trays, " +
            "physical drainage openings, drip noses and four LOD levels. Visual Fidelity remains UNSCORED " +
            "pending actual native 4K evidence.");
    }

    /// <summary>
    /// Safe entrypoint for other formal-preparation code. It may build a genuinely missing root before
    /// rendering, but it never assigns visual points and later native-4K pre-cull still revalidates state.
    /// </summary>
    public static void EnsurePreparedForFormalEvidence()
    {
        EnsureBenchmarkSceneIsActive();
        ValidateContractConfigOnly();
        if (IsAuthoredDanchiActive()) return;

        GameObject root = FindSceneObject(RootName);
        MeshRenderer[] legacy = FindLegacySillRenderers();
        if (root == null || legacy.Any(x => x.enabled))
        {
            RemoveGeneratedRootIfPresent();
            BuildPreparedAssembly();
        }
        ValidateOpenScene();
    }

    [MenuItem("NewTown/QA/Validate Facade Sill Drainage Interfaces")]
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
                    throw new InvalidOperationException(
                        "Generated sill-drainage root must not remain active when authored danchi art is authoritative.");
                return;
            }

            GameObject danchi = FindSceneObject("Danchi");
            GameObject detail = FindSceneObject("DanchiHighDetail");
            GameObject root = FindSceneObject(RootName);
            if (danchi == null || detail == null || root == null)
                throw new InvalidOperationException("Danchi, DanchiHighDetail or sill-drainage root is missing.");
            if (root.transform.parent != danchi.transform)
                throw new InvalidOperationException("Facade sill-drainage root must be parented directly under Danchi.");

            Material aluminum = AssetDatabase.LoadAssetAtPath<Material>(AluminumPath);
            ValidateAluminum(aluminum);

            MeshRenderer[] legacy = FindLegacySillRenderers();
            if (legacy.Length != ExpectedLegacySillCount)
                throw new InvalidOperationException(
                    $"Expected {ExpectedLegacySillCount} legacy HD_WindowSillDrip renderers, found {legacy.Length}.");
            if (legacy.Any(x => x.enabled))
                throw new InvalidOperationException(
                    "Legacy slab-like HD_WindowSillDrip renderers must remain disabled after the manufactured sill is installed.");

            QualityBlockFacadeSillDrainageManifest manifest =
                root.GetComponent<QualityBlockFacadeSillDrainageManifest>();
            if (manifest == null)
                throw new InvalidOperationException("Facade sill-drainage manifest is missing.");
            if (manifest.WindowCount != ExpectedWindowCount ||
                manifest.LegacySillCount != ExpectedLegacySillCount ||
                manifest.Lod0RendererCount != ExpectedWindowCount * Lod0RenderersPerWindow ||
                manifest.Lod1RendererCount != ExpectedWindowCount ||
                manifest.Lod2RendererCount != ExpectedWindowCount ||
                manifest.Lod3RendererCount != ExpectedWindowCount ||
                Mathf.Abs(manifest.DrainSlotWidthMeters - DrainSlotWidth) > 0.0001f ||
                Mathf.Abs(manifest.TrayFallMeters - TrayFall) > 0.0001f)
                throw new InvalidOperationException(
                    "Facade sill-drainage manifest counts/dimensions drifted from the construction contract.");

            LODGroup[] groups = root.GetComponentsInChildren<LODGroup>(true);
            if (groups.Length != ExpectedWindowCount)
                throw new InvalidOperationException(
                    $"Expected {ExpectedWindowCount} per-window sill LODGroups, found {groups.Length}.");

            int lod0 = 0, lod1 = 0, lod2 = 0, lod3 = 0;
            foreach (LODGroup group in groups)
            {
                LOD[] lods = group.GetLODs();
                if (lods.Length != 4)
                    throw new InvalidOperationException(
                        "Every sill-drainage assembly must have four LOD levels: " + HierarchyPath(group.transform));
                if (lods[0].renderers.Length != Lod0RenderersPerWindow ||
                    lods[1].renderers.Length != LodNRenderersPerWindow ||
                    lods[2].renderers.Length != LodNRenderersPerWindow ||
                    lods[3].renderers.Length != LodNRenderersPerWindow)
                    throw new InvalidOperationException(
                        "Sill-drainage LOD renderer assignment drifted: " + HierarchyPath(group.transform));
                if (Mathf.Abs(lods[0].screenRelativeTransitionHeight - Lod0Transition) > 0.0001f ||
                    Mathf.Abs(lods[1].screenRelativeTransitionHeight - Lod1Transition) > 0.0001f ||
                    Mathf.Abs(lods[2].screenRelativeTransitionHeight - Lod2Transition) > 0.0001f ||
                    Mathf.Abs(lods[3].screenRelativeTransitionHeight - Lod3Cull) > 0.0001f)
                    throw new InvalidOperationException(
                        "Sill-drainage LOD thresholds drifted from the anti-pop policy.");
                if (group.fadeMode != LODFadeMode.CrossFade || !group.animateCrossFading)
                    throw new InvalidOperationException(
                        "Sill-drainage LODs must use animated cross-fade.");

                lod0 += lods[0].renderers.Length;
                lod1 += lods[1].renderers.Length;
                lod2 += lods[2].renderers.Length;
                lod3 += lods[3].renderers.Length;
            }

            if (lod0 != ExpectedWindowCount * Lod0RenderersPerWindow ||
                lod1 != ExpectedWindowCount || lod2 != ExpectedWindowCount || lod3 != ExpectedWindowCount)
                throw new InvalidOperationException("Aggregate sill-drainage LOD counts are inconsistent.");

            if (root.GetComponentsInChildren<Collider>(true).Length != 0)
                throw new InvalidOperationException(
                    "Facade sill-drainage detail is render-only and may not add gameplay colliders.");

            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            int expectedTotal = ExpectedWindowCount *
                                (Lod0RenderersPerWindow + 3 * LodNRenderersPerWindow);
            if (renderers.Length != expectedTotal)
                throw new InvalidOperationException(
                    $"Unexpected sill-drainage renderer total: {renderers.Length}, expected {expectedTotal}.");

            foreach (MeshRenderer renderer in renderers)
            {
                if (renderer.sharedMaterial != aluminum)
                    throw new InvalidOperationException(
                        "Every sill-drainage renderer must use canonical aged aluminum: " +
                        HierarchyPath(renderer.transform));
                if (renderer.HasPropertyBlock())
                    throw new InvalidOperationException(
                        "Sill-drainage renderers may not hide material overrides in MaterialPropertyBlocks: " +
                        HierarchyPath(renderer.transform));

                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                Mesh mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null || !mesh.name.StartsWith("GM_FacadeSill_", StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "Sill-drainage renderer uses missing/non-authored geometry: " +
                        HierarchyPath(renderer.transform));
                if (mesh.name == "Cube" || mesh.name == "Cylinder" ||
                    mesh.name == "Plane" || mesh.name == "Quad")
                    throw new InvalidOperationException(
                        "Stock primitive geometry is forbidden in the sill-drainage evidence path.");
                if (mesh.uv == null || mesh.uv.Length != mesh.vertexCount ||
                    mesh.normals == null || mesh.normals.Length != mesh.vertexCount ||
                    mesh.tangents == null || mesh.tangents.Length != mesh.vertexCount)
                    throw new InvalidOperationException(
                        "Sill-drainage mesh is missing UV/normal/tangent data: " + mesh.name);
            }

            int weatheringCount =
                root.GetComponentsInChildren<QualityBlockWeatheringSurface>(true).Length;
            if (weatheringCount != expectedTotal)
                throw new InvalidOperationException(
                    $"Cause-based weathering metadata must exist on every sill renderer: " +
                    $"{weatheringCount}/{expectedTotal}.");

            for (int floor = 0; floor < 5; floor++)
            for (int bay = 0; bay < 6; bay++)
            {
                Transform assembly = root.transform.Find($"SillDrainage_{floor}_{bay}");
                if (assembly == null)
                    throw new InvalidOperationException(
                        $"Sill-drainage assembly missing: floor={floor}, bay={bay}.");
                ValidateDrainageTopology(assembly);
            }

            Debug.Log(
                "Facade sill-drainage QA passed structurally: 30 windows, sloped 20 mm aluminum trays, " +
                "two 50 mm physical drain openings per sill, projecting drip noses, eight metric-UV phases, " +
                "four anti-pop LOD levels and 30 legacy slab sills disabled. This awards zero Visual Fidelity " +
                "points; native 4K drainage/contact/grazing/temporal review is still required.");
        }
        finally
        {
            validating = false;
        }
    }

    [MenuItem("NewTown/QA/Validate Facade Sill Drainage Contract")]
    public static void ValidateContractConfigOnly()
    {
        SillContract contract = LoadJson<SillContract>(ContractPath);
        if (contract == null || contract.geometry == null ||
            contract.material == null || contract.qa == null)
            throw new InvalidOperationException(
                "Facade sill-drainage contract is null or incomplete.");

        var errors = new List<string>();
        Require(contract.schemaVersion == "1.0", "schemaVersion must be 1.0", errors);
        Require(contract.assemblyId == "apartment_sliding_sash_sill_drainage",
            "assemblyId mismatch", errors);
        Require(contract.expectedWindowCount == ExpectedWindowCount,
            $"expectedWindowCount must be {ExpectedWindowCount}", errors);

        RequireNear(contract.geometry.sillWidthM, SillWidth, 0.0001f, "sillWidthM", errors);
        RequireNear(contract.geometry.trayDepthM, TrayDepth, 0.0001f, "trayDepthM", errors);
        RequireNear(contract.geometry.trayThicknessM, TrayThickness, 0.0001f, "trayThicknessM", errors);
        RequireNear(contract.geometry.outwardFallM, TrayFall, 0.0001f, "outwardFallM", errors);
        RequireNear(contract.geometry.exteriorSkirtHeightM, ExteriorSkirtHeight, 0.0001f,
            "exteriorSkirtHeightM", errors);
        RequireNear(contract.geometry.drainSlotWidthM, DrainSlotWidth, 0.0001f,
            "drainSlotWidthM", errors);
        Require(contract.geometry.drainSlotCount == 2, "drainSlotCount must be two", errors);
        RequireNear(contract.geometry.recessDepthBehindDrainSlotM, DrainRecessDepth, 0.0001f,
            "recessDepthBehindDrainSlotM", errors);
        RequireNear(contract.geometry.controlledFrameSeatM, ControlledFrameSeat, 0.0001f,
            "controlledFrameSeatM", errors);

        Require(contract.material.assetPath == AluminumPath, "material assetPath mismatch", errors);
        Require(contract.material.metallicMin >= 0.45f &&
                contract.material.metallicMax <= 1.0f &&
                contract.material.metallicMin <= contract.material.metallicMax,
            "aluminum metallic range is invalid", errors);
        Require(contract.material.roughnessMin >= 0.30f &&
                contract.material.roughnessMax <= 0.80f &&
                contract.material.roughnessMin <= contract.material.roughnessMax,
            "aluminum roughness range is invalid", errors);
        Require(contract.material.wetness == 0f,
            "dry benchmark sill wetness must remain zero", errors);
        Require(!string.IsNullOrWhiteSpace(contract.material.albedo),
            "material albedo description is missing", errors);
        Require(!string.IsNullOrWhiteSpace(contract.material.specularFresnel),
            "material angular/Fresnel response is missing", errors);
        Require(!string.IsNullOrWhiteSpace(contract.material.normalScale),
            "material normal scale is missing", errors);
        Require(!string.IsNullOrWhiteSpace(contract.material.microstructure),
            "material microstructure is missing", errors);
        Require(!string.IsNullOrWhiteSpace(contract.material.uvAging),
            "material UV-aging policy is missing", errors);

        Require(contract.qa.lodCount == 4, "qa.lodCount must be four", errors);
        Require(contract.qa.phaseBins == PhaseBins, "qa.phaseBins mismatch", errors);
        RequireNear(contract.qa.phaseStepM, PhaseStepMeters, 0.0001f, "qa.phaseStepM", errors);
        Require(contract.qa.automaticVisualPoints == 0,
            "qa.automaticVisualPoints must remain zero", errors);
        Require(contract.qa.renderVerificationPending,
            "qa.renderVerificationPending must remain true before real render review", errors);
        Require(contract.qa.disableLegacySillSlabs,
            "legacy sill slabs must be disabled", errors);
        Require(contract.qa.requireSceneSaveBinding,
            "scene-save preparation binding must remain required", errors);
        Require(contract.qa.requireNative4KPreCullValidation,
            "native-4K pre-cull validation must remain required", errors);
        Require(contract.qa.requirePhysicalDrainOpening,
            "drain openings may not be painted or texture-only", errors);
        Require(contract.qa.preserveDrainOpeningTopologyThroughLod3,
            "drain topology must persist through LOD3", errors);

        foreach (string value in new[]
        {
            contract.manufacture, contract.dimensionsThickness, contract.materialsFinish,
            contract.mounting, contract.interfacesGapsSeals, contract.orientationExposure,
            contract.aging, contract.geometryVsMaterial, contract.lodPolicy,
            contract.weatheringCausality, contract.lookdevBrief, contract.sourceBasis
        })
            Require(!string.IsNullOrWhiteSpace(value),
                "mandatory construction/material reasoning field is empty", errors);

        string[] evidence =
        {
            "hero/facade_center",
            "oblique/construction_depth",
            "grazing/sash_rail_response",
            "temporal/oblique"
        };
        RequireExactSet(contract.requiredEvidenceRefs, evidence, "requiredEvidenceRefs", errors);

        string[] defects =
        {
            "visible_primitive_placeholder",
            "baked_or_painted_highlights",
            "impossible_material_physics",
            "obvious_repetition",
            "hero_geometry_intersection",
            "severe_aliasing_or_shimmer",
            "visible_lod_pop",
            "major_light_leak",
            "missing_construction_material_metadata",
            "unverified_render_claim"
        };
        RequireExactSet(contract.criticalDefectIds, defects, "criticalDefectIds", errors);

        if (!File.Exists(AbsolutePath(LookdevPath)))
            errors.Add("facade sill-drainage lookdev illustration is missing");
        string sourceAbsolute = AbsolutePath(SourcePath);
        if (!File.Exists(sourceAbsolute))
            errors.Add("facade sill-drainage source file is missing");
        else
        {
            string source = File.ReadAllText(sourceAbsolute);
            Require(source.Contains("EditorSceneManager.sceneSaving += OnSceneSaving"),
                "sceneSaving binding token is missing", errors);
            Require(source.Contains("Camera.onPreCull += OnCameraPreCull"),
                "native-4K pre-cull binding token is missing", errors);
            Require(source.Contains("Visual Fidelity remains UNSCORED"),
                "source must preserve non-scoring render-pending language", errors);
        }

        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Facade sill-drainage contract FAILED:\n - " + string.Join("\n - ", errors));

        Debug.Log(
            "Facade sill-drainage contract valid. This is implementation metadata only and awards zero Visual Fidelity points.");
    }

    private static void OnSceneSaving(Scene scene, string path)
    {
        if (!scene.IsValid() || !string.Equals(path, ScenePath, StringComparison.Ordinal) ||
            IsAuthoredDanchiActive())
            return;

        ValidateContractConfigOnly();
        GameObject detail = FindSceneObject("DanchiHighDetail");
        if (detail == null) return;

        GameObject root = FindSceneObject(RootName);
        MeshRenderer[] legacy = FindLegacySillRenderers();
        if (root == null || legacy.Any(x => x.enabled))
        {
            RemoveGeneratedRootIfPresent();
            BuildPreparedAssembly();
        }
        else
        {
            ValidateOpenScene();
        }
    }

    private static void OnCameraPreCull(Camera camera)
    {
        if (camera == null || camera.targetTexture == null ||
            !(camera.targetTexture.name.StartsWith("QA4K_", StringComparison.Ordinal) ||
              camera.targetTexture.name.StartsWith("QATemporal_", StringComparison.Ordinal)) ||
            !EditorSceneManager.GetActiveScene().IsValid() ||
            !string.Equals(EditorSceneManager.GetActiveScene().path, ScenePath, StringComparison.Ordinal))
            return;
        if (lastValidatedFrame == Time.frameCount) return;

        ValidateOpenScene();
        lastValidatedFrame = Time.frameCount;
    }

    private static void BuildPreparedAssembly()
    {
        EnsureBenchmarkSceneIsActive();
        ValidateContractConfigOnly();
        if (IsAuthoredDanchiActive()) return;

        GameObject danchi = FindSceneObject("Danchi");
        GameObject detail = FindSceneObject("DanchiHighDetail");
        if (danchi == null || detail == null)
            throw new InvalidOperationException(
                "DanchiHighDetail must exist before sill-drainage construction.");

        Material aluminum = AssetDatabase.LoadAssetAtPath<Material>(AluminumPath);
        ValidateAluminum(aluminum);
        Directory.CreateDirectory(MeshRoot);

        MeshRenderer[] legacy = FindLegacySillRenderers();
        if (legacy.Length != ExpectedLegacySillCount)
            throw new InvalidOperationException(
                $"Cannot replace sill slabs: expected {ExpectedLegacySillCount}, found {legacy.Length}.");
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
                throw new InvalidOperationException(
                    $"Detailed facade bay is missing for sill-drainage assembly: floor={floor}, bay={bay}.");

            int phase = PositiveHash($"sill-drainage:{floor}:{bay}") % PhaseBins;

            GameObject assembly = new GameObject($"SillDrainage_{floor}_{bay}");
            assembly.transform.SetParent(root.transform, false);
            assembly.transform.position = bayRoot.transform.position;
            assembly.transform.rotation = bayRoot.transform.rotation;
            assembly.transform.localScale = Vector3.one;

            Transform lod0Root = NewChild(assembly.transform, "LOD0");
            Transform lod1Root = NewChild(assembly.transform, "LOD1");
            Transform lod2Root = NewChild(assembly.transform, "LOD2");
            Transform lod3Root = NewChild(assembly.transform, "LOD3");

            var l0 = new List<Renderer>(Lod0RenderersPerWindow);
            MeshRenderer tray = AddRenderer("SillTray", lod0Root,
                new Vector3(0f, OriginY, OriginZ), Quaternion.identity,
                GetTrayMesh(phase), aluminum, true);
            AttachTrayWeathering(tray.gameObject);
            l0.Add(tray);

            foreach (Segment segment in BuildSkirtSegments())
            {
                MeshRenderer skirt = AddRenderer(segment.name, lod0Root,
                    new Vector3(segment.centerX, OriginY + SkirtCenterY, OriginZ + SkirtCenterZ),
                    Quaternion.identity,
                    GetMetricBox(new Vector3(segment.width, ExteriorSkirtHeight, ExteriorSkirtDepth),
                        phase, "Skirt_" + segment.id),
                    aluminum, true);
                AttachSkirtWeathering(skirt.gameObject);
                l0.Add(skirt);
            }

            MeshRenderer nose = AddRenderer("DripNose", lod0Root,
                new Vector3(0f, OriginY + NoseCenterY, OriginZ + NoseCenterZ),
                Quaternion.identity,
                GetMetricBox(new Vector3(SillWidth, DripNoseHeight, DripNoseDepth),
                    phase, "DripNose"),
                aluminum, true);
            AttachSkirtWeathering(nose.gameObject);
            l0.Add(nose);

            Mesh proxy = GetCombinedProxyMesh(phase);
            MeshRenderer l1 = AddRenderer("Sill_LOD1", lod1Root, Vector3.zero,
                Quaternion.identity, proxy, aluminum, true);
            MeshRenderer l2 = AddRenderer("Sill_LOD2", lod2Root, Vector3.zero,
                Quaternion.identity, proxy, aluminum, true);
            MeshRenderer l3 = AddRenderer("Sill_LOD3", lod3Root, Vector3.zero,
                Quaternion.identity, proxy, aluminum, false);
            AttachCombinedWeathering(l1.gameObject);
            AttachCombinedWeathering(l2.gameObject);
            AttachCombinedWeathering(l3.gameObject);

            var group = assembly.AddComponent<LODGroup>();
            group.SetLODs(new[]
            {
                new LOD(Lod0Transition, l0.ToArray()),
                new LOD(Lod1Transition, new Renderer[] { l1 }),
                new LOD(Lod2Transition, new Renderer[] { l2 }),
                new LOD(Lod3Cull, new Renderer[] { l3 })
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

        var manifest = root.AddComponent<QualityBlockFacadeSillDrainageManifest>();
        manifest.Configure(windows, legacy.Length, lod0Count, lod1Count, lod2Count, lod3Count,
            DrainSlotWidth, TrayFall, PhaseBins, PhaseStepMeters);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
    }

    private static Segment[] BuildSkirtSegments()
    {
        float half = SillWidth * 0.5f;
        float slotHalf = DrainSlotWidth * 0.5f;
        float leftSlotMin = -DrainSlotCenterX - slotHalf;
        float leftSlotMax = -DrainSlotCenterX + slotHalf;
        float rightSlotMin = DrainSlotCenterX - slotHalf;
        float rightSlotMax = DrainSlotCenterX + slotHalf;

        return new[]
        {
            Segment.FromEdges("Left", "L", -half, leftSlotMin),
            Segment.FromEdges("Center", "C", leftSlotMax, rightSlotMin),
            Segment.FromEdges("Right", "R", rightSlotMax, half)
        };
    }

    private static void ValidateDrainageTopology(Transform assembly)
    {
        Transform lod0 = assembly.Find("LOD0");
        if (lod0 == null)
            throw new InvalidOperationException("Sill LOD0 root missing: " + HierarchyPath(assembly));

        Transform tray = lod0.Find("SillTray");
        Transform left = lod0.Find("Skirt_Left");
        Transform center = lod0.Find("Skirt_Center");
        Transform right = lod0.Find("Skirt_Right");
        Transform nose = lod0.Find("DripNose");
        if (tray == null || left == null || center == null || right == null || nose == null)
            throw new InvalidOperationException(
                "Sill physical tray/skirt/drip topology is incomplete: " + HierarchyPath(assembly));

        float leftGap = center.localPosition.x - left.localPosition.x -
                        (RendererWidth(left) + RendererWidth(center)) * 0.5f;
        float rightGap = right.localPosition.x - center.localPosition.x -
                         (RendererWidth(right) + RendererWidth(center)) * 0.5f;
        if (Mathf.Abs(leftGap - DrainSlotWidth) > 0.002f ||
            Mathf.Abs(rightGap - DrainSlotWidth) > 0.002f)
            throw new InvalidOperationException(
                $"Sill drainage openings drifted from {DrainSlotWidth * 1000f:F0} mm: " +
                $"{leftGap * 1000f:F1}/{rightGap * 1000f:F1} mm.");

        float trayFront = tray.localPosition.z + tray.GetComponent<MeshFilter>().sharedMesh.bounds.max.z;
        float skirtBack = center.localPosition.z -
                          center.GetComponent<MeshFilter>().sharedMesh.bounds.extents.z;
        float recess = skirtBack - trayFront;
        if (recess < DrainRecessDepth - 0.004f || recess > DrainRecessDepth + 0.006f)
            throw new InvalidOperationException(
                $"Sill drain recess depth drifted: {recess:F4} m, expected near {DrainRecessDepth:F4} m.");

        float trayTopAtBack = OriginY + TrayTopBackY;
        float expectedFrameBottom = -0.880f;
        float seat = trayTopAtBack - expectedFrameBottom;
        if (Mathf.Abs(seat - ControlledFrameSeat) > 0.003f)
            throw new InvalidOperationException(
                $"Sill/frame seating depth drifted: {seat:F4} m, expected near {ControlledFrameSeat:F4} m.");
    }

    private static float RendererWidth(Transform t)
    {
        MeshFilter filter = t.GetComponent<MeshFilter>();
        return filter != null && filter.sharedMesh != null ? filter.sharedMesh.bounds.size.x : 0f;
    }

    private static Mesh GetTrayMesh(int phase)
    {
        string path = $"{MeshRoot}/GM_FacadeSill_Tray_P{phase}.asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;

        var profile = new[]
        {
            new Vector2(TrayBackZ, TrayBottomBackY),
            new Vector2(TrayFrontZ, TrayBottomFrontY),
            new Vector2(TrayFrontZ, TrayTopFrontY),
            new Vector2(TrayBackZ, TrayTopBackY)
        };
        Mesh mesh = BuildExtrudedProfile(SillWidth, profile);
        mesh.name = Path.GetFileNameWithoutExtension(path);
        BakeMetricUv(mesh, phase * PhaseStepMeters);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static Mesh GetMetricBox(Vector3 size, int phase, string id)
    {
        string path =
            $"{MeshRoot}/GM_FacadeSill_{id}_{Key(size.x)}_{Key(size.y)}_{Key(size.z)}_P{phase}.asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;

        Mesh mesh = UnityEngine.Object.Instantiate(
            QualityBlockDetailMeshLibrary.GetChamferedBox(size));
        mesh.name = Path.GetFileNameWithoutExtension(path);
        BakeMetricUv(mesh, phase * PhaseStepMeters);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static Mesh GetCombinedProxyMesh(int phase)
    {
        string path = $"{MeshRoot}/GM_FacadeSill_CombinedProxy_P{phase}.asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;

        var combines = new List<CombineInstance>
        {
            new CombineInstance
            {
                mesh = GetTrayMesh(phase),
                transform = Matrix4x4.Translate(new Vector3(0f, OriginY, OriginZ))
            }
        };

        foreach (Segment segment in BuildSkirtSegments())
        {
            combines.Add(new CombineInstance
            {
                mesh = GetMetricBox(
                    new Vector3(segment.width, ExteriorSkirtHeight, ExteriorSkirtDepth),
                    phase, "Skirt_" + segment.id),
                transform = Matrix4x4.Translate(
                    new Vector3(segment.centerX, OriginY + SkirtCenterY, OriginZ + SkirtCenterZ))
            });
        }

        combines.Add(new CombineInstance
        {
            mesh = GetMetricBox(new Vector3(SillWidth, DripNoseHeight, DripNoseDepth),
                phase, "DripNose"),
            transform = Matrix4x4.Translate(
                new Vector3(0f, OriginY + NoseCenterY, OriginZ + NoseCenterZ))
        });

        Mesh mesh = new Mesh { name = Path.GetFileNameWithoutExtension(path) };
        mesh.CombineMeshes(combines.ToArray(), true, true, false);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static Mesh BuildExtrudedProfile(float width, Vector2[] profileZY)
    {
        if (profileZY == null || profileZY.Length != 4)
            throw new ArgumentException("Extruded sill profile must be the four-point convex tray section.");

        float half = width * 0.5f;
        float cy = profileZY.Average(p => p.y);
        float cz = profileZY.Average(p => p.x);
        Vector3 center = new Vector3(0f, cy, cz);

        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        var uv = new List<Vector2>();

        for (int i = 0; i < profileZY.Length; i++)
        {
            int j = (i + 1) % profileZY.Length;
            Vector3 a = new Vector3(-half, profileZY[i].y, profileZY[i].x);
            Vector3 b = new Vector3(-half, profileZY[j].y, profileZY[j].x);
            Vector3 c = new Vector3(half, profileZY[j].y, profileZY[j].x);
            Vector3 d = new Vector3(half, profileZY[i].y, profileZY[i].x);
            AddQuadFacingOut(vertices, triangles, uv, a, b, c, d, center);
        }

        Vector3 p0 = new Vector3(-half, profileZY[0].y, profileZY[0].x);
        Vector3 p1 = new Vector3(-half, profileZY[1].y, profileZY[1].x);
        Vector3 p2 = new Vector3(-half, profileZY[2].y, profileZY[2].x);
        Vector3 p3 = new Vector3(-half, profileZY[3].y, profileZY[3].x);
        AddQuadFacingOut(vertices, triangles, uv, p0, p1, p2, p3, center);

        p0.x = p1.x = p2.x = p3.x = half;
        AddQuadFacingOut(vertices, triangles, uv, p0, p3, p2, p1, center);

        var mesh = new Mesh();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uv);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddQuadFacingOut(List<Vector3> vertices, List<int> triangles,
        List<Vector2> uv, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 meshCenter)
    {
        int start = vertices.Count;
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        vertices.Add(d);
        uv.Add(Vector2.zero);
        uv.Add(Vector2.right);
        uv.Add(Vector2.one);
        uv.Add(Vector2.up);

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
            float ax = Mathf.Abs(n.x);
            float ay = Mathf.Abs(n.y);
            float az = Mathf.Abs(n.z);
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

    private static MeshRenderer AddRenderer(string name, Transform parent,
        Vector3 localPosition, Quaternion localRotation, Mesh mesh, Material material,
        bool castsShadows)
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

    private static void AttachTrayWeathering(GameObject go)
    {
        var metadata = go.AddComponent<QualityBlockWeatheringSurface>();
        metadata.Configure(
            NewTownSurfaceExposure.RainExposed |
            NewTownSurfaceExposure.SunExposed |
            NewTownSurfaceExposure.UpwardFacing,
            NewTownStainSource.RainLedge |
            NewTownStainSource.DrainRunoff |
            NewTownStainSource.UVExposure,
            0.92f, 0.76f, 0.08f, 0f);
    }

    private static void AttachSkirtWeathering(GameObject go)
    {
        var metadata = go.AddComponent<QualityBlockWeatheringSurface>();
        metadata.Configure(
            NewTownSurfaceExposure.RainExposed |
            NewTownSurfaceExposure.SunExposed,
            NewTownStainSource.DrainRunoff |
            NewTownStainSource.UVExposure,
            0.88f, 0.80f, 0.10f, 0f);
    }

    private static void AttachCombinedWeathering(GameObject go)
    {
        var metadata = go.AddComponent<QualityBlockWeatheringSurface>();
        metadata.Configure(
            NewTownSurfaceExposure.RainExposed |
            NewTownSurfaceExposure.SunExposed |
            NewTownSurfaceExposure.UpwardFacing,
            NewTownStainSource.RainLedge |
            NewTownStainSource.DrainRunoff |
            NewTownStainSource.UVExposure,
            0.90f, 0.78f, 0.09f, 0f);
    }

    private static void ValidateAluminum(Material material)
    {
        if (material == null)
            throw new InvalidOperationException(
                "Canonical aged aluminum is missing. Build Danchi detail/PBR materials first.");
        if (material.shader == null || material.shader.name != "Standard")
            throw new InvalidOperationException(
                "Facade sill must use the canonical Standard-shader aged aluminum.");
        if (material.IsKeywordEnabled("_EMISSION") ||
            (material.HasProperty("_EmissionColor") &&
             material.GetColor("_EmissionColor").maxColorComponent > 0.001f))
            throw new InvalidOperationException(
                "Facade sill may not use emission or painted highlight energy.");

        // Before mapped microstructure is applied, the fallback scalar must still remain plausible.
        // Formal registered-material QA later validates the mapped 0.82-0.94 metallic range.
        if (!material.IsKeywordEnabled("_METALLICGLOSSMAP"))
        {
            float metallic = material.GetFloat("_Metallic");
            float smoothness = material.GetFloat("_Glossiness");
            if (metallic < 0.45f || metallic > 1.0f ||
                smoothness < 0.18f || smoothness > 0.72f)
                throw new InvalidOperationException(
                    "Fallback aged-aluminum scalar values are outside the physically plausible range.");
        }
    }

    private static MeshRenderer[] FindLegacySillRenderers()
    {
        return Resources.FindObjectsOfTypeAll<MeshRenderer>()
            .Where(x => x != null && x.gameObject.scene.IsValid() &&
                        string.Equals(x.gameObject.scene.path, ScenePath, StringComparison.Ordinal) &&
                        string.Equals(x.gameObject.name, "HD_WindowSillDrip", StringComparison.Ordinal))
            .OrderBy(x => HierarchyPath(x.transform), StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsAuthoredDanchiActive()
    {
        QualityBlockArtSlot slot = Resources.FindObjectsOfTypeAll<QualityBlockArtSlot>()
            .FirstOrDefault(x => x != null && x.gameObject.scene.IsValid() &&
                                 x.SlotId == "danchi.main");
        return slot != null && slot.IsUsingAuthoredArt;
    }

    private static void RemoveGeneratedRootIfPresent()
    {
        GameObject old = FindSceneObject(RootName);
        if (old != null) UnityEngine.Object.DestroyImmediate(old);
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
            throw new InvalidOperationException(
                "Facade sill-drainage evidence may run only in the already-prepared QualityBlock1990s scene.");
    }

    private static T LoadJson<T>(string assetPath)
    {
        string absolute = AbsolutePath(assetPath);
        if (!File.Exists(absolute))
            throw new FileNotFoundException("Required sill-drainage QA file not found: " + assetPath);
        T value = JsonUtility.FromJson<T>(File.ReadAllText(absolute));
        if (value == null)
            throw new InvalidOperationException("Could not parse sill-drainage QA JSON: " + assetPath);
        return value;
    }

    private static string AbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrWhiteSpace(projectRoot))
            throw new InvalidOperationException(
                "Could not resolve Unity project root for sill-drainage QA.");
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

    private static int Key(float meters) =>
        Mathf.RoundToInt(Mathf.Abs(meters) * 10000f);

    private static string HierarchyPath(Transform transform)
    {
        var names = new Stack<string>();
        Transform current = transform;
        while (current != null)
        {
            names.Push(current.name);
            current = current.parent;
        }
        return string.Join("/", names.ToArray());
    }

    private static void Require(bool condition, string message, List<string> errors)
    {
        if (!condition) errors.Add(message);
    }

    private static void RequireNear(float actual, float expected, float tolerance,
        string label, List<string> errors)
    {
        if (Mathf.Abs(actual - expected) > tolerance)
            errors.Add($"{label} must be {expected}, got {actual}");
    }

    private static void RequireExactSet(string[] actual, string[] expected,
        string label, List<string> errors)
    {
        if (actual == null || actual.Any(string.IsNullOrWhiteSpace) ||
            actual.Distinct(StringComparer.Ordinal).Count() != actual.Length ||
            !new HashSet<string>(actual, StringComparer.Ordinal).SetEquals(expected))
            errors.Add(label + " must be exactly [" + string.Join(", ", expected) + "]");
    }

    private struct Segment
    {
        public string name;
        public string id;
        public float centerX;
        public float width;

        public static Segment FromEdges(string name, string id, float minX, float maxX)
        {
            return new Segment
            {
                name = "Skirt_" + name,
                id = id,
                centerX = (minX + maxX) * 0.5f,
                width = maxX - minX
            };
        }
    }

    [Serializable]
    private sealed class SillContract
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
        public float sillWidthM;
        public float trayDepthM;
        public float trayThicknessM;
        public float outwardFallM;
        public float exteriorSkirtHeightM;
        public int drainSlotCount;
        public float drainSlotWidthM;
        public float recessDepthBehindDrainSlotM;
        public float controlledFrameSeatM;
    }

    [Serializable]
    private sealed class MaterialPolicy
    {
        public string assetPath;
        public string albedo;
        public float metallicMin;
        public float metallicMax;
        public float roughnessMin;
        public float roughnessMax;
        public string specularFresnel;
        public string normalScale;
        public string microstructure;
        public float wetness;
        public string uvAging;
    }

    [Serializable]
    private sealed class QaPolicy
    {
        public int lodCount;
        public int phaseBins;
        public float phaseStepM;
        public int automaticVisualPoints;
        public bool renderVerificationPending;
        public bool disableLegacySillSlabs;
        public bool requireSceneSaveBinding;
        public bool requireNative4KPreCullValidation;
        public bool requirePhysicalDrainOpening;
        public bool preserveDrainOpeningTopologyThroughLod3;
    }
}

[DisallowMultipleComponent]
public sealed class QualityBlockFacadeSillDrainageManifest : MonoBehaviour
{
    [SerializeField] int windowCount;
    [SerializeField] int legacySillCount;
    [SerializeField] int lod0RendererCount;
    [SerializeField] int lod1RendererCount;
    [SerializeField] int lod2RendererCount;
    [SerializeField] int lod3RendererCount;
    [SerializeField] float drainSlotWidthMeters;
    [SerializeField] float trayFallMeters;
    [SerializeField] int phaseBins;
    [SerializeField] float phaseStepMeters;

    public int WindowCount => windowCount;
    public int LegacySillCount => legacySillCount;
    public int Lod0RendererCount => lod0RendererCount;
    public int Lod1RendererCount => lod1RendererCount;
    public int Lod2RendererCount => lod2RendererCount;
    public int Lod3RendererCount => lod3RendererCount;
    public float DrainSlotWidthMeters => drainSlotWidthMeters;
    public float TrayFallMeters => trayFallMeters;

    public void Configure(int windows, int legacySills,
        int lod0, int lod1, int lod2, int lod3,
        float slotWidth, float trayFall, int phases, float phaseStep)
    {
        windowCount = windows;
        legacySillCount = legacySills;
        lod0RendererCount = lod0;
        lod1RendererCount = lod1;
        lod2RendererCount = lod2;
        lod3RendererCount = lod3;
        drainSlotWidthMeters = slotWidth;
        trayFallMeters = trayFall;
        phaseBins = phases;
        phaseStepMeters = phaseStep;
    }
}
