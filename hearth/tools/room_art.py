"""Placeholder art of the cutaway (docs/GDD.md, 2.5D cutaway like Hustle Castle).

Draws every room interior, the hearth, the rock, an empty dug cell, frost and dwellers as PNG
files for Unity: hearth/unity/LastHearth/Assets/LastHearth/Resources/Art. Dark fantasy palette:
cold stone, warm coal light. Run: python hearth/tools/room_art.py (needs Pillow). The game loads
the files by name, so a real artist can later replace any of them with a drawing of the same size.
"""
import math
import os
import random

from PIL import Image, ImageDraw, ImageFilter

OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                   "unity", "LastHearth", "Assets", "LastHearth", "Resources", "Art")
W, H = 256, 160          # one cell of the grid
FLOOR_Y = 128            # top of the floor planks

STONE = (62, 56, 58)
STONE_DARK = (34, 30, 34)
WOOD = (92, 60, 38)
WOOD_DARK = (58, 36, 22)
COAL = (28, 26, 30)
EMBER = (255, 140, 40)
IRON = (120, 124, 134)
SILVER = (200, 204, 214)


def base(rng, wall=STONE, light=(255, 170, 80), light_x=W // 2):
    """Back wall of stone blocks, plank floor, ceiling beam and a warm light pool."""
    img = Image.new("RGBA", (W, H), wall + (255,))
    d = ImageDraw.Draw(img)
    for row, y in enumerate(range(0, FLOOR_Y, 20)):
        off = 0 if row % 2 == 0 else 18
        for x in range(-off, W, 36):
            c = tuple(max(0, min(255, v + rng.randint(-8, 8))) for v in wall)
            d.rectangle([x + 1, y + 1, x + 34, y + 18], fill=c + (255,))
    d.rectangle([0, FLOOR_Y, W, H], fill=WOOD + (255,))
    for x in range(0, W, 32):
        d.line([x, FLOOR_Y, x, H], fill=WOOD_DARK + (255,), width=2)
    d.rectangle([0, 0, W, 9], fill=WOOD_DARK + (255,))
    # light pool
    glow = Image.new("L", (W, H), 0)
    g = ImageDraw.Draw(glow)
    g.ellipse([light_x - 120, 10, light_x + 120, H + 60], fill=110)
    glow = glow.filter(ImageFilter.GaussianBlur(34))
    img = Image.composite(Image.new("RGBA", (W, H), light + (255,)), img, glow)
    return img, ImageDraw.Draw(img)


def frame(img):
    """Thick rock frame so the cells read as rooms cut into the mountain."""
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, W - 1, H - 1], outline=STONE_DARK + (255,), width=5)
    return img


def lamp(d, x, y):
    d.line([x, 9, x, y - 8], fill=(40, 34, 30, 255), width=2)
    d.ellipse([x - 7, y - 8, x + 7, y + 6], fill=(255, 200, 110, 255), outline=(90, 60, 30, 255))


def bedrooms(rng):
    img, d = base(rng, light=(240, 170, 100), light_x=60)
    for bx in (20, 140):
        d.rectangle([bx, 100, bx + 96, 128], fill=WOOD_DARK + (255,))
        d.rectangle([bx + 4, 92, bx + 92, 108], fill=(140, 120, 100, 255))
        d.rectangle([bx + 30, 90, bx + 92, 106], fill=(110, 40, 40, 255))  # blanket
        d.ellipse([bx + 6, 86, bx + 28, 100], fill=(220, 210, 190, 255))     # pillow
    lamp(d, 60, 40)
    return img


def kitchen(rng):
    img, d = base(rng, light=(255, 150, 60), light_x=90)
    d.rectangle([40, 90, 140, 128], fill=STONE_DARK + (255,))          # hearth stones
    d.polygon([(70, 128), (90, 92), (110, 128)], fill=EMBER + (255,))
    d.polygon([(80, 128), (90, 104), (100, 128)], fill=(255, 220, 120, 255))
    d.ellipse([58, 64, 122, 98], fill=(40, 38, 42, 255))                # pot
    d.rectangle([62, 62, 118, 70], fill=(70, 66, 72, 255))
    for i in range(3):
        d.arc([70 + i * 12, 30, 90 + i * 12, 62], 200, 340, fill=(220, 220, 220, 180), width=2)
    d.rectangle([170, 96, 240, 128], fill=WOOD + (255,))                 # table
    d.ellipse([180, 86, 200, 98], fill=(200, 160, 80, 255))              # bread
    d.ellipse([206, 84, 232, 98], fill=(150, 60, 50, 255))               # meat
    return img


