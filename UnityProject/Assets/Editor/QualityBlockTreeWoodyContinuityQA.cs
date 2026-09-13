using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Corrects two source-observable defects in the generated tree fallback before authoritative capture:
/// (1) reverse radius steps at segmented trunk / primary-branch joints and (2) exact normalized bark-UV
/// restarts on every woody module. Corrected meshes use circular joint rings, metric cylindrical UVs and
/// deterministic per-tree phase offsets. Existing art-slot ownership, gameplay colliders and four-level
/// LOD membership remain unchanged. Source QA awards zero Visual Fidelity points.
/// </summary>
public static class QualityBlockTreeWoodyContinuityQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/tree_woody_continuity_contract.json";
    private const string MeshRoot = "Assets/Art/GeneratedTreeMeshes/WoodyContinuity";
    private const string BarkMaterialPath = "Assets/Art/GeneratedPBR/PBR_Bark.mat";
    private const string BarkAlbedoPath = "Assets/Art/GeneratedPBR/PBR_Bark_Albedo.png";
    private const string BarkNormalPath = "Assets/Art/GeneratedPBR/PBR_Bark_Normal.png";
    private const string BarkMaskPath = "Assets/Art/GeneratedPBR/PBR_Bark_MetallicSmoothness.png";
    private const string MasterPrefix = "HD_TreeMaster_";
    private const string Lod1RootName = "Tree_LOD1_Proxy";
    private const string Lod2RootName = "Tree_LOD2_Proxy";
    private const string Lod3RootName = "Tree_LOD3_Proxy";

    private const int ExpectedTrees = 6;
    private const int TrunkSections = 7;
    private const int RootFlares = 7;
    private const int PrimaryPairs = 7;
    private const int SecondaryBranches = 14;
    private const int Twigs = 28;
    private const float JointCenterToleranceM = 0.002f;
    private const float JointRadiusToleranceM = 0.002f;
    private const float ReverseTaperToleranceM = 0.001f;
    private const float BarkCyclesPerMeter = 0.55f;
    private const float BarkNormalScale = 0.85f;
    private const int MinimumDistinctTreePhases = 5;

    [MenuItem("NewTown/Vegetation/Correct Tree Woody Continuity")]
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
        Debug.Log("Tree woody continuity correction persisted. Visual Fidelity remains UNSCORED pending native 4K evidence.");
    }

    public static void ApplyToOpenScene()
    {
        EnsureScene();
        ValidateContractConfigOnly();
        Directory.CreateDirectory(MeshRoot);

        Material bark = AssetDatabase.LoadAssetAtPath<Material>(BarkMaterialPath);
        if (bark == null)
            throw new InvalidOperationException($"Missing bark material: {BarkMaterialPath}");
        ConfigureBarkMaterial(bark);

        int correctedTrees = 0;
        foreach (QualityBlockArtSlot slot in TreeSlots())
        {
            int treeIndex = ParseTreeIndex(slot.SlotId);
            if (slot.IsUsingAuthoredArt)
                continue;
            if (slot.FallbackRoot == null)
                throw new InvalidOperationException($"{slot.SlotId}: fallback root missing.");

            Transform master = slot.FallbackRoot.transform.Find(MasterPrefix + treeIndex);
            if (master == null)
                throw new InvalidOperationException($"Tree {treeIndex}: detailed generated master missing before woody continuity correction.");

            CorrectTree(master, treeIndex, bark);
            correctedTrees++;
        }

        if (correctedTrees == 0)
            Debug.Log("All tree slots use authored replacements; generated woody continuity correction not applicable.");

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("NewTown/QA/Validate Tree Woody Continuity Contract")]
    public static void ValidateContractConfigOnly()
    {
        string absolute = AbsolutePath(ContractPath);
        if (!File.Exists(absolute))
            throw new FileNotFoundException($"Missing tree woody continuity contract: {ContractPath}");

        Contract c = JsonUtility.FromJson<Contract>(File.ReadAllText(absolute));
        var errors = new List<string>();
        if (c == null) errors.Add("contract is null/unparseable");
        else
        {
            if (c.schemaVersion != "1.0.0") errors.Add("schemaVersion must remain 1.0.0");
            if (c.scenePath != ScenePath) errors.Add($"scenePath must remain {ScenePath}");
            if (c.expectedTrees != ExpectedTrees) errors.Add($"expectedTrees must remain {ExpectedTrees}");
            if (c.geometry == null || c.geometry.trunk == null) errors.Add("geometry.trunk is required");
            else
            {
                if (c.geometry.trunk.sections != TrunkSections) errors.Add("trunk section count must remain seven");
                if (Mathf.Abs(c.geometry.trunk.jointCenterGapMaxM - JointCenterToleranceM) > 0.0001f)
                    errors.Add("trunk joint-center hard limit must remain 2 mm");
                if (Mathf.Abs(c.geometry.trunk.jointRadiusMismatchMaxM - JointRadiusToleranceM) > 0.0001f)
                    errors.Add("trunk radius hard limit must remain 2 mm");
            }
            if (c.textureMapping == null || Mathf.Abs(c.textureMapping.barkCyclesPerMeter - BarkCyclesPerMeter) > 0.0001f)
                errors.Add("bark metric tiling must remain 0.55 cycles/m");
            if (c.textureMapping == null || !c.textureMapping.channelScaleSynchronizationRequired)
                errors.Add("all bark texture channels must share one physical scale");
            if (c.textureMapping == null || c.textureMapping.minimumDistinctTrunkStartPhasesAcrossSixTrees < MinimumDistinctTreePhases)
                errors.Add("at least five distinct trunk bark phases are mandatory");
            if (c.material == null || c.material.metallicMax > 0.0201f || Mathf.Abs(c.material.normalScale - BarkNormalScale) > 0.0001f)
                errors.Add("bark dielectric/normal-scale limits drifted");
            if (c.lodPolicy == null || c.lodPolicy.levels != 4 || !c.lodPolicy.crossFadeRequired || !c.lodPolicy.sourceAndProxyMeshIdentityRequired)
                errors.Add("four cross-faded LODs with corrected source/proxy mesh identity are mandatory");
            if (c.visualCreditPolicy == null || c.visualCreditPolicy.autoVisualPoints != 0 || c.visualCreditPolicy.criticalDefectClearedBySourcePass)
                errors.Add("source tree QA may not award visual points or clear critical defects");
        }

        if (errors.Count > 0)
            throw new InvalidOperationException("Tree woody continuity contract FAILED:\n - " + string.Join("\n - ", errors));
        Debug.Log("Tree woody continuity contract valid: six trees, metric bark UVs, <=2 mm woody joints, four LODs, automatic visual credit=0.");
    }

    [MenuItem("NewTown/QA/Validate Tree Woody Continuity")]
    public static void ValidateOpenScene()
    {
        EnsureScene();
        ValidateContractConfigOnly();
        ValidateBarkMaterialAndSampling();

        var startPhases = new HashSet<int>();
        int validated = 0;
        foreach (QualityBlockArtSlot slot in TreeSlots())
        {
            int treeIndex = ParseTreeIndex(slot.SlotId);
            if (slot.IsUsingAuthoredArt)
                continue;
            if (slot.FallbackRoot == null)
                throw new InvalidOperationException($"{slot.SlotId}: fallback root missing.");

            Transform master = slot.FallbackRoot.transform.Find(MasterPrefix + treeIndex);
            if (master == null)
                throw new InvalidOperationException($"Tree {treeIndex}: corrected detailed master missing.");

            MeshFilter[] sources = SourceWoodyFilters(master);
            int roots = CountPrefix(sources, "RootFlare_");
            int trunks = CountPrefix(sources, "TrunkSection_");
            int primary = CountPrefix(sources, "PrimaryBranch_");
            int secondary = CountPrefix(sources, "SecondaryBranch_");
            int twigs = CountPrefix(sources, "Twig_");
            if (roots != RootFlares || trunks != TrunkSections || primary != PrimaryPairs * 2 ||
                secondary != SecondaryBranches || twigs != Twigs)
                throw new InvalidOperationException(
                    $"Tree {treeIndex}: woody hierarchy count drifted roots/trunk/primary/secondary/twigs={roots}/{trunks}/{primary}/{secondary}/{twigs}.");

            foreach (MeshFilter source in sources)
            {
                if (source.sharedMesh == null || !source.sharedMesh.name.StartsWith("GM_TreeWoodyMetric_", StringComparison.Ordinal))
                    throw new InvalidOperationException($"Tree {treeIndex}: {source.name} does not use corrected metric woody mesh.");
                if (source.sharedMesh.name == "Cube" || source.sharedMesh.name == "Cylinder" ||
                    source.sharedMesh.name == "Sphere" || source.sharedMesh.name == "Capsule")
                    throw new InvalidOperationException($"Tree {treeIndex}: prohibited stock primitive remains on {source.name}.");
                MeshRenderer renderer = RequireRenderer(source);
                if (renderer.sharedMaterial == null || renderer.sharedMaterial.name != "PBR_Bark")
                    throw new InvalidOperationException($"Tree {treeIndex}: {source.name} lost the dry dielectric bark material.");
                ValidateRetainedProxyIdentity(master, source);
            }

            MeshFilter[] trunk = Enumerable.Range(0, TrunkSections)
                .Select(i => RequireSource(master, $"TrunkSection_{i}"))
                .ToArray();
            for (int i = 0; i < trunk.Length - 1; i++)
            {
                ValidateJoint(treeIndex, $"trunk {i}->{i + 1}", trunk[i], trunk[i + 1]);
                if (EndpointRadiusMeters(trunk[i], true) > EndpointRadiusMeters(trunk[i], false) + ReverseTaperToleranceM)
                    throw new InvalidOperationException($"Tree {treeIndex}: trunk section {i} reverse-tapers outward toward crown.");
                ValidateUvVContinuity(treeIndex, $"trunk {i}->{i + 1}", trunk[i], trunk[i + 1]);
            }
            MeshFilter finalTrunk = trunk[trunk.Length - 1];
            if (EndpointRadiusMeters(finalTrunk, true) > EndpointRadiusMeters(finalTrunk, false) + ReverseTaperToleranceM)
                throw new InvalidOperationException($"Tree {treeIndex}: final trunk section reverse-tapers.");

            float phase = PositiveFraction(EndpointUvV(trunk[0], false) * BarkCyclesPerMeter);
            startPhases.Add(Mathf.RoundToInt(phase * 1000f));

            for (int p = 0; p < PrimaryPairs; p++)
            {
                MeshFilter a = RequireSource(master, $"PrimaryBranch_{p}_A");
                MeshFilter b = RequireSource(master, $"PrimaryBranch_{p}_B");
                ValidateJoint(treeIndex, $"primary {p} A->B", a, b);
                ValidateUvVContinuity(treeIndex, $"primary {p} A->B", a, b);
                if (EndpointRadiusMeters(a, true) > EndpointRadiusMeters(a, false) + ReverseTaperToleranceM ||
                    EndpointRadiusMeters(b, true) > EndpointRadiusMeters(b, false) + ReverseTaperToleranceM)
                    throw new InvalidOperationException($"Tree {treeIndex}: primary branch {p} reverse-tapers.");
            }

            float anchorY = slot.FallbackRoot.transform.position.y;
            for (int r = 0; r < RootFlares; r++)
            {
                MeshFilter root = RequireSource(master, $"RootFlare_{r}");
                float tipY = SegmentEndpointWorld(root.transform, true).y - anchorY;
                if (tipY < -0.0101f || tipY > 0.1201f)
                    throw new InvalidOperationException($"Tree {treeIndex}: root flare {r} tip is {tipY:F4} m relative to fallback-root grade; expected -0.01..0.12 m.");
            }

            LODGroup group = master.GetComponent<LODGroup>();
            if (group == null || group.GetLODs().Length != 4 || group.fadeMode != LODFadeMode.CrossFade || !group.animateCrossFading)
                throw new InvalidOperationException($"Tree {treeIndex}: four-level animated cross-fade LOD contract is not intact.");
            validated++;
        }

        if (validated > 0 && startPhases.Count < MinimumDistinctTreePhases)
            throw new InvalidOperationException($"Tree bark phase diversity is too low: {startPhases.Count} distinct starts across {validated} generated trees; require >= {MinimumDistinctTreePhases}.");

        Debug.Log(
            $"Tree woody continuity QA passed for {validated} generated fallbacks: continuous <=2 mm trunk/primary joints, metric non-restarting bark UV, synchronized dry dielectric bark channels and retained LOD proxy identity. Native rendered review remains pending; Visual Fidelity is UNSCORED.");
    }

    private static void CorrectTree(Transform master, int treeIndex, Material bark)
    {
        MeshFilter[] sources = SourceWoodyFilters(master);
        if (CountPrefix(sources, "RootFlare_") != RootFlares || CountPrefix(sources, "TrunkSection_") != TrunkSections ||
            CountPrefix(sources, "PrimaryBranch_") != PrimaryPairs * 2 || CountPrefix(sources, "SecondaryBranch_") != SecondaryBranches ||
            CountPrefix(sources, "Twig_") != Twigs)
            throw new InvalidOperationException($"Tree {treeIndex}: unexpected woody hierarchy before correction.");

        // Trunk: preserve the generated centerline/transforms, but make every end radius equal the next
        // section's start radius. Metric V accumulates through all seven modules, so bark never restarts.
        float trunkV = treeIndex * 0.617f;
        float trunkU = treeIndex * 0.431f;
        for (int i = 0; i < TrunkSections; i++)
        {
            MeshFilter current = RequireSource(master, $"TrunkSection_{i}");
            float startR = RadialScaleMeters(current.transform);
            float endR = i < TrunkSections - 1
                ? RadialScaleMeters(RequireSource(master, $"TrunkSection_{i + 1}").transform)
                : startR * (0.115f / 0.1428571429f);
            float length = SegmentLengthMeters(current.transform);
            Mesh mesh = BuildAndSaveMetricMesh(treeIndex, current.name, 12, endR / startR, 0.045f,
                1000 + treeIndex * 101 + i, startR, endR, length, trunkU, trunkV);
            SetSourceAndRetainedProxies(master, current, mesh, bark);
            trunkV += length;
        }

        // Root flares retain their geometric role at grade but receive independent phase offsets.
        for (int r = 0; r < RootFlares; r++)
        {
            MeshFilter root = RequireSource(master, $"RootFlare_{r}");
            float startR = RadialScaleMeters(root.transform);
            float endR = startR * 0.58f;
            float length = SegmentLengthMeters(root.transform);
            float phaseU = treeIndex * 0.431f + r * 0.173f;
            float phaseV = treeIndex * 0.617f + r * 0.271f;
            Mesh mesh = BuildAndSaveMetricMesh(treeIndex, root.name, 10, 0.58f, 0.08f,
                2000 + treeIndex * 101 + r, startR, endR, length, phaseU, phaseV);
            SetSourceAndRetainedProxies(master, root, mesh, bark);
        }

        // Primary A/B chains: A ends at exactly B's start radius and shares continuous metric V with B.
        for (int p = 0; p < PrimaryPairs; p++)
        {
            MeshFilter a = RequireSource(master, $"PrimaryBranch_{p}_A");
            MeshFilter b = RequireSource(master, $"PrimaryBranch_{p}_B");
            float aStart = RadialScaleMeters(a.transform);
            float bStart = RadialScaleMeters(b.transform);
            float aLength = SegmentLengthMeters(a.transform);
            float bLength = SegmentLengthMeters(b.transform);
            float phaseU = treeIndex * 0.431f + p * 0.233f + 0.071f;
            float phaseV = treeIndex * 0.617f + p * 0.733f + 1.217f;

            Mesh meshA = BuildAndSaveMetricMesh(treeIndex, a.name, 10, bStart / aStart, 0.055f,
                3000 + treeIndex * 101 + p * 2, aStart, bStart, aLength, phaseU, phaseV);
            SetSourceAndRetainedProxies(master, a, meshA, bark);

            float bEnd = bStart * 0.62f;
            Mesh meshB = BuildAndSaveMetricMesh(treeIndex, b.name, 10, 0.62f, 0.055f,
                3001 + treeIndex * 101 + p * 2, bStart, bEnd, bLength, phaseU, phaseV + aLength);
            SetSourceAndRetainedProxies(master, b, meshB, bark);
        }

        foreach (MeshFilter secondary in sources.Where(x => x.name.StartsWith("SecondaryBranch_", StringComparison.Ordinal)))
            CorrectIndependentSegment(master, treeIndex, secondary, 8, 0.54f, 0.065f, 4000, bark);
        foreach (MeshFilter twig in sources.Where(x => x.name.StartsWith("Twig_", StringComparison.Ordinal)))
            CorrectIndependentSegment(master, treeIndex, twig, 7, 0.42f, 0.075f, 5000, bark);
    }

    private static void CorrectIndependentSegment(Transform master, int treeIndex, MeshFilter source, int sides,
        float endRatio, float irregularity, int seedBase, Material bark)
    {
        int ordinal = StableNameHash(source.name) & 0x3FFF;
        float startR = RadialScaleMeters(source.transform);
        float endR = startR * endRatio;
        float length = SegmentLengthMeters(source.transform);
        float phaseU = treeIndex * 0.431f + PositiveFraction(ordinal * 0.137f) * 1.3f;
        float phaseV = treeIndex * 0.617f + PositiveFraction(ordinal * 0.193f) * 2.1f;
        Mesh mesh = BuildAndSaveMetricMesh(treeIndex, source.name, sides, endRatio, irregularity,
            seedBase + treeIndex * 997 + ordinal, startR, endR, length, phaseU, phaseV);
        SetSourceAndRetainedProxies(master, source, mesh, bark);
    }

    private static Mesh BuildAndSaveMetricMesh(int treeIndex, string sourceName, int sides, float endRatio,
        float irregularity, int seed, float startRadiusM, float endRadiusM, float lengthM, float phaseUM, float vStartM)
    {
        if (sides < 6 || endRatio <= 0.05f || endRatio > 1.001f || startRadiusM <= 0f || endRadiusM <= 0f || lengthM <= 0.01f)
            throw new InvalidOperationException($"Invalid woody mesh parameters for tree {treeIndex}/{sourceName}.");

        Mesh generated = BuildMetricSegmentMesh(sides, endRatio, irregularity, seed,
            startRadiusM, endRadiusM, lengthM, phaseUM, vStartM);
        string safe = sourceName.Replace('/', '_').Replace('\\', '_').Replace(':', '_');
        string name = $"GM_TreeWoodyMetric_T{treeIndex}_{safe}";
        generated.name = name;
        string path = $"{MeshRoot}/{name}.asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(generated, path);
            return generated;
        }

        EditorUtility.CopySerialized(generated, existing);
        existing.name = name;
        EditorUtility.SetDirty(existing);
        UnityEngine.Object.DestroyImmediate(generated);
        return existing;
    }

    private static Mesh BuildMetricSegmentMesh(int sides, float endRatio, float irregularity, int seed,
        float startRadiusM, float endRadiusM, float lengthM, float phaseUM, float vStartM)
    {
        const int rings = 4;
        int stride = sides + 1;
        var vertices = new List<Vector3>(rings * stride + sides * 2 + 2);
        var uv = new List<Vector2>(rings * stride + sides * 2 + 2);
        var triangles = new List<int>(rings * sides * 6);

        for (int r = 0; r < rings; r++)
        {
            float t = r / (float)(rings - 1);
            float nominalRadius = Mathf.Lerp(1f, endRatio, t);
            float physicalRadius = Mathf.Lerp(startRadiusM, endRadiusM, t);
            float endpointFade = Mathf.Sin(Mathf.PI * t); // zero at joints: circular rings mate exactly.
            for (int s = 0; s <= sides; s++)
            {
                float f = s / (float)sides;
                float angle = Mathf.PI * 2f * f;
                float wobble = 1f + irregularity * endpointFade * Mathf.Sin(seed * 0.173f + s * 1.913f + r * 0.773f);
                vertices.Add(new Vector3(Mathf.Cos(angle) * nominalRadius * wobble, t,
                    Mathf.Sin(angle) * nominalRadius * wobble));
                float uMeters = phaseUM + f * (Mathf.PI * 2f * physicalRadius);
                uv.Add(new Vector2(uMeters, vStartM + t * lengthM));
            }
        }

        for (int r = 0; r < rings - 1; r++)
        for (int s = 0; s < sides; s++)
        {
            int a = r * stride + s;
            int b = a + 1;
            int c = (r + 1) * stride + s;
            int d = c + 1;
            triangles.Add(a); triangles.Add(c); triangles.Add(b);
            triangles.Add(b); triangles.Add(c); triangles.Add(d);
        }

        // Separate cap vertices keep cylindrical side tangents/UVs independent from cap projection.
        int bottomCenter = vertices.Count;
        vertices.Add(Vector3.zero);
        uv.Add(new Vector2(phaseUM, vStartM));
        int bottomRing = vertices.Count;
        for (int s = 0; s < sides; s++)
        {
            float f = s / (float)sides;
            float angle = Mathf.PI * 2f * f;
            vertices.Add(new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)));
            uv.Add(new Vector2(phaseUM + Mathf.Cos(angle) * startRadiusM, vStartM + Mathf.Sin(angle) * startRadiusM));
        }
        for (int s = 0; s < sides; s++)
        {
            int next = (s + 1) % sides;
            triangles.Add(bottomCenter); triangles.Add(bottomRing + s); triangles.Add(bottomRing + next);
        }

        int topCenter = vertices.Count;
        vertices.Add(new Vector3(0f, 1f, 0f));
        uv.Add(new Vector2(phaseUM, vStartM + lengthM));
        int topRing = vertices.Count;
        for (int s = 0; s < sides; s++)
        {
            float f = s / (float)sides;
            float angle = Mathf.PI * 2f * f;
            vertices.Add(new Vector3(Mathf.Cos(angle) * endRatio, 1f, Mathf.Sin(angle) * endRatio));
            uv.Add(new Vector2(phaseUM + Mathf.Cos(angle) * endRadiusM, vStartM + lengthM + Mathf.Sin(angle) * endRadiusM));
        }
        for (int s = 0; s < sides; s++)
        {
            int next = (s + 1) % sides;
            triangles.Add(topCenter); triangles.Add(topRing + next); triangles.Add(topRing + s);
        }

        var mesh = new Mesh();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uv);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void SetSourceAndRetainedProxies(Transform master, MeshFilter source, Mesh mesh, Material bark)
    {
        MeshRenderer sourceRenderer = RequireRenderer(source);
        source.sharedMesh = mesh;
        sourceRenderer.sharedMaterial = bark;
        EditorUtility.SetDirty(source);
        EditorUtility.SetDirty(sourceRenderer);

        int retained = RetainedThroughLod(source.name);
        string[] roots = { Lod1RootName, Lod2RootName, Lod3RootName };
        for (int lod = 1; lod <= retained; lod++)
        {
            Transform proxyRoot = master.Find(roots[lod - 1]);
            if (proxyRoot == null)
                throw new InvalidOperationException($"{master.name}: missing {roots[lod - 1]} while correcting {source.name}.");
            Transform proxy = proxyRoot.Find($"LOD{lod}_{source.name}");
            MeshFilter proxyFilter = proxy != null ? proxy.GetComponent<MeshFilter>() : null;
            MeshRenderer proxyRenderer = proxy != null ? proxy.GetComponent<MeshRenderer>() : null;
            if (proxyFilter == null || proxyRenderer == null)
                throw new InvalidOperationException($"{master.name}: retained LOD{lod} proxy missing for {source.name}.");
            proxyFilter.sharedMesh = mesh;
            proxyRenderer.sharedMaterial = bark;
            EditorUtility.SetDirty(proxyFilter);
            EditorUtility.SetDirty(proxyRenderer);
        }
    }

    private static void ValidateRetainedProxyIdentity(Transform master, MeshFilter source)
    {
        int retained = RetainedThroughLod(source.name);
        string[] roots = { Lod1RootName, Lod2RootName, Lod3RootName };
        MeshRenderer sourceRenderer = RequireRenderer(source);
        for (int lod = 1; lod <= retained; lod++)
        {
            Transform proxyRoot = master.Find(roots[lod - 1]);
            Transform proxyTransform = proxyRoot != null ? proxyRoot.Find($"LOD{lod}_{source.name}") : null;
            MeshFilter proxy = proxyTransform != null ? proxyTransform.GetComponent<MeshFilter>() : null;
            MeshRenderer proxyRenderer = proxyTransform != null ? proxyTransform.GetComponent<MeshRenderer>() : null;
            if (proxy == null || proxy.sharedMesh != source.sharedMesh)
                throw new InvalidOperationException($"{master.name}: LOD{lod} proxy for {source.name} does not share the corrected source mesh.");
            if (proxyRenderer == null || proxyRenderer.sharedMaterial != sourceRenderer.sharedMaterial)
                throw new InvalidOperationException($"{master.name}: LOD{lod} proxy for {source.name} does not share corrected bark material.");
        }
    }

    private static void ValidateJoint(int treeIndex, string label, MeshFilter first, MeshFilter second)
    {
        float centerGap = Vector3.Distance(SegmentEndpointWorld(first.transform, true), SegmentEndpointWorld(second.transform, false));
        if (centerGap > JointCenterToleranceM + 0.0001f)
            throw new InvalidOperationException($"Tree {treeIndex}: {label} center gap is {centerGap:F4} m; max {JointCenterToleranceM:F3} m.");
        float radiusGap = Mathf.Abs(EndpointRadiusMeters(first, true) - EndpointRadiusMeters(second, false));
        if (radiusGap > JointRadiusToleranceM + 0.0001f)
            throw new InvalidOperationException($"Tree {treeIndex}: {label} radius mismatch is {radiusGap:F4} m; max {JointRadiusToleranceM:F3} m.");
    }

    private static void ValidateUvVContinuity(int treeIndex, string label, MeshFilter first, MeshFilter second)
    {
        float delta = Mathf.Abs(EndpointUvV(first, true) - EndpointUvV(second, false));
        if (delta > 0.001f)
            throw new InvalidOperationException($"Tree {treeIndex}: {label} bark V restarts/drifts by {delta:F4} metric UV units.");
    }

    private static float EndpointRadiusMeters(MeshFilter filter, bool top)
    {
        Mesh mesh = filter.sharedMesh;
        Vector3[] vertices = mesh.vertices;
        float targetY = top ? mesh.bounds.max.y : mesh.bounds.min.y;
        Vector3[] ring = vertices.Where(v => Mathf.Abs(v.y - targetY) < 0.0001f).ToArray();
        if (ring.Length < 4)
            throw new InvalidOperationException($"{filter.name}: endpoint ring not found.");
        float maxLocal = ring.Max(v => Mathf.Sqrt(v.x * v.x + v.z * v.z));
        float averageLocal = ring.Where(v => Mathf.Sqrt(v.x * v.x + v.z * v.z) > maxLocal * 0.8f)
            .Average(v => Mathf.Sqrt(v.x * v.x + v.z * v.z));
        return averageLocal * RadialScaleMeters(filter.transform);
    }

    private static float EndpointUvV(MeshFilter filter, bool top)
    {
        Mesh mesh = filter.sharedMesh;
        Vector3[] vertices = mesh.vertices;
        Vector2[] uvs = mesh.uv;
        if (uvs == null || uvs.Length != vertices.Length)
            throw new InvalidOperationException($"{filter.name}: corrected woody mesh must have one UV0 per vertex.");
        float targetY = top ? mesh.bounds.max.y : mesh.bounds.min.y;
        float maxRadial = vertices.Where(v => Mathf.Abs(v.y - targetY) < 0.0001f)
            .Max(v => Mathf.Sqrt(v.x * v.x + v.z * v.z));
        var values = new List<float>();
        for (int i = 0; i < vertices.Length; i++)
        {
            if (Mathf.Abs(vertices[i].y - targetY) > 0.0001f) continue;
            float radial = Mathf.Sqrt(vertices[i].x * vertices[i].x + vertices[i].z * vertices[i].z);
            if (radial < maxRadial * 0.8f) continue;
            values.Add(uvs[i].y);
        }
        if (values.Count < 4)
            throw new InvalidOperationException($"{filter.name}: endpoint bark UV ring not found.");
        // Side-ring V is constant and has one more sample than the separate cap ring, so the median
        // rejects the symmetric planar-cap values without depending on vertex ordering.
        values.Sort();
        return values[values.Count / 2];
    }

    private static Vector3 SegmentEndpointWorld(Transform t, bool top)
    {
        return t.TransformPoint(top ? Vector3.up : Vector3.zero);
    }

    private static float SegmentLengthMeters(Transform t)
    {
        return Vector3.Distance(t.TransformPoint(Vector3.zero), t.TransformPoint(Vector3.up));
    }

    private static float RadialScaleMeters(Transform t)
    {
        Vector3 s = t.lossyScale;
        return (Mathf.Abs(s.x) + Mathf.Abs(s.z)) * 0.5f;
    }

    private static void ConfigureBarkMaterial(Material bark)
    {
        bark.SetFloat("_Metallic", 0f);
        if (bark.HasProperty("_BumpScale")) bark.SetFloat("_BumpScale", BarkNormalScale);
        if (bark.HasProperty("_EmissionColor")) bark.SetColor("_EmissionColor", Color.black);
        bark.DisableKeyword("_EMISSION");
        foreach (string property in new[] { "_MainTex", "_BumpMap", "_MetallicGlossMap" })
        {
            if (!bark.HasProperty(property) || bark.GetTexture(property) == null)
                throw new InvalidOperationException($"PBR_Bark requires texture property {property} for synchronized metric mapping.");
            bark.SetTextureScale(property, Vector2.one * BarkCyclesPerMeter);
            bark.SetTextureOffset(property, Vector2.zero);
        }
        EditorUtility.SetDirty(bark);
    }

    private static void ValidateBarkMaterialAndSampling()
    {
        Material bark = AssetDatabase.LoadAssetAtPath<Material>(BarkMaterialPath);
        if (bark == null) throw new InvalidOperationException("PBR_Bark material missing.");
        if (bark.HasProperty("_Metallic") && bark.GetFloat("_Metallic") > 0.0201f)
            throw new InvalidOperationException($"PBR_Bark is materially impossible: metallic={bark.GetFloat("_Metallic"):F3}.");
        if (bark.IsKeywordEnabled("_EMISSION") ||
            (bark.HasProperty("_EmissionColor") && bark.GetColor("_EmissionColor").maxColorComponent > 0.001f))
            throw new InvalidOperationException("PBR_Bark may not emit light.");
        if (bark.HasProperty("_BumpScale") && Mathf.Abs(bark.GetFloat("_BumpScale") - BarkNormalScale) > 0.001f)
            throw new InvalidOperationException("PBR_Bark normal scale drifted from 0.85.");

        foreach (string property in new[] { "_MainTex", "_BumpMap", "_MetallicGlossMap" })
        {
            if (!bark.HasProperty(property) || bark.GetTexture(property) == null)
                throw new InvalidOperationException($"PBR_Bark texture {property} missing.");
            Vector2 scale = bark.GetTextureScale(property);
            Vector2 offset = bark.GetTextureOffset(property);
            if ((scale - Vector2.one * BarkCyclesPerMeter).sqrMagnitude > 0.000001f || offset.sqrMagnitude > 0.000001f)
                throw new InvalidOperationException($"PBR_Bark {property} is not synchronized at {BarkCyclesPerMeter:F2} cycles/m with zero material offset.");
        }

        ValidateTextureImporter(BarkAlbedoPath, false);
        ValidateTextureImporter(BarkNormalPath, true);
        ValidateTextureImporter(BarkMaskPath, false);
    }

    private static void ValidateTextureImporter(string path, bool normalMap)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException($"Missing bark texture importer: {path}");
        if (!importer.mipmapEnabled || importer.filterMode != FilterMode.Trilinear || importer.anisoLevel < 8)
            throw new InvalidOperationException($"{path}: bark sampling requires mipmaps, trilinear filtering and anisotropy >= 8.");
        if (normalMap && importer.textureType != TextureImporterType.NormalMap)
            throw new InvalidOperationException($"{path}: bark normal must import as NormalMap.");
        if (!normalMap && path == BarkAlbedoPath && !importer.sRGBTexture)
            throw new InvalidOperationException($"{path}: bark albedo must import as sRGB.");
        if (path == BarkMaskPath && importer.sRGBTexture)
            throw new InvalidOperationException($"{path}: metallic/smoothness mask must remain linear.");
    }

    private static MeshFilter[] SourceWoodyFilters(Transform master)
    {
        return master.GetComponentsInChildren<MeshFilter>(true)
            .Where(f => !IsUnderProxy(f.transform, master) && IsWoodyName(f.name))
            .OrderBy(f => f.name, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsWoodyName(string name)
    {
        return name.StartsWith("RootFlare_", StringComparison.Ordinal) ||
               name.StartsWith("TrunkSection_", StringComparison.Ordinal) ||
               name.StartsWith("PrimaryBranch_", StringComparison.Ordinal) ||
               name.StartsWith("SecondaryBranch_", StringComparison.Ordinal) ||
               name.StartsWith("Twig_", StringComparison.Ordinal);
    }

    private static bool IsUnderProxy(Transform t, Transform master)
    {
        Transform current = t;
        while (current != null && current != master)
        {
            if (current.name == Lod1RootName || current.name == Lod2RootName || current.name == Lod3RootName)
                return true;
            current = current.parent;
        }
        return false;
    }

    private static MeshFilter RequireSource(Transform master, string name)
    {
        MeshFilter[] matches = SourceWoodyFilters(master).Where(f => f.name == name).ToArray();
        if (matches.Length != 1)
            throw new InvalidOperationException($"{master.name}: expected one source woody filter {name}, got {matches.Length}.");
        return matches[0];
    }

    private static MeshRenderer RequireRenderer(MeshFilter filter)
    {
        MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
        if (renderer == null)
            throw new InvalidOperationException($"{filter.name}: MeshRenderer missing.");
        return renderer;
    }

    private static int CountPrefix(IEnumerable<MeshFilter> filters, string prefix)
    {
        return filters.Count(f => f.name.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static int RetainedThroughLod(string objectName)
    {
        if (objectName.StartsWith("RootFlare_", StringComparison.Ordinal) ||
            objectName.StartsWith("TrunkSection_", StringComparison.Ordinal) ||
            objectName.StartsWith("PrimaryBranch_", StringComparison.Ordinal)) return 3;
        if (objectName.StartsWith("SecondaryBranch_", StringComparison.Ordinal)) return 2;
        if (objectName.StartsWith("Twig_", StringComparison.Ordinal)) return 1;
        return 0;
    }

    private static QualityBlockArtSlot[] TreeSlots()
    {
        QualityBlockArtSlot[] slots = Resources.FindObjectsOfTypeAll<QualityBlockArtSlot>()
            .Where(x => x.gameObject.scene.IsValid() && x.SlotId.StartsWith("vegetation.tree.", StringComparison.Ordinal))
            .OrderBy(x => x.SlotId, StringComparer.Ordinal)
            .ToArray();
        if (slots.Length != ExpectedTrees)
            throw new InvalidOperationException($"Expected {ExpectedTrees} tree art slots, got {slots.Length}.");
        return slots;
    }

    private static int ParseTreeIndex(string slotId)
    {
        int dot = slotId.LastIndexOf('.');
        int index;
        if (dot < 0 || !int.TryParse(slotId.Substring(dot + 1), out index) || index < 0 || index >= ExpectedTrees)
            throw new InvalidOperationException($"Invalid tree slot id: {slotId}");
        return index;
    }

    private static int StableNameHash(string value)
    {
        unchecked
        {
            int hash = 17;
            foreach (char c in value) hash = hash * 31 + c;
            return hash;
        }
    }

    private static float PositiveFraction(float value)
    {
        return value - Mathf.Floor(value);
    }

    private static void EnsureScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static string AbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrWhiteSpace(projectRoot))
            throw new InvalidOperationException("Unity project root unavailable.");
        return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
    }

    [Serializable] private sealed class Contract
    {
        public string schemaVersion;
        public string scenePath;
        public int expectedTrees;
        public Geometry geometry;
        public MaterialContract material;
        public TextureMapping textureMapping;
        public LodPolicy lodPolicy;
        public VisualCreditPolicy visualCreditPolicy;
    }
    [Serializable] private sealed class Geometry { public Trunk trunk; }
    [Serializable] private sealed class Trunk
    {
        public int sections;
        public float jointCenterGapMaxM;
        public float jointRadiusMismatchMaxM;
    }
    [Serializable] private sealed class MaterialContract
    {
        public float metallicMax;
        public float normalScale;
    }
    [Serializable] private sealed class TextureMapping
    {
        public float barkCyclesPerMeter;
        public bool channelScaleSynchronizationRequired;
        public int minimumDistinctTrunkStartPhasesAcrossSixTrees;
    }
    [Serializable] private sealed class LodPolicy
    {
        public int levels;
        public bool crossFadeRequired;
        public bool sourceAndProxyMeshIdentityRequired;
    }
    [Serializable] private sealed class VisualCreditPolicy
    {
        public int autoVisualPoints;
        public bool criticalDefectClearedBySourcePass;
    }
}
