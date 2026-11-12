"""Icons of the farm's goods, tools and animals (item.*, tool.*, animal.*): the HUD icon style — chunky shapes, a
thick tinted outline, a highlight at the top left. Written to Icons/ here and installed to Resources/Icons.

    python3 farm/art-candidates/fable/farm_icons.py [name ...]
"""
import os, shutil, sys
from lib import *
import lib
sys.path.insert(0, os.path.join(HERE, '..', '..', 'tools', 'art', 'farm_art'))
from iso import lite, dark, mix

lib.SW_SCALE = 1.4
ICONS = {}
RES = os.path.normpath(os.path.join(HERE, '..', '..', 'unity', 'SunnyValley', 'Assets', 'SunnyValley', 'Resources', 'Icons'))


def icon(name):
    def reg(fn):
        ICONS[name] = fn
        return fn
    return reg


def tri(c, l=.45, d=.38):
    return [(0, lite(c, l)), (.5, c), (1, dark(c, d))]


def ball(s, cx, cy, rx, ry, c, out=None, sw=5, hl=True, rot=0, extra=''):
    f = s.RG(tri(c), cx=.36, cy=.28, r=.9)
    sh = el(cx, cy + ry * 1.05, rx * 1.05, ry * .5, dark(c, .55), .45, 8) + extra
    if hl:
        sh += el(cx - rx * .38, cy - ry * .42, rx * .2, ry * .14, '#fff', .85, 1, -30)
    t = f' transform="rotate({rot} {cx} {cy})"' if rot else ''
    s.shape(f'<ellipse {{ID}} cx="{cx}" cy="{cy}" rx="{rx}" ry="{ry}"{t}/>', f, out or dark(c, .62), sh, sw=sw)


def blob(s, d, c, out=None, sw=5, shade='', grad=None):
    f = grad or s.LG(tri(c), x1=0, y1=0, x2=.6, y2=1)
    s.shape(d, f, out or dark(c, .62), shade, sw=sw)


def rr(x, y, w, h, r):
    return f'<rect {{ID}} x="{x}" y="{y}" width="{w}" height="{h}" rx="{r}"/>'


def leafp(x, y, ln, wd, rot):
    return f'M{x} {y} C {x-wd} {y-ln*.4}, {x-wd*.7} {y-ln*.9}, {x} {y-ln} C {x+wd*.7} {y-ln*.9}, {x+wd} {y-ln*.4}, {x} {y}Z', f'rotate({rot} {x} {y})'


def leaf(s, x, y, ln, wd, rot, c='#5fc23a'):
    d, tr = leafp(x, y, ln, wd, rot)
    s.shape(d, s.LG([(0, lite(c, .4)), (1, dark(c, .15))]), '#1e5c12', pa(f'M{x} {y} V{y-ln*.85}', stroke='#e8ffc0', sw=3, op=.6), tr=tr, sw=4.5)


# ---------------------------------------------------------------- crops and fruit
@icon('item.wheat')
def wheat(s):
    for rot, x in ((-22, 104), (20, 152), (0, 128)):
        g = f'rotate({rot} {x} 236)'
        s.add(pa(f'M{x} 236 V96', stroke='#7a4a08', sw=13, tr=g) + pa(f'M{x} 236 V96', stroke='#e8b83a', sw=6, tr=g))
        for i in range(5):
            y = 150 - i * 25
            for sg in (-1, 1):
                d = f'M{x} {y} C {x+sg*26} {y-4}, {x+sg*26} {y-34}, {x+sg*4} {y-40} C {x+sg*2} {y-20}, {x} {y-8}, {x} {y}Z'
                s.shape(d, s.LG(tri('#f6c53a')), '#7a4a08', el(x + sg * 12, y - 24, 4, 7, '#fff', .55, 1), tr=g, sw=4)
        s.shape(f'M{x} 50 C {x-13} 34, {x-9} 14, {x} 6 C {x+9} 14, {x+13} 34, {x} 50Z', s.LG(tri('#f6c53a')), '#7a4a08', '', tr=g, sw=4)


@icon('item.sunflower')
def sunflower(s):
    for i in range(14):
        d = 'M128 128 C 106 96, 110 44, 128 24 C 146 44, 150 96, 128 128Z'
        s.shape(d, s.LG(tri('#ffcf2e', .5, .25)), '#8a4a00', '', tr=f'rotate({i*360/14} 128 128)', sw=4.5)
    ball(s, 128, 128, 58, 58, '#8a4a1c', '#3e1c06', 6)
    for i in range(18):
        a = i * 2.4
        r = 10 + i * 2.4
        s.add(el(128 + math.cos(a) * r, 128 + math.sin(a) * r, 4.4, 4.4, '#4a240a', .8))


@icon('item.strawberry')
def strawberry(s):
    d = 'M128 62 C 196 44, 226 96, 206 150 C 190 196, 150 232, 128 240 C 106 232, 66 196, 50 150 C 30 96, 60 44, 128 62Z'
    f = s.RG(tri('#e8262e', .5, .4), cx=.36, cy=.3, r=.85)
    s.shape(d, f, '#6e0a10', el(128, 250, 90, 40, '#7a0810', .5, 10) + el(88, 104, 15, 22, '#fff', .75, 3, 30), sw=6)
    for (x, y) in ((100, 150), (140, 128), (170, 160), (128, 186), (92, 196), (158, 208), (182, 116), (72, 132)):
        s.add(el(x, y, 4.6, 6.4, '#ffe9a0') + el(x, y + 1.6, 4.6, 5, '#c98a10', .35))
    for rot in (-62, -30, 0, 30, 62):
        leaf(s, 128, 76, 52, 15, rot, '#4fb02e')


