"""Animated sprite sheets of dwellers, heroes and Fog creatures from Quaternius' characters (CC0 /
Quaternius Asset License: free in commercial games), in the style of the rooms (same studio, same
hand-painted look).

Universal Base Characters give the heads (a man, a woman), Modular Character Outfits - Fantasy the
clothes (peasant and ranger, part by part), the hairstyles are rigged to the same skeleton, and the
Universal Animation Library 1-2 animates them all (walk, work, sword combos, spells, zombie walk...).
Dwellers are mixed from these parts and recoloured, so no two look alike; heroes carry the weapons of
their class (the Fantasy Props kit's sword, axe and shield, a bow and staffs made here); the creatures
of the Fog are the Bestiary's imp and puglin and possessed humans, tinted per family.
Heads and hands are scaled up a little, for the toy-like look of Hustle Castle.
Every animation is rendered frame by frame into one sheet: rows are animations, columns are frames.
"""
import math
import os

import bpy
import bmesh  # after bpy: the bpy module provides it
from mathutils import Matrix, Vector
from PIL import Image

import qkit as q
import toy as t

FRAME = 192
FRAMES = 8
BASE = {"m": "Superhero_Male_FullBody", "f": "Superhero_Female_FullBody"}
SEX = {"m": "Male", "f": "Female"}
SKIN = ("Superhero", "Regular")       # skin materials: the head, the bare arms of the outfits
CLOTH = ("Peasant", "Ranger")

# hair colours (multiplied with the white hair texture)
BROWN, DARK, BLACK, BLOND, RED, GREY, GINGER = (0.42, 0.26, 0.15), (0.22, 0.14, 0.09), (0.09, 0.08, 0.08), (0.95, 0.78, 0.45), \
    (0.6, 0.2, 0.1), (0.7, 0.7, 0.72), (0.85, 0.42, 0.15)
# skin tones (multiplied)
FAIR, WARM, TAN, DEEP = (1.0, 0.97, 0.95), (1.0, 0.9, 0.82), (0.86, 0.7, 0.58), (0.62, 0.45, 0.35)

# A dweller: sex, outfit of each part (body, legs, feet), hood, pauldron, hair, hair colour, skin,
# clothes shift (hue offset, saturation, value), what the right hand holds.
DWELLERS = [
    dict(sex="m", body="Peasant", legs="Peasant", hair=["Hair_SimpleParted"], hc=BROWN, skin=WARM, shift=(0.0, 1.0, 1.0), hold="Mug"),
    dict(sex="f", body="Peasant", legs="Peasant", hair=["Hair_Buns"], hc=RED, skin=FAIR, shift=(0.55, 1.1, 0.95), hold="Book_5"),
    dict(sex="m", body="Ranger", legs="Peasant", hair=["Hair_Buzzed", "Hair_Beard"], hc=BLACK, skin=TAN, shift=(0.08, 0.9, 0.9), hold="Pickaxe_Bronze"),
    dict(sex="f", body="Ranger", legs="Ranger", hood=True, hair=["Hair_Long"], hc=BLOND, skin=WARM, shift=(0.0, 1.0, 1.0)),
    dict(sex="m", body="Peasant", legs="Ranger", hair=["Hair_Buzzed", "Hair_Beard"], hc=BLOND, skin=FAIR, shift=(0.6, 1.2, 0.9), hold="Mug"),
    dict(sex="f", body="Peasant", legs="Peasant", hair=["Hair_Long"], hc=BLACK, skin=DEEP, shift=(0.35, 1.2, 0.85)),
    dict(sex="m", body="Ranger", legs="Ranger", hair=["Hair_SimpleParted", "Hair_Beard"], hc=GREY, skin=WARM, shift=(0.9, 0.6, 0.85),
         hold="Scroll_1"),
    dict(sex="f", body="Ranger", legs="Peasant", hair=["Hair_BuzzedFemale"], hc=DARK, skin=TAN, shift=(0.02, 1.4, 1.0), pauldron=True),
    dict(sex="m", body="Peasant", legs="Peasant", hood=True, hair=["Hair_Beard"], hc=GINGER, skin=FAIR, shift=(0.75, 1.0, 0.8),
         hold="Pickaxe_Bronze"),
    dict(sex="f", body="Peasant", legs="Ranger", hair=["Hair_Buns"], hc=DARK, skin=WARM, shift=(0.12, 1.3, 1.05), hold="Carrot"),
]
WORK = ["Interact", "Farm_Harvest", "Fixing_Kneeling", "PickUp_Table", "TreeChopping_Loop", "Farm_Watering", "Push_Loop",
        "Fixing_Kneeling", "Farm_PlantSeed", "Chest_Open"]

