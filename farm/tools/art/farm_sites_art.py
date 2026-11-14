#!/usr/bin/env python3
"""Pictures of the working farm on the map (docs/FARM_UX.md §4), drawn as vectors in the map's own projection and
rendered with resvg: the tilled plot, every crop in four steps, fruit trees by season, animals, the yards of the
animal houses, a building for every workshop, signs, the road and greenery of the street, and the small things of
the interface on the map (ring, bubble, bars, sparkle, sickle). Written to Resources/Art/Farm.

    python3 farm/tools/art/farm_sites_art.py          (needs resvg_py, numpy and Pillow)
    python3 farm/tools/art/farm_sites_art.py Tree_,Bush   (only the pictures whose names start so)

The drawing code is in farm/tools/art/farm_art: iso.py (the projection, solids, layers and their lighting),
crops.py, trees.py, animals.py, things.py.
"""
import glob
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "farm_art"))
import iso      # noqa: E402
import crops    # noqa: E402
import things   # noqa: E402

import trees    # noqa: E402
import animals  # noqa: E402


def jobs():
    """Every picture: (file name, module, function, arguments) — the function returns the picture's Art."""
    j = [("Plot", "crops", "plot_art", ()), ("PlotOffer", "crops", "offer_art", ()), ("Seeded", "crops", "seeded_art", ())]
    j += [("Crop_%s_%d" % (c, st), "crops", "crop_art", (c, st)) for c in crops.CROPS for st in (1, 2, 3, 4)]
    j += [("Tree_%s_%s%s" % (k, st, "" if v == 1 else "_2"), "trees", "tree", (k, st, v)) for k in trees.SPECIES for st in ("leaf", "bloom", "ripe") for v in (1, 2)]
    j += [("Animal_%s%s" % (k, "_ready" if rd else ""), "animals", "animal", (k, rd)) for k in animals.ANIMALS for rd in (False, True)]
    j += [("Pen_%s" % k, "things", "pen", (k,)) for k in ("cowshed", "sheepfold", "pigsty", "apiary")]
    j += [("Workshop_%s" % k, "things", "workshop", (k,)) for k in things.WS]
    j += [(n, "things", f, a) for (n, f, a) in (("Sign", "sign", ()), ("Ring", "ring", ()), ("Bubble", "bubble", ()), ("Spark", "spark", ()), ("BarBack", "bar", (False,)),
                                                ("BarFill", "bar", (True,)), ("Sickle", "sickle", ()), ("Tuft1", "tuft", (1,)), ("Tuft2", "tuft", (2,)), ("Bush1", "bush", (1,)),
                                                ("Bush2", "bush", (2, "#ff8ab0")), ("Bush3", "bush", (3, "#ffd23a")), ("Street", "street_ground", ()), ("Grass", "street_ground", (False,)))]
    return j


def _make(job):
    name, mod, fn, args = job
    getattr(sys.modules[mod], fn)(*args).save(name)
    return name


if __name__ == "__main__":
    from multiprocessing import Pool
    only = sys.argv[1].split(",") if len(sys.argv) > 1 else None      # name prefixes, to redraw a part
    todo = [j for j in jobs() if only is None or any(j[0].startswith(p) for p in only)]
    if only is None:
        for old in glob.glob(os.path.join(iso.OUT, "*.png")):
            os.remove(old)
    with Pool(os.cpu_count() or 2) as pool:
        for n in pool.imap_unordered(_make, todo):
            print(n, flush=True)
    print(len(todo), "pictures written to", iso.OUT)
