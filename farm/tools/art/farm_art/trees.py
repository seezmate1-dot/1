"""Fruit trees (ART_BIBLE §8, §9): three species, each with its own build, bark, leaf, blossom and fruit; two
variants of each; three states.

apple  — a broad, rounded, uneven crown on a short thick trunk that forks low; oval toothed leaves; white-pink blossom;
pear   — a tall, upright, pointed crown on a straight trunk with rising boughs; glossy long-pointed leaves; white blossom;
cherry — a wide spreading umbrella on a red-brown trunk banded with lenticels; long drooping toothed leaves; pink blossom.

A crown is built of leaf masses (spheres); every mass is a handful of smaller clumps; every clump is single leaves.
Each leaf takes its tone from the way its clump, its mass and the whole crown face the light (top left), so the
crown reads as a solid with a lit side and a shaded side; then every mass is lit once more as a soft solid and
throws a shadow on the masses behind it. Fruit and blossom are drawn inside the clumps, on twigs, before the
clump's last leaves — so they sit in the foliage, partly covered. The trunk is a tapering solid with a root flare,
a fork and boughs that go up into the masses. The shadow lies on the ground under the crown and touches the trunk.
"""
import math
from iso import Art, lite, dark, mix, rnd, smooth, ribbon, ramp_at, LIGHT

TW, TH = 300, 310
OX, FOOT = 112, 62

# clusters: (dx, height of the centre, radius, depth) — depth 0 is behind the boughs, 1 and 2 in front
SPECIES = {
    'apple_tree': dict(
        ramp=['#0f3a1c', '#1f5a24', '#2f7a2c', '#4f9a36', '#86c248', '#b8dc58'],
        bark=('#3e2a1c', '#7a5236', '#a87a52'), blossom='#fff4f6', blush='#ff9ec0', shade='#d8a8c0',
        leaf=dict(ln=12.5, wd=4.9, teeth=5, droop=.15, spread=75),
        base=24, fork=(3, 46, 15),
        clusters=[(-34, 136, 32, 0), (26, 142, 34, 0), (66, 110, 27, 0),
                  (-64, 106, 30, 1), (-4, 124, 36, 1), (44, 118, 31, 1),
                  (-36, 92, 28, 2), (18, 92, 31, 2), (68, 86, 23, 2), (-80, 84, 18, 2)],
        limbs=[[(0, 40, 13), (-14, 62, 10), (-34, 84, 7), (-52, 100, 4)],
               [(4, 42, 13), (10, 66, 10), (20, 90, 7), (26, 116, 4)],
               [(8, 44, 10), (30, 62, 7.5), (52, 80, 5.5), (66, 96, 3.5)],
               [(-22, 72, 5), (-14, 92, 3.6), (-8, 112, 2.6)]]),
    'pear_tree': dict(
        ramp=['#1e4a1c', '#33682a', '#4f8a2e', '#7aa83a', '#a8cc50', '#d2e468'],
        bark=('#3a2c22', '#6e5642', '#9a8068'), blossom='#ffffff', blush='#f4f6ff', shade='#c8ccdc',
        leaf=dict(ln=13, wd=3.9, teeth=0, droop=-.3, spread=70, tone=-.07),
        base=22, fork=(1, 62, 13),
        clusters=[(2, 184, 19, 0), (-12, 164, 25, 0), (16, 158, 26, 0), (-26, 130, 27, 0), (28, 126, 27, 0),
                  (0, 168, 22, 1), (2, 140, 30, 1), (-28, 102, 25, 1), (30, 100, 25, 1),
                  (-16, 118, 25, 2), (18, 112, 26, 2), (0, 88, 27, 2), (-24, 82, 17, 2), (26, 80, 16, 2)],
        limbs=[[(1, 56, 11), (1, 84, 8), (2, 112, 6), (2, 140, 3.5)],
               [(0, 58, 8), (-14, 72, 6), (-26, 90, 4), (-32, 106, 2.6)],
               [(2, 60, 8), (15, 72, 6), (27, 88, 4), (33, 104, 2.6)]]),
    'cherry_tree': dict(
        ramp=['#0c3420', '#19512c', '#256e38', '#3f8a4a', '#74b852', '#a6d860'],
        bark=('#4a1e16', '#8a3e2c', '#b8604a'), blossom='#ffd9e6', blush='#ff8fb8', shade='#d07aa4',
        leaf=dict(ln=15.5, wd=3.9, teeth=7, droop=.7, spread=42),
        base=21, fork=(-2, 44, 14),
        clusters=[(-42, 114, 29, 0), (18, 120, 32, 0), (66, 104, 27, 0),
                  (-76, 90, 25, 1), (-12, 106, 33, 1), (44, 100, 29, 1), (88, 82, 20, 1),
                  (-48, 84, 27, 2), (6, 86, 29, 2), (58, 78, 25, 2), (-92, 72, 15, 2)],
        limbs=[[(-4, 40, 12), (-24, 56, 9), (-50, 72, 6.5), (-72, 84, 4)],
               [(-1, 42, 11), (-6, 64, 8), (-10, 88, 5.5), (-12, 106, 3.5)],
               [(2, 40, 12), (24, 54, 9), (50, 68, 6.5), (76, 78, 4)],
               [(22, 54, 6), (32, 74, 4.5), (42, 94, 3)]]),
}


