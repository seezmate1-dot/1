import random
from lib import *
from specials import hx, tube

STEEL = [(0, '#6f7b88'), (.22, '#ffffff'), (.55, '#b4c0cc'), (1, '#5e6a77')]
NS = 'vector-effect="non-scaling-stroke"'

def sprout(x, y, k=1.0):
    """Small two-leaf seedling standing at (x, y)."""
    return (el(x, y + 1, 9 * k, 3.5 * k, '#1a0c04', .55, 1)
            + pa(f'M{x} {y} C {x} {y-8*k}, {x-1*k} {y-14*k}, {x} {y-20*k}', stroke='#1f6410', sw=7 * k)
            + pa(f'M{x} {y} C {x} {y-8*k}, {x-1*k} {y-14*k}, {x} {y-20*k}', stroke='#7fd03a', sw=3.5 * k)
            + pa(f'M{x} {y-18*k} C {x-6*k} {y-30*k}, {x-18*k} {y-30*k}, {x-22*k} {y-24*k} C {x-16*k} {y-16*k}, {x-6*k} {y-14*k}, {x} {y-18*k} Z',
                 '#8fe044', stroke='#1f6410', sw=2.6 * k)
            + pa(f'M{x} {y-18*k} C {x+6*k} {y-32*k}, {x+20*k} {y-32*k}, {x+24*k} {y-26*k} C {x+18*k} {y-16*k}, {x+6*k} {y-14*k}, {x} {y-18*k} Z',
                 '#6cc22a', stroke='#1f6410', sw=2.6 * k)
            + el(x - 13 * k, y - 25 * k, 4 * k, 1.6 * k, '#eaffb0', .8, 0, -20))

# ---------------------------------------------------------------- Chain
def chain():
    s = S()
    OUT = '#232a33'
    def run(angle):
        g = f'<g transform="rotate({angle} 128 128)">'
        xs = list(range(-6, 263, 33))
        for i, x in enumerate(xs):          # edge-on links
            g += (f'<rect x="{x+10}" y="121" width="34" height="14" rx="7" fill="#8792a0" stroke="{OUT}" stroke-width="4"/>'
                  f'<rect x="{x+15}" y="123.5" width="24" height="3.5" rx="1.7" fill="#fff" opacity=".7"/>')
        for i, x in enumerate(xs):          # face-on links
            g += (f'<ellipse cx="{x+11}" cy="128" rx="17" ry="12" fill="none" stroke="{OUT}" stroke-width="15"/>'
                  f'<ellipse cx="{x+11}" cy="128" rx="17" ry="12" fill="none" stroke="#aab5c2" stroke-width="7.5"/>'
                  f'<path d="M{x-3} 121 A 17 12 0 0 1 {x+22} 119" fill="none" stroke="#fff" stroke-width="3" stroke-linecap="round" opacity=".85"/>'
                  f'<path d="M{x} 137 A 17 12 0 0 0 {x+25} 135" fill="none" stroke="#5a6572" stroke-width="3" stroke-linecap="round" opacity=".8"/>')
        return g + '</g>'
    cid = uid('c'); s.defs.append(f'<clipPath id="{cid}"><rect x="20" y="20" width="216" height="216" rx="40"/></clipPath>')
    s.add(f'<g clip-path="url(#{cid})">{run(45)}{run(-45)}</g>')
    tube(s, 'M108 122 V104 C 108 78, 148 78, 148 104 V122', 10, '#b4c0cc', OUT, '#fff', ow=4)
    gf = s.LG([(0, '#ffe680'), (.5, '#ffbf1e'), (1, '#d98a00')])
    s.shape('<rect {ID} x="92" y="116" width="72" height="58" rx="12"/>', gf, '#5e3200',
            el(128, 180, 40, 10, '#8a4a00', .5, 4) + pa('M100 124 H156', stroke='#fff', sw=4, op=.7, blur=1)
            + '<circle cx="128" cy="140" r="8" fill="#4a2600"/><rect x="124" y="140" width="8" height="20" rx="3" fill="#4a2600"/>')
    build('Obstacles', 'Chain', s, pad=14)

