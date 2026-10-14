"""The rooms of the cutaway, built from KayKit's dungeon kit and props (CC0) in Blender and rendered
by Cycles: one picture per room type, the hearth over two cells, an empty dug cell and bare rock.

A room is 12 units wide, 8 deep and 7 high (a KayKit wall module is 4 x 4, a character about 2.4),
open to the camera and framed by the rock of the mountain, so the cells tile into one cutaway.
Run: blenv/bin/python hearth/tools/art/render_all.py rooms
"""
import math
import random

import bpy
from mathutils import Vector

import kit
import toy as t

W, D, H = 12.0, 8.0, 6.0
PROP = 1.3   # props a little bigger than KayKit's native scale: the room reads fuller
RES = (768, 480)
FIRE = {"on": True, "seed": 0}   # the hearth's fire is animated in the game: rendered apart
ROCK = [(0.11, 0.105, 0.12), (0.13, 0.125, 0.14), (0.095, 0.09, 0.105), (0.15, 0.14, 0.15)]
WOOD = (0.45, 0.26, 0.13)


# ------------------------------------------------------------------ the shell

def walls(back="wall", rows=2, width=W, extra=None):
    """Back wall of dungeon modules (two rows), side walls, wooden floor, beams and ceiling."""
    n = int(round(width / 4))
    stretch = H / 4.0
    for i in range(n):
        x = -width / 2 + 2 + 4 * i
        name = extra[i] if extra and i in extra else back
        w = kit.put(name, (x, D + 0.5, 0), rot_z=180)
        w.scale = (1, 1, stretch)
    for side in (-1, 1):
        for j in range(2):
            w = kit.put("wall", (side * (width / 2 + 0.5), 2 + 4 * j, 0), rot_z=90 * side)
            w.scale = (1, 1, stretch)
    for i in range(n):
        for j in range(2):
            kit.put("floor_wood_large", (-width / 2 + 2 + 4 * i, 2 + 4 * j, 0))
    beam = t.mat((0.36, 0.2, 0.1), 0.75)
    for x in (-width / 2 + 0.3, width / 2 - 0.3):
        t.rbox((0.55, 0.55, H), (x, 0.7, H / 2), beam, bevel=0.08)
    t.rbox((width, 0.6, 0.55), (0, 0.7, H - 0.28), beam, bevel=0.08)
    for k in range(1, n):
        t.rbox((0.5, D, 0.5), (-width / 2 + 4 * k, D / 2, H - 0.25), beam, bevel=0.08)
    t.rbox((width + 2, D + 2, 0.6), (0, D / 2, H + 0.3), t.mat((0.2, 0.12, 0.07), 0.9), bevel=0)


def rock_frame(width, rng):
    """The cut through the mountain: big rounded rocks around the opening."""
    hw = width / 2
    def line(a, b, fixed, horizontal):
        pos = a
        while pos < b:
            size = rng.uniform(1.1, 1.9)
            dims = (size + 0.15, 1.6, 1.6 + rng.uniform(0, 0.25)) if horizontal else (1.6 + rng.uniform(0, 0.25), 1.6, size + 0.15)
            centre = (pos + size / 2, -0.9, fixed) if horizontal else (fixed, -0.9, pos + size / 2)
            t.rbox(dims, centre, t.mat(t.jitter(rng.choice(ROCK), rng, 0.015), 0.97), bevel=0.22)
            pos += size
    line(-hw - 1.5, hw + 1.5, H + 0.55, True)
    line(-hw - 1.5, hw + 1.5, -0.55, True)
    line(-1.5, H + 1.5, -hw - 0.55, False)
    line(-1.5, H + 1.5, hw + 0.55, False)


def camera(width=W):
    sc = bpy.context.scene
    sc.render.resolution_x = int(RES[0] * width / W)
    sc.render.resolution_y = RES[1]
    data = bpy.data.cameras.new("cam")
    data.sensor_fit = "HORIZONTAL"
    dist = 18.0
    data.angle = 2 * math.atan((width + 0.3) / 2 / dist)
    cam = bpy.data.objects.new("cam", data)
    sc.collection.objects.link(cam)
    cam.location = (0, -dist, H / 2 + 2.4)
    target = Vector((0, 1.4, H / 2 - 0.25))
    cam.rotation_euler = (target - Vector(cam.location)).to_track_quat("-Z", "Y").to_euler()
    sc.camera = cam


