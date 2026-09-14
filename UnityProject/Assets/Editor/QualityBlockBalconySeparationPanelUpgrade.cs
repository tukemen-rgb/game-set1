using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds the missing physical balcony separation-panel assembly without weakening the established
/// DanchiHighDetail physical-UV registry. The source Danchi detail hierarchy supplies bay transforms and
/// front divider-bracket datums; this pass owns a separate Danchi child root, authored metre-UV meshes,
/// a dry dielectric fibre-cement-like board material, and one LOD0/1/2/3 group per balcony bay.
///
/// Formal reflection/4K/temporal pre-cull validation is read-only and fail-closed. Source validity is
/// implementation evidence only and awards zero Visual Fidelity points without native rendered pixels.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockBalconySeparationPanelUpgrade
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/balcony_separation_panel_contract.json";
    private const string DanchiRootName = "Danchi";
    private const string DetailRootName = "DanchiHighDetail";
    private const string PanelRootName = "DanchiBalconySeparationPanels";
    private const string MeshRoot = "Assets/Art/GeneratedBalconyPartitionMeshes";
    private const string TextureRoot = "Assets/Art/GeneratedBalconyPartition";
    private const string BoardMaterialName = "MAT_BalconyPartitionFiberCement";
    private const string BoardMaterialPath = TextureRoot + "/MAT_BalconyPartitionFiberCement.mat";
    private const string BoardNormalPath = TextureRoot + "/MAT_BalconyPartitionFiberCement_Normal.png";
    private const string BoardMaskPath = TextureRoot + "/MAT_BalconyPartitionFiberCement_MetallicSmoothness.png";
    private const string AluminumMaterialPath = "Assets/Art/GeneratedDetailMaterials/MAT_AgedAluminum.mat";
    private const string SteelMaterialPath = "Assets/Art/GeneratedDetailMaterials/MAT_DarkGalvanizedSteel.mat";

    private const int TextureSize = 1024;
    private const int ExpectedBayCount = 30;
    private const int PhaseBins = 8;
    private const float PhaseStepMeters = 0.017f;
    private const float Epsilon = 0.0005f;
    private const float GeometryTolerance = 0.004f;

    private const float PartitionX = -1.78f;
    private const float FloorTopY = -0.79f;
    private const float BottomClearance = 0.06f;
    private const float OverallHeight = 1.78f;
    private const float RearZ = -7.18f;
    private const float FrontZ = -6.285f;
    private const float FrameSection = 0.032f;
    private const float BoardThickness = 0.006f;
    private const float BoardNormalScale = 0.32f;
    private const float BoardRepeatsPerMeter = 2.5f;
    private const float AluminumRepeatsPerMeter = 14f;
    private const float SteelRepeatsPerMeter = 11f;
    private const float RearBracketDepth = 0.14f;

    private const float Lod0Transition = 0.18f;
    private const float Lod1Transition = 0.08f;
    private const float Lod2Transition = 0.03f;
    private const float Lod3Cull = 0.008f;

    private static readonly string[] FrameNames =
    {
        "BP_DividerPanel_FrameFront",
        "BP_DividerPanel_FrameRear",
        "BP_DividerPanel_FrameTop",
        "BP_DividerPanel_FrameBottom"
    };

    private static readonly string[] BracketNames =
    {
        "BP_DividerPanel_WallBracketLow",
        "BP_DividerPanel_WallBracketHigh"
    };

    private static readonly string[] FastenerNames =
    {
        "BP_DividerPanel_WallFastenerLow_A",
        "BP_DividerPanel_WallFastenerLow_B",
        "BP_DividerPanel_WallFastenerHigh_A",
        "BP_DividerPanel_WallFastenerHigh_B"
    };

    static QualityBlockBalconySeparationPanelUpgrade()
    {
        Camera.onPreCull -= OnCameraPreCull;
        Camera.onPreCull += OnCameraPreCull;
    }

    [MenuItem("NewTown/Geometry/Apply Physical Balcony Separation Panels")]
    public static void ApplyToOpenScene()
    {
        ValidateContractConfigOnly();
        EnsureScene();

        GameObject oldRoot = FindSceneObject(PanelRootName);
        if (oldRoot != null)
            UnityEngine.Object.DestroyImmediate(oldRoot);

        if (IsAuthoredDanchiActive())
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("Authored danchi replacement is active; generated balcony separation panels were removed/not added. No Visual Fidelity points were assigned.");
            return;
        }

        GameObject danchi = FindSceneObject(DanchiRootName);
        GameObject detailRoot = FindSceneObject(DetailRootName);
        if (danchi == null || detailRoot == null)
            throw new InvalidOperationException("Danchi and DanchiHighDetail must exist before generated balcony separation panels are built.");
        RequireCompatibleDatumRoots(danchi.transform, detailRoot.transform);

        // Keep existing aluminium/steel microstructure canonical, but do not add the new board material to
        // DanchiHighDetail: that hierarchy has an intentionally fixed six-material metric-UV contract.
        QualityBlockDetailMaterialMicrostructureUpgrade.EnsureGeneratedAssets(false);
        EnsureBoardMaterialAssets();

        Material board = RequireMaterial(BoardMaterialPath, BoardMaterialName);
        Material aluminum = RequireMaterial(AluminumMaterialPath, "MAT_AgedAluminum");
        Material steel = RequireMaterial(SteelMaterialPath, "MAT_DarkGalvanizedSteel");
        ValidateMaterialTextureScale(aluminum, AluminumRepeatsPerMeter, "anodized aluminium");
        ValidateMaterialTextureScale(steel, SteelRepeatsPerMeter, "galvanized steel");

        var root = new GameObject(PanelRootName);
        root.transform.SetParent(danchi.transform, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        Transform[] sourceBays = FindBayAssemblies(detailRoot);
        if (sourceBays.Length != ExpectedBayCount)
            throw new InvalidOperationException($"Expected {ExpectedBayCount} generated balcony bay datums, got {sourceBays.Length}.");

        Directory.CreateDirectory(MeshRoot);
        foreach (Transform sourceBay in sourceBays)
        {
            ParseBayIdentity(sourceBay.name, out int floor, out int bayIndex);
            RequireFrontAttachmentDatums(sourceBay);

            var assembly = new GameObject($"BP_BayAssembly_{floor}_{bayIndex}");
            assembly.transform.SetParent(root.transform, false);
            assembly.transform.localPosition = sourceBay.localPosition;
            assembly.transform.localRotation = sourceBay.localRotation;
            assembly.transform.localScale = sourceBay.localScale;

            BuildPanelAssembly(assembly.transform, board, aluminum, steel);
            BuildPerPanelLod(assembly);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        ValidatePreparedState(deepMeshValidation: true);
    }

    [MenuItem("NewTown/QA/Validate Physical Balcony Separation Panels")]
    public static void ValidateOpenScene()
    {
        ValidateContractConfigOnly();
        EnsureScene();
        if (IsAuthoredDanchiActive())
        {
            if (FindSceneObject(PanelRootName) != null)
                throw new InvalidOperationException("Generated balcony separation panels must not coexist with an authored Danchi replacement.");
            Debug.Log("Authored danchi replacement is active; generated balcony separation-panel QA is not applicable. No Visual Fidelity points were assigned.");
            return;
        }

        ValidatePreparedState(deepMeshValidation: true);
        Debug.Log("Balcony separation-panel source/material/metric-UV/LOD QA passed. Native 3840x2160 frontal, oblique, grazing and temporal evidence is still required before any Visual Fidelity points or defect clearance.");
    }

    [MenuItem("NewTown/QA/Validate Balcony Separation Panel Contract")]
    public static void ValidateContractConfigOnly()
    {
        if (!File.Exists(ContractPath))
            throw new InvalidOperationException("Missing balcony separation-panel contract: " + ContractPath);

        string json = File.ReadAllText(ContractPath);
        string[] required =
        {
            "\"schemaVersion\": \"1.1.0\"",
            "\"assemblyRootName\": \"DanchiBalconySeparationPanels\"",
            "\"expectedBayAssemblies\": 30",
            "\"boardThickness\": 0.006",
            "\"frameSection\": 0.032",
            "\"bottomClearance\": 0.06",
            "\"fastenerHeadDiameter\": 0.012",
            "\"metallic\": 0.0",
            "\"roughnessRange\": [0.62, 0.74]",
            "\"normalScale\": 0.32",
            "\"wetness\": 0.0",
            "\"units\": \"meters\"",
            "\"phaseBinsPerAxis\": 8",
            "\"phaseStepMeters\": 0.017",
            "\"perPanelLodGroupRequired\": true",
            "\"compression\": \"Uncompressed\"",
            "\"mipmaps\": true",
            "\"minimumAnisotropy\": 8",
            "\"preCullBehavior\": \"READ_ONLY_FAIL_CLOSED\"",
            "\"visualFidelityPointsAwarded\": 0",
            "\"visualFidelityStatusWithoutRender\": \"UNSCORED_UNTIL_REAL_4K_RENDER\""
        };

        foreach (string token in required)
            if (json.IndexOf(token, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("Balcony separation-panel contract missing/changed token: " + token);
    }

    private static void BuildPanelAssembly(Transform parent, Material board, Material aluminum, Material steel)
    {
        float centerZ = (RearZ + FrontZ) * 0.5f;
        float overallDepth = FrontZ - RearZ;
        float centerY = FloorTopY + BottomClearance + OverallHeight * 0.5f;
        float boardHeight = OverallHeight - FrameSection * 2f;
        float boardDepth = overallDepth - FrameSection * 2f;

        GameObject core = AddMetricBox("BP_DividerPanel_Core", parent,
            new Vector3(PartitionX, centerY, centerZ),
            new Vector3(BoardThickness, boardHeight, boardDepth), board, BoardRepeatsPerMeter);
        ConfigureWeathering(core,
            NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.SunExposed,
            NewTownStainSource.RainLedge | NewTownStainSource.UVExposure,
            0.42f, 0.74f, 0.04f, 0.06f);

        GameObject front = AddMetricBox(FrameNames[0], parent,
            new Vector3(PartitionX, centerY, FrontZ),
            new Vector3(FrameSection, OverallHeight, FrameSection), aluminum, AluminumRepeatsPerMeter);
        GameObject rear = AddMetricBox(FrameNames[1], parent,
            new Vector3(PartitionX, centerY, RearZ),
            new Vector3(FrameSection, OverallHeight, FrameSection), aluminum, AluminumRepeatsPerMeter);
        GameObject top = AddMetricBox(FrameNames[2], parent,
            new Vector3(PartitionX, centerY + OverallHeight * 0.5f - FrameSection * 0.5f, centerZ),
            new Vector3(FrameSection, FrameSection, overallDepth), aluminum, AluminumRepeatsPerMeter);
        GameObject bottom = AddMetricBox(FrameNames[3], parent,
            new Vector3(PartitionX, centerY - OverallHeight * 0.5f + FrameSection * 0.5f, centerZ),
            new Vector3(FrameSection, FrameSection, overallDepth), aluminum, AluminumRepeatsPerMeter);

        foreach (GameObject frame in new[] { front, rear, top, bottom })
            ConfigureWeathering(frame,
                NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.SunExposed,
                NewTownStainSource.RainLedge | NewTownStainSource.UVExposure,
                0.56f, 0.68f, 0.03f, 0.07f);

        float lowY = FloorTopY + BottomClearance + 0.23f;
        float highY = FloorTopY + BottomClearance + OverallHeight - 0.23f;
        float bracketZ = RearZ - RearBracketDepth * 0.5f + 0.016f;
        AddMetricBox(BracketNames[0], parent, new Vector3(PartitionX, lowY, bracketZ),
            new Vector3(0.060f, 0.120f, RearBracketDepth), steel, SteelRepeatsPerMeter);
        AddMetricBox(BracketNames[1], parent, new Vector3(PartitionX, highY, bracketZ),
            new Vector3(0.060f, 0.120f, RearBracketDepth), steel, SteelRepeatsPerMeter);

        float fastenerZ = bracketZ - RearBracketDepth * 0.5f - 0.004f;
        AddMetricFastener(FastenerNames[0], parent, PartitionX - 0.018f, lowY - 0.026f, fastenerZ, steel);
        AddMetricFastener(FastenerNames[1], parent, PartitionX + 0.018f, lowY + 0.026f, fastenerZ, steel);
        AddMetricFastener(FastenerNames[2], parent, PartitionX - 0.018f, highY - 0.026f, fastenerZ, steel);
        AddMetricFastener(FastenerNames[3], parent, PartitionX + 0.018f, highY + 0.026f, fastenerZ, steel);
    }

    private static GameObject AddMetricBox(string name, Transform parent, Vector3 localPosition,
        Vector3 dimensions, Material material, float repeatsPerMeter)
    {
        Phase phase = ResolvePhase(HierarchyPath(parent) + "/" + name);
        Mesh mesh = GetOrCreateMetricBox(dimensions, repeatsPerMeter, phase);
        return AddMeshObject(name, parent, localPosition, Quaternion.identity, mesh, material);
    }

    private static void AddMetricFastener(string name, Transform parent, float x, float y, float z, Material material)
    {
        // QualityBlockDetailMeshLibrary follows Unity Cylinder convention: x/z are diameter and y is
        // half the final axial depth. 0.012 / 0.008 therefore means a 12 mm head and 16 mm total depth.
        Vector3 legacyScale = new Vector3(0.012f, 0.008f, 0.012f);
        Phase phase = ResolvePhase(HierarchyPath(parent) + "/" + name);
        Mesh mesh = GetOrCreateMetricCylinder(legacyScale, SteelRepeatsPerMeter, phase);
        AddMeshObject(name, parent, new Vector3(x, y, z), Quaternion.Euler(90f, 0f, 0f), mesh, material);
    }

    private static GameObject AddMeshObject(string name, Transform parent, Vector3 localPosition,
        Quaternion localRotation, Mesh mesh, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = localRotation;
        go.transform.localScale = Vector3.one;
        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
        return go;
    }

    private static Mesh GetOrCreateMetricBox(Vector3 dimensions, float repeatsPerMeter, Phase phase)
    {
        Mesh source = QualityBlockDetailMeshLibrary.GetChamferedBox(dimensions);
        string path = $"{MeshRoot}/GM_BP_Box_{Key(dimensions.x)}_{Key(dimensions.y)}_{Key(dimensions.z)}_R{Key(repeatsPerMeter)}_PU{phase.u}_PV{phase.v}.asset";
        return GetOrCreateMetricMesh(path, source, repeatsPerMeter, phase, false);
    }

    private static Mesh GetOrCreateMetricCylinder(Vector3 legacyScale, float repeatsPerMeter, Phase phase)
    {
        Mesh source = QualityBlockDetailMeshLibrary.GetBeveledCylinder(legacyScale, true);
        string path = $"{MeshRoot}/GM_BP_Hex_D{Key(legacyScale.x)}_H{Key(legacyScale.y * 2f)}_R{Key(repeatsPerMeter)}_PU{phase.u}_PV{phase.v}.asset";
        return GetOrCreateMetricMesh(path, source, repeatsPerMeter, phase, true);
    }

    private static Mesh GetOrCreateMetricMesh(string path, Mesh source, float repeatsPerMeter, Phase phase, bool cylindrical)
    {
        if (source == null || !source.isReadable)
            throw new InvalidOperationException("Balcony partition metric-UV source mesh is unavailable or unreadable.");

        Mesh baked = UnityEngine.Object.Instantiate(source);
        ApplyMetricUv(baked, repeatsPerMeter, phase, cylindrical);
        baked.name = Path.GetFileNameWithoutExtension(path);

        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(baked, path);
            return baked;
        }

        EditorUtility.CopySerialized(baked, existing);
        UnityEngine.Object.DestroyImmediate(baked);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    private static void ApplyMetricUv(Mesh mesh, float repeatsPerMeter, Phase phase, bool cylindrical)
    {
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        if (normals == null || normals.Length != vertices.Length)
        {
            mesh.RecalculateNormals();
            normals = mesh.normals;
        }

        Vector2 phaseMeters = new Vector2(phase.u * PhaseStepMeters, phase.v * PhaseStepMeters);
        Vector2[] uv = new Vector2[vertices.Length];
        Bounds bounds = mesh.bounds;

        if (cylindrical)
        {
            float radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
            float circumference = Mathf.Max(0.0001f, 2f * Mathf.PI * radius);
            int wrapRepeats = Mathf.Max(1, Mathf.RoundToInt(circumference * repeatsPerMeter));
            float wrapMeters = wrapRepeats / repeatsPerMeter;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];
                Vector3 n = normals[i].normalized;
                if (Mathf.Abs(n.y) > 0.92f)
                {
                    uv[i] = phaseMeters + new Vector2(v.x, v.z);
                    continue;
                }
                float angle = Mathf.Atan2(v.z, v.x);
                if (angle < 0f) angle += Mathf.PI * 2f;
                uv[i] = phaseMeters + new Vector2(angle / (Mathf.PI * 2f) * wrapMeters, v.y - bounds.min.y);
            }
        }
        else
        {
            Vector3[] axes = PreferredManufacturingAxes(bounds.size);
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 n = normals[i].normalized;
                Vector3 vAxis = Vector3.zero;
                foreach (Vector3 axis in axes)
                {
                    Vector3 projected = Vector3.ProjectOnPlane(axis, n);
                    if (projected.sqrMagnitude > 0.04f)
                    {
                        vAxis = projected.normalized;
                        break;
                    }
                }
                if (vAxis.sqrMagnitude < 0.5f)
                    vAxis = Vector3.ProjectOnPlane(Vector3.up, n).normalized;
                if (vAxis.sqrMagnitude < 0.5f)
                    vAxis = Vector3.ProjectOnPlane(Vector3.right, n).normalized;
                Vector3 uAxis = Vector3.Cross(vAxis, n).normalized;
                if (uAxis.sqrMagnitude < 0.5f)
                    throw new InvalidOperationException("Could not create balcony partition metric-UV tangent basis for " + mesh.name);
                uv[i] = phaseMeters + new Vector2(Vector3.Dot(vertices[i], uAxis), Vector3.Dot(vertices[i], vAxis));
            }
        }

        mesh.uv = uv;
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
    }

    private static Vector3[] PreferredManufacturingAxes(Vector3 size)
    {
        return new[]
        {
            new KeyValuePair<float, Vector3>(Mathf.Abs(size.x), Vector3.right),
            new KeyValuePair<float, Vector3>(Mathf.Abs(size.y), Vector3.up),
            new KeyValuePair<float, Vector3>(Mathf.Abs(size.z), Vector3.forward)
        }.OrderByDescending(x => x.Key).Select(x => x.Value).ToArray();
    }

    private static void BuildPerPanelLod(GameObject assembly)
    {
        MeshRenderer[] source = assembly.GetComponentsInChildren<MeshRenderer>(true)
            .Where(x => x.transform.parent == assembly.transform)
            .OrderBy(x => x.gameObject.name, StringComparer.Ordinal)
            .ToArray();
        if (source.Length != 11)
            throw new InvalidOperationException($"{assembly.name}: expected 11 separation-panel source renderers, got {source.Length}.");

        Transform lod1Root = CreateProxyRoot(assembly.transform, "BP_LOD1_Proxy");
        Transform lod2Root = CreateProxyRoot(assembly.transform, "BP_LOD2_Proxy");
        Transform lod3Root = CreateProxyRoot(assembly.transform, "BP_LOD3_Proxy");
        var lod1 = new List<Renderer>();
        var lod2 = new List<Renderer>();
        var lod3 = new List<Renderer>();

        foreach (MeshRenderer renderer in source)
        {
            int through = RetainedThroughLod(renderer.gameObject.name);
            if (through >= 1) lod1.Add(CreateProxyRenderer(renderer, lod1Root, 1));
            if (through >= 2) lod2.Add(CreateProxyRenderer(renderer, lod2Root, 2));
            if (through >= 3) lod3.Add(CreateProxyRenderer(renderer, lod3Root, 3));
        }

        var group = assembly.AddComponent<LODGroup>();
        group.SetLODs(new[]
        {
            new LOD(Lod0Transition, source.Cast<Renderer>().ToArray()),
            new LOD(Lod1Transition, lod1.ToArray()),
            new LOD(Lod2Transition, lod2.ToArray()),
            new LOD(Lod3Cull, lod3.ToArray())
        });
        group.fadeMode = LODFadeMode.CrossFade;
        group.animateCrossFading = true;
        group.RecalculateBounds();
    }

    private static int RetainedThroughLod(string name)
    {
        if (name.IndexOf("WallFastener", StringComparison.OrdinalIgnoreCase) >= 0) return 0;
        if (name.IndexOf("WallBracket", StringComparison.OrdinalIgnoreCase) >= 0) return 1;
        if (name.IndexOf("Frame", StringComparison.OrdinalIgnoreCase) >= 0) return 2;
        if (name.IndexOf("Core", StringComparison.OrdinalIgnoreCase) >= 0) return 3;
        throw new InvalidOperationException("Unknown balcony separation-panel LOD class: " + name);
    }

    private static Transform CreateProxyRoot(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static Renderer CreateProxyRenderer(MeshRenderer source, Transform proxyRoot, int lod)
    {
        var go = new GameObject($"LOD{lod}_{source.gameObject.name}");
        go.transform.SetParent(proxyRoot, false);
        go.transform.localPosition = source.transform.localPosition;
        go.transform.localRotation = source.transform.localRotation;
        go.transform.localScale = source.transform.localScale;
        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = source.sharedMaterials;
        renderer.shadowCastingMode = source.shadowCastingMode;
        renderer.receiveShadows = source.receiveShadows;
        renderer.lightProbeUsage = source.lightProbeUsage;
        renderer.reflectionProbeUsage = source.reflectionProbeUsage;
        renderer.probeAnchor = source.probeAnchor;
        renderer.motionVectorGenerationMode = source.motionVectorGenerationMode;
        return renderer;
    }

    private static void ValidatePreparedState(bool deepMeshValidation)
    {
        ValidateBoardMaterialAssets();
        GameObject danchi = FindSceneObject(DanchiRootName);
        GameObject detailRoot = FindSceneObject(DetailRootName);
        GameObject panelRoot = FindSceneObject(PanelRootName);
        if (danchi == null || detailRoot == null || panelRoot == null)
            throw new InvalidOperationException("Formal balcony separation-panel state is incomplete: Danchi/detail/panel root missing.");
        RequireCompatibleDatumRoots(danchi.transform, detailRoot.transform);
        if (panelRoot.transform.parent != danchi.transform || panelRoot.transform.localPosition.sqrMagnitude > Epsilon * Epsilon ||
            Quaternion.Angle(panelRoot.transform.localRotation, Quaternion.identity) > 0.01f ||
            (panelRoot.transform.localScale - Vector3.one).sqrMagnitude > Epsilon * Epsilon)
            throw new InvalidOperationException("Balcony separation-panel root must remain identity-aligned under Danchi.");

        Material board = RequireMaterial(BoardMaterialPath, BoardMaterialName);
        Material aluminum = RequireMaterial(AluminumMaterialPath, "MAT_AgedAluminum");
        Material steel = RequireMaterial(SteelMaterialPath, "MAT_DarkGalvanizedSteel");
        ValidateMaterialTextureScale(board, BoardRepeatsPerMeter, "partition board");
        ValidateMaterialTextureScale(aluminum, AluminumRepeatsPerMeter, "anodized aluminium");
        ValidateMaterialTextureScale(steel, SteelRepeatsPerMeter, "galvanized steel");

        Transform[] sourceBays = FindBayAssemblies(detailRoot);
        Transform[] assemblies = panelRoot.GetComponentsInChildren<Transform>(true)
            .Where(x => x.parent == panelRoot.transform && x.name.StartsWith("BP_BayAssembly_", StringComparison.Ordinal))
            .OrderBy(x => x.name, StringComparer.Ordinal)
            .ToArray();
        if (sourceBays.Length != ExpectedBayCount || assemblies.Length != ExpectedBayCount)
            throw new InvalidOperationException($"Balcony separation-panel bay coverage drifted: source={sourceBays.Length}, panels={assemblies.Length}, expected={ExpectedBayCount}.");

        var phasePairs = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < assemblies.Length; i++)
        {
            Transform assembly = assemblies[i];
            ParsePanelIdentity(assembly.name, out int floor, out int bayIndex);
            Transform sourceBay = sourceBays.FirstOrDefault(x => x.name == $"HD_BayAssembly_{floor}_{bayIndex}");
            if (sourceBay == null)
                throw new InvalidOperationException("Missing source bay datum for " + assembly.name);
            RequireFrontAttachmentDatums(sourceBay);
            if ((assembly.localPosition - sourceBay.localPosition).sqrMagnitude > GeometryTolerance * GeometryTolerance ||
                Quaternion.Angle(assembly.localRotation, sourceBay.localRotation) > 0.05f ||
                (assembly.localScale - sourceBay.localScale).sqrMagnitude > GeometryTolerance * GeometryTolerance)
                throw new InvalidOperationException(assembly.name + " drifted from its source HD_BayAssembly transform datum.");

            ValidateAssemblyParts(assembly, board, aluminum, steel, phasePairs, deepMeshValidation);
            ValidateAssemblyLod(assembly);
        }

        if (phasePairs.Count < 16)
            throw new InvalidOperationException($"Balcony separation-panel deterministic UV phase diversity too low: {phasePairs.Count} < 16.");
    }

    private static void ValidateAssemblyParts(Transform assembly, Material board, Material aluminum, Material steel,
        HashSet<string> phasePairs, bool deepMeshValidation)
    {
        var errors = new List<string>();
        Transform core = RequireDirectChild(assembly, "BP_DividerPanel_Core", errors);
        ValidatePart(core, board,
            new Vector3(PartitionX, FloorTopY + BottomClearance + OverallHeight * 0.5f, (RearZ + FrontZ) * 0.5f),
            new Vector3(BoardThickness, OverallHeight - FrameSection * 2f, (FrontZ - RearZ) - FrameSection * 2f),
            BoardRepeatsPerMeter, phasePairs, deepMeshValidation, errors);

        foreach (string name in FrameNames)
        {
            Transform part = RequireDirectChild(assembly, name, errors);
            if (part != null)
                ValidateMaterialAndMesh(part, aluminum, AluminumRepeatsPerMeter, phasePairs, deepMeshValidation, errors);
        }
        foreach (string name in BracketNames)
        {
            Transform part = RequireDirectChild(assembly, name, errors);
            if (part != null)
                ValidateMaterialAndMesh(part, steel, SteelRepeatsPerMeter, phasePairs, deepMeshValidation, errors);
        }
        foreach (string name in FastenerNames)
        {
            Transform part = RequireDirectChild(assembly, name, errors);
            if (part != null)
                ValidateMaterialAndMesh(part, steel, SteelRepeatsPerMeter, phasePairs, deepMeshValidation, errors);
        }

        Transform bottom = assembly.Find(FrameNames[3]);
        if (bottom != null)
        {
            float clearance = bottom.localPosition.y - LocalGeometrySize(bottom).y * 0.5f - FloorTopY;
            if (Mathf.Abs(clearance - BottomClearance) > GeometryTolerance)
                errors.Add($"{assembly.name}: frame floor clearance {clearance:0.####} m != {BottomClearance:0.###} ± {GeometryTolerance:0.###} m.");
        }
        Transform front = assembly.Find(FrameNames[0]);
        if (front != null && (Mathf.Abs(front.localPosition.z - FrontZ) > GeometryTolerance || front.localPosition.z > -6.26f))
            errors.Add(assembly.name + ": front frame drifted out of the bracket zone or into the railing plane.");
        Transform rear = assembly.Find(FrameNames[1]);
        if (rear != null && Mathf.Abs(rear.localPosition.z - RearZ) > GeometryTolerance)
            errors.Add(assembly.name + ": rear frame drifted from the facade-side endpoint.");

        if (errors.Count > 0)
            throw new InvalidOperationException("Balcony separation-panel assembly QA FAILED:\n - " + string.Join("\n - ", errors));
    }

    private static void ValidatePart(Transform part, Material expectedMaterial, Vector3 expectedPosition,
        Vector3 expectedSize, float repeats, HashSet<string> phasePairs, bool deep, List<string> errors)
    {
        if (part == null) return;
        if ((part.localPosition - expectedPosition).sqrMagnitude > GeometryTolerance * GeometryTolerance)
            errors.Add(part.parent.name + "/" + part.name + ": installation position drifted.");
        if (!Approximately(LocalGeometrySize(part), expectedSize, GeometryTolerance))
            errors.Add(part.parent.name + "/" + part.name + ": physical dimensions drifted.");
        ValidateMaterialAndMesh(part, expectedMaterial, repeats, phasePairs, deep, errors);
    }

    private static void ValidateMaterialAndMesh(Transform part, Material expectedMaterial, float repeats,
        HashSet<string> phasePairs, bool deep, List<string> errors)
    {
        MeshRenderer renderer = part.GetComponent<MeshRenderer>();
        MeshFilter filter = part.GetComponent<MeshFilter>();
        if (renderer == null || !renderer.enabled || !part.gameObject.activeInHierarchy || filter == null || filter.sharedMesh == null)
        {
            errors.Add(part.parent.name + "/" + part.name + ": active renderer/mesh state incomplete.");
            return;
        }
        if (renderer.sharedMaterial != expectedMaterial)
            errors.Add(part.parent.name + "/" + part.name + ": canonical material binding drifted.");
        Mesh mesh = filter.sharedMesh;
        string path = AssetDatabase.GetAssetPath(mesh);
        if (string.IsNullOrEmpty(path) || !path.StartsWith(MeshRoot + "/", StringComparison.Ordinal) ||
            !mesh.name.StartsWith("GM_BP_", StringComparison.Ordinal))
            errors.Add(part.parent.name + "/" + part.name + ": source mesh is not an authored balcony-partition mesh.");
        if (mesh.uv == null || mesh.uv.Length != mesh.vertexCount || mesh.tangents == null || mesh.tangents.Length != mesh.vertexCount)
            errors.Add(part.parent.name + "/" + part.name + ": metric UV/tangent data incomplete.");
        Phase expectedPhase = ResolvePhase(HierarchyPath(part.parent) + "/" + part.name);
        phasePairs.Add(expectedPhase.u + ":" + expectedPhase.v);
        if (mesh.name.IndexOf("_PU" + expectedPhase.u + "_PV" + expectedPhase.v, StringComparison.Ordinal) < 0 ||
            mesh.name.IndexOf("_R" + Key(repeats), StringComparison.Ordinal) < 0)
            errors.Add(part.parent.name + "/" + part.name + ": mesh phase/material-scale identity drifted.");
        if (deep && !MeshUvIsFinite(mesh))
            errors.Add(part.parent.name + "/" + part.name + ": metric UV contains non-finite values.");
    }

    private static void ValidateAssemblyLod(Transform assembly)
    {
        LODGroup group = assembly.GetComponent<LODGroup>();
        if (group == null)
            throw new InvalidOperationException(assembly.name + ": per-panel LODGroup missing.");
        LOD[] lods = group.GetLODs();
        if (lods.Length != 4 || lods[0].renderers.Length != 11 || lods[1].renderers.Length != 7 ||
            lods[2].renderers.Length != 5 || lods[3].renderers.Length != 1)
            throw new InvalidOperationException($"{assembly.name}: LOD renderer counts drifted; expected 11/7/5/1.");
        if (Mathf.Abs(lods[0].screenRelativeTransitionHeight - Lod0Transition) > Epsilon ||
            Mathf.Abs(lods[1].screenRelativeTransitionHeight - Lod1Transition) > Epsilon ||
            Mathf.Abs(lods[2].screenRelativeTransitionHeight - Lod2Transition) > Epsilon ||
            Mathf.Abs(lods[3].screenRelativeTransitionHeight - Lod3Cull) > Epsilon)
            throw new InvalidOperationException(assembly.name + ": LOD transition heights drifted from 0.18/0.08/0.03/0.008.");
        if (group.fadeMode != LODFadeMode.CrossFade || !group.animateCrossFading)
            throw new InvalidOperationException(assembly.name + ": animated LOD cross-fade is required.");
        if (!lods[3].renderers.All(x => x != null && x.gameObject.name.IndexOf("Core", StringComparison.OrdinalIgnoreCase) >= 0))
            throw new InvalidOperationException(assembly.name + ": LOD3 must retain only the large board-core occlusion proxy.");
    }

    private static void EnsureBoardMaterialAssets()
    {
        Directory.CreateDirectory(TextureRoot);
        WriteTextureIfChanged(BoardNormalPath, BuildNormalPng());
        WriteTextureIfChanged(BoardMaskPath, BuildMaskPng());
        ConfigureImporter(BoardNormalPath, true);
        ConfigureImporter(BoardMaskPath, false);

        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(BoardNormalPath);
        Texture2D mask = AssetDatabase.LoadAssetAtPath<Texture2D>(BoardMaskPath);
        if (normal == null || mask == null)
            throw new InvalidOperationException("Unity failed to import balcony partition physical-data textures.");

        Shader shader = Shader.Find("Standard");
        if (shader == null)
            throw new InvalidOperationException("Unity Standard shader is unavailable for the balcony partition material.");
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(BoardMaterialPath);
        if (mat == null)
        {
            mat = new Material(shader) { name = BoardMaterialName };
            AssetDatabase.CreateAsset(mat, BoardMaterialPath);
        }
        else mat.shader = shader;

        mat.color = new Color(0.64f, 0.63f, 0.58f, 1f);
        mat.SetTexture("_BumpMap", normal);
        mat.SetTextureScale("_BumpMap", Vector2.one * BoardRepeatsPerMeter);
        mat.SetFloat("_BumpScale", BoardNormalScale);
        mat.EnableKeyword("_NORMALMAP");
        mat.SetTexture("_MetallicGlossMap", mask);
        mat.SetTextureScale("_MetallicGlossMap", Vector2.one * BoardRepeatsPerMeter);
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Glossiness", 0.32f);
        mat.SetFloat("_GlossMapScale", 1f);
        mat.SetFloat("_SmoothnessTextureChannel", 0f);
        mat.EnableKeyword("_METALLICGLOSSMAP");
        mat.SetTextureScale("_MainTex", Vector2.one * BoardRepeatsPerMeter);
        mat.SetTextureOffset("_MainTex", Vector2.zero);
        mat.SetTextureOffset("_BumpMap", Vector2.zero);
        mat.SetTextureOffset("_MetallicGlossMap", Vector2.zero);
        mat.SetColor("_EmissionColor", Color.black);
        mat.DisableKeyword("_EMISSION");
        mat.SetFloat("_Mode", 0f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.One);
        mat.SetFloat("_DstBlend", (float)BlendMode.Zero);
        mat.SetFloat("_ZWrite", 1f);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.SetOverrideTag("RenderType", "Opaque");
        mat.renderQueue = -1;
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ValidateBoardMaterialAssets();
    }

    private static void ValidateBoardMaterialAssets()
    {
        Material mat = RequireMaterial(BoardMaterialPath, BoardMaterialName);
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(BoardNormalPath);
        Texture2D mask = AssetDatabase.LoadAssetAtPath<Texture2D>(BoardMaskPath);
        if (normal == null || mask == null || mat.shader == null || mat.shader.name != "Standard")
            throw new InvalidOperationException("Balcony partition board material/physical-data assets are incomplete.");
        if (mat.GetTexture("_BumpMap") != normal || mat.GetTexture("_MetallicGlossMap") != mask ||
            !mat.IsKeywordEnabled("_NORMALMAP") || !mat.IsKeywordEnabled("_METALLICGLOSSMAP"))
            throw new InvalidOperationException("Balcony partition board lost its canonical Standard PBR map binding.");
        if (Mathf.Abs(mat.GetFloat("_BumpScale") - BoardNormalScale) > Epsilon || Mathf.Abs(mat.GetFloat("_Metallic")) > Epsilon)
            throw new InvalidOperationException("Balcony partition board normal scale/metallic state drifted.");
        if (mat.IsKeywordEnabled("_EMISSION") || mat.GetColor("_EmissionColor").maxColorComponent > Epsilon ||
            mat.renderQueue > 2500 || mat.GetFloat("_Mode") > Epsilon || mat.GetFloat("_ZWrite") < 0.999f)
            throw new InvalidOperationException("Balcony partition board must remain opaque, non-emissive and depth-writing.");
        Color c = mat.color;
        if (Mathf.Abs(c.r - 0.64f) > 0.02f || Mathf.Abs(c.g - 0.63f) > 0.02f || Mathf.Abs(c.b - 0.58f) > 0.02f)
            throw new InvalidOperationException("Balcony partition board base albedo drifted outside the authored dry-mineral range.");
        ValidateMaterialTextureScale(mat, BoardRepeatsPerMeter, "partition board");
        ValidateImporter(BoardNormalPath, TextureImporterType.NormalMap);
        ValidateImporter(BoardMaskPath, TextureImporterType.Default);
    }

    private static void ValidateMaterialTextureScale(Material material, float repeatsPerMeter, string label)
    {
        Vector2 expected = Vector2.one * repeatsPerMeter;
        foreach (string property in new[] { "_MainTex", "_BumpMap", "_MetallicGlossMap" })
        {
            if (!material.HasProperty(property))
                throw new InvalidOperationException(label + " material missing Standard texture property " + property);
            if ((material.GetTextureScale(property) - expected).sqrMagnitude > Epsilon * Epsilon ||
                material.GetTextureOffset(property).sqrMagnitude > Epsilon * Epsilon)
                throw new InvalidOperationException($"{label}/{property} must remain {repeatsPerMeter:0.##} repeats per metric UV metre with zero offset.");
        }
    }

    private static byte[] BuildNormalPng()
    {
        var height = new float[TextureSize * TextureSize];
        for (int y = 0; y < TextureSize; y++)
        for (int x = 0; x < TextureSize; x++)
        {
            float u = x / (float)TextureSize;
            float v = y / (float)TextureSize;
            float a = PeriodicNoise01(u, v, 1709) - 0.5f;
            float b = PeriodicNoise01(u, v, 1877) - 0.5f;
            float fibre = Mathf.Sin(2f * Mathf.PI * (u * 23f + v * 17f + 0.31f));
            height[y * TextureSize + x] = 0.5f + a * 0.040f + b * 0.025f + fibre * 0.008f;
        }

        var pixels = new Color32[height.Length];
        const float gain = 7.5f;
        for (int y = 0; y < TextureSize; y++)
        {
            int ym = (y + TextureSize - 1) % TextureSize;
            int yp = (y + 1) % TextureSize;
            for (int x = 0; x < TextureSize; x++)
            {
                int xm = (x + TextureSize - 1) % TextureSize;
                int xp = (x + 1) % TextureSize;
                float dx = height[y * TextureSize + xp] - height[y * TextureSize + xm];
                float dy = height[yp * TextureSize + x] - height[ym * TextureSize + x];
                Vector3 n = new Vector3(-dx * gain, -dy * gain, 1f).normalized;
                pixels[y * TextureSize + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            }
        }
        return EncodePng(pixels);
    }

    private static byte[] BuildMaskPng()
    {
        var pixels = new Color32[TextureSize * TextureSize];
        for (int y = 0; y < TextureSize; y++)
        for (int x = 0; x < TextureSize; x++)
        {
            float u = x / (float)TextureSize;
            float v = y / (float)TextureSize;
            float roughness = Mathf.Lerp(0.62f, 0.74f, PeriodicNoise01(u, v, 2081));
            pixels[y * TextureSize + x] = new Color(0f, 0f, 0f, 1f - roughness);
        }
        return EncodePng(pixels);
    }

    private static byte[] EncodePng(Color32[] pixels)
    {
        var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false, true);
        try
        {
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture.EncodeToPNG();
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }

    private static float PeriodicNoise01(float u, float v, int seed)
    {
        float p = (seed % 997) * 0.0137f;
        float n = Mathf.Sin(2f * Mathf.PI * (u * 13f + v * 17f + p)) * 0.31f +
                  Mathf.Sin(2f * Mathf.PI * (u * 29f - v * 11f + p * 1.7f)) * 0.21f +
                  Mathf.Cos(2f * Mathf.PI * (u * 41f + v * 37f + p * 0.73f)) * 0.14f;
        return Mathf.Clamp01(0.5f + n);
    }

    private static void WriteTextureIfChanged(string path, byte[] png)
    {
        if (File.Exists(path) && File.ReadAllBytes(path).SequenceEqual(png)) return;
        File.WriteAllBytes(path, png);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
    }

    private static void ConfigureImporter(string path, bool normalMap)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException("TextureImporter unavailable for balcony partition texture: " + path);
        importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = false;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.crunchedCompression = false;
        importer.maxTextureSize = TextureSize;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 8;
        TextureImporterPlatformSettings standalone = importer.GetPlatformTextureSettings("Standalone");
        if (standalone.overridden)
        {
            standalone.overridden = false;
            importer.SetPlatformTextureSettings(standalone);
        }
        importer.SaveAndReimport();
    }

    private static void ValidateImporter(string path, TextureImporterType expectedType)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null || importer.textureType != expectedType || importer.sRGBTexture ||
            importer.textureCompression != TextureImporterCompression.Uncompressed || importer.crunchedCompression ||
            importer.maxTextureSize < TextureSize || !importer.mipmapEnabled || importer.wrapMode != TextureWrapMode.Repeat ||
            importer.filterMode != FilterMode.Trilinear || importer.anisoLevel < 8 ||
            importer.alphaSource != TextureImporterAlphaSource.FromInput)
            throw new InvalidOperationException("Balcony partition physical-data importer state drifted: " + path);
        if (importer.GetPlatformTextureSettings("Standalone").overridden)
            throw new InvalidOperationException("Balcony partition physical-data texture may not use a Standalone override: " + path);
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null || texture.width != TextureSize || texture.height != TextureSize)
            throw new InvalidOperationException("Balcony partition physical-data texture must resolve at 1024x1024: " + path);
    }

    private static void ConfigureWeathering(GameObject go, NewTownSurfaceExposure exposure,
        NewTownStainSource sources, float rain, float sun, float splash, float contact)
    {
        QualityBlockWeatheringSurface metadata = go.GetComponent<QualityBlockWeatheringSurface>();
        if (metadata == null) metadata = go.AddComponent<QualityBlockWeatheringSurface>();
        metadata.Configure(exposure, sources, rain, sun, splash, contact);
    }

    private static void RequireCompatibleDatumRoots(Transform danchi, Transform detail)
    {
        if (detail.parent != danchi || detail.localPosition.sqrMagnitude > Epsilon * Epsilon ||
            Quaternion.Angle(detail.localRotation, Quaternion.identity) > 0.01f ||
            (detail.localScale - Vector3.one).sqrMagnitude > Epsilon * Epsilon)
            throw new InvalidOperationException("DanchiHighDetail no longer shares the Danchi local datum required by balcony separation-panel installation.");
    }

    private static void RequireFrontAttachmentDatums(Transform sourceBay)
    {
        Transform low = sourceBay.Find("HD_DividerBracketLow");
        Transform high = sourceBay.Find("HD_DividerBracketHigh");
        if (low == null || high == null)
            throw new InvalidOperationException(sourceBay.name + ": existing front divider brackets are missing; do not synthesize a floating partition.");
        if (Mathf.Abs(low.localPosition.x - PartitionX) > GeometryTolerance ||
            Mathf.Abs(high.localPosition.x - PartitionX) > GeometryTolerance ||
            Mathf.Abs(low.localPosition.z + 6.24f) > 0.015f || Mathf.Abs(high.localPosition.z + 6.24f) > 0.015f)
            throw new InvalidOperationException(sourceBay.name + ": front divider-bracket datum drifted from the known generated construction interface.");
    }

    private static Transform[] FindBayAssemblies(GameObject detailRoot)
    {
        return detailRoot.GetComponentsInChildren<Transform>(true)
            .Where(x => x.parent == detailRoot.transform && x.name.StartsWith("HD_BayAssembly_", StringComparison.Ordinal))
            .OrderBy(x => x.name, StringComparer.Ordinal)
            .ToArray();
    }

    private static void ParseBayIdentity(string name, out int floor, out int bay)
    {
        string[] p = name.Split('_');
        if (p.Length != 4 || p[0] != "HD" || p[1] != "BayAssembly" ||
            !int.TryParse(p[2], out floor) || !int.TryParse(p[3], out bay))
            throw new InvalidOperationException("Unexpected source balcony bay name: " + name);
    }

    private static void ParsePanelIdentity(string name, out int floor, out int bay)
    {
        string[] p = name.Split('_');
        if (p.Length != 4 || p[0] != "BP" || p[1] != "BayAssembly" ||
            !int.TryParse(p[2], out floor) || !int.TryParse(p[3], out bay))
            throw new InvalidOperationException("Unexpected balcony separation-panel assembly name: " + name);
    }

    private static Transform RequireDirectChild(Transform parent, string name, List<string> errors)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name) return child;
        }
        errors.Add(parent.name + ": required panel part missing: " + name);
        return null;
    }

    private static Material RequireMaterial(string path, string expectedName)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null || !string.Equals(material.name, expectedName, StringComparison.Ordinal))
            throw new InvalidOperationException("Required balcony separation-panel material missing/drifted: " + path);
        return material;
    }

    private static Vector3 LocalGeometrySize(Transform part)
    {
        MeshFilter filter = part.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) return Vector3.zero;
        Vector3 size = filter.sharedMesh.bounds.size;
        Vector3 scale = part.localScale;
        return new Vector3(Mathf.Abs(size.x * scale.x), Mathf.Abs(size.y * scale.y), Mathf.Abs(size.z * scale.z));
    }

    private static bool MeshUvIsFinite(Mesh mesh)
    {
        foreach (Vector2 uv in mesh.uv)
            if (float.IsNaN(uv.x) || float.IsInfinity(uv.x) || float.IsNaN(uv.y) || float.IsInfinity(uv.y))
                return false;
        return true;
    }

    private static bool Approximately(Vector3 a, Vector3 b, float tolerance)
    {
        return Mathf.Abs(a.x - b.x) <= tolerance && Mathf.Abs(a.y - b.y) <= tolerance && Mathf.Abs(a.z - b.z) <= tolerance;
    }

    private struct Phase { public int u; public int v; }

    private static Phase ResolvePhase(string key)
    {
        return new Phase
        {
            u = (int)(Fnv1a(key, 2166136261u) % PhaseBins),
            v = (int)(Fnv1a(key + "|v", 2166136261u) % PhaseBins)
        };
    }

    private static uint Fnv1a(string text, uint seed)
    {
        uint h = seed;
        for (int i = 0; i < text.Length; i++)
        {
            h ^= text[i];
            h *= 16777619u;
        }
        return h;
    }

    private static int Key(float value) => Mathf.RoundToInt(Mathf.Abs(value) * 10000f);

    private static string HierarchyPath(Transform transform)
    {
        var parts = new List<string>();
        for (Transform t = transform; t != null; t = t.parent) parts.Add(t.name);
        parts.Reverse();
        return string.Join("/", parts);
    }

    private static void OnCameraPreCull(Camera camera)
    {
        if (camera == null || camera.gameObject == null || !camera.gameObject.scene.IsValid() || camera.gameObject.scene.path != ScenePath)
            return;
        bool reflection = camera.cameraType == CameraType.Reflection;
        string targetName = camera.targetTexture != null ? camera.targetTexture.name ?? string.Empty : string.Empty;
        bool formal = targetName.StartsWith("QA4K_", StringComparison.Ordinal) ||
                      targetName.StartsWith("QATemporal_", StringComparison.Ordinal) ||
                      targetName.StartsWith("QAPreparedTemporal_", StringComparison.Ordinal);
        if (!reflection && !formal) return;

        ValidateContractConfigOnly();
        if (IsAuthoredDanchiActive())
        {
            if (FindSceneObject(PanelRootName) != null)
                throw new InvalidOperationException("Formal evidence blocked: generated balcony panels coexist with authored Danchi art.");
            return;
        }
        // Formal callbacks are validation-only. They never regenerate textures, meshes, LODs or transforms.
        ValidatePreparedState(deepMeshValidation: false);
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
            .FirstOrDefault(x => x != null && x.scene.IsValid() && x.scene.path == ScenePath && x.name == name);
    }
}