@icon('item.cotton')
def cotton(s):
    leaf(s, 128, 220, 80, 30, -62, '#4fb02e'); leaf(s, 128, 220, 80, 30, 62, '#4fb02e')
    s.add(pa('M128 244 V190', stroke='#4a2608', sw=18) + pa('M128 244 V190', stroke='#a8703f', sw=9))
    blob(s, 'M60 178 C 70 214, 186 214, 196 178 L176 150 L128 172 L80 150Z', '#9a6a3a', '#4a2608')
    for (x, y, r) in ((82, 118, 46), (174, 118, 46), (128, 70, 50), (128, 136, 50)):
        ball(s, x, y, r, r * .95, '#f4f0e8', '#8a7a66', 5)


@icon('item.beet')
def beet(s):
    for rot, c in ((-34, '#3f9a3a'), (30, '#3f9a3a'), (-4, '#57b044')):
        d, tr = leafp(128, 112, 104, 26, rot)
        s.shape(d, s.LG(tri(c, .3, .2)), '#1e5c12', pa('M128 112 V22', stroke='#e04a8e', sw=6), tr=tr, sw=5)
    d = 'M128 96 C 196 96, 214 150, 190 190 C 172 220, 140 226, 128 250 C 116 226, 84 220, 66 190 C 42 150, 60 96, 128 96Z'
    f = s.RG(tri('#b02a72', .5, .45), cx=.36, cy=.28, r=.9)
    s.shape(d, f, '#4e0e30', el(128, 252, 80, 34, '#4e0e30', .5, 10) + el(92, 134, 14, 20, '#fff', .7, 3, 30)
            + pa('M84 176 Q128 190 172 172 M96 204 Q128 214 160 200', stroke='#7a1a50', sw=4, op=.6), sw=6)


@icon('item.potato')
def potato(s):
    for (x, y, rx, ry, rot) in ((162, 104, 62, 46, -18), (104, 160, 78, 58, 14)):
        ball(s, x, y, rx, ry, '#d9a85e', '#6a4216', 6, rot=rot)
        for (dx, dy) in ((-24, 6), (18, -10), (30, 18), (-6, 26)):
            s.add(el(x + dx, y + dy, 5, 3.4, '#8a5a24', .7, rot=rot))


@icon('item.pumpkin')
def pumpkin(s):
    s.add(pa('M128 70 C 124 44, 138 30, 156 26', stroke='#2e5c12', sw=22) + pa('M128 70 C 124 44, 138 30, 156 26', stroke='#6fae3a', sw=11))
    for (x, rx) in ((62, 44), (194, 44), (92, 50), (164, 50), (128, 52)):
        ball(s, x, 150, rx, 84, '#f08a1e', '#7a3a00', 5.5, hl=(x == 92))


@icon('item.pear')
def pear(s):
    s.add(pa('M128 60 C 128 40, 136 28, 148 20', stroke='#4a2608', sw=15) + pa('M128 60 C 128 40, 136 28, 148 20', stroke='#a8703f', sw=7))
    leaf(s, 134, 52, 56, 18, 62, '#5fc23a')
    d = 'M128 52 C 160 52, 164 92, 176 118 C 204 152, 204 240, 128 240 C 52 240, 52 152, 80 118 C 92 92, 96 52, 128 52Z'
    f = s.RG(tri('#f2d23c', .5, .35), cx=.36, cy=.3, r=.9)
    s.shape(d, f, '#7a5a08', el(128, 252, 84, 34, '#a87a10', .5, 10) + el(96, 150, 13, 26, '#fff', .7, 3, 12), sw=6)


@icon('item.cherry')
def cherry(s):
    s.add(pa('M92 160 C 100 100, 130 60, 156 36 C 160 80, 168 120, 172 150', stroke='#2e5c12', sw=17) + pa('M92 160 C 100 100, 130 60, 156 36 C 160 80, 168 120, 172 150', stroke='#6fae3a', sw=8))
    leaf(s, 156, 40, 70, 24, 74, '#5fc23a')
    ball(s, 88, 180, 58, 56, '#d21a3e', '#5e0a1c', 6)
    ball(s, 176, 172, 56, 54, '#c2183a', '#5e0a1c', 6)


# ---------------------------------------------------------------- animal goods
@icon('item.egg')
def egg(s):
    d = 'M128 20 C 190 20, 216 110, 216 152 C 216 208, 176 240, 128 240 C 80 240, 40 208, 40 152 C 40 110, 66 20, 128 20Z'
    f = s.RG([(0, '#ffffff'), (.55, '#fbeed6'), (1, '#dcc29a')], cx=.36, cy=.28, r=.9)
    s.shape(d, f, '#8a6a3a', el(128, 250, 90, 36, '#a8885a', .5, 10) + el(92, 86, 16, 30, '#fff', .9, 3, 18), sw=6)


