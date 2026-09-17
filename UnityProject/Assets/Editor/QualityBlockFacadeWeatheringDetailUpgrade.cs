using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Rebuilds facade weathering as source-anchored microgeometry instead of primitive rectangular
/// placeholder cubes. Every visible mark must terminate at a real construction/exposure source:
/// window drip edge, balcony drip edge, ferrous rail fixing, downpipe or grade splash zone.
///
/// This is a dry midsummer benchmark. The pass therefore models aged residue/soiling, not a generic
/// wet-look coating. It intentionally cannot award Visual Fidelity points; render evidence is still
/// required before the 100-point gate can be scored.
/// </summary>
public static class QualityBlockFacadeWeatheringDetailUpgrade
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string RootName = "FacadeWeatheringDetail";
    private const string LegacyRootName = "WeatheringOverlays";
    private const string AssetRoot = "Assets/Art/GeneratedWeatheringDetail";
    private const float FacadePlaneZ = -7.276f;

    [MenuItem("NewTown/Materials/Build Cause-Based Facade Weathering Detail")]
    public static void BuildAndApply()
    {
        EnsureBenchmarkScene();
        ApplyToOpenScene();
        ValidateOpenScene();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Cause-based facade weathering detail built from construction sources. Native 4K render verification remains pending.");
    }

    public static void ApplyToOpenScene()
    {
        EnsureBenchmarkScene();

        GameObject danchi = FindSceneObject("Danchi");
        GameObject detail = FindSceneObject("DanchiHighDetail");
        if (danchi == null || detail == null)
            throw new InvalidOperationException("Detailed danchi is required before facade weathering can be reconstructed.");

        GameObject legacy = FindSceneObject(LegacyRootName);
        if (legacy != null)
            UnityEngine.Object.DestroyImmediate(legacy);
        GameObject previous = FindSceneObject(RootName);
        if (previous != null)
            UnityEngine.Object.DestroyImmediate(previous);

        Directory.CreateDirectory(AssetRoot);
        Material rainResidue = GetOrCreateStainMaterial(
            "MAT_RainMineralResidue", new Color(0.23f, 0.245f, 0.225f, 0.105f), 0.08f);
        Material splashSoil = GetOrCreateStainMaterial(
            "MAT_GradeSplashSoil", new Color(0.18f, 0.135f, 0.082f, 0.135f), 0.05f);
        Material rustBleed = GetOrCreateStainMaterial(
            "MAT_FerrousRunoffResidue", new Color(0.31f, 0.105f, 0.035f, 0.12f), 0.10f);
        Material condensateResidue = GetOrCreateStainMaterial(
            "MAT_CondensateMineralResidue", new Color(0.30f, 0.31f, 0.28f, 0.085f), 0.06f);

        var root = new GameObject(RootName);
        root.transform.SetParent(danchi.transform, false);

        BuildGradeSplash(root.transform, splashSoil);
        BuildWindowAndDownpipeRunoff(root.transform, rainResidue);
        BuildBalconyDripResidue(root.transform, rainResidue);
        BuildRailFixingRust(root.transform, rustBleed);
        BuildAcDrainResidue(root.transform, condensateResidue);

        EditorUtility.SetDirty(root);
    }

    [MenuItem("NewTown/QA/Validate Cause-Based Facade Weathering Detail")]
    public static void ValidateOpenScene()
    {
        EnsureBenchmarkScene();

        if (FindSceneObject(LegacyRootName) != null)
            throw new InvalidOperationException("Legacy WeatheringOverlays still exists; primitive/rectangular placeholder weathering is forbidden in benchmark framing.");

        GameObject root = FindSceneObject(RootName);
        if (root == null)
            throw new InvalidOperationException("FacadeWeatheringDetail is missing.");
        if (root.GetComponentsInChildren<Collider>(true).Length != 0)
            throw new InvalidOperationException("Weathering detail must never alter gameplay collision.");

        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
        if (filters.Length != 5)
            throw new InvalidOperationException($"Expected five causal weathering mesh groups, got {filters.Length}.");

        string[] requiredGroups =
        {
            "Weathering_GradeSplash",
            "Weathering_RainRunoff",
            "Weathering_BalconyDrip",
            "Weathering_RailRust",
            "Weathering_ACResidue",
        };
        foreach (string group in requiredGroups)
        {
            GameObject go = FindSceneObject(group);
            if (go == null)
                throw new InvalidOperationException($"Required weathering source group missing: {group}.");
            Mesh mesh = go.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh == null || mesh.vertexCount < 12)
                throw new InvalidOperationException($"Weathering group {group} has insufficient non-placeholder geometry.");
        }

        string[] primitiveMeshNames = { "Cube", "Cylinder", "Sphere", "Capsule", "Plane", "Quad" };
        foreach (MeshFilter filter in filters)
        {
            if (filter.sharedMesh == null)
                throw new InvalidOperationException($"Weathering MeshFilter {filter.name} has no mesh.");
            if (primitiveMeshNames.Contains(filter.sharedMesh.name, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Built-in primitive mesh {filter.sharedMesh.name} is forbidden for visible weathering detail.");
        }

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Material mat = renderer.sharedMaterial;
            if (mat == null)
                throw new InvalidOperationException($"Weathering renderer {renderer.name} has no material.");
            if (mat.HasProperty("_Metallic") && mat.GetFloat("_Metallic") > 0.001f)
                throw new InvalidOperationException($"Weathering residue {mat.name} is incorrectly metallic.");
            if (mat.HasProperty("_Glossiness") && mat.GetFloat("_Glossiness") > 0.25f)
                throw new InvalidOperationException($"Dry weathering residue {mat.name} is implausibly glossy.");
        }

        int sillSources = FindSceneObjectsByPrefix("HD_WindowSillDrip").Length;
        int balconySources = FindSceneObjectsByPrefix("HD_BalconySlabLip").Length;
        int railSources = FindSceneObjectsByPrefix("HD_RailBasePlate_").Length;
        int acDrainSources = FindSceneObjectsByPrefix("HD_AC_DrainHose").Length;
        if (sillSources != 30 || balconySources != 30 || railSources != 210 || acDrainSources != 15)
            throw new InvalidOperationException(
                $"Weathering source topology changed unexpectedly: sills={sillSources}, balconies={balconySources}, railFixings={railSources}, acDrains={acDrainSources}.");

        Debug.Log("Facade weathering QA passed structurally: all visible marks are non-primitive, non-metallic, source-anchored microgeometry. Render verification remains pending.");
    }

    private static void BuildGradeSplash(Transform root, Material material)
    {
        var mesh = new MeshAccumulator(root);
        const int segments = 40;
        const float minX = -20.55f;
        const float maxX = 4.95f;
        const float bottomY = 0.025f;

        var bottom = new Vector3[segments + 1];
        var top = new Vector3[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float x = Mathf.Lerp(minX, maxX, t);
            // Splash is strongest near the real downpipe discharge side and otherwise stays within
            // the physically credible 0.18-0.42 m grade-splash band.
            float drainInfluence = Mathf.Exp(-Mathf.Abs(x - 4.45f) / 1.35f);
            float surfaceVariation = 0.025f * Mathf.Sin(x * 0.83f) + 0.015f * Mathf.Sin(x * 1.91f + 0.4f);
            float h = Mathf.Clamp(0.225f + 0.125f * drainInfluence + surfaceVariation, 0.18f, 0.42f);
            bottom[i] = new Vector3(x, bottomY, FacadePlaneZ);
            top[i] = new Vector3(x, bottomY + h, FacadePlaneZ);
        }
        mesh.AddStrip(bottom, top);
        CreateMeshObject("Weathering_GradeSplash", root, mesh.Build("GM_Weathering_GradeSplash"), material);
    }

    private static void BuildWindowAndDownpipeRunoff(Transform root, Material material)
    {
        var mesh = new MeshAccumulator(root);
        GameObject[] sills = FindSceneObjectsByPrefix("HD_WindowSillDrip")
            .OrderBy(x => x.transform.position.y)
            .ThenBy(x => x.transform.position.x)
            .ToArray();

        foreach (GameObject sill in sills)
        {
            Vector3 p = sill.transform.position;
            int seed = StablePathHash(sill.transform);
            float lateral = Hash01(seed, 13) * 1.36f - 0.68f;
            float length = Mathf.Lerp(0.36f, 0.94f, Hash01(seed, 29));
            float width = Mathf.Lerp(0.024f, 0.052f, Hash01(seed, 43));
            float drift = Mathf.Lerp(-0.045f, 0.045f, Hash01(seed, 61));
            Vector3 top = new Vector3(p.x + lateral, p.y - 0.94f, FacadePlaneZ);
            mesh.AddVerticalRibbon(top, length, width, drift, 6, seed);
        }

        // Concentrated mineral residue follows the installed full-height rainwater downpipe.
        mesh.AddVerticalRibbon(new Vector3(4.45f, 6.46f, FacadePlaneZ), 5.95f, 0.115f, 0.035f, 18, 9173);
        mesh.AddVerticalRibbon(new Vector3(4.40f, 3.85f, FacadePlaneZ + 0.0015f), 3.15f, 0.048f, -0.025f, 11, 11219);

        CreateMeshObject("Weathering_RainRunoff", root, mesh.Build("GM_Weathering_RainRunoff"), material);
    }

    private static void BuildBalconyDripResidue(Transform root, Material material)
    {
        var mesh = new MeshAccumulator(root);
        GameObject[] lips = FindSceneObjectsByPrefix("HD_BalconySlabLip")
            .OrderBy(x => x.transform.position.y)
            .ThenBy(x => x.transform.position.x)
            .ToArray();

        foreach (GameObject lip in lips)
        {
            Vector3 p = lip.transform.position;
            int seed = StablePathHash(lip.transform);
            int dripCount = Hash01(seed, 7) > 0.58f ? 2 : 1;
            for (int i = 0; i < dripCount; i++)
            {
                float lateral = Mathf.Lerp(-1.25f, 1.25f, Hash01(seed, 101 + i * 17));
                float length = Mathf.Lerp(0.10f, 0.31f, Hash01(seed, 149 + i * 19));
                float width = Mathf.Lerp(0.018f, 0.038f, Hash01(seed, 191 + i * 23));
                float planeZ = p.z + 0.052f + i * 0.0008f;
                mesh.AddVerticalRibbon(
                    new Vector3(p.x + lateral, p.y - 0.095f, planeZ),
                    length, width, Mathf.Lerp(-0.018f, 0.018f, Hash01(seed, 227 + i)), 4, seed + i * 31);
            }
        }
        CreateMeshObject("Weathering_BalconyDrip", root, mesh.Build("GM_Weathering_BalconyDrip"), material);
    }

    private static void BuildRailFixingRust(Transform root, Material material)
    {
        var mesh = new MeshAccumulator(root);
        GameObject[] plates = FindSceneObjectsByPrefix("HD_RailBasePlate_")
            .OrderBy(x => x.transform.position.y)
            .ThenBy(x => x.transform.position.x)
            .ToArray();

        foreach (GameObject plate in plates)
        {
            int seed = StablePathHash(plate.transform);
            // Coating failure is sparse. Keeping most fixtures clean avoids the repeated procedural
            // rust pattern that would itself become an immediate visual-fidelity defect.
            if (Hash01(seed, 313) > 0.19f)
                continue;

            Vector3 p = plate.transform.position;
            float length = Mathf.Lerp(0.07f, 0.19f, Hash01(seed, 331));
            float width = Mathf.Lerp(0.012f, 0.026f, Hash01(seed, 347));
            mesh.AddVerticalRibbon(
                new Vector3(p.x, p.y - 0.025f, p.z + 0.052f),
                length, width, Mathf.Lerp(-0.012f, 0.012f, Hash01(seed, 359)), 3, seed);
        }
        CreateMeshObject("Weathering_RailRust", root, mesh.Build("GM_Weathering_RailRust"), material);
    }

    private static void BuildAcDrainResidue(Transform root, Material material)
    {
        var mesh = new MeshAccumulator(root);
        GameObject[] drains = FindSceneObjectsByPrefix("HD_AC_DrainHose")
            .OrderBy(x => x.transform.position.y)
            .ThenBy(x => x.transform.position.x)
            .ToArray();

        foreach (GameObject drain in drains)
        {
            Renderer renderer = drain.GetComponent<Renderer>();
            Vector3 p = renderer != null ? renderer.bounds.center : drain.transform.position;
            float y = renderer != null ? renderer.bounds.min.y - 0.003f : p.y - 0.10f;
            int seed = StablePathHash(drain.transform);
            float rx = Mathf.Lerp(0.055f, 0.095f, Hash01(seed, 401));
            float rz = Mathf.Lerp(0.035f, 0.070f, Hash01(seed, 419));
            mesh.AddHorizontalEllipse(new Vector3(p.x, y, p.z + 0.02f), rx, rz, 14, seed);
        }
        CreateMeshObject("Weathering_ACResidue", root, mesh.Build("GM_Weathering_ACResidue"), material);
    }

    private static GameObject CreateMeshObject(string name, Transform parent, Mesh source, Material material)
    {
        string path = $"{AssetRoot}/{source.name}.asset";
        Mesh persistent = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (persistent == null)
        {
            persistent = source;
            AssetDatabase.CreateAsset(persistent, path);
        }
        else
        {
            EditorUtility.CopySerialized(source, persistent);
            UnityEngine.Object.DestroyImmediate(source);
            EditorUtility.SetDirty(persistent);
        }

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = persistent;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = true;
        renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbesAndSkybox;
        return go;
    }

    private static Material GetOrCreateStainMaterial(string name, Color color, float smoothness)
    {
        Shader shader = Shader.Find("Standard");
        if (shader == null)
            throw new InvalidOperationException("Standard shader not found for weathering detail.");

        string path = $"{AssetRoot}/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = shader;
        }

        mat.color = color;
        mat.SetFloat("_Mode", 2f);
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = (int)RenderQueue.Transparent;
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
        if (mat.HasProperty("_SpecularHighlights")) mat.SetFloat("_SpecularHighlights", 1f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void EnsureBenchmarkScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x.scene.IsValid() && x.name == name);
    }

    private static GameObject[] FindSceneObjectsByPrefix(string prefix)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .Where(x => x.scene.IsValid() && x.name.StartsWith(prefix, StringComparison.Ordinal))
            .ToArray();
    }

    private static int StablePathHash(Transform transform)
    {
        unchecked
        {
            int hash = 17;
            Transform current = transform;
            while (current != null)
            {
                string value = current.name;
                for (int i = 0; i < value.Length; i++)
                    hash = hash * 31 + value[i];
                current = current.parent;
            }
            return hash & 0x7fffffff;
        }
    }

    private static float Hash01(int seed, int salt)
    {
        unchecked
        {
            uint x = (uint)(seed ^ (salt * 0x45d9f3b));
            x ^= x >> 16;
            x *= 0x7feb352d;
            x ^= x >> 15;
            x *= 0x846ca68b;
            x ^= x >> 16;
            return (x & 0x00ffffff) / 16777215f;
        }
    }

    private sealed class MeshAccumulator
    {
        private readonly Transform root;
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<int> triangles = new List<int>();

        public MeshAccumulator(Transform rootTransform)
        {
            root = rootTransform;
        }

        public void AddVerticalRibbon(Vector3 topWorld, float length, float width, float totalDrift,
            int segments, int seed)
        {
            int start = vertices.Count;
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float eased = t * t * (3f - 2f * t);
                float drift = totalDrift * eased + (Hash01(seed, 503 + i) - 0.5f) * width * 0.24f;
                float taper = Mathf.Lerp(1f, 0.22f, t);
                float w = width * taper;
                Vector3 centerWorld = topWorld + new Vector3(drift, -length * t, 0f);
                vertices.Add(root.InverseTransformPoint(centerWorld + Vector3.left * w * 0.5f));
                vertices.Add(root.InverseTransformPoint(centerWorld + Vector3.right * w * 0.5f));
                uvs.Add(new Vector2(0f, t));
                uvs.Add(new Vector2(1f, t));
            }

            for (int i = 0; i < segments; i++)
            {
                int l0 = start + i * 2;
                int r0 = l0 + 1;
                int l1 = l0 + 2;
                int r1 = l0 + 3;
                triangles.Add(l0); triangles.Add(l1); triangles.Add(r0);
                triangles.Add(r0); triangles.Add(l1); triangles.Add(r1);
            }
        }

        public void AddStrip(Vector3[] bottomWorld, Vector3[] topWorld)
        {
            if (bottomWorld == null || topWorld == null || bottomWorld.Length != topWorld.Length || bottomWorld.Length < 2)
                throw new ArgumentException("Weathering strip arrays must match and contain at least two points.");

            int start = vertices.Count;
            for (int i = 0; i < bottomWorld.Length; i++)
            {
                float u = i / (float)(bottomWorld.Length - 1);
                vertices.Add(root.InverseTransformPoint(bottomWorld[i]));
                vertices.Add(root.InverseTransformPoint(topWorld[i]));
                uvs.Add(new Vector2(u, 0f));
                uvs.Add(new Vector2(u, 1f));
            }

            for (int i = 0; i < bottomWorld.Length - 1; i++)
            {
                int b0 = start + i * 2;
                int t0 = b0 + 1;
                int b1 = b0 + 2;
                int t1 = b0 + 3;
                triangles.Add(b0); triangles.Add(b1); triangles.Add(t0);
                triangles.Add(t0); triangles.Add(b1); triangles.Add(t1);
            }
        }

        public void AddHorizontalEllipse(Vector3 centerWorld, float radiusX, float radiusZ, int segments, int seed)
        {
            int centerIndex = vertices.Count;
            vertices.Add(root.InverseTransformPoint(centerWorld));
            uvs.Add(new Vector2(0.5f, 0.5f));

            for (int i = 0; i <= segments; i++)
            {
                float a = (i / (float)segments) * Mathf.PI * 2f;
                float radial = Mathf.Lerp(0.90f, 1.08f, Hash01(seed, 607 + i));
                Vector3 p = centerWorld + new Vector3(Mathf.Cos(a) * radiusX * radial, 0f, Mathf.Sin(a) * radiusZ * radial);
                vertices.Add(root.InverseTransformPoint(p));
                uvs.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
            }

            for (int i = 0; i < segments; i++)
            {
                triangles.Add(centerIndex);
                triangles.Add(centerIndex + i + 2);
                triangles.Add(centerIndex + i + 1);
            }
        }

        public Mesh Build(string name)
        {
            if (vertices.Count < 3 || triangles.Count < 3)
                throw new InvalidOperationException($"Cannot build empty weathering mesh {name}.");

            var mesh = new Mesh { name = name };
            if (vertices.Count > 65535)
                mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
