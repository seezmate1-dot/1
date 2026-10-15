"""The world around the fortress, in the style of the rooms (Quaternius' Medieval Village, Fantasy
Props and Stylized Nature MegaKits): the mountain peak with the Guardians' gatehouse above the
cutaway, the bare rock of undug cells, the frost over frozen rooms and the campaign map (an island
with forests, rocks and ten places, one per act)."""
import math
import random

import bpy
from bpy_extras.object_utils import world_to_camera_view
from mathutils import Vector

import qkit as q
import rooms
import toy as t


def _ortho_camera(width, height, centre, res, tilt=0.0):
    sc = bpy.context.scene
    sc.render.resolution_x, sc.render.resolution_y = res
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    sc.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = max(width, height * res[0] / res[1])
    cam.location = (centre[0], -40, centre[1] + 40 * math.tan(math.radians(tilt)))
    cam.rotation_euler = (math.radians(90 - tilt), 0, 0)
    sc.camera = cam
    return cam


class Building:
    """A group of kit pieces placed in metres around a local origin, then moved and scaled as one."""

    def __init__(self):
        self.root = bpy.data.objects.new("building", None)
        bpy.context.scene.collection.objects.link(self.root)

    def put(self, name, loc, rot_z=0.0, scale=1.0, tint=None, mul=None):
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


def pine(x, y, z, scale, rng, kind="Pine"):
    q.put("%s_%d" % (kind, rng.randint(1, 5)), (x, y, z), rot_z=rng.uniform(0, 360), scale=scale * rng.uniform(0.85, 1.15))


# ------------------------------------------------------------------ the mountain above the fortress

def sky(width, height, centre_y, rng, clouds=10):
    """A bright day sky behind everything, like Hustle Castle's: soft blue and fluffy white clouds."""
    blue = t.mat((0.42, 0.68, 1.0), 1.0, emit=(0.45, 0.72, 1.0), strength=1.2)
    t.rbox((width * 3, 0.5, height * 3), (0, 30, centre_y), blue, bevel=0)
    white = t.mat((1.0, 1.0, 1.0), 0.9, emit=(0.95, 0.97, 1.0), strength=0.9)
    for _ in range(clouds):
        cx, cz = rng.uniform(-width * 0.6, width * 0.6), centre_y + rng.uniform(0.05, 0.45) * height
        for _ in range(5):
            t.ball(rng.uniform(0.8, 1.6), (cx + rng.uniform(-2, 2), 26, cz + rng.uniform(-0.4, 0.5)), white, scale=(1.3, 0.6, 0.8))


def peak(x, y, base, height, seed, snow_from=0.55):
    """A stylized mountain: a jagged faceted cone, grey-blue rock with snow on the upper part."""
    bpy.ops.mesh.primitive_cone_add(vertices=14, radius1=base, radius2=base * 0.06, depth=height, location=(x, y, height / 2 - 0.5))
    o = bpy.context.active_object
    sub = o.modifiers.new("sub", "SUBSURF")
    sub.levels = sub.render_levels = 3
    sub.subdivision_type = "SIMPLE"
    tex = bpy.data.textures.new("rocky%d" % seed, "VORONOI")
    tex.noise_scale = base * 0.35
    disp = o.modifiers.new("disp", "DISPLACE")
    disp.texture = tex
    disp.strength = base * 0.18
    dec = o.modifiers.new("facets", "DECIMATE")
    dec.ratio = 0.12
    m = bpy.data.materials.new("peak")
    m.use_nodes = True
    nt = m.node_tree
    b = nt.nodes["Principled BSDF"]
    b.inputs["Roughness"].default_value = 0.9
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(geo.outputs["Position"], sep.inputs["Vector"])
    noise = nt.nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 0.6
    add = nt.nodes.new("ShaderNodeMath")
    add.operation = "MULTIPLY_ADD"
    add.inputs[1].default_value = 1.6
    nt.links.new(noise.outputs["Fac"], add.inputs[0])
    nt.links.new(sep.outputs["Z"], add.inputs[2])
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    lo, hi = height * snow_from, height * snow_from + 0.6
    ramp.color_ramp.elements[0].position = 0.0
    ramp.color_ramp.elements[0].color = (0.2, 0.21, 0.27, 1)
    ramp.color_ramp.elements[1].position = 1.0
    ramp.color_ramp.elements[1].color = (0.93, 0.96, 1.0, 1)
    e = ramp.color_ramp.elements.new(0.5)
    e.color = (0.27, 0.27, 0.34, 1)
    ramp.color_ramp.interpolation = "CONSTANT"
    mr = nt.nodes.new("ShaderNodeMapRange")
    mr.inputs["From Min"].default_value = lo - 3
    mr.inputs["From Max"].default_value = hi + 1
    nt.links.new(add.outputs[0], mr.inputs["Value"])
    nt.links.new(mr.outputs["Result"], ramp.inputs["Fac"])
    nt.links.new(ramp.outputs["Color"], b.inputs["Base Color"])
    o.data.materials.append(m)
    bpy.ops.object.shade_flat()
    return o


