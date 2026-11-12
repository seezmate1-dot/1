"""Trees, animals, pens, workshops and the small things of the farm map."""
import math
from iso import Art, A, B, L, lite, dark, mix, rnd, wall, rect, window, door, barrel, crate

# ---------------------------------------------------------------------------------------------- trees

TREES = {
    'apple_tree': dict(leaf='#4f9a36', fruit='#e23a2e', blossom='#ffd9e6', shape=(1.0, 1.0)),
    'pear_tree': dict(leaf='#7aa83a', fruit='#f0d23c', blossom='#ffffff', shape=(.84, 1.18)),
    'cherry_tree': dict(leaf='#3f8a4a', fruit='#c2183a', blossom='#ffc2d8', shape=(1.12, .92)),
}
TW, TH = 230, 292


def _leaf(a, x, y, ln, wd, ang, col, vein=None):
    """One leaf: a pointed oval from its stem end, at an angle."""
    t = math.radians(ang)
    dx, dy = math.cos(t), -math.sin(t)
    nx, ny = -dy, dx
    tx, ty = x + dx * ln, y + dy * ln
    mx, my = x + dx * ln * .45, y + dy * ln * .45
    a.path('M%.1f %.1f Q%.1f %.1f %.1f %.1f Q%.1f %.1f %.1f %.1f Z' % (x, y, mx + nx * wd, my + ny * wd, tx, ty, mx - nx * wd, my - ny * wd, x, y), col)
    if vein:
        a.line((x, y), (x + dx * ln * .85, y + dy * ln * .85), vein, .35, .7)


