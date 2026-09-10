using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Repairs and fail-closes the construction interface between generated mature root flares,
/// maintained plaza tree pits, the lawn datum and low understory. The original ecology pass used
/// a decorative-scale 0.74-0.86 m pit radius while the generated root-flare centerline can approach
/// 0.94 m; that can place roots through curb/paving. Boundary rings also need to follow the actual
/// support grade instead of hovering at one plaza-derived world height.
///
/// This pass is intentionally downstream of QualityBlockVegetationEcologyUpgrade. It preserves the
/// coarse gameplay ground/trunk colliders, changes only visual construction, and remains visually
/// UNSCORED until native Unity 4K evidence is reviewed.
/// </summary>
public static class QualityBlockVegetationRootZoneInterfaceQA
{
    public const string BenchmarkScenePath = "Assets/Scenes/QualityBlock1990s.unity";

    private const string ContractPath = "Assets/QA/vegetation_root_zone_interface_contract.json";
    private const string LookdevPath = "Assets/QA/vegetation_tree_pit_root_interface_lookdev.svg";
    private const string EcologyRootName = "VegetationEcologyDetail";
    private const string PitName = "MaintainedTreePit";

    private const int TreeCount = 6;
    private const int MaintainedPitCount = 4;
    private const int EdgingModules = 12;
    private const int ExpectedBoundaryRings = 3;

    private const float OpeningRadius = 1.15f;
    private const float CurbRadialWidth = 0.15f;
    private const float CurbHeight = 0.08f;
    private const float CurbPavingEmbed = 0.008f;
    private const float CurbGrassEmbed = 0.006f;
    private const float CurbJointGap = 0.012f;
    private const float SoilRenderSeparation = 0.0015f;
    private const float SoilGrassEmbed = 0.006f;
    private const float PavedPlantRootOffset = 0.001f;
    private const float GrassPlantRootOffset = 0.004f;
    private const float RootTipRadiusRatio = 0.58f;
    private const float MinimumRootClearance = 0.09f;

    private static readonly float[] RingPhaseDegrees = { 0f, 11f, 22f, 3f, 0f, 0f };

    [MenuItem("NewTown/Vegetation/Apply Root-Zone Construction Interface")]
    public static void ApplyAndPersist()
    {
        OpenBenchmarkIfNeeded();
        QualityBlockVegetationEcologyContractQA.Validate();
        ValidateContractConfigOnly();
        QualityBlockVegetationEcologyUpgrade.ApplyToOpenScene();
        QualityBlockVegetationEcologyUpgrade.ValidateOpenScene();
        ApplyToOpenScene();
        ValidateOpenScene();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Vegetation root-zone construction interface applied. Native 4K visual verification remains pending and Visual Fidelity remains UNSCORED.");
    }

