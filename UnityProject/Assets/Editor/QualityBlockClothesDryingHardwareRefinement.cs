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
/// Reconstructs the generated fallback's balcony clothes-pole supports as mechanically legible public-housing-style
/// hardware instead of a solid cylindrical peg attached to a tilted rectangular bar. The refinement preserves the
/// original 30-bay layout, but makes the load path explicit: side-wall back plate -> two anchors -> pivot/arm ->
/// annular pole receiver. Receiver holes are coaxial on the facade X axis so a left/right clothes pole would actually
/// pass through the paired supports.
///
/// The first scene save after a freshly rebuilt DanchiHighDetail may install the refinement. Once its manifest exists,
/// later saves are validation-only and fail closed on drift. Formal QA cameras are always read-only. This source-side
/// work awards zero Visual Fidelity points; native 3840x2160 frontal/oblique/grazing evidence remains mandatory.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockClothesDryingHardwareRefinement
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string DetailRootName = "DanchiHighDetail";
    private const string ContractPath = "Assets/QA/clothes_drying_hardware_contract.json";
    private const string LookdevPath = "Assets/QA/clothes_drying_hardware_lookdev.svg";
    private const string MeshRoot = "Assets/Art/GeneratedDetailMeshes";
    private const string MaterialPath = "Assets/Art/GeneratedDetailMaterials/MAT_DarkGalvanizedSteel.mat";

    private const int ExpectedBayCount = 30;
    private const int SupportsPerBay = 2;
    private const int ExpectedSupportCount = ExpectedBayCount * SupportsPerBay;
    private const int LegacyRenderersPerSupport = 2; // bracket + old solid receiver
    private const int RefinedRenderersPerSupport = 6; // bracket + ring + plate + pivot + two anchors
    private const int ExpectedLegacyRendererCount = ExpectedSupportCount * LegacyRenderersPerSupport;
    private const int ExpectedRefinedRendererCount = ExpectedSupportCount * RefinedRenderersPerSupport;

    private const float ArmLengthM = 0.420f;
    private const float ArmWidthM = 0.055f;
    private const float ArmDepthM = 0.050f;
    private const float BackPlateThicknessM = 0.014f;
    private const float BackPlateHeightM = 0.200f;
    private const float BackPlateDepthM = 0.090f;
    private const float ReceiverOuterDiameterM = 0.090f;
    private const float ReceiverInnerDiameterM = 0.038f;
    private const float ReceiverAxialDepthM = 0.024f;
    private const float PivotDiameterM = 0.028f;
    private const float PivotAxialDepthM = 0.018f;
    private const float AnchorHeadDiameterM = 0.012f;
    private const float AnchorHeadDepthM = 0.006f;
    private const float AnchorPitchM = 0.110f;

    private static bool applyingOrValidating;
    private static int lastFormalValidationFrame = -1;

    static QualityBlockClothesDryingHardwareRefinement()
    {
        EditorSceneManager.sceneSaving -= OnSceneSaving;
        EditorSceneManager.sceneSaving += OnSceneSaving;
        Camera.onPreCull -= OnCameraPreCull;
        Camera.onPreCull += OnCameraPreCull;
    }

    [MenuItem("NewTown/Geometry/Refine Balcony Clothes Drying Hardware")]
    public static void ApplyAndPersist()
    {
        EnsureBenchmarkSceneOpen();
        ValidateContractConfigOnly();

        if (IsAuthoredDanchiActive())
        {
            Debug.Log("Generated balcony clothes-drying refinement skipped because authored danchi art is authoritative.");
            return;
        }

        ApplyToOpenScene();
        ValidateOpenScene();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "Balcony clothes-drying hardware refined into 60 side-wall-mounted support assemblies with annular, X-axis-coaxial pole receivers. " +
            "Visual Fidelity remains UNSCORED pending native 4K evidence.");
    }

    public static void ApplyToOpenScene()
    {
        if (applyingOrValidating) return;
        applyingOrValidating = true;
        try
        {
            EnsureBenchmarkSceneOpen();
            ValidateContractConfigOnly();
            if (IsAuthoredDanchiActive()) return;

            GameObject detailRoot = FindSceneObject(DetailRootName);
            if (detailRoot == null)
                throw new InvalidOperationException("DanchiHighDetail is missing before clothes-drying hardware refinement.");

            var existingManifest = detailRoot.GetComponent<QualityBlockClothesDryingHardwareManifest>();
            if (existingManifest != null)
            {
                ValidateOpenSceneInternal(detailRoot, existingManifest);
                return;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            ValidateMaterial(material);
            Directory.CreateDirectory(MeshRoot);

            int bayCount = 0;
            int supportCount = 0;
            int refinedRendererCount = 0;

            for (int floor = 0; floor < 5; floor++)
            for (int bay = 0; bay < 6; bay++)
            {
                GameObject bayRoot = FindSceneObject($"HD_BayAssembly_{floor}_{bay}");
                if (bayRoot == null)
                    throw new InvalidOperationException($"Detailed bay missing for clothes hardware: floor={floor}, bay={bay}.");

                foreach (int side in new[] { -1, 1 })
                {
                    RefineSupport(bayRoot.transform, floor, bay, side, material);
                    supportCount++;
                    refinedRendererCount += RefinedRenderersPerSupport;
                }

                bayCount++;
            }

            var manifest = detailRoot.AddComponent<QualityBlockClothesDryingHardwareManifest>();
            manifest.Configure(
                bayCount,
                supportCount,
                refinedRendererCount,
                ArmLengthM,
                ReceiverOuterDiameterM,
                ReceiverInnerDiameterM,
                ReceiverAxialDepthM,
                AnchorPitchM,
                "X");

            EditorUtility.SetDirty(detailRoot);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
        }
        finally
        {
            applyingOrValidating = false;
        }
    }

    [MenuItem("NewTown/QA/Validate Balcony Clothes Drying Hardware")]
    public static void ValidateOpenScene()
    {
        if (applyingOrValidating) return;
        applyingOrValidating = true;
        try
        {
            EnsureBenchmarkSceneOpen();
            ValidateContractConfigOnly();

            if (IsAuthoredDanchiActive())
                return;

            GameObject detailRoot = FindSceneObject(DetailRootName);
            if (detailRoot == null)
                throw new InvalidOperationException("DanchiHighDetail is missing during clothes-drying hardware QA.");

            var manifest = detailRoot.GetComponent<QualityBlockClothesDryingHardwareManifest>();
            if (manifest == null)
                throw new InvalidOperationException(
                    "Clothes-drying hardware manifest is missing. A formal render may not use the legacy solid-receiver state.");

            ValidateOpenSceneInternal(detailRoot, manifest);

            Debug.Log(
                "Balcony clothes-drying hardware QA passed structurally: 30 bays, 60 paired supports, side-wall plates/anchors/pivots, " +
                "annular X-axis receivers with 38 mm clear bore, authored meshes, metric-UV-compatible source geometry and cause-based LOD0 weathering. " +
                "This awards zero Visual Fidelity points; actual 4K contact, silhouette, material and temporal review is still required.");
        }
        finally
        {
            applyingOrValidating = false;
        }
    }

    [MenuItem("NewTown/QA/Validate Balcony Clothes Drying Hardware Contract")]
    public static void ValidateContractConfigOnly()
    {
        ClothesHardwareContract contract = LoadContract();
        var errors = new List<string>();

        Require(contract != null, "contract is null/unparseable", errors);
        if (contract == null)
            throw new InvalidOperationException("Clothes-drying hardware contract FAILED: contract is null/unparseable.");

        Require(string.Equals(contract.schemaVersion, "1.0", StringComparison.Ordinal), "schemaVersion must be 1.0", errors);
        Require(string.Equals(contract.assemblyId, "balcony_clothes_pole_receiver_support", StringComparison.Ordinal), "assemblyId mismatch", errors);
        Require(contract.geometry != null, "geometry section missing", errors);
        Require(contract.material != null, "material section missing", errors);
        Require(contract.qa != null, "qa section missing", errors);

        if (contract.geometry != null)
        {
            Require(contract.geometry.bayCount == ExpectedBayCount, "bayCount mismatch", errors);
            Require(contract.geometry.supportsPerBay == SupportsPerBay, "supportsPerBay mismatch", errors);
            RequireNear(contract.geometry.armLengthM, ArmLengthM, 0.0001f, "armLengthM", errors);
            RequireNear(contract.geometry.receiverOuterDiameterM, ReceiverOuterDiameterM, 0.0001f, "receiverOuterDiameterM", errors);
            RequireNear(contract.geometry.receiverInnerDiameterM, ReceiverInnerDiameterM, 0.0001f, "receiverInnerDiameterM", errors);
            RequireNear(contract.geometry.receiverAxialDepthM, ReceiverAxialDepthM, 0.0001f, "receiverAxialDepthM", errors);
            RequireNear(contract.geometry.anchorPitchM, AnchorPitchM, 0.0001f, "anchorPitchM", errors);
            Require(string.Equals(contract.geometry.receiverAxis, "X", StringComparison.Ordinal), "receiverAxis must remain X", errors);
            Require(string.Equals(contract.geometry.poleAxisRelationship, "paired_receivers_coaxial_parallel_to_facade", StringComparison.Ordinal),
                "poleAxisRelationship mismatch", errors);
        }

        if (contract.material != null)
        {
            Require(string.Equals(contract.material.assetPath, MaterialPath, StringComparison.Ordinal), "material assetPath mismatch", errors);
            Require(contract.material.metallicMin >= 0.75f && contract.material.metallicMax <= 1.0f,
                "galvanized metallic range must remain conductive", errors);
            Require(contract.material.roughnessMin >= 0.45f && contract.material.roughnessMax <= 0.75f,
                "galvanized roughness range drifted", errors);
            Require(contract.material.specularF0 >= 0.60f && contract.material.specularF0 <= 0.80f,
                "galvanized F0 is outside the approved physical range", errors);
            Require(contract.material.wetness == 0f, "formal benchmark hardware must remain dry", errors);
        }

        if (contract.qa != null)
        {
            Require(contract.qa.visualFidelityPointsAwarded == 0, "source QA may not award Visual Fidelity points", errors);
            Require(contract.qa.renderVerificationPending, "renderVerificationPending must remain true", errors);
            Require(contract.qa.formalCameraReadOnlyValidation, "formal cameras must use read-only validation", errors);
            Require(contract.qa.requireAnnularReceiver, "annular receiver requirement must remain enabled", errors);
            Require(contract.qa.requireCauseBasedWeathering, "cause-based weathering requirement must remain enabled", errors);
            Require(contract.qa.requireMetricPhysicalUvCompatibility, "metric physical UV compatibility must remain required", errors);
        }

        foreach (string value in new[]
        {
            contract.manufacture, contract.dimensionsThickness, contract.materialsFinish, contract.mounting,
            contract.interfacesGapsSeals, contract.orientationExposure, contract.aging, contract.geometryVsMaterial,
            contract.lodPolicy, contract.weatheringCausality, contract.lookdevBrief, contract.sourceBasis,
            contract.researchStatus
        })
            Require(!string.IsNullOrWhiteSpace(value), "mandatory manufacture/material reasoning field is empty", errors);

        string[] requiredEvidence =
        {
            "hero/frontal_balcony_hardware",
            "oblique/construction_depth",
            "grazing/galvanized_edge_response",
            "crop/clothes_receiver_100pct",
            "temporal/oblique_lod_stability"
        };
        RequireExactSet(contract.requiredEvidenceRefs, requiredEvidence, "requiredEvidenceRefs", errors);

        string[] requiredDefects =
        {
            "visible_primitive_placeholder",
            "impossible_material_physics",
            "hero_geometry_intersection",
            "severe_aliasing_or_shimmer",
            "visible_lod_pop",
            "missing_construction_material_metadata",
            "unverified_render_claim"
        };
        RequireExactSet(contract.criticalDefectIds, requiredDefects, "criticalDefectIds", errors);

        if (!File.Exists(AbsolutePath(LookdevPath)))
            errors.Add("lookdev SVG is missing: " + LookdevPath);

        if (contract.forbiddenThemes == null ||
            !new HashSet<string>(contract.forbiddenThemes, StringComparer.OrdinalIgnoreCase)
                .SetEquals(new[] { "earthquake", "disaster", "reconstruction" }))
            errors.Add("forbiddenThemes must remain exactly earthquake/disaster/reconstruction.");

        if (errors.Count > 0)
            throw new InvalidOperationException("Clothes-drying hardware contract FAILED:\n - " + string.Join("\n - ", errors));
    }

    private static void ValidateOpenSceneInternal(
        GameObject detailRoot,
        QualityBlockClothesDryingHardwareManifest manifest)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        ValidateMaterial(material);

        if (manifest.BayCount != ExpectedBayCount ||
            manifest.SupportCount != ExpectedSupportCount ||
            manifest.RefinedRendererCount != ExpectedRefinedRendererCount)
            throw new InvalidOperationException(
                $"Clothes hardware manifest count drift: bays={manifest.BayCount}/{ExpectedBayCount}, " +
                $"supports={manifest.SupportCount}/{ExpectedSupportCount}, renderers={manifest.RefinedRendererCount}/{ExpectedRefinedRendererCount}.");

        RequireNearOrThrow(manifest.ArmLengthM, ArmLengthM, 0.0001f, "manifest arm length");
        RequireNearOrThrow(manifest.ReceiverOuterDiameterM, ReceiverOuterDiameterM, 0.0001f, "manifest receiver OD");
        RequireNearOrThrow(manifest.ReceiverInnerDiameterM, ReceiverInnerDiameterM, 0.0001f, "manifest receiver ID");
        RequireNearOrThrow(manifest.ReceiverAxialDepthM, ReceiverAxialDepthM, 0.0001f, "manifest receiver axial depth");
        RequireNearOrThrow(manifest.AnchorPitchM, AnchorPitchM, 0.0001f, "manifest anchor pitch");
        if (!string.Equals(manifest.ReceiverAxis, "X", StringComparison.Ordinal))
            throw new InvalidOperationException("Clothes receiver manifest axis drifted from X.");

        MeshRenderer[] brackets = SourceRenderers(detailRoot, "HD_ClothesBracket_");
        MeshRenderer[] receivers = SourceRenderers(detailRoot, "HD_ClothesReceiver_");
        MeshRenderer[] plates = SourceRenderers(detailRoot, "HD_ClothesBasePlate_");
        MeshRenderer[] pivots = SourceRenderers(detailRoot, "HD_ClothesPivot_");
        MeshRenderer[] anchors = SourceRenderers(detailRoot, "HD_ClothesAnchorBolt_");

        if (brackets.Length != ExpectedSupportCount || receivers.Length != ExpectedSupportCount ||
            plates.Length != ExpectedSupportCount || pivots.Length != ExpectedSupportCount || anchors.Length != ExpectedSupportCount * 2)
            throw new InvalidOperationException(
                $"Clothes hardware renderer topology drift: bracket={brackets.Length}, receiver={receivers.Length}, " +
                $"plate={plates.Length}, pivot={pivots.Length}, anchors={anchors.Length}.");

        int sourceTotal = brackets.Length + receivers.Length + plates.Length + pivots.Length + anchors.Length;
        if (sourceTotal != ExpectedRefinedRendererCount)
            throw new InvalidOperationException($"Unexpected refined clothes hardware source renderer total {sourceTotal}.");

        foreach (MeshRenderer renderer in brackets.Concat(receivers).Concat(plates).Concat(pivots).Concat(anchors))
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                throw new InvalidOperationException("Refined clothes hardware source renderer is disabled: " + HierarchyPath(renderer.transform));
            if (renderer.sharedMaterial != material)
                throw new InvalidOperationException("Refined clothes hardware uses non-canonical galvanized material: " + HierarchyPath(renderer.transform));
            if (renderer.HasPropertyBlock())
                throw new InvalidOperationException("Clothes hardware may not hide PBR overrides in a MaterialPropertyBlock: " + HierarchyPath(renderer.transform));
            if (renderer.shadowCastingMode == ShadowCastingMode.Off || !renderer.receiveShadows)
                throw new InvalidOperationException("Clothes hardware must participate in coherent midsummer sun/shadow response: " + HierarchyPath(renderer.transform));
            if (renderer.GetComponent<Collider>() != null)
                throw new InvalidOperationException("Clothes hardware refinement is visual-only and may not add gameplay colliders.");

            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null)
                throw new InvalidOperationException("Clothes hardware renderer has no mesh: " + HierarchyPath(renderer.transform));
            string meshPath = AssetDatabase.GetAssetPath(mesh) ?? string.Empty;
            if (!mesh.name.StartsWith("GM_HD_", StringComparison.Ordinal) &&
                !meshPath.StartsWith(QualityBlockDetailPhysicalUvUpgrade.MeshAssetRoot + "/", StringComparison.Ordinal))
                throw new InvalidOperationException("Clothes hardware must use authored detail geometry, not a stock primitive: " + mesh.name);
            if (mesh.name == "Cube" || mesh.name == "Cylinder" || mesh.name == "Plane" || mesh.name == "Quad")
                throw new InvalidOperationException("Stock primitive geometry is forbidden in clothes hardware evidence.");
            if (mesh.uv == null || mesh.uv.Length != mesh.vertexCount ||
                mesh.normals == null || mesh.normals.Length != mesh.vertexCount ||
                mesh.tangents == null || mesh.tangents.Length != mesh.vertexCount)
                throw new InvalidOperationException("Clothes hardware mesh lacks UV/normal/tangent data: " + mesh.name);
        }

        foreach (MeshRenderer receiver in receivers)
        {
            if (receiver.sharedMesh.vertexCount < 64)
                throw new InvalidOperationException("Receiver ring mesh is too simple to be annular manufactured geometry: " + receiver.sharedMesh.name);

            Vector3 axis = receiver.transform.TransformDirection(Vector3.up).normalized;
            float alignment = Mathf.Abs(Vector3.Dot(axis, Vector3.right));
            if (alignment < 0.995f)
                throw new InvalidOperationException(
                    $"Receiver bore axis must remain parallel to facade X (alignment={alignment:0.000}): {HierarchyPath(receiver.transform)}");
        }

        for (int floor = 0; floor < 5; floor++)
        for (int bay = 0; bay < 6; bay++)
        {
            GameObject bayRoot = FindSceneObject($"HD_BayAssembly_{floor}_{bay}");
            if (bayRoot == null)
                throw new InvalidOperationException($"Bay missing during clothes hardware pair QA: {floor}/{bay}.");

            MeshRenderer left = FindChildRenderer(bayRoot.transform, "HD_ClothesReceiver_L");
            MeshRenderer right = FindChildRenderer(bayRoot.transform, "HD_ClothesReceiver_R");
            if (left == null || right == null)
                throw new InvalidOperationException($"Paired receivers missing in bay {floor}/{bay}.");

            Vector3 l = left.transform.position;
            Vector3 r = right.transform.position;
            if (Mathf.Abs(l.y - r.y) > 0.004f || Mathf.Abs(l.z - r.z) > 0.004f)
                throw new InvalidOperationException(
                    $"Paired clothes receivers are not coaxial in bay {floor}/{bay}: left={l}, right={r}.");
            if (Mathf.Abs(r.x - l.x) < 2.70f)
                throw new InvalidOperationException($"Paired clothes receiver span collapsed in bay {floor}/{bay}.");
        }

        int weatheringCount = brackets.Concat(receivers).Concat(plates).Concat(pivots).Concat(anchors)
            .Count(r => r.GetComponent<QualityBlockWeatheringSurface>() != null);
        if (weatheringCount != ExpectedRefinedRendererCount)
            throw new InvalidOperationException(
                $"Every refined LOD0 clothes hardware renderer requires cause-based weathering metadata: {weatheringCount}/{ExpectedRefinedRendererCount}.");

        // The generated 4-level Danchi LOD group owns the refinement. LOD0 must contain every source piece;
        // LOD1/2 progressively retain receiver/support silhouette and LOD3 may cull this sub-pixel fixture.
        LODGroup group = detailRoot.GetComponent<LODGroup>();
        if (group != null)
        {
            LOD[] lods = group.GetLODs();
            if (lods.Length != 4)
                throw new InvalidOperationException("Danchi detail LOD group must remain four levels after clothes-hardware refinement.");
            var lod0Set = new HashSet<Renderer>(lods[0].renderers.Where(x => x != null));
            foreach (MeshRenderer renderer in brackets.Concat(receivers).Concat(plates).Concat(pivots).Concat(anchors))
                if (!lod0Set.Contains(renderer))
                    throw new InvalidOperationException("Refined clothes hardware source is missing from Danchi LOD0: " + HierarchyPath(renderer.transform));
        }
    }

    private static void RefineSupport(Transform bayRoot, int floor, int bay, int side, Material material)
    {
        string sideName = side < 0 ? "L" : "R";
        MeshRenderer bracket = FindChildRenderer(bayRoot, $"HD_ClothesBracket_{side}");
        MeshRenderer receiver = FindChildRenderer(bayRoot, $"HD_ClothesReceiver_{side}");
        if (bracket == null || receiver == null)
            throw new InvalidOperationException($"Legacy clothes bracket/receiver missing before refinement: floor={floor}, bay={bay}, side={side}.");

        // Public-housing-style side-wall installation: the mounting plate is thin along X; the pole bore is
        // parallel to X. Small deterministic angle variation represents installer position without changing
        // manufactured dimensions. Left/right members in one bay stay coaxial at the receiver center.
        float x = side * 1.47f;
        float installTiltDeg = (bay % 3 - 1) * 2.5f;
        Vector3 ringCenter = new Vector3(x, 0.600f, -6.720f);
        Vector3 pivotCenter = new Vector3(x, 0.425f, -6.815f);
        Vector3 backPlateCenter = new Vector3(x + side * 0.004f, 0.425f, -6.815f);

        Vector3 armCenter = (pivotCenter + ringCenter) * 0.5f;
        Vector3 armVector = ringCenter - pivotCenter;
        float physicalArmLength = Mathf.Max(ArmLengthM, armVector.magnitude);
        Mesh armMesh = QualityBlockDetailMeshLibrary.GetChamferedBox(new Vector3(ArmWidthM, physicalArmLength, ArmDepthM));
        bracket.gameObject.name = $"HD_ClothesBracket_{sideName}";
        bracket.GetComponent<MeshFilter>().sharedMesh = armMesh;
        bracket.transform.localPosition = armCenter;
        bracket.transform.localScale = Vector3.one;
        bracket.transform.localRotation = Quaternion.FromToRotation(Vector3.up, armVector.normalized) * Quaternion.Euler(installTiltDeg, 0f, 0f);
        bracket.sharedMaterial = material;
        RemoveCollider(bracket.gameObject);
        AttachWeathering(bracket.gameObject, false, 0.44f, 0.36f, 0.16f);

        receiver.gameObject.name = $"HD_ClothesReceiver_{sideName}";
        receiver.GetComponent<MeshFilter>().sharedMesh = GetReceiverRingMesh();
        receiver.transform.localPosition = ringCenter;
        receiver.transform.localScale = Vector3.one;
        receiver.transform.localRotation = Quaternion.Euler(0f, 0f, -90f); // source ring axis Y -> facade/pole axis X
        receiver.sharedMaterial = material;
        RemoveCollider(receiver.gameObject);
        AttachWeathering(receiver.gameObject, false, 0.56f, 0.42f, 0.22f);

        AddPart(
            $"HD_ClothesBasePlate_{sideName}", bayRoot, backPlateCenter, Quaternion.identity,
            QualityBlockDetailMeshLibrary.GetChamferedBox(new Vector3(BackPlateThicknessM, BackPlateHeightM, BackPlateDepthM)),
            material, false, 0.32f, 0.28f, 0.08f);

        AddPart(
            $"HD_ClothesPivot_{sideName}", bayRoot, pivotCenter + new Vector3(-side * 0.010f, 0f, 0f),
            Quaternion.Euler(0f, 0f, -90f),
            QualityBlockDetailMeshLibrary.GetBeveledCylinder(
                new Vector3(PivotDiameterM, PivotAxialDepthM * 0.5f, PivotDiameterM), false),
            material, false, 0.34f, 0.28f, 0.18f);

        for (int anchor = -1; anchor <= 1; anchor += 2)
        {
            Vector3 anchorPos = backPlateCenter + new Vector3(-side * (BackPlateThicknessM * 0.5f + 0.003f), anchor * AnchorPitchM * 0.5f, 0f);
            AddPart(
                $"HD_ClothesAnchorBolt_{sideName}_{(anchor < 0 ? "Low" : "High")}", bayRoot, anchorPos,
                Quaternion.Euler(0f, 0f, -90f),
                QualityBlockDetailMeshLibrary.GetBeveledCylinder(
                    new Vector3(AnchorHeadDiameterM, AnchorHeadDepthM * 0.5f, AnchorHeadDiameterM), true),
                material, true, 0.46f, 0.34f, 0.12f);
        }
    }

    private static MeshRenderer AddPart(
        string name,
        Transform parent,
        Vector3 localPosition,
        Quaternion localRotation,
        Mesh mesh,
        Material material,
        bool ferrousFastener,
        float rain,
        float sun,
        float contact)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = localRotation;
        go.transform.localScale = Vector3.one;
        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        AttachWeathering(go, ferrousFastener, rain, sun, contact);
        return renderer;
    }

    private static Mesh GetReceiverRingMesh()
    {
        string path = $"{MeshRoot}/GM_HD_BevelCylinder_ClothesReceiverRing_OD{Key(ReceiverOuterDiameterM)}_ID{Key(ReceiverInnerDiameterM)}_D{Key(ReceiverAxialDepthM)}.asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;

        Directory.CreateDirectory(MeshRoot);
        const int sides = 24;
        float ro = ReceiverOuterDiameterM * 0.5f;
        float ri = ReceiverInnerDiameterM * 0.5f;
        float halfDepth = ReceiverAxialDepthM * 0.5f;
        var vertices = new List<Vector3>(sides * 16);
        var triangles = new List<int>(sides * 24);
        var uv = new List<Vector2>(sides * 16);

        for (int i = 0; i < sides; i++)
        {
            float a0 = Mathf.PI * 2f * i / sides;
            float a1 = Mathf.PI * 2f * (i + 1) / sides;
            Vector3 o0 = new Vector3(Mathf.Cos(a0) * ro, 0f, Mathf.Sin(a0) * ro);
            Vector3 o1 = new Vector3(Mathf.Cos(a1) * ro, 0f, Mathf.Sin(a1) * ro);
            Vector3 i0 = new Vector3(Mathf.Cos(a0) * ri, 0f, Mathf.Sin(a0) * ri);
            Vector3 i1 = new Vector3(Mathf.Cos(a1) * ri, 0f, Mathf.Sin(a1) * ri);

            AddQuad(vertices, triangles, uv,
                o0 + Vector3.down * halfDepth, o1 + Vector3.down * halfDepth,
                o1 + Vector3.up * halfDepth, o0 + Vector3.up * halfDepth);
            AddQuad(vertices, triangles, uv,
                i1 + Vector3.down * halfDepth, i0 + Vector3.down * halfDepth,
                i0 + Vector3.up * halfDepth, i1 + Vector3.up * halfDepth);
            AddQuad(vertices, triangles, uv,
                i0 + Vector3.up * halfDepth, o0 + Vector3.up * halfDepth,
                o1 + Vector3.up * halfDepth, i1 + Vector3.up * halfDepth);
            AddQuad(vertices, triangles, uv,
                i1 + Vector3.down * halfDepth, o1 + Vector3.down * halfDepth,
                o0 + Vector3.down * halfDepth, i0 + Vector3.down * halfDepth);
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

    private static void AddQuad(
        List<Vector3> vertices,
        List<int> triangles,
        List<Vector2> uv,
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Vector3 d)
    {
        int start = vertices.Count;
        vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
        uv.Add(new Vector2(0f, 0f)); uv.Add(new Vector2(1f, 0f)); uv.Add(new Vector2(1f, 1f)); uv.Add(new Vector2(0f, 1f));
        triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
        triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
    }

    private static void AttachWeathering(GameObject go, bool ferrousFastener, float rain, float sun, float contact)
    {
        var metadata = go.GetComponent<QualityBlockWeatheringSurface>();
        if (metadata == null) metadata = go.AddComponent<QualityBlockWeatheringSurface>();
        NewTownStainSource sources = NewTownStainSource.UVExposure | NewTownStainSource.RecessGrime;
        if (ferrousFastener) sources |= NewTownStainSource.FerrousFixture;
        metadata.Configure(
            NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.Sheltered | NewTownSurfaceExposure.SunExposed,
            sources,
            Mathf.Clamp01(rain),
            Mathf.Clamp01(sun),
            0f,
            Mathf.Clamp01(contact));
    }

    private static void ValidateMaterial(Material material)
    {
        if (material == null)
            throw new InvalidOperationException("Canonical galvanized clothes-hardware material is missing: " + MaterialPath);
        if (material.shader == null || !string.Equals(material.shader.name, "Standard", StringComparison.Ordinal))
            throw new InvalidOperationException("Clothes hardware galvanized material must use Standard PBR.");
        if (!material.HasProperty("_Metallic") || material.GetFloat("_Metallic") < 0.65f || material.GetFloat("_Metallic") > 1.0f)
            throw new InvalidOperationException("Clothes hardware galvanized metallic value is outside the approved conductive range.");
        if (!material.HasProperty("_Glossiness"))
            throw new InvalidOperationException("Clothes hardware galvanized material is missing smoothness.");
        float roughness = 1f - material.GetFloat("_Glossiness");
        if (roughness < 0.40f || roughness > 0.78f)
            throw new InvalidOperationException($"Clothes hardware galvanized roughness is implausible: {roughness:0.###}.");
        if (material.IsKeywordEnabled("_EMISSION") && material.HasProperty("_EmissionColor") &&
            material.GetColor("_EmissionColor").maxColorComponent > 0.0001f)
            throw new InvalidOperationException("Clothes hardware may not contain emissive/baked-highlight energy.");
    }

    private static MeshRenderer[] SourceRenderers(GameObject detailRoot, string prefix)
    {
        return detailRoot.GetComponentsInChildren<MeshRenderer>(true)
            .Where(r => !IsUnderLodProxy(r.transform, detailRoot.transform))
            .Where(r => r.gameObject.name.StartsWith(prefix, StringComparison.Ordinal))
            .ToArray();
    }

    private static bool IsUnderLodProxy(Transform transform, Transform detailRoot)
    {
        Transform current = transform;
        while (current != null && current != detailRoot)
        {
            if (current.name == "HD_LOD1_Proxy" || current.name == "HD_LOD2_Proxy" || current.name == "HD_LOD3_Proxy")
                return true;
            current = current.parent;
        }
        return false;
    }

    private static MeshRenderer FindChildRenderer(Transform parent, string exactName)
    {
        return parent.GetComponentsInChildren<MeshRenderer>(true)
            .FirstOrDefault(r => string.Equals(r.gameObject.name, exactName, StringComparison.Ordinal));
    }

    private static void RemoveCollider(GameObject go)
    {
        Collider collider = go.GetComponent<Collider>();
        if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
    }

    private static void OnSceneSaving(Scene scene, string path)
    {
        if (applyingOrValidating || !scene.IsValid() || !string.Equals(path, ScenePath, StringComparison.Ordinal))
            return;
        if (IsAuthoredDanchiActive()) return;

        GameObject detailRoot = FindSceneObject(DetailRootName);
        if (detailRoot == null) return;

        var manifest = detailRoot.GetComponent<QualityBlockClothesDryingHardwareManifest>();
        if (manifest == null)
            ApplyToOpenScene();
        else
            ValidateOpenScene();
    }

    private static void OnCameraPreCull(Camera camera)
    {
        if (camera == null || camera.targetTexture == null ||
            !EditorSceneManager.GetActiveScene().IsValid() ||
            !string.Equals(EditorSceneManager.GetActiveScene().path, ScenePath, StringComparison.Ordinal) ||
            !IsFormalEvidenceTarget(camera.targetTexture.name))
            return;
        if (lastFormalValidationFrame == Time.frameCount)
            return;

        ValidateOpenScene();
        lastFormalValidationFrame = Time.frameCount;
    }

    private static bool IsFormalEvidenceTarget(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return name.StartsWith("QA4K_", StringComparison.Ordinal) ||
               name.StartsWith("QATemporal_", StringComparison.Ordinal) ||
               name.StartsWith("QAPreparedTemporal_", StringComparison.Ordinal);
    }

    private static bool IsAuthoredDanchiActive()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        return Resources.FindObjectsOfTypeAll<QualityBlockArtSlot>()
            .Any(x => x != null && x.gameObject.scene == scene && x.SlotId == "danchi.main" && x.IsUsingAuthoredArt);
    }

    private static void EnsureBenchmarkSceneOpen()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !string.Equals(scene.path, ScenePath, StringComparison.Ordinal))
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x != null && x.scene.IsValid() &&
                                 string.Equals(x.scene.path, ScenePath, StringComparison.Ordinal) &&
                                 string.Equals(x.name, name, StringComparison.Ordinal));
    }

    private static string HierarchyPath(Transform transform)
    {
        var names = new List<string>();
        Transform current = transform;
        while (current != null)
        {
            names.Add(current.name);
            current = current.parent;
        }
        names.Reverse();
        return string.Join("/", names);
    }

    private static ClothesHardwareContract LoadContract()
    {
        string path = AbsolutePath(ContractPath);
        if (!File.Exists(path))
            throw new FileNotFoundException("Clothes-drying hardware contract missing: " + ContractPath);
        return JsonUtility.FromJson<ClothesHardwareContract>(File.ReadAllText(path));
    }

    private static string AbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private static int Key(float meters) => Mathf.RoundToInt(Mathf.Abs(meters) * 10000f);

    private static void Require(bool condition, string message, List<string> errors)
    {
        if (!condition) errors.Add(message);
    }

    private static void RequireNear(float actual, float expected, float tolerance, string field, List<string> errors)
    {
        if (Mathf.Abs(actual - expected) > tolerance)
            errors.Add($"{field} must be {expected:0.####}, got {actual:0.####}");
    }

    private static void RequireNearOrThrow(float actual, float expected, float tolerance, string field)
    {
        if (Mathf.Abs(actual - expected) > tolerance)
            throw new InvalidOperationException($"{field} must be {expected:0.####}, got {actual:0.####}.");
    }

    private static void RequireExactSet(string[] actual, string[] expected, string label, List<string> errors)
    {
        var a = new HashSet<string>(actual ?? Array.Empty<string>(), StringComparer.Ordinal);
        var e = new HashSet<string>(expected, StringComparer.Ordinal);
        if (!a.SetEquals(e))
            errors.Add(label + " must match the canonical exact set.");
    }

    [Serializable]
    private sealed class ClothesHardwareContract
    {
        public string schemaVersion;
        public string assemblyId;
        public string researchStatus;
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
        public GeometryContract geometry;
        public MaterialContract material;
        public QaContract qa;
        public string[] requiredEvidenceRefs;
        public string[] criticalDefectIds;
        public string[] forbiddenThemes;
    }

    [Serializable]
    private sealed class GeometryContract
    {
        public int bayCount;
        public int supportsPerBay;
        public float armLengthM;
        public float receiverOuterDiameterM;
        public float receiverInnerDiameterM;
        public float receiverAxialDepthM;
        public float anchorPitchM;
        public string receiverAxis;
        public string poleAxisRelationship;
    }

    [Serializable]
    private sealed class MaterialContract
    {
        public string assetPath;
        public float[] albedoSrgb;
        public float roughnessMin;
        public float roughnessMax;
        public float metallicMin;
        public float metallicMax;
        public float specularF0;
        public float normalAmplitudeMm;
        public float microstructureScaleMm;
        public float wetAlbedoMultiplier;
        public float wetRoughnessMultiplier;
        public float uvFadeMax;
        public float wetness;
        public string angularFresnelResponse;
    }

    [Serializable]
    private sealed class QaContract
    {
        public int visualFidelityPointsAwarded;
        public bool renderVerificationPending;
        public bool formalCameraReadOnlyValidation;
        public bool requireAnnularReceiver;
        public bool requireCauseBasedWeathering;
        public bool requireMetricPhysicalUvCompatibility;
    }
}

