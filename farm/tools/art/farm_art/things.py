"""Trees, animals, pens, workshops and the small things of the farm map."""
import math
from iso import Art, A, B, L, lite, dark, mix, rnd, wall, rect, window, door

# ---------------------------------------------------------------------------------------------- trees

TREES = {
    'apple_tree': dict(leaf='#4f9a36', fruit='#e23a2e', blossom='#ffd9e6', shape=(1.0, 1.0)),
    'pear_tree': dict(leaf='#7aa83a', fruit='#f0d23c', blossom='#ffffff', shape=(.84, 1.18)),
    'cherry_tree': dict(leaf='#3f8a4a', fruit='#c2183a', blossom='#ffc2d8', shape=(1.12, .92)),
}
TW, TH = 210, 276


def tree(kind, state):
    """state: 'leaf' (after the harvest), 'bloom' (half way), 'ripe' (fruit on it)."""
    t = TREES[kind]
    a = Art(TW, TH, TW / 2, TH - 16)
    x, y = a.ox, a.oy
    r = rnd(len(kind) * 7 + 1)
    a.ell(x + 26, y + 2, 70, 22, '#1c2a10', .3, a.blur(6))
    # a ring of dug earth and the trunk
    a.ell(x, y, 30, 13, '#7a4c2a')
    a.ell(x, y - 1.5, 27, 11, '#9a6638')
    trunk = '#8a5a34'
    a.path('M%.1f %.1f q4 -30 -2 -62 l18 0 q-5 32 2 62 q-9 5 -18 0Z' % (x - 9, y), a.lg([(0, lite(trunk, .25)), (.5, trunk), (1, dark(trunk, .35))], 0, 0, 1, 0), dark(trunk, .5), 1)
    a.path('M%.1f %.1f q-12 -14 -26 -20' % (x - 4, y - 58), stroke=dark(trunk, .2), sw=7)
    a.path('M%.1f %.1f q12 -12 28 -16' % (x + 4, y - 60), stroke=dark(trunk, .3), sw=6)
    # the crown: big soft balls, dark at the bottom right, light at the top left
    sx, sy = t['shape']
    cx, cy, R = x, y - 132 * sy + (0 if sy <= 1 else 8), 62
    balls = [(-40, 22, .62), (42, 24, .62), (0, 34, .66), (-52, -6, .56), (54, -4, .56), (-26, -34, .62), (28, -36, .62), (0, -2, .8), (0, -52, .56)]
    leafc = t['leaf']
    for i, (dx, dy, k) in enumerate(balls):
        px, py, rr = cx + dx * sx, cy + dy * sy, R * k
        a.ell(px + 3, py + 4, rr, rr * .94, dark(leafc, .42))
        a.ell(px, py, rr, rr * .94, a.rg([(0, lite(leafc, .38)), (.55, leafc), (1, dark(leafc, .32))], .35, .3, .8))
    # leafy texture: dabs of light and dark
    for i in range(70):
        ang, d = r.random() * 6.283, math.sqrt(r.random()) * R * 1.08
        px, py = cx + math.cos(ang) * d * sx, cy + math.sin(ang) * d * sy * .92
        lit = (px - cx) + (py - cy) < 10
        a.ell(px, py, 5 + r.random() * 3, 3 + r.random() * 2, lite(leafc, .5) if lit else dark(leafc, .3), .5, rot=r.random() * 90 - 45)
    if state != 'leaf':
        n = 16 if state == 'ripe' else 26
        for i in range(n):
            ang, d = r.random() * 6.283, math.sqrt(.08 + r.random() * .8) * R * 1.0
            px, py = cx + math.cos(ang) * d * sx, cy + math.sin(ang) * d * sy * .9 + 4
            if state == 'bloom':
                for j in range(5):
                    q = 6.283 * j / 5
                    a.ell(px + math.cos(q) * 3.2, py + math.sin(q) * 3, 2.8, 2.6, t['blossom'])
                a.ell(px, py, 1.8, 1.7, '#f6c840')
            elif kind == 'cherry_tree':
                a.path('M%.1f %.1f q2 -7 6 -9 M%.1f %.1f q3 -5 5 -9' % (px - 4, py, px + 4, py + 1), stroke='#4a7a2a', sw=1.2)
                for dx in (-4, 4):
                    a.ell(px + dx, py + (1 if dx > 0 else 0), 4.6, 4.6, a.rg([(0, '#ff7a8a'), (.5, t['fruit']), (1, dark(t['fruit'], .4))]), stroke=dark(t['fruit'], .5), sw=.6)
                    a.ell(px + dx - 1.4, py - 1.4, 1.3, 1, '#ffd0d8', .9)
            elif kind == 'pear_tree':
                a.path('M%.1f %.1f q4.6 0 4.6 6 q4 5 0 10 q-4.6 3 -9.2 0 q-4 -5 0 -10 q0 -6 4.6 -6Z' % (px, py - 8), a.rg([(0, '#fff2a0'), (.5, t['fruit']), (1, '#b89a20')]), '#8a7414', .6)
                a.line((px, py - 8), (px + 1, py - 12), '#6b4a2a', 1.4)
            else:
                a.ell(px, py, 7, 6.6, a.rg([(0, '#ff9a80'), (.5, t['fruit']), (1, dark(t['fruit'], .4))]), stroke=dark(t['fruit'], .5), sw=.6)
                a.ell(px - 2.2, py - 2.4, 2, 1.4, '#ffd8cc', .9)
                a.line((px, py - 6), (px + 1, py - 9.5), '#6b4a2a', 1.3)
    return a


# ---------------------------------------------------------------------------------------------- animals

AW, AH = 150, 150
OUTL = '#4a2a14'


def _shadow(a, x, y, w):
    a.ell(x + 6, y + 1, w, w * .32, '#1c2a10', .32, a.blur(3))


def _eye(a, x, y, r=3.2):
    a.ell(x, y, r, r * 1.1, '#2a1608')
    a.ell(x - r * .3, y - r * .4, r * .38, r * .38, '#ffffff')


