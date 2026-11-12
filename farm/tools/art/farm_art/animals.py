"""Animals (ART_BIBLE §4–§6): drawn at their real size on the map — a hen 44 high, a sheep 60, a pig 52, a cow 96 —
each with its own build and outline of back and head, lit from the top left, standing on its own shadow.
Each comes plain and with its product beside it."""
import math
from iso import Art, lite, dark, mix, rnd

# canvas (map units) and where the feet are on it, from the left and from the bottom
SIZES = dict(chicken=(96, 88, 40, 26), sheep=(150, 122, 64, 38), pig=(160, 108, 68, 34), cow=(214, 172, 96, 50), bee=(120, 118, 46, 32))


def _ground(a, x, y, w, h):
    """The animal's shadow: thrown to the right and down, and dark right under it."""
    a.ell(x + h * .3 + 2, y + h * .1 + 1.5, w * .56, w * .17, '#0e2010', .40, a.blur(2))
    a.ell(x + 2, y + 1.5, w * .4, w * .1, '#0e2010', .45, a.blur(1.1))


def _eye(a, x, y, r=2.2):
    a.ell(x, y, r, r * 1.08, '#1e120a')
    a.ell(x - r * .32, y - r * .38, r * .36, r * .36, '#ffffff')


def _leg(a, x, y, h, w, col, hoof, lit=True):
    """A leg with a knee and a hoof."""
    c = col if lit else dark(col, .28)
    a.path('M%.1f %.1f q%.1f %.1f %.1f %.1f l%.1f 0 q%.1f %.1f %.1f %.1f Z' % (x - w / 2, y - h, -w * .12, h * .5, w * .1, h - 3, w * .8, w * .2, -h * .5, w * .1, -h + 3),
           a.lg([(0, lite(c, .2)), (.5, c), (1, dark(c, .3))], 0, 0, 1, 0), dark(c, .55), .5)
    a.path('M%.1f %.1f l%.1f 0 l.6 3.6 l%.1f 0 Z' % (x - w * .42, y - 3.4, w * .84, -w * .96), hoof, dark(hoof, .5), .4)


