#!/usr/bin/env python3
"""00_overview.png: the whole working farm (the «show» farm: every crop at every age, trees in leaf, bloom and fruit,
animals hungry, fed and with products, workshops working and done), and close views of its parts.
00_overview_1to1_*.png: parts of it at one pixel per map unit — the backgrounds' own resolution, the size things
have on a phone at zoom 1. sprites.png: every picture of Resources/Art/Farm at that in-game size, and the farm's
icons. sprites_100_*.jpg: every picture at 100% of its own pixels (3–4 per map unit), on the map's grass.

    FARM_SITES_DUMP=farm/docs/farm-ux/data dotnet test farm/ui-tests --filter DumpSites
    python3 farm/docs/farm-ux/overview.py
"""
import glob
import os
from PIL import Image, ImageDraw, ImageFont
import farmview as fv

HERE = os.path.dirname(os.path.abspath(__file__))
INK = (90, 53, 18)
PAPER = (250, 244, 230)


def view(rows, cx, cy, w, h, ms):
    """A window of the map, w × h map units around (cx, cy), with the farm on it."""
    x0, y1 = cx - w / 2, cy + h / 2
    base = fv.base_map(.5)
    crop = base.crop((int(x0 * .5), int((fv.MAPH - y1) * .5), int((x0 + w) * .5), int((fv.MAPH - y1 + h) * .5))).resize((int(w * ms), int(h * ms)), Image.LANCZOS)
    p = fv.Painter(crop, ms, x0, y1)
    fv.paint_farm(p, rows)
    return crop


def rounded(im, r=22):
    mask = Image.new("L", im.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, im.width - 1, im.height - 1], r, fill=255)
    out = Image.new("RGB", im.size, PAPER)
    out.paste(im.convert("RGB"), (0, 0), mask)
    return out


def overview():
    rows = fv.sites("show")
    f = lambda n: ImageFont.truetype(fv.FONT, n)
    whole = view(rows, fv.MAPW / 2, fv.MAPH / 2, fv.MAPW, fv.MAPH, .4)
    field = next(r for r in rows if r["kind"] == "Field")
    st = next(r for r in rows if r["kind"] == "Street")
    coop = next(r for r in rows if r["id"] == "yard.coop")
    tree = [r for r in rows if r["kind"] == "Tree"]
    shots = [
        ("Поле: девять культур, от всходов до спелого", view(rows, field["x"], field["y"] + 40, 1150, 760, 1.0)),
        ("Сад: в листве, в цвету, с плодами", view(rows, sum(t["x"] for t in tree) / len(tree), sum(t["y"] for t in tree) / len(tree) + 120, 1150, 760, 1.0)),
        ("Курятник: голодные, сытые, с яйцом", view(rows, coop["x"] + 60, coop["y"] + 120, 760, 500, 1.5)),
        ("Улица: коровник, овчарня, свинарник", view(rows, st["x"] + 800, 250, 1700, 520, .68)),
        ("Улица: пекарня, молочная, сарай, джем-цех, ткацкая, сахарная, пасека", view(rows, st["x"] + 3050, 250, 2500, 520, .68 * 1700 / 2500 * 1.59)),
    ]
    pad, head = 30, 96
    W = whole.width + 2 * pad
    # the close views in rows under the map
    y = head + whole.height + pad
    layout = []
    rowimgs = [[shots[0], shots[1]], [shots[2], shots[3]], [shots[4]]]
    for row in rowimgs:
        total = sum(s[1].width for s in row) + pad * (len(row) - 1)
        k = min(1.0, (W - 2 * pad) / total)
        x = pad
        hmax = 0
        for (cap, im) in row:
            im2 = im.resize((int(im.width * k), int(im.height * k)), Image.LANCZOS) if k < 1 else im
            layout.append((cap, im2, x, y + 44))
            x += im2.width + pad
            hmax = max(hmax, im2.height)
        y += 44 + hmax + pad
    out = Image.new("RGB", (W, y), PAPER)
    d = ImageDraw.Draw(out)
    d.text((pad, head / 2), "Хозяйство на карте: вся ферма и её части", font=f(44), fill=INK, anchor="lm")
    out.paste(rounded(whole), (pad, head))
    for (cap, im, x, yy) in layout:
        d.text((x + 4, yy - 22), cap, font=f(26), fill=INK, anchor="lm")
        out.paste(rounded(im), (x, yy))
    out.save(os.path.join(HERE, "00_overview.png"), optimize=True)
    print("00_overview.png", out.size)


