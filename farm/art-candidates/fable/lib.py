"""Shared helpers for hand-written SVG sprites (Sunny Valley art candidates)."""
import io, math, os
import resvg_py
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
_n = [0]
def uid(p='e'):
    _n[0] += 1
    return f'{p}{_n[0]}'

FILTERS = ''.join(
    f'<filter id="b{n}" x="-60%" y="-60%" width="220%" height="220%"><feGaussianBlur stdDeviation="{s}"/></filter>'
    for n, s in [(1, 1.2), (2, 2.2), (3, 3.5), (5, 5), (8, 8), (12, 12)])

def stops(st):
    out = ''
    for s in st:
        off, col = s[0], s[1]
        op = f' stop-opacity="{s[2]}"' if len(s) > 2 else ''
        out += f'<stop offset="{off}" stop-color="{col}"{op}/>'
    return out

def rg(st, cx=.38, cy=.3, r=.85, units=None):
    i = uid('g')
    u = ' gradientUnits="userSpaceOnUse"' if units else ''
    return f'url(#{i})', f'<radialGradient id="{i}" cx="{cx}" cy="{cy}" r="{r}"{u}>{stops(st)}</radialGradient>'

def lg(st, x1=0, y1=0, x2=0, y2=1, units=None):
    i = uid('g')
    u = ' gradientUnits="userSpaceOnUse"' if units else ''
    return f'url(#{i})', f'<linearGradient id="{i}" x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}"{u}>{stops(st)}</linearGradient>'

class S:
    """Accumulates defs + body."""
    def __init__(self):
        self.defs = []
        self.body = []
    def RG(self, st, **k):
        u, d = rg(st, **k); self.defs.append(d); return u
    def LG(self, st, **k):
        u, d = lg(st, **k); self.defs.append(d); return u
    def add(self, s):
        self.body.append(s)
    def shape(self, d, fill, outline, shade='', sw=4.5, tr='', extra=''):
        """Outlined shape with clipped inner shading. d: path data or element with {ID}."""
        i = uid('p')
        el = d.replace('{ID}', f'id="{i}"') if d.lstrip().startswith('<') else f'<path id="{i}" d="{d}"/>'
        self.defs.append(el + f'<clipPath id="{i}c"><use href="#{i}"/></clipPath>')
        g = f'<g transform="{tr}">' if tr else '<g>'
        if outline:
            g += f'<use href="#{i}" fill="{outline}" stroke="{outline}" stroke-width="{sw*2}" stroke-linejoin="round" stroke-linecap="round"/>'
        g += f'<use href="#{i}" fill="{fill}" {extra}/>'
        if shade:
            g += f'<g clip-path="url(#{i}c)">{shade}</g>'
        g += '</g>'
        self.body.append(g)
    def svg(self, tr='', vb='0 0 256 256'):
        return (f'<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="256" height="256" viewBox="{vb}">'
                f'<defs>{FILTERS}{"".join(self.defs)}</defs><g transform="{tr}">{"".join(self.body)}</g></svg>')

def el(cx, cy, rx, ry, fill='#fff', op=1, blur=0, rot=0):
    f = f' filter="url(#b{blur})"' if blur else ''
    t = f' transform="rotate({rot} {cx} {cy})"' if rot else ''
    return f'<ellipse cx="{cx}" cy="{cy}" rx="{rx}" ry="{ry}" fill="{fill}" opacity="{op}"{f}{t}/>'

def pa(d, fill='none', stroke=None, sw=0, op=1, blur=0, tr='', cap='round'):
    f = f' filter="url(#b{blur})"' if blur else ''
    s = f' stroke="{stroke}" stroke-width="{sw}" stroke-linecap="{cap}" stroke-linejoin="round"' if stroke else ''
    t = f' transform="{tr}"' if tr else ''
    return f'<path d="{d}" fill="{fill}"{s} opacity="{op}"{f}{t}/>'

def gloss(x, y, rx, ry, rot=-25, op=.9):
    """Soft halo + crisp specular blob + tiny dot."""
    return (el(x + rx * .5, y + ry * .5, rx * 2.2, ry * 2.0, '#fff', .32, 8, rot)
            + el(x, y, rx, ry, '#fff', op, 1, rot)
            + el(x + rx * 1.5, y + ry * 1.7, rx * .32, rx * .32, '#fff', op * .9, 1))

def star(cx, cy, n, ro, ri, rot=-90, sy=1.0):
    pts = []
    for k in range(n * 2):
        r = ro if k % 2 == 0 else ri
        a = math.radians(rot + k * 180 / n)
        pts.append((cx + r * math.cos(a), cy + r * math.sin(a) * sy))
    return 'M' + ' L'.join(f'{x:.1f} {y:.1f}' for x, y in pts) + 'Z'

def render(svg, size=256, ss=3):
    png = resvg_py.svg_to_bytes(svg_string=svg, width=size * ss, height=size * ss)
    im = Image.open(io.BytesIO(bytes(png))).convert('RGBA')
    return im.resize((size, size), Image.LANCZOS) if ss > 1 else im

PAD = 20
def build(folder, name, s, fit=True, pad=PAD):
    """Render; auto-fit the artwork into the canvas with uniform padding; write .svg + .png."""
    tr = ''
    if fit:
        im = render(s.svg(vb='-128 -128 512 512'), size=1024, ss=1)
        im = im.point(lambda v: 255 if v > 40 else 0) if False else im
        x0, y0, x1, y1 = [v / 2 - 128 for v in im.getchannel('A').point(lambda v: 255 if v > 24 else 0).getbbox()]
        w, h = x1 - x0, y1 - y0
        k = (256 - 2 * pad) / max(w, h)
        tx = 128 - k * (x0 + x1) / 2
        ty = 128 - k * (y0 + y1) / 2
        tr = f'translate({tx:.2f} {ty:.2f}) scale({k:.4f})'
    svg = s.svg(tr)
    d = os.path.join(HERE, folder); os.makedirs(d, exist_ok=True)
    open(os.path.join(d, name + '.svg'), 'w').write(svg)
    im = render(svg)
    im.save(os.path.join(d, name + '.png'))
    print(folder, name, im.getbbox())
