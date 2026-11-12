#!/usr/bin/env python3
"""Storyboards of the farm map's interactions (docs/FARM_UX.md), drawn from the game's real pictures: the chapter's
map and the farm's sites exactly as the game places and draws them (farmview.py over data/sites_*.tsv, which the
test DumpSitesForTheStoryboards writes from FarmSiteMap), the HUD icons and the font. The fan of round buttons and
the HUD's sizes repeat the code's (RadialLayout, Screens.Farm, Tokens). A picture of the design, not a screenshot.

    FARM_SITES_DUMP=farm/docs/farm-ux/data dotnet test farm/ui-tests --filter DumpSites
    python3 farm/docs/farm-ux/storyboards.py
"""
import copy
import math
import os
from PIL import Image, ImageDraw, ImageFont
import farmview as fv

HERE = os.path.dirname(os.path.abspath(__file__))
FONT = fv.FONT
S = 0.5                      # output pixels per canvas unit
W, H = 1080, 1920            # the canvas


def font(size):
    return ImageFont.truetype(FONT, int(size * S))


def fit(im, w, h):
    k = min(w / im.width, h / im.height)
    return im.resize((max(1, int(im.width * k)), max(1, int(im.height * k))), Image.LANCZOS)


def farm(base="debug", **changes):
    """The sites of a dumped farm with some of them changed: id → dict of fields (phase, item, progress, stage…)."""
    rows = copy.deepcopy(fv.sites(base))
    for r in rows:
        key = r["id"].replace(".", "_")
        if key in changes:
            r.update(changes[key])
    return rows


def site(rows, sid):
    return next(r for r in rows if r["id"] == sid)


EMPTY = dict(phase="Empty", item=None, stage=0, progress=0, attention=False)


def sown(item, progress=.03):
    return dict(phase="Growing", item=item, progress=progress, stage=1 if progress < .34 else 2 if progress < .75 else 3, attention=False)


def ripe(item):
    return dict(phase="Ready", item=item, stage=4, progress=1, attention=True)


class Scene:
    """One phone screen: the farm under a camera (a map point in the middle of the screen, a zoom), then the HUD
    and what the fingers do, in canvas units from the top left."""

    def __init__(self, rows, cx, cy, zoom, overrides=None, skip=()):
        self.cx, self.cy, self.zoom = cx, cy, zoom
        vw, vh = W / zoom, H / zoom
        x0, y1 = cx - vw / 2, cy + vh / 2
        base = fv.base_map(.5)
        box = (int(x0 * .5), int((fv.MAPH - y1) * .5), int((x0 + vw) * .5), int((fv.MAPH - y1 + vh) * .5))
        self.im = Image.new("RGBA", (int(W * S), int(H * S)), (121, 185, 78, 255))
        self.im.alpha_composite(base.crop(box).resize(self.im.size, Image.LANCZOS))
        fv.paint_farm(fv.Painter(self.im, zoom * S, x0, y1), rows, overrides, skip)
        self.d = ImageDraw.Draw(self.im)

    def view(self, x, y):
        """A map point in canvas units."""
        return (x - self.cx) * self.zoom + W / 2, H / 2 - (y - self.cy) * self.zoom

    def at(self, s, up=.5):
        """A site's middle (or another height of it) in canvas units."""
        return self.view(s["x"], s["y"] + s["h"] * up)

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


    def hud(self, coins="248", barn_bump=False, barn_badge=None, coins_bump=False):
        d = self.d
        M, G = 24, 16
        pw = (W - 2 * M - 3 * G) / 4
        vals = [("coin", coins), ("star", "3"), ("crystal", "120"), ("heart", "Полные")]
        for i, (ic, v) in enumerate(vals):
            x = M + i * (pw + G)
            d.rounded_rectangle([x * S, (M + 14) * S, (x + pw) * S, (M + 14 + 104) * S], 52 * S, fill=(62, 42, 23, 200))
            k = 1.25 if (coins_bump and i == 0) else 1
            self.paste(fv.icon(ic), x + 52, M + 66, 92 * k, 92 * k)
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
        self.paste(fv.icon("xp"), W / 2 - 250, py - 102, 108, 108)
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
        self.paste(fv.icon(ic), x, y, size * 0.72 * k, size * 0.72 * k)

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
        self.paste(fv.icon(ic), x, y - (12 if label else 0), 92, 92, alpha=1 if enabled else 0.5)
        if label:
            if label_icon:
                self.paste(fv.icon(label_icon), x - 40, y + 46, 38, 38)
            self.text(x + (16 if label_icon else 0), y + 46, label, 32)
        if locked:
            self.paste(fv.icon("lock"), x + 48, y - 48, 54, 54)

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
            self.paste(fv.icon(ic), x - 44, y, 64, 64)
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

