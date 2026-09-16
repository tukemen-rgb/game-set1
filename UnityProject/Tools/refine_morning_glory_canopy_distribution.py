"""Refine the existing morning-glory owner by breaking canopy repetition physically.

Input is the already-generated botanical refinement folder containing
`MorningGloryTrellis_<LEVEL>_botanical.glb`. Output intentionally keeps those
filenames inside a separate folder so the existing
`build_balcony_morning_glory_botanical_integration.py` can consume it without a
second integration owner.

This stage changes vertex positions only: no leaves, veins, flowers, stems,
trellis parts, materials or triangles are added or removed. Leaf attachment
zones are held fixed while the distal leaf shell and its existing geometric
veins are smoothly reoriented and modestly rescaled. The deterministic field is
height/exposure based, not camera based.

This script is authoring source. Its presence is not evidence that it was run,
imported by Unity, rendered by Unity, or passed Visual Fidelity.
"""
from __future__ import annotations

from pathlib import Path
import argparse
import hashlib
import json
import math

import numpy as np
import trimesh
from scipy.spatial import cKDTree

LEVELS = ("MASTER", "LOD0", "LOD1", "LOD2", "LOD3")
CONFIG = {
    "MASTER": dict(max_yaw_deg=18.0, max_radial_tilt_deg=10.0, max_sun_tilt_deg=8.0, max_roll_deg=6.5, scale_span=0.090, anchor_hold=0.11, blend_end=0.44),
    "LOD0":   dict(max_yaw_deg=18.0, max_radial_tilt_deg=10.0, max_sun_tilt_deg=8.0, max_roll_deg=6.5, scale_span=0.090, anchor_hold=0.11, blend_end=0.44),
    "LOD1":   dict(max_yaw_deg=16.0, max_radial_tilt_deg=9.0,  max_sun_tilt_deg=7.0, max_roll_deg=5.5, scale_span=0.080, anchor_hold=0.12, blend_end=0.45),
    "LOD2":   dict(max_yaw_deg=13.0, max_radial_tilt_deg=7.5,  max_sun_tilt_deg=5.5, max_roll_deg=4.5, scale_span=0.065, anchor_hold=0.14, blend_end=0.47),
    "LOD3":   dict(max_yaw_deg=10.0, max_radial_tilt_deg=6.0,  max_sun_tilt_deg=4.0, max_roll_deg=3.5, scale_span=0.050, anchor_hold=0.16, blend_end=0.50),
}
# Local-space lookdev assumption for a midsummer exterior exposure. This is not
# a claim about exact site azimuth/date/time and can be overridden on the CLI.
DEFAULT_SUN_DIR = np.array([0.42, 0.78, 0.46], dtype=float)
GOLDEN_ANGLE = math.pi * (3.0 - math.sqrt(5.0))
COLORS = {
    "Bamboo": [148, 128, 74, 255],
    "JuteTwine": [155, 117, 72, 255],
    "LivingStem": [69, 122, 39, 255],
    "LivingLeaf": [49, 112, 35, 255],
    "LeafVein": [67, 126, 42, 255],
    "FlowerBlue": [74, 72, 172, 255],
    "FlowerThroat": [210, 198, 224, 255],
    "DrySoil": [67, 43, 29, 255],
}


def unit(v: np.ndarray) -> np.ndarray:
    v = np.asarray(v, dtype=float)
    n = float(np.linalg.norm(v))
    if n < 1e-12:
        raise ValueError("zero vector")
    return v / n


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def components(mesh: trimesh.Trimesh) -> list[trimesh.Trimesh]:
    return list(mesh.split(only_watertight=False))


def merge(meshes: list[trimesh.Trimesh]) -> trimesh.Trimesh:
    if not meshes:
        raise ValueError("cannot merge empty mesh list")
    out = trimesh.util.concatenate(meshes)
    out.merge_vertices(digits_vertex=9)
    out.remove_unreferenced_vertices()
    out.fix_normals(multibody=True)
    return out


def rotation3(axis: np.ndarray, angle_rad: float) -> np.ndarray:
    axis = unit(axis)
    x, y, z = axis
    c = math.cos(angle_rad)
    s = math.sin(angle_rad)
    C = 1.0 - c
    return np.array(
        [
            [c + x*x*C, x*y*C - z*s, x*z*C + y*s],
            [y*x*C + z*s, c + y*y*C, y*z*C - x*s],
            [z*x*C - y*s, z*y*C + x*s, c + z*z*C],
        ],
        dtype=float,
    )


