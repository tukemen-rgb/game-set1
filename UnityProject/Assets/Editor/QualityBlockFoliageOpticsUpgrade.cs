using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Applies a deterministic thin-leaf optical model to the generated high-detail trees.
/// The pass is intentionally scene-context driven: canopy height, radial exposure and the
/// benchmark solar direction affect leaf value while the shader handles two-sided diffuse,
/// modest transmitted sunlight, sky fill and shadow attenuation.
///
/// Leaf geometry remains explicit, so the same sprays that produce close-range silhouette
/// also cast physically located dappled shadows rather than relying on a painted blob shadow.
/// </summary>
public static class QualityBlockFoliageOpticsUpgrade
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string MaterialRoot = "Assets/Art/GeneratedPBR";
    private const string ShaderName = "NewTown/FoliageTransmission";
    private const string MasterPrefix = "HD_TreeMaster_";
    private const string DarkMaterialPath = MaterialRoot + "/PBR_LeafDark_Transmission.mat";
    private const string MidMaterialPath = MaterialRoot + "/PBR_LeafMid_Transmission.mat";

    private static readonly int ExposureBiasId = Shader.PropertyToID("_ExposureBias");
    private static readonly int LeafVariationId = Shader.PropertyToID("_LeafVariation");
    private static readonly int TransmissionStrengthId = Shader.PropertyToID("_TransmissionStrength");

    [MenuItem("NewTown/Lighting/Build Tree Foliage Optics + Dappled Shadow Pass")]
    public static void BuildFoliageOptics()
    {
        QualityBlockTreeDetailUpgrade.BuildDetailedTrees();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        QualityBlockTreeWoodyContinuityQA.ApplyToOpenScene();
        QualityBlockFoliageMorphologyVariationUpgrade.ApplyToOpenScene();
        EnsureMaterials(out Material dark, out Material mid);
        ApplyToOpenScene(dark, mid);
        ValidateOpenScene();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Tree foliage optics + morphology-diversity pass built with corrected continuous woody geometry. Actual transmitted-light balance, dapple density, silhouette repetition, bark continuity and shadow softness still require Unity render inspection.");
    }

    [MenuItem("NewTown/Lighting/Apply Tree Foliage Optics Only")]
    public static void ApplyToOpenScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        QualityBlockFoliageMorphologyVariationUpgrade.ApplyToOpenScene();
        EnsureMaterials(out Material dark, out Material mid);
        ApplyToOpenScene(dark, mid);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    [MenuItem("NewTown/QA/Validate Tree Foliage Optics")]
    public static void ValidateOpenScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        QualityBlockTreeWoodyContinuityQA.ValidateOpenScene();
        QualityBlockFoliageMorphologyDiversityQA.ValidateContractConfigOnly();
        QualityBlockFoliageMorphologyVariationUpgrade.ValidateCurrentScene(false);

        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
            throw new InvalidOperationException($"Required foliage shader {ShaderName} was not found.");

        QualityBlockEnvironmentContext[] contexts = Resources.FindObjectsOfTypeAll<QualityBlockEnvironmentContext>()
            .Where(x => x.gameObject.scene.IsValid())
            .ToArray();
        if (contexts.Length != 1)
            throw new InvalidOperationException($"Expected exactly one environment context, got {contexts.Length}.");
        SolarSample solar = contexts[0].CalculateSolarSample();

        Light[] sunLights = Resources.FindObjectsOfTypeAll<Light>()
            .Where(x => x.gameObject.scene.IsValid() && x.type == LightType.Directional && x.enabled)
            .ToArray();
        if (sunLights.Length == 0)
            throw new InvalidOperationException("No enabled directional sun light exists for foliage validation.");
        Light sun = sunLights.OrderByDescending(x => x.intensity).First();
        if (Vector3.Dot(sun.transform.forward.normalized, solar.RayDirection.normalized) < 0.995f)
            throw new InvalidOperationException("Foliage sun direction does not match the deterministic environment solar model.");
        if (sun.shadows != LightShadows.Soft)
            throw new InvalidOperationException("Benchmark foliage requires soft directional-light shadows.");

        QualityBlockArtSlot[] slots = Resources.FindObjectsOfTypeAll<QualityBlockArtSlot>()
            .Where(x => x.gameObject.scene.IsValid() && x.SlotId.StartsWith("vegetation.tree.", StringComparison.Ordinal))
            .OrderBy(x => x.SlotId, StringComparer.Ordinal)
            .ToArray();
        if (slots.Length != 6)
            throw new InvalidOperationException($"Expected six tree art slots, got {slots.Length}.");

        int validatedTrees = 0;
        foreach (QualityBlockArtSlot slot in slots)
        {
            if (slot.IsUsingAuthoredArt) continue;
            int treeIndex = ParseTreeIndex(slot.SlotId);
            Transform master = slot.FallbackRoot != null ? slot.FallbackRoot.transform.Find(MasterPrefix + treeIndex) : null;
            if (master == null)
                throw new InvalidOperationException($"Tree {treeIndex} detailed master missing before foliage validation.");

            MeshRenderer[] sourceLeaves = master.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r => r.gameObject.name.StartsWith("LeafCluster_", StringComparison.Ordinal))
                .ToArray();
            MeshRenderer[] allLeaves = master.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r => r.gameObject.name.Contains("LeafCluster_"))
                .ToArray();
            if (sourceLeaves.Length < 24)
                throw new InvalidOperationException($"Tree {treeIndex} has too few explicit source leaf clusters for dappled shadowing: {sourceLeaves.Length}.");
            if (allLeaves.Length <= sourceLeaves.Length)
                throw new InvalidOperationException($"Tree {treeIndex} foliage LOD proxies were not found.");

            var exposureValues = new HashSet<int>();
            foreach (MeshRenderer renderer in allLeaves)
            {
                Material material = renderer.sharedMaterial;
                if (material == null || material.shader != shader)
                    throw new InvalidOperationException($"Tree {treeIndex} leaf {renderer.name} does not use {ShaderName}.");
                if (renderer.shadowCastingMode != ShadowCastingMode.On || !renderer.receiveShadows)
                    throw new InvalidOperationException($"Tree {treeIndex} leaf {renderer.name} must cast and receive shadows.");

                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                float transmission = block.GetFloat(TransmissionStrengthId);
                float exposure = block.GetFloat(ExposureBiasId);
                if (transmission < 0.20f || transmission > 0.50f)
                    throw new InvalidOperationException($"Tree {treeIndex} leaf {renderer.name} has implausible transmission {transmission:F3}.");
                if (exposure < -0.13f || exposure > 0.10f)
                    throw new InvalidOperationException($"Tree {treeIndex} leaf {renderer.name} has out-of-range canopy exposure {exposure:F3}.");
                exposureValues.Add(Mathf.RoundToInt(exposure * 1000f));
            }

            if (exposureValues.Count < 4)
                throw new InvalidOperationException($"Tree {treeIndex} canopy exposure is too uniform; expected height/radial/sun-context variation.");
            validatedTrees++;
        }

        if (validatedTrees == 0)
            Debug.Log("All tree slots use authored replacements; generated foliage-optics validation was not applicable.");
        else
            Debug.Log($"Foliage optical structure validated for {validatedTrees} generated trees: deterministic sun alignment, two-sided transmission materials, morphology diversity, per-cluster exposure variation and explicit soft-shadow casters are present. Unity render verification remains pending.");
    }

    private static void ApplyToOpenScene(Material dark, Material mid)
    {
        QualityBlockEnvironmentContext context = Resources.FindObjectsOfTypeAll<QualityBlockEnvironmentContext>()
            .SingleOrDefault(x => x.gameObject.scene.IsValid());
        if (context == null)
            throw new InvalidOperationException("Foliage optics requires the benchmark environment context.");
        SolarSample solar = context.CalculateSolarSample();

        QualityBlockArtSlot[] slots = Resources.FindObjectsOfTypeAll<QualityBlockArtSlot>()
            .Where(x => x.gameObject.scene.IsValid() && x.SlotId.StartsWith("vegetation.tree.", StringComparison.Ordinal))
            .OrderBy(x => x.SlotId, StringComparer.Ordinal)
            .ToArray();

        foreach (QualityBlockArtSlot slot in slots)
        {
            if (slot.IsUsingAuthoredArt) continue;
            int treeIndex = ParseTreeIndex(slot.SlotId);
            Transform master = slot.FallbackRoot != null ? slot.FallbackRoot.transform.Find(MasterPrefix + treeIndex) : null;
            if (master == null)
                throw new InvalidOperationException($"Tree {treeIndex} detailed master missing before foliage optics pass.");

            MeshRenderer[] leaves = master.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r => r.gameObject.name.Contains("LeafCluster_"))
                .ToArray();
            if (leaves.Length == 0)
                throw new InvalidOperationException($"Tree {treeIndex} has no leaf renderers.");

            float minY = leaves.Min(r => r.bounds.center.y);
            float maxY = leaves.Max(r => r.bounds.center.y);
            float maxRadius = Mathf.Max(0.1f, leaves.Max(r => HorizontalDistance(r.bounds.center, master.position)));

            foreach (MeshRenderer renderer in leaves)
            {
                int ordinal = ParseLeafOrdinal(renderer.gameObject.name);
                bool useDark = ((treeIndex + ordinal) & 1) == 0;
                renderer.sharedMaterial = useDark ? dark : mid;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;

                float heightExposure = Mathf.InverseLerp(minY, Mathf.Max(minY + 0.01f, maxY), renderer.bounds.center.y);
                Vector3 outward = renderer.bounds.center - master.position;
                Vector3 outwardHorizontal = new Vector3(outward.x, 0f, outward.z);
                Vector3 sunHorizontal = new Vector3(solar.DirectionToSun.x, 0f, solar.DirectionToSun.z);
                float sunSide = 0.5f;
                if (outwardHorizontal.sqrMagnitude > 0.0001f && sunHorizontal.sqrMagnitude > 0.0001f)
                    sunSide = Vector3.Dot(outwardHorizontal.normalized, sunHorizontal.normalized) * 0.5f + 0.5f;
                float radialExposure = Mathf.Clamp01(HorizontalDistance(renderer.bounds.center, master.position) / maxRadius);
                float exposure = Mathf.Clamp01(0.16f + heightExposure * 0.44f + radialExposure * 0.24f + sunSide * 0.16f);

                float variation = Mathf.Lerp(-0.045f, 0.045f, Hash01(treeIndex, ordinal, 811));
                float transmission = Mathf.Lerp(0.27f, 0.40f, exposure) + variation * 0.22f;
                float exposureBias = Mathf.Lerp(-0.105f, 0.075f, exposure);

                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetFloat(ExposureBiasId, exposureBias);
                block.SetFloat(LeafVariationId, variation);
                block.SetFloat(TransmissionStrengthId, Mathf.Clamp(transmission, 0.24f, 0.43f));
                renderer.SetPropertyBlock(block);
            }
        }
    }

    private static void EnsureMaterials(out Material dark, out Material mid)
    {
        Directory.CreateDirectory(MaterialRoot);
        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
            throw new InvalidOperationException($"Foliage shader {ShaderName} was not found. Ensure the shader asset imports before applying this pass.");

        Material sourceDark = AssetDatabase.LoadAssetAtPath<Material>(MaterialRoot + "/PBR_LeafDark.mat");
        Material sourceMid = AssetDatabase.LoadAssetAtPath<Material>(MaterialRoot + "/PBR_LeafMid.mat");
        if (sourceDark == null || sourceMid == null)
            throw new InvalidOperationException("Generated PBR leaf source materials are missing.");

        dark = CreateOrUpdateMaterial(DarkMaterialPath, "PBR_LeafDark_Transmission", sourceDark, shader,
            new Color(0.43f, 0.69f, 0.22f, 1f), 0.33f, 0.24f);
        mid = CreateOrUpdateMaterial(MidMaterialPath, "PBR_LeafMid_Transmission", sourceMid, shader,
            new Color(0.49f, 0.74f, 0.25f, 1f), 0.35f, 0.23f);
        AssetDatabase.SaveAssets();
    }

    private static Material CreateOrUpdateMaterial(string path, string name, Material source, Shader shader,
        Color transmissionColor, float transmissionStrength, float smoothnessScale)
    {
        Material target = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (target == null)
        {
            target = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(target, path);
        }
        else
        {
            target.shader = shader;
            target.name = name;
        }

        target.color = Color.white;
        target.mainTexture = source.mainTexture;
        target.mainTextureScale = source.mainTextureScale;
        target.mainTextureOffset = source.mainTextureOffset;
        if (source.HasProperty("_BumpMap")) target.SetTexture("_BumpMap", source.GetTexture("_BumpMap"));
        if (source.HasProperty("_MetallicGlossMap")) target.SetTexture("_MetallicGlossMap", source.GetTexture("_MetallicGlossMap"));
        target.SetColor("_TransmissionColor", transmissionColor);
        target.SetFloat("_TransmissionStrength", transmissionStrength);
        target.SetFloat("_Wrap", 0.27f);
        target.SetFloat("_SmoothnessScale", smoothnessScale);
        target.enableInstancing = true;
        target.doubleSidedGI = true;
        EditorUtility.SetDirty(target);
        return target;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private static int ParseTreeIndex(string slotId)
    {
        int dot = slotId.LastIndexOf('.');
        if (dot < 0 || !int.TryParse(slotId.Substring(dot + 1), out int value))
            throw new InvalidOperationException($"Invalid tree slot id {slotId}.");
        return value;
    }

    private static int ParseLeafOrdinal(string name)
    {
        const string marker = "LeafCluster_";
        int start = name.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0) throw new InvalidOperationException($"Leaf renderer name lacks {marker}: {name}");
        start += marker.Length;
        int end = start;
        while (end < name.Length && char.IsDigit(name[end])) end++;
        if (end == start || !int.TryParse(name.Substring(start, end - start), out int ordinal))
            throw new InvalidOperationException($"Cannot parse leaf ordinal from {name}.");
        return ordinal;
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
}