# ---------------------------------------------------------------- Bed (long, low, rectangular plank bed in 3/4 view)
def bed():
    s = S()
    OUT = '#3a1c06'
    # outer top rim (trapezoid: back edge slightly narrower) and front face of the border
    bl, br, by = 30, 226, 96        # back edge
    fl, fr_, fy = 12, 244, 160      # front edge
    fh = 17                          # height of the front plank face
    s.add(el(128, fy + fh + 4, 126, 14, '#1a0c04', .45, 5))
    # grass tufts behind / at the sides
    def tuft(x, y, k=1.0, flip=1):
        d = (f'M{x-10*k} {y} Q{x-12*k} {y-12*k} {x-16*k} {y-20*k} Q{x-6*k} {y-12*k} {x-3*k} {y-4*k} '
             f'Q{x-1*k} {y-18*k} {x+2*k} {y-26*k} Q{x+6*k} {y-14*k} {x+5*k} {y-4*k} '
             f'Q{x+10*k} {y-14*k} {x+18*k} {y-18*k} Q{x+12*k} {y-8*k} {x+10*k} {y} Z')
        s.shape(d, s.LG([(0, '#b8ec52'), (1, '#4b9e22')]), '#1f5c10', sw=3)
    tuft(36, by + 2, .9); tuft(222, by + 2, .8)
    wood = s.LG([(0, '#f2c483'), (1, '#c8873e')])
    face = s.LG([(0, '#d0904a'), (1, '#8e5222')])
    # front face
    s.shape(f'M{fl} {fy} H{fr_} V{fy+fh} H{fl} Z', face, OUT,
            pa(f'M{fl} {fy+fh/2} H{fr_}', stroke='#6e3a12', sw=2.5, op=.55)
            + ''.join(pa(f'M{x} {fy} V{fy+fh}', stroke='#6e3a12', sw=2.5, op=.6) for x in (92, 170))
            + pa(f'M{fl+2} {fy+3} H{fr_-2}', stroke='#ffe0b0', sw=2.5, op=.6), sw=4)
    # side faces (left/right ends, visible as thin slivers)
    s.shape(f'M{fl} {fy} L{bl} {by} V{by+fh*.6} L{fl} {fy+fh} Z', s.LG([(0, '#a86a30'), (1, '#6e3e14')]), OUT, sw=4)
    s.shape(f'M{fr_} {fy} L{br} {by} V{by+fh*.6} L{fr_} {fy+fh} Z', s.LG([(0, '#7a4618'), (1, '#4e2a0c')]), OUT, sw=4)
    # top rim with inner cut-out
    w = 11
    il, ir, iy = bl + w * .8, br - w * .8, by + w * .7
    jl, jr, jy = fl + w * 1.3, fr_ - w * 1.3, fy - w
    rim = f'M{bl} {by} H{br} L{fr_} {fy} H{fl} Z M{il} {iy} L{jl} {jy} H{jr} L{ir} {iy} Z'
    s.shape(f'<path {{ID}} fill-rule="evenodd" clip-rule="evenodd" d="{rim}"/>', wood, OUT,
            pa(f'M{bl+4} {by+3} H{br-4}', stroke='#fff', sw=2.5, op=.6)
            + pa(f'M{il} {iy} L{bl} {by} M{ir} {iy} L{br} {by} M{jl} {jy} L{fl} {fy} M{jr} {jy} L{fr_} {fy}', stroke='#8e5222', sw=2, op=.7), sw=4)
    # soil inside with three straight lengthwise ridges
    inner = f'M{il} {iy} L{jl} {jy} H{jr} L{ir} {iy} Z'
    soil = s.LG([(0, '#2e180a'), (1, '#1e0e04')])
    rows = []
    n = 3
    o = ''
    for r in range(n):
        t0 = (r + .5) / n
        y = iy + (jy - iy) * t0
        xl = il + (jl - il) * t0 + 4
        xr = ir + (jr - ir) * t0 - 4
        hh = 8 + 4 * t0                       # half height of a ridge (bigger at front)
        g = s.LG([(0, '#7e5232'), (.5, '#4e2e16'), (1, '#2a1608')])
        o += el((xl + xr) / 2, y + hh + 2, (xr - xl) / 2 + 2, 4, '#0e0602', .8, 1)
        o += f'<rect x="{xl:.1f}" y="{y-hh:.1f}" width="{xr-xl:.1f}" height="{2*hh:.1f}" rx="{hh:.1f}" fill="{g}" stroke="#1a0a02" stroke-width="2.5"/>'
        o += pa(f'M{xl+6:.1f} {y-hh+3:.1f} ' + ' '.join(f'L{xl+6+(xr-xl-12)*q/12:.1f} {y-hh+3+(1.2 if q%2 else -.6):.1f}' for q in range(1, 13)), stroke='#b08058', sw=2.2, op=.7)
        random.seed(r * 7 + 1)
        for _ in range(26):
            o += f'<circle cx="{random.uniform(xl+6, xr-6):.1f}" cy="{y + random.uniform(-hh*.6, hh*.7):.1f}" r="{random.uniform(1.3, 2.6):.1f}" fill="{random.choice(["#c99a6a", "#1a0a02"])}" opacity=".6"/>'
        rows.append((y, xl, xr, .7 + .35 * t0))
    s.shape(inner, soil, '', o + f'<path d="{inner}" fill="none" stroke="#0e0602" stroke-width="8" opacity=".55" filter="url(#b2)"/>')
    for y, xl, xr, k in rows:
        m = 4
        off = (rows.index((y, xl, xr, k)) % 2) * .5
        for j in range(m):
            x = xl + 22 + (xr - xl - 44) * (j + off * .6) / (m - 1 + off * .6)
            s.add(sprout(x, y + 1, .72 * k))
    # crumbs and front grass
    for x, y, r in ((24, fy + fh + 6, 5), (206, fy + fh + 8, 6), (120, fy + fh + 9, 4), (232, fy + fh + 2, 4)):
        s.shape(f'<ellipse {{ID}} cx="{x}" cy="{y}" rx="{r*1.3}" ry="{r}"/>', s.RG([(0, '#9a6a3a'), (1, '#4a2a10')]), OUT, el(x - 2, y - 2, r * .5, r * .3, '#e0b080', .7), sw=2.5)
    tuft(60, fy + fh + 8, .75); tuft(176, fy + fh + 9, .7); tuft(246, fy + fh, .6)
    build('Obstacles', 'Bed', s)

# ---------------------------------------------------------------- Crates (3/4 view: front + top + right side)
X0, Y0, W, H, DX, DY, D = 20, 92, 160, 148, 56, -46, 100
FRONT = 'matrix(1 0 0 1 %d %d)' % (X0, Y0)
TOP = f'matrix(1 0 {-DX/D} {-DY/D} {X0+DX} {Y0+DY})'      # local (u, v): v=0 back, v=D front
SIDE = f'matrix({DX/D} {DY/D} 0 1 {X0+W} {Y0})'           # local (w, h): w=0 front edge

