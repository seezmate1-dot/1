"""Puts the painted pictures (sdpaint.py / aigen.py, staged in tools/art/ai/sd) into the game.

- Rooms/<Room>.png: the painted interiors (1024 x 512, the Hearth 2048 x 512).
- Rooms/Rock.png: the painted rock of the mountain (an undug cell).
- Rooms/Frame.png: the opening a room sits in, cut out of that rock (nine slices: GameRunner.View
  DrawInterior), with a dark inner shadow so the rooms look sunk into the mountain.
- Map.png, Mountain.png, Battlefield.png: the painted world; on the map's water the points of the sun
  glints (map.sea in Art.json) and their picture UI/Glint.png.
- Splash.png: the loading screen (1920 x 1080), and "splash" in Art.json.
- Rooms/<Room>_2, _3: the richer looks by level, toned to the colours of the room's first look, and
  "roomTiers" in Art.json once every room has both.
- Rooms/Stairs: the stair tower beside the cutaway; Rooms/Arch, the open arch between rooms, cut here
  from the painted rock (the model kept painting a closed door into it); "stairs".
- Rooms/Rubble, Rooms/Scaffold: drawn over a cell (obstacle) and over a room being built; "rubble", "scaffold".
- Icons/<Name>, Buttons/<Name> (256 x 256), Decor/<name> (320 high, its width in Art.json follows the picture).
- Items/<Kind>: the equipment icons (256 x 256), "itemArt" once every kind of sdpaint.ITEMS is painted.
- Pets/<Id>, Companions/<Id>: portraits (256 x 256, standing on the bottom); "petArt", "companionArt".
- Scene/<Place>: the places around the fortress (512 x 512, standing on the bottom), and "scene" in Art.json.

    python hearth/tools/art/integrate.py [--from DIR]
"""
import argparse
import json
import os

from PIL import Image, ImageChops, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.join(os.path.dirname(os.path.dirname(HERE)), "unity", "LastHearth", "Assets", "LastHearth", "Resources", "Art")
STAGE = os.path.join(HERE, "ai", "sd")

# the opening in a 768 x 384 cell (GameRunner.View.DrawInterior draws the interior at 0.03 h from the
# sides, 0.06 h from the top, 0.88 h tall): the hole is 4 px smaller, so the rock overlaps the edges
HOLE = (16, 27, 752, 357)


