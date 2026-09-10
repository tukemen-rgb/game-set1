using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Rebinds LOD renderer sets after physical-refinement and notice-board display-case meshes replace/add
/// first-pass renderers. LODGroup stores explicit Renderer references, so adding a refined mesh after
/// SetLODs would otherwise leave it outside the LOD system and create an all-distance renderer / visible
/// transition defect. Formal validation also re-runs notice-board construction and printed-UV QA so the
/// completed prepared scene proves cover/paper/hardware geometry and print mapping survived.
/// </summary>
public static class QualityBlockParkFurnitureLodRebind
{
    private static readonly HashSet<string> SlideExcluded = new HashSet<string>(StringComparer.Ordinal)
    {
        "ChuteSheet", "ChuteLipL", "ChuteLipR", "ChuteRunout"
    };

    private static readonly HashSet<string> LampExcluded = new HashSet<string>(StringComparer.Ordinal)
    {
        "Diffuser"
    };

    [MenuItem("NewTown/Quality/Rebind Park Furniture Refined LOD Renderers")]
    public static void RebindAndValidate()
    {
        Rebind("HD_Slide", SlideExcluded);
        Rebind("HD_Lamp", LampExcluded);
        Rebind("HD_Bench", null);
        Rebind("HD_NoticeBoard", null);

        // Rebind happens before the shared microdetail pass converts generated meshes to metre-space UVs.
        // Validate only renderer membership/construction here; printed-UV QA is intentionally deferred
        // until after microdetail normalization in the save chain.
        ValidateAssembly("HD_Slide", "PhysicalChute", SlideExcluded);
        ValidateAssembly("HD_Lamp", "PhysicalDiffuser", LampExcluded);
        ValidateAssembly("HD_Bench", null, null);
        ValidateAssembly("HD_NoticeBoard", null, null);
        QualityBlockNoticeBoardDisplayCaseQA.ValidateOpenScene();
        Debug.Log("Park/street furniture LOD renderer sets rebound after physical/detail refinement.");
    }

    [MenuItem("NewTown/QA/Validate Park Furniture Refined LOD Binding")]
    public static void ValidateOpenScene()
    {
        ValidateAssembly("HD_Slide", "PhysicalChute", SlideExcluded);
        ValidateAssembly("HD_Lamp", "PhysicalDiffuser", LampExcluded);
        ValidateAssembly("HD_Bench", null, null);
        ValidateAssembly("HD_NoticeBoard", null, null);
        QualityBlockNoticeBoardDisplayCaseQA.ValidateOpenScene();
        QualityBlockNoticeBoardPrintedUvQA.ValidateOpenScene();
    }

    private static void Rebind(string rootName, HashSet<string> excluded)
    {
        GameObject root = GameObject.Find(rootName);
        if (root == null) throw new InvalidOperationException($"LOD rebind root missing: {rootName}");
        LODGroup group = root.GetComponent<LODGroup>();
        if (group == null) throw new InvalidOperationException($"LODGroup missing: {rootName}");

        LOD[] old = group.GetLODs();
        if (old.Length != 4) throw new InvalidOperationException($"{rootName} requires exactly four LODs before rebind.");
        var rebound = new LOD[4];
        for (int i = 0; i < 4; i++)
        {
            Transform tier = root.transform.Find($"LOD{i}");
            if (tier == null) throw new InvalidOperationException($"{rootName}/LOD{i} missing.");
            Renderer[] renderers = tier.GetComponentsInChildren<Renderer>(true)
                .Where(r => excluded == null || !excluded.Contains(r.gameObject.name))
                .ToArray();
            if (renderers.Length == 0) throw new InvalidOperationException($"{rootName}/LOD{i} has no renderers after rebind.");
            rebound[i] = new LOD(old[i].screenRelativeTransitionHeight, renderers)
            {
                fadeTransitionWidth = old[i].fadeTransitionWidth
            };
        }
        group.SetLODs(rebound);
        group.fadeMode = LODFadeMode.CrossFade;
        group.animateCrossFading = true;
        group.RecalculateBounds();
    }

    private static void ValidateAssembly(string rootName, string requiredRefinedRendererName, HashSet<string> excluded)
    {
        GameObject root = GameObject.Find(rootName);
        if (root == null) throw new InvalidOperationException($"LOD validation root missing: {rootName}");
        LODGroup group = root.GetComponent<LODGroup>();
        LOD[] lods = group != null ? group.GetLODs() : null;
        if (lods == null || lods.Length != 4)
            throw new InvalidOperationException($"{rootName} does not have four rebound LODs.");
        if (!group.animateCrossFading || group.fadeMode != LODFadeMode.CrossFade)
            throw new InvalidOperationException($"{rootName} must use animated LOD cross-fade.");

        for (int i = 0; i < lods.Length; i++)
        {
            Renderer[] rs = lods[i].renderers ?? Array.Empty<Renderer>();
            if (rs.Length == 0) throw new InvalidOperationException($"{rootName} LOD{i} has an empty renderer set.");
            if (requiredRefinedRendererName != null && !rs.Any(r => r != null && r.gameObject.name == requiredRefinedRendererName))
                throw new InvalidOperationException($"{rootName} LOD{i} is not bound to {requiredRefinedRendererName}.");
            if (excluded != null && rs.Any(r => r != null && excluded.Contains(r.gameObject.name)))
                throw new InvalidOperationException($"{rootName} LOD{i} still binds oversized first-pass renderer(s). ");
            if (rs.Any(r => r == null))
                throw new InvalidOperationException($"{rootName} LOD{i} has missing renderer references.");
        }
    }
}