    public static void ValidateContractConfigOnly()
    {
        if (!File.Exists(ContractPath))
            throw new InvalidOperationException($"Vegetation root-zone contract missing: {ContractPath}");
        if (!File.Exists(LookdevPath))
            throw new InvalidOperationException($"Vegetation root-zone lookdev illustration missing: {LookdevPath}");

        RootZoneContract contract = JsonUtility.FromJson<RootZoneContract>(File.ReadAllText(ContractPath));
        if (contract == null)
            throw new InvalidOperationException("Vegetation root-zone contract could not be parsed.");
        if (contract.runtimeRenderVerified)
            throw new InvalidOperationException("Root-zone contract must not claim runtime render verification before real Unity evidence exists.");
        if (!string.Equals(contract.visualFidelityStatus, "UNSCORED_UNTIL_REAL_4K_RENDER", StringComparison.Ordinal))
            throw new InvalidOperationException("Root-zone contract must remain visually unscored until native 4K pixels are reviewed.");
        if (contract.sceneDatum == null || contract.rootFlareEnvelope == null || contract.pitAssembly == null ||
            contract.understoryGrounding == null || contract.qa == null)
            throw new InvalidOperationException("Vegetation root-zone contract is missing required construction sections.");

        if (contract.sceneDatum.treeAnchorCount != TreeCount || contract.sceneDatum.maintainedPitCount != MaintainedPitCount ||
            contract.sceneDatum.interiorPavedRingCount != 1 || contract.sceneDatum.pavingBoundaryRingCount != ExpectedBoundaryRings ||
            contract.sceneDatum.grassOnlyTreeCount != 2 || !contract.sceneDatum.noGameplayColliderChanges)
            throw new InvalidOperationException("Vegetation root-zone scene datum/count contract drifted from the benchmark.");

        if (contract.rootFlareEnvelope.generatedRootFlaresPerTree != 7 ||
            !Near(contract.rootFlareEnvelope.tipRadiusRatio, RootTipRadiusRatio, 0.0001f) ||
            !Near(contract.rootFlareEnvelope.conservativeMaximumOuterRadiusMeters, 1.0531f, 0.0002f) ||
            !Near(contract.rootFlareEnvelope.minimumVisibleSoilClearanceToEdgingMeters, MinimumRootClearance, 0.0001f) ||
            !Near(contract.rootFlareEnvelope.requiredOpeningRadiusMeters, OpeningRadius, 0.0001f))
            throw new InvalidOperationException("Vegetation root-flare envelope/opening-radius contract drift detected.");
        if (OpeningRadius - contract.rootFlareEnvelope.conservativeMaximumOuterRadiusMeters < MinimumRootClearance)
            throw new InvalidOperationException("Contracted pit opening does not clear the conservative generated root-flare envelope.");

        PitAssemblySpec pit = contract.pitAssembly;
        if (!Near(pit.openingRadiusMeters, OpeningRadius, 0.0001f) ||
            !Near(pit.openingDiameterMeters, OpeningRadius * 2f, 0.0001f) ||
            pit.curbModuleCount != EdgingModules ||
            !Near(pit.curbRadialWidthMeters, CurbRadialWidth, 0.0001f) ||
            !Near(pit.curbHeightMeters, CurbHeight, 0.0001f) ||
            !Near(pit.curbPavingEmbedMeters, CurbPavingEmbed, 0.0001f) ||
            !Near(pit.curbGrassEmbedMeters, CurbGrassEmbed, 0.0001f) ||
            !Near(pit.curbJointGapMeters, CurbJointGap, 0.0001f) ||
            !Near(pit.soilTopVisualBiasAbovePavingMeters, SoilRenderSeparation, 0.0001f) ||
            !Near(pit.soilVisualBottomEmbedBelowGrassMeters, SoilGrassEmbed, 0.0001f))
            throw new InvalidOperationException("Vegetation root-zone pit fabrication dimensions drifted from the runtime refinement.");

        RequireText(pit.manufacture, "pit manufacture");
        RequireText(pit.mounting, "pit mounting");
        RequireText(pit.interfacesGapsSeals, "pit interfaces/gaps/seals");
        RequireText(pit.orientationExposure, "pit orientation/exposure");
        RequireText(pit.aging, "pit aging");
        RequireText(pit.geometryVsMaterial, "pit geometry-vs-material reasoning");
        RequireText(pit.lodPolicy, "pit LOD policy");

        if (!Near(contract.understoryGrounding.pavedPitPlantRootAboveSoilMeters, PavedPlantRootOffset, 0.0001f) ||
            !Near(contract.understoryGrounding.grassPlantRootAboveGroundMeters, GrassPlantRootOffset, 0.0001f))
            throw new InvalidOperationException("Understory physical grounding offsets drifted from the contract.");

        if (contract.materials == null || contract.materials.Length < 3)
            throw new InvalidOperationException("Root-zone contract must define soil, curb and bark material response.");
        foreach (MaterialSpec material in contract.materials)
        {
            RequireText(material.id, "root-zone material id");
            RequireText(material.sourceAsset, $"sourceAsset for {material.id}");
            RequireText(material.microstructure, $"microstructure for {material.id}");
            RequireText(material.wetness, $"wetness response for {material.id}");
            RequireText(material.uvAging, $"UV aging for {material.id}");
            RequireText(material.angularFresnel, $"angular/Fresnel response for {material.id}");
            ValidateRange(material.albedoSrgbTargetRange, 0f, 1f, $"albedo range for {material.id}");
            ValidateRange(material.roughnessRange, 0f, 1f, $"roughness range for {material.id}");
            ValidateRange(material.metallicRange, 0f, 1f, $"metallic range for {material.id}");
            if (material.metallicRange[1] > 0.02f)
                throw new InvalidOperationException($"Root-zone material {material.id} must remain dielectric/non-metallic.");
            if (material.dielectricF0Approx < 0.02f || material.dielectricF0Approx > 0.08f)
                throw new InvalidOperationException($"Implausible dielectric F0 in root-zone material {material.id}: {material.dielectricF0Approx}.");
            if (material.normalScaleTarget <= 0f || material.normalScaleTarget > 2f)
                throw new InvalidOperationException($"Implausible normal scale in root-zone material {material.id}: {material.normalScaleTarget}.");
        }

        if (!contract.qa.requireLookdevIllustration || !contract.qa.requireExactOpeningRadius ||
            !contract.qa.requireRootFlareClearance || !contract.qa.requireLocalSupportGradeForEveryCurbModule ||
            !contract.qa.requireNoSoilUndersideGapAtGrassBoundary || !contract.qa.requireUnderstorySurfaceGrounding ||
            !contract.qa.requireNoGeneratedColliders || !contract.qa.requireFourPits || !contract.qa.requireThreeBoundaryRings)
            throw new InvalidOperationException("Vegetation root-zone QA contract is missing a hard physical interface invariant.");
    }

