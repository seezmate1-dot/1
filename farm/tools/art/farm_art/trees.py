"""Fruit trees (ART_BIBLE §8, §9): three species with their own build, two variants of each, three states.

apple  — a broad, rounded, uneven crown on a short gnarled trunk that forks low;
pear   — a tall, upright, pyramidal crown on a straight trunk;
cherry — a wide spreading umbrella on a smooth red-brown trunk with horizontal lenticels.

A crown is a few leaf masses, each lit from the top left in four grouped steps of tone; the dark inside and the
limbs show between them. Blossom and fruit sit in the masses, under the lit leaves. The crown's shadow lies on
the ground to the right and a little down.
"""
import math
from iso import Art, lite, dark, mix, rnd

TW, TH = 300, 310
OX, FOOT = 112, 62
SHADOW = (.566, .198)        # where the shadow of a point lands, per unit of its height (0.6 long, to the right and down)

SPECIES = {
    'apple_tree': dict(
        ramp=['#0f3a1c', '#2f7a2c', '#4f9a36', '#86c248', '#b8dc58'], bark=('#3e2a1c', '#7a5236', '#a87a52'), blossom='#ffe6ee', shade='#d8a8c0',
        trunk=60, lean=4, fork=32,
        masses=[[(-52, 104, 33), (-8, 136, 36), (42, 116, 35), (62, 86, 26), (-24, 86, 31), (18, 80, 30)],
                [(-46, 90, 32), (-30, 128, 31), (14, 140, 34), (54, 108, 33), (2, 90, 34), (-66, 70, 22)]]),
    'pear_tree': dict(
        ramp=['#1e4a1c', '#4f8a2e', '#7aa83a', '#a8cc50', '#d2e468'], bark=('#3a2c22', '#6e5642', '#9a8068'), blossom='#ffffff', shade='#c8ccdc',
        trunk=62, lean=0, fork=46,
        masses=[[(0, 176, 20), (-12, 146, 27), (14, 136, 27), (-24, 110, 30), (22, 100, 31), (-4, 80, 32)],
                [(4, 180, 19), (12, 150, 26), (-16, 138, 27), (26, 112, 29), (-22, 104, 30), (6, 82, 33)]]),
    'cherry_tree': dict(
        ramp=['#0c3420', '#256e38', '#3f8a4a', '#74b852', '#a6d860'], bark=('#4a1e16', '#8a3e2c', '#b8604a'), blossom='#ffc6dc', shade='#d07aa4',
        trunk=54, lean=-3, fork=34,
        masses=[[(-74, 70, 27), (-38, 92, 32), (6, 98, 34), (50, 86, 31), (80, 62, 24), (-8, 66, 28)],
                [(-80, 58, 24), (-46, 84, 31), (-4, 100, 33), (42, 96, 32), (76, 74, 26), (16, 66, 28)]]),
}


def _leaf(a, x, y, ln, wd, ang, col, vein=None):
    t = math.radians(ang)
    dx, dy = math.cos(t), -math.sin(t)
    nx, ny = -dy, dx
    tx, ty = x + dx * ln, y + dy * ln
    mx, my = x + dx * ln * .45, y + dy * ln * .45
    a.path('M%.1f %.1f Q%.1f %.1f %.1f %.1f Q%.1f %.1f %.1f %.1f Z' % (x, y, mx + nx * wd, my + ny * wd, tx, ty, mx - nx * wd, my - ny * wd, x, y), col)
    if vein:
        a.line((x, y), (x + dx * ln * .85, y + dy * ln * .85), vein, .35, .7)


def _limb(a, p0, p1, w0, w1, bark, bend=0):
    """A tapering limb from p0 to p1 with a lit left side."""
    mx, my = (p0[0] + p1[0]) / 2 + bend, (p0[1] + p1[1]) / 2 - abs(bend) * .3
    for (w, c, off) in ((1.0, bark[0], 0), (.62, bark[1], -.5), (.22, bark[2], -1.1)):
        a.path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (p0[0] + off * w0 * .3, p0[1], mx + off * w0 * .25, my, (p0[0] + p1[0]) / 2 + bend * .5 + off, (p0[1] + p1[1]) / 2 - abs(bend) * .15),
               stroke=c, sw=w0 * w, cap='round')
        a.path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % ((p0[0] + p1[0]) / 2 + bend * .5 + off, (p0[1] + p1[1]) / 2 - abs(bend) * .15, (mx + p1[0]) / 2 + off, (my + p1[1]) / 2, p1[0], p1[1]),
               stroke=c, sw=max(1, (w0 + w1) / 2 * w), cap='round')


