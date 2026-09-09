using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Static preflight for the critical "immediately obvious repeated texture/module pattern" failure.
/// The rule is deliberately causal: manufactured housing modules may repeat when real construction
/// would repeat, while natural assemblies and occupant-controlled window dressing may not be exact
/// clones. Broad ground repetition is delegated to the existing metric/world-phase PBR validator.
/// This is implementation QA only; only actual 4K frames can decide whether repetition is visible.
/// </summary>
public static class QualityBlockSceneRepetitionQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/scene_repetition_contract.json";

    [MenuItem("NewTown/QA/Validate Benchmark Clone + Repetition Risk")]
    public static void ValidateOpenScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        ValidateContract();

        // Existing ground QA enforces metre-derived macro/detail scale and global world phase for
        // grass/paving/soil instead of one 0..1 primitive UV tile across each giant surface.
        QualityBlockGroundBaseSurfaceUpgrade.Validate();

        // Occupancy variation is intentionally applied behind regular manufactured windows. Its own
        // validator enforces 30 bays, >=18 unique LOD0 layouts and <=3 exact layout repetitions.
        QualityBlockFacadeOccupancyVariationUpgrade.ValidateOpenScene();

        ValidateGeneratedTreeDiversity();
        ValidateEcologyPatchDiversity();

        Debug.Log(
            "Benchmark static repetition preflight passed: metric ground phase, apartment occupancy diversity and natural-assembly clone checks are structurally valid. " +
            "Critical repeated-pattern status remains render-unverified until native 4K frames and 100% crops are inspected.");
    }

    private static void ValidateGeneratedTreeDiversity()
    {
        Transform[] roots = Resources.FindObjectsOfTypeAll<GameObject>()
            .Where(x => x.scene.IsValid() && x.name.StartsWith("HD_TreeMaster_", StringComparison.Ordinal))
            .OrderBy(x => x.name, StringComparer.Ordinal)
            .Select(x => x.transform)
            .ToArray();

        // Authored tree replacements legitimately reduce this count; only generated fallbacks are
        // compared with each other. One remaining fallback has no peer from which to prove cloning.
        if (roots.Length <= 1) return;
        RequireHierarchyDiversity("generated tree masters", roots, roots.Length, 1);
    }

    private static void ValidateEcologyPatchDiversity()
    {
        GameObject ecology = FindSceneObject("VegetationEcologyDetail");
        if (ecology == null) return; // Separate ecology gate owns presence/order; avoid save-event races.

        Transform[] patches = Enumerable.Range(0, 6)
            .Select(i => ecology.transform.Find($"EcologyPatch_{i}"))
            .Where(x => x != null)
            .ToArray();
        if (patches.Length != 6)
            throw new InvalidOperationException($"Vegetation ecology exists but only {patches.Length}/6 patches are available for repetition QA.");

        // Four paved and two grass-root contexts are already structurally different. At least five
        // unique render hierarchies are required, and no exact natural layout may occur >2 times.
        RequireHierarchyDiversity("vegetation ecology patches", patches, 5, 2);
    }

    private static void RequireHierarchyDiversity(string label, Transform[] roots, int minimumUnique, int maximumIdentical)
    {
        var counts = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (Transform root in roots)
        {
            string fingerprint = FingerprintRenderHierarchy(root);
            if (!counts.TryGetValue(fingerprint, out List<string> names))
            {
                names = new List<string>();
                counts.Add(fingerprint, names);
            }
            names.Add(root.name);
        }

        if (counts.Count < minimumUnique)
            throw new InvalidOperationException(
                $"{label} are too repetitive: unique hierarchy fingerprints={counts.Count}/{roots.Length}, minimum={minimumUnique}.");

        List<string> worst = counts.Values.OrderByDescending(x => x.Count).First();
        if (worst.Count > maximumIdentical)
            throw new InvalidOperationException(
                $"{label} contain {worst.Count} exact render-hierarchy clones ({string.Join(", ", worst)}), maximum allowed={maximumIdentical}.");
    }

    private static string FingerprintRenderHierarchy(Transform root)
    {
        string[] parts = root.GetComponentsInChildren<MeshRenderer>(true)
            .Select(renderer =>
            {
                Mesh mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                Material mat = renderer.sharedMaterial;
                Vector3 p = root.InverseTransformPoint(renderer.transform.position);
                Quaternion relativeRotation = Quaternion.Inverse(root.rotation) * renderer.transform.rotation;
                Vector3 e = relativeRotation.eulerAngles;
                Vector3 s = SafeRelativeScale(renderer.transform.lossyScale, root.lossyScale);
                return string.Join("|", new[]
                {
                    mesh != null ? mesh.name : "<nullmesh>",
                    mat != null ? mat.name : "<nullmat>",
                    Q(p.x, 0.005f).ToString(), Q(p.y, 0.005f).ToString(), Q(p.z, 0.005f).ToString(),
                    Q(e.x, 1.0f).ToString(), Q(e.y, 1.0f).ToString(), Q(e.z, 1.0f).ToString(),
                    Q(s.x, 0.005f).ToString(), Q(s.y, 0.005f).ToString(), Q(s.z, 0.005f).ToString(),
                });
            })
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        if (parts.Length == 0)
            throw new InvalidOperationException($"Cannot repetition-audit {root.name}: render hierarchy is empty.");
        return string.Join(";", parts);
    }

    private static Vector3 SafeRelativeScale(Vector3 child, Vector3 root)
    {
        return new Vector3(
            Mathf.Abs(root.x) > 0.00001f ? child.x / root.x : child.x,
            Mathf.Abs(root.y) > 0.00001f ? child.y / root.y : child.y,
            Mathf.Abs(root.z) > 0.00001f ? child.z / root.z : child.z);
    }

    private static int Q(float value, float step)
    {
        return Mathf.RoundToInt(value / step);
    }

    private static void ValidateContract()
    {
        if (!File.Exists(ContractPath))
            throw new InvalidOperationException($"Missing scene repetition QA contract: {ContractPath}");
        string json = File.ReadAllText(ContractPath);
        string[] required =
        {
            "critical_defect", "immediately_obvious_repeated_texture_or_module_pattern",
            "approved_repetition", "prohibited_repetition", "ground_world_phase",
            "generated_tree_fingerprint", "ecology_patch_fingerprint", "apartment_occupancy_layout",
            "render_review_required", "visualFidelityPointsAwarded", "PENDING_UNITY_RUNTIME"
        };
        foreach (string token in required)
            if (!json.Contains(token, StringComparison.Ordinal))
                throw new InvalidOperationException($"Scene repetition contract missing required token: {token}");
    }

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x.scene.IsValid() && x.name == name);
    }
}