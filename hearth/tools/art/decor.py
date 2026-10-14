"""Decorations a player puts into rooms (cosmetic, like in Hustle Castle): plain ones for silver or
Glory, premium ones for Sparks. Each is rendered alone on a transparent background with the camera
of the rooms, so it sits in a cell naturally. The ids match Core/Decor.cs."""
import math
import os
import random

import bpy

import kit
import toy as t

SIZE = 320


def _gold():
    return t.mat((1.0, 0.72, 0.2), 0.25, 1.0)


def crystal(rng):
    """A cluster of coal crystals glowing from inside: the light of the old Guardians."""
    t.rbox((1.6, 1.2, 0.35), (0, 0, 0.17), t.mat((0.25, 0.24, 0.28), 0.8), bevel=0.12)
    for k in range(7):
        h = rng.uniform(0.9, 2.0)
        bpy.ops.mesh.primitive_cone_add(vertices=6, radius1=rng.uniform(0.18, 0.3), radius2=0.02, depth=h,
                                        location=(rng.uniform(-0.45, 0.45), rng.uniform(-0.25, 0.25), 0.3 + h / 2),
                                        rotation=(rng.uniform(-0.35, 0.35), rng.uniform(-0.35, 0.35), 0))
        c = bpy.context.active_object
        c.data.materials.append(t.mat((1.0, 0.42, 0.12), 0.15, emit=(1.0, 0.4, 0.1), strength=1.2))
    t.light("POINT", (0, -0.8, 1.2), 120, color=(1.0, 0.5, 0.2), size=0.3)


def throne(rng):
    wood = t.mat((0.4, 0.22, 0.1), 0.6)
    red = t.mat((0.6, 0.1, 0.1), 0.8)
    gold = _gold()
    t.rbox((1.5, 1.3, 0.9), (0, 0, 0.45), wood, bevel=0.1)
    t.rbox((1.3, 1.1, 0.2), (0, 0, 0.95), red, bevel=0.08)
    t.rbox((1.5, 0.3, 2.4), (0, 0.55, 1.6), wood, bevel=0.12)
    t.rbox((1.1, 0.12, 1.6), (0, 0.38, 1.7), red, bevel=0.06)
    for x in (-0.75, 0.75):
        t.rbox((0.22, 1.2, 0.5), (x, 0, 1.2), wood, bevel=0.06)
        t.ball(0.16, (x, 0.55, 2.85), gold)
    t.ball(0.22, (0, 0.55, 2.95), gold)


def armor_stand(rng):
    import characters as c
    rig, meshes = c.build("hero", "Guardian")
    c._play(rig, "Idle", 0.0)
    for o in meshes:
        if c._part(o) in ("1H_Sword",):
            bpy.data.objects.remove(o, do_unlink=True)
    t.rbox((1.4, 1.0, 0.25), (0, 0, -0.12), t.mat((0.3, 0.18, 0.09), 0.7), bevel=0.06)


def treasure(rng):
    kit.put("chest_gold", (0, 0.3, 0), rot_z=10)
    for x, y, n in ((-1.2, -0.2, "coin_stack_large"), (1.1, -0.3, "coin_stack_medium"), (-0.5, -0.8, "coin_stack_small"), (0.6, -0.9, "coin")):
        kit.put(n, (x, y, 0), scale=1.2)


def plant(rng):
    t.cyl(0.55, 0.9, (0, 0, 0.45), t.mat((0.62, 0.32, 0.18), 0.7), bevel=0.06)
    t.cyl(0.5, 0.1, (0, 0, 0.9), t.mat((0.25, 0.15, 0.08), 0.95))
    for k in range(7):
        a = k / 7 * math.tau
        h = rng.uniform(0.9, 1.5)
        t.ball(rng.uniform(0.32, 0.45), (0.35 * math.cos(a), 0.35 * math.sin(a), 0.9 + h * 0.6), t.mat(t.jitter((0.25, 0.55, 0.22), rng, 0.05), 0.6))
    t.ball(0.5, (0, 0, 1.9), t.mat((0.3, 0.62, 0.26), 0.6))


