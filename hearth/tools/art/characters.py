"""Animated sprite sheets of dwellers, heroes and Fog creatures from KayKit's characters (CC0).

KayKit Adventurers (Knight, Barbarian, Mage, Rogue, Rogue_Hooded) share one rig with 76 animations,
so dwellers are mixed from their parts (a bearded head, a hooded one; a tunic, a robe...) and their
clothes are recoloured; heroes are the adventurers with the weapons of their class; the creatures of
the Fog are KayKit's skeletons tinted per family. Every animation is rendered frame by frame into one
sheet: rows are animations, columns are frames.
"""
import math
import os

import bpy
from mathutils import Vector
from PIL import Image

import toy as t

KAYKIT = os.environ.get("KAYKIT", "/home/user/kaykit-game-assets")
ADV = os.path.join(KAYKIT, "kaykit-character-pack-adventures-1.0", "addons", "kaykit_character_pack_adventures", "Characters", "gltf")
SKEL = os.path.join(KAYKIT, "kaykit-character-pack-skeletons-1.0", "addons", "kaykit_character_pack_skeletons")

FRAME = 192
FRAMES = 8
BODY = ["Head", "Body", "ArmLeft", "ArmRight", "LegLeft", "LegRight"]

# A dweller: base model, kept parts, parts borrowed from another model, clothes shift
# (hue offset, saturation, value), what the hands hold.
DWELLERS = [
    {"base": "Barbarian", "keep": BODY + ["Mug"], "shift": (0.0, 0.7, 0.9)},
    {"base": "Rogue", "keep": BODY, "shift": (0.45, 0.8, 0.9)},
    {"base": "Mage", "keep": BODY, "shift": (0.6, 0.55, 0.85)},
    {"base": "Knight", "keep": BODY + ["Cape"], "shift": (0.08, 0.35, 0.8)},
    {"base": "Rogue_Hooded", "keep": [p for p in BODY if p != "Head"] + ["Head_Hooded"], "shift": (0.85, 0.6, 0.85)},
    {"base": "Barbarian", "keep": BODY + ["Hat", "1H_Axe"], "shift": (0.55, 0.6, 0.85)},
    {"base": "Rogue", "keep": BODY, "shift": (0.1, 0.9, 1.0), "borrow": ("Mage", ["Body"])},
    {"base": "Knight", "keep": BODY, "shift": (0.0, 0.0, 1.0), "borrow": ("Barbarian", ["Body"])},
    {"base": "Mage", "keep": BODY + ["Spellbook"], "shift": (0.3, 0.6, 0.8), "borrow": ("Barbarian", ["Head"])},
    {"base": "Rogue", "keep": BODY + ["Cape"], "shift": (0.2, 0.5, 0.75), "borrow": ("Knight", ["Head"])},
]

# Heroes: model, kept gear, clothes shift.
HEROES = {
    "Guardian": ("Knight", ["Helmet", "Cape", "1H_Sword", "Rectangle_Shield"], (0.0, 1.0, 1.0)),
    "Swordsman": ("Knight", ["Cape", "2H_Sword"], (0.62, 1.0, 1.0)),
    "Cleaver": ("Barbarian", ["Hat", "Cape", "2H_Axe"], (0.0, 1.0, 1.0)),
    "Archer": ("Rogue_Hooded", ["Cape", "2H_Crossbow"], (0.0, 1.0, 1.0)),
    "CoalPriest": ("Mage", ["Hat", "Cape", "2H_Staff"], (0.33, 1.15, 0.7)),
    "AshMage": ("Mage", ["Hat", "Cape", "1H_Wand", "Spellbook"], (0.0, 1.0, 1.0)),
}
HERO_ATTACK = {"Guardian": "1H_Melee_Attack_Chop", "Swordsman": "2H_Melee_Attack_Slice", "Cleaver": "2H_Melee_Attack_Chop",
               "Archer": "2H_Ranged_Shoot", "CoalPriest": "Spellcast_Shoot", "AshMage": "Spellcast_Long"}
HERO_IDLE = {"Swordsman": "2H_Melee_Idle", "Cleaver": "2H_Melee_Idle"}

