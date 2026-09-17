using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Adds sparse geometric maintained-lawn blades over the generated GrassField so the benchmark does
/// not rely on a perfectly flat green plane for all non-tree vegetation. Compact combined patch
/// meshes keep the layer real-time: each populated patch contains curved double-sided lamina geometry
/// with deterministic height/width/lean variation and four cross-faded LODs. Hardscape/building
/// footprints are rejected before mesh generation so blades do not grow through constructed surfaces.
///
/// The source checks below improve implementation readiness only. Blade silhouette, density, grazing
/// response, shimmer and LOD transitions still require native 3840x2160 still/temporal evidence.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockGrassBladeFieldUpgrade
{
    public const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    public const string ContractPath = "Assets/QA/grass_blade_field_contract.json";
    public const string RootName = "GrassBladeFieldDetail";
    public const string GeneratorMarker = "Generator_grass-blade-field-v1.0.1";

    private const string MeshRoot = "Assets/Art/GeneratedGrassBladeMeshes";
    private const string SourceMaterialPath = "Assets/Art/GeneratedPBR/PBR_GrassWorn.mat";
    private const string BladeMaterialPath = "Assets/Art/GeneratedGrassBladeMeshes/MAT_GrassBladeField.mat";

    private const float FieldWidth = 54f;
    private const float FieldDepth = 36f;
    private const float PatchSize = 9f;
    private const int PatchColumns = 6;
    private const int PatchRows = 4;
    private const int Lods = 4;
    private const int Lod0CandidateCount = 240;
    private static readonly int[] LodCaps = { 240, 120, 54, 22 };
    private static readonly float[] LodRatios = { 1.0f, 0.50f, 0.23f, 0.09f };
    private static readonly int[] BladesPerTuft = { 3, 3, 2, 2 };
    private static readonly float[] LodTransitions = { 0.18f, 0.08f, 0.035f, 0.012f };

    private static bool applying;

    private readonly struct RectXZ
    {
        public readonly float MinX;
        public readonly float MaxX;
        public readonly float MinZ;
        public readonly float MaxZ;

        public RectXZ(float minX, float maxX, float minZ, float maxZ)
        {
            MinX = minX;
            MaxX = maxX;
            MinZ = minZ;
            MaxZ = maxZ;
        }

        public bool Contains(float x, float z, float margin)
        {
            return x >= MinX - margin && x <= MaxX + margin && z >= MinZ - margin && z <= MaxZ + margin;
        }

        public float EdgeDistance(float x, float z)
        {
            float dx = Mathf.Max(MinX - x, Mathf.Max(0f, x - MaxX));
            float dz = Mathf.Max(MinZ - z, Mathf.Max(0f, z - MaxZ));
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }

    private readonly struct Tuft
    {
        public readonly Vector3 LocalPosition;
        public readonly int Candidate;
        public readonly float Height;
        public readonly float Width;
        public readonly float Yaw;
        public readonly float Lean;

        public Tuft(Vector3 localPosition, int candidate, float height, float width, float yaw, float lean)
        {
            LocalPosition = localPosition;
            Candidate = candidate;
            Height = height;
            Width = width;
            Yaw = yaw;
            Lean = lean;
        }
    }

    // Conservative benchmark footprints. The extra margin leaves interface/detail ownership to the
    // hardscape and building systems rather than allowing grass blades to interpenetrate their edges.
    private static readonly RectXZ[] Exclusions =
    {
        new RectXZ(-21.25f, 5.25f, -15.85f, -7.15f), // apartment footprint
        new RectXZ(-20.00f, 4.00f, -5.50f, 7.50f),   // danchi plaza
        new RectXZ(6.10f, 11.30f, -11.00f, 11.00f), // park path
        new RectXZ(0.30f, 4.70f, -10.05f, -1.55f),  // worn path A
        new RectXZ(12.00f, 14.00f, 4.50f, 10.50f),  // worn path B
    };

    static QualityBlockGrassBladeFieldUpgrade()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        Camera.onPreCull += OnCameraPreCull;
        EditorApplication.delayCall += EnsureCurrentSceneIfReady;
    }

    [MenuItem("NewTown/Vegetation/Build Physical Grass Blade Field")]
    public static void BuildAndApply()
    {
        EnsureBenchmarkScene();
        ApplyToOpenScene(true);
        ValidateOpenScene(true);
        Debug.Log("Physical grass blade field built. Native 4K/temporal verification remains pending; no Visual Fidelity points were awarded.");
    }

    public static void ApplyToOpenScene(bool persist)
    {
        if (applying)
            return;

        EnsureBenchmarkScene();
        if (!File.Exists(ContractPath))
            throw new InvalidOperationException($"Grass blade contract missing: {ContractPath}");

        Material source = AssetDatabase.LoadAssetAtPath<Material>(SourceMaterialPath);
        if (source == null)
            throw new InvalidOperationException($"Grass blade pass requires generated source material {SourceMaterialPath}.");

        applying = true;
        try
        {
            Directory.CreateDirectory(MeshRoot);
            Material bladeMaterial = EnsureBladeMaterial(source);
            GameObject ground = FindSceneObject("Ground");
            if (ground == null)
                throw new InvalidOperationException("Ground root missing for grass blade field.");

            Transform existing = ground.transform.Find(RootName);
            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing.gameObject);

            var root = new GameObject(RootName);
            root.transform.SetParent(ground.transform, false);
            var marker = new GameObject(GeneratorMarker);
            marker.transform.SetParent(root.transform, false);

            int builtPatches = 0;
            for (int row = 0; row < PatchRows; row++)
            {
                for (int col = 0; col < PatchColumns; col++)
                {
                    int patchIndex = row * PatchColumns + col;
                    Vector3 center = new Vector3(
                        -FieldWidth * 0.5f + PatchSize * 0.5f + col * PatchSize,
                        0.004f,
                        -FieldDepth * 0.5f + PatchSize * 0.5f + row * PatchSize);

                    List<Tuft> tufts = GenerateTufts(patchIndex, center, Lod0CandidateCount);
                    if (tufts.Count < 8)
                        continue; // Cells with almost no exposed lawn should not manufacture dense strips in a tiny remainder.

                    BuildPatch(root.transform, patchIndex, center, tufts, bladeMaterial);
                    builtPatches++;
                }
            }

            if (builtPatches < 12)
                throw new InvalidOperationException($"Grass blade coverage unexpectedly sparse: only {builtPatches} patches survived construction exclusions.");

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            if (persist)
                EditorSceneManager.SaveOpenScenes();
            AssetDatabase.Refresh();
        }
        finally
        {
            applying = false;
        }
    }

    [MenuItem("NewTown/QA/Validate Physical Grass Blade Field")]
    public static void ValidateOpenSceneMenu()
    {
        ValidateOpenScene(true);
    }

    public static void ValidateOpenScene(bool logSuccess)
    {
        EnsureBenchmarkScene();
        ValidateContract();

        GameObject ground = FindSceneObject("Ground");
        Transform root = ground != null ? ground.transform.Find(RootName) : null;
        if (root == null)
            throw new InvalidOperationException("GrassBladeFieldDetail is missing from Ground.");
        if (root.Find(GeneratorMarker) == null)
            throw new InvalidOperationException("Grass blade generator-version marker is missing/stale.");

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        if (colliders.Length != 0)
            throw new InvalidOperationException($"Grass blade art must not add gameplay colliders; found {colliders.Length}.");

        Material bladeMaterial = AssetDatabase.LoadAssetAtPath<Material>(BladeMaterialPath);
        if (bladeMaterial == null)
            throw new InvalidOperationException("Grass blade material asset is missing.");
        if (bladeMaterial.HasProperty("_Metallic") && bladeMaterial.GetFloat("_Metallic") > 0.01f)
            throw new InvalidOperationException("Grass lamina material is materially impossible: metallic must be zero.");
        if (bladeMaterial.IsKeywordEnabled("_EMISSION") ||
            (bladeMaterial.HasProperty("_EmissionColor") && bladeMaterial.GetColor("_EmissionColor").maxColorComponent > 0.001f))
            throw new InvalidOperationException("Grass blade material may not use emission/baked-light response.");
        if (bladeMaterial.HasProperty("_GlossMapScale"))
        {
            float scale = bladeMaterial.GetFloat("_GlossMapScale");
            if (scale < 0.16f || scale > 0.36f)
                throw new InvalidOperationException($"Grass blade smoothness scale drifted outside dry-lamina range: {scale:F3}.");
        }

        Transform[] patches = Enumerable.Range(0, PatchColumns * PatchRows)
            .Select(i => root.Find($"Patch_{i:00}"))
            .Where(x => x != null)
            .ToArray();
        if (patches.Length < 12)
            throw new InvalidOperationException($"Expected at least 12 populated grass patches, got {patches.Length}.");

        var geometrySignatures = new HashSet<string>(StringComparer.Ordinal);
        foreach (Transform patch in patches)
        {
            LODGroup group = patch.GetComponent<LODGroup>();
            if (group == null)
                throw new InvalidOperationException($"Grass patch {patch.name} has no LODGroup.");
            LOD[] lods = group.GetLODs();
            if (lods.Length != Lods)
                throw new InvalidOperationException($"Grass patch {patch.name} expected {Lods} LODs, got {lods.Length}.");
            if (group.fadeMode != LODFadeMode.CrossFade || !group.animateCrossFading)
                throw new InvalidOperationException($"Grass patch {patch.name} must use animated cross-fade.");

            int previousVertices = int.MaxValue;
            for (int lod = 0; lod < lods.Length; lod++)
            {
                if (lods[lod].renderers == null || lods[lod].renderers.Length != 1)
                    throw new InvalidOperationException($"Grass patch {patch.name} LOD{lod} must be one combined renderer.");
                Renderer renderer = lods[lod].renderers[0];
                MeshFilter filter = renderer != null ? renderer.GetComponent<MeshFilter>() : null;
                Mesh mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null || !mesh.name.StartsWith("GM_GrassPatch_", StringComparison.Ordinal))
                    throw new InvalidOperationException($"Grass patch {patch.name} LOD{lod} lacks authored combined grass mesh.");
                if (mesh.name == "Quad" || mesh.name == "Plane" || mesh.name == "Cube")
                    throw new InvalidOperationException($"Grass patch {patch.name} reverted to primitive placeholder geometry.");
                if (mesh.vertexCount >= previousVertices)
                    throw new InvalidOperationException($"Grass patch {patch.name} LOD vertex reduction is not strict at LOD{lod}: {mesh.vertexCount} >= {previousVertices}.");
                previousVertices = mesh.vertexCount;
                if (renderer.sharedMaterial != bladeMaterial)
                    throw new InvalidOperationException($"Grass patch {patch.name} LOD{lod} material drifted from canonical blade material.");

                if (lod == 0)
                    geometrySignatures.Add(GeometrySignature(mesh));
            }
        }

        int requiredDistinct = Mathf.Min(10, patches.Length);
        if (geometrySignatures.Count < requiredDistinct)
            throw new InvalidOperationException(
                $"Grass patch geometry is too repetitive: only {geometrySignatures.Count} distinct structural signatures across {patches.Length} populated patches; require {requiredDistinct}.");

        foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (renderer.sharedMaterial != bladeMaterial)
                throw new InvalidOperationException($"Unexpected grass-field material on {renderer.name}.");
        }

        if (logSuccess)
            Debug.Log($"Grass blade source QA passed for {patches.Length} populated patches with area-proportional density, distinct deterministic geometry and four cross-faded LODs. Native 4K appearance remains unscored.");
    }

    private static void BuildPatch(Transform parent, int patchIndex, Vector3 center, List<Tuft> tufts, Material material)
    {
        var patch = new GameObject($"Patch_{patchIndex:00}");
        patch.transform.SetParent(parent, false);
        patch.transform.localPosition = center;

        var lods = new LOD[Lods];
        for (int lod = 0; lod < Lods; lod++)
        {
            int tuftCount = ResolveLodTuftCount(lod, tufts.Count);
            Mesh mesh = BuildPatchMesh(patchIndex, lod, center, tufts, tuftCount, BladesPerTuft[lod]);
            string meshPath = $"{MeshRoot}/GM_GrassPatch_{patchIndex:00}_LOD{lod}.asset";
            SaveOrReplaceMesh(meshPath, mesh);
            Mesh assetMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (assetMesh == null)
                throw new InvalidOperationException($"Failed to persist grass mesh {meshPath}.");

            var level = new GameObject($"LOD{lod}_Grass");
            level.transform.SetParent(patch.transform, false);
            var filter = level.AddComponent<MeshFilter>();
            filter.sharedMesh = assetMesh;
            var renderer = level.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            lods[lod] = new LOD(LodTransitions[lod], new Renderer[] { renderer });
        }

        var group = patch.AddComponent<LODGroup>();
        group.SetLODs(lods);
        group.fadeMode = LODFadeMode.CrossFade;
        group.animateCrossFading = true;
        group.RecalculateBounds();
    }

    private static int ResolveLodTuftCount(int lod, int available)
    {
        if (available < 1)
            throw new InvalidOperationException("Cannot build grass LODs from an empty patch.");
        if (lod < 0 || lod >= Lods)
            throw new ArgumentOutOfRangeException(nameof(lod));

        int count = lod == 0
            ? Mathf.Min(LodCaps[0], available)
            : Mathf.Min(LodCaps[lod], Mathf.Max(1, Mathf.FloorToInt(available * LodRatios[lod])));
        if (lod > 0)
            count = Mathf.Min(count, ResolveLodTuftCount(lod - 1, available) - 1);
        return Mathf.Max(1, count);
    }

    private static List<Tuft> GenerateTufts(int patchIndex, Vector3 center, int candidateCount)
    {
        // Exactly one candidate budget is evaluated per patch. Rejected candidates are NOT retried;
        // therefore a patch that is mostly occupied by hardscape naturally contains fewer blades
        // instead of concentrating a full-patch count into the remaining narrow grass strip.
        var result = new List<Tuft>(candidateCount);
        for (int candidate = 0; candidate < candidateCount; candidate++)
        {
            float lx = Mathf.Lerp(-PatchSize * 0.5f + 0.12f, PatchSize * 0.5f - 0.12f,
                Hash01(patchIndex, candidate, 101));
            float lz = Mathf.Lerp(-PatchSize * 0.5f + 0.12f, PatchSize * 0.5f - 0.12f,
                Hash01(patchIndex, candidate, 103));
            float wx = center.x + lx;
            float wz = center.z + lz;

            if (!IsGrassEligible(wx, wz))
                continue;

            float edgeDistance = NearestHardscapeDistance(wx, wz);
            float maintained = Mathf.InverseLerp(0.18f, 0.90f, edgeDistance);
            float height = Mathf.Lerp(0.055f, 0.165f, Hash01(patchIndex, candidate, 107));
            height *= Mathf.Lerp(0.72f, 1f, maintained);
            float width = Mathf.Lerp(0.0042f, 0.0092f, Hash01(patchIndex, candidate, 109));
            float yaw = Hash01(patchIndex, candidate, 113) * 360f;
            float lean = Mathf.Lerp(0.025f, 0.19f, Hash01(patchIndex, candidate, 127));
            result.Add(new Tuft(new Vector3(lx, 0f, lz), candidate, height, width, yaw, lean));
        }
        return result;
    }

    private static Mesh BuildPatchMesh(int patchIndex, int lod, Vector3 patchCenter, List<Tuft> tufts,
        int tuftCount, int bladesPerTuft)
    {
        var vertices = new List<Vector3>(tuftCount * bladesPerTuft * 12);
        var uv = new List<Vector2>(tuftCount * bladesPerTuft * 12);
        var triangles = new List<int>(tuftCount * bladesPerTuft * 24);

        for (int i = 0; i < tuftCount; i++)
        {
            Tuft tuft = tufts[i];
            for (int blade = 0; blade < bladesPerTuft; blade++)
            {
                float bladeYaw = tuft.Yaw + blade * (360f / bladesPerTuft) +
                                 Mathf.Lerp(-22f, 22f, Hash01(patchIndex, tuft.Candidate, 200 + blade));
                float height = tuft.Height * Mathf.Lerp(0.82f, 1.08f,
                    Hash01(patchIndex, tuft.Candidate, 230 + blade));
                float width = tuft.Width * Mathf.Lerp(0.82f, 1.12f,
                    Hash01(patchIndex, tuft.Candidate, 260 + blade));
                float lean = tuft.Lean * Mathf.Lerp(0.72f, 1.15f,
                    Hash01(patchIndex, tuft.Candidate, 290 + blade));
                AddBlade(vertices, uv, triangles, tuft.LocalPosition, patchCenter, bladeYaw, height, width, lean,
                    Hash01(patchIndex, tuft.Candidate, 320 + blade),
                    Hash01(patchIndex, tuft.Candidate, 350 + blade));
            }
        }

        var mesh = new Mesh
        {
            name = $"GM_GrassPatch_{patchIndex:00}_LOD{lod}",
            indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16,
        };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(triangles, 0, true);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddBlade(List<Vector3> vertices, List<Vector2> uv, List<int> triangles,
        Vector3 basePosition, Vector3 patchCenter, float yawDegrees, float height, float width, float lean,
        float uPhase, float vPhase)
    {
        float yaw = yawDegrees * Mathf.Deg2Rad;
        Vector3 forward = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
        Vector3 side = new Vector3(forward.z, 0f, -forward.x);
        Vector3 bend = forward * (height * lean);

        Vector3 p0 = basePosition - side * (width * 0.5f);
        Vector3 p1 = basePosition + side * (width * 0.5f);
        Vector3 midCenter = basePosition + Vector3.up * (height * 0.56f) + bend * 0.32f;
        Vector3 p2 = midCenter - side * (width * 0.39f);
        Vector3 p3 = midCenter + side * (width * 0.39f);
        Vector3 tipCenter = basePosition + Vector3.up * height + bend;
        Vector3 p4 = tipCenter - side * (width * 0.035f);
        Vector3 p5 = tipCenter + side * (width * 0.035f);

        int start = vertices.Count;
        Vector3[] face = { p0, p1, p2, p3, p4, p5 };
        for (int i = 0; i < face.Length; i++)
            vertices.Add(face[i]);
        for (int i = 0; i < face.Length; i++)
            vertices.Add(face[i]);

        float worldPhaseU = PositiveFraction((patchCenter.x + basePosition.x) / 0.42f + uPhase);
        float worldPhaseV = PositiveFraction((patchCenter.z + basePosition.z) / 0.42f + vPhase);
        Vector2[] faceUv =
        {
            new Vector2(worldPhaseU, worldPhaseV),
            new Vector2(worldPhaseU + 0.045f, worldPhaseV),
            new Vector2(worldPhaseU, worldPhaseV + 0.12f),
            new Vector2(worldPhaseU + 0.045f, worldPhaseV + 0.12f),
            new Vector2(worldPhaseU + 0.021f, worldPhaseV + 0.24f),
            new Vector2(worldPhaseU + 0.024f, worldPhaseV + 0.24f),
        };
        for (int i = 0; i < faceUv.Length; i++) uv.Add(faceUv[i]);
        for (int i = 0; i < faceUv.Length; i++) uv.Add(faceUv[i]);

        AddRibbonTriangles(triangles, start, false);
        AddRibbonTriangles(triangles, start + 6, true);
    }

    private static void AddRibbonTriangles(List<int> triangles, int start, bool reverse)
    {
        int[] local = { 0, 2, 1, 1, 2, 3, 2, 4, 3, 3, 4, 5 };
        for (int i = 0; i < local.Length; i += 3)
        {
            if (!reverse)
            {
                triangles.Add(start + local[i]);
                triangles.Add(start + local[i + 1]);
                triangles.Add(start + local[i + 2]);
            }
            else
            {
                triangles.Add(start + local[i]);
                triangles.Add(start + local[i + 2]);
                triangles.Add(start + local[i + 1]);
            }
        }
    }

    private static Material EnsureBladeMaterial(Material source)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(BladeMaterialPath);
        if (material == null)
        {
            material = new Material(source.shader) { name = "MAT_GrassBladeField" };
            AssetDatabase.CreateAsset(material, BladeMaterialPath);
        }
        material.shader = source.shader;
        material.CopyPropertiesFromMaterial(source);
        material.name = "MAT_GrassBladeField";
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.22f);
        if (material.HasProperty("_GlossMapScale")) material.SetFloat("_GlossMapScale", 0.28f);
        if (material.HasProperty("_BumpScale")) material.SetFloat("_BumpScale", 0.65f);
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
        material.DisableKeyword("_EMISSION");
        EditorUtility.SetDirty(material);
        return material;
    }

    private static bool IsGrassEligible(float x, float z)
    {
        if (x < -FieldWidth * 0.5f + 0.08f || x > FieldWidth * 0.5f - 0.08f ||
            z < -FieldDepth * 0.5f + 0.08f || z > FieldDepth * 0.5f - 0.08f)
            return false;

        const float constructionMargin = 0.16f;
        return !Exclusions.Any(rect => rect.Contains(x, z, constructionMargin));
    }

    private static float NearestHardscapeDistance(float x, float z)
    {
        float best = float.PositiveInfinity;
        foreach (RectXZ rect in Exclusions)
            best = Mathf.Min(best, rect.EdgeDistance(x, z));
        return best;
    }

    private static string GeometrySignature(Mesh mesh)
    {
        Vector3[] v = mesh.vertices;
        if (v == null || v.Length == 0)
            return "empty";
        Vector3 a = v[0];
        Vector3 b = v[v.Length / 2];
        Vector3 c = v[v.Length - 1];
        Bounds bounds = mesh.bounds;
        return string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "{0}:{1:F4},{2:F4}:{3:F4},{4:F4}:{5:F4},{6:F4}:{7:F3},{8:F3}",
            mesh.vertexCount, a.x, a.z, b.x, b.z, c.x, c.z, bounds.size.x, bounds.size.z);
    }

    private static void SaveOrReplaceMesh(string path, Mesh generated)
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(generated, path);
            return;
        }
        EditorUtility.CopySerialized(generated, existing);
        existing.name = generated.name;
        EditorUtility.SetDirty(existing);
        UnityEngine.Object.DestroyImmediate(generated);
    }

    private static void ValidateContract()
    {
        if (!File.Exists(ContractPath))
            throw new InvalidOperationException($"Grass blade contract missing: {ContractPath}");
        GrassBladeContract contract = JsonUtility.FromJson<GrassBladeContract>(File.ReadAllText(ContractPath));
        if (contract == null || contract.lod == null || contract.material == null || contract.construction == null)
            throw new InvalidOperationException("Grass blade contract could not be parsed.");
        if (contract.runtimeRenderVerified)
            throw new InvalidOperationException("Grass blade contract may not claim render verification before actual Unity evidence exists.");
        if (contract.lod.levelCount != 4 || !contract.lod.crossFade)
            throw new InvalidOperationException("Grass blade contract must require four cross-faded LOD levels.");
        if (contract.material.metallic != 0f || contract.material.wetness != 0f)
            throw new InvalidOperationException("Dry midsummer grass contract must remain dielectric with zero current wetness.");
        if (contract.construction.bladeHeightMinMeters < 0.04f || contract.construction.bladeHeightMaxMeters > 0.20f ||
            contract.construction.bladeHeightMinMeters >= contract.construction.bladeHeightMaxMeters)
            throw new InvalidOperationException("Grass blade height contract is outside maintained-lawn scale.");
        if (string.IsNullOrWhiteSpace(contract.construction.substrateInterface) ||
            string.IsNullOrWhiteSpace(contract.construction.geometryVsMaterial) ||
            string.IsNullOrWhiteSpace(contract.material.angularFresnelResponse))
            throw new InvalidOperationException("Grass blade contract is missing installation/material reasoning.");
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (applying || scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        EnsureCurrentSceneIfReady();
    }

    private static void EnsureCurrentSceneIfReady()
    {
        if (applying || EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
            return;
        if (AssetDatabase.LoadAssetAtPath<Material>(SourceMaterialPath) == null || !File.Exists(ContractPath))
            return;

        GameObject ground = FindSceneObject("Ground");
        Transform root = ground != null ? ground.transform.Find(RootName) : null;
        if (root != null && root.Find(GeneratorMarker) != null)
            return;

        try
        {
            ApplyToOpenScene(false);
        }
        catch (Exception ex)
        {
            Debug.LogError("Grass blade field auto-apply failed before formal rendering: " + ex);
        }
    }

    private static void OnCameraPreCull(Camera camera)
    {
        if (camera == null || applying || !IsFormalCamera(camera))
            return;
        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
            return;
        // Formal camera callbacks are strictly read-only. If the source build did not produce the
        // blade field, fail closed instead of repairing geometry inside the evidence render.
        ValidateOpenScene(false);
    }

    private static bool IsFormalCamera(Camera camera)
    {
        if (camera.cameraType == CameraType.Reflection)
            return true;
        string name = camera.name ?? string.Empty;
        return name.StartsWith("QA4K_", StringComparison.Ordinal) ||
               name.StartsWith("QATemporal_", StringComparison.Ordinal) ||
               name.StartsWith("QAPreparedTemporal_", StringComparison.Ordinal);
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

    private static float Hash01(int a, int b, int c)
    {
        unchecked
        {
            uint x = (uint)(a * 73856093) ^ (uint)(b * 19349663) ^ (uint)(c * 83492791);
            x ^= x >> 13;
            x *= 1274126177u;
            x ^= x >> 16;
            return (x & 0x00FFFFFFu) / 16777215f;
        }
    }

    private static float PositiveFraction(float value)
    {
        return value - Mathf.Floor(value);
    }

    [Serializable]
    private sealed class GrassBladeContract
    {
        public bool runtimeRenderVerified;
        public GrassConstruction construction;
        public GrassMaterial material;
        public GrassLod lod;
    }

    [Serializable]
    private sealed class GrassConstruction
    {
        public float bladeHeightMinMeters;
        public float bladeHeightMaxMeters;
        public string substrateInterface;
        public string geometryVsMaterial;
    }

    [Serializable]
    private sealed class GrassMaterial
    {
        public float metallic;
        public float wetness;
        public string angularFresnelResponse;
    }

    [Serializable]
    private sealed class GrassLod
    {
        public int levelCount;
        public bool crossFade;
    }
}