def tree(kind, state):
    """state: 'leaf' (after the harvest), 'bloom' (half way), 'ripe' (fruit on it)."""
    t = TREES[kind]
    a = Art(TW, TH, TW / 2 - 8, TH - 30, scale=3.5)
    a.seed = len(kind) + len(state)
    x, y = a.ox, a.oy
    r = rnd(len(kind) * 7 + 1)
    # the crown's shadow on the grass to the right, and the dark right at the foot
    a.ell(x + 30, y + 3, 66, 15, '#0e2010', .42, a.blur(5))
    a.ell(x + 6, y + 1, 28, 9, '#0e2010', .45, a.blur(3))
    for k in range(-6, 7):
        a.path('M%.1f %.1f q%.1f -4 %.1f -8' % (x + k * 3.4, y + 3, k * .3, k * 1.2 + r.random() * 2 - 1), stroke=r.choice(['#3f8a2c', '#5fae3c', '#7cc84c']), sw=1.2)
    # the trunk: flaring roots, furrowed bark, limbs that reach into the crown
    bark = '#7a5236'
    g = a.lg([(0, lite(bark, .3)), (.4, bark), (1, dark(bark, .55))], 0, 0, 1, 0)
    a.path('M%.1f %.1f q-9 -3 -16 4 q11 1 14 -2 q4 -34 -1 -70 l17 0 q-4 36 3 70 q5 4 15 2 q-7 -8 -15 -4 q-9 4 -17 0Z' % (x - 8.5, y), g, dark(bark, .6), .5)
    sx, sy = t['shape']
    cx, cy, R = x, y - 132 * sy + (0 if sy <= 1 else 8), 62
    limbs = [(-46, 8, 7), (44, 10, 6.5), (-22, -30, 5.5), (24, -34, 5), (0, -20, 6), (-58, -14, 4), (58, -10, 4)]
    for (dx, dy, wdt) in limbs:
        ex, ey = cx + dx * sx, cy + dy * sy
        a.path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (x, y - 62, x + dx * .25, (y - 62 + ey) / 2 - 6, ex, ey), stroke=dark(bark, .5), sw=wdt + 1.2)
        a.path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (x - .8, y - 62, x + dx * .25 - .8, (y - 62 + ey) / 2 - 6, ex - .8, ey), stroke=mix(bark, '#a87a52', .3), sw=wdt * .55)
    for i in range(22):
        bx = x - 7 + r.random() * 14
        by = y - 4 - r.random() * 60
        a.path('M%.1f %.1f q%.1f -6 0 -%.1f' % (bx, by, r.random() * 3 - 1.5, 8 + r.random() * 8), stroke=dark(bark, .6), sw=.7, op=.8)
        a.path('M%.1f %.1f q%.1f -5 0 -%.1f' % (bx - 1.6, by - 1, r.random() * 2 - 1, 6 + r.random() * 5), stroke=lite(bark, .45), sw=.5, op=.7)
    # the crown: masses of shade first, then leaves in clusters — every leaf its own shape, lit from the top left
    leafc = t['leaf']
    ramp = [mix(leafc, '#0a2416', .8), mix(leafc, '#14401e', .5), leafc, mix(leafc, '#d6ea58', .46), mix(leafc, '#f6f884', .78)]
    balls = [(0, -52, .56), (-26, -34, .62), (28, -36, .62), (-52, -6, .56), (54, -4, .56), (0, -2, .8), (-40, 22, .62), (42, 24, .62), (0, 34, .66)]
    for (dx, dy, k) in balls:
        a.ell(cx + dx * sx + 3, cy + dy * sy + 4, R * k * .92, R * k * .86, mix(ramp[0], ramp[1], .55))
    for (dx, dy, k) in balls:
        px, py, rr = cx + dx * sx, cy + dy * sy, R * k
        clumps = []
        for _ in range(int(86 * k * k / .4)):
            ang, d = r.random() * 6.283, math.sqrt(r.random()) * rr
            ox, oy = math.cos(ang) * d, math.sin(ang) * d * .94
            lit = (-ox * .55 - oy * .8) / rr
            low = (py + oy - cy) / (R * 1.3)
            tone = lit * 2.1 - low * 1.0 + (r.random() - .5) * .8 + 1.7
            clumps.append((max(0, min(4, int(round(tone)))), px + ox, py + oy, d / rr, ang))
        clumps.sort(key=lambda c: c[0])
        for (ti, qx, qy, edge, ang) in clumps:
            n = r.choice([3, 4, 5]) if edge < .8 else 4
            base = r.random() * 360
            for i in range(n):
                # leaves fan out of the cluster's middle; those at the crown's edge point outwards
                la = base + i * 360 / n + r.random() * 30 if edge < .8 else -math.degrees(ang) + (i - 1.5) * 34 + r.random() * 16
                _leaf(a, qx + (r.random() - .5) * 3, qy + (r.random() - .5) * 3, 7 + r.random() * 5, 2.8 + r.random() * 1.6, la, ramp[ti] if i else ramp[max(0, ti - 1)], vein=ramp[min(4, ti + 1)] if ti >= 2 else None)
    # dark gaps where one looks into the crown, and a limb showing through
    for _ in range(12):
        ang, d = r.random() * 6.283, (.3 + r.random() * .5) * R
        gx, gy = cx + math.cos(ang) * d * sx, cy + math.sin(ang) * d * sy + 12
        a.ell(gx, gy, 3.6 + r.random() * 3, 2.2 + r.random() * 1.6, ramp[0], .8, rot=r.random() * 90)
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
OUTL = '#5e3c22'


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
    a.ell(x, y - 38, 30, 27, body, stroke=OUTL, sw=1.1)
    a.path('M%.1f %.1f q14 4 22 -8' % (x - 2, y - 34), stroke='#c9b89a', sw=2.4)                                 # wing
    a.ell(x - 22, y - 66, 17, 16, body, stroke=OUTL, sw=1.1)
    a.path('M%.1f %.1f q4 -10 9 -3 q5 -8 9 0 q5 -5 6 4Z' % (x - 34, y - 78), '#e8362e', '#8a1410', 1.2)          # comb
    a.path('M%.1f %.1f l-13 4 l12 5Z' % (x - 36, y - 66), '#f6a81e', '#a8600a', 1.2)                             # beak
    a.path('M%.1f %.1f q-1 8 3 9 q4 -2 2 -9Z' % (x - 32, y - 58), '#e8362e', '#8a1410', 1)
    _eye(a, x - 26, y - 69)
    a.ell(x - 18, y - 62, 4, 2.6, '#ffb0b0', .7)
    # feathers: rows on the wing, the tail's quills, a ruff on the neck; the belly turns into shade
    a.ell(x + 2, y - 19, 18, 6, '#b8a888', .4)
    for row in range(3):
        for i in range(4 - row):
            fx, fy = x - 4 + i * 7 + row * 3, y - 42 + row * 6.5
            a.path('M%.1f %.1f q4.5 6 9 0' % (fx, fy), stroke='#b09a78', sw=1.1)
            a.path('M%.1f %.1f q4.5 5 9 0' % (fx, fy - 1), stroke='#ffffff', sw=.6, op=.8)
    for i in range(4):
        a.path('M%.1f %.1f q%.1f -7 %.1f -15' % (x + 24 + i * 3, y - 46, 3 + i, 3 + i * 2.4), stroke='#b09a78', sw=1)
    for i in range(5):
        a.path('M%.1f %.1f q3 4 6 0' % (x - 30 + i * 5, y - 54 + abs(i - 2) * 1.2), stroke='#c9b89a', sw=.9)
    for lx in (-8, 8):
        for k in range(3):
            a.line((x + lx - 1.6, y - 12 + k * 3), (x + lx + 1.6, y - 12 + k * 3), '#a8600a', .6)
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
    a.add('<rect x="%.1f" y="%.1f" width="88" height="46" rx="21" fill="%s" stroke="%s" stroke-width="1.1"/>' % (x - 42, y - 70, body, OUTL))
    for (dx, dy, rx, ry) in ((10, -56, 13, 9), (-12, -40, 9, 7), (30, -40, 8, 9)):
        a.ell(x + dx, y + dy, rx, ry, '#5a3a26')
    a.ell(x + 6, y - 25, 12, 6, '#ffc0c8', stroke='#c07080', sw=1)                                              # udder
    # the head
    hx, hy = x - 42, y - 62
    a.path('M%.1f %.1f q-8 -10 -2 -16 M%.1f %.1f q8 -10 2 -16' % (hx - 12, hy - 16, hx + 12, hy - 16), stroke='#e8d8b0', sw=5)
    a.ell(hx - 20, hy - 10, 9, 5.4, '#5a3a26', rot=-25, stroke=OUTL, sw=1.2)
    a.ell(hx + 20, hy - 10, 9, 5.4, '#f4efe6', rot=25, stroke=OUTL, sw=1.2)
    a.add('<rect x="%.1f" y="%.1f" width="36" height="40" rx="15" fill="%s" stroke="%s" stroke-width="1.1"/>' % (hx - 18, hy - 22, body, OUTL))
    a.ell(hx + 8, hy - 10, 9, 10, '#5a3a26')
    a.ell(hx, hy + 9, 16, 10, '#ffc8cc', stroke=OUTL, sw=1.5)
    a.ell(hx - 6, hy + 9, 2, 3, '#b06a78'); a.ell(hx + 6, hy + 9, 2, 3, '#b06a78')
    _eye(a, hx - 8, hy - 7); _eye(a, hx + 8, hy - 7)
    # the belly in shade, the back lit, hooves, the tail's tuft, hair on the poll, the ear's inside
    a.ell(x + 2, y - 31, 30, 5, '#9a8f80', .4)
    for lx in (-30, -14, 14, 30):
        a.path('M%.1f %.1f l0 4.6' % (x + lx, y - 6), stroke='#2a1c14', sw=8.4)
        a.line((x + lx - 3, y - 6.5), (x + lx + 3, y - 6.5), '#8a7a66', .8)
        a.line((x + lx - 2.4, y - 22), (x + lx - 2.4, y - 8), '#ffffff', 1, .7)
    a.path('M%.1f %.1f q5 4 2 10 q-4 2 -5 -4Z' % (x + 52, y - 44), '#5a3a26', '#2a1c14', .6)
    for k in range(4):
        a.path('M%.1f %.1f q2 -5 %.1f -7' % (hx - 5 + k * 3.2, hy - 20, k - 1.5), stroke='#e8dcc8', sw=1.1)
    a.ell(hx + 20, hy - 10, 5, 2.6, '#ffc8cc', rot=25)
    a.ell(hx - 9, hy + 5, 4, 2, '#ffffff', .7)
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
    # curls of wool all over, the fleece in shade underneath, little hooves
    rw = rnd(3)
    for (dx, dy, rr) in puffs:
        for _ in range(7):
            q, d = rw.random() * 6.283, rw.random() * rr * k * .75
            cx_, cy_ = x + dx * k + math.cos(q) * d, y + dy * k + 5 * (k - 1) * 4 + math.sin(q) * d * .9
            if cx_ > hx + 20:
                a.path('M%.1f %.1f q3 -3.4 6 0 q-1 2.6 -3.4 1.6' % (cx_ - 3, cy_), stroke='#c9bca4', sw=.8)
    for lx in (-22, -8, 10, 24):
        a.path('M%.1f %.1f l0 3.6' % (x + lx, y - 5), stroke='#16100c', sw=6.9)
        a.line((x + lx - 1.6, y - 20), (x + lx - 1.6, y - 8), '#6a5a50', .8)
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
    a.add('<rect x="%.1f" y="%.1f" width="84" height="48" rx="23" fill="%s" stroke="#8a3a4a" stroke-width="1.1"/>' % (x - 42, y - 64, pink))
    hx, hy = x - 38, y - 46
    a.path('M%.1f %.1f l-8 -14 l14 4Z M%.1f %.1f l8 -14 l-14 4Z' % (hx - 12, hy - 14, hx + 12, hy - 14), '#f09aa8', '#8a3a4a', 1.4)
    a.ell(hx, hy, 21, 19, pink, stroke='#8a3a4a', sw=1.1)
    a.ell(hx - 2, hy + 6, 10, 7.4, '#ffc2ca', stroke='#8a3a4a', sw=1.4)
    a.ell(hx - 5, hy + 6, 1.8, 2.6, '#8a3a4a'); a.ell(hx + 1.5, hy + 6, 1.8, 2.6, '#8a3a4a')
    _eye(a, hx - 9, hy - 5, 2.8); _eye(a, hx + 7, hy - 5, 2.8)
    # the belly in shade, a lit back, bristles, the ears' insides, the snout's rim, cloven hooves
    a.ell(x + 6, y - 22, 28, 4.6, '#b85a70', .38)
    rp = rnd(4)
    for _ in range(26):
        bx_, by_ = x - 10 + rp.random() * 42, y - 54 + rp.random() * 22
        a.line((bx_, by_), (bx_ + 2.4, by_ + 1.6), '#e88a9c', .6, .8)
    a.path('M%.1f %.1f l-4 -8 l7 2.4Z M%.1f %.1f l4 -8 l-7 2.4Z' % (hx - 13, hy - 15, hx + 13, hy - 15), '#ffc2ca')
    a.path('M%.1f %.1f q8 -5 16 0' % (hx - 10, hy + 1), stroke='#ffe6ea', sw=1.2)
    a.ell(hx - 13, hy + 3, 4.6, 3, '#ff9aa8', .6); a.ell(hx + 11, hy + 3, 4.6, 3, '#ff9aa8', .6)
    for lx in (-24, -10, 12, 26):
        a.line((x + lx, y - 4.5), (x + lx, y - 1), '#4a2a30', 1)
        a.line((x + lx - 2.4, y - 18), (x + lx - 2.4, y - 7), '#ffd2d8', .9, .8)
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
    # the coils are twisted straw: slanting strands, a lit top to every coil; a landing board
    yy = y - 24
    rs = rnd(8)
    for i, (w, h) in enumerate(rows):
        for k in range(int(w / 2.2)):
            sx_ = x - w + 3 + k * 4.4 + rs.random()
            a.line((sx_, yy - 2), (sx_ + 3, yy - h + 2), '#a8700e', .6, .8)
        a.path('M%.1f %.1f h%.1f' % (x - w + 5, yy - h + 2.6, 2 * w - 14), stroke='#ffe08a', sw=1.2, op=.8)
        yy -= h - 1
    a.add('<rect x="%.1f" y="%.1f" width="22" height="3" rx="1" fill="#8a5a24" stroke="#4a2a14" stroke-width=".5"/>' % (x - 11, y - 27))
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

