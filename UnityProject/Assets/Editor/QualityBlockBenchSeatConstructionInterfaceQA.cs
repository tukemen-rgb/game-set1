using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Corrects and verifies the manufactured load path of the generated benchmark bench:
/// timber slats -> steel bearer -> precast supports -> shallow below-grade embedment.
/// The original fallback left an 11.5 mm air gap between slats and bearer while its LOD0
/// bolt heads sat 3 mm above the timber and centered in the inter-slat gaps. This pass
/// removes those source-side floating/interpenetration risks without changing gameplay collision.
///
/// Source/scene QA only. Passing this check awards zero Visual Fidelity points and cannot
/// clear the floating/interpenetrating-geometry critical defect without native 4K evidence.
/// </summary>
public static class QualityBlockBenchSeatConstructionInterfaceQA
{
    private const string ContractPath = "Assets/QA/bench_seat_construction_interface_contract.json";
    private const string RuntimeReportPath = "Assets/QA/bench_seat_construction_interface_runtime_report.json";
    private const string BenchName = "HD_Bench";

    [MenuItem("NewTown/QA/Validate Bench Seat Construction Contract")]
    public static void ValidateContractConfigOnly()
    {
        BenchContract contract = LoadContract();
        List<string> errors = ValidateContract(contract);
        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Bench seat construction-interface contract FAILED:\n - " + string.Join("\n - ", errors));

        Debug.Log(
            "Bench seat construction-interface contract valid. This is implementation evidence only; " +
            "Visual Fidelity remains UNSCORED until native 4K pixels are reviewed.");
    }

    [MenuItem("NewTown/Geometry/Correct Bench Seat Construction Interfaces")]
    public static void ApplyToOpenScene()
    {
        BenchContract contract = LoadAndValidateContract();
        Transform bench = RequireBench();
        int adjusted = 0;

        for (int lod = 0; lod < 4; lod++)
        {
            Transform tier = RequireDirectChild(bench, $"LOD{lod}");
            Transform supportL = RequireDirectChild(tier, "SupportL");
            Transform supportR = RequireDirectChild(tier, "SupportR");

            float supportCenterY = lod <= 1
                ? contract.dimensionsMetres.highDetailSupportCenterY
                : contract.dimensionsMetres.proxySupportCenterY;
            SetLocalY(supportL, supportCenterY);
            SetLocalY(supportR, supportCenterY);
            adjusted += 2;

            if (lod <= 1)
            {
                Transform bearer = RequireDirectChild(tier, "SteelBearer");
                SetLocalY(bearer, contract.dimensionsMetres.steelBearerCenterY);
                adjusted++;

                for (int i = 0; i < contract.dimensionsMetres.slatCount; i++)
                    RequireDirectChild(tier, $"SeatSlat_{i}");
            }
            else
            {
                RequireDirectChild(tier, "SeatProxy");
            }

            if (lod == 0)
            {
                foreach (int xSign in new[] { -1, 1 })
                foreach (int zSign in new[] { -1, 1 })
                {
                    Transform bolt = RequireDirectChild(tier, $"SeatBolt_{xSign}_{zSign}");
                    Vector3 p = bolt.localPosition;
                    p.y = contract.dimensionsMetres.boltHeadCenterY;
                    p.z = zSign * contract.dimensionsMetres.outerSlatCenterAbsZ;
                    bolt.localPosition = p;
                    EditorUtility.SetDirty(bolt);
                    adjusted++;
                }
            }
        }

        LODGroup group = bench.GetComponent<LODGroup>();
        if (group == null)
            throw new InvalidOperationException("HD_Bench LODGroup is missing.");
        group.RecalculateBounds();
        EditorUtility.SetDirty(group);

        ValidationSummary summary = ValidateOpenSceneInternal(contract);
        WriteRuntimeReport(summary, adjusted);

        Debug.Log(
            $"Bench seat construction interfaces corrected: adjustedTransforms={adjusted}, " +
            $"maxLoadPathError={summary.maxLoadPathErrorMetres * 1000f:0.###} mm, " +
            $"boltEmbed={summary.minBoltEmbedMetres * 1000f:0.###}-{summary.maxBoltEmbedMetres * 1000f:0.###} mm. " +
            "Native 4K review is still required before visual points or critical-defect clearance.");
    }