def chicken(a, x, y, ready):
    _shadow(a, x, y, 34)
    for lx in (-8, 8):
        a.path('M%.1f %.1f l0 12 m-5 0 l10 0' % (x + lx, y - 14), stroke='#e8901a', sw=3)
    body = a.rg([(0, '#ffffff'), (.6, '#f6efe2'), (1, '#d9cbb4')])
    a.path('M%.1f %.1f q14 -14 30 -2 q8 -14 -4 -26 q-14 6 -14 18Z' % (x + 14, y - 42), body, OUTL, 1.6)          # tail
    a.ell(x, y - 38, 30, 27, body, stroke=OUTL, sw=1.8)
    a.path('M%.1f %.1f q14 4 22 -8' % (x - 2, y - 34), stroke='#c9b89a', sw=2.4)                                 # wing
    a.ell(x - 22, y - 66, 17, 16, body, stroke=OUTL, sw=1.8)
    a.path('M%.1f %.1f q4 -10 9 -3 q5 -8 9 0 q5 -5 6 4Z' % (x - 34, y - 78), '#e8362e', '#8a1410', 1.2)          # comb
    a.path('M%.1f %.1f l-13 4 l12 5Z' % (x - 36, y - 66), '#f6a81e', '#a8600a', 1.2)                             # beak
    a.path('M%.1f %.1f q-1 8 3 9 q4 -2 2 -9Z' % (x - 32, y - 58), '#e8362e', '#8a1410', 1)
    _eye(a, x - 26, y - 69)
    a.ell(x - 18, y - 62, 4, 2.6, '#ffb0b0', .7)
    if ready:
        a.ell(x + 30, y - 4, 16, 6, '#c9a25a')
        a.ell(x + 30, y - 6, 13, 5, '#e8c878')
        a.ell(x + 30, y - 13, 8.4, 10.6, a.rg([(0, '#ffffff'), (.7, '#f6ead0'), (1, '#d8c29a')]), stroke='#a88a58', sw=1.2)


def cow(a, x, y, ready):
    _shadow(a, x, y, 52)
    body = a.rg([(0, '#ffffff'), (.6, '#f4efe6'), (1, '#d2c8b8')])
    for lx in (-30, -14, 14, 30):
        a.path('M%.1f %.1f l0 24' % (x + lx, y - 26), stroke=OUTL, sw=10.5)
        a.path('M%.1f %.1f l0 22' % (x + lx, y - 26), stroke='#f4efe6', sw=7.5)
        a.path('M%.1f %.1f l0 4' % (x + lx, y - 5), stroke='#5a4030', sw=8)
    a.path('M%.1f %.1f q14 -4 14 14' % (x + 40, y - 56), stroke=OUTL, sw=3.4)
    a.add('<rect x="%.1f" y="%.1f" width="88" height="46" rx="21" fill="%s" stroke="%s" stroke-width="1.8"/>' % (x - 42, y - 70, body, OUTL))
    for (dx, dy, rx, ry) in ((10, -56, 13, 9), (-12, -40, 9, 7), (30, -40, 8, 9)):
        a.ell(x + dx, y + dy, rx, ry, '#5a3a26')
    a.ell(x + 6, y - 25, 12, 6, '#ffc0c8', stroke='#c07080', sw=1)                                              # udder
    # the head
    hx, hy = x - 42, y - 62
    a.path('M%.1f %.1f q-8 -10 -2 -16 M%.1f %.1f q8 -10 2 -16' % (hx - 12, hy - 16, hx + 12, hy - 16), stroke='#e8d8b0', sw=5)
    a.ell(hx - 20, hy - 10, 9, 5.4, '#5a3a26', rot=-25, stroke=OUTL, sw=1.2)
    a.ell(hx + 20, hy - 10, 9, 5.4, '#f4efe6', rot=25, stroke=OUTL, sw=1.2)
    a.add('<rect x="%.1f" y="%.1f" width="36" height="40" rx="15" fill="%s" stroke="%s" stroke-width="1.8"/>' % (hx - 18, hy - 22, body, OUTL))
    a.ell(hx + 8, hy - 10, 9, 10, '#5a3a26')
    a.ell(hx, hy + 9, 16, 10, '#ffc8cc', stroke=OUTL, sw=1.5)
    a.ell(hx - 6, hy + 9, 2, 3, '#b06a78'); a.ell(hx + 6, hy + 9, 2, 3, '#b06a78')
    _eye(a, hx - 8, hy - 7); _eye(a, hx + 8, hy - 7)
    if ready:
        bx, by = x + 44, y - 4
        a.path('M%.1f %.1f l3 -24 l22 0 l3 24 q-14 5 -28 0Z' % (bx - 14, by), a.lg([(0, '#eef2f6'), (.4, '#c2ccd8'), (1, '#8a96a6')], 0, 0, 1, 0), '#5a6674', 1.4)
        a.ell(bx, by - 24, 11, 4, '#ffffff', stroke='#5a6674', sw=1.2)
        a.path('M%.1f %.1f q11 -16 22 0' % (bx - 11, by - 24), stroke='#5a6674', sw=1.8)


def sheep(a, x, y, ready):
    _shadow(a, x, y, 44)
    for lx in (-22, -8, 10, 24):
        a.path('M%.1f %.1f l0 20' % (x + lx, y - 22), stroke='#3a2a22', sw=6.5)
    wool = '#fbf6ea' if not ready else '#ffffff'
    k = 1.14 if ready else 1.0
    puffs = [(-30, -40, 17), (-14, -56, 18), (8, -60, 19), (28, -52, 17), (38, -36, 15), (22, -26, 17), (0, -24, 18), (-20, -28, 16), (4, -42, 22)]
    for (dx, dy, rr) in puffs:
        a.ell(x + dx * k + 1.5, y + dy * k + 5 * (k - 1) * 4 + 2, rr * k, rr * k * .94, '#bfb29a')
    for (dx, dy, rr) in puffs:
        a.ell(x + dx * k, y + dy * k + 5 * (k - 1) * 4, rr * k, rr * k * .94, a.rg([(0, '#ffffff'), (.7, wool), (1, '#e2d8c4')]), stroke='#a89a80', sw=.8)
    hx, hy = x - 40, y - 44
    a.ell(hx - 13, hy - 3, 8, 4.6, '#3a2a22', rot=-30); a.ell(hx + 13, hy - 3, 8, 4.6, '#3a2a22', rot=30)
    a.add('<rect x="%.1f" y="%.1f" width="26" height="30" rx="12" fill="#4a3830" stroke="%s" stroke-width="1.6"/>' % (hx - 13, hy - 10, OUTL))
    for (dx, rr) in ((-8, 7), (0, 8), (8, 7)):
        a.ell(hx + dx, hy - 11, rr, rr * .9, '#ffffff', stroke='#a89a80', sw=.8)
    a.ell(hx - 5, hy + 2, 3, 3.4, '#ffffff'); a.ell(hx + 5, hy + 2, 3, 3.4, '#ffffff')
    a.ell(hx - 5, hy + 2.6, 1.6, 1.9, '#1a0e08'); a.ell(hx + 5, hy + 2.6, 1.6, 1.9, '#1a0e08')
    a.ell(hx, hy + 13, 3.4, 2.2, '#ffb0b8')
    if ready:
        for (dx, dy) in ((46, -6), (56, -10), (50, -16)):
            a.ell(x + dx, y + dy, 9, 8, '#ffffff', stroke='#a89a80', sw=.8)


