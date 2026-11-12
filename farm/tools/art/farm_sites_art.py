#!/usr/bin/env python3
"""Pictures of the working farm's sites on the map (docs/FARM_UX.md): the plot's soil, the plant's growth steps,
the pegged-out plot for sale, the selection ring, the bubble over a site, the sickle, the ground pad and the sign
of a pen, a plain workshop to tint, a sparkle. Drawn 4x and scaled down; written to Resources/Art/Farm.

    python3 farm/tools/art/farm_sites_art.py
"""
import math
import os
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "..", "unity", "SunnyValley", "Assets", "SunnyValley", "Resources", "Art", "Farm")
K = 4


def canvas(w, h):
    return Image.new("RGBA", (w * K, h * K), (0, 0, 0, 0))


def save(im, name, w, h):
    os.makedirs(OUT, exist_ok=True)
    im.resize((w, h), Image.LANCZOS).save(os.path.join(OUT, name + ".png"))


def rr(d, box, r, fill, outline=None, width=0):
    d.rounded_rectangle([v * K for v in box], r * K, fill=fill, outline=outline, width=width * K)


def shadow(im, box, r, alpha=70, blur=6, dy=5):
    s = Image.new("RGBA", im.size, (0, 0, 0, 0))
    ImageDraw.Draw(s).rounded_rectangle([box[0] * K, (box[1] + dy) * K, box[2] * K, (box[3] + dy) * K], r * K, fill=(40, 25, 10, alpha))
    im.alpha_composite(s.filter(ImageFilter.GaussianBlur(blur * K)))


def soil():
    w, h = 360, 232
    im = canvas(w, h)
    shadow(im, (14, 22, w - 14, h - 14), 34)
    d = ImageDraw.Draw(im)
    rr(d, (10, 26, w - 10, h - 10), 36, (92, 56, 30, 255))          # the bed's side
    rr(d, (10, 10, w - 10, h - 34), 36, (140, 92, 52, 255))         # its top
    rr(d, (18, 16, w - 18, h - 42), 30, (156, 104, 60, 255))
    for i in range(5):                                             # furrows
        y = 40 + i * 32
        rr(d, (34, y, w - 34, y + 14), 7, (118, 76, 42, 255))
        rr(d, (34, y + 9, w - 34, y + 14), 3, (172, 120, 72, 255))
    save(im, "Soil", w, h)


def offer():
    w, h = 360, 232
    im = canvas(w, h)
    d = ImageDraw.Draw(im)
    rr(d, (12, 12, w - 12, h - 12), 34, (255, 255, 255, 46))
    # a dashed rope around
    n = 26
    per = 2 * (w - 60) + 2 * (h - 60)
    for i in range(n):
        t = i / n * per
        if t < w - 60:
            x, y, dx, dy = 30 + t, 14, 1, 0
        elif t < w - 60 + h - 60:
            x, y, dx, dy = w - 14, 30 + t - (w - 60), 0, 1
        elif t < 2 * (w - 60) + h - 60:
            x, y, dx, dy = w - 30 - (t - (w - 60) - (h - 60)), h - 14, -1, 0
        else:
            x, y, dx, dy = 14, h - 30 - (t - 2 * (w - 60) - (h - 60)), 0, -1
        d.line([x * K, y * K, (x + dx * 14) * K, (y + dy * 14) * K], fill=(255, 255, 255, 235), width=6 * K)
    for x, y in ((22, 22), (w - 22, 22), (22, h - 22), (w - 22, h - 22)):  # pegs
        d.ellipse([(x - 13) * K, (y - 13) * K, (x + 13) * K, (y + 13) * K], fill=(150, 98, 52, 255), outline=(92, 56, 30, 255), width=3 * K)
    save(im, "SoilOffer", w, h)


def leaf(d, x, y, length, angle, width, col, edge):
    a = math.radians(angle)
    tx, ty = x + math.cos(a) * length, y - math.sin(a) * length
    nx, ny = -math.sin(a) * width, -math.cos(a) * width
    mx, my = (x + tx) / 2, (y + ty) / 2
    pts = [(x, y), (mx + nx, my + ny), (tx, ty), (mx - nx, my - ny)]
    d.polygon([(px * K, py * K) for px, py in pts], fill=col, outline=edge)
    d.line([x * K, y * K, tx * K, ty * K], fill=edge, width=max(1, int(1.5 * K)))


