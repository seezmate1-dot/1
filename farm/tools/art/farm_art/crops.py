"""The plot's soil bed and every crop's plants in four steps (young, half grown, nearly ripe, ripe)."""
import math
from iso import Art, A, B, lite, dark, mix, rnd, smooth, ribbon
import trees

CROPS = ['wheat', 'corn', 'carrot', 'sunflower', 'strawberry', 'cotton', 'beet', 'potato', 'pumpkin']
PW, PH = 170, 122            # the bed's canvas, map units
CW, CH = 170, 224            # a crop's canvas: the same foot, more room above
FOOT = 14                    # from the canvas' bottom to the footprint's nearest corner

SOIL, SOIL_D, SOIL_L = '#8f5d36', '#6a4226', '#b07a48'
G1, G2, G3 = '#4f9a36', '#6fbf45', '#a5dc62'     # leaf greens: shade, body, light


def bed_art(h=PH):
    return Art(PW, h, PW / 2, h - FOOT)


LEAFR = dict(r=2.6, depth=1.0, lit=.3, shade=.55, rim=.12, fine=.4)
SOILDROP = dict(dx=2, dy=2.6, sd=2, op=.34, col='#1c0e04')


def bed(a, wet=False):
    """Tilled soil: a low raised bed with rounded ridges and furrows, clods, a lit rim and grass at its foot."""
    m = .035
    a.poly([(m, m - .05), (1 - m + .1, m - .08), (1 - m + .1, 1 - m - .08), (m + .02, 1 - m)], '#0e2010', op=.36, flt=a.blur(2.4))
    side_l, side_r = '#7a4c2a', '#5a361e'
    z = 7
    a.layer(relief=dict(r=2.5, depth=.8, lit=.3, shade=.4, fine=.5))
    a.poly([(m, m, 0), (1 - m, m, 0), (1 - m, m, z), (m, m, z)], a.lg([(0, side_r), (1, dark(side_r, .3))]), None)
    a.poly([(m, m, 0), (m, 1 - m, 0), (m, 1 - m, z), (m, m, z)], a.lg([(0, side_l), (1, dark(side_l, .25))]), None)
    top = [(m, m, z), (1 - m, m, z), (1 - m, 1 - m, z), (m, 1 - m, z)]
    a.poly(top, a.lg([(0, dark(SOIL, .2)), (1, dark(SOIL, .34))]), dark(SOIL, .5), 1.0)
    r = rnd(7)
    # layers of earth on the cut sides
    for i in range(18):
        t = r.random()
        for side in (0, 1):
            p0 = a.p(m + t * (1 - 2 * m), m, r.random() * z) if side else a.p(m, m + t * (1 - 2 * m), r.random() * z)
            a.ell(p0[0], p0[1], 2 + r.random() * 3, .7, r.choice(['#3a2010', '#9a6a40']), .5)
    # ridges run along u: each a rounded bank of loose earth, lit from the left, the furrow between them dark
    n = 6
    a.layer(relief=dict(r=2.2, depth=1.25, lit=.5, shade=.75, fine=.3))
    w = (1 - 2 * m - .05) / n
    for i in range(n - 1, -1, -1):
        v0 = m + .025 + i * w
        pts = []
        steps = 14
        for j in range(steps + 1):
            u = m + .03 + j * (1 - 2 * m - .06) / steps
            pts.append(a.p(u, v0 + w * .12 + r.uniform(-.006, .006), z))
        for j in range(steps, -1, -1):
            u = m + .03 + j * (1 - 2 * m - .06) / steps
            pts.append(a.p(u, v0 + w * .88 + r.uniform(-.006, .006), z))
        a.path(smooth(pts, True, .35), a.lg([(0, lite(SOIL, .1)), (1, SOIL)]))
    a.layer()
    # crumbs and clods of turned earth, each with a lit side and a shadow; a few stones
    for _ in range(90):
        u, v = m + .06 + r.random() * (1 - 2 * m - .12), m + .06 + r.random() * (1 - 2 * m - .12)
        x, y = a.p(u, v, z)
        a.ell(x, y, .9 + r.random() * 1.1, .6 + r.random() * .5, r.choice([SOIL_L, SOIL_D, '#c08a56', '#4a2a14']), .7)
    a.layer(relief=dict(r=1.2, depth=1.2, lit=.5, shade=.6))
    for _ in range(30):
        u, v = m + .07 + r.random() * (1 - 2 * m - .14), m + .07 + r.random() * (1 - 2 * m - .14)
        x, y = a.p(u, v, z)
        rr = 1.5 + r.random() * 2
        a.ell(x + rr * .5, y + rr * .4, rr, rr * .55, '#2a1608', .6)
        a.path(smooth([(x - rr, y), (x - rr * .3, y - rr * .7), (x + rr * .7, y - rr * .45), (x + rr, y + rr * .2), (x, y + rr * .5)]), r.choice(['#9a6a40', '#8a5a34', '#a87a4a']))
    for _ in range(5):
        x, y = a.p(m + .1 + r.random() * .7, m + .1 + r.random() * .7, z)
        a.ell(x + .8, y + .8, 2.4, 1.4, '#2a1608', .6)
        a.ell(x, y, 2.4, 1.5, '#b0a89c')
    a.layer()
    # the rim catches the light on the left
    a.line(a.p(m, m, z), a.p(m, 1 - m, z), '#d09a62', 1.4, .7)
    a.line(a.p(m, 1 - m, z), a.p(1 - m, 1 - m, z), '#c48e58', 1.0, .4)
    # grass grows up to its foot
    a.layer(relief=dict(r=1.2, depth=1, lit=.3, shade=.4))
    for i in range(22):
        t = i / 21
        for (u, v) in ((m + t * (1 - 2 * m), m - .012), (m - .012, m + t * (1 - 2 * m))):
            x, y = a.p(u, v)
            for k in (-1, 0, 1):
                hgt = 5 + r.random() * 6
                lean = k * 2.6 + r.random() * 2 - 1
                a.path('M%.1f %.1f q%.1f %.1f %.1f %.1f q%.1f %.1f %.1f %.1f Z' % (x + k * 1.6 - 1, y + 2.4, 0, -hgt * .6, lean, -hgt, 1, hgt * .5, 2 - lean, hgt),
                       r.choice(['#5fae3c', '#7cc84c', '#4c9632', '#8fd05a']))


