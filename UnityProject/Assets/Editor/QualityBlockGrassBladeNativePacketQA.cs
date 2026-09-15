using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Fail-closed source/configuration QA proving that the physical grass-blade field is part of the
/// authoritative native-4K review packet rather than merely an InitializeOnLoad convenience side effect.
/// The only mutating grass build must happen before realtime reflection probes are requested. From the
/// reflection request onward, grass geometry/material/LOD state is read-only and explicitly revalidated
/// before still and temporal evidence. Critical-failure mappings are cross-checked against the canonical
/// Visual Fidelity Gate IDs so a descriptive alias cannot silently bypass an automatic FAIL. This is
/// implementation/evidence integrity only and awards no Visual Fidelity points without actual pixels.
/// </summary>
public static class QualityBlockGrassBladeNativePacketQA
{
    private const string ContractPath = "Assets/QA/grass_blade_native_packet_integration_contract.json";
    private const string NativePacketPath = "Assets/Editor/QualityBlockNative4KReviewPacket.cs";
    private const string SourceGrassContractPath = "Assets/QA/grass_blade_field_contract.json";
    private const string PersistenceContractPath = "Assets/QA/grass_blade_formal_persistence_contract.json";
    private const string VisualGatePath = "Assets/QA/visual_fidelity_gate.json";

    private const string SelfValidationToken = "QualityBlockGrassBladeNativePacketQA.ValidateContractConfigOnly();";
    private const string PersistencePreflightToken = "QualityBlockGrassBladeFormalPersistenceQA.ValidateContractConfigOnly();";
    private const string PersistedBuildToken = "QualityBlockGrassBladeFieldUpgrade.ApplyToOpenScene(true);";
    private const string SourceValidationToken = "QualityBlockGrassBladeFieldUpgrade.ValidateOpenScene(false);";
    private const string PersistenceValidationToken = "QualityBlockGrassBladeFormalPersistenceQA.ValidatePersistedEvidenceBinding();";
    private const string ReflectionRequestToken = "QualityBlockReflectionProbeAwaiter.Begin(FinishAfterReflectionSynchronization);";
    private const string FinishMethodToken = "private static void FinishAfterReflectionSynchronization()";
    private const string StillCaptureToken = "QualityBlock4KCapture.CapturePreparedSceneAfterProbeSync();";
    private const string TemporalBeginToken = "QualityBlockTemporalRuntimeEvidenceGuard.Begin();";

    private static readonly string[] RequiredCriticalFailureMappings =
    {
        "missing_construction_material_metadata",
        "visible_lod_pop",
        "severe_aliasing_or_shimmer",
        "unverified_render_claim"
    };

    private static readonly string[] ForbiddenLegacyAliases =
    {
        "severe_aliasing_or_shimmering",
        "claiming_render_quality_without_actual_render"
    };

