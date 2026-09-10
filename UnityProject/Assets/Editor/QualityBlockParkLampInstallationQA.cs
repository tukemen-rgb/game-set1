using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Reconstructs the benchmark-visible park lamp as an installed assembly rather than two stacked
/// concrete cylinders plus decorative solids. The pass creates a continuous tapered concrete pole,
/// uses a lower material submesh for causal grade moisture (no protruding fake dirt ring), adds a
/// curved weather-sealed service cover, and makes the pole-top adapter / globe retainers physically
/// continuous with the existing topology-aware PhysicalDiffuser.
///
/// This is source/runtime construction QA only. It never awards Visual Fidelity points; native
/// 3840x2160 stills, 100% crops and temporal evidence remain mandatory.
/// </summary>
public static class QualityBlockParkLampInstallationQA
{
    private const string ContractPath = "Assets/QA/park_lamp_installation_contract.json";
    private const string LookdevPath = "Assets/QA/park_lamp_installation_lookdev.svg";
    private const string MeshRoot = "Assets/Art/GeneratedParkFurnitureMeshes";
    private const string MaterialRoot = "Assets/Art/GeneratedParkFurnitureMaterials";

    private const float PoleBottomY = -0.080f;
    private const float PoleTopY = 3.920f;
    private const float PoleGradeRadius = 0.095f;
    private const float PoleTopRadius = 0.0625f;
    private const float MoistureTopY = 0.200f;

    private const float ServiceCenterY = 0.860f;
    private const float GasketHeight = 0.280f;
    private const float CoverHeight = 0.260f;
    private const float GasketHalfAngleDeg = 33.0f;
    private const float CoverHalfAngleDeg = 30.0f;
    private const float GasketBaseOffset = 0.0005f;
    private const float GasketThickness = 0.0015f;
    private const float CoverBaseOffset = 0.0015f;
    private const float CoverThickness = 0.0030f;

    private const float AdapterBottomY = 3.918f;
    private const float AdapterTopY = 4.267f;
    private const float LowerRetainerBottomY = 4.265f;
    private const float LowerRetainerTopY = 4.310f;
    private const float DiffuserBottomY = 4.305f;
    private const float DiffuserTopY = 4.560f;
    private const float UpperRetainerBottomY = 4.555f;
    private const float UpperRetainerTopY = 4.580f;
    private const float WeatherCapBottomY = 4.580f;
    private const float WeatherCapTopY = 4.596f;

    private static readonly int[] PoleSides = { 32, 24, 16, 12 };
    private static readonly int[] CoverSegments = { 12, 10, 8, 6 };

    private static readonly string[] LegacyRendererNames =
    {
        "PoleLower", "PoleUpper", "GroundMoistureBand", "Neck", "Cap", "Diffuser", "DiffuserCrown"
    };

    private static readonly string[] PersistentNames =
    {
        "PhysicalPole", "LampServiceGasket", "LampServiceCover", "LampPoleTopBoot",
        "LampAdapterStem", "LampLowerRetainer", "PhysicalDiffuser", "LampUpperRetainer", "LampWeatherCap"
    };

