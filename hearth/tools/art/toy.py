"""Toy-like cutaway art in Blender (the look of Hustle Castle: chunky rounded shapes, bright
saturated colours, soft light). Rooms are built from bevelled blocks and Kenney's CC0 toy models,
rendered by Cycles on the CPU. The rooms themselves are assembled from KayKit models (kit.py).

A room is a box open to the camera, 4 m wide, 2.5 m high, 3 m deep, inside a frame of big rounded
rocks, so neighbouring cells tile into one mountain.
"""
import math
import os
import random

import bpy
from mathutils import Vector

ROOM_W, ROOM_H, ROOM_D = 4.0, 2.5, 3.0
FRAME = 0.16
RES = (768, 480)
SAMPLES = int(os.environ.get("HEARTH_ART_SAMPLES", "64"))

# palette: warm toy colours, cold rock
STONE = [(0.42, 0.38, 0.36), (0.48, 0.43, 0.39), (0.37, 0.34, 0.33), (0.52, 0.47, 0.42), (0.45, 0.41, 0.4)]
ROCK = [(0.16, 0.17, 0.22), (0.19, 0.2, 0.25), (0.14, 0.15, 0.19), (0.21, 0.21, 0.26)]
PLANK = [(0.62, 0.36, 0.18), (0.56, 0.32, 0.16), (0.68, 0.41, 0.2), (0.6, 0.35, 0.17)]
WOOD_DARK = (0.32, 0.18, 0.09)


def reset(world=(0.05, 0.055, 0.08)):
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
    sc.render.resolution_x, sc.render.resolution_y = RES
    sc.render.resolution_percentage = 100
    # Standard keeps toy colours saturated (AgX would wash them out)
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


# ------------------------------------------------------------------ the room

def stone_wall(width, height, y, rng, palette=STONE, block_h=0.3, x0=None):
    """Back wall of chunky rounded stones in offset rows over dark mortar."""
    x0 = -width / 2 if x0 is None else x0
    rbox((width, 0.1, height), (x0 + width / 2, y + 0.08, height / 2), mat((0.16, 0.14, 0.14), 0.9), bevel=0)
    z = 0.0
    row = 0
    while z < height:
        x = x0 - (0.25 if row % 2 else 0.0)
        while x < x0 + width:
            w = rng.uniform(0.38, 0.62)
            bw = min(w, x0 + width - max(x, x0))
            cx = max(x, x0) + bw / 2
            if bw > 0.08:
                rbox((bw - 0.04, 0.16, block_h - 0.04), (cx, y - rng.uniform(0, 0.03), z + block_h / 2),
                     mat(jitter(rng.choice(palette), rng)), bevel=0.05)
            x += w
        z += block_h
        row += 1


def side_wall(x, height, depth, rng, palette=STONE):
    """Side walls: smooth plaster with a stone base, read in perspective."""
    rbox((0.2, depth, height), (x, depth / 2, height / 2), mat(jitter(palette[0], rng, 0.02), 0.8), bevel=0.02)


def plank_floor(width, depth, rng, palette=PLANK):
    z = 0.0
    y = 0.0
    rbox((width + 0.4, depth + 0.4, 0.2), (0, depth / 2, -0.12), mat(WOOD_DARK, 0.8), bevel=0)
    while y < depth:
        x = -width / 2 - rng.uniform(0, 0.6)
        while x < width / 2:
            length = rng.uniform(0.9, 1.6)
            a, b = max(x, -width / 2), min(x + length, width / 2)
            if b - a > 0.1:
                rbox((b - a - 0.02, 0.24, 0.06), ((a + b) / 2, y + 0.13, z - 0.03), mat(jitter(rng.choice(palette), rng)), bevel=0.015)
            x += length
        y += 0.26


def rock_frame(width, height, rng):
    """The cut through the mountain: big rounded rocks around the opening."""
    hw, t = width / 2, FRAME
    def rocks_line(a, b, fixed, horizontal, thickness):
        pos = a
        while pos < b:
            size = rng.uniform(0.35, 0.6)
            s = (size, 0.5, thickness) if horizontal else (thickness, 0.5, size)
            c = (pos + size / 2, -0.3, fixed) if horizontal else (fixed, -0.3, pos + size / 2)
            rbox((s[0] + 0.05, s[1], s[2] + rng.uniform(0, 0.06)), c, mat(jitter(rng.choice(ROCK), rng, 0.03), 0.85), bevel=0.09)
            pos += size
    rocks_line(-hw - 0.4, hw + 0.4, height + 0.18 - t, True, 0.5)
    rocks_line(-hw - 0.4, hw + 0.4, -0.18 + t, True, 0.5)
    rocks_line(-0.4, height + 0.4, -hw - 0.18 + t, False, 0.5)
    rocks_line(-0.4, height + 0.4, hw + 0.18 - t, False, 0.5)


def beams(width, height, depth):
    wood = mat((0.42, 0.24, 0.12), 0.7)
    for x in (-width / 2 + 0.14, width / 2 - 0.14):
        rbox((0.2, 0.2, height), (x, 0.3, height / 2), wood, bevel=0.03)
    rbox((width, 0.22, 0.22), (0, 0.3, height - 0.11), wood, bevel=0.03)
    for x in (-width / 4, width / 4):
        rbox((0.16, depth, 0.16), (x, depth / 2, height - 0.08), wood, bevel=0.03)
    rbox((width + 0.4, depth + 0.4, 0.2), (0, depth / 2, height + 0.1), mat((0.18, 0.12, 0.08), 0.9), bevel=0)


