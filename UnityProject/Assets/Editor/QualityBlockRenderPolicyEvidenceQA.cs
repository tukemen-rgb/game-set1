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
using UnityEngine.Rendering;

/// <summary>
/// Fail-closed proof for global rendering policy that can alter benchmark pixels without changing
/// scene geometry, materials, lights or camera transforms.
///
/// The formal benchmark intentionally uses the Built-in Render Pipeline and full-resolution texture
/// residency. The proof freezes the current quality level, graphics tier, serialized Graphics/Quality
/// project settings and every active renderer-bound Texture2D residency policy. This closes otherwise
/// invisible routes such as switching Standard shader tier/reflection options or dropping top mips after
/// realtime reflection cubemaps were rendered.
///
/// This is evidence-integrity infrastructure only. It never awards Visual Fidelity points and cannot
/// substitute for native 3840x2160 still/crop/temporal review.
/// </summary>
public static class QualityBlockRenderPolicyEvidenceQA
{
    public const string Algorithm = "SHA-256";

    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/render_policy_evidence_contract.json";

    [MenuItem("NewTown/QA/Validate Render Policy Evidence Contract")]
    public static void ValidateContractConfigOnly()
    {
        if (!File.Exists(ContractPath))
            throw new FileNotFoundException("Render-policy evidence contract missing: " + ContractPath);

        RenderPolicyContract contract = JsonUtility.FromJson<RenderPolicyContract>(File.ReadAllText(ContractPath));
        if (contract == null || !string.Equals(contract.schemaVersion, "1.0", StringComparison.Ordinal))
            throw new InvalidOperationException("Render-policy evidence contract is null/unparseable or not schema 1.0.");
        if (!string.Equals(contract.scenePath, ScenePath, StringComparison.Ordinal))
            throw new InvalidOperationException("Render-policy evidence scene path drifted.");
        if (!string.Equals(contract.fingerprintAlgorithm, Algorithm, StringComparison.Ordinal))
            throw new InvalidOperationException("Render-policy evidence fingerprint algorithm drifted.");

        RenderPolicyRequirements r = contract.requirements;
        if (r == null ||
            !r.requireBuiltInRenderPipeline ||
            !r.requireLinearColorSpace ||
            !r.requireGlobalTextureMipmapLimitZero ||
            !r.forbidRendererBoundTextureStreaming ||
            !r.forbidEffectiveRendererBoundMipmapLimitGroups ||
            !r.includeCurrentQualityLevel ||
            !r.includeActiveGraphicsTier ||
            !r.includeSerializedGraphicsSettings ||
            !r.includeSerializedQualitySettings ||
            !r.includePipelineAssetIdentity ||
            !r.includeRendererBoundTextureResidencyState ||
            !r.requireSameHashAtReflectionRequestEveryObservedPollCompletionAndPreStill ||
            !r.actualRenderRequiredForVisualPoints)
            throw new InvalidOperationException("Render-policy evidence requirements were weakened or are incomplete.");

        if (contract.visualFidelityPointsAwarded != 0 || contract.runtimeRenderVerified)
            throw new InvalidOperationException("Render-policy source configuration may not award visual points or claim runtime verification.");
    }