def bottle(s, c, cap, label=None):
    d = 'M100 26 H156 V70 C 156 92, 196 104, 196 140 V214 Q196 240 170 240 H86 Q60 240 60 214 V140 C 60 104, 100 92, 100 70Z'
    s.shape(d, s.LG([(0, lite(c, .5)), (.35, c), (1, dark(c, .28))], x1=0, y1=0, x2=1, y2=0), dark(c, .65),
            el(86, 170, 9, 50, '#fff', .7, 3) + (label or ''), sw=6)
    s.shape(rr(92, 12, 72, 30, 9), s.LG(tri(cap)), dark(cap, .6), pa('M100 20 H156', stroke='#fff', sw=4, op=.6, blur=1), sw=5)


@icon('item.milk')
def milk(s):
    bottle(s, '#f6f6f2', '#4a9ae0', '<rect x="60" y="150" width="136" height="50" fill="#4a9ae0"/>' + el(128, 175, 20, 16, '#fff'))


@icon('item.syrup')
def syrup(s):
    bottle(s, '#e0902a', '#8a4a1c', '<rect x="60" y="150" width="136" height="54" fill="#fff4dc"/>' + pa(star(128, 177, 5, 20, 9), '#e0902a'))


@icon('item.wool')
def wool(s):
    for (x, y, r) in ((70, 150, 46), (186, 150, 46), (98, 92, 46), (160, 90, 46), (128, 188, 50), (128, 132, 54)):
        ball(s, x, y, r, r * .94, '#fbf6ea', '#8a7a66', 5, hl=(x == 98))
    s.add(pa('M96 130 q20 -16 36 0 M132 168 q20 -14 34 2', stroke='#c9bca4', sw=6))


def jar(s, c, lid='#c8843c', fruit=None, lbl='#fff4dc'):
    d = 'M70 76 H186 Q210 82 210 110 V206 Q210 240 176 240 H80 Q46 240 46 206 V110 Q46 82 70 76Z'
    s.shape(d, s.LG([(0, lite(c, .45)), (.4, c), (1, dark(c, .35))], x1=0, y1=0, x2=1, y2=0), dark(c, .68),
            el(74, 160, 9, 54, '#fff', .65, 3) + f'<rect x="78" y="126" width="100" height="74" rx="12" fill="{lbl}" stroke="{dark(c,.5)}" stroke-width="4"/>'
            + (fruit or ''), sw=6)
    s.shape(rr(58, 34, 140, 50, 14), s.LG(tri(lid)), dark(lid, .62), pa('M70 46 H186', stroke='#fff', sw=5, op=.6, blur=1) + pa('M58 66 H198', stroke=dark(lid, .5), sw=5, op=.6), sw=5.5)


def dot(x, y, r, c):
    return f'<circle cx="{x}" cy="{y}" r="{r}" fill="{c}" stroke="{dark(c,.55)}" stroke-width="4"/>' + el(x - r * .3, y - r * .35, r * .25, r * .2, '#fff', .8)


@icon('item.honey')
def honey(s):
    jar(s, '#f2a81e', '#c8843c', pa(star(128, 163, 6, 26, 26, rot=0), '#f2a81e', stroke='#8a5a08', sw=4))


@icon('item.strawberry_jam')
def strawberry_jam(s):
    jar(s, '#e02a44', '#e8483a', dot(128, 166, 22, '#e8262e') + pa('M114 146 L128 152 L142 146', stroke='#2e8a1e', sw=7))


@icon('item.apple_jam')
def apple_jam(s):
    jar(s, '#d9a02a', '#6cc04a', dot(128, 166, 22, '#8fd04a') + pa('M128 146 l6 -10', stroke='#4a2608', sw=5))


@icon('item.pear_jam')
def pear_jam(s):
    jar(s, '#e6c93a', '#f2c230', dot(128, 172, 20, '#f2d23c') + dot(128, 150, 12, '#f2d23c'))


@icon('item.cherry_jam')
def cherry_jam(s):
    jar(s, '#a0142e', '#b83a5e', dot(112, 170, 16, '#c2183a') + dot(146, 170, 16, '#c2183a') + pa('M112 156 Q128 130 146 156', stroke='#2e8a1e', sw=5))


@icon('item.truffle')
def truffle(s):
    d = 'M70 150 C 40 110, 84 50, 132 62 C 170 36, 226 84, 208 134 C 236 180, 190 236, 138 222 C 100 244, 40 208, 70 150Z'
    f = s.RG(tri('#5a4030', .4, .5), cx=.36, cy=.3, r=.9)
    sh = el(128, 248, 90, 36, '#1e120a', .5, 10) + el(96, 100, 14, 10, '#fff', .45, 3, -30)
    for (x, y) in ((104, 140), (150, 108), (168, 160), (126, 184), (86, 178), (186, 120), (136, 142), (112, 104)):
        sh += el(x, y, 9, 6.4, '#8a6a4a', .8) + el(x + 1, y + 2, 6, 4, '#3a2416', .6)
    s.shape(d, f, '#22140a', sh, sw=6)


