import bpy
import math
import os


PROJECT = r"C:\Users\user\Desktop\Project2_test\Project2"
SOURCE_FBX = os.path.join(PROJECT, r"Assets\SW\Models\ProjectileVisuals\Production49\Sol4ElementalShotguns\item.weapon.shotgun.magmacrusher.fbx")
OUTPUT_FBX = SOURCE_FBX
OUTPUT_TEXTURE = os.path.join(PROJECT, r"Assets\SW\Textures\ProjectileVisuals\Production49\Sol4ElementalShotguns\item.weapon.shotgun.magmacrusher_surfacecracks_1024.png")
OUTPUT_BLEND = os.path.join(PROJECT, r"..\Project2_BlenderWork\Sol4MagmaCrusher_SurfaceCracks_Repair4.blend")
REPORT = os.path.join(PROJECT, r"Assets\SW\TEST\ProjectileVisuals\Production49\Sol4_Magma_SurfaceCracks_Repair4_BlenderValidation.txt")


def clear_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)


def select_only(obj):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def make_uv(obj):
    select_only(obj)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    mesh = obj.data
    while mesh.uv_layers:
        mesh.uv_layers.remove(mesh.uv_layers[0])
    mesh.uv_layers.new(name="MagmaCrust_UV")
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66.0), island_margin=0.025)
    bpy.ops.object.mode_set(mode='OBJECT')


def add_distance_crack(nodes, links, vector_socket, scale, width, name):
    voronoi = nodes.new("ShaderNodeTexVoronoi")
    voronoi.name = name + "_VoronoiDistance"
    voronoi.voronoi_dimensions = '3D'
    voronoi.feature = 'DISTANCE_TO_EDGE'
    voronoi.distance = 'EUCLIDEAN'
    voronoi.inputs["Scale"].default_value = scale
    links.new(vector_socket, voronoi.inputs["Vector"])

    noise = nodes.new("ShaderNodeTexNoise")
    noise.name = name + "_WidthNoise"
    noise.noise_dimensions = '3D'
    noise.inputs["Scale"].default_value = scale * 0.42
    noise.inputs["Detail"].default_value = 5.0
    noise.inputs["Roughness"].default_value = 0.72
    links.new(vector_socket, noise.inputs["Vector"])

    width_map = nodes.new("ShaderNodeMapRange")
    width_map.name = name + "_VariableWidth"
    width_map.inputs["From Min"].default_value = 0.0
    width_map.inputs["From Max"].default_value = 1.0
    width_map.inputs["To Min"].default_value = width * 0.62
    width_map.inputs["To Max"].default_value = width * 1.42
    links.new(noise.outputs["Fac"], width_map.inputs["Value"])

    less = nodes.new("ShaderNodeMath")
    less.name = name + "_DistanceThreshold"
    less.operation = 'LESS_THAN'
    links.new(voronoi.outputs["Distance"], less.inputs[0])
    links.new(width_map.outputs["Result"], less.inputs[1])
    return less.outputs[0]


def bake_cracks(obj):
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 8
    scene.render.image_settings.file_format = 'PNG'

    image = bpy.data.images.new("Magma_SurfaceCracks_1024", width=1024, height=1024, alpha=True, float_buffer=False)
    image.generated_color = (0.0, 0.0, 0.0, 1.0)

    material = bpy.data.materials.new("MagmaCrust_SurfaceCracks_Bake")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()

    texcoord = nodes.new("ShaderNodeTexCoord")
    texcoord.name = "AuthoredGeneratedCoordinates"
    warp_noise = nodes.new("ShaderNodeTexNoise")
    warp_noise.name = "LowFrequencySurfaceWarp"
    warp_noise.noise_dimensions = '3D'
    warp_noise.inputs["Scale"].default_value = 2.6
    warp_noise.inputs["Detail"].default_value = 6.0
    warp_noise.inputs["Roughness"].default_value = 0.78
    warp_noise.inputs["Distortion"].default_value = 0.34
    links.new(texcoord.outputs["Generated"], warp_noise.inputs["Vector"])

    warp_scale = nodes.new("ShaderNodeVectorMath")
    warp_scale.name = "WarpAmplitude"
    warp_scale.operation = 'SCALE'
    warp_scale.inputs[3].default_value = 0.18
    links.new(warp_noise.outputs["Color"], warp_scale.inputs[0])
    warped = nodes.new("ShaderNodeVectorMath")
    warped.name = "WarpedSurfaceCoordinates"
    warped.operation = 'ADD'
    links.new(texcoord.outputs["Generated"], warped.inputs[0])
    links.new(warp_scale.outputs["Vector"], warped.inputs[1])

    coarse = add_distance_crack(nodes, links, warped.outputs["Vector"], 5.2, 0.034, "PrimaryBranch")
    fine = add_distance_crack(nodes, links, warped.outputs["Vector"], 11.5, 0.020, "SecondaryBranch")
    combine = nodes.new("ShaderNodeMath")
    combine.name = "BranchNetworkMaximum"
    combine.operation = 'MAXIMUM'
    links.new(coarse, combine.inputs[0])
    links.new(fine, combine.inputs[1])

    breakup = nodes.new("ShaderNodeTexNoise")
    breakup.name = "BranchBreakupNoise"
    breakup.noise_dimensions = '3D'
    breakup.inputs["Scale"].default_value = 3.8
    breakup.inputs["Detail"].default_value = 4.5
    breakup.inputs["Roughness"].default_value = 0.68
    links.new(warped.outputs["Vector"], breakup.inputs["Vector"])
    breakup_ramp = nodes.new("ShaderNodeValToRGB")
    breakup_ramp.name = "SparseBranchBreakup"
    breakup_ramp.color_ramp.elements[0].position = 0.26
    breakup_ramp.color_ramp.elements[1].position = 0.48
    links.new(breakup.outputs["Fac"], breakup_ramp.inputs["Fac"])
    masked = nodes.new("ShaderNodeMath")
    masked.name = "IrregularBranchMask"
    masked.operation = 'MULTIPLY'
    links.new(combine.outputs[0], masked.inputs[0])
    links.new(breakup_ramp.outputs["Color"], masked.inputs[1])

    principled = nodes.new("ShaderNodeBsdfPrincipled")
    principled.inputs["Base Color"].default_value = (0.006, 0.003, 0.002, 1.0)
    principled.inputs["Roughness"].default_value = 0.82
    principled.inputs["Metallic"].default_value = 0.0
    links.new(masked.outputs[0], principled.inputs["Emission Color"])
    principled.inputs["Emission Strength"].default_value = 1.0
    output = nodes.new("ShaderNodeOutputMaterial")
    links.new(principled.outputs["BSDF"], output.inputs["Surface"])

    image_node = nodes.new("ShaderNodeTexImage")
    image_node.name = "MagmaSurfaceCrackBakeTarget"
    image_node.image = image
    nodes.active = image_node

    obj.data.materials.clear()
    obj.data.materials.append(material)
    select_only(obj)
    scene.render.engine = 'CYCLES'
    bpy.ops.object.bake(type='EMIT', margin=12, use_clear=True)
    os.makedirs(os.path.dirname(OUTPUT_TEXTURE), exist_ok=True)
    image.filepath_raw = OUTPUT_TEXTURE
    image.file_format = 'PNG'
    image.save()
    return material


