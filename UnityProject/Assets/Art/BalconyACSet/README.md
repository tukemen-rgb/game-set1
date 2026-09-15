# Balcony AC service set

Actual authored geometry for an unbranded around-2000 Japanese residential balcony interpretation: `OutdoorACCondenser780`, `RefrigerantPipeTrunking60x55_900`, and `CondensateDrainHose16_1200`.

The condenser has a true front opening, separate three-blade fan, real concentric/radial guard geometry, aluminum fin rows, side louvers, service valves/pipes, feet and EPDM pads. The pipe cover is hollow with a snap-cover seam and molded elbow. The condensate hose has a real bore, wall thickness and geometric corrugation.

Triangle counts (MASTER / LOD0 / LOD1 / LOD2 / LOD3):
- OutdoorACCondenser780: 21136 / 10544 / 5384 / 3336 / 2788
- RefrigerantPipeTrunking60x55_900: 1732 / 1308 / 980 / 828 / 748
- CondensateDrainHose16_1200: 22272 / 12024 / 4896 / 2420 / 1264

`UnityProject/Tools/generate_balcony_ac_set.py` is the repository authoring source. It is an equivalent compacted variant, not byte-identical to the exact source executed in this run; the exact executed source is included with the full export package delivered in the conversation. Mesh binaries are likewise delivered there rather than falsely claimed as committed.

External geometry verification passed finite coordinates/normals, non-degenerate faces, watertight/winding/manifold component checks, GLB/OBJ triangle roundtrip and strict LOD reduction. A CPU depth-buffered/backface-culled actual-mesh preview was inspected after replacing an unreliable Matplotlib hidden-surface preview.

This is not Unity render evidence. Visual Fidelity remains unscored. Unity import, 3840x2160 hero/oblique/grazing stills and 100% crops, temporal guard/fin shimmer, LOD pop, weathering and target-GPU performance remain pending. Godot and the formal benchmark scene are untouched.
