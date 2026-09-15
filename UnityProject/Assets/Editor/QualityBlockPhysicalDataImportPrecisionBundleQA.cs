using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Binds all subtle physical-data texture import precision gates to the two render boundaries that
/// matter for authoritative evidence: reflection-probe rendering and formal still/temporal cameras.
///
/// Individual domain gates may repair importer state during preparation. This bundle never repairs in
/// a render callback. It validates read-only and fails closed so cubemaps cannot be captured from one
/// texture-import state and final benchmark frames from another.
///
/// Implementation evidence only: this class awards zero Visual Fidelity points without real native-4K
/// rendered evidence.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockPhysicalDataImportPrecisionBundleQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/physical_data_import_precision_bundle_contract.json";

    private static bool validatingRenderBoundary;

    static QualityBlockPhysicalDataImportPrecisionBundleQA()
    {
        Camera.onPreCull -= ValidateBeforeRenderBoundary;
        Camera.onPreCull += ValidateBeforeRenderBoundary;
    }

    [MenuItem("NewTown/Materials/Apply All Physical-Data Import Precision")]
    public static void ApplyAllAndValidate()
    {
        ValidateContractConfigOnly();

        // Explicit preparation is allowed to repair importers. Keep it outside camera callbacks so
        // reflection and formal evidence never mutate their own source state while being rendered.
        QualityBlockFacadeMacroPrecisionUpgrade.ApplyAndValidate();
        QualityBlockBalconyMicrotextureImportPrecisionQA.ApplyAndValidate();
        QualityBlockGroundMicrotextureImportPrecisionQA.ApplyAndValidate();
        QualityBlockFoliageMicrotextureImportPrecisionQA.ApplyAndValidate();
        QualityBlockDetailMaterialMicrotextureImportPrecisionQA.ApplyAndValidate();
        QualityBlockParkFurnitureMicrotextureImportPrecisionQA.ApplyAndValidate();
        QualityBlockGenericPbrMicrotextureImportPrecisionQA.ApplyAndValidate();

        ValidatePreparedStateReadOnly();
        Debug.Log(
            "Physical-data import precision bundle prepared: facade macro plus balcony, ground, foliage, apartment-detail, park-furniture and generic-PBR physical-data textures satisfy their domain gates. " +
            "Reflection/formal render callbacks remain read-only and Visual Fidelity remains UNSCORED.");
    }

    [MenuItem("NewTown/QA/Validate Physical-Data Import Precision Bundle")]
    public static void ValidatePreparedStateReadOnly()
    {
        ValidateContractConfigOnly();

        // These public validators are read-only. Do not replace them with ApplyAndValidate here.
        QualityBlockFacadeMacroPrecisionUpgrade.ValidateFromMenu();
        QualityBlockBalconyMicrotextureImportPrecisionQA.ValidateFromMenu();
        QualityBlockGroundMicrotextureImportPrecisionQA.ValidateFromMenu();
        QualityBlockFoliageMicrotextureImportPrecisionQA.ValidateFromMenu();
        QualityBlockDetailMaterialMicrotextureImportPrecisionQA.ValidateFromMenu();
        QualityBlockParkFurnitureMicrotextureImportPrecisionQA.ValidateFromMenu();
        QualityBlockGenericPbrMicrotextureImportPrecisionQA.ValidateFromMenu();
    }

    public static void ValidateContractConfigOnly()
    {
        if (!File.Exists(ContractPath))
            throw new InvalidOperationException(
                "Missing physical-data import precision bundle contract: " + ContractPath);

        string json = File.ReadAllText(ContractPath);
        string[] required =
        {
            "\"schemaVersion\": \"1.1.0\"",
            "\"contractId\": \"physical-data-import-precision-bundle-v1\"",
            "QualityBlockFacadeMacroPrecisionUpgrade",
            "QualityBlockBalconyMicrotextureImportPrecisionQA",
            "QualityBlockGroundMicrotextureImportPrecisionQA",
            "QualityBlockFoliageMicrotextureImportPrecisionQA",
            "QualityBlockDetailMaterialMicrotextureImportPrecisionQA",
            "QualityBlockParkFurnitureMicrotextureImportPrecisionQA",
            "QualityBlockGenericPbrMicrotextureImportPrecisionQA",
            "\"protectedDomainCount\": 7",
            "\"reflectionCameraType\": \"Reflection\"",
            "\"formalPreCullBehavior\": \"READ_ONLY_FAIL_CLOSED\"",
            "\"reflectionPreCullBehavior\": \"READ_ONLY_FAIL_CLOSED\"",
            "\"repairDuringReflectionOrFormalRender\": false",
            "\"visualFidelityPointsAwarded\": 0",
            "\"passClaimAllowedWithoutRenderedEvidence\": false",
            "\"runtimeRenderVerification\": \"PENDING_UNITY_RUNTIME\""
        };

        for (int i = 0; i < required.Length; i++)
        {
            if (json.IndexOf(required[i], StringComparison.Ordinal) < 0)
                throw new InvalidOperationException(
                    "Physical-data import precision bundle contract missing/changed token: " + required[i]);
        }

        // Bind the bundle contract to every domain contract early. This catches a missing/relaxed
        // domain definition before any reflection or formal camera tries to use the aggregate gate.
        QualityBlockFacadeMacroPrecisionUpgrade.ValidateContractConfigOnly();
        QualityBlockBalconyMicrotextureImportPrecisionQA.ValidateContractConfigOnly();
        QualityBlockGroundMicrotextureImportPrecisionQA.ValidateContractConfigOnly();
        QualityBlockFoliageMicrotextureImportPrecisionQA.ValidateContractConfigOnly();
        QualityBlockDetailMaterialMicrotextureImportPrecisionQA.ValidateContractConfigOnly();
        QualityBlockParkFurnitureMicrotextureImportPrecisionQA.ValidateContractConfigOnly();
        QualityBlockGenericPbrMicrotextureImportPrecisionQA.ValidateContractConfigOnly();
    }

    private static void ValidateBeforeRenderBoundary(Camera camera)
    {
        if (validatingRenderBoundary || camera == null)
            return;

        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || !string.Equals(activeScene.path, ScenePath, StringComparison.Ordinal))
            return;

        bool reflectionBoundary = camera.cameraType == CameraType.Reflection;
        bool formalBoundary = IsFormalEvidenceCamera(camera);
        if (!reflectionBoundary && !formalBoundary)
            return;

        validatingRenderBoundary = true;
        try
        {
            // Strictly read-only. If preparation has not completed, abort the render boundary rather
            // than fixing importers after a cubemap/frame has already begun consuming texture state.
            ValidatePreparedStateReadOnly();
        }
        catch (Exception ex)
        {
            string boundary = reflectionBoundary ? "reflection-probe" : "formal benchmark";
            throw new InvalidOperationException(
                "Physical-data import precision bundle blocked a " + boundary +
                " render because protected texture import state is not canonical. " +
                "Run preparation before evidence rendering; do not repair in-flight.", ex);
        }
        finally
        {
            validatingRenderBoundary = false;
        }
    }

    private static bool IsFormalEvidenceCamera(Camera camera)
    {
        if (camera.targetTexture == null)
            return false;

        string targetName = camera.targetTexture.name ?? string.Empty;
        return targetName.StartsWith("QA4K_", StringComparison.Ordinal) ||
               targetName.StartsWith("QATemporal_", StringComparison.Ordinal) ||
               targetName.StartsWith("QAPreparedTemporal_", StringComparison.Ordinal);
    }
}