BARN = (W - 24 - 86, 24 + 132 + 16 + 156 + 2 * 216 + 68)      # the «Амбар» button


def field_cam(rows):
    # over the plots that are there (the first rows of the field), a little closer than «Поля» flies to
    ps = [r for r in rows if r["kind"] == "Plot"]
    return sum(p["x"] for p in ps) / len(ps), sum(p["y"] for p in ps) / len(ps) - 190, 1.05


def menu(sc, s, items, title, sub=None, bar=None, radius=70, pressed=None, glow=None):
    ax, ay = sc.at(s)
    pts = fan(len(items), ax, ay, radius)
    for i, it in enumerate(items):
        x, y = pts[i]
        sc.item(x + (8 if pressed == i else 0), y, *it[:1], **dict(label=it[1] if len(it) > 1 else None, label_icon=it[2] if len(it) > 2 else None,
                                                                enabled=it[3] if len(it) > 3 else True, locked=it[4] if len(it) > 4 else False,
                                                                pressed=pressed == i, glow=glow == i))
    top = min(p[1] for p in pts) - 72 - (75 if sub is None and bar is None else 100)
    sc.plate(min(max(ax, 290), W - 290), top, title, sub, bar)
    return pts


SEEDS = [("item.wheat", "1", "coin"), ("corn", "1", "coin"), ("carrot", "1", "coin"), ("item.sunflower", "2", "coin"), ("item.cotton", "Ур. 8", None, False, True)]


def sb_menu():
    empty = {"plot_%d" % i: EMPTY for i in (0, 1, 2, 3, 4)}
    rows = farm(**empty)
    cam = field_cam(rows)
    p2, p7 = site(rows, "plot.2"), site(rows, "plot.7")

    a = Scene(rows, *cam, overrides={"plot.2": dict(press=True)}); a.hud()
    a.finger(*a.at(p2))

    b = Scene(rows, *cam, overrides={"plot.2": dict(selected=True)}); b.hud()
    menu(b, p2, SEEDS, "Что посадить?")
    b.text(W / 2, b.at(site(rows, "plot.0"))[1] + 170, "Проведите семенами по грядкам", 32)

    c = Scene(rows, *cam, overrides={"plot.2": dict(selected=True)}); c.hud()
    pts = menu(c, p2, SEEDS, "Что посадить?", pressed=4)
    c.finger(pts[4][0] + 10, pts[4][1] + 20)
    c.toast("Хлопок: откроется на 8 уровне фермы")

    d = Scene(rows, *cam, overrides={"plot.7": dict(selected=True)}); d.hud()
    menu(d, p7, [("crystal", "2")], "Клубника", "38:20", .33)
    board("01_tap_menu", "1. Касание по грядке: меню рядом с ней, а не окно со списком", [a, b, c, d], [
        "Палец на грядке: она проседает (93%) и пружинит обратно. Касание отличается от перетаскивания карты порогом 10 dp.",
        "Пустая грядка раскрывает веер круглых кнопок 48 pt: семена с ценой. Грядка в кольце, камера сама подвигает её из-под HUD.",
        "Закрытое остаётся на месте, серым, с замком и уровнем. Касание — кнопка качается и говорит причину.",
        "Растущая грядка: имя, таймер, полоса и одно действие — ускорить за кристаллы (только после вопроса).",
    ])


