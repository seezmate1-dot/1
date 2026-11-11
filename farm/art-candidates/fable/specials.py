import colorsys
from lib import *

def hx(h, sat, v):
    r, g, b = colorsys.hsv_to_rgb(h % 1, sat, v)
    return '#%02x%02x%02x' % (int(r * 255), int(g * 255), int(b * 255))

def tube(s, d, w, col, out, hi=None, ow=4.5):
    s.add(pa(d, stroke=out, sw=w + ow * 2) + pa(d, stroke=col, sw=w))
    if hi: s.add(pa(d, stroke=hi, sw=w * .28, op=.7, tr=f'translate({-w*.18} {-w*.18})'))

# ---------------------------------------------------------------- Bee (upright, symmetric, one merged silhouette)
def circ(cx, cy, r):
    return f'M{cx-r} {cy} a{r} {r} 0 1 0 {2*r} 0 a{r} {r} 0 1 0 {-2*r} 0 Z'

def oval(cx, cy, rx, ry):
    return f'M{cx-rx} {cy} a{rx} {ry} 0 1 0 {2*rx} 0 a{rx} {ry} 0 1 0 {-2*rx} 0 Z'

BEE_VARIANTS = {
    # head (cy, r), body (cy, rx, ry), stripes y list, body tint, wing size
    'a': dict(hy=96, hr=60, by=184, brx=54, bry=50, stripes=(178, 210), body=None, wing=1.0),
    'b': dict(hy=100, hr=64, by=184, brx=60, bry=46, stripes=(172, 204), body='orange', wing=.95),
    'c': dict(hy=98, hr=58, by=180, brx=50, bry=52, stripes=(166, 194, 222), body=None, wing=1.05),
}

