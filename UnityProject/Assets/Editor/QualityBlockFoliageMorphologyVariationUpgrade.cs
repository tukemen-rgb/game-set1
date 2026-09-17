using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Breaks repeated leaf-spray silhouettes without randomising tree placement or branch construction.
/// The existing three biologically plausible leaf-spray masters remain the source morphology; this
/// pass derives twelve deterministic variants with small per-leaf size, angle and attachment-offset
/// changes, then assigns one stable variant per tree/cluster ordinal across every retained LOD proxy.
///
/// This is a source-side visual-risk reduction only. It does not claim that repetition, shimmer or
/// LOD transition artifacts are absent until native 3840x2160 still/temporal evidence is reviewed.
/// </summary>
public static class QualityBlockFoliageMorphologyVariationUpgrade
{
    public const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    public const string ContractPath = "Assets/QA/foliage_morphology_diversity_contract.json";
    public const string MeshRoot = "Assets/Art/GeneratedTreeMeshes";
    public const string MasterPrefix = "HD_TreeMaster_";
    public const int VariantCount = 12;
    public const int MinimumDistinctVariantsPerTree = 10;
    public const int MaximumUsePerVariantPerTree = 4;

    private static readonly string[] BaseSprayNames =
    {
        "GM_LeafSpray_A",
        "GM_LeafSpray_B",
        "GM_LeafSpray_C",
    };

    [MenuItem("NewTown/Geometry/Build Foliage Morphology Diversity")]
    public static void BuildAndApply()
    {
        QualityBlockTreeDetailUpgrade.BuildDetailedTrees();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        ApplyToOpenScene();
        ValidateCurrentScene(true);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Foliage morphology diversity built. Native 4K repetition/aliasing review remains pending.");
    }

    public static void ApplyToOpenScene()
    {
        EnsureBenchmarkScene();
        GenerateVariantMeshes();

        QualityBlockArtSlot[] slots = Resources.FindObjectsOfTypeAll<QualityBlockArtSlot>()
            .Where(x => x.gameObject.scene.IsValid() && x.SlotId.StartsWith("vegetation.tree.", StringComparison.Ordinal))
            .OrderBy(x => x.SlotId, StringComparer.Ordinal)
            .ToArray();
        if (slots.Length != 6)
            throw new InvalidOperationException($"Expected six tree art slots before foliage morphology pass, got {slots.Length}.");

        Mesh[] variants = LoadVariants();
        foreach (QualityBlockArtSlot slot in slots)
        {
            if (slot.IsUsingAuthoredArt)
                continue;

            int treeIndex = ParseTreeIndex(slot.SlotId);
            Transform master = slot.FallbackRoot != null ? slot.FallbackRoot.transform.Find(MasterPrefix + treeIndex) : null;
            if (master == null)
                throw new InvalidOperationException($"Tree {treeIndex} detailed master missing before foliage morphology pass.");

            MeshRenderer[] leaves = master.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r => r.gameObject.name.Contains("LeafCluster_"))
                .ToArray();
            if (leaves.Length == 0)
                throw new InvalidOperationException($"Tree {treeIndex} has no explicit leaf-cluster renderers.");

            foreach (MeshRenderer renderer in leaves)
            {
                int ordinal = ParseLeafOrdinal(renderer.gameObject.name);
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null)
                    throw new InvalidOperationException($"Tree {treeIndex} leaf {renderer.name} has no MeshFilter.");
                filter.sharedMesh = variants[VariantIndex(treeIndex, ordinal)];
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
    }

    [MenuItem("NewTown/QA/Validate Foliage Morphology Diversity")]
    public static void ValidateCurrentSceneMenu()
    {
        ValidateCurrentScene(true);
    }

    public static void ValidateCurrentScene(bool logSuccess)
    {
        EnsureBenchmarkScene();
        Mesh[] variants = LoadVariants();
        ValidateVariantMeshAssets(variants);

        QualityBlockArtSlot[] slots = Resources.FindObjectsOfTypeAll<QualityBlockArtSlot>()
            .Where(x => x.gameObject.scene.IsValid() && x.SlotId.StartsWith("vegetation.tree.", StringComparison.Ordinal))
            .OrderBy(x => x.SlotId, StringComparer.Ordinal)
            .ToArray();
        if (slots.Length != 6)
            throw new InvalidOperationException($"Expected six tree art slots, got {slots.Length}.");

        int validatedTrees = 0;
        foreach (QualityBlockArtSlot slot in slots)
        {
            if (slot.IsUsingAuthoredArt)
                continue;

            int treeIndex = ParseTreeIndex(slot.SlotId);
            Transform master = slot.FallbackRoot != null ? slot.FallbackRoot.transform.Find(MasterPrefix + treeIndex) : null;
            if (master == null)
                throw new InvalidOperationException($"Tree {treeIndex} detailed master missing during morphology validation.");

            MeshRenderer[] sourceLeaves = master.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r => r.gameObject.name.StartsWith("LeafCluster_", StringComparison.Ordinal))
                .ToArray();
            MeshRenderer[] allLeaves = master.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r => r.gameObject.name.Contains("LeafCluster_"))
                .ToArray();
            if (sourceLeaves.Length < 24)
                throw new InvalidOperationException($"Tree {treeIndex} has only {sourceLeaves.Length} source leaf clusters; morphology diversity cannot be evidenced structurally.");
            if (allLeaves.Length <= sourceLeaves.Length)
                throw new InvalidOperationException($"Tree {treeIndex} has no retained foliage LOD proxies to validate.");

