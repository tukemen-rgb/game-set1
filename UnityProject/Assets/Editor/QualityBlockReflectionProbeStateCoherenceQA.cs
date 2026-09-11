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
/// Freezes the exact realtime ReflectionProbe capture/influence state used by formal 4K evidence.
/// The digest is folded into the existing reflection-lighting request/poll/completion/pre-still hash.
/// This is evidence integrity only and awards zero Visual Fidelity points.
/// </summary>
public static class QualityBlockReflectionProbeStateCoherenceQA
{
    public const string Algorithm = "SHA-256";
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/reflection_probe_state_coherence_contract.json";
    private const string ProbeRootName = "PhysicalReflectionEnvironment";
    private const float Epsilon = 0.0001f;

    private static readonly ExpectedProbe[] Expected =
    {
        new ExpectedProbe("ReflectionProbe_DanchiFacade", new Vector3(-8f, 5.7f, -2.5f), new Vector3(34f, 17f, 27f), new Vector3(0f, 1.2f, -3f), 100),
        new ExpectedProbe("ReflectionProbe_ParkGround", new Vector3(10f, 4.2f, 2.5f), new Vector3(31f, 13f, 30f), new Vector3(-1.5f, 0.8f, -2.5f), 90),
    };

    [MenuItem("NewTown/QA/Validate Reflection Probe State Coherence Contract")]
    public static void ValidateContractConfigOnly()
    {
        Contract c = LoadContract();
        if (c.schemaVersion != "1.0" || c.status != "PENDING_REAL_UNITY_4K_RENDER")
            throw new InvalidOperationException("Reflection-probe state coherence must remain schema 1.0 and render-pending.");
        if (c.fingerprintAlgorithm != Algorithm ||
            c.fingerprintImplementation != "QualityBlockReflectionProbeStateCoherenceQA.BuildValidatedCurrentSha256")
            throw new InvalidOperationException("Reflection-probe state fingerprint implementation/algorithm drifted.");
        if (c.probeRoot != ProbeRootName || c.probeCount != Expected.Length || c.probes == null || c.probes.Length != Expected.Length)
            throw new InvalidOperationException("Reflection-probe root/count/configuration contract drifted.");

        string[] actualNames = c.probes.Select(x => x == null ? null : x.name).ToArray();
        RequireExactSet(actualNames, Expected.Select(x => x.Name).ToArray(), "reflection-probe identities");
        foreach (ExpectedProbe e in Expected)
            ValidateRequirement(c.probes.Single(x => x.name == e.Name), e);

        TemporalBinding t = c.temporalBinding;
        if (t == null || !t.foldIntoReflectionLightingFingerprint || !t.validateBeforeProbeRequest ||
            !t.validateEveryEditorPollWhileWaiting || !t.validateAfterProbeCompletion ||
            !t.validateImmediatelyBeforeStillCapture || !t.requireRequestCompletionMatch ||
            !t.requireCompletionStillMatch || !t.actualRenderRequiredForVisualPoints)
            throw new InvalidOperationException("Reflection-probe request/poll/completion/pre-still binding was weakened.");

        string[] fields =
        {
            "worldPosition", "worldRotation", "lossyScale", "size", "center", "blendDistance",
            "boxProjection", "resolution", "hdr", "intensity", "importance", "nearClipPlane",
            "farClipPlane", "shadowDistance", "cullingMask", "clearFlags", "backgroundColor",
            "mode", "refreshMode", "timeSlicingMode", "enabled", "activeInHierarchy"
        };
        RequireExactSet(c.fingerprintedFields, fields, "reflection-probe fingerprintedFields");
        if (c.verification == null || c.verification.visualFidelityPointsAwarded != 0 || c.verification.visualFidelityStatus != "UNSCORED")
            throw new InvalidOperationException("Probe-state source QA cannot award Visual Fidelity points before actual render review.");
    }

    public static string BuildValidatedCurrentSha256()
    {
        ValidateContractConfigOnly();
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            throw new InvalidOperationException("Probe-state fingerprint requires the persisted benchmark scene.");

        GameObject root = Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x.scene.IsValid() && x.name == ProbeRootName);
        if (root == null || !root.activeInHierarchy)
            throw new InvalidOperationException("Active physical reflection-probe root is missing.");

        ReflectionProbe[] probes = root.GetComponentsInChildren<ReflectionProbe>(true)
            .OrderBy(x => x.name, StringComparer.Ordinal).ToArray();
        if (probes.Length != Expected.Length)
            throw new InvalidOperationException($"Expected {Expected.Length} formal reflection probes, found {probes.Length}.");

