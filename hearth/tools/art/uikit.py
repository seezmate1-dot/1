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
from mathutils import Vector
from PIL import Image, ImageFilter

import phkit as ph
import toy as t

OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
                   "unity", "LastHearth", "Assets", "LastHearth", "Resources", "Art", "UI")
STUDIO = "studio_small_08"
GOLD = (1.0, 0.6, 0.16)
BLUE = (0.015, 0.07, 0.36)
RED = (0.6, 0.025, 0.012)
GREEN = (0.06, 0.36, 0.03)
# nine-slice borders in pixels of the written pictures (GameRunner.Hud.cs, UiSkin.cs use the same)
BORDERS = {"Pill": 30, "Window": 60, "Button": 14, "Panel": 24, "Field": 10}


# ------------------------------------------------------------------ scene and materials

def _scene(px_w, px_h, units_w):
    """A top-down orthographic studio: units_w scene units across px_w pixels (rendered at 2x)."""
    t.reset(soft=True)
    sc = bpy.context.scene
    # Khronos PBR Neutral keeps the colours of the materials as they are (AgX turned the gold to a
    # pale brass and the enamel grey): a product shot's colours, like the polished game pieces
    sc.view_settings.view_transform = "Khronos PBR Neutral"
    sc.view_settings.look = "None"
    sc.view_settings.exposure = 0.35
    sc.cycles.samples = 96
    sc.render.resolution_x, sc.render.resolution_y = px_w * 2, px_h * 2
    ph.sky(STUDIO, strength=1.0, rotation=30)
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    sc.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = units_w if px_w >= px_h else units_w * px_h / px_w
    cam.location = (0, 0, 20)
    sc.camera = cam
    # a key light from the top left and a soft fill: the bevels catch light on one side. Both stand
    # high and wide of the picture, aimed at its middle, so no flat varnished board (a window, the
    # bottom panel) mirrors them straight up into the camera as a milky patch
    for loc, energy, color in (((-7, 7, 14), 2700, (1.0, 0.96, 0.9)), ((7.5, -4.5, 9), 560, (0.9, 0.95, 1.0))):
        aim = Vector(loc) * -1
        t.light("AREA", loc, energy, color=color, size=4, rot=aim.to_track_quat("-Z", "Y").to_euler())
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
    return _principled("gold", GOLD, metal=1.0, rough=0.26)


def enamel(color):
    return _principled("enamel", color, rough=0.25, coat=1.0)


def wood(dark=0.45, varnish=0.7, along=False):
    """Dark wood under a glossy varnish (the clear coat shines over the grain); along: the grain
    runs across the picture (planks), not up it."""
    m = ph.material("dark_wood", size=1.6, res="2k", tint=(dark, dark * 0.9, dark * 0.8), bump=0.6)
    if along:
        mp = next(n for n in m.node_tree.nodes if n.type == "MAPPING")
        mp.inputs["Rotation"].default_value = (0, 0, math.radians(90))
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Coat Weight"].default_value = varnish
    b.inputs["Coat Roughness"].default_value = 0.12
    return m


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


def fillet(poly, radius, steps=14):
    """Rounds every corner of a polygon (x, y points, counter-clockwise) with an arc: radius is one
    number or one per corner; works for outer and inner (concave) corners alike."""
    out = []
    n = len(poly)
    for i in range(n):
        r = radius[i] if isinstance(radius, (list, tuple)) else radius
        px, py = poly[i]
        ux, uy = poly[i - 1][0] - px, poly[i - 1][1] - py
        vx, vy = poly[(i + 1) % n][0] - px, poly[(i + 1) % n][1] - py
        lu, lv = math.hypot(ux, uy), math.hypot(vx, vy)
        ux, uy, vx, vy = ux / lu, uy / lu, vx / lv, vy / lv
        ang = math.acos(max(-1.0, min(1.0, ux * vx + uy * vy)))
        if r <= 0 or ang > math.pi - 1e-3:
            out.append((px, py))
            continue
        d = r / math.tan(ang / 2)
        bx, by = ux + vx, uy + vy
        lb = math.hypot(bx, by)
        cx, cy = px + bx / lb * r / math.sin(ang / 2), py + by / lb * r / math.sin(ang / 2)
        a1 = math.atan2(py + uy * d - cy, px + ux * d - cx)
        a2 = math.atan2(py + vy * d - cy, px + vx * d - cx)
        da = (a2 - a1 + math.pi) % (2 * math.pi) - math.pi
        for k in range(steps + 1):
            a = a1 + da * k / steps
            out.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return out


