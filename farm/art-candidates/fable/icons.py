"""HUD icons that had only code-painted placeholders (Resources/Icons/<name>.png). Same style as the board sprites."""
import sys
from lib import *

OUT = '#4a2608'
WOOD = [(0, '#f0b468'), (.5, '#c47c34'), (1, '#8c4c18')]
STEEL = [(0, '#6f7b88'), (.22, '#ffffff'), (.55, '#b4c0cc'), (1, '#5e6a77')]
ICONS = {}
def icon(fn):
    ICONS[fn.__name__.replace('_', '.')] = fn
    return fn

def rrect(x, y, w, h, r):
    return f'<rect {{ID}} x="{x}" y="{y}" width="{w}" height="{h}" rx="{r}"/>'

def glyph(s, d, w, fill=None, out='#2a1608', oop=.5):
    """A chunky white stroke glyph with a translucent dark outline: reads on any button colour."""
    f = fill or s.LG([(0, '#ffffff'), (1, '#dfe8f2')])
    s.add(pa(d, stroke=out, sw=w + 16, op=oop))
    s.add(pa(d, stroke='#000', sw=w + 2, op=.18, tr='translate(0 5)'))
    s.add(pa(d, stroke='#c9d6e4', sw=w) + pa(d, stroke='#ffffff', sw=w - 7, tr='translate(0 -2.5)'))

# ---------------------------------------------------------------- glyphs
@icon
def pause(s):
    glyph(s, 'M88 66 V190', 44); glyph(s, 'M168 66 V190', 44)

@icon
def close(s):
    glyph(s, 'M70 70 L186 186 M186 70 L70 186', 40)

@icon
def check(s):
    glyph(s, 'M52 134 L104 186 L204 72', 40)

@icon
def plus(s):
    f = s.RG([(0, '#b6f07a'), (.5, '#6cc04a'), (1, '#3f8c2a')], cx=.38, cy=.3, r=.85)
    s.shape('<circle {ID} cx="128" cy="128" r="104"/>', f, '#245c14', el(128, 250, 110, 44, '#1e5210', .5, 10) + gloss(82, 70, 14, 20, 40, .85), sw=6)
    glyph(s, 'M128 76 V180 M76 128 H180', 34, oop=.35)

def gear_path(cx, cy, ro, ri, n=8, hole=0):
    pts = []
    for k in range(n):
        a0 = 2 * math.pi * k / n
        for da, r in ((-.2, ri), (-.12, ro), (.12, ro), (.2, ri)):
            a = a0 + da * 2 * math.pi / n * 2.0
            pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    d = 'M' + ' L'.join(f'{x:.1f} {y:.1f}' for x, y in pts) + 'Z'
    if hole:
        d += f' M{cx-hole} {cy} a{hole} {hole} 0 1 0 {2*hole} 0 a{hole} {hole} 0 1 0 {-2*hole} 0 Z'
    return d

@icon
def settings(s):
    f = s.LG([(0, '#ffffff'), (.6, '#dfe8f2'), (1, '#b4c4d6')])
    d = gear_path(128, 128, 108, 80, 8, 34)
    s.add(pa(d, 'none', stroke='#2a1608', sw=16, op=.5))
    s.shape(f'<path {{ID}} fill-rule="evenodd" clip-rule="evenodd" d="{d}"/>', f, None,
            el(128, 240, 120, 50, '#7f93aa', .6, 10) + pa('M60 96 A 76 76 0 0 1 150 54', stroke='#fff', sw=10, op=.9, blur=2))
    s.add('<circle cx="128" cy="128" r="34" fill="none" stroke="#2a1608" stroke-opacity=".5" stroke-width="8"/>')

# ---------------------------------------------------------------- objects
@icon
def sun(s):
    rays = star(128, 128, 12, 124, 84, rot=-90)
    rf = s.RG([(0, '#ffd23a'), (1, '#ff9a1e')], cx=.5, cy=.5, r=.5)
    s.shape(rays, rf, '#b85a00', '', sw=6)
    f = s.RG([(0, '#fffbd0'), (.45, '#ffe04a'), (.9, '#ffb21e'), (1, '#f59600')], cx=.38, cy=.3, r=.85)
    s.shape('<circle {ID} cx="128" cy="128" r="76"/>', f, '#b85a00', el(128, 214, 80, 34, '#e07a00', .5, 10) + gloss(96, 90, 13, 19, 40, .95), sw=5)