def sb_plant():
    ids = ["plot_%d" % i for i in range(10)]
    base = {k: EMPTY for k in ids}
    rows0 = farm(**base)
    cam = field_cam(rows0)
    P = lambda rows, i: site(rows, "plot.%d" % i)

    a = Scene(rows0, *cam, overrides={"plot.2": dict(selected=True)}); a.hud()
    pts = menu(a, P(rows0, 2), SEEDS[:4], "Что посадить?", pressed=0, glow=0)
    a.finger(pts[0][0], pts[0][1] + 10)

    def swept(upto, coins, pop):
        ch = dict(base)
        for i in upto:
            ch["plot_%d" % i] = sown("wheat")
        rows = farm(**ch)
        sc = Scene(rows, *cam, overrides={"plot.%d" % pop: dict(squash=1.1)})
        sc.hud(coins=coins, coins_bump=True)
        return sc, rows

    b, rows = swept((0, 1, 2), "245", 2)
    way = [pts[0]] + [b.at(P(rows, i)) for i in (0, 1, 2)]
    b.trail(way)
    fx, fy = way[-1]
    b.paste(fv.icon("item.wheat"), fx + 6, fy - 130, 150, 150, rot=-10)
    b.finger(fx, fy + 10)
    b.floater(fx + 20, fy - 250, "−1", "coin", fill=(255, 220, 100))

    c, rows = swept((0, 1, 2, 3, 4), "243", 4)
    way = [pts[0]] + [c.at(P(rows, i)) for i in range(5)]
    c.trail(way)
    fx, fy = way[-1]
    c.paste(fv.icon("item.wheat"), fx + 6, fy - 130, 150, 150, rot=-10)
    c.finger(fx, fy + 10)
    c.floater(fx - 30, fy - 250, "−1", "coin", fill=(255, 220, 100))

    d, rows = swept((0, 1, 2, 3, 4, 9, 8, 7), "0", 7)
    way = [d.at(P(rows, i)) for i in (4, 9, 8, 7, 6)]
    d.trail(way)
    fx, fy = way[-1]
    d.paste(fv.icon("item.wheat"), fx + 6, fy - 130, 150, 150, rot=8)
    d.finger(fx, fy + 10)
    d.toast("Монеты кончились — семена стоят денег")
    board("02_drag_plant", "2. Посев одним движением: семена тянутся из меню по грядкам", [a, b, c, d], [
        "Палец берёт семена из меню. Пока он не ушёл с кнопки, ничего не сеется — случайный сдвиг не засеет грядку под меню.",
        "Семена летят над пальцем. Каждая пустая грядка под ними засевается один раз: всходы, сжатие-растяжение, «−1» монета, счётчик тикает.",
        "Ряды идут по диагонали карты, как сама земля. Быстрый взмах не пропускает грядки: путь проходится по шагам. У края экрана карта едет сама.",
        "Если монет (и своих семян в амбаре) не хватило, движение останавливается с одной причиной, а не тостом на каждую грядку.",
    ])


