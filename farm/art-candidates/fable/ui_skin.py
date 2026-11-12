"""The UI skin (Resources/Art/UI): 9-slice panels, buttons, plates, bars, round buttons, badges, board cells.

Every rounded-rect sprite has 64 px corners, so the game shows any radius from one sprite
(Image.pixelsPerUnitMultiplier = 64 / radius); the 9-slice borders are in SLICES below and mirrored in
Game/UI/UiSkin.cs. «Tint» sprites are greyscale and get multiplied by the node's colour, with a white «Gloss»
overlay on top; the named colours (yellow, green, blue, red, grey) are painted in full colour.
"""
import json
from lib import *

R = 64          # corner radius of every 9-slice sprite, px
O = 7           # outline width of plates and wells
SLICES = {}     # name -> (left, bottom, right, top)

def rr(x, y, w, h, r, fill, extra=''):
    return f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{max(0, r)}" fill="{fill}" {extra}/>'

class Doc:
    def __init__(self, w, h):
        self.w, self.h, self.defs, self.body = w, h, [], []
    def lg(self, st, **k):
        u, d = lg(st, **k); self.defs.append(d); return u
    def rg(self, st, **k):
        u, d = rg(st, **k); self.defs.append(d); return u
    def clip(self, el_):
        i = uid('c'); self.defs.append(f'<clipPath id="{i}">{el_}</clipPath>'); return f'clip-path="url(#{i})"'
    def add(self, s): self.body.append(s)
    def svg(self):
        return (f'<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="{self.w}" height="{self.h}" viewBox="0 0 {self.w} {self.h}">'
                f'<defs>{FILTERS}{"".join(self.defs)}</defs>{"".join(self.body)}</svg>')

def save(name, d, border=None, ss=3):
    folder = os.path.join(HERE, 'UI'); os.makedirs(folder, exist_ok=True)
    svg = d.svg()
    open(os.path.join(folder, name + '.svg'), 'w').write(svg)
    png = resvg_py.svg_to_bytes(svg_string=svg, width=d.w * ss, height=d.h * ss)
    im = Image.open(io.BytesIO(bytes(png))).convert('RGBA').resize((d.w, d.h), Image.LANCZOS)
    im.save(os.path.join(folder, name + '.png'))
    if border: SLICES[name] = border
    print('UI', name, im.size, border)

# ---------------------------------------------------------------- buttons
SH = 10         # the soft drop shadow under buttons and panels, px below the sprite's body

def shadow(d, W, H):
    """A soft shadow under a body W×H (the sprite is SH taller)."""
    d.add(rr(8, 14, W - 16, H - 14 + SH - 5, R, '#2a1206', 'opacity=".38" filter="url(#b3)"'))

def button(name, top, bot, lipc, out, lip=26, mode='full'):
    """mode: full (colour), tint (greyscale for multiply), gloss (white highlights only)."""
    o = 9
    W, H = 2 * R + 16, 2 * R + 16 + lip
    d = Doc(W, H + SH)
    fh = H - 2 * o - lip
    if mode != 'gloss':
        shadow(d, W, H)
        d.add(rr(0, 0, W, H, R, out))
        d.add(rr(o, o, W - 2 * o, H - 2 * o, R - o, lipc))
        cp = d.clip(rr(o, o, W - 2 * o, H - 2 * o, R - o, '#000'))
        d.add(f'<g {cp}>' + rr(o, H - o - 11, W - 2 * o, 13, 0, '#000', 'opacity=".18"')
              + rr(o, o + fh - 2, W - 2 * o, 9, 0, '#fff', 'opacity=".16"') + '</g>')
        d.add(rr(o, o, W - 2 * o, fh, R - o, d.lg([(0, top), (.5, bot), (1, bot)])))
    if mode != 'tint':
        cp = d.clip(rr(o, o, W - 2 * o, fh, R - o, '#000'))
        g = d.lg([(0, '#fff', .7), (1, '#fff', .16)])
        hi = rr(o + 6, o + 5, W - 2 * o - 12, 44, R - o - 6, g)
        hi += el(36, 27, 10, 6, '#fff', .98, 1, -28) + el(52, 21, 3.5, 3, '#fff', .9, 1)
        hi += rr(o - 4, o + fh - 8, W - 2 * o + 8, 13, 0, '#fff', 'opacity=".3" filter="url(#b2)"')
        d.add(f'<g {cp}>{hi}</g>')
    save(name, d, (R, R + lip + SH, R, R))

