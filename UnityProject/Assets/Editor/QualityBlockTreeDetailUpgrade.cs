using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Replaces the generated tree fallback's cylinder + crown-clump look with a physically connected
/// construction hierarchy: root flare -> tapered trunk sections -> primary branches -> secondary
/// branches -> twigs -> explicit leaf sprays. The detailed fallback remains subordinate to the
/// existing ART SLOT, so PF_Tree_A/B/C authored assets still supersede it without scene changes.
///
/// The master is intentionally more detailed than the fixed 1080p benchmark strictly requires.
/// Four renderer-distinct LOD levels then remove construction tiers by physical scale so close-range
/// inspection keeps branch/leaf structure while distant trees do not pay the full renderer cost.
/// </summary>
public static class QualityBlockTreeDetailUpgrade
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string MeshRoot = "Assets/Art/GeneratedTreeMeshes";
    private const string MasterPrefix = "HD_TreeMaster_";
    private const string Lod1RootName = "Tree_LOD1_Proxy";
    private const string Lod2RootName = "Tree_LOD2_Proxy";
    private const string Lod3RootName = "Tree_LOD3_Proxy";

    private const float Lod0Transition = 0.34f;
    private const float Lod1Transition = 0.16f;
    private const float Lod2Transition = 0.065f;
    private const float Lod3Cull = 0.018f;

    [MenuItem("NewTown/Geometry/Build High-Detail Tree Masters + LODs")]
    public static void BuildDetailedTrees()
    {
        QualityBlockDanchiLodUpgrade.BuildLodDetailedQualityBlock();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GenerateMeshLibrary();
        ApplyToOpenScene();
        ValidateOpenScene();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("High-detail tree masters and four component LOD levels built. Unity render verification remains pending.");
    }

    [MenuItem("NewTown/Geometry/Generate Tree Detail Mesh Library Only")]
    public static void GenerateMeshLibrary()
    {
        Directory.CreateDirectory(MeshRoot);
        SaveMesh("GM_TreeRootFlare", BuildTaperedSegmentMesh(10, 0.58f, 0.08f, 11));
        SaveMesh("GM_TreeTrunkSegment", BuildTaperedSegmentMesh(12, 0.78f, 0.045f, 23));
        SaveMesh("GM_TreePrimaryBranch", BuildTaperedSegmentMesh(10, 0.62f, 0.055f, 37));
        SaveMesh("GM_TreeSecondaryBranch", BuildTaperedSegmentMesh(8, 0.54f, 0.065f, 53));
        SaveMesh("GM_TreeTwig", BuildTaperedSegmentMesh(7, 0.42f, 0.075f, 71));
        SaveMesh("GM_LeafSpray_A", BuildLeafSprayMesh(18, 101));
        SaveMesh("GM_LeafSpray_B", BuildLeafSprayMesh(20, 211));
        SaveMesh("GM_LeafSpray_C", BuildLeafSprayMesh(16, 307));
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("NewTown/Geometry/Apply High-Detail Tree Pass Only")]
    public static void ApplyToOpenScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() ||
            EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Material bark = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/GeneratedPBR/PBR_Bark.mat");
        Material leafDark = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/GeneratedPBR/PBR_LeafDark.mat");
        Material leafMid = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/GeneratedPBR/PBR_LeafMid.mat");
        if (bark == null || leafDark == null || leafMid == null)
            throw new InvalidOperationException("Tree detail pass requires generated PBR bark and leaf materials.");

        QualityBlockArtSlot[] slots = Resources.FindObjectsOfTypeAll<QualityBlockArtSlot>()
            .Where(x => x.gameObject.scene.IsValid() && x.SlotId.StartsWith("vegetation.tree.", StringComparison.Ordinal))
            .OrderBy(x => x.SlotId, StringComparer.Ordinal)
            .ToArray();
        if (slots.Length != 6)
            throw new InvalidOperationException($"Expected six tree art slots, got {slots.Length}.");

        foreach (QualityBlockArtSlot slot in slots)
        {
            int treeIndex = ParseTreeIndex(slot.SlotId);
            if (slot.IsUsingAuthoredArt)
            {
                Debug.Log($"{slot.SlotId}: authored replacement active; generated high-detail tree fallback skipped.");
                continue;
            }

            if (slot.FallbackRoot == null)
                throw new InvalidOperationException($"{slot.SlotId}: fallback root missing.");

            RemoveOldDetailedTree(slot, treeIndex);
            DisableLegacyFallbackRenderers(slot.FallbackRoot, treeIndex);

            float height = 5.6f + (treeIndex % 3) * 0.9f;
            GameObject master = new GameObject(MasterPrefix + treeIndex);
            master.transform.SetParent(slot.FallbackRoot.transform, false);

            BuildTreeMaster(master.transform, treeIndex, height, bark, leafDark, leafMid);
            BuildLods(master);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    [MenuItem("NewTown/QA/Validate High-Detail Trees")]
    public static void ValidateOpenScene()
    {
        QualityBlockArtSlot[] slots = Resources.FindObjectsOfTypeAll<QualityBlockArtSlot>()
            .Where(x => x.gameObject.scene.IsValid() && x.SlotId.StartsWith("vegetation.tree.", StringComparison.Ordinal))
            .OrderBy(x => x.SlotId, StringComparer.Ordinal)
            .ToArray();
        if (slots.Length != 6)
            throw new InvalidOperationException($"Expected six tree art slots, got {slots.Length}.");

        int validated = 0;
        foreach (QualityBlockArtSlot slot in slots)
        {
            if (slot.IsUsingAuthoredArt) continue;
            int treeIndex = ParseTreeIndex(slot.SlotId);
            Transform master = slot.FallbackRoot.transform.Find(MasterPrefix + treeIndex);
            if (master == null)
                throw new InvalidOperationException($"Tree {treeIndex} high-detail master missing.");

            MeshRenderer[] sourceRenderers = GetSourceRenderers(master.gameObject);
            int rootCount = sourceRenderers.Count(r => r.gameObject.name.StartsWith("RootFlare_", StringComparison.Ordinal));
            int trunkCount = sourceRenderers.Count(r => r.gameObject.name.StartsWith("TrunkSection_", StringComparison.Ordinal));
            int primaryCount = sourceRenderers.Count(r => r.gameObject.name.StartsWith("PrimaryBranch_", StringComparison.Ordinal));
            int secondaryCount = sourceRenderers.Count(r => r.gameObject.name.StartsWith("SecondaryBranch_", StringComparison.Ordinal));
            int twigCount = sourceRenderers.Count(r => r.gameObject.name.StartsWith("Twig_", StringComparison.Ordinal));
            int leafCount = sourceRenderers.Count(r => r.gameObject.name.StartsWith("LeafCluster_", StringComparison.Ordinal));

            if (rootCount < 6 || trunkCount < 6 || primaryCount < 12 || secondaryCount < 12 || twigCount < 24 || leafCount < 24)
                throw new InvalidOperationException(
                    $"Tree {treeIndex} hierarchy is under-detailed: roots/trunk/primary/secondary/twigs/leaves=" +
                    $"{rootCount}/{trunkCount}/{primaryCount}/{secondaryCount}/{twigCount}/{leafCount}.");

            foreach (MeshRenderer renderer in sourceRenderers)
            {
                Mesh mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null)
                    throw new InvalidOperationException($"Tree {treeIndex} renderer {renderer.name} has no mesh.");
                if (mesh.name == "Sphere" || mesh.name == "Cylinder" || mesh.name == "Cube")
                    throw new InvalidOperationException($"Tree {treeIndex} still contains primitive mesh {mesh.name} on {renderer.name}.");
                if (!mesh.name.StartsWith("GM_Tree", StringComparison.Ordinal) &&
                    !mesh.name.StartsWith("GM_LeafSpray", StringComparison.Ordinal))
                    throw new InvalidOperationException($"Tree {treeIndex} uses unexpected mesh {mesh.name} on {renderer.name}.");
            }

            LODGroup group = master.GetComponent<LODGroup>();
            if (group == null) throw new InvalidOperationException($"Tree {treeIndex} LODGroup missing.");
            LOD[] lods = group.GetLODs();
            if (lods.Length != 4)
                throw new InvalidOperationException($"Tree {treeIndex} expected four LODs, got {lods.Length}.");
            int[] counts = lods.Select(l => l.renderers.Length).ToArray();
            if (!(counts[0] > counts[1] && counts[1] > counts[2] && counts[2] > counts[3] && counts[3] > 0))
                throw new InvalidOperationException(
                    $"Tree {treeIndex} LOD renderer reduction invalid: {string.Join("/", counts)}.");
            if (group.fadeMode != LODFadeMode.CrossFade || !group.animateCrossFading)
                throw new InvalidOperationException($"Tree {treeIndex} LODs must use animated cross-fade.");

            MeshRenderer[] legacy = slot.FallbackRoot.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r => r.gameObject.name == $"Trunk_{treeIndex}" ||
                            r.gameObject.name.StartsWith($"Crown_{treeIndex}_", StringComparison.Ordinal))
                .ToArray();
            if (legacy.Any(r => r.enabled))
                throw new InvalidOperationException($"Tree {treeIndex} legacy trunk/crown renderer is still enabled.");

            Collider trunkCollider = slot.FallbackRoot.GetComponentsInChildren<Collider>(true)
                .FirstOrDefault(c => c.gameObject.name == $"Trunk_{treeIndex}");
            Collider[] crownColliders = slot.FallbackRoot.GetComponentsInChildren<Collider>(true)
                .Where(c => c.gameObject.name.StartsWith($"Crown_{treeIndex}_", StringComparison.Ordinal))
                .ToArray();
            if (trunkCollider == null || !trunkCollider.enabled)
                throw new InvalidOperationException($"Tree {treeIndex} requires an enabled simple trunk gameplay collider.");
            if (crownColliders.Any(c => c.enabled))
                throw new InvalidOperationException($"Tree {treeIndex} crown art colliders must be disabled; collision stays separated on the trunk.");

            validated++;
        }

        if (validated == 0)
            Debug.Log("All six tree art slots use authored replacements; generated tree master validation was not applicable.");
        else
            Debug.Log($"High-detail tree structural validation passed for {validated} generated fallbacks. Actual leaf shading, wind response and LOD transition appearance still require Unity render inspection.");
    }

    private static void BuildTreeMaster(Transform root, int treeIndex, float height, Material bark, Material leafDark, Material leafMid)
    {
        var rootAssembly = NewAssembly("RootFlareAssembly", root);
        for (int r = 0; r < 7; r++)
        {
            float angle = (treeIndex * 31f + r * (360f / 7f)) * Mathf.Deg2Rad;
            float length = 0.72f + 0.22f * Hash01(treeIndex, r, 1);
            Vector3 start = new Vector3(Mathf.Cos(angle) * 0.06f, 0.10f, Mathf.Sin(angle) * 0.06f);
            Vector3 end = new Vector3(Mathf.Cos(angle) * length, 0.025f + 0.045f * Hash01(treeIndex, r, 2), Mathf.Sin(angle) * length);
            CreateSegment($"RootFlare_{r}", rootAssembly, start, end, 0.16f + 0.035f * Hash01(treeIndex, r, 3), "GM_TreeRootFlare", bark);
        }

        var trunkAssembly = NewAssembly("TrunkAssembly", root);
        Vector3[] trunkPoints = new Vector3[8];
        for (int p = 0; p < trunkPoints.Length; p++)
        {
            float t = p / (float)(trunkPoints.Length - 1);
            trunkPoints[p] = new Vector3(
                Mathf.Sin(treeIndex * 0.73f + t * 4.1f) * (0.08f + 0.05f * t),
                Mathf.Lerp(0.04f, height * 0.80f, t),
                Mathf.Cos(treeIndex * 0.51f + t * 3.5f) * (0.07f + 0.045f * t));
        }
        for (int p = 0; p < trunkPoints.Length - 1; p++)
        {
            float t = p / (float)(trunkPoints.Length - 1);
            float radius = Mathf.Lerp(0.31f, 0.115f, t);
            CreateSegment($"TrunkSection_{p}", trunkAssembly, trunkPoints[p], trunkPoints[p + 1], radius, "GM_TreeTrunkSegment", bark);
        }

        var crownAssembly = NewAssembly("BranchCrownAssembly", root);
        int clusterOrdinal = 0;
        const int primaryBranches = 7;
        for (int p = 0; p < primaryBranches; p++)
        {
            float trunkT = 0.42f + p * 0.055f;
            Vector3 origin = SamplePolyline(trunkPoints, trunkT);
            float angle = (treeIndex * 43f + p * (360f / primaryBranches) + 18f * HashSigned(treeIndex, p, 4)) * Mathf.Deg2Rad;
            float primaryLength = 2.0f + 0.75f * Hash01(treeIndex, p, 5);
            Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 mid = origin + radial * (primaryLength * 0.48f) + Vector3.up * (0.42f + 0.24f * Hash01(treeIndex, p, 6));
            Vector3 tip = origin + radial * primaryLength + Vector3.up * (0.78f + 0.48f * Hash01(treeIndex, p, 7));

            var primaryAssembly = NewAssembly($"PrimaryAssembly_{p}", crownAssembly);
            CreateSegment($"PrimaryBranch_{p}_A", primaryAssembly, origin, mid, 0.13f, "GM_TreePrimaryBranch", bark);
            CreateSegment($"PrimaryBranch_{p}_B", primaryAssembly, mid, tip, 0.095f, "GM_TreePrimaryBranch", bark);

            for (int s = 0; s < 2; s++)
            {
                float side = s == 0 ? -1f : 1f;
                Vector3 tangent = (tip - mid).normalized;
                Vector3 lateral = Vector3.Cross(Vector3.up, tangent).normalized * side;
                Vector3 secondaryStart = Vector3.Lerp(mid, tip, 0.48f + 0.18f * s);
                float secondaryLength = 1.15f + 0.48f * Hash01(treeIndex, p, 20 + s);
                Vector3 secondaryTip = secondaryStart +
                                       (tangent * 0.42f + lateral * 0.82f + Vector3.up * (0.28f + 0.18f * s)).normalized * secondaryLength;
                CreateSegment($"SecondaryBranch_{p}_{s}", primaryAssembly, secondaryStart, secondaryTip,
                    0.061f, "GM_TreeSecondaryBranch", bark);

                for (int t = 0; t < 2; t++)
                {
                    float twigSide = t == 0 ? -1f : 1f;
                    Vector3 secondaryDir = (secondaryTip - secondaryStart).normalized;
                    Vector3 twigLateral = Vector3.Cross(Vector3.up, secondaryDir).normalized * twigSide;
                    Vector3 twigStart = Vector3.Lerp(secondaryStart, secondaryTip, 0.58f + 0.20f * t);
                    float twigLength = 0.62f + 0.34f * Hash01(treeIndex, p * 10 + s * 2 + t, 31);
                    Vector3 twigTip = twigStart +
                                      (secondaryDir * 0.42f + twigLateral * 0.62f + Vector3.up * 0.48f).normalized * twigLength;
                    CreateSegment($"Twig_{p}_{s}_{t}", primaryAssembly, twigStart, twigTip,
                        0.030f, "GM_TreeTwig", bark);

                    int keep = clusterOrdinal % 8 == 0 ? 3 :
                               (clusterOrdinal % 4 == 0 ? 2 : (clusterOrdinal % 2 == 0 ? 1 : 0));
                    Material leafMaterial = ((treeIndex + clusterOrdinal) & 1) == 0 ? leafDark : leafMid;
                    CreateLeafCluster(primaryAssembly, treeIndex, clusterOrdinal, keep, twigTip,
                        secondaryDir, leafMaterial);
                    clusterOrdinal++;
                }
            }
        }
    }

    private static void CreateLeafCluster(Transform parent, int treeIndex, int ordinal, int keepThrough,
        Vector3 position, Vector3 branchDirection, Material material)
    {
        var go = new GameObject($"LeafCluster_{ordinal:D2}_Keep{keepThrough}");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        Vector3 up = Vector3.Lerp(Vector3.up, branchDirection, 0.22f).normalized;
        go.transform.localRotation = Quaternion.LookRotation(branchDirection.sqrMagnitude > 0.001f ? branchDirection : Vector3.forward, up);
        float scale = 0.72f + 0.25f * Hash01(treeIndex, ordinal, 71);
        go.transform.localScale = new Vector3(scale * (0.88f + 0.15f * Hash01(treeIndex, ordinal, 72)),
            scale * (0.72f + 0.12f * Hash01(treeIndex, ordinal, 73)), scale);
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = LoadMesh($"GM_LeafSpray_{(char)('A' + ((treeIndex + ordinal) % 3))}");
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = material;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        mr.receiveShadows = true;
    }

    private static GameObject NewAssembly(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void CreateSegment(string name, Transform parent, Vector3 start, Vector3 end,
        float startRadius, string meshName, Material material)
    {
        Vector3 delta = end - start;
        if (delta.sqrMagnitude < 0.000001f)
            throw new InvalidOperationException($"Cannot create zero-length tree segment {name}.");

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = start;
        go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
        go.transform.localScale = new Vector3(startRadius, delta.magnitude, startRadius);
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = LoadMesh(meshName);
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = material;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        mr.receiveShadows = true;
    }

    private static void BuildLods(GameObject master)
    {
        RemoveProxyRoots(master.transform);
        LODGroup oldGroup = master.GetComponent<LODGroup>();
        if (oldGroup != null) UnityEngine.Object.DestroyImmediate(oldGroup);

        MeshRenderer[] sources = GetSourceRenderers(master);
        var lod0 = new List<Renderer>(sources);
        var lod1 = new List<Renderer>();
        var lod2 = new List<Renderer>();
        var lod3 = new List<Renderer>();
        Transform lod1Root = NewAssembly(Lod1RootName, master.transform).transform;
        Transform lod2Root = NewAssembly(Lod2RootName, master.transform).transform;
        Transform lod3Root = NewAssembly(Lod3RootName, master.transform).transform;

        foreach (MeshRenderer source in sources)
        {
            int retained = RetainedThroughLod(source.gameObject.name);
            if (retained >= 1) lod1.Add(CreateProxy(source, lod1Root, 1));
            if (retained >= 2) lod2.Add(CreateProxy(source, lod2Root, 2));
            if (retained >= 3) lod3.Add(CreateProxy(source, lod3Root, 3));
        }

        if (lod1.Count == 0 || lod2.Count == 0 || lod3.Count == 0)
            throw new InvalidOperationException($"Tree LOD classification produced empty level {lod1.Count}/{lod2.Count}/{lod3.Count}.");

        var group = master.AddComponent<LODGroup>();
        group.SetLODs(new[]
        {
            new LOD(Lod0Transition, lod0.ToArray()),
            new LOD(Lod1Transition, lod1.ToArray()),
            new LOD(Lod2Transition, lod2.ToArray()),
            new LOD(Lod3Cull, lod3.ToArray())
        });
        group.fadeMode = LODFadeMode.CrossFade;
        group.animateCrossFading = true;
        group.RecalculateBounds();
    }

    private static MeshRenderer CreateProxy(MeshRenderer source, Transform proxyRoot, int lod)
    {
        var go = new GameObject($"LOD{lod}_{source.gameObject.name}");
        go.transform.SetParent(proxyRoot, false);
        go.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        go.transform.localScale = DivideLossyScale(source.transform.lossyScale, proxyRoot.lossyScale);
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterials = source.sharedMaterials;
        mr.shadowCastingMode = source.shadowCastingMode;
        mr.receiveShadows = source.receiveShadows;
        mr.lightProbeUsage = source.lightProbeUsage;
        mr.reflectionProbeUsage = source.reflectionProbeUsage;
        return mr;
    }

    private static int RetainedThroughLod(string objectName)
    {
        if (objectName.StartsWith("RootFlare_", StringComparison.Ordinal) ||
            objectName.StartsWith("TrunkSection_", StringComparison.Ordinal) ||
            objectName.StartsWith("PrimaryBranch_", StringComparison.Ordinal))
            return 3;
        if (objectName.StartsWith("SecondaryBranch_", StringComparison.Ordinal)) return 2;
        if (objectName.StartsWith("Twig_", StringComparison.Ordinal)) return 1;
        if (objectName.StartsWith("LeafCluster_", StringComparison.Ordinal))
        {
            if (objectName.EndsWith("Keep3", StringComparison.Ordinal)) return 3;
            if (objectName.EndsWith("Keep2", StringComparison.Ordinal)) return 2;
            if (objectName.EndsWith("Keep1", StringComparison.Ordinal)) return 1;
            return 0;
        }
        return 1;
    }

    private static MeshRenderer[] GetSourceRenderers(GameObject master)
    {
        return master.GetComponentsInChildren<MeshRenderer>(true)
            .Where(r => !IsUnderProxy(r.transform, master.transform))
            .OrderBy(r => HierarchyPath(r.transform, master.transform), StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsUnderProxy(Transform transform, Transform master)
    {
        Transform current = transform;
        while (current != null && current != master)
        {
            if (current.name == Lod1RootName || current.name == Lod2RootName || current.name == Lod3RootName)
                return true;
            current = current.parent;
        }
        return false;
    }

    private static void RemoveProxyRoots(Transform master)
    {
        foreach (string n in new[] { Lod1RootName, Lod2RootName, Lod3RootName })
        {
            Transform old = master.Find(n);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
        }
    }

    private static void RemoveOldDetailedTree(QualityBlockArtSlot slot, int treeIndex)
    {
        LODGroup legacyGroup = slot.GetComponent<LODGroup>();
        if (legacyGroup != null) UnityEngine.Object.DestroyImmediate(legacyGroup);
        Transform old = slot.FallbackRoot.transform.Find(MasterPrefix + treeIndex);
        if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
    }

    private static void DisableLegacyFallbackRenderers(GameObject fallbackRoot, int treeIndex)
    {
        foreach (MeshRenderer renderer in fallbackRoot.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (renderer.gameObject.name == $"Trunk_{treeIndex}" ||
                renderer.gameObject.name.StartsWith($"Crown_{treeIndex}_", StringComparison.Ordinal))
                renderer.enabled = false;
        }

        // Keep collision intentionally cheaper than art: one simple trunk collider remains for
        // gameplay, while invisible spherical crown colliders are disabled so the player does not
        // collide with empty foliage volume. This preserves collider/art separation as detail rises.
        foreach (Collider collider in fallbackRoot.GetComponentsInChildren<Collider>(true))
        {
            if (collider.gameObject.name == $"Trunk_{treeIndex}")
                collider.enabled = true;
            else if (collider.gameObject.name.StartsWith($"Crown_{treeIndex}_", StringComparison.Ordinal))
                collider.enabled = false;
        }
    }

    private static int ParseTreeIndex(string slotId)
    {
        int lastDot = slotId.LastIndexOf('.');
        if (lastDot < 0 || !int.TryParse(slotId.Substring(lastDot + 1), out int index) || index < 0 || index > 5)
            throw new InvalidOperationException($"Invalid tree slot id {slotId}.");
        return index;
    }

    private static Vector3 SamplePolyline(Vector3[] points, float t)
    {
        t = Mathf.Clamp01(t);
        float scaled = t * (points.Length - 1);
        int i = Mathf.Min(points.Length - 2, Mathf.FloorToInt(scaled));
        return Vector3.Lerp(points[i], points[i + 1], scaled - i);
    }

    private static Vector3 DivideLossyScale(Vector3 worldScale, Vector3 parentScale)
    {
        return new Vector3(
            SafeDivide(worldScale.x, parentScale.x),
            SafeDivide(worldScale.y, parentScale.y),
            SafeDivide(worldScale.z, parentScale.z));
    }

    private static float SafeDivide(float a, float b)
    {
        return Mathf.Abs(b) < 0.00001f ? a : a / b;
    }

    private static string HierarchyPath(Transform transform, Transform root)
    {
        var parts = new List<string>();
        Transform current = transform;
        while (current != null && current != root)
        {
            parts.Add(current.name);
            current = current.parent;
        }
        parts.Reverse();
        return string.Join("/", parts);
    }

    private static Mesh LoadMesh(string meshName)
    {
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshRoot}/{meshName}.asset");
        if (mesh == null) throw new InvalidOperationException($"Generated tree mesh {meshName} not found.");
        return mesh;
    }

    private static void SaveMesh(string name, Mesh generated)
    {
        generated.name = name;
        string path = $"{MeshRoot}/{name}.asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(generated, path);
        }
        else
        {
            EditorUtility.CopySerialized(generated, existing);
            existing.name = name;
            EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(generated);
        }
    }

    private static Mesh BuildTaperedSegmentMesh(int sides, float endRadiusRatio, float irregularity, int seed)
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        var uv = new List<Vector2>();
        const int rings = 4;

        for (int r = 0; r < rings; r++)
        {
            float t = r / (float)(rings - 1);
            float radius = Mathf.Lerp(1f, endRadiusRatio, t);
            float lateral = Mathf.Sin(seed * 0.17f + t * 3.4f) * 0.035f;
            for (int s = 0; s < sides; s++)
            {
                float a = Mathf.PI * 2f * s / sides;
                float wobble = 1f + irregularity * Mathf.Sin(seed * 0.31f + s * 1.93f + r * 0.77f);
                vertices.Add(new Vector3(Mathf.Cos(a) * radius * wobble + lateral, t,
                    Mathf.Sin(a) * radius * wobble));
                uv.Add(new Vector2(s / (float)sides, t));
            }
        }

        for (int r = 0; r < rings - 1; r++)
        for (int s = 0; s < sides; s++)
        {
            int next = (s + 1) % sides;
            int a = r * sides + s;
            int b = r * sides + next;
            int c = (r + 1) * sides + s;
            int d = (r + 1) * sides + next;
            triangles.Add(a); triangles.Add(c); triangles.Add(b);
            triangles.Add(b); triangles.Add(c); triangles.Add(d);
        }

        int bottomCenter = vertices.Count;
        vertices.Add(Vector3.zero);
        uv.Add(new Vector2(0.5f, 0.5f));
        int topCenter = vertices.Count;
        vertices.Add(new Vector3(0f, 1f, 0f));
        uv.Add(new Vector2(0.5f, 0.5f));
        for (int s = 0; s < sides; s++)
        {
            int next = (s + 1) % sides;
            triangles.Add(bottomCenter); triangles.Add(s); triangles.Add(next);
            int top = (rings - 1) * sides;
            triangles.Add(topCenter); triangles.Add(top + next); triangles.Add(top + s);
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

    private static Mesh BuildLeafSprayMesh(int leafCount, int seed)
    {
        var vertices = new List<Vector3>(leafCount * 8);
        var triangles = new List<int>(leafCount * 12);
        var uv = new List<Vector2>(leafCount * 8);

        for (int i = 0; i < leafCount; i++)
        {
            float azimuth = Mathf.PI * 2f * Hash01(seed, i, 1);
            float radial = Mathf.Sqrt(Hash01(seed, i, 2));
            float vertical = HashSigned(seed, i, 3) * 0.58f;
            Vector3 center = new Vector3(Mathf.Cos(azimuth) * radial, vertical, Mathf.Sin(azimuth) * radial);
            Vector3 normal = new Vector3(HashSigned(seed, i, 4), 0.35f + Hash01(seed, i, 5), HashSigned(seed, i, 6)).normalized;
            Vector3 tangent = Vector3.Cross(normal, Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.92f ? Vector3.right : Vector3.up).normalized;
            Vector3 bitangent = Vector3.Cross(normal, tangent).normalized;
            float halfLength = 0.095f + 0.035f * Hash01(seed, i, 7);
            float halfWidth = halfLength * (0.42f + 0.10f * Hash01(seed, i, 8));
            Vector3 tip = center + bitangent * halfLength;
            Vector3 right = center + tangent * halfWidth;
            Vector3 basePoint = center - bitangent * halfLength;
            Vector3 left = center - tangent * halfWidth;
            int v = vertices.Count;

            // Duplicate front/back vertices so normals and tangents are well-defined on both sides.
            vertices.Add(tip); vertices.Add(right); vertices.Add(basePoint); vertices.Add(left);
            vertices.Add(tip); vertices.Add(left); vertices.Add(basePoint); vertices.Add(right);
            uv.Add(new Vector2(0.5f, 1f)); uv.Add(new Vector2(1f, 0.5f)); uv.Add(new Vector2(0.5f, 0f)); uv.Add(new Vector2(0f, 0.5f));
            uv.Add(new Vector2(0.5f, 1f)); uv.Add(new Vector2(0f, 0.5f)); uv.Add(new Vector2(0.5f, 0f)); uv.Add(new Vector2(1f, 0.5f));
            triangles.Add(v); triangles.Add(v + 1); triangles.Add(v + 2);
            triangles.Add(v); triangles.Add(v + 2); triangles.Add(v + 3);
            triangles.Add(v + 4); triangles.Add(v + 5); triangles.Add(v + 6);
            triangles.Add(v + 4); triangles.Add(v + 6); triangles.Add(v + 7);
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

    private static float Hash01(int a, int b, int c)
    {
        unchecked
        {
            uint x = (uint)(a * 73856093) ^ (uint)(b * 19349663) ^ (uint)(c * 83492791);
            x ^= x >> 13;
            x *= 1274126177u;
            x ^= x >> 16;
            return (x & 0x00FFFFFFu) / 16777215f;
        }
    }

    private static float HashSigned(int a, int b, int c)
    {
        return Hash01(a, b, c) * 2f - 1f;
    }
}
