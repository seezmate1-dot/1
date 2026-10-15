"""Hand-painted art for the game by an image model (OpenAI's gpt-image), in the look of Hustle Castle.

Our 3D renders (render_all.py) are the layout: every room, the map, the mountain and the battlefield
is sent as a reference and repainted in one painted style, keeping its composition (the floor line
where the dwellers walk, the places on the map), so the game's coordinates stay right. Interface
pieces, icons and decorations are painted from text on a transparent background. The first painted
room becomes the style reference of all the others, so everything matches.

    OPENAI_API_KEY=... python hearth/tools/art/aigen.py [names or groups ...] [--quality medium|high]

Groups: rooms, world, ui, icons, buttons, decor, all. Raw pictures are kept in tools/art/ai/ (so a
picture can be redone alone); the game's copies go to Resources/Art in the game's sizes.
Runs in GitHub Actions too (.github/workflows/ai-art.yml), with the key as a repository secret.
"""
import base64
import io
import json
import os
import sys
import time
import urllib.error
import urllib.request

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, "ai")
ART = os.path.join(os.path.dirname(os.path.dirname(HERE)), "unity", "LastHearth", "Assets", "LastHearth", "Resources", "Art")
MODEL = os.environ.get("OPENAI_IMAGE_MODEL", "gpt-image-1")
API = "https://api.openai.com/v1/images/"

STYLE = ("Hand-painted 2.5D mobile game art in the style of Hustle Castle: rich, polished and highly detailed, "
         "soft warm lighting with gentle shadows, smooth clean edges, soft gradients, saturated but harmonious natural "
         "colours, slightly stylized cartoon proportions, cozy medieval fantasy, crisp readable shapes, top-quality "
         "professional mobile game art. No text, no letters, no numbers, no watermark, no UI, no people.")

ROOM = ("A cutaway view of one room inside a medieval fortress carved into a mountain, seen straight from the front like a "
        "dollhouse with the front wall removed. Keep exactly the composition of the reference picture: the same camera, the "
        "same room size and position, the same floor line, the same furniture places, the same band of warm brown mountain "
        "rock framing the room above, below and at both sides. Repaint it beautifully: ")

ROOMS = {
    "Hearth": "the great hall of the Guardians, two rooms wide: a huge carved stone fireplace in the middle with glowing coals "
              "but no flames, stone columns, banners, a long rug, warm amber light",
    "Bedrooms": "cozy living quarters of a family: one big double bed with a warm quilt and pillows, a wooden cradle beside it, "
                "a wardrobe, a small table with a candle, a rug, a window with daylight, plaster walls with timber beams",
    "Kitchen": "a medieval kitchen: a stone hearth with a copper cauldron, a long table with bread, vegetables and pots, "
               "barrels, hanging herbs and sausages, a shelf with crockery",
    "CoalMine": "a coal mine chamber: dark rock walls with black coal seams, wooden supports, mine carts full of coal, "
                "picks and shovels, lanterns, a glow of warm light",
    "Barracks": "barracks: weapon racks with swords and spears, armour stands, bunks, shields on the walls, a training dummy, "
                "stone walls with banners",
    "Warehouse": "a storehouse: stacked crates, sacks of grain, barrels, shelves with goods, ropes, a ladder, timber beams",
    "Mine": "an iron and silver mine: rock walls with veins of silver ore, wooden scaffolds, carts of ore, lanterns, tools",
    "Tavern": "a lively tavern: a bar counter with kegs and mugs, round tables with benches, a fireplace, hanging lamps, "
              "warm golden light",
    "TradingPost": "a trading post: a merchant counter with scales and coins, shelves of goods, rolled carpets, crates, "
                   "a world map on the wall",
    "Forge": "a blacksmith forge: a glowing furnace, an anvil with a hammer, bellows, racks of swords and tools, sparks, "
             "orange firelight",
    "Infirmary": "an infirmary: clean beds, shelves of potion bottles and herbs, a table with a mortar and pestle, "
                 "white linen, soft cool daylight",
    "Library": "a library: tall bookshelves full of books, a reading desk with candles and an open book, scrolls, a globe, "
               "a ladder, warm light",
    "TrainingHall": "a training hall: training dummies, a rack of wooden weapons, archery target, mats on the floor, "
                    "banners, bright light",
    "Greenhouse": "an underground greenhouse: planting beds with vegetables and glowing mushrooms, pots with plants, "
                  "watering cans, hanging lamps like little suns",
    "Watchtower": "a watch room: a big telescope by a window, maps on a table, a brazier, spyglasses, horns, cool blue light",
    "Chapel": "a small chapel: an altar with a golden brazier of holy fire, candles, red banners, wooden pews, "
              "stained glass windows",
    "Archive": "an archive: shelves of scrolls and ledgers, chests, a writing desk with quills and ink, maps, candles",
    "Empty": "an empty dug-out room under construction: bare stone walls, rubble, scaffolding, a few planks and tools, "
             "dust in a beam of light",
}