# ---------------------------------------------------------------- sacks: feeds, flour, sugar
def sack(s, c, band, emblem=''):
    d = 'M84 54 C 60 72, 40 120, 44 190 Q46 240 96 240 H160 Q210 240 212 190 C 216 120, 196 72, 172 54Z'
    s.shape(d, s.LG([(0, lite(c, .4)), (.4, c), (1, dark(c, .3))], x1=0, y1=0, x2=1, y2=0), dark(c, .62),
            el(128, 252, 90, 30, dark(c, .6), .5, 10) + f'<rect x="30" y="126" width="196" height="64" fill="{band}"/>' + el(78, 110, 9, 30, '#fff', .5, 3) + emblem, sw=6)
    s.shape('M78 60 C 70 40, 84 22, 104 30 L128 24 L152 30 C 172 22, 186 40, 178 60Z', s.LG(tri(c, .5, .2)), dark(c, .62), '', sw=5.5)
    s.add(pa('M80 58 H176', stroke='#8a5a24', sw=11) + pa('M80 56 H176', stroke='#d9a85e', sw=5))


def kernels(c):
    return ''.join(el(x, y, 9, 11, c) + el(x - 2, y - 3, 3, 4, '#fff', .6) for (x, y) in ((104, 158), (128, 150), (152, 158), (116, 172), (140, 172)))


@icon('item.chicken_feed')
def chicken_feed(s):
    sack(s, '#e2c58e', '#f2b430', kernels('#fff0a0'))


@icon('item.cow_feed')
def cow_feed(s):
    sack(s, '#e2c58e', '#5fb23a', pa('M128 184 C 100 170, 100 140, 128 132 C 156 140, 156 170, 128 184Z', '#d8ffb0') + pa('M128 180 V140', stroke='#3f8a2c', sw=4))


@icon('item.sheep_feed')
def sheep_feed(s):
    sack(s, '#e2c58e', '#4a9ae0', ''.join(el(x, y, 13, 12, '#ffffff') for (x, y) in ((112, 152), (144, 152), (128, 168), (128, 146))))


@icon('item.pig_feed')
def pig_feed(s):
    sack(s, '#e2c58e', '#f08aa0', el(128, 158, 26, 19, '#ffd2da') + el(119, 158, 5, 8, '#c05a70') + el(137, 158, 5, 8, '#c05a70'))


@icon('item.flour')
def flour(s):
    sack(s, '#fbf6ea', '#d9a85e', pa('M128 186 V134', stroke='#fff8dc', sw=6) + ''.join(el(128 + sg * 11, y, 9, 6, '#fff8dc', rot=sg * -35) for y in (144, 158, 172) for sg in (-1, 1)))


@icon('item.sugar')
def sugar(s):
    def cube(x, y, w):
        f = s.LG([(0, '#ffffff'), (1, '#dfe8f4')])
        s.shape(f'M{x} {y} l{w} {-w*.42} l{w} {w*.42} v{w} l{-w} {w*.42} l{-w} {-w*.42}Z', f, '#6a86a8',
                pa(f'M{x} {y} l{w} {w*.42} l{w} {-w*.42} M{x+w} {y+w*.42} v{w}', stroke='#9ab4d0', sw=5) + pa(f'M{x+w} {y+w*.42} l{w} {-w*.42} v{w} l{-w} {w*.42}Z', '#c2d4ea', op=.8), sw=5)
    cube(84, 76, 46); cube(30, 150, 46); cube(126, 150, 46)


# ---------------------------------------------------------------- baked
def crust(c='#d9953a'):
    return [(0, lite(c, .5)), (.5, c), (1, dark(c, .4))]


@icon('item.bread')
def bread(s):
    d = 'M40 150 C 30 96, 80 62, 128 62 C 176 62, 226 96, 216 150 C 212 196, 180 206, 128 206 C 76 206, 44 196, 40 150Z'
    sh = el(128, 226, 100, 36, '#6a3a0a', .5, 10) + el(84, 100, 22, 12, '#fff', .6, 3, -24)
    for x in (88, 128, 168):
        sh += pa(f'M{x-16} 110 Q{x} 96 {x+16} 132', stroke='#fbe0a8', sw=11) + pa(f'M{x-16} 114 Q{x} 100 {x+16} 136', stroke='#8a4a10', sw=3, op=.6)
    s.shape(d, s.RG(crust(), cx=.36, cy=.25, r=.95), '#5e300a', sh, sw=6)


@icon('item.corn_bread')
def corn_bread(s):
    d = 'M40 110 L128 70 L216 110 V176 L128 220 L40 176Z'
    sh = pa('M40 110 L128 150 L216 110 M128 150 V220', stroke='#a8700a', sw=6) + pa('M40 110 L128 70 L216 110 L128 150Z', '#ffe27a', op=.9)
    for (x, y) in ((78, 150), (100, 178), (156, 172), (184, 150), (128, 110), (96, 104), (160, 106)):
        sh += el(x, y, 7, 6, '#fff3b0') + el(x + 1, y + 2, 5, 4, '#d9960a', .5)
    s.shape(d, s.LG(crust('#f2b93a')), '#6a4208', sh, sw=6)


