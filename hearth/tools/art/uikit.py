"""The interface as polished game art, rendered in Blender instead of drawn flat: round buttons of
glossy enamel in a polished gold ring, plates and windows of dark varnished wood with a gold edge,
a red close button, a green plus, a white bubble, a level badge, a gear; lit by a photo studio's
light (Poly Haven HDRI), so the gold reflects and the enamel shines, with soft shadows under them.
Every picture is rendered at twice its size and scaled down, so the edges are smooth.
Written to Resources/Art/UI; frames are drawn as nine slices by the game (their corners keep their
shape: see BORDERS, which the code uses: Pill and Window through ArtLibrary.DrawSliced at half
size on screen, the plank buttons, panels and fields through the GUI skin, UiSkin.cs).
"""
import math
import os

import bpy
from PIL import Image, ImageFilter

import phkit as ph
import toy as t

OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
                   "unity", "LastHearth", "Assets", "LastHearth", "Resources", "Art", "UI")
STUDIO = "studio_small_08"
GOLD = (1.0, 0.6, 0.16)
BLUE = (0.015, 0.07, 0.36)
# nine-slice borders in pixels of the written pictures (GameRunner.Hud.cs, UiSkin.cs use the same)
BORDERS = {"Pill": 30, "Window": 60, "Button": 14, "Panel": 24, "Field": 10}


# ------------------------------------------------------------------ scene and materials

def _scene(px_w, px_h, units_w):
    """A top-down orthographic studio: units_w scene units across px_w pixels (rendered at 2x)."""
    t.reset(soft=True)
    sc = bpy.context.scene
    sc.view_settings.view_transform = "AgX"
    sc.view_settings.look = "AgX - Punchy"
    sc.view_settings.exposure = 0.3
    sc.cycles.samples = 96
    sc.render.resolution_x, sc.render.resolution_y = px_w * 2, px_h * 2
    ph.sky(STUDIO, strength=1.0, rotation=30)
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    sc.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = units_w if px_w >= px_h else units_w * px_h / px_w
    cam.location = (0, 0, 20)
    sc.camera = cam
    # a key light from the top left and a soft fill: the bevels catch light on one side
    t.light("AREA", (-4, 4, 8), 900, color=(1.0, 0.96, 0.9), size=6, rot=(math.radians(30), math.radians(-25), 0))
    t.light("AREA", (5, -3, 6), 250, color=(0.9, 0.95, 1.0), size=6, rot=(math.radians(-25), math.radians(35), 0))
    return sc


def _principled(name, color, metal=0.0, rough=0.4, coat=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = tuple(color) + (1,)
    b.inputs["Metallic"].default_value = metal
    b.inputs["Roughness"].default_value = rough
    b.inputs["Coat Weight"].default_value = coat
    b.inputs["Coat Roughness"].default_value = 0.05
    return m


def gold():
    return _principled("gold", GOLD, metal=1.0, rough=0.22)


def enamel(color):
    return _principled("enamel", color, rough=0.25, coat=1.0)


def wood(dark=0.45):
    return ph.material("dark_wood", size=1.6, res="2k", tint=(dark, dark * 0.92, dark * 0.85), bump=0.6)


def _render(path, size, shadow=True):
    """Renders at twice the size, scales down (smooth edges) and lays a soft drop shadow under it."""
    tmp = path + ".tmp.png"
    t.render(tmp, transparent=True)
    img = Image.open(tmp).convert("RGBA").resize(size, Image.LANCZOS)
    os.remove(tmp)
    if shadow:
        a = img.getchannel("A").point(lambda v: int(v * 0.55))
        k = max(1, min(size) // 40)
        sh = Image.new("RGBA", size, (20, 10, 4, 0))
        sh.putalpha(a)
        sh = sh.filter(ImageFilter.GaussianBlur(k * 1.5))
        out = Image.new("RGBA", size, (0, 0, 0, 0))
        out.alpha_composite(sh, (0, k))
        out.alpha_composite(img)
        img = out
    img.save(path)
    print("ui", os.path.basename(path), flush=True)


# ------------------------------------------------------------------ shapes

def disc(radius, height, loc, material, dome=0.0, verts=96):
    """A round disc with softly bevelled edges; dome lifts its middle (glossy enamel)."""
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=height, location=loc)
    o = bpy.context.active_object
    bev = o.modifiers.new("bevel", "BEVEL")
    bev.width = min(height * 0.45, radius * 0.3)
    bev.segments = 6
    if dome:
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="DESELECT")
        bpy.ops.object.mode_set(mode="OBJECT")
        top = [f for f in o.data.polygons if f.normal.z > 0.9]
        for f in top:
            f.select = True
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.inset(thickness=radius * 0.02)
        bpy.ops.mesh.subdivide(number_cuts=12)
        bpy.ops.object.mode_set(mode="OBJECT")
        for v in o.data.vertices:
            r = math.hypot(v.co.x, v.co.y) / radius
            if v.co.z > height / 2 - 1e-4:
                v.co.z += dome * max(0.0, 1 - r * r)
    o.data.materials.append(material)
    bpy.ops.object.shade_smooth()
    return o


