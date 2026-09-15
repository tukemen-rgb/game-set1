using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Binds Visual Fidelity scoring to the exact native Unity render set that was captured and reviewed.
/// This QA intentionally does not award visual points. It closes the provenance gap where a manually
/// authored visual_fidelity_evidence.json could otherwise point at arbitrary existing PNGs.
///
/// Supported workflow:
///   1) CaptureAndSealNative4KEvidence() runs the real Unity capture and writes a SHA-256 receipt.
///   2) A reviewer scores the exact rendered files and records the receipt/manifest hashes in
///      visual_fidelity_evidence.json.
///   3) EvaluateEvidenceBoundVisualGate() proves path/hash/dimension/crop identity before invoking the
///      numeric 100-point gate.
/// </summary>
public static class QualityBlockRenderEvidenceProvenanceQA
{
    private const string ManifestPath = "Assets/QA/4k_capture_manifest.json";
    private const string EvidencePath = "Assets/QA/visual_fidelity_evidence.json";
    private const string ReceiptPath = "Assets/QA/render_capture_receipt.json";
    private const string BoundTemplatePath = "Assets/QA/visual_fidelity_evidence.bound_template.json";
    private const string ProvenancePath = "Assets/QA/render_evidence_provenance.json";
    private const int Width = 3840;
    private const int Height = 2160;

    private static readonly string[] RequiredViews = { "hero", "oblique", "grazing" };

    [MenuItem("NewTown/QA/Capture + Seal Native 4K Evidence")]
    public static void CaptureAndSealNative4KEvidence()
    {
        QualityBlock4KCapture.CaptureAll();
        QualityBlockBenchmarkObservabilityQA.ExtractPeriodAuthenticityCropsFromExistingFrames();
        SealCurrentCapture();
        WriteBoundEvidenceTemplate();
        AssetDatabase.Refresh();

        Debug.Log(
            "Native 4K render set captured and SHA-256 sealed. Visual Fidelity remains UNSCORED until a reviewer " +
            "records observed category evidence/defects and uses the evidence-bound evaluator.");
    }

    [MenuItem("NewTown/QA/Seal Existing Native 4K Capture")]
    public static void SealCurrentCapture()
    {
        CaptureManifest manifest = LoadJson<CaptureManifest>(ManifestPath);
        ValidateRuntimeManifest(manifest);

        string manifestHash = Sha256File(ManifestPath);
        var proofs = ValidateManifestFilesAndPixelExactCrops(manifest, true);

        var receipt = new CaptureReceipt
        {
            schemaVersion = "1.0",
            captureSessionId = Guid.NewGuid().ToString("N"),
            sealedUtc = DateTime.UtcNow.ToString("O"),
            unityVersion = manifest.unityVersion,
            graphicsDevice = manifest.graphicsDevice,
            graphicsApi = manifest.graphicsApi,
            projectColorSpace = manifest.projectColorSpace,
            manifestPath = ManifestPath,
            manifestSha256 = manifestHash,
            renderProducedByUnity = true,
            requiredViews = RequiredViews,
            files = proofs.ToArray(),
            visualFidelityStatus = "UNSCORED_REVIEW_REQUIRED",
            note = "Receipt proves byte identity/dimensions and 1:1 crop provenance for the Unity capture set. It does not award visual points."
        };

        WriteJson(ReceiptPath, receipt);
        Debug.Log($"Render capture receipt sealed: session={receipt.captureSessionId}, manifestSHA256={manifestHash}.");
    }

    [MenuItem("NewTown/QA/Write Bound Visual Evidence Template")]
    public static void WriteBoundEvidenceTemplate()
    {
        CaptureManifest manifest = LoadJson<CaptureManifest>(ManifestPath);
        ValidateRuntimeManifest(manifest);
        CaptureReceipt receipt = LoadJson<CaptureReceipt>(ReceiptPath);
        ValidateReceiptAgainstCurrentCapture(manifest, receipt, out string manifestHash, out string receiptHash);

        var templateCaptures = new List<EvidenceCapture>();
        foreach (string viewId in RequiredViews)
        {
            ManifestCapture capture = SingleCapture(manifest.captures, viewId, "manifest");
            templateCaptures.Add(new EvidenceCapture
            {
                viewId = capture.viewId,
                assetPath = capture.assetPath,
                width = capture.width,
                height = capture.height,
                cropPaths = capture.cropRecords.Select(x => x.assetPath).ToArray()
            });
        }

        var template = new BoundEvidenceTemplate
        {
            schemaVersion = "1.0",
            captureSessionId = receipt.captureSessionId,
            captureManifestSha256 = manifestHash,
            captureReceiptSha256 = receiptHash,
            renderVerified = false,
            unityVersion = manifest.unityVersion,
            captures = templateCaptures.ToArray(),
            categories = new CategoryEvidence[0],
            criticalDefects = new CriticalDefectEvidence[0],
            instructions =
                "Template only. Set renderVerified=true only after reviewing these exact native 4K files and 100% crops. " +
                "Populate all nine category scores/evidence/deductions/correctiveAction fields and every critical-defect review. " +
                "Copy the completed reviewed document to Assets/QA/visual_fidelity_evidence.json without changing the capture/receipt hashes."
        };

        WriteJson(BoundTemplatePath, template);
        Debug.Log("Bound Visual Fidelity evidence template written. It is deliberately unreviewed and cannot pass the gate.");
    }

