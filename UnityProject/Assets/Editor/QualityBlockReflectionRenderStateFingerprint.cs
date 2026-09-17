using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Deterministic SHA-256 fingerprint for the renderable benchmark scene state that must remain
/// identical while realtime reflection cubemaps are in flight and through native-4K still capture.
///
/// This complements the physical lighting fingerprint. It deliberately hashes the active renderer
/// set, transforms, mesh identity/shape invariants, current material serialization and bound texture
/// sampling/dependency state. It is evidence-integrity infrastructure only: equality never awards
/// Visual Fidelity points and never clears a render-visible critical defect.
/// </summary>
public static class QualityBlockReflectionRenderStateFingerprint
{
    public const string Algorithm = "SHA-256";

    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/reflection_render_state_coherence_contract.json";

    private static readonly string[] TextureProperties =
    {
        "_MainTex",
        "_BaseMap",
        "_BumpMap",
        "_MetallicGlossMap",
        "_SpecGlossMap",
        "_OcclusionMap",
        "_EmissionMap",
        "_DetailAlbedoMap",
        "_DetailNormalMap",
        "_ParallaxMap"
    };

    // Source validators are relatively expensive. Re-run them only when the raw render-state hash
    // changes; the reflection waiter may call this on every Editor poll while RenderIDs are in flight.
    private static string lastSourceValidatedRawSha256;

    [MenuItem("NewTown/QA/Validate Reflection Render-State Coherence Contract")]
    public static void ValidateContractConfigOnly()
    {
        if (!File.Exists(ContractPath))
            throw new FileNotFoundException($"Reflection render-state coherence contract missing: {ContractPath}");

        RenderStateContract contract = JsonUtility.FromJson<RenderStateContract>(File.ReadAllText(ContractPath));
        if (contract == null)
            throw new InvalidOperationException("Reflection render-state coherence contract is null/unparseable.");
        if (!string.Equals(contract.schemaVersion, "1.0", StringComparison.Ordinal))
            throw new InvalidOperationException($"Unexpected reflection render-state coherence schemaVersion '{contract.schemaVersion}'.");
        if (!string.Equals(contract.fingerprintAlgorithm, Algorithm, StringComparison.Ordinal))
            throw new InvalidOperationException("Reflection render-state coherence fingerprint algorithm drifted.");
        if (!string.Equals(contract.scenePath, ScenePath, StringComparison.Ordinal))
            throw new InvalidOperationException("Reflection render-state coherence scene path drifted.");
        if (contract.visualFidelityPointsAwarded != 0 || contract.runtimeRenderVerified)
            throw new InvalidOperationException("Reflection render-state coherence may not award visual points or claim runtime verification from source configuration.");

        RenderStateRequirements r = contract.requirements;
        if (r == null ||
            !r.requirePersistedBenchmarkScene ||
            !r.requireSourcePhysicalityValidationBeforeFirstAcceptedHash ||
            !r.includeSceneAssetDependencyHash ||
            !r.includeActiveRendererIdentityAndHierarchy ||
            !r.includeRendererTransforms ||
            !r.includeRendererShadowProbeAndSortingState ||
            !r.includeMeshIdentityCountsBoundsAndAssetDependencyHash ||
            !r.includeCurrentSerializedMaterialState ||
            !r.includeMaterialAssetDependencyHash ||
            !r.includeTextureBindingsSamplingAndDependencyHash ||
            !r.requireSameHashAtReflectionRequestEveryObservedPollCompletionAndPreStill ||
            !r.actualRenderRequiredForVisualPoints)
            throw new InvalidOperationException("Reflection render-state coherence requirements were weakened or are incomplete.");
    }