    [MenuItem("NewTown/QA/Validate Grass Blade Native Packet Integration")]
    public static void ValidateContractConfigOnly()
    {
        if (!File.Exists(ContractPath))
            throw new FileNotFoundException("Grass native-packet integration contract missing: " + ContractPath);
        if (!File.Exists(NativePacketPath))
            throw new FileNotFoundException("Native-4K review packet source missing: " + NativePacketPath);
        if (!File.Exists(SourceGrassContractPath) || !File.Exists(PersistenceContractPath))
            throw new FileNotFoundException("Grass source/persistence contracts must both exist before native-packet integration can be accepted.");
        if (!File.Exists(VisualGatePath))
            throw new FileNotFoundException("Canonical Visual Fidelity Gate contract missing: " + VisualGatePath);

        Contract contract = JsonUtility.FromJson<Contract>(File.ReadAllText(ContractPath));
        if (contract == null || !string.Equals(contract.schemaVersion, "1.1.0", StringComparison.Ordinal))
            throw new InvalidOperationException("Grass native-packet integration contract is null/unparseable or not schema 1.1.0.");
        if (!string.Equals(contract.status, "PENDING_REAL_UNITY_4K_RENDER", StringComparison.Ordinal) ||
            !string.Equals(contract.scenePath, "Assets/Scenes/QualityBlock1990s.unity", StringComparison.Ordinal) ||
            !string.Equals(contract.nativePacketPath, NativePacketPath, StringComparison.Ordinal) ||
            !string.Equals(contract.sourceGrassContractPath, SourceGrassContractPath, StringComparison.Ordinal) ||
            !string.Equals(contract.persistenceContractPath, PersistenceContractPath, StringComparison.Ordinal) ||
            !string.Equals(contract.visualGatePath, VisualGatePath, StringComparison.Ordinal))
            throw new InvalidOperationException("Grass native-packet integration identity/status paths drifted.");

        RequiredTokens tokens = contract.requiredTokens;
        MinimumOccurrences minimums = contract.minimumOccurrences;
        Ordering ordering = contract.ordering;
        if (tokens == null || minimums == null || ordering == null || contract.renderVerification == null)
            throw new InvalidOperationException("Grass native-packet integration contract is missing structured requirements.");
        if (!TokenEquals(tokens) ||
            minimums.selfValidation != 1 || minimums.persistenceContractPreflight != 1 ||
            minimums.finalPersistedBuild != 1 || minimums.sourceValidation < 4 || minimums.persistedEvidenceValidation < 4)
            throw new InvalidOperationException("Grass native-packet integration token/minimum requirements were weakened.");
        if (!ordering.finalPersistedBuildMustPrecedeReflectionRequest ||
            !ordering.noPersistedBuildAtOrAfterReflectionRequest ||
            !ordering.sourceAndPersistenceValidationRequiredBetweenFinalBuildAndReflectionRequest ||
            !ordering.sourceAndPersistenceValidationRequiredBetweenReflectionCompletionAndStillCapture ||
            !ordering.sourceAndPersistenceValidationRequiredBetweenStillSealAndTemporalBegin)
            throw new InvalidOperationException("Grass native-packet integration ordering requirements were weakened.");

        ValidateCriticalFailureMappings(contract);

        if (contract.renderVerification.runtimeRenderVerified ||
            contract.renderVerification.visualFidelityPointsAwarded != 0 ||
            !string.Equals(contract.renderVerification.visualFidelityStatus, "UNSCORED_UNTIL_REAL_4K_RENDER", StringComparison.Ordinal))
            throw new InvalidOperationException("Grass native-packet integration may not claim runtime verification or visual points.");

        string source = File.ReadAllText(NativePacketPath);
        RequireCount(source, SelfValidationToken, minimums.selfValidation, "self-validation");
        RequireCount(source, PersistencePreflightToken, minimums.persistenceContractPreflight, "persistence contract preflight");
        int buildCount = Count(source, PersistedBuildToken);
        if (buildCount != minimums.finalPersistedBuild)
            throw new InvalidOperationException($"Native packet must contain exactly one final persisted grass build; found {buildCount}.");
        RequireCount(source, SourceValidationToken, minimums.sourceValidation, "grass source validation");
        RequireCount(source, PersistenceValidationToken, minimums.persistedEvidenceValidation, "grass persisted-evidence validation");

        int build = RequireIndex(source, PersistedBuildToken);
        int reflection = RequireIndex(source, ReflectionRequestToken);
        int finish = RequireIndex(source, FinishMethodToken);
        int still = RequireIndex(source, StillCaptureToken);
        int temporal = RequireIndex(source, TemporalBeginToken);
        if (!(build < reflection && reflection < finish && finish < still && still < temporal))
            throw new InvalidOperationException("Native packet grass/reflection/still/temporal phase ordering is invalid.");
        if (source.IndexOf(PersistedBuildToken, reflection, StringComparison.Ordinal) >= 0)
            throw new InvalidOperationException("Grass geometry/material state may not be rebuilt at or after reflection synchronization begins.");

        RequireBothInWindow(source, SourceValidationToken, PersistenceValidationToken, build, reflection,
            "between final persisted grass build and reflection request");
        RequireBothInWindow(source, SourceValidationToken, PersistenceValidationToken, finish, still,
            "after reflection completion and before native-4K still capture");
        RequireBothInWindow(source, SourceValidationToken, PersistenceValidationToken, still, temporal,
            "after still capture/seal and before temporal capture begins");

        Debug.Log("Grass native-packet integration valid: one persisted pre-reflection build, explicit read-only validation before probe/still/temporal evidence, and canonical automatic-FAIL mappings bound to visual_fidelity_gate.json. Visual Fidelity remains UNSCORED.");
    }