    [MenuItem("NewTown/QA/Validate Render Evidence Provenance")]
    public static void ValidateEvidenceProvenance()
    {
        ProvenanceReport report = ValidateEvidenceProvenanceInternal();
        WriteJson(ProvenancePath, report);
        Debug.Log(
            $"Render evidence provenance VALID: session={report.captureSessionId}, manifestSHA256={report.manifestSha256}. " +
            "This validation does not award Visual Fidelity points.");
    }

    [MenuItem("NewTown/QA/Evaluate Evidence-Bound 4K Visual Fidelity Gate")]
    public static void EvaluateEvidenceBoundVisualGate()
    {
        ProvenanceReport report = ValidateEvidenceProvenanceInternal();
        WriteJson(ProvenancePath, report);

        // Invoke the existing numeric gate only after render identity, receipt identity and true 100% crop
        // equality have all been proven for the exact evidence document being scored.
        QualityBlockVisualFidelityGate.EvaluateVisualGate();
    }

    // Disable the legacy menu entry so normal editor use cannot skip provenance validation. The method
    // remains callable for backwards compatibility, but the machine-readable contract explicitly forbids
    // direct runner/CLI use; automated scoring must call EvaluateEvidenceBoundVisualGate instead.
    [MenuItem("NewTown/QA/Evaluate 4K Visual Fidelity Gate", true)]
    private static bool DisableLegacyUnboundVisualGateMenu()
    {
        return false;
    }

    private static ProvenanceReport ValidateEvidenceProvenanceInternal()
    {
        CaptureManifest manifest = LoadJson<CaptureManifest>(ManifestPath);
        ValidateRuntimeManifest(manifest);

        CaptureReceipt receipt = LoadJson<CaptureReceipt>(ReceiptPath);
        ValidateReceiptAgainstCurrentCapture(manifest, receipt, out string manifestHash, out string receiptHash);

        BoundVisualEvidence evidence = LoadJson<BoundVisualEvidence>(EvidencePath);
        Require(evidence.renderVerified,
            "visual_fidelity_evidence.json has renderVerified=false. Do not score an unreviewed render set.");
        Require(!string.IsNullOrWhiteSpace(evidence.captureSessionId),
            "Visual evidence is missing captureSessionId binding.");
        Require(string.Equals(evidence.captureSessionId, receipt.captureSessionId, StringComparison.Ordinal),
            "Visual evidence captureSessionId does not match the sealed Unity capture receipt.");
        Require(IsSha256(evidence.captureManifestSha256),
            "Visual evidence is missing a valid captureManifestSha256 binding.");
        Require(IsSha256(evidence.captureReceiptSha256),
            "Visual evidence is missing a valid captureReceiptSha256 binding.");
        Require(string.Equals(evidence.captureManifestSha256, manifestHash, StringComparison.OrdinalIgnoreCase),
            "Visual evidence was reviewed against a different capture manifest SHA-256.");
        Require(string.Equals(evidence.captureReceiptSha256, receiptHash, StringComparison.OrdinalIgnoreCase),
            "Visual evidence was reviewed against a different capture receipt SHA-256.");
        Require(string.Equals(evidence.unityVersion, manifest.unityVersion, StringComparison.Ordinal),
            "Visual evidence Unity version does not match the sealed runtime manifest.");

        ValidateEvidenceCaptureBindings(manifest, evidence);
        List<FileProof> liveProofs = ValidateManifestFilesAndPixelExactCrops(manifest, true);
        ValidateReceiptFileProofs(receipt, liveProofs);

        return new ProvenanceReport
        {
            schemaVersion = "1.0",
            validatedUtc = DateTime.UtcNow.ToString("O"),
            status = "VALID_FOR_VISUAL_SCORING",
            captureSessionId = receipt.captureSessionId,
            unityVersion = manifest.unityVersion,
            manifestPath = ManifestPath,
            manifestSha256 = manifestHash,
            receiptPath = ReceiptPath,
            receiptSha256 = receiptHash,
            evidencePath = EvidencePath,
            evidenceSha256 = Sha256File(EvidencePath),
            requiredViews = RequiredViews,
            verifiedFiles = liveProofs.ToArray(),
            pixelExact100PercentCropsVerified = true,
            visualScoreAwardedByThisQA = false,
            note =
                "Provenance is valid for scoring the exact sealed capture set only. Category scores and PASS/FAIL are still decided by QualityBlockVisualFidelityGate."
        };
    }

