using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Closes the formal-evidence gap between the physical grass-blade generator and the native-4K packet.
/// The grass generator's legacy scene-open convenience path is intentionally non-persistent; this
/// companion makes the canonical benchmark reopen deterministic by rebuilding/saving the grass layer
/// before control returns to the formal capture pipeline. Formal camera callbacks remain read-only and
/// require the current grass render state to match the latest scene-saved fingerprint.
///
/// This is provenance/readiness QA only. It cannot award Visual Fidelity points or claim render quality.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockGrassBladeFormalPersistenceQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/grass_blade_formal_persistence_contract.json";
    private const string SourceGrassContractPath = "Assets/QA/grass_blade_field_contract.json";
    private const string SourceMaterialPath = "Assets/Art/GeneratedPBR/PBR_GrassWorn.mat";
    private const string RootPath = "QualityBlock1990s/Ground/GrassBladeFieldDetail";
    private const string GeneratorMarker = "Generator_grass-blade-field-v1.0.1";
    private const string MainCameraName = "QualityCamera";

    private static bool binding;
    private static string latestSavedGrassFingerprint = string.Empty;

    static QualityBlockGrassBladeFormalPersistenceQA()
    {
        EditorSceneManager.sceneOpened -= OnSceneOpened;
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorSceneManager.sceneSaved -= OnSceneSaved;
        EditorSceneManager.sceneSaved += OnSceneSaved;
        Camera.onPreCull -= OnCameraPreCull;
        Camera.onPreCull += OnCameraPreCull;
        EditorApplication.delayCall += EnsureCurrentSceneIfReady;
    }

    [MenuItem("NewTown/QA/Validate Grass Blade Persisted Evidence Binding")]
    public static void ValidatePersistedEvidenceBinding()
    {
        ValidateContractConfigOnly();
        EnsureBenchmarkSceneOpen();
        EnsureCurrentSceneIfReady();
        ValidateFormalGrassStateAgainstLatestSave();
        Debug.Log(
            "Grass blade persisted-evidence binding valid: current grass render state matches the latest saved canonical-scene fingerprint. Native 4K appearance remains UNSCORED.");
    }

    [MenuItem("NewTown/QA/Validate Grass Blade Persistence Contract")]
    public static void ValidateContractConfigOnly()
    {
        if (!File.Exists(ContractPath))
            throw new FileNotFoundException("Grass formal-persistence contract missing: " + ContractPath);
        if (!File.Exists(SourceGrassContractPath))
            throw new FileNotFoundException("Grass source contract missing: " + SourceGrassContractPath);

        Contract contract = JsonUtility.FromJson<Contract>(File.ReadAllText(ContractPath));
        if (contract == null || !string.Equals(contract.schemaVersion, "1.0.0", StringComparison.Ordinal))
            throw new InvalidOperationException("Grass formal-persistence contract is null/unparseable or not schema 1.0.0.");
        if (!string.Equals(contract.scenePath, ScenePath, StringComparison.Ordinal) ||
            !string.Equals(contract.sourceGrassContractPath, SourceGrassContractPath, StringComparison.Ordinal) ||
            !string.Equals(contract.rootPath, RootPath, StringComparison.Ordinal) ||
            !string.Equals(contract.generatorMarker, GeneratorMarker, StringComparison.Ordinal))
            throw new InvalidOperationException("Grass formal-persistence contract identity/path binding drifted.");
        if (contract.formalPipeline == null ||
            !contract.formalPipeline.sceneOpenMustPersist ||
            !contract.formalPipeline.sceneSavedBaselineRequired ||
            !contract.formalPipeline.formalCullMustMatchLatestSavedGrassFingerprint ||
            !contract.formalPipeline.renderCallbacksReadOnly)
            throw new InvalidOperationException("Grass formal-persistence fail-closed pipeline requirements were weakened.");
        if (contract.renderVerification == null ||
            contract.renderVerification.runtimeRenderVerified ||
            contract.renderVerification.visualFidelityPointsAwarded != 0)
            throw new InvalidOperationException("Grass persistence QA may not claim a runtime render or award Visual Fidelity points.");

        string[] cameraClasses = contract.formalCameraClasses ?? Array.Empty<string>();
        string[] required = { MainCameraName, "Reflection", "QA4K_*", "QATemporal_*", "QAPreparedTemporal_*" };
        foreach (string cameraClass in required)
            if (Array.IndexOf(cameraClasses, cameraClass) < 0)
                throw new InvalidOperationException("Grass formal-persistence contract lost formal camera class: " + cameraClass);
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (binding || EditorApplication.isPlayingOrWillChangePlaymode ||
            !scene.IsValid() || !string.Equals(scene.path, ScenePath, StringComparison.Ordinal))
            return;

        latestSavedGrassFingerprint = string.Empty;

        // Source assets are established by the deterministic benchmark/PBR build. If a developer opens
        // the scene before that source exists, do not manufacture partial state here; formal pre-cull
        // remains fail-closed because no saved fingerprint baseline will exist.
        if (!File.Exists(ContractPath) || !File.Exists(SourceGrassContractPath) ||
            AssetDatabase.LoadAssetAtPath<Material>(SourceMaterialPath) == null)
            return;

        binding = true;
        try
        {
            ValidateContractConfigOnly();
            // Deliberately persistent. This is the key distinction from the generator's convenience
            // auto-apply path, which calls ApplyToOpenScene(false).
            QualityBlockGrassBladeFieldUpgrade.ApplyToOpenScene(true);
            QualityBlockGrassBladeFieldUpgrade.ValidateOpenScene(false);
            RecordLatestSavedGrassFingerprint(EditorSceneManager.GetActiveScene());
        }
        catch (Exception ex)
        {
            latestSavedGrassFingerprint = string.Empty;
            Debug.LogError("Grass persisted-evidence scene-open binding failed: " + ex);
            throw;
        }
        finally
        {
            binding = false;
        }
    }

    private static void OnSceneSaved(Scene scene)
    {
        if (!scene.IsValid() || !string.Equals(scene.path, ScenePath, StringComparison.Ordinal))
            return;

        try
        {
            if (!File.Exists(ContractPath) || !File.Exists(SourceGrassContractPath) ||
                AssetDatabase.LoadAssetAtPath<Material>(SourceMaterialPath) == null)
            {
                latestSavedGrassFingerprint = string.Empty;
                return;
            }

            QualityBlockGrassBladeFieldUpgrade.ValidateOpenScene(false);
            RecordLatestSavedGrassFingerprint(scene);
        }
        catch (Exception ex)
        {
            // Do not repair from a save callback. Clearing the baseline guarantees that any later
            // formal Camera.Render fails closed at pre-cull.
            latestSavedGrassFingerprint = string.Empty;
            Debug.LogError("Saved benchmark scene did not establish a valid grass fingerprint baseline: " + ex);
        }
    }

    private static void EnsureCurrentSceneIfReady()
    {
        if (binding || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !string.Equals(scene.path, ScenePath, StringComparison.Ordinal))
            return;
        if (!File.Exists(ContractPath) || !File.Exists(SourceGrassContractPath) ||
            AssetDatabase.LoadAssetAtPath<Material>(SourceMaterialPath) == null)
            return;

        try
        {
            GameObject root = GameObject.Find(RootPath);
            if (root == null || root.transform.Find(GeneratorMarker) == null)
            {
                // This is an editor setup path, not a render callback, so deterministic persistence is
                // allowed. It also covers domain reload into an already-open canonical scene.
                binding = true;
                QualityBlockGrassBladeFieldUpgrade.ApplyToOpenScene(true);
                QualityBlockGrassBladeFieldUpgrade.ValidateOpenScene(false);
                RecordLatestSavedGrassFingerprint(EditorSceneManager.GetActiveScene());
                return;
            }

            QualityBlockGrassBladeFieldUpgrade.ValidateOpenScene(false);
            // Only establish a baseline from an actually saved scene. If unrelated editor work has made
            // the scene dirty, wait for sceneSaved; do not silently persist the user's broader changes.
            if (!scene.isDirty)
                RecordLatestSavedGrassFingerprint(scene);
        }
        catch (Exception ex)
        {
            latestSavedGrassFingerprint = string.Empty;
            Debug.LogError("Grass persisted-evidence delayed initialization failed: " + ex);
        }
        finally
        {
            binding = false;
        }
    }

    private static void OnCameraPreCull(Camera camera)
    {
        if (camera == null || !IsFormalCamera(camera))
            return;

        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !string.Equals(scene.path, ScenePath, StringComparison.Ordinal))
            return;
        if (binding)
            throw new InvalidOperationException("Formal grass evidence render attempted while the persisted binding was mutating source state.");

        // Strictly read-only. Camera movement during hero/oblique/grazing capture may make unrelated
        // scene state dirty, so compare only the grass render fingerprint to the last scene-saved baseline.
        ValidateFormalGrassStateAgainstLatestSave();
    }

    private static void ValidateFormalGrassStateAgainstLatestSave()
    {
        ValidateContractConfigOnly();
        QualityBlockGrassBladeFieldUpgrade.ValidateOpenScene(false);

        if (string.IsNullOrWhiteSpace(latestSavedGrassFingerprint))
            throw new InvalidOperationException(
                "Grass formal render has no scene-saved fingerprint baseline. Reopen/prepare the canonical benchmark scene before evidence capture.");

        string current = ComputeGrassFingerprint();
        if (!string.Equals(current, latestSavedGrassFingerprint, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Grass render state differs from the latest persisted canonical-scene baseline. Save through the deterministic grass build before formal evidence capture; render-time repair is forbidden.");
    }

    private static void RecordLatestSavedGrassFingerprint(Scene scene)
    {
        if (!scene.IsValid() || !string.Equals(scene.path, ScenePath, StringComparison.Ordinal))
            throw new InvalidOperationException("Grass persistence baseline may only be recorded for the canonical benchmark scene.");
        if (!File.Exists(ScenePath))
            throw new FileNotFoundException("Canonical benchmark scene file is missing after grass persistence: " + ScenePath);

        latestSavedGrassFingerprint = ComputeGrassFingerprint();
        if (string.IsNullOrWhiteSpace(latestSavedGrassFingerprint))
            throw new InvalidOperationException("Grass persistence fingerprint unexpectedly resolved to an empty digest.");
    }

    private static string ComputeGrassFingerprint()
    {
        GameObject rootObject = GameObject.Find(RootPath);
        if (rootObject == null)
            throw new InvalidOperationException("GrassBladeFieldDetail is missing while computing the persisted evidence fingerprint.");
        Transform root = rootObject.transform;
        if (root.Find(GeneratorMarker) == null)
            throw new InvalidOperationException("Grass generator marker is missing while computing the persisted evidence fingerprint.");

        var sb = new StringBuilder(32768);
        sb.Append("grass-persisted-render-fingerprint-v1\n");

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true)
                     .OrderBy(x => RelativePath(root, x), StringComparer.Ordinal))
        {
            sb.Append("T|").Append(RelativePath(root, t)).Append('|').Append(t.gameObject.activeSelf ? '1' : '0').Append('|');
            AppendVector(sb, t.localPosition);
            sb.Append('|');
            AppendQuaternion(sb, t.localRotation);
            sb.Append('|');
            AppendVector(sb, t.localScale);
            sb.Append('\n');
        }

        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true)
                     .OrderBy(x => RelativePath(root, x.transform), StringComparer.Ordinal))
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null)
                throw new InvalidOperationException("Grass fingerprint encountered a MeshFilter with no shared mesh: " + RelativePath(root, filter.transform));
            string path = AssetDatabase.GetAssetPath(mesh) ?? string.Empty;
            sb.Append("M|").Append(RelativePath(root, filter.transform)).Append('|')
                .Append(path).Append('|').Append(DependencyHash(path)).Append('|')
                .Append(mesh.name).Append('|').Append(mesh.vertexCount).Append('|').Append(mesh.subMeshCount).Append('|');
            AppendVector(sb, mesh.bounds.center);
            sb.Append('|');
            AppendVector(sb, mesh.bounds.size);
            sb.Append('\n');
        }

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true)
                     .OrderBy(x => RelativePath(root, x.transform), StringComparer.Ordinal))
        {
            sb.Append("R|").Append(RelativePath(root, renderer.transform)).Append('|')
                .Append(renderer.enabled ? '1' : '0').Append('|')
                .Append((int)renderer.shadowCastingMode).Append('|')
                .Append(renderer.receiveShadows ? '1' : '0').Append('|')
                .Append((int)renderer.lightProbeUsage).Append('|')
                .Append((int)renderer.reflectionProbeUsage).Append('\n');

            Material[] materials = renderer.sharedMaterials ?? Array.Empty<Material>();
            for (int i = 0; i < materials.Length; i++)
                AppendMaterial(sb, RelativePath(root, renderer.transform), i, materials[i]);
        }

        foreach (LODGroup group in root.GetComponentsInChildren<LODGroup>(true)
                     .OrderBy(x => RelativePath(root, x.transform), StringComparer.Ordinal))
        {
            sb.Append("L|").Append(RelativePath(root, group.transform)).Append('|')
                .Append((int)group.fadeMode).Append('|')
                .Append(group.animateCrossFading ? '1' : '0').Append('|')
                .Append(F(group.size)).Append('|');
            AppendVector(sb, group.localReferencePoint);
            sb.Append('\n');

            LOD[] lods = group.GetLODs();
            for (int lod = 0; lod < lods.Length; lod++)
            {
                sb.Append("LD|").Append(lod).Append('|')
                    .Append(F(lods[lod].screenRelativeTransitionHeight)).Append('|')
                    .Append(F(lods[lod].fadeTransitionWidth));
                Renderer[] renderers = lods[lod].renderers ?? Array.Empty<Renderer>();
                foreach (Renderer lodRenderer in renderers)
                    sb.Append('|').Append(lodRenderer == null ? "<null>" : RelativePath(root, lodRenderer.transform));
                sb.Append('\n');
            }
        }

        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
        return BitConverter.ToString(digest).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static void AppendMaterial(StringBuilder sb, string rendererPath, int slot, Material material)
    {
        if (material == null)
        {
            sb.Append("MAT|").Append(rendererPath).Append('|').Append(slot).Append("|<null>\n");
            return;
        }

        string path = AssetDatabase.GetAssetPath(material) ?? string.Empty;
        sb.Append("MAT|").Append(rendererPath).Append('|').Append(slot).Append('|')
            .Append(path).Append('|').Append(DependencyHash(path)).Append('|')
            .Append(material.shader != null ? material.shader.name : "<no-shader>");

        AppendFloatProperty(sb, material, "_Metallic");
        AppendFloatProperty(sb, material, "_Glossiness");
        AppendFloatProperty(sb, material, "_GlossMapScale");
        AppendFloatProperty(sb, material, "_BumpScale");
        if (material.HasProperty("_Color"))
        {
            Color c = material.GetColor("_Color");
            sb.Append("|_Color=").Append(F(c.r)).Append(',').Append(F(c.g)).Append(',').Append(F(c.b)).Append(',').Append(F(c.a));
        }

        string[] textureProperties = { "_MainTex", "_BumpMap", "_MetallicGlossMap" };
        foreach (string property in textureProperties)
        {
            if (!material.HasProperty(property))
                continue;
            Texture texture = material.GetTexture(property);
            string texturePath = texture != null ? AssetDatabase.GetAssetPath(texture) ?? string.Empty : string.Empty;
            sb.Append('|').Append(property).Append('=').Append(texturePath).Append('@').Append(DependencyHash(texturePath));
        }
        sb.Append('\n');
    }

    private static void AppendFloatProperty(StringBuilder sb, Material material, string property)
    {
        if (material.HasProperty(property))
            sb.Append('|').Append(property).Append('=').Append(F(material.GetFloat(property)));
    }

    private static string DependencyHash(string assetPath)
    {
        return string.IsNullOrWhiteSpace(assetPath) ? "<runtime>" : AssetDatabase.GetAssetDependencyHash(assetPath).ToString();
    }

    private static string RelativePath(Transform root, Transform target)
    {
        if (target == root)
            return root.name;

        var names = new System.Collections.Generic.List<string>();
        Transform current = target;
        while (current != null && current != root)
        {
            names.Add(current.name);
            current = current.parent;
        }
        if (current != root)
            return "<outside>/" + target.name;
        names.Reverse();
        return root.name + "/" + string.Join("/", names);
    }

    private static void AppendVector(StringBuilder sb, Vector3 value)
    {
        sb.Append(F(value.x)).Append(',').Append(F(value.y)).Append(',').Append(F(value.z));
    }

    private static void AppendQuaternion(StringBuilder sb, Quaternion value)
    {
        sb.Append(F(value.x)).Append(',').Append(F(value.y)).Append(',').Append(F(value.z)).Append(',').Append(F(value.w));
    }

    private static string F(float value)
    {
        return value.ToString("R", CultureInfo.InvariantCulture);
    }

    private static bool IsFormalCamera(Camera camera)
    {
        if (camera.cameraType == CameraType.Reflection)
            return true;
        string name = camera.name ?? string.Empty;
        return string.Equals(name, MainCameraName, StringComparison.Ordinal) ||
               name.StartsWith("QA4K_", StringComparison.Ordinal) ||
               name.StartsWith("QATemporal_", StringComparison.Ordinal) ||
               name.StartsWith("QAPreparedTemporal_", StringComparison.Ordinal);
    }

    private static void EnsureBenchmarkSceneOpen()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !string.Equals(scene.path, ScenePath, StringComparison.Ordinal))
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    [Serializable]
    private sealed class Contract
    {
        public string schemaVersion;
        public string scenePath;
        public string sourceGrassContractPath;
        public string rootPath;
        public string generatorMarker;
        public FormalPipeline formalPipeline;
        public string[] formalCameraClasses;
        public RenderVerification renderVerification;
    }

    [Serializable]
    private sealed class FormalPipeline
    {
        public bool sceneOpenMustPersist;
        public bool sceneSavedBaselineRequired;
        public bool formalCullMustMatchLatestSavedGrassFingerprint;
        public bool renderCallbacksReadOnly;
    }

    [Serializable]
    private sealed class RenderVerification
    {
        public bool runtimeRenderVerified;
        public int visualFidelityPointsAwarded;
    }
}