def pig(a, x, y, ready):
    _shadow(a, x, y, 46)
    pink = a.rg([(0, '#ffd2d6'), (.6, '#f6a4b0'), (1, '#d87a8c')])
    for lx in (-24, -10, 12, 26):
        a.path('M%.1f %.1f l0 16' % (x + lx, y - 20), stroke='#8a3a4a', sw=10)
        a.path('M%.1f %.1f l0 14' % (x + lx, y - 20), stroke='#f09aa8', sw=7)
        a.path('M%.1f %.1f l0 3' % (x + lx, y - 4), stroke='#8a4a50', sw=7.4)
    a.path('M%.1f %.1f q10 -6 6 -12 q-6 -4 -6 4 q2 6 10 2' % (x + 40, y - 44), stroke='#d87a8c', sw=3)
    a.add('<rect x="%.1f" y="%.1f" width="84" height="48" rx="23" fill="%s" stroke="#8a3a4a" stroke-width="1.8"/>' % (x - 42, y - 64, pink))
    hx, hy = x - 38, y - 46
    a.path('M%.1f %.1f l-8 -14 l14 4Z M%.1f %.1f l8 -14 l-14 4Z' % (hx - 12, hy - 14, hx + 12, hy - 14), '#f09aa8', '#8a3a4a', 1.4)
    a.ell(hx, hy, 21, 19, pink, stroke='#8a3a4a', sw=1.8)
    a.ell(hx - 2, hy + 6, 10, 7.4, '#ffc2ca', stroke='#8a3a4a', sw=1.4)
    a.ell(hx - 5, hy + 6, 1.8, 2.6, '#8a3a4a'); a.ell(hx + 1.5, hy + 6, 1.8, 2.6, '#8a3a4a')
    _eye(a, hx - 9, hy - 5, 2.8); _eye(a, hx + 7, hy - 5, 2.8)
    if ready:
        bx, by = x + 46, y - 6
        a.ell(bx, by, 13, 10, a.rg([(0, '#8a6a4a'), (.6, '#5a4028'), (1, '#33200e')]), stroke='#22140a', sw=1.2)
        for (dx, dy) in ((-5, -3), (3, -5), (5, 2), (-3, 4), (0, -1)):
            a.ell(bx + dx, by + dy, 1.6, 1.2, '#b08a60', .9)


def bee(a, x, y, ready):
    """A hive on its stand; ready: honey runs from it and a full jar waits."""
    _shadow(a, x, y, 40)
    for lx in (-22, 22):
        a.path('M%.1f %.1f l0 16' % (x + lx, y - 16), stroke=OUTL, sw=7)
        a.path('M%.1f %.1f l0 15' % (x + lx, y - 16), stroke='#a8703f', sw=4.4)
    a.add('<rect x="%.1f" y="%.1f" width="64" height="9" rx="3" fill="#b98454" stroke="%s" stroke-width="1.4"/>' % (x - 32, y - 24, OUTL))
    rows = [(27, 13), (30, 13), (30, 13), (26, 12), (18, 10)]
    yy = y - 24
    for i, (w, h) in enumerate(rows):
        col = '#f2b93a' if i % 2 == 0 else '#e6a528'
        a.add('<rect x="%.1f" y="%.1f" width="%.1f" height="%.1f" rx="%.1f" fill="%s" stroke="#8a5a14" stroke-width="1.4"/>' % (
            x - w, yy - h, 2 * w, h + 1, h * .5, a.lg([(0, lite(col, .3)), (.4, col), (1, dark(col, .3))], 0, 0, 1, 0)))
        yy -= h - 1
    a.ell(x, y - 34, 7, 8, '#5a3410')
    if ready:
        a.path('M%.1f %.1f q2 12 6 12 q4 0 3 -8 q4 14 9 6' % (x - 16, y - 50), stroke='#f6a81e', sw=4.4)
        jx, jy = x + 44, y - 4
        a.add('<rect x="%.1f" y="%.1f" width="24" height="26" rx="6" fill="%s" stroke="#8a5a14" stroke-width="1.4"/>' % (jx - 12, jy - 26, a.lg([(0, '#ffd870'), (1, '#e69a1a')], 0, 0, 1, 0)))
        a.add('<rect x="%.1f" y="%.1f" width="28" height="7" rx="3" fill="#c07a3a" stroke="#6a3a14" stroke-width="1.2"/>' % (jx - 14, jy - 32))
        a.ell(jx - 5, jy - 16, 2.4, 6, '#fff2c0', .8)
    for (dx, dy) in ((-38, -66), (34, -78), (44, -46)):
        bx, by = x + dx, y + dy
        a.ell(bx - 3, by - 5, 5, 3, '#ffffff', .8, rot=-30, stroke='#9ab0c0', sw=.5)
        a.ell(bx + 3, by - 5, 5, 3, '#ffffff', .8, rot=30, stroke='#9ab0c0', sw=.5)
        a.ell(bx, by, 6, 4.4, '#f6c22e', stroke='#3a2408', sw=1)
        a.path('M%.1f %.1f l0 7 M%.1f %.1f l0 6' % (bx - 1.5, by - 3.6, bx + 2, by - 3), stroke='#3a2408', sw=1.6)


ANIMALS = dict(chicken=chicken, cow=cow, sheep=sheep, pig=pig, bee=bee)


def animal(kind, ready):
    a = Art(AW, AH, 66, AH - 12)
    ANIMALS[kind](a, a.ox, a.oy, ready)
    return a


# ---------------------------------------------------------------------------------------------- ground and fences

def ground(a, u0, v0, u1, v1, col, rim='#6fae44', n=60, seed=1, kind='dirt'):
    """A patch of worn ground on the grass: soft irregular edge, crumbs, grass creeping in."""
    r = rnd(seed)
    pts = []
    steps = 9
    for (ua, va, ub, vb) in ((u0, v0, u1, v0), (u1, v0, u1, v1), (u1, v1, u0, v1), (u0, v1, u0, v0)):
        for i in range(steps):
            t = i / steps
            pts.append(a.p(ua + (ub - ua) * t + (r.random() - .5) * .07, va + (vb - va) * t + (r.random() - .5) * .07))
    a.poly(pts, rim, raw=True, op=.55, flt=a.blur(3))
    a.poly(pts, a.lg([(0, lite(col, .12)), (1, dark(col, .1))]), raw=True, flt=a.blur(1.2))
    for _ in range(n):
        x, y = a.p(u0 + .08 + r.random() * (u1 - u0 - .16), v0 + .08 + r.random() * (v1 - v0 - .16))
        if kind == 'straw':
            ang = r.random() * 3.14
            a.line((x, y), (x + math.cos(ang) * 6, y + math.sin(ang) * 2.4), r.choice(['#f2d98a', '#d9b85a', '#fff0b0']), 1.1, .8)
        else:
            a.ell(x, y, 1.6 + r.random() * 2, 1 + r.random(), r.choice([lite(col, .25), dark(col, .2)]), .6)
    for _ in range(int(n * .7)):
        e = r.randrange(4)
        t = r.random()
        u, v = [(u0 + (u1 - u0) * t, v0), (u1, v0 + (v1 - v0) * t), (u0 + (u1 - u0) * t, v1), (u0, v0 + (v1 - v0) * t)][e]
        x, y = a.p(u + (r.random() - .5) * .1, v + (r.random() - .5) * .1)
        for k in (-1, 0, 1):
            a.path('M%.1f %.1f q%.1f -5 %.1f %.1f' % (x + k * 2, y + 2, k, k * 3 + r.random() * 2 - 1, -7 - r.random() * 4), stroke=r.choice(['#5fae3c', '#7cc84c', '#4c9632']), sw=1.6)


