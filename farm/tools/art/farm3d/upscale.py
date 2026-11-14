"""Upscales a picture x4 with Real-ESRGAN (tiled, CPU) and saves it at x(scale)."""
import sys, torch, numpy as np
from PIL import Image
import torch.nn as nn, torch.nn.functional as F

class RDB(nn.Module):
    def __init__(s, nf=64, gc=32):
        super().__init__()
        s.conv1 = nn.Conv2d(nf, gc, 3, 1, 1); s.conv2 = nn.Conv2d(nf + gc, gc, 3, 1, 1)
        s.conv3 = nn.Conv2d(nf + 2 * gc, gc, 3, 1, 1); s.conv4 = nn.Conv2d(nf + 3 * gc, gc, 3, 1, 1)
        s.conv5 = nn.Conv2d(nf + 4 * gc, nf, 3, 1, 1); s.l = nn.LeakyReLU(0.2, True)
    def forward(s, x):
        x1 = s.l(s.conv1(x)); x2 = s.l(s.conv2(torch.cat((x, x1), 1)))
        x3 = s.l(s.conv3(torch.cat((x, x1, x2), 1))); x4 = s.l(s.conv4(torch.cat((x, x1, x2, x3), 1)))
        return s.conv5(torch.cat((x, x1, x2, x3, x4), 1)) * 0.2 + x

class RRDB(nn.Module):
    def __init__(s, nf=64):
        super().__init__(); s.rdb1 = RDB(nf); s.rdb2 = RDB(nf); s.rdb3 = RDB(nf)
    def forward(s, x): return s.rdb3(s.rdb2(s.rdb1(x))) * 0.2 + x

class RRDBNet(nn.Module):
    def __init__(s, nb):
        super().__init__(); nf = 64
        s.conv_first = nn.Conv2d(3, nf, 3, 1, 1); s.body = nn.Sequential(*[RRDB(nf) for _ in range(nb)])
        s.conv_body = nn.Conv2d(nf, nf, 3, 1, 1); s.conv_up1 = nn.Conv2d(nf, nf, 3, 1, 1); s.conv_up2 = nn.Conv2d(nf, nf, 3, 1, 1)
        s.conv_hr = nn.Conv2d(nf, nf, 3, 1, 1); s.conv_last = nn.Conv2d(nf, 3, 3, 1, 1); s.l = nn.LeakyReLU(0.2, True)
    def forward(s, x):
        f = s.conv_first(x); f = f + s.conv_body(s.body(f))
        f = s.l(s.conv_up1(F.interpolate(f, scale_factor=2, mode="nearest")))
        f = s.l(s.conv_up2(F.interpolate(f, scale_factor=2, mode="nearest")))
        return s.conv_last(s.l(s.conv_hr(f)))

def load_model(path):
    sd = torch.load(path, map_location="cpu", weights_only=True)
    sd = sd.get("params_ema", sd.get("params", sd))
    nb = 1 + max(int(k.split(".")[1]) for k in sd if k.startswith("body."))
    m = RRDBNet(nb); m.load_state_dict(sd, strict=True); return m
src, model_path, out, scale = sys.argv[1], sys.argv[2], sys.argv[3], float(sys.argv[4])
torch.set_num_threads(4)
m = load_model(model_path).eval()
im = np.asarray(Image.open(src).convert("RGB")).astype(np.float32) / 255
H, W, _ = im.shape; T, P = 256, 16; S = 4
res = np.zeros((H * S, W * S, 3), np.float32)
with torch.no_grad():
    for y in range(0, H, T):
        for x in range(0, W, T):
            y0, x0 = max(0, y - P), max(0, x - P); y1, x1 = min(H, y + T + P), min(W, x + T + P)
            t = torch.from_numpy(im[y0:y1, x0:x1]).permute(2, 0, 1)[None]
            o = m(t)[0].permute(1, 2, 0).clamp(0, 1).numpy()
            oy, ox = (y - y0) * S, (x - x0) * S
            h, w = min(T, H - y) * S, min(T, W - x) * S
            res[y * S:y * S + h, x * S:x * S + w] = o[oy:oy + h, ox:ox + w]
img = Image.fromarray((res * 255 + .5).astype(np.uint8))
img = img.resize((int(W * scale), int(H * scale)), Image.LANCZOS)
img.save(out, quality=92)
print(out, img.size)