def blade(a, x, y, ang, ln, wd, col, bend=.25, vein=None, lit=None):
    """A long leaf from (x, y) at an angle (degrees, 90 is up), arching to one side: a lit half, a shaded half
    and a midrib."""
    t = math.radians(ang)
    dx, dy = math.cos(t), -math.sin(t)
    nx, ny = -dy, dx
    sp = []
    for i in range(6):
        f = i / 5
        off = bend * ln * f * f
        w = wd * 2 * (math.sin(math.pi * min(1, f * .9 + .1)) ** .7) * (1 - f * .25)
        sp.append((x + dx * ln * f + nx * off, y + dy * ln * f + ny * off + abs(bend) * ln * f * f * .35, max(.3, w if i < 5 else .3)))
    a.path(ribbon(sp, 5), col, dark(col, .45), .5)
    lit = lit or lite(col, .22)
    side = -1 if (nx * -.52 + ny * -.62) > 0 else 1
    sp2 = [(px - side * nx * w * .22, py - side * ny * w * .22, w * .5) for (px, py, w) in sp]
    a.path(ribbon(sp2, 5), lit, op=.8)
    a.path('M' + ' L'.join('%.1f %.1f' % (px, py) for (px, py, w) in sp[:-1]), stroke=vein or dark(col, .3), sw=.6, op=.7)
    return sp[-1][:2]


def oval(a, x, y, ang, ln, wd, col, teeth=0, vein=None, bend=0):
    """A broad leaf (trees.leaf) in the crop's greens."""
    trees.leaf(a, 'bush', x, y, ang, ln, wd, (lite(col, .2), dark(col, .12), vein or lite(col, .45)), teeth=teeth, bend=bend)


def stem(a, pts, col, w0, w1):
    """A stalk through the points, lit on its left."""
    n = len(pts)
    sp = [(p[0], p[1], w0 + (w1 - w0) * i / (n - 1)) for i, p in enumerate(pts)]
    a.path(ribbon(sp, 6), col, dark(col, .5), .4)
    a.path('M' + ' L'.join('%.1f %.1f' % (px - w * .2, py) for (px, py, w) in sp), stroke=lite(col, .4), sw=max(.5, w0 * .28), op=.8)


def flower5(a, x, y, R, col, heart='#f6d040', sq=.85):
    s = ''
    g = a.rg([(0, '#ffffff'), (.5, col), (1, dark(col, .12))], .5, .5, .55)
    for j in range(5):
        q = math.radians(j * 72 - 90)
        cx, cy = x + math.cos(q) * R * .62, y + math.sin(q) * R * .62 * sq
        s += '<ellipse cx="%.1f" cy="%.1f" rx="%.1f" ry="%.1f" fill="%s" stroke="%s" stroke-width=".3" transform="rotate(%.0f %.1f %.1f)"/>' % (cx, cy, R * .6, R * .46 * sq, g, dark(col, .3), math.degrees(q), cx, cy)
    a.add(s)
    a.ell(x, y, R * .3, R * .27 * sq, heart, stroke=dark(heart, .4), sw=.3)