def rug(rng):
    """A tapestry of the Guardians on a wooden frame (a rug on the floor would be lost in the cutaway)."""
    wood = t.mat((0.4, 0.22, 0.1), 0.6)
    t.rbox((0.15, 0.15, 3.0), (-1.1, 0, 1.5), wood, bevel=0.04)
    t.rbox((0.15, 0.15, 3.0), (1.1, 0, 1.5), wood, bevel=0.04)
    t.rbox((2.6, 0.18, 0.15), (0, 0, 2.95), wood, bevel=0.04)
    t.rbox((2.0, 0.06, 2.3), (0, 0, 1.75), t.mat((0.8, 0.6, 0.2), 0.8), bevel=0.03)
    t.rbox((1.7, 0.08, 2.0), (0, -0.01, 1.75), t.mat((0.15, 0.25, 0.5), 0.9), bevel=0.03)
    t.ball(0.35, (0, -0.06, 1.85), t.mat((1.0, 0.55, 0.15), 0.4, emit=(1.0, 0.45, 0.1), strength=0.8), scale=(1, 0.3, 1.3))


# id: (name, premium, price, builder or (kit model, scale))
CATALOG = {
    "banner_red": ("Алое знамя", False, ("banner_red", 1.0)),
    "banner_blue": ("Синее знамя", False, ("banner_patternB_blue", 1.0)),
    "banner_green": ("Зелёное знамя", False, ("banner_triple_green", 1.0)),
    "candles": ("Подсвечник", False, ("candle_triple", 1.4)),
    "barrels": ("Бочки", False, ("barrel_small_stack", 1.0)),
    "shelf": ("Полка со свечами", False, ("shelf_small_candles", 1.2)),
    "shield": ("Щит и мечи", False, ("sword_shield", 1.0)),
    "plant": ("Кадка с зеленью", False, plant),
    "rug": ("Гобелен Стражей", False, rug),
    "keg": ("Бочонок эля", False, ("keg_decorated", 0.8)),
    "gold_shield": ("Золотой герб", True, ("sword_shield_gold", 1.0)),
    "treasure": ("Сокровищница", True, treasure),
    "crystal": ("Кристалл угля", True, crystal),
    "armor": ("Доспехи Стража", True, armor_stand),
    "throne": ("Трон старейшины", True, throne),
    "pumpkin": ("Тыква-фонарь", True, ("pumpkin_orange_jackolantern", 2.2)),
    "shrine": ("Святилище свечей", True, ("shrine_candles", 1.2)),
    "chest_gold": ("Сундук мастера", True, ("chest_gold", 1.1)),
}


def render_one(key, out):
    name, premium, what = CATALOG[key]
    t.reset(world=(0, 0, 0))
    sc = bpy.context.scene
    sc.render.resolution_x = sc.render.resolution_y = SIZE
    sc.cycles.samples = 48
    rng = random.Random(key)
    if callable(what):
        what(rng)
    else:
        model, scale = what
        kit.put(model, (0, 0, 0), scale=scale)
    if key == "pumpkin":
        t.light("POINT", (0, 0, 0.8), 60, color=(1.0, 0.5, 0.15), size=0.2)
    from characters import _bounds
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    lo, hi = _bounds(meshes)
    size = max(hi.x - lo.x, hi.z - lo.z) * 1.15
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    sc.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = size
    cz = (lo.z + hi.z) / 2
    cam.location = ((lo.x + hi.x) / 2, -30, cz + 30 * math.tan(math.radians(10)))
    cam.rotation_euler = (math.radians(80), 0, 0)
    sc.camera = cam
    s = max(1.0, size / 3)
    t.light("AREA", (-3 * s, -6 * s, 5 * s), 900 * s * s, color=(1.0, 0.94, 0.86), size=5 * s, rot=(math.radians(50), 0, math.radians(-25)))
    t.light("AREA", (4 * s, 3 * s, 3 * s), 500 * s * s, color=(1.0, 0.62, 0.32), size=3 * s, rot=(math.radians(-60), 0, math.radians(140)))
    t.render(out, transparent=True)
    # how tall the thing is in the world: the game scales it against the room (7 units high)
    return {"name": name, "premium": premium, "height": round(hi.z - lo.z, 3), "width": round(hi.x - lo.x, 3)}


def render_all(out_dir):
    kit.SCALE = 1.3
    info = {}
    for key in CATALOG:
        info[key] = render_one(key, os.path.join(out_dir, key + ".png"))
        print("decor", key, flush=True)
    return info