def coal_mine(rng):
    img, d = base(rng, wall=(48, 44, 46), light=(200, 140, 80), light_x=200)
    for _ in range(26):
        x, y = rng.randint(20, 150), rng.randint(70, 124)
        r = rng.randint(8, 16)
        d.ellipse([x - r, y - r, x + r, y + r], fill=(rng.randint(18, 34),) * 3 + (255,))
    d.rectangle([150, 100, 230, 124], fill=WOOD_DARK + (255,))           # cart
    d.ellipse([156, 118, 172, 134], fill=(30, 30, 30, 255))
    d.ellipse([208, 118, 224, 134], fill=(30, 30, 30, 255))
    d.polygon([(154, 100), (226, 100), (214, 86), (166, 86)], fill=COAL + (255,))
    d.line([110, 40, 150, 100], fill=WOOD + (255,), width=5)             # pick
    d.arc([92, 26, 140, 60], 180, 330, fill=IRON + (255,), width=6)
    lamp(d, 200, 44)
    return img


def barracks(rng):
    img, d = base(rng, light=(230, 160, 90))
    for i, x in enumerate((30, 80, 130)):                               # shields
        col = [(120, 30, 30), (40, 60, 110), (120, 100, 40)][i]
        d.polygon([(x, 40), (x + 36, 40), (x + 36, 70), (x + 18, 90), (x, 70)], fill=col + (255,),
                  outline=(200, 180, 120, 255))
        d.line([x + 18, 44, x + 18, 84], fill=(220, 200, 140, 255), width=3)
    d.rectangle([180, 50, 240, 128], fill=WOOD_DARK + (255,))            # rack
    for k in range(3):
        x = 190 + k * 18
        d.line([x, 40, x, 124], fill=SILVER + (255,), width=3)
        d.polygon([(x - 4, 44), (x + 4, 44), (x, 30)], fill=SILVER + (255,))
    return img


def warehouse(rng):
    img, d = base(rng, light=(220, 170, 110))
    for x, y, s in ((20, 84, 44), (66, 84, 44), (40, 42, 42), (150, 92, 36), (190, 92, 36)):
        d.rectangle([x, y, x + s, y + s], fill=WOOD + (255,), outline=WOOD_DARK + (255,), width=3)
        d.line([x, y, x + s, y + s], fill=WOOD_DARK + (255,), width=2)
    d.ellipse([150, 50, 186, 92], fill=(100, 70, 40, 255), outline=WOOD_DARK + (255,), width=3)  # barrel
    d.rectangle([196, 70, 236, 92], fill=SILVER + (255,))                # silver ingots
    d.rectangle([200, 58, 232, 70], fill=IRON + (255,))
    return img


def mine(rng):
    img, d = base(rng, wall=(54, 50, 56), light=(190, 170, 140), light_x=70)
    for _ in range(14):
        x, y = rng.randint(130, 240), rng.randint(20, 110)
        d.polygon([(x, y), (x + 8, y + 4), (x + 4, y + 12), (x - 4, y + 8)], fill=SILVER + (255,))
    d.rectangle([30, 92, 110, 120], fill=(80, 82, 90, 255))               # ore cart
    d.polygon([(32, 92), (108, 92), (98, 76), (42, 76)], fill=(150, 150, 160, 255))
    d.ellipse([36, 114, 52, 130], fill=(30, 30, 30, 255))
    d.ellipse([88, 114, 104, 130], fill=(30, 30, 30, 255))
    d.line([0, 132, W, 132], fill=(90, 90, 96, 255), width=3)             # rails
    lamp(d, 70, 40)
    return img


def tavern(rng):
    img, d = base(rng, light=(255, 160, 70))
    for tx in (30, 150):
        d.ellipse([tx, 98, tx + 76, 112], fill=WOOD + (255,))
        d.rectangle([tx + 34, 108, tx + 42, 128], fill=WOOD_DARK + (255,))
        for mx in (tx + 14, tx + 50):
            d.rectangle([mx, 84, mx + 12, 100], fill=(170, 130, 60, 255))
            d.rectangle([mx, 82, mx + 12, 86], fill=(240, 240, 220, 255))
    d.ellipse([100, 30, 156, 70], fill=(100, 70, 40, 255), outline=WOOD_DARK + (255,), width=3)  # barrel
    lamp(d, 60, 40)
    lamp(d, 200, 40)
    return img


