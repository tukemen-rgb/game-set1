"""Refine the *actual* window/service facade already present in the latest repaired hero.

This is an integration refinement, not a second facade owner. It deliberately rejects the
simpler ExteriorFacadeSet-v2 panel grid as the integration target because the current hero
uses PaintedFiberCementFacadeWindowService3600x2700 with real window, vent and AC service
openings. Replacing that with the simpler vent-only wall would close hero apertures.

What this run changes, and only this:
- ExistingHero_Facade_Panel_*: same physical envelope/openings, but real front-edge bevels
  at MASTER/LOD0/LOD1/LOD2 and one facade-wide non-repeating PBR material field.
- ExistingHero_Facade_*_Seal: same X/Y joint envelope and material owner, but relocated
  from behind the 16 mm board to a 3 mm sealant body with a 0.4 mm exterior crown.

What this run preserves byte-for-byte in memory:
- every non-facade geometry;
- facade fixing heads, backer rods, window/vent/AC reveals;
- all previous drainage, guardrail, soffit, AC, window and other structural repair work.

The neutral warm facade color and bevel dimensions are art-direction / construction
assumptions, not claims identifying a historical Japanese product SKU. No sunlight,
shadow, wetness, random grime or highlight is baked into textures.
"""
from __future__ import annotations

from pathlib import Path
import argparse, copy, hashlib, json
import numpy as np
import trimesh
from PIL import Image, ImageFilter

LEVELS = ("MASTER", "LOD0", "LOD1", "LOD2", "LOD3")
BEVEL = {
    "MASTER": (0.0026, 0.0012),
    "LOD0": (0.0026, 0.0012),
    "LOD1": (0.0022, 0.0010),
    "LOD2": (0.0016, 0.0008),
    "LOD3": (0.0, 0.0),
}
FACADE_W = 3.6
FACADE_H = 2.7
PANEL_T = 0.016
SEAL_BODY_DEPTH = 0.0030
SEAL_CROWN = 0.0004
MATERIAL_SRGB = np.asarray([0.72, 0.70, 0.64], dtype=np.float32)
MATERIAL_ROUGHNESS_MEAN = 0.72
MATERIAL_NORMAL_SCALE = 0.060

