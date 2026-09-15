using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Deterministic SHA-256 fingerprint for renderer-to-lightmap bindings and the baked lightmap array used
/// by formal reflection/still evidence. Renderer.lightmapIndex / lightmapScaleOffset and their realtime
/// counterparts can change the illumination seen by scoreable geometry without changing its mesh,
/// material, transform or shared-lighting configuration. The general render-state fingerprint already
/// covers those other channels; this companion closes the per-renderer GI-binding gap and is folded into
/// the existing reflection lighting hash at every request/poll/completion/pre-still boundary.
///
/// This class is evidence-integrity infrastructure only. Hash equality awards zero Visual Fidelity points
/// and cannot substitute for native 3840x2160 rendered evidence plus 100% crops.
/// </summary>
public static class QualityBlockReflectionLightmapBindingFingerprint
{
    public const string Algorithm = "SHA-256";

    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/reflection_render_state_coherence_contract.json";
    private const string RequiredExtensionToken = "\"lightmapBindingExtensionVersion\": \"1.0\"";
    private const string RequiredRendererBindingToken = "\"includeRendererLightmapAndRealtimeLightmapBindings\": true";
    private const string RequiredArrayToken = "\"includeLightmapArrayModeAndTextureDependencies\": true";
    private const string RequiredPersistentToken = "\"rejectNonPersistentLightmapTextures\": true";

    [MenuItem("NewTown/QA/Validate Reflection Lightmap Binding Fingerprint Contract")]
    public static void ValidateContractConfigOnly()
    {
        if (!File.Exists(ContractPath))
            throw new FileNotFoundException("Reflection render-state coherence contract missing: " + ContractPath);

        string json = File.ReadAllText(ContractPath);
        RequireContractToken(json, RequiredExtensionToken, "lightmap binding extension version");
        RequireContractToken(json, RequiredRendererBindingToken, "renderer lightmap/realtime-lightmap binding coverage");
        RequireContractToken(json, RequiredArrayToken, "LightmapSettings array/mode coverage");
        RequireContractToken(json, RequiredPersistentToken, "non-persistent lightmap texture rejection");
    }

    /// <summary>
    /// Hashes only persisted benchmark-scene lightmap evidence. Active renderer selection intentionally
    /// matches the main reflection render-state fingerprint. A non-persistent lightmap texture fails closed
    /// because its provenance/dependency state cannot be reproduced after an Editor restart.
    /// </summary>
    public static string BuildCurrentSha256()
    {
        RequireQualityScene();
        ValidateContractConfigOnly();

        var sb = new StringBuilder(32768);
        Append(sb, "schema", "reflection-lightmap-binding-v1");
        Append(sb, "scene", ScenePath);
        Append(sb, "unityVersion", Application.unityVersion);
        Append(sb, "lightmapsMode", LightmapSettings.lightmapsMode.ToString());

        LightmapData[] lightmaps = LightmapSettings.lightmaps ?? Array.Empty<LightmapData>();
        Append(sb, "lightmaps.count", lightmaps.Length);
        for (int i = 0; i < lightmaps.Length; ++i)
        {
            LightmapData data = lightmaps[i];
            string prefix = "lightmap." + i.ToString(CultureInfo.InvariantCulture);
            if (data == null)
            {
                Append(sb, prefix, "<null>");
                continue;
            }

            AppendTextureIdentity(sb, prefix + ".color", data.lightmapColor);
            AppendTextureIdentity(sb, prefix + ".direction", data.lightmapDir);
            AppendTextureIdentity(sb, prefix + ".shadowMask", data.shadowMask);
        }

        Renderer[] renderers = Resources.FindObjectsOfTypeAll<Renderer>()
            .Where(x => x != null &&
                        x.gameObject.scene.IsValid() &&
                        string.Equals(x.gameObject.scene.path, ScenePath, StringComparison.Ordinal) &&
                        x.enabled &&
                        x.gameObject.activeInHierarchy)
            .OrderBy(x => HierarchyPath(x.transform), StringComparer.Ordinal)
            .ThenBy(x => x.GetType().FullName, StringComparer.Ordinal)
            .ToArray();

        Append(sb, "renderers.count", renderers.Length);
        for (int i = 0; i < renderers.Length; ++i)
        {
            Renderer renderer = renderers[i];
            string prefix = "renderer." + i.ToString(CultureInfo.InvariantCulture);
            Append(sb, prefix + ".path", HierarchyPath(renderer.transform));
            Append(sb, prefix + ".type", renderer.GetType().FullName ?? string.Empty);
            Append(sb, prefix + ".lightmapIndex", renderer.lightmapIndex);
            AppendVector4(sb, prefix + ".lightmapScaleOffset", renderer.lightmapScaleOffset);
            Append(sb, prefix + ".realtimeLightmapIndex", renderer.realtimeLightmapIndex);
            AppendVector4(sb, prefix + ".realtimeLightmapScaleOffset", renderer.realtimeLightmapScaleOffset);
        }

        using var sha = SHA256.Create();
        byte[] payload = Encoding.UTF8.GetBytes(sb.ToString());
        return BitConverter.ToString(sha.ComputeHash(payload)).Replace("-", string.Empty).ToLowerInvariant();
    }