def _variant(sp, variant):
    """The second tree of a species: its own arrangement of masses and boughs (laid out the other way round and
    reshaped), lit from the same side."""
    if variant == 1:
        return sp['clusters'], sp['limbs'], sp['fork']
    r = rnd(len(sp['clusters']) * 31 + 5)
    cl = [(-dx * (.94 + r.random() * .1) + r.uniform(-4, 4), h * (.95 + r.random() * .09) + r.uniform(-3, 3), rr * (.92 + r.random() * .16), z) for (dx, h, rr, z) in sp['clusters']]
    lb = [[(-dx * .96, h * (1 + .02 * i), w) for i, (dx, h, w) in enumerate(l)] for l in sp['limbs']]
    fx, fh, fw = sp['fork']
    return cl, lb, (-fx, fh, fw)


# ---------------------------------------------------------------------------------------------- leaves

def _leaf_outline(kind, ln, wd, r):
    """The leaf's outline in its own frame (stalk at 0,0, tip at ln,0): two lists of points, one per side."""
    if kind == 'apple_tree':
        prof = lambda u: math.sin(math.pi * u ** .8) ** .6
    elif kind == 'pear_tree':
        prof = lambda u: math.sin(math.pi * u ** .62) ** 1.0 * (1 - .18 * u)
    elif kind == 'cherry_tree':
        prof = lambda u: math.sin(math.pi * u ** 1.12) ** 1.15
    else:
        prof = lambda u: math.sin(math.pi * u ** .9) ** .75
    return prof


def leaf(a, kind, x, y, ang, ln, wd, cols, teeth=0, bend=0.0, rib=True, gloss=None):
    """One leaf at (x, y), pointing at ang (degrees, 0 right, 90 up). cols: (lit half, shaded half, midrib)."""
    prof = _leaf_outline(kind, ln, wd, None)
    t = math.radians(ang)
    cs, sn = math.cos(t), -math.sin(t)
    n = 2 * teeth + 2 if teeth else 7
    mid, up, lo = [], [], []
    for i in range(n + 1):
        u = i / n
        w = wd * prof(u)
        if teeth and 0 < i < n:
            w *= 1.07 if i % 2 else .92
        by = bend * ln * .5 * math.sin(math.pi * u * .9)
        px, py = u * ln, by
        mid.append((x + px * cs - py * sn, y + px * sn + py * cs))
        up.append((x + px * cs - (py - w) * sn, y + px * sn + (py - w) * cs))
        lo.append((x + px * cs - (py + w) * sn, y + px * sn + (py + w) * cs))
    # which half faces the light: the one whose side looks up and left
    nx, ny = sn, -cs            # the 'up' side's outward direction on the screen (for py - w)
    lit_up = nx * LIGHT[0] * -1 + ny * LIGHT[1] * -1 < 0
    c_lit, c_sh, c_rib = cols
    pts = lambda l: ' '.join('%.1f,%.1f' % q for q in l)
    s = '<polygon points="%s" fill="%s"/>' % (pts(up + lo[::-1]), c_sh)
    s += '<polygon points="%s" fill="%s"/>' % (pts((up if lit_up else lo) + mid[::-1]), c_lit)
    if rib:
        s += '<polyline points="%s" fill="none" stroke="%s" stroke-width=".45" stroke-opacity=".7"/>' % (pts(mid[:-1]), c_rib)
    if gloss:
        g = (up if lit_up else lo)
        k0, k1 = int(n * .3), int(n * .7)
        q = [((g[i][0] + mid[i][0]) / 2, (g[i][1] + mid[i][1]) / 2) for i in range(k0, k1 + 1)]
        s += '<polyline points="%s" fill="none" stroke="%s" stroke-width="%.1f" stroke-linecap="round" stroke-opacity=".55"/>' % (pts(q), gloss, wd * .32)
    a.add(s)


def _norm(v):
    l = math.sqrt(sum(c * c for c in v)) or 1
    return tuple(c / l for c in v)


