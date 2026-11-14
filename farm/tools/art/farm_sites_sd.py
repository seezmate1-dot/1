"""«Солнечная долина»: the working farm's world art (Resources/Art/Farm) painted by the same free image model as the
chapter 1 zone backgrounds (Stable Diffusion XL, DreamShaper XL Turbo, hearth/tools/art/sdpaint.py), in the same style
prompt, so trees, animals, pens, workshops and crops look like they belong to the painted map.

Each picture keeps the file name and pixel size the game already uses (FarmSitesView places them by their feet), is
painted on white, cut out (rembg), graded like every other piece and given a soft contact shadow under its feet.
States of one thing (a tree in leaf, in bloom, with fruit; an animal and its «product ready» look) are repainted from
the first picture (image to image), so the shape stays and only the state changes.

    ~/sdenv/bin/python farm/tools/art/farm_sites_sd.py [names or groups: trees animals pens workshops crops bushes plot]
        [--out DIR] [--force]

Without --force a picture already painted into --out is skipped (the run can be resumed after a stop).
"""
import argparse
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
sys.path.insert(0, os.path.join(ROOT, "hearth", "tools", "art"))

import sdpaint  # noqa: E402
import torch  # noqa: E402
from PIL import Image, ImageDraw, ImageFilter  # noqa: E402
from diffusers import StableDiffusionXLImg2ImgPipeline  # noqa: E402

FARM = os.path.join(ROOT, "farm", "unity", "SunnyValley", "Assets", "SunnyValley", "Resources", "Art", "Farm")
RAW = os.path.join(ROOT, "farm", "tools", "art", "sd_raw")   # the painted pictures before cutting, for the repaints

# the chapter 1 style (the zone backgrounds), told to be a little more natural: detailed, but the same warm colours
STYLE = ("detailed semi-realistic mobile farm game art, Township style, soft warm sunlight from top left, "
         "natural textures, warm natural colors")
NEGATIVE = ("photo, photograph, dull colors, dark, gloomy, text, watermark, signature, blurry, lowres, frame, border, "
            "cropped, cut off, multiple objects, several, collage, grid, people, person, hands, deformed, ugly, "
            "flat vector, clipart, outline, black outline, cast shadow, ground plane, grass field, landscape, scenery")
ISO = "isometric view, "
BACKDROP = ", isolated on plain white background"
# said first (CLIP reads only 77 tokens): without it fields and pens came out as whole scenes
FRONT = "one single game asset isolated on a plain white background, "
# what each kind must not get: no ground disc under things that stand on the map's own grass
EXTRA_NEG = {"tree": ", grass base, round base, platform, island, disc, ground patch, exposed roots, pot",
             "bush": ", grass base, round base, platform, island, disc, pot",
             "animal": ", grass base, round base, platform, ground patch, pen, fence",
             "crop": ", grass base, round base, soil disc, sand disc, platform, pot, wooden box, raised bed, pineapple",
             "pen": "", "building": "", "plot": ", plants, grass base"}
NO_FRUIT = ", fruit, apples, pears, cherries, berries, flowers, blossoms"

TREE = {
    "apple_tree": ("a young deciduous tree with a short gnarled forked brown trunk and a broad, rounded, slightly uneven lush "
                   "green leafy crown made of several clusters of leaves", 11),
    "pear_tree": ("a tall slender deciduous tree with a straight trunk and an upright oval pointed crown of glossy dark green "
                  "leaves", 23),
    "cherry_tree": ("a wide spreading deciduous tree with a short trunk of smooth reddish brown bark and a broad flat "
                    "umbrella shaped crown of green leaves on long branches", 37),
}
BLOOM = {"apple_tree": "covered in white and pale pink apple blossoms among the leaves",
         "pear_tree": "covered in masses of pure white pear blossoms all over the crown",
         "cherry_tree": "covered in soft pink cherry blossoms among the leaves"}
RIPE = {"apple_tree": "with many shiny ripe red apples hanging among the green leaves",
        "pear_tree": "with many ripe yellow pears hanging among the green leaves",
        "cherry_tree": "with many clusters of shiny dark red cherries hanging in pairs among the green leaves"}

ANIMAL = {
    "chicken": ("a cute plump white farm hen with a red comb, standing, full body, side view", None, "sitting on a straw "
                "nest with a white egg beside her"),
    "cow": ("a cute black and white spotted dairy cow with small horns, standing, full body, side view", None,
            "with a metal milk bucket full of milk next to her"),
    "sheep": ("a cute fluffy white sheep with a dark face and dark legs, thick curly wool, standing, full body, side view",
              None, "with a fluffy ball of white wool next to her"),
    "pig": ("a cute pink farm pig with a curly tail, standing, full body, side view", None,
            "with a small basket of brown truffles next to it"),
    "bee": ("a wooden straw bee skep beehive on a small wooden stand with a few bees flying around", None,
            "with a glass jar of golden honey next to it"),
}

