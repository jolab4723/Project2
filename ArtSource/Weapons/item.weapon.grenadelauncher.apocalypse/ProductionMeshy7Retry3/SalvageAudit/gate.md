# Apocalypse Retry3 cap-only salvage gate

- Cost: **0 credits**; Blender audit only.
- Source preserved: `../Raw/item.weapon.grenadelauncher.apocalypse_meshy7_retry3_raw.glb`.
- Muzzle end: negative X, confirmed from the Retry3 side and front orthographic QA.
- Permitted test operation: delete the connected, muzzle-facing center cap surface only. No vertex was added, removed, or moved; no bore wall or tube was generated.
- Geometry invariant: 752,219 vertices before/after, identical sorted-position SHA-256, identical dimensions.
- Deleted test surface: 10,353 dense source triangles spanning the existing center cap.
- 21x21 result: center depth **0.074533 m**, below the required **0.08 m**; only 5/441 samples reached at least 0.08 m.
- Existing cavity result: inward-facing inner walls were present through roughly 0.06 m, but not at the 0.09 m and 0.12 m slices (8/16 inward-wall tests; required 12/16).
- Open-boundary audit after face-only deletion: 757 boundary edges, 2 boundary loops, 15,151 intentionally retained loose triangulation edges, 0 overfull edges, 0 degenerate faces.
- Backface-culled front/side/isometric renders show a shallow stepped pocket rather than a continuous 0.08 m+ firing tunnel.

Result: **NOT SALVAGEABLE BY CAP DELETION ONLY**. No corrected candidate, texture, remesh, retry, Blender production file, FBX, or Unity handoff was created.