def render_mountain(out, res=(1536, 768)):
    """The peak above the cutaway: snowy mountains, pines and the Guardians' gatehouse."""
    t.reset(world=(0.35, 0.45, 0.6), soft=True)
    bpy.context.scene.cycles.samples = 96
    rng = random.Random("mountain")
    # the game hides the lowest fifth of the picture under the first floor of the cutaway: lift the gatehouse above it
    LIFT = 3.6
    width, height = 36.0, 18.0
    sky(width, height, height / 2, rng)
    peak(0, 6, 11, 17, 1)
    peak(-12, 9, 9, 12, 2)
    peak(12.5, 9, 9, 13, 3)
    peak(-21, 12, 8, 9, 4)
    peak(21, 12, 8, 10, 5)
    # the rock the fortress is cut into, along the bottom edge
    x = -width / 2 - 1.5
    while x < width / 2 + 1.5:
        for z in (0.0, 1.2):
            q.put("Rock_Medium_%d" % rng.randint(1, 3), (x, -7.0 + rng.uniform(-0.3, 0.3), z + LIFT - 0.4), rot_z=rng.uniform(0, 360),
                  scale=(0.75 * rng.uniform(0.9, 1.1), 0.75, 0.75), tint=rooms.ROCK_TINT, mul=rooms.ROCK_MUL)
        x += rng.uniform(1.6, 2.2)
    for _ in range(26):
        px = rng.uniform(-17, 17)
        if abs(px) < 4.5:
            continue
        pine(px, -5.5 + rng.uniform(0, 2.0), LIFT + 2.0 + rng.uniform(0, 1.2), 0.32, rng)
    # the gatehouse of the Guardians on the shoulder of the mountain: a keep between two towers
    b = Building()
    house(b, 0, 0, width=2, roof_tint=(0.5, 1.2, 0.8))
    for x in (-3.0, 3.0):
        tower(b, x, 0.2, 2, roof_tint=(0.5, 1.2, 0.8), door=False)
    b.place((0, -6.0, LIFT + 1.9), 0.55)
    for x in (-1.65, 1.65):
        t.rbox((0.04, 0.04, 1.6), (x, -6.3, LIFT + 6.1), t.mat((0.2, 0.2, 0.22), 0.4, 0.8), bevel=0)
        t.rbox((0.7, 0.03, 0.45), (x + 0.36, -6.3, LIFT + 6.65), t.mat((0.7, 0.1, 0.08), 0.8), bevel=0.02)
    for x in (-1.0, 1.0):
        t.light("POINT", (x, -7.5, LIFT + 3.0), 120, color=(1.0, 0.6, 0.3), size=0.3)
    t.light("SUN", (0, -10, 20), 4.5, color=(1.0, 0.96, 0.88), rot=(math.radians(45), 0, math.radians(-30)))
    t.light("POINT", (0, -11, LIFT + 6), 900, color=(1.0, 0.6, 0.3), size=1.0)
    _ortho_camera(width, height, (0, height / 2 + 0.6), res)
    t.render(out)


