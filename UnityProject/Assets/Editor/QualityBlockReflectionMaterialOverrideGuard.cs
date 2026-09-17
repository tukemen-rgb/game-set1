using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Fail-closed guard for Renderer MaterialPropertyBlock overrides during formal reflection/still evidence.
/// MaterialPropertyBlock can override per-renderer or per-material shader values without changing the
/// shared Material asset. Until arbitrary override payloads are fingerprinted explicitly, accepting such
/// a renderer would create a hidden route for albedo/roughness/metallic/emission/texture state to differ
/// between reflection cubemaps and the still. This QA awards zero Visual Fidelity points.
/// </summary>
public static class QualityBlockReflectionMaterialOverrideGuard
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/reflection_render_state_coherence_contract.json";
    private const string RequiredContractToken = "\"rejectUnfingerprintedMaterialPropertyBlocks\": true";

    [MenuItem("NewTown/QA/Validate No Unfingerprinted Reflection Material Overrides")]
    public static void ValidateOpenScene()
    {
        RequireQualityScene();
        ValidateContractRule();

        Renderer[] offenders = Resources.FindObjectsOfTypeAll<Renderer>()
            .Where(x => x != null &&
                        x.gameObject.scene.IsValid() &&
                        x.gameObject.scene.path == ScenePath &&
                        x.enabled &&
                        x.gameObject.activeInHierarchy &&
                        x.HasPropertyBlock())
            .OrderBy(x => HierarchyPath(x.transform), StringComparer.Ordinal)
            .ToArray();

        if (offenders.Length > 0)
        {
            string detail = string.Join(", ", offenders.Take(24).Select(x => HierarchyPath(x.transform)));
            if (offenders.Length > 24)
                detail += $", ... (+{offenders.Length - 24} more)";
            throw new InvalidOperationException(
                $"Formal reflection/still evidence contains {offenders.Length} active renderer(s) with unfingerprinted MaterialPropertyBlock overrides: {detail}. " +
                "Extend the render-state fingerprint to cover the exact override payload before using property blocks in benchmark evidence.");
        }

        Debug.Log(
            "Reflection material-override guard passed: no active benchmark renderer has an unfingerprinted MaterialPropertyBlock. " +
            "This is source/runtime-state integrity only and awards 0 Visual Fidelity points.");
    }

    private static void ValidateContractRule()
    {
        if (!File.Exists(ContractPath))
            throw new FileNotFoundException($"Reflection render-state coherence contract missing: {ContractPath}");
        string json = File.ReadAllText(ContractPath);
        if (json.IndexOf(RequiredContractToken, StringComparison.Ordinal) < 0)
            throw new InvalidOperationException(
                "Reflection render-state coherence contract no longer requires rejection of unfingerprinted MaterialPropertyBlock overrides.");
    }

    private static string HierarchyPath(Transform transform)
    {
        if (transform == null)
            return string.Empty;

        var segments = new System.Collections.Generic.Stack<string>();
        Transform current = transform;
        while (current != null && current.gameObject.scene.IsValid())
        {
            segments.Push(current.name + "[" + current.GetSiblingIndex() + "]");
            current = current.parent;
        }
        return string.Join("/", segments);
    }

    private static void RequireQualityScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            throw new InvalidOperationException($"Open {ScenePath} before validating reflection material overrides.");
    }
}
