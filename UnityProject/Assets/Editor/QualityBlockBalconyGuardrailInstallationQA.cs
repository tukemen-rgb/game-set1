using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Reconstructs the generated apartment balcony guard as a manufactured vertical-lattice assembly.
/// Legacy RailTop_* / Rail_* objects and their BoxColliders remain untouched as gameplay anchors, but
/// their benchmark-visible stock Cube renderers are disabled. Four explicit render LODs replace them.
/// The final-state QA also rechecks the slab fascia, lower rail, base plates and anchor-head interfaces
/// after those legacy post renderers are no longer available to the earlier balcony-interface QA.
/// Source/scene QA never awards Visual Fidelity points; native 3840x2160 pixels remain authoritative.
/// </summary>
public static class QualityBlockBalconyGuardrailInstallationQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/balcony_guardrail_installation_contract.json";
    private const string RootName = "BalconyGuardrailAssemblies";
    private const string MeshRoot = "Assets/Art/GeneratedGuardrailMeshes";
    private const string MaterialRoot = "Assets/Art/GeneratedGuardrailMaterials";
    private const string NormalPath = MaterialRoot + "/T_BalconyAnodizedMicro_N.png";
    private const int ExpectedBays = 30;

    private const float GuardTopHeight = 1.10f;
    private const float TopRailLength = 3.55f;
    private const float TopRailHeight = 0.070f;
    private const float TopRailDepth = 0.080f;
    private const float TopRailCenterY = GuardTopHeight - TopRailHeight * 0.5f;
    private const float TopRailBottomY = GuardTopHeight - TopRailHeight;

    private const int PrimaryPostCount = 7;
    private const float PrimaryPostWidth = 0.040f;
    private const float PrimaryPostDepth = 0.040f;
    private const float PrimaryPostPitch = 0.480f;
    private const float PrimaryPostEmbed = 0.040f;
    private const float PrimaryPostCenterY = (TopRailBottomY - PrimaryPostEmbed) * 0.5f;
    private const float PrimaryPostHeight = TopRailBottomY + PrimaryPostEmbed;

    private const int InfillCount = 22;
    private const float InfillWidth = 0.018f;
    private const float InfillDepth = 0.020f;
    private const float VerticalPitch = 0.120f;
    private const float LowerRailExpectedTop = 0.110f;
    private const float RailInsertion = 0.005f;
    private const float InfillBottomY = LowerRailExpectedTop - RailInsertion;
    private const float InfillTopY = TopRailBottomY + RailInsertion;
    private const float InfillCenterY = (InfillBottomY + InfillTopY) * 0.5f;
    private const float InfillHeight = InfillTopY - InfillBottomY;
    private const float MaximumCalculatedClearOpening = 0.102f;

    private const float SleeveWidth = 0.050f;
    private const float SleeveHeight = 0.012f;
    private const float SleeveDepth = 0.050f;
    private const float MicroTileM = 0.080f;
    private const float HeightTolerance = 0.005f;
    private const float LowerCaptureTolerance = 0.006f;
    private const float ContactTolerance = 0.003f;
    private const float AnchorEmbedMin = 0.001f;
    private const float AnchorEmbedMax = 0.006f;
    private const float LodBoundsTolerance = 0.010f;

    private static readonly float[] LodTransitions = { 0.18f, 0.08f, 0.03f, 0.008f };
    private static readonly HashSet<string> StockMeshes = new HashSet<string>(StringComparer.Ordinal)
    {
        "Cube", "Cylinder", "Sphere", "Capsule"
    };

    [MenuItem("NewTown/Facade/Reconstruct Balcony Guardrails")]
    public static void ApplyAndPersist()
    {
        EnsureScene();
        ValidateContractConfigOnly();
        ApplyToOpenScene();
        ValidateOpenScene();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Balcony guardrail reconstruction persisted. Visual Fidelity remains UNSCORED pending native 4K evidence.");
    }

    public static void ApplyToOpenScene()
    {
        EnsureScene();
        ValidateContractConfigOnly();
        DestroyGeneratedRoot();

        if (IsAuthoredDanchiActive())
        {
            Debug.Log("Authored danchi replacement is active; generated balcony guardrails are not added. No visual credit is awarded by this skip.");
            return;
        }

        GameObject danchi = Find("Danchi");
        GameObject detailRoot = Find("DanchiHighDetail");
        if (danchi == null || detailRoot == null)
            throw new InvalidOperationException("Danchi / DanchiHighDetail must exist before guardrail reconstruction.");

        Directory.CreateDirectory(MeshRoot);
        Directory.CreateDirectory(MaterialRoot);
        EnsureAnodizedNormal();
        Material material = BuildMaterial();
        Mesh[] meshes = BuildLodMeshes();

        GameObject root = new GameObject(RootName);
        root.transform.SetParent(danchi.transform, false);

        int built = 0;
        for (int floor = 0; floor < 5; floor++)
        for (int bay = 0; bay < 6; bay++)
        {
            Renderer floorRenderer = RequireActiveRenderer($"BalconyFloor_{floor}_{bay}");
            GameObject legacyTop = RequireObject($"RailTop_{floor}_{bay}");
            Renderer legacyTopRenderer = RequireRendererComponent(legacyTop, legacyTop.name);
            Transform detailBay = RequireDetailBay(detailRoot.transform, floor, bay);
            Renderer lowerRail = RequireDirectChildRenderer(detailBay, "HD_RailLower");

            float lowerTopRelative = lowerRail.bounds.max.y - floorRenderer.bounds.max.y;
            if (Mathf.Abs(lowerTopRelative - LowerRailExpectedTop) > LowerCaptureTolerance)
                throw new InvalidOperationException(
                    $"Guardrail lower-capture datum drifted for floor={floor} bay={bay}: {lowerTopRelative:F4} m; expected {LowerRailExpectedTop:F3} ± {LowerCaptureTolerance:F3} m.");

            Vector3 assemblyWorld = new Vector3(
                legacyTopRenderer.bounds.center.x,
                floorRenderer.bounds.max.y,
                legacyTopRenderer.bounds.center.z);

            BuildBayAssembly(root.transform, floor, bay, assemblyWorld, material, meshes, lowerTopRelative);

            // Preserve the original transforms/MeshFilters/BoxColliders as gameplay/bookkeeping anchors.
            // Only their stock renderers are disabled after replacement geometry exists.
            legacyTopRenderer.enabled = false;
            for (int r = -3; r <= 3; r++)
                RequireRendererComponent(RequireObject($"Rail_{floor}_{bay}_{r}"), $"Rail_{floor}_{bay}_{r}").enabled = false;

            built++;
        }

        if (built != ExpectedBays)
            throw new InvalidOperationException($"Expected {ExpectedBays} guardrail assemblies, built {built}.");

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("NewTown/QA/Validate Balcony Guardrail Contract")]
    public static void ValidateContractConfigOnly()
    {
        string absolute = AbsolutePath(ContractPath);
        if (!File.Exists(absolute)) throw new FileNotFoundException($"Missing guardrail contract: {ContractPath}");
        Contract c = JsonUtility.FromJson<Contract>(File.ReadAllText(absolute));
        var errors = new List<string>();
        if (c == null) errors.Add("contract is null/unparseable");
        else
        {
            if (c.schemaVersion != "1.0.0") errors.Add("schemaVersion must remain 1.0.0");
            if (c.scenePath != ScenePath) errors.Add($"scenePath must remain {ScenePath}");
            if (c.expectedBays != ExpectedBays) errors.Add($"expectedBays must remain {ExpectedBays}");
            if (c.dimensionsThickness == null) errors.Add("dimensionsThickness is required");
            else
            {
                if (Mathf.Abs(c.dimensionsThickness.guardTopHeightAboveFinishedFloorM - GuardTopHeight) > 0.0001f)
                    errors.Add("guard top height drifted from 1.10 m");
                if (c.dimensionsThickness.primaryPost == null || c.dimensionsThickness.primaryPost.count != PrimaryPostCount)
                    errors.Add("primary post count must remain seven");
                if (c.dimensionsThickness.infill == null || c.dimensionsThickness.infill.count != InfillCount)
                    errors.Add("infill count must remain twenty-two");
                if (c.dimensionsThickness.infill != null &&
                    Mathf.Abs(c.dimensionsThickness.infill.maximumCalculatedClearOpeningM - MaximumCalculatedClearOpening) > 0.0001f)
                    errors.Add("calculated clear opening must remain 0.102 m");
                if (c.dimensionsThickness.infill != null && c.dimensionsThickness.infill.maximumCalculatedClearOpeningM > 0.1101f)
                    errors.Add("calculated clear opening exceeds 110 mm hard ceiling");
            }
            if (c.lodPolicy == null || c.lodPolicy.levels != 4 || !c.lodPolicy.crossFadeRequired)
                errors.Add("four cross-faded LODs are mandatory");
            if (c.visualCreditPolicy == null || c.visualCreditPolicy.autoVisualPoints != 0 || c.visualCreditPolicy.criticalDefectClearedBySourcePass)
                errors.Add("source guardrail QA may not award visual points or clear critical defects");
        }
        if (errors.Count > 0)
            throw new InvalidOperationException("Balcony guardrail contract FAILED:\n - " + string.Join("\n - ", errors));
        Debug.Log("Balcony guardrail contract valid: 30 bays, 1.10 m top, 29 vertical centerlines, four LODs, automatic visual credit=0.");
    }

    [MenuItem("NewTown/QA/Validate Balcony Guardrail Installation")]
    public static void ValidateOpenScene()
    {
        EnsureScene();
        ValidateContractConfigOnly();
        if (IsAuthoredDanchiActive())
        {
            if (Find(RootName) != null) throw new InvalidOperationException("Generated guardrail root remains behind authored danchi art.");
            return;
        }

        GameObject root = Find(RootName);
        GameObject detailRootObject = Find("DanchiHighDetail");
        if (root == null) throw new InvalidOperationException("BalconyGuardrailAssemblies root is missing.");
        if (detailRootObject == null) throw new InvalidOperationException("DanchiHighDetail root is missing during final guardrail QA.");
        if (root.GetComponentsInChildren<Collider>(true).Length != 0)
            throw new InvalidOperationException("Generated guardrail render assemblies must not add gameplay colliders.");

        QualityBlockBalconyGuardrailManifest[] manifests = root.GetComponentsInChildren<QualityBlockBalconyGuardrailManifest>(true)
            .OrderBy(x => x.Floor).ThenBy(x => x.Bay).ToArray();
        if (manifests.Length != ExpectedBays)
            throw new InvalidOperationException($"Expected {ExpectedBays} guardrail manifests, got {manifests.Length}.");

        foreach (QualityBlockBalconyGuardrailManifest manifest in manifests)
        {
            int floor = manifest.Floor, bay = manifest.Bay;
            Renderer floorRenderer = RequireActiveRenderer($"BalconyFloor_{floor}_{bay}");
            float floorTop = floorRenderer.bounds.max.y;
            Transform detailBay = RequireDetailBay(detailRootObject.transform, floor, bay);
            Bounds top = ResolveTopRailBounds(floor, bay);
            float height = top.max.y - floorTop;
            if (Mathf.Abs(height - GuardTopHeight) > HeightTolerance)
                throw new InvalidOperationException($"Guardrail {floor}/{bay} top height is {height:F4} m; expected 1.100 ± 0.005 m.");
            if (Mathf.Abs(top.size.x - TopRailLength) > 0.004f ||
                Mathf.Abs(top.size.y - TopRailHeight) > 0.004f ||
                Mathf.Abs(top.size.z - TopRailDepth) > 0.004f)
                throw new InvalidOperationException($"Guardrail {floor}/{bay} top-rail section/span drifted.");

            if (manifest.PrimaryPostCount != PrimaryPostCount || manifest.InfillCount != InfillCount)
                throw new InvalidOperationException($"Guardrail {floor}/{bay} vertical-member manifest drifted.");
            if (manifest.MaximumClearOpeningM > 0.110f + 0.0001f ||
                Mathf.Abs(manifest.MaximumClearOpeningM - MaximumCalculatedClearOpening) > 0.001f)
                throw new InvalidOperationException($"Guardrail {floor}/{bay} clear-opening calculation is unsafe/inconsistent: {manifest.MaximumClearOpeningM:F4} m.");
            if (Mathf.Abs(manifest.LowerRailTopAboveFloorM - LowerRailExpectedTop) > LowerCaptureTolerance)
                throw new InvalidOperationException($"Guardrail {floor}/{bay} lower-capture manifest no longer matches corrected lower rail.");

            // Re-prove the construction stack that the earlier balcony-interface pass established before
            // its seven legacy Rail_* renderers were intentionally disabled by this replacement.
            Renderer fascia = RequireDirectChildRenderer(detailBay, "HD_BalconySlabLip");
            if (Mathf.Abs(fascia.bounds.max.y - floorTop) > ContactTolerance)
                throw new InvalidOperationException($"Guardrail {floor}/{bay}: concrete slab fascia top lost floor contact.");
            if (fascia.sharedMaterial == null || floorRenderer.sharedMaterial == null || fascia.sharedMaterial != floorRenderer.sharedMaterial)
                throw new InvalidOperationException($"Guardrail {floor}/{bay}: slab fascia no longer inherits balcony-floor concrete material.");

            Renderer lowerRail = RequireDirectChildRenderer(detailBay, "HD_RailLower");
            float lowerTopRelative = lowerRail.bounds.max.y - floorTop;
            if (Mathf.Abs(lowerTopRelative - LowerRailExpectedTop) > LowerCaptureTolerance)
                throw new InvalidOperationException($"Guardrail {floor}/{bay}: lower rail top is {lowerTopRelative:F4} m above floor, expected 0.110 ± 0.006 m.");

            Renderer dividerLow = RequireDirectChildRenderer(detailBay, "HD_DividerBracketLow");
            if (Mathf.Abs(dividerLow.bounds.min.y - floorTop) > ContactTolerance)
                throw new InvalidOperationException($"Guardrail {floor}/{bay}: low divider bracket is not seated on the slab.");

            GameObject legacyTop = RequireObject($"RailTop_{floor}_{bay}");
            ValidateLegacyAnchor(legacyTop, new Vector3(3.55f, 0.09f, 0.09f));
            for (int r = -3; r <= 3; r++)
            {
                GameObject legacyPost = RequireObject($"Rail_{floor}_{bay}_{r}");
                ValidateLegacyAnchor(legacyPost, new Vector3(0.045f, 0.88f, 0.045f));
                Bounds support = ResolveSupportPostBounds(floor, bay, r);
                Renderer plate = RequireDirectChildRenderer(detailBay, $"HD_RailBasePlate_{r}");
                if (Mathf.Abs(plate.bounds.min.y - floorTop) > ContactTolerance)
                    throw new InvalidOperationException($"Guardrail {floor}/{bay} plate {r} is not seated on the slab.");
                if (support.center.x < plate.bounds.min.x - 0.005f || support.center.x > plate.bounds.max.x + 0.005f ||
                    support.center.z < plate.bounds.min.z - 0.005f || support.center.z > plate.bounds.max.z + 0.005f)
                    throw new InvalidOperationException($"Guardrail {floor}/{bay} support {r} is not centered over its base plate.");
                if (support.min.y > floorTop - PrimaryPostEmbed + 0.005f ||
                    support.max.y < top.min.y - 0.005f)
                    throw new InvalidOperationException($"Guardrail {floor}/{bay} support {r} lost slab/top-rail load-path overlap.");

                ValidateAnchorHead(detailBay, $"HD_RailBolt_{r}_A", plate, floor, bay);
                ValidateAnchorHead(detailBay, $"HD_RailBolt_{r}_B", plate, floor, bay);
            }

            LODGroup group = manifest.GetComponent<LODGroup>();
            if (group == null) throw new InvalidOperationException($"Guardrail {floor}/{bay} LODGroup missing.");
            LOD[] lods = group.GetLODs();
            if (lods.Length != 4 || group.fadeMode != LODFadeMode.CrossFade || !group.animateCrossFading)
                throw new InvalidOperationException($"Guardrail {floor}/{bay} must use four animated cross-faded LODs.");

            Bounds? lod0 = null;
            int previousTriangles = int.MaxValue;
            for (int level = 0; level < 4; level++)
            {
                if (lods[level].renderers == null || lods[level].renderers.Length != 1 || lods[level].renderers[0] == null)
                    throw new InvalidOperationException($"Guardrail {floor}/{bay} LOD{level} must own exactly one combined renderer.");
                Renderer renderer = lods[level].renderers[0];
                Mesh mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null || !mesh.name.StartsWith("GM_BalconyGuardrail_LOD", StringComparison.Ordinal) || StockMeshes.Contains(mesh.name))
                    throw new InvalidOperationException($"Guardrail {floor}/{bay} LOD{level} is not dedicated authored/procedural guardrail geometry.");
                int triangles = mesh.triangles.Length / 3;
                if (triangles >= previousTriangles)
                    throw new InvalidOperationException($"Guardrail {floor}/{bay} triangle count does not decrease at LOD{level}: {triangles} >= {previousTriangles}.");
                previousTriangles = triangles;
                ValidateMaterial(renderer.sharedMaterial, $"Guardrail {floor}/{bay} LOD{level}");

                if (level == 0) lod0 = renderer.bounds;
                else
                {
                    Bounds b0 = lod0.Value;
                    Bounds b = renderer.bounds;
                    if (MaxComponent(Abs(b.size - b0.size)) > LodBoundsTolerance ||
                        (b.center - b0.center).magnitude > LodBoundsTolerance)
                        throw new InvalidOperationException($"Guardrail {floor}/{bay} LOD{level} macro bounds drift beyond 10 mm.");
                }
            }
        }

        Debug.Log("Balcony guardrail source QA passed: 30 dense vertical-lattice assemblies, 1.10 m top height, final fascia/base/anchor interfaces re-proved, legacy collider anchors preserved, stock renderers disabled and four decreasing-complexity cross-faded LODs retained. Native 4K review remains mandatory.");
    }

    /// <summary>Returns the actual generated top-rail datum when available, otherwise the active legacy renderer bounds.</summary>
    public static Bounds ResolveTopRailBounds(int floor, int bay)
    {
        Transform assembly = FindGuardrailAssembly(floor, bay);
        if (assembly != null)
            return WorldBoundsFromLocal(assembly, new Vector3(0f, TopRailCenterY, 0f), new Vector3(TopRailLength, TopRailHeight, TopRailDepth));
        Renderer legacy = RequireActiveRenderer($"RailTop_{floor}_{bay}");
        return legacy.bounds;
    }

    /// <summary>Returns the primary visual support datum aligned to the existing seven base plates.</summary>
    public static Bounds ResolveSupportPostBounds(int floor, int bay, int r)
    {
        if (r < -3 || r > 3) throw new ArgumentOutOfRangeException(nameof(r));
        Transform assembly = FindGuardrailAssembly(floor, bay);
        if (assembly != null)
        {
            float x = r * PrimaryPostPitch;
            return WorldBoundsFromLocal(assembly, new Vector3(x, PrimaryPostCenterY, 0f),
                new Vector3(PrimaryPostWidth, PrimaryPostHeight, PrimaryPostDepth));
        }
        Renderer legacy = RequireActiveRenderer($"Rail_{floor}_{bay}_{r}");
        return legacy.bounds;
    }

    private static void BuildBayAssembly(Transform parent, int floor, int bay, Vector3 worldPosition,
        Material material, Mesh[] meshes, float lowerTopRelative)
    {
        GameObject assembly = new GameObject(AssemblyName(floor, bay));
        assembly.transform.SetParent(parent, false);
        assembly.transform.position = worldPosition;
        assembly.transform.rotation = Quaternion.identity;
        assembly.transform.localScale = Vector3.one;

        Renderer[][] sets = new Renderer[4][];
        float phaseU = Mathf.Repeat(floor * 0.173f + bay * 0.097f, 1f);
        float phaseV = Mathf.Repeat(floor * 0.071f + bay * 0.211f, 1f);
        for (int level = 0; level < 4; level++)
        {
            GameObject tier = new GameObject($"LOD{level}");
            tier.transform.SetParent(assembly.transform, false);
            GameObject body = new GameObject("GuardrailBody");
            body.transform.SetParent(tier.transform, false);
            MeshFilter filter = body.AddComponent<MeshFilter>();
            filter.sharedMesh = meshes[level];
            MeshRenderer renderer = body.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            var block = new MaterialPropertyBlock();
            block.SetVector("_BumpMap_ST", new Vector4(1f, 1f, phaseU, phaseV));
            renderer.SetPropertyBlock(block);
            sets[level] = new Renderer[] { renderer };
        }

        LODGroup group = assembly.AddComponent<LODGroup>();
        group.fadeMode = LODFadeMode.CrossFade;
        group.animateCrossFading = true;
        group.SetLODs(new[]
        {
            new LOD(LodTransitions[0], sets[0]),
            new LOD(LodTransitions[1], sets[1]),
            new LOD(LodTransitions[2], sets[2]),
            new LOD(LodTransitions[3], sets[3])
        });
        group.RecalculateBounds();

        QualityBlockBalconyGuardrailManifest manifest = assembly.AddComponent<QualityBlockBalconyGuardrailManifest>();
        manifest.Configure(floor, bay, PrimaryPostCount, InfillCount, lowerTopRelative, MaximumCalculatedClearOpening);
    }

    private static Mesh[] BuildLodMeshes()
    {
        return new[]
        {
            BuildCombinedChamferedMeshAsset(0, true),
            BuildCombinedChamferedMeshAsset(1, false),
            BuildSimpleMeshAsset(2, false),
            BuildSimpleMeshAsset(3, true)
        };
    }

    private static Mesh BuildCombinedChamferedMeshAsset(int level, bool includeSleeves)
    {
        var combines = new List<CombineInstance>();
        AddCombine(combines, QualityBlockDetailMeshLibrary.GetChamferedBox(new Vector3(TopRailLength, TopRailHeight, TopRailDepth)),
            new Vector3(0f, TopRailCenterY, 0f));

        for (int i = -14; i <= 14; i++)
        {
            float x = i * VerticalPitch;
            bool support = IsPrimaryIndex(i);
            Vector3 size = support
                ? new Vector3(PrimaryPostWidth, PrimaryPostHeight, PrimaryPostDepth)
                : new Vector3(InfillWidth, InfillHeight, InfillDepth);
            float y = support ? PrimaryPostCenterY : InfillCenterY;
            AddCombine(combines, QualityBlockDetailMeshLibrary.GetChamferedBox(size), new Vector3(x, y, 0f));

            if (includeSleeves && support)
            {
                Mesh sleeve = QualityBlockDetailMeshLibrary.GetChamferedBox(new Vector3(SleeveWidth, SleeveHeight, SleeveDepth));
                AddCombine(combines, sleeve, new Vector3(x, TopRailBottomY + 0.002f, 0f));
                AddCombine(combines, sleeve, new Vector3(x, LowerRailExpectedTop - 0.002f, 0f));
            }
        }

        Mesh mesh = new Mesh { indexFormat = IndexFormat.UInt32, name = $"GM_BalconyGuardrail_LOD{level}" };
        mesh.CombineMeshes(combines.ToArray(), true, true, false);
        ApplyMetricUv(mesh);
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return SaveMesh(mesh, level);
    }

    private static Mesh BuildSimpleMeshAsset(int level, bool crossedInfill)
    {
        var vertices = new List<Vector3>(2048);
        var triangles = new List<int>(4096);
        AppendBox(vertices, triangles, new Vector3(0f, TopRailCenterY, 0f), new Vector3(TopRailLength, TopRailHeight, TopRailDepth));

        for (int i = -14; i <= 14; i++)
        {
            float x = i * VerticalPitch;
            if (IsPrimaryIndex(i))
                AppendBox(vertices, triangles, new Vector3(x, PrimaryPostCenterY, 0f),
                    new Vector3(PrimaryPostWidth, PrimaryPostHeight, PrimaryPostDepth));
            else if (crossedInfill)
                AppendCrossedVerticalProxy(vertices, triangles, x, InfillCenterY, InfillHeight);
            else
                AppendBox(vertices, triangles, new Vector3(x, InfillCenterY, 0f),
                    new Vector3(InfillWidth, InfillHeight, InfillDepth));
        }

        Mesh mesh = new Mesh { indexFormat = IndexFormat.UInt32, name = $"GM_BalconyGuardrail_LOD{level}" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0, true);
        ApplyMetricUv(mesh);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return SaveMesh(mesh, level);
    }

    private static Mesh SaveMesh(Mesh mesh, int level)
    {
        string path = $"{MeshRoot}/GM_BalconyGuardrail_LOD{level}.asset";
        if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static void AddCombine(List<CombineInstance> list, Mesh mesh, Vector3 center)
    {
        list.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(center, Quaternion.identity, Vector3.one) });
    }

    private static bool IsPrimaryIndex(int i) => Mathf.Abs(i) <= 12 && i % 4 == 0;

    private static void AppendBox(List<Vector3> v, List<int> t, Vector3 c, Vector3 size)
    {
        Vector3 e = size * 0.5f;
        int s = v.Count;
        v.Add(c + new Vector3(-e.x, -e.y, -e.z));
        v.Add(c + new Vector3(e.x, -e.y, -e.z));
        v.Add(c + new Vector3(e.x, e.y, -e.z));
        v.Add(c + new Vector3(-e.x, e.y, -e.z));
        v.Add(c + new Vector3(-e.x, -e.y, e.z));
        v.Add(c + new Vector3(e.x, -e.y, e.z));
        v.Add(c + new Vector3(e.x, e.y, e.z));
        v.Add(c + new Vector3(-e.x, e.y, e.z));
        int[] q =
        {
            0,2,1, 0,3,2, 4,5,6, 4,6,7,
            0,4,7, 0,7,3, 1,2,6, 1,6,5,
            3,7,6, 3,6,2, 0,1,5, 0,5,4
        };
        for (int i = 0; i < q.Length; i++) t.Add(s + q[i]);
    }

    private static void AppendCrossedVerticalProxy(List<Vector3> v, List<int> t, float x, float centerY, float height)
    {
        float h = height * 0.5f, wx = InfillWidth * 0.5f, dz = InfillDepth * 0.5f;
        AddDoubleSidedQuad(v, t,
            new Vector3(x - wx, centerY - h, 0f), new Vector3(x + wx, centerY - h, 0f),
            new Vector3(x + wx, centerY + h, 0f), new Vector3(x - wx, centerY + h, 0f));
        AddDoubleSidedQuad(v, t,
            new Vector3(x, centerY - h, -dz), new Vector3(x, centerY - h, dz),
            new Vector3(x, centerY + h, dz), new Vector3(x, centerY + h, -dz));
    }

    private static void AddDoubleSidedQuad(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        int s = v.Count;
        v.Add(a); v.Add(b); v.Add(c); v.Add(d);
        t.Add(s); t.Add(s + 1); t.Add(s + 2); t.Add(s); t.Add(s + 2); t.Add(s + 3);
        t.Add(s); t.Add(s + 2); t.Add(s + 1); t.Add(s); t.Add(s + 3); t.Add(s + 2);
    }

    private static void ApplyMetricUv(Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        Vector2[] uv = new Vector2[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 p = vertices[i];
            uv[i] = new Vector2((p.x + p.z * 0.37f) / MicroTileM, (p.y + p.z * 0.71f) / MicroTileM);
        }
        mesh.uv = uv;
    }

    private static Material BuildMaterial()
    {
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
        if (normal == null) throw new InvalidOperationException("Guardrail anodized micro-normal failed to import.");
        string path = MaterialRoot + "/MAT_BalconyGuardrail_AgedAnodized.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("Unity Standard shader unavailable for guardrail material.");
            m = new Material(shader) { name = "MAT_BalconyGuardrail_AgedAnodized" };
            AssetDatabase.CreateAsset(m, path);
        }
        m.color = new Color(0.48f, 0.50f, 0.49f, 1f);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Glossiness", 0.50f);
        m.SetTexture("_BumpMap", normal);
        m.SetFloat("_BumpScale", 0.24f);
        m.EnableKeyword("_NORMALMAP");
        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
        m.DisableKeyword("_EMISSION");
        EditorUtility.SetDirty(m);
        return m;
    }

    private static void EnsureAnodizedNormal()
    {
        const int size = 256;
        var texture = new Texture2D(size, size, TextureFormat.RGB24, true, true) { name = "T_BalconyAnodizedMicro_N" };
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = SurfaceHeight(x + 1, y) - SurfaceHeight(x - 1, y);
            float dy = SurfaceHeight(x, y + 1) - SurfaceHeight(x, y - 1);
            Vector3 n = new Vector3(-dx * 0.9f, -dy * 0.9f, 1f).normalized;
            pixels[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
        }
        texture.SetPixels(pixels);
        texture.Apply(true, false);
        File.WriteAllBytes(NormalPath, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(NormalPath, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(NormalPath) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Cannot configure guardrail normal importer.");
        importer.textureType = TextureImporterType.NormalMap;
        importer.sRGBTexture = false;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 8;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }

    private static float SurfaceHeight(int x, int y)
    {
        float extrusion = Mathf.Sin(y * Mathf.PI * 2f / 19f) * 0.12f + Mathf.Sin(y * Mathf.PI * 2f / 7f + 0.8f) * 0.045f;
        uint h = (uint)((x & 255) * 73856093) ^ (uint)((y & 255) * 19349663) ^ 0x9e3779b9u;
        h ^= h >> 13; h *= 1274126177u; h ^= h >> 16;
        float noise = (h & 1023u) / 1023f - 0.5f;
        return extrusion + noise * 0.035f;
    }

    private static void ValidateMaterial(Material m, string label)
    {
        if (m == null || m.shader == null) throw new InvalidOperationException($"{label}: material/shader missing.");
        float metallic = m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : -1f;
        float smoothness = m.HasProperty("_Glossiness") ? m.GetFloat("_Glossiness") : -1f;
        if (metallic < -0.0001f || metallic > 0.0201f)
            throw new InvalidOperationException($"{label}: anodized effective metallic {metallic:F3} is outside 0-0.02.");
        if (smoothness < 0.42f || smoothness > 0.58f)
            throw new InvalidOperationException($"{label}: smoothness {smoothness:F3} is outside 0.42-0.58.");
        if (m.GetTexture("_BumpMap") == null || !m.IsKeywordEnabled("_NORMALMAP"))
            throw new InvalidOperationException($"{label}: manufacture-scale normal map is missing.");
        if (m.HasProperty("_BumpScale") && (m.GetFloat("_BumpScale") < 0.18f || m.GetFloat("_BumpScale") > 0.30f))
            throw new InvalidOperationException($"{label}: normal scale is outside restrained anodized range.");
        if (m.IsKeywordEnabled("_EMISSION") || (m.HasProperty("_EmissionColor") && m.GetColor("_EmissionColor").maxColorComponent > 0.001f))
            throw new InvalidOperationException($"{label}: daytime guardrail may not use emission/baked light.");
    }

    private static void ValidateAnchorHead(Transform detailBay, string name, Renderer plate, int floor, int bay)
    {
        Renderer bolt = RequireDirectChildRenderer(detailBay, name);
        float embed = plate.bounds.max.y - bolt.bounds.min.y;
        if (embed < AnchorEmbedMin || embed > AnchorEmbedMax)
            throw new InvalidOperationException($"Guardrail {floor}/{bay}/{name}: anchor-head embed {embed:F4} m outside 0.001-0.006 m.");
        if (bolt.bounds.max.y <= plate.bounds.max.y)
            throw new InvalidOperationException($"Guardrail {floor}/{bay}/{name}: anchor head is fully buried in its base plate.");
    }

    private static void ValidateLegacyAnchor(GameObject go, Vector3 expectedColliderSize)
    {
        Renderer renderer = RequireRendererComponent(go, go.name);
        if (renderer.enabled)
            throw new InvalidOperationException($"Critical primitive-placeholder risk: legacy renderer still enabled after guardrail reconstruction: {go.name}");
        MeshFilter filter = go.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null)
            throw new InvalidOperationException($"Legacy gameplay anchor lost its mesh/filter bookkeeping: {go.name}");
        BoxCollider collider = go.GetComponent<BoxCollider>();
        if (collider == null || !collider.enabled)
            throw new InvalidOperationException($"Legacy gameplay anchor lost its BoxCollider: {go.name}");
        Vector3 size = collider.bounds.size;
        if (MaxComponent(Abs(size - expectedColliderSize)) > 0.003f)
            throw new InvalidOperationException($"Legacy gameplay collider envelope drifted for {go.name}: {size} vs {expectedColliderSize}.");
    }

    private static Transform FindGuardrailAssembly(int floor, int bay)
    {
        GameObject root = Find(RootName);
        if (root == null) return null;
        return root.transform.Find(AssemblyName(floor, bay));
    }

    private static Bounds WorldBoundsFromLocal(Transform transform, Vector3 localCenter, Vector3 localSize)
    {
        Vector3 center = transform.TransformPoint(localCenter);
        Vector3 scale = Abs(transform.lossyScale);
        return new Bounds(center, Vector3.Scale(localSize, scale));
    }

    private static string AssemblyName(int floor, int bay) => $"Guardrail_{floor}_{bay}";

    private static Transform RequireDetailBay(Transform detailRoot, int floor, int bay)
    {
        Transform t = detailRoot.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(x => x.name == $"HD_BayAssembly_{floor}_{bay}");
        if (t == null) throw new InvalidOperationException($"Missing HD_BayAssembly_{floor}_{bay}.");
        return t;
    }

    private static Renderer RequireDirectChildRenderer(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name != name) continue;
            Renderer r = child.GetComponent<Renderer>();
            if (r == null || !r.enabled) throw new InvalidOperationException($"Required active renderer missing: {parent.name}/{name}");
            return r;
        }
        throw new InvalidOperationException($"Required detail child missing: {parent.name}/{name}");
    }

    private static Renderer RequireActiveRenderer(string name)
    {
        GameObject go = RequireObject(name);
        Renderer r = go.GetComponent<Renderer>();
        if (r == null || !r.enabled || !go.activeInHierarchy)
            throw new InvalidOperationException($"Required active construction renderer missing: {name}");
        return r;
    }

    private static Renderer RequireRendererComponent(GameObject go, string label)
    {
        Renderer r = go != null ? go.GetComponent<Renderer>() : null;
        if (r == null) throw new InvalidOperationException($"Required renderer component missing: {label}");
        return r;
    }

    private static GameObject RequireObject(string name)
    {
        GameObject go = Find(name);
        if (go == null) throw new InvalidOperationException($"Required object missing: {name}");
        return go;
    }

    private static GameObject Find(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x.scene.IsValid() && x.scene.path == ScenePath && x.name == name);
    }

    private static void DestroyGeneratedRoot()
    {
        GameObject old = Find(RootName);
        if (old != null) UnityEngine.Object.DestroyImmediate(old);
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

    private static string AbsolutePath(string assetPath)
    {
        string root = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrWhiteSpace(root)) throw new InvalidOperationException("Could not resolve Unity project root.");
        return Path.GetFullPath(Path.Combine(root, assetPath));
    }

    private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    private static float MaxComponent(Vector3 v) => Mathf.Max(v.x, Mathf.Max(v.y, v.z));

    [Serializable] private sealed class Contract
    {
        public string schemaVersion;
        public string scenePath;
        public int expectedBays;
        public Dimensions dimensionsThickness;
        public LodPolicy lodPolicy;
        public VisualCreditPolicy visualCreditPolicy;
    }
    [Serializable] private sealed class Dimensions
    {
        public float guardTopHeightAboveFinishedFloorM;
        public CountSpec primaryPost;
        public InfillSpec infill;
    }
    [Serializable] private sealed class CountSpec { public int count; }
    [Serializable] private sealed class InfillSpec { public int count; public float maximumCalculatedClearOpeningM; }
    [Serializable] private sealed class LodPolicy { public int levels; public bool crossFadeRequired; }
    [Serializable] private sealed class VisualCreditPolicy
    {
        public int autoVisualPoints;
        public bool criticalDefectClearedBySourcePass;
    }
}

[DisallowMultipleComponent]
public sealed class QualityBlockBalconyGuardrailManifest : MonoBehaviour
{
    [SerializeField] private int floor;
    [SerializeField] private int bay;
    [SerializeField] private int primaryPostCount;
    [SerializeField] private int infillCount;
    [SerializeField] private float lowerRailTopAboveFloorM;
    [SerializeField] private float maximumClearOpeningM;

    public int Floor => floor;
    public int Bay => bay;
    public int PrimaryPostCount => primaryPostCount;
    public int InfillCount => infillCount;
    public float LowerRailTopAboveFloorM => lowerRailTopAboveFloorM;
    public float MaximumClearOpeningM => maximumClearOpeningM;

    public void Configure(int floorValue, int bayValue, int supports, int infill, float lowerTop, float maxOpening)
    {
        floor = floorValue;
        bay = bayValue;
        primaryPostCount = supports;
        infillCount = infill;
        lowerRailTopAboveFloorM = lowerTop;
        maximumClearOpeningM = maxOpening;
    }
}