WORLD = {
    "Map": "A world map of a fantasy archipelago seen from above at an angle, like Hustle Castle's map. Keep exactly the "
           "composition of the reference: the same islands in the same places with the same buildings, ships and dotted sea "
           "routes. Repaint it as a beautiful hand-painted game map: turquoise shallows and deep blue sea with soft waves, "
           "sandy beaches, lush green forests, warm sunlight, the islands in the north darker and misty purple.",
    "Mountain": "Towering jagged snowy mountain peaks with sharp ridges and deep blue shadows under a bright blue sky with "
                "soft white clouds, a small stone fortress gatehouse with two towers and red flags at the foot in the middle, "
                "green meadow and fir trees on the lower slopes, warm brown rock cliff at the bottom. Keep the composition "
                "of the reference picture.",
    "Battlefield": "A battlefield for side-view fights: a sunny green meadow with a dirt path along the bottom, a forest "
                   "behind, boulders, the trees on the right dying into creeping purple fog. Keep the composition of the "
                   "reference picture, the path at the same height.",
    "Rock": "A wall of solid warm brown mountain rock with layered strata, cracks and a few stones, seen straight on, "
            "filling the whole picture evenly (it tiles).",
}

# transparent pieces: (prompt, size of the game's picture)
UI = {
    "Round": ("A round glossy game button: deep blue enamel disc in a thick polished gold ring, soft highlight on top, "
              "seen from the front, centred, nothing else", 256),
    "Badge": ("A small round level badge: deep blue enamel disc in a gold ring, centred, nothing else", 128),
    "Close": ("A round red glossy close button with a white cross, in a gold ring, centred, nothing else", 128),
    "Plus": ("A round green glossy build button with a fat white plus sign, in a gold ring, centred, nothing else", 128),
    "Dot": ("A small glossy red notification dot with a thin white rim, centred, nothing else", 64),
    "Gear": ("A round settings button: a golden gear on a deep blue enamel disc in a gold ring, centred, nothing else", 128),
    "Bubble": ("A white glossy speech-bubble-like cloud with a small tail at the bottom, soft shading, centred, empty inside, "
               "nothing else", 128),
}
# nine-slice frames: painted wide, the game stretches the middle
FRAMES = {
    "Pill": ("A long horizontal plate of dark polished wood with rounded ends and a thin gold edge, seen from the front, "
             "filling the picture width, empty, nothing else", (256, 72)),
    "Window": ("A rectangular game window frame: dark varnished wooden board inside, a carved wooden frame with gold trim "
               "and gold corner ornaments, seen from the front, filling the picture, empty inside, nothing else", (512, 384)),
    "Button": ("A wide rectangular wooden game button with slightly rounded corners, polished planks, seen from the front, "
               "filling the picture, empty, nothing else", (192, 64)),
    "ButtonOn": ("A wide rectangular wooden game button with slightly rounded corners and a shining gold trim, seen from the "
                 "front, filling the picture, empty, nothing else", (192, 64)),
    "Panel": ("A square parchment panel in a dark wooden frame, seen from the front, filling the picture, empty, "
              "nothing else", (256, 256)),
    "Bar": ("A long horizontal panel of dark polished wood planks with a gold strip along its top edge, seen from the front, "
            "filling the picture, nothing else", (512, 112)),
}
ICONS = {
    "Coal": "a pile of shiny black coal lumps", "Food": "a basket of bread, apples and cheese",
    "Iron": "a stack of iron ingots", "Silver": "a stack of shining silver coins and a silver ingot",
    "Essence": "a glowing violet magic essence orb in a glass flask", "Sparks": "a sparkling blue-white crystal gem",
    "Blueprints": "rolled blueprint scrolls tied with string", "Cores": "a glowing red crystal heart of a monster",
    "Ember": "a glowing orange ember stone with small flames", "Glory": "a golden laurel wreath with a star",
}
BUTTONS = {
    "Collect": "a heap of gold coins", "Fights": "a crossed sword and wooden shield", "Growth": "an anvil with a hammer",
    "Routes": "a travel bag with a pickaxe", "Quests": "a rolled quest scroll with a red wax seal",
    "Shop": "a leather pouch of gold coins", "Events": "a golden chalice", "Rewards": "an open treasure chest with gold",
    "Map": "a folded parchment treasure map", "Fortress": "a small stone castle with red roofs and flags",
}
DECOR = {
    "armor": "a suit of knight armour on a stand", "banner_blue": "a hanging blue heraldic banner",
    "banner_green": "a hanging green heraldic banner", "banner_red": "a hanging red heraldic banner",
    "barrels": "a stack of wooden barrels", "candles": "a tall iron candelabrum with lit candles",
    "chest_gold": "a gold-bound treasure chest", "crystal": "a large glowing blue magic crystal on a stone base",
    "gold_shield": "an ornate golden shield", "keg": "a barrel of red apples", "plant": "a potted green plant",
    "pumpkin": "a big orange pumpkin", "rug": "an ornate red and gold rug seen at an angle",
    "shelf": "a wooden shelf with bottles and jars", "shield": "a wooden round shield with an iron rim",
    "shrine": "a small stone shrine with a candle", "throne": "a carved wooden throne with red cushions",
    "treasure": "a pile of gold coins and jewels",
}


