using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Fail-closed source/runtime-state QA for the generated balcony waterproof microsurface. This validates
/// the actual texture importer state, Standard-material bindings and scene renderer bindings, but awards no
/// Visual Fidelity points. Visible tiling, normal sparkle and temporal shimmer remain pixel-review questions.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockBalconySurfaceMicrostructureQA
{
    private const string ContractPath = "Assets/QA/balcony_surface_microstructure_contract.json";
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";

    private static readonly string[] CanonicalCriticalDefects =
    {
        "baked_or_painted_highlights",
        "impossible_material_physics",
        "obvious_repetition",
        "severe_aliasing_or_shimmer",
        "visible_lod_pop",
        "missing_construction_material_metadata",
        "unverified_render_claim"
    };

    static QualityBlockBalconySurfaceMicrostructureQA()
    {
        Camera.onPreCull -= OnPreCull;
        Camera.onPreCull += OnPreCull;
    }

    [MenuItem("NewTown/QA/Validate Balcony Waterproof Microstructure")]
    public static void ValidateOpenScene()
    {
        ValidateContractConfigOnly();
        ValidateAssetsAndMaterial();

        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
            throw new InvalidOperationException("Balcony microstructure QA requires the benchmark scene to be active.");
        if (QualityBlockBalconyDrainageWaterproofingUpgrade.IsAuthoredDanchiActive(scene)) return;

        // Reuse the construction validator first so microtexture cannot become a substitute for missing macro
        // fall, channel, drain or waterproof geometry.
        QualityBlockBalconyDrainageWaterproofingQA.ValidateOpenScene();

        Material material = AssetDatabase.LoadAssetAtPath<Material>(
            QualityBlockBalconyDrainageWaterproofingUpgrade.WaterproofMaterialPath);
        GameObject root = QualityBlockBalconyDrainageWaterproofingUpgrade.FindSceneObject(
            scene, QualityBlockBalconyDrainageWaterproofingUpgrade.RootName);
        if (root == null) throw new InvalidOperationException("Balcony drainage root missing during microstructure QA.");

        int waterproofBindings = 0;
        int floorBindings = 0;
        foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (renderer == null) continue;
            if (renderer.name == "WaterproofingAssembly" || renderer.name == "FloorFallSurface")
            {
                if (renderer.sharedMaterial != material)
                    throw new InvalidOperationException(renderer.name + " no longer uses the canonical waterproof material.");
                if (renderer.HasPropertyBlock())
                    throw new InvalidOperationException(
                        renderer.name + " uses a MaterialPropertyBlock; formal balcony microsurface state must remain inspectable and canonical.");
                if (renderer.name == "WaterproofingAssembly") waterproofBindings++;
                if (renderer.name == "FloorFallSurface") floorBindings++;
            }
        }

        if (waterproofBindings != 30 || floorBindings != 30)
            throw new InvalidOperationException(
                $"Expected 30 waterproof + 30 floor-fall bindings, got {waterproofBindings} + {floorBindings}.");
    }

    public static void ValidateAssetsAndMaterial()
    {
        ValidateContractConfigOnly();

        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(
            QualityBlockBalconySurfaceMicrostructureUpgrade.NormalTexturePath);
        Texture2D mask = AssetDatabase.LoadAssetAtPath<Texture2D>(
            QualityBlockBalconySurfaceMicrostructureUpgrade.MetallicSmoothnessTexturePath);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(
            QualityBlockBalconyDrainageWaterproofingUpgrade.WaterproofMaterialPath);

        if (normal == null || mask == null || material == null)
            throw new InvalidOperationException("Balcony microstructure texture/material assets are incomplete.");
        if (normal.width != QualityBlockBalconySurfaceMicrostructureUpgrade.TextureSize ||
            normal.height != QualityBlockBalconySurfaceMicrostructureUpgrade.TextureSize ||
            mask.width != QualityBlockBalconySurfaceMicrostructureUpgrade.TextureSize ||
            mask.height != QualityBlockBalconySurfaceMicrostructureUpgrade.TextureSize)
            throw new InvalidOperationException("Balcony microstructure textures must remain 1024x1024.");

        ValidateImporter(QualityBlockBalconySurfaceMicrostructureUpgrade.NormalTexturePath, true);
        ValidateImporter(QualityBlockBalconySurfaceMicrostructureUpgrade.MetallicSmoothnessTexturePath, false);

        if (material.shader == null || material.shader.name != "Standard" ||
            material.name != "MAT_BalconyWaterproofingDry")
            throw new InvalidOperationException("Balcony waterproof microstructure material identity drifted.");
        if (ColorDistance(material.color, new Color(0.245f, 0.255f, 0.245f, 1f)) > 0.015f)
            throw new InvalidOperationException("Balcony waterproof albedo drifted from the dry neutral contract.");
        if (material.mainTexture != null)
            throw new InvalidOperationException("Balcony waterproof albedo must remain texture-free; do not bake highlights/stains into base colour.");
        if ((material.mainTextureScale - Vector2.one * QualityBlockBalconySurfaceMicrostructureUpgrade.TextureScalePerMeterUv).sqrMagnitude > 0.0001f ||
            material.mainTextureOffset.sqrMagnitude > 0.000001f)
            throw new InvalidOperationException("Balcony waterproof metre-UV microtexture scale/offset drifted.");

        if (!material.HasProperty("_BumpMap") || material.GetTexture("_BumpMap") != normal ||
            !material.IsKeywordEnabled("_NORMALMAP"))
            throw new InvalidOperationException("Balcony waterproof normal-map binding/keyword is missing.");
        if (!material.HasProperty("_BumpScale") ||
            Mathf.Abs(material.GetFloat("_BumpScale") - QualityBlockBalconySurfaceMicrostructureUpgrade.RuntimeNormalScale) > 0.005f)
            throw new InvalidOperationException("Balcony waterproof normal scale drifted from the conservative contract.");

        if (!material.HasProperty("_MetallicGlossMap") || material.GetTexture("_MetallicGlossMap") != mask ||
            !material.IsKeywordEnabled("_METALLICGLOSSMAP"))
            throw new InvalidOperationException("Balcony waterproof metallic/smoothness map binding/keyword is missing.");
        if (!material.HasProperty("_Metallic") || Mathf.Abs(material.GetFloat("_Metallic")) > 0.001f)
            throw new InvalidOperationException("Balcony waterproof topcoat must remain dielectric metallic=0.");
        if (!material.HasProperty("_Glossiness") ||
            Mathf.Abs(material.GetFloat("_Glossiness") - QualityBlockBalconySurfaceMicrostructureUpgrade.BaseSmoothness) > 0.01f)
            throw new InvalidOperationException("Balcony waterproof base smoothness drifted.");
        if (!material.HasProperty("_GlossMapScale") || Mathf.Abs(material.GetFloat("_GlossMapScale") - 1f) > 0.001f)
            throw new InvalidOperationException("Balcony waterproof smoothness-map scale must remain 1.0.");
        if (material.IsKeywordEnabled("_EMISSION") ||
            (material.HasProperty("_EmissionColor") && material.GetColor("_EmissionColor").maxColorComponent > 0.001f))
            throw new InvalidOperationException("Balcony waterproof material may not fake micro-highlights using emission.");
    }

    [MenuItem("NewTown/QA/Validate Balcony Waterproof Microstructure Contract")]
    public static void ValidateContractConfigOnly()
    {
        if (!File.Exists(ContractPath))
            throw new FileNotFoundException("Missing balcony microstructure contract: " + ContractPath);
        Contract contract = JsonUtility.FromJson<Contract>(File.ReadAllText(ContractPath));
        if (contract == null || contract.schemaVersion != "1.0" ||
            contract.material == null || contract.sampling == null || contract.formalEvidencePolicy == null)
            throw new InvalidOperationException("Balcony microstructure contract is null/unparseable/incomplete.");

        if (contract.sampling.textureSize != QualityBlockBalconySurfaceMicrostructureUpgrade.TextureSize ||
            Mathf.Abs(contract.sampling.worldPeriodM - QualityBlockBalconySurfaceMicrostructureUpgrade.TextureWorldPeriodM) > 0.0001f ||
            Mathf.Abs(contract.sampling.uvScaleForMeterBasedUv - QualityBlockBalconySurfaceMicrostructureUpgrade.TextureScalePerMeterUv) > 0.0001f ||
            !contract.sampling.metricUvInputRequired || !contract.sampling.tileablePeriodicGeneration ||
            !contract.sampling.mipmapsRequired || !contract.sampling.trilinearRequired ||
            contract.sampling.minimumAnisotropy < 8 || !contract.sampling.normalMapImporterRequired ||
            !contract.sampling.linearDataRequired || !contract.sampling.metallicChannelMustRemainZero ||
            !contract.sampling.materialPropertyBlockOverridesForbidden)
            throw new InvalidOperationException("Balcony microstructure sampling contract was weakened or drifted.");

        if (Mathf.Abs(contract.material.metallic) > 0.0001f ||
            Mathf.Abs(contract.material.wetness) > 0.0001f ||
            Mathf.Abs(contract.material.baseSmoothness - QualityBlockBalconySurfaceMicrostructureUpgrade.BaseSmoothness) > 0.0001f ||
            Mathf.Abs(contract.material.runtimeNormalScale - QualityBlockBalconySurfaceMicrostructureUpgrade.RuntimeNormalScale) > 0.0001f ||
            contract.material.smoothnessRange == null || contract.material.smoothnessRange.Length != 2 ||
            Mathf.Abs(contract.material.smoothnessRange[0] - QualityBlockBalconySurfaceMicrostructureUpgrade.SmoothnessMin) > 0.0001f ||
            Mathf.Abs(contract.material.smoothnessRange[1] - QualityBlockBalconySurfaceMicrostructureUpgrade.SmoothnessMax) > 0.0001f)
            throw new InvalidOperationException("Balcony microstructure material physics drifted from source constants.");

        if (contract.formalEvidencePolicy.automaticVisualPoints != 0 ||
            contract.formalEvidencePolicy.runtimeRenderVerified)
            throw new InvalidOperationException("Balcony microstructure contract may not award points or claim runtime verification.");
        RequireExactSet(contract.criticalDefects, CanonicalCriticalDefects, "criticalDefects");
    }

    private static void ValidateImporter(string path, bool normalMap)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Missing TextureImporter for " + path);
        if (importer.textureType != (normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default))
            throw new InvalidOperationException(path + " texture type drifted.");
        if (importer.sRGBTexture)
            throw new InvalidOperationException(path + " is physical data and must remain linear, not sRGB.");
        if (importer.wrapMode != TextureWrapMode.Repeat || importer.filterMode != FilterMode.Trilinear ||
            !importer.mipmapEnabled || importer.anisoLevel < 8)
            throw new InvalidOperationException(path + " sampling must remain repeat + trilinear + mipmapped + anisotropy>=8.");
    }

    private static void OnPreCull(Camera camera)
    {
        if (camera == null || camera.gameObject == null || !camera.gameObject.scene.IsValid()) return;
        if (camera.gameObject.scene.path != ScenePath || camera.name != "MainCamera") return;
        if (QualityBlockBalconyDrainageWaterproofingUpgrade.IsAuthoredDanchiActive(camera.gameObject.scene)) return;
        try { ValidateOpenScene(); }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Formal benchmark render blocked by balcony waterproof microstructure QA: " + ex.Message, ex);
        }
    }

    private static float ColorDistance(Color a, Color b)
    {
        Vector4 d = (Vector4)(a - b);
        return d.magnitude;
    }

    private static void RequireExactSet(string[] actual, string[] expected, string label)
    {
        if (actual == null || actual.Any(string.IsNullOrWhiteSpace) ||
            actual.Distinct(StringComparer.Ordinal).Count() != actual.Length ||
            !new HashSet<string>(actual, StringComparer.Ordinal).SetEquals(expected))
            throw new InvalidOperationException(label + " must exactly match canonical Visual Fidelity Gate defect IDs.");
    }

    [Serializable]
    private sealed class Contract
    {
        public string schemaVersion;
        public MaterialContract material;
        public Sampling sampling;
        public FormalEvidencePolicy formalEvidencePolicy;
        public string[] criticalDefects;
    }

    [Serializable]
    private sealed class MaterialContract
    {
        public float metallic;
        public float baseSmoothness;
        public float runtimeNormalScale;
        public float[] smoothnessRange;
        public float wetness;
    }

    [Serializable]
    private sealed class Sampling
    {
        public int textureSize;
        public float worldPeriodM;
        public float uvScaleForMeterBasedUv;
        public bool metricUvInputRequired;
        public bool tileablePeriodicGeneration;
        public bool mipmapsRequired;
        public bool trilinearRequired;
        public int minimumAnisotropy;
        public bool normalMapImporterRequired;
        public bool linearDataRequired;
        public bool metallicChannelMustRemainZero;
        public bool materialPropertyBlockOverridesForbidden;
    }

    [Serializable]
    private sealed class FormalEvidencePolicy
    {
        public int automaticVisualPoints;
        public bool runtimeRenderVerified;
    }
}
