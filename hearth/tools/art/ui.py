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
    """Warm stone wall behind the whole interface (tiles)."""
    stone = _tex(VILLAGE, "T_UnevenBrick_BaseColor.png").resize((size, size), Image.LANCZOS)
    stone = ImageEnhance.Brightness(stone).enhance(0.62)
    tint = Image.new("RGB", stone.size, (235, 190, 150))
    return ImageChops.multiply(stone, tint).convert("RGBA")


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


def badge(size=64):
    """The level of a room: a gold ring around a deep blue disc (the number is drawn by the game)."""
    img = Image.new("RGBA", (size * 4, size * 4), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    S = size * 4
    d.ellipse((4, 8, S - 4, S), fill=(0, 0, 0, 110))
    d.ellipse((0, 0, S - 8, S - 8), fill=(120, 76, 20, 255))
    d.ellipse((10, 10, S - 18, S - 18), fill=GOLD + (255,))
    d.ellipse((30, 30, S - 38, S - 38), fill=(36, 58, 120, 255))
    d.ellipse((44, 38, S - 52, S * 0.5), fill=(70, 100, 170, 255))
    return img.resize((size, size), Image.LANCZOS)


def bubble(size=96):
    """The cloud over a room with something to collect: a white disc with a soft shadow and a little
    tail (the game puts the resource's icon in its middle)."""
    S = size * 4
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    shadow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).ellipse((S * 0.1, S * 0.12, S * 0.94, S * 0.9), fill=(0, 0, 0, 110))
    img.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(10)))
    d = ImageDraw.Draw(img)
    d.polygon([(S * 0.4, S * 0.78), (S * 0.6, S * 0.78), (S * 0.5, S * 0.98)], fill=(255, 255, 255, 255))
    d.ellipse((S * 0.06, S * 0.06, S * 0.94, S * 0.86), fill=(255, 255, 255, 255), outline=(120, 90, 60, 255), width=10)
    return img.resize((size, size), Image.LANCZOS)


def bar(size=(512, 112)):
    """The bottom panel under the main screen's round buttons: dark wood with a gold top edge."""
    w, h = size
    wood = plank((w, h), brightness=0.55, studs=False, crop_y=20).convert("RGBA")
    d = ImageDraw.Draw(wood)
    d.rectangle((0, 0, w, 5), fill=GOLD + (255,))
    d.rectangle((0, 5, w, 8), fill=DARK + (255,))
    return wood


def plus(size=96):
    """Build here: a round wooden button with a gold rim and a fat green plus."""
    S = size * 4
    wood = plank((S, S), brightness=1.15, studs=False).convert("RGBA")
    mask = Image.new("L", (S, S), 0)
    ImageDraw.Draw(mask).ellipse((6, 6, S - 6, S - 6), fill=255)
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    img.paste(wood, (0, 0), mask)
    d = ImageDraw.Draw(img)
    d.ellipse((6, 6, S - 6, S - 6), outline=GOLD + (255,), width=18)
    d.ellipse((2, 2, S - 2, S - 2), outline=DARK + (255,), width=6)
    w, l = S * 0.13, S * 0.28
    for box in ((S / 2 - w, S / 2 - l, S / 2 + w, S / 2 + l), (S / 2 - l, S / 2 - w, S / 2 + l, S / 2 + w)):
        d.rounded_rectangle(box, radius=int(w * 0.6), fill=(110, 200, 70, 255), outline=(30, 70, 20, 255), width=8)
    return img.resize((size, size), Image.LANCZOS)


def pill(size=(128, 40)):
    """A dark rounded plate for a short text over the art (a timer)."""
    img = Image.new("RGBA", size, (0, 0, 0, 0))
    ImageDraw.Draw(img).rounded_rectangle((0, 0, size[0] - 1, size[1] - 1), radius=size[1] // 2, fill=(30, 20, 14, 210),
                                         outline=GOLD + (255,), width=2)
    return img


