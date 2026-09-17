using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Reconstructs WornPathB as a physically legible compacted-soil branch through the park verge.
/// The rectangular source Renderer is suppressed but its coarse BoxCollider is preserved. Two whole
/// ParkPathEast curb modules form a deliberate opening; a curved/tapered metric-UV soil ribbon then
/// feathers to turf grade through LOD0/1/2/3. This source-side QA never awards Visual Fidelity points.
/// </summary>
public static class QualityBlockGroundWornPathBTransitionQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/ground_worn_path_b_transition_contract.json";
    private const string LookdevPath = "Assets/QA/ground_worn_path_b_transition_lookdev.svg";
    private const string SourceName = "WornPathB";
    private const string StateName = "GroundWornPathBTransitionState";
    private const string LodRootName = "HD_WornPathB_Transition_LOD";
    private const string CurbPrefix = "HD_Curb_ParkPathEast_";
    private const string GeneratedRoot = "Assets/Art/GeneratedGroundInterfaces/WornPathB";
    private const string MeshRoot = GeneratedRoot + "/Meshes";
    private const string MaterialRoot = GeneratedRoot + "/Materials";
    private const string MaterialPath = MaterialRoot + "/PBR_WornPathB_Transition.mat";
    private const int OpeningFirst = 26;
    private const int OpeningLast = 27;
    private const int CurbCount = 36;
    private const float MacroTileM = 6.4f;
    private const float DetailTileM = 0.30f;
    private const float PosTol = 0.018f;

    private static readonly int[] LongSegments = { 48, 28, 16, 8 };
    private static readonly int[] CrossCounts = { 7, 7, 5, 5 };
    private static readonly float[] LodScreen = { 0.58f, 0.32f, 0.16f, 0.055f };
    private static readonly Vector3 P0 = new Vector3(11.30f, 0f, 5.40f);
    private static readonly Vector3 P1 = new Vector3(12.45f, 0f, 5.40f);
    private static readonly Vector3 P2 = new Vector3(12.55f, 0f, 8.30f);
    private static readonly Vector3 P3 = new Vector3(13.25f, 0f, 10.35f);

    [MenuItem("NewTown/Geometry/Apply WornPathB Park-Verge Transition")]
    public static void ApplyAndPersist()
    {
        ValidateContractConfigOnly();
        RequireScene();
        ApplyToOpenScene();
        ValidateOpenScene();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("WornPathB transition persisted. Visual Fidelity remains UNSCORED pending native 4K evidence.");
    }

    public static void ApplyToOpenScene()
    {
        ValidateContractConfigOnly();
        RequireScene();
        Contract c = LoadContract();
        Renderer source = RequireRenderer(SourceName);
        Collider sourceCollider = source.GetComponent<Collider>();
        if (sourceCollider == null || !sourceCollider.enabled)
            throw new InvalidOperationException("WornPathB fallback collider is missing/disabled.");
        if (source.sharedMaterial == null)
            throw new InvalidOperationException("WornPathB source PBR material is missing.");

        source.enabled = false;
        EditorUtility.SetDirty(source);
        ApplyCurbOpening();

        GameObject old = FindSceneObject(StateName);
        if (old != null) UnityEngine.Object.DestroyImmediate(old);
        GameObject ground = FindSceneObject("Ground");
        if (ground == null) throw new InvalidOperationException("Ground root missing.");
        RequireIdentityWorld(ground.transform, "Ground");

        var state = new GameObject(StateName);
        state.transform.SetParent(ground.transform, false);
        Directory.CreateDirectory(MeshRoot);
        Directory.CreateDirectory(MaterialRoot);
        Material material = EnsureMaterial(source.sharedMaterial, c);

        var lodRoot = new GameObject(LodRootName);
        lodRoot.transform.SetParent(state.transform, false);
        var group = lodRoot.AddComponent<LODGroup>();
        group.fadeMode = LODFadeMode.CrossFade;
        group.animateCrossFading = true;
        var lods = new LOD[4];
        for (int i = 0; i < 4; i++)
        {
            var child = new GameObject($"HD_WornPathB_Transition_LOD{i}");
            child.transform.SetParent(lodRoot.transform, false);
            Mesh mesh = EnsureMesh(i, LongSegments[i], CrossCounts[i], c);
            var mf = child.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = child.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.receiveShadows = true;
            var weather = child.AddComponent<QualityBlockWeatheringSurface>();
            weather.Configure(
                NewTownSurfaceExposure.SunExposed | NewTownSurfaceExposure.RainExposed |
                NewTownSurfaceExposure.GroundContact | NewTownSurfaceExposure.UpwardFacing,
                NewTownStainSource.FootTraffic | NewTownStainSource.UVExposure,
                1f, 0.95f, 0.18f, 0.72f);
            lods[i] = new LOD(LodScreen[i], new Renderer[] { mr }) { fadeTransitionWidth = 0.12f };
        }
        group.SetLODs(lods);
        group.RecalculateBounds();

        var manifest = state.AddComponent<QualityBlockGroundWornPathBTransitionManifest>();
        manifest.Configure(OpeningFirst, OpeningLast, c.dimensions.curbOpeningWidthM,
            c.dimensions.parkPavingTopYM - c.dimensions.pathMouthTopYM,
            c.dimensions.compactedCoreTopYM - c.dimensions.grassTopYM,
            c.dimensions.shoulderJoinTopYM - c.dimensions.grassTopYM, true, true, true);
        EditorUtility.SetDirty(manifest);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    [MenuItem("NewTown/QA/Validate WornPathB Park-Verge Transition")]
    public static void ValidateOpenScene()
    {
        ValidateContractConfigOnly();
        RequireScene();
        Contract c = LoadContract();
        Renderer source = RequireRenderer(SourceName);
        Renderer park = RequireRenderer("ParkPath");
        Bounds sb = source.bounds;
        AssertNear(sb.min.x, c.sourceAudit.sourceMinXM, PosTol, "source min X");
        AssertNear(sb.max.x, c.sourceAudit.sourceMaxXM, PosTol, "source max X");
        AssertNear(sb.min.z, c.sourceAudit.sourceMinZM, PosTol, "source min Z");
        AssertNear(sb.max.z, c.sourceAudit.sourceMaxZM, PosTol, "source max Z");
        AssertNear(sb.max.y, c.sourceAudit.sourceTopYM, PosTol, "source top Y");
        AssertNear(park.bounds.max.x, c.dimensions.parkPathEastEdgeXM, PosTol, "ParkPath east edge");
        AssertNear(park.bounds.max.y, c.dimensions.parkPavingTopYM, PosTol, "ParkPath top");
        if (source.enabled) throw new InvalidOperationException("Coarse WornPathB Renderer must be disabled.");
        if (source.GetComponent<Collider>() == null || !source.GetComponent<Collider>().enabled)
            throw new InvalidOperationException("WornPathB fallback collider must remain enabled.");

        ValidateCurbOpening(c);
        GameObject state = FindSceneObject(StateName);
        if (state == null || state.transform.parent == null || state.transform.parent.name != "Ground")
            throw new InvalidOperationException("WornPathB correction must exist under the Ground metadata scope.");
        if (state.GetComponentsInChildren<Collider>(true).Length != 0)
            throw new InvalidOperationException("WornPathB visual correction must remain collider-free.");

        GameObject lodRoot = FindSceneObject(LodRootName);
        LODGroup group = lodRoot == null ? null : lodRoot.GetComponent<LODGroup>();
        if (group == null || !lodRoot.transform.IsChildOf(state.transform))
            throw new InvalidOperationException("WornPathB LOD root/group is missing or outside the correction state.");
        LOD[] lods = group.GetLODs();
        if (lods.Length != 4 || group.fadeMode != LODFadeMode.CrossFade || !group.animateCrossFading)
            throw new InvalidOperationException("WornPathB must retain four animated cross-faded LOD levels.");

        int priorVertices = int.MaxValue;
        Bounds? lod0Bounds = null;
        Material common = null;
        for (int i = 0; i < lods.Length; i++)
        {
            if (lods[i].renderers == null || lods[i].renderers.Length != 1 || lods[i].renderers[0] == null)
                throw new InvalidOperationException($"WornPathB LOD{i} must contain exactly one Renderer.");
            Renderer r = lods[i].renderers[0];
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null ||
                !mf.sharedMesh.name.StartsWith("GM_HD_Interface_WornPathB_", StringComparison.Ordinal))
                throw new InvalidOperationException($"WornPathB LOD{i} reverted to missing/non-authored geometry.");
            if (mf.sharedMesh.vertexCount >= priorVertices)
                throw new InvalidOperationException("WornPathB LOD vertex counts must strictly decrease.");
            priorVertices = mf.sharedMesh.vertexCount;
            ValidateUpwardSurface(mf.sharedMesh, i);

            if (lod0Bounds == null) lod0Bounds = r.bounds;
            else if ((r.bounds.center - lod0Bounds.Value.center).magnitude > c.hardLimits.maxLodBoundsCenterDriftM ||
                     (r.bounds.size - lod0Bounds.Value.size).magnitude > c.hardLimits.maxLodBoundsSizeDriftM)
                throw new InvalidOperationException($"WornPathB LOD{i} silhouette bounds drift beyond hard limit.");

            if (r.sharedMaterial == null || r.sharedMaterial.name != "PBR_WornPathB_Transition")
                throw new InvalidOperationException($"WornPathB LOD{i} material binding is invalid.");
            if (common == null) common = r.sharedMaterial;
            else if (common != r.sharedMaterial) throw new InvalidOperationException("WornPathB LOD materials diverged.");
            if (r.GetComponent<QualityBlockWeatheringSurface>() == null)
                throw new InvalidOperationException($"WornPathB LOD{i} lacks cause-based weathering metadata.");
        }
        ValidateMaterial(common, c);

        var m = state.GetComponent<QualityBlockGroundWornPathBTransitionManifest>();
        if (m == null || !m.SourceRendererDisabled || !m.SourceColliderPreserved || !m.VisualCorrectionColliderFree)
            throw new InvalidOperationException("WornPathB responsibility-separation manifest is invalid.");
        if (m.OpeningFirstIndex != OpeningFirst || m.OpeningLastIndex != OpeningLast)
            throw new InvalidOperationException("WornPathB curb-opening manifest indices drifted.");
        AssertNear(m.CurbOpeningWidthM, c.dimensions.curbOpeningWidthM, 0.002f, "manifest opening width");
        AssertNear(m.MouthBelowPavingM, c.dimensions.parkPavingTopYM - c.dimensions.pathMouthTopYM,
            0.001f, "manifest mouth step");

        Debug.Log("WornPathB structural QA passed, including upward triangle normals on all LODs. Pixel realism remains UNSCORED.");
    }

    [MenuItem("NewTown/QA/Validate WornPathB Transition Contract Only")]
    public static void ValidateContractConfigOnly()
    {
        if (!File.Exists(ContractPath) || !File.Exists(LookdevPath))
            throw new InvalidOperationException("WornPathB transition contract/lookdev is missing.");
        Contract c = LoadContract();
        if (c.runtimeRenderVerified || c.visualFidelityPointsAwarded != 0)
            throw new InvalidOperationException("Source-side WornPathB QA cannot claim render verification or visual points.");
        Need(c.visualFidelityStatus, "visualFidelityStatus"); Need(c.targetPeriod, "targetPeriod"); Need(c.targetRegion, "targetRegion");
        if (c.sourceAudit == null || c.assembly == null || c.dimensions == null || c.material == null ||
            c.weathering == null || c.lodPolicy == null || c.hardLimits == null)
            throw new InvalidOperationException("WornPathB contract is missing mandatory construction/material sections.");
        Need(c.sourceAudit.visualRisk, "sourceAudit.visualRisk");
        if (c.sourceAudit.legacyGrassGapToParkCurbM < 0.45f || c.sourceAudit.sourceWidthM < 1.9f || c.sourceAudit.sourceLengthM < 5.9f)
            throw new InvalidOperationException("WornPathB source audit no longer proves the rectangular/disconnected source risk.");
        Need(c.assembly.manufacture, "assembly.manufacture"); Need(c.assembly.mounting, "assembly.mounting");
        Need(c.assembly.interfacesGapsSeals, "assembly.interfacesGapsSeals"); Need(c.assembly.orientationExposure, "assembly.orientationExposure");
        Need(c.assembly.aging, "assembly.aging"); Need(c.assembly.geometryVsMaterial, "assembly.geometryVsMaterial");
        if (c.assembly.components == null || c.assembly.components.Length < 5)
            throw new InvalidOperationException("WornPathB contract must enumerate physical/source components.");
        AssertNear(c.dimensions.mouthCenterXM, P0.x, 0.001f, "mouth center X");
        AssertNear(c.dimensions.mouthCenterZM, P0.z, 0.001f, "mouth center Z");
        AssertNear(c.dimensions.terminalCenterXM, P3.x, 0.001f, "terminal center X");
        AssertNear(c.dimensions.terminalCenterZM, P3.z, 0.001f, "terminal center Z");
        if (c.dimensions.curbOpeningFirstIndex != OpeningFirst || c.dimensions.curbOpeningLastIndex != OpeningLast)
            throw new InvalidOperationException("WornPathB curb-opening index policy drifted.");
        AssertNear(c.dimensions.curbOpeningWidthM, 1.212f, 0.003f, "contract curb opening");
        float mouthStep = c.dimensions.parkPavingTopYM - c.dimensions.pathMouthTopYM;
        if (mouthStep < 0.001f || mouthStep > 0.006f)
            throw new InvalidOperationException("WornPathB mouth must remain 1-6 mm below paving.");
        if (c.dimensions.compactedCoreThicknessM < 0.030f || c.dimensions.compactedCoreThicknessM > 0.060f ||
            c.dimensions.shoulderJoinTopYM - c.dimensions.grassTopYM > 0.006f)
            throw new InvalidOperationException("WornPathB wearing-course/shoulder dimensions are outside hard construction bounds.");
        Need(c.material.family, "material.family"); Need(c.material.finish, "material.finish");
        Need(c.material.microstructure, "material.microstructure"); Need(c.material.uvAging, "material.uvAging");
        Need(c.material.angularFresnelResponse, "material.angularFresnelResponse");
        if (c.material.metallic > c.hardLimits.maxMetallic || c.material.roughnessMin < 0.85f ||
            c.material.roughnessMax > 1f || c.material.roughnessMin > c.material.roughnessMax || c.material.wetness != 0f)
            throw new InvalidOperationException("WornPathB dry-soil PBR contract is physically implausible.");
        AssertNear(c.material.macroTileMeters, MacroTileM, 0.001f, "macro tile");
        AssertNear(c.material.detailTileMeters, DetailTileM, 0.001f, "detail tile");
        Need(c.weathering.causes, "weathering.causes"); Need(c.weathering.forbidden, "weathering.forbidden");
        Need(c.lodPolicy.lod0, "lod0"); Need(c.lodPolicy.lod1, "lod1"); Need(c.lodPolicy.lod2, "lod2");
        Need(c.lodPolicy.lod3, "lod3"); Need(c.lodPolicy.transitionPolicy, "LOD transitionPolicy");
        if (c.evidencePlan == null || c.evidencePlan.Length < 4 || c.criticalFailPrevention == null || c.criticalFailPrevention.Length < 7)
            throw new InvalidOperationException("WornPathB contract lacks required rendered-evidence/critical-fail plans.");
    }

    private static void ApplyCurbOpening()
    {
        for (int i = 0; i < CurbCount; i++)
        {
            MeshRenderer mr = RequireRenderer(CurbPrefix + i.ToString("00")) as MeshRenderer;
            if (mr == null) throw new InvalidOperationException($"ParkPathEast curb {i} is not a MeshRenderer.");
            mr.enabled = i < OpeningFirst || i > OpeningLast;
            EditorUtility.SetDirty(mr);
        }
    }

    private static void ValidateCurbOpening(Contract c)
    {
        for (int i = 0; i < CurbCount; i++)
        {
            Renderer r = RequireRenderer(CurbPrefix + i.ToString("00"));
            bool visible = i < OpeningFirst || i > OpeningLast;
            if (r.enabled != visible) throw new InvalidOperationException($"ParkPathEast curb visibility drift at {i}.");
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null || (visible && !mf.sharedMesh.name.StartsWith("GM_HD_", StringComparison.Ordinal)))
                throw new InvalidOperationException($"ParkPathEast curb {i} missing authored mesh.");
        }
        Renderer first = RequireRenderer(CurbPrefix + "26");
        Renderer last = RequireRenderer(CurbPrefix + "27");
        Renderer before = RequireRenderer(CurbPrefix + "25");
        Renderer after = RequireRenderer(CurbPrefix + "28");
        AssertNear(first.bounds.center.z, c.dimensions.firstOpeningModuleCenterZM, PosTol, "opening module 26 center");
        AssertNear(last.bounds.center.z, c.dimensions.lastOpeningModuleCenterZM, PosTol, "opening module 27 center");
        AssertNear(before.bounds.max.z, c.dimensions.curbOpeningMinZM, PosTol, "opening min Z");
        AssertNear(after.bounds.min.z, c.dimensions.curbOpeningMaxZM, PosTol, "opening max Z");
        AssertNear(after.bounds.min.z - before.bounds.max.z, c.dimensions.curbOpeningWidthM, 0.024f, "measured curb opening");
    }

    private static Mesh EnsureMesh(int lod, int along, int crossCount, Contract c)
    {
        string path = $"{MeshRoot}/GM_HD_Interface_WornPathB_LOD{lod}.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null)
        {
            mesh = new Mesh();
            AssetDatabase.CreateAsset(mesh, path);
        }
        else mesh.Clear();
        mesh.name = $"GM_HD_Interface_WornPathB_LOD{lod}";

        float[] stations = crossCount == 7 ?
            new[] { -1f, -0.78f, -0.52f, 0f, 0.52f, 0.78f, 1f } :
            new[] { -1f, -0.62f, 0f, 0.62f, 1f };
        var v = new List<Vector3>((along + 1) * stations.Length);
        var uv = new List<Vector2>(v.Capacity);
        var tri = new List<int>(along * (stations.Length - 1) * 6);
        for (int iz = 0; iz <= along; iz++)
        {
            float t = iz / (float)along;
            Vector3 center = Bezier(t);
            Vector3 tangent = BezierTangent(t); tangent.y = 0f;
            if (tangent.sqrMagnitude < 0.000001f) throw new InvalidOperationException("Degenerate WornPathB spline tangent.");
            tangent.Normalize();
            Vector3 lateral = new Vector3(-tangent.z, 0f, tangent.x);
            float width = WidthAt(t, c);
            float centerY = CenterTopAt(t, c);
            float outerY = OuterTopAt(t, c);
            for (int ix = 0; ix < stations.Length; ix++)
            {
                float s = stations[ix], abs = Mathf.Abs(s), y = centerY;
                if (abs > 0.52f)
                    y = Mathf.Lerp(centerY, outerY, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.52f, 1f, abs)));
                else
                    y -= c.dimensions.centerWearDepressionM * (1f - Mathf.Clamp01(abs / 0.52f)) *
                         Mathf.Sin(Mathf.PI * Mathf.Clamp01(t * 1.15f));
                y += 0.0012f * Mathf.Sin(t * Mathf.PI * 5f + s * 0.7f) * Mathf.Sin(Mathf.PI * t) * (1f - 0.35f * abs);
                Vector3 p = center + lateral * (0.5f * width * s); p.y = y;
                v.Add(p); uv.Add(new Vector2(p.x, p.z));
            }
        }
        int row = stations.Length;
        for (int iz = 0; iz < along; iz++)
        {
            int a0 = iz * row, n0 = (iz + 1) * row;
            for (int ix = 0; ix < row - 1; ix++)
            {
                int a = a0 + ix, b = a + 1, c0 = n0 + ix, d = c0 + 1;
                // Cross(lateral, forward) points +Y. The entry stays at fixed width until t=.22,
                // preventing the outer rail from reversing longitudinally while the spline turns.
                tri.Add(a); tri.Add(b); tri.Add(c0);
                tri.Add(b); tri.Add(d); tri.Add(c0);
            }
        }
        mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0, true);
        mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        ValidateUpwardSurface(mesh, lod);
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    private static void ValidateUpwardSurface(Mesh mesh, int lod)
    {
        Vector3[] normals = mesh.normals;
        if (normals == null || normals.Length != mesh.vertexCount)
            throw new InvalidOperationException($"WornPathB LOD{lod} normals are missing.");
        float minY = 1f, meanY = 0f;
        for (int i = 0; i < normals.Length; i++) { minY = Mathf.Min(minY, normals[i].y); meanY += normals[i].y; }
        meanY /= Mathf.Max(1, normals.Length);
        if (minY < 0.55f || meanY < 0.80f)
            throw new InvalidOperationException($"WornPathB LOD{lod} has inverted/over-steep surface normals: minY={minY:F3}, meanY={meanY:F3}.");
    }

    private static Material EnsureMaterial(Material source, Contract c)
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (m == null) { m = new Material(source); AssetDatabase.CreateAsset(m, MaterialPath); }
        else { m.CopyPropertiesFromMaterial(source); m.shader = source.shader; }
        m.name = "PBR_WornPathB_Transition";
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.06f);
        if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", c.material.normalScale);
        if (m.HasProperty("_DetailNormalMapScale")) m.SetFloat("_DetailNormalMapScale", 1f);
        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
        m.DisableKeyword("_EMISSION");
        Metric(m, "_MainTex", MacroTileM); Metric(m, "_BumpMap", MacroTileM); Metric(m, "_MetallicGlossMap", MacroTileM);
        Metric(m, "_DetailAlbedoMap", DetailTileM); Metric(m, "_DetailNormalMap", DetailTileM);
        EditorUtility.SetDirty(m); return m;
    }

    private static void ValidateMaterial(Material m, Contract c)
    {
        if (m == null || m.shader == null || m.shader.name != "Standard") throw new InvalidOperationException("WornPathB PBR material/shader invalid.");
        if (m.HasProperty("_Metallic") && m.GetFloat("_Metallic") > c.hardLimits.maxMetallic) throw new InvalidOperationException("Metallic soil is forbidden.");
        if (m.IsKeywordEnabled("_EMISSION")) throw new InvalidOperationException("Emissive/baked-light soil is forbidden.");
        string[] textures = { "_MainTex", "_BumpMap", "_MetallicGlossMap", "_DetailAlbedoMap", "_DetailNormalMap" };
        if (textures.Any(x => m.GetTexture(x) == null)) throw new InvalidOperationException("WornPathB macro/detail PBR texture set incomplete.");
        Scale(m, "_MainTex", 1f / MacroTileM, c.hardLimits.maxUvScaleError); Scale(m, "_BumpMap", 1f / MacroTileM, c.hardLimits.maxUvScaleError);
        Scale(m, "_MetallicGlossMap", 1f / MacroTileM, c.hardLimits.maxUvScaleError); Scale(m, "_DetailAlbedoMap", 1f / DetailTileM, c.hardLimits.maxUvScaleError);
        Scale(m, "_DetailNormalMap", 1f / DetailTileM, c.hardLimits.maxUvScaleError);
    }

    private static void Metric(Material m, string prop, float tileM)
    {
        if (!m.HasProperty(prop)) return;
        float s = 1f / tileM; m.SetTextureScale(prop, new Vector2(s, s)); m.SetTextureOffset(prop, Vector2.zero);
    }
    private static void Scale(Material m, string prop, float expected, float tol)
    {
        Vector2 s = m.GetTextureScale(prop);
        if (Mathf.Abs(s.x - expected) > tol || Mathf.Abs(s.y - expected) > tol)
            throw new InvalidOperationException($"WornPathB {prop} physical UV scale drift: {s}.");
    }
    private static float WidthAt(float t, Contract c)
    {
        // Keep the mouth width constant while the route clears the curb and starts its turn. Expanding
        // the ribbon during this high-curvature interval can make an outer vertex rail reverse direction
        // and create folded/inverted triangles even when triangle winding itself is nominally correct.
        if (t <= 0.22f) return c.dimensions.mouthWidthM;
        if (t <= 0.55f)
            return Mathf.Lerp(c.dimensions.mouthWidthM, c.dimensions.midWidthM,
                Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.22f, 0.55f, t)));
        if (t <= 0.68f) return c.dimensions.midWidthM;
        return Mathf.Lerp(c.dimensions.midWidthM, c.dimensions.terminalWidthM,
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.68f, 1f, t)));
    }
    private static float CenterTopAt(float t, Contract c)
    {
        float y = Mathf.Lerp(c.dimensions.pathMouthTopYM, c.dimensions.compactedCoreTopYM, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.18f)));
        return Mathf.Lerp(y, c.dimensions.shoulderJoinTopYM + 0.002f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.84f, 1f, t)));
    }
    private static float OuterTopAt(float t, Contract c) => Mathf.Lerp(c.dimensions.pathMouthTopYM, c.dimensions.shoulderJoinTopYM, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.18f)));
    private static Vector3 Bezier(float t) { float u = 1f - t; return u*u*u*P0 + 3f*u*u*t*P1 + 3f*u*t*t*P2 + t*t*t*P3; }
    private static Vector3 BezierTangent(float t) { float u = 1f - t; return 3f*u*u*(P1-P0) + 6f*u*t*(P2-P1) + 3f*t*t*(P3-P2); }

    private static Contract LoadContract()
    {
        Contract c = JsonUtility.FromJson<Contract>(File.ReadAllText(ContractPath));
        if (c == null) throw new InvalidOperationException("WornPathB contract parse failed.");
        return c;
    }
    private static void RequireScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }
    private static GameObject FindSceneObject(string name) => Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(x => x.scene.IsValid() && x.name == name);
    private static Renderer RequireRenderer(string name)
    {
        GameObject go = FindSceneObject(name); Renderer r = go == null ? null : go.GetComponent<Renderer>();
        if (r == null) throw new InvalidOperationException($"Required Renderer missing: {name}"); return r;
    }
    private static void RequireIdentityWorld(Transform t, string label)
    {
        if (t.position.sqrMagnitude > 0.000001f || Quaternion.Angle(t.rotation, Quaternion.identity) > 0.01f || (t.lossyScale - Vector3.one).magnitude > 0.0001f)
            throw new InvalidOperationException($"{label} must retain identity world transform for metric interface geometry.");
    }
    private static void AssertNear(float a, float e, float tol, string label)
    {
        if (Mathf.Abs(a-e) > tol) throw new InvalidOperationException($"{label} drift: {a:F4}; expected {e:F4} ± {tol:F4}.");
    }
    private static void Need(string s, string label) { if (string.IsNullOrWhiteSpace(s)) throw new InvalidOperationException($"WornPathB contract missing {label}."); }

    [Serializable] private sealed class Contract
    {
        public string schemaVersion, visualFidelityStatus, targetPeriod, targetRegion; public bool runtimeRenderVerified; public int visualFidelityPointsAwarded;
        public SourceAudit sourceAudit; public Assembly assembly; public Dimensions dimensions; public MaterialSpec material; public Weathering weathering; public LodPolicy lodPolicy; public HardLimits hardLimits;
        public string[] evidencePlan, criticalFailPrevention;
    }
    [Serializable] private sealed class SourceAudit { public float sourceWidthM, sourceLengthM, sourceMinXM, sourceMaxXM, sourceMinZM, sourceMaxZM, sourceTopYM, legacyGrassGapToParkCurbM; public string visualRisk; }
    [Serializable] private sealed class Assembly { public string[] components; public string manufacture, mounting, interfacesGapsSeals, orientationExposure, aging, geometryVsMaterial; }
    [Serializable] private sealed class Dimensions
    {
        public float grassTopYM, parkPavingTopYM, parkPathEastEdgeXM; public int curbOpeningFirstIndex, curbOpeningLastIndex;
        public float firstOpeningModuleCenterZM, lastOpeningModuleCenterZM, curbOpeningMinZM, curbOpeningMaxZM, curbOpeningWidthM;
        public float mouthCenterXM, mouthCenterZM, pathMouthTopYM, mouthWidthM, midWidthM, terminalCenterXM, terminalCenterZM, terminalWidthM;
        public float compactedCoreTopYM, compactedCoreThicknessM, shoulderJoinTopYM, centerWearDepressionM;
    }
    [Serializable] private sealed class MaterialSpec
    {
        public string family, finish, microstructure, uvAging, angularFresnelResponse; public float albedoMin, albedoMax, roughnessMin, roughnessMax, metallic, specularF0, normalScale, wetness, macroTileMeters, detailTileMeters;
    }
    [Serializable] private sealed class Weathering { public string causes, forbidden; }
    [Serializable] private sealed class LodPolicy { public string lod0, lod1, lod2, lod3, transitionPolicy; }
    [Serializable] private sealed class HardLimits { public float maxMetallic, maxUvScaleError, maxLodBoundsCenterDriftM, maxLodBoundsSizeDriftM; }
}

