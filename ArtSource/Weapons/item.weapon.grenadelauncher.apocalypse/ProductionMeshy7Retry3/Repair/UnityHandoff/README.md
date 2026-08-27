# Apocalyptic Destruction - ProductionMeshy7Retry3 repaired handoff

Accepted controlled-repair production result for `item.weapon.grenadelauncher.apocalypse` (`GrenadeLauncher`).

- Final mesh: 71,678 vertices / 143,408 triangles.
- Protected bore liner: 34 vertices / 64 triangles, excluded from all decimation stages.
- Exterior-only staged decimation promoted the lowest visually safe `stage5_143k` candidate.
- Topology: boundary 0, nonmanifold/overfull 0, loose 0, degenerate 0.
- Bore: 21 x 21 grid, 441/441 hits, exact measured depth 0.120000005 m, muzzle-facing back wall.
- Coordinate contract: muzzle +Z, up +Y, one itemId root, `RightHandGrip`, `LeftHandGrip`, and `Muzzle` markers.
- Clean FBX reimport passed with identity mesh transforms and matching triangle count/dimensions.
- PBR: 2K BaseColor, Normal, ORM, Metallic, Roughness, and sparse electric-purple Emission maps.

The Unity handoff is file-only. Unity import, attachment, prefab/catalog work, scene changes, and gameplay validation were intentionally not performed.