def plant(name, rows, size):
    """A row of plants seen from the front: `size` 0..1 is how grown."""
    w, h = 360, 260
    im = canvas(w, h)
    d = ImageDraw.Draw(im)
    green, dark, light = (86, 176, 60, 255), (44, 112, 40, 255), (140, 214, 84, 255)
    for r in range(rows):
        y = h - 26 - (rows - 1 - r) * 46
        n = 5
        for i in range(n):
            x = 50 + i * (w - 100) / (n - 1) + (12 if r % 2 else -8)
            L = 26 + 78 * size
            d.ellipse([(x - 20) * K, (y - 5) * K, (x + 20) * K, (y + 7) * K], fill=(70, 44, 24, 150))
            for ang, k, c in ((118, 0.85, green), (62, 0.85, green), (90, 1.0, light), (150, 0.6, dark), (30, 0.6, dark)):
                if size < 0.3 and ang in (150, 30):
                    continue
                leaf(d, x, y, L * k, ang, 6 + 11 * size, c, dark)
    save(im, name, w, h)


def ring():
    w, h = 256, 160
    im = canvas(w, h)
    glow = canvas(w, h)
    ImageDraw.Draw(glow).ellipse([18 * K, 18 * K, (w - 18) * K, (h - 18) * K], outline=(255, 244, 150, 230), width=16 * K)
    im.alpha_composite(glow.filter(ImageFilter.GaussianBlur(7 * K)))
    ImageDraw.Draw(im).ellipse([20 * K, 20 * K, (w - 20) * K, (h - 20) * K], outline=(255, 255, 255, 255), width=7 * K)
    save(im, "Ring", w, h)


def bubble():
    w, h = 200, 228
    im = canvas(w, h)
    s = canvas(w, h)
    ImageDraw.Draw(s).ellipse([14 * K, 18 * K, (w - 14) * K, (h - 34) * K], fill=(30, 40, 20, 90))
    im.alpha_composite(s.filter(ImageFilter.GaussianBlur(6 * K)))
    d = ImageDraw.Draw(im)
    edge, face = (214, 190, 140, 255), (255, 252, 240, 255)
    d.polygon([(72 * K, 170 * K), (128 * K, 170 * K), (100 * K, 222 * K)], fill=edge)
    d.ellipse([8 * K, 4 * K, (w - 8) * K, 188 * K], fill=edge)
    d.polygon([(80 * K, 168 * K), (120 * K, 168 * K), (100 * K, 210 * K)], fill=face)
    d.ellipse([15 * K, 11 * K, (w - 15) * K, 181 * K], fill=face)
    d.ellipse([40 * K, 24 * K, 104 * K, 60 * K], fill=(255, 255, 255, 255))
    save(im, "Bubble", w, h)


def sickle():
    w = h = 256
    im = canvas(w, h)
    s = canvas(w, h)
    ds = ImageDraw.Draw(s)
    ds.arc([44 * K, 30 * K, 222 * K, 208 * K], 150, 20, fill=(20, 30, 20, 110), width=30 * K)
    im.alpha_composite(s.filter(ImageFilter.GaussianBlur(5 * K)))
    d = ImageDraw.Draw(im)
    # the handle
    d.line([70 * K, 236 * K, 112 * K, 150 * K], fill=(92, 56, 30, 255), width=40 * K)
    d.line([70 * K, 236 * K, 112 * K, 150 * K], fill=(176, 116, 62, 255), width=28 * K)
    d.ellipse([52 * K, 214 * K, 92 * K, 252 * K], fill=(176, 116, 62, 255), outline=(92, 56, 30, 255), width=6 * K)
    # the blade: a thick arc with a light edge
    d.arc([40 * K, 22 * K, 218 * K, 200 * K], 150, 20, fill=(84, 98, 112, 255), width=36 * K)
    d.arc([46 * K, 28 * K, 212 * K, 194 * K], 152, 18, fill=(214, 226, 236, 255), width=24 * K)
    d.arc([60 * K, 42 * K, 198 * K, 180 * K], 158, 12, fill=(160, 176, 190, 255), width=8 * K)
    save(im, "Sickle", w, h)


