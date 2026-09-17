using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class QualityBlockValidation
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string PbrRoot = "Assets/Art/GeneratedPBR";
    private const string MeshRoot = "Assets/Art/GeneratedMeshes";
    private const string WeatheringRoot = "Assets/Art/GeneratedWeathering";

    [MenuItem("NewTown/QA/Validate Quality Block")]
    public static void Validate()
    {
        // Validate the exact scene intended for review, including replacement slots, LODs,
        // physical summer lighting and source-driven weathering.
        QualityBlockWeatheringUpgrade.BuildWeatheredQualityBlock();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        string[] required = {
            "QualityBlock1990s", "Danchi", "ParkEntrance", "Trees", "StreetFurniture", "ArtSlots",
            "MainBlock", "StairTower", "SlideChute", "BenchSeat", "LampPole", "QualityCamera",
            "RoofParapetFront", "StairEntranceCanopy", "SlideLadderRailL", "SlideLadderRailR",
            "ARTSLOT_Danchi", "ARTSLOT_Slide", "ARTSLOT_Tree_0", "ARTSLOT_Tree_5",
            "PhysicalEnvironmentContext", "WeatheringOverlays",
            "Weathering_GroundSplash_MainBlock", "Weathering_DrainRunoff_RainGutter"
        };

        var all = Resources.FindObjectsOfTypeAll<GameObject>()
            .Where(x => x.scene.IsValid())
            .ToArray();

        var missing = required.Where(name => !all.Any(x => x.name == name)).ToArray();
        if (missing.Length > 0)
            throw new Exception("QualityBlock validation failed. Missing: " + string.Join(", ", missing));

        string[] forbiddenTokens = {
            "earthquake", "disaster", "reconstruction", "rubble", "collapsed",
            "震災", "地震", "災害", "復興", "瓦礫", "倒壊"
        };
        var forbidden = all
            .Where(x => forbiddenTokens.Any(t => x.name.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0))
            .Select(x => x.name)
            .Distinct()
            .ToArray();
        if (forbidden.Length > 0)
            throw new Exception("Forbidden disaster-theme object names found: " + string.Join(", ", forbidden));

        var cam = Camera.main;
        if (cam == null || Mathf.Abs(cam.aspect - (16f / 9f)) > 0.25f)
            Debug.LogWarning("Main camera exists, but verify 16:9 output in capture tool.");

        int balconyCount = all.Count(x => x.name.StartsWith("BalconyFloor_", StringComparison.Ordinal));
        int dividerCount = all.Count(x => x.name.StartsWith("BalconyDivider_", StringComparison.Ordinal));
        int acCount = all.Count(x => x.name.StartsWith("AC_", StringComparison.Ordinal));
        int crownCount = all.Count(x => x.name.StartsWith("Crown_", StringComparison.Ordinal));
        int ladderRungs = all.Count(x => x.name.StartsWith("SlideLadderRung_", StringComparison.Ordinal));

        if (balconyCount < 30) throw new Exception($"Expected at least 30 balcony modules, got {balconyCount}.");
        if (dividerCount < 30) throw new Exception($"Expected 30 balcony privacy dividers, got {dividerCount}.");
        if (acCount < 10) throw new Exception($"Expected visible AC variation, got {acCount} units.");
        if (crownCount < 20) throw new Exception($"Expected clustered tree crowns, got {crownCount}.");
        if (ladderRungs != 6) throw new Exception($"Expected six simple steel slide ladder rungs, got {ladderRungs}.");

        string[] requiredPbr = {
            "PBR_GrassWorn", "PBR_DrySoil", "PBR_DanchiConcrete", "PBR_WashedConcrete",
            "PBR_WarmPaving", "PBR_LeafDark", "PBR_LeafMid", "PBR_Bark"
        };
        foreach (string matName in requiredPbr)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{PbrRoot}/{matName}.mat");
            if (mat == null) throw new Exception($"Missing PBR material asset: {matName}");
            if (mat.mainTexture == null) throw new Exception($"PBR material lacks albedo: {matName}");
            if (mat.GetTexture("_BumpMap") == null) throw new Exception($"PBR material lacks normal map: {matName}");
            if (mat.GetTexture("_MetallicGlossMap") == null) throw new Exception($"PBR material lacks roughness/smoothness map: {matName}");
        }

        string[] requiredMeshes = {
            "GM_TreeTrunk_A", "GM_TreeTrunk_B", "GM_FoliageClump_A", "GM_FoliageClump_B",
            "GM_FoliageClump_C", "GM_SlideChute"
        };
        foreach (string meshName in requiredMeshes)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshRoot}/{meshName}.asset");
            if (mesh == null) throw new Exception($"Missing generated mesh asset: {meshName}");
            if (mesh.vertexCount < 8) throw new Exception($"Generated mesh is unexpectedly trivial: {meshName}");
        }

        AssertMaterial(all, "GrassField", "PBR_GrassWorn");
        AssertMaterial(all, "WornPathA", "PBR_DrySoil");
        AssertMaterial(all, "DanchiPlaza", "PBR_WarmPaving");
        AssertMaterial(all, "MainBlock", "PBR_DanchiConcrete");
        AssertMaterial(all, "StairTower", "PBR_DanchiConcrete");
        AssertMaterial(all, "BenchLegL", "PBR_WashedConcrete");
        AssertMaterial(all, "Trunk_0", "PBR_Bark");

        AssertMeshPrefix(all, "Trunk_0", "GM_TreeTrunk_");
        AssertMeshPrefix(all, "Crown_0_0", "GM_FoliageClump_");
        AssertMeshPrefix(all, "SlideChute", "GM_SlideChute");

        var crown = all.FirstOrDefault(x => x.name == "Crown_0_0");
        if (crown == null) throw new Exception("Missing Crown_0_0 for foliage material validation.");
        var crownRenderer = crown.GetComponent<Renderer>();
        if (crownRenderer == null || crownRenderer.sharedMaterial == null ||
            !crownRenderer.sharedMaterial.name.StartsWith("PBR_Leaf", StringComparison.Ordinal))
            throw new Exception("Crown_0_0 is not using the generated PBR foliage material.");

        ValidateArtSlots(all);
        ValidatePhysicalEnvironment(all);

        Debug.Log($"QualityBlock validation passed. objects={all.Length}, balconies={balconyCount}, dividers={dividerCount}, AC={acCount}, crowns={crownCount}, PBR={requiredPbr.Length}, meshes={requiredMeshes.Length}, artSlots=8, contextualWeathering=yes");
    }

    private static void ValidatePhysicalEnvironment(GameObject[] all)
    {
        var context = all
            .Select(x => x.GetComponent<QualityBlockEnvironmentContext>())
            .FirstOrDefault(x => x != null);
        if (context == null) throw new Exception("Physical environment context is missing.");

        SolarSample sample = context.CalculateSolarSample();
        if (Mathf.Abs(sample.ElevationDegrees - 58.1f) > 1.0f)
            throw new Exception($"Unexpected midsummer solar elevation: {sample.ElevationDegrees:F2} deg.");
        if (Mathf.Abs(sample.AzimuthDegrees - 244.2f) > 1.5f)
            throw new Exception($"Unexpected midsummer solar azimuth: {sample.AzimuthDegrees:F2} deg.");
        if (Mathf.Abs(sample.HorizontalShadowPerMetre - 0.62f) > 0.05f)
            throw new Exception($"Unexpected shadow-length ratio: {sample.HorizontalShadowPerMetre:F3} m per metre height.");

        var sun = all.FirstOrDefault(x => x.name == "SummerSun")?.GetComponent<Light>();
        if (sun == null || sun.type != LightType.Directional)
            throw new Exception("SummerSun directional light is missing.");
        if (Vector3.Dot(sun.transform.forward.normalized, sample.RayDirection.normalized) < 0.999f)
            throw new Exception("SummerSun transform is inconsistent with the physical solar model.");
        if (sun.shadows != LightShadows.Soft)
            throw new Exception("SummerSun must use soft shadows.");

        var weathered = all
            .Select(x => x.GetComponent<QualityBlockWeatheringSurface>())
            .Where(x => x != null)
            .ToArray();
        if (weathered.Length < 100)
            throw new Exception($"Expected broad cause-based weathering metadata coverage, got {weathered.Length} surfaces.");

        int rainStreaks = all.Count(x => x.name.StartsWith("Weathering_RainSill_Window_", StringComparison.Ordinal));
        int rustBleeds = all.Count(x => x.name.StartsWith("Weathering_RustRailBase", StringComparison.Ordinal));
        if (rainStreaks != 30)
            throw new Exception($"Each window should source one rain runoff trace; got {rainStreaks}.");
        if (rustBleeds != 60)
            throw new Exception($"Each balcony should have two rail-base rust sources; got {rustBleeds}.");

        string[] weatheringMaterials = { "MAT_RainRunoff", "MAT_GroundSplash", "MAT_RustBleed" };
        foreach (string name in weatheringMaterials)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{WeatheringRoot}/{name}.mat");
            if (mat == null) throw new Exception($"Missing generated contextual weathering material: {name}");
        }

        foreach (var surface in weathered)
        {
            if ((surface.StainSources & NewTownStainSource.GroundSplash) != 0 && surface.SplashExposure <= 0f)
                throw new Exception($"Ground splash source has zero splash exposure: {surface.gameObject.name}");
            if ((surface.StainSources & NewTownStainSource.UVExposure) != 0 && surface.SunExposure <= 0f)
                throw new Exception($"UV weathering source has zero sun exposure: {surface.gameObject.name}");
            if ((surface.StainSources & NewTownStainSource.FerrousFixture) != 0 &&
                surface.gameObject.name.StartsWith("Window_", StringComparison.Ordinal))
                throw new Exception($"Impossible rust source assigned to non-ferrous window context: {surface.gameObject.name}");
        }
    }

    private static void ValidateArtSlots(GameObject[] all)
    {
        var slots = all
            .Select(x => x.GetComponent<QualityBlockArtSlot>())
            .Where(x => x != null)
            .ToArray();
        if (slots.Length != 8)
            throw new Exception($"Expected 8 stable art slots (danchi, slide, six trees), got {slots.Length}.");

        string[] expectedIds = {
            "danchi.main", "park.slide",
            "vegetation.tree.0", "vegetation.tree.1", "vegetation.tree.2",
            "vegetation.tree.3", "vegetation.tree.4", "vegetation.tree.5"
        };
        foreach (string id in expectedIds)
            if (!slots.Any(x => string.Equals(x.SlotId, id, StringComparison.Ordinal)))
                throw new Exception($"Missing art slot id: {id}");

        foreach (var slot in slots)
        {
            if (slot.FallbackRoot == null)
                throw new Exception($"Art slot has no generated fallback: {slot.SlotId}");
            if (slot.AuthoredInstance == null && !slot.FallbackRoot.activeSelf)
                throw new Exception($"Fallback is unexpectedly hidden without authored replacement: {slot.SlotId}");
            if (slot.AuthoredInstance != null && slot.FallbackRoot.activeSelf)
                throw new Exception($"Fallback and authored art are both visible: {slot.SlotId}");
        }

        foreach (var slot in slots.Where(x => x.SlotId.StartsWith("vegetation.tree.", StringComparison.Ordinal)))
        {
            if (slot.AuthoredInstance != null) continue;
            var lodGroup = slot.GetComponent<LODGroup>();
            if (lodGroup == null)
                throw new Exception($"Generated tree fallback lacks LODGroup: {slot.SlotId}");
            if (lodGroup.GetLODs().Length != 3)
                throw new Exception($"Generated tree fallback expected 3 LOD levels: {slot.SlotId}");
        }
    }

    static void AssertMaterial(GameObject[] all, string objectName, string expectedMaterial)
    {
        var go = all.FirstOrDefault(x => x.name == objectName);
        if (go == null) throw new Exception($"Missing object for material validation: {objectName}");
        var renderer = go.GetComponent<Renderer>();
        if (renderer == null || renderer.sharedMaterial == null)
            throw new Exception($"Object has no renderer/material: {objectName}");
        if (!string.Equals(renderer.sharedMaterial.name, expectedMaterial, StringComparison.Ordinal))
            throw new Exception($"{objectName} expected material {expectedMaterial}, got {renderer.sharedMaterial.name}.");
    }

    static void AssertMeshPrefix(GameObject[] all, string objectName, string expectedPrefix)
    {
        var go = all.FirstOrDefault(x => x.name == objectName);
        if (go == null) throw new Exception($"Missing object for mesh validation: {objectName}");
        var filter = go.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null)
            throw new Exception($"Object has no mesh: {objectName}");
        if (!filter.sharedMesh.name.StartsWith(expectedPrefix, StringComparison.Ordinal))
            throw new Exception($"{objectName} expected authored mesh prefix {expectedPrefix}, got {filter.sharedMesh.name}.");
    }
}