def pie(s, top, rim='#e2a850', deco=''):
    s.shape('M30 150 H226 L200 214 Q128 236 56 214Z', s.LG(tri('#c9d2dc', .5, .35), x1=0, y1=0, x2=1, y2=0), '#3e4a58', pa('M44 160 H212', stroke='#fff', sw=5, op=.6, blur=1), sw=6)
    f = s.RG(tri(top, .45, .3), cx=.4, cy=.3, r=.8)
    s.shape('<ellipse {ID} cx="128" cy="140" rx="104" ry="56"/>', s.LG(crust(rim)), '#5e300a', '', sw=6)
    s.shape('<ellipse {ID} cx="128" cy="134" rx="84" ry="42"/>', f, dark(top, .55), deco + el(92, 114, 20, 9, '#fff', .55, 3, -16), sw=4.5)
    for i in range(12):
        a = math.pi * 2 * i / 12
        s.add(el(128 + math.cos(a) * 95, 139 + math.sin(a) * 50, 9, 7, '#f6cc80', .9))


@icon('item.apple_pie')
def apple_pie(s):
    lat = ''.join(pa(f'M{60+i*34} 96 L{30+i*34} 176', stroke='#f2c070', sw=13) for i in range(1, 6)) + ''.join(pa(f'M{26+i*34} 96 L{64+i*34} 176', stroke='#e2a850', sw=13) for i in range(5))
    pie(s, '#c8452f', deco=lat)
    s.add(dot(128, 70, 26, '#e23a2e') + pa('M128 46 l6 -12', stroke='#4a2608', sw=6))


@icon('item.pumpkin_pie')
def pumpkin_pie(s):
    pie(s, '#e8862a')
    for (x, y, r) in ((112, 122, 20), (144, 122, 20), (128, 106, 20), (128, 132, 18)):
        ball(s, x, y, r, r * .8, '#fffaf0', '#b09a7a', 4, hl=False)


@icon('item.cookies')
def cookies(s):
    for (x, y, r) in ((164, 100, 66), (96, 160, 74)):
        ball(s, x, y, r, r * .92, '#d9953a', '#5e300a', 6)
        for (dx, dy) in ((-.4, -.2), (.3, -.35), (.45, .25), (-.1, .4), (-.5, .35), (.05, 0)):
            s.add(el(x + dx * r, y + dy * r, r * .12, r * .1, '#4a240a') + el(x + dx * r - 2, y + dy * r - 2, r * .04, r * .035, '#b07a4a'))


@icon('item.honey_cake')
def honey_cake(s):
    d = 'M36 196 V120 L128 84 L220 120 V196 L128 232Z'
    sh = ''
    for i, y in enumerate((142, 166, 190)):
        sh += pa(f'M36 {y} L128 {y+36} L220 {y}', stroke='#fff3d0', sw=9) + pa(f'M36 {y+10} L128 {y+46} L220 {y+10}', stroke='#b8742a', sw=4, op=.6)
    s.shape(d, s.LG(crust('#d9953a')), '#5e300a', sh, sw=6)
    s.shape('M36 120 L128 84 L220 120 L128 156Z', s.LG(tri('#f2a81e', .5, .2)), '#7a4a08',
            pa('M60 122 L128 96 L196 122', stroke='#fff', sw=6, op=.55, blur=2), sw=5.5)
    s.add(pa('M70 132 v22 q0 10 10 8 v-22 M180 130 v30 q0 12 12 8 v-32', '#f2a81e', stroke='#7a4a08', sw=4))
    s.add(dot(128, 110, 15, '#e23a2e'))


# ---------------------------------------------------------------- dairy
@icon('item.cream')
def cream(s):
    s.add(pa('M196 130 q46 4 40 44 q-6 34 -48 30', stroke='#3e6a9c', sw=22) + pa('M196 130 q46 4 40 44 q-6 34 -48 30', stroke='#8fc0ee', sw=10))
    s.shape('M40 116 H206 L190 222 Q128 246 56 222Z', s.LG([(0, '#cfe6fb'), (.4, '#8fc0ee'), (1, '#5a90cc')], x1=0, y1=0, x2=1, y2=0), '#2f5688', el(72, 170, 8, 38, '#fff', .6, 3), sw=6)
    for (x, y, r) in ((84, 104, 34), (170, 104, 34), (128, 92, 40), (128, 62, 28), (128, 40, 16)):
        ball(s, x, y, r, r * .86, '#fffaf0', '#b09a7a', 5, hl=(x == 84))


@icon('item.butter')
def butter(s):
    s.shape('<ellipse {ID} cx="128" cy="196" rx="112" ry="34"/>', s.LG(tri('#dfe8f2', .5, .3)), '#4a5a6e', '', sw=6)
    d = 'M40 124 L150 96 L220 122 V170 L112 202 L40 172Z'
    sh = pa('M40 124 L112 152 L220 122 M112 152 V202', stroke='#c89a10', sw=6) + pa('M40 124 L150 96 L220 122 L112 152Z', '#fff0a0', op=.95) + el(100, 124, 26, 8, '#fff', .7, 3, -12)
    s.shape(d, s.LG(tri('#f6cf3a', .4, .25)), '#7a5a08', sh, sw=6)