def crate_face(fw, fh, lvl, pal, brace, ns=NS):
    """Draws one face in local coordinates 0..fw x 0..fh."""
    fr, inn, line, hi = pal
    b = 16
    o = f'<rect width="{fw}" height="{fh}" fill="{fr}"/>'
    o += f'<rect x="{b}" y="{b}" width="{fw-2*b}" height="{fh-2*b}" fill="{inn}" stroke="{line}" stroke-width="3" {ns}/>'
    n = 4
    for i in range(1, n):
        x = b + (fw - 2 * b) * i / n
        o += f'<path d="M{x} {b} V{fh-b}" stroke="{line}" stroke-width="3" opacity=".75" {ns}/>'
        o += f'<path d="M{x+2.5} {b} V{fh-b}" stroke="{hi}" stroke-width="1.5" opacity=".35" {ns}/>'
    o += f'<rect x="{b}" y="{b}" width="{fw-2*b}" height="{fh-2*b}" fill="none" stroke="#1a0a02" stroke-width="7" opacity=".35"/>'
    if brace:
        for d in (['M{0} {1} L{2} {3}'.format(b, fh - b, fw - b, b)] + (['M{0} {1} L{2} {3}'.format(b, b, fw - b, fh - b)] if lvl == 3 else [])):
            o += f'<path d="{d}" stroke="{line}" stroke-width="{b*1.5}" stroke-linecap="butt"/><path d="{d}" stroke="{fr}" stroke-width="{b*1.1}" stroke-linecap="butt"/>'
        o += f'<rect x="{b}" y="{b}" width="{fw-2*b}" height="{fh-2*b}" fill="none" stroke="{line}" stroke-width="3" {ns}/>'
    o += f'<path d="M0 {b} H{fw} M0 {fh-b} H{fw} M{b} 0 V{fh} M{fw-b} 0 V{fh}" stroke="{line}" stroke-width="2.5" opacity=".55" {ns}/>'
    if lvl == 1:
        for x, y in ((b / 2, b / 2), (fw - b / 2, b / 2), (b / 2, fh - b / 2), (fw - b / 2, fh - b / 2), (fw / 2, b / 2), (fw / 2, fh - b / 2)):
            o += f'<circle cx="{x}" cy="{y}" r="3.2" fill="{line}"/><circle cx="{x-.8}" cy="{y-.8}" r="1.2" fill="#ffe6b8"/>'
    if lvl >= 2:
        c = 30
        for sx, sy in ((0, 0), (1, 0), (0, 1), (1, 1)):
            tx, ty = sx * fw, sy * fh
            mx, my = (-1 if sx else 1), (-1 if sy else 1)
            p = f'M{tx} {ty} H{tx+mx*c} L{tx+mx*c*.6} {ty+my*b} H{tx+mx*b} V{ty+my*c*.6} L{tx} {ty+my*c} Z'
            o += f'<path d="{p}" fill="url(#steelA)" stroke="#262d36" stroke-width="3" stroke-linejoin="round" {ns}/>'
            o += f'<circle cx="{tx+mx*8}" cy="{ty+my*8}" r="3.4" fill="#3a444e"/><circle cx="{tx+mx*8-1}" cy="{ty+my*8-1}" r="1.3" fill="#fff"/>'
    if lvl >= 2:
        y = fh / 2
        o += f'<rect x="-1" y="{y-13}" width="{fw+2}" height="26" fill="url(#steelB)" stroke="#262d36" stroke-width="3" {ns}/>'
        for x in (fw * .14, fw * .38, fw * .62, fw * .86):
            o += f'<circle cx="{x}" cy="{y}" r="3.6" fill="#3a444e"/><circle cx="{x-1}" cy="{y-1}" r="1.4" fill="#fff"/>'
    return o

def crate(name, lvl):
    s = S()
    s.defs.append(f'<linearGradient id="steelA" x1="0" y1="0" x2="1" y2="1">{stops(STEEL)}</linearGradient>')
    s.defs.append(f'<linearGradient id="steelB" x1="0" y1="0" x2="0" y2="1">{stops(STEEL)}</linearGradient>')
    if lvl < 3:
        pal = ('#e9b773', '#c48542', '#6e3e14', '#fff')
        out = '#3e1e06'
    else:
        pal = ('#9a6634', '#6a4020', '#2e1606', '#fff')
        out = '#1e0e02'
    hexp = f'M{X0} {Y0} L{X0+DX} {Y0+DY} L{X0+W+DX} {Y0+DY} L{X0+W+DX} {Y0+H+DY} L{X0+W} {Y0+H} L{X0} {Y0+H} Z'
    s.add(el(X0 + W / 2 + 30, Y0 + H + 4, 130, 18, '#1a0c04', .45, 5))
    s.add(f'<path d="{hexp}" fill="{out}" stroke="{out}" stroke-width="10" stroke-linejoin="round"/>')
    faces = [(FRONT, W, H, 0, True), (TOP, W, D, .30, False), (SIDE, D, H, -.32, True)]
    for tr, fw, fh, shade, br in faces:
        tint = (f'<rect width="{fw}" height="{fh}" fill="#fff" opacity="{shade}"/>' if shade > 0 else
                f'<rect width="{fw}" height="{fh}" fill="#1a0800" opacity="{-shade}"/>' if shade < 0 else '')
        s.add(f'<g transform="{tr}">{crate_face(fw, fh, lvl, pal, br)}{tint}</g>')
    edges = f'M{X0} {Y0} H{X0+W} L{X0+W+DX} {Y0+DY} M{X0+W} {Y0} V{Y0+H}'
    s.add(pa(edges, stroke=out, sw=4) + pa(f'M{X0+4} {Y0+3} H{X0+W-4}', stroke='#fff', sw=2.5, op=.55)
          + pa(f'M{X0+W+3} {Y0+4} V{Y0+H-4}', stroke='#fff', sw=2, op=.3))
    if lvl == 3:   # padlock on the front band
        cx, cy = X0 + W / 2, Y0 + H / 2
        s.add(pa(f'M{cx-11} {cy-4} V{cy-16} C {cx-11} {cy-32}, {cx+11} {cy-32}, {cx+11} {cy-16} V{cy-4}', stroke='#262d36', sw=12)
              + pa(f'M{cx-11} {cy-4} V{cy-16} C {cx-11} {cy-32}, {cx+11} {cy-32}, {cx+11} {cy-16} V{cy-4}', stroke='#c8d2dc', sw=5))
        gf = s.LG([(0, '#ffe680'), (.5, '#ffbf1e'), (1, '#d98a00')])
        s.shape(f'<rect {{ID}} x="{cx-20}" y="{cy-8}" width="40" height="34" rx="8"/>', gf, '#5e3200',
                f'<circle cx="{cx}" cy="{cy+6}" r="5.5" fill="#4a2600"/><rect x="{cx-3}" y="{cy+6}" width="6" height="13" rx="2" fill="#4a2600"/>'
                + pa(f'M{cx-14} {cy-3} H{cx+14}', stroke='#fff', sw=3, op=.7, blur=1), sw=3.5)
    build('Obstacles', name, s)

