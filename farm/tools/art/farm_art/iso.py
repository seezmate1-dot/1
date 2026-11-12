"""A small isometric vector kit for the farm's map sprites: SVG strings rendered with resvg (3x supersample).

Projection: the ground plane's axes run down-right (u) and down-left (v) with the slope of the painted zone
backgrounds (about 0.59); z is up. Light comes from the top left: tops are lightest, faces looking left are lit,
faces looking right are in shade; a soft contact shadow lies on the ground to the right.
"""
import io
import math
import os
import random
import numpy as np
import resvg_py
from PIL import Image, ImageFilter

A, B = 75.0, 44.0            # half a ground tile on screen, in map units
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "..", "unity", "SunnyValley", "Assets", "SunnyValley", "Resources", "Art", "Farm"))


def rgb(h):
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def hexc(c):
    return '#%02x%02x%02x' % tuple(max(0, min(255, int(round(v)))) for v in c)


def mul(h, k):
    return hexc([v * k for v in rgb(h)])


def mix(a, b, t):
    ca, cb = rgb(a), rgb(b)
    return hexc([ca[i] + (cb[i] - ca[i]) * t for i in range(3)])


def lite(h, t=.25):
    return mix(h, '#fff6d8', t)


def dark(h, t=.25):
    return mix(h, '#3a1c10', t)


