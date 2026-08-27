# World Ender — Meshy 7 production status

## Generation

- Meshy task: `01a03d13-e737-7c38-8600-776931521ba6`
- Model: Meshy 7 (`latest`)
- Mode: Standard multi-image, geometry only
- Input order: `left.png`, `right.png`, `front.png` (muzzle), `back.png` (stock)
- Credits consumed: 20
- Raw output: `Raw/worldender_meshy7_side_primary_raw.glb`

## Raw geometry result

- Overall silhouette, stock, receiver, barrel, right-hand grip, and forward grip were preserved.
- The raw model was one connected, watertight component.
- Raw topology: 1,643,854 triangles, 0 boundary edges, 0 non-manifold edges.
- The generated muzzle was capped, so the raw output was not accepted as final.

## Local muzzle correction

- Source-preserving Blender correction: `Postprocess/fix_worldender_muzzle.py`
- Corrected GLB: `Postprocess/worldender_meshy7_muzzle_fixed.glb`
- Editable source: `Postprocess/worldender_meshy7_muzzle_fixed.blend`
- Correction report: `Postprocess/muzzle_fix_report.json`
- The bore is limited to the front collar so it does not pierce either barrel side wall.
- Corrected topology: one component, 0 boundary edges, 0 non-manifold edges.
- QA renders and metrics: `QA_MuzzleFixed/`

## Cost gates still pending

Do not spend these credits without explicit confirmation.

1. Conservative game-ready remesh: 5 credits.
2. Meshy PBR retexture using the approved four-view art: 10 credits.
3. After both pass, perform Blender axis/root/RightHandGrip/LeftHandGrip/Muzzle setup and Unity import QA.

