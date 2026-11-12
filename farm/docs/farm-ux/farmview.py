"""Draws the working farm as the game does (Game/UI/FarmSitesView.cs), for the pictures in this folder: the chapter's
map from Resources (chapter1_layout.json and its art) with the farm's sites on it, placed and in the states that
FarmSiteMap gives (data/sites_*.tsv, written by the test DumpSitesForTheStoryboards with FARM_SITES_DUMP set)."""
import json
import os
import random
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
RES = os.path.join(HERE, "..", "..", "unity", "SunnyValley", "Assets", "SunnyValley", "Resources")
ART = os.path.join(RES, "Art")
FONT = os.path.join(RES, "Fonts", "Nunito-ExtraBold.ttf")
LAYOUT = json.load(open(os.path.join(RES, "Meta", "chapter1_layout.json"), encoding="utf-8"))
MAPW, MAPH = LAYOUT["map"]["w"], LAYOUT["map"]["h"]

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


ICON_ART = {"coin": "Currency/Coin", "star": "Currency/Star", "crystal": "Currency/Crystal", "heart": "Currency/Heart",
            "corn": "Tiles/Corn", "carrot": "Tiles/Carrot", "apple": "Tiles/Apple", "tomato": "Tiles/Tomato", "eggplant": "Tiles/Eggplant",
            "blueberry": "Tiles/Blueberry", "basket": "Obstacles/Basket", "tool.sickle": "Farm/Sickle",
            "shovel": "Boosters/Shovel", "rake": "Boosters/Rake", "can": "Boosters/WateringCan", "shuffle": "Boosters/Shuffle",
            "pitchfork": "Specials/Pitchfork", "bomb": "Specials/Bomb", "rainbow": "Specials/Rainbow"}
PIECES = ("tomato", "corn", "apple", "carrot", "eggplant", "blueberry")
BOOSTERS = {"Line": "pitchfork", "Bomb": "bomb", "Rainbow": "rainbow", "Shovel": "shovel", "Rake": "rake", "WateringCan": "can", "Shuffle": "shuffle"}


def icon(name):
    """An icon by the game's name (ShapeSprites.Icon): painted art, else Resources/Icons."""
    if name in ICON_ART:
        return art(ICON_ART[name])
    key = "icon:" + name
    if key not in _cache:
        p = os.path.join(RES, "Icons", name + ".png")
        _cache[key] = Image.open(p).convert("RGBA") if os.path.exists(p) else None
    return _cache[key]


def item_icon(item):
    """EconomyViews.IconOf."""
    if item in BOOSTERS:
        return icon(BOOSTERS[item])
    return icon(item if item in PIECES else "item." + item)


def base_map(ms):
    """The restored farm of chapter 1 at a scale, as FarmWorld builds it (the painted hens are hidden: the farm has live ones)."""
    key = "map:%s" % ms
    if key in _cache:
        return _cache[key]
    im = Image.new("RGBA", (int(MAPW * ms), int(MAPH * ms)), (121, 185, 78, 255))
    for z in sorted(LAYOUT["zones"], key=lambda z: -z["y"]):
        bg = art(z["background"])
        if bg:
            im.paste(bg.resize((int(z["w"] * ms), int(z["h"] * ms)), Image.LANCZOS), (int(z["x"] * ms), int((MAPH - z["y"] - z["h"]) * ms)))
        for s in sorted(z["sprites"], key=lambda s: s.get("z", 0)):
            if s.get("show", "after") == "before" or s.get("until") or s["art"] == "Ch1/Objects/Hens":
                continue
            a = art(s["art"])
            if a is None:
                continue
            k = min(s["w"] * ms / a.width, s["h"] * ms / a.height)
            a = a.resize((max(1, int(a.width * k)), max(1, int(a.height * k))), Image.LANCZOS)
            im.alpha_composite(a, (int((z["x"] + s["x"]) * ms - a.width / 2), int((MAPH - z["y"] - s["y"]) * ms - a.height)))
    _cache[key] = im
    return im


def sites(name):
    rows = []
    for line in open(os.path.join(HERE, "data", "sites_%s.tsv" % name), encoding="utf-8"):
        f = line.rstrip("\n").split("\t")
        rows.append(dict(id=f[0], kind=f[1], ref=f[2], x=float(f[3]), y=float(f[4]), w=float(f[5]), h=float(f[6]), phase=f[7],
                         item=None if f[8] == "-" else f[8], progress=float(f[9]), stage=int(f[10]), attention=f[11] == "1", count=int(f[12]), level=int(f[13])))
    return rows


ANIMAL_CANVAS = dict(chicken=(96, 88, 40, 26), sheep=(150, 122, 64, 38), pig=(160, 108, 68, 34), cow=(214, 172, 96, 50), bee=(120, 118, 46, 32))


