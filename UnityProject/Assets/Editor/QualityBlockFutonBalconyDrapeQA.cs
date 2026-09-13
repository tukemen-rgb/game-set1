using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Replaces the two benchmark-facing Futon_* Cube renderers with closed-volume textile drapes that
/// physically wrap the final rendered balcony top rail. The legacy object/collider remains as a
/// gameplay anchor but its primitive renderer is disabled. The drape resolves the reconstructed
/// guardrail datum when present and compensates its hanging length when the rail height changes.
/// Source-side construction QA only: native 3840x2160 evidence remains mandatory before any Visual
/// Fidelity point or critical-defect clearance.
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
    private const float ThicknessM = 0.022f;
    private const float FabricTileM = 0.14f;
    private const float MinFloorClearanceM = 0.030f;
    private const float MaxFloorClearanceM = 0.090f;
    private const float LegacyRailCenterAboveFloorM = 0.770f;
    private const float RailEnvelopeOffsetM = 0.051f;
    private const float ContactToleranceM = 0.012f;

    private static readonly float[] LodTransitions = { 0.14f, 0.065f, 0.028f, 0.009f };
    private static readonly int[] WidthSegments = { 24, 16, 10, 6 };
    private static readonly int[] PathSegments = { 30, 24, 12, 6 };

    private struct Spec
    {
        public int floor, bay, materialVariant;
        public float frontDrop, backDrop, sag;
        public Spec(int floor, int bay, float frontDrop, float backDrop, float sag, int materialVariant)
        {
            this.floor = floor; this.bay = bay; this.frontDrop = frontDrop; this.backDrop = backDrop;
            this.sag = sag; this.materialVariant = materialVariant;
        }
    }

    private static readonly Spec[] Specs =
    {
        new Spec(2, 1, 0.700f, 0.480f, 0.012f, 0),
        new Spec(3, 4, 0.680f, 0.515f, 0.009f, 1),
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
        Debug.Log("Balcony futon drapes persisted against final rail datum. Visual Fidelity remains UNSCORED pending native 4K evidence.");
    }

    public static void ApplyToOpenScene()
    {
        EnsureScene();
        ValidateContractConfigOnly();

        GameObject danchi = Find("Danchi");
        if (danchi == null) throw new InvalidOperationException("Danchi root is missing.");

        QualityBlockArtSlot slot = Resources.FindObjectsOfTypeAll<QualityBlockArtSlot>()
            .FirstOrDefault(x => x.gameObject.scene.IsValid() && x.SlotId == "danchi.main");
        if (slot != null && slot.IsUsingAuthoredArt)
        {
            DestroyGeneratedRoot();
            foreach (Spec spec in Specs) DisableLegacyRenderer(spec);
            return;
        }

        Directory.CreateDirectory(MeshRoot);
        Directory.CreateDirectory(MaterialRoot);
        EnsureCottonNormal();
        Material[] materials = BuildMaterials();
        DestroyGeneratedRoot();

        GameObject root = new GameObject(RootName);
        root.transform.SetParent(danchi.transform, false);
        foreach (Spec spec in Specs) BuildAssembly(root.transform, spec, materials[spec.materialVariant]);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("NewTown/QA/Validate Balcony Futon Drape Contract")]
    public static void ValidateContractConfigOnly()
    {
        if (!File.Exists(ContractPath)) throw new InvalidOperationException($"Futon contract missing: {ContractPath}");
        Contract c = JsonUtility.FromJson<Contract>(File.ReadAllText(ContractPath));
        var errors = new List<string>();
        if (c == null) errors.Add("contract is null/unparseable");
        else
        {
            if (c.schemaVersion != "1.0.1") errors.Add("schemaVersion must remain 1.0.1");
            if (c.scenePath != ScenePath) errors.Add($"scenePath must be {ScenePath}");
            if (c.expectedAssemblies != 2) errors.Add("expectedAssemblies must remain 2");
            if (c.dimensionsThickness == null) errors.Add("dimensionsThickness block is required");
            else
            {
                if (Mathf.Abs(c.dimensionsThickness.bodyThicknessM - ThicknessM) > 0.0001f)
                    errors.Add("bodyThicknessM drifted from 0.022 m");
                if (Mathf.Abs(c.dimensionsThickness.minFloorClearanceM - MinFloorClearanceM) > 0.0001f)
                    errors.Add("minimum floor clearance drifted");
                if (Mathf.Abs(c.dimensionsThickness.maxFloorClearanceM - MaxFloorClearanceM) > 0.0001f)
                    errors.Add("maximum floor clearance drifted");
                if (Mathf.Abs(c.dimensionsThickness.legacyRailCenterAboveFloorM - LegacyRailCenterAboveFloorM) > 0.0001f)
                    errors.Add("legacy rail-center datum drifted from 0.770 m");
                if (Mathf.Abs(c.dimensionsThickness.railEnvelopeCenterlineOffsetM - RailEnvelopeOffsetM) > 0.0001f)
                    errors.Add("rail envelope offset drifted from 0.051 m");
            }
            if (c.lodPolicy == null || c.lodPolicy.levels != 4 || !c.lodPolicy.crossFadeRequired)
                errors.Add("four cross-faded LODs are mandatory");
            if (c.visualCreditPolicy == null || c.visualCreditPolicy.autoVisualPoints != 0)
                errors.Add("source QA may not award Visual Fidelity points");
        }
        if (errors.Count > 0) throw new InvalidOperationException("Balcony futon contract FAILED:\n - " + string.Join("\n - ", errors));
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
            if (Find(RootName) != null) throw new InvalidOperationException("Generated futons remain behind authored danchi art.");
            return;
        }

        GameObject root = Find(RootName);
        if (root == null) throw new InvalidOperationException("FutonBalconyDrapes root is missing.");
        if (root.GetComponentsInChildren<Collider>(true).Length != 0)
            throw new InvalidOperationException("Generated futons are render-only and may not add gameplay colliders.");

        foreach (Spec spec in Specs)
        {
            GameObject legacy = Find(LegacyName(spec));
            if (legacy == null) throw new InvalidOperationException($"Legacy occupancy anchor missing: {LegacyName(spec)}");
            Renderer legacyRenderer = legacy.GetComponent<Renderer>();
            if (legacyRenderer != null && legacyRenderer.enabled)
                throw new InvalidOperationException($"Critical placeholder risk: {LegacyName(spec)} Cube renderer is still enabled.");

            Transform assembly = root.transform.Find(AssemblyName(spec));
            if (assembly == null) throw new InvalidOperationException($"Futon assembly missing: {AssemblyName(spec)}");
            LODGroup group = assembly.GetComponent<LODGroup>();
            if (group == null) throw new InvalidOperationException($"{assembly.name} LODGroup missing.");
            LOD[] lods = group.GetLODs();
            if (lods.Length != 4) throw new InvalidOperationException($"{assembly.name} must have LOD0/1/2/3.");
            if (group.fadeMode != LODFadeMode.CrossFade || !group.animateCrossFading)
                throw new InvalidOperationException($"{assembly.name} must use animated LOD cross-fade.");

            Bounds? lod0 = null;
            int previousVertices = int.MaxValue;
            for (int level = 0; level < 4; level++)
            {
                if (lods[level].renderers == null || lods[level].renderers.Length != 1 || lods[level].renderers[0] == null)
                    throw new InvalidOperationException($"{assembly.name} LOD{level} must own one textile renderer.");
                Renderer r = lods[level].renderers[0];
                Mesh mesh = r.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null || !mesh.name.StartsWith("GM_FutonDrape_", StringComparison.Ordinal) || IsPrimitive(mesh.name))
                    throw new InvalidOperationException($"{assembly.name} LOD{level} is not dedicated drape geometry.");
                if (mesh.vertexCount >= previousVertices)
                    throw new InvalidOperationException($"{assembly.name} LOD vertex count does not progressively decrease at LOD{level}.");
                previousVertices = mesh.vertexCount;
                ValidateOutwardHemWinding(assembly.name, level, mesh);
                ValidateCotton(assembly.name, level, r.sharedMaterial);

                if (level == 0) lod0 = r.bounds;
                else
                {
                    Bounds b0 = lod0.Value;
                    Bounds b = r.bounds;
                    if (MaxComponent(Abs(b.size - b0.size)) > 0.018f || (b.center - b0.center).magnitude > 0.010f)
                        throw new InvalidOperationException($"{assembly.name} LOD{level} changes the drape macro silhouette beyond tolerance.");
                }
            }

            Bounds rail = QualityBlockBalconyGuardrailInstallationQA.ResolveTopRailBounds(spec.floor, spec.bay);
            Renderer floor = RequireRenderer($"BalconyFloor_{spec.floor}_{spec.bay}");
            Bounds cloth = lod0.Value;
            if (cloth.max.z < rail.max.z + 0.025f || cloth.min.z > rail.min.z - 0.005f)
                throw new InvalidOperationException($"{assembly.name} does not wrap both front/back sides of the final top rail.");
            if (cloth.max.y < rail.max.y + 0.004f)
                throw new InvalidOperationException($"{assembly.name} does not crest above the final top rail.");

            float floorClearance = cloth.min.y - floor.bounds.max.y;
            if (floorClearance < MinFloorClearanceM || floorClearance > MaxFloorClearanceM)
                throw new InvalidOperationException($"{assembly.name} lower-edge/floor clearance {floorClearance:F4}m outside 0.030-0.090m.");

            float inner = RailEnvelopeOffsetM - ThicknessM * 0.5f;
            if (Mathf.Abs(inner - rail.extents.y) > ContactToleranceM || Mathf.Abs(inner - rail.extents.z) > ContactToleranceM)
                throw new InvalidOperationException($"{assembly.name} rail-contact envelope no longer matches the final rendered rail section.");
        }

        if (root.GetComponentsInChildren<MeshRenderer>(true).Length != 8)
            throw new InvalidOperationException("Expected exactly two futons x four LOD renderers.");

        Debug.Log("Balcony futon source QA passed: primitive slabs disabled; two distinct closed drapes resolve the final guardrail datum, wrap the rail, clear the slab, keep outward-facing hems, stay dielectric and retain four cross-faded LODs. Native 4K inspection is still mandatory.");
    }

    private static void BuildAssembly(Transform parent, Spec spec, Material material)
    {
        GameObject legacy = Find(LegacyName(spec));
        if (legacy == null) throw new InvalidOperationException($"Legacy futon anchor missing: {LegacyName(spec)}");
        DisableLegacyRenderer(spec);
        Bounds rail = QualityBlockBalconyGuardrailInstallationQA.ResolveTopRailBounds(spec.floor, spec.bay);
        Renderer floor = RequireRenderer($"BalconyFloor_{spec.floor}_{spec.bay}");

        float railCenterAboveFloor = rail.center.y - floor.bounds.max.y;
        if (railCenterAboveFloor < LegacyRailCenterAboveFloorM - 0.010f)
            throw new InvalidOperationException(
                $"{LegacyName(spec)} final rail center is unexpectedly low ({railCenterAboveFloor:F4} m above floor); do not shorten bedding to hide a construction regression.");
        float resolvedFrontDrop = spec.frontDrop + Mathf.Max(0f, railCenterAboveFloor - LegacyRailCenterAboveFloorM);

        GameObject assembly = new GameObject(AssemblyName(spec));
        assembly.transform.SetParent(parent, false);
        assembly.transform.position = new Vector3(legacy.transform.position.x, rail.center.y, rail.center.z);
        Renderer[][] sets = new Renderer[4][];

        for (int level = 0; level < 4; level++)
        {
            GameObject tier = new GameObject($"LOD{level}");
            tier.transform.SetParent(assembly.transform, false);
            GameObject body = new GameObject("FutonBody");
            body.transform.SetParent(tier.transform, false);
            MeshFilter filter = body.AddComponent<MeshFilter>();
            filter.sharedMesh = BuildMeshAsset(spec, level, resolvedFrontDrop);
            MeshRenderer renderer = body.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            sets[level] = new Renderer[] { renderer };
        }

        LODGroup group = assembly.AddComponent<LODGroup>();
        group.fadeMode = LODFadeMode.CrossFade;
        group.animateCrossFading = true;
        group.SetLODs(new[]
        {
            new LOD(LodTransitions[0], sets[0]), new LOD(LodTransitions[1], sets[1]),
            new LOD(LodTransitions[2], sets[2]), new LOD(LodTransitions[3], sets[3])
        });
        group.RecalculateBounds();

        QualityBlockFutonDrapeManifest manifest = assembly.AddComponent<QualityBlockFutonDrapeManifest>();
        manifest.Configure(spec.floor, spec.bay, WidthM, ThicknessM, resolvedFrontDrop, spec.backDrop, floor.bounds.max.y, spec.sag);
    }

    private static Mesh BuildMeshAsset(Spec spec, int level, float resolvedFrontDrop)
    {
        string path = $"{MeshRoot}/GM_FutonDrape_F{spec.floor}_B{spec.bay}_LOD{level}.asset";
        if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
        Mesh mesh = BuildDrapeMesh(resolvedFrontDrop, spec.backDrop, spec.sag, WidthSegments[level], PathSegments[level]);
        mesh.name = $"GM_FutonDrape_F{spec.floor}_B{spec.bay}_LOD{level}";
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static Mesh BuildDrapeMesh(float frontDrop, float backDrop, float sag, int widthSegments, int pathSegments)
    {
        if (pathSegments % 6 != 0) throw new InvalidOperationException("Futon pathSegments must be divisible by six.");
        Vector2[] keys =
        {
            new Vector2(-backDrop, -0.090f), new Vector2(-0.060f, -0.061f), new Vector2(0.050f, -0.050f),
            new Vector2(0.061f, 0f), new Vector2(0.050f, 0.050f), new Vector2(-0.060f, 0.061f),
            new Vector2(-frontDrop, 0.105f)
        };

        int pc = pathSegments + 1, wc = widthSegments + 1, perSide = pc * wc;
        Vector2[] center = new Vector2[pc], normal = new Vector2[pc];
        float[] distance = new float[pc];
        for (int p = 0; p < pc; p++)
        {
            float u = p / (float)pathSegments * 6f;
            int s = Mathf.Min(5, Mathf.FloorToInt(u));
            float t = Mathf.Clamp01(u - s);
            center[p] = Catmull(keys[Mathf.Max(0, s - 1)], keys[s], keys[s + 1], keys[Mathf.Min(6, s + 2)], t);
            if (p > 0) distance[p] = distance[p - 1] + Vector2.Distance(center[p - 1], center[p]);
        }
        for (int p = 0; p < pc; p++)
        {
            Vector2 tangent = (center[Mathf.Min(pc - 1, p + 1)] - center[Mathf.Max(0, p - 1)]).normalized;
            normal[p] = new Vector2(-tangent.y, tangent.x).normalized;
        }

        Vector3[] vertices = new Vector3[perSide * 2];
        Vector2[] uv = new Vector2[vertices.Length];
        float halfW = WidthM * 0.5f, halfT = ThicknessM * 0.5f;
        for (int side = 0; side < 2; side++)
        for (int p = 0; p < pc; p++)
        for (int xIndex = 0; xIndex < wc; xIndex++)
        {
            float x = Mathf.Lerp(-halfW, halfW, xIndex / (float)widthSegments);
            float nx = x / halfW;
            float gravitySag = sag * Mathf.Max(0f, 1f - nx * nx);
            Vector2 yz = center[p] + normal[p] * ((side == 0 ? -1f : 1f) * halfT);
            yz.x -= gravitySag;
            int i = side * perSide + p * wc + xIndex;
            vertices[i] = new Vector3(x, yz.x, yz.y);
            uv[i] = new Vector2((x + halfW) / FabricTileM, distance[p] / FabricTileM);
        }

        var tris = new List<int>();
        for (int side = 0; side < 2; side++)
        for (int p = 0; p < pathSegments; p++)
        for (int x = 0; x < widthSegments; x++)
        {
            int o = side * perSide, a = o + p * wc + x, b = a + 1, d = o + (p + 1) * wc + x, c = d + 1;
            AddQuad(tris, a, b, c, d, side == 0);
        }

        int neg = 0, pos = perSide;
        for (int p = 0; p < pathSegments; p++)
        {
            AddQuad(tris, neg + p * wc, pos + p * wc, pos + (p + 1) * wc, neg + (p + 1) * wc, false);
            int nr0 = neg + p * wc + widthSegments, nr1 = neg + (p + 1) * wc + widthSegments;
            int pr0 = pos + p * wc + widthSegments, pr1 = pos + (p + 1) * wc + widthSegments;
            AddQuad(tris, nr0, nr1, pr1, pr0, false);
        }
        for (int x = 0; x < widthSegments; x++)
        {
            AddQuad(tris, neg + x, neg + x + 1, pos + x + 1, pos + x, false);
            int nf = neg + pathSegments * wc + x, pf = pos + pathSegments * wc + x;
            AddQuad(tris, nf, pf, pf + 1, nf + 1, false);
        }

        Mesh mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
        mesh.vertices = vertices; mesh.uv = uv; mesh.SetTriangles(tris, 0, true);
        mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        return mesh;
    }

    private static void ValidateOutwardHemWinding(string assembly, int lod, Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        if (vertices == null || triangles == null || triangles.Length < 3)
            throw new InvalidOperationException($"{assembly} LOD{lod} mesh is empty.");
        float minX = vertices.Min(v => v.x), maxX = vertices.Max(v => v.x);
        Vector3 leftSum = Vector3.zero, rightSum = Vector3.zero;
        int leftCount = 0, rightCount = 0;
        const float epsilon = 0.0001f;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
            bool left = Mathf.Abs(a.x - minX) < epsilon && Mathf.Abs(b.x - minX) < epsilon && Mathf.Abs(c.x - minX) < epsilon;
            bool right = Mathf.Abs(a.x - maxX) < epsilon && Mathf.Abs(b.x - maxX) < epsilon && Mathf.Abs(c.x - maxX) < epsilon;
            if (!left && !right) continue;
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            if (left) { leftSum += n; leftCount++; }
            if (right) { rightSum += n; rightCount++; }
        }
        if (leftCount == 0 || rightCount == 0)
            throw new InvalidOperationException($"{assembly} LOD{lod} closed side hems are missing.");
        if ((leftSum / leftCount).x > -0.80f || (rightSum / rightCount).x < 0.80f)
            throw new InvalidOperationException($"{assembly} LOD{lod} side-hem triangle winding faces inward and would disappear under backface culling.");
    }

    private static Vector2 Catmull(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    private static void AddQuad(List<int> tris, int a, int b, int c, int d, bool reverse)
    {
        if (!reverse) { tris.Add(a); tris.Add(b); tris.Add(c); tris.Add(a); tris.Add(c); tris.Add(d); }
        else { tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(a); tris.Add(d); tris.Add(c); }
    }

    private static Material[] BuildMaterials()
    {
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
        if (normal == null) throw new InvalidOperationException("Futon cotton normal failed to import.");
        return new[]
        {
            CottonMaterial("MAT_FutonCottonWashedBlueA", new Color(0.46f, 0.56f, 0.66f, 1f), 0.15f, normal),
            CottonMaterial("MAT_FutonCottonWashedBlueB", new Color(0.53f, 0.61f, 0.69f, 1f), 0.17f, normal)
        };
    }

    private static Material CottonMaterial(string name, Color albedo, float smoothness, Texture2D normal)
    {
        string path = $"{MaterialRoot}/{name}.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("Unity Standard shader unavailable.");
            m = new Material(shader) { name = name }; AssetDatabase.CreateAsset(m, path);
        }
        m.color = albedo; m.SetFloat("_Metallic", 0f); m.SetFloat("_Glossiness", smoothness);
        m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", 0.34f); m.EnableKeyword("_NORMALMAP");
        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
        m.DisableKeyword("_EMISSION"); EditorUtility.SetDirty(m); return m;
    }

    private static void EnsureCottonNormal()
    {
        const int size = 512;
        Texture2D t = new Texture2D(size, size, TextureFormat.RGB24, true, true) { name = "T_FutonCottonWeave_N" };
        Color[] px = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = Height(x + 1, y) - Height(x - 1, y), dy = Height(x, y + 1) - Height(x, y - 1);
            Vector3 n = new Vector3(-dx * 1.8f, -dy * 1.8f, 1f).normalized;
            px[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
        }
        t.SetPixels(px); t.Apply(true, false); File.WriteAllBytes(NormalPath, t.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(NormalPath, ImportAssetOptions.ForceUpdate);
        TextureImporter i = AssetImporter.GetAtPath(NormalPath) as TextureImporter;
        if (i == null) throw new InvalidOperationException("Cannot configure futon normal importer.");
        i.textureType = TextureImporterType.NormalMap; i.sRGBTexture = false; i.mipmapEnabled = true;
        i.wrapMode = TextureWrapMode.Repeat; i.filterMode = FilterMode.Trilinear; i.anisoLevel = 8;
        i.textureCompression = TextureImporterCompression.CompressedHQ; i.SaveAndReimport();
    }

    private static float Height(int x, int y)
    {
        float warp = Mathf.Sin(x * Mathf.PI * 0.57f) * 0.34f, weft = Mathf.Sin(y * Mathf.PI * 0.43f + 0.7f) * 0.29f;
        uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ 0x9E3779B9u; h ^= h >> 13; h *= 1274126177u; h ^= h >> 16;
        return warp + weft + ((h & 1023u) / 1023f - 0.5f) * 0.14f;
    }

    private static void ValidateCotton(string assembly, int lod, Material m)
    {
        if (m == null || m.shader == null || m.shader.name != "Standard")
            throw new InvalidOperationException($"{assembly} LOD{lod} cotton must use Standard PBR.");
        if (m.GetFloat("_Metallic") > 0.01f)
            throw new InvalidOperationException($"{assembly} LOD{lod} cotton is impossibly metallic.");
        float s = m.GetFloat("_Glossiness");
        if (s < 0.10f || s > 0.22f)
            throw new InvalidOperationException($"{assembly} LOD{lod} cotton smoothness outside dry-textile range.");
        if (!m.IsKeywordEnabled("_NORMALMAP") || m.GetTexture("_BumpMap") == null)
            throw new InvalidOperationException($"{assembly} LOD{lod} cotton weave normal missing.");
        if (m.IsKeywordEnabled("_EMISSION") || (m.HasProperty("_EmissionColor") && m.GetColor("_EmissionColor").maxColorComponent > 0.001f))
            throw new InvalidOperationException($"{assembly} LOD{lod} daytime futon may not emit light.");
    }

    private static void DisableLegacyRenderer(Spec spec)
    {
        GameObject legacy = Find(LegacyName(spec)); Renderer r = legacy != null ? legacy.GetComponent<Renderer>() : null;
        if (r != null) r.enabled = false;
    }

    private static void DestroyGeneratedRoot()
    {
        GameObject old = Find(RootName); if (old != null) UnityEngine.Object.DestroyImmediate(old);
    }

    private static Renderer RequireRenderer(string name)
    {
        Renderer r = Find(name)?.GetComponent<Renderer>();
        if (r == null || !r.enabled) throw new InvalidOperationException($"Required active construction renderer missing: {name}");
        return r;
    }

    private static string LegacyName(Spec s) => $"Futon_{s.floor}_{s.bay}";
    private static string AssemblyName(Spec s) => $"HD_FutonDrape_{s.floor}_{s.bay}";
    private static bool IsPrimitive(string n) => n == "Cube" || n == "Cylinder" || n == "Sphere" || n == "Capsule" || n == "Plane" || n == "Quad";
    private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    private static float MaxComponent(Vector3 v) => Mathf.Max(v.x, Mathf.Max(v.y, v.z));
    private static void EnsureScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }
    private static GameObject Find(string name) => Resources.FindObjectsOfTypeAll<GameObject>()
        .FirstOrDefault(x => x.scene.IsValid() && x.scene.path == ScenePath && x.name == name);

    [Serializable] private sealed class Contract
    {
        public string schemaVersion, scenePath;
        public int expectedAssemblies;
        public DimensionsThickness dimensionsThickness;
        public LodPolicy lodPolicy;
        public VisualCreditPolicy visualCreditPolicy;
    }
    [Serializable] private sealed class DimensionsThickness
    {
        public float bodyThicknessM;
        public float legacyRailCenterAboveFloorM;
        public float railEnvelopeCenterlineOffsetM;
        public float minFloorClearanceM;
        public float maxFloorClearanceM;
    }
    [Serializable] private sealed class LodPolicy { public int levels; public bool crossFadeRequired; }
    [Serializable] private sealed class VisualCreditPolicy { public int autoVisualPoints; }
}

[DisallowMultipleComponent]
public sealed class QualityBlockFutonDrapeManifest : MonoBehaviour
{
    [SerializeField] private int floor, bay;
    [SerializeField] private float widthM, bodyThicknessM, frontDropM, backDropM, balconyFloorTopWorldY, centerSagM;
    public void Configure(int sourceFloor, int sourceBay, float width, float thickness, float frontDrop, float backDrop, float floorTop, float sag)
    {
        floor = sourceFloor; bay = sourceBay; widthM = width; bodyThicknessM = thickness; frontDropM = frontDrop;
        backDropM = backDrop; balconyFloorTopWorldY = floorTop; centerSagM = Mathf.Clamp(sag, 0f, 0.020f);
    }
}