@icon
def lock(s):
    sh = s.LG(STEEL, x1=0, y1=0, x2=1, y2=0)
    d = 'M76 132 V92 A52 52 0 0 1 180 92 V132'
    s.add(pa(d, stroke='#28303a', sw=34) + pa(d, stroke=sh, sw=22) + pa('M84 92 A44 44 0 0 1 112 52', stroke='#fff', sw=5, op=.8, blur=1))
    f = s.RG([(0, '#fff3a0'), (.5, '#ffc928'), (1, '#e08a00')], cx=.36, cy=.25, r=.95)
    s.shape(rrect(44, 116, 168, 124, 30), f, '#7a3e00',
            el(128, 252, 100, 34, '#a85a00', .6, 10) + pa('M62 136 H190', stroke='#fff', sw=7, op=.6, blur=2) + gloss(74, 150, 9, 13, 40, .8), sw=6)
    s.add('<circle cx="128" cy="166" r="17" fill="#5e3410"/>' + pa('M128 168 V202', stroke='#5e3410', sw=15))

@icon
def clock(s):
    rim = s.LG([(0, '#ff8a6a'), (.5, '#ee4a34'), (1, '#b02818')])
    s.shape('<circle {ID} cx="128" cy="128" r="106"/>', rim, '#6e1408', pa('M44 96 A 92 92 0 0 1 152 38', stroke='#fff', sw=8, op=.7, blur=2), sw=6)
    f = s.RG([(0, '#ffffff'), (.7, '#f4f1e6'), (1, '#d8d0bc')], cx=.42, cy=.36, r=.8)
    s.add(f'<circle cx="128" cy="128" r="82" fill="#8a2a14"/><circle cx="128" cy="130" r="78" fill="{f}"/>')
    s.add(''.join(f'<circle cx="{128+62*math.cos(math.radians(a)):.1f}" cy="{130+62*math.sin(math.radians(a)):.1f}" r="{6 if a%90==0 else 3.5}" fill="#8a6a44"/>' for a in range(0, 360, 30)))
    s.add(pa('M128 130 V78', stroke=OUT, sw=13) + pa('M128 130 L166 150', stroke=OUT, sw=13) + '<circle cx="128" cy="130" r="11" fill="#ee4a34" stroke="#4a2608" stroke-width="4"/>')
    s.add(el(96, 92, 14, 9, '#fff', .9, 2, -35))

@icon
def tasks(s):
    wf = s.LG(WOOD, x1=0, y1=0, x2=1, y2=1)
    s.shape(rrect(42, 34, 172, 206, 22), wf, OUT, el(128, 250, 100, 30, '#5e3410', .5, 8) + pa('M56 46 H200', stroke='#ffe2b0', sw=5, op=.6, blur=1.2), sw=6)
    pf = s.LG([(0, '#ffffff'), (1, '#efe6d2')])
    s.shape(rrect(60, 58, 136, 166, 10), pf, '#8a6a44', '', sw=3)
    for i, y in enumerate((98, 140, 182)):
        s.add(pa(f'M78 {y} l11 11 l19 -22', stroke='#2f8f24', sw=11) if i < 2 else f'<rect x="76" y="{y-13}" width="28" height="26" rx="7" fill="none" stroke="#b9a88a" stroke-width="5"/>')
        s.add(pa(f'M122 {y} H178', stroke='#b9a88a', sw=10))
    mf = s.LG(STEEL, x1=0, y1=0, x2=0, y2=1)
    s.shape('M92 20 H164 Q174 20 174 30 V56 Q174 64 164 64 H92 Q82 64 82 56 V30 Q82 20 92 20Z', mf, '#28303a', pa('M92 30 H164', stroke='#fff', sw=4, op=.8, blur=1), sw=5)
    s.add('<circle cx="128" cy="38" r="8" fill="#28303a"/>')