BUTTONS = {
    'Yellow': ('#ffe86a', '#ffc420', '#dc8a00', '#8a4a00'),
    'Green': ('#a4e660', '#5fbc3c', '#3a8a22', '#1e5c12'),
    'Blue': ('#86d0ff', '#3c9cea', '#2068b0', '#103a6e'),
    'Red': ('#ff9474', '#ec4c32', '#a82a1a', '#6e1408'),
    'Grey': ('#e2d3b2', '#c6b28c', '#9a8662', '#5e4a2a'),
}

# ---------------------------------------------------------------- panels
def panel(name='Panel', inner=('#fdf0d2', '#f4dfb4'), dark=False):
    lip = 20
    o = 8
    W, H = 2 * R + 16, 2 * R + 16 + lip
    d = Doc(W, H + SH)
    shadow(d, W, H)
    d.add(rr(0, 0, W, H, R, '#5a3210'))
    d.add(rr(o, o, W - 2 * o, H - 2 * o, R - o, '#8e5a2a'))
    fh = H - 2 * o - lip
    d.add(rr(o, o, W - 2 * o, fh, R - o, d.lg([(0, '#e8b87a'), (1, '#c08a4e')])))
    cp = d.clip(rr(o, o, W - 2 * o, fh, R - o, '#000'))
    d.add(f'<g {cp}>' + rr(o + 2, o + 2, W - 2 * o - 4, fh, R - o - 2, 'none', 'stroke="#fff0d0" stroke-width="3.5" opacity=".8"') + '</g>')
    k = 17   # the wooden rim
    d.add(rr(o + k - 3, o + k - 3, W - 2 * (o + k) + 6, fh - 2 * k + 6, R - o - k + 3, '#7a4a20', 'opacity=".7"'))
    d.add(rr(o + k - 3, o + k - 1, W - 2 * (o + k) + 6, fh - 2 * k + 5, R - o - k + 3, '#fff0d0', 'opacity=".55"'))
    d.add(rr(o + k, o + k, W - 2 * (o + k), fh - 2 * k, R - o - k, d.lg([(0, inner[0]), (1, inner[1])])))
    cp = d.clip(rr(o + k, o + k, W - 2 * (o + k), fh - 2 * k, R - o - k, '#000'))
    if dark:
        d.add(f'<g {cp}>' + rr(o + k - 4, o + k - 6, W - 2 * (o + k) + 8, fh, R - o - k, 'none', 'stroke="#000" stroke-width="14" opacity=".5" filter="url(#b3)"') + '</g>')
    else:
        d.add(f'<g {cp}>' + rr(o + k, o + k + 1.5, W - 2 * (o + k), fh, R - o - k, 'none', 'stroke="#fff" stroke-width="3.5" opacity=".9"')
              + rr(o + k - 4, o + fh - k - 9, W, 13, 0, '#c89a5a', 'opacity=".35" filter="url(#b2)"') + '</g>')
    # a nail in every corner of the rim
    for cx, cy in ((27, 27), (W - 27, 27), (27, o + fh - 19), (W - 27, o + fh - 19)):
        d.add(f'<circle cx="{cx}" cy="{cy + 1.5}" r="5.2" fill="#5a3210"/><circle cx="{cx}" cy="{cy}" r="4.6" fill="#f6d9a0"/><circle cx="{cx - 1.2}" cy="{cy - 1.2}" r="1.8" fill="#fff"/>')
    save(name, d, (R, R + lip + SH, R, R))

