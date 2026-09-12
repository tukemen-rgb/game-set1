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
/// A new facade preparation epoch may create genuinely missing generated passes before the first formal
/// reflection fingerprint. The armed epoch is persisted in UnityEditor.SessionState and binds the scene
/// GUID plus stable GlobalObjectIds for optics/aperture/occupancy roots. Script/assembly reload therefore
/// cannot silently reset the guard and accept replacement roots as a fresh baseline. After arming, every
/// repeated reflection poll is report-free and fail-closed: deletion/replacement/drift or authored/fallback
/// mode changes abort instead of mutating evidence while a probe is in flight. Authored danchi art remains
/// authoritative. This integration layer awards zero Visual Fidelity points.
/// </summary>
public static class QualityBlockFacadeFormalBuildBinding
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/facade_formal_build_binding_contract.json";
    private const string OccupancyContractPath = "Assets/QA/facade_window_dressing_contract.json";
    private const string OpticsRootName = "DanchiFacadeOptics";
    private const string ApertureRootName = "DanchiFacadeApertureShell";
    private const string OccupancyRootName = "FacadeOccupancyVariation";
    private const string MainBlockName = "MainBlock";
    private const string ContractSchema = "1.1";
    private const int ExpectedFacadeCellParts = 120;
    private const int ExpectedSealParts = 120;
    private const int ExpectedApartmentGlassPanes = 60;

    private const string SessionPrefix = "QualityBlock.FacadeFormalBuildBinding.";
    private const string EpochArmedKey = SessionPrefix + "EpochArmed";
    private const string AuthoredEpochKey = SessionPrefix + "AuthoredEpoch";
    private const string OpticsGlobalObjectIdKey = SessionPrefix + "OpticsGlobalObjectId";
    private const string ApertureGlobalObjectIdKey = SessionPrefix + "ApertureGlobalObjectId";
    private const string OccupancyGlobalObjectIdKey = SessionPrefix + "OccupancyGlobalObjectId";
    private const string SceneGuidKey = SessionPrefix + "SceneGuid";
    private const string EpochTokenKey = SessionPrefix + "EpochToken";

    private static bool EpochArmed => SessionState.GetBool(EpochArmedKey, false);
    private static bool AuthoredEpoch => SessionState.GetBool(AuthoredEpochKey, false);
    private static string ArmedOpticsGlobalObjectId => SessionState.GetString(OpticsGlobalObjectIdKey, string.Empty);
    private static string ArmedApertureGlobalObjectId => SessionState.GetString(ApertureGlobalObjectIdKey, string.Empty);
    private static string ArmedOccupancyGlobalObjectId => SessionState.GetString(OccupancyGlobalObjectIdKey, string.Empty);
    private static string ArmedSceneGuid => SessionState.GetString(SceneGuidKey, string.Empty);
    private static string EpochToken => SessionState.GetString(EpochTokenKey, string.Empty);

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
        if (contract == null || !string.Equals(contract.schemaVersion, ContractSchema, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Facade formal-build binding contract is null/unparseable or not schema " + ContractSchema + ".");
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
            !r.reflectionPollValidationMustBeReportFree ||
            !r.inFlightRootReplacementMustAbort ||
            !r.persistEpochAcrossAssemblyReloadsWithSessionState ||
            !r.freezeStableGlobalObjectIdsAfterFirstReflectionBaseline ||
            !r.validateSceneGuidAcrossEpoch ||
            !r.authoredFallbackModeMustRemainStableAfterEpochArm ||
            !r.unsavedReplacementMustFailBeforeEvidence ||
            !r.actualRenderRequiredForVisualPoints)
            throw new InvalidOperationException("Facade formal-build binding requirements were weakened or are incomplete.");
        if (r.visualFidelityPointsAwarded != 0)
            throw new InvalidOperationException("Facade formal-build binding may never award automatic Visual Fidelity points.");

        RequireExactSet(contract.criticalDefectRisksReduced, CanonicalCriticalRisks, "criticalDefectRisksReduced");
        if (contract.requiredEvidence == null || contract.requiredEvidence.Length < 4 || contract.requiredEvidence.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Facade formal-build binding requiredEvidence is missing or incomplete.");
        if (contract.limitations == null || contract.limitations.Length < 5 || contract.limitations.Any(string.IsNullOrWhiteSpace))
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
    /// Called from the formal reflection fingerprint. The first call in a clean Editor session may build
    /// missing fallback facade passes. Once armed, the SessionState epoch survives script/assembly reloads
    /// and every later call is strictly report-free and read-only.
    /// </summary>
    public static void EnsurePreparedForFormalEvidence()
    {
        ValidateContractConfigOnly();
        EnsureBenchmarkSceneIsActive();

        if (EpochArmed)
        {
            ValidateArmedEpochReportFree();
            return;
        }

        bool authored = IsAuthoredDanchiActive();
        if (authored)
        {
            ArmAuthoredEpoch();
            return;
        }

        GameObject optics = FindSceneObject(OpticsRootName);
        if (optics == null)
            throw new InvalidOperationException(
                "DanchiFacadeOptics is missing before formal facade binding. The binding must run after facade optics, never synthesize around a missing optical baseline.");

        // Building is allowed only before the formal reflection epoch is armed.
        GameObject existingAperture = FindSceneObject(ApertureRootName);
        if (existingAperture == null)
        {
            QualityBlockFacadeApertureConstructionQA.ApplyAndPersist();
            QualityBlockFacadeAperturePhysicalUvQA.ApplyAndPersist();
        }
        else
        {
            // Full aperture validation writes its source-side runtime report; this is permitted exactly
            // here, before RenderProbe(), but never on subsequent in-flight polls.
            QualityBlockFacadeApertureConstructionQA.ValidateOpenScene();
            QualityBlockFacadeAperturePhysicalUvQA.ValidateOpenScene();
        }

        if (FindSceneObject(OccupancyRootName) == null)
            QualityBlockFacadeOccupancyVariationUpgrade.BuildAndApply();

        // First-epoch full validation is still pre-probe. It proves all dependent passes (including the
        // sceneSaving-bound glass thickness) exist before stable identities are frozen.
        ValidatePreparedScene();
        ArmFallbackEpoch(optics);
    }

    [MenuItem("NewTown/QA/Validate Prepared Facade Formal Build State")]
    public static void ValidatePreparedScene()
    {
        ValidateContractConfigOnly();
        EnsureBenchmarkSceneIsActive();

        if (IsAuthoredDanchiActive())
            return;

        RequirePreparedRoots();
        QualityBlockFacadeApertureConstructionQA.ValidateOpenScene();
        QualityBlockFacadeAperturePhysicalUvQA.ValidateOpenScene();
        QualityBlockFacadeOccupancyVariationUpgrade.ValidateOpenScene();
        // The glass-thickness pass is bound to sceneSaving. Aperture/UV/occupancy persistence above
        // crosses that save boundary before reflection capture, so validate the resulting edge shells.
        QualityBlockFacadeGlassThicknessUpgrade.ValidateOpenScene();
    }

    /// <summary>
    /// Strictly read-only post-arm validation. Stable object identity and the authored/fallback mode are
    /// checked before any delegated source validator so a domain reload cannot reinterpret replacement
    /// geometry as a fresh formal epoch.
    /// </summary>
    private static void ValidateArmedEpochReportFree()
    {
        EnsureBenchmarkSceneIsActive();
        if (!EpochArmed)
            throw new InvalidOperationException(
                "Facade formal-build epoch has not been armed. Formal preparation must run before reflection evidence.");

        string currentSceneGuid = RequireSceneGuid();
        if (string.IsNullOrWhiteSpace(ArmedSceneGuid) ||
            !string.Equals(currentSceneGuid, ArmedSceneGuid, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Benchmark scene GUID changed after the facade formal-build epoch was armed. Recapture from a clean Editor session.");
        if (string.IsNullOrWhiteSpace(EpochToken))
            throw new InvalidOperationException(
                "Facade formal-build epoch token is missing. Session-state evidence is incomplete; recapture from a clean Editor session.");

        bool authored = IsAuthoredDanchiActive();
        if (authored != AuthoredEpoch)
            throw new InvalidOperationException(
                "Danchi authored/fallback mode changed after the facade formal-build reflection epoch was armed.");

        GameObject optics = FindSceneObject(OpticsRootName);
        GameObject aperture = FindSceneObject(ApertureRootName);
        GameObject occupancy = FindSceneObject(OccupancyRootName);

        if (AuthoredEpoch)
        {
            if (aperture != null || occupancy != null)
                throw new InvalidOperationException(
                    "Generated aperture/occupancy roots appeared after an authored-danchi facade epoch was armed. Do not mix generated fallback construction into authored formal evidence.");
            return;
        }

        if (optics == null || aperture == null || occupancy == null)
            throw new InvalidOperationException(
                "Facade formal evidence state lost a required root after the reflection epoch was armed. In-flight evidence may not auto-repair deleted facade construction.");

        RequireSameStableIdentity(optics, ArmedOpticsGlobalObjectId, "facade optics");
        RequireSameStableIdentity(aperture, ArmedApertureGlobalObjectId, "facade aperture shell");
        RequireSameStableIdentity(occupancy, ArmedOccupancyGlobalObjectId, "facade occupancy variation");

        ValidatePreparedSceneReportFree();
    }

    private static void ValidatePreparedSceneReportFree()
    {
        RequirePreparedRoots();

        GameObject mainBlock = FindSceneObject(MainBlockName);
        Renderer mainRenderer = mainBlock != null ? mainBlock.GetComponent<Renderer>() : null;
        if (mainRenderer == null || mainRenderer.enabled)
            throw new InvalidOperationException(
                "MainBlock opaque fallback renderer must remain disabled after the formal facade baseline is armed.");

        GameObject aperture = FindSceneObject(ApertureRootName);
        int cellParts = aperture.GetComponentsInChildren<Renderer>(true)
            .Count(x => x != null && x.gameObject.name.StartsWith("FA_FacadeCell_", StringComparison.Ordinal));
        int sealParts = aperture.GetComponentsInChildren<Renderer>(true)
            .Count(x => x != null && x.gameObject.name.StartsWith("FA_WindowSeal_", StringComparison.Ordinal));
        int apartmentPanes = Resources.FindObjectsOfTypeAll<GameObject>()
            .Count(x => x != null && x.scene.IsValid() &&
                        string.Equals(x.scene.path, ScenePath, StringComparison.Ordinal) &&
                        x.name.StartsWith("FO_Glass_", StringComparison.Ordinal));
        if (cellParts != ExpectedFacadeCellParts || sealParts != ExpectedSealParts || apartmentPanes != ExpectedApartmentGlassPanes)
            throw new InvalidOperationException(
                $"Report-free facade construction fingerprint drifted: cells={cellParts}/{ExpectedFacadeCellParts}, seals={sealParts}/{ExpectedSealParts}, apartmentPanes={apartmentPanes}/{ExpectedApartmentGlassPanes}.");

        // These validators do not emit mutable runtime reports. They can therefore be reused on every
        // Editor poll to freeze physical UVs, deterministic occupancy layout and glass-edge construction.
        QualityBlockFacadeAperturePhysicalUvQA.ValidateOpenScene();
        QualityBlockFacadeOccupancyVariationUpgrade.ValidateOpenScene();
        QualityBlockFacadeGlassThicknessUpgrade.ValidateOpenScene();
    }

    private static void ArmFallbackEpoch(GameObject optics)
    {
        GameObject aperture = FindSceneObject(ApertureRootName);
        GameObject occupancy = FindSceneObject(OccupancyRootName);
        if (optics == null || aperture == null || occupancy == null)
            throw new InvalidOperationException("Cannot arm facade formal baseline with incomplete prepared roots.");

        string sceneGuid = RequireSceneGuid();
        string opticsId = RequireStableGlobalObjectId(optics, "facade optics");
        string apertureId = RequireStableGlobalObjectId(aperture, "facade aperture shell");
        string occupancyId = RequireStableGlobalObjectId(occupancy, "facade occupancy variation");

        SessionState.SetBool(AuthoredEpochKey, false);
        SessionState.SetString(OpticsGlobalObjectIdKey, opticsId);
        SessionState.SetString(ApertureGlobalObjectIdKey, apertureId);
        SessionState.SetString(OccupancyGlobalObjectIdKey, occupancyId);
        SessionState.SetString(SceneGuidKey, sceneGuid);
        SessionState.SetString(EpochTokenKey, Guid.NewGuid().ToString("N"));
        SessionState.SetBool(EpochArmedKey, true);

        Debug.Log(
            "Facade formal-build fallback epoch armed fail-closed for this Editor session: " +
            $"sceneGuid={sceneGuid}, optics={opticsId}, aperture={apertureId}, occupancy={occupancyId}, epochToken={EpochToken}. " +
            "Visual Fidelity remains UNSCORED pending actual native 4K evidence.");
    }

    private static void ArmAuthoredEpoch()
    {
        GameObject aperture = FindSceneObject(ApertureRootName);
        GameObject occupancy = FindSceneObject(OccupancyRootName);
        if (aperture != null || occupancy != null)
            throw new InvalidOperationException(
                "Generated facade aperture/occupancy roots must be absent before an authored-danchi formal epoch can be armed.");

        string sceneGuid = RequireSceneGuid();
        SessionState.SetBool(AuthoredEpochKey, true);
        SessionState.SetString(OpticsGlobalObjectIdKey, string.Empty);
        SessionState.SetString(ApertureGlobalObjectIdKey, string.Empty);
        SessionState.SetString(OccupancyGlobalObjectIdKey, string.Empty);
        SessionState.SetString(SceneGuidKey, sceneGuid);
        SessionState.SetString(EpochTokenKey, Guid.NewGuid().ToString("N"));
        SessionState.SetBool(EpochArmedKey, true);

        Debug.Log(
            "Facade formal-build authored epoch armed fail-closed for this Editor session: " +
            $"sceneGuid={sceneGuid}, epochToken={EpochToken}. Generated fallback roots may not appear until a clean Editor restart. " +
            "Visual Fidelity remains UNSCORED pending actual native 4K evidence.");
    }

    private static void RequireSameStableIdentity(GameObject current, string armedId, string label)
    {
        string currentId = RequireStableGlobalObjectId(current, label);
        if (string.IsNullOrWhiteSpace(armedId) || !string.Equals(currentId, armedId, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Stable GlobalObjectId changed for " + label + " after the facade formal-build epoch was armed. " +
                "A replacement object may not inherit an existing reflection baseline; restart the Editor and capture a fresh epoch.");
    }

    private static string RequireSceneGuid()
    {
        string guid = AssetDatabase.AssetPathToGUID(ScenePath);
        if (string.IsNullOrWhiteSpace(guid))
            throw new InvalidOperationException(
                "Could not resolve the persisted benchmark scene GUID for facade formal-build binding.");
        return guid;
    }

    private static string RequireStableGlobalObjectId(GameObject root, string label)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));
        GlobalObjectId id = GlobalObjectId.GetGlobalObjectIdSlow(root);
        string value = id.ToString();
        if (string.IsNullOrWhiteSpace(value) || value.StartsWith("GlobalObjectId_V1-0-", StringComparison.Ordinal))
            throw new InvalidOperationException(
                label + " has no stable persisted GlobalObjectId. Save the benchmark scene before formal reflection evidence; unsaved replacement geometry is not scoreable.");
        return value;
    }

    private static void RequirePreparedRoots()
    {
        foreach (string rootName in new[] { OpticsRootName, ApertureRootName, OccupancyRootName })
            if (FindSceneObject(rootName) == null)
                throw new InvalidOperationException("Formal facade build state is incomplete; missing root: " + rootName);
    }

    private static bool IsAuthoredDanchiActive()
    {
        QualityBlockArtSlot slot = Resources.FindObjectsOfTypeAll<QualityBlockArtSlot>()
            .FirstOrDefault(x => x != null && x.gameObject.scene.IsValid() &&
                                 string.Equals(x.gameObject.scene.path, ScenePath, StringComparison.Ordinal) &&
                                 x.SlotId == "danchi.main");
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
        public bool reflectionPollValidationMustBeReportFree;
        public bool inFlightRootReplacementMustAbort;
        public bool persistEpochAcrossAssemblyReloadsWithSessionState;
        public bool freezeStableGlobalObjectIdsAfterFirstReflectionBaseline;
        public bool validateSceneGuidAcrossEpoch;
        public bool authoredFallbackModeMustRemainStableAfterEpochArm;
        public bool unsavedReplacementMustFailBeforeEvidence;
        public bool actualRenderRequiredForVisualPoints;
        public int visualFidelityPointsAwarded;
    }
}
