"""The world around the fortress, from KayKit's Medieval Hexagon pack and toy shapes (CC0):
the mountain peak with the watchtowers above the cutaway, the bare rock of undug cells, the frost
over frozen rooms and the campaign map (an island of hex tiles with ten places, one per act)."""
import math
import random

import bpy
from bpy_extras.object_utils import world_to_camera_view
from mathutils import Vector

import kit
import toy as t

ROCK = [(0.11, 0.105, 0.12), (0.13, 0.125, 0.14), (0.095, 0.09, 0.105), (0.15, 0.14, 0.15)]


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


def pine(x, y, z, h, rng, snow=True):
    green = t.mat(t.jitter((0.12, 0.32, 0.2), rng, 0.04), 0.7)
    t.cyl(h * 0.06, h * 0.3, (x, y, z + h * 0.15), t.mat((0.3, 0.18, 0.1), 0.8))
    for k in range(3):
        r = h * (0.34 - k * 0.08)
        bpy.ops.mesh.primitive_cone_add(vertices=12, radius1=r, radius2=0, depth=h * 0.45, location=(x, y, z + h * (0.38 + k * 0.2)))
        c = bpy.context.active_object
        c.data.materials.append(green)
        bpy.ops.object.shade_smooth()
        if snow:
            bpy.ops.mesh.primitive_cone_add(vertices=12, radius1=r * 0.55, radius2=0, depth=h * 0.22, location=(x, y, z + h * (0.49 + k * 0.2)))
            s = bpy.context.active_object
            s.data.materials.append(t.mat((0.92, 0.95, 1.0), 0.5))


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
    """The peak above the cutaway: snowy mountains, pines and the watchtowers of the Guardians."""
    t.reset(world=(0.04, 0.05, 0.1))
    bpy.context.scene.cycles.samples = 96
    rng = random.Random("mountain")
    width, height = 36.0, 18.0
    sky(width, height, height / 2, rng)
    peak(0, 6, 11, 17, 1)
    peak(-12, 9, 9, 12, 2)
    peak(12.5, 9, 9, 13, 3)
    peak(-21, 12, 8, 9, 4)
    peak(21, 12, 8, 10, 5)
    # the rock the fortress is cut into, along the bottom edge
    for row in range(2):
        x = -width / 2 - 1
        while x < width / 2 + 1:
            size = rng.uniform(1.6, 2.6)
            t.rbox((size + 0.2, 3.0, 1.6), (x + size / 2, -1.0, row * 1.4 + 0.2), t.mat(t.jitter(rng.choice(ROCK), rng, 0.02), 0.95), bevel=0.35)
            x += size
    for _ in range(22):
        px = rng.uniform(-17, 17)
        if abs(px) < 3:
            continue
        pine(px, -2.2 + rng.uniform(0, 1.5), 2.6 + rng.uniform(0, 1.5), rng.uniform(1.8, 3.0), rng)
    # the watchtowers on the shoulders of the peak, the gate in the middle
    kit.put("building_castle_red", (0, -1.2, 2.6), rot_z=180, scale=2.6)
    for x in (-5.0, 5.0):
        kit.put("building_tower_A_red", (x, -1.0, 2.6), rot_z=180, scale=2.4)
        kit.put("flag_red", (x, -1.0, 2.6 + 2.2 * 2.4), scale=5.0)
    t.light("SUN", (0, -10, 20), 2.5, color=(0.75, 0.82, 1.0), rot=(math.radians(45), 0, math.radians(-30)))
    t.light("POINT", (0, -5, 6), 1600, color=(1.0, 0.6, 0.3), size=1.0)
    _ortho_camera(width, height, (0, height / 2 + 0.6), res)
    t.render(out)


def render_rock(out, res=(768, 480)):
    """A cell not dug yet: the raw rock of the mountain."""
    t.reset(world=(0.02, 0.02, 0.03))
    bpy.context.scene.cycles.samples = 48
    rng = random.Random("rock")
    for row in range(6):
        x = -8 - rng.uniform(0, 1)
        while x < 8:
            size = rng.uniform(1.4, 2.4)
            t.rbox((size + 0.15, 2.0, 1.6), (x + size / 2, 0, row * 1.45 - 0.6), t.mat(t.jitter(rng.choice(ROCK), rng, 0.02), 0.95), bevel=0.3)
            x += size
    t.light("AREA", (0, -8, 6), 900, color=(0.75, 0.8, 1.0), size=10, rot=(math.radians(50), 0, 0))
    _ortho_camera(12.3, 7.6, (0, 3.2), res)
    t.render(out)


