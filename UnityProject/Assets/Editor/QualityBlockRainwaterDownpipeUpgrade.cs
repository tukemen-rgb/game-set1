using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Replaces the legacy repeated RainGutter primitive cylinders with a physically assembled,
/// authored-mesh vertical rainwater downpipe. This pass is deliberately scoped to the visible
/// vertical riser; it does not invent an eave collector or underground termination.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockRainwaterDownpipeUpgrade
{
    public const string RootName = "DanchiRainwaterDrainage";
    public const string PvcMaterialName = "MAT_AgedRainwaterPVC";
    public const string ClampMaterialName = "MAT_RainwaterClampGalvanized";

    private const string GeneratedRoot = "Assets/Generated/QualityBlockRainwater";
    private const string MaterialRoot = GeneratedRoot + "/Materials";
    private const string TextureRoot = GeneratedRoot + "/Textures";
    private const string PvcMaterialPath = MaterialRoot + "/MAT_AgedRainwaterPVC.mat";
    private const string ClampMaterialPath = MaterialRoot + "/MAT_RainwaterClampGalvanized.mat";
    private const string PvcNormalPath = TextureRoot + "/T_RainwaterPVC_Normal.png";
    private const string PvcMetalSmoothPath = TextureRoot + "/T_RainwaterPVC_MetalSmooth.png";

    private const float PipeDiameter = 0.075f;
    private const float PipeHeight = 13.50f;
    private const float PipeCenterY = 7.00f;
    private const float PipeX = -9.55f;
    private const float PipeZ = 3.79f;
    private const float SocketDiameter = 0.092f;
    private const float SocketHeight = 0.13f;
    private const float SupportBandDiameter = 0.094f;
    private const float SupportBandHeight = 0.018f;

    private static readonly float[] SocketY = { 2.8f, 5.6f, 8.4f, 11.2f };
    private static readonly float[] SupportY = { 0.9f, 2.25f, 3.6f, 4.95f, 6.3f, 7.65f, 9.0f, 10.35f, 11.7f, 13.05f };

    static QualityBlockRainwaterDownpipeUpgrade()
    {
        Camera.onPreCull -= OnCameraPreCull;
        Camera.onPreCull += OnCameraPreCull;
    }

    [MenuItem("Tools/Quality Block/Generate + Apply Physical Rainwater Downpipe")]
    public static void GenerateAssetsAndApply()
    {
        EnsureGeneratedAssets();
        ApplyToOpenScene();
        AssetDatabase.SaveAssets();
        if (SceneManager.GetActiveScene().IsValid() && SceneManager.GetActiveScene().isLoaded)
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        QualityBlockRainwaterDownpipeConstructionQA.ValidateOpenScene(false);
    }

    public static void EnsureGeneratedAssets()
    {
        EnsureFolder(GeneratedRoot);
        EnsureFolder(MaterialRoot);
        EnsureFolder(TextureRoot);
        EnsurePvcTextures();

        Material pvc = EnsureMaterial(PvcMaterialPath, PvcMaterialName, new Color(0.43f, 0.42f, 0.39f, 1f), 0f, 0.37f);
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(PvcNormalPath);
        Texture2D metalSmooth = AssetDatabase.LoadAssetAtPath<Texture2D>(PvcMetalSmoothPath);
        if (pvc.HasProperty("_BumpMap")) pvc.SetTexture("_BumpMap", normal);
        if (pvc.HasProperty("_BumpScale")) pvc.SetFloat("_BumpScale", 0.35f);
        if (pvc.HasProperty("_MetallicGlossMap")) pvc.SetTexture("_MetallicGlossMap", metalSmooth);
        if (pvc.HasProperty("_GlossMapScale")) pvc.SetFloat("_GlossMapScale", 1f);
        pvc.EnableKeyword("_NORMALMAP");
        pvc.EnableKeyword("_METALLICGLOSSMAP");
        pvc.DisableKeyword("_EMISSION");
        if (pvc.HasProperty("_EmissionColor")) pvc.SetColor("_EmissionColor", Color.black);
        EditorUtility.SetDirty(pvc);

        Material clamp = EnsureMaterial(ClampMaterialPath, ClampMaterialName, new Color(0.31f, 0.31f, 0.30f, 1f), 0.80f, 0.36f);
        clamp.DisableKeyword("_EMISSION");
        if (clamp.HasProperty("_EmissionColor")) clamp.SetColor("_EmissionColor", Color.black);
        EditorUtility.SetDirty(clamp);
    }

    public static void ApplyToOpenScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.name != "QualityBlock1990s")
            throw new InvalidOperationException("Open QualityBlock1990s before applying the rainwater downpipe upgrade.");

        GameObject danchi = GameObject.Find("Danchi");
        if (danchi == null) throw new InvalidOperationException("Danchi root not found.");

        EnsureGeneratedAssets();
        DisableLegacyRainGutter(danchi.transform);

        Transform old = danchi.transform.Find(RootName);
        if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);

        var root = new GameObject(RootName);
        root.transform.SetParent(danchi.transform, false);
        Material pvc = AssetDatabase.LoadAssetAtPath<Material>(PvcMaterialPath);
        Material clamp = AssetDatabase.LoadAssetAtPath<Material>(ClampMaterialPath);
        if (pvc == null || clamp == null) throw new InvalidOperationException("Rainwater materials were not generated.");

        var lodGroup = root.AddComponent<LODGroup>();
        lodGroup.fadeMode = LODFadeMode.CrossFade;
        lodGroup.animateCrossFading = true;

        var lods = new LOD[4];
        lods[0] = new LOD(0.18f, BuildLod(root.transform, "LOD0", 0, pvc, clamp).ToArray());
        lods[1] = new LOD(0.08f, BuildLod(root.transform, "LOD1", 1, pvc, clamp).ToArray());
        lods[2] = new LOD(0.03f, BuildLod(root.transform, "LOD2", 2, pvc, clamp).ToArray());
        lods[3] = new LOD(0.008f, BuildLod(root.transform, "LOD3", 3, pvc, clamp).ToArray());
        lodGroup.SetLODs(lods);
        lodGroup.RecalculateBounds();

        QualityBlockPeriodMetadataRegistry.ApplyOrRefresh();
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static List<Renderer> BuildLod(Transform parent, string name, int level, Material pvc, Material clamp)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        var renderers = new List<Renderer>();

        renderers.Add(CreateCylinder(root.transform, "PipeBody", PipeDiameter, PipeHeight,
            new Vector3(PipeX, PipeCenterY, PipeZ), pvc, false));

        if (level <= 2)
        {
            foreach (float y in SocketY)
                renderers.Add(CreateCylinder(root.transform, "SocketSleeve", SocketDiameter, SocketHeight,
                    new Vector3(PipeX, y, PipeZ), pvc, false));
        }

        if (level <= 2)
        {
            foreach (float y in SupportY)
            {
                renderers.Add(CreateCylinder(root.transform, "SupportBand", SupportBandDiameter, SupportBandHeight,
                    new Vector3(PipeX, y, PipeZ), clamp, false));

                if (level <= 1)
                {
                    renderers.Add(CreateBox(root.transform, "StandOff", new Vector3(0.024f, 0.024f, 0.072f),
                        new Vector3(PipeX, y, 3.742f), clamp));
                    renderers.Add(CreateBox(root.transform, "WallPlate", new Vector3(0.060f, 0.050f, 0.006f),
                        new Vector3(PipeX, y, 3.704f), clamp));
                }

                if (level == 0)
                {
                    renderers.Add(CreateFastener(root.transform, "AnchorHead_L", new Vector3(PipeX - 0.018f, y, 3.709f), clamp));
                    renderers.Add(CreateFastener(root.transform, "AnchorHead_R", new Vector3(PipeX + 0.018f, y, 3.709f), clamp));
                }
            }
        }
        return renderers;
    }

    private static MeshRenderer CreateCylinder(Transform parent, string name, float diameter, float height, Vector3 position, Material material, bool fastener)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = QualityBlockDetailMeshLibrary.GetBeveledCylinder(new Vector3(diameter, height * 0.5f, diameter), fastener);
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        return renderer;
    }

    private static MeshRenderer CreateBox(Transform parent, string name, Vector3 size, Vector3 position, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = QualityBlockDetailMeshLibrary.GetChamferedBox(size);
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        return renderer;
    }

    private static MeshRenderer CreateFastener(Transform parent, string name, Vector3 position, Material material)
    {
        MeshRenderer renderer = CreateCylinder(parent, name, 0.010f, 0.006f, position, material, true);
        renderer.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        return renderer;
    }

    private static void DisableLegacyRainGutter(Transform danchi)
    {
        foreach (Transform t in danchi.GetComponentsInChildren<Transform>(true))
        {
            if (!string.Equals(t.name, "RainGutter", StringComparison.Ordinal)) continue;
            foreach (Renderer r in t.GetComponents<Renderer>()) r.enabled = false;
            foreach (Collider c in t.GetComponents<Collider>()) c.enabled = false;
        }
    }

    private static Material EnsureMaterial(string path, string name, Color color, float metallic, float smoothness)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("Standard shader unavailable.");
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
        return material;
    }

    private static void EnsurePvcTextures()
    {
        if (!File.Exists(PvcNormalPath) || !File.Exists(PvcMetalSmoothPath))
        {
            const int n = 512;
            var normal = new Texture2D(n, n, TextureFormat.RGBA32, false, true);
            var metalSmooth = new Texture2D(n, n, TextureFormat.RGBA32, false, true);
            var normalPixels = new Color32[n * n];
            var msPixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = x / (float)n;
                float v = y / (float)n;
                float gx = 0.018f * Mathf.Sin(u * Mathf.PI * 54f) + 0.007f * Mathf.Sin((u + v * 0.07f) * Mathf.PI * 131f);
                float gy = 0.004f * Mathf.Sin(v * Mathf.PI * 23f + u * 5f);
                Vector3 nn = new Vector3(-gx, -gy, 1f).normalized;
                normalPixels[y * n + x] = new Color32((byte)((nn.x * 0.5f + 0.5f) * 255f), (byte)((nn.y * 0.5f + 0.5f) * 255f), (byte)((nn.z * 0.5f + 0.5f) * 255f), 255);
                float smooth = 0.37f + 0.025f * Mathf.Sin(u * 31f + v * 17f) + 0.012f * Mathf.Sin(u * 83f - v * 47f);
                msPixels[y * n + x] = new Color32(0, 0, 0, (byte)(Mathf.Clamp01(smooth) * 255f));
            }
            normal.SetPixels32(normalPixels); normal.Apply();
            metalSmooth.SetPixels32(msPixels); metalSmooth.Apply();
            File.WriteAllBytes(PvcNormalPath, normal.EncodeToPNG());
            File.WriteAllBytes(PvcMetalSmoothPath, metalSmooth.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(normal);
            UnityEngine.Object.DestroyImmediate(metalSmooth);
            AssetDatabase.ImportAsset(PvcNormalPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(PvcMetalSmoothPath, ImportAssetOptions.ForceSynchronousImport);
        }
        ConfigureTexture(PvcNormalPath, true);
        ConfigureTexture(PvcMetalSmoothPath, false);
    }

    private static void ConfigureTexture(string path, bool normalMap)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("TextureImporter unavailable for " + path);
        importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = false;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 8;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.crunchedCompression = false;
        if (!normalMap) importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.SaveAndReimport();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = Path.GetFileName(path);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    private static void OnCameraPreCull(Camera camera)
    {
        if (camera == null || !IsFormalCamera(camera)) return;
        QualityBlockRainwaterDownpipeConstructionQA.ValidateOpenScene(true);
    }

    private static bool IsFormalCamera(Camera camera)
    {
        if (camera.cameraType == CameraType.Reflection) return true;
        string n = camera.name ?? string.Empty;
        return n.StartsWith("QA4K_", StringComparison.Ordinal) ||
               n.StartsWith("QATemporal_", StringComparison.Ordinal) ||
               n.StartsWith("QAPreparedTemporal_", StringComparison.Ordinal);
    }
}
