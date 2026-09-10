using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Reconstructs WornPathB as a physically legible compacted-soil branch through the park verge.
///
/// The source benchmark uses a 2 x 6 m Unity Cube whose top sits about 45 mm above the turf. It is
/// separated from the ParkPath by roughly 0.53 m of grass while the ParkPathEast precast curb remains
/// continuous, so the object can read as an isolated rectangular soil slab in the oblique foreground.
///
/// This correction preserves the coarse source BoxCollider for gameplay fallback but disables its
/// Renderer. It opens exactly two whole 588 mm precast curb modules, then creates a curved/tapered,
/// collider-free compacted-soil ribbon that begins 3 mm below the paved path, crosses the deliberate
/// 1.212 m curb opening, retains a roughly 40 mm compacted wearing course through the verge, feathers
/// its shoulders down to turf grade, and tapers back into the grass. Four authored LOD meshes retain
/// the same construction silhouette. UV0 is expressed in world metres and the copied PBR material is
/// retiled in metres rather than restarting a normalized primitive UV domain.
///
/// This class provides implementation/readiness evidence only. It never awards Visual Fidelity points;
/// actual 3840x2160 full frames, 100% crops and temporal evidence remain mandatory for scoring.
/// </summary>
public static class QualityBlockGroundWornPathBTransitionQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/ground_worn_path_b_transition_contract.json";
    private const string LookdevPath = "Assets/QA/ground_worn_path_b_transition_lookdev.svg";
    private const string SourcePathName = "WornPathB";
    private const string ParkPathName = "ParkPath";
    private const string StateName = "GroundWornPathBTransitionState";
    private const string LodRootName = "HD_WornPathB_Transition_LOD";
    private const string CurbPrefix = "HD_Curb_ParkPathEast_";
    private const string GeneratedRoot = "Assets/Art/GeneratedGroundInterfaces/WornPathB";
    private const string MeshRoot = GeneratedRoot + "/Meshes";
    private const string MaterialRoot = GeneratedRoot + "/Materials";
    private const string MaterialPath = MaterialRoot + "/PBR_WornPathB_Transition.mat";

    private const int OpeningFirstIndex = 26;
    private const int OpeningLastIndex = 27;
    private const int PreviousCurbIndex = 25;
    private const int NextCurbIndex = 28;
    private const int CurbCount = 36;

    private const float MacroTileMeters = 6.4f;
    private const float DetailTileMeters = 0.30f;
    private const float PositionTolerance = 0.018f;

    private static readonly int[] LongitudinalSegments = { 48, 28, 16, 8 };
    private static readonly int[] LateralStationCounts = { 7, 7, 5, 5 };
    private static readonly float[] LodHeights = { 0.58f, 0.32f, 0.16f, 0.055f };

    private static readonly Vector3 P0 = new Vector3(11.30f, 0f, 5.40f);
    private static readonly Vector3 P1 = new Vector3(12.15f, 0f, 5.40f);
    private static readonly Vector3 P2 = new Vector3(12.55f, 0f, 8.30f);
    private static readonly Vector3 P3 = new Vector3(13.25f, 0f, 10.35f);

    [MenuItem("NewTown/Geometry/Apply WornPathB Park-Verge Transition")]
    public static void ApplyAndPersist()
    {
        ValidateContractConfigOnly();
        RequireQualityScene();
        ApplyToOpenScene();
        ValidateOpenScene();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            "WornPathB park-verge transition persisted: source primitive hidden, two-module curb opening, " +
            "metric-UV compacted-soil ribbon and four construction-preserving LODs. Visual Fidelity remains UNSCORED.");
    }

    public static void ApplyToOpenScene()
    {
        ValidateContractConfigOnly();
        RequireQualityScene();
        TransitionContract contract = LoadContract();

        Renderer sourcePath = RequireRenderer(SourcePathName);
        Collider sourceCollider = sourcePath.GetComponent<Collider>();
        if (sourceCollider == null || !sourceCollider.enabled)
            throw new InvalidOperationException("WornPathB gameplay fallback collider is missing or disabled.");
        if (sourcePath.sharedMaterial == null)
            throw new InvalidOperationException("WornPathB source PBR material is missing before visual replacement.");

        // Preserve the coarse collision substrate, but never allow the rectangular primitive into pixels.
        sourcePath.enabled = false;
        EditorUtility.SetDirty(sourcePath);

        ApplyCurbOpening();

        GameObject previous = FindSceneObject(StateName);
        if (previous != null)
            UnityEngine.Object.DestroyImmediate(previous);

        GameObject ground = FindSceneObject("Ground");
        if (ground == null)
            throw new InvalidOperationException("Ground root missing for WornPathB transition state.");
        RequireIdentityWorldTransform(ground.transform, "Ground");

        var state = new GameObject(StateName);
        state.transform.SetParent(ground.transform, false);

        Directory.CreateDirectory(MeshRoot);
        Directory.CreateDirectory(MaterialRoot);
        Material material = EnsureMaterial(sourcePath.sharedMaterial, contract);

        var lodRoot = new GameObject(LodRootName);
        lodRoot.transform.SetParent(state.transform, false);
        var lodGroup = lodRoot.AddComponent<LODGroup>();
        lodGroup.fadeMode = LODFadeMode.CrossFade;
        lodGroup.animateCrossFading = true;

        var lods = new LOD[4];
        for (int i = 0; i < 4; i++)
        {
            string childName = $"HD_WornPathB_Transition_LOD{i}";
            var child = new GameObject(childName);
            child.transform.SetParent(lodRoot.transform, false);

            Mesh mesh = EnsureMesh(i, LongitudinalSegments[i], LateralStationCounts[i], contract);
            var filter = child.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.receiveShadows = true;

            var weathering = child.AddComponent<QualityBlockWeatheringSurface>();
            weathering.Configure(
                NewTownSurfaceExposure.SunExposed | NewTownSurfaceExposure.RainExposed |
                NewTownSurfaceExposure.GroundContact | NewTownSurfaceExposure.UpwardFacing,
                NewTownStainSource.FootTraffic | NewTownStainSource.UVExposure,
                1f, 0.95f, 0.18f, 0.72f);

            lods[i] = new LOD(LodHeights[i], new Renderer[] { renderer })
            {
                fadeTransitionWidth = 0.12f
            };
        }
        lodGroup.SetLODs(lods);
        lodGroup.RecalculateBounds();

        var manifest = state.AddComponent<QualityBlockGroundWornPathBTransitionManifest>();
        manifest.Configure(
            OpeningFirstIndex,
            OpeningLastIndex,
            contract.dimensions.curbOpeningWidthM,
            contract.dimensions.parkPavingTopYM - contract.dimensions.pathMouthTopYM,
            contract.dimensions.compactedCoreTopYM - contract.dimensions.grassTopYM,
            contract.dimensions.shoulderJoinTopYM - contract.dimensions.grassTopYM,
            true,
            true,
            true);
        EditorUtility.SetDirty(manifest);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    [MenuItem("NewTown/QA/Validate WornPathB Park-Verge Transition")]
    public static void ValidateOpenScene()
    {
        ValidateContractConfigOnly();
        RequireQualityScene();
        TransitionContract contract = LoadContract();

        Renderer sourcePath = RequireRenderer(SourcePathName);
        Renderer parkPath = RequireRenderer(ParkPathName);
        Bounds sourceBounds = sourcePath.bounds;
        Bounds parkBounds = parkPath.bounds;

        AssertNear(sourceBounds.min.x, contract.sourceAudit.sourceMinXM, PositionTolerance, "source min X");
        AssertNear(sourceBounds.max.x, contract.sourceAudit.sourceMaxXM, PositionTolerance, "source max X");
        AssertNear(sourceBounds.min.z, contract.sourceAudit.sourceMinZM, PositionTolerance, "source min Z");
        AssertNear(sourceBounds.max.z, contract.sourceAudit.sourceMaxZM, PositionTolerance, "source max Z");
        AssertNear(sourceBounds.max.y, contract.sourceAudit.sourceTopYM, PositionTolerance, "source top Y");
        AssertNear(parkBounds.max.x, contract.dimensions.parkPathEastEdgeXM, PositionTolerance, "ParkPath east edge");
        AssertNear(parkBounds.max.y, contract.dimensions.parkPavingTopYM, PositionTolerance, "ParkPath paving top");

        if (sourcePath.enabled)
            throw new InvalidOperationException("Coarse WornPathB primitive Renderer must remain disabled for benchmark evidence.");
        Collider sourceCollider = sourcePath.GetComponent<Collider>();
        if (sourceCollider == null || !sourceCollider.enabled)
            throw new InvalidOperationException("WornPathB gameplay fallback collider must remain enabled.");

        ValidateCurbOpening(contract);

        GameObject state = FindSceneObject(StateName);
        if (state == null)
            throw new InvalidOperationException("WornPathB transition state is missing.");
        if (state.transform.parent == null || state.transform.parent.name != "Ground")
            throw new InvalidOperationException("WornPathB transition state must inherit the Ground metadata domain.");
        if (state.GetComponentsInChildren<Collider>(true).Length != 0)
            throw new InvalidOperationException("WornPathB visual correction must remain collider-free.");

        GameObject lodRoot = FindSceneObject(LodRootName);
        if (lodRoot == null || !lodRoot.transform.IsChildOf(state.transform))
            throw new InvalidOperationException("WornPathB transition LOD root is missing or outside its state scope.");
        LODGroup group = lodRoot.GetComponent<LODGroup>();
        if (group == null)
            throw new InvalidOperationException("WornPathB transition LODGroup is missing.");
        LOD[] lods = group.GetLODs();
        if (lods.Length != 4)
            throw new InvalidOperationException($"WornPathB transition requires LOD0/1/2/3, got {lods.Length} levels.");
        if (group.fadeMode != LODFadeMode.CrossFade || !group.animateCrossFading)
            throw new InvalidOperationException("WornPathB transition LODs must use animated cross-fade.");

        int previousVertexCount = int.MaxValue;
        Bounds? referenceBounds = null;
        Material referenceMaterial = null;
        for (int i = 0; i < 4; i++)
        {
            if (lods[i].renderers == null || lods[i].renderers.Length != 1 || lods[i].renderers[0] == null)
                throw new InvalidOperationException($"WornPathB LOD{i} must contain exactly one renderer.");
            Renderer renderer = lods[i].renderers[0];
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
                throw new InvalidOperationException($"WornPathB LOD{i} mesh is missing.");
            if (!filter.sharedMesh.name.StartsWith("GM_HD_Interface_WornPathB_", StringComparison.Ordinal))
                throw new InvalidOperationException($"WornPathB LOD{i} reverted to non-authored/primitive mesh {filter.sharedMesh.name}.");
            if (filter.sharedMesh.vertexCount >= previousVertexCount)
                throw new InvalidOperationException("WornPathB LOD vertex counts must decrease strictly from LOD0 through LOD3.");
            previousVertexCount = filter.sharedMesh.vertexCount;

            Material material = renderer.sharedMaterial;
            if (material == null || material.name != "PBR_WornPathB_Transition")
                throw new InvalidOperationException($"WornPathB LOD{i} material binding is invalid.");
            if (referenceMaterial == null) referenceMaterial = material;
            else if (material != referenceMaterial)
                throw new InvalidOperationException("All WornPathB LODs must share the same physical material instance.");

            if (referenceBounds == null) referenceBounds = renderer.bounds;
            else
            {
                Bounds b = renderer.bounds;
                if ((b.center - referenceBounds.Value.center).magnitude > contract.hardLimits.maxLodBoundsCenterDriftM ||
                    (b.size - referenceBounds.Value.size).magnitude > contract.hardLimits.maxLodBoundsSizeDriftM)
                    throw new InvalidOperationException($"WornPathB LOD{i} silhouette bounds drift beyond hard limit.");
            }

            if (renderer.GetComponent<QualityBlockWeatheringSurface>() == null)
                throw new InvalidOperationException($"WornPathB LOD{i} is missing cause-based weathering metadata.");
        }

        ValidateMaterial(referenceMaterial, contract);

        QualityBlockGroundWornPathBTransitionManifest manifest =
            state.GetComponent<QualityBlockGroundWornPathBTransitionManifest>();
        if (manifest == null || !manifest.SourceRendererDisabled || !manifest.SourceColliderPreserved ||
            !manifest.VisualCorrectionColliderFree)
            throw new InvalidOperationException("WornPathB transition manifest does not prove source/visual responsibility separation.");
        if (manifest.OpeningFirstIndex != OpeningFirstIndex || manifest.OpeningLastIndex != OpeningLastIndex)
            throw new InvalidOperationException("WornPathB curb-opening manifest index drift.");
        AssertNear(manifest.CurbOpeningWidthM, contract.dimensions.curbOpeningWidthM, 0.002f, "manifest curb opening width");
        AssertNear(manifest.MouthBelowPavingM,
            contract.dimensions.parkPavingTopYM - contract.dimensions.pathMouthTopYM, 0.001f,
            "manifest path mouth below paving");

        Debug.Log(
            "WornPathB transition validation passed structurally: rectangular source renderer suppressed with collider preserved; " +
            "1.212 m two-module curb opening; curved/tapered compacted-soil ribbon; metre UVs; four cross-faded LODs; " +
            "dry dielectric material and cause-based foot-traffic/UV metadata. Pixel realism remains render-unverified and UNSCORED.");
    }

    [MenuItem("NewTown/QA/Validate WornPathB Transition Contract Only")]
    public static void ValidateContractConfigOnly()
    {
        if (!File.Exists(ContractPath))
            throw new InvalidOperationException($"WornPathB transition contract missing: {ContractPath}");
        if (!File.Exists(LookdevPath))
            throw new InvalidOperationException($"WornPathB transition lookdev missing: {LookdevPath}");

        TransitionContract contract = LoadContract();
        if (contract.runtimeRenderVerified)
            throw new InvalidOperationException("WornPathB source-side contract cannot claim runtime render verification.");
        if (contract.visualFidelityPointsAwarded != 0)
            throw new InvalidOperationException("WornPathB source-side QA cannot award Visual Fidelity points.");
        RequireText(contract.visualFidelityStatus, "visualFidelityStatus");
        RequireText(contract.targetPeriod, "targetPeriod");
        RequireText(contract.targetRegion, "targetRegion");

        if (contract.sourceAudit == null || contract.assembly == null || contract.dimensions == null ||
            contract.material == null || contract.weathering == null || contract.lodPolicy == null ||
            contract.hardLimits == null)
            throw new InvalidOperationException("WornPathB contract is missing required source/construction/material/LOD sections.");

        RequireText(contract.sourceAudit.visualRisk, "sourceAudit.visualRisk");
        if (contract.sourceAudit.legacyGrassGapToParkCurbM < 0.45f)
            throw new InvalidOperationException("WornPathB contract no longer demonstrates the isolated source-path gap being corrected.");
        if (contract.sourceAudit.sourceWidthM < 1.9f || contract.sourceAudit.sourceLengthM < 5.9f)
            throw new InvalidOperationException("WornPathB source audit dimensions drifted.");

        RequireText(contract.assembly.manufacture, "assembly.manufacture");
        RequireText(contract.assembly.mounting, "assembly.mounting");
        RequireText(contract.assembly.interfacesGapsSeals, "assembly.interfacesGapsSeals");
        RequireText(contract.assembly.orientationExposure, "assembly.orientationExposure");
        RequireText(contract.assembly.aging, "assembly.aging");
        RequireText(contract.assembly.geometryVsMaterial, "assembly.geometryVsMaterial");
        if (contract.assembly.components == null || contract.assembly.components.Length < 5)
            throw new InvalidOperationException("WornPathB assembly must enumerate at least five physical/source components.");

        AssertNear(contract.dimensions.mouthCenterXM, P0.x, 0.001f, "contract mouth center X");
        AssertNear(contract.dimensions.mouthCenterZM, P0.z, 0.001f, "contract mouth center Z");
        AssertNear(contract.dimensions.terminalCenterXM, P3.x, 0.001f, "contract terminal center X");
        AssertNear(contract.dimensions.terminalCenterZM, P3.z, 0.001f, "contract terminal center Z");
        if (contract.dimensions.pathMouthTopYM >= contract.dimensions.parkPavingTopYM ||
            contract.dimensions.parkPavingTopYM - contract.dimensions.pathMouthTopYM > 0.006f)
            throw new InvalidOperationException("WornPathB mouth must sit 1-6 mm below the ParkPath paving top.");
        if (contract.dimensions.curbOpeningFirstIndex != OpeningFirstIndex ||
            contract.dimensions.curbOpeningLastIndex != OpeningLastIndex)
            throw new InvalidOperationException("WornPathB curb-opening indices drifted.");
        AssertNear(contract.dimensions.curbOpeningWidthM, 1.212f, 0.003f, "curb opening width");
        if (contract.dimensions.compactedCoreThicknessM < 0.030f || contract.dimensions.compactedCoreThicknessM > 0.060f)
            throw new InvalidOperationException("WornPathB compacted wearing-course thickness must remain 30-60 mm.");
        if (contract.dimensions.shoulderJoinTopYM - contract.dimensions.grassTopYM > 0.006f)
            throw new InvalidOperationException("WornPathB feathered shoulder must return to turf grade within 6 mm.");

        RequireText(contract.material.family, "material.family");
        RequireText(contract.material.finish, "material.finish");
        RequireText(contract.material.microstructure, "material.microstructure");
        RequireText(contract.material.uvAging, "material.uvAging");
        RequireText(contract.material.angularFresnelResponse, "material.angularFresnelResponse");
        if (contract.material.metallic > contract.hardLimits.maxMetallic)
            throw new InvalidOperationException("WornPathB compacted soil must remain dielectric/non-metallic.");
        if (contract.material.roughnessMin < 0.85f || contract.material.roughnessMax > 1.0f ||
            contract.material.roughnessMin > contract.material.roughnessMax)
            throw new InvalidOperationException("WornPathB soil roughness contract is implausible.");
        AssertNear(contract.material.macroTileMeters, MacroTileMeters, 0.001f, "macro physical tile");
        AssertNear(contract.material.detailTileMeters, DetailTileMeters, 0.001f, "detail physical tile");
        if (contract.material.wetness != 0f)
            throw new InvalidOperationException("Dry midsummer benchmark WornPathB wetness must remain zero.");

        RequireText(contract.weathering.causes, "weathering.causes");
        RequireText(contract.weathering.forbidden, "weathering.forbidden");
        RequireText(contract.lodPolicy.lod0, "lodPolicy.lod0");
        RequireText(contract.lodPolicy.lod1, "lodPolicy.lod1");
        RequireText(contract.lodPolicy.lod2, "lodPolicy.lod2");
        RequireText(contract.lodPolicy.lod3, "lodPolicy.lod3");
        RequireText(contract.lodPolicy.transitionPolicy, "lodPolicy.transitionPolicy");
        if (contract.evidencePlan == null || contract.evidencePlan.Length < 4)
            throw new InvalidOperationException("WornPathB contract requires still/crop/temporal evidence plans.");
        if (contract.criticalFailPrevention == null || contract.criticalFailPrevention.Length < 7)
            throw new InvalidOperationException("WornPathB contract requires explicit critical-fail prevention rules.");
    }

    private static void ApplyCurbOpening()
    {
        for (int i = 0; i < CurbCount; i++)
        {
            GameObject go = FindSceneObject(CurbPrefix + i.ToString("00"));
            if (go == null)
                throw new InvalidOperationException($"Expected ParkPathEast curb module missing: {CurbPrefix}{i:00}");
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (renderer == null)
                throw new InvalidOperationException($"ParkPathEast curb module has no MeshRenderer: {go.name}");
            renderer.enabled = i < OpeningFirstIndex || i > OpeningLastIndex;
            EditorUtility.SetDirty(renderer);
        }
    }

    private static void ValidateCurbOpening(TransitionContract contract)
    {
        for (int i = 0; i < CurbCount; i++)
        {
            GameObject go = FindSceneObject(CurbPrefix + i.ToString("00"));
            if (go == null)
                throw new InvalidOperationException($"Expected ParkPathEast curb module missing: {CurbPrefix}{i:00}");
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            MeshFilter filter = go.GetComponent<MeshFilter>();
            if (renderer == null || filter == null || filter.sharedMesh == null)
                throw new InvalidOperationException($"ParkPathEast curb module is structurally incomplete: {go.name}");
            bool shouldRender = i < OpeningFirstIndex || i > OpeningLastIndex;
            if (renderer.enabled != shouldRender)
                throw new InvalidOperationException($"ParkPathEast curb module visibility drift at index {i}.");
            if (shouldRender && !filter.sharedMesh.name.StartsWith("GM_HD_", StringComparison.Ordinal))
                throw new InvalidOperationException($"Visible ParkPathEast curb module {i} reverted to primitive mesh {filter.sharedMesh.name}.");
        }

        Renderer first = RequireRenderer(CurbPrefix + OpeningFirstIndex.ToString("00"));
        Renderer second = RequireRenderer(CurbPrefix + OpeningLastIndex.ToString("00"));
        Renderer previous = RequireRenderer(CurbPrefix + PreviousCurbIndex.ToString("00"));
        Renderer next = RequireRenderer(CurbPrefix + NextCurbIndex.ToString("00"));
        AssertNear(first.bounds.center.z, contract.dimensions.firstOpeningModuleCenterZM, PositionTolerance, "first opening module center Z");
        AssertNear(second.bounds.center.z, contract.dimensions.lastOpeningModuleCenterZM, PositionTolerance, "last opening module center Z");

        float openMin = previous.bounds.max.z;
        float openMax = next.bounds.min.z;
        float opening = openMax - openMin;
        AssertNear(openMin, contract.dimensions.curbOpeningMinZM, PositionTolerance, "curb opening min Z");
        AssertNear(openMax, contract.dimensions.curbOpeningMaxZM, PositionTolerance, "curb opening max Z");
        AssertNear(opening, contract.dimensions.curbOpeningWidthM, 0.024f, "curb opening measured width");
    }

    private static Mesh EnsureMesh(int lod, int longitudinalSegments, int lateralCount, TransitionContract contract)
    {
        string path = $"{MeshRoot}/GM_HD_Interface_WornPathB_LOD{lod}.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.name = $"GM_HD_Interface_WornPathB_LOD{lod}";
            AssetDatabase.CreateAsset(mesh, path);
        }
        else
        {
            mesh.Clear();
            mesh.name = $"GM_HD_Interface_WornPathB_LOD{lod}";
        }

        float[] stations = lateralCount == 7
            ? new[] { -1f, -0.78f, -0.52f, 0f, 0.52f, 0.78f, 1f }
            : new[] { -1f, -0.62f, 0f, 0.62f, 1f };
        var vertices = new List<Vector3>((longitudinalSegments + 1) * stations.Length);
        var uv = new List<Vector2>(vertices.Capacity);
        var triangles = new List<int>(longitudinalSegments * (stations.Length - 1) * 6);

        for (int z = 0; z <= longitudinalSegments; z++)
        {
            float t = z / (float)longitudinalSegments;
            Vector3 center = Bezier(t);
            Vector3 tangent = BezierTangent(t);
            tangent.y = 0f;
            if (tangent.sqrMagnitude < 0.000001f)
                throw new InvalidOperationException("WornPathB spline produced a degenerate tangent.");
            tangent.Normalize();
            Vector3 lateral = new Vector3(-tangent.z, 0f, tangent.x);
            float width = WidthAt(t, contract);
            float centerTop = CenterTopAt(t, contract);
            float outerTop = OuterTopAt(t, contract);

            for (int x = 0; x < stations.Length; x++)
            {
                float s = stations[x];
                float a = Mathf.Abs(s);
                float y = centerTop;
                if (a > 0.52f)
                {
                    float edgeBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.52f, 1f, a));
                    y = Mathf.Lerp(centerTop, outerTop, edgeBlend);
                }
                else
                {
                    float tread = 1f - Mathf.Clamp01(a / 0.52f);
                    y -= contract.dimensions.centerWearDepressionM * tread * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t * 1.15f));
                }

                // Millimetre-scale long-wave settlement follows the walking direction. It is geometry,
                // not a painted highlight, and fades at the entry/terminal interfaces.
                float settleEnvelope = Mathf.Sin(Mathf.PI * t);
                y += 0.0012f * Mathf.Sin(t * Mathf.PI * 5.0f + s * 0.7f) * settleEnvelope * (1f - 0.35f * a);

                Vector3 p = center + lateral * (0.5f * width * s);
                p.y = y;
                vertices.Add(p);
                uv.Add(new Vector2(p.x, p.z));
            }
        }

        int row = stations.Length;
        for (int z = 0; z < longitudinalSegments; z++)
        {
            int a0 = z * row;
            int b0 = (z + 1) * row;
            for (int x = 0; x < row - 1; x++)
            {
                int a = a0 + x;
                int b = a0 + x + 1;
                int c = b0 + x;
                int d = b0 + x + 1;
                // Winding selected for upward-facing normals in Unity's left-handed scene convention.
                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(b);
                triangles.Add(b);
                triangles.Add(c);
                triangles.Add(d);
            }
        }

        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(triangles, 0, true);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    private static Material EnsureMaterial(Material source, TransitionContract contract)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(source) { name = "PBR_WornPathB_Transition" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else
        {
            material.CopyPropertiesFromMaterial(source);
            material.shader = source.shader;
            material.name = "PBR_WornPathB_Transition";
        }

        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.06f);
        if (material.HasProperty("_BumpScale")) material.SetFloat("_BumpScale", contract.material.normalScale);
        if (material.HasProperty("_DetailNormalMapScale")) material.SetFloat("_DetailNormalMapScale", 1f);
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
        material.DisableKeyword("_EMISSION");

        SetMetricTextureTransform(material, "_MainTex", MacroTileMeters);
        SetMetricTextureTransform(material, "_BumpMap", MacroTileMeters);
        SetMetricTextureTransform(material, "_MetallicGlossMap", MacroTileMeters);
        SetMetricTextureTransform(material, "_DetailAlbedoMap", DetailTileMeters);
        SetMetricTextureTransform(material, "_DetailNormalMap", DetailTileMeters);

        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ValidateMaterial(Material material, TransitionContract contract)
    {
        if (material == null)
            throw new InvalidOperationException("WornPathB transition material is missing.");
        if (material.shader == null || material.shader.name != "Standard")
            throw new InvalidOperationException("WornPathB transition must use the Standard PBR shader in this benchmark.");
        if (material.HasProperty("_Metallic") && material.GetFloat("_Metallic") > contract.hardLimits.maxMetallic)
            throw new InvalidOperationException("WornPathB transition has materially impossible metallic response.");
        if (material.IsKeywordEnabled("_EMISSION"))
            throw new InvalidOperationException("WornPathB dry soil may not use emissive/baked-light output.");
        if (material.GetTexture("_MainTex") == null || material.GetTexture("_BumpMap") == null ||
            material.GetTexture("_MetallicGlossMap") == null || material.GetTexture("_DetailAlbedoMap") == null ||
            material.GetTexture("_DetailNormalMap") == null)
            throw new InvalidOperationException("WornPathB transition material is missing required macro/detail PBR textures.");

        AssertTextureScale(material, "_MainTex", 1f / MacroTileMeters, contract.hardLimits.maxUvScaleError);
        AssertTextureScale(material, "_BumpMap", 1f / MacroTileMeters, contract.hardLimits.maxUvScaleError);
        AssertTextureScale(material, "_MetallicGlossMap", 1f / MacroTileMeters, contract.hardLimits.maxUvScaleError);
        AssertTextureScale(material, "_DetailAlbedoMap", 1f / DetailTileMeters, contract.hardLimits.maxUvScaleError);
        AssertTextureScale(material, "_DetailNormalMap", 1f / DetailTileMeters, contract.hardLimits.maxUvScaleError);
        if (material.HasProperty("_BumpScale"))
            AssertNear(material.GetFloat("_BumpScale"), contract.material.normalScale, 0.02f, "soil normal scale");
    }

    private static void SetMetricTextureTransform(Material material, string property, float tileMeters)
    {
        if (!material.HasProperty(property)) return;
        float scale = 1f / tileMeters;
        material.SetTextureScale(property, new Vector2(scale, scale));
        material.SetTextureOffset(property, Vector2.zero);
    }

    private static void AssertTextureScale(Material material, string property, float expected, float tolerance)
    {
        Vector2 scale = material.GetTextureScale(property);
        if (Mathf.Abs(scale.x - expected) > tolerance || Mathf.Abs(scale.y - expected) > tolerance)
            throw new InvalidOperationException(
                $"WornPathB {property} metric scale drift: {scale}; expected ({expected:F5},{expected:F5}).");
    }

    private static float WidthAt(float t, TransitionContract contract)
    {
        if (t <= 0.18f)
            return Mathf.Lerp(contract.dimensions.mouthWidthM, contract.dimensions.midWidthM,
                Mathf.SmoothStep(0f, 1f, t / 0.18f));
        if (t <= 0.68f)
            return contract.dimensions.midWidthM;
        return Mathf.Lerp(contract.dimensions.midWidthM, contract.dimensions.terminalWidthM,
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.68f, 1f, t)));
    }

    private static float CenterTopAt(float t, TransitionContract contract)
    {
        float entry = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.18f));
        float y = Mathf.Lerp(contract.dimensions.pathMouthTopYM, contract.dimensions.compactedCoreTopYM, entry);
        float terminal = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.84f, 1f, t));
        return Mathf.Lerp(y, contract.dimensions.shoulderJoinTopYM + 0.002f, terminal);
    }

    private static float OuterTopAt(float t, TransitionContract contract)
    {
        float entry = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.18f));
        return Mathf.Lerp(contract.dimensions.pathMouthTopYM, contract.dimensions.shoulderJoinTopYM, entry);
    }

    private static Vector3 Bezier(float t)
    {
        float u = 1f - t;
        return u * u * u * P0 + 3f * u * u * t * P1 + 3f * u * t * t * P2 + t * t * t * P3;
    }

    private static Vector3 BezierTangent(float t)
    {
        float u = 1f - t;
        return 3f * u * u * (P1 - P0) + 6f * u * t * (P2 - P1) + 3f * t * t * (P3 - P2);
    }

    private static TransitionContract LoadContract()
    {
        TransitionContract contract = JsonUtility.FromJson<TransitionContract>(File.ReadAllText(ContractPath));
        if (contract == null)
            throw new InvalidOperationException("WornPathB transition contract could not be parsed.");
        return contract;
    }

    private static void RequireQualityScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x.scene.IsValid() && x.name == name);
    }

    private static Renderer RequireRenderer(string name)
    {
        GameObject go = FindSceneObject(name);
        if (go == null)
            throw new InvalidOperationException($"Required scene object missing: {name}");
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer == null)
            throw new InvalidOperationException($"Required Renderer missing: {name}");
        return renderer;
    }

    private static void RequireIdentityWorldTransform(Transform transform, string label)
    {
        if (transform.position.sqrMagnitude > 0.000001f || Quaternion.Angle(transform.rotation, Quaternion.identity) > 0.01f ||
            (transform.lossyScale - Vector3.one).magnitude > 0.0001f)
            throw new InvalidOperationException($"{label} must retain identity world transform for metric world-UV interface geometry.");
    }

    private static void AssertNear(float actual, float expected, float tolerance, string label)
    {
        if (Mathf.Abs(actual - expected) > tolerance)
            throw new InvalidOperationException($"{label} drift: {actual:F4} m/code; expected {expected:F4} ± {tolerance:F4}.");
    }

    private static void RequireText(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"WornPathB contract missing {label}.");
    }

    [Serializable] private sealed class TransitionContract
    {
        public string schemaVersion;
        public string visualFidelityStatus;
        public string targetPeriod;
        public string targetRegion;
        public bool runtimeRenderVerified;
        public int visualFidelityPointsAwarded;
        public SourceAudit sourceAudit;
        public AssemblySpec assembly;
        public DimensionSpec dimensions;
        public MaterialSpec material;
        public WeatheringSpec weathering;
        public LodPolicy lodPolicy;
        public HardLimits hardLimits;
        public string[] evidencePlan;
        public string[] criticalFailPrevention;
    }

    [Serializable] private sealed class SourceAudit
    {
        public float sourceWidthM;
        public float sourceLengthM;
        public float sourceMinXM;
        public float sourceMaxXM;
        public float sourceMinZM;
        public float sourceMaxZM;
        public float sourceTopYM;
        public float legacyGrassGapToParkCurbM;
        public string visualRisk;
    }

    [Serializable] private sealed class AssemblySpec
    {
        public string[] components;
        public string manufacture;
        public string mounting;
        public string interfacesGapsSeals;
        public string orientationExposure;
        public string aging;
        public string geometryVsMaterial;
    }

    [Serializable] private sealed class DimensionSpec
    {
        public float grassTopYM;
        public float parkPavingTopYM;
        public float parkPathEastEdgeXM;
        public int curbOpeningFirstIndex;
        public int curbOpeningLastIndex;
        public float firstOpeningModuleCenterZM;
        public float lastOpeningModuleCenterZM;
        public float curbOpeningMinZM;
        public float curbOpeningMaxZM;
        public float curbOpeningWidthM;
        public float mouthCenterXM;
        public float mouthCenterZM;
        public float pathMouthTopYM;
        public float mouthWidthM;
        public float midWidthM;
        public float terminalCenterXM;
        public float terminalCenterZM;
        public float terminalWidthM;
        public float compactedCoreTopYM;
        public float compactedCoreThicknessM;
        public float shoulderJoinTopYM;
        public float centerWearDepressionM;
    }

    [Serializable] private sealed class MaterialSpec
    {
        public string family;
        public string finish;
        public float albedoMin;
        public float albedoMax;
        public float roughnessMin;
        public float roughnessMax;
        public float metallic;
        public float specularF0;
        public float normalScale;
        public string microstructure;
        public float wetness;
        public string uvAging;
        public string angularFresnelResponse;
        public float macroTileMeters;
        public float detailTileMeters;
    }

    [Serializable] private sealed class WeatheringSpec
    {
        public string causes;
        public string forbidden;
    }

    [Serializable] private sealed class LodPolicy
    {
        public string lod0;
        public string lod1;
        public string lod2;
        public string lod3;
        public string transitionPolicy;
    }

    [Serializable] private sealed class HardLimits
    {
        public float maxMetallic;
        public float maxUvScaleError;
        public float maxLodBoundsCenterDriftM;
        public float maxLodBoundsSizeDriftM;
    }
}