    private static void ValidateRuntimeManifest(CaptureManifest manifest)
    {
        Require(manifest != null, "4K capture manifest is null.");
        Require(manifest.renderProducedByUnity,
            "4K capture manifest is still a template (renderProducedByUnity=false); no Visual Fidelity scoring is allowed.");
        Require(manifest.width == Width && manifest.height == Height,
            $"Runtime manifest must be native {Width}x{Height}.");
        Require(string.Equals(manifest.visualFidelityStatus, "UNSCORED_REVIEW_REQUIRED", StringComparison.Ordinal),
            "Runtime manifest must remain UNSCORED_REVIEW_REQUIRED before review.");
        Require(!string.IsNullOrWhiteSpace(manifest.unityVersion), "Runtime manifest is missing Unity version.");
        Require(!string.IsNullOrWhiteSpace(manifest.graphicsDevice), "Runtime manifest is missing graphics device.");
        Require(!string.IsNullOrWhiteSpace(manifest.graphicsApi), "Runtime manifest is missing graphics API.");
        Require(string.Equals(manifest.projectColorSpace, "Linear", StringComparison.OrdinalIgnoreCase),
            $"4K fidelity evidence must be rendered in Linear color space, got '{manifest.projectColorSpace}'.");
        Require(!string.IsNullOrWhiteSpace(manifest.source) && manifest.source.Contains("Unity Camera.Render"),
            "Runtime manifest source does not identify the Unity Camera.Render capture path.");
        Require(ParseRoundTripUtc(manifest.generatedUtc), "Runtime manifest generatedUtc is missing or invalid.");
        Require(manifest.reflectionEnvironment != null && manifest.reflectionEnvironment.probesRefreshedImmediatelyBeforeCapture,
            "Realtime reflection probes were not recorded as refreshed immediately before capture.");
        Require(manifest.captures != null && manifest.captures.Length == RequiredViews.Length,
            $"Runtime manifest must contain exactly {RequiredViews.Length} required views.");

        foreach (string viewId in RequiredViews)
            SingleCapture(manifest.captures, viewId, "manifest");
    }