def bee_variant(v):
    p = BEE_VARIANTS[v]
    s = S()
    OUT, DARK = '#4a2608', '#3a1c08'
    hy, hr, by, brx, bry = p['hy'], p['hr'], p['by'], p['brx'], p['bry']
    # wings (symmetric, behind, rooted at the upper body)
    def wing(cx, cy, rx, ry, rot):
        i = uid('w')
        s.defs.append(f'<ellipse id="{i}" cx="{cx}" cy="{cy}" rx="{rx}" ry="{ry}" transform="rotate({rot} {cx} {cy})"/><clipPath id="{i}c"><use href="#{i}"/></clipPath>')
        wf = s.LG([(0, '#ffffff', .92), (1, '#9fd8ff', .6)])
        c, sn = math.cos(math.radians(rot)), math.sin(math.radians(rot))
        bx, by_ = cx - rx * c, cy - rx * sn
        vein = ''.join(pa(f'M{bx:.1f} {by_:.1f} Q {cx - o*sn:.1f} {cy + o*c:.1f} {cx + rx*.95*math.cos(math.radians(rot+a)):.1f} {cy + rx*.95*math.sin(math.radians(rot+a)):.1f}', stroke='#4f8cc4', sw=2.2, op=.75)
                       for o, a in ((-ry * .35, -16), (0, 0), (ry * .35, 16)))
        return (f'<use href="#{i}" fill="#2f6ca8" stroke="#2f6ca8" stroke-width="8"/><use href="#{i}" fill="{wf}"/>'
                f'<g clip-path="url(#{i}c)">{vein}' + el(cx - rx * .2, cy - ry * .35, rx * .5, ry * .2, '#fff', .9, 1, rot) + '</g>')
    k = p['wing']
    for m in ('', 'translate(256 0) scale(-1 1)'):
        s.add(f'<g transform="{m}">' + wing(128 - 76 * k, by - 40, 46 * k, 24 * k, -30) + wing(128 - 66 * k, by + 4, 34 * k, 18 * k, 18) + '</g>')
    # antennae (short)
    for m in ('', 'translate(256 0) scale(-1 1)'):
        s.add(f'<g transform="{m}">' + pa(f'M{128-22} {hy-hr+8} Q {128-30} {hy-hr-12} {128-40} {hy-hr-20}', stroke=OUT, sw=6)
              + f'<circle cx="{128-41}" cy="{hy-hr-22}" r="8.5" fill="{OUT}"/><circle cx="{128-44}" cy="{hy-hr-25}" r="2.8" fill="#c09060"/></g>')
    # stinger + tiny feet, behind the body outline
    s.shape(f'M118 {by+bry-6} L128 {by+bry+18} L138 {by+bry-6} Z', DARK, OUT, sw=3.5)
    for x in (100, 156):
        s.add(f'<ellipse cx="{x}" cy="{by+bry-4}" rx="11" ry="8" fill="{DARK}" stroke="{OUT}" stroke-width="7" paint-order="stroke"/>')
    # one merged silhouette: head + body as a single path (union via nonzero fill)
    union = circ(128, hy, hr) + ' ' + oval(128, by, brx, bry)
    yb = s.RG([(0, '#fff48a'), (.45, '#ffd028'), (.85, '#f2a100'), (1, '#d98200')], cx=.38, cy=.25, r=.9)
    bid = uid('bd'); s.defs.append(f'<clipPath id="{bid}"><path d="{oval(128, by, brx, bry)}"/></clipPath>')
    body_tint = el(128, by, brx, bry, '#ff9a00', .28) if p['body'] == 'orange' else ''
    stripes = ''.join(pa(f'M{128-brx-4} {y-11} Q128 {y+3} {128+brx+4} {y-11} L{128+brx+4} {y+7} Q128 {y+21} {128-brx-4} {y+7} Z', DARK) for y in p['stripes'])
    shade = (f'<g clip-path="url(#{bid})">{body_tint}{stripes}'
             + el(128, by + bry + 10, brx * 1.2, 26, '#7a3c00', .5, 8) + '</g>'
             # soft crease under the chin (no outline line)
             + el(128, hy + hr + 2, hr * .85, 10, '#b86a00', .55, 5)
             + el(128 + hr * .9, hy + 10, 18, hr * .8, '#d98200', .45, 8)
             + el(128 - hr * .55, hy + hr * .42, 12, 8, '#ff7d8a', .8, 3) + el(128 + hr * .55, hy + hr * .42, 12, 8, '#ff7d8a', .8, 3)
             + gloss(128 - hr * .5, hy - hr * .52, hr * .18, hr * .27, 40, .85)
             + el(128 - brx * .55, by - 4, 6, bry * .45, '#fff', .3, 4, 10))
    s.shape(f'<path {{ID}} d="{union}"/>', yb, OUT, shade, sw=5)
    # face
    for dx in (-21, 21):
        x = 128 + dx
        s.add(f'<ellipse cx="{x}" cy="{hy-2}" rx="{hr*.22:.1f}" ry="{hr*.28:.1f}" fill="#2a1206"/>' + el(x, hy + 4, hr * .14, hr * .15, '#6a3a18', .9, 1)
              + el(x - 4, hy - 10, hr * .1, hr * .12, '#fff') + el(x + 5, hy + 7, 2.8, 2.8, '#fff', .95))
    s.add(pa(f'M114 {hy+hr*.4} Q128 {hy+hr*.4+16} 142 {hy+hr*.4} Z', '#c8283a', stroke='#5a2a10', sw=4) + el(128, hy + hr * .4 + 8, 6, 3.4, '#ff8a9a', .9))
    return s

def bee():
    for v in BEE_VARIANTS:
        build('Specials', f'Bee_{v}', bee_variant(v))
    build('Specials', 'Bee', bee_variant(BEST_BEE))

BEST_BEE = 'a'