def smoothstep01(x: np.ndarray) -> np.ndarray:
    x = np.clip(x, 0.0, 1.0)
    return x * x * (3.0 - 2.0 * x)


def leaf_normal(mesh: trimesh.Trimesh) -> np.ndarray:
    v = np.asarray(mesh.vertices, dtype=float)
    centered = v - v.mean(axis=0)
    _, _, vh = np.linalg.svd(centered, full_matrices=False)
    n = unit(vh[-1])
    # Keep the comparable side of each leaf pointing generally upward. This is
    # only used to calculate the small authoring rotation; triangle winding is
    # preserved and subsequently repaired/validated.
    if n[1] < 0.0:
        n = -n
    return n


def infer_attachment(leaf: trimesh.Trimesh, stem_tree: cKDTree) -> tuple[np.ndarray, float]:
    d, _ = stem_tree.query(np.asarray(leaf.vertices), k=1)
    i = int(np.argmin(d))
    return np.asarray(leaf.vertices[i], dtype=float).copy(), float(d[i])


def leaf_radius(mesh: trimesh.Trimesh, pivot: np.ndarray) -> float:
    return max(float(np.linalg.norm(np.asarray(mesh.vertices) - pivot, axis=1).max()), 1e-5)


def deformation_field(
    points: np.ndarray,
    pivot: np.ndarray,
    radius: float,
    rotation: np.ndarray,
    scale: float,
    anchor_hold: float,
    blend_end: float,
) -> np.ndarray:
    """Blend from unchanged attachment zone to transformed distal geometry."""
    points = np.asarray(points, dtype=float)
    rel = points - pivot
    d = np.linalg.norm(rel, axis=1)
    hold = radius * anchor_hold
    end = max(radius * blend_end, hold + 1e-5)
    w = smoothstep01((d - hold) / (end - hold))[:, None]
    target = pivot + (rotation @ (rel * scale).T).T
    return points * (1.0 - w) + target * w


def assign_veins_to_leaves(
    vein_components: list[trimesh.Trimesh], leaf_components: list[trimesh.Trimesh]
) -> list[list[trimesh.Trimesh]]:
    groups: list[list[trimesh.Trimesh]] = [[] for _ in leaf_components]
    if not vein_components:
        return groups
    centres = np.asarray([g.centroid for g in leaf_components], dtype=float)
    tree = cKDTree(centres)
    for vein in vein_components:
        _, i = tree.query(np.asarray(vein.centroid, dtype=float), k=1)
        groups[int(i)].append(vein)
    # Existing botanical owner has five geometric veins per leaf. If that
    # ownership relationship changes, fail instead of silently assigning a
    # physically unrelated vein set.
    counts = [len(g) for g in groups]
    if any(c != 5 for c in counts):
        raise AssertionError(("unexpected vein ownership", counts))
    return groups


def authoring_transform(
    index: int,
    centre: np.ndarray,
    normal: np.ndarray,
    canopy_centre_xz: np.ndarray,
    height01: float,
    sun_dir: np.ndarray,
    cfg: dict,
) -> tuple[np.ndarray, float, dict]:
    # Low-discrepancy phase plus position terms gives stable variation without
    # camera-facing randomness or a hidden PRNG state.
    phase = index * GOLDEN_ANGLE + centre[0] * 4.73 + centre[2] * 3.19
    yaw_deg = cfg["max_yaw_deg"] * (0.72 * math.sin(phase) + 0.28 * math.sin(phase * 0.43 + 1.1))
    roll_deg = cfg["max_roll_deg"] * math.sin(phase * 1.37 + 0.4)

    radial = np.array([centre[0] - canopy_centre_xz[0], 0.0, centre[2] - canopy_centre_xz[1]], dtype=float)
    if np.linalg.norm(radial) < 1e-8:
        radial = np.array([math.cos(phase), 0.0, math.sin(phase)], dtype=float)
    radial = unit(radial)
    up = np.array([0.0, 1.0, 0.0])
    tangent = unit(np.cross(up, radial))

    # Lower leaves are broader/heavier and receive more outward/gravity droop;
    # upper leaves are slightly smaller/lighter and more sun-biased.
    droop_weight = 0.30 + 0.70 * (1.0 - height01)
    radial_tilt_deg = cfg["max_radial_tilt_deg"] * droop_weight * (0.82 + 0.18 * math.sin(phase * 0.79 + 0.6))
    scale = 1.0 + cfg["scale_span"] * (
        0.72 * (0.50 - height01) + 0.28 * math.sin(phase * 1.61 - 0.2)
    )
    scale = float(np.clip(scale, 1.0 - cfg["scale_span"], 1.0 + cfg["scale_span"]))

    r_droop = rotation3(tangent, math.radians(radial_tilt_deg))
    r_yaw = rotation3(up, math.radians(yaw_deg))
    base = r_yaw @ r_droop
    n1 = unit(base @ normal)

    # Bias leaf normal modestly toward the coherent exposure direction, while
    # retaining the source leaf's own orientation as the dominant signal.
    cross = np.cross(n1, sun_dir)
    if np.linalg.norm(cross) < 1e-9:
        r_sun = np.eye(3)
        sun_tilt_deg = 0.0
    else:
        full = math.degrees(math.acos(float(np.clip(np.dot(n1, sun_dir), -1.0, 1.0))))
        sun_tilt_deg = min(cfg["max_sun_tilt_deg"], full * (0.18 + 0.12 * height01))
        r_sun = rotation3(cross, math.radians(sun_tilt_deg))
    n2 = unit(r_sun @ n1)
    r_roll = rotation3(n2, math.radians(roll_deg))
    final = r_roll @ r_sun @ base
    return final, scale, {
        "yawDeg": float(yaw_deg),
        "radialTiltDeg": float(radial_tilt_deg),
        "sunTiltDeg": float(sun_tilt_deg),
        "rollDeg": float(roll_deg),
        "scale": float(scale),
    }