def wheat(a, x, y, k, st, r):
    ripe = st == 4
    green = mix('#4f9a36', '#a8b83e', (st - 1) / 3)
    stemc = '#d9a938' if ripe else green
    h = 62 * k
    n = 5 if st > 1 else 4
    for i in range(n):
        ox = (i - (n - 1) / 2) * 3.4 + r.uniform(-1, 1)
        lean = (i - (n - 1) / 2) * 1.5 + r.uniform(-2, 2)
        hh = h * (.82 + r.random() * .28)
        tx, ty = x + ox + lean, y - hh
        # a leaf blade or two at the foot
        blade(a, x + ox * .5, y, 90 - (lean * 4 + r.uniform(-25, 25)), hh * (.5 + r.random() * .2), 1.5 + k * .7, mix(green, '#c8a83a', .5) if ripe else green, bend=r.uniform(-.35, .35))
        if st >= 2:
            stem(a, [(x + ox * .5, y), (x + ox + lean * .4, y - hh * .55), (tx, ty)], stemc, 1.5, 1.0)
        if st >= 3:
            ear = '#f2c548' if ripe else '#b4c852'
            L = (15 if ripe else 12) * (.9 + r.random() * .2)
            droop = (lean * .12 + r.uniform(-.2, .2)) * (1.6 if ripe else .6)
            pts = [(tx + droop * j * j * .6, ty - j * L / 6 + abs(droop) * j * j * .12) for j in range(7)]
            awn = '#fbe69a' if ripe else '#d6e486'
            s = ''
            for j, (ex, ey) in enumerate(pts[:-1]):
                for sg in (-1, 1):
                    s += '<path d="M%.1f %.1f q%.1f %.1f %.1f %.1f" stroke="%s" stroke-width=".4" fill="none" opacity=".9"/>' % (ex + sg * 1.4, ey, sg * 2.2, -5, sg * (3 + j * .4) + droop * 2, -9 - (5 - j) * 1.2, awn)
            a.add(s)
            for j, (ex, ey) in enumerate(pts[:-1]):
                rr = 2.5 - abs(j - 2.5) * .22
                a.ell(ex - 1.5, ey, rr * .8, rr * 1.15, dark(ear, .2), rot=-22, stroke=dark(ear, .5), sw=.3)
                a.ell(ex + 1.5, ey - .9, rr * .8, rr * 1.15, ear, rot=22, stroke=dark(ear, .5), sw=.3)
                a.ell(ex + 1.1, ey - 1.5, rr * .3, rr * .5, lite(ear, .5), .8, rot=22)
            a.ell(pts[-1][0], pts[-1][1], 1.5, 2.6, lite(ear, .2), stroke=dark(ear, .5), sw=.3)


def corn(a, x, y, k, st, r):
    h = 84 * k
    lean = r.uniform(-2, 2)
    nodes = [(x + lean * f, y - h * f) for f in (0, .25, .5, .75, 1)]
    n = 3 + st
    order = list(range(n))
    back = [i for i in order if i % 3 == 2]
    def lf(i):
        f = .14 + .72 * i / n
        side = 1 if i % 2 else -1
        col = [G1, G2, '#5cab3c'][i % 3]
        blade(a, x + lean * f, y - h * f, 90 - side * (30 + r.random() * 16), (27 + r.random() * 8) * k, 3.4 * k + .8, col, bend=side * (.32 + r.random() * .16), vein=G3)
    for i in back:
        lf(i)
    stem(a, nodes, '#4f9a36', 4.6 * max(.55, k), 2.6 * max(.55, k))
    for (nx_, ny_) in nodes[1:-1]:
        a.path('M%.1f %.1f q%.1f 1.2 %.1f 0' % (nx_ - 2.2 * k, ny_, 2.2 * k, 4.4 * k), stroke='#2f7a2c', sw=.8)
    if st >= 3:
        # the tassel
        for j in range(-2, 3):
            a.path('M%.1f %.1f q%.1f %.1f %.1f %.1f' % (x + lean, y - h + 1, j * 1.6, -5 * k, j * 3.6, -9 * k - (2 - abs(j)) * 2.4), stroke='#b89a4a', sw=1.8)
            a.path('M%.1f %.1f q%.1f %.1f %.1f %.1f' % (x + lean, y - h + 1, j * 1.6, -5 * k, j * 3.6, -9 * k - (2 - abs(j)) * 2.4), stroke='#f0dc98', sw=1.0)
    for i in order:
        if i not in back:
            lf(i)
    if st >= 3:
        for (f, side) in ((.5, 1), (.36, -1)) if st == 4 else ((.5, 1),):
            cx, cy = x + lean * f + side * 4.5 * k, y - h * f - 7 * k
            rot = side * 20
            L, W = 13 * k, 5 * k
            inner = ''
            if st == 4:
                g = a.lg([(0, '#ffe680'), (.5, '#f6c53a'), (1, '#d8962a')], 0, 0, 1, 0)
                inner += '<ellipse cx="0" cy="0" rx="%.1f" ry="%.1f" fill="%s" stroke="#9a6a14" stroke-width=".4"/>' % (W, L, g)
                for row in range(-5, 6):
                    for c in range(-2, 3):
                        yy = row * L * .16
                        ww = W * math.sqrt(max(0, 1 - (yy / L) ** 2))
                        inner += '<ellipse cx="%.1f" cy="%.1f" rx="%.1f" ry="%.1f" fill="%s" stroke="#c88a1e" stroke-width=".25"/>' % (c * ww * .4, yy, ww * .2, L * .075, '#fff0a0' if c < 0 else '#f8d048' if c < 2 else '#e0a030')
                # the husk, peeled back
                inner += '<path d="M%.1f %.1f Q%.1f %.1f %.1f %.1f Q%.1f %.1f %.1f %.1f Z" fill="%s" stroke="%s" stroke-width=".4"/>' % (-W * 1.1, L * .2, -W * 1.5, L * 1.0, 0, L * 1.15, -W * .4, L * .6, -W * .5, L * .1, '#8fd05a', '#3f8a2c')
                inner += '<path d="M%.1f %.1f Q%.1f %.1f %.1f %.1f Q%.1f %.1f %.1f %.1f Z" fill="%s" stroke="%s" stroke-width=".4"/>' % (W * 1.1, L * .1, W * 1.6, L * .9, 0, L * 1.15, W * .5, L * .6, W * .5, L * .2, '#6fbf45', '#3f8a2c')
            else:
                inner += '<ellipse cx="0" cy="0" rx="%.1f" ry="%.1f" fill="%s" stroke="#3f8a2c" stroke-width=".4"/>' % (W, L, a.lg([(0, '#b8e078'), (1, '#6fbf45')], 0, 0, 1, 0))
                for c in (-.4, .1, .5):
                    inner += '<path d="M%.1f %.1f Q%.1f 0 %.1f %.1f" stroke="#4f9a36" stroke-width=".5" fill="none"/>' % (c * W, L * .9, c * W * 1.6, c * W * .4, -L * .9)
            # silk
            for j in (-1, 0, 1):
                inner += '<path d="M0 %.1f q%.1f -%.1f %.1f -%.1f" stroke="%s" stroke-width=".7" fill="none"/>' % (-L * .95, j * 3, 3 * k, j * 5 + 1, 7 * k, '#b8742a' if st == 4 else '#f0e0a0')
            a.group(inner, transform='translate(%.1f %.1f) rotate(%.0f)' % (cx, cy, rot))


