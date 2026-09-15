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
/// Adds the missing physical thickness cue to generated fallback facade glazing without introducing
/// a second broad transparent sheet. The existing front optical plane remains the transmission/Fresnel
/// surface; this pass adds only four perimeter edge strips extending 4 mm inward, matching nominal
/// late-1990s/circa-2000 clear float glazing construction.
///
/// The pass is deliberately tied to sceneSaving. QualityBlockFacadeOpticsUpgrade rebuilds its root
/// synchronously and QualityBlockEnvironmentLightingUpgrade then saves the benchmark scene before
/// reflection synchronization. Rebuilding the perimeter edge geometry at that save boundary therefore
/// keeps it present before the realtime probes are rendered, while authored replacement art remains
/// authoritative. No gameplay collider is created or changed.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockFacadeGlassThicknessUpgrade
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string OpticsRootName = "DanchiFacadeOptics";
    private const string EdgeChildName = "GlassThicknessEdge_4mm";
    private const string MeshRoot = "Assets/Art/GeneratedFacadeOpticsMeshes";
    private const string EdgeMeshPath = MeshRoot + "/GM_FacadeGlassEdge4mm.asset";
    private const string GlassMaterialPath = "Assets/Art/GeneratedFacadeOptics/MAT_WindowClearGlass.mat";
    private const string ContractPath = "Assets/QA/facade_glass_edge_construction_contract.json";
    private const float NominalThicknessMeters = 0.004f;
    private const int ExpectedPaneCount = 65;

    static QualityBlockFacadeGlassThicknessUpgrade()
    {
        EditorSceneManager.sceneSaving -= OnSceneSaving;
        EditorSceneManager.sceneSaving += OnSceneSaving;
    }

    [MenuItem("NewTown/Materials/Build Physical 4mm Facade Glass Edges")]
    public static void BuildForOpenScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() ||
            !string.Equals(EditorSceneManager.GetActiveScene().path, ScenePath, StringComparison.Ordinal))
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        if (IsAuthoredDanchiActive())
        {
            Debug.Log("Authored danchi replacement is active; generated 4 mm facade-glass edge pass was skipped.");
            return;
        }

        GameObject opticsRoot = FindSceneObject(OpticsRootName);
        if (opticsRoot == null)
            throw new InvalidOperationException(
                "DanchiFacadeOptics is missing. Build the generated facade optics before adding physical glass thickness.");

        BuildForOpticsRoot(opticsRoot);
        ValidateOpenScene();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "Physical facade-glass edge construction built: 65 panes retain one broad front optical surface and gain only four 4 mm perimeter edge faces. " +
            "This is implementation evidence only; grazing/oblique 4K render verification remains pending.");
    }

    /// <summary>
    /// Source-side/scene-side validator. Passing this method awards zero Visual Fidelity points.
    /// </summary>
    public static void ValidateOpenScene()
    {
        ValidateContract();

        if (IsAuthoredDanchiActive())
        {
            Debug.Log(
                "Facade-glass edge QA skipped because authored danchi replacement is active. Generated fallback geometry is not authoritative.");
            return;
        }

        GameObject opticsRoot = FindSceneObject(OpticsRootName);
        if (opticsRoot == null)
            throw new InvalidOperationException("DanchiFacadeOptics is missing for facade-glass edge QA.");

        GameObject[] panes = FindPaneObjects(opticsRoot);
        if (panes.Length != ExpectedPaneCount)
            throw new InvalidOperationException(
                $"Expected {ExpectedPaneCount} generated glazing panes before glass-edge QA, found {panes.Length}.");

        Mesh edgeMesh = AssetDatabase.LoadAssetAtPath<Mesh>(EdgeMeshPath);
        ValidateEdgeMeshAsset(edgeMesh);

        Material canonicalGlass = AssetDatabase.LoadAssetAtPath<Material>(GlassMaterialPath);
        ValidateGlassMaterial(canonicalGlass);

        int edgeCount = 0;
        foreach (GameObject pane in panes)
        {
            MeshRenderer paneRenderer = pane.GetComponent<MeshRenderer>();
            MeshFilter paneFilter = pane.GetComponent<MeshFilter>();
            if (paneRenderer == null || paneFilter == null || paneFilter.sharedMesh == null)
                throw new InvalidOperationException($"Glazing pane is missing render geometry: {HierarchyPath(pane.transform)}");
            if (pane.GetComponent<Collider>() != null)
                throw new InvalidOperationException($"Generated glazing pane may not add gameplay collision: {HierarchyPath(pane.transform)}");

            Transform edge = pane.transform.Find(EdgeChildName);
            if (edge == null)
                throw new InvalidOperationException($"4 mm perimeter edge is missing under glazing pane: {HierarchyPath(pane.transform)}");
            edgeCount++;

            if (edge.childCount != 0)
                throw new InvalidOperationException($"Glass edge object must not contain hidden nested geometry: {HierarchyPath(edge)}");
            if (edge.GetComponent<Collider>() != null)
                throw new InvalidOperationException($"Glass perimeter edge must remain render-only: {HierarchyPath(edge)}");

            MeshFilter edgeFilter = edge.GetComponent<MeshFilter>();
            MeshRenderer edgeRenderer = edge.GetComponent<MeshRenderer>();
            if (edgeFilter == null || edgeRenderer == null)
                throw new InvalidOperationException($"Glass edge render components are incomplete: {HierarchyPath(edge)}");
            if (edgeFilter.sharedMesh != edgeMesh)
                throw new InvalidOperationException($"Glass edge is not using the canonical 4 mm edge-only mesh: {HierarchyPath(edge)}");
            if (edgeRenderer.sharedMaterial != canonicalGlass || paneRenderer.sharedMaterial != canonicalGlass)
                throw new InvalidOperationException(
                    $"Front optical surface and thickness edge must share the same canonical nonmetallic glass material: {HierarchyPath(pane.transform)}");
            if (edgeRenderer.shadowCastingMode != ShadowCastingMode.Off || edgeRenderer.receiveShadows)
                throw new InvalidOperationException($"Transparent glass edge must not create an opaque-style shadow path: {HierarchyPath(edge)}");

            if (edge.localPosition.sqrMagnitude > 1e-10f || Quaternion.Angle(edge.localRotation, Quaternion.identity) > 0.001f ||
                (edge.localScale - Vector3.one).sqrMagnitude > 1e-10f)
                throw new InvalidOperationException(
                    $"Glass edge must be exactly co-located with its parent front optical plane before its 4 mm inward extrusion: {HierarchyPath(edge)}");

            ValidatePaneDimensions(pane.transform);

            float worldThickness = Vector3.Distance(
                edge.TransformPoint(Vector3.zero),
                edge.TransformPoint(new Vector3(0f, 0f, -NominalThicknessMeters)));
            if (Mathf.Abs(worldThickness - NominalThicknessMeters) > 0.0002f)
                throw new InvalidOperationException(
                    $"Glass pane physical thickness drifted from 4 mm at {HierarchyPath(edge)}: {worldThickness * 1000f:F3} mm.");
        }

        if (edgeCount != ExpectedPaneCount)
            throw new InvalidOperationException($"Expected {ExpectedPaneCount} physical glass edge shells, found {edgeCount}.");

        Debug.Log(
            "Facade glass-edge construction QA passed for 65 panes: one front optical surface plus edge-only 4 mm thickness geometry, canonical nonmetallic glass, no colliders. " +
            "Actual edge visibility, Fresnel/path-length response, sorting and aliasing still require native 4K oblique/grazing pixels; Visual Fidelity remains UNSCORED.");
    }

    [MenuItem("NewTown/QA/Validate Facade Glass Edge Construction Contract")]
    public static void ValidateContract()
    {
        string absolute = AbsolutePath(ContractPath);
        if (!File.Exists(absolute))
            throw new FileNotFoundException($"Facade glass-edge construction contract is missing: {ContractPath}");

        FacadeGlassEdgeContract contract = JsonUtility.FromJson<FacadeGlassEdgeContract>(File.ReadAllText(absolute));
        if (contract == null)
            throw new InvalidOperationException("Could not parse facade glass-edge construction contract.");
        if (!string.Equals(contract.schemaVersion, "1.0", StringComparison.Ordinal))
            throw new InvalidOperationException($"Unexpected facade glass-edge schemaVersion '{contract.schemaVersion}'.");
        if (!string.Equals(contract.assemblyId, "facade_clear_float_glazing_thickness", StringComparison.Ordinal))
            throw new InvalidOperationException($"Unexpected facade glass-edge assemblyId '{contract.assemblyId}'.");
        if (contract.expectedPaneCount != ExpectedPaneCount || contract.edgeQuadsPerPane != 4)
            throw new InvalidOperationException(
                $"Facade glass-edge topology contract drifted: panes={contract.expectedPaneCount}, edgeQuads={contract.edgeQuadsPerPane}.");
        if (Mathf.Abs(contract.nominalThicknessMm - 4f) > 0.001f)
            throw new InvalidOperationException($"Facade glass nominal thickness must remain exactly 4 mm, got {contract.nominalThicknessMm}.");
        if (!contract.forbidBroadBackSurface || !contract.renderOnlyNoCollider || !contract.requireSameGlassMaterialAsFrontSurface)
            throw new InvalidOperationException(
                "Facade glass-edge contract must forbid a duplicate broad back surface, remain render-only, and share the canonical front glass material.");

        RequireNonEmpty(contract.manufactureInstallation, "manufactureInstallation");
        RequireNonEmpty(contract.mountingInterfaces, "mountingInterfaces");
        RequireNonEmpty(contract.orientationExposureAging, "orientationExposureAging");
        RequireNonEmpty(contract.geometryVsMaterialDetail, "geometryVsMaterialDetail");
        RequireNonEmpty(contract.lodPolicy, "lodPolicy");
        RequireNonEmpty(contract.requiredEvidence, "requiredEvidence");

        string[] requiredDefects =
        {
            "visible_primitive_placeholder",
            "impossible_material_physics",
            "hero_geometry_intersection",
            "severe_aliasing_or_shimmer",
            "missing_construction_material_metadata",
            "unverified_render_claim"
        };
        RequireExactSet(contract.criticalDefectIds, requiredDefects, "criticalDefectIds");

        Debug.Log(
            "Facade glass-edge machine contract valid. This validates construction intent and QA policy only and awards zero Visual Fidelity points.");
    }

    private static void OnSceneSaving(Scene scene, string path)
    {
        if (!scene.IsValid() || !string.Equals(path, ScenePath, StringComparison.Ordinal))
            return;
        if (IsAuthoredDanchiActive())
            return;

        GameObject opticsRoot = FindSceneObject(OpticsRootName);
        if (opticsRoot == null)
            return;

        // Facade optics is rebuilt before the environment pass saves this scene. Adding the edge-only
        // shells here guarantees they are serialized before subsequent realtime reflection rendering.
        BuildForOpticsRoot(opticsRoot);
    }

    private static void BuildForOpticsRoot(GameObject opticsRoot)
    {
        Mesh edgeMesh = GetOrCreateEdgeMesh();
        Material glass = AssetDatabase.LoadAssetAtPath<Material>(GlassMaterialPath);
        if (glass == null)
            throw new InvalidOperationException(
                $"Canonical facade glass material is missing before thickness construction: {GlassMaterialPath}");

        GameObject[] panes = FindPaneObjects(opticsRoot);
        if (panes.Length != ExpectedPaneCount)
            throw new InvalidOperationException(
                $"Cannot build physical glass edges: expected {ExpectedPaneCount} panes, found {panes.Length}.");

        foreach (GameObject pane in panes)
        {
            Transform edge = pane.transform.Find(EdgeChildName);
            if (edge == null)
            {
                var edgeObject = new GameObject(EdgeChildName);
                edge = edgeObject.transform;
                edge.SetParent(pane.transform, false);
            }

            edge.localPosition = Vector3.zero;
            edge.localRotation = Quaternion.identity;
            edge.localScale = Vector3.one;

            MeshFilter filter = edge.GetComponent<MeshFilter>();
            if (filter == null) filter = edge.gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = edgeMesh;

            MeshRenderer renderer = edge.GetComponent<MeshRenderer>();
            if (renderer == null) renderer = edge.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = glass;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbesAndSkybox;

            // No collider is ever created. Remove accidental generated-only colliders fail-safe rather
            // than allow a render-detail pass to alter gameplay collision semantics.
            Collider collider = edge.GetComponent<Collider>();
            if (collider != null)
                UnityEngine.Object.DestroyImmediate(collider);

            EditorUtility.SetDirty(filter);
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(edge.gameObject);
        }
    }

    private static Mesh GetOrCreateEdgeMesh()
    {
        Directory.CreateDirectory(MeshRoot);
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(EdgeMeshPath);
        bool created = false;
        if (mesh == null)
        {
            mesh = new Mesh { name = "GM_FacadeGlassEdge4mm" };
            created = true;
        }

        var vertices = new List<Vector3>(16);
        var uv = new List<Vector2>(16);
        var triangles = new List<int>(24);

        AddQuad(vertices, uv, triangles,
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f),
            new Vector3(-0.5f, 0.5f, -NominalThicknessMeters), new Vector3(-0.5f, -0.5f, -NominalThicknessMeters));
        AddQuad(vertices, uv, triangles,
            new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, -NominalThicknessMeters),
            new Vector3(0.5f, 0.5f, -NominalThicknessMeters), new Vector3(0.5f, 0.5f, 0f));
        AddQuad(vertices, uv, triangles,
            new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f),
            new Vector3(0.5f, 0.5f, -NominalThicknessMeters), new Vector3(-0.5f, 0.5f, -NominalThicknessMeters));
        AddQuad(vertices, uv, triangles,
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(-0.5f, -0.5f, -NominalThicknessMeters),
            new Vector3(0.5f, -0.5f, -NominalThicknessMeters), new Vector3(0.5f, -0.5f, 0f));

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uv);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();

        if (created)
            AssetDatabase.CreateAsset(mesh, EdgeMeshPath);
        else
            EditorUtility.SetDirty(mesh);

        return mesh;
    }

    private static void AddQuad(List<Vector3> vertices, List<Vector2> uv, List<int> triangles,
        Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        int first = vertices.Count;
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        vertices.Add(d);
        uv.Add(new Vector2(0f, 0f));
        uv.Add(new Vector2(1f, 0f));
        uv.Add(new Vector2(1f, 1f));
        uv.Add(new Vector2(0f, 1f));
        triangles.Add(first + 0);
        triangles.Add(first + 1);
        triangles.Add(first + 2);
        triangles.Add(first + 0);
        triangles.Add(first + 2);
        triangles.Add(first + 3);
    }

    private static GameObject[] FindPaneObjects(GameObject opticsRoot)
    {
        return opticsRoot.GetComponentsInChildren<Transform>(true)
            .Select(x => x.gameObject)
            .Where(x =>
                x.transform.parent == opticsRoot.transform &&
                (x.name.StartsWith("FO_Glass_", StringComparison.Ordinal) ||
                 x.name.StartsWith("FO_StairGlass_", StringComparison.Ordinal)))
            .OrderBy(x => x.name, StringComparer.Ordinal)
            .ToArray();
    }

    private static void ValidatePaneDimensions(Transform pane)
    {
        Vector3 s = pane.localScale;
        bool apartment = pane.name.StartsWith("FO_Glass_", StringComparison.Ordinal);
        Vector2 expected = apartment ? new Vector2(1.03f, 1.46f) : new Vector2(1.12f, 1.18f);
        if (Mathf.Abs(s.x - expected.x) > 0.0005f || Mathf.Abs(s.y - expected.y) > 0.0005f || Mathf.Abs(s.z - 1f) > 0.0005f)
            throw new InvalidOperationException(
                $"Glazing pane dimensions drifted from the construction contract at {HierarchyPath(pane)}: localScale={s}.");
    }

    private static void ValidateEdgeMeshAsset(Mesh mesh)
    {
        if (mesh == null)
            throw new InvalidOperationException($"Canonical 4 mm glass edge mesh is missing: {EdgeMeshPath}");
        if (mesh.vertexCount != 16 || mesh.triangles.Length != 24)
            throw new InvalidOperationException(
                $"Glass edge mesh must contain exactly four independent perimeter quads and no front/back broad faces; vertices={mesh.vertexCount}, indices={mesh.triangles.Length}.");
        Vector3 size = mesh.bounds.size;
        if (Mathf.Abs(size.x - 1f) > 0.0001f || Mathf.Abs(size.y - 1f) > 0.0001f ||
            Mathf.Abs(size.z - NominalThicknessMeters) > 0.00005f)
            throw new InvalidOperationException(
                $"Glass edge mesh bounds drifted from 1x1x4mm edge-only construction: {size}.");
        if (mesh.uv == null || mesh.uv.Length != mesh.vertexCount || mesh.normals == null || mesh.normals.Length != mesh.vertexCount ||
            mesh.tangents == null || mesh.tangents.Length != mesh.vertexCount)
            throw new InvalidOperationException("Glass edge mesh requires complete UV0, normals and tangents.");
    }

    private static void ValidateGlassMaterial(Material glass)
    {
        if (glass == null)
            throw new InvalidOperationException($"Canonical clear glass material is missing: {GlassMaterialPath}");
        if (glass.shader == null || !string.Equals(glass.shader.name, "Standard", StringComparison.Ordinal))
            throw new InvalidOperationException("Fallback glass thickness must use the same Standard dielectric shader as the front optical surface.");
        if (glass.GetFloat("_Metallic") > 0.001f)
            throw new InvalidOperationException("Clear float glass thickness cannot be metallic.");
        float smoothness = glass.GetFloat("_Glossiness");
        if (smoothness < 0.82f || smoothness > 0.94f)
            throw new InvalidOperationException($"Clear glass smoothness outside physical fallback range: {smoothness:F3}.");
        if (!glass.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON") || glass.GetInt("_ZWrite") != 0)
            throw new InvalidOperationException("Clear glass thickness must remain premultiplied-transparent with ZWrite disabled.");
        if (glass.IsKeywordEnabled("_EMISSION") || glass.GetColor("_EmissionColor").maxColorComponent > 0.001f)
            throw new InvalidOperationException("Clear glass thickness may not use emissive/painted light response.");
    }

    private static bool IsAuthoredDanchiActive()
    {
        QualityBlockArtSlot slot = Resources.FindObjectsOfTypeAll<QualityBlockArtSlot>()
            .FirstOrDefault(x => x.gameObject.scene.IsValid() && string.Equals(x.SlotId, "danchi.main", StringComparison.Ordinal));
        return slot != null && slot.IsUsingAuthoredArt;
    }

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x.scene.IsValid() && string.Equals(x.name, name, StringComparison.Ordinal));
    }

    private static string HierarchyPath(Transform t)
    {
        if (t == null) return "<null>";
        var parts = new Stack<string>();
        Transform current = t;
        while (current != null)
        {
            parts.Push(current.name);
            current = current.parent;
        }
        return string.Join("/", parts.ToArray());
    }

    private static string AbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private static void RequireNonEmpty(string[] values, string label)
    {
        if (values == null || values.Length == 0 || values.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException($"Facade glass-edge contract requires non-empty '{label}' entries.");
    }

    private static void RequireExactSet(string[] actual, string[] expected, string label)
    {
        if (actual == null || actual.Any(string.IsNullOrWhiteSpace) ||
            actual.Distinct(StringComparer.Ordinal).Count() != actual.Length ||
            !new HashSet<string>(actual, StringComparer.Ordinal).SetEquals(expected))
            throw new InvalidOperationException(
                $"Facade glass-edge contract {label} must be exactly [{string.Join(", ", expected)}].");
    }

    [Serializable]
    private sealed class FacadeGlassEdgeContract
    {
        public string schemaVersion;
        public string assemblyId;
        public int expectedPaneCount;
        public float nominalThicknessMm;
        public int edgeQuadsPerPane;
        public bool forbidBroadBackSurface;
        public bool renderOnlyNoCollider;
        public bool requireSameGlassMaterialAsFrontSurface;
        public string[] manufactureInstallation;
        public string[] mountingInterfaces;
        public string[] orientationExposureAging;
        public string[] geometryVsMaterialDetail;
        public string[] lodPolicy;
        public string[] requiredEvidence;
        public string[] criticalDefectIds;
    }
}