# Fog creatures: skeleton, kept parts, weapons (asset, bone), tint, glow.
CREATURES = {
    "Fiend": ("Skeleton_Minion", ["Jaw", "Eyes", "Cloak"], [("Skeleton_Blade", "handslot.r")], (0.72, 0.82, 1.0), (0.2, 0.35, 0.8)),
    "Possessed": ("Skeleton_Mage", ["Jaw", "Eyes", "Hat", "Skull"], [("Skeleton_Staff", "handslot.r")], (0.85, 0.6, 1.0), (0.45, 0.1, 0.7)),
    "Knight": ("Skeleton_Warrior", ["Jaw", "Eyes", "Helmet", "Cloak"], [("Skeleton_Axe", "handslot.r"), ("Skeleton_Shield_Large_A", "handslot.l")], (0.62, 0.64, 0.74), None),
    "Root": ("Skeleton_Rogue", ["Jaw", "Eyes", "Hood", "Cape"], [("Skeleton_Crossbow", "handslot.r")], (0.62, 0.86, 0.45), (0.15, 0.35, 0.05)),
}

DWELLER_ANIMS = [("idle", "Unarmed_Idle"), ("walk", "Walking_A"), ("work", "Interact"), ("cheer", "Cheer")]
CREATURE_ANIMS = [("walk", "Walking_C"), ("attack", "1H_Melee_Attack_Chop"), ("hit", "Hit_A"), ("die", "Death_A")]


def hero_anims(key):
    return [("idle", HERO_IDLE.get(key, "Idle")), ("walk", "Walking_A"), ("attack", HERO_ATTACK[key]),
            ("hit", "Hit_A"), ("die", "Death_A"), ("cheer", "Cheer")]


# ------------------------------------------------------------------ building a character

PREFIXES = ("Knight_", "Barbarian_", "Mage_", "Rogue_", "Skeleton_Minion_", "Skeleton_Mage_", "Skeleton_Warrior_", "Skeleton_Rogue_")


def _part(obj):
    """'Knight_ArmLeft' -> 'ArmLeft', 'Rogue_Head_Hooded' -> 'Head_Hooded'; gear keeps its name."""
    name = obj.name.split(".")[0]
    for prefix in PREFIXES:
        if name.startswith(prefix):
            return name[len(prefix):]
    return name


def _load(path):
    new, roots = t._import(path)
    rig = next(o for o in new if o.type == "ARMATURE")
    return rig, [o for o in new if o.type == "MESH"]


def _keep(meshes, keep):
    for o in list(meshes):
        if _part(o) not in keep:
            bpy.data.objects.remove(o, do_unlink=True)
            meshes.remove(o)


def _move_to(obj, rig):
    """Re-binds a mesh of another character to this rig (KayKit characters share bone names)."""
    if obj.parent_type == "BONE":
        bone = obj.parent_bone
        local = obj.matrix_local.copy()
        obj.parent = rig
        obj.parent_type = "BONE"
        obj.parent_bone = bone
        obj.matrix_local = local
    else:
        obj.parent = rig
        for m in obj.modifiers:
            if m.type == "ARMATURE":
                m.object = rig


def _attach(rig, path, bone):
    new, roots = t._import(path)
    for o in roots:
        o.parent = rig
        o.parent_type = "BONE"
        o.parent_bone = bone
        o.location = (0, 0, 0)
        o.rotation_euler = (0, 0, 0)
    return [o for o in new if o.type == "MESH"]


def _shift(objects, hsv, parts=("Body", "LegLeft", "LegRight", "Cape", "Hat", "Hood", "Helmet", "Cloak")):
    """Recolours the clothes: hue offset, saturation and value multipliers."""
    if tuple(hsv) == (0.0, 1.0, 1.0):
        return
    for o in objects:
        if _part(o) not in parts:
            continue
        for i, m in enumerate(o.data.materials):
            nm = m.copy()
            nt = nm.node_tree
            bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
            if not bsdf.inputs["Base Color"].links:
                continue
            src = bsdf.inputs["Base Color"].links[0].from_socket
            hs = nt.nodes.new("ShaderNodeHueSaturation")
            hs.inputs["Hue"].default_value = (0.5 + hsv[0]) % 1.0
            hs.inputs["Saturation"].default_value = hsv[1]
            hs.inputs["Value"].default_value = hsv[2]
            nt.links.new(src, hs.inputs["Color"])
            nt.links.new(hs.outputs["Color"], bsdf.inputs["Base Color"])
            o.data.materials[i] = nm


