"""Puts the painted pictures (sdpaint.py / aigen.py, staged in tools/art/ai/sd) into the game.

- Rooms/<Room>.png: the painted interiors (1024 x 512, the Hearth 2048 x 512).
- Rooms/Rock.png: the painted rock of the mountain (an undug cell).
- Rooms/Frame.png: the opening a room sits in, cut out of that rock (nine slices: GameRunner.View
  DrawInterior), with a dark inner shadow so the rooms look sunk into the mountain.
- Map.png, Mountain.png, Battlefield.png: the painted world.
- Splash.png: the loading screen (1920 x 1080), and "splash" in Art.json.
- Rooms/<Room>_2, _3: the richer looks by level, toned to the colours of the room's first look, and
  "roomTiers" in Art.json once every room has both.
- Rooms/Rubble, Rooms/Scaffold: drawn over a cell (obstacle) and over a room being built; "rubble", "scaffold".
- Icons/<Name>, Buttons/<Name> (256 x 256), Decor/<name> (320 high, its width in Art.json follows the picture).

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


def flag(name):
    manifest = os.path.join(ART, "Art.json")
    m = json.load(open(manifest, encoding="utf-8"))
    m[name] = True
    json.dump(m, open(manifest, "w", encoding="utf-8"), ensure_ascii=False, indent=1)


def tone(img, ref):
    """The colours of img moved to those of ref (mean and spread of each channel in Lab), half way:
    the richer looks keep their own light but share the palette of the room's first look."""
    import numpy as np
    a = np.asarray(img.convert("LAB"), dtype=np.float32)
    b = np.asarray(ref.convert("LAB"), dtype=np.float32)
    out = (a - a.mean((0, 1))) / (a.std((0, 1)) + 1e-3) * b.std((0, 1)) + b.mean((0, 1))
    out = a + (out - a) * 0.6
    return Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), "LAB").convert("RGB")


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


def main(stage):
    for piece in ("Door", "Ladder"):
        p = os.path.join(stage, "Rooms", piece + ".png")
        if os.path.exists(p):
            Image.open(p).save(os.path.join(ART, "Rooms", piece + ".png"))
    if all(os.path.exists(os.path.join(ART, "Rooms", n + ".png")) for n in ("Door", "Ladder")):
        flag("passages")
    rooms = os.path.join(stage, "Rooms")
    for f in sorted(os.listdir(rooms)) if os.path.isdir(rooms) else []:
        name = f[:-4]
        if name in ("Door", "Ladder", "Rubble", "Scaffold") or "_" in name:
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
            fit(Image.open(p).convert("RGB"), size).save(os.path.join(ART, name + ".png"))
            print("world", name)


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--from", dest="stage", default=STAGE)
    main(ap.parse_args().stage)