            var sourceUse = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (MeshRenderer renderer in sourceLeaves)
            {
                int ordinal = ParseLeafOrdinal(renderer.gameObject.name);
                Mesh expected = variants[VariantIndex(treeIndex, ordinal)];
                Mesh actual = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (actual != expected)
                    throw new InvalidOperationException($"Tree {treeIndex} source leaf {renderer.name} morphology drifted: expected {expected.name}, got {(actual == null ? "<null>" : actual.name)}.");
                sourceUse[actual.name] = sourceUse.TryGetValue(actual.name, out int count) ? count + 1 : 1;
            }

            if (sourceUse.Count < MinimumDistinctVariantsPerTree)
                throw new InvalidOperationException($"Tree {treeIndex} uses only {sourceUse.Count} distinct leaf-spray morphologies; minimum is {MinimumDistinctVariantsPerTree}.");
            int maxUse = sourceUse.Values.Max();
            if (maxUse > MaximumUsePerVariantPerTree)
                throw new InvalidOperationException($"Tree {treeIndex} repeats one leaf-spray morphology {maxUse} times in LOD0; maximum allowed before rendered review is {MaximumUsePerVariantPerTree}.");

            foreach (MeshRenderer renderer in allLeaves)
            {
                int ordinal = ParseLeafOrdinal(renderer.gameObject.name);
                Mesh expected = variants[VariantIndex(treeIndex, ordinal)];
                Mesh actual = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (actual != expected)
                    throw new InvalidOperationException($"Tree {treeIndex} LOD foliage {renderer.name} does not preserve the source morphology identity for ordinal {ordinal}.");
            }

