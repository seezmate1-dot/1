"""The world around the fortress: the mountain above the cutaway (rock and snow from photo textures,
firs, the Guardians' gatehouse from the Medieval Village kit), the battlefield (a meadow at a real
forest's edge, the Fog on the right), the bare rock of undug cells, the frost over frozen rooms, and
the geography of the kingdom's map (islands, places, acts, sea routes; kingdom.py renders it).
Real-world assets come from Poly Haven (phkit.py), the buildings from Quaternius' kits (qkit.py)."""
import math
import random

import bpy
from mathutils import Vector

import qkit as q
import rooms
import toy as t


class Building:
    """A group of kit pieces placed in metres around a local origin, then moved and scaled as one."""

    def __init__(self):
        self.root = bpy.data.objects.new("building", None)
        bpy.context.scene.collection.objects.link(self.root)

    def put(self, name, loc, rot_z=0.0, scale=1.0, tint=None, mul=None):
        if name.startswith("Rock_Medium_"):
            # real boulders (Poly Haven) instead of the kit's faceted rocks
            import phkit as ph
            s = scale[0] if isinstance(scale, tuple) else scale
            p = ph.put(("namaqualand_boulder_03", "namaqualand_boulder_05", "boulder_01")[int(name[-1]) % 3], loc, rot_z=rot_z, length=2.4 * s)
        else:
            p = q.put(name, loc, rot_z=rot_z, scale=scale, tint=tint, mul=mul)
        p.parent = self.root
        return p

    def place(self, loc, scale, rot_z=0.0):
        self.root.location = loc
        self.root.scale = (scale,) * 3
        self.root.rotation_euler = (0, 0, math.radians(rot_z))
        return self


SIDES = ((0, -1, 0), (1, 0, 90), (0, 1, 180), (-1, 0, -90))   # a square of one module per side: offset and turn


def tower(b, x, y, levels, roof_tint=None, door=False):
    """A square stone tower, one wall module per side, with the conical tiled roof."""
    for lvl in range(levels):
        for k, (dx, dy, rot) in enumerate(SIDES):
            name = "Wall_UnevenBrick_Straight"
            if lvl == 0 and k == 0 and door:
                name = "Wall_UnevenBrick_Door_Round"
            elif lvl == levels - 1 and k in (0, 1, 3):
                name = "Wall_UnevenBrick_Window_Thin_Round"
            b.put(name, (x + dx, y + dy, lvl * 3.1), rot_z=rot)
    b.put("Roof_Tower_RoundTiles", (x, y, levels * 3.1 + 0.3), scale=0.48, tint=roof_tint)


def house(b, x, y, width=2, roof_tint=None):
    """A small house: `width` modules wide, one deep, a door, a window and a tiled roof."""
    for i in range(width):
        b.put("Wall_Plaster_Door_Round" if i == 0 else "Wall_Plaster_Window_Wide_Round", (x - width + 1 + 2 * i, y - 1, 0))
        b.put("Wall_Plaster_Straight", (x - width + 1 + 2 * i, y + 1, 0), rot_z=180)
    for s in (-1, 1):
        b.put("Wall_Plaster_Straight", (x + s * width, y, 0), rot_z=90 * s)
    b.put("Roof_RoundTiles_4x4" if width == 2 else "Roof_RoundTiles_4x6", (x, y, 3.0), rot_z=90 if width > 2 else 0,
          scale=(1.0, 0.75, 0.7), tint=roof_tint)


# ------------------------------------------------------------------ the mountain above the fortress

SKY_DAY = "kloofendal_48d_partly_cloudy_puresky"


def _terrain(x0, x1, y0, y1, step, height, material, attrs=None):
    """A heightfield mesh over x0..x1, y0..y1: height(x, y) gives z; attrs: {name: f(x, y, z)} painted
    on its points for the material to mix by."""
    nx, ny = int((x1 - x0) / step), int((y1 - y0) / step)
    bpy.ops.mesh.primitive_grid_add(x_subdivisions=nx, y_subdivisions=ny, size=1, location=(0, 0, 0))
    g = bpy.context.active_object
    g.scale = (x1 - x0, y1 - y0, 1)
    g.location = ((x0 + x1) / 2, (y0 + y1) / 2, 0)
    bpy.ops.object.transform_apply(location=True, scale=True)
    me = g.data
    for v in me.vertices:
        v.co.z = height(v.co.x, v.co.y)
    me.update()
    for name, f in (attrs or {}).items():
        a = me.attributes.new(name, "FLOAT", "POINT")
        a.data.foreach_set("value", [f(v.co.x, v.co.y, v.co.z, v.normal) for v in me.vertices])
    for poly in me.polygons:
        poly.use_smooth = True
    me.materials.append(material)
    return g