def carrot(a, x, y, k, st, r):
    n = 4 + st
    fr = []
    for i in range(n):
        ang = 90 + (i - (n - 1) / 2) * (130 / n) + r.uniform(-6, 6)
        fr.append((abs(ang - 90), ang))
    for (_, ang) in sorted(fr, reverse=True):
        ln = (14 + 26 * k) * (.75 + r.random() * .4)
        t = math.radians(ang)
        col = r.choice([G1, G2, '#5cab3c', '#86c452'])
        bend = (ang - 90) * .004
        pts = []
        for j in range(8):
            f = j / 7
            pts.append((x + math.cos(t) * ln * f + bend * ln * f * f * 20 * math.copysign(1, ang - 90) * .0 + (ang - 90) * -.0, y - 2 - math.sin(t) * ln * f + f * f * abs(ang - 90) * .08 * k))
        a.path('M' + ' L'.join('%.1f %.1f' % q for q in pts), stroke=dark(col, .35), sw=1.6)
        a.path('M' + ' L'.join('%.1f %.1f' % q for q in pts), stroke=lite(col, .1), sw=.9)
        # the frond: pairs of small cut leaflets, shorter towards the tip
        for j in range(2, 8):
            f = j / 7
            px, py = pts[j]
            for sg in (-1, 1):
                la = ang + sg * 55
                ll = (6.5 * k + 2) * (1.15 - f * .7)
                c = lite(col, .18) if sg < 0 else col
                trees.leaf(a, 'bush', px, py, la, ll, ll * .32, (lite(c, .15), dark(c, .1), lite(c, .4)), teeth=2, rib=False)
        trees.leaf(a, 'bush', pts[-1][0], pts[-1][1], ang, 5 * k + 2, 1.8 * k + .6, (lite(col, .3), col, col), rib=False)
    if st == 4:
        # the carrot's shoulder shows above the soil
        a.ell(x + 1.5, y + 2.6, 11, 5.4, '#2a1608', .5)
        d = smooth([(x - 9, y - 1), (x - 5, y - 5.4), (x + 5, y - 5.4), (x + 9, y - 1), (x + 7, y + 3.6), (x, y + 5), (x - 7, y + 3.6)])
        a.path(d, a.rg([(0, '#ffb45a'), (.55, '#f08a2a'), (1, '#b8541a')], .35, .3, .8), '#8a3e10', .5)
        a.path('M%.1f %.1f q4 1.6 8 .2 M%.1f %.1f q5 1.8 11 .2' % (x - 5, y + 2.6, x - 7, y - .4), stroke='#c9601c', sw=.6)
        a.ell(x - 3.4, y - 2.6, 3, 1.4, '#ffe0a8', .9, rot=-10)
        a.ell(x, y - 4.4, 3.4, 1.5, '#4f9a36')


