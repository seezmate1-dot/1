"""Blender helpers for the art of the game: scene reset (Cycles on the CPU, saturated colours),
simple materials and shapes (bevelled boxes, cylinders, balls), lights, model import and rendering.
The models themselves come from Quaternius' kits (qkit.py)."""
import os

import bpy

SAMPLES = int(os.environ.get("HEARTH_ART_SAMPLES", "64"))



def reset(world=(0.05, 0.055, 0.08), soft=False):
    """A clean scene. soft: Khronos PBR Neutral tone mapping, so bright walls and lamps roll off instead of burning
    to white (rooms and the world); otherwise Standard, the most saturated (characters, icons)."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    import addon_utils
    addon_utils.enable("cycles", default_set=True)
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = SAMPLES
    sc.cycles.use_denoising = True
    sc.cycles.max_bounces = 5
    sc.cycles.caustics_reflective = False
    sc.cycles.caustics_refractive = False
    sc.render.resolution_x, sc.render.resolution_y = 768, 384
    sc.render.resolution_percentage = 100
    # Standard keeps toy colours saturated (AgX would wash them out)
    if soft:
        sc.view_settings.view_transform = "Khronos PBR Neutral"
        sc.view_settings.look = "None"
        sc.view_settings.exposure = 0.1
    else:
        sc.view_settings.view_transform = "Standard"
        sc.view_settings.look = "Medium High Contrast"
        sc.view_settings.exposure = -0.2
    w = bpy.data.worlds.new("World")
    sc.world = w
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = world + (1,)
    _mats.clear()
    return sc


# ------------------------------------------------------------------ materials

_mats = {}


def mat(color, rough=0.5, metal=0.0, emit=None, strength=0.0, name=None):
    """Toy plastic-and-paint: a flat colour with a soft sheen."""
    key = (tuple(round(c, 3) for c in color), rough, metal, emit, strength)
    if key in _mats:
        return _mats[key]
    m = bpy.data.materials.new(name or "toy")
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = tuple(color) + (1,)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    if "Coat Weight" in b.inputs:
        b.inputs["Coat Weight"].default_value = 0.15
    if emit:
        b.inputs["Emission Color"].default_value = tuple(emit) + (1,)
        b.inputs["Emission Strength"].default_value = strength
    _mats[key] = m
    return m


def jitter(color, rng, amount=0.04):
    return tuple(max(0, min(1, c + rng.uniform(-amount, amount))) for c in color)


# ------------------------------------------------------------------ shapes

def rbox(size, loc, material, bevel=0.04, rot=(0, 0, 0), name="block"):
    """A rounded box: the basic toy brick."""
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.name = name
    o.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel > 0:
        mod = o.modifiers.new("bevel", "BEVEL")
        mod.width = min(bevel, min(size) * 0.45)
        mod.segments = 3
        mod.limit_method = "NONE"
    o.data.materials.append(material)
    bpy.ops.object.shade_smooth()
    return o


def cyl(radius, depth, loc, material, rot=(0, 0, 0), verts=24, bevel=0.0):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth, location=loc, rotation=rot)
    o = bpy.context.active_object
    if bevel:
        mod = o.modifiers.new("bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 3
    o.data.materials.append(material)
    bpy.ops.object.shade_smooth()
    return o


def ball(radius, loc, material, scale=(1, 1, 1)):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=radius, location=loc, segments=24, ring_count=12)
    o = bpy.context.active_object
    o.scale = scale
    o.data.materials.append(material)
    bpy.ops.object.shade_smooth()
    return o


def light(kind, loc, energy, color=(1.0, 0.75, 0.5), size=0.3, rot=(0, 0, 0)):
    data = bpy.data.lights.new(kind, kind)
    data.energy = energy
    data.color = color
    if kind == "AREA":
        data.size = size
    else:
        data.shadow_soft_size = size
    o = bpy.data.objects.new(kind, data)
    o.location = loc
    o.rotation_euler = rot
    bpy.context.scene.collection.objects.link(o)
    return o


# ------------------------------------------------------------------ models

def _import(path):
    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    new = [o for o in bpy.context.scene.objects if o not in before]
    for o in list(new):
        # Kenney's character files carry helper meshes (Cube, Icosphere) next to the body
        helper = o.type == "MESH" and o.name.split(".")[0] in ("Cube", "Icosphere")
        if o.type in ("CAMERA", "LIGHT") or helper:
            bpy.data.objects.remove(o, do_unlink=True)
            new.remove(o)
    roots = [o for o in new if o.parent is None or o.parent not in new]
    return new, roots


def render(path, transparent=False):
    sc = bpy.context.scene
    sc.render.film_transparent = transparent
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGBA" if transparent else "RGB"
    sc.render.filepath = path
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.render.render(write_still=True)