def _trunk(a, kind, sp, x, y, r, tops):
    """The trunk with its root flare, the fork, and limbs up to the masses (tops: where they go)."""
    bark = sp['bark']
    h, lean, fork = sp['trunk'], sp['lean'], sp['fork']
    wb = 13 if kind == 'apple_tree' else 10 if kind == 'pear_tree' else 11
    # roots: lobes spreading on the ground
    for (dx, w, ln) in ((-1, 7, 20), (1, 6.5, 22), (-.35, 5, 12), (.5, 5, 13)):
        ex, ey = x + dx * ln, y + 3 + abs(dx) * 2
        a.path('M%.1f %.1f Q%.1f %.1f %.1f %.1f L%.1f %.1f Q%.1f %.1f %.1f %.1f Z' % (x + dx * 3, y - 14, x + dx * ln * .5, y - 3, ex, ey, ex - dx * 2, ey + 1.6, x + dx * ln * .4, y + 3, x - dx * 2, y + 2),
               a.lg([(0, bark[2 if dx < 0 else 1]), (1, bark[0])]))
    fx, fy = x + lean, y - fork
    # the bole: wider at the foot, a little crooked for the apple
    k = 5 if kind == 'apple_tree' else 0
    g = a.lg([(0, bark[2]), (.38, bark[1]), (1, bark[0])], 0, 0, 1, 0)
    a.path('M%.1f %.1f Q%.1f %.1f %.1f %.1f L%.1f %.1f Q%.1f %.1f %.1f %.1f Z' % (
        x - wb, y + 1, x - wb * .5 - k, y - fork * .5, fx - wb * .46, fy, fx + wb * .46, fy, x + wb * .55 - k * .4, y - fork * .5, x + wb, y + 1), g)
    # bark
    if kind == 'cherry_tree':
        a.path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (x - wb * .45, y - 4, x - wb * .3, y - fork * .5, fx - wb * .22, fy), stroke=lite(bark[2], .35), sw=1.6, op=.7)
        for i in range(int(fork / 4.2)):
            yy = y - 5 - i * 4.2
            w = wb * (.8 - .4 * i / (fork / 4.2))
            cx = x + lean * (i * 4.2 / fork)
            a.line((cx - w * (.2 + r.random() * .5), yy), (cx + w * (.1 + r.random() * .6), yy + .4), lite(bark[2], .3), .9, .9)
            a.line((cx - w * .6, yy + 1.1), (cx + w * .5, yy + 1.4), bark[0], .5, .6)
    else:
        n = 26 if kind == 'apple_tree' else 34
        for i in range(n):
            t = r.random()
            bx = x + lean * t + (r.random() - .5) * wb * (1.5 - t * .7)
            by = y - 2 - t * fork
            ln = (9 if kind == 'apple_tree' else 6) + r.random() * 8
            a.path('M%.1f %.1f q%.1f -%.1f 0 -%.1f' % (bx, by, r.random() * 3 - 1.5, ln / 2, ln), stroke=bark[0], sw=.8 if kind == 'apple_tree' else .55, op=.9)
            a.path('M%.1f %.1f q%.1f -%.1f 0 -%.1f' % (bx - 1.4, by - 1, r.random() * 2 - 1, ln / 3, ln * .7), stroke=bark[2], sw=.5, op=.8)
        if kind == 'apple_tree':
            a.ell(x + 3, y - fork * .55, 3.2, 4, bark[0])
            a.ell(x + 2.6, y - fork * .55 - .4, 1.8, 2.4, mix(bark[0], '#1a0e08', .5))
    # the fork and the limbs: the thickest to the big masses, thinner to the outer ones
    order = sorted(range(len(tops)), key=lambda i: -tops[i][2])
    for n_, i in enumerate(order[:5]):
        tx, ty, rr = tops[i]
        w0 = wb * (.86 - n_ * .11)
        _limb(a, (fx, fy + 1), (tx, ty + rr * .25), w0, max(1.6, w0 * .32), bark, bend=(tx - fx) * .16)
    return fx, fy