def sunflower(a, x, y, k, st, r):
    h = 92 * k
    lean = r.uniform(-4, 4)
    tx, ty = x + lean, y - h
    pts = [(x, y), (x - lean * .4, y - h * .5), (tx, ty)]
    for i in range(2 + st):
        side = 1 if i % 2 else -1
        f = .16 + .6 * i / (2 + st)
        col = [G1, G2, '#5cab3c'][i % 3]
        px, py = x - lean * .4 * math.sin(f * math.pi), y - h * f
        a.path('M%.1f %.1f l%.1f %.1f' % (px, py, side * 4 * k, -2 * k), stroke='#4f9a36', sw=1.4)
        oval(a, px + side * 3 * k, py - 1.5 * k, 90 - side * (62 + r.random() * 18), 24 * k, 8.4 * k, col, teeth=4, bend=-side * .25)
    stem(a, pts, '#4f9a36', 4.2 * max(.6, k), 3 * max(.6, k))
    if st < 3:
        for sg in (-1, 0, 1):
            oval(a, tx, ty + 1, 90 + sg * 48, (13 if sg else 10) * k + 3, 4.4 * k + 1.2, '#86c452', teeth=3)
    if st == 3:
        a.ell(tx, ty - 1, 9, 8, a.rg([(0, '#a8e070'), (1, '#4f9a36')], .35, .3, .8), stroke='#2f7a2c', sw=.5)
        for j in range(8):
            q = math.radians(j * 45 + 10)
            trees.leaf(a, 'bush', tx + math.cos(q) * 4, ty - 1 + math.sin(q) * 3.6, -math.degrees(q), 8, 2.6, ('#8fd05a', '#4f9a36', '#b8dc58'), rib=False)
        a.ell(tx, ty - 1, 4, 3.6, '#e8c838')
    if st == 4:
        R = 18
        s = ''
        for ring, col, rr, cnt in ((0, '#e89a16', 1.0, 17), (1, '#ffcc28', .86, 17), (2, '#ffe060', .66, 13)):
            for i in range(cnt):
                t = 2 * math.pi * (i + ring * .5) / cnt
                px, py = tx + math.cos(t) * R * rr * .66, ty + math.sin(t) * R * rr * .62
                s += '<ellipse cx="%.1f" cy="%.1f" rx="%.1f" ry="%.1f" fill="%s" stroke="%s" stroke-width=".35" transform="rotate(%.0f %.1f %.1f)"/>' % (px, py, R * .4 * rr, R * .15 * rr, col, dark(col, .35), math.degrees(t), px, py)
        a.add(s)
        a.ell(tx, ty, R * .5, R * .47, a.rg([(0, '#a8662a'), (.6, '#7a441a'), (1, '#4a260c')], .4, .35, .7), stroke='#3a1c08', sw=.5)
        for j in range(60):
            q, d = j * 2.39996, math.sqrt(j / 60) * R * .44
            a.ell(tx + math.cos(q) * d, ty + math.sin(q) * d * .94, .7, .7, '#3a1c08' if j % 2 else '#c88a40', .8)
        a.ell(tx - 3, ty - 3, 3.4, 2.2, '#f0b060', .45)


def strawberry(a, x, y, k, st, r):
    n = 5 + st * 2
    items = []
    for i in range(n):
        t = 2 * math.pi * i / n + r.random() * .5
        d = (4 + 10 * k) * (.5 + r.random() * .5)
        items.append((math.sin(t) * d, t, d))
    def berry(px, py, s_=1.0, ripe=True):
        c = '#e3262e' if ripe else '#f0e0b0'
        a.path('M%.1f %.1f l0 -%.1f' % (px, py - 3.6 * s_, 3 * s_), stroke='#4f9a36', sw=.9)
        d = 'M%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f Z' % (px, py - 4 * s_, px + 7 * s_, py - 5 * s_, px + 5 * s_, py + 4 * s_, px, py + 7 * s_, px - 5 * s_, py + 4 * s_, px - 7 * s_, py - 5 * s_, px, py - 4 * s_)
        a.path(d, a.rg([(0, '#ff8a78'), (.5, c), (1, dark(c, .45))], .35, .3, .8), dark(c, .6), .5)
        for (dx, dy) in ((-2, -1), (1.6, -1.2), (-.4, 1.4), (2.4, 1.6), (-2.4, 2), (.4, 4)):
            a.ell(px + dx * s_, py + dy * s_, .5, .7, '#ffe9a0')
        for sg in (-1, 0, 1):
            trees.leaf(a, 'bush', px, py - 3.8 * s_, 90 + sg * 62, 4.4 * s_, 1.4 * s_, ('#8fd05a', '#4f9a36', '#4f9a36'), rib=False)
        a.ell(px - 2 * s_, py - 1.6 * s_, 1.1 * s_, 1.7 * s_, '#ffd0c0', .8, rot=20)
    for (_, t, d) in sorted(items):
        px, py = x + math.cos(t) * d, y - 4 * k - 3 + math.sin(t) * d * .4 - abs(math.cos(t)) * 2 * k
        col = [G1, G2, '#5cab3c'][int(t * 3) % 3]
        a.path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (x, y - 1, (x + px) / 2, py - 2, px, py), stroke='#5a8a30', sw=.9)
        # three toothed leaflets on each stalk
        for sg in (-1, 0, 1):
            oval(a, px, py, 90 - math.degrees(math.cos(t)) * .6 + sg * 58, (8 * k + 3) * (1 if sg == 0 else .88), 3.2 * k + 1.4, col, teeth=4)
    if st == 3:
        for i in range(4):
            px, py = x + (i - 1.5) * 7 + r.uniform(-1.5, 1.5), y - 8 - r.random() * 8
            flower5(a, px, py, 4, '#ffffff')
        berry(x + 9, y - 1, .6, False)
    if st == 4:
        for i in range(5):
            px, py = x + (i - 2) * 6.6 + r.uniform(-1, 1), y - 2 - (i % 2) * 6 - r.random() * 2
            berry(px, py, 1.05 + r.random() * .25)