    [MenuItem("NewTown/QA/Validate Bench Seat Construction Interfaces")]
    public static void ValidateOpenScene()
    {
        BenchContract contract = LoadAndValidateContract();
        ValidationSummary summary = ValidateOpenSceneInternal(contract);
        WriteRuntimeReport(summary, 0);

        Debug.Log(
            $"Bench seat construction-interface QA passed: maxLoadPathError={summary.maxLoadPathErrorMetres * 1000f:0.###} mm, " +
            $"boltEmbed={summary.minBoltEmbedMetres * 1000f:0.###}-{summary.maxBoltEmbedMetres * 1000f:0.###} mm. " +
            "Rendered contact shadows, bolt seating and silhouette remain unverified until native 4K evidence exists.");
    }

    private static ValidationSummary ValidateOpenSceneInternal(BenchContract contract)
    {
        Transform bench = RequireBench();
        var errors = new List<string>();
        var summary = new ValidationSummary
        {
            minBoltEmbedMetres = float.PositiveInfinity,
            maxBoltEmbedMetres = float.NegativeInfinity
        };

        for (int lod = 0; lod < 4; lod++)
        {
            Transform tier = RequireDirectChild(bench, $"LOD{lod}");
            Transform supportL = RequireDirectChild(tier, "SupportL");
            Transform supportR = RequireDirectChild(tier, "SupportR");
            Bounds supportLBounds = LocalBounds(supportL);
            Bounds supportRBounds = LocalBounds(supportR);

            if (lod <= 1)
            {
                Transform bearer = RequireDirectChild(tier, "SteelBearer");
                Bounds bearerBounds = LocalBounds(bearer);

                float slatBottom = float.PositiveInfinity;
                float slatTop = float.NegativeInfinity;
                for (int i = 0; i < contract.dimensionsMetres.slatCount; i++)
                {
                    Bounds slatBounds = LocalBounds(RequireDirectChild(tier, $"SeatSlat_{i}"));
                    slatBottom = Mathf.Min(slatBottom, slatBounds.min.y);
                    slatTop = Mathf.Max(slatTop, slatBounds.max.y);
                }

                TrackError(summary, Mathf.Abs(bearerBounds.max.y - slatBottom));
                TrackError(summary, Mathf.Abs(supportLBounds.max.y - bearerBounds.min.y));
                TrackError(summary, Mathf.Abs(supportRBounds.max.y - bearerBounds.min.y));

                if (Mathf.Abs(bearerBounds.max.y - slatBottom) > contract.qaRules.contactToleranceMetres)
                    errors.Add($"LOD{lod}: timber slats do not seat on the steel bearer.");
                if (Mathf.Abs(supportLBounds.max.y - bearerBounds.min.y) > contract.qaRules.contactToleranceMetres ||
                    Mathf.Abs(supportRBounds.max.y - bearerBounds.min.y) > contract.qaRules.contactToleranceMetres)
                    errors.Add($"LOD{lod}: steel bearer does not seat on both precast supports.");

                if (lod == 0)
                {
                    Bounds outerNegative = LocalBounds(RequireDirectChild(tier, "SeatSlat_0"));
                    Bounds outerPositive = LocalBounds(RequireDirectChild(tier, $"SeatSlat_{contract.dimensionsMetres.slatCount - 1}"));

                    foreach (int xSign in new[] { -1, 1 })
                    foreach (int zSign in new[] { -1, 1 })
                    {
                        Transform bolt = RequireDirectChild(tier, $"SeatBolt_{xSign}_{zSign}");
                        Bounds boltBounds = LocalBounds(bolt);
                        float embed = slatTop - boltBounds.min.y;
                        summary.minBoltEmbedMetres = Mathf.Min(summary.minBoltEmbedMetres, embed);
                        summary.maxBoltEmbedMetres = Mathf.Max(summary.maxBoltEmbedMetres, embed);

                        Bounds targetSlat = zSign < 0 ? outerNegative : outerPositive;
                        float boltCenterZ = bolt.localPosition.z;
                        float edgeMargin = Mathf.Min(
                            boltCenterZ - targetSlat.min.z,
                            targetSlat.max.z - boltCenterZ);

                        if (embed < contract.qaRules.boltEmbedMinMetres ||
                            embed > contract.qaRules.boltEmbedMaxMetres)
                            errors.Add(
                                $"LOD0 {bolt.name}: bolt/timber embed {embed * 1000f:0.###} mm outside " +
                                $"{contract.qaRules.boltEmbedMinMetres * 1000f:0.###}-{contract.qaRules.boltEmbedMaxMetres * 1000f:0.###} mm.");

                        if (edgeMargin < contract.qaRules.boltHeadEdgeMarginMinMetres)
                            errors.Add(
                                $"LOD0 {bolt.name}: bolt center is not safely over the intended outer slat; " +
                                $"edge margin={edgeMargin * 1000f:0.###} mm.");

                        if (Mathf.Abs(Mathf.Abs(boltCenterZ) - contract.dimensionsMetres.outerSlatCenterAbsZ) >
                            contract.qaRules.positionToleranceMetres)
                            errors.Add($"LOD0 {bolt.name}: bolt Z drifted away from outer-slat centerline.");
                    }
                }
            }
            else
            {
                Bounds proxyBounds = LocalBounds(RequireDirectChild(tier, "SeatProxy"));
                TrackError(summary, Mathf.Abs(supportLBounds.max.y - proxyBounds.min.y));
                TrackError(summary, Mathf.Abs(supportRBounds.max.y - proxyBounds.min.y));
                if (Mathf.Abs(supportLBounds.max.y - proxyBounds.min.y) > contract.qaRules.contactToleranceMetres ||
                    Mathf.Abs(supportRBounds.max.y - proxyBounds.min.y) > contract.qaRules.contactToleranceMetres)
                    errors.Add($"LOD{lod}: proxy seat does not seat on both precast supports.");
            }

            ValidateGroundInterface(tier, supportLBounds, supportRBounds, contract, errors);
        }

        if (float.IsPositiveInfinity(summary.minBoltEmbedMetres))
        {
            summary.minBoltEmbedMetres = 0f;
            summary.maxBoltEmbedMetres = 0f;
        }

        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Bench seat construction-interface QA FAILED:\n - " + string.Join("\n - ", errors));

        return summary;
    }