def trading_post(rng):
    img, d = base(rng, light=(240, 180, 100))
    d.rectangle([20, 92, 236, 128], fill=WOOD + (255,), outline=WOOD_DARK + (255,), width=3)  # counter
    d.line([128, 40, 128, 92], fill=(160, 130, 60, 255), width=3)         # scales
    d.line([96, 50, 160, 50], fill=(160, 130, 60, 255), width=3)
    for x in (96, 160):
        d.arc([x - 14, 50, x + 14, 74], 0, 180, fill=(200, 170, 80, 255), width=3)
    for x in (40, 60, 190, 210):
        d.ellipse([x, 80, x + 14, 92], fill=(220, 190, 80, 255))           # coins
    d.rectangle([30, 30, 70, 60], fill=(120, 40, 40, 255))                 # cloth
    return img


def forge(rng):
    img, d = base(rng, wall=(58, 50, 50), light=(255, 120, 40), light_x=70)
    d.rectangle([20, 70, 110, 128], fill=STONE_DARK + (255,))              # furnace
    d.rectangle([36, 86, 94, 118], fill=EMBER + (255,))
    d.rectangle([46, 96, 84, 118], fill=(255, 220, 120, 255))
    d.polygon([(140, 110), (220, 110), (230, 96), (130, 96)], fill=(70, 72, 80, 255))  # anvil
    d.rectangle([164, 110, 196, 128], fill=(60, 62, 70, 255))
    d.line([180, 60, 200, 92], fill=WOOD + (255,), width=4)               # hammer
    d.rectangle([168, 52, 196, 64], fill=IRON + (255,))
    return img


def infirmary(rng):
    img, d = base(rng, wall=(70, 66, 66), light=(230, 210, 180))
    for bx in (20, 140):
        d.rectangle([bx, 104, bx + 96, 128], fill=(200, 196, 186, 255))
        d.rectangle([bx + 2, 98, bx + 30, 108], fill=(240, 240, 236, 255))
    d.rectangle([118, 30, 138, 70], fill=(170, 30, 30, 255))               # cross
    d.rectangle([108, 40, 148, 60], fill=(170, 30, 30, 255))
    for x in (60, 80, 180):
        d.rectangle([x, 70, x + 10, 88], fill=(80, 140, 90, 255))           # potions
    return img


def library(rng):
    img, d = base(rng, light=(240, 190, 120))
    for sx in (16, 136):
        d.rectangle([sx, 20, sx + 104, 128], fill=WOOD_DARK + (255,))
        for row in range(4):
            y = 26 + row * 25
            x = sx + 6
            while x < sx + 96:
                w = rng.randint(6, 12)
                col = rng.choice([(120, 40, 40), (40, 70, 110), (60, 90, 50), (130, 100, 50)])
                d.rectangle([x, y, x + w, y + 20], fill=col + (255,))
                x += w + 2
    return img


def training_hall(rng):
    img, d = base(rng, light=(230, 170, 100))
    d.line([90, 128, 90, 60], fill=WOOD + (255,), width=8)                 # dummy
    d.ellipse([70, 34, 110, 66], fill=(170, 140, 90, 255))
    d.rectangle([66, 66, 114, 100], fill=(170, 140, 90, 255))
    d.line([60, 80, 120, 80], fill=WOOD + (255,), width=5)
    d.ellipse([150, 70, 220, 128], outline=(140, 40, 40, 255), width=6)    # target
    d.ellipse([170, 88, 200, 112], outline=(220, 220, 220, 255), width=5)
    return img


def greenhouse(rng):
    img, d = base(rng, wall=(52, 64, 52), light=(200, 230, 150))
    for bx in (16, 136):
        d.rectangle([bx, 108, bx + 104, 128], fill=(80, 56, 36, 255))
        for k in range(5):
            x = bx + 12 + k * 20
            h = rng.randint(30, 60)
            d.line([x, 108, x, 108 - h], fill=(60, 120, 50, 255), width=3)
            d.ellipse([x - 9, 108 - h - 8, x + 9, 108 - h + 8], fill=(80, 160, 70, 255))
    d.rectangle([90, 14, 166, 40], fill=(255, 200, 120, 255))              # coal lamps
    return img


