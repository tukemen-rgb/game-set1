# Balcony service-corner production — 2026-09-16

This batch does three production tasks rather than adding QA-only rules:

1. Refines `BalconyFaucetG13` through the existing faucet owner: correct index-cap axis, real EPDM/packing/collar geometry, deterministic UVs, base/metallic-roughness/normal microtexture in external glTF.
2. Refines `GardenHoseCoilNozzle` into a floor-valid resting pose. The old pistol grip extended below the coil ground plane; the grip/overmold/trigger group is rotated about the barrel axis and the asset is normalized to the lowest actual mesh point.
3. Adds `BalconyFloorDrain100`, a new 100 mm-class floor drain with 18 true radial grate slots, bedding ring, PVC flange/well/outlet and retaining screw.

Actual triangle counts:

| Asset | MASTER | LOD0 | LOD1 | LOD2 | LOD3 |
|---|---:|---:|---:|---:|---:|
| BalconyFaucetG13 hero refinement | 10,448 | 7,520 | 4,104 | 2,584 | 1,664 |
| GardenHoseCoilNozzle resting refinement | 30,380 | 17,012 | 8,692 | 4,772 | 3,084 |
| BalconyFloorDrain100 | 7,936 | 6,008 | 4,076 | 2,476 | 1,896 |

`BalconyServiceCornerReview_LOD0` uses actual generated LOD0 geometry for refined faucet/hose, existing watering can and scrub brush, plus the new floor drain. It is 49,460 triangles including reversible diagnostic floor/wall/curb and has zero unexpected AABB overlap between independent asset groups.

The existing `GalvanizedGardenBucket10L` was not duplicated. Its formal `.ntprop` owner exists, but no matching verified binary export was available in this working container and the Unity importer could not run here. Add it when that owner can execute or a verified export is available.

All previews are non-Unity mesh diagnostics. Visual Fidelity remains unscored. Last-recorded Implementation Readiness remains 93/100 and was not recomputed. Godot and the formal benchmark scene are untouched.
