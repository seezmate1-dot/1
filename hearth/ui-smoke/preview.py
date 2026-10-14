"""Renders the draw calls recorded by `dotnet run --project hearth/ui-smoke 6000 calls.txt` into a PNG,
to look at the cutaway without Unity: python hearth/ui-smoke/preview.py calls.txt out.png"""
import os
import re
import sys

from PIL import Image, ImageDraw, ImageFont

ART = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "unity", "LastHearth", "Assets", "LastHearth", "Resources")


def main(calls, out, scale=2):
    lines = []
    for l in open(calls, encoding="utf-8"):
        l = l.rstrip("\n")
        if l:
            lines.append(l.split("|") if l.startswith("T|") else l.split("|", 4))
    tex = [l for l in lines if l[0] == "T"]
    w = int(max(float(t[2]) + float(t[4]) for t in tex) * scale) + 4
    h = int(max(float(t[3]) + float(t[5]) for t in tex) * scale) + 4
    img = Image.new("RGBA", (w, h), (20, 18, 22, 255))
    try:
        font = ImageFont.truetype("DejaVuSans-Bold.ttf", 12 * scale)
    except OSError:
        font = ImageFont.load_default()
    d = ImageDraw.Draw(img)
    for l in lines:
        if l[0] == "T":
            name = l[1]
            x, y, rw, rh = (float(v) * scale for v in l[2:6])
            r, g, b, a = (float(v) for v in l[6:10])
            if name == "white":
                src = Image.new("RGBA", (1, 1), (255, 255, 255, 255))
            else:
                src = Image.open(os.path.join(ART, name + ".png")).convert("RGBA")
            src = src.resize((max(1, int(rw)), max(1, int(rh))))
            px = src.load()
            for yy in range(src.height):
                for xx in range(src.width):
                    p = px[xx, yy]
                    px[xx, yy] = (int(p[0] * r), int(p[1] * g), int(p[2] * b), int(p[3] * a))
            img.alpha_composite(src, (int(x), int(y)))
        else:
            x, y, shade, text = float(l[1]) * scale, float(l[2]) * scale, float(l[3]), l[4]
            text = re.sub(r"<[^>]*>", "", text).replace("\\n", "\n")
            d.multiline_text((x, y), text, spacing=2, fill=(0, 0, 0, 255) if shade < 0.5 else (255, 240, 220, 255), font=font)
    img.save(out)


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