    private static void ValidateCriticalFailureMappings(Contract contract)
    {
        CriticalFailureMappingPolicy policy = contract.criticalFailureMappingPolicy;
        if (policy == null || !policy.requireExactCanonicalIds ||
            !policy.requireMembershipInVisualFidelityGate || !policy.rejectLegacyAliases)
            throw new InvalidOperationException("Grass critical-failure mapping policy was weakened or is incomplete.");

        string[] mappings = contract.criticalFailureMappings ?? Array.Empty<string>();
        if (mappings.Length != RequiredCriticalFailureMappings.Length ||
            mappings.Any(string.IsNullOrWhiteSpace) ||
            mappings.Distinct(StringComparer.Ordinal).Count() != mappings.Length ||
            !new HashSet<string>(mappings, StringComparer.Ordinal).SetEquals(RequiredCriticalFailureMappings))
            throw new InvalidOperationException(
                "Grass native-packet criticalFailureMappings must be the exact canonical set: " +
                string.Join(", ", RequiredCriticalFailureMappings) + ".");

        string[] forbidden = contract.forbiddenLegacyAliases ?? Array.Empty<string>();
        if (forbidden.Length != ForbiddenLegacyAliases.Length ||
            forbidden.Distinct(StringComparer.Ordinal).Count() != forbidden.Length ||
            !new HashSet<string>(forbidden, StringComparer.Ordinal).SetEquals(ForbiddenLegacyAliases))
            throw new InvalidOperationException("Grass native-packet forbidden legacy critical aliases drifted.");
        if (mappings.Any(x => ForbiddenLegacyAliases.Contains(x, StringComparer.Ordinal)))
            throw new InvalidOperationException("Grass critical-failure mapping contains a forbidden descriptive/legacy alias.");

        VisualGateDocument gate = JsonUtility.FromJson<VisualGateDocument>(File.ReadAllText(VisualGatePath));
        if (gate == null || gate.criticalDefects == null || gate.criticalDefects.Length == 0)
            throw new InvalidOperationException("Canonical Visual Fidelity Gate critical-defect definitions are unavailable.");

        string[] gateIds = gate.criticalDefects
            .Where(x => x != null && !string.IsNullOrWhiteSpace(x.id))
            .Select(x => x.id)
            .ToArray();
        if (gateIds.Length != gate.criticalDefects.Length ||
            gateIds.Distinct(StringComparer.Ordinal).Count() != gateIds.Length)
            throw new InvalidOperationException("Canonical Visual Fidelity Gate contains null, blank or duplicate critical-defect IDs.");

        foreach (string mapping in mappings)
            if (!gateIds.Contains(mapping, StringComparer.Ordinal))
                throw new InvalidOperationException(
                    $"Grass critical-failure mapping '{mapping}' is not a canonical visual_fidelity_gate.json automatic-FAIL ID.");
        foreach (string alias in forbidden)
            if (gateIds.Contains(alias, StringComparer.Ordinal))
                throw new InvalidOperationException(
                    $"Forbidden grass critical alias '{alias}' unexpectedly became canonical; review the gate intentionally before changing this contract.");
    }