def _sphere_n(ox, oy, rr):
    d2 = min(.98, (ox * ox + oy * oy) / (rr * rr))
    return (ox / rr, oy / rr, math.sqrt(1 - d2))


def clump(a, kind, px, py, rr, base_n, ramp, lf, r, density=.3, tone=0.0, extra=None, young=False, extra_at=.72):
    """A clump of leaves: a dark heart, then leaves from its edge to its middle. base_n: the normal of the mass
    and the crown at this clump (so the clump takes their light). extra(frac): called once after most leaves are
    down, to draw fruit or blossom that the last leaves then partly cover."""
    bl = base_n[0] * LIGHT[0] + base_n[1] * LIGHT[1] + base_n[2] * LIGHT[2]
    t0 = .38 + .5 * bl + tone + lf.get('tone', 0)
    a.ell(px + rr * .08, py + rr * .14, rr * .86, rr * .8, ramp_at(ramp, max(0, t0 - .34)))
    n = max(6, int(rr * rr * density))
    leaves = []
    for _ in range(n):
        ang, d = r.random() * 6.283, math.sqrt(r.random()) * 1.02
        ox, oy = math.cos(ang) * d * rr, math.sin(ang) * d * rr * .92
        leaves.append((d + r.random() * .35, ox, oy, ang, d))
    leaves.sort(reverse=True)
    cut = int(n * extra_at)
    for i, (_, ox, oy, ang, d) in enumerate(leaves):
        if i == cut and extra:
            extra()
        ln_ = _sphere_n(ox, oy, rr * 1.03)
        nn = _norm((ln_[0] * .55 + base_n[0], ln_[1] * .55 + base_n[1], ln_[2] * .55 + base_n[2]))
        l = nn[0] * LIGHT[0] + nn[1] * LIGHT[1] + nn[2] * LIGHT[2]
        t = .36 + .6 * l + tone + lf.get('tone', 0) + (r.random() - .5) * .16
        if i < n * .3:
            t -= .14                                     # the leaves underneath are in the others' shade
        # the leaf points out of the clump, pulled down (or up) as the species hangs its leaves
        out_a = -math.degrees(ang)                       # screen angle pointing away from the clump's centre
        dr = lf['droop']
        aim = -90 if dr > 0 else 90
        da = ((aim - out_a + 180) % 360) - 180
        la = out_a + da * abs(dr) * (.6 + .4 * (1 - d)) + (r.random() - .5) * lf['spread']
        ln = lf['ln'] * (.78 + r.random() * .4) * (.9 if young else 1)
        wd = lf['wd'] * (.85 + r.random() * .3)
        c = ramp_at(ramp, t)
        cols = (ramp_at(ramp, t + .1), ramp_at(ramp, t - .09), ramp_at(ramp, t + (.24 if t > .35 else -.14)))
        gl = None
        if kind == 'pear_tree' and t > .5 and r.random() < .6:
            gl = '#f4ffc8'
        elif kind == 'cherry_tree' and t > .62 and r.random() < .3:
            gl = '#e2f8b0'
        leaf(a, kind, px + ox - math.cos(math.radians(la)) * ln * .35, py + oy + math.sin(math.radians(la)) * ln * .35, la, ln, wd, cols,
             teeth=lf['teeth'], bend=(r.random() - .5) * .5, gloss=gl)


# ---------------------------------------------------------------------------------------------- fruit, blossom

