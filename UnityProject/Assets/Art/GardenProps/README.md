# Garden prop production batch — 2026-09-15

Three authored parametric 3D model sources: TerracottaPot240, TerracottaSaucer214, GalvanizedGardenBucket10L. Units metres, Y up. Each has a high-detail master and LOD0/1/2/3, all physical components retained. The bucket has 11 manufactured components, not a closed primitive cylinder.

## Current deliverables
The .ntprop sources and NewTownGardenPropImporter.cs are committed here. The importer is designed to create prefab assets with Mesh subassets, Standard PBR materials and an LODGroup during import. It has NOT yet been compiled or executed in Unity. There is one combined mesh renderer per LOD with material submeshes; the master is an unrendered subasset. No scene-open, runtime, pre-cull or reflection mutation callbacks. No canonical scene changes.

Actual 15 GLB and 15 OBJ exports were generated and numerically checked outside Unity. The downloadable conversation package garden_props_models_2026-09-15.zip includes those exports, the tested Python exporter, per-part geometry report, hashes and a clearly labelled non-Unity solid mesh preview. These binary exports are not claimed to be committed here. The separate GitHub upload of the Python exporter was blocked by the tool and was not retried via another route.

## Triangle counts: master / LOD0 / LOD1 / LOD2 / LOD3
- Bucket: 48804 / 22372 / 10692 / 5308 / 2684
- Pot: 13312 / 6656 / 3328 / 1664 / 832
- Saucer: 6656 / 3328 / 1664 / 832 / 416

## Placement and material reasoning
The pot has a real 18 mm drainage hole, a foot ring and a rounded thick rim. The saucer has a continuous 10 mm floor and real bowl. Place pot y=0.010 m relative to saucer; do not fuse or overlap their solids. The bucket has a 0.5 mm sheet wall, separate crimped floor, returned lip, reinforcement plates, pivot eyes/pins and a bored wood grip. Dimensional/manufacturing assumptions, clearances, material finish, exposure and geometry-vs-material division are embedded in each source.

Clay and wood are dielectric; exposed zinc is a conductor. Deterministic base-color and roughness microvariation only; normalScale=0 is explicit. Dry maintained state, no arbitrary damage or invented moisture streaks. No directional highlights or shadows painted into textures. Modern manufacturer references are scale/construction references, not proof of an exact 1990s SKU.

## Verification and next production
Executed Python checks cover 65 closed component-material surfaces across 15 meshes: finite vertices/normals, nondegenerate faces, positive volume/consistent winding, welded watertightness, strictly decreasing triangle counts, bounds within 1 mm of master, GLB reload triangle/bounds parity. Nine lathe profiles were checked for simple non-self-intersecting cross sections. These checks do not establish assembly interference clearance, temporal stability, real-time performance, shader fidelity or Unity import parity.

Visual Fidelity remains UNSCORED_UNTIL_REAL_4K_RENDER. Readiness 93/100 is the last recorded project score, not recomputed here. Add these sources to existing scene evidence coverage only when formally placed; retain the existing central 100-point gate. No new scoring authority.

Next model: reuse this pot for a summer morning-glory plant, support stakes, twine, stems and leaves, after checking existing asset ownership and any new Unity runner evidence. Do actual model work rather than successive speculative QA-only expansions. Stop new commits after 2026-09-18 Asia/Tokyo.
