using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Reconstructs and verifies the missing manufactured access/load-path of HD_Slide.
/// The prior generated assembly had an approximately two-metre platform and a physical chute but no
/// climbable stair section. This pass adds a ten-tread coated-steel stair, laterally separated inclined
/// stringers with welded tread brackets, access rails, grade plates, a chute-head bearing tied to the
/// existing frame, and a ground-supported runout.
///
/// Source/runtime transform QA only. Passing this check awards zero Visual Fidelity points and cannot
/// clear floating/interpenetration, primitive, material or LOD critical defects without native 4K evidence.
/// </summary>
public static class QualityBlockSlideAccessInstallationQA
{
    private const string ContractPath = "Assets/QA/slide_access_installation_contract.json";
    private const string LookdevPath = "Assets/QA/slide_access_installation_lookdev.svg";
    private const string RuntimeReportPath = "Assets/QA/slide_access_installation_runtime_report.json";
    private const string SlideName = "HD_Slide";
    private const string GeneratedRootName = "SlideAccessInstallation";

    [MenuItem("NewTown/QA/Validate Slide Access Installation Contract")]
    public static void ValidateContractConfigOnly()
    {
        SlideContract contract = LoadContract();
        List<string> errors = ValidateContract(contract);
        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Slide access installation contract FAILED:\n - " + string.Join("\n - ", errors));

        if (!File.Exists(LookdevPath))
            throw new InvalidOperationException($"Missing required slide access lookdev illustration: {LookdevPath}");

        Debug.Log(
            "Slide access installation contract valid. This is implementation evidence only; " +
            "Visual Fidelity remains UNSCORED until native 4K pixels are reviewed.");
    }

    [MenuItem("NewTown/Geometry/Reconstruct Slide Access + Support Interfaces")]
    public static void ApplyToOpenScene()
    {
        SlideContract contract = LoadAndValidateContract();
        Transform slide = RequireSlide();
        int generatedRenderers = 0;

        for (int lod = 0; lod < contract.qaRules.requiredLodCount; lod++)
        {
            Transform tier = RequireDirectChild(slide, $"LOD{lod}");
            DestroyDirectChildIfPresent(tier, GeneratedRootName);

            Material painted = RequireMaterial(tier, "PBR_ParkPaintedSteel");
            Material exposed = RequireMaterial(tier, "PBR_ParkExposedSteel");
            Transform generated = new GameObject(GeneratedRootName).transform;
            generated.SetParent(tier, false);

            BuildAccessStair(generated, tier, contract, painted, exposed, lod);
            BuildChuteSupports(generated, tier, contract, painted);
            generatedRenderers += generated.GetComponentsInChildren<Renderer>(true).Length;
        }

        LODGroup group = slide.GetComponent<LODGroup>();
        if (group == null)
            throw new InvalidOperationException("HD_Slide LODGroup is missing.");
        group.RecalculateBounds();
        EditorUtility.SetDirty(group);

        ValidationSummary summary = ValidateOpenSceneInternal(contract, requireLodBinding: false);
        WriteRuntimeReport(summary, generatedRenderers, false);

        Debug.Log(
            $"Slide access/support reconstruction applied: generatedRenderers={generatedRenderers}, " +
            $"accessAngle={summary.accessAngleDegrees:0.###} deg, " +
            $"minTread/StringerClearance={summary.minStringerTreadLateralClearanceMetres * 1000f:0.###} mm, " +
            $"maxTread/BracketError={summary.maxTreadBracketContactErrorMetres * 1000f:0.###} mm. " +
            "LOD renderer rebinding must run next; native 4K review is still required before visual credit.");
    }

    [MenuItem("NewTown/QA/Validate Slide Access + Support Interfaces")]
    public static void ValidateOpenScene()
    {
        SlideContract contract = LoadAndValidateContract();
        ValidationSummary summary = ValidateOpenSceneInternal(contract, requireLodBinding: true);
        int renderers = CountGeneratedRenderers(RequireSlide(), contract.qaRules.requiredLodCount);
        WriteRuntimeReport(summary, renderers, true);

        Debug.Log(
            $"Slide access/support QA passed: angle={summary.accessAngleDegrees:0.###} deg, " +
            $"minTread/StringerClearance={summary.minStringerTreadLateralClearanceMetres * 1000f:0.###} mm, " +
            $"maxTread/BracketError={summary.maxTreadBracketContactErrorMetres * 1000f:0.###} mm, " +
            $"maxBracket/StringerMiss={summary.maxBracketStringerAxisMissMetres * 1000f:0.###} mm, " +
            $"headBearingGap={summary.maxHeadBearingGapMetres * 1000f:0.###} mm, " +
            $"runoutBearingGap={summary.maxRunoutBearingGapMetres * 1000f:0.###} mm. " +
            "Rendered contact shadows, material response and temporal stability remain unverified.");
    }

