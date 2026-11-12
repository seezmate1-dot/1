"""Mockups of the game's screens composed from the real skin, icon and board sprites.

The geometry is the game's own: mockups/layout-dump (a tiny console project over Game/Layout) writes the resolved
layouts of Screens.Level / Screens.Farm / Popups.Win at 1080×1920 as JSON, and this script draws every node the way
Game/UI/UiView.cs + UiSkin.cs do (same skin rules, same 9-slice borders, same tints). The board is drawn like
Board/BoardView.cs. Regenerate the JSON with:  cd mockups/layout-dump && dotnet run
"""
import json, os, random, math, sys
from PIL import Image, ImageDraw, ImageFont, ImageChops, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
RES = os.path.join(HERE, '..', '..', 'unity', 'SunnyValley', 'Assets', 'SunnyValley', 'Resources')
DUMP = os.path.join(HERE, 'mockups', 'layout-dump')
OUT = os.path.join(HERE, 'mockups')
FONT = os.path.join(RES, 'Fonts', 'Nunito-ExtraBold.ttf')
SLICES = json.load(open(os.path.join(HERE, 'UI', 'slices.json')))

TOK = dict(Wood=0xF6E3BCFF, WoodInset=0xE9CF9AFF, Plate=0x3E2A17C8, Yellow=0xFFC928FF, Green=0x6CC04AFF, Red=0xEE5A3CFF,
           Blue=0x47A8EEFF, TrackOff=0xC9B48EFF)
BUTTON_SKINS = {TOK['Yellow']: 'ButtonYellow', TOK['Green']: 'ButtonGreen', TOK['Blue']: 'ButtonBlue', TOK['Red']: 'ButtonRed', TOK['TrackOff']: 'ButtonGrey'}
ART = {'star.empty': 'Art/Currency/StarEmpty', 'coin': 'Art/Currency/Coin', 'star': 'Art/Currency/Star', 'crystal': 'Art/Currency/Crystal', 'heart': 'Art/Currency/Heart',
       'tomato': 'Art/Tiles/Tomato', 'corn': 'Art/Tiles/Corn', 'apple': 'Art/Tiles/Apple', 'carrot': 'Art/Tiles/Carrot',
       'eggplant': 'Art/Tiles/Eggplant', 'blueberry': 'Art/Tiles/Blueberry', 'pitchfork': 'Art/Specials/Pitchfork', 'bomb': 'Art/Specials/Bomb',
       'rainbow': 'Art/Specials/Rainbow', 'shovel': 'Art/Boosters/Shovel', 'rake': 'Art/Boosters/Rake', 'can': 'Art/Boosters/WateringCan',
       'shuffle': 'Art/Boosters/Shuffle', 'crate': 'Art/Obstacles/Crate1', 'ice': 'Art/Obstacles/Ice', 'chain': 'Art/Obstacles/Chain',
       'honey': 'Art/Obstacles/Honey', 'stone': 'Art/Obstacles/Stone', 'weed': 'Art/Obstacles/Weed', 'bed': 'Art/Obstacles/Bed',
       'basket': 'Art/Obstacles/Basket', 'chick': 'Art/Obstacles/Chick', 'bee': 'Art/Specials/Bee'}

_cache = {}
def res(path):
    if path not in _cache:
        p = os.path.join(RES, path)
        _cache[path] = Image.open(p + ('.png' if os.path.exists(p + '.png') else '.jpg')).convert('RGBA')
    return _cache[path]
def icon_img(name): return res(ART.get(name, 'Icons/' + name))
def rgba(c): return ((c >> 24) & 255, (c >> 16) & 255, (c >> 8) & 255, c & 255)

