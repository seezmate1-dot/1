"""The breakable layer under the pieces («трава», 1–2 layers) as raised plates, and the clod that flies when one breaks.

Grass1: a turf plate on a soil edge. Grass2: the tougher one — thicker, darker turf inside a wooden rim with nails.
Both fill the cell edge to edge (drawn at 0.98 of the cell), so neighbours leave the same thin gap everywhere.
"""
import random
from lib import *

def out(folder, name, svg, size=256):
    d = os.path.join(HERE, folder); os.makedirs(d, exist_ok=True)
    open(os.path.join(d, name + '.svg'), 'w').write(svg)
    png = resvg_py.svg_to_bytes(svg_string=svg, width=size * 3, height=size * 3)
    im = Image.open(io.BytesIO(bytes(png))).convert('RGBA').resize((size, size), Image.LANCZOS)
    im.save(os.path.join(d, name + '.png')); print(folder, name, im.getbbox())

def rr(x, y, w, h, r, fill, extra=''):
    return f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{r}" fill="{fill}" {extra}/>'

def tuft(x, y, k, col, dark):
    d = f'M{x-9*k} {y} Q{x-8*k} {y-12*k} {x-13*k} {y-19*k} Q{x-3*k} {y-14*k} {x-2*k} {y-4*k} Q{x} {y-18*k} {x+2*k} {y-26*k} Q{x+6*k} {y-14*k} {x+4*k} {y-4*k} Q{x+7*k} {y-13*k} {x+14*k} {y-17*k} Q{x+9*k} {y-9*k} {x+9*k} {y} Z'
    return f'<path d="{d}" fill="{col}" stroke="{dark}" stroke-width="{3.2*k:.1f}" stroke-linejoin="round"/>'

def plate(layers):
    s = S()
    two = layers == 2
    m, lip = 6, (40 if two else 26)                 # margin, the visible side edge
    W = 256 - 2 * m
    top_h = W - lip
    OUT = '#2e1a08' if two else '#3e2a0c'
    # the side: soil (1) or a stacked wooden base (2)
    side = s.LG([(0, '#8a5226'), (1, '#5e3410')]) if not two else s.LG([(0, '#9a6232'), (1, '#5a3210')])
    s.add(rr(m, m, W, W, 30, OUT))
    s.add(rr(m + 6, m + 6, W - 12, W - 12, 25, side))
    if two:
        s.add(pa(f'M{m+8} {256-m-22} H{256-m-8}', stroke='#3e2008', sw=4, op=.7) + pa(f'M{m+10} {256-m-27} H{256-m-10}', stroke='#e8b87a', sw=3, op=.45))
        s.add(''.join(pa(f'M{x} {256-m-lip} V{256-m-8}', stroke='#3e2008', sw=3, op=.55) for x in (70, 128, 186)))
    else:
        s.add(''.join(el(x, 256 - m - 13 + (i % 2) * 4, 5, 3.5, '#3e2008', .55) for i, x in enumerate((44, 92, 150, 204))))
    # the top face
    if two:
        wood = s.LG([(0, '#e8b87a'), (1, '#b9783a')])
        s.add(rr(m + 6, m + 6, W - 12, top_h - 6, 25, wood))
        s.add(rr(m + 9, m + 9, W - 18, top_h - 12, 22, 'none', 'stroke="#fff0d0" stroke-width="3" opacity=".7"'))
        k = 24
        s.add(rr(m + k - 3, m + k - 3, W - 2 * k + 6, top_h - 2 * k + 6, 20, '#3e2008'))
        g = s.LG([(0, '#6fbf3c'), (1, '#3f9426')])
        gx, gy, gw, gh, gr = m + k, m + k, W - 2 * k, top_h - 2 * k, 17
        for cx, cy in ((m + 16, m + 16), (256 - m - 16, m + 16), (m + 16, m + top_h - 14), (256 - m - 16, m + top_h - 14)):
            s.add(f'<circle cx="{cx}" cy="{cy+1.5}" r="5.5" fill="#3e2008"/><circle cx="{cx}" cy="{cy}" r="4.8" fill="#dfe6ee"/><circle cx="{cx-1.3}" cy="{cy-1.3}" r="1.8" fill="#fff"/>')
        dark, light, tcol = '#1e5c12', '#a6e06a', '#58b030'
    else:
        g = s.LG([(0, '#a4e05a'), (1, '#6cbf3a')])
        gx, gy, gw, gh, gr = m + 6, m + 6, W - 12, top_h - 6, 25
        dark, light, tcol = '#2f7a1a', '#d6f79a', '#8ad44a'
    i = uid('gc'); s.defs.append(f'<clipPath id="{i}"><rect x="{gx}" y="{gy}" width="{gw}" height="{gh}" rx="{gr}"/></clipPath>')
    s.add(rr(gx, gy, gw, gh, gr, g))
    rnd = random.Random(layers)
    inner = rr(gx + 2.5, gy + 3, gw - 5, gh + 20, gr - 2, 'none', f'stroke="{light}" stroke-width="5" opacity=".75"')      # bevel: light top edge
    inner += rr(gx - 6, gy + gh - 12, gw + 12, 16, 0, dark, 'opacity=".35" filter="url(#b2)"')                                  # darker towards the front edge
    for _ in range(9 if two else 6):                                                                                              # low-contrast mowing marks
        x, y = gx + 18 + rnd.random() * (gw - 36), gy + 22 + rnd.random() * (gh - 40)
        inner += pa(f'M{x-7:.0f} {y:.0f} l5 -9 l4 9 M{x+3:.0f} {y+2:.0f} l4 -7', stroke=dark, sw=3, op=.28)
    s.add(f'<g clip-path="url(#{i})">{inner}</g>')
    # tufts over the front edge: few on layer 1, a row on layer 2
    xs = (58, 198) if not two else (50, 104, 152, 206)
    for j, x in enumerate(xs):
        s.add(tuft(x, m + top_h + (3 if not two else -2), .8 if not two else .9 + .15 * (j % 2), tcol, dark))
    return s.svg()

def turf():
    """A clod: soil with grass on top (the debris of a broken plate)."""
    s = S()
    soil = s.LG([(0, '#9a6232'), (1, '#5e3410')])
    s.shape('M22 60 Q20 44 36 42 H92 Q108 44 106 60 L96 96 Q92 108 80 108 H48 Q36 108 32 96 Z', soil, '#2e1a08', el(50, 82, 6, 4, '#3e2008', .6) + el(78, 92, 5, 3.5, '#3e2008', .6), sw=4)
    g = s.LG([(0, '#a4e05a'), (1, '#5cb030')])
    s.shape('M16 56 Q14 34 34 32 L40 18 L50 32 L62 14 L72 32 L86 20 L92 34 Q114 36 112 56 Q110 66 98 64 Q64 58 30 64 Q18 66 16 56Z', g, '#1e5c12', pa('M30 42 Q64 36 98 42', stroke='#e8ffc0', sw=4, op=.7, blur=1), sw=4)
    svg = s.svg(vb='0 0 128 128').replace('width="256" height="256"', 'width="128" height="128"')
    out('Fx', 'Turf', svg, 128)

if __name__ == '__main__':
    out('Obstacles', 'Grass1', plate(1)); out('Obstacles', 'Grass2', plate(2)); turf()
