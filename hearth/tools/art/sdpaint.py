"""Hand-painted art for the game by a free image model running locally: Stable Diffusion XL with
DreamShaper XL Turbo v2.1 (Lykon, CreativeML Open RAIL++-M: commercial use allowed), on the CPU, no
account and no key. The free twin of aigen.py: same prompts, same jobs.

Our 3D renders are the layout: each picture is repainted from its render (image to image), so the
floor where the dwellers walk and the places on the map stay where the game expects them.

    sdenv/bin/python hearth/tools/art/sdpaint.py [names or groups] [--strength 0.6] [--steps 8] [--out DIR]

MODEL: the checkpoint file (default: $SD_MODEL or ~/models/DreamShaperXL_Turbo_v2_1.safetensors).
"""
import argparse
import os
import sys

import torch
from diffusers import DPMSolverMultistepScheduler, StableDiffusionXLImg2ImgPipeline, StableDiffusionXLPipeline
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import aigen  # noqa: E402  (the prompts and the game's sizes)

MODEL = os.environ.get("SD_MODEL", os.path.expanduser("~/models/DreamShaperXL_Turbo_v2_1.safetensors"))

# an artist's look, not a generator's: painted by hand, clean readable shapes, a calm even light, no glow
STYLE = ("hand-painted concept art for a mobile game, painterly brush strokes, clean simple shapes, Hustle Castle style, "
         "stylized, soft even lighting, warm rich colors, crisp edges, illustration by a professional artist")
NEGATIVE = ("photo, photorealistic, 3d render, cgi, octane, unreal engine, glow, glowing, bloom, lens flare, hdr, "
            "oversaturated, neon, overexposed, cluttered, messy details, noise, blurry, lowres, distorted, warped, "
            "text, watermark, signature, people, person, face")

ROOM = "interior of a room in a medieval fortress, dollhouse cutaway view straight from the front, back wall and floor, "

_pipe = None


def pipe():
    global _pipe
    if _pipe is None:
        torch.set_num_threads(os.cpu_count() or 4)
        # bfloat16 halves the memory (SDXL in float32 needs more than 14 GB)
        p = StableDiffusionXLImg2ImgPipeline.from_single_file(MODEL, torch_dtype=torch.bfloat16)
        # the scheduler DreamShaper Turbo is made for: DPM++ SDE Karras, few steps, low guidance
        p.scheduler = DPMSolverMultistepScheduler.from_config(p.scheduler.config, algorithm_type="sde-dpmsolver++",
                                                              use_karras_sigmas=True)
        p.set_progress_bar_config(disable=True)
        _pipe = p
    return _pipe


