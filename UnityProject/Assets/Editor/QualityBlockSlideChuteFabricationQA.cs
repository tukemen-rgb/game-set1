using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Replaces the physical slide's earlier overlapping open sheet surfaces with a single watertight
/// thin-sheet U-section solid. The running surface, raised side walls and both longitudinal ends share
/// one indexed manifold mesh, so a grazing benchmark view cannot reveal an open 2 mm shell edge or a
/// coincident side-sheet seam. This is source/runtime implementation QA only and awards zero Visual
/// Fidelity points until the native 4K pixels are actually reviewed.
/// </summary>
public static class QualityBlockSlideChuteFabricationQA
{
    private const string ContractPath = "Assets/QA/slide_chute_fabrication_contract.json";
    private const string LookdevPath = "Assets/QA/slide_chute_fabrication_lookdev.svg";
    private const string RuntimeReportPath = "Assets/QA/slide_chute_fabrication_runtime_report.json";
    private const string MeshRoot = "Assets/Art/GeneratedParkFurnitureMeshes";
    private const string SlideName = "HD_Slide";
    private const string PhysicalChuteName = "PhysicalChute";

    [MenuItem("NewTown/QA/Validate Slide Chute Fabrication Contract")]
    public static void ValidateContractConfigOnly()
    {
        ChuteContract contract = LoadContract();
        List<string> errors = ValidateContract(contract);
        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Slide chute fabrication contract FAILED:\n - " + string.Join("\n - ", errors));

        if (!File.Exists(LookdevPath))
            throw new InvalidOperationException($"Missing required slide chute fabrication lookdev: {LookdevPath}");

        Debug.Log(
            "Slide chute fabrication contract valid. Source metadata/topology can award zero Visual Fidelity points; " +
            "native 4K and temporal evidence remain mandatory.");
    }

    [MenuItem("NewTown/Geometry/Rebuild Slide Chute As Watertight Sheet Solid")]
    public static void ApplyToOpenScene()
    {
        ChuteContract contract = LoadAndValidateContract();
        Transform slide = RequireSlide();
        Directory.CreateDirectory(MeshRoot);

        for (int lod = 0; lod < contract.qaRules.requiredLodCount; lod++)
        {
            Transform tier = RequireDirectChild(slide, $"LOD{lod}");
            Transform chute = RequireDirectChild(tier, PhysicalChuteName);
            MeshFilter filter = RequireComponent<MeshFilter>(chute);
            MeshRenderer renderer = RequireComponent<MeshRenderer>(chute);

            int segments = contract.dimensionsMetres.lodSegments[lod];
            Mesh generated = BuildWatertightChuteMesh(contract, segments);
            string assetPath = $"{MeshRoot}/GM_ParkSlide_WatertightUSheet_v1_LOD{lod}_S{segments}.asset";
            filter.sharedMesh = SaveOrReplaceMeshAsset(assetPath, generated);

            Material stainless = FindMaterialRecursive(tier, contract.qaRules.requiredMaterialName);
            if (stainless == null)
                throw new InvalidOperationException(
                    $"{contract.qaRules.requiredMaterialName} missing while rebuilding LOD{lod} slide chute.");
            renderer.sharedMaterial = stainless;
            EditorUtility.SetDirty(filter);
            EditorUtility.SetDirty(renderer);
        }

        AssetDatabase.SaveAssets();
        ValidationSummary summary = ValidateOpenSceneInternal(contract, requireLodBinding: false);
        WriteRuntimeReport(summary, lodBindingVerified: false);

        Debug.Log(
            $"Slide chute rebuilt as a watertight fabricated sheet solid; minimum curve radius={summary.minimumCurveRadiusMetres:0.###} m. " +
            "Run park-furniture LOD rebind next. Visual Fidelity remains UNSCORED.");
    }