def _layered(name, layers):
    """A material mixing photo textures by painted masks: layers = [(texture, size, tint, mask or None)],
    the first is the base."""
    import kingdom as k
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    b = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    co = nt.nodes.new("ShaderNodeTexCoord").outputs["Object"]
    col = rough = nor = None
    for tex, size, tint, mask in layers:
        c2, r2, n2 = k._layer(nt, tex, size, co, tint=tint)
        if r2 is None:
            v = nt.nodes.new("ShaderNodeValue")
            v.outputs[0].default_value = 0.8
            r2 = v.outputs[0]
        if col is None:
            col, rough, nor = c2, r2, n2
            continue
        a = nt.nodes.new("ShaderNodeAttribute")
        a.attribute_name = mask
        col = k._mix(nt, a.outputs["Fac"], col, c2)
        rough = k._mixf(nt, a.outputs["Fac"], rough, r2)
        nor = k._mix(nt, a.outputs["Fac"], nor, n2)
    nt.links.new(col, b.inputs["Base Color"])
    nt.links.new(rough, b.inputs["Roughness"])
    nm = nt.nodes.new("ShaderNodeNormalMap")
    nm.inputs["Strength"].default_value = 0.8
    nt.links.new(nor, nm.inputs["Color"])
    nt.links.new(nm.outputs["Normal"], b.inputs["Normal"])
    return m


def _smooth(a, b, x):
    x = max(0.0, min(1.0, (x - a) / (b - a)))
    return x * x * (3 - 2 * x)


def _persp_camera(width, centre, res, dist=120.0, tilt=0.0):
    """A long lens far away: nearly flat like an orthographic view, but the sky shows behind."""
    sc = bpy.context.scene
    sc.render.resolution_x, sc.render.resolution_y = res
    data = bpy.data.cameras.new("cam")
    data.sensor_fit = "HORIZONTAL"
    data.angle = 2 * math.atan(width / 2 / dist)
    data.clip_end = 2000
    cam = bpy.data.objects.new("cam", data)
    sc.collection.objects.link(cam)
    cam.location = (centre[0], -dist, centre[1])
    cam.rotation_euler = (math.radians(90 + tilt), 0, 0)
    sc.camera = cam
    return cam


SKY_CLEAR = "kloofendal_43d_clear_puresky"


