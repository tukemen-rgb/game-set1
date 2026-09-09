using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Binds benchmark-facing construction parts to the material families declared by the lookdev contracts,
/// then validates the actual Unity Material objects used by active renderers. This closes the gap between
/// physically plausible metadata and materially impossible scene values. It is source/runtime QA only and
/// cannot award Visual Fidelity points without native rendered evidence.
/// </summary>
public static class QualityBlockSceneMaterialPhysicalityUpgrade
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/scene_material_physicality_contract.json";
    private const string DetailMaterialRoot = "Assets/Art/GeneratedDetailMaterials";
    private const string FacadeMaterialRoot = "Assets/Art/GeneratedFacadeOptics";

    private const string AluminumPath = DetailMaterialRoot + "/MAT_AgedAluminum.mat";
    private const string GalvanizedPath = DetailMaterialRoot + "/MAT_DarkGalvanizedSteel.mat";
    private const string DownpipePvcPath = DetailMaterialRoot + "/MAT_AgedDownpipePVC.mat";
    private const string FacadeConcretePath = FacadeMaterialRoot + "/MAT_FacadePaintedRC_Main.mat";

    [MenuItem("NewTown/Materials/Apply Scene Material Physicality Bindings")]
    public static void ApplyAndValidate()
    {
        ValidateContract();
        RequireQualityScene();

        Material aluminum = RequireMaterial(AluminumPath);
        Material galvanized = RequireMaterial(GalvanizedPath);
        Material facadeConcrete = RequireMaterial(FacadeConcretePath);
        Material downpipePvc = GetOrCreateDownpipePvc();

        // Keep the generated asset values aligned with the construction registry rather than merely
        // documenting physically plausible ranges that the actual Unity materials do not satisfy.
        SetStandardSurface(aluminum, smoothness: 0.50f, metallic: 0.85f);
        SetStandardSurface(galvanized, smoothness: 0.40f, metallic: 0.88f);
        SetStandardSurface(downpipePvc, smoothness: 0.18f, metallic: 0.0f);

        // The base rails remain benchmark-visible beneath the added anchorage/detail hierarchy.
        // Treat them as the same aged anodized-aluminium extrusion family as the authored rail detail.
        RebindPrefix("RailTop_", aluminum);
        RebindPrefix("Rail_", aluminum);

        // The legacy full-height cylinder is the visible pipe body. Use a dedicated aged PVC-U material;
        // the separate HD clamps/bolts remain galvanized metal. This avoids the previous concrete material
        // on the pipe without falsely turning the entire drainage assembly into metal.
        RebindExact("RainGutter", downpipePvc);

        // This part extends the reinforced-concrete balcony slab silhouette; it must read as coated RC,
        // not as anodized aluminium. Edge highlight comes from physical geometry and rough dielectric PBR.
        RebindPrefix("HD_BalconySlabLip", facadeConcrete);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        ValidateOpenScene();
        Debug.Log(
            "Scene material physicality bindings applied and validated. Actual angular response remains unscored until sealed native 4K evidence is reviewed.");
    }

    [MenuItem("NewTown/QA/Validate Scene Material Physicality")]
    public static void ValidateOpenScene()
    {
        ValidateContract();
        RequireQualityScene();

        var errors = new List<string>();
        Material aluminum = LoadRuleMaterial(AluminumPath, errors);
        Material galvanized = LoadRuleMaterial(GalvanizedPath, errors);
        Material downpipePvc = LoadRuleMaterial(DownpipePvcPath, errors);
        Material facadeConcrete = LoadRuleMaterial(FacadeConcretePath, errors);

        ValidateSurfaceRange(aluminum, AluminumPath, 0.70f, 1.00f, 0.42f, 0.62f, errors);
        ValidateSurfaceRange(galvanized, GalvanizedPath, 0.75f, 1.00f, 0.28f, 0.52f, errors);
        ValidateSurfaceRange(downpipePvc, DownpipePvcPath, 0.00f, 0.05f, 0.08f, 0.35f, errors);
        ValidateSurfaceRange(facadeConcrete, FacadeConcretePath, 0.00f, 0.05f, 0.00f, 0.30f, errors);

        if (aluminum != null)
        {
            ValidateBinding("RailTop_", aluminum, minimumCount: 30, errors);
            ValidateBinding("Rail_", aluminum, minimumCount: 210, errors);
            ValidateBinding("HD_WindowFrame_", aluminum, minimumCount: 120, errors);
            ValidateBinding("HD_WindowSashStile_", aluminum, minimumCount: 60, errors);
            ValidateBinding("HD_WindowSillDrip", aluminum, minimumCount: 30, errors);
            ValidateBinding("HD_RailMid", aluminum, minimumCount: 30, errors);
            ValidateBinding("HD_RailLower", aluminum, minimumCount: 30, errors);
        }

        if (galvanized != null)
        {
            ValidateBinding("HD_RailBasePlate_", galvanized, minimumCount: 210, errors);
            ValidateBinding("HD_RailBolt_", galvanized, minimumCount: 420, errors);
            ValidateBinding("HD_DownpipeClamp_", galvanized, minimumCount: 8, errors);
            ValidateBinding("HD_DownpipeClampBolt_", galvanized, minimumCount: 8, errors);
        }

        if (downpipePvc != null)
            ValidateExactBinding("RainGutter", downpipePvc, errors);
        if (facadeConcrete != null)
            ValidateBinding("HD_BalconySlabLip", facadeConcrete, minimumCount: 30, errors);

        ValidateAllActiveRendererMaterials(errors);

        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Scene material physicality QA FAILED:\n - " + string.Join("\n - ", errors));

        Debug.Log(
            "Scene material physicality QA passed for actual active Unity materials and hero construction bindings. Native 4K render review is still required.");
    }

    [MenuItem("NewTown/QA/Validate Scene Material Physicality Contract")]
    public static void ValidateContract()
    {
        if (!File.Exists(ContractPath))
            throw new InvalidOperationException($"Missing required material/construction metadata: {ContractPath}");

        string json = File.ReadAllText(ContractPath);
        string[] requiredTokens =
        {
            "\"criticalDefectRiskReduced\": \"materially_impossible_metallic_specular_values\"",
            "\"visualFidelityPointsAwarded\": 0",
            "\"MAT_AgedAluminum.mat\"",
            "\"MAT_DarkGalvanizedSteel.mat\"",
            "\"MAT_AgedDownpipePVC.mat\"",
            "\"MAT_FacadePaintedRC_Main.mat\"",
            "\"RailTop_*\"",
            "\"RainGutter\"",
            "\"HD_BalconySlabLip\"",
            "\"paintedOrBakedHighlightsForbidden\": true",
            "\"activeEmissionForbidden\": true",
            "\"runtimeRenderVerification\": \"PENDING_UNITY_RUNTIME\""
        };

        foreach (string token in requiredTokens)
            if (json.IndexOf(token, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException($"Scene material physicality contract missing required token: {token}");
    }

    private static Material GetOrCreateDownpipePvc()
    {
        Directory.CreateDirectory(DetailMaterialRoot);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(DownpipePvcPath);
        Shader shader = Shader.Find("Standard");
        if (shader == null)
            throw new InvalidOperationException("Standard shader not found for downpipe PVC fallback.");

        if (material == null)
        {
            material = new Material(shader) { name = "MAT_AgedDownpipePVC" };
            AssetDatabase.CreateAsset(material, DownpipePvcPath);
        }
        else
        {
            material.shader = shader;
        }

        // Medium gray weathered PVC-U: dark enough to separate from the facade, but not a black void.
        // Macro runoff/chalking remains in the causal weathering system, not baked into this base color.
        material.color = new Color(0.31f, 0.315f, 0.30f, 1f);
        material.DisableKeyword("_EMISSION");
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void SetStandardSurface(Material material, float smoothness, float metallic)
    {
        if (material == null || material.shader == null || material.shader.name != "Standard")
            throw new InvalidOperationException($"Expected Standard material, got {material?.name ?? "<null>"}.");
        material.SetFloat("_Glossiness", smoothness);
        material.SetFloat("_Metallic", metallic);
        material.DisableKeyword("_EMISSION");
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
        EditorUtility.SetDirty(material);
    }

    private static void RebindExact(string objectName, Material material)
    {
        Renderer renderer = ActiveRenderers().FirstOrDefault(x => x.gameObject.name == objectName);
        if (renderer == null)
            throw new InvalidOperationException($"Required benchmark renderer missing: {objectName}");
        renderer.sharedMaterial = material;
        EditorUtility.SetDirty(renderer);
    }

    private static void RebindPrefix(string prefix, Material material)
    {
        Renderer[] renderers = ActiveRenderers()
            .Where(x => x.gameObject.name.StartsWith(prefix, StringComparison.Ordinal))
            .ToArray();
        if (renderers.Length == 0)
            throw new InvalidOperationException($"No active renderer found for material binding prefix: {prefix}");
        foreach (Renderer renderer in renderers)
        {
            renderer.sharedMaterial = material;
            EditorUtility.SetDirty(renderer);
        }
    }

    private static void ValidateBinding(string prefix, Material expected, int minimumCount, List<string> errors)
    {
        Renderer[] renderers = ActiveRenderers()
            .Where(x => x.gameObject.name.StartsWith(prefix, StringComparison.Ordinal))
            .ToArray();
        if (renderers.Length < minimumCount)
        {
            errors.Add($"Material binding coverage for '{prefix}' is too low: {renderers.Length} < {minimumCount}.");
            return;
        }
        foreach (Renderer renderer in renderers)
        {
            if (renderer.sharedMaterial != expected)
                errors.Add($"{renderer.gameObject.name} uses '{renderer.sharedMaterial?.name ?? "<null>"}' instead of required '{expected.name}'.");
        }
    }

    private static void ValidateExactBinding(string objectName, Material expected, List<string> errors)
    {
        Renderer[] renderers = ActiveRenderers().Where(x => x.gameObject.name == objectName).ToArray();
        if (renderers.Length != 1)
        {
            errors.Add($"Expected exactly one active renderer named {objectName}, found {renderers.Length}.");
            return;
        }
        if (renderers[0].sharedMaterial != expected)
            errors.Add($"{objectName} uses '{renderers[0].sharedMaterial?.name ?? "<null>"}' instead of required '{expected.name}'.");
    }

    private static void ValidateSurfaceRange(Material material, string path,
        float metallicMin, float metallicMax, float smoothnessMin, float smoothnessMax, List<string> errors)
    {
        if (material == null) return;
        if (material.shader == null || material.shader.name != "Standard")
        {
            errors.Add($"{path} must use Standard shader in the current built-in pipeline fallback.");
            return;
        }

        float metallic = material.GetFloat("_Metallic");
        float smoothness = material.GetFloat("_Glossiness");
        if (!Finite(metallic) || metallic < metallicMin - 0.0001f || metallic > metallicMax + 0.0001f)
            errors.Add($"{path} metallic={metallic:0.###} outside [{metallicMin:0.##}, {metallicMax:0.##}].");
        if (!Finite(smoothness) || smoothness < smoothnessMin - 0.0001f || smoothness > smoothnessMax + 0.0001f)
            errors.Add($"{path} smoothness={smoothness:0.###} outside [{smoothnessMin:0.##}, {smoothnessMax:0.##}].");
        ValidateNoEmission(material, path, errors);
    }

    private static void ValidateAllActiveRendererMaterials(List<string> errors)
    {
        var seen = new HashSet<Material>();
        foreach (Renderer renderer in ActiveRenderers())
        foreach (Material material in renderer.sharedMaterials)
        {
            if (material == null)
            {
                errors.Add($"Active renderer {renderer.gameObject.name} has a null material slot.");
                continue;
            }
            if (!seen.Add(material)) continue;
            if (material.shader == null)
            {
                errors.Add($"Active material {material.name} has no shader.");
                continue;
            }

            if (material.HasProperty("_Metallic"))
            {
                float metallic = material.GetFloat("_Metallic");
                if (!Finite(metallic) || metallic < -0.0001f || metallic > 1.0001f)
                    errors.Add($"Active material {material.name} has invalid metallic={metallic}.");
            }
            if (material.HasProperty("_Glossiness"))
            {
                float smoothness = material.GetFloat("_Glossiness");
                if (!Finite(smoothness) || smoothness < -0.0001f || smoothness > 1.0001f)
                    errors.Add($"Active material {material.name} has invalid smoothness={smoothness}.");
            }
            if (material.HasProperty("_Color"))
            {
                Color c = material.GetColor("_Color");
                if (!Finite(c.r) || !Finite(c.g) || !Finite(c.b) || !Finite(c.a))
                    errors.Add($"Active material {material.name} has non-finite base color.");
            }
            ValidateNoEmission(material, $"active material {material.name}", errors);
        }
    }

    private static void ValidateNoEmission(Material material, string label, List<string> errors)
    {
        if (material == null || !material.HasProperty("_EmissionColor")) return;
        Color e = material.GetColor("_EmissionColor");
        float max = Mathf.Max(e.r, Mathf.Max(e.g, e.b));
        if (!Finite(max) || max > 0.01f)
            errors.Add($"{label} has active emission ({e}); daylight benchmark materials must not self-light.");
    }

    private static IEnumerable<Renderer> ActiveRenderers()
    {
        return Resources.FindObjectsOfTypeAll<Renderer>()
            .Where(x => x != null && x.gameObject.scene.IsValid() && x.enabled && x.gameObject.activeInHierarchy);
    }

    private static Material RequireMaterial(string path)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
            throw new InvalidOperationException($"Required material asset missing: {path}");
        return material;
    }

    private static Material LoadRuleMaterial(string path, List<string> errors)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) errors.Add($"Required physicality material missing: {path}");
        return material;
    }

    private static void RequireQualityScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() ||
            EditorSceneManager.GetActiveScene().path != ScenePath)
            throw new InvalidOperationException($"Open {ScenePath} before applying scene material physicality QA.");
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