def cotton(a, x, y, k, st, r):
    h = 54 * k
    wood = '#7a5230'
    tips = []
    twigs = []
    for i in range(2 + st):
        side = 1 if i % 2 else -1
        yy = y - h * (.22 + .6 * i / (2 + st))
        bx, by = x + side * (13 + r.random() * 7) * k, yy - (8 + r.random() * 4) * k
        twigs.append((side, yy, bx, by))
        tips.append((bx, by))
    tips.append((x + r.uniform(-1, 1), y - h))
    for (side, yy, bx, by) in twigs:
        stem(a, [(x, yy), (x + side * 6 * k, yy - 3 * k), (bx, by)], wood, 1.8, 1.1)
    stem(a, [(x, y), (x + r.uniform(-1, 1), y - h * .5), (x, y - h)], wood, 3, 1.6)
    for (side, yy, bx, by) in twigs:
        # a three-lobed leaf
        for sg in (-1, 0, 1):
            oval(a, x + side * 5 * k, yy - 2 * k, 90 - side * 72 + sg * 34, (15 if sg == 0 else 11.5) * k, 4.6 * k, G2 if side < 0 else G1, bend=0)
    for sg in (-1, 0, 1):
        oval(a, x, y - h * .82, 100 + sg * 36, 11 * k, 4 * k, '#86c452')
    if st >= 3:
        for (bx, by) in tips:
            if st == 3:
                a.ell(bx, by, 3.3, 4.2, a.rg([(0, '#c8e88a'), (1, '#5cab3c')], .35, .3, .8), stroke='#2f7a2c', sw=.45)
                a.path('M%.1f %.1f q.8 4 0 8' % (bx, by - 4), stroke='#3f8a2c', sw=.4)
                for sg in (-1, 1):
                    trees.leaf(a, 'bush', bx, by + 3.6, -90 + sg * 125, 5.4, 1.8, ('#8fd05a', '#4f9a36', '#4f9a36'), rib=False)
            else:
                # the open boll: dry brown bracts under four tufts of lint
                for sg in (-1, 0, 1):
                    trees.leaf(a, 'bush', bx, by + 3.4, -90 + sg * 62, 7.4, 2.6, ('#a87a4a', '#6a4a2a', '#6a4a2a'), rib=False)
                for dx, dy, rr in ((-3.8, -.6, 5), (3.8, -.6, 5), (0, -4.8, 5.6), (0, 1.2, 4.8)):
                    a.ell(bx + dx, by + dy, rr, rr * .92, a.rg([(0, '#ffffff'), (.6, '#fbf8f0'), (1, '#d6d0c2')], .36, .3, .8), stroke='#b8b0a0', sw=.3)


def beet(a, x, y, k, st, r):
    n = 4 + st
    fr = []
    for i in range(n):
        ang = 90 + (i - (n - 1) / 2) * (116 / n) + r.uniform(-5, 5)
        fr.append((abs(ang - 90), ang))
    for (_, ang) in sorted(fr, reverse=True):
        ln = (14 + 26 * k) * (.8 + r.random() * .3)
        t = math.radians(ang)
        sx, sy = x + math.cos(t) * ln * .42, y - 2 - math.sin(t) * ln * .42
        stem(a, [(x + math.cos(t) * 2, y - 1), ((x + sx) / 2 + math.cos(t), (y + sy) / 2), (sx, sy)], '#c0306e', 2.4, 1.6)
        col = ['#3f8a3a', '#57a844', '#4a9a3e'][int(ang) % 3]
        oval(a, sx, sy, ang, ln * .66, 5.6 * k + 1.8, col, vein='#e0679e', bend=(.25 if ang < 90 else -.25))
    if st == 4:
        a.ell(x + 1, y + 2.6, 10, 5, '#2a1608', .5)
        a.ell(x, y + .6, 9, 5.6, a.rg([(0, '#e060a0'), (.5, '#a82a6c'), (1, '#5e1440')], .35, .3, .8), stroke='#4a1030', sw=.5)
        a.path('M%.1f %.1f q4 1.6 9 .2' % (x - 5, y + 2.6), stroke='#7a1c50', sw=.6)
        a.ell(x - 3, y - 1.2, 2.8, 1.3, '#f8b0d4', .85, rot=-10)