def render_frost(out, res=(768, 480)):
    """Ice over a frozen room: icicles along the top, rime in the corners (transparent)."""
    t.reset(world=(0, 0, 0))
    bpy.context.scene.cycles.samples = 48
    rng = random.Random("frost")
    ice = t.mat((0.75, 0.9, 1.0), 0.15)
    x = -6.0
    while x < 6.0:
        h = rng.uniform(0.4, 1.3)
        bpy.ops.mesh.primitive_cone_add(vertices=10, radius1=rng.uniform(0.12, 0.24), radius2=0, depth=h, location=(x, 0, 7.4 - h / 2),
                                        rotation=(math.radians(180), 0, 0))
        bpy.context.active_object.data.materials.append(ice)
        bpy.ops.object.shade_smooth()
        x += rng.uniform(0.25, 0.6)
    t.rbox((12.4, 0.3, 0.35), (0, 0, 7.55), t.mat((0.9, 0.96, 1.0), 0.4), bevel=0.15)
    for cx in (-6, 6):
        for _ in range(26):
            t.ball(rng.uniform(0.1, 0.32), (cx * rng.uniform(0.8, 1.0), 0, rng.uniform(0, 7)), t.mat((0.85, 0.95, 1.0), 0.3), scale=(1, 0.3, 1))
    t.light("AREA", (0, -8, 6), 900, color=(0.8, 0.9, 1.0), size=10, rot=(math.radians(50), 0, 0))
    _ortho_camera(12.3, 7.6, (0, 3.8), res)
    t.render(out, transparent=True)


# ------------------------------------------------------------------ the campaign map

# the ten acts: (column, row) on the hex grid and what stands there
ACTS = [
    ((6, 1), "building_home_A_red", "Деревня у подножия"),
    ((4, 2), "building_mine_red", "Старые копи"),
    ((2, 3), "hills_A_trees", "Шепчущий лес"),
    ((4, 4), "building_destroyed", "Разрушенный монастырь"),
    ((7, 4), "building_watermill_red", "Мельница на реке"),
    ((9, 5), "building_castle_blue", "Застава Двора"),
    ((7, 7), "tree_dead_large", "Мёртвая роща"),
    ((4, 7), "building_tower_B_blue", "Башня отшельника"),
    ((3, 9), "building_destroyed", "Курганы"),
    ((6, 10), "building_castle_yellow", "Сердце Тумана"),
]
COLS, ROWS = 12, 12


def hex_pos(col, row):
    return (col * 2.0 + (1.0 if row % 2 else 0.0) - COLS, row * 1.725 - ROWS * 0.86)


def render_map(out, res=(1600, 1000)):
    """The island of the campaign: grass, forests, mountains, a river and the ten places."""
    t.reset(world=(0.1, 0.2, 0.32))
    bpy.context.scene.cycles.samples = 64
    rng = random.Random("map")
    acts = {a[0]: a for a in ACTS}
    river = set()
    lakes = {(5, 3), (5, 4), (6, 4), (8, 8), (9, 8)}
    for row in range(ROWS):
        for col in range(COLS):
            x, y = hex_pos(col, row)
            edge = min(col, row, COLS - 1 - col, ROWS - 1 - row)
            if edge == 0 or (edge == 1 and rng.random() < 0.35):
                kit.put("hex_water", (x, y, 0))
                continue
            if (col, row) in lakes:
                kit.put("hex_water", (x, y, 0))
                continue
            kit.put("hex_grass", (x, y, 0), rot_z=60 * rng.randint(0, 5), tint=(0.5, 0.8, 0.82))
            if (col, row) in acts:
                _, model, _ = acts[(col, row)]
                kit.put(model, (x, y, 0), rot_z=rng.choice((0, 60, 300)), scale=0.3 if model.startswith("tree") else 1.0)
            elif (col, row) not in river:
                r = rng.random()
                if row >= 8 and r < 0.35:
                    kit.put(rng.choice(["tree_dead_medium", "tree_dead_small", "gravestone"]), (x, y, 0), rot_z=rng.uniform(0, 360), scale=0.25)
                elif r < 0.22:
                    kit.put(rng.choice(["mountain_A_grass_trees", "mountain_B_grass_trees", "mountain_C_grass"]), (x, y, 0), rot_z=60 * rng.randint(0, 5))
                elif r < 0.5:
                    kit.put(rng.choice(["hills_A_trees", "hills_B_trees", "hills_C_trees"]), (x, y, 0), rot_z=60 * rng.randint(0, 5))
    # the Fog over the north: dark clouds
    for _ in range(9):
        kit.put(rng.choice(["cloud_big", "cloud_small"]), (rng.uniform(-10, 10), ROWS * 0.86 - rng.uniform(0, 3), 3.5 + rng.uniform(0, 1)),
                scale=1.4, tint=(0.75, 0.5, 0.45))
    t.light("SUN", (0, 0, 20), 2.2, color=(1.0, 0.93, 0.82), rot=(math.radians(40), 0, math.radians(-35)))
    sc = bpy.context.scene
    sc.render.resolution_x, sc.render.resolution_y = res
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    sc.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = COLS * 2.0 + 3.0
    tilt = math.radians(48)
    cam.location = (0, -30 * math.cos(tilt) - 1.5, 30 * math.sin(tilt))
    cam.rotation_euler = (math.radians(90) - tilt, 0, 0)
    sc.camera = cam
    bpy.context.view_layer.update()
    places = []
    for (col, row), model, name in ACTS:
        x, y = hex_pos(col, row)
        p = world_to_camera_view(sc, cam, Vector((x, y, 0.6)))
        places.append({"name": name, "x": round(p.x, 4), "y": round(1 - p.y, 4)})
    t.render(out)
    return {"acts": places}