def _tint(objects, color, glow):
    for o in objects:
        for i, m in enumerate(o.data.materials):
            nm = m.copy()
            nt = nm.node_tree
            bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
            mix = nt.nodes.new("ShaderNodeMix")
            mix.data_type = "RGBA"
            mix.blend_type = "MULTIPLY"
            mix.inputs["Factor"].default_value = 1.0
            if bsdf.inputs["Base Color"].links:
                nt.links.new(bsdf.inputs["Base Color"].links[0].from_socket, mix.inputs[6])
            mix.inputs[7].default_value = tuple(color) + (1,)
            nt.links.new(mix.outputs[2], bsdf.inputs["Base Color"])
            if glow and _part(o) == "Eyes":
                bsdf.inputs["Emission Color"].default_value = tuple(glow) + (1,)
                bsdf.inputs["Emission Strength"].default_value = 6.0
            o.data.materials[i] = nm


def build(kind, key):
    """Builds one character in the scene; returns (rig, meshes)."""
    if kind == "creature":
        model, parts, weapons, color, glow = CREATURES[key]
        rig, meshes = _load(os.path.join(SKEL, "Characters", "gltf", model + ".glb"))
        _keep(meshes, BODY + parts)
        for asset, bone in weapons:
            meshes += _attach(rig, os.path.join(SKEL, "Assets", "gltf", asset + ".gltf"), bone)
        _tint(meshes, color, glow)
        return rig, meshes
    if kind == "hero":
        model, gear, hsv = HEROES[key]
        rig, meshes = _load(os.path.join(ADV, model + ".glb"))
        head = ["Head_Hooded"] if model == "Rogue_Hooded" else ["Head"]
        _keep(meshes, BODY + head + gear)
        _shift(meshes, hsv)
        return rig, meshes
    d = DWELLERS[key]
    rig, meshes = _load(os.path.join(ADV, d["base"] + ".glb"))
    keep = list(d["keep"])
    if "borrow" in d:
        other, parts = d["borrow"]
        _keep(meshes, [k for k in keep if k not in parts])
        orig, extra = _load(os.path.join(ADV, other + ".glb"))
        for o in list(extra):
            if _part(o) in parts:
                _move_to(o, rig)
                meshes.append(o)
            else:
                bpy.data.objects.remove(o, do_unlink=True)
        bpy.data.objects.remove(orig, do_unlink=True)
    else:
        _keep(meshes, keep)
    _shift(meshes, d["shift"])
    return rig, meshes


# ------------------------------------------------------------------ rendering

def _bounds(objects):
    dg = bpy.context.evaluated_depsgraph_get()
    pts = []
    for o in objects:
        ev = o.evaluated_get(dg)
        me = ev.to_mesh()
        pts += [ev.matrix_world @ v.co for v in me.vertices]
        ev.to_mesh_clear()
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi


def _play(rig, action_name, frame_t):
    """Sets the rig to a moment (0..1) of one of its animations."""
    candidates = [a for a in bpy.data.actions if a.name.split(".")[0] == action_name]
    action = next((a for a in candidates if any(sl.identifier == "OB" + rig.name for sl in a.slots)), None) or \
        (candidates[0] if candidates else None)
    if action is None:
        raise KeyError(action_name)
    if rig.animation_data is None:
        rig.animation_data_create()
    rig.animation_data.action = action
    rig.animation_data.action_slot = next((sl for sl in action.slots if sl.identifier == "OB" + rig.name), action.slots[0])
    start, end = action.frame_range
    f = start + (end - start) * frame_t
    bpy.context.scene.frame_set(int(f), subframe=f - int(f))


