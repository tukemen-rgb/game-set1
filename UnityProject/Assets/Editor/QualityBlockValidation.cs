using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class QualityBlockValidation
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";

    [MenuItem("NewTown/QA/Validate Quality Block")]
    public static void Validate()
    {
        BuildQualityBlock1990s.Build();
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

        Debug.Log($"QualityBlock validation passed. objects={all.Length}, balconies={balconyCount}, AC={acCount}, crowns={crownCount}");
    }
}
