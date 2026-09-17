using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Refines the source-anchored facade weathering meshes with a dry, optically thin dielectric
/// residue shader. The parent weathering pass owns causal geometry. This pass owns only optical
/// density, edge feathering and live angular response so a hard alpha ribbon/ellipse cannot masquerade
/// as natural weathering in native 4K evidence.
///
/// This is implementation/readiness QA only. It cannot award Visual Fidelity points or clear any
/// critical defect without sealed native 3840x2160 pixel evidence.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockFacadeWeatheringOpticalRefinementQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string RootName = "FacadeWeatheringDetail";
    private const string ContractPath = "Assets/QA/facade_weathering_optical_refinement_contract.json";
    private const string LookdevPath = "Assets/QA/Lookdev/facade_weathering_optical_feathering.svg";
    private const string ShaderAssetPath = "Assets/Shaders/NewTownWeatheringResidue.shader";
    private const string ShaderName = "NewTown/QualityBlockWeatheringResidue";

    private sealed class Profile
    {
        public string Group;
        public string Material;
        public Color Tint;
        public float Opacity;
        public float Roughness;
        public float Mode;
        public float EdgeFeatherU;
        public float StartFeatherV;
        public float EndFeatherV;
        public float EllipseCore;
        public float MicroBreakup;
    }

    private static readonly Profile[] Profiles =
    {
        new Profile
        {
            Group = "Weathering_GradeSplash", Material = "MAT_GradeSplashSoil",
            Tint = new Color(0.18f, 0.135f, 0.082f, 1f), Opacity = 0.135f, Roughness = 0.95f,
            Mode = 1f, EdgeFeatherU = 0.012f, StartFeatherV = 0f, EndFeatherV = 0.35f,
            EllipseCore = 0.56f, MicroBreakup = 0.03f
        },
        new Profile
        {
            Group = "Weathering_RainRunoff", Material = "MAT_RainMineralResidue",
            Tint = new Color(0.23f, 0.245f, 0.225f, 1f), Opacity = 0.105f, Roughness = 0.92f,
            Mode = 0f, EdgeFeatherU = 0.22f, StartFeatherV = 0.08f, EndFeatherV = 0.28f,
            EllipseCore = 0.56f, MicroBreakup = 0.035f
        },
        new Profile
        {
            Group = "Weathering_BalconyDrip", Material = "MAT_RainMineralResidue",
            Tint = new Color(0.23f, 0.245f, 0.225f, 1f), Opacity = 0.105f, Roughness = 0.92f,
            Mode = 0f, EdgeFeatherU = 0.22f, StartFeatherV = 0.08f, EndFeatherV = 0.28f,
            EllipseCore = 0.56f, MicroBreakup = 0.035f
        },
        new Profile
        {
            Group = "Weathering_RailRust", Material = "MAT_FerrousRunoffResidue",
            Tint = new Color(0.31f, 0.105f, 0.035f, 1f), Opacity = 0.12f, Roughness = 0.90f,
            Mode = 0f, EdgeFeatherU = 0.22f, StartFeatherV = 0.08f, EndFeatherV = 0.30f,
            EllipseCore = 0.56f, MicroBreakup = 0.04f
        },
        new Profile
        {
            Group = "Weathering_ACResidue", Material = "MAT_CondensateMineralResidue",
            Tint = new Color(0.30f, 0.31f, 0.28f, 1f), Opacity = 0.085f, Roughness = 0.94f,
            Mode = 2f, EdgeFeatherU = 0.22f, StartFeatherV = 0f, EndFeatherV = 0.28f,
            EllipseCore = 0.56f, MicroBreakup = 0.03f
        }
    };

    static QualityBlockFacadeWeatheringOpticalRefinementQA()
    {
        Camera.onPreCull -= ValidateBeforeBenchmarkCameraCull;
        Camera.onPreCull += ValidateBeforeBenchmarkCameraCull;
    }

    [MenuItem("NewTown/Materials/Apply Facade Weathering Optical Refinement")]
    public static void ApplyAndPersist()
    {
        EnsureBenchmarkScene();
        QualityBlockFacadeWeatheringDetailUpgrade.ApplyToOpenScene();
        ApplyToOpenScene();
        ValidateOpenScene();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            "Facade weathering optical refinement applied: source-anchored residue now uses soft optical-density edges and live dry-dielectric response. " +
            "Visual Fidelity remains UNSCORED until sealed native 4K renders are reviewed.");
    }

    public static void ApplyToOpenScene()
    {
        EnsureBenchmarkScene();
        ValidateContractConfigOnly();

        GameObject root = FindSceneObject(RootName);
        if (root == null)
            throw new InvalidOperationException("FacadeWeatheringDetail is missing. Build source-anchored weathering before optical refinement.");

        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
            throw new InvalidOperationException($"Required weathering residue shader did not compile/load: {ShaderName}");

        foreach (Profile profile in Profiles)
        {
            GameObject group = FindSceneObject(profile.Group);
            if (group == null || !group.transform.IsChildOf(root.transform))
                throw new InvalidOperationException($"Required weathering group missing from {RootName}: {profile.Group}");

            MeshRenderer renderer = group.GetComponent<MeshRenderer>();
            if (renderer == null || renderer.sharedMaterial == null)
                throw new InvalidOperationException($"Weathering group has no material-bearing MeshRenderer: {profile.Group}");
            if (!string.Equals(renderer.sharedMaterial.name, profile.Material, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Weathering group {profile.Group} material changed: expected {profile.Material}, got {renderer.sharedMaterial.name}.");

            ConfigureMaterial(renderer.sharedMaterial, shader, profile);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbesAndSkybox;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            EditorUtility.SetDirty(renderer);
        }
    }

    [MenuItem("NewTown/QA/Validate Facade Weathering Optical Refinement")]
    public static void ValidateOpenScene()
    {
        EnsureBenchmarkScene();
        ValidateContractConfigOnly();
        QualityBlockFacadeWeatheringDetailUpgrade.ValidateOpenScene();

        GameObject root = FindSceneObject(RootName);
        if (root == null)
            throw new InvalidOperationException("FacadeWeatheringDetail is missing during optical QA.");

        var errors = new List<string>();
        MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        if (renderers.Length != Profiles.Length)
            errors.Add($"Expected exactly {Profiles.Length} optically refined weathering renderers, got {renderers.Length}.");

        foreach (Profile expected in Profiles)
        {
            GameObject group = FindSceneObject(expected.Group);
            if (group == null || !group.transform.IsChildOf(root.transform))
            {
                errors.Add($"Missing weathering group {expected.Group}.");
                continue;
            }

            MeshFilter filter = group.GetComponent<MeshFilter>();
            MeshRenderer renderer = group.GetComponent<MeshRenderer>();
            if (filter == null || filter.sharedMesh == null)
            {
                errors.Add($"{expected.Group} has no persistent mesh.");
                continue;
            }
            if (renderer == null || renderer.sharedMaterial == null)
            {
                errors.Add($"{expected.Group} has no material-bearing renderer.");
                continue;
            }

            Mesh mesh = filter.sharedMesh;
            Vector2[] uv = mesh.uv;
            if (uv == null || uv.Length != mesh.vertexCount)
            {
                errors.Add($"{expected.Group} UV0 coverage mismatch: uv={uv?.Length ?? 0}, vertices={mesh.vertexCount}.");
            }
            else if (uv.Length > 0)
            {
                float minU = uv.Min(x => x.x);
                float maxU = uv.Max(x => x.x);
                float minV = uv.Min(x => x.y);
                float maxV = uv.Max(x => x.y);
                if (minU < -0.01f || maxU > 1.01f || minV < -0.01f || maxV > 1.01f)
                    errors.Add($"{expected.Group} UV0 escaped normalized mask domain: U={minU:0.###}..{maxU:0.###}, V={minV:0.###}..{maxV:0.###}.");
                if (minU > 0.02f || maxU < 0.98f || minV > 0.02f || maxV < 0.98f)
                    errors.Add($"{expected.Group} UV0 does not span enough of the 0..1 feathering domain: U={minU:0.###}..{maxU:0.###}, V={minV:0.###}..{maxV:0.###}.");
            }

            Material mat = renderer.sharedMaterial;
            if (!string.Equals(mat.name, expected.Material, StringComparison.Ordinal))
                errors.Add($"{expected.Group} uses {mat.name}; expected {expected.Material}.");
            if (mat.shader == null || !string.Equals(mat.shader.name, ShaderName, StringComparison.Ordinal))
                errors.Add($"{expected.Group} must use shader {ShaderName}; got {mat.shader?.name ?? "<null>"}.");
            ValidateFloat(mat, "_Opacity", expected.Opacity, 0.001f, expected.Group, errors);
            ValidateFloat(mat, "_Roughness", expected.Roughness, 0.001f, expected.Group, errors);
            ValidateFloat(mat, "_DielectricF0", 0.04f, 0.0005f, expected.Group, errors);
            ValidateFloat(mat, "_ProfileMode", expected.Mode, 0.01f, expected.Group, errors);
            ValidateFloat(mat, "_EdgeFeatherU", expected.EdgeFeatherU, 0.001f, expected.Group, errors);
            ValidateFloat(mat, "_StartFeatherV", expected.StartFeatherV, 0.001f, expected.Group, errors);
            ValidateFloat(mat, "_EndFeatherV", expected.EndFeatherV, 0.001f, expected.Group, errors);
            ValidateFloat(mat, "_EllipseCore", expected.EllipseCore, 0.001f, expected.Group, errors);
            ValidateFloat(mat, "_MicroBreakup", expected.MicroBreakup, 0.001f, expected.Group, errors);

            if (mat.HasProperty("_Tint"))
            {
                Color actual = mat.GetColor("_Tint");
                if (!Approximately(actual, expected.Tint, 0.002f))
                    errors.Add($"{expected.Group} residue tint drifted from dry deposit contract: {actual} vs {expected.Tint}.");
            }
            else
            {
                errors.Add($"{expected.Group} material lacks _Tint.");
            }

            if (mat.HasProperty("_Metallic") && mat.GetFloat("_Metallic") > 0.001f)
                errors.Add($"{expected.Group} weathering residue cannot be metallic.");
            if (mat.IsKeywordEnabled("_EMISSION"))
                errors.Add($"{expected.Group} weathering residue cannot emit light.");
            if (mat.renderQueue < (int)RenderQueue.Transparent)
                errors.Add($"{expected.Group} must remain in transparent queue for optically thin blending; renderQueue={mat.renderQueue}.");
            if (renderer.shadowCastingMode != ShadowCastingMode.Off)
                errors.Add($"{expected.Group} must not cast a fictitious solid shadow from an optically thin residue film.");
            if (!renderer.receiveShadows)
                errors.Add($"{expected.Group} must receive live host-surface lighting/shadow response.");
            if (group.GetComponent<Collider>() != null)
                errors.Add($"{expected.Group} weathering must never alter gameplay collision.");
        }

        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Facade weathering optical-refinement QA FAILED:\n - " + string.Join("\n - ", errors));

        Debug.Log(
            "Facade weathering optical-refinement QA passed: five causal residue groups have normalized mask UVs, soft profile parameters, " +
            "high roughness, dielectric F0=0.04, zero emission and no solid shadow casting. This is source/runtime-state QA only; " +
            "native 4K pixel evidence is still required and Visual Fidelity remains UNSCORED.");
    }

    [MenuItem("NewTown/QA/Validate Facade Weathering Optical Contract")]
    public static void ValidateContractConfigOnly()
    {
        if (!File.Exists(ContractPath))
            throw new InvalidOperationException($"Missing required weathering optical contract: {ContractPath}");
        if (!File.Exists(LookdevPath))
            throw new InvalidOperationException($"Missing required weathering optical lookdev: {LookdevPath}");
        if (!File.Exists(ShaderAssetPath))
            throw new InvalidOperationException($"Missing required weathering residue shader source: {ShaderAssetPath}");

        string json = File.ReadAllText(ContractPath);
        string[] requiredTokens =
        {
            "\"contractVersion\": \"facade-weathering-optical-refinement-v1.0.0\"",
            "\"shader\": \"NewTown/QualityBlockWeatheringResidue\"",
            "\"surfaceState\": \"dry\"",
            "\"wetness\": 0.0",
            "\"metallicMustEqual\": 0.0",
            "\"dielectricF0\": 0.04",
            "\"minimumRoughness\": 0.90",
            "\"forbidPaintedOrBakedHighlights\": true",
            "\"runtimeRenderVerification\": \"PENDING_UNITY_RUNTIME\"",
            "\"visualFidelityPointsAwarded\": 0",
            "\"visualFidelityStatus\": \"UNSCORED\""
        };
        foreach (string token in requiredTokens)
            if (json.IndexOf(token, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException($"Facade-weathering optical contract missing required token: {token}");
    }

    private static void ConfigureMaterial(Material mat, Shader shader, Profile profile)
    {
        mat.shader = shader;
        mat.name = profile.Material;
        SetRequiredColor(mat, "_Tint", profile.Tint);
        SetRequiredFloat(mat, "_Opacity", profile.Opacity);
        SetRequiredFloat(mat, "_Roughness", profile.Roughness);
        SetRequiredFloat(mat, "_DielectricF0", 0.04f);
        SetRequiredFloat(mat, "_ProfileMode", profile.Mode);
        SetRequiredFloat(mat, "_EdgeFeatherU", profile.EdgeFeatherU);
        SetRequiredFloat(mat, "_StartFeatherV", profile.StartFeatherV);
        SetRequiredFloat(mat, "_EndFeatherV", profile.EndFeatherV);
        SetRequiredFloat(mat, "_EllipseCore", profile.EllipseCore);
        SetRequiredFloat(mat, "_MicroBreakup", profile.MicroBreakup);
        mat.DisableKeyword("_EMISSION");
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = (int)RenderQueue.Transparent;
        mat.doubleSidedGI = false;
        EditorUtility.SetDirty(mat);
    }

    private static void ValidateFloat(Material mat, string property, float expected, float tolerance,
        string group, List<string> errors)
    {
        if (!mat.HasProperty(property))
        {
            errors.Add($"{group} material lacks required shader property {property}.");
            return;
        }
        float actual = mat.GetFloat(property);
        if (!float.IsFinite(actual) || Mathf.Abs(actual - expected) > tolerance)
            errors.Add($"{group} {property}={actual:0.####}; expected {expected:0.####} ± {tolerance:0.####}.");
    }

    private static void SetRequiredFloat(Material mat, string property, float value)
    {
        if (!mat.HasProperty(property))
            throw new InvalidOperationException($"Weathering shader {mat.shader?.name ?? "<null>"} lacks required property {property}.");
        mat.SetFloat(property, value);
    }

    private static void SetRequiredColor(Material mat, string property, Color value)
    {
        if (!mat.HasProperty(property))
            throw new InvalidOperationException($"Weathering shader {mat.shader?.name ?? "<null>"} lacks required property {property}.");
        mat.SetColor(property, value);
    }

    private static bool Approximately(Color a, Color b, float tolerance)
    {
        return Mathf.Abs(a.r - b.r) <= tolerance && Mathf.Abs(a.g - b.g) <= tolerance &&
               Mathf.Abs(a.b - b.b) <= tolerance && Mathf.Abs(a.a - b.a) <= tolerance;
    }

    private static void ValidateBeforeBenchmarkCameraCull(Camera camera)
    {
        if (camera == null || camera.gameObject == null || !camera.gameObject.scene.IsValid())
            return;
        if (!string.Equals(camera.gameObject.scene.path, ScenePath, StringComparison.Ordinal))
            return;
        if (!string.Equals(camera.name, "MainCamera", StringComparison.Ordinal))
            return;
        if (FindSceneObject(RootName) == null)
            return; // Scene construction is not yet at the facade-weathering stage.

        ValidateOpenScene();
    }

    private static void EnsureBenchmarkScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x.scene.IsValid() && string.Equals(x.name, name, StringComparison.Ordinal));
    }
}
