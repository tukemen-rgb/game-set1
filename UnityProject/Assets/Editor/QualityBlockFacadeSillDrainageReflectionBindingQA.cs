using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Freezes the facade sill-drainage construction state at the first formal reflection-lighting
/// baseline. Before the epoch is armed, a genuinely missing generated sill pass may be prepared.
/// After arming, every reflection fingerprint evaluation is read-only: root replacement/deletion,
/// authored/fallback mode changes, legacy-sill re-enablement, LOD/material/topology drift or other
/// structural failures abort reflection evidence instead of silently repairing it.
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

    private static bool epochArmed;
    private static bool authoredEpoch;
    private static int armedRootInstanceId;

    static QualityBlockFacadeSillDrainageReflectionBindingQA()
    {
        EditorSceneManager.sceneOpened -= OnSceneOpened;
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    [MenuItem("NewTown/QA/Validate Facade Sill Reflection Binding Contract")]
    public static void ValidateContractConfigOnly()
    {
        BindingContract contract = LoadJson<BindingContract>(ContractPath);
        if (contract == null || !string.Equals(contract.schemaVersion, "1.0", StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Facade sill reflection-binding contract is null/unparseable or not schema 1.0.");
        if (!string.Equals(contract.scenePath, ScenePath, StringComparison.Ordinal))
            throw new InvalidOperationException("Facade sill reflection-binding scene identity drifted.");
        if (!string.Equals(contract.sillConstructionContractPath, SillContractPath, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Facade sill reflection binding no longer targets the canonical sill construction contract.");

        Requirements r = contract.requirements;
        if (r == null ||
            !r.prepareMissingGeneratedSillOnlyBeforeFirstReflectionBaseline ||
            !r.freezeGeneratedRootInstanceAfterFirstReflectionBaseline ||
            !r.validateSillBeforeEveryReflectionLightingFingerprint ||
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
            "missing_construction_material_metadata"
        };
        RequireExactSet(contract.criticalDefectRisksReduced, requiredRisks,
            "criticalDefectRisksReduced");

        if (contract.visualFidelityPointsAwarded != 0 || contract.runtimeRenderVerified)
            throw new InvalidOperationException(
                "Facade sill reflection binding may not award Visual Fidelity points or claim runtime render verification.");
        if (contract.limitations == null || contract.limitations.Length < 5 ||
            contract.limitations.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException(
                "Facade sill reflection-binding limitations are missing or incomplete.");

        QualityBlockFacadeSillDrainageUpgrade.ValidateContractConfigOnly();
    }

    /// <summary>
    /// Called before the first reflection fingerprint and again on subsequent fingerprint evaluations.
    /// The first call may prepare missing fallback sill geometry. Once armed, this method delegates to
    /// read-only validation only; it never rebuilds or repairs the sill pass.
    /// </summary>
    public static void EnsurePreparedForFormalEvidence()
    {
        ValidateContractConfigOnly();
        EnsureBenchmarkSceneIsActive();

        if (epochArmed)
        {
            ValidateForReflectionEvidence();
            return;
        }

        QualityBlockFacadeSillDrainageUpgrade.EnsurePreparedForFormalEvidence();

        bool authored = IsAuthoredDanchiActive();
        GameObject root = FindSceneObject(RootName);
        if (authored)
        {
            if (root != null)
                throw new InvalidOperationException(
                    "Generated sill-drainage root must be absent when authored danchi art is authoritative.");
            authoredEpoch = true;
            armedRootInstanceId = 0;
        }
        else
        {
            if (root == null)
                throw new InvalidOperationException(
                    "Facade sill-drainage root is missing after formal pre-baseline preparation.");
            QualityBlockFacadeSillDrainageUpgrade.ValidateOpenScene();
            authoredEpoch = false;
            armedRootInstanceId = root.GetInstanceID();
        }

        epochArmed = true;
    }

    /// <summary>
    /// Strictly read-only validation used by reflection request/poll/completion/pre-still fingerprints.
    /// </summary>
    public static void ValidateForReflectionEvidence()
    {
        ValidateContractConfigOnly();
        EnsureBenchmarkSceneIsActive();
        if (!epochArmed)
            throw new InvalidOperationException(
                "Facade sill reflection epoch has not been armed. Formal preparation must run before reflection evidence.");

        bool authored = IsAuthoredDanchiActive();
        if (authored != authoredEpoch)
            throw new InvalidOperationException(
                "Danchi authored/fallback mode changed after the facade sill reflection epoch was armed.");

        GameObject root = FindSceneObject(RootName);
        if (authoredEpoch)
        {
            if (root != null)
                throw new InvalidOperationException(
                    "Generated sill-drainage root appeared after an authored-danchi reflection epoch was armed.");
            return;
        }

        if (root == null)
            throw new InvalidOperationException(
                "Facade sill-drainage root was deleted after the reflection epoch was armed.");
        if (root.GetInstanceID() != armedRootInstanceId)
            throw new InvalidOperationException(
                "Facade sill-drainage root was replaced after the reflection epoch was armed. Recapture from a fresh baseline.");

        // Read-only: this validator checks renderer/material/mesh/LOD/weathering/topology state but does not rebuild it.
        QualityBlockFacadeSillDrainageUpgrade.ValidateOpenScene();
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        epochArmed = false;
        authoredEpoch = false;
        armedRootInstanceId = 0;
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
        Scene scene = EditorSceneManager.GetActiveScene();
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
        public bool freezeGeneratedRootInstanceAfterFirstReflectionBaseline;
        public bool validateSillBeforeEveryReflectionLightingFingerprint;
        public bool validationMustBeReadOnlyAfterEpochArm;
        public bool authoredFallbackModeMustRemainStableAfterEpochArm;
        public bool sillFailureAbortsReflectionEvidence;
        public bool actualRenderRequiredForVisualPoints;
    }
}
