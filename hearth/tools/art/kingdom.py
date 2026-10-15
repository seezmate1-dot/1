"""The map of the kingdom with real-world looks: the same archipelago as world.py (islands, places,
acts, sea routes), but built from Poly Haven's photoscanned assets (phkit.py) and lit by a real sky.

- The ground is one smooth mesh: sand on the shores, grass and forest floor inland, bare rock on
  the slopes, mud and burnt earth on the dark islands, dirt roads painted into it.
- The sea darkens from turquoise shallows to deep blue, with surf along the shores and small waves.
- Real trees, bushes and mossy rocks; real sailing ships on the routes, a wooden pier.
- The buildings of the places and the acts stay the Medieval Village kit's (their textures are real
  PBR too).
"""
import math
import random

import bpy
from bpy_extras.object_utils import world_to_camera_view
from mathutils import Vector

import phkit as ph
import toy as t
import world as w

SKY = "kloofendal_43d_clear_puresky"


def _smooth(a, b, x):
    x = max(0.0, min(1.0, (x - a) / (b - a)))
    return x * x * (3 - 2 * x)


def _seg_dist(px, py, ax, ay, bx, by):
    dx, dy = bx - ax, by - ay
    f = max(0.0, min(1.0, ((px - ax) * dx + (py - ay) * dy) / max(1e-9, dx * dx + dy * dy)))
    return math.hypot(px - ax - f * dx, py - ay - f * dy)


ROADS = [
    [w.PLACES["Fortress"][0], (-9.0, -4.6), w.PLACES["Routes"][0]],
    [w.PLACES["Fortress"][0], (-8.4, -2.8), (-6.2, -2.4)],
    w.WORLD_ACTS[:5],
    w.WORLD_ACTS[5:7],
    w.WORLD_ACTS[7:],
]


def _road_dist(x, y):
    best = 9.0
    for pts in ROADS:
        for (ax, ay), (bx, by) in zip(pts, pts[1:]):
            best = min(best, _seg_dist(x, y, ax, ay, bx, by))
    return best


def _attr(mesh, name, values):
    a = mesh.attributes.new(name, "FLOAT", "POINT")
    a.data.foreach_set("value", values)


def _mix(nt, fac, a, b):
    m = nt.nodes.new("ShaderNodeMix")
    m.data_type = "RGBA"
    nt.links.new(fac, m.inputs["Factor"])
    nt.links.new(a, m.inputs[6])
    nt.links.new(b, m.inputs[7])
    return m.outputs[2]


def _mixf(nt, fac, a, b):
    m = nt.nodes.new("ShaderNodeMix")
    m.data_type = "FLOAT"
    nt.links.new(fac, m.inputs["Factor"])
    nt.links.new(a, m.inputs[2])
    nt.links.new(b, m.inputs[3])
    return m.outputs[0]


def _layer(nt, name, size, coords, tint=None):
    """The colour, roughness and normal of one Poly Haven texture, tiled every `size` units; tint
    multiplies its colour."""
    maps = ph.texture_maps(name, "2k")
    mp = nt.nodes.new("ShaderNodeMapping")
    mp.inputs["Scale"].default_value = (1 / size, 1 / size, 1 / size)
    nt.links.new(coords, mp.inputs["Vector"])
    v = mp.outputs["Vector"]
    col = ph._image(nt, maps["diff"], v, box=False).outputs["Color"]
    if tint:
        rgb = nt.nodes.new("ShaderNodeRGB")
        rgb.outputs[0].default_value = tuple(tint) + (1,)
        mul = nt.nodes.new("ShaderNodeMix")
        mul.data_type = "RGBA"
        mul.blend_type = "MULTIPLY"
        mul.inputs["Factor"].default_value = 1.0
        nt.links.new(col, mul.inputs[6])
        nt.links.new(rgb.outputs[0], mul.inputs[7])
        col = mul.outputs[2]
    rough = ph._image(nt, maps["rough"], v, non_color=True, box=False).outputs["Color"] if "rough" in maps else None
    nor = ph._image(nt, maps["nor"], v, non_color=True, box=False).outputs["Color"]
    return col, rough, nor


