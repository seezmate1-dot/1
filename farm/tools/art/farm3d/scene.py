"""Shared Blender scene setup: open/import a model, relink textures, iso ortho camera, warm sun from the top left,
soft sky, shadow catcher, Cycles. Used by the farm 3D builders."""
import bpy, math, mathutils, os, glob, json
from bpy_extras.object_utils import world_to_camera_view

TEXDIRS = glob.glob(os.path.expanduser("~/assets3d/*/*/glTF")) + glob.glob(os.path.expanduser("~/assets3d/*/*/Textures"))

def load(src):
    if src.endswith(".blend"):
        bpy.ops.wm.open_mainfile(filepath=src)
        for o in list(bpy.context.scene.objects):
            if o.type in ("CAMERA", "LIGHT"):
                bpy.data.objects.remove(o)
    else:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        (bpy.ops.import_scene.fbx if src.endswith(".fbx") else bpy.ops.import_scene.gltf)(filepath=src)
    for im in bpy.data.images:
        if not im.filepath or os.path.exists(bpy.path.abspath(im.filepath)):
            continue
        stem = os.path.splitext(os.path.basename(im.filepath))[0]
        for d in TEXDIRS:
            for ext in (".png", ".jpg"):
                p = os.path.join(d, stem + ext)
                if os.path.exists(p):
                    im.filepath = p; im.reload(); break

def meshes():
    return [o for o in bpy.context.scene.objects if o.type == "MESH" and not o.hide_render and not o.name.startswith("_")]

def bounds(objs):
    mn = mathutils.Vector((1e9,) * 3); mx = -mn
    for o in objs:
        for v in o.data.vertices:
            w = o.matrix_world @ v.co
            mn = mathutils.Vector(map(min, mn, w)); mx = mathutils.Vector(map(max, mx, w))
    return mn, mx

def setup(size=1024, ground_z=None, sun_energy=4.0, samples=64, foot=(0, 0, 0)):
    sc = bpy.context.scene
    mn, mx = bounds(meshes())
    ctr = (mn + mx) / 2; ext = (mx - mn).length
    gz = mn.z if ground_z is None else ground_z
    bpy.ops.mesh.primitive_plane_add(size=ext * 6, location=(ctr.x, ctr.y, gz))
    g = bpy.context.object; g.name = "_ground"; g.is_shadow_catcher = True
    cam = bpy.data.objects.new("_cam", bpy.data.cameras.new("_cam")); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.type = "ORTHO"; cam.data.ortho_scale = ext * 1.0
    el, az = math.radians(30), math.radians(45)
    d = ext * 3
    cam.location = ctr + mathutils.Vector((d * math.cos(el) * math.sin(az), -d * math.cos(el) * math.cos(az), d * math.sin(el)))
    cam.rotation_euler = (ctr - cam.location).to_track_quat("-Z", "Y").to_euler()
    sun = bpy.data.objects.new("_sun", bpy.data.lights.new("_sun", "SUN")); sc.collection.objects.link(sun)
    sun.data.energy = sun_energy; sun.data.color = (1.0, 0.94, 0.82); sun.data.angle = math.radians(22)
    lp = mathutils.Vector((-1.0, -0.6, math.tan(math.radians(70)))).normalized()
    sun.rotation_euler = (-lp).to_track_quat("-Z", "Y").to_euler()
    w = bpy.data.worlds.new("_w"); sc.world = w; w.use_nodes = True
    bg = w.node_tree.nodes["Background"]; bg.inputs[0].default_value = (0.78, 0.87, 1.0, 1); bg.inputs[1].default_value = 1.6
    sc.render.engine = "CYCLES"; sc.cycles.device = "CPU"; sc.cycles.samples = samples; sc.cycles.use_denoising = True
    sc.cycles.max_bounces = 6; sc.cycles.transparent_max_bounces = 24
    sc.render.film_transparent = True; sc.render.resolution_x = sc.render.resolution_y = size
    sc.view_settings.view_transform = "Standard"; sc.view_settings.look = "Medium High Contrast"
    bpy.context.view_layer.update()
    fp = world_to_camera_view(sc, cam, mathutils.Vector(foot))
    return dict(foot=[fp.x * size, (1 - fp.y) * size], size=size)

def render(out, info):
    sc = bpy.context.scene; sc.render.filepath = out
    bpy.ops.render.render(write_still=True)
    json.dump(info, open(os.path.splitext(out)[0] + ".json", "w"))