def _resample(pts, n):
    """n points evenly spaced along a closed outline."""
    seg = [math.dist(pts[i], pts[(i + 1) % len(pts)]) for i in range(len(pts))]
    total = sum(seg)
    out, i, acc = [], 0, 0.0
    for k in range(n):
        s = total * k / n
        while acc + seg[i] < s:
            acc += seg[i]
            i += 1
        f = (s - acc) / seg[i] if seg[i] > 0 else 0
        a, b = pts[i], pts[(i + 1) % len(pts)]
        out.append((a[0] + (b[0] - a[0]) * f, a[1] + (b[1] - a[1]) * f))
    return out


def puffy(outline, height, z, material, rim=0.05, dome=0.0, hole=0.0, n=320, rings=12, rot=0.0):
    """A glossy cushion of any outline (closed, counter-clockwise, scene units round the origin): its
    side rounds over into the top in a soft rim `rim` wide (no hard edge to catch a dark line),
    the top domes up by `dome`; hole: the radius of a round hole through the middle (a gear).
    One seamless surface, so glyphs made of crossing bars have no seam where they cross."""
    c, s = math.cos(rot), math.sin(rot)
    pts = [(x * c - y * s, x * s + y * c) for x, y in _resample(outline, n)]
    normals = []
    for i in range(n):
        a, b = pts[i - 1], pts[(i + 1) % n]
        tx, ty = b[0] - a[0], b[1] - a[1]
        lt = math.hypot(tx, ty)
        normals.append((ty / lt, -tx / lt))

    def inset(d, zz):
        return [(p[0] - nx * d, p[1] - ny * d, zz) for p, (nx, ny) in zip(pts, normals)]

    top = z + height
    loops = [inset(0, z)]
    S = 7
    for k in range(S + 1):
        phi = k / S * math.pi / 2
        loops.append(inset(rim * (1 - math.cos(phi)), top - rim + rim * math.sin(phi)))
    edge = loops[-1]
    if hole:
        ang = [math.atan2(p[1], p[0]) for p in edge]
        for k in range(1, rings + 1):
            f = k / rings
            loops.append([(p[0] * (1 - f) + (hole + rim) * math.cos(a) * f,
                           p[1] * (1 - f) + (hole + rim) * math.sin(a) * f, top) for p, a in zip(edge, ang)])
        for k in range(S - 1, -1, -1):
            phi = k / S * math.pi / 2
            r = hole + rim * (1 - math.cos(phi))
            loops.append([(r * math.cos(a), r * math.sin(a), top - rim + rim * math.sin(phi)) for a in ang])
        loops.append([(hole * math.cos(a), hole * math.sin(a), z) for a in ang])
    else:
        # towards the middle a domed top rounds off into a circle, so a point of the outline (the
        # bubble's tail) leaves no ridge across it; a flat top keeps the outline (the glyphs)
        r0 = min(math.hypot(p[0], p[1]) for p in edge)
        for k in range(1, rings):
            f = 1 - k / rings
            g = f * f if dome else 1.0
            zz = top + dome * (1 - f * f) ** 2
            loop = []
            for p in edge:
                a = math.atan2(p[1], p[0])
                x = p[0] * g + r0 * math.cos(a) * (1 - g)
                y = p[1] * g + r0 * math.sin(a) * (1 - g)
                loop.append((x * f, y * f, zz))
            loops.append(loop)
    verts = [v for loop in loops for v in loop]
    faces = []
    for j in range(len(loops) - 1):
        for i in range(n):
            i2 = (i + 1) % n
            faces.append((j * n + i, j * n + i2, (j + 1) * n + i2, (j + 1) * n + i))
    if not hole:
        cidx = len(verts)
        verts.append((0.0, 0.0, top + dome))
        last = (len(loops) - 1) * n
        for i in range(n):
            faces.append((last + i, last + (i + 1) % n, cidx))
    me = bpy.data.meshes.new("puffy")
    me.from_pydata(verts, [], faces)
    me.update()
    for p in me.polygons:
        p.use_smooth = True
    o = bpy.data.objects.new("puffy", me)
    bpy.context.scene.collection.objects.link(o)
    o.data.materials.append(material)
    return o


def plus_outline(arm, half, inner=0.06):
    """A plus: bars `arm` wide reaching `half` from the middle, round ends, softened inner corners."""
    a, L = arm / 2, half
    poly = [(L, -a), (L, a), (a, a), (a, L), (-a, L), (-a, a), (-L, a), (-L, -a), (-a, -a), (-a, -L), (a, -L), (a, -a)]
    ro = a * 0.92
    return fillet(poly, [ro, ro, inner] * 4)


