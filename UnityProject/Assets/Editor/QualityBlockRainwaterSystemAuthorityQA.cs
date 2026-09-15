using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Fail-closed authority gate for the benchmark rainwater leader.
///
/// The project already has one formal, dimensioned rainwater implementation:
/// RainGutter supplies the continuous all-distance silhouette and
/// HD_RainwaterDownpipeAssembly supplies hollow joints, restraints, roof offset and receiver.
/// This gate prevents a second independently positioned system from silently coexisting with,
/// disabling or replacing that formal authority.
///
/// Render callbacks are read-only. Passing this gate awards zero Visual Fidelity points.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockRainwaterSystemAuthorityQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/rainwater_system_authority_contract.json";
    private const string ConstructionContractPath = "Assets/QA/rainwater_downpipe_installation_contract.json";
    private const string ConstructionQaPath = "Assets/Editor/QualityBlockRainwaterDownpipeInstallationQA.cs";
    private const string LookdevPath = "Assets/QA/Lookdev/rainwater_downpipe_installation.svg";
    private const string Native4KPath = "Assets/Editor/QualityBlockNative4KReviewPacket.cs";
    private const string DanchiName = "Danchi";
    private const string DetailRootName = "DanchiHighDetail";
    private const string PipeName = "RainGutter";
    private const string AccessoryRootName = "HD_RainwaterDownpipeAssembly";
    private const string ForbiddenDuplicateRootName = "DanchiRainwaterDrainage";

    private static readonly string[] ForbiddenDuplicatePaths =
    {
        "Assets/Editor/QualityBlockRainwaterDownpipeUpgrade.cs",
        "Assets/Editor/QualityBlockRainwaterDownpipeConstructionQA.cs",
        "Assets/QA/rainwater_downpipe_contract.json",
        "Assets/QA/rainwater_downpipe_lookdev.svg"
    };

    private const string RequiredApplyToken = "QualityBlockRainwaterDownpipeInstallationQA.ApplyAndPersist();";
    private const string RequiredValidateToken = "QualityBlockRainwaterDownpipeInstallationQA.ValidateOpenScene();";

    static QualityBlockRainwaterSystemAuthorityQA()
    {
        Camera.onPreCull -= OnCameraPreCull;
        Camera.onPreCull += OnCameraPreCull;
    }

    [MenuItem("NewTown/QA/Validate Rainwater System Authority Contract")]
    public static void ValidateContractConfigOnly()
    {
        var errors = new List<string>();
        string contract = ReadRequired(ContractPath, errors);
        string native4K = ReadRequired(Native4KPath, errors);
        ReadRequired(ConstructionContractPath, errors);
        ReadRequired(ConstructionQaPath, errors);
        ReadRequired(LookdevPath, errors);

        if (!string.IsNullOrEmpty(contract))
        {
            RequireToken(contract, "\"assetId\": \"danchi_rainwater_system_authority\"", ContractPath, errors);
            RequireToken(contract, "\"macroBody\": \"Danchi/RainGutter\"", ContractPath, errors);
            RequireToken(contract, "\"accessoryRoot\": \"Danchi/DanchiHighDetail/HD_RainwaterDownpipeAssembly\"", ContractPath, errors);
            RequireToken(contract, "\"sourcePassAwardsPoints\": false", ContractPath, errors);
            RequireToken(contract, "\"automaticPoints\": 0", ContractPath, errors);
            RequireToken(contract, "\"renderCallbackMayRepair\": false", ContractPath, errors);
        }

        foreach (string path in ForbiddenDuplicatePaths)
        {
            if (File.Exists(Absolute(path)))
                errors.Add("Forbidden duplicate rainwater source/contract still exists: " + path);
        }

        if (!string.IsNullOrEmpty(native4K))
        {
            int applyCount = CountOccurrences(native4K, RequiredApplyToken);
            int validateCount = CountOccurrences(native4K, RequiredValidateToken);
            if (applyCount != 1)
                errors.Add($"Native-4K path must apply the authoritative rainwater system exactly once; found {applyCount} occurrences.");
            if (validateCount < 3)
                errors.Add($"Native-4K path must validate the authoritative rainwater system at least three times; found {validateCount}.");
            if (native4K.Contains("QualityBlockRainwaterDownpipeUpgrade", StringComparison.Ordinal))
                errors.Add("Native-4K path references the forbidden duplicate rainwater implementation.");
            if (native4K.Contains(ForbiddenDuplicateRootName, StringComparison.Ordinal))
                errors.Add("Native-4K path references the forbidden duplicate rainwater root.");
        }

        // Preserve the mature manufacture/material/install contract as the authority.
        try
        {
            QualityBlockRainwaterDownpipeInstallationQA.ValidateContractConfigOnly();
        }
        catch (Exception ex)
        {
            errors.Add("Authoritative rainwater installation contract failed: " + ex.Message);
        }

        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Rainwater system authority contract FAILED:\n - " + string.Join("\n - ", errors));

        Debug.Log(
            "Rainwater system authority contract valid. The existing RainGutter + " +
            "HD_RainwaterDownpipeAssembly path remains the sole formal authority. " +
            "This is source/readiness QA only; Visual Fidelity remains unscored until real Unity renders exist.");
    }

    [MenuItem("NewTown/QA/Validate Rainwater System Authority In Open Scene")]
    public static void ValidateOpenSceneMenu()
    {
        ValidateOpenScene(true);
    }

    public static bool ValidateOpenScene(bool requireAuthoritativeAssembly)
    {
        var errors = new List<string>();
        try
        {
            ValidateContractConfigOnly();
        }
        catch (Exception ex)
        {
            errors.Add(ex.Message);
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath)
        {
            if (requireAuthoritativeAssembly)
                errors.Add("Formal rainwater authority validation requires the persisted QualityBlock1990s scene.");
            return Finish(errors);
        }

        GameObject danchi = FindSceneObject(DanchiName);
        if (danchi == null)
        {
            errors.Add("Danchi root is missing.");
            return Finish(errors);
        }

        GameObject[] forbiddenRoots = SceneObjectsNamed(ForbiddenDuplicateRootName);
        if (forbiddenRoots.Length > 0)
            errors.Add(
                $"Forbidden duplicate rainwater root '{ForbiddenDuplicateRootName}' exists {forbiddenRoots.Length} time(s). " +
                "Rebuild from the authoritative system before formal evidence capture.");

        Transform[] pipes = danchi.GetComponentsInChildren<Transform>(true)
            .Where(x => string.Equals(x.name, PipeName, StringComparison.Ordinal))
            .ToArray();
        int activePipeRenderers = pipes
            .SelectMany(x => x.GetComponents<Renderer>())
            .Count(x => x != null && x.enabled && x.gameObject.activeInHierarchy);

        if (requireAuthoritativeAssembly && pipes.Length != 1)
            errors.Add($"Expected exactly one authoritative {PipeName} object; found {pipes.Length}.");
        if (requireAuthoritativeAssembly && activePipeRenderers != 1)
            errors.Add($"Expected exactly one active authoritative {PipeName} renderer; found {activePipeRenderers}.");

        if (pipes.Length == 1)
        {
            MeshFilter filter = pipes[0].GetComponent<MeshFilter>();
            if (requireAuthoritativeAssembly && (filter == null || filter.sharedMesh == null))
                errors.Add("Authoritative RainGutter is missing its authored mesh.");
            else if (filter?.sharedMesh != null)
            {
                string meshName = filter.sharedMesh.name ?? string.Empty;
                if (meshName == "Cylinder" || meshName == "Cube" || meshName == "Sphere" ||
                    meshName == "Capsule" || meshName == "Plane" || meshName == "Quad")
                    errors.Add("Authoritative RainGutter still exposes a Unity primitive mesh: " + meshName);
                if (requireAuthoritativeAssembly && !meshName.StartsWith("GM_HD_", StringComparison.Ordinal))
                    errors.Add("Authoritative RainGutter is not bound to the generated high-detail mesh library: " + meshName);
            }
        }

        Transform detailRoot = FindDirectChild(danchi.transform, DetailRootName);
        if (requireAuthoritativeAssembly && detailRoot == null)
            errors.Add("DanchiHighDetail root is missing.");

        GameObject[] accessoryRoots = SceneObjectsNamed(AccessoryRootName);
        if (requireAuthoritativeAssembly && accessoryRoots.Length != 1)
            errors.Add($"Expected exactly one authoritative {AccessoryRootName}; found {accessoryRoots.Length}.");
        if (accessoryRoots.Length == 1)
        {
            if (detailRoot == null || !accessoryRoots[0].transform.IsChildOf(detailRoot))
                errors.Add("Authoritative rainwater accessory root is outside DanchiHighDetail.");
            if (requireAuthoritativeAssembly && !accessoryRoots[0].activeInHierarchy)
                errors.Add("Authoritative rainwater accessory root is inactive.");
            if (requireAuthoritativeAssembly && !accessoryRoots[0].GetComponentsInChildren<Renderer>(true)
                    .Any(x => x != null && x.enabled && x.gameObject.activeInHierarchy))
                errors.Add("Authoritative rainwater accessory root contains no active rendered construction.");
        }

        return Finish(errors);
    }

    private static void OnCameraPreCull(Camera camera)
    {
        if (camera == null) return;
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath) return;

        bool namedFormal = IsNamedFormalCamera(camera);
        bool reflection = camera.cameraType == CameraType.Reflection;
        if (!namedFormal && !reflection) return;

        // Editor reflection probes can render while a scene is still being rebuilt. Do not mutate or
        // reject that pre-authority transient. Once the authoritative accessory root exists, however,
        // reflection and every formal still/temporal cull must see the same single-system authority.
        bool authorityExists = SceneObjectsNamed(AccessoryRootName).Length == 1;
        if (reflection && !authorityExists) return;

        ValidateOpenScene(true);
    }

    private static bool IsNamedFormalCamera(Camera camera)
    {
        string n = camera.name ?? string.Empty;
        return n.StartsWith("QA4K_", StringComparison.Ordinal) ||
               n.StartsWith("QATemporal_", StringComparison.Ordinal) ||
               n.StartsWith("QAPreparedTemporal_", StringComparison.Ordinal);
    }

    private static bool Finish(List<string> errors)
    {
        if (errors.Count == 0) return true;
        throw new InvalidOperationException(
            "Rainwater system authority FAILED (fail-closed before formal evidence):\n - " +
            string.Join("\n - ", errors));
    }

    private static string ReadRequired(string assetPath, List<string> errors)
    {
        string path = Absolute(assetPath);
        if (!File.Exists(path))
        {
            errors.Add("Required rainwater authority file is missing: " + assetPath);
            return string.Empty;
        }
        return File.ReadAllText(path);
    }

    private static void RequireToken(string source, string token, string path, List<string> errors)
    {
        if (!source.Contains(token, StringComparison.Ordinal))
            errors.Add($"{path} is missing required authority token: {token}");
    }

    private static int CountOccurrences(string source, string token)
    {
        int count = 0;
        int offset = 0;
        while (offset <= source.Length - token.Length)
        {
            int index = source.IndexOf(token, offset, StringComparison.Ordinal);
            if (index < 0) break;
            count++;
            offset = index + token.Length;
        }
        return count;
    }

    private static string Absolute(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrWhiteSpace(projectRoot))
            throw new InvalidOperationException("Could not resolve Unity project root.");
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x != null && x.scene.IsValid() && x.scene.path == ScenePath && x.name == name);
    }

    private static GameObject[] SceneObjectsNamed(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .Where(x => x != null && x.scene.IsValid() && x.scene.path == ScenePath &&
                        string.Equals(x.name, name, StringComparison.Ordinal))
            .OrderBy(x => HierarchyPath(x.transform), StringComparer.Ordinal)
            .ToArray();
    }

    private static Transform FindDirectChild(Transform parent, string name)
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (string.Equals(child.name, name, StringComparison.Ordinal)) return child;
        }
        return null;
    }

    private static string HierarchyPath(Transform t)
    {
        if (t == null) return "<null>";
        var parts = new Stack<string>();
        Transform cursor = t;
        while (cursor != null)
        {
            parts.Push(cursor.name);
            cursor = cursor.parent;
        }
        return string.Join("/", parts);
    }
}
