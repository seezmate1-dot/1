"""Decorations a player puts into rooms (cosmetic, like in Hustle Castle): plain ones for silver or
Glory, premium ones for Sparks, mostly from Quaternius' Fantasy Props MegaKit (the rooms' own style).
Each is rendered alone on a transparent background with the camera of the rooms, so it sits in a
cell naturally. The ids match Core/Decor.cs."""
import math
import os
import random

import bpy

import qkit as q
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
    """The Guardian's armour on a stand: the hero himself, standing still on a plinth."""
    import characters as c
    c._load_animations()
    rig, meshes = c.build("hero", "Guardian")
    c._play(rig, "Idle_Loop", 0.0)
    t.rbox((1.0, 0.8, 0.2), (0, 0, -0.1), t.mat((0.3, 0.18, 0.09), 0.7), bevel=0.05)


def treasure(rng):
    q.put("Chest_Wood", (0, 0.3, 0), rot_z=10)
    for x, y, n in ((-0.9, -0.2, "Coin_Pile_2"), (0.9, -0.3, "Coin_Pile"), (-0.4, -0.7, "Coin_Pile"), (0.4, -0.8, "Pouch_Large")):
        q.put(n, (x, y, 0), scale=1.3)
    for _ in range(6):
        q.put("Coin", (rng.uniform(-1, 1), rng.uniform(-1, -0.4), 0), rot_z=rng.uniform(0, 360), scale=1.3)


def shields(gold=False):
    def build(rng):
        tint = (0.5, 1.6, 1.25) if gold else None
        q.put("Shield_Wooden", (0, 0, 0.6), scale=1.6, tint=tint)
        for side in (-1, 1):
            sw = q.put("Sword_Bronze", (side * 0.25, 0.1, 0.5), scale=1.1, tint=tint)
            sw.rotation_euler = (0, math.radians(side * 40), 0)
    return build


def pumpkin(rng):
    """A carved pumpkin with a candle inside."""
    orange = t.mat((0.95, 0.42, 0.08), 0.5)
    for k in range(8):
        a = k / 8 * math.tau
        t.ball(0.42, (0.22 * math.cos(a), 0.22 * math.sin(a), 0.42), orange, scale=(0.7, 0.7, 0.9))
    t.cyl(0.06, 0.25, (0, 0, 0.88), t.mat((0.3, 0.45, 0.15), 0.6))
    glow = t.mat((1.0, 0.75, 0.2), 0.3, emit=(1.0, 0.6, 0.1), strength=8)
    for x in (-0.17, 0.17):
        bpy.ops.mesh.primitive_cone_add(vertices=3, radius1=0.09, depth=0.05, location=(x, -0.56, 0.52), rotation=(math.radians(90), 0, 0))
        bpy.context.active_object.data.materials.append(glow)
    t.rbox((0.34, 0.05, 0.08), (0, -0.56, 0.3), glow, bevel=0.02)


def shrine(rng):
    """Candles and a chalice on a draped table: a small shrine to the old Guardians."""
    t.rbox((1.6, 0.8, 0.8), (0, 0, 0.4), t.mat((0.5, 0.45, 0.42), 0.8), bevel=0.06)
    t.rbox((1.7, 0.85, 0.06), (0, 0, 0.82), t.mat((0.55, 0.1, 0.1), 0.8), bevel=0.02)
    q.put("Chalice", (0, 0, 0.85), scale=1.5)
    for x in (-0.55, 0.55):
        q.put("CandleStick_Triple", (x, 0, 0.85))
    for x in (-1.1, 1.1):
        q.put("CandleStick_Stand", (x, 0.1, 0))


def plant(rng):
    t.cyl(0.32, 0.5, (0, 0, 0.25), t.mat((0.62, 0.32, 0.18), 0.7), bevel=0.04)
    t.cyl(0.29, 0.05, (0, 0, 0.5), t.mat((0.25, 0.15, 0.08), 0.95))
    q.put("Plant_1_Big", (0, 0, 0.5), scale=0.7)


def rug(rng):
    """A tapestry of the Guardians on a wooden frame (a rug on the floor would be lost in the cutaway)."""
    wood = t.mat((0.4, 0.22, 0.1), 0.6)
    t.rbox((0.15, 0.15, 3.0), (-1.1, 0, 1.5), wood, bevel=0.04)
    t.rbox((0.15, 0.15, 3.0), (1.1, 0, 1.5), wood, bevel=0.04)
    t.rbox((2.6, 0.18, 0.15), (0, 0, 2.95), wood, bevel=0.04)
    t.rbox((2.0, 0.06, 2.3), (0, 0, 1.75), t.mat((0.8, 0.6, 0.2), 0.8), bevel=0.03)
    t.rbox((1.7, 0.08, 2.0), (0, -0.01, 1.75), t.mat((0.15, 0.25, 0.5), 0.9), bevel=0.03)
    t.ball(0.35, (0, -0.06, 1.85), t.mat((1.0, 0.55, 0.15), 0.4, emit=(1.0, 0.45, 0.1), strength=0.8), scale=(1, 0.3, 1.3))


# id: (name, premium, builder or (model, scale, tint))
CATALOG = {
    "banner_red": ("Алое знамя", False, ("Banner_1_Cloth", 1.0, None)),
    "banner_blue": ("Синее знамя", False, ("Banner_2_Cloth", 1.0, None)),
    "banner_green": ("Зелёное знамя", False, ("Banner_1_Cloth", 1.0, (0.85, 1.0, 0.9))),
    "candles": ("Подсвечник", False, ("CandleStick_Stand", 1.0, None)),
    "barrels": ("Бочки", False, ("Barrel_Holder", 1.0, None)),
    "shelf": ("Полка с бутылями", False, ("Shelf_Small_Bottles", 1.4, None)),
    "shield": ("Щит и мечи", False, shields()),
    "plant": ("Кадка с зеленью", False, plant),
    "rug": ("Гобелен Стражей", False, rug),
    "keg": ("Бочка яблок", False, ("Barrel_Apples", 1.1, None)),
    "gold_shield": ("Золотой герб", True, shields(gold=True)),
    "treasure": ("Сокровищница", True, treasure),
    "crystal": ("Кристалл угля", True, crystal),
    "armor": ("Доспехи Стража", True, armor_stand),
    "throne": ("Трон старейшины", True, throne),
    "pumpkin": ("Тыква-фонарь", True, pumpkin),
    "shrine": ("Святилище свечей", True, shrine),
    "chest_gold": ("Сундук мастера", True, ("Chest_Wood", 1.2, (0.5, 1.5, 1.2))),
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
        model, scale, tint = what
        q.put(model, (0, 0, 0), scale=scale, tint=tint)
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
    # how tall the thing is in the world (m): the game scales it against the room (3.1 m high)
    return {"name": name, "premium": premium, "height": round(hi.z - lo.z, 3), "width": round(hi.x - lo.x, 3)}


def render_all(out_dir):
    info = {}
    for key in CATALOG:
        info[key] = render_one(key, os.path.join(out_dir, key + ".png"))
        print("decor", key, flush=True)
    return info