public sealed class QualityBlockGroundWornPathBTransitionManifest : MonoBehaviour
{
    [SerializeField] private int openingFirstIndex;
    [SerializeField] private int openingLastIndex;
    [SerializeField] private float curbOpeningWidthM;
    [SerializeField] private float mouthBelowPavingM;
    [SerializeField] private float compactedCoreAboveGrassM;
    [SerializeField] private float shoulderJoinAboveGrassM;
    [SerializeField] private bool sourceRendererDisabled;
    [SerializeField] private bool sourceColliderPreserved;
    [SerializeField] private bool visualCorrectionColliderFree;

    public int OpeningFirstIndex => openingFirstIndex;
    public int OpeningLastIndex => openingLastIndex;
    public float CurbOpeningWidthM => curbOpeningWidthM;
    public float MouthBelowPavingM => mouthBelowPavingM;
    public float CompactedCoreAboveGrassM => compactedCoreAboveGrassM;
    public float ShoulderJoinAboveGrassM => shoulderJoinAboveGrassM;
    public bool SourceRendererDisabled => sourceRendererDisabled;
    public bool SourceColliderPreserved => sourceColliderPreserved;
    public bool VisualCorrectionColliderFree => visualCorrectionColliderFree;

    public void Configure(int firstIndex, int lastIndex, float openingWidth, float mouthBelowPaving,
        float coreAboveGrass, float shoulderAboveGrass, bool rendererDisabled, bool colliderPreserved,
        bool correctionColliderFree)
    {
        openingFirstIndex = firstIndex;
        openingLastIndex = lastIndex;
        curbOpeningWidthM = openingWidth;
        mouthBelowPavingM = mouthBelowPaving;
        compactedCoreAboveGrassM = coreAboveGrass;
        shoulderJoinAboveGrassM = shoulderAboveGrass;
        sourceRendererDisabled = rendererDisabled;
        sourceColliderPreserved = colliderPreserved;
        visualCorrectionColliderFree = correctionColliderFree;
    }
}
