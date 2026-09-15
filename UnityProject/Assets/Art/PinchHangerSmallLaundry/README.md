# Pinch hanger + small laundry model batch — 2026-09-16

New original authored asset sets: `RoundPinchHanger360_24Clips`, `AnkleSockPair240`, `Handkerchief380Pinned`.

| Asset | MASTER | LOD0 | LOD1 | LOD2 | LOD3 |
|---|---:|---:|---:|---:|---:|
| RoundPinchHanger360_24Clips | 46104 | 32852 | 22912 | 15492 | 10542 |
| AnkleSockPair240 | 10240 | 5760 | 3168 | 1536 | 768 |
| Handkerchief380Pinned | 25596 | 12540 | 5772 | 2300 | 780 |

The hanger retains all 24 two-jaw clips at every LOD and models actual pivot apertures, wound spring/leg geometry, suspension links, circular frame, hub/spokes and hook. The sock pair has real open cuffs, physical material thickness and a separate inner cavity terminating at the toe. The handkerchief is a closed thin cloth material volume with thicker perimeter hems, gravity folds and two top contact pinches.

## Reproducible source
The exact three-module generator that produced the verified exports is committed as `UnityProject/Tools/pinch_hanger_generator_source.zip` (SHA-256 `a3fda7f41f590f76828979c5c97ab475015b0f667ae453a12a5df0fd0431683f`). It contains `generate_pinch_hanger_set.py`, `pinch_hanger_meshlib.py`, and `pinch_hanger_models.py`; module hashes are recorded in metadata/run records. The verified geometry report SHA-256 is `767548abba7a6fa44f1271d73769643606583eabb220726aa2e7c4da53697812`.

Executed outside Unity: 15 model exports plus one LOD0 review assembly, finite vertices/normals, no degenerate faces, welded watertight component surfaces, edge incidence exactly two, positive material volumes/consistent winding, GLB triangle/bounds roundtrip, OBJ triangle roundtrip and strict triangle reduction MASTER > LOD0 > LOD1 > LOD2 > LOD3. Total verified component surfaces across the 15 model levels: 525.

Actual diagnostic previews include hanger oblique/underside, one-clip close-up, sock cuff cavity, handkerchief frontal/oblique and the combined small-laundry arrangement. Previews use actual generated GLB with backface culling, but they are not Unity/PBR benchmark evidence and receive zero Visual Fidelity points.

Pending: Unity 6000.3.0f1 compile/import/material/coordinate parity, measured clip preload/contact, cloth compression/physics, target-hardware performance, native 3840x2160 captures/crops and temporal LOD/aliasing review. Last recorded Implementation Readiness remains 93/100 and was not recomputed. Visual Fidelity remains unscored. Godot and the formal benchmark scene remain untouched.
