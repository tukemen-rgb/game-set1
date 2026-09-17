using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Fail-closed uniqueness guard for apartment sliding-sash hardware.
///
/// The benchmark already has one authoritative, period-plausible crescent-latch implementation with
/// dedicated construction metadata, LOD0/1/2/3 and reflection binding. Adding a second plausible lock
/// system would reduce fidelity: two manufactured mechanisms would occupy one meeting-stile interface,
/// producing duplicate silhouettes/specular centroids and possible z-fight/intersection. This guard makes
/// "one two-panel sash -> one authoritative lock assembly" an explicit formal-render invariant.
///
/// It is read-only in render callbacks and awards no Visual Fidelity points. Real native-4K pixels remain
/// required to clear repetition, intersection, shimmer and LOD-pop critical defects.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockSashHardwareUniquenessQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/sash_hardware_uniqueness_contract.json";
    private const string AuthorizedRootName = "FacadeSashLatchHardware";
    private const string DanchiRootName = "Danchi";
    private const string LegacyHandlePrefix = "HD_WindowHandle_";
    private const int ExpectedWindowCount = 30;
    private const int ExpectedLegacyRendererCount = 60;

    private static readonly string[] CandidateTokens =
    {
        "SashLatch",
        "SashCrescent",
        "CrescentLock",
        "CrescentLatch",
        "WindowLock"
    };

    private static readonly string[] FormalCameraCoverage =
    {
        "QA4K_*",
        "QATemporal_*",
        "QAPreparedTemporal_*",
        "CameraType.Reflection"
    };

    private static readonly string[] RequiredCriticalDefects =
    {
        "obvious_repetition",
        "hero_geometry_intersection",
        "severe_aliasing_or_shimmer",
        "visible_lod_pop",
        "missing_construction_material_metadata",
        "unverified_render_claim"
    };

    private static bool validating;

    static QualityBlockSashHardwareUniquenessQA()
    {
        Camera.onPreCull -= OnCameraPreCull;
        Camera.onPreCull += OnCameraPreCull;
    }

    [MenuItem("NewTown/QA/Validate Sash Hardware Uniqueness Contract")]
    public static void ValidateConfigOnly()
    {
        SashHardwareUniquenessContract contract = LoadContract();
        if (contract == null || contract.qa == null)
            throw new InvalidOperationException("Sash hardware uniqueness contract is null or incomplete.");

        var errors = new List<string>();
        Require(string.Equals(contract.schemaVersion, "1.0.0", StringComparison.Ordinal), "schemaVersion must remain 1.0.0.", errors);
        Require(string.Equals(contract.gateId, "sash_hardware_uniqueness", StringComparison.Ordinal), "gateId mismatch.", errors);
        Require(string.Equals(contract.benchmarkScenePath, ScenePath, StringComparison.Ordinal), "benchmarkScenePath mismatch.", errors);
        Require(string.Equals(contract.authorizedRootName, AuthorizedRootName, StringComparison.Ordinal), "authorizedRootName mismatch.", errors);
        Require(string.Equals(contract.authorizedAssemblyId, "apartment_sliding_sash_crescent_latch", StringComparison.Ordinal), "authorizedAssemblyId mismatch.", errors);
        Require(string.Equals(contract.physicalLatchContractPath, "Assets/QA/facade_sash_latch_contract.json", StringComparison.Ordinal), "physicalLatchContractPath mismatch.", errors);
        Require(string.Equals(contract.metadataAuthorityContractPath, "Assets/QA/sash_latch_metadata_authority_contract.json", StringComparison.Ordinal), "metadataAuthorityContractPath mismatch.", errors);
        Require(string.Equals(contract.legacyHandlePrefix, LegacyHandlePrefix, StringComparison.Ordinal), "legacyHandlePrefix mismatch.", errors);
        Require(contract.expectedWindowCount == ExpectedWindowCount, $"expectedWindowCount must remain {ExpectedWindowCount}.", errors);
        Require(contract.expectedLegacyRendererCount == ExpectedLegacyRendererCount,
            $"expectedLegacyRendererCount must remain {ExpectedLegacyRendererCount}.", errors);
        RequireExactSet(contract.candidateNameTokens, CandidateTokens, "candidateNameTokens", errors);
        RequireExactSet(contract.formalCameraCoverage, FormalCameraCoverage, "formalCameraCoverage", errors);
        RequireExactSet(contract.criticalDefectIds, RequiredCriticalDefects, "criticalDefectIds", errors);
        RequireText(contract.authorityRule, "authorityRule", errors);
        RequireText(contract.constructionReasoning, "constructionReasoning", errors);
        RequireText(contract.renderBoundaryRule, "renderBoundaryRule", errors);
        RequireText(contract.lookdevBrief, "lookdevBrief", errors);

        Require(contract.qa.requireExactlyOneAuthorizedRootForGeneratedFallback,
            "requireExactlyOneAuthorizedRootForGeneratedFallback may not be disabled.", errors);
        Require(contract.qa.requireAuthorizedRootDirectlyUnderDanchi,
            "requireAuthorizedRootDirectlyUnderDanchi may not be disabled.", errors);
        Require(contract.qa.rejectActiveCandidateOutsideAuthorizedRoot,
            "rejectActiveCandidateOutsideAuthorizedRoot may not be disabled.", errors);
        Require(contract.qa.requireLegacyFallbackDisabled, "requireLegacyFallbackDisabled may not be disabled.", errors);
        Require(contract.qa.delegatePhysicalLatchValidation, "delegatePhysicalLatchValidation may not be disabled.", errors);
        Require(contract.qa.delegateMetadataAuthorityValidation, "delegateMetadataAuthorityValidation may not be disabled.", errors);
        Require(contract.qa.readOnlyDuringFormalCull, "readOnlyDuringFormalCull may not be disabled.", errors);
        Require(contract.qa.automaticVisualPoints == 0, "automaticVisualPoints must remain zero.", errors);
        Require(contract.qa.renderVerificationPending,
            "renderVerificationPending must remain true until actual native-4K review exists.", errors);

        if (errors.Count > 0)
            throw new InvalidOperationException("Sash hardware uniqueness contract FAILED:\n - " + string.Join("\n - ", errors));
    }

    [MenuItem("NewTown/QA/Validate Sash Hardware Uniqueness + Formal Scene")]
    public static void ValidateFormalScene()
    {
        if (validating)
            return;

        validating = true;
        try
        {
            ValidateConfigOnly();
            Scene active = SceneManager.GetActiveScene();
            if (!active.IsValid() || !string.Equals(active.path, ScenePath, StringComparison.Ordinal))
                throw new InvalidOperationException("Sash hardware uniqueness QA requires the canonical benchmark scene to be active.");

            // Delegate first: the authoritative system must satisfy its own construction/material metadata,
            // LOD, seating, legacy-handle and physical-material constraints before uniqueness is evaluated.
            QualityBlockSashLatchMetadataAuthorityQA.ValidateFormalScene();

            GameObject danchi = FindSceneObject(DanchiRootName);
            if (danchi == null)
                throw new InvalidOperationException("Danchi root is missing from the canonical benchmark scene.");

            bool authoredDanchi = IsAuthoredDanchiActive();
            GameObject[] authorizedRoots = FindSceneObjectsByExactName(AuthorizedRootName);

            if (authoredDanchi)
            {
                if (authorizedRoots.Length != 0)
                    throw new InvalidOperationException(
                        "Generated FacadeSashLatchHardware must be absent while authored danchi art is authoritative.");
            }
            else
            {
                if (authorizedRoots.Length != 1)
                    throw new InvalidOperationException(
                        $"Generated fallback requires exactly one {AuthorizedRootName} root; found {authorizedRoots.Length}.");

                GameObject authorized = authorizedRoots[0];
                if (!authorized.activeInHierarchy)
                    throw new InvalidOperationException("Authorized sash-latch root is inactive during formal evidence.");
                if (authorized.transform.parent != danchi.transform)
                    throw new InvalidOperationException("Authorized sash-latch root must remain parented directly under Danchi.");
            }

            GameObject canonicalRoot = authorizedRoots.Length == 1 ? authorizedRoots[0] : null;
            List<GameObject> rogueCandidates = FindRogueActiveCandidates(canonicalRoot);
            if (rogueCandidates.Count > 0)
            {
                string names = string.Join(", ", rogueCandidates.Select(HierarchyPath).OrderBy(x => x, StringComparer.Ordinal));
                throw new InvalidOperationException(
                    "Multiple/unauthorized sash-lock hardware hierarchies are active during formal evidence: " + names);
            }

            if (!authoredDanchi)
            {
                MeshRenderer[] legacy = Resources.FindObjectsOfTypeAll<MeshRenderer>()
                    .Where(x => x != null && x.gameObject.scene.IsValid() &&
                                string.Equals(x.gameObject.scene.path, ScenePath, StringComparison.Ordinal) &&
                                x.name.StartsWith(LegacyHandlePrefix, StringComparison.Ordinal))
                    .ToArray();
                if (legacy.Length != ExpectedLegacyRendererCount)
                    throw new InvalidOperationException(
                        $"Expected {ExpectedLegacyRendererCount} legacy fallback handle renderers for provenance, found {legacy.Length}.");
                if (legacy.Any(x => x.enabled && x.gameObject.activeInHierarchy))
                    throw new InvalidOperationException("Legacy HD_WindowHandle_ fallback renderer became visible during formal evidence.");

                LODGroup[] groups = canonicalRoot.GetComponentsInChildren<LODGroup>(true);
                if (groups.Length != ExpectedWindowCount)
                    throw new InvalidOperationException(
                        $"Authorized sash-latch root must retain {ExpectedWindowCount} per-window LODGroups; found {groups.Length}.");
            }

            Debug.Log(
                "Sash hardware uniqueness QA passed: one authoritative generated crescent-latch system (or none under authored replacement), " +
                "no active renderer-bearing sash/crescent/window-lock candidate outside that authority, and legacy fallback handles remain disabled. " +
                "This awards 0 Visual Fidelity points; actual 4K pixels still decide repetition/intersection/temporal defects.");
        }
        finally
        {
            validating = false;
        }
    }

    private static void OnCameraPreCull(Camera camera)
    {
        if (camera == null || validating || !IsFormalEvidenceCamera(camera))
            return;

        // Intentionally no once-per-frame cache: reflection and final still/temporal culls in the same
        // frame must each observe uniqueness so a mid-frame hierarchy toggle cannot evade the boundary.
        ValidateFormalScene();
    }

    private static bool IsFormalEvidenceCamera(Camera camera)
    {
        Scene scene = camera.gameObject.scene;
        if (!scene.IsValid() || !string.Equals(scene.path, ScenePath, StringComparison.Ordinal))
            return false;

        if (camera.cameraType == CameraType.Reflection)
            return true;

        string name = camera.name ?? string.Empty;
        return name.StartsWith("QA4K_", StringComparison.Ordinal) ||
               name.StartsWith("QATemporal_", StringComparison.Ordinal) ||
               name.StartsWith("QAPreparedTemporal_", StringComparison.Ordinal);
    }

    private static List<GameObject> FindRogueActiveCandidates(GameObject canonicalRoot)
    {
        var result = new List<GameObject>();
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>()
            .Where(x => x != null && x.scene.IsValid() &&
                        string.Equals(x.scene.path, ScenePath, StringComparison.Ordinal) &&
                        x.activeInHierarchy)
            .ToArray();

        foreach (GameObject candidate in objects)
        {
            if (!MatchesCandidateToken(candidate.name))
                continue;
            if (canonicalRoot != null &&
                (candidate == canonicalRoot || candidate.transform.IsChildOf(canonicalRoot.transform)))
                continue;

            bool hasRenderContent = candidate.GetComponent<MeshRenderer>() != null ||
                                    candidate.GetComponent<LODGroup>() != null ||
                                    candidate.GetComponentInChildren<MeshRenderer>(true) != null ||
                                    candidate.GetComponentInChildren<LODGroup>(true) != null;
            if (!hasRenderContent)
                continue;

            // Report only the highest candidate ancestor outside the canonical hierarchy so a duplicate
            // root with many matching children generates one actionable failure path rather than noise.
            Transform parent = candidate.transform.parent;
            bool candidateAncestorExists = false;
            while (parent != null)
            {
                if (canonicalRoot != null && parent == canonicalRoot.transform)
                    break;
                if (MatchesCandidateToken(parent.name))
                {
                    candidateAncestorExists = true;
                    break;
                }
                parent = parent.parent;
            }
            if (!candidateAncestorExists)
                result.Add(candidate);
        }

        return result
            .Distinct()
            .OrderBy(x => HierarchyPath(x), StringComparer.Ordinal)
            .ToList();
    }

    private static bool MatchesCandidateToken(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;
        return CandidateTokens.Any(token => name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static bool IsAuthoredDanchiActive()
    {
        return Resources.FindObjectsOfTypeAll<QualityBlockArtSlot>()
            .Any(x => x != null && x.gameObject.scene.IsValid() &&
                      string.Equals(x.gameObject.scene.path, ScenePath, StringComparison.Ordinal) &&
                      x.SlotId == "danchi.main" && x.IsUsingAuthoredArt);
    }

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x != null && x.scene.IsValid() &&
                                 string.Equals(x.scene.path, ScenePath, StringComparison.Ordinal) &&
                                 string.Equals(x.name, name, StringComparison.Ordinal));
    }

    private static GameObject[] FindSceneObjectsByExactName(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .Where(x => x != null && x.scene.IsValid() &&
                        string.Equals(x.scene.path, ScenePath, StringComparison.Ordinal) &&
                        string.Equals(x.name, name, StringComparison.Ordinal))
            .ToArray();
    }

    private static string HierarchyPath(GameObject gameObject)
    {
        return gameObject == null ? "<null>" : HierarchyPath(gameObject.transform);
    }

    private static string HierarchyPath(Transform transform)
    {
        var names = new List<string>();
        Transform cursor = transform;
        while (cursor != null)
        {
            names.Add(cursor.name);
            cursor = cursor.parent;
        }
        names.Reverse();
        return string.Join("/", names);
    }

    private static SashHardwareUniquenessContract LoadContract()
    {
        string absolute = AbsolutePath(ContractPath);
        if (!File.Exists(absolute))
            throw new FileNotFoundException("Sash hardware uniqueness contract is missing: " + ContractPath);
        return JsonUtility.FromJson<SashHardwareUniquenessContract>(File.ReadAllText(absolute));
    }

    private static string AbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private static void Require(bool condition, string message, List<string> errors)
    {
        if (!condition)
            errors.Add(message);
    }

    private static void RequireText(string value, string label, List<string> errors)
    {
        Require(!string.IsNullOrWhiteSpace(value), label + " is required.", errors);
    }

    private static void RequireExactSet(string[] actual, string[] expected, string label, List<string> errors)
    {
        string[] left = (actual ?? Array.Empty<string>()).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        string[] right = (expected ?? Array.Empty<string>()).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        if (!left.SequenceEqual(right, StringComparer.Ordinal))
            errors.Add(label + " must exactly match the canonical set.");
    }

    [Serializable]
    private sealed class SashHardwareUniquenessContract
    {
        public string schemaVersion;
        public string gateId;
        public string benchmarkScenePath;
        public string authorizedRootName;
        public string authorizedAssemblyId;
        public string physicalLatchContractPath;
        public string metadataAuthorityContractPath;
        public string legacyHandlePrefix;
        public int expectedWindowCount;
        public int expectedLegacyRendererCount;
        public string[] candidateNameTokens;
        public string authorityRule;
        public string constructionReasoning;
        public string renderBoundaryRule;
        public string lookdevBrief;
        public string[] formalCameraCoverage;
        public Qa qa;
        public string[] criticalDefectIds;
    }

    [Serializable]
    private sealed class Qa
    {
        public bool requireExactlyOneAuthorizedRootForGeneratedFallback;
        public bool requireAuthorizedRootDirectlyUnderDanchi;
        public bool rejectActiveCandidateOutsideAuthorizedRoot;
        public bool requireLegacyFallbackDisabled;
        public bool delegatePhysicalLatchValidation;
        public bool delegateMetadataAuthorityValidation;
        public bool readOnlyDuringFormalCull;
        public int automaticVisualPoints;
        public bool renderVerificationPending;
    }
}