def fence(a, pts_uv, col='#e8dcc0', h=20, gap=.25, rails=2):
    """A post-and-rail fence along a ground polyline, far parts first."""
    segs = []
    for (p, q) in zip(pts_uv, pts_uv[1:]):
        n = max(1, int(round(math.hypot(q[0] - p[0], q[1] - p[1]) / gap)))
        for i in range(n):
            segs.append(((p[0] + (q[0] - p[0]) * i / n, p[1] + (q[1] - p[1]) * i / n), (p[0] + (q[0] - p[0]) * (i + 1) / n, p[1] + (q[1] - p[1]) * (i + 1) / n)))
    segs.sort(key=lambda s: -(s[0][0] + s[0][1] + s[1][0] + s[1][1]))
    for (p, q) in segs:
        for k in range(rails):
            z = h * (.4 + .42 * k)
            a.line(a.p(p[0], p[1], z), a.p(q[0], q[1], z), dark(col, .4), 4.4)
            a.line(a.p(p[0], p[1], z + .6), a.p(q[0], q[1], z + .6), col, 2.6)
        for e in (p, q):
            x, y = a.p(*e)
            a.path('M%.1f %.1f l0 %.1f' % (x, y, -h), stroke=dark(col, .45), sw=6)
            a.path('M%.1f %.1f l0 %.1f' % (x - .6, y - .5, -h + 1), stroke=lite(col, .2), sw=3.4)


# ---------------------------------------------------------------------------------------------- pens

PENW, PENH = 500, 330
PEN_U, PEN_V = 2.9, 2.5       # the yard, in tiles


def _hut(a, u0, v0, wu, wv, hz, wallc, roofc, along='v', doorc='#6a4226', plank=False):
    a.shadow(u0, v0, u0 + wu, v0 + wv, .35, .3, 4)
    a.box(u0, v0, u0 + wu, v0 + wv, 0, hz, wallc)
    if plank:
        for i in range(1, int(wv * 6)):
            a.line(a.p(u0, v0 + i / 6, 0), a.p(u0, v0 + i / 6, hz), dark(wallc, .3), 1, .6)
        for i in range(1, int(wu * 6)):
            a.line(a.p(u0 + i / 6, v0, 0), a.p(u0 + i / 6, v0, hz), dark(wallc, .45), 1, .5)
    a.gable(u0, v0, u0 + wu, v0 + wv, hz, hz + 34, roofc, .1, along, wall=wallc)
    a.roof_lines(u0, v0, u0 + wu, v0 + wv, hz, hz + 34, roofc, 4, along, .1)


def pen(kind):
    a = Art(PENW, PENH, PEN_V * A + 14, PENH - 12, scale=1.0)
    U, V = PEN_U, PEN_V
    if kind == 'cowshed':
        ground(a, 0, 0, U, V, '#b99a5e', seed=2, kind='straw', n=90)
        _hut(a, U - 1.25, V - 1.5, 1.2, 1.4, 44, '#c8452f', '#8a8f98', 'v')
        wall(a, 'l', U - 1.25, V - .1, 0, door(42, -40, 36, 40, '#f4ead6') + '<path d="M44 -38 l32 36 M76 -38 l-32 36" stroke="#c8452f" stroke-width="2.4"/>')
        a.box(.5, V - .55, 1.3, V - .3, 0, 12, '#9a6a3a')                                  # trough
        a.poly([(.54, V - .51, 12), (1.26, V - .51, 12), (1.26, V - .34, 12), (.54, V - .34, 12)], '#e6c860')
        for (u, v) in ((U - .45, .5), (U - .2, .85)):                                      # hay bales
            a.cyl(u, v, .2, 0, 22, '#e6c25a', top='#f6dc82')
    elif kind == 'sheepfold':
        ground(a, 0, 0, U, V, '#8fbf52', rim='#7cc04a', seed=3, n=30)
        _hut(a, U - 1.2, V - 1.35, 1.1, 1.25, 36, '#d9c7a2', '#c9a24e', 'u')
        wall(a, 'l', U - 1.2, V - .1, 0, door(38, -34, 30, 34, '#5a3a22', True))
        a.box(.4, V - .5, 1.1, V - .28, 0, 10, '#8a6a4a')
        a.poly([(.44, V - .46, 10), (1.06, V - .46, 10), (1.06, V - .32, 10), (.44, V - .32, 10)], '#7ec0e8')
        for i in range(9):
            x, y = a.p(.3 + (i * .37) % 2.2, .3 + (i * .53) % 1.5)
            for j in range(5):
                q = 6.283 * j / 5
                a.ell(x + math.cos(q) * 2.6, y + math.sin(q) * 2, 2, 1.7, '#ffffff')
            a.ell(x, y, 1.4, 1.2, '#f6d040')
    elif kind == 'pigsty':
        ground(a, 0, 0, U, V, '#a47a52', seed=4, n=70)
        x, y = a.p(1.0, .9)
        a.ell(x, y, 80, 34, '#6e4a30', .9, a.blur(2))
        a.ell(x - 6, y - 2, 60, 24, '#855a3a', .9, a.blur(1.5))
        a.ell(x - 24, y - 8, 16, 5, '#a8805a', .6)
        _hut(a, U - 1.2, V - 1.3, 1.1, 1.2, 30, '#a8703f', '#6f8a5a', 'v', plank=True)
        wall(a, 'l', U - 1.2, V - .1, 0, rect(34, -30, 40, 30, '#3a2414', None, 0, 3))
        a.box(.3, V - .5, 1.2, V - .26, 0, 10, '#7a5a3a')
        a.poly([(.34, V - .46, 10), (1.16, V - .46, 10), (1.16, V - .3, 10), (.34, V - .3, 10)], '#c9a25a')
    else:  # apiary
        ground(a, 0, 0, U, V, '#86c24e', rim='#7cc04a', seed=5, n=20)
        r = rnd(9)
        for i in range(46):
            u, v = .15 + r.random() * (U - .3), .15 + r.random() * (V - .3)
            x, y = a.p(u, v)
            c = r.choice(['#ff8ab0', '#ffd23a', '#ffffff', '#b48cf0', '#ff7a4a'])
            a.line((x, y), (x, y - 7), '#4f9a36', 1.2)
            for j in range(5):
                q = 6.283 * j / 5
                a.ell(x + math.cos(q) * 2.6, y - 8 + math.sin(q) * 2.2, 2.1, 1.9, c)
            a.ell(x, y - 8, 1.4, 1.3, '#f6d040' if c != '#ffd23a' else '#b06a16')
        _hut(a, U - .95, V - 1.0, .8, .85, 26, '#f2e2b4', '#d98a2e', 'u')
        wall(a, 'l', U - .95, V - .12, 0, rect(22, -24, 22, 24, '#6a4226', None, 0, 2))
    post = '#e8dcc0' if kind != 'pigsty' else '#b98454'
    # the fence: the far sides first (behind the yard's things would be right, but the hut covers that corner)
    fence(a, [(0, V), (0, 0), (U * .36, 0)], post)
    fence(a, [(U * .62, 0), (U, 0), (U, V * .42)], post)
    return a