class Painter:
    """Puts pictures on a map image: positions in map units (y up), the image at ms pixels per unit."""

    def __init__(self, im, ms, x0=0, y1=MAPH):
        self.im, self.ms, self.x0, self.y1 = im, ms, x0, y1
        self.d = ImageDraw.Draw(im)

    def px(self, x, y):
        return (x - self.x0) * self.ms, (self.y1 - y) * self.ms

    def put(self, sprite, left, bottom, w, h, alpha=1.0, sx=1.0, sy=1.0):
        """A picture stretched to w × h map units, its lower left corner at (left, bottom)."""
        if sprite is None:
            return
        W, H = max(1, int(round(w * self.ms * sx))), max(1, int(round(h * self.ms * sy)))
        a = sprite.resize((W, H), Image.LANCZOS)
        if alpha < 1:
            a.putalpha(a.getchannel("A").point(lambda v: int(v * alpha)))
        X, Y = self.px(left + w / 2, bottom)
        x, y = int(round(X - W / 2)), int(round(Y - H))
        if x >= self.im.width or y >= self.im.height or x + W <= 0 or y + H <= 0:
            return
        self.im.alpha_composite(a, (max(0, x), max(0, y)), (max(0, -x), max(0, -y), min(W, self.im.width - x), min(H, self.im.height - y)))

    def fit(self, sprite, cx, cy, size, alpha=1.0):
        """A picture inside a square around (cx, cy), its proportions kept."""
        if sprite is None:
            return
        k = size / max(sprite.width, sprite.height)
        self.put(sprite, cx - sprite.width * k / 2, cy - sprite.height * k / 2, sprite.width * k, sprite.height * k, alpha)

    def bar(self, cx, cy, w, value):
        X, Y = self.px(cx, cy)
        w2, h2 = w * self.ms, 18 * self.ms
        self.d.rounded_rectangle([X - w2 / 2, Y - h2 / 2, X + w2 / 2, Y + h2 / 2], h2 / 2, fill=(58, 36, 20, 235))
        self.d.rounded_rectangle([X - w2 / 2 + 2 * self.ms, Y - h2 / 2 + 2 * self.ms, X - w2 / 2 + 2 * self.ms + max(h2 - 4 * self.ms, (w2 - 4 * self.ms) * value), Y + h2 / 2 - 2 * self.ms],
                                 h2 / 2, fill=(108, 192, 74, 255))

    def tag(self, cx, cy, text, ic):
        X, Y = self.px(cx, cy)
        k = self.ms
        w2, h2 = 160 * k, 52 * k
        self.d.rounded_rectangle([X - w2 / 2, Y - h2 / 2, X + w2 / 2, Y + h2 / 2], h2 / 2, fill=(62, 42, 23, 205))
        self.fit(icon(ic), cx - 52, cy, 40)
        f = ImageFont.truetype(FONT, max(7, int(34 * k)))
        self.d.text((X + 16 * k, Y), text, font=f, fill="white", anchor="mm")

    def bubble(self, cx, bottom, ic, plain=False, dim=False, lift=0):
        if plain:
            self.fit(ic, cx, bottom + lift + 38, 72)
            return
        self.put(art("Farm/Bubble"), cx - 48, bottom + lift, 96, 110, .8 if dim else 1)
        self.fit(ic, cx, bottom + lift + 63, 60, .5 if dim else 1)


def bubble_y(s):
    k = s["kind"]
    return s["h"] * .5 if k == "Tree" else s["h"] * .9 if k == "Plot" else s["h"] * .86 if k == "Workshop" else s["h"] + 2


def growth(s):
    """FarmSitesView.Growth: the picture of a growing thing as it stands."""
    k = s["kind"]
    if k == "Plot":
        if s["stage"] <= 0 or not s["item"]:
            return None
        if s["phase"] == "Growing" and s["progress"] < .08:
            return "Seeded"
        return "Crop_%s_%d" % (s["item"], s["stage"])
    if k == "Tree":
        return "Tree_%s_%s%s" % (s["ref"], "ripe" if s["phase"] == "Ready" else "bloom" if s["progress"] >= .5 else "leaf", "_2" if int(s["id"].split(".")[1]) % 2 == 1 else "")
    if k == "Animal":
        return "Animal_%s%s" % (s["ref"], "_ready" if s["phase"] == "Ready" else "")
    return None


