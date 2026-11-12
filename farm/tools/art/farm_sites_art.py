#!/usr/bin/env python3
"""Pictures of the working farm on the map (docs/FARM_UX.md §4), drawn as vectors in the map's own projection and
rendered with resvg: the tilled plot, every crop in four steps, fruit trees by season, animals, the yards of the
animal houses, a building for every workshop, signs, the road and greenery of the street, and the small things of
the interface on the map (ring, bubble, bars, sparkle, sickle). Written to Resources/Art/Farm.

    python3 farm/tools/art/farm_sites_art.py          (needs resvg_py and Pillow)

The drawing code is in farm/tools/art/farm_art: iso.py (the projection and solids), crops.py, things.py.
"""
import glob
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "farm_art"))
import iso      # noqa: E402
import crops    # noqa: E402
import things   # noqa: E402

if __name__ == "__main__":
    for old in glob.glob(os.path.join(iso.OUT, "*.png")):
        os.remove(old)
    made = dict(crops.build())
    made.update(things.build())
    print(len(made), "pictures written to", iso.OUT)
