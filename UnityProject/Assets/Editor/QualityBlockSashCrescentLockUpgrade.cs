using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Adds period-plausible room-side crescent locks to the generated apartment sliding sash.
/// The baseline facade already reconstructs glazing, sash depth and gaskets, but the meeting stile
/// still lacks the ordinary mechanical lock that gives a close window crop manufactured context.
///
/// This pass is deliberately construction-first: a coated metal mounting plate, two fixing screws on
/// a verified 50 mm pitch, pivot boss, curved multi-segment lever and opposing keeper are separate
/// geometry. The hardware remains behind the exterior glazing and uses four LOD levels. No visual
/// points are awarded until native 3840x2160 evidence proves the result.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockSashCrescentLockUpgrade
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string RootName = "SashCrescentLocks";
    private const string MaterialPath = "Assets/Art/GeneratedDetailMaterials/MAT_SashLockBronzeCoated.mat";
    private const string ContractPath = "Assets/QA/sash_crescent_lock_contract.json";
    private const int ExpectedWindowCount = 30;

    private const float ScrewPitch = 0.050f;
    private const float PlateWidth = 0.018f;
    private const float PlateHeight = 0.072f;
    private const float PlateDepth = 0.006f;
    private const float ScrewDiameter = 0.009f;
    private const float PivotDiameter = 0.012f;
    private const float KeeperWidth = 0.012f;
    private const float KeeperHeight = 0.038f;
    private const float KeeperDepth = 0.008f;
    private const float LeverRadius = 0.028f;

    private const float Lod0Transition = 0.030f;
    private const float Lod1Transition = 0.012f;
    private const float Lod2Transition = 0.004f;
    private const float Lod3Cull = 0.001f;

    private static bool validating;
    private static int lastValidatedFrame = -1;

    static QualityBlockSashCrescentLockUpgrade()
    {
        EditorSceneManager.sceneSaving -= OnSceneSaving;
        EditorSceneManager.sceneSaving += OnSceneSaving;
        Camera.onPreCull -= OnCameraPreCull;
        Camera.onPreCull += OnCameraPreCull;
    }

    [MenuItem("NewTown/Geometry/Rebuild Sash Crescent Locks")]
    public static void RebuildForOpenScene()
    {
        EnsureSceneOpen();
        ValidateContractConfigOnly();

        if (IsAuthoredDanchiActive())
        {
            RemoveGeneratedRootIfPresent();
            Debug.Log("Authored danchi replacement is active; generated sash crescent locks were skipped.");
            return;
        }

        Material material = GetOrCreateMaterial();
        RemoveGeneratedRootIfPresent();
        Build(material);
        ValidateOpenSceneCore();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "Sash crescent locks rebuilt for 30 apartment windows with discrete plate/screws/pivot/lever/keeper and LOD0/1/2/3. " +
            "Visual Fidelity remains UNSCORED pending native 4K render evidence.");
    }

    [MenuItem("NewTown/QA/Validate Sash Crescent Locks")]
    public static void ValidateOpenScene()
    {
        EnsureSceneOpen();
        ValidateOpenSceneCore();
    }

    [MenuItem("NewTown/QA/Validate Sash Crescent Lock Contract")]
    public static void ValidateContractConfigOnly()
    {
        string absolute = AbsolutePath(ContractPath);
        if (!File.Exists(absolute))
            throw new FileNotFoundException("Sash crescent lock contract is missing: " + ContractPath);

        SashLockContract contract = JsonUtility.FromJson<SashLockContract>(File.ReadAllText(absolute));
        if (contract == null || contract.geometry == null || contract.material == null || contract.lod == null || contract.qa == null)
            throw new InvalidOperationException("Sash crescent lock contract is null or incomplete.");

        var errors = new List<string>();
        Require(string.Equals(contract.schemaVersion, "1.0.0", StringComparison.Ordinal), "schemaVersion must remain 1.0.0", errors);
        Require(string.Equals(contract.assemblyId, "apartment_sliding_sash_crescent_lock", StringComparison.Ordinal), "assemblyId mismatch", errors);
        Require(contract.expectedWindowCount == ExpectedWindowCount, "expectedWindowCount must remain 30", errors);
        RequireNear(contract.geometry.mountingScrewPitchM, ScrewPitch, 0.0001f, "mountingScrewPitchM", errors);
        RequireNear(contract.geometry.plateWidthM, PlateWidth, 0.0001f, "plateWidthM", errors);
        RequireNear(contract.geometry.plateHeightM, PlateHeight, 0.0001f, "plateHeightM", errors);
        RequireNear(contract.geometry.plateDepthM, PlateDepth, 0.0001f, "plateDepthM", errors);
        RequireNear(contract.geometry.screwHeadDiameterM, ScrewDiameter, 0.0001f, "screwHeadDiameterM", errors);
        RequireNear(contract.geometry.pivotDiameterM, PivotDiameter, 0.0001f, "pivotDiameterM", errors);
        RequireNear(contract.geometry.keeperWidthM, KeeperWidth, 0.0001f, "keeperWidthM", errors);
        RequireNear(contract.geometry.keeperHeightM, KeeperHeight, 0.0001f, "keeperHeightM", errors);
        RequireNear(contract.geometry.keeperDepthM, KeeperDepth, 0.0001f, "keeperDepthM", errors);
        RequireNear(contract.geometry.leverArcRadiusM, LeverRadius, 0.0001f, "leverArcRadiusM", errors);
        Require(contract.geometry.leverSegmentCountLod0 == 5, "LOD0 lever must retain five physical segments", errors);
        Require(string.Equals(contract.material.assetPath, MaterialPath, StringComparison.Ordinal), "material asset path mismatch", errors);
        Require(contract.material.metallicMin == 0f && contract.material.metallicMax == 0f, "visible lock coating must remain dielectric", errors);
        Require(contract.material.roughnessMin >= 0.50f && contract.material.roughnessMax <= 0.70f,
            "lock coating roughness range drifted from restrained low-satin response", errors);
        RequireNear(contract.material.specularF0, 0.04f, 0.005f, "specularF0", errors);
        Require(contract.material.wetness == 0f, "interior benchmark lock must remain dry", errors);
        RequireNear(contract.lod.lod0Transition, Lod0Transition, 0.0001f, "lod0Transition", errors);
        RequireNear(contract.lod.lod1Transition, Lod1Transition, 0.0001f, "lod1Transition", errors);
        RequireNear(contract.lod.lod2Transition, Lod2Transition, 0.0001f, "lod2Transition", errors);
        RequireNear(contract.lod.lod3Cull, Lod3Cull, 0.0001f, "lod3Cull", errors);
        Require(contract.lod.animateCrossFading, "animated LOD cross-fade must remain enabled", errors);
        Require(contract.qa.lod0RendererCountPerWindow == 10, "LOD0 renderer count contract mismatch", errors);
        Require(contract.qa.lod1RendererCountPerWindow == 5, "LOD1 renderer count contract mismatch", errors);
        Require(contract.qa.lod2RendererCountPerWindow == 2, "LOD2 renderer count contract mismatch", errors);
        Require(contract.qa.lod3RendererCountPerWindow == 1, "LOD3 renderer count contract mismatch", errors);
        Require(contract.qa.requireNoColliders && contract.qa.requireNoStockPrimitiveMeshes && contract.qa.requireFormalPreCullValidation,
            "QA fail-closed requirements may not be disabled", errors);
        Require(contract.qa.automaticVisualPoints == 0, "source contract must never award Visual Fidelity points", errors);
        Require(contract.qa.renderVerificationPending, "render verification must remain pending until actual evidence exists", errors);

        foreach (string value in new[]
        {
            contract.manufacture, contract.dimensionsThickness, contract.materialsFinish, contract.mounting,
            contract.interfacesGapsSeals, contract.orientationExposure, contract.aging,
            contract.geometryVsMaterial, contract.lodPolicy, contract.sourceBasis, contract.lookdevGenerationBrief
        })
            Require(!string.IsNullOrWhiteSpace(value), "mandatory manufacture/material reasoning field is empty", errors);

        if (errors.Count > 0)
            throw new InvalidOperationException("Sash crescent lock contract FAILED:\n - " + string.Join("\n - ", errors));
    }

    private static void OnSceneSaving(Scene scene, string path)
    {
        if (!scene.IsValid() || !string.Equals(path, ScenePath, StringComparison.Ordinal))
            return;

        if (IsAuthoredDanchiActive())
        {
            RemoveGeneratedRootIfPresent();
            return;
        }

        ValidateContractConfigOnly();
        GameObject root = FindSceneObject(RootName);
        if (root == null)
            Build(GetOrCreateMaterial());
        else
            ValidateOpenSceneCore();
    }

    private static void OnCameraPreCull(Camera camera)
    {
        if (camera == null || !camera.gameObject.scene.IsValid() ||
            !string.Equals(camera.gameObject.scene.path, ScenePath, StringComparison.Ordinal))
            return;

        bool formal = camera.name.StartsWith("QA4K_", StringComparison.Ordinal) ||
                      camera.name.StartsWith("QATemporal_", StringComparison.Ordinal) ||
                      camera.name.StartsWith("QAPreparedTemporal_", StringComparison.Ordinal) ||
                      camera.cameraType == CameraType.Reflection;
        if (!formal || IsAuthoredDanchiActive() || lastValidatedFrame == Time.frameCount)
            return;

        lastValidatedFrame = Time.frameCount;
        ValidateOpenSceneCore();
    }

    private static void Build(Material material)
    {
        GameObject danchi = FindSceneObject("Danchi");
        if (danchi == null)
            throw new InvalidOperationException("Danchi fallback root is missing; cannot install sash locks.");

        var root = new GameObject(RootName);
        root.transform.SetParent(danchi.transform, false);

        int created = 0;
        for (int floor = 0; floor < 5; floor++)
        {
            for (int bay = 0; bay < 6; bay++)
            {
                GameObject baseWindow = FindSceneObject($"Window_{floor}_{bay}");
                if (baseWindow == null)
                    throw new InvalidOperationException($"Base apartment window missing: Window_{floor}_{bay}");

                BuildWindowLock(root.transform, floor, bay, baseWindow.transform.localPosition, material);
                created++;
            }
        }

        if (created != ExpectedWindowCount)
            throw new InvalidOperationException($"Created {created} sash locks; expected {ExpectedWindowCount}.");
    }

    private static void BuildWindowLock(Transform parent, int floor, int bay, Vector3 windowLocalPosition, Material material)
    {
        var lockRoot = new GameObject($"HD_SashCrescentLock_{floor}_{bay}");
        lockRoot.transform.SetParent(parent, false);
        lockRoot.transform.localPosition = windowLocalPosition;

        Renderer[] lod0 = BuildLod0(lockRoot.transform, material);
        Renderer[] lod1 = BuildLod1(lockRoot.transform, material);
        Renderer[] lod2 = BuildLod2(lockRoot.transform, material);
        Renderer[] lod3 = BuildLod3(lockRoot.transform, material);

        var group = lockRoot.AddComponent<LODGroup>();
        group.fadeMode = LODFadeMode.CrossFade;
        group.animateCrossFading = true;
        group.SetLODs(new[]
        {
            new LOD(Lod0Transition, lod0),
            new LOD(Lod1Transition, lod1),
            new LOD(Lod2Transition, lod2),
            new LOD(Lod3Cull, lod3)
        });
        group.RecalculateBounds();
    }

    private static Renderer[] BuildLod0(Transform lockRoot, Material material)
    {
        Transform root = NewChild(lockRoot, "Lock_LOD0");
        var renderers = new List<Renderer>(10);
        const float plateX = 0.022f;

        renderers.Add(AddBox("Plate", root, new Vector3(plateX, 0f, 0f), new Vector3(PlateWidth, PlateHeight, PlateDepth), Quaternion.identity, material));
        renderers.Add(AddCylinder("ScrewTop", root, new Vector3(plateX, ScrewPitch * 0.5f, 0.004f), ScrewDiameter, 0.003f, material));
        renderers.Add(AddCylinder("ScrewBottom", root, new Vector3(plateX, -ScrewPitch * 0.5f, 0.004f), ScrewDiameter, 0.003f, material));
        renderers.Add(AddCylinder("Pivot", root, new Vector3(plateX, 0f, 0.005f), PivotDiameter, 0.005f, material));
        renderers.Add(AddBox("Keeper", root, new Vector3(-0.014f, 0f, -0.001f), new Vector3(KeeperWidth, KeeperHeight, KeeperDepth), Quaternion.identity, material));

        float[] angles = { 115f, 145f, 175f, 205f, 235f };
        for (int i = 0; i < angles.Length; i++)
        {
            float rad = angles[i] * Mathf.Deg2Rad;
            Vector3 p = new Vector3(
                plateX + Mathf.Cos(rad) * LeverRadius,
                Mathf.Sin(rad) * LeverRadius,
                0.008f);
            renderers.Add(AddBox($"Lever_{i}", root, p, new Vector3(0.008f, 0.020f, 0.006f),
                Quaternion.Euler(0f, 0f, angles[i]), material));
        }
        return renderers.ToArray();
    }

    private static Renderer[] BuildLod1(Transform lockRoot, Material material)
    {
        Transform root = NewChild(lockRoot, "Lock_LOD1");
        var renderers = new List<Renderer>(5);
        const float plateX = 0.022f;
        renderers.Add(AddBox("Plate", root, new Vector3(plateX, 0f, 0f), new Vector3(PlateWidth, PlateHeight, PlateDepth), Quaternion.identity, material));
        renderers.Add(AddBox("Keeper", root, new Vector3(-0.014f, 0f, -0.001f), new Vector3(KeeperWidth, KeeperHeight, KeeperDepth), Quaternion.identity, material));
        float[] angles = { 130f, 175f, 220f };
        for (int i = 0; i < angles.Length; i++)
        {
            float rad = angles[i] * Mathf.Deg2Rad;
            Vector3 p = new Vector3(plateX + Mathf.Cos(rad) * LeverRadius, Mathf.Sin(rad) * LeverRadius, 0.007f);
            renderers.Add(AddBox($"Lever_{i}", root, p, new Vector3(0.010f, 0.027f, 0.006f),
                Quaternion.Euler(0f, 0f, angles[i]), material));
        }
        return renderers.ToArray();
    }

    private static Renderer[] BuildLod2(Transform lockRoot, Material material)
    {
        Transform root = NewChild(lockRoot, "Lock_LOD2");
        return new[]
        {
            AddBox("Plate", root, new Vector3(0.022f, 0f, 0f), new Vector3(0.018f, 0.068f, 0.005f), Quaternion.identity, material),
            AddBox("LeverProxy", root, new Vector3(0.001f, 0f, 0.005f), new Vector3(0.036f, 0.010f, 0.005f), Quaternion.Euler(0f, 0f, 10f), material)
        };
    }

    private static Renderer[] BuildLod3(Transform lockRoot, Material material)
    {
        Transform root = NewChild(lockRoot, "Lock_LOD3");
        return new[]
        {
            AddBox("LockValueProxy", root, new Vector3(0.010f, 0f, 0f), new Vector3(0.045f, 0.052f, 0.004f), Quaternion.identity, material)
        };
    }

    private static Material GetOrCreateMaterial()
    {
        Directory.CreateDirectory("Assets/Art/GeneratedDetailMaterials");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        Shader shader = Shader.Find("Standard");
        if (shader == null)
            throw new InvalidOperationException("Standard shader is unavailable for sash-lock material.");

        if (material == null)
        {
            material = new Material(shader) { name = "MAT_SashLockBronzeCoated" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else
        {
            material.shader = shader;
        }

        material.color = new Color(0.29f, 0.20f, 0.12f, 1f);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Glossiness", 0.40f);
        material.SetFloat("_BumpScale", 0f);
        material.DisableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", Color.black);
        material.SetOverrideTag("RenderType", "Opaque");
        material.SetInt("_ZWrite", 1);
        material.renderQueue = (int)RenderQueue.Geometry;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ValidateOpenSceneCore()
    {
        if (validating) return;
        validating = true;
        try
        {
            ValidateContractConfigOnly();
            if (IsAuthoredDanchiActive())
            {
                if (FindSceneObject(RootName) != null)
                    throw new InvalidOperationException("Generated sash-lock root must not remain active with authored danchi art.");
                return;
            }

            GameObject danchi = FindSceneObject("Danchi");
            GameObject root = FindSceneObject(RootName);
            if (danchi == null || root == null)
                throw new InvalidOperationException("Danchi or SashCrescentLocks root is missing from the prepared scene.");
            if (root.transform.parent != danchi.transform)
                throw new InvalidOperationException("SashCrescentLocks must be parented directly under Danchi for metadata coverage.");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
                throw new InvalidOperationException("Sash-lock material asset is missing.");
            if (material.shader == null || material.shader.name != "Standard")
                throw new InvalidOperationException("Sash-lock material must use the Standard shader fallback contract.");
            if (material.GetFloat("_Metallic") > 0.001f || material.GetFloat("_Glossiness") < 0.34f || material.GetFloat("_Glossiness") > 0.46f)
                throw new InvalidOperationException("Sash-lock coated-metal response drifted outside the dielectric roughness contract.");
            if (material.IsKeywordEnabled("_EMISSION") || material.GetColor("_EmissionColor").maxColorComponent > 0.001f)
                throw new InvalidOperationException("Sash-lock material may not be emissive.");

            Transform[] locks = root.GetComponentsInChildren<Transform>(true)
                .Where(t => t.parent == root.transform && t.name.StartsWith("HD_SashCrescentLock_", StringComparison.Ordinal))
                .ToArray();
            if (locks.Length != ExpectedWindowCount)
                throw new InvalidOperationException($"Expected {ExpectedWindowCount} sash-lock assemblies, found {locks.Length}.");

            int rendererTotal = 0;
            foreach (Transform lockRoot in locks)
            {
                LODGroup group = lockRoot.GetComponent<LODGroup>();
                if (group == null)
                    throw new InvalidOperationException("Sash-lock LODGroup missing: " + lockRoot.name);
                if (group.fadeMode != LODFadeMode.CrossFade || !group.animateCrossFading)
                    throw new InvalidOperationException("Sash-lock LOD must use animated cross-fade: " + lockRoot.name);

                LOD[] lods = group.GetLODs();
                if (lods.Length != 4)
                    throw new InvalidOperationException("Sash-lock must have exactly four LOD levels: " + lockRoot.name);
                int[] expectedCounts = { 10, 5, 2, 1 };
                float[] expectedTransitions = { Lod0Transition, Lod1Transition, Lod2Transition, Lod3Cull };
                for (int i = 0; i < 4; i++)
                {
                    if (lods[i].renderers.Length != expectedCounts[i])
                        throw new InvalidOperationException($"{lockRoot.name} LOD{i} renderer count {lods[i].renderers.Length}, expected {expectedCounts[i]}.");
                    if (Mathf.Abs(lods[i].screenRelativeTransitionHeight - expectedTransitions[i]) > 0.0001f)
                        throw new InvalidOperationException($"{lockRoot.name} LOD{i} transition drifted from contract.");
                }

                MeshRenderer[] renderers = lockRoot.GetComponentsInChildren<MeshRenderer>(true);
                if (renderers.Length != expectedCounts.Sum())
                    throw new InvalidOperationException($"Unexpected renderer total on {lockRoot.name}: {renderers.Length}.");
                rendererTotal += renderers.Length;
                foreach (MeshRenderer renderer in renderers)
                {
                    if (renderer.sharedMaterial != material)
                        throw new InvalidOperationException("Sash-lock renderer lost canonical material: " + HierarchyPath(renderer.transform));
                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null || !filter.sharedMesh.name.StartsWith("GM_HD_", StringComparison.Ordinal))
                        throw new InvalidOperationException("Sash-lock renderer uses missing/non-authored geometry: " + HierarchyPath(renderer.transform));
                    if (filter.sharedMesh.name == "Cube" || filter.sharedMesh.name == "Cylinder" || filter.sharedMesh.name == "Quad" || filter.sharedMesh.name == "Plane")
                        throw new InvalidOperationException("Stock primitive mesh is forbidden in sash-lock benchmark geometry: " + HierarchyPath(renderer.transform));
                    if (renderer.GetPropertyBlockHasData())
                        throw new InvalidOperationException("MaterialPropertyBlock overrides are forbidden on sash-lock evidence geometry: " + HierarchyPath(renderer.transform));
                }

                if (lockRoot.GetComponentsInChildren<Collider>(true).Length != 0)
                    throw new InvalidOperationException("Sash-lock detail must remain render-only with no collider: " + lockRoot.name);
            }

            if (rendererTotal != ExpectedWindowCount * 18)
                throw new InvalidOperationException($"Sash-lock renderer total {rendererTotal}, expected {ExpectedWindowCount * 18}.");

            Debug.Log(
                "Sash crescent lock QA passed structurally: 30 room-side hardware assemblies, 50 mm fixing pitch, separate plate/screws/pivot/lever/keeper, " +
                "dielectric coated-metal response, authored chamfer meshes, LOD0/1/2/3 and no colliders. This awards 0 Visual Fidelity points; actual 4K pixels remain required.");
        }
        finally
        {
            validating = false;
        }
    }

    private static Renderer AddBox(string name, Transform parent, Vector3 localPosition, Vector3 size, Quaternion localRotation, Material material)
    {
        Mesh mesh = QualityBlockDetailMeshLibrary.GetChamferedBox(size);
        return AddMesh(name, parent, localPosition, localRotation, mesh, material);
    }

    private static Renderer AddCylinder(string name, Transform parent, Vector3 localPosition, float diameter, float depth, Material material)
    {
        Mesh mesh = QualityBlockDetailMeshLibrary.GetBeveledCylinder(new Vector3(diameter, depth * 0.5f, diameter), false);
        return AddMesh(name, parent, localPosition, Quaternion.Euler(90f, 0f, 0f), mesh, material);
    }

    private static Renderer AddMesh(string name, Transform parent, Vector3 localPosition, Quaternion localRotation, Mesh mesh, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = localRotation;
        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        return renderer;
    }

    private static Transform NewChild(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static void RemoveGeneratedRootIfPresent()
    {
        GameObject old = FindSceneObject(RootName);
        if (old != null)
            UnityEngine.Object.DestroyImmediate(old);
    }

    private static bool IsAuthoredDanchiActive()
    {
        return Resources.FindObjectsOfTypeAll<QualityBlockArtSlot>()
            .Any(x => x != null && x.gameObject.scene.IsValid() && x.SlotId == "danchi.main" && x.IsUsingAuthoredArt);
    }

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x != null && x.scene.IsValid() && string.Equals(x.name, name, StringComparison.Ordinal));
    }

    private static void EnsureSceneOpen()
    {
        Scene active = EditorSceneManager.GetActiveScene();
        if (!active.IsValid() || !string.Equals(active.path, ScenePath, StringComparison.Ordinal))
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static string AbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private static string HierarchyPath(Transform transform)
    {
        var names = new List<string>();
        while (transform != null)
        {
            names.Add(transform.name);
            transform = transform.parent;
        }
        names.Reverse();
        return string.Join("/", names);
    }

    private static void Require(bool condition, string message, List<string> errors)
    {
        if (!condition) errors.Add(message);
    }

    private static void RequireNear(float actual, float expected, float tolerance, string label, List<string> errors)
    {
        if (Mathf.Abs(actual - expected) > tolerance)
            errors.Add($"{label}={actual} expected {expected} +/- {tolerance}");
    }

    [Serializable]
    private sealed class SashLockContract
    {
        public string schemaVersion;
        public string assemblyId;
        public int expectedWindowCount;
        public string manufacture;
        public string dimensionsThickness;
        public string materialsFinish;
        public string mounting;
        public string interfacesGapsSeals;
        public string orientationExposure;
        public string aging;
        public string geometryVsMaterial;
        public string lodPolicy;
        public string sourceBasis;
        public string lookdevGenerationBrief;
        public Geometry geometry;
        public MaterialContract material;
        public Lod lod;
        public Qa qa;
    }

    [Serializable]
    private sealed class Geometry
    {
        public float mountingScrewPitchM;
        public float plateWidthM;
        public float plateHeightM;
        public float plateDepthM;
        public float screwHeadDiameterM;
        public float pivotDiameterM;
        public float keeperWidthM;
        public float keeperHeightM;
        public float keeperDepthM;
        public float leverArcRadiusM;
        public int leverSegmentCountLod0;
    }

    [Serializable]
    private sealed class MaterialContract
    {
        public string assetPath;
        public float roughnessMin;
        public float roughnessMax;
        public float metallicMin;
        public float metallicMax;
        public float specularF0;
        public float normalScale;
        public float wetness;
    }

    [Serializable]
    private sealed class Lod
    {
        public float lod0Transition;
        public float lod1Transition;
        public float lod2Transition;
        public float lod3Cull;
        public bool animateCrossFading;
    }

    [Serializable]
    private sealed class Qa
    {
        public int lod0RendererCountPerWindow;
        public int lod1RendererCountPerWindow;
        public int lod2RendererCountPerWindow;
        public int lod3RendererCountPerWindow;
        public bool requireNoColliders;
        public bool requireNoStockPrimitiveMeshes;
        public bool requireFormalPreCullValidation;
        public int automaticVisualPoints;
        public bool renderVerificationPending;
    }
}