    [MenuItem("NewTown/Quality/Reconstruct Park Lamp Installation")]
    public static void ApplyToOpenScene()
    {
        ValidateContractConfigOnly();
        GameObject lamp = GameObject.Find("HD_Lamp");
        if (lamp == null)
            throw new InvalidOperationException("HD_Lamp missing; build/refine park furniture before lamp installation reconstruction.");

        Material concrete = RequireMaterial("PBR_ParkPrecastConcrete");
        Material contactConcrete = RequireMaterial("PBR_ParkContactConcrete");
        Material paintedSteel = RequireMaterial("PBR_ParkPaintedSteel");
        Material exposedSteel = RequireMaterial("PBR_ParkExposedSteel");
        Material epdm = EnsureEpdmMaterial();

        Directory.CreateDirectory(MeshRoot);

        for (int lod = 0; lod < 4; lod++)
        {
            Transform tier = lamp.transform.Find($"LOD{lod}");
            if (tier == null)
                throw new InvalidOperationException($"HD_Lamp/LOD{lod} missing.");

            DisableLegacyRenderers(tier);
            DestroyGeneratedChildren(tier);

            GameObject pole = new GameObject("PhysicalPole");
            pole.transform.SetParent(tier, false);
            MeshFilter poleMf = pole.AddComponent<MeshFilter>();
            poleMf.sharedMesh = GetTaperedPoleMesh(lod, PoleSides[lod]);
            MeshRenderer poleMr = pole.AddComponent<MeshRenderer>();
            // Submesh 0 is the same concrete substrate in the grade-contact moisture state; submesh 1 is dry concrete.
            poleMr.sharedMaterials = new[] { contactConcrete, concrete };

            float gasketBottomRadius = PoleRadiusAtY(ServiceCenterY - GasketHeight * 0.5f) + GasketBaseOffset;
            float gasketTopRadius = PoleRadiusAtY(ServiceCenterY + GasketHeight * 0.5f) + GasketBaseOffset;
            CreateCurvedShell(
                "LampServiceGasket", tier, ServiceCenterY,
                GetCurvedShellMesh("ServiceGasket", lod, gasketBottomRadius, gasketTopRadius, GasketHeight,
                    GasketHalfAngleDeg, GasketThickness, CoverSegments[lod]), epdm);

            float coverBottomRadius = PoleRadiusAtY(ServiceCenterY - CoverHeight * 0.5f) + CoverBaseOffset;
            float coverTopRadius = PoleRadiusAtY(ServiceCenterY + CoverHeight * 0.5f) + CoverBaseOffset;
            CreateCurvedShell(
                "LampServiceCover", tier, ServiceCenterY,
                GetCurvedShellMesh("ServiceCover", lod, coverBottomRadius, coverTopRadius, CoverHeight,
                    CoverHalfAngleDeg, CoverThickness, CoverSegments[lod]), paintedSteel);

            CreatePipe("LampPoleTopBoot", tier, 0.142f, 0.030f, 3.935f, paintedSteel);
            CreatePipe("LampAdapterStem", tier, 0.065f, AdapterTopY - AdapterBottomY,
                (AdapterTopY + AdapterBottomY) * 0.5f, paintedSteel);
            CreatePipe("LampLowerRetainer", tier, 0.235f, LowerRetainerTopY - LowerRetainerBottomY,
                (LowerRetainerTopY + LowerRetainerBottomY) * 0.5f, paintedSteel);
            CreatePipe("LampUpperRetainer", tier, 0.235f, UpperRetainerTopY - UpperRetainerBottomY,
                (UpperRetainerTopY + UpperRetainerBottomY) * 0.5f, paintedSteel);
            CreatePipe("LampWeatherCap", tier, 0.270f, WeatherCapTopY - WeatherCapBottomY,
                (WeatherCapTopY + WeatherCapBottomY) * 0.5f, paintedSteel);

            if (lod == 0)
                CreateServiceFasteners(tier, exposedSteel);
        }

        AssetDatabase.SaveAssets();
        Debug.Log(
            "Park lamp installation reconstruction applied before furniture LOD rebind: continuous tapered concrete pole, " +
            "causal flush moisture submesh, curved sealed service cover, inserted top adapter and retained globe head. " +
            "Visual Fidelity remains UNSCORED until native render evidence exists.");
    }

    [MenuItem("NewTown/QA/Validate Park Lamp Installation")]
    public static void ValidateOpenScene()
    {
        ValidateContractConfigOnly();
        GameObject lamp = GameObject.Find("HD_Lamp");
        if (lamp == null)
            throw new InvalidOperationException("Park-lamp QA requires HD_Lamp.");

        LODGroup group = lamp.GetComponent<LODGroup>();
        LOD[] lods = group != null ? group.GetLODs() : null;
        if (lods == null || lods.Length != 4)
            throw new InvalidOperationException("HD_Lamp must have exactly four rebound LODs.");
        if (!group.animateCrossFading || group.fadeMode != LODFadeMode.CrossFade)
            throw new InvalidOperationException("HD_Lamp must use animated cross-fade.");

        int colliderCount = lamp.GetComponentsInChildren<Collider>(true).Length;
        if (colliderCount != 0)
            throw new InvalidOperationException($"Generated HD_Lamp visual shell must remain collider-free; found {colliderCount}.");

        foreach (Light light in lamp.GetComponentsInChildren<Light>(true))
            if (light.enabled && light.intensity > 0.001f)
                throw new InvalidOperationException($"Daytime park lamp cannot emit light in benchmark evidence: {light.name} intensity={light.intensity}.");

        Material concrete = RequireMaterial("PBR_ParkPrecastConcrete");
        Material contactConcrete = RequireMaterial("PBR_ParkContactConcrete");
        Material paintedSteel = RequireMaterial("PBR_ParkPaintedSteel");
        Material exposedSteel = RequireMaterial("PBR_ParkExposedSteel");
        Material diffuser = RequireMaterial("PBR_LampDiffuserAged");
        Material epdm = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialRoot}/PBR_LampEPDM.mat");
        if (epdm == null)
            throw new InvalidOperationException("PBR_LampEPDM material missing.");

