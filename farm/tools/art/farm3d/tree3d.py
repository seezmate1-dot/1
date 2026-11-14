"""A fruit tree for the farm map: a Quaternius CC0 tree with the species' own leaves, bark tint, chunky proportions,
and modelled fruit or blossom in the crown. Usage: python tree3d.py species state variant out.png"""
import sys, os, math, random, bpy, mathutils
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import scene

species, state, variant, out = sys.argv[1], sys.argv[2], int(sys.argv[3]), sys.argv[4]
N = os.path.expanduser("~/assets3d/stylized_nature/Ultimate Stylized Nature - May 2022/Blends/")
TEX = os.path.join(os.path.dirname(os.path.abspath(__file__)), "tex")
CFG = {
    "apple":  dict(models=["MapleTree_4", "NormalTree_5"], scale=(1.2, 1.2, 0.9), bark=(0.62, 0.42, 0.28),
                   leaves="apple", fruit="apple", n_fruit=26, bloom=(1.0, 0.75, 0.8), n_bloom=230),
    "pear":   dict(models=["NormalTree_1", "NormalTree_2"], scale=(1.15, 1.15, 0.95), bark=(0.5, 0.42, 0.36),
                   leaves="pear", fruit="pear", n_fruit=26, bloom=(1.0, 1.0, 0.97), n_bloom=230),
    "cherry": dict(models=["MapleTree_2", "MapleTree_5"], scale=(1.3, 1.3, 0.85), bark=(0.62, 0.3, 0.25),
                   leaves="cherry", fruit="cherry", n_fruit=46, bloom=(0.9, 0.38, 0.55), n_bloom=260),
}[species]
random.seed(variant * 31 + len(species))
scene.load(N + CFG["models"][variant - 1] + ".blend")
tree = scene.meshes()[0]
tree.scale = CFG["scale"]; bpy.context.view_layer.update()

def mat_kind(m):
    n = m.name.lower()
    return "leaf" if "lea" in n else "bark" if "bark" in n else None

for slot in tree.material_slots:
    m = slot.material; k = mat_kind(m); nt = m.node_tree
    if k == "leaf":
        for n in nt.nodes:
            if n.type == "TEX_IMAGE":
                n.image = bpy.data.images.load(os.path.join(TEX, "%s_%s.png" % (CFG["leaves"], "maple" if "Maple" in CFG["models"][variant - 1] else "normal")))
    elif k == "bark":
        tex = [n for n in nt.nodes if n.type == "TEX_IMAGE" and any(l.to_socket.name == "Color" and l.to_node.type == "BSDF_DIFFUSE" for l in n.outputs[0].links)]
        dif = [n for n in nt.nodes if n.type == "BSDF_DIFFUSE"][0]
        mix = nt.nodes.new("ShaderNodeMix"); mix.data_type = "RGBA"; mix.blend_type = "MULTIPLY"
        mix.inputs["Factor"].default_value = 1.0; mix.inputs[7].default_value = (*[min(1.0, c * 2.2) for c in CFG["bark"]], 1)
        if tex:
            tex[0].image = bpy.data.images.load(os.path.expanduser("~/assets3d/stylized_nature/Ultimate Stylized Nature - May 2022/glTF/NormalTree_Bark.jpg"))
            nt.links.new(tex[0].outputs[0], mix.inputs[6]); nt.links.new(mix.outputs[2], dif.inputs["Color"])

def glossy(name, col, rough=0.3, sss=0.0):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*col, 1); b.inputs["Roughness"].default_value = rough
    if sss: b.inputs["Subsurface Weight"].default_value = sss
    return m

# fuller crown: two inner copies of the foliage, smaller and turned, fill the gaps; a thicker trunk with root flare
import bmesh
mn0, mx0 = scene.bounds([tree]); H0 = mx0.z - mn0.z
bm = bmesh.new(); bm.from_mesh(tree.data)
leafset = {i for i, s_ in enumerate(tree.material_slots) if mat_kind(s_.material) == "leaf"}
lv = {v for f in bm.faces if f.material_index in leafset for v in f.verts}
if lv:
    c0 = sum((v.co for v in lv), mathutils.Vector()) / len(lv)
    for k, (sc_, ang) in enumerate(((0.9, 0.7), (0.78, 2.1))):
        geom = [f for f in bm.faces if f.material_index in leafset and not f.tag]
        dup = bmesh.ops.duplicate(bm, geom=geom)
        nv = [e for e in dup["geom"] if isinstance(e, bmesh.types.BMVert)]
        for f in dup["geom"]:
            if isinstance(f, bmesh.types.BMFace): f.tag = True
        rot = mathutils.Matrix.Rotation(ang, 3, "Z")
        for v in nv: v.co = c0 + rot @ ((v.co - c0) * sc_)
    crown_low = min(v.co.z for v in lv)
    for f in bm.faces:
        if f.material_index in leafset: continue
        for v in f.verts:
            if v.co.z < crown_low + (c0.z - crown_low) * 0.2 and not v.tag:
                t = max(0.0, 1 - v.co.z / max(1e-6, crown_low))
                k_ = 1.18 + 0.22 * t ** 4
                v.co.x *= k_; v.co.y *= k_; v.tag = True