    public static void ApplyToOpenScene()
    {
        OpenBenchmarkIfNeeded();
        ValidateContractConfigOnly();

        GameObject ecologyRoot = FindSceneObject(EcologyRootName);
        Renderer plaza = FindSceneRenderer("DanchiPlaza");
        Renderer grass = FindSceneRenderer("GrassField");
        if (ecologyRoot == null || plaza == null || grass == null)
            throw new InvalidOperationException("Root-zone refinement requires VegetationEcologyDetail, DanchiPlaza and GrassField.");

        Transform[] patches = Enumerable.Range(0, TreeCount)
            .Select(i => ecologyRoot.transform.Find($"EcologyPatch_{i}"))
            .ToArray();
        if (patches.Any(x => x == null))
            throw new InvalidOperationException("Root-zone refinement requires all six ecology patches.");

        float plazaTop = plaza.bounds.max.y;
        float grassTop = grass.bounds.max.y;
        float soilTop = plazaTop + SoilRenderSeparation;
        float soilBottom = grassTop - SoilGrassEmbed;
        if (soilTop <= soilBottom)
            throw new InvalidOperationException("Invalid benchmark surface datums for root-zone soil volume.");

        for (int i = 0; i < patches.Length; i++)
        {
            Transform patch = patches[i];
            bool paved = ContainsXZ(plaza.bounds, patch.position);
            Transform pit = patch.Find(PitName);
            if (paved)
            {
                if (pit == null)
                    throw new InvalidOperationException($"Paved tree {i} has no maintained pit to refine.");
                RefinePit(pit, patch, i, plaza.bounds, plazaTop, grassTop, soilTop, soilBottom);
            }
            else if (pit != null)
            {
                throw new InvalidOperationException($"Grass-only tree {i} unexpectedly has a maintained pit.");
            }

            GroundUnderstory(patch, paved ? soilTop + PavedPlantRootOffset : grassTop + GrassPlantRootOffset);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    [MenuItem("NewTown/QA/Validate Vegetation Root-Zone Construction Interface")]
    public static void ValidateOpenScene()
    {
        OpenBenchmarkIfNeeded();
        ValidateContractConfigOnly();
        QualityBlockVegetationEcologyUpgrade.ValidateOpenScene();

        GameObject ecologyRoot = FindSceneObject(EcologyRootName);
        Renderer plaza = FindSceneRenderer("DanchiPlaza");
        Renderer grass = FindSceneRenderer("GrassField");
        if (ecologyRoot == null || plaza == null || grass == null)
            throw new InvalidOperationException("Vegetation root-zone validation requires ecology and ground surfaces.");

        QualityBlockArtSlot[] treeSlots = Resources.FindObjectsOfTypeAll<QualityBlockArtSlot>()
            .Where(x => x.gameObject.scene.IsValid() && x.SlotId.StartsWith("vegetation.tree.", StringComparison.Ordinal))
            .OrderBy(x => x.SlotId, StringComparer.Ordinal)
            .ToArray();
        if (treeSlots.Length != TreeCount)
            throw new InvalidOperationException($"Expected {TreeCount} tree slots for root-zone validation, got {treeSlots.Length}.");

        float plazaTop = plaza.bounds.max.y;
        float grassTop = grass.bounds.max.y;
        float expectedSoilTop = plazaTop + SoilRenderSeparation;
        float expectedSoilBottom = grassTop - SoilGrassEmbed;
        float expectedCurbCenterRadius = OpeningRadius + CurbRadialWidth * 0.5f;
        float expectedModuleLength = CurbChordLength(expectedCurbCenterRadius) - CurbJointGap;

        int pitCount = 0;
        int boundaryRingCount = 0;
        int interiorPavedRingCount = 0;

        for (int i = 0; i < TreeCount; i++)
        {
            Transform patch = ecologyRoot.transform.Find($"EcologyPatch_{i}");
            if (patch == null)
                throw new InvalidOperationException($"Missing ecology patch {i} during root-zone validation.");
            bool paved = ContainsXZ(plaza.bounds, patch.position);
            Transform pit = patch.Find(PitName);

            if (!paved)
            {
                if (pit != null)
                    throw new InvalidOperationException($"Grass-only tree {i} unexpectedly contains a maintained pit.");
                ValidatePlantGrounding(patch, grassTop + GrassPlantRootOffset, i);
                continue;
            }

            pitCount++;
            if (pit == null)
                throw new InvalidOperationException($"Paved tree {i} is missing its maintained root-zone pit.");

            MeshRenderer soil = pit.Find("SoilDisk")?.GetComponent<MeshRenderer>();
            MeshFilter soilFilter = pit.Find("SoilDisk")?.GetComponent<MeshFilter>();
            if (soil == null || soilFilter == null || soilFilter.sharedMesh == null)
                throw new InvalidOperationException($"Tree {i} root-zone soil mesh is missing.");
            if (!soilFilter.sharedMesh.name.StartsWith("GM_HD_", StringComparison.Ordinal))
                throw new InvalidOperationException($"Tree {i} root-zone soil uses non-authored/primitive mesh {soilFilter.sharedMesh.name}.");
            AssertNear(soil.bounds.extents.x, OpeningRadius, 0.012f, $"tree {i} soil opening X radius");
            AssertNear(soil.bounds.extents.z, OpeningRadius, 0.012f, $"tree {i} soil opening Z radius");
            AssertNear(soil.bounds.max.y, expectedSoilTop, 0.003f, $"tree {i} soil top/render-separation datum");
            if (soil.bounds.min.y > grassTop + 0.001f)
                throw new InvalidOperationException(
                    $"Tree {i} root-zone soil underside is unsupported above lawn datum: minY={soil.bounds.min.y:F4}, grassTop={grassTop:F4}.");
            if (soil.sharedMaterial == null || (soil.sharedMaterial.HasProperty("_Metallic") && soil.sharedMaterial.GetFloat("_Metallic") > 0.02f))
                throw new InvalidOperationException($"Tree {i} root-zone soil material is missing or materially impossible/metallic.");

            MeshRenderer[] edges = pit.GetComponentsInChildren<MeshRenderer>(true)
                .Where(x => x.gameObject.name.StartsWith("PitEdge_", StringComparison.Ordinal))
                .OrderBy(x => x.gameObject.name, StringComparer.Ordinal)
                .ToArray();
            if (edges.Length != EdgingModules)
                throw new InvalidOperationException($"Tree {i} requires {EdgingModules} curb modules, got {edges.Length}.");

            int pavingSupported = 0;
            int grassSupported = 0;
            foreach (MeshRenderer edge in edges)
            {
                MeshFilter filter = edge.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null || !filter.sharedMesh.name.StartsWith("GM_HD_", StringComparison.Ordinal))
                    throw new InvalidOperationException($"Tree {i} curb {edge.name} uses missing/non-authored geometry.");
                AssertNear(filter.sharedMesh.bounds.size.z, expectedModuleLength, 0.004f,
                    $"tree {i} curb {edge.name} visible chord length");

                float radial = HorizontalDistance(patch.position, edge.transform.position);
                AssertNear(radial, expectedCurbCenterRadius, 0.004f, $"tree {i} curb {edge.name} center radius");

                bool onPaving = ContainsXZ(plaza.bounds, edge.transform.position);
                float supportTop = onPaving ? plazaTop : grassTop;
                float embed = onPaving ? CurbPavingEmbed : CurbGrassEmbed;
                float expectedCenterY = supportTop - embed + CurbHeight * 0.5f;
                AssertNear(edge.transform.position.y, expectedCenterY, 0.002f,
                    $"tree {i} curb {edge.name} local-support center height");
                if (onPaving) pavingSupported++; else grassSupported++;

                if (edge.sharedMaterial == null || (edge.sharedMaterial.HasProperty("_Metallic") && edge.sharedMaterial.GetFloat("_Metallic") > 0.02f))
                    throw new InvalidOperationException($"Tree {i} curb {edge.name} material is missing or materially impossible/metallic.");
            }

            if (pavingSupported > 0 && grassSupported > 0)
                boundaryRingCount++;
            else if (pavingSupported == EdgingModules)
                interiorPavedRingCount++;
            else
                throw new InvalidOperationException(
                    $"Tree {i} maintained ring has unsupported substrate classification: paving={pavingSupported}, grass={grassSupported}.");

            ValidateGeneratedRootClearance(treeSlots[i], patch.position, i);
            ValidatePlantGrounding(patch, expectedSoilTop + PavedPlantRootOffset, i);
        }

        if (pitCount != MaintainedPitCount)
            throw new InvalidOperationException($"Expected {MaintainedPitCount} maintained pits, got {pitCount}.");
        if (boundaryRingCount != ExpectedBoundaryRings || interiorPavedRingCount != 1)
            throw new InvalidOperationException(
                $"Benchmark tree-pit support classification drift: boundary/interior={boundaryRingCount}/{interiorPavedRingCount}, expected 3/1.");

        Collider[] generatedColliders = ecologyRoot.GetComponentsInChildren<Collider>(true);
        if (generatedColliders.Length != 0)
            throw new InvalidOperationException($"Root-zone visual construction must remain collider-separated; found {generatedColliders.Length} colliders.");

        Debug.Log(
            "Vegetation root-zone interface QA passed structurally: four 2.30 m root-zone openings, minimum generated root clearance, " +
            "three grade-following plaza/lawn boundary rings, one interior paved ring, supported soil volume and grounded understory. " +
            "Actual 4K intersection/contact appearance remains UNSCORED until native rendered evidence is reviewed.");
    }

    private static void RefinePit(Transform pit, Transform patch, int treeIndex, Bounds plazaBounds,
        float plazaTop, float grassTop, float soilTop, float soilBottom)
    {
        Transform soilTransform = pit.Find("SoilDisk");
        MeshFilter soilFilter = soilTransform?.GetComponent<MeshFilter>();
        if (soilTransform == null || soilFilter == null)
            throw new InvalidOperationException($"Tree {treeIndex} soil disk missing before root-zone refinement.");

        float soilThickness = soilTop - soilBottom;
        soilFilter.sharedMesh = QualityBlockDetailMeshLibrary.GetBeveledCylinder(
            new Vector3(OpeningRadius * 2f, soilThickness, OpeningRadius * 2f), false);
        Vector3 soilWorld = soilTransform.position;
        soilWorld.y = (soilTop + soilBottom) * 0.5f;
        soilTransform.position = soilWorld;

        MeshRenderer[] edges = pit.GetComponentsInChildren<MeshRenderer>(true)
            .Where(x => x.gameObject.name.StartsWith("PitEdge_", StringComparison.Ordinal))
            .OrderBy(x => x.gameObject.name, StringComparer.Ordinal)
            .ToArray();
        if (edges.Length != EdgingModules)
            throw new InvalidOperationException($"Tree {treeIndex} expected {EdgingModules} curb modules before refinement, got {edges.Length}.");

        float centerRadius = OpeningRadius + CurbRadialWidth * 0.5f;
        float moduleLength = CurbChordLength(centerRadius) - CurbJointGap;
        Mesh edgeMesh = QualityBlockDetailMeshLibrary.GetChamferedBox(
            new Vector3(CurbRadialWidth, CurbHeight, moduleLength));
        float phase = RingPhaseDegrees[Mathf.Clamp(treeIndex, 0, RingPhaseDegrees.Length - 1)] * Mathf.Deg2Rad;

        for (int m = 0; m < edges.Length; m++)
        {
            float angle = phase + Mathf.PI * 2f * m / EdgingModules;
            Vector3 radial = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 tangent = new(-radial.z, 0f, radial.x);
            MeshRenderer edge = edges[m];
            edge.GetComponent<MeshFilter>().sharedMesh = edgeMesh;
            edge.transform.localPosition = radial * centerRadius;
            edge.transform.localRotation = Quaternion.LookRotation(tangent, Vector3.up);

            Vector3 world = edge.transform.position;
            bool onPaving = ContainsXZ(plazaBounds, world);
            float supportTop = onPaving ? plazaTop : grassTop;
            float embed = onPaving ? CurbPavingEmbed : CurbGrassEmbed;
            world.y = supportTop - embed + CurbHeight * 0.5f;
            edge.transform.position = world;
        }
    }

    private static void GroundUnderstory(Transform patch, float targetWorldY)
    {
        Transform lodRoot = patch.Find("UnderstoryLOD");
        if (lodRoot == null)
            throw new InvalidOperationException($"{patch.name} understory LOD root missing during grounding refinement.");

        foreach (Transform level in lodRoot)
        {
            if (!level.name.StartsWith("LOD", StringComparison.Ordinal))
                continue;
            foreach (Transform plant in level)
            {
                if (!plant.name.StartsWith("Plant_", StringComparison.Ordinal))
                    continue;
                Vector3 world = plant.position;
                world.y = targetWorldY;
                plant.position = world;
            }
        }
    }

    private static void ValidatePlantGrounding(Transform patch, float expectedWorldY, int treeIndex)
    {
        Transform lodRoot = patch.Find("UnderstoryLOD");
        if (lodRoot == null)
            throw new InvalidOperationException($"Tree {treeIndex} understory LOD root missing during grounding QA.");

        int count = 0;
        foreach (Transform level in lodRoot)
        {
            if (!level.name.StartsWith("LOD", StringComparison.Ordinal))
                continue;
            foreach (Transform plant in level)
            {
                if (!plant.name.StartsWith("Plant_", StringComparison.Ordinal))
                    continue;
                count++;
                AssertNear(plant.position.y, expectedWorldY, 0.002f,
                    $"tree {treeIndex} {level.name}/{plant.name} root grounding");
            }
        }
        if (count == 0)
            throw new InvalidOperationException($"Tree {treeIndex} has no understory plants to validate for grounding.");
    }

    private static void ValidateGeneratedRootClearance(QualityBlockArtSlot slot, Vector3 anchor, int treeIndex)
    {
        if (slot == null || slot.IsUsingAuthoredArt)
            return;
        if (slot.FallbackRoot == null)
            throw new InvalidOperationException($"Tree {treeIndex} generated fallback root missing during root-clearance QA.");

        Transform master = slot.FallbackRoot.transform.Find($"HD_TreeMaster_{treeIndex}");
        Transform rootAssembly = master?.Find("RootFlareAssembly");
        if (rootAssembly == null)
            throw new InvalidOperationException($"Tree {treeIndex} generated root-flare assembly missing.");

        MeshRenderer[] flares = rootAssembly.GetComponentsInChildren<MeshRenderer>(true)
            .Where(x => x.gameObject.name.StartsWith("RootFlare_", StringComparison.Ordinal))
            .OrderBy(x => x.gameObject.name, StringComparer.Ordinal)
            .ToArray();
        if (flares.Length != 7)
            throw new InvalidOperationException($"Tree {treeIndex} expected seven generated root flares, got {flares.Length}.");

        float maximumOuterRadius = 0f;
        foreach (MeshRenderer flare in flares)
        {
            Vector3 scale = flare.transform.lossyScale;
            Vector3 endpoint = flare.transform.position + flare.transform.up * Mathf.Abs(scale.y);
            float tipRadius = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)) * RootTipRadiusRatio;
            float outerRadius = HorizontalDistance(anchor, endpoint) + tipRadius;
            maximumOuterRadius = Mathf.Max(maximumOuterRadius, outerRadius);
        }