def sb_harvest():
    base = {"plot_%d" % i: ripe("wheat") for i in range(5)}
    base.update({"plot_5": ripe("carrot"), "plot_6": ripe("carrot"), "plot_7": ripe("strawberry"), "plot_8": sown("sunflower", .6), "plot_9": sown("corn", .5)})
    rows0 = farm(**base)
    cam = field_cam(rows0)
    P = lambda rows, i: site(rows, "plot.%d" % i)

    a = Scene(rows0, *cam); a.hud()
    a.finger(*a.at(P(rows0, 0)))
    a.text(W / 2, a.at(P(rows0, 0))[1] + 170, "Спелое покачивается и подпрыгивает", 34)

    def cut(done, pop=None):
        ch = dict(base)
        for i in done:
            ch["plot_%d" % i] = EMPTY
        rows = farm(**ch)
        ov = {"plot.%d" % pop: dict(squash=1.1)} if pop is not None else {}
        return rows, ov

    rows, ov = cut((0,), 0)
    ov["plot.0"] = dict(selected=True, squash=1.08)
    b = Scene(rows, *cam, overrides=ov); b.hud()
    pts = menu(b, P(rows, 0), [("tool.sickle",)], "Урожай", glow=0)
    ax, ay = b.at(P(rows, 0))
    arc = b.arc((ax, ay), BARN)
    b.trail(arc[:17], col=(255, 255, 255, 130), dash=14, width=6)
    for t in (9, 14):
        b.paste(fv.icon("item.wheat"), arc[t][0], arc[t][1], 96, 96)
    b.floater(ax + 110, ay - 30, "+2", "item.wheat")
    b.text(W / 2, ay + 170, "Проведите серпом по спелому", 32)

    rows, ov = cut((0, 1, 2, 3), 3)
    c = Scene(rows, *cam, overrides=ov); c.hud(barn_bump=True)
    way = [pts[0]] + [c.at(P(rows, i)) for i in (1, 2, 3)]
    c.trail(way)
    fx, fy = way[-1]
    c.paste(fv.icon("tool.sickle"), fx + 10, fy - 130, 160, 160, rot=-18)
    c.finger(fx, fy + 10)
    for i, t in ((3, 6), (2, 13), (1, 20)):
        sx, sy = c.at(P(rows, i))
        arc = c.arc((sx, sy), BARN, side=(i - 2) * 40)
        c.paste(fv.icon("item.wheat"), arc[t][0], arc[t][1], 96 * (1 - t / 60), 96 * (1 - t / 60))
    c.floater(fx - 20, fy - 250, "+2", "item.wheat")

    rows, ov = cut((0, 1, 2, 3, 4, 5), 5)
    d = Scene(rows, *cam, overrides=ov); d.hud()
    way = [d.at(P(rows, i)) for i in (4, 5, 6)]
    d.trail(way)
    fx, fy = way[-1]
    d.paste(fv.icon("tool.sickle"), fx + 10, fy - 130, 160, 160, rot=14)
    d.finger(fx, fy + 10)
    d.side(BARN[0] - 10, BARN[1], "barn", "Амбар", (238, 90, 60))
    d.toast("Амбар полон — продайте товары или расширьте его")
    board("03_harvest_swipe", "3. Уборка: касание собирает одну грядку, серп — всё, по чему прошёл", [a, b, c, d], [
        "Созревшее видно издалека: спелые растения и подпрыгивающий урожай над грядкой, на кнопке «Поля» — красная точка. «Поля» везёт камеру сюда.",
        "Касание сразу собирает грядку: «+2», колосья летят по дуге к «Амбару». Раз спелого ещё много — рядом остаётся серп.",
        "Серп тянут по грядкам: срезает только спелое, растущее не трогает. Каждый урожай летит в амбар, кнопка амбара подпрыгивает на прилёт.",
        "Амбар полон — движение останавливается, серп и кнопка «Амбар» качаются, причина сказана один раз.",
    ])


