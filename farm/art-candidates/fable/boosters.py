from lib import *
from specials import hx, tube

WOOD = [(0, '#f0b468'), (.5, '#c47c34'), (1, '#8c4c18')]
STEEL = [(0, '#6f7b88'), (.22, '#ffffff'), (.55, '#b4c0cc'), (1, '#5e6a77')]

# ---------------------------------------------------------------- Rake
def rake():
    s = S()
    R = 'rotate(40 128 128)'
    wf = s.LG(WOOD, x1=0, y1=0, x2=1, y2=0)
    s.shape('<rect {ID} x="117" y="4" width="22" height="182" rx="10"/>', wf, '#45220a', el(123, 90, 3, 78, '#fff', .45, 2), tr=R)
    mf = s.LG([(0, '#ff8a5a'), (.5, '#e2381e'), (1, '#9c1410')], x1=0, y1=0, x2=0, y2=1)
    d = 'M62 178 H194 Q204 178 204 188 V198'
    xs = [190 - i * 24.8 for i in range(6)]
    for x in xs:
        d += f' L{x+7} 198 L{x+7} 232 L{x} 250 L{x-7} 232 L{x-7} 198'
    d += ' L52 198 V188 Q52 178 62 178 Z'
    sf = s.LG(STEEL, x1=0, y1=0, x2=0, y2=1)
    hl = ''.join(pa(f'M{x-3} 202 V232', stroke='#fff', sw=3, op=.8, blur=1) for x in xs) + pa('M58 184 H198', stroke='#fff', sw=3, op=.7, blur=1) + el(128, 200, 80, 5, '#3c4652', .45, 3)
    s.shape(d, sf, '#28303a', hl, tr=R)
    s.shape('<rect {ID} x="110" y="166" width="36" height="28" rx="7"/>', mf, '#5a0a08', el(128, 172, 12, 3, '#fff', .6, 1), tr=R, sw=3.5)
    build('Boosters', 'Rake', s)

# ---------------------------------------------------------------- Shovel
def shovel():
    s = S()
    R = 'rotate(38 128 128)'
    wf = s.LG(WOOD, x1=0, y1=0, x2=1, y2=0)
    s.shape('<rect {ID} x="117" y="44" width="22" height="120" rx="8"/>', wf, '#45220a', el(123, 100, 3, 50, '#fff', .45, 2), tr=R)
    rf = s.LG([(0, '#ff8a5a'), (.5, '#e2381e'), (1, '#9c1410')], x1=0, y1=0, x2=1, y2=1)
    grip = '<path {ID} fill-rule="evenodd" clip-rule="evenodd" d="M98 4 H158 Q170 4 166 18 L148 56 Q146 62 140 62 H116 Q110 62 108 56 L90 18 Q86 4 98 4 Z M108 20 H148 L136 44 H120 Z"/>'
    s.shape(grip, rf, '#5a0a08', pa('M98 12 H156', stroke='#fff', sw=4, op=.6, blur=1), tr=R, sw=4)
    sf = s.LG(STEEL, x1=0, y1=0, x2=1, y2=0)
    blade = 'M84 150 H172 Q182 150 182 160 V198 C 182 228, 150 246, 128 256 C 106 246, 74 228, 74 198 V160 Q74 150 84 150 Z'
    sh = (pa('M128 150 V250', stroke='#56626e', sw=4, op=.55, blur=1) + pa('M133 150 V246', stroke='#fff', sw=3, op=.6, blur=1)
          + el(92, 192, 6, 30, '#fff', .8, 2) + el(128, 262, 60, 20, '#3c4652', .6, 8) + pa('M78 156 H178', stroke='#fff', sw=4, op=.7, blur=1))
    s.shape(blade, sf, '#28303a', sh, tr=R)
    s.shape('<rect {ID} x="112" y="140" width="32" height="30" rx="6"/>', s.LG(STEEL, x1=0, y1=0, x2=1, y2=0), '#28303a', '', tr=R, sw=3.5)
    build('Boosters', 'Shovel', s)