public sealed class QualityBlockGroundWornPathBTransitionManifest : MonoBehaviour
{
    [SerializeField] private int openingFirstIndex, openingLastIndex;
    [SerializeField] private float curbOpeningWidthM, mouthBelowPavingM, compactedCoreAboveGrassM, shoulderJoinAboveGrassM;
    [SerializeField] private bool sourceRendererDisabled, sourceColliderPreserved, visualCorrectionColliderFree;
    public int OpeningFirstIndex => openingFirstIndex; public int OpeningLastIndex => openingLastIndex;
    public float CurbOpeningWidthM => curbOpeningWidthM; public float MouthBelowPavingM => mouthBelowPavingM;
    public bool SourceRendererDisabled => sourceRendererDisabled; public bool SourceColliderPreserved => sourceColliderPreserved; public bool VisualCorrectionColliderFree => visualCorrectionColliderFree;
    public void Configure(int first, int last, float opening, float mouthStep, float coreAbove, float shoulderAbove, bool rendererOff, bool colliderKept, bool noVisualCollider)
    {
        openingFirstIndex=first; openingLastIndex=last; curbOpeningWidthM=opening; mouthBelowPavingM=mouthStep; compactedCoreAboveGrassM=coreAbove; shoulderJoinAboveGrassM=shoulderAbove;
        sourceRendererDisabled=rendererOff; sourceColliderPreserved=colliderKept; visualCorrectionColliderFree=noVisualCollider;
    }
}
