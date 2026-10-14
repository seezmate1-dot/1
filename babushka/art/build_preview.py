"""Builds the stylized 3D dacha in Blender and renders two preview shots.

Run (Blender as a Python module, no GPU needed):
    python art/build_preview.py [--quick]
Output: art/renders/exterior_night.png, art/renders/gameplay_view.png, art/dacha_preview.blend

The look target is "cozy stylized" (GDD 12): soft bevelled shapes, painted textures,
warm windows against a blue night. Models built here also serve as the art reference
for the Unreal assets.
"""
import math
import os
import sys

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
TEX = os.path.join(HERE, "textures")
OUT = os.path.join(HERE, "renders")
QUICK = "--quick" in sys.argv

# ---------------------------------------------------------------- scene setup
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.engine = "CYCLES"
scene.cycles.device = "CPU"
scene.cycles.samples = 24 if QUICK else 96
scene.cycles.use_denoising = True
scene.cycles.max_bounces = 4
scene.render.resolution_x = 960 if QUICK else 1600
scene.render.resolution_y = 540 if QUICK else 900
scene.view_settings.exposure = 1.1

world = bpy.data.worlds.new("Night")
scene.world = world
world.use_nodes = True
bg = world.node_tree.nodes.get("Background")
bg.inputs["Color"].default_value = (0.03, 0.05, 0.11, 1)
bg.inputs["Strength"].default_value = 1.0

collections = {}


def coll(name):
    if name not in collections:
        c = bpy.data.collections.new(name)
        scene.collection.children.link(c)
        collections[name] = c
    return collections[name]


# ---------------------------------------------------------------- materials
def _principled(mat):
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    bsdf = next((n for n in nodes if n.type == "BSDF_PRINCIPLED"), None)
    if bsdf is None:
        bsdf = nodes.new("ShaderNodeBsdfPrincipled")
        out = next(n for n in nodes if n.type == "OUTPUT_MATERIAL")
        mat.node_tree.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return bsdf


_mats = {}


def flat(name, color, rough=0.75, emit=0.0, metallic=0.0, transmission=0.0):
    key = ("flat", name)
    if key in _mats:
        return _mats[key]
    mat = bpy.data.materials.new(name)
    bsdf = _principled(mat)
    bsdf.inputs["Base Color"].default_value = (*color, 1)
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Metallic"].default_value = metallic
    if transmission:
        bsdf.inputs["Transmission Weight"].default_value = transmission
        bsdf.inputs["IOR"].default_value = 1.45
    if emit:
        bsdf.inputs["Emission Color"].default_value = (*color, 1)
        bsdf.inputs["Emission Strength"].default_value = emit
    _mats[key] = mat
    return mat


def textured(name, png, meters=1.0, rough=0.8, uv=False):
    """Image texture. World-space box projection so scaled boxes keep the same texel size."""
    key = ("tex", name)
    if key in _mats:
        return _mats[key]
    mat = bpy.data.materials.new(name)
    bsdf = _principled(mat)
    bsdf.inputs["Roughness"].default_value = rough
    nt = mat.node_tree
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(os.path.join(TEX, png + ".png"))
    if not uv:
        tex.projection = "BOX"
        tex.projection_blend = 0.2
        geo = nt.nodes.new("ShaderNodeNewGeometry")
        mapping = nt.nodes.new("ShaderNodeMapping")
        mapping.inputs["Scale"].default_value = (1 / meters, 1 / meters, 1 / meters)
        nt.links.new(geo.outputs["Position"], mapping.inputs["Vector"])
        nt.links.new(mapping.outputs["Vector"], tex.inputs["Vector"])
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    _mats[key] = mat
    return mat


# ---------------------------------------------------------------- shapes
def _finish(obj, mat, collection, bevel=0.0, smooth=False, subsurf=0):
    for c in obj.users_collection:
        c.objects.unlink(obj)
    coll(collection).objects.link(obj)
    if mat:
        obj.data.materials.append(mat)
    if bevel:
        mod = obj.modifiers.new("Bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 3
        mod.limit_method = "ANGLE"
    if subsurf:
        mod = obj.modifiers.new("Subsurf", "SUBSURF")
        mod.levels = subsurf
        mod.render_levels = subsurf
    if smooth:
        obj.data.shade_smooth()
    return obj


def box(name, center, size, mat, collection="House", bevel=0.02, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=center, rotation=rot)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return _finish(obj, mat, collection, bevel=bevel)


def cyl(name, center, radius, depth, mat, collection="Props", rot=(0, 0, 0), verts=32, bevel=0.0):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth, location=center, rotation=rot)
    obj = bpy.context.active_object
    obj.name = name
    return _finish(obj, mat, collection, bevel=bevel, smooth=True)


def ball(name, center, radius, mat, collection="Props", scale=(1, 1, 1), segments=32):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=segments // 2, radius=radius, location=center)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return _finish(obj, mat, collection, smooth=True)


