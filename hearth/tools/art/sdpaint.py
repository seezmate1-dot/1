"""Hand-painted art for the game by a free image model running locally: Stable Diffusion XL with
DreamShaper XL Turbo v2.1 (Lykon, CreativeML Open RAIL++-M: commercial use allowed), on the CPU, no
account and no key. The free twin of aigen.py: same prompts, same jobs.

Our 3D renders are the layout: each picture is repainted from its render (image to image), so the
floor where the dwellers walk and the places on the map stay where the game expects them.

    sdenv/bin/python hearth/tools/art/sdpaint.py [names or groups] [--strength 0.6] [--steps 8] [--out DIR]

Transparent pieces (group "pieces": Icons/<Name>, Buttons/<Name>, Decor/<name>, Rooms/Rubble, Rooms/Scaffold)
are painted from text on a plain white ground and cut out with rembg (pip install "rembg[cpu]"; without
it, a flood fill of the ground from the borders). Group "items": the icons of the heroes' equipment, Items/<Kind>
(ITEMS; the game draws them once Art.json has "itemArt").

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
_dtype = torch.bfloat16  # the precision of the painting (the UNet's), what its inputs are cast to


def native_bf16():
    """Whether the processor computes bfloat16 itself (AVX-512 BF16 or AMX); without it torch emulates
    bfloat16 and a picture takes over an hour instead of a couple of minutes."""
    try:
        flags = open("/proc/cpuinfo").read()
    except OSError:
        return False
    return "avx512_bf16" in flags or "amx_bf16" in flags


def _trim():
    """Hands the memory freed after loading back to the system (glibc keeps it otherwise)."""
    import ctypes
    import gc
    gc.collect()
    try:
        ctypes.CDLL("libc.so.6").malloc_trim(0)
    except OSError:
        pass


def memory_gb():
    """The memory this process may use: the machine's with its swap, or its control group's limit when lower."""
    total = 0.0
    try:
        # the swap counts: the float32 peak (about 14 GB) lasts only while the model is converted
        for line in open("/proc/meminfo"):
            if line.startswith(("MemTotal:", "SwapTotal:")):
                total += int(line.split()[1]) / 2 ** 20
        limit = open("/sys/fs/cgroup/memory.max").read().strip()
        if limit.isdigit():
            total = min(total, int(limit) / 2 ** 30)
    except OSError:
        pass
    return total


def pipe():
    global _pipe, _dtype
    if _pipe is None:
        torch.set_num_threads(os.cpu_count() or 4)
        # loaded in bfloat16: SDXL in float32 needs more than 14 GB while loading
        p = StableDiffusionXLImg2ImgPipeline.from_single_file(MODEL, torch_dtype=torch.bfloat16)
        _trim()
        # SD_PRECISION=fp32 or bf16 overrides the choice
        precision = os.environ.get("SD_PRECISION") or ("fp32" if not native_bf16() and memory_gb() >= 18 else "bf16")
        if precision == "fp32" and not native_bf16():
            # the painting itself in float32 (about 14 GB at the peak with the text encoders in bfloat16: they
            # run once), one tensor at a time; on a smaller machine the slow emulated bfloat16 has to do
            import gc
            for module in (p.unet, p.vae):
                for t in list(module.parameters()) + list(module.buffers()):
                    if t.dtype == torch.bfloat16:
                        t.data = t.data.float()
                gc.collect()
                _trim()
            # the attention and the decoder in slices: a big picture's activations stay small
            p.enable_attention_slicing()
            p.vae.enable_tiling()
            # diffusers may still report bfloat16 for a model converted tensor by tensor: say it outright
            p.unet.to(torch.float32)
            p.vae.to(torch.float32)
            _dtype = torch.float32
            print("float32 painting (no native bfloat16 on this processor)", flush=True)
        # the scheduler DreamShaper Turbo is made for: DPM++ SDE Karras, few steps, low guidance
        p.scheduler = DPMSolverMultistepScheduler.from_config(p.scheduler.config, algorithm_type="sde-dpmsolver++",
                                                              use_karras_sigmas=True)
        p.set_progress_bar_config(disable=True)
        _pipe = p
    return _pipe


def painter(cls=StableDiffusionXLImg2ImgPipeline):
    """A pipeline for the painting alone: the prompt comes encoded (embeds), and without the text encoders
    diffusers casts it to the UNet's precision instead of the encoders' bfloat16."""
    parts = dict(pipe().components)
    parts["text_encoder"] = parts["text_encoder_2"] = None
    return cls(**parts)


def embeds(prompt, negative):
    """The prompt read by the text encoders, in the painting's precision."""
    p = pipe()
    e = p.encode_prompt(prompt=prompt, device="cpu", num_images_per_prompt=1, do_classifier_free_guidance=True,
                        negative_prompt=negative)
    names = ("prompt_embeds", "negative_prompt_embeds", "pooled_prompt_embeds", "negative_pooled_prompt_embeds")
    return {n: t.to(_dtype) for n, t in zip(names, e)}


