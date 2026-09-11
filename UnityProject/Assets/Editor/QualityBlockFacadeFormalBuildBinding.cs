using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Closes a formal-build integration gap for the generated facade. The native-4K preparation path
/// rebuilds DanchiFacadeOptics, but the physical rough-opening shell, world-aligned aperture UVs and
/// deterministic apartment curtain variation are separate passes. Reflection probes must never seal a
/// baseline before those passes exist, otherwise scoreable pixels can silently fall back to the opaque
/// MainBlock / repeated-window state even though higher-fidelity source work is present in the repo.
///
/// The first formal reflection fingerprint may repair only a genuinely missing generated pass. Once a
/// pass exists, drift is fail-closed: it is validated rather than silently rebuilt. Authored danchi art
/// remains authoritative. This integration layer awards zero Visual Fidelity points.
/// </summary>
public static class QualityBlockFacadeFormalBuildBinding
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/facade_formal_build_binding_contract.json";
    private const string OccupancyContractPath = "Assets/QA/facade_window_dressing_contract.json";
    private const string OpticsRootName = "DanchiFacadeOptics";
    private const string ApertureRootName = "DanchiFacadeApertureShell";
    private const string OccupancyRootName = "FacadeOccupancyVariation";

    private static readonly string[] CanonicalCriticalRisks =
    {
        "visible_primitive_placeholder",
        "obvious_repetition",
        "hero_geometry_intersection",
        "major_light_leak",
        "missing_construction_material_metadata"
    };

    [MenuItem("NewTown/QA/Validate Facade Formal Build Binding Contract")]
    public static void ValidateContractConfigOnly()
    {
        BindingContract contract = LoadJson<BindingContract>(ContractPath);
        if (contract == null || !string.Equals(contract.schemaVersion, "1.0", StringComparison.Ordinal))
            throw new InvalidOperationException("Facade formal-build binding contract is null/unparseable or not schema 1.0.");
        if (!string.Equals(contract.scenePath, ScenePath, StringComparison.Ordinal))
            throw new InvalidOperationException("Facade formal-build binding scene identity drifted.");

        Requirements r = contract.requirements;
        if (r == null ||
            !r.mustRunAfterFacadeOptics ||
            !r.mustPrepareBeforeReflectionProbeBaseline ||
            !r.apertureConstructionRequired ||
            !r.aperturePhysicalUvRequired ||
            !r.occupancyVariationRequired ||
            !r.glassThicknessRequired ||
            !r.authoredDanchiRemainsAuthoritative ||
            !r.existingPassDriftMustFailClosed ||
            !r.actualRenderRequiredForVisualPoints)
            throw new InvalidOperationException("Facade formal-build binding requirements were weakened or are incomplete.");
        if (r.visualFidelityPointsAwarded != 0)
            throw new InvalidOperationException("Facade formal-build binding may never award automatic Visual Fidelity points.");

        RequireExactSet(contract.criticalDefectRisksReduced, CanonicalCriticalRisks, "criticalDefectRisksReduced");
        if (contract.requiredEvidence == null || contract.requiredEvidence.Length < 4 || contract.requiredEvidence.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Facade formal-build binding requiredEvidence is missing or incomplete.");
        if (contract.limitations == null || contract.limitations.Length < 3 || contract.limitations.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Facade formal-build binding limitations are missing or incomplete.");
        if (contract.forbiddenThemes == null ||
            !new HashSet<string>(contract.forbiddenThemes, StringComparer.OrdinalIgnoreCase).SetEquals(
                new[] { "earthquake", "disaster", "reconstruction" }))
            throw new InvalidOperationException("Facade formal-build binding forbidden-theme policy drifted.");

        // Keep every public delegated source contract independently fail-closed. Occupancy's own
        // contract validator is intentionally private; verify its required file exists here and let
        // ValidateOpenScene execute that validator once the generated scene state exists.
        QualityBlockFacadeApertureConstructionQA.ValidateContractConfigOnly();
        QualityBlockFacadeAperturePhysicalUvQA.ValidateContractConfigOnly();
        RequireAssetFile(OccupancyContractPath);
        QualityBlockFacadeGlassThicknessUpgrade.ValidateContract();
    }

    /// <summary>
    /// Called before a formal reflection baseline. Missing generated passes are built once, in physical
    /// dependency order. Existing passes are never auto-repaired here: validation must expose drift.
    /// </summary>
    public static void EnsurePreparedForFormalEvidence()
    {
        ValidateContractConfigOnly();
        EnsureBenchmarkSceneIsActive();

        if (IsAuthoredDanchiActive())
        {
            Debug.Log("Authored danchi replacement is active; generated facade formal-build binding is not applicable.");
            return;
        }

        if (FindSceneObject(OpticsRootName) == null)
            throw new InvalidOperationException(
                "DanchiFacadeOptics is missing before formal facade binding. The binding must run after facade optics, never synthesize around a missing optical baseline.");

        bool apertureWasMissing = FindSceneObject(ApertureRootName) == null;
        if (apertureWasMissing)
        {
            QualityBlockFacadeApertureConstructionQA.ApplyAndPersist();
            QualityBlockFacadeAperturePhysicalUvQA.ApplyAndPersist();
        }
        else
        {
            // Existing geometry is evidence state. Do not silently rewrite it when a contract/UV drift
            // should instead abort the formal packet.
            QualityBlockFacadeApertureConstructionQA.ValidateOpenScene();
            QualityBlockFacadeAperturePhysicalUvQA.ValidateOpenScene();
        }

        if (FindSceneObject(OccupancyRootName) == null)
            QualityBlockFacadeOccupancyVariationUpgrade.BuildAndApply();

        ValidatePreparedScene();
    }

    [MenuItem("NewTown/QA/Validate Prepared Facade Formal Build State")]
    public static void ValidatePreparedScene()
    {
        ValidateContractConfigOnly();
        EnsureBenchmarkSceneIsActive();

        if (IsAuthoredDanchiActive())
            return;

        foreach (string rootName in new[] { OpticsRootName, ApertureRootName, OccupancyRootName })
            if (FindSceneObject(rootName) == null)
                throw new InvalidOperationException("Formal facade build state is incomplete; missing root: " + rootName);

        QualityBlockFacadeApertureConstructionQA.ValidateOpenScene();
        QualityBlockFacadeAperturePhysicalUvQA.ValidateOpenScene();
        QualityBlockFacadeOccupancyVariationUpgrade.ValidateOpenScene();
        // The glass-thickness pass is bound to sceneSaving. Aperture/UV/occupancy persistence above
        // crosses that save boundary before reflection capture, so validate the resulting 65 edge shells.
        QualityBlockFacadeGlassThicknessUpgrade.ValidateOpenScene();
    }

    private static bool IsAuthoredDanchiActive()
    {
        QualityBlockArtSlot slot = Resources.FindObjectsOfTypeAll<QualityBlockArtSlot>()
            .FirstOrDefault(x => x != null && x.gameObject.scene.IsValid() && x.SlotId == "danchi.main");
        return slot != null && slot.IsUsingAuthoredArt;
    }

    private static void EnsureBenchmarkSceneIsActive()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() ||
            !string.Equals(EditorSceneManager.GetActiveScene().path, ScenePath, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Facade formal-build binding may run only in the already-prepared QualityBlock1990s scene. Active scene: " +
                EditorSceneManager.GetActiveScene().path);
    }

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x != null && x.scene.IsValid() &&
                                 string.Equals(x.scene.path, ScenePath, StringComparison.Ordinal) &&
                                 string.Equals(x.name, name, StringComparison.Ordinal));
    }

    private static void RequireAssetFile(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrWhiteSpace(projectRoot))
            throw new InvalidOperationException("Could not resolve Unity project root for facade formal-build binding.");
        string absolute = Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        if (!File.Exists(absolute))
            throw new FileNotFoundException("Required facade formal-build dependency file not found: " + assetPath);
    }

    private static T LoadJson<T>(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrWhiteSpace(projectRoot))
            throw new InvalidOperationException("Could not resolve Unity project root for facade formal-build binding.");
        string absolute = Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        if (!File.Exists(absolute))
            throw new FileNotFoundException("Required facade formal-build QA file not found: " + assetPath);
        T value = JsonUtility.FromJson<T>(File.ReadAllText(absolute));
        if (value == null)
            throw new InvalidOperationException("Could not parse facade formal-build QA JSON: " + assetPath);
        return value;
    }

    private static void RequireExactSet(string[] actual, string[] expected, string label)
    {
        if (actual == null || actual.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException(label + " is missing or contains a blank value.");
        if (actual.Distinct(StringComparer.Ordinal).Count() != actual.Length)
            throw new InvalidOperationException(label + " contains duplicates.");
        if (!new HashSet<string>(actual, StringComparer.Ordinal).SetEquals(expected))
            throw new InvalidOperationException(
                label + " must be exactly [" + string.Join(", ", expected) + "], got [" + string.Join(", ", actual) + "].");
    }

    [Serializable]
    private sealed class BindingContract
    {
        public string schemaVersion;
        public string purpose;
        public string scenePath;
        public Requirements requirements;
        public string[] criticalDefectRisksReduced;
        public string[] requiredEvidence;
        public string[] limitations;
        public string[] forbiddenThemes;
    }

    [Serializable]
    private sealed class Requirements
    {
        public bool mustRunAfterFacadeOptics;
        public bool mustPrepareBeforeReflectionProbeBaseline;
        public bool apertureConstructionRequired;
        public bool aperturePhysicalUvRequired;
        public bool occupancyVariationRequired;
        public bool glassThicknessRequired;
        public bool authoredDanchiRemainsAuthoritative;
        public bool existingPassDriftMustFailClosed;
        public bool actualRenderRequiredForVisualPoints;
        public int visualFidelityPointsAwarded;
    }
}