def _fruit(a, kind, x, y, k, shaded, r):
    """One fruit hanging on its stem at (x, y): the top of the fruit; k is its size."""
    stem = '#5a3a1e'
    if kind == 'apple_tree':
        c = '#b82a24' if shaded else '#e23a2e'
        a.line((x, y - 5 * k), (x + r.random() * 2 - 1, y), stem, 1.1)
        a.ell(x, y + 6 * k, 6.6 * k, 6.2 * k, a.rg([(0, '#ff9a84' if not shaded else '#d8584c'), (.5, c), (1, dark(c, .45))], .34, .28, .8), stroke=dark(c, .55), sw=.4)
        a.path('M%.1f %.1f q%.1f 1.6 %.1f 0' % (x - 2.4 * k, y + .6 * k, 2.4 * k, 4.8 * k), stroke=dark(c, .5), sw=.7)
        if not shaded:
            a.ell(x - 2.4 * k, y + 3.6 * k, 1.9 * k, 1.3 * k, '#ffe0d6', .9, rot=-30)
        elif r.random() < .5:
            a.ell(x + 1.6 * k, y + 7 * k, 3 * k, 3.4 * k, '#8a9a30', .35)
    elif kind == 'pear_tree':
        c = '#c8a826' if shaded else '#f0d23c'
        a.line((x, y - 6 * k), (x, y), stem, 1.1)
        a.path('M%.1f %.1f q%.1f 0 %.1f %.1f q%.1f %.1f 0 %.1f q%.1f %.1f %.1f 0 q%.1f %.1f 0 %.1f q0 %.1f %.1f %.1f Z' % (
            x, y, 3.2 * k, 3.2 * k, 6 * k, 4.6 * k, 5.6 * k, 9.4 * k, -4.6 * k, 3.4 * k, -9.4 * k, -4.6 * k, -5.4 * k, -9.4 * k, -6 * k, 3.4 * k, -6 * k),
               a.rg([(0, '#fff6a8' if not shaded else '#e2c84a'), (.5, c), (1, dark(c, .45))], .34, .3, .85), dark(c, .55), .4)
        if not shaded:
            a.ell(x - 2 * k, y + 10 * k, 1.5 * k, 3 * k, '#fffbd0', .85, rot=10)
        for _ in range(3):
            a.ell(x + (r.random() - .5) * 5 * k, y + (8 + r.random() * 7) * k, .4, .4, dark(c, .5), .8)
    else:
        c = '#8e1230' if shaded else '#c2183a'
        jx, jy = x, y - 3
        for (dx, ln) in ((-4.4, 11), (3.8, 13)):
            cx_, cy_ = x + dx * k, y + ln * k
            a.path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (jx, jy, jx + dx * .3, jy + ln * .5 * k, cx_, cy_ - 3.6 * k), stroke='#6a7a2a', sw=.8)
            a.ell(cx_, cy_, 4 * k, 3.9 * k, a.rg([(0, '#ff8a9a' if not shaded else '#c84a5e'), (.5, c), (1, dark(c, .5))], .34, .28, .8), stroke=dark(c, .6), sw=.35)
            if not shaded:
                a.ell(cx_ - 1.4 * k, cy_ - 1.4 * k, 1.2 * k, .9 * k, '#ffd6de', .95, rot=-30)


def _petals(a, x, y, k, col, shade, shaded, r):
    c = shade if shaded else col
    n = 5
    rot = r.random() * 72
    for j in range(n):
        q = math.radians(rot + j * 72)
        a.ell(x + math.cos(q) * 2.5 * k, y + math.sin(q) * 2.3 * k, 2.3 * k, 1.9 * k, c, rot=math.degrees(q))
    a.ell(x, y, 1.1 * k, 1 * k, '#e8b83a' if not shaded else '#b08a3a')


