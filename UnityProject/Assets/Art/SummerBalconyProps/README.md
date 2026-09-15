# Summer balcony props — 2026-09-16

Three new actual 3D asset sets were generated outside Unity and added to the production inventory:

| Asset | MASTER | LOD0 | LOD1 | LOD2 | LOD3 |
|---|---:|---:|---:|---:|---:|
| BalconySlipperPair | 1,048 | 944 | 800 | 664 | 616 |
| EnamelWashBasin280 | 5,924 | 4,452 | 2,980 | 1,508 | 772 |
| MosquitoCoilTray140 | 9,544 | 6,024 | 3,272 | 1,984 | 1,600 |

The tested source is `UnityProject/Tools/generate_summer_balcony_props.py` (SHA-256 `e0a391ee9a9c7eda86c500f551f92792a90c0fe3bdadc5ff7b7bd3222b6207c1`). It creates MASTER + LOD0/1/2/3 as GLB and OBJ and embeds simple physically classified materials. The binary exports and diagnostic previews are delivered as a run artifact, not falsely claimed to be committed to GitHub.

The slipper pair contains molded soles, bridge straps, anchor blocks, heel lips and real raised tread pods. The basin is a closed thin shell with a separate raised blue enamel rim accent and base feet. The mosquito-coil tray contains a stamped dish, hub/support hardware and an actual round-section 4.4-turn spiral coil. The coil is intentionally unlit: no smoke, ember, glow or ash is baked into model/material data.

Executed external checks cover every exported level: finite coordinates and normals, non-degenerate faces, welded watertight components, consistent winding/positive volume, manifold edge incidence, GLB roundtrip bounds/triangle parity, OBJ triangle parity and strictly decreasing LOD triangle counts. Component closure does not prove every assembly contact/intersection.

The generated PNGs are actual mesh diagnostics, not Unity or PBR render evidence. Visual Fidelity remains `UNSCORED_UNTIL_REAL_4K_RENDER`; zero visual points are awarded. Last recorded Implementation Readiness remains 93/100 and is not recomputed here.

Pending: Unity 6000.3.0f1 compile/import/material parity, LODGroup/temporal tests, target-hardware real-time performance, and fresh 3840x2160 benchmark captures with 100% crops. Godot and the formal benchmark scene are untouched.

Next production target: add a reversible balcony review arrangement that combines the existing repaired morning-glory/pot, laundry hardware, watering can/basket, and these three props at measured clearances. If Unity Runner or screenshots become available first, stop asset expansion and score/fix the real render.
