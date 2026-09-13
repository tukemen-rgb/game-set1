using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class QualityBlockCapture
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string OutputDir = "Assets/QA";
    private const string OutputPath = OutputDir + "/quality_block_1990s.png";

    [MenuItem("NewTown/QA/Capture Quality Block PNG")]
    public static void Capture()
    {
        // Always rebuild the exact review target: generated PBR materials, authored geometry pass,
        // replacement-ready art slots, physically derived midsummer sun, source-driven weathering,
        // high-granularity danchi construction, dimension-baked bevels, explicit danchi LOD0-LOD3,
        // physically connected root/trunk/branch/twig/leaf tree masters with their own four LODs,
        // and context-driven two-sided foliage optics that cast explicit leaf-geometry shadows.
        // If matching authored prefabs/FBX assets exist they still supersede generated fallbacks.
        QualityBlockFoliageOpticsUpgrade.BuildFoliageOptics();
        QualityBlockDanchiDetailUpgrade.ValidateOpenScene();
        QualityBlockDetailBevelUpgrade.ValidateOpenScene();
        QualityBlockDanchiLodUpgrade.ValidateOpenScene();
        QualityBlockTreeDetailUpgrade.ValidateOpenScene();
        QualityBlockFoliageOpticsUpgrade.ValidateOpenScene();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("QualityBlock capture failed: MainCamera not found.");
            return;
        }

        Directory.CreateDirectory(OutputDir);
        const int width = 1920;
        const int height = 1080;
        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
        {
            antiAliasing = 4
        };
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        var oldTarget = cam.targetTexture;
        var oldActive = RenderTexture.active;

        try
        {
            cam.targetTexture = rt;
            RenderTexture.active = rt;
            cam.Render();
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(OutputPath, tex.EncodeToPNG());
            AssetDatabase.Refresh();
            Debug.Log($"Captured {OutputPath}");
        }
        finally
        {
            cam.targetTexture = oldTarget;
            RenderTexture.active = oldActive;
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }
}
