using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Closes the formal-render lifecycle for the generated balcony separation panels.
///
/// The physical panel builder intentionally lives outside DanchiHighDetail so it does not weaken the
/// established six-material metric-UV registry. That separation also means the Native 4K packet must
/// explicitly build and persist the panel hierarchy; relying on editor initialization side effects is
/// not acceptable evidence. This wrapper owns that lifecycle boundary and verifies the source runner
/// calls it exactly once before reflection synchronization, then performs read-only validation after
/// reflection synchronization and again before temporal evidence.
///
/// Source/persistence validity is implementation evidence only and awards zero Visual Fidelity points.
/// </summary>
public static class QualityBlockBalconySeparationPanelFormalIntegrationQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/balcony_separation_panel_contract.json";
    private const string MetadataCoveragePath = "Assets/QA/scene_metadata_coverage_contract.json";
    private const string VisualGatePath = "Assets/QA/visual_fidelity_gate.json";
    private const string PacketPath = "Assets/Editor/QualityBlockNative4KReviewPacket.cs";

    private const string ApplyToken = "QualityBlockBalconySeparationPanelFormalIntegrationQA.ApplyAndPersist();";
    private const string ValidateToken = "QualityBlockBalconySeparationPanelFormalIntegrationQA.ValidateOpenScene();";
    private const string ContractValidateToken = "QualityBlockBalconySeparationPanelFormalIntegrationQA.ValidateContractConfigOnly();";
    private const string ReflectionBeginToken = "QualityBlockReflectionProbeAwaiter.Begin(FinishAfterReflectionSynchronization);";
    private const string StillCaptureToken = "QualityBlock4KCapture.CapturePreparedSceneAfterProbeSync();";
    private const string TemporalBeginToken = "QualityBlockTemporalRuntimeEvidenceGuard.Begin();";

    private static readonly string[] RequiredCriticalIds =
    {
        "visible_primitive_placeholder",
        "impossible_material_physics",
        "obvious_repetition",
        "hero_geometry_intersection",
        "visible_lod_pop",
        "missing_construction_material_metadata",
        "unverified_render_claim"
    };

    private static readonly string[] LegacyCriticalAliases =
    {
        "materially_impossible_metallic_or_specular",
        "floating_or_interpenetrating_hero_geometry",
        "obvious_repeated_module_pattern",
        "missing_required_construction_or_material_metadata"
    };

    [MenuItem("NewTown/QA/Validate Balcony Separation Panel Formal Integration")]
    public static void ValidateContractConfigOnly()
    {
        QualityBlockBalconySeparationPanelUpgrade.ValidateContractConfigOnly();

        PanelContract contract = LoadPanelContract();
        if (contract == null || contract.nativePacketIntegration == null)
            throw new InvalidOperationException("Balcony separation-panel contract is missing nativePacketIntegration.");

        NativePacketIntegration integration = contract.nativePacketIntegration;
        if (!string.Equals(integration.formalRunnerPath, PacketPath, StringComparison.Ordinal) ||
            !string.Equals(integration.buildMethod, "QualityBlockBalconySeparationPanelFormalIntegrationQA.ApplyAndPersist", StringComparison.Ordinal) ||
            !string.Equals(integration.validateMethod, "QualityBlockBalconySeparationPanelFormalIntegrationQA.ValidateOpenScene", StringComparison.Ordinal) ||
            !integration.persistBeforeReflection || !integration.validationOnlyAfterReflectionRequest ||
            integration.requiredPacketValidationBoundaries < 3 ||
            !string.Equals(integration.visualGateAuthorityPath, VisualGatePath, StringComparison.Ordinal) ||
            !string.Equals(integration.evidenceBindingContractPath, MetadataCoveragePath, StringComparison.Ordinal) ||
            !string.Equals(integration.evidenceBindingDomainId, "danchi_domain", StringComparison.Ordinal))
            throw new InvalidOperationException("Balcony separation-panel native packet integration policy drifted.");

        RequireBoundary(integration.requiredBoundaries, "pre_reflection");
        RequireBoundary(integration.requiredBoundaries, "post_reflection_pre_still");
        RequireBoundary(integration.requiredBoundaries, "post_still_pre_temporal");

        ValidateCriticalMappings(contract);
        ValidateMetadataEvidenceBinding();
        ValidatePacketSourceIntegration();
    }

    [MenuItem("NewTown/Geometry/Apply And Persist Balcony Separation Panels For Formal Capture")]
    public static void ApplyAndPersist()
    {
        ValidateContractConfigOnly();
        RequireQualityScene();

        QualityBlockBalconySeparationPanelUpgrade.ApplyToOpenScene();
        QualityBlockBalconySeparationPanelUpgrade.ValidateOpenScene();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.isDirty)
            throw new InvalidOperationException("Balcony separation-panel formal state remained dirty after persistence.");

        QualityBlockBalconySeparationPanelUpgrade.ValidateOpenScene();
        Debug.Log(
            "Balcony separation panels were explicitly built and persisted for the formal Native 4K packet. " +
            "Visual Fidelity remains UNSCORED until actual 3840x2160 still/crop/temporal evidence is reviewed.");
    }

    public static void ValidateOpenScene()
    {
        ValidateContractConfigOnly();
        RequireQualityScene();
        QualityBlockBalconySeparationPanelUpgrade.ValidateOpenScene();
    }

    private static void ValidatePacketSourceIntegration()
    {
        if (!File.Exists(PacketPath))
            throw new InvalidOperationException("Native 4K packet source is missing: " + PacketPath);

        string source = File.ReadAllText(PacketPath);
        if (CountOccurrences(source, ContractValidateToken) != 1)
            throw new InvalidOperationException("Native 4K packet must validate the balcony-panel integration contract exactly once during preflight.");
        if (CountOccurrences(source, ApplyToken) != 1)
            throw new InvalidOperationException("Native 4K packet must build/persist balcony separation panels exactly once.");
        if (CountOccurrences(source, ValidateToken) < 3)
            throw new InvalidOperationException("Native 4K packet must validate balcony panels at all three formal lifecycle boundaries.");

        int apply = source.IndexOf(ApplyToken, StringComparison.Ordinal);
        int reflection = source.IndexOf(ReflectionBeginToken, StringComparison.Ordinal);
        int still = source.IndexOf(StillCaptureToken, StringComparison.Ordinal);
        int temporal = source.IndexOf(TemporalBeginToken, StringComparison.Ordinal);
        if (apply < 0 || reflection < 0 || still < 0 || temporal < 0 || !(apply < reflection && reflection < still && still < temporal))
            throw new InvalidOperationException("Balcony-panel apply/reflection/still/temporal lifecycle ordering is invalid.");

        int preReflectionValidation = source.IndexOf(ValidateToken, apply + ApplyToken.Length, StringComparison.Ordinal);
        int postReflectionValidation = source.IndexOf(ValidateToken, reflection + ReflectionBeginToken.Length, StringComparison.Ordinal);
        int postStillValidation = source.IndexOf(ValidateToken, still + StillCaptureToken.Length, StringComparison.Ordinal);
        if (preReflectionValidation < 0 || preReflectionValidation >= reflection)
            throw new InvalidOperationException("Balcony-panel state must be validated after build and before reflection synchronization.");
        if (postReflectionValidation < 0 || postReflectionValidation >= still)
            throw new InvalidOperationException("Balcony-panel state must be revalidated after reflection synchronization and before still capture.");
        if (postStillValidation < 0 || postStillValidation >= temporal)
            throw new InvalidOperationException("Balcony-panel state must be revalidated after still capture and before temporal capture.");

        int secondApply = source.IndexOf(ApplyToken, apply + ApplyToken.Length, StringComparison.Ordinal);
        if (secondApply >= 0)
            throw new InvalidOperationException("Balcony panels may not be regenerated after the single formal pre-reflection build.");
    }

    private static void ValidateCriticalMappings(PanelContract contract)
    {
        if (!File.Exists(VisualGatePath))
            throw new InvalidOperationException("Visual Fidelity Gate is missing: " + VisualGatePath);

        VisualGate gate = JsonUtility.FromJson<VisualGate>(File.ReadAllText(VisualGatePath));
        var canonical = new HashSet<string>(
            gate?.criticalDefects?.Where(x => x != null && !string.IsNullOrWhiteSpace(x.id)).Select(x => x.id)
            ?? Enumerable.Empty<string>(),
            StringComparer.Ordinal);
        if (canonical.Count == 0)
            throw new InvalidOperationException("Visual Fidelity Gate contains no critical-defect IDs.");

        string[] mapped = contract.criticalDefectRisks ?? Array.Empty<string>();
        if (mapped.Length != mapped.Distinct(StringComparer.Ordinal).Count())
            throw new InvalidOperationException("Balcony separation-panel critical-defect mapping contains duplicates.");

        foreach (string required in RequiredCriticalIds)
        {
            if (!canonical.Contains(required))
                throw new InvalidOperationException("Required canonical critical-defect ID is missing from the Visual Fidelity Gate: " + required);
            if (!mapped.Contains(required, StringComparer.Ordinal))
                throw new InvalidOperationException("Balcony separation-panel contract is missing canonical critical mapping: " + required);
        }

        foreach (string id in mapped)
            if (!canonical.Contains(id))
                throw new InvalidOperationException("Balcony separation-panel contract uses a non-canonical critical-defect ID: " + id);

        foreach (string legacy in LegacyCriticalAliases)
            if (mapped.Contains(legacy, StringComparer.Ordinal))
                throw new InvalidOperationException("Legacy balcony-panel critical alias is forbidden: " + legacy);
    }

    private static void ValidateMetadataEvidenceBinding()
    {
        if (!File.Exists(MetadataCoveragePath))
            throw new InvalidOperationException("Scene metadata coverage contract is missing: " + MetadataCoveragePath);

        string source = File.ReadAllText(MetadataCoveragePath);
        const string contractToken = "Assets/QA/balcony_separation_panel_contract.json";
        if (CountOccurrences(source, contractToken) != 1 || source.IndexOf("\"id\": \"danchi_domain\"", StringComparison.Ordinal) < 0)
            throw new InvalidOperationException(
                "Balcony separation-panel contract must remain exactly once in the Danchi metadata evidence bundle.");
    }

    private static PanelContract LoadPanelContract()
    {
        if (!File.Exists(ContractPath))
            throw new InvalidOperationException("Balcony separation-panel contract is missing: " + ContractPath);
        PanelContract contract = JsonUtility.FromJson<PanelContract>(File.ReadAllText(ContractPath));
        if (contract == null || !string.Equals(contract.contractId, "balcony_separation_panel_physical_assembly", StringComparison.Ordinal))
            throw new InvalidOperationException("Balcony separation-panel contract could not be parsed or has the wrong contractId.");
        return contract;
    }

    private static void RequireBoundary(string[] boundaries, string required)
    {
        if (boundaries == null || !boundaries.Contains(required, StringComparer.Ordinal))
            throw new InvalidOperationException("Balcony separation-panel formal boundary missing: " + required);
    }

    private static void RequireQualityScene()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !string.Equals(scene.path, ScenePath, StringComparison.Ordinal))
            throw new InvalidOperationException("Balcony separation-panel formal integration requires the canonical benchmark scene: " + ScenePath);
    }

    private static int CountOccurrences(string source, string token)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(token)) return 0;
        int count = 0;
        int index = 0;
        while ((index = source.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }
        return count;
    }

    [Serializable]
    private sealed class PanelContract
    {
        public string contractId;
        public NativePacketIntegration nativePacketIntegration;
        public string[] criticalDefectRisks;
    }

    [Serializable]
    private sealed class NativePacketIntegration
    {
        public string formalRunnerPath;
        public string buildMethod;
        public string validateMethod;
        public bool persistBeforeReflection;
        public bool validationOnlyAfterReflectionRequest;
        public int requiredPacketValidationBoundaries;
        public string[] requiredBoundaries;
        public string visualGateAuthorityPath;
        public string evidenceBindingContractPath;
        public string evidenceBindingDomainId;
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
