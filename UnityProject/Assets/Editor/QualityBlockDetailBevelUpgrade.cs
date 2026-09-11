using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Converts the high-granularity danchi construction pass from Unity built-in primitive meshes to
/// dimension-baked authored meshes. This keeps the existing assembly/layout logic reviewable while
/// removing razor edges, primitive cylinder shading and round peg-like fasteners from the benchmark.
/// The authored geometry is then rebound to manufacturing-scale phased UVs before any LOD proxies are made.
/// </summary>
public static class QualityBlockDetailBevelUpgrade
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string DetailRootName = "DanchiHighDetail";

    [MenuItem("NewTown/Geometry/Build Beveled Detailed Weathered Danchi")]
    public static void BuildBeveledDetailedQualityBlock()
    {
        QualityBlockDanchiDetailUpgrade.BuildDetailedWeatheredQualityBlock();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        ApplyToOpenScene();
        ValidateOpenScene();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Beveled high-detail danchi with metric phased manufacture UVs built. Runtime Unity render verification remains pending.");
    }

    [MenuItem("NewTown/Geometry/Apply Detail Bevel Pass Only")]
    public static void ApplyToOpenScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() ||
            EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var root = FindSceneObject(DetailRootName);
        if (root == null)
            throw new InvalidOperationException("DanchiHighDetail is missing. Build the detail pass first.");

        int boxes = 0;
        int cylinders = 0;
        int hexFasteners = 0;

        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null) continue;

            string meshName = mesh.name ?? string.Empty;
            if (meshName == "Cube")
            {
                Vector3 dimensions = Abs(filter.transform.localScale);
                filter.sharedMesh = QualityBlockDetailMeshLibrary.GetChamferedBox(dimensions);
                filter.transform.localScale = Vector3.one;
                boxes++;
            }
            else if (meshName == "Cylinder")
            {
                Vector3 legacyScale = Abs(filter.transform.localScale);
                bool fastener = IsFastener(filter.gameObject.name);
                filter.sharedMesh = QualityBlockDetailMeshLibrary.GetBeveledCylinder(legacyScale, fastener);
                filter.transform.localScale = Vector3.one;
                cylinders++;
                if (fastener) hexFasteners++;
            }
        }

        var oldManifest = root.GetComponent<QualityBlockDetailGeometryManifest>();
        if (oldManifest != null) UnityEngine.Object.DestroyImmediate(oldManifest);
        var manifest = root.AddComponent<QualityBlockDetailGeometryManifest>();
        manifest.Configure(boxes, cylinders, hexFasteners);

        // This must run before DanchiLodUpgrade copies source renderers. LOD1-3 then inherit the same
        // physically scaled/phase-diverse mesh assets rather than reintroducing normalized primitive UVs.
        QualityBlockDetailPhysicalUvUpgrade.ApplyToOpenScene();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    [MenuItem("NewTown/QA/Validate Beveled Detail Geometry")]
    public static void ValidateOpenScene()
    {
        var root = FindSceneObject(DetailRootName);
        if (root == null) throw new InvalidOperationException("DanchiHighDetail is missing.");

        var manifest = root.GetComponent<QualityBlockDetailGeometryManifest>();
        if (manifest == null)
            throw new InvalidOperationException("Detailed geometry manifest is missing; bevel pass did not run.");
        if (manifest.ChamferedBoxCount <= 0 || manifest.BeveledCylinderCount <= 0)
            throw new InvalidOperationException(
                $"Bevel conversion did not find expected primitive classes: boxes={manifest.ChamferedBoxCount}, cylinders={manifest.BeveledCylinderCount}.");
        if (manifest.HexFastenerCount <= 0)
            throw new InvalidOperationException("No hex fasteners were produced from bolt/fastener geometry.");

        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
        int primitiveCount = filters.Count(f =>
            f.sharedMesh != null && (f.sharedMesh.name == "Cube" || f.sharedMesh.name == "Cylinder"));
        if (primitiveCount != 0)
            throw new InvalidOperationException($"High-detail root still contains {primitiveCount} built-in primitive meshes.");

        int authoredCount = filters.Count(f =>
            f.sharedMesh != null && f.sharedMesh.name.StartsWith("GM_HD_", StringComparison.Ordinal));
        if (authoredCount != filters.Length)
            throw new InvalidOperationException(
                $"Expected every high-detail MeshFilter to use GM_HD authored geometry: authored={authoredCount}, total={filters.Length}.");

        QualityBlockDetailPhysicalUvQA.ValidateOpenScene();

        Debug.Log(
            $"Beveled detail geometry validation passed structurally: chamferedBoxes={manifest.ChamferedBoxCount}, " +
            $"beveledCylinders={manifest.BeveledCylinderCount}, hexFasteners={manifest.HexFastenerCount}, authoredMeshes={authoredCount}, metricPhysicalUv=validated. " +
            "Actual edge highlights, microtexture repetition, seams and LOD behavior still require Unity render inspection.");
    }

    private static bool IsFastener(string objectName)
    {
        return objectName.IndexOf("Bolt", StringComparison.OrdinalIgnoreCase) >= 0 ||
               objectName.IndexOf("Fastener", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static Vector3 Abs(Vector3 v) =>
        new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x.scene.IsValid() && x.name == name);
    }
}

[DisallowMultipleComponent]
public sealed class QualityBlockDetailGeometryManifest : MonoBehaviour
{
    [SerializeField] private int chamferedBoxCount;
    [SerializeField] private int beveledCylinderCount;
    [SerializeField] private int hexFastenerCount;

    public int ChamferedBoxCount => chamferedBoxCount;
    public int BeveledCylinderCount => beveledCylinderCount;
    public int HexFastenerCount => hexFastenerCount;

    public void Configure(int boxes, int cylinders, int hexFasteners)
    {
        chamferedBoxCount = boxes;
        beveledCylinderCount = cylinders;
        hexFastenerCount = hexFasteners;
    }
}
