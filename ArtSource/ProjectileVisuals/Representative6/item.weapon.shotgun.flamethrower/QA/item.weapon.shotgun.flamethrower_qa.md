# Flamethrower FLAME_CONE QA

- Asset: `item.weapon.shotgun.flamethrower`
- Status: **PASS**
- Blender: `5.1.1`
- FBX: `C:\Users\user\Desktop\Project2_test\Project2\Assets\SW\Models\ProjectileVisuals\Representative6\item.weapon.shotgun.flamethrower\item.weapon.shotgun.flamethrower.fbx`
- Source blend: `C:\Users\user\Desktop\Project2_test\Project2\ArtSource\ProjectileVisuals\Representative6\item.weapon.shotgun.flamethrower\item.weapon.shotgun.flamethrower.blend`
- Validation: FBX re-imported into a factory-empty Blender scene.

## Structure

- `FLAME_CONE_ROOT` at `(0, 0, 0)` is the emission point.
- Geometry flows along local `+Z`; `+Y` is up.
- `HotCore`, `FlameOuter`, and `FlareRing` are separate open mesh layers.
- Four asymmetric outer tongues are combined in the `FlameOuter` mesh.
- All three meshes have `UVMap`; V follows the flame flow for scrolling masks.
- Final FBX includes only the root Empty and the three mesh layers.

## Re-import metrics

- Triangles: `3544`
- Bounds min: `[-1.056835651397705, -0.6780476570129395, 0.02734474092721939]`
- Bounds max: `[0.8938561081886292, 0.8998875021934509, 3.5987164974212646]`
- Materials: `FlameOuter, FlareRing, HotCore`

## Checks

- PASS — root_present_and_named
- PASS — root_origin_at_emission_point
- PASS — geometry_min_z_at_or_above_zero
- PASS — geometry_extends_positive_z
- PASS — identity_scale_after_reimport
- PASS — identity_rotation_after_reimport
- PASS — all_meshes_have_uvs
- PASS — expected_mesh_layers_present
- PASS — expected_material_slots_present
- PASS — triangle_count_in_2k_to_6k_target
- PASS — no_camera_light_armature_in_fbx

## Previews

- `C:\Users\user\Desktop\Project2_test\Project2\ArtSource\ProjectileVisuals\Representative6\item.weapon.shotgun.flamethrower\Previews\item.weapon.shotgun.flamethrower_hero.png`
- `C:\Users\user\Desktop\Project2_test\Project2\ArtSource\ProjectileVisuals\Representative6\item.weapon.shotgun.flamethrower\Previews\item.weapon.shotgun.flamethrower_side.png`
