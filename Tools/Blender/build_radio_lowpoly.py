from __future__ import annotations

import math
import random
from pathlib import Path

import bpy
from mathutils import Vector


REPO = Path(__file__).resolve().parents[2]
OUT_DIR = REPO / "Assets" / "_Project" / "Art" / "Models" / "RadioLowPoly"
TEX_DIR = OUT_DIR / "Textures"
OUT_DIR.mkdir(parents=True, exist_ok=True)
TEX_DIR.mkdir(parents=True, exist_ok=True)


def reset_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.materials, bpy.data.images, bpy.data.curves, bpy.data.meshes):
        for block in list(datablocks):
            if block.users == 0:
                datablocks.remove(block)


def save_texture(name: str, width: int, height: int, painter) -> Path:
    path = TEX_DIR / f"{name}.png"
    image = bpy.data.images.new(name, width=width, height=height, alpha=True)
    pixels = [0.0] * (width * height * 4)
    rng = random.Random(7401 + sum(ord(c) for c in name))
    for y in range(height):
        for x in range(width):
            rgba = painter(x, y, width, height, rng)
            i = (y * width + x) * 4
            pixels[i:i + 4] = rgba
    image.pixels.foreach_set(pixels)
    image.filepath_raw = str(path)
    image.file_format = "PNG"
    image.save()
    return path


def body_pixel(x, y, w, h, rng):
    grain = (rng.random() - 0.5) * 0.045
    edge = min(x, y, w - 1 - x, h - 1 - y)
    shade = -0.045 if edge < 10 else 0.0
    scratch = 0.0
    if (y * 5 + x * 3) % 193 == 0 or (y * 7 - x * 2) % 271 == 0:
        scratch = 0.10
    return (0.72 + grain + shade + scratch, 0.66 + grain + shade + scratch,
            0.52 + grain + shade + scratch, 1.0)


def black_pixel(x, y, w, h, rng):
    grain = (rng.random() - 0.5) * 0.055
    streak = 0.035 if (x + y * 4) % 137 == 0 else 0.0
    value = 0.055 + grain + streak
    return (value, value * 1.02, value * 1.05, 1.0)


def grille_pixel(x, y, w, h, rng):
    bg = 0.12 + (rng.random() - 0.5) * 0.035
    spacing = 16
    cx = (x - 8) % spacing - spacing / 2
    cy = (y - 8) % spacing - spacing / 2
    dot = cx * cx + cy * cy < 18
    if dot:
        return (0.008, 0.008, 0.009, 1.0)
    return (bg, bg * 1.03, bg * 1.05, 1.0)


def display_pixel(x, y, w, h, rng):
    u = x / max(1, w - 1)
    v = y / max(1, h - 1)
    base = 0.035 + (rng.random() - 0.5) * 0.012
    col = [base, base * 1.05, base * 1.08, 1.0]
    if x < 5 or x > w - 6 or y < 5 or y > h - 6:
        col[:3] = [0.16, 0.16, 0.15]
    for line_v, color in ((0.25, (0.92, 0.77, 0.49)),
                          (0.49, (0.92, 0.89, 0.75)),
                          (0.74, (0.92, 0.13, 0.055))):
        if abs(v - line_v) < 0.011:
            col[:3] = color
        tick = abs((u * 24.0) - round(u * 24.0))
        if tick < 0.035 and abs(v - line_v) < (0.12 if int(round(u * 24)) % 4 == 0 else 0.07):
            col[:3] = color
    if abs(u - 0.69) < 0.012:
        col[:3] = [0.95, 0.08, 0.025]
    return tuple(col)


def material_from_image(name: str, path: Path, roughness: float, metallic: float = 0.0,
                        projection: str = "XZ"):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    texcoord = nodes.new("ShaderNodeTexCoord")
    image = nodes.new("ShaderNodeTexImage")
    image.image = bpy.data.images.load(str(path), check_existing=True)
    image.interpolation = "Linear"
    links.new(texcoord.outputs["UV"], image.inputs["Vector"])
    links.new(image.outputs["Color"], shader.inputs["Base Color"])
    links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Metallic"].default_value = metallic
    return mat


def flat_material(name: str, color, roughness=0.5, metallic=0.0, emission=None):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1.0)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    if emission:
        bsdf.inputs["Emission Color"].default_value = (*emission, 1.0)
        bsdf.inputs["Emission Strength"].default_value = 2.0
    return mat


def finish_bevel(obj, width=0.08, segments=2, smooth=True):
    bevel = obj.modifiers.new("Small edge bevel", "BEVEL")
    bevel.width = width
    bevel.segments = segments
    bevel.limit_method = "ANGLE"
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.shade_smooth_by_angle()
    return obj


def cube(name, loc, scale, material, bevel=0.06, segments=2):
    bpy.ops.mesh.primitive_cube_add(location=loc)
    obj = bpy.context.object
    obj.name = name
    obj.scale = (scale[0] / 2, scale[1] / 2, scale[2] / 2)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if material:
        obj.data.materials.append(material)
    finish_bevel(obj, bevel, segments)
    return obj