def chicken(a, x, y, ready):
    _ground(a, x, y, 40, 30)
    out = '#8a7a66'
    white, cream, shade = '#fffaf0', '#eee4d0', '#c9b89a'
    # legs and toes
    for (lx, lit) in ((5, False), (-3, True)):
        c = '#e8901a' if lit else '#c07410'
        a.path('M%.1f %.1f l-.6 10' % (x + lx, y - 11), stroke='#8a4e08', sw=2.6)
        a.path('M%.1f %.1f l-.6 10' % (x + lx, y - 11), stroke=c, sw=1.5)
        a.path('M%.1f %.1f l-5 1.6 M%.1f %.1f l-1.4 2.6 M%.1f %.1f l3.6 1.6' % (x + lx - .6, y - 1, x + lx - .6, y - 1, x + lx - .6, y - 1), stroke=c, sw=1.3)
    # the tail: a fan of quills
    for i in range(6):
        ang = 38 + i * 13
        t = math.radians(ang)
        ln = 15 + (2.5 - abs(i - 2.5)) * 2.4
        bx, by = x + 13, y - 27
        tx, ty = bx + math.cos(t) * ln, by - math.sin(t) * ln
        nx, ny = math.sin(t) * 3, math.cos(t) * 3
        a.path('M%.1f %.1f Q%.1f %.1f %.1f %.1f Q%.1f %.1f %.1f %.1f Z' % (bx, by, (bx + tx) / 2 + nx, (by + ty) / 2 + ny, tx, ty, (bx + tx) / 2 - nx, (by + ty) / 2 - ny, bx, by),
               [shade, cream, white][min(2, i // 2)], out, .4)
        a.line((bx, by), (tx, ty), '#b8a888', .35)
    # the body: a full breast, the back rising to the tail
    body = 'M%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f Z' % (
        x - 15, y - 26, x - 19, y - 12, x - 4, y - 7, x + 7, y - 9, x + 17, y - 11, x + 19, y - 24, x + 14, y - 30, x + 6, y - 34, x - 9, y - 36, x - 15, y - 26)
    a.path(body, a.rg([(0, white), (.6, cream), (1, shade)], .35, .28, .8), out, .6)
    a.path('M%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f Z' % (x - 13, y - 14, x - 6, y - 8, x + 6, y - 9, x + 14, y - 14, x + 8, y - 11, x - 6, y - 10, x - 13, y - 14), shade, op=.75)
    # the wing, feathers in rows
    a.path('M%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f Z' % (x - 6, y - 28, x + 4, y - 31, x + 14, y - 26, x + 16, y - 18, x + 8, y - 13, x - 4, y - 16, x - 6, y - 28),
           a.lg([(0, white), (1, cream)]), out, .5)
    for row in range(3):
        for i in range(4 - row):
            fx, fy = x - 3 + i * 4.6 + row * 2, y - 25 + row * 3.6
            a.path('M%.1f %.1f q2.3 3 4.6 0' % (fx, fy), stroke='#b8a888', sw=.5)
    # neck feathers, head, comb, wattle, beak, eye
    a.path('M%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f L%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f Z' % (x - 14, y - 27, x - 17, y - 33, x - 18, y - 37, x - 17, y - 40, x - 8, y - 40, x - 7, y - 36, x - 6, y - 32, x - 6, y - 30),
           a.lg([(0, white), (1, cream)], 0, 0, 1, 0), out, .5)
    for i in range(4):
        a.path('M%.1f %.1f q1.6 2 3.2 0' % (x - 15.5 + i * 2.4, y - 31 + (i % 2)), stroke='#b8a888', sw=.45)
    hx, hy = x - 13, y - 42
    a.path('M%.1f %.1f q1 -6 3.6 -2 q1.6 -6 4 -1 q2.6 -4 3.4 1.4 Z' % (hx - 4.6, hy - 4), a.lg([(0, '#f06a5a'), (1, '#d8302a')]), '#8a1410', .5)
    a.ell(hx, hy, 6, 5.6, a.rg([(0, white), (.7, cream), (1, shade)], .35, .3, .8), stroke=out, sw=.55)
    a.path('M%.1f %.1f l-6.4 2 l6 2.2 Z' % (hx - 5, hy - .6), '#f2a81e', '#8a5a08', .45)
    a.line((hx - 10, hy + 1.3), (hx - 5.4, hy + 1.3), '#8a5a08', .35)
    a.path('M%.1f %.1f q-1.2 4.6 1 5.4 q2.4 -.8 1.4 -5.4 Z' % (hx - 4, hy + 3), '#d8302a', '#8a1410', .45)
    _eye(a, hx - 2, hy - 1, 1.5)
    a.ell(hx + 1.4, hy + 1.6, 1.8, 1.2, '#f8b0b0', .7)
    if ready:
        nx_, ny_ = x + 30, y - 1
        a.ell(nx_ + 2, ny_ + 2, 14, 4.4, '#0e2010', .4, a.blur(1))
        a.ell(nx_, ny_, 12.5, 4.6, '#b88a3a', stroke='#6a4a14', sw=.5)
        r = rnd(5)
        for _ in range(26):
            q = r.random() * 6.283
            a.line((nx_ + math.cos(q) * 12, ny_ + math.sin(q) * 4), (nx_ + math.cos(q) * 7, ny_ + math.sin(q) * 2 - 1.5), r.choice(['#f2d264', '#d9b03e', '#8a6a14']), .6)
        a.ell(nx_, ny_ - 5.5, 5.2, 6.6, a.rg([(0, '#ffffff'), (.7, '#f6ead0'), (1, '#d2bc94')], .35, .28, .8), stroke='#a88a58', sw=.5)


def cow(a, x, y, ready):
    _ground(a, x, y, 120, 80)
    out = '#5e4a3a'
    white, mid, shade, patch = '#fbf7ee', '#e6dfd2', '#b8ae9e', '#5a3a26'
    # far legs, then the near ones
    _leg(a, x - 30, y - 2, 40, 7.6, mid, '#2e2018', False)
    _leg(a, x + 44, y - 2, 42, 8.4, mid, '#2e2018', False)
    # the tail and its tuft
    a.path('M%.1f %.1f q10 8 7 34' % (x + 60, y - 78), stroke=out, sw=3.4)
    a.path('M%.1f %.1f q10 8 7 34' % (x + 60, y - 78), stroke=mid, sw=2.2)
    a.path('M%.1f %.1f q-4 9 1 15 q6 -5 4 -15 Z' % (x + 65, y - 46), patch, dark(patch, .5), .4)
    # the barrel: withers, a level back, hips, a deep belly, the brisket
    body = ('M%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f Z' % (
        x - 46, y - 84, x - 20, y - 92, x + 30, y - 88, x + 52, y - 86, x + 66, y - 84, x + 68, y - 60, x + 60, y - 44,
        x + 50, y - 34, x + 10, y - 30, x - 20, y - 34, x - 40, y - 36, x - 56, y - 44, x - 56, y - 62, x - 56, y - 74, x - 54, y - 80, x - 46, y - 84))
    a.path(body, a.lg([(0, white), (.55, mid), (1, shade)]), out, .7)
    # patches lie on the form: stretched along the barrel, wrapped over the back
    for d in ('M%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f Z' % (x - 12, y - 89, x + 6, y - 92, x + 26, y - 88, x + 28, y - 76, x + 30, y - 62, x + 12, y - 58, x, y - 64, x - 12, y - 68, x - 20, y - 80, x - 12, y - 89),
              'M%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f Z' % (x + 46, y - 86, x + 62, y - 84, x + 66, y - 66, x + 60, y - 54, x + 48, y - 54, x + 40, y - 70, x + 46, y - 86),
              'M%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f Z' % (x - 30, y - 56, x - 16, y - 60, x - 6, y - 50, x - 10, y - 40, x - 24, y - 36, x - 38, y - 44, x - 30, y - 56)):
        a.path(d, a.lg([(0, lite(patch, .18)), (1, dark(patch, .3))]))
    # the belly turns into shade; light along the spine
    a.path('M%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f Z' % (x - 52, y - 50, x - 30, y - 34, x + 30, y - 30, x + 62, y - 48, x + 40, y - 40, x - 20, y - 42, x - 52, y - 50), '#3a2a20', op=.28)
    a.path('M%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f' % (x - 40, y - 85, x - 10, y - 91, x + 30, y - 88, x + 54, y - 85), stroke='#ffffff', sw=1.4, op=.7)
    # the udder, the near legs
    a.path('M%.1f %.1f q2 12 12 12 q10 0 12 -12 Z' % (x + 22, y - 36), a.lg([(0, '#f8c0c8'), (1, '#d88a98')]), '#a85a6a', .5)
    for tx in (28, 34, 40):
        a.path('M%.1f %.1f l0 4' % (x + tx, y - 25), stroke='#d88a98', sw=2)
    _leg(a, x - 44, y, 42, 8.4, white, '#2e2018')
    _leg(a, x + 30, y, 44, 9.4, white, '#2e2018')
    # neck and head: a long face, a broad muzzle, ears out to the sides, short horns
    a.path('M%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f L%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f Z' % (x - 44, y - 84, x - 56, y - 90, x - 62, y - 92, x - 68, y - 92, x - 70, y - 66, x - 62, y - 60, x - 54, y - 56, x - 50, y - 58),
           a.lg([(0, white), (1, mid)]), out, .6)
    hx, hy = x - 74, y - 78
    for (ex, rot, c) in ((12, 28, patch), (-11, -28, white)):
        a.ell(hx + ex, hy - 13, 8, 4, c, rot=rot, stroke=out, sw=.5)
        a.ell(hx + ex, hy - 13, 5, 2, '#f0b8c0', rot=rot)
    for sg in (-1, 1):
        a.path('M%.1f %.1f q%.1f -8 %.1f -11' % (hx + sg * 6, hy - 17, sg * 5, sg * 2), stroke='#8a7a5a', sw=4)
        a.path('M%.1f %.1f q%.1f -8 %.1f -11' % (hx + sg * 6, hy - 17, sg * 5, sg * 2), stroke='#efe2c0', sw=2.4)
    a.path('M%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f Z' % (
        hx - 11, hy - 16, hx - 15, hy - 4, hx - 14, hy + 10, hx - 9, hy + 16, hx - 2, hy + 20, hx + 8, hy + 18, hx + 11, hy + 8, hx + 13, hy - 4, hx + 12, hy - 16, hx - 11, hy - 16),
           a.lg([(0, white), (1, mid)]), out, .6)
    a.path('M%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f Z' % (hx + 1, hy - 16, hx + 12, hy - 16, hx + 13, hy - 2, hx + 9, hy + 2, hx + 2, hy + 2, hx - 2, hy - 8, hx + 1, hy - 16), patch)
    a.path('M%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f Z' % (hx - 13, hy + 6, hx - 14, hy + 16, hx - 4, hy + 21, hx + 6, hy + 19, hx + 11, hy + 12, hx + 4, hy + 5, hx - 13, hy + 6),
           a.lg([(0, '#f8c8cc'), (1, '#e0a0aa')]), '#a85a6a', .5)
    a.ell(hx - 7, hy + 13, 1.6, 2.2, '#8a4a58'); a.ell(hx + 1.6, hy + 14, 1.6, 2.2, '#8a4a58')
    a.path('M%.1f %.1f q5 2.4 10 0' % (hx - 8, hy + 17.6), stroke='#a85a6a', sw=.6)
    _eye(a, hx - 6.4, hy - 3, 2.3); _eye(a, hx + 6.4, hy - 2.4, 2.3)
    for k in range(4):
        a.path('M%.1f %.1f q1.4 -4 %.1f -5.6' % (hx - 4 + k * 2.6, hy - 15, k - 1.5), stroke='#efe6d2', sw=1)
    if ready:
        bx, by = x + 86, y - 2
        a.ell(bx + 3, by + 2, 13, 4, '#0e2010', .42, a.blur(1))
        a.path('M%.1f %.1f l2.6 -20 l19 0 l2.6 20 q-12 4.4 -24.2 0Z' % (bx - 12, by), a.lg([(0, '#f2f5f8'), (.4, '#c2ccd8'), (1, '#8490a0')], 0, 0, 1, 0), '#56626e', .6)
        a.ell(bx, by - 20, 9.6, 3.4, '#ffffff', stroke='#56626e', sw=.5)
        a.path('M%.1f %.1f q9.6 -14 19.2 0' % (bx - 9.6, by - 20), stroke='#56626e', sw=1.2)
        a.path('M%.1f %.1f a12 4 0 0 0 23 0' % (bx - 11.5, by - 8), stroke='#7a8694', sw=.9)


def sheep(a, x, y, ready):
    k = 1.1 if ready else 1.0
    _ground(a, x, y, 80, 54)
    face, wool, wshade = '#4a3830', '#fbf6ea', '#bfb29a'
    for (lx, lit) in ((-16, False), (22, False), (-24, True), (14, True)):
        _leg(a, x + lx, y - (0 if lit else 2), 22, 5.2, '#3e3028' if lit else '#2a201a', '#16100c')
    r = rnd(3)
    # the fleece: a body of wool with a scalloped edge, darker underneath, curls all over
    cx, cy, rx, ry = x + 2, y - 38 * k, 36 * k, 21 * k
    lobes = []
    n = 16
    for i in range(n):
        q = 6.283 * i / n
        lobes.append((cx + math.cos(q) * rx, cy + math.sin(q) * ry, 9.5 + r.random() * 3))
    for (lx, ly, lr) in lobes:
        a.ell(lx + 1.4, ly + 2, lr, lr * .92, wshade)
    a.ell(cx, cy, rx, ry, a.lg([(0, '#ffffff'), (.6, wool), (1, '#d8ccb4')]))
    for (lx, ly, lr) in sorted(lobes, key=lambda l: l[1]):
        low = (ly - cy) / ry
        a.ell(lx, ly, lr, lr * .92, a.rg([(0, '#ffffff'), (.65, wool if low < .3 else '#e2d8c2'), (1, '#cfc2a8')], .36, .3, .8), stroke='#a89a80', sw=.4)
    for _ in range(70):
        q, d = r.random() * 6.283, math.sqrt(r.random())
        px, py = cx + math.cos(q) * d * rx, cy + math.sin(q) * d * ry
        a.path('M%.1f %.1f q2.2 -2.6 4.4 0 q-.6 2 -2.6 1.2' % (px - 2.2, py), stroke='#c9bca4' if py < cy + 6 else '#a89a80', sw=.55)
    a.path('M%.1f %.1f q%.1f %.1f %.1f 0' % (cx - rx * .8, cy + ry * .5, rx * .8, ry * .9, rx * 1.6), stroke='#8a7a64', sw=5, op=.25)
    # the head: a dark, narrow face under a cap of wool, ears to the sides
    hx, hy = x - 38 * k + 2, y - 44 * k
    for sg in (-1, 1):
        a.ell(hx + sg * 10.5, hy - 1, 6.4, 3, face, rot=sg * 24, stroke='#1e1410', sw=.4)
        a.ell(hx + sg * 10.5, hy - .6, 3.6, 1.4, '#7a6258', rot=sg * 24)
    a.path('M%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f Z' % (
        hx - 8, hy - 8, hx - 10, hy + 2, hx - 7, hy + 13, hx, hy + 15, hx + 7, hy + 13, hx + 10, hy + 2, hx + 8, hy - 8, hx + 4, hy - 11, hx - 4, hy - 11, hx - 8, hy - 8),
           a.lg([(0, lite(face, .2)), (1, dark(face, .3))], 0, 0, 1, 0), '#1e1410', .5)
    for (dx, rr) in ((-6, 5.4), (0, 6.4), (6, 5.4)):
        a.ell(hx + dx, hy - 10, rr, rr * .9, a.rg([(0, '#ffffff'), (1, '#e2d8c2')], .36, .3, .8), stroke='#a89a80', sw=.4)
    for sg in (-1, 1):
        a.ell(hx + sg * 4.2, hy + 1, 2.3, 2.6, '#f6f0e4')
        a.ell(hx + sg * 4.2, hy + 1.5, 1.2, 1.5, '#16100c')
        a.ell(hx + sg * 4.2 - .4, hy + 1, .4, .4, '#ffffff')
    a.path('M%.1f %.1f l2 2.2 l2 -2.2 M%.1f %.1f l0 2' % (hx - 2, hy + 9, hx, hy + 11.2), stroke='#e8a0aa', sw=.9)
    if ready:
        bx, by = x + 52, y - 4
        a.ell(bx + 3, by + 3, 14, 4, '#0e2010', .4, a.blur(1))
        for (dx, dy, rr) in ((-6, -2, 7), (5, -3, 7.6), (0, -9, 7), (-1, 1, 6)):
            a.ell(bx + dx, by + dy, rr, rr * .9, a.rg([(0, '#ffffff'), (1, '#e2d8c2')], .36, .3, .8), stroke='#a89a80', sw=.4)


def pig(a, x, y, ready):
    _ground(a, x, y, 90, 50)
    out, pink, light, shade = '#8a3a4a', '#eea0ae', '#f8c0c8', '#c06a7c'
    _leg(a, x - 20, y - 2, 20, 8, pink, '#6a3a40', False)
    _leg(a, x + 34, y - 2, 20, 8.6, pink, '#6a3a40', False)
    # a curl of a tail
    a.path('M%.1f %.1f q8 -3 7 -9 q-2 -6 -7 -2 q-2 5 5 6 q5 0 6 -5' % (x + 44, y - 38), stroke=out, sw=2.6)
    a.path('M%.1f %.1f q8 -3 7 -9 q-2 -6 -7 -2 q-2 5 5 6 q5 0 6 -5' % (x + 44, y - 38), stroke=pink, sw=1.5)
    # the barrel: a long, low body with a round rump and a sagging belly
    a.path('M%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f Z' % (
        x - 34, y - 46, x - 10, y - 54, x + 26, y - 53, x + 42, y - 46, x + 52, y - 40, x + 52, y - 26, x + 44, y - 19,
        x + 30, y - 12, x - 6, y - 11, x - 24, y - 15, x - 38, y - 20, x - 42, y - 36, x - 34, y - 46),
           a.lg([(0, light), (.55, pink), (1, shade)]), out, .7)
    a.path('M%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f Z' % (x - 34, y - 24, x - 14, y - 12, x + 28, y - 12, x + 48, y - 26, x + 30, y - 19, x - 10, y - 19, x - 34, y - 24), '#8a3a4a', op=.3)
    a.path('M%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f' % (x - 26, y - 48, x - 4, y - 53, x + 24, y - 52, x + 40, y - 46), stroke='#ffe6ea', sw=1.3, op=.8)
    r = rnd(4)
    for _ in range(40):
        bx_, by_ = x - 26 + r.random() * 68, y - 48 + r.random() * 22
        a.line((bx_, by_), (bx_ + 2.2, by_ + 1.4), '#fbd0d6' if by_ < y - 36 else '#d88a98', .45, .9)
    _leg(a, x - 30, y, 21, 9, light, '#6a3a40')
    _leg(a, x + 24, y, 21, 9.6, light, '#6a3a40')
    # the head: heavy jowls, ears flopping forward, a flat round snout
    hx, hy = x - 46, y - 34
    a.path('M%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f Z' % (
        hx - 2, hy - 17, hx - 16, hy - 15, hx - 21, hy - 2, hx - 17, hy + 8, hx - 12, hy + 16, hx + 6, hy + 17, hx + 14, hy + 8, hx + 20, hy - 2, hx + 14, hy - 17, hx - 2, hy - 17),
           a.rg([(0, light), (.6, pink), (1, shade)], .36, .3, .85), out, .65)
    for (ex, flip) in ((9, 1), (-10, -1)):
        a.path('M%.1f %.1f q%.1f -11 %.1f -12 q%.1f 6 %.1f 14 Z' % (hx + ex - flip * 4, hy - 13, flip * 2, flip * 9, flip * 1, -flip * 3), a.lg([(0, pink), (1, shade)]), out, .5)
        a.path('M%.1f %.1f q%.1f -6 %.1f -7' % (hx + ex - flip * 2, hy - 12, flip * 2, flip * 5), stroke='#f8c8d0', sw=1.6)
    a.ell(hx - 9, hy + 5, 9, 7.4, a.lg([(0, '#fbd0d6'), (1, '#e090a0')]), stroke=out, sw=.6, rot=-12)
    a.ell(hx - 11.6, hy + 4.6, 1.7, 2.6, '#8a3a4a', rot=-12); a.ell(hx - 6.4, hy + 5.6, 1.7, 2.6, '#8a3a4a', rot=-12)
    a.path('M%.1f %.1f q5 4 10 1' % (hx - 12, hy + 13), stroke=out, sw=.6)
    _eye(a, hx - 9, hy - 6, 2); _eye(a, hx + 4, hy - 5.4, 2)
    a.ell(hx + 9, hy + 3, 4, 2.6, '#f08a9c', .55)
    if ready:
        bx, by = x + 68, y - 4
        a.ell(bx + 3, by + 3, 12, 3.6, '#0e2010', .42, a.blur(1))
        a.path('M%.1f %.1f q-3 -8 3 -12 q6 -4 12 0 q7 3 5 10 q-2 6 -10 6 q-8 1 -10 -4 Z' % (bx - 9, by - 1), a.rg([(0, '#8a6a4a'), (.6, '#5a4028'), (1, '#2e1c0e')], .36, .3, .8), '#1e120a', .5)
        for (dx, dy) in ((-4, -6), (3, -8), (5, -2), (-3, -1), (0, -4.6)):
            a.ell(bx + dx, by + dy, 1.5, 1.1, '#b08a60', .9)
            a.ell(bx + dx + .5, by + dy + .6, 1, .7, '#2e1c0e', .7)


def bee(a, x, y, ready):
    """A straw hive on its stand; ready: honey runs from it and a full jar waits."""
    _ground(a, x, y, 50, 46)
    out = '#5e3c22'
    for lx in (-15, 15):
        a.path('M%.1f %.1f l0 11' % (x + lx, y - 11), stroke=out, sw=4.6)
        a.path('M%.1f %.1f l0 10.4' % (x + lx - .4, y - 11), stroke='#a8703f', sw=2.8)
    a.add('<rect x="%.1f" y="%.1f" width="44" height="6" rx="2" fill="%s" stroke="%s" stroke-width=".5"/>' % (x - 22, y - 16, a.lg([(0, '#d0a068'), (1, '#8a5a34')]), out))
    rows = [(18.5, 9), (20.5, 9), (20.5, 9), (17.5, 8.4), (12, 7)]
    yy = y - 16
    r = rnd(8)
    for i, (w, h) in enumerate(rows):
        col = '#f0b63a' if i % 2 == 0 else '#e2a22a'
        a.add('<rect x="%.1f" y="%.1f" width="%.1f" height="%.1f" rx="%.1f" fill="%s" stroke="#8a5a14" stroke-width=".5"/>' % (
            x - w, yy - h, 2 * w, h + .8, h * .5, a.lg([(0, lite(col, .32)), (.4, col), (1, dark(col, .38))], 0, 0, 1, 0)))
        for k in range(int(w / 1.5)):
            sx = x - w + 2 + k * 3 + r.random()
            a.line((sx, yy - 1.4), (sx + 2, yy - h + 1.4), '#a8700e', .4, .8)
        a.path('M%.1f %.1f h%.1f' % (x - w + 3.4, yy - h + 1.8, 2 * w - 10), stroke='#ffe08a', sw=.8, op=.8)
        yy -= h - .7
    a.ell(x, y - 23, 4.6, 5.4, '#3e2408')
    a.add('<rect x="%.1f" y="%.1f" width="14" height="2" rx=".8" fill="#8a5a24" stroke="#4a2a14" stroke-width=".35"/>' % (x - 7, y - 18.6))
    if ready:
        a.path('M%.1f %.1f q1.4 8 4 8 q2.8 0 2 -5.4 q2.8 9.6 6 4' % (x - 11, y - 33), stroke='#b8700a', sw=3.4)
        a.path('M%.1f %.1f q1.4 8 4 8 q2.8 0 2 -5.4 q2.8 9.6 6 4' % (x - 11, y - 33), stroke='#f6b02a', sw=2.2)
        jx, jy = x + 34, y - 2
        a.ell(jx + 2, jy + 2, 10, 3, '#0e2010', .42, a.blur(.9))
        a.add('<rect x="%.1f" y="%.1f" width="16" height="17" rx="4" fill="%s" stroke="#8a5a14" stroke-width=".5"/>' % (jx - 8, jy - 17, a.lg([(0, '#ffd870'), (.4, '#f2a81e'), (1, '#c8800e')], 0, 0, 1, 0)))
        a.add('<rect x="%.1f" y="%.1f" width="18.6" height="4.6" rx="2" fill="#c07a3a" stroke="#6a3a14" stroke-width=".45"/>' % (jx - 9.3, jy - 21))
        a.ell(jx - 3.4, jy - 10, 1.5, 4, '#fff2c0', .85)
    for (dx, dy) in ((-27, -44), (24, -52), (30, -30)):
        bx, by = x + dx, y + dy
        a.ell(bx - 2, by - 3.4, 3.4, 2, '#ffffff', .85, rot=-30, stroke='#9ab0c0', sw=.3)
        a.ell(bx + 2, by - 3.4, 3.4, 2, '#ffffff', .85, rot=30, stroke='#9ab0c0', sw=.3)
        a.ell(bx, by, 4, 3, '#f6c22e', stroke='#3a2408', sw=.5)
        a.path('M%.1f %.1f l0 4.6 M%.1f %.1f l0 4' % (bx - 1, by - 2.4, bx + 1.4, by - 2), stroke='#3a2408', sw=1)


ANIMALS = dict(chicken=chicken, cow=cow, sheep=sheep, pig=pig, bee=bee)


def animal(kind, ready):
    w, h, fx, foot = SIZES[kind]
    a = Art(w, h, fx, h - foot)
    a.seed = len(kind) + (3 if ready else 0)
    a.look = dict(volume=.1)
    ANIMALS[kind](a, a.ox, a.oy, ready)
    return a


def build():
    out = {}
    for k in ANIMALS:
        out['Animal_%s' % k] = animal(k, False).save('Animal_%s' % k)
        out['Animal_%s_ready' % k] = animal(k, True).save('Animal_%s_ready' % k)
    return out
