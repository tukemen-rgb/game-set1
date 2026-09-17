# Quality Block authored-art drop zone

This folder is the stable hand-off point for replacing generated benchmark geometry without changing scene logic.

## Accepted asset names

- `PF_Danchi_A.prefab` or `PF_Danchi_A.fbx`
- `PF_Slide_A.prefab` or `PF_Slide_A.fbx`
- `PF_Tree_A.prefab` / `PF_Tree_B.prefab` / `PF_Tree_C.prefab` (FBX also accepted)
- `.glb` can be used only when the Unity project has a compatible glTF importer installed.

The editor pipeline searches this folder first and keeps generated fallback geometry visible when no matching authored asset exists.

## Pivot / scale contract

All art uses Unity meters and Y-up.

- Danchi pivot: ground-level center of the slab footprint. Scene anchor `(-8, 0, -11.5)`.
- Slide pivot: ground-level center near the platform. Scene anchor `(12.6, 0, -5.2)`.
- Tree pivot: trunk center at ground level. Per-tree anchors are created automatically.

Authored assets are instantiated at local position `(0,0,0)`, identity rotation, scale `(1,1,1)`. Model scale or pivot errors should be fixed in the source asset rather than compensated in the scene.

## LOD contract

Generated tree fallbacks receive three LOD levels automatically. Authored tree prefabs should preferably ship with their own `LODGroup`; when they do, the pipeline preserves it. If an authored replacement has no `LODGroup`, a conservative single-representation cull group is added at the slot level.

## Visual constraints

Target: ordinary late-1990s to around-2000 Japanese new-town summer. Keep signs, hardware, balcony details, vegetation, playground equipment and surface aging period-plausible. Do not introduce earthquake, disaster, reconstruction, rubble or collapse themes.