def render_mountain(out, res=(1536, 768)):
    """The peak above the cutaway: a real mountain massif of rock and snow, firs on its lower slopes,
    the Guardians' gatehouse on a ledge, a partly cloudy day sky behind."""
    import phkit as ph
    from mathutils import noise
    t.reset(soft=True)
    sc = bpy.context.scene
    sc.cycles.samples = 128
    ph.sky(SKY_CLEAR, strength=1.3, rotation=340)
    t.light("SUN", (0, 0, 30), 3.0, color=(1.0, 0.94, 0.84), size=1.5, rot=(math.radians(50), 0, math.radians(-30)))
    rng = random.Random("mountain")
    # the game hides the lowest fifth of the picture under the first floor of the cutaway
    LIFT = 3.6
    width, height = 36.0, 18.0
    peaks = [(0, 30, 16.0, 11), (-14, 36, 12.0, 10), (14, 35, 13.0, 10), (-30, 44, 11.0, 12), (30, 42, 12.0, 12)]

    def h(x, y):
        ledge = LIFT + 1.2 + 0.05 * max(0.0, y) + 0.4 * noise.noise(Vector((x * 0.15, y * 0.15, 1)))
        # a range: broad swells shaped by ridged noise, so it rises in real ridges, gullies and crags
        env = sum(ph_ * math.exp(-(((x - px) / r) ** 2 + ((y - py) / (r * 1.3)) ** 2)) for px, py, ph_, r in peaks)
        rid = noise.ridged_multi_fractal(Vector((x * 0.035, y * 0.035, 0.5)), 1.0, 2.0, 6, 1.0, 2.0, noise_basis="PERLIN_ORIGINAL")
        return max(ledge, LIFT + env * (0.35 + 0.42 * rid) - 0.5)

    snow = lambda x, y, z, n: _smooth(LIFT + 8.5, LIFT + 10.5, z + 1.2 * noise.noise(Vector((x * 0.4, y * 0.4, 2)))) * _smooth(0.35, 0.6, n.z)  # noqa: E731
    rock = lambda x, y, z, n: _smooth(0.3, 0.6, 1 - n.z) * (1 - snow(x, y, z, n)) + _smooth(LIFT + 4, LIFT + 6, z) * 0.6  # noqa: E731
    mat = _layered("mountain", [("aerial_grass_rock", 5.0, (0.8, 1.05, 0.6), None), ("cliff_side", 4.0, (1.2, 1.05, 0.95), "rock"),
                                ("snow_02", 3.0, (1.0, 1.0, 1.05), "snow")])
    _terrain(-55, 55, -1, 70, 0.2, h, mat, {"rock": lambda x, y, z, n: min(1.0, rock(x, y, z, n)), "snow": snow})
    # the cut face in front: the same rock as around the rooms, down into the cutaway
    rooms.rock_mass(-width / 2 - 4, width / 2 + 4, -3, LIFT + 1.4, rng, y=-1.0, depth=0.35)
    # firs on the lower slopes
    for _ in range(140):
        x, y = rng.uniform(-24, 24), rng.uniform(1, 16)
        z = h(x, y)
        if z > LIFT + 6 or (abs(x) < 4.5 and y < 6):
            continue
        ph.put("fir_sapling", (x, y, z - 0.1), rot_z=rng.uniform(0, 360), height=rng.uniform(2.4, 3.8), length=3.0)
    # the gatehouse of the Guardians on the ledge: a keep between two towers, banners, torches
    b = Building()
    house(b, 0, 0, width=2)
    for x in (-3.0, 3.0):
        tower(b, x, 0.2, 2, door=False)
    b.place((0, 1.5, h(0, 1.5) - 0.1), 0.75)
    for x in (-2.25, 2.25):
        t.rbox((0.05, 0.05, 2.0), (x, 1.2, h(0, 1.5) + 5.9), t.mat((0.2, 0.2, 0.22), 0.4, 0.8), bevel=0)
        t.rbox((0.9, 0.03, 0.55), (x + 0.47, 1.2, h(0, 1.5) + 6.6), t.mat((0.6, 0.06, 0.05), 0.8), bevel=0.02)
    for x in (-1.0, 1.0):
        t.light("POINT", (x, 0.0, h(0, 1.5) + 1.2), 60, color=(1.0, 0.6, 0.3), size=0.3)
    _persp_camera(width, (0, height / 2 + 0.6), res, tilt=0.0)
    t.render(out)


