"""KayKit models (CC0) by name across the downloaded packs: dungeon, furniture, restaurant, halloween,
medieval hexagon, adventurers. Models are placed at their native scale (a dungeon wall is 4 x 4 units,
a character about 2.4), standing on the floor at a point."""
import math
import os

import bpy

import toy as t

KAYKIT = os.environ.get("KAYKIT", "/home/user/kaykit-game-assets")
_index = None


def _build_index():
    idx = {}
    for root, dirs, files in os.walk(KAYKIT):
        if "/.git" in root or "fbx" in root or "obj" in root.split(os.sep)[-1:]:
            continue
        for f in files:
            if f.endswith((".gltf", ".glb")):
                name = f.split(".")[0]
                path = os.path.join(root, f)
                # the same name in two packs: the dungeon wins, it sets the style
                if name not in idx or "dungeon" in path:
                    idx[name] = path
    return idx


def path(name):
    global _index
    if _index is None:
        _index = _build_index()
    if name not in _index:
        raise KeyError("KayKit model not found: " + name)
    return _index[name]


SCALE = 1.0


def put(name, loc, rot_z=0.0, scale=1.0, tint=None):
    """Places a KayKit model by name at loc (x, y on the floor; z lifts it), turned rot_z degrees."""
    new, roots = t._import(path(name))
    pivot = bpy.data.objects.new("kk_" + name, None)
    bpy.context.scene.collection.objects.link(pivot)
    for r in roots:
        r.parent = pivot
    pivot.location = loc
    pivot.rotation_euler = (0, 0, math.radians(rot_z))
    s = scale * (1.0 if name.startswith(("wall", "floor")) else SCALE)
    pivot.scale = (s, s, s)
    if tint:
        for o in new:
            if o.type == "MESH":
                for i, m in enumerate(o.data.materials):
                    nm = m.copy()
                    b = next(n for n in nm.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
                    hs = nm.node_tree.nodes.new("ShaderNodeHueSaturation")
                    hs.inputs["Hue"].default_value = tint[0]
                    hs.inputs["Saturation"].default_value = tint[1]
                    hs.inputs["Value"].default_value = tint[2]
                    if b.inputs["Base Color"].links:
                        nm.node_tree.links.new(b.inputs["Base Color"].links[0].from_socket, hs.inputs["Color"])
                        nm.node_tree.links.new(hs.outputs["Color"], b.inputs["Base Color"])
                    o.data.materials[i] = nm
    return pivot
