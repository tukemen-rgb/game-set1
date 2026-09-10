using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Corrects a source-side construction contradiction at the WornPathA -> DanchiPlaza mouth.
///
/// The coarse soil shortcut intentionally continues below the plaza as a collision substrate, but its
/// visible top is 5 mm lower than the plaza. The legacy visual curb generator nevertheless continues
/// seven additional 0.60 m-pitch blocks per side well into that paved footprint. In a 4K benchmark
/// this can read as two arbitrary concrete rails crossing the plaza after the soil path has disappeared.
///
/// This pass keeps the seven whole manufactured modules that fit before the plaza, suppresses only the
/// seven legacy continuation renderers on each side, and leaves all gameplay geometry/colliders alone.
/// It records the correction in a scene-local manifest and fails closed if future generator changes
/// re-enable an intruding curb, replace authored mesh geometry with primitives, break the 5 mm soil/curb
/// interface, or bind a materially impossible concrete response.
///
/// This is implementation/readiness QA only. It never awards Visual Fidelity points without actual
/// native 3840x2160 rendered evidence.
/// </summary>
public static class QualityBlockGroundPathPlazaTerminationQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/ground_path_plaza_termination_contract.json";
    private const string LookdevPath = "Assets/QA/ground_path_plaza_termination_lookdev.svg";
    private const string StateName = "GroundPathPlazaTerminationState";
    private const string WestPrefix = "HD_Curb_WornPathWest_";
    private const string EastPrefix = "HD_Curb_WornPathEast_";
    private const int FirstRetainedIndex = 0;
    private const int LastRetainedIndex = 6;
    private const int FirstSuppressedIndex = 7;
    private const int LastSuppressedIndex = 13;
    private const float PositionTolerance = 0.012f;

    [MenuItem("NewTown/Geometry/Apply Worn-Path Plaza Termination Correction")]
    public static void ApplyAndPersist()
    {
        ValidateContractConfigOnly();
        RequireQualityScene();
        ApplyToOpenScene();
        ValidateOpenScene();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            "WornPathA plaza-mouth curb correction persisted. Visual Fidelity remains UNSCORED until native 4K pixels are reviewed.");
    }

    public static void ApplyToOpenScene()
    {
        ValidateContractConfigOnly();
        RequireQualityScene();

        GroundPathTerminationContract contract = LoadContract();
        GameObject curbRoot = FindSceneObject("HD_PrecastCurbAssembly");
        if (curbRoot == null)
            throw new InvalidOperationException("HD_PrecastCurbAssembly is required before path-termination correction.");

        int retainedWest = ApplySide(WestPrefix);
        int retainedEast = ApplySide(EastPrefix);

        GameObject previous = FindSceneObject(StateName);
        if (previous != null)
            UnityEngine.Object.DestroyImmediate(previous);

        var state = new GameObject(StateName);
        state.transform.SetParent(curbRoot.transform, false);
        var manifest = state.AddComponent<QualityBlockGroundPathPlazaTerminationManifest>();
        manifest.Configure(
            retainedWest,
            retainedEast,
            LastSuppressedIndex - FirstSuppressedIndex + 1,
            LastSuppressedIndex - FirstSuppressedIndex + 1,
            contract.dimensions.terminationSetbackM,
            contract.dimensions.pathToCurbInnerGapM,
            contract.dimensions.pavingTopYM - contract.dimensions.soilTopYM);
        EditorUtility.SetDirty(manifest);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    [MenuItem("NewTown/QA/Validate Worn-Path Plaza Termination")]
    public static void ValidateOpenScene()
    {
        ValidateContractConfigOnly();
        RequireQualityScene();
        GroundPathTerminationContract contract = LoadContract();

        Renderer plaza = RequireRenderer("DanchiPlaza");
        Renderer path = RequireRenderer("WornPathA");
        Bounds plazaBounds = plaza.bounds;
        Bounds pathBounds = path.bounds;

        AssertNear(plazaBounds.min.z, contract.dimensions.plazaMinZM, PositionTolerance, "DanchiPlaza south edge");
        AssertNear(plazaBounds.max.y, contract.dimensions.pavingTopYM, PositionTolerance, "DanchiPlaza top");
        AssertNear(pathBounds.max.y, contract.dimensions.soilTopYM, PositionTolerance, "WornPathA top");

        float verticalSeparation = plazaBounds.max.y - pathBounds.max.y;
        if (verticalSeparation < contract.hardLimits.pavingAboveSoilMinM - 0.001f ||
            verticalSeparation > contract.hardLimits.pavingAboveSoilMaxM + 0.001f)
        {
            throw new InvalidOperationException(
                $"WornPathA/plaza vertical separation drift: {verticalSeparation:F4} m; expected " +
                $"{contract.hardLimits.pavingAboveSoilMinM:F3}-{contract.hardLimits.pavingAboveSoilMaxM:F3} m.");
        }

        ValidateSide(WestPrefix, true, pathBounds, plazaBounds, contract);
        ValidateSide(EastPrefix, false, pathBounds, plazaBounds, contract);

        GameObject state = FindSceneObject(StateName);
        if (state == null)
            throw new InvalidOperationException($"{StateName} is missing; correction has not been applied to the prepared scene.");
        var manifest = state.GetComponent<QualityBlockGroundPathPlazaTerminationManifest>();
        if (manifest == null)
            throw new InvalidOperationException("Ground path/plaza termination manifest is missing.");
        if (manifest.RetainedWest != contract.hardLimits.requiredRetainedModulesPerSide ||
            manifest.RetainedEast != contract.hardLimits.requiredRetainedModulesPerSide)
            throw new InvalidOperationException("Path-termination retained-module manifest drift.");
        if (manifest.SuppressedWest != contract.hardLimits.requiredSuppressedLegacyModulesPerSide ||
            manifest.SuppressedEast != contract.hardLimits.requiredSuppressedLegacyModulesPerSide)
            throw new InvalidOperationException("Path-termination suppressed-module manifest drift.");
        AssertNear(manifest.TerminationSetbackM, contract.dimensions.terminationSetbackM, 0.001f, "manifest termination setback");
        AssertNear(manifest.PathToCurbGapM, contract.dimensions.pathToCurbInnerGapM, 0.001f, "manifest path-to-curb gap");
        AssertNear(manifest.PavingAboveSoilM, verticalSeparation, 0.001f, "manifest paving/soil separation");

        Collider[] correctionColliders = state.GetComponentsInChildren<Collider>(true);
        if (contract.hardLimits.requireNoColliderOnCorrectionState && correctionColliders.Length != 0)
            throw new InvalidOperationException($"Correction state must not alter gameplay collision; found {correctionColliders.Length} collider(s).");

        Debug.Log(
            "WornPathA plaza termination validation passed structurally: 7 visible whole modules per side, " +
            "7 legacy continuation renderers suppressed per side, approximately 356 mm open setback, " +
            "approximately 5 mm curb/path interface, authored meshes and dry dielectric precast material. " +
            "Actual grounding, silhouette and material response remain render-unverified and Visual Fidelity remains UNSCORED.");
    }

    [MenuItem("NewTown/QA/Validate Worn-Path Termination Contract Only")]
    public static void ValidateContractConfigOnly()
    {
        if (!File.Exists(ContractPath))
            throw new InvalidOperationException($"Ground path/plaza termination contract missing: {ContractPath}");
        if (!File.Exists(LookdevPath))
            throw new InvalidOperationException($"Ground path/plaza termination lookdev missing: {LookdevPath}");

        GroundPathTerminationContract contract = LoadContract();
        if (contract.runtimeRenderVerified)
            throw new InvalidOperationException("Path-termination contract may not claim runtime render verification before actual evidence exists.");
        if (contract.visualFidelityPointsAwarded != 0)
            throw new InvalidOperationException("Source-side path-termination QA cannot award Visual Fidelity points.");
        RequireText(contract.visualFidelityStatus, "visualFidelityStatus");
        RequireText(contract.targetPeriod, "targetPeriod");
        RequireText(contract.targetRegion, "targetRegion");
        if (contract.assembly == null || contract.dimensions == null || contract.material == null ||
            contract.lodPolicy == null || contract.hardLimits == null || contract.researchBasis == null)
            throw new InvalidOperationException("Path-termination contract is missing required construction/material sections.");

        RequireText(contract.assembly.id, "assembly.id");
        RequireText(contract.assembly.sourceCondition, "assembly.sourceCondition");
        RequireText(contract.assembly.manufacture, "assembly.manufacture");
        RequireText(contract.assembly.mounting, "assembly.mounting");
        RequireText(contract.assembly.interfaces, "assembly.interfaces");
        RequireText(contract.assembly.orientation, "assembly.orientation");
        RequireText(contract.assembly.exposure, "assembly.exposure");
        RequireText(contract.assembly.aging, "assembly.aging");
        RequireText(contract.assembly.geometryVsMaterial, "assembly.geometryVsMaterial");
        RequireText(contract.assembly.periodAuthenticity, "assembly.periodAuthenticity");
        if (contract.assembly.components == null || contract.assembly.components.Length < 4)
            throw new InvalidOperationException("Path-termination assembly must enumerate physical components.");

        if (contract.dimensions.lastRetainedModuleIndex != LastRetainedIndex ||
            contract.dimensions.firstSuppressedModuleIndex != FirstSuppressedIndex)
            throw new InvalidOperationException("Path-termination contract index policy drift.");
        if (Mathf.Abs(contract.dimensions.modulePitchM - 0.600f) > 0.001f ||
            Mathf.Abs(contract.dimensions.moduleVisibleLengthM - 0.588f) > 0.001f ||
            Mathf.Abs(contract.dimensions.moduleJointGapM - 0.012f) > 0.001f)
            throw new InvalidOperationException("Path-termination modular curb dimensions drift.");
        if (contract.dimensions.terminationSetbackM < contract.hardLimits.terminationSetbackMinM ||
            contract.dimensions.terminationSetbackM > contract.hardLimits.terminationSetbackMaxM)
            throw new InvalidOperationException("Path-termination setback is outside its hard range.");
        if (contract.dimensions.pathToCurbInnerGapM < contract.hardLimits.pathToCurbGapMinM ||
            contract.dimensions.pathToCurbInnerGapM > contract.hardLimits.pathToCurbGapMaxM)
            throw new InvalidOperationException("Path-to-curb interface gap is outside its hard range.");
        if (contract.dimensions.legacyIntrusionIntoPlazaM < 0.20f)
            throw new InvalidOperationException("Contract no longer demonstrates the legacy curb/plaza intrusion being prevented.");

        RequireText(contract.material.id, "material.id");
        RequireText(contract.material.assetPath, "material.assetPath");
        RequireText(contract.material.microstructure, "material.microstructure");
        RequireText(contract.material.wetResponse, "material.wetResponse");
        RequireText(contract.material.uvAging, "material.uvAging");
        RequireText(contract.material.angularFresnelResponse, "material.angularFresnelResponse");
        if (contract.material.albedoSrgb == null || contract.material.albedoSrgb.Length != 3)
            throw new InvalidOperationException("Path-termination concrete albedo must contain three channels.");
        if (contract.material.metallic > contract.hardLimits.maxMetallic)
            throw new InvalidOperationException("Path-termination curb cannot be materially metallic.");
        if (contract.material.specularF0 < 0.02f || contract.material.specularF0 > 0.08f)
            throw new InvalidOperationException("Path-termination dielectric F0 is outside a plausible fallback range.");
        if (contract.material.roughnessNominal < contract.material.roughnessAllowedMin ||
            contract.material.roughnessNominal > contract.material.roughnessAllowedMax)
            throw new InvalidOperationException("Path-termination nominal roughness is outside declared bounds.");
        if (contract.material.wetness != 0f)
            throw new InvalidOperationException("Current dry midsummer benchmark must not silently declare a wet curb state.");

        RequireText(contract.lodPolicy.lod0, "lodPolicy.lod0");
        RequireText(contract.lodPolicy.lod1, "lodPolicy.lod1");
        RequireText(contract.lodPolicy.lod2, "lodPolicy.lod2");
        RequireText(contract.lodPolicy.lod3, "lodPolicy.lod3");
        RequireText(contract.lodPolicy.transitionPolicy, "lodPolicy.transitionPolicy");
        RequireText(contract.researchBasis.source, "researchBasis.source");
        RequireText(contract.researchBasis.referenceUse, "researchBasis.referenceUse");
        RequireText(contract.researchBasis.sourceUrl, "researchBasis.sourceUrl");
        if (contract.evidencePlan == null || contract.evidencePlan.Length < 4)
            throw new InvalidOperationException("Path-termination contract must define multi-angle plus temporal evidence plans.");
        if (contract.criticalFailPrevention == null || contract.criticalFailPrevention.Length < 5)
            throw new InvalidOperationException("Path-termination contract must define critical-fail prevention rules.");
    }

    private static int ApplySide(string prefix)
    {
        int retained = 0;
        for (int i = FirstRetainedIndex; i <= LastSuppressedIndex; i++)
        {
            GameObject go = FindSceneObject(prefix + i.ToString("00"));
            if (go == null)
                throw new InvalidOperationException($"Expected generated curb module missing: {prefix}{i:00}");
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (renderer == null)
                throw new InvalidOperationException($"Generated curb module has no MeshRenderer: {go.name}");

            bool shouldRender = i <= LastRetainedIndex;
            renderer.enabled = shouldRender;
            EditorUtility.SetDirty(renderer);
            if (shouldRender)
                retained++;
        }
        return retained;
    }

    private static void ValidateSide(string prefix, bool west, Bounds pathBounds, Bounds plazaBounds,
        GroundPathTerminationContract contract)
    {
        int retained = 0;
        int suppressed = 0;
        Renderer lastRetained = null;

        for (int i = FirstRetainedIndex; i <= LastSuppressedIndex; i++)
        {
            GameObject go = FindSceneObject(prefix + i.ToString("00"));
            if (go == null)
                throw new InvalidOperationException($"Path-termination QA missing module {prefix}{i:00}.");
            MeshFilter filter = go.GetComponent<MeshFilter>();
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (filter == null || filter.sharedMesh == null || renderer == null)
                throw new InvalidOperationException($"Incomplete curb render geometry on {go.name}.");
            if (!filter.sharedMesh.name.StartsWith(contract.hardLimits.requireAuthoredMeshPrefix, StringComparison.Ordinal))
                throw new InvalidOperationException($"Curb module {go.name} uses non-authored/primitive mesh {filter.sharedMesh.name}.");
            if (go.GetComponent<Collider>() != null)
                throw new InvalidOperationException($"Generated visual curb module {go.name} unexpectedly owns gameplay collision.");

            if (i <= LastRetainedIndex)
            {
                if (!renderer.enabled)
                    throw new InvalidOperationException($"Required visible curb module is disabled: {go.name}.");
                retained++;
                lastRetained = renderer;
                ValidateConcreteMaterial(renderer, contract);
                if (contract.hardLimits.requireWeatheringMetadataOnRetainedModules &&
                    go.GetComponent<QualityBlockWeatheringSurface>() == null)
                    throw new InvalidOperationException($"Retained curb module lacks cause-based weathering metadata: {go.name}.");
                if (contract.hardLimits.requireNoActiveCurbInsidePlaza &&
                    renderer.bounds.max.z > plazaBounds.min.z - 0.010f)
                    throw new InvalidOperationException(
                        $"Active curb module {go.name} intrudes into DanchiPlaza: maxZ={renderer.bounds.max.z:F3}, plazaMinZ={plazaBounds.min.z:F3}.");
            }
            else
            {
                if (renderer.enabled)
                    throw new InvalidOperationException($"Legacy curb continuation must remain suppressed: {go.name}.");
                suppressed++;
            }
        }

        if (retained != contract.hardLimits.requiredRetainedModulesPerSide)
            throw new InvalidOperationException($"{prefix} retained count={retained}; expected {contract.hardLimits.requiredRetainedModulesPerSide}.");
        if (suppressed != contract.hardLimits.requiredSuppressedLegacyModulesPerSide)
            throw new InvalidOperationException($"{prefix} suppressed count={suppressed}; expected {contract.hardLimits.requiredSuppressedLegacyModulesPerSide}.");
        if (lastRetained == null)
            throw new InvalidOperationException($"{prefix} has no retained terminal module.");

        float setback = plazaBounds.min.z - lastRetained.bounds.max.z;
        if (setback < contract.hardLimits.terminationSetbackMinM || setback > contract.hardLimits.terminationSetbackMaxM)
            throw new InvalidOperationException(
                $"{prefix} termination setback={setback:F3} m outside " +
                $"{contract.hardLimits.terminationSetbackMinM:F3}-{contract.hardLimits.terminationSetbackMaxM:F3} m.");
        AssertNear(setback, contract.dimensions.terminationSetbackM, PositionTolerance, $"{prefix} termination setback");

        Renderer datum = RequireRenderer(prefix + "06");
        float interfaceGap = west
            ? pathBounds.min.x - datum.bounds.max.x
            : datum.bounds.min.x - pathBounds.max.x;
        if (interfaceGap < contract.hardLimits.pathToCurbGapMinM || interfaceGap > contract.hardLimits.pathToCurbGapMaxM)
            throw new InvalidOperationException(
                $"{prefix} path interface gap={interfaceGap:F4} m outside " +
                $"{contract.hardLimits.pathToCurbGapMinM:F3}-{contract.hardLimits.pathToCurbGapMaxM:F3} m.");
        AssertNear(interfaceGap, contract.dimensions.pathToCurbInnerGapM, 0.002f, $"{prefix} path-to-curb gap");

        // Prove the first suppressed source module really is the contradictory continuation that would
        // cross the plaza boundary if accidentally re-enabled. This makes a silent generator-coordinate
        // drift fail closed rather than masking an unrelated object by name.
        Renderer firstSuppressed = RequireRenderer(prefix + FirstSuppressedIndex.ToString("00"));
        float legacyIntrusion = firstSuppressed.bounds.max.z - plazaBounds.min.z;
        if (legacyIntrusion < 0.20f)
            throw new InvalidOperationException(
                $"{prefix} first suppressed module no longer matches the documented legacy intrusion; observed {legacyIntrusion:F3} m. Re-audit instead of blindly suppressing it.");
        AssertNear(legacyIntrusion, contract.dimensions.legacyIntrusionIntoPlazaM, PositionTolerance, $"{prefix} documented legacy intrusion");
    }

    private static void ValidateConcreteMaterial(Renderer renderer, GroundPathTerminationContract contract)
    {
        Material material = renderer.sharedMaterial;
        if (material == null)
            throw new InvalidOperationException($"Curb renderer {renderer.name} has no material.");
        if (!string.Equals(AssetDatabase.GetAssetPath(material), contract.material.assetPath, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Curb renderer {renderer.name} material drift: {AssetDatabase.GetAssetPath(material)}; expected {contract.material.assetPath}.");
        if (material.shader == null || material.shader.name != "Standard")
            throw new InvalidOperationException($"Curb renderer {renderer.name} must use the Standard PBR fallback shader.");
        if (!material.HasProperty("_Metallic") || material.GetFloat("_Metallic") > contract.hardLimits.maxMetallic)
            throw new InvalidOperationException($"Materially impossible metallic concrete on {renderer.name}.");
        if (!material.IsKeywordEnabled("_NORMALMAP") || material.GetTexture("_BumpMap") == null)
            throw new InvalidOperationException($"Curb renderer {renderer.name} is missing physical micro-normal response.");
        if (!material.IsKeywordEnabled("_METALLICGLOSSMAP") || material.GetTexture("_MetallicGlossMap") == null)
            throw new InvalidOperationException($"Curb renderer {renderer.name} is missing the roughness/smoothness mask path.");
    }

    private static Renderer RequireRenderer(string objectName)
    {
        GameObject go = FindSceneObject(objectName);
        Renderer renderer = go != null ? go.GetComponent<Renderer>() : null;
        if (renderer == null)
            throw new InvalidOperationException($"Required renderer missing: {objectName}.");
        return renderer;
    }

    private static void RequireQualityScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (FindSceneObject("QualityBlock1990s") == null)
            throw new InvalidOperationException("QualityBlock1990s root is missing.");
    }

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x.scene.IsValid() && x.name == name);
    }

    private static GroundPathTerminationContract LoadContract()
    {
        GroundPathTerminationContract contract = JsonUtility.FromJson<GroundPathTerminationContract>(File.ReadAllText(ContractPath));
        if (contract == null)
            throw new InvalidOperationException("Ground path/plaza termination contract could not be parsed.");
        return contract;
    }

    private static void RequireText(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Ground path/plaza termination contract missing required {label}.");
    }

    private static void AssertNear(float actual, float expected, float tolerance, string label)
    {
        if (Mathf.Abs(actual - expected) > tolerance)
            throw new InvalidOperationException($"{label} drift: actual={actual:F4}, expected={expected:F4}, tolerance={tolerance:F4}.");
    }

    [Serializable]
    private sealed class GroundPathTerminationContract
    {
        public string schemaVersion;
        public string targetPeriod;
        public string targetRegion;
        public bool runtimeRenderVerified;
        public string visualFidelityStatus;
        public int visualFidelityPointsAwarded;
        public AssemblySpec assembly;
        public DimensionSpec dimensions;
        public MaterialSpec material;
        public LodSpec lodPolicy;
        public HardLimitSpec hardLimits;
        public ResearchSpec researchBasis;
        public string[] evidencePlan;
        public string[] criticalFailPrevention;
    }

    [Serializable]
    private sealed class AssemblySpec
    {
        public string id;
        public string sourceCondition;
        public string[] components;
        public string manufacture;
        public string mounting;
        public string interfaces;
        public string orientation;
        public string exposure;
        public string aging;
        public string geometryVsMaterial;
        public string periodAuthenticity;
    }

    [Serializable]
    private sealed class DimensionSpec
    {
        public float modulePitchM;
        public float moduleVisibleLengthM;
        public float moduleJointGapM;
        public float moduleWidthM;
        public float moduleHeightM;
        public float moduleCenterYM;
        public float moduleBottomYM;
        public float curbTopYM;
        public float soilTopYM;
        public float pavingTopYM;
        public float pathToCurbInnerGapM;
        public int lastRetainedModuleIndex;
        public float lastRetainedModuleMaxZM;
        public float plazaMinZM;
        public float terminationSetbackM;
        public int firstSuppressedModuleIndex;
        public float firstSuppressedModuleMaxZM;
        public float legacyIntrusionIntoPlazaM;
    }

    [Serializable]
    private sealed class MaterialSpec
    {
        public string id;
        public string assetPath;
        public float[] albedoSrgb;
        public float roughnessNominal;
        public float roughnessAllowedMin;
        public float roughnessAllowedMax;
        public float metallic;
        public float specularF0;
        public float normalScale;
        public string microstructure;
        public float wetness;
        public string wetResponse;
        public string uvAging;
        public string angularFresnelResponse;
    }

    [Serializable]
    private sealed class LodSpec
    {
        public string lod0;
        public string lod1;
        public string lod2;
        public string lod3;
        public string transitionPolicy;
    }

    [Serializable]
    private sealed class HardLimitSpec
    {
        public int requiredRetainedModulesPerSide;
        public int requiredSuppressedLegacyModulesPerSide;
        public float terminationSetbackMinM;
        public float terminationSetbackMaxM;
        public float pathToCurbGapMinM;
        public float pathToCurbGapMaxM;
        public float pavingAboveSoilMinM;
        public float pavingAboveSoilMaxM;
        public float maxMetallic;
        public string requireAuthoredMeshPrefix;
        public bool requireNoActiveCurbInsidePlaza;
        public bool requireNoColliderOnCorrectionState;
        public bool requireWeatheringMetadataOnRetainedModules;
    }

    [Serializable]
    private sealed class ResearchSpec
    {
        public string source;
        public string referenceUse;
        public string sourceUrl;
    }
}

