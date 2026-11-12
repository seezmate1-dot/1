#!/usr/bin/env python3
"""Storyboards of the farm map's interactions (docs/FARM_UX.md), drawn from the game's real pictures: the chapter's
map (Resources/Art/Ch1 by chapter1_layout.json), the sites' art (Resources/Art/Farm), the HUD icons and the font.
The numbers (where plots stand, the fan of round buttons, the HUD's sizes) repeat the code's: FarmSiteMap,
RadialLayout, Screens.Farm, Tokens. A picture of the design, not a screenshot of the build.

    python3 farm/docs/farm-ux/storyboards.py
"""
import json
import math
import os
from PIL import Image, ImageDraw, ImageFont, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
RES = os.path.join(HERE, "..", "..", "unity", "SunnyValley", "Assets", "SunnyValley", "Resources")
ART = os.path.join(RES, "Art")
S = 0.5                      # output pixels per canvas unit
W, H = 1080, 1920            # the canvas
FONT = os.path.join(RES, "Fonts", "Nunito-ExtraBold.ttf")


def font(size):
    return ImageFont.truetype(FONT, int(size * S))


_cache = {}


def art(name):
    if name not in _cache:
        im = None
        for ext in (".png", ".jpg"):
            p = os.path.join(ART, name + ext)
            if os.path.exists(p):
                im = Image.open(p).convert("RGBA")
        _cache[name] = im
    return _cache[name]


def icon(name):
    paths = {"coin": "Currency/Coin", "star": "Currency/Star", "crystal": "Currency/Crystal", "heart": "Currency/Heart",
             "corn": "Tiles/Corn", "carrot": "Tiles/Carrot", "apple": "Tiles/Apple", "tomato": "Tiles/Tomato",
             "sickle": "Farm/Sickle", "basket": "Obstacles/Basket", "chick": "Obstacles/Chick"}
    if name in paths:
        return art(paths[name])
    p = os.path.join(RES, "Icons", name + ".png")
    key = "icon:" + name
    if key not in _cache:
        _cache[key] = Image.open(p).convert("RGBA") if os.path.exists(p) else None
    return _cache[key]


def fit(im, w, h):
    k = min(w / im.width, h / im.height)
    return im.resize((max(1, int(im.width * k)), max(1, int(im.height * k))), Image.LANCZOS)


# ---------------------------------------------------------------------------------------------- the map

LAYOUT = json.load(open(os.path.join(RES, "Meta", "chapter1_layout.json"), encoding="utf-8"))
MAPW, MAPH = LAYOUT["map"]["w"], LAYOUT["map"]["h"]
MS = 0.5                     # the map is kept at half size
_map = None


def base_map():
    """The restored farm of chapter 1, as FarmWorld builds it (without the painted hens: the working farm has live ones)."""
    global _map
    if _map is not None:
        return _map
    im = Image.new("RGBA", (int(MAPW * MS), int(MAPH * MS)), (121, 185, 78, 255))
    for z in sorted(LAYOUT["zones"], key=lambda z: -z["y"]):
        bg = art(z["background"])
        if bg:
            im.paste(bg.resize((int(z["w"] * MS), int(z["h"] * MS)), Image.LANCZOS), (int(z["x"] * MS), int((MAPH - z["y"] - z["h"]) * MS)))
        for s in sorted(z["sprites"], key=lambda s: s.get("z", 0)):
            if s.get("show", "after") == "before" or s.get("until") or s["art"] == "Ch1/Objects/Hens":
                continue
            a = art(s["art"])
            if a is None:
                continue
            a = fit(a, s["w"] * MS, s["h"] * MS)
            im.alpha_composite(a, (int((z["x"] + s["x"]) * MS - a.width / 2), int((MAPH - z["y"] - s["y"]) * MS - a.height)))
    _map = im
    return im


def zone(zid):
    z = next(z for z in LAYOUT["zones"] if z["id"] == zid)
    return z["x"], z["y"]


