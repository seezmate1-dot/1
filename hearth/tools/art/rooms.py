"""The rooms of the cutaway, built from Quaternius' Medieval Village and Fantasy Props MegaKits (hand-
painted textures, one real-world scale) in Blender and rendered by Cycles: one picture per room type,
the hearth over two cells, an empty dug cell; the fire of the hearth is rendered apart (animated).

A room is 8 m wide, 4.4 m deep and 3.1 m high (four wall modules of 2 m, one storey), open to the
camera and framed by the rock of the mountain, so the cells tile into one cutaway.
Run: blenv/bin/python hearth/tools/art/render_all.py rooms
"""
import math
import random

import bpy
from mathutils import Vector

import qkit as q
import toy as t

W, D, H = 8.0, 4.4, 3.12
RES = (768, 384)
FIRE = {"on": True, "seed": 0}   # the hearth's fire is animated in the game: rendered apart
ROCK_TINT = (0.5, 0.55, 0.42)   # the mountain: the nature kit's rocks, darker and colder


# ------------------------------------------------------------------ the shell

def walls(back="Wall_UnevenBrick_Straight", width=W, extra=None, side="Wall_UnevenBrick_Straight",
          floor="Floor_WoodDark", sky=False):
    """Back wall of modules (extra: {index: other module}), side walls, floor, posts, beams, ceiling."""
    n = int(round(width / 2))
    for i in range(n):
        name = extra.get(i, back) if extra else back
        q.put(name, (-width / 2 + 1 + 2 * i, D, 0))
        if name == "Wall_Arch":
            t.rbox((1.9, 1.6, H), (-width / 2 + 1 + 2 * i, D + 0.85, H / 2), t.mat((0.03, 0.025, 0.02), 1.0), bevel=0)
        if "_Window_" in name:
            frame = "Window_" + name.split("_Window_")[1].replace("_Round", "_Round1").replace("_Flat", "_Flat1").replace("Flat2", "Flat1")
            try:
                q.put(frame, (-width / 2 + 1 + 2 * i, D, 0))
            except KeyError:
                pass
    for s in (-1, 1):
        for j in range(3):
            q.put(side, (s * width / 2, D - 1 - 2 * j, 0), rot_z=-90 * s)
    for i in range(n):
        for j in range(3):
            q.put(floor, (-width / 2 + 1 + 2 * i, D - 1 - 2 * j, 0))
    # wooden posts in the corners and a beam along the front, logs across the ceiling
    for x in (-width / 2 + 0.05, width / 2 - 0.37):
        q.put("Corner_Interior_Big", (x, D - 0.36, 0))
    for k in range(n // 2 + 1):
        x = -width / 2 + 0.3 + (width - 0.6) * k / (n // 2)
        t.rbox((0.32, D + 1.8, 0.3), (x, D / 2 - 0.7, H - 0.12), _wood(), bevel=0.04)
    t.rbox((width + 0.4, 0.4, 0.36), (0, -1.45, H - 0.15), _wood(), bevel=0.05)
    t.rbox((width + 2, D + 3, 0.3), (0, D / 2 - 1, H + 0.15), t.mat((0.16, 0.1, 0.06), 0.9), bevel=0)
    if sky:
        night_sky(width)


def _wood():
    return t.mat((0.36, 0.2, 0.1), 0.75)


def night_sky(width):
    """The night behind the windows of the back wall: deep blue with a moon."""
    t.rbox((width, 0.1, H + 1), (0, D + 1.2, H / 2), t.mat((0.04, 0.07, 0.18), 0.9, emit=(0.06, 0.1, 0.26), strength=0.8), bevel=0)
    rng = random.Random(7)
    star = t.mat((1, 1, 0.9), 0.5, emit=(1, 0.95, 0.8), strength=6)
    for _ in range(int(width * 6)):
        t.ball(rng.uniform(0.015, 0.035), (rng.uniform(-width / 2, width / 2), D + 1.1, rng.uniform(0.8, H + 0.4)), star)


def rock_frame(width, rng):
    """The cut through the mountain: big rocks of the nature kit around the opening."""
    hw = width / 2
    def rock(x, z, s):
        q.put("Rock_Medium_%d" % rng.randint(1, 3), (x, -2.2 + rng.uniform(-0.2, 0.2), z), rot_z=rng.uniform(0, 360),
              scale=(s * rng.uniform(0.9, 1.1), s, s * rng.uniform(0.85, 1.05)), tint=ROCK_TINT)
    x = -hw - 1.2
    while x < hw + 1.2:
        rock(x, H + 0.2, 0.5)
        rock(x + 0.5, -1.15, 0.5)
        x += rng.uniform(0.9, 1.2)
    z = -1.0
    while z < H + 1.0:
        for s in (-1, 1):
            rock(s * (hw + 0.85), z, 0.5)
        z += rng.uniform(0.8, 1.0)


def camera(width=W):
    sc = bpy.context.scene
    sc.render.resolution_x = int(RES[0] * width / W)
    sc.render.resolution_y = RES[1]
    data = bpy.data.cameras.new("cam")
    data.sensor_fit = "HORIZONTAL"
    dist = 30.0
    data.angle = 2 * math.atan((width + 0.5) / 2 / dist)
    cam = bpy.data.objects.new("cam", data)
    sc.collection.objects.link(cam)
    cam.location = (0, -dist, H / 2 + 3.4)
    target = Vector((0, 1.0, H / 2 - 0.2))
    cam.rotation_euler = (target - Vector(cam.location)).to_track_quat("-Z", "Y").to_euler()
    sc.camera = cam


def light_room(key=1.0, warm=1.0, width=W, cold=False):
    s = width / W
    t.light("AREA", (0, 0.2, H - 0.25), 420 * key * s, color=(0.68, 0.78, 1.0) if cold else (1.0, 0.93, 0.82), size=7 * s,
            rot=(math.radians(-15), 0, 0))
    t.light("AREA", (0, -11, H + 4), 110 * s, color=(0.58, 0.66, 0.9), size=14 * s, rot=(math.radians(62), 0, 0))
    t.light("AREA", (0, D * 0.55, H - 0.5), 260 * warm * s, color=(1.0, 0.66, 0.36), size=4 * s)


# ------------------------------------------------------------------ pieces

def torch(x, z=2.0):
    """A burning torch on the back wall that really lights the stones around it."""
    q.put("Torch_Metal", (x, D - 0.08, z))
    flame(x, D - 0.3, z + 0.3, 0.32, random.Random(int(x * 10)))
    t.light("POINT", (x, D - 0.6, z + 0.55), 70, color=(1.0, 0.55, 0.2), size=0.15)


def lamp(x, y, z=H):
    q.put("Chandelier", (x, y, z), scale=0.8)
    t.light("POINT", (x, y, z - 1.0), 160, color=(1.0, 0.65, 0.32), size=0.4)


def coal_pile(x, y, r=1.0, rng=None, glow=False):
    rng = rng or random.Random(int(x * 10 + y))
    for _ in range(int(14 * r)):
        px, py = x + rng.uniform(-r, r), y + rng.uniform(-r * 0.6, r * 0.6)
        size = rng.uniform(0.1, 0.18) * r ** 0.5
        hot = glow and rng.random() < 0.3
        m = t.mat((0.9, 0.3, 0.08), 0.4, emit=(1.0, 0.35, 0.05), strength=8) if hot else t.mat((0.07, 0.07, 0.08), 0.6)
        t.ball(size, (px, py, size * 0.6 + rng.uniform(0, 0.25) * r * (1 - abs(px - x) / r)), m, scale=(1, 1, 0.8))


def ore_vein(x, z, rng, color=(0.55, 0.58, 0.66), metal=0.85, n=10):
    """Shiny lumps of ore sticking out of the back wall."""
    for _ in range(n):
        t.ball(rng.uniform(0.08, 0.16), (x + rng.uniform(-0.7, 0.7), D - 0.12, z + rng.uniform(-0.45, 0.45)), t.mat(color, 0.25, metal),
               scale=(1, 0.6, 1))


def wagon(x, y, load=None, shiny=False, rot=0):
    """The village wagon, full of coal or ore."""
    s = 0.6
    q.put("Prop_Wagon", (x, y, 0), rot_z=rot, scale=s)
    if load:
        rng = random.Random(int(x * 13 + y))
        a = math.radians(rot)
        for _ in range(18):
            u, v = rng.uniform(-0.5, 0.5), rng.uniform(-0.75, 0.6)
            m = t.mat(load, 0.25 if shiny else 0.6, 0.85 if shiny else 0.0)
            t.ball(rng.uniform(0.1, 0.16), (x + (u * math.cos(a) - v * math.sin(a)) * s * 1.6, y + (u * math.sin(a) + v * math.cos(a)) * s * 1.6,
                                            s * 1.05 + rng.uniform(0, 0.12)), m, scale=(1, 1, 0.8))


def fireplace(x, y, width=1.6, height=1.8, fire=True, rng=None, blaze=1.0):
    """A stone fireplace: an arch of cut stone around a dark mouth, stone columns at its sides, glowing
    coal and fire inside, a chimney going up into the ceiling."""
    sx, sz = width / 1.6, height / 2.9
    soot = t.mat((0.02, 0.015, 0.012), 1.0)
    soot.node_tree.nodes["Principled BSDF"].inputs["Specular IOR Level"].default_value = 0.0
    t.rbox((width * 0.95, 0.9, height * 0.9), (x, y + 0.5, height * 0.45), soot, bevel=0.05)
    q.put("DoorFrame_Round_Brick", (x, y, 0), scale=(sx, 1.6, sz))
    for side in (-1, 1):
        # the column piece is cut aslant at its foot (it is made for roofs): stood on its head
        c = q.put("Prop_Chimney", (x + side * (width / 2 + 0.25 * sx), y + 0.25, H + 0.1), scale=(0.7 * sx, 0.9, (H + 0.1) / 2.2))
        c.rotation_euler = (0, math.pi, 0)
    coal_pile(x, y + 0.2, width * 0.3, random.Random(int(x * 7)), glow=True)
    if fire:
        flame(x, y + 0.15, 0.05, height * 0.32 * blaze, rng or random.Random(int(x * 7)))
    t.light("POINT", (x, y - 1.3, height * 0.3), 110 * width, color=(1.0, 0.45, 0.15), size=0.3 * width)


def target(x, y, z=1.7):
    for r, c in ((0.5, (0.95, 0.92, 0.85)), (0.38, (0.75, 0.15, 0.12)), (0.26, (0.95, 0.92, 0.85)), (0.14, (0.75, 0.15, 0.12))):
        t.cyl(r, 0.1 + (0.5 - r) * 0.08, (x, y, z), t.mat(c, 0.7), rot=(math.radians(90), 0, 0))


def plant_bed(x, y, rng, plants=("Plant_1", "Fern_1", "Plant_7", "Flower_3_Group", "Mushroom_Common")):
    t.rbox((2.2, 0.9, 0.5), (x, y, 0.25), _wood(), bevel=0.06)
    t.rbox((2.05, 0.75, 0.1), (x, y, 0.48), t.mat((0.22, 0.14, 0.08), 0.95), bevel=0.03)
    for k in range(4):
        name = rng.choice(plants)
        q.put(name, (x - 0.8 + k * 0.53, y + rng.uniform(-0.1, 0.1), 0.5), rot_z=rng.uniform(0, 360),
              scale=PLANT_SIZE.get(name, 0.7) * rng.uniform(0.85, 1.15))


PLANT_SIZE = {"Plant_1": 0.75, "Plant_1_Big": 0.5, "Fern_1": 0.55, "Plant_7": 0.8, "Plant_7_Big": 0.5, "Flower_3_Group": 0.4,
              "Flower_4_Group": 0.4, "Mushroom_Common": 1.4, "Mushroom_Laetiporus": 1.2, "Clover_1": 1.0, "Bush_Common_Flowers": 0.35}


def rug(x, y, w, d, color=(0.5, 0.08, 0.08)):
    t.rbox((w, d, 0.03), (x, y, 0.025), t.mat((0.8, 0.6, 0.2), 0.8), bevel=0.015)
    t.rbox((w - 0.2, d - 0.2, 0.035), (x, y, 0.03), t.mat(color, 0.9), bevel=0.015)
    t.rbox((w - 0.5, d - 0.5, 0.04), (x, y, 0.033), t.mat(tuple(min(1, c * 1.25) for c in color), 0.9), bevel=0.015)


def flame(x, y, z, size, rng):
    """Cartoon fire: a crown of tall pointed tongues, red-orange outside, yellow inside, a hot core."""
    tongues = []
    for layer, (col, strength, shrink, dy) in enumerate((((1.0, 0.25, 0.02), 1.6, 1.0, 0.0), ((1.0, 0.52, 0.04), 2.0, 0.72, -0.12),
                                                          ((1.0, 0.82, 0.3), 2.6, 0.45, -0.24))):
        for k in range(5 - layer):
            dx = (k - (4 - layer) / 2) * 0.26 * size * shrink
            h = size * shrink * rng.uniform(1.0, 1.45) * (1.0 - abs(dx) / (1.3 * size))
            r = size * 0.17 * shrink
            bpy.ops.mesh.primitive_cone_add(vertices=16, radius1=r, radius2=0.0, depth=h, location=(x + dx, y + dy * size, z + h / 2))
            cone = bpy.context.active_object
            cone.rotation_euler = (rng.uniform(-0.12, 0.12), rng.uniform(-0.2, 0.2) - dx / size * 0.2, 0)
            mod = cone.modifiers.new("smooth", "SUBSURF")
            mod.levels = mod.render_levels = 1
            cone.data.materials.append(t.mat(col, 0.4, emit=col, strength=strength))
            bpy.ops.object.shade_smooth()
            tongues.append(cone)
    return tongues


def banner(x, cloth=1, z=0.9, tint=None):
    q.put("Banner_%d_Cloth" % cloth if cloth else "Banner_1", (x, D - 0.12, z), tint=tint)


# ------------------------------------------------------------------ the rooms

def r_bedrooms(rng):
    walls(extra={1: "Wall_UnevenBrick_Window_Wide_Round"}, sky=True)
    for x in (-2.7, 0.0, 2.7):
        q.put("Bed_Twin%d" % (1 + int(x > 0)), (x, D - 1.3, 0))
    for x in (-3.5, 3.5):
        q.put("Chest_Wood", (x, 0.6, 0), rot_z=90 * (1 if x < 0 else -1), scale=0.8)
    q.put("CandleStick_Stand", (-1.35, D - 0.4, 0))
    q.put("BookGroup_Small_1", (1.35, D - 0.3, 0))
    rug(0, 0.9, 3.0, 1.2)
    torch(-3.4)
    torch(3.4)


def r_kitchen(rng):
    walls(floor="Floor_Brick")
    fireplace(2.4, D - 0.6, width=1.5, height=1.6)
    q.put("Cauldron", (2.4, D - 1.35, 0), scale=0.8)
    q.put("Table_Large", (-1.4, 1.6, 0))
    for i, (n, s) in enumerate([("Table_Plate", 1), ("Carrot", 1.4), ("Pot_1", 0.8), ("Mug", 1), ("Table_Knife", 1)]):
        q.put(n, (-2.4 + i * 0.5, 1.6 + rng.uniform(-0.2, 0.2), 0.81), rot_z=rng.uniform(0, 360), scale=s)
    q.put("Shelf_Small_Bottles", (-1.4, D - 0.25, 1.2))
    q.put("FarmCrate_Apple", (-3.3, D - 0.7, 0), rot_z=10)
    q.put("FarmCrate_Carrot", (-3.3, 0.6, 0), rot_z=-8)
    q.put("Barrel_Apples", (0.3, D - 0.6, 0))
    q.put("Bucket_Wooden_1", (3.4, 0.6, 0))
    torch(-2.6)


def r_coal_mine(rng):
    walls(extra={0: "Wall_Arch", 2: "Wall_Arch"}, floor="Floor_UnevenBrick")
    for x in (-2.0, 2.0):
        q.put("Prop_Support", (x, D - 0.2, 0), rot_z=0)
    coal_pile(-2.6, D - 1.0, 1.1, rng, glow=True)
    coal_pile(2.8, D - 1.1, 0.9, rng, glow=True)
    ore_vein(-1.0, 1.7, rng, color=(0.06, 0.06, 0.07), metal=0.0, n=12)
    ore_vein(3.0, 1.2, rng, color=(0.06, 0.06, 0.07), metal=0.0, n=8)
    wagon(0.2, 1.4, load=(0.08, 0.08, 0.09), rot=90)
    q.put("Pickaxe_Bronze", (-3.2, 0.6, 0.05), rot_z=30)
    q.put("Bucket_Metal", (3.2, 0.5, 0))
    q.put("Lantern_Wall", (-1.0, D - 0.1, 2.0))
    t.light("POINT", (-1.0, D - 0.6, 1.9), 90, color=(1.0, 0.6, 0.25), size=0.2)
    torch(3.4)


def r_barracks(rng):
    walls()
    q.put("Table_Large", (0, 1.4, 0))
    for x in (-0.8, 0.8):
        q.put("Stool", (x, 0.6, 0))
    q.put("Mug", (0.3, 1.4, 0.81))
    for x in (-2.8, 2.8):
        q.put("Bed_Twin1", (x, D - 1.3, 0))
    q.put("WeaponStand", (0, D - 0.5, 0))
    for x in (-1.3, 1.3):
        q.put("Shield_Wooden", (x, D - 0.15, 1.6))
    q.put("Chest_Wood", (-3.3, 0.7, 0), rot_z=90, scale=0.85)
    q.put("Sword_Bronze", (3.2, 0.7, 0.4), rot_z=40)
    banner(0, 1, z=1.9)
    torch(-1.9)
    torch(1.9)


def r_warehouse(rng):
    walls(floor="Floor_WoodLight")
    for x, y, n in ((-3.0, D - 0.8, "Crate_Wooden"), (-2.0, D - 0.8, "Crate_Metal"), (-2.5, D - 0.8, None)):
        if n:
            q.put(n, (x, y, 0), rot_z=rng.uniform(-8, 8))
    q.put("Crate_Wooden", (-2.5, D - 0.8, 0.95), rot_z=12, scale=0.9)
    q.put("Barrel_Holder", (1.0, D - 0.6, 0))
    q.put("Barrel", (2.8, D - 0.7, 0))
    q.put("Barrel", (3.4, D - 1.5, 0))
    q.put("Chest_Wood", (-0.8, 1.2, 0), rot_z=170)
    q.put("Coin_Pile_2", (0.5, 1.0, 0), scale=1.4)
    q.put("Coin_Pile", (1.2, 1.4, 0), scale=1.4)
    q.put("Bag", (-3.0, 1.0, 0))
    q.put("Pouch_Large", (2.6, 0.8, 0))
    q.put("Rope_1", (3.4, 0.4, 0))
    torch(-0.6)


def r_mine(rng):
    walls(extra={1: "Wall_Arch"}, floor="Floor_UnevenBrick")
    for x in (-3.0, 3.0):
        q.put("Prop_Support", (x, D - 0.2, 0))
    ore_vein(-3.0, 1.7, rng)
    ore_vein(2.4, 1.4, rng, color=(0.85, 0.86, 0.92), metal=1.0)
    wagon(0.0, 1.6, load=(0.6, 0.62, 0.7), shiny=True, rot=90)
    q.put("Crate_Metal", (-3.2, 0.8, 0), scale=0.9)
    q.put("Bucket_Metal", (3.1, 0.6, 0))
    q.put("Pickaxe_Bronze", (2.4, D - 0.4, 0.05), rot_z=200)
    for _ in range(4):
        q.put("Pebble_Round_%d" % rng.randint(1, 5), (rng.uniform(-2, 2), rng.uniform(0, 1), 0), scale=0.6, tint=(0.5, 0.6, 0.6))
    torch(0.0)


def r_tavern(rng):
    walls(extra={3: "Wall_UnevenBrick_Window_Wide_Round"}, sky=True)
    q.put("Table_Large", (-1.8, 1.4, 0))
    q.put("Bench", (-1.8, 0.6, 0))
    q.put("Table_Large", (2.0, 1.8, 0), rot_z=8)
    for x in (1.2, 2.8):
        q.put("Chair_1", (x, 1.0, 0), rot_z=rng.uniform(-15, 15))
    for x, y in ((-2.2, 1.5), (-1.3, 1.3), (2.2, 1.9)):
        q.put("Mug", (x, y, 0.81))
    q.put("Barrel_Holder", (-0.6, D - 0.6, 0))
    q.put("Barrel", (-3.2, D - 0.7, 0))
    q.put("Shelf_Small_Bottles", (1.6, D - 0.25, 1.3))
    lamp(0, 1.8)
    torch(-2.2)


def r_trading_post(rng):
    walls(floor="Floor_RedBrick")
    q.put("Stall_Empty", (-1.2, 2.2, 0), scale=0.85)
    for x, n in ((-2.0, "Coin_Pile_2"), (-1.2, "Pouch_Large"), (-0.4, "Coin_Pile")):
        q.put(n, (x, 2.0, 0.95), scale=1.0)
    q.put("Stall_Cart_Empty", (2.4, 2.4, 0), rot_z=-90, scale=0.85)
    q.put("FarmCrate_Apple", (2.4, 2.4, 0.7), scale=0.8)
    q.put("Chest_Wood", (-3.2, D - 0.8, 0))
    q.put("Crate_Wooden", (3.3, D - 0.7, 0))
    q.put("Bag", (0.8, D - 0.5, 0))
    banner(-1.0, 2, z=1.5)
    banner(1.6, 1, z=1.5)
    torch(-3.4)
    torch(3.4)


def r_forge(rng):
    walls(floor="Floor_UnevenBrick")
    fireplace(-2.2, D - 0.6, width=1.8, height=1.9)
    coal_pile(-0.5, D - 0.6, 0.5, rng)
    q.put("Anvil_Log", (0.8, 1.6, 0))
    q.put("Anvil", (0.8, 1.6, 0.55))
    q.put("Whetstone", (-2.8, 0.8, 0))
    q.put("WeaponStand", (2.8, D - 0.5, 0))
    q.put("Workbench", (2.6, 0.8, 0), rot_z=180)
    q.put("Sword_Bronze", (2.4, 0.8, 0.9), rot_z=80)
    q.put("Bucket_Metal", (-0.8, 0.6, 0))
    q.put("Chain_Coil", (1.6, D - 0.4, 0))
    torch(1.0)


def r_infirmary(rng):
    walls(back="Wall_Plaster_Straight", side="Wall_Plaster_Straight")
    for x in (-2.7, 2.7):
        q.put("Bed_Twin1", (x, D - 1.3, 0))
    q.put("Cabinet", (0, D - 0.35, 0))
    q.put("Table_Large", (0, 1.2, 0), scale=(0.7, 1, 1))
    for i, n in enumerate(["Potion_1", "Potion_2", "SmallBottles_1", "Potion_4", "Bottle_1"]):
        q.put(n, (-0.8 + i * 0.4, 1.2, 0.81))
    q.put("Bucket_Wooden_1", (-3.3, 0.6, 0))
    q.put("CandleStick_Stand", (3.3, 0.8, 0))
    t.light("POINT", (3.3, 0.8, 1.9), 60, color=(1.0, 0.7, 0.4), size=0.15)
    torch(-1.4)
    torch(1.4)


def r_library(rng):
    walls()
    for x in (-2.9, -1.4, 1.4, 2.9):
        q.put("Bookcase_2", (x, D - 0.3, 0))
    q.put("Table_Large", (0, 1.4, 0), scale=(0.8, 1, 1))
    q.put("Book_5", (-0.3, 1.4, 0.81), rot_z=20)
    q.put("Book_Stack_1", (0.6, 1.5, 0.81))
    q.put("CandleStick_Triple", (0.0, 1.6, 0.81))
    t.light("POINT", (0.0, 1.5, 1.4), 70, color=(1.0, 0.7, 0.4), size=0.15)
    q.put("Chair_1", (-0.8, 0.7, 0), rot_z=20)
    q.put("BookStand", (3.0, 0.9, 0), rot_z=200)
    q.put("Scroll_1", (-3.0, 0.6, 0), rot_z=40)
    rug(0, 1.3, 3.4, 1.6, color=(0.18, 0.22, 0.45))
    torch(0.0, z=2.3)


def r_training_hall(rng):
    walls(floor="Floor_WoodLight")
    q.put("Dummy", (-2.6, 2.2, 0), rot_z=10)
    q.put("Dummy", (-0.6, 2.6, 0), rot_z=-15)
    target(2.6, D - 0.2, 1.8)
    q.put("WeaponStand", (1.0, D - 0.5, 0))
    q.put("Peg_Rack", (-1.6, D - 0.1, 1.6))
    q.put("Shield_Wooden", (3.2, 1.0, 0.45), rot_z=200)
    q.put("Barrel", (-3.3, 0.6, 0))
    q.put("Axe_Bronze", (2.4, 0.6, 0.05), rot_z=70)
    banner(-3.2, 2, z=1.5)
    torch(0.0, z=2.3)


def r_greenhouse(rng):
    walls(back="Wall_Plaster_Straight", extra={1: "Wall_Plaster_Window_Wide_Round", 2: "Wall_Plaster_Window_Wide_Round"},
          side="Wall_Plaster_Straight", floor="Floor_Brick", sky=True)
    plant_bed(-2.4, D - 1.0, rng)
    plant_bed(1.8, D - 1.0, rng, plants=("Flower_4_Group", "Plant_1_Big", "Bush_Common_Flowers", "Fern_1"))
    plant_bed(-0.4, 1.0, rng, plants=("Mushroom_Common", "Mushroom_Laetiporus", "Clover_1", "Plant_7"))
    q.put("FarmCrate_Carrot", (3.2, 0.6, 0))
    q.put("Bucket_Wooden_1", (-3.3, 0.5, 0))
    for x in (-2.0, 2.0):
        t.rbox((1.0, 0.4, 0.3), (x, 2.0, H - 0.5), t.mat((1, 0.85, 0.55), 0.3, emit=(1.0, 0.7, 0.35), strength=6), bevel=0.1)
        t.light("AREA", (x, 2.0, H - 0.75), 180, color=(1.0, 0.85, 0.6), size=1.0)


def r_watchtower(rng):
    walls(extra={0: "Wall_UnevenBrick_Window_Wide_Round", 1: "Wall_UnevenBrick_Window_Wide_Round"}, sky=True)
    q.put("Table_Large", (2.0, 1.4, 0), scale=(0.7, 1, 1))
    q.put("Scroll_2", (1.8, 1.4, 0.81), rot_z=30)
    q.put("Key_Gold", (2.4, 1.3, 0.81))
    q.put("Chair_1", (2.0, 0.6, 0))
    q.put("Chest_Wood", (0.4, D - 0.6, 0), scale=0.85)
    q.put("WeaponStand", (3.2, D - 0.5, 0))
    # the spyglass on its tripod at the window
    for leg in (-25, 25, 155):
        t.cyl(0.03, 1.4, (-2.0 + 0.22 * math.sin(math.radians(leg)), 2.8 + 0.22 * math.cos(math.radians(leg)), 0.68), _wood(),
              rot=(math.radians(12) * math.cos(math.radians(leg)), -math.radians(12) * math.sin(math.radians(leg)), 0))
    t.cyl(0.11, 1.0, (-2.0, 3.0, 1.5), t.mat((0.62, 0.46, 0.2), 0.3, 0.8), rot=(math.radians(-70), 0, 0))
    q.put("Lantern_Wall", (3.6, D - 0.1, 2.0))
    t.light("POINT", (3.6, D - 0.6, 1.9), 90, color=(1.0, 0.6, 0.25), size=0.2)
    banner(1.4, 2, z=1.5)


def r_chapel(rng):
    walls(back="Wall_Plaster_Straight", extra={1: "Wall_Plaster_Window_Thin_Round", 2: "Wall_Plaster_Window_Thin_Round"},
          side="Wall_Plaster_Straight", floor="Floor_Brick", sky=True)
    stone = t.mat((0.55, 0.5, 0.48), 0.8)
    t.rbox((1.8, 0.9, 0.9), (0, D - 1.0, 0.45), stone, bevel=0.08)
    t.rbox((2.0, 1.0, 0.12), (0, D - 1.0, 0.95), t.mat((0.55, 0.12, 0.1), 0.8), bevel=0.04)
    q.put("Cauldron", (0, D - 1.0, 1.0), scale=0.55)
    coal_pile(0, D - 1.0, 0.3, rng, glow=True)
    flame(0, D - 1.0, 1.35, 0.4, rng)
    t.light("POINT", (0, D - 1.8, 1.6), 260, color=(1.0, 0.5, 0.15), size=0.3)
    q.put("Chalice", (0.7, D - 1.0, 1.01))
    for x in (-1.6, 1.6):
        q.put("CandleStick_Stand", (x, D - 0.9, 0))
        t.light("POINT", (x, D - 1.1, 1.9), 50, color=(1.0, 0.7, 0.4), size=0.12)
    for x in (-2.0, 2.0):
        q.put("Bench", (x, 1.0, 0))
    banner(-3.0, 1, z=1.4)
    banner(3.0, 1, z=1.4)


def r_archive(rng):
    walls()
    q.put("Bookcase_2", (-2.9, D - 0.3, 0))
    q.put("Cabinet", (-1.4, D - 0.35, 0))
    for x in (0.6, 1.8, 3.0):
        q.put("Chest_Wood", (x, D - 0.7, 0), rot_z=rng.uniform(-8, 8), scale=0.85)
    q.put("Table_Large", (0.4, 1.3, 0), scale=(0.8, 1, 1))
    q.put("Scroll_1", (0.0, 1.3, 0.81), rot_z=10)
    q.put("Scroll_2", (0.6, 1.4, 0.81), rot_z=60)
    q.put("Key_Metal", (1.0, 1.2, 0.81))
    q.put("CandleStick", (-0.4, 1.5, 0.81))
    t.light("POINT", (-0.4, 1.4, 1.4), 60, color=(1.0, 0.7, 0.4), size=0.12)
    q.put("Book_Stack_2", (-3.1, 0.7, 0))
    torch(-2.2, z=2.3)


def r_empty(rng):
    """A dug but empty cell: raw walls, rubble and scaffolding: build here."""
    walls(extra={0: "Wall_Arch", 3: "Wall_Arch"}, floor="Floor_UnevenBrick")
    for x in (-1.0, 1.0):
        q.put("Prop_Support", (x, D - 0.2, 0))
    for _ in range(7):
        q.put("Prop_Brick%d" % rng.randint(1, 4), (rng.uniform(-3, 3), rng.uniform(0.2, D - 1), 0), rot_z=rng.uniform(0, 360))
    q.put("Vase_Rubble_Medium", (-2.4, D - 0.8, 0))
    q.put("Crate_Wooden", (2.6, 1.0, 0), rot_z=20)
    q.put("Pickaxe_Bronze", (1.4, 0.6, 0.05), rot_z=-30)
    q.put("Lantern_Wall", (0, D - 0.1, 2.0))
    t.light("POINT", (0, D - 0.6, 1.9), 90, color=(1.0, 0.6, 0.25), size=0.2)


def r_hearth(rng):
    """The Hearth of the Guardians: two cells wide, the great fireplace of the kit's stone in the middle."""
    width = 2 * W
    walls(width=width, extra={0: "Wall_UnevenBrick_Window_Wide_Round", 7: "Wall_UnevenBrick_Window_Wide_Round"}, sky=True)
    fy = D - 0.7
    fireplace(0, fy, width=3.0, height=2.5, fire=FIRE["on"], rng=random.Random(FIRE["seed"]), blaze=1.7)
    t.rbox((4.6, 0.9, 0.24), (0, fy - 0.2, 2.62), _wood(), bevel=0.05)
    q.put("Shield_Wooden", (0, fy - 0.25, 2.66), scale=0.9)
    for dx, rz in ((-0.5, 15), (0.5, -15), (0, 90)):
        t.cyl(0.12, 1.2, (dx * 0.6, fy - 0.05, 0.14), t.mat((0.35, 0.2, 0.1), 0.8), rot=(0, math.radians(90), math.radians(rz)))
    t.light("POINT", (0, fy - 1.8, 1.2), 600, color=(1.0, 0.5, 0.15), size=0.8)
    for x in (-5.0, 5.0):
        banner(x, 1, z=1.2)
    q.put("Chest_Wood", (-6.4, 1.0, 0), rot_z=160)
    q.put("CandleStick_Stand", (6.4, 1.0, 0))
    q.put("Barrel", (7.0, D - 0.7, 0))
    q.put("Bench", (-3.6, 0.8, 0))
    q.put("Bench", (3.6, 0.8, 0))
    rug(0, 1.1, 4.0, 1.6)
    lamp(-5.0, 1.8)
    lamp(5.0, 1.8)


RECIPES = {
    "Bedrooms": r_bedrooms, "Kitchen": r_kitchen, "CoalMine": r_coal_mine, "Barracks": r_barracks,
    "Warehouse": r_warehouse, "Mine": r_mine, "Tavern": r_tavern, "TradingPost": r_trading_post,
    "Forge": r_forge, "Infirmary": r_infirmary, "Library": r_library, "TrainingHall": r_training_hall,
    "Greenhouse": r_greenhouse, "Watchtower": r_watchtower, "Chapel": r_chapel, "Archive": r_archive,
    "Empty": r_empty, "Hearth": r_hearth,
}


def render_room(name, out, samples=64):
    FIRE["on"] = name != "Hearth"
    t.reset(world=(0.03, 0.03, 0.04))
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
        FIRE["on"], FIRE["seed"] = True, 1000 + i
        t.reset(world=(0.05, 0.055, 0.08))
        bpy.context.scene.cycles.samples = samples
        rng = random.Random("Hearth")
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
