# Balcony life: three actual model exports

New original assets: WateringCanCompact, ClothespinBasket224, WoodenClothespin70.

| Asset | MASTER | LOD0 | LOD1 | LOD2 | LOD3 |
|---|---:|---:|---:|---:|---:|
| WateringCanCompact | 20300 | 14876 | 9708 | 5932 | 4540 |
| ClothespinBasket224 | 12160 | 8832 | 5760 | 3520 | 2896 |
| WoodenClothespin70 | 8080 | 4960 | 2616 | 1448 | 872 |

The tested generator is `UnityProject/Tools/generate_balcony_life.py`. Run from the repository root: `python UnityProject/Tools/generate_balcony_life.py --output /path/to/exports`. Dependencies: numpy, Shapely >=2.1, trimesh and Pillow. No external API or service. Models are metres, Y-up; lowest bearing point y=0.

The conversation download `balcony_life_models_2026-09-16.zip` contains 15 actual GLB and 15 OBJ files, MTL/base-color files, the GLB-embedded PBR textures, generation code, renderer code, geometry reports and actual-mesh full/close-up images. These binary exports/previews are delivered in the ZIP, not falsely claimed to be committed here. GitHub contains the exactly matching generator plus material/construction specifications, pending scorecard and persistent inventory.

The can has a continuous real body-to-spout cavity and 67 geometric rose holes. The basket has 32 slots and annular pivot eyes. The pin has actual concave spring seats, two wood jaws and continuous wound wire. No alpha-cutout substitute for these apertures. A preview-detected handle-root gap was fixed before final export. Sharp shading edges are split; topology tests weld coincident shading seams before checking material surfaces.

Executed externally: 80 component surfaces across 15 levels checked for finite coordinates/normals, no degenerate triangles, closed welded edges, positive volumes and consistent winding, GLB triangle/bounds roundtrip, OBJ triangle roundtrip and strict LOD triangle reduction. Eleven diagnostic GLB views use all submitted triangles and backface culling. Their diffuse/ambient shading is NOT full PBR evidence, Unity, or a visual PASS.

Pending: all mechanical contacts/intersections, wood small bevel/endgrain, Unity import/material/coordinate parity, LODGroup execution, target-hardware performance, native 3840x2160 stills/crops and temporal tests. OBJ/MTL is not a complete replacement for glTF PBR. Use the existing central Visual Fidelity Gate; do not score previews or the existence of source files. Last recorded readiness is 93/100, not recomputed. Visual Fidelity remains unscored.

Godot and the formal benchmark scene remain untouched. The concurrent morning-glory surface repair and its inventory changes are preserved, not attributed to this new asset batch. Next: a reversible balcony-life review arrangement using existing assets, not duplicate builders.
