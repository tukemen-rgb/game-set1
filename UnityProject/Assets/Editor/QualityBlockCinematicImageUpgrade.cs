using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Applies and validates the deterministic cinematic display transform used by both gameplay and
/// native 4K evidence capture. The pass is intentionally global and restrained: it may map scene
/// dynamic range to the display, but it may not conceal defects with bloom, sharpening or local FX.
/// </summary>
public static class QualityBlockCinematicImageUpgrade
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ShaderName = "Hidden/NewTown/FilmicTonemap";
    private const float ExposureEV = -0.45f;
    private const float Contrast = 1.03f;
    private const float Saturation = 0.97f;
    private const float ShadowSoftening = 0.025f;

    [MenuItem("NewTown/Lighting/Build Cinematic Exposure + Filmic Tonemap")]
    public static void BuildAndApply()
    {
        EnsureSceneOpen();
        ApplyToOpenScene();
        ValidateOpenScene();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Cinematic exposure/filmic display transform configured. Highlight retention and shadow separation remain actual-render verification pending.");
    }

    public static void ApplyToOpenScene()
    {
        EnsureSceneOpen();

        if (GraphicsSettings.currentRenderPipeline != null)
            throw new InvalidOperationException("QualityBlockFilmicTonemap uses Built-in Render Pipeline OnRenderImage; SRP is not supported by this benchmark pass.");

        // Scene-linear PBR and tonemapping require a linear-light project. This is a project-level
        // physical-plausibility requirement, not an artistic grade preference.
        if (PlayerSettings.colorSpace != ColorSpace.Linear)
            PlayerSettings.colorSpace = ColorSpace.Linear;

        Camera camera = Camera.main;
        if (camera == null)
            throw new InvalidOperationException("MainCamera missing before cinematic image pass.");

        Shader shader = Shader.Find(ShaderName);
        if (shader == null || !shader.isSupported)
            throw new InvalidOperationException($"Required filmic shader is missing or unsupported: {ShaderName}");

        QualityBlockFilmicTonemap[] existing = camera.GetComponents<QualityBlockFilmicTonemap>();
        QualityBlockFilmicTonemap tonemap = existing.FirstOrDefault();
        if (tonemap == null)
            tonemap = camera.gameObject.AddComponent<QualityBlockFilmicTonemap>();

        for (int i = existing.Length - 1; i >= 1; --i)
            UnityEngine.Object.DestroyImmediate(existing[i]);

        tonemap.Configure(shader, ExposureEV, Contrast, Saturation, ShadowSoftening);
        tonemap.enabled = true;

        // Formal evidence owns one explicit Built-in Forward perspective camera. Leaving renderingPath
        // at UsePlayerSettings would allow a project-level setting to switch the benchmark path without
        // changing this scene pass; a partial viewport could likewise hide edge defects outside the crop.
        camera.renderingPath = RenderingPath.Forward;
        camera.orthographic = false;
        camera.rect = new Rect(0f, 0f, 1f, 1f);
        camera.allowHDR = true;
        camera.allowMSAA = true;
        camera.allowDynamicResolution = false;
        camera.clearFlags = CameraClearFlags.Skybox;

        // Texture-angle clarity is a large 4K readability contributor, especially paving, walls and
        // grazing-angle ground. Force anisotropic sampling globally; do not add post sharpening.
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;

        // Edge/shadow stability is part of the same deterministic camera-quality contract. Applying
        // it here guarantees the environment-lighting and 4K capture paths cannot silently bypass
        // the high-density cascade/MSAA/LOD configuration.
        QualityBlockShadowStabilityUpgrade.ApplyToOpenScene();

        EditorUtility.SetDirty(camera);
        EditorUtility.SetDirty(tonemap);
    }

    [MenuItem("NewTown/QA/Validate Cinematic Exposure + Filmic Tonemap")]
    public static void ValidateOpenScene()
    {
        EnsureSceneOpen();

        if (GraphicsSettings.currentRenderPipeline != null)
            throw new InvalidOperationException("Visual-fidelity benchmark expects the Built-in Render Pipeline for its deterministic filmic pass.");
        if (PlayerSettings.colorSpace != ColorSpace.Linear)
            throw new InvalidOperationException("Project color space must be Linear for physically plausible PBR/exposure.");

        Camera camera = Camera.main;
        if (camera == null)
            throw new InvalidOperationException("MainCamera missing from cinematic-image QA.");
        if (camera.renderingPath != RenderingPath.Forward)
            throw new InvalidOperationException("MainCamera renderingPath must remain explicitly Forward for benchmark evidence.");
        if (camera.orthographic)
            throw new InvalidOperationException("MainCamera must remain perspective for benchmark evidence.");
        if (Mathf.Abs(camera.rect.x) > 0.0001f || Mathf.Abs(camera.rect.y) > 0.0001f ||
            Mathf.Abs(camera.rect.width - 1f) > 0.0001f || Mathf.Abs(camera.rect.height - 1f) > 0.0001f)
            throw new InvalidOperationException("MainCamera viewport must remain the full 0,0,1,1 frame for benchmark evidence.");
        if (!camera.allowHDR)
            throw new InvalidOperationException("HDR camera path is required before filmic highlight compression.");
        if (!camera.allowMSAA)
            throw new InvalidOperationException("MainCamera MSAA permission is disabled.");
        if (camera.allowDynamicResolution)
            throw new InvalidOperationException("Dynamic resolution is forbidden for benchmark evidence because it breaks pixel-exact 4K comparison.");
        if (camera.clearFlags != CameraClearFlags.Skybox)
            throw new InvalidOperationException("MainCamera must clear from the coherent physical skybox for benchmark evidence.");
        if (QualitySettings.anisotropicFiltering != AnisotropicFiltering.ForceEnable)
            throw new InvalidOperationException("Anisotropic filtering must be forced for grazing-angle 4K material fidelity.");

        QualityBlockFilmicTonemap[] effects = camera.GetComponents<QualityBlockFilmicTonemap>();
        if (effects.Length != 1)
            throw new InvalidOperationException($"Expected exactly one deterministic filmic tonemap on MainCamera; found {effects.Length}.");

        QualityBlockFilmicTonemap effect = effects[0];
        if (!effect.enabled || effect.FilmicShader == null || effect.FilmicShader.name != ShaderName || !effect.FilmicShader.isSupported)
            throw new InvalidOperationException("Filmic tonemap component/shader is missing, disabled or unsupported.");
        if (Mathf.Abs(effect.ExposureEV - ExposureEV) > 0.001f)
            throw new InvalidOperationException("Benchmark exposure EV drifted from the fixed cross-view value.");
        if (Mathf.Abs(effect.Contrast - Contrast) > 0.001f ||
            Mathf.Abs(effect.Saturation - Saturation) > 0.001f ||
            Mathf.Abs(effect.ShadowSoftening - ShadowSoftening) > 0.001f)
            throw new InvalidOperationException("Filmic display-transform parameters drifted from the benchmark contract.");

        // Source preflight also proves there is no second enabled OnRenderImage effect or camera command
        // buffer that could bloom/sharpen/composite defects after this deterministic filmic transform.
        // The purity QA additionally watches every canonical 4K Camera.Render at pre-cull/pre-render/post-render.
        QualityBlockCameraEvidencePurityQA.ValidateOpenScene();
        QualityBlockShadowStabilityUpgrade.ValidateOpenScene();

        Debug.Log("Cinematic image QA valid: Built-in Forward perspective camera, full viewport, physical sky clear, linear-light project, HDR camera, fixed exposure, deterministic filmic shoulder/toe, forced anisotropy, no hidden camera post effect/command buffer, no dynamic resolution, and shadow/edge stability contract enforced.");
    }

    private static void EnsureSceneOpen()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }
}