    [MenuItem("NewTown/QA/Validate Slide Chute Fabrication In Open Scene")]
    public static void ValidateOpenScene()
    {
        ChuteContract contract = LoadAndValidateContract();
        ValidationSummary summary = ValidateOpenSceneInternal(contract, requireLodBinding: true);
        WriteRuntimeReport(summary, lodBindingVerified: true);

        Debug.Log(
            $"Slide chute fabrication QA passed: minCurveRadius={summary.minimumCurveRadiusMetres:0.###} m, " +
            $"boundaryEdges={summary.maximumBoundaryEdgeCount}, nonManifoldEdges={summary.maximumNonManifoldEdgeCount}, " +
            $"minimumTriangleArea={summary.minimumTriangleAreaSquareMetres:0.#########} m^2. " +
            "Rendered thin-edge response, bearing contact and LOD stability remain unverified.");
    }

    private static ValidationSummary ValidateOpenSceneInternal(ChuteContract contract, bool requireLodBinding)
    {
        Transform slide = RequireSlide();
        var errors = new List<string>();
        var summary = new ValidationSummary
        {
            minimumCurveRadiusMetres = SampleMinimumCurveRadius(contract.dimensionsMetres, 1024),
            minimumTriangleAreaSquareMetres = float.PositiveInfinity,
        };

        LODGroup group = slide.GetComponent<LODGroup>();
        LOD[] lods = group != null ? group.GetLODs() : Array.Empty<LOD>();
        if (group == null || lods.Length != contract.qaRules.requiredLodCount)
            errors.Add($"HD_Slide must retain {contract.qaRules.requiredLodCount} LODs; found {lods.Length}.");
        if (group == null || !group.animateCrossFading || group.fadeMode != LODFadeMode.CrossFade)
            errors.Add("HD_Slide must retain animated cross-fade LOD transitions.");

        if (summary.minimumCurveRadiusMetres + 0.001f < contract.qaRules.minimumCurveRadiusMetres)
            errors.Add(
                $"Chute curve minimum radius {summary.minimumCurveRadiusMetres:0.###} m is below " +
                $"{contract.qaRules.minimumCurveRadiusMetres:0.###} m.");

        for (int lod = 0; lod < contract.qaRules.requiredLodCount; lod++)
        {
            Transform tier = RequireDirectChild(slide, $"LOD{lod}");
            Transform chute = RequireDirectChild(tier, PhysicalChuteName);
            MeshFilter filter = RequireComponent<MeshFilter>(chute);
            MeshRenderer renderer = RequireComponent<MeshRenderer>(chute);
            Mesh mesh = filter.sharedMesh;
            if (mesh == null)
            {
                errors.Add($"LOD{lod}: PhysicalChute mesh missing.");
                continue;
            }

            int expectedSegments = contract.dimensionsMetres.lodSegments[lod];
            if (!mesh.name.StartsWith("GM_ParkSlide_WatertightUSheet_v1_", StringComparison.Ordinal))
                errors.Add($"LOD{lod}: final chute did not bind the watertight fabricated mesh; got {mesh.name}.");

            MeshSummary meshSummary = InspectMesh(mesh, expectedSegments, contract);
            summary.maximumBoundaryEdgeCount = Math.Max(summary.maximumBoundaryEdgeCount, meshSummary.boundaryEdges);
            summary.maximumNonManifoldEdgeCount = Math.Max(summary.maximumNonManifoldEdgeCount, meshSummary.nonManifoldEdges);
            summary.minimumTriangleAreaSquareMetres = Math.Min(
                summary.minimumTriangleAreaSquareMetres, meshSummary.minimumTriangleAreaSquareMetres);

            if (meshSummary.boundaryEdges > contract.qaRules.maximumBoundaryEdgeCount)
                errors.Add($"LOD{lod}: watertight chute has {meshSummary.boundaryEdges} boundary edges.");
            if (meshSummary.nonManifoldEdges > contract.qaRules.maximumNonManifoldEdgeCount)
                errors.Add($"LOD{lod}: chute has {meshSummary.nonManifoldEdges} non-manifold edges.");
            if (meshSummary.minimumTriangleAreaSquareMetres < contract.qaRules.minimumTriangleAreaSquareMetres)
                errors.Add(
                    $"LOD{lod}: minimum triangle area {meshSummary.minimumTriangleAreaSquareMetres:0.#########} m^2 " +
                    "is degenerate or below the fabrication floor.");
            if (!meshSummary.sectionDimensionsValid)
                errors.Add($"LOD{lod}: chute U-section no longer matches locked width/thickness/side-height dimensions.");
            if (!meshSummary.endCapWindingValid)
                errors.Add($"LOD{lod}: head/runout cap winding is not outward-facing.");
            if (!meshSummary.runningSurfaceWindingValid)
                errors.Add($"LOD{lod}: running-surface winding is not consistently upward-facing.");

            if (renderer.sharedMaterial == null ||
                !string.Equals(renderer.sharedMaterial.name, contract.qaRules.requiredMaterialName, StringComparison.Ordinal))
                errors.Add(
                    $"LOD{lod}: PhysicalChute must use {contract.qaRules.requiredMaterialName}; " +
                    $"got {(renderer.sharedMaterial != null ? renderer.sharedMaterial.name : "<null>")}.");

            if (chute.GetComponentsInChildren<Collider>(true).Length != contract.qaRules.generatedColliderCount)
                errors.Add($"LOD{lod}: fabricated chute render shell must add zero gameplay colliders.");

            AssertLegacyChuteRendererDisabled(tier, "ChuteSheet", errors, lod);
            AssertLegacyChuteRendererDisabled(tier, "ChuteLipL", errors, lod);
            AssertLegacyChuteRendererDisabled(tier, "ChuteLipR", errors, lod);
            AssertLegacyChuteRendererDisabled(tier, "ChuteRunout", errors, lod, allowMissing: lod != 0);

            if (requireLodBinding && group != null && lod < lods.Length)
            {
                bool found = Array.Exists(lods[lod].renderers, r => r == renderer);
                if (!found)
                    errors.Add($"LOD{lod}: corrected PhysicalChute renderer is not bound to its HD_Slide LOD set.");
            }
        }

        if (float.IsPositiveInfinity(summary.minimumTriangleAreaSquareMetres))
            summary.minimumTriangleAreaSquareMetres = 0f;

        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Slide chute fabrication runtime QA FAILED:\n - " + string.Join("\n - ", errors));

        return summary;
    }