def render_battlefield(out, res=(1536, 768)):
    """Where the fights happen: a meadow at the edge of a real forest, a dirt path, boulders, the
    trees dying and the purple Fog creeping in on the right. The heroes stand on the left of the
    path, the creatures come from the right (0.88 of the picture high: GameRunner.DrawBattleStage)."""
    import kingdom as k
    import phkit as ph
    from mathutils import noise
    t.reset(soft=True)
    sc = bpy.context.scene
    sc.cycles.samples = 128
    ph.sky(SKY_DAY, strength=1.0, rotation=200)
    t.light("SUN", (0, 0, 30), 3.2, color=(1.0, 0.94, 0.84), size=1.5, rot=(math.radians(50), 0, math.radians(-40)))
    rng = random.Random("battlefield")

    def h(x, y):
        return 0.6 * noise.noise(Vector((x * 0.06, y * 0.06, 3))) * _smooth(4, 20, y)
    path = lambda x, y, z, n: 1 - _smooth(1.1, 2.2, abs(y + 10.8 + 0.4 * math.sin(x * 0.2)))  # noqa: E731
    dead = lambda x, y, z, n: _smooth(4, 14, x + 3 * noise.noise(Vector((x * 0.1, y * 0.1, 5))))  # noqa: E731
    mat = _layered("meadow", [("aerial_grass_rock", 5.0, (0.8, 1.15, 0.55), None), ("brown_mud_leaves_01", 3.0, (0.8, 0.75, 0.75), "dead"),
                              ("brown_mud_dry", 2.5, (1.0, 0.92, 0.82), "path")])
    _terrain(-60, 60, -20, 80, 0.5, h, mat, {"path": path, "dead": dead})
    # the forest behind; on the right it dies into the Fog
    for _ in range(170):
        x, y = rng.uniform(-40, 40), rng.uniform(8, 45)
        if x > 8 and rng.random() < 0.75:
            ph.put(rng.choice(["quiver_tree_01", "dead_quiver_trunk", "fir_sapling"]), (x, y, h(x, y) - 0.1), rot_z=rng.uniform(0, 360),
                   height=rng.uniform(4, 7), length=6)
        else:
            name = rng.choice(["island_tree_01", "island_tree_02", "fir_sapling", "fir_sapling"])
            ph.put(name, (x, y, h(x, y) - 0.1), rot_z=rng.uniform(0, 360), height=rng.uniform(5, 8), length=7)
    for _ in range(16):
        x = rng.choice([rng.uniform(-20, -8), rng.uniform(8, 20)])
        y = rng.uniform(-2, 6)
        ph.put(rng.choice(["namaqualand_boulder_03", "namaqualand_boulder_05", "boulder_01", "rock_moss_set_01"]), (x, y, h(x, y) - 0.2),
               rot_z=rng.uniform(0, 360), length=rng.uniform(1.2, 2.6))
    for _ in range(90):
        x, y = rng.uniform(-22, 22), rng.uniform(-8, 6)
        if abs(y + 10.8) < 2.2:
            continue
        ph.put(rng.choice(["fern_02", "shrub_04", "shrub_03", "nettle_plant", "periwinkle_plant"]), (x, y, h(x, y)), rot_z=rng.uniform(0, 360),
               height=rng.uniform(0.35, 0.7), length=1.2)
    # the Fog: soft purple veils on the right, one behind another
    fog = k._cloud_material((0.42, 0.34, 0.55), density=0.8, scale=0.6)
    for kx in range(6):
        o = k._cloud(14 + kx * 3.5, 4 + kx * 4, 1.6 + kx * 0.3, 14, 5, fog)
        o.rotation_euler = (math.radians(90), 0, 0)
    sc.render.resolution_x, sc.render.resolution_y = res
    data = bpy.data.cameras.new("cam")
    data.sensor_fit = "HORIZONTAL"
    data.lens = 30
    data.clip_end = 2000
    cam = bpy.data.objects.new("cam", data)
    sc.collection.objects.link(cam)
    cam.location = (0, -22, 4.2)
    cam.rotation_euler = (math.radians(82), 0, 0)
    sc.camera = cam
    t.render(out)


# ------------------------------------------------------------------ cells: rock and frost

def render_rock(out):
    """A cell not dug yet: the raw rock of the mountain, framed like a room so the cells tile."""
    t.reset(world=(0.02, 0.02, 0.03), soft=True)
    bpy.context.scene.cycles.samples = 48
    rng = random.Random("rock")
    rooms.rock_mass(-rooms.W / 2 - 3, rooms.W / 2 + 3, -3, rooms.H + 3, rng)
    rooms.camera(rooms.W)
    rooms.light_room(key=0.3, warm=0.0)
    t.light("AREA", (0, -9, rooms.H + 3), 260, color=(0.7, 0.75, 0.95), size=12, rot=(math.radians(55), 0, 0))
    t.render(out)


