# BalconyRainwaterSet

A reversible exterior rainwater-pipe construction batch for the Unity migration branch. It intentionally does not duplicate `BalconyFloorDrain100`; this owner covers a vertical VP75-scale downpipe, a top offset section and an independent wall clamp.

The scale reference is modern only: Kubota-Chemix lists nominal VP75 at 89 mm outside diameter and 5.5 mm minimum wall thickness. The fitting shapes, clamp construction and installation spacing are modeling assumptions rather than claims about a historical SKU.

Actual external outputs generated in this run: 15 GLB files, 15 OBJ sets, PBR proxy textures and `BalconyRainwaterInstalledReview_LOD0.glb`. Geometry verification covers finite vertices/normals, nondegenerate triangles, watertight component surfaces, consistent winding, positive volume, edge incidence 2, GLB roundtrip, OBJ triangle roundtrip and strict MASTER->LOD3 triangle reduction.

Unity compile/import/render, native 3840x2160 screenshots, 100% crops, temporal LOD/pop, target-hardware performance, pipe-flow behavior and clamp deformation remain unverified. Therefore Visual Fidelity is unscored and receives zero points.
