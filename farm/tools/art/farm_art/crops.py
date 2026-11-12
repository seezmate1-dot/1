"""The plot's soil bed and every crop's plants in four steps (young, half grown, nearly ripe, ripe)."""
import math
from iso import Art, A, B, lite, dark, mix, rnd

CROPS = ['wheat', 'corn', 'carrot', 'sunflower', 'strawberry', 'cotton', 'beet', 'potato', 'pumpkin']
PW, PH = 170, 122            # the bed's canvas, map units
CW, CH = 170, 224            # a crop's canvas: the same foot, more room above
FOOT = 14                    # from the canvas' bottom to the footprint's nearest corner

SOIL, SOIL_D, SOIL_L = '#8f5d36', '#6a4226', '#b07a48'
G1, G2, G3 = '#4f9a36', '#6fbf45', '#a5dc62'     # leaf greens: shade, body, light


def bed_art(h=PH):
    return Art(PW, h, PW / 2, h - FOOT)


def bed(a, wet=False):
    """Tilled soil: a low raised bed with furrows, a soft rim and grass at its foot."""
    m = .035
    a.poly([(m, m - .05), (1 - m + .13, m - .09), (1 - m + .13, 1 - m - .09), (m + .02, 1 - m)], '#0e2010', op=.34, flt=a.blur(2.2))
    side_l, side_r = '#7a4c2a', '#5a361e'
    z = 7
    a.poly([(m, m, 0), (1 - m, m, 0), (1 - m, m, z), (m, m, z)], a.lg([(0, side_r), (1, dark(side_r, .25))]), None)
    a.poly([(m, m, 0), (m, 1 - m, 0), (m, 1 - m, z), (m, m, z)], a.lg([(0, side_l), (1, dark(side_l, .2))]), None)
    top = [(m, m, z), (1 - m, m, z), (1 - m, 1 - m, z), (m, 1 - m, z)]
    a.poly(top, a.lg([(0, lite(SOIL, .12)), (1, dark(SOIL, .08))]), dark(SOIL, .5), 1.2)
    # furrows run along u: a dark trough and a lit ridge
    n = 5
    for i in range(n):
        v0 = m + .05 + i * (1 - 2 * m - .1) / n
        w = (1 - 2 * m - .1) / n
        a.poly([(m + .04, v0 + w * .08, z), (1 - m - .04, v0 + w * .08, z), (1 - m - .04, v0 + w * .5, z), (m + .04, v0 + w * .5, z)], SOIL_D, op=.75)
        a.poly([(m + .04, v0 + w * .5, z), (1 - m - .04, v0 + w * .5, z), (1 - m - .04, v0 + w * .72, z), (m + .04, v0 + w * .72, z)], SOIL_L, op=.55)
    # crumbs
    r = rnd(7)
    for _ in range(26):
        x, y = a.p(m + .08 + r.random() * (1 - 2 * m - .16), m + .08 + r.random() * (1 - 2 * m - .16), z)
        a.ell(x, y, 1.3 + r.random(), .8 + r.random() * .5, r.choice([SOIL_L, SOIL_D, '#c08a56']), .7)
    # clods of turned earth, each with a lit side and a shadow, and a few stones
    for _ in range(34):
        u, v = m + .07 + r.random() * (1 - 2 * m - .14), m + .07 + r.random() * (1 - 2 * m - .14)
        x, y = a.p(u, v, z)
        rr = 1.6 + r.random() * 2.2
        a.ell(x + rr * .5, y + rr * .35, rr, rr * .55, '#3a2010', .7)
        a.path('M%.1f %.1f l%.1f %.1f l%.1f %.1f l%.1f %.1f Z' % (x - rr, y, rr * .7, -rr * .7, rr * 1.1, rr * .25, -rr * .6, rr * .7), r.choice(['#9a6a40', '#8a5a34', '#a87a4a']))
        a.path('M%.1f %.1f l%.1f %.1f' % (x - rr, y, rr * .7, -rr * .7), stroke='#d0a070', sw=.5)
    for _ in range(5):
        x, y = a.p(m + .1 + r.random() * .7, m + .1 + r.random() * .7, z)
        a.ell(x + .6, y + .6, 2.2, 1.3, '#3a2010', .7)
        a.ell(x, y, 2.2, 1.3, '#b0a89c')
        a.ell(x - .6, y - .4, .9, .5, '#e0dcd4')
    # the rim catches the light on the left
    a.line(a.p(m, m, z), a.p(m, 1 - m, z), '#d09a62', 1.6, .7)
    a.line(a.p(m, 1 - m, z), a.p(1 - m, 1 - m, z), '#c48e58', 1.2, .45)
    # grass grows up to its foot
    for i in range(14):
        t = i / 13
        for (u, v) in ((m + t * (1 - 2 * m), m - .012), (m - .012, m + t * (1 - 2 * m))):
            x, y = a.p(u, v)
            for k in (-1, 0, 1):
                a.path('M%.1f %.1f q%.1f %.1f %.1f %.1f' % (x + k * 2, y + 2, k * 1.5, -5, k * 3.4 + r.random() * 2 - 1, -7 - r.random() * 4),
                       stroke=r.choice(['#5fae3c', '#7cc84c', '#4c9632']), sw=1.6)


