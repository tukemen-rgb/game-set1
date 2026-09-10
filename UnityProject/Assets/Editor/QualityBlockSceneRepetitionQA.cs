using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Static preflight for the critical "immediately obvious repeated texture/module pattern" failure.
/// The rule is deliberately causal: manufactured housing modules may repeat when real construction
/// would repeat, while natural assemblies and occupant-controlled window dressing may not be exact
/// clones. Broad ground repetition is delegated to the existing metric/world-phase PBR validator.
///
/// Exact hierarchy fingerprints are not sufficient because a procedural generator can evade them with
/// tiny, visually irrelevant jitter. Natural assemblies therefore also receive a deliberately coarse
/// perceptual signature that ignores small transform differences and generated numeric suffixes while
/// retaining spatial distribution, mesh complexity and material family. This remains implementation QA
/// only; only actual native-4K evidence can decide whether repetition is visible in the benchmark frame.
/// </summary>
public static class QualityBlockSceneRepetitionQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/scene_repetition_contract.json";
    private static readonly Regex NumericSuffix = new Regex(@"(?:[_\- ]?\d+)+$", RegexOptions.Compiled);

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
            "Benchmark static repetition preflight passed: metric ground phase, apartment occupancy diversity, exact natural-assembly clone checks and coarse perceptual near-clone checks are structurally valid. " +
            "The contract also requires current SHA-256-bound rendered repetition diagnostics before scoring. Critical repeated-pattern status remains render/human-review unverified until native 4K frames and 100% crops are inspected.");
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

        // A one-centimetre branch nudge must not be enough to classify two crowns as perceptually unique.
        // Requiring at least 60% coarse signatures, and never more than two near-clones, leaves room for
        // shared botanical architecture while rejecting obviously cloned procedural crowns.
        int minimumPerceptualUnique = Mathf.Max(2, Mathf.CeilToInt(roots.Length * 0.60f));
        RequirePerceptualDiversity("generated tree masters", roots, minimumPerceptualUnique, 2);
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
        // exact unique render hierarchies are required, and no exact natural layout may occur >2 times.
        RequireHierarchyDiversity("vegetation ecology patches", patches, 5, 2);

        // Tiny offsets are intentionally ignored here. At least four of six patches must still differ
        // at the larger planting-pattern level and no coarse layout may occur more than twice.
        RequirePerceptualDiversity("vegetation ecology patches", patches, 4, 2);
    }

    private static void RequireHierarchyDiversity(string label, Transform[] roots, int minimumUnique, int maximumIdentical)
    {
        Dictionary<string, List<string>> counts = GroupBySignature(roots, FingerprintRenderHierarchy);
        if (counts.Count < minimumUnique)
            throw new InvalidOperationException(
                $"{label} are too repetitive: unique hierarchy fingerprints={counts.Count}/{roots.Length}, minimum={minimumUnique}.");

        List<string> worst = counts.Values.OrderByDescending(x => x.Count).First();
        if (worst.Count > maximumIdentical)
            throw new InvalidOperationException(
                $"{label} contain {worst.Count} exact render-hierarchy clones ({string.Join(", ", worst)}), maximum allowed={maximumIdentical}.");
    }

    private static void RequirePerceptualDiversity(string label, Transform[] roots, int minimumUnique, int maximumNearIdentical)
    {
        Dictionary<string, List<string>> counts = GroupBySignature(roots, FingerprintPerceptualStructure);
        if (counts.Count < minimumUnique)
            throw new InvalidOperationException(
                $"{label} are perceptually too repetitive after coarse normalization: signatures={counts.Count}/{roots.Length}, minimum={minimumUnique}. " +
                "Small positional/rotational jitter does not count as meaningful natural variation.");

        List<string> worst = counts.Values.OrderByDescending(x => x.Count).First();
        if (worst.Count > maximumNearIdentical)
            throw new InvalidOperationException(
                $"{label} contain {worst.Count} coarse near-clones ({string.Join(", ", worst)}), maximum allowed={maximumNearIdentical}. " +
                "Vary crown/patch structure at a perceptible scale rather than adding sub-threshold jitter.");
    }

    private static Dictionary<string, List<string>> GroupBySignature(Transform[] roots, Func<Transform, string> signatureFn)
    {
        var counts = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (Transform root in roots)
        {
            string fingerprint = signatureFn(root);
            if (!counts.TryGetValue(fingerprint, out List<string> names))
            {
                names = new List<string>();
                counts.Add(fingerprint, names);
            }
            names.Add(root.name);
        }
        return counts;
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

    private static string FingerprintPerceptualStructure(Transform root)
    {
        MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true)
            .Where(x => x.enabled && x.gameObject.activeInHierarchy)
            .ToArray();
        if (renderers.Length == 0)
            throw new InvalidOperationException($"Cannot perceptually repetition-audit {root.name}: active render hierarchy is empty.");

        Vector3[] centers = renderers.Select(x => root.InverseTransformPoint(x.bounds.center)).ToArray();
        Vector3 min = centers[0];
        Vector3 max = centers[0];
        for (int i = 1; i < centers.Length; i++)
        {
            min = Vector3.Min(min, centers[i]);
            max = Vector3.Max(max, centers[i]);
        }
        Vector3 span = max - min;

        string[] parts = renderers.Select((renderer, index) =>
            {
                Mesh mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                Material mat = renderer.sharedMaterial;
                Vector3 p01 = Normalize01(centers[index], min, span);
                Quaternion relativeRotation = Quaternion.Inverse(root.rotation) * renderer.transform.rotation;
                Vector3 e = relativeRotation.eulerAngles;
                Vector3 s = SafeRelativeScale(renderer.transform.lossyScale, root.lossyScale);

                return string.Join("|", new[]
                {
                    CoarseMeshFamily(mesh),
                    NormalizeGeneratedName(mat != null ? mat.name : "<nullmat>"),
                    Q(p01.x, 0.08f).ToString(), Q(p01.y, 0.08f).ToString(), Q(p01.z, 0.08f).ToString(),
                    Q(e.x, 12.0f).ToString(), Q(e.y, 12.0f).ToString(), Q(e.z, 12.0f).ToString(),
                    Q(s.x, 0.08f).ToString(), Q(s.y, 0.08f).ToString(), Q(s.z, 0.08f).ToString(),
                });
            })
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        return string.Join(";", parts);
    }

    private static string CoarseMeshFamily(Mesh mesh)
    {
        if (mesh == null) return "<nullmesh>";
        // Generated names and tiny tessellation changes should not create fake uniqueness. Vertex count
        // is binned coarsely so a genuinely different branch/leaf density still changes the signature.
        int vertexBucket = Mathf.RoundToInt(mesh.vertexCount / 16.0f);
        return $"{NormalizeGeneratedName(mesh.name)}:v{vertexBucket}:s{mesh.subMeshCount}";
    }

    private static string NormalizeGeneratedName(string value)
    {
        if (string.IsNullOrEmpty(value)) return "<empty>";
        return NumericSuffix.Replace(value, string.Empty);
    }

    private static Vector3 Normalize01(Vector3 value, Vector3 min, Vector3 span)
    {
        return new Vector3(
            span.x > 0.0001f ? (value.x - min.x) / span.x : 0.5f,
            span.y > 0.0001f ? (value.y - min.y) / span.y : 0.5f,
            span.z > 0.0001f ? (value.z - min.z) / span.z : 0.5f);
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
            "perceptual_coarse_signature", "small_transform_jitter_does_not_count",
            "rendered_repetition_diagnostics", "crop_sha256_binding", "manifest_sha256_binding", "warning_only",
            "QualityBlockRenderedRepetitionDiagnostics", "automaticallyClearsCriticalDefect",
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