def light_room(key=1.0, warm=1.0, width=W, cold=False):
    s = width / W
    # the key light hangs inside the opening, so the room glows and the rock around it stays dark
    t.light("AREA", (0, 0.6, H - 0.4), 1500 * key * s, color=(0.65, 0.75, 1.0) if cold else (1.0, 0.93, 0.82), size=10 * s,
            rot=(math.radians(-20), 0, 0))
    t.light("AREA", (0, -14, H + 4), 260 * s, color=(0.55, 0.62, 0.85), size=20 * s, rot=(math.radians(60), 0, 0))
    t.light("AREA", (0, D * 0.5, H - 0.8), 900 * warm * s, color=(1.0, 0.66, 0.36), size=6 * s, rot=(0, 0, 0))


def torch(x, z=3.2, wall_y=D):
    """A burning torch on the back wall that really lights the stones around it."""
    kit.put("torch_mounted", (x, wall_y, z), rot_z=180)
    t.light("POINT", (x, wall_y - 0.7, z + 0.9), 260, color=(1.0, 0.55, 0.2), size=0.25)


def coal_pile(x, y, r=1.0, rng=None, glow=False):
    rng = rng or random.Random(int(x * 10 + y))
    for _ in range(int(14 * r)):
        px, py = x + rng.uniform(-r, r), y + rng.uniform(-r * 0.6, r * 0.6)
        size = rng.uniform(0.25, 0.45) * r
        hot = glow and rng.random() < 0.3
        m = t.mat((0.9, 0.3, 0.08), 0.4, emit=(1.0, 0.35, 0.05), strength=8) if hot else t.mat((0.08, 0.08, 0.09), 0.6)
        t.ball(size, (px, py, size * 0.6 + rng.uniform(0, 0.3) * r), m, scale=(1, 1, 0.8))


def cart(x, y, load=(0.08, 0.08, 0.09), shiny=False):
    """A mine cart full of coal or ore on two rails."""
    wood = t.mat((0.42, 0.24, 0.12), 0.7)
    iron = t.mat((0.2, 0.2, 0.23), 0.4, 0.8)
    for dy in (-0.55, 0.55):
        t.rbox((5.0, 0.15, 0.12), (x, y + dy, 0.08), iron, bevel=0.03)
    t.rbox((2.2, 1.4, 1.0), (x, y, 0.95), wood, bevel=0.12)
    t.rbox((2.3, 1.5, 0.15), (x, y, 1.45), iron, bevel=0.05)
    for dx in (-0.7, 0.7):
        for dy in (-0.72, 0.72):
            t.cyl(0.32, 0.18, (x + dx, y + dy, 0.35), iron, rot=(math.radians(90), 0, 0))
    rng = random.Random(int(x * 13 + y))
    for _ in range(12):
        m = t.mat(load, 0.25 if shiny else 0.6, 0.85 if shiny else 0.0)
        t.ball(rng.uniform(0.22, 0.34), (x + rng.uniform(-0.8, 0.8), y + rng.uniform(-0.45, 0.45), 1.55 + rng.uniform(0, 0.2)), m, scale=(1, 1, 0.8))


def ore_vein(x, z, rng, color=(0.55, 0.58, 0.66), metal=0.85, n=10):
    """Shiny lumps of ore sticking out of the back wall."""
    for _ in range(n):
        t.ball(rng.uniform(0.18, 0.32), (x + rng.uniform(-1.4, 1.4), D - 0.05, z + rng.uniform(-0.9, 0.9)), t.mat(color, 0.25, metal), scale=(1, 0.6, 1))


def furnace(x, y, width=3.0, height=3.2):
    rng = random.Random(int(x * 7))
    for row in range(int(height / 0.6)):
        for k in range(int(width / 0.9)):
            c = t.jitter((0.36, 0.34, 0.37), rng, 0.04)
            t.rbox((0.86, 2.0, 0.56), (x - width / 2 + 0.45 + k * 0.9 + (0.2 if row % 2 else 0), y, 0.3 + row * 0.6), t.mat(c, 0.85), bevel=0.12)
    t.rbox((width * 0.55, 0.3, height * 0.42), (x, y - 1.0, height * 0.3), t.mat((0.05, 0.03, 0.02), 0.9), bevel=0.15)
    t.rbox((width * 0.45, 0.2, height * 0.22), (x, y - 1.05, height * 0.18), t.mat((1, 0.4, 0.1), 0.4, emit=(1.0, 0.45, 0.1), strength=15), bevel=0.1)
    t.light("POINT", (x, y - 1.6, height * 0.3), 700, color=(1.0, 0.45, 0.15), size=0.6)


def anvil(x, y):
    iron = t.mat((0.22, 0.23, 0.27), 0.35, 0.8)
    t.rbox((0.7, 0.5, 0.9), (x, y, 0.45), t.mat(WOOD, 0.7), bevel=0.08)
    t.rbox((1.4, 0.55, 0.45), (x, y, 1.1), iron, bevel=0.08)
    t.rbox((0.5, 0.4, 0.3), (x - 0.75, y, 1.18), iron, bevel=0.1)