# ---------------------------------------------------------------------------------------------- workshops

WSW, WSH = 300, 300
WS = ['bakery', 'dairy', 'toolshed', 'jam', 'textile', 'sugar_mill']


def _smoke(a, x, y, n=4):
    for i in range(n):
        a.ell(x + 5 * i + (i % 2) * 5, y - 15 * i, 8 + 3.4 * i, 7 + 2.8 * i, '#ffffff', .78 - i * .15, a.blur(1.6))


def _chimney(a, u, v, z0, z1, col='#b8563c', w=.16):
    a.box(u, v, u + w, v + w, z0, z1, col)
    a.box(u - .025, v - .025, u + w + .025, v + w + .025, z1, z1 + 5, dark(col, .2))
    return a.p(u + w / 2, v + w / 2, z1 + 8)


def _pad(a, seed, col='#c9a86a'):
    ground(a, -.12, -.12, 2.0, 2.0, col, seed=seed, n=50)


def workshop(kind):
    a = Art(WSW, WSH, 2.0 * A + 4, WSH - 14, scale=1.6)
    u0, v0, wu, wv = .35, .35, 1.45, 1.45
    if kind == 'bakery':
        _pad(a, 11, '#d2b47a')
        a.shadow(u0, v0, u0 + wu, v0 + wv, .5)
        wc, rc, hz = '#f6e2b6', '#c8553a', 78
        a.box(u0, v0, u0 + wu, v0 + wv, 0, hz, wc)
        a.box(u0 - .001, v0 - .001, u0 + wu, v0 + wv, 0, 9, '#b98a66')
        a.gable(u0, v0, u0 + wu, v0 + wv, hz, hz + 40, rc, .12, 'v', wall=wc)
        a.roof_lines(u0, v0, u0 + wu, v0 + wv, hz, hz + 40, rc, 6, 'v', .12)
        top = _chimney(a, u0 + .22, v0 + .85, hz + 12, hz + 58)
        _smoke(a, top[0], top[1])
        wall(a, 'l', u0, v0 + wv, 0, door(18, -48, 26, 48, '#8a5a34', True) + window(62, -46, 42, 26, glass='#ffd98a')
             + '<path d="M56 -52 l56 0 l6 14 l-68 0 Z" fill="#e8483a"/>' + ''.join(rect(62 + i * 14, -52, 7, 14, '#fff4e0') for i in range(4))
             + '<path d="M50 -38 l68 0" stroke="#a82a20" stroke-width="2"/>')
        wall(a, 'r', u0, v0, 0, window(26, -46, 26, 26, glass='#ffd98a') + window(74, -46, 26, 26, glass='#ffd98a'))
        # a round sign with a loaf, and a crate of bread at the door
        x, y = a.p(u0 - .02, v0 + wv * .78, hz - 6)
        a.ell(x - 4, y, 17, 17, '#fff4dc', stroke='#8a5a34', sw=3)
        a.ell(x - 4, y + 1, 11, 7, a.rg([(0, '#f2c070'), (1, '#b8742a')]), stroke='#7a4416', sw=1)
        for i in (-4, 0, 4):
            a.line((x - 4 + i - 1.6, y - 2), (x - 4 + i + 1.6, y + 3), '#7a4416', 1.2)
        a.box(.02, 1.18, .32, 1.5, 0, 14, '#b98454')
        for (u, v) in ((.09, 1.26), (.2, 1.38), (.12, 1.42)):
            x, y = a.p(u, v, 15)
            a.ell(x, y, 10, 5.5, a.rg([(0, '#f6cc80'), (1, '#b8742a')]), stroke='#7a4416', sw=.8)
    elif kind == 'dairy':
        _pad(a, 12, '#cdbf9a')
        a.shadow(u0, v0, u0 + wu, v0 + wv, .5)
        wc, rc, hz = '#f6f2ea', '#4a82c4', 72
        a.box(u0, v0 + .35, u0 + wu, v0 + wv, 0, hz, wc)
        a.box(u0 - .001, v0 + .349, u0 + wu, v0 + wv, 0, 10, '#9fb8cc')
        a.gable(u0, v0 + .35, u0 + wu, v0 + wv, hz, hz + 36, rc, .1, 'u', wall=wc)
        a.roof_lines(u0, v0 + .35, u0 + wu, v0 + wv, hz, hz + 36, rc, 5, 'u', .1)
        wall(a, 'l', u0, v0 + wv, 0, door(34, -44, 28, 44, '#3f72b0') + '<circle cx="48" cy="-58" r="9" fill="#fff" stroke="#3f72b0" stroke-width="2"/>')
        wall(a, 'r', u0, v0 + .35, 0, window(18, -42, 24, 24) + window(60, -42, 24, 24) + window(100, -42, 24, 24))
        # the silo with its dome, milk cans by the door, a cheese on the sign
        a.cyl(u0 + wu - .34, v0 + .02, .3, 0, 96, '#dfe6ee')
        for z in (26, 52, 78):
            x, y = a.p(u0 + wu - .34, v0 + .02, z)
            a.path('M%.1f %.1f a%.1f %.1f 0 0 0 %.1f 0' % (x - .3 * A, y, .3 * A, .3 * B, .6 * A), stroke='#8fa0b4', sw=1.6)
        x, y = a.p(u0 + wu - .34, v0 + .02, 96)
        a.path('M%.1f %.1f a%.1f %.1f 0 0 1 %.1f 0 Z' % (x - .3 * A, y, .3 * A, .3 * A * .8, .6 * A), a.lg([(0, '#9cc0ea'), (.4, '#5a90d0'), (1, '#2f5f9c')], 0, 0, 1, 0), '#274e80', 1)
        for (u, v) in ((.1, 1.0), (.22, 1.22)):
            a.cyl(u, v, .1, 0, 24, '#c9d2dc', rtop=.07)
            x, y = a.p(u, v, 24)
            a.ell(x, y - 2, 4.4, 2.2, '#eef2f6', stroke='#6a7684', sw=.8)
        x, y = a.p(u0 - .02, v0 + .7, hz - 4)
        a.path('M%.1f %.1f l26 -9 l0 15 l-26 6Z' % (x - 22, y), a.lg([(0, '#ffe07a'), (1, '#f0b62e')]), '#a87410', 1.4)
        a.ell(x - 12, y + 2, 2.6, 2.6, '#d99a1e'); a.ell(x - 3, y - 2, 2, 2, '#d99a1e')
    elif kind == 'toolshed':
        _pad(a, 13, '#b89a6a')
        a.shadow(u0, v0, u0 + wu, v0 + wv, .45)
        wc, rc, hz = '#b98454', '#5f7f68', 62
        a.box(u0, v0, u0 + wu, v0 + wv, 0, hz, wc)
        for i in range(1, 9):
            a.line(a.p(u0, v0 + wv * i / 9, 0), a.p(u0, v0 + wv * i / 9, hz), dark(wc, .3), 1.1, .6)
            a.line(a.p(u0 + wu * i / 9, v0, 0), a.p(u0 + wu * i / 9, v0, hz), dark(wc, .45), 1.1, .5)
        # a lean-to roof: high at the back
        e = dark(rc, .45)
        a.poly([(u0 - .1, v0 - .1, hz - 4), (u0 - .1, v0 + wv + .1, hz - 4), (u0 + wu + .1, v0 + wv + .1, hz + 22), (u0 + wu + .1, v0 - .1, hz + 22)],
               a.lg([(0, lite(rc, .25)), (1, dark(rc, .1))]), e, 1)
        a.poly([(u0, v0, hz), (u0 + wu, v0, hz), (u0 + wu, v0, hz + 21)], dark(wc, .3), dark(wc, .5), .9)
        for i in range(1, 5):
            t = i / 5
            a.line(a.p(u0 - .1 + (wu + .2) * t, v0 - .1, hz - 4 + 26 * t), a.p(u0 - .1 + (wu + .2) * t, v0 + wv + .1, hz - 4 + 26 * t), dark(rc, .3), 1.3, .6)
        wall(a, 'l', u0, v0 + wv, 0, rect(30, -44, 50, 44, '#3a2414', '#2a180c', 1.5, 2) + rect(30, -44, 14, 44, '#8a5a34', '#5a3a1e', 1) + window(94, -40, 20, 20))
        # the grindstone on its trestle, an anvil on a stump, tools against the wall
        x, y = a.p(.12, 1.25)
        a.path('M%.1f %.1f l8 -22 l8 22 M%.1f %.1f l8 -22 l8 22' % (x - 20, y, x + 2, y), stroke='#6a4226', sw=3.6)
        a.ell(x - 1, y - 24, 16, 16, a.rg([(0, '#eef2f6'), (.6, '#aab4c0'), (1, '#6f7b88')]), stroke='#4a5460', sw=1.6)
        a.ell(x - 1, y - 24, 4, 4, '#6a4226')
        a.cyl(.2, .75, .13, 0, 14, '#8a5a34')
        x, y = a.p(.2, .75, 14)
        a.path('M%.1f %.1f l26 0 l-4 6 l-6 0 l0 5 l6 4 l-18 0 l6 -4 l0 -5 l-12 0Z' % (x - 12, y - 16), a.lg([(0, '#8a96a6'), (1, '#4a5460')]), '#2a3440', 1.2)
        for i, c in enumerate(('#c9d2dc', '#e8483a')):
            p, q = a.p(u0 + .5 + i * .2, v0 - .06, 0), a.p(u0 + .56 + i * .2, v0 - .01, 46)
            a.line(p, q, '#8a5a34', 3)
            a.ell(q[0], q[1], 6, 4, c, stroke=dark(c, .5), sw=.8)
    elif kind == 'jam':
        _pad(a, 14, '#d2b48a')
        a.shadow(u0, v0, u0 + wu, v0 + wv, .5)
        wc, rc, hz = '#fbe6dc', '#b83a5e', 74
        a.box(u0, v0, u0 + wu, v0 + wv, 0, hz, wc)
        a.box(u0 - .001, v0 - .001, u0 + wu, v0 + wv, 0, 9, '#c79aa0')
        a.gable(u0, v0, u0 + wu, v0 + wv, hz, hz + 38, rc, .12, 'v', wall=wc)
        a.roof_lines(u0, v0, u0 + wu, v0 + wv, hz, hz + 38, rc, 6, 'v', .12)
        wall(a, 'l', u0, v0 + wv, 0, door(22, -44, 26, 44, '#7a3a50', True) + window(66, -44, 40, 26, glass='#ffe6ee')
             + '<path d="M60 -16 l52 0 l0 6 l-52 0Z" fill="#8a5a34"/>' + ''.join('<circle cx="%d" cy="-20" r="5" fill="%s"/>' % (68 + i * 12, c) for i, c in enumerate(('#e8483a', '#ff8ab0', '#e8483a', '#b83a5e'))))
        wall(a, 'r', u0, v0, 0, window(30, -44, 26, 26, glass='#ffe6ee') + window(78, -44, 26, 26, glass='#ffe6ee'))
        # a giant jar by the door, barrels, a crate of berries
        a.cyl(.14, 1.3, .2, 0, 44, '#e23a52', top='#f06a7a')
        x, y = a.p(.14, 1.3, 44)
        a.ell(x, y - 3, 13, 6.4, '#f6e2b6', stroke='#8a5a34', sw=1.2)
        a.ell(x - 7, y + 20, 3, 12, '#ffffff', .5)
        a.add('<rect x="%.1f" y="%.1f" width="18" height="12" rx="2" fill="#fff4dc" stroke="#8a5a34" stroke-width=".8"/>' % (x - 8, y + 14))
        a.ell(x + 1, y + 20, 3.4, 3.4, '#e23a52')
        for (u, v) in ((.12, .72), (.3, .52)):
            a.cyl(u, v, .13, 0, 26, '#a8703f', top='#c79a5e')
            x, y = a.p(u, v, 13)
            a.path('M%.1f %.1f a%.1f %.1f 0 0 0 %.1f 0' % (x - .13 * A, y, .13 * A, .13 * B, .26 * A), stroke='#5a6674', sw=2)
    elif kind == 'textile':
        _pad(a, 15, '#c4b28e')
        a.shadow(u0, v0, u0 + wu, v0 + wv, .5)
        wc, rc, hz = '#ece2f6', '#7a58b4', 76
        a.box(u0, v0, u0 + wu, v0 + wv, 0, hz, wc)
        a.box(u0 - .001, v0 - .001, u0 + wu, v0 + wv, 0, 10, '#a89ac0')
        a.gable(u0, v0, u0 + wu, v0 + wv, hz, hz + 38, rc, .12, 'u', wall=wc)
        a.roof_lines(u0, v0, u0 + wu, v0 + wv, hz, hz + 38, rc, 6, 'u', .12)
        wall(a, 'l', u0, v0 + wv, 0, door(46, -44, 28, 44, '#5a3f8a') + window(12, -44, 22, 26) + window(88, -44, 22, 26))
        # the big wheel on the shaded wall
        wx, wy, R = 62, -34, 40
        spokes = ''.join('<line x1="%.1f" y1="%.1f" x2="%.1f" y2="%.1f" stroke="#7a5230" stroke-width="3"/>' % (wx, wy, wx + math.cos(q) * R, wy + math.sin(q) * R) for q in [i * math.pi / 4 for i in range(8)])
        wall(a, 'r', u0, v0 - .06, 0, '<circle cx="%s" cy="%s" r="%s" fill="none" stroke="#5a3a1e" stroke-width="9"/><circle cx="%s" cy="%s" r="%s" fill="none" stroke="#b98454" stroke-width="5"/>%s<circle cx="%s" cy="%s" r="7" fill="#5a3a1e"/>'
             % (wx, wy, R, wx, wy, R, spokes, wx, wy))
        # bolts of cloth and balls of yarn at the door
        for i, c in enumerate(('#e8483a', '#f2c230', '#4a9ae0')):
            x, y = a.p(.06 + i * .12, 1.1 + i * .14)
            a.add('<rect x="%.1f" y="%.1f" width="13" height="32" rx="6" fill="%s" stroke="%s" stroke-width="1"/>' % (x - 6, y - 32, a.lg([(0, lite(c, .3)), (1, dark(c, .25))], 0, 0, 1, 0), dark(c, .5)))
            a.ell(x, y - 32, 6.4, 3, lite(c, .4), stroke=dark(c, .5), sw=.8)
        for (u, v, c) in ((.16, .62, '#ff8ab0'), (.3, .5, '#8fd0f0')):
            x, y = a.p(u, v)
            a.ell(x, y - 8, 9, 8.6, a.rg([(0, lite(c, .5)), (1, dark(c, .2))]), stroke=dark(c, .5), sw=.8)
            a.path('M%.1f %.1f q8 -6 14 2 M%.1f %.1f q8 -2 12 6' % (x - 7, y - 10, x - 7, y - 6), stroke=dark(c, .3), sw=1)
    else:  # sugar_mill
        _pad(a, 16, '#cdb07a')
        a.shadow(u0, v0, u0 + wu, v0 + wv, .5)
        wc, rc, hz = '#f6dc8a', '#2f9a94', 72
        a.box(u0, v0 + .3, u0 + wu, v0 + wv, 0, hz, wc)
        a.box(u0 - .001, v0 + .299, u0 + wu, v0 + wv, 0, 9, '#c9a85a')
        a.gable(u0, v0 + .3, u0 + wu, v0 + wv, hz, hz + 36, rc, .1, 'v', wall=wc)
        a.roof_lines(u0, v0 + .3, u0 + wu, v0 + wv, hz, hz + 36, rc, 5, 'v', .1)
        wall(a, 'l', u0, v0 + wv, 0, door(30, -42, 30, 42, '#2a7a76', True) + window(72, -42, 24, 24))
        wall(a, 'r', u0, v0 + .3, 0, window(24, -42, 26, 24) + window(72, -42, 26, 24))
        # the tall brick stack with rings and smoke
        su, sv = u0 + wu - .3, v0 + .0
        a.cyl(su, sv, .2, 0, 150, '#c2674a', rtop=.13)
        for z in (40, 80, 120):
            x, y = a.p(su, sv, z)
            rr = (.2 - .07 * z / 150) * A
            a.path('M%.1f %.1f a%.1f %.1f 0 0 0 %.1f 0' % (x - rr, y, rr, rr * B / A, 2 * rr), stroke='#f2e2c0', sw=2.4)
        x, y = a.p(su, sv, 152)
        a.ell(x, y, .13 * A, .13 * B, '#3a2414')
        _smoke(a, x, y - 4, 4)
        # sacks of sugar and a bundle of cane
        for (u, v) in ((.1, 1.25), (.26, 1.4), (.16, 1.5)):
            x, y = a.p(u, v)
            a.add('<rect x="%.1f" y="%.1f" width="22" height="24" rx="8" fill="%s" stroke="#8a744a" stroke-width="1"/>' % (x - 11, y - 24, a.lg([(0, '#fff8e6'), (1, '#d9c8a0')], 0, 0, 1, 0)))
            a.path('M%.1f %.1f l8 -5 l8 5' % (x - 8, y - 23), stroke='#8a744a', sw=1.4)
        for i in range(5):
            p, q = a.p(.1 + i * .035, .72 + i * .03, 0), a.p(.2 + i * .035, .6 + i * .03, 44)
            a.line(p, q, '#3f8a2c', 4.4)
            a.line(p, q, '#9fd05a', 2.2)
    return a