def ribbon():
    """A title banner with folded ends, stretched along X only (its middle is flat)."""
    W, H, T = 220, 112, 46          # T: how far a tail reaches
    d = Doc(W, H)
    out, dk = '#103a6e', '#1c5aa0'
    for sx in (1, -1):
        tr = '' if sx == 1 else f'transform="translate({W} 0) scale(-1 1)"'
        tail = f'M4 30 H{T + 22} V{H - 4} H4 L24 {(30 + H - 4) / 2} Z'
        fold = f'M{T} {H - 22} L{T + 22} {H - 4} V{H - 22} Z'
        d.add(f'<g {tr}><path d="{tail}" fill="{dk}" stroke="{out}" stroke-width="7" stroke-linejoin="round"/>'
              f'<path d="M10 36 H{T + 22}" stroke="#fff" stroke-width="3" opacity=".25"/>'
              f'<path d="{fold}" fill="#0c2c58" stroke="{out}" stroke-width="5" stroke-linejoin="round"/></g>')
    d.add(rr(T - 12, 4, W - 2 * (T - 12), H - 26, 14, out))
    d.add(rr(T - 5, 11, W - 2 * (T - 5), H - 40, 9, d.lg([(0, '#8ad4ff'), (.5, '#3c9cea'), (1, '#2a80d0')])))
    cp = d.clip(rr(T - 5, 11, W - 2 * (T - 5), H - 40, 9, '#000'))
    d.add(f'<g {cp}>' + rr(T - 5, 11, W - 2 * (T - 5), 26, 9, d.lg([(0, '#fff', .55), (1, '#fff', .08)]))
          + rr(T - 5, H - 37, W, 10, 0, '#103a6e', 'opacity=".28"') + '</g>')
    save('Ribbon', d, (T + 30, 0, T + 30, 0))

def rays():
    """Golden rays behind a big star."""
    S_ = 256
    d = Doc(S_, S_)
    g = d.rg([(0, '#fff6b0', 1), (.5, '#ffc21e', .95), (.85, '#ff9a1e', .55), (1, '#ff9a1e', 0)], cx=128, cy=128, r=128, units=1)
    body = ''
    for k in range(12):
        w = 20 if k % 2 == 0 else 13
        L = 126 if k % 2 == 0 else 100
        body += f'<path d="M128 128 L{128 - w} {128 - L} L{128 + w} {128 - L}Z" transform="rotate({k * 30} 128 128)" fill="{g}"/>'
    d.add(f'<g filter="url(#b1)">{body}</g><circle cx="128" cy="128" r="80" fill="{d.rg([(0, "#fffbe0", .95), (.6, "#ffe36a", .5), (1, "#ffd84a", 0)], cx=.5, cy=.5, r=.5)}"/>')
    save('Rays', d)

def shine():
    """The light that sweeps across the main button: a slanted soft band."""
    d = Doc(128, 128)
    g = d.lg([(0, '#fff', 0), (.5, '#fff', .75), (1, '#fff', 0)], x1=0, y1=0, x2=1, y2=0)
    d.add(f'<path d="M58 0 H112 L70 128 H16 Z" fill="{g}"/>')
    save('Shine', d)

def inset():
    """A sunken sub-panel, greyscale: multiplied by the node's colour (WoodInset, a dimmed row, a picked card)."""
    W = H = 2 * R + 16
    d = Doc(W, H)
    d.add(rr(0, 2, W, H - 2, R, '#fff', 'opacity=".8"'))                  # a light lower lip
    d.add(rr(0, 0, W, H - 3, R, '#b4b4b4'))
    d.add(rr(2.5, 3.5, W - 5, H - 8.5, R - 2.5, d.lg([(0, '#f2f2f2'), (1, '#ffffff')])))
    cp = d.clip(rr(2.5, 3.5, W - 5, H - 8.5, R - 2.5, '#000'))
    d.add(f'<g {cp}>' + rr(-2, -6, W + 4, H + 20, R, 'none', 'stroke="#3a1c08" stroke-width="12" opacity=".3" filter="url(#b3)"') + '</g>')
    save('Inset', d, (R, R, R, R))

