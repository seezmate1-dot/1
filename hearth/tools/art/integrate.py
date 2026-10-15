"""Puts the painted pictures (sdpaint.py / aigen.py, staged in tools/art/ai/sd) into the game.

- Rooms/<Room>.png: the painted interiors (1024 x 512, the Hearth 2048 x 512).
- Rooms/Rock.png: the painted rock of the mountain (an undug cell).
- Rooms/Frame.png: the opening a room sits in, cut out of that rock (nine slices: GameRunner.View
  DrawInterior), with a dark inner shadow so the rooms look sunk into the mountain.
- Map.png, Mountain.png, Battlefield.png: the painted world.

    python hearth/tools/art/integrate.py [--from DIR]
"""
import argparse
import os

from PIL import Image, ImageChops, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.join(os.path.dirname(os.path.dirname(HERE)), "unity", "LastHearth", "Assets", "LastHearth", "Resources", "Art")
STAGE = os.path.join(HERE, "ai", "sd")

# the opening in a 768 x 384 cell (GameRunner.View.DrawInterior draws the interior at 0.055 h from the
# sides, 0.08 h from the top, 0.85 h tall): the hole is 4 px smaller, so the rock overlaps the edges
HOLE = (25, 35, 743, 353)


def frame(rock, size=(768, 384), radius=12):
    """The rock around a room's opening, transparent inside, shaded towards the hole."""
    W, H = size
    tex = rock.convert("RGB")
    k = max(W / tex.width, H / tex.height)
    tex = tex.resize((round(tex.width * k), round(tex.height * k)), Image.LANCZOS)
    tex = tex.crop(((tex.width - W) // 2, (tex.height - H) // 2, (tex.width - W) // 2 + W, (tex.height - H) // 2 + H))
    hole = Image.new("L", size, 0)
    ImageDraw.Draw(hole).rounded_rectangle(HOLE, radius=radius, fill=255)
    # darker towards the opening: the rock's edge falls away into the room
    shade = hole.filter(ImageFilter.MaxFilter(15)).filter(ImageFilter.GaussianBlur(9))
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


def main(stage):
    rooms = os.path.join(stage, "Rooms")
    for f in sorted(os.listdir(rooms)) if os.path.isdir(rooms) else []:
        name = f[:-4]
        img = Image.open(os.path.join(rooms, f)).convert("RGB")
        size = (2048, 512) if name == "Hearth" else (1024, 512)
        if name == "Rock":
            fit(img, (768, 384)).save(os.path.join(ART, "Rooms", "Rock.png"))
            frame(img).save(os.path.join(ART, "Rooms", "Frame.png"))
        else:
            fit(img, size).save(os.path.join(ART, "Rooms", f))
        print("room", name)
    for name, size in (("Map", (2400, 1350)), ("Mountain", (1536, 768)), ("Battlefield", (1536, 768))):
        p = os.path.join(stage, name + ".png")
        if os.path.exists(p):
            fit(Image.open(p).convert("RGB"), size).save(os.path.join(ART, name + ".png"))
            print("world", name)


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--from", dest="stage", default=STAGE)
    main(ap.parse_args().stage)
