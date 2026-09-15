using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Separates clean-session contract preflight from prepared-scene validation for the authoritative
/// native-4K review packet. A fresh clone does not repository-author QualitySettings.asset, so the
/// editor's pre-rebuild QualitySettings are not evidence. The deterministic benchmark rebuild must
/// establish the physical sun/shadow/camera/LOD policy first; only that prepared state is eligible for
/// scene validation. This QA awards zero Visual Fidelity points.
/// </summary>
public static class QualityBlockNative4KCleanSessionPreflightQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/native_4k_clean_session_preflight_contract.json";
    private const string ShadowContractPath = "Assets/QA/shadow_stability_contract.json";
    private const string SolarContractPath = "Assets/QA/solar_shadow_capture_coherence_contract.json";

    [MenuItem("NewTown/QA/Validate Native 4K Clean-Session Preflight Contract")]
    public static void ValidateContractConfigOnly()
    {
        if (!File.Exists(ContractPath))
            throw new FileNotFoundException("Native-4K clean-session preflight contract missing: " + ContractPath);
        if (!File.Exists(ShadowContractPath))
            throw new FileNotFoundException("Shadow-stability contract missing: " + ShadowContractPath);
        if (!File.Exists(SolarContractPath))
            throw new FileNotFoundException("Solar/shadow capture-coherence contract missing: " + SolarContractPath);

        Contract contract = JsonUtility.FromJson<Contract>(File.ReadAllText(ContractPath));
        if (contract == null || !string.Equals(contract.schemaVersion, "1.0", StringComparison.Ordinal))
            throw new InvalidOperationException("Native-4K clean-session preflight contract is null/unparseable or not schema 1.0.");
        if (!string.Equals(contract.scenePath, ScenePath, StringComparison.Ordinal))
            throw new InvalidOperationException("Native-4K clean-session preflight scene path drifted.");
        if (contract.initialPreflight == null ||
            !string.Equals(contract.initialPreflight.mode, "CONTRACT_ONLY_BEFORE_REBUILD", StringComparison.Ordinal) ||
            !contract.initialPreflight.forbidPreparedSceneStateValidation)
            throw new InvalidOperationException("Initial native-4K preflight must remain contract-only before deterministic scene rebuild.");
        if (contract.postRebuildBaseline == null ||
            !contract.postRebuildBaseline.required ||
            !contract.postRebuildBaseline.mustRunBeforeAnyPostBuildEvidenceMutation ||
            !contract.postRebuildBaseline.mustRunBeforeReflectionRequest)
            throw new InvalidOperationException("Prepared native-4K baseline validation requirements were weakened.");
        if (contract.visualFidelityPointsAwarded != 0 || contract.runtimeRenderVerified)
            throw new InvalidOperationException("Clean-session preflight QA may not award Visual Fidelity points or claim a runtime render.");

        string[] required = contract.requiredContracts ?? Array.Empty<string>();
        if (required.Length != 2 ||
            Array.IndexOf(required, ShadowContractPath) < 0 ||
            Array.IndexOf(required, SolarContractPath) < 0)
            throw new InvalidOperationException("Native-4K clean-session preflight must bind both shadow-stability and solar/shadow contracts.");

        // Config-only validation must never open, rebuild or judge the scene. It is safe on a clean runner
        // whose global QualitySettings have not yet been deterministically established by the benchmark.
        QualityBlockSolarShadowCaptureCoherenceQA.ValidateContractConfigOnly();
    }

    /// <summary>
    /// Called immediately after the deterministic benchmark rebuild/reopen, before any post-build
    /// evidence mutation. From this point onward scene state is authoritative and must satisfy the
    /// native-4K shadow/camera/LOD and physical solar contracts.
    /// </summary>
    public static void ValidatePreparedBaseline()
    {
        ValidateContractConfigOnly();

        if (!EditorSceneManager.GetActiveScene().IsValid() ||
            !string.Equals(EditorSceneManager.GetActiveScene().path, ScenePath, StringComparison.Ordinal))
            throw new InvalidOperationException("Prepared native-4K baseline requires the persisted benchmark scene: " + ScenePath);

        QualityBlockShadowStabilityUpgrade.ValidateOpenScene();
        QualityBlockSolarShadowCaptureCoherenceQA.ValidateOpenScene();

        Debug.Log(
            "Native-4K clean-session baseline valid: contract-only preflight preceded the deterministic rebuild, then the prepared scene proved the intended shadow/MSAA/LOD and physical solar state. Visual Fidelity remains UNSCORED until real pixels are reviewed.");
    }

    [Serializable]
    private sealed class Contract
    {
        public string schemaVersion;
        public string scenePath;
        public InitialPreflight initialPreflight;
        public PostRebuildBaseline postRebuildBaseline;
        public string[] requiredContracts;
        public int visualFidelityPointsAwarded;
        public bool runtimeRenderVerified;
    }

    [Serializable]
    private sealed class InitialPreflight
    {
        public string mode;
        public bool forbidPreparedSceneStateValidation;
    }

    [Serializable]
    private sealed class PostRebuildBaseline
    {
        public bool required;
        public bool mustRunBeforeAnyPostBuildEvidenceMutation;
        public bool mustRunBeforeReflectionRequest;
    }
}
