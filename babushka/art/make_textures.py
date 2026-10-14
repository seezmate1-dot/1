"""Generates the stylized dacha textures (1024x1024 PNG, tileable where it makes sense).

Run:  python art/make_textures.py   (needs Pillow and numpy)
Output: art/textures/*.png. The same files are used by the Blender preview and by Unreal.
"""
import math
import os
import random

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

S = 1024
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "textures")


def rgb(hex_color):
    hex_color = hex_color.lstrip("#")
    return tuple(int(hex_color[i:i + 2], 16) for i in (0, 2, 4))


def tile_noise(cells, seed, size=S):
    """Smooth tileable noise in 0..1: random grid upsampled from a 3x3 tiled copy, centre cropped."""
    rng = np.random.default_rng(seed)
    grid = rng.random((cells, cells))
    big = np.tile(grid, (3, 3))
    img = Image.fromarray((big * 255).astype(np.uint8)).resize((size * 3, size * 3), Image.BICUBIC)
    arr = np.asarray(img, dtype=np.float32)[size:size * 2, size:size * 2] / 255.0
    return arr


def shade(base, noise, amount):
    """Multiply a flat colour by noise around 1.0."""
    base = np.array(base, dtype=np.float32)
    factor = 1.0 + (noise[..., None] - 0.5) * 2 * amount
    return np.clip(base * factor, 0, 255)


def save(name, arr_or_img):
    img = arr_or_img if isinstance(arr_or_img, Image.Image) else Image.fromarray(np.asarray(arr_or_img, dtype=np.uint8))
    img.convert("RGB").save(os.path.join(OUT, name + ".png"))
    print("saved", name)


# ---------------------------------------------------------------- interior
def wallpaper():
    """Soviet wallpaper: mint background, thin stripes, rows of small flowers (period 128 px)."""
    arr = shade(rgb("#cfd9b8"), tile_noise(8, 1), 0.04)
    img = Image.fromarray(arr.astype(np.uint8))
    d = ImageDraw.Draw(img)
    for x in range(0, S, 64):
        d.line([(x, 0), (x, S)], fill=rgb("#bccaa3"), width=3)
    petal, center, leaf = rgb("#b5694f"), rgb("#e8c37a"), rgb("#6f8a5a")
    for row, y in enumerate(range(32, S, 128)):
        for x in range(32 + (64 if row % 2 else 0), S + 64, 128):
            cx, cy = x % S, y
            for a in range(5):
                ang = a * 2 * math.pi / 5
                px, py = cx + math.cos(ang) * 11, cy + math.sin(ang) * 11
                d.ellipse([px - 8, py - 8, px + 8, py + 8], fill=petal)
            d.ellipse([cx - 6, cy - 6, cx + 6, cy + 6], fill=center)
            d.ellipse([cx + 14, cy + 10, cx + 30, cy + 18], fill=leaf)
            d.ellipse([cx - 30, cy - 18, cx - 14, cy - 10], fill=leaf)
    save("T_Wallpaper", img.filter(ImageFilter.GaussianBlur(0.6)))


def carpet():
    """The wall carpet every Soviet flat had: red field, blue borders, golden medallion."""
    img = Image.new("RGB", (S, S), rgb("#8e1f22"))
    d = ImageDraw.Draw(img)
    navy, gold, cream, dark = rgb("#1f2e5a"), rgb("#d9a441"), rgb("#efe2c4"), rgb("#5a1214")
    d.rectangle([0, 0, S - 1, S - 1], outline=navy, width=70)
    d.rectangle([70, 70, S - 71, S - 71], outline=gold, width=10)
    d.rectangle([96, 96, S - 97, S - 97], outline=navy, width=24)
    for i in range(0, S, 48):  # zigzag on the outer border
        d.polygon([(i, 20), (i + 24, 50), (i + 48, 20)], fill=cream)
        d.polygon([(i, S - 20), (i + 24, S - 50), (i + 48, S - 20)], fill=cream)
        d.polygon([(20, i), (50, i + 24), (20, i + 48)], fill=cream)
        d.polygon([(S - 20, i), (S - 50, i + 24), (S - 20, i + 48)], fill=cream)
    c = S // 2
    for r, col in ((330, navy), (300, gold), (270, dark), (200, navy), (170, gold), (120, cream), (70, dark)):
        d.polygon([(c, c - r), (c + r, c), (c, c + r), (c - r, c)], fill=col)
    for (x, y) in ((220, 220), (S - 220, 220), (220, S - 220), (S - 220, S - 220)):
        for r, col in ((80, navy), (60, gold), (34, dark)):
            d.polygon([(x, y - r), (x + r, y), (x, y + r), (x - r, y)], fill=col)
    for y in range(160, S - 160, 56):
        for x in range(160, S - 160, 56):
            if abs(x - c) + abs(y - c) > 360:
                d.ellipse([x - 6, y - 6, x + 6, y + 6], fill=gold)
    noise = tile_noise(64, 2)
    arr = shade(np.asarray(img, dtype=np.float32), noise, 0.08)
    save("T_Carpet", arr)