@icon
def events(s):
    wf = s.LG(WOOD, x1=0, y1=0, x2=1, y2=0)
    s.shape(rrect(54, 22, 22, 222, 10), wf, OUT, el(60, 120, 3, 90, '#fff', .45, 2), sw=5)
    f = s.LG([(0, '#ff8a5a'), (.5, '#f0432e'), (1, '#b81e16')], x1=0, y1=0, x2=1, y2=1)
    d = 'M74 40 C 110 26, 138 62, 178 48 C 196 42, 214 36, 226 40 L196 98 L226 156 C 206 150, 190 160, 170 164 C 134 172, 110 140, 74 152 Z'
    sh = (pa('M74 132 C 110 120, 134 152, 170 144 C 190 140, 206 132, 226 138 L226 170 L74 170Z', '#8a0c10', op=.35, blur=3)
          + pa('M82 52 C 112 42, 138 72, 176 60', stroke='#fff', sw=7, op=.55, blur=2))
    s.shape(d, f, '#6e0a0c', sh, sw=6)
    s.add(pa(star(134, 100, 5, 30, 13), '#ffd83a', stroke='#8a4200', sw=5) + el(128, 92, 6, 4, '#fff', .8, 1))
    s.add(f'<circle cx="65" cy="22" r="15" fill="#ffc928" stroke="#7a3e00" stroke-width="5"/>' + el(61, 18, 4, 3, '#fff', .9))

def tomato(s, cx, cy, r):
    f = s.RG([(0, '#ff9a7a'), (.5, '#f0432e'), (1, '#b81e16')], cx=.38, cy=.3, r=.85)
    s.shape(f'<circle {{ID}} cx="{cx}" cy="{cy}" r="{r}"/>', f, '#6e0a0c', el(cx - r * .35, cy - r * .4, r * .22, r * .16, '#fff', .85, 1, -30), sw=4.5)
    s.add(pa(star(cx, cy - r * .8, 5, r * .5, r * .18), '#4fb02e', stroke='#1e5c12', sw=3.5))

@icon
def orders(s):
    # a crate of produce
    for x, rot in ((92, -14), (128, 0), (164, 14)):   # carrot tops / corn behind
        s.add(pa(f'M{x} 110 C {x-18} 80, {x-10} 50, {x} 30 C {x+10} 50, {x+18} 80, {x} 110Z', '#5fc23a', stroke='#1e5c12', sw=5, tr=f'rotate({rot} {x} 110)'))
    cf = s.LG([(0, '#fff0a0'), (.5, '#ffcf30'), (1, '#e89a00')], x1=0, y1=0, x2=1, y2=0)
    s.shape('M150 118 C 146 80, 160 52, 176 44 C 196 56, 206 86, 200 118Z', cf, '#7a3e00', pa('M160 70 L196 70 M156 90 L202 90 M168 52 V118 M186 52 V118', stroke='#c87a00', sw=3, op=.7), sw=5)
    tomato(s, 96, 100, 34); tomato(s, 138, 108, 28)
    wf = s.LG([(0, '#f2bc74'), (1, '#b9783a')])
    s.shape('M30 112 H226 L210 230 Q208 240 198 240 H58 Q48 240 46 230 Z', wf, OUT,
            pa('M38 154 H220 M44 196 H212', stroke='#7a4418', sw=5, op=.8) + pa('M36 122 H220', stroke='#ffe2b0', sw=6, op=.7, blur=1.2)
            + pa('M36 162 H220 M42 204 H212', stroke='#ffe2b0', sw=3, op=.5) + el(128, 250, 100, 22, '#5e3410', .5, 8), sw=6)
    for x in (58, 198):
        s.add(f'<circle cx="{x}" cy="134" r="5" fill="#5e3410"/><circle cx="{x + (-4 if x > 128 else 4)}" cy="218" r="5" fill="#5e3410"/>')

