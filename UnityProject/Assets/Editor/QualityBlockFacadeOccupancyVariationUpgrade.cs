using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Breaks the synthetic "thirty identical dark rectangles" failure mode without randomizing the
/// manufactured exterior facade. The repeated sash/window construction stays regular; variation is
/// placed where real occupied apartments vary: curtain opening, cloth family, hem length and slight
/// left/right gathering asymmetry. Curtain cloth sits physically between dielectric glazing and the
/// existing non-emissive room backing, with a coated-aluminium top track and explicit LOD0-3.
///
/// LOD0 uses a metre-UV pleated cloth mesh so grazing reflections/shading come from geometry + PBR,
/// not painted folds. Lower LODs use the existing optical plane because weave/fold silhouette is
/// sub-pixel at those screen fractions. Everything is render-only; gameplay colliders are untouched.
/// </summary>
public static class QualityBlockFacadeOccupancyVariationUpgrade
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string RootName = "FacadeOccupancyVariation";
    private const string ContractPath = "Assets/QA/facade_window_dressing_contract.json";
    private const string AssetRoot = "Assets/Art/GeneratedFacadeOptics";
    private const string MeshRoot = "Assets/Art/GeneratedFacadeOpticsMeshes/Occupancy";
    private const string OpticalPlanePath = "Assets/Art/GeneratedFacadeOpticsMeshes/GM_FacadeOpticalPlane.asset";
    private const int TextureSize = 512;

    private const float WindowWidth = 2.06f;
    private const float WindowHeight = 1.45f;
    private const float CurtainZ = -0.105f;
    private const float TrackZ = -0.112f;
    private const float ExistingBackingZ = -0.18f;
    private const float FabricTileMeters = 0.18f;
    private const float PleatPitchMeters = 0.055f;
    private const float PleatAmplitudeMeters = 0.012f;

    private static readonly float[] LodTransitions = { 0.22f, 0.10f, 0.045f, 0.018f };

    private static readonly Color[] CurtainPalette =
    {
        new Color(0.78f, 0.76f, 0.68f, 1f),
        new Color(0.70f, 0.68f, 0.61f, 1f),
        new Color(0.82f, 0.81f, 0.75f, 1f),
        new Color(0.62f, 0.64f, 0.63f, 1f),
    };

    [MenuItem("NewTown/Facade/Build Physical Apartment Curtain Variation")]
    public static void BuildAndApply()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        ApplyToOpenScene();
        ValidateOpenScene();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Physical apartment curtain variation applied. Visual Fidelity remains unscored until native 4K Unity evidence is reviewed.");
    }

    public static void ApplyToOpenScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        ValidateContract();

        QualityBlockArtSlot slot = Resources.FindObjectsOfTypeAll<QualityBlockArtSlot>()
            .FirstOrDefault(x => x.gameObject.scene.IsValid() && x.SlotId == "danchi.main");
        if (slot != null && slot.IsUsingAuthoredArt)
        {
            GameObject stale = FindSceneObject(RootName);
            if (stale != null) UnityEngine.Object.DestroyImmediate(stale);
            Debug.Log("Danchi authored replacement is active; generated apartment occupancy variation was skipped.");
            return;
        }

        GameObject optics = FindSceneObject("DanchiFacadeOptics");
        if (optics == null)
            throw new InvalidOperationException("Facade occupancy variation requires DanchiFacadeOptics to be built first.");

        Directory.CreateDirectory(AssetRoot);
        Directory.CreateDirectory(MeshRoot);
        EnsureFabricTextures();
        Material[] fabrics = BuildFabricMaterials();
        Material trackMaterial = BuildTrackMaterial();
        Mesh opticalPlane = AssetDatabase.LoadAssetAtPath<Mesh>(OpticalPlanePath);
        if (opticalPlane == null)
            throw new InvalidOperationException("Facade optical plane mesh is missing; run facade optics before occupancy variation.");
        Mesh trackMesh = GetOrCreateTrackMesh();

        GameObject previous = FindSceneObject(RootName);
        if (previous != null) UnityEngine.Object.DestroyImmediate(previous);

        GameObject root = new GameObject(RootName);
        root.transform.SetParent(optics.transform, false);

        for (int floor = 0; floor < 5; floor++)
        {
            for (int bay = 0; bay < 6; bay++)
            {
                Transform backing = FindDescendant(optics.transform, $"FO_InteriorBacking_{floor}_{bay}");
                if (backing == null)
                    throw new InvalidOperationException($"Interior backing missing for apartment {floor}/{bay}.");

                int palette = PositiveHash($"curtain:{floor}:{bay}:palette") % fabrics.Length;
                float gap = Mathf.Lerp(0.24f, 1.16f, Hash01(floor, bay, 101));
                float asymmetry = Mathf.Lerp(-0.16f, 0.16f, Hash01(floor, bay, 131));
                float available = WindowWidth - gap;
                float leftWidth = available * (0.5f + asymmetry * 0.5f);
                float rightWidth = available - leftWidth;
                float height = Mathf.Lerp(1.29f, 1.41f, Hash01(floor, bay, 167));
                float topOffset = Mathf.Lerp(-0.015f, 0.018f, Hash01(floor, bay, 191));

                // Clamp after deterministic variation so every leaf remains a plausible gathered panel.
                leftWidth = Mathf.Clamp(leftWidth, 0.25f, 0.93f);
                rightWidth = Mathf.Clamp(rightWidth, 0.25f, 0.93f);
                gap = Mathf.Max(0.20f, WindowWidth - leftWidth - rightWidth);

                GameObject assembly = new GameObject($"FO_Dressing_{floor}_{bay}");
                assembly.transform.SetParent(root.transform, false);
                assembly.transform.localPosition = new Vector3(backing.localPosition.x, backing.localPosition.y, 0f);

                float leftCenter = -WindowWidth * 0.5f + leftWidth * 0.5f;
                float rightCenter = WindowWidth * 0.5f - rightWidth * 0.5f;
                float topY = WindowHeight * 0.5f - 0.035f + topOffset;
                float centerY = topY - height * 0.5f;

                BuildLodSet(assembly.transform, floor, bay, leftWidth, rightWidth, height,
                    leftCenter, rightCenter, centerY, fabrics[palette], trackMaterial, opticalPlane, trackMesh);
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("NewTown/QA/Validate Physical Apartment Curtain Variation")]
    public static void ValidateOpenScene()
    {
        ValidateContract();

        QualityBlockArtSlot slot = Resources.FindObjectsOfTypeAll<QualityBlockArtSlot>()
            .FirstOrDefault(x => x.gameObject.scene.IsValid() && x.SlotId == "danchi.main");
        if (slot != null && slot.IsUsingAuthoredArt)
        {
            if (FindSceneObject(RootName) != null)
                throw new InvalidOperationException("Generated occupancy variation must not remain active behind authored danchi art.");
            return;
        }

        GameObject root = FindSceneObject(RootName);
        if (root == null)
            throw new InvalidOperationException("FacadeOccupancyVariation root is missing.");

        Transform[] assemblies = Enumerable.Range(0, 5)
            .SelectMany(floor => Enumerable.Range(0, 6).Select(bay => root.transform.Find($"FO_Dressing_{floor}_{bay}")))
            .ToArray();
        if (assemblies.Length != 30 || assemblies.Any(x => x == null))
            throw new InvalidOperationException("Facade occupancy variation must contain exactly thirty apartment dressing assemblies.");

        if (root.GetComponentsInChildren<Collider>(true).Length != 0)
            throw new InvalidOperationException("Apartment dressing is render-only and may not add gameplay colliders.");

        var signatures = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (Transform assembly in assemblies)
        {
            LODGroup group = assembly.GetComponent<LODGroup>();
            if (group == null)
                throw new InvalidOperationException($"{assembly.name} is missing its four-level LODGroup.");
            LOD[] lods = group.GetLODs();
            if (lods.Length != 4)
                throw new InvalidOperationException($"{assembly.name} expected four LODs, got {lods.Length}.");
            if (group.fadeMode != LODFadeMode.CrossFade || !group.animateCrossFading)
                throw new InvalidOperationException($"{assembly.name} must use animated LOD cross-fade.");

            Transform lod0 = assembly.Find("LOD0");
            if (lod0 == null) throw new InvalidOperationException($"{assembly.name} LOD0 root missing.");
            MeshRenderer left = lod0.Find("CurtainLeft")?.GetComponent<MeshRenderer>();
            MeshRenderer right = lod0.Find("CurtainRight")?.GetComponent<MeshRenderer>();
            MeshRenderer track = lod0.Find("CurtainTrack")?.GetComponent<MeshRenderer>();
            if (left == null || right == null || track == null)
                throw new InvalidOperationException($"{assembly.name} LOD0 must contain two cloth leaves plus track.");

            Mesh leftMesh = left.GetComponent<MeshFilter>()?.sharedMesh;
            Mesh rightMesh = right.GetComponent<MeshFilter>()?.sharedMesh;
            if (leftMesh == null || rightMesh == null ||
                !leftMesh.name.StartsWith("GM_CurtainPleat_", StringComparison.Ordinal) ||
                !rightMesh.name.StartsWith("GM_CurtainPleat_", StringComparison.Ordinal))
                throw new InvalidOperationException($"{assembly.name} LOD0 cloth must use dedicated pleated meshes, not primitives/flat placeholders.");

            Material cloth = left.sharedMaterial;
            if (cloth == null || cloth.shader == null || cloth.shader.name != "Standard")
                throw new InvalidOperationException($"{assembly.name} curtain cloth must use Standard PBR.");
            if (cloth.GetFloat("_Metallic") > 0.01f)
                throw new InvalidOperationException($"{assembly.name} curtain cloth is materially impossible metallic fabric.");
            if (cloth.GetFloat("_Glossiness") > 0.18f)
                throw new InvalidOperationException($"{assembly.name} curtain cloth is too smooth for the dry textile contract.");
            if (cloth.IsKeywordEnabled("_EMISSION") || cloth.GetColor("_EmissionColor").maxColorComponent > 0.001f)
                throw new InvalidOperationException($"{assembly.name} daytime curtain may not be emissive.");
            if (!cloth.IsKeywordEnabled("_NORMALMAP") || cloth.GetTexture("_BumpMap") == null)
                throw new InvalidOperationException($"{assembly.name} curtain cloth lacks textile normal microstructure.");

            Bounds lb = left.GetComponent<MeshFilter>().sharedMesh.bounds;
            Bounds rb = right.GetComponent<MeshFilter>().sharedMesh.bounds;
            float leftWidth = lb.size.x;
            float rightWidth = rb.size.x;
            float gap = WindowWidth - leftWidth - rightWidth;
            if (leftWidth < 0.24f || rightWidth < 0.24f || gap < 0.19f || gap > 1.22f)
                throw new InvalidOperationException($"{assembly.name} curtain dimensions violate the opening contract: left={leftWidth:F3}, right={rightWidth:F3}, gap={gap:F3}.");

            string sig = $"{Quantize(leftWidth, 0.01f)}|{Quantize(rightWidth, 0.01f)}|{Quantize(lb.size.y, 0.01f)}|{cloth.name}";
            signatures[sig] = signatures.TryGetValue(sig, out int count) ? count + 1 : 1;
        }

        if (signatures.Count < 18)
            throw new InvalidOperationException($"Apartment dressing variation is too repetitive: only {signatures.Count}/30 unique visible layouts.");
        int maxRepeat = signatures.Values.Max();
        if (maxRepeat > 3)
            throw new InvalidOperationException($"One apartment dressing layout repeats {maxRepeat} times; maximum allowed before render review is 3.");

        Material trackMat = AssetDatabase.LoadAssetAtPath<Material>($"{AssetRoot}/MAT_CurtainTrackCoatedAluminium.mat");
        if (trackMat == null || trackMat.GetFloat("_Metallic") > 0.01f)
            throw new InvalidOperationException("Curtain track finish is a dielectric coating and may not be treated as exposed metal.");

        Debug.Log($"Apartment occupancy variation structural QA passed: 30 assemblies, {signatures.Count} unique LOD0 layouts, max exact layout repeat={maxRepeat}. Actual 4K repetition visibility remains render-unverified.");
    }

    private static void BuildLodSet(Transform assembly, int floor, int bay,
        float leftWidth, float rightWidth, float height, float leftCenter, float rightCenter, float centerY,
        Material fabric, Material trackMaterial, Mesh opticalPlane, Mesh trackMesh)
    {
        var lodRendererSets = new Renderer[4][];
        for (int level = 0; level < 4; level++)
        {
            GameObject lodRoot = new GameObject($"LOD{level}");
            lodRoot.transform.SetParent(assembly, false);
            var renderers = new List<Renderer>();

            if (level == 0)
            {
                Mesh leftMesh = BuildPleatedCurtainMeshAsset(floor, bay, "L", leftWidth, height, Hash01(floor, bay, 223));
                Mesh rightMesh = BuildPleatedCurtainMeshAsset(floor, bay, "R", rightWidth, height, Hash01(floor, bay, 227));
                renderers.Add(AddMesh("CurtainLeft", lodRoot.transform,
                    new Vector3(leftCenter, centerY, CurtainZ), leftMesh, Vector3.one, fabric));
                renderers.Add(AddMesh("CurtainRight", lodRoot.transform,
                    new Vector3(rightCenter, centerY, CurtainZ), rightMesh, Vector3.one, fabric));
                renderers.Add(AddMesh("CurtainTrack", lodRoot.transform,
                    new Vector3(0f, WindowHeight * 0.5f - 0.018f, TrackZ), trackMesh,
                    new Vector3(2.02f, 0.018f, 0.022f), trackMaterial));
            }
            else
            {
                renderers.Add(AddMesh("CurtainLeft", lodRoot.transform,
                    new Vector3(leftCenter, centerY, CurtainZ), opticalPlane,
                    new Vector3(leftWidth, height, 1f), fabric));
                renderers.Add(AddMesh("CurtainRight", lodRoot.transform,
                    new Vector3(rightCenter, centerY, CurtainZ), opticalPlane,
                    new Vector3(rightWidth, height, 1f), fabric));
                if (level == 1)
                    renderers.Add(AddMesh("CurtainTrack", lodRoot.transform,
                        new Vector3(0f, WindowHeight * 0.5f - 0.018f, TrackZ), trackMesh,
                        new Vector3(2.02f, 0.018f, 0.022f), trackMaterial));
            }
            lodRendererSets[level] = renderers.ToArray();
        }

        LODGroup group = assembly.gameObject.AddComponent<LODGroup>();
        group.fadeMode = LODFadeMode.CrossFade;
        group.animateCrossFading = true;
        group.SetLODs(new[]
        {
            new LOD(LodTransitions[0], lodRendererSets[0]),
            new LOD(LodTransitions[1], lodRendererSets[1]),
            new LOD(LodTransitions[2], lodRendererSets[2]),
            new LOD(LodTransitions[3], lodRendererSets[3]),
        });
        group.RecalculateBounds();
    }

    private static MeshRenderer AddMesh(string name, Transform parent, Vector3 localPosition,
        Mesh mesh, Vector3 scale, Material material)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = true;
        return renderer;
    }

    private static Mesh BuildPleatedCurtainMeshAsset(int floor, int bay, string side, float width, float height, float phase)
    {
        string path = $"{MeshRoot}/GM_CurtainPleat_{floor}_{bay}_{side}.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool create = mesh == null;
        if (create) mesh = new Mesh { name = $"GM_CurtainPleat_{floor}_{bay}_{side}" };
        else mesh.Clear();

        int segments = Mathf.Clamp(Mathf.CeilToInt(width / 0.045f), 8, 28);
        var vertices = new Vector3[(segments + 1) * 2];
        var uv = new Vector2[vertices.Length];
        var triangles = new int[segments * 6];

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float x = Mathf.Lerp(-width * 0.5f, width * 0.5f, t);
            float z = Mathf.Sin((x / PleatPitchMeters + phase) * Mathf.PI * 2f) * PleatAmplitudeMeters;
            int bottom = i * 2;
            int top = bottom + 1;
            vertices[bottom] = new Vector3(x, -height * 0.5f, z);
            vertices[top] = new Vector3(x, height * 0.5f, z);
            uv[bottom] = new Vector2((x + width * 0.5f) / FabricTileMeters + phase, 0f);
            uv[top] = new Vector2((x + width * 0.5f) / FabricTileMeters + phase, height / FabricTileMeters);
        }

        for (int i = 0; i < segments; i++)
        {
            int v = i * 2;
            int k = i * 6;
            triangles[k + 0] = v;
            triangles[k + 1] = v + 1;
            triangles[k + 2] = v + 3;
            triangles[k + 3] = v;
            triangles[k + 4] = v + 3;
            triangles[k + 5] = v + 2;
        }

        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        if (create) AssetDatabase.CreateAsset(mesh, path);
        else EditorUtility.SetDirty(mesh);
        return mesh;
    }

    private static Mesh GetOrCreateTrackMesh()
    {
        string path = $"{MeshRoot}/GM_CurtainTrackExtrusion.asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;

        Vector3[] v =
        {
            new(-0.5f,-0.5f,-0.5f), new(0.5f,-0.5f,-0.5f), new(0.5f,0.5f,-0.5f), new(-0.5f,0.5f,-0.5f),
            new(-0.5f,-0.5f, 0.5f), new(0.5f,-0.5f, 0.5f), new(0.5f,0.5f, 0.5f), new(-0.5f,0.5f, 0.5f),
        };
        int[] t =
        {
            0,2,1, 0,3,2, 4,5,6, 4,6,7,
            0,1,5, 0,5,4, 3,7,6, 3,6,2,
            0,4,7, 0,7,3, 1,2,6, 1,6,5,
        };
        Mesh mesh = new Mesh { name = "GM_CurtainTrackExtrusion" };
        mesh.vertices = v;
        mesh.triangles = t;
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static void EnsureFabricTextures()
    {
        string normalPath = $"{AssetRoot}/CurtainFabric_Normal.png";
        if (!File.Exists(normalPath))
        {
            var heights = new float[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            {
                float v = y / (float)TextureSize;
                for (int x = 0; x < TextureSize; x++)
                {
                    float u = x / (float)TextureSize;
                    float warp = Mathf.Sin(Mathf.PI * 2f * u * 96f) * 0.55f;
                    float weft = Mathf.Sin(Mathf.PI * 2f * v * 128f + 0.7f) * 0.34f;
                    float cross = Mathf.Sin(Mathf.PI * 2f * (u * 48f + v * 51f)) * 0.11f;
                    heights[y * TextureSize + x] = warp + weft + cross;
                }
            }
            Color32[] pixels = BuildNormalPixels(heights, 0.75f);
            WriteTexture(normalPath, pixels, false, true);
        }

        for (int p = 0; p < CurtainPalette.Length; p++)
        {
            string path = $"{AssetRoot}/CurtainFabric_Albedo_{p}.png";
            if (File.Exists(path)) continue;
            var pixels = new Color32[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            {
                float v = y / (float)TextureSize;
                for (int x = 0; x < TextureSize; x++)
                {
                    float u = x / (float)TextureSize;
                    float yarn = 0.012f * Mathf.Sin(Mathf.PI * 2f * (u * 91f + v * 3f + p * 0.17f));
                    float slub = 0.010f * Mathf.Sin(Mathf.PI * 2f * (u * 13f - v * 17f + p * 0.31f));
                    Color c = CurtainPalette[p] * (1f + yarn + slub);
                    pixels[y * TextureSize + x] = c;
                }
            }
            WriteTexture(path, pixels, true, false);
        }
    }

    private static Material[] BuildFabricMaterials()
    {
        Shader shader = Shader.Find("Standard");
        if (shader == null) throw new InvalidOperationException("Standard shader not found for curtain cloth.");
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{AssetRoot}/CurtainFabric_Normal.png");
        if (normal == null) throw new InvalidOperationException("Curtain normal texture failed to import.");

        var result = new Material[CurtainPalette.Length];
        for (int i = 0; i < result.Length; i++)
        {
            string path = $"{AssetRoot}/MAT_CurtainFabric_{i}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = $"MAT_CurtainFabric_{i}" };
                AssetDatabase.CreateAsset(mat, path);
            }
            else mat.shader = shader;

            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"{AssetRoot}/CurtainFabric_Albedo_{i}.png");
            if (albedo == null) throw new InvalidOperationException($"Curtain albedo {i} failed to import.");
            mat.color = Color.white;
            mat.SetTexture("_MainTex", albedo);
            mat.SetTexture("_BumpMap", normal);
            mat.SetFloat("_BumpScale", 0.36f);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Glossiness", 0.09f + i * 0.012f);
            mat.EnableKeyword("_NORMALMAP");
            mat.DisableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.black);
            EditorUtility.SetDirty(mat);
            result[i] = mat;
        }
        return result;
    }

    private static Material BuildTrackMaterial()
    {
        Shader shader = Shader.Find("Standard");
        if (shader == null) throw new InvalidOperationException("Standard shader not found for coated curtain track.");
        string path = $"{AssetRoot}/MAT_CurtainTrackCoatedAluminium.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader) { name = "MAT_CurtainTrackCoatedAluminium" };
            AssetDatabase.CreateAsset(mat, path);
        }
        else mat.shader = shader;
        mat.color = new Color(0.76f, 0.76f, 0.72f, 1f);
        // The visible optical surface is the paint/powder coat, therefore dielectric despite the Al core.
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Glossiness", 0.38f);
        mat.DisableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", Color.black);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Color32[] BuildNormalPixels(float[] height, float strength)
    {
        var target = new Color32[height.Length];
        for (int y = 0; y < TextureSize; y++)
        {
            int ym = (y - 1 + TextureSize) % TextureSize;
            int yp = (y + 1) % TextureSize;
            for (int x = 0; x < TextureSize; x++)
            {
                int xm = (x - 1 + TextureSize) % TextureSize;
                int xp = (x + 1) % TextureSize;
                float dx = height[y * TextureSize + xp] - height[y * TextureSize + xm];
                float dy = height[yp * TextureSize + x] - height[ym * TextureSize + x];
                Vector3 n = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
                target[y * TextureSize + x] = new Color32(
                    (byte)Mathf.RoundToInt((n.x * 0.5f + 0.5f) * 255f),
                    (byte)Mathf.RoundToInt((n.y * 0.5f + 0.5f) * 255f),
                    (byte)Mathf.RoundToInt((n.z * 0.5f + 0.5f) * 255f), 255);
            }
        }
        return target;
    }

    private static void WriteTexture(string path, Color32[] pixels, bool sRgb, bool normalMap)
    {
        Texture2D tex = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false, !sRgb);
        tex.SetPixels32(pixels);
        tex.Apply(false, false);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException($"Texture importer unavailable for {path}.");
        importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = sRgb && !normalMap;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Trilinear;
        importer.mipmapEnabled = true;
        importer.anisoLevel = 8;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }

    private static void ValidateContract()
    {
        if (!File.Exists(ContractPath))
            throw new InvalidOperationException($"Missing required window-dressing construction/material metadata: {ContractPath}");
        string json = File.ReadAllText(ContractPath);
        string[] required =
        {
            "manufacture_installation", "components", "dimensions_mm", "materials_finish",
            "mounting", "interfaces_gaps_seals", "orientation", "exposure_aging",
            "geometry_vs_material", "albedo_linear_rgb", "roughness", "metallic",
            "specular_f0", "normal_scale", "microstructure", "wetness", "uv_aging",
            "angular_fresnel_response", "lod0", "lod1", "lod2", "lod3",
            "approved_manufactured_repetition", "prohibited_clone_repetition",
            "visualFidelityPointsAwarded", "PENDING_UNITY_RUNTIME"
        };
        foreach (string token in required)
            if (!json.Contains(token, StringComparison.Ordinal))
                throw new InvalidOperationException($"Window-dressing contract missing required metadata token: {token}");
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x.scene.IsValid() && x.name == name);
    }

    private static int Quantize(float value, float step)
    {
        return Mathf.RoundToInt(value / step);
    }

    private static float Hash01(int a, int b, int salt)
    {
        unchecked
        {
            uint x = (uint)(a * 73856093 ^ b * 19349663 ^ salt * 83492791);
            x ^= x >> 16;
            x *= 0x7feb352du;
            x ^= x >> 15;
            x *= 0x846ca68bu;
            x ^= x >> 16;
            return (x & 0x00ffffffu) / 16777215f;
        }
    }

    private static int PositiveHash(string value)
    {
        unchecked
        {
            int hash = 17;
            foreach (char c in value) hash = hash * 31 + c;
            return hash & 0x7fffffff;
        }
    }
}