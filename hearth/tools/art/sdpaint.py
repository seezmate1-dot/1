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
from diffusers import DPMSolverMultistepScheduler, StableDiffusionXLImg2ImgPipeline
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import aigen  # noqa: E402  (the prompts and the game's sizes)

MODEL = os.environ.get("SD_MODEL", os.path.expanduser("~/models/DreamShaperXL_Turbo_v2_1.safetensors"))

STYLE = ("masterpiece, best quality, hand-painted mobile game art, Hustle Castle style, stylized 2.5D cartoon, "
         "highly detailed, polished, soft warm lighting, smooth shading, vibrant harmonious colors, cozy medieval fantasy")
NEGATIVE = ("photo, photorealistic, realistic, 3d render, low poly, blurry, lowres, noisy, grainy, jpeg artifacts, "
            "text, letters, watermark, signature, logo, ui, people, person, character, face, deformed, ugly, dark, gloomy")

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


def paint(layout, prompt, size, strength=0.75, steps=8, seed=7, guidance=2.0):
    """Repaints the layout picture (scaled to size, multiples of 8) after the prompt."""
    w, h = (size[0] // 8) * 8, (size[1] // 8) * 8
    init = layout.convert("RGB").resize((w, h), Image.LANCZOS)
    g = torch.Generator().manual_seed(seed)
    # the subject first: the text encoders read only 77 tokens
    out = pipe()(prompt=prompt + ", " + STYLE, negative_prompt=NEGATIVE, image=init, strength=strength,
                 num_inference_steps=max(steps, int(round(2 / strength))), guidance_scale=guidance, generator=g,
                 width=w, height=h)
    return out.images[0]


def room(name, args):
    path = os.path.join(aigen.ART, "Rooms", name + ".png")
    layout = Image.open(path)
    k = 1152 / layout.width if layout.width <= layout.height * 2.5 else 1536 / layout.width
    size = (round(layout.width * k), round(layout.height * k))
    img = paint(layout, ROOM + aigen.ROOMS[name], size, args.strength, args.steps, args.seed)
    out = os.path.join(args.out, "Rooms", name + ".png") if args.out else path
    os.makedirs(os.path.dirname(out), exist_ok=True)
    img.save(out)
    print("room", name, img.size, flush=True)


def world(name, args):
    path = os.path.join(aigen.ART, "Rooms" if name == "Rock" else "", name + ".png")
    layout = Image.open(path)
    size = (1536, 864) if name == "Map" else (1280, 640)
    strength = min(args.strength, 0.55) if name == "Map" else args.strength
    img = paint(layout, aigen.WORLD[name], size, strength, args.steps, args.seed)
    if name == "Map":
        img = img.resize((2400, 1350), Image.LANCZOS)
    out = os.path.join(args.out, os.path.relpath(path, aigen.ART)) if args.out else path
    os.makedirs(os.path.dirname(out), exist_ok=True)
    img.save(out)
    print("world", name, img.size, flush=True)


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("names", nargs="*", default=["rooms"])
    ap.add_argument("--strength", type=float, default=0.75)
    ap.add_argument("--steps", type=int, default=8)
    ap.add_argument("--seed", type=int, default=7)
    ap.add_argument("--out", default="")
    args = ap.parse_args()
    groups = {"rooms": aigen.GROUPS["rooms"], "world": aigen.GROUPS["world"]}
    for n in [x for a in args.names for x in groups.get(a, [a])]:
        if n in aigen.ROOMS:
            room(n, args)
        elif n in aigen.WORLD:
            world(n, args)
        else:
            print("unknown:", n)
