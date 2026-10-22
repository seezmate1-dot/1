"""Draws the placeholder app icon of «Солнечная долина»: a sun over green hills and a little red barn.

    python3 farm/tools/icon.py

Writes farm/unity/SunnyValley/Assets/SunnyValley/Icon.png (1024×1024, needs Pillow).
"""
import math
import os

from PIL import Image, ImageDraw, ImageFilter

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "unity", "SunnyValley", "Assets", "SunnyValley", "Icon.png")
S = 1024


def main():
    img = Image.new("RGB", (S, S))
    d = ImageDraw.Draw(img)
    # sky: warm at the horizon, blue above
    for y in range(S):
        t = y / S
        d.line([(0, y), (S, y)], fill=(int(120 + 130 * t), int(200 + 40 * t), int(245 - 60 * t)))
    # the sun with rays
    cx, cy, r = 600, 360, 170
    rays = []
    for i in range(28):
        a = i * math.pi / 14
        rr = r + (70 if i % 2 == 0 else 30)
        rays.append((cx + rr * math.cos(a), cy + rr * math.sin(a)))
    d.polygon(rays, fill=(255, 178, 46))
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(255, 224, 102))
    d.ellipse([cx - 90, cy - 100, cx - 10, cy - 50], fill=(255, 240, 180))
    # hills
    d.ellipse([-300, 560, 700, 1400], fill=(108, 192, 74))
    d.ellipse([350, 620, 1350, 1400], fill=(134, 201, 90))
    d.ellipse([-100, 760, 1150, 1500], fill=(93, 170, 60))
    # the barn
    bx, by = 250, 640
    d.polygon([(bx, by + 60), (bx + 130, by - 40), (bx + 260, by + 60)], fill=(142, 46, 36))
    d.rectangle([bx + 20, by + 60, bx + 240, by + 250], fill=(200, 69, 58))
    d.rectangle([bx + 85, by + 130, bx + 175, by + 250], fill=(246, 227, 188))
    d.line([(bx + 85, by + 130), (bx + 175, by + 250)], fill=(200, 69, 58), width=12)
    d.line([(bx + 175, by + 130), (bx + 85, by + 250)], fill=(200, 69, 58), width=12)
    # a soft vignette
    mask = Image.new("L", (S, S), 0)
    ImageDraw.Draw(mask).ellipse([-200, -200, S + 200, S + 200], fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(120))
    dark = Image.new("RGB", (S, S), (60, 90, 40))
    img = Image.composite(img, dark, mask)
    img.save(OUT)
    print("->", os.path.normpath(OUT))


if __name__ == "__main__":
    main()
