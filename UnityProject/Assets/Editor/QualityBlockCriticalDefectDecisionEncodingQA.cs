using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Fail-closed raw-JSON validation for automatic Visual Fidelity critical-defect decisions.
///
/// Unity JsonUtility initializes a missing bool field to false. For critical defects, that default is
/// unsafe because an omitted `present` field could otherwise look identical to an explicit "absent"
/// decision after deserialization. This validator therefore inspects the raw evidence packet before the
/// normal typed parser is trusted. It awards zero Visual Fidelity points.
/// </summary>
public static class QualityBlockCriticalDefectDecisionEncodingQA
{
    private const string ContractPath = "Assets/QA/critical_defect_decision_encoding_contract.json";
    private const string EvidencePath = "Assets/QA/visual_fidelity_evidence.json";
    private const string ContractSchemaVersion = "1.0";
    private const string CriticalDecisionSchemaVersion = "1.0";

    private static readonly string[] CanonicalCriticalDefectIds =
    {
        "visible_primitive_placeholder",
        "baked_or_painted_highlights",
        "impossible_material_physics",
        "obvious_repetition",
        "hero_geometry_intersection",
        "sun_shadow_inconsistency",
        "forbidden_disaster_theme",
        "severe_aliasing_or_shimmer",
        "visible_lod_pop",
        "major_light_leak",
        "missing_construction_material_metadata",
        "unverified_render_claim"
    };

    private static readonly string[] TerminalStatuses = { "absent", "present" };
    private static readonly string[] FailClosedStatuses = { "uncertain", "unchecked" };

    [MenuItem("NewTown/QA/Validate Critical Defect Decision Encoding")]
    public static void ValidateCurrentEvidence()
    {
        if (!File.Exists(EvidencePath))
            throw new FileNotFoundException(
                "Reviewed visual evidence does not exist yet. Real Unity render evidence must be captured and reviewed first.",
                EvidencePath);

        ValidateEvidenceEncoding(File.ReadAllText(EvidencePath));
        Debug.Log(
            "Critical-defect decision encoding valid: all 12 canonical defects have one explicit present boolean and one terminal reviewStatus, with exact boolean/status agreement. This QA awards 0 Visual Fidelity points.");
    }

    public static void ValidateEvidenceEncoding(string evidenceJson)
    {
        ValidateContractConfigOnly();

        if (string.IsNullOrWhiteSpace(evidenceJson))
            throw new InvalidOperationException("Reviewed evidence JSON is blank; critical-defect decisions cannot be proven.");

        MatchCollection schemaMatches = Regex.Matches(
            evidenceJson,
            "\\\"criticalDecisionSchemaVersion\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"",
            RegexOptions.CultureInvariant);
        if (schemaMatches.Count != 1)
            throw new InvalidOperationException(
                $"Reviewed evidence must contain exactly one explicit criticalDecisionSchemaVersion field; got {schemaMatches.Count}.");
        if (!string.Equals(schemaMatches[0].Groups[1].Value, CriticalDecisionSchemaVersion, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Reviewed evidence criticalDecisionSchemaVersion must be '{CriticalDecisionSchemaVersion}', got '{schemaMatches[0].Groups[1].Value}'.");

        List<string> rawEntries = ExtractCriticalDefectObjects(evidenceJson);
        if (rawEntries.Count != CanonicalCriticalDefectIds.Length)
            throw new InvalidOperationException(
                $"Reviewed evidence must contain exactly {CanonicalCriticalDefectIds.Length} critical-defect objects, got {rawEntries.Count}.");

        var observedIds = new List<string>(rawEntries.Count);
        foreach (string rawEntry in rawEntries)
        {
            MatchCollection idMatches = Regex.Matches(
                rawEntry,
                "\\\"id\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"\\s*(?=[,}])",
                RegexOptions.CultureInvariant);
            if (idMatches.Count != 1)
                throw new InvalidOperationException(
                    $"Each critical-defect object must contain exactly one explicit string id field; got {idMatches.Count}.");

            string id = idMatches[0].Groups[1].Value;
            observedIds.Add(id);

            MatchCollection presentMatches = Regex.Matches(
                rawEntry,
                "\\\"present\\\"\\s*:\\s*(true|false)\\s*(?=[,}])",
                RegexOptions.CultureInvariant);
            if (presentMatches.Count != 1)
                throw new InvalidOperationException(
                    $"Critical defect '{id}' must contain exactly one explicit JSON boolean present field; got {presentMatches.Count}. Missing booleans are never inferred as false.");

            bool present = string.Equals(presentMatches[0].Groups[1].Value, "true", StringComparison.Ordinal);

            MatchCollection statusMatches = Regex.Matches(
                rawEntry,
                "\\\"reviewStatus\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"\\s*(?=[,}])",
                RegexOptions.CultureInvariant);
            if (statusMatches.Count != 1)
                throw new InvalidOperationException(
                    $"Critical defect '{id}' must contain exactly one explicit reviewStatus field; got {statusMatches.Count}.");

            string status = statusMatches[0].Groups[1].Value;
            if (FailClosedStatuses.Contains(status, StringComparer.Ordinal))
                throw new InvalidOperationException(
                    $"Critical defect '{id}' has non-terminal reviewStatus='{status}'. Uncertain/unchecked critical decisions fail closed and require review.");
            if (!TerminalStatuses.Contains(status, StringComparer.Ordinal))
                throw new InvalidOperationException(
                    $"Critical defect '{id}' has unknown reviewStatus='{status}'. Allowed scoreable statuses are absent/present only.");

            bool statusSaysPresent = string.Equals(status, "present", StringComparison.Ordinal);
            if (present != statusSaysPresent)
                throw new InvalidOperationException(
                    $"Critical defect '{id}' decision mismatch: present={present.ToString().ToLowerInvariant()} but reviewStatus='{status}'.");
        }

        RequireExactSet(observedIds.ToArray(), CanonicalCriticalDefectIds, "critical-defect raw decision IDs");
    }

