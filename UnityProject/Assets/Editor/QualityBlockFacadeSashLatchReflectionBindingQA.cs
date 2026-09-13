using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Persists the generated sash-latch formal-evidence epoch across script/assembly reloads.
/// The parent latch builder historically kept its epoch only in static fields and GetInstanceID(),
/// which can reset after a domain reload. This wrapper establishes the conservative evidence boundary
/// used by the reflection fingerprint: before the first baseline it may delegate one deterministic
/// preparation to the parent builder; after arming, SessionState + scene GUID + stable GlobalObjectId
/// make every subsequent request/poll/completion/pre-still check read-only and fail closed.
///
/// This class proves evidence identity/coherence only. It awards zero Visual Fidelity points and cannot
/// clear rendered scale, highlight, aliasing, LOD-pop, intersection or repetition defects without actual
/// native 3840x2160 evidence.
/// </summary>
public static class QualityBlockFacadeSashLatchReflectionBindingQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string RootName = "FacadeSashLatchHardware";
    private const string ContractPath = "Assets/QA/facade_sash_latch_reflection_binding_contract.json";
    private const string ParentContractPath = "Assets/QA/facade_sash_latch_contract.json";
    private const string ContractSchema = "1.0";

    private const string SessionPrefix = "QualityBlock.FacadeSashLatchReflectionBinding.";
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

    [MenuItem("NewTown/QA/Validate Facade Sash-Latch Reflection Binding Contract")]
    public static void ValidateContractConfigOnly()
    {
        BindingContract contract = LoadJson<BindingContract>(ContractPath);
        if (contract == null || !string.Equals(contract.schemaVersion, ContractSchema, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Facade sash-latch reflection-binding contract is null/unparseable or not schema " + ContractSchema + ".");
        if (!string.Equals(contract.scenePath, ScenePath, StringComparison.Ordinal))
            throw new InvalidOperationException("Facade sash-latch reflection-binding scene identity drifted.");
        if (!string.Equals(contract.parentConstructionContractPath, ParentContractPath, StringComparison.Ordinal))
            throw new InvalidOperationException("Facade sash-latch reflection binding no longer targets the canonical construction contract.");

        Requirements r = contract.requirements;
        if (r == null ||
            !r.prepareMissingGeneratedLatchOnlyBeforeFirstReflectionBaseline ||
            !r.freezeGeneratedRootGlobalObjectIdAfterFirstReflectionBaseline ||
            !r.persistEpochAcrossAssemblyReloadsWithSessionState ||
            !r.validateSceneGuidAcrossEpoch ||
            !r.authoredFallbackModeMustRemainStableAfterEpochArm ||
            !r.validationMustBeReadOnlyAfterEpochArm ||
            !r.validateParentConstructionBeforeEveryReflectionLightingFingerprint ||
            !r.rootReplacementOrDeletionMustAbortEvidence ||
            !r.actualRenderRequiredForVisualPoints)
            throw new InvalidOperationException(
                "Facade sash-latch reflection-binding requirements were weakened or are incomplete.");

        string[] expectedRisks =
        {
            "hero_geometry_intersection",
            "impossible_material_physics",
            "visible_lod_pop",
            "missing_construction_material_metadata"
        };
        RequireExactSet(contract.criticalDefectRisksReduced, expectedRisks, "criticalDefectRisksReduced");

        if (contract.visualFidelityPointsAwarded != 0 || contract.runtimeRenderVerified)
            throw new InvalidOperationException(
                "Facade sash-latch reflection binding may not award Visual Fidelity points or claim runtime render verification.");
        if (contract.limitations == null || contract.limitations.Length < 6 || contract.limitations.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Facade sash-latch reflection-binding limitations are missing or incomplete.");

        QualityBlockFacadeSashLatchUpgrade.ValidateContractConfigOnly();
    }

    /// <summary>
    /// The first call in a clean Editor session may create a genuinely missing generated latch root through
    /// the parent builder. Once armed, this method never calls a latch mutator again; it validates stable
    /// persisted identity and the exact parent construction state only.
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

        // The curve and LOD asset refinements are prepared by the central reflection binding before this
        // call, so the parent builder resolves the already-refined canonical mesh paths on first creation.
        QualityBlockFacadeSashLatchUpgrade.EnsurePreparedForFormalEvidence();

        bool authored = IsAuthoredDanchiActive();
        GameObject root = FindSceneObject(RootName);
        string sceneGuid = RequireSceneGuid();
        string rootGlobalObjectId = string.Empty;

        if (authored)
        {
            if (root != null)
                throw new InvalidOperationException(
                    "Generated sash-latch root must be absent when authored danchi art is authoritative.");
        }
        else
        {
            if (root == null)
                throw new InvalidOperationException(
                    "Facade sash-latch root is missing after formal pre-baseline preparation.");
            QualityBlockFacadeSashLatchUpgrade.ValidateOpenScene();
            rootGlobalObjectId = RequireStableGlobalObjectId(root);
        }

        SessionState.SetBool(AuthoredEpochKey, authored);
        SessionState.SetString(RootGlobalObjectIdKey, rootGlobalObjectId);
        SessionState.SetString(SceneGuidKey, sceneGuid);
        SessionState.SetString(EpochTokenKey, Guid.NewGuid().ToString("N"));
        SessionState.SetBool(EpochArmedKey, true);

        Debug.Log(
            "Facade sash-latch reflection epoch armed fail-closed for this Editor session: " +
            $"authored={authored}, sceneGuid={sceneGuid}, rootGlobalObjectId={rootGlobalObjectId}, epochToken={EpochToken}. " +
            "Assembly reloads may not reinterpret replacement latch geometry as a fresh baseline. Visual Fidelity remains UNSCORED.");
    }

    /// <summary>
    /// Strictly read-only post-arm validation for reflection request/poll/completion/pre-still boundaries.
    /// </summary>
    public static void ValidateForReflectionEvidence()
    {
        ValidateContractConfigOnly();
        EnsureBenchmarkSceneIsActive();
        if (!EpochArmed)
            throw new InvalidOperationException(
                "Facade sash-latch reflection epoch has not been armed. Formal preparation must run before reflection evidence.");

        string currentSceneGuid = RequireSceneGuid();
        if (string.IsNullOrWhiteSpace(ArmedSceneGuid) ||
            !string.Equals(currentSceneGuid, ArmedSceneGuid, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Benchmark scene GUID changed after the sash-latch reflection epoch was armed. Start a clean Editor session and capture a fresh baseline.");
        if (string.IsNullOrWhiteSpace(EpochToken))
            throw new InvalidOperationException(
                "Facade sash-latch reflection epoch token is missing. Session-state evidence is incomplete; recapture from a clean Editor session.");

        bool authored = IsAuthoredDanchiActive();
        if (authored != AuthoredEpoch)
            throw new InvalidOperationException(
                "Danchi authored/fallback mode changed after the sash-latch reflection epoch was armed.");

        GameObject root = FindSceneObject(RootName);
        if (AuthoredEpoch)
        {
            if (root != null)
                throw new InvalidOperationException(
                    "Generated sash-latch root appeared after an authored-danchi reflection epoch was armed.");
            QualityBlockFacadeSashLatchUpgrade.ValidateOpenScene();
            return;
        }

        if (root == null)
            throw new InvalidOperationException(
                "Facade sash-latch root was deleted after the reflection epoch was armed. In-flight evidence may not auto-repair it.");

        string currentGlobalObjectId = RequireStableGlobalObjectId(root);
        if (string.IsNullOrWhiteSpace(ArmedRootGlobalObjectId) ||
            !string.Equals(currentGlobalObjectId, ArmedRootGlobalObjectId, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Facade sash-latch root stable identity changed after the reflection epoch was armed. " +
                "A replacement object may not inherit an existing reflection baseline; restart the Editor and recapture.");

        QualityBlockFacadeSashLatchUpgrade.ValidateOpenScene();
    }

    private static string RequireSceneGuid()
    {
        string guid = AssetDatabase.AssetPathToGUID(ScenePath);
        if (string.IsNullOrWhiteSpace(guid))
            throw new InvalidOperationException("Could not resolve the persisted benchmark scene GUID for sash-latch reflection binding.");
        return guid;
    }

    private static string RequireStableGlobalObjectId(GameObject root)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));
        GlobalObjectId id = GlobalObjectId.GetGlobalObjectIdSlow(root);
        string value = id.ToString();
        if (string.IsNullOrWhiteSpace(value) || value.StartsWith("GlobalObjectId_V1-0-", StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Facade sash-latch root has no stable persisted GlobalObjectId. Save the benchmark scene before formal reflection evidence; unsaved replacement geometry is not scoreable evidence.");
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
                "Facade sash-latch reflection binding may run only in the active QualityBlock1990s benchmark scene.");
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
            throw new InvalidOperationException("Could not resolve Unity project root for sash-latch reflection binding.");
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
        public string parentConstructionContractPath;
        public Requirements requirements;
        public string[] criticalDefectRisksReduced;
        public int visualFidelityPointsAwarded;
        public bool runtimeRenderVerified;
        public string[] limitations;
    }

    [Serializable]
    private sealed class Requirements
    {
        public bool prepareMissingGeneratedLatchOnlyBeforeFirstReflectionBaseline;
        public bool freezeGeneratedRootGlobalObjectIdAfterFirstReflectionBaseline;
        public bool persistEpochAcrossAssemblyReloadsWithSessionState;
        public bool validateSceneGuidAcrossEpoch;
        public bool authoredFallbackModeMustRemainStableAfterEpochArm;
        public bool validationMustBeReadOnlyAfterEpochArm;
        public bool validateParentConstructionBeforeEveryReflectionLightingFingerprint;
        public bool rootReplacementOrDeletionMustAbortEvidence;
        public bool actualRenderRequiredForVisualPoints;
    }
}