    private static Mesh BuildWatertightChuteMesh(ChuteContract contract, int segments)
    {
        DimensionsMetres d = contract.dimensionsMetres;
        float half = d.chuteWidth * 0.5f;
        float thickness = d.sheetThickness;
        float side = d.sideWallHeight;

        // CCW when viewed along +tangent. This is the physical solid cross-section of the sheet:
        // bottom running-sheet face -> right outer wall -> right cap -> right inner wall -> running
        // surface -> left inner wall -> left cap -> left outer wall. There are no overlapping shells.
        Vector2[] section =
        {
            new Vector2(-half, -thickness),
            new Vector2( half, -thickness),
            new Vector2( half, side),
            new Vector2( half - thickness, side),
            new Vector2( half - thickness, 0f),
            new Vector2(-half + thickness, 0f),
            new Vector2(-half + thickness, side),
            new Vector2(-half, side),
        };

        List<int> endCapTriangles = TriangulateCounterClockwise(section);
        int ringSize = section.Length;
        var vertices = new List<Vector3>((segments + 1) * ringSize);
        var uv = new List<Vector2>((segments + 1) * ringSize);
        var triangles = new List<int>(segments * ringSize * 6 + endCapTriangles.Count * 2);

        float[] perimeter = BuildPerimeterCoordinate(section);
        Vector3 previousCenter = Vector3.zero;
        float longitudinalMetres = 0f;

        for (int i = 0; i <= segments; i++)
        {
            float u = i / (float)segments;
            Vector3 center;
            Vector3 tangent;
            Vector3 normal;
            EvaluateFrame(d, u, out center, out tangent, out normal);

            if (i > 0)
                longitudinalMetres += Vector3.Distance(previousCenter, center);
            previousCenter = center;

            for (int k = 0; k < ringSize; k++)
            {
                Vector2 p = section[k];
                vertices.Add(center + Vector3.right * p.x + normal * p.y);
                uv.Add(new Vector2(perimeter[k], longitudinalMetres));
            }
        }

        for (int i = 0; i < segments; i++)
        {
            int aRing = i * ringSize;
            int bRing = (i + 1) * ringSize;
            for (int k = 0; k < ringSize; k++)
            {
                int kn = (k + 1) % ringSize;
                AddQuad(
                    triangles,
                    aRing + k,
                    aRing + kn,
                    bRing + kn,
                    bRing + k);
            }
        }

        int lastRing = segments * ringSize;
        for (int i = 0; i < endCapTriangles.Count; i += 3)
        {
            int a = endCapTriangles[i];
            int b = endCapTriangles[i + 1];
            int c = endCapTriangles[i + 2];

            // Section polygon is CCW looking along +tangent: reverse at the head (-tangent),
            // preserve at the runout (+tangent).
            triangles.Add(a); triangles.Add(c); triangles.Add(b);
            triangles.Add(lastRing + a); triangles.Add(lastRing + b); triangles.Add(lastRing + c);
        }

        var mesh = new Mesh
        {
            name = $"GM_ParkSlide_WatertightUSheet_v1_S{segments}"
        };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uv);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static MeshSummary InspectMesh(Mesh mesh, int segments, ChuteContract contract)
    {
        var result = new MeshSummary
        {
            minimumTriangleAreaSquareMetres = float.PositiveInfinity,
            sectionDimensionsValid = true,
            endCapWindingValid = true,
            runningSurfaceWindingValid = true,
        };

        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        int ringSize = 8;
        int expectedVertexCount = (segments + 1) * ringSize;
        if (vertices.Length != expectedVertexCount)
            result.sectionDimensionsValid = false;

        var edgeUse = new Dictionary<ulong, int>();
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int a = triangles[i];
            int b = triangles[i + 1];
            int c = triangles[i + 2];
            if (a < 0 || b < 0 || c < 0 || a >= vertices.Length || b >= vertices.Length || c >= vertices.Length)
            {
                result.nonManifoldEdges = int.MaxValue;
                result.minimumTriangleAreaSquareMetres = 0f;
                return result;
            }

            float area = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]).magnitude * 0.5f;
            result.minimumTriangleAreaSquareMetres = Mathf.Min(result.minimumTriangleAreaSquareMetres, area);
            AddEdge(edgeUse, a, b);
            AddEdge(edgeUse, b, c);
            AddEdge(edgeUse, c, a);
        }

        foreach (KeyValuePair<ulong, int> pair in edgeUse)
        {
            if (pair.Value == 1) result.boundaryEdges++;
            else if (pair.Value != 2) result.nonManifoldEdges++;
        }

        if (vertices.Length >= ringSize)
            result.sectionDimensionsValid &= ValidateSectionDimensions(vertices, 0, contract);
        if (vertices.Length >= expectedVertexCount)
            result.sectionDimensionsValid &= ValidateSectionDimensions(vertices, segments * ringSize, contract);

        DimensionsMetres d = contract.dimensionsMetres;
        Vector3 startCenter, startTangent, startNormal;
        Vector3 endCenter, endTangent, endNormal;
        EvaluateFrame(d, 0f, out startCenter, out startTangent, out startNormal);
        EvaluateFrame(d, 1f, out endCenter, out endTangent, out endNormal);

        Vector3 startCapNormal = Vector3.zero;
        Vector3 endCapNormal = Vector3.zero;
        int lastStart = segments * ringSize;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int a = triangles[i];
            int b = triangles[i + 1];
            int c = triangles[i + 2];
            Vector3 n = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            if (a < ringSize && b < ringSize && c < ringSize) startCapNormal += n;
            if (a >= lastStart && b >= lastStart && c >= lastStart) endCapNormal += n;
        }
        if (startCapNormal.sqrMagnitude < 1e-10f || Vector3.Dot(startCapNormal.normalized, -startTangent) < 0.90f)
            result.endCapWindingValid = false;
        if (endCapNormal.sqrMagnitude < 1e-10f || Vector3.Dot(endCapNormal.normalized, endTangent) < 0.90f)
            result.endCapWindingValid = false;

        // Per longitudinal segment there are eight cross-section quads, each contributing six indices.
        // Cross-section edge 4->5 is the human-contact running surface and must face the local +normal.
        int sideIndicesPerSegment = ringSize * 6;
        for (int s = 0; s < segments; s++)
        {
            int indexOffset = s * sideIndicesPerSegment + 4 * 6;
            if (indexOffset + 2 >= triangles.Length)
            {
                result.runningSurfaceWindingValid = false;
                break;
            }
            int a = triangles[indexOffset];
            int b = triangles[indexOffset + 1];
            int c = triangles[indexOffset + 2];
            Vector3 n = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]).normalized;
            Vector3 center, tangent, normal;
            EvaluateFrame(d, (s + 0.5f) / segments, out center, out tangent, out normal);
            if (Vector3.Dot(n, normal) < 0.85f)
            {
                result.runningSurfaceWindingValid = false;
                break;
            }
        }

        if (float.IsPositiveInfinity(result.minimumTriangleAreaSquareMetres))
            result.minimumTriangleAreaSquareMetres = 0f;
        return result;
    }

    private static bool ValidateSectionDimensions(Vector3[] vertices, int start, ChuteContract contract)
    {
        if (start + 7 >= vertices.Length) return false;
        DimensionsMetres d = contract.dimensionsMetres;
        float tol = contract.qaRules.dimensionToleranceMetres;

        float outerWidth = Vector3.Distance(vertices[start + 0], vertices[start + 1]);
        float rightCap = Vector3.Distance(vertices[start + 2], vertices[start + 3]);
        float runningWidth = Vector3.Distance(vertices[start + 4], vertices[start + 5]);
        float leftCap = Vector3.Distance(vertices[start + 6], vertices[start + 7]);
        float rightInnerWall = Vector3.Distance(vertices[start + 3], vertices[start + 4]);
        float leftInnerWall = Vector3.Distance(vertices[start + 5], vertices[start + 6]);
        Vector3 bottomMid = (vertices[start + 0] + vertices[start + 1]) * 0.5f;
        Vector3 runningMid = (vertices[start + 4] + vertices[start + 5]) * 0.5f;
        float sheetThickness = Vector3.Distance(bottomMid, runningMid);

        return Approximately(outerWidth, d.chuteWidth, tol) &&
               Approximately(rightCap, d.sheetThickness, tol) &&
               Approximately(leftCap, d.sheetThickness, tol) &&
               Approximately(sheetThickness, d.sheetThickness, tol) &&
               Approximately(runningWidth, d.chuteWidth - 2f * d.sheetThickness, tol) &&
               Approximately(rightInnerWall, d.sideWallHeight, tol) &&
               Approximately(leftInnerWall, d.sideWallHeight, tol);
    }

    private static float SampleMinimumCurveRadius(DimensionsMetres d, int samples)
    {
        float dzdu = d.zEnd - d.zStart;
        float minimum = float.PositiveInfinity;
        for (int i = 0; i <= samples; i++)
        {
            float u = i / (float)samples;
            float dydu = HermiteDerivative(u, d.yStart, d.yEnd, d.startDyDu, d.endDyDu);
            float d2ydu2 = HermiteSecondDerivative(u, d.yStart, d.yEnd, d.startDyDu, d.endDyDu);
            double denominator = Math.Pow(dzdu * dzdu + dydu * dydu, 1.5);
            double curvature = denominator > 1e-12 ? Math.Abs(dzdu * d2ydu2) / denominator : 0.0;
            float radius = curvature > 1e-9 ? (float)(1.0 / curvature) : float.PositiveInfinity;
            minimum = Mathf.Min(minimum, radius);
        }
        return minimum;
    }

    private static void EvaluateFrame(
        DimensionsMetres d,
        float u,
        out Vector3 center,
        out Vector3 tangent,
        out Vector3 normal)
    {
        float z = Mathf.Lerp(d.zStart, d.zEnd, u);
        float y = Hermite(u, d.yStart, d.yEnd, d.startDyDu, d.endDyDu);
        float dydu = HermiteDerivative(u, d.yStart, d.yEnd, d.startDyDu, d.endDyDu);
        float dzdu = d.zEnd - d.zStart;
        center = new Vector3(0f, y, z);
        tangent = new Vector3(0f, dydu, dzdu).normalized;
        normal = Vector3.Cross(tangent, Vector3.right).normalized;
        if (normal.y < 0f) normal = -normal;
    }

    private static float Hermite(float t, float y0, float y1, float m0, float m1)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return (2f * t3 - 3f * t2 + 1f) * y0 +
               (t3 - 2f * t2 + t) * m0 +
               (-2f * t3 + 3f * t2) * y1 +
               (t3 - t2) * m1;
    }

    private static float HermiteDerivative(float t, float y0, float y1, float m0, float m1)
    {
        float t2 = t * t;
        return (6f * t2 - 6f * t) * y0 +
               (3f * t2 - 4f * t + 1f) * m0 +
               (-6f * t2 + 6f * t) * y1 +
               (3f * t2 - 2f * t) * m1;
    }

    private static float HermiteSecondDerivative(float t, float y0, float y1, float m0, float m1)
    {
        return (12f * t - 6f) * y0 +
               (6f * t - 4f) * m0 +
               (-12f * t + 6f) * y1 +
               (6f * t - 2f) * m1;
    }

    private static float[] BuildPerimeterCoordinate(Vector2[] section)
    {
        var result = new float[section.Length];
        for (int i = 1; i < section.Length; i++)
            result[i] = result[i - 1] + Vector2.Distance(section[i - 1], section[i]);
        return result;
    }

    private static List<int> TriangulateCounterClockwise(Vector2[] polygon)
    {
        var remaining = new List<int>();
        for (int i = 0; i < polygon.Length; i++) remaining.Add(i);
        var result = new List<int>((polygon.Length - 2) * 3);
        int guard = polygon.Length * polygon.Length;

        while (remaining.Count > 3 && guard-- > 0)
        {
            bool clipped = false;
            for (int i = 0; i < remaining.Count; i++)
            {
                int prev = remaining[(i - 1 + remaining.Count) % remaining.Count];
                int curr = remaining[i];
                int next = remaining[(i + 1) % remaining.Count];
                if (Cross2D(polygon[prev], polygon[curr], polygon[next]) <= 1e-10f) continue;

                bool contains = false;
                for (int j = 0; j < remaining.Count; j++)
                {
                    int candidate = remaining[j];
                    if (candidate == prev || candidate == curr || candidate == next) continue;
                    if (PointInTriangle(polygon[candidate], polygon[prev], polygon[curr], polygon[next]))
                    {
                        contains = true;
                        break;
                    }
                }
                if (contains) continue;

                result.Add(prev);
                result.Add(curr);
                result.Add(next);
                remaining.RemoveAt(i);
                clipped = true;
                break;
            }

            if (!clipped)
                throw new InvalidOperationException("Failed to triangulate slide chute U-section end cap.");
        }

        if (remaining.Count != 3)
            throw new InvalidOperationException("Slide chute U-section end-cap triangulation did not converge.");
        result.Add(remaining[0]);
        result.Add(remaining[1]);
        result.Add(remaining[2]);
        return result;
    }

    private static float Cross2D(Vector2 a, Vector2 b, Vector2 c)
    {
        return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
    }

    private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        const float eps = -1e-8f;
        float c0 = Cross2D(a, b, p);
        float c1 = Cross2D(b, c, p);
        float c2 = Cross2D(c, a, p);
        return c0 >= eps && c1 >= eps && c2 >= eps;
    }

    private static void AddQuad(List<int> triangles, int a, int b, int c, int d)
    {
        triangles.Add(a); triangles.Add(b); triangles.Add(c);
        triangles.Add(a); triangles.Add(c); triangles.Add(d);
    }

    private static void AddEdge(Dictionary<ulong, int> edgeUse, int a, int b)
    {
        uint min = (uint)Math.Min(a, b);
        uint max = (uint)Math.Max(a, b);
        ulong key = ((ulong)min << 32) | max;
        int count;
        edgeUse.TryGetValue(key, out count);
        edgeUse[key] = count + 1;
    }

    private static Mesh SaveOrReplaceMeshAsset(string path, Mesh generated)
    {
        generated.name = Path.GetFileNameWithoutExtension(path);
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(generated, path);
            return generated;
        }

        EditorUtility.CopySerialized(generated, existing);
        existing.name = Path.GetFileNameWithoutExtension(path);
        EditorUtility.SetDirty(existing);
        UnityEngine.Object.DestroyImmediate(generated);
        return existing;
    }

    private static void AssertLegacyChuteRendererDisabled(
        Transform tier, string childName, List<string> errors, int lod, bool allowMissing = false)
    {
        Transform child = tier.Find(childName);
        if (child == null)
        {
            if (!allowMissing) errors.Add($"LOD{lod}: expected legacy chute child {childName} missing.");
            return;
        }
        Renderer renderer = child.GetComponent<Renderer>();
        if (renderer != null && renderer.enabled)
            errors.Add($"LOD{lod}: legacy open/box chute renderer is still enabled: {childName}.");
    }

    private static Material FindMaterialRecursive(Transform root, string materialName)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            foreach (Material material in renderer.sharedMaterials)
                if (material != null && string.Equals(material.name, materialName, StringComparison.Ordinal))
                    return material;
        }
        return null;
    }

    private static Transform RequireSlide()
    {
        GameObject slide = GameObject.Find(SlideName);
        if (slide == null) throw new InvalidOperationException($"{SlideName} missing.");
        return slide.transform;
    }

    private static Transform RequireDirectChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child == null)
            throw new InvalidOperationException($"Required child missing: {parent.name}/{name}");
        return child;
    }

    private static T RequireComponent<T>(Transform transform) where T : Component
    {
        T component = transform.GetComponent<T>();
        if (component == null)
            throw new InvalidOperationException($"{transform.name} missing required {typeof(T).Name}.");
        return component;
    }

    private static bool Approximately(float actual, float expected, float tolerance)
    {
        return Mathf.Abs(actual - expected) <= tolerance;
    }

    private static ChuteContract LoadAndValidateContract()
    {
        ChuteContract contract = LoadContract();
        List<string> errors = ValidateContract(contract);
        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Slide chute fabrication contract FAILED:\n - " + string.Join("\n - ", errors));
        return contract;
    }

    private static ChuteContract LoadContract()
    {
        if (!File.Exists(ContractPath))
            throw new InvalidOperationException($"Missing required slide chute fabrication metadata: {ContractPath}");
        string json = File.ReadAllText(ContractPath);
        ChuteContract contract = JsonUtility.FromJson<ChuteContract>(json);
        if (contract == null)
            throw new InvalidOperationException("Unable to parse slide chute fabrication contract.");
        return contract;
    }

    private static List<string> ValidateContract(ChuteContract contract)
    {
        var errors = new List<string>();
        if (contract.dimensionsMetres == null) errors.Add("dimensionsMetres missing.");
        if (contract.qaRules == null) errors.Add("qaRules missing.");
        if (contract.renderVerification == null) errors.Add("renderVerification missing.");
        if (errors.Count > 0) return errors;

        DimensionsMetres d = contract.dimensionsMetres;
        QaRules q = contract.qaRules;
        if (d.chuteWidth < 0.45f || d.chuteWidth > 1.20f) errors.Add("chuteWidth outside one-person slide envelope.");
        if (d.sheetThickness + 1e-6f < q.minimumSheetThicknessMetres) errors.Add("sheetThickness below hard minimum.");
        if (d.sideWallHeight < 0.10f) errors.Add("sideWallHeight too small for the locked fabricated envelope.");
        if (d.zEnd <= d.zStart || d.yStart <= d.yEnd) errors.Add("chute descent endpoints invalid.");
        if (d.lodSegments == null || d.lodSegments.Length != q.requiredLodCount) errors.Add("lodSegments must define all four LODs.");
        else
        {
            for (int i = 0; i < d.lodSegments.Length; i++)
            {
                if (d.lodSegments[i] < 8) errors.Add($"LOD{i} segment count below safe silhouette floor.");
                if (i > 0 && d.lodSegments[i] >= d.lodSegments[i - 1])
                    errors.Add("LOD segment counts must decrease monotonically.");
            }
        }
        if (q.maximumBoundaryEdgeCount != 0 || q.maximumNonManifoldEdgeCount != 0)
            errors.Add("watertight topology hard limits must remain zero.");
        if (!string.Equals(q.requiredMaterialName, "PBR_SlideStainless", StringComparison.Ordinal))
            errors.Add("requiredMaterialName must remain PBR_SlideStainless.");
        if (contract.renderVerification.visualFidelityPointsAwarded != 0)
            errors.Add("Source contract must award zero Visual Fidelity points.");
        if (!string.Equals(contract.renderVerification.status, "PENDING_UNITY_RUNTIME", StringComparison.Ordinal))
            errors.Add("renderVerification must remain PENDING_UNITY_RUNTIME before real pixels exist.");

        string json = File.ReadAllText(ContractPath);
        string[] requiredTokens =
        {
            "\"albedo_linear_rgb\"", "\"roughness_range\"", "\"metallic_range\"",
            "\"normalScale\"", "\"microstructure\"", "\"wetness\"", "\"uvAging\"",
            "\"angularResponse\"", "\"geometryVsMaterialDetail\"", "\"lodPolicy\"",
            "\"visualFidelityPointsAwarded\": 0", "PENDING_UNITY_RUNTIME"
        };
        foreach (string token in requiredTokens)
            if (!json.Contains(token, StringComparison.Ordinal))
                errors.Add($"contract missing required material/construction token: {token}");
        return errors;
    }

    private static void WriteRuntimeReport(ValidationSummary summary, bool lodBindingVerified)
    {
        var report = new RuntimeReport
        {
            schemaVersion = "1.0",
            generatedUtc = DateTime.UtcNow.ToString("O"),
            unityVersion = Application.unityVersion,
            visualFidelityStatus = "UNSCORED_RENDER_REVIEW_REQUIRED",
            visualFidelityPointsAwarded = 0,
            renderVerification = "PENDING_NATIVE_4K_REVIEW",
            minimumCurveRadiusMetres = summary.minimumCurveRadiusMetres,
            maximumBoundaryEdgeCount = summary.maximumBoundaryEdgeCount,
            maximumNonManifoldEdgeCount = summary.maximumNonManifoldEdgeCount,
            minimumTriangleAreaSquareMetres = summary.minimumTriangleAreaSquareMetres,
            lodBindingVerified = lodBindingVerified,
        };
        File.WriteAllText(RuntimeReportPath, JsonUtility.ToJson(report, true));
        AssetDatabase.ImportAsset(RuntimeReportPath);
    }

    [Serializable]
    private sealed class ChuteContract
    {
        public string schemaVersion;
        public string id;
        public DimensionsMetres dimensionsMetres;
        public QaRules qaRules;
        public RenderVerification renderVerification;
    }

    [Serializable]
    private sealed class DimensionsMetres
    {
        public float chuteWidth;
        public float sheetThickness;
        public float sideWallHeight;
        public float zStart;
        public float zEnd;
        public float yStart;
        public float yEnd;
        public float startDyDu;
        public float endDyDu;
        public float minimumAllowedCurveRadius;
        public int[] lodSegments;
    }

    [Serializable]
    private sealed class QaRules
    {
        public int requiredLodCount;
        public int generatedColliderCount;
        public float minimumSheetThicknessMetres;
        public float dimensionToleranceMetres;
        public float positionWeldToleranceMetres;
        public int maximumBoundaryEdgeCount;
        public int maximumNonManifoldEdgeCount;
        public float minimumTriangleAreaSquareMetres;
        public float minimumCurveRadiusMetres;
        public string requiredMaterialName;
        public bool newRenderersMustBeLodBound;
    }

    [Serializable]
    private sealed class RenderVerification
    {
        public string status;
        public int visualFidelityPointsAwarded;
    }

    private sealed class MeshSummary
    {
        public int boundaryEdges;
        public int nonManifoldEdges;
        public float minimumTriangleAreaSquareMetres;
        public bool sectionDimensionsValid;
        public bool endCapWindingValid;
        public bool runningSurfaceWindingValid;
    }

    private sealed class ValidationSummary
    {
        public float minimumCurveRadiusMetres;
        public int maximumBoundaryEdgeCount;
        public int maximumNonManifoldEdgeCount;
        public float minimumTriangleAreaSquareMetres;
    }

    [Serializable]
    private sealed class RuntimeReport
    {
        public string schemaVersion;
        public string generatedUtc;
        public string unityVersion;
        public string visualFidelityStatus;
        public int visualFidelityPointsAwarded;
        public string renderVerification;
        public float minimumCurveRadiusMetres;
        public int maximumBoundaryEdgeCount;
        public int maximumNonManifoldEdgeCount;
        public float minimumTriangleAreaSquareMetres;
        public bool lodBindingVerified;
    }
}
