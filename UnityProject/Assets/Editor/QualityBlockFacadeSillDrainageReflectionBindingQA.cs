using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Freezes the facade sill-drainage construction state at the first formal reflection-lighting
/// baseline. Before the epoch is armed, a genuinely missing generated sill pass and its deterministic
/// tray edge-break refinement may be prepared. After arming, every reflection fingerprint evaluation
/// is read-only: root replacement/deletion, mesh/proxy drift, authored/fallback mode changes,
/// legacy-sill re-enablement, LOD/material/topology drift or other structural failures abort evidence.
///
/// SessionState plus GlobalObjectId preserve the binding across script/assembly reloads within the same
/// Editor session. A clean Editor restart remains the conservative reset boundary for a new formal epoch.
///
/// This is evidence-coherence infrastructure only. It awards zero Visual Fidelity points and cannot
/// clear any rendered critical defect without sealed native-4K evidence.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockFacadeSillDrainageReflectionBindingQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string RootName = "FacadeSillDrainageInterfaces";
    private const string ContractPath = "Assets/QA/facade_sill_drainage_reflection_binding_contract.json";
    private const string SillContractPath = "Assets/QA/facade_sill_drainage_contract.json";
    private const string SillEdgeBreakContractPath = "Assets/QA/facade_sill_tray_edge_break_contract.json";
    private const string ContractSchema = "1.2";

    private const string SessionPrefix = "QualityBlock.FacadeSillReflectionBinding.";
    private const string EpochArmedKey = SessionPrefix + "EpochArmed";
    private const string AuthoredEpochKey = SessionPrefix + "AuthoredEpoch";
    private const string RootGlobalObjectIdKey = SessionPrefix + "RootGlobalObjectId";
    private const string SceneGuidKey = SessionPrefix + "SceneGuid";
    private const string EpochTokenKey = SessionPrefix + "EpochToken";

    private static bool EpochArmed => SessionState.GetBool(EpochArmedKey, false);
    private static bool AuthoredEpoch => SessionState.GetBool(AuthoredEpochKey, false);
    private static string ArmedRootGlobalObjectId => SessionState.GetString(RootGlobalObjectIdKey, string.Empty);
    private static string ArmedSceneGuid => SessionState.GetString(SceneGuidKey, string.Empty);
    private static string EpochToken => SessionState.GetString(EpochTokenKey, string.Empty);

    [MenuItem("NewTown/QA/Validate Facade Sill Reflection Binding Contract")]
    public static void ValidateContractConfigOnly()
    {
        BindingContract contract = LoadJson<BindingContract>(ContractPath);
        if (contract == null || !string.Equals(contract.schemaVersion, ContractSchema, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Facade sill reflection-binding contract is null/unparseable or not schema " + ContractSchema + ".");
        if (!string.Equals(contract.scenePath, ScenePath, StringComparison.Ordinal))
            throw new InvalidOperationException("Facade sill reflection-binding scene identity drifted.");
        if (!string.Equals(contract.sillConstructionContractPath, SillContractPath, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Facade sill reflection binding no longer targets the canonical sill construction contract.");
        if (!string.Equals(contract.sillTrayEdgeBreakContractPath, SillEdgeBreakContractPath, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Facade sill reflection binding no longer targets the canonical tray edge-break contract.");

        Requirements r = contract.requirements;
        if (r == null ||
            !r.prepareMissingGeneratedSillOnlyBeforeFirstReflectionBaseline ||
            !r.ensureSillTrayEdgeBreakBeforeFirstReflectionBaseline ||
            !r.freezeGeneratedRootGlobalObjectIdAfterFirstReflectionBaseline ||
            !r.freezeSillTrayMeshFingerprintAfterFirstReflectionBaseline ||
            !r.persistEpochAcrossAssemblyReloadsWithSessionState ||
            !r.validateSceneGuidAcrossEpoch ||
            !r.validateSillBeforeEveryReflectionLightingFingerprint ||
            !r.validateSillTrayEdgeBreakBeforeEveryReflectionLightingFingerprint ||
            !r.validationMustBeReadOnlyAfterEpochArm ||
            !r.authoredFallbackModeMustRemainStableAfterEpochArm ||
            !r.sillFailureAbortsReflectionEvidence ||
            !r.actualRenderRequiredForVisualPoints)
            throw new InvalidOperationException(
                "Facade sill reflection-binding requirements were weakened or are incomplete.");

        string[] requiredRisks =
        {
            "baked_or_painted_highlights",
            "impossible_material_physics",
            "visible_lod_pop",
            "missing_construction_material_metadata"
        };
        RequireExactSet(contract.criticalDefectRisksReduced, requiredRisks,
            "criticalDefectRisksReduced");

        if (contract.visualFidelityPointsAwarded != 0 || contract.runtimeRenderVerified)
            throw new InvalidOperationException(
                "Facade sill reflection binding may not award Visual Fidelity points or claim runtime render verification.");
        if (contract.limitations == null || contract.limitations.Length < 7 ||
            contract.limitations.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException(
                "Facade sill reflection-binding limitations are missing or incomplete.");

        QualityBlockFacadeSillDrainageUpgrade.ValidateContractConfigOnly();
        QualityBlockFacadeSillTrayEdgeBreakRefinement.ValidateContractConfigOnly();
    }

    /// <summary>
    /// Called before the first reflection fingerprint and again on subsequent fingerprint evaluations.
    /// The first call may prepare missing fallback sill geometry and refine the tray/proxy mesh assets.
    /// Once armed, both bindings survive assembly reloads and delegate to read-only validation only.
    /// </summary>
    public static void EnsurePreparedForFormalEvidence()
    {
        ValidateContractConfigOnly();
        EnsureBenchmarkSceneIsActive();

        if (EpochArmed)
        {
            ValidateForReflectionEvidence();
            return;
        }

        QualityBlockFacadeSillDrainageUpgrade.EnsurePreparedForFormalEvidence();
        QualityBlockFacadeSillTrayEdgeBreakRefinement.EnsurePreparedForFormalEvidence();

        bool authored = IsAuthoredDanchiActive();
        GameObject root = FindSceneObject(RootName);
        string sceneGuid = RequireSceneGuid();
        string rootGlobalObjectId = string.Empty;

        if (authored)
        {
            if (root != null)
                throw new InvalidOperationException(
                    "Generated sill-drainage root must be absent when authored danchi art is authoritative.");
        }
        else
        {
            if (root == null)
                throw new InvalidOperationException(
                    "Facade sill-drainage root is missing after formal pre-baseline preparation.");
            QualityBlockFacadeSillDrainageUpgrade.ValidateOpenScene();
            QualityBlockFacadeSillTrayEdgeBreakRefinement.ValidateGeneratedAssets();
            rootGlobalObjectId = RequireStableGlobalObjectId(root);
        }

        SessionState.SetBool(AuthoredEpochKey, authored);
        SessionState.SetString(RootGlobalObjectIdKey, rootGlobalObjectId);
        SessionState.SetString(SceneGuidKey, sceneGuid);
        SessionState.SetString(EpochTokenKey, Guid.NewGuid().ToString("N"));
        SessionState.SetBool(EpochArmedKey, true);

        Debug.Log(
            "Facade sill reflection epoch armed fail-closed for this Editor session: " +
            $"authored={authored}, sceneGuid={sceneGuid}, rootGlobalObjectId={rootGlobalObjectId}, epochToken={EpochToken}. " +
            "Tray edge-break/proxy mesh fingerprint is frozen independently; Visual Fidelity remains UNSCORED.");
    }

    /// <summary>
    /// Strictly read-only validation used by reflection request/poll/completion/pre-still fingerprints.
    /// </summary>
    public static void ValidateForReflectionEvidence()
    {
        ValidateContractConfigOnly();
        EnsureBenchmarkSceneIsActive();
        if (!EpochArmed)
            throw new InvalidOperationException(
                "Facade sill reflection epoch has not been armed. Formal preparation must run before reflection evidence.");

        string currentSceneGuid = RequireSceneGuid();
        if (string.IsNullOrWhiteSpace(ArmedSceneGuid) ||
            !string.Equals(currentSceneGuid, ArmedSceneGuid, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Benchmark scene GUID changed after the facade sill reflection epoch was armed. Start from a clean Editor session and recapture a fresh reflection baseline.");
        if (string.IsNullOrWhiteSpace(EpochToken))
            throw new InvalidOperationException(
                "Facade sill reflection epoch token is missing. Session-state evidence is incomplete; recapture from a clean Editor session.");

        bool authored = IsAuthoredDanchiActive();
        if (authored != AuthoredEpoch)
            throw new InvalidOperationException(
                "Danchi authored/fallback mode changed after the facade sill reflection epoch was armed.");

        // The refinement owns a separate SessionState mesh fingerprint. Validate it before any early return so
        // authored/fallback epoch changes or a post-arm tray/proxy mutation cannot bypass reflection evidence QA.
        QualityBlockFacadeSillTrayEdgeBreakRefinement.ValidateGeneratedAssets();

        GameObject root = FindSceneObject(RootName);
        if (AuthoredEpoch)
        {
            if (root != null)
                throw new InvalidOperationException(
                    "Generated sill-drainage root appeared after an authored-danchi reflection epoch was armed.");
            return;
        }

        if (root == null)
            throw new InvalidOperationException(
                "Facade sill-drainage root was deleted after the reflection epoch was armed.");

        string currentGlobalObjectId = RequireStableGlobalObjectId(root);
        if (string.IsNullOrWhiteSpace(ArmedRootGlobalObjectId) ||
            !string.Equals(currentGlobalObjectId, ArmedRootGlobalObjectId, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Facade sill-drainage root stable identity changed after the reflection epoch was armed. " +
                "A replacement object may not inherit an existing reflection baseline; recapture in a clean Editor session.");

        QualityBlockFacadeSillDrainageUpgrade.ValidateOpenScene();
        QualityBlockFacadeSillTrayEdgeBreakRefinement.ValidateGeneratedAssets();
    }

    private static string RequireSceneGuid()
    {
        string guid = AssetDatabase.AssetPathToGUID(ScenePath);
        if (string.IsNullOrWhiteSpace(guid))
            throw new InvalidOperationException(
                "Could not resolve the persisted benchmark scene GUID for facade sill reflection binding.");
        return guid;
    }

    private static string RequireStableGlobalObjectId(GameObject root)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));
        GlobalObjectId id = GlobalObjectId.GetGlobalObjectIdSlow(root);
        string value = id.ToString();
        if (string.IsNullOrWhiteSpace(value) ||
            value.StartsWith("GlobalObjectId_V1-0-", StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Facade sill-drainage root has no stable persisted GlobalObjectId. Save the benchmark scene before formal reflection evidence; do not score an unsaved replacement root.");
        return value;
    }

    private static bool IsAuthoredDanchiActive()
    {
        QualityBlockArtSlot slot = Resources.FindObjectsOfTypeAll<QualityBlockArtSlot>()
            .FirstOrDefault(x => x != null && x.gameObject.scene.IsValid() &&
                                 string.Equals(x.gameObject.scene.path, ScenePath, StringComparison.Ordinal) &&
                                 x.SlotId == "danchi.main");
        return slot != null && slot.IsUsingAuthoredArt;
    }

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x != null && x.scene.IsValid() &&
                                 string.Equals(x.scene.path, ScenePath, StringComparison.Ordinal) &&
                                 string.Equals(x.name, name, StringComparison.Ordinal));
    }

    private static void EnsureBenchmarkSceneIsActive()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !string.Equals(scene.path, ScenePath, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Facade sill reflection binding may run only in the active QualityBlock1990s benchmark scene.");
    }

    private static T LoadJson<T>(string assetPath)
    {
        string absolute = AbsolutePath(assetPath);
        if (!File.Exists(absolute))
            throw new FileNotFoundException("Required QA file not found: " + assetPath);
        T value = JsonUtility.FromJson<T>(File.ReadAllText(absolute));
        if (value == null)
            throw new InvalidOperationException("Could not parse QA JSON: " + assetPath);
        return value;
    }

    private static string AbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrWhiteSpace(projectRoot))
            throw new InvalidOperationException("Could not resolve Unity project root for facade sill reflection binding.");
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private static void RequireExactSet(string[] actual, string[] expected, string label)
    {
        if (actual == null || actual.Any(string.IsNullOrWhiteSpace) ||
            actual.Distinct(StringComparer.Ordinal).Count() != actual.Length ||
            !new HashSet<string>(actual, StringComparer.Ordinal).SetEquals(expected))
            throw new InvalidOperationException(
                label + " must be exactly [" + string.Join(", ", expected) + "].");
    }

    [Serializable]
    private sealed class BindingContract
    {
        public string schemaVersion;
        public string purpose;
        public string scenePath;
        public string sillConstructionContractPath;
        public string sillTrayEdgeBreakContractPath;
        public Requirements requirements;
        public string[] criticalDefectRisksReduced;
        public int visualFidelityPointsAwarded;
        public bool runtimeRenderVerified;
        public string[] limitations;
    }

    [Serializable]
    private sealed class Requirements
    {
        public bool prepareMissingGeneratedSillOnlyBeforeFirstReflectionBaseline;
        public bool ensureSillTrayEdgeBreakBeforeFirstReflectionBaseline;
        public bool freezeGeneratedRootGlobalObjectIdAfterFirstReflectionBaseline;
        public bool freezeSillTrayMeshFingerprintAfterFirstReflectionBaseline;
        public bool persistEpochAcrossAssemblyReloadsWithSessionState;
        public bool validateSceneGuidAcrossEpoch;
        public bool validateSillBeforeEveryReflectionLightingFingerprint;
        public bool validateSillTrayEdgeBreakBeforeEveryReflectionLightingFingerprint;
        public bool validationMustBeReadOnlyAfterEpochArm;
        public bool authoredFallbackModeMustRemainStableAfterEpochArm;
        public bool sillFailureAbortsReflectionEvidence;
        public bool actualRenderRequiredForVisualPoints;
    }
}
