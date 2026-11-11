"""Mockups of the game's screens composed from the real skin, icon and board sprites.

The geometry is the game's own: mockups/layout-dump (a tiny console project over Game/Layout) writes the resolved
layouts of Screens.Level / Screens.Farm / Popups.Win at 1080×1920 as JSON, and this script draws every node the way
Game/UI/UiView.cs + UiSkin.cs do (same skin rules, same 9-slice borders, same tints). The board is drawn like
Board/BoardView.cs. Regenerate the JSON with:  cd mockups/layout-dump && dotnet run
"""
import json, os, random, math
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
ART = {'coin': 'Art/Currency/Coin', 'star': 'Art/Currency/Star', 'crystal': 'Art/Currency/Crystal', 'heart': 'Art/Currency/Heart',
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
    def text(self, x, y, w, h, lines, size, col, align='Center', outline=False):
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
            if outline:
                layer = Image.new('RGBA', self.im.size, (0, 0, 0, 0)); ld = ImageDraw.Draw(layer)
                o = 2.5 * self.k
                for dx, dy in ((o, o), (o, -o), (-o, o), (-o, -o)):
                    ld.text((px + dx, py + dy), line, font=f, fill=(70, 40, 12, 200), anchor=anc)
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
        c.sliced('Panel', x, y - depth, w, h + depth, rad)
    elif depth > 0:
        skin = BUTTON_SKINS.get(n['color']) if n['kind'] == 'Button' else None
        if skin:
            c.sliced(skin, x, y - depth, w, h + depth, rad)
        else:
            c.sliced('ButtonTint', x, y - depth, w, h + depth, rad, col)
            c.sliced('ButtonGloss', x, y - depth, w, h + depth, rad)
    elif col[3] == 255:
        c.sliced('Inset', x, y, w, h, rad, col)
    else:
        c.round_rect(x, y, w, h, rad, col)

def draw(c, n, world=None, board=None):
    if n['hidden']: return
    k = n['kind']
    if k == 'Dim':
        c.round_rect(n['x'], n['y'], n['w'], n['h'], 0, rgba(n['color']))
    elif k == 'Icon':
        c.fit(icon_img(n['icon']), n['x'], n['y'], n['w'], n['h'])
    elif k == 'Label':
        t = n['text']
        c.text(n['x'], n['y'], n['w'], n['h'], t['lines'], t['size'], rgba(t['color']), t['align'], t['outline'])
    elif k == 'World':
        if world: world(c, n)
    elif k == 'Board':
        if board: board(c, n)
    elif n['shape'] != 'None':
        draw_shape(c, n)
    elif k == 'Panel':
        c.round_rect(n['x'], n['y'], n['w'], n['h'], 0, rgba(n['color'] | 0xFF))
        if n['icon']:
            bg = res('Art/' + n['icon'])
            s = max(n['w'] / bg.width, n['h'] / bg.height)
            bw, bh = bg.width * s, bg.height * s
            c.paste(bg, n['x'] + (n['w'] - bw) / 2, n['y'] + (n['h'] - bh) / 2, bw, bh, rgba(n['depthColor']))
    for ch in n['children']:
        draw(c, ch, world, board)

# ---------------------------------------------------------------- the board (BoardView.Show)
TILES = ['tomato', 'corn', 'apple', 'carrot', 'eggplant', 'blueberry']
CROP = [(237, 64, 46), (255, 209, 51), (115, 209, 64), (255, 140, 38), (153, 77, 191), (77, 115, 242)]
RAINBOW_AURA = (217, 128, 255)
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

def board(specials, obstacles, fx=None, holes=()):
    def f(c, n):
        cols, rows, cell = n['cols'], n['rows'], n['cell']
        x0, y0 = n['x'], n['y']
        at = lambda q, r: (x0 + (q + .5) * cell, y0 + (rows - r - .5) * cell)
        pad = max(3, cell * .07); o = max(3, cell * .035)
        cells = [(q, r) for r in range(rows) for q in range(cols) if (q, r) not in holes]
        for grow, col, rad in ((pad + o, FRAME_OUT, cell * .22 + o), (pad, FRAME_IN, cell * .22)):
            for q, r in cells:
                cx, cy = at(q, r); s = cell + 2 * grow
                c.round_rect(cx - s / 2, cy - s / 2, s, s, rad, col)
        for q, r in cells:
            cx, cy = at(q, r); s = cell - 2
            c.paste(res('Art/UI/CellLight' if (q + r) % 2 == 0 else 'Art/UI/CellDark'), cx - s / 2, cy - s / 2, s, s)
        g = make_grid(cols, rows)
        aura = res('Art/Fx/Aura')
        for q, r in cells:
            cx, cy = at(q, r)
            ob = obstacles.get((q, r))
            if ob in ('crate', 'stone', 'basket', 'Crate2', 'Crate3'):
                im = res('Art/Obstacles/' + ob) if ob[0] == 'C' else icon_img(ob)
                s = cell * .96; c.fit(im, cx - s / 2, cy - s / 2, s, s); continue
            if ob == 'bed':
                s = cell * .96; c.fit(icon_img('bed'), cx - s / 2, cy - s / 2, s, s)
            sp = specials.get((q, r))
            if sp:
                kind, color = sp
                colr = RAINBOW_AURA if kind == 'rainbow' else CROP[color]
                s = cell * 1.25
                c.paste(aura, cx - s / 2, cy - s / 2, s, s, colr + (230,))
                s = cell * .8
                im = icon_img('pitchfork' if kind.startswith('line') else kind)
                c.fit(im, cx - s / 2, cy - s / 2, s, s, rot=-90 if kind == 'lineH' else 0)
            else:
                s = cell * .86
                c.fit(icon_img(TILES[g[r][q]]), cx - s / 2, cy - s / 2, s, s)
            if ob in ('ice', 'chain', 'honey'):
                s = cell * .98; c.fit(icon_img(ob), cx - s / 2, cy - s / 2, s, s, (255, 255, 255, 235 if ob == 'ice' else 255))
        if fx: fx(c, n, at, cell)
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

def render(layout, path, under=None, **kw):
    L = load(layout)
    c = Canvas(L['W'], L['H'])
    if under:
        U = load(under[0]); draw(c, U['root'], **under[1])
    draw(c, L['root'], **kw)
    c.im.convert('RGB').save(os.path.join(OUT, path)); print(path)
    return c

def specials_sheet():
    """The super pieces and their effects, one frame each (what Board/BoardView.Cues.cs spawns)."""
    cell = 150
    W, H = 1080, 1500
    c = Canvas(W, H)
    c.round_rect(0, 0, W, H, 0, (0x5E, 0x9A, 0x3E, 255))
    def strip(y, n=6, x=90):
        for grow, col, rad in ((cell * .105, FRAME_OUT, cell * .255), (cell * .07, FRAME_IN, cell * .22)):
            for q in range(n):
                s = cell + 2 * grow; c.round_rect(x + q * cell - grow, y - grow, s, s, rad, col)
        for q in range(n):
            c.paste(res('Art/UI/CellLight' if q % 2 == 0 else 'Art/UI/CellDark'), x + q * cell + 1, y + 1, cell - 2, cell - 2)
        return lambda q: (x + (q + .5) * cell, y + cell / 2)
    def put(im, p, s, col=(255, 255, 255, 255), rot=0, sx=1.0):
        c.fit(im, p[0] - s * sx / 2, p[1] - s / 2, s * sx, s, col, rot)
    labels = []
    def lab(y, t): labels.append((y + 22, t))
    aura, spark, streak = res('Art/Fx/Aura'), res('Art/Fx/Sparkle'), res('Art/Fx/Streak')
    # 1: idle specials with their aura
    y = H - 260; at = strip(y); lab(y + cell + 22, 'Суперфишки на поле: свечение с лучами')
    for q, (k, col) in enumerate((('pitchfork', 0), ('pitchfork', 3), ('bomb', 5), ('rainbow', None), ('bee', 1), ('tomato', None))):
        if k != 'tomato':
            put(aura, at(q), cell * 1.25, (RAINBOW_AURA if col is None else CROP[col]) + (230,))
        put(icon_img(k), at(q), cell * (.8 if k != 'tomato' else .86), rot=-90 if q == 0 else 0)
    # 2: the pitchfork's line clear
    y -= 290; at = strip(y); lab(y + cell + 22, 'Вилы: световая полоса по ряду')
    for q in (0, 1, 5): put(icon_img(TILES[q]), at(q), cell * .86)
    a, b = at(0), at(5)
    c.paste(streak, a[0] - cell * .3, a[1] - cell * .31, b[0] - a[0] + cell * .6, cell * .62, (255, 190, 170, 245))
    put(icon_img('pitchfork'), at(4), cell * .85, rot=-90)
    for i, q in enumerate((1.2, 2.1, 3.0)): put(spark, (at(0)[0] + q * cell, a[1] + (22 if i % 2 else -26)), cell * .34)
    # 3: the bomb
    y -= 290; at = strip(y); lab(y + cell + 22, 'Бомба: взрыв и ударная волна')
    for q in (0, 5): put(icon_img(TILES[q + 0]), at(q), cell * .86)
    put(res('Art/Fx/Burst'), at(2.5), cell * 2.1)
    for i in range(10):
        a_ = i * 36 + 12; r = cell * (1.15 + .22 * (i % 3))
        put(spark if i % 2 == 0 else res('Art/Fx/Pollen'), (at(2.5)[0] + r * math.cos(math.radians(a_)), at(2.5)[1] + r * .55 * math.sin(math.radians(a_))), cell * (.3 if i % 2 == 0 else .16), (255, 220, 150, 255))
    # 4: the rainbow
    y -= 290; at = strip(y); lab(y + cell + 22, 'Радуга: радужное кольцо и лучи к фишкам')
    for q in (0, 1, 4, 5): put(icon_img('blueberry' if q in (0, 5) else TILES[q]), at(q), cell * .86)
    for q in (0, 5):
        a, b = at(2.5), at(q)
        c.paste(streak, min(a[0], b[0]), a[1] - cell * .16, abs(b[0] - a[0]), cell * .32, (170, 190, 255, 235))
        put(spark, b, cell * .5)
    put(res('Art/Fx/RainbowRing'), at(2.5), cell * 1.9)
    put(aura, at(2.5), cell * 1.4, RAINBOW_AURA + (230,)); put(icon_img('rainbow'), at(2.5), cell * .95)
    # 5: the bee
    y -= 290; at = strip(y); lab(y + cell + 22, 'Пчела: летит к цели, за ней пыльца')
    put(icon_img('crate'), at(5), cell * .96)
    for q in (0,): put(icon_img(TILES[2]), at(q), cell * .86)
    for i in range(7):
        t = i / 7
        px = at(0.6)[0] + (at(4.2)[0] - at(0.6)[0]) * t; py = at(0)[1] + cell * .55 * 4 * t * (1 - t) - 6
        put(res('Art/Fx/Pollen'), (px, py), cell * (.16 + .26 * t), (255, 255, 255, int(70 + 185 * t)))
    put(icon_img('bee'), (at(4.2)[0], at(0)[1] + 8), cell * .95, rot=-14)
    for y, t in labels: c.text(90, y, 900, 50, [t], 36, (255, 255, 255, 255), 'Left', True)
    c.im.convert('RGB').save(os.path.join(OUT, 'specials_fx.png')); print('specials_fx.png')

if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    lvl = dict(board=board(SPECIALS, OBST))
    render('level', 'level.png', **lvl)
    render('farm', 'farm.png', world=world('Yard'))
    render('farm2', 'farm_economy.png', world=world('Garden'))
    render('win', 'popup_win.png', under=('level', lvl))
    render('start', 'popup_start.png', under=('farm', dict(world=world('Yard'))))
    render('settings', 'popup_settings.png', under=('farm', dict(world=world('Yard'))))
    specials_sheet()