# ---------------------------------------------------------------- Ice (translucent block, 3/4 view like the crates)
def ice():
    s = S()
    OUT = '#1f64a8'
    hexp = f'M{X0} {Y0} L{X0+DX} {Y0+DY} L{X0+W+DX} {Y0+DY} L{X0+W+DX} {Y0+H+DY} L{X0+W} {Y0+H} L{X0} {Y0+H} Z'
    s.add(el(X0 + W / 2 + 30, Y0 + H + 4, 130, 18, '#0a2a50', .35, 5))
    s.add(f'<path d="{hexp}" fill="none" stroke="{OUT}" stroke-width="10" stroke-linejoin="round"/>')
    fr = s.LG([(0, '#e8f9ff', .82), (.55, '#9ad8ff', .72), (1, '#5ab0f0', .82)], x1=0, y1=0, x2=1, y2=1)
    tp = s.LG([(0, '#ffffff', .92), (1, '#c8eeff', .85)])
    sd = s.LG([(0, '#6cbcf4', .85), (1, '#3a8ad8', .9)])
    s.add(f'<path d="M{X0} {Y0} H{X0+W} V{Y0+H} H{X0} Z" fill="{fr}"/>')
    s.add(f'<path d="M{X0} {Y0} L{X0+DX} {Y0+DY} H{X0+W+DX} L{X0+W} {Y0} Z" fill="{tp}"/>')
    s.add(f'<path d="M{X0+W} {Y0} L{X0+W+DX} {Y0+DY} V{Y0+H+DY} L{X0+W} {Y0+H} Z" fill="{sd}"/>')
    # inner bevels
    s.add(f'<rect x="{X0+12}" y="{Y0+12}" width="{W-24}" height="{H-24}" rx="10" fill="none" stroke="#fff" stroke-width="4" opacity=".6"/>')
    s.add(f'<g transform="{SIDE}"><rect x="10" y="12" width="{D-20}" height="{H-24}" rx="8" fill="none" stroke="#fff" stroke-width="3" opacity=".35" {NS}/></g>')
    s.add(f'<g transform="{TOP}"><rect x="12" y="12" width="{W-24}" height="{D-24}" rx="8" fill="none" stroke="#7ac4f0" stroke-width="3" opacity=".6" {NS}/></g>')
    # diagonal glints, frost, cracks, sparkles
    cid = uid('c'); s.defs.append(f'<clipPath id="{cid}"><rect x="{X0}" y="{Y0}" width="{W}" height="{H}"/></clipPath>')
    s.add(f'<g clip-path="url(#{cid})">' + pa(f'M{X0-10} {Y0+110} L{X0+110} {Y0-10} L{X0+140} {Y0-10} L{X0-10} {Y0+140} Z', '#fff', op=.5, blur=1)
          + pa(f'M{X0-10} {Y0+170} L{X0+170} {Y0-10} L{X0+180} {Y0-10} L{X0-10} {Y0+180} Z', '#fff', op=.35, blur=1)
          + el(X0 + W / 2, Y0 + H, W * .6, 22, '#ffffff', .45, 5) + '</g>')
    s.add(pa(f'M{X0+120} {Y0+H} L{X0+104} {Y0+H-34} L{X0+122} {Y0+H-58} M{X0+104} {Y0+H-34} L{X0+76} {Y0+H-40}', stroke='#fff', sw=3.5, op=.9)
          + pa(f'M{X0+W+10} {Y0+30} L{X0+W+26} {Y0+56} L{X0+W+20} {Y0+86}', stroke='#d8f2ff', sw=3, op=.8))
    s.add(pa(star(X0 + 36, Y0 + 34, 4, 20, 5), '#fff') + pa(star(X0 + W + 30, Y0 - 10, 4, 12, 3), '#fff', op=.95)
          + pa(star(X0 + 134, Y0 + 26, 4, 9, 2.5), '#fff', op=.9))
    edges = f'M{X0} {Y0} H{X0+W} L{X0+W+DX} {Y0+DY} M{X0+W} {Y0} V{Y0+H}'
    s.add(pa(edges, stroke=OUT, sw=3.5, op=.8) + pa(f'M{X0+4} {Y0+3} H{X0+W-4}', stroke='#fff', sw=3, op=.9) + pa(f'M{X0+W+3} {Y0+4} V{Y0+H-4}', stroke='#fff', sw=2.5, op=.6))
    build('Obstacles', 'Ice', s)