    private static bool TokenEquals(RequiredTokens t)
    {
        return t.selfValidation == SelfValidationToken &&
               t.persistenceContractPreflight == PersistencePreflightToken &&
               t.finalPersistedBuild == PersistedBuildToken &&
               t.sourceValidation == SourceValidationToken &&
               t.persistedEvidenceValidation == PersistenceValidationToken &&
               t.reflectionRequest == ReflectionRequestToken &&
               t.stillCapture == StillCaptureToken &&
               t.temporalBegin == TemporalBeginToken;
    }

    private static void RequireBothInWindow(string source, string a, string b, int startExclusive, int endExclusive, string label)
    {
        int aIndex = source.IndexOf(a, startExclusive + 1, StringComparison.Ordinal);
        int bIndex = source.IndexOf(b, startExclusive + 1, StringComparison.Ordinal);
        if (aIndex < 0 || bIndex < 0 || aIndex >= endExclusive || bIndex >= endExclusive)
            throw new InvalidOperationException("Native packet requires both grass source and persisted-evidence validation " + label + ".");
    }

    private static int RequireIndex(string source, string token)
    {
        int index = source.IndexOf(token, StringComparison.Ordinal);
        if (index < 0)
            throw new InvalidOperationException("Native packet missing required grass integration token: " + token);
        return index;
    }

    private static void RequireCount(string source, string token, int minimum, string label)
    {
        int count = Count(source, token);
        if (count < minimum)
            throw new InvalidOperationException($"Native packet {label} count {count} is below required minimum {minimum}.");
    }

    private static int Count(string source, string token)
    {
        int count = 0;
        int cursor = 0;
        while (cursor <= source.Length - token.Length)
        {
            int found = source.IndexOf(token, cursor, StringComparison.Ordinal);
            if (found < 0)
                break;
            count++;
            cursor = found + token.Length;
        }
        return count;
    }

    [Serializable]
    private sealed class Contract
    {
        public string schemaVersion;
        public string status;
        public string scenePath;
        public string nativePacketPath;
        public string sourceGrassContractPath;
        public string persistenceContractPath;
        public string visualGatePath;
        public RequiredTokens requiredTokens;
        public MinimumOccurrences minimumOccurrences;
        public Ordering ordering;
        public CriticalFailureMappingPolicy criticalFailureMappingPolicy;
        public string[] criticalFailureMappings;
        public string[] forbiddenLegacyAliases;
        public RenderVerification renderVerification;
    }

    [Serializable]
    private sealed class RequiredTokens
    {
        public string selfValidation;
        public string persistenceContractPreflight;
        public string finalPersistedBuild;
        public string sourceValidation;
        public string persistedEvidenceValidation;
        public string reflectionRequest;
        public string stillCapture;
        public string temporalBegin;
    }

    [Serializable]
    private sealed class MinimumOccurrences
    {
        public int selfValidation;
        public int persistenceContractPreflight;
        public int finalPersistedBuild;
        public int sourceValidation;
        public int persistedEvidenceValidation;
    }

    [Serializable]
    private sealed class Ordering
    {
        public bool finalPersistedBuildMustPrecedeReflectionRequest;
        public bool noPersistedBuildAtOrAfterReflectionRequest;
        public bool sourceAndPersistenceValidationRequiredBetweenFinalBuildAndReflectionRequest;
        public bool sourceAndPersistenceValidationRequiredBetweenReflectionCompletionAndStillCapture;
        public bool sourceAndPersistenceValidationRequiredBetweenStillSealAndTemporalBegin;
    }

    [Serializable]
    private sealed class CriticalFailureMappingPolicy
    {
        public bool requireExactCanonicalIds;
        public bool requireMembershipInVisualFidelityGate;
        public bool rejectLegacyAliases;
    }

    [Serializable]
    private sealed class VisualGateDocument
    {
        public CriticalDefectDefinition[] criticalDefects;
    }

    [Serializable]
    private sealed class CriticalDefectDefinition
    {
        public string id;
    }

    [Serializable]
    private sealed class RenderVerification
    {
        public bool runtimeRenderVerified;
        public int visualFidelityPointsAwarded;
        public string visualFidelityStatus;
    }
}
