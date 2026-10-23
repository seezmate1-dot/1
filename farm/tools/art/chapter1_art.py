"""«Солнечная долина», chapter 1 «Восстановление»: every picture the farm scene needs and where it stands.

One table per zone (below) is the single source of two files:

- farm/tools/art/chapter1_art.json — the art manifest read by hearth/tools/art/sdpaint.py (FARM_CH1 and
  FARM_CH1_BACKGROUNDS there): every name "Farm/Ch1/<Group>/<Name>" with its English prompt, kind and size.
  Kinds: "background" (painted as a full opaque picture, saved .jpg, not cut out), "object", "look", "before",
  "junk", "fx", "portrait" (painted on white and cut out like the other farm pieces).
- farm/unity/SunnyValley/Assets/SunnyValley/Resources/Meta/chapter1_layout.json — the placement the game reads
  (FarmWorld): the map, each zone's rectangle on it, its background and every sprite's position, size and sort
  order in the zone's own coordinates (1600×1200, origin at the lower left, a sprite stands on its bottom centre),
  which task shows it ("after"), hides it ("before": broken things and junk cleared by the task) or swaps it for a
  look of the player's choice ("looks"), plus a placeholder shape the game paints until the art exists.

    python3 farm/tools/art/chapter1_art.py           # writes both files
    python3 farm/tools/art/chapter1_art.py --check   # fails if they are stale or a task has no picture
    python3 farm/tools/art/chapter1_art.py --install [DIR]   # copies painted DIR/Farm/Ch1/** (default: the sd-art.yml
                                                             # staging folder hearth/tools/art/ai/sd) into Resources/Art/Ch1

Painting (on GitHub, sd-art.yml): sdpaint.py farm-ch1 (all), farm-ch1-bg, farm-ch1-objects, farm-ch1-portraits;
the pictures land in Farm/Ch1/... and go to the game as Resources/Art/Ch1/... (the "Farm/" prefix dropped, like
the match-3 pieces).
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
FARM = os.path.dirname(os.path.dirname(HERE))
META = os.path.join(FARM, "unity", "SunnyValley", "Assets", "SunnyValley", "Resources", "Meta")
MANIFEST = os.path.join(HERE, "chapter1_art.json")
LAYOUT = os.path.join(META, "chapter1_layout.json")
CHAPTER = os.path.join(META, "chapter1.json")

ZW, ZH = 1600, 1200          # a zone in its own units (= map units)
MARGIN = 400                 # grass around the 3×3 zones
COLS, ROWS = 3, 3

# the wording every chapter 1 prompt shares (sdpaint adds its style after it)
ISO = "isometric three-quarter view from above, Township style"
BG_SIZE = (1600, 1200)

# ---------------------------------------------------------------------------------------------------- the zones
# Each zone: its cell on the 3×3 map (col, row from the lower left), the background prompt, placeholder ground
# shapes, the camera point and the sprites. A sprite: (name, kind, prompt, task, show, x, y, w, h, ph, color, extra)
# written as dicts for clarity; "looks" lists (choice id, name, prompt) for tasks with three looks.


def S(art, kind, prompt, task, show, x, y, w, h, ph, color, z=0, until=None, looks=None, tap=False, flat=False):
    return dict(art=art, kind=kind, prompt=prompt, task=task, show=show, x=x, y=y, w=w, h=h, ph=ph, color=color, z=z,
                until=until, looks=looks, tap=tap, flat=flat)


ZONES = [
    # ------------------------------------------------------------------ Двор: the gate at the bottom, the path up to the house
    dict(id="yard", cell=(1, 0), name="Yard", camera=(800, 520),
         bg="an overgrown farmyard of trampled grass and patches of bare earth, a faint dirt track running from the bottom "
            "center straight up to the top center, a few stones, nothing built, empty ground",
         ground="#8CCB5E",
         ph=[("rect", 800, 600, 1600, 1200, "#8CCB5E"), ("rect", 800, 640, 260, 1120, "#C9B07A"),
             ("ellipse", 300, 900, 420, 260, "#7DBE52"), ("ellipse", 1300, 820, 380, 240, "#7DBE52")],
         sprites=[
             S("Before/GateFallen", "before", "a broken wooden farm gate lying fallen flat on the grass, cracked boards, one leaning post",
               "yard_gate", "before", 800, 70, 440, 220, "gate_broken", "#8A6A44"),
             S("Objects/Gate", "object", "a sturdy wooden farm gate with two square posts, the two wings swung open, a small roof over it",
               "yard_gate", "after", 800, 60, 460, 360, "gate", "#B98C5A"),
             S("Junk/WeedsTall", "junk", "a clump of tall overgrown wild weeds and burdock, dry grass, thistles",
               "yard_weeds", "before", 560, 380, 380, 260, "weeds", "#5E8F3A"),
             S("Junk/WeedsTall", "junk", "", "yard_weeds", "before", 1060, 420, 380, 260, "weeds", "#5E8F3A"),
             S("Junk/WeedsBush", "junk", "a thick overgrown bush of nettles and weeds with dry stalks",
               "yard_weeds", "before", 780, 620, 360, 240, "weeds", "#6B9A3E"),
             S("Looks/PathStone", "look", "", "yard_path", "after", 800, 260, 320, 880, "path", "#BDB7A8", flat=True, tap=True,
               looks=[("look1", "Looks/PathStone", "a long straight walkway of big grey flat stone slabs set in green grass, the walkway fills the whole picture from bottom to top"),
                      ("look2", "Looks/PathBrick", "a long straight walkway of red bricks in a herringbone pattern set in green grass, the walkway fills the whole picture from bottom to top"),
                      ("look3", "Looks/PathWood", "a long straight walkway of big round wooden log slices set in green grass, the walkway fills the whole picture from bottom to top")]),
             S("Junk/JunkPile", "junk", "a pile of old farm junk: broken crates, an old tire, a rusty bucket, rotten boards",
               "yard_junk", "before", 1260, 280, 420, 320, "junk", "#8A7A66"),
             S("Junk/JunkHeap", "junk", "a heap of rubbish: an old broken chair, scrap metal, a torn sack, a cracked barrel",
               "yard_junk", "before", 330, 720, 380, 300, "junk", "#7E6E5A"),
             S("Before/FenceBroken", "before", "a short section of a broken old wooden fence, leaning and missing boards",
               "yard_fence", "before", 400, 30, 520, 220, "fence_broken", "#8A6A44"),
             S("Before/FenceBroken", "before", "", "yard_fence", "before", 1200, 30, 520, 220, "fence_broken", "#8A6A44"),
             S("Before/FenceBroken", "before", "", "yard_fence", "before", 150, 560, 300, 200, "fence_broken", "#8A6A44"),
             S("Before/FenceBroken", "before", "", "yard_fence", "before", 1450, 560, 300, 200, "fence_broken", "#8A6A44"),
         ] + [S("Looks/FenceWood", "look", "", "yard_fence", "after", x, y, w, 220, "fence", "#C8955A", tap=True,
                looks=[("look1", "Looks/FenceWood", "a short straight section of a neat wooden plank fence with posts"),
                       ("look2", "Looks/FenceWattle", "one short straight section of a rustic fence of thin bent branches woven horizontally between upright wooden stakes"),
                       ("look3", "Looks/FencePicket", "a short straight section of a white picket fence with pointed tops")])
              for x, y, w in ((400, 30, 520), (1200, 30, 520), (150, 560, 300), (1450, 560, 300))] + [
             S("Objects/Bench", "object", "a cozy wooden garden bench with a curved back", "yard_bench", "after", 560, 200, 260, 180, "bench", "#B07A44"),
             S("Objects/Doghouse", "object", "a small wooden dog kennel with a red pitched roof and a round door",
               "yard_doghouse", "after", 1240, 600, 300, 280, "shed", "#C0703A"),
             S("Objects/Sharik", "object", "a cute shaggy friendly brown farm dog sitting, wagging its tail",
               "yard_doghouse", "after", 1060, 580, 170, 160, "animal", "#8A5A2B"),
             S("Objects/Lamp", "object", "an old-fashioned iron lantern on a tall wooden post, warm light inside",
               "yard_lamp", "after", 1100, 90, 140, 380, "post", "#4A4A4A"),
             S("Objects/FlowerBed", "object", "a long low flower bed full of colorful blooming flowers: daisies, tulips and marigolds",
               "yard_flowers", "after", 400, 150, 420, 150, "flowers", "#FF7AA0"),
             S("Objects/FlowerBed", "object", "", "yard_flowers", "after", 1200, 150, 420, 150, "flowers", "#FF7AA0"),
         ]),

    # ------------------------------------------------------------------ Дом: the house in the middle, porch to the front, veranda right
    dict(id="house", cell=(1, 1), name="House", camera=(760, 560),
         bg="a grassy plot with a flat bare dirt clearing in the middle where a farmhouse would stand, a small wooden "
            "veranda floor area to the right, a dirt path coming in from the bottom center, overgrown edges, nothing built",
         ground="#86C95A",
         ph=[("rect", 800, 600, 1600, 1200, "#86C95A"), ("ellipse", 760, 520, 1100, 700, "#C9B07A"),
             ("rect", 800, 90, 260, 180, "#C9B07A"), ("rect", 1280, 300, 420, 260, "#B98C5A")],
         sprites=[
             S("Before/HouseOld", "before", "an old abandoned wooden farmhouse with a sagging roof full of holes, broken boarded windows, peeling paint",
               "house_roof", "before", 760, 300, 900, 780, "house_old", "#A08A6A", z=-1),
             S("Looks/HouseRoofRed", "look", "", "house_roof", "after", 760, 300, 900, 780, "house", "#D9573A", z=-1, tap=True,
               looks=[("look1", "Looks/HouseRoofRed", "a cozy restored wooden farmhouse with a red tiled roof, white window frames, a chimney"),
                      ("look2", "Looks/HouseRoofGreen", "a cozy restored wooden farmhouse with a green metal sheet roof, white window frames, a chimney"),
                      ("look3", "Looks/HouseRoofThatch", "a cozy restored wooden farmhouse with a golden thatched straw roof, white window frames, a chimney")]),
             S("Junk/Cobwebs", "junk", "one big grey dusty spider web cobweb with a few clumps of grey dust, only the web, no building", "house_dust", "before", 760, 760, 360, 220, "dust", "#D8D8D8", z=3),
             S("Before/WindowBoarded", "before", "a broken house window boarded up with crossed planks", "house_windows", "before",
               470, 480, 170, 170, "window_broken", "#7A5A3A", z=1),
             S("Before/WindowBoarded", "before", "", "house_windows", "before", 1050, 480, 170, 170, "window_broken", "#7A5A3A", z=1),
             S("Objects/Window", "object", "a clean house window with shiny glass panes and a white wooden frame", "house_windows", "after",
               470, 480, 170, 170, "window", "#9ED9F6", z=1),
             S("Objects/Window", "object", "", "house_windows", "after", 1050, 480, 170, 170, "window", "#9ED9F6", z=1),
             S("Objects/Curtains", "object", "pretty red and white checkered curtains tied at the sides of a window", "house_curtains", "after",
               470, 480, 170, 170, "curtains", "#E8505A", z=2),
             S("Objects/Curtains", "object", "", "house_curtains", "after", 1050, 480, 170, 170, "curtains", "#E8505A", z=2),
             S("Objects/Door", "object", "a new green wooden front door with a brass handle and a small window", "house_door", "after",
               760, 330, 170, 260, "door", "#4E9A54", z=1),
             S("Before/PorchBroken", "before", "broken rotten wooden porch steps with missing boards", "house_porch", "before",
               760, 190, 420, 200, "porch_broken", "#8A6A44", z=2),
             S("Looks/PorchWood", "look", "", "house_porch", "after", 760, 180, 440, 240, "porch", "#C8955A", z=2, tap=True,
               looks=[("look1", "Looks/PorchWood", "wooden porch steps with a little railing"),
                      ("look2", "Looks/PorchStone", "stone porch steps with a low stone railing"),
                      ("look3", "Looks/PorchFlowers", "wooden porch steps with flower pots and climbing roses on the railing")]),
             S("Fx/ChimneySmoke", "fx", "a single soft curl of white cartoon smoke puffs rising upward, only the smoke, no ground, no island, no house", "house_stove", "after", 1020, 1010, 180, 190, "smoke", "#F2F2F2", z=5),
             S("Objects/GrandmaChest", "object", "an old open wooden chest bound with iron, with a notebook, a shawl and seed bags inside",
               "house_chest", "after", 330, 200, 230, 180, "chest", "#9A6234", z=3),
             S("Objects/VerandaTable", "object", "a wooden table covered with an embroidered tablecloth, a samovar, cups and a plate of pies",
               "house_table", "after", 1300, 230, 380, 280, "table", "#E9CF9A"),
             S("Looks/SignCarved", "look", "", "house_sign", "after", 180, 60, 280, 300, "sign", "#B07A44", z=4, tap=True,
               looks=[("look1", "Looks/SignCarved", "a carved wooden farm signboard on two posts, a plain empty board with no letters, a carved sun on top"),
                      ("look2", "Looks/SignPainted", "a brightly painted wooden signboard on two posts, a plain empty board with no letters, painted flowers around its edge"),
                      ("look3", "Looks/SignForged", "a plain empty wooden board with no letters hanging on chains from a black wrought iron bracket with curls on a post")]),
         ]),

    # ------------------------------------------------------------------ Огород: beds behind the house
    dict(id="garden", cell=(0, 1), name="Garden", camera=(760, 520),
         bg="a large flat field of bare brown earth surrounded by grass, a small trodden path along the right edge, nothing planted, "
            "empty ground",
         ground="#86C95A",
         ph=[("rect", 800, 600, 1600, 1200, "#86C95A"), ("ellipse", 720, 420, 1180, 640, "#A9824F")],
         sprites=[
             S("Objects/BedsDug", "object", "three long garden beds of freshly dug dark brown soil side by side, empty, the beds fill the whole picture", "garden_dig", "after",
               720, 140, 940, 520, "beds", "#6E4A2A", flat=True),
             S("Looks/BedBordersPine", "look", "", "garden_beds", "after", 720, 130, 980, 560, "borders", "#C8955A", flat=True, z=1, tap=True,
               looks=[("look1", "Looks/BedBordersPine", "garden beds framed with neat pine board borders, empty soil"),
                      ("look2", "Looks/BedBordersStone", "garden beds framed with borders of round field stones, empty soil"),
                      ("look3", "Looks/BedBordersWattle", "garden beds framed with low woven wicker borders, empty soil")]),
             S("Objects/WheatSprouts", "object", "three long garden beds of dark brown soil side by side with dense rows of bright green young sprouts, the beds fill the whole picture", "garden_sow", "after",
               720, 160, 880, 480, "sprouts", "#7CC24A", flat=True, z=2),
             S("Junk/WeedsGarden", "junk", "a dense patch of overgrown garden weeds, dandelions and couch grass",
               "garden_clear", "before", 480, 380, 440, 280, "weeds", "#5E8F3A"),
             S("Junk/WeedsGarden", "junk", "", "garden_clear", "before", 960, 330, 440, 280, "weeds", "#5E8F3A"),
             S("Junk/WeedsBush", "junk", "", "garden_clear", "before", 720, 620, 400, 260, "weeds", "#6B9A3E"),
             S("Objects/Compost", "object", "a wooden compost box full of leaves and grass clippings", "garden_compost", "after",
               1380, 720, 300, 240, "crate", "#7A5A32"),
             S("Before/BarrelBroken", "before", "a collapsed wooden barrel fallen apart: loose curved wooden staves in a heap on the ground and rusty iron hoops lying beside", "garden_water", "before",
               1340, 280, 220, 220, "barrel_broken", "#8A6A44"),
             S("Objects/RainBarrel", "object", "a wooden rain barrel full of water with a tap and a watering can beside it", "garden_water", "after",
               1340, 280, 220, 240, "barrel", "#9A6234"),
             S("Objects/SeedBox", "object", "a small wooden box of paper seed packets", "garden_seeds", "after", 1180, 120, 200, 150, "crate", "#C8955A"),
             S("Looks/ScarecrowHat", "look", "", "garden_scarecrow", "after", 720, 520, 240, 400, "scarecrow", "#D9A040", z=3, tap=True,
               looks=[("look1", "Looks/ScarecrowHat", "a garden scarecrow on a wooden cross pole: a burlap sack head with a stitched smile, a straw hat, a patched shirt, straw sticking out of the sleeves"),
                      ("look2", "Looks/ScarecrowScarf", "a garden scarecrow on a wooden cross pole: a burlap sack head with a stitched smile, a flowery headscarf, an apron, straw sticking out of the sleeves"),
                      ("look3", "Looks/ScarecrowBucket", "a garden scarecrow on a wooden cross pole: a burlap sack head with a stitched smile and a tin bucket for a hat, straw sticking out of the sleeves")]),
             S("Objects/Greenhouse", "object", "a small greenhouse made of glass panes in a white wooden frame, with seedlings inside",
               "garden_greenhouse", "after", 280, 720, 460, 400, "greenhouse", "#BFE8F0"),
         ]),

    # ------------------------------------------------------------------ Сад: three old apple trees, a trail, the gazebo
    dict(id="orchard", cell=(0, 2), name="Orchard", camera=(800, 560),
         bg="the grassy ground of an old orchard with fallen leaves and a few fallen apples, dappled shade, no trees, empty ground",
         ground="#7DBE52",
         ph=[("rect", 800, 600, 1600, 1200, "#7DBE52"), ("ellipse", 400, 300, 520, 300, "#6FAF48"),
             ("ellipse", 1200, 900, 600, 300, "#6FAF48")],
         sprites=[
             S("Objects/Trail", "object", "a long winding sandy trail edged with small stones curving through green grass, the trail fills the whole picture from bottom to top", "orchard_trail", "after",
               800, 40, 320, 760, "path", "#C9B07A", flat=True),
             S("Junk/Thicket", "junk", "a big dense clump of wild round green bushes, thorny brambles and dry branches",
               "orchard_trail", "before", 800, 120, 620, 380, "weeds", "#4E7A30", z=5),
             S("Junk/Thicket", "junk", "", "orchard_trail", "before", 560, 560, 520, 340, "weeds", "#4E7A30", z=5),
         ] + [s for (x, y) in ((420, 480), (1080, 460), (780, 780)) for s in (
             S("Before/AppleTreeDry", "before", "an old dead apple tree with bare grey twisted dry branches, no leaves, a few brown fallen leaves on the ground",
               "orchard_prune", "before", x, y, 420, 480, "tree_dry", "#7A6A4A"),
             S("Objects/AppleTree", "object", "a neatly pruned old apple tree with a round green crown", "orchard_prune", "after",
               x, y, 420, 480, "tree", "#4E9A34", until="orchard_whitewash"),
             S("Objects/AppleTreeWhite", "object", "a neat old apple tree with a round green crown and a whitewashed trunk",
               "orchard_whitewash", "after", x, y, 420, 480, "tree", "#4E9A34", until="orchard_bloom"),
             S("Objects/AppleTreeBloom", "object", "an old apple tree in full bloom covered with pink and white blossoms, whitewashed trunk",
               "orchard_bloom", "after", x, y, 420, 480, "tree_bloom", "#F4B6C8"))] + [
             S("Looks/SwingBoard", "look", "", "orchard_swing", "after", 1250, 420, 200, 300, "swing", "#B07A44", z=1, tap=True,
               looks=[("look1", "Looks/SwingBoard", "a simple wooden board swing hanging on two ropes from a branch"),
                      ("look2", "Looks/SwingTire", "an old tire swing hanging on a rope from a branch"),
                      ("look3", "Looks/SwingChair", "a woven wicker chair swing hanging on ropes from a branch")]),
             S("Objects/Beehive", "object", "an old wooden beehive box on legs with a few bees around", "orchard_hive", "after",
               200, 240, 200, 220, "crate", "#E8C040"),
             S("Objects/YoungAppleTrees", "object", "a row of three young apple saplings tied to wooden stakes", "orchard_graft", "after",
               320, 880, 460, 280, "saplings", "#6CC04A"),
             S("Looks/GazeboWood", "look", "", "orchard_gazebo", "after", 1300, 820, 460, 460, "gazebo", "#C8955A", tap=True,
               looks=[("look1", "Looks/GazeboWood", "an open hexagonal wooden garden gazebo with benches inside and a shingle roof"),
                      ("look2", "Looks/GazeboGrape", "an open wooden garden gazebo overgrown with green grape vines and bunches of grapes"),
                      ("look3", "Looks/GazeboColumns", "an elegant round garden gazebo with white columns and a domed roof")]),
             S("Objects/Wattle", "object", "a short straight section of a rustic woven wattle fence with clay pots on the stakes",
               "orchard_wattle", "after", 420, 20, 620, 220, "fence", "#A07A44"),
             S("Objects/Wattle", "object", "", "orchard_wattle", "after", 1180, 20, 620, 220, "fence", "#A07A44"),
         ]),

    # ------------------------------------------------------------------ Пруд: the water in the middle
    dict(id="pond", cell=(0, 0), name="Pond", camera=(800, 600),
         bg="a grassy shore around a large oval pond of clear blue water in the middle of the picture, sandy edges, a few stones, "
            "nothing built",
         ground="#86C95A",
         ph=[("rect", 800, 600, 1600, 1200, "#86C95A"), ("ellipse", 800, 620, 1120, 640, "#E3CF94"),
             ("ellipse", 800, 630, 1000, 560, "#5CB8E8"), ("ellipse", 700, 700, 360, 140, "#8FD3F5")],
         sprites=[
             S("Junk/Reeds", "junk", "a dense overgrown clump of tall brown cattail reeds and sedge", "pond_reeds", "before",
               380, 400, 380, 300, "reeds", "#7A8A3A", z=4),
             S("Junk/Reeds", "junk", "", "pond_reeds", "before", 1220, 420, 380, 300, "reeds", "#7A8A3A", z=4),
             S("Junk/Reeds", "junk", "", "pond_reeds", "before", 800, 250, 400, 300, "reeds", "#7A8A3A", z=4),
             S("Junk/PondTrash", "junk", "floating rubbish in water: an old tire, plastic bottles, a broken crate and a boot",
               "pond_trash", "before", 700, 600, 320, 170, "junk", "#6E6E5A"),
             S("Junk/PondTrash", "junk", "", "pond_trash", "before", 990, 540, 300, 160, "junk", "#6E6E5A"),
             S("Before/BridgeBroken", "before", "a small broken wooden footbridge with missing planks and a fallen railing",
               "pond_bridge", "before", 1300, 700, 420, 260, "bridge_broken", "#8A6A44"),
             S("Looks/BridgeWood", "look", "", "pond_bridge", "after", 1300, 690, 440, 280, "bridge", "#C8955A", tap=True,
               looks=[("look1", "Looks/BridgeWood", "a small arched wooden footbridge with railings"),
                      ("look2", "Looks/BridgeStone", "one small humpbacked footbridge of grey stone blocks with low stone parapets"),
                      ("look3", "Looks/BridgeRope", "a small rope suspension footbridge with wooden planks")]),
             S("Objects/Ducks", "object", "a family of three white ducks swimming", "pond_ducks", "after", 760, 620, 280, 170, "birds", "#FFFFFF"),
             S("Objects/Pier", "object", "a sturdy little wooden pier of brown planks on thick posts standing in blue water, a fishing rod and a bucket on it", "pond_pier", "after",
               300, 600, 380, 250, "pier", "#B98C5A"),
             S("Objects/WaterLilies", "object", "floating green water lily pads with pink lotus flowers", "pond_lilies", "after",
               960, 720, 260, 130, "lilies", "#6CC04A", z=-1),
             S("Objects/WaterLilies", "object", "", "pond_lilies", "after", 600, 500, 240, 120, "lilies", "#6CC04A", z=-1),
             S("Before/BoatOld", "before", "an old wooden rowing boat with cracked peeling paint lying on the shore", "pond_boat", "before",
               480, 470, 320, 190, "boat_broken", "#8A7A66"),
             S("Looks/BoatBlue", "look", "", "pond_boat", "after", 480, 470, 320, 190, "boat", "#3E86D8", tap=True,
               looks=[("look1", "Looks/BoatBlue", "a freshly tarred wooden rowing boat painted bright blue with oars"),
                      ("look2", "Looks/BoatRed", "a freshly tarred wooden rowing boat painted bright red with oars"),
                      ("look3", "Looks/BoatGreen", "a freshly tarred wooden rowing boat painted bright green with oars")]),
         ] + [S("Objects/ShoreLantern", "object", "a small paper lantern on a short wooden pole, glowing warm", "pond_lights", "after",
                x, y, 120, 260, "post", "#FFC928") for x, y in ((240, 260), (800, 120), (1360, 300), (1440, 860))]),

    # ------------------------------------------------------------------ Амбар: the big barn
    dict(id="barn", cell=(2, 1), name="Barn", camera=(780, 560),
         bg="a wide flat farmyard of packed earth with scattered straw where a big barn would stand, grass around the edges, "
            "nothing built, empty ground",
         ground="#86C95A",
         ph=[("rect", 800, 600, 1600, 1200, "#86C95A"), ("ellipse", 800, 480, 1400, 820, "#CDB27A"),
             ("ellipse", 600, 300, 300, 120, "#E3CF94")],
         sprites=[
             S("Before/BarnOld", "before", "an old big weathered grey wooden barn with a holey roof, faded boards", "barn_paint", "before",
               760, 260, 1000, 860, "barn_old", "#9A8A7A", z=-1),
             S("Looks/BarnRed", "look", "", "barn_paint", "after", 760, 260, 1000, 860, "barn", "#C8453A", z=-1, tap=True,
               looks=[("look1", "Looks/BarnRed", "a big classic farm barn painted bright red with white trim and a hay loft door"),
                      ("look2", "Looks/BarnYellow", "a big classic farm barn painted sunny yellow with white trim and a hay loft door"),
                      ("look3", "Looks/BarnBlue", "a big classic farm barn painted sky blue with white trim and a hay loft door")]),
             S("Before/BarnDoorsStuck", "before", "big closed wooden barn doors jammed shut with planks nailed across", "barn_doors", "before",
               760, 270, 380, 380, "doors_closed", "#7A5A3A", z=1),
             S("Objects/BarnDoorsOpen", "object", "big wooden barn doors swung wide open with hay visible inside", "barn_doors", "after",
               760, 270, 420, 380, "doors_open", "#E8C870", z=1),
             S("Objects/RoofPatches", "object", "a flat strip of new orange wooden roof shingles in neat overlapping rows", "barn_roof", "after", 760, 820, 520, 200,
               "patches", "#B98C5A", z=2, until="barn_paint"),
             S("Objects/Planks", "object", "a neat pile of fresh light yellow wooden boards stacked crosswise", "barn_floor", "after", 1440, 140, 240, 150, "pile", "#E0B070"),
             S("Objects/Shelves", "object", "rustic wooden shelves with jars of pickles, jam and tools", "barn_shelves", "after",
               1380, 380, 260, 300, "shelves", "#B07A44"),
             S("Objects/Scales", "object", "old cast iron farm weighing scales with weights", "barn_scales", "after", 300, 200, 220, 240, "post", "#5A6A7A"),
             S("Objects/OwlHouse", "object", "a small wooden owl nesting box with a cute brown owl peeking out", "barn_owl", "after",
               1180, 760, 170, 210, "birdhouse", "#9A6234", z=3),
             S("Before/CartBroken", "before", "an old broken wooden farm cart with a missing wheel tipped on its side", "barn_cart", "before",
               260, 520, 400, 270, "cart_broken", "#8A6A44"),
             S("Objects/Cart", "object", "a repaired wooden farm cart with big spoked wheels and fresh hay inside", "barn_cart", "after",
               260, 520, 400, 280, "cart", "#C0803A"),
             S("Objects/HarvestPile", "object", "a pile of harvest: burlap sacks of grain, crates of vegetables and golden hay bales",
               "barn_fill", "after", 1100, 140, 400, 270, "pile", "#E8C040"),
         ]),

    # ------------------------------------------------------------------ Курятник
    dict(id="coop", cell=(2, 0), name="Coop", camera=(800, 520),
         bg="a small grassy plot with a sandy patch of bare ground in the middle, a few pecked seeds, nothing built, empty ground",
         ground="#8CCB5E",
         ph=[("rect", 800, 600, 1600, 1200, "#8CCB5E"), ("ellipse", 800, 420, 1200, 640, "#E3CF94")],
         sprites=[
             S("Before/CoopRuined", "before", "a crooked tumbledown old wooden chicken coop with broken walls and a collapsed roof",
               "coop_walls", "before", 800, 440, 640, 560, "shed_old", "#9A8A7A"),
             S("Objects/CoopWalls", "object", "a small repaired wooden chicken coop with sturdy plank walls and a ramp, no roof",
               "coop_walls", "after", 800, 440, 640, 520, "shed", "#C8955A", until="coop_roof"),
             S("Looks/CoopRoofRed", "look", "", "coop_roof", "after", 800, 440, 640, 600, "shed", "#D9573A", tap=True,
               looks=[("look1", "Looks/CoopRoofRed", "a cozy wooden chicken coop with a red pitched roof and a little ramp"),
                      ("look2", "Looks/CoopRoofStriped", "a cozy wooden chicken coop with a red and white striped pitched roof and a little ramp"),
                      ("look3", "Looks/CoopRoofVane", "a cozy wooden chicken coop with a pitched roof topped by a rooster weather vane")]),
             S("Junk/CoopJunk", "junk", "a heap of dirty old straw, broken boards and feathers", "coop_clean", "before",
               480, 300, 360, 230, "junk", "#9A8A5A", z=2),
             S("Junk/CoopJunk", "junk", "", "coop_clean", "before", 1080, 260, 340, 220, "junk", "#9A8A5A", z=2),
             S("Objects/StrawNests", "object", "three round straw nests in a wooden box", "coop_nests", "after", 1220, 420, 240, 160, "nests", "#E8C870"),
             S("Objects/ChickenRun", "object", "a long low wooden picket fence with a small gate in the middle, the fence fills the picture from left to right",
               "coop_yard", "after", 800, 30, 1100, 210, "fence", "#C8955A", z=3),
             S("Objects/Hens", "object", "two plump brown and white hens pecking", "coop_hens", "after", 560, 250, 240, 190, "birds", "#C0703A", z=1),
             S("Objects/Hens", "object", "", "coop_hens", "after", 1020, 230, 240, 190, "birds", "#C0703A", z=1),
             S("Objects/Feeder", "object", "a wooden chicken feeder trough with grain and a tin water dish", "coop_feeder", "after",
               1280, 200, 230, 170, "trough", "#9A6234", z=1),
             S("Objects/EggBasket", "object", "a small wicker basket with fresh brown eggs on straw", "coop_egg", "after",
               340, 170, 180, 140, "basket", "#C8955A", z=2),
         ]),

    # ------------------------------------------------------------------ Мельница: the windmill on the hill
    dict(id="mill", cell=(1, 2), name="Mill", camera=(800, 600),
         bg="a gentle green hill with a dirt road winding up from the bottom to a flat grassy top in the middle, wildflowers, "
            "nothing built, empty ground",
         ground="#86C95A",
         ph=[("rect", 800, 600, 1600, 1200, "#86C95A"), ("ellipse", 800, 640, 1300, 900, "#9AD06A"),
             ("rect", 800, 260, 220, 520, "#C9B07A"), ("ellipse", 800, 600, 600, 260, "#C9B07A")],
         sprites=[
             S("Junk/RoadDebris", "junk", "a heap of fallen tree logs, broken branches and big grey boulders lying across a dirt road", "mill_road", "before",
               720, 180, 420, 240, "junk", "#8A7A5A", z=5),
             S("Junk/RoadDebris", "junk", "", "mill_road", "before", 900, 420, 400, 230, "junk", "#8A7A5A", z=5),
             S("Before/MillBoarded", "before", "an old abandoned stone windmill tower with a boarded up door and cracked walls, no sails",
               "mill_open", "before", 800, 500, 560, 620, "tower_old", "#A09A8A"),
             S("Objects/MillTower", "object", "a restored white stone windmill tower with a wooden cap roof and an open door, no sails",
               "mill_open", "after", 800, 500, 560, 620, "tower", "#F3E3C3"),
             S("Before/SailsBroken", "before", "broken torn windmill sails, a wooden cross frame with missing slats", "mill_sails", "before",
               800, 840, 680, 560, "sails_broken", "#8A7A66", z=2),
             S("Looks/SailsWhite", "look", "", "mill_sails", "after", 800, 840, 680, 560, "sails", "#FFFFFF", z=2, tap=True,
               looks=[("look1", "Looks/SailsWhite", "only the sails of a windmill: four long blades of white canvas on wooden frames joined at a round hub, an X shape, front view, no tower, no building"),
                      ("look2", "Looks/SailsLattice", "only the sails of a windmill: four long blades of open wooden lattice joined at a round hub, an X shape, front view, no tower, no building"),
                      ("look3", "Looks/SailsRibbons", "only the sails of a windmill: four long blades of wooden frames with colorful ribbons joined at a round hub, an X shape, front view, no tower, no building")]),
             S("Objects/Millstones", "object", "two big round grey millstones leaning together", "mill_stones", "after", 1220, 480, 260, 200, "stones", "#9A9A9A"),
             S("Objects/Gears", "object", "big wooden mill gears and an oil can", "mill_gears", "after", 400, 500, 240, 200, "gears", "#B07A44"),
             S("Before/Bulldozer", "before", "a big yellow bulldozer with a scoop parked menacingly", "mill_guard", "before",
               1260, 240, 440, 320, "machine", "#FFC928", z=3),
             S("Objects/EmptySacks", "object", "a neat stack of new empty folded burlap sacks", "mill_sacks", "after", 440, 280, 280, 180, "pile",
               "#C9A46A", until="mill_flour"),
             S("Objects/FlourSacks", "object", "a pile of full white flour sacks with flour dust and a fresh loaf of bread", "mill_flour", "after",
               440, 270, 320, 240, "pile", "#F2EAD8"),
             S("Looks/FestivalGarlands", "look", "", "mill_festival", "after", 800, 120, 1200, 520, "garland", "#FF6F91", z=6, tap=True,
               looks=[("look1", "Looks/FestivalGarlands", "two tall wooden poles with one long garland of colorful flowers and green leaves hanging in a curve between them"),
                      ("look2", "Looks/FestivalFlags", "strings of colorful triangle bunting flags strung between two tall wooden poles"),
                      ("look3", "Looks/FestivalLanterns", "strings of glowing paper lanterns strung between two tall wooden poles")]),
         ]),
]

# the corner without a zone: a meadow with haystacks, always there
FILLER = dict(id="meadow", cell=(2, 2), name="Meadow",
              bg="a sunny green meadow with wildflowers, two golden haystacks and a few round trees at the edges, a distant forest",
              ph=[("rect", 800, 600, 1600, 1200, "#8CCB5E"), ("ellipse", 500, 500, 260, 220, "#E8C040"),
                  ("ellipse", 1100, 700, 240, 200, "#E8C040"), ("ellipse", 200, 1050, 300, 240, "#4E9A34"),
                  ("ellipse", 1400, 1000, 320, 260, "#4E9A34")])

# effects and the fog over closed zones
FX = {
    "Fx/Dust": ("fx", "a round puff of light brown dust cloud, cartoon smoke puff", (256, 256)),
    "Fx/Sparkle": ("fx", "a bright golden four-pointed sparkle star", (128, 128)),
    "Fog/Cloud1": ("fx", "a single big fluffy white cartoon cloud floating in the air, only the cloud, no ground, no grass, no island, no trees", (512, 320)),
    "Fog/Cloud2": ("fx", "a long fluffy white cartoon cloud bank floating in the air, only the cloud, no ground, no grass, no island, no trees", (512, 256)),
}

# the speakers of chapter 1 (chapter1.json characters), busts for the dialogue window
PORTRAITS = {
    "anya": ("Anya", "a cheerful young woman of 25 with a long chestnut braid, freckles, a straw hat and denim overalls, warm smile"),
    "matvey": ("Matvey", "a kind old farmer of 75 with a white beard, a flat cap, a checked shirt and a twinkle in his eyes"),
    "vera": ("Vera", "a jolly plump middle-aged village woman with rosy cheeks, a flowery headscarf and an apron, laughing"),
    "grachev": ("Grachev", "a slick middle-aged property developer with slicked back hair, a grey suit, a gold watch and a smug grin"),
    "sharik": ("Sharik", "a shaggy friendly brown mongrel dog with floppy ears and a happy open mouth"),
    "pasha": ("Pasha", "an energetic ten year old boy with messy blond hair, a cap on backwards and a gap-toothed grin"),
    "nina": ("Nina", "a dignified woman of 60, head of the village council, short grey hair, glasses and a cardigan, friendly"),
    "fedot": ("Fedot", "a strong bearded carpenter of 45 with a pencil behind his ear, a work apron and rolled-up sleeves, smiling"),
    "lida": ("Lida", "a lively young postwoman with curly red hair, a blue cap and a mail bag, waving"),
    "diary": ("Diary", "an old grandmother's notebook with a checkered cover, a pressed flower and a ribbon bookmark, slightly open"),
}

# ---------------------------------------------------------------------------------------------------- building


def _size(w, h, big=False):
    """The painted size for a sprite of w×h units: its aspect, the long side 512 (1024 for buildings)."""
    side = 1024 if big else 512
    k = side / max(w, h)
    return (max(64, int(round(w * k / 8)) * 8), max(64, int(round(h * k / 8)) * 8))


def build():
    chapter = json.load(open(CHAPTER, encoding="utf-8"))
    tasks = {t["id"]: t for t in chapter["tasks"]}
    manifest = {}
    zones = []

    def art(name, kind, prompt, size):
        key = "Farm/Ch1/" + name
        if key in manifest:
            return
        if not prompt:
            raise SystemExit("no prompt for the first use of " + name)
        manifest[key] = {"kind": kind, "prompt": prompt + ", " + ISO if kind not in ("portrait", "fx") else prompt, "size": list(size)}

    for z in ZONES + [FILLER]:
        col, row = z["cell"]
        bg = "Ch1/Backgrounds/" + z["name"]
        manifest["Farm/" + bg] = {"kind": "background", "opaque": True,
                                  "prompt": z["bg"] + ", " + ISO + ", seen from above at 30 degrees, wide landscape, full picture",
                                  "size": list(BG_SIZE)}
        out = {"id": z["id"], "x": MARGIN + col * ZW, "y": MARGIN + row * ZH, "w": ZW, "h": ZH, "background": bg,
               "ph": [dict(shape=s, x=x, y=y, w=w, h=h, color=c) for s, x, y, w, h, c in z["ph"]], "sprites": []}
        if "camera" in z:
            out["camera"] = {"x": z["camera"][0], "y": z["camera"][1]}
        for i, s in enumerate(z.get("sprites", [])):
            if s["task"] not in tasks:
                raise SystemExit("unknown task " + s["task"])
            if tasks[s["task"]]["zone"] != z["id"]:
                raise SystemExit("task %s is not in zone %s" % (s["task"], z["id"]))
            big = s["w"] >= 600 or s["h"] >= 600
            sp = {"id": "%s.%d" % (z["id"], i), "task": s["task"], "show": s["show"], "x": s["x"], "y": s["y"], "w": s["w"], "h": s["h"],
                  # the sort order: lower on the ground is nearer; flat things (paths, beds) lie under everything standing
                  "z": (-4000 if s["flat"] else 0) + (ZH - s["y"]) + 2000 * s["z"],
                  "ph": s["ph"], "color": s["color"]}
            if s["until"]:
                sp["until"] = s["until"]
            if s["looks"]:
                choices = [c["id"] for c in tasks[s["task"]].get("choices", [])]
                if [c for c, _, _ in s["looks"]] != choices:
                    raise SystemExit("looks of %s do not match its choices %s" % (s["task"], choices))
                sp["looks"] = {}
                for c, name, prompt in s["looks"]:
                    art(name, "look", prompt, _size(s["w"], s["h"], big))
                    sp["looks"][c] = "Ch1/" + name
                sp["art"] = sp["looks"][choices[0]]
            else:
                art(s["art"], s["kind"], s["prompt"], _size(s["w"], s["h"], big))
                sp["art"] = "Ch1/" + s["art"]
            if s["tap"]:
                sp["tap"] = True
            out["sprites"].append(sp)
        zones.append(out)

    for name, (kind, prompt, size) in FX.items():
        manifest["Farm/Ch1/" + name] = {"kind": kind, "prompt": prompt, "size": list(size)}
    portraits = {}
    for cid, (name, prompt) in PORTRAITS.items():
        manifest["Farm/Ch1/Portraits/" + name] = {
            "kind": "portrait", "size": [512, 512],
            "prompt": "a friendly cartoon character bust portrait, head and shoulders, looking at the viewer: " + prompt}
        portraits[cid] = "Ch1/Portraits/" + name

    layout = {
        "about": "Written by farm/tools/art/chapter1_art.py: zones on the map, backgrounds and every sprite of chapter 1 "
                 "(x, y = the bottom centre in the zone's units, origin lower left; z = draw order, larger in front; "
                 "show: after = appears when the task is done, before = disappears; until = disappears when that task is done).",
        "map": {"w": 2 * MARGIN + COLS * ZW, "h": 2 * MARGIN + ROWS * ZH, "ground": "#79B94E"},
        "zones": zones,
        "fog": ["Ch1/Fog/Cloud1", "Ch1/Fog/Cloud2"],
        "dust": "Ch1/Fx/Dust",
        "sparkle": "Ch1/Fx/Sparkle",
        "portraits": portraits,
    }
    return chapter, manifest, layout


def coverage(chapter, layout):
    """Every task changes something on the farm; every character has a portrait."""
    problems = []
    shown = {s["task"] for z in layout["zones"] for s in z["sprites"]}
    for t in chapter["tasks"]:
        if t["id"] not in shown:
            problems.append("task %s has no sprite" % t["id"])
    looks = {s["task"] for z in layout["zones"] for s in z["sprites"] if "looks" in s}
    for t in chapter["tasks"]:
        if t.get("choices") and t["id"] not in looks:
            problems.append("task %s has choices but no looks" % t["id"])
    for c in chapter["characters"]:
        if c["id"] not in layout["portraits"]:
            problems.append("character %s has no portrait" % c["id"])
    for z in layout["zones"]:
        for s in z["sprites"]:
            if not (0 <= s["x"] <= ZW and 0 <= s["y"] <= ZH and s["y"] + s["h"] <= ZH + 200):
                problems.append("sprite %s is outside its zone" % s["id"])
    return problems


def dump(o):
    return json.dumps(o, ensure_ascii=False, indent=1) + "\n"


def install(src):
    """Puts the painted chapter 1 pictures into the game: <src>/Farm/Ch1/X → Resources/Art/Ch1/X."""
    import shutil
    base = os.path.join(src, "Farm", "Ch1")
    dst = os.path.join(META, "..", "Art", "Ch1")
    n = 0
    for folder, _, files in os.walk(base):
        for f in files:
            if f.endswith((".png", ".jpg")):
                rel = os.path.relpath(os.path.join(folder, f), base)
                os.makedirs(os.path.dirname(os.path.join(dst, rel)), exist_ok=True)
                shutil.copyfile(os.path.join(folder, f), os.path.join(dst, rel))
                n += 1
    print("installed %d pictures into %s" % (n, os.path.normpath(dst)))


def main():
    if "--install" in sys.argv:
        i = sys.argv.index("--install")
        install(sys.argv[i + 1] if len(sys.argv) > i + 1 else os.path.join(FARM, "..", "hearth", "tools", "art", "ai", "sd"))
        return
    chapter, manifest, layout = build()
    problems = coverage(chapter, layout)
    if problems:
        print("\n".join(problems))
        sys.exit(1)
    files = {MANIFEST: dump({"about": "Chapter 1 art for hearth/tools/art/sdpaint.py (FARM_CH1, FARM_CH1_BACKGROUNDS), "
                                      "written by farm/tools/art/chapter1_art.py", "pieces": manifest}),
             LAYOUT: dump(layout)}
    kinds = {}
    for v in manifest.values():
        kinds[v["kind"]] = kinds.get(v["kind"], 0) + 1
    if "--check" in sys.argv:
        stale = [p for p, text in files.items() if not os.path.exists(p) or open(p, encoding="utf-8").read() != text]
        if stale:
            print("stale (run python3 farm/tools/art/chapter1_art.py):", *stale)
            sys.exit(1)
        print("chapter 1 art: %d pictures %s, layout up to date" % (len(manifest), kinds))
        return
    for p, text in files.items():
        with open(p, "w", encoding="utf-8", newline="\n") as f:
            f.write(text)
    print("chapter 1 art: %d pictures %s" % (len(manifest), kinds))


if __name__ == "__main__":
    main()