# ---------------------------------------------------------------- Stone (faceted boulder)
def stone():
    s = S()
    OUT = '#272d36'
    s.add(el(128, 214, 112, 20, '#10141a', .45, 5))
    outline = 'M34 188 L28 140 L50 96 L92 64 L148 54 L194 72 L224 112 L232 160 L214 196 L168 210 L98 212 Z'
    s.shape(outline, '#8d96a3', OUT, '', sw=5)
    facets = [  # (points, colour)
        ('M50 96 L92 64 L148 54 L130 104 L76 118 Z', '#d4dae2'),
        ('M148 54 L194 72 L224 112 L172 124 L130 104 Z', '#b3bcc7'),
        ('M28 140 L50 96 L76 118 L64 170 L34 188 Z', '#9aa4b0'),
        ('M76 118 L130 104 L172 124 L160 176 L98 182 L64 170 Z', '#a9b2be'),
        ('M172 124 L224 112 L232 160 L214 196 L160 176 Z', '#7a8492'),
        ('M34 188 L64 170 L98 182 L160 176 L214 196 L168 210 L98 212 Z', '#646d7a'),
    ]
    cid = uid('c'); s.defs.append(f'<clipPath id="{cid}"><path d="{outline}"/></clipPath>')
    o = f'<g clip-path="url(#{cid})">'
    for d, c in facets:
        o += f'<path d="{d}" fill="{c}" stroke="#5a6370" stroke-width="2" stroke-linejoin="round"/>'
    # speckles
    random.seed(5)
    for _ in range(40):
        x, y = random.uniform(40, 220), random.uniform(64, 206)
        o += f'<circle cx="{x:.0f}" cy="{y:.0f}" r="{random.uniform(1.2,2.6):.1f}" fill="{random.choice(["#4a525e", "#e6eaef"])}" opacity=".5"/>'
    # crack
    o += pa('M136 58 L124 92 L140 112 L118 140 L130 166 L118 188', stroke='#1e232a', sw=5)
    o += pa('M140 112 L162 126', stroke='#1e232a', sw=3.5) + pa('M118 140 L96 146', stroke='#1e232a', sw=3)
    o += pa('M140 60 L128 92 L144 112 L122 140 L134 166', stroke='#fff', sw=1.8, op=.55)
    # chip on the right
    o += '<path d="M224 112 L210 128 L232 140 Z" fill="#4e5763" stroke="#272d36" stroke-width="2.5"/>'
    o += gloss(82, 86, 8, 13, 40, .7) + '</g>'
    s.add(o)
    s.add(f'<path d="{outline}" fill="none" stroke="{OUT}" stroke-width="5" stroke-linejoin="round"/>')
    # moss clumps on top
    for (cx, cy, rx, ry) in ((96, 76, 34, 14), (176, 74, 22, 10), (46, 118, 12, 18)):
        pts = []
        for k in range(28):
            a = k / 28 * 2 * math.pi
            r = 1 + .18 * math.sin(5 * a + cx)
            pts.append((cx + rx * r * math.cos(a), cy + ry * r * math.sin(a)))
        d = 'M' + ' L'.join(f'{x:.1f} {y:.1f}' for x, y in pts) + 'Z'
        s.shape(d, s.LG([(0, '#b6ec5a'), (1, '#4f9a22')]), '#1f5410',
                ''.join(f'<circle cx="{cx+rx*math.cos(a)*.5:.1f}" cy="{cy+ry*math.sin(a)*.4-2:.1f}" r="2.4" fill="#e2ff9a" opacity=".7"/>' for a in (0.3, 2.2, 4.1)), sw=3)
    # pebble
    s.shape('<ellipse {ID} cx="226" cy="204" rx="16" ry="11"/>', s.RG([(0, '#dfe4ea'), (1, '#7d8795')]), OUT, el(220, 199, 5, 3, '#fff', .8, 1), sw=3.5)
    build('Obstacles', 'Stone', s)

# ---------------------------------------------------------------- Honey (puddle + comb + stuck bee)
def honey():
    s = S()
    OUT = '#7a3e00'
    pts = []
    for k in range(200):
        a = k / 200 * 2 * math.pi
        r = 1 + .07 * math.sin(6 * a + .8) + .04 * math.sin(11 * a)
        pts.append((128 + 106 * r * math.cos(a), 170 + 54 * r * math.sin(a)))
    pud = 'M' + ' L'.join(f'{x:.1f} {y:.1f}' for x, y in pts) + 'Z'
    s.add(el(128, 196, 112, 40, '#3a1c00', .4, 5))
    # depth (front edge) then top surface
    s.shape(pud, '#c06a00', OUT, '', tr='translate(0 10)')
    f = s.RG([(0, '#fff2a0'), (.35, '#ffcc2e'), (.8, '#f39d00'), (1, '#d97c00')], cx=.4, cy=.3, r=.75)
    s.shape(pud, f, OUT, el(128, 210, 100, 18, '#b85c00', .45, 8) + el(80, 150, 30, 8, '#fff', .55, 3, -8) + el(66, 148, 8, 4, '#fff', .95, 1, -8)
            + el(176, 196, 18, 4, '#fff', .6, 2, -6) + ''.join(el(x, y, r, r * .6, '#fff8c8', .8) + el(x - r * .3, y - r * .25, r * .35, r * .25, '#fff') for x, y, r in ((52, 176, 6), (196, 160, 4.5), (110, 200, 3.5))))
    # honeycomb chunk
    hx_ = lambda cx, cy, r: 'M' + ' L'.join(f'{cx + r*math.cos(math.radians(60*i)):.1f} {cy + r*math.sin(math.radians(60*i)):.1f}' for i in range(6)) + 'Z'
    wax = s.LG([(0, '#ffe08a'), (1, '#e7a21c')])
    R = 22
    cy0 = 132
    cells = [(146, cy0), (146 + 1.5 * R, cy0 - R * .866), (146 + 1.5 * R, cy0 + R * .866), (146, cy0 - 2 * R * .866), (146 + 3 * R, cy0), (146 - 1.5 * R, cy0 + R * .866)]
    comb = ' '.join(hx_(x, y, R) for x, y in cells)
    s.shape(comb, wax, OUT, '', sw=4.5)
    for i, (x, y) in enumerate(cells):
        filled = i % 2 == 0
        inner = s.RG([(0, '#ffe680'), (1, '#e08a00')]) if filled else s.RG([(0, '#8a4a00'), (1, '#c87a14')], cx=.6, cy=.7)
        s.add(f'<path d="{hx_(x, y + 1, R * .68)}" fill="{inner}" stroke="#a85e00" stroke-width="2.5"/>'
              + (el(x - 5, y - 5, 4, 2.5, '#fff', .85, 0, -30) if filled else ''))
    # honey pooling around the base of the comb
    s.add(pa('M96 164 C 120 154, 150 170, 170 168 C 196 166, 214 160, 226 166 C 222 182, 190 190, 160 188 C 130 188, 104 182, 96 164 Z', '#ffc22a')
          + el(150, 172, 34, 4, '#fff6c0', .6, 2))
    # drips running from the comb into the puddle
    for x, y0, y1 in ((146, 148, 178), (196, 158, 184)):
        s.shape(f'M{x-7} {y0} V{y1-8} C {x-7} {y1+4}, {x+7} {y1+4}, {x+7} {y1-8} V{y0} Z', '#f6a800', OUT, el(x - 3, (y0 + y1) / 2, 2, 10, '#fff', .75, 1), sw=3.5)
    # small stuck bee (half sunk)
    bx, by = 84, 182
    bf = s.RG([(0, '#fff48a'), (.6, '#ffc820'), (1, '#e09000')])
    s.add(f'<g transform="rotate(-18 {bx} {by})">'
          + f'<ellipse cx="{bx-6}" cy="{by-18}" rx="12" ry="7" fill="#e6f6ff" opacity=".85" stroke="#3f78ad" stroke-width="2.5" transform="rotate(-30 {bx-6} {by-18})"/>'
          + f'<ellipse cx="{bx+8}" cy="{by-19}" rx="11" ry="6.5" fill="#e6f6ff" opacity=".85" stroke="#3f78ad" stroke-width="2.5" transform="rotate(25 {bx+8} {by-19})"/>'
          + f'<ellipse cx="{bx}" cy="{by}" rx="20" ry="14" fill="{bf}" stroke="#4a2608" stroke-width="3.5"/>'
          + f'<path d="M{bx-6} {by-13} Q{bx-2} {by} {bx-6} {by+13} M{bx+5} {by-13} Q{bx+9} {by} {bx+5} {by+13}" stroke="#3a1c08" stroke-width="5"/>'
          + f'<circle cx="{bx-22}" cy="{by-4}" r="10" fill="{bf}" stroke="#4a2608" stroke-width="3.5"/>'
          + f'<circle cx="{bx-25}" cy="{by-6}" r="2.8" fill="#2a1408"/><circle cx="{bx-26}" cy="{by-7}" r="1" fill="#fff"/>'
          + pa(f'M{bx-28} {by-12} Q{bx-34} {by-24} {bx-38} {by-24} M{bx-22} {by-14} Q{bx-22} {by-26} {bx-18} {by-28}', stroke='#4a2608', sw=2.5) + '</g>')
    # honey covering the bee's lower half
    s.add(f'<path d="M50 186 C 64 194, 104 196, 118 186 C 116 200, 60 204, 50 186 Z" fill="#f7ae10" opacity=".95"/>' + el(70, 192, 10, 2, '#fff', .6, 1))
    build('Obstacles', 'Honey', s)

