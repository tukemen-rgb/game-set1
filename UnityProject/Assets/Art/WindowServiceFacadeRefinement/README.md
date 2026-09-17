# Window-service facade surface refinement — 2026-09-17

**Later material pass:** [Contextual dry weathering](../BalconyDrainageIntegration/CONTEXT_WEATHERING_JA.md) adds a material-only option to this existing facade source. It anchors residue to measured vent/clamp/floor interfaces and preserves the actual panel geometry, apertures and seals. The original clean material path below remains available.

This batch refines the existing `PaintedFiberCementFacadeWindowService3600x2700` inside the latest drained + structural-repair hero. It intentionally does **not** replace that wall with the simpler vent-only `ExteriorFacadeSet v2`, because doing so would close the current window and AC service apertures.

Actual outputs were generated for MASTER and LOD0/1/2/3. Facade triangles are MASTER 2,976 / LOD0 2,208 / LOD1 732 / LOD2 344 / LOD3 120. Integrated hero triangles are 181,460 / 101,932 / 48,944 / 25,456 / 15,632. Ten existing panel envelopes remain unchanged. At MASTER/LOD0/LOD1, the three sealant solids were moved from behind the 16 mm board to a 3 mm physical body with a 0.4 mm exterior crown. Existing fixing heads, reveals, backers, drainage, guardrail, soffit, window and AC geometry remain unchanged.

The panel material uses a warm neutral sRGB mean `[0.72, 0.70, 0.64]`, metallic 0, roughness about 0.72 and a subtle normal proxy. Its 1024 px field maps once over the 3.6 x 2.7 m facade, so no small repeated facade tile is introduced. This is art direction / generic construction plausibility, not a historical product identification. No lighting, wetness, random dirt or painted highlight is baked.

Actual verification in the generation environment included finite/nondegenerate closed new meshes, watertight/winding/positive-volume/edge-incidence checks, GLB reload, facade OBJ triangle reload, strict LOD decrease, exact reloaded vertices/faces for non-facade and unrelated facade geometry, and opening-preservation checks. Material-colored CPU previews submitted every LOD0 face with no arbitrary face cap.

Binary outputs and material-colored previews are delivered in the conversation artifact `window_service_facade_refinement_2026-09-17.zip`; they are not claimed committed here. The package also contains the exact executed local source and detailed verification JSON.

Not verified: Unity compile/import/render, 3840x2160 formal pixels, reflections/refraction/GI, temporal LOD/shimmer, performance. Visual Fidelity therefore remains unscored and no points are awarded. Last recorded Implementation Readiness remains 93/100 and was not recomputed.
