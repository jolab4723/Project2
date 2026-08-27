# Conversation Starter Unity handoff

This folder is the import-ready handoff only; it does not modify Unity Assets.

- FBX: `item.weapon.shotgun.conversationstarter.fbx`
- Axis: muzzle `+Z`, up `+Y`
- Hierarchy: one item-id root, one mesh child, `RightHandGrip`, `LeftHandGrip`, and `Muzzle`
- Final mesh: 93,870 vertices / 187,772 triangles
- Topology: boundary 0 / non-manifold 0 / loose 0 / degenerate 0
- Muzzle center depth: 0.206454 m after one-meter normalization (required 0.08 m)
- Textures: BaseColor, Normal, ORM, Metallic, Roughness, and exact sparse Emission mask
- Validation: clean FBX reimport passed; character attachment and Unity material remap were intentionally not run

See `production_manifest.json` and `QA/` for machine-readable evidence.
