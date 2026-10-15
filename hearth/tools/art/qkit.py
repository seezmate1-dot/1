"""Quaternius models (CC0 / Quaternius Asset License: free in commercial games) by name across the
downloaded packs (fetch_packs.py): Medieval Village MegaKit (walls, floors, beams), Fantasy Props
MegaKit (furniture, tools), Stylized Nature MegaKit (rocks, trees), Universal Base Characters,
Modular Character Outfits - Fantasy, Universal Animation Library 1-2 and the Bestiary.

All the packs share one real-world scale (metres): a wall module is 2 m wide and 3.1 m high, a
floor tile 2 x 2 m, a character about 1.8 m, so a room built from them reads like Hustle Castle's.
"""
import math
import os

import bpy

import toy as t

QUATERNIUS = os.environ.get("QUATERNIUS", "/home/user/quaternius")
_index = None


def _build_index():
    idx = {}
    for root, dirs, files in os.walk(QUATERNIUS):
        for f in files:
            if f.endswith((".gltf", ".glb")):
                name = f.rsplit(".", 1)[0]
                path = os.path.join(root, f)
                # hairstyles come twice (at the origin and rigged to the head bone): the rigged ones follow animations
                if name in idx and "Origin at 0" in path:
                    continue
                idx[name] = path
    return idx


def path(name):
    global _index
    if _index is None:
        _index = _build_index()
    if name not in _index:
        raise KeyError("Quaternius model not found: " + name)
    return _index[name]


def put(name, loc, rot_z=0.0, scale=1.0, tint=None, mul=None):
    """Places a model by name at loc, turned rot_z degrees; tint = (hue, saturation, value) shift,
    mul = a colour its colours are multiplied by."""
    new, roots = t._import(path(name))
    pivot = bpy.data.objects.new("q_" + name, None)
    bpy.context.scene.collection.objects.link(pivot)
    for r in roots:
        r.parent = pivot
    pivot.location = loc
    pivot.rotation_euler = (0, 0, math.radians(rot_z))
    s = scale if isinstance(scale, tuple) else (scale, scale, scale)
    pivot.scale = s
    if tint:
        retint(new, tint)
    if mul:
        multiply(new, mul)
    return pivot


def multiply(objects, color, only=None):
    """Multiplies the base colour of the materials (only those whose name has one of `only`)."""
    for o in objects:
        if o.type != "MESH":
            continue
        for i, m in enumerate(o.data.materials):
            if m is None or (only and not any(k in m.name for k in only)):
                continue
            nm = m.copy()
            nt = nm.node_tree
            bsdf = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
            if bsdf is None:
                continue
            mix = nt.nodes.new("ShaderNodeMix")
            mix.data_type = "RGBA"
            mix.blend_type = "MULTIPLY"
            mix.inputs["Factor"].default_value = 1.0
            if bsdf.inputs["Base Color"].links:
                nt.links.new(bsdf.inputs["Base Color"].links[0].from_socket, mix.inputs[6])
            else:
                mix.inputs[6].default_value = bsdf.inputs["Base Color"].default_value
            mix.inputs[7].default_value = tuple(color) + (1,)
            nt.links.new(mix.outputs[2], bsdf.inputs["Base Color"])
            o.data.materials[i] = nm


def retint(objects, tint, only=None):
    """Shifts the colours of the objects' materials (hue, saturation, value), optionally only the
    materials whose name contains one of `only`."""
    for o in objects:
        if o.type != "MESH":
            continue
        for i, m in enumerate(o.data.materials):
            if m is None or (only and not any(k in m.name for k in only)):
                continue
            nm = m.copy()
            b = next((n for n in nm.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
            if b is None:
                continue
            hs = nm.node_tree.nodes.new("ShaderNodeHueSaturation")
            hs.inputs["Hue"].default_value = tint[0]
            hs.inputs["Saturation"].default_value = tint[1]
            hs.inputs["Value"].default_value = tint[2]
            if b.inputs["Base Color"].links:
                nm.node_tree.links.new(b.inputs["Base Color"].links[0].from_socket, hs.inputs["Color"])
            else:
                hs.inputs["Color"].default_value = b.inputs["Base Color"].default_value
            nm.node_tree.links.new(hs.outputs["Color"], b.inputs["Base Color"])
            o.data.materials[i] = nm