        float clearance = OpeningRadius - maximumOuterRadius;
        if (clearance < MinimumRootClearance - 0.004f)
            throw new InvalidOperationException(
                $"Tree {treeIndex} generated root flare approaches/intersects the pit edge: outerRadius={maximumOuterRadius:F4} m, " +
                $"opening={OpeningRadius:F4} m, clearance={clearance:F4} m, required>={MinimumRootClearance:F4} m.");
    }

    private static float CurbChordLength(float centerRadius)
    {
        return 2f * centerRadius * Mathf.Sin(Mathf.PI / EdgingModules);
    }

    private static bool ContainsXZ(Bounds bounds, Vector3 world)
    {
        const float epsilon = 0.001f;
        return world.x >= bounds.min.x - epsilon && world.x <= bounds.max.x + epsilon &&
               world.z >= bounds.min.z - epsilon && world.z <= bounds.max.z + epsilon;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private static Renderer FindSceneRenderer(string name)
    {
        GameObject go = FindSceneObject(name);
        return go != null ? go.GetComponent<Renderer>() : null;
    }

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(x => x.scene.IsValid() && x.name == name);
    }

    private static void OpenBenchmarkIfNeeded()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() || EditorSceneManager.GetActiveScene().path != BenchmarkScenePath)
            EditorSceneManager.OpenScene(BenchmarkScenePath, OpenSceneMode.Single);
    }

    private static bool Near(float actual, float expected, float tolerance)
    {
        return Mathf.Abs(actual - expected) <= tolerance;
    }

    private static void AssertNear(float actual, float expected, float tolerance, string label)
    {
        if (!Near(actual, expected, tolerance))
            throw new InvalidOperationException($"Vegetation root-zone {label} drift: {actual:F5}, expected {expected:F5} +/- {tolerance:F5}.");
    }

    private static void RequireText(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Vegetation root-zone contract missing {label}.");
    }

    private static void ValidateRange(float[] range, float min, float max, string label)
    {
        if (range == null || range.Length != 2 || range[0] < min || range[1] > max || range[0] > range[1])
            throw new InvalidOperationException($"Invalid {label}.");
    }

    [Serializable]
    private sealed class RootZoneContract
    {
        public bool runtimeRenderVerified;
        public string visualFidelityStatus;
        public SceneDatum sceneDatum;
        public RootFlareEnvelope rootFlareEnvelope;
        public PitAssemblySpec pitAssembly;
        public UnderstoryGroundingSpec understoryGrounding;
        public MaterialSpec[] materials;
        public QaSpec qa;
    }

    [Serializable]
    private sealed class SceneDatum
    {
        public int treeAnchorCount;
        public int maintainedPitCount;
        public int interiorPavedRingCount;
        public int pavingBoundaryRingCount;
        public int grassOnlyTreeCount;
        public bool noGameplayColliderChanges;
    }

    [Serializable]
    private sealed class RootFlareEnvelope
    {
        public int generatedRootFlaresPerTree;
        public float tipRadiusRatio;
        public float conservativeMaximumOuterRadiusMeters;
        public float minimumVisibleSoilClearanceToEdgingMeters;
        public float requiredOpeningRadiusMeters;
    }

    [Serializable]
    private sealed class PitAssemblySpec
    {
        public float openingRadiusMeters;
        public float openingDiameterMeters;
        public float soilTopVisualBiasAbovePavingMeters;
        public float soilVisualBottomEmbedBelowGrassMeters;
        public int curbModuleCount;
        public float curbRadialWidthMeters;
        public float curbHeightMeters;
        public float curbPavingEmbedMeters;
        public float curbGrassEmbedMeters;
        public float curbJointGapMeters;
        public string manufacture;
        public string mounting;
        public string interfacesGapsSeals;
        public string orientationExposure;
        public string aging;
        public string geometryVsMaterial;
        public string lodPolicy;
    }

    [Serializable]
    private sealed class UnderstoryGroundingSpec
    {
        public float pavedPitPlantRootAboveSoilMeters;
        public float grassPlantRootAboveGroundMeters;
    }

    [Serializable]
    private sealed class MaterialSpec
    {
        public string id;
        public string sourceAsset;
        public float[] albedoSrgbTargetRange;
        public float[] roughnessRange;
        public float[] metallicRange;
        public float dielectricF0Approx;
        public float normalScaleTarget;
        public string microstructure;
        public string wetness;
        public string uvAging;
        public string angularFresnel;
    }

    [Serializable]
    private sealed class QaSpec
    {
        public bool requireLookdevIllustration;
        public bool requireExactOpeningRadius;
        public bool requireRootFlareClearance;
        public bool requireLocalSupportGradeForEveryCurbModule;
        public bool requireNoSoilUndersideGapAtGrassBoundary;
        public bool requireUnderstorySurfaceGrounding;
        public bool requireNoGeneratedColliders;
        public bool requireFourPits;
        public bool requireThreeBoundaryRings;
    }
}