def leaf(a, x, y, ang, ln, wd, col, vein=None, bend=.25):
    """A blade from (x, y) at an angle (degrees, 90 is up), bent to one side."""
    t = math.radians(ang)
    dx, dy = math.cos(t), -math.sin(t)
    nx, ny = -dy, dx
    tx, ty = x + dx * ln + nx * ln * bend, y + dy * ln + ny * ln * bend
    mx, my = x + dx * ln * .55, y + dy * ln * .55
    d = 'M%.1f %.1f Q%.1f %.1f %.1f %.1f Q%.1f %.1f %.1f %.1f Z' % (x, y, mx + nx * wd, my + ny * wd, tx, ty, mx - nx * wd * .8, my - ny * wd * .8, x, y)
    a.path(d, col, dark(col, .4), .7)
    if vein:
        a.path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (x, y, mx, my, tx, ty), stroke=vein, sw=.9, op=.8)


def wheat(a, x, y, k, st, r):
    ripe = st == 4
    stem = '#d9a938' if ripe else mix('#5fae3c', '#b9b644', (st - 1) / 3.5)
    h = 58 * k
    for i in range(6):
        ox = (i - 2.5) * 3.2 + r.random() * 2
        lean = (i - 2.5) * 2.2 + r.random() * 3 - 1.5
        hh = h * (.85 + r.random() * .25)
        tx, ty = x + ox + lean, y - hh
        a.path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (x + ox * .5, y, x + ox, y - hh * .6, tx, ty), stroke=dark(stem, .15), sw=1.8)
        a.path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (x + ox * .5, y, x + ox, y - hh * .6, tx, ty), stroke=stem, sw=1.0)
        if st >= 3:
            ear = '#f0c448' if ripe else '#b7cc55'
            for j in range(4):
                ey = ty + j * 3.6
                a.ell(tx - 1.6 + lean * .02 * j, ey, 2.3, 2.9, dark(ear, .18), rot=-25)
                a.ell(tx + 1.6 + lean * .02 * j, ey + 1, 2.3, 2.9, ear, rot=25)
            a.ell(tx, ty - 3, 1.9, 3, lite(ear, .3))
            # awns: a fine bristle from every grain, fanning up and out
            awn = '#fbe08a' if ripe else '#d2e07a'
            for j in range(5):
                ey = ty - 3 + j * 3.2
                for sg in (-1, 1):
                    a.path('M%.1f %.1f q%.1f %.1f %.1f %.1f' % (tx + sg * 1.6, ey, sg * 2.4, -5, sg * (3.4 + j * .5) + lean * .08, -10 - (4 - j) * 1.6), stroke=awn, sw=.4)
            a.path('M%.1f %.1f l%.1f -9' % (tx, ty - 5, lean * .05), stroke=awn, sw=.45)
        elif st == 2:
            leaf(a, x + ox, y - hh * .5, 60 if i % 2 else 120, 12 * k, 2, G2)


