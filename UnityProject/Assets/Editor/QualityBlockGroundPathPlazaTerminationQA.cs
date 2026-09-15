using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Reconstructs the WornPathA -> DanchiPlaza mouth as a deliberate installed/used ground interface.
///
/// Source audit found two linked visual contradictions. WornPathA is a coarse 4.4 x 8.5 m Cube whose
/// top continues beneath DanchiPlaza and leaves a 0.70 m east strip visible beside the plaza after most
/// of the soil has already been hidden. Both 0.60 m-pitch precast edging runs also continue well past
/// the plaza's south edge. That can read as a partly buried rectangular primitive plus arbitrary curb
/// rails instead of a path that actually terminates at a paved residential plaza.
///
/// The gameplay Cube and its collider are preserved, but its renderer is disabled. A collider-free,
/// authored metric-UV soil surface replaces it visually, retaining the established path between the
/// curb runs and using the final 356 mm as an open, gently rising/tapering mouth. Seven whole precast
/// modules per side remain visible; the seven legacy continuation renderers per side are suppressed.
/// Existing manufactured curb meshes/materials/weathering metadata remain authoritative.
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
    private const string VisiblePathName = "HD_WornPathA_TerminatedSurface";
    private const string WestPrefix = "HD_Curb_WornPathWest_";
    private const string EastPrefix = "HD_Curb_WornPathEast_";
    private const string GeneratedRoot = "Assets/Art/GeneratedGroundInterfaces";
    private const string GeneratedMeshRoot = GeneratedRoot + "/Meshes";
    private const string GeneratedMaterialRoot = GeneratedRoot + "/Materials";
    private const string VisiblePathMeshPath = GeneratedMeshRoot + "/GM_HD_Interface_WornPathA_TerminatedSurface.asset";
    private const int FirstRetainedIndex = 0;
    private const int LastRetainedIndex = 6;
    private const int FirstSuppressedIndex = 7;
    private const int LastSuppressedIndex = 13;
    private const int PathLongitudinalSegments = 18;
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
            "WornPathA plaza-mouth correction persisted with authored metric-UV soil and terminated curb runs. " +
            "Visual Fidelity remains UNSCORED until native 4K pixels are reviewed.");
    }

    public static void ApplyToOpenScene()
    {
        ValidateContractConfigOnly();
        RequireQualityScene();
        GroundPathTerminationContract contract = LoadContract();

        GameObject curbRoot = FindSceneObject("HD_PrecastCurbAssembly");
        if (curbRoot == null)
            throw new InvalidOperationException("HD_PrecastCurbAssembly is required before path-termination correction.");

        Renderer sourcePath = RequireRenderer("WornPathA");
        Collider sourceCollider = sourcePath.GetComponent<Collider>();
        if (sourceCollider == null)
            throw new InvalidOperationException("WornPathA gameplay/collision substrate is missing its source collider.");
        if (sourcePath.sharedMaterial == null)
            throw new InvalidOperationException("WornPathA source material is missing before visual replacement.");

        // Preserve gameplay collision and source transform, remove only the coarse primitive from pixels.
        sourcePath.enabled = false;
        EditorUtility.SetDirty(sourcePath);

        int retainedWest = ApplySide(WestPrefix);
        int retainedEast = ApplySide(EastPrefix);

        GameObject previous = FindSceneObject(StateName);
        if (previous != null)
            UnityEngine.Object.DestroyImmediate(previous);

        var state = new GameObject(StateName);
        state.transform.position = Vector3.zero;
        state.transform.rotation = Quaternion.identity;
        state.transform.localScale = Vector3.one;
        state.transform.SetParent(curbRoot.transform, true);

        Directory.CreateDirectory(GeneratedMeshRoot);
        Directory.CreateDirectory(GeneratedMaterialRoot);
        Mesh visibleMesh = EnsureVisiblePathMesh(contract);
        Material visibleMaterial = EnsureVisiblePathMaterial(sourcePath.sharedMaterial, contract);
        GameObject visiblePath = CreateVisiblePathSurface(state.transform, visibleMesh, visibleMaterial);
        ConfigureVisiblePathWeathering(visiblePath);

        var manifest = state.AddComponent<QualityBlockGroundPathPlazaTerminationManifest>();
        manifest.Configure(
            retainedWest,
            retainedEast,
            LastSuppressedIndex - FirstSuppressedIndex + 1,
            LastSuppressedIndex - FirstSuppressedIndex + 1,
            contract.dimensions.terminationSetbackM,
            contract.dimensions.pathToCurbInnerGapM,
            contract.dimensions.pavingTopYM - contract.dimensions.legacySoilTopYM,
            contract.dimensions.pavingTopYM - contract.dimensions.pathMouthTopYM,
            visibleMesh.vertexCount,
            true);
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
        Renderer sourcePath = RequireRenderer("WornPathA");
        Bounds plazaBounds = plaza.bounds;
        Bounds sourcePathBounds = sourcePath.bounds;

        AssertNear(plazaBounds.min.z, contract.dimensions.plazaMinZM, PositionTolerance, "DanchiPlaza south edge");
        AssertNear(plazaBounds.max.x, contract.dimensions.plazaMaxXM, PositionTolerance, "DanchiPlaza east edge");
        AssertNear(plazaBounds.max.y, contract.dimensions.pavingTopYM, PositionTolerance, "DanchiPlaza top");
        AssertNear(sourcePathBounds.max.y, contract.dimensions.legacySoilTopYM, PositionTolerance, "legacy WornPathA top");

        if (contract.hardLimits.requireSourcePathRendererDisabled && sourcePath.enabled)
            throw new InvalidOperationException("Coarse WornPathA Cube renderer must be disabled in the prepared visual scene.");
        Collider sourceCollider = sourcePath.GetComponent<Collider>();
        if (contract.hardLimits.requireSourcePathColliderPreserved && (sourceCollider == null || !sourceCollider.enabled))
            throw new InvalidOperationException("WornPathA gameplay collider must remain present and enabled after visual replacement.");

        float legacyVerticalSeparation = plazaBounds.max.y - sourcePathBounds.max.y;
        if (legacyVerticalSeparation < contract.hardLimits.legacyPavingAboveSoilMinM - 0.001f ||
            legacyVerticalSeparation > contract.hardLimits.legacyPavingAboveSoilMaxM + 0.001f)
        {
            throw new InvalidOperationException(
                $"Legacy WornPathA/plaza vertical relationship drift: {legacyVerticalSeparation:F4} m; expected " +
                $"{contract.hardLimits.legacyPavingAboveSoilMinM:F3}-{contract.hardLimits.legacyPavingAboveSoilMaxM:F3} m.");
        }

        Renderer visiblePath = RequireRenderer(VisiblePathName);
        ValidateVisiblePathSurface(visiblePath, plazaBounds, contract);
        ValidateSide(WestPrefix, true, visiblePath.bounds, plazaBounds, contract);
        ValidateSide(EastPrefix, false, visiblePath.bounds, plazaBounds, contract);

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
        AssertNear(manifest.PathToCurbGapM, contract.dimensions.pathToCurbInnerGapM, 0.001f, "manifest nominal path-to-curb gap");
        AssertNear(manifest.LegacyPavingAboveSoilM, legacyVerticalSeparation, 0.001f, "manifest legacy paving/soil separation");
        AssertNear(manifest.MouthBelowPavingM,
            contract.dimensions.pavingTopYM - contract.dimensions.pathMouthTopYM, 0.001f, "manifest mouth/paving separation");
        if (manifest.VisiblePathVertexCount != visiblePath.GetComponent<MeshFilter>().sharedMesh.vertexCount)
            throw new InvalidOperationException("Visible path mesh vertex-count manifest drift.");
        if (!manifest.SourcePathRendererDisabled)
            throw new InvalidOperationException("Manifest does not record source path renderer suppression.");

        Collider[] correctionColliders = state.GetComponentsInChildren<Collider>(true);
        if (contract.hardLimits.requireNoColliderOnCorrectionState && correctionColliders.Length != 0)
            throw new InvalidOperationException($"Correction state must not alter gameplay collision; found {correctionColliders.Length} collider(s).");

        Debug.Log(
            "WornPathA plaza termination validation passed structurally: coarse source renderer suppressed while collider is retained; " +
            "authored metric-UV soil ends at the plaza edge; 7 visible whole curb modules and 7 suppressed legacy continuation modules per side; " +
            "approximately 356 mm open mouth; dry dielectric soil/concrete materials. Actual grounding, silhouette, texture stability and material " +
            "response remain render-unverified and Visual Fidelity remains UNSCORED.");
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
        if (contract.assembly == null || contract.dimensions == null || contract.curbMaterial == null ||
            contract.soilMaterial == null || contract.lodPolicy == null || contract.hardLimits == null || contract.researchBasis == null)
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
        if (contract.assembly.components == null || contract.assembly.components.Length < 5)
            throw new InvalidOperationException("Path-termination assembly must enumerate physical/source components.");

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
            throw new InvalidOperationException("Nominal path-to-curb interface gap is outside its hard range.");
        if (contract.dimensions.legacyWestCurbIntrusionIntoPlazaM < 0.20f)
            throw new InvalidOperationException("Contract no longer demonstrates the legacy west-curb/plaza intrusion being prevented.");
        if (contract.dimensions.legacyVisibleEastSoilStripWidthM < 0.50f)
            throw new InvalidOperationException("Contract no longer records the source east soil-strip contradiction being replaced.");
        if (contract.dimensions.pathMouthEastXM > contract.dimensions.plazaMaxXM ||
            contract.dimensions.pathMouthWestXM >= contract.dimensions.pathMouthEastXM)
            throw new InvalidOperationException("Authored path mouth must terminate within the plaza edge envelope.");

        ValidateMaterialSpec(contract.curbMaterial, contract.hardLimits.maxMetallic, "curbMaterial");
        ValidateSoilMaterialSpec(contract.soilMaterial, contract.hardLimits.maxMetallic);

        RequireText(contract.lodPolicy.lod0, "lodPolicy.lod0");
        RequireText(contract.lodPolicy.lod1, "lodPolicy.lod1");
        RequireText(contract.lodPolicy.lod2, "lodPolicy.lod2");
        RequireText(contract.lodPolicy.lod3, "lodPolicy.lod3");
        RequireText(contract.lodPolicy.transitionPolicy, "lodPolicy.transitionPolicy");
        RequireText(contract.researchBasis.source, "researchBasis.source");
        RequireText(contract.researchBasis.referenceUse, "researchBasis.referenceUse");
        RequireText(contract.researchBasis.sourceUrl, "researchBasis.sourceUrl");
        if (contract.evidencePlan == null || contract.evidencePlan.Length < 5)
            throw new InvalidOperationException("Path-termination contract must define multi-angle plus temporal evidence plans.");
        if (contract.criticalFailPrevention == null || contract.criticalFailPrevention.Length < 7)
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

    private static Mesh EnsureVisiblePathMesh(GroundPathTerminationContract contract)
    {
        Mesh generated = BuildVisiblePathMesh(contract);
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(VisiblePathMeshPath);
        if (existing == null)
        {
            generated.name = "GM_HD_Interface_WornPathA_TerminatedSurface";
            AssetDatabase.CreateAsset(generated, VisiblePathMeshPath);
            return generated;
        }

        EditorUtility.CopySerialized(generated, existing);
        UnityEngine.Object.DestroyImmediate(generated);
        existing.name = "GM_HD_Interface_WornPathA_TerminatedSurface";
        EditorUtility.SetDirty(existing);
        return existing;
    }

    private static Mesh BuildVisiblePathMesh(GroundPathTerminationContract contract)
    {
        var vertices = new List<Vector3>((PathLongitudinalSegments + 1) * 3);
        var uv = new List<Vector2>((PathLongitudinalSegments + 1) * 3);
        var triangles = new List<int>(PathLongitudinalSegments * 12);

        float southZ = contract.dimensions.pathSouthZM;
        float northZ = contract.dimensions.plazaMinZM;
        float mouthStartZ = contract.dimensions.lastRetainedModuleMaxZM;
        float fullLength = northZ - southZ;
        if (fullLength <= 0.5f)
            throw new InvalidOperationException("Authored WornPathA surface has invalid longitudinal extent.");

        for (int i = 0; i <= PathLongitudinalSegments; i++)
        {
            float t = i / (float)PathLongitudinalSegments;
            float z = Mathf.Lerp(southZ, northZ, t);
            float mouthT = z <= mouthStartZ ? 0f : Mathf.InverseLerp(mouthStartZ, northZ, z);
            float preMouthT = Mathf.Clamp01(Mathf.InverseLerp(southZ, mouthStartZ, z));

            // Small deterministic inward edge wear breaks the broad source rectangle but fades to zero
            // at both the south datum and the manufactured curb termination, keeping interfaces stable.
            float envelope = Mathf.Sin(preMouthT * Mathf.PI);
            envelope *= envelope;
            float westInset = mouthT > 0f ? 0f : envelope * (0.006f + 0.005f * (0.5f + 0.5f * Mathf.Sin(preMouthT * 19.0f)));
            float eastInset = mouthT > 0f ? 0f : envelope * (0.005f + 0.006f * (0.5f + 0.5f * Mathf.Sin(preMouthT * 17.0f + 1.2f)));

            float westX = mouthT > 0f
                ? Mathf.Lerp(contract.dimensions.pathSouthWestXM, contract.dimensions.pathMouthWestXM, mouthT)
                : contract.dimensions.pathSouthWestXM + westInset;
            float eastX = mouthT > 0f
                ? Mathf.Lerp(contract.dimensions.pathSouthEastXM, contract.dimensions.pathMouthEastXM, mouthT)
                : contract.dimensions.pathSouthEastXM - eastInset;
            if (eastX - westX < 2.5f)
                throw new InvalidOperationException($"Authored WornPathA mouth collapsed at row {i}: width={eastX - westX:F3} m.");

            float edgeY = Mathf.Lerp(contract.dimensions.legacySoilTopYM, contract.dimensions.pathMouthTopYM, mouthT);
            float centerDepression = Mathf.Lerp(0.0022f, 0f, mouthT) * (0.85f + 0.15f * Mathf.Sin(t * Mathf.PI * 5f));
            float centerX = (westX + eastX) * 0.5f;

            vertices.Add(new Vector3(westX, edgeY, z));
            vertices.Add(new Vector3(centerX, edgeY - centerDepression, z));
            vertices.Add(new Vector3(eastX, edgeY, z));

            // UV0 is in world metres. The cloned material uses repeats-per-metre scale, preserving
            // physical grain scale through the taper instead of stretching a normalized 0..1 texture.
            uv.Add(new Vector2(westX, z));
            uv.Add(new Vector2(centerX, z));
            uv.Add(new Vector2(eastX, z));
        }

        for (int i = 0; i < PathLongitudinalSegments; i++)
        {
            int row0 = i * 3;
            int row1 = (i + 1) * 3;
            for (int lane = 0; lane < 2; lane++)
            {
                int a = row0 + lane;
                int b = row0 + lane + 1;
                int c = row1 + lane + 1;
                int d = row1 + lane;
                // Winding selected for +Y-facing top surface.
                triangles.Add(a); triangles.Add(d); triangles.Add(c);
                triangles.Add(a); triangles.Add(c); triangles.Add(b);
            }
        }

        var mesh = new Mesh();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uv);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Material EnsureVisiblePathMaterial(Material source, GroundPathTerminationContract contract)
    {
        string path = contract.soilMaterial.generatedAssetPath;
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(source) { name = "MAT_WornPathA_TerminatedSurface" };
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.CopyPropertiesFromMaterial(source);
            material.shader = source.shader;
            material.name = "MAT_WornPathA_TerminatedSurface";
        }

        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0f);

        SetMetricTextureTransform(material, "_MainTex", contract.soilMaterial.macroTileM);
        SetMetricTextureTransform(material, "_BumpMap", contract.soilMaterial.macroTileM);
        SetMetricTextureTransform(material, "_MetallicGlossMap", contract.soilMaterial.macroTileM);
        SetMetricTextureTransform(material, "_DetailAlbedoMap", contract.soilMaterial.detailTileM);
        SetMetricTextureTransform(material, "_DetailNormalMap", contract.soilMaterial.detailTileM);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void SetMetricTextureTransform(Material material, string property, float tileMeters)
    {
        if (!material.HasProperty(property) || material.GetTexture(property) == null)
            return;
        float repeatsPerMetre = 1f / Mathf.Max(0.001f, tileMeters);
        material.SetTextureScale(property, new Vector2(repeatsPerMetre, repeatsPerMetre));
        material.SetTextureOffset(property, Vector2.zero);
    }

    private static GameObject CreateVisiblePathSurface(Transform parent, Mesh mesh, Material material)
    {
        var go = new GameObject(VisiblePathName);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        go.isStatic = true;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.receiveShadows = true;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    private static void ConfigureVisiblePathWeathering(GameObject go)
    {
        QualityBlockWeatheringSurface metadata = go.GetComponent<QualityBlockWeatheringSurface>();
        if (metadata == null)
            metadata = go.AddComponent<QualityBlockWeatheringSurface>();
        metadata.Configure(
            NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.SunExposed |
            NewTownSurfaceExposure.GroundContact | NewTownSurfaceExposure.UpwardFacing,
            NewTownStainSource.FootTraffic | NewTownStainSource.UVExposure | NewTownStainSource.GroundSplash,
            1f, 0.92f, 0.42f, 0.88f);
    }

    private static void ValidateVisiblePathSurface(Renderer renderer, Bounds plazaBounds, GroundPathTerminationContract contract)
    {
        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null)
            throw new InvalidOperationException("Authored WornPathA replacement mesh is missing.");
        Mesh mesh = filter.sharedMesh;
        if (!mesh.name.StartsWith(contract.hardLimits.requireAuthoredMeshPrefix, StringComparison.Ordinal))
            throw new InvalidOperationException($"Visible WornPathA replacement uses non-authored/primitive mesh {mesh.name}.");
        if (renderer.GetComponent<Collider>() != null)
            throw new InvalidOperationException("Authored WornPathA visual surface must remain collider-free.");
        if (contract.hardLimits.requireWeatheringMetadataOnVisibleSoil &&
            renderer.GetComponent<QualityBlockWeatheringSurface>() == null)
            throw new InvalidOperationException("Authored WornPathA visual surface lacks cause-based weathering metadata.");

        Vector3[] vertices = mesh.vertices;
        Vector2[] uvs = mesh.uv;
        if (vertices == null || uvs == null || vertices.Length != uvs.Length || vertices.Length < 45)
            throw new InvalidOperationException("Authored WornPathA mesh lacks sufficient metric-UV geometric evidence.");

        float maxWorldZ = float.NegativeInfinity;
        float mouthTopY = float.NegativeInfinity;
        float minU = float.PositiveInfinity;
        float maxU = float.NegativeInfinity;
        float minV = float.PositiveInfinity;
        float maxV = float.NegativeInfinity;
        float upwardNormalSum = 0f;
        Vector3[] normals = mesh.normals;
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 world = renderer.transform.TransformPoint(vertices[i]);
            maxWorldZ = Mathf.Max(maxWorldZ, world.z);
            if (Mathf.Abs(world.z - contract.dimensions.plazaMinZM) <= 0.010f)
                mouthTopY = Mathf.Max(mouthTopY, world.y);
            minU = Mathf.Min(minU, uvs[i].x);
            maxU = Mathf.Max(maxU, uvs[i].x);
            minV = Mathf.Min(minV, uvs[i].y);
            maxV = Mathf.Max(maxV, uvs[i].y);
            if (normals != null && normals.Length == vertices.Length)
                upwardNormalSum += normals[i].y;
        }

        if (contract.hardLimits.requireNoVisiblePathBeyondPlazaEdge && maxWorldZ > plazaBounds.min.z + 0.002f)
            throw new InvalidOperationException(
                $"Authored WornPathA extends beyond the plaza edge: maxZ={maxWorldZ:F4}, plazaMinZ={plazaBounds.min.z:F4}.");
        AssertNear(maxWorldZ, contract.dimensions.plazaMinZM, 0.002f, "authored WornPathA terminal Z");
        AssertNear(mouthTopY, contract.dimensions.pathMouthTopYM, 0.002f, "authored WornPathA mouth top Y");
        float mouthBelowPaving = plazaBounds.max.y - mouthTopY;
        if (mouthBelowPaving < contract.hardLimits.mouthBelowPavingMinM ||
            mouthBelowPaving > contract.hardLimits.mouthBelowPavingMaxM)
            throw new InvalidOperationException(
                $"Authored WornPathA mouth/paving separation={mouthBelowPaving:F4} m outside " +
                $"{contract.hardLimits.mouthBelowPavingMinM:F4}-{contract.hardLimits.mouthBelowPavingMaxM:F4} m.");

        float uvSpanX = maxU - minU;
        float uvSpanZ = maxV - minV;
        if (Mathf.Abs(uvSpanX - renderer.bounds.size.x) > 0.05f ||
            Mathf.Abs(uvSpanZ - renderer.bounds.size.z) > 0.05f)
            throw new InvalidOperationException(
                $"Authored WornPathA UV0 is not metre-scaled: uvSpan=({uvSpanX:F3},{uvSpanZ:F3}), " +
                $"bounds=({renderer.bounds.size.x:F3},{renderer.bounds.size.z:F3}).");
        if (upwardNormalSum <= vertices.Length * 0.75f)
            throw new InvalidOperationException("Authored WornPathA top normals are not consistently upward-facing.");

        ValidateSoilMaterial(renderer.sharedMaterial, contract);
    }

    private static void ValidateSide(string prefix, bool west, Bounds visiblePathBounds, Bounds plazaBounds,
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
                        $"Active curb module {go.name} continues beyond the path/plaza termination datum: " +
                        $"maxZ={renderer.bounds.max.z:F3}, plazaMinZ={plazaBounds.min.z:F3}.");
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
            ? visiblePathBounds.min.x - datum.bounds.max.x
            : datum.bounds.min.x - visiblePathBounds.max.x;
        if (interfaceGap < contract.hardLimits.pathToCurbGapMinM || interfaceGap > contract.hardLimits.pathToCurbGapMaxM)
            throw new InvalidOperationException(
                $"{prefix} path interface gap={interfaceGap:F4} m outside " +
                $"{contract.hardLimits.pathToCurbGapMinM:F3}-{contract.hardLimits.pathToCurbGapMaxM:F3} m.");

        Renderer firstSuppressed = RequireRenderer(prefix + FirstSuppressedIndex.ToString("00"));
        float continuationBeyondEdge = firstSuppressed.bounds.max.z - plazaBounds.min.z;
        if (continuationBeyondEdge < 0.20f)
            throw new InvalidOperationException(
                $"{prefix} first suppressed module no longer matches the documented legacy continuation; observed {continuationBeyondEdge:F3} m. Re-audit instead of blindly suppressing it.");
        AssertNear(continuationBeyondEdge, contract.dimensions.legacyWestCurbIntrusionIntoPlazaM,
            PositionTolerance, $"{prefix} documented legacy continuation beyond plaza edge");
    }

    private static void ValidateConcreteMaterial(Renderer renderer, GroundPathTerminationContract contract)
    {
        Material material = renderer.sharedMaterial;
        if (material == null)
            throw new InvalidOperationException($"Curb renderer {renderer.name} has no material.");
        if (!string.Equals(AssetDatabase.GetAssetPath(material), contract.curbMaterial.assetPath, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Curb renderer {renderer.name} material drift: {AssetDatabase.GetAssetPath(material)}; expected {contract.curbMaterial.assetPath}.");
        if (material.shader == null || material.shader.name != "Standard")
            throw new InvalidOperationException($"Curb renderer {renderer.name} must use the Standard PBR fallback shader.");
        if (!material.HasProperty("_Metallic") || material.GetFloat("_Metallic") > contract.hardLimits.maxMetallic)
            throw new InvalidOperationException($"Materially impossible metallic concrete on {renderer.name}.");
        if (!material.IsKeywordEnabled("_NORMALMAP") || material.GetTexture("_BumpMap") == null)
            throw new InvalidOperationException($"Curb renderer {renderer.name} is missing physical micro-normal response.");
        if (!material.IsKeywordEnabled("_METALLICGLOSSMAP") || material.GetTexture("_MetallicGlossMap") == null)
            throw new InvalidOperationException($"Curb renderer {renderer.name} is missing the roughness/smoothness mask path.");
    }

    private static void ValidateSoilMaterial(Material material, GroundPathTerminationContract contract)
    {
        if (material == null)
            throw new InvalidOperationException("Authored WornPathA soil material is missing.");
        if (!string.Equals(AssetDatabase.GetAssetPath(material), contract.soilMaterial.generatedAssetPath, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Authored WornPathA soil material path drift: {AssetDatabase.GetAssetPath(material)}; expected {contract.soilMaterial.generatedAssetPath}.");
        if (material.shader == null || material.shader.name != "Standard")
            throw new InvalidOperationException("Authored WornPathA soil must use the Standard PBR fallback shader.");
        if (!material.HasProperty("_Metallic") || material.GetFloat("_Metallic") > contract.hardLimits.maxMetallic)
            throw new InvalidOperationException("Materially impossible metallic compacted soil on authored WornPathA.");
        if (!material.IsKeywordEnabled("_NORMALMAP") || material.GetTexture("_BumpMap") == null ||
            !material.IsKeywordEnabled("_METALLICGLOSSMAP") || material.GetTexture("_MetallicGlossMap") == null ||
            !material.IsKeywordEnabled("_DETAIL_MULX2") || material.GetTexture("_DetailAlbedoMap") == null ||
            material.GetTexture("_DetailNormalMap") == null)
            throw new InvalidOperationException("Authored WornPathA soil is missing required macro/detail PBR texture bindings.");

        AssertTextureScale(material, "_MainTex", 1f / contract.soilMaterial.macroTileM, 0.01f);
        AssertTextureScale(material, "_BumpMap", 1f / contract.soilMaterial.macroTileM, 0.01f);
        AssertTextureScale(material, "_MetallicGlossMap", 1f / contract.soilMaterial.macroTileM, 0.01f);
        AssertTextureScale(material, "_DetailAlbedoMap", 1f / contract.soilMaterial.detailTileM, 0.03f);
        AssertTextureScale(material, "_DetailNormalMap", 1f / contract.soilMaterial.detailTileM, 0.03f);
    }

    private static void AssertTextureScale(Material material, string property, float expected, float tolerance)
    {
        Vector2 scale = material.GetTextureScale(property);
        if (Mathf.Abs(scale.x - expected) > tolerance || Mathf.Abs(scale.y - expected) > tolerance)
            throw new InvalidOperationException(
                $"Physical texture scale drift on {material.name}/{property}: got {scale}, expected {expected:F4} repeats per metre-UV unit.");
    }

    private static void ValidateMaterialSpec(MaterialSpec material, float maxMetallic, string label)
    {
        RequireText(material.id, label + ".id");
        RequireText(material.assetPath, label + ".assetPath");
        RequireText(material.microstructure, label + ".microstructure");
        RequireText(material.wetResponse, label + ".wetResponse");
        RequireText(material.uvAging, label + ".uvAging");
        RequireText(material.angularFresnelResponse, label + ".angularFresnelResponse");
        if (material.albedoSrgb == null || material.albedoSrgb.Length != 3)
            throw new InvalidOperationException(label + " albedo must contain three channels.");
        if (material.metallic > maxMetallic)
            throw new InvalidOperationException(label + " cannot be materially metallic.");
        if (material.specularF0 < 0.02f || material.specularF0 > 0.08f)
            throw new InvalidOperationException(label + " dielectric F0 is outside a plausible fallback range.");
        if (material.roughnessNominal < material.roughnessAllowedMin || material.roughnessNominal > material.roughnessAllowedMax)
            throw new InvalidOperationException(label + " nominal roughness is outside declared bounds.");
        if (material.wetness != 0f)
            throw new InvalidOperationException("Current dry midsummer benchmark must not silently declare a wet " + label + " state.");
    }

    private static void ValidateSoilMaterialSpec(SoilMaterialSpec material, float maxMetallic)
    {
        RequireText(material.id, "soilMaterial.id");
        RequireText(material.sourceObject, "soilMaterial.sourceObject");
        RequireText(material.generatedAssetPath, "soilMaterial.generatedAssetPath");
        RequireText(material.microstructure, "soilMaterial.microstructure");
        RequireText(material.uvAging, "soilMaterial.uvAging");
        RequireText(material.angularFresnelResponse, "soilMaterial.angularFresnelResponse");
        if (material.albedoReferenceSrgb == null || material.albedoReferenceSrgb.Length != 3)
            throw new InvalidOperationException("soilMaterial albedo reference must contain three channels.");
        if (material.metallic > maxMetallic)
            throw new InvalidOperationException("Compacted soil cannot be materially metallic.");
        if (material.specularF0 < 0.02f || material.specularF0 > 0.08f)
            throw new InvalidOperationException("Compacted-soil dielectric F0 is outside a plausible fallback range.");
        if (material.roughnessMin < 0.75f || material.roughnessMax > 1f || material.roughnessMin > material.roughnessMax)
            throw new InvalidOperationException("Compacted-soil roughness bounds are implausible.");
        if (material.macroTileM < 1f || material.detailTileM < 0.05f || material.detailTileM > 0.8f)
            throw new InvalidOperationException("Compacted-soil physical texture scales are implausible.");
        if (material.wetness != 0f)
            throw new InvalidOperationException("Current dry midsummer benchmark must not silently declare wet soil.");
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
        public MaterialSpec curbMaterial;
        public SoilMaterialSpec soilMaterial;
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
        public float legacySoilTopYM;
        public float pavingTopYM;
        public float pathSouthZM;
        public float pathSouthWestXM;
        public float pathSouthEastXM;
        public float pathMouthWestXM;
        public float pathMouthEastXM;
        public float pathMouthTopYM;
        public float pathToCurbInnerGapM;
        public int lastRetainedModuleIndex;
        public float lastRetainedModuleMaxZM;
        public float plazaMinZM;
        public float plazaMaxXM;
        public float terminationSetbackM;
        public int firstSuppressedModuleIndex;
        public float firstSuppressedModuleMaxZM;
        public float legacyWestCurbIntrusionIntoPlazaM;
        public float legacyVisibleEastSoilStripWidthM;
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
    private sealed class SoilMaterialSpec
    {
        public string id;
        public string sourceObject;
        public string generatedAssetPath;
        public float[] albedoReferenceSrgb;
        public float roughnessMin;
        public float roughnessMax;
        public float metallic;
        public float specularF0;
        public float normalScale;
        public float macroTileM;
        public float detailTileM;
        public string microstructure;
        public float wetness;
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
        public float legacyPavingAboveSoilMinM;
        public float legacyPavingAboveSoilMaxM;
        public float mouthBelowPavingMinM;
        public float mouthBelowPavingMaxM;
        public float maxMetallic;
        public string requireAuthoredMeshPrefix;
        public bool requireSourcePathRendererDisabled;
        public bool requireSourcePathColliderPreserved;
        public bool requireNoVisiblePathBeyondPlazaEdge;
        public bool requireNoActiveCurbInsidePlaza;
        public bool requireNoColliderOnCorrectionState;
        public bool requireWeatheringMetadataOnRetainedModules;
        public bool requireWeatheringMetadataOnVisibleSoil;
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
    [SerializeField] private float legacyPavingAboveSoilM;
    [SerializeField] private float mouthBelowPavingM;
    [SerializeField] private int visiblePathVertexCount;
    [SerializeField] private bool sourcePathRendererDisabled;

    public int RetainedWest => retainedWest;
    public int RetainedEast => retainedEast;
    public int SuppressedWest => suppressedWest;
    public int SuppressedEast => suppressedEast;
    public float TerminationSetbackM => terminationSetbackM;
    public float PathToCurbGapM => pathToCurbGapM;
    public float LegacyPavingAboveSoilM => legacyPavingAboveSoilM;
    public float MouthBelowPavingM => mouthBelowPavingM;
    public int VisiblePathVertexCount => visiblePathVertexCount;
    public bool SourcePathRendererDisabled => sourcePathRendererDisabled;

    public void Configure(int visibleWest, int visibleEast, int hiddenWest, int hiddenEast,
        float setback, float interfaceGap, float legacyVerticalSeparation, float mouthClearance,
        int pathVertexCount, bool sourceRendererDisabled)
    {
        retainedWest = visibleWest;
        retainedEast = visibleEast;
        suppressedWest = hiddenWest;
        suppressedEast = hiddenEast;
        terminationSetbackM = setback;
        pathToCurbGapM = interfaceGap;
        legacyPavingAboveSoilM = legacyVerticalSeparation;
        mouthBelowPavingM = mouthClearance;
        visiblePathVertexCount = pathVertexCount;
        sourcePathRendererDisabled = sourceRendererDisabled;
    }
}