def tinted(im, col):
    """Unity's vertex colour: every channel multiplied."""
    if col == (255, 255, 255, 255): return im
    r, g, b, a = im.split()
    f = lambda ch, v: ch.point(lambda p: p * v // 255)
    return Image.merge('RGBA', (f(r, col[0]), f(g, col[1]), f(b, col[2]), f(a, col[3])))

class Canvas:
    def __init__(self, w, h, ss=1):
        self.w, self.h, self.k = w, h, ss
        self.im = Image.new('RGBA', (int(w * ss), int(h * ss)), (0, 0, 0, 255))
    def box(self, x, y, w, h):
        """Layout rect (origin lower left, Y up) -> pixel box."""
        k = self.k
        return [int(round(x * k)), int(round((self.h - y - h) * k)), int(round((x + w) * k)), int(round((self.h - y) * k))]
    def paste(self, im, x, y, w, h, col=(255, 255, 255, 255)):
        b = self.box(x, y, w, h)
        if b[2] <= b[0] or b[3] <= b[1]: return
        im = tinted(im.resize((b[2] - b[0], b[3] - b[1]), Image.LANCZOS), col)
        self.im.alpha_composite(im, (b[0], b[1])) if b[0] >= 0 and b[1] >= 0 else self.im.paste(im, (b[0], b[1]), im)
    def fit(self, im, x, y, w, h, col=(255, 255, 255, 255), rot=0):
        """preserveAspect."""
        if rot: im = im.rotate(rot, Image.BICUBIC)
        a = im.width / im.height
        if w / h > a: nw, nh = h * a, h
        else: nw, nh = w, w / a
        self.paste(im, x + (w - nw) / 2, y + (h - nh) / 2, nw, nh, col)
    def sliced(self, name, x, y, w, h, radius, col=(255, 255, 255, 255)):
        """Image.Type.Sliced with pixelsPerUnitMultiplier = 64 / radius (borders shrink to fit, like Unity)."""
        im = res('Art/UI/' + name)
        L, B, R, T = SLICES[name]
        m = 64 / max(1, radius)
        l, b, r, t = L / m, B / m, R / m, T / m
        if l + r > w: s = w / (l + r); l *= s; r *= s
        if t + b > h: s = h / (t + b); t *= s; b *= s
        xs_s, ys_s = [0, L, im.width - R, im.width], [0, T, im.height - B, im.height]
        xs_d, ys_d = [x, x + l, x + w - r, x + w], [y + h, y + h - t, y + b, y]
        for i in range(3):
            for j in range(3):
                if xs_s[i + 1] <= xs_s[i] or ys_s[j + 1] <= ys_s[j]: continue
                part = im.crop((xs_s[i], ys_s[j], xs_s[i + 1], ys_s[j + 1]))
                self.paste(part, xs_d[i], ys_d[j + 1], xs_d[i + 1] - xs_d[i], ys_d[j] - ys_d[j + 1], col)
    def round_rect(self, x, y, w, h, radius, col):
        b = self.box(x, y, w, h)
        layer = Image.new('RGBA', self.im.size, (0, 0, 0, 0))
        ImageDraw.Draw(layer).rounded_rectangle(b, radius * self.k, fill=col)
        self.im.alpha_composite(layer)
    def text(self, x, y, w, h, lines, size, col, align='Center', stroke=None):
        """stroke: the colour of a thick outline (Unity Outline: four diagonal copies) with a drop shadow under it."""
        f = ImageFont.truetype(FONT, int(round(size * self.k)))
        lh = size * 1.364 * self.k
        b = self.box(x, y, w, h)
        cy = (b[1] + b[3]) / 2 - lh * (len(lines) - 1) / 2
        d = ImageDraw.Draw(self.im)
        for i, line in enumerate(lines):
            if align == 'Left': px, anc = b[0], 'lm'
            elif align == 'Right': px, anc = b[2], 'rm'
            else: px, anc = (b[0] + b[2]) / 2, 'mm'
            py = cy + i * lh
            if stroke:
                o = min(4.5, max(2.5, size * .065)) * self.k
                layer = Image.new('RGBA', self.im.size, (0, 0, 0, 0)); ld = ImageDraw.Draw(layer)
                for dx, dy in ((o, o), (o, -o), (-o, o), (-o, -o)):
                    ld.text((px + dx, py + dy + o * 1.3), line, font=f, fill=stroke[:3] + (170,), anchor=anc)
                ld.text((px, py + o * 1.3), line, font=f, fill=stroke[:3] + (170,), anchor=anc)
                self.im.alpha_composite(layer)
                layer = Image.new('RGBA', self.im.size, (0, 0, 0, 0)); ld = ImageDraw.Draw(layer)
                for dx, dy in ((o, o), (o, -o), (-o, o), (-o, -o)):
                    ld.text((px + dx, py + dy), line, font=f, fill=stroke, anchor=anc)
                self.im.alpha_composite(layer)
            d.text((px, py), line, font=f, fill=col, anchor=anc)

# ---------------------------------------------------------------- UiView + UiSkin, in Python
def draw_shape(c, n):
    x, y, w, h, col, depth, rad = n['x'], n['y'], n['w'], n['h'], rgba(n['color']), n['depth'], n['radius']
    white = (255, 255, 255, 255)
    if n['shape'] == 'Circle':
        s = min(w, h); cx, cy = x + w / 2, y + h / 2
        if 'booster' in n['id'] and n['id'].endswith('.disc'):
            c.paste(res('Art/UI/BoosterSlot'), cx - s / 2, cy - s / 2, s, s)
        elif depth > 0:
            lip = s * 16 / 256
            c.paste(res('Art/UI/RoundTint'), cx - s / 2, cy - s / 2 - lip, s, s + lip, col)
            c.paste(res('Art/UI/RoundGloss'), cx - s / 2, cy - s / 2 - lip, s, s + lip)
        else:
            c.paste(res('Art/UI/DotTint'), cx - s / 2, cy - s / 2, s, s, col)
            c.paste(res('Art/UI/DotGloss'), cx - s / 2, cy - s / 2, s, s)
        return
    if n['kind'] == 'Bar':
        c.sliced('BarTrack', x, y, w, h, rad)
        fw = max(min(1, h / w), min(1, max(0, n['value']))) * w
        c.sliced('BarFill', x, y, fw, h, rad, rgba(n['depthColor']))
        c.sliced('BarGloss', x, y, fw, h, rad)
    elif n['color'] == TOK['Plate']:
        c.sliced('Plate', x, y, w, h, rad)
    elif n['color'] == TOK['Wood'] and depth > 0:
        drop = depth + 10 * min(rad, h / 2) / 64
        c.sliced('PanelDark' if n['id'].endswith('.moves') else 'Panel', x, y - drop, w, h + drop, rad)
    elif depth > 0:
        drop = depth + 10 * min(rad, h / 2) / 64
        skin = BUTTON_SKINS.get(n['color']) if n['kind'] == 'Button' else None
        if skin:
            c.sliced(skin, x, y - drop, w, h + drop, rad)
        else:
            c.sliced('ButtonTint', x, y - drop, w, h + drop, rad, col)
            c.sliced('ButtonGloss', x, y - drop, w, h + drop, rad)
    elif col[3] == 255:
        c.sliced('Inset', x, y, w, h, rad, col)
    else:
        c.round_rect(x, y, w, h, rad, col)

def icon_boost(n, p):
    """UiSkin.IconBoost: (scale, dx, dy)."""
    if p is None: return 1, 0, 0
    h = n['h']
    if p['shape'] == 'Circle' and p['id'].endswith('.disc'):
        return (1.5, 0, h * .14) if 'booster' in p['id'] else (1.36, 0, h * .05)
    if p['shape'] == 'Circle' and p['kind'] == 'Button': return 1.14, 0, 0
    if p['shape'] == 'RoundRect' and p['color'] == TOK['Plate'] and p['id'].endswith('.bg'): return 1.34, -h * .08, 0
    if '.goal' in n['id'] and n['id'].endswith('.icon'): return 1.16, 0, -h * .03
    return 1, 0, 0

STROKES = {TOK['Yellow']: (150, 78, 0, 255), TOK['Green']: (28, 88, 16, 255), TOK['Blue']: (16, 58, 110, 255), TOK['Red']: (110, 20, 8, 255)}
def is_ribbon_title(n): return n['kind'] == 'Label' and n['id'].endswith('.panel.title') and n['w'] >= 192
def is_goal_count(n, p): return n['kind'] == 'Label' and n['id'].endswith('.count') and p is not None and '.goal' in p['id']

def text_look(n, chain):
    """UiSkin.TextFor: (colour, stroke colour or None)."""
    t = n['text']; p = chain[-1] if chain else None
    if is_ribbon_title(n): return (255, 255, 255, 255), (16, 58, 110, 255)
    if is_goal_count(n, p): return (255, 255, 255, 255), None
    if p is not None and p['id'].endswith('.moves'):
        return ((255, 255, 255, 255), (62, 34, 10, 255)) if n['id'].endswith('.value') else ((255, 222, 160, 255), None)
    if not t['outline']: return rgba(t['color']), None
    for q in reversed(chain):
        if q['shape'] == 'None': continue
        if q['kind'] == 'Button' and q['color'] in STROKES: return rgba(t['color']), STROKES[q['color']]
        break
    return rgba(t['color']), (62, 34, 10, 255)

def draw(c, n, world=None, board=None, chain=(), off=(0, 0, 1)):
    """off: what an enlarged ancestor icon does to its children (the count on the heart)."""
    if n['hidden']: return
    k = n['kind']
    p = chain[-1] if chain else None
    x, y, w, h = n['x'], n['y'], n['w'], n['h']
    if k == 'Dim':
        c.round_rect(x, y, w, h, 0, rgba(n['color']))
    elif k == 'Icon':
        s, dx, dy = icon_boost(n, p)
        gx, gy = w * (s - 1) / 2, h * (s - 1) / 2
        x, y, w, h = x - gx + dx, y - gy + dy, w * s, h * s
        if n['icon'] == 'star' and n['w'] >= 150:
            c.paste(res('Art/UI/Rays'), x - w * .34, y - h * .34, w * 1.68, h * 1.68)
        c.fit(icon_img(n['icon']), x, y, w, h)
    elif k == 'Label':
        t = n['text']
        if is_ribbon_title(n):
            cut = (h - 112) / 2
            c.sliced('Ribbon', x - 8, y + cut - 4, w + 16, 112, 64)
        elif is_goal_count(n, p):
            side = max(0, (w - 100) / 2)
            c.sliced('Plate', x + side, y + 1, w - 2 * side, h - 2, h / 2)
        col, stroke = text_look(n, list(chain))
        c.text(x, y, w, h, t['lines'], t['size'], col, t['align'], stroke)
    elif k == 'World':
        if world: world(c, n)
    elif k == 'Board':
        if board: board(c, n)
    elif n['shape'] != 'None':
        draw_shape(c, n)
        if n['kind'] == 'Button' and n['color'] == TOK['Yellow'] and h >= 175 and SHINE:
            # the sweeping light, caught mid-way
            sh = res('Art/UI/Shine')
            layer = Canvas(c.w, c.h); layer.im = Image.new('RGBA', c.im.size, (0, 0, 0, 0))
            layer.paste(sh, x + w * .62, y + 8, h, h - 12)
            mask = Canvas(c.w, c.h); mask.im = Image.new('RGBA', c.im.size, (0, 0, 0, 0))
            mask.round_rect(x + 9, y + 6, w - 18, h - 14, n['radius'] - 6, (255, 255, 255, 255))
            layer.im.putalpha(ImageChops.multiply(layer.im.getchannel('A'), mask.im.getchannel('A')))
            c.im.alpha_composite(layer.im)
    elif k == 'Panel':
        c.round_rect(x, y, w, h, 0, rgba(n['color'] | 0xFF))
        if n['icon']:
            bg = res('Art/' + n['icon'])
            s = max(w / bg.width, h / bg.height)
            bw, bh = bg.width * s, bg.height * s
            c.paste(bg, x + (w - bw) / 2, y + (h - bh) / 2, bw, bh, rgba(n['depthColor']))
    for ch in n['children']:
        draw(c, ch, world, board, tuple(chain) + (n,))

SHINE = True

# ---------------------------------------------------------------- the board (BoardView.Show)
TILES = ['tomato', 'corn', 'apple', 'carrot', 'eggplant', 'blueberry']
CROP = [(237, 64, 46), (255, 209, 51), (115, 209, 64), (255, 140, 38), (153, 77, 191), (77, 115, 242)]
SEAM = (0xC4, 0xBC, 0xAD, 255)
FRAME_OUT, FRAME_IN = (0x4A, 0x26, 0x08, 255), (0xA0, 0x6A, 0x34, 255)

def make_grid(cols, rows, seed=7):
    rnd = random.Random(seed)
    g = [[0] * cols for _ in range(rows)]
    for r in range(rows):
        for q in range(cols):
            while True:
                v = rnd.randrange(6)
                if q >= 2 and g[r][q - 1] == v and g[r][q - 2] == v: continue
                if r >= 2 and g[r - 1][q] == v and g[r - 2][q] == v: continue
                g[r][q] = v; break
    return g

def draw_special(c, kind, cx, cy, cell, t=0.0, rot=0, sx=1.0, sy=1.0, alpha=255):
    """A special piece in its idle pose at time t (PieceVisual.Idle): no light behind it, the piece itself moves."""
    s = cell * .92
    col = (255, 255, 255, alpha)
    def put(im, x, y, w, h, r=0, tint=col):
        if r: im = im.rotate(r, Image.BICUBIC)
        c.paste(im, x - w / 2, y - h / 2, w, h, tint)
    if kind == 'bee':
        bob = cell * .035 * math.sin(t * 4.2)
        flap = .55 + .45 * abs(math.sin(t * 34))
        root = cy + bob + (.42 - .5) * s
        wings = res('Art/Fx/BeeWings')
        c.paste(wings, cx - s * sx / 2, root - .42 * s * flap, s * sx, s * flap, col)       # folds about the wing roots
        put(res('Art/Fx/BeeBody'), cx, cy + bob, s * sx, s * sy, rot)
    elif kind == 'bomb':
        cyc = (t * 1.1) % 1
        q = .06 * (math.sin(cyc / .18 * math.pi) if cyc < .36 else 0)
        w, h = s * sx * (1 + q), s * sy * (1 - q)
        put(icon_img('bomb'), cx, cy - (s - h) / 2 * 0, w, h)
        f = .7 + .45 * abs(math.sin(t * 17)) * (.65 + .35 * math.sin(t * 7.3))
        ps = s * .42 * f
        put(res('Art/Fx/Sparkle'), cx + .175 * w, cy + .31 * h, ps, ps, t * 260, (255, 235, 140, alpha))
    elif kind.startswith('line'):
        base = -90 if kind == 'lineH' else 0
        cyc = t % 3.4
        wig = math.sin(cyc / .5 * math.pi * 3) * (1 - cyc / .5) * 9 if cyc < .5 else 0
        put(icon_img('pitchfork'), cx, cy, s, s, base + wig)
        g = min(1, max(0, (cyc - .45) / .6))
        if 0 < g < 1:
            lx, ly = (-.2 + .4 * g) * s, .2 * s
            if kind == 'lineH': lx, ly = ly, -lx
            ps = s * .4
            put(res('Art/Fx/Sparkle'), cx + lx, cy + ly, ps, ps, g * 120, (255, 255, 255, int(alpha * math.sin(g * math.pi))))
    elif kind == 'rainbow':
        k = 1 + .05 * math.sin(t * 2.6)
        put(icon_img('rainbow'), cx, cy, s * k * sx, s * k * sy, -(t * 30) + rot)

def board(specials, obstacles, grass=None, holes=()):
    grass = grass or {}
    def f(c, n):
        cols, rows, cell = n['cols'], n['rows'], n['cell']
        x0, y0 = n['x'], n['y']
        at = lambda q, r: (x0 + (q + .5) * cell, y0 + (rows - r - .5) * cell)
        pad = max(3, cell * .07); o = max(2, cell * .035)
        cells = [(q, r) for r in range(rows) for q in range(cols) if (q, r) not in holes]
        for grow, col, rad in ((pad + o, FRAME_OUT, cell * .22 + o), (pad, FRAME_IN, cell * .22)):
            for q, r in cells:
                cx, cy = at(q, r); s = cell + 2 * grow
                c.round_rect(cx - s / 2, cy - s / 2, s, s, rad, col)
        for q, r in cells:
            cx, cy = at(q, r); s = cell + 2
            c.round_rect(cx - s / 2, cy - s / 2, s, s, cell * .22 - pad + 1, SEAM)
        for q, r in cells:
            cx, cy = at(q, r); s = cell - 2
            c.paste(res('Art/UI/CellLight' if (q + r) % 2 == 0 else 'Art/UI/CellDark'), cx - s / 2, cy - s / 2, s, s)
        for (q, r), layers in grass.items():                      # the breakable plates under the pieces
            cx, cy = at(q, r); s = cell * .98
            c.paste(res('Art/Obstacles/Grass%d' % layers), cx - s / 2, cy - s / 2, s, s)
        g = make_grid(cols, rows)
        for i, (q, r) in enumerate(cells):
            cx, cy = at(q, r)
            ob = obstacles.get((q, r))
            if ob in ('crate', 'stone', 'basket', 'Crate2', 'Crate3'):
                im = res('Art/Obstacles/' + ob) if ob[0] == 'C' else icon_img(ob)
                s = cell * .96; c.fit(im, cx - s / 2, cy - s / 2, s, s); continue
            sp = specials.get((q, r))
            if sp:
                draw_special(c, sp[0], cx, cy, cell, t=.37 + i * 1.713)
            else:
                s = cell * .86
                c.fit(icon_img(TILES[g[r][q]]), cx - s / 2, cy - s / 2, s, s)
            if ob in ('ice', 'chain', 'honey'):
                s = cell * .98; c.fit(icon_img(ob), cx - s / 2, cy - s / 2, s, s, (255, 255, 255, 235 if ob == 'ice' else 255))
    return f

def world(name):
    def f(c, n):
        bg = res('Art/Ch1/Backgrounds/' + name)
        s = max(n['w'] / bg.width, n['h'] / bg.height)
        bw, bh = bg.width * s, bg.height * s
        c.paste(bg, n['x'] + (n['w'] - bw) / 2, n['y'] + (n['h'] - bh) / 2, bw, bh)
    return f

def load(name): return json.load(open(os.path.join(DUMP, name + '.json')))

SPECIALS = {(2, 2): ('lineH', 0), (5, 3): ('bomb', 5), (3, 5): ('rainbow', 0), (6, 6): ('bee', 1), (1, 6): ('lineV', 3)}
OBST = {(0, 0): 'crate', (1, 0): 'crate', (6, 0): 'Crate2', (7, 0): 'Crate3', (7, 3): 'ice', (7, 4): 'ice', (0, 4): 'chain', (4, 7): 'stone', (3, 7): 'stone', (4, 1): 'honey'}

GRASS = {(1, 1): 1, (2, 1): 1, (3, 1): 1, (1, 2): 1, (5, 4): 2, (6, 4): 2, (5, 5): 2, (6, 5): 2, (4, 5): 1, (4, 4): 1, (2, 6): 2, (3, 6): 1}

def render(layout, path, under=None, **kw):
    L = load(layout)
    c = Canvas(L['W'], L['H'])
    if under:
        U = load(under[0]); draw(c, U['root'], **under[1])
    draw(c, L['root'], **kw)
    c.im.convert('RGB').save(os.path.join(OUT, path)); print(path)
    return c

def specials_sheet():
    """A filmstrip: how each special piece lives on the board (idle frames) and fires (activation frames), plus a
    turf plate breaking. The frames follow PieceVisual.Idle and the cues of Board/BoardView.Cues.cs."""
    cell = 132
    N = 7
    W = 1080
    x0 = (W - N * cell) / 2
    rows = [('Бомба: тикает (сжатие-растяжение), фитиль искрит', 'bomb_idle'), ('Бомба: пружинит, взрыв, волна, осколки', 'bomb_fire'),
            ('Вилы: вздрагивают, по зубьям бежит блик', 'fork_idle'), ('Вилы: летят по ряду со следом, фишки лопаются', 'fork_fire'),
            ('Радуга: медленно вращается и дышит', 'rainbow_idle'), ('Радуга: лепестки разлетаются, искры летят к целям', 'rainbow_fire'),
            ('Пчела: машет крыльями и покачивается', 'bee_idle'), ('Пчела: летит по дуге с пыльцой, кувырок на цели', 'bee_fire'),
            ('Новая суперфишка: выскакивает (0 - 1,2 - 1) с искрами', 'appear'), ('Травяная плитка: 2 слоя, 1 слой, разбита', 'turf')]
    pitch = cell + 84
    H = int(len(rows) * pitch + 40)
    c = Canvas(W, H)
    c.round_rect(0, 0, W, H, 0, (0x55, 0x8E, 0x3A, 255))
    spark, streak, pollen = res('Art/Fx/Sparkle'), res('Art/Fx/Streak'), res('Art/Fx/Pollen')
    def put(im, x, y, s, col=(255, 255, 255, 255), rot=0, sx=1.0, sy=1.0):
        if rot: im = im.rotate(rot, Image.BICUBIC)
        c.paste(im, x - s * sx / 2, y - s * sy / 2, s * sx, s * sy, col)
    def tile(name, x, y, k=1.0, a=255, sx=1.0, sy=1.0): put(icon_img(name), x, y, cell * .86 * k, (255, 255, 255, a), 0, sx, sy)
    def burst(x, y, col, r, n=7, a=255):
        for i in range(n):
            ang = (i + .37 * (i % 3)) / n * math.pi * 2
            px, py = x + r * math.cos(ang), y + r * math.sin(ang) + r * .15
            (put(spark, px, py, cell * .3, col[:3] + (a,)) if i % 2 == 0 else c.round_rect(px - 7, py - 7, 14, 14, 7, col[:3] + (a,)))
    def ring(x, y, r, a, col=(255, 235, 170)):
        layer = Image.new('RGBA', c.im.size, (0, 0, 0, 0)); d = ImageDraw.Draw(layer)
        cy = c.h - y
        d.ellipse([x - r, cy - r, x + r, cy + r], outline=col + (a,), width=int(max(3, r * .09)))
        c.im.alpha_composite(layer.filter(ImageFilter.GaussianBlur(2)))
    for ri, (title, kind) in enumerate(rows):
        y = H - 40 - ri * pitch - 58 - cell
        for grow, col, rad in ((cell * .105, FRAME_OUT, cell * .255), (cell * .07, FRAME_IN, cell * .22)):
            for q in range(N):
                s = cell + 2 * grow; c.round_rect(x0 + q * cell - grow, y - grow, s, s, rad, col)
        c.round_rect(x0 - 1, y - 1, N * cell + 2, cell + 2, cell * .16, SEAM)
        for q in range(N):
            c.paste(res('Art/UI/CellLight' if q % 2 == 0 else 'Art/UI/CellDark'), x0 + q * cell + 1, y + 1, cell - 2, cell - 2)
        X = lambda q: x0 + (q + .5) * cell
        Y = y + cell / 2
        if kind == 'bomb_idle':
            for q in range(N): draw_special(c, 'bomb', X(q), Y, cell, t=q * .075 + .02)
        elif kind == 'fork_idle':
            for q in range(N): draw_special(c, 'lineV', X(q), Y, cell, t=(.08, .2, .34, .56, .72, .88, 1.5)[q])
        elif kind == 'rainbow_idle':
            for q in range(N): draw_special(c, 'rainbow', X(q), Y, cell, t=q * .55)
        elif kind == 'bee_idle':
            for q in range(N): draw_special(c, 'bee', X(q), Y, cell, t=q * .0231 + (q % 2) * 1.5)
        elif kind == 'bomb_fire':
            draw_special(c, 'bomb', X(0), Y, cell, t=.6)
            draw_special(c, 'bomb', X(1), Y - cell * .1, cell, t=.6, sx=1.22, sy=.74)
            put(res('Art/Fx/Burst'), X(2), Y, cell * 1.0); ring(X(2), Y, cell * .42, 255)
            put(res('Art/Fx/Burst'), X(3), Y, cell * 1.25, (255, 255, 255, 200)); ring(X(3), Y, cell * .6, 220); burst(X(3), Y, (255, 150, 40), cell * .5, 8)
            ring(X(4), Y, cell * .62, 130); burst(X(4), Y - cell * .05, (255, 150, 40), cell * .5, 8, 220); burst(X(4), Y - cell * .12, (92, 56, 26), cell * .36, 6, 255)
            burst(X(5), Y - cell * .22, (255, 150, 40), cell * .42, 8, 120); burst(X(5), Y - cell * .3, (92, 56, 26), cell * .3, 6, 150)
        elif kind == 'fork_fire':
            names = ['tomato', 'corn', 'apple', 'carrot', 'eggplant', 'blueberry', 'tomato']
            for q in (0, 1, 2): tile(names[q], X(q), Y, 1.0)
            c.paste(streak, X(2.6), Y - cell * .31, X(5.2) - X(2.6), cell * .62, (255, 205, 170, 240))
            tile(names[3], X(3), Y, 1.18, 150, 1.06, .96); burst(X(3), Y, CROP[3], cell * .34, 7, 230)
            tile(names[4], X(4), Y, 1.1, 235, 1.2, .9)
            put(icon_img('pitchfork'), X(5.2), Y, cell * .85, rot=-90)
            tile(names[6], X(6), Y)
            burst(X(2), Y, CROP[2], cell * .44, 7, 120) if False else None
        elif kind == 'rainbow_fire':
            for q in (0, 6): tile('blueberry', X(q), Y)
            tile('corn', X(1), Y); tile('apple', X(5), Y)
            for q, a in ((0, 235), (6, 235)):
                xa, xb = sorted((X(3), X(q)))
                c.paste(streak, xa, Y - cell * .16, xb - xa, cell * .32, (170, 190, 255, a))
            put(res('Art/Fx/RainbowRing'), X(3), Y, cell * 1.5, (255, 255, 255, 230))
            for i in range(10):
                ang = (i + .5) / 10 * math.pi * 2
                put(res('Art/Fx/Petal'), X(3) + cell * .62 * math.cos(ang), Y + cell * .62 * math.sin(ang) * .9, cell * .34, CROP[i % 6] + (255,), math.degrees(ang) - 90)
            draw_special(c, 'rainbow', X(3), Y, cell, t=1.3, sx=1.15, sy=1.15)
            put(spark, X(1.6), Y + cell * .12, cell * .5, (150, 170, 255, 255), 30); put(spark, X(4.5), Y + cell * .12, cell * .5, (150, 170, 255, 255), 30)
            burst(X(0), Y, CROP[5], cell * .3, 5, 255); burst(X(6), Y, CROP[5], cell * .3, 5, 255)
        elif kind == 'bee_fire':
            tile('apple', X(0), Y, 1, 110)
            put(icon_img('crate'), X(6), Y, cell * .96)
            for i in range(1, 9):
                t = i / 10
                px = X(.3) + (X(4.6) - X(.3)) * t; py = Y + cell * .46 * 4 * t * (1 - t) - 4
                put(pollen, px, py, cell * (.14 + .2 * t), (255, 255, 255, int(60 + 190 * t)))
            t = .86; bx = X(.3) + (X(4.6) - X(.3)) * t; by = Y + cell * .46 * 4 * t * (1 - t)
            c.paste(res('Art/Fx/BeeWings').rotate(-22, Image.BICUBIC), bx - cell * .46, by - cell * .46, cell * .92, cell * .92)
            c.paste(res('Art/Fx/BeeBody').rotate(-22, Image.BICUBIC), bx - cell * .46, by - cell * .46, cell * .92, cell * .92)
            put(icon_img('bee'), X(6) - cell * .1, Y + cell * .1, cell * .6, (255, 255, 255, 200), 140)
            burst(X(6), Y, (255, 215, 60), cell * .46, 6, 255)
        elif kind == 'appear':
            for q, (k, a) in enumerate(((0, 0), (.45, 255), (.95, 255), (1.2, 255), (1.08, 255), (1.0, 255), (1.0, 255))):
                if k: draw_special(c, 'bomb', X(q), Y, cell, t=.6, sx=k, sy=k)
            ring(X(1), Y, cell * .25, 230); ring(X(2), Y, cell * .42, 180); ring(X(3), Y, cell * .58, 90)
            burst(X(2), Y, (255, 220, 110), cell * .4, 8, 255); burst(X(3), Y - cell * .06, (255, 220, 110), cell * .56, 8, 170); burst(X(4), Y - cell * .16, (255, 220, 110), cell * .6, 8, 70)
        elif kind == 'turf':
            g1, g2, turf = res('Art/Obstacles/Grass1'), res('Art/Obstacles/Grass2'), res('Art/Fx/Turf')
            s = cell * .98
            def plate(im, q, k=1.0): c.paste(im, X(q) - s * k / 2, Y - s * k / 2, s * k, s * k)
            def clods(q, r, a):
                for i in range(7):
                    ang = (i + .37 * (i % 3)) / 7 * math.pi * 2
                    put(turf, X(q) + r * math.cos(ang), Y + r * math.sin(ang) + r * .2, cell * (.3 + .08 * (i % 3)), (255, 255, 255, a), i * 50)
            plate(g2, 0); tile('tomato', X(0), Y)
            plate(g2, 1, 1.08); tile('tomato', X(1), Y, 1.15, 170, 1.1, .95)
            plate(g1, 2); clods(2, cell * .42, 255)
            plate(g1, 3); tile('carrot', X(3), Y)
            plate(g1, 4, 1.08); tile('carrot', X(4), Y, 1.15, 170, 1.1, .95)
            clods(5, cell * .4, 255); ring(X(5), Y, cell * .4, 120, (150, 110, 70))
            clods(6, cell * .52, 110)
        c.text(x0 - 6, y + cell + 16, 1000, 46, [title], 31, (255, 255, 255, 255), 'Left', (40, 70, 24, 255))
    c.im.convert('RGB').save(os.path.join(OUT, 'specials_fx.png')); print('specials_fx.png')

def before_after(before_dir):
    """The level and farm screens: the previous committed look (left of each pair) against the current one."""
    names = ['level.png', 'farm.png']
    k = .5
    w, h = int(1080 * k), int(1920 * k)
    sheet = Image.new('RGB', (4 * w + 5 * 24, h + 110), (38, 30, 24))
    d = ImageDraw.Draw(sheet); f = ImageFont.truetype(FONT, 40)
    for i, n in enumerate(names):
        for j, (folder, lab) in enumerate(((before_dir, 'было'), (OUT, 'стало'))):
            x = 24 + (i * 2 + j) * (w + 24)
            sheet.paste(Image.open(os.path.join(folder, n)).convert('RGB').resize((w, h), Image.LANCZOS), (x, 86))
            d.text((x + w / 2, 44), lab, font=f, fill=(255, 226, 170) if j else (190, 170, 150), anchor='mm')
    sheet.save(os.path.join(OUT, 'before_after.png')); print('before_after.png')

if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    lvl = dict(board=board(SPECIALS, OBST, GRASS))
    render('level', 'level.png', **lvl)
    render('farm', 'farm.png', world=world('Yard'))
    render('farm2', 'farm_economy.png', world=world('Garden'))
    render('win', 'popup_win.png', under=('level', lvl))
    render('start', 'popup_start.png', under=('farm', dict(world=world('Yard'))))
    render('settings', 'popup_settings.png', under=('farm', dict(world=world('Yard'))))
    specials_sheet()
    if len(sys.argv) > 1: before_after(sys.argv[1])