            validatedTrees++;
        }

        if (logSuccess)
        {
            if (validatedTrees == 0)
                Debug.Log("All tree slots use authored replacements; generated foliage morphology validation was not applicable.");
            else
                Debug.Log($"Foliage morphology source QA passed for {validatedTrees} generated trees with deterministic 12-variant assignment. Actual 4K repetition, silhouette naturalness, shimmer and LOD visibility remain render-unverified.");
        }
    }

    public static void GenerateVariantMeshes()
    {
        Directory.CreateDirectory(MeshRoot);
        Mesh[] bases = BaseSprayNames.Select(LoadBase).ToArray();

        for (int variant = 0; variant < VariantCount; variant++)
        {
            Mesh source = bases[variant % bases.Length];
            Mesh derived = BuildVariant(source, variant);
            SaveOrReplaceMesh(VariantAssetPath(variant), derived);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static Mesh BuildVariant(Mesh source, int variant)
    {
        Vector3[] sourceVertices = source.vertices;
        if (sourceVertices == null || sourceVertices.Length < 8 || sourceVertices.Length % 8 != 0)
            throw new InvalidOperationException($"Base leaf spray {source.name} must contain contiguous two-sided leaf groups of eight vertices.");

        Vector3[] vertices = (Vector3[])sourceVertices.Clone();
        int leafCount = vertices.Length / 8;
        float boundsScale = Mathf.Max(0.1f, source.bounds.extents.magnitude);

        for (int leaf = 0; leaf < leafCount; leaf++)
        {
            int first = leaf * 8;
            Vector3 center = Vector3.zero;
            for (int i = 0; i < 4; i++) center += sourceVertices[first + i];
            center *= 0.25f;

            float widthScale = Mathf.Lerp(0.90f, 1.08f, Hash01(variant, leaf, 101));
            float lengthScale = Mathf.Lerp(0.92f, 1.10f, Hash01(variant, leaf, 103));
            float depthScale = Mathf.Lerp(0.97f, 1.03f, Hash01(variant, leaf, 107));
            float pitch = Mathf.Lerp(-8f, 8f, Hash01(variant, leaf, 109));
            float yaw = Mathf.Lerp(-12f, 12f, Hash01(variant, leaf, 113));
            float roll = Mathf.Lerp(-18f, 18f, Hash01(variant, leaf, 127));
            Quaternion rotation = Quaternion.Euler(pitch, yaw, roll);
            Vector3 offset = new Vector3(
                Mathf.Lerp(-0.010f, 0.010f, Hash01(variant, leaf, 131)),
                Mathf.Lerp(-0.008f, 0.012f, Hash01(variant, leaf, 137)),
                Mathf.Lerp(-0.010f, 0.010f, Hash01(variant, leaf, 139))) * Mathf.Min(1f, boundsScale);

            Vector3 scale = new Vector3(widthScale, lengthScale, depthScale);
            for (int i = 0; i < 8; i++)
            {
                Vector3 relative = sourceVertices[first + i] - center;
                vertices[first + i] = center + offset + rotation * Vector3.Scale(relative, scale);
            }
        }

        var mesh = new Mesh
        {
            name = VariantName(variant),
            indexFormat = source.indexFormat,
            vertices = vertices,
            triangles = source.triangles,
            uv = source.uv,
            uv2 = source.uv2,
            colors = source.colors,
        };
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void ValidateVariantMeshAssets(Mesh[] variants)
    {
        if (variants.Length != VariantCount || variants.Any(x => x == null))
            throw new InvalidOperationException($"Expected {VariantCount} generated foliage morphology meshes.");

        var uniqueNames = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < variants.Length; i++)
        {
            Mesh mesh = variants[i];
            if (!string.Equals(mesh.name, VariantName(i), StringComparison.Ordinal))
                throw new InvalidOperationException($"Morphology asset {i} has unexpected mesh name {mesh.name}.");
            if (!uniqueNames.Add(mesh.name))
                throw new InvalidOperationException($"Duplicate foliage morphology mesh name {mesh.name}.");
            if (mesh.vertexCount < 8 || mesh.vertexCount % 8 != 0 || mesh.triangles == null || mesh.triangles.Length < 12)
                throw new InvalidOperationException($"Morphology mesh {mesh.name} has invalid two-sided leaf topology.");
            if (mesh.uv == null || mesh.uv.Length != mesh.vertexCount)
                throw new InvalidOperationException($"Morphology mesh {mesh.name} lost leaf UVs.");
            if (mesh.normals == null || mesh.normals.Length != mesh.vertexCount || mesh.tangents == null || mesh.tangents.Length != mesh.vertexCount)
                throw new InvalidOperationException($"Morphology mesh {mesh.name} lacks complete normals/tangents for PBR foliage shading.");
            if (!FinitePositive(mesh.bounds.size.x) || !FinitePositive(mesh.bounds.size.y) || !FinitePositive(mesh.bounds.size.z))
                throw new InvalidOperationException($"Morphology mesh {mesh.name} has invalid bounds.");
        }
    }

    private static Mesh[] LoadVariants()
    {
        var result = new Mesh[VariantCount];
        for (int i = 0; i < VariantCount; i++)
        {
            result[i] = AssetDatabase.LoadAssetAtPath<Mesh>(VariantAssetPath(i));
            if (result[i] == null)
                throw new InvalidOperationException($"Missing foliage morphology mesh {VariantAssetPath(i)}. Run the morphology build pass after tree-detail generation.");
        }
        return result;
    }

    private static Mesh LoadBase(string name)
    {
        string path = MeshRoot + "/" + name + ".asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null)
            throw new InvalidOperationException($"Missing base leaf-spray mesh {path}; run high-detail tree generation first.");
        return mesh;
    }

    private static void SaveOrReplaceMesh(string path, Mesh generated)
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(generated, path);
            return;
        }

        generated.name = Path.GetFileNameWithoutExtension(path);
        EditorUtility.CopySerialized(generated, existing);
        existing.name = generated.name;
        EditorUtility.SetDirty(existing);
        UnityEngine.Object.DestroyImmediate(generated);
    }

    public static int VariantIndex(int treeIndex, int ordinal)
    {
        unchecked
        {
            // 7 is coprime to 12, so consecutive ordinals traverse the full library before repeating.
            int value = treeIndex * 5 + ordinal * 7 + (ordinal / VariantCount) * 3;
            value %= VariantCount;
            if (value < 0) value += VariantCount;
            return value;
        }
    }

    private static string VariantName(int variant) => $"GM_LeafSpray_Morph_{variant:D2}";
    private static string VariantAssetPath(int variant) => MeshRoot + "/" + VariantName(variant) + ".asset";

    private static int ParseTreeIndex(string slotId)
    {
        int dot = slotId.LastIndexOf('.');
        if (dot < 0 || !int.TryParse(slotId.Substring(dot + 1), out int value))
            throw new InvalidOperationException($"Invalid tree slot id {slotId}.");
        return value;
    }

    public static int ParseLeafOrdinal(string name)
    {
        const string marker = "LeafCluster_";
        int start = name.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
            throw new InvalidOperationException($"Leaf renderer name lacks {marker}: {name}");
        start += marker.Length;
        int end = start;
        while (end < name.Length && char.IsDigit(name[end])) end++;
        if (end == start || !int.TryParse(name.Substring(start, end - start), out int ordinal))
            throw new InvalidOperationException($"Cannot parse leaf ordinal from {name}.");
        return ordinal;
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

    private static bool FinitePositive(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }

    private static void EnsureBenchmarkScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            throw new InvalidOperationException($"Foliage morphology pass requires active benchmark scene {ScenePath}.");
    }
}
