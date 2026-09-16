# BalconyDrainageIntegration

Integration/refinement layer; existing `BalconyFloorDrain100` and `CondensateDrainHose16_1200` base owners remain unchanged.

- Re-executed FloorDrain100 exact LOD triangle parity: {'MASTER': 7936, 'LOD0': 6008, 'LOD1': 4076, 'LOD2': 2476, 'LOD3': 1896}.
- Real drain-well cutout added to the sloped waterproof floor.
- Hollow corrugated condensate hose rerouted continuously to the drain center with gravity fall.
- Low-side mineral trace only; no random weathering.
- `BalconyExteriorHeroDrained3600` generated for MASTER + LOD0/1/2/3 without changing formal benchmark state.

Fit: hose start delta 0.0000 mm; outlet-to-drain horizontal delta 0.0000 mm; minimum hose/floor outer clearance 7.80 mm; drain top flush delta 0.0000 mm; flange bearing 8.6 mm.

Unity compile/import/render and temporal LOD behavior remain pending. Visual Fidelity remains unscored until required real Unity 3840x2160 pixels and 100% crops exist.