def ground(a, u0, v0, u1, v1, col, rim='#5c9a3a', n=60, seed=1, kind='dirt'):
    """A patch of worn ground on the grass, drawn sharp: an irregular edge where the turf ends, pebbles and
    crumbs (or straws) with a light and a dark side, tufts of grass along the edge."""
    r = rnd(seed)
    pts, outer = [], []
    steps = 11
    for (ua, va, ub, vb) in ((u0, v0, u1, v0), (u1, v0, u1, v1), (u1, v1, u0, v1), (u0, v1, u0, v0)):
        for i in range(steps):
            t = i / steps
            ju, jv = (r.random() - .5) * .06, (r.random() - .5) * .06
            pts.append(a.p(ua + (ub - ua) * t + ju, va + (vb - va) * t + jv))
            cu, cv = (u0 + u1) / 2, (v0 + v1) / 2
            ou, ov = ua + (ub - ua) * t + ju, va + (vb - va) * t + jv
            outer.append(a.p(cu + (ou - cu) * 1.045, cv + (ov - cv) * 1.045))
    a.poly(outer, rim, raw=True)
    a.poly(pts, a.lg([(0, lite(col, .14)), (1, dark(col, .12))]), raw=True)
    for _ in range(n):
        x, y = a.p(u0 + .08 + r.random() * (u1 - u0 - .16), v0 + .08 + r.random() * (v1 - v0 - .16))
        if kind == 'straw':
            ang = r.random() * 3.14
            c = r.choice(['#f2d98a', '#d9b85a', '#fff0b0', '#c9a240'])
            a.line((x, y), (x + math.cos(ang) * 7, y + math.sin(ang) * 2.8), dark(c, .4), 1.1)
            a.line((x, y - .4), (x + math.cos(ang) * 7, y - .4 + math.sin(ang) * 2.8), c, .7)
        else:
            rr = 1 + r.random() * 2
            c = r.choice([lite(col, .3), dark(col, .25), '#b8b0a4', '#9a8f80'])
            a.ell(x + .5, y + .6, rr, rr * .6, dark(col, .5), .8)
            a.ell(x, y, rr, rr * .6, c)
            a.ell(x - rr * .3, y - rr * .2, rr * .4, rr * .22, lite(c, .5), .8)
    for _ in range(int(n * .9)):
        e = r.randrange(4)
        t = r.random()
        u, v = [(u0 + (u1 - u0) * t, v0), (u1, v0 + (v1 - v0) * t), (u0 + (u1 - u0) * t, v1), (u0, v0 + (v1 - v0) * t)][e]
        x, y = a.p(u + (r.random() - .5) * .08, v + (r.random() - .5) * .08)
        for k in (-1.5, -.5, .5, 1.5):
            c = r.choice(['#3f8a2c', '#5fae3c', '#7cc84c', '#4c9632'])
            a.path('M%.1f %.1f q%.1f -5 %.1f %.1f' % (x + k * 1.6, y + 2, k * .6, k * 2.6 + r.random() * 2 - 1, -7 - r.random() * 5), stroke=c, sw=1.3)


def fence(a, pts_uv, col='#e8dcc0', h=20, gap=.25, rails=2):
    """A post-and-rail fence along a ground polyline, far parts first: squared posts with caps, rails with the
    grain of the wood, a thin shadow on the ground."""
    r = rnd(int(pts_uv[0][0] * 31 + pts_uv[0][1] * 17) + 3)
    segs = []
    for (p, q) in zip(pts_uv, pts_uv[1:]):
        n = max(1, int(round(math.hypot(q[0] - p[0], q[1] - p[1]) / gap)))
        for i in range(n):
            segs.append(((p[0] + (q[0] - p[0]) * i / n, p[1] + (q[1] - p[1]) * i / n), (p[0] + (q[0] - p[0]) * (i + 1) / n, p[1] + (q[1] - p[1]) * (i + 1) / n)))
    segs.sort(key=lambda s: -(s[0][0] + s[0][1] + s[1][0] + s[1][1]))
    for (p, q) in segs:
        g0, g1 = a.p(p[0], p[1]), a.p(q[0], q[1])
        a.line((g0[0] + 4, g0[1] + 1.5), (g1[0] + 4, g1[1] + 1.5), '#0e2010', 2.2, .28)
        for k in range(rails):
            z = h * (.36 + .42 * k)
            p0, p1 = a.p(p[0], p[1], z), a.p(q[0], q[1], z)
            a.line(p0, p1, dark(col, .5), 4.2)
            a.line((p0[0], p0[1] - .5), (p1[0], p1[1] - .5), col, 3)
            a.line((p0[0], p0[1] - 1.5), (p1[0], p1[1] - 1.5), lite(col, .45), .8)
            for _ in range(2):
                t = r.random() * .7
                a.line((p0[0] + (p1[0] - p0[0]) * t, p0[1] + (p1[1] - p0[1]) * t), (p0[0] + (p1[0] - p0[0]) * (t + .25), p0[1] + (p1[1] - p0[1]) * (t + .25) + .3), dark(col, .45), .4, .8)
        for e in (p, q):
            x, y = a.p(*e)
            a.add(rect(x - 2.6, y - h, 5.2, h, a.lg([(0, lite(col, .25)), (.55, col), (1, dark(col, .45))], 0, 0, 1, 0), dark(col, .6), .4, .6))
            a.path('M%.1f %.1f l2.6 -2.6 l2.6 2.6 Z' % (x - 2.6, y - h), lite(col, .4), dark(col, .5), .4)
            a.line((x - .6, y - h + 4), (x - .4, y - 3), dark(col, .4), .4, .7)


PENW, PENH = 500, 330
PEN_U, PEN_V = 2.9, 2.5       # the yard, in tiles


def _hut(a, u0, v0, wu, wv, hz, wallc, roofc, along='v', tex='planks', left='', right='', trim='#f2e2c0'):
    """An animal house: a small relative of the workshops — a stone footing, walls, its door, a shingled roof."""
    a.shadow(u0, v0, u0 + wu, v0 + wv, .6, .42, 3)
    a.box(u0, v0, u0 + wu, v0 + wv, 0, hz, wallc, tex=tex, seed=int(hz))
    a.box(u0 - .012, v0 - .012, u0 + wu, v0 + wv, 0, 5, '#9a9086', tex='stone', seed=int(hz) + 1, top=False)
    wall(a, 'l', u0, v0 + wv, 0, left)
    wall(a, 'r', u0, v0, 0, right)
    for (pu, pv) in ((u0, v0 + wv), (u0, v0), (u0 + wu, v0)):
        p0, p1 = a.p(pu, pv, 5), a.p(pu, pv, hz)
        a.line(p0, p1, dark(trim, .5), 3.6)
        a.line((p0[0] - .4, p0[1]), (p1[0] - .4, p1[1]), trim, 2.2)
    a.gable(u0, v0, u0 + wu, v0 + wv, hz, hz + 32, roofc, .1, along, wall=wallc, trim=trim, tex=tex, thick=4.5)


def _trough(a, u0, v0, u1, v1, fill, h=11):
    """A plank trough with what is in it (water with a glint, feed)."""
    x, y = a.p((u0 + u1) / 2, (v0 + v1) / 2)
    a.ell(x + 8, y + 3, (u1 - u0) * A * .7, 8, '#0e2010', .35, a.blur(2))
    a.box(u0, v0, u1, v1, 0, h, '#9a6a3a', tex='planks', seed=4, top='#5a3a1e')
    i = .035
    a.poly([(u0 + i, v0 + i, h - 2), (u1 - i, v0 + i, h - 2), (u1 - i, v1 - i, h - 2), (u0 + i, v1 - i, h - 2)], fill)
    if fill.startswith('#7'):
        a.line(a.p(u0 + .12, v0 + .08, h - 2), a.p(u0 + .4, v0 + .08, h - 2), '#e6f6ff', 1.2, .9)
        a.line(a.p(u0 + .5, v1 - .1, h - 2), a.p(u0 + .66, v1 - .1, h - 2), '#e6f6ff', .9, .8)
    for (pu, pv) in ((u0, v0), (u1, v0), (u0, v1)):
        a.line(a.p(pu, pv, 0), a.p(pu, pv, h + 1), '#4a2a14', 2.2)


def _bale(a, u, v, r=.2, h=22):
    """A round bale of hay, standing: straw strands around it, a spiral on its top, a band."""
    xx, yy = a.p(u, v)
    a.ell(xx + 6, yy + 2, r * A * 1.2, r * B * 1.2, '#0e2010', .38, a.blur(1.6))
    a.cyl(u, v, r, 0, h, '#e2ba4e', top='#f6dc82')
    rr = rnd(int(u * 40))
    for _ in range(46):
        t = rr.random() * 1.9 - .95
        z = rr.random() * h
        x, y = xx + r * A * t, yy - z + r * B * math.sqrt(1 - t * t)
        a.line((x, y), (x + rr.random() * 2 - 1, y - 3 - rr.random() * 4), rr.choice(['#fff0a8', '#b8901e', '#f6d264', '#8a6a14']), .55)
    x, y = a.p(u, v, h)
    for k in (.8, .55, .3):
        a.ell(x, y, r * A * k, r * B * k, 'none', stroke='#c9a030', sw=.6)
    a.path('M%.1f %.1f a%.1f %.1f 0 0 0 %.1f 0' % (xx - r * A, yy - h * .5, r * A, r * B, 2 * r * A), stroke='#7a5a24', sw=1.1)


