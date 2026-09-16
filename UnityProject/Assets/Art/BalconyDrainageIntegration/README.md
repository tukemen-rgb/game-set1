# BalconyDrainageIntegration

**2026-09-17:** [Floor/curb surface and preview-shadow refinement](SURFACE_REFINEMENT_JA.md) updates the existing materials and corrects collapsed UVs while preserving all triangle positions. The delivery is a building-component preview; do not substitute it for later plant/laundry scene assemblies.

**2026-09-16 latest revision:** [Drained3600 floor and interface repair](../BalconyStructuralContactRepair/README.md) supersedes the original assembled preview and its counts below. The earlier records remain historical; use the current `build_balcony_structural_contact_repair.py` and component `*_v2.py` sources for the repaired assembly.
The original exported floor did not match its declared cutout: a rectangle-coordinate ordering error removed the drain support region. The latest revision corrects this while preserving the drain and hose meshes.

Integration/refinement layer; existing `BalconyFloorDrain100` and `CondensateDrainHose16_1200` base owners remain unchanged.

- Re-executed FloorDrain100 exact LOD triangle parity: {'MASTER': 7936, 'LOD0': 6008, 'LOD1': 4076, 'LOD2': 2476, 'LOD3': 1896}.
- Real drain-well cutout added to the sloped waterproof floor.
- Hollow corrugated condensate hose rerouted continuously to the drain center with gravity fall.
- Low-side mineral trace only; no random weathering.
- `BalconyExteriorHeroDrained3600` generated for MASTER + LOD0/1/2/3 without changing formal benchmark state.

Fit: hose start delta 0.0000 mm; outlet-to-drain horizontal delta 0.0000 mm; minimum hose/floor outer clearance 7.80 mm; drain top flush delta 0.0000 mm; flange bearing 8.6 mm.

Unity compile/import/render and temporal LOD behavior remain pending. Visual Fidelity remains unscored until required real Unity 3840x2160 pixels and 100% crops exist.