def ring(radius, thickness, loc, material, flat=0.55):
    """A polished ring: a torus pressed flat."""
    bpy.ops.mesh.primitive_torus_add(major_radius=radius, minor_radius=thickness, major_segments=128, minor_segments=32, location=loc)
    o = bpy.context.active_object
    o.scale = (1, 1, flat)
    o.data.materials.append(material)
    bpy.ops.object.shade_smooth()
    return o


def plate(w, h, depth, radius, loc, material, edge=0.04):
    """A rounded rectangle slab: round corners seen from above, softly bevelled top edges."""
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    o = bpy.context.active_object
    o.scale = (w, h, depth)
    bpy.ops.object.transform_apply(scale=True)
    # round the four vertical edges only (bevel weight), then soften every edge a little
    me = o.data
    attr = me.attributes.new("bevel_weight_edge", "FLOAT", "EDGE")
    for e in me.edges:
        a, b = me.vertices[e.vertices[0]].co, me.vertices[e.vertices[1]].co
        attr.data[e.index].value = 1.0 if abs(a.x - b.x) < 1e-6 and abs(a.y - b.y) < 1e-6 else 0.0
    corners = o.modifiers.new("corners", "BEVEL")
    corners.limit_method = "WEIGHT"
    corners.width = radius
    corners.segments = 16
    if edge > 0:
        soft = o.modifiers.new("soft", "BEVEL")
        soft.limit_method = "ANGLE"
        soft.width = min(edge, depth * 0.45)
        soft.segments = 5
    o.data.materials.append(material)
    bpy.ops.object.shade_smooth()
    return o


def frame(w, h, depth, radius, band, loc, material, edge=0.03):
    """A rounded rectangular rim `band` wide (the gold edge of a plate or window)."""
    outer = plate(w, h, depth, radius, loc, material, edge)
    inner = plate(w - 2 * band, h - 2 * band, depth * 3, max(0.01, radius - band), loc, material, 0.0)
    for m in list(outer.modifiers):
        bpy.context.view_layer.objects.active = outer
        bpy.ops.object.modifier_apply(modifier=m.name)
    for m in list(inner.modifiers):
        bpy.context.view_layer.objects.active = inner
        bpy.ops.object.modifier_apply(modifier=m.name)
    cut = outer.modifiers.new("cut", "BOOLEAN")
    cut.operation = "DIFFERENCE"
    cut.object = inner
    bpy.context.view_layer.objects.active = outer
    bpy.ops.object.modifier_apply(modifier="cut")
    bpy.data.objects.remove(inner, do_unlink=True)
    soft = outer.modifiers.new("soft", "BEVEL")
    soft.limit_method = "ANGLE"
    soft.width = min(edge, band * 0.3, depth * 0.4)
    soft.segments = 4
    return outer


def stud(x, y, z, r, material):
    o = disc(r, r * 0.8, (x, y, z), material, dome=r * 0.5, verts=48)
    return o


# ------------------------------------------------------------------ the pieces