PEN = {
    "cowshed": "a small square fenced cow paddock on a patch of trampled earth with a little red wooden cowshed, a wooden "
               "water trough and hay bales",
    "sheepfold": "a small square fenced sheep pen on a patch of grass with a little wooden shelter with a straw roof and a "
                 "feeding rack of hay",
    "pigsty": "a small square fenced pigsty with a muddy puddle, a little wooden pig shed with a green roof and a feeding "
              "trough",
    "apiary": "a small square fenced apiary garden with flowers, three wooden bee hives on stands and a little shed",
}

WORKSHOP = {
    "bakery": "a cozy small country bakery building with a red tile roof, a brick chimney with smoke, a bread sign, "
              "flour sacks by the door, flower boxes under the windows",
    "dairy": "a small white country dairy building with a blue roof, a tall metal milk silo, milk cans by the door",
    "toolshed": "a small rustic wooden workshop shed with a dark green roof, a grindstone, an anvil and stacked firewood "
                "outside",
    "jam": "a small half-timbered jam kitchen cottage with a raspberry pink tile roof, a big jam jar sign, wooden "
           "barrels and a crate of apples by the door",
    "textile": "a small cottage weaving workshop with a purple roof, a wooden water wheel on the side and colorful "
               "cloth drying on a line",
    "sugar_mill": "a small country sugar mill building with a teal roof, a tall red brick chimney with smoke and "
                  "bundles of sugar cane by the door",
}

CROP = {
    "wheat": ["a few tiny green wheat sprouts", "a clump of young green wheat stalks", "a clump of tall wheat with "
              "green-gold ears", "a thick clump of ripe golden wheat with full ears and long awns"],
    "carrot": ["a few tiny carrot seedlings", "a clump of young feathery carrot leaves", "a clump of lush feathery "
               "carrot tops", "lush feathery carrot tops with bright orange carrots poking out of the soil"],
    "corn": ["a few tiny corn seedlings", "three young upright corn stalks with long narrow arching green leaves",
             "three tall upright green corn stalks with tassels on top",
             "three tall upright corn stalks with ripe yellow corn cobs in green husks"],
    "sunflower": ["a few tiny sunflower seedlings", "young green sunflower plants", "tall sunflower plants with closed "
                  "green buds", "tall sunflowers with big bright yellow blooming heads"],
    "strawberry": ["a few tiny strawberry seedlings", "a low clump of strawberry leaves", "a strawberry bush with white "
                   "flowers and green berries", "a lush strawberry bush with many ripe red strawberries"],
    "pumpkin": ["a few tiny pumpkin seedlings", "young sprawling pumpkin vines with big leaves", "pumpkin vines with "
                "yellow flowers and small green pumpkins", "pumpkin vines with big ripe orange pumpkins"],
    "beet": ["a few tiny beet seedlings", "a clump of young beet leaves with red stems", "lush beet leaves with red "
             "veins", "lush beet leaves with round dark red beetroots showing at the soil"],
    "potato": ["a few tiny potato sprouts", "a clump of young potato plants", "bushy potato plants with small white "
               "flowers", "bushy potato plants with brown potatoes dug out beside them"],
    "cotton": ["a few tiny cotton seedlings", "young green cotton plants", "cotton plants with green bolls",
               "cotton plants with fluffy white open cotton bolls"],
}