@icon
def shop(s):
    wf = s.LG([(0, '#f2bc74'), (1, '#b9783a')])
    s.shape(rrect(46, 112, 164, 130, 12), wf, OUT, pa('M46 190 H210', stroke='#7a4418', sw=5, op=.8) + el(128, 250, 100, 22, '#5e3410', .5, 8), sw=6)
    win = s.LG([(0, '#fff6c8'), (1, '#ffd98a')])
    s.shape(rrect(64, 130, 128, 58, 8), win, '#7a4418', '', sw=4)
    tomato(s, 96, 170, 20); 
    s.add(f'<circle cx="134" cy="172" r="18" fill="#7ad03a" stroke="#1e5c12" stroke-width="4.5"/>' + el(128, 165, 5, 4, '#fff', .8, 1)
          + f'<circle cx="166" cy="172" r="17" fill="#ffa02a" stroke="#8a4200" stroke-width="4.5"/>' + el(160, 166, 5, 4, '#fff', .8, 1))
    # striped awning with scallops
    n = 6; w = 204 / n
    i0 = uid('aw')
    d = 'M40 28 H216 L230 96 '
    for k in range(n):
        x1 = 230 - k * (204 + 0) / n * 1.0 - 0
        d += f'a{w/2+0:.1f} {22} 0 0 1 {-w:.1f} 0 '
    d = 'M40 28 H216 L230 96 ' + ''.join(f'a{w/2:.2f} 22 0 0 1 {-w:.2f} 0 ' for _ in range(n)) + 'Z'
    stripes = ''.join(pa(f'M{40 + 176*k/n:.1f} 20 H{40 + 176*(k+1)/n:.1f} L{26 + 204*(k+1)/n:.1f} 130 H{26 + 204*k/n:.1f}Z', '#f0432e' if k % 2 == 0 else '#fff6ea') for k in range(n))
    s.shape(d, '#fff6ea', '#6e0a0c', stripes + pa('M40 34 H216', stroke='#fff', sw=6, op=.5, blur=1.5) + pa('M28 92 H228 V130 H28Z', '#000', op=.13, blur=3), sw=6)

@icon
def fields(s):
    sf = s.LG([(0, '#c98a50'), (1, '#8a5226')])
    s.shape('M22 196 C 22 150, 70 132, 128 132 C 186 132, 234 150, 234 196 C 234 226, 190 240, 128 240 C 66 240, 22 226, 22 196Z', sf, '#3e2008',
            pa('M48 176 C 90 158, 166 158, 208 176 M40 206 C 90 188, 166 188, 216 206', stroke='#5e3410', sw=6, op=.6) + pa('M60 156 C 100 144, 156 144, 196 156', stroke='#f0c08a', sw=5, op=.5, blur=1.5)
            + el(128, 250, 110, 22, '#3e2008', .5, 8), sw=6)
    for x, h, k in ((66, 70, .85), (128, 104, 1.1), (190, 70, .85)):
        y = 164 if x != 128 else 158
        lf = s.LG([(0, '#b6f07a'), (1, '#4fb02e')])
        s.add(pa(f'M{x} {y} V{y-h}', stroke='#1e5c12', sw=13) + pa(f'M{x} {y} V{y-h}', stroke='#5fc23a', sw=6))
        for sgn in (-1, 1):
            w = 46 * k
            d = f'M{x} {y-h+6} C {x+sgn*w*.3} {y-h-w*.75}, {x+sgn*w} {y-h-w*.7}, {x+sgn*w*1.1} {y-h-w*.2} C {x+sgn*w*.8} {y-h+w*.25}, {x+sgn*w*.3} {y-h+w*.2}, {x} {y-h+6}Z'
            s.shape(d, lf, '#1e5c12', pa(f'M{x} {y-h+4} L{x+sgn*w*.9} {y-h-w*.3}', stroke='#e8ffc0', sw=3, op=.6), sw=4.5)