def validate_leaf(mesh: trimesh.Trimesh, label: str) -> None:
    if not np.isfinite(mesh.vertices).all() or not np.isfinite(mesh.vertex_normals).all():
        raise AssertionError((label, "non-finite"))
    if np.any(mesh.area_faces < 1e-13):
        raise AssertionError((label, "degenerate"))
    if not mesh.is_watertight:
        raise AssertionError((label, "not watertight"))
    if not mesh.is_winding_consistent:
        raise AssertionError((label, "winding"))
    if mesh.volume <= 0.0:
        raise AssertionError((label, "non-positive volume"))


def validate_group(mesh: trimesh.Trimesh, label: str) -> None:
    if not np.isfinite(mesh.vertices).all() or not np.isfinite(mesh.vertex_normals).all():
        raise AssertionError((label, "non-finite"))
    if np.any(mesh.area_faces < 1e-13):
        raise AssertionError((label, "degenerate"))
    if not mesh.is_winding_consistent:
        raise AssertionError((label, "winding"))


def material_scene(groups: dict[str, trimesh.Trimesh]) -> trimesh.Scene:
    scene = trimesh.Scene()
    for name, mesh in groups.items():
        q = mesh.copy()
        q.visual = trimesh.visual.ColorVisuals(
            mesh=q,
            face_colors=np.tile(COLORS.get(name, [180, 180, 180, 255]), (len(q.faces), 1)),
        )
        scene.add_geometry(q, node_name=name, geom_name=name)
    return scene


def export_obj(scene: trimesh.Scene, path: Path) -> None:
    text = trimesh.exchange.obj.export_obj(scene, include_normals=True, include_color=True)
    path.write_text(text)


def stats(values: list[float]) -> dict:
    a = np.asarray(values, dtype=float)
    return {"min": float(a.min()), "mean": float(a.mean()), "max": float(a.max())}