@icon('item.cheese')
def cheese(s):
    d = 'M24 150 L196 70 Q236 96 236 140 V198 L24 222Z'
    sh = pa('M24 150 L236 140', stroke='#c88a10', sw=6) + pa('M24 150 L196 70 Q236 96 236 140Z', '#ffe27a', op=.95) + el(150, 108, 34, 9, '#fff', .6, 3, -24)
    for (x, y, r) in ((84, 186, 16), (150, 176, 20), (200, 184, 12), (118, 196, 8)):
        sh += el(x, y, r, r * .85, '#c88a10', .9) + el(x + 2, y + 3, r * .8, r * .68, '#e8a820')
    s.shape(d, s.LG(tri('#f6b93a', .4, .25)), '#7a4a08', sh, sw=6)


# ---------------------------------------------------------------- textile, sweets
@icon('item.fabric')
def fabric(s):
    c = '#4a9ae0'
    s.shape('M60 70 H196 V214 Q128 236 60 214Z', s.LG([(0, lite(c, .4)), (.5, c), (1, dark(c, .3))], x1=0, y1=0, x2=1, y2=0), dark(c, .65),
            ''.join(pa(f'M60 {y} Q128 {y+18} 196 {y}', stroke='#cfe6fb', sw=5, op=.6) for y in (110, 150, 190)), sw=6)
    s.shape('<ellipse {ID} cx="128" cy="70" rx="68" ry="26"/>', s.LG(tri(c, .55, .1)), dark(c, .65), '', sw=6)
    s.add(el(128, 70, 26, 10, '#f6e2b6') + el(128, 71, 12, 4.6, '#8a6a44'))
    s.shape('M196 150 L236 170 L226 222 L196 214Z', s.LG(tri(c, .3, .3)), dark(c, .65), '', sw=5)


@icon('item.yarn')
def yarn(s):
    s.add(pa('M60 60 L196 206', stroke='#6a4226', sw=17) + pa('M60 60 L196 206', stroke='#e2b06a', sw=8) + pa('M200 56 L64 210', stroke='#6a4226', sw=17) + pa('M200 56 L64 210', stroke='#e2b06a', sw=8))
    c = '#f06a9a'
    lines = ''.join(pa(f'M{40+i*14} 150 Q128 {60+i*30} {216-i*10} {120+i*12}', stroke=dark(c, .3), sw=5, op=.7) for i in range(6)) \
        + ''.join(pa(f'M{70+i*18} 70 Q{100+i*10} 150 {80+i*20} 230', stroke=lite(c, .4), sw=5, op=.7) for i in range(5))
    ball(s, 128, 142, 88, 86, c, '#7a1a44', 6, extra=lines)
    s.add(pa('M204 190 q30 10 20 36 q-10 20 -36 10', stroke='#7a1a44', sw=14) + pa('M204 190 q30 10 20 36 q-10 20 -36 10', stroke=c, sw=6))


@icon('item.sweater')
def sweater(s):
    c = '#e8483a'
    d = 'M92 40 Q128 66 164 40 L236 84 L212 150 L186 138 V226 H70 V138 L44 150 L20 84Z'
    sh = pa('M92 40 Q128 76 164 40', stroke='#fff4dc', sw=13) + ''.join(pa(f'M70 {y} H186', stroke='#fff4dc', sw=8, op=.9) for y in (118, 142)) \
        + pa('M70 214 H186', stroke=dark(c, .3), sw=13) + el(86, 80, 12, 20, '#fff', .45, 4, 30)
    s.shape(d, s.LG(tri(c, .35, .3)), '#6e1408', sh, sw=6)


@icon('item.candy')
def candy(s):
    c = '#f06a9a'
    for sg in (-1, 1):
        x = 128 + sg * 62
        s.shape(f'M{x} 128 L{x+sg*56} 84 L{x+sg*44} 128 L{x+sg*56} 172Z', s.LG(tri('#ffd23a', .4, .25)), '#8a5a00', '', sw=5.5)
    sw = ''.join(pa(f'M{84+i*22} 60 Q{120+i*22} 128 {84+i*22} 196', stroke='#ffffff', sw=11, op=.9) for i in range(5))
    ball(s, 128, 128, 70, 62, c, '#7a1a44', 6, extra=sw)


# ---------------------------------------------------------------- tools
WOODG = [(0, '#f0b468'), (.5, '#c47c34'), (1, '#8c4c18')]
STEELG = [(0, '#6f7b88'), (.22, '#ffffff'), (.55, '#b4c0cc'), (1, '#5e6a77')]


def handle(s, d, w=30):
    s.add(pa(d, stroke='#4a2608', sw=w + 12) + pa(d, stroke=s.LG(WOODG, x1=0, y1=0, x2=1, y2=1), sw=w) + pa(d, stroke='#ffe2b0', sw=5, op=.55, tr='translate(-7 -5)'))


@icon('tool.hammer')
def hammer(s):
    handle(s, 'M70 226 L150 110')
    s.shape('M96 60 L176 30 Q204 40 214 70 L232 112 L196 128 L182 100 L128 122Z', s.LG(STEELG, x1=0, y1=0, x2=1, y2=1), '#28303a', pa('M108 64 L176 40', stroke='#fff', sw=6, op=.8, blur=1), sw=6)


