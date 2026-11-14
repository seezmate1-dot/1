"""Shows sprites as the game does: on the Orchard background at one pixel per map unit (and x2, a phone's density)."""
import sys
from PIL import Image
BG = "/home/user/1/farm/unity/SunnyValley/Assets/SunnyValley/Resources/Art/Ch1/Backgrounds/Orchard.jpg"
out, fs = sys.argv[1], sys.argv[2:]
bg = Image.open(BG).convert("RGBA")
for k, y in ((1.0, 0), (2.0, 1)):
    pass
W = 255  # tree canvas in map units
row = []
for k in (1.0, 2.0):
    w = int(W * k); h = int(263.5 * k)
    strip = bg.resize((bg.width * 2, bg.height * 2)).crop((300, 900, 300 + int(w * 0.8 * len(fs)) + 80, 900 + h)).copy()
    for i, f in enumerate(fs):
        im = Image.open(f).convert("RGBA").resize((w, h), Image.LANCZOS)
        strip.alpha_composite(im, (20 + int(i * w * 0.8), 0))
    row.append(strip)
sh = Image.new("RGB", (max(r.width for r in row), sum(r.height for r in row)))
y = 0
for r in row: sh.paste(r, (0, y)); y += r.height
sh.save(out)
