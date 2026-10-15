"""Poly Haven assets (CC0, free for any use, no account): photoscanned models, PBR textures and sky
HDRIs, downloaded on demand from api.polyhaven.com into a local cache (PH_CACHE, by default
~/.cache/polyhaven). They give the art real-world colours and surfaces: ships, trees, rocks, sand,
grass and stone, lit by a real sky.
"""
import json
import math
import os
import subprocess

import bpy

import toy as t

CACHE = os.environ.get("PH_CACHE", os.path.expanduser("~/.cache/polyhaven"))
API = "https://api.polyhaven.com/files/"


def _fetch(url, dest):
    if os.path.exists(dest) and os.path.getsize(dest) > 0:
        return dest
    os.makedirs(os.path.dirname(dest), exist_ok=True)
    subprocess.run(["curl", "-sSfL", "--retry", "4", "-o", dest + ".part", url], check=True)
    os.replace(dest + ".part", dest)
    return dest


def files(name):
    path = os.path.join(CACHE, name, "files.json")
    _fetch(API + name, path)
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def model_path(name, res="1k"):
    """The glTF of a model (its textures at res), downloaded once."""
    g = files(name)["gltf"]
    res = res if res in g else sorted(g, key=lambda r: int(r[:-1]))[0]
    entry = g[res]["gltf"]
    root = os.path.join(CACHE, name, res)
    for rel, info in entry.get("include", {}).items():
        _fetch(info["url"], os.path.join(root, rel))
    return _fetch(entry["url"], os.path.join(root, name + ".gltf"))


_shared = {}
_dims = {}


def _copy(o, parent):
    c = o.copy()  # the mesh and its materials stay shared: hundreds of trees cost one
    bpy.context.scene.collection.objects.link(c)
    c.parent = parent
    for child in o.children:
        _copy(child, c)


def _simplify(objects, max_faces):
    """Photoscans carry millions of faces (a tree up to 17 million): far more than a picture needs
    and more than memory holds for a forest. Collapse each mesh to at most max_faces."""
    for o in objects:
        if o.type != "MESH" or len(o.data.polygons) <= max_faces:
            continue
        old = o.data
        mod = o.modifiers.new("simplify", "DECIMATE")
        mod.ratio = max_faces / len(old.polygons)
        mod.use_collapse_triangulate = True
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.modifier_apply(modifier=mod.name)
        if old.users == 0:
            bpy.data.meshes.remove(old)


def _measure(pivot):
    """The size of a placed model (x, y, z extents) at scale 1."""
    from mathutils import Vector
    bpy.context.view_layer.update()
    lo, hi = Vector((1e9, 1e9, 1e9)), Vector((-1e9, -1e9, -1e9))
    inv = pivot.matrix_world.inverted()
    for o in pivot.children_recursive:
        if o.type != "MESH":
            continue
        for c in o.bound_box:
            p = inv @ (o.matrix_world @ Vector(c))
            lo = Vector(map(min, lo, p))
            hi = Vector(map(max, hi, p))
    return hi - lo


def put(name, loc, rot_z=0.0, scale=1.0, res="1k", share=True, height=None, length=None, max_faces=60000):
    """Places a model at loc (its origin on the ground), turned rot_z degrees. Its size: scale, or
    height (its top that high, in scene units), or length (its longest side that long)."""
    pivot = bpy.data.objects.new("ph_" + name, None)
    bpy.context.scene.collection.objects.link(pivot)
    key = (name, res)
    first = _shared.get(key) if share else None
    done = False
    if first is not None:
        try:
            for r in first.children:
                _copy(r, pivot)
            done = True
        except ReferenceError:  # the scene was reset since
            pass
    if not done:
        new, roots = t._import(model_path(name, res))
        for r in roots:
            r.parent = pivot
        _simplify(new, max_faces)
        for o in new:
            if o.type == "MESH":
                for p in o.data.polygons:
                    p.use_smooth = True
        _dims[key] = _measure(pivot)
        if share:
            _shared[key] = pivot
    d = _dims.get(key)
    if d is not None and (height or length):
        # both given: the smaller fits (a wide clump of trees keeps within `length`)
        fits = []
        if height:
            fits.append(height / max(1e-6, d.z))
        if length:
            fits.append(length / max(1e-6, d.x, d.y))
        scale = min(fits)
    pivot.location = loc
    pivot.rotation_euler = (0, 0, math.radians(rot_z))
    pivot.scale = scale if isinstance(scale, tuple) else (scale, scale, scale)
    return pivot