def render_battlefield(out, res=(1536, 768)):
    """Where the fights happen: a sunny clearing at the edge of the forest, rocks and dead trees,
    the Fog creeping in from the right. The heroes stand on the left, the creatures come from the right."""
    t.reset(world=(0.35, 0.45, 0.6), soft=True)
    bpy.context.scene.cycles.samples = 96
    rng = random.Random("battlefield")
    sky(60, 20, 16, rng, clouds=14)
    peak(-10, 40, 12, 14, 7)
    peak(8, 44, 13, 17, 8)
    grass = t.mat((0.3, 0.55, 0.18), 0.9)
    t.rbox((120, 90, 1.0), (0, 15, -0.5), grass, bevel=0)
    path = t.mat((0.42, 0.33, 0.22), 0.95)
    # the heroes and creatures stand on the path: 0.88 of the picture high (GameRunner.DrawBattleStage)
    t.rbox((60, 3.6, 0.05), (0, -10.8, 0.01), path, bevel=0)
    # forest behind, thinning to dead trees on the Fog's side
    for _ in range(80):
        x, y = rng.uniform(-30, 30), rng.uniform(10, 32)
        kind = "DeadTree" if x > 6 and rng.random() < 0.8 else rng.choice(["Pine", "CommonTree", "CommonTree"])
        q.put("%s_%d" % (kind, rng.randint(1, 5)), (x, y, 0), rot_z=rng.uniform(0, 360), scale=rng.uniform(0.55, 0.85))
    for _ in range(14):
        x = rng.choice([rng.uniform(-18, -9), rng.uniform(9, 18)])
        q.put("Rock_Medium_%d" % rng.randint(1, 3), (x, rng.uniform(0.5, 3), 0), rot_z=rng.uniform(0, 360), scale=rng.uniform(0.4, 0.8))
    for _ in range(40):
        q.put(rng.choice(["Grass_Common_Short", "Grass_Wispy_Short", "Clover_1", "Fern_1"]),
              (rng.uniform(-14, 14), rng.uniform(-8, 3), 0), rot_z=rng.uniform(0, 360), scale=rng.uniform(0.5, 0.9))
    # the Fog on the right
    fog = t.mat((0.2, 0.17, 0.27), 1.0)
    for _ in range(30):
        t.ball(rng.uniform(0.8, 1.6), (rng.uniform(12, 26), rng.uniform(6, 16), rng.uniform(-0.3, 1.2)), fog, scale=(1.8, 1, 0.4))
    t.light("SUN", (0, 0, 20), 4.2, color=(1.0, 0.95, 0.85), rot=(math.radians(55), 0, math.radians(-40)))
    sc = bpy.context.scene
    sc.render.resolution_x, sc.render.resolution_y = res
    data = bpy.data.cameras.new("cam")
    data.sensor_fit = "HORIZONTAL"
    data.lens = 30
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
    t.rbox((14, 0.2, 9), (0, 0.5, rooms.H / 2), t.mat((0.05, 0.05, 0.06), 1.0), bevel=0)
    z = -1.3
    while z < rooms.H + 1.4:
        x = -5.6 + rng.uniform(0, 0.6)
        while x < 5.8:
            q.put("Rock_Medium_%d" % rng.randint(1, 3), (x, -1.4 + rng.uniform(-0.4, 0.4), z), rot_z=rng.uniform(0, 360),
                  scale=(0.55 * rng.uniform(0.9, 1.15), 0.55, 0.55 * rng.uniform(0.85, 1.1)), tint=rooms.ROCK_TINT, mul=rooms.ROCK_MUL)
            x += rng.uniform(1.0, 1.3)
        z += rng.uniform(0.7, 0.85)
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


