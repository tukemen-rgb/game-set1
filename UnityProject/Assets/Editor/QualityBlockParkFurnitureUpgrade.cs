using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Rebuilds the park/street-furniture fallback from actual manufactured subassemblies instead of
/// benchmark-visible Unity primitives. Gameplay colliders on the legacy placeholders are preserved;
/// their renderers are disabled and the high-detail render shell is collider-free.
/// </summary>
public static class QualityBlockParkFurnitureUpgrade
{
    private const string MaterialRoot = "Assets/Art/GeneratedParkFurnitureMaterials";
    private const string ParkRootName = "ParkFurnitureHighDetail";
    private const string StreetRootName = "StreetFurnitureHighDetail";

    [MenuItem("NewTown/Quality/Build Park + Street Furniture Construction Detail")]
    public static void BuildAndApply()
    {
        Transform park = Require("ParkEntrance");
        Transform street = Require("StreetFurniture");

        DisableLegacyRenderers(new[]
        {
            "SlideLegL", "SlideLegR", "SlidePlatform", "SlideChute",
            "BenchSeat", "BenchLegL", "BenchLegR",
            "LampPole", "LampGlobe", "NoticeBoardPosts", "NoticeBoardPanel"
        });

        DestroyChildIfPresent(park, ParkRootName);
        DestroyChildIfPresent(street, StreetRootName);

        Material paintedSteel = Mat("PBR_ParkPaintedSteel", new Color(0.105f, 0.305f, 0.39f), 0.68f, 0f);
        Material exposedSteel = Mat("PBR_ParkExposedSteel", new Color(0.34f, 0.355f, 0.36f), 0.31f, 0.78f);
        Material stainless = Mat("PBR_SlideStainless", new Color(0.49f, 0.515f, 0.52f), 0.22f, 0.86f);
        Material timber = Mat("PBR_BenchTimber", new Color(0.255f, 0.145f, 0.072f), 0.72f, 0f);
        Material concrete = Mat("PBR_ParkPrecastConcrete", new Color(0.49f, 0.485f, 0.46f), 0.86f, 0f);
        Material contactConcrete = Mat("PBR_ParkContactConcrete", new Color(0.395f, 0.39f, 0.37f), 0.91f, 0f);
        Material board = Mat("PBR_NoticeBoardBacking", new Color(0.205f, 0.16f, 0.105f), 0.79f, 0f);
        Material diffuser = Mat("PBR_LampDiffuserAged", new Color(0.71f, 0.73f, 0.68f), 0.42f, 0f);

        var parkRoot = new GameObject(ParkRootName);
        parkRoot.transform.SetParent(park, false);
        BuildSlide(parkRoot.transform, paintedSteel, exposedSteel, stainless);
        BuildBench(parkRoot.transform, timber, exposedSteel, concrete, contactConcrete);

        var streetRoot = new GameObject(StreetRootName);
        streetRoot.transform.SetParent(street, false);
        BuildLamp(streetRoot.transform, paintedSteel, exposedSteel, concrete, contactConcrete, diffuser);
        BuildNoticeBoard(streetRoot.transform, paintedSteel, exposedSteel, board, contactConcrete);

        AssetDatabase.SaveAssets();
        ValidateOpenScene();
        Debug.Log("Park/street furniture rebuilt as manufactured assemblies with 4-tier LODs; legacy gameplay colliders preserved separately.");
    }

