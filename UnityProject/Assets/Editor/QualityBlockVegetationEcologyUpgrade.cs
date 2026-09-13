using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Adds a cause-based vegetation ground layer around the six benchmark trees without changing
/// gameplay collision. Paved tree anchors receive maintained soil pits and precast edging; all
/// anchors receive sparse low understory whose density is reduced toward the nearest pedestrian
/// pressure source. The pass reuses authored tree-detail meshes rather than Unity primitives and
/// supplies four renderer-distinct LOD levels for the added biological detail.
/// </summary>
public static class QualityBlockVegetationEcologyUpgrade
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string RootName = "VegetationEcologyDetail";
    private const string MeshRoot = "Assets/Art/GeneratedTreeMeshes";
    private const string LeafMaterialPath = "Assets/Art/GeneratedPBR/PBR_LeafMid_Transmission.mat";
    private const string StemMaterialPath = "Assets/Art/GeneratedPBR/PBR_LeafDark_Transmission.mat";
    private const string SoilMaterialPath = "Assets/Art/GeneratedPBR/PBR_DrySoil.mat";
    private const string CurbMaterialPath = "Assets/Art/GeneratedPBR/PBR_WashedConcrete.mat";

    private static readonly Vector3[] PressureCenters =
    {
        new(-8f, 0f, 1f),       // danchi plaza circulation
        new(8.7f, 0f, 0f),      // park path
        new(2.5f, 0f, -5.8f),   // worn shortcut A
        new(13f, 0f, 7.5f),     // worn shortcut B
    };

    private static readonly float[] LodTransitions = { 0.18f, 0.08f, 0.035f, 0.012f };

    [MenuItem("NewTown/Vegetation/Apply Tree-Pit + Understory Ecology Pass")]
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
        Debug.Log("Vegetation ecology detail applied. Actual density, silhouette and LOD appearance remain pending native 4K Unity review.");
    }

    public static void ApplyToOpenScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject sceneRoot = FindSceneObject("QualityBlock1990s");
        if (sceneRoot == null)
            throw new InvalidOperationException("QualityBlock1990s root not found for vegetation ecology pass.");

        QualityBlockArtSlot[] treeSlots = Resources.FindObjectsOfTypeAll<QualityBlockArtSlot>()
            .Where(x => x.gameObject.scene.IsValid() && x.SlotId.StartsWith("vegetation.tree.", StringComparison.Ordinal))
            .OrderBy(x => x.SlotId, StringComparer.Ordinal)
            .ToArray();
        if (treeSlots.Length != 6)
            throw new InvalidOperationException($"Vegetation ecology requires six tree anchors, got {treeSlots.Length}.");

        Material leaf = AssetDatabase.LoadAssetAtPath<Material>(LeafMaterialPath);
        Material stem = AssetDatabase.LoadAssetAtPath<Material>(StemMaterialPath);
        Material soil = AssetDatabase.LoadAssetAtPath<Material>(SoilMaterialPath);
        Material curb = AssetDatabase.LoadAssetAtPath<Material>(CurbMaterialPath);
        if (leaf == null || stem == null || soil == null || curb == null)
            throw new InvalidOperationException("Vegetation ecology requires generated foliage, soil and washed-concrete materials.");
        if (leaf.shader == null || leaf.shader.name != "NewTown/FoliageTransmission")
            throw new InvalidOperationException("Vegetation ecology leaf material must use NewTown/FoliageTransmission.");

        GameObject previous = FindSceneObject(RootName);
        if (previous != null)
            UnityEngine.Object.DestroyImmediate(previous);

        var root = new GameObject(RootName);
        root.transform.SetParent(sceneRoot.transform, false);

        for (int i = 0; i < treeSlots.Length; i++)
        {
            Vector3 anchor = treeSlots[i].transform.position;
            bool paved = IsInsideDanchiPlaza(anchor);
            float pitRadius = paved ? 0.74f + 0.06f * (i % 3) : 0f;

            var patch = new GameObject($"EcologyPatch_{i}");
            patch.transform.SetParent(root.transform, false);
            patch.transform.position = anchor;

            if (paved)
                BuildMaintainedTreePit(patch.transform, i, pitRadius, soil, curb);

            Vector3 pressureDirection = NearestPressureDirection(anchor);
            BuildUnderstoryLods(patch.transform, i, paved, pitRadius, pressureDirection, stem, leaf);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    [MenuItem("NewTown/QA/Validate Tree-Pit + Understory Ecology")]
    public static void ValidateOpenScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject root = FindSceneObject(RootName);
        if (root == null)
            throw new InvalidOperationException("Vegetation ecology root is missing.");

        Transform[] patches = Enumerable.Range(0, 6)
            .Select(i => root.transform.Find($"EcologyPatch_{i}"))
            .ToArray();
        if (patches.Any(x => x == null))
            throw new InvalidOperationException("Vegetation ecology must contain all six deterministic tree patches.");

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        if (colliders.Length != 0)
            throw new InvalidOperationException($"Vegetation ecology art must remain collider-separated; found {colliders.Length} colliders.");

        int pavedPitCount = 0;
        var lod0Counts = new HashSet<int>();
        for (int i = 0; i < patches.Length; i++)
        {
            Transform patch = patches[i];
            bool paved = IsInsideDanchiPlaza(patch.position);
            Transform pit = patch.Find("MaintainedTreePit");
            if (paved)
            {
                pavedPitCount++;
                if (pit == null)
                    throw new InvalidOperationException($"Paved tree {i} is missing its maintained soil pit.");
                MeshRenderer soilRenderer = pit.Find("SoilDisk")?.GetComponent<MeshRenderer>();
                if (soilRenderer == null || soilRenderer.sharedMaterial == null)
                    throw new InvalidOperationException($"Tree pit {i} soil disk/material missing.");
                if (soilRenderer.sharedMaterial.HasProperty("_Metallic") && soilRenderer.sharedMaterial.GetFloat("_Metallic") > 0.02f)
                    throw new InvalidOperationException($"Tree pit {i} soil is incorrectly metallic.");
                int edgingModules = pit.GetComponentsInChildren<MeshRenderer>(true)
                    .Count(r => r.gameObject.name.StartsWith("PitEdge_", StringComparison.Ordinal));
                if (edgingModules != 12)
                    throw new InvalidOperationException($"Tree pit {i} expected 12 precast edging modules, got {edgingModules}.");
            }
            else if (pit != null)
            {
                throw new InvalidOperationException($"Grass-ground tree {i} should not receive an artificial paved tree-pit collar.");
            }

            Transform understory = patch.Find("UnderstoryLOD");
            if (understory == null)
                throw new InvalidOperationException($"Tree {i} understory LOD root missing.");
            LODGroup group = understory.GetComponent<LODGroup>();
            if (group == null)
                throw new InvalidOperationException($"Tree {i} understory LODGroup missing.");
            LOD[] lods = group.GetLODs();
            if (lods.Length != 4)
                throw new InvalidOperationException($"Tree {i} understory expected four LODs, got {lods.Length}.");
            int[] counts = lods.Select(x => x.renderers.Length).ToArray();
            if (!(counts[0] > counts[1] && counts[1] > counts[2] && counts[2] > counts[3] && counts[3] > 0))
                throw new InvalidOperationException($"Tree {i} understory LOD reduction invalid: {string.Join("/", counts)}.");
            if (group.fadeMode != LODFadeMode.CrossFade || !group.animateCrossFading)
                throw new InvalidOperationException($"Tree {i} understory must use animated cross-fade.");
            lod0Counts.Add(counts[0]);

            foreach (MeshFilter filter in patch.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                    throw new InvalidOperationException($"Vegetation ecology mesh missing on {filter.name}.");
                string meshName = filter.sharedMesh.name;
                bool allowed = meshName.StartsWith("GM_HD_", StringComparison.Ordinal) ||
                               meshName.StartsWith("GM_TreeTwig", StringComparison.Ordinal) ||
                               meshName.StartsWith("GM_LeafSpray", StringComparison.Ordinal);
                if (!allowed || meshName == "Cube" || meshName == "Cylinder" || meshName == "Sphere")
                    throw new InvalidOperationException($"Vegetation ecology uses prohibited/unknown mesh {meshName} on {filter.name}.");
            }
        }

        if (pavedPitCount != 4)
            throw new InvalidOperationException($"Expected four paved-plaza tree pits from benchmark anchors, got {pavedPitCount}.");
        if (lod0Counts.Count < 4)
            throw new InvalidOperationException("Understory density is too repetitive; expected at least four distinct LOD0 renderer counts across six trees.");

        Debug.Log("Vegetation ecology structural QA passed: six cause-based patches, four maintained paved tree pits, non-primitive detail meshes and four-level cross-faded understory LODs. Native render verification remains pending.");
    }

    private static void BuildMaintainedTreePit(Transform parent, int treeIndex, float pitRadius, Material soil, Material curb)
    {
        var pit = new GameObject("MaintainedTreePit");
        pit.transform.SetParent(parent, false);

        float diameter = pitRadius * 2f;
        Mesh soilMesh = QualityBlockDetailMeshLibrary.GetBeveledCylinder(
            new Vector3(diameter, 0.012f, diameter), false);
        var disk = new GameObject("SoilDisk");
        disk.transform.SetParent(pit.transform, false);
        disk.transform.localPosition = new Vector3(0f, 0.058f, 0f);
        disk.transform.localRotation = Quaternion.Euler(0f, treeIndex * 17f, 0f);
        var diskFilter = disk.AddComponent<MeshFilter>();
        diskFilter.sharedMesh = soilMesh;
        var diskRenderer = disk.AddComponent<MeshRenderer>();
        diskRenderer.sharedMaterial = soil;
        diskRenderer.shadowCastingMode = ShadowCastingMode.On;
        diskRenderer.receiveShadows = true;

        const int modules = 12;
        float moduleRadius = pitRadius + 0.075f;
        float moduleLength = (Mathf.PI * 2f * moduleRadius / modules) * 0.82f;
        Mesh edgeMesh = QualityBlockDetailMeshLibrary.GetChamferedBox(new Vector3(0.15f, 0.08f, moduleLength));
        for (int m = 0; m < modules; m++)
        {
            float angle = Mathf.PI * 2f * m / modules;
            Vector3 radial = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 tangent = new(-radial.z, 0f, radial.x);
            var edge = new GameObject($"PitEdge_{m:00}");
            edge.transform.SetParent(pit.transform, false);
            edge.transform.localPosition = radial * moduleRadius + Vector3.up * 0.082f;
            edge.transform.localRotation = Quaternion.LookRotation(tangent, Vector3.up);
            var filter = edge.AddComponent<MeshFilter>();
            filter.sharedMesh = edgeMesh;
            var renderer = edge.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = curb;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }
    }

    private static void BuildUnderstoryLods(Transform parent, int treeIndex, bool paved, float pitRadius,
        Vector3 pressureDirection, Material stem, Material leaf)
    {
        var lodRoot = new GameObject("UnderstoryLOD");
        lodRoot.transform.SetParent(parent, false);

        int basePlantCount = paved ? 7 + (treeIndex % 3) : 16 + (treeIndex % 4) * 2;
        float[] ratios = { 1f, 0.60f, 0.32f, 0.14f };
        var lods = new LOD[4];

        for (int lod = 0; lod < 4; lod++)
        {
            var level = new GameObject($"LOD{lod}_Plants");
            level.transform.SetParent(lodRoot.transform, false);
            int targetPlants = Mathf.Max(1, Mathf.RoundToInt(basePlantCount * ratios[lod]));
            var renderers = new List<Renderer>();
            int accepted = 0;
            int candidate = 0;
            int safety = targetPlants * 8 + 16;
            while (accepted < targetPlants && candidate < safety)
            {
                Vector3 local = CandidatePlantPosition(treeIndex, candidate, paved, pitRadius);
                Vector3 dir = new Vector3(local.x, 0f, local.z);
                float pressureSide = dir.sqrMagnitude > 0.0001f && pressureDirection.sqrMagnitude > 0.0001f
                    ? Vector3.Dot(dir.normalized, pressureDirection.normalized)
                    : 0f;
                float keepProbability = Mathf.Lerp(0.88f, 0.38f, Mathf.Clamp01(pressureSide * 0.5f + 0.5f));
                bool keep = Hash01(treeIndex, candidate, 907) <= keepProbability || candidate < 2;
                if (keep)
                {
                    CreatePlant(level.transform, treeIndex, candidate, accepted, lod, local, stem, leaf, renderers);
                    accepted++;
                }
                candidate++;
            }

            if (accepted != targetPlants || renderers.Count == 0)
                throw new InvalidOperationException($"Tree {treeIndex} LOD{lod} could not build required understory density {accepted}/{targetPlants}.");
            lods[lod] = new LOD(LodTransitions[lod], renderers.ToArray());
        }

        var group = lodRoot.AddComponent<LODGroup>();
        group.SetLODs(lods);
        group.fadeMode = LODFadeMode.CrossFade;
        group.animateCrossFading = true;
        group.RecalculateBounds();
    }

    private static void CreatePlant(Transform parent, int treeIndex, int candidate, int ordinal, int lod,
        Vector3 localPosition, Material stem, Material leaf, List<Renderer> renderers)
    {
        var plant = new GameObject($"Plant_{ordinal:00}_C{candidate:00}");
        plant.transform.SetParent(parent, false);
        plant.transform.localPosition = localPosition;
        float yaw = Hash01(treeIndex, candidate, 311) * 360f;
        plant.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

        float height = Mathf.Lerp(0.18f, 0.42f, Hash01(treeIndex, candidate, 313));
        float spread = Mathf.Lerp(0.10f, 0.18f, Hash01(treeIndex, candidate, 317));

        if (lod <= 1)
        {
            var stemGo = new GameObject("Stem");
            stemGo.transform.SetParent(plant.transform, false);
            stemGo.transform.localPosition = Vector3.up * 0.004f;
            Vector3 lean = new(
                HashSigned(treeIndex, candidate, 401) * 0.08f,
                height,
                HashSigned(treeIndex, candidate, 409) * 0.08f);
            stemGo.transform.localRotation = Quaternion.FromToRotation(Vector3.up, lean.normalized);
            stemGo.transform.localScale = new Vector3(0.007f, lean.magnitude, 0.007f);
            var stemFilter = stemGo.AddComponent<MeshFilter>();
            stemFilter.sharedMesh = LoadTreeMesh("GM_TreeTwig");
            var stemRenderer = stemGo.AddComponent<MeshRenderer>();
            stemRenderer.sharedMaterial = stem;
            stemRenderer.shadowCastingMode = ShadowCastingMode.On;
            stemRenderer.receiveShadows = true;
            renderers.Add(stemRenderer);
        }

        var leafGo = new GameObject("LeafRosette");
        leafGo.transform.SetParent(plant.transform, false);
        leafGo.transform.localPosition = Vector3.up * (height * (lod == 0 ? 0.92f : 0.78f));
        leafGo.transform.localRotation = Quaternion.Euler(
            Mathf.Lerp(-14f, 10f, Hash01(treeIndex, candidate, 503)),
            Hash01(treeIndex, candidate, 509) * 360f,
            Mathf.Lerp(-10f, 12f, Hash01(treeIndex, candidate, 521)));
        float lodScale = lod == 0 ? 1f : lod == 1 ? 0.92f : lod == 2 ? 0.78f : 0.64f;
        leafGo.transform.localScale = Vector3.one * spread * lodScale;
        var leafFilter = leafGo.AddComponent<MeshFilter>();
        leafFilter.sharedMesh = LoadTreeMesh($"GM_LeafSpray_{(char)('A' + ((treeIndex + candidate) % 3))}");
        var leafRenderer = leafGo.AddComponent<MeshRenderer>();
        leafRenderer.sharedMaterial = leaf;
        leafRenderer.shadowCastingMode = ShadowCastingMode.On;
        leafRenderer.receiveShadows = true;
        renderers.Add(leafRenderer);
    }

    private static Vector3 CandidatePlantPosition(int treeIndex, int candidate, bool paved, float pitRadius)
    {
        float angle = Hash01(treeIndex, candidate, 601) * Mathf.PI * 2f;
        float radial01 = Mathf.Sqrt(Hash01(treeIndex, candidate, 607));
        float minRadius = paved ? 0.18f : 0.38f;
        float maxRadius = paved ? Mathf.Max(minRadius + 0.05f, pitRadius * 0.78f) : 1.28f;
        float radius = Mathf.Lerp(minRadius, maxRadius, radial01);
        float jitter = HashSigned(treeIndex, candidate, 613) * 0.035f;
        float y = paved ? 0.072f : 0.010f;
        return new Vector3(Mathf.Cos(angle) * (radius + jitter), y, Mathf.Sin(angle) * (radius - jitter));
    }

    private static Vector3 NearestPressureDirection(Vector3 anchor)
    {
        Vector3 best = Vector3.forward;
        float bestSq = float.PositiveInfinity;
        foreach (Vector3 center in PressureCenters)
        {
            Vector3 delta = center - anchor;
            delta.y = 0f;
            float sq = delta.sqrMagnitude;
            if (sq < bestSq)
            {
                bestSq = sq;
                best = delta;
            }
        }
        return best.sqrMagnitude > 0.0001f ? best.normalized : Vector3.forward;
    }

    private static bool IsInsideDanchiPlaza(Vector3 p)
    {
        return p.x >= -20.05f && p.x <= 4.05f && p.z >= -5.55f && p.z <= 7.55f;
    }

    private static Mesh LoadTreeMesh(string name)
    {
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshRoot}/{name}.asset");
        if (mesh == null)
            throw new InvalidOperationException($"Required authored vegetation mesh missing: {name}.");
        return mesh;
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

    private static float HashSigned(int a, int b, int c) => Hash01(a, b, c) * 2f - 1f;
}