# ---------------------------------------------------------------- Shuffle (pinwheel)
def shuffle():
    s = S()
    wf = s.LG(WOOD, x1=0, y1=0, x2=1, y2=0)
    s.shape('<rect {ID} x="120" y="120" width="16" height="140" rx="7"/>', wf, '#45220a', el(125, 200, 2.5, 50, '#fff', .45, 2))
    blade = 'M128 128 C 118 96, 120 54, 128 16 C 172 22, 204 52, 208 86 C 176 90, 150 106, 128 128 Z'
    fold = 'M128 128 C 118 96, 120 54, 128 16 C 150 56, 148 100, 128 128 Z'
    hues = [0.0, .07, .15, .36, .58, .78]
    for i, h in enumerate(hues):
        sat = .85
        f = s.LG([(0, hx(h, sat * .7, 1)), (1, hx(h, sat, .92))], x1=0, y1=0, x2=1, y2=1)
        sh = pa(fold, hx(h - (.03 if .1 < h < .2 else 0), .95, .8 if .05 < h < .2 else .62), op=.8) + pa('M136 34 C 168 42, 190 60, 198 80', stroke='#fff', sw=5, op=.55, blur=2) + el(128, 128, 30, 30, '#000', .25, 8)
        s.shape(blade, f, hx(h, .9, .36), sh, tr=f'rotate({i*60} 128 128)', sw=4)
    pf = s.RG([(0, '#fff8c8'), (.6, '#ffc928'), (1, '#d98a00')])
    s.shape('<circle {ID} cx="128" cy="128" r="15"/>', pf, '#6a3a00', el(123, 123, 5, 4, '#fff', .9, 1), sw=3.5)
    build('Boosters', 'Shuffle', s)

# ---------------------------------------------------------------- Watering can
def can():
    s = S()
    OUT = '#650a12'
    RED = [(0, '#b3161c'), (.25, '#ff7a5c'), (.55, '#e8352a'), (1, '#96101a')]
    rf = s.LG(RED, x1=0, y1=0, x2=1, y2=0)
    # back handle + top handle
    tube(s, 'M70 124 C 20 118, 14 196, 68 204', 13, '#d42a24', OUT, '#ff9a80')
    tube(s, 'M82 112 C 84 52, 150 52, 156 112', 13, '#d42a24', OUT, '#ff9a80')
    # spout
    sp = s.LG(RED, x1=0, y1=0, x2=1, y2=1)
    s.shape('M150 168 L208 84 L228 98 L168 214 Z', sp, OUT, pa('M158 170 L210 94', stroke='#ffb09a', sw=5, op=.7, blur=2))
    rose = '<ellipse {ID} cx="220" cy="88" rx="30" ry="13" transform="rotate(35 220 88)"/>'
    dots = ''.join(f'<circle cx="{220+dx}" cy="{88+dy}" r="2.6" fill="#7a0c14"/>' for dx, dy in ((-14, -10), (-2, -2), (10, 6), (-10, 2), (2, 8), (-4, -12), (14, -2), (20, 12)))
    s.shape(rose, s.LG([(0, '#ffb09a'), (1, '#e03a2c')]), OUT, dots)
    # water fanning out of the rose face and arcing down
    wf = s.LG([(0, '#e6f8ff'), (1, '#3aa4f2')])
    ax, ay = math.cos(math.radians(35)), math.sin(math.radians(35))   # rose major axis
    nx, ny = .64, -.77                                                # outward normal of the rose face
    for i_, (t_, reach) in enumerate(((-.6, 30), (0, 38), (.6, 46))):
        px, py = 220 + ax * 26 * t_ + nx * 8, 88 + ay * 26 * t_ + ny * 8
        cx_, cy_ = px + nx * reach * 1.3, py + ny * reach * 1.1
        ex, ey = px + nx * reach + 18 + 12 * i_, py + 58 + 14 * i_
        d = f'M{px:.1f} {py:.1f} Q{cx_:.1f} {cy_:.1f} {ex:.1f} {ey:.1f}'
        s.add(pa(d, stroke='#145a9c', sw=7) + pa(d, stroke='#8fd2ff', sw=3.5) + pa(d, stroke='#fff', sw=1.3, op=.8))
        dx, dy = ex + 1, ey + 14
        s.shape(f'M{dx} {dy-11} C {dx+7} {dy-2}, {dx+7} {dy+8}, {dx} {dy+8} C {dx-7} {dy+8}, {dx-7} {dy-2}, {dx} {dy-11} Z', wf, '#145a9c',
                el(dx - 2, dy + 1, 1.8, 3, '#fff', .9), sw=2.5)
    body = 'M64 112 H172 L178 222 Q121 240 58 222 Z'
    sh = (el(118, 250, 80, 24, '#5a060c', .55, 8) + el(84, 170, 7, 44, '#fff', .5, 4)
          + pa('M60 196 Q120 212 178 196', stroke='#8a0c14', sw=4, op=.6) + pa('M61 190 Q120 206 177 190', stroke='#ffb09a', sw=2.5, op=.6))
    s.shape(body, rf, OUT, sh)
    s.shape('<ellipse {ID} cx="118" cy="112" rx="55" ry="13"/>', s.LG([(0, '#7a0c14'), (1, '#c8201c')]), OUT,
            el(118, 122, 46, 9, '#3fa9f5', .95) + el(108, 119, 16, 2.5, '#d6f4ff', .9), sw=4)
    build('Boosters', 'WateringCan', s)

if __name__ == '__main__':
    for f in (rake, shovel, shuffle, can): f()
