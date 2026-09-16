# BalconySoffitSet

Three reversible balcony-ceiling assets: a 3.6 x 1.2 m four-panel painted fiber-cement soffit, a 450 mm service hatch, and a 3.6 m formed exterior drip-edge flashing. Dimensions and construction are generic modeling assumptions, not a claim about a specific historical SKU.

The soffit uses real panel thickness, recessed near-LOD joints, sealant/backer rods, hidden furring, fasteners, perimeter seal and exterior shadow reveal. The hatch has an independent aluminum frame/leaf, EPDM gasket, recessed finger-cup cavity and near-LOD hinges/fasteners. The flashing has mounting flange, vertical drop, drip kick, hem, near-LOD end dams and EPDM-isolated fasteners.

The connected BalconyLaundryHardware owner was inspected and is intentionally not duplicated. Godot and formal benchmark scenes are untouched.

The repository Python file is a compact source-only authoring implementation. This run's delivered ZIP contains the executed full exporter plus 15 actual GLBs, 15 actual OBJs, PBR texture maps, numerical validation and `BalconySoffitInstalledReview_LOD0.glb`. Do not infer that generated binary meshes are committed to GitHub.

External geometry checks passed for finite vertices/normals, nondegenerate triangles, per-component watertightness, winding, positive volume, edge incidence=2, GLB triangle/bounds roundtrip, OBJ triangle roundtrip and strict MASTER -> LOD3 triangle reduction. Unity compile/import/render, native 4K pixels, temporal LOD behavior and target-hardware performance remain pending. Visual Fidelity is therefore unscored; no points are awarded.
