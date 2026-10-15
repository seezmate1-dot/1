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

    def put(self, name, loc, rot_z=0.0, scale=1.0, tint=None):
        p = q.put(name, loc, rot_z=rot_z, scale=scale, tint=tint)
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

def sky(width, height, centre_y, rng, stars=60):
    """Twilight sky behind everything: a gradient plane, the moon, a few stars."""
    top = t.mat((0.06, 0.08, 0.2), 1.0, emit=(0.08, 0.1, 0.28), strength=1.0)
    t.rbox((width * 3, 0.5, height * 3), (0, 30, centre_y), top, bevel=0)
    t.ball(1.6, (width * 0.3, 25, centre_y + height * 0.32), t.mat((1, 0.97, 0.85), 0.5, emit=(1, 0.95, 0.8), strength=6))
    for _ in range(stars):
        t.ball(rng.uniform(0.05, 0.12), (rng.uniform(-width, width) * 0.6, 25, centre_y + rng.uniform(-0.1, 0.5) * height),
               t.mat((1, 1, 1), 0.5, emit=(1, 0.95, 0.85), strength=rng.uniform(3, 8)))


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
    t.reset(world=(0.04, 0.05, 0.1))
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
                  scale=(0.75 * rng.uniform(0.9, 1.1), 0.75, 0.75), tint=rooms.ROCK_TINT)
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
    t.light("SUN", (0, -10, 20), 2.5, color=(0.75, 0.82, 1.0), rot=(math.radians(45), 0, math.radians(-30)))
    t.light("POINT", (0, -11, LIFT + 6), 900, color=(1.0, 0.6, 0.3), size=1.0)
    _ortho_camera(width, height, (0, height / 2 + 0.6), res)
    t.render(out)


def render_battlefield(out, res=(1536, 768)):
    """Where the fights happen: a clearing at the edge of the forest at dusk, rocks and dead trees,
    the Fog creeping in from the right. The heroes stand on the left, the creatures come from the right."""
    t.reset(world=(0.05, 0.06, 0.12))
    bpy.context.scene.cycles.samples = 96
    rng = random.Random("battlefield")
    sky(60, 20, 16, rng, stars=40)
    peak(-10, 40, 12, 14, 7)
    peak(8, 44, 13, 17, 8)
    grass = t.mat((0.22, 0.38, 0.16), 0.9)
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
    t.light("SUN", (0, 0, 20), 2.6, color=(1.0, 0.75, 0.55), rot=(math.radians(60), 0, math.radians(-50)))
    t.light("SUN", (0, 0, 20), 0.9, color=(0.55, 0.65, 1.0), rot=(math.radians(-40), 0, math.radians(160)))
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
    t.reset(world=(0.02, 0.02, 0.03))
    bpy.context.scene.cycles.samples = 48
    rng = random.Random("rock")
    t.rbox((14, 0.2, 9), (0, 0.5, rooms.H / 2), t.mat((0.05, 0.05, 0.06), 1.0), bevel=0)
    z = -1.3
    while z < rooms.H + 1.4:
        x = -5.6 + rng.uniform(0, 0.6)
        while x < 5.8:
            q.put("Rock_Medium_%d" % rng.randint(1, 3), (x, -1.4 + rng.uniform(-0.4, 0.4), z), rot_z=rng.uniform(0, 360),
                  scale=(0.55 * rng.uniform(0.9, 1.15), 0.55, 0.55 * rng.uniform(0.85, 1.1)), tint=rooms.ROCK_TINT)
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
              tint=rooms.ROCK_TINT)
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
ISLAND = (13.0, 11.0)   # half-sizes of the island's ellipse


def _height(x, y, rng_noise):
    """The island's ground: a dome under the ellipse with gentle hills; below 0 is the sea."""
    d = math.sqrt((x / ISLAND[0]) ** 2 + (y / ISLAND[1]) ** 2)
    base = 1.0 - d ** 2.2
    hills = 0.6 * math.sin(x * 0.55 + rng_noise[0]) * math.cos(y * 0.6 + rng_noise[1]) + 0.3 * math.sin(x * 1.3 + y * 0.9 + rng_noise[2])
    return base * 1.4 + hills * max(0.0, base) ** 0.5 - 0.1


def _ground_material():
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
    for pos, col in ((0.07, (0.82, 0.72, 0.48, 1)), (0.11, (0.4, 0.62, 0.22, 1)), (0.5, (0.28, 0.5, 0.17, 1)), (0.85, (0.34, 0.4, 0.22, 1))):
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