    private static void BuildAccessStair(
        Transform parent,
        Transform tier,
        SlideContract contract,
        Material painted,
        Material exposed,
        int lod)
    {
        DimensionsMetres d = contract.dimensionsMetres;

        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * d.stringerCenterAbsX;
            CreateBox(
                $"AccessFootPlate_{SideName(side)}", parent,
                new Vector3(x, d.accessFootPlateThickness * 0.5f, d.stringerLowerZ),
                new Vector3(d.accessFootPlateWidth, d.accessFootPlateThickness, d.accessFootPlateDepth), painted);

            CreatePipeBetween(
                $"AccessStringer_{SideName(side)}", parent,
                new Vector3(x, d.stringerLowerY, d.stringerLowerZ),
                new Vector3(x, d.stringerUpperY, d.stringerUpperZ),
                d.stringerDiameter, painted);
        }

        for (int i = 0; i < d.treadCount; i++)
        {
            float y = d.firstTreadCenterY + i * d.treadRise;
            float z = d.firstTreadCenterZ + i * d.treadRun;
            CreateBox(
                $"AccessTread_{i:00}", parent,
                new Vector3(0f, y, z),
                new Vector3(d.treadWidth, d.treadThickness, d.treadDepth), painted);

            float treadBottom = y - d.treadThickness * 0.5f;
            for (int side = -1; side <= 1; side += 2)
            {
                // The tread plate stops before the round stringer. A small welded bracket bridges the
                // lateral clearance and overlaps the stringer envelope instead of making the horizontal
                // plate slice through an inclined tube.
                CreateBox(
                    $"AccessTreadBracket_{SideName(side)}_{i:00}", parent,
                    new Vector3(
                        side * d.treadBracketCenterAbsX,
                        treadBottom - d.treadBracketHeight * 0.5f,
                        z),
                    new Vector3(d.treadBracketWidth, d.treadBracketHeight, d.treadBracketDepth),
                    painted);
            }
        }

        float lastY = d.firstTreadCenterY + (d.treadCount - 1) * d.treadRise;
        float lastZ = d.firstTreadCenterZ + (d.treadCount - 1) * d.treadRun;
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 railStart = new Vector3(
                side * d.handrailCenterAbsX,
                d.firstTreadCenterY + d.handrailVerticalOffset,
                d.firstTreadCenterZ);
            Vector3 railEnd = new Vector3(
                side * d.handrailCenterAbsX,
                lastY + d.handrailVerticalOffset,
                lastZ);
            CreatePipeBetween($"AccessHandrail_{SideName(side)}", parent, railStart, railEnd, d.handrailDiameter, painted);