EXPECTED_OPENINGS = {
    "window": {"x": [-1.59, 0.35], "y": [-0.83, 1.13]},
    "vent": {"x": [0.77, 0.99], "y": [0.61, 0.83]},
    "ac_service": {"x": [0.72, 0.86], "y": [-0.24, -0.10]},
}


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for block in iter(lambda: f.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()


def array_digest(a: np.ndarray) -> bytes:
    a = np.ascontiguousarray(a)
    return a.dtype.str.encode() + str(a.shape).encode() + a.tobytes()


def geometry_digest(items: list[tuple[str, trimesh.Trimesh]]) -> str:
    h = hashlib.sha256()
    for name, g in sorted(items, key=lambda x: x[0]):
        h.update(name.encode("utf-8"))
        h.update(array_digest(np.asarray(g.vertices)))
        h.update(array_digest(np.asarray(g.faces)))
    return h.hexdigest()


def normalized_field(rng, size: int, blur_radius: float) -> np.ndarray:
    src = rng.normal(0, 1, (size, size))
    gray = np.uint8(np.clip(src * 28 + 128, 0, 255))
    img = Image.fromarray(gray, "L").filter(ImageFilter.GaussianBlur(blur_radius))
    out = np.asarray(img, dtype=np.float32) / 255.0 - 0.5
    return out / max(float(out.std()), 1e-6)


def create_facade_textures(out_dir: Path):
    out_dir.mkdir(parents=True, exist_ok=True)
    rng = np.random.default_rng(17092601)
    size = 1024
    coarse = normalized_field(rng, size, 48)
    medium = normalized_field(rng, size, 14)
    fine = rng.normal(0, 1, (size, size)).astype(np.float32)
    fine /= max(float(fine.std()), 1e-6)
    field = 0.48 * coarse + 0.33 * medium + 0.19 * fine
    field -= float(field.mean())
    field /= max(float(field.std()), 1e-6)

    albedo = np.clip(MATERIAL_SRGB[None, None, :] * (1 + 0.012 * field[:, :, None]), 0, 1)
    rough = np.clip(MATERIAL_ROUGHNESS_MEAN + 0.025 * (0.62 * medium + 0.38 * fine), 0.60, 0.82)
    mr = np.zeros((size, size, 3), dtype=np.uint8)
    mr[:, :, 1] = np.uint8(np.round(rough * 255))
    mr[:, :, 2] = 0

    height = 0.58 * medium + 0.42 * fine
    gy, gx = np.gradient(height)
    n = np.dstack([-gx * MATERIAL_NORMAL_SCALE, -gy * MATERIAL_NORMAL_SCALE, np.ones_like(gx)])
    n /= np.maximum(np.linalg.norm(n, axis=2)[:, :, None], 1e-8)
    normal = np.uint8(np.round(np.clip(n * 0.5 + 0.5, 0, 1) * 255))

    bc = Image.fromarray(np.uint8(np.round(albedo * 255)), "RGB")
    mi = Image.fromarray(mr, "RGB")
    ni = Image.fromarray(normal, "RGB")
    bc_path = out_dir / "PaintedFiberCement_WindowService_base.png"
    mr_path = out_dir / "PaintedFiberCement_WindowService_mr.png"
    nm_path = out_dir / "PaintedFiberCement_WindowService_normal.png"
    bc.save(bc_path); mi.save(mr_path); ni.save(nm_path)
    mat = trimesh.visual.material.PBRMaterial(
        name="PaintedFiberCement",
        baseColorTexture=bc,
        metallicRoughnessTexture=mi,
        normalTexture=ni,
        metallicFactor=1.0,
        roughnessFactor=1.0,
        doubleSided=False,
    )
    return mat, {
        "base": bc_path.name, "metallicRoughness": mr_path.name, "normal": nm_path.name,
        "baseSHA256": sha256_file(bc_path), "mrSHA256": sha256_file(mr_path), "normalSHA256": sha256_file(nm_path),
        "albedoSRGBMean": MATERIAL_SRGB.tolist(), "metallic": 0.0,
        "roughnessMean": MATERIAL_ROUGHNESS_MEAN, "normalScaleProxy": MATERIAL_NORMAL_SCALE,
        "textureResolution": [size, size], "coverageMetres": [FACADE_W, FACADE_H],
        "microstructure": "unique facade-wide isotropic paint/cement variation; no tile inside UV domain",
        "wetness": 0.0, "uvAging": "none baked", "fresnel": "dielectric F0 approximately 0.04",
    }


def facade_uv(vertices: np.ndarray) -> np.ndarray:
    return np.column_stack(((vertices[:, 0] + FACADE_W / 2) / FACADE_W,
                            (vertices[:, 1] + FACADE_H / 2) / FACADE_H))


def bevel_box_from_bounds(bounds: np.ndarray, bevel_width: float, bevel_depth: float,
                           name: str, material) -> trimesh.Trimesh:
    b = np.asarray(bounds, dtype=float)
    x0, y0, z0 = b[0]; x1, y1, z1 = b[1]
    w, h, t = x1 - x0, y1 - y0, z1 - z0
    if bevel_width <= 0 or bevel_depth <= 0:
        m = trimesh.creation.box(extents=[w, h, t])
        m.apply_translation([(x0+x1)/2, (y0+y1)/2, (z0+z1)/2])
    else:
        if 2 * bevel_width >= min(w, h):
            raise ValueError(f"bevel too large for {name}: {w} x {h}")
        if bevel_depth >= t:
            raise ValueError(f"bevel depth >= panel thickness for {name}")
        zs = [z0, z1 - bevel_depth, z1]
        rings = [
            [(x0,y0,zs[0]),(x1,y0,zs[0]),(x1,y1,zs[0]),(x0,y1,zs[0])],
            [(x0,y0,zs[1]),(x1,y0,zs[1]),(x1,y1,zs[1]),(x0,y1,zs[1])],
            [(x0+bevel_width,y0+bevel_width,zs[2]),
             (x1-bevel_width,y0+bevel_width,zs[2]),
             (x1-bevel_width,y1-bevel_width,zs[2]),
             (x0+bevel_width,y1-bevel_width,zs[2])],
        ]
        vertices = np.asarray([p for ring in rings for p in ring], dtype=float)
        faces = [[0,2,1],[0,3,2], [8,9,10],[8,10,11]]
        for r0, r1 in ((0,4),(4,8)):
            for j in range(4):
                k=(j+1)%4; a,bx,c,d=r0+j,r0+k,r1+k,r1+j
                faces += [[a,bx,c],[a,c,d]]
        m = trimesh.Trimesh(vertices=vertices, faces=np.asarray(faces, dtype=np.int64), process=False)
        m.fix_normals(multibody=True)
    m.metadata.update(name=name, material="PaintedFiberCement",
                      bevelWidthM=float(bevel_width), bevelDepthM=float(bevel_depth),
                      sourceEnvelope=b.tolist())
    m.visual = trimesh.visual.TextureVisuals(uv=facade_uv(m.vertices), material=material)
    return m


def copy_material_with_uv(source: trimesh.Trimesh, target: trimesh.Trimesh, uv_scale=0.12):
    mat = copy.deepcopy(getattr(source.visual, "material", None))
    if mat is None:
        raise ValueError(f"source mesh {source.metadata.get('name')} has no material")
    target.visual = trimesh.visual.TextureVisuals(uv=target.vertices[:, [0, 1]] / uv_scale, material=mat)


def rebuilt_seal(source: trimesh.Trimesh, name: str) -> trimesh.Trimesh:
    b = source.bounds.copy()
    x0,y0,_ = b[0]; x1,y1,_ = b[1]
    z1 = SEAL_CROWN
    z0 = z1 - SEAL_BODY_DEPTH
    m = trimesh.creation.box(extents=[x1-x0, y1-y0, z1-z0])
    m.apply_translation([(x0+x1)/2, (y0+y1)/2, (z0+z1)/2])
    m.metadata.update(name=name, material="JointSealant", sourceXYBounds=b[:, :2].tolist(),
                      sealBodyDepthM=SEAL_BODY_DEPTH, exteriorCrownM=SEAL_CROWN)
    copy_material_with_uv(source, m)
    return m


def validate_closed(mesh: trimesh.Trimesh, name: str):
    if not np.isfinite(mesh.vertices).all() or not np.isfinite(mesh.vertex_normals).all():
        raise AssertionError(f"non-finite geometry: {name}")
    if not np.all(mesh.area_faces > 1e-14):
        raise AssertionError(f"degenerate face: {name}")
    q = mesh.copy(); q.merge_vertices(digits_vertex=9); q.remove_unreferenced_vertices(); q.fix_normals(multibody=True)
    if not q.is_watertight or not q.is_winding_consistent or not q.volume > 0:
        raise AssertionError(f"closed-volume validation failed: {name}")
    _, counts = np.unique(q.edges_sorted, axis=0, return_counts=True)
    if not np.all(counts == 2):
        raise AssertionError(f"edge incidence !=2: {name}")
    return {"name":name,"triangles":int(len(mesh.faces)),"volumeM3":float(q.volume),
            "watertight":True,"windingConsistent":True,"edgeIncidence2":True}


def is_panel(name): return name.startswith("ExistingHero_Facade_Panel_")
def is_seal(name): return name.startswith("ExistingHero_Facade_") and name.endswith("_Seal")
def is_facade(name): return name.startswith("ExistingHero_Facade_")


def opening_metrics_from_panel_bounds(panel_bounds: list[np.ndarray]):
    result = {}
    for key, op in EXPECTED_OPENINGS.items():
        ox0,ox1=op["x"]; oy0,oy1=op["y"]
        intersects=[]
        for b in panel_bounds:
            px0,py0=b[0,0],b[0,1]; px1,py1=b[1,0],b[1,1]
            ix=max(0.0,min(px1,ox1)-max(px0,ox0)); iy=max(0.0,min(py1,oy1)-max(py0,oy0))
            if ix>1e-9 and iy>1e-9: intersects.append(float(ix*iy))
        total=float(sum(intersects))
        result[key]={"boundsMetres":op,"panelOverlapAreaM2":total,"float32AreaToleranceM2":1e-7,"preserved":total <= 1e-7}
    return result


def scene_export(scene: trimesh.Scene, path: Path):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(scene.export(file_type="glb"))
    re = trimesh.load(path, force="scene", process=False)
    tri = sum(len(g.faces) for g in scene.geometry.values())
    if sum(len(g.faces) for g in re.geometry.values()) != tri:
        raise AssertionError(f"GLB triangle mismatch {path}")
    if not np.allclose(re.bounds, scene.bounds, atol=1e-6):
        raise AssertionError(f"GLB bounds mismatch {path}")
    return re, tri


def obj_export(scene: trimesh.Scene, directory: Path, stem: str):
    directory.mkdir(parents=True, exist_ok=True)
    text, files = trimesh.exchange.obj.export_obj(scene, include_normals=True, include_texture=True, return_texture=True)
    obj = directory / f"{stem}.obj"
    obj.write_text(text, encoding="utf-8")
    for fn,data in files.items():
        p=directory/fn
        p.write_text(data,encoding="utf-8") if isinstance(data,str) else p.write_bytes(data)
    re=trimesh.load(obj,force="scene",process=False)
    return obj, sum(len(g.faces) for g in re.geometry.values())


def refine_level(input_path: Path, level: str, output_root: Path, panel_material):
    source = trimesh.load(input_path, force="scene", process=False)
    if any(not np.allclose(source.graph[node][0], np.eye(4), atol=1e-9) for node in source.graph.nodes_geometry):
        raise AssertionError("unexpected non-identity node transform; explicit transform preservation required")

    source_nonfac = [(n,g) for n,g in source.geometry.items() if not is_facade(n)]
    source_facade_unchanged = [(n,g) for n,g in source.geometry.items() if is_facade(n) and not is_panel(n) and not is_seal(n)]
    nonfac_digest = geometry_digest(source_nonfac)
    facade_unchanged_digest = geometry_digest(source_facade_unchanged)

    bevel_w, bevel_d = BEVEL[level]
    target = trimesh.Scene()
    refined_facade = trimesh.Scene()
    new_validations=[]
    panel_bounds_before=[]; panel_bounds_after=[]
    replaced_panels=[]; replaced_seals=[]

    for name,g in source.geometry.items():
        if is_panel(name):
            panel_bounds_before.append(g.bounds.copy())
            q=bevel_box_from_bounds(g.bounds,bevel_w,bevel_d,name,panel_material)
            new_validations.append(validate_closed(q,name))
            panel_bounds_after.append(q.bounds.copy())
            target.add_geometry(q,node_name=name,geom_name=name)
            refined_facade.add_geometry(q.copy(),node_name=name,geom_name=name)
            replaced_panels.append({"name":name,"beforeTriangles":int(len(g.faces)),"afterTriangles":int(len(q.faces)),
                                    "boundsPreserved":bool(np.allclose(g.bounds,q.bounds,atol=1e-9))})
        elif is_seal(name):
            q=rebuilt_seal(g,name); new_validations.append(validate_closed(q,name))
            target.add_geometry(q,node_name=name,geom_name=name)
            refined_facade.add_geometry(q.copy(),node_name=name,geom_name=name)
            replaced_seals.append({"name":name,"beforeBounds":g.bounds.tolist(),"afterBounds":q.bounds.tolist(),
                                  "xyBoundsPreserved":bool(np.allclose(g.bounds[:,:2],q.bounds[:,:2],atol=1e-9)),
                                  "beforeTriangles":int(len(g.faces)),"afterTriangles":int(len(q.faces))})
        else:
            q=g.copy()
            target.add_geometry(q,node_name=name,geom_name=name)
            if is_facade(name): refined_facade.add_geometry(q.copy(),node_name=name,geom_name=name)

    if geometry_digest([(n,g) for n,g in target.geometry.items() if not is_facade(n)]) != nonfac_digest:
        raise AssertionError("non-facade geometry changed in memory")
    if geometry_digest([(n,g) for n,g in target.geometry.items() if is_facade(n) and not is_panel(n) and not is_seal(n)]) != facade_unchanged_digest:
        raise AssertionError("unintended facade geometry changed in memory")

    if len(panel_bounds_before)!=10 or len(panel_bounds_after)!=10:
        raise AssertionError(f"expected 10 window-service panel solids, got {len(panel_bounds_before)}")
    for a,b in zip(panel_bounds_before,panel_bounds_after):
        if not np.allclose(a,b,atol=1e-9): raise AssertionError("panel envelope changed")
    openings=opening_metrics_from_panel_bounds(panel_bounds_after)
    if not all(x["preserved"] for x in openings.values()): raise AssertionError(f"opening intrusion: {openings}")

    out_integrated=output_root/"integrated"/f"BalconyExteriorHeroDrainedStructuralRepairFacadeRefined3600_{level}.glb"
    _, tri=scene_export(target,out_integrated)
    out_facade=output_root/"facade"/f"PaintedFiberCementFacadeWindowService3600x2700_Refined_{level}.glb"
    _, facade_tri=scene_export(refined_facade,out_facade)
    obj_path,obj_tri=obj_export(refined_facade,output_root/"facade"/f"PaintedFiberCementFacadeWindowService3600x2700_Refined_{level}_OBJ",
                                f"PaintedFiberCementFacadeWindowService3600x2700_Refined_{level}")
    if obj_tri != facade_tri: raise AssertionError(f"OBJ triangle mismatch {level}: {obj_tri} != {facade_tri}")

    return {
        "level":level,
        "input":str(input_path),"inputSHA256":sha256_file(input_path),
        "integratedOutput":str(out_integrated),"integratedSHA256":sha256_file(out_integrated),
        "integratedTriangles":int(tri),"integratedGeometryCount":len(target.geometry),
        "facadeOutput":str(out_facade),"facadeSHA256":sha256_file(out_facade),
        "facadeTriangles":int(facade_tri),"facadeGeometryCount":len(refined_facade.geometry),
        "facadeOBJ":str(obj_path),"facadeOBJTriangles":int(obj_tri),
        "sourceNonFacadeGeometryDigest":nonfac_digest,
        "targetNonFacadeGeometryDigestInMemory":geometry_digest([(n,g) for n,g in target.geometry.items() if not is_facade(n)]),
        "sourceUnchangedFacadeGeometryDigest":facade_unchanged_digest,
        "targetUnchangedFacadeGeometryDigestInMemory":geometry_digest([(n,g) for n,g in target.geometry.items() if is_facade(n) and not is_panel(n) and not is_seal(n)]),
        "unaffectedGeometryExactInMemory":True,
        "panelCount":len(replaced_panels),"replacedPanels":replaced_panels,
        "sealCount":len(replaced_seals),"replacedSeals":replaced_seals,
        "newClosedMeshValidation":new_validations,
        "openings":openings,
        "bevelWidthMm":bevel_w*1000,"bevelDepthMm":bevel_d*1000,
        "sealCrownMm":SEAL_CROWN*1000,"sealBodyDepthMm":SEAL_BODY_DEPTH*1000,
        "glbRoundtrip":True,"facadeObjTriangleRoundtrip":True,
        "bounds":target.bounds.tolist(),
    }


def run(input_dir: Path, output_root: Path):
    if output_root.exists():
        raise FileExistsError(output_root)
    output_root.mkdir(parents=True)
    panel_material, material_meta=create_facade_textures(output_root/"textures")
    records=[]
    for level in LEVELS:
        p=input_dir/f"BalconyExteriorHeroDrainedStructuralRepair3600_{level}.glb"
        if not p.exists(): raise FileNotFoundError(p)
        rec=refine_level(p,level,output_root,panel_material)
        records.append(rec)
        print(level, rec["integratedTriangles"], rec["facadeTriangles"], flush=True)

    counts=[r["integratedTriangles"] for r in records]
    facade_counts=[r["facadeTriangles"] for r in records]
    if not all(a>b for a,b in zip(counts,counts[1:])): raise AssertionError(f"integrated LOD not strict {counts}")
    if not all(a>b for a,b in zip(facade_counts,facade_counts[1:])): raise AssertionError(f"facade LOD not strict {facade_counts}")

    metadata={
        "schema":1,"units":"metres",
        "owner":"PaintedFiberCementFacadeWindowService3600x2700 / refinement layer",
        "integrationBase":"BalconyExteriorHeroDrainedStructuralRepair3600",
        "materials":{"PaintedFiberCement":material_meta},
        "records":records,
        "visualFidelity":{"authority":"UnityProject/Assets/QA/visual_fidelity_gate.json","score":None,"pass":False,"pointsAwarded":0,
                          "status":"UNSCORED_UNTIL_REAL_UNITY_3840x2160_RENDER"},
        "implementationReadiness":{"lastRecorded":93,"recomputedThisRun":False},
        "unityCompile":False,"unityImport":False,"unityRender":False,"temporalLOD":False,
    }
    (output_root/"WindowServiceFacadeSurfaceRefinement.execution.json").write_text(json.dumps(metadata,indent=2)+"\n",encoding="utf-8")
    return metadata


if __name__ == "__main__":
    ap=argparse.ArgumentParser()
    ap.add_argument("--input-dir",type=Path,required=True)
    ap.add_argument("--output",type=Path,required=True)
    args=ap.parse_args()
    run(args.input_dir,args.output)
