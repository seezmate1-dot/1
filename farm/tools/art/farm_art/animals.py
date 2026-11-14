"""Animals (ART_BIBLE §4–§6): drawn at their real size on the map — a hen 44 high, a sheep 60, a pig 52, a cow 96 —
each with its own build and outline of back and head. Every part of the body (far legs, barrel, near legs, head,
muzzle, ears) is its own layer: lit from the top left as a soft solid, throwing a soft shadow on the parts behind
it. Each stands on its own shadow, which touches its feet. Each comes plain and with its product beside it."""
import math
from iso import Art, lite, dark, mix, rnd, smooth, ribbon

# canvas (map units) and where the feet are on it, from the left and from the bottom
SIZES = dict(chicken=(96, 88, 40, 26), sheep=(150, 122, 64, 38), pig=(160, 108, 68, 34), cow=(214, 172, 96, 50), bee=(120, 118, 46, 32))

DROP = dict(dx=1.6, dy=2.4, sd=2.2, op=.34, col='#2a1408')


def _ground(a, x, y, w, feet=()):
    """The shadow: a soft pool right under the body, a little to the right, and a dark touch under every foot."""
    a.ell(x + w * .1, y + 2.5, w * .56, w * .15, '#0e2010', .36, a.blur(3))
    a.ell(x + w * .04, y + 1.5, w * .38, w * .08, '#0e2010', .3, a.blur(1.6))
    for (fx, fw) in feet:
        a.ell(x + fx + 1, y + 1, fw, fw * .36, '#0e2010', .5, a.blur(.9))


def _eye(a, x, y, r=2.6, look=(-.25, .1)):
    """A big shining eye: dark iris, a soft brown rim, two glints, a lid line."""
    a.ell(x, y, r * 1.04, r * 1.14, '#fffdf6', stroke='#5a4030', sw=.3)
    ix, iy = x + look[0] * r, y + look[1] * r
    a.ell(ix, iy, r * .8, r * .9, a.rg([(0, '#4a2c16'), (.7, '#24140a'), (1, '#120a06')], .5, .6, .6))
    a.ell(ix - r * .3, iy - r * .38, r * .34, r * .34, '#ffffff')
    a.ell(ix + r * .3, iy + r * .36, r * .16, r * .16, '#ffffff', .8)
    a.path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (x - r * 1.1, y - r * .5, x, y - r * 1.5, x + r * 1.1, y - r * .5), stroke='#5a4030', sw=.45)


def _leg(a, P, pts, col, hoof, out, hoof_h=6):
    """A leg along (dx, h, width) points down to the ground, with a hoof."""
    d = ribbon([P(dx, h) + (w,) for (dx, h, w) in pts], 6)
    a.path(d, a.lg([(0, lite(col, .12)), (1, dark(col, .2))]), out, .6)
    dx, h, w = pts[-1]
    x, y = P(dx, 0)
    a.path('M%.1f %.1f L%.1f %.1f Q%.1f %.1f %.1f %.1f L%.1f %.1f Z' % (x - w * .5, y - hoof_h, x - w * .62, y - .6, x, y + 1.2, x + w * .62, y - .6, x + w * .5, y - hoof_h),
           a.lg([(0, lite(hoof, .25)), (1, hoof)]), dark(hoof, .5), .4)
    a.path('M%.1f %.1f l0 %.1f' % (x - w * .05, y - hoof_h * .7, hoof_h * .7), stroke=dark(hoof, .6), sw=.5)


def _blush(a, x, y, rx, ry, col='#ff7a90', op=.45):
    a.ell(x, y, rx, ry, col, op, a.blur(1.2))


# ---------------------------------------------------------------------------------------------- hen

