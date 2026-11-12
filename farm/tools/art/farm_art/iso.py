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


def painterly(im, seed=1, grain=.07, volume=.24, rim=.4, contrast=1.14, sat=1.18):
    """The painted look of the zone backgrounds, over a clean vector render: uneven brushy tone instead of flat
    fills, the whole thing lighter towards the light (top left) and darker away from it, a dark turn of the form
    along the edges that look away from the light and a warm lit edge on the side that faces it."""
    a = np.asarray(im).astype(np.float32) / 255
    rgb, al = a[..., :3].copy(), a[..., 3]
    h, w = al.shape
    ys, xs = np.nonzero(al > .05)
    if len(xs) == 0:
        return im
    rng = np.random.default_rng(seed)
    n = .5 * _noise(rng, w, h, 26) + .3 * _noise(rng, w, h, 9) + .2 * _noise(rng, w, h, 3)
    rgb *= (1 + grain * n)[..., None]
    # a little warm–cool drift, as paint has
    t = _noise(rng, w, h, 40) * .022
    rgb[..., 0] += t
    rgb[..., 2] -= t
    x0, x1, y0, y1 = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
    u = (np.arange(w, dtype=np.float32) - x0) / max(1, x1 - x0)
    v = (np.arange(h, dtype=np.float32) - y0) / max(1, y1 - y0)
    g = 1 + volume * .5 - volume * (.45 * u[None, :] + .55 * v[:, None])
    rgb *= g[..., None]
    # the form turns away at the lower right edge and catches the light at the upper left one
    d = max(2, int(min(w, h) * .018))
    A = Image.fromarray((al * 255).astype(np.uint8))

    def shifted(dx, dy):
        out = Image.new('L', A.size, 0)
        out.paste(A, (dx, dy))
        return np.asarray(out).astype(np.float32) / 255

    away = np.clip(al - shifted(-d, -d), 0, 1)
    lit = np.clip(al - shifted(d, d), 0, 1)
    blur = lambda m: np.asarray(Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(d * .9))).astype(np.float32) / 255
    away, lit = blur(away), blur(lit)
    rgb *= (1 - rim * away)[..., None]
    rgb += (np.array([1.0, .96, .8], dtype=np.float32)[None, None, :] - rgb) * (rim * .55 * lit)[..., None]
    # the backgrounds are saturated and contrasty
    grey = rgb.mean(axis=2, keepdims=True)
    rgb = grey + (rgb - grey) * sat
    rgb = (rgb - .5) * contrast + .5
    out = np.dstack([np.clip(rgb, 0, 1), al])
    return Image.fromarray((out * 255 + .5).astype(np.uint8), 'RGBA')


