#!/usr/bin/env python3
"""00_overview.png: the whole working farm (the «show» farm: every crop at every age, trees in leaf, bloom and fruit,
animals hungry, fed and with products, workshops working and done), and close views of its parts.
sprites.png: every picture of Resources/Art/Farm and the farm's icons.

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
        ("Улица: коровник, овчарня, свинарник", view(rows, st["x"] + 800, 215, 1700, 450, .68)),
        ("Улица: пекарня, молочная, сарай, джем-цех, ткацкая, сахарная, пасека", view(rows, st["x"] + 3100, 215, 2700, 450, .68 * 1700 / 2700 * 1.59)),
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


def sheet():
    """Every farm picture on grass, grouped, at the size it has on the map at zoom 1; and the icons."""
    f = lambda n: ImageFont.truetype(fv.FONT, n)
    names = sorted(os.path.basename(p)[:-4] for p in glob.glob(os.path.join(fv.ART, "Farm", "*.png")))
    size = {"Plot": (170, 122), "PlotOffer": (170, 122), "Seeded": (170, 224), "Sign": (105, 120), "Ring": (220, 140), "Bubble": (96, 110), "Spark": (56, 56),
            "BarBack": (128, 26), "BarFill": (128, 26), "Sickle": (128, 128), "Road": (256, 96), "Tuft1": (60, 44), "Tuft2": (60, 44), "Bush1": (120, 96), "Bush2": (120, 96), "Bush3": (120, 96)}

    def dims(n):
        if n.startswith("Crop_"):
            return 170, 224
        if n.startswith("Tree_"):
            return 210, 276
        if n.startswith("Animal_"):
            return 150, 150
        if n.startswith("Pen_"):
            return 500, 330
        if n.startswith("Workshop_"):
            return 300, 300
        return size.get(n, (128, 128))

    groups = [
        ("Грядка и культуры: всходы, молодое, почти спелое, спелое", [["Plot", "PlotOffer", "Seeded"]] + [["Crop_%s_%d" % (c, st) for st in (1, 2, 3, 4)] for c in
                                                                              ("wheat", "corn", "carrot", "sunflower", "strawberry", "cotton", "beet", "potato", "pumpkin")]),
        ("Плодовые деревья: в листве, в цвету, с плодами", [["Tree_%s_%s" % (t, s) for s in ("leaf", "bloom", "ripe")] for t in ("apple_tree", "pear_tree", "cherry_tree")]),
        ("Животные: обычный вид и «продукт готов»", [["Animal_%s%s" % (a, r) for a in ("chicken", "cow", "sheep") for r in ("", "_ready")], ["Animal_%s%s" % (a, r) for a in ("pig", "bee") for r in ("", "_ready")]]),
        ("Загоны", [["Pen_cowshed", "Pen_sheepfold"], ["Pen_pigsty", "Pen_apiary"]]),
        ("Мастерские", [["Workshop_bakery", "Workshop_dairy", "Workshop_toolshed"], ["Workshop_jam", "Workshop_textile", "Workshop_sugar_mill"]]),
        ("Мелочи карты", [["Sign", "Ring", "Bubble", "Spark", "BarBack", "BarFill", "Sickle", "Tuft1", "Tuft2", "Bush1", "Bush2", "Bush3", "Road"]]),
    ]
    used = {n for _, rows in groups for row in rows for n in row}
    missing = [n for n in names if n not in used]
    assert not missing, missing
    # crops: two crops per line to keep the sheet wide rather than endless
    crops = groups[0][1]
    groups[0] = (groups[0][0], [crops[0]] + [crops[i] + (crops[i + 1] if i + 1 < len(crops) else []) for i in range(1, len(crops), 2)])
    pad, gap = 30, 14
    blocks = []
    width = 0
    for title, rows in groups:
        h = 50
        for row in rows:
            rw = sum(dims(n)[0] + gap for n in row)
            width = max(width, rw)
            h += max(dims(n)[1] for n in row) + gap
        blocks.append((title, rows, h))
    icons = sorted(os.path.basename(p)[:-4] for p in glob.glob(os.path.join(fv.RES, "Icons", "*.png")) if os.path.basename(p).split(".")[0] in ("item", "tool", "animal"))
    cols = (width) // 112
    icon_h = 50 + ((len(icons) + cols - 1) // cols) * 112
    W = width + 2 * pad
    H = 96 + sum(b[2] + pad for b in blocks) + icon_h + pad
    out = Image.new("RGBA", (W, H), PAPER + (255,))
    d = ImageDraw.Draw(out)
    d.text((pad, 48), "Картинки хозяйства: Resources/Art/Farm и значки item.*, tool.*, animal.*", font=f(36), fill=INK, anchor="lm")
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
                im = fv.art("Farm/" + n).resize((w, hh), Image.LANCZOS)
                out.alpha_composite(im, (x, yy + rh - hh))
                x += w + gap
            yy += rh + gap
        y += h + pad
    d.rounded_rectangle([pad - 10, y, W - pad + 10, y + icon_h], 20, fill=(233, 205, 160, 255))
    d.text((pad + 6, y + 26), "Значки товаров, инструментов и животных (вместо заглушек из кода)", font=f(26), fill=INK, anchor="lm")
    for i, n in enumerate(icons):
        im = fv.icon(n).resize((100, 100), Image.LANCZOS)
        out.alpha_composite(im, (pad + (i % cols) * 112, y + 50 + (i // cols) * 112))
    out.convert("RGB").save(os.path.join(HERE, "sprites.png"), optimize=True)
    print("sprites.png", out.size)


if __name__ == "__main__":
    overview()
    sheet()