bm.to_mesh(tree.data); bm.free(); tree.data.update()

# the crown's outer leaf faces, those the camera can see first
me = tree.data; mw = tree.matrix_world; n3 = mw.to_3x3()
leaf_idx = [i for i, s in enumerate(tree.material_slots) if mat_kind(s.material) == "leaf"]
polys = [p for p in me.polygons if p.material_index in leaf_idx]
cen = sum((mw @ p.center for p in polys), mathutils.Vector()) / len(polys)
cam_dir = mathutils.Vector((math.sin(math.radians(45)), -math.cos(math.radians(45)), 0.6)).normalized()
mn, mx = scene.bounds([tree]); H = mx.z - mn.z
cands = []
rnd = random.Random(5)
for p in polys:
    vs = [mw @ me.vertices[i].co for i in p.vertices]
    nrm = (n3 @ p.normal).normalized()
    area = p.area * (tree.scale[0] * tree.scale[2])
    for _ in range(max(1, int(area / (H * H) * 4000))):
        # a random point of the polygon (fan triangles)
        k = rnd.randrange(1, len(vs) - 1); u, v = rnd.random(), rnd.random()
        if u + v > 1: u, v = 1 - u, 1 - v
        c = vs[0] + (vs[k] - vs[0]) * u + (vs[k + 1] - vs[0]) * v
        r = c - cen
        n2 = nrm if nrm.dot(r) >= 0 else -nrm
        cands.append((c, r, n2))
dg = bpy.context.evaluated_depsgraph_get()
outer = [t for t in cands if not bpy.context.scene.ray_cast(dg, t[0] + cam_dir * H * 0.004, cam_dir)[0]]
print("visible leaf faces", len(outer), "of", len(cands))

def pick(n, front_bias, mind_k=0.055):
    pts = [t for t in outer if t[0].z > mn.z + H * 0.3 and t[2].dot(cam_dir) > 0.2]
    random.shuffle(pts); res = []
    mind = H * mind_k
    for t in pts:
        if all((t[0] - q[0]).length > mind for q in res): res.append(t)
        if len(res) >= n: break
    return res

def add_stem(top, length, rad, mat):
    bpy.ops.mesh.primitive_cylinder_add(radius=rad, depth=length, location=top + mathutils.Vector((0, 0, length / 2)), vertices=8)
    s = bpy.context.object; s.data.materials.append(mat); s.rotation_euler = (random.uniform(-.4, .4), random.uniform(-.4, .4), 0)

def smooth(o, levels=1):
    bpy.context.view_layer.objects.active = o; bpy.ops.object.shade_smooth()
    if levels: o.modifiers.new("s", "SUBSURF").levels = levels

stem_m = glossy("stem", (0.18, 0.1, 0.04), 0.7)
if state == "ripe":
    kind = CFG["fruit"]
    pts = pick(CFG["n_fruit"], -0.1)
    if kind == "apple":
        mats = [glossy("a1", (0.42, 0.01, 0.008), 0.25), glossy("a2", (0.5, 0.02, 0.008), 0.25), glossy("a3", (0.46, 0.03, 0.01), 0.27)]
        r = H * 0.036
        for c, rv, nrm in pts:
            loc = c - nrm * r * 0.35 - mathutils.Vector((0, 0, r * 0.4))
            bpy.ops.mesh.primitive_uv_sphere_add(radius=r * random.uniform(0.85, 1.1), location=loc, segments=24, ring_count=14)
            f = bpy.context.object; f.scale = (1, 1, 0.88)
            for v in f.data.vertices:  # dimple at the top and bottom
                zn = v.co.z / (r * 1.1)
                if abs(zn) > 0.8: v.co.z -= math.copysign(r * 0.18 * (abs(zn) - 0.8) / 0.2, zn)
            f.data.materials.append(random.choice(mats)); smooth(f, 0)
            add_stem(loc + mathutils.Vector((0, 0, r * 0.7)), r * 0.6, r * 0.07, stem_m)
    elif kind == "pear":
        mats = [glossy("p1", (0.55, 0.42, 0.04), 0.32), glossy("p2", (0.45, 0.45, 0.05), 0.32), glossy("p3", (0.6, 0.36, 0.04), 0.32)]
        r = H * 0.03
        for c, rv, nrm in pts:
            loc = c - nrm * r * 0.3 - mathutils.Vector((0, 0, r * 1.0))
            bpy.ops.mesh.primitive_uv_sphere_add(radius=r, location=loc, segments=24, ring_count=16)
            f = bpy.context.object
            for v in f.data.vertices:
                zn = v.co.z / r
                k = 1 - 0.5 * max(0.0, zn) ** 0.9
                v.co.x *= k; v.co.y *= k; v.co.z *= 1.45 if zn > 0 else 1.0
            f.rotation_euler = (random.uniform(-.25, .25), random.uniform(-.25, .25), 0)
            f.data.materials.append(random.choice(mats)); smooth(f, 0)
            add_stem(loc + mathutils.Vector((0, 0, r * 1.4)), r * 0.7, r * 0.08, stem_m)
    else:  # cherry pairs on a shared stalk
        m = glossy("c", (0.42, 0.0, 0.02), 0.18, 0.08)
        r = H * 0.021
        for c, rv, nrm in pts:
            top = c - nrm * r * 0.5
            for sgn in (-1, 1):
                off = mathutils.Vector((sgn * r * 1.15, sgn * r * 0.3, -r * 3.2 + random.uniform(-r, r) * 0.4))
                loc = top + off
                bpy.ops.mesh.primitive_uv_sphere_add(radius=r * random.uniform(0.9, 1.05), location=loc, segments=20, ring_count=12)
                f = bpy.context.object; f.data.materials.append(m); smooth(f, 0)
                d = (top - loc); L = d.length
                bpy.ops.mesh.primitive_cylinder_add(radius=r * 0.09, depth=L, location=(top + loc) / 2, vertices=6)
                s = bpy.context.object; s.rotation_euler = d.to_track_quat("Z", "Y").to_euler(); s.data.materials.append(stem_m)
