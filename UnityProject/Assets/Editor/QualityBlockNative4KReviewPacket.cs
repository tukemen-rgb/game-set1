using UnityEditor;
using UnityEngine;

/// <summary>
/// Single runner entry point for the first real Unity verification session. Once a Unity editor/runner
/// is available, this command should be preferred over speculative source expansion: it captures and
/// seals the still evidence, generates objective display diagnostics, then captures/seals the temporal
/// shimmer/LOD probes. It intentionally stops before Visual Fidelity scoring because a reviewer must
/// inspect the actual pixels and record evidence/deductions/corrective actions.
/// </summary>
public static class QualityBlockNative4KReviewPacket
{
    [MenuItem("NewTown/QA/Prepare Complete Native 4K Review Packet")]
    public static void Prepare()
    {
        QualityBlockVisualFidelityGate.ValidateGateConfig();
        QualityBlockShadowStabilityUpgrade.ValidateOpenScene();
        QualityBlockRenderedImageDiagnostics.ValidateContractConfigOnly();
        QualityBlockTemporalStabilityCapture.ValidateContractConfigOnly();

        // Still path: rebuilds the scored benchmark, refreshes realtime probes, captures native 4K,
        // creates pixel-exact crops, seals SHA-256 provenance, and writes the bound review template.
        QualityBlockRenderEvidenceProvenanceQA.CaptureAndSealNative4KEvidence();

        // Objective triage only. This cannot award Cinematic Image or Lighting points.
        QualityBlockRenderedImageDiagnostics.AnalyzeExistingCapture();

        // Motion path: native-4K subpixel grazing and LOD-walk sequences with sealed manifests.
        QualityBlockTemporalStabilityCapture.CaptureAndSeal();

        AssetDatabase.Refresh();
        Debug.Log(
            "Complete native-4K review packet prepared: sealed hero/oblique/grazing stills + 100% crops, " +
            "cinematic display diagnostics, and sealed temporal probes. Visual Fidelity remains UNSCORED " +
            "until the exact evidence is reviewed and the evidence-bound 100-point gate is evaluated.");
    }
}