def paint(layout, prompt, size, strength=0.65, steps=8, seed=7, guidance=2.0):
    """Repaints the layout picture (scaled to size, multiples of 8) after the prompt."""
    w, h = (size[0] // 8) * 8, (size[1] // 8) * 8
    init = layout.convert("RGB").resize((w, h), Image.LANCZOS)
    g = torch.Generator().manual_seed(seed)
    # the subject first: the text encoders read only 77 tokens
    out = pipe()(prompt=prompt + ", " + STYLE, negative_prompt=NEGATIVE, image=init, strength=strength,
                 num_inference_steps=max(steps, int(round(2 / strength))), guidance_scale=guidance, generator=g,
                 width=w, height=h)
    return grade(out.images[0])


def grade(img):
    """One finish for every picture, like one artist's palette: a touch more contrast and warmth,
    tamed saturation, a light sharpening of the shapes."""
    from PIL import ImageEnhance, ImageFilter
    img = img.convert("RGB")
    img = ImageEnhance.Color(img).enhance(0.92)
    img = ImageEnhance.Contrast(img).enhance(1.06)
    r, g, b = img.split()
    r = r.point(lambda v: min(255, int(v * 1.03 + 2)))
    b = b.point(lambda v: int(v * 0.96))
    img = Image.merge("RGB", (r, g, b))
    return img.filter(ImageFilter.UnsharpMask(radius=1.2, percent=40, threshold=2))


def draw(prompt, size, steps=8, seed=7, guidance=2.0, negative=NEGATIVE):
    """A picture from the prompt alone (text to image, the same model)."""
    w, h = (size[0] // 8) * 8, (size[1] // 8) * 8
    t2i = StableDiffusionXLPipeline(**pipe().components)
    g = torch.Generator().manual_seed(seed)
    return t2i(prompt=prompt + ", " + STYLE, negative_prompt=negative, num_inference_steps=steps, guidance_scale=guidance,
               generator=g, width=w, height=h).images[0]


# the look of a room by its level (GameRunner picks Rooms/<Type>_2 from level 4, _3 from level 8)
TIERS = {
    "1": "simple and modest, few furnishings, bare walls",
    "2": "well furnished, tidy, decorated with rugs, shelves and lamps",
    "3": "rich and luxurious, ornate carved furniture, gold trim, banners, many details",
}


def room(name, args):
    base, _, tier = name.partition("_")
    if tier:
        return _room_tier(base, tier, args)
    path = os.path.join(aigen.ART, "Rooms", name + ".png")
    layout = Image.open(path)
    k = 1152 / layout.width if layout.width <= layout.height * 2.5 else 1536 / layout.width
    size = (round(layout.width * k), round(layout.height * k))
    # the living quarters change what stands in them (one double bed and a cradle instead of three beds): a freer hand
    strength = max(args.strength, 0.82) if name == "Bedrooms" else args.strength
    img = paint(layout, ROOM + aigen.ROOMS[name], size, strength, args.steps, args.seed)
    out = os.path.join(args.out, "Rooms", name + ".png") if args.out else path
    os.makedirs(os.path.dirname(out), exist_ok=True)
    img.save(out)
    print("room", name, img.size, flush=True)


def _room_tier(name, tier, args):
    """A room at a higher level: the same layout, richer furnishings, a stronger touch."""
    layout = Image.open(os.path.join(aigen.ART, "Rooms", name + ".png"))
    k = 1152 / layout.width if layout.width <= layout.height * 2.5 else 1536 / layout.width
    size = (round(layout.width * k), round(layout.height * k))
    img = paint(layout, ROOM + aigen.ROOMS[name] + ", " + TIERS[tier], size, min(0.8, args.strength + 0.1), args.steps, args.seed)
    out = os.path.join(args.out or aigen.ART, "Rooms", "%s_%s.png" % (name, tier))
    os.makedirs(os.path.dirname(out), exist_ok=True)
    img.save(out)
    print("room", name, "tier", tier, img.size, flush=True)


def world(name, args):
    path = os.path.join(aigen.ART, "Rooms" if name == "Rock" else "", name + ".png")
    layout = Image.open(path)
    size = (1280, 720) if name == "Map" else (1280, 640)
    strength = min(args.strength, 0.55) if name == "Map" else args.strength
    img = paint(layout, aigen.WORLD[name], size, strength, args.steps, args.seed)
    if name == "Map":
        img = img.resize((2400, 1350), Image.LANCZOS)  # the size the map's places were measured on
    out = os.path.join(args.out, os.path.relpath(path, aigen.ART)) if args.out else path
    os.makedirs(os.path.dirname(out), exist_ok=True)
    img.save(out)
    print("world", name, img.size, flush=True)


def _cutout(img, tolerance=38):
    """The background (the colour at the corners, reached from the borders) made transparent."""
    from PIL import ImageDraw
    rgb = img.convert("RGB")
    probe = rgb.copy()
    key = (255, 0, 255)
    for corner in ((0, 0), (rgb.width - 1, 0), (0, rgb.height - 1), (rgb.width - 1, rgb.height - 1)):
        if probe.getpixel(corner) != key:
            ImageDraw.floodfill(probe, corner, key, thresh=tolerance)
    mask = Image.new("L", rgb.size, 255)
    px, mp = probe.load(), mask.load()
    for y in range(rgb.height):
        for x in range(rgb.width):
            if px[x, y] == key:
                mp[x, y] = 0
    out = rgb.convert("RGBA")
    out.putalpha(mask)
    return out


def door(args):
    """The door between two rooms: a wooden arched door in a stone arch, cut to its arch."""
    from PIL import ImageDraw, ImageFilter
    img = draw("front view of one sturdy wooden arched door with iron hinges set in a stone archway, centered, symmetric, "
               "plain dark background", (512, 768), seed=args.seed)
    w, h = img.size
    mask = Image.new("L", img.size, 0)
    d = ImageDraw.Draw(mask)
    x0, x1, top, bottom = w * 0.08, w * 0.92, h * 0.04, h * 0.98
    r = (x1 - x0) / 2
    d.ellipse((x0, top, x1, top + 2 * r), fill=255)
    d.rectangle((x0, top + r, x1, bottom), fill=255)
    out = img.convert("RGBA")
    out.putalpha(mask.filter(ImageFilter.GaussianBlur(1.5)))
    _save(out.crop((int(x0) - 2, int(top) - 2, int(x1) + 2, int(bottom) + 2)), "Rooms/Door.png", args)


def ladder(args):
    """The ladder between floors: a wooden ladder, its background cut away."""
    img = draw("a single straight wooden ladder standing upright, front view, centered, isolated on a plain white background",
               (384, 1024), seed=args.seed)
    out = _cutout(img)
    bbox = out.getchannel("A").getbbox()
    _save(out.crop(bbox) if bbox else out, "Rooms/Ladder.png", args)


def splash(args):
    """The loading screen's key art: the fortress in the mountain at dusk, painted big, then grown to 1920x1080."""
    img = draw("epic key art painting, a mighty fortress carved into a snowy mountain cliff at dusk, many small windows glowing "
               "with warm orange firelight, cold blue twilight sky, thick purple fog creeping in the valley below, "
               "three small hero silhouettes with a sword and a banner standing on a rocky ledge in the foreground, "
               "wide cinematic composition, dramatic scale",
               (1216, 704), seed=args.seed, guidance=2.2,
               negative=NEGATIVE.replace(", people, person, face", ", faces, close-up, crowd, frame, border"))
    _save(grade(img.resize((1920, 1080), Image.LANCZOS)), "Splash.png", args)


def _save(img, rel, args):
    path = os.path.join(args.out or aigen.ART, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path)
    print("piece", rel, img.size, flush=True)


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("names", nargs="*", default=["rooms"])
    ap.add_argument("--strength", type=float, default=0.65)
    ap.add_argument("--steps", type=int, default=8)
    ap.add_argument("--seed", type=int, default=7)
    ap.add_argument("--out", default="")
    args = ap.parse_args()
    groups = {"rooms": aigen.GROUPS["rooms"], "world": aigen.GROUPS["world"], "extras": ["Door", "Ladder"],
              "tiers": [r + "_" + t for r in aigen.GROUPS["rooms"] if r not in ("Empty", "Hearth") for t in ("2", "3")]}
    for n in [x for a in args.names for x in groups.get(a, [a])]:
        if n.partition("_")[0] in aigen.ROOMS:
            room(n, args)
        elif n in aigen.WORLD:
            world(n, args)
        elif n == "Door":
            door(args)
        elif n == "Ladder":
            ladder(args)
        elif n == "Splash":
            splash(args)
        else:
            print("unknown:", n)