# ---------------------------------------------------------------------------------------------- small things

def sign(icon_draw=None):
    """A wooden sign on a post, a little turned, to hang an icon on."""
    a = Art(140, 160, 70, 148)
    x, y = a.ox, a.oy
    a.ell(x + 8, y + 1, 26, 8, '#1c2a10', .3, a.blur(3))
    a.path('M%.1f %.1f l0 -70 l10 0 l0 70 Z' % (x - 5, y), a.lg([(0, '#c79a5e'), (1, '#8a5a34')], 0, 0, 1, 0), '#5a3a1e', 1.2)
    a.path('M%.1f %.1f l92 -10 l4 60 l-92 12 Z' % (x - 48, y - 128), '#6a4226', '#4a2a14', 1.4)
    a.path('M%.1f %.1f l84 -9 l3 52 l-84 11 Z' % (x - 44, y - 124), a.lg([(0, '#f2d49a'), (1, '#d9aa62')]), '#8a5a34', 1)
    for i in (1, 2):
        a.line((x - 44 + 1, y - 124 + i * 21), (x + 41, y - 133 + i * 21), '#c08e4e', 1.2, .7)
    for (dx, dy) in ((-39, -119), (35, -127), (-38, -68), (37, -77)):
        a.ell(x + dx, y + dy, 2.2, 2.2, '#8a96a6', stroke='#4a5460', sw=.6)
    for k in (-1, 0, 1, 2):
        a.path('M%.1f %.1f q%.1f -6 %.1f -10' % (x - 8 + k * 6, y + 2, k, k * 3), stroke='#5fae3c', sw=2)
    return a