def shell(seed, width=ROOM_W, wall=STONE, floor=PLANK, side=None, wooden_beams=True):
    rng = random.Random(seed)
    stone_wall(width, ROOM_H, ROOM_D, rng, wall)
    for x in (-width / 2 - 0.1, width / 2 + 0.1):
        side_wall(x, ROOM_H, ROOM_D, rng, side or wall)
    plank_floor(width, ROOM_D, rng, floor)
    if wooden_beams:
        beams(width, ROOM_H, ROOM_D)
    rock_frame(width, ROOM_H, rng)
    return rng


def camera(width=ROOM_W):
    sc = bpy.context.scene
    sc.render.resolution_x = int(RES[0] * width / ROOM_W)
    data = bpy.data.cameras.new("cam")
    data.sensor_fit = "HORIZONTAL"
    dist = 6.0
    data.angle = 2 * math.atan((width - 2 * FRAME + 0.06) / 2 / dist)
    cam = bpy.data.objects.new("cam", data)
    sc.collection.objects.link(cam)
    cam.location = (0, -dist, ROOM_H / 2 + 0.9)
    target = Vector((0, 0.45, ROOM_H / 2 - 0.08))
    cam.rotation_euler = (target - Vector(cam.location)).to_track_quat("-Z", "Y").to_euler()
    sc.camera = cam
    return cam


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


def mood(key=1.0, warm=1.0, cold=False):
    """Soft bright toy light: a big key from the front-top, warm lamps inside, a little bounce."""
    light("AREA", (0, -2.0, ROOM_H + 0.6), 150 * key, color=(0.62, 0.72, 1.0) if cold else (1.0, 0.9, 0.78),
          size=5, rot=(math.radians(55), 0, 0))
    light("POINT", (0, ROOM_D * 0.55, ROOM_H - 0.55), 110 * warm, color=(1.0, 0.62, 0.32), size=0.6)


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


def place(path, loc, height=None, rot_z=0.0, scale=None, recolor=None):
    """Imports a model, scales it to a height (m) and stands it on the floor at loc."""
    new, roots = _import(path)
    pivot = bpy.data.objects.new("pivot", None)
    bpy.context.scene.collection.objects.link(pivot)
    for r in roots:
        r.parent = pivot
    bpy.context.view_layer.update()
    pts = [o.matrix_world @ Vector(c) for o in new if o.type == "MESH" for c in o.bound_box]
    if pts:
        lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
        hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
        s = scale if scale else (height / max(1e-6, hi.z - lo.z) if height else 1.0)
        centre = (lo + hi) / 2
        for r in roots:
            r.location -= Vector((centre.x, centre.y, lo.z))
        pivot.scale = (s, s, s)
    pivot.rotation_euler = (0, 0, math.radians(rot_z))
    pivot.location = loc
    if recolor:
        for o in new:
            if o.type == "MESH":
                o.data.materials.clear()
                o.data.materials.append(recolor)
    return pivot, new


# ------------------------------------------------------------------ toy furniture

def bed(loc, rot_z=0, blanket=(0.7, 0.18, 0.16)):
    """A chunky wooden bed with a fat mattress, a pillow and a blanket."""
    x, y, _ = loc
    wood = mat((0.5, 0.28, 0.13), 0.6)
    group = []
    def at(dx, dy, dz):
        a = math.radians(rot_z)
        return (x + dx * math.cos(a) - dy * math.sin(a), y + dx * math.sin(a) + dy * math.cos(a), dz)
    r = (0, 0, math.radians(rot_z))
    group.append(rbox((0.9, 1.9, 0.28), at(0, 0, 0.22), wood, 0.05, r))
    group.append(rbox((0.95, 0.12, 0.85), at(0, 0.95, 0.42), wood, 0.05, r))
    group.append(rbox((0.95, 0.12, 0.42), at(0, -0.95, 0.25), wood, 0.05, r))
    group.append(rbox((0.82, 1.78, 0.2), at(0, 0, 0.44), mat((0.92, 0.88, 0.8), 0.7), 0.08, r))
    group.append(rbox((0.6, 0.32, 0.14), at(0, 0.7, 0.58), mat((0.98, 0.96, 0.92), 0.7), 0.07, r))
    group.append(rbox((0.86, 1.15, 0.08), at(0, -0.28, 0.56), mat(blanket, 0.75), 0.04, r))
    return group


def hanging_lamp(loc, energy=55):
    """A toy lantern on a chain; its light makes the room cosy."""
    x, y, z = loc
    rbox((0.02, 0.02, ROOM_H - z), (x, y, (ROOM_H + z) / 2), mat((0.15, 0.15, 0.17), 0.4, 0.8), bevel=0)
    rbox((0.26, 0.26, 0.05), (x, y, z + 0.17), mat((0.2, 0.2, 0.22), 0.4, 0.8), bevel=0.02)
    rbox((0.22, 0.22, 0.28), (x, y, z), mat((1.0, 0.75, 0.4), 0.3, emit=(1.0, 0.6, 0.25), strength=6), bevel=0.04)
    rbox((0.26, 0.26, 0.05), (x, y, z - 0.16), mat((0.2, 0.2, 0.22), 0.4, 0.8), bevel=0.02)
    light("POINT", (x, y, z - 0.05), energy, color=(1.0, 0.62, 0.3), size=0.12)


def rug(loc, size, color=(0.62, 0.12, 0.12), border=(0.9, 0.7, 0.25)):
    x, y = loc
    rbox((size[0], size[1], 0.03), (x, y, 0.015), mat(border, 0.85), bevel=0.012)
    rbox((size[0] - 0.12, size[1] - 0.12, 0.035), (x, y, 0.02), mat(color, 0.9), bevel=0.012)


def render(path, transparent=False):
    sc = bpy.context.scene
    sc.render.film_transparent = transparent
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGBA" if transparent else "RGB"
    sc.render.filepath = path
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.render.render(write_still=True)