            Transform platformRail = RequireDirectChild(tier, side < 0 ? "HandrailL" : "HandrailR");
            Vector3 platformAttach = PointOnPipeAxisAtY(platformRail, railEnd.y);
            CreatePipeBetween(
                $"AccessRailBridge_{SideName(side)}", parent,
                railEnd, platformAttach, d.handrailDiameter, painted);
        }

        // Small exposed heads only survive in LOD0. They are deliberately not used as fake rivet noise
        // in lower LODs, where they would alias before they add construction information.
        if (lod == 0)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                CreateFastener(
                    $"ChuteHeadFastener_{SideName(side)}", parent,
                    new Vector3(side * 0.36f, 1.995f, d.headBearerCenterZ),
                    d.lod0HeadFastenerDiameter, d.lod0HeadFastenerHeight, exposed);
            }
        }
    }

    private static void BuildChuteSupports(
        Transform parent,
        Transform tier,
        SlideContract contract,
        Material painted)
    {
        DimensionsMetres d = contract.dimensionsMetres;

        CreatePipeBetween(
            "ChuteHeadBearer", parent,
            new Vector3(-d.headBearerLength * 0.5f, d.headBearerCenterY, d.headBearerCenterZ),
            new Vector3(d.headBearerLength * 0.5f, d.headBearerCenterY, d.headBearerCenterZ),
            d.headBearerDiameter, painted);

        for (int side = -1; side <= 1; side += 2)
        {
            Transform leg = RequireDirectChild(tier, side < 0 ? "LegL" : "LegR");
            Vector3 bearerEnd = new Vector3(
                side * d.headBearerLength * 0.5f,
                d.headBearerCenterY,
                d.headBearerCenterZ);
            Vector3 frameAttach = PointOnPipeAxisAtY(leg, d.headBearerCenterY);
            CreatePipeBetween(
                $"ChuteHeadTie_{SideName(side)}", parent,
                bearerEnd, frameAttach, d.handrailDiameter, painted);
        }

        CreatePipeBetween(
            "RunoutBearer", parent,
            new Vector3(-d.runoutBearerLength * 0.5f, d.runoutBearerCenterY, d.runoutBearerCenterZ),
            new Vector3(d.runoutBearerLength * 0.5f, d.runoutBearerCenterY, d.runoutBearerCenterZ),
            d.runoutBearerDiameter, painted);

        float runoutFootTop = d.runoutFootPlateThickness;
        float runoutBearerBottom = d.runoutBearerCenterY - d.runoutBearerDiameter * 0.5f;
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * d.runoutPostCenterAbsX;
            CreateBox(
                $"RunoutFootPlate_{SideName(side)}", parent,
                new Vector3(x, d.runoutFootPlateThickness * 0.5f, d.runoutBearerCenterZ),
                new Vector3(d.runoutFootPlateWidth, d.runoutFootPlateThickness, d.runoutFootPlateDepth), painted);
            CreatePipeBetween(
                $"RunoutPost_{SideName(side)}", parent,
                new Vector3(x, runoutFootTop, d.runoutBearerCenterZ),
                new Vector3(x, runoutBearerBottom, d.runoutBearerCenterZ),
                d.runoutPostDiameter, painted);
        }
    }

    private static ValidationSummary ValidateOpenSceneInternal(SlideContract contract, bool requireLodBinding)
    {
        Transform slide = RequireSlide();
        var errors = new List<string>();
        var summary = new ValidationSummary
        {
            minStringerTreadLateralClearanceMetres = float.PositiveInfinity
        };
        DimensionsMetres d = contract.dimensionsMetres;
        QaRules q = contract.qaRules;

        LODGroup group = slide.GetComponent<LODGroup>();
        LOD[] lods = group != null ? group.GetLODs() : Array.Empty<LOD>();
        if (lods.Length != q.requiredLodCount)
            errors.Add($"HD_Slide must retain {q.requiredLodCount} LODs; found {lods.Length}.");
        if (group == null || !group.animateCrossFading || group.fadeMode != LODFadeMode.CrossFade)
            errors.Add("HD_Slide must retain animated cross-fade LOD transitions.");

        for (int lod = 0; lod < q.requiredLodCount; lod++)
        {
            Transform tier = RequireDirectChild(slide, $"LOD{lod}");
            Transform generated = RequireDirectChild(tier, GeneratedRootName);
            if (generated.GetComponentsInChildren<Collider>(true).Length != q.generatedColliderCount)
                errors.Add($"LOD{lod}: slide visual access/support assembly must add zero gameplay colliders.");

            MeshFilter builtIn = generated.GetComponentsInChildren<MeshFilter>(true)
                .FirstOrDefault(x => x.sharedMesh != null && IsBuiltInPrimitive(x.sharedMesh.name));
            if (builtIn != null)
                errors.Add($"LOD{lod}: generated slide access exposes built-in primitive mesh {builtIn.sharedMesh.name} on {builtIn.name}.");

            Transform deck = RequireDirectChild(tier, "Deck");
            Bounds deckBounds = LocalAxisAlignedBounds(deck);
            Transform firstTread = RequireDirectChild(generated, "AccessTread_00");
            Transform lastTread = RequireDirectChild(generated, $"AccessTread_{d.treadCount - 1:00}");
            Bounds firstBounds = LocalAxisAlignedBounds(firstTread);
            Bounds lastBounds = LocalAxisAlignedBounds(lastTread);

            float firstRise = firstBounds.max.y;
            if (firstRise < q.minimumFirstTreadRiseMetres - q.positionToleranceMetres ||
                firstRise > q.maximumFirstTreadRiseMetres + q.positionToleranceMetres)
                errors.Add($"LOD{lod}: first tread rise {firstRise:0.####} m is outside the locked access range.");

            float topRise = deckBounds.max.y - lastBounds.max.y;
            float topHorizontalGap = deckBounds.min.z - lastBounds.max.z;
            summary.maxTopStepRiseMetres = Mathf.Max(summary.maxTopStepRiseMetres, topRise);
            summary.maxTopHorizontalGapMetres = Mathf.Max(summary.maxTopHorizontalGapMetres, topHorizontalGap);
            if (topRise < -q.contactToleranceMetres || topRise > q.maximumTopTreadVerticalRiseMetres)
                errors.Add($"LOD{lod}: top tread/platform vertical rise {topRise:0.####} m is not stepable.");
            if (topHorizontalGap < -q.contactToleranceMetres || topHorizontalGap > q.maximumTopTreadHorizontalGapMetres)
                errors.Add($"LOD{lod}: top tread/platform horizontal gap {topHorizontalGap:0.####} m is outside the locked interface range.");

            Transform leftStringer = RequireDirectChild(generated, "AccessStringer_L");
            Transform rightStringer = RequireDirectChild(generated, "AccessStringer_R");
            AxisSegment leftAxis = PipeAxis(leftStringer);
            AxisSegment rightAxis = PipeAxis(rightStringer);

            float lateralClearance =
                d.stringerCenterAbsX - d.stringerDiameter * 0.5f - d.treadWidth * 0.5f;
            summary.minStringerTreadLateralClearanceMetres = Mathf.Min(
                summary.minStringerTreadLateralClearanceMetres, lateralClearance);
            if (lateralClearance < q.minimumStringerToTreadLateralClearanceMetres)
                errors.Add(
                    $"LOD{lod}: tread/stringer lateral clearance {lateralClearance * 1000f:0.###} mm is below " +
                    $"{q.minimumStringerToTreadLateralClearanceMetres * 1000f:0.###} mm; the horizontal tread risks cutting into the inclined pipe.");

            for (int i = 0; i < d.treadCount; i++)
            {
                Transform tread = RequireDirectChild(generated, $"AccessTread_{i:00}");
                Bounds tb = LocalAxisAlignedBounds(tread);
                if (tb.size.z + q.positionToleranceMetres < q.minimumTreadDepthMetres)
                    errors.Add($"LOD{lod} tread {i}: depth {tb.size.z:0.####} m is below the 170 mm floor.");

                if (i > 0)
                {
                    Transform prev = RequireDirectChild(generated, $"AccessTread_{i - 1:00}");
                    float rise = tread.localPosition.y - prev.localPosition.y;
                    if (rise <= 0f || rise > q.maximumStepRiseMetres + q.positionToleranceMetres)
                        errors.Add($"LOD{lod} tread {i}: rise {rise:0.####} m exceeds the locked step-rise limit.");
                }

                for (int side = -1; side <= 1; side += 2)
                {
                    Transform bracket = RequireDirectChild(
                        generated, $"AccessTreadBracket_{SideName(side)}_{i:00}");
                    Bounds bb = LocalAxisAlignedBounds(bracket);
                    float treadBracketError = Mathf.Abs(bb.max.y - tb.min.y);
                    summary.maxTreadBracketContactErrorMetres = Mathf.Max(
                        summary.maxTreadBracketContactErrorMetres, treadBracketError);
                    if (treadBracketError > q.contactToleranceMetres)
                        errors.Add(
                            $"LOD{lod} tread {i} {SideName(side)}: tread/bracket vertical contact error is " +
                            $"{treadBracketError * 1000f:0.###} mm.");

                    AxisSegment axis = side < 0 ? leftAxis : rightAxis;
                    Vector3 stringerAxisAtTread = PointOnSegmentAtZ(axis, tread.localPosition.z);
                    float bracketAxisMiss = DistanceOutsideBounds(stringerAxisAtTread, bb);
                    summary.maxBracketStringerAxisMissMetres = Mathf.Max(
                        summary.maxBracketStringerAxisMissMetres, bracketAxisMiss);
                    if (bracketAxisMiss > q.maximumBracketStringerAxisMissMetres)
                        errors.Add(
                            $"LOD{lod} tread {i} {SideName(side)}: welded bracket misses the stringer axis envelope by " +
                            $"{bracketAxisMiss * 1000f:0.###} mm.");

                    float treadSide = side < 0 ? tb.min.x : tb.max.x;
                    float bracketInner = side < 0 ? bb.max.x : bb.min.x;
                    if (Mathf.Abs(treadSide - bracketInner) > q.contactToleranceMetres)
                        errors.Add(
                            $"LOD{lod} tread {i} {SideName(side)}: bracket does not begin at the tread edge; " +
                            $"edge error={Mathf.Abs(treadSide - bracketInner) * 1000f:0.###} mm.");
                }
            }

            Vector3 stairDelta = lastTread.localPosition - firstTread.localPosition;
            float angle = Mathf.Atan2(stairDelta.y, Mathf.Abs(stairDelta.z)) * Mathf.Rad2Deg;
            summary.accessAngleDegrees = angle;
            if (angle < q.minimumAccessAngleDegrees - 0.01f || angle > q.maximumAccessAngleDegrees + 0.01f)
                errors.Add($"LOD{lod}: access angle {angle:0.###} deg is outside {q.minimumAccessAngleDegrees}-{q.maximumAccessAngleDegrees} deg.");

            for (int side = -1; side <= 1; side += 2)
            {
                Transform plate = RequireDirectChild(generated, $"AccessFootPlate_{SideName(side)}");
                Bounds pb = LocalAxisAlignedBounds(plate);
                AxisSegment axis = side < 0 ? leftAxis : rightAxis;
                Vector3 lower = axis.a.y <= axis.b.y ? axis.a : axis.b;
                Vector3 plateTop = new Vector3(side * d.stringerCenterAbsX, pb.max.y, d.stringerLowerZ);
                float footError = Vector3.Distance(lower, plateTop);
                summary.maxFootPlateContactErrorMetres = Mathf.Max(summary.maxFootPlateContactErrorMetres, footError);
                if (footError > q.contactToleranceMetres)
                    errors.Add($"LOD{lod}: {SideName(side)} stringer does not terminate into its grade plate ({footError * 1000f:0.###} mm error).");
            }

            Transform chute = RequireDirectChild(tier, "PhysicalChute");
            MeshFilter chuteFilter = chute.GetComponent<MeshFilter>();
            if (chuteFilter == null || chuteFilter.sharedMesh == null)
            {
                errors.Add($"LOD{lod}: PhysicalChute mesh missing.");
            }
            else
            {
                float headSkinBottom = ExtremeRingMinY(chuteFilter.sharedMesh, useMinZ: true);
                float runoutSkinBottom = ExtremeRingMinY(chuteFilter.sharedMesh, useMinZ: false);
                Bounds headBearer = LocalAxisAlignedBounds(RequireDirectChild(generated, "ChuteHeadBearer"));
                Bounds runoutBearer = LocalAxisAlignedBounds(RequireDirectChild(generated, "RunoutBearer"));
                float headGap = headSkinBottom - headBearer.max.y;
                float runoutGap = runoutSkinBottom - runoutBearer.max.y;
                summary.maxHeadBearingGapMetres = Mathf.Max(summary.maxHeadBearingGapMetres, Mathf.Abs(headGap));
                summary.maxRunoutBearingGapMetres = Mathf.Max(summary.maxRunoutBearingGapMetres, Mathf.Abs(runoutGap));
                if (Mathf.Abs(headGap) > q.maximumChuteBearingGapMetres)
                    errors.Add($"LOD{lod}: chute-head bearing mismatch is {headGap * 1000f:0.###} mm.");
                if (Mathf.Abs(runoutGap) > q.maximumChuteBearingGapMetres)
                    errors.Add($"LOD{lod}: runout bearing mismatch is {runoutGap * 1000f:0.###} mm.");
            }

            Bounds runout = LocalAxisAlignedBounds(RequireDirectChild(generated, "RunoutBearer"));
            for (int side = -1; side <= 1; side += 2)
            {
                Transform post = RequireDirectChild(generated, $"RunoutPost_{SideName(side)}");
                Transform foot = RequireDirectChild(generated, $"RunoutFootPlate_{SideName(side)}");
                AxisSegment postAxis = PipeAxis(post);
                Vector3 low = postAxis.a.y <= postAxis.b.y ? postAxis.a : postAxis.b;
                Vector3 high = postAxis.a.y > postAxis.b.y ? postAxis.a : postAxis.b;
                Bounds footBounds = LocalAxisAlignedBounds(foot);
                float footError = Mathf.Abs(low.y - footBounds.max.y);
                float bearerError = Mathf.Abs(high.y - runout.min.y);
                summary.maxRunoutLoadPathErrorMetres = Mathf.Max(
                    summary.maxRunoutLoadPathErrorMetres, Mathf.Max(footError, bearerError));
                if (footError > q.contactToleranceMetres || bearerError > q.contactToleranceMetres)
                    errors.Add($"LOD{lod}: {SideName(side)} runout post does not continuously seat foot plate -> bearer.");
            }

            if (requireLodBinding && lod < lods.Length)
            {
                var bound = new HashSet<Renderer>(lods[lod].renderers ?? Array.Empty<Renderer>());
                Renderer missing = generated.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => !bound.Contains(r));
                if (missing != null)
                    errors.Add($"LOD{lod}: generated renderer is outside the rebound LOD set: {missing.name}.");
            }

            ValidateMaterialClass(generated, errors, lod);
        }

        if (float.IsPositiveInfinity(summary.minStringerTreadLateralClearanceMetres))
            summary.minStringerTreadLateralClearanceMetres = 0f;

        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Slide access/support installation QA FAILED:\n - " + string.Join("\n - ", errors));

        return summary;
    }

    private static void ValidateMaterialClass(Transform generated, List<string> errors, int lod)
    {
        foreach (Renderer renderer in generated.GetComponentsInChildren<Renderer>(true))
        {
            Material material = renderer.sharedMaterial;
            if (material == null)
            {
                errors.Add($"LOD{lod}: generated slide component {renderer.name} has no material.");
                continue;
            }

            if (material.name == "PBR_ParkPaintedSteel" && material.HasProperty("_Metallic") &&
                material.GetFloat("_Metallic") > 0.05f)
                errors.Add($"LOD{lod}: coated structural steel became materially metallic on {renderer.name}.");
        }
    }

    private static float ExtremeRingMinY(Mesh mesh, bool useMinZ)
    {
        Vector3[] vertices = mesh.vertices;
        if (vertices == null || vertices.Length == 0)
            throw new InvalidOperationException("PhysicalChute mesh has no vertices.");

        float extremeZ = useMinZ ? vertices.Min(v => v.z) : vertices.Max(v => v.z);
        float tolerance = Mathf.Max(0.0005f, mesh.bounds.size.z * 0.0002f);
        float minY = float.PositiveInfinity;
        foreach (Vector3 v in vertices)
        {
            if (Mathf.Abs(v.z - extremeZ) <= tolerance)
                minY = Mathf.Min(minY, v.y);
        }
        if (float.IsPositiveInfinity(minY))
            throw new InvalidOperationException("Could not sample PhysicalChute end ring.");
        return minY;
    }

    private static void CreateBox(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.AddComponent<MeshFilter>().sharedMesh = QualityBlockDetailMeshLibrary.GetChamferedBox(size);
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static void CreatePipeBetween(
        string name,
        Transform parent,
        Vector3 a,
        Vector3 b,
        float diameter,
        Material material)
    {
        Vector3 delta = b - a;
        float length = delta.magnitude;
        if (length <= 0.001f)
            throw new InvalidOperationException($"Cannot create zero-length slide pipe {name}.");

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = (a + b) * 0.5f;
        go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, delta / length);
        go.AddComponent<MeshFilter>().sharedMesh = QualityBlockDetailMeshLibrary.GetBeveledCylinder(
            new Vector3(diameter, length * 0.5f, diameter), false);
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static void CreateFastener(
        string name,
        Transform parent,
        Vector3 localPosition,
        float diameter,
        float height,
        Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.AddComponent<MeshFilter>().sharedMesh = QualityBlockDetailMeshLibrary.GetBeveledCylinder(
            new Vector3(diameter, height * 0.5f, diameter), true);
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static AxisSegment PipeAxis(Transform pipe)
    {
        MeshFilter filter = pipe.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null)
            throw new InvalidOperationException($"Pipe mesh missing: {pipe.name}");
        float halfLength = filter.sharedMesh.bounds.extents.y * Mathf.Abs(pipe.localScale.y);
        Vector3 axis = pipe.localRotation * Vector3.up;
        Vector3 center = pipe.localPosition + pipe.localRotation * Vector3.Scale(filter.sharedMesh.bounds.center, pipe.localScale);
        return new AxisSegment
        {
            a = center - axis * halfLength,
            b = center + axis * halfLength
        };
    }

    private static Vector3 PointOnPipeAxisAtY(Transform pipe, float targetY)
    {
        AxisSegment segment = PipeAxis(pipe);
        float dy = segment.b.y - segment.a.y;
        if (Mathf.Abs(dy) < 0.00001f)
            return (segment.a + segment.b) * 0.5f;
        float t = Mathf.Clamp01((targetY - segment.a.y) / dy);
        return Vector3.Lerp(segment.a, segment.b, t);
    }

    private static Vector3 PointOnSegmentAtZ(AxisSegment segment, float targetZ)
    {
        float dz = segment.b.z - segment.a.z;
        if (Mathf.Abs(dz) < 0.00001f)
            return (segment.a + segment.b) * 0.5f;
        float t = Mathf.Clamp01((targetZ - segment.a.z) / dz);
        return Vector3.Lerp(segment.a, segment.b, t);
    }

    private static float DistanceOutsideBounds(Vector3 point, Bounds bounds)
    {
        float dx = point.x < bounds.min.x ? bounds.min.x - point.x :
                   point.x > bounds.max.x ? point.x - bounds.max.x : 0f;
        float dy = point.y < bounds.min.y ? bounds.min.y - point.y :
                   point.y > bounds.max.y ? point.y - bounds.max.y : 0f;
        float dz = point.z < bounds.min.z ? bounds.min.z - point.z :
                   point.z > bounds.max.z ? point.z - bounds.max.z : 0f;
        return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private static Bounds LocalAxisAlignedBounds(Transform child)
    {
        MeshFilter filter = child.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null)
            throw new InvalidOperationException($"Mesh missing on slide component {child.name}.");

        if (Quaternion.Angle(child.localRotation, Quaternion.identity) > 0.01f)
        {
            Vector3[] vertices = filter.sharedMesh.vertices;
            if (vertices == null || vertices.Length == 0)
                throw new InvalidOperationException($"Mesh has no vertices on {child.name}.");
            Vector3 first = child.localPosition + child.localRotation * Vector3.Scale(vertices[0], child.localScale);
            Bounds bounds = new Bounds(first, Vector3.zero);
            for (int i = 1; i < vertices.Length; i++)
                bounds.Encapsulate(child.localPosition + child.localRotation * Vector3.Scale(vertices[i], child.localScale));
            return bounds;
        }

        Bounds mb = filter.sharedMesh.bounds;
        Vector3 center = child.localPosition + Vector3.Scale(mb.center, child.localScale);
        Vector3 size = Vector3.Scale(mb.size, Abs(child.localScale));
        return new Bounds(center, size);
    }

    private static Material RequireMaterial(Transform root, string materialName)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        foreach (Material material in renderer.sharedMaterials ?? Array.Empty<Material>())
            if (material != null && material.name == materialName)
                return material;
        throw new InvalidOperationException($"Required slide material missing in {root.name}: {materialName}");
    }

    private static Transform RequireSlide()
    {
        GameObject slide = GameObject.Find(SlideName);
        if (slide == null)
            throw new InvalidOperationException("HD_Slide is missing; park/street furniture must be built before slide access QA.");
        return slide.transform;
    }

    private static Transform RequireDirectChild(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child == null)
            throw new InvalidOperationException($"Missing slide component: {parent.name}/{childName}");
        return child;
    }

    private static void DestroyDirectChildIfPresent(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null) UnityEngine.Object.DestroyImmediate(child.gameObject);
    }

    private static int CountGeneratedRenderers(Transform slide, int lodCount)
    {
        int count = 0;
        for (int lod = 0; lod < lodCount; lod++)
        {
            Transform tier = RequireDirectChild(slide, $"LOD{lod}");
            Transform generated = RequireDirectChild(tier, GeneratedRootName);
            count += generated.GetComponentsInChildren<Renderer>(true).Length;
        }
        return count;
    }

    private static string SideName(int side) => side < 0 ? "L" : "R";
    private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    private static bool IsBuiltInPrimitive(string meshName) =>
        meshName == "Cube" || meshName == "Cylinder" || meshName == "Sphere" ||
        meshName == "Capsule" || meshName == "Plane" || meshName == "Quad";

    private static SlideContract LoadAndValidateContract()
    {
        SlideContract contract = LoadContract();
        List<string> errors = ValidateContract(contract);
        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Slide access installation contract FAILED:\n - " + string.Join("\n - ", errors));
        if (!File.Exists(LookdevPath))
            throw new InvalidOperationException($"Missing required slide access lookdev illustration: {LookdevPath}");
        return contract;
    }

    private static SlideContract LoadContract()
    {
        if (!File.Exists(ContractPath))
            throw new InvalidOperationException($"Missing required slide access construction metadata: {ContractPath}");
        SlideContract contract = JsonUtility.FromJson<SlideContract>(File.ReadAllText(ContractPath));
        if (contract == null)
            throw new InvalidOperationException($"Could not parse slide access metadata: {ContractPath}");
        return contract;
    }

    private static List<string> ValidateContract(SlideContract contract)
    {
        var errors = new List<string>();
        if (contract == null)
        {
            errors.Add("contract is null.");
            return errors;
        }
        if (contract.schemaVersion != "1.0") errors.Add($"schemaVersion must remain 1.0, got {contract.schemaVersion}.");
        if (contract.id != "slide_access_installation_interface") errors.Add("contract id drifted.");
        if (contract.dimensionsMetres == null) errors.Add("dimensionsMetres missing.");
        if (contract.qaRules == null) errors.Add("qaRules missing.");
        if (contract.renderVerification == null) errors.Add("renderVerification missing.");
        if (errors.Count > 0) return errors;

        DimensionsMetres d = contract.dimensionsMetres;
        QaRules q = contract.qaRules;
        if (d.treadCount != 10) errors.Add($"treadCount must remain 10, got {d.treadCount}.");
        if (d.treadDepth < 0.17f || d.treadThickness < 0.0032f)
            errors.Add("tread depth/thickness fell below the locked construction reference.");
        if (d.treadRise > 0.22f || d.treadRise <= 0f)
            errors.Add("treadRise must remain positive and <= 0.22 m.");
        float angle = Mathf.Atan2(d.treadRise, Mathf.Abs(d.treadRun)) * Mathf.Rad2Deg;
        if (angle < 50f || angle > 75f)
            errors.Add($"contract stair angle {angle:0.###} deg is outside 50-75 deg.");
        if (d.stringerDiameter <= 0f || d.handrailDiameter <= 0f ||
            d.headBearerDiameter <= 0f || d.runoutBearerDiameter <= 0f)
            errors.Add("structural tube diameters must be positive.");
        if (d.treadBracketWidth <= 0f || d.treadBracketHeight <= 0f || d.treadBracketDepth <= 0f)
            errors.Add("tread support bracket dimensions must be positive.");

        float lateralClearance =
            d.stringerCenterAbsX - d.stringerDiameter * 0.5f - d.treadWidth * 0.5f;
        if (lateralClearance < q.minimumStringerToTreadLateralClearanceMetres)
            errors.Add(
                $"contract tread/stringer lateral clearance {lateralClearance * 1000f:0.###} mm is below its own " +
                $"{q.minimumStringerToTreadLateralClearanceMetres * 1000f:0.###} mm floor.");

        float bracketMinX = d.treadBracketCenterAbsX - d.treadBracketWidth * 0.5f;
        float bracketMaxX = d.treadBracketCenterAbsX + d.treadBracketWidth * 0.5f;
        if (bracketMinX > d.treadWidth * 0.5f + q.contactToleranceMetres)
            errors.Add("support bracket does not reach the tread edge.");
        if (bracketMaxX < d.stringerCenterAbsX - q.maximumBracketStringerAxisMissMetres)
            errors.Add("support bracket does not reach the stringer axis envelope.");

        if (q.requiredLodCount != 4 || q.generatedColliderCount != 0 || !q.newRenderersMustBeLodBound)
            errors.Add("LOD/collider/binding fail-closed policy drifted.");
        if (q.minimumTreadDepthMetres < 0.17f || q.maximumStepRiseMetres > 0.22f ||
            q.minimumAccessAngleDegrees < 50f || q.maximumAccessAngleDegrees > 75f)
            errors.Add("QA safety/construction limits were relaxed beyond the locked reference.");
        if (q.minimumStringerToTreadLateralClearanceMetres < 0.015f ||
            q.maximumBracketStringerAxisMissMetres > 0.004f)
            errors.Add("tread/stringer separation or bracket/stringer join QA was relaxed beyond the locked interface limits.");
        if (contract.renderVerification.status != "PENDING_UNITY_RUNTIME" ||
            contract.renderVerification.visualFidelityPointsAwarded != 0)
            errors.Add("source contract must remain PENDING_UNITY_RUNTIME with zero visual points.");
        return errors;
    }

    private static void WriteRuntimeReport(ValidationSummary summary, int generatedRenderers, bool lodBindingVerified)
    {
        var report = new RuntimeReport
        {
            schemaVersion = "1.0",
            status = "IMPLEMENTATION_QA_ONLY_VISUAL_UNSCORED",
            generatedRendererCount = generatedRenderers,
            lodBindingVerified = lodBindingVerified,
            accessAngleDegrees = summary.accessAngleDegrees,
            minStringerTreadLateralClearanceMetres = summary.minStringerTreadLateralClearanceMetres,
            maxTreadBracketContactErrorMetres = summary.maxTreadBracketContactErrorMetres,
            maxBracketStringerAxisMissMetres = summary.maxBracketStringerAxisMissMetres,
            maxFootPlateContactErrorMetres = summary.maxFootPlateContactErrorMetres,
            maxTopStepRiseMetres = summary.maxTopStepRiseMetres,
            maxTopHorizontalGapMetres = summary.maxTopHorizontalGapMetres,
            maxHeadBearingGapMetres = summary.maxHeadBearingGapMetres,
            maxRunoutBearingGapMetres = summary.maxRunoutBearingGapMetres,
            maxRunoutLoadPathErrorMetres = summary.maxRunoutLoadPathErrorMetres,
            visualFidelityPointsAwarded = 0,
            renderVerification = "PENDING_NATIVE_3840X2160_AND_100_PERCENT_CROPS"
        };
        File.WriteAllText(RuntimeReportPath, JsonUtility.ToJson(report, true));
        AssetDatabase.ImportAsset(RuntimeReportPath, ImportAssetOptions.ForceUpdate);
    }

    [Serializable]
    private sealed class SlideContract
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
        public int treadCount;
        public float treadWidth;
        public float treadThickness;
        public float treadDepth;
        public float firstTreadCenterY;
        public float treadRise;
        public float firstTreadCenterZ;
        public float treadRun;
        public float stringerDiameter;
        public float stringerCenterAbsX;
        public float stringerLowerY;
        public float stringerLowerZ;
        public float stringerUpperY;
        public float stringerUpperZ;
        public float treadBracketCenterAbsX;
        public float treadBracketWidth;
        public float treadBracketHeight;
        public float treadBracketDepth;
        public float accessFootPlateWidth;
        public float accessFootPlateThickness;
        public float accessFootPlateDepth;
        public float handrailDiameter;
        public float handrailCenterAbsX;
        public float handrailVerticalOffset;
        public float handrailBridgeTargetAbsX;
        public float handrailBridgeTargetZ;
        public float deckTopY;
        public float deckRearEdgeZ;
        public float headBearerDiameter;
        public float headBearerLength;
        public float headBearerCenterY;
        public float headBearerCenterZ;
        public float headTieCenterAbsX;
        public float headTieRearZ;
        public float runoutBearerDiameter;
        public float runoutBearerLength;
        public float runoutBearerCenterY;
        public float runoutBearerCenterZ;
        public float runoutPostDiameter;
        public float runoutPostCenterAbsX;
        public float runoutFootPlateWidth;
        public float runoutFootPlateThickness;
        public float runoutFootPlateDepth;
        public float lod0HeadFastenerDiameter;
        public float lod0HeadFastenerHeight;
    }

    [Serializable]
    private sealed class QaRules
    {
        public float positionToleranceMetres;
        public float contactToleranceMetres;
        public float minimumTreadDepthMetres;
        public float maximumStepRiseMetres;
        public float minimumAccessAngleDegrees;
        public float maximumAccessAngleDegrees;
        public float maximumTopTreadHorizontalGapMetres;
        public float maximumTopTreadVerticalRiseMetres;
        public float maximumFirstTreadRiseMetres;
        public float minimumFirstTreadRiseMetres;
        public float minimumStringerToTreadLateralClearanceMetres;
        public float maximumBracketStringerAxisMissMetres;
        public float maximumChuteBearingGapMetres;
        public int requiredLodCount;
        public int generatedColliderCount;
        public bool newRenderersMustBeLodBound;
    }

    [Serializable]
    private sealed class RenderVerification
    {
        public string status;
        public int visualFidelityPointsAwarded;
    }

    private struct AxisSegment
    {
        public Vector3 a;
        public Vector3 b;
    }

    private sealed class ValidationSummary
    {
        public float accessAngleDegrees;
        public float minStringerTreadLateralClearanceMetres;
        public float maxTreadBracketContactErrorMetres;
        public float maxBracketStringerAxisMissMetres;
        public float maxFootPlateContactErrorMetres;
        public float maxTopStepRiseMetres;
        public float maxTopHorizontalGapMetres;
        public float maxHeadBearingGapMetres;
        public float maxRunoutBearingGapMetres;
        public float maxRunoutLoadPathErrorMetres;
    }

    [Serializable]
    private sealed class RuntimeReport
    {
        public string schemaVersion;
        public string status;
        public int generatedRendererCount;
        public bool lodBindingVerified;
        public float accessAngleDegrees;
        public float minStringerTreadLateralClearanceMetres;
        public float maxTreadBracketContactErrorMetres;
        public float maxBracketStringerAxisMissMetres;
        public float maxFootPlateContactErrorMetres;
        public float maxTopStepRiseMetres;
        public float maxTopHorizontalGapMetres;
        public float maxHeadBearingGapMetres;
        public float maxRunoutBearingGapMetres;
        public float maxRunoutLoadPathErrorMetres;
        public int visualFidelityPointsAwarded;
        public string renderVerification;
    }
}
