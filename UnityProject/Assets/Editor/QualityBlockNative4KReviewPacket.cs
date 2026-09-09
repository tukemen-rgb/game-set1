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
        QualityBlockStructuralSurfaceSaveGate.ValidateContract();
        QualityBlockSceneMaterialPhysicalityUpgrade.ValidateContract();
        QualityBlockSceneMetadataCoverageQA.ValidateContractConfigOnly();
        QualityBlockTextureSamplingUpgrade.ValidateContractConfigOnly();

        // Still path: rebuilds the scored benchmark, refreshes realtime probes, captures native 4K,
        // creates pixel-exact crops, seals SHA-256 provenance, and writes the bound review template.
        // Quality-scene save gates also replace retained structural Cube/Cylinder renderer meshes,
        // preserve gameplay collision footprints, bind actual hero materials to construction-physical
        // metallic/specular families, normalize generated map registration + 4K texture sampling,
        // and reject any active renderer outside registered manufacture/installation/material metadata
        // domains before the scored scene is persisted for capture.
        QualityBlockRenderEvidenceProvenanceQA.CaptureAndSealNative4KEvidence();
        QualityBlockStructuralSurfaceRefinement.ValidateOpenScene();
        QualityBlockSceneMaterialPhysicalityUpgrade.ValidateOpenScene();
        QualityBlockSceneMetadataCoverageQA.ValidateOpenScene();
        QualityBlockTextureSamplingUpgrade.ValidateOpenScene();

        // Objective triage only. This cannot award Cinematic Image or Lighting points.
        QualityBlockRenderedImageDiagnostics.AnalyzeExistingCapture();

        // Motion path: native-4K subpixel grazing and LOD-walk sequences with sealed manifests.
        QualityBlockTemporalStabilityCapture.CaptureAndSeal();

        AssetDatabase.Refresh();
        Debug.Log(
            "Complete native-4K review packet prepared: sealed hero/oblique/grazing stills + 100% crops, " +
            "validated retained structural geometry, actual material physicality bindings, scene-wide construction/material metadata coverage, " +
            "generated map registration + mipmapped trilinear anisotropic texture sampling, cinematic display diagnostics, and sealed temporal probes. " +
            "Visual Fidelity remains UNSCORED until the exact evidence is reviewed and the evidence-bound 100-point gate is evaluated.");
    }
}