    private static List<FileProof> ValidateManifestFilesAndPixelExactCrops(CaptureManifest manifest, bool comparePixels)
    {
        var proofs = new List<FileProof>();

        foreach (string viewId in RequiredViews)
        {
            ManifestCapture capture = SingleCapture(manifest.captures, viewId, "manifest");
            string expectedFramePath = $"Assets/QA/Captures4K/{viewId}_3840x2160.png";
            Require(string.Equals(capture.assetPath, expectedFramePath, StringComparison.Ordinal),
                $"{viewId} full-frame path must be the canonical capture output: {expectedFramePath}");
            Require(capture.width == Width && capture.height == Height,
                $"{viewId} manifest dimensions are not native {Width}x{Height}.");
            Require(capture.msaaSamples >= 1,
                $"{viewId} msaaSamples={capture.msaaSamples}; a template/non-runtime capture cannot be sealed.");
            Require(capture.cropRecords != null && capture.cropRecords.Length > 0,
                $"{viewId} has no 100% crop records.");

            Texture2D full = LoadPng(capture.assetPath, Width, Height);
            try
            {
                proofs.Add(MakeProof("full_frame", viewId, string.Empty, capture.assetPath, full.width, full.height));

                var cropIds = new HashSet<string>(StringComparer.Ordinal);
                var cropPaths = new HashSet<string>(StringComparer.Ordinal);
                foreach (ManifestCrop crop in capture.cropRecords)
                {
                    Require(crop != null, $"{viewId} contains a null crop record.");
                    Require(!string.IsNullOrWhiteSpace(crop.id) && cropIds.Add(crop.id),
                        $"{viewId} contains a missing/duplicate crop id '{crop.id}'.");
                    Require(!string.IsNullOrWhiteSpace(crop.assetPath) && cropPaths.Add(crop.assetPath),
                        $"{viewId} contains a missing/duplicate crop path '{crop.assetPath}'.");
                    Require(crop.assetPath.StartsWith($"Assets/QA/Captures4K/{viewId}_crop_", StringComparison.Ordinal) &&
                            crop.assetPath.EndsWith("_100pct.png", StringComparison.Ordinal),
                        $"{viewId}/{crop.id} crop path is not a canonical 100% capture path.");
                    Require(!crop.resampled,
                        $"{viewId}/{crop.id} is marked resampled=true; it is not valid 100% evidence.");
                    Require(crop.x >= 0 && crop.y >= 0 && crop.width > 0 && crop.height > 0 &&
                            crop.x + crop.width <= Width && crop.y + crop.height <= Height,
                        $"{viewId}/{crop.id} crop rectangle is outside the native frame.");

                    Texture2D cropTexture = LoadPng(crop.assetPath, crop.width, crop.height);
                    try
                    {
                        if (comparePixels)
                            ValidatePixelExactCrop(full, cropTexture, crop, viewId);
                        proofs.Add(MakeProof("100pct_crop", viewId, crop.id, crop.assetPath, cropTexture.width, cropTexture.height));
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(cropTexture);
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(full);
            }
        }

        return proofs;
    }

    private static void ValidatePixelExactCrop(Texture2D full, Texture2D cropTexture, ManifestCrop crop, string viewId)
    {
        Color32[] src = full.GetPixels32();
        Color32[] dst = cropTexture.GetPixels32();
        Require(dst.Length == crop.width * crop.height,
            $"{viewId}/{crop.id} decoded crop pixel count is invalid.");

        for (int y = 0; y < crop.height; y++)
        {
            int srcOffset = (crop.y + y) * full.width + crop.x;
            int dstOffset = y * crop.width;
            for (int x = 0; x < crop.width; x++)
            {
                if (!src[srcOffset + x].Equals(dst[dstOffset + x]))
                    throw new InvalidOperationException(
                        $"{viewId}/{crop.id} is not a pixel-exact 100% crop. First mismatch at crop pixel ({x},{y}). " +
                        "Resampling, sharpening, replacement or stale crop evidence is an automatic provenance failure.");
            }
        }
    }

    private static void ValidateEvidenceCaptureBindings(CaptureManifest manifest, BoundVisualEvidence evidence)
    {
        Require(evidence.captures != null && evidence.captures.Length == RequiredViews.Length,
            $"Visual evidence must bind exactly {RequiredViews.Length} views.");

        foreach (string viewId in RequiredViews)
        {
            ManifestCapture manifestCapture = SingleCapture(manifest.captures, viewId, "manifest");
            EvidenceCapture evidenceCapture = SingleCapture(evidence.captures, viewId, "visual evidence");

            Require(string.Equals(evidenceCapture.assetPath, manifestCapture.assetPath, StringComparison.Ordinal),
                $"{viewId} evidence full-frame path does not match the sealed manifest.");
            Require(evidenceCapture.width == manifestCapture.width && evidenceCapture.height == manifestCapture.height,
                $"{viewId} evidence dimensions do not match the sealed manifest.");

            string[] manifestCrops = manifestCapture.cropRecords.Select(x => x.assetPath).ToArray();
            string[] evidenceCrops = evidenceCapture.cropPaths ?? new string[0];
            Require(manifestCrops.Length == evidenceCrops.Length,
                $"{viewId} evidence crop count differs from the sealed manifest.");
            Require(new HashSet<string>(manifestCrops, StringComparer.Ordinal).SetEquals(evidenceCrops),
                $"{viewId} evidence crop paths are not exactly the sealed manifest crop set.");
        }
    }

    private static void ValidateReceiptAgainstCurrentCapture(CaptureManifest manifest, CaptureReceipt receipt,
        out string manifestHash, out string receiptHash)
    {
        Require(receipt != null, "Render capture receipt is null.");
        Require(receipt.renderProducedByUnity, "Render capture receipt does not attest a Unity-produced render.");
        Require(!string.IsNullOrWhiteSpace(receipt.captureSessionId), "Render capture receipt has no captureSessionId.");
        Require(ParseRoundTripUtc(receipt.sealedUtc), "Render capture receipt sealedUtc is missing or invalid.");
        Require(string.Equals(receipt.unityVersion, manifest.unityVersion, StringComparison.Ordinal),
            "Render capture receipt Unity version differs from current manifest.");
        Require(string.Equals(receipt.projectColorSpace, manifest.projectColorSpace, StringComparison.Ordinal),
            "Render capture receipt color space differs from current manifest.");
        Require(string.Equals(receipt.manifestPath, ManifestPath, StringComparison.Ordinal),
            "Render capture receipt points at an unexpected manifest path.");

        manifestHash = Sha256File(ManifestPath);
        Require(IsSha256(receipt.manifestSha256) &&
                string.Equals(receipt.manifestSha256, manifestHash, StringComparison.OrdinalIgnoreCase),
            "Current 4K capture manifest bytes no longer match the sealed receipt.");

        List<FileProof> liveProofs = ValidateManifestFilesAndPixelExactCrops(manifest, true);
        ValidateReceiptFileProofs(receipt, liveProofs);
        receiptHash = Sha256File(ReceiptPath);
    }

    private static void ValidateReceiptFileProofs(CaptureReceipt receipt, List<FileProof> liveProofs)
    {
        Require(receipt.files != null && receipt.files.Length == liveProofs.Count,
            "Render capture receipt file count does not match the current manifest evidence set.");

        var receiptByPath = receipt.files.ToDictionary(x => x.assetPath, StringComparer.Ordinal);
        foreach (FileProof live in liveProofs)
        {
            Require(receiptByPath.TryGetValue(live.assetPath, out FileProof sealedProof),
                $"Current render evidence file was not sealed in the receipt: {live.assetPath}");
            Require(string.Equals(sealedProof.sha256, live.sha256, StringComparison.OrdinalIgnoreCase),
                $"Render evidence bytes changed after sealing: {live.assetPath}");
            Require(sealedProof.width == live.width && sealedProof.height == live.height,
                $"Render evidence dimensions changed after sealing: {live.assetPath}");
            Require(string.Equals(sealedProof.kind, live.kind, StringComparison.Ordinal) &&
                    string.Equals(sealedProof.viewId, live.viewId, StringComparison.Ordinal) &&
                    string.Equals(sealedProof.cropId ?? string.Empty, live.cropId ?? string.Empty, StringComparison.Ordinal),
                $"Render evidence identity changed after sealing: {live.assetPath}");
        }
    }

    private static FileProof MakeProof(string kind, string viewId, string cropId, string assetPath, int width, int height)
    {
        return new FileProof
        {
            kind = kind,
            viewId = viewId,
            cropId = cropId,
            assetPath = assetPath,
            width = width,
            height = height,
            sha256 = Sha256File(assetPath)
        };
    }

    private static Texture2D LoadPng(string assetPath, int expectedWidth, int expectedHeight)
    {
        string absolute = AbsolutePath(assetPath);
        Require(File.Exists(absolute), $"Required render evidence file is missing: {assetPath}");
        Require(string.Equals(Path.GetExtension(assetPath), ".png", StringComparison.OrdinalIgnoreCase),
            $"Render evidence must be PNG: {assetPath}");

        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        try
        {
            Require(texture.LoadImage(File.ReadAllBytes(absolute), false), $"Could not decode PNG evidence: {assetPath}");
            Require(texture.width == expectedWidth && texture.height == expectedHeight,
                $"Decoded evidence dimensions for {assetPath} are {texture.width}x{texture.height}; expected {expectedWidth}x{expectedHeight}.");
            return texture;
        }
        catch
        {
            UnityEngine.Object.DestroyImmediate(texture);
            throw;
        }
    }

    private static ManifestCapture SingleCapture(ManifestCapture[] captures, string viewId, string source)
    {
        ManifestCapture[] matches = (captures ?? new ManifestCapture[0])
            .Where(x => x != null && string.Equals(x.viewId, viewId, StringComparison.Ordinal)).ToArray();
        Require(matches.Length == 1, $"{source} must contain exactly one '{viewId}' capture, found {matches.Length}.");
        return matches[0];
    }

    private static EvidenceCapture SingleCapture(EvidenceCapture[] captures, string viewId, string source)
    {
        EvidenceCapture[] matches = (captures ?? new EvidenceCapture[0])
            .Where(x => x != null && string.Equals(x.viewId, viewId, StringComparison.Ordinal)).ToArray();
        Require(matches.Length == 1, $"{source} must contain exactly one '{viewId}' capture, found {matches.Length}.");
        return matches[0];
    }

    private static string Sha256File(string assetPath)
    {
        string absolute = AbsolutePath(assetPath);
        Require(File.Exists(absolute), $"Cannot hash missing file: {assetPath}");
        using (SHA256 sha = SHA256.Create())
        using (FileStream stream = File.OpenRead(absolute))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static bool IsSha256(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 64) return false;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
            if (!hex) return false;
        }
        return true;
    }

    private static bool ParseRoundTripUtc(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime parsed) &&
               parsed != default;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static T LoadJson<T>(string assetPath)
    {
        string absolute = AbsolutePath(assetPath);
        if (!File.Exists(absolute)) throw new FileNotFoundException($"Required QA file not found: {assetPath}");
        T value = JsonUtility.FromJson<T>(File.ReadAllText(absolute));
        if (value == null) throw new InvalidOperationException($"Could not parse QA JSON: {assetPath}");
        return value;
    }

    private static void WriteJson<T>(string assetPath, T value)
    {
        string absolute = AbsolutePath(assetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute));
        File.WriteAllText(absolute, JsonUtility.ToJson(value, true));
        AssetDatabase.Refresh();
    }