    /// <summary>
    /// Returns the current render-state hash after proving the source-side physicality/sampling/metadata
    /// gates for this exact state. Unchanged states use the cached validation result so per-poll hashing
    /// does not repeatedly execute the expensive scene-wide validators.
    /// </summary>
    public static string BuildValidatedCurrentSha256()
    {
        ValidateContractConfigOnly();
        string before = BuildCurrentSha256();
        if (string.Equals(before, lastSourceValidatedRawSha256, StringComparison.OrdinalIgnoreCase))
            return before;

        ValidateSourceState();
        string after = BuildCurrentSha256();
        if (!string.Equals(before, after, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Source validators changed renderable scene/material state while establishing the reflection render-state baseline. " +
                "Use explicit Apply passes before synchronization; validation must be side-effect free.");

        lastSourceValidatedRawSha256 = after;
        return after;
    }

    /// <summary>
    /// Fast deterministic hash used at observed reflection-wait boundaries. No visual points are implied.
    /// </summary>
    public static string BuildCurrentSha256()
    {
        RequireQualityScene();

        var sb = new StringBuilder(131072);
        Append(sb, "schema", "reflection-render-state-v1");
        Append(sb, "scene.path", ScenePath);
        Append(sb, "unityVersion", Application.unityVersion);
        Append(sb, "scene.assetDependencyHash", DependencyHash(ScenePath));

        Renderer[] renderers = Resources.FindObjectsOfTypeAll<Renderer>()
            .Where(x => x != null &&
                        x.gameObject.scene.IsValid() &&
                        x.gameObject.scene.path == ScenePath &&
                        x.enabled &&
                        x.gameObject.activeInHierarchy)
            .OrderBy(x => HierarchyPath(x.transform), StringComparer.Ordinal)
            .ThenBy(x => x.GetType().FullName, StringComparer.Ordinal)
            .ToArray();

        Append(sb, "renderers.count", renderers.Length);

        var materialStateCache = new Dictionary<Material, string>();
        var meshStateCache = new Dictionary<Mesh, string>();

        for (int i = 0; i < renderers.Length; ++i)
        {
            Renderer renderer = renderers[i];
            string prefix = "renderer." + i.ToString(CultureInfo.InvariantCulture);
            GameObject go = renderer.gameObject;
            Transform t = renderer.transform;

            Append(sb, prefix + ".path", HierarchyPath(t));
            Append(sb, prefix + ".type", renderer.GetType().FullName ?? string.Empty);
            Append(sb, prefix + ".name", go.name);
            Append(sb, prefix + ".layer", go.layer);
            Append(sb, prefix + ".staticFlags", ((int)GameObjectUtility.GetStaticEditorFlags(go)).ToString(CultureInfo.InvariantCulture));
            Append(sb, prefix + ".enabled", renderer.enabled);
            Append(sb, prefix + ".active", go.activeInHierarchy);
            Append(sb, prefix + ".forceRenderingOff", renderer.forceRenderingOff);
            Append(sb, prefix + ".shadowCastingMode", renderer.shadowCastingMode.ToString());
            Append(sb, prefix + ".receiveShadows", renderer.receiveShadows);
            Append(sb, prefix + ".lightProbeUsage", renderer.lightProbeUsage.ToString());
            Append(sb, prefix + ".reflectionProbeUsage", renderer.reflectionProbeUsage.ToString());
            Append(sb, prefix + ".motionVectorGenerationMode", renderer.motionVectorGenerationMode.ToString());
            Append(sb, prefix + ".allowOcclusionWhenDynamic", renderer.allowOcclusionWhenDynamic);
            Append(sb, prefix + ".sortingLayerID", renderer.sortingLayerID);
            Append(sb, prefix + ".sortingOrder", renderer.sortingOrder);
            Append(sb, prefix + ".probeAnchor", renderer.probeAnchor == null ? string.Empty : HierarchyPath(renderer.probeAnchor));

            AppendVector3(sb, prefix + ".position", t.position);
            AppendQuaternion(sb, prefix + ".rotation", t.rotation);
            AppendVector3(sb, prefix + ".lossyScale", t.lossyScale);
            AppendVector3(sb, prefix + ".bounds.center", renderer.bounds.center);
            AppendVector3(sb, prefix + ".bounds.extents", renderer.bounds.extents);

            Mesh mesh = ResolveSharedMesh(renderer);
            if (mesh == null)
            {
                Append(sb, prefix + ".mesh", "<none>");
            }
            else
            {
                if (!meshStateCache.TryGetValue(mesh, out string meshState))
                {
                    meshState = BuildMeshState(mesh);
                    meshStateCache.Add(mesh, meshState);
                }
                Append(sb, prefix + ".meshStateSha256", Sha256Text(meshState));
            }

            Material[] materials = renderer.sharedMaterials ?? Array.Empty<Material>();
            Append(sb, prefix + ".materialSlots", materials.Length);
            for (int slot = 0; slot < materials.Length; ++slot)
            {
                Material material = materials[slot];
                string key = prefix + ".material." + slot.ToString(CultureInfo.InvariantCulture);
                if (material == null)
                {
                    Append(sb, key, "<null>");
                    continue;
                }

                if (!materialStateCache.TryGetValue(material, out string materialState))
                {
                    materialState = BuildMaterialState(material);
                    materialStateCache.Add(material, materialState);
                }
                Append(sb, key + ".name", material.name);
                Append(sb, key + ".stateSha256", Sha256Text(materialState));
            }
        }

        Append(sb, "uniqueMeshes.count", meshStateCache.Count);
        Append(sb, "uniqueMaterials.count", materialStateCache.Count);
        return Sha256Text(sb.ToString());
    }

    public static void RequireCurrentMatch(string expectedSha256, string phase)
    {
        if (string.IsNullOrWhiteSpace(expectedSha256) || expectedSha256.Length != 64)
            throw new InvalidOperationException($"{phase} render-state fingerprint is missing or malformed.");

        string current = BuildValidatedCurrentSha256();
        if (!string.Equals(current, expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Renderable scene/material state drifted before {phase}: expected {expectedSha256}, current {current}. " +
                "Reflection evidence is invalid and capture must abort.");
    }

    private static void ValidateSourceState()
    {
        QualityBlockSceneMaterialPhysicalityUpgrade.ValidateOpenScene();
        QualityBlockTextureSamplingUpgrade.ValidateOpenScene();
        QualityBlockPhysicalTexelDensityQA.ValidateOpenScene();
        QualityBlockAlbedoLightingNeutralityQA.ValidateGeneratedBaseAlbedos();
        QualityBlockSceneMetadataCoverageQA.ValidateOpenScene();
        QualityBlockBenchmarkPrimitiveExposureQA.ValidateOpenScene();
    }

    private static string BuildMeshState(Mesh mesh)
    {
        var sb = new StringBuilder(2048);
        string path = AssetDatabase.GetAssetPath(mesh);
        Append(sb, "name", mesh.name);
        Append(sb, "assetPath", path ?? string.Empty);
        Append(sb, "assetGuid", string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path));
        Append(sb, "assetDependencyHash", DependencyHash(path));
        Append(sb, "vertexCount", mesh.vertexCount);
        Append(sb, "subMeshCount", mesh.subMeshCount);
        Append(sb, "indexFormat", mesh.indexFormat.ToString());
        AppendVector3(sb, "bounds.center", mesh.bounds.center);
        AppendVector3(sb, "bounds.extents", mesh.bounds.extents);

        for (int subMesh = 0; subMesh < mesh.subMeshCount; ++subMesh)
        {
            Append(sb, $"submesh.{subMesh}.topology", mesh.GetTopology(subMesh).ToString());
            Append(sb, $"submesh.{subMesh}.indexCount", mesh.GetIndexCount(subMesh).ToString(CultureInfo.InvariantCulture));
            Append(sb, $"submesh.{subMesh}.baseVertex", mesh.GetBaseVertex(subMesh).ToString(CultureInfo.InvariantCulture));
        }

        // For generated in-memory meshes, authored-asset dependency hashing is unavailable. Record a
        // bounded deterministic vertex sample to detect common in-place edits without making each Editor
        // polling boundary scale with every vertex of every generated tree/building mesh.
        if (string.IsNullOrEmpty(path) && mesh.isReadable && mesh.vertexCount > 0)
        {
            Vector3[] vertices = mesh.vertices;
            int sampleCount = Mathf.Min(64, vertices.Length);
            Append(sb, "runtimeVertexSample.count", sampleCount);
            for (int sample = 0; sample < sampleCount; ++sample)
            {
                int index = sampleCount == 1 ? 0 : Mathf.RoundToInt(sample * (vertices.Length - 1f) / (sampleCount - 1f));
                Append(sb, $"runtimeVertexSample.{sample}.index", index);
                AppendVector3(sb, $"runtimeVertexSample.{sample}.value", vertices[index]);
            }
        }

        return sb.ToString();
    }

    private static string BuildMaterialState(Material material)
    {
        var sb = new StringBuilder(8192);
        string path = AssetDatabase.GetAssetPath(material);
        Append(sb, "name", material.name);
        Append(sb, "assetPath", path ?? string.Empty);
        Append(sb, "assetGuid", string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path));
        Append(sb, "assetDependencyHash", DependencyHash(path));
        Append(sb, "shader", material.shader == null ? string.Empty : material.shader.name);
        Append(sb, "renderQueue", material.renderQueue);
        Append(sb, "enableInstancing", material.enableInstancing);
        Append(sb, "doubleSidedGI", material.doubleSidedGI);
        Append(sb, "globalIlluminationFlags", material.globalIlluminationFlags.ToString());

        string[] keywords = material.shaderKeywords ?? Array.Empty<string>();
        Array.Sort(keywords, StringComparer.Ordinal);
        Append(sb, "keywords", string.Join(",", keywords));

        // Editor serialization captures the current in-memory saved-property state, including custom
        // shader scalar/vector/color values that a fixed Standard-only property list would miss.
        Append(sb, "editorSerializedState", EditorJsonUtility.ToJson(material, false));

        foreach (string property in TextureProperties)
        {
            if (!material.HasProperty(property))
                continue;

            Append(sb, "textureProperty", property);
            Texture texture = material.GetTexture(property);
            AppendVector2(sb, property + ".scale", material.GetTextureScale(property));
            AppendVector2(sb, property + ".offset", material.GetTextureOffset(property));
            if (texture == null)
            {
                Append(sb, property + ".texture", "<null>");
                continue;
            }

            string texturePath = AssetDatabase.GetAssetPath(texture);
            Append(sb, property + ".name", texture.name);
            Append(sb, property + ".type", texture.GetType().FullName ?? string.Empty);
            Append(sb, property + ".assetPath", texturePath ?? string.Empty);
            Append(sb, property + ".assetGuid", string.IsNullOrEmpty(texturePath) ? string.Empty : AssetDatabase.AssetPathToGUID(texturePath));
            Append(sb, property + ".assetDependencyHash", DependencyHash(texturePath));
            Append(sb, property + ".width", texture.width);
            Append(sb, property + ".height", texture.height);
            Append(sb, property + ".mipmapCount", texture.mipmapCount);
            Append(sb, property + ".filterMode", texture.filterMode.ToString());
            Append(sb, property + ".wrapMode", texture.wrapMode.ToString());
            Append(sb, property + ".anisoLevel", texture.anisoLevel);
        }

        return sb.ToString();
    }

