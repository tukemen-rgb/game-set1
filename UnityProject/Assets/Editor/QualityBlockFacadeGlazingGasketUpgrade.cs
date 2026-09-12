using System;
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
/// Adds the missing glazing-to-sash interface to the generated apartment facade.
/// The optical panes and 4 mm glass edges already exist, but without a seated gasket the glass reads
/// as if it floats inside the aluminum extrusion. This pass builds a dimensioned dark EPDM perimeter
/// immediately outside each apartment pane, with metric UVs and an independent four-level LOD group.
///
/// The pass is bound to the same scene-save boundary used by the facade optical stack. It never changes
/// gameplay collision and it awards no Visual Fidelity points: native 3840x2160 frontal/oblique/grazing
/// pixels remain authoritative for contact, thickness, repetition, edge aliasing and material response.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockFacadeGlazingGasketUpgrade
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string OpticsRootName = "DanchiFacadeOptics";
    private const string RootName = "FacadeGlazingGasketInterfaces";
    private const string MaterialPath = "Assets/Art/GeneratedDetailMaterials/MAT_WindowRubber.mat";
    private const string MeshRoot = "Assets/Art/GeneratedFacadeOpticsMeshes/GlazingGasket";
    private const string ContractPath = "Assets/QA/facade_glazing_gasket_contract.json";
    private const string SourcePath = "Assets/Editor/QualityBlockFacadeGlazingGasketUpgrade.cs";

    private const int ExpectedPaneCount = 60;
    private const int Lod0RenderersPerPane = 4;
    private const float PaneWidth = 1.03f;
    private const float PaneHeight = 1.46f;
    private const float GasketFaceWidth = 0.010f;
    private const float GasketDepth = 0.010f;
    private const float GasketCenterZOffset = -0.001f;
    private const int PhaseBins = 8;
    private const float PhaseStepMeters = 0.013f;

    private const float Lod0Transition = 0.18f;
    private const float Lod1Transition = 0.08f;
    private const float Lod2Transition = 0.03f;
    private const float Lod3Cull = 0.008f;

    private static bool validating;
    private static int lastValidatedFrame = -1;

    static QualityBlockFacadeGlazingGasketUpgrade()
    {
        EditorSceneManager.sceneSaving -= OnSceneSaving;
        EditorSceneManager.sceneSaving += OnSceneSaving;
        Camera.onPreCull -= OnCameraPreCull;
        Camera.onPreCull += OnCameraPreCull;
    }

    [MenuItem("NewTown/Geometry/Rebuild Facade Glazing Gasket Interfaces")]
    public static void RebuildForOpenScene()
    {
        EnsureSceneOpen();
        ValidateContractConfigOnly();

        if (IsAuthoredDanchiActive())
        {
            RemoveGeneratedRootIfPresent();
            Debug.Log("Authored danchi replacement is active; generated glazing-gasket interfaces were skipped.");
            return;
        }

        GameObject opticsRoot = FindSceneObject(OpticsRootName);
        if (opticsRoot == null)
            throw new InvalidOperationException("DanchiFacadeOptics is missing. Build facade optics before glazing-gasket interfaces.");

        RemoveGeneratedRootIfPresent();
        BuildForOpticsRoot(opticsRoot);
        ValidateOpenScene();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "Facade glazing-gasket interfaces rebuilt for 60 apartment panes with metric UVs and LOD0/1/2/3. " +
            "Visual Fidelity remains UNSCORED pending actual native 4K evidence.");
    }

    [MenuItem("NewTown/QA/Validate Facade Glazing Gasket Interfaces")]
    public static void ValidateOpenScene()
    {
        if (validating) return;
        validating = true;
        try
        {
            EnsureSceneOpen();
            ValidateContractConfigOnly();

            if (IsAuthoredDanchiActive())
            {
                if (FindSceneObject(RootName) != null)
                    throw new InvalidOperationException("Generated glazing-gasket root must not remain active when authored danchi art is authoritative.");
                return;
            }

            GameObject opticsRoot = FindSceneObject(OpticsRootName);
            GameObject root = FindSceneObject(RootName);
            if (opticsRoot == null || root == null)
                throw new InvalidOperationException("Facade optics or glazing-gasket interface root is missing from the prepared scene.");
            if (root.transform.parent != opticsRoot.transform)
                throw new InvalidOperationException("Glazing-gasket interface root must be parented directly under DanchiFacadeOptics.");

            GameObject[] panes = FindApartmentPanes(opticsRoot);
            if (panes.Length != ExpectedPaneCount)
                throw new InvalidOperationException($"Expected {ExpectedPaneCount} apartment optical panes, found {panes.Length}.");

            Material rubber = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            ValidateRubberMaterial(rubber);

            QualityBlockFacadeGlazingGasketManifest manifest = root.GetComponent<QualityBlockFacadeGlazingGasketManifest>();
            if (manifest == null)
                throw new InvalidOperationException("Facade glazing-gasket manifest is missing.");
            if (manifest.PaneCount != ExpectedPaneCount || manifest.Lod0RendererCount != ExpectedPaneCount * Lod0RenderersPerPane ||
                manifest.Lod1RendererCount != ExpectedPaneCount || manifest.Lod2RendererCount != ExpectedPaneCount ||
                manifest.Lod3RendererCount != ExpectedPaneCount)
                throw new InvalidOperationException(
                    $"Glazing-gasket manifest counts drifted: panes={manifest.PaneCount}, renderers=" +
                    $"{manifest.Lod0RendererCount}/{manifest.Lod1RendererCount}/{manifest.Lod2RendererCount}/{manifest.Lod3RendererCount}.");
            if (Mathf.Abs(manifest.GasketFaceWidthMeters - GasketFaceWidth) > 0.00001f ||
                Mathf.Abs(manifest.GasketDepthMeters - GasketDepth) > 0.00001f)
                throw new InvalidOperationException("Glazing-gasket manifest dimensions drifted from the construction contract.");

            LODGroup group = root.GetComponent<LODGroup>();
            if (group == null)
                throw new InvalidOperationException("Facade glazing-gasket LODGroup is missing.");
            LOD[] lods = group.GetLODs();
            if (lods.Length != 4)
                throw new InvalidOperationException($"Expected four glazing-gasket LOD levels, got {lods.Length}.");
            int[] expectedCounts = { ExpectedPaneCount * Lod0RenderersPerPane, ExpectedPaneCount, ExpectedPaneCount, ExpectedPaneCount };
            for (int i = 0; i < 4; i++)
                if (lods[i].renderers.Length != expectedCounts[i])
                    throw new InvalidOperationException($"Glazing-gasket LOD{i} renderer count invalid: {lods[i].renderers.Length}, expected {expectedCounts[i]}.");
            if (Mathf.Abs(lods[0].screenRelativeTransitionHeight - Lod0Transition) > 0.0001f ||
                Mathf.Abs(lods[1].screenRelativeTransitionHeight - Lod1Transition) > 0.0001f ||
                Mathf.Abs(lods[2].screenRelativeTransitionHeight - Lod2Transition) > 0.0001f ||
                Mathf.Abs(lods[3].screenRelativeTransitionHeight - Lod3Cull) > 0.0001f)
                throw new InvalidOperationException("Glazing-gasket LOD transition thresholds drifted from policy.");
            if (group.fadeMode != LODFadeMode.CrossFade || !group.animateCrossFading)
                throw new InvalidOperationException("Glazing-gasket LODs must use animated cross-fade.");

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            if (colliders.Length != 0)
                throw new InvalidOperationException($"Glazing-gasket detail must remain render-only; found {colliders.Length} colliders.");

            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length != expectedCounts.Sum())
                throw new InvalidOperationException($"Unexpected glazing-gasket renderer total: {renderers.Length}.");
            foreach (MeshRenderer renderer in renderers)
            {
                if (renderer.sharedMaterial != rubber)
                    throw new InvalidOperationException("Every glazing-gasket renderer must use the canonical EPDM material: " + HierarchyPath(renderer.transform));
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null ||
                    !filter.sharedMesh.name.StartsWith("GM_FacadeGasket_", StringComparison.Ordinal))
                    throw new InvalidOperationException("Glazing-gasket renderer uses missing/non-authored geometry: " + HierarchyPath(renderer.transform));
                string meshName = filter.sharedMesh.name;
                if (meshName == "Cube" || meshName == "Quad" || meshName == "Plane")
                    throw new InvalidOperationException("Stock primitive geometry is forbidden in the glazing-gasket evidence path.");
                if (renderer.GetPropertyBlockHasData())
                    throw new InvalidOperationException("Glazing-gasket renderers may not hide material overrides in a MaterialPropertyBlock: " + HierarchyPath(renderer.transform));
            }

            Transform lod0Root = root.transform.Find("Gasket_LOD0");
            if (lod0Root == null)
                throw new InvalidOperationException("Glazing-gasket LOD0 root is missing.");
            int weatheringCount = lod0Root.GetComponentsInChildren<QualityBlockWeatheringSurface>(true).Length;
            if (weatheringCount != expectedCounts[0])
                throw new InvalidOperationException($"Expected cause-based weathering metadata on all {expectedCounts[0]} LOD0 gasket bars, found {weatheringCount}.");

            foreach (GameObject pane in panes)
                ValidatePaneInterface(pane, lod0Root);

            Debug.Log(
                "Facade glazing-gasket QA passed structurally: 60 panes, 240 LOD0 physical EPDM bars, 60/60/60 simplified LOD1/2/3 rings, " +
                "metric UVs, no colliders or stock primitives. This awards zero Visual Fidelity points; 4K contact/grazing/aliasing evidence is still required.");
        }
        finally
        {
            validating = false;
        }
    }

    [MenuItem("NewTown/QA/Validate Facade Glazing Gasket Contract")]
    public static void ValidateContractConfigOnly()
    {
        string absolute = AbsolutePath(ContractPath);
        if (!File.Exists(absolute))
            throw new FileNotFoundException($"Facade glazing-gasket contract is missing: {ContractPath}");

        GlazingGasketContract contract = JsonUtility.FromJson<GlazingGasketContract>(File.ReadAllText(absolute));
        if (contract == null || contract.geometry == null || contract.material == null || contract.qa == null)
            throw new InvalidOperationException("Facade glazing-gasket contract is null or incomplete.");

        var errors = new List<string>();
        Require(string.Equals(contract.schemaVersion, "1.0", StringComparison.Ordinal), "schemaVersion must be 1.0", errors);
        Require(string.Equals(contract.assemblyId, "apartment_sliding_sash_glazing_gasket", StringComparison.Ordinal), "assemblyId mismatch", errors);
        Require(contract.expectedPaneCount == ExpectedPaneCount, $"expectedPaneCount must be {ExpectedPaneCount}", errors);
        RequireNear(contract.geometry.paneWidthM, PaneWidth, 0.0001f, "paneWidthM", errors);
        RequireNear(contract.geometry.paneHeightM, PaneHeight, 0.0001f, "paneHeightM", errors);
        RequireNear(contract.geometry.visibleGasketFaceWidthM, GasketFaceWidth, 0.0001f, "visibleGasketFaceWidthM", errors);
        RequireNear(contract.geometry.gasketDepthM, GasketDepth, 0.0001f, "gasketDepthM", errors);
        RequireNear(contract.geometry.glassNominalThicknessM, 0.004f, 0.0001f, "glassNominalThicknessM", errors);
        Require(string.Equals(contract.material.assetPath, MaterialPath, StringComparison.Ordinal), "material assetPath mismatch", errors);
        Require(contract.material.metallicMax <= 0.001f, "EPDM metallicMax must remain <= 0.001", errors);
        Require(contract.material.roughnessMin >= 0.70f && contract.material.roughnessMax <= 1.0f && contract.material.roughnessMin <= contract.material.roughnessMax,
            "EPDM roughness range must stay high and physically valid", errors);
        Require(contract.material.specularF0Min >= 0.02f && contract.material.specularF0Max <= 0.08f && contract.material.specularF0Min <= contract.material.specularF0Max,
            "EPDM dielectric F0 range is invalid", errors);
        Require(contract.material.wetness == 0f, "benchmark glazing gasket must remain dry", errors);
        Require(contract.qa.lodCount == 4, "qa.lodCount must be four", errors);
        Require(contract.qa.lod0RenderersPerPane == Lod0RenderersPerPane, "qa.lod0RenderersPerPane mismatch", errors);
        Require(contract.qa.phaseBins == PhaseBins, "qa.phaseBins mismatch", errors);
        RequireNear(contract.qa.phaseStepM, PhaseStepMeters, 0.0001f, "qa.phaseStepM", errors);
        Require(contract.qa.automaticVisualPoints == 0, "qa.automaticVisualPoints must remain zero", errors);
        Require(contract.qa.renderVerificationPending, "qa.renderVerificationPending must remain true before real render review", errors);
        Require(contract.qa.requireSceneSaveBinding, "qa.requireSceneSaveBinding must remain true", errors);
        Require(contract.qa.requireNative4KPreCullValidation, "qa.requireNative4KPreCullValidation must remain true", errors);

        foreach (string value in new[]
        {
            contract.manufacture, contract.dimensionsThickness, contract.materialsFinish,
            contract.mounting, contract.interfacesGapsSeals, contract.orientationExposure,
            contract.aging, contract.geometryVsMaterial, contract.lodPolicy,
            contract.weatheringCausality, contract.lookdevBrief, contract.sourceBasis
        })
            Require(!string.IsNullOrWhiteSpace(value), "mandatory construction/material reasoning field is empty", errors);

        string[] requiredEvidence = { "hero/facade_center", "oblique/construction_depth", "grazing/sash_rail_response" };
        RequireExactSet(contract.requiredEvidenceRefs, requiredEvidence, "requiredEvidenceRefs", errors);
        string[] requiredDefects =
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
        RequireExactSet(contract.criticalDefectIds, requiredDefects, "criticalDefectIds", errors);

        string sourceAbsolute = AbsolutePath(SourcePath);
        if (!File.Exists(sourceAbsolute))
            errors.Add("facade glazing-gasket source file is missing");
        else
        {
            string source = File.ReadAllText(sourceAbsolute);
            Require(source.Contains("EditorSceneManager.sceneSaving += OnSceneSaving"), "sceneSaving formal build binding token is missing", errors);
            Require(source.Contains("Camera.onPreCull += OnCameraPreCull"), "native-4K pre-cull QA binding token is missing", errors);
            Require(source.Contains("Visual Fidelity remains UNSCORED"), "source must preserve non-scoring render-pending language", errors);
        }

        if (errors.Count > 0)
            throw new InvalidOperationException("Facade glazing-gasket contract FAILED:\n - " + string.Join("\n - ", errors));

        Debug.Log("Facade glazing-gasket contract valid. This is implementation metadata only and awards zero Visual Fidelity points.");
    }

    private static void OnSceneSaving(Scene scene, string path)
    {
        if (!scene.IsValid() || !string.Equals(path, ScenePath, StringComparison.Ordinal) || IsAuthoredDanchiActive())
            return;

        GameObject opticsRoot = FindSceneObject(OpticsRootName);
        if (opticsRoot == null)
            return;

        ValidateContractConfigOnly();
        GameObject existing = FindSceneObject(RootName);
        if (existing == null)
        {
            BuildForOpticsRoot(opticsRoot);
            return;
        }

        // Once serialized, unexpected drift is never silently repaired at a later save boundary.
        // That keeps candidate mutations explicit and prevents a reflection/still evidence epoch from
        // changing geometry merely because a scene was saved again.
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

    private static void BuildForOpticsRoot(GameObject opticsRoot)
    {
        GameObject[] panes = FindApartmentPanes(opticsRoot);
        if (panes.Length != ExpectedPaneCount)
            throw new InvalidOperationException($"Cannot build glazing-gasket interfaces: expected {ExpectedPaneCount} apartment panes, found {panes.Length}.");

        Material rubber = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        ValidateRubberMaterial(rubber);
        Directory.CreateDirectory(MeshRoot);

        var root = new GameObject(RootName);
        root.transform.SetParent(opticsRoot.transform, false);
        Transform lod0Root = NewChild(root.transform, "Gasket_LOD0");
        Transform lod1Root = NewChild(root.transform, "Gasket_LOD1");
        Transform lod2Root = NewChild(root.transform, "Gasket_LOD2");
        Transform lod3Root = NewChild(root.transform, "Gasket_LOD3");

        var lod0 = new List<Renderer>(ExpectedPaneCount * Lod0RenderersPerPane);
        var lod1 = new List<Renderer>(ExpectedPaneCount);
        var lod2 = new List<Renderer>(ExpectedPaneCount);
        var lod3 = new List<Renderer>(ExpectedPaneCount);

        foreach (GameObject pane in panes)
        {
            ValidatePaneTransform(pane);
            int phaseBin = PositiveHash(pane.name) % PhaseBins;
            float phase = phaseBin * PhaseStepMeters;
            Vector3 center = pane.transform.localPosition + new Vector3(0f, 0f, GasketCenterZOffset);
            float x = PaneWidth * 0.5f + GasketFaceWidth * 0.5f;
            float y = PaneHeight * 0.5f + GasketFaceWidth * 0.5f;

            Mesh vertical = GetOrCreateMetricChamferedBox(
                new Vector3(GasketFaceWidth, PaneHeight + GasketFaceWidth * 2f, GasketDepth), phaseBin);
            Mesh horizontal = GetOrCreateMetricChamferedBox(
                new Vector3(PaneWidth, GasketFaceWidth, GasketDepth), phaseBin);

            MeshRenderer left = AddRenderer($"FGG_L0_{pane.name}_L", lod0Root,
                center + new Vector3(-x, 0f, 0f), vertical, rubber, true);
            MeshRenderer right = AddRenderer($"FGG_L0_{pane.name}_R", lod0Root,
                center + new Vector3(x, 0f, 0f), vertical, rubber, true);
            MeshRenderer top = AddRenderer($"FGG_L0_{pane.name}_T", lod0Root,
                center + new Vector3(0f, y, 0f), horizontal, rubber, true);
            MeshRenderer bottom = AddRenderer($"FGG_L0_{pane.name}_B", lod0Root,
                center + new Vector3(0f, -y, 0f), horizontal, rubber, true);
            lod0.Add(left); lod0.Add(right); lod0.Add(top); lod0.Add(bottom);

            AttachGasketWeathering(left.gameObject);
            AttachGasketWeathering(right.gameObject);
            AttachGasketWeathering(top.gameObject);
            AttachGasketWeathering(bottom.gameObject);

            Mesh lod1Mesh = GetOrCreateCombinedRingMesh(GasketFaceWidth, GasketDepth * 0.85f, phaseBin, "L1");
            Mesh lod2Mesh = GetOrCreateFrontRingMesh(GasketFaceWidth * 0.80f, phase, "L2", phaseBin);
            Mesh lod3Mesh = GetOrCreateFrontRingMesh(GasketFaceWidth * 0.60f, phase, "L3", phaseBin);

            lod1.Add(AddRenderer($"FGG_L1_{pane.name}", lod1Root, center, lod1Mesh, rubber, true));
            lod2.Add(AddRenderer($"FGG_L2_{pane.name}", lod2Root, center + new Vector3(0f, 0f, 0.001f), lod2Mesh, rubber, false));
            lod3.Add(AddRenderer($"FGG_L3_{pane.name}", lod3Root, center + new Vector3(0f, 0f, 0.0015f), lod3Mesh, rubber, false));
        }

        var group = root.AddComponent<LODGroup>();
        group.SetLODs(new[]
        {
            new LOD(Lod0Transition, lod0.ToArray()),
            new LOD(Lod1Transition, lod1.ToArray()),
            new LOD(Lod2Transition, lod2.ToArray()),
            new LOD(Lod3Cull, lod3.ToArray()),
        });
        group.fadeMode = LODFadeMode.CrossFade;
        group.animateCrossFading = true;
        group.RecalculateBounds();

        var manifest = root.AddComponent<QualityBlockFacadeGlazingGasketManifest>();
        manifest.Configure(ExpectedPaneCount, lod0.Count, lod1.Count, lod2.Count, lod3.Count,
            GasketFaceWidth, GasketDepth, PhaseBins, PhaseStepMeters);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
    }

    private static Mesh GetOrCreateMetricChamferedBox(Vector3 size, int phaseBin)
    {
        string path = $"{MeshRoot}/GM_FacadeGasket_Box_{Key(size.x)}_{Key(size.y)}_{Key(size.z)}_P{phaseBin}.asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;

        Mesh source = QualityBlockDetailMeshLibrary.GetChamferedBox(size);
        Mesh mesh = UnityEngine.Object.Instantiate(source);
        mesh.name = Path.GetFileNameWithoutExtension(path);
        BakeMetricUv(mesh, phaseBin * PhaseStepMeters);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static Mesh GetOrCreateCombinedRingMesh(float faceWidth, float depth, int phaseBin, string lodId)
    {
        string path = $"{MeshRoot}/GM_FacadeGasket_Ring_{lodId}_W{Key(faceWidth)}_D{Key(depth)}_P{phaseBin}.asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;

        float x = PaneWidth * 0.5f + faceWidth * 0.5f;
        float y = PaneHeight * 0.5f + faceWidth * 0.5f;
        Mesh vertical = QualityBlockDetailMeshLibrary.GetChamferedBox(new Vector3(faceWidth, PaneHeight + faceWidth * 2f, depth));
        Mesh horizontal = QualityBlockDetailMeshLibrary.GetChamferedBox(new Vector3(PaneWidth, faceWidth, depth));
        var combines = new[]
        {
            new CombineInstance { mesh = vertical, transform = Matrix4x4.Translate(new Vector3(-x, 0f, 0f)) },
            new CombineInstance { mesh = vertical, transform = Matrix4x4.Translate(new Vector3(x, 0f, 0f)) },
            new CombineInstance { mesh = horizontal, transform = Matrix4x4.Translate(new Vector3(0f, y, 0f)) },
            new CombineInstance { mesh = horizontal, transform = Matrix4x4.Translate(new Vector3(0f, -y, 0f)) },
        };
        var mesh = new Mesh { name = Path.GetFileNameWithoutExtension(path) };
        mesh.CombineMeshes(combines, true, true, false);
        BakeMetricUv(mesh, phaseBin * PhaseStepMeters);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static Mesh GetOrCreateFrontRingMesh(float faceWidth, float phaseMeters, string lodId, int phaseBin)
    {
        string path = $"{MeshRoot}/GM_FacadeGasket_Ring_{lodId}_W{Key(faceWidth)}_P{phaseBin}.asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;

        float outerHalfX = PaneWidth * 0.5f + faceWidth;
        float outerHalfY = PaneHeight * 0.5f + faceWidth;
        float innerHalfX = PaneWidth * 0.5f;
        float innerHalfY = PaneHeight * 0.5f;
        var vertices = new List<Vector3>(16);
        var triangles = new List<int>(24);
        var uv = new List<Vector2>(16);

        AddFrontQuad(vertices, triangles, uv,
            new Vector3(-outerHalfX, innerHalfY, 0f), new Vector3(outerHalfX, innerHalfY, 0f),
            new Vector3(outerHalfX, outerHalfY, 0f), new Vector3(-outerHalfX, outerHalfY, 0f), phaseMeters);
        AddFrontQuad(vertices, triangles, uv,
            new Vector3(-outerHalfX, -outerHalfY, 0f), new Vector3(outerHalfX, -outerHalfY, 0f),
            new Vector3(outerHalfX, -innerHalfY, 0f), new Vector3(-outerHalfX, -innerHalfY, 0f), phaseMeters);
        AddFrontQuad(vertices, triangles, uv,
            new Vector3(-outerHalfX, -innerHalfY, 0f), new Vector3(-innerHalfX, -innerHalfY, 0f),
            new Vector3(-innerHalfX, innerHalfY, 0f), new Vector3(-outerHalfX, innerHalfY, 0f), phaseMeters);
        AddFrontQuad(vertices, triangles, uv,
            new Vector3(innerHalfX, -innerHalfY, 0f), new Vector3(outerHalfX, -innerHalfY, 0f),
            new Vector3(outerHalfX, innerHalfY, 0f), new Vector3(innerHalfX, innerHalfY, 0f), phaseMeters);

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

    private static void AddFrontQuad(List<Vector3> vertices, List<int> triangles, List<Vector2> uv,
        Vector3 a, Vector3 b, Vector3 c, Vector3 d, float phaseMeters)
    {
        int start = vertices.Count;
        vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
        uv.Add(new Vector2(a.x + phaseMeters, a.y + phaseMeters));
        uv.Add(new Vector2(b.x + phaseMeters, b.y + phaseMeters));
        uv.Add(new Vector2(c.x + phaseMeters, c.y + phaseMeters));
        uv.Add(new Vector2(d.x + phaseMeters, d.y + phaseMeters));
        triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
        triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
    }

    private static MeshRenderer AddRenderer(string name, Transform parent, Vector3 localPosition,
        Mesh mesh, Material material, bool castsShadows)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.identity;
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

    private static void AttachGasketWeathering(GameObject go)
    {
        var metadata = go.AddComponent<QualityBlockWeatheringSurface>();
        metadata.Configure(
            NewTownSurfaceExposure.Sheltered | NewTownSurfaceExposure.Recessed,
            NewTownStainSource.RecessGrime | NewTownStainSource.UVExposure,
            0.18f, 0.24f, 0f, 0.10f);
    }

    private static void ValidatePaneInterface(GameObject pane, Transform lod0Root)
    {
        ValidatePaneTransform(pane);
        Vector3 center = pane.transform.localPosition + new Vector3(0f, 0f, GasketCenterZOffset);
        float x = PaneWidth * 0.5f + GasketFaceWidth * 0.5f;
        float y = PaneHeight * 0.5f + GasketFaceWidth * 0.5f;
        ValidatePart(lod0Root, $"FGG_L0_{pane.name}_L", center + new Vector3(-x, 0f, 0f));
        ValidatePart(lod0Root, $"FGG_L0_{pane.name}_R", center + new Vector3(x, 0f, 0f));
        ValidatePart(lod0Root, $"FGG_L0_{pane.name}_T", center + new Vector3(0f, y, 0f));
        ValidatePart(lod0Root, $"FGG_L0_{pane.name}_B", center + new Vector3(0f, -y, 0f));
    }

    private static void ValidatePart(Transform root, string name, Vector3 expectedPosition)
    {
        Transform part = root.Find(name);
        if (part == null)
            throw new InvalidOperationException("Glazing-gasket LOD0 part missing: " + name);
        if ((part.localPosition - expectedPosition).sqrMagnitude > 1e-8f ||
            Quaternion.Angle(part.localRotation, Quaternion.identity) > 0.001f ||
            (part.localScale - Vector3.one).sqrMagnitude > 1e-8f)
            throw new InvalidOperationException("Glazing-gasket LOD0 transform drifted: " + name);
    }

    private static void ValidatePaneTransform(GameObject pane)
    {
        if (Quaternion.Angle(pane.transform.localRotation, Quaternion.identity) > 0.001f)
            throw new InvalidOperationException("Apartment optical pane rotation drifted from the gasket installation basis: " + pane.name);
        if (Mathf.Abs(pane.transform.localScale.x - PaneWidth) > 0.0001f ||
            Mathf.Abs(pane.transform.localScale.y - PaneHeight) > 0.0001f ||
            Mathf.Abs(pane.transform.localScale.z - 1f) > 0.0001f)
            throw new InvalidOperationException(
                $"Apartment optical pane dimensions drifted at {pane.name}: {pane.transform.localScale}, expected {PaneWidth:F3} x {PaneHeight:F3} m.");
    }

    private static void ValidateRubberMaterial(Material material)
    {
        if (material == null)
            throw new InvalidOperationException($"Canonical window EPDM material is missing: {MaterialPath}");
        if (material.shader == null || !string.Equals(material.shader.name, "Standard", StringComparison.Ordinal))
            throw new InvalidOperationException("Canonical window EPDM must use the Standard shader contract.");
        if (material.GetFloat("_Metallic") > 0.001f)
            throw new InvalidOperationException("Window EPDM gasket became metallic.");
        if (material.IsKeywordEnabled("_EMISSION") || material.GetColor("_EmissionColor").maxColorComponent > 0.001f)
            throw new InvalidOperationException("Window EPDM gasket may not encode a painted highlight through emission.");
        if (!QualityBlockDetailPhysicalUvUpgrade.IsCanonicalDetailMaterial(material) ||
            Mathf.Abs(QualityBlockDetailPhysicalUvUpgrade.RepeatsPerMeterFor(material) - 18f) > 0.001f)
            throw new InvalidOperationException("Window EPDM gasket is no longer registered to the canonical 18 repeats/metre physical-UV profile.");
        foreach (string property in new[] { "_MainTex", "_BumpMap", "_MetallicGlossMap" })
        {
            if (!material.HasProperty(property))
                throw new InvalidOperationException($"Window EPDM material is missing texture property {property}.");
            Vector2 scale = material.GetTextureScale(property);
            if ((scale - Vector2.one * 18f).sqrMagnitude > 0.0001f)
                throw new InvalidOperationException($"Window EPDM {property} scale must remain 18 repeats/metre, got {scale}.");
        }
    }

    private static GameObject[] FindApartmentPanes(GameObject opticsRoot)
    {
        return opticsRoot.GetComponentsInChildren<Transform>(true)
            .Where(t => t != null && t.gameObject != opticsRoot && t.name.StartsWith("FO_Glass_", StringComparison.Ordinal))
            .Select(t => t.gameObject)
            .OrderBy(x => x.name, StringComparer.Ordinal)
            .ToArray();
    }

    private static Transform NewChild(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static bool IsAuthoredDanchiActive()
    {
        QualityBlockArtSlot slot = Resources.FindObjectsOfTypeAll<QualityBlockArtSlot>()
            .FirstOrDefault(x => x.gameObject.scene.IsValid() && x.SlotId == "danchi.main");
        return slot != null && slot.IsUsingAuthoredArt;
    }

    private static void RemoveGeneratedRootIfPresent()
    {
        GameObject old = FindSceneObject(RootName);
        if (old != null) UnityEngine.Object.DestroyImmediate(old);
    }

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x.scene.IsValid() && x.name == name);
    }

    private static string HierarchyPath(Transform transform)
    {
        var parts = new List<string>();
        for (Transform t = transform; t != null; t = t.parent) parts.Add(t.name);
        parts.Reverse();
        return string.Join("/", parts);
    }

    private static int PositiveHash(string text)
    {
        unchecked
        {
            int hash = 17;
            for (int i = 0; i < text.Length; i++) hash = hash * 31 + text[i];
            return hash & 0x7fffffff;
        }
    }

    private static int Key(float meters) => Mathf.RoundToInt(Mathf.Abs(meters) * 100000f);

    private static void EnsureSceneOpen()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() ||
            !string.Equals(EditorSceneManager.GetActiveScene().path, ScenePath, StringComparison.Ordinal))
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static string AbsolutePath(string assetPath)
    {
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        return Path.Combine(projectRoot ?? string.Empty, assetPath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static void Require(bool condition, string message, List<string> errors)
    {
        if (!condition) errors.Add(message);
    }

    private static void RequireNear(float actual, float expected, float tolerance, string label, List<string> errors)
    {
        if (Mathf.Abs(actual - expected) > tolerance)
            errors.Add($"{label} expected {expected}, got {actual}");
    }

    private static void RequireExactSet(string[] actual, string[] expected, string label, List<string> errors)
    {
        string[] a = actual ?? Array.Empty<string>();
        var actualSet = new HashSet<string>(a, StringComparer.Ordinal);
        var expectedSet = new HashSet<string>(expected, StringComparer.Ordinal);
        if (a.Length != actualSet.Count || !actualSet.SetEquals(expectedSet))
            errors.Add($"{label} must be the exact canonical set [{string.Join(", ", expected)}]");
    }

    [Serializable]
    private sealed class GlazingGasketContract
    {
        public string schemaVersion;
        public string assemblyId;
        public int expectedPaneCount;
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
        public GeometrySpec geometry;
        public MaterialSpec material;
        public QaSpec qa;
        public string[] requiredEvidenceRefs;
        public string[] criticalDefectIds;
    }

    [Serializable]
    private sealed class GeometrySpec
    {
        public float paneWidthM;
        public float paneHeightM;
        public float visibleGasketFaceWidthM;
        public float gasketDepthM;
        public float glassNominalThicknessM;
    }

    [Serializable]
    private sealed class MaterialSpec
    {
        public string assetPath;
        public float[] baseColorSrgb;
        public float roughnessMin;
        public float roughnessMax;
        public float metallicMax;
        public float specularF0Min;
        public float specularF0Max;
        public float normalScale;
        public float microstructureMm;
        public float wetness;
        public string uvAging;
        public string angularFresnelResponse;
    }

    [Serializable]
    private sealed class QaSpec
    {
        public int lodCount;
        public int lod0RenderersPerPane;
        public int phaseBins;
        public float phaseStepM;
        public int automaticVisualPoints;
        public bool renderVerificationPending;
        public bool requireSceneSaveBinding;
        public bool requireNative4KPreCullValidation;
    }
}

[DisallowMultipleComponent]
public sealed class QualityBlockFacadeGlazingGasketManifest : MonoBehaviour
{
    [SerializeField] private int paneCount;
    [SerializeField] private int lod0RendererCount;
    [SerializeField] private int lod1RendererCount;
    [SerializeField] private int lod2RendererCount;
    [SerializeField] private int lod3RendererCount;
    [SerializeField] private float gasketFaceWidthMeters;
    [SerializeField] private float gasketDepthMeters;
    [SerializeField] private int phaseBins;
    [SerializeField] private float phaseStepMeters;

    public int PaneCount => paneCount;
    public int Lod0RendererCount => lod0RendererCount;
    public int Lod1RendererCount => lod1RendererCount;
    public int Lod2RendererCount => lod2RendererCount;
    public int Lod3RendererCount => lod3RendererCount;
    public float GasketFaceWidthMeters => gasketFaceWidthMeters;
    public float GasketDepthMeters => gasketDepthMeters;

    public void Configure(int panes, int lod0, int lod1, int lod2, int lod3,
        float faceWidth, float depth, int uvPhaseBins, float uvPhaseStepMeters)
    {
        paneCount = panes;
        lod0RendererCount = lod0;
        lod1RendererCount = lod1;
        lod2RendererCount = lod2;
        lod3RendererCount = lod3;
        gasketFaceWidthMeters = faceWidth;
        gasketDepthMeters = depth;
        phaseBins = uvPhaseBins;
        phaseStepMeters = uvPhaseStepMeters;
    }
}

internal static class QualityBlockMeshRendererPropertyBlockExtensions
{
    private static readonly MaterialPropertyBlock Scratch = new MaterialPropertyBlock();

    public static bool GetPropertyBlockHasData(this MeshRenderer renderer)
    {
        Scratch.Clear();
        renderer.GetPropertyBlock(Scratch);
        return !Scratch.isEmpty;
    }
}