def export_and_validate(crust, source_summary):
    root = bpy.data.objects.new("item.weapon.shotgun.magmacrusher", None)
    bpy.context.collection.objects.link(root)
    crust.parent = root
    crust.location = (0.0, 0.0, 0.0)
    root.location = (0.0, 0.0, 0.0)
    root.rotation_euler = (0.0, 0.0, 0.0)
    root.scale = (1.0, 1.0, 1.0)

    os.makedirs(os.path.dirname(OUTPUT_BLEND), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=OUTPUT_BLEND)
    select_only(root)
    crust.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=OUTPUT_FBX,
        use_selection=True,
        object_types={'EMPTY', 'MESH'},
        apply_unit_scale=True,
        bake_space_transform=False,
        axis_forward='-Z',
        axis_up='Y',
        add_leaf_bones=False,
        path_mode='AUTO'
    )

    clear_scene()
    bpy.ops.import_scene.fbx(filepath=OUTPUT_FBX)
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
    forbidden = [obj.name for obj in bpy.context.scene.objects if obj.type in {'CAMERA', 'LIGHT', 'ARMATURE'}]
    mesh = meshes[0].data if meshes else None
    uv_values = []
    if mesh and mesh.uv_layers:
        uv_values = [(loop.uv.x, loop.uv.y) for loop in mesh.uv_layers.active.data]
    triangles = sum(len(poly.vertices) - 2 for obj in meshes for poly in obj.data.polygons)
    report = [
        "Sol4 Magma Surface Cracks Repair4 Blender Validation",
        "Source objects: " + source_summary,
        "Reimport objects: " + ", ".join(obj.name + ":" + obj.type for obj in bpy.context.scene.objects),
        "Mesh count: " + str(len(meshes)),
        "Triangle count: " + str(triangles),
        "UV layer: " + (mesh.uv_layers.active.name if mesh and mesh.uv_layers else "MISSING"),
        "UV loops: " + str(len(uv_values)),
        "UV min/max: " + ("%.6f %.6f / %.6f %.6f" % (
            min(v[0] for v in uv_values), min(v[1] for v in uv_values),
            max(v[0] for v in uv_values), max(v[1] for v in uv_values)) if uv_values else "MISSING"),
        "Forbidden object types: " + (", ".join(forbidden) if forbidden else "0"),
        "Root transform applied: " + str(all(abs(value - target) < 1e-5 for value, target in zip(
            list(bpy.context.scene.objects.get("item.weapon.shotgun.magmacrusher").scale), (1.0, 1.0, 1.0)))) if bpy.context.scene.objects.get("item.weapon.shotgun.magmacrusher") else "False",
        "FBX: " + OUTPUT_FBX,
        "Texture: " + OUTPUT_TEXTURE,
        "Blend: " + OUTPUT_BLEND,
    ]
    os.makedirs(os.path.dirname(REPORT), exist_ok=True)
    with open(REPORT, 'w', encoding='utf-8') as handle:
        handle.write("\n".join(report) + "\n")
    print("\n".join(report))


def main():
    clear_scene()
    bpy.ops.import_scene.fbx(filepath=SOURCE_FBX)
    mesh_objects = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
    source_summary = ", ".join(obj.name + "(tris=" + str(sum(len(poly.vertices) - 2 for poly in obj.data.polygons)) + ")" for obj in mesh_objects)
    print("Imported source:", source_summary)
    candidates = [obj for obj in mesh_objects if "fissure" not in obj.name.lower() and "crack" not in obj.name.lower()]
    crust_candidates = [obj for obj in candidates if "crust" in obj.name.lower() or "surface" in obj.name.lower()]
    crust = max(crust_candidates or candidates, key=lambda obj: len(obj.data.polygons))
    for obj in list(bpy.context.scene.objects):
        if obj != crust:
            bpy.data.objects.remove(obj, do_unlink=True)
    crust.name = "MagmaCrust_SurfaceIntegrated"
    crust.data.name = "MagmaCrust_SurfaceIntegrated_Mesh"
    crust.parent = None
    make_uv(crust)
    bake_cracks(crust)
    export_and_validate(crust, source_summary)


if __name__ == "__main__":
    main()