def jobs():
    """name → (prompt, base name or None, strength, seed, kind). The base is painted first; a repaint starts from it."""
    j = {}
    for t, (desc, seed) in TREE.items():
        for v, s in (("", seed), ("_2", seed + 100)):
            leaf = "Tree_%s_leaf%s" % (t, v)
            j[leaf] = (ISO + desc + ", green leaves only, no fruit, single tree", None, 1.0, s, "tree")
            j["Tree_%s_bloom%s" % (t, v)] = ("a blooming tree " + BLOOM[t] + ", " + ISO + desc, leaf, 0.88 if t == "pear_tree" else 0.8, s, "tree")
            j["Tree_%s_ripe%s" % (t, v)] = ("a fruit tree " + RIPE[t] + ", " + ISO + desc, leaf, 0.75, s, "tree")
    for a, (desc, _, ready) in ANIMAL.items():
        base = "Animal_" + a
        j[base] = (ISO + desc, None, 1.0, 5, "animal")
        j[base + "_ready"] = (ready[0].upper() + ready[1:] + ", " + ISO + desc, base, 0.6, 5, "animal")
    for p, desc in PEN.items():
        j["Pen_" + p] = (FRONT + desc + ", " + ISO + "the whole square enclosure in view, centered", None, 1.0, 9,
                         "pen")
    for w, desc in WORKSHOP.items():
        j["Workshop_" + w] = (FRONT + ISO + desc + ", standing on a small patch of earth and grass, single building, game "
                              "building asset", None, 1.0, 13, "building")
    for c, stages in CROP.items():
        for i, desc in enumerate(stages, 1):
            j["Crop_%s_%d" % (c, i)] = (FRONT + desc + ", one compact round clump, " + ISO + "centered", None, 1.0,
                                        17 + i, "crop")
    j["Seeded"] = (FRONT + "seven tiny green seedlings sprouting from little dark soil mounds in a small diamond "
                   "pattern, " + ISO + "centered", None, 1.0, 3, "crop")
    for i in (1, 2, 3):
        j["Bush%d" % i] = (FRONT + ISO + ["a round leafy green bush", "a low wide flowering bush with small pink flowers",
                                  "a leafy bush with small yellow flowers"][i - 1] + ", single bush", None, 1.0, 40 + i,
                           "bush")
    j["Plot"] = (FRONT + "an isometric diamond shaped garden bed of freshly tilled brown soil with furrows, short grass "
                 "tufts along the edges, bare soil, empty, no plants, no seedlings, centered", None, 1.0, 2, "plot")
    return j


GROUPS = {"trees": "Tree_", "animals": "Animal_", "pens": "Pen_", "workshops": "Workshop_", "crops": ("Crop_", "Seeded"),
          "bushes": "Bush", "plot": "Plot"}


BG = os.path.join(ROOT, "farm", "unity", "SunnyValley", "Assets", "SunnyValley", "Resources", "Art", "Ch1", "Backgrounds")
# the style reference per kind: the zone background that shows that kind of thing (IP-Adapter copies its look)
STYLE_REF = {"tree": "Orchard", "bush": "Orchard", "animal": "Coop", "pen": "Barn", "building": "House",
             "crop": "Garden", "plot": "Garden"}
IPA = os.path.expanduser("~/models/ip-adapter")
IPA_SCALE = float(os.environ.get("IPA_SCALE", "0"))   # 0: off (at 0.45 it copies whole bits of the scene)
_t2i = None


def t2i_pipe():
    """SDXL (the same checkpoint as sdpaint) with IP-Adapter: the picture takes the look of a zone background."""
    global _t2i
    if _t2i is None:
        from diffusers import DPMSolverMultistepScheduler, StableDiffusionXLPipeline
        from transformers import CLIPVisionModelWithProjection
        torch.set_num_threads(os.cpu_count() or 4)
        if IPA_SCALE <= 0:
            p = StableDiffusionXLPipeline.from_single_file(sdpaint.MODEL, torch_dtype=torch.bfloat16)
            p.scheduler = DPMSolverMultistepScheduler.from_config(p.scheduler.config, algorithm_type="sde-dpmsolver++",
                                                                  use_karras_sigmas=True)
            p.set_progress_bar_config(disable=True)
            _t2i = p
            return p
        enc = CLIPVisionModelWithProjection.from_pretrained(os.path.join(IPA, "models", "image_encoder"),
                                                            torch_dtype=torch.bfloat16)
        p = StableDiffusionXLPipeline.from_single_file(sdpaint.MODEL, torch_dtype=torch.bfloat16, image_encoder=enc)
        p.load_ip_adapter(IPA, subfolder="sdxl_models", weight_name="ip-adapter_sdxl_vit-h.safetensors",
                          image_encoder_folder=None)
        p.set_ip_adapter_scale(IPA_SCALE)
        p.scheduler = DPMSolverMultistepScheduler.from_config(p.scheduler.config, algorithm_type="sde-dpmsolver++",
                                                              use_karras_sigmas=True)
        p.set_progress_bar_config(disable=True)
        _t2i = p
    return _t2i


def style_ref(kind):
    return Image.open(os.path.join(BG, STYLE_REF[kind] + ".jpg")).convert("RGB")


def _ipa(kind):
    return {"ip_adapter_image": style_ref(kind)} if IPA_SCALE > 0 else {}