        AssertDielectric(concrete, "lamp concrete");
        AssertDielectric(contactConcrete, "lamp contact concrete");
        AssertDielectric(paintedSteel, "lamp painted steel");
        AssertDielectric(diffuser, "lamp diffuser");
        AssertDielectric(epdm, "lamp EPDM");
        if (1f - epdm.GetFloat("_Glossiness") < 0.85f)
            throw new InvalidOperationException("Lamp EPDM must remain matte (roughness >= 0.85).");
        if (exposedSteel.HasProperty("_Metallic") && exposedSteel.GetFloat("_Metallic") < 0.95f)
            throw new InvalidOperationException(
                "Lamp service-cover fasteners use exposed steel and must be fully metallic after the authoritative park microdetail pass.");
        if (diffuser.IsKeywordEnabled("_EMISSION") && diffuser.HasProperty("_EmissionColor") &&
            diffuser.GetColor("_EmissionColor").maxColorComponent > 0.001f)
            throw new InvalidOperationException("Daytime aged park diffuser must not use emissive brightness.");

        for (int lod = 0; lod < 4; lod++)
        {
            Transform tier = lamp.transform.Find($"LOD{lod}");
            if (tier == null) throw new InvalidOperationException($"HD_Lamp/LOD{lod} missing.");

            AssertLegacyDisabled(tier);
            AssertNoVisibleStockPrimitive(tier);

            Transform physicalPole = RequireChild(tier, "PhysicalPole");
            Transform physicalDiffuser = RequireChild(tier, "PhysicalDiffuser");
            Transform gasket = RequireChild(tier, "LampServiceGasket");
            Transform cover = RequireChild(tier, "LampServiceCover");
            Transform boot = RequireChild(tier, "LampPoleTopBoot");
            Transform adapter = RequireChild(tier, "LampAdapterStem");
            Transform lower = RequireChild(tier, "LampLowerRetainer");
            Transform upper = RequireChild(tier, "LampUpperRetainer");
            Transform cap = RequireChild(tier, "LampWeatherCap");

            ValidatePole(physicalPole, contactConcrete, concrete, lod);
            ValidateCurvedServiceInterface(gasket, cover, lod);
            ValidateHeadInterfaces(physicalPole, boot, adapter, lower, physicalDiffuser, upper, cap, lod);
            ValidateLodMembership(lods[lod], tier, lod);

            Transform[] fasteners = tier.Cast<Transform>()
                .Where(x => x.name.StartsWith("LampServiceBolt_", StringComparison.Ordinal))
                .ToArray();
            if (lod == 0 && fasteners.Length != 4)
                throw new InvalidOperationException($"HD_Lamp/LOD0 requires four service-cover fasteners; got {fasteners.Length}.");
            if (lod > 0 && fasteners.Length != 0)
                throw new InvalidOperationException($"HD_Lamp/LOD{lod} must omit sub-pixel service-cover fasteners.");
        }

