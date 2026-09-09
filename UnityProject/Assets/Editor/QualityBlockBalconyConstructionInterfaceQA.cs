using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Corrects and verifies the generated fallback balcony slab/railing attachment stack.
/// The original detail pass placed the fascia, rail base plates, anchor heads and lower rail
/// partly inside or above/below the wrong slab plane, and assigned the slab fascia an aluminum
/// material. This pass derives the interface from the persisted balcony-floor top plane, seats
/// manufactured hardware on that plane, and restores the fascia to the concrete material.
///
/// Source/scene QA only: a successful pass awards zero Visual Fidelity points and cannot clear
/// floating/interpenetrating geometry or material critical defects without native 4K evidence.
/// </summary>
public static class QualityBlockBalconyConstructionInterfaceQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/balcony_construction_interface_contract.json";
    private const string RuntimeReportPath = "Assets/QA/balcony_construction_interface_runtime_report.json";
    private const string DetailRootName = "DanchiHighDetail";
    private const int ExpectedBayCount = 30;

    [MenuItem("NewTown/QA/Validate Balcony Construction Interface Contract")]
    public static void ValidateContractConfigOnly()
    {
        InterfaceContract contract = LoadContract();
        List<string> errors = ValidateContract(contract);
        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Balcony construction-interface contract FAILED:\n - " + string.Join("\n - ", errors));

        Debug.Log(
            "Balcony construction-interface contract valid. This protects physical installation intent only; " +
            "Visual Fidelity remains UNSCORED until native 4K pixels are reviewed.");
    }

    [MenuItem("NewTown/Geometry/Correct Balcony Slab + Rail Interfaces")]
    public static void ApplyAndPersist()
    {
        EnsureScene();
        InterfaceContract contract = LoadAndValidateContract();

        if (IsAuthoredDanchiActive())
        {
            Debug.Log(
                "Authored danchi replacement is active; generated balcony-interface correction is not applicable. " +
                "The authored assembly still requires the native 4K Visual Fidelity gate.");
            return;
        }

        GameObject detailRoot = FindSceneObject(DetailRootName);
        if (detailRoot == null)
            throw new InvalidOperationException("DanchiHighDetail is missing; generated balcony interfaces cannot be corrected.");

        Transform[] bays = FindBayAssemblies(detailRoot);
        if (bays.Length != ExpectedBayCount)
            throw new InvalidOperationException($"Expected {ExpectedBayCount} HD_BayAssembly roots, got {bays.Length}.");

        int correctedParts = 0;
        foreach (Transform bay in bays)
        {
            ParseBayIdentity(bay.name, out int floor, out int bayIndex);
            GameObject floorObject = FindSceneObject($"BalconyFloor_{floor}_{bayIndex}");
            Renderer floorRenderer = RequireRenderer(floorObject, $"BalconyFloor_{floor}_{bayIndex}");
            float floorTopLocalY = ResolveFloorTopInBayLocalSpace(bay, floorRenderer);

            if (Mathf.Abs(floorTopLocalY - contract.dimensionsMetres.expectedFloorTopLocalY) >
                contract.dimensionsMetres.floorTopTolerance)
                throw new InvalidOperationException(
                    $"{bay.name} balcony floor top drifted to {floorTopLocalY:0.####} m; " +
                    $"locked generated-fallback expectation is {contract.dimensionsMetres.expectedFloorTopLocalY:0.####} ± " +
                    $"{contract.dimensionsMetres.floorTopTolerance:0.####} m. Reconstruct the assembly instead of silently adapting.");

            Transform fascia = RequireDirectChild(bay, "HD_BalconySlabLip");
            SetLocalY(fascia, floorTopLocalY - contract.dimensionsMetres.fasciaHeight * 0.5f);
            RequireRenderer(fascia.gameObject, fascia.name).sharedMaterial = floorRenderer.sharedMaterial;
            correctedParts++;

            for (int r = -3; r <= 3; r++)
            {
                Transform plate = RequireDirectChild(bay, $"HD_RailBasePlate_{r}");
                SetLocalY(plate, floorTopLocalY + contract.dimensionsMetres.basePlateHeight * 0.5f);
                correctedParts++;

                float plateTop = floorTopLocalY + contract.dimensionsMetres.basePlateHeight;
                float boltCenter = plateTop + contract.dimensionsMetres.boltHeight * 0.5f -
                                   contract.dimensionsMetres.boltEmbedTarget;
                Transform boltA = RequireDirectChild(bay, $"HD_RailBolt_{r}_A");
                Transform boltB = RequireDirectChild(bay, $"HD_RailBolt_{r}_B");
                SetLocalY(boltA, boltCenter);
                SetLocalY(boltB, boltCenter);
                correctedParts += 2;
            }

            Transform lowerRail = RequireDirectChild(bay, "HD_RailLower");
            SetLocalY(lowerRail,
                floorTopLocalY + contract.dimensionsMetres.lowerRailBottomClearance +
                contract.dimensionsMetres.lowerRailHeight * 0.5f);
            correctedParts++;

            Transform dividerLow = RequireDirectChild(bay, "HD_DividerBracketLow");
            SetLocalY(dividerLow,
                floorTopLocalY + contract.dimensionsMetres.dividerBracketLowHeight * 0.5f);
            correctedParts++;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        ValidationSummary summary = ValidateOpenSceneInternal(contract);
        WriteRuntimeReport(summary, correctedParts);
        Debug.Log(
            $"Balcony construction interfaces corrected and persisted: bays={summary.bayCount}, partsAdjusted={correctedParts}. " +
            "Fascia now inherits balcony concrete; plates/brackets seat on the slab top and lower rails remain above it. " +
            "Native 4K review is still required before any visual points or critical-defect clearance.");
    }

    [MenuItem("NewTown/QA/Validate Balcony Slab + Rail Interfaces")]
    public static void ValidateOpenScene()
    {
        EnsureScene();
        InterfaceContract contract = LoadAndValidateContract();
        if (IsAuthoredDanchiActive())
        {
            Debug.Log(
                "Authored danchi replacement is active; generated balcony-interface QA is not applicable. " +
                "No Visual Fidelity points are awarded by this skip.");
            return;
        }

        ValidationSummary summary = ValidateOpenSceneInternal(contract);
        WriteRuntimeReport(summary, 0);
        Debug.Log(
            $"Balcony construction-interface QA passed: bays={summary.bayCount}, plates={summary.plateCount}, " +
            $"bolts={summary.boltCount}, maxContactError={summary.maxContactErrorMetres * 1000f:0.##} mm. " +
            "Rendered contact shadows, interpenetration and material response remain unverified until native 4K evidence exists.");
    }

    private static ValidationSummary ValidateOpenSceneInternal(InterfaceContract contract)
    {
        GameObject detailRoot = FindSceneObject(DetailRootName);
        if (detailRoot == null)
            throw new InvalidOperationException("DanchiHighDetail is missing; generated balcony interfaces cannot be verified.");

        Transform[] bays = FindBayAssemblies(detailRoot);
        if (bays.Length != ExpectedBayCount)
            throw new InvalidOperationException($"Expected {ExpectedBayCount} HD_BayAssembly roots, got {bays.Length}.");

        var errors = new List<string>();
        var summary = new ValidationSummary { bayCount = bays.Length };

        foreach (Transform bay in bays)
        {
            ParseBayIdentity(bay.name, out int floor, out int bayIndex);
            GameObject floorObject = FindSceneObject($"BalconyFloor_{floor}_{bayIndex}");
            Renderer floorRenderer = RequireRenderer(floorObject, $"BalconyFloor_{floor}_{bayIndex}");
            float floorTop = floorRenderer.bounds.max.y;
            float floorTopLocalY = ResolveFloorTopInBayLocalSpace(bay, floorRenderer);

            if (Mathf.Abs(floorTopLocalY - contract.dimensionsMetres.expectedFloorTopLocalY) >
                contract.dimensionsMetres.floorTopTolerance)
                errors.Add($"{bay.name}: floor top local Y {floorTopLocalY:0.####} is outside the locked fallback tolerance.");

            Renderer fascia = RequireRenderer(RequireDirectChild(bay, "HD_BalconySlabLip").gameObject, "HD_BalconySlabLip");
            TrackContactError(summary, Mathf.Abs(fascia.bounds.max.y - floorTop));
            if (Mathf.Abs(fascia.bounds.max.y - floorTop) > contract.dimensionsMetres.contactTolerance)
                errors.Add($"{bay.name}: concrete fascia top is not flush to balcony floor top.");
            if (fascia.sharedMaterial == null || floorRenderer.sharedMaterial == null ||
                fascia.sharedMaterial != floorRenderer.sharedMaterial)
                errors.Add($"{bay.name}: slab fascia does not inherit the balcony-floor concrete material.");

            Renderer lowerRail = RequireRenderer(RequireDirectChild(bay, "HD_RailLower").gameObject, "HD_RailLower");
            float lowerClearance = lowerRail.bounds.min.y - floorTop;
            summary.minLowerRailClearanceMetres = summary.railClearanceInitialized
                ? Mathf.Min(summary.minLowerRailClearanceMetres, lowerClearance)
                : lowerClearance;
            summary.maxLowerRailClearanceMetres = summary.railClearanceInitialized
                ? Mathf.Max(summary.maxLowerRailClearanceMetres, lowerClearance)
                : lowerClearance;
            summary.railClearanceInitialized = true;
            if (lowerClearance < contract.dimensionsMetres.lowerRailClearanceMin ||
                lowerClearance > contract.dimensionsMetres.lowerRailClearanceMax)
                errors.Add($"{bay.name}: lower rail clearance is {lowerClearance:0.####} m, outside allowed range.");

            Renderer dividerLow = RequireRenderer(RequireDirectChild(bay, "HD_DividerBracketLow").gameObject, "HD_DividerBracketLow");
            TrackContactError(summary, Mathf.Abs(dividerLow.bounds.min.y - floorTop));
            if (Mathf.Abs(dividerLow.bounds.min.y - floorTop) > contract.dimensionsMetres.contactTolerance)
                errors.Add($"{bay.name}: low divider bracket is not seated on the slab top.");

            for (int r = -3; r <= 3; r++)
            {
                Renderer plate = RequireRenderer(RequireDirectChild(bay, $"HD_RailBasePlate_{r}").gameObject,
                    $"HD_RailBasePlate_{r}");
                summary.plateCount++;
                float plateContact = Mathf.Abs(plate.bounds.min.y - floorTop);
                TrackContactError(summary, plateContact);
                if (plateContact > contract.dimensionsMetres.contactTolerance)
                    errors.Add($"{bay.name}/plate[{r}]: base plate is not seated on the slab top.");

                GameObject postObject = FindSceneObject($"Rail_{floor}_{bayIndex}_{r}");
                Renderer post = RequireRenderer(postObject, $"Rail_{floor}_{bayIndex}_{r}");
                if (post.bounds.center.x < plate.bounds.min.x - contract.dimensionsMetres.contactTolerance ||
                    post.bounds.center.x > plate.bounds.max.x + contract.dimensionsMetres.contactTolerance ||
                    post.bounds.center.z < plate.bounds.min.z - contract.dimensionsMetres.contactTolerance ||
                    post.bounds.center.z > plate.bounds.max.z + contract.dimensionsMetres.contactTolerance)
                    errors.Add($"{bay.name}/plate[{r}]: base plate does not laterally contain its rail-post centerline.");
                if (post.bounds.min.y >= floorTop - contract.dimensionsMetres.contactTolerance ||
                    post.bounds.max.y <= plate.bounds.max.y + 0.05f)
                    errors.Add($"{bay.name}/post[{r}]: rail post no longer has a plausible anchored-through attachment relationship.");

                ValidateBolt(bay, $"HD_RailBolt_{r}_A", plate, contract, summary, errors);
                ValidateBolt(bay, $"HD_RailBolt_{r}_B", plate, contract, summary, errors);
            }
        }

        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Balcony slab/rail construction-interface QA FAILED:\n - " + string.Join("\n - ", errors.Take(80)) +
                (errors.Count > 80 ? $"\n - ... {errors.Count - 80} additional errors" : string.Empty));

        return summary;
    }

    private static void ValidateBolt(Transform bay, string name, Renderer plate, InterfaceContract contract,
        ValidationSummary summary, List<string> errors)
    {
        Renderer bolt = RequireRenderer(RequireDirectChild(bay, name).gameObject, name);
        summary.boltCount++;
        float embed = plate.bounds.max.y - bolt.bounds.min.y;
        summary.minBoltEmbedMetres = summary.boltEmbedInitialized ? Mathf.Min(summary.minBoltEmbedMetres, embed) : embed;
        summary.maxBoltEmbedMetres = summary.boltEmbedInitialized ? Mathf.Max(summary.maxBoltEmbedMetres, embed) : embed;
        summary.boltEmbedInitialized = true;
        if (embed < contract.dimensionsMetres.boltEmbedMin || embed > contract.dimensionsMetres.boltEmbedMax)
            errors.Add($"{bay.name}/{name}: bolt-to-plate embed is {embed:0.####} m, outside controlled range.");
        if (bolt.bounds.max.y <= plate.bounds.max.y)
            errors.Add($"{bay.name}/{name}: bolt head is fully buried in the base plate.");
    }

    private static void TrackContactError(ValidationSummary summary, float value)
    {
        summary.maxContactErrorMetres = Mathf.Max(summary.maxContactErrorMetres, value);
    }

    private static Transform[] FindBayAssemblies(GameObject detailRoot)
    {
        return detailRoot.GetComponentsInChildren<Transform>(true)
            .Where(x => x != detailRoot.transform && x.name.StartsWith("HD_BayAssembly_", StringComparison.Ordinal))
            .OrderBy(x => x.name, StringComparer.Ordinal)
            .ToArray();
    }

    private static float ResolveFloorTopInBayLocalSpace(Transform bay, Renderer floorRenderer)
    {
        Vector3 worldPoint = new Vector3(bay.position.x, floorRenderer.bounds.max.y, bay.position.z);
        return bay.InverseTransformPoint(worldPoint).y;
    }

    private static void ParseBayIdentity(string name, out int floor, out int bay)
    {
        string[] parts = name.Split('_');
        if (parts.Length != 4 || parts[0] != "HD" || parts[1] != "BayAssembly" ||
            !int.TryParse(parts[2], out floor) || !int.TryParse(parts[3], out bay) ||
            floor < 0 || floor > 4 || bay < 0 || bay > 5)
            throw new InvalidOperationException($"Unexpected detailed bay assembly name: {name}");
    }

    private static void SetLocalY(Transform transform, float y)
    {
        Vector3 p = transform.localPosition;
        p.y = y;
        transform.localPosition = p;
        EditorUtility.SetDirty(transform);
    }

    private static Transform RequireDirectChild(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name) return child;
        }
        throw new InvalidOperationException($"Required child missing under {parent.name}: {name}");
    }

    private static Renderer RequireRenderer(GameObject go, string label)
    {
        if (go == null) throw new InvalidOperationException($"Required object missing: {label}");
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer == null || !renderer.enabled || !go.activeInHierarchy)
            throw new InvalidOperationException($"Required active renderer missing: {label}");
        return renderer;
    }

    private static bool IsAuthoredDanchiActive()
    {
        return Resources.FindObjectsOfTypeAll<QualityBlockArtSlot>()
            .Any(x => x != null && x.gameObject.scene.IsValid() && x.gameObject.scene.path == ScenePath &&
                      x.SlotId == "danchi.main" && x.IsUsingAuthoredArt);
    }

    private static void EnsureScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x.scene.IsValid() && x.scene.path == ScenePath && x.name == name);
    }

    private static InterfaceContract LoadAndValidateContract()
    {
        InterfaceContract contract = LoadContract();
        List<string> errors = ValidateContract(contract);
        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Balcony construction-interface contract FAILED:\n - " + string.Join("\n - ", errors));
        return contract;
    }

    private static InterfaceContract LoadContract()
    {
        string absolute = AbsolutePath(ContractPath);
        if (!File.Exists(absolute))
            throw new FileNotFoundException($"Balcony construction-interface contract missing: {ContractPath}");
        InterfaceContract contract = JsonUtility.FromJson<InterfaceContract>(File.ReadAllText(absolute));
        if (contract == null)
            throw new InvalidOperationException($"Could not parse {ContractPath}");
        return contract;
    }

    private static List<string> ValidateContract(InterfaceContract contract)
    {
        var errors = new List<string>();
        if (contract == null)
        {
            errors.Add("Contract is null/unparseable.");
            return errors;
        }

        RequireText(contract.schemaVersion, "schemaVersion", errors);
        RequireText(contract.scenePath, "scenePath", errors);
        RequireText(contract.detailRootName, "detailRootName", errors);
        RequireText(contract.policy, "policy", errors);
        if (contract.scenePath != ScenePath) errors.Add($"scenePath must be {ScenePath}.");
        if (contract.detailRootName != DetailRootName) errors.Add($"detailRootName must be {DetailRootName}.");
        if (contract.expectedBayAssemblies != ExpectedBayCount)
            errors.Add($"expectedBayAssemblies must be {ExpectedBayCount}.");
        if (contract.automaticVisualPoints != 0)
            errors.Add("automaticVisualPoints must remain zero.");
        if (!contract.renderVerificationRequired)
            errors.Add("renderVerificationRequired must remain true.");

        RequireArray(contract.manufactureModel, 4, "manufactureModel", errors);
        RequireArray(contract.installationSequence, 5, "installationSequence", errors);
        RequireArray(contract.interfacesGapsSeals, 5, "interfacesGapsSeals", errors);
        RequireArray(contract.orientationExposureAging, 3, "orientationExposureAging", errors);
        RequireArray(contract.geometryVsMaterialDetail, 3, "geometryVsMaterialDetail", errors);
        RequireArray(contract.criticalDefectRelationship, 3, "criticalDefectRelationship", errors);

        Dimensions d = contract.dimensionsMetres;
        if (d == null) errors.Add("dimensionsMetres is required.");
        else
        {
            RequireNear(d.expectedFloorTopLocalY, -0.79f, 0.0001f, "expectedFloorTopLocalY", errors);
            RequireNear(d.fasciaHeight, 0.18f, 0.0001f, "fasciaHeight", errors);
            RequireNear(d.basePlateHeight, 0.025f, 0.0001f, "basePlateHeight", errors);
            RequireNear(d.boltHeight, 0.020f, 0.0001f, "boltHeight", errors);
            RequireNear(d.boltEmbedTarget, 0.003f, 0.0001f, "boltEmbedTarget", errors);
            RequireNear(d.lowerRailHeight, 0.040f, 0.0001f, "lowerRailHeight", errors);
            RequireNear(d.lowerRailBottomClearance, 0.070f, 0.0001f, "lowerRailBottomClearance", errors);
            RequireNear(d.dividerBracketLowHeight, 0.160f, 0.0001f, "dividerBracketLowHeight", errors);
            if (d.floorTopTolerance <= 0f || d.floorTopTolerance > 0.005f)
                errors.Add("floorTopTolerance must be >0 and <=5 mm.");
            if (d.contactTolerance <= 0f || d.contactTolerance > 0.005f)
                errors.Add("contactTolerance must be >0 and <=5 mm.");
            if (d.boltEmbedMin < 0.0005f || d.boltEmbedMax > 0.008f || d.boltEmbedMin >= d.boltEmbedMax)
                errors.Add("bolt embed range must remain a small positive 0.5-8 mm controlled interface.");
            if (d.boltEmbedTarget < d.boltEmbedMin || d.boltEmbedTarget > d.boltEmbedMax)
                errors.Add("boltEmbedTarget must remain inside the hard embed range.");
            if (d.lowerRailClearanceMin < 0.04f || d.lowerRailClearanceMax > 0.12f ||
                d.lowerRailClearanceMin >= d.lowerRailClearanceMax)
                errors.Add("lower-rail clearance range must remain physically visible and bounded within 40-120 mm.");
        }

        if (contract.lookdevBrief == null)
            errors.Add("lookdevBrief is required.");
        else
        {
            RequireText(contract.lookdevBrief.frontal, "lookdevBrief.frontal", errors);
            RequireText(contract.lookdevBrief.oblique, "lookdevBrief.oblique", errors);
            RequireText(contract.lookdevBrief.grazing, "lookdevBrief.grazing", errors);
            RequireText(contract.lookdevBrief.macro100Percent, "lookdevBrief.macro100Percent", errors);
        }

        if (contract.researchReferences == null || contract.researchReferences.Length < 2 ||
            contract.researchReferences.Any(x => x == null || string.IsNullOrWhiteSpace(x.title) ||
                                                 string.IsNullOrWhiteSpace(x.url) || string.IsNullOrWhiteSpace(x.relevance)))
            errors.Add("At least two complete researchReferences are required.");

        return errors;
    }

    private static void WriteRuntimeReport(ValidationSummary summary, int correctedParts)
    {
        var report = new RuntimeReport
        {
            schemaVersion = "1.0",
            generatedUtc = DateTime.UtcNow.ToString("O"),
            scenePath = ScenePath,
            bayCount = summary.bayCount,
            plateCount = summary.plateCount,
            boltCount = summary.boltCount,
            correctedPartsThisInvocation = correctedParts,
            maxContactErrorMetres = summary.maxContactErrorMetres,
            minBoltEmbedMetres = summary.minBoltEmbedMetres,
            maxBoltEmbedMetres = summary.maxBoltEmbedMetres,
            minLowerRailClearanceMetres = summary.minLowerRailClearanceMetres,
            maxLowerRailClearanceMetres = summary.maxLowerRailClearanceMetres,
            fasciaMatchesBalconyConcrete = true,
            visualFidelityPointsAwarded = 0,
            renderedVerificationPending = true,
            note = "Scene-source construction interfaces verified only. Native 3840x2160 frontal/oblique/grazing evidence is still required."
        };
        File.WriteAllText(AbsolutePath(RuntimeReportPath), JsonUtility.ToJson(report, true));
        AssetDatabase.Refresh();
    }

    private static string AbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrWhiteSpace(projectRoot))
            throw new InvalidOperationException("Could not resolve Unity project root.");
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private static void RequireText(string value, string field, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) errors.Add($"{field} is required.");
    }

    private static void RequireArray(string[] values, int minimum, string field, List<string> errors)
    {
        if (values == null || values.Length < minimum || values.Any(string.IsNullOrWhiteSpace))
            errors.Add($"{field} requires at least {minimum} non-empty entries.");
    }

    private static void RequireNear(float value, float expected, float tolerance, string field, List<string> errors)
    {
        if (Mathf.Abs(value - expected) > tolerance)
            errors.Add($"{field} must remain {expected:0.####}; got {value:0.####}.");
    }

    [Serializable]
    private sealed class InterfaceContract
    {
        public string schemaVersion;
        public string scenePath;
        public string detailRootName;
        public int expectedBayAssemblies;
        public string policy;
        public string[] manufactureModel;
        public Dimensions dimensionsMetres;
        public string[] installationSequence;
        public string[] interfacesGapsSeals;
        public MaterialsFinish materialsFinish;
        public string[] orientationExposureAging;
        public string[] geometryVsMaterialDetail;
        public LookdevBrief lookdevBrief;
        public ResearchReference[] researchReferences;
        public string[] criticalDefectRelationship;
        public int automaticVisualPoints;
        public bool renderVerificationRequired;
    }

    [Serializable]
    private sealed class Dimensions
    {
        public float expectedFloorTopLocalY;
        public float floorTopTolerance;
        public float fasciaHeight;
        public float basePlateHeight;
        public float boltHeight;
        public float boltEmbedTarget;
        public float boltEmbedMin;
        public float boltEmbedMax;
        public float lowerRailHeight;
        public float lowerRailBottomClearance;
        public float lowerRailClearanceMin;
        public float lowerRailClearanceMax;
        public float dividerBracketLowHeight;
        public float contactTolerance;
    }

    [Serializable]
    private sealed class MaterialsFinish
    {
        public string fascia;
        public string basePlateAndBolts;
        public string rails;
        public string weatheringRule;
    }

    [Serializable]
    private sealed class LookdevBrief
    {
        public string frontal;
        public string oblique;
        public string grazing;
        public string macro100Percent;
    }

    [Serializable]
    private sealed class ResearchReference
    {
        public string title;
        public string url;
        public string relevance;
    }

    private sealed class ValidationSummary
    {
        public int bayCount;
        public int plateCount;
        public int boltCount;
        public float maxContactErrorMetres;
        public bool boltEmbedInitialized;
        public float minBoltEmbedMetres;
        public float maxBoltEmbedMetres;
        public bool railClearanceInitialized;
        public float minLowerRailClearanceMetres;
        public float maxLowerRailClearanceMetres;
    }

    [Serializable]
    private sealed class RuntimeReport
    {
        public string schemaVersion;
        public string generatedUtc;
        public string scenePath;
        public int bayCount;
        public int plateCount;
        public int boltCount;
        public int correctedPartsThisInvocation;
        public float maxContactErrorMetres;
        public float minBoltEmbedMetres;
        public float maxBoltEmbedMetres;
        public float minLowerRailClearanceMetres;
        public float maxLowerRailClearanceMetres;
        public bool fasciaMatchesBalconyConcrete;
        public int visualFidelityPointsAwarded;
        public bool renderedVerificationPending;
        public string note;
    }
}
