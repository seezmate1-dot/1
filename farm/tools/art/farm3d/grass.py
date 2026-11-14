"""Painted grass detail over a background's flat grass: thousands of short blade strokes, each a little lighter or
darker than the paint under it, only where the picture is plain grass (not on objects, paths or edges).
python grass.py in out [density]"""
import sys, math, numpy as np
from PIL import Image, ImageDraw, ImageFilter
from scipy.ndimage import uniform_filter, binary_erosion
from skimage.color import rgb2hsv
src, out = sys.argv[1], sys.argv[2]
dens = float(sys.argv[3]) if len(sys.argv) > 3 else 1.0
im = Image.open(src).convert("RGB"); a = np.asarray(im).astype(np.float32)
H, W, _ = a.shape
hsv = rgb2hsv(a / 255)
lum = a.mean(2)
m = uniform_filter(lum, 9); sd = np.sqrt(np.maximum(uniform_filter(lum ** 2, 9) - m ** 2, 0))
grass = (hsv[..., 0] > 0.13) & (hsv[..., 0] < 0.42) & (hsv[..., 1] > 0.28) & (sd < 14)
grass = binary_erosion(grass, iterations=3)
rng = np.random.default_rng(1)
n = int(grass.sum() / 26 * dens)
ys, xs = np.nonzero(grass)
idx = rng.choice(len(xs), size=min(n, len(xs)), replace=False)
layer = Image.new("RGBA", (W, H), (0, 0, 0, 0)); d = ImageDraw.Draw(layer)
for i in idx:
    x, y = int(xs[i]), int(ys[i]); c = a[y, x]
    dark = c.mean() < 70
    if rng.random() < 0.5:     # a lit blade: lighter and a touch more yellow
        f = rng.uniform(1.08, 1.2); col = np.clip(c * f + np.array([5, 3, -3]), 0, 255)
    else:
        f = rng.uniform(0.8, 0.9); col = np.clip(c * f, 0, 255)
    if dark: col = np.clip(c * rng.uniform(0.85, 1.25), 0, 255)
    L = rng.uniform(5, 12); ang = math.radians(-90 + rng.uniform(-32, 32)); bend = rng.uniform(-0.5, 0.5)
    x1, y1 = x + math.cos(ang) * L * 0.55, y + math.sin(ang) * L * 0.55
    x2, y2 = x1 + math.cos(ang + bend) * L * 0.45, y1 + math.sin(ang + bend) * L * 0.45
    al = int(rng.uniform(80, 150))
    d.line([(x, y), (x1, y1), (x2, y2)], fill=(int(col[0]), int(col[1]), int(col[2]), al), width=2, joint="curve")
layer = layer.filter(ImageFilter.GaussianBlur(0.35))
res = im.convert("RGBA"); res.alpha_composite(layer)
res.convert("RGB").save(out, quality=93)
print(out, len(idx), "strokes")
