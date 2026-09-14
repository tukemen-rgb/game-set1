using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Fail-closed source-membership gate for the benchmark rainwater system.
///
/// The construction/material evidence receipt derives its SHA-256 source set from
/// scene_metadata_coverage_contract.json. Therefore the authoritative rainwater installation
/// contract and the single-authority policy must both remain explicit members of the Danchi
/// domain before formal still, temporal or reflection evidence can be rendered.
///
/// This gate is source/provenance QA only. It never repairs a render callback and awards zero
/// Visual Fidelity points.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockRainwaterEvidenceBindingQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string CoveragePath = "Assets/QA/scene_metadata_coverage_contract.json";
    private const string AuthorityPath = "Assets/QA/rainwater_system_authority_contract.json";
    private const string InstallationPath = "Assets/QA/rainwater_downpipe_installation_contract.json";
    private const string DanchiDomainId = "danchi_domain";
    private const string DanchiRootName = "Danchi";

    private static readonly string[] RequiredLinkedContracts =
    {
        InstallationPath,
        AuthorityPath
    };

    static QualityBlockRainwaterEvidenceBindingQA()
    {
        Camera.onPreCull -= OnCameraPreCull;
        Camera.onPreCull += OnCameraPreCull;
    }

    [MenuItem("NewTown/QA/Validate Rainwater Evidence Binding Membership")]
    public static void ValidateContractConfigOnly()
    {
        ValidateMembershipOrThrow();

        // Prove that the project-wide evidence binder can resolve the complete coverage-derived
        // source bundle. This remains outside per-camera callbacks to avoid repeated whole-bundle IO.
        QualityBlockConstructionMaterialEvidenceBindingQA.ValidateContractConfigOnly();

        Debug.Log(
            "Rainwater evidence membership valid: installation + single-authority contracts are " +
            "members of danchi_domain and therefore enter the construction/material SHA-256 bundle. " +
            "This source gate awards 0 Visual Fidelity points.");
    }

    [MenuItem("NewTown/QA/Validate Rainwater Evidence Binding In Open Scene")]
    public static void ValidateOpenScene()
    {
        ValidateContractConfigOnly();
        QualityBlockRainwaterSystemAuthorityQA.ValidateOpenScene(true);
    }

    private static void OnCameraPreCull(Camera camera)
    {
        if (camera == null) return;
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath) return;

        string cameraName = camera.name ?? string.Empty;
        bool formal = cameraName.StartsWith("QA4K_", StringComparison.Ordinal) ||
                      cameraName.StartsWith("QATemporal_", StringComparison.Ordinal) ||
                      cameraName.StartsWith("QAPreparedTemporal_", StringComparison.Ordinal);
        bool reflection = camera.cameraType == CameraType.Reflection;
        if (!formal && !reflection) return;

        // Read-only and fail-closed. No source/scene repair is allowed from a render callback.
        ValidateMembershipOrThrow();
    }

    private static void ValidateMembershipOrThrow()
    {
        var errors = new List<string>();

        CoverageContract coverage = ReadJson<CoverageContract>(CoveragePath, errors);
        AuthorityContract authority = ReadJson<AuthorityContract>(AuthorityPath, errors);
        RequireFile(InstallationPath, errors);

        if (coverage != null)
        {
            CoverageDomain[] matches = (coverage.domains ?? Array.Empty<CoverageDomain>())
                .Where(x => x != null && string.Equals(x.id, DanchiDomainId, StringComparison.Ordinal))
                .ToArray();

            if (matches.Length != 1)
            {
                errors.Add($"Expected exactly one '{DanchiDomainId}' metadata domain; found {matches.Length}.");
            }
            else
            {
                CoverageDomain danchi = matches[0];
                if (!string.Equals(danchi.rootName, DanchiRootName, StringComparison.Ordinal))
                    errors.Add($"{DanchiDomainId}.rootName must remain '{DanchiRootName}', got '{danchi.rootName}'.");

                string[] paths = danchi.contractPaths ?? Array.Empty<string>();
                if (paths.Length != paths.Distinct(StringComparer.Ordinal).Count())
                    errors.Add($"{DanchiDomainId}.contractPaths contains duplicate entries.");

                foreach (string required in RequiredLinkedContracts)
                {
                    int count = paths.Count(x => string.Equals(x, required, StringComparison.Ordinal));
                    if (count != 1)
                        errors.Add(
                            $"{DanchiDomainId}.contractPaths must contain exactly one '{required}' entry; found {count}. " +
                            "Without it the rainwater source cannot be part of the render-bound metadata bundle.");
                }
            }
        }

        if (authority != null)
        {
            EvidenceBinding evidence = authority.evidenceBinding;
            if (evidence == null)
            {
                errors.Add("Rainwater authority contract is missing evidenceBinding policy.");
            }
            else
            {
                if (!string.Equals(evidence.sceneMetadataCoverageContract, CoveragePath, StringComparison.Ordinal))
                    errors.Add("Rainwater authority evidenceBinding points at a non-canonical metadata coverage contract.");
                if (!string.Equals(evidence.domainId, DanchiDomainId, StringComparison.Ordinal))
                    errors.Add($"Rainwater authority evidenceBinding.domainId must remain '{DanchiDomainId}'.");

                string[] required = evidence.requiredLinkedContracts ?? Array.Empty<string>();
                if (required.Length != required.Distinct(StringComparer.Ordinal).Count() ||
                    !new HashSet<string>(required, StringComparer.Ordinal).SetEquals(RequiredLinkedContracts))
                    errors.Add("Rainwater authority requiredLinkedContracts must be exactly installation + authority contracts.");

                if (!evidence.requireExactSha256WithRenderedScene ||
                    !evidence.rejectPostCaptureContractMutation ||
                    !evidence.freshUnityRenderRequiredAfterMutation)
                    errors.Add("Rainwater authority evidence-binding SHA/mutation rules were weakened.");
                if (evidence.sourcePassAwardsPoints || evidence.automaticPoints != 0)
                    errors.Add("Rainwater evidence membership may not award Visual Fidelity points.");
            }
        }

        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Rainwater evidence binding membership FAILED (fail-closed before formal evidence):\n - " +
                string.Join("\n - ", errors));
    }

    private static T ReadJson<T>(string assetPath, List<string> errors) where T : class
    {
        string absolute = Absolute(assetPath);
        if (!File.Exists(absolute))
        {
            errors.Add("Required rainwater evidence source is missing: " + assetPath);
            return null;
        }

        try
        {
            T value = JsonUtility.FromJson<T>(File.ReadAllText(absolute));
            if (value == null)
                errors.Add("Required rainwater evidence source is unparseable: " + assetPath);
            return value;
        }
        catch (Exception ex)
        {
            errors.Add($"Could not parse {assetPath}: {ex.Message}");
            return null;
        }
    }

    private static void RequireFile(string assetPath, List<string> errors)
    {
        if (!File.Exists(Absolute(assetPath)))
            errors.Add("Required rainwater evidence source is missing: " + assetPath);
    }

    private static string Absolute(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrWhiteSpace(projectRoot))
            throw new InvalidOperationException("Could not resolve Unity project root for rainwater evidence QA.");
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    [Serializable]
    private sealed class CoverageContract
    {
        public CoverageDomain[] domains;
    }

    [Serializable]
    private sealed class CoverageDomain
    {
        public string id;
        public string rootName;
        public string[] contractPaths;
    }

    [Serializable]
    private sealed class AuthorityContract
    {
        public EvidenceBinding evidenceBinding;
    }

    [Serializable]
    private sealed class EvidenceBinding
    {
        public string sceneMetadataCoverageContract;
        public string domainId;
        public string[] requiredLinkedContracts;
        public bool requireExactSha256WithRenderedScene;
        public bool rejectPostCaptureContractMutation;
        public bool freshUnityRenderRequiredAfterMutation;
        public bool sourcePassAwardsPoints;
        public int automaticPoints;
    }
}