def ring():
    a = Art(220, 140, 110, 100)
    x, y = a.ox, a.oy - 30
    a.ell(x, y, 96, 56, 'none', .9, a.blur(5), stroke='#fff2a0', sw=12)
    a.ell(x, y, 96, 56, 'none', stroke='#ffffff', sw=5)
    return a


def bubble():
    a = Art(110, 126, 55, 120)
    x = a.ox
    a.ell(x + 3, 60, 46, 46, '#1c2a10', .28, a.blur(4))
    edge, face = '#c9a86a', '#fffaf0'
    a.path('M%.1f 96 l10 22 l12 -24 Z' % (x - 12), edge)
    a.ell(x, 54, 49, 49, edge)
    a.path('M%.1f 94 l8 17 l9 -19 Z' % (x - 9), face)
    a.ell(x, 54, 45, 45, a.rg([(0, '#ffffff'), (1, '#f6ecd4')]))
    a.ell(x - 18, 28, 14, 8, '#ffffff', .9, rot=-30)
    return a


def spark():
    a = Art(64, 64, 32, 32)
    pts = []
    for i in range(8):
        q = math.pi / 4 * i
        rr = 29 if i % 2 == 0 else 7
        pts.append((32 + math.cos(q) * rr, 32 + math.sin(q) * rr))
    a.poly(pts, '#fff6c0', raw=True, flt=a.blur(2.5))
    a.poly(pts, '#ffffff', raw=True)
    return a


