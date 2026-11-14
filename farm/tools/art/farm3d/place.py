"""Puts a render into the game's canvas: the same canvas size as the sprite it replaces, the foot on the game's foot
point, the height of the old picture. python place.py render.png TargetName kind [height_scale]"""
import sys, json, os
import numpy as np
from PIL import Image
FARM = "/home/user/1/farm/unity/SunnyValley/Assets/SunnyValley/Resources/Art/Farm/"
# canvas in map units and the foot (from the left, from the bottom): FarmSitesView.cs
FOOT = {"tree": (255, 263.5, 95.2, 52.7), "bush": None}

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
    px[..., 3] = np.where(sh, px[..., 3] * 0.75, px[..., 3]); im = Image.fromarray(px.astype(np.uint8), "RGBA")
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