def cone(name, center, r1, r2, depth, mat, collection="Props", rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cone_add(vertices=32, radius1=r1, radius2=r2, depth=depth, location=center, rotation=rot)
    obj = bpy.context.active_object
    obj.name = name
    return _finish(obj, mat, collection, smooth=True)


def plane(name, center, size, mat, collection="Props", rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_plane_add(size=1, location=center, rotation=rot)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = (size[0], size[1], 1)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return _finish(obj, mat, collection)


def light(name, kind, location, energy, color, size=0.1, rot=(0, 0, 0), spot_deg=60, blend=0.3, collection="Lights"):
    data = bpy.data.lights.new(name, type=kind)
    data.energy = energy
    data.color = color
    if kind in ("POINT", "SPOT"):
        data.shadow_soft_size = size
    if kind == "AREA":
        data.size = size
    if kind == "SPOT":
        data.spot_size = math.radians(spot_deg)
        data.spot_blend = blend
    obj = bpy.data.objects.new(name, data)
    obj.location = location
    obj.rotation_euler = rot
    coll(collection).objects.link(obj)
    return obj


def look_at(obj, target):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


# ---------------------------------------------------------------- materials used
M = {
    "siding": textured("Siding", "T_Siding", 1.6),
    "wallpaper": textured("Wallpaper", "T_Wallpaper", 1.2),
    "floor": textured("Floor", "T_FloorPaint", 2.0, rough=0.55),
    "slate": textured("Slate", "T_Slate", 2.0),
    "fence": textured("FenceWood", "T_Fence", 1.4),
    "logs": textured("Logs", "T_Logs", 1.4),
    "grass": textured("Grass", "T_Grass", 3.0, rough=0.95),
    "soil": textured("Soil", "T_Soil", 1.5, rough=1.0),
    "boards": textured("Boards", "T_Boards", 1.2),
    "oilcloth": textured("Oilcloth", "T_Oilcloth", 0.6, rough=0.35),
    "carpet": textured("Carpet", "T_Carpet", uv=True, rough=0.95),
    "scarf": textured("Headscarf", "T_Headscarf", 0.25),
    "wood_dark": flat("WoodDark", (0.18, 0.09, 0.05), 0.5),
    "wood_light": flat("WoodLight", (0.45, 0.28, 0.15), 0.6),
    "white": flat("Whitewash", (0.86, 0.84, 0.78), 0.9),
    "window": flat("WindowGlow", (1.0, 0.62, 0.28), 0.4, emit=6.0),
    "frame": flat("WindowFrame", (0.92, 0.9, 0.84), 0.6),
    "blanket": flat("Blanket", (0.3, 0.42, 0.62), 0.9),
    "pillow": flat("Pillow", (0.93, 0.91, 0.86), 0.9),
    "glass": flat("Crystal", (0.85, 0.92, 0.95), 0.05, transmission=1.0),
    "tv": flat("TVCase", (0.16, 0.1, 0.07), 0.4),
    "screen": flat("TVScreen", (0.45, 0.7, 1.0), 0.2, emit=3.0),
    "pie": flat("PieCrust", (0.85, 0.5, 0.16), 0.5),
    "plate": flat("Plate", (0.95, 0.95, 0.92), 0.2),
    "cabbage": flat("Cabbage", (0.42, 0.65, 0.32), 0.7),
    "leaves": flat("Leaves", (0.16, 0.36, 0.14), 0.9),
    "apple": flat("Apple", (0.75, 0.15, 0.1), 0.4),
    "skin": flat("Skin", (0.93, 0.7, 0.55), 0.6),
    "dress": flat("Dress", (0.2, 0.25, 0.5), 0.85),
    "apron": flat("Apron", (0.92, 0.88, 0.75), 0.85),
    "shoe": flat("FeltBoot", (0.25, 0.22, 0.2), 0.95),
    "metal": flat("Metal", (0.6, 0.6, 0.62), 0.3, metallic=1.0),
    "black": flat("Black", (0.02, 0.02, 0.02), 0.4),
    "shirt": flat("KidShirt", (0.95, 0.75, 0.2), 0.8),
    "cap": flat("KidCap", (0.2, 0.45, 0.8), 0.6),
    "pants": flat("KidPants", (0.25, 0.3, 0.35), 0.8),
    "sneaker": flat("Sneaker", (0.95, 0.95, 0.95), 0.5),
    "red": flat("Red", (0.75, 0.12, 0.1), 0.5),
    "moonglow": flat("Moon", (0.85, 0.9, 1.0), 0.5, emit=8.0),
    "chicken": flat("Chicken", (0.95, 0.93, 0.88), 0.9),
}

# ---------------------------------------------------------------- house: 10 x 7 m, walls 2.6 m
H, T = 2.6, 0.15


def wall(name, x0, y0, x1, y1, inner=True, outer=False):
    """Axis-aligned wall segment. Outer walls get siding outside and wallpaper inside."""
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    sx, sy = max(abs(x1 - x0), T), max(abs(y1 - y0), T)
    if outer:
        box(name + "_out", (cx, cy, H / 2), (sx, sy, H), M["siding"], bevel=0.01)
        # wallpaper skin on the inner face
        if x0 == x1:
            off = 0.09 if x0 == 0 else -0.09
            box(name + "_in", (cx + off, cy, H / 2), (0.03, sy - 0.3, H - 0.02), M["wallpaper"], "Interior", bevel=0)
        else:
            off = 0.09 if y0 == 0 else -0.09
            box(name + "_in", (cx, cy + off, H / 2), (sx - 0.3, 0.03, H - 0.02), M["wallpaper"], "Interior", bevel=0)
    else:
        box(name, (cx, cy, H / 2), (sx, sy, H), M["wallpaper"], "Interior", bevel=0.01)


def wall_with_doors(name, axis, fixed, start, end, doors, outer=False, door_w=0.9):
    """Wall from start to end along axis ('x' or 'y') with door gaps centred at doors."""
    cuts = sorted(doors)
    pos = start
    for i, d in enumerate(cuts + [None]):
        stop = end if d is None else d - door_w / 2
        if stop - pos > 0.05:
            if axis == "x":
                wall("%s_%d" % (name, i), pos, fixed, stop, fixed, outer=outer)
            else:
                wall("%s_%d" % (name, i), fixed, pos, fixed, stop, outer=outer)
        if d is not None:
            pos = d + door_w / 2
            # lintel above the door
            if axis == "x":
                box("%s_lintel_%d" % (name, i), (d, fixed, H - 0.25), (door_w, T, 0.5), M["siding"] if outer else M["wallpaper"], "Interior", bevel=0)
            else:
                box("%s_lintel_%d" % (name, i), (fixed, d, H - 0.25), (T, door_w, 0.5), M["wallpaper"], "Interior", bevel=0)


# floor and plinth
box("Floor", (5, 3.5, -0.05), (10, 7, 0.1), M["floor"], "House", bevel=0)
box("Plinth", (5, 3.5, -0.35), (10.4, 7.4, 0.5), flat("Plinth", (0.35, 0.33, 0.3), 0.9), "House", bevel=0.03)
# outer walls: front (south) has the entry door at x = 8.3
wall_with_doors("WallSouth", "x", 0, 0, 10, [8.3], outer=True)
wall("WallNorth", 0, 7, 10, 7, outer=True)
wall("WallWest", 0, 0, 0, 7, outer=True)
wall("WallEast", 10, 0, 10, 7, outer=True)
# interior walls
wall_with_doors("WallMid", "x", 3.5, 0, 10, [2.0, 5.5, 8.5])
wall_with_doors("WallKitchenKids", "y", 4.0, 3.5, 7, [])
wall_with_doors("WallKidsGranny", "y", 7.0, 3.5, 7, [])
wall_with_doors("WallLivingEntry", "y", 6.0, 0, 3.5, [1.8])

# windows: glowing panes with white frames (outside only)
for i, (x, y, side) in enumerate([(2.0, 0, "S"), (4.6, 0, "S"), (2.0, 7, "N"), (5.5, 7, "N"), (8.5, 7, "N"), (0, 5.2, "W"), (10, 5.2, "E"), (10, 1.6, "E")]):
    if side in "SN":
        off = -0.09 if side == "S" else 0.09
        box("WindowGlow_%d" % i, (x, y + off, 1.5), (1.0, 0.02, 1.0), M["window"], "House", bevel=0)
        box("WindowFrame_%d" % i, (x, y + off * 1.3, 1.5), (1.2, 0.04, 1.2), M["frame"], "House", bevel=0.01)
        box("WindowCross_%d" % i, (x, y + off * 1.6, 1.5), (0.06, 0.03, 1.0), M["frame"], "House", bevel=0)
        box("Shutter_L_%d" % i, (x - 0.78, y + off * 1.5, 1.5), (0.32, 0.04, 1.2), flat("Shutter", (0.3, 0.45, 0.6), 0.7), "House")
        box("Shutter_R_%d" % i, (x + 0.78, y + off * 1.5, 1.5), (0.32, 0.04, 1.2), flat("Shutter", (0.3, 0.45, 0.6), 0.7), "House")
    else:
        off = -0.09 if side == "W" else 0.09
        box("WindowGlow_%d" % i, (x + off, y, 1.5), (0.02, 1.0, 1.0), M["window"], "House", bevel=0)
        box("WindowFrame_%d" % i, (x + off * 1.3, y, 1.5), (0.04, 1.2, 1.2), M["frame"], "House", bevel=0.01)

# gable roof (hidden in the gameplay view)
for side in (-1, 1):
    pitch = math.radians(32)
    box("Roof_%d" % side, (5, 3.5 + side * 1.95, H + 1.05), (11.2, 4.4, 0.12), M["slate"], "Roof", bevel=0.01, rot=(side * -pitch, 0, 0))
box("RoofRidge", (5, 3.5, H + 2.22), (11.2, 0.25, 0.12), M["wood_dark"], "Roof")
def gable(name, x, width, rise):
    """Triangular wall under the roof ends."""
    import bmesh
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    pts = [(-0.06, 0.0, 0.0), (-0.06, width, 0.0), (-0.06, width / 2, rise), (0.06, 0.0, 0.0), (0.06, width, 0.0), (0.06, width / 2, rise)]
    v = [bm.verts.new(p) for p in pts]
    for face in ((0, 1, 2), (5, 4, 3), (0, 3, 4, 1), (1, 4, 5, 2), (2, 5, 3, 0)):
        bm.faces.new([v[i] for i in face])
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    obj.location = (x, 0, H)
    coll("Roof").objects.link(obj)
    obj.data.materials.append(M["siding"])


for x in (0.0, 10.0):
    gable("Gable_%s" % x, x, 7.0, 2.15)
box("Chimney", (2.0, 5.0, H + 2.0), (0.6, 0.6, 1.6), flat("Brick", (0.55, 0.25, 0.18), 0.9), "Roof")

# porch with steps and a warm lamp
box("Porch", (8.3, -0.7, -0.2), (2.2, 1.2, 0.2), M["boards"], "House")
box("Step", (8.3, -1.45, -0.42), (1.6, 0.5, 0.15), M["boards"], "House")
for x in (7.3, 9.3):
    cyl("PorchPost_%s" % x, (x, -1.2, 1.0), 0.06, 2.2, M["wood_light"], "House")
box("PorchRoof", (8.3, -0.75, 2.25), (2.6, 1.6, 0.08), M["slate"], "House", rot=(math.radians(-12), 0, 0))
light("PorchLamp", "POINT", (8.3, -0.6, 2.0), 120, (1.0, 0.7, 0.4), size=0.08)
ball("PorchBulb", (8.3, -0.6, 2.05), 0.07, flat("Bulb", (1.0, 0.8, 0.5), 0.3, emit=20), "House")

# ---------------------------------------------------------------- interior furniture
# Kitchen (x 0..4, y 3.5..7): russian stove, table with oilcloth and pies, shelves with jars
box("Stove", (0.85, 6.1, 1.0), (1.5, 1.5, 2.0), M["white"], "Interior", bevel=0.06)
box("StoveMouth", (0.85, 5.33, 0.75), (0.5, 0.05, 0.4), M["black"], "Interior", bevel=0.02)
box("TableTop", (2.6, 5.0, 0.76), (1.3, 0.85, 0.06), M["oilcloth"], "Interior", bevel=0.01)
for dx in (-0.55, 0.55):
    for dy in (-0.33, 0.33):
        cyl("TableLeg", (2.6 + dx, 5.0 + dy, 0.37), 0.04, 0.74, M["wood_dark"], "Interior")
cyl("PiePlate", (2.6, 5.0, 0.8), 0.28, 0.03, M["plate"], "Interior")
for i, (dx, dy) in enumerate([(-0.1, -0.05), (0.1, -0.06), (0.0, 0.1), (-0.08, 0.12), (0.12, 0.1)]):
    ball("Pie_%d" % i, (2.6 + dx, 5.0 + dy, 0.85 + (0.04 if i > 2 else 0)), 0.09, M["pie"], "Interior", scale=(1.3, 0.8, 0.55))
for i, x in enumerate((2.0, 3.2)):
    box("Stool_%d" % i, (x, 4.35, 0.45), (0.35, 0.35, 0.05), M["wood_light"], "Interior")
    cyl("StoolLeg_%d" % i, (x, 4.35, 0.22), 0.04, 0.44, M["wood_light"], "Interior")
box("Shelf", (3.6, 6.85, 1.4), (0.7, 0.25, 0.04), M["wood_light"], "Interior")
for i in range(4):
    cyl("Jar_%d" % i, (3.35 + i * 0.17, 6.85, 1.52), 0.06, 0.2, M["glass"], "Interior")
    cyl("JarFill_%d" % i, (3.35 + i * 0.17, 6.85, 1.48), 0.05, 0.11, M["red"] if i % 2 else M["cabbage"], "Interior")
light("KitchenLamp", "POINT", (2.5, 5.2, 2.3), 140, (1.0, 0.72, 0.45), size=0.15)

# Kids room (x 4..7): two beds, wall carpet, the kid's ball self lives in the kitchen
for i, x in enumerate((4.6, 6.4)):
    box("Bed_%d" % i, (x, 5.6, 0.25), (0.85, 1.9, 0.4), M["wood_dark"], "Interior", bevel=0.03)
    box("Blanket_%d" % i, (x, 5.45, 0.5), (0.82, 1.4, 0.14), M["blanket"], "Interior", bevel=0.05)
    box("Pillow_%d" % i, (x, 6.3, 0.52), (0.6, 0.35, 0.14), M["pillow"], "Interior", bevel=0.06)
plane("WallCarpet_Kids", (5.5, 6.9, 1.4), (1.8, 1.3), M["carpet"], "Interior", rot=(math.radians(90), 0, 0))
light("KidsLamp", "POINT", (5.5, 5.3, 2.3), 90, (1.0, 0.72, 0.45), size=0.15)

# Grandma's room (x 7..10): sideboard with crystal and the phone, bed, big carpet
box("Sideboard", (8.0, 6.75, 0.9), (1.6, 0.45, 1.8), M["wood_dark"], "Interior", bevel=0.03)
box("SideboardGlass", (8.0, 6.52, 1.3), (1.4, 0.02, 0.7), M["glass"], "Interior", bevel=0)
for i in range(6):
    cone("Crystal_%d" % i, (7.45 + i * 0.22, 6.75, 1.1), 0.07, 0.04, 0.16, M["glass"], "Interior")
box("Phone", (8.0, 6.7, 1.86), (0.25, 0.18, 0.1), M["red"], "Interior", bevel=0.03)
box("GrannyBed", (9.4, 5.2, 0.3), (1.0, 2.0, 0.5), M["wood_dark"], "Interior", bevel=0.03)
box("GrannyQuilt", (9.4, 5.0, 0.62), (0.98, 1.5, 0.16), flat("Quilt", (0.65, 0.2, 0.3), 0.9), "Interior", bevel=0.06)
for i, z in enumerate((0.75, 0.9, 1.02)):
    box("PillowStack_%d" % i, (9.4, 5.95, z), (0.55 - i * 0.1, 0.4 - i * 0.07, 0.14), M["pillow"], "Interior", bevel=0.06)
plane("WallCarpet_Granny", (9.95, 5.2, 1.5), (2.0, 1.4), M["carpet"], "Interior", rot=(math.radians(90), 0, math.radians(90)))
light("GrannyLamp", "POINT", (8.6, 5.2, 2.3), 70, (1.0, 0.68, 0.4), size=0.15)

# Living room (x 0..6, y 0..3.5): TV on a stand, armchair, floor rug
box("TVStand", (0.6, 1.8, 0.3), (0.6, 0.9, 0.6), M["wood_dark"], "Interior", bevel=0.02)
box("TV", (0.62, 1.8, 0.88), (0.6, 0.75, 0.56), M["tv"], "Interior", bevel=0.05)
box("TVScreen", (0.93, 1.75, 0.9), (0.02, 0.5, 0.38), M["screen"], "Interior", bevel=0)
for a in (-25, 25):
    cyl("Antenna", (0.62, 1.8 + a / 100, 1.35), 0.01, 0.45, M["metal"], "Interior", rot=(math.radians(a), 0, 0))
light("TVGlow", "AREA", (1.1, 1.75, 0.9), 25, (0.5, 0.7, 1.0), size=0.5, rot=(0, math.radians(90), 0))
box("Armchair", (2.6, 1.8, 0.35), (0.85, 0.85, 0.5), flat("ArmchairFabric", (0.45, 0.22, 0.18), 0.95), "Interior", bevel=0.08)
box("ArmchairBack", (2.95, 1.8, 0.8), (0.2, 0.85, 0.7), flat("ArmchairFabric", (0.45, 0.22, 0.18), 0.95), "Interior", bevel=0.08)
plane("FloorRug", (2.4, 1.6, 0.012), (2.4, 1.8), M["carpet"], "Interior")
plane("WallCarpet_Living", (3.0, 3.38, 1.5), (2.2, 1.5), M["carpet"], "Interior", rot=(math.radians(90), 0, math.radians(180)))
light("LivingLamp", "POINT", (3.0, 1.8, 2.3), 80, (1.0, 0.7, 0.42), size=0.15)

# Entry (x 6..10, y 0..3.5): wardrobe (hiding spot), sneakers, felt boots, coats
box("Wardrobe", (9.55, 2.4, 1.0), (0.7, 1.2, 2.0), M["wood_light"], "Interior", bevel=0.03)
box("WardrobeDoorLine", (9.19, 2.4, 1.0), (0.02, 0.02, 1.9), M["wood_dark"], "Interior", bevel=0)
for i, dy in enumerate((-0.1, 0.12)):
    box("Sneaker_%d" % i, (7.0, 0.5 + dy, 0.06), (0.28, 0.11, 0.12), M["sneaker"], "Interior", bevel=0.04)
    box("SneakerStripe_%d" % i, (7.0, 0.5 + dy, 0.09), (0.2, 0.115, 0.03), M["red"], "Interior", bevel=0)
for i, dy in enumerate((-0.12, 0.12)):
    cyl("FeltBoot_%d" % i, (6.6, 1.3 + dy, 0.22), 0.08, 0.42, M["shoe"], "Interior")
box("CoatHooks", (7.2, 3.4, 1.7), (1.2, 0.05, 0.08), M["wood_dark"], "Interior")
box("Coat", (7.0, 3.3, 1.25), (0.45, 0.12, 0.8), flat("Coat", (0.32, 0.3, 0.22), 0.95), "Interior", bevel=0.05)
light("EntryLamp", "POINT", (8.0, 1.7, 2.3), 60, (1.0, 0.7, 0.42), size=0.15)


# ---------------------------------------------------------------- characters
def attach(parts, name, origin, collection):
    """Parent parts (built in world space) to an empty at origin, keeping them in place."""
    root = bpy.data.objects.new(name, None)
    root.location = origin
    coll(collection).objects.link(root)
    bpy.context.view_layer.update()
    inverse = root.matrix_world.inverted()
    for p in parts:
        p.parent = root
        p.matrix_parent_inverse = inverse
    return root


def grandma(origin, facing_deg, collection="Characters"):
    """Round grandma: dress, apron, headscarf with polka dots, round glasses, flashlight."""
    ox, oy, oz = origin
    parts = []
    parts.append(cone("Gran_Dress", (ox, oy, oz + 0.5), 0.42, 0.26, 1.0, M["dress"], collection))
    parts.append(cone("Gran_Apron", (ox, oy, oz + 0.45), 0.43, 0.27, 0.75, M["apron"], collection))
    parts.append(ball("Gran_Body", (ox, oy, oz + 1.05), 0.3, M["dress"], collection, scale=(1.05, 0.9, 0.9)))
    parts.append(ball("Gran_Head", (ox, oy, oz + 1.42), 0.2, M["skin"], collection))
    parts.append(ball("Gran_Scarf", (ox - 0.02, oy, oz + 1.47), 0.225, M["scarf"], collection, scale=(1.0, 1.0, 0.95)))
    parts.append(cone("Gran_ScarfKnot", (ox, oy, oz + 1.25), 0.12, 0.02, 0.18, M["scarf"], collection))
    for side in (-1, 1):
        parts.append(cyl("Gran_Glass", (ox + 0.19, oy + side * 0.075, oz + 1.43), 0.055, 0.015, M["metal"], collection, rot=(0, math.radians(90), 0)))
        parts.append(ball("Gran_Cheek", (ox + 0.16, oy + side * 0.12, oz + 1.37), 0.04, flat("Cheek", (0.95, 0.5, 0.45), 0.6), collection))
        parts.append(cyl("Gran_Arm", (ox + 0.12, oy + side * 0.32, oz + 0.98), 0.07, 0.45, M["dress"], collection, rot=(0, math.radians(70), side * math.radians(10))))
        parts.append(ball("Gran_Boot", (ox + 0.05, oy + side * 0.14, oz + 0.05), 0.09, M["shoe"], collection, scale=(1.4, 1.0, 0.7)))
    parts.append(cyl("Gran_Flashlight", (ox + 0.42, oy + 0.33, oz + 0.92), 0.04, 0.24, M["metal"], collection, rot=(0, math.radians(80), 0)))
    root = attach(parts, "Grandma", origin, collection)
    root.rotation_euler = (0, 0, math.radians(facing_deg))
    # Flashlight: the visible cone of sight (GDD 12)
    tip = Vector((ox + 0.55, oy + 0.33, oz + 0.95))
    rot = Vector((math.cos(math.radians(facing_deg)), math.sin(math.radians(facing_deg)), -0.45))
    beam = light("Gran_Flashlight_Beam", "SPOT", (0, 0, 0), 450, (1.0, 0.86, 0.6), size=0.02, spot_deg=55, blend=0.25)
    pivot = Vector((ox, oy, oz))
    local = tip - pivot
    ang = math.radians(facing_deg)
    beam.location = pivot + Vector((local.x * math.cos(ang) - local.y * math.sin(ang), local.x * math.sin(ang) + local.y * math.cos(ang), local.z))
    look_at(beam, beam.location + rot)
    return root


def kid(origin, facing_deg, crouch=False, collection="Characters"):
    """Bean-shaped grandchild: big head, blue cap, yellow shirt."""
    ox, oy, oz = origin
    h = 0.8 if crouch else 1.0
    parts = [
        cyl("Kid_Legs", (ox, oy, oz + 0.2 * h), 0.13, 0.4 * h, M["pants"], collection),
        ball("Kid_Body", (ox, oy, oz + 0.55 * h), 0.2, M["shirt"], collection, scale=(1, 0.9, 1.15)),
        ball("Kid_Head", (ox, oy, oz + 0.95 * h), 0.2, M["skin"], collection),
        ball("Kid_Cap", (ox, oy, oz + 1.02 * h), 0.205, M["cap"], collection, scale=(1, 1, 0.75)),
        box("Kid_Visor", (ox + 0.2, oy, oz + 1.03 * h), (0.22, 0.26, 0.03), M["cap"], collection, bevel=0.01),
    ]
    for side in (-1, 1):
        parts.append(ball("Kid_Eye", (ox + 0.18, oy + side * 0.07, oz + 0.97 * h), 0.03, M["black"], collection))
        parts.append(box("Kid_Sneaker", (ox + 0.05, oy + side * 0.08, oz + 0.04), (0.2, 0.09, 0.08), M["sneaker"], collection, bevel=0.03))
    root = attach(parts, "Kid", origin, collection)
    root.rotation_euler = (0, 0, math.radians(facing_deg))
    return root


def kolobok(origin, collection="Characters"):
    """Fullness level 5: the kid is a ball with a cap (GDD 5)."""
    ox, oy, oz = origin
    ball("Kolobok_Body", (ox, oy, oz + 0.42), 0.42, M["shirt"], collection)
    ball("Kolobok_Face", (ox + 0.12, oy, oz + 0.55), 0.3, M["skin"], collection, scale=(0.9, 1, 0.9))
    ball("Kolobok_Cap", (ox, oy, oz + 0.78), 0.24, M["cap"], collection, scale=(1, 1, 0.6))
    for side in (-1, 1):
        ball("Kolobok_Eye", (ox + 0.38, oy + side * 0.09, oz + 0.6), 0.035, M["black"], collection)
        ball("Kolobok_Cheek", (ox + 0.36, oy + side * 0.2, oz + 0.5), 0.06, flat("Cheek", (0.95, 0.5, 0.45), 0.6), collection)


# ---------------------------------------------------------------- yard (fence 30 x 22 m around the house)
YX0, YX1, YY0, YY1 = -6, 24, -10, 12
box("Ground", ((YX0 + YX1) / 2, (YY0 + YY1) / 2, -0.62), (60, 50, 0.2), M["grass"], "Yard", bevel=0)
# path from the gate to the porch
for i in range(7):
    box("PathBoard_%d" % i, (8.3, -2.2 - i * 1.0, -0.5), (1.0, 0.8, 0.06), M["boards"], "Yard", bevel=0.01)


def fence_line(x0, y0, x1, y1, skip=None):
    length = math.hypot(x1 - x0, y1 - y0)
    n = int(length / 0.22)
    for i in range(n):
        t = (i + 0.5) / n
        x, y = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
        if skip and skip[0] <= (x if y0 == y1 else y) <= skip[1]:
            continue
        hgt = 1.35 + (0.08 if i % 2 else 0)
        box("Picket", (x, y, -0.5 + hgt / 2), (0.16 if y0 == y1 else 0.04, 0.04 if y0 == y1 else 0.16, hgt), M["fence"], "Yard", bevel=0.01)
    for z in (-0.1, 0.55):
        cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
        box("Rail", (cx, cy, z), (abs(x1 - x0) or 0.05, abs(y1 - y0) or 0.05, 0.06), M["wood_dark"], "Yard", bevel=0)


fence_line(YX0, YY0, YX1, YY0, skip=(7.6, 9.0))   # front with the gate
fence_line(YX0, YY1, YX1, YY1)
fence_line(YX0, YY0, YX0, YY1, skip=(-4.6, -4.0))  # the gap thin kids crawl under
fence_line(YX1, YY0, YX1, YY1)
box("Gate", (8.3, YY0, 0.15), (1.4, 0.06, 1.3), M["wood_light"], "Yard", bevel=0.02)
box("GateBoardLow", (YX0, -4.3, -0.32), (0.04, 0.6, 0.3), M["fence"], "Yard")

# garden beds with cabbages, west of the house
for row in range(4):
    y = -3.0 - row * 1.4
    box("Bed_%d" % row, (1.5, y, -0.45), (7.0, 0.8, 0.25), M["soil"], "Yard", bevel=0.05)
    for i in range(9):
        ball("Cabbage", (-1.6 + i * 0.75, y, -0.25), 0.2, M["cabbage"], "Yard", scale=(1, 1, 0.8), segments=16)

# bathhouse of logs, east
box("Banya", (17.5, 6.5, 0.7), (3.4, 3.0, 2.4), M["logs"], "Yard", bevel=0.04)
for side in (-1, 1):
    box("BanyaRoof_%d" % side, (17.5, 6.5 + side * 0.85, 2.25), (3.9, 1.9, 0.1), M["slate"], "Yard", rot=(side * math.radians(-28), 0, 0))
box("BanyaDoor", (17.5, 4.98, 0.4), (0.7, 0.05, 1.6), M["wood_dark"], "Yard")
cyl("BanyaPipe", (18.4, 7.0, 2.8), 0.08, 1.2, M["metal"], "Yard")
ball("Steam", (18.4, 7.0, 3.6), 0.3, flat("SteamPuff", (0.8, 0.85, 0.9), 1.0, emit=0.3), "Yard", scale=(1.4, 1.4, 1))

# chicken coop, north-east
box("Coop", (17.5, 10.0, 0.3), (2.6, 1.8, 1.6), M["fence"], "Yard", bevel=0.03)
box("CoopRoof", (17.5, 10.0, 1.2), (3.0, 2.2, 0.1), M["slate"], "Yard", rot=(math.radians(10), 0, 0))
box("CoopRamp", (16.0, 9.4, -0.3), (1.2, 0.35, 0.05), M["boards"], "Yard", rot=(0, math.radians(-20), 0))
for i, (x, y) in enumerate([(15.0, 8.6), (15.6, 8.2), (14.6, 7.9)]):
    ball("Chicken_%d" % i, (x, y, -0.32), 0.17, M["chicken"], "Yard", scale=(1.3, 1, 1))
    ball("ChickenHead_%d" % i, (x + 0.18, y, -0.12), 0.08, M["chicken"], "Yard")
    cone("Comb_%d" % i, (x + 0.18, y, -0.03), 0.04, 0.0, 0.07, M["red"], "Yard")

# outhouse with a heart cut-out (every dacha has one)
box("Outhouse", (21.5, 10.5, 0.5), (1.2, 1.2, 2.2), M["fence"], "Yard", bevel=0.02)
box("OuthouseRoof", (21.5, 10.5, 1.7), (1.5, 1.5, 0.08), M["slate"], "Yard", rot=(math.radians(-12), 0, 0))
ball("OuthouseHeart", (21.5, 9.88, 1.1), 0.1, M["window"], "Yard", scale=(1, 0.2, 1))

# apple trees
for i, (x, y) in enumerate([(-3.5, 8.5), (13.0, -6.5), (20.0, -2.5), (-3.0, 3.0)]):
    cyl("Trunk_%d" % i, (x, y, 0.5), 0.15, 2.2, M["wood_dark"], "Yard")
    for j, (dx, dy, dz, r) in enumerate([(0, 0, 2.2, 1.1), (0.6, 0.3, 1.9, 0.8), (-0.5, -0.4, 2.0, 0.85), (0.1, -0.6, 2.6, 0.7)]):
        ball("Crown_%d_%d" % (i, j), (x + dx, y + dy, dz), r, M["leaves"], "Yard", segments=20)
    for k in range(6):
        ang = k * 1.05
        ball("Apple_%d_%d" % (i, k), (x + math.cos(ang) * 1.0, y + math.sin(ang) * 1.0, 1.8 + (k % 3) * 0.25), 0.08, M["apple"], "Yard", segments=12)

# moon and the night light
ball("MoonDisc", (-30, 60, 30), 3.0, M["moonglow"], "Yard")
moon = light("MoonLight", "SUN", (0, 0, 20), 0.6, (0.6, 0.7, 1.0), rot=(math.radians(50), 0, math.radians(200)))
moon.data.angle = math.radians(2)

# characters: grandma on the porch, a kid sneaking by the garden, the kolobok in the kitchen
gran_out = grandma((8.4, -1.1, -0.35), -125)
kid_garden = kid((-0.6, -6.9, -0.5), 180, crouch=True)
kolobok((3.3, 5.6, 0.0))
kid_hiding = kid((8.9, 2.0, 0.0), 160)

os.makedirs(OUT, exist_ok=True)


def render(name, cam_loc, target, lens=35, hide=()):
    cam_data = bpy.data.cameras.new(name)
    cam_data.lens = lens
    cam = bpy.data.objects.new(name, cam_data)
    cam.location = cam_loc
    scene.collection.objects.link(cam)
    look_at(cam, target)
    scene.camera = cam
    for c in collections.values():
        c.hide_render = c.name in hide
    scene.render.filepath = os.path.join(OUT, name + ".png")
    bpy.ops.render.render(write_still=True)
    print("rendered", scene.render.filepath)


# 1. Exterior: the dacha at night, grandma on the porch with her flashlight.
render("exterior_night", (-7.5, -19.0, 8.0), (6.5, 0.0, 0.6), lens=30)

# 2. Gameplay camera: roof off and walls cut at 1.6 m, 3/4 view from above like the game (GDD 12).
CUT = 1.6
for obj in list(bpy.data.objects):
    n = obj.name
    if n.startswith(("WindowGlow", "WindowFrame", "WindowCross", "Shutter", "PorchRoof", "PorchPost", "PorchBulb")) or "lintel" in n:
        obj.hide_render = True
    elif n.startswith("Wall") and not n.startswith("WallCarpet"):
        obj.scale.z = CUT / H
        obj.location.z = obj.location.z * CUT / H
    elif n.startswith("WallCarpet"):
        obj.location.z = 0.95
        obj.scale.y = 0.8  # planes are rotated upright: local Y is the height
gran_out.location = (5.4, 2.6, 0.0)
gran_out.rotation_euler = (0, 0, math.radians(20))
for obj in bpy.data.objects:
    if obj.name == "Gran_Flashlight_Beam":
        obj.location = (5.9, 2.75, 0.95)
        look_at(obj, (8.2, 1.9, 0.0))
render("gameplay_view", (5.0, -6.2, 11.5), (5.0, 3.4, 0.0), lens=30, hide=("Roof",))

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "dacha_preview.blend"))
print("saved blend")
