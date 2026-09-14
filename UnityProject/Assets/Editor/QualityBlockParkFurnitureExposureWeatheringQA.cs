using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Applies restrained, cause-based optical zoning to park/street furniture without adding weathering
/// geometry. The pass deliberately reuses the authoritative microdetail textures and changes only small
/// material multipliers on components whose exposure/contact class is unambiguous. This keeps highlights
/// driven by SummerSun/sky/reflections, preserves metallic classes, and avoids turning dirt/wear into a
/// floating decal or a painted lighting cue.
///
/// Source/scene validation is implementation-readiness evidence only. It awards zero Visual Fidelity
/// points until native 3840x2160 stills, 100% crops and temporal evidence are reviewed.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockParkFurnitureExposureWeatheringQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/park_furniture_exposure_weathering_contract.json";
    private const string LookdevPath = "Assets/QA/park_furniture_exposure_weathering_lookdev.svg";
    private const string MaterialRoot = "Assets/Art/GeneratedParkFurnitureMaterials";
    private const string VariantRoot = MaterialRoot + "/Weathering";

    private const string BasePaintName = "PBR_ParkPaintedSteel";
    private const string BaseTimberName = "PBR_BenchTimber";
    private const string UvPaintName = "PBR_ParkPaintedSteel_UVExposed";
    private const string HandPaintName = "PBR_ParkPaintedSteel_HandContact";
    private const string SunTimberName = "PBR_BenchTimber_SunExposed";
    private const string ChuteMaterialName = "PBR_SlideStainless";

    private static readonly Color UvPaintMultiplier = new Color(1.000f, 0.985f, 0.965f, 1f);
    private static readonly Color HandPaintMultiplier = new Color(0.985f, 0.985f, 0.985f, 1f);
    private static readonly Color SunTimberMultiplier = new Color(1.000f, 0.985f, 0.955f, 1f);
    private const float UvPaintGlossScaleMultiplier = 0.88f;
    private const float HandPaintGlossScaleMultiplier = 1.08f;
    private const float SunTimberGlossScaleMultiplier = 0.90f;
    private static bool validatingCamera;

    static QualityBlockParkFurnitureExposureWeatheringQA()
    {
        Camera.onPreCull -= OnCameraPreCull;
        Camera.onPreCull += OnCameraPreCull;
    }

    [MenuItem("NewTown/Quality/Apply Park Furniture Cause-Based Exposure Weathering")]
    public static void ApplyToOpenScene()
    {
        ValidateContractConfigOnly();
        EnsureBenchmarkScene();

        GameObject slide = RequireRoot("HD_Slide");
        GameObject bench = RequireRoot("HD_Bench");
        GameObject notice = RequireRoot("HD_NoticeBoard");
        GameObject lamp = RequireRoot("HD_Lamp");

        int rendererCountBefore = CountRenderers(slide, bench, notice, lamp);
        int colliderCountBefore = CountColliders(slide, bench, notice, lamp);

        Material basePaint = RequireBaseMaterial(BasePaintName);
        Material baseTimber = RequireBaseMaterial(BaseTimberName);
        Material uvPaint = GetOrCreateVariant(
            basePaint, UvPaintName, UvPaintMultiplier, UvPaintGlossScaleMultiplier);
        Material handPaint = GetOrCreateVariant(
            basePaint, HandPaintName, HandPaintMultiplier, HandPaintGlossScaleMultiplier);
        Material sunTimber = GetOrCreateVariant(
            baseTimber, SunTimberName, SunTimberMultiplier, SunTimberGlossScaleMultiplier);

        // Deterministic reset first: a prior interrupted run cannot leave a causal variant on a
        // sheltered or unrelated component. Only the named zones below are re-applied afterwards.
        ResetVariants(slide, basePaint, baseTimber);
        ResetVariants(bench, basePaint, baseTimber);
        ResetVariants(notice, basePaint, baseTimber);

        ApplySlideZones(slide, uvPaint, handPaint);
        ApplyBenchZones(bench, sunTimber);
        ApplyNoticeBoardZones(notice, uvPaint);

        if (CountRenderers(slide, bench, notice, lamp) != rendererCountBefore ||
            CountColliders(slide, bench, notice, lamp) != colliderCountBefore)
            throw new InvalidOperationException(
                "Exposure-weathering pass changed renderer/collider topology. Weathering must remain material-only.");

        AssetDatabase.SaveAssets();
        ValidateOpenScene();
        Debug.Log(
            "Park/street-furniture cause-based exposure weathering applied: UV/rain paint zones, hand-contact rail response, " +
            "sun-exposed timber and sheltered notice-board separation. No geometry was added and Visual Fidelity remains UNSCORED.");
    }

    [MenuItem("NewTown/QA/Validate Park Furniture Cause-Based Exposure Weathering")]
    public static void ValidateOpenScene()
    {
        ValidateContractConfigOnly();
        EnsureBenchmarkScene();

        GameObject slide = RequireRoot("HD_Slide");
        GameObject bench = RequireRoot("HD_Bench");
        GameObject notice = RequireRoot("HD_NoticeBoard");
        GameObject lamp = RequireRoot("HD_Lamp");

        Material basePaint = RequireBaseMaterial(BasePaintName);
        Material baseTimber = RequireBaseMaterial(BaseTimberName);
        Material uvPaint = RequireVariant(UvPaintName);
        Material handPaint = RequireVariant(HandPaintName);
        Material sunTimber = RequireVariant(SunTimberName);

        ValidateVariant(basePaint, uvPaint, UvPaintMultiplier, UvPaintGlossScaleMultiplier, "UV-exposed paint");
        ValidateVariant(basePaint, handPaint, HandPaintMultiplier, HandPaintGlossScaleMultiplier, "hand-contact paint");
        ValidateVariant(baseTimber, sunTimber, SunTimberMultiplier, SunTimberGlossScaleMultiplier, "sun-exposed timber");

        var allowedVariantRenderers = new HashSet<Renderer>();
        ValidateSlideZones(slide, uvPaint, handPaint, allowedVariantRenderers);
        ValidateBenchZones(bench, sunTimber, allowedVariantRenderers);
        ValidateNoticeBoardZones(notice, uvPaint, allowedVariantRenderers);
        ValidateNoVariantLeakage(new[] { slide, bench, notice }, allowedVariantRenderers);
        ValidateProtectedSurfaces(slide, notice, lamp);

        Debug.Log(
            "Park/street-furniture exposure-weathering QA passed at source/scene level: causal component zoning, base-map identity, " +
            "dielectric classes, protected chute/lamp ownership and four-LOD persistence are intact. Render evidence is still required.");
    }

    [MenuItem("NewTown/QA/Validate Park Furniture Exposure Weathering Contract")]
    public static void ValidateContractConfigOnly()
    {
        if (!File.Exists(ContractPath))
            throw new InvalidOperationException($"Missing required exposure-weathering metadata: {ContractPath}");
        if (!File.Exists(LookdevPath))
            throw new InvalidOperationException($"Missing required exposure-weathering lookdev illustration: {LookdevPath}");

        string json = File.ReadAllText(ContractPath);
        string[] tokens =
        {
            "\"id\": \"park_furniture_exposure_weathering\"",
            "\"wetness\": 0.0",
            "\"paint_uv_exposed\"",
            "\"paint_hand_contact\"",
            "\"timber_sun_exposed\"",
            "\"requiredSlideTreadsPerLod\": 10",
            "\"requiredSlideHandContactComponentsPerLod\": 5",
            "\"requiredNoticeExposedComponentsPerLod\": 2",
            "\"preservePhysicalChuteMaterial\": \"PBR_SlideStainless\"",
            "\"requireNoGeneratedRendererOrCollider\": true",
            "\"formalCameraFailClosed\": true",
            "\"reflectionCameraFailClosed\": true",
            "\"visualFidelityPointsAwarded\": 0",
            "PENDING_UNITY_RUNTIME"
        };
        foreach (string token in tokens)
            if (!json.Contains(token, StringComparison.Ordinal))
                throw new InvalidOperationException($"Exposure-weathering contract missing required token: {token}");
    }

    private static void ApplySlideZones(GameObject slide, Material uvPaint, Material handPaint)
    {
        for (int lod = 0; lod < 4; lod++)
        {
            Transform tier = RequireDirectChild(slide.transform, $"LOD{lod}");
            SetMaterial(RequireDirectChild(tier, "Deck"), uvPaint);

            Transform access = RequireDirectChild(tier, "SlideAccessInstallation");
            foreach (Renderer renderer in access.GetComponentsInChildren<Renderer>(true))
            {
                string n = renderer.gameObject.name;
                if (n.StartsWith("AccessTread_", StringComparison.Ordinal))
                    renderer.sharedMaterial = uvPaint;
                else if (IsHandContactName(n))
                    renderer.sharedMaterial = handPaint;
            }

            Transform top = tier.Find("HandrailTop") ?? access.Find("AccessPlatformTransitionTop");
            if (top == null)
                throw new InvalidOperationException($"LOD{lod} has no hand-contact platform top rail.");
            SetMaterial(top, handPaint);
        }
    }

    private static void ApplyBenchZones(GameObject bench, Material sunTimber)
    {
        for (int lod = 0; lod < 4; lod++)
        {
            Transform tier = RequireDirectChild(bench.transform, $"LOD{lod}");
            if (lod <= 1)
            {
                for (int i = 0; i < 5; i++)
                    SetMaterial(RequireDirectChild(tier, $"SeatSlat_{i}"), sunTimber);
            }
            else
            {
                SetMaterial(RequireDirectChild(tier, "SeatProxy"), sunTimber);
            }
        }
    }

    private static void ApplyNoticeBoardZones(GameObject notice, Material uvPaint)
    {
        for (int lod = 0; lod < 4; lod++)
        {
            Transform tier = RequireDirectChild(notice.transform, $"LOD{lod}");
            SetMaterial(RequireDirectChild(tier, "FrameTop"), uvPaint);
            Transform caseRoot = RequireDirectChild(tier, "DisplayCaseConstruction");
            SetMaterial(RequireDirectChild(caseRoot, "DripHood"), uvPaint);
        }
    }

    private static void ValidateSlideZones(
        GameObject slide, Material uvPaint, Material handPaint, HashSet<Renderer> allowed)
    {
        for (int lod = 0; lod < 4; lod++)
        {
            Transform tier = RequireDirectChild(slide.transform, $"LOD{lod}");
            AssertMaterial(RequireRenderer(RequireDirectChild(tier, "Deck")), uvPaint, $"LOD{lod}/Deck", allowed);

            Transform access = RequireDirectChild(tier, "SlideAccessInstallation");
            Renderer[] treads = access.GetComponentsInChildren<Renderer>(true)
                .Where(r => r.gameObject.name.StartsWith("AccessTread_", StringComparison.Ordinal))
                .ToArray();
            if (treads.Length != 10)
                throw new InvalidOperationException($"LOD{lod} must retain 10 UV/rain-exposed slide treads; got {treads.Length}.");
            foreach (Renderer tread in treads)
                AssertMaterial(tread, uvPaint, $"LOD{lod}/{tread.name}", allowed);

            var handRenderers = access.GetComponentsInChildren<Renderer>(true)
                .Where(r => IsHandContactName(r.gameObject.name))
                .ToList();
            Transform directTop = tier.Find("HandrailTop");
            if (directTop != null) handRenderers.Add(RequireRenderer(directTop));
            if (handRenderers.Count != 5)
                throw new InvalidOperationException(
                    $"LOD{lod} must retain exactly 5 hand-contact rail components; got {handRenderers.Count}.");
            foreach (Renderer hand in handRenderers)
                AssertMaterial(hand, handPaint, $"LOD{lod}/{hand.name}", allowed);

            Transform chute = RequireDirectChild(tier, "PhysicalChute");
            Renderer chuteRenderer = RequireRenderer(chute);
            if (chuteRenderer.sharedMaterial == null ||
                !string.Equals(chuteRenderer.sharedMaterial.name, ChuteMaterialName, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"LOD{lod} PhysicalChute material ownership changed; expected {ChuteMaterialName}, got " +
                    $"{(chuteRenderer.sharedMaterial != null ? chuteRenderer.sharedMaterial.name : "<null>")}.");
        }
    }

    private static void ValidateBenchZones(GameObject bench, Material sunTimber, HashSet<Renderer> allowed)
    {
        for (int lod = 0; lod < 4; lod++)
        {
            Transform tier = RequireDirectChild(bench.transform, $"LOD{lod}");
            if (lod <= 1)
            {
                for (int i = 0; i < 5; i++)
                    AssertMaterial(
                        RequireRenderer(RequireDirectChild(tier, $"SeatSlat_{i}")), sunTimber,
                        $"LOD{lod}/SeatSlat_{i}", allowed);
            }
            else
            {
                AssertMaterial(
                    RequireRenderer(RequireDirectChild(tier, "SeatProxy")), sunTimber,
                    $"LOD{lod}/SeatProxy", allowed);
            }

            foreach (string contactName in new[] { "ContactL", "ContactR" })
            {
                Renderer contact = RequireRenderer(RequireDirectChild(tier, contactName));
                if (contact.sharedMaterial == null ||
                    !contact.sharedMaterial.name.Contains("PBR_ParkContactConcrete", StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"LOD{lod}/{contactName} lost its causal grade-contact concrete material.");
            }
        }
    }

    private static void ValidateNoticeBoardZones(GameObject notice, Material uvPaint, HashSet<Renderer> allowed)
    {
        for (int lod = 0; lod < 4; lod++)
        {
            Transform tier = RequireDirectChild(notice.transform, $"LOD{lod}");
            AssertMaterial(
                RequireRenderer(RequireDirectChild(tier, "FrameTop")), uvPaint,
                $"LOD{lod}/FrameTop", allowed);
            Transform caseRoot = RequireDirectChild(tier, "DisplayCaseConstruction");
            AssertMaterial(
                RequireRenderer(RequireDirectChild(caseRoot, "DripHood")), uvPaint,
                $"LOD{lod}/DripHood", allowed);

            Renderer backing = RequireRenderer(RequireDirectChild(tier, "Backing"));
            if (IsWeatheringVariant(backing.sharedMaterial))
                throw new InvalidOperationException($"LOD{lod} sheltered notice backing received exterior weathering.");

            foreach (Renderer r in caseRoot.GetComponentsInChildren<Renderer>(true))
            {
                string n = r.gameObject.name;
                if ((n.StartsWith("Notice_", StringComparison.Ordinal) || n == "ClearAcrylicCover") &&
                    IsWeatheringVariant(r.sharedMaterial))
                    throw new InvalidOperationException($"LOD{lod}/{n} is sheltered/protected and cannot use an exterior weathering variant.");
            }
        }
    }

    private static void ValidateProtectedSurfaces(GameObject slide, GameObject notice, GameObject lamp)
    {
        // The lamp has its own topology-aware lower-pole moisture submesh and diffuser aging system.
        // This pass must not become a second owner of those surfaces.
        foreach (Renderer r in lamp.GetComponentsInChildren<Renderer>(true))
            if (IsWeatheringVariant(r.sharedMaterial))
                throw new InvalidOperationException($"HD_Lamp renderer {r.name} was incorrectly claimed by exposure-weathering variants.");

        for (int lod = 0; lod < 4; lod++)
        {
            Transform caseRoot = RequireDirectChild(
                RequireDirectChild(notice.transform, $"LOD{lod}"), "DisplayCaseConstruction");
            Renderer acrylic = RequireRenderer(RequireDirectChild(caseRoot, "ClearAcrylicCover"));
            if (acrylic.sharedMaterial == null || !acrylic.sharedMaterial.name.Contains("PBR_NoticeClearAcrylic", StringComparison.Ordinal))
                throw new InvalidOperationException($"LOD{lod} notice acrylic material ownership changed unexpectedly.");
        }
    }

    private static void ValidateNoVariantLeakage(IEnumerable<GameObject> roots, HashSet<Renderer> allowed)
    {
        foreach (GameObject root in roots)
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            if (IsWeatheringVariant(renderer.sharedMaterial) && !allowed.Contains(renderer))
                throw new InvalidOperationException(
                    $"Cause-based weathering variant leaked onto an unregistered component: {root.name}/{renderer.name}.");
    }

    private static Material GetOrCreateVariant(
        Material source, string variantName, Color colorMultiplier, float glossScaleMultiplier)
    {
        if (!Directory.Exists(VariantRoot))
        {
            Directory.CreateDirectory(VariantRoot);
            AssetDatabase.Refresh();
        }

        string path = $"{VariantRoot}/{variantName}.mat";
        Material variant = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (variant == null)
        {
            variant = new Material(source) { name = variantName };
            AssetDatabase.CreateAsset(variant, path);
        }
        else
        {
            variant.shader = source.shader;
            variant.CopyPropertiesFromMaterial(source);
            variant.shaderKeywords = source.shaderKeywords;
            variant.renderQueue = source.renderQueue;
            variant.enableInstancing = source.enableInstancing;
            variant.doubleSidedGI = source.doubleSidedGI;
            variant.globalIlluminationFlags = source.globalIlluminationFlags;
            variant.name = variantName;
        }

        if (variant.HasProperty("_Color"))
            variant.SetColor("_Color", Multiply(source.GetColor("_Color"), colorMultiplier));
        if (variant.HasProperty("_GlossMapScale"))
            variant.SetFloat("_GlossMapScale", source.GetFloat("_GlossMapScale") * glossScaleMultiplier);
        if (variant.HasProperty("_Metallic"))
            variant.SetFloat("_Metallic", source.GetFloat("_Metallic"));
        DisableEmission(variant);
        EditorUtility.SetDirty(variant);
        return variant;
    }

    private static void ValidateVariant(
        Material source, Material variant, Color colorMultiplier, float glossScaleMultiplier, string label)
    {
        if (source.shader != variant.shader)
            throw new InvalidOperationException($"{label} changed shader family.");

        foreach (string property in new[] { "_MainTex", "_BumpMap", "_MetallicGlossMap" })
        {
            if (source.HasProperty(property) != variant.HasProperty(property))
                throw new InvalidOperationException($"{label} property availability drift: {property}.");
            if (!source.HasProperty(property)) continue;
            if (source.GetTexture(property) != variant.GetTexture(property))
                throw new InvalidOperationException($"{label} must reuse source texture exactly: {property}.");
            if (!Approximately(source.GetTextureScale(property), variant.GetTextureScale(property), 0.0001f) ||
                !Approximately(source.GetTextureOffset(property), variant.GetTextureOffset(property), 0.0001f))
                throw new InvalidOperationException($"{label} changed physical UV scale/offset for {property}.");
        }

        if (source.HasProperty("_BumpScale") &&
            Mathf.Abs(source.GetFloat("_BumpScale") - variant.GetFloat("_BumpScale")) > 0.0001f)
            throw new InvalidOperationException($"{label} changed source normal scale.");

        float sourceMetallic = source.HasProperty("_Metallic") ? source.GetFloat("_Metallic") : 0f;
        float variantMetallic = variant.HasProperty("_Metallic") ? variant.GetFloat("_Metallic") : 0f;
        if (Mathf.Abs(sourceMetallic - variantMetallic) > 0.0001f || variantMetallic > 0.05f)
            throw new InvalidOperationException($"{label} changed dielectric metallic class: {variantMetallic:F4}.");

        if (variant.HasProperty("_Color"))
        {
            Color expected = Multiply(source.GetColor("_Color"), colorMultiplier);
            if (!Approximately(expected, variant.GetColor("_Color"), 0.002f))
                throw new InvalidOperationException($"{label} albedo multiplier drifted from the restrained exposure contract.");
        }

        if (source.HasProperty("_GlossMapScale"))
        {
            float expected = source.GetFloat("_GlossMapScale") * glossScaleMultiplier;
            float actual = variant.GetFloat("_GlossMapScale");
            if (Mathf.Abs(expected - actual) > 0.002f || actual < 0.75f || actual > 1.12f)
                throw new InvalidOperationException($"{label} smoothness scale outside physical contract: {actual:F3}.");
        }

        if (variant.IsKeywordEnabled("_EMISSION") ||
            (variant.HasProperty("_EmissionColor") && variant.GetColor("_EmissionColor").maxColorComponent > 0.001f))
            throw new InvalidOperationException($"{label} cannot use emission as fake brightness.");
        if (variant.renderQueue >= 2501)
            throw new InvalidOperationException($"{label} must remain an opaque geometry material; renderQueue={variant.renderQueue}.");
    }

    private static void ResetVariants(GameObject root, Material basePaint, Material baseTimber)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null) continue;
                if (materials[i].name == UvPaintName || materials[i].name == HandPaintName)
                {
                    materials[i] = basePaint;
                    changed = true;
                }
                else if (materials[i].name == SunTimberName)
                {
                    materials[i] = baseTimber;
                    changed = true;
                }
            }
            if (changed)
            {
                renderer.sharedMaterials = materials;
                EditorUtility.SetDirty(renderer);
            }
        }
    }

    private static void SetMaterial(Transform target, Material material)
    {
        Renderer renderer = RequireRenderer(target);
        renderer.sharedMaterial = material;
        EditorUtility.SetDirty(renderer);
    }

    private static void AssertMaterial(Renderer renderer, Material expected, string label, HashSet<Renderer> allowed)
    {
        if (renderer.sharedMaterial != expected)
            throw new InvalidOperationException(
                $"Cause-based material mismatch on {label}: expected {expected.name}, got " +
                $"{(renderer.sharedMaterial != null ? renderer.sharedMaterial.name : "<null>")}.");
        allowed.Add(renderer);
    }

    private static bool IsHandContactName(string name) =>
        name.StartsWith("AccessHandrail_", StringComparison.Ordinal) ||
        name.StartsWith("AccessRailBridge_", StringComparison.Ordinal) ||
        name == "AccessPlatformTransitionTop";

    private static bool IsWeatheringVariant(Material material) =>
        material != null && (material.name == UvPaintName || material.name == HandPaintName || material.name == SunTimberName);

    private static Material RequireBaseMaterial(string materialName)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialRoot}/{materialName}.mat");
        if (material == null)
            throw new InvalidOperationException(
                $"Base material missing: {materialName}. Run park-furniture construction/microdetail before exposure weathering.");
        return material;
    }

    private static Material RequireVariant(string materialName)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>($"{VariantRoot}/{materialName}.mat");
        if (material == null)
            throw new InvalidOperationException($"Exposure-weathering material variant missing: {materialName}.");
        return material;
    }

    private static GameObject RequireRoot(string name)
    {
        GameObject root = GameObject.Find(name);
        if (root == null) throw new InvalidOperationException($"Exposure-weathering root missing: {name}.");
        return root;
    }

    private static Transform RequireDirectChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child == null) throw new InvalidOperationException($"Required exposure/weathering component missing: {parent.name}/{name}.");
        return child;
    }

    private static Renderer RequireRenderer(Transform target)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer == null) throw new InvalidOperationException($"Renderer missing on exposure/weathering component: {target.name}.");
        return renderer;
    }

    private static int CountRenderers(params GameObject[] roots) =>
        roots.Sum(root => root.GetComponentsInChildren<Renderer>(true).Length);

    private static int CountColliders(params GameObject[] roots) =>
        roots.Sum(root => root.GetComponentsInChildren<Collider>(true).Length);

    private static Color Multiply(Color a, Color b) =>
        new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);

    private static bool Approximately(Vector2 a, Vector2 b, float tolerance) =>
        Vector2.Distance(a, b) <= tolerance;

    private static bool Approximately(Color a, Color b, float tolerance) =>
        Mathf.Abs(a.r - b.r) <= tolerance && Mathf.Abs(a.g - b.g) <= tolerance &&
        Mathf.Abs(a.b - b.b) <= tolerance && Mathf.Abs(a.a - b.a) <= tolerance;

    private static void DisableEmission(Material material)
    {
        material.DisableKeyword("_EMISSION");
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
    }

    private static void EnsureBenchmarkScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
            throw new InvalidOperationException(
                $"Exposure-weathering QA requires the benchmark scene {ScenePath}; active={scene.path}.");
    }

    private static void OnCameraPreCull(Camera camera)
    {
        if (validatingCamera || camera == null) return;
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath) return;
        if (!IsFormalCamera(camera)) return;

        validatingCamera = true;
        try
        {
            ValidateOpenScene();
        }
        finally
        {
            validatingCamera = false;
        }
    }

    private static bool IsFormalCamera(Camera camera)
    {
        if (camera.cameraType == CameraType.Reflection) return true;
        string n = camera.name ?? string.Empty;
        return n.StartsWith("QA4K_", StringComparison.Ordinal) ||
               n.StartsWith("QATemporal_", StringComparison.Ordinal) ||
               n.StartsWith("QAPreparedTemporal_", StringComparison.Ordinal);
    }
}
