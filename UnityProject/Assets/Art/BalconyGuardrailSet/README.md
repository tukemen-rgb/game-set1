# BalconyGuardrailSet

This owner was created only after inspecting the current `gpt/unity-migration` tree for railing/handrail/parapet owners and finding no matching subsystem. It does not replace or duplicate the existing window, AC, laundry, cleaning or faucet owners.

The run authored three reversible assets: a 3.6 m powder-coated guardrail, a 1.2 m corner return and a four-segment concrete balcony curb. Near LODs retain chamfered profile edges, post base plates, EPDM isolation, mechanical anchors and raised weld-fillet geometry. The curb has a slight outward fall and real movement gaps with recessed sealant/backer rods. Dimensions and material construction are explicit generic modeling assumptions, not identification of a period-specific SKU.

Actual generated outputs in the run artifact: 15 GLB + 15 OBJ plus `BalconyGuardrailInstalledReview_LOD0.glb`, deterministic PBR texture proxies, the executed generator and numerical verification. Repository metadata records their triangle counts and generator SHA256; binaries are not claimed committed.

Triangle counts (MASTER/LOD0/LOD1/LOD2/LOD3): main guardrail `9072/7152/2992/552/384`; corner return `4792/3640/1408/228/180`; curb `464/368/296/228/204`. The diagnostic integrated review is `11172` triangles.

External validation covered finite vertices/normals, nondegenerate triangles, component watertightness, consistent winding, positive volumes, edge incidence 2, GLB bounds/triangle roundtrip, OBJ triangle roundtrip and strict MASTER→LOD3 triangle reduction. Unity compile/import/render, 3840x2160 benchmark pixels, 100% crops and temporal LOD behavior remain unverified. Visual Fidelity therefore remains unscored and no points are awarded. Godot, base and formal benchmark scenes are untouched.