def render_frost(out):
    """Ice over a frozen room: icicles along the top, rime up the sides (transparent, room camera)."""
    t.reset(world=(0, 0, 0))
    bpy.context.scene.cycles.samples = 48
    rng = random.Random("frost")
    ice = t.mat((0.75, 0.9, 1.0), 0.15)
    hw = rooms.W / 2
    x = -hw - 0.2
    while x < hw + 0.2:
        h = rng.uniform(0.25, 0.75)
        bpy.ops.mesh.primitive_cone_add(vertices=10, radius1=rng.uniform(0.06, 0.13), radius2=0, depth=h,
                                        location=(x, -1.5, rooms.H - 0.3 - h / 2), rotation=(math.radians(180), 0, 0))
        bpy.context.active_object.data.materials.append(ice)
        bpy.ops.object.shade_smooth()
        x += rng.uniform(0.15, 0.35)
    t.rbox((rooms.W + 0.6, 0.45, 0.25), (0, -1.5, rooms.H - 0.2), t.mat((0.9, 0.96, 1.0), 0.4), bevel=0.1)
    for cx in (-hw, hw):
        for _ in range(30):
            t.ball(rng.uniform(0.06, 0.18), (cx * rng.uniform(0.92, 1.0), -1.2, rng.uniform(0, rooms.H - 0.2)),
                   t.mat((0.85, 0.95, 1.0), 0.3), scale=(1, 0.3, 1))
    rooms.camera(rooms.W)
    t.light("AREA", (0, -8, 6), 600, color=(0.8, 0.9, 1.0), size=10, rot=(math.radians(50), 0, 0))
    t.render(out, transparent=True)


# ------------------------------------------------------------------ the campaign map

def _village(b, rng):
    house(b, -2.5, 0.5, roof_tint=(0.5, 1.0, 1.0))
    house(b, 2.5, -0.5, roof_tint=(0.53, 1.0, 0.9))
    b.put("Prop_Wagon", (0, -2.5, 0), rot_z=60)


def _mines(b, rng):
    for x in (-1.2, 1.2):
        b.put("Prop_Support", (x, 0, 0), rot_z=90)
    for k in range(5):
        b.put("Rock_Medium_%d" % (k % 3 + 1), (rng.uniform(-3, 3), rng.uniform(0.5, 2.5), 0), rot_z=rng.uniform(0, 360), scale=0.9,
              tint=rooms.ROCK_TINT, mul=rooms.ROCK_MUL)
    b.put("Prop_Wagon", (0.5, -2.2, 0), rot_z=90)


def _forest(b, rng):
    for _ in range(9):
        b.put("CommonTree_%d" % rng.randint(1, 5), (rng.uniform(-4, 4), rng.uniform(-4, 4), 0), rot_z=rng.uniform(0, 360), scale=0.8)


def _monastery(b, rng):
    for k, (dx, dy, rot) in enumerate(SIDES):
        b.put("Wall_Arch" if k % 2 else "Wall_UnevenBrick_Window_Thin_Round", (dx * 2, dy * 2, 0), rot_z=rot, scale=(2, 1, 1))
    for _ in range(8):
        b.put("Prop_Brick%d" % rng.randint(1, 4), (rng.uniform(-3, 3), rng.uniform(-3, 3), 0), rot_z=rng.uniform(0, 360), scale=2.5)


def _mill(b, rng):
    house(b, 0, 0, width=3, roof_tint=(0.47, 1.0, 1.0))
    for k in range(4):
        a = k * math.pi / 2 + 0.4
        t.rbox((0.5, 0.15, 4.5), (0, 0, 0), t.mat((0.75, 0.68, 0.55), 0.8), bevel=0.05)
        blade = bpy.context.active_object
        blade.parent = b.root
        blade.location = (2.0 * math.sin(a), -2.2, 5.0 + 2.0 * math.cos(a))
        blade.rotation_euler = (0, a, 0)


def _outpost(b, rng):
    for x in (-3.0, 3.0):
        tower(b, x, 0, 2, roof_tint=(0.08, 1.2, 0.8))
    b.put("Wall_UnevenBrick_Door_Round", (-1, -0.3, 0))
    b.put("Wall_UnevenBrick_Straight", (1, -0.3, 0))
    for x in (-3.0, 3.0):
        b.put("Banner_2_Cloth", (x, -1.35, 3.5))


def _dead_grove(b, rng):
    for _ in range(7):
        b.put("DeadTree_%d" % rng.randint(1, 5), (rng.uniform(-4, 4), rng.uniform(-4, 4), 0), rot_z=rng.uniform(0, 360), scale=0.7)