@icon('tool.nails')
def nails(s):
    for (x, rot) in ((84, -16), (128, 0), (172, 16)):
        g = f'rotate({rot} {x} 130)'
        s.shape(f'M{x-11} 70 H{x+11} L{x+5} 222 L{x} 240 L{x-5} 222Z', s.LG(STEELG, x1=0, y1=0, x2=1, y2=0), '#28303a', '', tr=g, sw=5)
        s.shape(f'<ellipse {{ID}} cx="{x}" cy="66" rx="30" ry="13"/>', s.LG(STEELG), '#28303a', el(x - 8, 62, 10, 3, '#fff', .8), tr=g, sw=5)


@icon('tool.paint')
def paint(s):
    s.add(pa('M48 110 Q128 20 208 110', stroke='#28303a', sw=15) + pa('M48 110 Q128 20 208 110', stroke='#b4c0cc', sw=6))
    s.shape('M44 104 H212 L196 220 Q128 242 60 220Z', s.LG(STEELG, x1=0, y1=0, x2=1, y2=0), '#28303a', '<rect x="40" y="140" width="180" height="46" fill="#e8483a"/>' + el(76, 160, 8, 40, '#fff', .5, 3), sw=6)
    s.shape('<ellipse {ID} cx="128" cy="104" rx="84" ry="26"/>', s.LG(tri('#e8483a', .4, .2)), '#6e1408', el(100, 98, 30, 6, '#fff', .5, 2), sw=6)
    s.add(pa('M176 112 q6 40 -4 52 q-14 8 -14 -10 q2 -26 -6 -40', '#e8483a', stroke='#6e1408', sw=5))


@icon('tool.saw')
def saw(s):
    teeth = ' '.join(f'L{60+i*17} {214-i*8.2} L{68+i*17} {198-i*8.2}' for i in range(9))
    s.shape(f'M40 150 L196 74 L214 110 {teeth[::-1] and ""} L214 110 ' + ' '.join(f'L{206-i*18} {122+i*9} L{196-i*18} {112+i*9}' for i in range(9)) + ' L40 188Z',
            s.LG(STEELG, x1=0, y1=0, x2=0, y2=1), '#28303a', pa('M52 150 L192 84', stroke='#fff', sw=6, op=.7, blur=1), sw=5.5)
    s.shape('M176 40 Q236 30 240 86 Q240 132 200 136 L180 96 Q206 92 204 74 Q200 60 184 70Z', s.LG(WOODG, x1=0, y1=0, x2=1, y2=1), '#4a2608', el(214, 60, 12, 7, '#ffe2b0', .6, 2), sw=6)


@icon('tool.axe')
def axe(s):
    handle(s, 'M64 232 L164 60')
    s.shape('M112 44 L196 30 Q238 66 226 132 Q190 116 150 132 L132 96Z', s.LG(STEELG, x1=0, y1=0, x2=1, y2=1), '#28303a',
            pa('M124 50 L192 40', stroke='#fff', sw=6, op=.8, blur=1) + pa('M222 60 Q232 96 222 126', stroke='#fff', sw=7, op=.8, blur=1), sw=6)


# ---------------------------------------------------------------- animals: faces, as the HUD's hen
def eyes(s, x1, x2, y, r=11):
    for x in (x1, x2):
        s.add(el(x, y, r, r * 1.15, '#2a1608') + el(x - r * .3, y - r * .4, r * .36, r * .36, '#fff'))


def cheeks(s, x1, x2, y):
    s.add(el(x1, y, 16, 10, '#ff9aa8', .7) + el(x2, y, 16, 10, '#ff9aa8', .7))


@icon('animal.chicken')
def a_chicken(s):
    s.shape('M92 62 q8 -40 30 -18 q10 -34 30 -4 q22 -22 22 22Z', s.LG(tri('#e8362e', .4, .3)), '#6e1408', '', sw=5.5)
    ball(s, 128, 148, 96, 90, '#fbf6ea', '#6a4a2a', 6)
    eyes(s, 96, 160, 130)
    cheeks(s, 74, 182, 158)
    s.shape('M106 150 H150 L128 184Z', s.LG(tri('#f6a81e', .4, .2)), '#7a4a08', '', sw=5)
    s.shape('M116 180 q-2 26 12 26 q14 0 12 -26Z', s.LG(tri('#e8362e', .4, .3)), '#6e1408', '', sw=4.5)


@icon('animal.cow')
def a_cow(s):
    for sg in (-1, 1):
        x = 128 + sg * 74
        s.add(pa(f'M{x} 78 q{sg*18} -30 {sg*4} -52', stroke='#6a5a3a', sw=22) + pa(f'M{x} 78 q{sg*18} -30 {sg*4} -52', stroke='#f2e2b6', sw=11))
        s.shape(f'<ellipse {{ID}} cx="{128+sg*96}" cy="104" rx="34" ry="20" transform="rotate({sg*22} {128+sg*96} 104)"/>', s.LG(tri('#f4efe6' if sg > 0 else '#6a4630', .3, .3)), '#4a2a14', '', sw=5.5)
    s.shape(rr(44, 56, 168, 170, 70), s.RG([(0, '#ffffff'), (.6, '#f4efe6'), (1, '#d2c8b8')], cx=.36, cy=.28, r=.9), '#4a2a14',
            '<ellipse cx="172" cy="98" rx="42" ry="46" fill="#6a4630"/>' + el(86, 86, 18, 12, '#fff', .8, 3, -30), sw=6)
    s.shape('<ellipse {ID} cx="128" cy="196" rx="70" ry="44"/>', s.LG(tri('#ffc2ca', .4, .25)), '#8a3a4a', el(104, 178, 16, 8, '#fff', .6, 3), sw=6)
    s.add(el(104, 198, 9, 13, '#b06a78') + el(152, 198, 9, 13, '#b06a78'))
    eyes(s, 92, 164, 124)


