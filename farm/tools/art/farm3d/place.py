"""Puts a render into the game's canvas: the same canvas size as the sprite it replaces, the foot on the game's foot
point, the height of the old picture. python place.py render.png TargetName kind [height_scale]"""
import sys, json, os
import numpy as np
from PIL import Image
FARM = "/home/user/1/farm/unity/SunnyValley/Assets/SunnyValley/Resources/Art/Farm/"
# canvas in map units and the foot (from the left, from the bottom): FarmSitesView.cs
KUWA = int(os.environ.get("KUWA", "4"))
FOOT = {"tree": (255, 263.5, 95.2, 52.7), "bush": None}

def grade(px):
    """The zone backgrounds' greens: warm yellow-greens with deep olive shadows. Greens turn toward yellow, get
    more saturated and brighter in the lights; other hues (fruit, bark, petals) are left alone."""
    from skimage.color import rgb2hsv as rgb_to_hsv, hsv2rgb as hsv_to_rgb
    rgb = px[..., :3] / 255.0
    hsv = rgb_to_hsv(np.clip(rgb, 0, 1))
    h, s_, v = hsv[..., 0], hsv[..., 1], hsv[..., 2]
    g = np.clip(1 - np.abs(h - 0.27) / 0.12, 0, 1)            # how green
    h2 = h - 0.045 * g * (0.4 + 0.6 * v)                        # lights turn more yellow than shadows
    s2 = np.clip(s_ * (1 + 0.18 * g), 0, 1)
    v2 = np.clip(v ** (1 - 0.12 * g) * (1 + 0.05 * g), 0, 1)
    out = hsv_to_rgb(np.dstack([h2 % 1.0, s2, v2])) * 255
    res = px.copy(); res[..., :3] = out
    return res

def kuwahara(rgb, r):
    """Painterly smoothing: each pixel takes the mean of the least varied of its four corner windows — clumps of
    colour with crisp edges between them, like the painted zone backgrounds."""
    from scipy.ndimage import uniform_filter
    k = r + 1
    mean = np.stack([uniform_filter(rgb[..., c], k) for c in range(3)], -1)
    sq = np.stack([uniform_filter(rgb[..., c] ** 2, k) for c in range(3)], -1)
    var = (sq - mean ** 2).sum(-1)
    H, W = var.shape; best = np.full((H, W), np.inf); out = np.zeros_like(rgb)
    o = r // 2
    for dy in (-o, o):
        for dx in (-o, o):
            v = np.roll(var, (dy, dx), (0, 1)); m = np.roll(mean, (dy, dx), (0, 1))
            sel = v < best; best[sel] = v[sel]; out[sel] = m[sel]
    return out

def ground_shadow(alpha, foot, size):
    """The backgrounds' shadow: the silhouette laid on the ground away from the light (down and to the right),
    squashed, deep olive, soft-edged."""
    from PIL import ImageFilter
    a = Image.fromarray(alpha.astype(np.uint8))
    fx, fy = foot
    # flatten onto the ground: y' = fy + (y - fy) * -0.42 (mirror below the foot), x' = x + (fy - y) * 0.55
    q, t = 0.24, 0.32          # squash and lean: the shadow lies close under the crown, a little to the right
    k = t / q
    sh = a.transform(a.size, Image.AFFINE, (1, -k, k * fy, 0, -1 / q, fy * (1 + 1 / q)), resample=Image.BILINEAR)
    from PIL import ImageDraw
    d = ImageDraw.Draw(sh)
    ys, xs = np.nonzero(alpha > 200); w = (xs.max() - xs.min()) * 0.32
    d.ellipse((fx - w * 0.55, fy - w * 0.16, fx + w * 0.75, fy + w * 0.2), fill=255)
    sh = sh.filter(ImageFilter.GaussianBlur(size * 0.006))
    return np.asarray(sh).astype(np.float32) / 255.0

def place(src, name, kind, hk=1.0, dst=None):
    old = Image.open(FARM + name + ".png"); W, H = old.size
    oa = np.asarray(old)[..., 3]
    if FOOT.get(kind):
        cw, ch, fx, fy = FOOT[kind]; foot = (fx / cw * W, H - fy / ch * H)
    else:  # bottom centre of the old picture's solid part
        ys, xs = np.nonzero(oa > 200); foot = ((xs.min() + xs.max()) / 2, ys.max())
    ys, xs = np.nonzero(oa > 200); old_top = ys.min(); old_l, old_r = xs.min(), xs.max()
    im = Image.open(src).convert("RGBA")
    px = np.asarray(im).astype(np.float32)
    sh = px[..., :3].max(axis=2) < 14            # the shadow catcher's pixels: black, translucent
    px[..., 3] = np.where(sh, 0, px[..., 3])          # the catcher's shadow is replaced by the painted one
    obj = px[..., 3] > 0
    if KUWA:
        from scipy.ndimage import gaussian_filter
        kw = kuwahara(px[..., :3], KUWA)
        kw = np.stack([gaussian_filter(kw[..., c], 1.1) for c in range(3)], -1)
        px[..., :3] = np.where(obj[..., None], kw, px[..., :3])
    px = grade(px)
    info0 = json.load(open(os.path.splitext(src)[0] + ".json"))
    shd = ground_shadow(px[..., 3], info0["foot"], px.shape[0]) * 0.72
    a_obj = px[..., 3] / 255.0
    col = np.array([31, 59, 16], np.float32)
    a_out = a_obj + shd * (1 - a_obj)
    rgb = (px[..., :3] * a_obj[..., None] + col * (shd * (1 - a_obj))[..., None]) / np.maximum(a_out, 1e-6)[..., None]
    px = np.dstack([rgb, a_out * 255])
    im = Image.fromarray(np.clip(px, 0, 255).astype(np.uint8), "RGBA")
    info = json.load(open(os.path.splitext(src)[0] + ".json"))
    fx, fy = info["foot"]; a = np.asarray(im)[..., 3]
    ys, xs = np.nonzero(a > 200)          # the solid object (the shadow is fainter)
    top = ys.min(); l, r = xs.min(), xs.max()
    s = (foot[1] - old_top) / (fy - top) * hk
    s = min(s, (foot[0] - 4) / max(1, fx - l), (W - foot[0] - 4) / max(1, r - fx))
    im = im.resize((round(im.width * s), round(im.height * s)), Image.LANCZOS)
    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    out.paste(im, (round(foot[0] - fx * s), round(foot[1] - fy * s)), im)
    out.save(dst or FARM + name + ".png")
    return s

if __name__ == "__main__":
    print(place(sys.argv[1], sys.argv[2], sys.argv[3], float(sys.argv[4]) if len(sys.argv) > 4 else 1.0,
                sys.argv[5] if len(sys.argv) > 5 else None))