def plate():
    W = H = 2 * R + 16
    d = Doc(W, H)
    d.add(rr(0, 3, W, H - 3, R, '#fff', 'opacity=".28"'))
    d.add(rr(0, 0, W, H - 3, R, '#2a1606', 'opacity=".92"'))
    d.add(rr(5, 5, W - 10, H - 13, R - 5, d.lg([(0, '#3a2210', .9), (1, '#5a3a1e', .86)])))
    cp = d.clip(rr(5, 5, W - 10, H - 13, R - 5, '#000'))
    d.add(f'<g {cp}>' + rr(2, 0, W - 4, H + 20, R, 'none', 'stroke="#000" stroke-width="12" opacity=".45" filter="url(#b3)"')
          + rr(5, 6, W - 10, H - 13, R - 5, 'none', 'stroke="#ffd9a0" stroke-width="2.5" opacity=".28"') + '</g>')
    save('Plate', d, (R, R, R, R))

def bar():
    W = H = 2 * R + 16
    d = Doc(W, H)
    d.add(rr(0, 3, W, H - 3, R, '#fff', 'opacity=".3"'))
    d.add(rr(0, 0, W, H - 3, R, '#2a1606'))
    d.add(rr(7, 7, W - 14, H - 17, R - 7, d.lg([(0, '#4a2e14'), (1, '#6e4a26')])))
    cp = d.clip(rr(7, 7, W - 14, H - 17, R - 7, '#000'))
    d.add(f'<g {cp}>' + rr(4, 0, W - 8, H + 20, R, 'none', 'stroke="#000" stroke-width="14" opacity=".5" filter="url(#b3)"') + '</g>')
    save('BarTrack', d, (R, R, R, R))
    # the fill: greyscale, multiplied by the bar's colour; sits inside the track's rim
    d = Doc(W, H)
    m = 12
    d.add(rr(m, m, W - 2 * m, H - 2 * m - 3, R - m, d.lg([(0, '#ffffff'), (.5, '#f2f2f2'), (1, '#b4b4b4')])))
    cp = d.clip(rr(m, m, W - 2 * m, H - 2 * m - 3, R - m, '#000'))
    d.add(f'<g {cp}>' + rr(m, m, W - 2 * m, H - 2 * m - 3, R - m, 'none', 'stroke="#000" stroke-width="5" opacity=".28"') + '</g>')
    save('BarFill', d, (R, R, R, R))
    d = Doc(W, H)
    d.add(rr(m + 10, m + 8, W - 2 * m - 20, 22, 11, d.lg([(0, '#fff', .75), (1, '#fff', .2)])))
    save('BarGloss', d, (R, R, R, R))

# ---------------------------------------------------------------- round buttons, badges
def round_(mode):
    S_, lip = 256, 16
    d = Doc(S_, S_ + lip)
    c, r = S_ / 2, S_ / 2
    o = 9
    if mode == 'tint':
        d.add(f'<circle cx="{c}" cy="{c + lip}" r="{r}" fill="#5a5a5a"/><circle cx="{c}" cy="{c}" r="{r}" fill="#5a5a5a"/><rect x="0" y="{c}" width="{S_}" height="{lip}" fill="#5a5a5a"/>')
        d.add(f'<circle cx="{c}" cy="{c + lip - 2}" r="{r - o}" fill="#a4a4a4"/>')
        d.add(f'<circle cx="{c}" cy="{c}" r="{r - o}" fill="{d.rg([(0, "#ffffff"), (.6, "#f4f4f4"), (1, "#c8c8c8")], cx=.4, cy=.3, r=.8)}"/>')
    else:
        cp = d.clip(f'<circle cx="{c}" cy="{c}" r="{r - o}"/>')
        g = d.lg([(0, '#fff', .6), (1, '#fff', .05)])
        hi = f'<ellipse cx="{c}" cy="{c - 44}" rx="{r - o - 22}" ry="{r - o - 52}" fill="{g}"/>'
        hi += el(72, 62, 16, 9, '#fff', .95, 1, -35)
        hi += f'<circle cx="{c}" cy="{c - 5}" r="{r - o}" fill="none" stroke="#fff" stroke-width="7" opacity=".3" filter="url(#b3)"/>'
        d.add(f'<g {cp}>{hi}</g>')
    save('Round' + ('Tint' if mode == 'tint' else 'Gloss'), d)