def corn(a, x, y, k, st, r):
    h = 80 * k
    a.path('M%.1f %.1f L%.1f %.1f' % (x, y, x + 1, y - h), stroke='#3f8a2c', sw=4.2 * max(.6, k))
    a.path('M%.1f %.1f L%.1f %.1f' % (x - .8, y, x + .2, y - h), stroke='#7cc84c', sw=1.6 * max(.6, k))
    n = 2 + st
    for i in range(n):
        yy = y - h * (.18 + .7 * i / n)
        side = 1 if i % 2 else -1
        leaf(a, x, yy, 90 - side * (52 + r.random() * 14), (30 + r.random() * 8) * k, 4.6 * k, G2 if side < 0 else G1, vein=G3, bend=-side * .32)
    if st >= 3:
        cy = y - h * .5
        cob = '#f6c53a' if st == 4 else '#9fcf58'
        a.ell(x + 7 * k, cy, 5.4 * k, 12 * k, dark(cob, .25), rot=18)
        a.ell(x + 6.4 * k, cy - .6, 4.2 * k, 10.6 * k, cob, rot=18)
        if st == 4:
            for j in range(4):
                a.ell(x + 5.2 * k + j * .9, cy - 7 * k + j * 4.6 * k, 1.3, 1.3, '#fff0a0', .9)
        leaf(a, x + 3 * k, cy + 10 * k, 62, 17 * k, 4 * k, G2, bend=.1)
        # the tassel
        for j in (-1, 0, 1):
            a.path('M%.1f %.1f q%.1f %.1f %.1f %.1f' % (x + 1, y - h, j * 3, -7, j * 6, -11 * k), stroke='#e8d08a', sw=1.4)


def carrot(a, x, y, k, st, r):
    if st == 4:
        a.ell(x, y + 1, 7.5, 4.6, '#c9601c')
        a.ell(x, y - .5, 6.6, 4, '#f08a2a')
        a.ell(x - 2, y - 1.6, 2.6, 1.4, '#ffc27a', .9)
    n = 3 + st * 2
    for i in range(n):
        ang = 90 + (i - (n - 1) / 2) * (120 / n) + r.random() * 8 - 4
        ln = (16 + 22 * k) * (.75 + r.random() * .4)
        t = math.radians(ang)
        tx, ty = x + math.cos(t) * ln, y - 2 - math.sin(t) * ln
        col = [G1, G2, G3][i % 3]
        a.path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (x, y - 2, x + math.cos(t) * ln * .3, y - ln * .7, tx, ty), stroke=dark(col, .3), sw=2.6)
        a.path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (x, y - 2, x + math.cos(t) * ln * .3, y - ln * .7, tx, ty), stroke=col, sw=1.5)
        # feathery side leaflets
        for j in range(2, 6):
            f = j / 6
            px, py = x + (tx - x) * f, y - 2 + (ty - y + 2) * f - math.sin(f * math.pi) * ln * .12
            for sgn in (-1, 1):
                a.path('M%.1f %.1f l%.1f %.1f' % (px, py, sgn * 4.4 * k + 1, -3.4 * k - 1), stroke=col, sw=1.5)


def sunflower(a, x, y, k, st, r):
    h = 92 * k
    lean = r.random() * 6 - 3
    tx, ty = x + lean, y - h
    a.path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (x, y, x - lean, y - h * .5, tx, ty), stroke='#3f8a2c', sw=4 * max(.6, k))
    a.path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (x - .8, y, x - lean - .8, y - h * .5, tx - .6, ty), stroke='#84cc50', sw=1.4)
    for i in range(1 + st):
        side = 1 if i % 2 else -1
        yy = y - h * (.2 + .55 * i / (1 + st))
        leaf(a, x, yy, 90 - side * 58, 24 * k, 8.5 * k, G2 if side < 0 else G1, vein=G3, bend=-side * .12)
    if st == 3:
        a.ell(tx, ty, 8, 7, '#4f9a36')
        a.ell(tx - 1.5, ty - 1.5, 5, 4.2, '#8fd05a')
    if st == 4:
        R = 17
        for ring, col, rr in ((0, '#e89a16', 1.0), (1, '#ffd02e', .86)):
            for i in range(14):
                t = 2 * math.pi * (i + ring * .5) / 14
                px, py = tx + math.cos(t) * R * rr * .68, ty + math.sin(t) * R * rr * .6
                a.ell(px, py, R * .42 * rr, R * .2 * rr, col, rot=math.degrees(t) * 1.0, stroke=dark(col, .3), sw=.5)
        a.ell(tx, ty, R * .52, R * .46, '#6a3a12')
        a.ell(tx - 1.5, ty - 1.5, R * .36, R * .3, a.rg([(0, '#b06a26'), (1, '#7a441a')]))
        a.ell(tx - 3, ty - 3, 3, 2, '#e0a050', .7)