/// <summary>
/// Scene-local source/readiness evidence only. These values do not represent rendered quality.
/// </summary>
public sealed class QualityBlockGroundPathPlazaTerminationManifest : MonoBehaviour
{
    [SerializeField] private int retainedWest;
    [SerializeField] private int retainedEast;
    [SerializeField] private int suppressedWest;
    [SerializeField] private int suppressedEast;
    [SerializeField] private float terminationSetbackM;
    [SerializeField] private float pathToCurbGapM;
    [SerializeField] private float pavingAboveSoilM;

    public int RetainedWest => retainedWest;
    public int RetainedEast => retainedEast;
    public int SuppressedWest => suppressedWest;
    public int SuppressedEast => suppressedEast;
    public float TerminationSetbackM => terminationSetbackM;
    public float PathToCurbGapM => pathToCurbGapM;
    public float PavingAboveSoilM => pavingAboveSoilM;

    public void Configure(int visibleWest, int visibleEast, int hiddenWest, int hiddenEast,
        float setback, float interfaceGap, float verticalSeparation)
    {
        retainedWest = visibleWest;
        retainedEast = visibleEast;
        suppressedWest = hiddenWest;
        suppressedEast = hiddenEast;
        terminationSetbackM = setback;
        pathToCurbGapM = interfaceGap;
        pavingAboveSoilM = verticalSeparation;
    }
}