/// <summary>
/// Runtime evidence guard for the editor benchmark. Once the detailed tree/ground chain exists, any
/// MainCamera render of the benchmark must carry the repaired root-zone construction state. This is
/// not a visual score; it only prevents formal still/temporal evidence from silently rendering an
/// earlier ecology state with root/curb intersections or floating boundary modules.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockVegetationRootZonePreRenderGuard
{
    private static bool validating;

    static QualityBlockVegetationRootZonePreRenderGuard()
    {
        Camera.onPreCull -= OnPreCull;
        Camera.onPreCull += OnPreCull;
    }

    private static void OnPreCull(Camera camera)
    {
        if (validating || camera == null || !camera.gameObject.scene.IsValid() ||
            camera.gameObject.scene.path != QualityBlockVegetationRootZoneInterfaceQA.BenchmarkScenePath ||
            !camera.CompareTag("MainCamera"))
            return;

        bool detailedTreesReady = Resources.FindObjectsOfTypeAll<GameObject>()
            .Any(x => x.scene == camera.gameObject.scene && x.name.StartsWith("HD_TreeMaster_", StringComparison.Ordinal));
        bool detailedGroundReady = Resources.FindObjectsOfTypeAll<GameObject>()
            .Any(x => x.scene == camera.gameObject.scene && x.name == "GroundHighDetail");
        if (!detailedTreesReady || !detailedGroundReady)
            return;

        validating = true;
        try
        {
            QualityBlockVegetationRootZoneInterfaceQA.ValidateOpenScene();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "MainCamera render blocked: vegetation root-zone construction is not physically coherent with the prepared benchmark.", ex);
        }
        finally
        {
            validating = false;
        }
    }
}