def pen(kind):
    a = Art(PENW, PENH, PEN_V * A + 14, PENH - 12, scale=2.04)
    a.seed = len(kind)
    U, V = PEN_U, PEN_V
    r = rnd(len(kind) * 3)
    if kind == 'cowshed':
        ground(a, 0, 0, U, V, '#b99a5e', seed=2, kind='straw', n=150)
        _hut(a, U - 1.25, V - 1.5, 1.2, 1.4, 46, '#b8432e', '#7f8690', 'v', 'planks',
             left=door(40, -42, 38, 37, '#f4ead6', frame='#fff6e6') + '<path d="M42 -40 l34 33 M76 -40 l-34 33" stroke="#b8432e" stroke-width="2.6"/>' + window(92, -36, 16, 16, frame='#fff6e6'),
             right=window(30, -36, 16, 16, frame='#fff6e6') + window(68, -36, 16, 16, frame='#fff6e6'), trim='#fff6e6')
        _trough(a, .5, V - .55, 1.3, V - .3, '#e6c860')
        _bale(a, U - .45, .5); _bale(a, U - .2, .86); _bale(a, U - .62, .22, .17, 18)
        # a pitchfork against the bales, a milk churn
        p0, p1 = a.p(U - .78, .62, 0), a.p(U - .6, .56, 40)
        a.line(p0, p1, '#8a5a34', 2)
        for k in (-3, 0, 3):
            a.line((p1[0] + k, p1[1] + 1), (p1[0] + k * 1.2, p1[1] - 9), '#9aa0a8', 1)
    elif kind == 'sheepfold':
        ground(a, 0, 0, U, V, '#86be4e', rim='#5c9a3a', seed=3, n=20)
        for _ in range(140):
            x, y = a.p(.1 + r.random() * (U - .2), .1 + r.random() * (V - .2))
            c = r.choice(['#4c9632', '#5fae3c', '#9ad25a', '#3f8a2c'])
            a.path('M%.1f %.1f q%.1f -4 %.1f -7' % (x, y, r.random() * 2 - 1, r.random() * 4 - 2), stroke=c, sw=1)
        _hut(a, U - 1.2, V - 1.35, 1.1, 1.25, 38, '#e2d2ae', '#b8903e', 'u', 'timber',
             left=door(36, -36, 30, 31, '#5a3a22', True), right=window(34, -32, 16, 15) + window(70, -32, 16, 15))
        _trough(a, .4, V - .5, 1.1, V - .28, '#7ec0e8', 10)
        for i in range(14):
            x, y = a.p(.3 + (i * .37) % 2.2, .3 + (i * .53) % 1.5)
            a.line((x, y), (x, y - 6), '#4c9632', .9)
            for j in range(5):
                q = 6.283 * j / 5
                a.ell(x + math.cos(q) * 2.4, y - 7 + math.sin(q) * 2, 1.9, 1.6, '#ffffff', stroke='#c9c0b0', sw=.3)
            a.ell(x, y - 7, 1.3, 1.1, '#f6d040')
        # a salt lick and a heap of fleece
        barrel(a, U - .3, .4, .11, 12, '#9a6a3a')
    elif kind == 'pigsty':
        ground(a, 0, 0, U, V, '#a47a52', seed=4, n=110)
        x, y = a.p(1.0, .9)
        a.ell(x, y, 78, 33, '#5e3e28')
        a.ell(x - 4, y - 2, 70, 28, '#7a5236')
        a.ell(x - 8, y - 3, 52, 19, '#8a6040')
        for (dx, dy, rx) in ((-24, -8, 16), (20, 4, 12), (-6, 10, 9)):
            a.ell(x + dx, y + dy, rx, rx * .3, '#b08a64', .9)
            a.ell(x + dx - 3, y + dy - 1, rx * .4, rx * .1, '#d8b890', .9)
        for _ in range(26):
            q, d = r.random() * 6.283, .8 + r.random() * .25
            a.ell(x + math.cos(q) * 78 * d, y + math.sin(q) * 33 * d, 2.4 + r.random() * 2, 1.2 + r.random(), '#5e3e28', .9)
        _hut(a, U - 1.2, V - 1.3, 1.1, 1.2, 32, '#a8703f', '#6f8a5a', 'v', 'planks',
             left=rect(30, -31, 44, 26, '#2a1810', '#5a3a1e', 1.4, 3) + rect(30, -31, 44, 3, '#7a5230'), right=window(40, -28, 14, 12, frame='#e8d8b0'), trim='#c79a5e')
        _trough(a, .3, V - .5, 1.2, V - .26, '#c9a25a', 10)
        barrel(a, U - .28, .36, .12, 20)
    else:  # apiary
        ground(a, 0, 0, U, V, '#86c24e', rim='#5c9a3a', seed=5, n=14)
        for i in range(70):
            u, v = .12 + r.random() * (U - .24), .12 + r.random() * (V - .24)
            x, y = a.p(u, v)
            c = r.choice(['#ff8ab0', '#ffd23a', '#ffffff', '#b48cf0', '#ff7a4a'])
            a.path('M%.1f %.1f q%.1f -4 0 -8' % (x, y, r.random() * 2 - 1), stroke='#3f8a2c', sw=1)
            a.path('M%.1f %.1f q3 -1 4 -4' % (x, y - 3), stroke='#5fae3c', sw=.9)
            for j in range(5):
                q = 6.283 * j / 5
                a.ell(x + math.cos(q) * 2.5, y - 9 + math.sin(q) * 2.1, 2, 1.8, c, stroke=dark(c, .3), sw=.25)
            a.ell(x, y - 9, 1.3, 1.2, '#f6d040' if c != '#ffd23a' else '#b06a16')
        _hut(a, U - .95, V - 1.0, .8, .85, 28, '#f6e6b8', '#d98a2e', 'u', 'timber', left=door(18, -26, 22, 21, '#6a4226'), right=window(22, -24, 14, 12))
        crate(a, U - .5, .3, .26, 12)
    post = '#e8dcc0' if kind != 'pigsty' else '#b98454'
    fence(a, [(0, V), (0, 0), (U * .36, 0)], post)
    fence(a, [(U * .62, 0), (U, 0), (U, V * .42)], post)
    # the gate stands open
    g0 = a.p(U * .36, 0)
    a.poly([(g0[0], g0[1] - 4), (g0[0] + 26, g0[1] + 9), (g0[0] + 26, g0[1] - 9), (g0[0], g0[1] - 20)], 'none', dark(post, .3), 2.6, raw=True)
    a.line((g0[0], g0[1] - 20), (g0[0] + 26, g0[1] + 9), dark(post, .3), 1.6)
    return a


WSW, WSH = 344, 320
WS = ['bakery', 'dairy', 'toolshed', 'jam', 'textile', 'sugar_mill']


def _smoke(a, x, y, n=5):
    """Puffs of smoke, each with a lit side: they thin out as they rise."""
    for i in range(n):
        px, py, rr = x + 5 * i + (i % 2) * 6, y - 13 * i, 6 + 2.6 * i
        op = .85 - i * .15
        a.ell(px + 1.2, py + 1.4, rr, rr * .85, '#b8c0c8', op)
        a.ell(px, py, rr, rr * .85, '#f4f6f8', op)
        a.ell(px - rr * .3, py - rr * .3, rr * .45, rr * .35, '#ffffff', op)


def _chimney(a, u, v, z0, z1, col='#b8563c', w=.17):
    a.box(u, v, u + w, v + w, z0, z1, col, tex='brick')
    a.box(u - .03, v - .03, u + w + .03, v + w + .03, z1, z1 + 4.5, '#8a8078', tex='stone')
    a.box(u + .03, v + .03, u + w - .03, v + w - .03, z1 + 4.5, z1 + 9, dark(col, .25), top='#2a1c14')
    return a.p(u + w / 2, v + w / 2, z1 + 12)


def _pad(a, seed, col='#c9a86a'):
    ground(a, -.12, -.12, 2.0, 2.0, col, seed=seed, n=80)


def house(a, u0, v0, wu, wv, hz, rise, wc, rc, along, tex='timber', trim='#f6ead0', plinth='#a89a8a', left='', right='', seed=0, over=.12):
    """A workshop's body: a stone foundation, walls of their material with corner posts, what is on the two walls
    that look at the viewer (doors, windows, signs: flat drawings, origin at the wall's foot), and the roof."""
    a.shadow(u0, v0, u0 + wu, v0 + wv, .85)
    a.box(u0, v0, u0 + wu, v0 + wv, 0, hz, wc, tex=tex, seed=seed)
    a.box(u0 - .012, v0 - .012, u0 + wu, v0 + wv, 0, 9, plinth, tex='stone', seed=seed + 3, top=False)
    wall(a, 'l', u0, v0 + wv, 0, left)
    wall(a, 'r', u0, v0, 0, right)
    # corner posts: the near corner and the two far ends
    for (pu, pv, c) in ((u0, v0 + wv, trim), (u0 + wu, v0, dark(trim, .35)), (u0, v0, trim)):
        p0, p1 = a.p(pu, pv, 9), a.p(pu, pv, hz)
        a.line(p0, p1, dark(c, .45), 5)
        a.line((p0[0] - .4, p0[1]), (p1[0] - .4, p1[1]), c, 3.4)
        a.line((p0[0] - 1.4, p0[1]), (p1[0] - 1.4, p1[1]), lite(c, .5), .8)
    a.gable(u0, v0, u0 + wu, v0 + wv, hz, hz + rise, rc, over, along, wall=wc, trim=trim, tex=tex)