def sb_animals():
    rows0 = fv.sites("debug")
    hens = [r for r in rows0 if r["kind"] == "Animal" and r["ref"] == "chicken"]
    ids = [h["id"].replace(".", "_") for h in hens]
    yard = site(rows0, "yard.coop")
    cam = (yard["x"] + 60, yard["y"] - 150, 1.25)
    hungry = dict(phase="Hungry", item="chicken_feed", attention=True, progress=0)
    fed = dict(phase="Growing", item="egg", progress=.3, attention=False)
    egg = dict(phase="Ready", item="egg", attention=True, progress=1)

    def coop(states, **kw):
        rows = farm(**{ids[i]: st for i, st in enumerate(states)})
        return Scene(rows, *cam, **kw), [site(rows, h["id"]) for h in hens]

    a, hs = coop([hungry, hungry, egg]); a.hud()
    a.finger(*a.at(hs[0]))

    b, hs = coop([fed, hungry, egg], overrides={hens[0]["id"]: dict(selected=True, squash=1.08)}); b.hud()
    pts = menu(b, hs[0], [("item.chicken_feed", "×2")], "Корм для кур", radius=60, glow=0)
    ax, ay = b.at(hs[0])
    b.floater(ax - 150, ay - 10, "−1", "item.chicken_feed", fill=(255, 230, 190))
    way = [pts[0], b.at(hs[1])]
    b.trail(way)
    b.finger(way[1][0], way[1][1] + 10)

    c, hs = coop([fed, fed, hungry], overrides={hens[2]["id"]: dict(squash=1.08)}); c.hud(barn_bump=True)
    x, y = c.at(hs[2])
    c.finger(x, y)
    arc = c.arc((x, y - 60), BARN)
    c.trail(arc[:18], col=(255, 255, 255, 130), dash=14, width=6)
    c.paste(fv.icon("item.egg"), arc[12][0], arc[12][1], 96, 96)
    c.floater(x - 120, y - 120, "+1", "item.egg")

    nofeed = dict(phase="Hungry", item="chicken_feed", attention=False, progress=0)
    d, hs = coop([fed, fed, nofeed], overrides={hens[2]["id"]: dict(selected=True)}); d.hud()
    menu(d, hs[2], [("item.chicken_feed", "×0", None, False), ("workshop", "Мельница")], "Курица", "Нужен корм: корм для кур", radius=60)
    board("04_animals", "4. Животные: касание кормит и собирает; корм и корзина тянутся по загону", [a, b, c, d], [
        "Голодная курица показывает корм в пузыре, готовая стоит с яйцом и подпрыгивающим пузырём, сытая — с тонкой полосой. Животные дышат и подскакивают.",
        "Касание по голодной кормит её сразу («−1» корм). Остались голодные — рядом остаётся корм: им проводят по остальным.",
        "Касание по готовому забирает продукт: он летит по дуге к «Амбару», кнопка подпрыгивает. Много готовых — проводят корзиной.",
        "Корма нет — кнопка корма серая с причиной, рядом путь: «Мельница» (камера летит к ней и открывает её производство).",
    ])


