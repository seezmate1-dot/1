import sys, os, bpy, mathutils
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import scene
scene.load(sys.argv[1]); info = scene.setup(size=320, samples=12); scene.render(sys.argv[2], info)