def paint(layout, prompt, size, strength=0.65, steps=8, seed=7, guidance=2.0, negative=NEGATIVE):
    """Repaints the layout picture (scaled to size, multiples of 8) after the prompt."""
    w, h = (size[0] // 8) * 8, (size[1] // 8) * 8
    init = layout.convert("RGB").resize((w, h), Image.LANCZOS)
    g = torch.Generator().manual_seed(seed)
    # the subject first: the text encoders read only 77 tokens
    e = embeds(prompt + ", " + STYLE, negative)
    out = painter()(**e, image=init, strength=strength,
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
    e = embeds(prompt + ", " + STYLE, negative)
    t2i = painter(StableDiffusionXLPipeline)
    g = torch.Generator().manual_seed(seed)
    return t2i(**e, num_inference_steps=steps, guidance_scale=guidance,
               generator=g, width=w, height=h).images[0]


# rooms where nobody sleeps: the model reads a long table in the layout as a bed
SLEEP = ("Bedrooms", "Barracks", "Infirmary")


def room_negative(name):
    return NEGATIVE if name in SLEEP else NEGATIVE + ", bed, beds"


# the living quarters of a family: the layout has three beds, the painting one double bed and a cradle
BEDS = ("one big wooden double bed for two with a red quilt and two white pillows standing in the middle against the back "
        "wall, a small wooden baby cradle on rockers next to the bed, a rug on the floor")


# what stands in the middle of a room when the layout's furniture is read wrongly (inpainting)
CENTRE = {
    "Bedrooms": (BEDS, ", three beds, two single beds, bunk beds"),
    "Archive": ("a large wooden writing desk with an open ledger, quills, an ink pot and stacks of scrolls in the middle, "
                "a wooden chair, chests of scrolls beside it", ", bed, beds, sofa, couch, mattress"),
}


def repaint_centre(img, steps=8, seed=7, room="Bedrooms"):
    """The middle of a room painted again in place (inpainting): in the living quarters one double bed and a
    cradle, in the archive a writing desk."""
    from diffusers import StableDiffusionXLInpaintPipeline
    from PIL import ImageDraw, ImageFilter
    w, h = img.size
    mask = Image.new("L", img.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle((w * 0.1, h * 0.38, w * 0.9, h * 0.88), radius=h * 0.06, fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(h * 0.02))
    inpaint = painter(StableDiffusionXLInpaintPipeline)
    g = torch.Generator().manual_seed(seed)
    what, avoid = CENTRE[room]
    out = inpaint(**embeds(what + ", " + STYLE, NEGATIVE + avoid), image=img,
                  mask_image=mask, strength=0.99, num_inference_steps=max(steps, 12), guidance_scale=2.5, generator=g,
                  width=w, height=h).images[0]
    return Image.composite(grade(out), img, mask)


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
    img = paint(layout, ROOM + aigen.ROOMS[name], size, args.strength, args.steps, args.seed, negative=room_negative(name))
    if name in CENTRE:
        img = repaint_centre(img, args.steps, args.seed, name)
    out = os.path.join(args.out, "Rooms", name + ".png") if args.out else path
    os.makedirs(os.path.dirname(out), exist_ok=True)
    img.save(out)
    print("room", name, img.size, flush=True)


def _room_tier(name, tier, args):
    """A room at a higher level: the same layout, richer furnishings, a stronger touch."""
    layout = Image.open(os.path.join(aigen.ART, "Rooms", name + ".png"))
    k = 1152 / layout.width if layout.width <= layout.height * 2.5 else 1536 / layout.width
    size = (round(layout.width * k), round(layout.height * k))
    img = paint(layout, ROOM + aigen.ROOMS[name] + ", " + TIERS[tier], size, min(0.8, args.strength + 0.1), args.steps,
                args.seed, negative=room_negative(name))
    if name in CENTRE:
        img = repaint_centre(img, args.steps, args.seed, name)
    out = os.path.join(args.out or aigen.ART, "Rooms", "%s_%s.png" % (name, tier))
    os.makedirs(os.path.dirname(out), exist_ok=True)
    img.save(out)
    print("room", name, "tier", tier, img.size, flush=True)


def world(name, args):
    path = os.path.join(aigen.ART, "Rooms" if name == "Rock" else "", name + ".png")
    layout = Image.open(path)
    size = (1280, 720) if name == "Map" else (1280, 640)
    # the map keeps its islands in place, the mountain its snowy peaks: a lighter hand
    strength = min(args.strength, 0.62) if name == "Map" else min(args.strength, 0.5) if name == "Mountain" else args.strength
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


def stairs(args):
    """One floor of the stair tower beside the cutaway (GameRunner.View DrawStairTower): a wooden staircase
    climbing inside a stone tower; the floors repeat it, so its top meets its bottom."""
    img = draw("cutaway view straight from the front into a narrow medieval stone stair tower, one sturdy wooden "
               "staircase with a wooden railing climbing diagonally from the bottom left corner to the top right corner, "
               "stone block walls, a burning wall torch, warm light, a wooden floor at the bottom", (768, 768), seed=args.seed)
    _save(grade(img).resize((512, 512), Image.LANCZOS), "Rooms/Stairs.png", args)


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
    bg = _ground(img)
    out = _unmix(grade(img), _matte(img, bg, holes=True), _ground(grade(img)))
    bbox = out.getchannel("A").point(lambda a: 255 if a > 8 else 0).getbbox()
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


# ------------------------------------------------------------------ the transparent pieces

# what each piece shows (after aigen's ICONS, BUTTONS and DECOR, worded for this model: the object, its
# shape and colours; no "glowing", the negative prompt rules glow out)
ICONS = {
    "Coal": "a pile of black coal lumps with dark grey facets",
    "Food": "a wicker basket full of bread loaves, red apples and a wedge of yellow cheese",
    "Iron": "a stack of three dark grey iron ingots",
    "Silver": "a stack of silver coins with a silver ingot",
    "Essence": "a round glass flask of violet magic potion with a cork stopper",
    "Sparks": "one faceted blue-white crystal gem",
    "Blueprints": "two rolled blue blueprint scrolls tied with string",
    "Cores": "a faceted red crystal heart",
    "Ember": "a round orange ember stone with red-hot cracks",
    "Glory": "a golden laurel wreath with a golden star in the middle",
}
BUTTONS = {
    "Collect": "a heap of gold coins", "Fights": "a crossed steel sword and round wooden shield",
    "Growth": "an iron anvil with a blacksmith hammer", "Routes": "a leather travel bag with a pickaxe",
    "Quests": "a rolled parchment scroll with a red wax seal", "Shop": "a leather pouch full of gold coins",
    "Events": "a golden chalice", "Rewards": "an open wooden treasure chest full of gold coins",
    "Map": "a folded parchment map with a red cross", "Fortress": "a small stone castle with red roofs and flags",
}
DECOR = dict(aigen.DECOR)
DECOR.update({
    "candles": "a tall iron candelabrum with five lit candles", "crystal": "a large blue magic crystal on a stone base",
    "keg": "a wooden barrel full of red apples", "plant": "a green leafy plant in a clay pot",
    "rug": "an ornate red and gold rug lying flat, seen at an angle", "shelf": "a wooden wall shelf with bottles and jars",
})
OBSTACLES = {
    "Rubble": "a wide low heap of fallen grey-brown rocks, boulders and broken wooden planks, a collapsed rubble pile, "
              "side view",
    "Cradle": "a small wooden baby cradle on curved rockers with a white blanket and a little pillow, side view",
    "Arch": "a thick medieval wall pillar of warm sandstone blocks with an open round-arched doorway through it, front "
            "view, symmetric, the doorway a dark passage, a stone threshold",
    "Scaffold": "wooden construction scaffolding, front view, a frame of wooden poles and planks tied with ropes, "
                "a wooden ladder, a bucket hanging on a rope, sparse with empty gaps",
}
# the places around the fortress, like Hustle Castle's surface: tapping one opens its window (GameRunner.Scene)
SCENE = {
    "Merchant": "a medieval merchant's market stall tent with a striped red and cream canvas roof, crates, sacks and "
                "barrels of goods in front",
    "Portal": "a tall upright stone portal gate standing vertically, an arch of carved rune stones, a swirling "
              "blue-violet magic vortex filling the arch, two steps of a stone platform in front, front view",
    "Arena": "a small round wooden fighting arena with a palisade fence, colourful pennants and two crossed spears at the gate",
    "Board": "a wooden quest notice board on two posts with a small shingle roof, parchment notes pinned to it",
    "Pier": "a short wooden pier on posts with a small moored rowing boat with a furled sail",
    "Pen": "a small fenced wooden animal pen with a doghouse, a hay bale and a feeding trough",
}

# the pets (Kennel, GDD 8.1) and the heroes' companions (GDD 8.2): portraits for their windows
PETS = {
    "Wolf": "a young grey wolf sitting, friendly, thick fur",
    "Owl": "a brown owl with big amber eyes perched on a small branch",
    "CoalCat": "a black cat with glowing orange ember eyes and sooty fur, sitting",
    "Raven": "a black raven with glossy feathers standing",
    "BearCub": "a small round brown bear cub sitting",
    "MistFox": "a silver-white fox with a bushy tail wrapped in wisps of pale blue mist",
}
COMPANIONS = {
    "Squire": "a young medieval squire boy with a wooden shield and a short sword, full figure",
    "Wolfhound": "a big grey wolfhound dog with a leather collar, standing",
    "Falcon": "a real falcon bird, an animal not a person, perched with folded wings, a leather jess on its leg",
    "Firefly": "a big glowing coal firefly, an insect with a bright orange ember body and translucent wings",
}

# the heroes' equipment (GDD 8; Core/Items.cs ItemKind): one icon per kind, Items/<Kind>, drawn on a plate in
# the colour of the item's rarity once Art.json has "itemArt"
ITEMS = {
    "Mace": "a heavy iron flanged mace with a leather-wrapped grip",
    "Sword": "a straight steel longsword with a crossguard and a leather grip",
    "Axe": "a big double-bladed battle axe on a long wooden haft",
    "Bow": "one single wooden longbow standing upright with a taut bowstring, one bow only",
    "Censer": "a bronze censer on a chain with glowing coals inside and a wisp of smoke",
    "Staff": "a gnarled dark wooden mage staff topped with a smouldering ember crystal",
    "Shield": "a round wooden shield with an iron rim and an iron boss",
    "Bracers": "a pair of studded leather and iron bracers",
    "Quiver": "a leather quiver full of feathered arrows",
    "Tome": "an open thick magic spellbook with glowing runes on its pages, a book, not a chest",
    "Helmet": "an iron knight helmet with a nasal guard",
    "Hood": "a dark grey cloth hood with a short cape",
    "Armor": "a steel plate cuirass with shoulder pauldrons",
    "Jerkin": "a brown leather jerkin with laces and a belt",
    "Robe": "a long dark red mage robe with ember-coloured trim",
    "Boots": "a pair of sturdy leather boots with iron toe caps",
    "Amulet": "a silver amulet on a chain with a glowing amber stone",
}


def _piece_list():
    out = {}
    for n, what in ICONS.items():
        out["Icons/" + n] = ("a game resource icon: " + what, (256, 256))
    for n, what in BUTTONS.items():
        out["Buttons/" + n] = ("a game button icon: " + what, (256, 256))
    for n, what in DECOR.items():
        p = os.path.join(aigen.ART, "Decor", n + ".png")
        out["Decor/" + n] = ("a room decoration: " + what + ", seen from the front",
                             Image.open(p).size if os.path.exists(p) else (320, 320))
    for n, what in OBSTACLES.items():
        out["Rooms/" + n] = (what, {"Arch": (192, 512), "Cradle": (384, 384)}.get(n, (768, 384)))
    for n, what in PETS.items():
        out["Pets/" + n] = ("a cute pet for a fantasy game: " + what + ", full body, three-quarter view", (256, 256))
    for n, what in COMPANIONS.items():
        out["Companions/" + n] = ("a hero's companion for a fantasy game: " + what + ", three-quarter view", (256, 256))
    # a small ship that sails between the islands of the map (GameRunner.Map DrawShips)
    out["Map/Ship"] = ("a small wooden sailing ship with one white sail and a red pennant, seen from above at an angle, "
                       "a fantasy game map piece", (192, 192))
    for n, what in SCENE.items():
        out["Scene/" + n] = (what + ", three-quarter view from slightly above", (512, 512))
    for kind, what in ITEMS.items():
        out["Items/" + kind] = ("a game equipment icon: " + what + ", single item", (256, 256))
    return out


PIECES = _piece_list()
BACKDROP = ", single object, centered, isolated on a plain flat white background, no shadow, no ground"

_rembg = None


def _matte(img, bg, holes=False):
    """The piece's alpha: rembg's matte (isnet-general-use) when it is installed, else the background
    flooded from the borders; holes: also cut every patch of the background colour inside the piece
    (the gaps of the scaffolding)."""
    import numpy as np
    global _rembg
    alpha = None
    try:
        import rembg
        if _rembg is None:
            _rembg = rembg.new_session(os.environ.get("REMBG_MODEL", "isnet-general-use"))
        alpha = np.asarray(rembg.remove(img.convert("RGB"), session=_rembg).getchannel("A"), dtype=np.float32) / 255
    except Exception as e:  # noqa: BLE001  (no rembg, or its model could not be fetched: the flood fill)
        print("  rembg unavailable (%s), flood fill" % type(e).__name__, flush=True)
        alpha = np.asarray(_cutout(img).getchannel("A"), dtype=np.float32) / 255
    if holes:
        d = np.abs(np.asarray(img.convert("RGB"), dtype=np.float32) - bg).max(axis=2)
        alpha = np.minimum(alpha, np.clip((d - 14) / 26, 0, 1))
    alpha[alpha < 0.06] = 0
    return alpha


def _unmix(img, alpha, bg):
    """The colours at the soft edges without the background mixed in (no white fringe)."""
    import numpy as np
    c = np.asarray(img.convert("RGB"), dtype=np.float32)
    a = np.maximum(alpha, 1e-3)[..., None]
    c = np.clip((c - (1 - a) * bg) / a, 0, 255)
    rgba = np.dstack([c, alpha[..., None] * 255]).round().astype(np.uint8)
    return Image.fromarray(rgba, "RGBA")


def _place(img, size, pad=0.04, bottom=False):
    """The piece cropped to what it shows and fitted into its picture: centred, or standing on the bottom."""
    bbox = img.getchannel("A").point(lambda a: 255 if a > 8 else 0).getbbox() or (0, 0, img.width, img.height)
    p = img.crop(bbox)
    W, H = size
    k = min(W * (1 - 2 * pad) / p.width, H * (1 - 2 * pad) / p.height)
    p = p.resize((max(1, round(p.width * k)), max(1, round(p.height * k))), Image.LANCZOS)
    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    y = H - p.height - round(H * pad * 0.5) if bottom else (H - p.height) // 2
    out.alpha_composite(p, ((W - p.width) // 2, y))
    return out


def _ground(img):
    """The colour of the plain ground: the median of the picture's border."""
    import numpy as np
    c = np.asarray(img.convert("RGB"), dtype=np.float32)
    return np.median(np.concatenate([c[:8].reshape(-1, 3), c[-8:].reshape(-1, 3),
                                     c[:, :8].reshape(-1, 3), c[:, -8:].reshape(-1, 3)]), axis=0)


def piece(name, args):
    """A transparent piece: painted on a plain white ground, the ground cut away, the colours graded,
    trimmed to the object and scaled down into the game's size (painted large for smooth edges)."""
    prompt, size = PIECES[name]
    wide = size[0] >= size[1] * 1.6
    gen = (1216, 608) if wide else (1024, 1024)
    img = draw(prompt + BACKDROP, gen, steps=args.steps, seed=args.seed)
    alpha = _matte(img, _ground(img), holes=name.startswith("Rooms/"))
    graded = grade(img)
    out = _unmix(graded, alpha, _ground(graded))
    out = _place(out, size, pad=0.02 if wide else 0.05, bottom=wide or name.startswith("Scene/"))
    _save(out, name + ".png", args)


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
    groups = {"rooms": aigen.GROUPS["rooms"], "world": aigen.GROUPS["world"], "extras": ["Door", "Ladder", "Stairs", "Rooms/Arch"],
              "tiers": [r + "_" + t for r in aigen.GROUPS["rooms"] if r not in ("Empty", "Hearth") for t in ("2", "3")],
              "pieces": [n for n in PIECES if not n.startswith(("Scene/", "Pets/", "Companions/", "Items/"))],
              "scene": [n for n in PIECES if n.startswith("Scene/")],
              "pets": [n for n in PIECES if n.startswith(("Pets/", "Companions/"))],
              "items": [n for n in PIECES if n.startswith("Items/")]}
    for n in [x for a in args.names for x in groups.get(a, [a])]:
        if n in PIECES:
            piece(n, args)
        elif n.partition("_")[0] in aigen.ROOMS:
            room(n, args)
        elif n in aigen.WORLD:
            world(n, args)
        elif n == "Door":
            door(args)
        elif n == "Ladder":
            ladder(args)
        elif n == "Stairs":
            stairs(args)
        elif n == "Splash":
            splash(args)
        else:
            print("unknown:", n)