def cylinder(name, loc, radius, depth, material, vertices=24, rotation=(math.pi / 2, 0, 0), bevel=0.025):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth,
                                       end_fill_type="NGON", location=loc, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    if material:
        obj.data.materials.append(material)
    finish_bevel(obj, bevel, 2)
    return obj


def cylinder_between(name, start, end, radius, material, vertices=12):
    start, end = Vector(start), Vector(end)
    vec = end - start
    midpoint = (start + end) * 0.5
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=vec.length,
                                       location=midpoint)
    obj = bpy.context.object
    obj.name = name
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(vec.normalized())
    if material:
        obj.data.materials.append(material)
    finish_bevel(obj, radius * 0.18, 1)
    return obj


def join_objects(objects, name):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    objects[0].name = name
    return objects[0]


def planar_uv_xz(obj):
    """Create deterministic front-facing UVs that survive FBX/glTF export."""
    mesh = obj.data
    if not mesh.uv_layers:
        uv_layer = mesh.uv_layers.new(name="RadioUV")
    else:
        uv_layer = mesh.uv_layers.active
        uv_layer.name = "RadioUV"
    xs = [v.co.x for v in mesh.vertices]
    zs = [v.co.z for v in mesh.vertices]
    min_x, max_x = min(xs), max(xs)
    min_z, max_z = min(zs), max(zs)
    span_x = max(max_x - min_x, 1e-6)
    span_z = max(max_z - min_z, 1e-6)
    for poly in mesh.polygons:
        for loop_index in poly.loop_indices:
            co = mesh.vertices[mesh.loops[loop_index].vertex_index].co
            uv_layer.data[loop_index].uv = ((co.x - min_x) / span_x, (co.z - min_z) / span_z)


def create_knob(name, x, z, radius, depth, black_mat, accent_mat):
    parts = [cylinder(name, (x, -0.87, z), radius, depth, black_mat, vertices=24)]
    ring = cylinder(name + "_Grip", (x, -0.865, z), radius * 1.08, depth * 0.42,
                    black_mat, vertices=20, bevel=0.018)
    parts.append(ring)
    mark = cube(name + "_Marker", (x, -1.02, z + radius * 0.42),
                (radius * 0.07, 0.025, radius * 0.33), accent_mat, bevel=0.006, segments=1)
    parts.append(mark)
    return join_objects(parts, name)


def build_radio():
    body_tex = save_texture("radio_body_beige", 256, 256, body_pixel)
    black_tex = save_texture("radio_black_metal", 256, 256, black_pixel)
    grille_tex = save_texture("radio_speaker_grille", 384, 384, grille_pixel)
    display_tex = save_texture("radio_frequency_display", 768, 256, display_pixel)

    beige = material_from_image("MAT_Body_Beige_Textured", body_tex, 0.62)
    black = material_from_image("MAT_Black_Textured", black_tex, 0.38)
    grille = material_from_image("MAT_Speaker_Grille_Texture", grille_tex, 0.52)
    display = material_from_image("MAT_Frequency_Display_Texture", display_tex, 0.24)
    metal = flat_material("MAT_Antenna_Chrome", (0.38, 0.40, 0.42), 0.21, 0.78)
    dark_metal = flat_material("MAT_Dark_Trim", (0.018, 0.019, 0.021), 0.3, 0.2)
    ivory = flat_material("MAT_Knob_Marker", (0.86, 0.80, 0.66), 0.48)

    body = cube("Radio_Body", (0, 0, 0), (4.8, 1.36, 2.72), beige, 0.18, 3)
    top = cube("Top_Black_Band", (0, -0.01, 1.33), (4.55, 1.18, 0.20), black, 0.08, 2)
    speaker_border = cube("Speaker_Border", (-1.27, -0.72, 0.04), (2.04, 0.13, 2.15), dark_metal, 0.12, 3)
    speaker = cube("Speaker_Grille", (-1.27, -0.80, 0.04), (1.82, 0.06, 1.94), grille, 0.08, 2)

    display_border = cube("Frequency_Display_Border", (0.94, -0.74, 0.62),
                          (2.35, 0.15, 0.90), dark_metal, 0.11, 2)
    display_face = cube("Frequency_Display", (0.94, -0.835, 0.62),
                        (2.16, 0.035, 0.71), display, 0.045, 2)
    # The border is joined so the complete tuning window selects as one object.
    frequency_display = join_objects([display_face, display_border], "Frequency_Display")

    large_knob = create_knob("Tuning_Knob_Large", 0.63, -0.47, 0.58, 0.24, black, ivory)
    small_knob = create_knob("Volume_Knob_Small", 1.73, -0.53, 0.36, 0.22, black, ivory)

    # Low-sided telescoping antenna, angled like the reference.
    base = cylinder("Antenna_Base", (-1.82, 0.0, 1.51), 0.15, 0.42, dark_metal,
                    vertices=12, rotation=(0, 0, 0), bevel=0.02)
    antenna_parts = [base]
    points = [(-1.76, 0.0, 1.64), (0.20, 0.0, 2.20), (1.48, 0.0, 2.52), (2.20, 0.0, 2.72)]
    radii = [0.065, 0.052, 0.040]
    for i in range(3):
        antenna_parts.append(cylinder_between(f"Antenna_Segment_{i+1}", points[i], points[i + 1],
                                              radii[i], metal, vertices=12))
    tip = cylinder_between("Antenna_Tip", (2.20, 0.0, 2.72), (2.36, 0.0, 2.77), 0.065,
                           dark_metal, vertices=12)
    antenna_parts.append(tip)
    antenna = join_objects(antenna_parts, "Telescopic_Antenna")

    # Small feet give a readable silhouette without adding dense geometry.
    cube("Foot_Left", (-1.65, 0.2, -1.43), (0.62, 0.65, 0.16), dark_metal, 0.05, 1)
    cube("Foot_Right", (1.65, 0.2, -1.43), (0.62, 0.65, 0.16), dark_metal, 0.05, 1)

    # Custom properties make the interaction pieces easy to identify in Unity/Blender.
    large_knob["radio_control"] = "tuning"
    small_knob["radio_control"] = "volume"
    frequency_display["radio_control"] = "frequency_display"
    body["asset_style"] = "low_poly_textured"
    body["source_reference"] = "ChatGPT Image Sep 25, 2026, 12_03_39 AM(1).png"

    collection = bpy.data.collections.new("Radio_LowPoly")
    bpy.context.scene.collection.children.link(collection)
    for obj in list(bpy.context.scene.objects):
        if obj.type == "MESH":
            planar_uv_xz(obj)
            for old_collection in list(obj.users_collection):
                old_collection.objects.unlink(obj)
            collection.objects.link(obj)

    return collection