def _ground_material():
    """Sand, grass, rock, dark earth and road, mixed by the masks painted on the mesh's points."""
    m = bpy.data.materials.new("kingdom_ground")
    m.use_nodes = True
    nt = m.node_tree
    b = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    tc = nt.nodes.new("ShaderNodeTexCoord")
    co = tc.outputs["Object"]
    layers = {
        "grass": _layer(nt, "aerial_grass_rock", 4.0, co, tint=(0.85, 1.25, 0.55)),
        "sand": _layer(nt, "coast_sand_01", 2.0, co),
        "rock": _layer(nt, "aerial_rocks_02", 3.0, co),
        "dark": _layer(nt, "brown_mud_leaves_01", 2.5, co),
        "road": _layer(nt, "aerial_ground_rock", 1.5, co),
    }
    masks = {}
    for k in ("sand", "rock", "dark", "road"):
        a = nt.nodes.new("ShaderNodeAttribute")
        a.attribute_name = k
        masks[k] = a.outputs["Fac"]
    col, rough, nor = layers["grass"]
    if rough is None:
        v = nt.nodes.new("ShaderNodeValue")
        v.outputs[0].default_value = 0.8
        rough = v.outputs[0]
    for k in ("dark", "rock", "road", "sand"):
        c2, r2, n2 = layers[k]
        col = _mix(nt, masks[k], col, c2)
        if r2 is not None:
            rough = _mixf(nt, masks[k], rough, r2)
        nor = _mix(nt, masks[k], nor, n2)
    # a touch warmer and livelier than the photos, like a painted map
    hs = nt.nodes.new("ShaderNodeHueSaturation")
    hs.inputs["Saturation"].default_value = 1.3
    hs.inputs["Value"].default_value = 1.05
    nt.links.new(col, hs.inputs["Color"])
    nt.links.new(hs.outputs["Color"], b.inputs["Base Color"])
    nt.links.new(rough, b.inputs["Roughness"])
    nm = nt.nodes.new("ShaderNodeNormalMap")
    nm.inputs["Strength"].default_value = 0.6
    nt.links.new(nor, nm.inputs["Color"])
    nt.links.new(nm.outputs["Normal"], b.inputs["Normal"])
    return m