# ------------------------------------------------------------------ the pieces

def round_button(path, size=256, color=BLUE):
    _scene(size, size, 2.3)
    disc(0.86, 0.16, (0, 0, 0.1), enamel(color), dome=0.12)
    ring(0.9, 0.12, (0, 0, 0.17), gold())
    _render(path, (size, size))


def badge(path, size=128):
    round_button(path, size)


def dot(path, size=64):
    """The red dot of something waiting: a domed drop of red enamel with a white mark, in gold."""
    _scene(size, size, 2.3)
    disc(0.84, 0.2, (0, 0, 0.1), enamel(RED), dome=0.28)
    ring(0.88, 0.12, (0, 0, 0.18), gold())
    white = glyph_white()
    puffy(fillet([(-0.1, -0.05), (0.1, -0.05), (0.13, 0.55), (-0.13, 0.55)], 0.08), 0.1, 0.36, white, rim=0.05)
    disc(0.12, 0.12, (0, -0.34, 0.4), white, dome=0.05, verts=48)
    _render(path, (size, size))


def _round(color, ring_color=None):
    """The base of the round buttons: a domed enamel disc in a polished gold ring."""
    disc(0.84, 0.16, (0, 0, 0.1), enamel(color), dome=0.12)
    ring(0.88, 0.11, (0, 0, 0.17), ring_color or gold())


def glyph_white():
    return _principled("white", (0.93, 0.92, 0.9), rough=0.22, coat=0.6)


def close_button(path, size=128):
    """Red enamel, a white cross: one seamless glossy glyph."""
    _scene(size, size, 2.3)
    _round(RED)
    puffy(plus_outline(0.27, 0.5), 0.12, 0.3, glyph_white(), rim=0.06, rot=math.radians(45))
    _render(path, (size, size))


def plus(path, size=128):
    _scene(size, size, 2.3)
    _round(GREEN)
    puffy(plus_outline(0.28, 0.52), 0.12, 0.3, glyph_white(), rim=0.06)
    _render(path, (size, size))


def gear_outline(teeth=8, root=0.4, tip=0.56, n=512):
    """A cog: flat-topped teeth with soft flanks (a smoothed square wave round a circle)."""
    k = 2.6
    pts = []
    for i in range(n):
        a = 2 * math.pi * i / n
        w = 0.5 + 0.5 * math.tanh(k * math.cos(teeth * a)) / math.tanh(k)
        r = root + (tip - root) * w
        pts.append((r * math.cos(a), r * math.sin(a)))
    return pts


def gear(path, size=128):
    """The settings: a white glossy cog on blue enamel."""
    _scene(size, size, 2.3)
    _round(BLUE)
    puffy(gear_outline(), 0.13, 0.3, glyph_white(), rim=0.05, hole=0.15, rot=math.radians(22.5 / 2))
    _render(path, (size, size))


def pill(path, size=(256, 72)):
    """A plate for a number or a name: dark varnished wood in a thin gold edge."""
    w, h = 2.56, 0.72
    _scene(*size, w + 0.04)
    plate(w - 0.1, h - 0.1, 0.1, 0.27, (0, 0, 0.06), wood(0.32, along=True), edge=0.03)
    frame(w - 0.04, h - 0.04, 0.13, 0.3, 0.06, (0, 0, 0.08), gold(), edge=0.02)
    _render(path, size)


def window(path, size=(512, 384)):
    """A window's board: dark wood inside, a wide frame of lighter varnished wood between two gold
    rims, a gold boss with a blue enamel stone on each corner (all within the 60 px slice)."""
    w, h = 5.12, 3.84
    _scene(*size, w + 0.3)  # a margin round the frame, so its shadow fades out inside the picture
    plate(w - 0.4, h - 0.4, 0.06, 0.18, (0, 0, 0.04), wood(0.22), edge=0.02)
    frame(w - 0.06, h - 0.06, 0.18, 0.32, 0.36, (0, 0, 0.1), wood(0.7), edge=0.06)
    frame(w - 0.62, h - 0.62, 0.2, 0.16, 0.06, (0, 0, 0.12), gold(), edge=0.02)
    frame(w - 0.04, h - 0.04, 0.2, 0.34, 0.05, (0, 0, 0.12), gold(), edge=0.02)
    g, stone = gold(), enamel(BLUE)
    for sx in (-1, 1):
        for sy in (-1, 1):
            x, y = sx * (w / 2 - 0.2), sy * (h / 2 - 0.2)
            disc(0.16, 0.12, (x, y, 0.24), g, dome=0.04, verts=64)
            disc(0.09, 0.08, (x, y, 0.32), stone, dome=0.06, verts=64)
    _render(path, size)


