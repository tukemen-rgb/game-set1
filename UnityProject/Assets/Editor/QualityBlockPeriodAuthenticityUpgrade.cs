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
    private const string ContractPath = "Assets/QA/period_authenticity_contract.json";
    private const string LookdevPath = "Assets/QA/period_rooftop_reception_installation_lookdev.svg";

    private const float MastDiameter = 0.048f;
    private const float MastTop = 3.28f;
    private const float StayDiameter = 0.025f;
    private const float StayCollarHeight = 0.98f;

    private static readonly Vector3[] StayBasePoints =
    {
        new Vector3(-0.54f, 0.1805f, -0.31f),
        new Vector3( 0.54f, 0.1805f, -0.31f),
        new Vector3(-0.54f, 0.1805f,  0.31f),
        new Vector3( 0.54f, 0.1805f,  0.31f),
    };

    private static readonly Vector3[] StayCollarPoints =
    {
        new Vector3(-0.045f, StayCollarHeight, -0.045f),
        new Vector3( 0.045f, StayCollarHeight, -0.045f),
        new Vector3(-0.045f, StayCollarHeight,  0.045f),
        new Vector3( 0.045f, StayCollarHeight,  0.045f),
    };

    [MenuItem("NewTown/Period/Apply Year-2000 Rooftop Authenticity")]
    public static void ApplyToOpenScene()
    {
        ValidateContractConfigOnly();

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
        // so the antenna reads as maintained communal service infrastructure, not a decorative prop.
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

    [MenuItem("NewTown/QA/Validate Year-2000 Period Authenticity Contract")]
    public static void ValidateContractConfigOnly()
    {
        if (!File.Exists(ContractPath))
            throw new InvalidOperationException($"Period-authenticity contract missing: {ContractPath}");
        if (!File.Exists(LookdevPath))
            throw new InvalidOperationException($"Period rooftop installation lookdev missing: {LookdevPath}");

        string json = File.ReadAllText(ContractPath);
        string[] requiredTokens =
        {
            "\"contractVersion\": 2",
            "\"id\": \"communal_rooftop_vhf_uhf_reception\"",
            "\"outsideDiameterMeters\": 0.048",
            "\"heightAboveRoofMeters\": 3.28",
            "\"mastStayCountPerLod\": 4",
            "\"mastStayDiameterMeters\": 0.025",
            "\"mastStayCollarHeightMeters\": 0.98",
            "\"railFootPlateCountLod0And1\": 4",
            "\"loadPathMustPersistAllLods\": true",
            "\"visualFidelityPointsAwardedFromThisContract\": 0",
            "PENDING_NATIVE_UNITY_3840x2160_RENDER_AND_100_PERCENT_CROP"
        };
        foreach (string token in requiredTokens)
            if (json.IndexOf(token, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException($"Period-authenticity contract missing required token: {token}");
    }

    [MenuItem("NewTown/QA/Validate Year-2000 Period Authenticity")]
    public static void ValidateOpenScene()
    {
        ValidateContractConfigOnly();

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
        AssertNear(root.transform.localPosition, new Vector3(-10.35f, 13.20f, -11.25f), 0.01f,
            "period rooftop local installation datum");
        if (Quaternion.Angle(root.transform.localRotation, Quaternion.Euler(0f, 32f, 0f)) > 0.2f)
            throw new InvalidOperationException("Period rooftop assembly azimuth drifted from the documented 32 degree installation.");

        LODGroup group = root.GetComponent<LODGroup>();
        if (group == null) throw new InvalidOperationException("Period rooftop assembly has no LODGroup.");
        LOD[] lods = group.GetLODs();
        if (lods.Length != 4) throw new InvalidOperationException($"Expected four period-authentic LODs, got {lods.Length}.");
        if (group.fadeMode != LODFadeMode.CrossFade || !group.animateCrossFading)
            throw new InvalidOperationException("Period rooftop LOD must use animated cross-fade.");

        int[] expectedVhf = { 8, 6, 4, 2 };
        int[] expectedUhf = { 14, 8, 4, 2 };
        for (int lod = 0; lod < 4; lod++)
        {
            if (CountNamedPrefix($"PA_VHF_Element_LOD{lod}_") != expectedVhf[lod])
                throw new InvalidOperationException($"LOD{lod} VHF element count drifted from {expectedVhf[lod]}.");
            if (CountNamedPrefix($"PA_UHF_Element_LOD{lod}_") != expectedUhf[lod])
                throw new InvalidOperationException($"LOD{lod} UHF element count drifted from {expectedUhf[lod]}.");
            if (CountNamedPrefix($"PA_Ballast_LOD{lod}_") != 4)
                throw new InvalidOperationException($"LOD{lod} must retain four non-penetrating precast ballast blocks.");
            if (CountNamedPrefix($"PA_MastStay_LOD{lod}_") != 4)
                throw new InvalidOperationException($"LOD{lod} must retain all four triangulating mast stays.");
            if (FindSceneObject($"PA_MastStayCollar_LOD{lod}") == null)
                throw new InvalidOperationException($"LOD{lod} mast stay collar is missing.");

            int expectedFootDetails = lod <= 1 ? 4 : 0;
            if (CountNamedPrefix($"PA_MastStayFoot_LOD{lod}_") != expectedFootDetails)
                throw new InvalidOperationException($"LOD{lod} stay-foot plate count must be {expectedFootDetails}.");
            if (CountNamedPrefix($"PA_MastStayLug_LOD{lod}_") != expectedFootDetails)
                throw new InvalidOperationException($"LOD{lod} stay-collar lug count must be {expectedFootDetails}.");

            ValidateStayGeometry(lod);
        }

        if (CountNamedPrefix("PA_CoaxClip_LOD0_") != 6 || CountNamedPrefix("PA_CoaxClip_LOD1_") != 6)
            throw new InvalidOperationException("LOD0/LOD1 coax must retain six explicit mast clips instead of floating alongside the mast.");
        if (CountNamedPrefix("PA_CoaxClip_LOD2_") != 0 || CountNamedPrefix("PA_CoaxClip_LOD3_") != 0)
            throw new InvalidOperationException("LOD2/LOD3 unexpectedly retain sub-pixel coax clip detail.");

        if (root.GetComponentsInChildren<Collider>(true).Length != 0)
            throw new InvalidOperationException("Period-authentic visual service detail must not alter gameplay collision.");

        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
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

        Debug.Log(
            "Year-2000 rooftop reception assembly passed source/scene QA with a four-way triangulated non-penetrating mast load path in every LOD, " +
            "LOD0/1 stay-foot/lug hardware and explicit coax clips. Visual period/geometry/material points still require sealed native 4K pixels.");
    }

    private static void BuildReceptionLod(
        Transform parent, int lod, int vhfCount, int uhfCount,
        Material galvanized, Material blackAbs, Material coaxPvc, Material precast)
    {
        BuildNonPenetratingBase(parent, lod, galvanized, precast);

        AddRod($"PA_Mast_LOD{lod}", parent,
            new Vector3(0f, 0.16f, 0f), new Vector3(0f, MastTop, 0f), MastDiameter, galvanized);

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

            // Explicit UV-resistant clips bridge the cable-to-mast gap. They are geometry at the two
            // near-camera LODs only; distant LODs retain the cable silhouette without sub-pixel blocks.
            for (int clip = 0; clip < 6; clip++)
            {
                float y = 0.52f + clip * 0.43f;
                AddBox($"PA_CoaxClip_LOD{lod}_{clip}", parent,
                    new Vector3(0.018f, y, 0.0125f), new Vector3(0.046f, 0.018f, 0.040f), blackAbs);
            }
        }
    }

    private static void BuildNonPenetratingBase(Transform parent, int lod, Material steel, Material concrete)
    {
        // Four precast ballast blocks carry paired hot-dip-galvanized rails. A short saddle alone is
        // not a credible visible restraint for a 3.28 m mast under wind, so four diagonal stays transfer
        // mast bending load into the ends of both ballast rails. The structural load path persists in
        // every LOD; only small foot/lug hardware is culled beyond LOD1.
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

        // 82 mm OD x 110 mm high galvanized sleeve/collar around the 48 mm mast gives the four stays
        // an explicit upper node rather than letting them terminate in empty space.
        AddRod($"PA_MastStayCollar_LOD{lod}", parent,
            new Vector3(0f, 0.925f, 0f), new Vector3(0f, 1.035f, 0f), 0.082f, steel);

        for (int i = 0; i < StayBasePoints.Length; i++)
        {
            if (lod <= 1)
            {
                AddBox($"PA_MastStayFoot_LOD{lod}_{i}", parent,
                    new Vector3(StayBasePoints[i].x, 0.1715f, StayBasePoints[i].z),
                    new Vector3(0.13f, 0.018f, 0.10f), steel);
                AddBox($"PA_MastStayLug_LOD{lod}_{i}", parent,
                    StayCollarPoints[i], new Vector3(0.055f, 0.050f, 0.055f), steel);
            }

            AddRod($"PA_MastStay_LOD{lod}_{i}", parent,
                StayBasePoints[i], StayCollarPoints[i], StayDiameter, steel);
        }
    }

    private static void ValidateStayGeometry(int lod)
    {
        for (int i = 0; i < StayBasePoints.Length; i++)
        {
            GameObject stay = FindSceneObject($"PA_MastStay_LOD{lod}_{i}");
            if (stay == null)
                throw new InvalidOperationException($"Missing LOD{lod} mast stay {i}.");
            if (stay.transform.parent == null || stay.transform.parent.name != $"PA_RooftopReception_LOD{lod}")
                throw new InvalidOperationException($"LOD{lod} mast stay {i} is outside its authored LOD root.");

            Vector3 expectedMid = (StayBasePoints[i] + StayCollarPoints[i]) * 0.5f;
            AssertNear(stay.transform.localPosition, expectedMid, 0.003f, $"LOD{lod} mast stay {i} midpoint");

            Vector3 expectedAxis = (StayCollarPoints[i] - StayBasePoints[i]).normalized;
            Vector3 actualAxis = (stay.transform.localRotation * Vector3.up).normalized;
            if (Vector3.Dot(expectedAxis, actualAxis) < 0.9995f)
                throw new InvalidOperationException($"LOD{lod} mast stay {i} no longer follows its rail-foot to collar load path.");

            Renderer renderer = stay.GetComponent<Renderer>();
            if (renderer == null || renderer.sharedMaterial == null || renderer.sharedMaterial.name != "MAT_Period_HotDipGalvanizedSteel")
                throw new InvalidOperationException($"LOD{lod} mast stay {i} lost its galvanized steel material.");
        }
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
        material.DisableKeyword("_EMISSION");
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ValidateMaterial(string name, float minMetallic, float maxMetallic, float minSmooth, float maxSmooth)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialRoot}/{name}.mat");
        if (material == null) throw new InvalidOperationException($"Missing period material {name}.");
        if (material.shader == null || material.shader.name != "Standard")
            throw new InvalidOperationException($"Period material {name} must use Standard PBR in this benchmark pipeline.");
        float metallic = material.GetFloat("_Metallic");
        float smooth = material.GetFloat("_Glossiness");
        if (metallic < minMetallic || metallic > maxMetallic)
            throw new InvalidOperationException($"Material {name} metallic {metallic:F3} is outside [{minMetallic:F2},{maxMetallic:F2}].");
        if (smooth < minSmooth || smooth > maxSmooth)
            throw new InvalidOperationException($"Material {name} smoothness {smooth:F3} is outside [{minSmooth:F2},{maxSmooth:F2}].");
        if (material.IsKeywordEnabled("_EMISSION") ||
            (material.HasProperty("_EmissionColor") && material.GetColor("_EmissionColor").maxColorComponent > 0.0001f))
            throw new InvalidOperationException($"Period material {name} may not fake highlights with emission.");
    }

    private static void AssertNear(Vector3 actual, Vector3 expected, float tolerance, string label)
    {
        if ((actual - expected).magnitude > tolerance)
            throw new InvalidOperationException($"{label} drifted: expected {expected}, got {actual}.");
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