    private static Mesh ResolveSharedMesh(Renderer renderer)
    {
        if (renderer is SkinnedMeshRenderer skinned)
            return skinned.sharedMesh;
        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        return filter == null ? null : filter.sharedMesh;
    }

    private static string HierarchyPath(Transform transform)
    {
        if (transform == null)
            return string.Empty;

        var segments = new Stack<string>();
        Transform current = transform;
        while (current != null && current.gameObject.scene.IsValid())
        {
            segments.Push(current.name + "[" + current.GetSiblingIndex().ToString(CultureInfo.InvariantCulture) + "]");
            current = current.parent;
        }
        return string.Join("/", segments);
    }

    private static string DependencyHash(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return string.Empty;
        return AssetDatabase.GetAssetDependencyHash(assetPath).ToString();
    }

    private static string Sha256Text(string text)
    {
        using var sha = SHA256.Create();
        byte[] payload = Encoding.UTF8.GetBytes(text ?? string.Empty);
        return BitConverter.ToString(sha.ComputeHash(payload)).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static void RequireQualityScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            throw new InvalidOperationException($"Reflection render-state fingerprint requires the persisted benchmark scene: {ScenePath}");
    }

    private static void AppendVector2(StringBuilder sb, string key, Vector2 value)
    {
        Append(sb, key + ".x", value.x);
        Append(sb, key + ".y", value.y);
    }