def rrect(w, h, r):
    """A rounded rectangle's outline, centred."""
    return fillet([(-w / 2, -h / 2), (w / 2, -h / 2), (w / 2, h / 2), (-w / 2, h / 2)], r)


def button(path, size=(192, 64), trim=False, light=0.62, height=0.12):
    """A plank button: a cushion of varnished wood with round corners (within the skin's 14 px
    border); trim: the chosen one, with a gold edge."""
    w, h = 1.92, 0.64
    _scene(*size, w + 0.04)
    puffy(rrect(w - 0.08, h - 0.08, 0.09), height, 0.02, wood(light, along=True), rim=0.07)
    if trim:
        frame(w - 0.04, h - 0.04, height + 0.05, 0.11, 0.05, (0, 0, 0.04), gold(), edge=0.02)
    _render(path, size)


def panel(path, size=(256, 256)):
    """A parchment panel in a wooden frame (lists inside windows)."""
    w = 2.56
    _scene(*size, w + 0.22)  # room for the shadow (a hard-edged square behind the round corners otherwise)
    paper = ph.material("beige_wall_001", size=2.5, res="1k", tint=(1.0, 0.9, 0.72), bump=0.2)
    plate(w - 0.3, w - 0.3, 0.04, 0.1, (0, 0, 0.03), paper, edge=0.01)
    frame(w - 0.04, w - 0.04, 0.12, 0.12, 0.16, (0, 0, 0.07), wood(0.5), edge=0.04)
    _render(path, size)


def field(path, size=(128, 40)):
    w, h = 1.28, 0.4
    _scene(*size, w + 0.1)
    paper = ph.material("beige_wall_001", size=2.5, res="1k", tint=(1.0, 0.92, 0.78), bump=0.2)
    plate(w - 0.06, h - 0.06, 0.04, 0.06, (0, 0, 0.03), paper, edge=0.01)
    frame(w - 0.02, h - 0.02, 0.08, 0.08, 0.04, (0, 0, 0.05), wood(0.4), edge=0.02)
    _render(path, size)


def bar(path, size=(512, 112)):
    """The bottom panel under the round buttons: two long varnished planks, their grain running
    along them (the game stretches the picture across the screen), under a polished gold rail."""
    w, h = 5.12, 1.12
    _scene(*size, w)
    wd = wood(0.4, along=True)
    for cy, ph_ in ((0.15, 0.5), (-0.33, 0.46)):
        plate(w * 1.3, ph_, 0.12, 0.0001, (0, cy, 0.06), wd, edge=0.035)
    bpy.ops.mesh.primitive_cylinder_add(vertices=48, radius=0.07, depth=w * 1.3, location=(0, h / 2 - 0.11, 0.14),
                                        rotation=(0, math.radians(90), 0))
    rail = bpy.context.active_object
    rail.scale = (0.75, 1, 1)
    rail.data.materials.append(gold())
    bpy.ops.object.shade_smooth()
    _render(path, size)


def bubble_outline(radius=0.96, tail=0.24, width=0.42, n=512):
    """A round cloud with a short pointed tail at the bottom (towards the room)."""
    pts = []
    for i in range(n):
        a = 2 * math.pi * i / n
        d = math.atan2(math.sin(a + math.pi / 2), math.cos(a + math.pi / 2))
        d = math.sqrt(d * d + 0.0025)
        r = radius + tail * max(0.0, 1 - d / width) ** 2
        pts.append((r * math.cos(a), r * math.sin(a)))
    return pts


def bubble(path, size=128):
    """The cloud over a room with something to collect: a white glossy domed bubble with a tail;
    the disc spans 0.08..0.92 of the picture across and 0.04..0.88 down (GameRunner.View.cs
    puts the resource's icon at its middle, 0.46 down)."""
    _scene(size, size, 2.3)
    white = _principled("white", (0.86, 0.88, 0.92), rough=0.2, coat=1.0)
    o = puffy(bubble_outline(), 0.16, 0.05, white, rim=0.1, dome=0.3, rings=16)
    o.location.y = 0.09
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
    button(j("ButtonHover"), light=0.82)
    button(j("ButtonActive"), light=0.4, height=0.08)
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
