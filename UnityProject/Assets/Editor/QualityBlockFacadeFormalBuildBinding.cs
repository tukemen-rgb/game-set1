using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
/// reflection fingerprint. The armed epoch is persisted in UnityEditor.SessionState. Generated fallback
/// mode binds the scene GUID plus stable GlobalObjectIds for optics/aperture/occupancy roots. Authored mode
/// binds the exact danchi art slot/root plus a deterministic SHA-256 over persisted transform/visibility,
/// renderer mesh/material dependencies and LOD state. Script/assembly reload therefore cannot silently
/// reset the guard and accept replacement or mutated authored geometry as a fresh baseline. After arming,
/// every repeated reflection poll is report-free and fail-closed. This layer awards zero Visual Fidelity
/// points; actual native 4K rendered evidence remains mandatory.
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
    private const string DanchiSlotId = "danchi.main";
    private const string ContractSchema = "1.2";
    private const string AuthoredFingerprintSchema = "facade-authored-evidence-v1";
    private const int ExpectedFacadeCellParts = 120;
    private const int ExpectedSealParts = 120;
    private const int ExpectedApartmentGlassPanes = 60;

    private const string SessionPrefix = "QualityBlock.FacadeFormalBuildBinding.";
    private const string EpochArmedKey = SessionPrefix + "EpochArmed";
    private const string AuthoredEpochKey = SessionPrefix + "AuthoredEpoch";
    private const string OpticsGlobalObjectIdKey = SessionPrefix + "OpticsGlobalObjectId";
    private const string ApertureGlobalObjectIdKey = SessionPrefix + "ApertureGlobalObjectId";
    private const string OccupancyGlobalObjectIdKey = SessionPrefix + "OccupancyGlobalObjectId";
    private const string AuthoredFingerprintKey = SessionPrefix + "AuthoredFingerprint";
    private const string SceneGuidKey = SessionPrefix + "SceneGuid";
    private const string EpochTokenKey = SessionPrefix + "EpochToken";

    private static bool EpochArmed => SessionState.GetBool(EpochArmedKey, false);
    private static bool AuthoredEpoch => SessionState.GetBool(AuthoredEpochKey, false);
    private static string ArmedOpticsGlobalObjectId => SessionState.GetString(OpticsGlobalObjectIdKey, string.Empty);
    private static string ArmedApertureGlobalObjectId => SessionState.GetString(ApertureGlobalObjectIdKey, string.Empty);
    private static string ArmedOccupancyGlobalObjectId => SessionState.GetString(OccupancyGlobalObjectIdKey, string.Empty);
    private static string ArmedAuthoredFingerprint => SessionState.GetString(AuthoredFingerprintKey, string.Empty);
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
            !r.freezeAuthoredRepresentationFingerprintAfterFirstReflectionBaseline ||
            !r.authoredFingerprintMustCoverStableIdentityTransformRendererMeshMaterialAndLod ||
            !r.authoredFingerprintMustPersistAcrossAssemblyReloads ||
            !r.legacyAuthoredEpochWithoutFingerprintMustFailClosed ||
            !r.actualRenderRequiredForVisualPoints)
            throw new InvalidOperationException("Facade formal-build binding requirements were weakened or are incomplete.");
        if (r.visualFidelityPointsAwarded != 0)
            throw new InvalidOperationException("Facade formal-build binding may never award automatic Visual Fidelity points.");

        RequireExactSet(contract.criticalDefectRisksReduced, CanonicalCriticalRisks, "criticalDefectRisksReduced");
        if (contract.requiredEvidence == null || contract.requiredEvidence.Length < 4 || contract.requiredEvidence.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Facade formal-build binding requiredEvidence is missing or incomplete.");
        if (contract.limitations == null || contract.limitations.Length < 6 || contract.limitations.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Facade formal-build binding limitations are missing or incomplete.");
        if (contract.forbiddenThemes == null ||
            !new HashSet<string>(contract.forbiddenThemes, StringComparer.OrdinalIgnoreCase).SetEquals(
                new[] { "earthquake", "disaster", "reconstruction" }))
            throw new InvalidOperationException("Facade formal-build binding forbidden-theme policy drifted.");

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

        GameObject existingAperture = FindSceneObject(ApertureRootName);
        if (existingAperture == null)
        {
            QualityBlockFacadeApertureConstructionQA.ApplyAndPersist();
            QualityBlockFacadeAperturePhysicalUvQA.ApplyAndPersist();
        }
        else
        {
            QualityBlockFacadeApertureConstructionQA.ValidateOpenScene();
            QualityBlockFacadeAperturePhysicalUvQA.ValidateOpenScene();
        }

        if (FindSceneObject(OccupancyRootName) == null)
            QualityBlockFacadeOccupancyVariationUpgrade.BuildAndApply();

        ValidatePreparedScene();
        ArmFallbackEpoch(optics);
    }

    [MenuItem("NewTown/QA/Validate Prepared Facade Formal Build State")]
    public static void ValidatePreparedScene()
    {
        ValidateContractConfigOnly();
        EnsureBenchmarkSceneIsActive();

        if (IsAuthoredDanchiActive())
        {
            ValidateAuthoredRepresentationPreArm();
            return;
        }

        RequirePreparedRoots();
        QualityBlockFacadeApertureConstructionQA.ValidateOpenScene();
        QualityBlockFacadeAperturePhysicalUvQA.ValidateOpenScene();
        QualityBlockFacadeOccupancyVariationUpgrade.ValidateOpenScene();
        QualityBlockFacadeGlassThicknessUpgrade.ValidateOpenScene();
    }

    /// <summary>
    /// Strictly read-only post-arm validation. Stable object identity and authored/fallback mode are
    /// checked before delegated source validation so a domain reload cannot reinterpret replacement
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

            if (string.IsNullOrWhiteSpace(ArmedAuthoredFingerprint))
                throw new InvalidOperationException(
                    "Authored facade epoch predates schema 1.2 or lost its persisted representation fingerprint. " +
                    "Failing closed: restart the Editor and arm a fresh authored epoch before formal evidence.");

            string currentFingerprint = BuildAuthoredRepresentationFingerprint();
            if (!string.Equals(currentFingerprint, ArmedAuthoredFingerprint, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Authored danchi representation changed after the facade formal-build reflection epoch was armed. " +
                    "Slot/root identity, transform/visibility, renderer mesh/material dependencies or LOD state drifted. " +
                    "Do not repair or re-arm in flight; restart the Editor and capture a fresh epoch.");

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
        SessionState.SetString(AuthoredFingerprintKey, string.Empty);
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
        string authoredFingerprint = BuildAuthoredRepresentationFingerprint();

        SessionState.SetBool(AuthoredEpochKey, true);
        SessionState.SetString(OpticsGlobalObjectIdKey, string.Empty);
        SessionState.SetString(ApertureGlobalObjectIdKey, string.Empty);
        SessionState.SetString(OccupancyGlobalObjectIdKey, string.Empty);
        SessionState.SetString(AuthoredFingerprintKey, authoredFingerprint);
        SessionState.SetString(SceneGuidKey, sceneGuid);
        SessionState.SetString(EpochTokenKey, Guid.NewGuid().ToString("N"));
        SessionState.SetBool(EpochArmedKey, true);

        Debug.Log(
            "Facade formal-build authored epoch armed fail-closed for this Editor session: " +
            $"sceneGuid={sceneGuid}, authoredFingerprint={authoredFingerprint}, epochToken={EpochToken}. " +
            "The exact authored slot/root, render dependencies, transforms, visibility and LOD state are frozen until a clean Editor restart. " +
            "Visual Fidelity remains UNSCORED pending actual native 4K evidence.");
    }

    private static void ValidateAuthoredRepresentationPreArm()
    {
        BuildAuthoredRepresentationFingerprint();
    }

    /// <summary>
    /// Builds a deterministic, read-only fingerprint of the scoreable authored danchi representation.
    /// This deliberately excludes volatile runtime state and includes only persisted evidence-affecting
    /// identity/state. Project asset dependency hashes cover in-place edits to imported meshes, materials,
    /// textures and shaders even when the scene object GlobalObjectId itself is unchanged.
    /// </summary>
    private static string BuildAuthoredRepresentationFingerprint()
    {
        QualityBlockArtSlot slot = RequireAuthoredDanchiSlot();
        GameObject authored = slot.AuthoredInstance;
        GameObject fallback = slot.FallbackRoot;

        if (authored == null || !authored.activeSelf || !authored.activeInHierarchy)
            throw new InvalidOperationException(
                "Authored danchi formal evidence requires an active persisted authored instance.");
        if (!slot.enabled || !slot.gameObject.activeInHierarchy)
            throw new InvalidOperationException(
                "Authored danchi art slot must remain enabled and active for formal evidence.");
        if (fallback == null)
            throw new InvalidOperationException(
                "Authored danchi art slot lost its generated fallback reference; representation provenance is incomplete.");
        if (fallback.activeSelf)
            throw new InvalidOperationException(
                "Authored danchi and generated fallback are simultaneously active. Formal evidence must contain exactly one facade representation.");

        RequireSceneMembership(slot.gameObject, "danchi art slot");
        RequireSceneMembership(authored, "authored danchi root");
        RequireSceneMembership(fallback, "danchi fallback root");

        string slotId = RequireStableGlobalObjectId(slot.gameObject, "danchi art slot");
        string authoredId = RequireStableGlobalObjectId(authored, "authored danchi root");
        string fallbackId = RequireStableGlobalObjectId(fallback, "danchi fallback root");

        string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(authored);
        if (string.IsNullOrWhiteSpace(prefabPath))
            throw new InvalidOperationException(
                "Authored danchi root is not backed by a persisted prefab/model asset. Unsaved or detached replacement art is not valid formal evidence.");
        string prefabGuid = AssetDatabase.AssetPathToGUID(prefabPath);
        if (string.IsNullOrWhiteSpace(prefabGuid))
            throw new InvalidOperationException(
                "Could not resolve authored danchi prefab/model GUID: " + prefabPath);
        string prefabDependencyHash = AssetDatabase.GetAssetDependencyHash(prefabPath).ToString();

        var lines = new List<string>
        {
            "schema=" + AuthoredFingerprintSchema,
            "sceneGuid=" + RequireSceneGuid(),
            "slotId=" + slot.SlotId,
            "expectedAsset=" + slot.ExpectedAssetName,
            "slotGlobalId=" + slotId,
            "authoredGlobalId=" + authoredId,
            "fallbackGlobalId=" + fallbackId,
            "fallbackActiveSelf=" + Bool01(fallback.activeSelf),
            "slotWorldMatrix=" + FormatMatrix4x4(slot.transform.localToWorldMatrix),
            "prefabPath=" + prefabPath,
            "prefabGuid=" + prefabGuid,
            "prefabDependencyHash=" + prefabDependencyHash
        };

        Transform[] transforms = authored.GetComponentsInChildren<Transform>(true);
        if (transforms.Length == 0)
            throw new InvalidOperationException("Authored danchi contains no transform hierarchy.");

        foreach (Transform t in transforms.OrderBy(x => RequireStableGlobalObjectId(x, "authored transform"), StringComparer.Ordinal))
        {
            string id = RequireStableGlobalObjectId(t, "authored transform");
            lines.Add(
                "transform|" + id +
                "|path=" + GetRelativeTransformPath(authored.transform, t) +
                "|activeSelf=" + Bool01(t.gameObject.activeSelf) +
                "|localPosition=" + FormatVector3(t.localPosition) +
                "|localRotation=" + FormatQuaternion(t.localRotation) +
                "|localScale=" + FormatVector3(t.localScale));
        }

        Renderer[] renderers = authored.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            throw new InvalidOperationException(
                "Authored danchi contains no renderers; it cannot be used as scoreable facade evidence.");
        if (!renderers.Any(x => x != null && x.enabled && x.gameObject.activeInHierarchy))
            throw new InvalidOperationException(
                "Authored danchi has no enabled renderer active in hierarchy.");

        foreach (Renderer renderer in renderers.OrderBy(x => RequireStableGlobalObjectId(x, "authored renderer"), StringComparer.Ordinal))
        {
            string rendererId = RequireStableGlobalObjectId(renderer, "authored renderer");
            lines.Add(
                "renderer|" + rendererId +
                "|type=" + renderer.GetType().FullName +
                "|enabled=" + Bool01(renderer.enabled) +
                "|shadowCasting=" + renderer.shadowCastingMode +
                "|receiveShadows=" + Bool01(renderer.receiveShadows) +
                "|lightProbeUsage=" + renderer.lightProbeUsage +
                "|reflectionProbeUsage=" + renderer.reflectionProbeUsage);

            Mesh mesh = null;
            var skinned = renderer as SkinnedMeshRenderer;
            if (skinned != null)
            {
                mesh = skinned.sharedMesh;
            }
            else if (renderer is MeshRenderer)
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                    throw new InvalidOperationException(
                        "Authored MeshRenderer has no persisted MeshFilter/sharedMesh: " + GetRelativeTransformPath(authored.transform, renderer.transform));
                mesh = filter.sharedMesh;
            }

            if (mesh != null)
                lines.Add("mesh|" + rendererId + "|" + RequirePersistentAssetDependencyIdentity(mesh, "authored renderer mesh"));

            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
                throw new InvalidOperationException(
                    "Authored renderer has no shared material: " + GetRelativeTransformPath(authored.transform, renderer.transform));
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                if (material == null)
                    throw new InvalidOperationException(
                        "Authored renderer contains a null material slot: " + GetRelativeTransformPath(authored.transform, renderer.transform));
                lines.Add(
                    "material|" + rendererId +
                    "|slot=" + materialIndex.ToString(CultureInfo.InvariantCulture) +
                    "|" + RequirePersistentAssetDependencyIdentity(material, "authored renderer material"));
            }
        }

        LODGroup[] lodGroups = slot.GetComponentsInChildren<LODGroup>(true)
            .Where(x => x != null &&
                        (x.transform == slot.transform || x.transform == authored.transform || x.transform.IsChildOf(authored.transform)))
            .ToArray();
        foreach (LODGroup group in lodGroups.OrderBy(x => RequireStableGlobalObjectId(x, "authored LODGroup"), StringComparer.Ordinal))
        {
            string groupId = RequireStableGlobalObjectId(group, "authored LODGroup");
            lines.Add(
                "lodGroup|" + groupId +
                "|enabled=" + Bool01(group.enabled) +
                "|size=" + FormatFloat(group.size) +
                "|localReferencePoint=" + FormatVector3(group.localReferencePoint) +
                "|fadeMode=" + group.fadeMode +
                "|animateCrossFading=" + Bool01(group.animateCrossFading));

            LOD[] lods = group.GetLODs();
            for (int lodIndex = 0; lodIndex < lods.Length; lodIndex++)
            {
                LOD lod = lods[lodIndex];
                string rendererIds = string.Join(
                    ",",
                    (lod.renderers ?? Array.Empty<Renderer>())
                        .Select(x => x == null ? "NULL" : RequireStableGlobalObjectId(x, "LOD renderer")));
                lines.Add(
                    "lod|" + groupId +
                    "|index=" + lodIndex.ToString(CultureInfo.InvariantCulture) +
                    "|height=" + FormatFloat(lod.screenRelativeTransitionHeight) +
                    "|fadeWidth=" + FormatFloat(lod.fadeTransitionWidth) +
                    "|renderers=" + rendererIds);
            }
        }

        lines.Sort(StringComparer.Ordinal);
        string canonical = string.Join("\n", lines);
        using (SHA256 sha = SHA256.Create())
        {
            byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
            var hex = new StringBuilder(digest.Length * 2);
            foreach (byte value in digest)
                hex.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            return hex.ToString();
        }
    }

    private static QualityBlockArtSlot RequireAuthoredDanchiSlot()
    {
        QualityBlockArtSlot[] slots = GetDanchiSlots();
        if (slots.Length != 1)
            throw new InvalidOperationException(
                "Formal facade evidence requires exactly one persisted art slot with SlotId '" + DanchiSlotId + "', got " + slots.Length + ".");

        QualityBlockArtSlot slot = slots[0];
        if (!slot.IsUsingAuthoredArt)
            throw new InvalidOperationException(
                "The danchi art slot is not using authored art while an authored formal representation was required.");
        return slot;
    }

    private static QualityBlockArtSlot[] GetDanchiSlots()
    {
        return Resources.FindObjectsOfTypeAll<QualityBlockArtSlot>()
            .Where(x => x != null && x.gameObject.scene.IsValid() &&
                        string.Equals(x.gameObject.scene.path, ScenePath, StringComparison.Ordinal) &&
                        string.Equals(x.SlotId, DanchiSlotId, StringComparison.Ordinal))
            .ToArray();
    }

    private static string RequirePersistentAssetDependencyIdentity(UnityEngine.Object asset, string label)
    {
        if (asset == null)
            throw new ArgumentNullException(nameof(asset));

        string path = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException(
                label + " is not a persisted project asset. Runtime/unsaved render dependencies are not valid formal evidence.");

        string guid;
        long localId;
        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out guid, out localId) ||
            string.IsNullOrWhiteSpace(guid))
            throw new InvalidOperationException(
                "Could not resolve persistent GUID/local file identifier for " + label + ": " + asset.name);

        string dependencyHash = AssetDatabase.GetAssetDependencyHash(path).ToString();
        return
            "path=" + path +
            "|guid=" + guid +
            "|localId=" + localId.ToString(CultureInfo.InvariantCulture) +
            "|dependencyHash=" + dependencyHash;
    }

    private static void RequireSceneMembership(GameObject go, string label)
    {
        if (go == null || !go.scene.IsValid() || !string.Equals(go.scene.path, ScenePath, StringComparison.Ordinal))
            throw new InvalidOperationException(
                label + " is not persisted in the benchmark scene. Cross-scene or unsaved authored evidence is rejected.");
    }

    private static string GetRelativeTransformPath(Transform root, Transform value)
    {
        if (root == null || value == null)
            return "<null>";
        if (value == root)
            return ".";

        var segments = new List<string>();
        Transform cursor = value;
        while (cursor != null && cursor != root)
        {
            segments.Add(cursor.name + "#" + cursor.GetSiblingIndex().ToString(CultureInfo.InvariantCulture));
            cursor = cursor.parent;
        }
        if (cursor != root)
            throw new InvalidOperationException("Authored renderer/transform escaped the authored danchi root hierarchy.");
        segments.Reverse();
        return string.Join("/", segments);
    }

    private static string Bool01(bool value)
    {
        return value ? "1" : "0";
    }

    private static string FormatFloat(float value)
    {
        return value.ToString("R", CultureInfo.InvariantCulture);
    }

    private static string FormatVector3(Vector3 value)
    {
        return FormatFloat(value.x) + "," + FormatFloat(value.y) + "," + FormatFloat(value.z);
    }

    private static string FormatQuaternion(Quaternion value)
    {
        return FormatFloat(value.x) + "," + FormatFloat(value.y) + "," + FormatFloat(value.z) + "," + FormatFloat(value.w);
    }

    private static string FormatMatrix4x4(Matrix4x4 value)
    {
        var values = new string[16];
        for (int row = 0; row < 4; row++)
        {
            for (int column = 0; column < 4; column++)
                values[row * 4 + column] = FormatFloat(value[row, column]);
        }
        return string.Join(",", values);
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

    private static string RequireStableGlobalObjectId(UnityEngine.Object target, string label)
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        GlobalObjectId id = GlobalObjectId.GetGlobalObjectIdSlow(target);
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
        QualityBlockArtSlot[] slots = GetDanchiSlots();
        if (slots.Length > 1)
            throw new InvalidOperationException(
                "Multiple danchi.main art slots exist in the benchmark scene; formal evidence cannot choose one nondeterministically.");
        return slots.Length == 1 && slots[0].IsUsingAuthoredArt;
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
        public bool freezeAuthoredRepresentationFingerprintAfterFirstReflectionBaseline;
        public bool authoredFingerprintMustCoverStableIdentityTransformRendererMeshMaterialAndLod;
        public bool authoredFingerprintMustPersistAcrossAssemblyReloads;
        public bool legacyAuthoredEpochWithoutFingerprintMustFailClosed;
        public bool actualRenderRequiredForVisualPoints;
        public int visualFidelityPointsAwarded;
    }
}