def setup_render():
    world = bpy.context.scene.world
    world.color = (0.035, 0.035, 0.035)
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.025, 0.027, 0.032, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.35

    bpy.ops.object.camera_add(location=(6.25, -8.2, 4.25))
    camera = bpy.context.object
    camera.name = "Preview_Camera"
    direction = Vector((0.05, 0.0, 0.35)) - camera.location
    camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    camera.data.lens = 56
    bpy.context.scene.camera = camera

    def area(name, location, energy, size, color):
        bpy.ops.object.light_add(type="AREA", location=location)
        light = bpy.context.object
        light.name = name
        light.data.energy = energy
        light.data.shape = "DISK"
        light.data.size = size
        light.data.color = color
        light.rotation_euler = (Vector((0, 0, 0.3)) - light.location).to_track_quat("-Z", "Y").to_euler()

    area("Key_Light", (-3.5, -4.5, 6.0), 900, 5.0, (1.0, 0.84, 0.68))
    area("Fill_Light", (4.5, -2.0, 3.0), 650, 4.0, (0.62, 0.76, 1.0))
    area("Rim_Light", (0.0, 3.0, 5.0), 1100, 3.5, (1.0, 0.45, 0.22))

    # Ground is kept outside the export selection.
    bpy.ops.mesh.primitive_plane_add(size=30, location=(0, 0, -1.54))
    ground = bpy.context.object
    ground.name = "Preview_Ground"
    ground.data.materials.append(flat_material("MAT_Ground", (0.025, 0.028, 0.033), 0.72))

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1000
    scene.render.resolution_y = 800
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(OUT_DIR / "RadioLowPoly_Preview.png")
    scene.render.film_transparent = False
    scene.render.image_settings.color_mode = "RGBA"


def export_assets(collection):
    blend_path = OUT_DIR / "RadioLowPoly.blend"
    glb_path = OUT_DIR / "RadioLowPoly.glb"
    fbx_path = OUT_DIR / "RadioLowPoly.fbx"

    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    bpy.ops.render.render(write_still=True)

    bpy.ops.object.select_all(action="DESELECT")
    for obj in collection.objects:
        obj.select_set(True)
    bpy.ops.export_scene.gltf(filepath=str(glb_path), export_format="GLB", use_selection=True,
                              export_apply=True, export_materials="EXPORT")
    bpy.ops.export_scene.fbx(filepath=str(fbx_path), use_selection=True, apply_unit_scale=True,
                             use_mesh_modifiers=True, mesh_smooth_type="FACE", add_leaf_bones=False,
                             axis_forward="-Z", axis_up="Y", path_mode="COPY", embed_textures=True)

    mesh_objects = [o for o in collection.objects if o.type == "MESH"]
    vertices = sum(len(o.data.vertices) for o in mesh_objects)
    polygons = sum(len(o.data.polygons) for o in mesh_objects)
    report = OUT_DIR / "RadioLowPoly_Report.txt"
    report.write_text(
        "RadioLowPoly asset report\n"
        f"Mesh objects: {len(mesh_objects)}\n"
        f"Vertices (base meshes): {vertices}\n"
        f"Polygons (base meshes): {polygons}\n"
        "Separate interaction objects: Frequency_Display, Tuning_Knob_Large, Volume_Knob_Small\n"
        "Speaker holes are represented by a texture to keep polygon count low.\n",
        encoding="utf-8",
    )


reset_scene()
radio_collection = build_radio()
setup_render()
export_assets(radio_collection)
print(f"RADIO_ASSET_READY={OUT_DIR}")