def round_button(size=128):
    """A round button: a deep blue disc in a gold rim (the icons of the main screen stand out on it)."""
    S = size * 4
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse((14, 26, S - 6, S - 2), fill=(0, 0, 0, 120))
    d.ellipse((4, 4, S - 4, S - 4), fill=DARK + (255,))
    d.ellipse((10, 10, S - 10, S - 10), fill=(196, 140, 52, 255))
    d.ellipse((22, 22, S - 22, S - 22), fill=GOLD + (255,))
    inner = Image.new("RGBA", (S - 80, S - 80), (0, 0, 0, 0))
    di = ImageDraw.Draw(inner)
    n = S - 80
    for k in range(60):
        f = k / 60
        col = (int(30 + 40 * f), int(58 + 60 * f), int(120 + 70 * f), 255)
        pad = int(f * n * 0.35)
        di.ellipse((pad, pad * 0.6, n - pad, n - pad * 1.4), fill=col)
    mask = Image.new("L", (n, n), 0)
    ImageDraw.Draw(mask).ellipse((0, 0, n, n), fill=255)
    img.paste(inner, (40, 40), mask)
    return img.resize((size, size), Image.LANCZOS)


def window(size=(512, 384), frame=26):
    """A window over the fortress: a dark wooden board in a gold-studded frame; light text reads on it."""
    w, h = size
    board = plank((w, h), brightness=0.42, studs=False, crop_y=20).convert("RGB")
    inner = Image.new("RGB", (w - 2 * frame, h - 2 * frame), (44, 28, 18))
    grain = plank((w - 2 * frame, h - 2 * frame), brightness=0.3, studs=False, crop_y=300).convert("RGB")
    inner = Image.blend(inner, grain, 0.45)
    board.paste(inner, (frame, frame), _rounded_mask(inner.size, 10))
    d = ImageDraw.Draw(board)
    d.rounded_rectangle((frame - 3, frame - 3, w - frame + 2, h - frame + 2), radius=12, outline=GOLD, width=3)
    d.rounded_rectangle((0, 0, w - 1, h - 1), radius=16, outline=DARK, width=4)
    d.rounded_rectangle((5, 5, w - 6, h - 6), radius=13, outline=(150, 100, 40), width=2)
    _studs(board, GOLD, r=6, inset=13)
    out = Image.new("RGBA", size, (0, 0, 0, 0))
    out.paste(board, (0, 0), _rounded_mask(size, 16))
    return out


def close_button(size=96):
    """The round red button with a white cross that closes a window."""
    S = size * 4
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse((10, 20, S - 2, S - 2), fill=(0, 0, 0, 120))
    d.ellipse((4, 4, S - 12, S - 12), fill=(180, 40, 30, 255), outline=GOLD + (255,), width=18)
    d.ellipse((40, 30, S - 60, S * 0.45), fill=(215, 80, 60, 255))
    c, r = (S - 8) / 2, S * 0.2
    for a, b in (((c - r, c - r), (c + r, c + r)), ((c - r, c + r), (c + r, c - r))):
        d.line((a, b), fill=(255, 245, 230, 255), width=int(S * 0.1))
    return img.resize((size, size), Image.LANCZOS)


def dot(size=32):
    """The red dot on a button: something is waiting there."""
    S = size * 4
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse((4, 4, S - 4, S - 4), fill=(225, 40, 30, 255), outline=(255, 240, 220, 255), width=12)
    return img.resize((size, size), Image.LANCZOS)


def gear(size=128):
    """Settings: a gold gear on the round wooden button."""
    import math
    img = round_button(size).resize((size * 4, size * 4), Image.LANCZOS)
    d = ImageDraw.Draw(img)
    S = size * 4
    c = S / 2
    teeth = []
    for k in range(16):
        a = k * math.pi / 8
        r = S * (0.3 if k % 2 == 0 else 0.23)
        for da in (-0.17, 0.17):
            teeth.append((c + r * math.cos(a + da), c + r * math.sin(a + da)))
    d.polygon(teeth, fill=GOLD + (255,), outline=DARK + (255,))
    d.ellipse((c - S * 0.09, c - S * 0.09, c + S * 0.09, c + S * 0.09), fill=(60, 36, 18, 255))
    return img.resize((size, size), Image.LANCZOS)


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
    badge().save(os.path.join(out_dir, "Badge.png"))
    bubble().save(os.path.join(out_dir, "Bubble.png"))
    plus().save(os.path.join(out_dir, "Plus.png"))
    pill().save(os.path.join(out_dir, "Pill.png"))
    round_button().save(os.path.join(out_dir, "Round.png"))
    bar().save(os.path.join(out_dir, "Bar.png"))
    window().save(os.path.join(out_dir, "Window.png"))
    close_button().save(os.path.join(out_dir, "Close.png"))
    dot().save(os.path.join(out_dir, "Dot.png"))
    gear().save(os.path.join(out_dir, "Gear.png"))
    print("ui written to", out_dir)


if __name__ == "__main__":
    render_all()