def sb_workshop():
    rows0 = fv.sites("debug")
    bk = site(rows0, "workshop.bakery")
    cam = (bk["x"] + 130, bk["y"] + 330, 0.95)

    a = Scene(farm(workshop_bakery=dict(phase="Ready", item="bread", attention=True, count=1)), *cam); a.hud()
    a.finger(*a.at(bk, .4))

    work = dict(phase="Growing", item="corn_bread", progress=.4, attention=False)
    b = Scene(farm(workshop_bakery=work), *cam, overrides={"workshop.bakery": dict(selected=True)}); b.hud()
    items = [("item.bread", "15 мин"), ("item.corn_bread", "30 мин", None, False), ("item.apple_pie", "1 ч"), ("item.cookies", "Ур. 11", None, False, True), ("workshop", "Ещё")]
    ax, ay = b.at(bk, .45)
    pts = fan(len(items), ax, ay, 150)

    def recipes(sc, pressed=None):
        for i, it in enumerate(items):
            sc.item(pts[i][0] + (8 if pressed == i else 0), pts[i][1], it[0], it[1], enabled=it[3] if len(it) > 3 else True, locked=it[4] if len(it) > 4 else False, pressed=pressed == i)
        top = min(q[1] for q in pts) - 72 - 110
        sc.d.rounded_rectangle([(ax - 260) * S, (top - 100) * S, (ax + 260) * S, (top + 100) * S], 30 * S, fill=(62, 42, 23, 210))
        sc.text(ax, top - 62, "Пекарня", 44)
        sc.text(ax, top - 18, "Кукурузный хлеб · 18:02", 32, fill=(255, 238, 180))
        for i, (col, ic) in enumerate([((108, 192, 74), "item.corn_bread"), ((255, 255, 255, 46), None)]):
            qx = ax + (i - .5) * 84
            sc.d.ellipse([(qx - 36) * S, (top + 20) * S, (qx + 36) * S, (top + 92) * S], fill=col)
            if ic:
                sc.paste(fv.icon(ic), qx, top + 56, 56, 56)

    recipes(b)
    c = Scene(farm(workshop_bakery=work), *cam, overrides={"workshop.bakery": dict(selected=True)}); c.hud()
    recipes(c, pressed=1)
    c.finger(pts[1][0] + 10, pts[1][1] + 20)
    c.toast("Кукурузный хлеб — не хватает: яйцо 0/1")

    jam = site(rows0, "workshop.jam")
    d = Scene(rows0, jam["x"] + 60, jam["y"] + 330, 0.95); d.hud()
    d.finger(*d.at(jam, .4))
    d.text(W / 2, 1330, "Что ещё не построено, стоит на своём месте:", 34)
    d.text(W / 2, 1376, "с ценой — можно строить, с замком — по уровню", 34)
    board("05_workshop", "5. Мастерская: компактное производство у самого здания", [a, b, c, d], [
        "Готовая продукция подпрыгивает в пузыре над зданием. Касание забирает её в амбар — без окна.",
        "Следующее касание раскрывает веер рецептов и плашку с очередью (идёт / свободно) и таймером. «Ещё» — полное окно мастерской.",
        "Рецепт, на который не хватает, остаётся серым и называет недостающее: «яйцо 0/1». Заполненная очередь — тоже причина.",
        "У каждой мастерской своё здание. Непостроенные стоят на улице полупрозрачными: касание — «Построить» рядом со зданием.",
    ])


def sb_camera():
    rows = fv.sites("debug")
    f = site(rows, "field")
    cx, cy = f["x"] - 150, f["y"] - 150

    a = Scene(rows, cx, cy, 0.8); a.hud()
    a.trail([(700, 1100), (560, 1010), (420, 920)], width=12)
    a.finger(420, 920)
    a.trail([(420, 920), (300, 843), (220, 792), (170, 760), (140, 741)], col=(255, 244, 150, 220), dash=12, width=8)
    a.text(W / 2, 1380, "ведение 1:1  →  отпустили: скольжение", 34)

    b = Scene(rows, fv.MAPW - 420, cy, 0.8); b.hud()
    b.trail([(760, 1000), (520, 1000)], width=12)
    b.finger(520, 1000)
    b.d.line([(W - 60) * S, 300 * S, (W - 60) * S, 1500 * S], fill=(255, 255, 255, 200), width=int(8 * S))
    b.text(W / 2 - 40, 1380, "за краем карта тянется всё туже", 34)
    b.text(W / 2 - 40, 1426, "и возвращается пружиной", 34)

    c = Scene(rows, cx, cy, 1.15); c.hud()
    for (x0, y0), (x1, y1) in (((470, 1010), (330, 1150)), ((610, 870), (750, 730))):
        c.trail([(x0, y0), (x1, y1)], width=12)
        c.finger(x1, y1)
    c.d.ellipse([(540 - 14) * S, (940 - 14) * S, (540 + 14) * S, (940 + 14) * S], fill=(255, 244, 150, 255))
    c.text(W / 2, 1380, "щипок: точка между пальцами на месте", 34)
    c.text(W / 2, 1426, "масштаб 0,35…1,6 (чуть дальше — резина)", 34)

    d = Scene(rows, cx, cy, 1.25); d.hud()
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