    private static void AppendVector3(StringBuilder sb, string key, Vector3 value)
    {
        Append(sb, key + ".x", value.x);
        Append(sb, key + ".y", value.y);
        Append(sb, key + ".z", value.z);
    }

    private static void AppendQuaternion(StringBuilder sb, string key, Quaternion value)
    {
        Append(sb, key + ".x", value.x);
        Append(sb, key + ".y", value.y);
        Append(sb, key + ".z", value.z);
        Append(sb, key + ".w", value.w);
    }

    private static void Append(StringBuilder sb, string key, float value)
    {
        Append(sb, key, value.ToString("R", CultureInfo.InvariantCulture));
    }

    private static void Append(StringBuilder sb, string key, int value)
    {
        Append(sb, key, value.ToString(CultureInfo.InvariantCulture));
    }

    private static void Append(StringBuilder sb, string key, bool value)
    {
        Append(sb, key, value ? "true" : "false");
    }

    private static void Append(StringBuilder sb, string key, string value)
    {
        sb.Append(key).Append('=').Append(value ?? string.Empty).Append('\n');
    }

    [Serializable]
    private sealed class RenderStateContract
    {
        public string schemaVersion;
        public string fingerprintAlgorithm;
        public string scenePath;
        public int visualFidelityPointsAwarded;
        public bool runtimeRenderVerified;
        public RenderStateRequirements requirements;
    }

    [Serializable]
    private sealed class RenderStateRequirements
    {
        public bool requirePersistedBenchmarkScene;
        public bool requireSourcePhysicalityValidationBeforeFirstAcceptedHash;
        public bool includeSceneAssetDependencyHash;
        public bool includeActiveRendererIdentityAndHierarchy;
        public bool includeRendererTransforms;
        public bool includeRendererShadowProbeAndSortingState;
        public bool includeMeshIdentityCountsBoundsAndAssetDependencyHash;
        public bool includeCurrentSerializedMaterialState;
        public bool includeMaterialAssetDependencyHash;
        public bool includeTextureBindingsSamplingAndDependencyHash;
        public bool requireSameHashAtReflectionRequestEveryObservedPollCompletionAndPreStill;
        public bool actualRenderRequiredForVisualPoints;
    }
}
