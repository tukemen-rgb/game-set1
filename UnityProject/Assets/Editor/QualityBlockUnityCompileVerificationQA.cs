using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

/// <summary>
/// Fail-closed proof for the 7-point Unity compile component of Implementation Readiness.
///
/// Merely placing Assets/QA/unity_compile_verified.json in the repository is not sufficient. A valid
/// evidence file must be written by this class after Unity completes a requested CleanBuildCache script
/// compilation, with zero script-compilation errors, under the exact project Unity version, and while a
/// deterministic SHA-256 of every tracked compile input remains unchanged across the compile. Readiness
/// re-hashes those inputs every time, so any later C#/asmdef/package/project-setting change makes the
/// evidence stale automatically.
///
/// This class never awards Visual Fidelity points and never claims render quality.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockUnityCompileVerificationQA
{
    private const string ContractPath = "Assets/QA/unity_compile_verification_contract.json";
    private const string EvidencePath = "Assets/QA/unity_compile_verified.json";
    private const string SentinelPath = "Library/QualityBlockUnityCompileVerificationRequest.json";
    private const string RequiredUnityVersion = "6000.3.0f1";
    private const string CompileMode = "CLEAN_BUILD_CACHE";
    private const string RecorderId = "QualityBlockUnityCompileVerificationQA.OnCompilationFinished";

    private static readonly string[] SourceRoots =
    {
        "Assets",
        "Packages"
    };

    private static readonly string[] IncludedSuffixes =
    {
        ".cs",
        ".asmdef",
        ".asmref",
        ".rsp",
        ".dll",
        ".winmd",
        ".dll.meta",
        ".winmd.meta",
        ".asmdef.meta",
        ".asmref.meta",
        ".rsp.meta"
    };

    private static readonly string[] ExplicitInputPaths =
    {
        "Packages/manifest.json",
        "Packages/packages-lock.json",
        "ProjectSettings/ProjectVersion.txt",
        "ProjectSettings/ProjectSettings.asset"
    };

    static QualityBlockUnityCompileVerificationQA()
    {
        CompilationPipeline.compilationFinished -= OnCompilationFinished;
        CompilationPipeline.compilationFinished += OnCompilationFinished;
    }

    [MenuItem("NewTown/QA/Request Clean Unity Compile Verification")]
    public static void RequestCleanCompileVerification()
    {
        ValidateContractConfigOnly();

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException(
                "Cannot start compile verification while Unity is already compiling or updating. Retry from an idle editor/runner session.");

        InputFingerprint input = BuildCurrentInputFingerprint();
        DeleteIfExists(AbsoluteProjectPath(EvidencePath));

        var request = new VerificationRequest
        {
            schemaVersion = "1.0",
            requestedUtc = DateTime.UtcNow.ToString("O"),
            unityVersion = Application.unityVersion,
            compileMode = CompileMode,
            compileInputSha256 = input.sha256,
            compileInputFileCount = input.fileCount
        };
        WriteJsonAbsolute(AbsoluteProjectPath(SentinelPath), request);
        AssetDatabase.Refresh();

        Debug.Log(
            $"Requested full clean Unity script compile verification for {input.fileCount} deterministic inputs, SHA-256={input.sha256}. " +
            "Implementation Readiness compile points remain unavailable until compilationFinished records fresh evidence.");

        CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache);
    }

    [MenuItem("NewTown/QA/Validate Unity Compile Evidence For Readiness")]
    public static void ValidateEvidenceForReadiness()
    {
        if (!TryValidateEvidenceForReadiness(out string reason))
            throw new InvalidOperationException("Unity compile verification is not readiness-valid: " + reason);

        Debug.Log("Unity compile verification VALID for Implementation Readiness: " + reason +
                  " Visual Fidelity remains unaffected and unscored until real render evidence is reviewed.");
    }

    public static bool TryValidateEvidenceForReadiness(out string reason)
    {
        try
        {
            ValidateContractConfigOnly();

            string absoluteEvidence = AbsoluteProjectPath(EvidencePath);
            if (!File.Exists(absoluteEvidence))
            {
                reason = "real clean-compile evidence is missing";
                return false;
            }

            CompileEvidence evidence = JsonUtility.FromJson<CompileEvidence>(File.ReadAllText(absoluteEvidence));
            if (evidence == null)
            {
                reason = "compile evidence JSON is unparseable";
                return false;
            }

            Require(string.Equals(evidence.schemaVersion, "1.0", StringComparison.Ordinal),
                $"compile evidence schema must be 1.0, got '{evidence.schemaVersion}'");
            Require(string.Equals(evidence.status, "VERIFIED_CLEAN_COMPILE", StringComparison.Ordinal),
                $"compile evidence status must be VERIFIED_CLEAN_COMPILE, got '{evidence.status}'");
            Require(evidence.verified,
                "compile evidence verified flag is false");
            Require(string.Equals(evidence.unityVersion, RequiredUnityVersion, StringComparison.Ordinal),
                $"compile evidence Unity version must be {RequiredUnityVersion}, got '{evidence.unityVersion}'");
            Require(string.Equals(evidence.compileMode, CompileMode, StringComparison.Ordinal),
                $"compile evidence mode must be {CompileMode}, got '{evidence.compileMode}'");
            Require(evidence.cleanBuildCacheRequested,
                "compile evidence does not prove a CleanBuildCache request");
            Require(!evidence.scriptCompilationFailed,
                "compile evidence reports script compilation failure");
            Require(string.Equals(evidence.recordedBy, RecorderId, StringComparison.Ordinal),
                $"compile evidence recordedBy is not the authoritative recorder '{RecorderId}'");
            Require(evidence.implementationReadinessPoints == 7,
                $"compile evidence must represent exactly 7 readiness points, got {evidence.implementationReadinessPoints}");
            Require(evidence.visualFidelityPointsAwarded == 0,
                "compile verification must never award Visual Fidelity points");
            Require(!string.IsNullOrWhiteSpace(evidence.verifiedUtc),
                "compile evidence has no verifiedUtc timestamp");
            Require(DateTime.TryParse(evidence.verifiedUtc, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out DateTime verifiedUtc),
                "compile evidence verifiedUtc is not a valid round-trip timestamp");
            Require(verifiedUtc.ToUniversalTime() <= DateTime.UtcNow.AddMinutes(5),
                "compile evidence timestamp is implausibly in the future");

            InputFingerprint current = BuildCurrentInputFingerprint();
            Require(evidence.compileInputFileCount == current.fileCount,
                $"compile input count is stale: evidence={evidence.compileInputFileCount}, current={current.fileCount}");
            Require(string.Equals(evidence.compileInputSha256, current.sha256, StringComparison.OrdinalIgnoreCase),
                "compile input SHA-256 is stale; project compile inputs changed after verification");

            reason = $"fresh clean compile verified with Unity {evidence.unityVersion}; inputs={current.fileCount}, SHA-256={current.sha256}";
            return true;
        }
        catch (Exception ex)
        {
            reason = ex.Message;
            return false;
        }
    }

    [MenuItem("NewTown/QA/Validate Unity Compile Verification Contract")]
    public static void ValidateContractConfigOnly()
    {
        string absoluteContract = AbsoluteProjectPath(ContractPath);
        Require(File.Exists(absoluteContract), "Unity compile verification contract is missing: " + ContractPath);

        Contract contract = JsonUtility.FromJson<Contract>(File.ReadAllText(absoluteContract));
        Require(contract != null, "Unity compile verification contract is null/unparseable.");
        Require(string.Equals(contract.schemaVersion, "1.0", StringComparison.Ordinal),
            $"Unity compile verification contract schema must be 1.0, got '{contract.schemaVersion}'.");
        Require(string.Equals(contract.requiredUnityVersion, RequiredUnityVersion, StringComparison.Ordinal),
            $"Compile-verification Unity version drifted from {RequiredUnityVersion}.");
        Require(string.Equals(contract.evidencePath, EvidencePath, StringComparison.Ordinal),
            "Compile-verification evidence path drifted.");
        Require(string.Equals(contract.requestSentinelPath, SentinelPath, StringComparison.Ordinal),
            "Compile-verification request sentinel path drifted.");
        Require(string.Equals(contract.compileMode, CompileMode, StringComparison.Ordinal),
            "Compile-verification mode must remain CLEAN_BUILD_CACHE.");
        RequireExactSet(contract.sourceRoots, SourceRoots, "compile source roots");
        RequireExactSet(contract.includedSuffixes, IncludedSuffixes, "compile input suffixes");
        RequireExactSet(contract.explicitInputPaths, ExplicitInputPaths, "explicit compile inputs");
        Require(contract.rules != null &&
                contract.rules.requireCleanBuildCacheRequest &&
                contract.rules.requireExactUnityVersion &&
                contract.rules.requireStableCompileInputHashAcrossCompile &&
                contract.rules.requireScriptCompilationFailedFalse &&
                contract.rules.rejectMissingEvidence &&
                contract.rules.rejectStaleInputHash &&
                contract.rules.deletePriorEvidenceBeforeVerificationRequest,
            "Unity compile verification fail-closed rules were weakened.");
        Require(contract.rules.readinessPoints == 7,
            "Unity compile verification must remain worth exactly 7 Implementation Readiness points.");
        Require(contract.rules.visualFidelityPointsAwarded == 0,
            "Unity compile verification contract must award zero Visual Fidelity points.");
        Require(contract.verification != null &&
                !contract.verification.runtimeCompileVerified &&
                contract.verification.implementationReadinessPointsAwardedNow == 0 &&
                contract.verification.visualFidelityPointsAwarded == 0 &&
                string.Equals(contract.verification.status, "PENDING_REAL_UNITY_CLEAN_COMPILE", StringComparison.Ordinal),
            "Source-side compile verification contract must remain explicitly pending real Unity execution.");
    }

    private static void OnCompilationFinished(object context)
    {
        string sentinelAbsolute = AbsoluteProjectPath(SentinelPath);
        if (!File.Exists(sentinelAbsolute))
            return;

        try
        {
            ValidateContractConfigOnly();
            VerificationRequest request = JsonUtility.FromJson<VerificationRequest>(File.ReadAllText(sentinelAbsolute));
            Require(request != null, "Compile verification request sentinel is unparseable.");
            Require(string.Equals(request.schemaVersion, "1.0", StringComparison.Ordinal),
                "Compile verification request sentinel schema drifted.");
            Require(string.Equals(request.unityVersion, RequiredUnityVersion, StringComparison.Ordinal),
                "Compile verification request was created by the wrong Unity version.");
            Require(string.Equals(Application.unityVersion, RequiredUnityVersion, StringComparison.Ordinal),
                $"Current Unity runtime must be {RequiredUnityVersion}, got '{Application.unityVersion}'.");
            Require(string.Equals(request.compileMode, CompileMode, StringComparison.Ordinal),
                "Compile verification request did not request CLEAN_BUILD_CACHE.");
            Require(!EditorUtility.scriptCompilationFailed,
                "Unity reports script compilation errors after the requested clean compile.");

            InputFingerprint current = BuildCurrentInputFingerprint();
            Require(request.compileInputFileCount == current.fileCount,
                "Compile input file count changed while the clean compile was running.");
            Require(string.Equals(request.compileInputSha256, current.sha256, StringComparison.OrdinalIgnoreCase),
                "Compile input SHA-256 changed while the clean compile was running.");

            var evidence = new CompileEvidence
            {
                schemaVersion = "1.0",
                status = "VERIFIED_CLEAN_COMPILE",
                verified = true,
                verifiedUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                compileMode = CompileMode,
                cleanBuildCacheRequested = true,
                scriptCompilationFailed = false,
                compileInputSha256 = current.sha256,
                compileInputFileCount = current.fileCount,
                recordedBy = RecorderId,
                implementationReadinessPoints = 7,
                visualFidelityPointsAwarded = 0,
                note = "Clean Unity script-compilation evidence only. Freshness is revalidated from deterministic compile-input bytes before readiness points are awarded; this evidence never awards Visual Fidelity points."
            };
            WriteJsonAbsolute(AbsoluteProjectPath(EvidencePath), evidence);
            File.Delete(sentinelAbsolute);
            AssetDatabase.Refresh();

            Debug.Log(
                $"Unity clean compile VERIFIED: {current.fileCount} inputs, SHA-256={current.sha256}, Unity={Application.unityVersion}. " +
                "The 7 Implementation Readiness points are now eligible while this input hash remains current. Visual Fidelity points awarded: 0.");
        }
        catch (Exception ex)
        {
            DeleteIfExists(AbsoluteProjectPath(EvidencePath));
            DeleteIfExists(sentinelAbsolute);
            AssetDatabase.Refresh();
            Debug.LogError(
                "Unity clean compile verification failed closed; no compile-readiness evidence was retained: " + ex.Message);
        }
    }

    private static InputFingerprint BuildCurrentInputFingerprint()
    {
        string projectRoot = ProjectRoot();
        var relativePaths = new HashSet<string>(StringComparer.Ordinal);

        foreach (string root in SourceRoots)
        {
            string absoluteRoot = Path.Combine(projectRoot, root);
            Require(Directory.Exists(absoluteRoot), "Compile source root is missing: " + root);
            foreach (string absoluteFile in Directory.GetFiles(absoluteRoot, "*", SearchOption.AllDirectories))
            {
                string normalized = absoluteFile.Replace('\\', '/');
                if (IncludedSuffixes.Any(suffix => normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)))
                    relativePaths.Add(ToProjectRelative(projectRoot, absoluteFile));
            }
        }

        foreach (string explicitPath in ExplicitInputPaths)
        {
            string absolute = Path.Combine(projectRoot, explicitPath);
            Require(File.Exists(absolute), "Explicit compile input is missing: " + explicitPath);
            relativePaths.Add(explicitPath.Replace('\\', '/'));
        }

        string[] ordered = relativePaths.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        using (SHA256 sha = SHA256.Create())
        {
            foreach (string relative in ordered)
            {
                byte[] pathBytes = Encoding.UTF8.GetBytes(relative + "\n");
                sha.TransformBlock(pathBytes, 0, pathBytes.Length, pathBytes, 0);

                string absolute = Path.Combine(projectRoot, relative.Replace('/', Path.DirectorySeparatorChar));
                using (FileStream stream = File.OpenRead(absolute))
                {
                    byte[] buffer = new byte[64 * 1024];
                    int read;
                    while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                        sha.TransformBlock(buffer, 0, read, buffer, 0);
                }

                byte[] separator = { 0 };
                sha.TransformBlock(separator, 0, separator.Length, separator, 0);
            }
            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            string hash = BitConverter.ToString(sha.Hash).Replace("-", string.Empty).ToLowerInvariant();
            return new InputFingerprint { sha256 = hash, fileCount = ordered.Length };
        }
    }

    private static string ToProjectRelative(string projectRoot, string absolutePath)
    {
        string root = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                      + Path.DirectorySeparatorChar;
        string full = Path.GetFullPath(absolutePath);
        Require(full.StartsWith(root, StringComparison.OrdinalIgnoreCase),
            "Compile input escaped the Unity project root: " + full);
        return full.Substring(root.Length).Replace('\\', '/');
    }

    private static void RequireExactSet(string[] actual, string[] expected, string label)
    {
        string[] a = actual ?? Array.Empty<string>();
        Require(a.Length == a.Distinct(StringComparer.Ordinal).Count(), label + " contains duplicates.");
        Require(new HashSet<string>(a, StringComparer.Ordinal).SetEquals(expected),
            label + " must remain the exact authoritative set.");
    }

    private static void WriteJsonAbsolute<T>(string absolutePath, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
        File.WriteAllText(absolutePath, JsonUtility.ToJson(value, true));
    }

    private static void DeleteIfExists(string absolutePath)
    {
        if (File.Exists(absolutePath))
            File.Delete(absolutePath);
    }

    private static string AbsoluteProjectPath(string projectRelativePath)
    {
        return Path.GetFullPath(Path.Combine(ProjectRoot(), projectRelativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string ProjectRoot()
    {
        return Directory.GetParent(Application.dataPath).FullName;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class InputFingerprint
    {
        public string sha256;
        public int fileCount;
    }

    [Serializable]
    private sealed class Contract
    {
        public string schemaVersion;
        public string requiredUnityVersion;
        public string evidencePath;
        public string requestSentinelPath;
        public string compileMode;
        public string[] sourceRoots;
        public string[] includedSuffixes;
        public string[] explicitInputPaths;
        public ContractRules rules;
        public SourceVerification verification;
    }

    [Serializable]
    private sealed class ContractRules
    {
        public bool requireCleanBuildCacheRequest;
        public bool requireExactUnityVersion;
        public bool requireStableCompileInputHashAcrossCompile;
        public bool requireScriptCompilationFailedFalse;
        public bool rejectMissingEvidence;
        public bool rejectStaleInputHash;
        public bool deletePriorEvidenceBeforeVerificationRequest;
        public int readinessPoints;
        public int visualFidelityPointsAwarded;
    }

    [Serializable]
    private sealed class SourceVerification
    {
        public bool runtimeCompileVerified;
        public int implementationReadinessPointsAwardedNow;
        public int visualFidelityPointsAwarded;
        public string status;
    }

    [Serializable]
    private sealed class VerificationRequest
    {
        public string schemaVersion;
        public string requestedUtc;
        public string unityVersion;
        public string compileMode;
        public string compileInputSha256;
        public int compileInputFileCount;
    }

    [Serializable]
    private sealed class CompileEvidence
    {
        public string schemaVersion;
        public string status;
        public bool verified;
        public string verifiedUtc;
        public string unityVersion;
        public string compileMode;
        public bool cleanBuildCacheRequested;
        public bool scriptCompilationFailed;
        public string compileInputSha256;
        public int compileInputFileCount;
        public string recordedBy;
        public int implementationReadinessPoints;
        public int visualFidelityPointsAwarded;
        public string note;
    }
}