def round_button(path, size=256, color=BLUE):
    _scene(size, size, 2.3)
    disc(0.86, 0.16, (0, 0, 0.1), enamel(color), dome=0.12)
    ring(0.9, 0.12, (0, 0, 0.17), gold())
    _render(path, (size, size))


def badge(path, size=128):
    round_button(path, size)


def dot(path, size=64):
    _scene(size, size, 2.3)
    disc(0.8, 0.2, (0, 0, 0.12), enamel((0.75, 0.05, 0.03)), dome=0.25)
    ring(0.86, 0.1, (0, 0, 0.2), _principled("cream", (0.95, 0.9, 0.82), rough=0.3))
    _render(path, (size, size))


def close_button(path, size=128):
    _scene(size, size, 2.3)
    disc(0.84, 0.16, (0, 0, 0.1), enamel((0.62, 0.06, 0.04)), dome=0.12)
    ring(0.88, 0.11, (0, 0, 0.17), gold())
    white = _principled("white", (1.0, 0.98, 0.94), rough=0.25, coat=0.5)
    for k, a in enumerate((45, -45)):
        p = plate(0.92, 0.22, 0.12, 0.09, (0, 0, 0.36 + 0.01 * k), white, edge=0.05)
        p.rotation_euler = (0, 0, math.radians(a))
    _render(path, (size, size))


def plus(path, size=128):
    _scene(size, size, 2.3)
    disc(0.84, 0.16, (0, 0, 0.1), enamel((0.12, 0.45, 0.08)), dome=0.12)
    ring(0.88, 0.11, (0, 0, 0.17), gold())
    white = _principled("white", (0.96, 0.94, 0.9), rough=0.3, coat=0.5)
    plate(0.95, 0.24, 0.12, 0.1, (0, 0, 0.36), white, edge=0.05)
    plate(0.24, 0.95, 0.12, 0.1, (0, 0, 0.36), white, edge=0.05)
    _render(path, (size, size))


def gear(path, size=128):
    _scene(size, size, 2.3)
    disc(0.86, 0.16, (0, 0, 0.1), enamel(BLUE), dome=0.12)
    ring(0.9, 0.12, (0, 0, 0.17), gold())
    g = gold()
    disc(0.42, 0.14, (0, 0, 0.32), g, verts=64)
    for k in range(8):
        a = k * math.pi / 4
        p = plate(0.2, 0.2, 0.14, 0.04, (0.48 * math.cos(a), 0.48 * math.sin(a), 0.32), g, edge=0.03)
        p.rotation_euler = (0, 0, a)
    disc(0.15, 0.2, (0, 0, 0.36), enamel((0.08, 0.06, 0.05)), verts=48)
    _render(path, (size, size))


def pill(path, size=(256, 72)):
    """A plate for a number or a name: dark varnished wood in a thin gold edge."""
    w, h = 2.56, 0.72
    _scene(*size, w + 0.04)
    plate(w - 0.1, h - 0.1, 0.1, 0.27, (0, 0, 0.06), wood(0.32), edge=0.03)
    frame(w - 0.04, h - 0.04, 0.13, 0.3, 0.06, (0, 0, 0.08), gold(), edge=0.02)
    _render(path, size)


def window(path, size=(512, 384)):
    """A window's board: dark wood inside, a wide carved frame of lighter wood with a gold rim and
    gold studs in the corners."""
    w, h = 5.12, 3.84
    _scene(*size, w + 0.04)
    plate(w - 0.4, h - 0.4, 0.06, 0.18, (0, 0, 0.04), wood(0.22), edge=0.02)
    frame(w - 0.06, h - 0.06, 0.18, 0.32, 0.36, (0, 0, 0.1), wood(0.55), edge=0.05)
    frame(w - 0.62, h - 0.62, 0.2, 0.16, 0.06, (0, 0, 0.12), gold(), edge=0.02)
    frame(w - 0.04, h - 0.04, 0.2, 0.34, 0.05, (0, 0, 0.12), gold(), edge=0.02)
    g = gold()
    for sx in (-1, 1):
        for sy in (-1, 1):
            stud(sx * (w / 2 - 0.2), sy * (h / 2 - 0.2), 0.24, 0.08, g)
    _render(path, size)


