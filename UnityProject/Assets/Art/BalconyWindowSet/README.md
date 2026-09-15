# BalconyWindowSet

Three reversible exterior architectural assets authored after checking the branch for an existing sash/screen owner: `AluminumSlidingSashWindow1800x1800`, `MosquitoScreenPanel870x1760`, and `ExteriorSillDripFlashing1800`.

The sash includes an outer frame, twin tracks, two overlapping leaves, recessed glass, EPDM glazing gaskets, glazing beads, rollers/axles, crescent lock, pull recess, meeting-stile brush seals, weep cavities and fixing heads. The removable screen includes four rails, EPDM spline, actual crossed geometric strands and a pull. The flashing includes an outward fall, upstand, front drop, hemmed drip edge, end dams, fasteners and isolation washers. Dimensions are modeling assumptions, not claims about a historical product.

Run: `python UnityProject/Tools/generate_balcony_window_set.py --output /path/to/out`. Dependencies: numpy, trimesh, Pillow. The generator exports MASTER + LOD0/1/2/3 as GLB and OBJ, deterministic PBR base/MR/normal textures, numerical verification JSON, metadata, and a diagnostic installed review. Conversation delivery contains those actual generated binaries and preview. Repository stores source/metadata only.

Triangle counts: sash `5124/3852/2532/1676/1212`; screen `7812/5268/2724/1436/672`; flashing `1032/792/552/392/312`. Diagnostic review LOD0 is 9,948 triangles. Component meshes were checked for finite geometry, nondegenerate faces, watertightness, winding consistency, positive volume and manifold edge incidence; GLB/OBJ triangle roundtrips passed. This is not Unity evidence.

Visual Fidelity remains unscored. No Unity runner, compile, import, native 3840x2160 render, 100% crop, glass transmission review, temporal LOD review or target-hardware performance test exists. Godot, base branch and formal benchmark scenes remain untouched.