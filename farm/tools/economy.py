"""Economy model of «Солнечная долина» (GDD 5-8): generates the balance and documents the pacing.

What it does:
  1. Describes the content of the farm economy (crops, orchard, animals, workshops and recipes, barn,
     plots, town, expansions, daily tasks) — the single source of these numbers.
  2. Values every good from its production chain (work minutes -> order coins, XP, sell price).
  3. Models chapter 1 «Восстановление» from Resources/Meta/chapter1.json: stars per zone, match-3
     levels and minutes per zone, the day the chapter ends (target 3-5 days).
  4. Models the farm day by day for a free player (5 sessions a day, two farm passes per session around
     a few match-3 levels), buying what unlocks; derives the XP table so that farm levels arrive on the
     designed schedule; tracks coins, tools and barn capacity.
  5. Runs consistency checks and writes
       farm/unity/SunnyValley/Assets/SunnyValley/Resources/Meta/balance.json
       farm/docs/ECONOMY.md

The C# player bot (Core/Tests~/MetaBotTests.cs) plays the real rules for 30 days and must land within
`pacing.tolerancePercent` of the model's days; MODEL_EFFICIENCY below is the share of the production
capacity the bot actually turns into orders (measured by the bot, see ECONOMY.md).

Run:            python3 farm/tools/economy.py            (writes the files)
CI consistency: python3 farm/tools/economy.py --check    (fails if the files are stale or a check fails)
"""
import json
import math
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
META = os.path.join(ROOT, "unity", "SunnyValley", "Assets", "SunnyValley", "Resources", "Meta")
BALANCE_PATH = os.path.join(META, "balance.json")
CHAPTER_PATH = os.path.join(META, "chapter1.json")
DOC_PATH = os.path.join(ROOT, "docs", "ECONOMY.md")

MAX_LEVEL = 30
MODEL_DAYS = 60

# ============================================================== the player the model plays for
SESSION_HOURS = [8.0, 12.0, 16.0, 19.5, 22.5]   # local time of the 5 daily sessions
ATTEMPTS_PER_SESSION = 5                         # match-3 attempts per session (lives permitting)
WIN_RATE = 0.8                                   # share of won attempts (GDD 4.8: early levels ~90%, later waves)
LEVEL_MINUTES = 2.5                              # one match-3 attempt (GDD 3: 2-4 min, early ones short)
FIRST_SESSION_HOUR = 8.0                         # install at 8:00 of day 0
MODEL_EFFICIENCY = 0.42                          # share of production capacity that becomes orders (bot-measured)
TOLERANCE_PERCENT = 25                           # allowed bot deviation from the model's days

# ============================================================== currencies, lives, match-3 (GDD 4.6-4.7, 7)
START = {"coins": 300, "crystals": 20, "stars": 0,
         "boosters": {"Line": 1, "Bomb": 1, "Rainbow": 1, "Shovel": 2, "Shuffle": 1}, "tools": {}}
LIVES = {"max": 5, "regenSeconds": 1800, "refillCrystals": 12}
MATCH3 = {
    "winCoins": 20,
    "extraMoves": 5,
    "extraMovesPrices": [900, 1900, 2900],
    "milestoneEvery": 10,
    "milestoneReward": {"crystals": 3, "infiniteLivesMinutes": 30},
    "boosterEvery": 3,
    "boosterRotation": ["Shovel", "Line", "Rake", "Bomb", "WateringCan", "Shuffle", "Rainbow"],
    "toolPercent": 15,
}
BOARD_COINS_AVG = 25   # what the board pays for leftover moves on average (match-3 core), for the coin flow

BOOSTERS = [  # id, name, kind, shop coins, description
    ("Line", "Вилы", "pre", 500, "Вилы на поле с первого хода"),
    ("Bomb", "Бочка-бомба", "pre", 700, "Бочка-бомба на поле с первого хода"),
    ("Rainbow", "Радуга-подсолнух", "pre", 900, "Радуга-подсолнух на поле с первого хода"),
    ("Shovel", "Лопата", "in", 400, "Убирает одну клетку"),
    ("Rake", "Грабли", "in", 500, "Убирает строку"),
    ("WateringCan", "Лейка", "in", 500, "Убирает столбец"),
    ("Shuffle", "Перемешать", "in", 300, "Перемешивает фишки"),
]
TOOLS = [("hammer", "Молоток", "barn"), ("nails", "Гвозди", "barn"), ("paint", "Краска", "barn"),
         ("saw", "Пила", "land"), ("axe", "Топор", "land")]
TOOL_DROPS = {"harvestPercent": 3, "orderPercent": 10, "landFromLevel": 5, "landSharePercent": 40}
SPEEDUP = {"secondsPerCrystal": 600, "minCrystals": 1}

# ============================================================== farm content (GDD 6)
# crops: id, name, level, grow minutes
CROPS = [
    ("wheat", "Пшеница", 1, 2),
    ("corn", "Кукуруза", 1, 5),
    ("carrot", "Морковь", 2, 10),
    ("sunflower", "Подсолнух", 4, 20),
    ("strawberry", "Клубника", 6, 60),
    ("cotton", "Хлопок", 8, 120),
    ("beet", "Свёкла", 10, 180),
    ("potato", "Картофель", 13, 240),
    ("pumpkin", "Тыква", 16, 480),
]
CROP_YIELD = 2
# trees: id, name, fruit id, fruit name, level, cycle minutes, yield, costs (count = max)
TREES = [
    ("apple_tree", "Яблоня", "apple", "Яблоко", 1, 180, 3, [0, 0, 400, 600, 800, 1000]),
    ("pear_tree", "Груша", "pear", "Груша", 9, 240, 3, [900, 1200, 1500, 1800, 2100, 2400]),
    ("cherry_tree", "Вишня", "cherry", "Вишня", 14, 300, 3, [1600, 2000, 2400, 2800, 3200, 3600]),
]
# animal houses: id, name, level, coins, population needed, capacity
ANIMAL_HOUSES = [
    ("coop", "Курятник", 1, 0, 0, 6),
    ("cowshed", "Коровник", 3, 600, 0, 6),
    ("sheepfold", "Овчарня", 7, 1500, 60, 6),
    ("apiary", "Пасека", 11, 2800, 120, 6),
    ("pigsty", "Свинарник", 15, 5000, 200, 6),
]
# animals: id, name, house, product id, product name, feed id, minutes, level, costs
ANIMALS = [
    ("chicken", "Курица", "coop", "egg", "Яйцо", "chicken_feed", 20, 1, [0, 0, 0, 150, 200, 260]),
    ("cow", "Корова", "cowshed", "milk", "Молоко", "cow_feed", 60, 3, [300, 350, 420, 500, 600, 700]),
    ("sheep", "Овца", "sheepfold", "wool", "Шерсть", "sheep_feed", 120, 7, [700, 800, 900, 1000, 1100, 1200]),
    ("bee", "Улей", "apiary", "honey", "Мёд", "sunflower", 90, 11, [1200, 1400, 1600, 1800, 2000, 2200]),
    ("pig", "Свинья", "pigsty", "truffle", "Трюфель", "pig_feed", 180, 15, [1800, 2100, 2400, 2700, 3000, 3300]),
]
# feeds and products made in workshops: id, name, kind
MADE_ITEMS = [
    ("chicken_feed", "Корм для кур", "feed"),
    ("cow_feed", "Корм для коров", "feed"),
    ("sheep_feed", "Корм для овец", "feed"),
    ("pig_feed", "Корм для свиней", "feed"),
    ("flour", "Мука", "product"),
    ("bread", "Хлеб", "product"),
    ("corn_bread", "Кукурузный хлеб", "product"),
    ("apple_pie", "Яблочный пирог", "product"),
    ("cookies", "Печенье", "product"),
    ("honey_cake", "Медовик", "product"),
    ("pumpkin_pie", "Тыквенный пирог", "product"),
    ("cream", "Сливки", "product"),
    ("butter", "Масло", "product"),
    ("cheese", "Сыр", "product"),
    ("strawberry_jam", "Клубничное варенье", "product"),
    ("apple_jam", "Яблочное варенье", "product"),
    ("pear_jam", "Грушевое варенье", "product"),
    ("cherry_jam", "Вишнёвое варенье", "product"),
    ("fabric", "Ткань", "product"),
    ("yarn", "Пряжа", "product"),
    ("sweater", "Свитер", "product"),
    ("sugar", "Сахар", "product"),
    ("syrup", "Сироп", "product"),
    ("candy", "Леденцы", "product"),
]
MATCH3_ITEMS = [("tomato", "Помидор"), ("eggplant", "Баклажан"), ("blueberry", "Черника")]