def dot(mode):
    S_ = 128
    d = Doc(S_, S_)
    c = S_ / 2
    if mode == 'tint':
        d.add(f'<circle cx="{c}" cy="{c}" r="{c - 9}" fill="{d.rg([(0, "#ffffff"), (.6, "#f0f0f0"), (1, "#b8b8b8")], cx=.4, cy=.3, r=.8)}"/>')
    else:
        d.add(f'<circle cx="{c}" cy="{c + 3}" r="{c - 5}" fill="none" stroke="#3a1c08" stroke-width="10" opacity=".45"/>')
        d.add(f'<circle cx="{c}" cy="{c}" r="{c - 7}" fill="none" stroke="#fff" stroke-width="9"/>')
        d.add(f'<ellipse cx="{c}" cy="{c - 22}" rx="{c - 26}" ry="{c - 42}" fill="{d.lg([(0, "#fff", .6), (1, "#fff", .1)])}"/>')
    save('Dot' + ('Tint' if mode == 'tint' else 'Gloss'), d)

def booster_slot():
    S_ = 256
    d = Doc(S_, S_)
    lip = 14
    d.add(rr(0, 0, S_, S_, 60, '#5e3410'))
    d.add(rr(8, 8, S_ - 16, S_ - 16, 52, '#96602e'))
    d.add(rr(8, 8, S_ - 16, S_ - 16 - lip, 52, d.lg([(0, '#e2b276'), (1, '#c48e52')])))
    cp = d.clip(rr(8, 8, S_ - 16, S_ - 16 - lip, 52, '#000'))
    d.add(f'<g {cp}>' + rr(10, 10.5, S_ - 20, S_, 50, 'none', 'stroke="#ffe8c0" stroke-width="4" opacity=".7"') + '</g>')
    k = 24
    d.add(rr(k - 3, k - 2, S_ - 2 * k + 6, S_ - 2 * k - lip + 7, 40, '#fff', 'opacity=".55"'))
    d.add(rr(k - 3, k - 3, S_ - 2 * k + 6, S_ - 2 * k - lip + 5, 40, '#8a5a2c'))
    d.add(rr(k, k, S_ - 2 * k, S_ - 2 * k - lip, 37, d.lg([(0, '#f0d9a8'), (1, '#fbeccb')])))
    cp = d.clip(rr(k, k, S_ - 2 * k, S_ - 2 * k - lip, 37, '#000'))
    d.add(f'<g {cp}>' + rr(k - 4, k - 8, S_ - 2 * k + 8, S_, 40, 'none', 'stroke="#7a4a1c" stroke-width="14" opacity=".4" filter="url(#b5)"') + '</g>')
    save('BoosterSlot', d)

# ---------------------------------------------------------------- the board
def cell(name, top, bot, edge):
    """A calm, neutral board cell: the pieces and the turf plates carry the colour, not the board."""
    S_ = 128
    d = Doc(S_, S_)
    d.add(rr(0, 0, S_, S_, 16, edge))
    d.add(rr(0, 0, S_, S_ - 3, 16, d.lg([(0, top), (1, bot)])))
    cp = d.clip(rr(0, 0, S_, S_ - 3, 16, '#000'))
    d.add(f'<g {cp}>' + rr(1.5, 2, S_ - 3, S_, 15, 'none', 'stroke="#fff" stroke-width="3" opacity=".3"') + '</g>')
    save(name, d, (24, 24, 24, 24))

def all_():
    for n, (t, b, l, o) in BUTTONS.items():
        button('Button' + n, t, b, l, o)
    button('ButtonTint', '#ffffff', '#ededed', '#a0a0a0', '#5c5c5c', mode='tint')
    button('ButtonGloss', 0, 0, 0, 0, mode='gloss')
    panel(); panel('PanelDark', ('#3a2210', '#5a3a1e'), dark=True); ribbon(); rays(); shine(); inset(); plate(); bar()
    round_('tint'); round_('gloss'); dot('tint'); dot('gloss')
    booster_slot()
    cell('CellLight', '#ebe6dc', '#e5dfd4', '#cfc8ba')
    cell('CellDark', '#e1dbcf', '#dad3c6', '#c6beaf')
    json.dump(SLICES, open(os.path.join(HERE, 'UI', 'slices.json'), 'w'), indent=1)

if __name__ == '__main__':
    all_()