    private static void ValidateGroundInterface(
        Transform tier,
        Bounds supportL,
        Bounds supportR,
        BenchContract contract,
        List<string> errors)
    {
        Transform contactL = RequireDirectChild(tier, "ContactL");
        Transform contactR = RequireDirectChild(tier, "ContactR");
        Bounds contactLBounds = LocalBounds(contactL);
        Bounds contactRBounds = LocalBounds(contactR);

        if (supportL.min.y > -contract.qaRules.minimumHiddenSupportEmbedMetres ||
            supportR.min.y > -contract.qaRules.minimumHiddenSupportEmbedMetres)
            errors.Add($"{tier.name}: precast supports are not shallowly embedded below the grade plane.");

        if (Mathf.Abs(contactLBounds.min.y) > contract.qaRules.positionToleranceMetres ||
            Mathf.Abs(contactRBounds.min.y) > contract.qaRules.positionToleranceMetres)
            errors.Add($"{tier.name}: visible moisture/contact sleeves must start at the grade plane, not float above or extend visibly below it.");

        if (contactLBounds.max.y < contract.dimensionsMetres.contactBandHeight - contract.qaRules.positionToleranceMetres ||
            contactRBounds.max.y < contract.dimensionsMetres.contactBandHeight - contract.qaRules.positionToleranceMetres)
            errors.Add($"{tier.name}: contact-weathering sleeve height is below the causal splash/capillary contract.");

        if (contactLBounds.max.y > supportL.max.y || contactRBounds.max.y > supportR.max.y)
            errors.Add($"{tier.name}: contact-weathering sleeve extends above the structural support top.");
    }