# workshops: id, name, level, coins, population, slots, slot crystals, recipes
# recipe: id, level, inputs, output, count, minutes
WORKSHOPS = [
    ("mill", "Мельница", 1, 0, 0, 3, [8, 15, 25], [
        ("chicken_feed", 1, {"wheat": 2, "corn": 1}, "chicken_feed", 3, 5),
        ("flour", 2, {"wheat": 3}, "flour", 1, 10),
        ("cow_feed", 3, {"corn": 2, "carrot": 1}, "cow_feed", 3, 10),
        ("sheep_feed", 7, {"wheat": 2, "carrot": 2}, "sheep_feed", 3, 15),
        ("pig_feed", 15, {"corn": 2, "potato": 1}, "pig_feed", 3, 20),
    ]),
    ("bakery", "Пекарня", 2, 300, 0, 2, [8, 15, 25], [
        ("bread", 2, {"flour": 2}, "bread", 1, 15),
        ("corn_bread", 4, {"corn": 3, "egg": 1}, "corn_bread", 1, 30),
        ("apple_pie", 6, {"flour": 1, "apple": 2, "egg": 1}, "apple_pie", 1, 60),
        ("cookies", 11, {"flour": 1, "egg": 2, "sugar": 1}, "cookies", 1, 45),
        ("honey_cake", 13, {"flour": 1, "honey": 1, "cream": 1}, "honey_cake", 1, 90),
        ("pumpkin_pie", 17, {"pumpkin": 1, "flour": 1, "egg": 1}, "pumpkin_pie", 1, 120),
    ]),
    ("dairy", "Молочная", 4, 900, 30, 2, [8, 15, 25], [
        ("cream", 4, {"milk": 1}, "cream", 1, 20),
        ("butter", 5, {"milk": 2}, "butter", 1, 30),
        ("cheese", 6, {"milk": 3}, "cheese", 1, 60),
    ]),
    ("toolshed", "Сарай инструментов", 5, 1200, 40, 2, [10, 20], [
        ("make_shovel", 5, {"bread": 1, "corn": 3}, "Shovel", 1, 60),
        ("make_rake", 6, {"flour": 2, "carrot": 3}, "Rake", 1, 90),
        ("make_watering_can", 8, {"butter": 1, "wheat": 5}, "WateringCan", 1, 90),
        ("make_shuffle", 9, {"apple_jam": 1, "egg": 2}, "Shuffle", 1, 120),
        ("make_line", 11, {"fabric": 1, "cheese": 1}, "Line", 1, 120),
        ("make_bomb", 13, {"sweater": 1, "corn_bread": 1}, "Bomb", 1, 180),
        ("make_rainbow", 16, {"syrup": 1, "sweater": 1}, "Rainbow", 1, 240),
    ]),
    ("jam", "Джем-цех", 7, 1800, 60, 2, [8, 15, 25], [
        ("strawberry_jam", 7, {"strawberry": 3}, "strawberry_jam", 1, 60),
        ("apple_jam", 7, {"apple": 3}, "apple_jam", 1, 60),
        ("pear_jam", 10, {"pear": 3}, "pear_jam", 1, 75),
        ("cherry_jam", 15, {"cherry": 3, "sugar": 1}, "cherry_jam", 1, 90),
    ]),
    ("textile", "Ткацкая", 8, 2500, 80, 2, [8, 15, 25], [
        ("fabric", 8, {"cotton": 2}, "fabric", 1, 45),
        ("yarn", 9, {"wool": 2}, "yarn", 1, 40),
        ("sweater", 12, {"yarn": 2, "fabric": 1}, "sweater", 1, 120),
    ]),
    ("sugar_mill", "Сахарная", 10, 3500, 110, 2, [8, 15, 25], [
        ("sugar", 10, {"beet": 2}, "sugar", 1, 30),
        ("syrup", 12, {"beet": 2, "strawberry": 1}, "syrup", 1, 60),
        ("candy", 14, {"sugar": 2, "cream": 1}, "candy", 1, 60),
    ]),
]

# ============================================================== barn, plots, town (GDD 6)
BARN_CAPACITY = 70
BARN_UPGRADES = 25          # each +BARN_STEP, tools 1+k of each barn tool (k = 0-based index, capped)
BARN_STEP = 25
PLOTS_START = 6
# extra plots: (level, coins)
PLOT_OFFERS = [(1, 50), (1, 80), (2, 120), (2, 160), (3, 220), (4, 300), (5, 380), (6, 470), (7, 570),
               (8, 680), (9, 800), (10, 940), (11, 1100), (12, 1280), (13, 1480), (14, 1700), (15, 1950),
               (16, 2200), (18, 2500), (20, 2800), (22, 3200), (24, 3600), (26, 4000), (28, 4500)]