    private static string AbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    [Serializable]
    private sealed class CaptureManifest
    {
        public string schemaVersion;
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
        public ReflectionEnvironmentState reflectionEnvironment;
        public ManifestCapture[] captures;
    }

    [Serializable]
    private sealed class ReflectionEnvironmentState
    {
        public bool probesRefreshedImmediatelyBeforeCapture;
    }

    [Serializable]
    private sealed class ManifestCapture
    {
        public string viewId;
        public string assetPath;
        public int width;
        public int height;
        public int msaaSamples;
        public ManifestCrop[] cropRecords;
    }

    [Serializable]
    private sealed class ManifestCrop
    {
        public string id;
        public string assetPath;
        public int x;
        public int y;
        public int width;
        public int height;
        public bool resampled;
    }

    [Serializable]
    private sealed class CaptureReceipt
    {
        public string schemaVersion;
        public string captureSessionId;
        public string sealedUtc;
        public string unityVersion;
        public string graphicsDevice;
        public string graphicsApi;
        public string projectColorSpace;
        public string manifestPath;
        public string manifestSha256;
        public bool renderProducedByUnity;
        public string[] requiredViews;
        public FileProof[] files;
        public string visualFidelityStatus;
        public string note;
    }

    [Serializable]
    private sealed class BoundVisualEvidence
    {
        public string schemaVersion;
        public string captureSessionId;
        public string captureManifestSha256;
        public string captureReceiptSha256;
        public bool renderVerified;
        public string unityVersion;
        public EvidenceCapture[] captures;
    }

