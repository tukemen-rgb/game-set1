# Faucet / hose hard-surface production — 2026-09-16

Two new unbranded asset sets were produced after ownership search found no faucet/hose authoring owner on `gpt/unity-migration`.

| Asset | MASTER | LOD0 | LOD1 | LOD2 | LOD3 |
|---|---:|---:|---:|---:|---:|
| BalconyFaucetG13 | 6,128 | 4,832 | 2,664 | 1,720 | 1,140 |
| GardenHoseCoilNozzle | 30,380 | 17,012 | 8,692 | 4,772 | 3,084 |

The run artifact contains 10 GLB and 10 OBJ files. The faucet has 20 closed components per LOD. The hose/nozzle has 17 and a stainless shower face with 33 actual through-holes rather than painted dots. A diagnostic LOD0 review assembly was exported at 21,868 triangles; its wall/floor boxes are diagnostic only and are not formal benchmark content.

Preview inspection caught two nozzle interface defects before final output: connector-to-barrel spacing and selector-head-to-shower-face spacing. Both were closed before the final generation run. External verification covered finite vertices/normals, nondegenerate faces, welded watertightness, winding, edge incidence=2, positive component volumes, GLB/OBJ triangle roundtrip and strict LOD reduction.

Current SANEI/TAKAGI dimensions are modern scale/interface references only. No exact historical consumer SKU is claimed. Material/construction assumptions are in `FaucetHose.metadata.json`.

Unity compile/import/material parity, real-time performance, native 3840x2160 hero/oblique/grazing stills, 100% crops and temporal LOD checks remain pending. Visual Fidelity is therefore unscored; last-recorded Implementation Readiness remains 93/100 without recomputation. Godot and the formal benchmark scene remain untouched.