@icon
def soil(s):
    sf = s.LG([(0, '#c98a50'), (1, '#8a5226')])
    wf = s.LG([(0, '#f2bc74'), (1, '#b9783a')])
    s.shape('M34 150 C 34 96, 80 84, 128 84 C 176 84, 222 96, 222 150 Z', sf, '#3e2008',
            pa('M62 126 C 100 108, 156 108, 194 126', stroke='#5e3410', sw=6, op=.6) + pa('M70 104 C 104 92, 152 92, 186 104', stroke='#f0c08a', sw=5, op=.5, blur=1.5)
            + ''.join(el(x, y, 7, 4, '#5e3410', .7) for x, y in ((92, 138), (150, 132), (176, 142))), sw=6)
    s.shape(rrect(18, 142, 220, 76, 14), wf, OUT, pa('M22 180 H234', stroke='#7a4418', sw=5, op=.8) + pa('M28 152 H228', stroke='#ffe2b0', sw=5, op=.7, blur=1.2)
            + ''.join(f'<circle cx="{x}" cy="{y}" r="4.5" fill="#5e3410"/>' for x in (40, 216) for y in (162, 200)) + el(128, 226, 110, 14, '#5e3410', .45, 6), sw=6)
    lf = s.LG([(0, '#b6f07a'), (1, '#4fb02e')])
    s.add(pa('M128 100 V60', stroke='#1e5c12', sw=12) + pa('M128 100 V60', stroke='#5fc23a', sw=5))
    for sgn in (-1, 1):
        s.shape(f'M128 64 C {128+sgn*10} 34, {128+sgn*42} 30, {128+sgn*48} 46 C {128+sgn*40} 66, {128+sgn*14} 70, 128 64Z', lf, '#1e5c12', '', sw=4.5)

@icon
def animals(s):
    # a white hen, front view
    OUTH = '#6a4a2a'
    s.add(pa('M108 58 C 96 30, 116 18, 126 36 C 130 14, 154 18, 148 42 C 166 34, 172 56, 152 66Z', '#f0432e', stroke='#6e0a0c', sw=6))
    f = s.RG([(0, '#ffffff'), (.6, '#f6f0e2'), (1, '#d8c8a8')], cx=.4, cy=.3, r=.85)
    body = 'M128 50 C 176 50, 196 92, 190 124 C 226 134, 236 176, 216 206 C 196 236, 60 236, 40 206 C 20 176, 30 134, 66 124 C 60 92, 80 50, 128 50Z'
    sh = (el(128, 248, 110, 36, '#b89a6a', .55, 10) + pa('M52 168 C 60 196, 84 208, 104 204 M204 168 C 196 196, 172 208, 152 204', stroke='#c8b48c', sw=6, op=.8)
          + gloss(96, 84, 10, 15, 40, .9))
    s.shape(body, f, OUTH, sh, sw=6)
    for x in (104, 152):
        s.add(f'<ellipse cx="{x}" cy="108" rx="11" ry="14" fill="#2a1206"/>' + el(x - 3, 102, 4, 5, '#fff') )
    s.add(el(84, 132, 12, 8, '#ff9aa0', .8, 2) + el(172, 132, 12, 8, '#ff9aa0', .8, 2))
    s.add(pa('M110 126 H146 L128 152Z', '#ffb21e', stroke='#8a4200', sw=5) + pa('M114 128 H142', stroke='#ffe08a', sw=4, op=.8))
    s.add(pa('M120 152 C 112 170, 122 182, 128 170 C 134 182, 144 170, 136 152Z', '#f0432e', stroke='#6e0a0c', sw=4.5))
    for x in (100, 156):
        s.add(pa(f'M{x} 232 v14 M{x-12} 250 h24', stroke='#8a4200', sw=13) + pa(f'M{x} 232 v14 M{x-12} 250 h24', stroke='#ffb21e', sw=6))

def house(s, x, y, w, h, roof, roofout, wall=('#fff6e0', '#e8cfa0'), door=True, rh=None):
    rh = rh or w * .55
    wf = s.LG([(0, wall[0]), (1, wall[1])])
    win = (f'<rect x="{x+w*.56}" y="{y+h*.22}" width="{w*.26}" height="{w*.26}" rx="4" fill="#8ad4ff" stroke="#2a5a8a" stroke-width="4"/>') if door else ''
    dr = pa(f'M{x+w*.16} {y+h} V{y+h*.42} Q{x+w*.16} {y+h*.3} {x+w*.3} {y+h*.3} Q{x+w*.44} {y+h*.3} {x+w*.44} {y+h*.42} V{y+h}Z', '#b9783a', stroke=OUT, sw=4) if door else ''
    s.shape(rrect(x, y, w, h, 6), wf, OUT, pa(f'M{x} {y} H{x+w} V{y+16} H{x}Z', '#8a5a2c', op=.35, blur=3) + win + dr, sw=5.5)
    rf = s.LG([(0, roof[0]), (1, roof[1])])
    d = f'M{x-14} {y+6} L{x+w/2} {y-rh} L{x+w+14} {y+6} Z'
    s.shape(d, rf, roofout, pa(f'M{x} {y-4} L{x+w/2} {y-rh+12}', stroke='#fff', sw=5, op=.5, blur=1.5), sw=5.5)