def _noise(rng, w, h, cell):
    small = rng.random((max(2, h // cell + 2), max(2, w // cell + 2))).astype(np.float32)
    im = Image.fromarray((small * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC)
    return np.asarray(im).astype(np.float32) / 127.5 - 1


def painterly(im, seed=1, grain=.045, volume=.2, rim=0, contrast=1.12, sat=1.16, px=4.0):
    """The tone of the zone backgrounds over a clean, sharp vector render: a fine uneven grain in the colour (not
    a blur — every edge stays as drawn), the whole thing a little lighter towards the light (top left) and darker
    away from it, and the saturation and contrast of the painted map. px: pixels per map unit, so the grain is the
    same size on the map whatever the picture's resolution."""
    a = np.asarray(im).astype(np.float32) / 255
    rgb, al = a[..., :3].copy(), a[..., 3]
    h, w = al.shape
    ys, xs = np.nonzero(al > .05)
    if len(xs) == 0:
        return im
    rng = np.random.default_rng(seed)
    k = max(1.0, px / 2)
    n = .45 * _noise(rng, w, h, int(22 * k)) + .3 * _noise(rng, w, h, int(7 * k)) + .25 * _noise(rng, w, h, max(2, int(2 * k)))
    rgb *= (1 + grain * n)[..., None]
    t = _noise(rng, w, h, int(36 * k)) * .018
    rgb[..., 0] += t
    rgb[..., 2] -= t
    if volume:
        x0, x1, y0, y1 = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
        u = (np.arange(w, dtype=np.float32) - x0) / max(1, x1 - x0)
        v = (np.arange(h, dtype=np.float32) - y0) / max(1, y1 - y0)
        g = 1 + volume * .5 - volume * (.45 * u[None, :] + .55 * v[:, None])
        rgb *= g[..., None]
    grey = rgb.mean(axis=2, keepdims=True)
    rgb = grey + (rgb - grey) * sat
    rgb = (rgb - .5) * contrast + .5
    out = np.dstack([np.clip(rgb, 0, 1), al])
    return Image.fromarray((out * 255 + .5).astype(np.uint8), 'RGBA')


class Art:
    """One sprite: a canvas in map units with an origin on the ground (the footprint's nearest corner)."""

    def __init__(self, w, h, ox=None, oy=None, scale=4.0):
        self.w, self.h = w, h
        self.ox = w / 2 if ox is None else ox
        self.oy = h - 8 if oy is None else oy
        self.scale = scale
        self.defs, self.body = [], []
        self.n = 0
        self.paint = True          # the painted look (not for the interface's things)
        self.seed = 1
        self.look = {}             # painterly()'s numbers, when a picture wants its own

    # ---------------------------------------------------------------- projection
    def p(self, u, v, z=0):
        return (self.ox + (u - v) * A, self.oy - (u + v) * B - z)

    def pts(self, lst):
        return ' '.join('%.1f,%.1f' % self.p(*q) for q in lst)

    # ---------------------------------------------------------------- paint
    def uid(self):
        self.n += 1
        return 'i%d' % self.n

    def lg(self, stops, x1=0, y1=0, x2=0, y2=1):
        i = self.uid()
        st = ''.join('<stop offset="%s" stop-color="%s"%s/>' % (s[0], s[1], ' stop-opacity="%s"' % s[2] if len(s) > 2 else '') for s in stops)
        self.defs.append('<linearGradient id="%s" x1="%s" y1="%s" x2="%s" y2="%s">%s</linearGradient>' % (i, x1, y1, x2, y2, st))
        return 'url(#%s)' % i

    def rg(self, stops, cx=.4, cy=.32, r=.75):
        i = self.uid()
        st = ''.join('<stop offset="%s" stop-color="%s"%s/>' % (s[0], s[1], ' stop-opacity="%s"' % s[2] if len(s) > 2 else '') for s in stops)
        self.defs.append('<radialGradient id="%s" cx="%s" cy="%s" r="%s">%s</radialGradient>' % (i, cx, cy, r, st))
        return 'url(#%s)' % i

    def blur(self, sd):
        i = self.uid()
        self.defs.append('<filter id="%s" x="-50%%" y="-50%%" width="200%%" height="200%%"><feGaussianBlur stdDeviation="%s"/></filter>' % (i, sd))
        return 'url(#%s)' % i

    def add(self, s):
        self.body.append(s)

    def poly(self, lst, fill, stroke=None, sw=1.2, op=1, flt=None, raw=False):
        pts = ' '.join('%.1f,%.1f' % q for q in lst) if raw else self.pts(lst)
        s = '<polygon points="%s" fill="%s"' % (pts, fill)
        if stroke:
            # edges are a thin darker turn of the colour, not a drawn outline
            s += ' stroke="%s" stroke-width="%s" stroke-opacity=".4" stroke-linejoin="round"' % (stroke, sw)
        if op != 1:
            s += ' opacity="%s"' % op
        if flt:
            s += ' filter="%s"' % flt
        self.add(s + '/>')

    def path(self, d, fill='none', stroke=None, sw=1, op=1, flt=None, cap='round'):
        s = '<path d="%s" fill="%s"' % (d, fill)
        if stroke:
            s += ' stroke="%s" stroke-width="%s" stroke-linecap="%s" stroke-linejoin="round"' % (stroke, sw, cap)
        if op != 1:
            s += ' opacity="%s"' % op
        if flt:
            s += ' filter="%s"' % flt
        self.add(s + '/>')

    def ell(self, cx, cy, rx, ry, fill, op=1, flt=None, rot=0, stroke=None, sw=1):
        s = '<ellipse cx="%.1f" cy="%.1f" rx="%.1f" ry="%.1f" fill="%s"' % (cx, cy, rx, ry, fill)
        if op != 1:
            s += ' opacity="%s"' % op
        if flt:
            s += ' filter="%s"' % flt
        if rot:
            s += ' transform="rotate(%s %.1f %.1f)"' % (rot, cx, cy)
        if stroke:
            s += ' stroke="%s" stroke-width="%s"' % (stroke, sw)
        self.add(s + '/>')

    def line(self, a, b, col, sw=2, op=1):
        self.add('<line x1="%.1f" y1="%.1f" x2="%.1f" y2="%.1f" stroke="%s" stroke-width="%s" stroke-linecap="round"%s/>'
                 % (a[0], a[1], b[0], b[1], col, sw, ' opacity="%s"' % op if op != 1 else ''))

    # ---------------------------------------------------------------- solids
    def shadow(self, u0, v0, u1, v1, h=.5, op=.40, sd=2.5):
        """The cast shadow of a block (ART_BIBLE §2): its footprint pushed along the ground away from the light —
        to the right and a little down on the screen, (+0.25, −1) in tiles — by h tiles (0.6 of the height)."""
        du, dv = .25 * h, -h
        self.poly([(u0, v1), (u0, v0), (u0 + du, v0 + dv), (u1 + du, v0 + dv), (u1 + du, v1 + dv), (u1, v1)], '#0e2010', op=op, flt=self.blur(sd))

    def blob_shadow(self, u, v, r, op=.3, sd=4, squash=.5):
        x, y = self.p(u, v)
        self.ell(x + r * A * .25, y + 2, r * A, r * A * squash, '#1c2a10', op, self.blur(sd))

    def face(self, side, u, v, z0, length, height, tex, col, seed=0):
        """Material on a wall (length along it × height, map units), drawn crisp: 'planks' (boards with grain and
        nails), 'plaster' (fine speckle, hairline cracks), 'timber' (plaster in a dark timber frame), 'brick',
        'stone' (courses of cut stones with mortar); and the shade where the wall meets the ground."""
        r = rnd(seed + int(u * 97) + int(v * 53) + (7 if side == 'l' else 0))
        s = ''
        if tex == 'planks':
            x = 0
            while x < length:
                bw = 6.5 + r.random() * 3
                w = min(bw, length - x)
                tone = r.choice([lite(col, .14), lite(col, .06), col, dark(col, .08), dark(col, .16)])
                s += rect(x, -height, w, height, tone)
                s += rect(x, -height, .7, height, dark(col, .55), None, 0, 0, .8) + rect(x + .7, -height, .6, height, lite(col, .35), None, 0, 0, .5)
                for _ in range(3):
                    gx, gy = x + 1.6 + r.random() * max(.5, w - 3), -r.random() * height
                    s += '<path d="M%.1f %.1f q%.1f -5 0 -%.1f" stroke="%s" stroke-width=".45" fill="none" opacity=".55"/>' % (gx, gy, r.random() * 1.4 - .7, 7 + r.random() * 12, dark(col, .45))
                if r.random() < .3:
                    ky = -r.random() * height * .8 - 4
                    s += '<ellipse cx="%.1f" cy="%.1f" rx="1.5" ry="2" fill="%s" opacity=".7"/>' % (x + w / 2, ky, dark(col, .5))
                for ny in (-height + 3.5, -4):
                    s += '<circle cx="%.1f" cy="%.1f" r=".65" fill="%s"/>' % (x + w / 2, ny, dark(col, .6))
                x += bw
        elif tex in ('plaster', 'timber'):
            for _ in range(int(length * height / 14)):
                x, y = r.random() * length, -r.random() * height
                s += '<circle cx="%.1f" cy="%.1f" r="%.2f" fill="%s" opacity=".5"/>' % (x, y, .35 + r.random() * .5, r.choice([lite(col, .5), dark(col, .25), dark(col, .15)]))
            for _ in range(int(length / 22) + 1):
                x, y = r.random() * length, -r.random() * height * .85 - 3
                s += '<path d="M%.1f %.1f l%.1f 3 l%.1f 4 l%.1f 3" stroke="%s" stroke-width=".4" fill="none" opacity=".6"/>' % (x, y, 1.5 - r.random() * 3, 1.5 - r.random() * 3, 1.5 - r.random() * 3, dark(col, .5))
            # a patch where the plaster fell off and the brick shows
            if length > 60:
                px_, py_ = length * (.15 + r.random() * .2), -8 - r.random() * 6
                for j in range(3):
                    for i in range(3 - j):
                        s += rect(px_ + i * 5.2 + j * 2.6, py_ - j * 2.9, 4.6, 2.4, r.choice(['#b8623e', '#a8553a', '#c47048']), '#5e3020', .25, .4)
            if tex == 'timber':
                beam = '#5e3c22'
                def b(x, y, w, h):
                    return rect(x, y, w, h, beam, dark(beam, .5), .35) + rect(x, y, w, min(h, 1), lite(beam, .3), None, 0, 0, .6)
                s += b(0, -height, length, 4.5) + b(0, -5, length, 5) + b(0, -height * .52, length, 4)
                n = max(2, int(length / 34))
                for i in range(n + 1):
                    s += b(min(length - 4.5, i * length / n), -height, 4.5, height)
                # braces in the outer bays
                for (xa, xb) in ((4, length / n), (length - 4, length - length / n)):
                    s += '<path d="M%.1f %.1f L%.1f %.1f" stroke="%s" stroke-width="4" fill="none"/>' % (xa, -6, xb, -height * .5, beam)
        elif tex in ('brick', 'stone'):
            bh = 4.2 if tex == 'brick' else 6.5
            mortar = dark(col, .45) if tex == 'brick' else dark(col, .5)
            s += rect(0, -height, length, height, mortar)
            row = 0
            y = 0
            while y < height:
                x = -(row % 2) * (4.5 if tex == 'brick' else 6)
                while x < length:
                    bw = 9 if tex == 'brick' else 10 + r.random() * 7
                    x0, x1 = max(0, x), min(length, x + bw - .7)
                    hh = min(bh - .7, height - y)
                    if x1 > x0 and hh > .5:
                        tone = r.choice([lite(col, .18), lite(col, .08), col, dark(col, .1), dark(col, .2)])
                        s += rect(x0, -y - hh, x1 - x0, hh, tone, None, 0, .9 if tex == 'stone' else .25)
                        s += rect(x0, -y - hh, x1 - x0, .7, lite(tone, .35), None, 0, 0, .7)
                    x += bw
                y += bh
                row += 1
        if z0 == 0:
            g = self.lg([(0, '#241408', 0), (1, '#241408', .42)])
            s += rect(0, -min(height, 14), length, min(height, 14), g)
        wall(self, side, u, v, z0, s)

    def box(self, u0, v0, u1, v1, z0, z1, col, top=None, edge=True, left=None, right=None, tex=None, seed=0):
        """A block: the left face lit, the right face in shade, the top lightest."""
        notop = top is False
        top = top or lite(col, .22)
        lf = left or col
        rt = right or mix(dark(col, .42), '#2c2c60', .14)
        e = dark(col, .45) if edge else None
        # the face looking left (v = v0 … nearest in v), the face looking right (u = u0)
        self.poly([(u0, v0, z0), (u1, v0, z0), (u1, v0, z1), (u0, v0, z1)], self.lg([(0, lite(rt, .06)), (1, dark(rt, .12))]), e, .9)
        self.poly([(u0, v0, z0), (u0, v1, z0), (u0, v1, z1), (u0, v0, z1)], self.lg([(0, lite(lf, .1)), (1, dark(lf, .1))]), e, .9)
        if not notop:
            self.poly([(u0, v0, z1), (u1, v0, z1), (u1, v1, z1), (u0, v1, z1)], top, e, .9)
        if tex:
            self.face('l', u0, v1, z0, (v1 - v0) * L, z1 - z0, tex, lf, seed)
            self.face('r', u0, v0, z0, (u1 - u0) * L, z1 - z0, tex, rt, seed + 1)

    def tiles(self, q, col, rows=9, cols=12, seed=0):
        """Shingles on a roof slope: q = eave-near, eave-far, ridge-far, ridge-near (3D points). Every shingle is
        its own shape with its own tone; each course overlaps the one below, with a lit lower edge and a shadow
        under it; the gaps between shingles show."""
        r = rnd(seed + 31)
        P = [self.p(*c) for c in q]

        def at(s, t):
            ax, ay = P[0][0] + (P[1][0] - P[0][0]) * s, P[0][1] + (P[1][1] - P[0][1]) * s
            bx, by = P[3][0] + (P[2][0] - P[3][0]) * s, P[3][1] + (P[2][1] - P[3][1]) * s
            return ax + (bx - ax) * t, ay + (by - ay) * t

        self.poly([at(0, 0), at(1, 0), at(1, 1), at(0, 1)], dark(col, .5), raw=True)
        gap = .012
        for j in range(rows - 1, -1, -1):
            t0, t1 = j / rows, (j + 1) / rows
            off = (j % 2) * .5 / cols
            i = -1
            while i < cols:
                s0, s1 = max(0, i / cols + off + gap * .5), min(1, (i + 1) / cols + off - gap * .5)
                i += 1
                if s1 <= s0:
                    continue
                tone = r.choice([lite(col, .2), lite(col, .1), col, col, dark(col, .08), dark(col, .16), dark(col, .26)])
                tone = mix(tone, lite(col, .3), t0 * .3)
                lift = (r.random() - .5) * .25 / rows
                self.poly([at(s0, t0 + lift), at(s1, t0 + lift), at(s1, t1 + .35 / rows), at(s0, t1 + .35 / rows)], tone, raw=True)
                # the lit lower edge of the shingle and its thickness
                self.line(at(s0, t0 + lift), at(s1, t0 + lift), dark(tone, .5), .9, .9)
                self.line(at(s0, t0 + lift + .13 / rows), at(s1, t0 + lift + .13 / rows), lite(tone, .45), .5, .8)

    def gable(self, u0, v0, u1, v1, z0, zr, col, over=.08, along='u', wall=None, edge=True, trim='#f2e2c0', tex=None, thick=6.0):
        """A gabled roof over a footprint, a real slab: shingles on the slope that looks at the viewer, a fascia
        board along its eave with the roof's thickness, barge boards along the gable's edges, a ridge cap. The
        ridge runs along u (the gable wall is the lit one) or along v (the gable wall is the shaded one)."""
        o = over
        ze = z0 - o * 30
        rows = 10
        if along == 'u':
            vm = (v0 + v1) / 2
            if wall:
                self.poly([(u0, v0, z0), (u0, v1, z0), (u0, vm, zr)], self.lg([(0, lite(wall, .1)), (1, dark(wall, .1))]))
                if tex:
                    self.gable_face('l', u0, v1, z0, (v1 - v0) * L, zr - z0, tex, wall)
            # the far slope's edge, then the slope that looks at the viewer
            self.poly([(u0 - o, vm, zr), (u1 + o, vm, zr), (u1 + o, v1 + o, ze), (u0 - o, v1 + o, ze)], dark(col, .4))
            q = [(u0 - o, v0 - o, ze), (u1 + o, v0 - o, ze), (u1 + o, vm, zr), (u0 - o, vm, zr)]
            self.poly([(u0, v0, ze - 1), (u1, v0, ze - 1), (u1, v0, ze - 13), (u0, v0, ze - 13)], '#241408', op=.3)
            self.tiles(q, dark(col, .1), rows, max(8, int((u1 - u0 + 2 * o) * 13)), int(u0 * 100))
            # the eave: the roof's thickness and a fascia board
            self.poly([(u0 - o, v0 - o, ze), (u1 + o, v0 - o, ze), (u1 + o, v0 - o, ze - thick), (u0 - o, v0 - o, ze - thick)], dark(trim, .38))
            self.poly([(u0 - o, v0 - o, ze - 1), (u1 + o, v0 - o, ze - 1), (u1 + o, v0 - o, ze - 2.4), (u0 - o, v0 - o, ze - 2.4)], lite(trim, .1), op=.8)
            # barge boards on the gable end (u0): two boards following the slopes
            for (va, vb) in ((v0 - o, vm), (v1 + o, vm)):
                self.poly([(u0 - o, va, ze), (u0 - o, vb, zr), (u0 - o, vb, zr - thick - 1), (u0 - o, va, ze - thick - 1)], self.lg([(0, lite(trim, .15)), (1, dark(trim, .15))]), dark(trim, .5), .5)
            ridge = [(u0 - o, vm, zr), (u1 + o, vm, zr)]
        else:
            um = (u0 + u1) / 2
            if wall:
                self.poly([(u0, v0, z0), (u1, v0, z0), (um, v0, zr)], self.lg([(0, dark(wall, .3)), (1, mix(dark(wall, .45), '#2c2c60', .14))]))
                if tex:
                    self.gable_face('r', u0, v0, z0, (u1 - u0) * L, zr - z0, tex, mix(dark(wall, .42), '#2c2c60', .14))
            self.poly([(um, v0 - o, zr), (um, v1 + o, zr), (u1 + o, v1 + o, ze), (u1 + o, v0 - o, ze)], dark(col, .42))
            q = [(u0 - o, v0 - o, ze), (u0 - o, v1 + o, ze), (um, v1 + o, zr), (um, v0 - o, zr)]
            self.poly([(u0, v0, ze - 1), (u0, v1, ze - 1), (u0, v1, ze - 13), (u0, v0, ze - 13)], '#241408', op=.26)
            self.tiles(q, lite(col, .06), rows, max(8, int((v1 - v0 + 2 * o) * 13)), int(v0 * 100))
            self.poly([(u0 - o, v0 - o, ze), (u0 - o, v1 + o, ze), (u0 - o, v1 + o, ze - thick), (u0 - o, v0 - o, ze - thick)], self.lg([(0, lite(trim, .1)), (1, dark(trim, .2))]))
            self.poly([(u0 - o, v0 - o, ze - thick + 1.2), (u0 - o, v1 + o, ze - thick + 1.2), (u0 - o, v1 + o, ze - thick), (u0 - o, v0 - o, ze - thick)], dark(trim, .5), op=.7)
            for (ua, ub) in ((u0 - o, um), (u1 + o, um)):
                self.poly([(ua, v0 - o, ze), (ub, v0 - o, zr), (ub, v0 - o, zr - thick - 1), (ua, v0 - o, ze - thick - 1)], dark(trim, .34), dark(trim, .6), .5)
            ridge = [(um, v0 - o, zr), (um, v1 + o, zr)]
        # the ridge cap: a row of rounded tiles along the top
        a_, b_ = self.p(*ridge[0]), self.p(*ridge[1])
        n = max(6, int(math.hypot(b_[0] - a_[0], b_[1] - a_[1]) / 9))
        self.line(a_, b_, dark(col, .55), 5.5)
        for i in range(n):
            p0 = (a_[0] + (b_[0] - a_[0]) * i / n, a_[1] + (b_[1] - a_[1]) * i / n)
            p1 = (a_[0] + (b_[0] - a_[0]) * (i + .86) / n, a_[1] + (b_[1] - a_[1]) * (i + .86) / n)
            self.line(p0, p1, lite(col, .12 if i % 2 else .22), 4)
            self.line((p0[0], p0[1] - 1.2), (p1[0], p1[1] - 1.2), lite(col, .5), .9, .8)

    def gable_face(self, side, u, v, z0, length, rise, tex, col):
        """The triangle of wall under a gable: a round vent with a frame, boards or beams as the wall below."""
        s = ''
        mid = length / 2
        if tex == 'planks':
            x = 0
            while x < length:
                top = rise * (1 - abs(x - mid) / mid)
                s += rect(x, -top, .7, top, dark(col, .5), None, 0, 0, .7)
                x += 7.5
        elif tex == 'timber':
            beam = '#5e3c22'
            s += rect(mid - 2.2, -rise + 3, 4.4, rise - 3, beam) + rect(0, -4.5, length, 4.5, beam)
        s += '<circle cx="%.1f" cy="%.1f" r="%.1f" fill="%s" stroke="%s" stroke-width="1.6"/>' % (mid, -rise * .38, min(7.5, rise * .2), '#2e2018', lite(col, .5))
        s += '<path d="M%.1f %.1f h%.1f M%.1f %.1f v%.1f" stroke="%s" stroke-width="1"/>' % (mid - 6, -rise * .38, 12, mid, -rise * .38 - 6, 12, lite(col, .5))
        wall(self, side, u, v, z0, s)

    def roof_lines(self, u0, v0, u1, v1, z0, zr, col, n=4, along='v', over=.08):
        """(The gable paints its own shingles now.)"""
        return
        c = dark(col, .25)
        for i in range(1, n):
            t = i / n
            if along == 'v':
                um = (u0 + u1) / 2
                a = self.p(u0 - over + (um - u0 + over) * t, v0 - over, z0 - over * 30 + (zr - z0 + over * 30) * t)
                b = self.p(u0 - over + (um - u0 + over) * t, v1 + over, z0 - over * 30 + (zr - z0 + over * 30) * t)
            else:
                vm = (v0 + v1) / 2
                a = self.p(u0 - over, v0 - over + (vm - v0 + over) * t, z0 - over * 30 + (zr - z0 + over * 30) * t)
                b = self.p(u1 + over, v0 - over + (vm - v0 + over) * t, z0 - over * 30 + (zr - z0 + over * 30) * t)
            self.line(a, b, c, 1.4, .55)

    def cyl(self, u, v, r, z0, z1, col, top=None, rtop=None):
        """An upright cylinder (a silo, a tower, a barrel); rtop narrows it."""
        x, y0 = self.p(u, v, z0)
        _, y1 = self.p(u, v, z1)
        rx0, rx1 = r * A * 1.0, (rtop if rtop is not None else r) * A
        ry0, ry1 = rx0 * B / A, rx1 * B / A
        g = self.lg([(0, lite(col, .25)), (.35, col), (1, dark(col, .38))], 0, 0, 1, 0)
        d = 'M%.1f %.1f L%.1f %.1f A%.1f %.1f 0 0 0 %.1f %.1f L%.1f %.1f A%.1f %.1f 0 0 1 %.1f %.1f Z' % (
            x - rx0, y0, x - rx1, y1, rx1, ry1, x + rx1, y1, x + rx0, y0, rx0, ry0, x - rx0, y0)
        self.path(d, g, dark(col, .45), .9)
        self.ell(x, y1, rx1, ry1, top or lite(col, .3), stroke=dark(col, .45), sw=.9)

    def cone(self, u, v, r, z0, z1, col):
        x, y0 = self.p(u, v, z0)
        _, y1 = self.p(u, v, z1)
        rx, ry = r * A, r * B
        g = self.lg([(0, lite(col, .2)), (.4, col), (1, dark(col, .4))], 0, 0, 1, 0)
        self.path('M%.1f %.1f L%.1f %.1f L%.1f %.1f A%.1f %.1f 0 0 1 %.1f %.1f Z' % (x - rx, y0, x, y1, x + rx, y0, rx, ry, x - rx, y0), g, dark(col, .45), .9)

    def door(self, u, v0, v1, z, col, side='left', u1=None):
        """A door or window on a wall: on the face looking left (u fixed) by default, else on the face looking right (v fixed)."""
        if side == 'left':
            self.poly([(u, v0, 0 if z[0] == 0 else z[0]), (u, v1, z[0]), (u, v1, z[1]), (u, v0, z[1])], col, dark(col, .5), 1)
        else:
            self.poly([(v0, u, z[0]), (v1, u, z[0]), (v1, u, z[1]), (v0, u, z[1])], col, dark(col, .5), 1)

    # ---------------------------------------------------------------- output
    def svg(self):
        s = self.scale
        return ('<svg xmlns="http://www.w3.org/2000/svg" width="%d" height="%d" viewBox="0 0 %s %s"><defs>%s</defs>%s</svg>'
                % (self.w * s, self.h * s, self.w, self.h, ''.join(self.defs), ''.join(self.body)))

    def image(self, ss=3):
        w, h = int(self.w * self.scale), int(self.h * self.scale)
        png = resvg_py.svg_to_bytes(svg_string=self.svg(), width=w * ss, height=h * ss)
        im = Image.open(io.BytesIO(bytes(png))).convert('RGBA').resize((w, h), Image.LANCZOS)
        return painterly(im, self.seed, px=self.scale, **self.look) if self.paint else im

    def save(self, name):
        os.makedirs(OUT, exist_ok=True)
        im = self.image()
        im.save(os.path.join(OUT, name + '.png'), optimize=True)
        return im


def rnd(seed):
    return random.Random(seed)


L = math.hypot(A, B)         # a tile's edge on the screen


def wall(a, side, u, v, z, svg):
    """Puts flat drawing (x to the right along the wall, y down, map units) on a wall.
    side 'l': the lit wall u = const, origin at its far end (v); side 'r': the shaded wall v = const, origin at its near end (u)."""
    x0, y0 = a.p(u, v, z)
    k = B / L if side == 'l' else -B / L
    a.add('<g transform="matrix(%.4f %.4f 0 1 %.1f %.1f)">%s</g>' % (A / L, k, x0, y0, svg))


def rect(x, y, w, h, fill, stroke=None, sw=1, rx=0, op=1):
    s = '<rect x="%.1f" y="%.1f" width="%.1f" height="%.1f" rx="%s" fill="%s"' % (x, y, w, h, rx, fill)
    if stroke:
        s += ' stroke="%s" stroke-width="%s"' % (stroke, sw)
    if op != 1:
        s += ' opacity="%s"' % op
    return s + '/>'


def window(x, y, w, h, frame='#fff4dc', glass='#8fd0f0', shutters=None, box=None, arch=False):
    """A window on a wall (flat, map units; y is its top): a frame with depth, glass with a reflection, mullions,
    a sill; shutters with slats; a flower box."""
    s = ''
    if shutters:
        sw_ = w * .46
        for sx in (x - sw_ - 2.5, x + w + 2.5):
            s += rect(sx, y - 1, sw_, h + 2, shutters, dark(shutters, .55), .5, .8)
            for k in range(1, int(h / 3.2)):
                s += rect(sx + 1.2, y - 1 + k * 3.2, sw_ - 2.4, .8, dark(shutters, .4)) + rect(sx + 1.2, y - 1 + k * 3.2 + .8, sw_ - 2.4, .5, lite(shutters, .35))
    rx = w / 2 if arch else 1
    s += rect(x - 2.6, y - 2.6, w + 5.2, h + 5.2, dark(frame, .5), None, 0, rx + 2) + rect(x - 2, y - 2, w + 4, h + 4, frame, None, 0, rx + 1.6)
    s += rect(x, y, w, h, dark(glass, .5), None, 0, rx) + rect(x + .8, y + 1.6, w - 1.6, h - 2.4, glass, None, 0, rx)
    s += '<path d="M%.1f %.1f l%.1f 0 l-%.1f %.1f l-%.1f 0 Z" fill="#ffffff" opacity=".6"/>' % (x + w * .25, y + 1.6, w * .3, h * .55, h * .78, w * .3 * .0 + w * .12)
    s += '<path d="M%.1f %.1f l%.1f 0 l-%.1f %.1f Z" fill="#ffffff" opacity=".35"/>' % (x + w * .68, y + 1.6, w * .16, w * .3, h * .5)
    s += rect(x + w / 2 - .9, y, 1.8, h, frame) + rect(x, y + h * .46 - .9, w, 1.8, frame)
    s += rect(x + w / 2 + .9, y, .5, h, dark(frame, .4), None, 0, 0, .6) + rect(x, y + h * .46 + .9, w, .5, dark(frame, .4), None, 0, 0, .6)
    # the sill, with its shadow on the wall
    s += rect(x - 4, y + h + 2, w + 8, 3, lite(frame, .2), dark(frame, .5), .4, .6) + rect(x - 3, y + h + 5, w + 6, 1.6, '#241408', None, 0, 0, .3)
    if box:
        s += rect(x - 3, y + h + 4.5, w + 6, 6.5, '#8a5a34', '#4a2a14', .5, 1) + rect(x - 3, y + h + 4.5, w + 6, 1.2, '#c79a5e')
        n = max(3, int(w / 5))
        for i in range(n):
            fx = x - 1 + (i + .5) * (w + 2) / n
            s += '<ellipse cx="%.1f" cy="%.1f" rx="3.2" ry="2.6" fill="%s"/>' % (fx, y + h + 3.4, ['#4f9a36', '#6fbf45'][i % 2])
            s += '<circle cx="%.1f" cy="%.1f" r="1.5" fill="%s"/><circle cx="%.1f" cy="%.1f" r=".55" fill="#fff2a0"/>' % (fx + (i % 3 - 1) * .8, y + h + 1.6 - (i % 2), box, fx + (i % 3 - 1) * .8, y + h + 1.6 - (i % 2))
    return s


def door(x, y, w, h, col='#8a5a34', arch=False, frame='#fff4dc', lamp=False):
    """A door (y is its top): a frame, planks with a brace, hinges, a handle, a stone step; a lamp over it."""
    rx = w / 2 if arch else 1
    s = rect(x - 3.4, y - 3.4, w + 6.8, h + 3.4, dark(frame, .5), None, 0, rx + 3) + rect(x - 2.6, y - 2.6, w + 5.2, h + 2.6, frame, None, 0, rx + 2.2)
    s += rect(x, y, w, h, dark(col, .45), None, 0, rx) + rect(x + .6, y + .6, w - 1.2, h - .6, col, None, 0, rx)
    n = max(3, int(w / 5.5))
    for i in range(1, n):
        px_ = x + i * w / n
        s += rect(px_ - .4, y + (w * .3 if arch else 1), .8, h - (w * .3 if arch else 1), dark(col, .5)) + rect(px_ + .4, y + (w * .3 if arch else 1), .5, h - (w * .3 if arch else 1), lite(col, .3), None, 0, 0, .7)
    for yy in (y + h * .3, y + h * .8):
        s += rect(x + .6, yy, w - 1.2, 2.6, dark(col, .22), dark(col, .55), .3)
        s += rect(x + .6, yy + .6, 5, 1.4, '#3a3a40')
    s += '<path d="M%.1f %.1f L%.1f %.1f" stroke="%s" stroke-width="2.2"/>' % (x + 1.5, y + h * .8, x + w - 1.5, y + h * .32 + 2.6, dark(col, .25))
    s += rect(x + w - 5.2, y + h * .5, 2.2, 6, '#3a3a40', None, 0, 1) + '<circle cx="%.1f" cy="%.1f" r="1.7" fill="#f2c94c" stroke="#8a6a14" stroke-width=".4"/>' % (x + w - 4.1, y + h * .56)
    s += rect(x - 5, y + h, w + 10, 3.2, '#b8b0a4', '#6a645c', .4, .6)
    if lamp:
        lx, ly = x + w + 7, y + 2
        s += '<path d="M%.1f %.1f h4 v3" stroke="#3a3a40" stroke-width="1.2" fill="none"/>' % (lx - 4, ly)
        s += rect(lx - 2.4, ly + 3, 4.8, 6.5, '#ffe9a0', '#3a3a40', .8, .8) + rect(lx - 3, ly + 2.2, 6, 1.4, '#3a3a40') + '<circle cx="%.1f" cy="%.1f" r="1.3" fill="#fff8d0"/>' % (lx, ly + 6.4)
    return s


def barrel(a, u, v, r=.13, h=26, col='#a8703f'):
    """A cask: staves, two iron hoops, the head with its boards."""
    x, y0 = a.p(u, v, 0)
    rx, ry = r * A, r * B
    a.ell(x + rx * .5, y0 + 1, rx * 1.15, ry * 1.1, '#0e2010', .4, a.blur(1.5))
    bulge = 1.14
    g = a.lg([(0, lite(col, .28)), (.3, col), (1, dark(col, .45))], 0, 0, 1, 0)
    a.path('M%.1f %.1f Q%.1f %.1f %.1f %.1f A%.1f %.1f 0 0 0 %.1f %.1f Q%.1f %.1f %.1f %.1f A%.1f %.1f 0 0 1 %.1f %.1f Z' % (
        x - rx, y0, x - rx * bulge, y0 - h / 2, x - rx, y0 - h, rx, ry, x + rx, y0 - h, x + rx * bulge, y0 - h / 2, x + rx, y0, rx, ry, x - rx, y0), g, dark(col, .55), .5)
    for i in range(-3, 4):
        t = i / 3.5
        a.path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (x + rx * t, y0 + ry * math.sqrt(max(0, 1 - t * t)), x + rx * t * bulge, y0 - h / 2 + ry * math.sqrt(max(0, 1 - t * t)), x + rx * t, y0 - h + ry * math.sqrt(max(0, 1 - t * t))),
               stroke=dark(col, .5), sw=.6, op=.8)
    for hz in (h * .22, h * .78):
        k = 1 + (bulge - 1) * (1 - abs(hz / h - .5) * 2) * .9
        a.path('M%.1f %.1f A%.1f %.1f 0 0 0 %.1f %.1f' % (x - rx * k, y0 - hz, rx * k, ry * k, x + rx * k, y0 - hz), stroke='#3a3a40', sw=2.6)
        a.path('M%.1f %.1f A%.1f %.1f 0 0 0 %.1f %.1f' % (x - rx * k, y0 - hz - .7, rx * k, ry * k, x + rx * k * .2, y0 - hz - .7 + ry * k), stroke='#9aa0a8', sw=.7)
    a.ell(x, y0 - h, rx, ry, lite(col, .22), stroke=dark(col, .55), sw=.6)
    a.ell(x, y0 - h, rx * .8, ry * .8, lite(col, .32), stroke=dark(col, .4), sw=.4)
    for i in (-1, 0, 1):
        a.line((x + i * rx * .3, y0 - h - ry * .72), (x + i * rx * .3, y0 - h + ry * .72), dark(col, .35), .5, .8)


def crate(a, u, v, s=.3, h=14, col='#b98454'):
    """A wooden crate: boards on both faces, corner posts, a rim."""
    a.ell(a.p(u + s / 2, v + s / 2)[0] + 6, a.p(u, v)[1] + 1, s * A * 1.1, s * B * 1.1, '#0e2010', .35, a.blur(1.5))
    a.box(u, v, u + s, v + s, 0, h, col, tex='planks', seed=int(u * 50), top=dark(col, .55))
    for (pu, pv) in ((u, v), (u + s, v), (u, v + s)):
        p0, p1 = a.p(pu, pv, 0), a.p(pu, pv, h)
        a.line(p0, p1, dark(col, .4), 2.4)
        a.line((p0[0] - .5, p0[1]), (p1[0] - .5, p1[1]), lite(col, .3), .8)
    a.poly([(u, v, h), (u + s, v, h), (u + s, v + s, h), (u, v + s, h)], 'none', lite(col, .35), 1.6)
