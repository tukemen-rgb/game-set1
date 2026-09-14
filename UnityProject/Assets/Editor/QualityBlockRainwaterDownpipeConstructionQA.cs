using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Read-only construction/material/LOD gate for the physical rainwater downpipe.
/// Formal render callbacks must never repair the scene: any deviation fails before evidence capture.
/// </summary>
public static class QualityBlockRainwaterDownpipeConstructionQA
{
    private const string ContractPath = "Assets/QA/rainwater_downpipe_contract.json";
    private const string PvcNormalPath = "Assets/Generated/QualityBlockRainwater/Textures/T_RainwaterPVC_Normal.png";
    private const string PvcMetalSmoothPath = "Assets/Generated/QualityBlockRainwater/Textures/T_RainwaterPVC_MetalSmooth.png";

    [MenuItem("Tools/Quality Block/QA/Validate Physical Rainwater Downpipe")]
    public static void ValidateMenu()
    {
        ValidateOpenScene(true);
        Debug.Log("[RainwaterDownpipeQA] PASS (source/construction readiness only; this awards zero Visual Fidelity points).");
    }

    public static bool ValidateOpenScene(bool throwOnFail)
    {
        var errors = new List<string>();
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.name != "QualityBlock1990s")
            return true;

        if (AssetDatabase.LoadAssetAtPath<TextAsset>(ContractPath) == null)
            errors.Add("Missing machine-readable construction contract: " + ContractPath);

        GameObject danchi = GameObject.Find("Danchi");
        if (danchi == null)
        {
            errors.Add("Danchi root missing.");
            return Finish(errors, throwOnFail);
        }

        int legacyCount = 0;
        foreach (Transform t in danchi.GetComponentsInChildren<Transform>(true))
        {
            if (!string.Equals(t.name, "RainGutter", StringComparison.Ordinal)) continue;
            legacyCount++;
            foreach (Renderer r in t.GetComponents<Renderer>())
                if (r.enabled) errors.Add("Legacy RainGutter renderer is still enabled: " + HierarchyPath(t));
            foreach (Collider c in t.GetComponents<Collider>())
                if (c.enabled) errors.Add("Legacy RainGutter collider is still enabled: " + HierarchyPath(t));
        }
        if (legacyCount < 5)
            errors.Add("Expected five legacy RainGutter provenance objects; found " + legacyCount + ".");

        Transform root = danchi.transform.Find(QualityBlockRainwaterDownpipeUpgrade.RootName);
        if (root == null)
        {
            errors.Add("Physical rainwater root missing: Danchi/" + QualityBlockRainwaterDownpipeUpgrade.RootName);
            return Finish(errors, throwOnFail);
        }

        LODGroup[] groups = root.GetComponentsInChildren<LODGroup>(true);
        if (groups.Length != 1) errors.Add("Expected exactly one rainwater LODGroup; found " + groups.Length + ".");
        if (groups.Length == 1)
        {
            LODGroup group = groups[0];
            LOD[] lods = group.GetLODs();
            if (group.fadeMode != LODFadeMode.CrossFade) errors.Add("Rainwater LODGroup must use CrossFade.");
            if (!group.animateCrossFading) errors.Add("Rainwater LODGroup animateCrossFading must be enabled.");
            if (lods.Length != 4) errors.Add("Rainwater LODGroup must contain LOD0/1/2/3; found " + lods.Length + ".");
            int[] minimumRendererCounts = { 55, 35, 15, 1 };
            float[] expectedHeights = { 0.18f, 0.08f, 0.03f, 0.008f };
            for (int i = 0; i < lods.Length && i < 4; i++)
            {
                if (lods[i].renderers == null || lods[i].renderers.Length < minimumRendererCounts[i])
                    errors.Add("LOD" + i + " renderer count below construction minimum.");
                if (Mathf.Abs(lods[i].screenRelativeTransitionHeight - expectedHeights[i]) > 0.001f)
                    errors.Add("LOD" + i + " transition height drifted from contract.");
            }
        }

        Material pvc = null;
        Material clamp = null;
        int pipeBodies = 0;
        foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
            {
                errors.Add("Renderer missing authored mesh: " + HierarchyPath(renderer.transform));
                continue;
            }

            string meshName = filter.sharedMesh.name ?? string.Empty;
            if (meshName == "Cylinder" || meshName == "Cube" || meshName == "Sphere" || meshName == "Capsule" || meshName == "Plane" || meshName == "Quad")
                errors.Add("Visible Unity primitive-placeholder mesh detected: " + HierarchyPath(renderer.transform) + " -> " + meshName);
            if (!meshName.StartsWith("GM_HD_", StringComparison.Ordinal))
                errors.Add("Rainwater component is not using the authored detail-mesh library: " + HierarchyPath(renderer.transform) + " -> " + meshName);

            if (renderer.name == "PipeBody") pipeBodies++;
            Material material = renderer.sharedMaterial;
            if (material == null)
            {
                errors.Add("Missing material: " + HierarchyPath(renderer.transform));
                continue;
            }
            if (renderer.name == "PipeBody" || renderer.name == "SocketSleeve") pvc = material;
            else clamp = material;

