using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// High-detail ground/infrastructure pass for the generated 1990s new-town benchmark.
///
/// The base scene used broad paving/soil cubes, which left the building and park visually floating
/// on undifferentiated planes. This pass reconstructs the ground as assembled infrastructure:
/// planned paving joints, individual precast curb modules and construction gaps, a recessed linear
/// drain with removable grate panels and real bars, utility-cover frame/cover/slot assemblies, and
/// small soil-path edging blocks. Generated visual detail is collider-free; the existing coarse
/// ground remains the gameplay collision layer until an authored collision replacement is supplied.
///
/// Weathering metadata is attached to physical sources and interfaces rather than painted randomly.
/// The generated assembly is also wrapped in an art slot so PF_GroundInfrastructure_A can replace
/// the fallback without changing gameplay or benchmark coordinates.
/// </summary>
public static class QualityBlockGroundDetailUpgrade
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string MaterialRoot = "Assets/Art/GeneratedGroundMaterials";
    private const string LookdevPath = "Assets/QA/ground_construction_lookdev.json";
    private const string SlotName = "ARTSLOT_GroundInfrastructure";
    private const string FallbackName = "GroundHighDetail";
    private const string ExpectedReplacement = "PF_GroundInfrastructure_A";

    private const int DrainPanelCount = 36;
    private const float DrainPitch = 0.60f;
    private const float DrainPanelVisibleLength = 0.588f;
    private const float DrainWidth = 0.34f;
    private const float DrainStartCenterX = -18.5f;
    private const float DrainCenterZ = -5.45f;

    [MenuItem("NewTown/Geometry/Build High-Detail Ground Infrastructure + LODs")]
    public static void BuildDetailedGround()
    {
        // Preserve the existing quality chain. This rebuilds the benchmark, PBR materials, danchi,
        // LODs, trees and foliage optics before the ground infrastructure is applied.
        QualityBlockFoliageOpticsUpgrade.BuildFoliageOptics();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        ApplyToOpenScene();
        ValidateOpenScene();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("High-detail ground infrastructure built. Native Unity 4K render verification remains pending.");
    }

    [MenuItem("NewTown/Geometry/Apply High-Detail Ground Infrastructure Only")]
    public static void ApplyToOpenScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject sceneRoot = FindSceneObject("QualityBlock1990s");
        if (sceneRoot == null)
            throw new InvalidOperationException("QualityBlock1990s root not found.");
        if (FindSceneObject("Ground") == null)
            throw new InvalidOperationException("Ground root not found.");

        GameObject previousSlot = FindSceneObject(SlotName);
        if (previousSlot != null)
            UnityEngine.Object.DestroyImmediate(previousSlot);
        GameObject strayFallback = FindSceneObject(FallbackName);
        if (strayFallback != null)
            UnityEngine.Object.DestroyImmediate(strayFallback);

        EnsureMaterials(
            out Material paving,
            out Material curb,
            out Material grate,
            out Material castIron,
            out Material joint,
            out Material dampChannel);

        ApplyBaseGroundMaterials(paving);

        Transform slotParent = FindSceneObject("ArtSlots")?.transform ?? sceneRoot.transform;
        var slotGo = new GameObject(SlotName);
        slotGo.transform.SetParent(slotParent, false);

        var fallback = new GameObject(FallbackName);
        fallback.transform.SetParent(slotGo.transform, false);
        var artRoot = new GameObject("GroundHighDetail_Art");
        artRoot.transform.SetParent(fallback.transform, false);

        int pavingJointCount = BuildPavingJoints(artRoot.transform, joint);
        int curbModuleCount = BuildCurbsAndEdging(artRoot.transform, curb);
        BuildDrainage(artRoot.transform, curb, grate, dampChannel,
            out int drainPanelCount, out int drainBarCount, out int lod0RendererCount);
        int utilityCoverCount = BuildUtilityCovers(artRoot.transform, castIron, joint);

        var manifest = fallback.AddComponent<QualityBlockGroundDetailManifest>();
        manifest.Configure(
            pavingJointCount,
            curbModuleCount,
            drainPanelCount,
            drainBarCount,
            utilityCoverCount,
            lod0RendererCount);

        var slot = slotGo.AddComponent<QualityBlockArtSlot>();
        slot.Configure("ground.infrastructure", ExpectedReplacement, fallback);
        AttachReplacementIfAvailable(slot);

        EditorUtility.SetDirty(manifest);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    [MenuItem("NewTown/QA/Validate High-Detail Ground Infrastructure")]
    public static void ValidateOpenScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        ValidateLookdevRegistry();

        QualityBlockArtSlot slot = Resources.FindObjectsOfTypeAll<QualityBlockArtSlot>()
            .FirstOrDefault(x => x.gameObject.scene.IsValid() && x.SlotId == "ground.infrastructure");
        if (slot == null)
            throw new InvalidOperationException("Ground infrastructure art slot is missing.");
        if (slot.ExpectedAssetName != ExpectedReplacement)
            throw new InvalidOperationException($"Ground art slot expected asset mismatch: {slot.ExpectedAssetName}.");
        if (slot.FallbackRoot == null)
            throw new InvalidOperationException("Ground infrastructure fallback root is missing.");

        var manifest = slot.FallbackRoot.GetComponent<QualityBlockGroundDetailManifest>();
        if (manifest == null)
            throw new InvalidOperationException("Ground detail manifest is missing.");
        if (manifest.PavingJointCount < 17)
            throw new InvalidOperationException($"Expected at least 17 physical paving joints, got {manifest.PavingJointCount}.");
        if (manifest.CurbModuleCount < 130)
            throw new InvalidOperationException($"Ground curb/edging module count unexpectedly low: {manifest.CurbModuleCount}.");
        if (manifest.DrainPanelCount != DrainPanelCount)
            throw new InvalidOperationException($"Expected {DrainPanelCount} drain panels, got {manifest.DrainPanelCount}.");
        if (manifest.DrainBarCount < 250)
            throw new InvalidOperationException($"LOD0 drainage grate bar count unexpectedly low: {manifest.DrainBarCount}.");
        if (manifest.UtilityCoverCount != 3)
            throw new InvalidOperationException($"Expected three utility covers, got {manifest.UtilityCoverCount}.");
        if (manifest.Lod0RendererCount < 300)
            throw new InvalidOperationException($"Drain LOD0 renderer count unexpectedly low: {manifest.Lod0RendererCount}.");

        Collider[] generatedColliders = slot.FallbackRoot.GetComponentsInChildren<Collider>(true);
        if (generatedColliders.Length != 0)
            throw new InvalidOperationException(
                $"Generated ground art must remain collider-separated; found {generatedColliders.Length} colliders under the detail fallback.");

        MeshFilter[] filters = slot.FallbackRoot.GetComponentsInChildren<MeshFilter>(true);
        if (filters.Length < 450)
            throw new InvalidOperationException($"Ground authored-mesh part count unexpectedly low: {filters.Length}.");
        foreach (MeshFilter filter in filters)
        {
            if (filter.sharedMesh == null)
                throw new InvalidOperationException($"Ground mesh missing on {filter.name}.");
            if (!filter.sharedMesh.name.StartsWith("GM_HD_", StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Generated ground part {filter.name} uses non-authored/primitive mesh {filter.sharedMesh.name}.");
        }

        LODGroup drainLod = Resources.FindObjectsOfTypeAll<LODGroup>()
            .FirstOrDefault(x => x.gameObject.scene.IsValid() && x.name == "HD_DrainageGrates_LOD");
        if (drainLod == null)
            throw new InvalidOperationException("Drainage LODGroup is missing.");
        if (drainLod.GetLODs().Length != 4)
            throw new InvalidOperationException($"Expected four drain LOD levels, got {drainLod.GetLODs().Length}.");
        if (drainLod.fadeMode != LODFadeMode.CrossFade)
            throw new InvalidOperationException("Drainage LOD must use cross-fade to reduce visible popping.");

        int weatheringMetadata = slot.FallbackRoot.GetComponentsInChildren<QualityBlockWeatheringSurface>(true).Length;
        if (weatheringMetadata < 150)
            throw new InvalidOperationException(
                $"Ground cause-based weathering metadata count unexpectedly low: {weatheringMetadata}.");

        Debug.Log(
            $"Ground detail validation passed structurally: joints={manifest.PavingJointCount}, curb modules={manifest.CurbModuleCount}, " +
            $"drain panels={manifest.DrainPanelCount}, LOD0 bars={manifest.DrainBarCount}, utility covers={manifest.UtilityCoverCount}. " +
            "Actual 4K material/lighting fidelity remains unscored until Unity renders are inspected.");
    }

    [MenuItem("NewTown/QA/Validate Ground Construction Lookdev Registry")]
    public static void ValidateLookdevRegistry()
    {
        if (!File.Exists(LookdevPath))
            throw new InvalidOperationException($"Ground construction/lookdev registry missing: {LookdevPath}");

        string json = File.ReadAllText(LookdevPath);
        string[] prohibitedThemes = { "earthquake", "disaster", "reconstruction" };
        foreach (string token in prohibitedThemes)
        {
            // The QA rule itself may state that a theme is prohibited. Only reject accidental positive
            // scene-content declarations; the canonical phrase below is intentionally allowed.
            string positiveToken = $"{token} theme";
            if (json.IndexOf(positiveToken, StringComparison.OrdinalIgnoreCase) >= 0)
                throw new InvalidOperationException($"Ground registry contains prohibited theme content: {positiveToken}.");
        }

        GroundLookdevRegistry registry = JsonUtility.FromJson<GroundLookdevRegistry>(json);
        if (registry == null)
            throw new InvalidOperationException("Ground construction/lookdev registry could not be parsed.");
        if (registry.materials == null || registry.materials.Length < 6)
            throw new InvalidOperationException("Ground registry must define at least six materially distinct surfaces.");
        if (registry.assemblies == null || registry.assemblies.Length < 4)
            throw new InvalidOperationException("Ground registry must define paving, curb, drainage and utility-cover assemblies.");

        foreach (GroundMaterialSpec material in registry.materials)
        {
            RequireText(material.id, "ground material id");
            RequireText(material.materialFamily, $"materialFamily for {material.id}");
            RequireText(material.finish, $"finish for {material.id}");
            RequireText(material.frontLightResponse, $"frontLightResponse for {material.id}");
            RequireText(material.grazingLightResponse, $"grazingLightResponse for {material.id}");
            RequireText(material.shadeResponse, $"shadeResponse for {material.id}");

            if (material.roughnessMin < 0f || material.roughnessMax > 1f || material.roughnessMin > material.roughnessMax)
                throw new InvalidOperationException($"Invalid roughness range for {material.id}.");
            if (material.metallicMin < 0f || material.metallicMax > 1f || material.metallicMin > material.metallicMax)
                throw new InvalidOperationException($"Invalid metallic range for {material.id}.");
            if (material.wetAlbedoMultiplier < 0.65f || material.wetAlbedoMultiplier > 1.0f)
                throw new InvalidOperationException($"Implausible wet albedo multiplier for {material.id}: {material.wetAlbedoMultiplier}.");
            if (material.wetRoughnessMultiplier <= 0f || material.wetRoughnessMultiplier >= 1f)
                throw new InvalidOperationException($"Wet roughness must decrease for {material.id}.");

            bool cementitious = material.id.Contains("concrete") || material.id.Contains("paving") || material.id.Contains("joint");
            if (cementitious && material.metallicMax > 0.05f)
                throw new InvalidOperationException($"Impossible metallic concrete/mineral material in ground registry: {material.id}.");
        }

        GroundMaterialSpec grate = registry.materials.FirstOrDefault(x => x.id == "galvanized_drain_grate");
        if (grate == null || grate.metallicMin < 0.65f)
            throw new InvalidOperationException("Galvanized drainage grate must retain physically metallic response.");
        GroundMaterialSpec cover = registry.materials.FirstOrDefault(x => x.id == "cast_iron_utility_cover");
        if (cover == null || cover.metallicMin < 0.60f)
            throw new InvalidOperationException("Cast-iron utility cover must retain physically metallic response.");

        foreach (GroundAssemblySpec assembly in registry.assemblies)
        {
            RequireText(assembly.id, "ground assembly id");
            RequireText(assembly.nominalDimensions, $"nominalDimensions for {assembly.id}");
            RequireText(assembly.manufacture, $"manufacture for {assembly.id}");
            RequireText(assembly.mounting, $"mounting for {assembly.id}");
            RequireText(assembly.interfaces, $"interfaces for {assembly.id}");
            RequireText(assembly.orientationExposure, $"orientationExposure for {assembly.id}");
            RequireText(assembly.aging, $"aging for {assembly.id}");
            RequireText(assembly.geometryVsMaterial, $"geometryVsMaterial for {assembly.id}");
            RequireText(assembly.lookdevBrief, $"lookdevBrief for {assembly.id}");
        }

        Debug.Log(
            $"Ground construction/lookdev registry valid: materials={registry.materials.Length}, assemblies={registry.assemblies.Length}. " +
            $"runtimeRenderVerified={registry.runtimeRenderVerified}.");
    }

    private static int BuildPavingJoints(Transform parent, Material joint)
    {
        var root = NewRoot("HD_PavingJointAssembly", parent);
        int count = 0;

        // DanchiPlaza top is approximately y=0.05 m. These narrow pieces sit mostly embedded in the
        // surface and represent real saw-cut/recess width rather than a dark line painted into albedo.
        for (float x = -18f; x <= 2.01f; x += 2f)
        {
            GameObject part = AddBox($"HD_PavingJoint_NS_{count}", root.transform,
                new Vector3(x, 0.0505f, 1.0f), new Vector3(0.012f, 0.005f, 12.2f), joint);
            ConfigureWeathering(part,
                NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.Recessed | NewTownSurfaceExposure.UpwardFacing,
                NewTownStainSource.RecessGrime | NewTownStainSource.FootTraffic,
                0.92f, 0.88f, 0.08f, 0f);
            count++;
        }

        for (float z = -4.5f; z <= 6.51f; z += 2f)
        {
            GameObject part = AddBox($"HD_PavingJoint_EW_{count}", root.transform,
                new Vector3(-8.0f, 0.0505f, z), new Vector3(23.2f, 0.005f, 0.012f), joint);
            ConfigureWeathering(part,
                NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.Recessed | NewTownSurfaceExposure.UpwardFacing,
                NewTownStainSource.RecessGrime | NewTownStainSource.FootTraffic,
                0.92f, 0.88f, 0.08f, 0f);
            count++;
        }

        return count;
    }

    private static int BuildCurbsAndEdging(Transform parent, Material curb)
    {
        var root = NewRoot("HD_PrecastCurbAssembly", parent);
        int count = 0;

        // Front edge of the apartment plaza: individual 0.60 m modules with ~12 mm installation gaps.
        count += AddCurbRunX(root.transform, "PlazaFront", -19.7f, 7.55f, 40, 0.60f,
            new Vector3(0.588f, 0.14f, 0.22f), 0.115f, curb, 0.94f, 0.86f, 0.62f);

        // Both sides of the long park path. Modules are rotated by dimension rather than by a scaled
        // Unity cube, keeping bevel width fixed in real millimetres through the mesh library.
        count += AddCurbRunZ(root.transform, "ParkPathWest", 6.02f, -10.5f, 36, 0.60f,
            new Vector3(0.18f, 0.12f, 0.588f), 0.105f, curb, 0.92f, 0.82f, 0.48f);
        count += AddCurbRunZ(root.transform, "ParkPathEast", 11.38f, -10.5f, 36, 0.60f,
            new Vector3(0.18f, 0.12f, 0.588f), 0.105f, curb, 0.92f, 0.82f, 0.48f);

        // Low edging beside the worn soil shortcut makes the material transition physically readable
        // while preserving the existing soil surface as the gameplay-collision substrate.
        count += AddCurbRunZ(root.transform, "WornPathWest", 0.24f, -9.75f, 14, 0.60f,
            new Vector3(0.11f, 0.08f, 0.588f), 0.075f, curb, 0.84f, 0.78f, 0.72f);
        count += AddCurbRunZ(root.transform, "WornPathEast", 4.76f, -9.75f, 14, 0.60f,
            new Vector3(0.11f, 0.08f, 0.588f), 0.075f, curb, 0.84f, 0.78f, 0.72f);

        return count;
    }

    private static int AddCurbRunX(Transform parent, string id, float startCenterX, float z,
        int count, float pitch, Vector3 size, float y, Material material,
        float rain, float sun, float splash)
    {
        int built = 0;
        for (int i = 0; i < count; i++)
        {
            GameObject part = AddBox($"HD_Curb_{id}_{i:00}", parent,
                new Vector3(startCenterX + i * pitch, y, z), size, material);
            ConfigureWeathering(part,
                NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.SunExposed |
                NewTownSurfaceExposure.GroundContact | NewTownSurfaceExposure.UpwardFacing,
                NewTownStainSource.GroundSplash | NewTownStainSource.RecessGrime | NewTownStainSource.UVExposure,
                rain, sun, splash, 0f);
            built++;
        }
        return built;
    }

    private static int AddCurbRunZ(Transform parent, string id, float x, float startCenterZ,
        int count, float pitch, Vector3 size, float y, Material material,
        float rain, float sun, float splash)
    {
        int built = 0;
        for (int i = 0; i < count; i++)
        {
            GameObject part = AddBox($"HD_Curb_{id}_{i:00}", parent,
                new Vector3(x, y, startCenterZ + i * pitch), size, material);
            ConfigureWeathering(part,
                NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.SunExposed |
                NewTownSurfaceExposure.GroundContact | NewTownSurfaceExposure.UpwardFacing,
                NewTownStainSource.GroundSplash | NewTownStainSource.RecessGrime | NewTownStainSource.UVExposure,
                rain, sun, splash, 0f);
            built++;
        }
        return built;
    }

    private static void BuildDrainage(Transform parent, Material curb, Material grate, Material dampChannel,
        out int panelCount, out int lod0BarCount, out int lod0RendererCount)
    {
        var root = NewRoot("HD_LinearDrainageAssembly", parent);
        float totalLength = DrainPanelCount * DrainPitch;
        float centerX = DrainStartCenterX + (DrainPanelCount - 1) * DrainPitch * 0.5f;

        GameObject channel = AddBox("HD_DrainChannel_Recess", root.transform,
            new Vector3(centerX, 0.026f, DrainCenterZ),
            new Vector3(totalLength - 0.04f, 0.034f, 0.30f), dampChannel);
        ConfigureWeathering(channel,
            NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.Recessed | NewTownSurfaceExposure.GroundContact,
            NewTownStainSource.DrainRunoff | NewTownStainSource.RecessGrime,
            1f, 0.16f, 0.50f, 0f);

        GameObject shoulderA = AddBox("HD_DrainShoulder_South", root.transform,
            new Vector3(centerX, 0.068f, DrainCenterZ + 0.205f),
            new Vector3(totalLength, 0.09f, 0.09f), curb);
        GameObject shoulderB = AddBox("HD_DrainShoulder_North", root.transform,
            new Vector3(centerX, 0.068f, DrainCenterZ - 0.205f),
            new Vector3(totalLength, 0.09f, 0.09f), curb);
        foreach (GameObject shoulder in new[] { shoulderA, shoulderB })
            ConfigureWeathering(shoulder,
                NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.GroundContact | NewTownSurfaceExposure.UpwardFacing,
                NewTownStainSource.DrainRunoff | NewTownStainSource.GroundSplash,
                1f, 0.70f, 0.42f, 0f);

        var lodRoot = NewRoot("HD_DrainageGrates_LOD", root.transform);
        var lod0 = NewRoot("LOD0_IndividualFramesAndBars", lodRoot.transform);
        var lod1 = NewRoot("LOD1_ReducedBars", lodRoot.transform);
        var lod2 = NewRoot("LOD2_PanelProxies", lodRoot.transform);
        var lod3 = NewRoot("LOD3_ContinuousProxy", lodRoot.transform);

        int bars = 0;
        for (int i = 0; i < DrainPanelCount; i++)
        {
            float x = DrainStartCenterX + i * DrainPitch;
            BuildDrainPanel(lod0.transform, 0, i, x, grate, 7, ref bars);
            int ignored = 0;
            BuildDrainPanel(lod1.transform, 1, i, x, grate, 3, ref ignored);

            GameObject proxy = AddBox($"HD_DrainLOD2_Panel_{i:00}", lod2.transform,
                new Vector3(x, 0.073f, DrainCenterZ),
                new Vector3(DrainPanelVisibleLength, 0.018f, DrainWidth - 0.025f), grate);
            ConfigureWeathering(proxy,
                NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.SunExposed | NewTownSurfaceExposure.UpwardFacing,
                NewTownStainSource.FerrousFixture | NewTownStainSource.FootTraffic,
                1f, 0.68f, 0.04f, 0f);
        }

        GameObject farProxy = AddBox("HD_DrainLOD3_ContinuousMetalSilhouette", lod3.transform,
            new Vector3(centerX, 0.071f, DrainCenterZ),
            new Vector3(totalLength - 0.025f, 0.014f, DrainWidth - 0.035f), grate);
        ConfigureWeathering(farProxy,
            NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.SunExposed | NewTownSurfaceExposure.UpwardFacing,
            NewTownStainSource.FerrousFixture | NewTownStainSource.FootTraffic,
            1f, 0.68f, 0.04f, 0f);

        Renderer[] r0 = lod0.GetComponentsInChildren<Renderer>(true);
        Renderer[] r1 = lod1.GetComponentsInChildren<Renderer>(true);
        Renderer[] r2 = lod2.GetComponentsInChildren<Renderer>(true);
        Renderer[] r3 = lod3.GetComponentsInChildren<Renderer>(true);
        var group = lodRoot.AddComponent<LODGroup>();
        group.fadeMode = LODFadeMode.CrossFade;
        group.animateCrossFading = true;
        group.SetLODs(new[]
        {
            new LOD(0.28f, r0),
            new LOD(0.13f, r1),
            new LOD(0.055f, r2),
            new LOD(0.018f, r3),
        });
        group.RecalculateBounds();

        panelCount = DrainPanelCount;
        lod0BarCount = bars;
        lod0RendererCount = r0.Length;
    }

    private static void BuildDrainPanel(Transform parent, int lod, int index, float centerX,
        Material grate, int barCount, ref int barCounter)
    {
        var module = NewRoot($"HD_DrainLOD{lod}_Module_{index:00}", parent);
        ConfigureWeathering(module,
            NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.SunExposed | NewTownSurfaceExposure.UpwardFacing,
            NewTownStainSource.FerrousFixture | NewTownStainSource.FootTraffic,
            1f, 0.68f, 0.04f, 0f);

        AddBox($"HD_DrainLOD{lod}_FrameSouth_{index:00}", module.transform,
            new Vector3(centerX, 0.077f, DrainCenterZ + 0.147f),
            new Vector3(DrainPanelVisibleLength, 0.026f, 0.024f), grate);
        AddBox($"HD_DrainLOD{lod}_FrameNorth_{index:00}", module.transform,
            new Vector3(centerX, 0.077f, DrainCenterZ - 0.147f),
            new Vector3(DrainPanelVisibleLength, 0.026f, 0.024f), grate);
        AddBox($"HD_DrainLOD{lod}_FrameWest_{index:00}", module.transform,
            new Vector3(centerX - DrainPanelVisibleLength * 0.5f + 0.011f, 0.077f, DrainCenterZ),
            new Vector3(0.022f, 0.026f, 0.294f), grate);
        AddBox($"HD_DrainLOD{lod}_FrameEast_{index:00}", module.transform,
            new Vector3(centerX + DrainPanelVisibleLength * 0.5f - 0.011f, 0.077f, DrainCenterZ),
            new Vector3(0.022f, 0.026f, 0.294f), grate);

        float usable = DrainPanelVisibleLength - 0.09f;
        for (int b = 0; b < barCount; b++)
        {
            float t = (b + 1f) / (barCount + 1f);
            float x = centerX - usable * 0.5f + usable * t;
            AddBox($"HD_DrainLOD{lod}_Bar_{index:00}_{b:00}", module.transform,
                new Vector3(x, 0.0785f, DrainCenterZ),
                new Vector3(lod == 0 ? 0.018f : 0.025f, 0.020f, 0.282f), grate);
            if (lod == 0) barCounter++;
        }
    }

    private static int BuildUtilityCovers(Transform parent, Material castIron, Material joint)
    {
        var root = NewRoot("HD_UtilityCoverAssembly", parent);
        Vector3[] positions =
        {
            new Vector3(-13.3f, 0f, 3.2f),
            new Vector3(-2.8f, 0f, 4.8f),
            new Vector3(8.7f, 0f, 5.8f),
        };

        for (int i = 0; i < positions.Length; i++)
        {
            var assembly = NewRoot($"HD_UtilityCover_{i:00}", root.transform);
            ConfigureWeathering(assembly,
                NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.SunExposed | NewTownSurfaceExposure.UpwardFacing,
                NewTownStainSource.FerrousFixture | NewTownStainSource.FootTraffic | NewTownStainSource.RecessGrime,
                0.96f, 0.84f, 0.05f, 0f);

            Vector3 p = positions[i];
            AddCylinder($"HD_UtilityFrame_{i:00}", assembly.transform,
                p + new Vector3(0f, 0.061f, 0f), 0.72f, 0.026f, castIron);
            AddCylinder($"HD_UtilityCoverDisk_{i:00}", assembly.transform,
                p + new Vector3(0f, 0.076f, 0f), 0.64f, 0.022f, castIron);
            AddCylinder($"HD_UtilityRaisedBoss_{i:00}", assembly.transform,
                p + new Vector3(0f, 0.088f, 0f), 0.50f, 0.008f, castIron);

            // Two recessed lifting slots are separate geometry. They sit nearly flush so they read as
            // manufactured recesses at 100% crop without becoming bright/decal-like marks.
            AddBox($"HD_UtilityLiftSlotA_{i:00}", assembly.transform,
                p + new Vector3(-0.14f, 0.092f, 0f), new Vector3(0.10f, 0.006f, 0.032f), joint);
            AddBox($"HD_UtilityLiftSlotB_{i:00}", assembly.transform,
                p + new Vector3(0.14f, 0.092f, 0f), new Vector3(0.10f, 0.006f, 0.032f), joint);
        }

        return positions.Length;
    }

    private static void EnsureMaterials(out Material paving, out Material curb, out Material grate,
        out Material castIron, out Material joint, out Material dampChannel)
    {
        paving = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/GeneratedPBR/PBR_WarmPaving.mat");
        curb = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/GeneratedPBR/PBR_WashedConcrete.mat");
        if (paving == null || curb == null)
        {
            QualityBlockPbrUpgrade.GenerateLibrary();
            paving = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/GeneratedPBR/PBR_WarmPaving.mat");
            curb = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/GeneratedPBR/PBR_WashedConcrete.mat");
        }
        if (paving == null || curb == null)
            throw new InvalidOperationException("Required generated paving/curb PBR materials are missing.");

        Directory.CreateDirectory(MaterialRoot);
        grate = GetOrCreateMaterial("MAT_GalvanizedDrainGrate", new Color(0.30f, 0.32f, 0.32f), 0.40f, 0.88f);
        castIron = GetOrCreateMaterial("MAT_CastIronUtilityCover", new Color(0.15f, 0.16f, 0.15f), 0.33f, 0.82f);
        joint = GetOrCreateMaterial("MAT_PavingJointRecess", new Color(0.11f, 0.105f, 0.095f), 0.06f, 0f);
        dampChannel = GetOrCreateMaterial("MAT_DampDrainChannel", new Color(0.24f, 0.25f, 0.22f), 0.32f, 0f);
    }

    private static Material GetOrCreateMaterial(string name, Color color, float smoothness, float metallic)
    {
        string path = $"{MaterialRoot}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("Standard");
        if (shader == null)
            throw new InvalidOperationException("Standard shader not found for generated ground materials.");

        if (material == null)
        {
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.shader = shader;
        }

        material.color = color;
        material.SetFloat("_Glossiness", Mathf.Clamp01(smoothness));
        material.SetFloat("_Metallic", Mathf.Clamp01(metallic));
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ApplyBaseGroundMaterials(Material paving)
    {
        foreach (string name in new[] { "DanchiPlaza", "ParkPath" })
        {
            GameObject go = FindSceneObject(name);
            Renderer renderer = go != null ? go.GetComponent<Renderer>() : null;
            if (renderer != null)
                renderer.sharedMaterial = paving;
        }
    }

    private static GameObject AddBox(string name, Transform parent, Vector3 worldPosition,
        Vector3 dimensions, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = worldPosition;
        go.transform.rotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        go.isStatic = true;

        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = QualityBlockDetailMeshLibrary.GetChamferedBox(dimensions);
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        renderer.receiveShadows = true;
        return go;
    }

    private static GameObject AddCylinder(string name, Transform parent, Vector3 worldPosition,
        float diameter, float height, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = worldPosition;
        go.transform.rotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        go.isStatic = true;

        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = QualityBlockDetailMeshLibrary.GetBeveledCylinder(
            new Vector3(diameter, height * 0.5f, diameter), false);
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

    private static void ConfigureWeathering(GameObject go, NewTownSurfaceExposure exposure,
        NewTownStainSource sources, float rain, float sun, float splash, float contact)
    {
        QualityBlockWeatheringSurface metadata = go.GetComponent<QualityBlockWeatheringSurface>();
        if (metadata == null)
            metadata = go.AddComponent<QualityBlockWeatheringSurface>();
        metadata.Configure(exposure, sources, rain, sun, splash, contact);
    }

    private static void AttachReplacementIfAvailable(QualityBlockArtSlot slot)
    {
        const string replacementRoot = "Assets/Art/ReplacementPrefabs";
        GameObject asset = null;
        foreach (string extension in new[] { ".prefab", ".fbx", ".glb" })
        {
            asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{replacementRoot}/{ExpectedReplacement}{extension}");
            if (asset != null) break;
        }
        if (asset == null) return;

        GameObject instance = PrefabUtility.InstantiatePrefab(asset, slot.transform) as GameObject;
        if (instance == null)
            throw new InvalidOperationException($"Could not instantiate authored ground replacement {ExpectedReplacement}.");
        instance.name = ExpectedReplacement + "_Authored";
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        slot.SetAuthoredInstance(instance);
    }

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x.scene.IsValid() && x.name == name);
    }

    private static void RequireText(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Ground registry missing required {label}.");
    }

    [Serializable]
    private sealed class GroundLookdevRegistry
    {
        public bool runtimeRenderVerified;
        public GroundMaterialSpec[] materials;
        public GroundAssemblySpec[] assemblies;
    }

    [Serializable]
    private sealed class GroundMaterialSpec
    {
        public string id;
        public string materialFamily;
        public string finish;
        public float roughnessMin;
        public float roughnessMax;
        public float metallicMin;
        public float metallicMax;
        public float wetAlbedoMultiplier;
        public float wetRoughnessMultiplier;
        public string frontLightResponse;
        public string grazingLightResponse;
        public string shadeResponse;
    }

    [Serializable]
    private sealed class GroundAssemblySpec
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

/// <summary>
/// Scene-local structural evidence used by QA. Counts refer to the generated fallback master, not
/// to an authored replacement. Runtime image quality is deliberately not represented by this data.
/// </summary>
public sealed class QualityBlockGroundDetailManifest : MonoBehaviour
{
    [SerializeField] private int pavingJointCount;
    [SerializeField] private int curbModuleCount;
    [SerializeField] private int drainPanelCount;
    [SerializeField] private int drainBarCount;
    [SerializeField] private int utilityCoverCount;
    [SerializeField] private int lod0RendererCount;

    public int PavingJointCount => pavingJointCount;
    public int CurbModuleCount => curbModuleCount;
    public int DrainPanelCount => drainPanelCount;
    public int DrainBarCount => drainBarCount;
    public int UtilityCoverCount => utilityCoverCount;
    public int Lod0RendererCount => lod0RendererCount;

    public void Configure(int joints, int curbModules, int drainPanels, int drainBars,
        int utilityCovers, int nearRenderers)
    {
        pavingJointCount = joints;
        curbModuleCount = curbModules;
        drainPanelCount = drainPanels;
        drainBarCount = drainBars;
        utilityCoverCount = utilityCovers;
        lod0RendererCount = nearRenderers;
    }
}
