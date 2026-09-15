using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Fail-closed QA for the benchmark's midsummer key-light state. It binds the deterministic solar
/// datum, the one-and-only directional key, stable shadow-map settings and the runtime pre-cull guard.
/// Category and automatic-FAIL IDs are cross-checked against visual_fidelity_gate.json so descriptive
/// aliases cannot silently bypass the canonical 100-point gate. It never awards visual points.
/// </summary>
public static class QualityBlockSolarShadowCaptureCoherenceQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/solar_shadow_capture_coherence_contract.json";
    private const string VisualGatePath = "Assets/QA/visual_fidelity_gate.json";
    private const string LookdevPath = "Assets/QA/Lookdev/solar_shadow_capture_coherence.svg";

    private static readonly string[] RequiredCategoryIds =
    {
        "lighting_shadows_reflections",
        "cinematic_image",
        "temporal_lod_aliasing"
    };

    private static readonly string[] RequiredCriticalIds =
    {
        "sun_shadow_inconsistency",
        "severe_aliasing_or_shimmer",
        "visible_lod_pop",
        "major_light_leak",
        "unverified_render_claim"
    };

    private static readonly string[] ForbiddenLegacyAliases =
    {
        "inconsistent_sun_shadow_direction",
        "severe_aliasing_or_shimmering",
        "claiming_render_quality_without_actual_render"
    };

    [MenuItem("NewTown/Lighting/Apply + Validate Solar Shadow Capture Coherence")]
    public static void ApplyAndPersist()
    {
        EnsureSceneOpen();
        QualityBlockEnvironmentContext context = RequireSingleContext();
        Light sun = RequireSummerSun();
        context.ApplyToDirectionalLight(sun);
        ValidateOpenScene();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Solar/shadow capture coherence reapplied and persisted. Real 4K shadow/contact evidence remains review-pending.");
    }

    [MenuItem("NewTown/QA/Validate Solar Shadow Capture Coherence Contract")]
    public static void ValidateContractConfigOnly()
    {
        SolarShadowCoherenceContract contract = LoadContract();
        if (!string.Equals(contract.schemaVersion, "1.1", StringComparison.Ordinal))
            throw new InvalidOperationException("Solar/shadow contract must use schema 1.1 canonical Visual Fidelity Gate bindings.");
        if (contract.status != "PENDING_REAL_UNITY_4K_RENDER")
            throw new InvalidOperationException("Solar/shadow contract must remain render-pending until actual Unity evidence exists.");
        if (!string.Equals(contract.visualGatePath, VisualGatePath, StringComparison.Ordinal))
            throw new InvalidOperationException("Solar/shadow contract visualGatePath drifted from the canonical gate.");
        if (contract.solarDatum == null || contract.shadowState == null || contract.failClosed == null || contract.verification == null)
            throw new InvalidOperationException("Solar/shadow contract is missing required structured sections.");

        ValidateCanonicalGateBindings(contract);

        if (Mathf.Abs(contract.solarDatum.latitudeDegrees - QualityBlockSolarShadowRuntimeContract.BenchmarkLatitudeDegrees) > 0.0001f ||
            contract.solarDatum.dayOfYear != QualityBlockSolarShadowRuntimeContract.BenchmarkDayOfYear ||
            Mathf.Abs(contract.solarDatum.localApparentSolarTimeHours - QualityBlockSolarShadowRuntimeContract.BenchmarkSolarTimeHours) > 0.0001f)
            throw new InvalidOperationException("Machine-readable solar datum drifted from the runtime capture contract.");

        SolarSample calculated = NewTownSolarModel.Calculate(
            contract.solarDatum.latitudeDegrees,
            contract.solarDatum.dayOfYear,
            contract.solarDatum.localApparentSolarTimeHours);
        if (Mathf.Abs(calculated.ElevationDegrees - contract.solarDatum.expectedElevationDegrees) > 0.02f ||
            DeltaAngleAbs(calculated.AzimuthDegrees, contract.solarDatum.expectedAzimuthDegrees) > 0.02f ||
            Mathf.Abs(calculated.HorizontalShadowPerMetre - contract.solarDatum.expectedHorizontalShadowPerMetre) > 0.002f)
            throw new InvalidOperationException(
                $"Solar-model reference math drifted: calculated elevation={calculated.ElevationDegrees:F5}, azimuth={calculated.AzimuthDegrees:F5}, shadow/m={calculated.HorizontalShadowPerMetre:F5}.");

        if (contract.shadowState.projection != "StableFit" || contract.shadowState.cascades != 4 ||
            contract.shadowState.cascadeSplit == null || contract.shadowState.cascadeSplit.Length != 3 ||
            Vector3.Distance(new Vector3(contract.shadowState.cascadeSplit[0], contract.shadowState.cascadeSplit[1], contract.shadowState.cascadeSplit[2]),
                QualityBlockSolarShadowRuntimeContract.CascadeSplit) > 0.0001f ||
            Mathf.Abs(contract.shadowState.shadowDistanceMetres - QualityBlockSolarShadowRuntimeContract.ShadowDistance) > 0.001f ||
            Mathf.Abs(contract.shadowState.lightBias - QualityBlockSolarShadowRuntimeContract.ShadowBias) > 0.0001f ||
            Mathf.Abs(contract.shadowState.lightNormalBias - QualityBlockSolarShadowRuntimeContract.ShadowNormalBias) > 0.0001f ||
            Mathf.Abs(contract.shadowState.lightNearPlane - QualityBlockSolarShadowRuntimeContract.ShadowNearPlane) > 0.0001f ||
            Mathf.Abs(contract.shadowState.globalNearPlaneOffset - QualityBlockSolarShadowRuntimeContract.GlobalShadowNearPlaneOffset) > 0.0001f ||
            contract.shadowState.requestedMsaa != QualityBlockSolarShadowRuntimeContract.RequestedMsaa ||
            Mathf.Abs(contract.shadowState.lodBias - QualityBlockSolarShadowRuntimeContract.LodBias) > 0.0001f)
            throw new InvalidOperationException("Machine-readable shadow state drifted from the runtime capture contract.");

        if (contract.failClosed.requiredActiveDirectionalLightCount != 1 ||
            contract.failClosed.requiredDirectionalLightName != "SummerSun" ||
            !contract.failClosed.mainCameraPreCullGuardRequired ||
            !contract.failClosed.cameraRenderMustInvokeGuard ||
            !contract.failClosed.actualRenderRequiredForVisualPoints)
            throw new InvalidOperationException("Solar/shadow fail-closed policy has been weakened.");

        if (contract.verification.visualFidelityPointsAwarded != 0 || contract.verification.native4kRenderVerified)
            throw new InvalidOperationException("Solar/shadow implementation contract must not self-award visual fidelity before real 4K review.");

        if (!File.Exists(LookdevPath))
            throw new InvalidOperationException($"Solar/shadow lookdev diagram missing: {LookdevPath}");
        string svg = File.ReadAllText(LookdevPath);
        foreach (string token in new[] { "58.11", "244.23", "0.622", "SummerSun", "pre-cull", "92/100" })
            if (svg.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0)
                throw new InvalidOperationException($"Solar/shadow lookdev diagram missing token: {token}");

        Debug.Log("Solar/shadow machine contract valid: deterministic midsummer sample, exact stable shadow state, sole directional key, render-time guard and canonical Visual Fidelity Gate bindings; visual points remain zero.");
    }

    [MenuItem("NewTown/QA/Validate Solar Shadow Capture Coherence Scene")]
    public static void ValidateOpenScene()
    {
        EnsureSceneOpen();
        SolarShadowCoherenceContract contract = LoadContract();
        ValidateContractConfigOnly();

        QualityBlockEnvironmentContext context = RequireSingleContext();
        Light sun = RequireSummerSun();
        QualityBlockSolarShadowRuntimeContract.ValidateOrThrow(context, sun, true);

        SolarSample sample = context.CalculateSolarSample();
        if (Mathf.Abs(sample.ElevationDegrees - contract.solarDatum.expectedElevationDegrees) > 0.02f ||
            DeltaAngleAbs(sample.AzimuthDegrees, contract.solarDatum.expectedAzimuthDegrees) > 0.02f ||
            Mathf.Abs(sample.HorizontalShadowPerMetre - contract.solarDatum.expectedHorizontalShadowPerMetre) > 0.002f)
            throw new InvalidOperationException("Prepared scene solar sample does not match the machine-readable reference datum.");

        QualityBlockSolarShadowPreRenderGuard[] guards = Resources.FindObjectsOfTypeAll<QualityBlockSolarShadowPreRenderGuard>()
            .Where(x => x.gameObject.scene.IsValid())
            .ToArray();
        if (guards.Length != 1)
            throw new InvalidOperationException($"Expected exactly one solar/shadow pre-render guard, found {guards.Length}.");
        QualityBlockSolarShadowPreRenderGuard guard = guards[0];
        if (!guard.enabled || guard.Context != context || guard.Sun != sun || guard.gameObject != context.gameObject)
            throw new InvalidOperationException("Solar/shadow pre-render guard is disabled or bound to the wrong context/light.");

        Light[] directionals = Resources.FindObjectsOfTypeAll<Light>()
            .Where(x => x.gameObject.scene.IsValid() && x.enabled && x.gameObject.activeInHierarchy && x.type == LightType.Directional)
            .ToArray();
        if (directionals.Length != 1 || directionals[0] != sun)
            throw new InvalidOperationException("Prepared scene does not contain exactly one active directional light.");

        Debug.Log(
            $"Solar/shadow scene coherence valid: elevation={sample.ElevationDegrees:F3} deg, azimuth={sample.AzimuthDegrees:F3} deg, " +
            $"horizontal shadow={sample.HorizontalShadowPerMetre:F3} m per vertical metre. Pre-cull guard is armed; rendered evidence is still required before scoring.");
    }

    private static void ValidateCanonicalGateBindings(SolarShadowCoherenceContract contract)
    {
        if (!File.Exists(VisualGatePath))
            throw new InvalidOperationException("Canonical Visual Fidelity Gate missing: " + VisualGatePath);
        if (contract.criticalFailureMappingPolicy == null ||
            !contract.criticalFailureMappingPolicy.requireExactCanonicalIds ||
            !contract.criticalFailureMappingPolicy.requireMembershipInVisualFidelityGate ||
            !contract.criticalFailureMappingPolicy.rejectLegacyAliases)
            throw new InvalidOperationException("Solar/shadow canonical critical-failure mapping policy was weakened.");

        VisualGateDocument gate = JsonUtility.FromJson<VisualGateDocument>(File.ReadAllText(VisualGatePath));
        if (gate == null || gate.categories == null || gate.criticalDefects == null)
            throw new InvalidOperationException("Canonical Visual Fidelity Gate cannot be parsed for solar/shadow binding.");

        string[] gateCategories = gate.categories.Where(x => x != null && !string.IsNullOrWhiteSpace(x.id)).Select(x => x.id).ToArray();
        string[] gateCritical = gate.criticalDefects.Where(x => x != null && !string.IsNullOrWhiteSpace(x.id)).Select(x => x.id).ToArray();
        if (gateCategories.Length != gate.categories.Length || gateCategories.Distinct(StringComparer.Ordinal).Count() != gateCategories.Length ||
            gateCritical.Length != gate.criticalDefects.Length || gateCritical.Distinct(StringComparer.Ordinal).Count() != gateCritical.Length)
            throw new InvalidOperationException("Canonical Visual Fidelity Gate contains blank or duplicate IDs.");

        RequireExactSet(contract.visualFidelityCategoryImpact, RequiredCategoryIds, "visualFidelityCategoryImpact");
        RequireExactSet(contract.criticalFailMappings, RequiredCriticalIds, "criticalFailMappings");
        RequireExactSet(contract.forbiddenLegacyAliases, ForbiddenLegacyAliases, "forbiddenLegacyAliases");

        foreach (string id in RequiredCategoryIds)
            if (!gateCategories.Contains(id, StringComparer.Ordinal))
                throw new InvalidOperationException($"Solar/shadow category '{id}' is not canonical in visual_fidelity_gate.json.");
        foreach (string id in RequiredCriticalIds)
            if (!gateCritical.Contains(id, StringComparer.Ordinal))
                throw new InvalidOperationException($"Solar/shadow automatic-FAIL ID '{id}' is not canonical in visual_fidelity_gate.json.");
        foreach (string alias in ForbiddenLegacyAliases)
            if (contract.criticalFailMappings.Contains(alias, StringComparer.Ordinal) || gateCritical.Contains(alias, StringComparer.Ordinal))
                throw new InvalidOperationException($"Forbidden solar/shadow legacy alias '{alias}' must not be used as a canonical automatic-FAIL ID.");
    }

    private static void RequireExactSet(string[] actual, string[] expected, string label)
    {
        string[] values = actual ?? Array.Empty<string>();
        if (values.Length != expected.Length || values.Any(string.IsNullOrWhiteSpace) ||
            values.Distinct(StringComparer.Ordinal).Count() != values.Length ||
            !new HashSet<string>(values, StringComparer.Ordinal).SetEquals(expected))
            throw new InvalidOperationException($"Solar/shadow {label} must be the exact canonical set: {string.Join(", ", expected)}.");
    }

    private static SolarShadowCoherenceContract LoadContract()
    {
        if (!File.Exists(ContractPath))
            throw new InvalidOperationException($"Solar/shadow machine-readable contract missing: {ContractPath}");
        SolarShadowCoherenceContract contract = JsonUtility.FromJson<SolarShadowCoherenceContract>(File.ReadAllText(ContractPath));
        if (contract == null)
            throw new InvalidOperationException("Solar/shadow contract could not be parsed.");
        return contract;
    }

    private static QualityBlockEnvironmentContext RequireSingleContext()
    {
        QualityBlockEnvironmentContext[] contexts = Resources.FindObjectsOfTypeAll<QualityBlockEnvironmentContext>()
            .Where(x => x.gameObject.scene.IsValid())
            .ToArray();
        if (contexts.Length != 1)
            throw new InvalidOperationException($"Expected exactly one physical environment context, found {contexts.Length}.");
        return contexts[0];
    }

    private static Light RequireSummerSun()
    {
        GameObject sunGo = Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x.scene.IsValid() && x.name == "SummerSun");
        Light sun = sunGo != null ? sunGo.GetComponent<Light>() : null;
        if (sun == null || sun.type != LightType.Directional)
            throw new InvalidOperationException("SummerSun directional light is missing.");
        return sun;
    }

    private static void EnsureSceneOpen()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static float DeltaAngleAbs(float a, float b)
    {
        return Mathf.Abs(Mathf.DeltaAngle(a, b));
    }

    [Serializable]
    private sealed class SolarShadowCoherenceContract
    {
        public string schemaVersion;
        public string status;
        public string visualGatePath;
        public string[] visualFidelityCategoryImpact;
        public SolarDatum solarDatum;
        public ShadowState shadowState;
        public FailClosed failClosed;
        public CriticalFailureMappingPolicy criticalFailureMappingPolicy;
        public string[] forbiddenLegacyAliases;
        public string[] criticalFailMappings;
        public Verification verification;
    }

    [Serializable] private sealed class CriticalFailureMappingPolicy
    {
        public bool requireExactCanonicalIds;
        public bool requireMembershipInVisualFidelityGate;
        public bool rejectLegacyAliases;
    }

    [Serializable] private sealed class VisualGateDocument
    {
        public GateCategory[] categories;
        public CriticalDefectDefinition[] criticalDefects;
    }

    [Serializable] private sealed class GateCategory { public string id; }
    [Serializable] private sealed class CriticalDefectDefinition { public string id; }

    [Serializable]
    private sealed class SolarDatum
    {
        public float latitudeDegrees;
        public int dayOfYear;
        public float localApparentSolarTimeHours;
        public float expectedElevationDegrees;
        public float expectedAzimuthDegrees;
        public float expectedHorizontalShadowPerMetre;
    }

    [Serializable]
    private sealed class ShadowState
    {
        public string projection;
        public int cascades;
        public float[] cascadeSplit;
        public float shadowDistanceMetres;
        public float lightBias;
        public float lightNormalBias;
        public float lightNearPlane;
        public float globalNearPlaneOffset;
        public int requestedMsaa;
        public float lodBias;
    }

    [Serializable]
    private sealed class FailClosed
    {
        public int requiredActiveDirectionalLightCount;
        public string requiredDirectionalLightName;
        public bool mainCameraPreCullGuardRequired;
        public bool cameraRenderMustInvokeGuard;
        public bool actualRenderRequiredForVisualPoints;
    }

    [Serializable]
    private sealed class Verification
    {
        public bool compileVerified;
        public bool native4kRenderVerified;
        public bool preCullGuardObservedInUnity;
        public int visualFidelityPointsAwarded;
        public string visualFidelityStatus;
    }
}
