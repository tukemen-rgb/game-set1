using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Converts the generated benchmark scene into stable art replacement slots.
/// Artists can drop a prefab/FBX (or a GLB when a glTF importer is installed) into
/// Assets/Art/ReplacementPrefabs using the documented names; the generated geometry
/// remains the fallback until that asset exists.
/// </summary>
public static class QualityBlockArtReplacement
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ReplacementRoot = "Assets/Art/ReplacementPrefabs";

    private static readonly Vector3[] TreeAnchors =
    {
        new(-20f, 0f, -1.5f), new(-15f, 0f, 5.5f), new(-3.5f, 0f, 7.5f),
        new(3.5f, 0f, -2f), new(15.5f, 0f, 2.2f), new(19f, 0f, 9.5f)
    };

    [MenuItem("NewTown/Art/Build Replacement-Ready Quality Block")]
    public static void BuildReplacementReadyQualityBlock()
    {
        QualityBlockMeshUpgrade.BuildMeshQualityBlock();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        BuildSlotsOnOpenScene();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("Replacement-ready quality block rebuilt. Authored art is optional; generated fallback remains visible until a matching asset exists.");
    }

    public static void BuildSlotsOnOpenScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() ||
            EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        if (GameObject.Find("ArtSlots") != null)
            throw new InvalidOperationException("ArtSlots already exist. Rebuild through NewTown > Art > Build Replacement-Ready Quality Block to refresh cleanly.");

        Directory.CreateDirectory(ReplacementRoot);
        AssetDatabase.Refresh();

        var sceneRoot = GameObject.Find("QualityBlock1990s");
        if (sceneRoot == null)
            throw new InvalidOperationException("QualityBlock1990s root not found.");

        var artRoot = new GameObject("ArtSlots");
        artRoot.transform.SetParent(sceneRoot.transform, false);

        BuildDanchiSlot(artRoot.transform);
        BuildSlideSlot(artRoot.transform);
        BuildTreeSlots(artRoot.transform);
    }

    private static void BuildDanchiSlot(Transform artRoot)
    {
        var fallback = GameObject.Find("Danchi");
        if (fallback == null) throw new InvalidOperationException("Danchi fallback root not found.");

        var slot = CreateSlot("ARTSLOT_Danchi", artRoot, new Vector3(-8f, 0f, -11.5f),
            "danchi.main", "PF_Danchi_A", fallback);
        fallback.transform.SetParent(slot.transform, true);
        AttachReplacementIfAvailable(slot, "PF_Danchi_A");
        ConfigureSingleRepresentationCull(slot, 0.008f);
    }

    private static void BuildSlideSlot(Transform artRoot)
    {
        var slotGo = new GameObject("ARTSLOT_Slide");
        slotGo.transform.SetParent(artRoot, false);
        slotGo.transform.position = new Vector3(12.6f, 0f, -5.2f);

        var fallbackGroup = new GameObject("Fallback_Slide");
        fallbackGroup.transform.SetParent(slotGo.transform, false);

        var slideParts = Resources.FindObjectsOfTypeAll<GameObject>()
            .Where(x => x.scene.IsValid() && x.name.StartsWith("Slide", StringComparison.Ordinal))
            .Where(x => x != slotGo && x != fallbackGroup)
            .ToArray();
        if (slideParts.Length < 6)
            throw new InvalidOperationException($"Expected upgraded slide parts before slotting, got {slideParts.Length}.");

        foreach (var part in slideParts)
            part.transform.SetParent(fallbackGroup.transform, true);

        var slot = slotGo.AddComponent<QualityBlockArtSlot>();
        slot.Configure("park.slide", "PF_Slide_A", fallbackGroup);
        AttachReplacementIfAvailable(slot, "PF_Slide_A");
        ConfigureSingleRepresentationCull(slot, 0.02f);
    }

    private static void BuildTreeSlots(Transform artRoot)
    {
        for (int i = 0; i < TreeAnchors.Length; i++)
        {
            string expected = $"PF_Tree_{(char)('A' + (i % 3))}";
            var slotGo = new GameObject($"ARTSLOT_Tree_{i}");
            slotGo.transform.SetParent(artRoot, false);
            slotGo.transform.position = TreeAnchors[i];

            var fallbackGroup = new GameObject($"Fallback_Tree_{i}");
            fallbackGroup.transform.SetParent(slotGo.transform, false);

            var treeParts = Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(x => x.scene.IsValid())
                .Where(x => x.name == $"Trunk_{i}" || x.name.StartsWith($"Crown_{i}_", StringComparison.Ordinal))
                .ToArray();
            if (treeParts.Length != 5)
                throw new InvalidOperationException($"Tree {i} expected one trunk and four crowns, got {treeParts.Length} parts.");

            foreach (var part in treeParts)
                part.transform.SetParent(fallbackGroup.transform, true);

            var slot = slotGo.AddComponent<QualityBlockArtSlot>();
            slot.Configure($"vegetation.tree.{i}", expected, fallbackGroup);
            AttachReplacementIfAvailable(slot, expected);
            ConfigureTreeLods(slot);
        }
    }

    private static QualityBlockArtSlot CreateSlot(
        string name, Transform parent, Vector3 worldAnchor, string slotId, string expectedAsset, GameObject fallback)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = worldAnchor;
        var slot = go.AddComponent<QualityBlockArtSlot>();
        slot.Configure(slotId, expectedAsset, fallback);
        return slot;
    }

    private static void AttachReplacementIfAvailable(QualityBlockArtSlot slot, string assetName)
    {
        var asset = FindReplacementAsset(assetName);
        if (asset == null) return;

        var instance = PrefabUtility.InstantiatePrefab(asset, slot.transform) as GameObject;
        if (instance == null)
            throw new InvalidOperationException($"Could not instantiate authored art asset {assetName}.");

        instance.name = assetName + "_Authored";
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        slot.SetAuthoredInstance(instance);
    }

    private static GameObject FindReplacementAsset(string assetName)
    {
        string[] extensions = { ".prefab", ".fbx", ".glb" };
        foreach (string extension in extensions)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{ReplacementRoot}/{assetName}{extension}");
            if (asset != null) return asset;
        }

        string[] guids = AssetDatabase.FindAssets($"{assetName} t:GameObject", new[] { ReplacementRoot });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.Equals(Path.GetFileNameWithoutExtension(path), assetName, StringComparison.OrdinalIgnoreCase))
                continue;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset != null) return asset;
        }
        return null;
    }

    private static void ConfigureTreeLods(QualityBlockArtSlot slot)
    {
        if (slot.AuthoredInstance != null)
        {
            if (slot.AuthoredInstance.GetComponentInChildren<LODGroup>(true) == null)
                ConfigureSingleRepresentationCull(slot, 0.025f);
            return;
        }

        var fallback = slot.FallbackRoot;
        var trunk = fallback.GetComponentsInChildren<Renderer>(true)
            .FirstOrDefault(x => x.gameObject.name.StartsWith("Trunk_", StringComparison.Ordinal));
        var crowns = fallback.GetComponentsInChildren<Renderer>(true)
            .Where(x => x.gameObject.name.StartsWith("Crown_", StringComparison.Ordinal))
            .OrderBy(x => x.gameObject.name)
            .ToArray();
        if (trunk == null || crowns.Length != 4)
            throw new InvalidOperationException($"Cannot configure tree LODs for {slot.name}: fallback renderers incomplete.");

        var group = slot.gameObject.AddComponent<LODGroup>();
        var lod0 = new List<Renderer> { trunk };
        lod0.AddRange(crowns);
        var lod1 = new[] { trunk, crowns[0], crowns[2] };
        var lod2 = new[] { trunk, crowns[1] };
        group.SetLODs(new[]
        {
            new LOD(0.50f, lod0.ToArray()),
            new LOD(0.20f, lod1),
            new LOD(0.065f, lod2)
        });
        group.fadeMode = LODFadeMode.CrossFade;
        group.animateCrossFading = true;
        group.RecalculateBounds();
    }

    private static void ConfigureSingleRepresentationCull(QualityBlockArtSlot slot, float cullHeight)
    {
        if (slot.AuthoredInstance != null && slot.AuthoredInstance.GetComponentInChildren<LODGroup>(true) != null)
            return;

        var source = slot.AuthoredInstance != null ? slot.AuthoredInstance : slot.FallbackRoot;
        var renderers = source != null ? source.GetComponentsInChildren<Renderer>(true) : Array.Empty<Renderer>();
        if (renderers.Length == 0) return;

        var existing = slot.GetComponent<LODGroup>();
        if (existing != null) UnityEngine.Object.DestroyImmediate(existing);
        var group = slot.gameObject.AddComponent<LODGroup>();
        group.SetLODs(new[] { new LOD(cullHeight, renderers) });
        group.RecalculateBounds();
    }
}