def potato(a, x, y, k, st, r):
    yel = st == 4
    n = 4 + st
    shoots = []
    for i in range(n):
        ang = 90 + (i - (n - 1) / 2) * (120 / n) + r.uniform(-8, 8)
        shoots.append((abs(ang - 90), ang))
    tops = []
    for (_, ang) in sorted(shoots, reverse=True):
        ln = (10 + 20 * k) * (.8 + r.random() * .35)
        t = math.radians(ang)
        tx, ty = x + math.cos(t) * ln, y - 2 - math.sin(t) * ln * .9
        stem(a, [(x, y - 1), ((x + tx) / 2, (y + ty) / 2 - 2), (tx, ty)], '#4f8a36', 1.8, 1.0)
        col = r.choice([G1, G2, '#5cab3c', '#3f8a3a'])
        if yel and r.random() < .4:
            col = '#b9b644'
        # a compound leaf: pairs of leaflets and one at the end
        for f in (.45, .72):
            px, py = x + (tx - x) * f, y - 1 + (ty - y) * f - 1.5
            for sg in (-1, 1):
                oval(a, px, py, ang + sg * 62, 7.5 * k + 2, 3.2 * k + 1, col)
        oval(a, tx, ty, ang, 9 * k + 2.4, 3.8 * k + 1.2, lite(col, .12))
        tops.append((tx, ty))
    if st == 3:
        for (tx, ty) in tops[-3:]:
            flower5(a, tx + r.uniform(-2, 2), ty - 3, 3.6, '#e4d4ff', '#f2c230')
    if st == 4:
        for dx, dy, rr in ((-10, 2, 6.4), (8, 3, 7), (-1, 5.5, 6)):
            a.ell(x + dx + 1, y + dy + 1.6, rr, rr * .66, '#2a1608', .5)
            a.ell(x + dx, y + dy, rr, rr * .7, a.rg([(0, '#f6dca4'), (.6, '#d8ae6a'), (1, '#9a7038')], .35, .3, .8), stroke='#7a5428', sw=.45, rot=r.uniform(-15, 15))
            for _ in range(3):
                a.ell(x + dx + r.uniform(-3, 3), y + dy + r.uniform(-1.5, 1.5), .6, .4, '#8a6030', .7)


def pumpkin(a, x, y, k, st, r):
    # the vine and its big lobed leaves
    n = 3 + st
    lv = []
    for i in range(n):
        t = 2 * math.pi * i / n + .6
        d = (8 + 16 * k)
        lv.append((math.sin(t), t, d))
    def leaf_(t, d):
        px, py = x + math.cos(t) * d, y - 5 * k + math.sin(t) * d * .42
        a.path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (x, y - 2, (x + px) / 2, py - 8 * k, px, py), stroke='#3f8a2c', sw=2.0)
        a.path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (x, y - 2.4, (x + px) / 2, py - 8 * k - .4, px, py - .4), stroke='#8fd05a', sw=.8)
        col = [G1, G2, '#5cab3c'][int(t * 2) % 3]
        rr = 8.5 * k + 3
        pts = []
        for j in range(15):
            q = 2 * math.pi * j / 15
            lobe = 1 + .16 * math.cos(5 * q)
            pts.append((px + math.cos(q) * rr * lobe, py - 1 + math.sin(q) * rr * .62 * lobe))
        dd = smooth(pts)
        a.path(dd, a.rg([(0, lite(col, .25)), (.7, col), (1, dark(col, .2))], .35, .3, .8), dark(col, .45), .5)
        for j in range(5):
            q = 2 * math.pi * j / 5
            a.line((px, py - 1), (px + math.cos(q) * rr * .9, py - 1 + math.sin(q) * rr * .56), lite(col, .4), .55, .7)
    for (sn, t, d) in sorted(lv):
        if sn < .2:
            leaf_(t, d)
    if st >= 2:
        # a curling tendril
        a.path('M%.1f %.1f q8 -8 12 -2 q2 5 -3 4 q-3 -2 0 -4' % (x + 4, y - 6 * k), stroke='#6fbf45', sw=.8)
    if st >= 3:
        R = 9.5 if st == 3 else 18
        col = '#7fb848' if st == 3 else '#f08a1e'
        cx, cy = x + 2, y - R * .58
        a.ell(cx + 3, cy + R * .66, R * 1.15, R * .32, '#1c0e04', .4, a.blur(2))
        for dx, w in ((-R * .66, .46), (R * .66, .46), (-R * .36, .58), (R * .36, .58), (0, .56)):
            a.ell(cx + dx, cy, R * w, R * .78, a.rg([(0, lite(col, .4)), (.55, col), (1, dark(col, .38))], .34, .28, .85), stroke=dark(col, .5), sw=.5)
        a.path('M%.1f %.1f q.6 -%.1f %.1f -%.1f' % (cx - 1, cy - R * .7, R * .3, R * .28, R * .42), stroke='#3a5a1a', sw=R * .2 + 1.4)
        a.path('M%.1f %.1f q.6 -%.1f %.1f -%.1f' % (cx - 1.4, cy - R * .7, R * .3, R * .26, R * .42), stroke='#7a9a3a', sw=R * .1 + .6)
        a.ell(cx - R * .5, cy - R * .32, R * .13, R * .26, '#fff0c0', .75, rot=20)
    for (sn, t, d) in sorted(lv):
        if sn >= .2:
            leaf_(t, d)