    [MenuItem("NewTown/QA/Validate Park + Street Furniture Construction Detail")]
    public static void ValidateOpenScene()
    {
        GameObject parkRoot = GameObject.Find(ParkRootName);
        GameObject streetRoot = GameObject.Find(StreetRootName);
        if (parkRoot == null || streetRoot == null)
            throw new InvalidOperationException("Park/street-furniture high-detail roots are missing.");

        string[] assemblies = { "HD_Slide", "HD_Bench", "HD_Lamp", "HD_NoticeBoard" };
        foreach (string name in assemblies)
        {
            GameObject go = GameObject.Find(name);
            if (go == null) throw new InvalidOperationException($"Required manufactured assembly missing: {name}");
            LODGroup lod = go.GetComponent<LODGroup>();
            if (lod == null || lod.GetLODs().Length != 4)
                throw new InvalidOperationException($"{name} must have LOD0/1/2/3.");
            if (!lod.animateCrossFading || lod.fadeMode != LODFadeMode.CrossFade)
                throw new InvalidOperationException($"{name} must use animated cross-fade to reduce visible LOD pop.");
        }

        foreach (Collider c in parkRoot.GetComponentsInChildren<Collider>(true))
            throw new InvalidOperationException($"Generated visual furniture must not add gameplay colliders: {c.name}");
        foreach (Collider c in streetRoot.GetComponentsInChildren<Collider>(true))
            throw new InvalidOperationException($"Generated visual furniture must not add gameplay colliders: {c.name}");

        string[] legacy =
        {
            "SlideLegL", "SlideLegR", "SlidePlatform", "SlideChute", "BenchSeat", "BenchLegL", "BenchLegR",
            "LampPole", "LampGlobe", "NoticeBoardPosts", "NoticeBoardPanel"
        };
        foreach (string name in legacy)
        {
            GameObject go = GameObject.Find(name);
            Renderer r = go != null ? go.GetComponent<Renderer>() : null;
            if (r != null && r.enabled)
                throw new InvalidOperationException($"Critical placeholder risk: legacy primitive renderer still visible: {name}");
        }

        ValidateMaterials(parkRoot);
        ValidateMaterials(streetRoot);
    }

    private static void BuildSlide(Transform parent, Material paint, Material exposed, Material stainless)
    {
        CreateLodAssembly(parent, "HD_Slide", new Vector3(12.6f, 0f, -5f), (tier, lod) =>
        {
            // Typical late-Showa/Heisei municipal slide logic: welded painted JIS-like steel tube frame,
            // bolted deck, separate stainless chute sheet with raised side edges, no monolithic plastic shell.
            Pipe("LegL", tier, new Vector3(-0.63f, 1.06f, -0.16f), 0.0486f, 2.12f, Quaternion.Euler(0f, 0f, 6f), paint);
            Pipe("LegR", tier, new Vector3(0.63f, 1.06f, -0.16f), 0.0486f, 2.12f, Quaternion.Euler(0f, 0f, -6f), paint);
            Box("Deck", tier, new Vector3(0f, 2.03f, -0.18f), new Vector3(1.72f, 0.055f, 1.28f), paint);
            Box("ChuteSheet", tier, new Vector3(0f, 1.08f, 2.14f), new Vector3(0.92f, 0.035f, 4.18f), stainless, Quaternion.Euler(-24f, 0f, 0f));
            Box("ChuteLipL", tier, new Vector3(-0.48f, 1.15f, 2.12f), new Vector3(0.035f, 0.16f, 4.08f), stainless, Quaternion.Euler(-24f, 0f, 0f));
            Box("ChuteLipR", tier, new Vector3(0.48f, 1.15f, 2.12f), new Vector3(0.035f, 0.16f, 4.08f), stainless, Quaternion.Euler(-24f, 0f, 0f));

            if (lod <= 2)
            {
                Pipe("RearCrossBrace", tier, new Vector3(0f, 1.17f, -0.42f), 0.034f, 1.18f, Quaternion.Euler(0f, 0f, 90f), paint);
                Pipe("HandrailL", tier, new Vector3(-0.72f, 2.54f, -0.12f), 0.034f, 1.08f, Quaternion.identity, paint);
                Pipe("HandrailR", tier, new Vector3(0.72f, 2.54f, -0.12f), 0.034f, 1.08f, Quaternion.identity, paint);
                Pipe("HandrailTop", tier, new Vector3(0f, 3.03f, -0.12f), 0.034f, 1.42f, Quaternion.Euler(0f, 0f, 90f), paint);
            }
            if (lod == 0)
            {
                for (int side = -1; side <= 1; side += 2)
                for (int z = -1; z <= 1; z += 2)
                    Fastener($"DeckBolt_{side}_{z}", tier, new Vector3(side * 0.68f, 2.07f, -0.18f + z * 0.42f), 0.016f, 0.009f, exposed);
                Box("ChuteRunout", tier, new Vector3(0f, 0.215f, 4.06f), new Vector3(0.92f, 0.032f, 0.72f), stainless, Quaternion.Euler(-4f, 0f, 0f));
            }
        });
    }

