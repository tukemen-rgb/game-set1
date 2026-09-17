# BalconyLaundryHardwareSet

Actual authored model batch for the Unity migration. It is modular and does not modify the formal benchmark scene.

## Physical assets
- `BalconyLaundryArm450`: 450 mm outdoor wall bracket with 8 mm fabricated arm plate, three real Ø38 mm pole holes, base plate, lower gusset, hinge boss/pin/washers and four visible fastener heads.
- `LaundryPole2560`: hollow 32 mm × 2.56 m stainless pole, 1.2 mm wall, polymer inserted plugs/collars/rounded end caps and a stop collar/screw.
- `BalconyLaundryReferenceAssembly`: external proof assembly uses two arms at 1.8 m spacing and places the pole through the outer 38 mm holes (3 mm nominal radial clearance).

Current official Kawaguchi Giken product pages were used only for modern scale references: 350/450/550 mm balcony arm classes and a 32 mm × 2.56 m stainless pole. Geometry is generic/unbranded and is not claimed to reproduce a current or historical SKU.

## Actual geometry produced
The committed generator was executed in this run. External OBJ and GLB exports passed finite-vertex, non-degenerate-face, per-component watertightness/winding and GLB round-trip triangle checks.

| Asset | MASTER | LOD0 | LOD1 | LOD2 | LOD3 |
|---|---:|---:|---:|---:|---:|
| BalconyLaundryArm450 | 4,556 | 3,084 | 1,964 | 1,356 | 972 |
| LaundryPole2560 | 20,928 | 9,856 | 4,240 | 1,776 | 800 |
| two-arm reference assembly | 30,040 | 16,024 | 8,168 | 4,488 | 2,744 |

The generated binary/text mesh exports are downloadable run artifacts, not claimed to be committed Unity assets. The generator and construction/material metadata are committed so the outputs are reproducible without mutating the canonical scene.

## Material rules
Galvanized steel and stainless are full metallic conductors, not gray dielectrics. EPDM is metallic 0 / dielectric F0 ≈ 0.04. No directional highlights, shadows, rust streaks or damage are baked. Normal scale is explicitly zero for this batch; close-range normal microstructure remains a later real-render-driven task.

## Verification boundary
Unity 6000.3.0f1 compile/import, prefab authoring, real-time performance, temporal LOD stability and 3840×2160 pixels are still unverified. No Visual Fidelity category points are awarded and no PASS is claimed. The last project readiness record remains 93/100; this batch does not recompute it.
