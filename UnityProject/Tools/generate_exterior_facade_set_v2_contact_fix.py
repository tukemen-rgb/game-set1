"""Contact correction for the existing ExteriorFacadeSet v2 owner.

This is not a new facade system. It wraps `generate_exterior_facade_set_v2.py`
and changes only two demonstrated contact defects in the unexecuted v2 source:

1. Existing fastener cylinders were centered at z=11.0 mm with 3.0 mm depth.
   Their back faces therefore started at z=9.5 mm while the local 16 mm board
   face is z=8.0 mm + panel plane tolerance. With +/-0.30 mm board tolerance,
   the source-defined fasteners floated 1.2-1.8 mm in front of the board.
2. Existing 6 mm-deep joint strips were centered at z=11 mm, spanning
   z=8-14 mm, i.e. up to 6 mm proud of the nominal board face. This wrapper
   retains the 6 mm visible joint width but gives the sealant a 3.0 mm body
   ending at a 0.4 mm nominal crown over the facade face. The existing backer
   remains behind it.

Fastener back faces are seated exactly on each parent bay's local front plane.
No lighting, dirt, water streak, highlight or historical product claim is added.
The dimensions below are generic authoring assumptions for visual construction
plausibility and must still be checked against actual rendered Unity pixels.

Because the model execution runtime was unavailable when this source was
committed, this file is source-only evidence: it does NOT claim generated GLB,
OBJ, triangle counts, Unity import/compile/render, or Visual Fidelity points.
"""
from __future__ import annotations

from pathlib import Path
import argparse
import numpy as np

import generate_exterior_facade_set_v2 as v2

ORIGINAL_FACADE_V2 = v2.facade_v2

# Existing visible joint width is retained. Only depth/contact are corrected.
SEALANT_VISIBLE_WIDTH_M = 0.006
SEALANT_BODY_DEPTH_M = 0.0030
SEALANT_NOMINAL_CROWN_M = 0.0004
FASTENER_HEAD_DEPTH_M = 0.0030


def panel_plane_at(x: float, y: float) -> float:
    """Return the existing v2 parent-bay plane offset at an XY point."""
    ix = int(np.searchsorted(np.asarray(v2.XS), x, side='right') - 1)
    iy = int(np.searchsorted(np.asarray(v2.YS), y, side='right') - 1)
    ix = max(0, min(3, ix))
    iy = max(0, min(2, iy))
    return float(v2.PANEL_PLANE_Z[ix * 3 + iy])


def source_contact_metrics() -> dict:
    """Source-derived geometry deltas; these are not runtime mesh measurements."""
    local_faces = v2.T / 2 + v2.PANEL_PLANE_Z
    old_fastener_back = 0.011 - FASTENER_HEAD_DEPTH_M / 2
    old_gaps = old_fastener_back - local_faces
    old_sealant_front = 0.011 + 0.006 / 2
    return {
        'basis': 'source-derived constants; not executed mesh evidence',
        'panelFrontRangeMm': [float(local_faces.min() * 1000), float(local_faces.max() * 1000)],
        'oldFastenerBackPlaneMm': float(old_fastener_back * 1000),
        'oldFastenerFloatGapRangeMm': [float(old_gaps.min() * 1000), float(old_gaps.max() * 1000)],
        'oldSealantFrontPlaneMm': float(old_sealant_front * 1000),
        'oldSealantProudOverNominalFaceMm': float((old_sealant_front - v2.T / 2) * 1000),
        'newSealantFrontPlaneMm': float((v2.T / 2 + SEALANT_NOMINAL_CROWN_M) * 1000),
        'newSealantProudOverNominalFaceMm': float(SEALANT_NOMINAL_CROWN_M * 1000),
        'newFastenerBackToLocalPanelFaceMm': 0.0,
    }


def facade_v2_contact_fixed(level: str):
    """Use v2 panels/materials but replace only floating contact pieces."""
    parts = ORIGINAL_FACADE_V2(level)
    sec, fast, _, _, bead, _ = v2.LEVELS[level]

    # Remove the two contact implementations being corrected. Everything else,
    # including panels, vent reveal, backing, material ownership and dimensions,
    # stays from the existing v2 owner.
    filtered = []
    for part in parts:
        name = part.metadata.get('name', '')
        if name in ('VJoint', 'HJoint'):
            continue
        if name.startswith('FastB') or name.startswith('FastT'):
            continue
        filtered.append(part)
    parts = filtered

    if bead:
        # The crown is only 0.4 mm above nominal panel face, while the 3 mm body
        # extends backward into the joint/backer zone. This is intentionally a
        # simple tooled-strip proxy, not a painted highlight.
        front_z = v2.T / 2 + SEALANT_NOMINAL_CROWN_M
        center_z = front_z - SEALANT_BODY_DEPTH_M / 2
        for x in v2.XS[1:-1]:
            parts.append(v2.v1.box(
                [SEALANT_VISIBLE_WIDTH_M, v2.H - .02, SEALANT_BODY_DEPTH_M],
                [x, 0, center_z], 'VJoint', 'JointSealant'))
        for y in v2.YS[1:-1]:
            parts.append(v2.v1.box(
                [v2.W - .02, SEALANT_VISIBLE_WIDTH_M, SEALANT_BODY_DEPTH_M],
                [0, y, center_z], 'HJoint', 'JointSealant'))

    if fast:
        ys = (-1.31, 1.31)
        for i, x in enumerate(np.linspace(-1.6, 1.6, max(2, fast // 2))):
            for prefix, y in zip(('FastB', 'FastT'), ys):
                plane = panel_plane_at(float(x), float(y))
                local_face = plane + v2.T / 2
                center_z = local_face + FASTENER_HEAD_DEPTH_M / 2
                parts.append(v2.v1.cyl(
                    .0032, FASTENER_HEAD_DEPTH_M,
                    [float(x), float(y), center_z], [0, 0, 1], sec,
                    f'{prefix}{i}', 'GalvanizedSteel'))

    return parts


def run(output: Path):
    # Patch the existing v2 execution path in-process. Its validation/export,
    # material generation, installed review, GLB/OBJ roundtrip checks and strict
    # LOD reduction remain authoritative when a runtime is available.
    previous = v2.facade_v2
    v2.facade_v2 = facade_v2_contact_fixed
    try:
        report = v2.run(output)
    finally:
        v2.facade_v2 = previous
    report['contactFix'] = source_contact_metrics()
    report['contactFix']['executionNote'] = (
        'These metrics are source-derived until this wrapper is actually run; '
        'the base v2 run output remains responsible for mesh/export evidence.')
    return report


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    metrics = source_contact_metrics()
    print('source contact metrics:', metrics, flush=True)
    run(args.output)