def watchtower(rng):
    img, d = base(rng, light=(150, 170, 220), light_x=170)
    d.rectangle([130, 20, 230, 100], fill=(30, 40, 70, 255), outline=STONE_DARK + (255,), width=6)  # window
    d.ellipse([196, 30, 216, 50], fill=(230, 230, 210, 255))              # moon
    d.line([180, 20, 180, 100], fill=STONE_DARK + (255,), width=4)
    d.line([40, 128, 70, 70], fill=WOOD + (255,), width=4)                 # telescope
    d.line([100, 128, 70, 70], fill=WOOD + (255,), width=4)
    d.polygon([(50, 60), (120, 40), (124, 52), (54, 72)], fill=(150, 120, 60, 255))
    return img


def chapel(rng):
    img, d = base(rng, wall=(56, 48, 56), light=(255, 130, 50))
    d.rectangle([88, 92, 168, 128], fill=STONE_DARK + (255,))              # altar
    d.polygon([(108, 92), (128, 40), (148, 92)], fill=EMBER + (255,))
    d.polygon([(118, 92), (128, 60), (138, 92)], fill=(255, 230, 140, 255))
    for x in (40, 216):
        d.rectangle([x - 4, 60, x + 4, 128], fill=(220, 200, 160, 255))    # candles
        d.ellipse([x - 5, 48, x + 5, 62], fill=(255, 210, 120, 255))
    return img


def archive(rng):
    img, d = base(rng, light=(220, 180, 120))
    for sx in (16, 150):
        d.rectangle([sx, 24, sx + 90, 128], fill=WOOD_DARK + (255,))
        for row in range(4):
            for col in range(4):
                x, y = sx + 6 + col * 21, 30 + row * 24
                d.ellipse([x, y + 6, x + 18, y + 18], fill=(220, 200, 150, 255), outline=(120, 90, 50, 255))
    d.rectangle([112, 96, 144, 128], fill=WOOD + (255,))
    d.polygon([(108, 96), (148, 96), (140, 84), (116, 84)], fill=(230, 210, 160, 255))  # open scroll
    return img


def hearth(rng):
    """The hearth takes two cells: the great fireplace of the Guardians."""
    w = W * 2
    img = Image.new("RGBA", (w, H), STONE + (255,))
    d = ImageDraw.Draw(img)
    for row, y in enumerate(range(0, FLOOR_Y, 20)):
        off = 0 if row % 2 == 0 else 18
        for x in range(-off, w, 36):
            c = tuple(max(0, min(255, v + rng.randint(-8, 8))) for v in (72, 62, 60))
            d.rectangle([x + 1, y + 1, x + 34, y + 18], fill=c + (255,))
    d.rectangle([0, FLOOR_Y, w, H], fill=(70, 56, 48, 255))
    glow = Image.new("L", (w, H), 0)
    g = ImageDraw.Draw(glow)
    g.ellipse([w // 2 - 230, -20, w // 2 + 230, H + 120], fill=200)
    glow = glow.filter(ImageFilter.GaussianBlur(40))
    img = Image.composite(Image.new("RGBA", (w, H), (255, 120, 40, 255)), img, glow)
    d = ImageDraw.Draw(img)
    cx = w // 2
    d.rectangle([cx - 150, 30, cx + 150, 136], fill=(66, 58, 56, 255), outline=STONE_DARK + (255,), width=4)
    d.pieslice([cx - 100, 40, cx + 100, 200], 180, 360, fill=(26, 14, 10, 255))
    d.rectangle([cx - 100, 120, cx + 100, 136], fill=(26, 14, 10, 255))
    for sc, col in ((1.0, (220, 70, 20)), (0.75, (250, 140, 30)), (0.5, (255, 210, 90)), (0.25, (255, 245, 200))):
        pts = []
        for k in range(0, 361, 6):
            t = math.radians(k)
            pts.append((cx + 70 * sc * math.sin(t) * math.sin(t / 2) ** 1.5, 134 - 96 * sc * (1 + math.cos(t)) / 2))
        d.polygon(pts, fill=col + (255,))
    d.rounded_rectangle([cx - 80, 124, cx + 80, 136], 6, fill=(70, 40, 25, 255))
    d.rectangle([cx - 170, 20, cx + 170, 32], fill=(90, 80, 76, 255))       # mantel
    return frame_wide(img)


def frame_wide(img):
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, img.width - 1, img.height - 1], outline=STONE_DARK + (255,), width=5)
    return img


