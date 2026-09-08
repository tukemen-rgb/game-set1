using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class QualityBlockValidation
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string PbrRoot = "Assets/Art/GeneratedPBR";

    [MenuItem("NewTown/QA/Validate Quality Block")]
    public static void Validate()
    {
        // Build the same PBR-upgraded scene that is intended for screenshots/review.
        QualityBlockPbrUpgrade.BuildPbrQualityBlock();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        string[] required = {
            "QualityBlock1990s", "Danchi", "ParkEntrance", "Trees", "StreetFurniture",
            "MainBlock", "StairTower", "SlideChute", "BenchSeat", "LampPole", "QualityCamera"
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
        int acCount = all.Count(x => x.name.StartsWith("AC_", StringComparison.Ordinal));
        int crownCount = all.Count(x => x.name.StartsWith("Crown_", StringComparison.Ordinal));

        if (balconyCount < 30) throw new Exception($"Expected at least 30 balcony modules, got {balconyCount}.");
        if (acCount < 10) throw new Exception($"Expected visible AC variation, got {acCount} units.");
        if (crownCount < 20) throw new Exception($"Expected clustered tree crowns, got {crownCount}.");

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

        AssertMaterial(all, "GrassField", "PBR_GrassWorn");
        AssertMaterial(all, "WornPathA", "PBR_DrySoil");
        AssertMaterial(all, "DanchiPlaza", "PBR_WarmPaving");
        AssertMaterial(all, "MainBlock", "PBR_DanchiConcrete");
        AssertMaterial(all, "StairTower", "PBR_DanchiConcrete");
        AssertMaterial(all, "BenchLegL", "PBR_WashedConcrete");
        AssertMaterial(all, "Trunk_0", "PBR_Bark");

        var crown = all.FirstOrDefault(x => x.name == "Crown_0_0");
        if (crown == null) throw new Exception("Missing Crown_0_0 for foliage material validation.");
        var crownRenderer = crown.GetComponent<Renderer>();
        if (crownRenderer == null || crownRenderer.sharedMaterial == null ||
            !crownRenderer.sharedMaterial.name.StartsWith("PBR_Leaf", StringComparison.Ordinal))
            throw new Exception("Crown_0_0 is not using the generated PBR foliage material.");

        Debug.Log($"QualityBlock validation passed. objects={all.Length}, balconies={balconyCount}, AC={acCount}, crowns={crownCount}, PBR={requiredPbr.Length}");
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
}