HEROES = {
    "Guardian": dict(sex="m", body="Ranger", legs="Ranger", pauldron=True, hair=["Hair_Buzzed", "Hair_Beard"], hc=BROWN, skin=WARM,
                     shift=(0.62, 0.35, 0.75), right="Sword_Bronze", left="Shield_Wooden"),
    "Swordsman": dict(sex="f", body="Ranger", legs="Ranger", pauldron=True, hair=["Hair_Buns"], hc=BLACK, skin=FAIR,
                      shift=(0.02, 1.3, 0.85), right="Sword_Bronze", right_scale=1.2),
    "Cleaver": dict(sex="m", body="Peasant", legs="Ranger", hair=["Hair_Buzzed", "Hair_Beard"], hc=RED, skin=TAN,
                    shift=(0.97, 1.3, 0.7), right="Axe_Bronze", right_scale=1.7),
    "Archer": dict(sex="f", body="Ranger", legs="Ranger", hood=True, hair=["Hair_Long"], hc=BLOND, skin=WARM, shift=(0.0, 1.0, 1.0),
                   right="bow"),
    "CoalPriest": dict(sex="m", body="Peasant", legs="Peasant", hood=True, hair=["Hair_Beard"], hc=GREY, skin=WARM,
                       shift=(0.05, 0.4, 0.35), right="staff_coal"),
    "AshMage": dict(sex="f", body="Peasant", legs="Ranger", hood=True, hair=["Hair_Long"], hc=GREY, skin=FAIR, shift=(0.6, 0.15, 0.95),
                    right="staff_ash"),
}
HERO_ANIMS = {
    "Guardian": ("Idle_Shield_Loop", "Sword_Regular_A"), "Swordsman": ("Sword_Idle", "Sword_Regular_Combo"),
    "Cleaver": ("Sword_Idle", "Sword_Heavy_Combo"), "Archer": ("Pistol_Idle_Loop", "Pistol_Shoot"),
    "CoalPriest": ("Spell_Simple_Idle_Loop", "Spell_Simple_Shoot"), "AshMage": ("Spell_Simple_Idle_Loop", "Spell_Simple_Shoot"),
}

# Fog creatures: a model (a Bestiary monster or a possessed human), tint, glowing eyes, size, animations.
CREATURES = {
    "Fiend": dict(model="Imp", tint=(0.75, 0.55, 0.95), glow=(1.0, 0.3, 0.05), size=1.0,
                  anims=("Jog_Fwd_Loop", "Sword_Attack")),
    "Possessed": dict(sex="m", body="Peasant", legs="Peasant", hair=["Hair_SimpleParted"], hc=GREY, skin=(0.62, 0.66, 0.6),
                      shift=(0.75, 0.3, 0.55), tint=(0.78, 0.7, 0.9), glow=(0.7, 0.2, 1.0), size=1.0,
                      anims=("Zombie_Walk_Fwd_Loop", "Zombie_Scratch")),
    "Knight": dict(sex="m", body="Ranger", legs="Ranger", hood=True, pauldron=True, hair=[], hc=BLACK, skin=(0.5, 0.5, 0.6),
                   shift=(0.6, 0.2, 0.45), tint=(0.55, 0.58, 0.7), glow=(0.3, 0.7, 1.0), size=1.1, right="Sword_Bronze",
                   left="Shield_Wooden", anims=("Walk_Formal_Loop", "Sword_Regular_A")),
    "Root": dict(model="Puglin", tint=(0.55, 0.62, 0.38), glow=(0.6, 1.0, 0.2), size=1.5, anims=("Walk_Loop", "Melee_Hook")),
}


