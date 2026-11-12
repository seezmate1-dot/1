"""Board effect sprites (Resources/Art/Fx). White ones (Sparkle, Streak, Petal) are tinted by the game;
Burst, RainbowRing and Pollen carry their own colours and are drawn untinted."""
import colorsys
from lib import *
from lib import _bbox as lib_bbox

def out(name, svg, w=256, h=256, ss=3):
    d = os.path.join(HERE, 'Fx'); os.makedirs(d, exist_ok=True)
    open(os.path.join(d, name + '.svg'), 'w').write(svg)
    png = resvg_py.svg_to_bytes(svg_string=svg, width=w * ss, height=h * ss)
    im = Image.open(io.BytesIO(bytes(png))).convert('RGBA').resize((w, h), Image.LANCZOS)
    im.save(os.path.join(d, name + '.png')); print('Fx', name, im.size)

def doc(body, defs='', w=256, h=256):
    return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="0 0 {w} {h}"><defs>{FILTERS}{defs}</defs>{body}</svg>')

def sparkle():
    g, d1 = rg([(0, '#fff', 1), (.5, '#fff', .55), (1, '#fff', 0)], cx=.5, cy=.5, r=.5)
    def ray(L, w, rot):
        return f'<path d="M64 {64-L} Q{64+w*.35} {64-w*.35} {64+L} 64 Q{64+w*.35} {64+w*.35} 64 {64+L} Q{64-w*.35} {64+w*.35} {64-L} 64 Q{64-w*.35} {64-w*.35} 64 {64-L}Z" transform="rotate({rot} 64 64)" fill="#fff"/>'
    body = f'<circle cx="64" cy="64" r="40" fill="{g}" opacity=".7"/>' + ray(60, 13, 0) + f'<g opacity=".8">{ray(30, 9, 45)}</g>'
    out('Sparkle', doc(body, d1, 128, 128), 128, 128)

def burst():
    """A cartoon explosion: a spiky orange cloud, a yellow one and a white-hot core, with a dark outline."""
    def spikes(n, ro, ri, rot, jit):
        pts = []
        for k in range(n * 2):
            r = (ro * (1 - jit * ((k * 7) % 5) / 5)) if k % 2 == 0 else ri
            a = math.radians(rot + k * 180 / n)
            pts.append((128 + r * math.cos(a), 128 + r * math.sin(a)))
        return 'M' + ' L'.join(f'{x:.1f} {y:.1f}' for x, y in pts) + 'Z'
    g1, d1 = rg([(0, '#ffb21e'), (.7, '#ff7a1a'), (1, '#e8420e')], cx=.5, cy=.5, r=.5)
    g2, d2 = rg([(0, '#fff6a0'), (1, '#ffc928')], cx=.5, cy=.5, r=.5)
    g0, d0 = rg([(0, '#ffb21e', .55), (1, '#ff7a1a', 0)], cx=.5, cy=.5, r=.5)
    a, b, c = spikes(12, 118, 78, -90, .22), spikes(12, 84, 54, -75, .2), spikes(10, 46, 28, -90, .1)
    body = (f'<circle cx="128" cy="128" r="128" fill="{g0}"/>'
            + f'<path d="{a}" fill="{g1}" stroke="#8a2400" stroke-width="7" stroke-linejoin="round"/>'
            + f'<path d="{b}" fill="{g2}" stroke="#f08a00" stroke-width="4" stroke-linejoin="round"/>'
            + f'<path d="{c}" fill="#fffdf0" stroke="#ffe36a" stroke-width="3" stroke-linejoin="round"/>'
            + el(98, 92, 12, 7, '#fff', .8, 1, -35))
    out('Burst', doc(body, d0 + d1 + d2))

