# HangingBlueSoapNet: user-reference model

Original blue open-net bag, rounded ivory soap, neck hook, eye and cords. Photograph used for construction reference only; no source-image pixels or watermarks redistributed. Fibre/alloy/dimensions are explicit modeling assumptions. Existing faucet is reused without modification.

Run `python UnityProject/Tools/generate_hanging_soap_net.py --output /path/to/out --faucet /path/to/BalconyFaucetG13Hero_LOD0.glb`. Omit `--faucet` for the standalone accessory. Requires numpy, scipy, trimesh, Pillow; no network calls. Metres, Y-up, root at host inlet-neck axis. Review translation [0,0.55,0.024] is for the supplied known host only.

MASTER/LOD0/LOD1/LOD2/LOD3 triangles: 103904/61968/32136/17200/9840. All 32 net yarns remain. 41 logical physical parts compacted into four material meshes per export, not 41 separate yarn renderers. Unity draw calls and temporal behavior are unmeasured.

The conversation ZIP contains five actual OBJ and five GLB levels, combined host review GLB, the reused host input, textures, numerical reports, generator and depth-buffered actual-geometry previews. GitHub stores the byte-identical executed generator and metadata/inventory delta; binaries are not claimed committed.

This is a shape prototype, not a movie-quality result. Yarn vertices and face centres were tested outside the analytic soap volume; this is not a complete intersection proof. Softer actual knitted loops, knot contacts, photo-like host silhouette/wear, UV microdetail, Unity import and native 4K/temporal evidence remain pending. The existing central Visual Fidelity Gate is unchanged; zero visual points awarded. Godot and formal benchmark scenes untouched.
