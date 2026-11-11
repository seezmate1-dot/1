"""Currency icons (Resources/Art/Currency): Coin, Crystal, Heart, Star, plus the empty star slot (Icons/star.empty)."""
from lib import *

def coin():
    s = S()
    OUT = '#7a3e00'
    rim = s.LG([(0, '#ffe98a'), (.5, '#ffc21e'), (1, '#d98200')])
    s.shape('<circle {ID} cx="128" cy="128" r="106"/>', rim, OUT,
            el(128, 250, 120, 40, '#a85a00', .55, 8) + pa('M40 96 A 96 96 0 0 1 150 34', stroke='#fff', sw=9, op=.75, blur=2), sw=6)
    face = s.RG([(0, '#fff3a0'), (.5, '#ffcf30'), (1, '#f0a000')], cx=.4, cy=.32, r=.8)
    s.add('<circle cx="128" cy="128" r="84" fill="#b86a00"/>')
    s.add(f'<circle cx="128" cy="131" r="80" fill="{face}"/>')
    # embossed sunflower: petals + centre
    pet = 'M128 128 C 114 104, 116 80, 128 66 C 140 80, 142 104, 128 128Z'
    s.add(''.join(pa(pet, '#e89a00', tr=f'rotate({i*36} 128 131) translate(0 5)') for i in range(10)))
    s.add(''.join(pa(pet, '#ffe36a', stroke='#c87a00', sw=2.5, tr=f'rotate({i*36} 128 131) translate(0 3)') for i in range(10)))
    c = s.RG([(0, '#ffc64a'), (1, '#d07800')], cx=.4, cy=.35, r=.8)
    s.add(f'<circle cx="128" cy="131" r="27" fill="{c}" stroke="#a85a00" stroke-width="3.5"/>')
    s.add(''.join(f'<circle cx="{128+13*math.cos(a*1.05):.1f}" cy="{131+13*math.sin(a*1.05):.1f}" r="3" fill="#a85a00" opacity=".7"/>' for a in range(6)) + '<circle cx="128" cy="131" r="3" fill="#a85a00" opacity=".7"/>')
    s.add(gloss(84, 76, 13, 20, 40, .9) + el(176, 190, 5, 5, '#fff', .8, 1))
    build('Currency', 'Coin', s, pad=10)

def crystal():
    s = S()
    OUT = '#0c5a2e'
    # emerald, faceted: table on top, pavilion below
    outline = 'M72 40 H184 L236 104 L128 232 L20 104 Z'
    base = s.LG([(0, '#7df2a0'), (.5, '#22c860'), (1, '#0a8a44')])
    fac = (pa('M72 40 L104 104 L20 104Z', '#5fe08a') + pa('M72 40 H184 L152 104 H104Z', '#b6ffc8')
           + pa('M184 40 L236 104 H152Z', '#2fbf66') + pa('M20 104 H104 L128 232Z', '#1fae58')
           + pa('M104 104 H152 L128 232Z', '#3fd878') + pa('M152 104 H236 L128 232Z', '#0c8a44')
           + pa('M72 40 H184 L152 104 H104Z M104 104 L128 232 L152 104 M20 104 H236', stroke='#eafff0', sw=2.5, op=.55)
           + pa('M84 50 H150 L134 82 H100Z', '#fff', op=.55, blur=2) + el(60, 96, 5, 5, '#fff', .9, 1))
    s.shape(outline, base, OUT, fac, sw=6)
    s.add(pa(star(196, 60, 4, 26, 7), '#fff', op=.95) + pa(star(64, 170, 4, 14, 4), '#fff', op=.85))
    build('Currency', 'Crystal', s, pad=12)

HEART = 'M128 226 C 60 176, 22 138, 22 92 C 22 56, 48 34, 78 34 C 100 34, 118 46, 128 64 C 138 46, 156 34, 178 34 C 208 34, 234 56, 234 92 C 234 138, 196 176, 128 226 Z'
def heart():
    s = S()
    f = s.RG([(0, '#ff9a8a'), (.4, '#ff4a3c'), (.85, '#d81428'), (1, '#a80c20')], cx=.36, cy=.28, r=.9)
    sh = (el(150, 236, 110, 44, '#7a0014', .55, 12) + pa('M214 70 C 228 100, 214 140, 170 180', stroke='#ff9aa0', sw=7, op=.55, blur=3)
          + gloss(74, 74, 17, 25, 38, .92))
    s.shape(HEART, f, '#6e0a14', sh, sw=6)
    build('Currency', 'Heart', s, pad=12)

STAR = None
def star_path(ro=112, ri=54, r=13):
    """Five-point star with rounded tips (quadratic through each vertex)."""
    pts = []
    for k in range(10):
        rad = ro if k % 2 == 0 else ri
        a = math.radians(-90 + k * 36)
        pts.append((128 + rad * math.cos(a), 134 + rad * math.sin(a)))
    d = ''
    n = len(pts)
    for i, p in enumerate(pts):
        a, b = pts[i - 1], pts[(i + 1) % n]
        rr = r if i % 2 == 0 else r * .5
        def tow(q):
            L = math.hypot(q[0] - p[0], q[1] - p[1]); return (p[0] + (q[0] - p[0]) * rr / L, p[1] + (q[1] - p[1]) * rr / L)
        p0, p1 = tow(a), tow(b)
        d += ('M' if i == 0 else 'L') + f'{p0[0]:.1f} {p0[1]:.1f} Q{p[0]:.1f} {p[1]:.1f} {p1[0]:.1f} {p1[1]:.1f} '
    return d + 'Z'

def star_icon():
    s = S()
    f = s.RG([(0, '#fff7a8'), (.45, '#ffd42a'), (.9, '#f59a00'), (1, '#e08200')], cx=.38, cy=.28, r=.85)
    d = star_path()
    sh = (el(128, 262, 130, 60, '#b85e00', .55, 12)
          + pa(star_path(74, 36, 8), '#fff2a0', op=.5, blur=3)
          + gloss(92, 92, 12, 19, 40, .95) + el(176, 150, 5, 5, '#fff', .75, 1))
    s.shape(d, f, '#8a4200', sh, sw=6.5)
    build('Currency', 'Star', s, pad=10)

def star_empty():
    """An empty star slot: the same silhouette pressed into the wood."""
    s = S()
    f = s.LG([(0, '#8a5a2c'), (1, '#b98a52')])
    d = star_path()
    sh = (pa(d, 'none', stroke='#4a2608', sw=22, op=.55, blur=5, tr='translate(0 9)')
          + pa(d, 'none', stroke='#ffe2b0', sw=7, op=.35, blur=2, tr='translate(0 -5)'))
    s.shape(d, f, '#5e3410', sh, sw=6.5)
    s.add(pa(d, 'none', stroke='#e9cf9a', sw=3, op=.5, tr='translate(0 7)') if False else '')
    build('Icons', 'star.empty', s, pad=10)

if __name__ == '__main__':
    coin(); crystal(); heart(); star_icon(); star_empty()