def fruit(a, kind, x, y, k, shaded, r):
    """One fruit (or a bunch of cherries) hanging from (x, y) — the end of its twig; k is its size."""
    wood = '#5a3a1e'
    if kind == 'apple_tree':
        c = '#b82a24' if shaded else '#e23a2e'
        R = 7.4 * k
        cx, cy = x + r.uniform(-1, 1), y + 2 * k + R
        a.path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (x, y, x + 1.5, y + 2.5 * k, cx, cy - R * .78), stroke=wood, sw=1.2)
        d = ('M%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f Z' % (
            cx, cy - R * .74, cx - R * .5, cy - R * 1.08, cx - R * 1.04, cy - R * .7, cx - R, cy - R * .05,
            cx - R * .96, cy + R * .66, cx - R * .5, cy + R * 1.0, cx, cy + R * .9,
            cx + R * .5, cy + R * 1.0, cx + R * .96, cy + R * .66, cx + R, cy - R * .05,
            cx + R * 1.04, cy - R * .7, cx + R * .5, cy - R * 1.08, cx, cy - R * .74))
        a.path(d, a.rg([(0, '#ff8f78' if not shaded else '#d05a4c'), (.42, c), (1, dark(c, .5))], .34, .3, .78), dark(c, .6), .45)
        a.group('<ellipse cx="%.1f" cy="%.1f" rx="%.1f" ry="%.1f" fill="#f2c83c" opacity="%s" filter="%s"/>' % (cx + R * .45, cy - R * .2, R * .5, R * .7, .5 if not shaded else .25, a.blur(1.4)), clip=a.clip(d))
        a.path('M%.1f %.1f q%.1f %.1f %.1f 0' % (cx - R * .34, cy - R * .74, R * .34, R * .34, R * .68), stroke=dark(c, .62), sw=.9, op=.8)
        a.ell(cx - R * .4, cy - R * .34, R * .26, R * .17, '#fff0e6', .92 if not shaded else .45, rot=-32)
        a.ell(cx - R * .12, cy + R * .1, R * .08, R * .06, '#fff0e6', .5)
        a.path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (cx - R * .5, cy + R * .7, cx, cy + R * .9, cx + R * .5, cy + R * .68), stroke='#ff9a70', sw=.7, op=.45)
    elif kind == 'pear_tree':
        c = '#c8a826' if shaded else '#f0d23c'
        R = 6.4 * k
        tilt = r.uniform(-8, 8)
        cx, cy = x, y + 3.5 * k
        a.path('M%.1f %.1f L%.1f %.1f' % (x, y, cx, cy + 1), stroke=wood, sw=1.3)
        pts = [(0, 0), (.42, .25), (.5, .95), (1.0, 1.75), (.9, 2.5), (.4, 2.95), (0, 3.0), (-.4, 2.95), (-.9, 2.5), (-1.0, 1.75), (-.5, .95), (-.42, .25)]
        d = smooth([(cx + px * R, cy + py * R) for (px, py) in pts])
        g = a.rg([(0, '#fff8b0' if not shaded else '#e0c850'), (.45, c), (1, dark(c, .42))], .34, .5, .7)
        inner = '<path d="%s" fill="%s" stroke="%s" stroke-width=".45"/>' % (d, g, dark(c, .55))
        cl = a.clip(d)
        inner += '<g clip-path="%s"><ellipse cx="%.1f" cy="%.1f" rx="%.1f" ry="%.1f" fill="#9ab83a" opacity=".5" filter="%s"/>' % (cl, cx, cy + R * .3, R * .9, R * .8, a.blur(1.2))
        inner += '<ellipse cx="%.1f" cy="%.1f" rx="%.1f" ry="%.1f" fill="#e8843a" opacity="%s" filter="%s"/></g>' % (cx + R * .6, cy + R * 1.9, R * .5, R * .8, .4, a.blur(1.4))
        for _ in range(7):
            inner += '<circle cx="%.1f" cy="%.1f" r=".3" fill="%s" opacity=".7"/>' % (cx + r.uniform(-.7, .7) * R, cy + r.uniform(1.0, 2.7) * R, dark(c, .5))
        inner += '<ellipse cx="%.1f" cy="%.1f" rx="%.1f" ry="%.1f" fill="#fffde0" opacity="%s" transform="rotate(8 %.1f %.1f)"/>' % (cx - R * .42, cy + R * 1.6, R * .16, R * .5, .9 if not shaded else .4, cx - R * .42, cy + R * 1.6)
        a.group(inner, transform='rotate(%.1f %.1f %.1f)' % (tilt, x, y))
    else:
        c = '#8e1230' if shaded else '#c2183a'
        R = 4.3 * k
        n = r.choice([2, 2, 3])
        for i in range(n):
            dx = (i - (n - 1) / 2) * R * 1.75 + r.uniform(-1, 1)
            ln = (11 + r.random() * 5) * k
            cx, cy = x + dx, y + ln + R
            a.path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (x, y, x + dx * .25, y + ln * .55, cx, cy - R * .8), stroke='#7a8a30', sw=.85)
            a.ell(cx, cy, R, R * .96, a.rg([(0, '#ff7a90' if not shaded else '#c0465a'), (.45, c), (1, dark(c, .55))], .34, .3, .8), stroke=dark(c, .65), sw=.4)
            a.path('M%.1f %.1f q%.1f %.1f %.1f 0' % (cx - R * .3, cy - R * .78, R * .3, R * .3, R * .6), stroke=dark(c, .6), sw=.6, op=.8)
            a.ell(cx - R * .38, cy - R * .36, R * .28, R * .18, '#ffe4ea', .95 if not shaded else .5, rot=-32)
            a.path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (cx - R * .5, cy + R * .62, cx, cy + R * .86, cx + R * .55, cy + R * .55), stroke='#ff8a9a', sw=.5, op=.5)
        a.ell(x, y, 1.3, 1.1, wood)