def texture_maps(name, res="2k"):
    """The maps of a PBR texture: diffuse, normal (GL), roughness, displacement, as local files."""
    fs = files(name)
    out = {}
    for key, kind in (("Diffuse", "diff"), ("nor_gl", "nor"), ("Rough", "rough"), ("Displacement", "disp"), ("arm", "arm")):
        if key not in fs:
            continue
        by_res = fs[key]
        r = res if res in by_res else sorted(by_res, key=lambda x: int(x[:-1]))[0]
        fmt = "jpg" if "jpg" in by_res[r] else next(iter(by_res[r]))
        url = by_res[r][fmt]["url"]
        out[kind] = _fetch(url, os.path.join(CACHE, name, r, kind + "." + fmt))
    return out


def _image(nt, path, coords, non_color=False, box=True):
    n = nt.nodes.new("ShaderNodeTexImage")
    n.image = bpy.data.images.load(path, check_existing=True)
    if non_color:
        n.image.colorspace_settings.name = "Non-Color"
    if box:
        n.projection = "BOX"
        n.projection_blend = 0.25
    nt.links.new(coords, n.inputs["Vector"])
    return n


def material(name, size=2.0, res="2k", tint=None, bump=1.0, rough_mul=1.0):
    """A material from a Poly Haven texture, projected from the object's own space in metres (one
    tile every `size` metres), so any mesh takes it without UVs. tint multiplies the colour."""
    maps = texture_maps(name, res)
    m = bpy.data.materials.new("ph_" + name)
    m.use_nodes = True
    nt = m.node_tree
    b = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    tc = nt.nodes.new("ShaderNodeTexCoord")
    mp = nt.nodes.new("ShaderNodeMapping")
    mp.inputs["Scale"].default_value = (1 / size, 1 / size, 1 / size)
    nt.links.new(tc.outputs["Object"], mp.inputs["Vector"])
    v = mp.outputs["Vector"]
    col = _image(nt, maps["diff"], v).outputs["Color"]
    if tint:
        mix = nt.nodes.new("ShaderNodeMix")
        mix.data_type = "RGBA"
        mix.blend_type = "MULTIPLY"
        mix.inputs["Factor"].default_value = 1.0
        nt.links.new(col, mix.inputs[6])
        mix.inputs[7].default_value = tuple(tint) + (1,)
        col = mix.outputs[2]
    nt.links.new(col, b.inputs["Base Color"])
    if "rough" in maps:
        r = _image(nt, maps["rough"], v, non_color=True).outputs["Color"]
        if rough_mul != 1.0:
            mm = nt.nodes.new("ShaderNodeMath")
            mm.operation = "MULTIPLY"
            mm.inputs[1].default_value = rough_mul
            nt.links.new(r, mm.inputs[0])
            r = mm.outputs[0]
        nt.links.new(r, b.inputs["Roughness"])
    if "nor" in maps and bump > 0:
        nm = nt.nodes.new("ShaderNodeNormalMap")
        nm.inputs["Strength"].default_value = bump
        nt.links.new(_image(nt, maps["nor"], v, non_color=True).outputs["Color"], nm.inputs["Color"])
        nt.links.new(nm.outputs["Normal"], b.inputs["Normal"])
    return m


def sky(name, strength=1.0, res="2k", rotation=0.0):
    """Lights the scene with a real sky (an HDRI), turned `rotation` degrees."""
    fs = files(name)["hdri"]
    r = res if res in fs else sorted(fs, key=lambda x: int(x[:-1]))[0]
    path = _fetch(fs[r]["hdr"]["url"], os.path.join(CACHE, name, r, name + ".hdr"))
    w = bpy.context.scene.world
    w.use_nodes = True
    nt = w.node_tree
    nt.nodes.clear()
    env = nt.nodes.new("ShaderNodeTexEnvironment")
    env.image = bpy.data.images.load(path, check_existing=True)
    mp = nt.nodes.new("ShaderNodeMapping")
    mp.inputs["Rotation"].default_value = (0, 0, math.radians(rotation))
    tc = nt.nodes.new("ShaderNodeTexCoord")
    nt.links.new(tc.outputs["Generated"], mp.inputs["Vector"])
    nt.links.new(mp.outputs["Vector"], env.inputs["Vector"])
    bg = nt.nodes.new("ShaderNodeBackground")
    bg.inputs["Strength"].default_value = strength
    out = nt.nodes.new("ShaderNodeOutputWorld")
    nt.links.new(env.outputs["Color"], bg.inputs["Color"])
    nt.links.new(bg.outputs["Background"], out.inputs["Surface"])