def chicken(a, x, y, ready):
    P = lambda dx, h: (x + dx, y - h)
    _ground(a, x + 2, y, 36, feet=((-3, 5), (6, 5)))
    out = '#8a7a66'
    white, cream, shade = '#fffaf0', '#f2e8d4', '#cdbc9e'
    # tail: a fan of feathers rising behind
    a.layer(relief=dict(r=2.5, depth=1.2, lit=.3, shade=.5, fine=.4))
    for i in range(7):
        ang = math.radians(28 + i * 15)
        ln = 15 + (3 - abs(i - 3)) * 2.2
        bx, by = P(13, 24)
        tx, ty = bx + math.cos(ang) * ln, by - math.sin(ang) * ln
        nx, ny = math.sin(ang) * 3.6, math.cos(ang) * 3.6
        a.path('M%.1f %.1f Q%.1f %.1f %.1f %.1f Q%.1f %.1f %.1f %.1f Z' % (bx, by, (bx + tx) / 2 + nx, (by + ty) / 2 + ny, tx, ty, (bx + tx) / 2 - nx, (by + ty) / 2 - ny, bx, by),
               a.lg([(0, white), (1, [shade, cream, white][min(2, i // 2)])]), out, .45)
        a.line((bx, by), ((bx + tx * 3) / 4, (by + ty * 3) / 4), '#c0ae8e', .4)
    # legs
    a.layer(relief=dict(r=1.2, depth=1.2, lit=.4, shade=.5))
    for (lx, lit) in ((7, False), (-2, True)):
        c = '#f09a1e' if lit else '#c97c12'
        fx, fy = P(lx, 0)
        a.path('M%.1f %.1f L%.1f %.1f' % (P(lx + 1, 13)[0], P(lx + 1, 13)[1], fx, fy - 1), stroke='#8a4e08', sw=3)
        a.path('M%.1f %.1f L%.1f %.1f' % (P(lx + 1, 13)[0], P(lx + 1, 13)[1], fx, fy - 1), stroke=c, sw=2)
        for (tx, ty) in ((-6, 1.6), (-2.4, 3), (3.6, 1.8)):
            a.path('M%.1f %.1f l%.1f %.1f' % (fx, fy - 1, tx, ty), stroke='#8a4e08', sw=2.2)
            a.path('M%.1f %.1f l%.1f %.1f' % (fx, fy - 1, tx, ty), stroke=c, sw=1.4)
    # the body: a full round breast, the back rising to the tail
    a.layer(relief=dict(r=7, depth=1.0, lit=.3, shade=.62, rim=.2), drop=DROP)
    body = smooth([P(-14, 30), P(-18, 20), P(-13, 11), P(-2, 8), (P(9, 9)), P(17, 15), P(19, 24), P(14, 30), P(4, 33), P(-7, 34)])
    a.path(body, a.rg([(0, white), (.65, cream), (1, shade)], .32, .26, .85), out, .6)
    r = rnd(2)
    cl = a.clip(body)
    s = ''
    for row in range(5):
        for i in range(7):
            fx, fy = P(-15 + i * 4.6 + (row % 2) * 2.3, 28 - row * 4.2)
            s += '<path d="M%.1f %.1f q2.3 3.2 4.6 0" stroke="#c9b898" stroke-width=".5" fill="none" opacity=".8"/>' % (fx, fy)
    a.group(s, clip=cl)
    # the wing: three rows of feathers
    a.layer(relief=dict(r=3.5, depth=1.1, lit=.3, shade=.55), drop=dict(DROP, op=.28))
    wing = smooth([P(-5, 27), P(4, 29.5), P(13, 26), P(17, 18), P(11, 13), P(2, 13.5), P(-5, 18)])
    a.path(wing, a.lg([(0, white), (1, '#e6d8bc')]), out, .5)
    for row in range(3):
        for i in range(4 - row):
            fx, fy = P(-2.5 + i * 4.6 + row * 2.4, 24.5 - row * 3.6)
            a.path('M%.1f %.1f q2.3 3.2 4.6 0' % (fx, fy), stroke='#b8a888', sw=.55)
    for i in range(3):
        a.path('M%.1f %.1f L%.1f %.1f' % (P(6 + i * 3, 15 + i)[0], P(6 + i * 3, 15 + i)[1], P(13 + i * 1.6, 15.5 + i * 1.5)[0], P(13 + i * 1.6, 15.5 + i * 1.5)[1]), stroke='#b8a888', sw=.5)
    # comb behind the head
    a.layer(relief=dict(r=1.6, depth=1.2, lit=.4, shade=.5))
    hx, hy = P(-11, 39)
    a.path('M%.1f %.1f q-.5 -6.5 3 -4.6 q.6 -5.4 4.2 -2.6 q2.6 -3.4 4.4 .8 q1.4 3 -1 6 Z' % (hx - 5.5, hy - 4), a.lg([(0, '#f06a5a'), (1, '#d8302a')]), '#8a1410', .5)
    # neck and head
    a.layer(relief=dict(r=5, depth=1.0, lit=.3, shade=.55, rim=.2), drop=dict(DROP, op=.25))
    a.path(smooth([P(-16, 28), P(-18, 36), P(-17.6, 42), P(-13, 46.6), P(-7, 46), P(-3.6, 41), P(-4, 34), P(-5, 28), P(-10, 25)]), a.rg([(0, white), (.7, cream), (1, shade)], .35, .3, .85), out, .55)
    for i in range(5):
        fx, fy = P(-15.5 + i * 2.3, 30 - (i % 2) * 1.4)
        a.path('M%.1f %.1f q1.4 2.4 2.8 0' % (fx, fy), stroke='#c0ae8e', sw=.45)
    a.layer()
    # wattle, beak, eye, cheek
    a.path('M%.1f %.1f q-1.8 5 .8 6.4 q3 -.6 2 -6 Z' % (hx - 4.6, hy + 2.4), a.lg([(0, '#f06a5a'), (1, '#c8281e')]), '#8a1410', .45)
    a.path('M%.1f %.1f l-7 2.4 l6.4 2.6 Z' % (hx - 5.6, hy - 1.4), a.lg([(0, '#ffc640'), (1, '#e8901a')]), '#8a5a08', .45)
    a.line((hx - 11.6, hy + 1.1), (hx - 6, hy + 1), '#8a5a08', .4)
    _blush(a, hx + 1.6, hy + 2.2, 2.4, 1.6, '#ff8a8a', .5)
    _eye(a, hx - 2, hy - 1.4, 2.1, look=(-.2, .1))
    if ready:
        nx_, ny_ = x + 33, y - 1
        a.layer(relief=dict(r=3, depth=1, lit=.3, shade=.5, fine=.5))
        a.ell(nx_ + 2, ny_ + 2, 15, 4.6, '#0e2010', .4, a.blur(1.2))
        a.ell(nx_, ny_, 13, 5, '#b88a3a', stroke='#6a4a14', sw=.5)
        r = rnd(5)
        for _ in range(60):
            q = r.random() * 6.283
            a.line((nx_ + math.cos(q) * 13 * (.8 + r.random() * .3), ny_ + math.sin(q) * 5), (nx_ + math.cos(q + .3) * 6, ny_ + math.sin(q) * 2.4 - 1.5), r.choice(['#f2d264', '#d9b03e', '#8a6a14', '#fff0a8']), .7)
        a.layer(relief=dict(r=3, depth=1, lit=.2, shade=.5), drop=DROP)
        a.ell(nx_, ny_ - 5.5, 5.6, 7, a.rg([(0, '#ffffff'), (.7, '#f8eed8'), (1, '#d8c29a')], .35, .28, .8), stroke='#a88a58', sw=.5)
        a.layer()
        a.ell(nx_ - 1.8, ny_ - 8, 1.4, 2, '#ffffff', .9, rot=-20)


# ---------------------------------------------------------------------------------------------- cow

def cow(a, x, y, ready):
    P = lambda dx, h: (x + dx, y - h)
    _ground(a, x + 8, y, 118, feet=((-30, 8), (40, 8), (-14, 7), (54, 7)))
    out = '#5e4a3a'
    white, mid, shade, patch = '#fbf7ee', '#e9e1d2', '#c2b7a6', '#6a4630'
    hoof = '#3a2a20'
    LEG = dict(r=4.5, depth=1.1, lit=.3, shade=.6)
    # far legs
    a.layer(relief=LEG)
    _leg(a, P, [(-13, 42, 15), (-12, 22, 12), (-13, 5, 12.5)], dark(mid, .22), hoof, out)
    _leg(a, P, [(52, 44, 17), (57, 24, 12), (54, 5, 12.5)], dark(mid, .22), hoof, out)
    a.layer(relief=LEG, drop=dict(DROP, op=.3))
    _leg(a, P, [(-31, 46, 17), (-30, 24, 14), (-31, 5, 14.5)], white, hoof, out, 6.5)
    _leg(a, P, [(40, 48, 21), (46, 26, 14), (42, 5, 14.5)], white, hoof, out, 6.5)
    # tail
    a.layer(relief=dict(r=2, depth=1, lit=.3, shade=.5))
    tl = [P(58, 76) + (5,), P(66, 66) + (3.4,), P(68, 50) + (2.8,), P(66, 38) + (2.6,)]
    a.path(ribbon(tl, 6), mid, out, .5)
    a.path(smooth([P(64, 40), P(62, 30), P(66, 22), P(70, 30), P(69, 40)]), a.lg([(0, patch), (1, dark(patch, .3))]), dark(patch, .5), .4)
    # the barrel
    a.layer(relief=dict(r=15, depth=1.35, lit=.3, shade=.8, rim=.2), drop=DROP)
    body = smooth([P(-44, 78), P(-28, 86), P(0, 88), P(30, 87), P(52, 82), P(63, 68), P(63, 50), P(54, 36), P(36, 31), P(14, 28), P(-10, 29), P(-30, 33), P(-44, 42), P(-50, 58), P(-50, 70)])
    a.path(body, a.lg([(0, white), (.6, mid), (1, shade)]), out, .7)
    cl = a.clip(body)
    s = ''
    for pts in ([P(-14, 90), P(8, 92), P(28, 86), P(30, 70), P(16, 58), P(-2, 62), P(-16, 74)],
                [P(46, 86), P(66, 80), P(68, 58), P(58, 46), P(46, 52), P(40, 68)],
                [P(-34, 52), P(-20, 56), P(-12, 46), P(-18, 34), P(-34, 30), P(-42, 40)],
                [P(14, 40), P(26, 42), P(30, 34), P(22, 26), P(10, 30)]):
        s += '<path d="%s" fill="%s"/>' % (smooth(pts), a.lg([(0, lite(patch, .12)), (1, dark(patch, .25))]))
    s += '<ellipse cx="%.1f" cy="%.1f" rx="52" ry="13" fill="#6a5040" opacity=".4" filter="%s"/>' % (P(10, 30) + (a.blur(5),))
    a.group(s, clip=cl)
    # udder
    a.layer(relief=dict(r=4, depth=1, lit=.3, shade=.5), drop=dict(DROP, op=.2))
    a.path(smooth([P(18, 36), P(16, 28), P(22, 22), P(32, 21), P(40, 26), P(40, 36)]), a.lg([(0, '#fbc8d0'), (1, '#e090a0')]), '#a85a6a', .5)
    for tx in (22, 29, 36):
        a.path('M%.1f %.1f l0 4.6' % (P(tx, 23)[0], P(tx, 23)[1]), stroke='#a85a6a', sw=3.2)
        a.path('M%.1f %.1f l0 4.4' % (P(tx, 23)[0], P(tx, 23)[1]), stroke='#eaa0ae', sw=2.2)
    # ears and horns behind the head
    hx, hy = -54, 78
    a.layer(relief=dict(r=2.6, depth=1.1, lit=.35, shade=.5), drop=dict(DROP, op=.25))
    for (sg, c) in ((-1, white), (1, patch)):
        ex, ey = P(hx + sg * 19, hy + 12)
        e = smooth([P(hx + sg * 13, hy + 14), P(hx + sg * 22, hy + 18), P(hx + sg * 29, hy + 13), P(hx + sg * 22, hy + 7), P(hx + sg * 13, hy + 9)])
        a.path(e, c, out, .5)
        a.path(smooth([P(hx + sg * 16, hy + 13), P(hx + sg * 22, hy + 15.5), P(hx + sg * 26, hy + 12.6), P(hx + sg * 21, hy + 9.6)]), '#f0b0bc')
    for sg in (-1, 1):
        a.path(ribbon([P(hx + sg * 8, hy + 18) + (5,), P(hx + sg * 12, hy + 25) + (3.8,), P(hx + sg * 11, hy + 31) + (1.2,)], 6), a.lg([(0, '#fff6dc'), (1, '#d8c498')]), '#8a7a5a', .5)
    # the head: broad brow, long face
    a.layer(relief=dict(r=10, depth=1.0, lit=.3, shade=.6, rim=.2), drop=DROP)
    head = smooth([P(hx - 15, hy + 18), P(hx, hy + 22), P(hx + 15, hy + 18), P(hx + 19, hy + 4), P(hx + 15, hy - 12), P(hx + 8, hy - 20), P(hx - 8, hy - 20), P(hx - 15, hy - 12), P(hx - 19, hy + 4)])
    a.path(head, a.lg([(0, white), (1, mid)]), out, .65)
    a.group('<path d="%s" fill="%s"/>' % (smooth([P(hx + 2, hy + 24), P(hx + 20, hy + 20), P(hx + 22, hy + 2), P(hx + 14, hy - 6), P(hx + 5, hy - 2), P(hx + 1, hy + 10)]), patch), clip=a.clip(head))
    # forelock
    for k in range(5):
        a.path('M%.1f %.1f q%.1f 3 %.1f 7' % (P(hx - 6 + k * 3, hy + 22)[0], P(hx - 6 + k * 3, hy + 22)[1], (k - 2) * .8, (k - 2) * 1.2), stroke='#f4ead6', sw=2.2)
    # the muzzle
    a.layer(relief=dict(r=6, depth=1.0, lit=.3, shade=.5, rim=.15), drop=dict(DROP, op=.25))
    a.path(smooth([P(hx - 15, hy - 8), P(hx, hy - 4), P(hx + 15, hy - 8), P(hx + 16, hy - 17), P(hx + 9, hy - 24), P(hx - 9, hy - 24), P(hx - 16, hy - 17)]), a.lg([(0, '#fbd0d4'), (1, '#e8a2ac')]), '#a85a6a', .55)
    a.layer()
    for sg in (-1, 1):
        a.ell(P(hx + sg * 6.5, 0)[0], P(0, hy - 13)[1], 2.2, 3, '#9a4e5e', rot=sg * 14)
        a.ell(P(hx + sg * 6.5, 0)[0] - .4, P(0, hy - 13.6)[1], 1, 1.4, '#5e2a36', rot=sg * 14)
    a.path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (P(hx - 7, hy - 19.6) + P(hx, hy - 22.6) + P(hx + 7, hy - 19.6)), stroke='#a85a6a', sw=.8)
    _blush(a, P(hx - 13, 0)[0], P(0, hy - 4)[1], 4, 2.6)
    _blush(a, P(hx + 13, 0)[0], P(0, hy - 4)[1], 4, 2.6)
    _eye(a, *P(hx - 8.5, hy + 4), r=3.5)
    _eye(a, *P(hx + 8.5, hy + 4), r=3.5)
    if ready:
        bx, by = x + 90, y - 2
        a.ell(bx + 3, by + 2, 15, 4.4, '#0e2010', .42, a.blur(1.2))
        a.layer(relief=dict(r=5, depth=1.2, lit=.4, shade=.6, rim=.3))
        a.path('M%.1f %.1f l3 -22 l20 0 l3 22 q-13 4.6 -26 0Z' % (bx - 13, by), a.lg([(0, '#eef2f6'), (.4, '#c2ccd8'), (1, '#8490a0')], 0, 0, 1, 0), '#56626e', .6)
        for hz in (6, 16):
            a.path('M%.1f %.1f q%.1f 4 %.1f 0' % (bx - 12.4 + hz * .12, by - hz, 12.4 - hz * .12, 24.8 - hz * .24), stroke='#6a7684', sw=1.4)
        a.layer()
        a.ell(bx, by - 22, 10, 3.6, '#7a8694', stroke='#56626e', sw=.5)
        a.ell(bx, by - 21.4, 8.8, 2.9, a.lg([(0, '#ffffff'), (1, '#e8eef4')]))
        a.path('M%.1f %.1f q10 -16 20 0' % (bx - 10, by - 22), stroke='#56626e', sw=1.4)
        a.path('M%.1f %.1f q10 -16 20 0' % (bx - 10, by - 22.5), stroke='#c2ccd8', sw=.5)


# ---------------------------------------------------------------------------------------------- sheep

def sheep(a, x, y, ready):
    P = lambda dx, h: (x + dx, y - h)
    k = 1.12 if ready else 1.0
    _ground(a, x + 6, y, 78, feet=((-18, 4.5), (22, 4.5), (-8, 4), (31, 4)))
    face, wool, wshade = '#5a4438', '#fbf6ea', '#cfc2a8'
    hoof = '#1e1612'
    a.layer(relief=dict(r=2.4, depth=1.1, lit=.3, shade=.55))
    for (lx, far) in ((-8, True), (31, True)):
        _leg(a, P, [(lx, 22, 7), (lx, 10, 5), (lx, 3, 5.4)], '#3a2c24', hoof, '#140e0a', 3.4)
    a.layer(relief=dict(r=2.4, depth=1.1, lit=.35, shade=.55))
    for lx in (-18, 22):
        _leg(a, P, [(lx, 22, 8), (lx, 10, 5.6), (lx, 3, 6)], '#4e3c32', hoof, '#140e0a', 3.6)
    r = rnd(3)
    # the fleece: a cloud of round locks, the lower ones in shade
    cx, cy, rx, ry = 7, 20 + 19 * k, 30 * k, 18 * k
    locks = []
    for i in range(15):
        q = 6.283 * i / 15 + r.uniform(-.1, .1)
        locks.append((cx + math.cos(q) * rx, cy + math.sin(q) * ry, (8.6 + r.random() * 3) * k))
    a.layer(relief=dict(r=11 * k, depth=.9, lit=.25, shade=.7, rim=.2, fine=.3), drop=DROP)
    d = ''
    for (lx, ly, lr) in locks:
        px, py = P(lx, ly)
        d += '<circle cx="%.1f" cy="%.1f" r="%.1f"/>' % (px, py, lr)
    px, py = P(cx, cy)
    a.add('<g fill="%s" stroke="#a89a80" stroke-width="1">%s<ellipse cx="%.1f" cy="%.1f" rx="%.1f" ry="%.1f"/></g>' % (wool, d, px, py, rx, ry))
    a.add('<g fill="%s">%s<ellipse cx="%.1f" cy="%.1f" rx="%.1f" ry="%.1f"/></g>' % (a.lg([(0, '#ffffff'), (.55, wool), (1, '#ddd0b8')]), d, px, py, rx, ry))
    # locks on the body: each a little dome with its own curl
    a.layer(relief=dict(r=2.6, depth=.8, lit=.3, shade=.42, fine=.2))
    for _ in range(int(46 * k)):
        q, dd = r.random() * 6.283, math.sqrt(r.random()) * .98
        lx, ly = cx + math.cos(q) * dd * rx * 1.08, cy + math.sin(q) * dd * ry * 1.1
        px, py = P(lx, ly)
        rr = (4.2 + r.random() * 2.6) * k
        low = (cy - ly) / ry
        a.ell(px, py, rr, rr * .9, wool if low < .2 else mix(wool, '#d8ccb4', min(1, low)), stroke='#b8aa90', sw=.35, op=.95)
        a.path('M%.1f %.1f q%.1f -%.1f %.1f 0 q-%.1f %.1f -%.1f %.1f' % (px - rr * .5, py + rr * .1, rr * .5, rr * .7, rr, rr * .2, rr * .5, rr * .6, rr * .2), stroke='#c4b69c', sw=.45, op=.8)
    # the head: ears, a dark soft face, a cap of wool
    hx, hy = -29 * k + 2, 40 * k + 2
    a.layer(relief=dict(r=2.4, depth=1.1, lit=.35, shade=.5), drop=dict(DROP, op=.25))
    for sg in (-1, 1):
        e = smooth([P(hx + sg * 7, hy + 6), P(hx + sg * 14, hy + 7.5), P(hx + sg * 19, hy + 3), P(hx + sg * 14, hy + 1), P(hx + sg * 7, hy + 2)])
        a.path(e, face, '#1e1410', .45)
        a.path(smooth([P(hx + sg * 9, hy + 5), P(hx + sg * 14, hy + 5.8), P(hx + sg * 17, hy + 3.2), P(hx + sg * 13, hy + 2.6)]), '#b08a80')
    a.layer(relief=dict(r=6.5, depth=1.0, lit=.36, shade=.55, rim=.22), drop=DROP)
    a.path(smooth([P(hx - 9, hy + 9), P(hx, hy + 12), P(hx + 9, hy + 9), P(hx + 11, hy), P(hx + 8, hy - 10), P(hx + 3, hy - 15), P(hx - 3, hy - 15), P(hx - 8, hy - 10), P(hx - 11, hy)]),
           a.lg([(0, lite(face, .16)), (1, dark(face, .2))]), '#1e1410', .5)
    a.layer(relief=dict(r=3, depth=.9, lit=.3, shade=.45), drop=dict(DROP, op=.25))
    for (dx, dy, rr) in ((-6.5, 9.5, 5), (0, 12, 6), (6.5, 9.5, 5), (-3, 14.5, 4), (3.4, 14.6, 4)):
        a.ell(*P(hx + dx, hy + dy), rr, rr * .9, a.rg([(0, '#ffffff'), (1, '#e6dcc6')], .36, .3, .8), stroke='#b8aa90', sw=.4)
    a.layer()
    _eye(a, *P(hx - 5, hy + 1.6), r=2.9)
    _eye(a, *P(hx + 5, hy + 1.6), r=2.9)
    nx, ny = P(hx, hy - 9.5)
    a.path('M%.1f %.1f l2.4 2.6 l2.4 -2.6 Z' % (nx - 2.4, ny), '#e8a0aa', '#8a4a58', .4)
    a.path('M%.1f %.1f l0 2 M%.1f %.1f q-2 1.8 -3.6 .4 M%.1f %.1f q2 1.8 3.6 .4' % (nx, ny + 2.6, nx, ny + 4.6, nx, ny + 4.6), stroke='#2a1c16', sw=.6)
    _blush(a, nx - 7, ny - 3.5, 2.6, 1.6, '#ff9aa0', .4)
    _blush(a, nx + 7, ny - 3.5, 2.6, 1.6, '#ff9aa0', .4)
    if ready:
        bx, by = x + 56, y - 4
        a.ell(bx + 3, by + 3, 15, 4.4, '#0e2010', .4, a.blur(1.2))
        a.layer(relief=dict(r=4.5, depth=.9, lit=.25, shade=.6, fine=.3), drop=DROP)
        for (dx, dy, rr) in ((-7, -2, 7.4), (6, -3, 8), (0, -10, 7.4), (-1, 1, 6.4), (-9, -8, 5), (9, -9, 5)):
            a.ell(bx + dx, by + dy, rr, rr * .9, a.rg([(0, '#ffffff'), (1, '#e6dcc6')], .36, .3, .8), stroke='#b8aa90', sw=.4)
            a.path('M%.1f %.1f q%.1f -%.1f %.1f 0' % (bx + dx - rr * .5, by + dy, rr * .5, rr * .6, rr), stroke='#c4b69c', sw=.5)


# ---------------------------------------------------------------------------------------------- pig

def pig(a, x, y, ready):
    P = lambda dx, h: (x + dx, y - h)
    _ground(a, x + 8, y, 92, feet=((-22, 6), (30, 6), (-8, 5), (42, 5)))
    out, pink, light, shade = '#9a4a5a', '#f2a6b4', '#fbc8d0', '#cc7a8c'
    hoof = '#7a4048'
    LEG = dict(r=3.4, depth=1.1, lit=.3, shade=.55)
    a.layer(relief=LEG)
    _leg(a, P, [(-8, 22, 11), (-8, 10, 8), (-8, 3, 8.4)], dark(pink, .2), hoof, out, 4)
    _leg(a, P, [(42, 22, 12), (43, 10, 8), (42, 3, 8.4)], dark(pink, .2), hoof, out, 4)
    a.layer(relief=LEG, drop=dict(DROP, op=.3))
    _leg(a, P, [(-22, 24, 12), (-22, 10, 9.4), (-22, 3, 9.8)], light, hoof, out, 4.4)
    _leg(a, P, [(30, 25, 13), (31, 10, 9.4), (30, 3, 9.8)], light, hoof, out, 4.4)
    # the curl of the tail
    a.layer(relief=dict(r=1.3, depth=1.1, lit=.4, shade=.5))
    tx, ty = P(48, 38)
    for (c, w) in ((out, 3.4), (pink, 2.2)):
        a.path('M%.1f %.1f q8 -1 8 -7 q-1 -6 -6 -4 q-4 3 0 7 q5 3 9 -1' % (tx, ty), stroke=c, sw=w)
    # the barrel: long, low and round
    a.layer(relief=dict(r=13, depth=1.35, lit=.3, shade=.8, rim=.22), drop=DROP)
    body = smooth([P(-32, 46), P(-16, 52), P(6, 54), P(28, 53), P(44, 47), P(53, 36), P(53, 25), P(45, 16), P(30, 12), P(6, 10.5), P(-18, 12), P(-33, 18), P(-40, 30), P(-39, 40)])
    a.path(body, a.lg([(0, light), (.55, pink), (1, shade)]), out, .7)
    r = rnd(4)
    s = ''
    for _ in range(46):
        bx_, by_ = P(-28 + r.random() * 74, 18 + r.random() * 32)
        s += '<path d="M%.1f %.1f q1.4 .2 2.6 1.6" stroke="%s" stroke-width=".45" fill="none" opacity=".8"/>' % (bx_, by_, '#ffe0e4' if r.random() < .6 else '#d88a98')
    s += '<ellipse cx="%.1f" cy="%.1f" rx="44" ry="10" fill="#a04a60" opacity=".45" filter="%s"/>' % (P(8, 12) + (a.blur(4),))
    a.group(s, clip=a.clip(body))
    # the head: round, with heavy cheeks; ears flopping forward; a flat snout
    hx, hy = -36, 34
    a.layer(relief=dict(r=9.5, depth=1.0, lit=.3, shade=.58, rim=.22), drop=DROP)
    a.path(smooth([P(hx - 14, hy + 15), P(hx, hy + 19), P(hx + 14, hy + 15), P(hx + 20, hy + 3), P(hx + 17, hy - 10), P(hx + 6, hy - 17), P(hx - 8, hy - 17), P(hx - 18, hy - 10), P(hx - 21, hy + 3)]),
           a.rg([(0, light), (.6, pink), (1, shade)], .36, .3, .85), out, .65)
    a.layer(relief=dict(r=3, depth=1.1, lit=.35, shade=.55), drop=dict(DROP, op=.3))
    for sg in (-1, 1):
        e = smooth([P(hx + sg * 8, hy + 15), P(hx + sg * 15, hy + 22), P(hx + sg * 22, hy + 17), P(hx + sg * 21, hy + 8), P(hx + sg * 15, hy + 5), P(hx + sg * 10, hy + 9)])
        a.path(e, a.lg([(0, pink), (1, shade)]), out, .5)
        a.path(smooth([P(hx + sg * 12, hy + 14), P(hx + sg * 16, hy + 18), P(hx + sg * 19.5, hy + 14), P(hx + sg * 17, hy + 9)]), '#e88a9c', op=.8)
    a.layer(relief=dict(r=4, depth=1.1, lit=.35, shade=.5, rim=.2), drop=dict(DROP, op=.3))
    sx, sy = P(hx - 6, hy - 5)
    a.ell(sx, sy, 10, 7.6, a.lg([(0, '#ffd6dc'), (1, '#ea96a6')]), stroke=out, sw=.6, rot=-6)
    a.layer()
    for sg in (-1, 1):
        a.ell(sx + sg * 3.6, sy + .2, 1.7, 2.7, '#8a3a4a', rot=-6)
    a.path('M%.1f %.1f q6 4.6 12 1' % (sx - 5, sy + 9.6), stroke=out, sw=.8)
    _eye(a, *P(hx - 12, hy + 6), r=3.1)
    _eye(a, *P(hx + 5.5, hy + 6), r=3.1)
    _blush(a, *P(hx + 12, hy - 4), 4.4, 2.8, '#ff7a94', .5)
    _blush(a, *P(hx - 18, hy - 3), 2.6, 2.4, '#ff7a94', .4)
    if ready:
        bx, by = x + 72, y - 4
        a.ell(bx + 3, by + 3, 13, 4, '#0e2010', .42, a.blur(1.1))
        a.layer(relief=dict(r=5, depth=1.1, lit=.3, shade=.6, fine=.4))
        a.path(smooth([(bx - 10, by - 2), (bx - 11, by - 9), (bx - 5, by - 15), (bx + 3, by - 15.6), (bx + 10, by - 11), (bx + 11, by - 4), (bx + 5, by + 1.6), (bx - 4, by + 2)]),
               a.rg([(0, '#8a6a4a'), (.6, '#5a4028'), (1, '#2e1c0e')], .36, .3, .8), '#1e120a', .5)
        a.layer()
        r = rnd(9)
        for _ in range(16):
            dx, dy = r.uniform(-8, 8), r.uniform(-13, 0)
            a.ell(bx + dx + .5, by + dy + .6, 1.3, .9, '#2e1c0e', .7)
            a.ell(bx + dx, by + dy, 1.3, .9, '#b08a60', .85)


# ---------------------------------------------------------------------------------------------- hive

def bee(a, x, y, ready):
    """A straw hive on its stand; ready: honey runs from it and a full jar waits."""
    _ground(a, x + 3, y, 52, feet=((-15, 4), (15, 4)))
    out = '#5e3c22'
    a.layer(relief=dict(r=2.4, depth=1.2, lit=.4, shade=.6, fine=.3))
    for lx in (-15, 15):
        a.add('<rect x="%.1f" y="%.1f" width="5" height="12" rx="1" fill="#a8703f" stroke="%s" stroke-width=".5"/>' % (x + lx - 2.5, y - 12, out))
    a.add('<rect x="%.1f" y="%.1f" width="46" height="6.4" rx="2" fill="%s" stroke="%s" stroke-width=".5"/>' % (x - 23, y - 17, a.lg([(0, '#d0a068'), (1, '#8a5a34')]), out))
    for i in range(5):
        a.line((x - 20 + i * 9, y - 15.6), (x - 14 + i * 9, y - 15.2), '#7a4a24', .4, .7)
    rows = [(19, 9), (21, 9), (21, 9), (18.5, 8.6), (14, 7.6), (8, 6)]
    yy = y - 16.6
    r = rnd(8)
    a.layer(relief=dict(r=3.2, depth=1.15, lit=.36, shade=.62, rim=.2, fine=.35), drop=DROP)
    for i, (w, h) in enumerate(rows):
        col = '#f0b63a' if i % 2 == 0 else '#e6a82e'
        a.add('<rect x="%.1f" y="%.1f" width="%.1f" height="%.1f" rx="%.1f" fill="%s" stroke="#8a5a14" stroke-width=".5"/>' % (
            x - w, yy - h, 2 * w, h + .8, h * .5, a.lg([(0, lite(col, .25)), (.45, col), (1, dark(col, .3))], 0, 0, 1, 0)))
        s = ''
        for k in range(int(w / 1.1)):
            sx = x - w + 2 + k * 2.2 + r.random()
            s += '<path d="M%.1f %.1f q1.6 -%.1f 2.2 -%.1f" stroke="%s" stroke-width=".45" fill="none" opacity=".75"/>' % (sx, yy - 1.2, h * .5, h - 2.4, r.choice(['#a8700e', '#c08a1c', '#ffe08a']))
        a.add(s)
        yy -= h - .8
    a.layer()
    a.ell(x, y - 24, 5, 5.8, a.rg([(0, '#5a3410'), (1, '#2a1604')], .5, .7, .7), stroke='#8a5a14', sw=.5)
    a.add('<rect x="%.1f" y="%.1f" width="15" height="2.4" rx="1" fill="#9a6a34" stroke="#4a2a14" stroke-width=".35"/>' % (x - 7.5, y - 19.4))
    if ready:
        a.layer(relief=dict(r=1.6, depth=1.2, lit=.6, shade=.4))
        a.path('M%.1f %.1f q1.4 9 4.4 9 q3 0 2.2 -6 q3 10.6 6.4 4.4' % (x - 12, y - 35), stroke='#b8700a', sw=3.8)
        a.path('M%.1f %.1f q1.4 9 4.4 9 q3 0 2.2 -6 q3 10.6 6.4 4.4' % (x - 12, y - 35), stroke='#f8b62e', sw=2.6)
        jx, jy = x + 36, y - 2
        a.layer()
        a.ell(jx + 2, jy + 2, 11, 3.2, '#0e2010', .42, a.blur(1))
        a.layer(relief=dict(r=3.6, depth=1.1, lit=.4, shade=.5, rim=.3))
        a.add('<rect x="%.1f" y="%.1f" width="17" height="18" rx="4.4" fill="%s" stroke="#8a5a14" stroke-width=".5"/>' % (jx - 8.5, jy - 18, a.lg([(0, '#ffd870'), (.4, '#f2a81e'), (1, '#c8800e')], 0, 0, 1, 0)))
        a.add('<rect x="%.1f" y="%.1f" width="19.6" height="5" rx="2" fill="#c07a3a" stroke="#6a3a14" stroke-width=".45"/>' % (jx - 9.8, jy - 22.4))
        a.layer()
        a.add('<rect x="%.1f" y="%.1f" width="9" height="7" rx="1" fill="#fff6dc" stroke="#b08a50" stroke-width=".3"/>' % (jx - 4.5, jy - 13))
        a.ell(jx, jy - 9.5, 2, 1.6, '#f2a81e')
        a.ell(jx - 5.6, jy - 10, 1.1, 5, '#fff2c0', .8)
    a.layer()
    for (dx, dy, rot) in ((-27, -46, -10), (25, -55, 14), (32, -32, -6)):
        bx, by = x + dx, y + dy
        s = '<ellipse cx="-2.4" cy="-4" rx="4" ry="2.4" fill="#ffffff" opacity=".8" stroke="#9ab0c0" stroke-width=".3" transform="rotate(-32 -2.4 -4)"/>'
        s += '<ellipse cx="2" cy="-4.4" rx="4" ry="2.4" fill="#ffffff" opacity=".8" stroke="#9ab0c0" stroke-width=".3" transform="rotate(24 2 -4.4)"/>'
        s += '<ellipse cx="0" cy="0" rx="4.8" ry="3.5" fill="%s" stroke="#3a2408" stroke-width=".5"/>' % a.rg([(0, '#ffe46a'), (1, '#f0a81e')], .35, .3, .8)
        s += '<path d="M-.8 -3.3 q-1 3.3 0 6.6 M1.8 -3 q-1 3 0 6" stroke="#3a2408" stroke-width="1.2" fill="none"/>'
        s += '<circle cx="-3.6" cy="-.4" r="1.9" fill="#3a2408"/><circle cx="-4.2" cy="-.9" r=".55" fill="#ffffff"/>'
        a.group(s, transform='translate(%.1f %.1f) rotate(%d)' % (bx, by, rot))


ANIMALS = dict(chicken=chicken, cow=cow, sheep=sheep, pig=pig, bee=bee)


def animal(kind, ready):
    w, h, fx, foot = SIZES[kind]
    a = Art(w, h, fx, h - foot)
    a.seed = len(kind) + (3 if ready else 0)
    a.look = dict(volume=.06, grain=.025)
    ANIMALS[kind](a, a.ox, a.oy, ready)
    return a


def build():
    out = {}
    for k in ANIMALS:
        out['Animal_%s' % k] = animal(k, False).save('Animal_%s' % k)
        out['Animal_%s_ready' % k] = animal(k, True).save('Animal_%s_ready' % k)
    return out


if __name__ == '__main__':
    import sys
    from PIL import Image
    ks = sys.argv[2].split(',') if len(sys.argv) > 2 else list(ANIMALS)
    ims = [[animal(k, rd).image() for k in ks] for rd in (False, True)]
    W = sum(im.width for im in ims[0])
    H = max(im.height for im in ims[0])
    sheet = Image.new('RGBA', (W, H * 2), (121, 185, 78, 255))
    for j, row in enumerate(ims):
        xx = 0
        for im in row:
            sheet.alpha_composite(im, (xx, j * H + H - im.height))
            xx += im.width
    sheet.convert('RGB').save(sys.argv[1], quality=92)
