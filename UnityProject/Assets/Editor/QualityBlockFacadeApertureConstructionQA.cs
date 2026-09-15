using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Reconstructs the generated MainBlock facade as a render shell with real apartment-window apertures.
/// The original fallback is one opaque cube, so merely placing transparent glazing near its front face
/// cannot create a physically valid opening. This pass disables only the MainBlock renderer (retaining
/// its gameplay collider), builds side/back/roof/base shell pieces and a front RC wall segmented around
/// thirty rough openings, then seats a narrow dark perimeter seal between opening and existing sash.
///
/// This remains source/evidence-readiness work. Passing these checks awards zero Visual Fidelity points;
/// native 3840x2160 hero/oblique/grazing evidence is still required to judge occlusion, light leaks,
/// reflections, repetition and the final construction read.
/// </summary>
public static class QualityBlockFacadeApertureConstructionQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/facade_aperture_installation_contract.json";
    private const string LookdevPath = "Assets/QA/facade_window_aperture_lookdev.svg";
    private const string RootName = "DanchiFacadeApertureShell";
    private const string MainBlockName = "MainBlock";
    private const string FacadeMaterialPath = "Assets/Art/GeneratedFacadeOptics/MAT_FacadePaintedRC_Main.mat";
    private const string SealMaterialPath = "Assets/Art/GeneratedDetailMaterials/MAT_WindowRubber.mat";
    private const string RuntimeReportPath = "Assets/QA/facade_aperture_installation_runtime_report.json";

    private const float BuildingXMin = -21.0f;
    private const float BuildingXMax = 5.0f;
    private const float BuildingYMin = 0.0f;
    private const float BuildingYMax = 13.2f;
    private const float BuildingZFront = -7.3f;
    private const float BuildingZBack = -15.7f;
    private const float ShellDepth = 0.22f;
    private const float ShellBackZ = BuildingZFront - ShellDepth;
    private const float ShellCenterZ = (BuildingZFront + ShellBackZ) * 0.5f;
    private const float BayPitch = 4.15f;
    private const float FloorPitch = 2.55f;
    private const float FirstBayX = -18.3f;
    private const float FirstFloorY = 1.55f;
    private const float OpeningWidth = 2.40f;
    private const float OpeningHeight = 1.80f;
    private const float FrameOuterWidth = 2.36f;
    private const float FrameOuterHeight = 1.76f;
    private const float SealWidth = 0.014f;
    private const float SealDepth = 0.012f;
    private const float ClearCoreInset = 0.020f;
    private const int ExpectedWindows = 30;
    private const int ExpectedFacadeCellParts = ExpectedWindows * 4;
    private const int ExpectedSealParts = ExpectedWindows * 4;
    private const int ExpectedApartmentGlassPanes = ExpectedWindows * 2;

    [MenuItem("NewTown/Geometry/Reconstruct Facade Window Apertures")]
    public static void ApplyAndPersist()
    {
        EnsureSceneOpen();
        ValidateContractConfigOnly();

        if (GeneratedDanchiIsReplacedByAuthoredArt())
        {
            GameObject stale = FindSceneObject(RootName);
            if (stale != null) UnityEngine.Object.DestroyImmediate(stale);
            Debug.Log("Danchi authored replacement is active; generated facade-aperture reconstruction was skipped.");
            return;
        }

        GameObject danchi = FindSceneObject("Danchi");
        GameObject mainBlock = FindSceneObject(MainBlockName);
        if (danchi == null || mainBlock == null)
            throw new InvalidOperationException("Danchi/MainBlock is missing; facade aperture shell cannot be reconstructed.");

        Material facade = AssetDatabase.LoadAssetAtPath<Material>(FacadeMaterialPath);
        Material seal = AssetDatabase.LoadAssetAtPath<Material>(SealMaterialPath);
        if (facade == null)
            throw new InvalidOperationException($"Facade material missing: {FacadeMaterialPath}. Run facade optics first.");
        if (seal == null)
            throw new InvalidOperationException($"Window seal material missing: {SealMaterialPath}. Run danchi detail first.");

        GameObject old = FindSceneObject(RootName);
        if (old != null) UnityEngine.Object.DestroyImmediate(old);

        Renderer baseRenderer = mainBlock.GetComponent<Renderer>();
        if (baseRenderer == null)
            throw new InvalidOperationException("MainBlock has no Renderer to replace with an aperture shell.");
        baseRenderer.enabled = false;

        var root = new GameObject(RootName);
        root.transform.SetParent(danchi.transform, false);

        BuildOuterShell(root.transform, facade);
        BuildFrontFacadeAndSeals(root.transform, facade, seal);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.Refresh();

        ValidateOpenScene();
        Debug.Log(
            "Generated apartment facade reconstructed with real rough openings and perimeter seals; " +
            "MainBlock renderer is disabled while its collider remains available. Render verification remains pending.");
    }

    [MenuItem("NewTown/QA/Validate Facade Aperture Contract")]
    public static void ValidateContractConfigOnly()
    {
        string absolute = AbsolutePath(ContractPath);
        if (!File.Exists(absolute))
            throw new FileNotFoundException($"Facade aperture installation contract missing: {ContractPath}");
        if (!File.Exists(AbsolutePath(LookdevPath)))
            throw new FileNotFoundException($"Facade aperture lookdev illustration missing: {LookdevPath}");

        Contract contract = JsonUtility.FromJson<Contract>(File.ReadAllText(absolute));
        if (contract == null || contract.geometry == null || contract.qa == null)
            throw new InvalidOperationException("Facade aperture contract is null or incomplete.");

        var errors = new List<string>();
        RequireEqual(contract.scenePath, ScenePath, "scenePath", errors);
        RequireEqual(contract.rootName, RootName, "rootName", errors);
        Require(contract.expectedApartmentWindowCount == ExpectedWindows,
            $"expectedApartmentWindowCount must be {ExpectedWindows}", errors);
        RequireNear(contract.geometry.buildingWidthM, BuildingXMax - BuildingXMin, 0.0001f, "buildingWidthM", errors);
        RequireNear(contract.geometry.buildingHeightM, BuildingYMax - BuildingYMin, 0.0001f, "buildingHeightM", errors);
        RequireNear(contract.geometry.buildingDepthM, BuildingZFront - BuildingZBack, 0.0001f, "buildingDepthM", errors);
        RequireNear(contract.geometry.frontFacadePlaneZ, BuildingZFront, 0.0001f, "frontFacadePlaneZ", errors);
        RequireNear(contract.geometry.frontShellDepthM, ShellDepth, 0.0001f, "frontShellDepthM", errors);
        RequireNear(contract.geometry.bayPitchM, BayPitch, 0.0001f, "bayPitchM", errors);
        RequireNear(contract.geometry.floorPitchM, FloorPitch, 0.0001f, "floorPitchM", errors);
        RequireNear(contract.geometry.roughOpeningWidthM, OpeningWidth, 0.0001f, "roughOpeningWidthM", errors);
        RequireNear(contract.geometry.roughOpeningHeightM, OpeningHeight, 0.0001f, "roughOpeningHeightM", errors);
        RequireNear(contract.geometry.frameOuterWidthM, FrameOuterWidth, 0.0001f, "frameOuterWidthM", errors);
        RequireNear(contract.geometry.frameOuterHeightM, FrameOuterHeight, 0.0001f, "frameOuterHeightM", errors);
        RequireNear(contract.geometry.perimeterClearanceEachSideM, (OpeningWidth - FrameOuterWidth) * 0.5f,
            0.0001f, "perimeterClearanceEachSideM", errors);
        RequireNear(contract.geometry.perimeterSealVisibleWidthM, SealWidth, 0.0001f, "perimeterSealVisibleWidthM", errors);
        RequireNear(contract.geometry.perimeterSealDepthM, SealDepth, 0.0001f, "perimeterSealDepthM", errors);
        Require(contract.geometry.mainBlockRendererMustBeDisabled, "mainBlockRendererMustBeDisabled must be true", errors);
        Require(contract.geometry.glassMustRemainBehindFacadeFrontPlane, "glassMustRemainBehindFacadeFrontPlane must be true", errors);
        Require(contract.geometry.glassMustRemainAheadOfFacadeShellBackPlane, "glassMustRemainAheadOfFacadeShellBackPlane must be true", errors);
        Require(contract.qa.requiredFacadeCellPartCount == ExpectedFacadeCellParts,
            $"requiredFacadeCellPartCount must be {ExpectedFacadeCellParts}", errors);
        Require(contract.qa.requiredPerimeterSealPartCount == ExpectedSealParts,
            $"requiredPerimeterSealPartCount must be {ExpectedSealParts}", errors);
        Require(contract.qa.requiredGlassPaneCount == ExpectedApartmentGlassPanes,
            $"requiredGlassPaneCount must be {ExpectedApartmentGlassPanes}", errors);
        RequireNear(contract.qa.clearCoreInsetFromRoughOpeningEdgeM, ClearCoreInset, 0.0001f,
            "clearCoreInsetFromRoughOpeningEdgeM", errors);
        Require(contract.qa.automaticVisualPoints == 0, "automaticVisualPoints must remain 0", errors);
        Require(contract.qa.renderVerificationPending, "renderVerificationPending must remain true until real evidence exists", errors);

        foreach (string metadata in new[]
        {
            contract.manufacture, contract.dimensionsThickness, contract.materialsFinish,
            contract.mounting, contract.interfacesGapsSeals, contract.orientationExposure,
            contract.aging, contract.geometryVsMaterial, contract.lodPolicy
        })
            Require(!string.IsNullOrWhiteSpace(metadata), "mandatory manufacture/installation metadata field is empty", errors);

        if (errors.Count > 0)
            throw new InvalidOperationException("Facade aperture installation contract FAILED:\n - " + string.Join("\n - ", errors));

        Debug.Log("Facade aperture installation contract valid. This is non-scoring implementation metadata only.");
    }

    [MenuItem("NewTown/QA/Validate Reconstructed Facade Apertures")]
    public static void ValidateOpenScene()
    {
        EnsureSceneOpen();
        ValidateContractConfigOnly();

        if (GeneratedDanchiIsReplacedByAuthoredArt())
        {
            Debug.Log("Danchi authored replacement is active; generated facade aperture scene QA is not applicable.");
            return;
        }

        GameObject danchi = FindSceneObject("Danchi");
        GameObject mainBlock = FindSceneObject(MainBlockName);
        GameObject root = FindSceneObject(RootName);
        if (danchi == null || mainBlock == null || root == null)
            throw new InvalidOperationException("Danchi/MainBlock/facade aperture root missing from prepared scene.");

        Renderer mainRenderer = mainBlock.GetComponent<Renderer>();
        if (mainRenderer == null || mainRenderer.enabled)
            throw new InvalidOperationException(
                "MainBlock renderer must be disabled; otherwise the original opaque cube fills the reconstructed window apertures.");

        Renderer[] facadeCells = root.GetComponentsInChildren<Renderer>(true)
            .Where(x => x.gameObject.name.StartsWith("FA_FacadeCell_", StringComparison.Ordinal))
            .ToArray();
        Renderer[] seals = root.GetComponentsInChildren<Renderer>(true)
            .Where(x => x.gameObject.name.StartsWith("FA_WindowSeal_", StringComparison.Ordinal))
            .ToArray();
        if (facadeCells.Length != ExpectedFacadeCellParts)
            throw new InvalidOperationException($"Expected {ExpectedFacadeCellParts} facade cell wall parts, found {facadeCells.Length}.");
        if (seals.Length != ExpectedSealParts)
            throw new InvalidOperationException($"Expected {ExpectedSealParts} perimeter seal parts, found {seals.Length}.");

        int paneCount = Resources.FindObjectsOfTypeAll<GameObject>()
            .Count(x => x.scene.IsValid() && x.name.StartsWith("FO_Glass_", StringComparison.Ordinal));
        if (paneCount != ExpectedApartmentGlassPanes)
            throw new InvalidOperationException($"Expected {ExpectedApartmentGlassPanes} apartment glass panes, found {paneCount}.");

        Material facade = AssetDatabase.LoadAssetAtPath<Material>(FacadeMaterialPath);
        Material seal = AssetDatabase.LoadAssetAtPath<Material>(SealMaterialPath);
        ValidateDielectricMaterial(facade, "painted RC", 0.001f);
        ValidateDielectricMaterial(seal, "window perimeter seal", 0.001f);
        if (facade == null || !facade.IsKeywordEnabled("_NORMALMAP") || !facade.IsKeywordEnabled("_DETAIL_MULX2"))
            throw new InvalidOperationException("Reconstructed facade shell must retain the two-scale RC normal response.");

        int checkedOpenings = 0;
        int checkedPanes = 0;
        for (int floor = 0; floor < 5; floor++)
        {
            float y = FirstFloorY + floor * FloorPitch;
            for (int bay = 0; bay < 6; bay++)
            {
                float x = FirstBayX + bay * BayPitch;
                Bounds clearCore = new Bounds(
                    danchi.transform.TransformPoint(new Vector3(x, y, ShellCenterZ)),
                    new Vector3(OpeningWidth - ClearCoreInset * 2f, OpeningHeight - ClearCoreInset * 2f,
                        ShellDepth - 0.02f));

                foreach (Renderer wall in facadeCells)
                {
                    if (wall.enabled && wall.gameObject.activeInHierarchy && wall.bounds.Intersects(clearCore))
                        throw new InvalidOperationException(
                            $"Opaque facade wall intersects apartment rough-opening clear core at floor={floor}, bay={bay}: {wall.gameObject.name}");
                }

                string sealPrefix = $"FA_WindowSeal_{floor}_{bay}_";
                int openingSealCount = seals.Count(x => x.gameObject.name.StartsWith(sealPrefix, StringComparison.Ordinal));
                if (openingSealCount != 4)
                    throw new InvalidOperationException(
                        $"Apartment opening floor={floor}, bay={bay} requires four perimeter seal parts, found {openingSealCount}.");

                foreach (string leaf in new[] { "L", "R" })
                {
                    GameObject pane = FindSceneObject($"FO_Glass_{floor}_{bay}_{leaf}");
                    Renderer paneRenderer = pane != null ? pane.GetComponent<Renderer>() : null;
                    if (pane == null || paneRenderer == null || !paneRenderer.enabled || !pane.activeInHierarchy)
                        throw new InvalidOperationException($"Apartment glass pane missing/disabled: FO_Glass_{floor}_{bay}_{leaf}");

                    float localZ = danchi.transform.InverseTransformPoint(pane.transform.position).z;
                    if (localZ >= BuildingZFront - 0.001f)
                        throw new InvalidOperationException(
                            $"Glass pane {pane.name} is not recessed behind facade front plane: localZ={localZ:F4}");
                    if (localZ <= ShellBackZ + 0.001f)
                        throw new InvalidOperationException(
                            $"Glass pane {pane.name} is behind the reconstructed facade shell: localZ={localZ:F4}");
                    checkedPanes++;
                }

                checkedOpenings++;
            }
        }

        WriteRuntimeReport(checkedOpenings, checkedPanes, facadeCells.Length, seals.Length);
        AssetDatabase.Refresh();

        Debug.Log(
            $"Facade aperture construction QA passed in Unity scene state: openings={checkedOpenings}, panes={checkedPanes}, " +
            $"facadeCellParts={facadeCells.Length}, seals={seals.Length}. Visual Fidelity remains UNSCORED pending native 4K review.");
    }

    private static void BuildOuterShell(Transform parent, Material facade)
    {
        float width = BuildingXMax - BuildingXMin;
        float height = BuildingYMax - BuildingYMin;
        float depth = BuildingZFront - BuildingZBack;
        float cx = (BuildingXMin + BuildingXMax) * 0.5f;
        float cy = (BuildingYMin + BuildingYMax) * 0.5f;
        float cz = (BuildingZFront + BuildingZBack) * 0.5f;
        const float perimeterThickness = 0.18f;

        AddPart("FA_Shell_Back", parent,
            new Vector3(cx, cy, BuildingZBack + perimeterThickness * 0.5f),
            new Vector3(width, height, perimeterThickness), facade);
        AddPart("FA_Shell_LeftEnd", parent,
            new Vector3(BuildingXMin + perimeterThickness * 0.5f, cy, cz),
            new Vector3(perimeterThickness, height, depth), facade);
        AddPart("FA_Shell_RightEnd", parent,
            new Vector3(BuildingXMax - perimeterThickness * 0.5f, cy, cz),
            new Vector3(perimeterThickness, height, depth), facade);
        AddPart("FA_Shell_Roof", parent,
            new Vector3(cx, BuildingYMax - perimeterThickness * 0.5f, cz),
            new Vector3(width, perimeterThickness, depth), facade);
        AddPart("FA_Shell_Base", parent,
            new Vector3(cx, BuildingYMin + perimeterThickness * 0.5f, cz),
            new Vector3(width, perimeterThickness, depth), facade);
    }

    private static void BuildFrontFacadeAndSeals(Transform parent, Material facade, Material seal)
    {
        float sidePierWidth = (BayPitch - OpeningWidth) * 0.5f;
        float spandrelHeight = (FloorPitch - OpeningHeight) * 0.5f;
        float firstCellLeft = FirstBayX - BayPitch * 0.5f;
        float lastCellRight = FirstBayX + 5f * BayPitch + BayPitch * 0.5f;
        float firstCellBottom = FirstFloorY - FloorPitch * 0.5f;
        float lastCellTop = FirstFloorY + 4f * FloorPitch + FloorPitch * 0.5f;

        if (firstCellBottom > BuildingYMin)
            AddPart("FA_FrontBottomInfill", parent,
                new Vector3((BuildingXMin + BuildingXMax) * 0.5f,
                    (BuildingYMin + firstCellBottom) * 0.5f, ShellCenterZ),
                new Vector3(BuildingXMax - BuildingXMin, firstCellBottom - BuildingYMin, ShellDepth), facade);
        if (lastCellTop < BuildingYMax)
            AddPart("FA_FrontTopInfill", parent,
                new Vector3((BuildingXMin + BuildingXMax) * 0.5f,
                    (lastCellTop + BuildingYMax) * 0.5f, ShellCenterZ),
                new Vector3(BuildingXMax - BuildingXMin, BuildingYMax - lastCellTop, ShellDepth), facade);
        if (firstCellLeft > BuildingXMin)
            AddPart("FA_FrontLeftInfill", parent,
                new Vector3((BuildingXMin + firstCellLeft) * 0.5f,
                    (firstCellBottom + lastCellTop) * 0.5f, ShellCenterZ),
                new Vector3(firstCellLeft - BuildingXMin, lastCellTop - firstCellBottom, ShellDepth), facade);
        if (lastCellRight < BuildingXMax)
            AddPart("FA_FrontRightInfill", parent,
                new Vector3((lastCellRight + BuildingXMax) * 0.5f,
                    (firstCellBottom + lastCellTop) * 0.5f, ShellCenterZ),
                new Vector3(BuildingXMax - lastCellRight, lastCellTop - firstCellBottom, ShellDepth), facade);

        float sealGapCenter = (OpeningWidth - FrameOuterWidth) * 0.25f;
        float sealZ = BuildingZFront - SealDepth * 0.25f;

        for (int floor = 0; floor < 5; floor++)
        {
            float y = FirstFloorY + floor * FloorPitch;
            for (int bay = 0; bay < 6; bay++)
            {
                float x = FirstBayX + bay * BayPitch;
                string prefix = $"FA_FacadeCell_{floor}_{bay}_";

                AddPart(prefix + "LeftPier", parent,
                    new Vector3(x - OpeningWidth * 0.5f - sidePierWidth * 0.5f, y, ShellCenterZ),
                    new Vector3(sidePierWidth, FloorPitch, ShellDepth), facade);
                AddPart(prefix + "RightPier", parent,
                    new Vector3(x + OpeningWidth * 0.5f + sidePierWidth * 0.5f, y, ShellCenterZ),
                    new Vector3(sidePierWidth, FloorPitch, ShellDepth), facade);
                AddPart(prefix + "TopSpandrel", parent,
                    new Vector3(x, y + OpeningHeight * 0.5f + spandrelHeight * 0.5f, ShellCenterZ),
                    new Vector3(OpeningWidth, spandrelHeight, ShellDepth), facade);
                AddPart(prefix + "BottomSpandrel", parent,
                    new Vector3(x, y - OpeningHeight * 0.5f - spandrelHeight * 0.5f, ShellCenterZ),
                    new Vector3(OpeningWidth, spandrelHeight, ShellDepth), facade);

                float verticalSealX = FrameOuterWidth * 0.5f + sealGapCenter;
                float horizontalSealY = FrameOuterHeight * 0.5f + sealGapCenter;
                string sealPrefix = $"FA_WindowSeal_{floor}_{bay}_";
                AddPart(sealPrefix + "L", parent,
                    new Vector3(x - verticalSealX, y, sealZ),
                    new Vector3(SealWidth, FrameOuterHeight, SealDepth), seal);
                AddPart(sealPrefix + "R", parent,
                    new Vector3(x + verticalSealX, y, sealZ),
                    new Vector3(SealWidth, FrameOuterHeight, SealDepth), seal);
                AddPart(sealPrefix + "T", parent,
                    new Vector3(x, y + horizontalSealY, sealZ),
                    new Vector3(FrameOuterWidth, SealWidth, SealDepth), seal);
                AddPart(sealPrefix + "B", parent,
                    new Vector3(x, y - horizontalSealY, sealZ),
                    new Vector3(FrameOuterWidth, SealWidth, SealDepth), seal);
            }
        }
    }

    private static GameObject AddPart(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material)
    {
        if (size.x <= 0f || size.y <= 0f || size.z <= 0f)
            throw new InvalidOperationException($"Facade aperture part has non-positive size: {name} {size}");

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = QualityBlockDetailMeshLibrary.GetChamferedBox(size);
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
        return go;
    }

    private static void ValidateDielectricMaterial(Material material, string label, float metallicMax)
    {
        if (material == null)
            throw new InvalidOperationException($"{label} material is missing.");
        if (material.shader == null)
            throw new InvalidOperationException($"{label} material has no shader.");
        if (material.HasProperty("_Metallic") && material.GetFloat("_Metallic") > metallicMax)
            throw new InvalidOperationException(
                $"{label} material became materially impossible for this contract: metallic={material.GetFloat("_Metallic"):F4} > {metallicMax:F4}");
    }

    private static void WriteRuntimeReport(int openings, int panes, int wallParts, int sealParts)
    {
        var report = new RuntimeReport
        {
            generatedUtc = DateTime.UtcNow.ToString("O"),
            scenePath = ScenePath,
            rootName = RootName,
            verifiedInUnityScene = true,
            mainBlockRendererDisabled = true,
            verifiedOpeningCount = openings,
            verifiedApartmentGlassPaneCount = panes,
            verifiedFacadeCellPartCount = wallParts,
            verifiedPerimeterSealPartCount = sealParts,
            frontFacadePlaneZ = BuildingZFront,
            shellBackPlaneZ = ShellBackZ,
            automaticVisualPoints = 0,
            visualFidelityStatus = "UNSCORED_PENDING_NATIVE_4K_PIXEL_REVIEW"
        };
        File.WriteAllText(AbsolutePath(RuntimeReportPath), JsonUtility.ToJson(report, true) + Environment.NewLine);
    }

    private static bool GeneratedDanchiIsReplacedByAuthoredArt()
    {
        QualityBlockArtSlot slot = Resources.FindObjectsOfTypeAll<QualityBlockArtSlot>()
            .FirstOrDefault(x => x != null && x.gameObject.scene.IsValid() && x.SlotId == "danchi.main");
        return slot != null && slot.IsUsingAuthoredArt;
    }

    private static void EnsureSceneOpen()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() ||
            EditorSceneManager.GetActiveScene().path != ScenePath)
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
        if (string.IsNullOrWhiteSpace(projectRoot))
            throw new InvalidOperationException("Could not resolve Unity project root for facade aperture QA.");
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private static void Require(bool condition, string message, List<string> errors)
    {
        if (!condition) errors.Add(message);
    }

    private static void RequireEqual(string actual, string expected, string label, List<string> errors)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
            errors.Add($"{label} must be '{expected}', got '{actual}'.");
    }

    private static void RequireNear(float actual, float expected, float tolerance, string label, List<string> errors)
    {
        if (Mathf.Abs(actual - expected) > tolerance)
            errors.Add($"{label} must be {expected:F4}, got {actual:F4}.");
    }

    [Serializable]
    private sealed class Contract
    {
        public string scenePath;
        public string rootName;
        public int expectedApartmentWindowCount;
        public Geometry geometry;
        public string manufacture;
        public string dimensionsThickness;
        public string materialsFinish;
        public string mounting;
        public string interfacesGapsSeals;
        public string orientationExposure;
        public string aging;
        public string geometryVsMaterial;
        public string lodPolicy;
        public QA qa;
    }

    [Serializable]
    private sealed class Geometry
    {
        public float buildingWidthM;
        public float buildingHeightM;
        public float buildingDepthM;
        public float frontFacadePlaneZ;
        public float frontShellDepthM;
        public float bayPitchM;
        public float floorPitchM;
        public float roughOpeningWidthM;
        public float roughOpeningHeightM;
        public float frameOuterWidthM;
        public float frameOuterHeightM;
        public float perimeterClearanceEachSideM;
        public float perimeterSealVisibleWidthM;
        public float perimeterSealDepthM;
        public bool glassMustRemainBehindFacadeFrontPlane;
        public bool glassMustRemainAheadOfFacadeShellBackPlane;
        public bool mainBlockRendererMustBeDisabled;
    }

    [Serializable]
    private sealed class QA
    {
        public int requiredFacadeCellPartCount;
        public int requiredPerimeterSealPartCount;
        public int requiredGlassPaneCount;
        public float clearCoreInsetFromRoughOpeningEdgeM;
        public int automaticVisualPoints;
        public bool renderVerificationPending;
    }

    [Serializable]
    private sealed class RuntimeReport
    {
        public string generatedUtc;
        public string scenePath;
        public string rootName;
        public bool verifiedInUnityScene;
        public bool mainBlockRendererDisabled;
        public int verifiedOpeningCount;
        public int verifiedApartmentGlassPaneCount;
        public int verifiedFacadeCellPartCount;
        public int verifiedPerimeterSealPartCount;
        public float frontFacadePlaneZ;
        public float shellBackPlaneZ;
        public int automaticVisualPoints;
        public string visualFidelityStatus;
    }
}
