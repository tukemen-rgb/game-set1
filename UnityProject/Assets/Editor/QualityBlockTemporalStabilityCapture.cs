using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Native-4K motion evidence for temporal shimmer and LOD transition review.
/// This tool deliberately awards zero visual points. It only produces and cryptographically seals
/// deterministic frame sequences that a reviewer can inspect at 100% pixel scale.
/// </summary>
public static class QualityBlockTemporalStabilityCapture
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/temporal_stability_contract.json";
    private const string OutputRoot = "Assets/QA/Temporal4K";
    private const string ManifestPath = "Assets/QA/temporal_stability_manifest.json";
    private const string ReceiptPath = "Assets/QA/temporal_stability_receipt.json";
    private const int Width = 3840;
    private const int Height = 2160;

    private static readonly Vector3 GrazingPosition = new Vector3(-22.0f, 3.0f, 7.0f);
    private static readonly Vector3 GrazingTarget = new Vector3(-8.2f, 3.8f, -7.8f);
    private static readonly Vector3 ObliquePosition = new Vector3(18.0f, 3.4f, 12.5f);
    private static readonly Vector3 ObliqueTarget = new Vector3(-6.0f, 4.0f, -8.3f);

    [MenuItem("NewTown/QA/Validate Temporal Stability Capture Contract")]
    public static void ValidateContractConfigOnly()
    {
        TemporalContract contract = LoadJson<TemporalContract>(ContractPath);
        ValidateContract(contract);
        Debug.Log("Temporal stability contract valid: native 3840x2160, sealed subpixel-grazing and LOD-walk sequences, zero automatic visual points.");
    }

    [MenuItem("NewTown/QA/Capture + Seal Temporal Stability Evidence")]
    public static void CaptureAndSeal()
    {
        TemporalContract contract = LoadJson<TemporalContract>(ContractPath);
        ValidateContract(contract);
        PrepareAndValidateScene();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        QualityBlockEnvironmentLightingUpgrade.ValidateOpenScene();
        QualityBlockEnvironmentLightingUpgrade.RefreshRealtimeProbesImmediately();

        Camera cam = Camera.main;
        if (cam == null)
            throw new InvalidOperationException("Temporal capture failed: MainCamera not found.");

        LODGroup[] lodGroups = UnityEngine.Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None);
        int fourLevelGroups = lodGroups.Count(x => x != null && x.GetLODs().Length >= 4);
        int animatedCrossFadeGroups = lodGroups.Count(x => x != null && x.animateCrossFading && x.GetLODs().Length >= 4);
        if (fourLevelGroups == 0)
            throw new InvalidOperationException("Temporal capture refused: no 4-level LODGroup is present in the benchmark scene.");
        if (animatedCrossFadeGroups == 0)
            throw new InvalidOperationException("Temporal capture refused: no 4-level LODGroup uses animated cross-fading.");

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
            foreach (ProbeSpec spec in contract.probes)
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

        var manifest = new TemporalManifest
        {
            schemaVersion = "1.0",
            captureSessionId = sessionId,
            generatedUtc = DateTime.UtcNow.ToString("O"),
            unityVersion = Application.unityVersion,
            graphicsDevice = SystemInfo.graphicsDeviceName,
            graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
            projectColorSpace = QualitySettings.activeColorSpace.ToString(),
            width = Width,
            height = Height,
            source = "Unity Camera.Render -> MSAA RenderTexture -> single-sample resolve -> Texture2D.ReadPixels",
            renderProducedByUnity = true,
            visualFidelityStatus = "UNSCORED_REVIEW_REQUIRED",
            lodBias = QualitySettings.lodBias,
            lodGroupCount = lodGroups.Length,
            fourLevelLodGroupCount = fourLevelGroups,
            animatedCrossFadeFourLevelGroupCount = animatedCrossFadeGroups,
            lodGroups = BuildLodSummaries(lodGroups),
            probes = probeRecords.ToArray(),
            note = "Temporal evidence production is not a Visual Fidelity score. Review all sealed frames/crops at 100% and explicitly record observedTemporalRefs before scoring."
        };

        WriteJson(ManifestPath, manifest);
        string manifestSha = Sha256File(AbsolutePath(ManifestPath));
        var receipt = new TemporalReceipt
        {
            schemaVersion = "1.0",
            captureSessionId = sessionId,
            sealedUtc = DateTime.UtcNow.ToString("O"),
            manifestAssetPath = ManifestPath,
            manifestSha256 = manifestSha,
            hashAlgorithm = "SHA-256",
            visualFidelityStatus = "UNSCORED_REVIEW_REQUIRED",
            note = "Receipt seals the manifest only; validation also recomputes every full-frame and 100% crop hash from the manifest."
        };
        WriteJson(ReceiptPath, receipt);
        AssetDatabase.Refresh();

        ValidateTemporalEvidenceForScoring(
            sessionId,
            ManifestPath,
            manifestSha,
            ReceiptPath,
            Sha256File(AbsolutePath(ReceiptPath)));

        Debug.Log(
            $"Temporal stability evidence captured and sealed: session={sessionId}, probes={probeRecords.Count}, " +
            $"fourLevelLODGroups={fourLevelGroups}. Visual Fidelity remains UNSCORED_REVIEW_REQUIRED.");
    }

    [MenuItem("NewTown/QA/Validate Latest Temporal Stability Evidence")]
    public static void ValidateLatestTemporalEvidence()
    {
        TemporalReceipt receipt = LoadJson<TemporalReceipt>(ReceiptPath);
        ValidateTemporalEvidenceForScoring(
            receipt.captureSessionId,
            receipt.manifestAssetPath,
            receipt.manifestSha256,
            ReceiptPath,
            Sha256File(AbsolutePath(ReceiptPath)));
        Debug.Log("Latest temporal stability evidence is sealed and pixel-exact. This validation awards zero Visual Fidelity points.");
    }

    public static void ValidateTemporalEvidenceForScoring(
        string captureSessionId,
        string manifestAssetPath,
        string manifestSha256,
        string receiptAssetPath,
        string receiptSha256)
    {
        TemporalContract contract = LoadJson<TemporalContract>(ContractPath);
        ValidateContract(contract);

        if (string.IsNullOrWhiteSpace(captureSessionId))
            throw new InvalidOperationException("Temporal evidence has no captureSessionId.");
        if (manifestAssetPath != ManifestPath)
            throw new InvalidOperationException($"Temporal manifest must be {ManifestPath}, got {manifestAssetPath}.");
        if (receiptAssetPath != ReceiptPath)
            throw new InvalidOperationException($"Temporal receipt must be {ReceiptPath}, got {receiptAssetPath}.");

        string manifestAbsolute = AbsolutePath(manifestAssetPath);
        string receiptAbsolute = AbsolutePath(receiptAssetPath);
        if (!File.Exists(manifestAbsolute) || !File.Exists(receiptAbsolute))
            throw new InvalidOperationException("Temporal evidence manifest/receipt is missing. Real Unity temporal capture is required.");

        string actualManifestSha = Sha256File(manifestAbsolute);
        string actualReceiptSha = Sha256File(receiptAbsolute);
        if (!EqualsSha(actualManifestSha, manifestSha256))
            throw new InvalidOperationException("Temporal evidence manifest SHA-256 does not match the review reference.");
        if (!EqualsSha(actualReceiptSha, receiptSha256))
            throw new InvalidOperationException("Temporal evidence receipt SHA-256 does not match the review reference.");

        TemporalReceipt receipt = LoadJson<TemporalReceipt>(receiptAssetPath);
        if (receipt.captureSessionId != captureSessionId)
            throw new InvalidOperationException("Temporal receipt captureSessionId does not match the review reference.");
        if (receipt.manifestAssetPath != manifestAssetPath || !EqualsSha(receipt.manifestSha256, actualManifestSha))
            throw new InvalidOperationException("Temporal receipt does not seal the current manifest.");

        TemporalManifest manifest = LoadJson<TemporalManifest>(manifestAssetPath);
        if (manifest.captureSessionId != captureSessionId)
            throw new InvalidOperationException("Temporal manifest captureSessionId does not match the review reference.");
        if (!manifest.renderProducedByUnity || manifest.width != Width || manifest.height != Height)
            throw new InvalidOperationException("Temporal manifest is not verified native 3840x2160 Unity render evidence.");
        if (manifest.probes == null || manifest.probes.Length != contract.probes.Length)
            throw new InvalidOperationException("Temporal manifest does not contain every required probe sequence.");
        if (manifest.fourLevelLodGroupCount < 1 || manifest.animatedCrossFadeFourLevelGroupCount < 1)
            throw new InvalidOperationException("Temporal manifest did not record any valid 4-level animated-crossfade LOD system.");

        foreach (ProbeSpec expected in contract.probes)
        {
            ProbeRecord probe = manifest.probes.SingleOrDefault(x => x != null && x.id == expected.id);
            if (probe == null)
                throw new InvalidOperationException($"Temporal manifest missing required probe {expected.id}.");
            if (probe.frames == null || probe.frames.Length != expected.frameCount)
                throw new InvalidOperationException($"Temporal probe {expected.id} has {probe.frames?.Length ?? 0} frames; expected {expected.frameCount}.");

            var indices = new HashSet<int>();
            foreach (FrameRecord frame in probe.frames)
            {
                if (!indices.Add(frame.index))
                    throw new InvalidOperationException($"Temporal probe {expected.id} contains duplicate frame index {frame.index}.");
                ValidateFrameAndCrop(expected, frame);
            }
            for (int i = 0; i < expected.frameCount; i++)
                if (!indices.Contains(i))
                    throw new InvalidOperationException($"Temporal probe {expected.id} is missing frame {i}.");
        }
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
            throw new InvalidOperationException($"Temporal probe {spec.id} frame {index} has an invalid camera target.");

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

        var msaaTarget = new RenderTexture(msaaDescriptor) { name = $"QATemporal_{spec.id}_{index:00}_MSAA" };
        var resolvedTarget = new RenderTexture(resolveDescriptor) { name = $"QATemporal_{spec.id}_{index:00}_Resolved" };
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
            float baseDistance = Vector3.Distance(ObliquePosition, ObliqueTarget);
            float distance = baseDistance + motion;
            if (distance < 5f)
                throw new InvalidOperationException($"LOD walk requested unsafe camera distance {distance:F2} m.");
            position = ObliqueTarget + radial * distance;
            target = ObliqueTarget;
            return;
        }

        throw new InvalidOperationException($"Unsupported temporal probe id: {spec.id}");
    }

    private static void ValidateFrameAndCrop(ProbeSpec expected, FrameRecord frame)
    {
        if (frame.width != Width || frame.height != Height)
            throw new InvalidOperationException($"Temporal frame {expected.id}/{frame.index} is not 3840x2160.");
        if (string.IsNullOrWhiteSpace(frame.assetPath) || !File.Exists(AbsolutePath(frame.assetPath)))
            throw new InvalidOperationException($"Temporal full frame missing: {frame.assetPath}");
        if (!EqualsSha(Sha256File(AbsolutePath(frame.assetPath)), frame.sha256))
            throw new InvalidOperationException($"Temporal full-frame hash mismatch: {frame.assetPath}");
        ValidatePngDimensions(frame.assetPath, Width, Height);

        if (frame.crop == null)
            throw new InvalidOperationException($"Temporal frame {expected.id}/{frame.index} has no 100% crop.");
        CropRecord crop = frame.crop;
        if (crop.id != expected.cropId || crop.x != expected.cropX || crop.y != expected.cropY ||
            crop.width != expected.cropWidth || crop.height != expected.cropHeight || crop.resampled)
            throw new InvalidOperationException($"Temporal crop metadata mismatch: {expected.id}/{frame.index}.");
        if (string.IsNullOrWhiteSpace(crop.assetPath) || !File.Exists(AbsolutePath(crop.assetPath)))
            throw new InvalidOperationException($"Temporal 100% crop missing: {crop.assetPath}");
        if (!EqualsSha(Sha256File(AbsolutePath(crop.assetPath)), crop.sha256))
            throw new InvalidOperationException($"Temporal crop hash mismatch: {crop.assetPath}");
        ValidatePngDimensions(crop.assetPath, expected.cropWidth, expected.cropHeight);
        ValidateCropPixels(frame.assetPath, crop);
    }

    private static void ValidateCropPixels(string fullFrameAssetPath, CropRecord crop)
    {
        byte[] fullBytes = File.ReadAllBytes(AbsolutePath(fullFrameAssetPath));
        byte[] cropBytes = File.ReadAllBytes(AbsolutePath(crop.assetPath));
        var full = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
        var cropped = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
        try
        {
            if (!ImageConversion.LoadImage(full, fullBytes, false) || !ImageConversion.LoadImage(cropped, cropBytes, false))
                throw new InvalidOperationException("Could not decode temporal PNG evidence for pixel-exact validation.");
            Color32[] sourcePixels = full.GetPixels32();
            Color32[] cropPixels = cropped.GetPixels32();
            int p = 0;
            for (int y = 0; y < crop.height; y++)
            {
                int sourceRow = (crop.y + y) * Width + crop.x;
                for (int x = 0; x < crop.width; x++, p++)
                    if (!ColorEquals(sourcePixels[sourceRow + x], cropPixels[p]))
                        throw new InvalidOperationException(
                            $"Temporal crop is not pixel-exact at local ({x},{y}) for {crop.assetPath}.");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(full);
            UnityEngine.Object.DestroyImmediate(cropped);
        }
    }

    private static bool ColorEquals(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

    private static void ValidatePngDimensions(string assetPath, int expectedWidth, int expectedHeight)
    {
        byte[] bytes = File.ReadAllBytes(AbsolutePath(assetPath));
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
        try
        {
            if (!ImageConversion.LoadImage(texture, bytes, false))
                throw new InvalidOperationException($"Could not decode PNG evidence: {assetPath}");
            if (texture.width != expectedWidth || texture.height != expectedHeight)
                throw new InvalidOperationException(
                    $"PNG dimensions mismatch for {assetPath}: {texture.width}x{texture.height}, expected {expectedWidth}x{expectedHeight}.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    private static void ValidateContract(TemporalContract contract)
    {
        if (contract == null)
            throw new InvalidOperationException("Temporal stability contract is null.");
        if (contract.width != Width || contract.height != Height || !contract.requirePixelExactCrops)
            throw new InvalidOperationException("Temporal contract must require native 3840x2160 and pixel-exact 100% crops.");
        if (contract.probes == null || contract.probes.Length != 2)
            throw new InvalidOperationException("Temporal contract must define exactly subpixel_grazing and lod_walk_oblique probes.");

        string[] expectedIds = { "subpixel_grazing", "lod_walk_oblique" };
        foreach (string id in expectedIds)
        {
            ProbeSpec probe = contract.probes.SingleOrDefault(x => x != null && x.id == id);
            if (probe == null)
                throw new InvalidOperationException($"Temporal contract missing probe {id}.");
            if (probe.frameCount < 9)
                throw new InvalidOperationException($"Temporal probe {id} needs at least 9 frames, got {probe.frameCount}.");
            if (probe.cropX < 0 || probe.cropY < 0 || probe.cropWidth <= 0 || probe.cropHeight <= 0 ||
                probe.cropX + probe.cropWidth > Width || probe.cropY + probe.cropHeight > Height)
                throw new InvalidOperationException($"Temporal probe {id} crop lies outside native 4K bounds.");
        }

        if (contract.requiredProbeRefsForTemporalCategory == null ||
            !new HashSet<string>(contract.requiredProbeRefsForTemporalCategory, StringComparer.Ordinal).SetEquals(expectedIds))
            throw new InvalidOperationException("Temporal category must require both temporal probe references.");
    }

    private static void PrepareAndValidateScene()
    {
        QualityBlockGroundDetailUpgrade.BuildDetailedGround();
        QualityBlockGroundMicrodetailUpgrade.BuildAndApply();
        QualityBlockGroundBaseSurfaceUpgrade.BuildAndApply();
        QualityBlockGroundContactInterfaceUpgrade.ApplyToOpenScene();
        QualityBlockFacadeOpticsUpgrade.BuildAndApply();
        QualityBlockEnvironmentLightingUpgrade.BuildAndApply();
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
        QualityBlockEnvironmentLightingUpgrade.ValidateOpenScene();
        QualityBlockMaterialConstructionQA.ValidateRegistry();
        QualityBlockVisualFidelityGate.ValidateGateConfig();
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
        if (transform == null) return string.Empty;
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
    private sealed class TemporalContract
    {
        public string schemaVersion;
        public string purpose;
        public int width;
        public int height;
        public bool requirePixelExactCrops;
        public ProbeSpec[] probes;
        public string[] requiredProbeRefsForTemporalCategory;
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
        public string reason;
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
}