# ---------------------------------------------------------------- Basket (wicker weave)
def basket():
    s = S()
    OUT = '#3e1e06'
    cx, top, bot, rx_t, rx_b = 128, 118, 222, 100, 76
    s.add(el(128, 226, 100, 18, '#1a0c04', .45, 5))
    # handle (behind the opening)
    def braid(d, w):
        return (pa(d, stroke=OUT, sw=w + 9) + pa(d, stroke='#c98a44', sw=w)
                + f'<path d="{d}" fill="none" stroke="#8a5220" stroke-width="{w}" stroke-dasharray="5 9" opacity=".8"/>'
                + pa(d, stroke='#f6d49a', sw=w * .3, op=.6, tr='translate(-2 -3)'))
    s.add(braid(f'M{cx-82} {top} C {cx-80} 10, {cx+80} 10, {cx+82} {top}', 16))
    # inside of the basket
    s.add(f'<ellipse cx="{cx}" cy="{top}" rx="{rx_t}" ry="26" fill="#8a5220" stroke="{OUT}" stroke-width="9"/>')
    s.add(f'<ellipse cx="{cx}" cy="{top+8}" rx="{rx_t-14}" ry="16" fill="#4a2408"/>' + pa(f'M{cx-rx_t+12} {top} Q {cx} {top-40} {cx+rx_t-12} {top}', stroke='#c98a44', sw=3, op=.6))
    # body with weave
    body = f'M{cx-rx_t} {top} C {cx-rx_t+4} {top+50}, {cx-rx_b-6} {bot-20}, {cx-rx_b} {bot-6} Q {cx} {bot+22} {cx+rx_b} {bot-6} C {cx+rx_b+6} {bot-20}, {cx+rx_t-4} {top+50}, {cx+rx_t} {top} Q {cx} {top+28} {cx-rx_t} {top} Z'
    rows, cols = 11, 10
    weave = '<rect x="0" y="0" width="256" height="256" fill="#3a1804"/>'
    rh = (bot + 16 - (top + 6)) / rows
    def geom(yc):
        t_ = (yc - top) / (bot - top)
        rx = rx_t + (rx_b - rx_t) * t_ + 8
        sag = 22 * (1 - t_ * .3)
        return rx, sag
    def ypos(x, yc):
        rx, sag = geom(yc)
        return yc + sag * (1 - min(1, ((x - cx) / rx) ** 2))
    stake_x = lambda c, yc: cx + geom(yc)[0] * math.sin(-math.pi / 2 + math.pi * (c + .5) / cols)
    # vertical stakes underneath (visible in the gaps between rods)
    for c in range(cols):
        pts = [(stake_x(c, top + 4 + k * 8), ypos(stake_x(c, top + 4 + k * 8), top + 4 + k * 8)) for k in range(int((bot + 20 - top) / 8))]
        d = 'M' + ' L'.join(f'{x:.1f} {y:.1f}' for x, y in pts)
        weave += pa(d, stroke='#2a1002', sw=11) + pa(d, stroke='#a8682a', sw=7)
    # long thin horizontal willow rods
    for r in range(rows):
        yc = top + 8 + r * rh
        rx, sag = geom(yc)
        d = f'M{cx-rx-8:.1f} {yc:.1f} Q {cx:.1f} {yc + 2*sag:.1f} {cx+rx+8:.1f} {yc:.1f}'
        light = '#ffd28a' if r % 2 else '#f2bc70'
        weave += pa(d, stroke='#2a1002', sw=rh - .6) + pa(d, stroke=light, sw=rh - 3.6) + pa(d, stroke='#fff2cc', sw=1.4, op=.6, tr='translate(0 -1.6)')
        # where the stake passes OVER this rod, redraw a stake segment with shadows on the rod
        for c in range(cols):
            if (r + c) % 2: continue
            x = stake_x(c, yc)
            y = ypos(x, yc)
            ang = -math.pi / 2 + math.pi * (c + .5) / cols
            w = 10 * max(.35, math.cos(ang)) + 2
            weave += el(x - w * .9, y, w * .5, rh * .45, '#2a1002', .55, 1) + el(x + w * .9, y, w * .5, rh * .45, '#2a1002', .55, 1)
            weave += f'<rect x="{x-w/2:.1f}" y="{y-rh/2-1:.1f}" width="{w:.1f}" height="{rh+2:.1f}" rx="{w/2:.1f}" fill="#d8964c" stroke="#2a1002" stroke-width="1.8"/>'
            weave += f'<rect x="{x-w/2+1.4:.1f}" y="{y-rh/2:.1f}" width="1.6" height="{rh:.1f}" fill="#ffe2a8" opacity=".6"/>'
    shade = s.LG([(0, '#2a1000', .7), (.25, '#2a1000', 0), (.7, '#2a1000', 0), (1, '#2a1000', .75)], x1=0, y1=0, x2=1, y2=0)
    weave += f'<path d="{body}" fill="{shade}"/>' + el(cx, bot + 8, 80, 16, '#2a1000', .5, 5) + el(cx - 54, top + 50, 8, 34, '#fff', .3, 5, 10)
    s.shape(body, '#5a2e0a', OUT, weave)
    # braided rim (front half over body)
    rimd = f'M{cx-rx_t} {top} Q {cx} {top+30} {cx+rx_t} {top}'
    s.add(braid(rimd, 15))
    s.add(f'<ellipse cx="{cx-rx_t+2}" cy="{top}" rx="10" ry="9" fill="#c98a44" stroke="{OUT}" stroke-width="4"/><ellipse cx="{cx+rx_t-2}" cy="{top}" rx="10" ry="9" fill="#c98a44" stroke="{OUT}" stroke-width="4"/>')
    build('Obstacles', 'Basket', s)