def floor_paint():
    """Floorboards painted red-brown, slightly worn."""
    noise_big, noise_grain = tile_noise(6, 3), tile_noise(96, 4)
    arr = np.zeros((S, S, 3), dtype=np.float32)
    rng = random.Random(5)
    for i in range(8):
        y0, y1 = i * 128, (i + 1) * 128
        tint = rng.uniform(0.92, 1.06)
        base = np.array(rgb("#7b3a24"), dtype=np.float32) * tint
        arr[y0:y1] = base
    arr = shade(arr, noise_grain, 0.06) * (0.94 + noise_big[..., None] * 0.1)
    wear = np.clip((tile_noise(40, 18) - 0.7) * 3.0, 0, 1)[..., None]  # small worn spots
    arr = arr * (1 - wear * 0.25) + (arr * 1.2 + 12) * (wear * 0.25)
    for i in range(8):
        arr[i * 128:i * 128 + 3] *= 0.45
        x = (i * 397) % S
        arr[i * 128:(i + 1) * 128, x:x + 3] *= 0.5
    save("T_FloorPaint", np.clip(arr, 0, 255))


def oilcloth():
    """Kitchen table oilcloth: cream with red checks and tiny flowers."""
    img = Image.new("RGB", (S, S), rgb("#f1ead8"))
    d = ImageDraw.Draw(img, "RGBA")
    for i in range(0, S, 128):
        d.rectangle([i, 0, i + 40, S], fill=(196, 52, 46, 110))
        d.rectangle([0, i, S, i + 40], fill=(196, 52, 46, 110))
    for y in range(84, S, 128):
        for x in range(84, S, 128):
            d.ellipse([x - 10, y - 10, x + 10, y + 10], fill=rgb("#4f8a4a"))
            d.ellipse([x - 4, y - 4, x + 4, y + 4], fill=rgb("#e7b84a"))
    save("T_Oilcloth", img)


def headscarf():
    """Grandma's headscarf: red with white polka dots."""
    img = Image.new("RGB", (S, S), rgb("#c8372f"))
    d = ImageDraw.Draw(img)
    for row, y in enumerate(range(0, S + 64, 64)):
        for x in range(0 if row % 2 == 0 else 32, S + 64, 64):
            d.ellipse([x - 12, y - 12, x + 12, y + 12], fill=rgb("#f4eee2"))
    save("T_Headscarf", img)


# ---------------------------------------------------------------- exterior
def siding():
    """Dacha wall: horizontal boards painted pale blue-green, with chipped paint."""
    noise, chips = tile_noise(64, 6), tile_noise(40, 7)
    arr = shade(rgb("#8fb3ad"), noise, 0.05)
    for i in range(0, S, 96):
        arr[i:i + 10] *= 0.62               # shadow under each board
        arr[i + 10:i + 18] *= 1.08          # lit top edge
    bare = (chips > 0.9)[..., None]
    arr = np.where(bare, shade(rgb("#8a7258"), noise, 0.1), arr)
    save("T_Siding", np.clip(arr, 0, 255))


def slate():
    """Corrugated asbestos slate roof."""
    noise = tile_noise(48, 8)
    x = np.arange(S)
    wave = (np.sin(x / S * 2 * math.pi * 16) * 0.5 + 0.5)[None, :]
    arr = np.array(rgb("#8b8d8a"), dtype=np.float32) * (0.72 + wave[..., None] * 0.38)
    arr = shade(arr, noise, 0.07)
    for y in range(0, S, 256):
        arr[y:y + 6] *= 0.6
    moss = np.clip((tile_noise(10, 9) - 0.75) * 4, 0, 1)[..., None] * 0.5
    arr = arr * (1 - moss) + arr * np.array([0.75, 0.9, 0.65]) * moss
    save("T_Slate", np.clip(arr, 0, 255))


def fence():
    """Weathered vertical fence planks."""
    grain = np.asarray(Image.fromarray((tile_noise(128, 10) * 255).astype(np.uint8)).resize((S, S)), dtype=np.float32) / 255
    stretch = np.asarray(Image.fromarray((tile_noise(32, 11, 256) * 255).astype(np.uint8)).resize((S, S * 4)).crop((0, 0, S, S)), dtype=np.float32) / 255
    arr = shade(rgb("#8a7a63"), stretch * 0.7 + grain * 0.3, 0.16)
    for x in range(0, S, 128):
        arr[:, x:x + 8] *= 0.35
    save("T_Fence", np.clip(arr, 0, 255))


def logs():
    """Bathhouse log wall: round logs stacked horizontally."""
    noise = tile_noise(80, 12)
    y = np.arange(S) % 128
    profile = np.sin(y / 128 * math.pi)[:, None]
    arr = np.array(rgb("#9a6a3d"), dtype=np.float32) * (0.45 + profile[..., None] * 0.65)
    save("T_Logs", np.clip(shade(arr, noise, 0.08), 0, 255))


def grass():
    noise_a, noise_b = tile_noise(12, 13), tile_noise(160, 14)
    arr = shade(rgb("#4f7a3a"), noise_a * 0.6 + noise_b * 0.4, 0.22)
    save("T_Grass", arr)


def soil():
    noise_a, noise_b = tile_noise(20, 15), tile_noise(200, 16)
    arr = shade(rgb("#4a3324"), noise_a * 0.5 + noise_b * 0.5, 0.25)
    save("T_Soil", arr)


def path_boards():
    """Garden walkway boards."""
    noise = tile_noise(100, 17)
    arr = shade(rgb("#9b8462"), noise, 0.1)
    for y in range(0, S, 170):
        arr[y:y + 14] = arr[y:y + 14] * 0.25
    save("T_Boards", np.clip(arr, 0, 255))


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    for make in (wallpaper, carpet, floor_paint, oilcloth, headscarf, siding, slate, fence, logs, grass, soil, path_boards):
        make()