@icon
def town(s):
    house(s, 118, 96, 104, 140, ('#6ab8ff', '#2a6ec0'), '#143a6e')
    house(s, 30, 140, 104, 96, ('#ff8a5a', '#d8381e'), '#6e0a0c')
    s.add(el(128, 240, 110, 8, '#3e2008', .25, 3))

@icon
def barn(s):
    wf = s.LG([(0, '#ff6a4a'), (1, '#c02818')])
    body = 'M40 240 V130 L70 70 L128 40 L186 70 L216 130 V240 Z'
    planks = ''.join(pa(f'M{x} 60 V240', stroke='#8a1410', sw=3, op=.5) for x in range(64, 216, 24))
    s.shape(body, wf, '#5a0808', planks + pa('M48 132 L74 78 L128 50', stroke='#ffb0a0', sw=6, op=.6, blur=1.5) + el(128, 250, 100, 20, '#5a0808', .5, 8), sw=6)
    rf = 'M26 136 L62 62 L128 28 L194 62 L230 136'
    s.add(pa(rf, stroke='#3e2008', sw=30) + pa(rf, stroke='#fff6ea', sw=17) + pa(rf, stroke='#d8c8b0', sw=5, tr='translate(0 5)', op=.8))
    s.shape(rrect(88, 150, 80, 90, 6), '#fff6ea', '#5a0808', pa('M92 154 L164 236 M164 154 L92 236', stroke='#c02818', sw=9) + pa('M128 150 V240', stroke='#c02818', sw=6), sw=5)
    s.add('<circle cx="128" cy="98" r="21" fill="#4a1a10" stroke="#fff6ea" stroke-width="8"/>')

@icon
def workshop(s):
    # a steel gear with a hammer across it
    gf = s.LG([(0, '#eef3f8'), (.5, '#b4c0cc'), (1, '#74808e')])
    d = gear_path(140, 148, 96, 70, 8, 30)
    s.shape(f'<path {{ID}} fill-rule="evenodd" clip-rule="evenodd" d="{d}"/>', gf, '#28303a',
            pa('M84 120 A 66 66 0 0 1 160 86', stroke='#fff', sw=9, op=.85, blur=2) + el(140, 250, 110, 44, '#4a5460', .5, 10), sw=6)
    s.add('<circle cx="140" cy="148" r="30" fill="none" stroke="#28303a" stroke-width="6"/>')
    R = 'rotate(-42 128 128)'
    wf = s.LG(WOOD, x1=0, y1=0, x2=1, y2=0)
    s.shape(rrect(116, 70, 24, 176, 10), wf, OUT, el(122, 150, 3, 70, '#fff', .45, 2), tr=R, sw=5)
    hf = s.LG([(0, '#ff8a5a'), (.5, '#e2381e'), (1, '#9c1410')])
    s.shape('M70 26 H176 Q190 26 190 40 V78 Q190 92 176 92 H70 Q56 92 56 78 V40 Q56 26 70 26Z', hf, '#5a0a08',
            pa('M68 38 H178', stroke='#fff', sw=6, op=.6, blur=1.5) + pa('M96 26 V92 M160 26 V92', stroke='#5a0a08', sw=4, op=.5), tr=R, sw=5.5)