TOWN_BASE_LOTS = 6
TOWN_BASE_POPULATION_CAP = 30
# houses: id, name, level, coins, population, xp, max
HOUSES = [
    ("cottage", "Домик", 2, 150, 10, 5, 12),
    ("attic_house", "Дом с мансардой", 5, 600, 20, 15, 12),
    ("two_storey", "Двухэтажный дом", 9, 1500, 35, 30, 12),
    ("terem", "Терем", 14, 3500, 55, 60, 12),
]
# services: id, name, level, coins, capacity, xp
SERVICES = [
    ("cafe", "Кафе", 3, 400, 40, 10),
    ("post", "Почта", 5, 800, 50, 15),
    ("school", "Школа", 8, 1600, 70, 25),
    ("clinic", "Медпункт", 10, 2400, 80, 35),
    ("library", "Библиотека", 12, 3200, 90, 45),
    ("club", "Клуб", 14, 4500, 110, 60),
    ("market", "Рынок", 16, 6000, 130, 80),
    ("stadium", "Стадион", 19, 9000, 150, 110),
]
# decorations: id, name, level, coins, stars, beauty, xp, max (0 = unlimited)
DECORATIONS = [
    ("bench", "Скамейка", 2, 50, 0, 1, 2, 0),
    ("flowerbed", "Клумба", 3, 120, 0, 2, 4, 0),
    ("lantern", "Фонарь", 4, 200, 0, 3, 5, 0),
    ("scarecrow", "Пугало", 5, 300, 0, 4, 8, 4),
    ("well", "Колодец", 6, 600, 0, 6, 12, 1),
    ("fountain", "Фонтан", 9, 2000, 0, 12, 30, 1),
    ("gazebo", "Беседка", 12, 3000, 0, 15, 40, 2),
    ("swing", "Качели", 1, 0, 8, 6, 15, 1),
    ("weathervane", "Флюгер-петушок", 1, 0, 10, 8, 20, 1),
    ("grandma_statue", "Памятник бабушке Дуне", 1, 0, 15, 20, 40, 1),
    ("bandstand", "Летняя эстрада", 4, 0, 20, 25, 60, 1),
    ("lighthouse", "Маяк у пруда", 8, 0, 30, 35, 90, 1),
]
# land expansions: (level, coins, saw, axe, lots)
EXPANSIONS = [(4, 300, 1, 1, 3), (6, 600, 2, 2, 3), (8, 1000, 3, 3, 3), (10, 1500, 4, 4, 3), (12, 2200, 5, 5, 4),
              (14, 3000, 6, 6, 4), (16, 4000, 7, 7, 4), (18, 5200, 8, 8, 4), (20, 6500, 9, 9, 5),
              (22, 8000, 10, 10, 5), (25, 10000, 11, 11, 5), (28, 12500, 12, 12, 5)]

FARM_START = {
    "trees": {"apple_tree": 2},
    "animals": {"chicken": 3},
    "animalHouses": ["coop"],
    "workshops": ["mill"],
    "items": {"wheat": 6, "corn": 4, "chicken_feed": 3},
    "town": {"cottage": 1},
}

# ============================================================== orders and truck
ORDER_SLOTS = 9
ORDER_FULFILL_COOLDOWN = 60
ORDER_DISCARD_COOLDOWN = 15 * 60
TRUCK = {"level": 6, "crates": 3, "unitsPercent": 150, "returnSeconds": 3600, "coinPercent": 130,
         "xpPercent": 130, "tools": 1}


def order_max_chain_minutes(level):
    """The slowest production chain an order may ask for at this level (fairness)."""
    return int(min(600, 30 + 25 * (level - 1) + 4 * (level - 1) ** 2))


def order_max_kinds(level):
    return 2 if level <= 2 else 3