SIZE = {"Plot": (170, 122), "PlotOffer": (170, 122), "Seeded": (170, 224), "Sign": (140, 160), "Ring": (220, 140), "Bubble": (110, 126), "Spark": (64, 64),
        "BarBack": (128, 26), "BarFill": (128, 26), "Sickle": (128, 128), "Street": (512, 440), "Grass": (512, 440),
        "Tuft1": (60, 44), "Tuft2": (60, 44), "Bush1": (120, 96), "Bush2": (120, 96), "Bush3": (120, 96)}
CROPS = ("wheat", "corn", "carrot", "sunflower", "strawberry", "cotton", "beet", "potato", "pumpkin")
TREES = ("apple_tree", "pear_tree", "cherry_tree")


def dims(n):
    """A picture's canvas in map units (its size on the map at zoom 1)."""
    if n.startswith("Crop_"):
        return 170, 224
    if n.startswith("Tree_"):
        return 230, 292
    if n.startswith("Animal_"):
        return 150, 150
    if n.startswith("Pen_"):
        return 500, 330
    if n.startswith("Workshop_"):
        return 344, 320
    return SIZE.get(n, (128, 128))


GROUPS = [
    ("Грядка и культуры: всходы, молодое, почти спелое, спелое", [["Plot", "PlotOffer", "Seeded"]] + [["Crop_%s_%d" % (c, st) for st in (1, 2, 3, 4)] for c in CROPS]),
    ("Плодовые деревья: в листве, в цвету, с плодами", [["Tree_%s_%s" % (t, st) for st in ("leaf", "bloom", "ripe")] for t in TREES]),
    ("Животные: обычный вид и «продукт готов»", [["Animal_%s%s" % (a, r) for a in ("chicken", "cow", "sheep") for r in ("", "_ready")], ["Animal_%s%s" % (a, r) for a in ("pig", "bee") for r in ("", "_ready")]]),
    ("Загоны", [["Pen_cowshed", "Pen_sheepfold"], ["Pen_pigsty", "Pen_apiary"]]),
    ("Мастерские", [["Workshop_bakery", "Workshop_dairy", "Workshop_toolshed"], ["Workshop_jam", "Workshop_textile", "Workshop_sugar_mill"]]),
    ("Мелочи карты", [["Sign", "Ring", "Bubble", "Spark", "BarBack", "BarFill", "Sickle", "Tuft1", "Tuft2", "Bush1", "Bush2", "Bush3"], ["Street", "Grass"]]),
]