def tree(kind, state, variant=1):
    """state: 'leaf' (after the harvest), 'bloom' (half way), 'ripe' (fruit on it). variant: 1 or 2."""
    sp = SPECIES[kind]
    a = Art(TW, TH, OX, TH - FOOT, scale=3.2)
    a.seed = len(kind) * 7 + len(state) + variant
    x, y = a.ox, a.oy
    r = rnd(len(kind) * 13 + variant * 101)
    masses = [list(m) for m in sp['masses'][variant - 1]]
    if state == 'ripe':
        # the load pulls the crown down: lower masses sag, all hang a little heavier
        for m in masses:
            m[1] -= 3 + (5 if m[1] < 80 else 0)
            m[2] *= 1.03
    ramp = list(sp['ramp'])
    if state == 'bloom':
        # the whole crown takes the colour of the blossom on its lit side
        ramp[4] = mix(ramp[4], sp['blossom'], .62)
        ramp[3] = mix(ramp[3], sp['blossom'], .42)
        ramp[2] = mix(ramp[2], sp['blossom'], .16)
    tops = [(x + dx, y - hh, rr) for (dx, hh, rr) in masses]

    # 1. the shadow on the ground: the crown's masses and the trunk, thrown to the right and down, as one shape
    sh = ''
    for (dx, hh, rr) in masses:
        sh += '<ellipse cx="%.1f" cy="%.1f" rx="%.1f" ry="%.1f"/>' % (x + dx * .9 + hh * SHADOW[0], y + hh * SHADOW[1] + 2, rr * 1.02, rr * .46)
    tx, ty = x + sp['trunk'] * SHADOW[0] * 1.6, y + sp['trunk'] * SHADOW[1] * 1.6 + 2
    sh += '<path d="M%.1f %.1f L%.1f %.1f" stroke="#0e2010" stroke-width="9" stroke-linecap="round"/>' % (x + 2, y + 2, tx, ty)
    a.add('<g fill="#0e2010" opacity=".40" filter="%s">%s</g>' % (a.blur(2.4), sh))
    a.ell(x + 3, y + 2.5, 20, 6.5, '#0e2010', .45, a.blur(1.3))

    # 2. trunk, roots, fork, limbs; grass at the foot
    _trunk(a, kind, sp, x, y, r, tops)
    for k_ in range(-7, 8):
        c = r.choice(['#3f8a2c', '#5fae3c', '#7cc84c'])
        a.path('M%.1f %.1f q%.1f -4 %.1f %.1f' % (x + k_ * 3.2, y + 4, k_ * .3, k_ * 1.1 + r.random() * 2 - 1, -6 - r.random() * 5), stroke=c, sw=1.1)

    # 3. the inside of the crown: deep shade behind the masses, smaller than they are, so gaps stay between them
    for (px, py, rr) in tops:
        a.ell(px + 2, py + 3, rr * .78, rr * .7, mix(ramp[0], '#061a10', .35))
    # limbs seen in the gaps, in the crown's shade
    for i in range(len(tops) - 1):
        (ax_, ay_, ar), (bx_, by_, br) = tops[i], tops[i + 1]
        a.path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (ax_, ay_ + ar * .3, (ax_ + bx_) / 2, (ay_ + by_) / 2 + 8, bx_, by_ + br * .3), stroke=mix(sp['bark'][0], '#0a1a10', .3), sw=2.4)

    # 4. the masses, far (high) ones first; in each: shade, half tone, then fruit or blossom, then the lit leaves over them
    crown_cx = sum(t[0] for t in tops) / len(tops)
    crown_cy = sum(t[1] for t in tops) / len(tops)
    for (px, py, rr) in sorted(tops, key=lambda t: t[1]):
        clumps = []
        for _ in range(int(rr * rr * .105)):
            ang, d = r.random() * 6.283, math.sqrt(r.random()) * rr
            ox, oy = math.cos(ang) * d, math.sin(ang) * d * .9
            # light: how this spot of the mass faces the top left, and where the mass sits in the crown
            lit = (-ox * .6 - oy * .8) / rr + ((crown_cx - px) * .25 + (crown_cy - py) * .35) / 60 + (r.random() - .5) * .16
            tone = 0 if lit < -.42 else 1 if lit < -.02 else 2 if lit < .42 else 3 if lit < .8 else 4
            clumps.append((tone, px + ox, py + oy, d / rr, ang))
        for step in (0, 1, 2, 'x', 3, 4):
            if step == 'x':
                if state == 'ripe':
                    n = max(2, int(rr / (7 if kind != 'cherry_tree' else 5.5)))
                    for _ in range(n):
                        # fruit hangs at the edges of the mass, more of it low, some of it in the shade
                        ang = math.radians(200 + r.random() * 230)
                        d = (.5 + r.random() * .45) * rr
                        fx_, fy_ = px + math.cos(ang) * d, py - math.sin(ang) * d * .85
                        shaded = (fx_ - px) * .6 + (fy_ - py) * .8 > rr * .18
                        _fruit(a, kind, fx_, fy_, .8 + r.random() * .5, shaded, r)
                elif state == 'bloom':
                    for _ in range(int(rr * .9)):
                        ang, d = r.random() * 6.283, math.sqrt(r.random()) * rr * .96
                        bx_, by_ = px + math.cos(ang) * d, py + math.sin(ang) * d * .9
                        shaded = (bx_ - px) * .6 + (by_ - py) * .8 > rr * .1
                        _petals(a, bx_, by_, .85 + r.random() * .5, sp['blossom'], sp['shade'], shaded, r)
                continue
            for (tone, qx, qy, edge, ang) in clumps:
                if tone != step:
                    continue
                n = r.choice([3, 4, 5]) if edge < .8 else 4
                base = r.random() * 360
                for i in range(n):
                    la = base + i * 360 / n + r.random() * 30 if edge < .8 else -math.degrees(ang) + (i - 1.5) * 36 + r.random() * 16
                    c = ramp[tone] if i else ramp[max(0, tone - 1)]
                    _leaf(a, qx + (r.random() - .5) * 3, qy + (r.random() - .5) * 3, 6.5 + r.random() * 4.5, 2.6 + r.random() * 1.5, la, c,
                          vein=ramp[min(4, tone + 1)] if tone >= 2 and state != 'bloom' else None)
    return a


def build():
    out = {}
    for k in SPECIES:
        for st in ('leaf', 'bloom', 'ripe'):
            for v in (1, 2):
                name = 'Tree_%s_%s%s' % (k, st, '' if v == 1 else '_2')
                out[name] = tree(k, st, v).save(name)
    return out