def blossom(a, x, y, k, sp, shaded, r, bud=False):
    """A five-petalled flower (or a bud): petals with a blush at the edge, a green-yellow heart, stamens."""
    col = mix(sp['blossom'], sp['shade'], .75) if shaded else sp['blossom']
    edge = mix(sp['blush'], sp['shade'], .6) if shaded else sp['blush']
    if bud:
        a.ell(x, y, 1.9 * k, 2.5 * k, a.rg([(0, lite(edge, .3)), (1, edge)], .4, .35, .7), rot=r.uniform(-40, 40), stroke=dark(edge, .35), sw=.25)
        return
    g = a.rg([(0, col), (.62, col), (1, edge)], .5, .5, .5)
    rot = r.random() * 72
    R = 3.0 * k
    sq = .78 + r.random() * .2                      # flowers face every way: some are seen at an angle
    s = ''
    for j in range(5):
        q = math.radians(rot + j * 72)
        cx, cy = x + math.cos(q) * R * .78, y + math.sin(q) * R * .78 * sq
        s += '<ellipse cx="%.1f" cy="%.1f" rx="%.1f" ry="%.1f" fill="%s" stroke="%s" stroke-width=".25" transform="rotate(%.0f %.1f %.1f)"/>' % (
            cx, cy, R * .78, R * .62 * sq, g, dark(edge, .25), math.degrees(q), cx, cy)
    a.add(s)
    a.ell(x, y, R * .34, R * .3 * sq, '#d8e070' if not shaded else '#a8a850')
    for j in range(6):
        q = r.random() * 6.283
        a.ell(x + math.cos(q) * R * .42, y + math.sin(q) * R * .42 * sq, .42, .42, '#e8a020' if not shaded else '#a87820')


# ---------------------------------------------------------------------------------------------- the trunk

