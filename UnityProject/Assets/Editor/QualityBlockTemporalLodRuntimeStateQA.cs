using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Fail-closed runtime guard for the LOD state that actually governs formal prepared temporal evidence.
///
/// Static LOD topology/physical-UV QA proves that the authored LODs are structurally coherent. This guard
/// closes the separate runtime-state gap: Unity can still globally disable cross-fading, skip LOD0, alter
/// lodBias, alter the global animated cross-fade duration, or mutate the LODGroup between formal frames.
///
/// While QualityBlockTemporalRuntimeEvidenceGuard is armed, every MainCamera pre-cull must match one
/// deterministic LOD-runtime fingerprint. This is provenance only. It awards zero Visual Fidelity points;
/// actual native-4K temporal pixels remain authoritative for visible pop/shimmer scoring.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockTemporalLodRuntimeStateQA
{
    public const string Algorithm = "temporal-lod-runtime-state-v1";

    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/temporal_lod_runtime_state_contract.json";
    private const string GatePath = "Assets/QA/visual_fidelity_gate.json";
    private const string DetailRootName = "DanchiHighDetail";
    private const float MinimumLodBias = 1.0f;
    private const float Epsilon = 0.0001f;

    private static readonly float[] ExpectedTransitionHeights = { 0.18f, 0.08f, 0.03f, 0.008f };
    private static readonly string[] ExpectedCriticalDefects =
    {
        "severe_aliasing_or_shimmer",
        "visible_lod_pop"
    };

    private static bool sequenceActive;
    private static string sequenceBaselineSha256;
    private static int sequencePreCullCheckCount;

    static QualityBlockTemporalLodRuntimeStateQA()
    {
        Camera.onPreCull -= OnCameraPreCull;
        Camera.onPreCull += OnCameraPreCull;
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
    }

    [MenuItem("NewTown/QA/Validate Temporal LOD Runtime State Contract")]
    public static void ValidateContractConfigOnly()
    {
        RuntimeContract contract = LoadJson<RuntimeContract>(ContractPath);
        if (contract == null || contract.schemaVersion != "1.0")
            throw new InvalidOperationException("Temporal LOD runtime-state contract is missing or unsupported; schema 1.0 is required.");
        if (contract.scenePath != ScenePath || contract.detailRootName != DetailRootName ||
            contract.authoritativeGuard != nameof(QualityBlockTemporalLodRuntimeStateQA) ||
            contract.fingerprintAlgorithm != Algorithm)
            throw new InvalidOperationException("Temporal LOD runtime-state contract identity drifted.");

        Requirements r = contract.requirements;
        if (r == null ||
            !r.requireQualityLodCrossFadeEnabled ||
            !r.requireMaximumLodLevelZero ||
            !Approximately(r.minimumLodBias, MinimumLodBias) ||
            !r.requirePositiveCrossFadeAnimationDuration ||
            !r.requireFourLods ||
            r.requiredFadeMode != LODFadeMode.CrossFade.ToString() ||
            !r.requireAnimatedCrossFading ||
            r.requiredTransitionHeights == null ||
            r.requiredTransitionHeights.Length != ExpectedTransitionHeights.Length ||
            !r.requireStrictRendererCountReduction ||
            !r.bindQualityLevelIdentity ||
            !r.bindQualityLodCrossFadeEnabled ||
            !r.bindLodBias ||
            !r.bindMaximumLodLevel ||
            !r.bindCrossFadeAnimationDuration ||
            !r.bindLodGroupSizeAndReferencePoint ||
            !r.bindLodRendererCountsAndAssignments ||
            !r.bindRootWorldTransform ||
            !r.requireSameStateBeforeEveryTemporalMainCameraCull ||
            !r.actualRenderRequiredForVisualPoints ||
            !r.manualPixelReviewRequired ||
            r.automaticVisualPoints != 0)
            throw new InvalidOperationException("Temporal LOD runtime-state requirements were weakened or are incomplete.");

        for (int i = 0; i < ExpectedTransitionHeights.Length; i++)
        {
            if (!Approximately(r.requiredTransitionHeights[i], ExpectedTransitionHeights[i]))
                throw new InvalidOperationException($"Temporal LOD transition contract drifted at LOD{i}: expected {ExpectedTransitionHeights[i]}, got {r.requiredTransitionHeights[i]}.");
        }

        string[] critical = (contract.criticalDefectRisksReduced ?? Array.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
        string[] expectedCritical = ExpectedCriticalDefects.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        if (!critical.SequenceEqual(expectedCritical, StringComparer.Ordinal))
            throw new InvalidOperationException("Temporal LOD runtime-state critical-defect IDs must exactly match visible_lod_pop and severe_aliasing_or_shimmer.");

        VisualGate gate = LoadJson<VisualGate>(GatePath);
        string[] gateCritical = (gate?.criticalDefects ?? Array.Empty<CriticalDefect>())
            .Where(x => x != null && !string.IsNullOrWhiteSpace(x.id))
            .Select(x => x.id)
            .ToArray();
        foreach (string id in ExpectedCriticalDefects)
        {
            if (!gateCritical.Contains(id, StringComparer.Ordinal))
                throw new InvalidOperationException($"Temporal LOD runtime-state contract references non-canonical Visual Fidelity critical defect '{id}'.");
        }

        Debug.Log("Temporal LOD runtime-state contract valid: global LOD policy + Danchi LOD selection state are pinned for formal temporal pre-cull; zero automatic visual points.");
    }

    [MenuItem("NewTown/QA/Validate Temporal LOD Runtime State")]
    public static void ValidateOpenScene()
    {
        ValidateContractConfigOnly();
        QualityBlockDetailLodPhysicalUvBindingQA.ValidateOpenScene();
        string sha = BuildCurrentSha256();
        Debug.Log($"Temporal LOD runtime state valid: {Algorithm} sha256={sha}. Visual Fidelity remains unscored until actual temporal render review.");
    }

    /// <summary>
    /// Returns a deterministic SHA-256 of every runtime LOD selector that can change which authored
    /// Danchi renderer set is visible during formal capture. The method is side-effect free and fail-closed.
    /// </summary>
    public static string BuildCurrentSha256()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
            throw new InvalidOperationException($"Temporal LOD runtime-state QA requires the benchmark scene: {ScenePath}");
        if (scene.isDirty)
            throw new InvalidOperationException("Temporal LOD runtime-state QA refuses a dirty benchmark scene; runtime evidence must bind the persisted prepared scene.");

        if (!QualitySettings.enableLODCrossFade)
            throw new InvalidOperationException("Formal temporal evidence requires QualitySettings.enableLODCrossFade=true; global LOD cross-fade is disabled.");
        if (QualitySettings.maximumLODLevel != 0)
            throw new InvalidOperationException($"Formal temporal evidence requires QualitySettings.maximumLODLevel=0 so LOD0 cannot be globally skipped; current={QualitySettings.maximumLODLevel}.");
        if (!IsFinite(QualitySettings.lodBias) || QualitySettings.lodBias < MinimumLodBias)
            throw new InvalidOperationException($"Formal temporal evidence requires finite QualitySettings.lodBias >= {MinimumLodBias.ToString("R", CultureInfo.InvariantCulture)}; current={QualitySettings.lodBias.ToString("R", CultureInfo.InvariantCulture)}.");
        if (!IsFinite(LODGroup.crossFadeAnimationDuration) || LODGroup.crossFadeAnimationDuration <= 0.0f)
            throw new InvalidOperationException($"Formal temporal evidence requires a positive finite LODGroup.crossFadeAnimationDuration; current={LODGroup.crossFadeAnimationDuration.ToString("R", CultureInfo.InvariantCulture)}.");

        Transform[] allTransforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Transform[] roots = allTransforms
            .Where(t => t != null && t.name == DetailRootName && t.gameObject.scene == scene)
            .ToArray();
        if (roots.Length != 1)
            throw new InvalidOperationException($"Temporal LOD runtime-state QA requires exactly one '{DetailRootName}' root; found {roots.Length}.");

        Transform root = roots[0];
        if (!root.gameObject.activeInHierarchy)
            throw new InvalidOperationException($"{DetailRootName} must be active for formal temporal evidence.");
        RequireFinite(root.position, "DanchiHighDetail world position");
        RequireFinite(root.rotation, "DanchiHighDetail world rotation");
        RequireFinite(root.lossyScale, "DanchiHighDetail world scale");

        LODGroup[] groups = root.GetComponents<LODGroup>();
        if (groups.Length != 1 || groups[0] == null)
            throw new InvalidOperationException($"{DetailRootName} must contain exactly one LODGroup; found {groups.Length}.");
        LODGroup group = groups[0];
        if (!group.enabled)
            throw new InvalidOperationException("DanchiHighDetail LODGroup must be enabled for formal temporal evidence.");
        if (group.fadeMode != LODFadeMode.CrossFade || !group.animateCrossFading)
            throw new InvalidOperationException($"DanchiHighDetail LODGroup must use animated CrossFade; fadeMode={group.fadeMode}, animateCrossFading={group.animateCrossFading}.");
        if (!IsFinite(group.size) || group.size <= 0.0f)
            throw new InvalidOperationException($"DanchiHighDetail LODGroup size must be positive and finite; current={group.size.ToString("R", CultureInfo.InvariantCulture)}.");
        RequireFinite(group.localReferencePoint, "DanchiHighDetail LODGroup localReferencePoint");

        LOD[] lods = group.GetLODs();
        if (lods == null || lods.Length != ExpectedTransitionHeights.Length)
            throw new InvalidOperationException($"DanchiHighDetail requires exactly four LOD levels; found {(lods == null ? 0 : lods.Length)}.");

        int previousRendererCount = int.MaxValue;
        var sb = new StringBuilder(32768);
        Append(sb, "algorithm", Algorithm);
        Append(sb, "scene", scene.path);
        Append(sb, "qualityLevelIndex", QualitySettings.GetQualityLevel().ToString(CultureInfo.InvariantCulture));
        string[] qualityNames = QualitySettings.names ?? Array.Empty<string>();
        int qualityIndex = QualitySettings.GetQualityLevel();
        string qualityName = qualityIndex >= 0 && qualityIndex < qualityNames.Length ? qualityNames[qualityIndex] : "<out-of-range>";
        Append(sb, "qualityLevelName", qualityName);
        Append(sb, "enableLODCrossFade", QualitySettings.enableLODCrossFade ? "1" : "0");
        Append(sb, "lodBias", F(QualitySettings.lodBias));
        Append(sb, "maximumLODLevel", QualitySettings.maximumLODLevel.ToString(CultureInfo.InvariantCulture));
        Append(sb, "crossFadeAnimationDuration", F(LODGroup.crossFadeAnimationDuration));
        Append(sb, "rootPosition", V(root.position));
        Append(sb, "rootRotation", Q(root.rotation));
        Append(sb, "rootLossyScale", V(root.lossyScale));
        Append(sb, "groupEnabled", group.enabled ? "1" : "0");
        Append(sb, "fadeMode", group.fadeMode.ToString());
        Append(sb, "animateCrossFading", group.animateCrossFading ? "1" : "0");
        Append(sb, "groupSize", F(group.size));
        Append(sb, "localReferencePoint", V(group.localReferencePoint));
        Append(sb, "lodCount", lods.Length.ToString(CultureInfo.InvariantCulture));

        for (int i = 0; i < lods.Length; i++)
        {
            LOD lod = lods[i];
            if (!Approximately(lod.screenRelativeTransitionHeight, ExpectedTransitionHeights[i]))
                throw new InvalidOperationException($"DanchiHighDetail LOD{i} transition drifted: expected {ExpectedTransitionHeights[i]}, got {lod.screenRelativeTransitionHeight}.");
            if (!IsFinite(lod.fadeTransitionWidth) || lod.fadeTransitionWidth < 0.0f || lod.fadeTransitionWidth > 1.0f)
                throw new InvalidOperationException($"DanchiHighDetail LOD{i} fadeTransitionWidth must be finite in [0,1]; current={lod.fadeTransitionWidth}.");

            Renderer[] renderers = lod.renderers ?? Array.Empty<Renderer>();
            if (renderers.Length <= 0)
                throw new InvalidOperationException($"DanchiHighDetail LOD{i} has no renderers.");
            if (i > 0 && renderers.Length >= previousRendererCount)
                throw new InvalidOperationException($"DanchiHighDetail renderer counts must strictly decrease across LODs; LOD{i - 1}={previousRendererCount}, LOD{i}={renderers.Length}.");
            previousRendererCount = renderers.Length;

            Append(sb, $"lod{i}.transition", F(lod.screenRelativeTransitionHeight));
            Append(sb, $"lod{i}.fadeWidth", F(lod.fadeTransitionWidth));
            Append(sb, $"lod{i}.rendererCount", renderers.Length.ToString(CultureInfo.InvariantCulture));

            string[] assignments = renderers
                .Select(r =>
                {
                    if (r == null)
                        throw new InvalidOperationException($"DanchiHighDetail LOD{i} contains a null renderer assignment.");
                    if (r.gameObject.scene != scene)
                        throw new InvalidOperationException($"DanchiHighDetail LOD{i} renderer '{r.name}' belongs to a different scene.");
                    return RelativePath(root, r.transform) + "|" + r.GetType().FullName;
                })
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();

            for (int j = 0; j < assignments.Length; j++)
                Append(sb, $"lod{i}.renderer[{j}]", assignments[j]);
        }

        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
        return string.Concat(digest.Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
    }

    public static void RequireCurrentMatch(string expectedSha256, string phase)
    {
        if (string.IsNullOrWhiteSpace(expectedSha256))
            throw new InvalidOperationException("Temporal LOD runtime-state expected SHA-256 is empty.");
        string current = BuildCurrentSha256();
        if (!string.Equals(current, expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Temporal LOD runtime state drifted during {phase}: expected={expectedSha256}, current={current}.");
    }

    private static void OnCameraPreCull(Camera cam)
    {
        if (!QualityBlockTemporalRuntimeEvidenceGuard.IsRunning || cam == null || cam != Camera.main)
            return;

        if (!sequenceActive)
        {
            ValidateContractConfigOnly();
            QualityBlockDetailLodPhysicalUvBindingQA.ValidateOpenScene();
            sequenceBaselineSha256 = BuildCurrentSha256();
            sequencePreCullCheckCount = 0;
            sequenceActive = true;
        }
        else
        {
            RequireCurrentMatch(sequenceBaselineSha256, $"formal temporal MainCamera pre-cull #{sequencePreCullCheckCount + 1}");
        }

        // Rebuild even for the first frame after the baseline was captured so every formal pre-cull
        // has an explicit validated state observation rather than relying only on initialization.
        RequireCurrentMatch(sequenceBaselineSha256, $"formal temporal MainCamera pre-cull #{sequencePreCullCheckCount + 1}");
        sequencePreCullCheckCount++;
    }

    private static void OnEditorUpdate()
    {
        if (!sequenceActive || QualityBlockTemporalRuntimeEvidenceGuard.IsRunning)
            return;

        Debug.Log($"Temporal LOD runtime-state guard completed {sequencePreCullCheckCount} formal MainCamera pre-cull checks under {Algorithm} sha256={sequenceBaselineSha256}. Runtime pixels remain unscored until review.");
        sequenceActive = false;
        sequenceBaselineSha256 = null;
        sequencePreCullCheckCount = 0;
    }

    private static string RelativePath(Transform root, Transform target)
    {
        if (target == root)
            return root.name;

        var parts = new System.Collections.Generic.List<string>();
        Transform current = target;
        while (current != null && current != root)
        {
            parts.Add(current.name);
            current = current.parent;
        }
        if (current != root)
            throw new InvalidOperationException($"Renderer '{target.name}' is not a descendant of {root.name}.");
        parts.Add(root.name);
        parts.Reverse();
        return string.Join("/", parts);
    }

    private static void Append(StringBuilder sb, string key, string value)
    {
        sb.Append(key).Append('=').Append(value ?? string.Empty).Append('\n');
    }

    private static string F(float value) => value.ToString("R", CultureInfo.InvariantCulture);
    private static string V(Vector3 value) => F(value.x) + "," + F(value.y) + "," + F(value.z);
    private static string Q(Quaternion value) => F(value.x) + "," + F(value.y) + "," + F(value.z) + "," + F(value.w);

    private static bool Approximately(float a, float b) => Mathf.Abs(a - b) <= Epsilon;
    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private static void RequireFinite(Vector3 value, string label)
    {
        if (!IsFinite(value.x) || !IsFinite(value.y) || !IsFinite(value.z))
            throw new InvalidOperationException(label + " contains a non-finite component.");
    }

    private static void RequireFinite(Quaternion value, string label)
    {
        if (!IsFinite(value.x) || !IsFinite(value.y) || !IsFinite(value.z) || !IsFinite(value.w))
            throw new InvalidOperationException(label + " contains a non-finite component.");
    }

    private static T LoadJson<T>(string assetPath) where T : class
    {
        string absolute = AbsolutePath(assetPath);
        if (!File.Exists(absolute))
            throw new InvalidOperationException("Missing required QA asset: " + assetPath);
        T value = JsonUtility.FromJson<T>(File.ReadAllText(absolute));
        if (value == null)
            throw new InvalidOperationException("Failed to parse required QA asset: " + assetPath);
        return value;
    }

    private static string AbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrWhiteSpace(projectRoot))
            throw new InvalidOperationException("Unable to resolve Unity project root.");
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    [Serializable]
    private sealed class RuntimeContract
    {
        public string schemaVersion;
        public string scenePath;
        public string detailRootName;
        public string authoritativeGuard;
        public string fingerprintAlgorithm;
        public Requirements requirements;
        public string[] criticalDefectRisksReduced;
    }

    [Serializable]
    private sealed class Requirements
    {
        public bool requireQualityLodCrossFadeEnabled;
        public bool requireMaximumLodLevelZero;
        public float minimumLodBias;
        public bool requirePositiveCrossFadeAnimationDuration;
        public bool requireFourLods;
        public string requiredFadeMode;
        public bool requireAnimatedCrossFading;
        public float[] requiredTransitionHeights;
        public bool requireStrictRendererCountReduction;
        public bool bindQualityLevelIdentity;
        public bool bindQualityLodCrossFadeEnabled;
        public bool bindLodBias;
        public bool bindMaximumLodLevel;
        public bool bindCrossFadeAnimationDuration;
        public bool bindLodGroupSizeAndReferencePoint;
        public bool bindLodRendererCountsAndAssignments;
        public bool bindRootWorldTransform;
        public bool requireSameStateBeforeEveryTemporalMainCameraCull;
        public bool actualRenderRequiredForVisualPoints;
        public bool manualPixelReviewRequired;
        public int automaticVisualPoints;
    }

    [Serializable]
    private sealed class VisualGate
    {
        public CriticalDefect[] criticalDefects;
    }

    [Serializable]
    private sealed class CriticalDefect
    {
        public string id;
    }
}