def _hermit(b, rng):
    tower(b, 0, 0, 3, roof_tint=(0.08, 1.2, 0.8), door=True)
    for _ in range(3):
        b.put("Pine_%d" % rng.randint(1, 5), (rng.uniform(-4, 4), rng.uniform(1.5, 4), 0), scale=0.7)


def _barrows(b, rng):
    for k in range(4):
        b.put("Rock_Medium_%d" % (k % 3 + 1), (rng.uniform(-3, 3), rng.uniform(-3, 3), -0.4), rot_z=rng.uniform(0, 360), scale=1.3,
              tint=(0.5, 0.5, 0.75))
    for _ in range(4):
        b.put("Pebble_Square_%d" % rng.randint(1, 6), (rng.uniform(-3, 3), rng.uniform(-3, 3), 0), scale=2.5)


def _fog_heart(b, rng):
    glow = t.mat((0.45, 0.15, 0.7), 0.2, emit=(0.6, 0.2, 1.0), strength=3)
    for k in range(7):
        h = rng.uniform(3, 7)
        bpy.ops.mesh.primitive_cone_add(vertices=6, radius1=rng.uniform(0.5, 0.9), radius2=0.05, depth=h,
                                        location=(rng.uniform(-1.6, 1.6), rng.uniform(-1.6, 1.6), h / 2),
                                        rotation=(rng.uniform(-0.3, 0.3), rng.uniform(-0.3, 0.3), 0))
        c = bpy.context.active_object
        c.data.materials.append(glow)
        c.parent = b.root
    for _ in range(4):
        b.put("DeadTree_%d" % rng.randint(1, 5), (rng.uniform(-5, 5), rng.uniform(-5, 5), 0), scale=0.6)


# the ten acts: position on the island (x, y) and what stands there
ACTS = [
    ((2.0, -8.6), _village, "Деревня у подножия"),
    ((-2.0, -6.9), _mines, "Старые копи"),
    ((-7.0, -5.2), _forest, "Шепчущий лес"),
    ((-3.0, -3.4), _monastery, "Разрушенный монастырь"),
    ((3.0, -3.4), _mill, "Мельница на реке"),
    ((7.0, -1.7), _outpost, "Застава Двора"),
    ((3.0, 1.7), _dead_grove, "Мёртвая роща"),
    ((-3.0, 1.7), _hermit, "Башня отшельника"),
    ((-5.0, 5.2), _barrows, "Курганы"),
    ((1.0, 6.9), _fog_heart, "Сердце Тумана"),
]



# ------------------------------------------------------------------ the world map (an archipelago, like Hustle Castle's)

def _arena(b, rng):
    """A round arena: a ring of stone walls facing out, banners over the gate."""
    n = 12
    for k in range(n):
        a = k / n * math.tau
        name = "Wall_UnevenBrick_Door_Round" if k == 9 else "Wall_UnevenBrick_Straight"
        for turn in (0, 180):
            b.put(name, (3.8 * math.sin(a), -3.8 * math.cos(a), 0), rot_z=-math.degrees(a) + turn)
    for k in range(0, n, 3):
        a = (k + 0.5) / n * math.tau
        tower(b, 4.2 * math.sin(a), -4.2 * math.cos(a), 1, roof_tint=(0.02, 1.2, 0.9))
    for k in (8, 10):
        a = k / n * math.tau
        b.put("Banner_1_Cloth", (4.0 * math.sin(a), -4.0 * math.cos(a), 1.6), rot_z=-math.degrees(a))
    t.rbox((0.1, 0.1, 0.1), (0, 0, 0), t.mat((0.8, 0.7, 0.5), 0.9), bevel=0)


def _lair(b, rng):
    """The monsters' lair: a heap of big rocks around a dark cave mouth, dead trees and bones."""
    for k in range(7):
        a = k / 7 * math.pi + math.pi
        b.put("Rock_Medium_%d" % (k % 3 + 1), (3.0 * math.cos(a), 1.5 + 1.6 * math.sin(a) + 1.8, 0), rot_z=rng.uniform(0, 360),
              scale=rng.uniform(1.2, 1.7), tint=(0.5, 0.4, 0.8), mul=(0.7, 0.62, 0.6))
    for _ in range(5):
        b.put("DeadTree_%d" % rng.randint(1, 5), (rng.uniform(-5, 5), rng.uniform(1, 5), 0), rot_z=rng.uniform(0, 360), scale=0.7)
    for _ in range(6):
        b.put("Pebble_Round_%d" % rng.randint(1, 5), (rng.uniform(-2, 2), rng.uniform(-2.5, -0.5), 0), scale=2.0, mul=(1.0, 0.95, 0.85))