        Debug.Log(
            "Park lamp installation QA passed at source/runtime scene level: continuous taper, grade embed/material causality, " +
            "curved service-cover bearing, head insertion/retention, four-tier LOD membership and physical material classes. " +
            "This is not a Visual Fidelity PASS; rendered evidence is still required.");
    }

    public static void ValidateContractConfigOnly()
    {
        if (!File.Exists(ContractPath))
            throw new InvalidOperationException($"Missing required park-lamp construction/material metadata: {ContractPath}");
        if (!File.Exists(LookdevPath))
            throw new InvalidOperationException($"Missing required park-lamp lookdev illustration: {LookdevPath}");

        string json = File.ReadAllText(ContractPath);
        string[] tokens =
        {
            "\"id\": \"park_lamp_installation_interface\"",
            "\"bottomBelowGradeMetres\": 0.08",
            "\"topYMetres\": 3.92",
            "\"gradeDiameterMetres\": 0.190",
            "\"topDiameterMetres\": 0.125",
            "\"visibleHeightAboveGradeMetres\": 0.20",
            "\"curved service cover\"",
            "\"poleAdapterOverlapTargetMetres\": 0.002",
            "\"lowerRetainerDiffuserOverlapTargetMetres\": 0.005",
            "\"upperRetainerDiffuserOverlapTargetMetres\": 0.005",
            "\"requiredLodCount\": 4",
            "\"physicalPoleRadialSegments\": [32, 24, 16, 12]",
            "\"visualFidelityPointsAwarded\": 0",
            "PENDING_UNITY_RUNTIME"
        };
        foreach (string token in tokens)
            if (!json.Contains(token, StringComparison.Ordinal))
                throw new InvalidOperationException($"Park-lamp metadata contract missing required token: {token}");
    }

    private static void ValidatePole(Transform pole, Material contactConcrete, Material concrete, int lod)
    {
        MeshFilter mf = pole.GetComponent<MeshFilter>();
        MeshRenderer mr = pole.GetComponent<MeshRenderer>();
        if (mf == null || mf.sharedMesh == null || mr == null)
            throw new InvalidOperationException($"HD_Lamp/LOD{lod}/PhysicalPole is incomplete.");
        if (!mf.sharedMesh.name.StartsWith("GM_ParkLamp_ContinuousTaperPole_", StringComparison.Ordinal))
            throw new InvalidOperationException($"PhysicalPole uses unexpected mesh at LOD{lod}: {mf.sharedMesh.name}");
        if (mf.sharedMesh.subMeshCount != 2)
            throw new InvalidOperationException($"PhysicalPole must separate moisture-contact and dry concrete as two submeshes at LOD{lod}.");
        Material[] mats = mr.sharedMaterials;
        if (mats.Length != 2 || mats[0] != contactConcrete || mats[1] != concrete)
            throw new InvalidOperationException($"PhysicalPole material/submesh order drift at LOD{lod}.");

        Bounds b = mf.sharedMesh.bounds;
        if (b.min.y < -0.121f || b.min.y > -0.039f)
            throw new InvalidOperationException($"PhysicalPole below-grade embed invalid at LOD{lod}: minY={b.min.y:F4}.");
        if (Mathf.Abs(b.max.y - PoleTopY) > 0.004f)
            throw new InvalidOperationException($"PhysicalPole top drift at LOD{lod}: {b.max.y:F4} vs {PoleTopY:F4}.");
        if (b.size.y < 3.98f)
            throw new InvalidOperationException($"PhysicalPole too short at LOD{lod}: {b.size.y:F4} m.");

        Vector3[] v = mf.sharedMesh.vertices;
        float gradeRadius = RadiusNearY(v, 0f);
        float topRadius = RadiusNearY(v, PoleTopY);
        if (Mathf.Abs(gradeRadius - PoleGradeRadius) > 0.002f)
            throw new InvalidOperationException($"PhysicalPole grade radius drift at LOD{lod}: {gradeRadius:F4}.");
        if (Mathf.Abs(topRadius - PoleTopRadius) > 0.002f)
            throw new InvalidOperationException($"PhysicalPole top radius drift at LOD{lod}: {topRadius:F4}.");
    }

    private static void ValidateCurvedServiceInterface(Transform gasket, Transform cover, int lod)
    {
        Mesh gasketMesh = RequireMesh(gasket, lod);
        Mesh coverMesh = RequireMesh(cover, lod);
        if (Mathf.Abs(gasket.localPosition.y - ServiceCenterY) > 0.001f ||
            Mathf.Abs(cover.localPosition.y - ServiceCenterY) > 0.001f)
            throw new InvalidOperationException($"Service-cover vertical placement drift at LOD{lod}.");
        if (Mathf.Abs(gasketMesh.bounds.size.y - GasketHeight) > 0.002f ||
            Mathf.Abs(coverMesh.bounds.size.y - CoverHeight) > 0.002f)
            throw new InvalidOperationException($"Service-cover/gasket height drift at LOD{lod}.");

        (float gBottomMin, float gBottomMax) = ShellRadiusRange(gasketMesh, false);
        (float gTopMin, float gTopMax) = ShellRadiusRange(gasketMesh, true);
        (float cBottomMin, float cBottomMax) = ShellRadiusRange(coverMesh, false);
        (float cTopMin, float cTopMax) = ShellRadiusRange(coverMesh, true);
        float bottomSignedGap = cBottomMin - gBottomMax;
        float topSignedGap = cTopMin - gTopMax;
        if (bottomSignedGap > 0.003f || topSignedGap > 0.003f)
            throw new InvalidOperationException(
                $"Service cover floats above gasket at LOD{lod}: bottomGap={bottomSignedGap:F5}, topGap={topSignedGap:F5}.");
        if (bottomSignedGap < -0.008f || topSignedGap < -0.008f)
            throw new InvalidOperationException(
                $"Service cover penetrates gasket implausibly at LOD{lod}: bottom={bottomSignedGap:F5}, top={topSignedGap:F5}.");
        if (Mathf.Abs(bottomSignedGap + 0.0005f) > 0.0015f || Mathf.Abs(topSignedGap + 0.0005f) > 0.0015f)
            throw new InvalidOperationException(
                $"Service cover/gasket bearing deviates from 0.5 mm designed overlap at LOD{lod}: bottom={bottomSignedGap:F5}, top={topSignedGap:F5}.");
    }

    private static void ValidateHeadInterfaces(Transform pole, Transform boot, Transform adapter, Transform lower,
        Transform diffuser, Transform upper, Transform cap, int lod)
    {
        Interval p = VerticalInterval(pole);
        Interval b = VerticalInterval(boot);
        Interval a = VerticalInterval(adapter);
        Interval l = VerticalInterval(lower);
        Interval d = VerticalInterval(diffuser);
        Interval u = VerticalInterval(upper);
        Interval c = VerticalInterval(cap);

        AssertEdge("pole->adapter", p.max, a.min, -0.002f, lod);
        AssertEdge("adapter->lower retainer", a.max, l.min, -0.002f, lod);
        AssertEdge("lower retainer->diffuser", l.max, d.min, -0.005f, lod);
        AssertEdge("diffuser->upper retainer", d.max, u.min, -0.005f, lod);
        AssertEdge("upper retainer->weather cap", u.max, c.min, 0.0f, lod);
        if (b.min - p.max > 0.003f || a.min - b.max > 0.003f)
            throw new InvalidOperationException($"Pole-top boot does not bridge concrete pole to adapter at LOD{lod}.");

        if (Mathf.Abs(d.min - DiffuserBottomY) > 0.004f || Mathf.Abs(d.max - DiffuserTopY) > 0.004f)
            throw new InvalidOperationException(
                $"PhysicalDiffuser vertical envelope drift at LOD{lod}: [{d.min:F4},{d.max:F4}] vs [{DiffuserBottomY:F4},{DiffuserTopY:F4}].");
    }

    private static void ValidateLodMembership(LOD lod, Transform tier, int index)
    {
        HashSet<Renderer> members = new HashSet<Renderer>((lod.renderers ?? Array.Empty<Renderer>()).Where(x => x != null));
        foreach (string name in PersistentNames)
        {
            Transform t = tier.Find(name);
            Renderer r = t != null ? t.GetComponent<Renderer>() : null;
            if (r == null || !members.Contains(r))
                throw new InvalidOperationException($"HD_Lamp/LOD{index} does not bind persistent renderer {name} to its LOD set.");
        }
        foreach (string legacy in LegacyRendererNames)
        {
            Transform t = tier.Find(legacy);
            Renderer r = t != null ? t.GetComponent<Renderer>() : null;
            if (r != null && members.Contains(r))
                throw new InvalidOperationException($"HD_Lamp/LOD{index} still binds replaced legacy renderer {legacy}.");
        }
    }

    private static void AssertLegacyDisabled(Transform tier)
    {
        foreach (string name in LegacyRendererNames)
        {
            Transform child = tier.Find(name);
            Renderer r = child != null ? child.GetComponent<Renderer>() : null;
            if (r != null && r.enabled)
                throw new InvalidOperationException($"Replaced lamp renderer remains visible: {tier.parent.name}/{tier.name}/{name}");
        }
    }

    private static void AssertNoVisibleStockPrimitive(Transform tier)
    {
        foreach (MeshFilter mf in tier.GetComponentsInChildren<MeshFilter>(true))
        {
            Renderer r = mf.GetComponent<Renderer>();
            if (r == null || !r.enabled || mf.sharedMesh == null) continue;
            string n = mf.sharedMesh.name;
            if (n == "Cube" || n == "Cylinder" || n == "Sphere" || n == "Capsule")
                throw new InvalidOperationException($"Visible stock primitive remains in manufactured lamp: {mf.name} mesh={n}");
        }
    }

    private static void DisableLegacyRenderers(Transform tier)
    {
        foreach (string name in LegacyRendererNames)
        {
            Transform child = tier.Find(name);
            Renderer r = child != null ? child.GetComponent<Renderer>() : null;
            if (r != null) r.enabled = false;
        }
    }

    private static void DestroyGeneratedChildren(Transform tier)
    {
        var names = new HashSet<string>(PersistentNames.Where(x => x != "PhysicalDiffuser"), StringComparer.Ordinal);
        for (int i = 0; i < 4; i++) names.Add($"LampServiceBolt_{i}");
        foreach (string name in names)
        {
            Transform child = tier.Find(name);
            if (child != null) UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
    }

    private static Mesh GetTaperedPoleMesh(int lod, int sides)
    {
        string path = $"{MeshRoot}/GM_ParkLamp_ContinuousTaperPole_LOD{lod}_S{sides}_v1.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh != null) return mesh;
        mesh = BuildTaperedPoleMesh(sides);
        mesh.name = Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static Mesh BuildTaperedPoleMesh(int sides)
    {
        float moistureRadius = PoleRadiusAtY(MoistureTopY);
        float[] ys = { PoleBottomY, 0f, MoistureTopY, PoleTopY };
        float[] rs = { PoleGradeRadius + 0.001f, PoleGradeRadius, moistureRadius, PoleTopRadius };
        var vertices = new List<Vector3>((sides + 1) * ys.Length + 2);
        var uv = new List<Vector2>((sides + 1) * ys.Length + 2);
        var contactTriangles = new List<int>(sides * 12);
        var dryTriangles = new List<int>(sides * 6);

        for (int ring = 0; ring < ys.Length; ring++)
        {
            for (int i = 0; i <= sides; i++)
            {
                float u = i / (float)sides;
                float a = u * Mathf.PI * 2f;
                vertices.Add(new Vector3(Mathf.Cos(a) * rs[ring], ys[ring], Mathf.Sin(a) * rs[ring]));
                uv.Add(new Vector2(u * Mathf.PI * 2f * rs[ring], ys[ring] - PoleBottomY));
            }
        }

        int stride = sides + 1;
        for (int ring = 0; ring < ys.Length - 1; ring++)
        {
            List<int> target = ring < 2 ? contactTriangles : dryTriangles;
            for (int i = 0; i < sides; i++)
            {
                int a = ring * stride + i;
                int b = a + 1;
                int c = (ring + 1) * stride + i + 1;
                int d = (ring + 1) * stride + i;
                target.Add(a); target.Add(b); target.Add(c);
                target.Add(a); target.Add(c); target.Add(d);
            }
        }

        int bottomCenter = vertices.Count;
        vertices.Add(new Vector3(0f, PoleBottomY, 0f)); uv.Add(Vector2.zero);
        int topCenter = vertices.Count;
        vertices.Add(new Vector3(0f, PoleTopY, 0f)); uv.Add(Vector2.zero);
        for (int i = 0; i < sides; i++)
        {
            contactTriangles.Add(bottomCenter);
            contactTriangles.Add(i + 1);
            contactTriangles.Add(i);

            int top = (ys.Length - 1) * stride;
            dryTriangles.Add(topCenter);
            dryTriangles.Add(top + i);
            dryTriangles.Add(top + i + 1);
        }

        var mesh = new Mesh();
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uv);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(contactTriangles, 0, false);
        mesh.SetTriangles(dryTriangles, 1, false);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh GetCurvedShellMesh(string stem, int lod, float bottomRadius, float topRadius,
        float height, float halfAngleDeg, float thickness, int segments)
    {
        string path = $"{MeshRoot}/GM_ParkLamp_{stem}_LOD{lod}_S{segments}_v1.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh != null) return mesh;
        mesh = BuildCurvedShellMesh(bottomRadius, topRadius, height, halfAngleDeg, thickness, segments);
        mesh.name = Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static Mesh BuildCurvedShellMesh(float bottomRadius, float topRadius, float height,
        float halfAngleDeg, float thickness, int segments)
    {
        var vertices = new List<Vector3>(segments * 32);
        var triangles = new List<int>(segments * 36);
        var uv = new List<Vector2>(segments * 32);
        float y0 = -height * 0.5f;
        float y1 = height * 0.5f;
        float half = halfAngleDeg * Mathf.Deg2Rad;

        for (int i = 0; i < segments; i++)
        {
            float a0 = Mathf.Lerp(-half, half, i / (float)segments);
            float a1 = Mathf.Lerp(-half, half, (i + 1) / (float)segments);
            Vector3 r0 = new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0));
            Vector3 r1 = new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1));
            Vector3 rm = new Vector3(Mathf.Sin((a0 + a1) * 0.5f), 0f, Mathf.Cos((a0 + a1) * 0.5f));

            Vector3 ob0 = r0 * (bottomRadius + thickness) + Vector3.up * y0;
            Vector3 ob1 = r1 * (bottomRadius + thickness) + Vector3.up * y0;
            Vector3 ot0 = r0 * (topRadius + thickness) + Vector3.up * y1;
            Vector3 ot1 = r1 * (topRadius + thickness) + Vector3.up * y1;
            Vector3 ib0 = r0 * bottomRadius + Vector3.up * y0;
            Vector3 ib1 = r1 * bottomRadius + Vector3.up * y0;
            Vector3 it0 = r0 * topRadius + Vector3.up * y1;
            Vector3 it1 = r1 * topRadius + Vector3.up * y1;

            AddQuad(vertices, triangles, uv, ob0, ob1, ot1, ot0, rm);          // visible curved face
            AddQuad(vertices, triangles, uv, ib1, ib0, it0, it1, -rm);         // back face
            AddQuad(vertices, triangles, uv, ot0, ot1, it1, it0, Vector3.up);  // top edge
            AddQuad(vertices, triangles, uv, ib0, ib1, ob1, ob0, Vector3.down);// bottom edge

            if (i == 0)
            {
                Vector3 tangent = new Vector3(Mathf.Cos(a0), 0f, -Mathf.Sin(a0));
                AddQuad(vertices, triangles, uv, ib0, ob0, ot0, it0, -tangent);
            }
            if (i == segments - 1)
            {
                Vector3 tangent = new Vector3(Mathf.Cos(a1), 0f, -Mathf.Sin(a1));
                AddQuad(vertices, triangles, uv, ob1, ib1, it1, ot1, tangent);
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

    private static void AddQuad(List<Vector3> vertices, List<int> triangles, List<Vector2> uv,
        Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 expectedNormal)
    {
        int start = vertices.Count;
        vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
        uv.Add(new Vector2(0f, 0f)); uv.Add(new Vector2(1f, 0f));
        uv.Add(new Vector2(1f, 1f)); uv.Add(new Vector2(0f, 1f));
        Vector3 n = Vector3.Cross(b - a, c - a);
        if (Vector3.Dot(n, expectedNormal) >= 0f)
        {
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
        }
        else
        {
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
            triangles.Add(start); triangles.Add(start + 3); triangles.Add(start + 2);
        }
    }

    private static void CreateCurvedShell(string name, Transform parent, float centerY, Mesh mesh, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, centerY, 0f);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static void CreatePipe(string name, Transform parent, float diameter, float height, float centerY, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, centerY, 0f);
        go.AddComponent<MeshFilter>().sharedMesh =
            QualityBlockDetailMeshLibrary.GetBeveledCylinder(new Vector3(diameter, height * 0.5f, diameter), false);
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static void CreateServiceFasteners(Transform tier, Material material)
    {
        float[] angles = { -18f, 18f };
        float[] ys = { ServiceCenterY - 0.090f, ServiceCenterY + 0.090f };
        int index = 0;
        foreach (float y in ys)
        foreach (float angle in angles)
        {
            float a = angle * Mathf.Deg2Rad;
            Vector3 radial = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            float radius = PoleRadiusAtY(y) + CoverBaseOffset + CoverThickness + 0.0015f;
            var go = new GameObject($"LampServiceBolt_{index++}");
            go.transform.SetParent(tier, false);
            go.transform.localPosition = radial * radius + Vector3.up * y;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, radial);
            go.AddComponent<MeshFilter>().sharedMesh =
                QualityBlockDetailMeshLibrary.GetBeveledCylinder(new Vector3(0.008f, 0.002f, 0.008f), true);
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
    }

    private static Material EnsureEpdmMaterial()
    {
        Directory.CreateDirectory(MaterialRoot);
        string path = $"{MaterialRoot}/PBR_LampEPDM.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("Standard shader unavailable for PBR_LampEPDM.");
            mat = new Material(shader) { name = "PBR_LampEPDM" };
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_Color", new Color(0.025f, 0.027f, 0.026f, 1f));
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Glossiness", 0.08f); // roughness 0.92
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material RequireMaterial(string name)
    {
        string path = $"{MaterialRoot}/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) throw new InvalidOperationException($"Required park-lamp material missing: {path}");
        return mat;
    }

    private static void AssertDielectric(Material mat, string label)
    {
        if (mat == null || !mat.HasProperty("_Metallic"))
            throw new InvalidOperationException($"{label} lacks Standard metallic semantics.");
        if (mat.GetFloat("_Metallic") > 0.05f)
            throw new InvalidOperationException($"{label} became metallic: {mat.GetFloat("_Metallic"):F3}.");
    }

    private static float PoleRadiusAtY(float y)
    {
        float clamped = Mathf.Clamp(y, 0f, PoleTopY);
        return Mathf.Lerp(PoleGradeRadius, PoleTopRadius, clamped / PoleTopY);
    }

    private static float RadiusNearY(Vector3[] vertices, float y)
    {
        float best = float.MaxValue;
        float maxRadius = 0f;
        foreach (Vector3 p in vertices)
        {
            float dy = Mathf.Abs(p.y - y);
            float radius = new Vector2(p.x, p.z).magnitude;
            if (dy + 1e-6f < best)
            {
                best = dy;
                maxRadius = radius;
            }
            else if (Mathf.Abs(dy - best) < 1e-6f)
            {
                maxRadius = Mathf.Max(maxRadius, radius);
            }
        }
        return maxRadius;
    }

    private static (float min, float max) ShellRadiusRange(Mesh mesh, bool top)
    {
        Vector3[] vertices = mesh.vertices;
        float targetY = top ? mesh.bounds.max.y : mesh.bounds.min.y;
        float min = float.MaxValue;
        float max = 0f;
        foreach (Vector3 p in vertices)
        {
            if (Mathf.Abs(p.y - targetY) > 0.0001f) continue;
            float r = new Vector2(p.x, p.z).magnitude;
            min = Mathf.Min(min, r);
            max = Mathf.Max(max, r);
        }
        if (min == float.MaxValue)
            throw new InvalidOperationException($"Cannot sample curved-shell radial range: {mesh.name}");
        return (min, max);
    }

    private static Interval VerticalInterval(Transform t)
    {
        MeshFilter mf = t.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
            throw new InvalidOperationException($"Vertical interval requires a mesh: {t.name}");
        Bounds b = mf.sharedMesh.bounds;
        // All locked head/pole parts are generated with identity rotation and unit scale.
        return new Interval(t.localPosition.y + b.min.y, t.localPosition.y + b.max.y);
    }

    private static void AssertEdge(string label, float lowerPartTop, float upperPartBottom, float expectedSignedGap, int lod)
    {
        float signedGap = upperPartBottom - lowerPartTop; // negative = intentional overlap
        if (signedGap > 0.003f)
            throw new InvalidOperationException($"Lamp {label} floats at LOD{lod}: gap={signedGap:F5} m.");
        if (signedGap < -0.008f)
            throw new InvalidOperationException($"Lamp {label} over-penetrates at LOD{lod}: overlap={-signedGap:F5} m.");
        if (Mathf.Abs(signedGap - expectedSignedGap) > 0.0025f)
            throw new InvalidOperationException(
                $"Lamp {label} installation drift at LOD{lod}: signedGap={signedGap:F5}, expected={expectedSignedGap:F5}.");
    }

    private static Transform RequireChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child == null) throw new InvalidOperationException($"Required lamp component missing: {parent.parent.name}/{parent.name}/{name}");
        return child;
    }

    private static Mesh RequireMesh(Transform t, int lod)
    {
        MeshFilter mf = t.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
            throw new InvalidOperationException($"Lamp mesh missing at LOD{lod}: {t.name}");
        return mf.sharedMesh;
    }

    private readonly struct Interval
    {
        public readonly float min;
        public readonly float max;
        public Interval(float min, float max) { this.min = min; this.max = max; }
    }
}
