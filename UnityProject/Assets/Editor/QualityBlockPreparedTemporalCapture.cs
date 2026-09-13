using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Authoritative temporal capture path for the complete native-4K review packet.
/// Unlike the legacy standalone temporal capture command, this path never rebuilds or reopens the
/// benchmark after the accepted asynchronous reflection synchronization. It captures the two required
/// temporal probes from the exact already-prepared scene state, writes the same temporal manifest/receipt
/// format consumed by QualityBlockTemporalStabilityCapture validation, and adds a SHA-256 binding receipt
/// tying that manifest to the current persisted scene plus the reflection completion/wait proofs.
/// No visual points are awarded here.
/// </summary>
public static class QualityBlockPreparedTemporalCapture
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string TemporalContractPath = "Assets/QA/temporal_stability_contract.json";
    private const string PreparedContractPath = "Assets/QA/temporal_prepared_capture_contract.json";
    private const string ReflectionReceiptPath = "Assets/QA/reflection_probe_refresh_receipt.json";
    private const string ReflectionWaitProofPath = "Assets/QA/reflection_probe_async_wait_receipt.json";
    private const string OutputRoot = "Assets/QA/Temporal4K";
    private const string ManifestPath = "Assets/QA/temporal_stability_manifest.json";
    private const string ReceiptPath = "Assets/QA/temporal_stability_receipt.json";
    private const string BindingPath = "Assets/QA/temporal_prepared_scene_binding.json";
    private const int Width = 3840;
    private const int Height = 2160;

    private static readonly Vector3 GrazingPosition = new Vector3(-22.0f, 3.0f, 7.0f);
    private static readonly Vector3 GrazingTarget = new Vector3(-8.2f, 3.8f, -7.8f);
    private static readonly Vector3 ObliquePosition = new Vector3(18.0f, 3.4f, 12.5f);
    private static readonly Vector3 ObliqueTarget = new Vector3(-6.0f, 4.0f, -8.3f);

    [MenuItem("NewTown/QA/Validate Prepared Temporal Capture Contract")]
    public static void ValidateContractConfigOnly()
    {
        PreparedContract contract = LoadJson<PreparedContract>(PreparedContractPath);
        ValidatePreparedContract(contract);
        QualityBlockTemporalStabilityCapture.ValidateContractConfigOnly();
        Debug.Log("Prepared temporal capture contract valid: no scene rebuild/reopen after async reflection synchronization; zero automatic visual points.");
    }

    /// <summary>
    /// Captures temporal evidence from the current prepared scene only. Caller must have completed the
    /// asynchronous reflection wait already. This method intentionally contains no build/apply/open calls.
    /// </summary>
    public static void CaptureAndSealPreparedScene()
    {
        ValidateContractConfigOnly();
        ValidatePreparedSceneWithoutMutation();

        // Fail closed if the accepted reflection state is not the asynchronous, fresh completion proof.
        QualityBlockReflectionProbeCaptureSyncQA.ValidateRuntimeReceipt();
        QualityBlockReflectionProbeAwaiter.ValidateLatestWaitProof();

        string sceneAbsolute = AbsolutePath(ScenePath);
        string reflectionReceiptAbsolute = AbsolutePath(ReflectionReceiptPath);
        string reflectionWaitAbsolute = AbsolutePath(ReflectionWaitProofPath);
        if (!File.Exists(sceneAbsolute) || !File.Exists(reflectionReceiptAbsolute) || !File.Exists(reflectionWaitAbsolute))
            throw new InvalidOperationException("Prepared temporal capture is missing scene/reflection proof files.");

        string sceneSha = Sha256File(sceneAbsolute);
        string reflectionReceiptSha = Sha256File(reflectionReceiptAbsolute);
        string reflectionWaitSha = Sha256File(reflectionWaitAbsolute);

        Camera cam = Camera.main;
        if (cam == null)
            throw new InvalidOperationException("Prepared temporal capture failed: MainCamera not found.");

        LODGroup[] lodGroups = UnityEngine.Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None);
        int fourLevelGroups = lodGroups.Count(x => x != null && x.GetLODs().Length >= 4);
        int animatedCrossFadeGroups = lodGroups.Count(x => x != null && x.animateCrossFading && x.GetLODs().Length >= 4);
        if (fourLevelGroups == 0 || animatedCrossFadeGroups == 0)
            throw new InvalidOperationException("Prepared temporal capture requires at least one four-level animated-crossfade LODGroup.");

        TemporalContract temporal = LoadJson<TemporalContract>(TemporalContractPath);
        ValidateTemporalContractShape(temporal);

        string sessionId = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        string sessionDir = OutputRoot + "/" + sessionId;
        Directory.CreateDirectory(AbsolutePath(sessionDir));

        Vector3 originalPosition = cam.transform.position;
        Quaternion originalRotation = cam.transform.rotation;
        float originalFov = cam.fieldOfView;
        float originalAspect = cam.aspect;
        RenderTexture originalTarget = cam.targetTexture;
        RenderTexture originalActive = RenderTexture.active;

        var probeRecords = new List<ProbeRecord>();
        try
        {
            foreach (ProbeSpec spec in temporal.probes)
                probeRecords.Add(CaptureProbe(cam, spec, sessionDir));
        }
        finally
        {
            cam.transform.position = originalPosition;
            cam.transform.rotation = originalRotation;
            cam.fieldOfView = originalFov;
            cam.aspect = originalAspect;
            cam.targetTexture = originalTarget;
            RenderTexture.active = originalActive;
        }

        // Reconfirm the persisted scene itself was not rewritten during the temporal pass.
        string sceneShaAfter = Sha256File(sceneAbsolute);
        if (!EqualsSha(sceneSha, sceneShaAfter))
            throw new InvalidOperationException("Prepared temporal capture mutated the persisted benchmark scene; temporal evidence is rejected.");

        var manifest = new TemporalManifest
        {
            schemaVersion = "1.1-prepared",
            captureSessionId = sessionId,
            generatedUtc = DateTime.UtcNow.ToString("O"),
            unityVersion = Application.unityVersion,
            graphicsDevice = SystemInfo.graphicsDeviceName,
            graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
            projectColorSpace = QualitySettings.activeColorSpace.ToString(),
            width = Width,
            height = Height,
            source = "prepared benchmark scene; Unity Camera.Render -> MSAA RenderTexture -> single-sample resolve -> Texture2D.ReadPixels",
            renderProducedByUnity = true,
            visualFidelityStatus = "UNSCORED_REVIEW_REQUIRED",
            lodBias = QualitySettings.lodBias,
            lodGroupCount = lodGroups.Length,
            fourLevelLodGroupCount = fourLevelGroups,
            animatedCrossFadeFourLevelGroupCount = animatedCrossFadeGroups,
            lodGroups = BuildLodSummaries(lodGroups),
            probes = probeRecords.ToArray(),
            captureMode = "PREPARED_SCENE_ASYNC_REFLECTION_BOUND_NO_REBUILD",
            scenePath = ScenePath,
            sceneAssetSha256 = sceneSha,
            reflectionCompletionReceiptSha256 = reflectionReceiptSha,
            reflectionAsyncWaitProofSha256 = reflectionWaitSha,
            note = "Temporal evidence was captured without rebuilding/reopening after the accepted asynchronous reflection synchronization. Manual 100%-pixel review is still mandatory; zero Visual Fidelity points are automatic."
        };
        WriteJson(ManifestPath, manifest);

        string manifestSha = Sha256File(AbsolutePath(ManifestPath));
        var receipt = new TemporalReceipt
        {
            schemaVersion = "1.1-prepared",
            captureSessionId = sessionId,
            sealedUtc = DateTime.UtcNow.ToString("O"),
            manifestAssetPath = ManifestPath,
            manifestSha256 = manifestSha,
            hashAlgorithm = "SHA-256",
            visualFidelityStatus = "UNSCORED_REVIEW_REQUIRED",
            note = "Receipt seals the prepared-scene temporal manifest. Frame/crop hashes are independently revalidated by QualityBlockTemporalStabilityCapture."
        };
        WriteJson(ReceiptPath, receipt);
        AssetDatabase.Refresh();

        // Reuse the established pixel-exact provenance validator before writing the prepared binding.
        QualityBlockTemporalStabilityCapture.ValidateLatestTemporalEvidence();

        var binding = new PreparedBinding
        {
            schemaVersion = "1.0",
            generatedUtc = DateTime.UtcNow.ToString("O"),
            captureSessionId = sessionId,
            scenePath = ScenePath,
            sceneAssetSha256 = sceneSha,
            temporalManifestAssetPath = ManifestPath,
            temporalManifestSha256 = manifestSha,
            temporalReceiptAssetPath = ReceiptPath,
            temporalReceiptSha256 = Sha256File(AbsolutePath(ReceiptPath)),
            reflectionCompletionReceiptAssetPath = ReflectionReceiptPath,
            reflectionCompletionReceiptSha256 = reflectionReceiptSha,
            reflectionAsyncWaitProofAssetPath = ReflectionWaitProofPath,
            reflectionAsyncWaitProofSha256 = reflectionWaitSha,
            sceneRebuiltAfterReflectionSynchronization = false,
            sceneReopenedAfterReflectionSynchronization = false,
            manualReviewRequired = true,
            automaticVisualPoints = 0,
            visualFidelityStatus = "UNSCORED_REVIEW_REQUIRED",
            note = "Cryptographic binding for temporal provenance only. It does not prove absence of shimmer or LOD pop."
        };
        WriteJson(BindingPath, binding);
        AssetDatabase.Refresh();
        ValidateLatestPreparedBinding();

        Debug.Log(
            $"Prepared temporal evidence captured/sealed: session={sessionId}, probes={probeRecords.Count}, " +
            $"scene/reflection proof hashes bound. Visual Fidelity remains UNSCORED_REVIEW_REQUIRED.");
    }

    [MenuItem("NewTown/QA/Validate Latest Prepared Temporal Binding")]
    public static void ValidateLatestPreparedBinding()
    {
        PreparedBinding binding = LoadJson<PreparedBinding>(BindingPath);
        if (binding == null || binding.schemaVersion != "1.0")
            throw new InvalidOperationException("Prepared temporal binding is missing or unsupported.");
        if (binding.scenePath != ScenePath || binding.sceneRebuiltAfterReflectionSynchronization || binding.sceneReopenedAfterReflectionSynchronization)
            throw new InvalidOperationException("Prepared temporal binding does not prove a no-rebuild/no-reopen capture path.");
        if (!binding.manualReviewRequired || binding.automaticVisualPoints != 0)
            throw new InvalidOperationException("Prepared temporal binding must require manual review and award zero automatic points.");

        string[] paths =
        {
            binding.scenePath,
            binding.temporalManifestAssetPath,
            binding.temporalReceiptAssetPath,
            binding.reflectionCompletionReceiptAssetPath,
            binding.reflectionAsyncWaitProofAssetPath
        };
        if (paths.Any(string.IsNullOrWhiteSpace) || paths.Any(x => !File.Exists(AbsolutePath(x))))
            throw new InvalidOperationException("Prepared temporal binding references a missing source/proof file.");

        if (!EqualsSha(binding.sceneAssetSha256, Sha256File(AbsolutePath(binding.scenePath))))
            throw new InvalidOperationException("Prepared temporal binding scene SHA-256 no longer matches.");
        if (!EqualsSha(binding.temporalManifestSha256, Sha256File(AbsolutePath(binding.temporalManifestAssetPath))))
            throw new InvalidOperationException("Prepared temporal binding manifest SHA-256 no longer matches.");
        if (!EqualsSha(binding.temporalReceiptSha256, Sha256File(AbsolutePath(binding.temporalReceiptAssetPath))))
            throw new InvalidOperationException("Prepared temporal binding receipt SHA-256 no longer matches.");
        if (!EqualsSha(binding.reflectionCompletionReceiptSha256, Sha256File(AbsolutePath(binding.reflectionCompletionReceiptAssetPath))))
            throw new InvalidOperationException("Prepared temporal binding reflection completion receipt SHA-256 no longer matches.");
        if (!EqualsSha(binding.reflectionAsyncWaitProofSha256, Sha256File(AbsolutePath(binding.reflectionAsyncWaitProofAssetPath))))
            throw new InvalidOperationException("Prepared temporal binding async wait proof SHA-256 no longer matches.");

        TemporalManifest manifest = LoadJson<TemporalManifest>(ManifestPath);
        if (manifest.captureSessionId != binding.captureSessionId ||
            manifest.captureMode != "PREPARED_SCENE_ASYNC_REFLECTION_BOUND_NO_REBUILD" ||
            !EqualsSha(manifest.sceneAssetSha256, binding.sceneAssetSha256) ||
            !EqualsSha(manifest.reflectionCompletionReceiptSha256, binding.reflectionCompletionReceiptSha256) ||
            !EqualsSha(manifest.reflectionAsyncWaitProofSha256, binding.reflectionAsyncWaitProofSha256))
            throw new InvalidOperationException("Prepared temporal manifest does not match its no-rebuild reflection binding.");

        QualityBlockTemporalStabilityCapture.ValidateLatestTemporalEvidence();
        Debug.Log("Prepared temporal binding valid. Provenance is intact; visual shimmer/LOD findings still require manual 100%-pixel review.");
    }

    private static void ValidatePreparedSceneWithoutMutation()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            throw new InvalidOperationException($"Prepared temporal capture requires the already-open persisted benchmark scene: {ScenePath}");
        if (EditorSceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Prepared temporal capture refused: benchmark scene has unsaved changes after reflection synchronization.");

        QualityBlockEnvironmentLightingUpgrade.ValidateOpenScene();
        QualityBlockDanchiDetailUpgrade.ValidateOpenScene();
        QualityBlockDetailBevelUpgrade.ValidateOpenScene();
        QualityBlockDanchiLodUpgrade.ValidateOpenScene();
        QualityBlockTreeDetailUpgrade.ValidateOpenScene();
        QualityBlockFoliageOpticsUpgrade.ValidateOpenScene();
        QualityBlockGroundDetailUpgrade.ValidateOpenScene();
        QualityBlockGroundMicrodetailUpgrade.Validate();
        QualityBlockGroundBaseSurfaceUpgrade.Validate();
        QualityBlockGroundContactInterfaceUpgrade.ValidateOpenScene();
        QualityBlockFacadeOpticsUpgrade.ValidateOpenScene();
        QualityBlockMaterialConstructionQA.ValidateRegistry();
        QualityBlockStructuralSurfaceRefinement.ValidateOpenScene();
        QualityBlockSceneMaterialPhysicalityUpgrade.ValidateOpenScene();
        QualityBlockSceneMetadataCoverageQA.ValidateOpenScene();
        QualityBlockTextureSamplingUpgrade.ValidateOpenScene();
        QualityBlockFoliagePhysicalityQA.ValidateOpenScene();
        QualityBlockSceneRepetitionQA.ValidateOpenScene();
        QualityBlockVisualFidelityGate.ValidateGateConfig();
    }

    private static ProbeRecord CaptureProbe(Camera cam, ProbeSpec spec, string sessionDir)
    {
        var frames = new List<FrameRecord>();
        for (int i = 0; i < spec.frameCount; i++)
        {
            float t = spec.frameCount <= 1 ? 0f : i / (float)(spec.frameCount - 1);
            ResolvePose(spec, t, out Vector3 position, out Vector3 target);
            frames.Add(CaptureFrame(cam, spec, i, t, position, target, sessionDir));
        }

        return new ProbeRecord
        {
            id = spec.id,
            frameCount = spec.frameCount,
            fieldOfView = spec.fieldOfView,
            motionMode = spec.motionMode,
            motionStartMeters = spec.motionStartMeters,
            motionEndMeters = spec.motionEndMeters,
            frames = frames.ToArray()
        };
    }

    private static FrameRecord CaptureFrame(Camera cam, ProbeSpec spec, int index, float t,
        Vector3 position, Vector3 target, string sessionDir)
    {
        Vector3 forward = target - position;
        if (forward.sqrMagnitude < 0.001f)
            throw new InvalidOperationException($"Prepared temporal probe {spec.id} frame {index} has an invalid camera target.");

        cam.transform.position = position;
        cam.transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        cam.fieldOfView = spec.fieldOfView;
        cam.aspect = Width / (float)Height;

        int msaaSamples = ResolveSupportedMsaaSamples();
        var msaaDescriptor = new RenderTextureDescriptor(Width, Height, RenderTextureFormat.ARGB32, 24)
        {
            msaaSamples = msaaSamples,
            useMipMap = false,
            autoGenerateMips = false,
            sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear,
        };
        var resolveDescriptor = msaaDescriptor;
        resolveDescriptor.msaaSamples = 1;

        var msaaTarget = new RenderTexture(msaaDescriptor) { name = $"QAPreparedTemporal_{spec.id}_{index:00}_MSAA" };
        var resolvedTarget = new RenderTexture(resolveDescriptor) { name = $"QAPreparedTemporal_{spec.id}_{index:00}_Resolved" };
        var fullFrame = new Texture2D(Width, Height, TextureFormat.RGB24, false, false);
        RenderTexture previousActive = RenderTexture.active;

        string fullAssetPath = $"{sessionDir}/{spec.id}_frame_{index:00}_3840x2160.png";
        string cropAssetPath = $"{sessionDir}/{spec.id}_frame_{index:00}_crop_{spec.cropId}_{spec.cropWidth}x{spec.cropHeight}_100pct.png";

        try
        {
            msaaTarget.Create();
            resolvedTarget.Create();
            cam.targetTexture = msaaTarget;
            cam.Render();
            Graphics.Blit(msaaTarget, resolvedTarget);
            RenderTexture.active = resolvedTarget;
            fullFrame.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
            fullFrame.Apply(false, false);
            WritePng(fullAssetPath, fullFrame);
            WritePixelExactCrop(fullFrame, cropAssetPath, spec);
        }
        finally
        {
            cam.targetTexture = null;
            RenderTexture.active = previousActive;
            UnityEngine.Object.DestroyImmediate(fullFrame);
            msaaTarget.Release();
            resolvedTarget.Release();
            UnityEngine.Object.DestroyImmediate(msaaTarget);
            UnityEngine.Object.DestroyImmediate(resolvedTarget);
        }

        return new FrameRecord
        {
            index = index,
            normalizedMotion = t,
            cameraPosition = ToArray(position),
            cameraTarget = ToArray(target),
            msaaSamples = msaaSamples,
            assetPath = fullAssetPath,
            width = Width,
            height = Height,
            sha256 = Sha256File(AbsolutePath(fullAssetPath)),
            crop = new CropRecord
            {
                id = spec.cropId,
                assetPath = cropAssetPath,
                x = spec.cropX,
                y = spec.cropY,
                width = spec.cropWidth,
                height = spec.cropHeight,
                resampled = false,
                sha256 = Sha256File(AbsolutePath(cropAssetPath))
            }
        };
    }

    private static void ResolvePose(ProbeSpec spec, float t, out Vector3 position, out Vector3 target)
    {
        float motion = Mathf.Lerp(spec.motionStartMeters, spec.motionEndMeters, t);
        if (spec.id == "subpixel_grazing")
        {
            Vector3 forward = (GrazingTarget - GrazingPosition).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 offset = right * motion;
            position = GrazingPosition + offset;
            target = GrazingTarget + offset;
            return;
        }

        if (spec.id == "lod_walk_oblique")
        {
            Vector3 radial = (ObliquePosition - ObliqueTarget).normalized;
            float distance = Vector3.Distance(ObliquePosition, ObliqueTarget) + motion;
            if (distance < 5f)
                throw new InvalidOperationException($"Prepared LOD walk requested unsafe camera distance {distance:F2} m.");
            position = ObliqueTarget + radial * distance;
            target = ObliqueTarget;
            return;
        }

        throw new InvalidOperationException($"Unsupported temporal probe id: {spec.id}");
    }

    private static LodGroupSummary[] BuildLodSummaries(LODGroup[] groups)
    {
        return groups
            .Where(x => x != null && x.GetLODs().Length >= 4)
            .OrderBy(x => GetObjectPath(x.transform), StringComparer.Ordinal)
            .Select(x => new LodGroupSummary
            {
                objectPath = GetObjectPath(x.transform),
                lodCount = x.GetLODs().Length,
                animateCrossFading = x.animateCrossFading,
                fadeMode = x.fadeMode.ToString(),
                transitionHeights = x.GetLODs().Select(l => l.screenRelativeTransitionHeight).ToArray()
            })
            .ToArray();
    }

    private static string GetObjectPath(Transform transform)
    {
        var names = new List<string>();
        for (Transform current = transform; current != null; current = current.parent)
            names.Add(current.name);
        names.Reverse();
        return string.Join("/", names);
    }

    private static int ResolveSupportedMsaaSamples()
    {
        var descriptor = new RenderTextureDescriptor(Width, Height, RenderTextureFormat.ARGB32, 24)
        {
            msaaSamples = 8,
            useMipMap = false,
            autoGenerateMips = false,
        };
        int supported = SystemInfo.GetRenderTextureSupportedMSAASampleCount(descriptor);
        if (supported >= 8) return 8;
        if (supported >= 4) return 4;
        if (supported >= 2) return 2;
        return 1;
    }

    private static void WritePixelExactCrop(Texture2D source, string assetPath, ProbeSpec spec)
    {
        var crop = new Texture2D(spec.cropWidth, spec.cropHeight, TextureFormat.RGB24, false, false);
        try
        {
            crop.SetPixels(source.GetPixels(spec.cropX, spec.cropY, spec.cropWidth, spec.cropHeight));
            crop.Apply(false, false);
            WritePng(assetPath, crop);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(crop);
        }
    }

    private static void WritePng(string assetPath, Texture2D texture)
    {
        string absolute = AbsolutePath(assetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute));
        File.WriteAllBytes(absolute, texture.EncodeToPNG());
    }

    private static void ValidatePreparedContract(PreparedContract contract)
    {
        if (contract == null || contract.schemaVersion != "1.0")
            throw new InvalidOperationException("Prepared temporal capture contract is null or unsupported.");
        if (contract.authoritativeEntryPoint != "QualityBlockPreparedTemporalCapture.CaptureAndSealPreparedScene" ||
            contract.scenePath != ScenePath || contract.width != Width || contract.height != Height)
            throw new InvalidOperationException("Prepared temporal capture contract identity/dimensions drifted.");
        if (contract.requirements == null || contract.requirements.sceneRebuildAfterReflectionSynchronization ||
            contract.requirements.sceneReopenAfterReflectionSynchronization || !contract.requirements.samePreparedSceneAsStillCapture ||
            !contract.requirements.requireFreshAsyncReflectionWaitProofBeforeFirstTemporalFrame ||
            !contract.requirements.hashReflectionCompletionReceipt || !contract.requirements.hashAsyncWaitProof ||
            !contract.requirements.hashTemporalManifest || !contract.requirements.manualPixelReviewRequired ||
            contract.requirements.automaticVisualPoints != 0)
            throw new InvalidOperationException("Prepared temporal capture contract must remain no-rebuild, async-reflection-bound and non-scoring.");
    }

    private static void ValidateTemporalContractShape(TemporalContract temporal)
    {
        if (temporal == null || temporal.width != Width || temporal.height != Height || !temporal.requirePixelExactCrops || temporal.probes == null)
            throw new InvalidOperationException("Temporal stability contract is incompatible with prepared native-4K capture.");
        string[] ids = temporal.probes.Select(x => x.id).ToArray();
        if (!new HashSet<string>(ids, StringComparer.Ordinal).SetEquals(new[] { "subpixel_grazing", "lod_walk_oblique" }))
            throw new InvalidOperationException("Prepared temporal capture requires exactly the subpixel_grazing and lod_walk_oblique probes.");
        foreach (ProbeSpec p in temporal.probes)
        {
            if (p.frameCount < 9 || p.cropX < 0 || p.cropY < 0 || p.cropWidth <= 0 || p.cropHeight <= 0 ||
                p.cropX + p.cropWidth > Width || p.cropY + p.cropHeight > Height)
                throw new InvalidOperationException($"Prepared temporal probe contract invalid: {p.id}.");
        }
    }

    private static T LoadJson<T>(string assetPath)
    {
        string absolute = AbsolutePath(assetPath);
        if (!File.Exists(absolute))
            throw new FileNotFoundException($"Required QA file not found: {assetPath}");
        T value = JsonUtility.FromJson<T>(File.ReadAllText(absolute));
        if (value == null)
            throw new InvalidOperationException($"Could not parse QA JSON: {assetPath}");
        return value;
    }

    private static void WriteJson<T>(string assetPath, T value)
    {
        string absolute = AbsolutePath(assetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute));
        File.WriteAllText(absolute, JsonUtility.ToJson(value, true));
    }

    private static string Sha256File(string absolutePath)
    {
        using (SHA256 sha = SHA256.Create())
        using (FileStream stream = File.OpenRead(absolutePath))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static bool EqualsSha(string a, string b) =>
        !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b) &&
        string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string AbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private static float[] ToArray(Vector3 value) => new[] { value.x, value.y, value.z };

    [Serializable]
    private sealed class PreparedContract
    {
        public string schemaVersion;
        public string authoritativeEntryPoint;
        public string scenePath;
        public int width;
        public int height;
        public PreparedRequirements requirements;
    }

    [Serializable]
    private sealed class PreparedRequirements
    {
        public bool sceneRebuildAfterReflectionSynchronization;
        public bool sceneReopenAfterReflectionSynchronization;
        public bool samePreparedSceneAsStillCapture;
        public bool requireFreshAsyncReflectionWaitProofBeforeFirstTemporalFrame;
        public bool hashReflectionCompletionReceipt;
        public bool hashAsyncWaitProof;
        public bool hashTemporalManifest;
        public bool manualPixelReviewRequired;
        public int automaticVisualPoints;
    }

    [Serializable]
    private sealed class TemporalContract
    {
        public int width;
        public int height;
        public bool requirePixelExactCrops;
        public ProbeSpec[] probes;
    }

    [Serializable]
    private sealed class ProbeSpec
    {
        public string id;
        public int frameCount;
        public float fieldOfView;
        public string motionMode;
        public float motionStartMeters;
        public float motionEndMeters;
        public string cropId;
        public int cropX;
        public int cropY;
        public int cropWidth;
        public int cropHeight;
    }

    [Serializable]
    private sealed class TemporalManifest
    {
        public string schemaVersion;
        public string captureSessionId;
        public string generatedUtc;
        public string unityVersion;
        public string graphicsDevice;
        public string graphicsApi;
        public string projectColorSpace;
        public int width;
        public int height;
        public string source;
        public bool renderProducedByUnity;
        public string visualFidelityStatus;
        public float lodBias;
        public int lodGroupCount;
        public int fourLevelLodGroupCount;
        public int animatedCrossFadeFourLevelGroupCount;
        public LodGroupSummary[] lodGroups;
        public ProbeRecord[] probes;
        public string captureMode;
        public string scenePath;
        public string sceneAssetSha256;
        public string reflectionCompletionReceiptSha256;
        public string reflectionAsyncWaitProofSha256;
        public string note;
    }

    [Serializable]
    private sealed class ProbeRecord
    {
        public string id;
        public int frameCount;
        public float fieldOfView;
        public string motionMode;
        public float motionStartMeters;
        public float motionEndMeters;
        public FrameRecord[] frames;
    }

    [Serializable]
    private sealed class FrameRecord
    {
        public int index;
        public float normalizedMotion;
        public float[] cameraPosition;
        public float[] cameraTarget;
        public int msaaSamples;
        public string assetPath;
        public int width;
        public int height;
        public string sha256;
        public CropRecord crop;
    }

    [Serializable]
    private sealed class CropRecord
    {
        public string id;
        public string assetPath;
        public int x;
        public int y;
        public int width;
        public int height;
        public bool resampled;
        public string sha256;
    }

    [Serializable]
    private sealed class LodGroupSummary
    {
        public string objectPath;
        public int lodCount;
        public bool animateCrossFading;
        public string fadeMode;
        public float[] transitionHeights;
    }

    [Serializable]
    private sealed class TemporalReceipt
    {
        public string schemaVersion;
        public string captureSessionId;
        public string sealedUtc;
        public string manifestAssetPath;
        public string manifestSha256;
        public string hashAlgorithm;
        public string visualFidelityStatus;
        public string note;
    }

    [Serializable]
    private sealed class PreparedBinding
    {
        public string schemaVersion;
        public string generatedUtc;
        public string captureSessionId;
        public string scenePath;
        public string sceneAssetSha256;
        public string temporalManifestAssetPath;
        public string temporalManifestSha256;
        public string temporalReceiptAssetPath;
        public string temporalReceiptSha256;
        public string reflectionCompletionReceiptAssetPath;
        public string reflectionCompletionReceiptSha256;
        public string reflectionAsyncWaitProofAssetPath;
        public string reflectionAsyncWaitProofSha256;
        public bool sceneRebuiltAfterReflectionSynchronization;
        public bool sceneReopenedAfterReflectionSynchronization;
        public bool manualReviewRequired;
        public int automaticVisualPoints;
        public string visualFidelityStatus;
        public string note;
    }
}