@icon('animal.sheep')
def a_sheep(s):
    for (x, y, r) in ((60, 120, 44), (196, 120, 44), (84, 70, 46), (172, 70, 46), (128, 54, 46), (64, 176, 40), (192, 176, 40)):
        ball(s, x, y, r, r * .94, '#fbf6ea', '#8a7a66', 5, hl=(x == 84))
    for sg in (-1, 1):
        s.shape(f'<ellipse {{ID}} cx="{128+sg*70}" cy="138" rx="30" ry="15" transform="rotate({sg*-28} {128+sg*70} 138)"/>', s.LG(tri('#4a3830', .3, .3)), '#22140c', '', sw=5)
    s.shape(rr(70, 96, 116, 136, 54), s.RG(tri('#5a463c', .35, .4), cx=.36, cy=.28, r=.9), '#22140c', el(98, 122, 14, 10, '#fff', .35, 3, -30), sw=6)
    for x in (100, 156):
        s.add(el(x, 152, 14, 16, '#fff') + el(x, 155, 7, 9, '#1a0e08') + el(x - 2, 151, 2.6, 2.6, '#fff'))
    s.add(el(128, 200, 13, 8, '#ffb0b8') + pa('M128 208 V216', stroke='#22140c', sw=4))
    for (x, r) in ((100, 26), (128, 30), (156, 26)):
        ball(s, x, 94, r, r * .9, '#fbf6ea', '#8a7a66', 4.5, hl=False)


@icon('animal.pig')
def a_pig(s):
    for sg in (-1, 1):
        x = 128 + sg * 70
        s.shape(f'M{x-sg*30} 84 L{x+sg*30} 30 L{x+sg*34} 100Z', s.LG(tri('#f09aa8', .3, .25)), '#8a3a4a', '', sw=6)
    ball(s, 128, 144, 100, 92, '#f6a4b0', '#8a3a4a', 6)
    s.shape('<ellipse {ID} cx="128" cy="170" rx="50" ry="38"/>', s.LG(tri('#ffc2ca', .4, .2)), '#8a3a4a', el(110, 154, 14, 7, '#fff', .6, 3), sw=5.5)
    s.add(el(110, 172, 9, 14, '#8a3a4a') + el(146, 172, 9, 14, '#8a3a4a'))
    eyes(s, 86, 170, 118)
    cheeks(s, 62, 194, 152)


@icon('animal.bee')
def a_bee(s):
    for sg in (-1, 1):
        s.shape(f'<ellipse {{ID}} cx="{128+sg*52}" cy="70" rx="54" ry="34" transform="rotate({sg*-28} {128+sg*52} 70)"/>', s.LG([(0, '#ffffff'), (1, '#cfe6fb')]), '#5a7a9c', el(128 + sg * 60, 60, 16, 7, '#fff', .8, 2), sw=5)
    s.add(pa('M104 60 q-10 -34 -30 -36 M152 60 q10 -34 30 -36', stroke='#3a2408', sw=8) + el(72, 24, 10, 10, '#3a2408') + el(184, 24, 10, 10, '#3a2408'))
    f = s.RG(tri('#f6c22e', .5, .3), cx=.36, cy=.28, r=.9)
    stripes = pa('M40 150 H216', stroke='#3a2408', sw=26) + pa('M56 204 H200', stroke='#3a2408', sw=24) + el(92, 96, 18, 12, '#fff', .8, 3, -30)
    s.shape('<ellipse {ID} cx="128" cy="150" rx="88" ry="90"/>', f, '#3a2408', stripes, sw=6)
    eyes(s, 100, 156, 108, 10)
    cheeks(s, 78, 178, 126)
    s.add(pa('M114 122 q14 12 28 0', stroke='#3a2408', sw=5))


SHEET = os.path.join(HERE, 'Icons', '_farm_sheet.png')

if __name__ == '__main__':
    only = sys.argv[1:]
    for name, fn in ICONS.items():
        if only and name not in only:
            continue
        s = S(); fn(s); build('Icons', name, s, pad=6, shadow=True)
        shutil.copy(os.path.join(HERE, 'Icons', name + '.png'), os.path.join(RES, name + '.png'))
    names = list(ICONS)
    cols, size = 10, 128
    rows = (len(names) + cols - 1) // cols
    im = Image.new('RGBA', (cols * size, rows * size), (233, 205, 160, 255))
    for i, n in enumerate(names):
        ic = Image.open(os.path.join(HERE, 'Icons', n + '.png')).convert('RGBA').resize((size - 12, size - 12), Image.LANCZOS)
        im.alpha_composite(ic, (i % cols * size + 6, i // cols * size + 6))
    im.save(SHEET)
