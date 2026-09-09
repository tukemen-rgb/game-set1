using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Period-authentic exterior silhouette pass for the generated year-2000 Japanese new-town benchmark.
/// The base block already carries repetitive mass-produced housing hardware. This pass adds one
/// communal analogue-era rooftop receiving assembly whose manufacture and installation can be
/// reconstructed physically, rather than scattering arbitrary nostalgic props around the scene.
///
/// Visual intent: the VHF/UHF Yagi silhouette should read at the roofline in frontal/oblique 4K
/// frames, while the close construction remains credible under a 100% crop. No Unity primitive
/// renderer is used; all visible pieces use the authored detail-mesh cache.
/// </summary>
public static class QualityBlockPeriodAuthenticityUpgrade
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string RootName = "PeriodAuthenticity2000";
    private const string MaterialRoot = "Assets/Art/GeneratedPeriodMaterials";

    [MenuItem("NewTown/Period/Apply Year-2000 Rooftop Authenticity")]
    public static void ApplyToOpenScene()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var slot = Resources.FindObjectsOfTypeAll<QualityBlockArtSlot>()
            .FirstOrDefault(x => x.gameObject.scene == scene && x.SlotId == "danchi.main");
        if (slot != null && slot.IsUsingAuthoredArt)
        {
            Debug.Log("Period-authentic generated rooftop assembly skipped because authored danchi art is active.");
            return;
        }

        GameObject danchi = FindSceneObject("Danchi");
        GameObject detail = FindSceneObject("DanchiHighDetail");
        if (danchi == null || detail == null)
            throw new InvalidOperationException("Period pass requires Danchi and DanchiHighDetail before application.");

        GameObject old = FindSceneObject(RootName);
        if (old != null) UnityEngine.Object.DestroyImmediate(old);

        Directory.CreateDirectory(MaterialRoot);
        Material galvanized = GetOrCreateMaterial(
            "MAT_Period_HotDipGalvanizedSteel",
            new Color(0.49f, 0.51f, 0.50f), metallic: 1.0f, smoothness: 0.30f);
        Material blackAbs = GetOrCreateMaterial(
            "MAT_Period_UVAgedBlackABS",
            new Color(0.075f, 0.078f, 0.075f), metallic: 0.0f, smoothness: 0.18f);
        Material coaxPvc = GetOrCreateMaterial(
            "MAT_Period_BlackCoaxPVC",
            new Color(0.045f, 0.047f, 0.044f), metallic: 0.0f, smoothness: 0.15f);
        Material precast = GetOrCreateMaterial(
            "MAT_Period_PrecastBallastConcrete",
            new Color(0.49f, 0.49f, 0.46f), metallic: 0.0f, smoothness: 0.10f);

        var root = new GameObject(RootName);
        root.transform.SetParent(danchi.transform, false);
        // Flat-roof benchmark slab top is y=13.20 m. The mount is kept away from the parapet edge
        // so the antenna reads as a maintained communal service assembly, not a decorative prop.
        root.transform.localPosition = new Vector3(-10.35f, 13.20f, -11.25f);
        root.transform.localRotation = Quaternion.Euler(0f, 32f, 0f);

        int[] vhfCounts = { 8, 6, 4, 2 };
        int[] uhfCounts = { 14, 8, 4, 2 };
        var lodRenderers = new Renderer[4][];

        for (int lod = 0; lod < 4; lod++)
        {
            var lodRoot = new GameObject($"PA_RooftopReception_LOD{lod}");
            lodRoot.transform.SetParent(root.transform, false);
            BuildReceptionLod(lodRoot.transform, lod, vhfCounts[lod], uhfCounts[lod],
                galvanized, blackAbs, coaxPvc, precast);
            lodRenderers[lod] = lodRoot.GetComponentsInChildren<Renderer>(true);
        }

        var group = root.AddComponent<LODGroup>();
        group.fadeMode = LODFadeMode.CrossFade;
        group.animateCrossFading = true;
        group.SetLODs(new[]
        {
            new LOD(0.38f, lodRenderers[0]),
            new LOD(0.20f, lodRenderers[1]),
            new LOD(0.095f, lodRenderers[2]),
            new LOD(0.025f, lodRenderers[3]),
        });
        group.RecalculateBounds();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorSceneManager.MarkSceneDirty(scene);
    }

    [MenuItem("NewTown/QA/Validate Year-2000 Period Authenticity")]
    public static void ValidateOpenScene()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        var slot = Resources.FindObjectsOfTypeAll<QualityBlockArtSlot>()
            .FirstOrDefault(x => x.gameObject.scene == scene && x.SlotId == "danchi.main");
        if (slot != null && slot.IsUsingAuthoredArt)
        {
            Debug.Log("Period-authentic generated rooftop QA skipped because authored danchi art is active.");
            return;
        }

        GameObject root = FindSceneObject(RootName);
        if (root == null) throw new InvalidOperationException("PeriodAuthenticity2000 root is missing.");
        if (root.transform.parent == null || root.transform.parent.name != "Danchi")
            throw new InvalidOperationException("Period rooftop assembly must remain parented to Danchi.");
        if (Mathf.Abs(root.transform.localPosition.y - 13.20f) > 0.02f)
            throw new InvalidOperationException("Period rooftop assembly no longer sits on the documented roof datum.");

        LODGroup group = root.GetComponent<LODGroup>();
        if (group == null) throw new InvalidOperationException("Period rooftop assembly has no LODGroup.");
        LOD[] lods = group.GetLODs();
        if (lods.Length != 4) throw new InvalidOperationException($"Expected four period-authentic LODs, got {lods.Length}.");
        if (group.fadeMode != LODFadeMode.CrossFade || !group.animateCrossFading)
            throw new InvalidOperationException("Period rooftop LOD must use animated cross-fade.");

        int vhf0 = CountNamedPrefix("PA_VHF_Element_LOD0_");
        int uhf0 = CountNamedPrefix("PA_UHF_Element_LOD0_");
        if (vhf0 != 8) throw new InvalidOperationException($"LOD0 VHF element count must be 8, got {vhf0}.");
        if (uhf0 != 14) throw new InvalidOperationException($"LOD0 UHF element count must be 14, got {uhf0}.");
        if (CountNamedPrefix("PA_Ballast_LOD0_") != 4)
            throw new InvalidOperationException("LOD0 must retain four non-penetrating precast ballast blocks.");

        if (root.GetComponentsInChildren<Collider>(true).Length != 0)
            throw new InvalidOperationException("Period-authentic visual service detail must not alter gameplay collision.");

        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null)
                throw new InvalidOperationException($"Missing mesh on period component {filter.name}.");
            if (!filter.sharedMesh.name.StartsWith("GM_HD_", StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Period component {filter.name} is not using the authored detail-mesh cache: {filter.sharedMesh.name}.");
        }

        ValidateMaterial("MAT_Period_HotDipGalvanizedSteel", minMetallic: 0.95f, maxMetallic: 1.0f, minSmooth: 0.24f, maxSmooth: 0.38f);
        ValidateMaterial("MAT_Period_UVAgedBlackABS", minMetallic: 0f, maxMetallic: 0.05f, minSmooth: 0.10f, maxSmooth: 0.28f);
        ValidateMaterial("MAT_Period_BlackCoaxPVC", minMetallic: 0f, maxMetallic: 0.05f, minSmooth: 0.08f, maxSmooth: 0.24f);
        ValidateMaterial("MAT_Period_PrecastBallastConcrete", minMetallic: 0f, maxMetallic: 0.05f, minSmooth: 0.04f, maxSmooth: 0.18f);

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.sharedMaterial == null)
                throw new InvalidOperationException($"Period renderer {renderer.name} has no material.");
            if (renderer.shadowCastingMode == ShadowCastingMode.Off || !renderer.receiveShadows)
                throw new InvalidOperationException($"Period renderer {renderer.name} must participate in coherent sun/shadow response.");
        }

        Debug.Log("Year-2000 period-authentic rooftop assembly passed source/scene QA. Visual period score still requires native 4K render evidence.");
    }

    private static void BuildReceptionLod(
        Transform parent, int lod, int vhfCount, int uhfCount,
        Material galvanized, Material blackAbs, Material coaxPvc, Material precast)
    {
        BuildNonPenetratingBase(parent, lod, galvanized, precast);

        AddRod($"PA_Mast_LOD{lod}", parent,
            new Vector3(0f, 0.16f, 0f), new Vector3(0f, 3.28f, 0f), 0.048f, galvanized);

        // Communal analogue-era VHF and UHF receiving arrays. VHF remains deliberately larger;
        // the compact UHF array is offset vertically so the silhouette cannot collapse into one
        // ambiguous decorative comb at the benchmark roofline.
        AddRod($"PA_VHF_Boom_LOD{lod}", parent,
            new Vector3(0f, 2.93f, -1.22f), new Vector3(0f, 2.93f, 1.22f), 0.026f, galvanized);
        BuildYagiElements(parent, "PA_VHF_Element", lod, vhfCount,
            y: 2.93f, zMin: -1.10f, zMax: 1.10f, maxLength: 1.70f, minLength: 1.08f,
            diameter: 0.018f, galvanized);

        AddRod($"PA_UHF_Boom_LOD{lod}", parent,
            new Vector3(0f, 2.34f, -0.84f), new Vector3(0f, 2.34f, 0.84f), 0.020f, galvanized);
        BuildYagiElements(parent, "PA_UHF_Element", lod, uhfCount,
            y: 2.34f, zMin: -0.75f, zMax: 0.75f, maxLength: 0.54f, minLength: 0.34f,
            diameter: 0.012f, galvanized);

        if (lod <= 1)
        {
            AddBox($"PA_VHF_FeedBox_LOD{lod}", parent,
                new Vector3(0.08f, 2.93f, -0.12f), new Vector3(0.16f, 0.10f, 0.085f), blackAbs);
            AddBox($"PA_UHF_FeedBox_LOD{lod}", parent,
                new Vector3(0.07f, 2.34f, -0.06f), new Vector3(0.13f, 0.085f, 0.075f), blackAbs);

            AddRod($"PA_CoaxDrop_LOD{lod}", parent,
                new Vector3(0.036f, 0.24f, 0.025f), new Vector3(0.036f, 2.87f, 0.025f), 0.012f, coaxPvc);
            AddRod($"PA_RoofCoaxConduit_LOD{lod}", parent,
                new Vector3(0.04f, 0.17f, 0.03f), new Vector3(1.16f, 0.17f, 0.03f), 0.018f, coaxPvc);
            AddBox($"PA_RoofJunctionBox_LOD{lod}", parent,
                new Vector3(1.25f, 0.20f, 0.03f), new Vector3(0.18f, 0.22f, 0.10f), blackAbs);
        }
    }

    private static void BuildNonPenetratingBase(Transform parent, int lod, Material steel, Material concrete)
    {
        // Four precast ballast blocks carry two hot-dip galvanized cross rails. This avoids an
        // unexplained roof-membrane penetration and gives the mast a visible load path into the slab.
        Vector3[] positions =
        {
            new Vector3(-0.43f, 0.06f, -0.43f), new Vector3(0.43f, 0.06f, -0.43f),
            new Vector3(-0.43f, 0.06f, 0.43f),  new Vector3(0.43f, 0.06f, 0.43f),
        };
        for (int i = 0; i < positions.Length; i++)
            AddBox($"PA_Ballast_LOD{lod}_{i}", parent, positions[i], new Vector3(0.42f, 0.12f, 0.42f), concrete);

        AddBox($"PA_BaseRailA_LOD{lod}", parent, new Vector3(0f, 0.135f, -0.31f), new Vector3(1.22f, 0.055f, 0.065f), steel);
        AddBox($"PA_BaseRailB_LOD{lod}", parent, new Vector3(0f, 0.135f, 0.31f), new Vector3(1.22f, 0.055f, 0.065f), steel);
        AddBox($"PA_MastSaddle_LOD{lod}", parent, new Vector3(0f, 0.19f, 0f), new Vector3(0.18f, 0.11f, 0.16f), steel);
    }

    private static void BuildYagiElements(
        Transform parent, string prefix, int lod, int count,
        float y, float zMin, float zMax, float maxLength, float minLength,
        float diameter, Material material)
    {
        for (int i = 0; i < count; i++)
        {
            float t = count <= 1 ? 0f : i / (float)(count - 1);
            float z = Mathf.Lerp(zMin, zMax, t);
            float length = Mathf.Lerp(maxLength, minLength, t);
            AddRod($"{prefix}_LOD{lod}_{i}", parent,
                new Vector3(-length * 0.5f, y, z), new Vector3(length * 0.5f, y, z), diameter, material);
        }
    }

    private static GameObject AddBox(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = QualityBlockDetailMeshLibrary.GetChamferedBox(size);
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        return go;
    }

    private static GameObject AddRod(string name, Transform parent, Vector3 localA, Vector3 localB, float diameter, Material material)
    {
        Vector3 delta = localB - localA;
        float length = delta.magnitude;
        if (length < 0.001f) throw new ArgumentException($"Rod {name} has negligible length.");

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = (localA + localB) * 0.5f;
        go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = QualityBlockDetailMeshLibrary.GetBeveledCylinder(
            new Vector3(diameter, length * 0.5f, diameter), fastener: false);
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        return go;
    }

    private static Material GetOrCreateMaterial(string name, Color color, float metallic, float smoothness)
    {
        string path = $"{MaterialRoot}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("Standard");
        if (shader == null) throw new InvalidOperationException("Unity Standard shader unavailable for period-authentic material creation.");

        if (material == null)
        {
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        material.color = color;
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Glossiness", smoothness);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ValidateMaterial(string name, float minMetallic, float maxMetallic, float minSmooth, float maxSmooth)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialRoot}/{name}.mat");
        if (material == null) throw new InvalidOperationException($"Missing period material {name}.");
        float metallic = material.GetFloat("_Metallic");
        float smooth = material.GetFloat("_Glossiness");
        if (metallic < minMetallic || metallic > maxMetallic)
            throw new InvalidOperationException($"Material {name} metallic {metallic:F3} is outside [{minMetallic:F2},{maxMetallic:F2}].");
        if (smooth < minSmooth || smooth > maxSmooth)
            throw new InvalidOperationException($"Material {name} smoothness {smooth:F3} is outside [{minSmooth:F2},{maxSmooth:F2}].");
    }

    private static int CountNamedPrefix(string prefix)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .Count(x => x.scene.IsValid() && x.name.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static GameObject FindSceneObject(string name)
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x.scene == scene && x.name == name);
    }
}