def white_border(img):
    """Share of the picture's edge that is plain white: a scene (field, landscape) fills the edge, an asset does not."""
    import numpy as np
    a = np.asarray(img.convert("RGB")).astype(int)
    m = max(4, a.shape[0] // 40)
    edge = np.concatenate([a[:m].reshape(-1, 3), a[-m:].reshape(-1, 3), a[:, :m].reshape(-1, 3), a[:, -m:].reshape(-1, 3)])
    return float(((edge.min(axis=1) > 215) & (edge.max(axis=1) - edge.min(axis=1) < 25)).mean())


def paint(fn, seed, tries=4):
    """fn(seed) → picture; repainted with another seed while its edge is not white."""
    best, score = None, -1.0
    for k in range(tries):
        img = fn(seed + 1000 * k)
        w = white_border(img)
        if w > score:
            best, score = img, w
        if w >= 0.8:
            break
        print("  edge %.2f white, another seed" % w, flush=True)
    return best


def draw(prompt, size, seed, negative, kind):
    w, h = (size[0] // 8) * 8, (size[1] // 8) * 8
    g = torch.Generator().manual_seed(seed)
    return t2i_pipe()(prompt=prompt + ", " + STYLE, negative_prompt=negative, **_ipa(kind),
                      num_inference_steps=8, guidance_scale=2.0, generator=g, width=w, height=h).images[0]


def img2img(init, prompt, size, strength, seed, negative=NEGATIVE, kind="tree"):
    w, h = (size[0] // 8) * 8, (size[1] // 8) * 8
    g = torch.Generator().manual_seed(seed)
    p = StableDiffusionXLImg2ImgPipeline(**t2i_pipe().components)
    p.set_progress_bar_config(disable=True)
    return p(prompt=prompt + ", " + STYLE, negative_prompt=negative, **_ipa(kind),
             image=init.convert("RGB").resize((w, h), Image.LANCZOS), strength=strength,
             num_inference_steps=max(8, int(round(2 / strength))), guidance_scale=2.0, generator=g).images[0]


def _old_img2img(init, prompt, size, strength, seed, negative=NEGATIVE):
    w, h = (size[0] // 8) * 8, (size[1] // 8) * 8
    e = sdpaint.embeds(prompt + ", " + STYLE, negative)
    g = torch.Generator().manual_seed(seed)
    return sdpaint.painter(StableDiffusionXLImg2ImgPipeline)(
        **e, image=init.convert("RGB").resize((w, h), Image.LANCZOS), strength=strength,
        num_inference_steps=max(8, int(round(2 / strength))), guidance_scale=2.0, generator=g, width=w, height=h).images[0]


def contact_shadow(img, kind):
    """A soft shadow under the feet (light from the top left: the shadow leans a little to the right), the art bible's
    contact shadow, under the piece."""
    import numpy as np
    a = np.asarray(img.getchannel("A"))
    rows = np.where(a.max(axis=1) > 40)[0]
    if not len(rows):
        return img
    bottom = rows[-1]
    band = a[max(0, bottom - img.height // 10):bottom + 1]
    cols = np.where(band.max(axis=0) > 40)[0]
    x0, x1 = cols[0], cols[-1]
    wide = {"tree": 0.9, "animal": 0.95, "bush": 1.0, "crop": 1.0, "pen": 0.0, "building": 0.0, "plot": 0.0}[kind]
    if wide == 0:
        return img
    x0, x1, bottom = float(x0), float(x1), float(bottom)
    cx, w = (x0 + x1) / 2 + (x1 - x0) * 0.06, (x1 - x0) * wide
    h = max(8.0, w * 0.28)
    sh = Image.new("L", img.size, 0)
    ImageDraw.Draw(sh).ellipse((cx - w / 2, bottom - h * 0.6, cx + w / 2, bottom + h * 0.4), fill=110)
    sh = sh.filter(ImageFilter.GaussianBlur(max(3, h * 0.25)))
    shadow = Image.new("RGBA", img.size, (14, 32, 16, 0))
    shadow.putalpha(sh)
    out = Image.new("RGBA", img.size, (0, 0, 0, 0))
    out.alpha_composite(shadow)
    out.alpha_composite(img)
    return out


def cut_base(alpha, kind):
    """The model likes to stand a tree or a bush on a disc or a tile of ground: cut everything below the trunk where
    the silhouette suddenly widens again (the map has its own grass)."""
    import numpy as np
    if kind not in ("tree", "bush"):
        return alpha
    rows = np.where((alpha > 0.3).any(axis=1))[0]
    if not len(rows):
        return alpha
    top, bot = rows[0], rows[-1]
    hgt = bot - top
    width = np.array([(alpha[r] > 0.3).sum() for r in range(alpha.shape[0])])
    lo, hi = top + int(hgt * 0.45), top + int(hgt * 0.95)
    if hi <= lo:
        return alpha
    narrow = lo + int(np.argmin(width[lo:hi]))
    trunk = max(4, width[narrow])
    cut = None
    for r in range(narrow, bot + 1):
        if width[r] > trunk * 3.2 and width[min(bot, r + 6)] > trunk * 3.2:
            cut = r
            break
    if cut is None:
        return alpha
    out = alpha.copy()
    out[cut + 4:] = 0
    # roots fade out over a few pixels
    for i in range(4):
        out[cut + i] *= 1 - (i + 1) / 5
    return out


def palette_match(img, alpha, kind, amount=0.5):
    """Moves the piece's colours half way to the zone background's (mean and spread per Lab channel, over the
    piece's own pixels), so it is painted in the map's palette."""
    import numpy as np
    from skimage import color
    ref = np.asarray(style_ref(kind).resize((400, 300)), dtype=np.float32) / 255
    rl = color.rgb2lab(ref).reshape(-1, 3)
    src = np.asarray(img.convert("RGB"), dtype=np.float32) / 255
    lab = color.rgb2lab(src)
    m = alpha > 0.5
    if m.sum() < 100:
        return img
    px = lab[m]
    mu, sd = px.mean(0), px.std(0) + 1e-3
    rmu, rsd = rl.mean(0), rl.std(0) + 1e-3
    # lightness keeps its own spread (the object's form), colour moves toward the map's
    target = (lab - mu) * np.array([1.0, *(rsd[1:] / sd[1:])]) + np.array([mu[0], *rmu[1:]])
    out = lab * (1 - amount) + target * amount
    out[..., 0] = lab[..., 0]
    rgb = np.clip(color.lab2rgb(out), 0, 1)
    return Image.fromarray((rgb * 255).round().astype(np.uint8), "RGB")


def finish(raw, size, kind):
    alpha = cut_base(sdpaint._matte(raw, sdpaint._ground(raw)), kind)
    graded = palette_match(sdpaint.grade(raw), alpha, kind, float(os.environ.get("PALETTE", "0.25")))
    cut = sdpaint._unmix(graded, alpha, sdpaint._ground(graded))
    # room at the bottom for the shadow
    placed = sdpaint._place(cut, size, pad=0.05, bottom=True)
    return contact_shadow(placed, kind)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("names", nargs="*", default=["trees", "animals", "workshops", "pens", "crops", "bushes", "plot"])
    ap.add_argument("--out", default=FARM)
    ap.add_argument("--force", action="store_true")
    args = ap.parse_args()
    allj = jobs()
    order = []
    for n in args.names:
        pref = GROUPS.get(n)
        picked = [k for k in allj if pref and k.startswith(pref)] or ([n] if n in allj else [])
        if not picked:
            print("unknown:", n)
        order += [k for k in picked if k not in order]
    os.makedirs(RAW, exist_ok=True)
    for name in order:
        prompt, base, strength, seed, kind = allj[name]
        target = os.path.join(args.out, name + ".png")
        raw_path = os.path.join(RAW, name + ".png")
        if os.path.exists(target) and not args.force and args.out != FARM:
            print("skip", name, flush=True)
            continue
        size = Image.open(os.path.join(FARM, name + ".png")).size
        gen = (1216, 832) if size[0] > size[1] * 1.25 else (1024, 1024) if size[0] >= size[1] * 0.8 else (896, 1152)
        neg = NEGATIVE + EXTRA_NEG[kind] + (NO_FRUIT if name.startswith("Tree_") and "_leaf" in name else "")
        if os.path.exists(raw_path) and not args.force:
            raw = Image.open(raw_path).convert("RGB")
        elif base:
            braw = os.path.join(RAW, base + ".png")
            if not os.path.exists(braw):
                print("  base missing for", name, "- painting from text", flush=True)
                raw = paint(lambda sd: draw(prompt + BACKDROP, gen, sd, neg, kind), seed)
            else:
                raw = paint(lambda sd: img2img(Image.open(braw), prompt + BACKDROP, gen, strength, sd, neg, kind), seed)
        else:
            raw = paint(lambda sd: draw(prompt + BACKDROP, gen, sd, neg, kind), seed)
        if not os.path.exists(raw_path) or args.force:
            raw.save(raw_path)
        finish(raw, size, kind).save(target)
        print("painted", name, size, flush=True)


if __name__ == "__main__":
    main()