def hero_anims(key):
    idle, attack = HERO_ANIMS[key]
    return [("idle", idle), ("walk", "Walk_Loop"), ("attack", attack), ("hit", "Hit_Chest"), ("die", "Death01"), ("cheer", "Dance_Loop")]


def dweller_anims(i):
    return [("idle", "Idle_Loop"), ("walk", "Walk_Loop"), ("work", WORK[i % len(WORK)]), ("cheer", "Dance_Loop")]


def creature_anims(key):
    walk, attack = CREATURES[key]["anims"]
    return [("walk", walk), ("attack", attack), ("hit", "Hit_Chest"), ("die", "Death01")]


# ------------------------------------------------------------------ building a character

def _load(name):
    new, roots = t._import(q.path(name))
    rig = next(o for o in new if o.type == "ARMATURE")
    return rig, [o for o in new if o.type == "MESH"]


def _merge(rig, name):
    """Imports a part rigged to the same skeleton and binds it to this rig."""
    other, meshes = _load(name)
    for o in meshes:
        o.parent = rig
        for m in o.modifiers:
            if m.type == "ARMATURE":
                m.object = rig
    bpy.data.objects.remove(other, do_unlink=True)
    return meshes


def _head_only(meshes):
    """Cuts the base body down to the head and neck: the outfit dresses the rest."""
    for o in meshes:
        if not o.name.lower().startswith("superhero"):
            continue
        keep = {o.vertex_groups[n].index for n in ("Head", "neck_01") if n in o.vertex_groups}
        bm = bmesh.new()
        bm.from_mesh(o.data)
        dl = bm.verts.layers.deform.active
        kill = [v for v in bm.verts if sum(w for g, w in v[dl].items() if g in keep) < 0.5]
        bmesh.ops.delete(bm, geom=kill, context="VERTS")
        bm.to_mesh(o.data)
        bm.free()


def _toy(rig, head=1.28, hands=1.12):
    """Bigger head and hands, whatever the animation does (a scale constraint on top of it)."""
    for bone, s in (("Head", head), ("hand_l", hands), ("hand_r", hands)):
        if bone not in rig.pose.bones:
            continue
        e = bpy.data.objects.new("scale_" + bone, None)
        bpy.context.scene.collection.objects.link(e)
        e.scale = (s, s, s)
        c = rig.pose.bones[bone].constraints.new("COPY_SCALE")
        c.target = e
        c.use_offset = True
        c.owner_space = "LOCAL"
        c.target_space = "WORLD"


def _multiply(objects, color, only=None):
    """Multiplies the base colour of the materials (only those whose name has one of `only`)."""
    if color is None:
        return
    for o in objects:
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


def _glow_eyes(objects, glow):
    for o in objects:
        for m in o.data.materials:
            if m and ("Eye" in m.name or "eye" in o.name.lower()):
                b = next((n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
                if b:
                    b.inputs["Emission Color"].default_value = tuple(glow) + (1,)
                    b.inputs["Emission Strength"].default_value = 8.0


def _prop(name):
    """A held thing: a model of the kits, or a bow or a staff made here. Returns its root objects."""
    if name == "bow":
        # an arc of a torus: grip at the origin, limbs along z (like a sword's blade), bulging forward
        bpy.ops.mesh.primitive_torus_add(major_radius=0.55, minor_radius=0.025, major_segments=32, minor_segments=8)
        bow = bpy.context.active_object
        bm = bmesh.new()
        bm.from_mesh(bow.data)
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.x < 0.15], context="VERTS")
        bmesh.ops.translate(bm, verts=bm.verts, vec=(-0.55, 0, 0))
        bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(90), 3, "X"))
        bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(90), 3, "Z"))
        bmesh.ops.create_cone(bm, cap_ends=False, segments=6, radius1=0.006, radius2=0.006, depth=1.05,
                              matrix=Matrix.Translation((0, -0.15, 0)))
        # in the bow-holding pose the hand's x axis is the vertical one
        bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(90), 3, "Y"))
        bm.to_mesh(bow.data)
        bm.free()
        bow.data.materials.append(t.mat((0.4, 0.22, 0.1), 0.6))
        return [bow]
    if name.startswith("staff"):
        glow = (1.0, 0.4, 0.08) if name == "staff_coal" else (0.7, 0.75, 1.0)
        staff = t.cyl(0.03, 1.5, (0, 0, 0.3), t.mat((0.3, 0.17, 0.08), 0.7))
        orb = t.ball(0.1, (0, 0, 1.1), t.mat(glow, 0.3, emit=glow, strength=5))
        bpy.ops.object.select_all(action="DESELECT")
        orb.select_set(True)
        staff.select_set(True)
        bpy.context.view_layer.objects.active = staff
        bpy.ops.object.join()
        staff.data.transform(Matrix.Translation((0, 0, 0.3)))
        staff.location = (0, 0, 0)
        return [staff]
    new, roots = t._import(q.path(name))
    return roots


