# Balcony cleaning set — actual generated meshes

Three new model sets were generated and reloaded outside Unity. They are original, unbranded design interpretations rather than exact product replicas.

| Asset | MASTER | LOD0 | LOD1 | LOD2 | LOD3 |
|---|---:|---:|---:|---:|---:|
| OutdoorBroom900 | 71,808 | 37,728 | 15,936 | 4,128 | 1,440 |
| PlasticDustpan265 | 1,216 | 928 | 656 | 400 | 336 |
| HandScrubBrush185 | 56,530 | 29,856 | 12,032 | 2,712 | 840 |

`UnityProject/Tools/generate_balcony_cleaning_set.py` creates MASTER + LOD0/1/2/3 as GLB and OBJ. The broom retains individual tapered fibres, the dustpan has an actual through hanging hole and joined scoop construction, and the scrub brush retains individual tapered bristles. This is production mesh work, not a validator-only change.

Executed verification: every generated component is finite, nondegenerate, watertight after welded seam checking, consistently wound with positive volume, and manifold by edge incidence. GLB reload preserves triangle count/bounds, OBJ reload preserves triangle count, and runtime LOD counts strictly decrease. Assembly intersections and mechanical bending/contact are not exhaustively simulated.

Actual-mesh diagnostic previews were manually inspected. They use a CPU z-buffer and backface culling, but are not Unity/PBR benchmark renders and cannot receive Visual Fidelity points. Unity 6000.3.0f1 compile/import, LODGroup execution, target-hardware performance and native 3840x2160 evidence remain pending.

A separate actual LOD0 balcony review assembly was also created outside Unity from the existing laundry hardware, repaired morning glory/pot/saucer, household props and this cleaning set. It is 159,460 triangles including diagnostic floor/wall/parapet context. Placement AABB checks found no unexpected overlap among independently placed props. The context boxes are review-only and excluded from asset inventory and scoring.