def strawberry(a, x, y, k, st, r):
    n = 4 + st * 2
    for i in range(n):
        t = 2 * math.pi * i / n + r.random() * .5
        d = (5 + 9 * k) * (.6 + r.random() * .5)
        px, py = x + math.cos(t) * d, y - 5 * k - 3 + math.sin(t) * d * .45 - abs(math.cos(t)) * 2
        col = [G1, G2, '#5cab3c'][i % 3]
        a.ell(px, py, 6.2 * k + 2, 4.8 * k + 1.6, col, rot=math.degrees(t) * .4, stroke=dark(col, .4), sw=.7)
        a.line((px - 3 * math.cos(t), py), (px + 3 * math.cos(t), py - 1), G3, .8, .7)
    if st == 3:
        for i in range(3):
            px, py = x + (i - 1) * 8 + r.random() * 3, y - 9 - r.random() * 6
            for j in range(5):
                t = 2 * math.pi * j / 5
                a.ell(px + math.cos(t) * 2.4, py + math.sin(t) * 2.1, 1.9, 1.7, '#ffffff')
            a.ell(px, py, 1.5, 1.4, '#f6d040')
    if st == 4:
        for i in range(4):
            px, py = x + (i - 1.5) * 7.5 + r.random() * 2, y - 3 - (i % 2) * 5 - r.random() * 2
            a.path('M%.1f %.1f q5.6 -1 5 4.6 q-1.4 5.4 -5 7 q-3.6 -1.6 -5 -7 q-.6 -5.6 5 -4.6Z' % (px, py - 4), a.rg([(0, '#ff7a6a'), (.6, '#e3262e'), (1, '#a8141e')]), '#8a1018', .7)
            a.path('M%.1f %.1f l-3.4 -1.6 l3.4 -1 l3.4 1 Z' % (px, py - 3.4), '#4f9a36')
            a.ell(px - 1.6, py - 1, 1.2, 1.6, '#ffc0b0', .85)
            for dx, dy in ((1.6, 1.6), (-1, 3.2), (2, 4.6)):
                a.ell(px + dx, py + dy, .55, .55, '#ffe9a0')


def cotton(a, x, y, k, st, r):
    h = 50 * k
    a.path('M%.1f %.1f L%.1f %.1f' % (x, y, x, y - h), stroke='#6b4a2a', sw=2.6)
    tips = []
    for i in range(2 + st):
        side = 1 if i % 2 else -1
        yy = y - h * (.25 + .6 * i / (2 + st))
        bx, by = x + side * (13 + r.random() * 6) * k, yy - 9 * k
        a.path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (x, yy, x + side * 6 * k, yy - 2, bx, by), stroke='#6b4a2a', sw=1.8)
        leaf(a, x + side * 4 * k, yy - 1, 90 - side * 70, 15 * k, 6 * k, G2 if side < 0 else G1, bend=-side * .1)
        tips.append((bx, by))
    tips.append((x, y - h))
    leaf(a, x, y - h * .8, 110, 13 * k, 5 * k, G3)
    if st >= 3:
        for (bx, by) in tips:
            if st == 3:
                a.ell(bx, by, 4.2, 4.8, '#7cc84c', stroke='#3f8a2c', sw=.7)
            else:
                a.ell(bx, by + 3, 6, 3, '#7a5a34')
                for dx, dy, rr in ((-3.4, -1, 4.6), (3.4, -1, 4.6), (0, -4.6, 5.2), (0, .6, 4.4)):
                    a.ell(bx + dx, by + dy, rr, rr * .92, '#e6e2d8')
                    a.ell(bx + dx - .9, by + dy - .9, rr * .8, rr * .74, '#ffffff')


def beet(a, x, y, k, st, r):
    if st == 4:
        a.ell(x, y + .5, 9, 5.6, '#6e1a48')
        a.ell(x, y - 1, 8, 4.8, a.rg([(0, '#d04a8e'), (1, '#8e2460')]))
        a.ell(x - 2.6, y - 2.4, 2.8, 1.5, '#f0a0cc', .8)
    n = 3 + st
    for i in range(n):
        ang = 90 + (i - (n - 1) / 2) * (110 / n) + r.random() * 8 - 4
        ln = (14 + 24 * k) * (.8 + r.random() * .3)
        t = math.radians(ang)
        sx, sy = x + math.cos(t) * ln * .45, y - 2 - math.sin(t) * ln * .45
        a.line((x, y - 2), (sx, sy), '#c0306e', 2.2)
        col = ['#3f8a3a', '#57a844', '#4a9a3e'][i % 3]
        leaf(a, sx, sy, ang, ln * .62, 5.4 * k + 1.6, col, vein='#d85a94', bend=(.15 if ang < 90 else -.15))