def pad():
    w, h = 512, 300
    im = canvas(w, h)
    d = ImageDraw.Draw(im)
    d.ellipse([6 * K, 10 * K, (w - 6) * K, (h - 6) * K], fill=(196, 160, 104, 255))
    d.ellipse([20 * K, 20 * K, (w - 20) * K, (h - 24) * K], fill=(216, 184, 126, 255))
    im = im.filter(ImageFilter.GaussianBlur(2 * K))
    d = ImageDraw.Draw(im)
    # a low fence along the far side
    n = 15
    for i in range(n):
        t = i / (n - 1)
        a = math.pi * (1 - t)
        x, y = w / 2 + math.cos(a) * (w / 2 - 26), h * 0.46 - math.sin(a) * (h * 0.36)
        rr(d, (x - 7, y - 34, x + 7, y + 6), 5, (236, 222, 190, 255), (150, 118, 76, 255), 2)
        if i:
            d.line([px * K, (py - 16) * K, x * K, (y - 16) * K], fill=(236, 222, 190, 255), width=7 * K)
        px, py = x, y
    save(im, "PenPad", w, h)


def sign():
    w, h = 256, 276
    im = canvas(w, h)
    shadow(im, (30, 30, w - 30, 180), 22)
    d = ImageDraw.Draw(im)
    rr(d, (116, 150, 140, 268), 8, (120, 78, 40, 255), (80, 50, 26, 255), 3)
    rr(d, (22, 16, w - 22, 176), 24, (92, 56, 30, 255))
    rr(d, (30, 22, w - 30, 164), 20, (214, 160, 96, 255))
    rr(d, (40, 30, w - 40, 154), 16, (236, 190, 124, 255))
    save(im, "PenSign", w, h)


def workshop():
    """A plain small building in greys and whites: Image.color tints it (each workshop its own colour)."""
    w, h = 384, 384
    im = canvas(w, h)
    s = canvas(w, h)
    ImageDraw.Draw(s).ellipse([20 * K, 300 * K, (w - 20) * K, 380 * K], fill=(30, 40, 20, 110))
    im.alpha_composite(s.filter(ImageFilter.GaussianBlur(8 * K)))
    d = ImageDraw.Draw(im)
    rr(d, (56, 170, w - 56, 350), 14, (150, 150, 150, 255))                      # outline
    rr(d, (64, 176, w - 64, 344), 10, (244, 244, 244, 255))                      # wall
    rr(d, (64, 300, w - 64, 344), 10, (214, 214, 214, 255))                      # plinth
    d.polygon([(30 * K, 190 * K), (w / 2 * K, 44 * K), ((w - 30) * K, 190 * K)], fill=(120, 120, 120, 255))
    d.polygon([(48 * K, 182 * K), (w / 2 * K, 62 * K), ((w - 48) * K, 182 * K)], fill=(176, 176, 176, 255))
    d.polygon([(w / 2 * K, 62 * K), ((w - 48) * K, 182 * K), ((w / 2 + 40) * K, 182 * K)], fill=(150, 150, 150, 255))
    rr(d, (236, 60, 272, 130), 6, (130, 130, 130, 255))                           # chimney
    rr(d, (160, 232, 224, 344), 10, (110, 110, 110, 255))                         # door
    rr(d, (168, 240, 216, 344), 6, (160, 160, 160, 255))
    for x in (88, 248):                                                           # windows
        rr(d, (x, 220, x + 50, 276), 8, (120, 120, 120, 255))
        rr(d, (x + 6, 226, x + 44, 270), 5, (255, 255, 255, 255))
    save(im, "Workshop", w, h)


def spark():
    w = h = 96
    im = canvas(w, h)
    d = ImageDraw.Draw(im)
    c = w / 2
    pts = []
    for i in range(8):
        a = math.pi / 4 * i
        r = 44 if i % 2 == 0 else 10
        pts.append(((c + math.cos(a) * r) * K, (c + math.sin(a) * r) * K))
    d.polygon(pts, fill=(255, 255, 255, 255))
    glow = im.filter(ImageFilter.GaussianBlur(4 * K))
    glow.alpha_composite(im)
    save(glow, "Spark", w, h)


def bar():
    w, h = 128, 28
    im = canvas(w, h)
    d = ImageDraw.Draw(im)
    rr(d, (0, 0, w, h), 14, (60, 40, 24, 230))
    rr(d, (4, 4, w - 4, h - 4), 10, (255, 255, 255, 60))
    save(im, "BarBack", w, h)
    im = canvas(w, h)
    d = ImageDraw.Draw(im)
    rr(d, (4, 4, w - 4, h - 4), 10, (255, 255, 255, 255))
    save(im, "BarFill", w, h)


if __name__ == "__main__":
    soil(); offer(); plant("Plant1", 1, 0.12); plant("Plant2", 2, 0.5); plant("Plant3", 3, 1.0)
    ring(); bubble(); sickle(); pad(); sign(); workshop(); spark(); bar()
    print("written to", os.path.normpath(OUT))