def frame(rock, size=(768, 384), radius=8):
    """The rock around a room's opening, transparent inside, shaded towards the hole."""
    W, H = size
    tex = rock.convert("RGB")
    k = max(W / tex.width, H / tex.height)
    tex = tex.resize((round(tex.width * k), round(tex.height * k)), Image.LANCZOS)
    tex = tex.crop(((tex.width - W) // 2, (tex.height - H) // 2, (tex.width - W) // 2 + W, (tex.height - H) // 2 + H))
    hole = Image.new("L", size, 0)
    ImageDraw.Draw(hole).rounded_rectangle((HOLE[0], HOLE[1], W - (768 - HOLE[2]), H - (384 - HOLE[3])), radius=radius, fill=255)
    # darker towards the opening: the rock's edge falls away into the room
    shade = hole.filter(ImageFilter.MaxFilter(9)).filter(ImageFilter.GaussianBlur(5))
    dark = Image.new("RGB", size, (24, 14, 8))
    tex = Image.composite(dark, tex, shade.point(lambda v: int(v * 0.7)))
    # a thin dark lip right at the edge, a soft light rim on the floor's edge below the opening
    edge = ImageChops.subtract(hole.filter(ImageFilter.MaxFilter(5)), hole)
    tex = Image.composite(Image.new("RGB", size, (16, 9, 5)), tex, edge)
    out = tex.convert("RGBA")
    out.putalpha(ImageChops.invert(hole))
    return out


def fit(src, size):
    """Scaled to cover the size, centred, cropped."""
    k = max(size[0] / src.width, size[1] / src.height)
    img = src.resize((round(src.width * k), round(src.height * k)), Image.LANCZOS)
    x, y = (img.width - size[0]) // 2, (img.height - size[1]) // 2
    return img.crop((x, y, x + size[0], y + size[1]))


def arch(rock, size=(192, 512)):
    """The open arch between two rooms, cut from the painted rock: a stone pillar as high as the rooms'
    openings with a round-topped passage through it, dark inside with warm light on its threshold, a ring
    of lighter cut stones around the opening and soft shading at its sides."""
    import numpy as np
    W, H = size
    tex = fit(rock.convert("RGB"), (W * 2, H)).crop((W // 2, 0, W // 2 + W, H))
    t = np.asarray(tex, dtype=np.float32)
    y, x = np.mgrid[0:H, 0:W].astype(np.float32)
    cx, half, top = W / 2, W * 0.30, H * 0.30           # the passage: 0.6 of the width, its arch from 0.30 of the height
    spring = top + half                                  # where the round top meets the straight sides
    # signed distance outside the opening (negative inside)
    d_side = np.abs(x - cx) - half
    d_round = np.hypot(x - cx, y - spring) - half
    d = np.where(y < spring, d_round, d_side)
    ring = np.clip(1 - np.abs(d - W * 0.06) / (W * 0.06), 0, 1)  # the cut stones around the opening
    stone = t * (1 + 0.22 * ring[..., None])
    # the sides of the pillar turn away from the light
    side = np.clip(np.minimum(x, W - 1 - x) / (W * 0.18), 0, 1)
    stone *= (0.62 + 0.38 * side)[..., None]
    # the passage: dark, a warm glow on its floor, darker towards the arch
    depth = np.clip((y - top) / (H - top), 0, 1)
    glow = np.clip((depth - 0.7) / 0.3, 0, 1) ** 1.5
    passage = np.stack([22 + 120 * glow, 14 + 72 * glow, 9 + 30 * glow], -1)
    inside = np.clip(-d / 3, 0, 1)[..., None]
    out = stone * (1 - inside) + passage * inside
    # a dark lip just inside the opening
    lip = np.clip(1 - np.abs(d + 2.5) / 2.5, 0, 1)[..., None] * 0.6
    out = out * (1 - lip)
    rgba = np.dstack([np.clip(out, 0, 255), np.full((H, W), 255.0)]).astype(np.uint8)
    img = Image.fromarray(rgba, "RGBA")
    # the pillar's edges fade a little into the rock frame
    a = Image.new("L", size, 0)
    ImageDraw.Draw(a).rectangle((3, 0, W - 4, H), fill=255)
    img.putalpha(a.filter(ImageFilter.GaussianBlur(2)))
    return img


# where the cradle stands in the living quarters (centre x, floor y, height; fractions of the picture)
CRADLE = {"Bedrooms": (0.80, 0.74, 0.21), "Bedrooms_2": (0.64, 0.83, 0.23), "Bedrooms_3": (0.64, 0.83, 0.23)}


def place(room, prop, cx, floor, height):
    """A painted prop set on the floor of a room picture, with a soft shadow under it."""
    W, H = room.size
    prop = prop.convert("RGBA")
    box = prop.getchannel("A").point(lambda v: 255 if v > 24 else 0).getbbox()
    prop = prop.crop(box) if box else prop
    h = round(H * height)
    prop = prop.resize((max(1, round(prop.width * h / prop.height)), h), Image.LANCZOS)
    x, y = round(W * cx - prop.width / 2), round(H * floor - prop.height)
    out = room.convert("RGBA")
    shadow = Image.new("L", room.size, 0)
    ImageDraw.Draw(shadow).ellipse((x + prop.width * 0.05, y + prop.height * 0.9, x + prop.width * 0.95, y + prop.height * 1.06), fill=110)
    out = Image.composite(Image.new("RGBA", room.size, (20, 12, 6, 255)), out, shadow.filter(ImageFilter.GaussianBlur(H * 0.012)))
    out.alpha_composite(prop, (x, y))
    return out.convert("RGB")


def flag(name):
    manifest = os.path.join(ART, "Art.json")
    m = json.load(open(manifest, encoding="utf-8"))
    m[name] = True
    json.dump(m, open(manifest, "w", encoding="utf-8"), ensure_ascii=False, indent=1)


def tone(img, ref, amount=0.5):
    """The colours of img moved towards those of ref (mean and spread of each channel), half way:
    the richer looks keep their own light but share the palette of the room's first look."""
    import numpy as np
    a = np.asarray(img.convert("RGB"), dtype=np.float32)
    b = np.asarray(ref.convert("RGB"), dtype=np.float32)
    out = (a - a.mean((0, 1))) / (a.std((0, 1)) + 1e-3) * b.std((0, 1)) + b.mean((0, 1))
    out = a + (out - a) * amount
    return Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), "RGB")


def trim(img, size, bottom=False, margin=6):
    """A transparent piece cut to what it shows and fitted into size (centred, or standing on the bottom)."""
    img = img.convert("RGBA")
    box = img.getchannel("A").point(lambda v: 255 if v > 24 else 0).getbbox()
    if box:
        img = img.crop(box)
    k = min((size[0] - 2 * margin) / img.width, (size[1] - 2 * margin) / img.height)
    img = img.resize((max(1, round(img.width * k)), max(1, round(img.height * k))), Image.LANCZOS)
    out = Image.new("RGBA", size, (0, 0, 0, 0))
    y = size[1] - margin - img.height if bottom else (size[1] - img.height) // 2
    out.alpha_composite(img, ((size[0] - img.width) // 2, y))
    return out


def pieces(stage):
    """Icons, button pictures and decorations (transparent), and the rubble and scaffolding over the cells."""
    for group, size in (("Icons", (256, 256)), ("Buttons", (256, 256))):
        d = os.path.join(stage, group)
        for f in sorted(os.listdir(d)) if os.path.isdir(d) else []:
            if os.path.exists(os.path.join(ART, group, f)):
                trim(Image.open(os.path.join(d, f)), size).save(os.path.join(ART, group, f))
                print(group, f[:-4])
    d = os.path.join(stage, "Decor")
    manifest = os.path.join(ART, "Art.json")
    m = json.load(open(manifest, encoding="utf-8"))
    for f in sorted(os.listdir(d)) if os.path.isdir(d) else []:
        name = f[:-4]
        if name not in m.get("decor", {}):
            continue
        img = Image.open(os.path.join(d, f)).convert("RGBA")
        box = img.getchannel("A").point(lambda v: 255 if v > 24 else 0).getbbox()
        img = img.crop(box) if box else img
        k = 320 / max(img.width, img.height)
        img = img.resize((max(1, round(img.width * k)), max(1, round(img.height * k))), Image.LANCZOS)
        img.save(os.path.join(ART, "Decor", f))
        # the game draws a decoration as tall as it is in metres and as wide as the picture says
        m["decor"][name]["width"] = round(m["decor"][name]["height"] * img.width / img.height, 3)
        print("decor", name)
    json.dump(m, open(manifest, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    # the portraits of the pets and of the companions: all of a kind, or the windows keep their text
    for group, names, key in (("Pets", ("Wolf", "Owl", "CoalCat", "Raven", "BearCub", "MistFox"), "petArt"),
                              ("Companions", ("Squire", "Wolfhound", "Falcon", "Firefly"), "companionArt")):
        d = os.path.join(stage, group)
        if all(os.path.exists(os.path.join(d, n + ".png")) for n in names):
            os.makedirs(os.path.join(ART, group), exist_ok=True)
            for n in names:
                trim(Image.open(os.path.join(d, n + ".png")), (256, 256), bottom=True, margin=4).save(os.path.join(ART, group, n + ".png"))
            flag(key)
            print(group, " ".join(names))
    # the icons of the equipment (GameRunner.Gear): all kinds the painter knows, or plates with names stay
    import re
    src = open(os.path.join(HERE, "sdpaint.py"), encoding="utf-8").read()
    block = src[src.index("ITEMS = {"):src.index("}", src.index("ITEMS = {"))]
    kinds = re.findall(r'"(\w+)":', block)
    d = os.path.join(stage, "Items")
    if kinds and all(os.path.exists(os.path.join(d, k + ".png")) for k in kinds):
        os.makedirs(os.path.join(ART, "Items"), exist_ok=True)
        for k in kinds:
            trim(Image.open(os.path.join(d, k + ".png")), (256, 256)).save(os.path.join(ART, "Items", k + ".png"))
        flag("itemArt")
        print("Items", " ".join(kinds))
    # the places around the fortress (GameRunner.Scene): all six, or the side buttons stay
    d = os.path.join(stage, "Scene")
    places = ("Merchant", "Portal", "Arena", "Board", "Pier", "Pen")
    if all(os.path.exists(os.path.join(d, n + ".png")) for n in places):
        os.makedirs(os.path.join(ART, "Scene"), exist_ok=True)
        for n in places:
            trim(Image.open(os.path.join(d, n + ".png")), (512, 512), bottom=True, margin=2).save(os.path.join(ART, "Scene", n + ".png"))
        flag("scene")
        print("scene", " ".join(places))
    for name, bottom in (("Rubble", True), ("Scaffold", False)):
        p = os.path.join(stage, "Rooms", name + ".png")
        if os.path.exists(p):
            img = Image.open(p).convert("RGBA")
            if bottom:
                # a heap on the floor of the cell, two thirds of it wide
                out = Image.new("RGBA", (768, 384), (0, 0, 0, 0))
                out.alpha_composite(trim(img, (560, 300), bottom=True, margin=0), (104, 384 - 300 - 30))
            else:
                # scaffolding over the whole opening
                box = img.getchannel("A").getbbox()
                out = (img.crop(box) if box else img).resize((1024, 512), Image.LANCZOS)
            out.save(os.path.join(ART, "Rooms", name + ".png"))
            flag(name.lower())
            print("room", name)


def glint(path):
    """A soft four-pointed sun glint (white, transparent around), for the map's water."""
    import numpy as np
    n = 64
    y, x = np.mgrid[0:n, 0:n] / (n - 1) * 2 - 1
    star = np.exp(-(x * 6) ** 2 - (y * 1.2) ** 2 * 3) + np.exp(-(y * 6) ** 2 - (x * 1.2) ** 2 * 3)
    a = np.clip(np.exp(-(x ** 2 + y ** 2) * 8) * 0.9 + star * 0.6, 0, 1)
    img = Image.new("RGBA", (n, n), (255, 255, 255, 0))
    img.putalpha(Image.fromarray((a * 255).astype("uint8"), "L"))
    img.save(path)


def sea_points(img, count=70, seed=3):
    """Points on the open water of the map (blue, away from the shores and the ships), spread over it."""
    import random
    import numpy as np
    a = np.asarray(img.convert("RGB").resize((240, 135)), dtype=np.float32)
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    water = (b > r + 35) & (b > g + 5) & (b > 90)
    # away from anything that is not water: eroded by a few cells
    m = Image.fromarray((water * 255).astype("uint8"), "L").filter(ImageFilter.MinFilter(9))
    ys, xs = np.nonzero(np.asarray(m) > 0)
    rng = random.Random(seed)
    picked = []
    for i in rng.sample(range(len(xs)), min(len(xs), count * 20)):
        x, y = (xs[i] + 0.5) / 240, (ys[i] + 0.5) / 135
        if all((x - px) ** 2 + ((y - py) * 9 / 16) ** 2 > 0.0016 for px, py in picked):
            picked.append((x, y))
        if len(picked) == count:
            break
    return [{"name": "", "x": round(x, 4), "y": round(y, 4)} for x, y in picked]


def ship_lanes(img, count=3, seed=5):
    """Lanes for the little ships: straight runs between two points of open water that stay on the water all the
    way (sampled every few pixels), a fifth to a third of the map long, not crossing each other's middles."""
    import random
    import numpy as np
    small = img.convert("RGB").resize((240, 135))
    a = np.asarray(small, dtype=np.float32)
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    water = (b > r + 35) & (b > g + 5) & (b > 90)
    m = np.asarray(Image.fromarray((water * 255).astype("uint8"), "L").filter(ImageFilter.MinFilter(7))) > 0
    ys, xs = np.nonzero(m)
    rng = random.Random(seed)
    lanes = []
    for _ in range(20000):
        i, j = rng.randrange(len(xs)), rng.randrange(len(xs))
        x0, y0, x1, y1 = xs[i], ys[i], xs[j], ys[j]
        length = ((x1 - x0) ** 2 + (y1 - y0) ** 2) ** 0.5
        if not 40 <= length <= 85 or abs(y1 - y0) > abs(x1 - x0) * 0.6:
            continue
        # away from the map's edges, where the view seldom goes
        if min(y0, y1) < 14 or max(y0, y1) > 121 or min(x0, x1) < 8 or max(x0, x1) > 232:
            continue
        steps = int(length)
        if not all(m[round(y0 + (y1 - y0) * k / steps), round(x0 + (x1 - x0) * k / steps)] for k in range(steps + 1)):
            continue
        mx, my = (x0 + x1) / 2, (y0 + y1) / 2
        if any(((mx - (l[0] + l[2]) / 2) ** 2 + (my - (l[1] + l[3]) / 2) ** 2) ** 0.5 < 55 for l in lanes):
            continue
        lanes.append((x0, y0, x1, y1))
        if len(lanes) == count:
            break
    return [[{"name": "", "x": round(float(x0 + 0.5) / 240, 4), "y": round(float(y0 + 0.5) / 135, 4)},
             {"name": "", "x": round(float(x1 + 0.5) / 240, 4), "y": round(float(y1 + 0.5) / 135, 4)}] for x0, y0, x1, y1 in lanes]


def main(stage):
    for piece in ("Door", "Ladder"):
        p = os.path.join(stage, "Rooms", piece + ".png")
        if os.path.exists(p):
            Image.open(p).save(os.path.join(ART, "Rooms", piece + ".png"))
    if all(os.path.exists(os.path.join(ART, "Rooms", n + ".png")) for n in ("Door", "Ladder")):
        flag("passages")
    # the stair tower beside the cutaway and the open arches between the rooms (GameRunner.View DrawStairTower)
    stairs, rock = (os.path.join(stage, "Rooms", n + ".png") for n in ("Stairs", "Rock"))
    if os.path.exists(stairs) and os.path.exists(rock):
        fit(Image.open(stairs).convert("RGB"), (512, 512)).save(os.path.join(ART, "Rooms", "Stairs.png"))
        arch(Image.open(rock)).save(os.path.join(ART, "Rooms", "Arch.png"))
        flag("stairs")
        print("stairs and arches")
    rooms = os.path.join(stage, "Rooms")
    for f in sorted(os.listdir(rooms)) if os.path.isdir(rooms) else []:
        name = f[:-4]
        if name in ("Door", "Ladder", "Rubble", "Scaffold", "Stairs", "Arch", "Cradle") or "_" in name:
            continue
        img = Image.open(os.path.join(rooms, f)).convert("RGB")
        size = (2048, 512) if name == "Hearth" else (1024, 512)
        if name == "Rock":
            fit(img, (768, 384)).save(os.path.join(ART, "Rooms", "Rock.png"))
            frame(img).save(os.path.join(ART, "Rooms", "Frame.png"))
            flag("framed")
        else:
            fit(img, size).save(os.path.join(ART, "Rooms", f))
        print("room", name)
    tiers = sorted(f[:-4] for f in os.listdir(rooms) if "_" in f) if os.path.isdir(rooms) else []
    for name in tiers:
        base = name.partition("_")[0]
        img = fit(Image.open(os.path.join(rooms, name + ".png")).convert("RGB"), (1024, 512))
        ref = os.path.join(rooms, base + ".png")
        if os.path.exists(ref):
            img = tone(img, Image.open(ref).convert("RGB"))
        img.save(os.path.join(ART, "Rooms", name + ".png"))
        print("room", name)
    cradle = os.path.join(rooms, "Cradle.png")
    if os.path.exists(cradle):
        # a family lives here: one double bed and a cradle beside it
        for name, spot in CRADLE.items():
            p = os.path.join(ART, "Rooms", name + ".png")
            if os.path.exists(p):
                place(Image.open(p), Image.open(cradle), *spot).save(p)
                print("cradle", name)
    have = set(f[:-4] for f in os.listdir(os.path.join(ART, "Rooms")))
    types = [n for n in json.load(open(os.path.join(ART, "Art.json"), encoding="utf-8")).get("rooms", {})
             if n not in ("Empty", "Hearth")]
    if types and all(n + "_2" in have and n + "_3" in have for n in types):
        flag("roomTiers")
    pieces(stage)
    splash = os.path.join(stage, "Splash.png")
    if os.path.exists(splash):
        # the loading screen's key art (LoadingScreen.cs shows it when the manifest says so)
        fit(Image.open(splash).convert("RGB"), (1920, 1080)).save(os.path.join(ART, "Splash.png"))
        flag("splash")
        print("splash")
    for name, size in (("Map", (2400, 1350)), ("Mountain", (1536, 768)), ("Battlefield", (1536, 768))):
        p = os.path.join(stage, name + ".png")
        if os.path.exists(p):
            img = fit(Image.open(p).convert("RGB"), size)
            img.save(os.path.join(ART, name + ".png"))
            print("world", name)
            if name == "Map":
                # the sun glints on the water (GameRunner.Map DrawSea)
                glint(os.path.join(ART, "UI", "Glint.png"))
                manifest = os.path.join(ART, "Art.json")
                m = json.load(open(manifest, encoding="utf-8"))
                m.setdefault("map", {})["sea"] = sea_points(img)
                ship = os.path.join(stage, "Map", "Ship.png")
                if os.path.exists(ship):
                    # a little ship sails the lanes (GameRunner.Map DrawShips)
                    os.makedirs(os.path.join(ART, "Map"), exist_ok=True)
                    trim(Image.open(ship), (192, 192), margin=2).save(os.path.join(ART, "Map", "Ship.png"))
                    m["map"]["ships"] = ship_lanes(img)
                    m["mapShips"] = True
                json.dump(m, open(manifest, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
                print("sea", len(m["map"]["sea"]))


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--from", dest="stage", default=STAGE)
    main(ap.parse_args().stage)