def rock(rng):
    """Raw mountain around the dug cells (tiles seamlessly)."""
    img = Image.new("RGBA", (W, H), (40, 36, 40, 255))
    d = ImageDraw.Draw(img)
    for _ in range(90):
        x, y = rng.randint(0, W), rng.randint(0, H)
        r = rng.randint(6, 26)
        c = rng.randint(30, 56)
        d.ellipse([x - r, y - r, x + r, y + r], fill=(c, c - 3, c + 2, 255))
    return img.filter(ImageFilter.GaussianBlur(1.5))


def empty_cell(rng):
    """A dug, unbuilt cell: rough walls and a lantern; the player can build here."""
    img, d = base(rng, wall=(50, 46, 50), light=(160, 130, 100))
    d.rectangle([0, FLOOR_Y, W, H], fill=(60, 52, 46, 255))
    for x in (60, 196):                                                    # props of the dig
        d.line([x, 30, x, FLOOR_Y], fill=WOOD + (255,), width=6)
    d.line([60, 30, 196, 30], fill=WOOD + (255,), width=6)
    return frame(img)


def frost():
    """Overlay of a frozen room: blue tint, ice at the edges."""
    img = Image.new("RGBA", (W, H), (120, 170, 230, 70))
    d = ImageDraw.Draw(img)
    rng = random.Random(7)
    for _ in range(40):
        x = rng.choice([rng.randint(0, 30), rng.randint(W - 30, W)])
        y = rng.randint(0, H)
        d.line([x, y, x + rng.randint(-12, 12), y + rng.randint(-12, 12)], fill=(230, 245, 255, 200), width=2)
    for x in range(0, W, 14):
        d.polygon([(x, 0), (x + 10, 0), (x + 5, rng.randint(8, 22))], fill=(220, 240, 255, 220))
    return img


def dweller(color, lantern):
    """A dweller, 24 x 44: hood, cloak and maybe a coal lantern."""
    img = Image.new("RGBA", (24, 44), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.polygon([(4, 42), (20, 42), (17, 14), (7, 14)], fill=color + (255,))
    d.ellipse([6, 2, 18, 16], fill=tuple(max(0, c - 30) for c in color) + (255,))
    d.ellipse([9, 7, 15, 13], fill=(230, 190, 150, 255))
    if lantern:
        d.ellipse([16, 22, 23, 30], fill=(255, 200, 90, 255))
    return img


def creature():
    """A creature of the Pale Fog, 64 x 64: a smoky body with burning eyes."""
    img = Image.new("RGBA", (64, 64), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse([8, 14, 56, 62], fill=(200, 210, 225, 200))
    for x, y, r in ((14, 20, 10), (48, 22, 9), (30, 10, 12), (20, 50, 10), (46, 50, 10)):
        d.ellipse([x - r, y - r, x + r, y + r], fill=(185, 195, 215, 170))
    d.ellipse([20, 30, 28, 38], fill=(255, 60, 30, 255))
    d.ellipse([36, 30, 44, 38], fill=(255, 60, 30, 255))
    d.arc([22, 40, 42, 54], 200, 340, fill=(40, 30, 40, 255), width=3)
    return img.filter(ImageFilter.GaussianBlur(0.8))


ROOMS = {
    "Bedrooms": bedrooms, "Kitchen": kitchen, "CoalMine": coal_mine, "Barracks": barracks,
    "Warehouse": warehouse, "Mine": mine, "Tavern": tavern, "TradingPost": trading_post,
    "Forge": forge, "Infirmary": infirmary, "Library": library, "TrainingHall": training_hall,
    "Greenhouse": greenhouse, "Watchtower": watchtower, "Chapel": chapel, "Archive": archive,
}


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, draw in ROOMS.items():
        frame(draw(random.Random(name))).save(os.path.join(OUT, "Room" + name + ".png"))
    hearth(random.Random("hearth")).save(os.path.join(OUT, "RoomHearth.png"))
    rock(random.Random("rock")).save(os.path.join(OUT, "Rock.png"))
    empty_cell(random.Random("empty")).save(os.path.join(OUT, "Empty.png"))
    frost().save(os.path.join(OUT, "Frost.png"))
    dweller((70, 64, 80), True).save(os.path.join(OUT, "Dweller.png"))
    dweller((140, 40, 36), False).save(os.path.join(OUT, "Hero.png"))
    creature().save(os.path.join(OUT, "Creature.png"))
    print("art written to", OUT)


if __name__ == "__main__":
    main()
