using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Reconstructs physical grade/contact interfaces that otherwise read as hard texture cutouts at 4K.
/// This pass intentionally does not paint AO or a dark border into albedo. Contact is created by real
/// plinth projection, recessed mineral joints and thin, irregular grade shoulders at actual material
/// boundaries. Generated visual geometry remains collider-free and subordinate to authored art slots.
/// </summary>
public static class QualityBlockGroundContactInterfaceUpgrade
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/ground_contact_interface_contract.json";
    private const string RootName = "GroundContactInterfaces";
    private const string AssetRoot = "Assets/Art/GeneratedGroundInterfaces";
    private const string MeshRoot = AssetRoot + "/Meshes";
    private const string MaterialRoot = AssetRoot + "/Materials";

    [MenuItem("NewTown/Geometry/Build Physical Ground Contact Interfaces")]
    public static void BuildAndApply()
    {
        QualityBlockGroundDetailUpgrade.BuildDetailedGround();
        QualityBlockGroundMicrodetailUpgrade.BuildAndApply();
        QualityBlockGroundBaseSurfaceUpgrade.BuildAndApply();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        ApplyToOpenScene();
        ValidateOpenScene();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Physical ground contact interfaces built. Visual Fidelity remains unscored until native Unity 4K evidence is reviewed.");
    }

    [MenuItem("NewTown/Geometry/Apply Physical Ground Contact Interfaces Only")]
    public static void ApplyToOpenScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        ValidateContract();
        Directory.CreateDirectory(AssetRoot);
        Directory.CreateDirectory(MeshRoot);
        Directory.CreateDirectory(MaterialRoot);

        GameObject groundArt = FindSceneObject("GroundHighDetail_Art");
        if (groundArt == null)
            throw new InvalidOperationException("GroundHighDetail_Art is required before contact-interface construction.");

        GameObject old = FindSceneObject(RootName);
        if (old != null)
            UnityEngine.Object.DestroyImmediate(old);

        var root = new GameObject(RootName);
        root.transform.SetParent(groundArt.transform, false);

        Material plinth = GetOrCreateMaterial(
            "MAT_FoundationPlinthMortar", new Color(0.42f, 0.41f, 0.38f), 0.12f, 0f);
        Material soil = GetOrCreateMaterial(
            "MAT_InterfaceCompactedSoil", new Color(0.28f, 0.205f, 0.125f), 0.055f, 0f);
        Material joint = GetOrCreateMaterial(
            "MAT_InterfaceRecessJoint", new Color(0.12f, 0.115f, 0.105f), 0.035f, 0f);

        int plinthParts = BuildDanchiFoundationInterface(root.transform, plinth, soil, joint);
        int hardscapeShoulders = BuildHardscapeEdgeShoulders(root.transform, soil);
        int erodedShoulders = BuildWornPathBEdge(root.transform, soil);

        var manifest = root.AddComponent<QualityBlockGroundContactManifest>();
        manifest.Configure(plinthParts, hardscapeShoulders, erodedShoulders);
        EditorUtility.SetDirty(manifest);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    [MenuItem("NewTown/QA/Validate Physical Ground Contact Interfaces")]
    public static void ValidateOpenScene()
    {
        ValidateContract();
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject root = FindSceneObject(RootName);
        if (root == null)
            throw new InvalidOperationException("GroundContactInterfaces root is missing.");

        var manifest = root.GetComponent<QualityBlockGroundContactManifest>();
        if (manifest == null)
            throw new InvalidOperationException("Ground contact manifest is missing.");
        if (manifest.PlinthPartCount < 8)
            throw new InvalidOperationException($"Foundation/plinth contact detail unexpectedly low: {manifest.PlinthPartCount} parts.");
        if (manifest.HardscapeShoulderCount < 3)
            throw new InvalidOperationException($"Expected at least three hardscape/grass interface shoulders, got {manifest.HardscapeShoulderCount}.");
        if (manifest.ErodedShoulderCount != 2)
            throw new InvalidOperationException($"Expected two organic WornPathB edge shoulders, got {manifest.ErodedShoulderCount}.");

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        if (colliders.Length != 0)
            throw new InvalidOperationException($"Ground contact visual detail must be collider-separated; found {colliders.Length} colliders.");

        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
        if (filters.Length < 13)
            throw new InvalidOperationException($"Ground contact geometry unexpectedly sparse: {filters.Length} mesh parts.");
        foreach (MeshFilter filter in filters)
        {
            if (filter.sharedMesh == null)
                throw new InvalidOperationException($"Ground contact mesh missing on {filter.name}.");
            bool authored = filter.sharedMesh.name.StartsWith("GM_HD_", StringComparison.Ordinal) ||
                            filter.sharedMesh.name.StartsWith("GM_HD_Interface", StringComparison.Ordinal);
            if (!authored)
                throw new InvalidOperationException($"Ground contact part {filter.name} uses non-authored mesh {filter.sharedMesh.name}.");
        }

        MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        foreach (MeshRenderer renderer in renderers)
        {
            Material mat = renderer.sharedMaterial;
            if (mat == null)
                throw new InvalidOperationException($"Ground contact renderer {renderer.name} has no material.");
            if (mat.HasProperty("_Metallic") && mat.GetFloat("_Metallic") > 0.01f)
                throw new InvalidOperationException($"Impossible metallic mineral/soil interface material on {renderer.name}: {mat.name}.");
            renderer.receiveShadows = true;
        }

        int weatheringCount = root.GetComponentsInChildren<QualityBlockWeatheringSurface>(true).Length;
        if (weatheringCount < 10)
            throw new InvalidOperationException($"Ground contact interfaces lack cause-based exposure metadata: {weatheringCount} surfaces.");

        Transform informalA = root.transform.Find("WornPathB_EdgeWest");
        Transform informalB = root.transform.Find("WornPathB_EdgeEast");
        if (informalA == null || informalB == null)
            throw new InvalidOperationException("Organic WornPathB interface ribbons are missing.");
        if (informalA.GetComponent<MeshFilter>()?.sharedMesh.vertexCount < 40 || informalB.GetComponent<MeshFilter>()?.sharedMesh.vertexCount < 40)
            throw new InvalidOperationException("WornPathB interface ribbons are too simple to break a rectangular edge at close range.");

        Debug.Log(
            $"Ground contact validation passed structurally: plinth={manifest.PlinthPartCount}, " +
            $"hardscape shoulders={manifest.HardscapeShoulderCount}, eroded shoulders={manifest.ErodedShoulderCount}. " +
            "Actual contact shadow, edge breakup and 4K grounding remain render-unverified.");
    }

    private static int BuildDanchiFoundationInterface(Transform parent, Material plinth, Material soil, Material joint)
    {
        var root = NewRoot("DanchiFoundationGradeInterface", parent);
        int count = 0;

        // Main RC block front face is z=-7.30 at grade. The protective mortar toe projects toward
        // the camera/positive-Z side by 0.14 m, producing a real contact ledge instead of fake AO.
        count += AddBox("FoundationPlinth_Main", root.transform,
            new Vector3(-8.0f, 0.09f, -7.23f), new Vector3(25.96f, 0.18f, 0.14f), plinth) != null ? 1 : 0;

        // Stair tower reaches farther forward. Front toe and side returns physically close the joint.
        count += AddBox("FoundationPlinth_StairFront", root.transform,
            new Vector3(-8.0f, 0.085f, -5.27f), new Vector3(3.18f, 0.17f, 0.16f), plinth) != null ? 1 : 0;
        count += AddBox("FoundationPlinth_StairReturnW", root.transform,
            new Vector3(-9.57f, 0.085f, -6.31f), new Vector3(0.12f, 0.17f, 1.92f), plinth) != null ? 1 : 0;
        count += AddBox("FoundationPlinth_StairReturnE", root.transform,
            new Vector3(-6.43f, 0.085f, -6.31f), new Vector3(0.12f, 0.17f, 1.92f), plinth) != null ? 1 : 0;

        // Real recessed isolation lines at the stair-tower returns. Geometry—not black albedo—makes
        // the contact read in grazing light.
        AddBox("FoundationJoint_StairW", root.transform,
            new Vector3(-9.635f, 0.078f, -6.31f), new Vector3(0.012f, 0.145f, 1.88f), joint);
        AddBox("FoundationJoint_StairE", root.transform,
            new Vector3(-6.365f, 0.078f, -6.31f), new Vector3(0.012f, 0.145f, 1.88f), joint);
        count += 2;

        // Narrow grade shoulders keep the mineral/grass transition slightly below the toe. They are
        // split so the stair projection does not create impossible overlapping surfaces.
        AddBox("FoundationShoulder_MainLeft", root.transform,
            new Vector3(-15.20f, 0.008f, -7.08f), new Vector3(11.45f, 0.016f, 0.18f), soil);
        AddBox("FoundationShoulder_MainRight", root.transform,
            new Vector3(-0.80f, 0.008f, -7.08f), new Vector3(11.45f, 0.016f, 0.18f), soil);
        count += 2;

        foreach (Transform child in root.transform)
        {
            bool isJoint = child.name.StartsWith("FoundationJoint_", StringComparison.Ordinal);
            ConfigureWeathering(
                child.gameObject,
                NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.GroundContact |
                (isJoint ? NewTownSurfaceExposure.Recessed : NewTownSurfaceExposure.SunExposed),
                isJoint
                    ? NewTownStainSource.RecessGrime | NewTownStainSource.GroundSplash
                    : NewTownStainSource.GroundSplash | NewTownStainSource.UVExposure,
                0.92f, isJoint ? 0.18f : 0.62f, 0.88f, 0f);
        }

        return count;
    }

    private static int BuildHardscapeEdgeShoulders(Transform parent, Material soil)
    {
        int count = 0;
        var root = NewRoot("HardscapeVegetationInterfaceShoulders", parent);

        // Outside the front apartment-plaza curb (grass side).
        AddRibbon("PlazaFront_GrassShoulder", root.transform,
            new Vector3(-19.95f, 0.006f, 7.70f), new Vector3(4.00f, 0.006f, 7.70f),
            40, 0.052f, 0.030f, 0.014f, 4103, soil);
        count++;

        // Outside both park-path curb runs; ribbon is intentionally below curb top and follows the
        // vegetation side, breaking the razor-straight grass-to-concrete texture transition.
        AddRibbon("ParkPathWest_GrassShoulder", root.transform,
            new Vector3(5.90f, 0.006f, -10.85f), new Vector3(5.90f, 0.006f, 10.85f),
            36, 0.050f, 0.028f, 0.012f, 4211, soil);
        AddRibbon("ParkPathEast_GrassShoulder", root.transform,
            new Vector3(11.50f, 0.006f, -10.85f), new Vector3(11.50f, 0.006f, 10.85f),
            36, 0.050f, 0.028f, 0.012f, 4327, soil);
        count += 2;

        foreach (Transform child in root.transform)
            ConfigureWeathering(
                child.gameObject,
                NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.SunExposed |
                NewTownSurfaceExposure.GroundContact | NewTownSurfaceExposure.UpwardFacing,
                NewTownStainSource.GroundSplash | NewTownStainSource.DrainRunoff | NewTownStainSource.UVExposure,
                1f, 0.78f, 0.64f, 0f);

        return count;
    }

    private static int BuildWornPathBEdge(Transform parent, Material soil)
    {
        // WornPathB is an informal desire path centered at x=13, width=2 m, z=4.5..10.5. The edge
        // should be an eroded vegetation/soil transition, not another manufactured curb.
        AddRibbon("WornPathB_EdgeWest", parent,
            new Vector3(11.98f, 0.010f, 4.42f), new Vector3(11.98f, 0.010f, 10.58f),
            24, 0.090f, 0.065f, 0.020f, 5101, soil);
        AddRibbon("WornPathB_EdgeEast", parent,
            new Vector3(14.02f, 0.010f, 4.42f), new Vector3(14.02f, 0.010f, 10.58f),
            24, 0.090f, 0.065f, 0.020f, 5233, soil);

        foreach (string name in new[] { "WornPathB_EdgeWest", "WornPathB_EdgeEast" })
        {
            GameObject go = FindSceneObject(name);
            ConfigureWeathering(
                go,
                NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.SunExposed |
                NewTownSurfaceExposure.GroundContact | NewTownSurfaceExposure.UpwardFacing,
                NewTownStainSource.FootTraffic | NewTownStainSource.UVExposure | NewTownStainSource.GroundSplash,
                1f, 0.84f, 0.42f, 0.86f);
        }
        return 2;
    }

    private static GameObject AddRibbon(string name, Transform parent, Vector3 start, Vector3 end,
        int segments, float baseHalfWidth, float widthVariation, float reliefVariation, int seed, Material material)
    {
        string path = $"{MeshRoot}/GM_HD_Interface_{name}.asset";
        Mesh mesh = BuildInterfaceRibbonMesh(name, start, end, segments, baseHalfWidth, widthVariation, reliefVariation, seed);
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(mesh, existing);
            UnityEngine.Object.DestroyImmediate(mesh);
            mesh = existing;
            EditorUtility.SetDirty(mesh);
        }
        else
        {
            mesh.name = "GM_HD_Interface_" + name;
            AssetDatabase.CreateAsset(mesh, path);
        }

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        go.isStatic = true;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        renderer.receiveShadows = true;
        return go;
    }

    private static Mesh BuildInterfaceRibbonMesh(string id, Vector3 start, Vector3 end, int segments,
        float baseHalfWidth, float widthVariation, float reliefVariation, int seed)
    {
        segments = Mathf.Max(4, segments);
        Vector3 axis = end - start;
        float length = new Vector2(axis.x, axis.z).magnitude;
        if (length < 0.10f)
            throw new InvalidOperationException($"Interface ribbon {id} is too short.");
        Vector3 tangent = new Vector3(axis.x, 0f, axis.z).normalized;
        Vector3 lateral = new Vector3(-tangent.z, 0f, tangent.x);
        const float thickness = 0.008f;

        var vertices = new List<Vector3>((segments + 1) * 4);
        var uvs = new List<Vector2>((segments + 1) * 4);
        var triangles = new List<int>(segments * 18 + 12);

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 center = Vector3.Lerp(start, end, t);
            float slow = Mathf.Sin((t * 3.0f + Hash01(seed, 7) * 1.7f) * Mathf.PI * 2f);
            float fast = HashSigned(seed, i * 13 + 5);
            float halfWidth = Mathf.Max(0.018f, baseHalfWidth + widthVariation * (0.62f * slow + 0.38f * fast));
            float relief = reliefVariation * (0.55f * HashSigned(seed, i * 17 + 11) + 0.45f * slow);
            center.y += relief;

            Vector3 leftTop = center - lateral * halfWidth;
            Vector3 rightTop = center + lateral * halfWidth;
            Vector3 leftBottom = leftTop - Vector3.up * thickness;
            Vector3 rightBottom = rightTop - Vector3.up * thickness;
            vertices.Add(leftTop);
            vertices.Add(rightTop);
            vertices.Add(leftBottom);
            vertices.Add(rightBottom);
            float u = t * length;
            uvs.Add(new Vector2(u, 0f));
            uvs.Add(new Vector2(u, 1f));
            uvs.Add(new Vector2(u, 0f));
            uvs.Add(new Vector2(u, 1f));
        }

        for (int i = 0; i < segments; i++)
        {
            int a = i * 4;
            int b = (i + 1) * 4;
            AddQuad(triangles, a, b, b + 1, a + 1);       // top
            AddQuad(triangles, a + 2, a + 3, b + 3, b + 2); // bottom
            AddQuad(triangles, a, a + 2, b + 2, b);         // left side
            AddQuad(triangles, a + 1, b + 1, b + 3, a + 3); // right side
        }
        AddQuad(triangles, 0, 1, 3, 2);
        int last = segments * 4;
        AddQuad(triangles, last, last + 2, last + 3, last + 1);

        var mesh = new Mesh();
        mesh.name = "GM_HD_Interface_" + id;
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddQuad(List<int> triangles, int a, int b, int c, int d)
    {
        triangles.Add(a); triangles.Add(b); triangles.Add(c);
        triangles.Add(a); triangles.Add(c); triangles.Add(d);
    }

    private static GameObject AddBox(string name, Transform parent, Vector3 worldPosition, Vector3 dimensions, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = worldPosition;
        go.transform.rotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        go.isStatic = true;
        go.AddComponent<MeshFilter>().sharedMesh = QualityBlockDetailMeshLibrary.GetChamferedBox(dimensions);
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        renderer.receiveShadows = true;
        return go;
    }

    private static GameObject NewRoot(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static Material GetOrCreateMaterial(string name, Color color, float smoothness, float metallic)
    {
        string path = $"{MaterialRoot}/{name}.mat";
        Shader shader = Shader.Find("Standard");
        if (shader == null)
            throw new InvalidOperationException("Standard shader not found for ground contact material.");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.shader = shader;
            material.name = name;
        }
        material.color = color;
        material.SetFloat("_Glossiness", Mathf.Clamp01(smoothness));
        material.SetFloat("_Metallic", Mathf.Clamp01(metallic));
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ConfigureWeathering(GameObject go, NewTownSurfaceExposure exposure,
        NewTownStainSource sources, float rain, float sun, float splash, float contact)
    {
        if (go == null)
            throw new InvalidOperationException("Cannot attach ground-contact weathering metadata to a null object.");
        QualityBlockWeatheringSurface metadata = go.GetComponent<QualityBlockWeatheringSurface>();
        if (metadata == null)
            metadata = go.AddComponent<QualityBlockWeatheringSurface>();
        metadata.Configure(exposure, sources, rain, sun, splash, contact);
        EditorUtility.SetDirty(metadata);
    }

    private static float Hash01(int seed, int value)
    {
        unchecked
        {
            uint x = (uint)(seed * 374761393 + value * 668265263);
            x = (x ^ (x >> 13)) * 1274126177u;
            x ^= x >> 16;
            return (x & 0x00ffffffu) / 16777215f;
        }
    }

    private static float HashSigned(int seed, int value)
    {
        return Hash01(seed, value) * 2f - 1f;
    }

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x.scene.IsValid() && x.name == name);
    }

    private static void ValidateContract()
    {
        if (!File.Exists(ContractPath))
            throw new InvalidOperationException($"Ground contact interface contract missing: {ContractPath}");
        GroundContactContract contract = JsonUtility.FromJson<GroundContactContract>(File.ReadAllText(ContractPath));
        if (contract == null || contract.materials == null || contract.assemblies == null)
            throw new InvalidOperationException("Ground contact interface contract could not be parsed.");
        if (contract.runtimeRenderVerified)
            throw new InvalidOperationException("Ground contact contract may not claim runtime render verification before real Unity evidence is recorded.");
        if (contract.materials.Length < 3 || contract.assemblies.Length < 3)
            throw new InvalidOperationException("Ground contact contract must define at least three material and three assembly families.");
        foreach (GroundContactMaterialSpec material in contract.materials)
        {
            Require(material.id, "material id");
            Require(material.materialFamily, $"materialFamily for {material.id}");
            Require(material.finish, $"finish for {material.id}");
            Require(material.microstructure, $"microstructure for {material.id}");
            Require(material.wetResponse, $"wetResponse for {material.id}");
            Require(material.frontLightResponse, $"frontLightResponse for {material.id}");
            Require(material.grazingLightResponse, $"grazingLightResponse for {material.id}");
            Require(material.shadeResponse, $"shadeResponse for {material.id}");
            if (material.roughnessRange == null || material.roughnessRange.Length != 2 ||
                material.roughnessRange[0] < 0f || material.roughnessRange[1] > 1f ||
                material.roughnessRange[0] > material.roughnessRange[1])
                throw new InvalidOperationException($"Invalid roughness range for ground contact material {material.id}.");
            if (material.metallicRange == null || material.metallicRange.Length != 2 || material.metallicRange[1] > 0.01f)
                throw new InvalidOperationException($"Ground contact mineral/soil material must be non-metallic: {material.id}.");
        }
        foreach (GroundContactAssemblySpec assembly in contract.assemblies)
        {
            Require(assembly.id, "assembly id");
            Require(assembly.nominalDimensions, $"nominalDimensions for {assembly.id}");
            Require(assembly.manufacture, $"manufacture for {assembly.id}");
            Require(assembly.mounting, $"mounting for {assembly.id}");
            Require(assembly.interfaces, $"interfaces for {assembly.id}");
            Require(assembly.orientationExposure, $"orientationExposure for {assembly.id}");
            Require(assembly.aging, $"aging for {assembly.id}");
            Require(assembly.geometryVsMaterial, $"geometryVsMaterial for {assembly.id}");
            Require(assembly.lookdevBrief, $"lookdevBrief for {assembly.id}");
        }
    }

    private static void Require(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Ground contact contract missing required {label}.");
    }

    [Serializable]
    private sealed class GroundContactContract
    {
        public bool runtimeRenderVerified;
        public GroundContactMaterialSpec[] materials;
        public GroundContactAssemblySpec[] assemblies;
    }

    [Serializable]
    private sealed class GroundContactMaterialSpec
    {
        public string id;
        public string materialFamily;
        public string finish;
        public float[] roughnessRange;
        public float[] metallicRange;
        public string microstructure;
        public string wetResponse;
        public string frontLightResponse;
        public string grazingLightResponse;
        public string shadeResponse;
    }

    [Serializable]
    private sealed class GroundContactAssemblySpec
    {
        public string id;
        public string nominalDimensions;
        public string manufacture;
        public string mounting;
        public string interfaces;
        public string orientationExposure;
        public string aging;
        public string geometryVsMaterial;
        public string lookdevBrief;
    }
}

public sealed class QualityBlockGroundContactManifest : MonoBehaviour
{
    [SerializeField] int plinthPartCount;
    [SerializeField] int hardscapeShoulderCount;
    [SerializeField] int erodedShoulderCount;

    public int PlinthPartCount => plinthPartCount;
    public int HardscapeShoulderCount => hardscapeShoulderCount;
    public int ErodedShoulderCount => erodedShoulderCount;

    public void Configure(int plinthParts, int hardscapeShoulders, int erodedShoulders)
    {
        plinthPartCount = Mathf.Max(0, plinthParts);
        hardscapeShoulderCount = Mathf.Max(0, hardscapeShoulders);
        erodedShoulderCount = Mathf.Max(0, erodedShoulders);
    }
}
