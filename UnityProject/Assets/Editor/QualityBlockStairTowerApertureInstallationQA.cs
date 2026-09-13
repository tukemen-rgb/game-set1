using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Reconstructs the generated stair tower as an RC shell with five real rough openings instead of
/// leaving transparent stair glazing in front of one opaque StairTower box. The original collider is
/// retained for gameplay while only its renderer is disabled. The new front wall has physical depth,
/// the existing high-detail sash is seated into the opening, a four-sided EPDM/sealant perimeter is
/// supplied at near LODs, and stair glass/backing planes are moved behind the facade plane.
///
/// This is implementation-readiness work only. Passing source/runtime QA awards zero Visual Fidelity
/// points and cannot clear light leaks, primitive reads, reflection errors, repetition or LOD pop until
/// native 3840x2160 hero/oblique/grazing evidence and 100% crops are reviewed.
/// </summary>
public static class QualityBlockStairTowerApertureInstallationQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/stair_tower_aperture_installation_contract.json";
    private const string LookdevPath = "Assets/QA/stair_tower_aperture_lookdev.svg";
    private const string RuntimeReportPath = "Assets/QA/stair_tower_aperture_runtime_report.json";
    private const string RootName = "StairTowerApertureShell";
    private const string StairTowerName = "StairTower";
    private const string DanchiName = "Danchi";
    private const string MeshRoot = "Assets/Art/GeneratedStairTowerApertureMeshes";
    private const string SourceRcMaterialPath = "Assets/Art/GeneratedFacadeOptics/MAT_FacadePaintedRC_Stair.mat";
    private const string PhysicalRcMaterialPath = "Assets/Art/GeneratedFacadeOptics/MAT_FacadePaintedRC_StairAperturePhysicalUV.mat";
    private const string SealMaterialPath = "Assets/Art/GeneratedDetailMaterials/MAT_WindowRubber.mat";
    private const string SashMaterialPath = "Assets/Art/GeneratedDetailMaterials/MAT_StairSashAnodizedAluminum.mat";

    private const float TowerCenterX = -8.0f;
    private const float TowerXMin = -9.6f;
    private const float TowerXMax = -6.4f;
    private const float TowerYMin = 0.0f;
    private const float TowerYMax = 13.8f;
    private const float TowerZFront = -5.35f;
    private const float TowerZBack = -7.35f;
    private const float FrontShellDepth = 0.22f;
    private const float FrontShellBackZ = TowerZFront - FrontShellDepth;
    private const float FrontShellCenterZ = (TowerZFront + FrontShellBackZ) * 0.5f;
    private const float PerimeterShellThickness = 0.18f;

    private const float FirstWindowY = 1.55f;
    private const float FloorPitch = 2.55f;
    private const int WindowCount = 5;
    private const float FrameOuterWidth = 1.46f;
    private const float FrameOuterHeight = 1.48f;
    private const float RoughOpeningWidth = 1.52f;
    private const float RoughOpeningHeight = 1.54f;
    private const float PerimeterClearanceEachSide = 0.03f;
    private const float SealVisibleWidth = 0.014f;
    private const float SealDepth = 0.012f;
    private const float ClearCoreInset = 0.075f;
    private const float ExistingFrameAssemblyShiftZ = -0.070f;
    private const float GlassPlaneZ = -5.370f;
    private const float BackingPlaneZ = -5.520f;
    private const float MacroTileMeters = 2.40f;
    private const float DetailTileMeters = 0.22f;
    private const float DetailScale = MacroTileMeters / DetailTileMeters;
    private const float PositionTolerance = 0.003f;

    private static readonly float[] LodHeights = { 0.45f, 0.22f, 0.09f, 0.025f };

    [MenuItem("NewTown/Geometry/Reconstruct Stair Tower Window Apertures")]
    public static void ApplyAndPersist()
    {
        EnsureSceneOpen();
        ValidateContractConfigOnly();

        if (GeneratedDanchiIsReplacedByAuthoredArt())
        {
            GameObject stale = FindSceneObject(RootName);
            if (stale != null) UnityEngine.Object.DestroyImmediate(stale);
            Debug.Log("Danchi authored replacement is active; generated stair-tower aperture reconstruction was skipped.");
            return;
        }

        GameObject danchi = FindSceneObject(DanchiName);
        GameObject stairTower = FindSceneObject(StairTowerName);
        if (danchi == null || stairTower == null)
            throw new InvalidOperationException("Danchi/StairTower is missing; stair-tower opening reconstruction cannot continue.");

        Material sourceRc = AssetDatabase.LoadAssetAtPath<Material>(SourceRcMaterialPath);
        Material seal = AssetDatabase.LoadAssetAtPath<Material>(SealMaterialPath);
        if (sourceRc == null)
            throw new InvalidOperationException($"Stair facade RC source material missing: {SourceRcMaterialPath}. Run facade optics first.");
        if (seal == null)
            throw new InvalidOperationException($"Window rubber/seal material missing: {SealMaterialPath}. Run danchi detail first.");

        Material physicalRc = GetOrCreatePhysicalRcMaterial(sourceRc);
        Material sash = GetOrCreateSashMaterial();

        GameObject old = FindSceneObject(RootName);
        if (old != null) UnityEngine.Object.DestroyImmediate(old);

        Renderer legacyRenderer = stairTower.GetComponent<Renderer>();
        if (legacyRenderer == null)
            throw new InvalidOperationException("StairTower has no renderer to replace with an aperture shell.");
        legacyRenderer.enabled = false;

        Directory.CreateDirectory(AbsolutePath(MeshRoot));
        var root = new GameObject(RootName);
        root.transform.SetParent(danchi.transform, false);

        var lodRenderers = new Renderer[4][];
        for (int lod = 0; lod < 4; lod++)
        {
            var tier = new GameObject($"LOD{lod}");
            tier.transform.SetParent(root.transform, false);
            BuildTowerShellTier(tier.transform, lod, physicalRc, seal, danchi.transform);
            lodRenderers[lod] = tier.GetComponentsInChildren<Renderer>(true);
        }

        var group = root.AddComponent<LODGroup>();
        group.fadeMode = LODFadeMode.CrossFade;
        group.animateCrossFading = true;
        var lods = new LOD[4];
        for (int i = 0; i < 4; i++)
            lods[i] = new LOD(LodHeights[i], lodRenderers[i]);
        group.SetLODs(lods);
        group.RecalculateBounds();

        SeatExistingSashAndOptics(sash);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.Refresh();
        ValidateOpenScene();

        Debug.Log(
            "Stair tower reconstructed as a four-LOD RC shell with five true rough openings, physical reveal depth, seated sash/glass and near-LOD perimeter seals. " +
            "Visual Fidelity remains UNSCORED pending native 4K review.");
    }

    [MenuItem("NewTown/QA/Validate Stair Tower Aperture Contract")]
    public static void ValidateContractConfigOnly()
    {
        string absolute = AbsolutePath(ContractPath);
        if (!File.Exists(absolute))
            throw new FileNotFoundException($"Stair tower aperture contract missing: {ContractPath}");
        if (!File.Exists(AbsolutePath(LookdevPath)))
            throw new FileNotFoundException($"Stair tower aperture lookdev illustration missing: {LookdevPath}");

        Contract contract = JsonUtility.FromJson<Contract>(File.ReadAllText(absolute));
        if (contract == null || contract.geometry == null || contract.qa == null || contract.materials == null)
            throw new InvalidOperationException("Stair tower aperture contract is null or incomplete.");

        var errors = new List<string>();
        RequireEqual(contract.scenePath, ScenePath, "scenePath", errors);
        RequireEqual(contract.rootName, RootName, "rootName", errors);
        RequireEqual(contract.stairTowerName, StairTowerName, "stairTowerName", errors);
        Require(contract.expectedWindowCount == WindowCount, $"expectedWindowCount must be {WindowCount}", errors);
        RequireNear(contract.geometry.towerWidthM, TowerXMax - TowerXMin, 0.0001f, "towerWidthM", errors);
        RequireNear(contract.geometry.towerHeightM, TowerYMax - TowerYMin, 0.0001f, "towerHeightM", errors);
        RequireNear(contract.geometry.towerDepthM, TowerZFront - TowerZBack, 0.0001f, "towerDepthM", errors);
        RequireNear(contract.geometry.frontFacadePlaneZ, TowerZFront, 0.0001f, "frontFacadePlaneZ", errors);
        RequireNear(contract.geometry.frontShellDepthM, FrontShellDepth, 0.0001f, "frontShellDepthM", errors);
        RequireNear(contract.geometry.frameOuterWidthM, FrameOuterWidth, 0.0001f, "frameOuterWidthM", errors);
        RequireNear(contract.geometry.frameOuterHeightM, FrameOuterHeight, 0.0001f, "frameOuterHeightM", errors);
        RequireNear(contract.geometry.roughOpeningWidthM, RoughOpeningWidth, 0.0001f, "roughOpeningWidthM", errors);
        RequireNear(contract.geometry.roughOpeningHeightM, RoughOpeningHeight, 0.0001f, "roughOpeningHeightM", errors);
        RequireNear(contract.geometry.perimeterClearanceEachSideM, PerimeterClearanceEachSide, 0.0001f, "perimeterClearanceEachSideM", errors);
        RequireNear(contract.geometry.glassPlaneZ, GlassPlaneZ, 0.0001f, "glassPlaneZ", errors);
        RequireNear(contract.geometry.backingPlaneZ, BackingPlaneZ, 0.0001f, "backingPlaneZ", errors);
        RequireNear(contract.geometry.macroTileMeters, MacroTileMeters, 0.0001f, "macroTileMeters", errors);
        RequireNear(contract.geometry.detailTileMeters, DetailTileMeters, 0.0001f, "detailTileMeters", errors);
        Require(contract.geometry.legacyRendererDisabledColliderPreserved,
            "legacyRendererDisabledColliderPreserved must be true", errors);
        Require(contract.geometry.fourLodShellRequired, "fourLodShellRequired must be true", errors);
        Require(contract.qa.requiredLodCount == 4, "requiredLodCount must be 4", errors);
        Require(contract.qa.requiredNearLodSealParts == WindowCount * 4 * 2,
            $"requiredNearLodSealParts must be {WindowCount * 4 * 2}", errors);
        RequireNear(contract.qa.clearCoreInsetM, ClearCoreInset, 0.0001f, "clearCoreInsetM", errors);
        RequireNear(contract.qa.positionToleranceM, PositionTolerance, 0.0001f, "positionToleranceM", errors);
        Require(contract.qa.automaticVisualPoints == 0, "automaticVisualPoints must remain 0", errors);
        Require(contract.qa.renderVerificationPending, "renderVerificationPending must remain true", errors);

        foreach (string metadata in new[]
        {
            contract.manufacture, contract.dimensionsThickness, contract.materialsFinish,
            contract.mounting, contract.interfacesGapsSeals, contract.orientationExposure,
            contract.aging, contract.geometryVsMaterial, contract.lodPolicy, contract.referenceBasis
        })
            Require(!string.IsNullOrWhiteSpace(metadata), "mandatory manufacture/installation metadata field is empty", errors);

        string[] requiredMaterials = { "painted_rc", "anodized_aluminum_sash", "epdm_sealant", "clear_glass", "stairwell_backing" };
        foreach (string id in requiredMaterials)
        {
            MaterialSpec spec = contract.materials.FirstOrDefault(x => x != null && x.id == id);
            if (spec == null)
            {
                errors.Add($"material specification missing: {id}");
                continue;
            }
            Require(spec.albedoSrgbMin >= 0f && spec.albedoSrgbMax <= 1f && spec.albedoSrgbMin <= spec.albedoSrgbMax,
                $"{id} albedo range invalid", errors);
            Require(spec.roughnessMin >= 0f && spec.roughnessMax <= 1f && spec.roughnessMin <= spec.roughnessMax,
                $"{id} roughness range invalid", errors);
            Require(spec.metallic >= 0f && spec.metallic <= 1f, $"{id} metallic invalid", errors);
            Require(spec.specularF0 >= 0f && spec.specularF0 <= 1f, $"{id} specularF0 invalid", errors);
            Require(!string.IsNullOrWhiteSpace(spec.normalScale), $"{id} normalScale missing", errors);
            Require(!string.IsNullOrWhiteSpace(spec.microstructure), $"{id} microstructure missing", errors);
            Require(!string.IsNullOrWhiteSpace(spec.wetness), $"{id} wetness missing", errors);
            Require(!string.IsNullOrWhiteSpace(spec.uvAging), $"{id} uvAging missing", errors);
            Require(!string.IsNullOrWhiteSpace(spec.fresnelResponse), $"{id} fresnelResponse missing", errors);
        }

        if (errors.Count > 0)
            throw new InvalidOperationException("Stair tower aperture installation contract FAILED:\n - " + string.Join("\n - ", errors));
    }

    [MenuItem("NewTown/QA/Validate Reconstructed Stair Tower Apertures")]
    public static void ValidateOpenScene()
    {
        EnsureSceneOpen();
        ValidateContractConfigOnly();

        if (GeneratedDanchiIsReplacedByAuthoredArt())
        {
            Debug.Log("Danchi authored replacement is active; generated stair-tower aperture QA is not applicable.");
            return;
        }

        GameObject danchi = FindSceneObject(DanchiName);
        GameObject stairTower = FindSceneObject(StairTowerName);
        GameObject root = FindSceneObject(RootName);
        if (danchi == null || stairTower == null || root == null)
            throw new InvalidOperationException("Danchi/StairTower/StairTowerApertureShell missing from prepared scene.");

        Renderer legacy = stairTower.GetComponent<Renderer>();
        if (legacy == null || legacy.enabled)
            throw new InvalidOperationException(
                "StairTower renderer must be disabled; otherwise the opaque legacy volume fills all five reconstructed openings.");
        if (stairTower.GetComponent<Collider>() == null)
            throw new InvalidOperationException("StairTower gameplay collider must be preserved while replacing only its renderer.");

        LODGroup group = root.GetComponent<LODGroup>();
        if (group == null || group.GetLODs().Length != 4)
            throw new InvalidOperationException("Stair tower aperture shell must expose LOD0/1/2/3.");
        if (!group.animateCrossFading || group.fadeMode != LODFadeMode.CrossFade)
            throw new InvalidOperationException("Stair tower aperture shell must use animated cross-fade to reduce visible LOD pop.");

        Material rc = AssetDatabase.LoadAssetAtPath<Material>(PhysicalRcMaterialPath);
        Material sash = AssetDatabase.LoadAssetAtPath<Material>(SashMaterialPath);
        if (rc == null || sash == null)
            throw new InvalidOperationException("Stair tower physical RC/sash material is missing.");
        ValidatePhysicalRcMaterial(rc);
        ValidateSashMaterial(sash);

        Renderer[] rcRenderers = root.GetComponentsInChildren<Renderer>(true)
            .Where(x => x.gameObject.name.StartsWith("ST_RC_", StringComparison.Ordinal))
            .ToArray();
        if (rcRenderers.Length != 13 * 4)
            throw new InvalidOperationException($"Expected {13 * 4} RC shell renderers across four LODs, found {rcRenderers.Length}.");
        foreach (Renderer renderer in rcRenderers)
        {
            if (renderer.sharedMaterial != rc)
                throw new InvalidOperationException($"Stair tower RC renderer has wrong material: {HierarchyPath(renderer.transform)}");
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null || mesh.name == "Cube" || mesh.name == "Cylinder" || mesh.name == "Sphere" || mesh.name == "Capsule")
                throw new InvalidOperationException($"Stair tower shell exposes stock/invalid primitive geometry: {HierarchyPath(renderer.transform)}");
            string meshPath = AssetDatabase.GetAssetPath(mesh);
            if (string.IsNullOrWhiteSpace(meshPath) || !meshPath.StartsWith(MeshRoot + "/", StringComparison.Ordinal))
                throw new InvalidOperationException($"Stair tower RC mesh is not persisted under {MeshRoot}: {HierarchyPath(renderer.transform)}");
        }

        Renderer[] seals = root.GetComponentsInChildren<Renderer>(true)
            .Where(x => x.gameObject.name.StartsWith("ST_Seal_", StringComparison.Ordinal))
            .ToArray();
        if (seals.Length != WindowCount * 4 * 2)
            throw new InvalidOperationException(
                $"Near LODs require {WindowCount * 4 * 2} four-sided perimeter seal renderers, found {seals.Length}.");

        int checkedOpenings = 0;
        for (int floor = 0; floor < WindowCount; floor++)
        {
            float y = FirstWindowY + floor * FloorPitch;
            Bounds clearCore = new Bounds(
                danchi.transform.TransformPoint(new Vector3(TowerCenterX, y, FrontShellCenterZ)),
                new Vector3(RoughOpeningWidth - ClearCoreInset * 2f,
                    RoughOpeningHeight - ClearCoreInset * 2f,
                    FrontShellDepth - 0.02f));

            foreach (Renderer wall in rcRenderers)
            {
                if (wall.enabled && wall.gameObject.activeInHierarchy && wall.bounds.Intersects(clearCore))
                    throw new InvalidOperationException(
                        $"Opaque stair tower RC intersects rough-opening clear core at floor={floor}: {HierarchyPath(wall.transform)}");
            }

            GameObject frameAssembly = FindSceneObject($"HD_StairWindowAssembly_{floor}");
            if (frameAssembly == null || Mathf.Abs(frameAssembly.transform.localPosition.z - ExistingFrameAssemblyShiftZ) > PositionTolerance)
                throw new InvalidOperationException($"Stair sash assembly {floor} is not seated at the locked facade depth.");

            foreach (Renderer framePart in frameAssembly.GetComponentsInChildren<Renderer>(true)
                         .Where(x => x.gameObject.name.StartsWith("HD_StairFrame", StringComparison.Ordinal)))
                if (framePart.sharedMaterial != sash)
                    throw new InvalidOperationException($"Stair sash part uses the wrong material: {HierarchyPath(framePart.transform)}");

            GameObject oldOneSidedSeal = frameAssembly.transform.Cast<Transform>()
                .Select(x => x.gameObject)
                .FirstOrDefault(x => x.name == "HD_StairSeal");
            Renderer oldSealRenderer = oldOneSidedSeal != null ? oldOneSidedSeal.GetComponent<Renderer>() : null;
            if (oldSealRenderer != null && oldSealRenderer.enabled)
                throw new InvalidOperationException($"Legacy one-sided stair seal remains visible at floor={floor}; four-sided perimeter seal must own the interface.");

            GameObject glass = FindSceneObject($"FO_StairGlass_{floor}");
            GameObject backing = FindSceneObject($"FO_StairBacking_{floor}");
            if (glass == null || backing == null)
                throw new InvalidOperationException($"Stair glass/backing optical stack missing at floor={floor}.");
            float glassZ = danchi.transform.InverseTransformPoint(glass.transform.position).z;
            float backingZ = danchi.transform.InverseTransformPoint(backing.transform.position).z;
            if (Mathf.Abs(glassZ - GlassPlaneZ) > PositionTolerance)
                throw new InvalidOperationException($"Stair glass depth invalid at floor={floor}: {glassZ:F4}.");
            if (Mathf.Abs(backingZ - BackingPlaneZ) > PositionTolerance)
                throw new InvalidOperationException($"Stair backing depth invalid at floor={floor}: {backingZ:F4}.");
            if (!(glassZ < TowerZFront && glassZ > FrontShellBackZ))
                throw new InvalidOperationException($"Stair glass must remain behind the facade face but ahead of shell back at floor={floor}.");
            if (!(backingZ < glassZ && backingZ > FrontShellBackZ))
                throw new InvalidOperationException($"Stairwell backing must remain behind glass but inside the reconstructed shell at floor={floor}.");

            checkedOpenings++;
        }

        WriteRuntimeReport(checkedOpenings, rcRenderers.Length, seals.Length);
        AssetDatabase.Refresh();
        Debug.Log(
            $"Stair tower aperture QA passed in scene state: openings={checkedOpenings}, rcRenderers={rcRenderers.Length}, seals={seals.Length}. " +
            "This is implementation evidence only; Visual Fidelity remains UNSCORED pending native 4K/100% crop review.");
    }

    private static void BuildTowerShellTier(Transform tier, int lod, Material rc, Material seal, Transform danchi)
    {
        float width = TowerXMax - TowerXMin;
        float height = TowerYMax - TowerYMin;
        float depth = TowerZFront - TowerZBack;
        float cy = (TowerYMin + TowerYMax) * 0.5f;
        float cz = (TowerZFront + TowerZBack) * 0.5f;

        AddRcBox($"ST_RC_LOD{lod}_Back", tier,
            new Vector3(TowerCenterX, cy, TowerZBack + PerimeterShellThickness * 0.5f),
            new Vector3(width, height, PerimeterShellThickness), rc, danchi, lod);
        AddRcBox($"ST_RC_LOD{lod}_LeftEnd", tier,
            new Vector3(TowerXMin + PerimeterShellThickness * 0.5f, cy, cz),
            new Vector3(PerimeterShellThickness, height, depth), rc, danchi, lod);
        AddRcBox($"ST_RC_LOD{lod}_RightEnd", tier,
            new Vector3(TowerXMax - PerimeterShellThickness * 0.5f, cy, cz),
            new Vector3(PerimeterShellThickness, height, depth), rc, danchi, lod);
        AddRcBox($"ST_RC_LOD{lod}_Roof", tier,
            new Vector3(TowerCenterX, TowerYMax - PerimeterShellThickness * 0.5f, cz),
            new Vector3(width, PerimeterShellThickness, depth), rc, danchi, lod);
        AddRcBox($"ST_RC_LOD{lod}_Base", tier,
            new Vector3(TowerCenterX, TowerYMin + PerimeterShellThickness * 0.5f, cz),
            new Vector3(width, PerimeterShellThickness, depth), rc, danchi, lod);

        float openingHalfW = RoughOpeningWidth * 0.5f;
        float frontCenterZ = FrontShellCenterZ;
        AddRcBox($"ST_RC_LOD{lod}_FrontLeftPier", tier,
            new Vector3((TowerXMin + TowerCenterX - openingHalfW) * 0.5f, cy, frontCenterZ),
            new Vector3((TowerCenterX - openingHalfW) - TowerXMin, height, FrontShellDepth), rc, danchi, lod);
        AddRcBox($"ST_RC_LOD{lod}_FrontRightPier", tier,
            new Vector3((TowerCenterX + openingHalfW + TowerXMax) * 0.5f, cy, frontCenterZ),
            new Vector3(TowerXMax - (TowerCenterX + openingHalfW), height, FrontShellDepth), rc, danchi, lod);

        float previousTop = TowerYMin;
        for (int floor = 0; floor < WindowCount; floor++)
        {
            float y = FirstWindowY + floor * FloorPitch;
            float openingBottom = y - RoughOpeningHeight * 0.5f;
            if (openingBottom > previousTop + 0.001f)
                AddRcBox($"ST_RC_LOD{lod}_Spandrel_{floor}", tier,
                    new Vector3(TowerCenterX, (previousTop + openingBottom) * 0.5f, frontCenterZ),
                    new Vector3(RoughOpeningWidth, openingBottom - previousTop, FrontShellDepth), rc, danchi, lod);
            previousTop = y + RoughOpeningHeight * 0.5f;

            if (lod <= 1)
                AddPerimeterSeal(tier, lod, floor, y, seal);
        }
        if (TowerYMax > previousTop + 0.001f)
            AddRcBox($"ST_RC_LOD{lod}_Spandrel_Top", tier,
                new Vector3(TowerCenterX, (previousTop + TowerYMax) * 0.5f, frontCenterZ),
                new Vector3(RoughOpeningWidth, TowerYMax - previousTop, FrontShellDepth), rc, danchi, lod);
    }

    private static void AddPerimeterSeal(Transform parent, int lod, int floor, float y, Material seal)
    {
        float z = TowerZFront + SealDepth * 0.18f;
        float horizontalWidth = FrameOuterWidth + SealVisibleWidth * 2f;
        float verticalHeight = FrameOuterHeight;
        AddDetailBox($"ST_Seal_LOD{lod}_{floor}_L", parent,
            new Vector3(TowerCenterX - FrameOuterWidth * 0.5f - SealVisibleWidth * 0.5f, y, z),
            new Vector3(SealVisibleWidth, verticalHeight, SealDepth), seal);
        AddDetailBox($"ST_Seal_LOD{lod}_{floor}_R", parent,
            new Vector3(TowerCenterX + FrameOuterWidth * 0.5f + SealVisibleWidth * 0.5f, y, z),
            new Vector3(SealVisibleWidth, verticalHeight, SealDepth), seal);
        AddDetailBox($"ST_Seal_LOD{lod}_{floor}_T", parent,
            new Vector3(TowerCenterX, y + FrameOuterHeight * 0.5f + SealVisibleWidth * 0.5f, z),
            new Vector3(horizontalWidth, SealVisibleWidth, SealDepth), seal);
        AddDetailBox($"ST_Seal_LOD{lod}_{floor}_B", parent,
            new Vector3(TowerCenterX, y - FrameOuterHeight * 0.5f - SealVisibleWidth * 0.5f, z),
            new Vector3(horizontalWidth, SealVisibleWidth, SealDepth), seal);
    }

    private static void SeatExistingSashAndOptics(Material sash)
    {
        for (int floor = 0; floor < WindowCount; floor++)
        {
            GameObject assembly = FindSceneObject($"HD_StairWindowAssembly_{floor}");
            if (assembly == null)
                throw new InvalidOperationException($"HD stair window assembly missing: floor={floor}. Run danchi detail first.");
            Vector3 ap = assembly.transform.localPosition;
            ap.z = ExistingFrameAssemblyShiftZ;
            assembly.transform.localPosition = ap;

            foreach (Renderer r in assembly.GetComponentsInChildren<Renderer>(true))
            {
                if (r.gameObject.name.StartsWith("HD_StairFrame", StringComparison.Ordinal))
                    r.sharedMaterial = sash;
                if (r.gameObject.name == "HD_StairSeal")
                    r.enabled = false;
            }

            GameObject glass = FindSceneObject($"FO_StairGlass_{floor}");
            GameObject backing = FindSceneObject($"FO_StairBacking_{floor}");
            if (glass == null || backing == null)
                throw new InvalidOperationException($"Facade optics stair glass/backing missing: floor={floor}. Run facade optics first.");

            Vector3 gp = glass.transform.localPosition;
            gp.z = GlassPlaneZ;
            glass.transform.localPosition = gp;
            Vector3 bp = backing.transform.localPosition;
            bp.z = BackingPlaneZ;
            backing.transform.localPosition = bp;
            EditorUtility.SetDirty(glass.transform);
            EditorUtility.SetDirty(backing.transform);
        }
    }

    private static GameObject AddRcBox(string name, Transform parent, Vector3 localPosition, Vector3 size,
        Material material, Transform danchi, int lod)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        var filter = go.AddComponent<MeshFilter>();
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        filter.sharedMesh = GetOrCreateWorldAlignedBoxMesh(name, size, localPosition, lod);
        return go;
    }

    private static GameObject AddDetailBox(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        var filter = go.AddComponent<MeshFilter>();
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        filter.sharedMesh = QualityBlockSurfaceMeshLibrary.GetChamferedBox(size, 0.0012f);
        return go;
    }

    private static Mesh GetOrCreateWorldAlignedBoxMesh(string name, Vector3 size, Vector3 localCenter, int lod)
    {
        string path = $"{MeshRoot}/{Sanitize(name)}.asset";
        Mesh built = BuildWorldAlignedBoxMesh(size, localCenter);
        built.name = $"GM_STAIR_APERTURE_{Sanitize(name)}";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(built, path);
            return built;
        }
        EditorUtility.CopySerialized(built, existing);
        UnityEngine.Object.DestroyImmediate(built);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    private static Mesh BuildWorldAlignedBoxMesh(Vector3 size, Vector3 center)
    {
        float hx = size.x * 0.5f;
        float hy = size.y * 0.5f;
        float hz = size.z * 0.5f;
        var vertices = new List<Vector3>(24);
        var triangles = new List<int>(36);
        var uv = new List<Vector2>(24);
        AddFace(vertices, triangles, uv, center,
            new Vector3(-hx,-hy,hz), new Vector3(hx,-hy,hz), new Vector3(hx,hy,hz), new Vector3(-hx,hy,hz), Vector3.forward);
        AddFace(vertices, triangles, uv, center,
            new Vector3(hx,-hy,-hz), new Vector3(-hx,-hy,-hz), new Vector3(-hx,hy,-hz), new Vector3(hx,hy,-hz), Vector3.back);
        AddFace(vertices, triangles, uv, center,
            new Vector3(hx,-hy,hz), new Vector3(hx,-hy,-hz), new Vector3(hx,hy,-hz), new Vector3(hx,hy,hz), Vector3.right);
        AddFace(vertices, triangles, uv, center,
            new Vector3(-hx,-hy,-hz), new Vector3(-hx,-hy,hz), new Vector3(-hx,hy,hz), new Vector3(-hx,hy,-hz), Vector3.left);
        AddFace(vertices, triangles, uv, center,
            new Vector3(-hx,hy,hz), new Vector3(hx,hy,hz), new Vector3(hx,hy,-hz), new Vector3(-hx,hy,-hz), Vector3.up);
        AddFace(vertices, triangles, uv, center,
            new Vector3(-hx,-hy,-hz), new Vector3(hx,-hy,-hz), new Vector3(hx,-hy,hz), new Vector3(-hx,-hy,hz), Vector3.down);
        var mesh = new Mesh();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uv);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddFace(List<Vector3> vertices, List<int> triangles, List<Vector2> uv,
        Vector3 center, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 intendedNormal)
    {
        int start = vertices.Count;
        vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
        foreach (Vector3 v in new[] { a, b, c, d })
        {
            Vector3 p = center + v;
            Vector3 n = intendedNormal;
            Vector3 an = new Vector3(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z));
            if (an.z >= an.x && an.z >= an.y) uv.Add(new Vector2(p.x / MacroTileMeters, p.y / MacroTileMeters));
            else if (an.x >= an.y) uv.Add(new Vector2(p.z / MacroTileMeters, p.y / MacroTileMeters));
            else uv.Add(new Vector2(p.x / MacroTileMeters, p.z / MacroTileMeters));
        }
        Vector3 cross = Vector3.Cross(b - a, c - a);
        if (Vector3.Dot(cross, intendedNormal) >= 0f)
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

    private static Material GetOrCreatePhysicalRcMaterial(Material source)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(PhysicalRcMaterialPath);
        if (mat == null)
        {
            mat = new Material(source) { name = "MAT_FacadePaintedRC_StairAperturePhysicalUV" };
            AssetDatabase.CreateAsset(mat, PhysicalRcMaterialPath);
        }
        else
        {
            mat.CopyPropertiesFromMaterial(source);
            mat.shader = source.shader;
            mat.name = "MAT_FacadePaintedRC_StairAperturePhysicalUV";
        }
        foreach (string property in new[] { "_MainTex", "_BumpMap", "_MetallicGlossMap" })
            if (mat.HasProperty(property))
            {
                mat.SetTextureScale(property, Vector2.one);
                mat.SetTextureOffset(property, Vector2.zero);
            }
        if (mat.HasProperty("_DetailNormalMap"))
        {
            mat.SetTextureScale("_DetailNormalMap", new Vector2(DetailScale, DetailScale));
            mat.SetTextureOffset("_DetailNormalMap", Vector2.zero);
        }
        mat.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material GetOrCreateSashMaterial()
    {
        Shader shader = Shader.Find("Standard");
        if (shader == null) throw new InvalidOperationException("Standard shader not found for stair sash material.");
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(SashMaterialPath);
        if (mat == null)
        {
            mat = new Material(shader) { name = "MAT_StairSashAnodizedAluminum" };
            AssetDatabase.CreateAsset(mat, SashMaterialPath);
        }
        else mat.shader = shader;
        mat.color = new Color(0.52f, 0.535f, 0.53f, 1f);
        mat.SetFloat("_Metallic", 0.90f);
        mat.SetFloat("_Glossiness", 0.42f);
        if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.black);
        mat.DisableKeyword("_EMISSION");
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void ValidatePhysicalRcMaterial(Material mat)
    {
        if (mat.shader == null || mat.GetFloat("_Metallic") > 0.001f)
            throw new InvalidOperationException("Stair aperture RC must remain a non-metallic dielectric.");
        if (!mat.IsKeywordEnabled("_NORMALMAP") || !mat.IsKeywordEnabled("_DETAIL_MULX2"))
            throw new InvalidOperationException("Stair aperture RC lost required macro/detail normal response.");
        if (mat.HasProperty("_DetailNormalMap"))
        {
            Vector2 expected = new Vector2(DetailScale, DetailScale);
            if ((mat.GetTextureScale("_DetailNormalMap") - expected).sqrMagnitude > 0.0001f)
                throw new InvalidOperationException("Stair aperture RC detail-normal physical scale changed.");
        }
    }

    private static void ValidateSashMaterial(Material mat)
    {
        float metallic = mat.GetFloat("_Metallic");
        float smoothness = mat.GetFloat("_Glossiness");
        if (metallic < 0.82f || metallic > 1.0f)
            throw new InvalidOperationException($"Anodized aluminum sash metallic response outside locked range: {metallic:F3}.");
        if (smoothness < 0.34f || smoothness > 0.50f)
            throw new InvalidOperationException($"Aged anodized aluminum sash smoothness outside locked range: {smoothness:F3}.");
        if (mat.IsKeywordEnabled("_EMISSION"))
            throw new InvalidOperationException("Daytime stair sash may not be emissive.");
    }

    private static void WriteRuntimeReport(int openings, int rcRenderers, int seals)
    {
        var report = new RuntimeReport
        {
            generatedUtc = DateTime.UtcNow.ToString("O"),
            scenePath = ScenePath,
            openingsChecked = openings,
            rcRendererCountAcrossLods = rcRenderers,
            nearLodSealRendererCount = seals,
            glassPlaneZ = GlassPlaneZ,
            backingPlaneZ = BackingPlaneZ,
            visualFidelityStatus = "UNSCORED",
            visualPointsAwarded = 0,
            renderVerificationPending = true
        };
        File.WriteAllText(AbsolutePath(RuntimeReportPath), JsonUtility.ToJson(report, true));
    }

    private static bool GeneratedDanchiIsReplacedByAuthoredArt()
    {
        return Resources.FindObjectsOfTypeAll<QualityBlockArtSlot>()
            .Any(x => x.gameObject.scene.IsValid() && x.SlotId == "danchi.main" && x.IsUsingAuthoredArt);
    }

    private static void EnsureSceneOpen()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x != null && x.scene.IsValid() && x.scene.path == ScenePath && x.name == name);
    }

    private static string AbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrWhiteSpace(projectRoot)) throw new InvalidOperationException("Could not resolve Unity project root.");
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private static string HierarchyPath(Transform t)
    {
        var parts = new Stack<string>();
        while (t != null) { parts.Push(t.name); t = t.parent; }
        return string.Join("/", parts);
    }

    private static string Sanitize(string value)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
        return value.Replace('/', '_').Replace('\\', '_');
    }

    private static void Require(bool condition, string message, List<string> errors)
    {
        if (!condition) errors.Add(message);
    }

    private static void RequireNear(float actual, float expected, float tolerance, string field, List<string> errors)
    {
        if (Mathf.Abs(actual - expected) > tolerance) errors.Add($"{field}: expected {expected}, got {actual}");
    }

    private static void RequireEqual(string actual, string expected, string field, List<string> errors)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal)) errors.Add($"{field}: expected '{expected}', got '{actual}'");
    }

    [Serializable]
    private sealed class Contract
    {
        public string schemaVersion;
        public string scenePath;
        public string rootName;
        public string stairTowerName;
        public int expectedWindowCount;
        public string referenceBasis;
        public string manufacture;
        public string dimensionsThickness;
        public string materialsFinish;
        public string mounting;
        public string interfacesGapsSeals;
        public string orientationExposure;
        public string aging;
        public string geometryVsMaterial;
        public string lodPolicy;
        public GeometrySpec geometry;
        public MaterialSpec[] materials;
        public QaSpec qa;
    }

    [Serializable]
    private sealed class GeometrySpec
    {
        public float towerWidthM;
        public float towerHeightM;
        public float towerDepthM;
        public float frontFacadePlaneZ;
        public float frontShellDepthM;
        public float frameOuterWidthM;
        public float frameOuterHeightM;
        public float roughOpeningWidthM;
        public float roughOpeningHeightM;
        public float perimeterClearanceEachSideM;
        public float sealVisibleWidthM;
        public float sealDepthM;
        public float glassPlaneZ;
        public float backingPlaneZ;
        public float macroTileMeters;
        public float detailTileMeters;
        public bool legacyRendererDisabledColliderPreserved;
        public bool fourLodShellRequired;
    }

    [Serializable]
    private sealed class MaterialSpec
    {
        public string id;
        public float albedoSrgbMin;
        public float albedoSrgbMax;
        public float roughnessMin;
        public float roughnessMax;
        public float metallic;
        public float specularF0;
        public string normalScale;
        public string microstructure;
        public string wetness;
        public string uvAging;
        public string fresnelResponse;
    }

    [Serializable]
    private sealed class QaSpec
    {
        public int requiredLodCount;
        public int requiredNearLodSealParts;
        public float clearCoreInsetM;
        public float positionToleranceM;
        public int automaticVisualPoints;
        public bool renderVerificationPending;
    }

    [Serializable]
    private sealed class RuntimeReport
    {
        public string generatedUtc;
        public string scenePath;
        public int openingsChecked;
        public int rcRendererCountAcrossLods;
        public int nearLodSealRendererCount;
        public float glassPlaneZ;
        public float backingPlaneZ;
        public string visualFidelityStatus;
        public int visualPointsAwarded;
        public bool renderVerificationPending;
    }
}