def process(level: str, input_dir: Path, output_dir: Path, sun_dir: np.ndarray) -> dict:
    cfg = CONFIG[level]
    src_path = input_dir / f"MorningGloryTrellis_{level}_botanical.glb"
    if not src_path.exists():
        raise FileNotFoundError(src_path)
    src = trimesh.load(src_path, force="scene", process=False)
    required = {"LivingStem", "LivingLeaf"}
    missing = required.difference(src.geometry)
    if missing:
        raise KeyError((level, "missing owner geometry", sorted(missing)))

    leaf_parts = components(src.geometry["LivingLeaf"])
    leaf_parts = sorted(leaf_parts, key=lambda g: (float(g.centroid[1]), float(g.centroid[0]), float(g.centroid[2])))
    if not leaf_parts:
        raise AssertionError((level, "no leaves"))
    for i, leaf in enumerate(leaf_parts):
        validate_leaf(leaf, f"{level}:source-leaf:{i}")

    vein_parts = components(src.geometry["LeafVein"]) if "LeafVein" in src.geometry else []
    vein_groups = assign_veins_to_leaves(vein_parts, leaf_parts)
    stem_vertices = np.asarray(src.geometry["LivingStem"].vertices, dtype=float)
    if len(stem_vertices) == 0:
        raise AssertionError((level, "empty stem geometry"))
    stem_tree = cKDTree(stem_vertices)

    centres = np.asarray([g.centroid for g in leaf_parts], dtype=float)
    ymin, ymax = float(centres[:, 1].min()), float(centres[:, 1].max())
    yrange = max(ymax - ymin, 1e-6)
    canopy_centre_xz = np.median(centres[:, [0, 2]], axis=0)

    new_leaves: list[trimesh.Trimesh] = []
    new_veins: list[trimesh.Trimesh] = []
    rows = []
    source_leaf_triangles = int(sum(len(g.faces) for g in leaf_parts))
    source_vein_triangles = int(sum(len(g.faces) for g in vein_parts))

    for i, leaf in enumerate(leaf_parts):
        centre = np.asarray(leaf.centroid, dtype=float)
        height01 = float((centre[1] - ymin) / yrange)
        pivot, gap = infer_attachment(leaf, stem_tree)
        radius = leaf_radius(leaf, pivot)
        normal = leaf_normal(leaf)
        R, scale, authored = authoring_transform(
            i, centre, normal, canopy_centre_xz, height01, sun_dir, cfg
        )
        q = leaf.copy()
        q.vertices = deformation_field(
            q.vertices, pivot, radius, R, scale, cfg["anchor_hold"], cfg["blend_end"]
        )
        q.fix_normals(multibody=True)
        validate_leaf(q, f"{level}:refined-leaf:{i}")
        new_leaves.append(q)

        for j, vein in enumerate(vein_groups[i]):
            vv = vein.copy()
            vv.vertices = deformation_field(
                vv.vertices, pivot, radius, R, scale, cfg["anchor_hold"], cfg["blend_end"]
            )
            vv.fix_normals(multibody=True)
            validate_group(vv, f"{level}:refined-vein:{i}:{j}")
            new_veins.append(vv)

        rows.append(
            {
                "index": i,
                "height01": height01,
                "attachmentGapMm": gap * 1000.0,
                "attachmentHoldRadiusMm": radius * cfg["anchor_hold"] * 1000.0,
                **authored,
            }
        )

    groups = {name: mesh.copy() for name, mesh in src.geometry.items()}
    groups["LivingLeaf"] = merge(new_leaves)
    if vein_parts:
        groups["LeafVein"] = merge(new_veins)

    if len(groups["LivingLeaf"].faces) != source_leaf_triangles:
        raise AssertionError((level, "leaf topology changed"))
    if vein_parts and len(groups["LeafVein"].faces) != source_vein_triangles:
        raise AssertionError((level, "vein topology changed"))
    for name, mesh in groups.items():
        validate_group(mesh, f"{level}:group:{name}")

    source_triangles = int(sum(len(g.faces) for g in src.geometry.values()))
    scene = material_scene(groups)
    refined_triangles = int(sum(len(g.faces) for g in scene.geometry.values()))
    if refined_triangles != source_triangles:
        raise AssertionError((level, "total topology changed", source_triangles, refined_triangles))

    output_dir.mkdir(parents=True, exist_ok=True)
    glb = output_dir / f"MorningGloryTrellis_{level}_botanical.glb"
    obj = output_dir / f"MorningGloryTrellis_{level}_botanical.obj"
    glb.write_bytes(scene.export(file_type="glb"))
    export_obj(scene, obj)

    reload_glb = trimesh.load(glb, force="scene", process=False)
    reload_obj = trimesh.load(obj, force="scene", process=False)
    glb_triangles = int(sum(len(g.faces) for g in reload_glb.geometry.values()))
    obj_triangles = int(sum(len(g.faces) for g in reload_obj.geometry.values()))
    if glb_triangles != refined_triangles or obj_triangles != refined_triangles:
        raise AssertionError((level, "roundtrip triangles", refined_triangles, glb_triangles, obj_triangles))

    src_span = np.ptp(src.bounds, axis=0)
    out_span = np.ptp(scene.bounds, axis=0)
    ratio = out_span / np.maximum(src_span, 1e-8)
    if np.any(ratio < 0.75) or np.any(ratio > 1.30):
        raise AssertionError((level, "implausible canopy span change", ratio.tolist()))

    return {
        "level": level,
        "sourceTriangles": source_triangles,
        "refinedTriangles": refined_triangles,
        "topologyTriangleCountUnchanged": True,
        "leafCount": len(leaf_parts),
        "veinsPerLeaf": 5 if vein_parts else 0,
        "attachmentGapMm": stats([r["attachmentGapMm"] for r in rows]),
        "yawDeg": stats([r["yawDeg"] for r in rows]),
        "radialTiltDeg": stats([r["radialTiltDeg"] for r in rows]),
        "sunTiltDeg": stats([r["sunTiltDeg"] for r in rows]),
        "rollDeg": stats([r["rollDeg"] for r in rows]),
        "scale": stats([r["scale"] for r in rows]),
        "sourceBoundsMetres": src.bounds.tolist(),
        "refinedBoundsMetres": scene.bounds.tolist(),
        "spanRatio": ratio.tolist(),
        "glbRoundtripTriangles": glb_triangles,
        "objRoundtripTriangles": obj_triangles,
        "glbSHA256": sha256(glb),
        "objSHA256": sha256(obj),
        "leafShellsWatertight": all(g.is_watertight for g in new_leaves),
        "leafShellsPositiveVolume": all(g.volume > 0.0 for g in new_leaves),
        "leafAuthoring": rows,
    }


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--input", type=Path, required=True, help="folder containing botanical-refined GLBs")
    ap.add_argument("--output", type=Path, required=True, help="different folder for canopy-refined GLB/OBJ outputs")
    ap.add_argument(
        "--sun-dir",
        type=float,
        nargs=3,
        metavar=("X", "Y", "Z"),
        default=DEFAULT_SUN_DIR.tolist(),
        help="local-space coherent exposure direction; default is a lookdev assumption",
    )
    a = ap.parse_args()
    if a.input.resolve() == a.output.resolve():
        raise ValueError("input and output folders must differ; never overwrite the botanical source")
    sun_dir = unit(np.asarray(a.sun_dir, dtype=float))
    if sun_dir[1] <= 0.0:
        raise ValueError("sun direction must have positive local Y")

    rows = [process(level, a.input, a.output, sun_dir) for level in LEVELS]
    counts = [row["refinedTriangles"] for row in rows]
    if not all(a > b for a, b in zip(counts, counts[1:])):
        raise AssertionError(("strict LOD reduction lost", counts))

    report = {
        "schema": 1,
        "date": "2026-09-17",
        "owner": "MorningGloryTrellisPlant_BotanicalRefinement",
        "status": "EXTERNAL_CANOPY_DISTRIBUTION_REFINEMENT_NOT_UNITY_VISUAL_EVIDENCE",
        "manufactureInstallationReasoning": {
            "componentOwnership": "Existing bamboo trellis, jute, vine/stem, curved leaf shells, geometric veins and flowers are retained. No second plant owner is created.",
            "attachment": "Each leaf's nearest source connection to LivingStem is inferred as a pivot. A local hold zone remains fixed and the deformation ramps smoothly toward the distal leaf.",
            "orientation": "Variation is deterministic and driven by leaf height, radial canopy position and a coherent exterior exposure vector; it is not aimed at the review camera.",
            "exposure": "Lower leaves receive more outward/gravity tilt and slightly larger scale; upper leaves are modestly smaller and more sun-biased.",
            "geometryVsMaterial": "Leaf orientation, silhouette placement and vein seating are geometry. Existing PBR material assignment is performed by the downstream botanical integration owner.",
            "aging": "No random damage, wetness, burned lighting, discoloration or historical-cultivar claim is added in this stage.",
        },
        "sunDirectionLocal": sun_dir.tolist(),
        "sunDirectionStatus": "lookdev assumption; override for a measured scene sun vector",
        "levels": rows,
        "strictLODReduction": True,
        "integrationContract": "Point existing build_balcony_morning_glory_botanical_integration.py --refined at this output folder; filenames are intentionally interface-compatible.",
        "visualFidelity": {
            "score": None,
            "pass": False,
            "pointsAwarded": 0,
            "expectedImpactOnly": ["geometry_construction", "vegetation_natural_complexity"],
            "criticalDefectRiskAddressedOnly": "obvious_repetition",
            "reason": "External geometry authoring is not Unity 3840x2160 pixel or temporal evidence.",
        },
        "unityCompile": False,
        "unityImport": False,
        "unityRender": False,
    }
    (a.output / "canopy_distribution_report.json").write_text(json.dumps(report, indent=2) + "\n")
    print(json.dumps({row["level"]: row["refinedTriangles"] for row in rows}, indent=2))


if __name__ == "__main__":
    main()
