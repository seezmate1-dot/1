# Farm 3D art (fruit trees)

The fruit trees on the farm map are rendered in Blender from free CC0 models by Quaternius
(https://quaternius.com, "Ultimate Stylized Nature" pack, public domain, no attribution required).

- `leaves.py` redraws each leaf card of the pack's leaf atlas as the species' own leaf (apple: broad serrated,
  pear: glossy pointed, cherry: long narrow) → `tex/<species>_<atlas>.png`.
- `tree3d.py species state variant out.png` builds the tree: species leaves, warm bark, a fuller crown, thicker
  trunk with root flare, grass tufts, and modelled apples / pears / cherry pairs or five-petal blossoms placed where
  the camera sees the crown; renders it with Cycles (iso camera, warm sun from the top left).
- `place.py` fits the render into the game's canvas (same size, foot point and height as before).
- `run_trees.sh` renders and places all 18 tree sprites.

Setup: `python3.11 -m venv ~/bpyenv && ~/bpyenv/bin/pip install bpy==4.2.0 gdown`; download the pack from the
Quaternius site into `~/assets3d/stylized_nature/`. Blender may segfault on exit after saving; that is harmless.
