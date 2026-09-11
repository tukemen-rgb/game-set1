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
/// Validates and fingerprints the exact realtime ReflectionProbe configuration used by formal 4K evidence.
/// A cubemap can be freshly rendered under the correct sun/material state and still be invalid evidence if
/// probe position, influence volume, capture center, intensity, clipping, culling, clear policy or importance
/// drifted between the probe request and the still. The resulting SHA-256 is folded into the existing
/// reflection-lighting fingerprint so request -> every Editor poll -> completion -> pre-still validation
/// observes one identical probe configuration. This is evidence-integrity infrastructure only and awards
/// zero Visual Fidelity points.
/// </summary>
public static class QualityBlockReflectionProbeStateCoherenceQA
{
    public const string Algorithm = "SHA-256";

    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/reflection_probe_state_coherence_contract.json";
    private const string ContractSchema = "1.0";
    private const string ProbeRootName = "PhysicalReflectionEnvironment";
    private const float FloatTolerance = 0.0001f;

    private static readonly CanonicalProbe[] CanonicalProbes =
    {
        new CanonicalProbe(
            "ReflectionProbe_DanchiFacade",
            new Vector3(-8f, 5.7f, -2.5f),
            new Vector3(34f, 17f, 27f),
            new Vector3(0f, 1.2f, -3f),
            100),
        new CanonicalProbe(
            "ReflectionProbe_ParkGround",
            new Vector3(10f, 4.2f, 2.5f),
            new Vector3(31f, 13f, 30f),
            new Vector3(-1.5f, 0.8f, -2.5f),
            90),
    };

    [MenuItem("NewTown/QA/Validate Reflection Probe State Coherence Contract")]
    public static void ValidateContractConfigOnly()
    {
        ProbeStateContract contract = LoadContract();
        if (contract.schemaVersion != ContractSchema)
            throw new InvalidOperationException(
                $"Reflection-probe state coherence contract schema must remain {ContractSchema}.");
        if (contract.status != "PENDING_REAL_UNITY_4K_RENDER")
            throw new InvalidOperationException(
                "Reflection-probe state coherence must remain render-pending until actual native-4K evidence is reviewed.");
        if (contract.fingerprintAlgorithm != Algorithm ||
            contract.fingerprintImplementation != "QualityBlockReflectionProbeStateCoherenceQA.BuildValidatedCurrentSha256")
            throw new InvalidOperationException("Reflection-probe state fingerprint implementation/algorithm drifted.");
        if (contract.probeRoot != ProbeRootName || contract.probeCount != CanonicalProbes.Length)
            throw new InvalidOperationException("Reflection-probe root/count contract drifted.");
        if (contract.probes == null || contract.probes.Length != CanonicalProbes.Length)
            throw new InvalidOperationException("Reflection-probe canonical configuration records are missing.");

        string[] expectedNames = CanonicalProbes.Select(x => x.Name).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        string[] actualNames = contract.probes.Select(x => x == null ? null : x.name)
            .OrderBy(x => x, StringComparer.Ordinal).ToArray();
        if (actualNames.Any(string.IsNullOrWhiteSpace) ||
            !actualNames.SequenceEqual(expectedNames, StringComparer.Ordinal))
            throw new InvalidOperationException("Reflection-probe state contract identities drifted.");

        foreach (CanonicalProbe canonical in CanonicalProbes)
        {
            ProbeRequirement requirement = contract.probes.Single(x => x.name == canonical.Name);
            ValidateRequirementAgainstCanonical(requirement, canonical);
        }

        TemporalBinding binding = contract.temporalBinding;
        if (binding == null ||
            !binding.foldIntoReflectionLightingFingerprint ||
            !binding.validateBeforeProbeRequest ||
            !binding.validateEveryEditorPollWhileWaiting ||
            !binding.validateAfterProbeCompletion ||
            !binding.validateImmediatelyBeforeStillCapture ||
            !binding.requireRequestCompletionMatch ||
            !binding.requireCompletionStillMatch ||
            !binding.actualRenderRequiredForVisualPoints)
            throw new InvalidOperationException(
                "Reflection-probe state temporal binding was weakened; request/poll/completion/pre-still identity and actual-render-only scoring are mandatory.");

        string[] requiredFields =
        {
            "worldPosition", "worldRotation", "lossyScale", "size", "center", "blendDistance",
            "boxProjection", "resolution", "hdr", "intensity", "importance", "nearClipPlane",
            "farClipPlane", "shadowDistance", "cullingMask", "clearFlags", "backgroundColor",
            "mode", "refreshMode", "timeSlicingMode", "enabled", "activeInHierarchy"
        };
        RequireExactSet(contract.fingerprintedFields, requiredFields, "reflection-probe fingerprintedFields");

        if (contract.verification == null || contract.verification.visualFidelityPointsAwarded != 0 ||
            contract.verification.visualFidelityStatus != "UNSCORED")
            throw new InvalidOperationException(
                "Reflection-probe state coherence contract must award zero Visual Fidelity points before actual render review.");
    }

