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

    [MenuItem("NewTown/QA/Validate Quality Block")]
    public static void Validate()
    {
        // Validate the same geometry + PBR scene that is intended for screenshots/review.
        QualityBlockMeshUpgrade.BuildMeshQualityBlock();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        string[] required = {
            "QualityBlock1990s", "Danchi", "ParkEntrance", "Trees", "StreetFurniture",
            "MainBlock", "StairTower", "SlideChute", "BenchSeat", "LampPole", "QualityCamera",
            "RoofParapetFront", "StairEntranceCanopy", "SlideLadderRailL", "SlideLadderRailR"
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

        Debug.Log($"QualityBlock validation passed. objects={all.Length}, balconies={balconyCount}, dividers={dividerCount}, AC={acCount}, crowns={crownCount}, PBR={requiredPbr.Length}, meshes={requiredMeshes.Length}");
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