[DisallowMultipleComponent]
public sealed class QualityBlockClothesDryingHardwareManifest : MonoBehaviour
{
    [SerializeField] private int bayCount;
    [SerializeField] private int supportCount;
    [SerializeField] private int refinedRendererCount;
    [SerializeField] private float armLengthM;
    [SerializeField] private float receiverOuterDiameterM;
    [SerializeField] private float receiverInnerDiameterM;
    [SerializeField] private float receiverAxialDepthM;
    [SerializeField] private float anchorPitchM;
    [SerializeField] private string receiverAxis;

    public int BayCount => bayCount;
    public int SupportCount => supportCount;
    public int RefinedRendererCount => refinedRendererCount;
    public float ArmLengthM => armLengthM;
    public float ReceiverOuterDiameterM => receiverOuterDiameterM;
    public float ReceiverInnerDiameterM => receiverInnerDiameterM;
    public float ReceiverAxialDepthM => receiverAxialDepthM;
    public float AnchorPitchM => anchorPitchM;
    public string ReceiverAxis => receiverAxis;

    public void Configure(
        int bays,
        int supports,
        int rendererCount,
        float armLength,
        float receiverOd,
        float receiverId,
        float receiverDepth,
        float anchorPitch,
        string axis)
    {
        bayCount = bays;
        supportCount = supports;
        refinedRendererCount = rendererCount;
        armLengthM = armLength;
        receiverOuterDiameterM = receiverOd;
        receiverInnerDiameterM = receiverId;
        receiverAxialDepthM = receiverDepth;
        anchorPitchM = anchorPitch;
        receiverAxis = axis;
    }
}
