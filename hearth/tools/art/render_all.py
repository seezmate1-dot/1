"""Renders all the art of the game into the Unity project (Resources/Art) and writes Art.json.

  blenv/bin/python hearth/tools/art/render_all.py [characters] [rooms] [fire] [world] [decor] [icons]

Needs Blender as a Python module (bpy), Pillow and the KayKit packs (KAYKIT, default
/home/user/kaykit-game-assets): adventurers, skeletons, dungeon remastered, furniture bits,
restaurant bits, halloween bits, medieval hexagon. All CC0.
"""
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
                   "unity", "LastHearth", "Assets", "LastHearth", "Resources", "Art")
MANIFEST = os.path.join(OUT, "Art.json")


def load_manifest():
    if os.path.exists(MANIFEST):
        with open(MANIFEST, encoding="utf-8") as f:
            return json.load(f)
    return {"sheets": {}, "rooms": {}, "fire": {}, "decor": {}}


def save_manifest(m):
    os.makedirs(OUT, exist_ok=True)
    with open(MANIFEST, "w", encoding="utf-8") as f:
        json.dump(m, f, ensure_ascii=False, indent=1, sort_keys=True)


def characters(m):
    import characters as c
    jobs = [("dweller", i, c.DWELLER_ANIMS) for i in range(len(c.DWELLERS))]
    jobs += [("hero", k, c.hero_anims(k)) for k in c.HEROES]
    jobs += [("creature", k, c.CREATURE_ANIMS) for k in c.CREATURES]
    for kind, key, anims in jobs:
        name = "%s%s" % (kind.capitalize(), key)
        start = time.time()
        info = c.sheet(kind, key, anims, os.path.join(OUT, "Sheets", name + ".png"))
        c.portrait(kind, key, os.path.join(OUT, "Portraits", name + ".png"))
        m["sheets"][name] = info
        save_manifest(m)
        print("sheet", name, round(time.time() - start), flush=True)


def rooms_(m):
    import rooms
    for name in rooms.RECIPES:
        start = time.time()
        rooms.render_room(name, os.path.join(OUT, "Rooms", name + ".png"), samples=64)
        m["rooms"][name] = {"cells": 2 if name == "Hearth" else 1}
        save_manifest(m)
        print("room", name, round(time.time() - start), flush=True)


def fire(m):
    import rooms
    m["fire"] = rooms.render_fire(os.path.join(OUT, "Fire"))
    save_manifest(m)


def world(m):
    import world as w
    w.render_mountain(os.path.join(OUT, "Mountain.png"))
    w.render_rock(os.path.join(OUT, "Rooms", "Rock.png"))
    w.render_frost(os.path.join(OUT, "Rooms", "Frost.png"))
    m["map"] = w.render_map(os.path.join(OUT, "Map.png"))
    save_manifest(m)


def icons(m):
    import icons as i
    i.render_all(os.path.join(OUT, "Icons"))


def decor(m):
    import decor as d
    m["decor"] = d.render_all(os.path.join(OUT, "Decor"))
    save_manifest(m)


if __name__ == "__main__":
    parts = [a for a in sys.argv[1:] if not a.startswith("-")] or ["characters", "rooms", "fire", "world", "decor", "icons"]
    m = load_manifest()
    for p in parts:
        {"characters": characters, "rooms": rooms_, "fire": fire, "world": world, "decor": decor, "icons": icons}[p](m)
    print("done", parts)