def sheet(kind, key, anims, out, frames=FRAMES, samples=20):
    """Renders the animations of one character into a sheet (rows: animations, columns: frames)."""
    t.reset(world=(0, 0, 0))
    sc = bpy.context.scene
    sc.render.resolution_x = sc.render.resolution_y = FRAME * 2
    sc.cycles.samples = samples
    rig, meshes = build(kind, key)
    pivot = bpy.data.objects.new("pivot", None)
    sc.collection.objects.link(pivot)
    rig.parent = pivot
    _play(rig, anims[0][1], 0.0)
    lo, hi = _bounds(meshes)
    height = hi.z - lo.z
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    sc.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = max(height * 1.5, (hi.x - lo.x) * 1.4)
    cz = lo.z + height * 0.52
    cam.location = (0, -20, cz + 20 * math.tan(math.radians(10)))
    cam.rotation_euler = (math.radians(80), 0, 0)
    sc.camera = cam
    s = max(0.5, height / 1.8)
    t.light("AREA", (-3 * s, -6 * s, 5 * s), 1500 * s * s, color=(1.0, 0.94, 0.86), size=5 * s,
            rot=(math.radians(50), 0, math.radians(-25)))
    t.light("AREA", (4 * s, 3 * s, 3 * s), 800 * s * s, color=(1.0, 0.62, 0.32), size=3 * s,
            rot=(math.radians(-60), 0, math.radians(140)))
    t.light("AREA", (0, -6 * s, 0.5 * s), 300 * s * s, color=(0.6, 0.7, 1.0), size=6 * s, rot=(math.radians(90), 0, 0))
    paths = []
    cache = os.environ.get("HEARTH_ART_CACHE", "/tmp")
    for row, (label, action) in enumerate(anims):
        pivot.rotation_euler = (0, 0, math.radians(65 if label in ("walk", "attack", "hit", "die") else 18))
        for i in range(frames):
            _play(rig, action, min(1.0, i / (frames - 1)) if label == "die" else i / frames)
            path = os.path.join(cache, "frames", "%s_%s_%s_%d.png" % (kind, key, label, i))
            t.render(path, transparent=True)
            paths.append((row, i, path))
    img = Image.new("RGBA", (FRAME * frames, FRAME * len(anims)), (0, 0, 0, 0))
    for row, i, path in paths:
        img.paste(Image.open(path).resize((FRAME, FRAME), Image.LANCZOS), (i * FRAME, row * FRAME))
    os.makedirs(os.path.dirname(out), exist_ok=True)
    img.save(out)
    return {"rows": [label for label, _ in anims], "frames": frames, "size": FRAME, "height": round(height, 3)}


def portrait(kind, key, out, size=256):
    """A still of the character's idle pose, facing the camera: for cards, lists and the hero screen."""
    t.reset(world=(0, 0, 0))
    sc = bpy.context.scene
    sc.render.resolution_x = sc.render.resolution_y = size
    sc.cycles.samples = 48
    rig, meshes = build(kind, key)
    anims = hero_anims(key) if kind == "hero" else (CREATURE_ANIMS if kind == "creature" else DWELLER_ANIMS)
    _play(rig, anims[0][1], 0.0)
    lo, hi = _bounds(meshes)
    height = hi.z - lo.z
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    sc.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = max(height, hi.x - lo.x) * 1.15
    cam.location = (0.0, -20, lo.z + height * 0.5 + 20 * math.tan(math.radians(6)))
    cam.rotation_euler = (math.radians(84), 0, math.radians(-12))
    cam.location.x = -20 * math.sin(math.radians(12))
    sc.camera = cam
    s = max(0.5, height / 1.8)
    t.light("AREA", (-3 * s, -6 * s, 5 * s), 1500 * s * s, color=(1.0, 0.94, 0.86), size=5 * s,
            rot=(math.radians(50), 0, math.radians(-25)))
    t.light("AREA", (4 * s, 3 * s, 3 * s), 800 * s * s, color=(1.0, 0.62, 0.32), size=3 * s,
            rot=(math.radians(-60), 0, math.radians(140)))
    t.render(out, transparent=True)
