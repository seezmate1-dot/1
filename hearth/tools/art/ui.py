"""The flat pieces of the interface, drawn with Pillow (the buttons, plates and windows are rendered
in 3D by uikit.py): wooden scroll bars cut from the Fantasy Props kit's wood, and the map's two
buttons: a folded parchment map and the fortress cut out of the rendered map.
Run: python hearth/tools/art/ui.py (Pillow only).
"""
import os
import sys

from PIL import Image, ImageDraw, ImageEnhance, ImageFilter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
QUATERNIUS = os.environ.get("QUATERNIUS", "/home/user/quaternius")
PROPS = os.path.join(QUATERNIUS, "fantasy-props-megakit", "Textures")
VILLAGE = os.path.join(QUATERNIUS, "medieval-village-megakit", "Medieval Village MegaKit[Standard]", "Textures")
OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
                   "unity", "LastHearth", "Assets", "LastHearth", "Resources", "Art", "UI")

IRON = (52, 46, 44)
GOLD = (236, 184, 74)
DARK = (34, 20, 12)


def _tex(folder, name):
    return Image.open(os.path.join(folder, name)).convert("RGB")


def _rounded_mask(size, radius):
    m = Image.new("L", size, 0)
    ImageDraw.Draw(m).rounded_rectangle((0, 0, size[0] - 1, size[1] - 1), radius=radius, fill=255)
    return m


def _bevel(img, depth=4, light=1.35, shade=0.6):
    """Lights the top-left edge and shades the bottom-right one: the plank stands out."""
    w, h = img.size
    hi = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(hi)
    for k in range(depth):
        a = int(255 * (1 - k / depth))
        d.line((k, k, w - 1 - k, k), fill=a)
        d.line((k, k, k, h - 1 - k), fill=a)
    lo = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(lo)
    for k in range(depth):
        a = int(255 * (1 - k / depth))
        d.line((k, h - 1 - k, w - 1 - k, h - 1 - k), fill=a)
        d.line((w - 1 - k, k, w - 1 - k, h - 1 - k), fill=a)
    bright = ImageEnhance.Brightness(img).enhance(light)
    dark = ImageEnhance.Brightness(img).enhance(shade)
    img = Image.composite(bright, img, hi)
    return Image.composite(dark, img, lo)


def _studs(img, color=IRON, r=4, inset=9):
    d = ImageDraw.Draw(img)
    w, h = img.size
    for x, y in ((inset, inset), (w - inset, inset), (inset, h - inset), (w - inset, h - inset)):
        d.ellipse((x - r, y - r, x + r, y + r), fill=color, outline=(20, 16, 14))
        d.ellipse((x - r + 1, y - r + 1, x - 1, y - 1), fill=tuple(min(255, c + 70) for c in color))
    return img


def plank(size=(192, 64), brightness=1.0, trim=None, studs=True, crop_y=80):
    """A wooden plank from the furniture trim sheet: bevelled, outlined, with studs or a gold trim."""
    # the top of the trim sheet is all planks (below ~y 900 there are other parts)
    k = min(4.0, (900 - crop_y) / size[1], 2000 / size[0])
    wood = _tex(PROPS, "T_Trim_Furniture_BaseColor.png").crop((40, crop_y, 40 + int(size[0] * k), crop_y + int(size[1] * k)))
    wood = wood.resize(size, Image.LANCZOS)
    wood = ImageEnhance.Brightness(wood).enhance(brightness)
    wood = ImageEnhance.Color(wood).enhance(1.15)
    wood = _bevel(wood)
    d = ImageDraw.Draw(wood)
    w, h = size
    d.rounded_rectangle((0, 0, w - 1, h - 1), radius=8, outline=DARK, width=3)
    if trim:
        d.rounded_rectangle((4, 4, w - 5, h - 5), radius=6, outline=trim, width=3)
    if studs:
        _studs(wood, GOLD if trim else IRON)
    out = Image.new("RGBA", size, (0, 0, 0, 0))
    out.paste(wood, (0, 0), _rounded_mask(size, 8))
    return out




def scroll_track(size=(18, 96)):
    img = Image.new("RGBA", size, (0, 0, 0, 0))
    ImageDraw.Draw(img).rounded_rectangle((3, 0, size[0] - 4, size[1] - 1), radius=6, fill=(26, 18, 14, 200), outline=(12, 8, 6, 255))
    return img


def scroll_thumb(size=(18, 64)):
    wood = plank((size[1], size[0]), brightness=1.1, studs=False).rotate(90, expand=True)
    return wood