    [Serializable]
    private sealed class BoundEvidenceTemplate
    {
        public string schemaVersion;
        public string captureSessionId;
        public string captureManifestSha256;
        public string captureReceiptSha256;
        public bool renderVerified;
        public string unityVersion;
        public EvidenceCapture[] captures;
        public CategoryEvidence[] categories;
        public CriticalDefectEvidence[] criticalDefects;
        public string instructions;
    }

    [Serializable]
    private sealed class EvidenceCapture
    {
        public string viewId;
        public string assetPath;
        public int width;
        public int height;
        public string[] cropPaths;
    }

    [Serializable]
    private sealed class CategoryEvidence
    {
        public string id;
        public int score;
        public string evidence;
        public string deductions;
        public string correctiveAction;
    }

    [Serializable]
    private sealed class CriticalDefectEvidence
    {
        public string id;
        public bool present;
        public string evidence;
    }

    [Serializable]
    private sealed class FileProof
    {
        public string kind;
        public string viewId;
        public string cropId;
        public string assetPath;
        public int width;
        public int height;
        public string sha256;
    }

    [Serializable]
    private sealed class ProvenanceReport
    {
        public string schemaVersion;
        public string validatedUtc;
        public string status;
        public string captureSessionId;
        public string unityVersion;
        public string manifestPath;
        public string manifestSha256;
        public string receiptPath;
        public string receiptSha256;
        public string evidencePath;
        public string evidenceSha256;
        public string[] requiredViews;
        public FileProof[] verifiedFiles;
        public bool pixelExact100PercentCropsVerified;
        public bool visualScoreAwardedByThisQA;
        public string note;
    }
}
