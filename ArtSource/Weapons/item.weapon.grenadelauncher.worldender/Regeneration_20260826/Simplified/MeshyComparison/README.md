# World Ender Meshy comparison

This folder contains the paid comparison requested on 2026-08-26. Neither generated model was imported into Unity.

| Route | Credits | GLB | Triangles | Components | Largest component | Result |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| Meshy 7 Standard Multi-Image | 30 | 53.40 MB | 1,680,878 | 320 | 1.4522% | Reject |
| Meshy T2 Smart Topology, 15K | 15 | 7.66 MB | 14,889 | 572 | 1.4050% | Reject |

Meshy 7 preserves the approved red, black, ivory and gold design much better. T2 is dramatically lighter, but its shape and topology are not production-ready. Both outputs reconstruct the long weapon as a broken 2.5D assembly: the complete silhouette appears from the wrong axis while the true side views contain separated stock, receiver and muzzle pieces.

The main Meshy 7 mistake in this trial was using the literal muzzle-facing `front.png` as the first image. Meshy 7 treats its first image as the primary view. For long weapons the primary must therefore be the approved orthographic side view, followed by the opposite side, muzzle and rear views.

## Files

- `Meshy7/QA/contact_solid.png`: geometry-only six-axis and isometric inspection.
- `Meshy7/QA/contact_textured.png`: textured inspection.
- `T2/QA/contact_solid.png`: geometry-only six-axis and isometric inspection.
- `T2/QA/contact_textured.png`: textured inspection.
- Each `raw_geometry_report.json` contains the Blender measurements.
- `comparison_report.json` contains the final machine-readable decision.

## Decision

Use Meshy 7 Standard Multi-Image for the formal pipeline, but run an untextured geometry task first and reject bad depth reconstruction before paying for textures. Do not use T2 as the primary generator for long Gunner weapons. T2 remains an optional compact-prop or LOD experiment.