def _ground_material(lush=False):
    m = bpy.data.materials.new("ground")
    m.use_nodes = True
    nt = m.node_tree
    b = nt.nodes["Principled BSDF"]
    b.inputs["Roughness"].default_value = 0.9
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(geo.outputs["Position"], sep.inputs["Vector"])
    noise = nt.nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 0.35
    madd = nt.nodes.new("ShaderNodeMath")
    madd.operation = "MULTIPLY_ADD"
    madd.inputs[1].default_value = 0.5
    nt.links.new(noise.outputs["Fac"], madd.inputs[0])
    nt.links.new(sep.outputs["Z"], madd.inputs[2])
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    els = ramp.color_ramp.elements
    els[0].position, els[0].color = 0.0, (0.82, 0.72, 0.48, 1)
    els[1].position, els[1].color = 1.0, (0.5, 0.48, 0.46, 1)
    stops = ((0.07, (0.82, 0.72, 0.48, 1)), (0.11, (0.4, 0.62, 0.22, 1)), (0.5, (0.28, 0.5, 0.17, 1)), (0.85, (0.34, 0.4, 0.22, 1)))
    if lush:
        # deeper, varied greens like a painted kingdom map: a thin beach, meadows, darker woods, stony tops
        els[1].color = (0.42, 0.42, 0.4, 1)
        stops = ((0.03, (0.86, 0.76, 0.5, 1)), (0.06, (0.36, 0.56, 0.16, 1)), (0.3, (0.27, 0.48, 0.13, 1)), (0.55, (0.2, 0.4, 0.12, 1)),
                 (0.8, (0.3, 0.38, 0.18, 1)))
        noise.inputs["Scale"].default_value = 0.6
        madd.inputs[1].default_value = 0.9
    for pos, col in stops:
        e = els.new(pos)
        e.color = col
    mr = nt.nodes.new("ShaderNodeMapRange")
    mr.inputs["From Min"].default_value = 0.0
    mr.inputs["From Max"].default_value = 2.4
    nt.links.new(madd.outputs[0], mr.inputs["Value"])
    nt.links.new(mr.outputs["Result"], ramp.inputs["Fac"])
    # the north is under the Fog: the ground turns grey-violet
    fog = nt.nodes.new("ShaderNodeMapRange")
    fog.inputs["From Min"].default_value = 3.0
    fog.inputs["From Max"].default_value = 8.0
    nt.links.new(sep.outputs["Y"], fog.inputs["Value"])
    mix = nt.nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    mix.inputs[7].default_value = (0.3, 0.26, 0.36, 1)
    nt.links.new(fog.outputs["Result"], mix.inputs["Factor"])
    nt.links.new(ramp.outputs["Color"], mix.inputs[6])
    nt.links.new(mix.outputs[2], b.inputs["Base Color"])
    return m


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


def _boat(x, y, rot, sail=(0.95, 0.92, 0.85)):
    """A little sailing boat: a wooden hull, a deck, a mast and a sail with a red pennant."""
    wood = t.mat((0.45, 0.26, 0.12), 0.7)
    root = bpy.data.objects.new("boat", None)
    bpy.context.scene.collection.objects.link(root)
    hull = t.ball(0.5, (0, 0, 0.05), wood, scale=(1.4, 0.55, 0.5))
    t.rbox((1.45, 0.58, 0.05), (0, 0, 0.16), t.mat((0.75, 0.2, 0.12), 0.7), bevel=0.02).parent = root
    deck = t.rbox((1.5, 0.5, 0.06), (0, 0, 0.22), t.mat((0.62, 0.42, 0.22), 0.7), bevel=0.02)
    mast = t.cyl(0.04, 1.4, (0.05, 0, 0.9), wood)
    cloth = t.rbox((0.04, 0.8, 0.85), (0.12, 0, 1.0), t.mat(sail, 0.8), bevel=0.02)
    flag = t.rbox((0.02, 0.3, 0.14), (0.05, 0.15, 1.6), t.mat((0.8, 0.12, 0.1), 0.8), bevel=0)
    for o in (hull, deck, mast, cloth, flag):
        o.parent = root
    root.location = (x, y, 0.1)
    root.rotation_euler = (0, 0, math.radians(rot))
    root.scale = (1.3, 1.3, 1.3)


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


