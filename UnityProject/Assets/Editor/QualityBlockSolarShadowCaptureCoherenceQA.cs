using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Fail-closed QA for the benchmark's midsummer key-light state. It exists because validating shadow
/// settings before a generated-scene rebuild is insufficient: the physical-environment pass reapplies
/// SummerSun later. This validator binds the solar sample, the one-and-only directional key, shadow-map
/// settings and the runtime pre-cull guard into one auditable contract. It never awards visual points.
/// </summary>
public static class QualityBlockSolarShadowCaptureCoherenceQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/solar_shadow_capture_coherence_contract.json";
    private const string LookdevPath = "Assets/QA/Lookdev/solar_shadow_capture_coherence.svg";

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
        RequireText(contract.schemaVersion, "schemaVersion");
        if (contract.status != "PENDING_REAL_UNITY_4K_RENDER")
            throw new InvalidOperationException("Solar/shadow contract must remain render-pending until actual Unity evidence exists.");
        if (contract.solarDatum == null || contract.shadowState == null || contract.failClosed == null || contract.verification == null)
            throw new InvalidOperationException("Solar/shadow contract is missing required structured sections.");

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

        string[] requiredCritical =
        {
            "inconsistent_sun_shadow_direction",
            "severe_aliasing_or_shimmering",
            "visible_lod_pop",
            "major_light_leak",
            "claiming_render_quality_without_actual_render"
        };
        foreach (string id in requiredCritical)
            if (contract.criticalFailMappings == null || !contract.criticalFailMappings.Contains(id))
                throw new InvalidOperationException($"Solar/shadow contract missing critical-fail mapping: {id}");

        if (contract.verification.visualFidelityPointsAwarded != 0 || contract.verification.native4kRenderVerified)
            throw new InvalidOperationException("Solar/shadow implementation contract must not self-award visual fidelity before real 4K review.");

        if (!File.Exists(LookdevPath))
            throw new InvalidOperationException($"Solar/shadow lookdev diagram missing: {LookdevPath}");
        string svg = File.ReadAllText(LookdevPath);
        foreach (string token in new[] { "58.11", "244.23", "0.622", "SummerSun", "pre-cull", "92/100" })
            if (svg.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0)
                throw new InvalidOperationException($"Solar/shadow lookdev diagram missing token: {token}");

        Debug.Log("Solar/shadow machine contract valid: deterministic midsummer sample, exact stable shadow state, sole directional key and render-time guard; visual points remain zero.");
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

        // A contradictory second directional light is a critical visual defect, even if it is dimmer
        // than SummerSun. Do not select the strongest light and silently ignore the contradiction.
        Light[] directionals = Resources.FindObjectsOfTypeAll<Light>()
            .Where(x => x.gameObject.scene.IsValid() && x.enabled && x.gameObject.activeInHierarchy && x.type == LightType.Directional)
            .ToArray();
        if (directionals.Length != 1 || directionals[0] != sun)
            throw new InvalidOperationException("Prepared scene does not contain exactly one active directional light.");

        Debug.Log(
            $"Solar/shadow scene coherence valid: elevation={sample.ElevationDegrees:F3} deg, azimuth={sample.AzimuthDegrees:F3} deg, " +
            $"horizontal shadow={sample.HorizontalShadowPerMetre:F3} m per vertical metre. Pre-cull guard is armed; rendered evidence is still required before scoring.");
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

    private static void RequireText(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Solar/shadow contract missing {label}.");
    }

    [Serializable]
    private sealed class SolarShadowCoherenceContract
    {
        public string schemaVersion;
        public string status;
        public SolarDatum solarDatum;
        public ShadowState shadowState;
        public FailClosed failClosed;
        public string[] criticalFailMappings;
        public Verification verification;
    }

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
