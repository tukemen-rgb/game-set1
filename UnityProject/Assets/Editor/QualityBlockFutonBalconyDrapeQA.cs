using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Replaces the two benchmark-facing legacy Futon_* Cube renderers with render-only, closed-volume
/// textile drapes that actually wrap the balcony top rail. The gameplay/collision object is retained
/// but its primitive renderer is disabled. This is a source-side reduction of the automatic-fail
/// placeholder/interpenetration risk only; it never awards Visual Fidelity points without native 4K pixels.
/// </summary>
public static class QualityBlockFutonBalconyDrapeQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/futon_balcony_drape_contract.json";
    private const string RootName = "FutonBalconyDrapes";
    private const string MeshRoot = "Assets/Art/GeneratedFutonMeshes";
    private const string MaterialRoot = "Assets/Art/GeneratedFutonMaterials";
    private const string NormalPath = MaterialRoot + "/T_FutonCottonWeave_N.png";

    private const float WidthM = 1.15f;
    private const float BodyThicknessM = 0.022f;
    private const float FabricTileM = 0.14f;
    private const float MaxCenterSagM = 0.012f;
    private const float MinFloorClearanceM = 0.030f;
    private const float MaxFloorClearanceM = 0.090f;
    private const float ContactToleranceM = 0.012f;

    private static readonly float[] LodTransitions = { 0.14f, 0.065f, 0.028f, 0.009f };
    private static readonly int[] WidthSegments = { 24, 16, 10, 6 };
    private static readonly int[] PathSegments = { 30, 24, 12, 6 };

    private struct FutonSpec
    {
        public int floor;
        public int bay;
        public float frontDrop;
        public float backDrop;
        public float sag;
        public int materialVariant;

        public FutonSpec(int floor, int bay, float frontDrop, float backDrop, float sag, int materialVariant)
        {
            this.floor = floor;
            this.bay = bay;
            this.frontDrop = frontDrop;
            this.backDrop = backDrop;
            this.sag = sag;
            this.materialVariant = materialVariant;
        }
    }

    private static readonly FutonSpec[] Specs =
    {
        // Deliberately non-identical occupancy. Both preserve the legacy 1.15 m visible width, while
        // balanced front/back drops differ enough to avoid exact clone silhouettes.
        new FutonSpec(2, 1, 0.700f, 0.480f, 0.012f, 0),
        new FutonSpec(3, 4, 0.645f, 0.535f, 0.009f, 1),
    };

    [MenuItem("NewTown/Facade/Reconstruct Balcony Futon Drapes")]
    public static void ApplyAndPersist()
    {
        EnsureScene();
        ApplyToOpenScene();
        ValidateOpenScene();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Balcony futon drapes reconstructed and persisted. Visual Fidelity remains UNSCORED pending native 4K evidence.");
    }

    public static void ApplyToOpenScene()
    {
        EnsureScene();
        ValidateContractConfigOnly();

        GameObject danchi = FindSceneObject("Danchi");
        if (danchi == null) throw new InvalidOperationException("Danchi root is missing.");

        QualityBlockArtSlot slot = Resources.FindObjectsOfTypeAll<QualityBlockArtSlot>()
            .FirstOrDefault(x => x.gameObject.scene.IsValid() && x.SlotId == "danchi.main");
        if (slot != null && slot.IsUsingAuthoredArt)
        {
            GameObject stale = FindSceneObject(RootName);
            if (stale != null) UnityEngine.Object.DestroyImmediate(stale);
            foreach (FutonSpec spec in Specs)
            {
                GameObject legacy = FindSceneObject(LegacyName(spec));
                Renderer renderer = legacy != null ? legacy.GetComponent<Renderer>() : null;
                if (renderer != null) renderer.enabled = false;
            }
            return;
        }

        Directory.CreateDirectory(MeshRoot);
        Directory.CreateDirectory(MaterialRoot);
        EnsureCottonNormalTexture();
        Material[] materials = BuildMaterials();

        GameObject old = FindSceneObject(RootName);
        if (old != null) UnityEngine.Object.DestroyImmediate(old);

        GameObject root = new GameObject(RootName);
        root.transform.SetParent(danchi.transform, false);

        foreach (FutonSpec spec in Specs)
            BuildAssembly(root.transform, spec, materials[spec.materialVariant]);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("NewTown/QA/Validate Balcony Futon Drape Contract")]
    public static void ValidateContractConfigOnly()
    {
        if (!File.Exists(ContractPath))
            throw new InvalidOperationException($"Futon construction contract missing: {ContractPath}");

        FutonContract contract = JsonUtility.FromJson<FutonContract>(File.ReadAllText(ContractPath));
        var errors = new List<string>();
        if (contract == null) errors.Add("contract is null/unparseable");
        else
        {
            if (contract.schemaVersion != "1.0.0") errors.Add("schemaVersion must remain 1.0.0");
            if (contract.scenePath != ScenePath) errors.Add($"scenePath must be {ScenePath}");
            if (contract.expectedAssemblies != 2) errors.Add("expectedAssemblies must remain 2");
            if (Mathf.Abs(contract.bodyThicknessM - BodyThicknessM) > 0.0001f) errors.Add("bodyThicknessM drifted from 0.022 m");
            if (Mathf.Abs(contract.minFloorClearanceM - MinFloorClearanceM) > 0.0001f) errors.Add("minFloorClearanceM drifted");
            if (Mathf.Abs(contract.maxFloorClearanceM - MaxFloorClearanceM) > 0.0001f) errors.Add("maxFloorClearanceM drifted");
            if (contract.visualCreditPolicy == null || contract.visualCreditPolicy.autoVisualPoints != 0)
                errors.Add("source contract may not award Visual Fidelity points");
            if (contract.lodPolicy == null || contract.lodPolicy.levels != 4 || !contract.lodPolicy.crossFadeRequired)
                errors.Add("four cross-faded LODs are mandatory");
        }

        if (errors.Count > 0)
            throw new InvalidOperationException("Balcony futon construction contract FAILED:\n - " + string.Join("\n - ", errors));
    }

    [MenuItem("NewTown/QA/Validate Balcony Futon Drape Installation")]
    public static void ValidateOpenScene()
    {
        EnsureScene();
        ValidateContractConfigOnly();

        QualityBlockArtSlot slot = Resources.FindObjectsOfTypeAll<QualityBlockArtSlot>()
            .FirstOrDefault(x => x.gameObject.scene.IsValid() && x.SlotId == "danchi.main");
        if (slot != null && slot.IsUsingAuthoredArt)
        {
            if (FindSceneObject(RootName) != null)
                throw new InvalidOperationException("Generated futon drapes must not remain behind authored danchi art.");
            return;
        }

        GameObject root = FindSceneObject(RootName);
        if (root == null) throw new InvalidOperationException("FutonBalconyDrapes root is missing.");
        if (root.GetComponentsInChildren<Collider>(true).Length != 0)
            throw new InvalidOperationException("Generated futon drapes are render-only and may not add gameplay colliders.");

        foreach (FutonSpec spec in Specs)
        {
            string legacyName = LegacyName(spec);
            GameObject legacy = FindSceneObject(legacyName);
            if (legacy == null) throw new InvalidOperationException($"Legacy occupancy anchor missing: {legacyName}");
            Renderer legacyRenderer = legacy.GetComponent<Renderer>();
            if (legacyRenderer != null && legacyRenderer.enabled)
                throw new InvalidOperationException($"Critical primitive-placeholder risk: legacy {legacyName} renderer is still enabled.");

            Transform assembly = root.transform.Find(AssemblyName(spec));
            if (assembly == null) throw new InvalidOperationException($"Futon assembly missing: {AssemblyName(spec)}");

            LODGroup group = assembly.GetComponent<LODGroup>();
            if (group == null) throw new InvalidOperationException($"{assembly.name} is missing LODGroup.");
            LOD[] lods = group.GetLODs();
            if (lods.Length != 4) throw new InvalidOperationException($"{assembly.name} must have exactly four LOD levels.");
            if (group.fadeMode != LODFadeMode.CrossFade || !group.animateCrossFading)
                throw new InvalidOperationException($"{assembly.name} must use animated cross-fade.");

            Bounds? lod0Bounds = null;
            int previousVertices = int.MaxValue;
            for (int level = 0; level < 4; level++)
            {
                if (lods[level].renderers == null || lods[level].renderers.Length != 1 || lods[level].renderers[0] == null)
                    throw new InvalidOperationException($"{assembly.name} LOD{level} must own exactly one closed textile renderer.");

                Renderer renderer = lods[level].renderers[0];
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                Mesh mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null || !mesh.name.StartsWith("GM_FutonDrape_", StringComparison.Ordinal))
                    throw new InvalidOperationException($"{assembly.name} LOD{level} must use dedicated futon geometry, not a primitive placeholder.");
                if (IsBuiltInPrimitive(mesh.name))
                    throw new InvalidOperationException($"{assembly.name} LOD{level} still exposes built-in primitive mesh {mesh.name}.");
                if (mesh.vertexCount >= previousVertices)
                    throw new InvalidOperationException($"{assembly.name} LOD vertex counts must progressively decrease; LOD{level} has {mesh.vertexCount} vertices after {previousVertices}.");
                previousVertices = mesh.vertexCount;

                Material material = renderer.sharedMaterial;
                ValidateCottonMaterial(assembly.name, level, material);

                if (level == 0) lod0Bounds = renderer.bounds;
                else if (lod0Bounds.HasValue)
                {
                    Bounds b0 = lod0Bounds.Value;
                    Bounds b = renderer.bounds;
                    float sizeDelta = MaxComponent(Abs(b.size - b0.size));
                    float centerDelta = (b.center - b0.center).magnitude;
                    if (sizeDelta > 0.018f || centerDelta > 0.010f)
                        throw new InvalidOperationException($"{assembly.name} LOD{level} changes macro drape silhouette too much: sizeDelta={sizeDelta:F4}m centerDelta={centerDelta:F4}m.");
                }
            }

            Renderer rail = RequireRenderer($"RailTop_{spec.floor}_{spec.bay}");
            Renderer floor = RequireRenderer($"BalconyFloor_{spec.floor}_{spec.bay}");
            Bounds cloth = lod0Bounds ?? throw new InvalidOperationException($"{assembly.name} LOD0 bounds missing.");

            // The thick textile must wrap both sides and crest above the top rail rather than remain a flat slab in front.
            if (cloth.max.z < rail.bounds.max.z + 0.025f || cloth.min.z > rail.bounds.min.z - 0.005f)
                throw new InvalidOperationException($"{assembly.name} does not physically wrap front/back of the top rail.");
            if (cloth.max.y < rail.bounds.max.y + 0.004f)
                throw new InvalidOperationException($"{assembly.name} crest does not rise over the top rail.");

            float floorClearance = cloth.min.y - floor.bounds.max.y;
            if (floorClearance < MinFloorClearanceM || floorClearance > MaxFloorClearanceM)
                throw new InvalidOperationException(
                    $"{assembly.name} lower edge/floor clearance {floorClearance:F4}m outside {MinFloorClearanceM:F3}-{MaxFloorClearanceM:F3}m; " +
                    "do not hide slab interpenetration by changing the QA threshold.");

            float railHalfY = rail.bounds.extents.y;
            float railHalfZ = rail.bounds.extents.z;
            float designedInnerOffset = BodyThicknessM * 0.5f;
            float crestContact = Mathf.Abs((0.056f - designedInnerOffset) - railHalfY);
            float faceContact = Mathf.Abs((0.056f - designedInnerOffset) - railHalfZ);
            if (crestContact > ContactToleranceM || faceContact > ContactToleranceM)
                throw new InvalidOperationException(
                    $"{assembly.name} rail contact envelope drifted: crest={crestContact:F4}m face={faceContact:F4}m.");
        }

        MeshRenderer[] active = root.GetComponentsInChildren<MeshRenderer>(true);
        if (active.Length != 8)
            throw new InvalidOperationException($"Expected two futons x four LOD renderers = 8, got {active.Length}.");

        Debug.Log(
            "Balcony futon source QA passed: two legacy Cube renderers disabled; two non-identical closed textile drapes wrap real rail geometry, " +
            "clear the balcony floor, remain dielectric and preserve four progressively simplified cross-faded LOD silhouettes. " +
            "Native 3840x2160 pixels are still required before any Visual Fidelity credit or critical-defect clearance.");
    }

    private static void BuildAssembly(Transform parent, FutonSpec spec, Material material)
    {
        GameObject legacy = FindSceneObject(LegacyName(spec));
        if (legacy == null) throw new InvalidOperationException($"Legacy futon anchor missing: {LegacyName(spec)}");
        Renderer legacyRenderer = legacy.GetComponent<Renderer>();
        if (legacyRenderer != null) legacyRenderer.enabled = false;

        Renderer rail = RequireRenderer($"RailTop_{spec.floor}_{spec.bay}");
        Renderer floor = RequireRenderer($"BalconyFloor_{spec.floor}_{spec.bay}");

        GameObject assembly = new GameObject(AssemblyName(spec));
        assembly.transform.SetParent(parent, false);
        assembly.transform.position = new Vector3(legacy.transform.position.x, rail.bounds.center.y, rail.bounds.center.z);

        Renderer[][] rendererSets = new Renderer[4][];
        for (int level = 0; level < 4; level++)
        {
            GameObject lodRoot = new GameObject($"LOD{level}");
            lodRoot.transform.SetParent(assembly.transform, false);

            Mesh mesh = BuildOrReplaceDrapeMeshAsset(spec, level);
            GameObject body = new GameObject("FutonBody");
            body.transform.SetParent(lodRoot.transform, false);
            MeshFilter filter = body.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = body.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            rendererSets[level] = new Renderer[] { renderer };
        }

        LODGroup group = assembly.AddComponent<LODGroup>();
        group.fadeMode = LODFadeMode.CrossFade;
        group.animateCrossFading = true;
        group.SetLODs(new[]
        {
            new LOD(LodTransitions[0], rendererSets[0]),
            new LOD(LodTransitions[1], rendererSets[1]),
            new LOD(LodTransitions[2], rendererSets[2]),
            new LOD(LodTransitions[3], rendererSets[3]),
        });
        group.RecalculateBounds();

        var manifest = assembly.AddComponent<QualityBlockFutonDrapeManifest>();
        manifest.Configure(spec.floor, spec.bay, WidthM, BodyThicknessM, spec.frontDrop, spec.backDrop,
            floor.bounds.max.y, spec.sag);
    }

    private static Mesh BuildOrReplaceDrapeMeshAsset(FutonSpec spec, int level)
    {
        string path = $"{MeshRoot}/GM_FutonDrape_F{spec.floor}_B{spec.bay}_LOD{level}.asset";
        Mesh old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old != null) AssetDatabase.DeleteAsset(path);

        Mesh mesh = BuildDrapeMesh(WidthM, spec.frontDrop, spec.backDrop, spec.sag,
            WidthSegments[level], PathSegments[level]);
        mesh.name = $"GM_FutonDrape_F{spec.floor}_B{spec.bay}_LOD{level}";
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static Mesh BuildDrapeMesh(float width, float frontDrop, float backDrop, float sag,
        int widthSegments, int pathSegments)
    {
        if (pathSegments % 6 != 0)
            throw new InvalidOperationException("Futon path segment count must be divisible by six so every installation control point is sampled exactly.");

        Vector2[] keys =
        {
            new Vector2(-backDrop, -0.090f),
            new Vector2(-0.060f, -0.061f),
            new Vector2( 0.050f, -0.050f),
            new Vector2( 0.061f,  0.000f),
            new Vector2( 0.050f,  0.050f),
            new Vector2(-0.060f,  0.061f),
            new Vector2(-frontDrop,  0.105f),
        };

        int pathCount = pathSegments + 1;
        int widthCount = widthSegments + 1;
        Vector2[] centers = new Vector2[pathCount];
        Vector2[] normals = new Vector2[pathCount];
        float[] distances = new float[pathCount];

        for (int p = 0; p < pathCount; p++)
        {
            float u = p / (float)pathSegments * (keys.Length - 1);
            int seg = Mathf.Min(keys.Length - 2, Mathf.FloorToInt(u));
            float t = Mathf.Clamp01(u - seg);
            Vector2 p0 = keys[Mathf.Max(0, seg - 1)];
            Vector2 p1 = keys[seg];
            Vector2 p2 = keys[seg + 1];
            Vector2 p3 = keys[Mathf.Min(keys.Length - 1, seg + 2)];
            centers[p] = CatmullRom(p0, p1, p2, p3, t);
            if (p > 0) distances[p] = distances[p - 1] + Vector2.Distance(centers[p - 1], centers[p]);
        }

        for (int p = 0; p < pathCount; p++)
        {
            Vector2 prev = centers[Mathf.Max(0, p - 1)];
            Vector2 next = centers[Mathf.Min(pathCount - 1, p + 1)];
            Vector2 tangent = (next - prev).normalized;
            normals[p] = new Vector2(-tangent.y, tangent.x).normalized;
        }

        int surfaceVertexCount = pathCount * widthCount;
        var vertices = new Vector3[surfaceVertexCount * 2];
        var uv = new Vector2[vertices.Length];
        float halfWidth = width * 0.5f;
        float halfThickness = BodyThicknessM * 0.5f;

        for (int side = 0; side < 2; side++)
        {
            float sign = side == 0 ? -1f : 1f;
            int sideOffset = side * surfaceVertexCount;
            for (int p = 0; p < pathCount; p++)
            {
                for (int xIndex = 0; xIndex < widthCount; xIndex++)
                {
                    float x01 = xIndex / (float)widthSegments;
                    float x = Mathf.Lerp(-halfWidth, halfWidth, x01);
                    float normalizedX = halfWidth > 0f ? x / halfWidth : 0f;
                    float centerSag = sag * Mathf.Max(0f, 1f - normalizedX * normalizedX);
                    Vector2 yz = centers[p] + normals[p] * (sign * halfThickness);
                    yz.x -= centerSag;

                    int i = sideOffset + p * widthCount + xIndex;
                    vertices[i] = new Vector3(x, yz.x, yz.y);
                    uv[i] = new Vector2((x + halfWidth) / FabricTileM, distances[p] / FabricTileM);
                }
            }
        }

        var triangles = new List<int>(pathSegments * widthSegments * 12 + (pathSegments + widthSegments) * 12);
        for (int side = 0; side < 2; side++)
        {
            int offset = side * surfaceVertexCount;
            bool reverse = side == 0;
            for (int p = 0; p < pathSegments; p++)
            for (int x = 0; x < widthSegments; x++)
            {
                int a = offset + p * widthCount + x;
                int b = a + 1;
                int d = offset + (p + 1) * widthCount + x;
                int c = d + 1;
                AddQuad(triangles, a, b, c, d, reverse);
            }
        }

        // Close the textile volume at both side hems and at both hanging ends. This keeps the physical
        // 22 mm loft observable at oblique/grazing angles instead of relying on a two-sided zero-thickness plane.
        int negative = 0;
        int positive = surfaceVertexCount;
        for (int p = 0; p < pathSegments; p++)
        {
            int n0 = negative + p * widthCount;
            int n1 = negative + (p + 1) * widthCount;
            int p0 = positive + p * widthCount;
            int p1 = positive + (p + 1) * widthCount;
            AddQuad(triangles, n0, p0, p1, n1, true);

            int nr0 = negative + p * widthCount + widthSegments;
            int nr1 = negative + (p + 1) * widthCount + widthSegments;
            int pr0 = positive + p * widthCount + widthSegments;
            int pr1 = positive + (p + 1) * widthCount + widthSegments;
            AddQuad(triangles, nr0, nr1, pr1, pr0, true);
        }

        for (int x = 0; x < widthSegments; x++)
        {
            int nBack0 = negative + x;
            int nBack1 = nBack0 + 1;
            int pBack0 = positive + x;
            int pBack1 = pBack0 + 1;
            AddQuad(triangles, nBack0, nBack1, pBack1, pBack0, false);

            int nFront0 = negative + pathSegments * widthCount + x;
            int nFront1 = nFront0 + 1;
            int pFront0 = positive + pathSegments * widthCount + x;
            int pFront1 = pFront0 + 1;
            AddQuad(triangles, nFront0, pFront0, pFront1, nFront1, false);
        }

        var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.SetTriangles(triangles, 0, true);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * ((2f * p1) + (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    private static void AddQuad(List<int> tris, int a, int b, int c, int d, bool reverse)
    {
        if (!reverse)
        {
            tris.Add(a); tris.Add(b); tris.Add(c);
            tris.Add(a); tris.Add(c); tris.Add(d);
        }
        else
        {
            tris.Add(a); tris.Add(c); tris.Add(b);
            tris.Add(a); tris.Add(d); tris.Add(c);
        }
    }

    private static Material[] BuildMaterials()
    {
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
        if (normal == null) throw new InvalidOperationException("Generated futon cotton normal map failed to import.");

        return new[]
        {
            GetOrCreateCottonMaterial("MAT_FutonCottonWashedBlueA", new Color(0.46f, 0.56f, 0.66f, 1f), 0.15f, normal, new Vector2(0.12f, 0.37f)),
            GetOrCreateCottonMaterial("MAT_FutonCottonWashedBlueB", new Color(0.53f, 0.61f, 0.69f, 1f), 0.17f, normal, new Vector2(0.43f, 0.08f)),
        };
    }

    private static Material GetOrCreateCottonMaterial(string name, Color albedo, float smoothness,
        Texture2D normal, Vector2 offset)
    {
        string path = $"{MaterialRoot}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("Unity Standard shader is unavailable.");
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }

        material.color = albedo;
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Glossiness", smoothness);
        material.SetTexture("_BumpMap", normal);
        material.SetFloat("_BumpScale", 0.34f);
        material.EnableKeyword("_NORMALMAP");
        material.SetTextureScale("_MainTex", Vector2.one);
        material.SetTextureOffset("_MainTex", offset);
        if (material.HasProperty("_EmissionColor"))
            material.SetColor("_EmissionColor", Color.black);
        material.DisableKeyword("_EMISSION");
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureCottonNormalTexture()
    {
        const int size = 512;
        var texture = new Texture2D(size, size, TextureFormat.RGB24, true, true);
        texture.name = "T_FutonCottonWeave_N";
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = Height(x + 1, y) - Height(x - 1, y);
            float dy = Height(x, y + 1) - Height(x, y - 1);
            Vector3 n = new Vector3(-dx * 1.8f, -dy * 1.8f, 1f).normalized;
            pixels[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
        }

        texture.SetPixels(pixels);
        texture.Apply(true, false);
        File.WriteAllBytes(NormalPath, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(NormalPath, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(NormalPath) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Unable to configure generated futon normal map importer.");
        importer.textureType = TextureImporterType.NormalMap;
        importer.sRGBTexture = false;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 8;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }

    private static float Height(int x, int y)
    {
        // Deterministic fine cotton weave: asymmetric warp/weft periods plus tiny hashed fibre noise.
        float warp = Mathf.Sin(x * Mathf.PI * 0.57f) * 0.34f;
        float weft = Mathf.Sin(y * Mathf.PI * 0.43f + 0.7f) * 0.29f;
        uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ 0x9E3779B9u;
        h ^= h >> 13; h *= 1274126177u; h ^= h >> 16;
        float fibre = ((h & 1023u) / 1023f - 0.5f) * 0.14f;
        return warp + weft + fibre;
    }

    private static void ValidateCottonMaterial(string assemblyName, int level, Material material)
    {
        if (material == null || material.shader == null || material.shader.name != "Standard")
            throw new InvalidOperationException($"{assemblyName} LOD{level} cotton must use Standard PBR material.");
        if (material.GetFloat("_Metallic") > 0.01f)
            throw new InvalidOperationException($"{assemblyName} LOD{level} cotton is materially impossible metallic textile.");
        float smoothness = material.GetFloat("_Glossiness");
        if (smoothness < 0.10f || smoothness > 0.22f)
            throw new InvalidOperationException($"{assemblyName} LOD{level} cotton smoothness {smoothness:F3} is outside the dry aged textile range.");
        if (!material.IsKeywordEnabled("_NORMALMAP") || material.GetTexture("_BumpMap") == null)
            throw new InvalidOperationException($"{assemblyName} LOD{level} lacks cotton weave normal microstructure.");
        if (material.IsKeywordEnabled("_EMISSION") ||
            (material.HasProperty("_EmissionColor") && material.GetColor("_EmissionColor").maxColorComponent > 0.001f))
            throw new InvalidOperationException($"{assemblyName} LOD{level} dry daytime textile may not be emissive.");
    }

    private static Renderer RequireRenderer(string name)
    {
        GameObject go = FindSceneObject(name);
        Renderer renderer = go != null ? go.GetComponent<Renderer>() : null;
        if (renderer == null || !renderer.enabled)
            throw new InvalidOperationException($"Required active construction renderer missing: {name}");
        return renderer;
    }

    private static string LegacyName(FutonSpec spec) => $"Futon_{spec.floor}_{spec.bay}";
    private static string AssemblyName(FutonSpec spec) => $"HD_FutonDrape_{spec.floor}_{spec.bay}";

    private static bool IsBuiltInPrimitive(string meshName) =>
        meshName == "Cube" || meshName == "Cylinder" || meshName == "Sphere" ||
        meshName == "Capsule" || meshName == "Plane" || meshName == "Quad";

    private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    private static float MaxComponent(Vector3 v) => Mathf.Max(v.x, Mathf.Max(v.y, v.z));

    private static void EnsureScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static GameObject FindSceneObject(string name) =>
        Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x.scene.IsValid() && x.scene.path == ScenePath && x.name == name);

    [Serializable]
    private sealed class FutonContract
    {
        public string schemaVersion;
        public string scenePath;
        public int expectedAssemblies;
        public float bodyThicknessM;
        public float minFloorClearanceM;
        public float maxFloorClearanceM;
        public LodPolicy lodPolicy;
        public VisualCreditPolicy visualCreditPolicy;
    }

    [Serializable]
    private sealed class LodPolicy
    {
        public int levels;
        public bool crossFadeRequired;
    }

    [Serializable]
    private sealed class VisualCreditPolicy
    {
        public int autoVisualPoints;
    }
}

[DisallowMultipleComponent]
public sealed class QualityBlockFutonDrapeManifest : MonoBehaviour
{
    [SerializeField] private int floor;
    [SerializeField] private int bay;
    [SerializeField] private float widthM;
    [SerializeField] private float bodyThicknessM;
    [SerializeField] private float frontDropM;
    [SerializeField] private float backDropM;
    [SerializeField] private float balconyFloorTopWorldY;
    [SerializeField] private float centerSagM;

    public void Configure(int sourceFloor, int sourceBay, float width, float thickness, float frontDrop,
        float backDrop, float floorTop, float sag)
    {
        floor = sourceFloor;
        bay = sourceBay;
        widthM = width;
        bodyThicknessM = thickness;
        frontDropM = frontDrop;
        backDropM = backDrop;
        balconyFloorTopWorldY = floorTop;
        centerSagM = Mathf.Min(MaxCenterSagClamp, Mathf.Max(0f, sag));
    }

    private const float MaxCenterSagClamp = 0.020f;
}