def _sack(a, u, v, c='#fff8e6', band=None):
    x, y = a.p(u, v)
    a.ell(x + 4, y + 1, 11, 4, '#0e2010', .35, a.blur(1.2))
    a.add('<path d="M%.1f %.1f q-4 -12 0 -22 q10 -5 20 0 q4 10 0 22 q-10 4 -20 0Z" fill="%s" stroke="%s" stroke-width=".6"/>' % (x - 10, y, a.lg([(0, lite(c, .3)), (.5, c), (1, dark(c, .35))], 0, 0, 1, 0), dark(c, .6)))
    a.path('M%.1f %.1f q5 -7 10 -2 q4 -5 8 2' % (x - 9, y - 22), stroke=dark(c, .5), sw=1.4)
    a.line((x - 7, y - 20.5), (x + 7, y - 20.5), '#8a5a24', 1.6)
    if band:
        a.add(rect(x - 9.5, y - 14, 19, 6, band, None, 0, 1, .9))
    for k in range(3):
        a.path('M%.1f %.1f q2 6 0 12' % (x - 5 + k * 5, y - 18), stroke=dark(c, .3), sw=.5, op=.7)


def workshop(kind):
    a = Art(WSW, WSH, 2.0 * A + 4, WSH - 14, scale=2.97)
    a.seed = len(kind) * 3
    u0, v0, wu, wv = .35, .35, 1.45, 1.45
    LW = wv * L          # the lit wall's length; the shaded wall's is wu * L
    if kind == 'bakery':
        _pad(a, 11, '#d2b47a')
        hz = 78
        left = (door(16, -52, 26, 43, '#8a5a34', True, lamp=True)
                + window(60, -50, 44, 27, glass='#ffd98a', box='#ff7a6a')
                + '<path d="M54 -58 l56 0 l7 15 l-70 0 Z" fill="#d8402e" stroke="#7a1e14" stroke-width=".6"/>'
                + ''.join('<path d="M%d -58 l7 0 l.9 15 l-8.8 0 Z" fill="#fff6e6"/>' % (61 + i * 14) for i in range(4))
                + '<path d="M47 -43 q4.4 5 8.8 0 q4.4 5 8.8 0 q4.4 5 8.8 0 q4.4 5 8.8 0 q4.4 5 8.8 0 q4.4 5 8.8 0 q4.4 5 8.8 0 q4.4 5 8.4 0" fill="#d8402e" stroke="#7a1e14" stroke-width=".5"/>')
        right = window(22, -50, 22, 27, glass='#ffd98a', shutters='#4f8a4a', box='#ffd23a') + window(78, -50, 22, 27, glass='#ffd98a', shutters='#4f8a4a', box='#ffd23a')
        house(a, u0, v0, wu, wv, hz, 42, '#f6e6c0', '#c8553a', 'v', 'timber', left=left, right=right, seed=1)
        top = _chimney(a, u0 + .2, v0 + .82, hz + 12, hz + 58)
        _smoke(a, top[0], top[1])
        # a hanging sign with a loaf on an iron bracket
        x, y = a.p(u0 - .03, v0 + wv * .8, hz - 8)
        a.path('M%.1f %.1f l-20 4 M%.1f %.1f l8 -9' % (x + 8, y - 16, x - 4, y - 13), stroke='#3a3a40', sw=1.6)
        a.line((x - 8, y - 12), (x - 8, y - 6), '#3a3a40', 1)
        a.ell(x - 8, y + 6, 13, 13, '#8a5a34', stroke='#4a2a14', sw=.8)
        a.ell(x - 8, y + 6, 11, 11, '#fff6e0')
        a.ell(x - 8, y + 7, 8, 5, a.rg([(0, '#f6cc80'), (1, '#b8742a')]), stroke='#7a4416', sw=.6)
        for i in (-3, 0, 3):
            a.line((x - 8 + i - 1.2, y + 5), (x - 8 + i + 1.2, y + 8.6), '#7a4416', .8)
        # at the door: a crate of loaves, sacks of flour, a barrel
        crate(a, .0, 1.16, .3, 13)
        for (u, v) in ((.07, 1.24), (.19, 1.36), (.1, 1.4)):
            x, y = a.p(u, v, 14)
            a.ell(x, y, 9.5, 5.2, a.rg([(0, '#f6cc80'), (1, '#b8742a')]), stroke='#7a4416', sw=.5)
            for i in (-3, 0, 3):
                a.line((x + i - 1, y - 2), (x + i + 1, y + 1.4), '#fbe0a8', .9)
        _sack(a, .14, .62, '#fff8e6', '#d9a85e'); _sack(a, .3, .5, '#fff8e6', '#d9a85e')
        barrel(a, 1.72, .16, .12, 24)
    elif kind == 'dairy':
        _pad(a, 12, '#cdbf9a')
        hz = 72
        left = door(36, -50, 28, 41, '#3f72b0', lamp=True) + window(84, -48, 20, 24, shutters='#3f72b0')
        right = ''.join(window(14 + i * 34, -48, 20, 24, shutters='#3f72b0', box='#ffffff' if i == 1 else None) for i in range(3))
        house(a, u0, v0 + .35, wu, wv - .35, hz, 36, '#f8f4ec', '#4a82c4', 'u', 'plaster', plinth='#9aa8b4', left=left, right=right, seed=3, over=.1)
        # the silo: riveted rings, a ladder, a dome
        su, sv = u0 + wu - .34, v0 + .02
        a.blob_shadow(su + .1, sv - .05, .36, .4, 3)
        a.cyl(su, sv, .3, 0, 100, '#dfe6ee')
        for z in range(14, 100, 14):
            x, y = a.p(su, sv, z)
            a.path('M%.1f %.1f a%.1f %.1f 0 0 0 %.1f 0' % (x - .3 * A, y, .3 * A, .3 * B, .6 * A), stroke='#7f90a4', sw=1.3)
            a.path('M%.1f %.1f a%.1f %.1f 0 0 0 %.1f 0' % (x - .3 * A, y - 1, .3 * A, .3 * B, .6 * A), stroke='#ffffff', sw=.5, op=.8)
            for q in range(-4, 5):
                t = q / 4.6
                a.ell(x + .3 * A * t, y + .3 * B * math.sqrt(1 - t * t) + 2, .55, .55, '#6f8094')
        x, y = a.p(su - .12, sv, 0)
        a.path('M%.1f %.1f l0 -92 M%.1f %.1f l0 -92' % (x - 16, y + 4, x - 9, y + 5), stroke='#5a6674', sw=1.2)
        for z in range(6, 92, 7):
            a.line((x - 16, y + 4 - z), (x - 9, y + 5 - z), '#5a6674', 1)
        x, y = a.p(su, sv, 100)
        a.path('M%.1f %.1f a%.1f %.1f 0 0 1 %.1f 0 Z' % (x - .3 * A, y, .3 * A, .3 * A * .8, .6 * A), a.lg([(0, '#9cc0ea'), (.4, '#5a90d0'), (1, '#2f5f9c')], 0, 0, 1, 0), '#274e80', .8)
        a.path('M%.1f %.1f q8 -16 22 -17' % (x - 14, y - 4), stroke='#d0e4f8', sw=1.4, op=.8)
        # milk cans at the door, a round of cheese on the sign
        for (u, v) in ((.08, 1.0), (.2, 1.2), (.04, 1.26)):
            xx, yy = a.p(u, v)
            a.ell(xx + 3, yy + 1, 8, 3.4, '#0e2010', .35, a.blur(1.2))
            a.cyl(u, v, .09, 0, 20, '#c9d2dc', rtop=.085)
            a.cyl(u, v, .085, 20, 27, '#c9d2dc', rtop=.05)
            x, y = a.p(u, v, 27)
            a.ell(x, y - 1.2, 4.6, 2.3, '#eef2f6', stroke='#6a7684', sw=.6)
            a.path('M%.1f %.1f q-5 2 -4 10 M%.1f %.1f q5 2 4 10' % (x - 4, y + 4, x + 4, y + 4), stroke='#7f90a4', sw=1.1)
        x, y = a.p(u0 - .03, v0 + .66, hz - 2)
        a.path('M%.1f %.1f l-18 4' % (x + 6, y - 16), stroke='#3a3a40', sw=1.6)
        a.path('M%.1f %.1f l26 -8 l0 14 l-26 6Z' % (x - 24, y - 6), a.lg([(0, '#ffe07a'), (1, '#f0b62e')]), '#a87410', 1)
        a.path('M%.1f %.1f l26 -8 l-4 -5 l-24 7Z' % (x - 24, y - 6), '#fff0a8', '#a87410', .6)
        a.ell(x - 14, y - 2, 2.6, 2.6, '#d99a1e'); a.ell(x - 5, y - 6, 2, 2, '#d99a1e'); a.ell(x - 18, y + 4, 1.6, 1.6, '#d99a1e')
        crate(a, 1.74, .9, .24, 12, '#a88454')
    elif kind == 'toolshed':
        _pad(a, 13, '#b89a6a')
        wc, rc, hz = '#b98454', '#5f7f68', 62
        a.shadow(u0, v0, u0 + wu, v0 + wv, .8)
        a.box(u0, v0, u0 + wu, v0 + wv, 0, hz, wc, tex='planks', seed=5)
        a.box(u0 - .012, v0 - .012, u0 + wu, v0 + wv, 0, 7, '#8a8078', tex='stone', seed=6, top=False)
        # the big doorway, one leaf open, the dark inside with a workbench
        wall(a, 'l', u0, v0 + wv, 0, rect(26, -50, 52, 43, '#241810', '#5a3a1e', 1.6, 2) + rect(34, -22, 36, 3, '#8a5a34') + rect(36, -19, 3, 12, '#6a4226') + rect(64, -19, 3, 12, '#6a4226')
             + '<path d="M40 -34 l4 12 M50 -36 l0 14 M58 -34 l-3 12" stroke="#9aa0a8" stroke-width="1.6"/>'
             + door(6, -50, 18, 43, '#9a6a3a') + window(92, -44, 20, 20, frame='#e8d8b0'))
        wall(a, 'r', u0, v0, 0, window(36, -44, 20, 20, frame='#e8d8b0', shutters='#5f7f68') + '<path d="M72 -40 h30 M72 -40 v-4 M102 -40 v-4" stroke="#5a3a1e" stroke-width="1.4" fill="none"/>'
             + ''.join('<path d="M%d -40 l0 %d" stroke="#9aa0a8" stroke-width="1.5"/>' % (76 + i * 6, 10 + (i % 3) * 5) for i in range(5)))
        for (pu, pv) in ((u0, v0 + wv), (u0, v0), (u0 + wu, v0)):
            p0, p1 = a.p(pu, pv, 7), a.p(pu, pv, hz + (21 if pu > u0 else 0))
            a.line(p0, p1, '#5a3a1e', 4.4)
            a.line((p0[0] - .6, p0[1]), (p1[0] - .6, p1[1]), '#a87a4a', 2)
        # a lean-to roof, high at the back: the slab's edge shows along the front and the side
        q = [(u0 - .1, v0 - .1, hz - 4), (u0 - .1, v0 + wv + .1, hz - 4), (u0 + wu + .1, v0 + wv + .1, hz + 22), (u0 + wu + .1, v0 - .1, hz + 22)]
        a.poly([(u0, v0, hz), (u0 + wu, v0, hz), (u0 + wu, v0, hz + 21)], '#7a5230')
        wall(a, 'r', u0, v0, hz, ''.join(rect(x, -21 * x / (wu * L), .8, 21 * x / (wu * L), '#4a2a14') for x in range(8, int(wu * L), 8)))
        a.tiles(q, lite(rc, .1), 11, 18, 5)
        a.poly([q[0], q[1], (q[1][0], q[1][1], q[1][2] - 6), (q[0][0], q[0][1], q[0][2] - 6)], a.lg([(0, '#e8d8b0'), (1, '#b8a070')]), '#5a4a2a', .5)
        a.poly([q[0], q[3], (q[3][0], q[3][1], q[3][2] - 6), (q[0][0], q[0][1], q[0][2] - 6)], '#8a7a54', '#4a3a1e', .5)
        # the grindstone on its trestle, an anvil on a stump, a pile of split logs, tools against the wall
        x, y = a.p(.12, 1.25)
        a.ell(x + 4, y + 1, 20, 6, '#0e2010', .35, a.blur(1.5))
        a.path('M%.1f %.1f l8 -22 l8 22 M%.1f %.1f l8 -22 l8 22' % (x - 20, y, x + 2, y), stroke='#4a2a14', sw=4.4)
        a.path('M%.1f %.1f l8 -22 l8 22 M%.1f %.1f l8 -22 l8 22' % (x - 20, y, x + 2, y), stroke='#9a6a3a', sw=2.4)
        a.ell(x - 1, y - 24, 16, 16, '#5a6470')
        a.ell(x - 2.4, y - 24, 14.6, 16, a.rg([(0, '#eef2f6'), (.6, '#aab4c0'), (1, '#6f7b88')]))
        for rr in (11, 7.5):
            a.ell(x - 2.4, y - 24, rr * .92, rr, 'none', stroke='#7f8a98', sw=.5)
        a.ell(x - 2.4, y - 24, 3.4, 3.8, '#6a4226', stroke='#3a2414', sw=.6)
        a.path('M%.1f %.1f l12 -3 l0 6' % (x - 2.4, y - 24), stroke='#3a3a40', sw=1.6)
        barrel(a, .22, .76, .12, 13, '#8a5a34')
        x, y = a.p(.22, .76, 13)
        a.path('M%.1f %.1f l28 0 l-5 6 l-6 0 l0 5 l7 4 l-20 0 l7 -4 l0 -5 l-13 0Z' % (x - 13, y - 17), a.lg([(0, '#9aa6b6'), (1, '#4a5460')]), '#2a3440', .8)
        a.line((x - 12, y - 16.2), (x + 14, y - 16.2), '#d0d8e0', .8)
        for i, (lu, lv) in enumerate(((1.66, .2), (1.76, .3), (1.6, .36), (1.7, .46), (1.68, .3))):
            lx, ly = a.p(lu, lv, 4 + (8 if i == 4 else 0))
            a.ell(lx, ly, 6.4, 5.4, '#e2b678', stroke='#5a3a1e', sw=.6)
            a.ell(lx, ly, 4, 3.4, 'none', stroke='#b8864a', sw=.5)
            a.ell(lx, ly, 1.6, 1.3, '#b8864a')
        for i, c in enumerate(('#c9d2dc', '#e8483a', '#c9d2dc')):
            p, q2 = a.p(u0 + .46 + i * .2, v0 - .07, 0), a.p(u0 + .52 + i * .2, v0 - .01, 48)
            a.line(p, q2, '#5a3a1e', 3.2)
            a.line((p[0] - .5, p[1]), (q2[0] - .5, q2[1]), '#b8864a', 1.6)
            if i == 1:
                a.path('M%.1f %.1f l-7 -3 l1 -6 l12 2 l1 6 Z' % (q2[0], q2[1]), c, dark(c, .5), .6)
            else:
                a.path('M%.1f %.1f l-8 2 M%.1f %.1f l0 -7 M%.1f %.1f l0 -7 M%.1f %.1f l0 -7 M%.1f %.1f l0 -7' % (q2[0] + 5, q2[1], q2[0] - 3, q2[1] + 2, q2[0], q2[1] + 1.3, q2[0] + 3, q2[1] + .6, q2[0] + 5, q2[1]), stroke='#7f8a98', sw=1.4)
    elif kind == 'jam':
        _pad(a, 14, '#d2b48a')
        hz = 74
        jars = ''.join('<rect x="%d" y="%d" width="7" height="8" rx="2" fill="%s" stroke="#5a2a30" stroke-width=".4"/><rect x="%d" y="%d" width="7" height="2" fill="#f6e2b6"/>'
                       % (66 + i * 9, -34 if j else -45, c, 66 + i * 9, -35 if j else -46) for j in range(2) for i, c in enumerate(('#e8483a', '#f2a81e', '#b83a5e', '#e6c93a')))
        left = (door(18, -50, 26, 41, '#7a3a50', True, lamp=True) + window(62, -50, 42, 28, glass='#fff0f4') + jars + rect(62, -37, 42, 1.4, '#8a5a34'))
        right = window(22, -48, 22, 26, glass='#ffe6ee', shutters='#b83a5e', box='#e8483a') + window(78, -48, 22, 26, glass='#ffe6ee', shutters='#b83a5e', box='#e8483a')
        house(a, u0, v0, wu, wv, hz, 38, '#fbe8de', '#b83a5e', 'v', 'timber', plinth='#b09a96', left=left, right=right, seed=6)
        top = _chimney(a, u0 + .9, v0 + .3, hz + 22, hz + 52, '#a85a4a', .14)
        _smoke(a, top[0], top[1], 3)
        # a giant jar by the door, barrels, crates of berries
        xx, yy = a.p(.14, 1.3)
        a.ell(xx + 6, yy + 1, 20, 7, '#0e2010', .38, a.blur(1.6))
        a.cyl(.14, 1.3, .2, 0, 40, '#d8304a', top='#e85a6a')
        a.cyl(.14, 1.3, .16, 40, 46, '#e8c888', top='#f6e2b6')
        x, y = a.p(.14, 1.3, 46)
        a.path('M%.1f %.1f a12 5.2 0 0 0 24 0' % (x - 12, y + 3), stroke='#8a5a34', sw=1.2)
        a.ell(x - 8, y + 26, 2.6, 13, '#ffffff', .55)
        a.add('<rect x="%.1f" y="%.1f" width="18" height="13" rx="2" fill="#fff6e0" stroke="#8a5a34" stroke-width=".6"/>' % (x - 8, y + 17))
        a.ell(x + 1, y + 24, 3.6, 3.4, '#e23a52'); a.path('M%.1f %.1f l3 -1.4 l3 1.4' % (x - 2, y + 20.6), stroke='#3f8a2c', sw=1.2)
        barrel(a, .12, .72, .13, 26); barrel(a, .32, .52, .13, 26)
        crate(a, 1.7, .5, .28, 11)
        r2 = rnd(5)
        for _ in range(16):
            x, y = a.p(1.72 + r2.random() * .24, .52 + r2.random() * .24, 12)
            a.ell(x, y, 2.6, 2.4, r2.choice(['#e8262e', '#c2183a', '#ff5a5a']))
            a.ell(x - .7, y - .8, .8, .6, '#ffd0d0')
    elif kind == 'textile':
        _pad(a, 15, '#c4b28e')
        hz = 76
        left = door(46, -52, 28, 43, '#5a3f8a', lamp=True) + window(12, -50, 22, 27, shutters='#7a58b4') + window(90, -50, 20, 27, shutters='#7a58b4')
        house(a, u0, v0, wu, wv, hz, 40, '#eee4f6', '#7a58b4', 'u', 'plaster', plinth='#a09aac', left=left, right=window(104, -50, 16, 24), seed=8)
        # the big wheel on the shaded wall: a rim of two rings, spokes, paddles, an axle
        wx, wy, R = 56, -36, 40
        g = ''
        for i in range(12):
            q = i * math.pi / 6
            g += '<rect x="%.1f" y="%.1f" width="9" height="13" fill="#8a5a34" stroke="#3a2414" stroke-width=".5" transform="rotate(%.1f %.1f %.1f)"/>' % (wx + R - 3, wy - 6.5, math.degrees(q), wx, wy)
        g += '<circle cx="%s" cy="%s" r="%s" fill="none" stroke="#3a2414" stroke-width="8"/><circle cx="%s" cy="%s" r="%s" fill="none" stroke="#b98454" stroke-width="5.4"/>' % (wx, wy, R, wx, wy, R)
        g += '<circle cx="%s" cy="%s" r="%s" fill="none" stroke="#e2b678" stroke-width="1"/>' % (wx, wy, R + 2)
        g += '<circle cx="%s" cy="%s" r="%s" fill="none" stroke="#3a2414" stroke-width="3.6"/><circle cx="%s" cy="%s" r="%s" fill="none" stroke="#a87a4a" stroke-width="2"/>' % (wx, wy, R * .55, wx, wy, R * .55)
        for i in range(8):
            q = i * math.pi / 4
            g += '<line x1="%.1f" y1="%.1f" x2="%.1f" y2="%.1f" stroke="#3a2414" stroke-width="4"/><line x1="%.1f" y1="%.1f" x2="%.1f" y2="%.1f" stroke="#a87a4a" stroke-width="2.2"/>' % (
                wx, wy, wx + math.cos(q) * R, wy + math.sin(q) * R, wx, wy, wx + math.cos(q) * R, wy + math.sin(q) * R)
        g += '<circle cx="%s" cy="%s" r="7" fill="#5a3a1e" stroke="#2a180c" stroke-width="1"/><circle cx="%s" cy="%s" r="3" fill="#9aa0a8"/>' % (wx, wy, wx, wy)
        wall(a, 'r', u0, v0 - .07, 0, g)
        # a line of dyed cloth, bolts of fabric, a basket of yarn
        p0, p1 = a.p(.02, 1.75, 0), a.p(.02, .95, 0)
        for (px, py) in (p0, p1):
            a.line((px, py), (px, py - 46), '#5a3a1e', 3)
            a.line((px - .5, py), (px - .5, py - 46), '#b8864a', 1.4)
        a.path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (p0[0], p0[1] - 44, (p0[0] + p1[0]) / 2, (p0[1] + p1[1]) / 2 - 38, p1[0], p1[1] - 44), stroke='#e8dcc0', sw=.9)
        for i, c in enumerate(('#e8483a', '#f2c230', '#4a9ae0')):
            t = .2 + i * .27
            cx_, cy_ = p0[0] + (p1[0] - p0[0]) * t, p0[1] + (p1[1] - p0[1]) * t - 41
            a.path('M%.1f %.1f l13 7.6 l0 24 q-6.5 -3 -13 -7.6 Z' % (cx_ - 6, cy_ - 3), a.lg([(0, lite(c, .3)), (1, dark(c, .25))]), dark(c, .55), .5)
            a.path('M%.1f %.1f l0 22 M%.1f %.1f l0 23' % (cx_ - 2, cy_, cx_ + 3, cy_ + 3), stroke=dark(c, .3), sw=.5)
        for i, c in enumerate(('#e8483a', '#4a9ae0', '#6cc04a')):
            x, y = a.p(.2 + i * .1, .64 - i * .1)
            a.add('<rect x="%.1f" y="%.1f" width="11" height="30" rx="5" fill="%s" stroke="%s" stroke-width=".6"/>' % (x - 5.5, y - 30, a.lg([(0, lite(c, .35)), (1, dark(c, .3))], 0, 0, 1, 0), dark(c, .55)))
            a.ell(x, y - 30, 5.5, 2.6, lite(c, .45), stroke=dark(c, .5), sw=.5)
            a.ell(x, y - 30, 2, 1, '#f6e2b6')
        x, y = a.p(1.72, .3)
        a.ell(x + 3, y + 1, 15, 5, '#0e2010', .35, a.blur(1.2))
        a.path('M%.1f %.1f q0 12 6 13 l16 0 q6 -1 6 -13 Z' % (x - 14, y - 13), a.lg([(0, '#d9b070'), (1, '#9a6a34')], 0, 0, 1, 0), '#5a3a1e', .6)
        for k in range(5):
            a.line((x - 13 + k * 6.5, y - 12), (x - 11 + k * 6, y), '#7a5024', .6)
        for (dx, c) in ((-7, '#ff8ab0'), (1, '#8fd0f0'), (8, '#f2c230')):
            a.ell(x + dx, y - 15, 6.4, 6, a.rg([(0, lite(c, .5)), (1, dark(c, .25))]), stroke=dark(c, .5), sw=.5)
            a.path('M%.1f %.1f q5 -4 10 1 M%.1f %.1f q5 -1 9 4' % (x + dx - 5, y - 16, x + dx - 5, y - 13), stroke=dark(c, .35), sw=.5)
    else:  # sugar_mill
        _pad(a, 16, '#cdb07a')
        hz = 72
        left = door(28, -50, 30, 41, '#2a7a76', True, lamp=True) + window(72, -48, 22, 25, shutters='#2f9a94', box='#ffffff')
        right = window(22, -48, 22, 25, shutters='#2f9a94') + window(72, -48, 22, 25, shutters='#2f9a94')
        house(a, u0, v0 + .3, wu, wv - .3, hz, 36, '#f6dc8a', '#2f9a94', 'v', 'timber', plinth='#b0a082', left=left, right=right, seed=10, over=.1)
        # the tall brick stack: courses of brick, iron bands, a lip, smoke
        su, sv = u0 + wu - .3, v0 + .0
        a.blob_shadow(su + .1, sv - .05, .26, .4, 3)
        a.cyl(su, sv, .2, 0, 150, '#c2674a', rtop=.13)
        x0, y0 = a.p(su, sv, 0)
        for z in range(4, 150, 4):
            rr = (.2 - .07 * z / 150) * A
            y = y0 - z
            a.path('M%.1f %.1f a%.1f %.1f 0 0 0 %.1f 0' % (x0 - rr, y, rr, rr * B / A, 2 * rr), stroke='#7a3220', sw=.5, op=.8)
            for q in range(-3, 4):
                t = (q + (.5 if (z // 4) % 2 else 0)) / 3.6
                if abs(t) < 1:
                    a.line((x0 + rr * t, y + rr * B / A * math.sqrt(1 - t * t)), (x0 + rr * t, y - 4 + rr * B / A * math.sqrt(1 - t * t)), '#7a3220', .45, .8)
        for z in (40, 84, 126):
            rr = (.2 - .07 * z / 150) * A
            a.path('M%.1f %.1f a%.1f %.1f 0 0 0 %.1f 0' % (x0 - rr, y0 - z, rr, rr * B / A, 2 * rr), stroke='#3a3a40', sw=2.6)
            a.path('M%.1f %.1f a%.1f %.1f 0 0 0 %.1f 0' % (x0 - rr, y0 - z - .8, rr, rr * B / A, 2 * rr), stroke='#aab0b8', sw=.6)
        a.cyl(su, sv, .15, 150, 156, '#8a4a34', top='#2a1810')
        x, y = a.p(su, sv, 160)
        _smoke(a, x, y - 2, 5)
        # sacks of sugar, a bundle of cane, a hand cart
        _sack(a, .1, 1.25, '#fff8e6', '#4a9ae0'); _sack(a, .28, 1.4, '#fff8e6', '#4a9ae0'); _sack(a, .16, 1.52, '#fff8e6', '#4a9ae0')
        for i in range(6):
            p, q2 = a.p(.1 + i * .035, .72 + i * .03, 0), a.p(.2 + i * .035, .6 + i * .03, 46)
            a.line(p, q2, '#2e6a1c', 3.8)
            a.line((p[0] - .5, p[1]), (q2[0] - .5, q2[1]), '#9fd05a', 1.8)
            for k in (1, 2, 3):
                jx, jy = p[0] + (q2[0] - p[0]) * k / 4, p[1] + (q2[1] - p[1]) * k / 4
                a.line((jx - 1.8, jy), (jx + 1.8, jy + .4), '#2e6a1c', .9)
        a.path('M%.1f %.1f q8 -3 14 2' % (a.p(.12, .74, 14)[0] - 2, a.p(.12, .74, 14)[1]), stroke='#8a5a24', sw=1.6)
        crate(a, 1.74, .72, .26, 12)
    return a


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
    a.paint = False
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
    a.paint = False
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
    a.paint = False
    return a


def bar(fill):
    a = Art(128, 26, 0, 0, scale=1)
    if fill:
        a.add(rect(4, 4, 120, 18, a.lg([(0, '#ffffff'), (1, '#e6e6e6')]), None, 0, 9))
        a.add(rect(8, 6, 112, 5, '#ffffff', None, 0, 2.5, .7))
    else:
        a.add(rect(0, 0, 128, 26, '#3a2414', None, 0, 13, .9))
        a.add(rect(3, 3, 122, 20, '#6a4a2e', None, 0, 10))
    a.paint = False
    return a


def sickle():
    a = Art(128, 128, 0, 0)
    a.path('M34 96 q-18 -58 36 -76 q30 -6 44 14 q-22 -12 -44 2 q-30 20 -18 54 Z', '#1c2a10', op=.3, flt=a.blur(3))
    a.path('M26 120 l14 -34', stroke='#5a3410', sw=17)
    a.path('M26 120 l14 -34', stroke='#c8843c', sw=11)
    a.path('M24 117 l5 -12', stroke='#f0b468', sw=4)
    a.path('M32 92 q-18 -58 36 -76 q30 -6 44 14 q-22 -12 -44 2 q-30 20 -18 54 Z', a.lg([(0, '#ffffff'), (.5, '#c2ccd8'), (1, '#6f7b88')], 0, 0, 1, 1), '#3a4654', 3.4)
    a.path('M38 78 q-8 -42 32 -56', stroke='#ffffff', sw=3, op=.9)
    a.paint = False
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


GW, GH = 512, 440


def street_ground(street=True):
    """The ground of the street under the farm, a strip that repeats left to right: mottled meadow grass with
    tufts and flowers, a worn dirt road along the front, and a rail fence along the back that hides the seam
    with the painted zones. Everything that crosses the left edge comes back in at the right one."""
    a = Art(GW, GH, 0, 0, scale=2.0)
    a.seed = 77
    # it repeats: no light across it, no turned edges, only a fine grain
    a.look = dict(volume=0, rim=0, grain=.025, contrast=1.0, sat=1.0)
    r = rnd(40)
    wrap = (0, GW, -GW)
    a.add(rect(0, 34 if street else 0, GW, GH - (34 if street else 0), a.lg([(0, '#79b94e'), (.6, '#72b049'), (1, '#6aa644')]) if street else '#6aa644'))
    # light and shade lie in soft patches, as under scattered clouds and trees
    for _ in range(26):
        x, y, rx = r.random() * GW, 60 + r.random() * 270, 40 + r.random() * 70
        c = r.choice(['#8cc656', '#84c050', '#5c9a3a', '#548f36', '#94cc5a'])
        for dx in wrap:
            a.ell(x + dx, y, rx, rx * .42, c, .34, a.blur(9))
    # the road: packed earth with ruts, pebbles, grass creeping in from both sides
    for dx in (wrap if street else ()):
        a.path('M%d 372 q64 -14 128 0 t128 0 t128 0 t128 0 l0 62 l-512 0Z' % dx, '#a8854e', op=.6, flt=a.blur(5))
        a.path('M%d 380 q64 -12 128 0 t128 0 t128 0 t128 0 l0 60 l-512 0Z' % dx, a.lg([(0, '#dcc08a'), (1, '#c2a068')]), flt=a.blur(2))
        for yy in (398, 420):
            a.path('M%d %d q64 -7 128 0 t128 0 t128 0 t128 0' % (dx, yy), stroke='#a8854e', sw=5, op=.4)
            a.path('M%d %d q64 -7 128 0 t128 0 t128 0 t128 0' % (dx, yy + 3), stroke='#f0dcae', sw=2, op=.4)
    for _ in range(90 if street else 0):
        x, y = r.random() * GW, 386 + r.random() * 50
        c = r.choice(['#f0dcaa', '#a8854e', '#d2b47a', '#8a6e42'])
        for dx in wrap:
            a.ell(x + dx, y, 1.5 + r.random() * 3, 1 + r.random() * 1.4, c, .75)
    # tufts everywhere, thicker along the road's edge and the fence
    spots = [(r.random() * GW, 70 + r.random() * 300) for _ in range(150)] + [(r.random() * GW, 366 + r.random() * 16) for _ in range(70)] + [(r.random() * GW, 46 + r.random() * 16) for _ in range(60)]
    if not street:
        spots = [(r.random() * GW, 12 + r.random() * (GH - 20)) for _ in range(230)]
    spots.sort(key=lambda p: p[1])
    for (x, y) in spots:
        n = r.choice([3, 4, 5])
        for k in range(n):
            c = r.choice(['#3f8a2c', '#4c9632', '#5fae3c', '#7cc84c', '#9ad25a'])
            ex, ey = (k - n / 2) * 3 + r.random() * 3, -7 - r.random() * 9
            for dx in wrap:
                a.path('M%.1f %.1f q%.1f %.1f %.1f %.1f' % (x + dx + k * 1.6, y, ex * .2, ey * .6, ex, ey), stroke=c, sw=1.7)
    for _ in range(46):
        x, y = r.random() * GW, 70 + r.random() * 290
        c = r.choice(['#ffffff', '#ffd23a', '#ff9ab8', '#c8a8ff', '#ffb04a'])
        for dx in wrap:
            a.line((x + dx, y), (x + dx, y - 6), '#4c9632', 1.1)
            for j in range(5):
                q = 6.283 * j / 5
                a.ell(x + dx + math.cos(q) * 2.4, y - 7 + math.sin(q) * 2.1, 1.9, 1.7, c)
            a.ell(x + dx, y - 7, 1.3, 1.2, '#f6c22e' if c != '#ffd23a' else '#b06a16')
    # the fence along the back: weathered posts and two rails, its shadow on the grass
    post, rail = '#a8845c', '#c2a078'
    if not street:
        return a
    for dx in wrap:
        a.add(rect(dx, 40, GW, 7, '#16260e', None, 0, 3, .3).replace('/>', ' filter="%s"/>' % a.blur(3)))
        for yy in (14, 26):
            a.add(rect(dx, yy, GW, 5.5, a.lg([(0, lite(rail, .3)), (1, dark(rail, .3))]), dark(rail, .5), .5, 2))
            for k in range(8):
                gx = dx + r.random() * GW
                a.line((gx, yy + 2), (gx + 10 + r.random() * 14, yy + 2.6), dark(rail, .4), .7, .5)
    for k in range(8):
        px = 32 + k * 64
        for dx in wrap:
            a.add(rect(px + dx - 5, 4, 10, 40, a.lg([(0, lite(post, .3)), (.5, post), (1, dark(post, .4))], 0, 0, 1, 0), dark(post, .55), .6, 2.5))
            a.ell(px + dx, 5, 5, 2.2, lite(post, .4))
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
                    ('Tuft1', tuft(1)), ('Tuft2', tuft(2)), ('Bush1', bush(1)), ('Bush2', bush(2, '#ff8ab0')), ('Bush3', bush(3, '#ffd23a')), ('Street', street_ground()), ('Grass', street_ground(False))):
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
