"""Species leaf atlases: every leaf card of a Quaternius atlas is redrawn in place as a detailed species leaf."""
import sys, math, numpy as np
from PIL import Image, ImageDraw, ImageFilter
from scipy import ndimage

SPEC = {  # shape: width factor, tip sharpness, serration (count, depth); colours base→tip, rib
    "apple":  dict(w=0.95, tip=0.9, ser=(16, 0.05), c0=(58, 112, 34), c1=(122, 178, 58), rib=(170, 205, 110), length=1.0),
    "pear":   dict(w=0.72, tip=0.55, ser=(0, 0), c0=(52, 112, 40), c1=(118, 178, 62), rib=(140, 190, 110), length=1.0, gloss=True),
    "cherry": dict(w=0.5, tip=0.45, ser=(26, 0.035), c0=(48, 104, 36), c1=(104, 166, 56), rib=(160, 200, 105), length=1.08),
    "bush":   dict(w=0.8, tip=0.8, ser=(12, 0.04), c0=(44, 100, 38), c1=(108, 168, 60), rib=(150, 196, 104), length=1.0),
}

def leaf_poly(L, W, s, n=120):
    pts_l, pts_r = [], []
    cnt, dep = s["ser"]
    for i in range(n + 1):
        t = i / n
        w = math.sin(math.pi * min(t, 0.999)) ** s["tip"] * (1 - 0.25 * t)
        if cnt:
            saw = (t * cnt) % 1.0
            w *= 1 + dep * (saw - 0.5) * 2 * (0.3 + 0.7 * math.sin(math.pi * t))
        w *= W / 2 * s["w"]
        pts_l.append((t * L, w)); pts_r.append((t * L, -w))
    return pts_l + pts_r[::-1]

def draw_leaf(img, cx, cy, ang, L, W, s, rng):
    S = 4
    size = int(max(L, W) * 1.3) + 4
    tile = Image.new("RGBA", (size * S, size * S), (0, 0, 0, 0))
    d = ImageDraw.Draw(tile)
    ox, oy = (size * S - L * S) / 2, size * S / 2
    poly = [(ox + x * S, oy + y * S) for x, y in leaf_poly(L, W, s)]
    # colour: gradient along the leaf, darker lower half (the fold)
    mask = Image.new("L", tile.size, 0); ImageDraw.Draw(mask).polygon(poly, fill=255)
    xs = np.linspace(0, 1, tile.size[0])[None, :]; ys = np.linspace(-1, 1, tile.size[1])[:, None]
    t = np.clip((xs * tile.size[0] - ox) / (L * S), 0, 1)
    jitter = rng.uniform(-0.12, 0.12)
    c0, c1 = np.array(s["c0"]), np.array(s["c1"])
    col = c0[None, None, :] * (1 - t[..., None] * 0.8) + c1[None, None, :] * (t[..., None] * 0.8 + 0.2)
    col = col * (1 + jitter)
    fold = np.where(ys > 0, 0.86, 1.04)[..., None] * np.ones_like(t)[..., None]
    col = np.clip(col * fold, 0, 255)
    rgba = np.dstack([col, np.asarray(mask)]).astype(np.uint8)
    tile = Image.fromarray(rgba, "RGBA")
    d = ImageDraw.Draw(tile)
    rib = tuple(s["rib"]) + (255,)
    d.line([(ox + 0.02 * L * S, oy), (ox + 0.95 * L * S, oy)], fill=rib, width=max(2, int(L * S * 0.025)))
    for k in range(1, 8):  # veins
        tx = k / 8.5
        x0 = ox + tx * L * S
        wl = W * S / 2 * s["w"] * math.sin(math.pi * tx) ** s["tip"] * 0.8
        for sg in (1, -1):
            d.line([(x0, oy), (x0 + wl * 0.7, oy + sg * wl)], fill=rib[:3] + (150,), width=max(1, int(L * S * 0.01)))
    if s.get("gloss"):
        hl = Image.new("L", tile.size, 0)
        ImageDraw.Draw(hl).ellipse([ox + L * S * 0.25, oy - W * S * 0.22, ox + L * S * 0.7, oy - W * S * 0.04], fill=90)
        hl = hl.filter(ImageFilter.GaussianBlur(S * 3))
        white = Image.new("RGBA", tile.size, (235, 255, 220, 0)); white.putalpha(hl)
        tile = Image.alpha_composite(tile, white)
    tile.putalpha(Image.fromarray(np.minimum(np.asarray(tile.getchannel("A")), np.asarray(mask))))
    tile = tile.rotate(-math.degrees(ang), resample=Image.BICUBIC, expand=False)
    tile = tile.resize((size, size), Image.LANCZOS)
    img.alpha_composite(tile, (int(cx - size / 2), int(cy - size / 2)))

def build(src, species, out):
    a = np.asarray(Image.open(src).convert("RGBA"))[..., 3] > 100
    lab, n = ndimage.label(a)
    img = Image.new("RGBA", (a.shape[1], a.shape[0]), (0, 0, 0, 0))
    s = SPEC[species]; rng = np.random.default_rng(3)
    for i in range(1, n + 1):
        ys, xs = np.nonzero(lab == i)
        if len(xs) < 200: continue
        cx, cy = xs.mean(), ys.mean()
        cov = np.cov(np.vstack([xs - cx, ys - cy]))
        ev, evec = np.linalg.eigh(cov); u = evec[:, 1]
        proj = (xs - cx) * u[0] + (ys - cy) * u[1]
        # base end: the end nearer the atlas centre (leaves radiate out from the stem)
        to_c = np.array([a.shape[1] / 2 - cx, a.shape[0] / 2 - cy])
        if np.dot(u, to_c) > 0: u = -u; proj = -proj
        L = (proj.max() - proj.min()) * s["length"]
        W = 4 * math.sqrt(ev[0])
        start = np.array([cx, cy]) + u * proj.min()
        mid = start + u * L / 2
        draw_leaf(img, mid[0], mid[1], math.atan2(u[1], u[0]), L, W, s, rng)
    img.save(out)
    print(out, n)

if __name__ == "__main__":
    build(sys.argv[1], sys.argv[2], sys.argv[3])
