using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Fail-closed source contract for the lighting-state fields that must be identity-bound across
/// realtime reflection-probe request/poll/completion and the immediately following native-4K still.
///
/// A field can remain individually legal while still changing the rendered result. The reflection
/// fingerprint therefore has to bind not only the physical sun/sky values, but also the rendering
/// policy that decides how those values are applied (light render mode, bounce energy, pixel-light
/// budget, shadowmask mode and subtractive shadow color). This QA also prevents stale aliases in the
/// reflection contract's critical-defect mapping from drifting away from the canonical 100-point gate.
///
/// Contract validity is implementation/evidence integrity only. It awards zero Visual Fidelity points.
/// </summary>
public static class QualityBlockReflectionLightingCoverageQA
{
    private const string SyncContractPath = "Assets/QA/reflection_probe_capture_sync_contract.json";
    private const string GatePath = "Assets/QA/visual_fidelity_gate.json";
    private const string ExpectedSchema = "1.1";
    private const string ExpectedFingerprint = "QualityBlockReflectionLightingStateFingerprint.BuildCurrentSha256";

    private static readonly string[] CanonicalMappedCriticalDefects =
    {
        "sun_shadow_inconsistency",
        "major_light_leak",
        "unverified_render_claim"
    };

    [MenuItem("NewTown/QA/Validate Reflection Lighting Fingerprint Coverage")]
    public static void ValidateContractConfigOnly()
    {
        ReflectionSyncContract sync = LoadJson<ReflectionSyncContract>(SyncContractPath);
        if (sync == null || !string.Equals(sync.schemaVersion, ExpectedSchema, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Reflection lighting coverage requires reflection_probe_capture_sync_contract schema " + ExpectedSchema + ".");

        LightingStateBinding lighting = sync.lightingStateBinding;
        if (lighting == null)
            throw new InvalidOperationException("Reflection lighting coverage contract is missing lightingStateBinding.");
        if (!string.Equals(lighting.fingerprintImplementation, ExpectedFingerprint, StringComparison.Ordinal) ||
            !string.Equals(lighting.fingerprintAlgorithm, QualityBlockReflectionLightingStateFingerprint.Algorithm, StringComparison.Ordinal))
            throw new InvalidOperationException("Reflection lighting coverage no longer targets the canonical SHA-256 fingerprint implementation.");

        if (!lighting.validateBeforeProbeRequest ||
            !lighting.validateEveryEditorPollWhileWaiting ||
            !lighting.validateAfterProbeCompletion ||
            !lighting.validateImmediatelyBeforeStillCapture ||
            !lighting.requireRequestCompletionMatch ||
            !lighting.requireCompletionStillMatch ||
            !lighting.includeSummerSunTransformAndPbrLightState ||
            !lighting.includeAuthoritativeSunRenderModeAndBounceState ||
            !lighting.includeProceduralSkyMaterialState ||
            !lighting.includeAmbientProbeCoefficients ||
            !lighting.includeRenderSettingsReflectionAndFogState ||
            !lighting.includeSubtractiveShadowColor ||
            !lighting.includeGlobalShadowAndLodSettings ||
            !lighting.includePixelLightAndShadowmaskState ||
            !lighting.actualRenderRequiredForVisualPoints)
            throw new InvalidOperationException(
                "Reflection lighting coverage was weakened. Request/poll/completion/pre-still identity must include sun render/bounce state, " +
                "sky/ambient/reflection/fog, subtractive shadow color, pixel-light/shadowmask state and global shadow/LOD state.");

        CriticalFailurePolicy policy = sync.criticalFailurePolicy;
        if (policy == null)
            throw new InvalidOperationException("Reflection lighting coverage contract is missing criticalFailurePolicy.");
        RequireExactSet(policy.mapsToVisualGateCriticalDefects, CanonicalMappedCriticalDefects,
            "reflection critical-defect mappings");

        VisualGate gate = LoadJson<VisualGate>(GatePath);
        if (gate == null || gate.criticalDefects == null)
            throw new InvalidOperationException("Canonical visual_fidelity_gate critical defects are missing.");
        string[] canonicalGateIds = gate.criticalDefects
            .Where(x => x != null)
            .Select(x => x.id)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
        foreach (string required in CanonicalMappedCriticalDefects)
        {
            if (!canonicalGateIds.Contains(required, StringComparer.Ordinal))
                throw new InvalidOperationException(
                    "Reflection contract maps to critical defect '" + required + "' but the canonical Visual Fidelity Gate does not contain that exact ID.");
        }
    }

    private static void RequireExactSet(string[] actual, string[] expected, string label)
    {
        if (actual == null)
            throw new InvalidOperationException(label + " is missing.");
        if (actual.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException(label + " contains a blank value.");
        if (actual.Distinct(StringComparer.Ordinal).Count() != actual.Length)
            throw new InvalidOperationException(label + " contains duplicates.");
        if (!new HashSet<string>(actual, StringComparer.Ordinal).SetEquals(expected ?? Array.Empty<string>()))
            throw new InvalidOperationException(
                label + " must be exactly [" + string.Join(", ", expected ?? Array.Empty<string>()) + "], got [" +
                string.Join(", ", actual) + "].");
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
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    [Serializable]
    private sealed class ReflectionSyncContract
    {
        public string schemaVersion;
        public LightingStateBinding lightingStateBinding;
        public CriticalFailurePolicy criticalFailurePolicy;
    }

    [Serializable]
    private sealed class LightingStateBinding
    {
        public string fingerprintImplementation;
        public string fingerprintAlgorithm;
        public bool validateBeforeProbeRequest;
        public bool validateEveryEditorPollWhileWaiting;
        public bool validateAfterProbeCompletion;
        public bool validateImmediatelyBeforeStillCapture;
        public bool requireRequestCompletionMatch;
        public bool requireCompletionStillMatch;
        public bool includeSummerSunTransformAndPbrLightState;
        public bool includeAuthoritativeSunRenderModeAndBounceState;
        public bool includeProceduralSkyMaterialState;
        public bool includeAmbientProbeCoefficients;
        public bool includeRenderSettingsReflectionAndFogState;
        public bool includeSubtractiveShadowColor;
        public bool includeGlobalShadowAndLodSettings;
        public bool includePixelLightAndShadowmaskState;
        public bool actualRenderRequiredForVisualPoints;
    }

    [Serializable]
    private sealed class CriticalFailurePolicy
    {
        public string[] mapsToVisualGateCriticalDefects;
    }

    [Serializable]
    private sealed class VisualGate
    {
        public CriticalDefect[] criticalDefects;
    }

    [Serializable]
    private sealed class CriticalDefect
    {
        public string id;
    }
}
