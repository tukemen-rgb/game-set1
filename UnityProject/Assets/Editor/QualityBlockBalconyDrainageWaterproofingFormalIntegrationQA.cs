using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Makes the generated balcony drainage/waterproofing assembly an explicit, persisted member of the
/// authoritative Native-4K packet. InitializeOnLoad/scene-saving side effects are not accepted as formal
/// evidence: one mutating build must occur before reflection synchronization, then all later lifecycle
/// boundaries are validation-only. The detailed manufacture/material contract must also be present exactly
/// once in the Danchi metadata domain so the construction/material SHA-256 source bundle seals it to the
/// rendered evidence. Source/configuration success awards zero Visual Fidelity points without real pixels.
/// </summary>
public static class QualityBlockBalconyDrainageWaterproofingFormalIntegrationQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string IntegrationContractPath = "Assets/QA/balcony_drainage_native_packet_integration_contract.json";
    private const string SourceContractPath = "Assets/QA/balcony_drainage_waterproofing_contract.json";
    private const string VisualGatePath = "Assets/QA/visual_fidelity_gate.json";
    private const string MetadataCoveragePath = "Assets/QA/scene_metadata_coverage_contract.json";
    private const string EvidenceBinderPath = "Assets/Editor/QualityBlockConstructionMaterialEvidenceBindingQA.cs";
    private const string PacketPath = "Assets/Editor/QualityBlockNative4KReviewPacket.cs";

    private const string SelfValidationToken = "QualityBlockBalconyDrainageWaterproofingFormalIntegrationQA.ValidateContractConfigOnly();";
    private const string ApplyToken = "QualityBlockBalconyDrainageWaterproofingFormalIntegrationQA.ApplyAndPersist();";
    private const string ValidateToken = "QualityBlockBalconyDrainageWaterproofingFormalIntegrationQA.ValidateOpenScene();";
    private const string ReflectionToken = "QualityBlockReflectionProbeAwaiter.Begin(FinishAfterReflectionSynchronization);";
    private const string FinishToken = "private static void FinishAfterReflectionSynchronization()";
    private const string StillToken = "QualityBlock4KCapture.CapturePreparedSceneAfterProbeSync();";
    private const string TemporalToken = "QualityBlockTemporalRuntimeEvidenceGuard.Begin();";

    private static readonly string[] CanonicalCriticalIds =
    {
        "visible_primitive_placeholder",
        "baked_or_painted_highlights",
        "impossible_material_physics",
        "obvious_repetition",
        "hero_geometry_intersection",
        "sun_shadow_inconsistency",
        "forbidden_disaster_theme",
        "severe_aliasing_or_shimmer",
        "visible_lod_pop",
        "major_light_leak",
        "missing_construction_material_metadata",
        "unverified_render_claim"
    };

    [MenuItem("NewTown/QA/Validate Balcony Drainage Formal Integration")]
    public static void ValidateContractConfigOnly()
    {
        QualityBlockBalconyDrainageWaterproofingQA.ValidateContractConfigOnly();
        RequireFile(IntegrationContractPath);
        RequireFile(SourceContractPath);
        RequireFile(VisualGatePath);
        RequireFile(MetadataCoveragePath);
        RequireFile(EvidenceBinderPath);
        RequireFile(PacketPath);

        Contract contract = JsonUtility.FromJson<Contract>(File.ReadAllText(IntegrationContractPath));
        if (contract == null || !string.Equals(contract.schemaVersion, "1.0.0", StringComparison.Ordinal) ||
            !string.Equals(contract.contractId, "balcony_drainage_waterproofing_native_packet_integration", StringComparison.Ordinal) ||
            !string.Equals(contract.status, "PENDING_REAL_UNITY_4K_RENDER", StringComparison.Ordinal) ||
            !string.Equals(contract.scenePath, ScenePath, StringComparison.Ordinal) ||
            !string.Equals(contract.nativePacketPath, PacketPath, StringComparison.Ordinal) ||
            !string.Equals(contract.sourceContractPath, SourceContractPath, StringComparison.Ordinal) ||
            !string.Equals(contract.visualGatePath, VisualGatePath, StringComparison.Ordinal) ||
            !string.Equals(contract.sceneMetadataCoverageContractPath, MetadataCoveragePath, StringComparison.Ordinal) ||
            !string.Equals(contract.constructionMaterialEvidenceBindingSourcePath, EvidenceBinderPath, StringComparison.Ordinal))
            throw new InvalidOperationException("Balcony drainage formal integration identity/status/path policy drifted.");

        if (contract.formalLifecycle == null || contract.requiredTokens == null || contract.minimumOccurrences == null ||
            contract.metadataCoverageBinding == null || contract.criticalFailureMappingPolicy == null || contract.renderVerification == null)
            throw new InvalidOperationException("Balcony drainage formal integration contract is structurally incomplete.");

        FormalLifecycle lifecycle = contract.formalLifecycle;
        if (!string.Equals(lifecycle.buildMethod, "QualityBlockBalconyDrainageWaterproofingFormalIntegrationQA.ApplyAndPersist", StringComparison.Ordinal) ||
            !string.Equals(lifecycle.validateMethod, "QualityBlockBalconyDrainageWaterproofingFormalIntegrationQA.ValidateOpenScene", StringComparison.Ordinal) ||
            !lifecycle.persistBeforeReflection || !lifecycle.validationOnlyAfterReflectionRequest ||
            lifecycle.requiredPacketValidationBoundaries < 3)
            throw new InvalidOperationException("Balcony drainage formal lifecycle policy was weakened.");
        RequireBoundary(lifecycle.requiredBoundaries, "pre_reflection");
        RequireBoundary(lifecycle.requiredBoundaries, "post_reflection_pre_still");
        RequireBoundary(lifecycle.requiredBoundaries, "post_still_pre_temporal");

        RequiredTokens tokens = contract.requiredTokens;
        if (tokens.selfValidation != SelfValidationToken || tokens.persistedBuild != ApplyToken ||
            tokens.readOnlyValidation != ValidateToken || tokens.reflectionRequest != ReflectionToken ||
            tokens.stillCapture != StillToken || tokens.temporalBegin != TemporalToken)
            throw new InvalidOperationException("Balcony drainage formal source tokens drifted.");
        if (contract.minimumOccurrences.selfValidation != 1 || contract.minimumOccurrences.persistedBuild != 1 ||
            contract.minimumOccurrences.readOnlyValidation < 3)
            throw new InvalidOperationException("Balcony drainage formal occurrence requirements were weakened.");

        ValidateMetadataEvidenceBinding(contract.metadataCoverageBinding);
        ValidateCanonicalCriticalMappings(contract);
        ValidatePacketSourceIntegration(contract.minimumOccurrences);

        if (contract.renderVerification.runtimeRenderVerified ||
            contract.renderVerification.visualFidelityPointsAwarded != 0 ||
            !string.Equals(contract.renderVerification.visualFidelityStatus, "UNSCORED_UNTIL_REAL_4K_RENDER", StringComparison.Ordinal))
            throw new InvalidOperationException("Balcony drainage integration may not claim render verification or Visual Fidelity points.");
    }

    [MenuItem("NewTown/Geometry/Apply And Persist Balcony Drainage For Formal Capture")]
    public static void ApplyAndPersist()
    {
        ValidateContractConfigOnly();
        RequireQualityScene();
        QualityBlockBalconyDrainageWaterproofingUpgrade.ApplyToOpenScene();
        QualityBlockBalconyDrainageWaterproofingQA.ValidateOpenScene();

        Scene scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        if (scene.isDirty)
            throw new InvalidOperationException("Balcony drainage formal state remained dirty after persistence.");

        QualityBlockBalconyDrainageWaterproofingQA.ValidateOpenScene();
        Debug.Log("Balcony drainage/waterproofing explicitly persisted for Native-4K formal evidence. Visual Fidelity remains UNSCORED pending real pixels.");
    }

    public static void ValidateOpenScene()
    {
        ValidateContractConfigOnly();
        RequireQualityScene();
        QualityBlockBalconyDrainageWaterproofingQA.ValidateOpenScene();
    }

    private static void ValidateMetadataEvidenceBinding(MetadataCoverageBinding requirement)
    {
        if (!string.Equals(requirement.requiredDomainId, "danchi_domain", StringComparison.Ordinal) ||
            !string.Equals(requirement.requiredRootName, "Danchi", StringComparison.Ordinal) ||
            !string.Equals(requirement.requiredContractPath, SourceContractPath, StringComparison.Ordinal) ||
            !requirement.requireExactlyOneDomainMatch || !requirement.requireExactlyOneContractPathMatch ||
            !requirement.requireConstructionMaterialBundleEnumeration)
            throw new InvalidOperationException("Balcony drainage metadata evidence policy was weakened.");

        CoverageDocument coverage = JsonUtility.FromJson<CoverageDocument>(File.ReadAllText(MetadataCoveragePath));
        if (coverage == null || coverage.domains == null)
            throw new InvalidOperationException("Scene metadata coverage domains unavailable.");
        CoverageDomain[] matches = coverage.domains.Where(x => x != null &&
            string.Equals(x.id, "danchi_domain", StringComparison.Ordinal) &&
            string.Equals(x.rootName, "Danchi", StringComparison.Ordinal)).ToArray();
        if (matches.Length != 1)
            throw new InvalidOperationException($"Expected exactly one Danchi metadata domain, found {matches.Length}.");

        int danchiCount = (matches[0].contractPaths ?? Array.Empty<string>())
            .Count(x => string.Equals(x, SourceContractPath, StringComparison.Ordinal));
        int totalCount = coverage.domains.Where(x => x != null && x.contractPaths != null)
            .Sum(x => x.contractPaths.Count(p => string.Equals(p, SourceContractPath, StringComparison.Ordinal)));
        if (danchiCount != 1 || totalCount != 1)
            throw new InvalidOperationException($"{SourceContractPath} must appear exactly once and only in Danchi metadata; danchi={danchiCount}, total={totalCount}.");

        string binder = File.ReadAllText(EvidenceBinderPath);
        foreach (string token in new[] {
            "BuildRequiredSourcePaths(CoverageContract coverage)",
            "foreach (string path in domain.contractPaths)",
            "paths.Add(path);" })
            if (binder.IndexOf(token, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("Construction/material SHA-256 evidence binder no longer enumerates scene-domain contractPaths: " + token);
    }

    private static void ValidateCanonicalCriticalMappings(Contract contract)
    {
        if (!contract.criticalFailureMappingPolicy.requireExactCanonicalIds ||
            !contract.criticalFailureMappingPolicy.requireMembershipInVisualFidelityGate ||
            !contract.criticalFailureMappingPolicy.sourceValidationCannotClearRenderedDefects)
            throw new InvalidOperationException("Balcony drainage critical-failure mapping policy was weakened.");

        string[] mapped = contract.canonicalCriticalFailureMappings ?? Array.Empty<string>();
        if (mapped.Length != CanonicalCriticalIds.Length || mapped.Any(string.IsNullOrWhiteSpace) ||
            mapped.Distinct(StringComparer.Ordinal).Count() != mapped.Length ||
            !new HashSet<string>(mapped, StringComparer.Ordinal).SetEquals(CanonicalCriticalIds))
            throw new InvalidOperationException("Balcony drainage critical mappings are not the exact canonical automatic-FAIL set.");

        VisualGate gate = JsonUtility.FromJson<VisualGate>(File.ReadAllText(VisualGatePath));
        string[] gateIds = gate?.criticalDefects?.Where(x => x != null && !string.IsNullOrWhiteSpace(x.id)).Select(x => x.id).ToArray()
            ?? Array.Empty<string>();
        if (gateIds.Length != CanonicalCriticalIds.Length || gateIds.Distinct(StringComparer.Ordinal).Count() != gateIds.Length ||
            !new HashSet<string>(gateIds, StringComparer.Ordinal).SetEquals(CanonicalCriticalIds))
            throw new InvalidOperationException("Canonical Visual Fidelity Gate critical-defect set drifted from the required 12 automatic FAIL IDs.");
    }

    private static void ValidatePacketSourceIntegration(MinimumOccurrences minimums)
    {
        string source = File.ReadAllText(PacketPath);
        if (Count(source, SelfValidationToken) != minimums.selfValidation)
            throw new InvalidOperationException("Native packet must preflight balcony drainage formal integration exactly once.");
        if (Count(source, ApplyToken) != minimums.persistedBuild)
            throw new InvalidOperationException("Native packet must contain exactly one persisted balcony drainage build.");
        if (Count(source, ValidateToken) < minimums.readOnlyValidation)
            throw new InvalidOperationException("Native packet lacks required balcony drainage lifecycle validations.");

        int apply = RequireIndex(source, ApplyToken);
        int reflection = RequireIndex(source, ReflectionToken);
        int finish = RequireIndex(source, FinishToken);
        int still = RequireIndex(source, StillToken);
        int temporal = RequireIndex(source, TemporalToken);
        if (!(apply < reflection && reflection < finish && finish < still && still < temporal))
            throw new InvalidOperationException("Balcony drainage apply/reflection/still/temporal phase ordering is invalid.");
        if (source.IndexOf(ApplyToken, reflection, StringComparison.Ordinal) >= 0)
            throw new InvalidOperationException("Balcony drainage may not be regenerated at or after reflection synchronization begins.");
        RequireValidationInWindow(source, apply, reflection, "after persisted build and before reflection request");
        RequireValidationInWindow(source, finish, still, "after reflection completion and before still capture");
        RequireValidationInWindow(source, still, temporal, "after still capture and before temporal capture");
    }

    private static void RequireValidationInWindow(string source, int startExclusive, int endExclusive, string label)
    {
        int index = source.IndexOf(ValidateToken, startExclusive + 1, StringComparison.Ordinal);
        if (index < 0 || index >= endExclusive)
            throw new InvalidOperationException("Balcony drainage read-only validation required " + label + ".");
    }

    private static void RequireBoundary(string[] values, string required)
    {
        if (values == null || !values.Contains(required, StringComparer.Ordinal))
            throw new InvalidOperationException("Balcony drainage lifecycle boundary missing: " + required);
    }

    private static void RequireFile(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Required balcony drainage integration file missing: " + path);
    }

    private static void RequireQualityScene()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !string.Equals(scene.path, ScenePath, StringComparison.Ordinal))
            throw new InvalidOperationException("Balcony drainage formal integration requires canonical benchmark scene: " + ScenePath);
    }

    private static int RequireIndex(string source, string token)
    {
        int index = source.IndexOf(token, StringComparison.Ordinal);
        if (index < 0) throw new InvalidOperationException("Native packet missing balcony drainage integration token: " + token);
        return index;
    }

    private static int Count(string source, string token)
    {
        int count = 0, cursor = 0;
        while (cursor <= source.Length - token.Length)
        {
            int found = source.IndexOf(token, cursor, StringComparison.Ordinal);
            if (found < 0) break;
            count++;
            cursor = found + token.Length;
        }
        return count;
    }

    [Serializable] private sealed class Contract
    {
        public string schemaVersion;
        public string contractId;
        public string status;
        public string scenePath;
        public string nativePacketPath;
        public string sourceContractPath;
        public string visualGatePath;
        public string sceneMetadataCoverageContractPath;
        public string constructionMaterialEvidenceBindingSourcePath;
        public MetadataCoverageBinding metadataCoverageBinding;
        public FormalLifecycle formalLifecycle;
        public RequiredTokens requiredTokens;
        public MinimumOccurrences minimumOccurrences;
        public string[] canonicalCriticalFailureMappings;
        public CriticalFailureMappingPolicy criticalFailureMappingPolicy;
        public RenderVerification renderVerification;
    }
    [Serializable] private sealed class MetadataCoverageBinding
    {
        public string requiredDomainId;
        public string requiredRootName;
        public string requiredContractPath;
        public bool requireExactlyOneDomainMatch;
        public bool requireExactlyOneContractPathMatch;
        public bool requireConstructionMaterialBundleEnumeration;
    }
    [Serializable] private sealed class FormalLifecycle
    {
        public string buildMethod;
        public string validateMethod;
        public bool persistBeforeReflection;
        public bool validationOnlyAfterReflectionRequest;
        public int requiredPacketValidationBoundaries;
        public string[] requiredBoundaries;
    }
    [Serializable] private sealed class RequiredTokens
    {
        public string selfValidation;
        public string persistedBuild;
        public string readOnlyValidation;
        public string reflectionRequest;
        public string stillCapture;
        public string temporalBegin;
    }
    [Serializable] private sealed class MinimumOccurrences
    {
        public int selfValidation;
        public int persistedBuild;
        public int readOnlyValidation;
    }
    [Serializable] private sealed class CriticalFailureMappingPolicy
    {
        public bool requireExactCanonicalIds;
        public bool requireMembershipInVisualFidelityGate;
        public bool sourceValidationCannotClearRenderedDefects;
    }
    [Serializable] private sealed class RenderVerification
    {
        public bool runtimeRenderVerified;
        public string visualFidelityStatus;
        public int visualFidelityPointsAwarded;
    }
    [Serializable] private sealed class CoverageDocument { public CoverageDomain[] domains; }
    [Serializable] private sealed class CoverageDomain { public string id; public string rootName; public string[] contractPaths; }
    [Serializable] private sealed class VisualGate { public CriticalDefect[] criticalDefects; }
    [Serializable] private sealed class CriticalDefect { public string id; }
}
