"""Builds compare.png: old (left) vs new (right) for every sprite + 64px readability rows."""
import os
from PIL import Image, ImageDraw, ImageFont
HERE = os.path.dirname(os.path.abspath(__file__))
OLD = os.path.join(HERE, '..', '..', 'unity', 'SunnyValley', 'Assets', 'SunnyValley', 'Resources', 'Art')
GROUPS = [('Tiles', ['Apple', 'Blueberry', 'Carrot', 'Corn', 'Eggplant', 'Tomato']),
          ('Specials', ['Bee', 'Bomb', 'Pitchfork', 'Rainbow']),
          ('Boosters', ['Rake', 'Shovel', 'Shuffle', 'WateringCan']),
          ('Obstacles', ['Basket', 'Bed', 'Chain', 'Chick', 'Crate1', 'Crate2', 'Crate3', 'Honey', 'Ice', 'Stone', 'Weed'])]
BG, PANEL = (138, 143, 150), (122, 127, 134)
def font(sz):
    for p in ('/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf', '/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf'):
        if os.path.exists(p): return ImageFont.truetype(p, sz)
    return ImageFont.load_default()
F, FS = font(16), font(13)
SZ, COLS, CW, CH = 168, 4, 2 * 168 + 28, 168 + 44
rows = []
for g, names in GROUPS:
    for i in range(0, len(names), COLS): rows.append((g, names[i:i + COLS]))
W = COLS * CW + 20
H = len(rows) * CH + 20 + 2 * 130
im = Image.new('RGBA', (W, H), BG + (255,)); d = ImageDraw.Draw(im)
load = lambda base, g, n: Image.open(os.path.join(base, g, n + '.png')).convert('RGBA')
for r, (g, names) in enumerate(rows):
    for c, n in enumerate(names):
        x, y = 10 + c * CW, 10 + r * CH
        d.rounded_rectangle([x + 4, y + 4, x + CW - 6, y + CH - 6], 10, fill=PANEL)
        d.text((x + 12, y + 8), f'{g}/{n}', font=F, fill=(255, 255, 255))
        d.text((x + 12, y + CH - 26), 'old', font=FS, fill=(225, 225, 225)); d.text((x + 12 + SZ + 8, y + CH - 26), 'new (fable)', font=FS, fill=(255, 240, 170))
        for k, base in enumerate((OLD, HERE)):
            s = load(base, g, n).resize((SZ, SZ), Image.LANCZOS)
            im.alpha_composite(s, (x + 10 + k * (SZ + 8), y + 26))
# 64px rows on a board-like background
y0 = len(rows) * CH + 20
for k, (lab, base) in enumerate((('new tiles @64px', HERE), ('old tiles @64px', OLD))):
    y = y0 + k * 130
    d.text((14, y), lab, font=F, fill=(255, 255, 255))
    for j, (bg, x0) in enumerate((((233, 205, 160), 14), ((92, 132, 84), 14 + 6 * 72 + 30), (BG, 14 + 12 * 72 + 60))):
        if x0 + 6 * 72 > W: continue
        d.rounded_rectangle([x0, y + 24, x0 + 6 * 72, y + 24 + 76], 8, fill=bg)
        for i, n in enumerate(GROUPS[0][1]):
            im.alpha_composite(load(base, 'Tiles', n).resize((64, 64), Image.LANCZOS), (x0 + 4 + i * 72, y + 30))
im.convert('RGB').save(os.path.join(HERE, 'compare.png'))
print(im.size)
