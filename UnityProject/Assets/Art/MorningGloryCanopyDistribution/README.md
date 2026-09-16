# Morning-glory canopy distribution refinement — 2026-09-17

This batch materially improves the existing `MorningGloryTrellisPlant_BotanicalRefinement` owner; it does **not** create a second morning-glory/planter system. The distal leaf shells and their existing geometric veins receive deterministic, height/exposure-based yaw, radial droop, sun bias, roll and modest scale variation while the attachment zone is held. Triangle topology is preserved.

## Demonstrated blocker fixed

The previously authored nearest-centroid vein association fails its own five-veins-per-leaf invariant on LOD0: one leaf receives six and a neighbor receives four. The executed source fixes this with a capacity-constrained minimum-cost assignment (`linear_sum_assignment`) with five ownership slots per leaf. MASTER/LOD0/LOD1 retain five geometric vein components per leaf; LOD2/LOD3 have no geometric vein group by design of the source LODs.

## Actual external geometry evidence

Canopy triangles MASTER/LOD0/LOD1/LOD2/LOD3: `65208 / 39184 / 22516 / 8110 / 3460`. Integrated hero triangles: `266636 / 151100 / 76452 / 36062 / 20340`. GLB and OBJ roundtrips were executed for the canopy assets. Leaf shells were checked finite, non-degenerate, watertight, winding-consistent and positive-volume. Integrated hero GLBs reloaded at the original hero triangle counts. LOD0 conservative support-plane clearances are 386.61 mm rear-to-facade, 237.96 mm front-to-main-rail-inner and 162.26 mm left-to-return-rail-inner.

The material diagnostic submitted all 151,100 LOD0 triangles for both the whole-scene and closeup views; no arbitrary face drawing cap was used. Those images are external CPU diagnostics, **not Unity evidence**.

## Status boundary

`Assets/QA/4k_capture_manifest.json` had `observed_pixels: false` at the start HEAD and GitHub Actions showed no Unity run. Therefore Unity compile/import/render, 3840x2160 benchmark captures, 100% crops, temporal LOD/shimmer and engine PBR remain unverified. Visual Fidelity is unscored with zero points awarded. Last recorded Implementation Readiness is 93/100 and was not recomputed.

The generated GLB/OBJ binaries and PNG diagnostics are delivered in the conversation artifact package; this Git commit stores source and machine-readable evidence, not a claim that the binaries were committed.