def _camp(b, rng):
    """The camp to raid: wagons, crates, barrels and a fence, red banners."""
    b.put("Prop_Wagon", (-2.0, 0.5, 0), rot_z=30)
    b.put("Stall_Cart_Empty", (2.0, 1.0, 0), rot_z=-20)
    for k in range(4):
        b.put("Crate_Wooden", (rng.uniform(-1, 1), rng.uniform(-1.5, 0), 0), rot_z=rng.uniform(0, 90))
        b.put("Barrel", (rng.uniform(-3, 3), rng.uniform(-2.5, 2.5), 0))
    for k in range(10):
        a = k / 10 * math.tau
        b.put("Prop_WoodenFence_Single", (4.5 * math.cos(a), 3.5 * math.sin(a), 0), rot_z=math.degrees(a) + 90)
    for x in (-3.5, 3.5):
        t_ = t.cyl(0.08, 4.0, (0, 0, 0), t.mat((0.3, 0.18, 0.08), 0.7))
        t_.parent = b.root
        t_.location = (x, 2.5, 2.0)
        b.put("Banner_1_Cloth", (x, 2.4, 2.2))


def _crossroads(b, rng):
    """The crossroads the routes start from: a signpost, a loaded wagon, bags."""
    wood = t.mat((0.4, 0.24, 0.12), 0.7)
    post = t.cyl(0.12, 3.2, (0, 0, 0), wood)
    post.parent = b.root
    post.location = (0, 0, 1.6)
    for k, (z, rot) in enumerate(((2.7, 20), (2.2, -35), (1.7, 60))):
        arrow = t.rbox((1.6, 0.12, 0.38), (0, 0, 0), t.mat((0.62, 0.42, 0.22), 0.7), bevel=0.04)
        arrow.parent = b.root
        arrow.location = (0.7 * math.cos(math.radians(rot)), 0.7 * math.sin(math.radians(rot)), z)
        arrow.rotation_euler = (0, 0, math.radians(rot))
    b.put("Prop_Wagon", (2.6, -0.5, 0), rot_z=80)
    for k in range(3):
        b.put("Bag", (-1.5 + k * 0.8, -1.2, 0), rot_z=rng.uniform(0, 360), scale=1.3)


def _portal(b, rng):
    """The portal of the events: standing stones in a ring around a glowing violet gate."""
    for k in range(8):
        a = k / 8 * math.tau
        b.put("Rock_Medium_%d" % (k % 3 + 1), (3.0 * math.cos(a), 3.0 * math.sin(a), 0), rot_z=rng.uniform(0, 360),
              scale=(0.45, 0.45, 1.5), tint=(0.5, 0.3, 0.9), mul=(0.75, 0.75, 0.85))
    glow = t.mat((0.6, 0.25, 1.0), 0.2, emit=(0.7, 0.3, 1.0), strength=6)
    bpy.ops.mesh.primitive_torus_add(major_radius=1.6, minor_radius=0.25, location=(0, 0, 2.0), rotation=(math.radians(90), 0, 0))
    ring = bpy.context.active_object
    ring.data.materials.append(glow)
    ring.parent = b.root
    disc = t.cyl(1.4, 0.05, (0, 0, 0), t.mat((0.5, 0.3, 1.0), 0.2, emit=(0.55, 0.35, 1.0), strength=2.5), rot=(math.radians(90), 0, 0))
    disc.parent = b.root
    disc.location = (0, 0, 2.0)


def _home(b, rng):
    """The player's fortress: the gatehouse on its rock, as above the cutaway."""
    house(b, 0, 0, width=2, roof_tint=(0.5, 1.2, 0.8))
    for x in (-3.0, 3.0):
        tower(b, x, 0.2, 2, roof_tint=(0.5, 1.2, 0.8))
    for x in (-1.65, 1.65):
        t_ = t.cyl(0.05, 2.0, (0, 0, 0), t.mat((0.2, 0.2, 0.22), 0.4, 0.8))
        t_.parent = b.root
        t_.location = (x * 1.8, 0.2, 7.2)


