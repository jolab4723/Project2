# Echo Vault Production Retry Output Plan

This directory is reserved for postprocessing only if the separately approved slot-swap H3 retry passes every raw visual gate.

Planned outputs:

- `item.weapon.shotgun.echovault.blend`
- `item.weapon.shotgun.echovault.fbx`
- `Textures/BaseColor.png`
- `Textures/Normal.png`
- `Textures/ORM.png`
- `Textures/EmissionMask.png`
- `QA/` directional renders and clean-FBX-reimport evidence
- `production_validation.json`

Planned hierarchy and axes:

- One root named `item.weapon.shotgun.echovault`
- Applied transforms; no negative or nonuniform scale
- Muzzle direction `+Z`; weapon up `+Y`
- `RightHandGrip` at the actual trigger-hand grip center axis
- `LeftHandGrip` at the fore-end contact center
- `Muzzle` at the exact center of the verified open bore, with local `+Z` pointing outward
- No camera, light, armature, or collider in the FBX

Emission policy: derive a strict binary mask only from the designated cyan family in the authored BaseColor texture. Do not dilate, blur, morph, raycast, split the mesh/UV, or add temporary emissive geometry.
