using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Physical installation pass for the benchmark-facing rainwater leader. The long pipe stays outside
/// the accessory LODGroup as the continuous silhouette; hollow sockets/bands, wall restraint, roof offset
/// and receiver are explicit construction. Source/runtime QA never awards Visual Fidelity points.
/// </summary>
public static class QualityBlockRainwaterDownpipeInstallationQA
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private const string ContractPath = "Assets/QA/rainwater_downpipe_installation_contract.json";
    private const string ReportPath = "Assets/QA/rainwater_downpipe_installation_runtime_report.json";
    private const string MeshRoot = "Assets/Art/GeneratedDetailMeshes";
    private const string DetailRootName = "DanchiHighDetail";
    private const string AssemblyName = "HD_RainwaterDownpipeAssembly";
    private const string PipeName = "RainGutter";
    private const string PvcPath = "Assets/Art/GeneratedDetailMaterials/MAT_AgedDownpipePVC.mat";
    private const string MetalPath = "Assets/Art/GeneratedDetailMaterials/MAT_DarkGalvanizedSteel.mat";

    private const float PipeX = 4.45f, FacadeZ = -7.30f, PipeZ = -7.18f;
    private const float PipeOd = 0.075f, PipeBottom = 0.16f, PipeTop = 12.92f;
    private const float CouplerOd = 0.084f, CouplerId = 0.0765f, CouplerH = 0.09f;
    private const float BandOd = 0.084f, BandId = 0.077f, BandH = 0.032f;
    private const float PlateDepth = 0.008f, InterfaceOverlap = 0.002f;
    private const float ReceiverOd = 0.115f, ReceiverId = 0.080f, ReceiverH = 0.16f, ReceiverY = 0.10f;
    private const int RingSides = 24;

    private static readonly float[] CouplerY = { 2.66f, 5.31f, 7.96f, 10.61f };
    private static readonly float[] ClampY = { 0.82f, 2.37f, 3.92f, 5.47f, 7.02f, 8.57f, 10.12f, 11.67f };

    [MenuItem("NewTown/Geometry/Reconstruct Rainwater Downpipe Installation")]
    public static void ApplyAndPersist()
    {
        ValidateContractConfigOnly();
        RequireScene();
        Material pvc = RequireMaterial(PvcPath);
        Material metal = RequireMaterial(MetalPath);
        ValidateMaterial(pvc, "PVC-U", 0f, 0.05f, 0.08f, 0.35f);
        ValidateMaterial(metal, "galvanized", 0.75f, 1f, 0.28f, 0.52f);

        GameObject pipe = Find(PipeName);
        GameObject detailRoot = Find(DetailRootName);
        if (pipe == null || detailRoot == null)
            throw new InvalidOperationException("Rainwater reconstruction requires RainGutter and DanchiHighDetail.");

        RemovePrevious(detailRoot.transform);
        ConfigureMacroPipe(pipe, pvc);

        var root = new GameObject(AssemblyName);
        root.transform.SetParent(detailRoot.transform, false);

        for (int i = 0; i < CouplerY.Length; i++)
        {
            GameObject g = Ring($"HD_DownpipeJointSleeve_{i}", root.transform,
                new Vector3(PipeX, CouplerY[i], PipeZ), Quaternion.identity,
                CouplerOd, CouplerId, CouplerH, pvc);
            Weather(g, NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.SunExposed,
                NewTownStainSource.DrainRunoff | NewTownStainSource.UVExposure, 1f, 0.72f, 0.10f);
        }

        for (int i = 0; i < ClampY.Length; i++) BuildRestraint(root.transform, i, ClampY[i], metal);
        BuildRoofOffset(root.transform, pvc, metal);
        BuildReceiver(root.transform, pvc);

        QualityBlockDanchiLodUpgrade.ApplyToOpenScene();
        QualityBlockDanchiLodUpgrade.ValidateOpenScene();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ValidateOpenScene();
    }

    [MenuItem("NewTown/QA/Validate Rainwater Downpipe Installation Contract")]
    public static void ValidateContractConfigOnly()
    {
        string path = Absolute(ContractPath);
        if (!File.Exists(path)) throw new InvalidOperationException($"Missing required construction metadata: {ContractPath}");
        string json = File.ReadAllText(path);
        string[] required =
        {
            "\"assemblyId\": \"danchi.rainwater.downpipe.east\"",
            "\"criticalDefectRiskReduced\": \"floating_interpenetrating_hero_geometry\"",
            "\"visualFidelityPointsAwarded\": 0",
            "\"pipeOuterDiameterMetres\": 0.075",
            "\"pipeCentreZMetres\": -7.18",
            "\"couplerInnerDiameterMetres\": 0.0765",
            "\"pipeBandInnerDiameterMetres\": 0.077",
            "\"groundReceiverInnerDiameterMetres\": 0.08",
            "\"hollowSleevesRequired\": true",
            "\"maximumBracketInterfaceGapMetres\": 0.003",
            "\"requiredPipeInsertionIntoReceiverMetres\": 0.02",
            "\"aged_pvc_u_downpipe\"", "\"aged_galvanized_bracket_hardware\"",
            "\"paintedOrBakedHighlightsForbidden\": true", "\"levelsRequired\": 4",
            "\"requirePipeBodyVisibleAtAllDistances\": true", "\"automaticVisualScore\": 0",
            "\"runtimeRenderVerification\": \"PENDING_UNITY_RUNTIME\"",
            "Assets/QA/Lookdev/rainwater_downpipe_installation.svg"
        };
        foreach (string token in required)
            if (!json.Contains(token)) throw new InvalidOperationException($"Rainwater contract missing: {token}");
        if (!File.Exists(Absolute("Assets/QA/Lookdev/rainwater_downpipe_installation.svg")))
            throw new InvalidOperationException("Rainwater construction lookdev illustration is missing.");
    }

    [MenuItem("NewTown/QA/Validate Rainwater Downpipe Installation")]
    public static void ValidateOpenScene()
    {
        ValidateContractConfigOnly();
        RequireScene();
        var errors = new List<string>();
        Material pvc = AssetDatabase.LoadAssetAtPath<Material>(PvcPath);
        Material metal = AssetDatabase.LoadAssetAtPath<Material>(MetalPath);
        if (pvc == null) errors.Add("PVC-U material missing.");
        if (metal == null) errors.Add("Galvanized material missing.");
        TryMaterial(pvc, "PVC-U", 0f, 0.05f, 0.08f, 0.35f, errors);
        TryMaterial(metal, "galvanized", 0.75f, 1f, 0.28f, 0.52f, errors);

        GameObject pipe = Find(PipeName), root = Find(AssemblyName), detailRoot = Find(DetailRootName);
        if (pipe == null) errors.Add("RainGutter is missing."); else ValidatePipe(pipe, pvc, errors);
        if (root == null) errors.Add("Rainwater assembly root is missing.");
        if (detailRoot == null) errors.Add("DanchiHighDetail is missing.");
        if (root != null && detailRoot != null && !root.transform.IsChildOf(detailRoot.transform))
            errors.Add("Rainwater accessories are outside DanchiHighDetail.");

        ExactPrefix("HD_DownpipeJointSleeve_", 4, errors);
        ExactPrefix("HD_DownpipeClamp_", 8, errors);
        ExactPrefix("HD_DownpipeStandoff_", 8, errors);
        ExactPrefix("HD_DownpipeBracketPlate_", 8, errors);
        ExactPrefix("HD_DownpipeClampBolt_", 16, errors);
        ExactName("HD_DownpipeRoofOffset", 1, errors);
        ExactName("HD_DownpipeRoofCollar", 1, errors);
        ExactName("HD_DownpipeGroundReceiver", 1, errors);

        for (int i = 0; i < CouplerY.Length; i++)
        {
            GameObject g = Find($"HD_DownpipeJointSleeve_{i}");
            if (g == null) continue;
            Renderer r = g.GetComponent<Renderer>();
            if (r != null && (Mathf.Abs(r.bounds.center.x - PipeX) > 0.01f ||
                              Mathf.Abs(r.bounds.center.y - CouplerY[i]) > 0.015f ||
                              Mathf.Abs(r.bounds.center.z - PipeZ) > 0.01f))
                errors.Add($"Joint sleeve {i} is off the pipe centreline.");
            ValidateRing(g, CouplerOd, CouplerId, CouplerH, errors);
            Binding(g, pvc, errors);
        }

        for (int i = 0; i < ClampY.Length; i++)
        {
            GameObject band = Find($"HD_DownpipeClamp_{i}");
            GameObject arm = Find($"HD_DownpipeStandoff_{i}");
            GameObject plate = Find($"HD_DownpipeBracketPlate_{i}");
            if (band != null)
            {
                ValidateRing(band, BandOd, BandId, BandH, errors);
                Binding(band, metal, errors);
            }
            Binding(arm, metal, errors); Binding(plate, metal, errors);
            ValidateInterfaces(i, band, arm, plate, errors);
            if (plate?.GetComponent<Renderer>() is Renderer pr && Mathf.Abs(pr.bounds.min.z - FacadeZ) > 0.006f)
                errors.Add($"Bracket plate {i} is not seated on facade plane.");
        }

        GameObject receiver = Find("HD_DownpipeGroundReceiver");
        if (receiver != null)
        {
            ValidateRing(receiver, ReceiverOd, ReceiverId, ReceiverH, errors);
            Binding(receiver, pvc, errors);
            if (pipe?.GetComponent<Renderer>() is Renderer p && receiver.GetComponent<Renderer>() is Renderer rr)
            {
                float insertion = rr.bounds.max.y - p.bounds.min.y;
                if (insertion < 0.01f || insertion > 0.035f)
                    errors.Add($"Ground receiver insertion {insertion:F4}m outside [0.010,0.035].");
            }
        }
        GameObject collar = Find("HD_DownpipeRoofCollar");
        if (collar != null) { ValidateRing(collar, 0.105f, 0.078f, 0.024f, errors); Binding(collar, metal, errors); }

        ValidateLod(detailRoot, errors);
        if (errors.Count > 0)
            throw new InvalidOperationException("Rainwater downpipe installation QA FAILED:\n - " + string.Join("\n - ", errors));
        WriteReport(pipe, root);
        Debug.Log("Rainwater installation QA passed source/runtime invariants. Native 4K pixels remain mandatory; Visual Fidelity is UNSCORED.");
    }

    private static void ConfigureMacroPipe(GameObject pipe, Material pvc)
    {
        MeshFilter f = pipe.GetComponent<MeshFilter>(); Renderer r = pipe.GetComponent<Renderer>();
        if (f == null || r == null) throw new InvalidOperationException("RainGutter requires MeshFilter + Renderer.");
        float h = PipeTop - PipeBottom;
        f.sharedMesh = QualityBlockDetailMeshLibrary.GetBeveledCylinder(new Vector3(PipeOd, h * 0.5f, PipeOd), false);
        pipe.transform.localScale = Vector3.one; pipe.transform.rotation = Quaternion.identity;
        pipe.transform.position = new Vector3(PipeX, (PipeTop + PipeBottom) * 0.5f, PipeZ);
        r.sharedMaterial = pvc;
        if (pipe.GetComponent<CylinderCollider>() is CylinderCollider c)
        { c.direction = 1; c.center = Vector3.zero; c.radius = PipeOd * 0.5f; c.height = h; }
        Weather(pipe, NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.SunExposed,
            NewTownStainSource.DrainRunoff | NewTownStainSource.UVExposure | NewTownStainSource.GroundSplash,
            1f, 0.70f, 0.52f);
    }

    private static void BuildRestraint(Transform root, int i, float y, Material metal)
    {
        GameObject band = Ring($"HD_DownpipeClamp_{i}", root, new Vector3(PipeX, y, PipeZ), Quaternion.identity,
            BandOd, BandId, BandH, metal);
        Weather(band, NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.SunExposed,
            NewTownStainSource.FerrousFixture | NewTownStainSource.DrainRunoff | NewTownStainSource.UVExposure,
            0.95f, 0.70f, 0.08f);

        float plateFront = FacadeZ + PlateDepth;
        float bandBack = PipeZ - BandOd * 0.5f;
        float armBack = plateFront - InterfaceOverlap;
        float armFront = bandBack + InterfaceOverlap;
        float depth = armFront - armBack;
        if (depth <= 0f) throw new InvalidOperationException("Rainwater bracket standoff depth is non-positive.");

        GameObject arm = Box($"HD_DownpipeStandoff_{i}", root,
            new Vector3(PipeX, y, (armBack + armFront) * 0.5f), new Vector3(0.025f, 0.025f, depth), metal);
        Weather(arm, NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.Recessed,
            NewTownStainSource.FerrousFixture | NewTownStainSource.RecessGrime, 0.82f, 0.36f, 0.04f);

        GameObject plate = Box($"HD_DownpipeBracketPlate_{i}", root,
            new Vector3(PipeX, y, FacadeZ + PlateDepth * 0.5f), new Vector3(0.075f, 0.065f, PlateDepth), metal);
        Weather(plate, NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.Recessed,
            NewTownStainSource.FerrousFixture | NewTownStainSource.RecessGrime, 0.72f, 0.32f, 0.02f);

        for (int side = -1; side <= 1; side += 2)
        {
            GameObject bolt = Cylinder($"HD_DownpipeClampBolt_{i}_{(side < 0 ? "A" : "B")}", root,
                new Vector3(PipeX + side * 0.020f, y, FacadeZ + 0.011f), 0.012f, 0.006f,
                Quaternion.Euler(90f, 0f, 0f), metal, true);
            Weather(bolt, NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.Recessed,
                NewTownStainSource.FerrousFixture | NewTownStainSource.RecessGrime, 0.72f, 0.30f, 0.02f);
        }
    }

    private static void BuildRoofOffset(Transform root, Material pvc, Material metal)
    {
        Vector3 a = new Vector3(PipeX, PipeTop - 0.035f, PipeZ);
        Vector3 b = new Vector3(PipeX, PipeTop + 0.055f, FacadeZ + 0.030f);
        GameObject offset = PipeBetween("HD_DownpipeRoofOffset", root, a, b, PipeOd, pvc);
        Weather(offset, NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.SunExposed,
            NewTownStainSource.DrainRunoff | NewTownStainSource.UVExposure, 1f, 0.78f, 0.08f);
        GameObject collar = Ring("HD_DownpipeRoofCollar", root,
            new Vector3(PipeX, PipeTop + 0.055f, FacadeZ + 0.010f), Quaternion.Euler(90f, 0f, 0f),
            0.105f, 0.078f, 0.024f, metal);
        Weather(collar, NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.Recessed,
            NewTownStainSource.DrainRunoff | NewTownStainSource.FerrousFixture | NewTownStainSource.RecessGrime,
            0.92f, 0.44f, 0.05f);
    }

    private static void BuildReceiver(Transform root, Material pvc)
    {
        GameObject g = Ring("HD_DownpipeGroundReceiver", root, new Vector3(PipeX, ReceiverY, PipeZ),
            Quaternion.identity, ReceiverOd, ReceiverId, ReceiverH, pvc);
        Weather(g, NewTownSurfaceExposure.RainExposed | NewTownSurfaceExposure.GroundContact | NewTownSurfaceExposure.Recessed,
            NewTownStainSource.DrainRunoff | NewTownStainSource.GroundSplash | NewTownStainSource.RecessGrime,
            1f, 0.42f, 0.90f);
    }

    private static GameObject PipeBetween(string name, Transform parent, Vector3 a, Vector3 b, float od, Material mat)
    {
        Vector3 d = b - a; if (d.sqrMagnitude < 1e-6f) throw new InvalidOperationException("Zero-length pipe segment.");
        return Cylinder(name, parent, (a + b) * 0.5f, od, d.magnitude,
            Quaternion.FromToRotation(Vector3.up, d.normalized), mat, false);
    }

    private static GameObject Cylinder(string name, Transform parent, Vector3 pos, float od, float h,
        Quaternion rotation, Material mat, bool fastener)
    {
        var go = Bare(name, parent, pos, rotation);
        go.AddComponent<MeshFilter>().sharedMesh = QualityBlockDetailMeshLibrary.GetBeveledCylinder(new Vector3(od, h * 0.5f, od), fastener);
        go.AddComponent<MeshRenderer>().sharedMaterial = mat; return go;
    }

    private static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat)
    {
        var go = Bare(name, parent, pos, Quaternion.identity);
        go.AddComponent<MeshFilter>().sharedMesh = QualityBlockDetailMeshLibrary.GetChamferedBox(size);
        go.AddComponent<MeshRenderer>().sharedMaterial = mat; return go;
    }

    private static GameObject Ring(string name, Transform parent, Vector3 pos, Quaternion rotation,
        float od, float id, float h, Material mat)
    {
        var go = Bare(name, parent, pos, rotation);
        go.AddComponent<MeshFilter>().sharedMesh = RingMesh(od, id, h);
        go.AddComponent<MeshRenderer>().sharedMaterial = mat; return go;
    }

    private static GameObject Bare(string name, Transform parent, Vector3 pos, Quaternion rotation)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, true); go.transform.position = pos;
        go.transform.rotation = rotation; go.transform.localScale = Vector3.one; return go;
    }

    private static Mesh RingMesh(float od, float id, float h)
    {
        if (od <= id || id <= 0f || h <= 0f) throw new InvalidOperationException("Invalid hollow sleeve dimensions.");
        Directory.CreateDirectory(Absolute(MeshRoot));
        string path = $"{MeshRoot}/GM_RAIN_Annular_OD{Key(od)}_ID{Key(id)}_H{Key(h)}_S{RingSides}.asset";
        Mesh cached = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (cached != null) return cached;
        float ro = od * .5f, ri = id * .5f, hy = h * .5f;
        var v = new List<Vector3>(RingSides * 16); var t = new List<int>(RingSides * 24); var uv = new List<Vector2>(RingSides * 16);
        for (int n = 0; n < RingSides; n++)
        {
            float a0 = Mathf.PI * 2f * n / RingSides, a1 = Mathf.PI * 2f * (n + 1) / RingSides;
            Vector3 o0b = new Vector3(Mathf.Cos(a0)*ro,-hy,Mathf.Sin(a0)*ro), o1b = new Vector3(Mathf.Cos(a1)*ro,-hy,Mathf.Sin(a1)*ro);
            Vector3 o0t = new Vector3(o0b.x,hy,o0b.z), o1t = new Vector3(o1b.x,hy,o1b.z);
            Vector3 i0b = new Vector3(Mathf.Cos(a0)*ri,-hy,Mathf.Sin(a0)*ri), i1b = new Vector3(Mathf.Cos(a1)*ri,-hy,Mathf.Sin(a1)*ri);
            Vector3 i0t = new Vector3(i0b.x,hy,i0b.z), i1t = new Vector3(i1b.x,hy,i1b.z);
            Quad(v,t,uv,o1b,o0b,o0t,o1t); // outer wall: outward normal
            Quad(v,t,uv,i0b,i1b,i1t,i0t); // inner wall: inward normal
            Quad(v,t,uv,o1t,o0t,i0t,i1t); // top annulus: +Y
            Quad(v,t,uv,o0b,o1b,i1b,i0b); // bottom annulus: -Y
        }
        var mesh = new Mesh { name = Path.GetFileNameWithoutExtension(path) };
        mesh.SetVertices(v); mesh.SetTriangles(t,0); mesh.SetUVs(0,uv); mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh,path); return mesh;
    }

    private static void Quad(List<Vector3> v, List<int> t, List<Vector2> uv, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        int s=v.Count; v.Add(a);v.Add(b);v.Add(c);v.Add(d); uv.Add(Vector2.zero);uv.Add(Vector2.right);uv.Add(Vector2.one);uv.Add(Vector2.up);
        t.Add(s);t.Add(s+1);t.Add(s+2);t.Add(s);t.Add(s+2);t.Add(s+3);
    }

    private static void RemovePrevious(Transform detailRoot)
    {
        Transform p=detailRoot.Find(AssemblyName); if(p!=null) UnityEngine.Object.DestroyImmediate(p.gameObject);
        foreach(GameObject g in Resources.FindObjectsOfTypeAll<GameObject>().Where(x=>x.scene.IsValid() &&
            (x.name.StartsWith("HD_DownpipeClamp_",StringComparison.Ordinal)||x.name.StartsWith("HD_DownpipeClampBolt_",StringComparison.Ordinal))).ToArray())
            UnityEngine.Object.DestroyImmediate(g);
    }

    private static void Weather(GameObject go, NewTownSurfaceExposure e, NewTownStainSource s, float rain, float sun, float splash)
    {
        var m=go.GetComponent<QualityBlockWeatheringSurface>()??go.AddComponent<QualityBlockWeatheringSurface>(); m.Configure(e,s,rain,sun,splash,0f);
    }

    private static void ValidatePipe(GameObject go, Material expected, List<string> errors)
    {
        Renderer r=go.GetComponent<Renderer>(); MeshFilter f=go.GetComponent<MeshFilter>();
        if(r==null||f==null||f.sharedMesh==null){errors.Add("RainGutter renderer/mesh missing.");return;}
        if(!r.enabled||!go.activeInHierarchy) errors.Add("RainGutter macro silhouette is inactive.");
        if(!f.sharedMesh.name.StartsWith("GM_HD_BevelCylinder_",StringComparison.Ordinal)) errors.Add("RainGutter is not dimension-baked beveled geometry.");
        Bounds b=r.bounds;
        if(Mathf.Abs(b.size.x-PipeOd)>.004f||Mathf.Abs(b.size.z-PipeOd)>.004f) errors.Add($"Pipe OD invalid: {b.size}.");
        if(Mathf.Abs(b.min.y-PipeBottom)>.01f||Mathf.Abs(b.max.y-PipeTop)>.01f) errors.Add("Pipe vertical extent invalid.");
        if(Mathf.Abs(b.center.x-PipeX)>.01f||Mathf.Abs(b.center.z-PipeZ)>.01f) errors.Add("Pipe centreline invalid.");
        float clearance=b.min.z-FacadeZ; if(clearance<.05f||clearance>.15f) errors.Add($"Pipe-wall clearance {clearance:F4}m invalid.");
        Binding(go,expected,errors);
        if(go.transform.parent!=null&&go.transform.parent.name==DetailRootName) errors.Add("Macro pipe must remain outside accessory LODGroup.");
    }

    private static void ValidateRing(GameObject go,float od,float id,float h,List<string> errors)
    {
        Mesh m=go.GetComponent<MeshFilter>()?.sharedMesh; if(m==null){errors.Add($"{go.name} ring mesh missing.");return;}
        string key=$"GM_RAIN_Annular_OD{Key(od)}_ID{Key(id)}_H{Key(h)}_S{RingSides}";
        if(m.name!=key) errors.Add($"{go.name} hollow mesh identity invalid: {m.name}.");
        if(Mathf.Abs(m.bounds.size.x-od)>.0015f||Mathf.Abs(m.bounds.size.z-od)>.0015f||Mathf.Abs(m.bounds.size.y-h)>.0015f) errors.Add($"{go.name} hollow mesh bounds invalid.");
        if(m.vertexCount<RingSides*16) errors.Add($"{go.name} does not contain complete inner/outer annular geometry.");
    }

    private static void ValidateInterfaces(int i,GameObject band,GameObject arm,GameObject plate,List<string> errors)
    {
        Renderer br=band?.GetComponent<Renderer>(), ar=arm?.GetComponent<Renderer>(), pr=plate?.GetComponent<Renderer>(); if(br==null||ar==null||pr==null)return;
        float g1=ar.bounds.min.z-pr.bounds.max.z, g2=br.bounds.min.z-ar.bounds.max.z;
        if(g1>.003f||g1<-.006f) errors.Add($"Bracket {i} plate/arm interface={g1:F4}m.");
        if(g2>.003f||g2<-.006f) errors.Add($"Bracket {i} arm/band interface={g2:F4}m.");
    }

    private static void ValidateLod(GameObject detailRoot,List<string> errors)
    {
        LODGroup g=detailRoot?.GetComponent<LODGroup>(); if(g==null){errors.Add("DanchiHighDetail LODGroup missing.");return;}
        LOD[] l=g.GetLODs(); if(l.Length!=4){errors.Add($"Expected 4 LODs, got {l.Length}.");return;}
        bool j0=l[0].renderers.Any(x=>x!=null&&x.gameObject.name=="HD_DownpipeJointSleeve_0");
        bool j1=l[1].renderers.Any(x=>x!=null&&x.gameObject.name=="LOD1_HD_DownpipeJointSleeve_0");
        bool j2=l[2].renderers.Any(x=>x!=null&&x.gameObject.name.Contains("DownpipeJointSleeve"));
        bool j3=l[3].renderers.Any(x=>x!=null&&x.gameObject.name.Contains("DownpipeJointSleeve"));
        if(!j0||!j1||j2||j3) errors.Add($"Joint LOD policy invalid {j0}/{j1}/{j2}/{j3}.");
        bool b0=l[0].renderers.Any(x=>x!=null&&x.gameObject.name=="HD_DownpipeClampBolt_0_A");
        bool b1=l[1].renderers.Any(x=>x!=null&&x.gameObject.name.Contains("DownpipeClampBolt_0_A"));
        if(!b0||b1) errors.Add("Downpipe fastener must be LOD0-only.");
        if(g.fadeMode!=LODFadeMode.CrossFade||!g.animateCrossFading) errors.Add("Accessory LODs require animated cross-fade.");
    }

    private static void ExactPrefix(string prefix,int expected,List<string> errors)
    { int n=Resources.FindObjectsOfTypeAll<GameObject>().Count(x=>x.scene.IsValid()&&x.name.StartsWith(prefix,StringComparison.Ordinal)); if(n!=expected)errors.Add($"{prefix} count {n}, expected {expected}."); }
    private static void ExactName(string name,int expected,List<string> errors)
    { int n=Resources.FindObjectsOfTypeAll<GameObject>().Count(x=>x.scene.IsValid()&&x.name==name); if(n!=expected)errors.Add($"{name} count {n}, expected {expected}."); }
    private static void Binding(GameObject go,Material expected,List<string> errors)
    { if(go==null||expected==null)return; Renderer r=go.GetComponent<Renderer>(); if(r==null)errors.Add($"{go.name} renderer missing."); else if(r.sharedMaterial!=expected)errors.Add($"{go.name} material is {r.sharedMaterial?.name??"<null>"}, expected {expected.name}."); }

    private static void TryMaterial(Material m,string label,float m0,float m1,float s0,float s1,List<string> errors)
    { if(m==null)return; try{ValidateMaterial(m,label,m0,m1,s0,s1);}catch(Exception ex){errors.Add(ex.Message);} }
    private static void ValidateMaterial(Material m,string label,float m0,float m1,float s0,float s1)
    {
        if(m==null||m.shader==null||m.shader.name!="Standard")throw new InvalidOperationException($"{label} must use Standard shader.");
        float metal=m.GetFloat("_Metallic"), smooth=m.GetFloat("_Glossiness");
        if(metal<m0-.0001f||metal>m1+.0001f)throw new InvalidOperationException($"{label} metallic {metal:F3} invalid.");
        if(smooth<s0-.0001f||smooth>s1+.0001f)throw new InvalidOperationException($"{label} smoothness {smooth:F3} invalid.");
        if(m.HasProperty("_EmissionColor")){Color e=m.GetColor("_EmissionColor");if(Mathf.Max(e.r,Mathf.Max(e.g,e.b))>.01f)throw new InvalidOperationException($"{label} must not emit.");}
    }

    private static void WriteReport(GameObject pipe,GameObject root)
    {
        Renderer r=pipe?.GetComponent<Renderer>(); var report=new RuntimeReport{
            schemaVersion="1.1.0",unityVersion=Application.unityVersion,scenePath=ScenePath,status="SOURCE_RUNTIME_QA_PASS_RENDER_REVIEW_PENDING",
            visualFidelityPointsAwarded=0,pipeBoundsCentre=r!=null?r.bounds.center:Vector3.zero,pipeBoundsSize=r!=null?r.bounds.size:Vector3.zero,
            sourceAccessoryRendererCount=root!=null?root.GetComponentsInChildren<Renderer>(true).Length:0,couplerCount=4,clampCount=8,fastenerHeadCount=16,
            hollowAnnularInterfaces=true,maximumBracketInterfaceGapMetres=.003f,lodLevels=4,native4KReviewPending=true,
            criticalDefectClearance="NOT_CLEARED_WITHOUT_NATIVE_4K_PIXELS"};
        File.WriteAllText(Absolute(ReportPath),JsonUtility.ToJson(report,true)); AssetDatabase.ImportAsset(ReportPath,ImportAssetOptions.ForceUpdate);
    }

    private static Material RequireMaterial(string p){Material m=AssetDatabase.LoadAssetAtPath<Material>(p);if(m==null)throw new InvalidOperationException($"Required material missing: {p}");return m;}
    private static void RequireScene(){if(!EditorSceneManager.GetActiveScene().IsValid()||EditorSceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException($"Open {ScenePath} first.");}
    private static GameObject Find(string n)=>Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(x=>x.scene.IsValid()&&x.scene.path==ScenePath&&x.name==n);
    private static int Key(float m)=>Mathf.RoundToInt(m*10000f);
    private static string Absolute(string assetPath){string root=Directory.GetParent(Application.dataPath)?.FullName;if(string.IsNullOrWhiteSpace(root))throw new InvalidOperationException("Unity project root unavailable.");return Path.GetFullPath(Path.Combine(root,assetPath));}

    [Serializable]
    private sealed class RuntimeReport
    {
        public string schemaVersion,unityVersion,scenePath,status,criticalDefectClearance;
        public int visualFidelityPointsAwarded,sourceAccessoryRendererCount,couplerCount,clampCount,fastenerHeadCount,lodLevels;
        public Vector3 pipeBoundsCentre,pipeBoundsSize;
        public bool hollowAnnularInterfaces,native4KReviewPending;
        public float maximumBracketInterfaceGapMetres;
    }
}
