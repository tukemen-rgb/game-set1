# Laundry soft-goods production — actual generated meshes

Three new model sets were generated and reloaded outside Unity: a 700×1200 mm terry towel draped over the existing laundry-pole diameter, a cotton T-shirt on a separate PP hanger, and a 1400×1900 mm summer sheet draped over an assumed parapet top. The fabric bodies are closed thin solids with physical thickness; they are not zero-thickness cards.

| Asset | MASTER | LOD0 | LOD1 | LOD2 | LOD3 |
|---|---:|---:|---:|---:|---:|
| BathTowel700x1200PoleDrape | 55,996 | 29,948 | 13,052 | 4,476 | 1,532 |
| CottonTShirtM_Hanger | 29,856 | 6,456 | 4,724 | 1,104 | 372 |
| SummerSheet1400x1900RailDrape | 64,508 | 33,276 | 14,972 | 5,036 | 1,868 |

`UnityProject/Tools/generate_laundry_softgoods.py` recreates the exports without network access. External checks cover finite geometry, nondegenerate triangles, welded watertightness/manifold edges, positive volume/winding, GLB+OBJ roundtrips and strict LOD triangle reduction. These are geometry checks, not Unity visual evidence.

A separate LOD0 review combines the new towel and shirt with the already-authored `BalconyLaundryReferenceAssembly_LOD0`, without modifying or duplicating the existing hardware. It is 52,428 triangles and remains outside the formal benchmark scene. The towel fold and hanger hook overlap the pole cross-section intentionally as contact assumptions; Unity collision/contact must still be inspected.

Known production limitation: the current T-shirt is a closed thin garment solid with a real neck hole, not yet a full front/back sewn shell with open sleeve cuffs and waist. Treat it as a visible production intermediate and refine if native close-up evidence exposes this simplification.

Materials are dry dielectrics (cloth/PP) with low-amplitude deterministic base/roughness microstructure and no baked highlight. Proxy 128×128 maps are not final 4K textile detail. Construction/material metadata records geometry-vs-material responsibilities and cause-based aging constraints.

Unity 6000.3.0f1 compile/import, material parity, real-time performance, wind/contact behavior, LOD transitions and native 3840×2160 stills/crops remain pending. Visual Fidelity remains unscored; the last recorded Implementation Readiness 93/100 is not recomputed here.