def plot_point(i):
    zx, zy = zone("meadow")
    return zx + 416 + (i % 5) * 192, zy + 770 - (i // 5) * 128


PLOT_W, PLOT_H = 180, 116


class Scene:
    """One phone screen: the map under a camera, things on it in map units, the HUD and overlays in canvas units."""

    def __init__(self, cx, cy, zoom):
        self.cx, self.cy, self.zoom = cx, cy, zoom
        m = base_map()
        # the part of the map the view shows
        vw, vh = W / zoom, H / zoom
        box = [int((cx - vw / 2) * MS), int((MAPH - cy - vh / 2) * MS), int((cx + vw / 2) * MS), int((MAPH - cy + vh / 2) * MS)]
        self.im = Image.new("RGBA", (int(W * S), int(H * S)), (121, 185, 78, 255))
        crop = m.crop(box).resize(self.im.size, Image.LANCZOS)
        self.im.alpha_composite(crop)
        self.d = ImageDraw.Draw(self.im)

    # map → output pixels
    def px(self, x, y):
        return ((x - self.cx) * self.zoom + W / 2) * S, (H / 2 - (y - self.cy) * self.zoom) * S

    def put(self, im, x, y, w, h, foot=True, alpha=1.0, scale=(1, 1), rot=0):
        """A picture on the map: (x, y) its bottom centre (or its middle), w × h in map units."""
        if im is None:
            return
        k = self.zoom * S
        a = fit(im, w * k * scale[0], h * k * scale[1]) if scale == (1, 1) else im.resize((max(1, int(w * k * scale[0])), max(1, int(h * k * scale[1]))), Image.LANCZOS)
        if rot:
            a = a.rotate(rot, resample=Image.BICUBIC, expand=True)
        if alpha < 1:
            a.putalpha(a.getchannel("A").point(lambda v: int(v * alpha)))
        X, Y = self.px(x, y)
        self.im.alpha_composite(a, (int(X - a.width / 2), int(Y - (a.height if foot else a.height / 2))))

    def stretch(self, im, x, y, w, h, alpha=1.0, scale=1.0):
        k = self.zoom * S
        a = im.resize((max(1, int(w * k * scale)), max(1, int(h * k * (2 - scale) if scale != 1 else h * k))), Image.LANCZOS)
        if alpha < 1:
            a.putalpha(a.getchannel("A").point(lambda v: int(v * alpha)))
        X, Y = self.px(x, y)
        self.im.alpha_composite(a, (int(X - a.width / 2), int(Y - a.height)))

    def plot(self, i, stage=0, crop=None, bar=None, pressed=False, squash=1.0):
        x, y = plot_point(i)
        self.stretch(art("Farm/Soil"), x, y, PLOT_W * (0.93 if pressed else 1), PLOT_H * (0.93 if pressed else 1), scale=squash)
        if stage:
            self.stretch(art("Farm/Plant%d" % min(3, stage)), x, y + PLOT_H * 0.22, PLOT_W * 0.92, PLOT_H * 1.05)
        if crop and stage >= 4:
            self.put(icon(crop), x, y + PLOT_H * 0.95 + 8, 88, 88)
        if bar is not None:
            self.bar(x, y + 4, 144, bar)

    def offer(self, i, price):
        x, y = plot_point(i)
        self.stretch(art("Farm/SoilOffer"), x, y, PLOT_W, PLOT_H)
        self.put(icon("plus"), x, y + PLOT_H / 2 + 6, 60, 60, foot=False)
        self.tag(x, y + PLOT_H * 0.2, price, "coin")

    def bar(self, x, y, w, value):
        X, Y = self.px(x, y)
        k = self.zoom * S
        w2, h2 = w * k, 20 * k
        self.d.rounded_rectangle([X - w2 / 2, Y - h2 / 2, X + w2 / 2, Y + h2 / 2], h2 / 2, fill=(60, 40, 24, 230))
        self.d.rounded_rectangle([X - w2 / 2 + 2, Y - h2 / 2 + 2, X - w2 / 2 + 2 + (w2 - 4) * value, Y + h2 / 2 - 2], h2 / 2, fill=(108, 192, 74, 255))

    def tag(self, x, y, text, ic=None):
        X, Y = self.px(x, y)
        k = self.zoom * S
        w2, h2 = 170 * k, 56 * k
        self.d.rounded_rectangle([X - w2 / 2, Y - h2 / 2, X + w2 / 2, Y + h2 / 2], h2 / 2, fill=(62, 42, 23, 200))
        f = ImageFont.truetype(FONT, max(8, int(34 * k)))
        if ic:
            a = fit(icon(ic), 44 * k, 44 * k)
            self.im.alpha_composite(a, (int(X - w2 / 2 + 8 * k), int(Y - a.height / 2)))
        self.d.text((X + (18 * k if ic else 0), Y), text, font=f, fill="white", anchor="mm")

    def ring(self, x, y, w):
        self.stretch(art("Farm/Ring"), x, y - 14, w * 1.3, max(110, w * 0.8))

    def bubble(self, x, y, ic, dy=0, dim=False):
        b = art("Farm/Bubble")
        self.put(b, x, y + dy, 104, 118, alpha=0.8 if dim else 1)
        self.put(icon(ic), x, y + dy + 68, 66, 66, foot=False, alpha=0.5 if dim else 1)

    # ------------------------------------------------------------------ overlays in canvas units (origin top left)

    def text(self, x, y, s, size=40, fill="white", anchor="mm", stroke=3):
        self.d.text((x * S, y * S), s, font=font(size), fill=fill, anchor=anchor, stroke_width=int(stroke * S * 2), stroke_fill=(56, 30, 10, 255))

    def paste(self, im, x, y, w, h, alpha=1.0, rot=0):
        if im is None:
            return
        a = fit(im, w * S, h * S)
        if rot:
            a = a.rotate(rot, resample=Image.BICUBIC, expand=True)
        if alpha < 1:
            a.putalpha(a.getchannel("A").point(lambda v: int(v * alpha)))
        self.im.alpha_composite(a, (int(x * S - a.width / 2), int(y * S - a.height / 2)))

    def view(self, x, y):
        """A map point in canvas units."""
        X, Y = self.px(x, y)
        return X / S, Y / S

    def hud(self, coins="248", barn_bump=False, barn_badge=None, coins_bump=False):
        d = self.d
        M, G = 24, 16
        pw = (W - 2 * M - 3 * G) / 4
        vals = [("coin", coins), ("star", "3"), ("crystal", "120"), ("heart", "Полные")]
        for i, (ic, v) in enumerate(vals):
            x = M + i * (pw + G)
            d.rounded_rectangle([x * S, (M + 14) * S, (x + pw) * S, (M + 14 + 104) * S], 52 * S, fill=(62, 42, 23, 200))
            k = 1.25 if (coins_bump and i == 0) else 1
            self.paste(icon(ic), x + 52, M + 66, 92 * k, 92 * k)
            self.text(x + pw / 2 + 36, M + 66, v, 42)
        top = M + 132 + G
        left = [("fields", "Поля", (108, 192, 74)), ("animals", "Животные", (255, 154, 60)), ("workshop", "Цеха", (71, 168, 238)), ("events", "События", (160, 112, 224))]
        right = [("shop", "Магазин", (255, 154, 60)), ("orders", "Заказы", (183, 131, 78)), ("barn", "Амбар", (238, 90, 60)), ("town", "Городок", (79, 216, 232))]
        for i, (ic, cap, col) in enumerate(left):
            self.side(M + 86, top + i * 216 + 68, ic, cap, col)
        self.round(W - M - 86, top + 70, "settings", (71, 168, 238), 140)
        for i, (ic, cap, col) in enumerate(right):
            k = 1.18 if (barn_bump and ic == "barn") else 1
            self.side(W - M - 86, top + 156 + i * 216 + 68, ic, cap, col, k, barn_badge if ic == "barn" else None)
        # the level strip and «Играть»
        py = H - M - G - 176
        d.rounded_rectangle([(W / 2 - 320) * S, (py - 32 - 140) * S, (W / 2 + 320) * S, (py - 32) * S], 28 * S, fill=(62, 42, 23, 200))
        self.paste(icon("xp"), W / 2 - 250, py - 102, 108, 108)
        self.text(W / 2 + 40, py - 138, "Ферма · уровень 7", 34)
        d.rounded_rectangle([(W / 2 - 180) * S, (py - 104) * S, (W / 2 + 296) * S, (py - 48) * S], 28 * S, fill=(90, 58, 30, 220))
        d.rounded_rectangle([(W / 2 - 176) * S, (py - 100) * S, (W / 2 + 60) * S, (py - 52) * S], 24 * S, fill=(108, 192, 74, 255))
        d.rounded_rectangle([(W / 2 - 346) * S, (py + 12) * S, (W / 2 + 346) * S, (py + 176) * S], 44 * S, fill=(217, 142, 0, 255))
        d.rounded_rectangle([(W / 2 - 346) * S, py * S, (W / 2 + 346) * S, (py + 160) * S], 44 * S, fill=(255, 201, 40, 255))
        self.text(W / 2, py + 80, "Играть 48", 76, stroke=4)

    def round(self, x, y, ic, col, size, k=1.0):
        r = size / 2 * k
        self.d.ellipse([(x - r) * S, (y - r + 8) * S, (x + r) * S, (y + r + 8) * S], fill=tuple(int(c * 0.6) for c in col) + (255,))
        self.d.ellipse([(x - r) * S, (y - r) * S, (x + r) * S, (y + r) * S], fill=col + (255,))
        self.paste(icon(ic), x, y, size * 0.72 * k, size * 0.72 * k)

    def side(self, x, y, ic, cap, col, k=1.0, badge=None):
        self.round(x, y, ic, col, 136, k)
        self.text(x, y + 100, cap, 34)
        if badge:
            self.d.ellipse([(x + 34) * S, (y - 74) * S, (x + 90) * S, (y - 18) * S], fill=(238, 60, 50, 255))
            self.text(x + 62, y - 46, badge, 32, stroke=0)

    def item(self, x, y, ic, label=None, label_icon=None, enabled=True, locked=False, pressed=False, glow=False):
        """A round menu button, RadialLayout.Item across."""
        r = 72 * (0.9 if pressed else 1)
        d = self.d
        if glow:
            d.ellipse([(x - r - 14) * S, (y - r - 14) * S, (x + r + 14) * S, (y + r + 14) * S], fill=(255, 244, 150, 150))
        d.ellipse([(x - r) * S, (y - r + 8) * S, (x + r) * S, (y + r + 8) * S], fill=(40, 26, 8, 90))
        d.ellipse([(x - r) * S, (y - r) * S, (x + r) * S, (y + r) * S], fill=(126, 84, 44, 255))
        d.ellipse([(x - r + 7) * S, (y - r + 7) * S, (x + r - 7) * S, (y + r - 7) * S], fill=(255, 246, 222, 255) if enabled else (196, 190, 178, 255))
        self.paste(icon(ic), x, y - (12 if label else 0), 92, 92, alpha=1 if enabled else 0.5)
        if label:
            if label_icon:
                self.paste(icon(label_icon), x - 40, y + 46, 38, 38)
            self.text(x + (16 if label_icon else 0), y + 46, label, 32)
        if locked:
            self.paste(icon("lock"), x + 48, y - 48, 54, 54)

    def plate(self, x, y, title, sub=None, bar=None, w=520):
        h = 76 + (44 if sub else 0) + (30 if bar is not None else 0)
        self.d.rounded_rectangle([(x - w / 2) * S, (y - h / 2) * S, (x + w / 2) * S, (y + h / 2) * S], 30 * S, fill=(62, 42, 23, 210))
        ty = y - h / 2 + 38
        self.text(x, ty, title, 44)
        if sub:
            self.text(x, ty + 44, sub, 32, fill=(255, 238, 180))
        if bar is not None:
            by = y + h / 2 - 26
            self.d.rounded_rectangle([(x - 200) * S, (by - 11) * S, (x + 200) * S, (by + 11) * S], 11 * S, fill=(40, 26, 12, 255))
            self.d.rounded_rectangle([(x - 197) * S, (by - 8) * S, (x - 197 + 394 * bar) * S, (by + 8) * S], 8 * S, fill=(108, 192, 74, 255))

    def toast(self, s):
        tw = self.d.textlength(s, font=font(38)) / S + 80
        y = 24 + 132 + 16 + 60
        self.d.rounded_rectangle([(W / 2 - tw / 2) * S, (y - 44) * S, (W / 2 + tw / 2) * S, (y + 44) * S], 44 * S, fill=(62, 42, 23, 235))
        self.text(W / 2, y, s, 38, stroke=0)

    def finger(self, x, y, down=True):
        d = self.d
        r = 46
        d.ellipse([(x - r - 16) * S, (y - r - 16) * S, (x + r + 16) * S, (y + r + 16) * S], outline=(255, 255, 255, 150 if down else 0), width=int(6 * S))
        d.ellipse([(x - r) * S, (y - r) * S, (x + r) * S, (y + r) * S], fill=(255, 255, 255, 110), outline=(255, 255, 255, 235), width=int(6 * S))

    def trail(self, pts, col=(255, 255, 255, 200), dash=22, width=10):
        """A dashed line through canvas points: the finger's way."""
        for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
            n = max(1, int(math.hypot(x1 - x0, y1 - y0) / dash))
            for i in range(0, n, 2):
                a, b = i / n, min(1, (i + 1) / n)
                self.d.line([(x0 + (x1 - x0) * a) * S, (y0 + (y1 - y0) * a) * S, (x0 + (x1 - x0) * b) * S, (y0 + (y1 - y0) * b) * S], fill=col, width=int(width * S))

    def arc(self, a, b, lift=160, n=24, side=0):
        """Points of the arc a flying thing takes from a to b (FarmFx.Fly), canvas units."""
        mid = ((a[0] + b[0]) / 2 + side, (a[1] + b[1]) / 2 - abs(b[1] - a[1]) * 0.2 - lift)
        return [((1 - t) ** 2 * a[0] + 2 * (1 - t) * t * mid[0] + t * t * b[0], (1 - t) ** 2 * a[1] + 2 * (1 - t) * t * mid[1] + t * t * b[1]) for t in [i / n for i in range(n + 1)]]

    def floater(self, x, y, s, ic=None, fill="white"):
        if ic:
            self.paste(icon(ic), x - 44, y, 64, 64)
        self.text(x + (10 if ic else 0), y, s, 54, fill=fill, stroke=4)


def fan(n, ax, ay, radius, below=False):
    """RadialLayout.Place without the clamping: n buttons on an arc over (ax, ay), canvas units, y down."""
    item, gap = 144, 18
    pitch = item + gap
    r = max(radius + item / 2 + gap, 190)
    step = 2 * math.asin(min(1, pitch / (2 * r)))
    pts = []
    for j in range(n):
        a = math.pi / 2 + (j - (n - 1) / 2) * step
        pts.append((ax + math.cos(a) * r, ay - math.sin(a) * r * (-1 if below else 1)))
    # moved as a whole into the screen, as RadialLayout does
    lo, hi = min(p[0] for p in pts) - item / 2, max(p[0] for p in pts) + item / 2
    dx = 20 - lo if lo < 20 else (W - 20 - hi if hi > W - 20 else 0)
    return [(x + dx, y) for x, y in pts[::-1]]


def board(name, title, panels, captions):
    """Panels side by side with a title and a caption under each."""
    pw, ph = panels[0].im.size
    pad, head, foot = 28, 84, 150
    out = Image.new("RGB", (pad + len(panels) * (pw + pad), head + ph + foot), (250, 244, 230))
    d = ImageDraw.Draw(out)
    d.text((pad, head / 2), title, font=ImageFont.truetype(FONT, 38), fill=(90, 53, 18), anchor="lm")
    for i, (p, c) in enumerate(zip(panels, captions)):
        x = pad + i * (pw + pad)
        mask = Image.new("L", p.im.size, 0)
        ImageDraw.Draw(mask).rounded_rectangle([0, 0, pw, ph], 26, fill=255)
        out.paste(p.im.convert("RGB"), (x, head), mask)
        d.rounded_rectangle([x, head, x + pw, head + ph], 26, outline=(90, 53, 18), width=3)
        d.ellipse([x + 4, head + ph + 18, x + 48, head + ph + 62], fill=(255, 201, 40))
        d.text((x + 26, head + ph + 40), str(i + 1), font=ImageFont.truetype(FONT, 26), fill=(90, 53, 18), anchor="mm")
        yy = head + ph + 22
        for line in wrap(d, c, ImageFont.truetype(FONT, 22), pw - 60):
            d.text((x + 58, yy), line, font=ImageFont.truetype(FONT, 22), fill=(90, 53, 18))
            yy += 29
    out.save(os.path.join(HERE, name + ".png"), optimize=True)
    print(name + ".png", out.size)


def wrap(d, text, f, width):
    lines, cur = [], ""
    for word in text.split():
        t = (cur + " " + word).strip()
        if d.textlength(t, font=f) <= width:
            cur = t
        else:
            lines.append(cur)
            cur = word
    if cur:
        lines.append(cur)
    return lines


# ---------------------------------------------------------------------------------------------- the boards

FIELD = (plot_point(2)[0], plot_point(7)[1] - 150)       # the camera over the plots, as «Поля» leaves it


def field(zoom=0.72, **kw):
    return Scene(FIELD[0], FIELD[1], zoom)


def plots(sc, states, offer=10):
    for i in sorted(states, key=lambda i: -plot_point(i)[1]):
        sc.plot(i, **states[i])
    if offer is not None:
        sc.offer(offer, "220")


def sb_menu():
    empty = {i: {} for i in range(10)}
    empty.update({5: dict(stage=2, bar=0.55), 6: dict(stage=2, bar=0.5), 7: dict(stage=1, bar=0.2)})

    a = field(); st = dict(empty); st[2] = dict(pressed=True); plots(a, st); a.hud()
    x, y = a.view(plot_point(2)[0], plot_point(2)[1] + 58)
    a.finger(x, y)

    b = field(); x0, y0 = plot_point(2); b.ring(x0, y0, PLOT_W); plots(b, empty); b.hud()
    ax, ay = b.view(x0, y0 + 58)
    pts = fan(4, ax, ay, 86)
    b.item(*pts[0], "corn", "1", "coin"); b.item(*pts[1], "carrot", "1", "coin"); b.item(*pts[2], "tomato", "2", "coin")
    b.item(*pts[3], "apple", "Ур. 8", enabled=False, locked=True)
    b.plate(ax, min(p[1] for p in pts) - 72 - 75, "Что посадить?")
    b.text(W / 2, b.view(*plot_point(7))[1] + 150, "Проведите семенами по грядкам", 32)

    c = field(); c.ring(x0, y0, PLOT_W); plots(c, empty); c.hud()
    c.item(*pts[0], "corn", "1", "coin"); c.item(*pts[1], "carrot", "1", "coin"); c.item(*pts[2], "tomato", "2", "coin")
    c.item(pts[3][0] + 8, pts[3][1], "apple", "Ур. 8", enabled=False, locked=True, pressed=True)
    c.plate(ax, min(p[1] for p in pts) - 72 - 75, "Что посадить?")
    c.finger(pts[3][0] + 10, pts[3][1] + 20)
    c.toast("Клубника: откроется на 8 уровне фермы")

    d = field(); x6, y6 = plot_point(6); d.ring(x6, y6, PLOT_W); plots(d, empty); d.hud()
    ax, ay = d.view(x6, y6 + 58)
    p = fan(1, ax, ay, 86)[0]
    d.item(*p, "crystal", "2")
    d.plate(ax, p[1] - 72 - 100, "Морковь", "04:32", 0.5)
    board("01_tap_menu", "1. Касание по грядке: меню рядом с ней, а не окно со списком", [a, b, c, d], [
        "Палец на грядке: она проседает (93%) и пружинит обратно. Касание отличается от перетаскивания карты порогом 10 dp.",
        "Пустая грядка раскрывает веер круглых кнопок 48 pt: семена с ценой. Грядка в кольце, камера сама подвигает её из-под HUD.",
        "Закрытое остаётся на месте, серым, с замком и уровнем. Касание — кнопка качается и говорит причину.",
        "Растущая грядка: имя, таймер, полоса и одно действие — ускорить за кристаллы (только после вопроса).",
    ])


def sb_plant():
    base = {i: {} for i in range(10)}
    x0, y0 = plot_point(2)

    a = field(); a.ring(x0, y0, PLOT_W); plots(a, base); a.hud()
    ax, ay = a.view(x0, y0 + 58)
    pts = fan(3, ax, ay, 86)
    a.item(*pts[0], "corn", "1", "coin", pressed=True, glow=True); a.item(*pts[1], "carrot", "1", "coin"); a.item(*pts[2], "tomato", "2", "coin")
    a.plate(ax, min(p[1] for p in pts) - 72 - 75, "Что посадить?")
    a.finger(pts[0][0], pts[0][1] + 10)

    def row(sc, upto, coins):
        st = dict(base)
        for i in range(upto):
            st[i] = dict(stage=1, bar=0.03, squash=1.12 if i == upto - 1 else 1.0)
        plots(sc, st); sc.hud(coins=coins, coins_bump=True)

    b = field(); row(b, 3, "245")
    way = [pts[0]] + [b.view(plot_point(i)[0], plot_point(i)[1] + 50) for i in (0, 1, 2)]
    b.trail(way)
    fx, fy = way[-1]
    b.paste(icon("corn"), fx + 6, fy - 130, 150, 150, rot=-10)
    b.finger(fx, fy + 10)
    b.floater(b.view(*plot_point(2))[0], b.view(*plot_point(2))[1] - 190, "−1", "coin", fill=(255, 220, 100))

    c = field(); row(c, 5, "243")
    way = [pts[0]] + [c.view(plot_point(i)[0], plot_point(i)[1] + 50) for i in range(5)]
    c.trail(way)
    fx, fy = way[-1]
    c.paste(icon("corn"), fx + 6, fy - 130, 150, 150, rot=-10)
    c.finger(fx, fy + 10)
    c.floater(fx - 30, fy - 240, "−1", "coin", fill=(255, 220, 100))

    d = field(); st = dict(base)
    for i in range(5):
        st[i] = dict(stage=1, bar=0.03)
    for i in (9, 8, 7):
        st[i] = dict(stage=1, bar=0.03, squash=1.1 if i == 7 else 1)
    plots(d, st); d.hud(coins="0")
    way = [d.view(plot_point(i)[0], plot_point(i)[1] + 50) for i in (4, 9, 8, 7, 6)]
    d.trail(way)
    fx, fy = way[-1]
    d.paste(icon("corn"), fx + 6, fy - 130, 150, 150, rot=8)
    d.finger(fx, fy + 10)
    d.toast("Монеты кончились — семена стоят денег")
    board("02_drag_plant", "2. Посев одним движением: семена тянутся из меню по грядкам", [a, b, c, d], [
        "Палец берёт семена из меню. Пока он не ушёл с кнопки, ничего не сеется — случайный сдвиг не засеет грядку под меню.",
        "Семена летят над пальцем. Каждая пустая грядка под ними засевается один раз: росток, сжатие-растяжение, «−1» монета, счётчик тикает.",
        "Быстрый взмах между двумя кадрами не пропускает грядки: путь проходится по шагам (FarmSiteMap.Along). У края экрана карта едет сама.",
        "Если монет (и своих семян в амбаре) не хватило, движение останавливается с одной причиной, а не тостом на каждую грядку.",
    ])


def sb_harvest():
    ripe = {i: dict(stage=4, crop="corn") for i in range(5)}
    ripe.update({i: dict(stage=4, crop="carrot") for i in range(5, 8)})
    ripe.update({8: dict(stage=2, bar=0.6), 9: dict(stage=2, bar=0.55)})

    a = field(); plots(a, ripe); a.hud(barn_badge=None)
    x, y = a.view(plot_point(0)[0], plot_point(0)[1] + 58)
    a.finger(x, y)
    a.text(W / 2, a.view(*plot_point(7))[1] + 150, "Спелое покачивается и подпрыгивает", 34)

    def cut(sc, upto, flying):
        st = dict(ripe)
        for i in range(upto):
            st[i] = dict(squash=1.1 if i == upto - 1 else 1.0)
        plots(sc, st)

    b = field(); cut(b, 1, 0); b.hud(barn_bump=False)
    x0, y0 = plot_point(0)
    b.ring(x0, y0, PLOT_W); b.plot(0)
    ax, ay = b.view(x0, y0 + 58)
    p = fan(1, ax, ay, 86)[0]
    b.item(*p, "sickle", glow=True)
    b.plate(ax, p[1] - 72 - 80, "Урожай")
    b.text(W / 2, b.view(*plot_point(7))[1] + 150, "Проведите серпом по спелому", 32)
    barn = (W - 24 - 86, 24 + 132 + 16 + 156 + 2 * 216 + 68)
    arc = b.arc((ax, ay), barn)
    b.trail(arc[:16], col=(255, 255, 255, 120), dash=14, width=6)
    for t in (9, 13):
        b.paste(icon("corn"), arc[t][0], arc[t][1], 96, 96)
    b.floater(ax, ay - 40, "+2", "corn")

    c = field(); cut(c, 4, 3); c.hud(barn_bump=True, barn_badge=None)
    way = [p] + [c.view(plot_point(i)[0], plot_point(i)[1] + 50) for i in range(1, 4)]
    c.trail(way)
    fx, fy = way[-1]
    c.paste(icon("sickle"), fx + 10, fy - 130, 160, 160, rot=-18)
    c.finger(fx, fy + 10)
    for i, t in ((3, 6), (2, 13), (1, 20)):
        sx, sy = c.view(plot_point(i)[0], plot_point(i)[1] + 60)
        arc = c.arc((sx, sy), barn, side=(i - 2) * 40)
        c.paste(icon("corn"), arc[t][0], arc[t][1], 96 * (1 - t / 60), 96 * (1 - t / 60))
    c.floater(fx - 20, fy - 250, "+2", "corn")

    d = field(); cut(d, 7, 0); d.hud(barn_bump=False)
    way = [d.view(plot_point(i)[0], plot_point(i)[1] + 50) for i in (4, 9, 6, 7)]
    way = [d.view(plot_point(4)[0], plot_point(4)[1] + 50), d.view(plot_point(5)[0] - 60, plot_point(5)[1] + 50), d.view(plot_point(7)[0], plot_point(7)[1] + 50)]
    d.trail(way)
    fx, fy = way[-1]
    d.paste(icon("sickle"), fx + 10, fy - 130, 160, 160, rot=14)
    d.finger(fx, fy + 10)
    d.side(barn[0] - 10, barn[1], "barn", "Амбар", (238, 90, 60))
    d.toast("Амбар полон — продайте товары или расширьте его")
    board("03_harvest_swipe", "3. Уборка: касание собирает одну грядку, серп — всё, по чему прошёл", [a, b, c, d], [
        "Созревшее видно издалека: над грядкой подпрыгивает урожай, на кнопке «Поля» — красная точка. Кнопка «Поля» теперь везёт камеру сюда.",
        "Касание сразу собирает грядку: «+2», початки летят по дуге к «Амбару». Раз спелого ещё много — рядом остаётся серп.",
        "Серп тянут по грядкам: срезает только спелое, растущее не трогает. Каждый урожай летит в амбар, кнопка амбара подпрыгивает на прилёт.",
        "Амбар полон — движение останавливается, серп и кнопка «Амбар» качаются, причина сказана один раз.",
    ])


def sb_animals():
    cx, cy = zone("coop")
    px, py = cx + 800, cy + 150
    spots = [(px - 110 + c * 140 + r * 36, py + 130 - r * 130) for r in range(2) for c in range(3)]
    hen = art("Obstacles/Chick")

    def coop(states, select=None):
        sc = Scene(px + 40, py + 260, 1.15)
        sc.put(art("Farm/PenSign"), px - 270, py + 20, 140, 150)
        sc.put(hen, px - 270, py + 120, 70, 70, foot=False)
        for i, (x, y) in sorted(enumerate(spots[:4]), key=lambda t: -t[1][1]):
            if select == i:
                sc.ring(x, y, 132)
            sc.put(hen, x, y, 132, 132)
            s = states[i]
            if s == "hungry":
                sc.bubble(x, y + 132, "corn")
            elif s == "ready":
                sc.bubble(x, y + 132, "basket", dy=14)
            elif s == "fed":
                sc.bar(x, y - 8, 106, 0.3)
        return sc

    a = coop(["hungry", "hungry", "ready", "fed"]); a.hud()
    x, y = a.view(spots[0][0], spots[0][1] + 66)
    a.finger(x, y)

    b = coop(["fed", "hungry", "ready", "fed"], select=0); b.hud()
    ax, ay = b.view(spots[0][0], spots[0][1] + 66)
    p = fan(1, ax, ay, 76)[0]
    b.item(*p, "corn", "×2", glow=True)
    b.plate(ax + 60, p[1] - 72 - 80, "Корм для кур")
    b.floater(ax + 150, ay - 10, "−1", "corn", fill=(255, 230, 190))
    way = [p, b.view(spots[1][0], spots[1][1] + 66)]
    b.trail(way)
    b.finger(way[1][0], way[1][1] + 10)

    c = coop(["fed", "fed", "ready", "fed"]); c.hud(barn_bump=True)
    x, y = c.view(spots[2][0], spots[2][1] + 66)
    c.finger(x, y)
    barn = (W - 24 - 86, 24 + 132 + 16 + 156 + 2 * 216 + 68)
    arc = c.arc((x, y - 60), barn)
    c.trail(arc[:18], col=(255, 255, 255, 120), dash=14, width=6)
    c.paste(icon("basket"), arc[12][0], arc[12][1], 96, 96)
    c.floater(x, y - 200, "+1", "basket")

    d = coop(["fed", "fed", "hungry", "fed"], select=2); d.hud()
    ax, ay = d.view(spots[2][0], spots[2][1] + 66)
    pts = fan(2, ax, ay, 76)
    d.item(*pts[0], "corn", "×0", enabled=False)
    d.item(*pts[1], "workshop", "Мельница")
    d.plate(ax - 40, min(q[1] for q in pts) - 72 - 100, "Курица", "Нужен корм: корм для кур")
    board("04_animals", "4. Животные: касание кормит и собирает; корм и корзина тянутся по загону", [a, b, c, d], [
        "Голодная курица показывает корм в пузыре, готовая — подпрыгивающий продукт, сытая — тонкую полосу. Животные дышат и подскакивают.",
        "Касание по голодной кормит её сразу («−1» корм). Остались голодные — рядом остаётся корм: им проводят по остальным.",
        "Касание по готовому забирает продукт: он летит по дуге к «Амбару», кнопка подпрыгивает. Много готовых — проводят корзиной.",
        "Корма нет — кнопка корма серая с причиной, рядом путь: «Мельница» (камера летит к ней и открывает её производство).",
    ])


def sb_workshop():
    mx, my = zone("mill")
    wx, wy = mx + 800, my + 500

    def mill(zoom=0.8):
        return Scene(wx, wy + 250, zoom)

    a = mill(); a.hud(); a.bubble(wx, wy + 340, "corn", dy=10)
    x, y = a.view(wx, wy + 170)
    a.finger(x, y)

    b = mill(); b.hud()
    b.ring(wx, wy, 380)
    ax, ay = b.view(wx, wy + 170)
    pts = fan(4, ax, ay, 150)
    b.item(*pts[0], "corn", "5 мин"); b.item(*pts[1], "tomato", "10 мин", enabled=False)
    b.item(*pts[2], "carrot", "Ур. 9", enabled=False, locked=True); b.item(*pts[3], "workshop", "Ещё")
    top = min(q[1] for q in pts) - 72 - 110
    b.d.rounded_rectangle([(ax - 260) * S, (top - 100) * S, (ax + 260) * S, (top + 100) * S], 30 * S, fill=(62, 42, 23, 210))
    b.text(ax, top - 62, "Мельница", 44)
    b.text(ax, top - 18, "Корм для кур · 03:12", 32, fill=(255, 238, 180))
    for i, (col, ic) in enumerate([((108, 192, 74), "corn"), ((255, 255, 255, 100), "corn"), ((255, 255, 255, 46), None)]):
        qx = ax + (i - 1) * 84
        b.d.ellipse([(qx - 36) * S, (top + 20) * S, (qx + 36) * S, (top + 92) * S], fill=col)
        if ic:
            b.paste(icon(ic), qx, top + 56, 56, 56)

    c = mill(); c.hud()
    c.ring(wx, wy, 380)
    c.item(*pts[0], "corn", "5 мин"); c.item(pts[1][0] + 8, pts[1][1], "tomato", "10 мин", enabled=False, pressed=True)
    c.item(*pts[2], "carrot", "Ур. 9", enabled=False, locked=True); c.item(*pts[3], "workshop", "Ещё")
    c.finger(pts[1][0] + 10, pts[1][1] + 20)
    c.toast("Мука — не хватает: пшеница 1/3")

    d = mill(); d.hud()
    x, y = d.view(wx - 600, wy - 140)
    for i, (nm, col) in enumerate([("Пекарня", (250, 200, 130)), ("Молочная", (190, 226, 248))]):
        w = art("Farm/Workshop").copy()
        tint = Image.new("RGBA", w.size, col + (255,))
        w = Image.composite(Image.blend(w, tint, 0.0), w, w.getchannel("A"))
        r, g, bl, al = w.split()
        w = Image.merge("RGBA", (r.point(lambda v: v * col[0] // 255), g.point(lambda v: v * col[1] // 255), bl.point(lambda v: v * col[2] // 255), al))
        d.put(w, wx - 220 + i * 420, wy - 330, 300, 300, alpha=1 if i == 0 else 0.75)
        if i == 1:
            d.tag(wx - 220 + i * 420, wy - 330 + 42, "900", "coin")
        else:
            d.bar(wx - 220, wy - 338, 150, 0.4)
    d.text(W / 2, d.view(wx, wy - 400)[1], "Новые мастерские встают в ряд у дороги:", 34)
    d.text(W / 2, d.view(wx, wy - 400)[1] + 46, "построенная работает, следующая — с ценой", 34)
    board("05_workshop", "5. Мастерская: компактное производство у самого здания", [a, b, c, d], [
        "Готовая продукция подпрыгивает в пузыре над зданием. Касание забирает её в амбар — без окна.",
        "Следующее касание раскрывает веер рецептов и плашку с очередью (готово / идёт / ждёт / свободно) и таймером. «Ещё» — полное окно.",
        "Рецепт, на который не хватает, остаётся серым и называет недостающее: «пшеница 1/3». Заполненная очередь — тоже причина.",
        "Что ещё не построено, стоит на карте полупрозрачным с ценой или уровнем: касание — «Построить» рядом со зданием.",
    ])


def sb_camera():
    cx, cy = FIELD[0] - 200, FIELD[1] - 100

    a = Scene(cx, cy, 0.8); a.hud()
    a.trail([(700, 1100), (560, 1010), (420, 920)], width=12)
    a.finger(420, 920)
    a.trail([(420, 920), (300, 843), (220, 792), (170, 760), (140, 741)], col=(255, 244, 150, 220), dash=12, width=8)
    a.text(W / 2, 1380, "ведение 1:1  →  отпустили: скольжение", 34)

    b = Scene(MAPW - 420, cy, 0.8); b.hud()
    b.trail([(760, 1000), (520, 1000)], width=12)
    b.finger(520, 1000)
    b.d.line([(W - 60) * S, 300 * S, (W - 60) * S, 1500 * S], fill=(255, 255, 255, 200), width=int(8 * S))
    b.text(W / 2 - 40, 1380, "за краем карта тянется всё туже", 34)
    b.text(W / 2 - 40, 1426, "и возвращается пружиной", 34)

    c = Scene(cx, cy, 1.15); c.hud()
    for (x0, y0), (x1, y1) in (((470, 1010), (330, 1150)), ((610, 870), (750, 730))):
        c.trail([(x0, y0), (x1, y1)], width=12)
        c.finger(x1, y1)
    c.d.ellipse([(540 - 14) * S, (940 - 14) * S, (540 + 14) * S, (940 + 14) * S], fill=(255, 244, 150, 255))
    c.text(W / 2, 1380, "щипок: точка между пальцами на месте", 34)
    c.text(W / 2, 1426, "масштаб 0,35…1,6 (чуть дальше — резина)", 34)

    d = Scene(cx, cy, 1.25); d.hud()
    d.finger(540, 940); d.finger(552, 948, down=False)
    d.text(W / 2, 1380, "двойное касание по земле:", 34)
    d.text(W / 2, 1426, "ближе (1,25) и обратно (0,8)", 34)
    board("06_camera", "6. Камера: палец ведёт карту, а не «листает страницу»", [a, b, c, d], [
        "Один палец ведёт карту 1:1; после взмаха она скользит и останавливается трением. Скорость берётся за последние 0,1 с.",
        "Мягкие границы: за краем карта идёт за пальцем всё слабее (до 170 ед.), отпустили — пружинит назад. Поля HUD учтены.",
        "Два пальца масштабируют вокруг точки между ними. Диапазон зажат; слегка за пределом — «резина» и возврат.",
        "Двойное касание приближает и возвращает. Касание по объекту двойным не считается: первое уже открыло меню.",
    ])


if __name__ == "__main__":
    sb_menu(); sb_plant(); sb_harvest(); sb_animals(); sb_workshop(); sb_camera()
