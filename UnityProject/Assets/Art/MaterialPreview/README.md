# Material-colored actual-mesh previews

User correction, 2026-09-16: blue-only verification pictures are not acceptable; show actual material colors.

The previous conversation preview used Poly3DCollection without explicit face colors and an arbitrary 65,000-face drawing cap. The supplied GLB already contained material textures. `UnityProject/Tools/render_material_preview.py` now provides an actual-material review path: every face and node transform is submitted; base color, metallic-roughness and valid tangent-space normal maps are sampled with depth buffering, coherent diagnostic sun/shadows and supersampling. No AI-generated pixels stand in for 3D geometry.

Run: `python UnityProject/Tools/render_material_preview.py input.glb --output /existing/output/folder/preview.png --palette`. Requirements: numpy, trimesh, Pillow, numba and the Linux DejaVu font used by the diagnostic header. Omit `--palette` to display source colors. With `--palette`, a separate colorized GLB is saved beside the image. The source has the full neutral-palette definition: cream walls, silver aluminum, dark brown-gray coated railings, ivory AC, gray concrete/PVC, separate off-white soffit; unspecified materials retain their original colors. No geometry is changed and source metal/roughness/normal maps are retained.

The complete byte-identical executed renderer is committed, not an abbreviated implementation. Five supplied Grounded3600 levels were colorized and reloaded: original vertices, faces and bounds are unchanged; metal/roughness/normal maps match byte-for-byte. LOD0 has 95,584 triangles, all submitted for overview and two closeups. A separate earlier household scene was rendered in its source colors. The delivery archive is balcony_material_colors_2026-09-16.zip, containing five actual colorized GLBs, four PNGs, full renderer, reproduction driver, palette and reports. Binaries are not claimed committed.

The input is the prior conversation's Grounded3600 archive, not the concurrently committed Drained3600 model. The concurrent drainage commit 8450a0834df172f401b653ba417e966ca26c917d is preserved unchanged; no drain implementation is duplicated or overwritten.

Limitations: NOT Unity, NOT a Visual Fidelity PASS or calibrated PBR evidence. Ambient environment is analytic; no scene reflections, refraction or GI. Glass remains a source opaque tint. Diagnostic normal handling for near-axis-aligned planar faces suppresses uncreased-box interpolation artifacts; saved model normals are unchanged. Coloring does not fix the existing vertical access hatch, unseated return rail or disconnected picket tops. Those remain production issues for their existing owners.

Future user-facing production reviews must include material-colored whole and closeup views. Clay/wireframe may be supplemental only. Do not truncate a mesh to fit a drawing budget. Keep the central Visual Fidelity gate unchanged and Unity compile/import/render/temporal checks pending until executed.