def _hold(rig, name, bone="hand_r", scale=1.0):
    roots = _prop(name)
    left = bone == "hand_l"
    if name.startswith("staff"):
        # a staff stays upright in the hand whatever the arm does: it follows the hand, not its turn
        for o in roots:
            c = o.constraints.new("COPY_LOCATION")
            c.target = rig
            c.subtarget = bone
            o.scale = (scale * rig.scale[0],) * 3
        return roots
    for o in roots:
        o.parent = rig
        o.parent_type = "BONE"
        o.parent_bone = bone
        o.rotation_mode = "XYZ"
        if name == "Shield_Wooden":
            o.location = (0.05, -0.05, 0.0)
            o.rotation_euler = (math.radians(0), math.radians(90), math.radians(90 if left else -90))
        else:
            o.location = (0, -0.05, 0.03)
            o.rotation_euler = (math.radians(90), 0, math.radians(90 if not left else -90))
        o.scale = (scale, scale, scale)
    bpy.context.view_layer.update()
    return [o for r in roots for o in [r] + list(r.children_recursive) if o.type == "MESH"]


def human(d):
    """A person from a recipe: head, outfit parts, hood, pauldron, hair, colours, held things."""
    sex = SEX[d["sex"]]
    rig, meshes = _load(BASE[d["sex"]])
    _head_only(meshes)
    meshes += _merge(rig, "%s_%s_Body" % (sex, d["body"]))
    meshes += _merge(rig, "%s_%s_Arms" % (sex, d["body"]))
    meshes += _merge(rig, "%s_%s_Legs" % (sex, d["legs"]))
    feet = "%s_%s_Feet" % (sex, d["legs"])
    meshes += _merge(rig, feet + "_Boots" if d["sex"] == "m" and d["legs"] == "Ranger" else feet)
    if d.get("hood"):
        meshes += _merge(rig, "%s_Ranger_Head_Hood" % sex)
    if d.get("pauldron"):
        meshes += _merge(rig, "Male_Ranger_Acc_Pauldron" if d["sex"] == "m" else "Female_Ranger_Acc_Pauldrons")
    hair = []
    for h in d.get("hair", []):
        if d.get("hood") and h != "Hair_Beard":
            continue
        hair += _merge(rig, h)
    _multiply(hair, d["hc"])
    for o in meshes:
        if o.name.startswith("Eyebrows"):
            _multiply([o], d["hc"])
    meshes += hair
    _multiply(meshes, d["skin"], only=SKIN)
    q.retint(meshes, (0.5 + d["shift"][0], d["shift"][1], d["shift"][2]), only=CLOTH)
    if d.get("hold"):
        meshes += _hold(rig, d["hold"])
    if d.get("right"):
        meshes += _hold(rig, d["right"], scale=d.get("right_scale", 1.0))
    if d.get("left"):
        meshes += _hold(rig, d["left"], "hand_l")
    _toy(rig)
    return rig, meshes


def build(kind, key):
    """Builds one character in the scene; returns (rig, meshes)."""
    if kind == "dweller":
        return human(DWELLERS[key])
    if kind == "hero":
        return human(HEROES[key])
    c = CREATURES[key]
    if "model" in c:
        rig, meshes = _load(c["model"])
        _toy(rig, head=1.15, hands=1.0)
    else:
        rig, meshes = human(c)
    _multiply(meshes, c["tint"])
    _glow_eyes(meshes, c["glow"])
    rig.scale = (c["size"],) * 3
    return rig, meshes