def bar(fill):
    a = Art(128, 26, 0, 0, scale=1)
    if fill:
        a.add(rect(4, 4, 120, 18, a.lg([(0, '#ffffff'), (1, '#e6e6e6')]), None, 0, 9))
        a.add(rect(8, 6, 112, 5, '#ffffff', None, 0, 2.5, .7))
    else:
        a.add(rect(0, 0, 128, 26, '#3a2414', None, 0, 13, .9))
        a.add(rect(3, 3, 122, 20, '#6a4a2e', None, 0, 10))
    return a


def sickle():
    a = Art(128, 128, 0, 0)
    a.path('M34 96 q-18 -58 36 -76 q30 -6 44 14 q-22 -12 -44 2 q-30 20 -18 54 Z', '#1c2a10', op=.3, flt=a.blur(3))
    a.path('M26 120 l14 -34', stroke='#5a3410', sw=17)
    a.path('M26 120 l14 -34', stroke='#c8843c', sw=11)
    a.path('M24 117 l5 -12', stroke='#f0b468', sw=4)
    a.path('M32 92 q-18 -58 36 -76 q30 -6 44 14 q-22 -12 -44 2 q-30 20 -18 54 Z', a.lg([(0, '#ffffff'), (.5, '#c2ccd8'), (1, '#6f7b88')], 0, 0, 1, 1), '#3a4654', 3.4)
    a.path('M38 78 q-8 -42 32 -56', stroke='#ffffff', sw=3, op=.9)
    return a


# ---------------------------------------------------------------------------------------------- decorations of the road strip

def tuft(seed):
    a = Art(60, 44, 30, 38)
    r = rnd(seed)
    for i in range(9):
        k = (i - 4) * 5 + r.random() * 3
        c = r.choice(['#4c9632', '#5fae3c', '#7cc84c', '#8fd05a'])
        a.path('M%.1f 38 q%.1f -12 %.1f %.1f' % (30 + k * .5, k * .2, k, -16 - r.random() * 14), stroke=dark(c, .3), sw=4)
        a.path('M%.1f 38 q%.1f -12 %.1f %.1f' % (30 + k * .5, k * .2, k, -16 - r.random() * 14 + 1), stroke=c, sw=2.4)
    return a


def bush(seed, flowers=None):
    a = Art(120, 96, 60, 86)
    r = rnd(seed)
    a.ell(66, 86, 44, 10, '#1c2a10', .3, a.blur(3))
    col = '#4f9a36'
    for (dx, dy, rr) in ((-26, -18, 22), (24, -18, 22), (0, -14, 26), (-12, -40, 22), (14, -42, 21), (0, -52, 16)):
        a.ell(60 + dx + 2, 86 + dy + 3, rr, rr * .9, dark(col, .4))
        a.ell(60 + dx, 86 + dy, rr, rr * .9, a.rg([(0, lite(col, .4)), (.6, col), (1, dark(col, .3))]))
    for i in range(22):
        q, d = r.random() * 6.283, math.sqrt(r.random()) * 40
        x, y = 60 + math.cos(q) * d, 54 + math.sin(q) * d * .7
        if flowers:
            a.ell(x, y, 3.4, 3.2, flowers)
            a.ell(x, y, 1.3, 1.3, '#fff2a0')
        else:
            a.ell(x, y, 4, 2.6, lite(col, .5), .5, rot=r.random() * 90)
    return a


def road():
    """A stretch of dirt road that repeats left to right, with soft grass edges."""
    a = Art(256, 96, 0, 0, scale=1.5)
    r = rnd(21)
    a.add(rect(-10, 20, 276, 56, '#b99a62', None, 0, 0, .55) .replace('/>', ' filter="%s"/>' % a.blur(6)))
    a.add(rect(-10, 27, 276, 42, a.lg([(0, '#e2c88e'), (1, '#c9aa6c')]), None, 0, 0).replace('/>', ' filter="%s"/>' % a.blur(3)))
    for _ in range(70):
        x, y = r.random() * 256, 30 + r.random() * 36
        for dx in (0, 256, -256):
            a.ell(x + dx, y, 2 + r.random() * 3, 1.2 + r.random(), r.choice(['#f0dcaa', '#b08e58', '#d2b47a']), .7)
    for y in (40, 58):
        for dx in (0, 256, -256):
            a.path('M%.1f %.1f q64 -5 128 0 t128 0' % (dx, y), stroke='#b08e58', sw=3, op=.35)
    for _ in range(40):
        x = r.random() * 256
        y = r.choice([24, 72]) + r.random() * 6 - 3
        for dx in (0, 256, -256):
            for k in (-1, 0, 1):
                a.path('M%.1f %.1f q%.1f -5 %.1f -9' % (x + dx + k * 2, y + 3, k, k * 3), stroke=r.choice(['#5fae3c', '#7cc84c', '#4c9632']), sw=1.8)
    return a


def build():
    out = {}
    for k in TREES:
        for st in ('leaf', 'bloom', 'ripe'):
            out['Tree_%s_%s' % (k, st)] = tree(k, st).save('Tree_%s_%s' % (k, st))
    for k in ANIMALS:
        out['Animal_%s' % k] = animal(k, False).save('Animal_%s' % k)
        out['Animal_%s_ready' % k] = animal(k, True).save('Animal_%s_ready' % k)
    for k in ('cowshed', 'sheepfold', 'pigsty', 'apiary'):
        out['Pen_%s' % k] = pen(k).save('Pen_%s' % k)
    for k in WS:
        out['Workshop_%s' % k] = workshop(k).save('Workshop_%s' % k)
    for name, a in (('Sign', sign()), ('Ring', ring()), ('Bubble', bubble()), ('Spark', spark()), ('BarBack', bar(False)), ('BarFill', bar(True)), ('Sickle', sickle()),
                    ('Tuft1', tuft(1)), ('Tuft2', tuft(2)), ('Bush1', bush(1)), ('Bush2', bush(2, '#ff8ab0')), ('Bush3', bush(3, '#ffd23a')), ('Road', road())):
        out[name] = a.save(name)
    return out


if __name__ == '__main__':
    import sys
    from PIL import Image
    out = build()
    names = sys.argv[2].split(',')
    sheet = Image.new("RGBA", (1500, 6000), (124, 186, 78, 255))
    x = y = 8
    rowh = 0
    for n in out:
        if not any(n.startswith(p) for p in names):
            continue
        im = out[n]
        if x + im.width > 1492:
            x, y, rowh = 8, y + rowh + 8, 0
        sheet.alpha_composite(im, (x, y))
        x += im.width + 8
        rowh = max(rowh, im.height)
    sheet.crop((0, 0, 1500, y + rowh + 8)).convert('RGB').save(sys.argv[1], quality=90)