    public static string BuildValidatedCurrentSha256()
    {
        ValidateContractConfigOnly();
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            throw new InvalidOperationException($"Reflection-probe state fingerprint requires persisted benchmark scene: {ScenePath}");

        GameObject root = Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x.scene.IsValid() && x.name == ProbeRootName);
        if (root == null || !root.activeInHierarchy)
            throw new InvalidOperationException($"Active reflection-probe root is missing: {ProbeRootName}");

        ReflectionProbe[] probes = root.GetComponentsInChildren<ReflectionProbe>(true)
            .OrderBy(x => x.name, StringComparer.Ordinal)
            .ToArray();
        if (probes.Length != CanonicalProbes.Length)
            throw new InvalidOperationException(
                $"Expected exactly {CanonicalProbes.Length} formal reflection probes, found {probes.Length}.");

        var sb = new StringBuilder(4096);
        Append(sb, "schema", "reflection-probe-state-v1");
        Append(sb, "scene", EditorSceneManager.GetActiveScene().path);
        Append(sb, "unityVersion", Application.unityVersion);
        Append(sb, "root.name", root.name);

        foreach (CanonicalProbe canonical in CanonicalProbes.OrderBy(x => x.Name, StringComparer.Ordinal))
        {
            ReflectionProbe probe = probes.SingleOrDefault(x => x.name == canonical.Name);
            if (probe == null)
                throw new InvalidOperationException($"Canonical reflection probe missing: {canonical.Name}");
            ValidateProbeAgainstCanonical(probe, canonical);
            AppendProbeState(sb, probe);
        }

        using var sha = SHA256.Create();
        byte[] payload = Encoding.UTF8.GetBytes(sb.ToString());
        return BitConverter.ToString(sha.ComputeHash(payload)).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static void ValidateRequirementAgainstCanonical(ProbeRequirement r, CanonicalProbe c)
    {
        if (!VectorMatches(r.worldPosition, c.WorldPosition) ||
            !VectorMatches(r.size, c.Size) ||
            !VectorMatches(r.center, c.Center) ||
            !VectorMatches(r.worldScale, Vector3.one) ||
            !QuaternionMatches(r.worldRotation, Quaternion.identity) ||
            !Nearly(r.blendDistance, 4.5f) || !r.boxProjection || r.resolution != 512 || !r.hdr ||
            !Nearly(r.intensity, 1f) || r.importance != c.Importance ||
            !Nearly(r.nearClipPlane, 0.25f) || !Nearly(r.farClipPlane, 95f) ||
            !Nearly(r.shadowDistance, 70f) || r.cullingMask != -1 ||
            r.clearFlags != "Skybox" || !ColorMatches(r.backgroundColor, Color.black) ||
            r.mode != "Realtime" || r.refreshMode != "ViaScripting" ||
            r.timeSlicingMode != "NoTimeSlicing")
            throw new InvalidOperationException($"Reflection-probe canonical requirement drifted for {c.Name}.");
    }