    public static void ValidateContractConfigOnly()
    {
        if (!File.Exists(ContractPath))
            throw new FileNotFoundException($"Critical-defect decision encoding contract missing: {ContractPath}");

        DecisionContract contract = JsonUtility.FromJson<DecisionContract>(File.ReadAllText(ContractPath));
        if (contract == null)
            throw new InvalidOperationException("Critical-defect decision encoding contract could not be parsed.");
        if (!string.Equals(contract.schemaVersion, ContractSchemaVersion, StringComparison.Ordinal) ||
            !string.Equals(contract.criticalDecisionSchemaVersion, CriticalDecisionSchemaVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("Critical-defect decision encoding schema version drifted.");
        if (contract.runtimeRenderVerified || contract.visualScoreAwardedByThisQA)
            throw new InvalidOperationException("Critical-defect decision encoding QA may not claim runtime rendering or award Visual Fidelity points.");

        RequireExactSet(contract.criticalDefectIds, CanonicalCriticalDefectIds, "critical-defect decision contract IDs");
        RequireExactSet(contract.terminalScoreableStatuses, TerminalStatuses, "terminal scoreable critical statuses");
        RequireExactSet(contract.failClosedStatuses, FailClosedStatuses, "fail-closed critical statuses");

        DecisionRules rules = contract.rules;
        if (rules == null ||
            !rules.requireExactlyTwelveCriticalDefectObjects ||
            !rules.requireCanonicalCriticalDefectIdsExactlyOnce ||
            !rules.requireExplicitPresentBooleanPerDefect ||
            !rules.requireExactlyOnePresentFieldPerDefect ||
            !rules.requireExplicitReviewStatusPerDefect ||
            !rules.requireExactlyOneReviewStatusFieldPerDefect ||
            !rules.requireStatusBooleanAgreement ||
            !rules.rejectUncertainOrUncheckedCriticalDecision ||
            !rules.rejectUnknownReviewStatus ||
            !rules.missingOrDuplicateDecisionFieldFails ||
            rules.automaticVisualFidelityUplift != 0)
            throw new InvalidOperationException("Critical-defect decision encoding contract was weakened or drifted.");
    }

    private static List<string> ExtractCriticalDefectObjects(string json)
    {
        MatchCollection propertyMatches = Regex.Matches(
            json,
            "\\\"criticalDefects\\\"\\s*:",
            RegexOptions.CultureInvariant);
        if (propertyMatches.Count != 1)
            throw new InvalidOperationException(
                $"Reviewed evidence must contain exactly one criticalDefects array property; got {propertyMatches.Count}.");

        int arrayStart = json.IndexOf('[', propertyMatches[0].Index + propertyMatches[0].Length);
        if (arrayStart < 0)
            throw new InvalidOperationException("criticalDefects property is not followed by a JSON array.");

        var objects = new List<string>();
        bool inString = false;
        bool escaping = false;
        int arrayDepth = 0;
        int objectDepth = 0;
        int objectStart = -1;
        bool closedRootArray = false;

        for (int i = arrayStart; i < json.Length; i++)
        {
            char c = json[i];
            if (inString)
            {
                if (escaping)
                {
                    escaping = false;
                    continue;
                }

                if (c == '\\')
                {
                    escaping = true;
                    continue;
                }

                if (c == '"')
                    inString = false;
                continue;
            }

            if (c == '"')
            {
                inString = true;
                continue;
            }

            if (c == '[')
            {
                arrayDepth++;
                continue;
            }

            if (c == ']')
            {
                arrayDepth--;
                if (arrayDepth < 0)
                    throw new InvalidOperationException("criticalDefects array has unbalanced brackets.");
                if (arrayDepth == 0)
                {
                    if (objectDepth != 0)
                        throw new InvalidOperationException("criticalDefects array ended inside an object.");
                    closedRootArray = true;
                    break;
                }
                continue;
            }

            if (c == '{')
            {
                if (arrayDepth == 1 && objectDepth == 0)
                    objectStart = i;
                if (objectDepth > 0 || arrayDepth == 1)
                    objectDepth++;
                continue;
            }

            if (c == '}')
            {
                if (objectDepth <= 0)
                    throw new InvalidOperationException("criticalDefects array contains an unmatched object terminator.");
                objectDepth--;
                if (objectDepth == 0)
                {
                    if (objectStart < 0)
                        throw new InvalidOperationException("criticalDefects object start could not be determined.");
                    objects.Add(json.Substring(objectStart, i - objectStart + 1));
                    objectStart = -1;
                }
                continue;
            }

            if (arrayDepth == 1 && objectDepth == 0 && !char.IsWhiteSpace(c) && c != ',')
                throw new InvalidOperationException(
                    $"criticalDefects array may contain objects only; unexpected token '{c}' at character {i}.");
        }

        if (!closedRootArray)
            throw new InvalidOperationException("criticalDefects array was not terminated.");
        if (inString || objectDepth != 0)
            throw new InvalidOperationException("criticalDefects raw JSON is structurally incomplete.");

        return objects;
    }

    private static void RequireExactSet(string[] actual, string[] expected, string label)
    {
        if (actual == null)
            throw new InvalidOperationException($"{label} is missing.");
        if (actual.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException($"{label} contains a blank value.");
        if (actual.Distinct(StringComparer.Ordinal).Count() != actual.Length)
            throw new InvalidOperationException($"{label} contains duplicates.");

        var actualSet = new HashSet<string>(actual, StringComparer.Ordinal);
        var expectedSet = new HashSet<string>(expected ?? Array.Empty<string>(), StringComparer.Ordinal);
        if (!actualSet.SetEquals(expectedSet))
            throw new InvalidOperationException(
                $"{label} must be exactly [{string.Join(", ", expectedSet)}], got [{string.Join(", ", actualSet)}].");
    }

    [Serializable]
    private sealed class DecisionContract
    {
        public string schemaVersion;
        public string criticalDecisionSchemaVersion;
        public string[] criticalDefectIds;
        public string[] terminalScoreableStatuses;
        public string[] failClosedStatuses;
        public DecisionRules rules;
        public bool runtimeRenderVerified;
        public bool visualScoreAwardedByThisQA;
    }

    [Serializable]
    private sealed class DecisionRules
    {
        public bool requireExactlyTwelveCriticalDefectObjects;
        public bool requireCanonicalCriticalDefectIdsExactlyOnce;
        public bool requireExplicitPresentBooleanPerDefect;
        public bool requireExactlyOnePresentFieldPerDefect;
        public bool requireExplicitReviewStatusPerDefect;
        public bool requireExactlyOneReviewStatusFieldPerDefect;
        public bool requireStatusBooleanAgreement;
        public bool rejectUncertainOrUncheckedCriticalDecision;
        public bool rejectUnknownReviewStatus;
        public bool missingOrDuplicateDecisionFieldFails;
        public int automaticVisualFidelityUplift;
    }
}