def map_icon(size=192):
    """The "Карта" button: a folded parchment map, blue sea with green islands, a dotted red route
    to a cross."""
    import math
    w, h = int(size * 0.86), int(size * 0.66)
    paper = _tex(VILLAGE, "T_Plaster_BaseColor.png").crop((300, 300, 300 + w * 2, 300 + h * 2)).resize((w, h), Image.LANCZOS)
    paper = ImageEnhance.Color(paper).enhance(1.3)
    sea = Image.new("RGB", (w, h), (58, 128, 176))
    land = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(land)
    for cx, cy, rx, ry in ((0.2, 0.64, 0.15, 0.24), (0.52, 0.26, 0.14, 0.16), (0.8, 0.7, 0.12, 0.18)):
        d.polygon([((cx + rx * (1 + 0.22 * math.sin(3 * a + cx * 9) + 0.12 * math.sin(7 * a)) * math.cos(a)) * w,
                    (cy + ry * (1 + 0.22 * math.sin(3 * a + cx * 9) + 0.12 * math.sin(7 * a)) * math.sin(a)) * h)
                   for a in [i * math.pi / 24 for i in range(48)]], fill=255)
    land = land.filter(ImageFilter.GaussianBlur(2)).point(lambda v: 255 if v > 128 else 0)
    shore = land.filter(ImageFilter.MaxFilter(7))
    m = Image.composite(paper, sea, shore)
    m = Image.composite(Image.new("RGB", (w, h), (112, 168, 72)), m, land)
    m = Image.blend(m, paper, 0.18)
    d = ImageDraw.Draw(m)
    pts = [(0.2 + 0.6 * k, 0.64 - 0.5 * math.sin(math.pi * k) + 0.06 * k) for k in [i / 13 for i in range(14)]]
    for i, (x, y) in enumerate(pts[:-1]):
        if i % 2 == 0:
            d.ellipse((x * w - 3, y * h - 3, x * w + 3, y * h + 3), fill=(196, 40, 30))
    x, y = pts[-1][0] * w, pts[-1][1] * h
    for dx in (-1, 1):
        d.line((x - 9, y - 9 * dx, x + 9, y + 9 * dx), fill=(196, 40, 30), width=5)
    # three folds: the middle panel lighter, the sides darker
    for i, k in enumerate((0.95, 1.08, 0.88)):
        box = (i * w // 3, 0, (i + 1) * w // 3, h)
        m.paste(ImageEnhance.Brightness(m.crop(box)).enhance(k), box[:2])
    d = ImageDraw.Draw(m)
    d.rectangle((0, 0, w - 1, h - 1), outline=DARK, width=3)
    out = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    shadow = Image.new("RGBA", (w, h), (0, 0, 0, 120))
    out.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(3)), ((size - w) // 2 + 4, (size - h) // 2 + 6))
    out.paste(m.rotate(8, expand=False, resample=Image.BICUBIC), ((size - w) // 2, (size - h) // 2))
    return out


def fortress_icon(map_png, at, size=192):
    """The "Крепость" button: the fortress cut out of the rendered map, in a soft round frame."""
    src = Image.open(map_png).convert("RGBA")
    r = int(src.width * 0.1)
    x, y = int((at[0] + 0.02) * src.width), int(at[1] * src.height - r * 0.2)
    img = src.crop((x - r, y - r, x + r, y + r)).resize((size, size), Image.LANCZOS)
    mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(mask).ellipse((8, 8, size - 8, size - 8), fill=255)
    img.putalpha(mask.filter(ImageFilter.GaussianBlur(4)))
    return img


def map_buttons(out_dir):
    """The map's two buttons, next to the rendered ones (icons.py): the map, and the way home."""
    import json
    art = os.path.dirname(out_dir)
    map_icon().save(os.path.join(out_dir, "Map.png"))
    places = json.load(open(os.path.join(art, "Art.json"), encoding="utf-8")).get("map", {}).get("places", {})
    home = places.get("Fortress")
    if home:
        fortress_icon(os.path.join(art, "Map.png"), (home["x"], home["y"])).save(os.path.join(out_dir, "Fortress.png"))


def render_all(out_dir=OUT):
    """The flat pieces (the 3D ones are uikit.py's): wooden scroll bars, the map's two buttons."""
    os.makedirs(out_dir, exist_ok=True)
    scroll_track().save(os.path.join(out_dir, "ScrollTrack.png"))
    scroll_thumb().save(os.path.join(out_dir, "ScrollThumb.png"))
    map_buttons(os.path.join(os.path.dirname(out_dir), "Buttons"))
    print("ui written to", out_dir)


if __name__ == "__main__":
    render_all()