    private static void ValidateProbeAgainstCanonical(ReflectionProbe probe, CanonicalProbe c)
    {
        if (!probe.isActiveAndEnabled || !probe.gameObject.activeInHierarchy)
            throw new InvalidOperationException($"{probe.name} must remain active and enabled.");
        if (!VectorClose(probe.transform.position, c.WorldPosition) ||
            !QuaternionClose(probe.transform.rotation, Quaternion.identity) ||
            !VectorClose(probe.transform.lossyScale, Vector3.one))
            throw new InvalidOperationException(
                $"{probe.name} transform drifted. Expected world position {c.WorldPosition}, identity rotation and unit world scale.");
        if (!VectorClose(probe.size, c.Size) || !VectorClose(probe.center, c.Center))
            throw new InvalidOperationException($"{probe.name} influence/capture volume drifted.");
        if (!Nearly(probe.blendDistance, 4.5f) || !probe.boxProjection || probe.resolution != 512 || !probe.hdr)
            throw new InvalidOperationException($"{probe.name} blend/box/HDR/resolution policy drifted.");
        if (!Nearly(probe.intensity, 1f) || probe.importance != c.Importance)
            throw new InvalidOperationException($"{probe.name} intensity/importance drifted.");
        if (!Nearly(probe.nearClipPlane, 0.25f) || !Nearly(probe.farClipPlane, 95f) ||
            !Nearly(probe.shadowDistance, 70f))
            throw new InvalidOperationException($"{probe.name} capture clipping/shadow distance drifted.");
        if (probe.cullingMask != -1)
            throw new InvalidOperationException($"{probe.name} must render all benchmark layers; selective culling is prohibited.");
        if (probe.clearFlags != ReflectionProbeClearFlags.Skybox)
            throw new InvalidOperationException($"{probe.name} must clear from the physical skybox.");
        if (!ColorClose(probe.backgroundColor, Color.black))
            throw new InvalidOperationException($"{probe.name} fallback background color drifted from canonical black.");
        if (probe.mode != ReflectionProbeMode.Realtime ||
            probe.refreshMode != ReflectionProbeRefreshMode.ViaScripting ||
            probe.timeSlicingMode != ReflectionProbeTimeSlicingMode.NoTimeSlicing)
            throw new InvalidOperationException($"{probe.name} realtime refresh/time-slicing policy drifted.");
    }

    private static void AppendProbeState(StringBuilder sb, ReflectionProbe p)
    {
        string prefix = "probe." + p.name + ".";
        Append(sb, prefix + "enabled", p.enabled);
        Append(sb, prefix + "activeInHierarchy", p.gameObject.activeInHierarchy);
        AppendVector(sb, prefix + "worldPosition", p.transform.position);
        AppendQuaternion(sb, prefix + "worldRotation", p.transform.rotation);
        AppendVector(sb, prefix + "lossyScale", p.transform.lossyScale);
        AppendVector(sb, prefix + "size", p.size);
        AppendVector(sb, prefix + "center", p.center);
        Append(sb, prefix + "blendDistance", p.blendDistance);
        Append(sb, prefix + "boxProjection", p.boxProjection);
        Append(sb, prefix + "resolution", p.resolution);
        Append(sb, prefix + "hdr", p.hdr);
        Append(sb, prefix + "intensity", p.intensity);
        Append(sb, prefix + "importance", p.importance);
        Append(sb, prefix + "nearClipPlane", p.nearClipPlane);
        Append(sb, prefix + "farClipPlane", p.farClipPlane);
        Append(sb, prefix + "shadowDistance", p.shadowDistance);
        Append(sb, prefix + "cullingMask", p.cullingMask);
        Append(sb, prefix + "clearFlags", p.clearFlags.ToString());
        AppendColor(sb, prefix + "backgroundColor", p.backgroundColor);
        Append(sb, prefix + "mode", p.mode.ToString());
        Append(sb, prefix + "refreshMode", p.refreshMode.ToString());
        Append(sb, prefix + "timeSlicingMode", p.timeSlicingMode.ToString());
    }

    private static ProbeStateContract LoadContract()
    {
        string absolute = AbsolutePath(ContractPath);
        if (!File.Exists(absolute))
            throw new FileNotFoundException("Reflection-probe state coherence contract missing: " + ContractPath);
        ProbeStateContract contract = JsonUtility.FromJson<ProbeStateContract>(File.ReadAllText(absolute));
        if (contract == null)
            throw new InvalidOperationException("Reflection-probe state coherence contract is unreadable.");
        return contract;
    }

    private static void RequireExactSet(string[] actual, string[] expected, string label)
    {
        if (actual == null || actual.Any(string.IsNullOrWhiteSpace) ||
            actual.Distinct(StringComparer.Ordinal).Count() != actual.Length ||
            !new HashSet<string>(actual, StringComparer.Ordinal).SetEquals(expected))
            throw new InvalidOperationException(
                label + " must be exactly [" + string.Join(", ", expected) + "].");
    }

    private static bool VectorMatches(VectorRecord value, Vector3 expected) =>
        value != null && Nearly(value.x, expected.x) && Nearly(value.y, expected.y) && Nearly(value.z, expected.z);