# ------------------------------------------------------------------ the API

def _post(endpoint, fields, files=None):
    """POST to the images API (JSON, or multipart with files); returns the decoded picture."""
    key = os.environ.get("OPENAI_API_KEY")
    if not key:
        sys.exit("OPENAI_API_KEY is not set")
    headers = {"Authorization": "Bearer " + key}
    if files:
        boundary = "----hearth%d" % int(time.time() * 1000)
        body = io.BytesIO()
        for k, v in fields.items():
            body.write(("--%s\r\nContent-Disposition: form-data; name=\"%s\"\r\n\r\n%s\r\n" % (boundary, k, v)).encode())
        for name, (filename, data) in files:
            body.write(("--%s\r\nContent-Disposition: form-data; name=\"%s\"; filename=\"%s\"\r\nContent-Type: image/png\r\n\r\n"
                        % (boundary, name, filename)).encode())
            body.write(data)
            body.write(b"\r\n")
        body.write(("--%s--\r\n" % boundary).encode())
        data = body.getvalue()
        headers["Content-Type"] = "multipart/form-data; boundary=" + boundary
    else:
        data = json.dumps(fields).encode()
        headers["Content-Type"] = "application/json"
    for attempt in range(5):
        req = urllib.request.Request(API + endpoint, data=data, headers=headers)
        try:
            with urllib.request.urlopen(req, timeout=600) as r:
                out = json.load(r)
            return Image.open(io.BytesIO(base64.b64decode(out["data"][0]["b64_json"])))
        except urllib.error.HTTPError as e:
            msg = e.read().decode(errors="replace")
            if e.code in (429, 500, 502, 503) and attempt < 4:
                print("  retry after", e.code, msg[:200], flush=True)
                time.sleep(20 * (attempt + 1))
                continue
            sys.exit("OpenAI error %d: %s" % (e.code, msg[:1000]))
        except (urllib.error.URLError, TimeoutError) as e:
            if attempt < 4:
                time.sleep(15)
                continue
            raise e


def _png(img):
    b = io.BytesIO()
    img.save(b, "PNG")
    return b.getvalue()


def generate(prompt, size="1536x1024", quality="medium", transparent=False, refs=()):
    """A picture from the prompt; refs: reference pictures (layout first, then style)."""
    fields = {"model": MODEL, "prompt": prompt, "size": size, "quality": quality, "n": 1}
    if transparent:
        fields["background"] = "transparent"
        fields["output_format"] = "png"
    if refs:
        files = [("image[]", ("ref%d.png" % i, _png(r))) for i, r in enumerate(refs)]
        return _post("edits", fields, files)
    return _post("generations", fields)


# ------------------------------------------------------------------ fitting the game's pictures