def target(x, y, z=2.6):
    for r, c in ((0.9, (0.95, 0.92, 0.85)), (0.68, (0.75, 0.15, 0.12)), (0.46, (0.95, 0.92, 0.85)), (0.24, (0.75, 0.15, 0.12))):
        t.cyl(r, 0.2 + (0.9 - r) * 0.1, (x, y, z), t.mat(c, 0.7), rot=(math.radians(90), 0, 0))


def plant_bed(x, y, rng, kind="green"):
    t.rbox((3.4, 1.4, 0.8), (x, y, 0.4), t.mat(WOOD, 0.7), bevel=0.12)
    t.rbox((3.2, 1.2, 0.2), (x, y, 0.75), t.mat((0.25, 0.16, 0.1), 0.95), bevel=0.05)
    for k in range(5):
        px = x - 1.3 + k * 0.65
        if kind == "pumpkin":
            kit.put(rng.choice(["pumpkin_orange_small", "pumpkin_yellow_small"]), (px, y, 0.85), rot_z=rng.uniform(0, 360), scale=1.2)
        else:
            h = rng.uniform(0.6, 1.3)
            t.cyl(0.06, h, (px, y, 0.8 + h / 2), t.mat((0.25, 0.5, 0.18), 0.7))
            t.ball(rng.uniform(0.3, 0.45), (px, y, 0.8 + h), t.mat(t.jitter((0.3, 0.62, 0.25), rng, 0.06), 0.6))
            if rng.random() < 0.5:
                t.ball(0.12, (px + 0.2, y - 0.25, 0.8 + h * 0.8), t.mat((0.85, 0.2, 0.15), 0.4))


def window_night(x, z=4.6, w=2.8, h=2.4):
    """A window cut through the rock to the night: sky, stars and the moon behind the glass."""
    sky = t.mat((0.1, 0.16, 0.35), 0.9, emit=(0.12, 0.2, 0.45), strength=1.2)
    t.rbox((w, 0.2, h), (x, D + 0.05, z), sky, bevel=0)
    t.ball(0.35, (x + 0.6, D - 0.05, z + 0.55), t.mat((1, 0.97, 0.85), 0.5, emit=(1, 0.95, 0.8), strength=4))
    frame = t.mat((0.3, 0.29, 0.33), 0.8)
    for dz in (-h / 2, h / 2):
        t.rbox((w + 0.4, 0.5, 0.35), (x, D - 0.1, z + dz), frame, bevel=0.1)
    for dx in (-w / 2, 0, w / 2):
        t.rbox((0.3, 0.5, h + 0.3), (x + dx, D - 0.1, z), frame, bevel=0.1)


# ------------------------------------------------------------------ the rooms

def r_bedrooms(rng):
    walls(extra={1: "wall_shelves"})
    for x in (-4.0, 0.0, 4.0):
        kit.put("bed_decorated", (x - 0.4, 5.7, 0), rot_z=0)
    for x in (-2.0, 2.0):
        kit.put("trunk_small_A", (x, 6.8, 0), rot_z=180)
        kit.put("candle_lit", (x, 6.8, 1.1))
    rug(0, 2.6, 5.0, 2.6)
    kit.put("banner_patternA_red", (-4, D, 0.2), rot_z=180)
    kit.put("banner_patternA_blue", (4, D, 0.2), rot_z=180)
    torch(-2.0)
    torch(2.0)