    private static bool QuaternionMatches(QuaternionRecord value, Quaternion expected) =>
        value != null && Nearly(value.x, expected.x) && Nearly(value.y, expected.y) &&
        Nearly(value.z, expected.z) && Nearly(value.w, expected.w);

    private static bool ColorMatches(ColorRecord value, Color expected) =>
        value != null && Nearly(value.r, expected.r) && Nearly(value.g, expected.g) &&
        Nearly(value.b, expected.b) && Nearly(value.a, expected.a);

    private static bool VectorClose(Vector3 a, Vector3 b) => (a - b).sqrMagnitude <= FloatTolerance * FloatTolerance;

    private static bool QuaternionClose(Quaternion a, Quaternion b) =>
        Mathf.Abs(Quaternion.Dot(a, b)) >= 1f - FloatTolerance;

    private static bool ColorClose(Color a, Color b) =>
        Nearly(a.r, b.r) && Nearly(a.g, b.g) && Nearly(a.b, b.b) && Nearly(a.a, b.a);

    private static bool Nearly(float a, float b) => Mathf.Abs(a - b) <= FloatTolerance;

    private static string AbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private static void AppendVector(StringBuilder sb, string key, Vector3 value)
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

    private static void AppendColor(StringBuilder sb, string key, Color value)
    {
        Append(sb, key + ".r", value.r);
        Append(sb, key + ".g", value.g);
        Append(sb, key + ".b", value.b);
        Append(sb, key + ".a", value.a);
    }

    private static void Append(StringBuilder sb, string key, float value) =>
        Append(sb, key, value.ToString("R", CultureInfo.InvariantCulture));

    private static void Append(StringBuilder sb, string key, int value) =>
        Append(sb, key, value.ToString(CultureInfo.InvariantCulture));

    private static void Append(StringBuilder sb, string key, bool value) => Append(sb, key, value ? "true" : "false");

    private static void Append(StringBuilder sb, string key, string value) =>
        sb.Append(key).Append('=').Append(value ?? string.Empty).Append('\n');

    private sealed class CanonicalProbe
    {
        public readonly string Name;
        public readonly Vector3 WorldPosition;
        public readonly Vector3 Size;
        public readonly Vector3 Center;
        public readonly int Importance;

        public CanonicalProbe(string name, Vector3 worldPosition, Vector3 size, Vector3 center, int importance)
        {
            Name = name;
            WorldPosition = worldPosition;
            Size = size;
            Center = center;
            Importance = importance;
        }
    }

    [Serializable]
    private sealed class ProbeStateContract
    {
        public string schemaVersion;
        public string status;
        public string fingerprintAlgorithm;
        public string fingerprintImplementation;
        public string probeRoot;
        public int probeCount;
        public ProbeRequirement[] probes;
        public string[] fingerprintedFields;
        public TemporalBinding temporalBinding;
        public Verification verification;
    }

    [Serializable]
    private sealed class ProbeRequirement
    {
        public string name;
        public VectorRecord worldPosition;
        public QuaternionRecord worldRotation;
        public VectorRecord worldScale;
        public VectorRecord size;
        public VectorRecord center;
        public float blendDistance;
        public bool boxProjection;
        public int resolution;
        public bool hdr;
        public float intensity;
        public int importance;
        public float nearClipPlane;
        public float farClipPlane;
        public float shadowDistance;
        public int cullingMask;
        public string clearFlags;
        public ColorRecord backgroundColor;
        public string mode;
        public string refreshMode;
        public string timeSlicingMode;
    }

    [Serializable]
    private sealed class VectorRecord { public float x; public float y; public float z; }
    [Serializable]
    private sealed class QuaternionRecord { public float x; public float y; public float z; public float w; }
    [Serializable]
    private sealed class ColorRecord { public float r; public float g; public float b; public float a; }

    [Serializable]
    private sealed class TemporalBinding
    {
        public bool foldIntoReflectionLightingFingerprint;
        public bool validateBeforeProbeRequest;
        public bool validateEveryEditorPollWhileWaiting;
        public bool validateAfterProbeCompletion;
        public bool validateImmediatelyBeforeStillCapture;
        public bool requireRequestCompletionMatch;
        public bool requireCompletionStillMatch;
        public bool actualRenderRequiredForVisualPoints;
    }

    [Serializable]
    private sealed class Verification
    {
        public bool sourceImplemented;
        public bool unityCompileVerified;
        public bool native4kRenderVerified;
        public int visualFidelityPointsAwarded;
        public string visualFidelityStatus;
    }
}
