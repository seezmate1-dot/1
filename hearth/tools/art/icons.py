"""Toy icons of the ten resources for the top bar (Resources/Art/Icons/<Resource>.png, 128 x 128), in
the style of the rooms (some are the Fantasy Props kit's models)."""
import math
import os
import random

import bpy

import qkit as q
import toy as t

SIZE = 128


def _gem(color, strength=0.35):
    bpy.ops.mesh.primitive_cone_add(vertices=6, radius1=0.6, radius2=0.0, depth=0.9, location=(0, 0, 0.45), rotation=(math.pi, 0, 0))
    bpy.context.active_object.data.materials.append(t.mat(color, 0.1, emit=color, strength=strength))
    bpy.ops.mesh.primitive_cone_add(vertices=6, radius1=0.6, radius2=0.35, depth=0.35, location=(0, 0, 1.07))
    bpy.context.active_object.data.materials.append(t.mat(color, 0.1, emit=color, strength=strength * 1.4))


def coal(rng):
    for k in range(5):
        t.ball(rng.uniform(0.3, 0.42), (rng.uniform(-0.4, 0.4), rng.uniform(-0.2, 0.2), 0.35 + (k > 2) * 0.35),
               t.mat((0.07, 0.07, 0.08), 0.35), scale=(1, 1, 0.85))
    t.ball(0.18, (0.1, -0.35, 0.5), t.mat((1, 0.4, 0.1), 0.3, emit=(1, 0.4, 0.1), strength=2))


def food(rng):
    q.put("FarmCrate_Apple", (0, 0, 0), scale=1.0)
    q.put("Carrot", (0.45, -0.45, 0.0), rot_z=30, scale=2.2)


def ingot(color, metal=1.0):
    def f(rng):
        for k, (x, z) in enumerate(((-0.35, 0.18), (0.35, 0.18), (0, 0.52))):
            t.rbox((0.7, 0.42, 0.34), (x, 0, z), t.mat(color, 0.35, metal * 0.35), bevel=0.06)
    return f


def essence(rng):
    t.ball(0.55, (0, 0, 0.6), t.mat((0.5, 0.25, 0.9), 0.15, emit=(0.6, 0.3, 1.0), strength=0.4))
    t.ball(0.2, (-0.18, -0.35, 0.82), t.mat((0.95, 0.85, 1.0), 0.1, emit=(0.9, 0.8, 1.0), strength=1.0))


def sparks(rng):
    _gem((0.3, 0.75, 1.0))


def ember(rng):
    import rooms
    rooms.flame(0, 0, 0, 1.6, rng)


def cores(rng):
    _gem((0.85, 0.08, 0.12))


def blueprints(rng):
    q.put("Scroll_1", (0, 0, 0), rot_z=15, scale=4.0)
    q.put("Scroll_2", (0.1, -0.3, 0.2), rot_z=-25, scale=4.0, tint=(0.62, 0.8, 1.1))


def glory(rng):
    t.cyl(0.55, 0.15, (0, 0, 0.6), t.mat((1.0, 0.72, 0.18), 0.35, 0.3), rot=(math.radians(90), 0, 0), bevel=0.04)
    t.cyl(0.38, 0.18, (0, -0.02, 0.6), t.mat((0.85, 0.55, 0.12), 0.4, 0.3), rot=(math.radians(90), 0, 0))
    for side in (-1, 1):
        t.rbox((0.22, 0.12, 0.6), (side * 0.18, 0.05, 1.25), t.mat((0.7, 0.12, 0.1), 0.6), bevel=0.04, rot=(0, math.radians(side * 15), 0))


ICONS = {
    "Coal": coal, "Food": food, "Iron": ingot((0.45, 0.47, 0.52), 0.9), "Silver": ingot((0.85, 0.87, 0.92), 1.0),
    "Essence": essence, "Sparks": sparks, "Ember": ember, "Cores": cores, "Blueprints": blueprints, "Glory": glory,
}


def render_all(out_dir):
    from characters import _bounds
    for name, build in ICONS.items():
        t.reset(world=(0.45, 0.45, 0.5))
        sc = bpy.context.scene
        sc.render.resolution_x = sc.render.resolution_y = SIZE * 2
        sc.cycles.samples = 48
        build(random.Random(name))
        meshes = [o for o in bpy.data.objects if o.type == "MESH"]
        lo, hi = _bounds(meshes)
        size = max(hi.x - lo.x, hi.z - lo.z) * 1.2
        cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
        sc.collection.objects.link(cam)
        cam.data.type = "ORTHO"
        cam.data.ortho_scale = size
        cam.location = ((lo.x + hi.x) / 2, -20, (lo.z + hi.z) / 2 + 20 * math.tan(math.radians(22)))
        cam.rotation_euler = (math.radians(68), 0, 0)
        sc.camera = cam
        t.light("AREA", (-3, -6, 6), 900, color=(1.0, 0.95, 0.88), size=5, rot=(math.radians(50), 0, math.radians(-25)))
        t.light("AREA", (4, 3, 3), 400, color=(1.0, 0.7, 0.4), size=3, rot=(math.radians(-60), 0, math.radians(140)))
        t.render(os.path.join(out_dir, name + ".png"), transparent=True)
        print("icon", name, flush=True)