        var sb = new StringBuilder(4096);
        Add(sb, "schema", "reflection-probe-state-v1");
        Add(sb, "scene", EditorSceneManager.GetActiveScene().path);
        Add(sb, "unityVersion", Application.unityVersion);
        foreach (ExpectedProbe e in Expected.OrderBy(x => x.Name, StringComparer.Ordinal))
        {
            ReflectionProbe p = probes.SingleOrDefault(x => x.name == e.Name);
            if (p == null) throw new InvalidOperationException("Canonical reflection probe missing: " + e.Name);
            ValidateProbe(p, e);
            AppendProbe(sb, p);
        }

        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString())))
            .Replace("-", string.Empty).ToLowerInvariant();
    }

    private static void ValidateRequirement(ProbeRequirement r, ExpectedProbe e)
    {
        if (r == null || !V(r.worldPosition, e.Position) || !Q(r.worldRotation, Quaternion.identity) ||
            !V(r.worldScale, Vector3.one) || !V(r.size, e.Size) || !V(r.center, e.Center) ||
            !Near(r.blendDistance, 4.5f) || !r.boxProjection || r.resolution != 512 || !r.hdr ||
            !Near(r.intensity, 1f) || r.importance != e.Importance || !Near(r.nearClipPlane, 0.25f) ||
            !Near(r.farClipPlane, 95f) || !Near(r.shadowDistance, 70f) || r.cullingMask != -1 ||
            r.clearFlags != "Skybox" || r.mode != "Realtime" || r.refreshMode != "ViaScripting" ||
            r.timeSlicingMode != "NoTimeSlicing")
            throw new InvalidOperationException("Canonical ReflectionProbe contract drifted for " + e.Name + ".");
    }

    private static void ValidateProbe(ReflectionProbe p, ExpectedProbe e)
    {
        if (!p.isActiveAndEnabled || !p.gameObject.activeInHierarchy)
            throw new InvalidOperationException(p.name + " must remain active and enabled.");
        if (!V(p.transform.position, e.Position) || !Q(p.transform.rotation, Quaternion.identity) || !V(p.transform.lossyScale, Vector3.one))
            throw new InvalidOperationException(p.name + " world transform drifted from the authored probe installation.");
        if (!V(p.size, e.Size) || !V(p.center, e.Center) || !Near(p.blendDistance, 4.5f) || !p.boxProjection)
            throw new InvalidOperationException(p.name + " influence/capture volume drifted.");
        if (p.resolution != 512 || !p.hdr || !Near(p.intensity, 1f) || p.importance != e.Importance)
            throw new InvalidOperationException(p.name + " resolution/HDR/intensity/importance drifted.");
        if (!Near(p.nearClipPlane, 0.25f) || !Near(p.farClipPlane, 95f) || !Near(p.shadowDistance, 70f))
            throw new InvalidOperationException(p.name + " clip/shadow capture distance drifted.");
        if (p.cullingMask != -1)
            throw new InvalidOperationException(p.name + " uses selective culling; all benchmark layers are required in reflections.");
        if (p.clearFlags != ReflectionProbeClearFlags.Skybox)
            throw new InvalidOperationException(p.name + " must clear from the physical skybox.");
        if (!Finite(p.backgroundColor.r) || !Finite(p.backgroundColor.g) || !Finite(p.backgroundColor.b) || !Finite(p.backgroundColor.a))
            throw new InvalidOperationException(p.name + " has non-finite fallback background state.");
        if (p.mode != ReflectionProbeMode.Realtime || p.refreshMode != ReflectionProbeRefreshMode.ViaScripting ||
            p.timeSlicingMode != ReflectionProbeTimeSlicingMode.NoTimeSlicing)
            throw new InvalidOperationException(p.name + " realtime refresh/time-slicing policy drifted.");
    }

    private static void AppendProbe(StringBuilder sb, ReflectionProbe p)
    {
        string k = "probe." + p.name + ".";
        Add(sb, k + "enabled", p.enabled); Add(sb, k + "activeInHierarchy", p.gameObject.activeInHierarchy);
        Add(sb, k + "worldPosition", p.transform.position); Add(sb, k + "worldRotation", p.transform.rotation);
        Add(sb, k + "lossyScale", p.transform.lossyScale); Add(sb, k + "size", p.size); Add(sb, k + "center", p.center);
        Add(sb, k + "blendDistance", p.blendDistance); Add(sb, k + "boxProjection", p.boxProjection);
        Add(sb, k + "resolution", p.resolution); Add(sb, k + "hdr", p.hdr); Add(sb, k + "intensity", p.intensity);
        Add(sb, k + "importance", p.importance); Add(sb, k + "nearClipPlane", p.nearClipPlane);
        Add(sb, k + "farClipPlane", p.farClipPlane); Add(sb, k + "shadowDistance", p.shadowDistance);
        Add(sb, k + "cullingMask", p.cullingMask); Add(sb, k + "clearFlags", p.clearFlags.ToString());
        Add(sb, k + "backgroundColor", p.backgroundColor); Add(sb, k + "mode", p.mode.ToString());
        Add(sb, k + "refreshMode", p.refreshMode.ToString()); Add(sb, k + "timeSlicingMode", p.timeSlicingMode.ToString());
    }

    private static Contract LoadContract()
    {
        string path = Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, ContractPath));
        if (!File.Exists(path)) throw new FileNotFoundException("Probe-state contract missing: " + ContractPath);
        Contract c = JsonUtility.FromJson<Contract>(File.ReadAllText(path));
        if (c == null) throw new InvalidOperationException("Probe-state contract is unreadable.");
        return c;
    }

    private static void RequireExactSet(string[] actual, string[] expected, string label)
    {
        if (actual == null || actual.Any(string.IsNullOrWhiteSpace) || actual.Distinct(StringComparer.Ordinal).Count() != actual.Length ||
            !new HashSet<string>(actual, StringComparer.Ordinal).SetEquals(expected))
            throw new InvalidOperationException(label + " must be exactly [" + string.Join(", ", expected) + "].");
    }

    private static bool Near(float a, float b) => Mathf.Abs(a - b) <= Epsilon;
    private static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
    private static bool V(Vector3 a, Vector3 b) => (a - b).sqrMagnitude <= Epsilon * Epsilon;
    private static bool V(Vec a, Vector3 b) => a != null && Near(a.x, b.x) && Near(a.y, b.y) && Near(a.z, b.z);
    private static bool Q(Quaternion a, Quaternion b) => Mathf.Abs(Quaternion.Dot(a, b)) >= 1f - Epsilon;
    private static bool Q(Quat a, Quaternion b) => a != null && Near(a.x, b.x) && Near(a.y, b.y) && Near(a.z, b.z) && Near(a.w, b.w);

    private static void Add(StringBuilder sb, string key, string v) => sb.Append(key).Append('=').Append(v ?? string.Empty).Append('\n');
    private static void Add(StringBuilder sb, string key, int v) => Add(sb, key, v.ToString(CultureInfo.InvariantCulture));
    private static void Add(StringBuilder sb, string key, float v) => Add(sb, key, v.ToString("R", CultureInfo.InvariantCulture));
    private static void Add(StringBuilder sb, string key, bool v) => Add(sb, key, v ? "true" : "false");
    private static void Add(StringBuilder sb, string key, Vector3 v) { Add(sb, key + ".x", v.x); Add(sb, key + ".y", v.y); Add(sb, key + ".z", v.z); }
    private static void Add(StringBuilder sb, string key, Quaternion v) { Add(sb, key + ".x", v.x); Add(sb, key + ".y", v.y); Add(sb, key + ".z", v.z); Add(sb, key + ".w", v.w); }
    private static void Add(StringBuilder sb, string key, Color v) { Add(sb, key + ".r", v.r); Add(sb, key + ".g", v.g); Add(sb, key + ".b", v.b); Add(sb, key + ".a", v.a); }

    private sealed class ExpectedProbe
    {
        public readonly string Name; public readonly Vector3 Position; public readonly Vector3 Size; public readonly Vector3 Center; public readonly int Importance;
        public ExpectedProbe(string name, Vector3 position, Vector3 size, Vector3 center, int importance)
        { Name = name; Position = position; Size = size; Center = center; Importance = importance; }
    }

    [Serializable] private sealed class Contract
    {
        public string schemaVersion; public string status; public string fingerprintAlgorithm; public string fingerprintImplementation;
        public string probeRoot; public int probeCount; public ProbeRequirement[] probes; public string[] fingerprintedFields;
        public TemporalBinding temporalBinding; public Verification verification;
    }
    [Serializable] private sealed class ProbeRequirement
    {
        public string name; public Vec worldPosition; public Quat worldRotation; public Vec worldScale; public Vec size; public Vec center;
        public float blendDistance; public bool boxProjection; public int resolution; public bool hdr; public float intensity; public int importance;
        public float nearClipPlane; public float farClipPlane; public float shadowDistance; public int cullingMask;
        public string clearFlags; public string mode; public string refreshMode; public string timeSlicingMode;
    }
    [Serializable] private sealed class Vec { public float x; public float y; public float z; }
    [Serializable] private sealed class Quat { public float x; public float y; public float z; public float w; }
    [Serializable] private sealed class TemporalBinding
    {
        public bool foldIntoReflectionLightingFingerprint; public bool validateBeforeProbeRequest; public bool validateEveryEditorPollWhileWaiting;
        public bool validateAfterProbeCompletion; public bool validateImmediatelyBeforeStillCapture; public bool requireRequestCompletionMatch;
        public bool requireCompletionStillMatch; public bool actualRenderRequiredForVisualPoints;
    }
    [Serializable] private sealed class Verification
    {
        public int visualFidelityPointsAwarded; public string visualFidelityStatus;
    }
}
