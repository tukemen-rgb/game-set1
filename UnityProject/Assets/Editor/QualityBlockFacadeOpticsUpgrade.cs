using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Replaces the generated fallback's opaque blue-gray window blocks with a physically separated
/// optical stack: clear dielectric glass in front of a non-emissive interior/curtain backing.
/// It also gives the hero concrete facade a two-scale PBR coating with metric-aware front-face
/// tiling so 4K crops do not read as a single flat color or one giant texture tile.
///
/// This pass intentionally targets the generated fallback only. Authored art remains authoritative.
/// No gameplay colliders are added or removed; the hidden base Window_* renderers retain any
/// existing colliders while generated optical surfaces are render-only.
/// </summary>
public static class QualityBlockFacadeOpticsUpgrade
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string RootName = "DanchiFacadeOptics";
    private const string AssetRoot = "Assets/Art/GeneratedFacadeOptics";
    private const string MeshRoot = "Assets/Art/GeneratedFacadeOpticsMeshes";
    private const int TextureSize = 1024;

    private const float ApartmentPaneWidth = 1.03f;
    private const float ApartmentPaneHeight = 1.46f;
    private const float ApartmentPaneHalfOffsetX = 0.53f;
    private const float SlidingPaneDepthSeparation = 0.014f;
    private const float InteriorBackingDepth = 0.18f;

    [MenuItem("NewTown/Materials/Build Facade Concrete and Glazing Optics")]
    public static void BuildAndApply()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() ||
            EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var slot = Resources.FindObjectsOfTypeAll<QualityBlockArtSlot>()
            .FirstOrDefault(x => x.gameObject.scene.IsValid() && x.SlotId == "danchi.main");
        if (slot != null && slot.IsUsingAuthoredArt)
        {
            Debug.Log("Danchi authored replacement is active; generated facade optics pass was skipped.");
            return;
        }

        Directory.CreateDirectory(AssetRoot);
        Directory.CreateDirectory(MeshRoot);

        GenerateFacadeTextureSet();
        Material facadeMain = BuildFacadeMaterial(
            "MAT_FacadePaintedRC_Main",
            new Vector2(26.0f / 2.4f, 13.2f / 2.4f),
            new Vector2(26.0f / 0.22f, 13.2f / 0.22f));
        Material facadeStair = BuildFacadeMaterial(
            "MAT_FacadePaintedRC_Stair",
            new Vector2(3.2f / 2.4f, 13.8f / 2.4f),
            new Vector2(3.2f / 0.22f, 13.8f / 0.22f));
        Material glass = BuildGlassMaterial();
        Material[] backing = BuildBackingMaterials();
        Mesh plane = GetOrCreatePlaneMesh();

        var danchi = FindSceneObject("Danchi");
        if (danchi == null)
            throw new InvalidOperationException("Danchi fallback root not found for facade optics pass.");

        var old = FindSceneObject(RootName);
        if (old != null)
            UnityEngine.Object.DestroyImmediate(old);

        AssignFacadeMaterial("MainBlock", facadeMain);
        AssignFacadeMaterial("StairTower", facadeStair);

        var root = new GameObject(RootName);
        root.transform.SetParent(danchi.transform, false);

        int apartmentWindowCount = 0;
        int stairWindowCount = 0;
        int paneCount = 0;
        int backingCount = 0;

        for (int floor = 0; floor < 5; floor++)
        {
            for (int bay = 0; bay < 6; bay++)
            {
                var baseWindow = FindSceneObject($"Window_{floor}_{bay}");
                if (baseWindow == null)
                    throw new InvalidOperationException($"Base apartment window missing: Window_{floor}_{bay}");

                DisableBaseWindowRenderer(baseWindow);
                Vector3 p = baseWindow.transform.localPosition;

                // Two sash leaves occupy physically different depth planes. Both surfaces face
                // outward (+Z in this fallback) and sit behind the aluminum frame face.
                AddOpticalPlane(
                    $"FO_Glass_{floor}_{bay}_L",
                    root.transform,
                    p + new Vector3(-ApartmentPaneHalfOffsetX, 0f, 0.030f),
                    new Vector2(ApartmentPaneWidth, ApartmentPaneHeight),
                    glass,
                    plane);
                AddOpticalPlane(
                    $"FO_Glass_{floor}_{bay}_R",
                    root.transform,
                    p + new Vector3(ApartmentPaneHalfOffsetX, 0f, 0.030f - SlidingPaneDepthSeparation),
                    new Vector2(ApartmentPaneWidth, ApartmentPaneHeight),
                    glass,
                    plane);
                paneCount += 2;

                // The interior plane is deliberately behind the glazing rather than encoded in
                // the glass albedo. A deterministic bay palette prevents thirty identical black holes.
                int variant = PositiveHash($"{floor}:{bay}:interior") % backing.Length;
                AddOpticalPlane(
                    $"FO_InteriorBacking_{floor}_{bay}",
                    root.transform,
                    p + new Vector3(0f, 0f, -InteriorBackingDepth),
                    new Vector2(2.06f, 1.45f),
                    backing[variant],
                    plane);
                backingCount++;
                apartmentWindowCount++;
            }
        }

        for (int floor = 0; floor < 5; floor++)
        {
            var baseWindow = FindSceneObject($"StairWindow_{floor}");
            if (baseWindow == null)
                throw new InvalidOperationException($"Base stair window missing: StairWindow_{floor}");

            DisableBaseWindowRenderer(baseWindow);
            Vector3 p = baseWindow.transform.localPosition;
            AddOpticalPlane(
                $"FO_StairGlass_{floor}",
                root.transform,
                p + new Vector3(0f, 0f, 0.026f),
                new Vector2(1.12f, 1.18f),
                glass,
                plane);
            AddOpticalPlane(
                $"FO_StairBacking_{floor}",
                root.transform,
                p + new Vector3(0f, 0f, -0.14f),
                new Vector2(1.10f, 1.16f),
                backing[(floor + 1) % backing.Length],
                plane);
            paneCount++;
            backingCount++;
            stairWindowCount++;
        }

        var manifest = root.AddComponent<QualityBlockFacadeOpticsManifest>();
        manifest.Configure(apartmentWindowCount, stairWindowCount, paneCount, backingCount,
            SlidingPaneDepthSeparation, InteriorBackingDepth);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log(
            $"Facade optics applied: apartmentWindows={apartmentWindowCount}, stairWindows={stairWindowCount}, " +
            $"glassPanes={paneCount}, backingPlanes={backingCount}. Unity render verification is still required.");
    }

    [MenuItem("NewTown/QA/Validate Facade Optics")]
    public static void ValidateOpenScene()
    {
        var root = FindSceneObject(RootName);
        if (root == null)
            throw new InvalidOperationException("DanchiFacadeOptics is missing.");

        var manifest = root.GetComponent<QualityBlockFacadeOpticsManifest>();
        if (manifest == null)
            throw new InvalidOperationException("Facade optics manifest is missing.");
        if (manifest.ApartmentWindowCount != 30 || manifest.StairWindowCount != 5)
            throw new InvalidOperationException(
                $"Facade optics window counts invalid: apartments={manifest.ApartmentWindowCount}, stair={manifest.StairWindowCount}.");
        if (manifest.GlassPaneCount != 65)
            throw new InvalidOperationException($"Expected 65 generated glass panes, got {manifest.GlassPaneCount}.");
        if (manifest.BackingPlaneCount != 35)
            throw new InvalidOperationException($"Expected 35 interior backing planes, got {manifest.BackingPlaneCount}.");
        if (Mathf.Abs(manifest.SlidingPaneDepthSeparationMeters - SlidingPaneDepthSeparation) > 0.0001f)
            throw new InvalidOperationException("Sliding-pane depth separation differs from the facade optics contract.");

        int enabledOpaqueBaseWindows = 0;
        foreach (var r in Resources.FindObjectsOfTypeAll<MeshRenderer>())
        {
            if (!r.gameObject.scene.IsValid()) continue;
            string n = r.gameObject.name;
            if ((n.StartsWith("Window_", StringComparison.Ordinal) ||
                 n.StartsWith("StairWindow_", StringComparison.Ordinal)) && r.enabled)
                enabledOpaqueBaseWindows++;
        }
        if (enabledOpaqueBaseWindows != 0)
            throw new InvalidOperationException(
                $"{enabledOpaqueBaseWindows} opaque base window renderers remain enabled behind the optical stack.");

        var panes = Resources.FindObjectsOfTypeAll<GameObject>()
            .Where(x => x.scene.IsValid() &&
                (x.name.StartsWith("FO_Glass_", StringComparison.Ordinal) ||
                 x.name.StartsWith("FO_StairGlass_", StringComparison.Ordinal)))
            .ToArray();
        if (panes.Length != 65)
            throw new InvalidOperationException($"Expected 65 facade optical glass objects, found {panes.Length}.");
        if (panes.Any(x => x.GetComponent<Collider>() != null))
            throw new InvalidOperationException("Generated glazing must be render-only and may not add gameplay colliders.");

        Material glass = AssetDatabase.LoadAssetAtPath<Material>($"{AssetRoot}/MAT_WindowClearGlass.mat");
        if (glass == null)
            throw new InvalidOperationException("Clear glass material is missing.");
        if (glass.shader == null || glass.shader.name != "Standard")
            throw new InvalidOperationException("Generated fallback glazing must use the built-in Standard shader contract.");
        if (glass.GetFloat("_Metallic") > 0.001f)
            throw new InvalidOperationException("Glass metallic value must remain zero.");
        if (glass.GetFloat("_Glossiness") < 0.82f || glass.GetFloat("_Glossiness") > 0.94f)
            throw new InvalidOperationException("Glass smoothness is outside the dry clear-glass fallback range.");
        if (!glass.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON") || glass.IsKeywordEnabled("_ALPHABLEND_ON"))
            throw new InvalidOperationException("Glass must use premultiplied Transparent mode, not Fade transparency.");
        if (glass.GetInt("_ZWrite") != 0)
            throw new InvalidOperationException("Transparent glass must not write the opaque depth buffer in this fallback.");
        if (glass.renderQueue < (int)RenderQueue.Transparent)
            throw new InvalidOperationException("Glass render queue is not transparent.");
        if (glass.IsKeywordEnabled("_EMISSION") || glass.GetColor("_EmissionColor").maxColorComponent > 0.001f)
            throw new InvalidOperationException("Generated daytime glass may not be emissive.");

        foreach (string path in new[]
        {
            $"{AssetRoot}/MAT_FacadePaintedRC_Main.mat",
            $"{AssetRoot}/MAT_FacadePaintedRC_Stair.mat"
        })
        {
            Material concrete = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (concrete == null)
                throw new InvalidOperationException($"Facade concrete material missing: {path}");
            if (concrete.GetFloat("_Metallic") > 0.001f)
                throw new InvalidOperationException($"Facade concrete became metallic: {path}");
            if (!concrete.IsKeywordEnabled("_NORMALMAP") || !concrete.IsKeywordEnabled("_DETAIL_MULX2"))
                throw new InvalidOperationException($"Facade material is missing required primary/detail normal response: {path}");
        }

        string contractPath = "Assets/QA/facade_optics_contract.json";
        if (!File.Exists(contractPath))
            throw new InvalidOperationException("Facade optics construction/material contract is missing.");
        string contractText = File.ReadAllText(contractPath);
        foreach (string required in new[]
        {
            "clear_float_glass",
            "facade_painted_rc",
            "nominalThicknessMm",
            "specularF0",
            "grazing",
            "automaticFailConditions"
        })
            if (!contractText.Contains(required))
                throw new InvalidOperationException($"Facade optics contract is missing required metadata token: {required}");

        Debug.Log(
            "Facade optics structural/material QA passed. This is implementation evidence only; " +
            "actual Fresnel, transparency sorting, reflection balance and 4K plausibility still require a Unity render.");
    }

    private static void GenerateFacadeTextureSet()
    {
        int n = TextureSize * TextureSize;
        var albedo = new Color32[n];
        var normal = new Color32[n];
        var mask = new Color32[n];
        var detailNormal = new Color32[n];
        var heights = new float[n];
        var detailHeights = new float[n];

        for (int y = 0; y < TextureSize; y++)
        {
            float v = y / (float)TextureSize;
            for (int x = 0; x < TextureSize; x++)
            {
                float u = x / (float)TextureSize;
                int i = y * TextureSize + x;

                float macro = PeriodicFbm(u, v, 905, 6);
                float aggregate = Aggregate(u, v, 1241);
                float micro = PeriodicFbm(u, v, 1709, 3);
                float h = Mathf.Clamp01(macro * 0.28f + aggregate * 0.52f + micro * 0.20f);
                heights[i] = h;
                detailHeights[i] = Mathf.Clamp01(Aggregate(u, v, 2617) * 0.68f + PeriodicFbm(u, v, 3023, 2) * 0.32f);

                // Albedo carries material color variation only. No ledge shadow, sky reflection,
                // rain streak, or directional highlight is painted into the texture.
                float low = (macro - 0.5f) * 0.065f;
                float grain = (aggregate - 0.5f) * 0.030f;
                Color baseColor = new Color(0.63f, 0.62f, 0.58f);
                Color c = baseColor * (1f + low + grain);
                albedo[i] = new Color(
                    Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), 1f);

                float roughness = Mathf.Lerp(0.79f, 0.93f,
                    Mathf.Clamp01(0.55f * macro + 0.45f * (1f - aggregate)));
                byte smooth = (byte)Mathf.RoundToInt((1f - roughness) * 255f);
                mask[i] = new Color32(0, 0, 0, smooth);
            }
        }

        BuildNormalPixels(heights, normal, 2.0f);
        BuildNormalPixels(detailHeights, detailNormal, 1.25f);

        WriteTexture($"{AssetRoot}/FacadeRC_Albedo.png", albedo, true, false);
        WriteTexture($"{AssetRoot}/FacadeRC_Normal.png", normal, false, true);
        WriteTexture($"{AssetRoot}/FacadeRC_MetallicSmoothness.png", mask, false, false);
        WriteTexture($"{AssetRoot}/FacadeRC_DetailNormal.png", detailNormal, false, true);
    }

    private static Material BuildFacadeMaterial(string name, Vector2 macroScale, Vector2 detailScale)
    {
        Shader shader = Shader.Find("Standard");
        if (shader == null)
            throw new InvalidOperationException("Standard shader not found for facade optics pass.");

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

        mat.color = Color.white;
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Glossiness", 0.14f);
        mat.SetFloat("_GlossMapScale", 1f);
        mat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>($"{AssetRoot}/FacadeRC_Albedo.png"));
        mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{AssetRoot}/FacadeRC_Normal.png"));
        mat.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{AssetRoot}/FacadeRC_MetallicSmoothness.png"));
        mat.SetTexture("_DetailNormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{AssetRoot}/FacadeRC_DetailNormal.png"));
        mat.SetFloat("_BumpScale", 0.82f);
        mat.SetFloat("_DetailNormalMapScale", 0.42f);
        mat.SetTextureScale("_MainTex", macroScale);
        mat.SetTextureScale("_BumpMap", macroScale);
        mat.SetTextureScale("_MetallicGlossMap", macroScale);
        mat.SetTextureScale("_DetailNormalMap", detailScale);
        mat.EnableKeyword("_NORMALMAP");
        mat.EnableKeyword("_METALLICGLOSSMAP");
        mat.EnableKeyword("_DETAIL_MULX2");
        mat.DisableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", Color.black);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material BuildGlassMaterial()
    {
        Shader shader = Shader.Find("Standard");
        if (shader == null)
            throw new InvalidOperationException("Standard shader not found for clear glazing.");

        string path = $"{AssetRoot}/MAT_WindowClearGlass.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader) { name = "MAT_WindowClearGlass" };
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = shader;
        }

        // Standard/Transparent keeps specular reflections while alpha controls transmitted body color.
        // Fresnel is produced by the Standard shader's dielectric response and smoothness, not painted art.
        mat.SetFloat("_Mode", 3f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetInt("_SrcBlend", (int)BlendMode.One);
        mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_ALPHABLEND_ON");
        mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = (int)RenderQueue.Transparent;
        mat.color = new Color(0.72f, 0.81f, 0.84f, 0.18f);
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Glossiness", 0.88f);
        mat.SetFloat("_GlossMapScale", 0.88f);
        mat.DisableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", Color.black);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material[] BuildBackingMaterials()
    {
        Color[] colors =
        {
            new Color(0.18f, 0.19f, 0.18f),
            new Color(0.43f, 0.40f, 0.34f),
            new Color(0.54f, 0.52f, 0.46f),
            new Color(0.31f, 0.34f, 0.35f),
        };

        var result = new Material[colors.Length];
        Shader shader = Shader.Find("Standard");
        if (shader == null)
            throw new InvalidOperationException("Standard shader not found for interior backing.");

        for (int i = 0; i < colors.Length; i++)
        {
            string path = $"{AssetRoot}/MAT_InteriorBacking_{i}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = $"MAT_InteriorBacking_{i}" };
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }
            mat.color = colors[i];
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Glossiness", 0.06f + i * 0.01f);
            mat.DisableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.black);
            EditorUtility.SetDirty(mat);
            result[i] = mat;
        }
        return result;
    }

    private static Mesh GetOrCreatePlaneMesh()
    {
        string path = $"{MeshRoot}/GM_FacadeOpticalPlane.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh != null) return mesh;

        var vertices = new List<Vector3>
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3( 0.5f, -0.5f, 0f),
            new Vector3( 0.5f,  0.5f, 0f),
            new Vector3(-0.5f,  0.5f, 0f),
        };
        var triangles = new List<int> { 0, 1, 2, 0, 2, 3 };
        var uv = new List<Vector2>
        {
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(1f, 1f), new Vector2(0f, 1f),
        };

        mesh = new Mesh { name = "GM_FacadeOpticalPlane" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uv);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static GameObject AddOpticalPlane(string name, Transform parent, Vector3 localPosition,
        Vector2 size, Material material, Mesh mesh)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return go;
    }

    private static void DisableBaseWindowRenderer(GameObject go)
    {
        var renderer = go.GetComponent<MeshRenderer>();
        if (renderer == null)
            throw new InvalidOperationException($"Base window renderer missing: {go.name}");
        renderer.enabled = false;
    }

    private static void AssignFacadeMaterial(string objectName, Material material)
    {
        var go = FindSceneObject(objectName);
        if (go == null)
            throw new InvalidOperationException($"Facade object missing: {objectName}");
        var renderer = go.GetComponent<Renderer>();
        if (renderer == null)
            throw new InvalidOperationException($"Facade renderer missing: {objectName}");
        renderer.sharedMaterial = material;
    }

    private static void BuildNormalPixels(float[] heights, Color32[] target, float strength)
    {
        for (int y = 0; y < TextureSize; y++)
        {
            int ym = (y - 1 + TextureSize) % TextureSize;
            int yp = (y + 1) % TextureSize;
            for (int x = 0; x < TextureSize; x++)
            {
                int xm = (x - 1 + TextureSize) % TextureSize;
                int xp = (x + 1) % TextureSize;
                float dx = heights[y * TextureSize + xp] - heights[y * TextureSize + xm];
                float dy = heights[yp * TextureSize + x] - heights[ym * TextureSize + x];
                Vector3 n = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
                target[y * TextureSize + x] = new Color32(
                    (byte)Mathf.RoundToInt((n.x * 0.5f + 0.5f) * 255f),
                    (byte)Mathf.RoundToInt((n.y * 0.5f + 0.5f) * 255f),
                    (byte)Mathf.RoundToInt((n.z * 0.5f + 0.5f) * 255f), 255);
            }
        }
    }

    private static void WriteTexture(string path, Color32[] pixels, bool sRgb, bool normalMap)
    {
        var tex = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false, !sRgb);
        tex.SetPixels32(pixels);
        tex.Apply(false, false);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException($"Texture importer unavailable for {path}");
        importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = sRgb && !normalMap;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Trilinear;
        importer.mipmapEnabled = true;
        importer.anisoLevel = 12;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.SaveAndReimport();
    }

    private static float PeriodicFbm(float u, float v, int seed, int octaves)
    {
        float sum = 0f;
        float weight = 0f;
        float amplitude = 1f;
        float phase = (seed % 997) * 0.0131f;
        for (int octave = 0; octave < octaves; octave++)
        {
            int f = 1 << octave;
            float a = Mathf.Sin(Mathf.PI * 2f * (u * f + v * (f + 1)) + phase);
            float b = Mathf.Cos(Mathf.PI * 2f * (u * (f + 2) - v * f) + phase * 1.71f);
            sum += ((a + b) * 0.25f + 0.5f) * amplitude;
            weight += amplitude;
            amplitude *= 0.53f;
        }
        return Mathf.Clamp01(sum / Mathf.Max(0.0001f, weight));
    }

    private static float Aggregate(float u, float v, int seed)
    {
        float a = Mathf.Sin(Mathf.PI * 2f * (u * 43f + v * 47f) + seed * 0.011f);
        float b = Mathf.Cos(Mathf.PI * 2f * (u * 61f - v * 37f) + seed * 0.017f);
        float c = Mathf.Sin(Mathf.PI * 2f * (u * 83f + v * 71f) + seed * 0.007f);
        return Mathf.Clamp01(0.5f + a * b * 0.27f + c * 0.12f);
    }

    private static int PositiveHash(string text)
    {
        unchecked
        {
            int h = 23;
            for (int i = 0; i < text.Length; i++) h = h * 31 + text[i];
            return h & 0x7fffffff;
        }
    }

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x.scene.IsValid() && x.name == name);
    }
}

[DisallowMultipleComponent]
public sealed class QualityBlockFacadeOpticsManifest : MonoBehaviour
{
    [SerializeField] private int apartmentWindowCount;
    [SerializeField] private int stairWindowCount;
    [SerializeField] private int glassPaneCount;
    [SerializeField] private int backingPlaneCount;
    [SerializeField] private float slidingPaneDepthSeparationMeters;
    [SerializeField] private float interiorBackingDepthMeters;

    public int ApartmentWindowCount => apartmentWindowCount;
    public int StairWindowCount => stairWindowCount;
    public int GlassPaneCount => glassPaneCount;
    public int BackingPlaneCount => backingPlaneCount;
    public float SlidingPaneDepthSeparationMeters => slidingPaneDepthSeparationMeters;
    public float InteriorBackingDepthMeters => interiorBackingDepthMeters;

    public void Configure(int apartmentWindows, int stairWindows, int panes, int backings,
        float paneDepthSeparation, float backingDepth)
    {
        apartmentWindowCount = apartmentWindows;
        stairWindowCount = stairWindows;
        glassPaneCount = panes;
        backingPlaneCount = backings;
        slidingPaneDepthSeparationMeters = paneDepthSeparation;
        interiorBackingDepthMeters = backingDepth;
    }
}
