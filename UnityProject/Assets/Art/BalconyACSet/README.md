# Balcony AC service set

Actual authored geometry for an unbranded around-2000 Japanese residential balcony interpretation. Existing owners remain `OutdoorACCondenser780`, `RefrigerantPipeTrunking60x55_900`, and `CondensateDrainHose16_1200`; this run extends the same set with `RefrigerantLineSetPair_6p35_9p52`, `PipeCoverWallElbow60x55`, and `ACWallPenetrationSeal75`. No duplicate condenser or trunking system was created.

The condenser has a true front opening, separate three-blade fan, real concentric/radial guard geometry, aluminum fin rows, side louvers, service valves/pipes, feet and EPDM pads. The trunking is hollow and the condensate hose has a real bore/corrugation. The new line pair adds geometric copper, hollow insulation, bored flare nuts and transition boots. The new top elbow is a hollow curved rectangular PVC shell. The wall entry uses a true PVC sleeve plus an irregular closed putty annulus and EPDM lip.

Triangle counts (MASTER / LOD0 / LOD1 / LOD2 / LOD3):
- OutdoorACCondenser780: 21136 / 10544 / 5384 / 3336 / 2788
- RefrigerantPipeTrunking60x55_900: 1732 / 1308 / 980 / 828 / 748
- CondensateDrainHose16_1200: 22272 / 12024 / 4896 / 2420 / 1264
- RefrigerantLineSetPair_6p35_9p52: 24720 / 13424 / 6208 / 3168 / 1680
- PipeCoverWallElbow60x55: 4224 / 2208 / 1216 / 608 / 480
- ACWallPenetrationSeal75: 864 / 640 / 448 / 336 / 240

`UnityProject/Tools/generate_balcony_ac_install_details.py` and `build_balcony_ac_installed_review.py` are byte-identical to the sources executed for this extension. The previous base-set generator remains as recorded by its earlier metadata. Full mesh binaries are delivered in the conversation artifact rather than falsely claimed as committed.

External geometry verification passed finite coordinates/normals, non-degenerate faces, watertight/winding/manifold component checks, GLB bounds/triangle roundtrip, OBJ triangle roundtrip and strict LOD reduction. `BalconyACInstalledReview_LOD0` reuses the existing condenser/trunking/drain and constrains the new service line to 0.5 mm of the existing trunking starts; trunking back is within 0.03 mm of the diagnostic wall exterior and the top elbow has a documented covered 2 mm assembly overlap. These are coordinate checks, not Unity physics/contact proof.

The 6.35/9.52 mm tube sizes and cover/sleeve dimensions are modeling references, not an assertion about a specific historical product. No earthquake/disaster/reconstruction content is introduced. Godot and the formal benchmark scene are untouched.

This remains non-Unity evidence. Visual Fidelity is unscored. Unity import, native 3840x2160 frontal/oblique/grazing captures and 100% crops, temporal LOD/shimmer, scene-causal weathering, full intersection/contact validation and target-GPU performance remain pending.