def r_kitchen(rng):
    walls(extra={0: "wall_shelves"})
    furnace(3.6, 6.8, width=4.0, height=3.0)
    kit.put("stew_pot", (3.6, 5.2, 0), scale=1.6)
    kit.put("kitchentable_A_large", (-2.5, 4.5, 0), rot_z=90, scale=1.1)
    for i, f in enumerate(["food_ingredient_carrot", "food_ingredient_cheese", "food_ingredient_ham", "bowl", "cuttingboard"]):
        kit.put(f, (-3.5 + i * 0.6, 4.5 + rng.uniform(-0.5, 0.5), 2.1), rot_z=rng.uniform(0, 360), scale=0.8)
    for i, c in enumerate(["crate_carrots", "crate_potatoes", "crate_tomatoes", "crate_onions"]):
        kit.put(c, (-5.0 + (i % 2) * 1.3, 1.6 + (i // 2) * 1.3, 0), rot_z=rng.uniform(-10, 10), scale=1.3)
    kit.put("barrel_small_stack", (0.6, 7.0, 0))
    kit.put("jar_A_large", (-0.6, 7.0, 0), scale=1.5)
    torch(-2.0)


def r_coal_mine(rng):
    walls(back="wall_cracked", extra={1: "wall_scaffold"})
    for x, y, r in ((-3.8, 6.4, 1.5), (3.8, 6.4, 1.3)):
        coal_pile(x, y, r, rng, glow=True)
    ore_vein(0.2, 3.2, rng, color=(0.06, 0.06, 0.07), metal=0.0, n=14)
    cart(0.4, 3.0)
    kit.put("bucket_empty", (-4.6, 2.4, 0), scale=1.4)
    kit.put("barrel_small", (4.8, 2.4, 0))
    kit.put("lantern_hanging", (-1.5, 3.5, 4.4), scale=1.3)
    t.light("POINT", (-1.5, 3.5, 4.0), 300, color=(1.0, 0.6, 0.25), size=0.3)
    torch(3.0)


def r_barracks(rng):
    walls(extra={1: "wall_shelves"})
    kit.put("table_long", (0, 3.6, 0), rot_z=90, scale=0.95)
    for x in (-1.6, 1.6):
        kit.put("stool", (x, 2.2, 0))
    for x, n in ((-4.0, "sword_shield"), (4.0, "sword_shield_gold")):
        kit.put(n, (x, D - 0.3, 3.0), rot_z=180, scale=1.1)
    kit.put("banner_shield_red", (0, D, 0.2), rot_z=180)
    for x in (-4.6, 4.6):
        kit.put("chest", (x, 5.6, 0), rot_z=180 if x > 0 else 0)
    kit.put("shield_round_color", (-4.6, 4.2, 1.3), rot_z=40, scale=1.6)
    kit.put("axe_2handed", (4.8, 3.6, 0.2), rot_z=10, scale=1.6)
    torch(-2.0)
    torch(2.0)


def r_warehouse(rng):
    walls()
    kit.put("crates_stacked", (-4.0, 5.8, 0))
    kit.put("box_stacked", (-1.2, 6.4, 0))
    kit.put("barrel_large_decorated", (2.0, 6.0, 0))
    kit.put("keg_decorated", (4.6, 6.6, 0), rot_z=180, scale=0.8)
    kit.put("trunk_large_A", (-3.6, 2.4, 0), rot_z=15)
    kit.put("chest_gold", (0.6, 2.6, 0), rot_z=-10)
    kit.put("coin_stack_large", (2.2, 2.2, 0), scale=1.4)
    kit.put("coin_stack_medium", (3.1, 2.8, 0), scale=1.4)
    kit.put("box_small_decorated", (4.6, 2.4, 0), rot_z=20)
    torch(0.0)


def r_mine(rng):
    walls(back="wall_cracked", extra={0: "wall_scaffold", 2: "wall_scaffold"})
    ore_vein(-3.4, 3.4, rng)
    ore_vein(3.6, 2.8, rng, color=(0.85, 0.86, 0.92), metal=1.0)
    cart(0.0, 3.2, load=(0.6, 0.62, 0.7), shiny=True)
    kit.put("crate_A_big", (-4.4, 5.8, 0), scale=1.2)
    kit.put("bucket_water", (4.4, 6.2, 0), scale=1.4)
    kit.put("ladder", (4.9, 6.8, 0), rot_z=180, scale=1.8)
    kit.put("resource_stone", (-4.4, 2.2, 0), scale=1.4)
    torch(-1.0)


def r_tavern(rng):
    walls(extra={2: "wall_shelves"})
    kit.put("table_long_tablecloth_decorated_A", (-2.4, 4.0, 0), rot_z=90)
    kit.put("table_medium_tablecloth_decorated_B", (3.2, 3.6, 0))
    for x in (-4.4, -2.4, -0.4):
        kit.put("stool", (x, 2.4, 0))
    kit.put("keg_decorated", (0, 7.0, 0), rot_z=180, scale=0.8)
    kit.put("barrel_large", (4.8, 6.6, 0), scale=0.8)
    kit.put("mug_full", (2.9, 3.4, 1.6), scale=1.4)
    kit.put("mug_full", (3.5, 3.8, 1.6), scale=1.4)
    kit.put("banner_patternB_yellow", (-4, D, 0.2), rot_z=180)
    torch(-0.0)
    torch(4.0)


def r_trading_post(rng):
    walls()
    kit.put("table_long_tablecloth", (0, 3.3, 0), rot_z=90)
    for x, n in ((-2.6, "coin_stack_large"), (-1.6, "coin_stack_small"), (1.4, "crate_cheese"), (2.6, "crate_ham")):
        kit.put(n, (x, 3.3, 1.75), scale=1.2)
    kit.put("chest_gold", (-4.3, 6.4, 0), rot_z=180)
    kit.put("crates_stacked", (4.0, 6.3, 0))
    kit.put("barrel_small_stack", (1.0, 6.8, 0))
    kit.put("banner_triple_yellow", (-1.8, D, 0.2), rot_z=180)
    kit.put("banner_triple_red", (2.2, D, 0.2), rot_z=180)
    torch(-4.0)
    torch(0.2)


def r_forge(rng):
    walls(back="wall_cracked")
    furnace(-3.4, 6.6, width=4.2, height=3.6)
    coal_pile(-0.8, 6.8, 0.8, rng, glow=True)
    anvil(1.5, 3.4)
    kit.put("barrel_large", (4.6, 6.4, 0), scale=0.75)
    kit.put("sword_2handed", (3.4, D - 0.3, 3.8), rot_z=180, scale=1.6)
    kit.put("axe_1handed", (4.6, D - 0.3, 3.8), rot_z=180, scale=1.6)
    kit.put("shield_square_color", (2.2, D - 0.3, 3.2), rot_z=180, scale=1.7)
    kit.put("table_small", (4.6, 2.6, 0))
    torch(3.6)


def r_infirmary(rng):
    walls(extra={1: "wall_shelves"})
    for x in (-4.0, 3.2):
        kit.put("bed_frame", (x, 5.7, 0))
        t.rbox((1.35, 2.7, 0.35), (x, 5.75, 1.05), t.mat((0.95, 0.94, 0.9), 0.8), bevel=0.15)
        t.rbox((1.0, 0.6, 0.3), (x, 6.7, 1.35), t.mat((1, 1, 0.98), 0.8), bevel=0.13)
    kit.put("table_medium", (-0.5, 3.0, 0))
    for i, b in enumerate(["bottle_A_green", "bottle_B_brown", "bottle_C_green", "jar_B_small"]):
        kit.put(b, (-1.2 + i * 0.45, 3.0, 2.05), scale=0.7)
    kit.put("banner_patternC_white", (-0.5, D, 0.2), rot_z=180)
    kit.put("candle_triple", (0.6, 3.2, 1.6), scale=1.4)
    t.light("POINT", (0.6, 3.0, 2.8), 120, color=(1.0, 0.7, 0.4), size=0.2)
    torch(-2.2)


def r_library(rng):
    walls(back="wall_shelves", extra={1: "wall_shelves"})
    kit.put("table_medium_decorated_A", (0, 3.6, 0))
    kit.put("spellbook_open", (-0.3, 3.6, 1.62), scale=1.6)
    kit.put("candle_triple", (0.8, 3.8, 1.62), scale=1.3)
    t.light("POINT", (0.8, 3.6, 2.7), 140, color=(1.0, 0.7, 0.4), size=0.2)
    for x in (-4.5, 4.5):
        kit.put("shelf_large", (x, D - 0.2, 0), rot_z=180)
    kit.put("chair", (-1.6, 3.0, 0), rot_z=60)
    kit.put("book_set", (4.0, 2.6, 0), scale=1.5)
    rug(0, 3.2, 4.4, 2.6)
    torch(-2.0)
    torch(2.0)


def r_training_hall(rng):
    walls()
    dummy(-3.6, 5.2)
    dummy(-0.8, 5.6)
    target(3.4, D - 0.4, 3.0)
    kit.put("sword_shield", (1.4, D - 0.3, 3.4), rot_z=180)
    rack = t.mat((0.4, 0.23, 0.11), 0.7)
    t.rbox((2.6, 0.4, 0.2), (4.0, 5.6, 2.3), rack, bevel=0.05)
    for dx in (-1.1, 1.1):
        t.rbox((0.2, 0.4, 2.4), (4.0 + dx, 5.6, 1.2), rack, bevel=0.05)
    for k, w in enumerate(["sword_1handed", "axe_1handed", "dagger"]):
        kit.put(w, (3.3 + k * 0.7, 5.4, 0.2), rot_z=180, scale=1.2)
    kit.put("barrel_small", (-4.8, 2.4, 0))
    kit.put("bucket_arrows", (1.6, 2.4, 0), scale=1.4)
    kit.put("shield_round", (-2.2, 2.4, 0.6), rot_z=30, scale=1.2)
    kit.put("banner_patternA_green", (-4.8, D, 0.2), rot_z=180)
    torch(0.8)


def dummy(x, y):
    sack = t.mat((0.78, 0.62, 0.38), 0.9)
    wood = t.mat((0.42, 0.24, 0.12), 0.7)
    t.rbox((1.3, 1.3, 0.25), (x, y, 0.12), wood, bevel=0.06)
    t.cyl(0.14, 3.0, (x, y, 1.5), wood)
    t.rbox((1.2, 0.8, 1.4), (x, y, 2.5), sack, bevel=0.35)
    t.ball(0.52, (x, y, 3.6), sack)
    t.rbox((2.4, 0.25, 0.25), (x, y, 2.9), wood, bevel=0.06)
    t.rbox((1.25, 0.85, 0.12), (x, y, 2.2), t.mat((0.55, 0.2, 0.15), 0.8), bevel=0.04)


def r_greenhouse(rng):
    walls(back="wall_arched")
    plant_bed(-3.6, 5.8, rng)
    plant_bed(1.0, 6.0, rng, kind="pumpkin")
    plant_bed(-1.4, 2.6, rng)
    kit.put("bucket_water", (4.2, 2.6, 0), scale=2.0)
    for x in (-3.0, 1.4):
        t.rbox((1.6, 0.6, 0.5), (x, 4.0, 5.6), t.mat((1, 0.85, 0.55), 0.3, emit=(1.0, 0.7, 0.35), strength=6), bevel=0.15)
        t.light("AREA", (x, 4.0, 5.2), 450, color=(1.0, 0.85, 0.6), size=1.5, rot=(0, 0, 0))


def r_watchtower(rng):
    walls()
    window_night(-1.8)
    kit.put("table_medium", (2.8, 3.6, 0))
    kit.put("crossbow_2handed", (2.6, 3.6, 1.62), rot_z=30, scale=1.6)
    kit.put("bucket_arrows", (-4.6, 2.6, 0), scale=2.0)
    kit.put("chair", (2.6, 2.2, 0), rot_z=180)
    kit.put("banner_thin_blue", (4.6, D, 0.2), rot_z=180)
    for leg in (-25, 25, 155):
        t.cyl(0.07, 2.6, (-3.4 + 0.45 * math.sin(math.radians(leg)), 5.0 + 0.45 * math.cos(math.radians(leg)), 1.25), t.mat((0.4, 0.23, 0.11), 0.7),
              rot=(math.radians(12) * math.cos(math.radians(leg)), -math.radians(12) * math.sin(math.radians(leg)), 0))
    t.cyl(0.24, 2.0, (-3.4, 4.6, 2.9), t.mat((0.62, 0.46, 0.2), 0.3, 0.8), rot=(math.radians(-70), 0, 0))
    kit.put("trunk_medium_A", (0.6, 6.6, 0), rot_z=180)
    kit.put("table_small_decorated_A", (-0.8, 2.6, 0))
    kit.put("lantern_standing", (4.8, 6.4, 0), scale=1.2)
    t.light("POINT", (4.8, 6.2, 2.2), 160, color=(1.0, 0.6, 0.25), size=0.2)
    torch(4.0)


def r_chapel(rng):
    walls(back="wall_arched")
    stone = t.mat((0.5, 0.47, 0.5), 0.8)
    t.rbox((3.2, 1.6, 1.4), (0, 6.6, 0.7), stone, bevel=0.2)
    t.rbox((3.6, 1.9, 0.3), (0, 6.6, 1.55), t.mat((0.62, 0.58, 0.6), 0.7), bevel=0.1)
    t.rbox((2.2, 0.15, 1.2), (0, 5.78, 0.7), t.mat((0.75, 0.62, 0.25), 0.35, 0.7), bevel=0.05)
    coal_pile(0, 6.6, 0.55, rng, glow=True)
    for dx in (-1.4, 1.4):
        kit.put("candle_triple", (dx, 6.6, 1.7), scale=1.0)
    t.ball(0.5, (0, 6.5, 2.3), t.mat((1, 0.5, 0.12), 0.3, emit=(1.0, 0.45, 0.1), strength=10), scale=(1, 1, 0.7))
    t.light("POINT", (0, 5.4, 2.8), 900, color=(1.0, 0.5, 0.15), size=0.5)
    for x in (-4.4, 4.4):
        kit.put("pillar_decorated", (x, 6.4, 0), scale=0.8)
        kit.put("candle_triple", (x * 0.6, 3.0, 0), scale=1.6)
    pew = t.mat((0.42, 0.24, 0.12), 0.7)
    for x in (-2.4, 2.4):
        t.rbox((3.0, 0.8, 0.25), (x, 2.6, 0.9), pew, bevel=0.06)
        t.rbox((3.0, 0.2, 0.9), (x, 3.0, 1.4), pew, bevel=0.06)
        for dx in (-1.3, 1.3):
            t.rbox((0.25, 0.7, 0.9), (x + dx, 2.6, 0.45), pew, bevel=0.05)
    kit.put("banner_patternC_yellow", (-2.6, D, 0.2), rot_z=180)
    kit.put("banner_patternC_yellow", (2.6, D, 0.2), rot_z=180)


def r_archive(rng):
    walls(back="wall_shelves", extra={1: "wall"})
    for x, n in ((-4.2, "trunk_large_B"), (-1.4, "trunk_medium_C"), (4.4, "trunk_large_C")):
        kit.put(n, (x, 6.4, 0), rot_z=180)
    kit.put("table_long_decorated_C", (1.2, 3.2, 0), rot_z=90)
    kit.put("keyring_hanging", (1.4, D - 0.2, 3.6), rot_z=180, scale=1.5)
    kit.put("spellbook_closed", (0.4, 3.0, 1.75), scale=1.5)
    kit.put("candle_thin_lit", (2.6, 3.4, 1.75), scale=1.5)
    t.light("POINT", (2.6, 3.2, 3.0), 120, color=(1.0, 0.7, 0.4), size=0.2)
    torch(-2.8)


def r_empty(rng):
    """A dug but empty cell: raw walls, rubble and scaffolding: build here."""
    walls(back="wall_cracked", extra={0: "wall_scaffold", 2: "wall_broken"})
    kit.put("rubble_large", (-3.0, 5.6, 0))
    kit.put("rubble_half", (3.6, 6.0, 0))
    kit.put("crate_long_empty", (1.0, 2.6, 0), scale=1.6)
    kit.put("lantern_standing", (-0.8, 2.8, 0), scale=1.6)
    t.light("POINT", (-0.8, 2.6, 2.4), 160, color=(1.0, 0.6, 0.25), size=0.2)


def flame(x, y, z, size, rng):
    """Cartoon fire: a crown of pointed tongues, orange outside, yellow inside, a hot core."""
    tongues = []
    for layer, (col, strength, shrink, dy) in enumerate((((1.0, 0.32, 0.04), 3.0, 1.0, 0.0), ((1.0, 0.58, 0.08), 4.0, 0.7, -0.12),
                                                          ((1.0, 0.86, 0.35), 5.0, 0.42, -0.24))):
        for k in range(5 - layer):
            dx = (k - (4 - layer) / 2) * 0.38 * size * shrink
            h = size * shrink * rng.uniform(0.75, 1.15) * (1.0 - abs(dx) / (1.6 * size))
            r = size * 0.28 * shrink
            bpy.ops.mesh.primitive_cone_add(vertices=16, radius1=r, radius2=0.0, depth=h, location=(x + dx, y + dy, z + h / 2))
            cone = bpy.context.active_object
            cone.rotation_euler = (rng.uniform(-0.12, 0.12), rng.uniform(-0.2, 0.2) - dx * 0.15, 0)
            mod = cone.modifiers.new("smooth", "SUBSURF")
            mod.levels = mod.render_levels = 2
            cone.data.materials.append(t.mat(col, 0.4, emit=col, strength=strength))
            bpy.ops.object.shade_smooth()
            tongues.append(cone)
    return tongues


def rug(x, y, w, d):
    t.rbox((w, d, 0.06), (x, y, 0.03), t.mat((0.8, 0.6, 0.2), 0.8), bevel=0.03)
    t.rbox((w - 0.4, d - 0.4, 0.08), (x, y, 0.04), t.mat((0.5, 0.08, 0.08), 0.9), bevel=0.03)
    t.rbox((w - 1.0, d - 1.0, 0.09), (x, y, 0.045), t.mat((0.62, 0.14, 0.1), 0.9), bevel=0.03)


def r_hearth(rng):
    """The Hearth of the Guardians: two cells wide, the great fireplace of stone blocks in the middle."""
    width = 2 * W
    walls(width=width, extra={0: "wall_arched", 5: "wall_arched"})
    fy = D - 0.9
    # the stone body of the fireplace: blocks in offset rows around an arched mouth
    for row in range(9):
        z = 0.3 + row * 0.6
        offset = 0.45 if row % 2 else 0.0
        x = -4.5 - offset
        while x < 4.5:
            bw = 0.9
            cx = x + bw / 2
            mouth = abs(cx) < 2.7 and z < 3.6 - 0.25 * max(0, abs(cx) - 1.2) ** 2
            if not mouth and abs(cx) < 4.6:
                c = t.jitter((0.3, 0.28, 0.31), rng, 0.04)
                t.rbox((0.86, 1.8, 0.56), (cx, fy, z), t.mat(c, 0.85), bevel=0.13)
            x += bw
    t.rbox((5.6, 1.4, 4.0), (0, fy + 0.5, 1.9), t.mat((0.08, 0.05, 0.04), 0.95), bevel=0.2)
    # mantel and the crest above it
    t.rbox((10.4, 2.4, 0.55), (0, fy - 0.2, 5.65), t.mat((0.38, 0.21, 0.1), 0.7), bevel=0.12)
    kit.put("sword_shield_gold", (0, fy - 0.9, 3.9 + 0.05), rot_z=180, scale=1.0)
    # logs, glowing coal and the fire
    for dx, rz in ((-0.9, 15), (0.9, -15), (0, 90)):
        t.cyl(0.32, 2.6, (dx * 0.6, fy - 0.6, 0.5), t.mat((0.35, 0.2, 0.1), 0.8), rot=(0, math.radians(90), math.radians(rz)))
    coal_pile(0, fy - 0.7, 1.3, rng, glow=True)
    if FIRE["on"]:
        flame(0.0, fy - 0.9, 0.55, 3.3, random.Random(FIRE["seed"]))
    t.light("POINT", (0, fy - 3.4, 2.4), 1100, color=(1.0, 0.5, 0.15), size=1.4)
    for x in (-7.0, 7.0):
        kit.put("pillar_decorated", (x, D - 0.9, 0), scale=0.75)
        kit.put("banner_patternA_red", (x * 1.4, D, 0.2), rot_z=180)
    kit.put("chest_gold", (-8.6, 3.0, 0), rot_z=20)
    kit.put("candle_triple", (8.6, 3.0, 0))
    kit.put("barrel_small_stack", (9.6, 6.6, 0))
    rug(0, 2.6, 7.0, 3.0)


RECIPES = {
    "Bedrooms": r_bedrooms, "Kitchen": r_kitchen, "CoalMine": r_coal_mine, "Barracks": r_barracks,
    "Warehouse": r_warehouse, "Mine": r_mine, "Tavern": r_tavern, "TradingPost": r_trading_post,
    "Forge": r_forge, "Infirmary": r_infirmary, "Library": r_library, "TrainingHall": r_training_hall,
    "Greenhouse": r_greenhouse, "Watchtower": r_watchtower, "Chapel": r_chapel, "Archive": r_archive,
    "Empty": r_empty, "Hearth": r_hearth,
}


def render_room(name, out, samples=64):
    kit.SCALE = PROP
    FIRE["on"] = name != "Hearth"
    t.reset(world=(0.05, 0.055, 0.08))
    bpy.context.scene.cycles.samples = samples
    rng = random.Random(name)
    width = 2 * W if name == "Hearth" else W
    RECIPES[name](rng)
    rock_frame(width, rng)
    camera(width)
    light_room(width=width, warm=0.6 if name == "Hearth" else 1.0)
    t.render(out)


def render_fire(out_dir, frames=8, samples=48):
    """The fire of the hearth alone, frame by frame, on a transparent background, cropped to the
    flames; the game draws it over the hearth at the returned place (shares of the hearth image)."""
    import os
    from PIL import Image
    paths = []
    for i in range(frames):
        kit.SCALE = PROP
        FIRE["on"], FIRE["seed"] = True, 1000 + i
        t.reset(world=(0.05, 0.055, 0.08))
        bpy.context.scene.cycles.samples = samples
        rng = random.Random("Hearth")
        before = set(bpy.data.objects)
        r_hearth(rng)
        rock_frame(2 * W, rng)
        camera(2 * W)
        light_room(width=2 * W, warm=0.6)
        for o in bpy.data.objects:
            if o.type == "MESH" and not o.name.startswith("Cone"):  # the tongues of fire are the only cones
                o.visible_camera = False
        path = os.path.join(out_dir, "frame_%d.png" % i)
        t.render(path, transparent=True)
        paths.append(path)
    imgs = [Image.open(p) for p in paths]
    box = None
    for im in imgs:
        b = im.split()[3].getbbox()
        if b:
            box = b if box is None else (min(box[0], b[0]), min(box[1], b[1]), max(box[2], b[2]), max(box[3], b[3]))
    W_, H_ = imgs[0].size
    fw, fh = box[2] - box[0], box[3] - box[1]
    sheet = Image.new("RGBA", (fw * frames, fh), (0, 0, 0, 0))
    for i, im in enumerate(imgs):
        sheet.paste(im.crop(box), (i * fw, 0))
    sheet.save(os.path.join(out_dir, "..", "HearthFire.png"))
    for p in paths:
        os.remove(p)
    os.rmdir(out_dir)
    return {"frames": frames, "x": box[0] / W_, "y": box[1] / H_, "w": fw / W_, "h": fh / H_}
