using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class QualityBlockBalconyDrainageWaterproofingQA
{
    private const string ContractPath = "Assets/QA/balcony_drainage_waterproofing_contract.json";
    private const float PositionTolerance = 0.015f;

    static QualityBlockBalconyDrainageWaterproofingQA()
    {
        Camera.onPreCull -= OnPreCull;
        Camera.onPreCull += OnPreCull;
    }

    [MenuItem("NewTown/QA/Validate Balcony Drainage + Waterproofing")]
    public static void ValidateOpenScene()
    {
        ValidateContractConfigOnly();
        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != QualityBlockBalconyDrainageWaterproofingUpgrade.ScenePath)
            throw new InvalidOperationException("Balcony drainage QA requires the benchmark scene to be active.");
        if (QualityBlockBalconyDrainageWaterproofingUpgrade.IsAuthoredDanchiActive(scene)) return;

        GameObject[] roots = Resources.FindObjectsOfTypeAll<GameObject>()
            .Where(x => x.scene == scene && x.name == QualityBlockBalconyDrainageWaterproofingUpgrade.RootName).ToArray();
        if (roots.Length != 1) throw new InvalidOperationException($"Expected one balcony drainage root, got {roots.Length}.");
        GameObject root = roots[0];
        if (!root.activeInHierarchy) throw new InvalidOperationException("Balcony drainage root is inactive.");
        if (root.transform.parent == null || root.transform.parent.name != "Danchi")
            throw new InvalidOperationException("Balcony drainage root must remain attached to the Danchi fallback root.");
        if (root.GetComponentsInChildren<Collider>(true).Length != 0)
            throw new InvalidOperationException("Balcony drainage detail must not introduce colliders.");

        Mesh waterproofMesh = AssetDatabase.LoadAssetAtPath<Mesh>(QualityBlockBalconyDrainageWaterproofingUpgrade.WaterproofMeshPath);
        Mesh drainMesh = AssetDatabase.LoadAssetAtPath<Mesh>(QualityBlockBalconyDrainageWaterproofingUpgrade.DrainMeshPath);
        Material waterproofMat = AssetDatabase.LoadAssetAtPath<Material>(QualityBlockBalconyDrainageWaterproofingUpgrade.WaterproofMaterialPath);
        Material drainMat = AssetDatabase.LoadAssetAtPath<Material>(QualityBlockBalconyDrainageWaterproofingUpgrade.DrainMaterialPath);
        if (waterproofMesh == null || drainMesh == null || waterproofMat == null || drainMat == null)
            throw new InvalidOperationException("Generated balcony drainage assets are incomplete.");
        if (waterproofMesh.vertexCount < 300 || !waterproofMesh.name.StartsWith("GM_BalconyWaterproofingAssembly", StringComparison.Ordinal))
            throw new InvalidOperationException("Waterproofing assembly is not the combined chamfered construction mesh.");
        if (drainMesh.vertexCount < 800 || !drainMesh.name.StartsWith("GM_BalconyDrainGrate", StringComparison.Ordinal))
            throw new InvalidOperationException("Drain grate is not the combined manufactured frame/slat mesh.");

        ValidateMaterial(waterproofMat, "MAT_BalconyWaterproofingDry", new Color(0.245f,0.255f,0.245f,1f), 0.28f);
        ValidateMaterial(drainMat, "MAT_BalconyDrainCoatedCast", new Color(0.20f,0.22f,0.20f,1f), 0.34f);

        MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        if (renderers.Length != 60)
            throw new InvalidOperationException($"Expected exactly 60 combined renderers (2 x 30 bays), got {renderers.Length}.");
        if (root.transform.childCount != 30)
            throw new InvalidOperationException($"Expected 30 balcony drainage bay assemblies, got {root.transform.childCount}.");

        for (int f = 0; f < 5; f++)
        for (int b = 0; b < 6; b++)
        {
            GameObject slabObject = QualityBlockBalconyDrainageWaterproofingUpgrade.FindSceneObject(scene, $"BalconyFloor_{f}_{b}");
            Renderer slab = slabObject != null ? slabObject.GetComponent<Renderer>() : null;
            if (slab == null) throw new InvalidOperationException($"BalconyFloor_{f}_{b} missing during drainage QA.");
            Bounds sb = slab.bounds;

            Transform bay = root.transform.Find($"BalconyDW_{f}_{b}");
            if (bay == null) throw new InvalidOperationException($"BalconyDW_{f}_{b} missing.");
            RequireNear(bay.position, new Vector3(sb.center.x, sb.max.y, sb.center.z), PositionTolerance, $"BalconyDW_{f}_{b} floor interface");
            if (Quaternion.Angle(bay.rotation, Quaternion.identity) > 0.05f)
                throw new InvalidOperationException($"BalconyDW_{f}_{b} rotation drifted.");

            Transform waterproof = bay.Find("WaterproofingAssembly");
            Transform drain = bay.Find("DrainGrate");
            ValidateBinding(waterproof, waterproofMesh, waterproofMat, $"BalconyDW_{f}_{b}/WaterproofingAssembly");
            ValidateBinding(drain, drainMesh, drainMat, $"BalconyDW_{f}_{b}/DrainGrate");
            RequireNear(waterproof.localPosition, Vector3.zero, 0.0005f, $"BalconyDW_{f}_{b} waterproof local position");
            float side = b % 2 == 0 ? -1f : 1f;
            RequireNear(drain.localPosition,
                new Vector3(side * QualityBlockBalconyDrainageWaterproofingUpgrade.DrainOffsetX, 0f,
                    QualityBlockBalconyDrainageWaterproofingUpgrade.ChannelZ),
                0.0005f, $"BalconyDW_{f}_{b} deterministic drain position");

            Bounds db = drain.GetComponent<Renderer>().bounds;
            if (db.min.x < sb.min.x - 0.01f || db.max.x > sb.max.x + 0.01f ||
                db.min.z < sb.min.z - 0.01f || db.max.z > sb.max.z + 0.01f)
                throw new InvalidOperationException($"BalconyDW_{f}_{b} drain extends outside the slab footprint.");
            if (db.min.y < sb.max.y - 0.002f || db.max.y > sb.max.y + 0.012f)
                throw new InvalidOperationException($"BalconyDW_{f}_{b} drain is not flush/semi-flush with the slab top.");

            Bounds wb = waterproof.GetComponent<Renderer>().bounds;
            if (wb.min.x < sb.min.x - 0.02f || wb.max.x > sb.max.x + 0.02f)
                throw new InvalidOperationException($"BalconyDW_{f}_{b} waterproofing exceeds bay width.");
            float upstand = wb.max.y - sb.max.y;
            if (upstand < 0.09f || upstand > 0.11f)
                throw new InvalidOperationException($"BalconyDW_{f}_{b} upstand is {upstand:F4} m; expected the 0.10 m benchmark reconstruction assumption.");
        }

        Debug.Log("Balcony drainage/waterproofing QA passed as implementation evidence only. Native 4K visual scoring remains pending.");
    }

    public static void ValidateContractConfigOnly()
    {
        if (!File.Exists(ContractPath)) throw new InvalidOperationException("Missing contract: " + ContractPath);
        string json = File.ReadAllText(ContractPath);
        foreach (string token in new[] {
            "\"schemaVersion\": \"1.0\"", "\"bayCount\": 30", "\"nominalOutletDiameterM\": 0.05",
            "\"channelWidthM\": 0.085", "\"drainGrateOuterSizeM\": 0.11", "\"waterproofUpstandHeightM\": 0.10",
            "\"waterproofMembraneNominalBuildUpM\": 0.003", "\"visibleUnityPrimitiveMeshesForbidden\": true",
            "\"wetnessMustRemainZero\": true", "\"automaticVisualPoints\": 0", "\"actualTemporalVerificationRequired\": true",
            "\"visible_primitive_placeholder_geometry\"", "\"obviously_painted_baked_highlights\"",
            "\"materially_impossible_metallic_specular_values\"", "\"obvious_repetition\"",
            "\"floating_or_interpenetrating_hero_geometry\"", "\"inconsistent_sun_shadow_direction\"",
            "\"disaster_theme\"", "\"severe_aliasing_or_shimmer\"", "\"visible_lod_pop\"",
            "\"major_light_leak\"", "\"missing_required_construction_material_metadata\"",
            "\"claiming_render_quality_without_actual_render\"" })
            if (json.IndexOf(token, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("Balcony drainage contract missing required token: " + token);
    }

    private static void ValidateBinding(Transform t, Mesh mesh, Material mat, string label)
    {
        if (t == null) throw new InvalidOperationException(label + " missing.");
        MeshFilter mf = t.GetComponent<MeshFilter>(); MeshRenderer mr = t.GetComponent<MeshRenderer>();
        if (mf == null || mr == null || mf.sharedMesh != mesh || mr.sharedMaterial != mat)
            throw new InvalidOperationException(label + " canonical mesh/material binding drifted.");
        if (t.GetComponent<Collider>() != null) throw new InvalidOperationException(label + " unexpectedly has a collider.");
        if ((t.localScale - Vector3.one).sqrMagnitude > 0.000001f || Quaternion.Angle(t.localRotation, Quaternion.identity) > 0.01f)
            throw new InvalidOperationException(label + " distorts its dimension-baked mesh by transform scale/rotation.");
        if (mr.shadowCastingMode == ShadowCastingMode.Off || !mr.receiveShadows)
            throw new InvalidOperationException(label + " is excluded from coherent contact/shadow response.");
        if (mr.lightProbeUsage == LightProbeUsage.Off || mr.reflectionProbeUsage == ReflectionProbeUsage.Off)
            throw new InvalidOperationException(label + " is excluded from scene lighting/reflection probes.");
    }

    private static void ValidateMaterial(Material m, string name, Color color, float gloss)
    {
        if (m.shader == null || m.shader.name != "Standard" || m.name != name)
            throw new InvalidOperationException(name + " shader/name binding invalid.");
        if (ColorDistance(m.color, color) > 0.015f) throw new InvalidOperationException(name + " albedo drifted.");
        if (!m.HasProperty("_Metallic") || Mathf.Abs(m.GetFloat("_Metallic")) > 0.001f)
            throw new InvalidOperationException(name + " exposed coated/waterproof surface must remain metallic=0.");
        if (!m.HasProperty("_Glossiness") || Mathf.Abs(m.GetFloat("_Glossiness") - gloss) > 0.01f)
            throw new InvalidOperationException(name + " roughness/smoothness drifted.");
        if (m.IsKeywordEnabled("_EMISSION") || (m.HasProperty("_EmissionColor") && m.GetColor("_EmissionColor").maxColorComponent > 0.001f))
            throw new InvalidOperationException(name + " may not fake highlights with emission.");
        if (m.HasProperty("_Mode") && Mathf.Abs(m.GetFloat("_Mode")) > 0.001f)
            throw new InvalidOperationException(name + " must remain opaque and dry.");
    }

    private static void OnPreCull(Camera camera)
    {
        if (camera == null || camera.gameObject == null || !camera.gameObject.scene.IsValid()) return;
        if (camera.gameObject.scene.path != QualityBlockBalconyDrainageWaterproofingUpgrade.ScenePath || camera.name != "MainCamera") return;
        if (QualityBlockBalconyDrainageWaterproofingUpgrade.IsAuthoredDanchiActive(camera.gameObject.scene)) return;
        try { ValidateOpenScene(); }
        catch (Exception ex) { throw new InvalidOperationException("Formal benchmark render blocked by balcony drainage/waterproofing QA: " + ex.Message, ex); }
    }

    private static void RequireNear(Vector3 a, Vector3 b, float tolerance, string label)
    {
        if ((a-b).sqrMagnitude > tolerance*tolerance)
            throw new InvalidOperationException($"{label} drifted. Expected {b}, got {a}.");
    }

    private static float ColorDistance(Color a, Color b) => Mathf.Max(Mathf.Abs(a.r-b.r),
        Mathf.Max(Mathf.Abs(a.g-b.g), Mathf.Max(Mathf.Abs(a.b-b.b), Mathf.Abs(a.a-b.a))));
}