            if (material.IsKeywordEnabled("_EMISSION") || (material.HasProperty("_EmissionColor") && material.GetColor("_EmissionColor").maxColorComponent > 0.001f))
                errors.Add("Emission/baked-light surrogate prohibited on rainwater material: " + material.name);
        }
        if (pipeBodies != 4) errors.Add("Expected one retained pipe silhouette in each of four LODs; found " + pipeBodies + ".");

        ValidatePvcMaterial(pvc, errors);
        ValidateClampMaterial(clamp, errors);
        ValidateTexture(PvcNormalPath, true, errors);
        ValidateTexture(PvcMetalSmoothPath, false, errors);

        Transform lod0 = root.Find("LOD0");
        if (lod0 == null) errors.Add("LOD0 root missing.");
        else
        {
            Transform pipe = lod0.Find("PipeBody");
            if (pipe == null) errors.Add("LOD0 PipeBody missing.");
            else
            {
                Vector3 p = pipe.localPosition;
                if (Mathf.Abs(p.x + 9.55f) > 0.002f || Mathf.Abs(p.y - 7.00f) > 0.002f || Mathf.Abs(p.z - 3.79f) > 0.002f)
                    errors.Add("Pipe installation datum drifted from contract: " + p);
            }
            if (CountNamed(lod0, "SocketSleeve") != 4) errors.Add("LOD0 requires four socket sleeves.");
            if (CountNamed(lod0, "SupportBand") != 10) errors.Add("LOD0 requires ten support bands.");
            if (CountNamed(lod0, "StandOff") != 10) errors.Add("LOD0 requires ten stand-offs.");
            if (CountNamed(lod0, "WallPlate") != 10) errors.Add("LOD0 requires ten wall plates.");
            if (CountPrefix(lod0, "AnchorHead_") != 20) errors.Add("LOD0 requires twenty wall-plate fastener heads.");
        }

        return Finish(errors, throwOnFail);
    }

    private static void ValidatePvcMaterial(Material material, List<string> errors)
    {
        if (material == null) { errors.Add("PVC material not resolved from rendered assembly."); return; }
        if (material.name != QualityBlockRainwaterDownpipeUpgrade.PvcMaterialName)
            errors.Add("Unexpected PVC material: " + material.name);
        float metallic = material.HasProperty("_Metallic") ? material.GetFloat("_Metallic") : 0f;
        float smooth = material.HasProperty("_Glossiness") ? material.GetFloat("_Glossiness") : -1f;
        if (metallic > 0.05f) errors.Add("PVC metallic is physically implausible: " + metallic);
        if (smooth < 0.32f || smooth > 0.42f) errors.Add("PVC smoothness outside contract: " + smooth);
        if (!material.IsKeywordEnabled("_NORMALMAP")) errors.Add("PVC normal-map response is disabled.");
    }

    private static void ValidateClampMaterial(Material material, List<string> errors)
    {
        if (material == null) { errors.Add("Clamp material not resolved from rendered assembly."); return; }
        if (material.name != QualityBlockRainwaterDownpipeUpgrade.ClampMaterialName)
            errors.Add("Unexpected clamp material: " + material.name);
        float metallic = material.HasProperty("_Metallic") ? material.GetFloat("_Metallic") : -1f;
        float smooth = material.HasProperty("_Glossiness") ? material.GetFloat("_Glossiness") : -1f;
        if (metallic < 0.70f || metallic > 0.90f) errors.Add("Galvanized clamp metallic outside contract: " + metallic);
        if (smooth < 0.30f || smooth > 0.45f) errors.Add("Galvanized clamp smoothness outside contract: " + smooth);
    }

    private static void ValidateTexture(string path, bool normalMap, List<string> errors)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) { errors.Add("Missing rainwater physical-data texture: " + path); return; }
        if (importer.textureCompression != TextureImporterCompression.Uncompressed) errors.Add(path + " must remain Uncompressed.");
        if (importer.crunchedCompression) errors.Add(path + " Crunch compression prohibited.");
        if (importer.sRGBTexture) errors.Add(path + " physical data must be linear (sRGB off).");
        if (!importer.mipmapEnabled) errors.Add(path + " mipmaps required for temporal stability.");
        if (importer.wrapMode != TextureWrapMode.Repeat) errors.Add(path + " must Repeat.");
        if (importer.filterMode != FilterMode.Trilinear) errors.Add(path + " must use Trilinear filtering.");
        if (importer.anisoLevel < 8) errors.Add(path + " anisotropy must be >= 8.");
        if (normalMap && importer.textureType != TextureImporterType.NormalMap) errors.Add(path + " must be imported as NormalMap.");
        if (!normalMap && importer.textureType != TextureImporterType.Default) errors.Add(path + " metallic/smoothness must use Default texture type.");
        TextureImporterPlatformSettings standalone = importer.GetPlatformTextureSettings("Standalone");
        if (standalone.overridden) errors.Add(path + " Standalone override prohibited for formal evidence.");
    }

    private static int CountNamed(Transform root, string name)
    {
        int count = 0;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) count++;
        return count;
    }

    private static int CountPrefix(Transform root, string prefix)
    {
        int count = 0;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith(prefix, StringComparison.Ordinal)) count++;
        return count;
    }

    private static bool Finish(List<string> errors, bool throwOnFail)
    {
        if (errors.Count == 0) return true;
        string message = "[RainwaterDownpipeQA] FAIL\n - " + string.Join("\n - ", errors.ToArray());
        if (throwOnFail) throw new InvalidOperationException(message);
        Debug.LogError(message);
        return false;
    }

    private static string HierarchyPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
        return path;
    }
}
