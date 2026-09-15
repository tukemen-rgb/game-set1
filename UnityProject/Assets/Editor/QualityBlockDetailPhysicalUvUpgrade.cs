using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Converts the authored high-detail danchi mesh UV0 field from face-normalized 0..1 coordinates to
/// manufacturing-scale metres. Repeated components receive deterministic cut-origin phase offsets so
/// identical apartment modules do not restart the same microtexture patch. Cylindrical sidewalls keep
/// axial metres and quantize circumference wrap to a whole texture repeat to avoid a visible seam.
/// This is source-side implementation readiness only; native 4K/temporal pixels remain authoritative.
/// </summary>
public static class QualityBlockDetailPhysicalUvUpgrade
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string DetailRootName = "DanchiHighDetail";
    private const string MeshRoot = "Assets/Art/GeneratedDetailPhysicalUvMeshes";
    private const float PhaseStepMeters = 0.017f;
    private const int PhaseBinsPerAxis = 8;

    private sealed class MaterialProfile
    {
        public readonly string name;
        public readonly float repeatsPerMeter;

        public MaterialProfile(string name, float repeatsPerMeter)
        {
            this.name = name;
            this.repeatsPerMeter = repeatsPerMeter;
        }
    }

    private static readonly MaterialProfile[] Profiles =
    {
        new MaterialProfile("MAT_AgedAluminum", 14f),
        new MaterialProfile("MAT_DarkGalvanizedSteel", 11f),
        new MaterialProfile("MAT_WindowRubber", 18f),
        new MaterialProfile("MAT_AgedACPlastic", 12f),
        new MaterialProfile("MAT_PipeInsulation", 8f),
        new MaterialProfile("MAT_DrainHose", 7f),
    };

    [MenuItem("NewTown/Materials/Apply Metric Phased UVs To Danchi Details")]
    public static void ApplyAndPersist()
    {
        EnsureSceneOpen();
        ApplyToOpenScene();
        QualityBlockDetailPhysicalUvQA.ValidateOpenScene();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Metric phased UVs applied to high-detail danchi. Visual Fidelity remains UNSCORED pending native 4K and temporal evidence.");
    }

    public static void ApplyToOpenScene()
    {
        EnsureSceneOpen();
        GameObject root = FindSceneObject(DetailRootName);
        if (root == null)
            throw new InvalidOperationException("DanchiHighDetail is missing. Build the authored bevel detail pass first.");

        Directory.CreateDirectory(AbsolutePath(MeshRoot));
        MeshRenderer[] renderers = SourceRenderers(root);
        if (renderers.Length == 0)
            throw new InvalidOperationException("DanchiHighDetail contains no source renderers for metric UV conversion.");

        var phases = new HashSet<string>(StringComparer.Ordinal);
        var materialNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (MeshRenderer renderer in renderers)
        {
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            Mesh source = filter != null ? filter.sharedMesh : null;
            if (source == null || !source.isReadable)
                throw new InvalidOperationException("Metric UV conversion requires a readable mesh: " + HierarchyPath(renderer.transform));
            if (!source.name.StartsWith("GM_HD_", StringComparison.Ordinal) &&
                !AssetDatabase.GetAssetPath(source).StartsWith(MeshRoot + "/", StringComparison.Ordinal))
                throw new InvalidOperationException("Metric UV conversion only accepts authored GM_HD detail meshes: " + source.name);

            Material material = renderer.sharedMaterial;
            MaterialProfile profile = ResolveProfile(material);
            materialNames.Add(profile.name);

            Phase phase = ResolvePhase(renderer.transform);
            phases.Add(phase.uBin + ":" + phase.vBin);
            Vector2 phaseMeters = new Vector2(phase.uBin * PhaseStepMeters, phase.vBin * PhaseStepMeters);

            string sourceBaseName = SourceBaseName(source.name);
            int repeatKey = Mathf.RoundToInt(profile.repeatsPerMeter * 100f);
            string assetPath = $"{MeshRoot}/{Sanitize(sourceBaseName)}_PU{phase.uBin}_PV{phase.vBin}_R{repeatKey}.asset";
            Mesh baked = BakeMetricUv(source, phaseMeters, profile.repeatsPerMeter);
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
                filter.sharedMesh = existing;
                EditorUtility.SetDirty(existing);
            }

            ApplyCanonicalTextureTransform(material, profile.repeatsPerMeter);
            EditorUtility.SetDirty(filter);
            EditorUtility.SetDirty(renderer);
        }

        QualityBlockDetailPhysicalUvManifest old = root.GetComponent<QualityBlockDetailPhysicalUvManifest>();
        if (old != null) UnityEngine.Object.DestroyImmediate(old);
        QualityBlockDetailPhysicalUvManifest manifest = root.AddComponent<QualityBlockDetailPhysicalUvManifest>();
        manifest.Configure(renderers.Length, phases.Count, materialNames.Count, PhaseStepMeters, PhaseBinsPerAxis);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
    }

    public static float RepeatsPerMeterFor(Material material)
    {
        return ResolveProfile(material).repeatsPerMeter;
    }

    public static bool IsCanonicalDetailMaterial(Material material)
    {
        if (material == null) return false;
        return Profiles.Any(p => string.Equals(p.name, material.name, StringComparison.Ordinal));
    }

    public static string MeshAssetRoot => MeshRoot;
    public static string DetailRoot => DetailRootName;
    public static float PhaseStep => PhaseStepMeters;
    public static int PhaseBins => PhaseBinsPerAxis;

    private static Mesh BakeMetricUv(Mesh source, Vector2 phaseMeters, float repeatsPerMeter)
    {
        Mesh mesh = UnityEngine.Object.Instantiate(source);
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        if (normals == null || normals.Length != vertices.Length)
        {
            mesh.RecalculateNormals();
            normals = mesh.normals;
        }

        Vector2[] uv = new Vector2[vertices.Length];
        bool[] cylindrical = new bool[vertices.Length];
        bool isCylinder = source.name.IndexOf("BevelCylinder", StringComparison.Ordinal) >= 0;
        Bounds bounds = mesh.bounds;

        if (isCylinder)
        {
            float radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
            float circumference = Mathf.Max(0.0001f, 2f * Mathf.PI * radius);
            int wrapRepeats = Mathf.Max(1, Mathf.RoundToInt(circumference * repeatsPerMeter));
            float wrapMeters = wrapRepeats / repeatsPerMeter;

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];
                Vector3 n = normals[i].normalized;
                if (Mathf.Abs(n.y) > 0.92f)
                {
                    // End caps are planar manufactured faces; preserve metre scale in local x/z.
                    uv[i] = phaseMeters + new Vector2(v.x, v.z);
                    continue;
                }

                float angle = Mathf.Atan2(v.z, v.x);
                if (angle < 0f) angle += Mathf.PI * 2f;
                uv[i] = phaseMeters + new Vector2(
                    angle / (Mathf.PI * 2f) * wrapMeters,
                    v.y - bounds.min.y);
                cylindrical[i] = true;
            }

            // The mesh library duplicates vertices per manufactured face. That allows the final seam
            // segment to unwrap independently without pulling any adjacent face across the texture.
            int[] triangles = mesh.triangles;
            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                int a = triangles[t];
                int b = triangles[t + 1];
                int c = triangles[t + 2];
                if (!cylindrical[a] || !cylindrical[b] || !cylindrical[c])
                    continue;

                float min = Mathf.Min(uv[a].x, Mathf.Min(uv[b].x, uv[c].x));
                float max = Mathf.Max(uv[a].x, Mathf.Max(uv[b].x, uv[c].x));
                if (max - min <= wrapMeters * 0.5f)
                    continue;

                float threshold = phaseMeters.x + wrapMeters * 0.5f;
                if (uv[a].x < threshold) uv[a].x += wrapMeters;
                if (uv[b].x < threshold) uv[b].x += wrapMeters;
                if (uv[c].x < threshold) uv[c].x += wrapMeters;
            }
        }
        else
        {
            Vector3[] preferredAxes = PreferredManufacturingAxes(bounds.size);
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 n = normals[i].normalized;
                Vector3 vAxis = Vector3.zero;
                foreach (Vector3 axis in preferredAxes)
                {
                    Vector3 projected = Vector3.ProjectOnPlane(axis, n);
                    if (projected.sqrMagnitude > 0.04f)
                    {
                        vAxis = projected.normalized;
                        break;
                    }
                }
                if (vAxis.sqrMagnitude < 0.5f)
                    vAxis = Vector3.ProjectOnPlane(Vector3.up, n).normalized;
                if (vAxis.sqrMagnitude < 0.5f)
                    vAxis = Vector3.ProjectOnPlane(Vector3.right, n).normalized;

                Vector3 uAxis = Vector3.Cross(vAxis, n).normalized;
                if (uAxis.sqrMagnitude < 0.5f)
                    throw new InvalidOperationException("Could not construct a stable tangent basis for metric detail UVs: " + source.name);

                uv[i] = phaseMeters + new Vector2(Vector3.Dot(vertices[i], uAxis), Vector3.Dot(vertices[i], vAxis));
            }
        }

        mesh.uv = uv;
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Vector3[] PreferredManufacturingAxes(Vector3 size)
    {
        var axes = new[]
        {
            new KeyValuePair<float, Vector3>(Mathf.Abs(size.x), Vector3.right),
            new KeyValuePair<float, Vector3>(Mathf.Abs(size.y), Vector3.up),
            new KeyValuePair<float, Vector3>(Mathf.Abs(size.z), Vector3.forward),
        };
        return axes.OrderByDescending(x => x.Key).Select(x => x.Value).ToArray();
    }

    private static void ApplyCanonicalTextureTransform(Material material, float repeatsPerMeter)
    {
        if (material == null)
            throw new InvalidOperationException("Detail renderer has no material.");
        Vector2 scale = Vector2.one * repeatsPerMeter;
        foreach (string property in new[] { "_MainTex", "_BumpMap", "_MetallicGlossMap" })
        {
            if (!material.HasProperty(property))
                throw new InvalidOperationException($"{material.name} is missing required Standard texture property {property}.");
            material.SetTextureScale(property, scale);
            material.SetTextureOffset(property, Vector2.zero);
        }
        EditorUtility.SetDirty(material);
    }

    private static MaterialProfile ResolveProfile(Material material)
    {
        if (material == null)
            throw new InvalidOperationException("High-detail danchi renderer is missing its canonical material.");
        MaterialProfile profile = Profiles.FirstOrDefault(p => string.Equals(p.name, material.name, StringComparison.Ordinal));
        if (profile == null)
            throw new InvalidOperationException("High-detail danchi renderer uses an unregistered detail material: " + material.name);
        return profile;
    }

    private static MeshRenderer[] SourceRenderers(GameObject root)
    {
        return root.GetComponentsInChildren<MeshRenderer>(true)
            .Where(r => r != null && r.gameObject.activeInHierarchy)
            .Where(r => !IsLodProxy(r.transform))
            .OrderBy(r => HierarchyPath(r.transform), StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsLodProxy(Transform transform)
    {
        for (Transform t = transform; t != null; t = t.parent)
            if (t.name.StartsWith("HD_LOD", StringComparison.Ordinal))
                return true;
        return false;
    }

    private struct Phase
    {
        public int uBin;
        public int vBin;
    }

    private static Phase ResolvePhase(Transform transform)
    {
        string path = HierarchyPath(transform);
        uint a = Fnv1a(path, 2166136261u);
        uint b = Fnv1a(path + "|v", 2166136261u);
        return new Phase
        {
            uBin = (int)(a % PhaseBinsPerAxis),
            vBin = (int)(b % PhaseBinsPerAxis),
        };
    }

    private static uint Fnv1a(string text, uint seed)
    {
        uint hash = seed;
        for (int i = 0; i < text.Length; i++)
        {
            hash ^= text[i];
            hash *= 16777619u;
        }
        return hash;
    }

    private static string SourceBaseName(string name)
    {
        int marker = name.IndexOf("_PU", StringComparison.Ordinal);
        return marker > 0 ? name.Substring(0, marker) : name;
    }

    private static string Sanitize(string value)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');
        return value.Replace(' ', '_');
    }

    private static string HierarchyPath(Transform transform)
    {
        var parts = new List<string>();
        for (Transform t = transform; t != null; t = t.parent)
            parts.Add(t.name);
        parts.Reverse();
        return string.Join("/", parts);
    }

    private static void EnsureSceneOpen()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static string AbsolutePath(string assetPath)
    {
        string root = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(root, assetPath));
    }

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x.scene.IsValid() && x.name == name);
    }
}

[DisallowMultipleComponent]
public sealed class QualityBlockDetailPhysicalUvManifest : MonoBehaviour
{
    [SerializeField] private int sourceRendererCount;
    [SerializeField] private int distinctPhasePairCount;
    [SerializeField] private int canonicalMaterialCount;
    [SerializeField] private float phaseStepMeters;
    [SerializeField] private int phaseBinsPerAxis;

    public int SourceRendererCount => sourceRendererCount;
    public int DistinctPhasePairCount => distinctPhasePairCount;
    public int CanonicalMaterialCount => canonicalMaterialCount;
    public float PhaseStepMeters => phaseStepMeters;
    public int PhaseBinsPerAxis => phaseBinsPerAxis;

    public void Configure(int renderers, int phases, int materials, float phaseStep, int phaseBins)
    {
        sourceRendererCount = renderers;
        distinctPhasePairCount = phases;
        canonicalMaterialCount = materials;
        phaseStepMeters = phaseStep;
        phaseBinsPerAxis = phaseBins;
    }
}