# ------------------------------------------------------------------ animations

_libs = []


def _load_animations():
    """Imports the animation libraries' actions into this scene (their mannequins are removed)."""
    for lib in ("UAL1_Standard", "UAL2_Standard"):
        before = set(bpy.data.objects)
        bpy.ops.import_scene.gltf(filepath=q.path(lib))
        for o in set(bpy.data.objects) - before:
            bpy.data.objects.remove(o, do_unlink=True)


def _play(rig, action_name, frame_t):
    """Sets the rig to a moment (0..1) of one of the library's animations."""
    action = next((a for a in bpy.data.actions if a.name.split(".")[0] == action_name), None)
    if action is None:
        raise KeyError(action_name)
    if rig.animation_data is None:
        rig.animation_data_create()
    if rig.animation_data.action != action:
        rig.animation_data.action = action
        rig.animation_data.action_slot = action.slots[0]
    start, end = action.frame_range
    f = start + (end - start) * frame_t
    bpy.context.scene.frame_set(int(f), subframe=f - int(f))


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


def _lights(s):
    t.light("AREA", (-3 * s, -6 * s, 5 * s), 1300 * s * s, color=(1.0, 0.94, 0.86), size=5 * s, rot=(math.radians(50), 0, math.radians(-25)))
    t.light("AREA", (4 * s, 3 * s, 3 * s), 700 * s * s, color=(1.0, 0.62, 0.32), size=3 * s, rot=(math.radians(-60), 0, math.radians(140)))
    t.light("AREA", (0, -6 * s, 0.5 * s), 250 * s * s, color=(0.6, 0.7, 1.0), size=6 * s, rot=(math.radians(90), 0, 0))


ONCE = ("attack", "hit", "die")


def sheet(kind, key, anims, out, frames=FRAMES, samples=20):
    """Renders the animations of one character into a sheet (rows: animations, columns: frames)."""
    t.reset(world=(0, 0, 0))
    sc = bpy.context.scene
    sc.render.resolution_x = sc.render.resolution_y = FRAME * 2
    sc.cycles.samples = samples
    _load_animations()
    rig, meshes = build(kind, key)
    pivot = bpy.data.objects.new("pivot", None)
    sc.collection.objects.link(pivot)
    rig.parent = pivot
    _play(rig, anims[0][1], 0.0)
    lo, hi = _bounds(meshes)
    height = hi.z - lo.z
    # the frame shows the character at 1/1.5 of its height, feet at 0.24 from the bottom (ArtLibrary.DrawCharacter)
    size = height * 1.5
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    sc.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = size
    tilt = math.radians(8)
    cz = lo.z + size * (0.5 - 0.24)
    cam.location = (0, -20, cz + 20 * math.tan(tilt))
    cam.rotation_euler = (math.radians(90) - tilt, 0, 0)
    sc.camera = cam
    _lights(max(0.5, height / 1.8))
    paths = []
    cache = os.environ.get("HEARTH_ART_CACHE", "/tmp")
    for row, (label, action) in enumerate(anims):
        pivot.rotation_euler = (0, 0, math.radians(65 if label in ("walk", "attack", "hit", "die") else 18))
        for i in range(frames):
            _play(rig, action, min(1.0, i / (frames - 1)) if label in ONCE else i / frames)
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
    """A still of the character's idle pose, head and shoulders, facing the camera: for cards and lists."""
    t.reset(world=(0, 0, 0))
    sc = bpy.context.scene
    sc.render.resolution_x = sc.render.resolution_y = size
    sc.cycles.samples = 48
    _load_animations()
    rig, meshes = build(kind, key)
    anims = hero_anims(key) if kind == "hero" else (creature_anims(key) if kind == "creature" else dweller_anims(key))
    _play(rig, anims[0][1], 0.0)
    lo, hi = _bounds(meshes)
    height = hi.z - lo.z
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    sc.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = height * 0.62
    cam.location = (-20 * math.sin(math.radians(14)), -20 * math.cos(math.radians(14)), hi.z - height * 0.27)
    cam.rotation_euler = (math.radians(90), 0, math.radians(-14))
    sc.camera = cam
    _lights(max(0.5, height / 1.8))
    t.render(out, transparent=True)
