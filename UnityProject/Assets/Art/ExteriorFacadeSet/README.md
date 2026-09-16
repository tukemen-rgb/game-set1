# ExteriorFacadeSet

High-coverage exterior-wall construction batch for `gpt/unity-migration`. It intentionally does not replace or duplicate the existing window, AC, rainwater, soffit, guardrail, faucet or service-corner owners.

The set contains a 3.6 x 2.7 m generic painted fiber-cement facade, a 150-class exterior vent hood, and a 3.6 m formed base starter flashing. Dimensions are modeling assumptions rather than identification of a specific historical product. The vent-bearing panel field is physically split around the opening; the vent uses a real hollow sleeve, louver geometry and coarse geometric insect-screen proxy. Painted metal is dielectric (`metallic=0`); only galvanized hardware is metallic.

Run `python UnityProject/Tools/generate_exterior_facade_set.py --output <directory>` with numpy, trimesh and Pillow. The executed exporter generates MASTER plus LOD0/1/2/3 in GLB and OBJ, PBR proxy textures, an installed review GLB, construction metadata and numerical verification. Generated binaries are deliberately kept outside Git in this run and delivered separately.

External verification covers finite vertices/normals, nondegenerate faces, component watertightness, winding, positive volume, manifold edge incidence, GLB triangle/bounds roundtrip, OBJ triangle roundtrip and strict triangle reduction across LODs. It does not establish Unity import/compile/render quality, temporal LOD stability, target-hardware performance, or Visual Fidelity PASS. The installed review is reversible and does not mutate the formal benchmark scene.