    private static void BuildBench(Transform parent, Material timber, Material steel, Material concrete, Material contact)
    {
        CreateLodAssembly(parent, "HD_Bench", new Vector3(6.4f, 0f, -1.6f), (tier, lod) =>
        {
            Box("SupportL", tier, new Vector3(-1.08f, 0.28f, 0f), new Vector3(0.16f, 0.56f, 0.48f), concrete);
            Box("SupportR", tier, new Vector3(1.08f, 0.28f, 0f), new Vector3(0.16f, 0.56f, 0.48f), concrete);
            Box("ContactL", tier, new Vector3(-1.08f, 0.06f, 0f), new Vector3(0.18f, 0.12f, 0.50f), contact);
            Box("ContactR", tier, new Vector3(1.08f, 0.06f, 0f), new Vector3(0.18f, 0.12f, 0.50f), contact);

            if (lod >= 2)
            {
                Box("SeatProxy", tier, new Vector3(0f, 0.56f, 0f), new Vector3(3.02f, 0.105f, 0.58f), timber);
            }
            else
            {
                for (int i = 0; i < 5; i++)
                {
                    float z = -0.235f + i * 0.1175f;
                    Box($"SeatSlat_{i}", tier, new Vector3(0f, 0.56f, z), new Vector3(3.02f, 0.042f, 0.102f), timber);
                }
                Box("SteelBearer", tier, new Vector3(0f, 0.505f, 0f), new Vector3(2.42f, 0.045f, 0.40f), steel);
            }
            if (lod == 0)
            {
                for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    Fastener($"SeatBolt_{x}_{z}", tier, new Vector3(x * 1.02f, 0.588f, z * 0.18f), 0.014f, 0.008f, steel);
            }
        });
    }

    private static void BuildLamp(Transform parent, Material paint, Material steel, Material concrete, Material contact, Material diffuser)
    {
        CreateLodAssembly(parent, "HD_Lamp", new Vector3(4.7f, 0f, 5.2f), (tier, lod) =>
        {
            // Daytime scene keeps the original lamp Light at intensity 0; this is a period-correct
            // globe/mercury-vapor-era silhouette, not a modern LED slab luminaire.
            Pipe("PoleLower", tier, new Vector3(0f, 1.05f, 0f), 0.19f, 2.10f, Quaternion.identity, concrete);
            Pipe("PoleUpper", tier, new Vector3(0f, 3.03f, 0f), 0.15f, 1.86f, Quaternion.identity, concrete);
            Pipe("GroundMoistureBand", tier, new Vector3(0f, 0.10f, 0f), 0.196f, 0.20f, Quaternion.identity, contact);
            Pipe("Neck", tier, new Vector3(0f, 4.08f, 0f), 0.055f, 0.34f, Quaternion.identity, paint);
            Pipe("Cap", tier, new Vector3(0f, 4.31f, 0f), 0.29f, 0.08f, Quaternion.identity, steel);
            Pipe("Diffuser", tier, new Vector3(0f, 4.42f, 0f), lod >= 2 ? 0.38f : 0.43f, 0.22f, Quaternion.identity, diffuser);
            if (lod == 0)
                Pipe("DiffuserCrown", tier, new Vector3(0f, 4.57f, 0f), 0.31f, 0.08f, Quaternion.identity, steel);
        });
    }

    private static void BuildNoticeBoard(Transform parent, Material paint, Material steel, Material board, Material contact)
    {
        CreateLodAssembly(parent, "HD_NoticeBoard", new Vector3(9.3f, 0f, 7.2f), (tier, lod) =>
        {
            Pipe("PostL", tier, new Vector3(-0.94f, 0.95f, 0f), 0.055f, 1.90f, Quaternion.identity, paint);
            Pipe("PostR", tier, new Vector3(0.94f, 0.95f, 0f), 0.055f, 1.90f, Quaternion.identity, paint);
            Pipe("PostFootL", tier, new Vector3(-0.94f, 0.08f, 0f), 0.064f, 0.16f, Quaternion.identity, contact);
            Pipe("PostFootR", tier, new Vector3(0.94f, 0.08f, 0f), 0.064f, 0.16f, Quaternion.identity, contact);
            Box("Backing", tier, new Vector3(0f, 1.63f, 0f), new Vector3(2.28f, 1.23f, 0.055f), board);
            Box("FrameTop", tier, new Vector3(0f, 2.265f, -0.004f), new Vector3(2.42f, 0.06f, 0.075f), steel);
            Box("FrameBottom", tier, new Vector3(0f, 0.995f, -0.004f), new Vector3(2.42f, 0.06f, 0.075f), steel);
            Box("FrameL", tier, new Vector3(-1.18f, 1.63f, -0.004f), new Vector3(0.06f, 1.33f, 0.075f), steel);
            Box("FrameR", tier, new Vector3(1.18f, 1.63f, -0.004f), new Vector3(0.06f, 1.33f, 0.075f), steel);
            if (lod == 0)
            {
                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    Fastener($"FrameBolt_{x}_{y}", tier, new Vector3(x * 1.14f, 1.63f + y * 0.59f, -0.055f), 0.012f, 0.007f, steel, Quaternion.Euler(90f, 0f, 0f));
            }
        });
    }