def render_map(out, res=(1600, 1000)):
    """The island of the campaign: beaches, meadows, forests and rocks, the Fog in the north, ten places."""
    t.reset(world=(0.1, 0.2, 0.32))
    bpy.context.scene.cycles.samples = 64
    rng = random.Random("map")
    noise = (rng.uniform(0, 6), rng.uniform(0, 6), rng.uniform(0, 6))
    # the ground
    bpy.ops.mesh.primitive_grid_add(x_subdivisions=120, y_subdivisions=100, size=1, location=(0, 0, 0))
    ground = bpy.context.active_object
    ground.scale = (32, 28, 1)
    bpy.ops.object.transform_apply(scale=True)
    for v in ground.data.vertices:
        v.co.z = _height(v.co.x, v.co.y, noise)
    ground.data.materials.append(_ground_material())
    bpy.ops.object.shade_smooth()
    sea = t.mat((0.12, 0.35, 0.55), 0.15)
    t.rbox((80, 70, 0.1), (0, 0, -0.02), sea, bevel=0)
    # the places of the acts
    for (x, y), build, name in ACTS:
        b = Building()
        build(b, random.Random(name))
        b.place((x, y, _height(x, y, noise)), 0.22, rot_z=rng.uniform(-15, 15))
    # forests, groves and rocks between them
    groves = [(rng.uniform(-11, 11), rng.uniform(-9, 9)) for _ in range(9)]
    for k in range(420):
        if k % 3:
            gx, gy = rng.choice(groves)
            x, y = gx + rng.gauss(0, 1.3), gy + rng.gauss(0, 1.0)
        else:
            x, y = rng.uniform(-12, 12), rng.uniform(-10, 10)
        z = _height(x, y, noise)
        if z < 0.25 or any((x - ax) ** 2 + (y - ay) ** 2 < 2.4 for (ax, ay), _, _ in ACTS):
            continue
        north = y > 3.5
        r = rng.random()
        if r < 0.55:
            kind = "DeadTree" if north and rng.random() < 0.7 else ("Pine" if z > 1.2 else "CommonTree")
            q.put("%s_%d" % (kind, rng.randint(1, 5)), (x, y, z - 0.05), rot_z=rng.uniform(0, 360), scale=rng.uniform(0.15, 0.2))
        elif r < 0.7:
            q.put("Rock_Medium_%d" % rng.randint(1, 3), (x, y, z - 0.1), rot_z=rng.uniform(0, 360), scale=rng.uniform(0.15, 0.25))
        elif r < 0.8 and not north:
            q.put(rng.choice(["Bush_Common", "Bush_Common_Flowers"]), (x, y, z), rot_z=rng.uniform(0, 360), scale=0.2)
    # the Fog over the north: dark clouds
    fogm = t.mat((0.3, 0.26, 0.36), 1.0)
    for _ in range(12):
        cx, cy = rng.uniform(-10, 10), rng.uniform(9.0, 11.5)
        if abs(cx - 1.0) < 2.5:
            continue
        for _ in range(3):
            t.ball(rng.uniform(0.6, 1.1), (cx + rng.uniform(-1.0, 1.0), cy + rng.uniform(-0.5, 0.5), 2.0 + rng.uniform(0, 0.5)), fogm,
                   scale=(1.3, 1, 0.6))
    t.light("SUN", (0, 0, 20), 3.4, color=(1.0, 0.93, 0.82), rot=(math.radians(40), 0, math.radians(-35)))
    t.light("SUN", (0, 0, 20), 0.8, color=(0.6, 0.7, 1.0), rot=(math.radians(-30), 0, math.radians(150)))
    sc = bpy.context.scene
    sc.render.resolution_x, sc.render.resolution_y = res
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    sc.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 27.0
    tilt = math.radians(48)
    cam.location = (0, -30 * math.cos(tilt) - 1.0, 30 * math.sin(tilt))
    cam.rotation_euler = (math.radians(90) - tilt, 0, 0)
    sc.camera = cam
    bpy.context.view_layer.update()
    places = []
    for (x, y), build, name in ACTS:
        p = world_to_camera_view(sc, cam, Vector((x, y, _height(x, y, noise) + 0.6)))
        places.append({"name": name, "x": round(p.x, 4), "y": round(1 - p.y, 4)})
    t.render(out)
    return {"acts": places}