# ---------------------------------------------------------------- Chick (fluffy character)
def fluffy(cx, cy, rx, ry, n, amp, phase=0):
    pts = []
    for k in range(n * 8):
        a = k / (n * 8) * 2 * math.pi
        bump = amp * abs(math.sin(n * a / 2 + phase)) * (.55 + .9 * abs(math.sin(2.3 * a + phase * 7))) - amp * .4
        pts.append((cx + (rx + bump) * math.cos(a), cy + (ry + bump) * math.sin(a)))
    return 'M' + ' L'.join(f'{x:.1f} {y:.1f}' for x, y in pts) + 'Z'

def chick():
    s = S()
    OUT = '#8a5200'
    s.add(el(128, 240, 70, 10, '#3a1c00', .4, 3))
    for x in (104, 152):
        leg = f'M{x} 214 V234 M{x} 234 L{x-13} 242 M{x} 234 L{x} 245 M{x} 234 L{x+13} 242'
        s.add(pa(leg, stroke='#9a4200', sw=11) + pa(leg, stroke='#ff9a1e', sw=5.5))
    yf = s.RG([(0, '#fffbb0'), (.45, '#ffe04a'), (.85, '#f7b322'), (1, '#e09410')], cx=.38, cy=.28, r=.85)
    # tuft on top
    for a, k in ((-26, .8), (0, 1), (24, .85)):
        s.shape('M128 50 C 118 38, 120 20, 129 10 C 138 22, 140 38, 128 50Z', '#ffe04a', OUT, tr=f'rotate({a} 128 50) translate(128 50) scale({k}) translate(-128 -50)', sw=3.5)
    # round-1 body silhouette with a few soft fluff notches (cheeks and chest)
    body = ('M128 38 C 166 38, 188 66, 184 98 L190 102 L186 108 C 210 124, 214 152, 206 174 L211 180 L203 184 C 194 210, 166 226, 128 226 '
            'C 90 226, 62 210, 53 184 L45 180 L50 174 C 42 152, 46 124, 70 108 L66 102 L72 98 C 68 66, 90 38, 128 38Z')
    sh = (el(128, 242, 90, 34, '#c97a00', .55, 8) + el(128, 108, 40, 8, '#e09410', .3, 5)
          + el(86, 126, 13, 9, '#ff8a8a', .7, 3) + el(170, 126, 13, 9, '#ff8a8a', .7, 3)
          + el(128, 180, 44, 32, '#fffbd0', .55, 8) + gloss(96, 62, 10, 15, 40, .85))
    s.shape(body, yf, OUT, sh)
    # small resting wings
    for m in ('', 'translate(256 0) scale(-1 1)'):
        s.shape('M58 144 C 40 154, 40 186, 60 196 C 74 190, 78 162, 58 144Z', s.LG([(0, '#ffd83a'), (1, '#eea018')]), OUT,
                pa('M52 164 C 54 174, 58 182, 63 188', stroke='#c98200', sw=2.5, op=.6), tr=m, sw=4)
    # big shiny eyes
    for x in (106, 150):
        s.add(f'<ellipse cx="{x}" cy="98" rx="13" ry="16" fill="#2a1206"/>' + el(x, 104, 9, 8, '#6a3a18', .9, 1)
              + el(x - 4, 91, 6, 7, '#fff') + el(x + 5, 106, 2.8, 2.8, '#fff', .95))
    of = s.LG([(0, '#ffb43a'), (1, '#f07a0a')])
    s.shape('M114 116 Q128 106 142 116 Q128 132 114 116Z', of, '#9a4200', pa('M116 117 Q128 121 140 117', stroke='#9a4200', sw=2.5, op=.7) + el(124, 113, 4, 1.6, '#fff', .7), sw=3)
    build('Obstacles', 'Chick', s)

