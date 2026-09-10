using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Reconstructs the benchmark-visible neighborhood notice board as a shallow weatherproof display case
/// rather than a flat backing rectangle. Manufacture-scale geometry carries backing depth, clear acrylic,
/// EPDM retention, hinges, latch, drip hood and paper thickness; printed ink remains texture detail.
/// Source/scene validation is implementation-readiness only and never awards Visual Fidelity points.
/// </summary>
public static class QualityBlockNoticeBoardDisplayCaseQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/notice_board_display_case_contract.json";
    private const string LookdevPath = "Assets/QA/notice_board_display_case_lookdev.svg";
    private const string BoardName = "HD_NoticeBoard";
    private const string CaseRootName = "DisplayCaseConstruction";
    private const string MaterialRoot = "Assets/Art/GeneratedParkFurnitureMaterials/NoticeBoard";
    private const string TextureRoot = MaterialRoot + "/PrintedNotices";

    private const float FrameWidth = 2.42f;
    private const float FrameHeight = 1.33f;
    private const float FrameRail = 0.06f;
    private const float BackingWidth = 2.28f;
    private const float BackingHeight = 1.23f;
    private const float BackingThickness = 0.018f;
    private const float BackingCenterZ = -0.020f;
    private const float CoverWidth = 2.20f;
    private const float CoverHeight = 1.11f;
    private const float CoverThickness = 0.003f;
    private const float CoverCenterZ = 0.032f;
    private const float PaperThickness = 0.0004f;
    private const float PaperCenterZ = -0.0108f;
    private const float PaperFrontZ = -0.0106f;
    private const float GasketWidth = 0.010f;
    private const float GasketDepth = 0.006f;
    private const float GasketCenterZ = 0.029f;
    private const float HoodCenterY = 2.3075f;
    private const float HoodHeight = 0.025f;
    private const float HingeX = -1.18f;
    private const float HingeZ = 0.036f;
    private const float LatchX = 1.18f;
    private const float LatchY = 1.63f;
    private const float LatchZ = 0.041f;
    private const int TextureSize = 512;

    private readonly struct NoticeSpec
    {
        public readonly string Id;
        public readonly Vector2 Size;
        public readonly Vector2 Center;
        public readonly Color PaperLinear;
        public readonly Color AccentLinear;
        public readonly int Seed;

        public NoticeSpec(string id, Vector2 size, Vector2 center, Color paperLinear, Color accentLinear, int seed)
        {
            Id = id;
            Size = size;
            Center = center;
            PaperLinear = paperLinear;
            AccentLinear = accentLinear;
            Seed = seed;
        }
    }

    private static readonly NoticeSpec[] Notices =
    {
        new NoticeSpec("A4_LeftUpper", new Vector2(0.21f, 0.297f), new Vector2(-0.74f, 1.86f), new Color(0.82f, 0.79f, 0.66f), new Color(0.16f, 0.17f, 0.16f), 1103),
        new NoticeSpec("A3_LeftLower", new Vector2(0.42f, 0.297f), new Vector2(-0.42f, 1.38f), new Color(0.72f, 0.79f, 0.81f), new Color(0.10f, 0.22f, 0.30f), 2207),
        new NoticeSpec("A4_CenterUpper", new Vector2(0.21f, 0.297f), new Vector2(-0.10f, 1.88f), new Color(0.90f, 0.88f, 0.80f), new Color(0.17f, 0.17f, 0.16f), 3319),
        new NoticeSpec("A3_Right", new Vector2(0.297f, 0.42f), new Vector2(0.47f, 1.58f), new Color(0.77f, 0.82f, 0.70f), new Color(0.12f, 0.26f, 0.17f), 4421),
        new NoticeSpec("A5_RightUpper", new Vector2(0.148f, 0.21f), new Vector2(0.84f, 1.96f), new Color(0.85f, 0.73f, 0.73f), new Color(0.35f, 0.14f, 0.16f), 5531),
    };

    [MenuItem("NewTown/Quality/Reconstruct Notice Board Display Case")]
    public static void ApplyToOpenScene()
    {
        ValidateContractConfigOnly();
        EnsureScene();

        GameObject board = RequireSceneObject(BoardName);
        Material painted = FindMaterialRecursive(board, "PBR_ParkPaintedSteel");
        Material exposed = FindMaterialRecursive(board, "PBR_ParkExposedSteel");
        Material backing = FindMaterialRecursive(board, "PBR_NoticeBoardBacking");
        if (painted == null || exposed == null || backing == null)
            throw new InvalidOperationException("Notice-board reconstruction could not resolve authoritative painted/exposed/backing materials.");

        Material acrylic = GetOrCreateAcrylicMaterial();
        Material gasket = GetOrCreateGasketMaterial();
        Material[] papers = Notices.Select(GetOrCreateNoticeMaterial).ToArray();

        for (int lod = 0; lod < 4; lod++)
        {
            Transform tier = board.transform.Find($"LOD{lod}");
            if (tier == null) throw new InvalidOperationException($"{BoardName}/LOD{lod} missing.");
            RebuildTier(tier, lod, painted, exposed, backing, acrylic, gasket, papers);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ValidateGeometryAndMaterials(board, requireLodMembership: false);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Notice-board display-case construction applied. Visual Fidelity remains UNSCORED until native render evidence exists.");
    }

    [MenuItem("NewTown/QA/Validate Notice Board Display Case")]
    public static void ValidateOpenScene()
    {
        ValidateContractConfigOnly();
        EnsureScene();
        GameObject board = RequireSceneObject(BoardName);
        ValidateGeometryAndMaterials(board, requireLodMembership: true);
        Debug.Log("Notice-board display-case source/runtime QA passed. This does not award Visual Fidelity points.");
    }

    [MenuItem("NewTown/QA/Validate Notice Board Display Case Contract")]
    public static void ValidateContractConfigOnly()
    {
        if (!File.Exists(ContractPath))
            throw new InvalidOperationException($"Missing required notice-board construction metadata: {ContractPath}");
        if (!File.Exists(LookdevPath))
            throw new InvalidOperationException($"Missing required notice-board construction lookdev: {LookdevPath}");

        string json = File.ReadAllText(ContractPath);
        string[] requiredTokens =
        {
            "\"id\": \"notice_board_display_case_installation\"",
            "\"clearCoverThickness\": 0.003",
            "\"displayCavityDepth\": 0.040",
            "\"requiredHingeCount\": 3",
            "\"requiredNoticeCount\": 5",
            "\"minimumDistinctNoticeMaterialCount\": 3",
            "\"requiredLodCount\": 4",
            "\"generatedColliderCount\": 0",
            "\"printedPaper\"",
            "\"clearAcrylicCover\"",
            "\"epdmGasket\"",
            "\"visualFidelityPointsAwarded\": 0",
            "PENDING_UNITY_RUNTIME"
        };
        foreach (string token in requiredTokens)
            if (!json.Contains(token, StringComparison.Ordinal))
                throw new InvalidOperationException($"Notice-board display-case contract missing required token: {token}");
    }

    private static void RebuildTier(Transform tier, int lod, Material painted, Material exposed,
        Material backingMaterial, Material acrylic, Material gasket, Material[] papers)
    {
        Transform backing = tier.Find("Backing");
        if (backing == null) throw new InvalidOperationException($"{tier.name}/Backing missing.");
        MeshFilter backingFilter = backing.GetComponent<MeshFilter>();
        MeshRenderer backingRenderer = backing.GetComponent<MeshRenderer>();
        if (backingFilter == null || backingRenderer == null)
            throw new InvalidOperationException($"{tier.name}/Backing must have MeshFilter/MeshRenderer.");
        backing.localPosition = new Vector3(0f, 1.63f, BackingCenterZ);
        backing.localRotation = Quaternion.identity;
        backing.localScale = Vector3.one;
        backingFilter.sharedMesh = QualityBlockDetailMeshLibrary.GetChamferedBox(new Vector3(BackingWidth, BackingHeight, BackingThickness));
        backingRenderer.sharedMaterial = backingMaterial;

        foreach (string frameName in new[] { "FrameTop", "FrameBottom", "FrameL", "FrameR" })
        {
            Transform frame = tier.Find(frameName);
            MeshRenderer mr = frame != null ? frame.GetComponent<MeshRenderer>() : null;
            if (mr == null) throw new InvalidOperationException($"{tier.name}/{frameName} missing renderer.");
            mr.sharedMaterial = painted;
        }

        Transform previous = tier.Find(CaseRootName);
        if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
        var caseRoot = new GameObject(CaseRootName);
        caseRoot.transform.SetParent(tier, false);

        AddBox("ClearAcrylicCover", caseRoot.transform, new Vector3(0f, 1.63f, CoverCenterZ),
            new Vector3(CoverWidth, CoverHeight, CoverThickness), acrylic);

        float halfCoverW = CoverWidth * 0.5f;
        float halfCoverH = CoverHeight * 0.5f;
        AddBox("GasketTop", caseRoot.transform, new Vector3(0f, 1.63f + halfCoverH + GasketWidth * 0.5f, GasketCenterZ),
            new Vector3(CoverWidth + GasketWidth * 2f, GasketWidth, GasketDepth), gasket);
        AddBox("GasketBottom", caseRoot.transform, new Vector3(0f, 1.63f - halfCoverH - GasketWidth * 0.5f, GasketCenterZ),
            new Vector3(CoverWidth + GasketWidth * 2f, GasketWidth, GasketDepth), gasket);
        AddBox("GasketLeft", caseRoot.transform, new Vector3(-halfCoverW - GasketWidth * 0.5f, 1.63f, GasketCenterZ),
            new Vector3(GasketWidth, CoverHeight, GasketDepth), gasket);
        AddBox("GasketRight", caseRoot.transform, new Vector3(halfCoverW + GasketWidth * 0.5f, 1.63f, GasketCenterZ),
            new Vector3(GasketWidth, CoverHeight, GasketDepth), gasket);

        AddBox("DripHood", caseRoot.transform, new Vector3(0f, HoodCenterY, 0.012f),
            new Vector3(2.48f, HoodHeight, 0.14f), painted);

        float[] hingeY = { 1.20f, 1.63f, 2.06f };
        for (int i = 0; i < hingeY.Length; i++)
            AddCylinder($"HingeKnuckle_{i}", caseRoot.transform, new Vector3(HingeX, hingeY[i], HingeZ),
                0.016f, 0.080f, Quaternion.identity, exposed);

        AddBox("LatchBody", caseRoot.transform, new Vector3(LatchX, LatchY, LatchZ),
            new Vector3(0.035f, 0.055f, 0.012f), exposed);
        AddCylinder("LatchKeyCylinder", caseRoot.transform, new Vector3(LatchX, LatchY, LatchZ + 0.010f),
            0.012f, 0.008f, Quaternion.Euler(90f, 0f, 0f), exposed);

        for (int i = 0; i < Notices.Length; i++)
        {
            NoticeSpec notice = Notices[i];
            AddBox($"Notice_{notice.Id}", caseRoot.transform,
                new Vector3(notice.Center.x, notice.Center.y, PaperCenterZ),
                new Vector3(notice.Size.x, notice.Size.y, PaperThickness), papers[i]);

            if (lod == 0)
            {
                float px = notice.Size.x * 0.34f;
                float py = notice.Size.y * 0.39f;
                AddCylinder($"Pin_{notice.Id}_L", caseRoot.transform,
                    new Vector3(notice.Center.x - px, notice.Center.y + py, PaperFrontZ + 0.002f),
                    0.007f, 0.004f, Quaternion.Euler(90f, 0f, 0f), exposed);
                AddCylinder($"Pin_{notice.Id}_R", caseRoot.transform,
                    new Vector3(notice.Center.x + px, notice.Center.y + py, PaperFrontZ + 0.002f),
                    0.007f, 0.004f, Quaternion.Euler(90f, 0f, 0f), exposed);
            }
        }
    }

    private static void ValidateGeometryAndMaterials(GameObject board, bool requireLodMembership)
    {
        LODGroup group = board.GetComponent<LODGroup>();
        if (group == null || group.GetLODs().Length != 4)
            throw new InvalidOperationException("HD_NoticeBoard must retain exactly four LOD tiers.");
        LOD[] lods = group.GetLODs();

        float backingFront = BackingCenterZ + BackingThickness * 0.5f;
        float coverRear = CoverCenterZ - CoverThickness * 0.5f;
        float airGap = coverRear - PaperFrontZ;
        if (Mathf.Abs(backingFront - (PaperCenterZ - PaperThickness * 0.5f)) > 0.001f)
            throw new InvalidOperationException("Notice paper no longer seats on the backing within 1 mm.");
        if (airGap < 0.020f)
            throw new InvalidOperationException($"Notice paper-to-cover air gap too small: {airGap:F4} m.");

        float horizontalClearance = ((FrameWidth - FrameRail * 2f) - CoverWidth) * 0.5f;
        float verticalClearance = ((FrameHeight - FrameRail * 2f) - CoverHeight) * 0.5f;
        if (horizontalClearance < 0.030f || horizontalClearance > 0.070f ||
            verticalClearance < 0.030f || verticalClearance > 0.070f)
            throw new InvalidOperationException($"Clear-cover edge clearance outside 30-70 mm: H={horizontalClearance:F4}, V={verticalClearance:F4}.");

        float frameTopUpper = 2.265f + 0.060f * 0.5f;
        float hoodBottom = HoodCenterY - HoodHeight * 0.5f;
        if (Mathf.Abs(hoodBottom - frameTopUpper) > 0.0025f)
            throw new InvalidOperationException($"Drip hood does not bear on the top frame: gap={Mathf.Abs(hoodBottom - frameTopUpper):F4} m.");

        if (Mathf.Abs(HingeX - (-1.18f)) > 0.020f || Mathf.Abs(LatchX - 1.18f) > 0.020f)
            throw new InvalidOperationException("Hinge/latch axes drifted away from their frame rails.");

        var distinctPaperMaterials = new HashSet<Material>();
        for (int lod = 0; lod < 4; lod++)
        {
            Transform tier = board.transform.Find($"LOD{lod}");
            Transform caseRoot = tier != null ? tier.Find(CaseRootName) : null;
            if (caseRoot == null) throw new InvalidOperationException($"{BoardName}/LOD{lod}/{CaseRootName} missing.");

            int notices = caseRoot.Cast<Transform>().Count(x => x.name.StartsWith("Notice_", StringComparison.Ordinal));
            int pins = caseRoot.Cast<Transform>().Count(x => x.name.StartsWith("Pin_", StringComparison.Ordinal));
            int hinges = caseRoot.Cast<Transform>().Count(x => x.name.StartsWith("HingeKnuckle_", StringComparison.Ordinal));
            if (notices != 5) throw new InvalidOperationException($"LOD{lod} requires 5 notice sheets, got {notices}.");
            if (hinges != 3) throw new InvalidOperationException($"LOD{lod} requires 3 hinge knuckles, got {hinges}.");
            if ((lod == 0 && pins != 10) || (lod > 0 && pins != 0))
                throw new InvalidOperationException($"LOD{lod} pin policy invalid: count={pins}.");

            foreach (Collider c in caseRoot.GetComponentsInChildren<Collider>(true))
                throw new InvalidOperationException($"Notice-board visual case added a gameplay collider: {c.name}");

            foreach (MeshFilter mf in caseRoot.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = mf.sharedMesh;
                if (mesh == null) throw new InvalidOperationException($"Missing mesh on {mf.name}.");
                string n = mesh.name ?? string.Empty;
                if (n == "Cube" || n == "Cylinder" || n == "Sphere" || n == "Capsule")
                    throw new InvalidOperationException($"Critical placeholder risk: built-in primitive mesh in notice-board case: {mf.name}/{n}");
            }

            Transform cover = caseRoot.Find("ClearAcrylicCover");
            Renderer coverRenderer = cover != null ? cover.GetComponent<Renderer>() : null;
            if (coverRenderer == null) throw new InvalidOperationException($"LOD{lod} clear acrylic cover missing renderer.");
            ValidateAcrylic(coverRenderer.sharedMaterial);

            foreach (Transform child in caseRoot.Cast<Transform>().Where(x => x.name.StartsWith("Notice_", StringComparison.Ordinal)))
            {
                Renderer r = child.GetComponent<Renderer>();
                if (r == null || r.sharedMaterial == null) throw new InvalidOperationException($"Notice sheet missing material: {child.name}");
                distinctPaperMaterials.Add(r.sharedMaterial);
                ValidatePaperMaterial(r.sharedMaterial);
            }

            if (requireLodMembership)
            {
                var members = new HashSet<Renderer>(lods[lod].renderers.Where(x => x != null));
                foreach (Renderer r in caseRoot.GetComponentsInChildren<Renderer>(true))
                    if (!members.Contains(r))
                        throw new InvalidOperationException($"Notice-board renderer escaped LOD{lod} membership: {r.name}");
            }
        }

        if (distinctPaperMaterials.Count < 3)
            throw new InvalidOperationException($"Notice-board requires at least 3 distinct printed-paper materials, got {distinctPaperMaterials.Count}.");
    }

    private static Material GetOrCreateAcrylicMaterial()
    {
        EnsureMaterialRoot();
        string path = MaterialRoot + "/PBR_NoticeClearAcrylic.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("Standard shader unavailable for notice-board acrylic.");
            mat = new Material(shader) { name = "PBR_NoticeClearAcrylic" };
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.color = new Color(0.86f, 0.93f, 0.95f, 0.16f);
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Glossiness", 0.89f);
        mat.SetFloat("_Mode", 3f);
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = (int)RenderQueue.Transparent;
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material GetOrCreateGasketMaterial()
    {
        EnsureMaterialRoot();
        string path = MaterialRoot + "/PBR_NoticeEPDM.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("Standard shader unavailable for notice-board gasket.");
            mat = new Material(shader) { name = "PBR_NoticeEPDM" };
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.color = new Color(0.025f, 0.027f, 0.026f, 1f);
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Glossiness", 0.16f);
        mat.renderQueue = (int)RenderQueue.Geometry;
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material GetOrCreateNoticeMaterial(NoticeSpec spec)
    {
        EnsureMaterialRoot();
        string texturePath = TextureRoot + "/Notice_" + spec.Id + ".png";
        EnsureNoticeTexture(texturePath, spec);
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (texture == null) throw new InvalidOperationException($"Generated notice texture failed to import: {texturePath}");

        string materialPath = MaterialRoot + "/PBR_NoticePaper_" + spec.Id + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (mat == null)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("Standard shader unavailable for printed notice paper.");
            mat = new Material(shader) { name = "PBR_NoticePaper_" + spec.Id };
            AssetDatabase.CreateAsset(mat, materialPath);
        }
        mat.color = Color.white;
        mat.SetTexture("_MainTex", texture);
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Glossiness", 0.16f);
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void EnsureNoticeTexture(string assetPath, NoticeSpec spec)
    {
        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        if (existing != null && existing.width == TextureSize && existing.height == TextureSize) return;

        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrWhiteSpace(projectRoot)) throw new InvalidOperationException("Could not resolve Unity project root.");
        string absolute = Path.Combine(projectRoot, assetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute));

        var tex = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false, false);
        Color32 paper = (Color32)spec.PaperLinear.gamma;
        Color32 accent = (Color32)spec.AccentLinear.gamma;
        Color32 ink = (Color32)new Color(0.12f, 0.115f, 0.105f, 1f).gamma;
        var pixels = Enumerable.Repeat(paper, TextureSize * TextureSize).ToArray();

        int margin = 42;
        DrawRect(pixels, margin, TextureSize - 110, TextureSize - margin * 2, 42, accent);
        int lineY = TextureSize - 165;
        for (int row = 0; row < 11; row++)
        {
            int width = TextureSize - margin * 2 - ((spec.Seed + row * 37) % 120);
            int x = margin + ((spec.Seed + row * 13) % 18);
            DrawRect(pixels, x, lineY - row * 30, Mathf.Max(120, width), row % 4 == 0 ? 8 : 5, ink);
        }
        DrawRect(pixels, margin, 50, 105 + (spec.Seed % 80), 16, accent);
        tex.SetPixels32(pixels);
        tex.Apply(false, false);
        File.WriteAllBytes(absolute, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) throw new InvalidOperationException($"Notice texture importer unavailable: {assetPath}");
        importer.sRGBTexture = true;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 4;
        importer.maxTextureSize = TextureSize;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }

    private static void DrawRect(Color32[] pixels, int x, int y, int width, int height, Color32 color)
    {
        int x0 = Mathf.Clamp(x, 0, TextureSize - 1);
        int y0 = Mathf.Clamp(y, 0, TextureSize - 1);
        int x1 = Mathf.Clamp(x + width, 0, TextureSize);
        int y1 = Mathf.Clamp(y + height, 0, TextureSize);
        for (int yy = y0; yy < y1; yy++)
        for (int xx = x0; xx < x1; xx++)
            pixels[yy * TextureSize + xx] = color;
    }

    private static void ValidateAcrylic(Material mat)
    {
        if (mat == null) throw new InvalidOperationException("Notice-board acrylic material missing.");
        float metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;
        float smooth = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0f;
        if (metallic > 0.01f) throw new InvalidOperationException($"Clear acrylic became metallic: {metallic:F3}");
        if (smooth < 0.85f || smooth > 0.93f) throw new InvalidOperationException($"Clear acrylic smoothness outside physical source contract: {smooth:F3}");
        if (mat.color.a < 0.08f || mat.color.a > 0.30f) throw new InvalidOperationException($"Clear acrylic alpha outside source contract: {mat.color.a:F3}");
        if (mat.renderQueue < 2501) throw new InvalidOperationException("Clear acrylic must remain on a transparent render queue.");
    }

    private static void ValidatePaperMaterial(Material mat)
    {
        if (mat.HasProperty("_Metallic") && mat.GetFloat("_Metallic") > 0.01f)
            throw new InvalidOperationException($"Printed paper became metallic: {mat.name}");
        if (!mat.HasProperty("_MainTex") || mat.GetTexture("_MainTex") == null)
            throw new InvalidOperationException($"Printed ink must remain texture/material detail: missing _MainTex on {mat.name}");
        Texture2D tex = mat.GetTexture("_MainTex") as Texture2D;
        if (tex == null || tex.width != TextureSize || tex.height != TextureSize)
            throw new InvalidOperationException($"Notice paper texture must be {TextureSize}px: {mat.name}");
        string path = AssetDatabase.GetAssetPath(tex);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null || !importer.sRGBTexture || !importer.mipmapEnabled)
            throw new InvalidOperationException($"Notice paper texture import semantics invalid: {path}");
    }

    private static GameObject AddBox(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        go.AddComponent<MeshFilter>().sharedMesh = QualityBlockDetailMeshLibrary.GetChamferedBox(size);
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    private static GameObject AddCylinder(string name, Transform parent, Vector3 localPosition,
        float diameter, float height, Quaternion localRotation, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = localRotation;
        go.transform.localScale = Vector3.one;
        go.AddComponent<MeshFilter>().sharedMesh =
            QualityBlockDetailMeshLibrary.GetBeveledCylinder(new Vector3(diameter, height * 0.5f, diameter), true);
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    private static Material FindMaterialRecursive(GameObject root, string materialName)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        foreach (Material material in renderer.sharedMaterials)
            if (material != null && material.name.Contains(materialName, StringComparison.Ordinal))
                return material;
        return null;
    }

    private static void EnsureMaterialRoot()
    {
        if (!Directory.Exists(MaterialRoot)) Directory.CreateDirectory(MaterialRoot);
        if (!Directory.Exists(TextureRoot)) Directory.CreateDirectory(TextureRoot);
        AssetDatabase.Refresh();
    }

    private static void EnsureScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static GameObject RequireSceneObject(string name)
    {
        GameObject go = Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x.scene.IsValid() && x.scene.path == ScenePath && x.name == name);
        if (go == null) throw new InvalidOperationException($"Required benchmark object missing: {name}");
        return go;
    }
}