def _sea_material():
    """Turquoise over the shallows, deep blue further out, white surf at the shore, glossy small waves."""
    m = bpy.data.materials.new("kingdom_sea")
    m.use_nodes = True
    nt = m.node_tree
    b = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    depth = nt.nodes.new("ShaderNodeAttribute")
    depth.attribute_name = "depth"
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    els = ramp.color_ramp.elements
    els[0].position, els[0].color = 0.0, (0.32, 0.78, 0.72, 1)
    els[1].position, els[1].color = 1.0, (0.012, 0.11, 0.26, 1)
    e = els.new(0.18)
    e.color = (0.07, 0.48, 0.62, 1)
    e = els.new(0.5)
    e.color = (0.025, 0.24, 0.45, 1)
    nt.links.new(depth.outputs["Fac"], ramp.inputs["Fac"])
    # surf: broken white where the water is shallowest
    tc = nt.nodes.new("ShaderNodeTexCoord")
    noise = nt.nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 3.0
    noise.inputs["Detail"].default_value = 6.0
    nt.links.new(tc.outputs["Object"], noise.inputs["Vector"])
    shore = nt.nodes.new("ShaderNodeMapRange")
    shore.inputs["From Min"].default_value = 0.0
    shore.inputs["From Max"].default_value = 0.05
    shore.inputs["To Min"].default_value = 1.0
    shore.inputs["To Max"].default_value = 0.0
    nt.links.new(depth.outputs["Fac"], shore.inputs["Value"])
    foam = nt.nodes.new("ShaderNodeMath")
    foam.operation = "MULTIPLY"
    nt.links.new(shore.outputs["Result"], foam.inputs[0])
    nt.links.new(noise.outputs["Fac"], foam.inputs[1])
    cut = nt.nodes.new("ShaderNodeMapRange")
    cut.inputs["From Min"].default_value = 0.25
    cut.inputs["From Max"].default_value = 0.45
    nt.links.new(foam.outputs[0], cut.inputs["Value"])
    col = _mix(nt, cut.outputs["Result"], ramp.outputs["Color"], nt.nodes.new("ShaderNodeRGB").outputs[0])
    white = [n for n in nt.nodes if n.type == "RGB"][0]
    white.outputs[0].default_value = (0.92, 0.96, 0.97, 1)
    nt.links.new(col, b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = 0.12
    # waves: two noise layers as a bump
    waves = nt.nodes.new("ShaderNodeTexNoise")
    waves.inputs["Scale"].default_value = 9.0
    waves.inputs["Detail"].default_value = 4.0
    wm = nt.nodes.new("ShaderNodeMapping")
    wm.inputs["Scale"].default_value = (1.0, 2.2, 1.0)
    nt.links.new(tc.outputs["Object"], wm.inputs["Vector"])
    nt.links.new(wm.outputs["Vector"], waves.inputs["Vector"])
    bump = nt.nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.18
    bump.inputs["Distance"].default_value = 0.05
    nt.links.new(waves.outputs["Fac"], bump.inputs["Height"])
    nt.links.new(bump.outputs["Normal"], b.inputs["Normal"])
    return m


def _cloud_material(color, density=1.0, scale=1.6):
    """A soft cloud on a flat sheet: white (or purple Fog) where the noise is thick, see-through elsewhere."""
    m = bpy.data.materials.new("cloud")
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    tc = nt.nodes.new("ShaderNodeTexCoord")
    noise = nt.nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = scale
    noise.inputs["Detail"].default_value = 8.0
    noise.inputs["Roughness"].default_value = 0.6
    nt.links.new(tc.outputs["Object"], noise.inputs["Vector"])
    # fade towards the sheet's edges
    grad = nt.nodes.new("ShaderNodeTexGradient")
    grad.gradient_type = "SPHERICAL"
    nt.links.new(tc.outputs["Generated"], grad.inputs["Vector"])
    gm = nt.nodes.new("ShaderNodeMapping")
    gm.inputs["Location"].default_value = (-0.5, -0.5, -0.5)
    gm.inputs["Scale"].default_value = (2.0, 2.0, 2.0)
    nt.links.new(tc.outputs["Generated"], gm.inputs["Vector"])
    nt.links.new(gm.outputs["Vector"], grad.inputs["Vector"])
    mul = nt.nodes.new("ShaderNodeMath")
    mul.operation = "MULTIPLY"
    nt.links.new(noise.outputs["Fac"], mul.inputs[0])
    nt.links.new(grad.outputs["Fac"], mul.inputs[1])
    alpha = nt.nodes.new("ShaderNodeMapRange")
    alpha.inputs["From Min"].default_value = 0.12
    alpha.inputs["From Max"].default_value = 0.4
    alpha.inputs["To Max"].default_value = density
    nt.links.new(mul.outputs[0], alpha.inputs["Value"])
    diff = nt.nodes.new("ShaderNodeBsdfDiffuse")
    diff.inputs["Color"].default_value = tuple(color) + (1,)
    tr = nt.nodes.new("ShaderNodeBsdfTransparent")
    mix = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(alpha.outputs["Result"], mix.inputs["Fac"])
    nt.links.new(tr.outputs[0], mix.inputs[1])
    nt.links.new(diff.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs["Surface"])
    return m


def _cloud(x, y, z, sx, sy, material):
    bpy.ops.mesh.primitive_plane_add(size=1, location=(x, y, z))
    o = bpy.context.active_object
    o.scale = (sx, sy, 1)
    o.data.materials.append(material)
    o.visible_shadow = False
    return o


def _ground(noise):
    bpy.ops.mesh.primitive_grid_add(x_subdivisions=420, y_subdivisions=280, size=1, location=(0, 0, 0))
    g = bpy.context.active_object
    g.scale = (42, 28, 1)
    bpy.ops.object.transform_apply(scale=True)
    me = g.data
    n = len(me.vertices)
    sand, rock, dark, road = [0.0] * n, [0.0] * n, [0.0] * n, [0.0] * n
    for i, v in enumerate(me.vertices):
        x, y = v.co.x, v.co.y
        z = w._wheight(x, y, noise)
        # fine relief on land
        z += 0.06 * math.sin(x * 3.1 + noise[2]) * math.cos(y * 2.7) * max(0.0, min(1.0, z * 2))
        v.co.z = z if z > 0.02 else min(z, -0.25)
        key, kind = w._island_of(x, y)
        sand[i] = 1.0 - _smooth(0.06, 0.3, z)
        dark[i] = 1.0 if kind == "dark" else 0.0
        road[i] = (1.0 - _smooth(0.12, 0.3, _road_dist(x, y))) * (1.0 - sand[i])
    me.update()
    # steep slopes show bare rock
    for i, v in enumerate(me.vertices):
        rock[i] = _smooth(0.25, 0.55, 1.0 - v.normal.z) * (1.0 - sand[i])
    for name, values in (("sand", sand), ("rock", rock), ("dark", dark), ("road", road)):
        _attr(me, name, values)
    me.materials.append(_ground_material())
    for p in me.polygons:
        p.use_smooth = True
    return g


def _sea(noise):
    bpy.ops.mesh.primitive_grid_add(x_subdivisions=300, y_subdivisions=200, size=1, location=(0, 0, -0.02))
    s = bpy.context.active_object
    s.scale = (70, 50, 1)
    bpy.ops.object.transform_apply(scale=True)
    me = s.data
    depth = []
    for v in me.vertices:
        z = w._wheight(v.co.x, v.co.y, noise)
        depth.append(max(0.0, min(1.0, -z / 0.9)))
    _attr(me, "depth", depth)
    me.materials.append(_sea_material())
    return s


def _bridge(x0, y0, x1, y1):
    """A wooden bridge over the strait: planks on a gentle arch, posts and rails."""
    wood = ph.material("brown_planks_05", size=1.0, res="1k")
    n = int(math.hypot(x1 - x0, y1 - y0) / 0.16)
    ang = math.atan2(y1 - y0, x1 - x0)
    for k in range(n + 1):
        f = k / n
        x, y = x0 + (x1 - x0) * f, y0 + (y1 - y0) * f
        z = 0.22 + 0.22 * math.sin(f * math.pi)
        t.rbox((0.14, 0.62, 0.035), (x, y, z), wood, bevel=0.01, rot=(0, 0, ang))
        if k % 4 == 0:
            for side in (-1, 1):
                px, py = x - side * 0.3 * math.sin(ang), y + side * 0.3 * math.cos(ang)
                t.cyl(0.025, 0.7, (px, py, z - 0.1), wood, verts=12)


def render_worldmap(out, res=(2400, 1350), samples=64):
    t.reset(soft=True)
    sc = bpy.context.scene
    sc.view_settings.view_transform = "AgX"
    sc.view_settings.look = "AgX - Punchy"
    sc.view_settings.exposure = 0.25
    sc.cycles.samples = samples
    sc.cycles.max_bounces = 6
    ph.sky(SKY, strength=1.0, rotation=200)
    t.light("SUN", (0, 0, 20), 3.2, color=(1.0, 0.93, 0.8), size=2.0, rot=(math.radians(40), 0, math.radians(-35)))
    rng = random.Random("kingdom")
    noise = (1.3, 4.1, 2.2)
    ground = _ground(noise)
    _sea(noise)

    # rocks ringing the shores
    for key, ((cx, cy), (rx, ry), _, kind) in w.ISLANDS.items():
        for k in range(int((rx + ry) * 1.6)):
            a = rng.uniform(0, math.tau)
            x, y = cx + rx * 0.95 * math.cos(a), cy + ry * 0.95 * math.sin(a)
            ph.put(rng.choice(["rock_moss_set_01", "rock_moss_set_02", "namaqualand_boulder_03", "namaqualand_boulder_05", "boulder_01"]),
                   (x, y, -0.1), rot_z=rng.uniform(0, 360), length=rng.uniform(0.35, 0.7))

    # ships on the sea routes, one moored at the pier of the routes
    route_rng = random.Random("routes")
    for a, b in w.SEA_ROUTES:
        mx, my = w._sea_route(a, b, route_rng)
        bx, by = mx + route_rng.uniform(-0.5, 0.5), my + route_rng.uniform(-0.4, 0.4)
        if all(w._wheight(bx + dx, by + dy, noise) < -0.3 for dx, dy in ((0, 0), (1.2, 0), (-1.2, 0), (0, 1.0), (0, -1.0))):
            ph.put("ship_pinnace", (bx, by, -0.03), rot_z=route_rng.uniform(0, 360), length=2.0)
    ph.put("dutch_ship_medium", (14.8, -1.6, -0.05), rot_z=200, length=2.6)
    ph.put("dutch_ship_large_01", (-16.2, 1.2, -0.05), rot_z=-30, length=3.0)
    pier = ph.put("modular_wooden_pier", (-5.9, -6.7, -0.15), rot_z=60, length=2.0)
    ph.put("ship_pinnace", (-4.3, -7.8, -0.03), rot_z=150, length=1.8)
    _bridge(-6.0, -2.2, -6.4, -1.0)

    # the places and the acts
    for key, ((x, y), build, scale) in w.PLACES.items():
        bld = w.Building()
        build(bld, random.Random(key))
        bld.place((x, y, w._wheight(x, y, noise) - 0.05), scale)
    village = w.Building()
    w.house(village, -3.0, 2.5)
    w.house(village, 3.0, 2.0)
    village.place((-9.2, -2.2, w._wheight(-9.2, -2.2, noise) - 0.05), 0.3)
    for (x, y), act in zip(w.WORLD_ACTS, w.ACTS):
        bld = w.Building()
        act[1](bld, random.Random(str((x, y))))
        bld.place((x, y, w._wheight(x, y, noise) - 0.05), 0.24, rot_z=rng.uniform(-15, 15))
    busy = [w.PLACES[k][0] for k in w.PLACES] + list(w.WORLD_ACTS) + [(-9.2, -2.2)]

    # woods: broad trees and pines on the green islands, dead trees and firs on the dark ones
    green_trees = [("island_tree_01", 1.3), ("island_tree_02", 1.2), ("fir_sapling", 1.1), ("searsia_lucida", 0.8)]
    dark_trees = [("fir_sapling", 1.1), ("dead_quiver_trunk", 0.8), ("fir_sapling", 0.9), ("quiver_tree_02", 0.9)]
    bushes = [("shrub_02", 0.3), ("wild_rooibos_bush", 0.25), ("searsia_lucida", 0.35), ("fern_02", 0.18)]
    placed = 0
    for _ in range(9000):
        x, y = rng.uniform(-18, 18), rng.uniform(-10, 11)
        key, kind = w._island_of(x, y)
        if key is None:
            continue
        z = w._wheight(x, y, noise)
        if z < 0.22 or _road_dist(x, y) < 0.3 or any((x - px) ** 2 + (y - py) ** 2 < 1.6 for px, py in busy):
            continue
        r = rng.random()
        if r < 0.62:
            name, h = rng.choice(dark_trees if kind == "dark" else green_trees)
            ph.put(name, (x, y, z - 0.03), rot_z=rng.uniform(0, 360), height=h * rng.uniform(0.8, 1.2), length=h)
        else:
            name, h = rng.choice(bushes)
            ph.put(name, (x, y, z - 0.02), rot_z=rng.uniform(0, 360), height=h * rng.uniform(0.8, 1.2), length=h * 1.5)
        placed += 1
    print("trees and bushes", placed, flush=True)

    sc.render.resolution_x, sc.render.resolution_y = res
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    sc.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 34.0
    tilt = math.radians(52)
    cam.location = (0.3, -30 * math.cos(tilt) + 1.4, 30 * math.sin(tilt))
    cam.rotation_euler = (math.radians(90) - tilt, 0, 0)
    cam.data.clip_end = 200
    sc.camera = cam
    bpy.context.view_layer.update()

    def at(x, y, lift=0.8):
        p = world_to_camera_view(sc, cam, Vector((x, y, max(0.0, w._wheight(x, y, noise)) + lift)))
        return {"x": round(p.x, 4), "y": round(1 - p.y, 4)}
    places = {key: at(*w.PLACES[key][0], lift=1.6) for key in w.PLACES}
    acts = [dict(at(x, y), name=a[2]) for (x, y), a in zip(w.WORLD_ACTS, w.ACTS)]
    del ground, pier
    t.render(out)
    return {"acts": acts, "places": places}