    [MenuItem("NewTown/QA/Validate Render Policy Evidence In Open Scene")]
    public static void ValidateOpenScene()
    {
        ValidateContractConfigOnly();
        RequireQualityScene();

        var errors = new List<string>();
        if (GraphicsSettings.currentRenderPipeline != null)
            errors.Add("Formal benchmark requires the Built-in Render Pipeline; GraphicsSettings.currentRenderPipeline is not null.");
        if (GraphicsSettings.defaultRenderPipeline != null)
            errors.Add("Formal benchmark requires no default SRP asset; GraphicsSettings.defaultRenderPipeline is not null.");
        if (QualitySettings.renderPipeline != null)
            errors.Add("Formal benchmark requires no quality-level SRP override; QualitySettings.renderPipeline is not null.");
        if (QualitySettings.activeColorSpace != ColorSpace.Linear)
            errors.Add("Formal benchmark requires Linear color space.");
        if (QualitySettings.globalTextureMipmapLimit != 0)
            errors.Add(
                $"Formal native-4K evidence requires globalTextureMipmapLimit=0; current={QualitySettings.globalTextureMipmapLimit}. " +
                "Dropping top mips is not acceptable for 100% texture/microdetail review.");

        TextureBinding[] bindings = CollectActiveTextureBindings();
        if (bindings.Length < 10)
            errors.Add($"Render-policy evidence found too few active renderer-bound Texture2D bindings ({bindings.Length}); expected the detailed benchmark scene.");

        foreach (TextureBinding binding in bindings)
        {
            Texture2D texture = binding.Texture;
            if (texture == null || texture.mipmapCount <= 1)
                continue;

            // A texture that explicitly ignores mip limits is full-res regardless of global/group policy.
            // Otherwise, the formal packet requires both global limit zero and no per-texture limit group.
            if (!texture.ignoreMipmapLimit && !string.IsNullOrEmpty(texture.mipmapLimitGroup))
                errors.Add(
                    $"Renderer-bound texture {binding.Label} participates in mipmap-limit group '{texture.mipmapLimitGroup}'. " +
                    "Formal 100% crops require an unambiguous full-resolution residency policy.");

            string path = AssetDatabase.GetAssetPath(texture);
            if (!string.IsNullOrEmpty(path))
            {
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    errors.Add($"Texture importer unavailable for active renderer-bound texture {binding.Label} at {path}.");
                }
                else if (importer.streamingMipmaps)
                {
                    errors.Add(
                        $"Renderer-bound texture {binding.Label} enables mipmap streaming ({path}). " +
                        "Formal still/temporal evidence forbids camera-dependent residency changes between reflection and capture.");
                }
            }
        }