def trunk(a, kind, sp, x, y, limbs, fork, r, shade_to):
    bark = sp['bark']
    wb = sp['base']
    fx, fh, fw = fork
    P = lambda dx, h: (x + dx, y - h)
    bend = 5 if kind == 'apple_tree' else 0 if kind == 'pear_tree' else -3
    spine = [(x, y + 3, wb * 1.55), (x + bend * .3, y - 5, wb * 1.08), (x + bend, y - fh * .45, wb * .8), (x + fx, y - fh, fw), (x + fx, y - fh - 8, fw * .9)]
    shapes = [ribbon(spine, 10)]
    # roots: toes spreading on the ground
    for (sg, ln, w, dy) in ((-1, wb * 1.25, wb * .5, 3), (1, wb * 1.3, wb * .48, 4), (-.45, wb * .8, wb * .42, 6), (.5, wb * .85, wb * .4, 7)):
        shapes.append(ribbon([(x + sg * wb * .1, y - 9, w * 1.5), (x + sg * ln * .55, y - 1 + dy * .3, w), (x + sg * ln, y + dy, w * .25)], 6))
    for l in limbs:
        shapes.append(ribbon([P(dx, h) + (w,) for (dx, h, w) in l], 8))
    d_all = ' '.join(shapes)
    g = a.lg([(0, bark[2]), (.45, bark[1]), (1, bark[0])], 0, 0, 1, 0)
    a.path(d_all, bark[1])
    cl = a.clip(d_all)
    s = ''
    # the broad light on the left of the bole and the shade on the right
    s += '<path d="%s" fill="%s" opacity=".55"/>' % (shapes[0], g)
    top = y - fh - 10
    if kind == 'cherry_tree':
        # smooth, shining bark with rings of pale lenticels round the trunk and the boughs
        for sh, pts_, step in [(spine, None, 4.6)] + [([P(dx, h) + (w,) for (dx, h, w) in l], None, 6.5) for l in limbs]:
            from iso import spline
            sp_ = spline(sh, 10)
            acc = 0
            for i in range(1, len(sp_) - 1):
                (x0, y0, w0), (x1, y1, w1) = sp_[i - 1], sp_[i]
                acc += math.hypot(x1 - x0, y1 - y0)
                if acc < step:
                    continue
                acc = r.uniform(-1.2, 1.2)
                dx_, dy_ = x1 - x0, y1 - y0
                ln = math.hypot(dx_, dy_) or 1
                nx, ny = -dy_ / ln, dx_ / ln
                tx, ty = dx_ / ln, dy_ / ln
                f0, f1 = r.uniform(-.52, -.1), r.uniform(.05, .5)
                sag = w1 * .16
                p0 = (x1 + nx * w1 * f0, y1 + ny * w1 * f0)
                p1 = (x1 + nx * w1 * f1, y1 + ny * w1 * f1)
                pm = (x1 - tx * sag, y1 - ty * sag)
                s += '<path d="M%.1f %.1f Q%.1f %.1f %.1f %.1f" stroke="%s" stroke-width="%.1f" fill="none" stroke-linecap="round" opacity=".7"/>' % (p0[0], p0[1], pm[0], pm[1], p1[0], p1[1], mix(bark[2], '#f0c0a0', .3), .45 + w1 * .035)
                s += '<path d="M%.1f %.1f Q%.1f %.1f %.1f %.1f" stroke="%s" stroke-width=".4" fill="none" stroke-linecap="round" opacity=".6"/>' % (p0[0] - tx, p0[1] - ty, pm[0] - tx, pm[1] - ty, p1[0] - tx, p1[1] - ty, bark[0])
        s += '<path d="M%.1f %.1f Q%.1f %.1f %.1f %.1f" stroke="#ffd8c0" stroke-width="2.2" fill="none" stroke-linecap="round" opacity=".5"/>' % (x - wb * .3, y - 8, x + bend * .6 - wb * .22, y - fh * .5, x + fx - fw * .24, y - fh + 2)
    else:
        # furrowed bark: long dark cracks with a lit lip (apple: scaly plates; pear: small square checks)
        n = 30 if kind == 'apple_tree' else 46
        for i in range(n):
            t = r.random()
            w_here = wb * (1.3 - t * .7)
            bx = x + bend * t + fx * t * t + r.uniform(-.5, .5) * w_here
            by = y + 2 - t * (fh + 8)
            ln = (8 if kind == 'apple_tree' else 4.5) + r.random() * (9 if kind == 'apple_tree' else 4)
            wob = r.uniform(-1.6, 1.6)
            s += '<path d="M%.1f %.1f q%.1f -%.1f %.1f -%.1f" stroke="%s" stroke-width="%.1f" fill="none" stroke-linecap="round" opacity=".8"/>' % (bx, by, wob, ln / 2, wob * .3, ln, mix(bark[0], '#1a0e08', .3), 1.1 if kind == 'apple_tree' else .8)
            s += '<path d="M%.1f %.1f q%.1f -%.1f %.1f -%.1f" stroke="%s" stroke-width=".6" fill="none" stroke-linecap="round" opacity=".75"/>' % (bx - 1.2, by - .6, wob, ln / 2, wob * .3, ln * .8, lite(bark[2], .25))
            if kind == 'pear_tree' and r.random() < .7:
                s += '<path d="M%.1f %.1f l%.1f .6" stroke="%s" stroke-width=".7" opacity=".7"/>' % (bx - 2.2, by - ln * .5, 4.4, bark[0])
        for l in limbs:
            for (dx, h, w) in l[:-1]:
                for _ in range(3):
                    s += '<path d="M%.1f %.1f l%.1f -%.1f" stroke="%s" stroke-width=".6" opacity=".6"/>' % (x + dx + r.uniform(-.4, .4) * w, y - h - r.random() * 10, r.uniform(-2, 2), 4 + r.random() * 4, bark[0])
        if kind == 'apple_tree':
            kx, ky = x + bend * .6 + wb * .12, y - fh * .5
            s += '<ellipse cx="%.1f" cy="%.1f" rx="3.4" ry="4.4" fill="%s"/><ellipse cx="%.1f" cy="%.1f" rx="2" ry="2.8" fill="%s"/>' % (kx, ky, bark[0], kx - .3, ky - .3, mix(bark[0], '#140a06', .55))
            s += '<path d="M%.1f %.1f a4.2 5.2 0 0 1 6 -2" stroke="%s" stroke-width=".8" fill="none" opacity=".8"/>' % (kx - 4.4, ky - 1, lite(bark[2], .2))
    s += '<rect x="0" y="0" width="%d" height="%.1f" fill="%s"/>' % (TW, shade_to, a.lg([(0, '#0a1e10', .8), (.7, '#0a1e10', .6), (1, '#0a1e10', 0)]))
    a.group(s, clip=cl)


# ---------------------------------------------------------------------------------------------- the tree