def streak():
    """A horizontal light streak for line clears and beams: bright core, tapered soft ends. Stretched along X."""
    g, d1 = lg([(0, '#fff', 0), (.12, '#fff', .9), (.5, '#fff', 1), (.88, '#fff', .9), (1, '#fff', 0)], x1=0, y1=0, x2=1, y2=0)
    body = (f'<ellipse cx="128" cy="32" rx="124" ry="26" fill="{g}" opacity=".45" filter="url(#b5)"/>'
            + f'<path d="M2 32 Q128 8 254 32 Q128 56 2 32Z" fill="{g}" opacity=".8" filter="url(#b1)"/>'
            + f'<path d="M14 32 Q128 23 242 32 Q128 41 14 32Z" fill="#fff"/>')
    out('Streak', doc(body, d1, 256, 64), 256, 64)

def rainbow_ring():
    segs = ''
    n = 48
    for k in range(n):
        a0, a1 = math.radians(k * 360 / n - .6), math.radians((k + 1) * 360 / n + .6)
        r, g_, b = colorsys.hsv_to_rgb(k / n, .8, 1)
        col = '#%02x%02x%02x' % (int(r * 255), int(g_ * 255), int(b * 255))
        ro, ri = 118, 86
        segs += (f'<path d="M{128+ro*math.cos(a0):.2f} {128+ro*math.sin(a0):.2f} A{ro} {ro} 0 0 1 {128+ro*math.cos(a1):.2f} {128+ro*math.sin(a1):.2f} '
                 f'L{128+ri*math.cos(a1):.2f} {128+ri*math.sin(a1):.2f} A{ri} {ri} 0 0 0 {128+ri*math.cos(a0):.2f} {128+ri*math.sin(a0):.2f}Z" fill="{col}"/>')
    body = (f'<g filter="url(#b8)" opacity=".6">{segs}</g><g filter="url(#b2)">{segs}</g>'
            + '<circle cx="128" cy="128" r="103" fill="none" stroke="#fff" stroke-width="7" opacity=".85" filter="url(#b2)"/>'
            + ''.join(f'<path d="{star(128 + 103*math.cos(math.radians(a)), 128 + 103*math.sin(math.radians(a)), 4, 15, 4)}" fill="#fff"/>' for a in (-50, 70, 190)))
    out('RainbowRing', doc(body))

def pollen():
    """A bee's trail: a golden puff with a tiny star."""
    g, d1 = rg([(0, '#fffbd0', 1), (.4, '#ffd83a', .95), (.75, '#ffb21e', .5), (1, '#ff9a00', 0)], cx=.5, cy=.5, r=.5)
    body = f'<circle cx="64" cy="64" r="60" fill="{g}"/><path d="{star(64, 64, 4, 34, 9)}" fill="#fff" opacity=".95"/>'
    out('Pollen', doc(body, d1, 128, 128), 128, 128)

def petal():
    """One white petal (tinted): the rainbow throws a ring of them when it fires."""
    g, d1 = lg([(0, '#fff'), (1, '#d8d8d8')])
    body = (f'<path d="M64 120 C 30 92, 30 36, 64 8 C 98 36, 98 92, 64 120Z" fill="{g}" stroke="#8a8a8a" stroke-width="6" stroke-linejoin="round"/>'
            + el(54, 48, 7, 16, '#fff', .9, 2, 10))
    out('Petal', doc(body, d1, 128, 128), 128, 128)

def bee_parts():
    """The bee in two layers on the same canvas as Specials/Bee: the wings alone and the body without them,
    so the board can flap the wings under the body."""
    import specials
    full = specials.bee_variant(specials.BEST_BEE)
    x0, y0, x1, y1 = lib_bbox(full)
    k = (256 - 2 * PAD) / max(x1 - x0, y1 - y0)
    tr = f'translate({128 - k * (x0 + x1) / 2:.2f} {128 - k * (y0 + y1) / 2:.2f}) scale({k:.4f})'
    for name, keep in (('BeeWings', lambda i: i < 2), ('BeeBody', lambda i: i >= 2)):
        s = specials.bee_variant(specials.BEST_BEE)
        s.body = [b for i, b in enumerate(s.body) if keep(i)]
        out(name, s.svg(tr))

if __name__ == '__main__':
    sparkle(); petal(); bee_parts(); burst(); streak(); rainbow_ring(); pollen()