    private static void CreateLodAssembly(Transform parent, string name, Vector3 localPosition, Action<Transform, int> buildTier)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.localPosition = localPosition;
        var group = root.AddComponent<LODGroup>();
        group.fadeMode = LODFadeMode.CrossFade;
        group.animateCrossFading = true;

        float[] heights = { 0.36f, 0.18f, 0.075f, 0.025f };
        var lods = new LOD[4];
        for (int i = 0; i < 4; i++)
        {
            var tier = new GameObject($"LOD{i}");
            tier.transform.SetParent(root.transform, false);
            buildTier(tier.transform, i);
            Renderer[] renderers = tier.GetComponentsInChildren<Renderer>(true);
            lods[i] = new LOD(heights[i], renderers) { fadeTransitionWidth = 0.12f };
        }
        group.SetLODs(lods);
        group.RecalculateBounds();
    }

    private static GameObject Box(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material, Quaternion? rotation = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = rotation ?? Quaternion.identity;
        go.AddComponent<MeshFilter>().sharedMesh = QualityBlockDetailMeshLibrary.GetChamferedBox(size);
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    private static GameObject Pipe(string name, Transform parent, Vector3 localPosition, float diameter, float height, Quaternion rotation, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = rotation;
        go.AddComponent<MeshFilter>().sharedMesh = QualityBlockDetailMeshLibrary.GetBeveledCylinder(new Vector3(diameter, height * 0.5f, diameter), false);
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    private static GameObject Fastener(string name, Transform parent, Vector3 localPosition, float diameter, float height, Material material, Quaternion? rotation = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = rotation ?? Quaternion.identity;
        go.AddComponent<MeshFilter>().sharedMesh = QualityBlockDetailMeshLibrary.GetBeveledCylinder(new Vector3(diameter, height * 0.5f, diameter), true);
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    private static Material Mat(string name, Color color, float roughness, float metallic)
    {
        if (!Directory.Exists(MaterialRoot))
        {
            Directory.CreateDirectory(MaterialRoot);
            AssetDatabase.Refresh();
        }
        string path = $"{MaterialRoot}/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("Standard shader unavailable.");
            mat = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.color = color;
        mat.SetFloat("_Metallic", Mathf.Clamp01(metallic));
        mat.SetFloat("_Glossiness", 1f - Mathf.Clamp01(roughness));
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void ValidateMaterials(GameObject root)
    {
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            Material m = r.sharedMaterial;
            if (m == null) throw new InvalidOperationException($"Missing material on {r.name}");
            float metallic = m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : 0f;
            if ((m.name.Contains("Concrete") || m.name.Contains("Timber") || m.name.Contains("Diffuser") || m.name.Contains("Backing") || m.name.Contains("Painted")) && metallic > 0.05f)
                throw new InvalidOperationException($"Materially impossible metallic value on coated/dielectric material {m.name}: {metallic}");
            if (m.HasProperty("_Glossiness"))
            {
                float roughness = 1f - m.GetFloat("_Glossiness");
                if (roughness < 0.12f || roughness > 0.98f)
                    throw new InvalidOperationException($"Furniture roughness outside physical contract on {m.name}: {roughness}");
            }
        }
    }

    private static Transform Require(string name)
    {
        GameObject go = GameObject.Find(name);
        if (go == null) throw new InvalidOperationException($"Required benchmark object missing: {name}");
        return go.transform;
    }

    private static void DisableLegacyRenderers(IEnumerable<string> names)
    {
        foreach (string name in names)
        {
            GameObject go = GameObject.Find(name);
            Renderer r = go != null ? go.GetComponent<Renderer>() : null;
            if (r != null) r.enabled = false;
        }
    }

    private static void DestroyChildIfPresent(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null) UnityEngine.Object.DestroyImmediate(child.gameObject);
    }
}