# the islands of the archipelago: centre, half-sizes, height, and what grows on them ("dark" for the
# hunters' woods and the Fog's island)
ISLANDS = {
    "home": ((-11.0, -3.6), (5.4, 3.8), 1.4, "green"),
    "act1": ((-2.0, 0.4), (4.6, 2.9), 1.2, "green"),
    "act2": ((5.6, 3.6), (4.0, 2.5), 1.2, "green"),
    "act3": ((-0.4, 7.6), (3.8, 2.0), 1.1, "dark"),
    "arena": ((10.6, -4.6), (3.3, 2.3), 1.0, "green"),
    "hunt": ((-12.6, 5.6), (3.6, 2.6), 1.2, "dark"),
    "raids": ((13.6, 2.4), (2.9, 2.1), 1.0, "green"),
    "portal": ((9.6, 8.6), (2.7, 1.7), 1.0, "dark"),
}
PLACES = {
    "Fortress": ((-12.4, -3.4), _home, 0.55),
    "Arena": ((10.6, -4.5), _arena, 0.42),
    "Hunt": ((-12.6, 5.8), _lair, 0.42),
    "Raids": ((13.6, 2.6), _camp, 0.4),
    "Routes": ((-6.4, -6.2), _crossroads, 0.45),
    "Events": ((9.6, 8.7), _portal, 0.42),
}
# the ten acts of the campaign across its three islands, from the bridge to the Heart of the Fog
WORLD_ACTS = [(-5.0, -0.6), (-3.0, 1.6), (-0.6, -0.8), (1.2, 1.4), (3.6, 2.6), (6.0, 4.8), (8.0, 2.8), (-2.8, 7.2), (-0.4, 8.4), (1.8, 7.4)]
SEA_ROUTES = [("home", "act1"), ("act1", "act2"), ("act1", "act3"), ("home", "arena"), ("act2", "raids"), ("home", "hunt"), ("act2", "portal"),
              ("act2", "arena")]


def _wheight(x, y, noise):
    """The ground: every island a dome with gentle hills; below 0 is the sea."""
    best = -1.0
    for (cx, cy), (rx, ry), h, _ in ISLANDS.values():
        d = math.sqrt(((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2)
        wobble = 0.08 * math.sin(3 * math.atan2(y - cy, x - cx) + cx) + 0.05 * math.sin(7 * math.atan2(y - cy, x - cx) + cy)
        base = 1.0 - (d * (1 - wobble)) ** 2.4
        hills = 0.35 * math.sin(x * 0.7 + noise[0]) * math.cos(y * 0.8 + noise[1])
        best = max(best, base * h + hills * max(0.0, base) ** 0.5 - 0.12)
    return best


def _island_of(x, y):
    for key, ((cx, cy), (rx, ry), _, kind) in ISLANDS.items():
        if ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 < 1.0:
            return key, kind
    return None, None


def _sea_route(a, b, rng):
    """A dotted route across the sea between two islands, like the paths on a game map."""
    (ax, ay), (bx, by) = ISLANDS[a][0], ISLANDS[b][0]
    dot = t.mat((1.0, 0.97, 0.88), 0.6, emit=(1.0, 0.95, 0.85), strength=0.3)
    n = int(math.hypot(bx - ax, by - ay) / 0.45)
    bend = rng.uniform(-1.2, 1.2)
    for k in range(1, n):
        f = k / n
        # a gentle curve: offset sideways along a sine
        px = ax + (bx - ax) * f - (by - ay) / max(1e-6, math.hypot(bx - ax, by - ay)) * bend * math.sin(f * math.pi)
        py = ay + (by - ay) * f + (bx - ax) / max(1e-6, math.hypot(bx - ax, by - ay)) * bend * math.sin(f * math.pi)
        if _wheight(px, py, (0, 0, 0)) < -0.05 and k % 2 == 0:
            t.cyl(0.09, 0.03, (px, py, 0.03), dot, verts=10)
    return (ax + bx) / 2, (ay + by) / 2
