using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Source-side physical texel-density preflight for the native 4K benchmark.
///
/// This deliberately measures only generated, textured MeshRenderer submeshes. It converts triangle UV
/// area (including the material's _MainTex tiling) and the bound Texture2D resolution into an area-weighted
/// texels-per-metre estimate in world space. The result is a hard source-asset adequacy floor, not a visual
/// score: native 100% crops and temporal evidence remain authoritative for perceived sharpness, repetition,
/// shimmer and material plausibility.
/// </summary>
public static class QualityBlockPhysicalTexelDensityQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string RootName = "QualityBlock1990s";
    private const string ContractPath = "Assets/QA/physical_texel_density_contract.json";
    private const string ManagedMaterialPrefix = "Assets/Art/Generated";
    private const int MinimumSamples = 6;
    private const float HardMinimumTexelsPerMeter = 256f;
    private const float PreferredHeroTexelsPerMeter = 512f;
    private const double AreaEpsilon = 1e-10;

    private sealed class Sample
    {
        public string RendererName;
        public string MaterialName;
        public string TextureName;
        public int Submesh;
        public double WorldArea;
        public double EffectiveUvArea;
        public float TexelsPerMeter;
    }

    [MenuItem("NewTown/QA/Validate Physical Texel Density Source Readiness")]
    public static void ValidateOpenScene()
    {
        ValidateContractConfigOnly();
        RequireQualityScene();

        GameObject root = GameObject.Find(RootName);
        if (root == null)
            throw new InvalidOperationException("Physical texel-density QA requires the QualityBlock1990s root.");

        var errors = new List<string>();
        var samples = new List<Sample>();
        MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true)
            .Where(r => r != null && r.enabled && r.gameObject.activeInHierarchy)
            .OrderBy(r => HierarchyPath(r.transform), StringComparer.Ordinal)
            .ToArray();

        foreach (MeshRenderer renderer in renderers)
            MeasureRenderer(renderer, samples, errors);

        if (samples.Count < MinimumSamples)
            errors.Add($"Physical texel-density QA measured too few generated textured submeshes: {samples.Count} < {MinimumSamples}.");

        foreach (Sample sample in samples.OrderBy(s => s.TexelsPerMeter))
        {
            if (!FinitePositive(sample.TexelsPerMeter))
            {
                errors.Add($"Invalid physical texel-density result on {sample.RendererName}/submesh {sample.Submesh}: {sample.TexelsPerMeter}.");
                continue;
            }

            if (sample.TexelsPerMeter + 0.001f < HardMinimumTexelsPerMeter)
            {
                errors.Add(
                    $"Generated textured surface is below the {HardMinimumTexelsPerMeter:0} texel/m source floor: " +
                    $"{sample.RendererName}/submesh {sample.Submesh}, material={sample.MaterialName}, texture={sample.TextureName}, " +
                    $"density={sample.TexelsPerMeter:0.0} texel/m, worldArea={sample.WorldArea:0.###} m^2. " +
                    "Increase authored source resolution or physically correct tiling; do not compensate with sharpening or baked highlights.");
            }
        }

        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Physical texel-density source QA FAILED:\n - " + string.Join("\n - ", errors));

        float minimum = samples.Min(s => s.TexelsPerMeter);
        float median = Percentile(samples.Select(s => s.TexelsPerMeter).OrderBy(v => v).ToArray(), 0.5f);
        int belowPreferred = samples.Count(s => s.TexelsPerMeter < PreferredHeroTexelsPerMeter);
        Debug.Log(
            $"Physical texel-density source QA passed: samples={samples.Count}, minimum={minimum:0.0} texel/m, " +
            $"median={median:0.0} texel/m, belowPreferred512={belowPreferred}. " +
            "This proves only a conservative source-density floor. Visual Fidelity remains UNSCORED until sealed native 4K crops and temporal evidence are reviewed.");
    }

    [MenuItem("NewTown/QA/Validate Physical Texel Density Contract")]
    public static void ValidateContractConfigOnly()
    {
        if (!File.Exists(ContractPath))
            throw new InvalidOperationException($"Missing required physical texel-density contract: {ContractPath}");

        string json = File.ReadAllText(ContractPath);
        string[] requiredTokens =
        {
            "\"contractVersion\": \"physical-texel-density-v1.0.0\"",
            "\"minimumMeasuredTexturedSubmeshSamples\": 6",
            "\"hardMinimumTexelsPerMeter\": 256",
            "\"preferredHeroSurfaceTexelsPerMeter\": 512",
            "\"materialTextureProperty\": \"_MainTex\"",
            "\"meshUvChannel\": 0",
            "\"visualFidelityPointsAwarded\": 0",
            "\"runtimeRenderVerification\": \"PENDING_UNITY_RUNTIME\""
        };

        foreach (string token in requiredTokens)
            if (json.IndexOf(token, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException($"Physical texel-density contract missing token: {token}");
    }

    private static void MeasureRenderer(MeshRenderer renderer, List<Sample> samples, List<string> errors)
    {
        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        Mesh mesh = filter != null ? filter.sharedMesh : null;
        if (mesh == null)
            return;

        Material[] materials = renderer.sharedMaterials ?? Array.Empty<Material>();
        int submeshCount = Math.Min(mesh.subMeshCount, materials.Length);
        if (submeshCount <= 0)
            return;

        bool hasManagedTexturedMaterial = false;
        for (int submesh = 0; submesh < submeshCount; submesh++)
        {
            Material material = materials[submesh];
            if (TryGetManagedMainTexture(material, out Texture2D texture))
            {
                hasManagedTexturedMaterial = true;
                break;
            }
        }

        if (!hasManagedTexturedMaterial)
            return;

        if (!mesh.isReadable)
        {
            errors.Add(
                $"Managed textured benchmark mesh is not readable, so physical texel density cannot be proven: {HierarchyPath(renderer.transform)} / {mesh.name}.");
            return;
        }

        Vector3[] vertices = mesh.vertices;
        Vector2[] uv = mesh.uv;
        if (vertices == null || vertices.Length < 3 || uv == null || uv.Length != vertices.Length)
        {
            errors.Add(
                $"Managed textured benchmark mesh has missing/incompatible UV0 data: {HierarchyPath(renderer.transform)} / {mesh.name}, " +
                $"vertices={(vertices == null ? 0 : vertices.Length)}, uv0={(uv == null ? 0 : uv.Length)}.");
            return;
        }

        Matrix4x4 localToWorld = renderer.localToWorldMatrix;
        for (int submesh = 0; submesh < submeshCount; submesh++)
        {
            Material material = materials[submesh];
            if (!TryGetManagedMainTexture(material, out Texture2D texture))
                continue;

            Vector2 tiling = material.GetTextureScale("_MainTex");
            double tilingArea = Math.Abs((double)tiling.x * tiling.y);
            if (!FiniteNonZero(tiling.x) || !FiniteNonZero(tiling.y) || tilingArea <= AreaEpsilon)
            {
                errors.Add(
                    $"Managed textured material has invalid physical tiling: {material.name} on {HierarchyPath(renderer.transform)} = {tiling}.");
                continue;
            }

            int[] triangles;
            try
            {
                triangles = mesh.GetTriangles(submesh);
            }
            catch (Exception ex)
            {
                errors.Add($"Could not read triangles for {mesh.name} submesh {submesh}: {ex.Message}");
                continue;
            }

            double worldArea = 0.0;
            double uvArea = 0.0;
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                int ia = triangles[i];
                int ib = triangles[i + 1];
                int ic = triangles[i + 2];
                if ((uint)ia >= (uint)vertices.Length || (uint)ib >= (uint)vertices.Length || (uint)ic >= (uint)vertices.Length)
                {
                    errors.Add($"Triangle index out of range on {mesh.name} submesh {submesh}.");
                    break;
                }

                Vector3 a = localToWorld.MultiplyPoint3x4(vertices[ia]);
                Vector3 b = localToWorld.MultiplyPoint3x4(vertices[ib]);
                Vector3 c = localToWorld.MultiplyPoint3x4(vertices[ic]);
                double triWorldArea = 0.5 * Vector3.Cross(b - a, c - a).magnitude;
                if (triWorldArea <= AreaEpsilon)
                    continue;

                Vector2 ua = uv[ia];
                Vector2 ub = uv[ib];
                Vector2 uc = uv[ic];
                Vector2 du1 = ub - ua;
                Vector2 du2 = uc - ua;
                double triUvArea = 0.5 * Math.Abs((double)du1.x * du2.y - (double)du1.y * du2.x);

                worldArea += triWorldArea;
                uvArea += triUvArea;
            }

            if (worldArea <= AreaEpsilon)
                continue;
            if (uvArea <= AreaEpsilon)
            {
                errors.Add(
                    $"Managed textured surface has no usable UV0 area: {HierarchyPath(renderer.transform)}/submesh {submesh}, material={material.name}.");
                continue;
            }

            double texelArea = uvArea * tilingArea * texture.width * (double)texture.height;
            float texelsPerMeter = (float)Math.Sqrt(texelArea / worldArea);
            samples.Add(new Sample
            {
                RendererName = HierarchyPath(renderer.transform),
                MaterialName = material.name,
                TextureName = texture.name + $"({texture.width}x{texture.height})",
                Submesh = submesh,
                WorldArea = worldArea,
                EffectiveUvArea = uvArea * tilingArea,
                TexelsPerMeter = texelsPerMeter
            });
        }
    }

    private static bool TryGetManagedMainTexture(Material material, out Texture2D texture)
    {
        texture = null;
        if (material == null || material.shader == null || !material.HasProperty("_MainTex"))
            return false;

        string materialPath = AssetDatabase.GetAssetPath(material);
        if (string.IsNullOrEmpty(materialPath) || !materialPath.StartsWith(ManagedMaterialPrefix, StringComparison.Ordinal))
            return false;

        texture = material.GetTexture("_MainTex") as Texture2D;
        return texture != null;
    }

    private static void RequireQualityScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            throw new InvalidOperationException(
                $"Physical texel-density QA requires the persisted benchmark scene to be open: {ScenePath}");
    }

    private static float Percentile(float[] sorted, float t)
    {
        if (sorted == null || sorted.Length == 0)
            return 0f;
        if (sorted.Length == 1)
            return sorted[0];
        float position = Mathf.Clamp01(t) * (sorted.Length - 1);
        int lo = Mathf.FloorToInt(position);
        int hi = Mathf.CeilToInt(position);
        return Mathf.Lerp(sorted[lo], sorted[hi], position - lo);
    }

    private static bool FinitePositive(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }

    private static bool FiniteNonZero(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && Mathf.Abs(value) > 0.000001f;
    }

    private static string HierarchyPath(Transform transform)
    {
        if (transform == null)
            return "<null>";
        var names = new List<string>();
        Transform current = transform;
        while (current != null)
        {
            names.Add(current.name);
            current = current.parent;
        }
        names.Reverse();
        return string.Join("/", names);
    }
}