def potato(a, x, y, k, st, r):
    n = 6 + st * 3
    yel = st == 4
    for i in range(n):
        t = 2 * math.pi * i / n + r.random() * .6
        d = (4 + 11 * k) * (.4 + r.random() * .7)
        px, py = x + math.cos(t) * d, y - 6 - 10 * k * (.4 + r.random() * .8) + math.sin(t) * d * .3
        col = [G1, G2, '#5cab3c', '#86c452'][i % 4]
        if yel and i % 3 == 0:
            col = '#b9b644'
        a.ell(px, py, 5 * k + 1.6, 3.6 * k + 1.2, col, rot=r.random() * 80 - 40, stroke=dark(col, .4), sw=.6)
    if st == 3:
        for i in range(3):
            px, py = x + (i - 1) * 9 + r.random() * 3, y - 16 * k - r.random() * 6
            for j in range(5):
                t = 2 * math.pi * j / 5
                a.ell(px + math.cos(t) * 2.3, py + math.sin(t) * 2, 1.8, 1.6, '#e8dcff')
            a.ell(px, py, 1.4, 1.3, '#f2c230')
    if st == 4:
        for dx, dy, rr in ((-9, 2, 6), (7, 3, 6.6), (-1, 5, 5.6)):
            a.ell(x + dx, y + dy + 1, rr, rr * .7, '#8a6030')
            a.ell(x + dx, y + dy, rr * .94, rr * .64, a.rg([(0, '#f0d49a'), (1, '#c79a58')]))
            a.ell(x + dx - 1.6, y + dy + .6, .7, .5, '#8a6030', .7)
            a.ell(x + dx + 2, y + dy - .4, .6, .45, '#8a6030', .7)


def pumpkin(a, x, y, k, st, r):
    # the vine and its big leaves
    for i in range(2 + st):
        t = 2 * math.pi * i / (2 + st) + .6
        d = (8 + 15 * k)
        px, py = x + math.cos(t) * d, y - 4 + math.sin(t) * d * .42
        a.path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (x, y - 2, (x + px) / 2, py - 7, px, py), stroke='#4f9a36', sw=1.8)
        col = [G1, G2, '#5cab3c'][i % 3]
        rr = 7.5 * k + 2.5
        a.path('M%.1f %.1f q%.1f %.1f %.1f %.1f q%.1f %.1f %.1f %.1f q%.1f %.1f %.1f %.1f q%.1f %.1f %.1f %.1f Z' % (
            px - rr, py, 0, -rr * .8, rr, -rr * .75, rr * .9, 0, rr, rr * .7, 0, rr * .55, -rr, rr * .5, -rr * .8, 0, -rr, -rr * .45), col, dark(col, .4), .7)
        a.line((px - rr * .6, py), (px + rr * .6, py - 1), G3, .9, .7)
    if st >= 3:
        R = 9 if st == 3 else 17
        col = '#7fb848' if st == 3 else '#f08a1e'
        cx, cy = x + 2, y - R * .55
        a.ell(cx + 2, cy + R * .62, R * 1.1, R * .3, '#1c2a10', .3, a.blur(2))
        for dx, w in ((-R * .62, .5), (R * .62, .5), (-R * .32, .62), (R * .32, .62), (0, .6)):
            a.ell(cx + dx, cy, R * w, R * .78, a.rg([(0, lite(col, .35)), (.6, col), (1, dark(col, .3))]), stroke=dark(col, .4), sw=.7)
        a.path('M%.1f %.1f q1 -6 5 -7' % (cx, cy - R * .72), stroke='#5a7a2a', sw=3)
        a.ell(cx - R * .5, cy - R * .3, R * .16, R * .26, '#ffe0a0', .7, rot=20)


DRAW = dict(wheat=wheat, corn=corn, carrot=carrot, sunflower=sunflower, strawberry=strawberry, cotton=cotton, beet=beet, potato=potato, pumpkin=pumpkin)
GRID = dict(wheat=5, corn=4, carrot=4, sunflower=3, strawberry=4, cotton=3, beet=4, potato=4, pumpkin=2)
SIZE = dict(wheat=.9, corn=.86, carrot=.84, sunflower=1, strawberry=.82, cotton=1, beet=.84, potato=.82, pumpkin=1)
SCALE = {1: .38, 2: .66, 3: .9, 4: 1.0}