def tree(kind, state, variant=1):
    """state: 'leaf' (after the harvest), 'bloom' (half way), 'ripe' (fruit on it). variant: 1 or 2."""
    sp = SPECIES[kind]
    a = Art(TW, TH, OX, TH - FOOT, scale=3.2)
    a.seed = len(kind) * 7 + len(state) + variant
    a.look = dict(volume=.1, grain=.03)
    x, y = a.ox, a.oy
    r = rnd(len(kind) * 13 + variant * 101)
    clusters, limbs, fork = _variant(sp, variant)
    clusters = [list(c) for c in clusters]
    if state == 'ripe':
        for c in clusters:                      # the load pulls the boughs down
            c[1] -= 2 + (3 if c[1] < 100 else 0)
    ramp = list(sp['ramp'])
    lf = dict(sp['leaf'])
    young = state == 'bloom'
    if young:
        ramp = [mix(c, '#b8d860', .12 + .05 * i) for i, c in enumerate(ramp)]

    # 1. the shadow on the ground: under the crown, a little to the right, joined to the foot of the trunk
    sh = ''
    for (dx, hh, rr, z) in clusters:
        sh += '<ellipse cx="%.1f" cy="%.1f" rx="%.1f" ry="%.1f"/>' % (x + dx * .82 + 16 + hh * .06, y + 9 + hh * .035 - dx * .0, rr * .98, rr * .4)
    a.add('<g fill="#0e2010" opacity=".46" filter="%s">%s</g>' % (a.blur(3.4), sh))
    a.ell(x + 5, y + 4, sp['base'] * 1.5, sp['base'] * .46, '#0e2010', .5, a.blur(1.8))

    ccx = sum(c[0] for c in clusters) / len(clusters)
    ccy = sum(c[1] for c in clusters) / len(clusters)
    cr = max(max(abs(c[0] - ccx) + c[2] for c in clusters), max(abs(c[1] - ccy) + c[2] for c in clusters))

    def mass(c, idx):
        dx, hh, rr, z = c
        px, py = x + dx, y - hh
        # where this mass sits on the crown: its normal on the crown as a whole
        cn = _sphere_n(dx - ccx, -(hh - ccy), cr * 1.05)
        a.layer(relief=dict(r=rr * .42, depth=.9, lit=.22, shade=.6, rim=.16, fine=.15), drop=dict(dx=2.5, dy=5, sd=3.5, op=.38 if z else .0, col='#06220f'))
        # the dark inside, a little smaller than the mass
        a.ell(px + 1, py + 2, rr * .8, rr * .74, ramp_at(ramp, .04))
        # clumps: a ring from the far lower right round to the lit upper left, then the middle one nearest to us
        n_ring = 6 if rr > 24 else 5
        cl = []
        for i in range(n_ring):
            q = math.radians(40 + i * 360 / n_ring + r.uniform(-14, 14))
            d = rr * (.56 + r.random() * .1)
            cl.append((math.cos(q) * d, math.sin(q) * d * .94, rr * (.46 + r.random() * .1)))
        cl.sort(key=lambda t: -(t[0] * .52 + t[1] * .62))        # the darkest first, the lit ones over them
        cl.append((r.uniform(-.1, .1) * rr, r.uniform(-.05, .12) * rr, rr * .56))
        late = []
        for j, (ox, oy, cr_) in enumerate(cl):
            sn = _sphere_n(ox, oy, rr * 1.02)
            bn = _norm((sn[0] * .6 + cn[0] * .95, sn[1] * .6 + cn[1] * .95, sn[2] * .4 + cn[2] * .45))
            low = oy > rr * .12
            extra = None
            cx_, cy_ = px + ox, py + oy
            shaded = (bn[0] * LIGHT[0] + bn[1] * LIGHT[1] + bn[2] * LIGHT[2]) < .1
            at = .72
            if state == 'bloom':
                late.append(lambda cx_=cx_, cy_=cy_, cr_=cr_, shaded=shaded: [blossom(a, cx_ + r.uniform(-.9, .9) * cr_, cy_ + r.uniform(-.9, .9) * cr_, .9 + r.random() * .4, sp, shaded, r, bud=r.random() < .2) for _ in range(int(cr_ * (.3 if not shaded else .1)))])
            if state == 'ripe' and z > 0 and r.random() < dict(apple_tree=.5, pear_tree=.42, cherry_tree=.55)[kind]:
                def fr(cx_=cx_, cy_=cy_, cr_=cr_, shaded=shaded):
                    fx_, fy_ = cx_ + r.uniform(-.5, .5) * cr_, cy_ + r.uniform(-.3, .3) * cr_
                    k_ = .92 + r.random() * .3
                    # the fruit shades the leaves under it
                    a.ell(fx_ + 2.5, fy_ + (13 if kind != 'cherry_tree' else 19) * k_, 8 * k_, 7.5 * k_, '#06220f', .4, a.blur(1.6))
                    fruit(a, kind, fx_, fy_, k_, shaded, r)
                    # leaves of the same spur hang over the fruit's stalk and shoulders
                    for (ang_, dx_) in ((-128 - r.random() * 30, -1.5), (-52 + r.random() * 30, 1.5), (180 + r.uniform(-25, 25), 3), (r.uniform(-20, 20), -3)):
                        t = .42 + (0 if shaded else .22) + r.uniform(-.06, .06)
                        leaf(a, kind, fx_ + dx_, fy_ - 1.5 + r.uniform(-1, 1), ang_, lf['ln'] * (.8 + r.random() * .25), lf['wd'],
                             (ramp_at(ramp, t + .1), ramp_at(ramp, t - .09), ramp_at(ramp, t + .24)), teeth=lf['teeth'], bend=r.uniform(-.3, .3))
                late.append(fr)
            elif state == 'bloom':
                def extra(cx_=cx_, cy_=cy_, cr_=cr_, shaded=shaded):
                    for _ in range(int(cr_ * (.42 if not shaded else .3))):
                        q, d = r.random() * 6.283, math.sqrt(r.random()) * cr_ * .98
                        bx_, by_ = cx_ + math.cos(q) * d, cy_ + math.sin(q) * d * .92
                        for m in range(r.choice([2, 3, 3, 4])):
                            blossom(a, bx_ + r.uniform(-4.5, 4.5), by_ + r.uniform(-4, 4), .85 + r.random() * .4, sp, shaded, r, bud=r.random() < (.3 if kind == 'apple_tree' else .14))
            if state == 'bloom':
                pass
            clump(a, kind, cx_, cy_, cr_, bn, ramp, lf, r, density=.3 if not young else .22, tone=-.1 * (z == 0) - .12 * max(0, oy / rr) - .1 * max(0, min(1, (ccy - hh + oy) / cr + .2)), extra=extra, young=young, extra_at=at)

        for f in late:
            f()
        return None

    order = sorted(range(len(clusters)), key=lambda i: (clusters[i][3], -clusters[i][1]))
    back = [i for i in order if clusters[i][3] == 0]
    front = [i for i in order if clusters[i][3] > 0]
    # 2. the far masses
    for i in back:
        mass(clusters[i], i)
    # 3. the trunk, the fork, the boughs — a solid lit from the left; the crown's shade lies on their upper part
    a.layer(relief=dict(r=sp['base'] * .3, depth=1.5, lit=.55, shade=.8, rim=.3, fine=.3), drop=dict(dx=3, dy=3, sd=3, op=.5, col='#06220f'))
    lowest = min(c[1] - c[2] for c in clusters if c[3] > 0)
    trunk(a, kind, sp, x, y, limbs, fork, r, y - lowest + 24)
    # grass at the foot, over the roots
    a.layer()
    for k_ in range(-9, 10):
        c = r.choice(['#3f8a2c', '#5fae3c', '#7cc84c', '#5fae3c'])
        bx = x + k_ * 3.6 + r.uniform(-1, 1)
        hgt = (5 + r.random() * 6) * (1 if abs(k_) > 2 else .55)
        a.path('M%.1f %.1f q%.1f %.1f %.1f %.1f q%.1f %.1f %.1f %.1f Z' % (bx - 1.1, y + 5 + abs(k_) * .15, k_ * .1, -hgt * .6, k_ * .35 + r.uniform(-1.5, 1.5), -hgt, 1.2, hgt * .5, 2.2 - k_ * .35, hgt), c)
    # 4. the near masses
    for i in front:
        mass(clusters[i], i)
    return a


def build():
    out = {}
    for k in SPECIES:
        for st in ('leaf', 'bloom', 'ripe'):
            for v in (1, 2):
                name = 'Tree_%s_%s%s' % (k, st, '' if v == 1 else '_2')
                out[name] = tree(k, st, v).save(name)
    return out


if __name__ == '__main__':
    import sys
    from PIL import Image
    kinds = sys.argv[2].split(',') if len(sys.argv) > 2 else list(SPECIES)
    states = sys.argv[3].split(',') if len(sys.argv) > 3 else ['leaf', 'bloom', 'ripe']
    vs = [int(v) for v in sys.argv[4].split(',')] if len(sys.argv) > 4 else [1]
    ims = [[tree(k, st, v).image() for st in states for v in vs] for k in kinds]
    w, h = ims[0][0].size
    sheet = Image.new('RGBA', (len(ims[0]) * w, len(ims) * h), (121, 185, 78, 255))
    for j, row in enumerate(ims):
        for i, im in enumerate(row):
            sheet.alpha_composite(im, (i * w, j * h))
    sheet.convert('RGB').save(sys.argv[1], quality=92)