    public static void RequireCurrentMatch(string expectedSha256, string phase)
    {
        if (string.IsNullOrWhiteSpace(expectedSha256) || expectedSha256.Length != 64)
            throw new InvalidOperationException(phase + " lightmap-binding fingerprint is missing or malformed.");

        string current = BuildCurrentSha256();
        if (!string.Equals(current, expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Renderer/lightmap GI binding state drifted before " + phase + ": expected " + expectedSha256 +
                ", current " + current + ". Reflection/still evidence is invalid and capture must abort.");
    }

    private static void AppendTextureIdentity(StringBuilder sb, string key, Texture texture)
    {
        if (texture == null)
        {
            Append(sb, key, "<null>");
            return;
        }

        string path = AssetDatabase.GetAssetPath(texture);
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException(
                "Formal reflection evidence contains a non-persistent lightmap texture at " + key +
                " ('" + texture.name + "'). Save/bake the lightmap as a project asset before capture.");

        string guid = AssetDatabase.AssetPathToGUID(path);
        if (string.IsNullOrWhiteSpace(guid))
            throw new InvalidOperationException("Could not resolve lightmap texture GUID for " + path + ".");

        Append(sb, key + ".name", texture.name);
        Append(sb, key + ".type", texture.GetType().FullName ?? string.Empty);
        Append(sb, key + ".path", path);
        Append(sb, key + ".guid", guid);
        Append(sb, key + ".dependencyHash", AssetDatabase.GetAssetDependencyHash(path).ToString());
        Append(sb, key + ".width", texture.width);
        Append(sb, key + ".height", texture.height);
        Append(sb, key + ".mipmapCount", texture.mipmapCount);
        Append(sb, key + ".filterMode", texture.filterMode.ToString());
        Append(sb, key + ".wrapMode", texture.wrapMode.ToString());
        Append(sb, key + ".anisoLevel", texture.anisoLevel);
    }

    private static void RequireContractToken(string json, string token, string label)
    {
        if (string.IsNullOrWhiteSpace(json) || json.IndexOf(token, StringComparison.Ordinal) < 0)
            throw new InvalidOperationException(
                "Reflection render-state coherence contract no longer requires " + label + ".");
    }

    private static void RequireQualityScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() ||
            !string.Equals(EditorSceneManager.GetActiveScene().path, ScenePath, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Reflection lightmap-binding fingerprint requires the persisted benchmark scene: " + ScenePath);
    }

    private static string HierarchyPath(Transform transform)
    {
        if (transform == null)
            return string.Empty;

        var segments = new System.Collections.Generic.Stack<string>();
        Transform current = transform;
        while (current != null && current.gameObject.scene.IsValid())
        {
            segments.Push(current.name + "[" + current.GetSiblingIndex().ToString(CultureInfo.InvariantCulture) + "]");
            current = current.parent;
        }
        return string.Join("/", segments);
    }

    private static void AppendVector4(StringBuilder sb, string key, Vector4 value)
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

    private static void Append(StringBuilder sb, string key, string value)
    {
        sb.Append(key).Append('=').Append(value ?? string.Empty).Append('\n');
    }
}