        if (errors.Count > 0)
            throw new InvalidOperationException("Render-policy evidence QA FAILED:\n - " + string.Join("\n - ", errors));
    }

    /// <summary>
    /// Validates full-resolution/Built-in policy, then returns a deterministic hash of the exact global
    /// render policy and renderer-bound texture residency state. The returned hash is intended to be
    /// folded into the existing reflection request/poll/completion/pre-still synchronization proof.
    /// </summary>
    public static string BuildValidatedCurrentSha256()
    {
        ValidateOpenScene();

        var sb = new StringBuilder(65536);
        Append(sb, "schema", "render-policy-evidence-v1");
        Append(sb, "scene", ScenePath);
        Append(sb, "unityVersion", Application.unityVersion);
        Append(sb, "quality.level", QualitySettings.GetQualityLevel());
        string[] qualityNames = QualitySettings.names ?? Array.Empty<string>();
        int qualityLevel = QualitySettings.GetQualityLevel();
        Append(sb, "quality.name", qualityLevel >= 0 && qualityLevel < qualityNames.Length ? qualityNames[qualityLevel] : string.Empty);
        Append(sb, "graphics.activeTier", Graphics.activeTier.ToString());
        Append(sb, "quality.activeColorSpace", QualitySettings.activeColorSpace.ToString());
        Append(sb, "quality.globalTextureMipmapLimit", QualitySettings.globalTextureMipmapLimit);
        Append(sb, "quality.streamingMipmapsActive", QualitySettings.streamingMipmapsActive);
        Append(sb, "quality.streamingMipmapsAddAllCameras", QualitySettings.streamingMipmapsAddAllCameras);
        Append(sb, "quality.streamingMipmapsMaxLevelReduction", QualitySettings.streamingMipmapsMaxLevelReduction);
        Append(sb, "quality.streamingMipmapsMemoryBudget", QualitySettings.streamingMipmapsMemoryBudget);
        Append(sb, "quality.streamingMipmapsMaxFileIORequests", QualitySettings.streamingMipmapsMaxFileIORequests);
        Append(sb, "quality.streamingMipmapsRenderersPerFrame", QualitySettings.streamingMipmapsRenderersPerFrame);

        AppendPipelineAsset(sb, "pipeline.current", GraphicsSettings.currentRenderPipeline);
        AppendPipelineAsset(sb, "pipeline.default", GraphicsSettings.defaultRenderPipeline);
        AppendPipelineAsset(sb, "pipeline.qualityOverride", QualitySettings.renderPipeline);

        UnityEngine.Object graphicsSettings = GraphicsSettings.GetGraphicsSettings();
        UnityEngine.Object qualitySettings = QualitySettings.GetQualitySettings();
        if (graphicsSettings == null || qualitySettings == null)
            throw new InvalidOperationException("Unity did not expose GraphicsSettings/QualitySettings objects for formal evidence hashing.");
        Append(sb, "graphicsSettings.serializedSha256", Sha256(EditorJsonUtility.ToJson(graphicsSettings, false)));
        Append(sb, "qualitySettings.serializedSha256", Sha256(EditorJsonUtility.ToJson(qualitySettings, false)));

        TextureBinding[] bindings = CollectActiveTextureBindings();
        Append(sb, "textureBindings.count", bindings.Length);
        for (int i = 0; i < bindings.Length; ++i)
        {
            TextureBinding binding = bindings[i];
            Texture2D texture = binding.Texture;
            string prefix = "textureBinding." + i.ToString(CultureInfo.InvariantCulture);
            Append(sb, prefix + ".label", binding.Label);
            Append(sb, prefix + ".width", texture.width);
            Append(sb, prefix + ".height", texture.height);
            Append(sb, prefix + ".mipmapCount", texture.mipmapCount);
            Append(sb, prefix + ".ignoreMipmapLimit", texture.ignoreMipmapLimit);
            Append(sb, prefix + ".mipmapLimitGroup", texture.mipmapLimitGroup ?? string.Empty);
            Append(sb, prefix + ".requestedMipmapLevel", texture.requestedMipmapLevel);

            string path = AssetDatabase.GetAssetPath(texture);
            Append(sb, prefix + ".assetPath", path ?? string.Empty);
            Append(sb, prefix + ".assetGuid", string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path));
            Append(sb, prefix + ".assetDependencyHash", DependencyHash(path));
            TextureImporter importer = string.IsNullOrEmpty(path) ? null : AssetImporter.GetAtPath(path) as TextureImporter;
            Append(sb, prefix + ".streamingMipmaps", importer != null && importer.streamingMipmaps);
            Append(sb, prefix + ".streamingMipmapsPriority", importer == null ? 0 : importer.streamingMipmapsPriority);
        }

        return Sha256(sb.ToString());
    }

    public static void RequireCurrentMatch(string expectedSha256, string phase)
    {
        if (string.IsNullOrWhiteSpace(expectedSha256) || expectedSha256.Length != 64)
            throw new InvalidOperationException($"{phase} render-policy fingerprint is missing or malformed.");
        string current = BuildValidatedCurrentSha256();
        if (!string.Equals(current, expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Global render/quality/texture-residency policy drifted before {phase}: expected {expectedSha256}, current {current}. " +
                "Reflection/still evidence is invalid and capture must abort.");
    }

    private static TextureBinding[] CollectActiveTextureBindings()
    {
        Renderer[] renderers = Resources.FindObjectsOfTypeAll<Renderer>()
            .Where(r => r != null && r.gameObject.scene.IsValid() && r.gameObject.scene.path == ScenePath &&
                        r.enabled && r.gameObject.activeInHierarchy)
            .OrderBy(r => HierarchyPath(r.transform), StringComparer.Ordinal)
            .ToArray();

        var bindings = new List<TextureBinding>();
        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.sharedMaterials ?? Array.Empty<Material>();
            for (int slot = 0; slot < materials.Length; ++slot)
            {
                Material material = materials[slot];
                if (material == null) continue;
                string[] properties = material.GetTexturePropertyNames() ?? Array.Empty<string>();
                Array.Sort(properties, StringComparer.Ordinal);
                foreach (string property in properties)
                {
                    Texture2D texture = material.GetTexture(property) as Texture2D;
                    if (texture == null) continue;
                    bindings.Add(new TextureBinding
                    {
                        Texture = texture,
                        Label = HierarchyPath(renderer.transform) + "|slot=" + slot.ToString(CultureInfo.InvariantCulture) +
                                "|material=" + material.name + "|property=" + property + "|texture=" + texture.name
                    });
                }
            }
        }

        return bindings.OrderBy(x => x.Label, StringComparer.Ordinal).ToArray();
    }

    private static void AppendPipelineAsset(StringBuilder sb, string prefix, RenderPipelineAsset asset)
    {
        if (asset == null)
        {
            Append(sb, prefix + ".assigned", false);
            return;
        }

        Append(sb, prefix + ".assigned", true);
        Append(sb, prefix + ".type", asset.GetType().FullName ?? string.Empty);
        string path = AssetDatabase.GetAssetPath(asset);
        Append(sb, prefix + ".assetPath", path ?? string.Empty);
        Append(sb, prefix + ".assetGuid", string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path));
        Append(sb, prefix + ".assetDependencyHash", DependencyHash(path));
    }

    private static string HierarchyPath(Transform transform)
    {
        var stack = new Stack<string>();
        Transform current = transform;
        while (current != null && current.gameObject.scene.IsValid())
        {
            stack.Push(current.name + "[" + current.GetSiblingIndex().ToString(CultureInfo.InvariantCulture) + "]");
            current = current.parent;
        }
        return string.Join("/", stack);
    }

    private static string DependencyHash(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath)) return string.Empty;
        return AssetDatabase.GetAssetDependencyHash(assetPath).ToString();
    }

    private static string Sha256(string text)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? string.Empty)))
            .Replace("-", string.Empty).ToLowerInvariant();
    }

    private static void RequireQualityScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            throw new InvalidOperationException("Render-policy evidence requires the persisted benchmark scene: " + ScenePath);
    }

    private static void Append(StringBuilder sb, string key, bool value) => Append(sb, key, value ? "true" : "false");
    private static void Append(StringBuilder sb, string key, int value) => Append(sb, key, value.ToString(CultureInfo.InvariantCulture));
    private static void Append(StringBuilder sb, string key, float value) => Append(sb, key, value.ToString("R", CultureInfo.InvariantCulture));
    private static void Append(StringBuilder sb, string key, string value) => sb.Append(key).Append('=').Append(value ?? string.Empty).Append('\n');

    private sealed class TextureBinding
    {
        public Texture2D Texture;
        public string Label;
    }

    [Serializable]
    private sealed class RenderPolicyContract
    {
        public string schemaVersion;
        public string scenePath;
        public string fingerprintAlgorithm;
        public RenderPolicyRequirements requirements;
        public int visualFidelityPointsAwarded;
        public bool runtimeRenderVerified;
    }

    [Serializable]
    private sealed class RenderPolicyRequirements
    {
        public bool requireBuiltInRenderPipeline;
        public bool requireLinearColorSpace;
        public bool requireGlobalTextureMipmapLimitZero;
        public bool forbidRendererBoundTextureStreaming;
        public bool forbidEffectiveRendererBoundMipmapLimitGroups;
        public bool includeCurrentQualityLevel;
        public bool includeActiveGraphicsTier;
        public bool includeSerializedGraphicsSettings;
        public bool includeSerializedQualitySettings;
        public bool includePipelineAssetIdentity;
        public bool includeRendererBoundTextureResidencyState;
        public bool requireSameHashAtReflectionRequestEveryObservedPollCompletionAndPreStill;
        public bool actualRenderRequiredForVisualPoints;
    }
}
