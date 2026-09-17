using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Re-bakes the reconstructed facade shell UV0 from absolute Danchi-local metres so every repeated
/// bay shares one physically scaled coordinate field instead of restarting the same 0..1 concrete tile.
/// The existing two-scale facade PBR set is reused: macro scale is baked at 2.4 m/tile in UV0 and the
/// detail-normal scale remains 0.22 m/tile through material tiling. This is a source-side anti-repeat /
/// texel-scale control only; real 4K and temporal evidence remain authoritative.
/// </summary>
public static class QualityBlockFacadeAperturePhysicalUvQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/facade_aperture_uv_contract.json";
    private const string RootName = "DanchiFacadeApertureShell";
    private const string SourceMaterialPath = "Assets/Art/GeneratedFacadeOptics/MAT_FacadePaintedRC_Main.mat";
    private const string PhysicalMaterialPath = "Assets/Art/GeneratedFacadeOptics/MAT_FacadePaintedRC_AperturePhysicalUV.mat";
    private const string MeshRoot = "Assets/Art/GeneratedFacadeApertureMeshes";
    private const float MacroTileMeters = 2.4f;
    private const float DetailTileMeters = 0.22f;
    private const float DetailScale = MacroTileMeters / DetailTileMeters;
    private const float PhaseQuantization = 0.02f;
    private const int MinimumFacadeCellRenderers = 120;
    private const int MinimumDistinctPhases = 20;

    [MenuItem("NewTown/Materials/Bake World-Aligned Facade Aperture UVs")]
    public static void ApplyAndPersist()
    {
        EnsureSceneOpen();
        ValidateContractConfigOnly();

        GameObject root = FindSceneObject(RootName);
        if (root == null)
            throw new InvalidOperationException(
                $"{RootName} is missing. Reconstruct the facade apertures before baking physical UVs.");
        GameObject danchi = FindSceneObject("Danchi");
        if (danchi == null)
            throw new InvalidOperationException("Danchi root is missing.");

        Material source = AssetDatabase.LoadAssetAtPath<Material>(SourceMaterialPath);
        if (source == null)
            throw new InvalidOperationException($"Source facade material missing: {SourceMaterialPath}");
        Material physical = GetOrCreatePhysicalMaterial(source);
        Directory.CreateDirectory(AbsolutePath(MeshRoot));

        MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true)
            .Where(r => r != null && r.gameObject.activeInHierarchy)
            .Where(r => !r.gameObject.name.StartsWith("FA_WindowSeal_", StringComparison.Ordinal))
            .OrderBy(r => HierarchyPath(r.transform), StringComparer.Ordinal)
            .ToArray();
        if (renderers.Length == 0)
            throw new InvalidOperationException("No reconstructed facade shell renderers were found for physical UV baking.");

        foreach (MeshRenderer renderer in renderers)
        {
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            Mesh sourceMesh = filter != null ? filter.sharedMesh : null;
            if (sourceMesh == null)
                throw new InvalidOperationException($"Facade shell renderer has no mesh: {HierarchyPath(renderer.transform)}");
            if (!sourceMesh.isReadable)
                throw new InvalidOperationException($"Facade shell source mesh is not readable: {sourceMesh.name}");

            string assetPath = $"{MeshRoot}/{Sanitize(renderer.gameObject.name)}_PhysicalUV.asset";
            Mesh baked = BakeWorldAlignedUvMesh(sourceMesh, renderer.transform, danchi.transform);
            baked.name = Path.GetFileNameWithoutExtension(assetPath);

            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(baked, assetPath);
                filter.sharedMesh = baked;
            }
            else
            {
                EditorUtility.CopySerialized(baked, existing);
                UnityEngine.Object.DestroyImmediate(baked);
                EditorUtility.SetDirty(existing);
                filter.sharedMesh = existing;
            }

            renderer.sharedMaterial = physical;
            EditorUtility.SetDirty(filter);
            EditorUtility.SetDirty(renderer);
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.Refresh();
        ValidateOpenScene();
        Debug.Log(
            $"World-aligned facade aperture UV bake complete: shellRenderers={renderers.Length}, macroTile={MacroTileMeters:F2} m, detailTile={DetailTileMeters:F2} m. " +
            "Visual Fidelity remains UNSCORED pending native 4K/temporal review.");
    }

    [MenuItem("NewTown/QA/Validate Facade Aperture Physical UV Contract")]
    public static void ValidateContractConfigOnly()
    {
        string path = AbsolutePath(ContractPath);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Facade aperture UV contract missing: {ContractPath}");

        Contract contract = JsonUtility.FromJson<Contract>(File.ReadAllText(path));
        if (contract == null || contract.qa == null)
            throw new InvalidOperationException("Facade aperture UV contract is null or incomplete.");

        var errors = new List<string>();
        RequireEqual(contract.scenePath, ScenePath, "scenePath", errors);
        RequireEqual(contract.apertureRootName, RootName, "apertureRootName", errors);
        RequireEqual(contract.sourceMaterial, SourceMaterialPath, "sourceMaterial", errors);
        RequireEqual(contract.physicalUvMaterial, PhysicalMaterialPath, "physicalUvMaterial", errors);
        RequireEqual(contract.generatedMeshRoot, MeshRoot, "generatedMeshRoot", errors);
        RequireNear(contract.macroTileMeters, MacroTileMeters, 0.0001f, "macroTileMeters", errors);
        RequireNear(contract.detailTileMeters, DetailTileMeters, 0.0001f, "detailTileMeters", errors);
        RequireNear(contract.mainTextureScale, 1f, 0.0001f, "mainTextureScale", errors);
        RequireNear(contract.detailTextureScale, DetailScale, 0.0002f, "detailTextureScale", errors);
        Require(contract.qa.minimumFacadeCellRenderers == MinimumFacadeCellRenderers,
            $"minimumFacadeCellRenderers must be {MinimumFacadeCellRenderers}", errors);
        Require(contract.qa.minimumDistinctQuantizedUvPhasesAcrossApartmentCells == MinimumDistinctPhases,
            $"minimumDistinctQuantizedUvPhasesAcrossApartmentCells must be {MinimumDistinctPhases}", errors);
        RequireNear(contract.qa.quantizedPhaseStep, PhaseQuantization, 0.0001f, "quantizedPhaseStep", errors);
        Require(contract.qa.automaticVisualPoints == 0, "automaticVisualPoints must remain 0", errors);
        Require(contract.qa.renderVerificationPending, "renderVerificationPending must remain true", errors);
        Require(!string.IsNullOrWhiteSpace(contract.projection), "projection metadata is missing", errors);
        Require(!string.IsNullOrWhiteSpace(contract.phasePolicy), "phasePolicy metadata is missing", errors);
        Require(!string.IsNullOrWhiteSpace(contract.materialPolicy), "materialPolicy metadata is missing", errors);

        if (errors.Count > 0)
            throw new InvalidOperationException("Facade aperture UV contract FAILED:\n - " + string.Join("\n - ", errors));
    }

    [MenuItem("NewTown/QA/Validate World-Aligned Facade Aperture UVs")]
    public static void ValidateOpenScene()
    {
        EnsureSceneOpen();
        ValidateContractConfigOnly();
        GameObject root = FindSceneObject(RootName);
        if (root == null)
            throw new InvalidOperationException($"{RootName} is missing.");

        Material physical = AssetDatabase.LoadAssetAtPath<Material>(PhysicalMaterialPath);
        if (physical == null)
            throw new InvalidOperationException($"Physical facade UV material missing: {PhysicalMaterialPath}");
        ValidateMaterialScales(physical);

        MeshRenderer[] shellRenderers = root.GetComponentsInChildren<MeshRenderer>(true)
            .Where(r => r != null && r.enabled && r.gameObject.activeInHierarchy)
            .Where(r => !r.gameObject.name.StartsWith("FA_WindowSeal_", StringComparison.Ordinal))
            .OrderBy(r => HierarchyPath(r.transform), StringComparer.Ordinal)
            .ToArray();
        MeshRenderer[] facadeCells = shellRenderers
            .Where(r => r.gameObject.name.StartsWith("FA_FacadeCell_", StringComparison.Ordinal))
            .ToArray();

        if (facadeCells.Length < MinimumFacadeCellRenderers)
            throw new InvalidOperationException(
                $"World-aligned UV QA expected at least {MinimumFacadeCellRenderers} facade cell renderers, found {facadeCells.Length}.");

        var errors = new List<string>();
        foreach (MeshRenderer renderer in shellRenderers)
        {
            if (renderer.sharedMaterial != physical)
                errors.Add($"Facade shell renderer does not use the physical-UV material: {HierarchyPath(renderer.transform)}");
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null || !mesh.isReadable)
            {
                errors.Add($"Facade shell physical-UV mesh missing/unreadable: {HierarchyPath(renderer.transform)}");
                continue;
            }
            Vector2[] uv = mesh.uv;
            if (uv == null || uv.Length != mesh.vertexCount || uv.Length < 3)
                errors.Add($"Facade shell physical-UV mesh has invalid UV0: {HierarchyPath(renderer.transform)} / {mesh.name}");
            if (!AssetDatabase.GetAssetPath(mesh).StartsWith(MeshRoot + "/", StringComparison.Ordinal))
                errors.Add($"Facade shell mesh is not the persisted physical-UV asset: {HierarchyPath(renderer.transform)} / {mesh.name}");
        }

        // Sample one deterministic left-pier mesh from each apartment opening. Absolute Danchi-local UVs
        // should move phase across the 4.15 m bay / 2.55 m floor grid instead of restarting identically.
        var phases = new HashSet<string>(StringComparer.Ordinal);
        for (int floor = 0; floor < 5; floor++)
        for (int bay = 0; bay < 6; bay++)
        {
            string name = $"FA_FacadeCell_{floor}_{bay}_LeftPier";
            MeshRenderer renderer = facadeCells.FirstOrDefault(r => r.gameObject.name == name);
            if (renderer == null)
            {
                errors.Add($"Missing phase-probe facade cell: {name}");
                continue;
            }
            Vector2[] uv = renderer.GetComponent<MeshFilter>().sharedMesh.uv;
            Vector2 centroid = Vector2.zero;
            foreach (Vector2 p in uv) centroid += p;
            centroid /= Mathf.Max(1, uv.Length);
            float fx = Mathf.Repeat(centroid.x, 1f);
            float fy = Mathf.Repeat(centroid.y, 1f);
            int qx = Mathf.RoundToInt(fx / PhaseQuantization);
            int qy = Mathf.RoundToInt(fy / PhaseQuantization);
            phases.Add(qx + ":" + qy);
        }

        if (phases.Count < MinimumDistinctPhases)
            errors.Add(
                $"Facade cell UV phase diversity too low: {phases.Count} distinct quantized phases < {MinimumDistinctPhases}. " +
                "Repeated modules may restart the same concrete patch.");

        if (errors.Count > 0)
            throw new InvalidOperationException("World-aligned facade aperture UV QA FAILED:\n - " + string.Join("\n - ", errors));

        Debug.Log(
            $"World-aligned facade aperture UV QA passed: shellRenderers={shellRenderers.Length}, facadeCells={facadeCells.Length}, distinctCellPhases={phases.Count}, " +
            $"nominalMacroDensity≈{1024f / MacroTileMeters:0.0} texel/m. This is source readiness only; rendered repetition/shimmer remains unscored.");
    }

    private static Material GetOrCreatePhysicalMaterial(Material source)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(PhysicalMaterialPath);
        if (mat == null)
        {
            mat = new Material(source) { name = "MAT_FacadePaintedRC_AperturePhysicalUV" };
            AssetDatabase.CreateAsset(mat, PhysicalMaterialPath);
        }
        else
        {
            mat.CopyPropertiesFromMaterial(source);
            mat.shader = source.shader;
            mat.name = "MAT_FacadePaintedRC_AperturePhysicalUV";
        }

        foreach (string property in new[] { "_MainTex", "_BumpMap", "_MetallicGlossMap" })
            if (mat.HasProperty(property))
            {
                mat.SetTextureScale(property, Vector2.one);
                mat.SetTextureOffset(property, Vector2.zero);
            }
        if (mat.HasProperty("_DetailNormalMap"))
        {
            mat.SetTextureScale("_DetailNormalMap", new Vector2(DetailScale, DetailScale));
            mat.SetTextureOffset("_DetailNormalMap", Vector2.zero);
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Mesh BakeWorldAlignedUvMesh(Mesh source, Transform rendererTransform, Transform danchi)
    {
        Mesh mesh = UnityEngine.Object.Instantiate(source);
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        if (normals == null || normals.Length != vertices.Length)
        {
            mesh.RecalculateNormals();
            normals = mesh.normals;
        }

        var uv = new Vector2[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 worldP = rendererTransform.TransformPoint(vertices[i]);
            Vector3 p = danchi.InverseTransformPoint(worldP);
            Vector3 worldN = rendererTransform.TransformDirection(normals[i]).normalized;
            Vector3 n = danchi.InverseTransformDirection(worldN).normalized;
            Vector3 a = new Vector3(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z));

            if (a.z >= a.x && a.z >= a.y)
                uv[i] = new Vector2(p.x / MacroTileMeters, p.y / MacroTileMeters);
            else if (a.x >= a.y)
                uv[i] = new Vector2(p.z / MacroTileMeters, p.y / MacroTileMeters);
            else
                uv[i] = new Vector2(p.x / MacroTileMeters, p.z / MacroTileMeters);
        }

        mesh.uv = uv;
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void ValidateMaterialScales(Material mat)
    {
        foreach (string property in new[] { "_MainTex", "_BumpMap", "_MetallicGlossMap" })
            if (mat.HasProperty(property))
            {
                Vector2 scale = mat.GetTextureScale(property);
                if ((scale - Vector2.one).sqrMagnitude > 0.000001f)
                    throw new InvalidOperationException($"Physical facade material {property} scale must be 1, got {scale}.");
            }
        if (mat.HasProperty("_DetailNormalMap"))
        {
            Vector2 scale = mat.GetTextureScale("_DetailNormalMap");
            Vector2 expected = new Vector2(DetailScale, DetailScale);
            if ((scale - expected).sqrMagnitude > 0.0001f)
                throw new InvalidOperationException(
                    $"Physical facade detail-normal scale must be {expected}, got {scale}.");
        }
        if (mat.HasProperty("_Metallic") && mat.GetFloat("_Metallic") > 0.001f)
            throw new InvalidOperationException("Physical facade aperture RC material became metallic.");
        if (!mat.IsKeywordEnabled("_NORMALMAP") || !mat.IsKeywordEnabled("_DETAIL_MULX2"))
            throw new InvalidOperationException("Physical facade aperture material lost required primary/detail normal response.");
    }

    private static void EnsureSceneOpen()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() ||
            EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x != null && x.scene.IsValid() && x.scene.path == ScenePath && x.name == name);
    }

    private static string HierarchyPath(Transform t)
    {
        var parts = new List<string>();
        while (t != null)
        {
            parts.Add(t.name);
            t = t.parent;
        }
        parts.Reverse();
        return string.Join("/", parts);
    }

    private static string Sanitize(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name.Replace('/', '_').Replace('\\', '_');
    }

    private static string AbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrWhiteSpace(projectRoot))
            throw new InvalidOperationException("Could not resolve Unity project root for facade UV QA.");
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private static void Require(bool condition, string message, List<string> errors)
    {
        if (!condition) errors.Add(message);
    }

    private static void RequireEqual(string actual, string expected, string label, List<string> errors)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
            errors.Add($"{label} must be '{expected}', got '{actual}'.");
    }

    private static void RequireNear(float actual, float expected, float tolerance, string label, List<string> errors)
    {
        if (Mathf.Abs(actual - expected) > tolerance)
            errors.Add($"{label} must be {expected:F6}, got {actual:F6}.");
    }

    [Serializable]
    private sealed class Contract
    {
        public string scenePath;
        public string apertureRootName;
        public string sourceMaterial;
        public string physicalUvMaterial;
        public string generatedMeshRoot;
        public float macroTileMeters;
        public float detailTileMeters;
        public float mainTextureScale;
        public float detailTextureScale;
        public string projection;
        public string phasePolicy;
        public string materialPolicy;
        public QA qa;
    }

    [Serializable]
    private sealed class QA
    {
        public int minimumFacadeCellRenderers;
        public int minimumDistinctQuantizedUvPhasesAcrossApartmentCells;
        public float quantizedPhaseStep;
        public int automaticVisualPoints;
        public bool renderVerificationPending;
    }
}