elif state == "bloom":
    # five-petal blossoms with a yellow heart, in clusters, facing out of the crown
    pet = glossy("petal", CFG["bloom"], 0.5, 0.3); heart = glossy("heart", (0.9, 0.7, 0.1), 0.6)
    r = H * (0.011 if species == "pear" else 0.015)
    bpy.ops.mesh.primitive_circle_add(vertices=16, radius=r, fill_type="NGON", location=(0, 0, 0))
    p0 = bpy.context.object; p0.scale = (0.55, 1, 1); bpy.ops.object.transform_apply(scale=True)
    for v in p0.data.vertices: v.co.y += r * 0.85; v.co.z += (v.co.y / r) ** 2 * r * 0.12
    petals = [p0]
    for k in range(1, 5):
        q = p0.copy(); q.data = p0.data.copy(); bpy.context.scene.collection.objects.link(q)
        q.rotation_euler = (0, 0, k * 2 * math.pi / 5); petals.append(q)
    bpy.ops.mesh.primitive_uv_sphere_add(radius=r * 0.3, location=(0, 0, r * 0.08), segments=10, ring_count=6)
    hrt = bpy.context.object; hrt.data.materials.append(heart)
    for p in petals: p.data.materials.append(pet)
    bpy.ops.object.select_all(action="DESELECT")
    for o in petals + [hrt]: o.select_set(True)
    bpy.context.view_layer.objects.active = p0; bpy.ops.object.transform_apply(rotation=True); bpy.ops.object.join()
    flower = bpy.context.object; flower.name = "_flower_src"; flower.hide_render = True
    pts = pick(CFG["n_bloom"] // 3, -0.35, 0.045)
    for c, rv, nrm in pts:
        for k in range(random.randint(2, 3)):
            o = flower.copy(); bpy.context.scene.collection.objects.link(o); o.hide_render = False; o.name = "fl"
            jit = mathutils.Vector([random.uniform(-1, 1) for _ in range(3)]) * r * 2.2
            o.location = c + jit - nrm * r * 0.3
            q = nrm.to_track_quat("Z", "Y"); o.rotation_euler = q.to_euler()
            o.rotation_euler.rotate_axis("Z", random.uniform(0, 6.28)); o.rotation_euler.rotate_axis("X", random.uniform(-.5, .5))
            o.scale = [random.uniform(0.8, 1.15)] * 3

# grass tufts at the foot
GR = os.path.expanduser("~/assets3d/stylized_nature/Ultimate Stylized Nature - May 2022/glTF/Grass_Small.gltf")
for k, (dx, dy, rz) in enumerate(((-0.05, -0.04, 0.3), (0.06, -0.02, 2.0), (0.0, 0.05, 4.1))):
    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.gltf(filepath=GR)
    for o in set(bpy.context.scene.objects) - before:
        if o.parent is None:
            gmn, gmx = scene.bounds([m for m in [o] + list(o.children_recursive) if m.type == "MESH"])
            sz = H * 0.11 / max(1e-6, gmx.z - gmn.z)
            o.scale = (sz * 1.3, sz * 1.3, sz); o.location = (dx * H, dy * H, mn.z); o.rotation_euler = (0, 0, rz)
info = scene.setup(size=1024, foot=tuple(mw @ mathutils.Vector((0, 0, 0))), samples=64)
scene.render(out, info)
