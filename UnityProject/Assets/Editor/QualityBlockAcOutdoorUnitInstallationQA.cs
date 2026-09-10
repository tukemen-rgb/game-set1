using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Corrects and verifies the generated balcony outdoor-unit installation stack.
///
/// The fallback detail pass originally placed its 18 mm mount plates entirely below the balcony
/// walking-surface plane and let the 45 mm feet penetrate that plane, while the base condenser
/// casing sat only about 20 mm above it. This pass reconstructs the actual support sequence from
/// the persisted slab-top renderer: slab -> plate -> foot -> condenser chassis. The condenser is
/// raised only as required, casing-mounted fan/grille detail follows it, and service/drain lines are
/// rebuilt between moved unit-side outlets and fixed wall/floor interfaces.
///
/// Source/scene QA only. Passing this code cannot award Visual Fidelity points or clear a critical
/// floating/intersection/material defect without native 3840x2160 evidence and 100% crop review.
/// </summary>
public static class QualityBlockAcOutdoorUnitInstallationQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/ac_outdoor_unit_installation_contract.json";
    private const string RuntimeReportPath = "Assets/QA/ac_outdoor_unit_installation_runtime_report.json";
    private const string DetailRootName = "DanchiHighDetail";
    private const int ExpectedGeneratedUnits = 15;

    [MenuItem("NewTown/QA/Validate Outdoor AC Installation Contract")]
    public static void ValidateContractConfigOnly()
    {
        InstallationContract contract = LoadContract();
        List<string> errors = ValidateContract(contract);
        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Outdoor AC installation contract FAILED:\n - " + string.Join("\n - ", errors));

        Debug.Log(
            "Outdoor AC installation contract valid. Construction intent only; Visual Fidelity remains " +
            "UNSCORED until native 4K pixels and 100% crops are reviewed.");
    }

    [MenuItem("NewTown/Geometry/Correct Outdoor AC Installation Interfaces")]
    public static void ApplyAndPersist()
    {
        EnsureScene();
        InstallationContract contract = LoadAndValidateContract();

        if (IsAuthoredDanchiActive())
        {
            Debug.Log(
                "Authored danchi replacement is active; generated outdoor-unit correction is not applicable. " +
                "The authored assembly still requires the native 4K Visual Fidelity gate.");
            return;
        }

        GameObject detailRoot = FindSceneObject(DetailRootName);
        if (detailRoot == null)
            throw new InvalidOperationException("DanchiHighDetail is missing; outdoor-unit interfaces cannot be corrected.");

        Transform[] bays = FindGeneratedAcBays(detailRoot);
        if (bays.Length != ExpectedGeneratedUnits)
            throw new InvalidOperationException(
                $"Expected {ExpectedGeneratedUnits} generated outdoor-unit bays, got {bays.Length}.");

        int correctedParts = 0;
        float maxCasingRaise = 0f;

        foreach (Transform bay in bays)
        {
            ParseBayIdentity(bay.name, out int floor, out int bayIndex);
            Renderer floor = RequireRenderer(FindSceneObject($"BalconyFloor_{floor}_{bayIndex}"),
                $"BalconyFloor_{floor}_{bayIndex}");
            float floorTopLocalY = ResolveFloorTopInBayLocalSpace(bay, floor);
            if (Mathf.Abs(floorTopLocalY - contract.dimensionsMetres.expectedFloorTopLocalY) >
                contract.dimensionsMetres.floorTopTolerance)
                throw new InvalidOperationException(
                    $"{bay.name}: balcony floor top drifted to {floorTopLocalY:0.####} m; expected " +
                    $"{contract.dimensionsMetres.expectedFloorTopLocalY:0.####} ± " +
                    $"{contract.dimensionsMetres.floorTopTolerance:0.####} m. Reconstruct instead of adapting silently.");

            Renderer casing = RequireRenderer(FindSceneObject($"AC_{floor}_{bayIndex}"), $"AC_{floor}_{bayIndex}");
            float casingHeight = casing.bounds.size.y;
            if (Mathf.Abs(casingHeight - contract.dimensionsMetres.casingHeight) > 0.006f)
                throw new InvalidOperationException(
                    $"AC_{floor}_{bayIndex}: casing height {casingHeight:0.####} m no longer matches the locked " +
                    $"{contract.dimensionsMetres.casingHeight:0.####} m fallback assembly.");

            Renderer plateL = RequireChildRenderer(bay, "HD_AC_MountPlate_-1");
            Renderer plateR = RequireChildRenderer(bay, "HD_AC_MountPlate_1");
            MoveRendererMinYTo(plateL, floor.bounds.max.y);
            MoveRendererMinYTo(plateR, floor.bounds.max.y);
            correctedParts += 2;

            Renderer footL = RequireChildRenderer(bay, "HD_AC_Foot_-1");
            Renderer footR = RequireChildRenderer(bay, "HD_AC_Foot_1");
            MoveRendererMinYTo(footL, plateL.bounds.max.y);
            MoveRendererMinYTo(footR, plateR.bounds.max.y);
            correctedParts += 2;

            float supportTop = 0.5f * (footL.bounds.max.y + footR.bounds.max.y);
            float desiredCasingBottom = supportTop - contract.dimensionsMetres.casingFootEmbedTarget;
            float casingDeltaWorldY = desiredCasingBottom - casing.bounds.min.y;
            casing.transform.position += Vector3.up * casingDeltaWorldY;
            EditorUtility.SetDirty(casing.transform);
            correctedParts++;
            maxCasingRaise = Mathf.Max(maxCasingRaise, Mathf.Abs(casingDeltaWorldY));

            ShiftCasingMountedDetail(bay, casingDeltaWorldY, ref correctedParts);

            // Bolts remain tied to the fixed plate stack. Their heads must rise above the plate while
            // retaining a small, intentional embed that reads as tightened hardware rather than a floating cap.
            RepositionMountBolt(bay, "HD_AC_MountBolt_-1_-1", plateL,
                contract.dimensionsMetres.casingFootEmbedTarget * 0.5f, ref correctedParts);
            RepositionMountBolt(bay, "HD_AC_MountBolt_-1_1", plateL,
                contract.dimensionsMetres.casingFootEmbedTarget * 0.5f, ref correctedParts);
            RepositionMountBolt(bay, "HD_AC_MountBolt_1_-1", plateR,
                contract.dimensionsMetres.casingFootEmbedTarget * 0.5f, ref correctedParts);
            RepositionMountBolt(bay, "HD_AC_MountBolt_1_1", plateR,
                contract.dimensionsMetres.casingFootEmbedTarget * 0.5f, ref correctedParts);

            float localDeltaY = bay.InverseTransformVector(Vector3.up * casingDeltaWorldY).y;
            RebuildServiceLines(bay, localDeltaY, floorTopLocalY,
                contract.dimensionsMetres.drainOutletClearanceTarget, ref correctedParts);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        ValidationSummary summary = ValidateOpenSceneInternal(contract);
        summary.maxCasingRaiseMetres = maxCasingRaise;
        WriteRuntimeReport(summary, correctedParts);

        Debug.Log(
            $"Outdoor AC installation interfaces corrected and persisted: units={summary.unitCount}, " +
            $"partsAdjusted={correctedParts}, maxCasingRaise={maxCasingRaise * 1000f:0.##} mm. " +
            "Plates/feet are above the slab, chassis are supported by the feet, service-line joints are rebuilt, " +
            "and drain terminals remain above the walking surface. Native 4K review is still required.");
    }

    [MenuItem("NewTown/QA/Validate Outdoor AC Installation Interfaces")]
    public static void ValidateOpenScene()
    {
        EnsureScene();
        InstallationContract contract = LoadAndValidateContract();
        if (IsAuthoredDanchiActive())
        {
            Debug.Log(
                "Authored danchi replacement is active; generated outdoor-unit interface QA is not applicable. " +
                "No Visual Fidelity points are awarded by this skip.");
            return;
        }

        ValidationSummary summary = ValidateOpenSceneInternal(contract);
        WriteRuntimeReport(summary, 0);
        Debug.Log(
            $"Outdoor AC installation QA passed: units={summary.unitCount}, supports={summary.supportCount}, " +
            $"maxSupportContactError={summary.maxSupportContactErrorMetres * 1000f:0.##} mm, " +
            $"drainClearance={summary.minDrainClearanceMetres * 1000f:0.##}-" +
            $"{summary.maxDrainClearanceMetres * 1000f:0.##} mm. Rendered contact, pipe shading and " +
            "interpenetration remain unverified until native 4K evidence exists.");
    }

    private static ValidationSummary ValidateOpenSceneInternal(InstallationContract contract)
    {
        GameObject detailRoot = FindSceneObject(DetailRootName);
        if (detailRoot == null)
            throw new InvalidOperationException("DanchiHighDetail is missing; outdoor-unit interfaces cannot be verified.");

        Transform[] bays = FindGeneratedAcBays(detailRoot);
        if (bays.Length != ExpectedGeneratedUnits)
            throw new InvalidOperationException(
                $"Expected {ExpectedGeneratedUnits} generated outdoor-unit bays, got {bays.Length}.");

        var errors = new List<string>();
        var summary = new ValidationSummary { unitCount = bays.Length };

        foreach (Transform bay in bays)
        {
            ParseBayIdentity(bay.name, out int floor, out int bayIndex);
            Renderer floor = RequireRenderer(FindSceneObject($"BalconyFloor_{floor}_{bayIndex}"),
                $"BalconyFloor_{floor}_{bayIndex}");
            Renderer casing = RequireRenderer(FindSceneObject($"AC_{floor}_{bayIndex}"), $"AC_{floor}_{bayIndex}");
            float floorTopLocalY = ResolveFloorTopInBayLocalSpace(bay, floor);

            if (Mathf.Abs(floorTopLocalY - contract.dimensionsMetres.expectedFloorTopLocalY) >
                contract.dimensionsMetres.floorTopTolerance)
                errors.Add($"{bay.name}: floor top local Y {floorTopLocalY:0.####} is outside the locked fallback tolerance.");

            Renderer[] plates =
            {
                RequireChildRenderer(bay, "HD_AC_MountPlate_-1"),
                RequireChildRenderer(bay, "HD_AC_MountPlate_1")
            };
            Renderer[] feet =
            {
                RequireChildRenderer(bay, "HD_AC_Foot_-1"),
                RequireChildRenderer(bay, "HD_AC_Foot_1")
            };

            for (int i = 0; i < 2; i++)
            {
                summary.supportCount++;
                TrackMax(ref summary.maxSupportContactErrorMetres, Mathf.Abs(plates[i].bounds.min.y - floor.bounds.max.y));
                if (Mathf.Abs(plates[i].bounds.min.y - floor.bounds.max.y) >
                    contract.dimensionsMetres.supportContactTolerance)
                    errors.Add($"{bay.name}/plate[{i}]: mounting plate is not seated on the slab top.");

                TrackMax(ref summary.maxSupportContactErrorMetres, Mathf.Abs(feet[i].bounds.min.y - plates[i].bounds.max.y));
                if (Mathf.Abs(feet[i].bounds.min.y - plates[i].bounds.max.y) >
                    contract.dimensionsMetres.supportContactTolerance)
                    errors.Add($"{bay.name}/foot[{i}]: support foot is not seated on its mounting plate.");

                if (!ContainsXZ(casing.bounds, plates[i].bounds.center, 0.005f) ||
                    !ContainsXZ(casing.bounds, feet[i].bounds.center, 0.005f))
                    errors.Add($"{bay.name}/support[{i}]: plate/foot centerline falls outside the condenser footprint.");

                float embed = feet[i].bounds.max.y - casing.bounds.min.y;
                TrackRange(ref summary.casingEmbedInitialized, ref summary.minCasingFootEmbedMetres,
                    ref summary.maxCasingFootEmbedMetres, embed);
                if (embed < contract.dimensionsMetres.casingFootEmbedMin ||
                    embed > contract.dimensionsMetres.casingFootEmbedMax)
                    errors.Add($"{bay.name}/support[{i}]: chassis-to-foot embed {embed:0.####} m is outside the controlled range.");
            }

            ValidateMountBolt(bay, "HD_AC_MountBolt_-1_-1", plates[0], contract, summary, errors);
            ValidateMountBolt(bay, "HD_AC_MountBolt_-1_1", plates[0], contract, summary, errors);
            ValidateMountBolt(bay, "HD_AC_MountBolt_1_-1", plates[1], contract, summary, errors);
            ValidateMountBolt(bay, "HD_AC_MountBolt_1_1", plates[1], contract, summary, errors);

            ValidatePipeJoint(bay, "HD_AC_Refrigerant_H", "HD_AC_Refrigerant_V", contract, summary, errors);
            ValidatePipeJoint(bay, "HD_AC_SecondLine_H", "HD_AC_SecondLine_V", contract, summary, errors);

            Transform drain = RequireDirectChild(bay, "HD_AC_DrainHose");
            GetCylinderEndpointsLocal(drain, out Vector3 drainA, out Vector3 drainB);
            float outletY = Mathf.Min(drainA.y, drainB.y);
            float drainClearance = outletY - floorTopLocalY;
            TrackRange(ref summary.drainInitialized, ref summary.minDrainClearanceMetres,
                ref summary.maxDrainClearanceMetres, drainClearance);
            if (drainClearance < contract.dimensionsMetres.drainOutletClearanceMin ||
                drainClearance > contract.dimensionsMetres.drainOutletClearanceMax)
                errors.Add($"{bay.name}: drain outlet clearance {drainClearance:0.####} m is outside the controlled range.");
        }

        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Outdoor AC installation-interface QA FAILED:\n - " + string.Join("\n - ", errors.Take(100)) +
                (errors.Count > 100 ? $"\n - ... {errors.Count - 100} additional errors" : string.Empty));

        return summary;
    }

    private static void ShiftCasingMountedDetail(Transform bay, float worldDeltaY, ref int correctedParts)
    {
        string[] exact = { "HD_AC_FanDisc", "HD_AC_FanHub" };
        foreach (string name in exact)
        {
            Transform t = RequireDirectChild(bay, name);
            t.position += Vector3.up * worldDeltaY;
            EditorUtility.SetDirty(t);
            correctedParts++;
        }

        for (int i = -3; i <= 3; i++)
        {
            foreach (string name in new[] { $"HD_AC_GrilleH_{i}", $"HD_AC_GrilleV_{i}" })
            {
                Transform t = RequireDirectChild(bay, name);
                t.position += Vector3.up * worldDeltaY;
                EditorUtility.SetDirty(t);
                correctedParts++;
            }
        }
    }

    private static void RepositionMountBolt(Transform bay, string name, Renderer plate,
        float embedTarget, ref int correctedParts)
    {
        Renderer bolt = RequireChildRenderer(bay, name);
        MoveRendererMinYTo(bolt, plate.bounds.max.y - embedTarget);
        correctedParts++;
    }

    private static void RebuildServiceLines(Transform bay, float casingDeltaLocalY, float floorTopLocalY,
        float drainClearance, ref int correctedParts)
    {
        Vector3 p0 = new Vector3(1.49f, -0.56f + casingDeltaLocalY, -7.10f);
        Vector3 p1 = new Vector3(1.54f, -0.56f + casingDeltaLocalY, -7.26f);
        Vector3 p2 = new Vector3(1.54f, -0.18f, -7.26f);
        SetCylinderBetween(RequireDirectChild(bay, "HD_AC_Refrigerant_H"), p0, p1, 0.020f);
        SetCylinderBetween(RequireDirectChild(bay, "HD_AC_Refrigerant_V"), p1, p2, 0.020f);

        Vector3 offset = new Vector3(0f, 0.045f, 0f);
        SetCylinderBetween(RequireDirectChild(bay, "HD_AC_SecondLine_H"), p0 + offset, p1 + offset, 0.014f);
        SetCylinderBetween(RequireDirectChild(bay, "HD_AC_SecondLine_V"), p1 + offset, p2 + offset, 0.014f);

        Vector3 drainStart = new Vector3(1.45f, -0.61f + casingDeltaLocalY, -7.08f);
        Vector3 drainEnd = new Vector3(1.42f, floorTopLocalY + drainClearance, -6.88f);
        SetCylinderBetween(RequireDirectChild(bay, "HD_AC_DrainHose"), drainStart, drainEnd, 0.012f);
        correctedParts += 5;
    }

    private static void SetCylinderBetween(Transform cylinder, Vector3 localA, Vector3 localB, float radius)
    {
        Vector3 delta = localB - localA;
        if (delta.sqrMagnitude < 0.000001f)
            throw new InvalidOperationException($"{cylinder.name}: cannot build zero-length service line.");

        cylinder.localPosition = (localA + localB) * 0.5f;
        cylinder.localRotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
        cylinder.localScale = new Vector3(radius, delta.magnitude * 0.5f, radius);
        EditorUtility.SetDirty(cylinder);
    }

    private static void ValidatePipeJoint(Transform bay, string horizontalName, string verticalName,
        InstallationContract contract, ValidationSummary summary, List<string> errors)
    {
        Transform h = RequireDirectChild(bay, horizontalName);
        Transform v = RequireDirectChild(bay, verticalName);
        GetCylinderEndpointsLocal(h, out Vector3 hA, out Vector3 hB);
        GetCylinderEndpointsLocal(v, out Vector3 vA, out Vector3 vB);

        float gap = Mathf.Min(
            Mathf.Min(Vector3.Distance(hA, vA), Vector3.Distance(hA, vB)),
            Mathf.Min(Vector3.Distance(hB, vA), Vector3.Distance(hB, vB)));
        summary.maxPipeJointGapMetres = Mathf.Max(summary.maxPipeJointGapMetres, gap);
        if (gap > contract.dimensionsMetres.pipeJointTolerance)
            errors.Add($"{bay.name}/{horizontalName}+{verticalName}: service-line joint gap {gap:0.####} m exceeds tolerance.");
    }

    private static void GetCylinderEndpointsLocal(Transform cylinder, out Vector3 a, out Vector3 b)
    {
        Vector3 half = cylinder.localRotation * (Vector3.up * cylinder.localScale.y);
        a = cylinder.localPosition - half;
        b = cylinder.localPosition + half;
    }

    private static void ValidateMountBolt(Transform bay, string name, Renderer plate,
        InstallationContract contract, ValidationSummary summary, List<string> errors)
    {
        Renderer bolt = RequireChildRenderer(bay, name);
        summary.boltCount++;
        float embed = plate.bounds.max.y - bolt.bounds.min.y;
        TrackRange(ref summary.boltEmbedInitialized, ref summary.minBoltPlateEmbedMetres,
            ref summary.maxBoltPlateEmbedMetres, embed);
        if (embed < contract.dimensionsMetres.boltPlateEmbedMin ||
            embed > contract.dimensionsMetres.boltPlateEmbedMax)
            errors.Add($"{bay.name}/{name}: bolt-to-plate embed {embed:0.####} m is outside controlled range.");
        if (bolt.bounds.max.y <= plate.bounds.max.y)
            errors.Add($"{bay.name}/{name}: bolt head is fully buried in the mounting plate.");
    }

    private static bool ContainsXZ(Bounds container, Vector3 point, float tolerance)
    {
        return point.x >= container.min.x - tolerance && point.x <= container.max.x + tolerance &&
               point.z >= container.min.z - tolerance && point.z <= container.max.z + tolerance;
    }

    private static void MoveRendererMinYTo(Renderer renderer, float targetWorldY)
    {
        float delta = targetWorldY - renderer.bounds.min.y;
        renderer.transform.position += Vector3.up * delta;
        EditorUtility.SetDirty(renderer.transform);
    }

    private static Transform[] FindGeneratedAcBays(GameObject detailRoot)
    {
        return detailRoot.GetComponentsInChildren<Transform>(true)
            .Where(x => x != detailRoot.transform && x.name.StartsWith("HD_BayAssembly_", StringComparison.Ordinal))
            .Where(x =>
            {
                ParseBayIdentity(x.name, out int floor, out int bay);
                return FindSceneObject($"AC_{floor}_{bay}") != null;
            })
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

    private static Renderer RequireChildRenderer(Transform parent, string name)
    {
        return RequireRenderer(RequireDirectChild(parent, name).gameObject, name);
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

    private static void TrackMax(ref float current, float value)
    {
        current = Mathf.Max(current, value);
    }

    private static void TrackRange(ref bool initialized, ref float min, ref float max, float value)
    {
        if (!initialized)
        {
            min = max = value;
            initialized = true;
            return;
        }
        min = Mathf.Min(min, value);
        max = Mathf.Max(max, value);
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

    private static InstallationContract LoadAndValidateContract()
    {
        InstallationContract contract = LoadContract();
        List<string> errors = ValidateContract(contract);
        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Outdoor AC installation contract FAILED:\n - " + string.Join("\n - ", errors));
        return contract;
    }

    private static InstallationContract LoadContract()
    {
        string absolute = AbsolutePath(ContractPath);
        if (!File.Exists(absolute))
            throw new FileNotFoundException($"Outdoor AC installation contract missing: {ContractPath}");
        InstallationContract contract = JsonUtility.FromJson<InstallationContract>(File.ReadAllText(absolute));
        if (contract == null)
            throw new InvalidOperationException($"Could not parse {ContractPath}");
        return contract;
    }

    private static List<string> ValidateContract(InstallationContract contract)
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
        if (contract.expectedGeneratedUnits != ExpectedGeneratedUnits)
            errors.Add($"expectedGeneratedUnits must be {ExpectedGeneratedUnits}.");
        if (contract.automaticVisualPoints != 0) errors.Add("automaticVisualPoints must remain zero.");
        if (!contract.renderVerificationRequired) errors.Add("renderVerificationRequired must remain true.");

        RequireArray(contract.manufactureModel, 5, "manufactureModel", errors);
        RequireArray(contract.installationSequence, 6, "installationSequence", errors);
        RequireArray(contract.interfacesGapsSeals, 6, "interfacesGapsSeals", errors);
        RequireArray(contract.orientationExposureAging, 4, "orientationExposureAging", errors);
        RequireArray(contract.geometryVsMaterialDetail, 3, "geometryVsMaterialDetail", errors);
        RequireArray(contract.lodPolicy, 4, "lodPolicy", errors);
        RequireArray(contract.criticalDefectRelationship, 4, "criticalDefectRelationship", errors);

        Dimensions d = contract.dimensionsMetres;
        if (d == null) errors.Add("dimensionsMetres is required.");
        else
        {
            RequireNear(d.expectedFloorTopLocalY, -0.79f, 0.0001f, "expectedFloorTopLocalY", errors);
            RequireNear(d.mountPlateHeight, 0.018f, 0.0001f, "mountPlateHeight", errors);
            RequireNear(d.footHeight, 0.045f, 0.0001f, "footHeight", errors);
            RequireNear(d.casingHeight, 0.48f, 0.0001f, "casingHeight", errors);
            RequireNear(d.casingFootEmbedTarget, 0.006f, 0.0001f, "casingFootEmbedTarget", errors);
            RequireNear(d.drainOutletClearanceTarget, 0.010f, 0.0001f, "drainOutletClearanceTarget", errors);
            if (d.floorTopTolerance <= 0f || d.floorTopTolerance > 0.005f)
                errors.Add("floorTopTolerance must be >0 and <=5 mm.");
            if (d.supportContactTolerance <= 0f || d.supportContactTolerance > 0.005f)
                errors.Add("supportContactTolerance must be >0 and <=5 mm.");
            if (d.casingFootEmbedMin < 0.001f || d.casingFootEmbedMax > 0.012f ||
                d.casingFootEmbedMin >= d.casingFootEmbedMax ||
                d.casingFootEmbedTarget < d.casingFootEmbedMin || d.casingFootEmbedTarget > d.casingFootEmbedMax)
                errors.Add("casing/foot embed must stay a small positive controlled 1-12 mm interface with target inside range.");
            if (d.drainOutletClearanceMin < 0.003f || d.drainOutletClearanceMax > 0.025f ||
                d.drainOutletClearanceMin >= d.drainOutletClearanceMax ||
                d.drainOutletClearanceTarget < d.drainOutletClearanceMin ||
                d.drainOutletClearanceTarget > d.drainOutletClearanceMax)
                errors.Add("drain outlet clearance must remain a positive 3-25 mm gap with target inside range.");
            if (d.pipeJointTolerance <= 0f || d.pipeJointTolerance > 0.005f)
                errors.Add("pipeJointTolerance must be >0 and <=5 mm.");
            if (d.boltPlateEmbedMin < 0.0005f || d.boltPlateEmbedMax > 0.008f ||
                d.boltPlateEmbedMin >= d.boltPlateEmbedMax)
                errors.Add("bolt/plate embed must remain a small positive 0.5-8 mm controlled interface.");
        }

        if (contract.materialsFinish == null) errors.Add("materialsFinish is required.");
        if (contract.materialLookdev == null) errors.Add("materialLookdev is required.");
        if (contract.lookdevBrief == null)
            errors.Add("lookdevBrief is required.");
        else
        {
            RequireText(contract.lookdevBrief.frontal, "lookdevBrief.frontal", errors);
            RequireText(contract.lookdevBrief.oblique, "lookdevBrief.oblique", errors);
            RequireText(contract.lookdevBrief.grazing, "lookdevBrief.grazing", errors);
            RequireText(contract.lookdevBrief.macro100Percent, "lookdevBrief.macro100Percent", errors);
        }

        if (contract.researchReferences == null || contract.researchReferences.Length < 3 ||
            contract.researchReferences.Any(x => x == null || string.IsNullOrWhiteSpace(x.title) ||
                                                 string.IsNullOrWhiteSpace(x.url) || string.IsNullOrWhiteSpace(x.relevance)))
            errors.Add("At least three complete researchReferences are required.");

        return errors;
    }

    private static void WriteRuntimeReport(ValidationSummary summary, int correctedParts)
    {
        var report = new RuntimeReport
        {
            schemaVersion = "1.0",
            generatedUtc = DateTime.UtcNow.ToString("O"),
            scenePath = ScenePath,
            unitCount = summary.unitCount,
            supportCount = summary.supportCount,
            boltCount = summary.boltCount,
            correctedPartsThisInvocation = correctedParts,
            maxSupportContactErrorMetres = summary.maxSupportContactErrorMetres,
            minCasingFootEmbedMetres = summary.minCasingFootEmbedMetres,
            maxCasingFootEmbedMetres = summary.maxCasingFootEmbedMetres,
            minBoltPlateEmbedMetres = summary.minBoltPlateEmbedMetres,
            maxBoltPlateEmbedMetres = summary.maxBoltPlateEmbedMetres,
            maxPipeJointGapMetres = summary.maxPipeJointGapMetres,
            minDrainClearanceMetres = summary.minDrainClearanceMetres,
            maxDrainClearanceMetres = summary.maxDrainClearanceMetres,
            maxCasingRaiseMetres = summary.maxCasingRaiseMetres,
            visualFidelityPointsAwarded = 0,
            renderedVerificationPending = true,
            note = "Scene-source outdoor-unit construction interfaces verified only. Native 3840x2160 frontal/oblique/grazing evidence and 100% crop review remain required."
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
    private sealed class InstallationContract
    {
        public string schemaVersion;
        public string scenePath;
        public string detailRootName;
        public int expectedGeneratedUnits;
        public string policy;
        public string[] manufactureModel;
        public Dimensions dimensionsMetres;
        public string[] installationSequence;
        public string[] interfacesGapsSeals;
        public MaterialsFinish materialsFinish;
        public MaterialLookdev materialLookdev;
        public string[] orientationExposureAging;
        public string[] geometryVsMaterialDetail;
        public string[] lodPolicy;
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
        public float mountPlateHeight;
        public float footHeight;
        public float casingHeight;
        public float casingFootEmbedTarget;
        public float casingFootEmbedMin;
        public float casingFootEmbedMax;
        public float supportContactTolerance;
        public float drainOutletClearanceTarget;
        public float drainOutletClearanceMin;
        public float drainOutletClearanceMax;
        public float pipeJointTolerance;
        public float boltPlateEmbedMin;
        public float boltPlateEmbedMax;
    }

    [Serializable]
    private sealed class MaterialsFinish
    {
        public string casing;
        public string fanAndGrille;
        public string mountingHardware;
        public string pipeInsulation;
        public string drainHose;
        public string wallCollar;
    }

    // The strings are intentionally descriptive rather than executable render parameters; actual scene
    // material values remain enforced separately by QualityBlockSceneMaterialPhysicalityUpgrade.
    [Serializable]
    private sealed class MaterialLookdev
    {
        public MaterialBrief agedAcPlastic;
        public MaterialBrief galvanizedHardware;
        public MaterialBrief pipeInsulation;
        public MaterialBrief drainHose;
    }

    [Serializable]
    private sealed class MaterialBrief
    {
        public string albedoLinearRange;
        public string roughnessRange;
        public string metallicRange;
        public string dielectricF0Range;
        public string normalScale;
        public string microstructure;
        public string wetness;
        public string uvAging;
        public string angularFresnel;
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
        public int unitCount;
        public int supportCount;
        public int boltCount;
        public float maxSupportContactErrorMetres;
        public bool casingEmbedInitialized;
        public float minCasingFootEmbedMetres;
        public float maxCasingFootEmbedMetres;
        public bool boltEmbedInitialized;
        public float minBoltPlateEmbedMetres;
        public float maxBoltPlateEmbedMetres;
        public float maxPipeJointGapMetres;
        public bool drainInitialized;
        public float minDrainClearanceMetres;
        public float maxDrainClearanceMetres;
        public float maxCasingRaiseMetres;
    }

    [Serializable]
    private sealed class RuntimeReport
    {
        public string schemaVersion;
        public string generatedUtc;
        public string scenePath;
        public int unitCount;
        public int supportCount;
        public int boltCount;
        public int correctedPartsThisInvocation;
        public float maxSupportContactErrorMetres;
        public float minCasingFootEmbedMetres;
        public float maxCasingFootEmbedMetres;
        public float minBoltPlateEmbedMetres;
        public float maxBoltPlateEmbedMetres;
        public float maxPipeJointGapMetres;
        public float minDrainClearanceMetres;
        public float maxDrainClearanceMetres;
        public float maxCasingRaiseMetres;
        public int visualFidelityPointsAwarded;
        public bool renderedVerificationPending;
        public string note;
    }
}