def crop_art(crop, st, with_bed=False):
    a = Art(CW, CH, CW / 2, CH - FOOT)
    a.seed = len(crop) * 5 + st
    if with_bed:
        bed(a)
    r = rnd(hash(crop) % 1000 + 11)
    n = GRID[crop]
    cells = []
    for i in range(n):
        for j in range(n):
            u = .16 + (i + .5) * .68 / n + (r.random() - .5) * .04
            v = .16 + (j + .5) * .68 / n + (r.random() - .5) * .04
            cells.append((u, v))
    cells.sort(key=lambda c: -(c[0] + c[1]))
    for (u, v) in cells:
        x, y = a.p(u, v, 7)
        # the same plant every step, only older
        rr = rnd(int(u * 1000) * 31 + int(v * 1000))
        k = SCALE[st] * SIZE[crop] * (.92 + rr.random() * .16)
        # each plant shades the soil under it
        a.ell(x + 5 * k, y + 1.5, 13 * k + 3, 5 * k + 1.5, '#2a1608', .3, a.blur(1.6))
        DRAW[crop](a, x, y, k, st, rr)
    return a


def seeded_art():
    """Just sown: little mounds with the first two leaves."""
    a = Art(CW, CH, CW / 2, CH - FOOT)
    r = rnd(3)
    for i in range(3):
        for j in range(3):
            x, y = a.p(.16 + (2 - i + .5) * .68 / 3, .16 + (2 - j + .5) * .68 / 3, 7)
            a.ell(x, y + 1, 7, 3.4, '#6a4226')
            a.ell(x, y, 6, 2.8, '#a8703f')
            a.path('M%.1f %.1f q-1 -5 -5 -6 q5 -1.6 5 6 q0 -7.6 5 -6 q-4 1 -5 6Z' % (x, y - 1), '#8fd05a', '#3f8a2c', .7)
    return a


def offer_art():
    """The next plot for sale: turf marked out with pegs and a rope."""
    a = bed_art()
    m = .05
    a.poly([(m, m), (1 - m, m), (1 - m, 1 - m), (m, 1 - m)], '#ffffff', op=.16)
    cs = [(m, m), (1 - m, m), (1 - m, 1 - m), (m, 1 - m)]
    for i in range(4):
        p, q = a.p(*cs[i], 9), a.p(*cs[(i + 1) % 4], 9)
        a.add('<line x1="%.1f" y1="%.1f" x2="%.1f" y2="%.1f" stroke="#fff8e0" stroke-width="2.2" stroke-dasharray="7 5" stroke-linecap="round"/>' % (p[0], p[1], q[0], q[1]))
    for c in sorted(cs, key=lambda c: -(c[0] + c[1])):
        x, y = a.p(*c)
        a.ell(x + 2, y + 1, 5, 2.4, '#1c2a10', .3, a.blur(1.5))
        a.path('M%.1f %.1f l0 -13 l5 0 l0 13 Z' % (x - 2.5, y), a.lg([(0, '#e0a868'), (1, '#b07a40')], 0, 0, 1, 0), '#6a4226', .8)
        a.ell(x, y - 13, 2.5, 1.3, '#f0c488')
    return a


def build():
    out = {}
    a = bed_art(); bed(a); out['Plot'] = a.save('Plot')
    out['PlotOffer'] = offer_art().save('PlotOffer')
    out['Seeded'] = seeded_art().save('Seeded')
    for c in CROPS:
        for st in (1, 2, 3, 4):
            out['Crop_%s_%d' % (c, st)] = crop_art(c, st).save('Crop_%s_%d' % (c, st))
    return out


if __name__ == '__main__':
    import sys
    from PIL import Image
    build()
    # a contact sheet on grass: every crop, every step, on its bed
    sheet = Image.new('RGBA', (4 * 360 + 20, len(CROPS) * 300), (124, 186, 78, 255))
    for i, c in enumerate(CROPS):
        for st in (1, 2, 3, 4):
            im = crop_art(c, st, True).image()
            sheet.alpha_composite(im, (10 + (st - 1) * 360, i * 300 - 90))
    sheet.convert('RGB').save(sys.argv[1] if len(sys.argv) > 1 else 'crops.jpg', quality=90)