# ---------------------------------------------------------------- Weed (scraggly tuft with dandelion)
def weed():
    s = S()
    OUT = '#17400a'
    def dandelion_leaf(k=1.0):
        # jagged, toothed leaf pointing up, base at (128, 220)
        L = 150 * k
        left, right = [], []
        for i in range(7):
            t = i / 6
            y = 220 - L * t
            w = 22 * k * math.sin(math.pi * min(1, t * 1.15)) + 3
            left += [(128 - w, y), (128 - w * .45, y - L / 14)]
            right += [(128 + w * .45, y - L / 14), (128 + w, y)]
        pts = [(128, 220)] + left + [(128, 220 - L - 6)] + list(reversed(right))
        return 'M' + ' L'.join(f'{x:.1f} {y:.1f}' for x, y in pts) + 'Z'
    blade = 'M128 222 C 118 180, 120 110, 128 40 C 138 110, 140 180, 128 222Z'
    c1 = s.LG([(0, '#b2dc3c'), (1, '#4a8a1a')])
    c2 = s.LG([(0, '#8ccf3a'), (1, '#2e7a14')])
    c3 = s.LG([(0, '#c8d84a'), (1, '#6a8a1c')])
    items = [('b', -62, .7, c3), ('b', 64, .72, c3), ('d', -38, .95, c2), ('d', 40, .9, c2), ('b', -18, 1.05, c1), ('b', 14, .92, c3), ('d', 0, 1.0, c1)]
    for kind, a, k, col in items:
        tr = f'rotate({a} 128 222) translate(128 222) scale({k}) translate(-128 -222)'
        if kind == 'b':
            s.shape(blade, col, OUT, pa('M128 216 L128 60', stroke='#1f5c10', sw=3, op=.6), tr=tr, sw=3.5)
        else:
            s.shape(dandelion_leaf(), col, OUT, pa('M128 218 L128 80', stroke='#d6ef7a', sw=4, op=.7), tr=tr, sw=3.5)
    # dandelion flower on a stalk
    s.add(pa('M150 220 C 160 180, 176 130, 186 74', stroke=OUT, sw=10) + pa('M150 220 C 160 180, 176 130, 186 74', stroke='#7ab832', sw=5))
    pet = ''.join(f'<path d="M186 72 L{186 + 30*math.cos(math.radians(a)):.1f} {72 + 30*math.sin(math.radians(a)):.1f}" stroke="#b06a00" stroke-width="11" stroke-linecap="round"/>' for a in range(0, 360, 24))
    pet += ''.join(f'<path d="M186 72 L{186 + 28*math.cos(math.radians(a)):.1f} {72 + 28*math.sin(math.radians(a)):.1f}" stroke="#ffd21e" stroke-width="6" stroke-linecap="round"/>' for a in range(0, 360, 24))
    pet += ''.join(f'<path d="M186 72 L{186 + 18*math.cos(math.radians(a)):.1f} {72 + 18*math.sin(math.radians(a)):.1f}" stroke="#ffb000" stroke-width="5" stroke-linecap="round"/>' for a in range(12, 372, 24))
    s.add(pet + '<circle cx="186" cy="72" r="9" fill="#ff9a00" stroke="#b06a00" stroke-width="2.5"/>' + el(180, 64, 5, 3, '#fff8c0', .9, 1, -30))
    # seed puff on the other side
    s.add(pa('M108 220 C 98 180, 80 140, 66 100', stroke=OUT, sw=9) + pa('M108 220 C 98 180, 80 140, 66 100', stroke='#7ab832', sw=4.5))
    puff = '<circle cx="64" cy="88" r="28" fill="#5a6a78"/><circle cx="64" cy="88" r="25" fill="#e8f0f6"/><circle cx="58" cy="82" r="14" fill="#fff" opacity=".8" filter="url(#b3)"/>'
    for k in range(18):
        a = math.radians(k * 20)
        x, y = 64 + 24 * math.cos(a), 88 + 24 * math.sin(a)
        puff += f'<path d="M64 88 L{x:.1f} {y:.1f}" stroke="#9aaab8" stroke-width="1.6"/><circle cx="{x:.1f}" cy="{y:.1f}" r="3.4" fill="#fff" stroke="#9aaab8" stroke-width="1.2"/>'
    s.add(puff + '<circle cx="64" cy="88" r="5" fill="#a87a3a"/>')
    # dirt clump
    df = s.RG([(0, '#9a6a3a'), (1, '#4e2c10')], cx=.4, cy=.3, r=.8)
    s.shape('M58 236 C 56 214, 92 204, 128 204 C 164 204, 200 214, 198 236 Q128 252 58 236Z', df, '#2e1606',
            ''.join(f'<circle cx="{x}" cy="{y}" r="{r}" fill="#d9a878" opacity=".8"/>' for x, y, r in ((84, 226, 3.5), (112, 218, 3), (150, 224, 4), (176, 230, 3), (132, 236, 3))))
    build('Obstacles', 'Weed', s)

if __name__ == '__main__':
    import sys
    allf = dict(Crate1=lambda: crate('Crate1', 1), Crate2=lambda: crate('Crate2', 2), Crate3=lambda: crate('Crate3', 3),
                Stone=stone, Ice=ice, Honey=honey, Chain=chain, Chick=chick, Basket=basket, Bed=bed, Weed=weed)
    for n in (sys.argv[1:] or allf): allf[n]()