def paint_street(p, rows):
    st = next((r for r in rows if r["kind"] == "Street"), None)
    if st is None:
        return
    x = -512
    while x < MAPW + 512:
        p.put(art("Farm/Grass"), x, -439, 513, 440)
        p.put(art("Farm/Street"), x, 0, 513, 440)
        x += 512
    x0, x1 = st["x"] - 420, st["y"] + 420
    rng = random.Random(20261112)
    real = [r for r in rows if r["kind"] not in ("Street", "Field", "Yard")]
    for i in range(70):
        x, y = x0 + rng.random() * (x1 - x0), 110 + rng.random() * 250
        name = rng.choice(["Bush1", "Bush2", "Bush3", "Tuft1", "Tuft2"])
        bush = name.startswith("Bush")
        if any(s["y"] < 460 and abs(x - s["x"]) < s["w"] / 2 + 200 and s["y"] - 70 < y < s["y"] + 340 for s in real):
            continue
        if bush:
            p.put(art("Farm/" + name), x - 60, y - 26, 150, 110)
        else:
            p.put(art("Farm/" + name), x - 30, y, 60, 44)


def paint_site(p, s, press=None, selected=False, squash=1.0, bob=0.0, show_bubble=True):
    """One site as FarmSitesView.Make and Show draw it. squash > 1: wider and lower (a pop)."""
    k, x, y, w, h = s["kind"], s["x"], s["y"], s["w"], s["h"]
    scale = .93 if press else 1.0
    sx, sy = scale * squash, scale * (2 - squash)
    locked, offer = s["phase"] == "Locked", s["phase"] == "Offer"
    if selected:
        rw = max(190, w * 1.25)
        p.put(art("Farm/Ring"), x - rw / 2, y + (h / 2 if k in ("Plot", "PlotOffer") else rw * .12) - rw * 140 / 220 / 2, rw, rw * 140 / 220)
    look = growth(s)
    if k == "Plot":
        p.put(art("Farm/Plot"), x - 85, y - 14, 170, 122, 1, sx, sy)
        if look:
            p.put(art("Farm/" + look), x - 85, y - 14, 170, 224, 1, sx, sy)
    elif k == "PlotOffer":
        p.put(art("Farm/PlotOffer"), x - 85, y - 14, 170, 122, .45 if locked else 1)
        p.fit(icon("plus"), x, y + h * .62, 56)
    elif k == "Tree":
        p.put(art("Farm/" + look), x - 95.2, y - 52.7, 255, 263.5, 1, sx, sy)
    elif k == "Animal":
        c = ANIMAL_CANVAS[s["ref"]]
        p.put(art("Farm/" + look), x - c[2], y - c[3] + bob, c[0], c[1], 1, sx, sy)
    elif k in ("Pen", "Nursery"):
        p.put(art("Farm/Sign"), x - 52.5, y - 9, 105, 120)
        ic = icon("apple") if k == "Nursery" else icon("animal." + {"coop": "chicken", "cowshed": "cow", "sheepfold": "sheep", "apiary": "bee", "pigsty": "pig"}.get(s["ref"], "chicken"))
        p.fit(ic, x - 1, y - 9 + 120 * .69, 46)
    elif k == "Workshop" and s["ref"] != "mill":
        p.put(art("Farm/Workshop_" + s["ref"]), x - 154, y - 22, 344, 320, .45 if locked else .7 if offer else 1, sx, sy)

    if s["phase"] == "Growing":
        p.bar(x, y + (-4 if k == "Plot" else -10), min(120, w * .7), s["progress"])
    ic, dim = None, False
    if k in ("Plot", "Tree", "Workshop") and s["phase"] == "Ready":
        ic = item_icon(s["item"])
    elif k == "Animal" and s["phase"] in ("Ready", "Hungry"):
        ic, dim = item_icon(s["item"]), s["phase"] == "Hungry" and not s["attention"]
    elif k == "Barn" and s["attention"]:
        ic = icon("tool.hammer")
    elif k == "Orders" and s["attention"]:
        ic = icon("orders")
    if ic is not None and show_bubble:
        p.bubble(x, y + bubble_y(s), ic, k in ("Plot", "Tree"), dim, 10 if s["attention"] else 0)
    if locked or offer:
        ty = y + (h * .3 if k == "PlotOffer" else 10 if k == "Workshop" else -22)
        p.tag(x, ty, "Ур. %d" % s["level"] if locked else ("Даром" if s["count"] <= 0 else str(s["count"])), "lock" if locked else "coin")


def paint_farm(p, rows, overrides=None, skip=()):
    """The whole working farm: the street, the yards, then every site, far ones first."""
    overrides = overrides or {}
    paint_street(p, rows)
    for r in sorted([r for r in rows if r["kind"] == "Yard"], key=lambda r: -r["y"]):
        if r["ref"] != "coop":
            p.put(art("Farm/Pen_" + r["ref"]), r["x"] - 201.5, r["y"] - 12, 500, 330, 1 if r["phase"] == "Idle" else .5)
    for s in sorted([r for r in rows if r["kind"] not in ("Street", "Field", "Yard")], key=lambda r: -r["y"]):
        if s["id"] in skip:
            continue
        paint_site(p, s, **overrides.get(s["id"], {}))