class Art:
    """One sprite: a canvas in map units with an origin on the ground (the footprint's nearest corner)."""

    def __init__(self, w, h, ox=None, oy=None, scale=2.0):
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
    def shadow(self, u0, v0, u1, v1, h=.5, op=.46, sd=4):
        """The contact shadow of a block: its footprint pushed right along the ground."""
        d = h
        lst = [(u0, v0), (u1, v0), (u1 + d, v0 - d * .35), (u1 + d, v1 - d * .35), (u1, v1), (u0, v1)]
        self.poly([(u0 + .02, v0), (u1 + d, v0 - d * .4), (u1 + d, v1 - d * .4), (u1, v1 + .02), (u0, v1)], '#0e2010', op=op, flt=self.blur(sd))

    def blob_shadow(self, u, v, r, op=.3, sd=4, squash=.5):
        x, y = self.p(u, v)
        self.ell(x + r * A * .25, y + 2, r * A, r * A * squash, '#1c2a10', op, self.blur(sd))

    def face(self, side, u, v, z0, length, height, tex, col, seed=0):
        """Material on a wall (length along it × height, map units): 'planks', 'plaster', 'brick', 'stone'; and the
        dark where it meets the ground."""
        r = rnd(seed + int(u * 97) + int(v * 53) + (7 if side == 'l' else 0))
        s = ''
        if tex == 'planks':
            x = 0
            while x < length:
                bw = 7 + r.random() * 4
                tone = r.choice([lite(col, .12), dark(col, .1), dark(col, .2), col, lite(col, .05)])
                s += rect(x, -height, min(bw, length - x), height, tone, None, 0, 0, .55)
                s += '<line x1="%.1f" y1="%.1f" x2="%.1f" y2="0" stroke="%s" stroke-width=".9" opacity=".55"/>' % (x, -height, x, dark(col, .5))
                for _ in range(2):
                    gx, gy = x + 1.5 + r.random() * (bw - 3), -r.random() * height
                    s += '<line x1="%.1f" y1="%.1f" x2="%.1f" y2="%.1f" stroke="%s" stroke-width=".6" opacity=".4"/>' % (gx, gy, gx + r.random() - .5, gy - 6 - r.random() * 10, dark(col, .4))
                x += bw
        elif tex == 'plaster':
            for _ in range(int(length * height / 90)):
                x, y = r.random() * length, -r.random() * height
                s += '<ellipse cx="%.1f" cy="%.1f" rx="%.1f" ry="%.1f" fill="%s" opacity="%.2f"/>' % (x, y, 3 + r.random() * 9, 2 + r.random() * 5, r.choice([lite(col, .3), dark(col, .18), dark(col, .1)]), .12 + r.random() * .14)
            for _ in range(3):
                x, y = r.random() * length, -r.random() * height * .8
                s += '<path d="M%.1f %.1f l%.1f %.1f l%.1f %.1f" stroke="%s" stroke-width=".6" fill="none" opacity=".35"/>' % (x, y, 2 - r.random() * 4, 5, 3 - r.random() * 4, 6, dark(col, .5))
        elif tex in ('brick', 'stone'):
            bh = 5 if tex == 'brick' else 8
            row = 0
            y = 0
            while y < height:
                x = -(row % 2) * 6
                while x < length:
                    bw = (11 if tex == 'brick' else 12 + r.random() * 8)
                    x0, x1 = max(0, x), min(length, x + bw - 1)
                    if x1 > x0:
                        s += rect(x0, -y - bh + .6, x1 - x0, bh - 1.2, r.choice([lite(col, .14), col, dark(col, .14), dark(col, .24)]), None, 0, 1.2 if tex == 'stone' else .4, .8)
                    x += bw
                y += bh
                row += 1
        if z0 == 0:
            g = self.lg([(0, '#241408', 0), (1, '#241408', .34)])
            s += rect(0, -min(height, 16), length, min(height, 16), g)
        wall(self, side, u, v, z0, s)

    def box(self, u0, v0, u1, v1, z0, z1, col, top=None, edge=True, left=None, right=None, tex=None, seed=0):
        """A block: the left face lit, the right face in shade, the top lightest."""
        top = top or lite(col, .22)
        lf = left or col
        rt = right or mix(dark(col, .42), '#2c2c60', .14)
        e = dark(col, .45) if edge else None
        # the face looking left (v = v0 … nearest in v), the face looking right (u = u0)
        self.poly([(u0, v0, z0), (u1, v0, z0), (u1, v0, z1), (u0, v0, z1)], self.lg([(0, lite(rt, .06)), (1, dark(rt, .12))]), e, .9)
        self.poly([(u0, v0, z0), (u0, v1, z0), (u0, v1, z1), (u0, v0, z1)], self.lg([(0, lite(lf, .1)), (1, dark(lf, .1))]), e, .9)
        self.poly([(u0, v0, z1), (u1, v0, z1), (u1, v1, z1), (u0, v1, z1)], top, e, .9)
        if tex:
            self.face('l', u0, v1, z0, (v1 - v0) * L, z1 - z0, tex, lf, seed)
            self.face('r', u0, v0, z0, (u1 - u0) * L, z1 - z0, tex, rt, seed + 1)

    def tiles(self, q, col, rows=8, cols=12, seed=0):
        """Shingles on a roof slope: q = eave-near, eave-far, ridge-far, ridge-near (3D points). Courses overlap:
        each casts a thin shadow on the one below, tiles differ a little in tone."""
        r = rnd(seed + 31)
        P = [self.p(*c) for c in q]

        def at(s, t):
            ax, ay = P[0][0] + (P[1][0] - P[0][0]) * s, P[0][1] + (P[1][1] - P[0][1]) * s
            bx, by = P[3][0] + (P[2][0] - P[3][0]) * s, P[3][1] + (P[2][1] - P[3][1]) * s
            return ax + (bx - ax) * t, ay + (by - ay) * t

        for j in range(rows - 1, -1, -1):
            t0, t1 = j / rows, (j + 1) / rows
            off = (j % 2) * .5 / cols
            i = -1
            while i < cols:
                s0, s1 = max(0, i / cols + off), min(1, (i + 1) / cols + off)
                i += 1
                if s1 <= s0:
                    continue
                tone = r.choice([lite(col, .16), lite(col, .06), col, col, dark(col, .1), dark(col, .2)])
                tone = mix(tone, lite(col, .25), t0 * .35)
                self.poly([at(s0, t0), at(s1, t0), at(s1, t1 + .02), at(s0, t1 + .02)], tone, raw=True)
                self.line(at(s0, t0), at(s0, t1), dark(col, .45), .6, .35)
            self.line(at(0, t0), at(1, t0), dark(col, .55), 1.5, .5)
            self.line(at(0, t0 + .28 / rows), at(1, t0 + .28 / rows), lite(col, .4), .7, .3)

    def gable(self, u0, v0, u1, v1, z0, zr, col, over=.08, along='u', wall=None, edge=True):
        """A gabled roof over a footprint: the ridge runs along u (gable end looks left) or v (gable end looks right)."""
        e = dark(col, .45) if edge else None
        o = over
        if along == 'u':
            vm = (v0 + v1) / 2
            # the gable wall under the roof, looking… the end at u0 faces the viewer's right side
            if wall:
                self.poly([(u0, v0, z0), (u0, v1, z0), (u0, vm, zr)], self.lg([(0, lite(wall, .1)), (1, dark(wall, .1))]), dark(wall, .45), .9)
            # the slope looking at the viewer (v0 side) and a sliver of the far one
            self.poly([(u0 - o, vm, zr), (u1 + o, vm, zr), (u1 + o, v1 + o, z0 - o * 30), (u0 - o, v1 + o, z0 - o * 30)], dark(col, .3), e, .9)
            q = [(u0 - o, v0 - o, z0 - o * 30), (u1 + o, v0 - o, z0 - o * 30), (u1 + o, vm, zr), (u0 - o, vm, zr)]
            self.poly(q, dark(col, .12), e, .9)
            self.tiles(q, dark(col, .12), 7, max(6, int((u1 - u0) * 9)), int(u0 * 100))
            # the eave's shadow on the wall under it
            self.poly([(u0, v0, z0 - o * 30 - 1), (u1, v0, z0 - o * 30 - 1), (u1, v0, z0 - o * 30 - 12), (u0, v0, z0 - o * 30 - 12)], '#241408', op=.22)
        else:
            um = (u0 + u1) / 2
            if wall:
                self.poly([(u0, v0, z0), (u1, v0, z0), (um, v0, zr)], self.lg([(0, lite(wall, .0)), (1, dark(wall, .3))]), dark(wall, .45), .9)
            self.poly([(um, v0 - o, zr), (um, v1 + o, zr), (u1 + o, v1 + o, z0 - o * 30), (u1 + o, v0 - o, z0 - o * 30)], dark(col, .32), e, .9)
            q = [(u0 - o, v0 - o, z0 - o * 30), (u0 - o, v1 + o, z0 - o * 30), (um, v1 + o, zr), (um, v0 - o, zr)]
            self.poly(q, lite(col, .08), e, .9)
            self.tiles(q, lite(col, .08), 7, max(6, int((v1 - v0) * 9)), int(v0 * 100))
            self.poly([(u0, v0, z0 - o * 30 - 1), (u0, v1, z0 - o * 30 - 1), (u0, v1, z0 - o * 30 - 12), (u0, v0, z0 - o * 30 - 12)], '#241408', op=.2)

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

    def image(self, ss=2):
        w, h = int(self.w * self.scale), int(self.h * self.scale)
        png = resvg_py.svg_to_bytes(svg_string=self.svg(), width=w * ss, height=h * ss)
        im = Image.open(io.BytesIO(bytes(png))).convert('RGBA').resize((w, h), Image.LANCZOS)
        return painterly(im, self.seed, **self.look) if self.paint else im

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


def window(x, y, w, h, frame='#fff4dc', glass='#8fd0f0'):
    return (rect(x - 2, y - 2, w + 4, h + 4, frame, dark(frame, .45), .8, 1.5) + rect(x, y, w, h, glass, None, 0, 1)
            + '<path d="M%.1f %.1f l%.1f 0 l-%.1f %.1f Z" fill="#ffffff" opacity=".55"/>' % (x, y, w * .7, w * .7, h * .7)
            + rect(x + w / 2 - .8, y, 1.6, h, frame) + rect(x, y + h / 2 - .8, w, 1.6, frame))


def door(x, y, w, h, col='#8a5a34', arch=False):
    s = rect(x - 2, y - 2, w + 4, h + 2, dark(col, .35), None, 0, 2 if not arch else w / 2) + rect(x, y, w, h, col, None, 0, 1 if not arch else w / 2 - 1)
    for i in range(1, 3):
        s += rect(x + i * w / 3 - .5, y + (w / 3 if arch else 0), 1, h - (w / 3 if arch else 0), dark(col, .3))
    return s + '<circle cx="%.1f" cy="%.1f" r="1.6" fill="#f2c94c"/>' % (x + w - 4, y + h * .55)
