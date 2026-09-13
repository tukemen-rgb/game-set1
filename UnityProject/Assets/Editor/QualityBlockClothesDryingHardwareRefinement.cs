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
/// Reconstructs the generated fallback balcony clothes-pole supports as mechanically legible
/// side-wall-mounted hardware. The legacy solid receiver becomes a true annular ring whose bore
/// is coaxial with the facade X axis; back plate, anchors and pivot make the load path explicit.
/// Formal cameras are validation-only. This source pass awards zero Visual Fidelity points.
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
    private const int RefinedRenderersPerSupport = 6; // arm, receiver, plate, pivot, two anchors
    private const int ExpectedRefinedRendererCount = ExpectedSupportCount * RefinedRenderersPerSupport;

    // The generated fallback is not an attribution to one historical manufacturer. These dimensions
    // preserve the existing balcony datum while replacing the visibly impossible solid-peg assembly.
    private const float ArmLengthM = 0.340f;
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
    private const float PositionToleranceM = 0.004f;

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
            Debug.Log("Generated clothes-drying refinement skipped because authored danchi art is authoritative.");
            return;
        }

        ApplyToOpenScene();
        ValidateOpenScene();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Balcony clothes-drying hardware refinement persisted. Visual Fidelity remains UNSCORED pending native 4K evidence.");
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

            var existing = detailRoot.GetComponent<QualityBlockClothesDryingHardwareManifest>();
            if (existing != null)
            {
                ValidateOpenSceneInternal(detailRoot, existing);
                return;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            ValidateMaterial(material);
            Directory.CreateDirectory(MeshRoot);

            int bays = 0;
            int supports = 0;
            for (int floor = 0; floor < 5; floor++)
            for (int bay = 0; bay < 6; bay++)
            {
                GameObject bayRoot = FindSceneObject($"HD_BayAssembly_{floor}_{bay}");
                if (bayRoot == null)
                    throw new InvalidOperationException($"Detailed bay missing for clothes hardware: floor={floor}, bay={bay}.");

                RefineSupport(bayRoot.transform, floor, bay, -1, material);
                RefineSupport(bayRoot.transform, floor, bay, 1, material);
                supports += 2;
                bays++;
            }

            var manifest = detailRoot.AddComponent<QualityBlockClothesDryingHardwareManifest>();
            manifest.Configure(
                bays,
                supports,
                supports * RefinedRenderersPerSupport,
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
            if (IsAuthoredDanchiActive()) return;

            GameObject detailRoot = FindSceneObject(DetailRootName);
            if (detailRoot == null)
                throw new InvalidOperationException("DanchiHighDetail is missing during clothes-drying hardware QA.");

            var manifest = detailRoot.GetComponent<QualityBlockClothesDryingHardwareManifest>();
            if (manifest == null)
                throw new InvalidOperationException("Clothes-drying hardware manifest is missing; formal evidence may not use the legacy solid-receiver state.");

            ValidateOpenSceneInternal(detailRoot, manifest);
            Debug.Log(
                "Clothes-drying hardware QA passed structurally: 30 bays / 60 supports, annular X-axis receivers, " +
                "plates/anchors/pivots, authored meshes, physical material authority and cause-based weathering. " +
                "No Visual Fidelity points were assigned without native 4K evidence.");
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
        if (contract == null)
            throw new InvalidOperationException("Clothes-drying hardware contract is null/unparseable.");

        Require(contract.schemaVersion == "1.0", "schemaVersion must be 1.0", errors);
        Require(contract.assemblyId == "balcony_clothes_pole_receiver_support", "assemblyId mismatch", errors);
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
            Require(contract.geometry.receiverAxis == "X", "receiverAxis must remain X", errors);
            Require(contract.geometry.poleAxisRelationship == "paired_receivers_coaxial_parallel_to_facade", "poleAxisRelationship mismatch", errors);
        }

        if (contract.material != null)
        {
            Require(contract.material.assetPath == MaterialPath, "material assetPath mismatch", errors);
            Require(contract.material.metallicMin >= 0.75f && contract.material.metallicMax <= 1f, "galvanized metallic range drifted", errors);
            Require(contract.material.roughnessMin >= 0.45f && contract.material.roughnessMax <= 0.75f, "galvanized roughness range drifted", errors);
            Require(contract.material.specularF0 >= 0.60f && contract.material.specularF0 <= 0.80f, "galvanized F0 drifted", errors);
            RequireNear(contract.material.wetness, 0f, 0.0001f, "formal wetness", errors);
        }

        if (contract.qa != null)
        {
            Require(contract.qa.visualFidelityPointsAwarded == 0, "source QA may not award Visual Fidelity points", errors);
            Require(contract.qa.renderVerificationPending, "renderVerificationPending must remain true", errors);
            Require(contract.qa.formalCameraReadOnlyValidation, "formal cameras must remain validation-only", errors);
            Require(contract.qa.requireAnnularReceiver, "annular receiver requirement disabled", errors);
            Require(contract.qa.requireCauseBasedWeathering, "cause-based weathering requirement disabled", errors);
            Require(contract.qa.requireMetricPhysicalUvCompatibility, "metric UV requirement disabled", errors);
        }

        foreach (string field in new[]
        {
            contract.researchStatus, contract.manufacture, contract.dimensionsThickness, contract.materialsFinish,
            contract.mounting, contract.interfacesGapsSeals, contract.orientationExposure, contract.aging,
            contract.geometryVsMaterial, contract.lodPolicy, contract.weatheringCausality, contract.lookdevBrief,
            contract.sourceBasis
        })
            Require(!string.IsNullOrWhiteSpace(field), "mandatory manufacture/material reasoning field is empty", errors);

        RequireExactSet(contract.requiredEvidenceRefs, new[]
        {
            "hero/frontal_balcony_hardware",
            "oblique/construction_depth",
            "grazing/galvanized_edge_response",
            "crop/clothes_receiver_100pct",
            "temporal/oblique_lod_stability"
        }, "requiredEvidenceRefs", errors);

        RequireExactSet(contract.criticalDefectIds, new[]
        {
            "visible_primitive_placeholder",
            "impossible_material_physics",
            "hero_geometry_intersection",
            "severe_aliasing_or_shimmer",
            "visible_lod_pop",
            "missing_construction_material_metadata",
            "unverified_render_claim"
        }, "criticalDefectIds", errors);

        Require(contract.forbiddenThemes != null &&
                new HashSet<string>(contract.forbiddenThemes, StringComparer.OrdinalIgnoreCase)
                    .SetEquals(new[] { "earthquake", "disaster", "reconstruction" }),
            "forbiddenThemes must remain exactly earthquake/disaster/reconstruction", errors);
        Require(File.Exists(AbsolutePath(LookdevPath)), "lookdev SVG missing", errors);

        if (errors.Count > 0)
            throw new InvalidOperationException("Clothes-drying hardware contract FAILED:\n - " + string.Join("\n - ", errors));
    }

    private static void ValidateOpenSceneInternal(GameObject detailRoot, QualityBlockClothesDryingHardwareManifest manifest)
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
        RequireNearOrThrow(manifest.ReceiverAxialDepthM, ReceiverAxialDepthM, 0.0001f, "manifest receiver depth");
        RequireNearOrThrow(manifest.AnchorPitchM, AnchorPitchM, 0.0001f, "manifest anchor pitch");
        if (manifest.ReceiverAxis != "X")
            throw new InvalidOperationException("Clothes receiver manifest axis drifted from X.");

        MeshRenderer[] arms = SourceRenderers(detailRoot, "HD_ClothesBracket_");
        MeshRenderer[] receivers = SourceRenderers(detailRoot, "HD_ClothesReceiver_");
        MeshRenderer[] plates = SourceRenderers(detailRoot, "HD_ClothesBasePlate_");
        MeshRenderer[] pivots = SourceRenderers(detailRoot, "HD_ClothesPivot_");
        MeshRenderer[] anchors = SourceRenderers(detailRoot, "HD_ClothesAnchorBolt_");

        if (arms.Length != 60 || receivers.Length != 60 || plates.Length != 60 || pivots.Length != 60 || anchors.Length != 120)
            throw new InvalidOperationException(
                $"Clothes hardware topology drift: arm={arms.Length}, receiver={receivers.Length}, plate={plates.Length}, pivot={pivots.Length}, anchors={anchors.Length}.");

        MeshRenderer[] all = arms.Concat(receivers).Concat(plates).Concat(pivots).Concat(anchors).ToArray();
        if (all.Length != ExpectedRefinedRendererCount)
            throw new InvalidOperationException($"Unexpected clothes hardware source-renderer count {all.Length}/{ExpectedRefinedRendererCount}.");

        foreach (MeshRenderer renderer in all)
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                throw new InvalidOperationException("Refined clothes hardware is disabled: " + HierarchyPath(renderer.transform));
            if (renderer.sharedMaterial != material)
                throw new InvalidOperationException("Non-canonical clothes hardware material: " + HierarchyPath(renderer.transform));
            if (renderer.HasPropertyBlock())
                throw new InvalidOperationException("MaterialPropertyBlock PBR override is forbidden on clothes hardware: " + HierarchyPath(renderer.transform));
            if (renderer.shadowCastingMode == ShadowCastingMode.Off || !renderer.receiveShadows)
                throw new InvalidOperationException("Clothes hardware must cast/receive coherent midsummer shadows: " + HierarchyPath(renderer.transform));
            if (renderer.GetComponent<Collider>() != null)
                throw new InvalidOperationException("Visual-only clothes hardware may not add gameplay colliders.");
            if (renderer.GetComponent<QualityBlockWeatheringSurface>() == null)
                throw new InvalidOperationException("Cause-based weathering metadata missing: " + HierarchyPath(renderer.transform));

            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null)
                throw new InvalidOperationException("Clothes hardware renderer has no mesh: " + HierarchyPath(renderer.transform));
            string meshPath = AssetDatabase.GetAssetPath(mesh) ?? string.Empty;
            bool authored = mesh.name.StartsWith("GM_HD_", StringComparison.Ordinal) ||
                            meshPath.StartsWith(QualityBlockDetailPhysicalUvUpgrade.MeshAssetRoot + "/", StringComparison.Ordinal);
            if (!authored || mesh.name == "Cube" || mesh.name == "Cylinder" || mesh.name == "Plane" || mesh.name == "Quad")
                throw new InvalidOperationException("Primitive/unapproved clothes hardware mesh: " + mesh.name);
            if (mesh.uv == null || mesh.uv.Length != mesh.vertexCount ||
                mesh.normals == null || mesh.normals.Length != mesh.vertexCount ||
                mesh.tangents == null || mesh.tangents.Length != mesh.vertexCount)
                throw new InvalidOperationException("Clothes hardware mesh lacks UV/normal/tangent data: " + mesh.name);
        }

        foreach (MeshRenderer receiver in receivers)
        {
            Mesh ringMesh = receiver.GetComponent<MeshFilter>().sharedMesh;
            if (ringMesh == null || ringMesh.vertexCount < 64)
                throw new InvalidOperationException("Receiver is too simple to establish an annular manufactured bore.");
            Vector3 boreAxis = receiver.transform.TransformDirection(Vector3.up).normalized;
            if (Mathf.Abs(Vector3.Dot(boreAxis, Vector3.right)) < 0.995f)
                throw new InvalidOperationException("Receiver bore axis is not parallel to facade X: " + HierarchyPath(receiver.transform));
        }

        for (int floor = 0; floor < 5; floor++)
        for (int bay = 0; bay < 6; bay++)
        {
            GameObject bayRoot = FindSceneObject($"HD_BayAssembly_{floor}_{bay}");
            MeshRenderer left = bayRoot != null ? FindChildRenderer(bayRoot.transform, "HD_ClothesReceiver_L") : null;
            MeshRenderer right = bayRoot != null ? FindChildRenderer(bayRoot.transform, "HD_ClothesReceiver_R") : null;
            if (left == null || right == null)
                throw new InvalidOperationException($"Paired clothes receivers missing in bay {floor}/{bay}.");

            Vector3 l = left.transform.position;
            Vector3 r = right.transform.position;
            if (Mathf.Abs(l.y - r.y) > PositionToleranceM || Mathf.Abs(l.z - r.z) > PositionToleranceM)
                throw new InvalidOperationException($"Receiver pair is not coaxial in bay {floor}/{bay}: left={l}, right={r}.");
            if (Mathf.Abs(r.x - l.x) < 2.70f)
                throw new InvalidOperationException($"Receiver pair span collapsed in bay {floor}/{bay}.");
        }

        LODGroup group = detailRoot.GetComponent<LODGroup>();
        if (group != null)
        {
            LOD[] lods = group.GetLODs();
            if (lods.Length != 4)
                throw new InvalidOperationException("Danchi clothes hardware must remain governed by the four-level Danchi LOD group.");
            var lod0 = new HashSet<Renderer>(lods[0].renderers.Where(x => x != null));
            foreach (MeshRenderer renderer in all)
                if (!lod0.Contains(renderer))
                    throw new InvalidOperationException("Refined clothes hardware missing from Danchi LOD0: " + HierarchyPath(renderer.transform));
        }
    }

    private static void RefineSupport(Transform bayRoot, int floor, int bay, int side, Material material)
    {
        string sideName = side < 0 ? "L" : "R";
        MeshRenderer arm = FindChildRenderer(bayRoot, $"HD_ClothesBracket_{side}");
        MeshRenderer receiver = FindChildRenderer(bayRoot, $"HD_ClothesReceiver_{side}");
        if (arm == null || receiver == null)
            throw new InvalidOperationException($"Legacy clothes support missing before refinement: floor={floor}, bay={bay}, side={side}.");

        float x = side * 1.47f;
        Vector3 ringCenter = new Vector3(x, 0.600f, -6.720f);
        const float verticalDrop = 0.290f;
        float rearward = Mathf.Sqrt(ArmLengthM * ArmLengthM - verticalDrop * verticalDrop);
        Vector3 pivotCenter = new Vector3(x, ringCenter.y - verticalDrop, ringCenter.z - rearward);
        Vector3 plateCenter = new Vector3(x + side * 0.004f, pivotCenter.y, pivotCenter.z);
        Vector3 armVector = ringCenter - pivotCenter;
        if (Mathf.Abs(armVector.magnitude - ArmLengthM) > 0.0005f)
            throw new InvalidOperationException("Computed clothes-support load path no longer matches nominal arm length.");

        arm.gameObject.name = $"HD_ClothesBracket_{sideName}";
        arm.GetComponent<MeshFilter>().sharedMesh = QualityBlockDetailMeshLibrary.GetChamferedBox(new Vector3(ArmWidthM, ArmLengthM, ArmDepthM));
        arm.transform.localPosition = (pivotCenter + ringCenter) * 0.5f;
        arm.transform.localRotation = Quaternion.FromToRotation(Vector3.up, armVector.normalized);
        arm.transform.localScale = Vector3.one;
        arm.sharedMaterial = material;
        RemoveCollider(arm.gameObject);
        AttachWeathering(arm.gameObject, false, 0.44f, 0.36f, 0.16f);

        receiver.gameObject.name = $"HD_ClothesReceiver_{sideName}";
        receiver.GetComponent<MeshFilter>().sharedMesh = GetReceiverRingMesh();
        receiver.transform.localPosition = ringCenter;
        receiver.transform.localRotation = Quaternion.Euler(0f, 0f, -90f); // source ring Y axis -> facade/pole X axis
        receiver.transform.localScale = Vector3.one;
        receiver.sharedMaterial = material;
        RemoveCollider(receiver.gameObject);
        AttachWeathering(receiver.gameObject, false, 0.56f, 0.42f, 0.22f);

        AddPart($"HD_ClothesBasePlate_{sideName}", bayRoot, plateCenter, Quaternion.identity,
            QualityBlockDetailMeshLibrary.GetChamferedBox(new Vector3(BackPlateThicknessM, BackPlateHeightM, BackPlateDepthM)),
            material, false, 0.32f, 0.28f, 0.08f);

        AddPart($"HD_ClothesPivot_{sideName}", bayRoot, pivotCenter + new Vector3(-side * 0.010f, 0f, 0f),
            Quaternion.Euler(0f, 0f, -90f),
            QualityBlockDetailMeshLibrary.GetBeveledCylinder(new Vector3(PivotDiameterM, PivotAxialDepthM * 0.5f, PivotDiameterM), false),
            material, false, 0.34f, 0.28f, 0.18f);

        for (int anchorSign = -1; anchorSign <= 1; anchorSign += 2)
        {
            Vector3 anchorPos = plateCenter + new Vector3(-side * (BackPlateThicknessM * 0.5f + 0.003f), anchorSign * AnchorPitchM * 0.5f, 0f);
            AddPart($"HD_ClothesAnchorBolt_{sideName}_{(anchorSign < 0 ? "Low" : "High")}", bayRoot, anchorPos,
                Quaternion.Euler(0f, 0f, -90f),
                QualityBlockDetailMeshLibrary.GetBeveledCylinder(new Vector3(AnchorHeadDiameterM, AnchorHeadDepthM * 0.5f, AnchorHeadDiameterM), true),
                material, true, 0.46f, 0.34f, 0.12f);
        }
    }

    private static MeshRenderer AddPart(
        string name, Transform parent, Vector3 localPosition, Quaternion localRotation, Mesh mesh, Material material,
        bool ferrousFastener, float rain, float sun, float contact)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = localRotation;
        go.transform.localScale = Vector3.one;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
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
        float half = ReceiverAxialDepthM * 0.5f;
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
            AddQuad(vertices, triangles, uv, o0 + Vector3.down * half, o1 + Vector3.down * half, o1 + Vector3.up * half, o0 + Vector3.up * half);
            AddQuad(vertices, triangles, uv, i1 + Vector3.down * half, i0 + Vector3.down * half, i0 + Vector3.up * half, i1 + Vector3.up * half);
            AddQuad(vertices, triangles, uv, i0 + Vector3.up * half, o0 + Vector3.up * half, o1 + Vector3.up * half, i1 + Vector3.up * half);
            AddQuad(vertices, triangles, uv, i1 + Vector3.down * half, o1 + Vector3.down * half, o0 + Vector3.down * half, i0 + Vector3.down * half);
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

    private static void AddQuad(List<Vector3> vertices, List<int> triangles, List<Vector2> uv, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        int start = vertices.Count;
        vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
        uv.Add(new Vector2(0f, 0f)); uv.Add(new Vector2(1f, 0f)); uv.Add(new Vector2(1f, 1f)); uv.Add(new Vector2(0f, 1f));
        triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
        triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
    }

    private static void AttachWeathering(GameObject go, bool ferrousFastener, float rain, float sun, float contact)
    {
        var metadata = go.GetComponent<QualityBlockWeatheringSurface>() ?? go.AddComponent<QualityBlockWeatheringSurface>();
        NewTownStainSource sources = NewTownStainSource.UVExposure | NewTownStainSource.RecessGrime;
        if (ferrousFastener) sources |= NewTownStainSource.FerrousFixture;
        metadata.Configure(
            NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.Sheltered | NewTownSurfaceExposure.SunExposed,
            sources, Mathf.Clamp01(rain), Mathf.Clamp01(sun), 0f, Mathf.Clamp01(contact));
    }

    private static void ValidateMaterial(Material material)
    {
        // Metallic/smoothness R/A textures are authoritative for these materials. The legacy danchi
        // builder rewrites ignored fallback scalar values when it rebuilds the scene, so checking the
        // scalar against the conductor range here would be a false failure. Reuse the canonical source
        // validator, then only require finite normalized fallbacks.
        QualityBlockDetailMaterialMicrostructureUpgrade.ValidateGeneratedAssets(false);

        if (material == null)
            throw new InvalidOperationException("Canonical galvanized material missing: " + MaterialPath);
        if (material.shader == null || material.shader.name != "Standard")
            throw new InvalidOperationException("Clothes hardware material must use Unity Standard PBR.");
        if (!material.HasProperty("_Metallic") || !material.HasProperty("_Glossiness") ||
            !material.HasProperty("_MetallicGlossMap") || material.GetTexture("_MetallicGlossMap") == null ||
            !material.IsKeywordEnabled("_METALLICGLOSSMAP"))
            throw new InvalidOperationException("Clothes hardware is missing canonical metallic/smoothness-map authority.");
        if (!IsFiniteUnit(material.GetFloat("_Metallic")) || !IsFiniteUnit(material.GetFloat("_Glossiness")))
            throw new InvalidOperationException("Clothes hardware fallback metallic/smoothness scalars must be finite values in [0,1].");
        if (material.IsKeywordEnabled("_EMISSION") ||
            (material.HasProperty("_EmissionColor") && material.GetColor("_EmissionColor").maxColorComponent > 0.0001f))
            throw new InvalidOperationException("Clothes hardware may not contain emissive/baked-highlight energy.");
    }

    private static bool IsFiniteUnit(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 1f;

    private static MeshRenderer[] SourceRenderers(GameObject detailRoot, string prefix)
    {
        return detailRoot.GetComponentsInChildren<MeshRenderer>(true)
            .Where(r => !IsUnderLodProxy(r.transform, detailRoot.transform))
            .Where(r => r.gameObject.name.StartsWith(prefix, StringComparison.Ordinal))
            .ToArray();
    }

    private static bool IsUnderLodProxy(Transform transform, Transform detailRoot)
    {
        for (Transform current = transform; current != null && current != detailRoot; current = current.parent)
            if (current.name == "HD_LOD1_Proxy" || current.name == "HD_LOD2_Proxy" || current.name == "HD_LOD3_Proxy")
                return true;
        return false;
    }

    private static MeshRenderer FindChildRenderer(Transform parent, string exactName)
    {
        return parent.GetComponentsInChildren<MeshRenderer>(true)
            .FirstOrDefault(r => r.gameObject.name == exactName);
    }

    private static void RemoveCollider(GameObject go)
    {
        Collider collider = go.GetComponent<Collider>();
        if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
    }

    private static void OnSceneSaving(Scene scene, string path)
    {
        if (applyingOrValidating || !scene.IsValid() || path != ScenePath || IsAuthoredDanchiActive()) return;
        GameObject root = FindSceneObject(DetailRootName);
        if (root == null) return;
        if (root.GetComponent<QualityBlockClothesDryingHardwareManifest>() == null) ApplyToOpenScene();
        else ValidateOpenScene();
    }

    private static void OnCameraPreCull(Camera camera)
    {
        if (camera == null || camera.targetTexture == null ||
            !EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath ||
            !IsFormalEvidenceTarget(camera.targetTexture.name) || lastFormalValidationFrame == Time.frameCount)
            return;
        ValidateOpenScene();
        lastFormalValidationFrame = Time.frameCount;
    }

    private static bool IsFormalEvidenceTarget(string name)
    {
        return !string.IsNullOrEmpty(name) &&
               (name.StartsWith("QA4K_", StringComparison.Ordinal) ||
                name.StartsWith("QATemporal_", StringComparison.Ordinal) ||
                name.StartsWith("QAPreparedTemporal_", StringComparison.Ordinal));
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
        if (!scene.IsValid() || scene.path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x != null && x.scene.IsValid() && x.scene.path == ScenePath && x.name == name);
    }

    private static string HierarchyPath(Transform transform)
    {
        var names = new List<string>();
        for (Transform current = transform; current != null; current = current.parent) names.Add(current.name);
        names.Reverse();
        return string.Join("/", names);
    }

    private static ClothesHardwareContract LoadContract()
    {
        string path = AbsolutePath(ContractPath);
        if (!File.Exists(path)) throw new FileNotFoundException("Clothes-drying hardware contract missing: " + ContractPath);
        return JsonUtility.FromJson<ClothesHardwareContract>(File.ReadAllText(path));
    }

    private static string AbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private static int Key(float meters) => Mathf.RoundToInt(Mathf.Abs(meters) * 10000f);
    private static void Require(bool ok, string message, List<string> errors) { if (!ok) errors.Add(message); }
    private static void RequireNear(float actual, float expected, float tolerance, string field, List<string> errors)
    {
        if (Mathf.Abs(actual - expected) > tolerance) errors.Add($"{field} must be {expected:0.####}, got {actual:0.####}");
    }
    private static void RequireNearOrThrow(float actual, float expected, float tolerance, string field)
    {
        if (Mathf.Abs(actual - expected) > tolerance) throw new InvalidOperationException($"{field} must be {expected:0.####}, got {actual:0.####}.");
    }
    private static void RequireExactSet(string[] actual, string[] expected, string label, List<string> errors)
    {
        if (!new HashSet<string>(actual ?? Array.Empty<string>(), StringComparer.Ordinal).SetEquals(expected))
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

    public void Configure(int bays, int supports, int rendererCount, float armLength, float receiverOd, float receiverId,
        float receiverDepth, float anchorPitch, string axis)
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