DRAW = dict(wheat=wheat, corn=corn, carrot=carrot, sunflower=sunflower, strawberry=strawberry, cotton=cotton, beet=beet, potato=potato, pumpkin=pumpkin)
GRID = dict(wheat=5, corn=3, carrot=4, sunflower=3, strawberry=3, cotton=3, beet=4, potato=3, pumpkin=2)
SIZE = dict(wheat=.9, corn=.92, carrot=.84, sunflower=1, strawberry=1.0, cotton=1, beet=.84, potato=1.0, pumpkin=1)
SCALE = {1: .38, 2: .66, 3: .9, 4: 1.0}


def crop_art(crop, st, with_bed=False):
    a = Art(CW, CH, CW / 2, CH - FOOT)
    a.seed = len(crop) * 5 + st
    a.look = dict(volume=.08, grain=.03)
    if with_bed:
        bed(a)
        a.layer()
    r = rnd(sum(ord(c) for c in crop) + 11)
    n = GRID[crop]
    cells = []
    for i in range(n):
        for j in range(n):
            u = .16 + (i + .5) * .68 / n + (r.random() - .5) * .04
            v = .16 + (j + .5) * .68 / n + (r.random() - .5) * .04
            cells.append((u, v, i + j))
    cells.sort(key=lambda c: -(c[0] + c[1]))
    plants = []
    for (u, v, row) in cells:
        x, y = a.p(u, v, 7)
        rr = rnd(int(u * 1000) * 31 + int(v * 1000))       # the same plant every step, only older
        k = SCALE[st] * SIZE[crop] * (.92 + rr.random() * .16)
        # each plant shades the soil under it
        a.ell(x + 4 * k, y + 1.5, 12 * k + 3, 4.6 * k + 1.5, '#1c0e04', .34, a.blur(1.6))
        plants.append((row, x, y, k, rr))
    last = None
    for (row, x, y, k, rr) in plants:
        if row != last:
            # every row is lit as a solid and shades the row behind it
            a.layer(relief=LEAFR, drop=SOILDROP)
            last = row
        DRAW[crop](a, x, y, k, st, rr)
    return a


def plot_art():
    a = bed_art()
    bed(a)
    return a


def seeded_art():
    """Just sown: little mounds of fresh earth with the first two leaves."""
    a = Art(CW, CH, CW / 2, CH - FOOT)
    a.look = dict(volume=.05, grain=.03)
    r = rnd(3)
    cells = [(.16 + (2 - i + .5) * .68 / 3, .16 + (2 - j + .5) * .68 / 3) for i in range(3) for j in range(3)]
    cells.sort(key=lambda c: -(c[0] + c[1]))
    for (u, v) in cells:
        x, y = a.p(u, v, 7)
        a.ell(x + 2, y + 2, 9.5, 4, '#1c0e04', .4, a.blur(1.2))
    a.layer(relief=dict(r=2.6, depth=1.2, lit=.45, shade=.6, fine=.3))
    for (u, v) in cells:
        x, y = a.p(u, v, 7)
        a.path(smooth([(x - 8.5, y + 1), (x - 5, y - 3), (x + 1, y - 4.4), (x + 6.5, y - 2.4), (x + 8.5, y + 1.4), (x + 3, y + 3.6), (x - 4, y + 3.4)]), a.lg([(0, '#b07a48'), (1, '#7a4c2a')]))
        for _ in range(7):
            a.ell(x + r.uniform(-6, 6), y + r.uniform(-2.4, 2.4), 1, .7, r.choice(['#c08a56', '#5a361e']), .8)
    a.layer(relief=dict(r=1.6, depth=1.1, lit=.35, shade=.5), drop=SOILDROP)
    for (u, v) in cells:
        x, y = a.p(u, v, 7)
        a.path('M%.1f %.1f q.6 -4 0 -7' % (x, y - 2), stroke='#6fbf45', sw=1.5)
        for sg in (-1, 1):
            trees.leaf(a, 'bush', x, y - 8.6, 90 - sg * (52 + r.random() * 14), 8 + r.random() * 2, 3, ('#b8e878', '#6fbf45', '#d8f4a0'))
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


def _one(args):
    c, st = args
    return crop_art(c, st, True).image()


if __name__ == '__main__':
    import sys
    from multiprocessing import Pool
    from PIL import Image
    cs = sys.argv[2].split(',') if len(sys.argv) > 2 else CROPS
    with Pool(4) as p:
        ims = p.map(_one, [(c, st) for c in cs for st in (1, 2, 3, 4)])
    # a contact sheet on grass: every crop, every step, on its bed
    sheet = Image.new('RGBA', (4 * 680, len(cs) * 620), (121, 185, 78, 255))
    for i, im in enumerate(ims):
        sheet.alpha_composite(im.crop((0, 896 - 620, 680, 896)), ((i % 4) * 680, (i // 4) * 620))
    sheet.convert('RGB').save(sys.argv[1], quality=90)
