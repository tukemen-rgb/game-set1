# Faucet + hanging soap-net reference v2

This batch materially refines the existing `BalconyFaucetG13` hero owner and the existing `HangingBlueSoapNet` accessory using the user-supplied reference photo for construction/silhouette only. No reference-photo pixels or watermarks are redistributed.

The faucet remains in the existing owner chain (`generate_faucet_hose.py` -> `refine_faucet_hero.py`): the former cross-handle/cylindrical camera-near silhouette is replaced inside the refiner by a rounded used body, compact single lever with blue index cap, toothed union, short hooked spout and real outlet cavity. PBR textures carry metallic/roughness/normal data; weathering is roughness-led rather than baked lighting.

The soap net keeps true open geometry but changes from a rigid regular cylinder with blue triangular cords to a shorter/wider slackened crossed-yarn bag with an irregular mouth and a thin stainless neck loop/drop/split hanger. All 28 yarns remain through MASTER and LOD0/1/2/3; 37 logical parts are compacted to four material meshes.

Actual external outputs generated in this run: five faucet GLB + five faucet OBJ, five soap-net GLB + five soap-net OBJ, and one combined LOD0 review GLB. Faucet triangle counts: 13472/8712/4992/3080/2012. Soap-net triangle counts: 88480/51552/25872/14352/8672. Combined LOD0: 60264 triangles.

Executed checks cover finite geometry/normals, nondegenerate triangles, per-component watertight/winding/positive volume, GLB roundtrip, OBJ triangle roundtrip, strict LOD triangle reduction, UV/normal maps on the faucet, and analytic soap-envelope clearance for yarn vertices/triangle centres. Diagnostic previews rasterize actual GLB triangles with back-face culling.

Not verified: Unity compile/import/render, native 3840x2160 frames/crops, temporal LOD/shimmer, full yarn-yarn/contact intersections, cloth deformation and target-hardware performance. Visual Fidelity therefore remains unscored; last recorded Implementation Readiness remains 93/100 and was not recomputed. Godot, base merge and formal benchmark scenes are untouched.
