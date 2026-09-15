# Garden prop production — 2026-09-15

Four authored asset sets now exist. The first batch contains parametric `TerracottaPot240`, `TerracottaSaucer214` and `GalvanizedGardenBucket10L`. The second batch adds `MorningGloryTrellisPlant`, with deterministic Unity Editor authoring source plus an actually generated and reloaded OBJ/MTL mesh package outside Unity. `asset_inventory.json` is the persistent machine-readable inventory.

## Triangle counts: MASTER / LOD0 / LOD1 / LOD2 / LOD3
- GalvanizedGardenBucket10L: 48,804 / 22,372 / 10,692 / 5,308 / 2,684
- TerracottaPot240: 13,312 / 6,656 / 3,328 / 1,664 / 832
- TerracottaSaucer214: 6,656 / 3,328 / 1,664 / 832 / 416
- MorningGloryTrellisPlant external actual OBJ proof: 36,264 / 23,888 / 15,028 / 5,134 / 2,644

## MorningGloryTrellisPlant
The plant does not duplicate the terracotta pot. It is authored to sit inside the existing pot: 101 mm radius soil insert, three tapered bamboo stakes, bamboo-node collars, sagging jute support tiers, primary twining stems, petioles/peduncles, closed thin leaf solids, raised leaf veins at hero LODs, funnel flowers with pale throats and buds. MASTER is authoring-only; runtime intent is LOD0–3.

Botanical proportions are informed by Kew Plants of the World Online for *Ipomoea nil*: twining habit, cordate/three-lobed leaves around 8 cm scale and funnel-shaped corollas. This is morphology evidence only, not evidence for a specific historical consumer product. The support is generic, unbranded bamboo/twine construction.

All plant materials are dielectric. Per-material albedo, roughness, intended normal scale, microstructure, wetness, UV-aging policy and Fresnel response are recorded in `MorningGloryTrellis/MorningGloryTrellis.metadata.json`. No directional highlights or shadows are baked. Current dry-reference state has no arbitrary damage or disaster cue.

`MorningGloryTrellisAuthoring.cs` is an explicit Editor authoring command: it creates a high-detail MASTER mesh asset, LOD0–3 mesh assets, Standard-shader materials and a cross-fade LODGroup prefab. It has no scene-open/runtime/pre-cull callback and does not modify the formal benchmark scene automatically. This source has not yet compiled or executed in Unity.

## Executed vs pending verification
The external OBJ files were actually generated and reloaded outside Unity. Executed checks: finite vertices/normals, unit-length normals, no degenerate triangles, face winding consistent with authored normals, exact OBJ-reload triangle counts and strictly decreasing runtime LOD triangle counts. The mesh package is a run artifact; it is not Unity evidence.

Pending: Unity 6000.3.0f1 compile, Editor authoring execution, material illumination, assembly intersection review, real-time performance, LOD pop/shimmer review, 3840x2160 captures and 100% crops. LOD2/3 intentionally reduce foliage density, so silhouette stability must be judged in real temporal capture.

Visual Fidelity remains `UNSCORED_UNTIL_REAL_4K_RENDER`; zero visual points were added. Readiness 93/100 is the last recorded project score and was not recomputed. The existing central 100-point Visual Fidelity Gate remains the only scoring authority.

Next production target after ownership search: balcony household-service microassembly (clothesline sockets/pole, small watering can, clothespin basket). Preserve Godot; never merge base; stop new commits after 2026-09-18 Asia/Tokyo.