# ---------------------------------------------------------------- Bomb (powder keg)
def bomb():
    s = S()
    OUT = '#3a1c08'
    # fuse + spark behind
    tube(s, 'M128 70 C 132 46, 150 36, 172 36', 8, '#e8d2a0', OUT, ow=3.5)
    glow = s.RG([(0, '#fff7c0', 1), (.4, '#ffb21e', .9), (1, '#ff6a00', 0)], cx=.5, cy=.5, r=.5)
    s.add(f'<circle cx="178" cy="34" r="34" fill="{glow}"/>')
    s.shape(star(178, 34, 8, 27, 11, rot=-100), '#ffd21e', '#c24a00', pa(star(178, 34, 8, 15, 6, rot=-100), '#fff8d0'), sw=3)
    body = 'M68 82 C 46 116, 46 192, 68 226 Q128 246 188 226 C 210 192, 210 116, 188 82 Q128 64 68 82Z'
    wf = s.LG([(0, '#8f5422'), (.22, '#d99a55'), (.5, '#c07c3a'), (1, '#6e3c12')], x1=0, y1=0, x2=1, y2=0)
    stv = ''.join(pa(f'M{x} 66 C {x - (128-x)*.42} 120, {x - (128-x)*.42} 190, {x} 244', stroke='#5e3410', sw=3.5, op=.65) for x in (88, 108, 128, 148, 168))
    sh = (stv + el(128, 252, 90, 26, '#2e1404', .6, 8) + el(86, 154, 8, 60, '#ffe2b0', .45, 5))
    s.shape(body, wf, OUT, sh)
    mf = s.LG([(0, '#6a7582'), (.25, '#f2f6fa'), (.55, '#9aa6b4'), (1, '#4a5460')], x1=0, y1=0, x2=1, y2=0)
    for y in (98, 186):
        dx = 3 if y < 150 else 3
        hoop = f'M{53+ (4 if y<150 else 0)} {y} Q128 {y+18} {203-(4 if y<150 else 0)} {y} L{203-(0 if y<150 else 4)} {y+20} Q128 {y+38} {53+(0 if y<150 else 4)} {y+20}Z'
        riv = ''.join(f'<circle cx="{x}" cy="{y + 19 - abs(x-128)*.08}" r="3.6" fill="#3a444e"/><circle cx="{x-1}" cy="{y + 18 - abs(x-128)*.08}" r="1.4" fill="#e8eef4"/>' for x in (86, 128, 170))
        s.shape(hoop, mf, '#262d36', pa(f'M52 {y+3} Q128 {y+21} 204 {y+3}', stroke='#fff', sw=2.5, op=.6) + riv, sw=3.5)
    # red warning band
    band = 'M49 126 Q128 146 207 126 L207 176 Q128 196 49 176Z'
    rf = s.LG([(0, '#a81414'), (.25, '#ff6a4a'), (.55, '#e22a20'), (1, '#8a0c10')], x1=0, y1=0, x2=1, y2=0)
    s.shape(band, rf, '#5a0808', pa(star(128, 162, 8, 20, 9, rot=-90), '#ffd83a', stroke='#8a2a00', sw=2.5) + f'<circle cx="128" cy="162" r="6" fill="#fff6c0"/>', sw=3.5)
    # lid
    lf = s.LG([(0, '#b97a3c'), (1, '#7a4418')])
    s.shape('<ellipse {ID} cx="128" cy="80" rx="58" ry="15"/>', lf, OUT, el(128, 74, 40, 6, '#e8b87a', .5, 3) + f'<ellipse cx="128" cy="76" rx="9" ry="4" fill="#3a1c08"/>', sw=3.5)
    tube(s, 'M128 76 C 129 64, 132 56, 138 50', 8, '#e8d2a0', OUT, ow=0)
    build('Specials', 'Bomb', s)