def _bridge(x0, y0, x1, y1):
    """A wooden bridge over a narrow strait."""
    wood = t.mat((0.5, 0.32, 0.16), 0.7)
    n = int(math.hypot(x1 - x0, y1 - y0) / 0.32)
    ang = math.atan2(y1 - y0, x1 - x0)
    for k in range(n + 1):
        f = k / n
        x, y = x0 + (x1 - x0) * f, y0 + (y1 - y0) * f
        z = 0.25 + 0.25 * math.sin(f * math.pi)
        t.rbox((0.28, 0.9, 0.06), (x, y, z), wood, bevel=0.01, rot=(0, 0, ang))
        if k % 3 == 0:
            for side in (-1, 1):
                t.cyl(0.04, 0.6, (x - side * 0.42 * math.sin(ang), y + side * 0.42 * math.cos(ang), z - 0.1), wood)


def _road(points, noise, rng):
    """A dirt road along a polyline on land: overlapping flat discs of earth, a few stones."""
    dirt = t.mat((0.58, 0.44, 0.27), 0.95)
    edge = t.mat((0.44, 0.33, 0.2), 0.95)
    for (x0, y0), (x1, y1) in zip(points, points[1:]):
        n = max(2, int(math.hypot(x1 - x0, y1 - y0) / 0.16))
        for k in range(n):
            f = k / n
            x, y = x0 + (x1 - x0) * f, y0 + (y1 - y0) * f
            z = _wheight(x, y, noise)
            if z < 0.05:
                continue
            t.cyl(0.3, 0.06, (x, y, z), edge, verts=12)
            t.cyl(0.23, 0.06, (x, y, z + 0.02), dirt, verts=12)
            if rng.random() < 0.1:
                q.put("Pebble_Round_%d" % rng.randint(1, 5), (x + rng.uniform(-0.35, 0.35), y + rng.uniform(-0.35, 0.35), z), scale=0.3)


def _pier(x, y, rot):
    """A wooden pier into the sea with a boat tied to it: the routes start here."""
    wood = t.mat((0.5, 0.32, 0.16), 0.7)
    root = bpy.data.objects.new("pier", None)
    bpy.context.scene.collection.objects.link(root)
    for k in range(7):
        plank = t.rbox((0.3, 1.0, 0.06), (k * 0.3, 0, 0.3), wood, bevel=0.01)
        plank.parent = root
        if k % 2 == 0:
            for side in (-1, 1):
                post = t.cyl(0.05, 0.8, (k * 0.3, side * 0.45, 0.0), wood)
                post.parent = root
    root.location = (x, y, 0)
    root.rotation_euler = (0, 0, math.radians(rot))