def button(path, size=(192, 64), trim=False, light=0.62):
    """A plank button: varnished wood with rounded corners; trim: the chosen one, with a gold edge."""
    w, h = 1.92, 0.64
    _scene(*size, w + 0.04)
    plate(w - 0.08, h - 0.08, 0.14, 0.09, (0, 0, 0.08), wood(light), edge=0.04)
    if trim:
        frame(w - 0.04, h - 0.04, 0.17, 0.1, 0.05, (0, 0, 0.1), gold(), edge=0.02)
    _render(path, size)


def panel(path, size=(256, 256)):
    """A parchment panel in a wooden frame (lists inside windows)."""
    w = 2.56
    _scene(*size, w + 0.04)
    paper = ph.material("beige_wall_001", size=2.5, res="1k", tint=(1.0, 0.9, 0.72), bump=0.2)
    plate(w - 0.3, w - 0.3, 0.04, 0.1, (0, 0, 0.03), paper, edge=0.01)
    frame(w - 0.04, w - 0.04, 0.12, 0.12, 0.16, (0, 0, 0.07), wood(0.5), edge=0.04)
    _render(path, size)


def field(path, size=(128, 40)):
    w, h = 1.28, 0.4
    _scene(*size, w + 0.04)
    paper = ph.material("beige_wall_001", size=2.5, res="1k", tint=(1.0, 0.92, 0.78), bump=0.2)
    plate(w - 0.06, h - 0.06, 0.04, 0.06, (0, 0, 0.03), paper, edge=0.01)
    frame(w - 0.02, h - 0.02, 0.08, 0.08, 0.04, (0, 0, 0.05), wood(0.4), edge=0.02)
    _render(path, size)


def bar(path, size=(512, 112)):
    """The bottom panel under the round buttons: a long varnished board with a gold top edge."""
    w, h = 5.12, 1.12
    _scene(*size, w)
    plate(w * 1.2, h - 0.1, 0.12, 0.0001, (0, -0.05, 0.06), wood(0.4), edge=0.04)
    plate(w * 1.2, 0.08, 0.16, 0.0001, (0, h / 2 - 0.12, 0.09), gold(), edge=0.03)
    _render(path, size)


def bubble(path, size=128):
    """The cloud over a room with something to collect: a white glossy bubble with a little tail."""
    _scene(size, size, 2.3)
    white = _principled("white", (0.97, 0.97, 0.96), rough=0.3, coat=0.6)
    disc(0.86, 0.2, (0, 0.1, 0.12), white, dome=0.2)
    tail = disc(0.22, 0.18, (0, -0.78, 0.1), white, verts=3)
    tail.rotation_euler = (0, 0, math.radians(-90))
    _render(path, (size, size))


def background(path, size=512):
    """Behind everything: a dark castle wall (a photo texture, tiled)."""
    tex = ph.texture_maps("castle_brick_07", "1k")["diff"]
    img = Image.open(tex).convert("RGB").resize((size, size), Image.LANCZOS)
    from PIL import ImageEnhance
    img = ImageEnhance.Brightness(img).enhance(0.45)
    img.save(path)


def render_all(out_dir=OUT):
    os.makedirs(out_dir, exist_ok=True)
    j = lambda n: os.path.join(out_dir, n + ".png")  # noqa: E731
    round_button(j("Round"))
    badge(j("Badge"))
    dot(j("Dot"))
    close_button(j("Close"))
    plus(j("Plus"))
    gear(j("Gear"))
    pill(j("Pill"))
    window(j("Window"))
    button(j("Button"))
    button(j("ButtonHover"), light=0.75)
    button(j("ButtonActive"), light=0.45)
    button(j("ButtonOn"), trim=True, light=0.7)
    panel(j("Panel"))
    field(j("Field"))
    bar(j("Bar"))
    bubble(j("Bubble"))
    background(j("Background"))


if __name__ == "__main__":
    import sys
    only = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if only:
        for n in only:
            globals()[n](os.path.join(OUT, n + ".png"))
    else:
        render_all()
