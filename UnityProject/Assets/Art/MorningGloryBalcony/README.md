# Balcony morning-glory planter integration — 2026-09-17

This batch reuses the previously repaired `MorningGloryTrellisPlant`, existing `TerracottaPot240`, and existing `TerracottaSaucer214`. It does not create a second base plant, pot, saucer, or ground-grass owner.

Actual external outputs were generated for MASTER and LOD0/1/2/3. The assembled planter is 67,032 / 39,760 / 22,756 / 8,590 / 4,204 triangles. Review-only integration into the current facade-refined drained structural hero is 248,492 / 141,692 / 71,700 / 34,046 / 19,836 triangles.

The saucer/pot/plant assembly is installed at x=-1.45 m, z=0.55 m. It is tilted 0.859 degrees about X so the saucer bottom follows the existing 1.5% outward balcony fall rather than floating over a level plane. LOD0 world-envelope clearances are 387.6 mm to the facade plane, 241.8 mm to the main guardrail inner envelope, and 154.6 mm to the return-rail inner envelope. It remains far from the AC condenser, condensate route, and floor drain.

The repaired plant geometry is retained. This run adds deterministic PBR micro-material maps for bamboo, jute, stem, leaf, vein, blue petal, pale throat, and dry soil. All remain dielectric/non-metallic; roughness and normal microstructure are recorded in metadata. No sunlight, wetness, random dirt, algae, mineral ring, or painted highlight is baked. Existing terracotta source materials are retained.

Material-colored diagnostics submitted every LOD0 face: 141,692 faces for the whole view and integrated close-up, and 39,760 for the standalone planter. These are actual-mesh CPU previews, not Unity renders and not Visual Fidelity evidence.

The conversation artifact `balcony_morning_glory_planter_integration_2026-09-17.zip` contains the actual GLB/OBJ files, PBR textures, material-colored previews, repaired-plant and pot/saucer inputs, exact local authoring source, and verification records. GitHub intentionally stores evidence plus a tested replay integration script, not the large binary package.

Not verified: Unity compile/import/render, native 3840x2160 pixels, 100% formal crops, reflections/refraction/GI, temporal LOD/shimmer, or performance. Visual Fidelity remains unscored with 0 points awarded. Last recorded Implementation Readiness remains 93/100 and was not recomputed.