def sheet():
    """sprites.png: every farm picture on grass at its in-game size (one pixel per map unit — downscaled from the
    files, never up), grouped; and the icons."""
    f = lambda n: ImageFont.truetype(fv.FONT, n)
    names = sorted(os.path.basename(p)[:-4] for p in glob.glob(os.path.join(fv.ART, "Farm", "*.png")))
    used = {n for _, rows in GROUPS for row in rows for n in row}
    missing = [n for n in names if n not in used]
    assert not missing, missing
    groups = list(GROUPS)
    crops = groups[0][1]
    groups[0] = (groups[0][0], [crops[0]] + [crops[i] + (crops[i + 1] if i + 1 < len(crops) else []) for i in range(1, len(crops), 2)])
    pad, gap = 30, 14
    blocks = []
    width = 0
    for title, rows in groups:
        h = 50
        for row in rows:
            width = max(width, sum(dims(n)[0] + gap for n in row))
            h += max(dims(n)[1] for n in row) + gap
        blocks.append((title, rows, h))
    icons = sorted(os.path.basename(p)[:-4] for p in glob.glob(os.path.join(fv.RES, "Icons", "*.png")) if os.path.basename(p).split(".")[0] in ("item", "tool", "animal"))
    cols = width // 112
    icon_h = 50 + ((len(icons) + cols - 1) // cols) * 112
    W = width + 2 * pad
    H = 96 + sum(b[2] + pad for b in blocks) + icon_h + pad
    out = Image.new("RGBA", (W, H), PAPER + (255,))
    d = ImageDraw.Draw(out)
    d.text((pad, 48), "Картинки хозяйства в размере игры (1 пиксель на единицу карты) и значки", font=f(34), fill=INK, anchor="lm")
    y = 96
    for title, rows, h in blocks:
        d.rounded_rectangle([pad - 10, y, W - pad + 10, y + h], 20, fill=(124, 186, 78, 255))
        d.text((pad + 6, y + 26), title, font=f(26), fill="white", anchor="lm", stroke_width=2, stroke_fill=(50, 90, 30))
        yy = y + 50
        for row in rows:
            rh = max(dims(n)[1] for n in row)
            x = pad
            for n in row:
                w, hh = dims(n)
                out.alpha_composite(fv.art("Farm/" + n).resize((w, hh), Image.LANCZOS), (x, yy + rh - hh))
                x += w + gap
            yy += rh + gap
        y += h + pad
    d.rounded_rectangle([pad - 10, y, W - pad + 10, y + icon_h], 20, fill=(233, 205, 160, 255))
    d.text((pad + 6, y + 26), "Значки товаров, инструментов и животных", font=f(26), fill=INK, anchor="lm")
    for i, n in enumerate(icons):
        out.alpha_composite(fv.icon(n).resize((100, 100), Image.LANCZOS), (pad + (i % cols) * 112, y + 50 + (i // cols) * 112))
    out.convert("RGB").save(os.path.join(HERE, "sprites.png"), optimize=True)
    print("sprites.png", out.size)


def sheets_100():
    """sprites_100_*.jpg: the pictures at 100% of their own pixels, on the map's grass colour."""
    for old in glob.glob(os.path.join(HERE, "sprites_100_*.jpg")):
        os.remove(old)
    sets = [("crops_%d" % (k + 1), [["Crop_%s_%d" % (c, st) for st in (1, 2, 3, 4)] for c in CROPS[k * 3:k * 3 + 3]]) for k in range(3)]
    sets += [("trees", GROUPS[1][1]), ("animals", [["Animal_%s%s" % (a, r) for a in ("chicken", "cow", "sheep", "pig", "bee")] for r in ("", "_ready")]),
             ("pens", GROUPS[3][1]), ("workshops", GROUPS[4][1]),
             ("small", [["Plot", "PlotOffer", "Seeded", "Sign"], ["Bush1", "Bush2", "Bush3", "Tuft1", "Tuft2", "Sickle", "Bubble", "Ring"], ["Street", "Grass"]])]
    gap = 12
    for name, rows in sets:
        ims = [[fv.art("Farm/" + n) for n in row] for row in rows]
        W = max(sum(im.width + gap for im in row) for row in ims) + gap
        H = sum(max(im.height for im in row) + gap for row in ims) + gap
        out = Image.new("RGBA", (W, H), (121, 185, 78, 255))
        y = gap
        for row in ims:
            rh = max(im.height for im in row)
            x = gap
            for im in row:
                out.alpha_composite(im, (x, y + rh - im.height))
                x += im.width + gap
            y += rh + gap
        out.convert("RGB").save(os.path.join(HERE, "sprites_100_%s.jpg" % name), quality=90)
        print("sprites_100_%s.jpg" % name, out.size)


def one_to_one():
    """Parts of the show farm at one pixel per map unit: the painted backgrounds at their own resolution."""
    rows = fv.sites("show")
    field = next(r for r in rows if r["kind"] == "Field")
    st = next(r for r in rows if r["kind"] == "Street")
    coop = next(r for r in rows if r["id"] == "yard.coop")
    tree = [r for r in rows if r["kind"] == "Tree"]
    for old in glob.glob(os.path.join(HERE, "00_overview_1to1_*.png")):
        os.remove(old)
    for name, (cx, cy, w, h) in (("field", (field["x"], field["y"] + 40, 1300, 860)),
                                 ("orchard", (sum(t["x"] for t in tree) / len(tree), sum(t["y"] for t in tree) / len(tree) + 110, 1300, 860)),
                                 ("coop", (coop["x"] + 60, coop["y"] + 130, 900, 640)),
                                 ("street_pens", (st["x"] + 800, 250, 1700, 560)),
                                 ("street_workshops", (st["x"] + 3050, 250, 2400, 560))):
        im = view(rows, cx, cy, w, h, 1.0)
        im.convert("RGB").save(os.path.join(HERE, "00_overview_1to1_%s.png" % name), optimize=True)
        print("00_overview_1to1_%s.png" % name, im.size)


if __name__ == "__main__":
    overview()
    one_to_one()
    sheet()
    sheets_100()