def _canvas(img, size):
    """The reference on the model's canvas: scaled to the canvas width, centred, the margins filled
    by stretching its own edge rows (they get repainted and cropped away)."""
    W, H = size
    w, h = W, round(img.height * W / img.width)
    if h > H:
        h, w = H, round(img.width * H / img.height)
    src = img.convert("RGB").resize((w, h), Image.LANCZOS)
    out = src.resize((W, H), Image.LANCZOS)
    out.paste(src, ((W - w) // 2, (H - h) // 2))
    return out, ((W - w) // 2, (H - h) // 2, w, h)


def _crop(img, box, size):
    x, y, w, h = box
    return img.crop((x, y, x + w, y + h)).resize(size, Image.LANCZOS)


def _trim(img, size, pad=0.04):
    """A transparent piece cropped to what it shows, centred in a square or the given size."""
    img = img.convert("RGBA")
    bbox = img.getchannel("A").point(lambda a: 255 if a > 8 else 0).getbbox() or (0, 0, img.width, img.height)
    piece = img.crop(bbox)
    W, H = size
    k = min(W * (1 - 2 * pad) / piece.width, H * (1 - 2 * pad) / piece.height)
    piece = piece.resize((max(1, round(piece.width * k)), max(1, round(piece.height * k))), Image.LANCZOS)
    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    out.alpha_composite(piece, ((W - piece.width) // 2, (H - piece.height) // 2))
    return out


def _fill(img, size):
    """A frame piece stretched to fill its picture exactly (nine-slice art)."""
    img = img.convert("RGBA")
    bbox = img.getchannel("A").point(lambda a: 255 if a > 8 else 0).getbbox() or (0, 0, img.width, img.height)
    return img.crop(bbox).resize(size, Image.LANCZOS)


def _save_raw(name, img):
    os.makedirs(RAW, exist_ok=True)
    path = os.path.join(RAW, name + ".png")
    img.save(path)
    return path


def _style_ref():
    p = os.path.join(RAW, "Room_Hearth.png")
    return Image.open(p) if os.path.exists(p) else None


# ------------------------------------------------------------------ the jobs

def room(name, quality):
    ref_path = os.path.join(ART, "Rooms", name + ".png")
    layout = Image.open(ref_path)
    canvas, box = _canvas(layout, (1536, 1024))
    style = _style_ref() if name != "Hearth" else None
    prompt = STYLE + " " + ROOM + ROOMS[name] + "." + (" Paint it in exactly the same art style as the second reference picture."
                                                       if style else "")
    img = generate(prompt, "1536x1024", quality, refs=[canvas] + ([style] if style else []))
    _save_raw("Room_" + name, img)
    out = _crop(img.convert("RGB"), box, (layout.width * 4 // 3, layout.height * 4 // 3))
    out.save(ref_path)
    print("room", name, out.size, flush=True)


def world(name, quality):
    ref_path = os.path.join(ART, "Rooms" if name == "Rock" else "", name + ".png")
    layout = Image.open(ref_path)
    canvas, box = _canvas(layout, (1536, 1024))
    style = _style_ref()
    prompt = STYLE.replace(" no people.", "") + " " + WORLD[name] + (" Paint it in the same art style as the second reference picture."
                                                                    if style else "")
    img = generate(prompt, "1536x1024", "high" if name == "Map" else quality, refs=[canvas] + ([style] if style else []))
    _save_raw("World_" + name, img)
    out = _crop(img.convert("RGB"), box, layout.size if name != "Map" else (2400, 1350))
    out.save(ref_path)
    print("world", name, out.size, flush=True)


def piece(group, name, prompt, size, quality, fill=False):
    text = STYLE + " A single game asset on a fully transparent background: " + prompt + "."
    img = generate(text, "1024x1024" if size[0] <= size[1] * 1.6 else "1536x1024", quality, transparent=True)
    _save_raw(group + "_" + name, img)
    out = _fill(img, size) if fill else _trim(img, size)
    folder = {"UI": "UI", "Frame": "UI", "Icon": "Icons", "Button": "Buttons", "Decor": "Decor"}[group]
    out.save(os.path.join(ART, folder, name + ".png"))
    print(group.lower(), name, out.size, flush=True)


def run(names, quality):
    for n in names:
        if n in ROOMS:
            room(n, quality)
        elif n in WORLD:
            world(n, quality)
        elif n in UI:
            piece("UI", n, UI[n][0], (UI[n][1],) * 2, quality)
        elif n in FRAMES:
            piece("Frame", n, FRAMES[n][0], FRAMES[n][1], quality, fill=True)
        elif n in ICONS:
            piece("Icon", n, "a resource icon: " + ICONS[n] + ", centred, nothing else", (256, 256), quality)
        elif n in BUTTONS:
            piece("Button", n, "a button icon: " + BUTTONS[n] + ", centred, nothing else", (256, 256), quality)
        elif n in DECOR:
            size = Image.open(os.path.join(ART, "Decor", n + ".png")).size
            piece("Decor", n, "a room decoration: " + DECOR[n] + ", seen from the front, centred, nothing else", size, quality)
        else:
            print("unknown:", n)


GROUPS = {
    # the Hearth first: it sets the style of all the rest
    "rooms": ["Hearth"] + [r for r in ROOMS if r != "Hearth"],
    "world": list(WORLD),
    "ui": list(UI) + list(FRAMES),
    "icons": list(ICONS),
    "buttons": list(BUTTONS),
    "decor": list(DECOR),
}
GROUPS["all"] = sum((GROUPS[g] for g in ("rooms", "world", "ui", "icons", "buttons", "decor")), [])


if __name__ == "__main__":
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    quality = "high" if "--quality=high" in sys.argv or "--high" in sys.argv else "medium"
    names = []
    for a in args or ["all"]:
        for part in a.replace(",", " ").split():
            names += GROUPS.get(part, [part])
    run(names, quality)
