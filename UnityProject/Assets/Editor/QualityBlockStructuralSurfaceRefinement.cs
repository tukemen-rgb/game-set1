using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Removes retained Unity Cube/Cylinder renderer meshes from benchmark-facing structural fallback
/// components without changing their world dimensions or gameplay collision footprint. This is a
/// source-side risk reduction for the automatic-fail condition "visible primitive-placeholder
/// geometry"; it never clears that defect without inspection of the real native-4K render.
/// </summary>
public static class QualityBlockStructuralSurfaceRefinement
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string RootName = "QualityBlock1990s";
    private const float BoundsToleranceMetres = 0.002f;

    [MenuItem("NewTown/Geometry/Refine Retained Structural Primitive Surfaces")]
    public static void BuildAndApply()
    {
        EnsureScene();
        ApplyToOpenScene();
        ValidateOpenScene();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Structural surface refinement persisted. Actual primitive visibility remains a native-4K review item.");
    }

    public static void ApplyToOpenScene()
    {
        EnsureScene();
        GameObject root = FindSceneObject(RootName);
        if (root == null)
            throw new InvalidOperationException("QualityBlock1990s root not found.");

        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!IsStructuralTarget(filter.gameObject)) continue;
            Renderer renderer = filter.GetComponent<Renderer>();
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            Mesh mesh = filter.sharedMesh;
            if (mesh == null) continue;

            if (string.Equals(mesh.name, "Cube", StringComparison.Ordinal))
                ReplaceCube(filter, renderer);
            else if (string.Equals(mesh.name, "Cylinder", StringComparison.Ordinal))
                ReplaceCylinder(filter, renderer);
        }

        QualityBlockSurfaceGeometryManifest oldManifest = root.GetComponent<QualityBlockSurfaceGeometryManifest>();
        if (oldManifest != null) UnityEngine.Object.DestroyImmediate(oldManifest);

        MeshFilter[] targets = root.GetComponentsInChildren<MeshFilter>(true)
            .Where(x => IsStructuralTarget(x.gameObject))
            .Where(IsActivelyRendered)
            .ToArray();
        int chamfered = targets.Count(x => x.sharedMesh != null &&
            x.sharedMesh.name.StartsWith("GM_SURF_ChamferBox_", StringComparison.Ordinal));
        int beveledCylinders = targets.Count(x => x.sharedMesh != null &&
            x.sharedMesh.name.StartsWith("GM_HD_BevelCylinder_", StringComparison.Ordinal));

        var manifest = root.AddComponent<QualityBlockSurfaceGeometryManifest>();
        manifest.Configure(targets.Length, chamfered, beveledCylinders);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
    }

    [MenuItem("NewTown/QA/Validate Structural Surface Geometry")]
    public static void ValidateOpenScene()
    {
        EnsureScene();
        GameObject root = FindSceneObject(RootName);
        if (root == null) throw new InvalidOperationException("QualityBlock1990s root not found.");

        QualityBlockSurfaceGeometryManifest manifest = root.GetComponent<QualityBlockSurfaceGeometryManifest>();
        if (manifest == null)
            throw new InvalidOperationException("Structural surface geometry manifest is missing; refinement did not run.");
        if (manifest.StructuralTargetCount < 300)
            throw new InvalidOperationException(
                $"Structural target inventory unexpectedly low: {manifest.StructuralTargetCount}. Expected broad facade/ground coverage.");
        if (manifest.ChamferedBoxCount < 300)
            throw new InvalidOperationException(
                $"Chamfered structural box coverage unexpectedly low: {manifest.ChamferedBoxCount}.");
        if (manifest.BeveledCylinderCount < 1)
            throw new InvalidOperationException("Expected at least the building downpipe to use dimension-baked cylinder geometry.");

        MeshFilter[] residual = root.GetComponentsInChildren<MeshFilter>(true)
            .Where(x => IsStructuralTarget(x.gameObject))
            .Where(IsActivelyRendered)
            .Where(x => x.sharedMesh != null && IsBuiltInPrimitive(x.sharedMesh.name))
            .ToArray();
        if (residual.Length > 0)
            throw new InvalidOperationException(
                "Retained structural fallback still exposes built-in primitive renderer meshes: " +
                string.Join(", ", residual.Select(x => $"{x.gameObject.name}:{x.sharedMesh.name}").Take(20)));

        // The old spherical park lamp must be hidden by the physical lamp refinement. If it becomes
        // active again, do not pretend the structural cleanup has removed all benchmark placeholders.
        MeshFilter lampGlobe = root.GetComponentsInChildren<MeshFilter>(true)
            .FirstOrDefault(x => x.gameObject.name == "LampGlobe" && IsActivelyRendered(x));
        if (lampGlobe != null && lampGlobe.sharedMesh != null &&
            string.Equals(lampGlobe.sharedMesh.name, "Sphere", StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Legacy LampGlobe Sphere is actively rendered; physical diffuser replacement is not controlling the benchmark.");

        Debug.Log(
            $"Structural surface geometry valid: targets={manifest.StructuralTargetCount}, " +
            $"chamferedBoxes={manifest.ChamferedBoxCount}, beveledCylinders={manifest.BeveledCylinderCount}, " +
            "active targeted built-in primitives=0. Native-4K inspection is still required before clearing the critical visual defect.");
    }

    private static void ReplaceCube(MeshFilter filter, Renderer renderer)
    {
        Transform t = filter.transform;
        Vector3 scale = t.localScale;
        RequirePositiveScale(filter.gameObject.name, scale);
        EnsureOnlyColliderType<BoxCollider>(filter.gameObject);

        Bounds rendererBefore = renderer.bounds;
        List<Bounds> colliderBefore = filter.GetComponents<Collider>().Select(x => x.bounds).ToList();
        BoxCollider[] colliders = filter.GetComponents<BoxCollider>();

        float bevelCap = ResolveBoxBevelCap(filter.gameObject.name);
        filter.sharedMesh = QualityBlockSurfaceMeshLibrary.GetChamferedBox(scale, bevelCap);
        foreach (BoxCollider collider in colliders)
        {
            collider.center = Vector3.Scale(collider.center, scale);
            collider.size = Vector3.Scale(collider.size, scale);
        }
        t.localScale = Vector3.one;

        AssertBoundsPreserved(filter.gameObject.name, rendererBefore, renderer.bounds, "renderer");
        Collider[] after = filter.GetComponents<Collider>();
        for (int i = 0; i < colliderBefore.Count; i++)
            AssertBoundsPreserved(filter.gameObject.name, colliderBefore[i], after[i].bounds, $"collider[{i}]");
    }

    private static void ReplaceCylinder(MeshFilter filter, Renderer renderer)
    {
        Transform t = filter.transform;
        Vector3 scale = t.localScale;
        RequirePositiveScale(filter.gameObject.name, scale);
        if (Mathf.Abs(scale.x - scale.z) > Mathf.Max(0.0001f, Mathf.Max(scale.x, scale.z) * 0.01f))
            throw new InvalidOperationException(
                $"Structural cylinder {filter.gameObject.name} is elliptically scaled ({scale}); explicit authored geometry is required.");
        EnsureOnlyColliderType<CylinderCollider>(filter.gameObject);
        foreach (CylinderCollider collider in filter.GetComponents<CylinderCollider>())
            if (collider.direction != 1)
                throw new InvalidOperationException(
                    $"Structural cylinder {filter.gameObject.name} uses a non-Y CylinderCollider; automatic footprint preservation is unsafe.");

        Bounds rendererBefore = renderer.bounds;
        List<Bounds> colliderBefore = filter.GetComponents<Collider>().Select(x => x.bounds).ToList();
        CylinderCollider[] colliders = filter.GetComponents<CylinderCollider>();

        filter.sharedMesh = QualityBlockDetailMeshLibrary.GetBeveledCylinder(scale, false);
        foreach (CylinderCollider collider in colliders)
        {
            collider.center = Vector3.Scale(collider.center, scale);
            collider.radius *= Mathf.Max(scale.x, scale.z);
            collider.height *= scale.y;
        }
        t.localScale = Vector3.one;

        AssertBoundsPreserved(filter.gameObject.name, rendererBefore, renderer.bounds, "renderer");
        Collider[] after = filter.GetComponents<Collider>();
        for (int i = 0; i < colliderBefore.Count; i++)
            AssertBoundsPreserved(filter.gameObject.name, colliderBefore[i], after[i].bounds, $"collider[{i}]");
    }

    private static float ResolveBoxBevelCap(string name)
    {
        if (name.StartsWith("Rail_", StringComparison.Ordinal) ||
            name.StartsWith("RailTop_", StringComparison.Ordinal))
            return 0.0020f; // welded/painted steel square section edge break
        if (name.StartsWith("AC_", StringComparison.Ordinal))
            return 0.0040f; // folded/plastic equipment casing edge radius proxy
        if (name == "GrassField" || name == "DanchiPlaza" || name == "ParkPath" ||
            name.StartsWith("WornPath", StringComparison.Ordinal))
            return 0.0030f; // ground layer edge is mostly buried; avoid an oversized highlight
        if (name == "MainBlock" || name == "StairTower" ||
            name.StartsWith("BalconyFloor_", StringComparison.Ordinal) ||
            name.StartsWith("BalconyDivider_", StringComparison.Ordinal) ||
            name.StartsWith("RoofParapet", StringComparison.Ordinal) ||
            name == "StairEntranceCanopy")
            return 0.0060f; // small cast/formwork arris; not a stylized rounded corner
        return 0.0030f;
    }

    private static bool IsStructuralTarget(GameObject go)
    {
        string n = go.name;
        if (n == "MainBlock" || n == "StairTower" || n == "RainGutter" ||
            n == "GrassField" || n == "DanchiPlaza" || n == "ParkPath" ||
            n == "WornPathA" || n == "WornPathB" ||
            n == "RoofParapetFront" || n == "RoofParapetRear" || n == "StairEntranceCanopy" ||
            n == "SlideLegL" || n == "SlideLegR" || n == "SlidePlatform" ||
            n == "BenchSeat" || n == "BenchLegL" || n == "BenchLegR" ||
            n == "LampPole" || n == "NoticeBoardPosts" || n == "NoticeBoardPanel")
            return true;

        return n.StartsWith("BalconyFloor_", StringComparison.Ordinal) ||
               n.StartsWith("BalconyDivider_", StringComparison.Ordinal) ||
               n.StartsWith("RailTop_", StringComparison.Ordinal) ||
               n.StartsWith("Rail_", StringComparison.Ordinal) ||
               n.StartsWith("AC_", StringComparison.Ordinal) ||
               n.StartsWith("FacadeBand_", StringComparison.Ordinal);
    }

    private static bool IsActivelyRendered(MeshFilter filter)
    {
        Renderer renderer = filter.GetComponent<Renderer>();
        return renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy;
    }

    private static bool IsBuiltInPrimitive(string meshName) =>
        meshName == "Cube" || meshName == "Cylinder" || meshName == "Sphere" ||
        meshName == "Capsule" || meshName == "Plane" || meshName == "Quad";

    private static void EnsureOnlyColliderType<T>(GameObject go) where T : Collider
    {
        Collider[] colliders = go.GetComponents<Collider>();
        Collider unsupported = colliders.FirstOrDefault(x => !(x is T));
        if (unsupported != null)
            throw new InvalidOperationException(
                $"Cannot refine {go.name}: collider {unsupported.GetType().Name} cannot be footprint-preserved safely.");
    }

    private static void RequirePositiveScale(string objectName, Vector3 scale)
    {
        if (scale.x <= 0f || scale.y <= 0f || scale.z <= 0f)
            throw new InvalidOperationException(
                $"Cannot bake structural dimensions for {objectName} with non-positive local scale {scale}.");
    }

    private static void AssertBoundsPreserved(string objectName, Bounds before, Bounds after, string kind)
    {
        float centerDelta = (before.center - after.center).magnitude;
        float sizeDelta = MaxComponent(Abs(before.size - after.size));
        if (centerDelta > BoundsToleranceMetres || sizeDelta > BoundsToleranceMetres)
            throw new InvalidOperationException(
                $"{objectName} {kind} footprint changed during visual mesh refinement: " +
                $"centerDelta={centerDelta:F6}m sizeDelta={sizeDelta:F6}m.");
    }

    private static Vector3 Abs(Vector3 v) =>
        new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    private static float MaxComponent(Vector3 v) => Mathf.Max(v.x, Mathf.Max(v.y, v.z));

    private static void EnsureScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() ||
            EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static GameObject FindSceneObject(string name) =>
        Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x.scene.IsValid() && x.name == name);
}

[DisallowMultipleComponent]
public sealed class QualityBlockSurfaceGeometryManifest : MonoBehaviour
{
    [SerializeField] private int structuralTargetCount;
    [SerializeField] private int chamferedBoxCount;
    [SerializeField] private int beveledCylinderCount;

    public int StructuralTargetCount => structuralTargetCount;
    public int ChamferedBoxCount => chamferedBoxCount;
    public int BeveledCylinderCount => beveledCylinderCount;

    public void Configure(int targets, int boxes, int cylinders)
    {
        structuralTargetCount = targets;
        chamferedBoxCount = boxes;
        beveledCylinderCount = cylinders;
    }
}
