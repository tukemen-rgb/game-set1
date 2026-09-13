using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Keeps notice-sheet print textures physically mapped after the shared park microdetail pass rewrites
/// generated furniture meshes from legacy 0..1 face UVs to metre-space planar UVs. Each notice has its
/// own material, so inverse physical sheet dimensions plus a 0.5/0.5 center offset map metre-space
/// coordinates back to one complete printed sheet without texture repetition or stretched pseudo-text.
/// This post-microdetail normalization also keeps the clear acrylic inside the scene-wide furniture
/// roughness floor and uses Standard-shader Transparent (premultiplied) semantics so reflections remain
/// visible instead of being incorrectly faded with the sheet alpha.
/// </summary>
public static class QualityBlockNoticeBoardPrintedUvQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string BoardName = "HD_NoticeBoard";
    private const string CaseRootName = "DisplayCaseConstruction";
    private const float CompatibleAcrylicSmoothness = 0.86f; // roughness 0.14

    [MenuItem("NewTown/Quality/Normalize Notice Board Printed UVs")]
    public static void ApplyAndValidate()
    {
        EnsureScene();
        GameObject board = RequireBoard();
        int normalized = 0;

        for (int lod = 0; lod < 4; lod++)
        {
            Transform tier = board.transform.Find($"LOD{lod}");
            Transform caseRoot = tier != null ? tier.Find(CaseRootName) : null;
            if (caseRoot == null) throw new InvalidOperationException($"{BoardName}/LOD{lod}/{CaseRootName} missing.");

            foreach (Transform notice in caseRoot.Cast<Transform>().Where(x => x.name.StartsWith("Notice_", StringComparison.Ordinal)))
            {
                MeshFilter mf = notice.GetComponent<MeshFilter>();
                Renderer renderer = notice.GetComponent<Renderer>();
                if (mf == null || mf.sharedMesh == null || renderer == null || renderer.sharedMaterial == null)
                    throw new InvalidOperationException($"Printed notice is missing mesh/material: {notice.name}");

                Vector3 size = mf.sharedMesh.bounds.size;
                if (size.x <= 0.05f || size.y <= 0.05f)
                    throw new InvalidOperationException($"Printed notice physical bounds invalid: {notice.name} {size}");

                Material material = renderer.sharedMaterial;
                material.SetTextureScale("_MainTex", new Vector2(1f / size.x, 1f / size.y));
                material.SetTextureOffset("_MainTex", new Vector2(0.5f, 0.5f));
                EditorUtility.SetDirty(material);
                normalized++;
            }
        }

        NormalizeAcrylicCompatibility(board);
        AssetDatabase.SaveAssets();
        ValidateOpenScene();
        Debug.Log($"Notice-board printed metre-UV normalization applied to {normalized} LOD notice renderers; acrylic roughness normalized to 0.14 and Standard Transparent premultiplied blending enforced. Render quality remains unscored.");
    }

    [MenuItem("NewTown/QA/Validate Notice Board Printed UVs")]
    public static void ValidateOpenScene()
    {
        EnsureScene();
        GameObject board = RequireBoard();
        int checkedCount = 0;

        for (int lod = 0; lod < 4; lod++)
        {
            Transform tier = board.transform.Find($"LOD{lod}");
            Transform caseRoot = tier != null ? tier.Find(CaseRootName) : null;
            if (caseRoot == null) throw new InvalidOperationException($"{BoardName}/LOD{lod}/{CaseRootName} missing.");

            Transform[] notices = caseRoot.Cast<Transform>()
                .Where(x => x.name.StartsWith("Notice_", StringComparison.Ordinal)).ToArray();
            if (notices.Length != 5)
                throw new InvalidOperationException($"LOD{lod} requires five printed notice sheets, got {notices.Length}.");

            Transform cover = caseRoot.Find("ClearAcrylicCover");
            Renderer coverRenderer = cover != null ? cover.GetComponent<Renderer>() : null;
            Material coverMaterial = coverRenderer != null ? coverRenderer.sharedMaterial : null;
            ValidateAcrylicCompatibility(coverMaterial, lod);

            foreach (Transform notice in notices)
            {
                MeshFilter mf = notice.GetComponent<MeshFilter>();
                Renderer renderer = notice.GetComponent<Renderer>();
                if (mf == null || mf.sharedMesh == null || renderer == null || renderer.sharedMaterial == null)
                    throw new InvalidOperationException($"Printed notice missing mesh/material: {notice.name}");

                Vector3 size = mf.sharedMesh.bounds.size;
                Vector2 expectedScale = new Vector2(1f / size.x, 1f / size.y);
                Vector2 actualScale = renderer.sharedMaterial.GetTextureScale("_MainTex");
                Vector2 actualOffset = renderer.sharedMaterial.GetTextureOffset("_MainTex");
                if (!Approximately(actualScale.x, expectedScale.x, 0.02f) ||
                    !Approximately(actualScale.y, expectedScale.y, 0.02f))
                    throw new InvalidOperationException(
                        $"Printed notice physical UV scale drift: {notice.name}, got {actualScale}, expected {expectedScale}.");
                if (Vector2.Distance(actualOffset, new Vector2(0.5f, 0.5f)) > 0.002f)
                    throw new InvalidOperationException($"Printed notice UV center offset drift: {notice.name}, got {actualOffset}.");

                Texture2D texture = renderer.sharedMaterial.GetTexture("_MainTex") as Texture2D;
                if (texture == null) throw new InvalidOperationException($"Printed notice texture missing: {notice.name}");
                string path = AssetDatabase.GetAssetPath(texture);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null || importer.wrapMode != TextureWrapMode.Clamp || !importer.mipmapEnabled || !importer.sRGBTexture)
                    throw new InvalidOperationException($"Printed notice texture sampling/import semantics invalid: {path}");

                Vector2[] uv = mf.sharedMesh.uv;
                if (uv == null || uv.Length != mf.sharedMesh.vertexCount)
                    throw new InvalidOperationException($"Printed notice mesh has no valid UV0: {notice.name}");
                checkedCount++;
            }
        }

        if (checkedCount != 20)
            throw new InvalidOperationException($"Expected 20 notice-sheet renderer instances across four LODs, got {checkedCount}.");
    }

    private static void NormalizeAcrylicCompatibility(GameObject board)
    {
        Material material = board.GetComponentsInChildren<Renderer>(true)
            .Select(x => x.sharedMaterial)
            .FirstOrDefault(x => x != null && x.name.Contains("PBR_NoticeClearAcrylic", StringComparison.Ordinal));
        if (material == null || !material.HasProperty("_Glossiness"))
            throw new InvalidOperationException("Notice-board acrylic material missing during post-microdetail compatibility normalization.");

        material.SetFloat("_Glossiness", CompatibleAcrylicSmoothness);
        material.SetFloat("_Mode", 3f);
        material.SetInt("_SrcBlend", (int)BlendMode.One);
        material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHABLEND_ON");
        material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
    }

    private static void ValidateAcrylicCompatibility(Material material, int lod)
    {
        if (material == null || !material.HasProperty("_Glossiness"))
            throw new InvalidOperationException($"LOD{lod} notice-board acrylic material/smoothness property missing.");
        float smoothness = material.GetFloat("_Glossiness");
        float roughness = 1f - smoothness;
        if (Mathf.Abs(smoothness - CompatibleAcrylicSmoothness) > 0.005f || roughness < 0.12f || roughness > 0.15f)
            throw new InvalidOperationException(
                $"LOD{lod} notice-board acrylic roughness is not compatible with both optical and scene-wide furniture gates: smoothness={smoothness:F3}, roughness={roughness:F3}.");
        if (!material.HasProperty("_Mode") || Mathf.Abs(material.GetFloat("_Mode") - 3f) > 0.01f)
            throw new InvalidOperationException($"LOD{lod} notice-board acrylic must use Standard Transparent mode.");
        if (!material.HasProperty("_SrcBlend") || material.GetInt("_SrcBlend") != (int)BlendMode.One ||
            !material.HasProperty("_DstBlend") || material.GetInt("_DstBlend") != (int)BlendMode.OneMinusSrcAlpha ||
            !material.HasProperty("_ZWrite") || material.GetInt("_ZWrite") != 0)
            throw new InvalidOperationException($"LOD{lod} notice-board acrylic Standard Transparent blend/ZWrite state drifted.");
        if (material.IsKeywordEnabled("_ALPHABLEND_ON") || !material.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON") ||
            material.renderQueue != (int)RenderQueue.Transparent)
            throw new InvalidOperationException($"LOD{lod} notice-board acrylic must preserve reflected highlights through premultiplied Transparent semantics.");
    }

    private static bool Approximately(float a, float b, float relativeTolerance)
    {
        float denominator = Mathf.Max(0.0001f, Mathf.Max(Mathf.Abs(a), Mathf.Abs(b)));
        return Mathf.Abs(a - b) / denominator <= relativeTolerance;
    }

    private static void EnsureScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static GameObject RequireBoard()
    {
        GameObject board = Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x.scene.IsValid() && x.scene.path == ScenePath && x.name == BoardName);
        if (board == null) throw new InvalidOperationException($"Required benchmark object missing: {BoardName}");
        return board;
    }
}