def render_worldmap(out, res=(2400, 1350)):
    """The kingdom around the fortress, like Hustle Castle's map: an archipelago in a blue sea. The
    fortress has its own island with a village and a pier; the campaign crosses three islands from
    a bridge to the Heart of the Fog; the arena, the camp to raid, the hunters' dark woods and the
    portal of the events each stand on an island of their own. Dotted sea routes with sailing boats
    join them; shallows and rocks ring the shores. Returns where each place and act is drawn (shares
    of the picture)."""
    t.reset(world=(0.32, 0.45, 0.62), soft=True)
    bpy.context.scene.cycles.samples = 64
    rng = random.Random("archipelago")
    noise = (rng.uniform(0, 6), rng.uniform(0, 6), rng.uniform(0, 6))
    bpy.ops.mesh.primitive_grid_add(x_subdivisions=220, y_subdivisions=140, size=1, location=(0, 0, 0))
    ground = bpy.context.active_object
    ground.scale = (40, 26, 1)
    bpy.ops.object.transform_apply(scale=True)
    for v in ground.data.vertices:
        z = _wheight(v.co.x, v.co.y, noise)
        # the shores dive under the shallows (where two islands meet the ground would lie at the water's level)
        v.co.z = z if z > 0.03 else min(z, -0.12)
    ground.data.materials.append(_ground_material(lush=True))
    bpy.ops.object.shade_smooth()
    # the sea: deep blue, lighter shallows around every island
    t.rbox((120, 90, 0.1), (0, 0, -0.06), t.mat((0.06, 0.3, 0.55), 0.12), bevel=0)
    shallow = t.mat((0.2, 0.62, 0.72), 0.15)
    for k, ((cx, cy), (rx, ry), _, _) in enumerate(ISLANDS.values()):
        t.cyl(1.0, 0.04, (cx, cy, -0.02 - 0.004 * k), shallow, verts=64).scale = (rx * 1.18, ry * 1.25, 1)
    # little white waves on the open sea
    foam = t.mat((0.92, 0.97, 1.0), 0.6)
    wave_rng = random.Random("waves")
    for _ in range(160):
        x, y = wave_rng.uniform(-18, 18), wave_rng.uniform(-10, 11)
        if _wheight(x, y, noise) < -0.6:
            t.ball(0.3, (x, y, 0.0), foam, scale=(1.0, 0.16, 0.06)).rotation_euler = (0, 0, math.radians(wave_rng.uniform(-12, 12)))
    # rocks along the shores
    for (cx, cy), (rx, ry), _, _ in ISLANDS.values():
        for k in range(int((rx + ry) * 2.2)):
            a = rng.uniform(0, math.tau)
            x, y = cx + rx * 0.98 * math.cos(a), cy + ry * 0.98 * math.sin(a)
            q.put("Rock_Medium_%d" % rng.randint(1, 3), (x, y, -0.15), rot_z=rng.uniform(0, 360), scale=rng.uniform(0.14, 0.24),
                  tint=rooms.ROCK_TINT, mul=rooms.ROCK_MUL, share=True)
    # sea routes with boats, a bridge from home to the campaign, the pier of the routes
    route_rng = random.Random("routes")
    for k, (a, b) in enumerate(SEA_ROUTES):
        mx, my = _sea_route(a, b, route_rng)
        bx, by = mx + route_rng.uniform(-0.5, 0.5), my + route_rng.uniform(-0.4, 0.4)
        # only in open water, clear of the shores
        if all(_wheight(bx + dx, by + dy, noise) < -0.3 for dx, dy in ((0, 0), (1.2, 0), (-1.2, 0), (0, 1.0), (0, -1.0))):
            _boat(bx, by, route_rng.uniform(0, 360))
    _bridge(-6.0, -2.2, -6.4, -1.0)
    _pier(-6.0, -6.6, -30)
    _boat(-4.6, -7.5, 20, sail=(0.85, 0.2, 0.15))
    _boat(15.5, -1.4, 110, sail=(0.85, 0.2, 0.15))
    _boat(-15.6, 1.4, -70)
    # roads on the islands: from the fortress to the pier and to the bridge, along the acts
    road_rng = random.Random("roads")
    _road([PLACES["Fortress"][0], (-9.0, -4.6), PLACES["Routes"][0]], noise, road_rng)
    _road([PLACES["Fortress"][0], (-8.4, -2.8), (-6.2, -2.4)], noise, road_rng)
    _road(WORLD_ACTS[:5], noise, road_rng)
    _road(WORLD_ACTS[5:7], noise, road_rng)
    _road(WORLD_ACTS[7:], noise, road_rng)
    # the places and the acts
    for key, ((x, y), build, scale) in PLACES.items():
        bld = Building()
        build(bld, random.Random(key))
        bld.place((x, y, _wheight(x, y, noise) - 0.05), scale)
    village = Building()
    house(village, -3.0, 2.5, roof_tint=(0.5, 1.0, 1.0))
    house(village, 3.0, 2.0, roof_tint=(0.53, 1.0, 0.9))
    village.place((-9.2, -2.2, _wheight(-9.2, -2.2, noise) - 0.05), 0.3)
    acts_builders = [a[1] for a in ACTS]
    for (x, y), build in zip(WORLD_ACTS, acts_builders):
        bld = Building()
        build(bld, random.Random(str((x, y))))
        bld.place((x, y, _wheight(x, y, noise) - 0.05), 0.24, rot_z=rng.uniform(-15, 15))
    busy = [PLACES[k][0] for k in PLACES] + WORLD_ACTS + [(-9.2, -2.2)]
    # woods, rocks and bushes on the islands; the dark ones are dead woods
    for k in range(1500):
        key, kind = None, None
        while key is None:
            x, y = rng.uniform(-17, 17), rng.uniform(-9.5, 10.5)
            key, kind = _island_of(x, y)
        z = _wheight(x, y, noise)
        if z < 0.15 or any((x - px) ** 2 + (y - py) ** 2 < 2.4 for px, py in busy):
            continue
        r = rng.random()
        if r < 0.6:
            name = ("DeadTree" if rng.random() < 0.6 else "Pine") if kind == "dark" else ("Pine" if z > 1.0 else "CommonTree")
            q.put("%s_%d" % (name, rng.randint(1, 5)), (x, y, z - 0.05), rot_z=rng.uniform(0, 360), scale=rng.uniform(0.13, 0.18), share=True)
        elif r < 0.7:
            q.put("Rock_Medium_%d" % rng.randint(1, 3), (x, y, z - 0.1), rot_z=rng.uniform(0, 360), scale=rng.uniform(0.12, 0.2), share=True)
        elif kind != "dark":
            q.put(rng.choice(["Bush_Common", "Bush_Common_Flowers", "Grass_Common_Tall"]), (x, y, z), rot_z=rng.uniform(0, 360), scale=0.2, share=True)
    # the Fog over the northern islands, and clouds at the edges of the map
    fogm = t.mat((0.36, 0.32, 0.44), 1.0)
    for _ in range(22):
        cx, cy = rng.uniform(-18, 18), rng.uniform(10.5, 13.0)
        for _ in range(3):
            t.ball(rng.uniform(0.5, 1.0), (cx + rng.uniform(-1, 1), cy + rng.uniform(-0.4, 0.4), 1.8 + rng.uniform(0, 0.5)), fogm,
                   scale=(1.3, 1, 0.5))
    white = t.mat((1.0, 1.0, 1.0), 0.9)
    for cx, cy in ((-17, 9), (17.5, -8.5), (-17.5, -9.5), (18, 6)):
        for _ in range(6):
            t.ball(rng.uniform(0.9, 1.6), (cx + rng.uniform(-1.5, 1.5), cy + rng.uniform(-1, 1), 3.0 + rng.uniform(0, 0.6)), white,
                   scale=(1.4, 1, 0.6))
    t.light("SUN", (0, 0, 20), 4.0, color=(1.0, 0.95, 0.86), rot=(math.radians(42), 0, math.radians(-35)))
    t.light("SUN", (0, 0, 20), 1.0, color=(0.6, 0.7, 1.0), rot=(math.radians(-30), 0, math.radians(150)))
    sc = bpy.context.scene
    sc.render.resolution_x, sc.render.resolution_y = res
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    sc.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 34.0
    tilt = math.radians(52)
    cam.location = (0.3, -30 * math.cos(tilt) + 1.4, 30 * math.sin(tilt))
    cam.rotation_euler = (math.radians(90) - tilt, 0, 0)
    sc.camera = cam
    bpy.context.view_layer.update()

    def at(x, y, lift=0.8):
        p = world_to_camera_view(sc, cam, Vector((x, y, max(0.0, _wheight(x, y, noise)) + lift)))
        return {"x": round(p.x, 4), "y": round(1 - p.y, 4)}
    places = {key: at(*PLACES[key][0], lift=1.6) for key in PLACES}
    acts = [dict(at(x, y), name=a[2]) for (x, y), a in zip(WORLD_ACTS, ACTS)]
    t.render(out)
    return {"acts": acts, "places": places}