@icon
def truck(s):
    s.add(el(128, 226, 110, 10, '#000', .25, 4))
    # crates in the back
    wf = s.LG([(0, '#f2bc74'), (1, '#b9783a')])
    s.shape(rrect(26, 84, 62, 62, 6), wf, OUT, pa('M30 88 L84 142 M84 88 L30 142', stroke='#7a4418', sw=5), sw=5)
    tomato(s, 118, 122, 24)
    bf = s.LG([(0, '#ffb04a'), (1, '#f07a1e')])
    s.shape('M14 138 H150 V200 H14 Z', bf, '#7a3000', pa('M14 148 H150', stroke='#ffe2b0', sw=5, op=.7, blur=1.2) + pa('M60 138 V200 M104 138 V200', stroke='#b85a10', sw=4, op=.7), sw=6)
    cf = s.LG([(0, '#ff6a4a'), (1, '#c02818')])
    s.shape('M150 90 H196 Q208 90 214 100 L238 144 Q242 150 242 158 V200 H150 Z', cf, '#5a0808',
            pa('M158 98 H198', stroke='#ffb0a0', sw=5, op=.7, blur=1.2) + '<path d="M166 104 H194 Q200 104 204 110 L220 142 H166Z" fill="#aee4ff" stroke="#5a0808" stroke-width="5" stroke-linejoin="round"/>'
            + pa('M174 112 L186 136', stroke='#fff', sw=6, op=.8), sw=6)
    s.add(f'<rect x="8" y="194" width="240" height="16" rx="8" fill="#5e6a77" stroke="#28303a" stroke-width="5"/>')
    for x in (62, 194):
        tf = s.RG([(0, '#6a6460'), (1, '#2a2622')], cx=.4, cy=.35, r=.8)
        s.add(f'<circle cx="{x}" cy="208" r="30" fill="{tf}" stroke="#1a1612" stroke-width="6"/><circle cx="{x}" cy="208" r="12" fill="#dfe8f2" stroke="#5e6a77" stroke-width="4"/>')

@icon
def xp(s):
    f = s.RG([(0, '#9ad8ff'), (.5, '#47a8ee'), (1, '#1e62b0')], cx=.38, cy=.28, r=.9)
    hexa = 'M128 18 L214 60 Q226 66 226 80 V176 Q226 190 214 196 L128 238 L42 196 Q30 190 30 176 V80 Q30 66 42 60 Z'
    s.shape(hexa, f, '#103a6e', pa('M128 34 L206 72 Q212 76 212 84 V172', stroke='#fff', sw=0) + el(128, 250, 110, 50, '#0e3a78', .55, 12)
            + pa('M46 84 Q46 74 56 68 L128 34', stroke='#fff', sw=8, op=.6, blur=2), sw=6.5)
    sf = s.LG([(0, '#fff7a8'), (1, '#ffc21e')])
    s.add(pa(star(128, 132, 5, 66, 30), '#103a6e', stroke='#103a6e', sw=14, op=.6, tr='translate(0 4)'))
    s.add(pa(star(128, 130, 5, 66, 30), sf, stroke='#8a4200', sw=6) + el(112, 110, 9, 6, '#fff', .9, 1, -30))

def person(s, cx, cy, k, shirt, shout, skin=('#ffe0c0', '#f2b88a'), hair='#6e3a10'):
    bf = s.LG([(0, shirt[0]), (1, shirt[1])])
    w = 74 * k
    s.shape(f'M{cx-w} {cy+110*k} C {cx-w} {cy+40*k}, {cx-w*.5} {cy+22*k}, {cx} {cy+22*k} C {cx+w*.5} {cy+22*k}, {cx+w} {cy+40*k}, {cx+w} {cy+110*k} Z', bf, shout,
            pa(f'M{cx-w*.7} {cy+60*k} C {cx-w*.5} {cy+38*k}, {cx-w*.2} {cy+32*k}, {cx} {cy+32*k}', stroke='#fff', sw=6 * k, op=.45, blur=2), sw=6)
    hf = s.RG([(0, skin[0]), (1, skin[1])], cx=.4, cy=.35, r=.8)
    r = 46 * k
    s.shape(f'<circle {{ID}} cx="{cx}" cy="{cy-10*k}" r="{r}"/>', hf, '#7a4418',
            pa(f'M{cx-r-4} {cy-14*k} C {cx-r} {cy-r*1.5}, {cx+r} {cy-r*1.5}, {cx+r+4} {cy-14*k} C {cx+r*.4} {cy-r*.75}, {cx-r*.2} {cy-r*.9}, {cx-r-4} {cy-14*k}Z', hair)
            + el(cx - r * .38, cy - 2 * k, 5 * k, 6.5 * k, '#2a1206') + el(cx + r * .38, cy - 2 * k, 5 * k, 6.5 * k, '#2a1206')
            + pa(f'M{cx-9*k} {cy+14*k} Q{cx} {cy+22*k} {cx+9*k} {cy+14*k}', stroke='#a04a2a', sw=4 * k), sw=5.5)