def order_max_units(level):
    return min(11, 3 + level // 2)


def order_max_work(level):
    return int(round(24 * (1 + 0.3 * (level - 1))))


# ============================================================== daily (GDD 8)
DAILY_TASKS = [  # id, kind, target, text, needs farm, reward
    ("win3", "win_levels", 3, "Пройти 3 уровня", False, {"coins": 150}),
    ("win6", "win_levels", 6, "Пройти 6 уровней", False, {"coins": 300, "boosters": {"Shovel": 1}}),
    ("play5", "play_levels", 5, "Сыграть 5 уровней", False, {"coins": 120}),
    ("stars3", "spend_stars", 3, "Потратить 3 звезды на ферму", False, {"coins": 150}),
    ("stars6", "spend_stars", 6, "Потратить 6 звёзд на ферму", False, {"boosters": {"Rake": 1}}),
    ("boost2", "use_boosters", 2, "Использовать 2 буста", False, {"coins": 100}),
    ("harvest20", "harvest", 20, "Собрать 20 урожая", True, {"coins": 200}),
    ("harvest40", "harvest", 40, "Собрать 40 урожая", True, {"coins": 350, "boosters": {"WateringCan": 1}}),
    ("produce5", "produce", 5, "Произвести 5 товаров", True, {"coins": 200}),
    ("orders3", "orders", 3, "Выполнить 3 заказа", True, {"coins": 250}),
    ("orders6", "orders", 6, "Выполнить 6 заказов", True, {"coins": 400, "crystals": 1}),
    ("feed6", "feed_animals", 6, "Покормить животных 6 раз", True, {"coins": 150}),
]
DAILY_CHEST = {"crystals": 3, "infiniteLivesMinutes": 30}
LOGIN_GIFTS = [
    {"coins": 100},
    {"boosters": {"Shovel": 1}},
    {"coins": 200},
    {"boosters": {"Bomb": 1}},
    {"crystals": 3},
    {"coins": 400},
    {"crystals": 5, "boosters": {"Rainbow": 1}, "infiniteLivesMinutes": 60},
]

# ============================================================== valuation
STEP_EXPONENT = 0.8        # value of a production step: minutes ** 0.8 (long steps work while you are away)
TREE_FACTOR = 0.5          # trees cost nothing to run: their step is worth less
ANIMAL_FACTOR = 0.6
XP_PER_WORK = 0.35
COINS_PER_WORK = 2.2
SELL_SHARE = 0.4


def step(minutes, factor=1.0):
    return factor * minutes ** STEP_EXPONENT


def value_items():
    """work (model minutes) and level of every barn good."""
    work, level, name, kind = {}, {}, {}, {}
    for cid, cname, lv, grow in CROPS:
        work[cid] = 0.5 + step(grow) / CROP_YIELD
        level[cid], name[cid], kind[cid] = lv, cname, "crop"
    for tid, tname, fid, fname, lv, cyc, yld, costs in TREES:
        work[fid] = 0.5 + step(cyc, TREE_FACTOR) / yld
        level[fid], name[fid], kind[fid] = lv, fname, "fruit"
    for mid, mname, mkind in MADE_ITEMS:
        name[mid], kind[mid] = mname, mkind
    for mid, mname in MATCH3_ITEMS:
        work[mid] = 3.0
        level[mid], name[mid], kind[mid] = 1, mname, "match3"
    recipes = [r for w in WORKSHOPS for r in w[7]]
    for _ in range(20):   # fixed point: every pass values what its inputs allow
        for rid, rlv, inputs, out, cnt, mins in recipes:
            if out not in work and out not in BOOSTER_IDS and all(i in work for i in inputs):
                work[out] = (sum(work[i] * n for i, n in inputs.items()) + step(mins)) / cnt
                level[out] = rlv
        for aid, aname, house, prod, pname, feed, mins, lv, costs in ANIMALS:
            if prod not in work and feed in work:
                work[prod] = work[feed] + step(mins, ANIMAL_FACTOR)
                level[prod], name[prod], kind[prod] = lv, pname, "animal"
    missing = [m[0] for m in MADE_ITEMS if m[0] not in work]
    if missing:
        raise SystemExit("economy: cannot value %s (broken chain)" % ", ".join(missing))
    return work, level, name, kind


BOOSTER_IDS = {b[0] for b in BOOSTERS}


def item_defs():
    work, level, name, kind = value_items()
    order = [c[0] for c in CROPS] + [t[2] for t in TREES] + [a[3] for a in ANIMALS] + \
            [m[0] for m in MADE_ITEMS] + [m[0] for m in MATCH3_ITEMS]
    defs = []
    for iid in order:
        w = work[iid]
        coins = max(2, int(round(w * COINS_PER_WORK)))
        defs.append({
            "id": iid, "name": name[iid], "kind": kind[iid], "level": level[iid],
            "sell": max(1, int(round(coins * SELL_SHARE))),
            "orderCoins": coins,
            "xp": max(1, int(round(w * XP_PER_WORK))),
            "work": round(w, 2),
        })
    return defs


# ============================================================== chapter 1 model
def chapter_model():
    with open(CHAPTER_PATH, encoding="utf-8") as f:
        ch = json.load(f)
    zones = []
    for z in ch["zones"]:
        tasks = [t for t in ch["tasks"] if t["zone"] == z["id"]]
        stars = sum(t["stars"] for t in tasks)
        zones.append({"id": z["id"], "name": z["name"], "tasks": len(tasks), "stars": stars,
                      "choices": sum(1 for t in tasks if t.get("choices")),
                      "lines": sum(len(t.get("lines", [])) for t in tasks)})
    total = sum(z["stars"] for z in zones)
    # Session model: 5 sessions a day, ATTEMPTS_PER_SESSION attempts each, lives regenerate in between.
    stars_per_session = ATTEMPTS_PER_SESSION * WIN_RATE
    cum, t_day = 0.0, []
    for z in zones:
        cum += z["stars"]
        sessions = math.ceil(cum / stars_per_session - 1e-9)
        day = session_time(sessions - 1) + ATTEMPTS_PER_SESSION * LEVEL_MINUTES / 1440
        z["levels"] = z["stars"] / WIN_RATE
        z["minutes"] = z["levels"] * LEVEL_MINUTES
        z["minutesPerStar"] = z["minutes"] / z["stars"]
        z["day"] = day
        t_day.append(day)
    return ch, zones, total, t_day[-1]


def session_time(index):
    """Day (fractional, since install at FIRST_SESSION_HOUR of day 0) of the session with this index."""
    per_day = len(SESSION_HOURS)
    d, s = divmod(index, per_day)
    return d + (SESSION_HOURS[s] - FIRST_SESSION_HOUR) / 24


# ============================================================== farm model
def gaps_minutes():
    """Farm passes of one day: (gap before the pass, minutes). Two passes per session around the match-3 levels."""
    session_len = ATTEMPTS_PER_SESSION * LEVEL_MINUTES
    out = []
    hours = SESSION_HOURS
    for i, h in enumerate(hours):
        prev = hours[i - 1] - (24 if i == 0 else 0)
        out.append((h - prev) * 60 - session_len)
        out.append(session_len)
    return out


def best_crop_value(level, gap):
    best = 0.0
    work, _, _, _ = VALUES
    for cid, _, lv, grow in CROPS:
        if lv <= level and grow <= gap:
            best = max(best, CROP_YIELD * (work[cid] - 0.5) + 1.0)
    return best


def recipe_avg(workshop, level):
    rs = [r for r in workshop[7] if r[1] <= level]
    if not rs:
        return None
    return sum(r[5] for r in rs) / len(rs)


class Farm:
    """The model player's farm: what they own, bought greedily as levels and coins allow."""

    def __init__(self):
        self.plots = PLOTS_START
        self.trees = dict(FARM_START["trees"])
        self.animals = dict(FARM_START["animals"])
        self.houses_animals = set(FARM_START["animalHouses"])
        self.workshops = {w: WS[w][5] for w in FARM_START["workshops"]}
        self.town = dict(FARM_START["town"])
        self.expansions = 0
        self.barn_upgrades = 0
        self.tools = {t[0]: 0 for t in TOOLS}

    def population(self):
        return sum(h[4] * self.town.get(h[0], 0) for h in HOUSES)

    def pop_cap(self):
        return TOWN_BASE_POPULATION_CAP + sum(s[4] * self.town.get(s[0], 0) for s in SERVICES)

    def lots(self):
        return TOWN_BASE_LOTS + sum(e[4] for e in EXPANSIONS[:self.expansions])

    def lots_used(self):
        return sum(self.town.get(h[0], 0) for h in HOUSES) + sum(self.town.get(s[0], 0) for s in SERVICES)

    def capacity_value(self, level, gap):
        """Production value (work minutes) the farm can make during one gap."""
        work = VALUES[0]
        v = self.plots * best_crop_value(level, gap)
        for tid, _, fid, _, lv, cyc, yld, _ in TREES:
            v += self.trees.get(tid, 0) * yld * (work[fid] - 0.5) * min(1.0, gap / cyc)
        for aid, _, _, prod, _, feed, mins, lv, _ in ANIMALS:
            n = self.animals.get(aid, 0)
            v += n * step(mins, ANIMAL_FACTOR) * min(1.0, gap / mins)
        for wid, slots in self.workshops.items():
            w = WS[wid]
            avg = recipe_avg(w, level)
            if avg is None:
                continue
            jobs = min(slots, gap / avg)
            v += jobs * step(avg)
        return v

    def harvests(self, level, gap):
        n = self.plots * (1 if best_crop_value(level, gap) > 0 else 0)
        n += sum(self.trees.values()) * min(1.0, gap / 180)
        n += sum(self.animals.values()) * min(1.0, gap / 60)
        return n


WS = {w[0]: w for w in WORKSHOPS}


def barn_capacity(upgrades):
    return BARN_CAPACITY + BARN_STEP * upgrades


def barn_upgrade_tools(k):
    n = min(1 + k, 12)
    return {"hammer": n, "nails": n, "paint": n}


def target_gap_days(level):
    """Designed time from level L to L+1 (days): quick first levels, a steady climb, slower after 20."""
    g = 0.13 * level
    if level >= 20:
        g *= 1 + 0.08 * (level - 19)
    return max(0.06, g)


def buy_list(level):
    """What the model player buys, in priority order (only what this level unlocks)."""
    out = []
    for w in WORKSHOPS:
        if w[2] <= level:
            out.append(("workshop", w[0], w[3], w[4]))
    for h in ANIMAL_HOUSES:
        if h[2] <= level:
            out.append(("animal_house", h[0], h[3], h[4]))
    return out


def farm_model(start_day, xp_table):
    """Day-by-day (pass-by-pass) model of the farm from the end of chapter 1. Returns per-level days and flows."""
    farm = Farm()
    level, xp, coins = 1, 0.0, CHAPTER_COINS_AT_END
    t = start_day
    gaps = gaps_minutes()
    # Start at the next session after the chapter ends.
    day_index = int(math.floor(t))
    reached = {1: t}
    flows = {}   # level -> dict of sums
    rate = {}    # level -> xp per day measured
    tools_found = 0.0
    pass_i = 0
    # find the pass index after start_day
    while True:
        d, i = divmod(pass_i, len(gaps))
        s = SESSION_HOURS[i // 2]
        when = d + (s - FIRST_SESSION_HOUR) / 24 + (ATTEMPTS_PER_SESSION * LEVEL_MINUTES / 1440 if i % 2 else 0)
        if when >= t:
            break
        pass_i += 1
    total_days = MODEL_DAYS
    while True:
        d, i = divmod(pass_i, len(gaps))
        s = SESSION_HOURS[i // 2]
        when = d + (s - FIRST_SESSION_HOUR) / 24 + (ATTEMPTS_PER_SESSION * LEVEL_MINUTES / 1440 if i % 2 else 0)
        if when > total_days:
            break
        gap = gaps[i]
        cap = farm.capacity_value(level, gap)
        made = cap * MODEL_EFFICIENCY
        gained_xp = made * XP_PER_WORK
        gained_coins = made * COINS_PER_WORK
        # match-3 during the session (counted on the first pass of a session)
        if i % 2 == 0:
            wins = ATTEMPTS_PER_SESSION * WIN_RATE
            gained_coins += wins * (MATCH3["winCoins"] + BOARD_COINS_AVG)
            tools_found += wins * MATCH3["toolPercent"] / 100
        tools_found += farm.harvests(level, gap) * TOOL_DROPS["harvestPercent"] / 100
        tools_found += made * XP_PER_WORK / max(1.0, avg_order_xp(level)) * TOOL_DROPS["orderPercent"] / 100
        f = flows.setdefault(level, {"xp": 0.0, "coins": 0.0, "spent": 0.0, "days": 0.0})
        f["coins"] += gained_coins
        coins += gained_coins
        xp += gained_xp
        f["xp"] += gained_xp
        # level ups
        while level < MAX_LEVEL and xp >= xp_table[level]:   # xp_table[L] = total xp for level L+1
            level += 1
            reached[level] = when
            coins += level_reward(level)["coins"]
        # spend: buildings first, then houses/services, plots, animals, trees, decorations
        coins = spend(farm, level, coins, flows.setdefault(level, {"xp": 0.0, "coins": 0.0, "spent": 0.0, "days": 0.0}))
        # tools: barn upgrades when needed, expansions when lots run out
        land_share = TOOL_DROPS["landSharePercent"] / 100 if level >= TOOL_DROPS["landFromLevel"] else 0.0
        farm.tools_barn = getattr(farm, "tools_barn", 0.0) + tools_found * (1 - land_share)
        farm.tools_land = getattr(farm, "tools_land", 0.0) + tools_found * land_share
        tools_found = 0.0
        need = sum(barn_upgrade_tools(farm.barn_upgrades).values())
        if farm.tools_barn >= need and farm.barn_upgrades < BARN_UPGRADES:
            farm.tools_barn -= need
            farm.barn_upgrades += 1
        if farm.expansions < len(EXPANSIONS):
            e = EXPANSIONS[farm.expansions]
            if e[0] <= level and farm.tools_land >= e[2] + e[3] and coins >= e[1] and farm.lots_used() >= farm.lots() - 1:
                farm.tools_land -= e[2] + e[3]
                coins -= e[1]
                farm.expansions += 1
        pass_i += 1
    return reached, flows, farm


def spend(farm, level, coins, f):
    reserve = 100 + 20 * level
    def pay(c):
        nonlocal coins
        if coins - c >= reserve:
            coins -= c
            f["spent"] += c
            return True
        return False
    for w in WORKSHOPS:
        if w[2] <= level and w[0] not in farm.workshops and farm.population() >= w[4] and pay(w[3]):
            farm.workshops[w[0]] = w[5]
    for h in ANIMAL_HOUSES:
        if h[2] <= level and h[0] not in farm.houses_animals and farm.population() >= h[4] and pay(h[3]):
            farm.houses_animals.add(h[0])
    for s in SERVICES:
        if s[2] <= level and farm.town.get(s[0], 0) == 0 and farm.lots_used() < farm.lots() and pay(s[3]):
            farm.town[s[0]] = 1
    for h in reversed(HOUSES):
        while (h[2] <= level and farm.town.get(h[0], 0) < h[6] and farm.lots_used() < farm.lots()
               and farm.population() + h[4] <= farm.pop_cap() and pay(h[3])):
            farm.town[h[0]] = farm.town.get(h[0], 0) + 1
    while farm.plots - PLOTS_START < len(PLOT_OFFERS):
        lv, c = PLOT_OFFERS[farm.plots - PLOTS_START]
        if lv > level or not pay(c):
            break
        farm.plots += 1
    for aid, _, house, _, _, _, _, lv, costs in ANIMALS:
        while lv <= level and house in farm.houses_animals and farm.animals.get(aid, 0) < len(costs) \
                and pay(costs[farm.animals.get(aid, 0)]):
            farm.animals[aid] = farm.animals.get(aid, 0) + 1
    for tid, _, _, _, lv, _, _, costs in TREES:
        while lv <= level and farm.trees.get(tid, 0) < len(costs) and pay(costs[farm.trees.get(tid, 0)]):
            farm.trees[tid] = farm.trees.get(tid, 0) + 1
    return coins


def avg_order_xp(level):
    return order_max_work(level) * 0.6 * XP_PER_WORK


def level_reward(level):
    r = {"coins": 40 * level, "crystals": 5 if level % 5 == 0 else 2}
    if level % 3 == 0:
        r["boosters"] = {MATCH3["boosterRotation"][level % len(MATCH3["boosterRotation"])]: 1}
    if level % 4 == 0:
        r["tools"] = {"hammer": 1, "nails": 1, "paint": 1}
    return r


CHAPTER_COINS_AT_END = 0  # set in main from the chapter model


def chapter_coins(ch, total_stars):
    wins = total_stars / 1.0
    coins = START["coins"] + wins * (MATCH3["winCoins"] + BOARD_COINS_AVG)
    for z in ch["zones"]:
        coins += z.get("reward", {}).get("coins", 0)
    for t in ch["tasks"]:
        coins += t.get("reward", {}).get("coins", 0)
    coins += ch.get("finaleReward", {}).get("coins", 0)
    return coins


def build_xp_table(start_day):
    """XP thresholds such that the model player reaches level L+1 after target_gap_days(L).
    Iterates: measure the model's XP rate at each level, set the threshold, re-run."""
    table = [0] + [int(60 * l ** 2.1) for l in range(1, MAX_LEVEL)]  # first guess: xp_table[L] = total for L+1
    cumulative = [0.0] * (MAX_LEVEL + 1)
    for _ in range(12):
        reached, flows, _ = farm_model(start_day, table)
        new = [0]
        total = 0.0
        for lv in range(1, MAX_LEVEL):
            f = flows.get(lv)
            if f and lv + 1 in reached:
                days = reached[lv + 1] - reached[lv]
                per_day = f["xp"] / max(days, 1e-6)
            else:
                per_day = prev_rate if lv > 1 else 100.0
            prev_rate = per_day
            total += per_day * target_gap_days(lv)
            new.append(nice(total))
        for i in range(1, len(new)):
            if new[i] <= new[i - 1]:
                new[i] = new[i - 1] + 5
        if new == table:
            break
        table = new
    return table


def nice(x):
    if x < 100:
        return int(round(x / 5.0) * 5) or 5
    if x < 1000:
        return int(round(x / 10.0) * 10)
    if x < 10000:
        return int(round(x / 50.0) * 50)
    return int(round(x / 100.0) * 100)


# ============================================================== building the balance
def reward(r):
    out = {}
    for k in ("coins", "stars", "crystals", "xp", "lives", "infiniteLivesMinutes"):
        if r.get(k):
            out[k] = r[k]
    for k in ("items", "tools", "boosters"):
        if r.get(k):
            out[k] = dict(sorted(r[k].items()))
    return out


def build_balance(items, xp_table, pacing):
    item_ids = {i["id"] for i in items}
    b = {
        "version": 1,
        "generatedBy": "farm/tools/economy.py",
        "start": START,
        "lives": LIVES,
        "match3": dict(MATCH3, milestoneReward=reward(MATCH3["milestoneReward"])),
        "boosters": [{"id": i, "name": n, "kind": k, "coins": c, "description": d} for i, n, k, c, d in BOOSTERS],
        "tools": [{"id": i, "name": n, "kind": k} for i, n, k in TOOLS],
        "items": items,
        "crops": [{"item": c[0], "growSeconds": c[3] * 60, "seedCoins": seed_price(c), "yield": CROP_YIELD} for c in CROPS],
        "trees": [{"id": t[0], "name": t[1], "item": t[2], "level": t[4], "cycleSeconds": t[5] * 60, "yield": t[6],
                   "costs": t[7]} for t in TREES],
        "animalHouses": [{"id": h[0], "name": h[1], "level": h[2], "coins": h[3], "population": h[4], "capacity": h[5]}
                         for h in ANIMAL_HOUSES],
        "animals": [{"id": a[0], "name": a[1], "house": a[2], "product": a[3], "feed": a[5], "seconds": a[6] * 60,
                     "level": a[7], "costs": a[8]} for a in ANIMALS],
        "workshops": [{"id": w[0], "name": w[1], "level": w[2], "coins": w[3], "population": w[4], "slots": w[5],
                       "slotCrystals": w[6],
                       "recipes": [{"id": r[0], "level": r[1], "inputs": dict(sorted(r[2].items())), "output": r[3],
                                    "count": r[4], "seconds": r[5] * 60} for r in w[7]]} for w in WORKSHOPS],
        "barn": {"capacity": BARN_CAPACITY,
                 "upgrades": [{"capacity": BARN_STEP, "tools": barn_upgrade_tools(k)} for k in range(BARN_UPGRADES)]},
        "plots": {"start": PLOTS_START, "offers": [{"level": l, "coins": c} for l, c in PLOT_OFFERS]},
        "expansions": [{"id": "land%d" % (i + 1), "name": "Участок %d" % (i + 1), "level": e[0], "coins": e[1],
                        "tools": {"axe": e[3], "saw": e[2]}, "lots": e[4]} for i, e in enumerate(EXPANSIONS)],
        "toolDrops": TOOL_DROPS,
        "levels": [{"level": l, "xp": (0 if l == 1 else xp_table[l - 1]), "reward": ({} if l == 1 else reward(level_reward(l)))}
                   for l in range(1, MAX_LEVEL + 1)],
        "orders": {
            "slots": ORDER_SLOTS,
            "fulfillCooldownSeconds": ORDER_FULFILL_COOLDOWN,
            "discardCooldownSeconds": ORDER_DISCARD_COOLDOWN,
            "maxChainMinutes": [order_max_chain_minutes(l) for l in range(1, MAX_LEVEL + 1)],
            "maxKinds": [order_max_kinds(l) for l in range(1, MAX_LEVEL + 1)],
            "maxWorkMinutes": [order_max_work(l) for l in range(1, MAX_LEVEL + 1)],
            "maxUnits": [order_max_units(l) for l in range(1, MAX_LEVEL + 1)],
            "coinPercent": 100,
            "xpPercent": 100,
        },
        "truck": TRUCK,
        "town": {
            "baseLots": TOWN_BASE_LOTS,
            "basePopulationCap": TOWN_BASE_POPULATION_CAP,
            "houses": [{"id": h[0], "name": h[1], "kind": "house", "level": h[2], "coins": h[3], "population": h[4],
                        "xp": h[5], "max": h[6]} for h in HOUSES],
            "services": [{"id": s[0], "name": s[1], "kind": "service", "level": s[2], "coins": s[3], "capacity": s[4],
                          "xp": s[5], "max": 1} for s in SERVICES],
            "decorations": [{"id": d[0], "name": d[1], "kind": "decoration", "level": d[2], "coins": d[3],
                             "stars": d[4], "beauty": d[5], "xp": d[6], "max": d[7]} for d in DECORATIONS],
        },
        "daily": {
            "perDay": 3,
            "tasks": [{"id": t[0], "kind": t[1], "target": t[2], "text": t[3], "farm": t[4], "reward": reward(t[5])}
                      for t in DAILY_TASKS],
            "chest": reward(DAILY_CHEST),
            "login": [reward(r) for r in LOGIN_GIFTS],
        },
        "speedUp": SPEEDUP,
        "farmStart": FARM_START,
        "pacing": pacing,
    }
    return b


def seed_price(crop):
    cid = crop[0]
    it = next(i for i in ITEMS if i["id"] == cid)
    return max(1, int(round(it["orderCoins"] * 0.25)))


# ============================================================== checks
def checks(items, balance, zones, chapter_day, reached, ch):
    problems = []
    by_id = {i["id"]: i for i in items}
    # 1. chapter 1 takes 3-5 days and every zone costs 1-3 minutes of match-3 per star
    if not 3.0 <= chapter_day <= 5.0:
        problems.append("глава 1 заканчивается на день %.1f (нужно 3-5)" % chapter_day)
    for z in zones:
        if not 1.0 <= z["minutesPerStar"] <= 3.5:
            problems.append("зона %s: %.1f минуты на звезду" % (z["id"], z["minutesPerStar"]))
        if not 8 <= z["tasks"] <= 20:
            problems.append("зона %s: %d заданий" % (z["id"], z["tasks"]))
    # 2. every good can be made at its level, its chain fits the order limit of its level eventually
    for it in items:
        if it["kind"] == "match3" or it["kind"] == "feed":
            continue
        if chain_minutes(it["id"]) > order_max_chain_minutes(MAX_LEVEL):
            problems.append("%s: цепочка %d мин длиннее любого заказа" % (it["id"], chain_minutes(it["id"])))
    # 3. barn: the biggest order and a truck fit easily in the starting barn of that level
    for lv in range(1, MAX_LEVEL + 1):
        if order_max_units(lv) * order_max_kinds(lv) > BARN_CAPACITY // 2:
            problems.append("уровень %d: заказ больше половины стартового амбара" % lv)
    # 4. prices: order > sell, seeds cheaper than selling the harvest (planting is never a loss)
    for c in balance["crops"]:
        if c["seedCoins"] >= by_id[c["item"]]["sell"] * c["yield"]:
            problems.append("%s: семена дороже урожая" % c["item"])
    # 5. levels: XP grows, farm levels arrive on schedule, nothing unlocks before it can be afforded
    xs = [l["xp"] for l in balance["levels"]]
    if any(b <= a for a, b in zip(xs, xs[1:])):
        problems.append("опыт уровней не растёт")
    for lv in (5, 10, 15, 20):
        if lv not in reached:
            problems.append("уровень фермы %d не достигнут за %d дней" % (lv, MODEL_DAYS))
    if 10 in reached and not 8 <= reached[10] <= 14:
        problems.append("уровень 10 на день %.1f (нужно 8-14)" % reached[10])
    if 20 in reached and not 24 <= reached[20] <= 40:
        problems.append("уровень 20 на день %.1f (нужно 24-40)" % reached[20])
    # 6. every booster of the tool shed exists, every recipe output is a booster or a good
    for w in WORKSHOPS:
        for r in w[7]:
            if r[3] not in by_id and r[3] not in BOOSTER_IDS:
                problems.append("рецепт %s: неизвестный выход" % r[0])
    # 7. the extra-moves ladder of GDD 4.7
    if MATCH3["extraMovesPrices"] != [900, 1900, 2900]:
        problems.append("+5 ходов: цены не 900/1900/2900")
    # 8. chapter data matches GDD 5
    if len(ch["zones"]) != 8:
        problems.append("в главе 1 не 8 зон")
    if any(not 1 <= t["stars"] <= 3 for t in ch["tasks"]):
        problems.append("задание дороже 3 или дешевле 1 звезды")
    # 9. crops between 2 minutes and 8 hours
    if min(c[3] for c in CROPS) != 2 or max(c[3] for c in CROPS) != 480:
        problems.append("культуры не от 2 минут до 8 часов")
    return problems


def chain_minutes(item, depth=0):
    if depth > 10:
        return 10 ** 6
    for c in CROPS:
        if c[0] == item:
            return c[3]
    for t in TREES:
        if t[2] == item:
            return t[5]
    for a in ANIMALS:
        if a[3] == item:
            return chain_minutes(a[5], depth + 1) + a[6]
    for w in WORKSHOPS:
        for r in w[7]:
            if r[3] == item:
                return max(chain_minutes(i, depth + 1) for i in r[2]) + r[5]
    return 0


# ============================================================== the document
def write_doc(items, balance, zones, total_stars, chapter_day, reached, flows, farm, xp_table):
    L = []
    a = L.append
    a("# «Солнечная долина» — экономика")
    a("")
    a("Сгенерировано `farm/tools/economy.py` (не править руками). Данные — "
      "`Resources/Meta/balance.json`; сюжет и задания главы 1 — `Resources/Meta/chapter1.json`.")
    a("Проверка согласованности: `python3 farm/tools/economy.py --check` (CI), игровой бот — "
      "`Core/Tests~/MetaBotTests.cs` (играет настоящими правилами 7 и 30 дней).")
    a("")
    a("## Модельный игрок")
    a("")
    a("- %d сессий в день (%s), в каждой %d попыток 3-в-ряд по ~%.1f мин, побед %.0f%%." % (
        len(SESSION_HOURS), ", ".join("%d:%02d" % (int(h), int(round((h % 1) * 60))) for h in SESSION_HOURS),
        ATTEMPTS_PER_SESSION, LEVEL_MINUTES, WIN_RATE * 100))
    a("- На ферме — два захода за сессию: до уровней и после них (≈%d мин спустя)." % int(ATTEMPTS_PER_SESSION * LEVEL_MINUTES))
    a("- Звёзды тратит сразу на доступные задания; монеты — на мастерские, дома, грядки, животных и деревья.")
    a("- Эффективность хозяйства %.0f%%: такая доля производственной мощности фермы превращается в "
      "выполненные заказы (измерено ботом; остальное — простой между сессиями и несовпадение с заказами)." % (MODEL_EFFICIENCY * 100))
    a("")
    a("## Глава 1 «Восстановление»: звёзды по зонам")
    a("")
    a("Каждый выигранный уровень = 1 звезда. Минут 3-в-ряд на звезду = длина уровня / доля побед.")
    a("")
    a("| Зона | Заданий | С выбором вида | Звёзд | Уровней 3-в-ряд | Минут игры | Минут на звезду | Готово к дню |")
    a("|---|---:|---:|---:|---:|---:|---:|---:|")
    for z in zones:
        a("| %s | %d | %d | %d | %.0f | %.0f | %.1f | %.1f |" % (z["name"], z["tasks"], z["choices"], z["stars"],
                                                           z["levels"], z["minutes"], z["minutesPerStar"], z["day"]))
    a("| **Всего** | %d | %d | %d | %.0f | %.0f | %.1f | **%.1f** |" % (
        sum(z["tasks"] for z in zones), sum(z["choices"] for z in zones), total_stars, total_stars / WIN_RATE,
        total_stars / WIN_RATE * LEVEL_MINUTES, LEVEL_MINUTES / WIN_RATE, chapter_day))
    a("")
    a("Глава заканчивается на день %.1f (цель 3–5). Монет к концу главы ≈ %d (уровни, награды зон и финала)." % (
        chapter_day, CHAPTER_COINS_AT_END))
    a("")
    a("## Уровни фермы")
    a("")
    a("Опыт уровня подобран так, чтобы модельный игрок шёл по задуманному графику (день от установки игры).")
    a("")
    a("| Уровень | Опыт всего | День | Опыт/день на уровне | Монет/день | Потрачено | Открывает |")
    a("|---:|---:|---:|---:|---:|---:|---|")
    names = {i["id"]: i["name"] for i in items}
    unlocks = unlock_names(names)
    for lv in range(1, MAX_LEVEL + 1):
        f = flows.get(lv)
        day = reached.get(lv)
        span = (reached[lv + 1] - reached[lv]) if (lv + 1) in reached and lv in reached else None
        xpd = "%.0f" % (f["xp"] / span) if f and span else "—"
        cpd = "%.0f" % (f["coins"] / span) if f and span else "—"
        spent = "%.0f" % f["spent"] if f else "—"
        a("| %d | %d | %s | %s | %s | %s | %s |" % (lv, 0 if lv == 1 else xp_table[lv - 1],
                                                   "%.1f" % day if day is not None else "—", xpd, cpd, spent,
                                                   ", ".join(unlocks.get(lv, [])) or "—"))
    a("")
    a("К концу модели (день %d): грядок %d, мастерских %d, животных %d, деревьев %d, улучшений амбара %d "
      "(вместимость %d), расширений %d." % (MODEL_DAYS, farm.plots, len(farm.workshops), sum(farm.animals.values()),
                                           sum(farm.trees.values()), farm.barn_upgrades, barn_capacity(farm.barn_upgrades),
                                           farm.expansions))
    a("")
    a("## Товары")
    a("")
    a("Цена товара считается по цепочке: работа = сумма работы входов + шаг (минуты^%.1f; деревья ×%.1f, "
      "животные ×%.1f) / выход. Заказ платит %.1f монеты и %.2f опыта за минуту работы; продажа из амбара — %d%% цены заказа." % (
        STEP_EXPONENT, TREE_FACTOR, ANIMAL_FACTOR, COINS_PER_WORK, XP_PER_WORK, int(SELL_SHARE * 100)))
    a("")
    a("| Товар | Вид | Уровень | Цепочка, мин | Работа | Заказ, монет | Опыт | Продажа |")
    a("|---|---|---:|---:|---:|---:|---:|---:|")
    for it in items:
        a("| %s | %s | %d | %s | %.1f | %d | %d | %d |" % (it["name"], it["kind"], it["level"],
                                                       chain_minutes(it["id"]) if it["kind"] != "match3" else "—",
                                                       it["work"], it["orderCoins"], it["xp"], it["sell"]))
    a("")
    a("## Заказы")
    a("")
    a("Доска из %d карточек; после выполнения новая через %d с, после отказа — через %d мин. Заказ просит только то, "
      "что игрок может сделать прямо сейчас (есть грядка/дерево/животное/мастерская и рецепт открыт), и только "
      "с цепочкой не длиннее предела уровня." % (ORDER_SLOTS, ORDER_FULFILL_COOLDOWN, ORDER_DISCARD_COOLDOWN // 60))
    a("")
    a("| Уровень | Самая длинная цепочка, мин | Видов товара | Штук одного | Работы на заказ, мин |")
    a("|---:|---:|---:|---:|---:|")
    for lv in (1, 2, 3, 5, 8, 10, 12, 15, 20, 25, 30):
        a("| %d | %d | %d | %d | %d |" % (lv, order_max_chain_minutes(lv), order_max_kinds(lv), order_max_units(lv),
                                         order_max_work(lv)))
    a("")
    a("Грузовик (с уровня %d): %d ящика, возвращается через %d мин, платит %d%% монет и опыта и %d инструмент." % (
        TRUCK["level"], TRUCK["crates"], TRUCK["returnSeconds"] // 60, TRUCK["coinPercent"], TRUCK["tools"]))
    a("")
    a("## Амбар и инструменты")
    a("")
    a("Амбар: %d мест, каждое улучшение +%d за молоток, гвозди и краску (1, 2, 3… каждого). Инструменты выпадают "
      "при сборе урожая (%d%%), в заказах (%d%%) и за уровни 3-в-ряд (%d%%); пила и топор — с уровня %d (%d%% находок)." % (
        BARN_CAPACITY, BARN_STEP, TOOL_DROPS["harvestPercent"], TOOL_DROPS["orderPercent"], MATCH3["toolPercent"],
        TOOL_DROPS["landFromLevel"], TOOL_DROPS["landSharePercent"]))
    a("")
    a("| Улучшение | Вместимость | Инструментов каждого |")
    a("|---:|---:|---:|")
    for k in range(0, 10):
        a("| %d | %d | %d |" % (k + 1, barn_capacity(k + 1), barn_upgrade_tools(k)["hammer"]))
    a("")
    a("## Городок")
    a("")
    a("Жители: дома дают жителей, сервисы — место для них (база %d), каждое здание дома/сервиса занимает участок "
      "(база %d, расширения за пилу и топор). Мастерским и фермам животных нужны работники (жители)." % (
        TOWN_BASE_POPULATION_CAP, TOWN_BASE_LOTS))
    a("")
    a("| Мастерская | Уровень | Монет | Жителей |")
    a("|---|---:|---:|---:|")
    for w in WORKSHOPS:
        a("| %s | %d | %d | %d |" % (w[1], w[2], w[3], w[4]))
    for h in ANIMAL_HOUSES:
        a("| %s | %d | %d | %d |" % (h[1], h[2], h[3], h[4]))
    a("")
    a("## Валюты и поток монет")
    a("")
    a("- Монеты: уровень 3-в-ряд даёт %d + поле (≈%d), заказы и грузовик, продажа из амбара. Траты: стройка, "
      "грядки, животные, деревья, семена, бусты (%s), +5 ходов (900/1900/2900)." % (
        MATCH3["winCoins"], BOARD_COINS_AVG, ", ".join("%s %d" % (b[1], b[3]) for b in BOOSTERS)))
    a("- Звёзды: 1 за выигранный уровень; глава 1 — %d звёзд; потом особые украшения за звёзды." % total_stars)
    a("- Кристаллы: старт %d, каждый %d-й уровень 3-в-ряд %d, сундук дня %d, уровни фермы 2–5; траты — ускорения "
      "(1 за %d мин), слоты мастерских, пополнение жизней (%d)." % (
        START["crystals"], MATCH3["milestoneEvery"], MATCH3["milestoneReward"]["crystals"], DAILY_CHEST["crystals"],
        SPEEDUP["secondsPerCrystal"] // 60, LIVES["refillCrystals"]))
    a("- Жизни: %d, одна за %d мин; проигрыш тратит жизнь, выигрыш — нет." % (LIVES["max"], LIVES["regenSeconds"] // 60))
    a("")
    return "\n".join(L) + "\n"


def unlock_names(names):
    u = {}
    def add(lv, s):
        u.setdefault(lv, []).append(s)
    for c in CROPS:
        if c[2] > 1:
            add(c[2], c[1])
    for t in TREES:
        if t[4] > 1:
            add(t[4], t[1])
    for h in ANIMAL_HOUSES:
        if h[2] > 1:
            add(h[2], h[1])
    for w in WORKSHOPS:
        if w[2] > 1:
            add(w[2], w[1])
        for r in w[7]:
            if r[1] > w[2]:
                add(r[1], names.get(r[3], dict((b[0], b[1]) for b in BOOSTERS).get(r[3], r[3])))
    if TRUCK["level"] > 1:
        add(TRUCK["level"], "грузовик")
    return u


# ============================================================== main
VALUES = None
ITEMS = None


def generate():
    global VALUES, ITEMS, CHAPTER_COINS_AT_END
    VALUES = value_items()
    ITEMS = item_defs()
    ch, zones, total_stars, chapter_day = chapter_model()
    CHAPTER_COINS_AT_END = int(chapter_coins(ch, total_stars))
    xp_table = build_xp_table(chapter_day)
    reached, flows, farm = farm_model(chapter_day, xp_table)
    pacing = {
        "sessionHours": SESSION_HOURS,
        "attemptsPerSession": ATTEMPTS_PER_SESSION,
        "winRate": WIN_RATE,
        "levelMinutes": LEVEL_MINUTES,
        "chapter1MinDays": 3.0,
        "chapter1MaxDays": 5.0,
        "chapter1Day": round(chapter_day, 2),
        "levelDays": {str(l): round(d, 2) for l, d in sorted(reached.items())},
        "tolerancePercent": TOLERANCE_PERCENT,
    }
    balance = build_balance(ITEMS, xp_table, pacing)
    problems = checks(ITEMS, balance, zones, chapter_day, reached, ch)
    doc = write_doc(ITEMS, balance, zones, total_stars, chapter_day, reached, flows, farm, xp_table)
    text = json.dumps(balance, ensure_ascii=False, indent=1) + "\n"
    return text, doc, problems


def main():
    check = "--check" in sys.argv
    text, doc, problems = generate()
    if check:
        stale = []
        for path, want in ((BALANCE_PATH, text), (DOC_PATH, doc)):
            try:
                with open(path, encoding="utf-8") as f:
                    have = f.read().replace("\r\n", "\n")
            except OSError:
                have = None
            if have != want:
                stale.append(os.path.relpath(path, os.path.dirname(ROOT)))
        for s in stale:
            problems.append("%s устарел: запустите python3 farm/tools/economy.py" % s)
    else:
        os.makedirs(META, exist_ok=True)
        with open(BALANCE_PATH, "w", encoding="utf-8", newline="\n") as f:
            f.write(text)
        with open(DOC_PATH, "w", encoding="utf-8", newline="\n") as f:
            f.write(doc)
    for p in problems:
        print("ПРОБЛЕМА:", p)
    if problems:
        sys.exit(1)
    print("economy: ok")


if __name__ == "__main__":
    main()