# ---------------------------------------------------------------- Pitchfork
def pitchfork():
    s = S()
    wf = s.LG([(0, '#f0b468'), (.5, '#c47c34'), (1, '#8c4c18')], x1=0, y1=0, x2=1, y2=0)
    s.shape('<rect {ID} x="116" y="126" width="24" height="116" rx="10"/>', wf, '#45220a',
            el(123, 180, 3, 50, '#fff', .45, 2) + pa('M116 214 h24 M116 224 h24', stroke='#6e3a10', sw=3, op=.6))
    mf = s.LG([(0, '#f6f9fc'), (.5, '#b4bfcc'), (1, '#6c7886')], x1=0, y1=0, x2=1, y2=0, units=1)
    mf = s.LG([(0, '#7b8794'), (.18, '#ffffff'), (.5, '#b9c4d0'), (1, '#66727f')], x1=0, y1=0, x2=.34, y2=0)
    s.defs[-1] = s.defs[-1].replace('<linearGradient', '<linearGradient spreadMethod="repeat"')
    head = ('M119 40 L128 12 L137 40 L137 98 L176 98 L176 46 L185 20 L194 46 L194 104 Q194 120 178 120 L140 120 L140 138 '
            'L116 138 L116 120 L78 120 Q62 120 62 104 L62 46 L71 20 L80 46 L80 98 L119 98 Z')
    hl = ''.join(pa(f'M{x} 44 V96', stroke='#fff', sw=3.5, op=.85, blur=1) for x in (67.5, 124.5, 181.5))
    s.shape(head, mf, '#28303a', hl + el(128, 124, 70, 6, '#3c4652', .5, 3))
    rf = s.LG([(0, '#ff8a5a'), (.5, '#e2381e'), (1, '#9c1410')], x1=0, y1=0, x2=1, y2=0)
    s.shape('<rect {ID} x="111" y="132" width="34" height="26" rx="7"/>', rf, '#5a0a08', el(121, 145, 3, 9, '#fff', .7, 1), sw=3.5)
    build('Specials', 'Pitchfork', s)

# ---------------------------------------------------------------- Rainbow sunflower
def rainbow():
    s = S()
    N = 12
    pet = 'M128 128 C 104 100, 106 52, 128 18 C 150 52, 152 100, 128 128Z'
    for layer, (scale, off, v) in enumerate([(1.0, .5, .82), (.92, 0, 1.0)]):
        for i in range(N):
            h = (i + off) / N
            a = (i + off) * 360 / N
            f = s.LG([(0, hx(h, .45, 1.0 * v)), (.6, hx(h, .85, v)), (1, hx(h + .03, .95, .8 * v))])
            sh = pa('M128 124 L128 40', stroke=hx(h, .9, .6 * v), sw=3, op=.45) + (el(120, 62, 4, 16, '#fff', .55, 2, 8) if layer else '')
            s.shape(pet, f, hx(h, .9, .38), sh, sw=3.5,
                    tr=f'rotate({a:.1f} 128 128) translate({128*(1-scale):.1f} {128*(1-scale):.1f}) scale({scale})')
    cf = s.RG([(0, '#c98a3c'), (.6, '#8a5220'), (1, '#55300e')], cx=.4, cy=.35, r=.8)
    seeds = ''
    for k in range(1, 70):
        r = 4.2 * math.sqrt(k); a = k * 2.39996
        x, y = 128 + r * math.cos(a), 128 + r * math.sin(a)
        seeds += f'<circle cx="{x:.1f}" cy="{y:.1f}" r="2.6" fill="#3e2008" opacity=".7"/><circle cx="{x-.8:.1f}" cy="{y-.8:.1f}" r="1.1" fill="#e8b060" opacity=".8"/>'
    s.shape('<circle {ID} cx="128" cy="128" r="40"/>', cf, '#3a1c06',
            seeds + f'<circle cx="128" cy="128" r="37" fill="none" stroke="#ffb53a" stroke-width="5" opacity=".55"/>' + el(112, 108, 16, 9, '#fff', .35, 3, -30))
    build('Specials', 'Rainbow', s)

if __name__ == '__main__':
    for f in (bee, bomb, pitchfork, rainbow): f()