@icon
def people(s):
    person(s, 176, 104, .8, ('#6ab8ff', '#2a6ec0'), '#143a6e', hair='#3a2a20')
    person(s, 100, 120, 1.0, ('#8ad85a', '#3f8c2a'), '#1e5c12', hair='#b85a10')

@icon
def lot(s):
    gf = s.LG([(0, '#a6e06a'), (1, '#5fb03a')])
    s.shape('M128 132 L240 182 L128 236 L16 182 Z', '#8a5226', '#3e2008', '', sw=6, tr='translate(0 12)')
    s.shape('M128 124 L240 176 L128 230 L16 176 Z', gf, '#1e5c12',
            pa('M128 132 L224 176', stroke='#e8ffc0', sw=5, op=.5, blur=1.5) + pa('M72 150 L184 204 M184 150 L72 204', stroke='#3f8c2a', sw=4, op=.5), sw=6)
    wf = s.LG(WOOD, x1=0, y1=0, x2=1, y2=0)
    s.shape(rrect(120, 84, 16, 106, 6), wf, OUT, '', sw=5)
    sf = s.LG([(0, '#fff6e0'), (1, '#e8cfa0')])
    s.shape(rrect(64, 22, 128, 82, 12), sf, OUT, pa('M72 32 H184', stroke='#fff', sw=5, op=.8, blur=1), sw=6)
    s.add(pa('M128 40 V86 M105 63 H151', stroke='#1e5c12', sw=22) + pa('M128 40 V86 M105 63 H151', stroke='#5fc23a', sw=12))

@icon
def beauty(s):
    s.add(pa('M128 150 C 124 190, 132 214, 128 244', stroke='#1e5c12', sw=16) + pa('M128 150 C 124 190, 132 214, 128 244', stroke='#5fc23a', sw=7))
    lf = s.LG([(0, '#b6f07a'), (1, '#4fb02e')])
    s.shape('M128 214 C 150 180, 196 176, 212 192 C 200 222, 160 232, 128 214Z', lf, '#1e5c12', pa('M134 212 L200 194', stroke='#e8ffc0', sw=3, op=.6), sw=5)
    s.shape('M128 226 C 106 196, 66 194, 50 208 C 62 236, 100 244, 128 226Z', lf, '#1e5c12', pa('M122 224 L62 210', stroke='#e8ffc0', sw=3, op=.6), sw=5)
    pf = s.RG([(0, '#ffd0e0'), (.5, '#ff7fa8'), (1, '#e03a78')], cx=.5, cy=.9, r=.9)
    pet = 'M128 104 C 98 84, 96 36, 128 22 C 160 36, 158 84, 128 104Z'
    for i in range(6):
        s.shape(pet, pf, '#8a1244', el(122, 46, 8, 14, '#fff', .5, 3), tr=f'rotate({i*60} 128 104)', sw=5)
    cf = s.RG([(0, '#fff3a0'), (.6, '#ffc928'), (1, '#e08a00')], cx=.4, cy=.35, r=.8)
    s.shape('<circle {ID} cx="128" cy="104" r="28"/>', cf, '#7a3e00', el(119, 95, 7, 5, '#fff', .9, 1, -30), sw=5)

def sheet(names, path, size=128, cols=8, bgs=((233, 205, 160), (71, 168, 238), (108, 192, 74))):
    rows = (len(names) + cols - 1) // cols
    im = Image.new('RGBA', (cols * size, rows * size * len(bgs)), (0, 0, 0, 255))
    for b, bg in enumerate(bgs):
        im.paste(bg + (255,), (0, b * rows * size, cols * size, (b + 1) * rows * size))
        for i, n in enumerate(names):
            ic = Image.open(os.path.join(HERE, n + '.png')).convert('RGBA').resize((size - 12, size - 12), Image.LANCZOS)
            im.alpha_composite(ic, (i % cols * size + 6, (b * rows + i // cols) * size + 6))
    im.save(path)

if __name__ == '__main__':
    only = sys.argv[1:]
    for name, fn in ICONS.items():
        if only and name not in only: continue
        s = S(); fn(s); build('Icons', name, s, pad=10)
