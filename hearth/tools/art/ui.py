"""The look of the interface, cut from the hand-painted textures of the kits the rooms are built from
(Quaternius' Fantasy Props and Medieval Village MegaKits): wooden plank buttons with iron studs, a
gold-trimmed plank for the chosen tab, parchment panels in a wooden frame, a dark stone background,
wooden scroll bars. Written to Resources/Art/UI; the game stretches them as nine-slice images
(UiSkin.cs: the corners keep their size, the middle stretches).
Run: python hearth/tools/art/ui.py (Pillow only).
"""
import os
import sys

from PIL import Image, ImageChops, ImageDraw, ImageEnhance, ImageFilter

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


def parchment(size=(256, 256), frame=14):
    """A parchment panel (the plaster texture, warmed) in a frame of dark wood."""
    paper = _tex(VILLAGE, "T_Plaster_BaseColor.png").crop((200, 200, 200 + size[0] * 3, 200 + size[1] * 3)).resize(size, Image.LANCZOS)
    paper = ImageEnhance.Color(paper).enhance(1.25)
    paper = ImageEnhance.Brightness(paper).enhance(1.08)
    # darker towards the edges, like old paper
    vign = Image.new("L", size, 0)
    ImageDraw.Draw(vign).rounded_rectangle((frame, frame, size[0] - frame, size[1] - frame), radius=12, fill=255)
    vign = vign.filter(ImageFilter.GaussianBlur(14))
    paper = Image.composite(paper, ImageEnhance.Brightness(paper).enhance(0.72), vign)
    wood = plank((size[0], size[1]), brightness=0.62, studs=False, crop_y=20).convert("RGB")
    inner = _rounded_mask((size[0] - 2 * frame, size[1] - 2 * frame), 8)
    wood.paste(paper.crop((frame, frame, size[0] - frame, size[1] - frame)), (frame, frame), inner)
    d = ImageDraw.Draw(wood)
    d.rounded_rectangle((frame - 2, frame - 2, size[0] - frame + 1, size[1] - frame + 1), radius=9, outline=DARK, width=2)
    d.rounded_rectangle((0, 0, size[0] - 1, size[1] - 1), radius=10, outline=DARK, width=3)
    _studs(wood, GOLD, r=4, inset=7)
    out = Image.new("RGBA", size, (0, 0, 0, 0))
    out.paste(wood, (0, 0), _rounded_mask(size, 10))
    return out


def background(size=512):
    """Dark stone wall behind the whole interface (tiles)."""
    stone = _tex(VILLAGE, "T_UnevenBrick_BaseColor.png").resize((size, size), Image.LANCZOS)
    stone = ImageEnhance.Brightness(stone).enhance(0.3)
    tint = Image.new("RGB", stone.size, (60, 50, 70))
    return ImageChops.multiply(stone, ImageEnhance.Brightness(tint).enhance(3.2)).convert("RGBA")


def scroll_track(size=(18, 96)):
    img = Image.new("RGBA", size, (0, 0, 0, 0))
    ImageDraw.Draw(img).rounded_rectangle((3, 0, size[0] - 4, size[1] - 1), radius=6, fill=(26, 18, 14, 200), outline=(12, 8, 6, 255))
    return img


def scroll_thumb(size=(18, 64)):
    wood = plank((size[1], size[0]), brightness=1.1, studs=False).rotate(90, expand=True)
    return wood


def field(size=(128, 40)):
    paper = parchment((size[0], size[1]), frame=5)
    return paper


def render_all(out_dir=OUT):
    os.makedirs(out_dir, exist_ok=True)
    plank(brightness=1.0).save(os.path.join(out_dir, "Button.png"))
    plank(brightness=1.25).save(os.path.join(out_dir, "ButtonHover.png"))
    plank(brightness=0.72).save(os.path.join(out_dir, "ButtonActive.png"))
    plank(brightness=1.15, trim=GOLD).save(os.path.join(out_dir, "ButtonOn.png"))
    parchment().save(os.path.join(out_dir, "Panel.png"))
    background().save(os.path.join(out_dir, "Background.png"))
    scroll_track().save(os.path.join(out_dir, "ScrollTrack.png"))
    scroll_thumb().save(os.path.join(out_dir, "ScrollThumb.png"))
    field().save(os.path.join(out_dir, "Field.png"))
    print("ui written to", out_dir)


if __name__ == "__main__":
    render_all()