    private static void TrackError(ValidationSummary summary, float error)
    {
        summary.maxLoadPathErrorMetres = Mathf.Max(summary.maxLoadPathErrorMetres, error);
    }

    private static Bounds LocalBounds(Transform child)
    {
        MeshFilter filter = child.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null)
            throw new InvalidOperationException($"Mesh missing on bench component {child.name}.");

        Bounds meshBounds = filter.sharedMesh.bounds;
        Vector3 center = child.localPosition + Vector3.Scale(meshBounds.center, child.localScale);
        Vector3 size = Vector3.Scale(meshBounds.size, Abs(child.localScale));
        return new Bounds(center, size);
    }

    private static Vector3 Abs(Vector3 v) =>
        new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    private static void SetLocalY(Transform transform, float y)
    {
        Vector3 p = transform.localPosition;
        p.y = y;
        transform.localPosition = p;
        EditorUtility.SetDirty(transform);
    }

    private static Transform RequireBench()
    {
        GameObject bench = GameObject.Find(BenchName);
        if (bench == null)
            throw new InvalidOperationException("HD_Bench is missing; park/street furniture must be built before bench interface QA.");
        return bench.transform;
    }

    private static Transform RequireDirectChild(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child == null)
            throw new InvalidOperationException($"Missing bench component: {parent.name}/{childName}");
        return child;
    }

    private static BenchContract LoadAndValidateContract()
    {
        BenchContract contract = LoadContract();
        List<string> errors = ValidateContract(contract);
        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Bench seat construction-interface contract FAILED:\n - " + string.Join("\n - ", errors));
        return contract;
    }

    private static BenchContract LoadContract()
    {
        if (!File.Exists(ContractPath))
            throw new InvalidOperationException($"Missing required construction metadata: {ContractPath}");
        BenchContract contract = JsonUtility.FromJson<BenchContract>(File.ReadAllText(ContractPath));
        if (contract == null)
            throw new InvalidOperationException($"Could not parse bench construction metadata: {ContractPath}");
        return contract;
    }

    private static List<string> ValidateContract(BenchContract contract)
    {
        var errors = new List<string>();
        if (contract.schemaVersion != "1.0") errors.Add($"schemaVersion must remain 1.0, got {contract.schemaVersion}.");
        if (contract.id != "bench_seat_construction_interface") errors.Add("contract id drifted.");
        if (contract.dimensionsMetres == null) errors.Add("dimensionsMetres missing.");
        if (contract.qaRules == null) errors.Add("qaRules missing.");
        if (contract.renderVerification == null) errors.Add("renderVerification missing.");
        if (errors.Count > 0) return errors;

        DimensionsMetres d = contract.dimensionsMetres;
        QaRules q = contract.qaRules;
        if (d.slatCount != 5) errors.Add($"slatCount must remain 5, got {d.slatCount}.");
        if (!Approx(d.slatCenterY, 0.56f) || !Approx(d.slatThickness, 0.042f))
            errors.Add("slat elevation/thickness drifted from the manufactured benchmark contract.");
        if (!Approx(d.steelBearerThickness, 0.045f) || !Approx(d.steelBearerCenterY, 0.5165f))
            errors.Add("steel bearer seating dimensions drifted.");
        if (!Approx(d.highDetailSupportCenterY, 0.214f) || !Approx(d.proxySupportCenterY, 0.2275f))
            errors.Add("precast support seating dimensions drifted.");
        if (!Approx(d.boltHeadHeight, 0.008f) || !Approx(d.boltHeadCenterY, 0.583f))
            errors.Add("bolt seating dimensions drifted.");
        if (!Approx(d.outerSlatCenterAbsZ, 0.235f))
            errors.Add("outer slat / bolt centerline drifted.");
        if (q.contactToleranceMetres > 0.003f)
            errors.Add("contact tolerance may not exceed 3 mm.");
        if (q.boltEmbedMinMetres < 0.001f || q.boltEmbedMaxMetres > 0.004f ||
            q.boltEmbedMinMetres >= q.boltEmbedMaxMetres)
            errors.Add("bolt embed hard range must stay within 1-4 mm.");
        if (q.boltHeadEdgeMarginMinMetres < 0.020f)
            errors.Add("bolt head edge margin may not be below 20 mm.");
        if (contract.renderVerification.visualFidelityPointsAwarded != 0)
            errors.Add("source bench interface QA may never award Visual Fidelity points.");
        if (contract.renderVerification.status != "PENDING_UNITY_RUNTIME")
            errors.Add("render verification must remain pending until actual Unity evidence exists.");
        return errors;
    }

    private static bool Approx(float a, float b) => Mathf.Abs(a - b) <= 0.00001f;

    private static void WriteRuntimeReport(ValidationSummary summary, int adjustedTransforms)
    {
        var report = new RuntimeReport
        {
            schemaVersion = "1.0",
            status = "SOURCE_SCENE_QA_ONLY",
            adjustedTransforms = adjustedTransforms,
            maxLoadPathErrorMetres = summary.maxLoadPathErrorMetres,
            minBoltEmbedMetres = summary.minBoltEmbedMetres,
            maxBoltEmbedMetres = summary.maxBoltEmbedMetres,
            visualFidelityPointsAwarded = 0,
            renderVerification = "PENDING_UNITY_RUNTIME",
            note = "Passing this report does not clear floating/interpenetrating geometry without native 3840x2160 evidence and 100% crop review."
        };

        File.WriteAllText(RuntimeReportPath, JsonUtility.ToJson(report, true));
        AssetDatabase.ImportAsset(RuntimeReportPath, ImportAssetOptions.ForceUpdate);
    }

    [Serializable]
    private sealed class BenchContract
    {
        public string schemaVersion;
        public string id;
        public DimensionsMetres dimensionsMetres;
        public QaRules qaRules;
        public RenderVerification renderVerification;
    }

    [Serializable]
    private sealed class DimensionsMetres
    {
        public int slatCount;
        public float slatCenterY;
        public float slatThickness;
        public float steelBearerThickness;
        public float steelBearerCenterY;
        public float highDetailSupportCenterY;
        public float proxySupportCenterY;
        public float supportHeight;
        public float boltHeadHeight;
        public float boltHeadCenterY;
        public float outerSlatCenterAbsZ;
        public float contactBandHeight;
    }

    [Serializable]
    private sealed class QaRules
    {
        public float contactToleranceMetres;
        public float positionToleranceMetres;
        public float boltEmbedMinMetres;
        public float boltEmbedMaxMetres;
        public float boltHeadEdgeMarginMinMetres;
        public float minimumHiddenSupportEmbedMetres;
    }

    [Serializable]
    private sealed class RenderVerification
    {
        public string status;
        public int visualFidelityPointsAwarded;
    }

    [Serializable]
    private sealed class RuntimeReport
    {
        public string schemaVersion;
        public string status;
        public int adjustedTransforms;
        public float maxLoadPathErrorMetres;
        public float minBoltEmbedMetres;
        public float maxBoltEmbedMetres;
        public int visualFidelityPointsAwarded;
        public string renderVerification;
        public string note;
    }

    private sealed class ValidationSummary
    {
        public float maxLoadPathErrorMetres;
        public float minBoltEmbedMetres;
        public float maxBoltEmbedMetres;
    }
}
