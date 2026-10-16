"""Full economy model of "Last Hearth" for a free player, day by day for a year.

Builds on tools/pacing_sim.py (hearth level by day, materials per level) and adds every
other flow: coal and food upkeep, essence and squad power, campaign gating, arena,
events, shop, diamonds (Iskry), speed-ups and builders. Runs consistency checks and writes
docs/ECONOMY.md.

Run: python hearth/tools/economy.py
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pacing_sim as pacing  # noqa: E402

DAYS = 365
OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "docs", "ECONOMY.md")

# ---------------------------------------------------------------- fortress and people

def hearth_warmth(level):
    """GDD 7: warmth units produced by the hearth."""
    return 100 + 35 * (level - 1)


def dweller_cap(level):
    """GDD 8: 6 + 2 per hearth level, 64 at level 30."""
    return 6 + 2 * (level - 1)


SQUAD_SLOTS = (8, 14, 20, 26)


def squad_size(level):
    """GDD 10: 5 heroes, slots open at hearth 8, 14, 20, 26."""
    return 5 + sum(1 for gate in SQUAD_SLOTS if level >= gate)


def builders(level, bought_fourth):
    return pacing.builders(level) + (1 if bought_fourth else 0)


# ---------------------------------------------------------------- materials (silver + iron)
# Where materials come from (share of the hourly income the pacing model needs).
MATERIAL_SOURCES = {
    "Производство залов": 0.52,
    "Кампания": 0.15,
    "Вылазки": 0.10,
    "События": 0.18,
    "Арена, набеги и поручения": 0.05,
}
EFFECTIVE_HOURS_PER_DAY = 23.5  # 4 sessions, offline cap 10 h covers the night (pacing model)
SILVER_SHARE = 0.6              # the rest is iron

# Where materials go (share of the daily income). Building is what pacing needs; the rest is spent
# on heroes and gear, so a free player never piles up resources.
MATERIAL_SINKS = {
    "Стройка (очаг и залы)": 0.80,
    "Снаряжение и кузня": 0.10,
    "Тренировка жителей": 0.03,
    "Исследования в библиотеке": 0.03,
    "Найм в таверне": 0.04,
}

BUILD_SHARE = pacing.BUILD_SHARE  # = MATERIAL_SINKS["Стройка (очаг и залы)"]


def total_income(level):
    """All materials per hour. pacing_sim.income() is the part that goes to building."""
    return pacing.income(level) / BUILD_SHARE


hearth_job = pacing.hearth_job


def nice(x, digits=2):
    """Round up to two (or more) significant digits."""
    if x <= 0:
        return 0
    p = 10 ** max(int(math.floor(math.log10(x))) - digits + 1, 0)
    return int(math.ceil(x / p) * p)


def storage(row):
    """Warehouse capacity (silver + iron) with the warehouse at the cap of this hearth level:
    the hearth price plus 5% (anti-rush, GDD 9.5). Three significant digits keep the rule exact."""
    return nice(pacing.storage_cap(row), 3)


STORAGE_GATE_FROM = 10  # the first day (tutorial) is free of the warehouse rule (MIN_STORAGE)


def hero_gate(result):
    """(hearth level, requirement, five best heroes of the free player when the hearth job starts)."""
    rows, reached = result["rows"], result["reached_run"]
    by_day = {d["day"]: d for d in result["days"]}
    out = []
    for lv in range(HERO_GATE_FROM, pacing.MAX_LEVEL):
        if lv + 1 not in reached or reached[lv + 1] > DAYS:
            break
        t = hearth_start_day(rows, reached, lv)
        out.append((lv, hero_requirement(lv), by_day[max(1, int(t))]["top5"]))
    return out


def rusher_max_level(essence):
    """The highest hearth a player can reach with this much essence, however many diamonds he has:
    every hearth job from level 5 on needs five heroes at the requirement."""
    level = 5
    while level < 30 and core_essence(level) <= essence:
        level += 1
    return level


# ---------------------------------------------------------------- coal and food upkeep

def coal_burn_per_hour(level):
    """Rooms use 85% of the hearth warmth; 10 warmth units burn 1 coal per hour (mode 'steady')."""
    return hearth_warmth(level) * 0.85 / 10.0


def coal_production_per_hour(level):
    """Mines produce 15% more than the steady burn: enough for the evening Surge (x1.5 for an hour)."""
    return coal_burn_per_hour(level) * 1.15


def food_per_hour(level):
    return dweller_cap(level) * 1.0  # each dweller eats 1 food per hour


# ---------------------------------------------------------------- essence and heroes

def essence_income_per_day(level):
    """Daily essence from Surge, arena, expeditions and repeats (GDD 10, 11). First clears of the
    campaign and events add their own. The sources follow campaign progress and squad power, and
    essence is never sold for diamonds, so a player cannot rush it."""
    return 170 * 1.13 ** (level - 1)


def hero_level_cost(hero_level):
    return 40 * 1.11 ** hero_level


def hero_power(hero_level, hearth_level):
    """Level and gear (gear quality follows the hearth level)."""
    return 100 * 1.07 ** hero_level * 1.05 ** hearth_level


def hero_level_cap(hearth_level):
    return 2 * hearth_level + 5


# Anti-rush (GDD 9.5): the hearth job level -> level+1 starts only when five heroes have reached
# this level. A free player is HERO_SLACK levels above it, so the rule stops only rushers.
HERO_CORE = 5
HERO_GATE_FROM = 10  # the first day (tutorial) is free of the hero rule
HERO_SLACK = 2
MISSION_ESSENCE_DAYS = 0.6  # a first clear of a campaign battle gives 0.6 of a day of essence
STARTER_ESSENCE = 9000      # tutorial and first-day quests (heroes grow fast at first)
STARTER_DAYS = 2


KEY_ROOMS_EARLY = ["Жилые покои", "Кухня", "Угольная шахта", "Казарма", "Рудник", "Кузня", "Лазарет", "Библиотека",
                   "Тренировочный зал"]
KEY_ROOMS_CYCLE = ["Казарма", "Кузня", "Лазарет", "Библиотека", "Тренировочный зал"]


def key_room(level):
    """The one room that must be at level L before the hearth job L -> L+1 (anti-rush rule 2)."""
    if level <= len(KEY_ROOMS_EARLY):
        return KEY_ROOMS_EARLY[level - 1]
    return KEY_ROOMS_CYCLE[(level - 10) % len(KEY_ROOMS_CYCLE)]


def hero_requirement(level):
    return 0 if level < HERO_GATE_FROM else 2 * level - 2


def essence_to_level(h):
    """Essence to raise one hero from level 1 to level h."""
    return sum(hero_level_cost(k) for k in range(1, h))


def core_essence(level):
    """Essence for five heroes at the requirement of this hearth level."""
    return HERO_CORE * essence_to_level(hero_requirement(level))


def hearth_start_day(rows, reached, level):
    """Day the hearth job of this level starts (all rooms done)."""
    return reached[level + 1] - rows[level - 1]["hearth_h"] / 24.0


# ---------------------------------------------------------------- campaign (GDD 11)
ACTS = 10
MISSIONS_PER_ACT = 18  # plus a boss
MISSIONS = ACTS * (MISSIONS_PER_ACT + 1)
# Day by which a free player reaches mission N: fast at first, then a new battle every 2-3 days,
# so the campaign never stalls for weeks (the gate inside it is squad power).
CAMPAIGN_PACE = [(0, 0), (12, 1), (19, 2), (38, 5), (57, 10), (76, 18), (95, 32), (114, 55),
                 (133, 90), (152, 140), (171, 195), (190, 255)]
MISSIONS_PER_DAY_MAX = 12
MAX_MISSION_GAP_DAYS = 3.5


def mission_day(n):
    """Target day of mission n (1..190), linear between the pace points."""
    for (m0, d0), (m1, d1) in zip(CAMPAIGN_PACE, CAMPAIGN_PACE[1:]):
        if n <= m1:
            return d0 + (d1 - d0) * (n - m0) / (m1 - m0)
    return CAMPAIGN_PACE[-1][1]

# ---------------------------------------------------------------- diamonds (Iskry)
SPEEDUP_POINTS = [(1, 1), (60, 20), (24 * 60, 260), (7 * 24 * 60, 1000)]  # minutes -> diamonds


FREE_FINISH_MINUTES = 5  # the last 5 minutes of any job are finished for free


def speedup_cost(minutes):
    """Diamonds to finish a timer: log-log interpolation (cheaper per minute for long timers)."""
    if minutes <= FREE_FINISH_MINUTES:
        return 0
    pts = SPEEDUP_POINTS
    if minutes <= pts[0][0]:
        return pts[0][1]
    for (m0, c0), (m1, c1) in zip(pts, pts[1:]):
        if minutes <= m1:
            t = (math.log(minutes) - math.log(m0)) / (math.log(m1) - math.log(m0))
            return math.ceil(math.exp(math.log(c0) + (math.log(c1) - math.log(c0)) * t))
    m0, c0 = pts[-1]
    return math.ceil(c0 * minutes / m0)


DIAMONDS_DAILY_TASKS = 10      # 3 of 5 daily tasks
DIAMONDS_LOGIN_CALENDAR = 140  # per 28 days (days need not be consecutive)
ACHIEVEMENT_POOL = 3000        # one-time, front-loaded
DIAMONDS_PER_ACT_BOSS = 50
DIAMONDS_PER_3_STAR_CHEST = 5  # one chest per mission with 3 stars
FOURTH_BUILDER_PRICE = 2500

# Spending habits of a free player (diamonds per day once available).
ARENA_UNLOCK = 16              # PvP waits until the two-week hook is over (docs/RESEARCH.md)
ARENA_EXTRA_TICKETS = 20       # an extra arena ticket, bought twice a week (league weekends)
SHOP_REFRESH = 10              # one paid refresh of the daily shop

# ---------------------------------------------------------------- PvP (docs/MODES.md, 2-3)
# Until the server exists both modes are played against ghosts of the same hearth level.
ARENA_TICKETS = 5              # tickets in stock, one comes back every 3 hours
ARENA_TICKET_HOURS = 3
ARENA_START_RATING = 1000
# league: (name, rating from, diamonds a week, Glory a week, essence days a week)
ARENA_LEAGUES = [("Пепел", 0, 20, 100, 0.3), ("Бронза", 1100, 40, 200, 0.5), ("Серебро", 1250, 60, 300, 0.7),
                 ("Золото", 1400, 80, 450, 1.0), ("Пламя", 1600, 100, 600, 1.3), ("Легенда Очага", 1800, 150, 800, 1.5)]
ARENA_WIN_ESSENCE = 0.015      # of the daily essence per win (0.005 per loss): with the league, the 15% arena share
ARENA_GLORY = (10, 3)          # Glory for a win, a loss
RAID_UNLOCK = 17
RAID_FLAGS = 3                 # flags in stock, one comes back every 4 hours
RAID_FLAG_HOURS = 4
RAID_LOOT_HOURS = 0.5          # materials of a won raid in hours of income, paid by the game, not by the defender
RAID_TROPHY_GLORY = 20         # «Трофеи» of a 3-star raid go to the Glory shop
RAID_REVENGE_HOURS = 24        # a free revenge raid with double loot
# Glory shop: (goods, price, per week, amount: essence days or hourglass minutes)
GLORY_SHOP = [("Essence", 150, 3, 0.25), ("Hourglass", 250, 2, 60), ("Seal", 900, 1, 0)]

# ---------------------------------------------------------------- events (docs/EVENTS.md)
# Day 1 of the 28-day cycle is a Monday. At most two events run at once.
# name: (period, first day, days, diamonds won, diamonds a free player spends, essence days,
#        materials in hours of income, hourglass minutes)
EVENTS = {
    "Долгая ночь":       (7, 6, 2, 50, 30, 1.0, 6, 90),
    "Портал в Туман":    (14, 1, 4, 80, 100, 1.5, 12, 180),
    "Турнир Стражей":    (14, 8, 3, 60, 40, 1.0, 8, 90),
    "Кладоискатели":     (28, 10, 5, 150, 60, 1.0, 20, 360),
    "Ярмарка Пепла":     (28, 18, 3, 20, 0, 0.5, 12, 90),
    "Осада застав":      (28, 25, 4, 80, 60, 1.5, 16, 180),
    "Праздник очага":    (91, 60, 10, 200, 100, 2.0, 24, 720),
}
EVENT_IDS = {"Долгая ночь": "LongNight", "Портал в Туман": "Portal", "Турнир Стражей": "Tournament",
             "Кладоискатели": "Treasure", "Ярмарка Пепла": "Fair", "Осада застав": "Siege", "Праздник очага": "Festival"}
# Something new every day or two of the two-week hook: hearth 9 is the first evening, 15 is day 14.
EVENT_UNLOCK_HEARTH = {"Праздник очага": 9, "Ярмарка Пепла": 9, "Кладоискатели": 10, "Долгая ночь": 11,
                       "Портал в Туман": 12, "Турнир Стражей": 13, "Осада застав": 15}

HOURGLASS_DAILY_MINUTES = 45   # daily tasks (3 of 5) give a 45-minute hourglass
HOURGLASS_SURGE_MINUTES = 15   # a won evening Surge gives 15 minutes (from hearth 2)

# Diamond shop (docs/MODES.md, section 5)
RESERVE = 300                  # a free player keeps a small reserve for event extras
PACK_PRICE = 30                # one hour of full material income; a speed-up hour costs 20
PACKS_PER_WEEK = 14


def event_running(name, day):
    period, first, duration = EVENTS[name][:3]
    if day < first:
        return False
    return (day - first) % period < duration


def event_starts(name, day):
    period, first = EVENTS[name][:2]
    return day >= first and (day - first) % period == 0


# ---------------------------------------------------------------- simulation

def level_by_day(reached):
    level_on = {}
    level = 1
    for day in range(DAYS + 2):
        while level + 1 in reached and reached[level + 1] <= day:
            level += 1
        level_on[day] = level
    return level_on


def builder_gated(row, reached):
    """True while builders, not materials, hold the player back at this level."""
    lv = row["level"]
    if lv + 1 not in reached:
        return False
    window_h = max((reached[lv + 1] - reached[lv]) * 24, 0.1)
    work_h = row["hearth_h"] + row["room_h"] * row["rooms"] / pacing.builders(lv)
    return work_h > 0.7 * window_h


def run(rows, reached):
    level_on = level_by_day(reached)
    gated = {row["level"]: builder_gated(row, reached) for row in rows}
    days = []
    diamonds = 0.0
    achievements_left = ACHIEVEMENT_POOL
    fourth_builder_day = None
    essence = 0.0
    squad_levels = []
    acts_done = 0
    missions_done = 0
    act_finish_day = {}
    act_power = {}
    act_open_level = {}
    essence_by_day = {}  # day -> all essence received so far
    essence_got = 0.0
    earned_by = {k: 0.0 for k in ("Поручения дня", "Календарь входа", "Достижения", "Кампания", "Арена", "События")}
    spent_by = {k: 0.0 for k in ("Четвёртый строитель", "Билет арены и обновление лавки", "Доп. попытки в событиях",
                                 "Ускорения", "Наборы ресурсов")}
    speed = {}      # day -> speed-up minutes (hourglasses + diamonds)
    packs = {}      # day -> extra hours of full income bought with diamonds
    hourglass_total = 0.0
    diamond_minutes_total = 0.0
    packs_this_week = 0
    for day in range(1, DAYS + 1):
        lv = level_on[day]
        row = rows[min(lv, len(rows)) - 1]
        rec = {"day": day, "level": lv}
        rec["materials"] = total_income(lv) * EFFECTIVE_HOURS_PER_DAY

        # essence -> heroes: the five core heroes first (the hearth requirement), then the rest of
        # the squad up to three levels behind them, then everyone evenly up to the cap
        got = essence_income_per_day(lv) + (STARTER_ESSENCE / STARTER_DAYS if day <= STARTER_DAYS else 0)
        for name, spec in EVENTS.items():
            if lv >= EVENT_UNLOCK_HEARTH[name] and event_starts(name, day):
                got += essence_income_per_day(lv) * spec[5]
        essence += got
        essence_got += got
        while len(squad_levels) < squad_size(lv):
            squad_levels.append(max(1, min(squad_levels) - 2) if squad_levels else 1)
        cap = hero_level_cap(lv)
        core_target = min(cap, hero_requirement(lv) + HERO_SLACK)
        while True:
            squad_levels.sort(reverse=True)
            core, rest = squad_levels[:HERO_CORE], squad_levels[HERO_CORE:]
            if min(core) < core_target:
                i = HERO_CORE - 1 - core[::-1].index(min(core))
            elif rest and min(rest) < min(core) - 3:
                i = HERO_CORE + rest.index(min(rest))
            elif min(squad_levels) < cap:
                i = squad_levels.index(min(squad_levels))
            else:
                break
            if essence < hero_level_cost(squad_levels[i]):
                break
            essence -= hero_level_cost(squad_levels[i])
            squad_levels[i] += 1
        rec["power"] = sum(hero_power(h, lv) for h in squad_levels)
        rec["squad"] = len(squad_levels)
        rec["hero_level"] = sum(squad_levels) / len(squad_levels)
        rec["top5"] = sorted(squad_levels, reverse=True)[HERO_CORE - 1]

        # campaign: the recommended power of mission N is the squad power on its target day,
        # so the player clears it on that day (at most 8 new battles a day)
        per_day = MISSIONS_PER_DAY_MAX
        while missions_done < MISSIONS and mission_day(missions_done + 1) <= day and per_day > 0:
            missions_done += 1
            per_day -= 1
            essence += essence_income_per_day(lv) * MISSION_ESSENCE_DAYS  # first clear
            essence_got += essence_income_per_day(lv) * MISSION_ESSENCE_DAYS
            if (missions_done - 1) % (MISSIONS_PER_ACT + 1) == 0:
                act_open_level[(missions_done - 1) // (MISSIONS_PER_ACT + 1) + 1] = level_on[int(mission_day(missions_done))]
            diamonds += DIAMONDS_PER_3_STAR_CHEST
            earned_by["Кампания"] += DIAMONDS_PER_3_STAR_CHEST
            act = (missions_done - 1) // (MISSIONS_PER_ACT + 1) + 1
            act_power.setdefault(act, [rec["power"], rec["power"]])[1] = rec["power"]
            if missions_done % (MISSIONS_PER_ACT + 1) == 0:
                acts_done += 1
                act_finish_day[acts_done] = day
                diamonds += DIAMONDS_PER_ACT_BOSS
                earned_by["Кампания"] += DIAMONDS_PER_ACT_BOSS
        rec["missions"] = missions_done
        essence_by_day[day] = essence_got

        # diamonds earned
        daily = {"Поручения дня": DIAMONDS_DAILY_TASKS, "Календарь входа": DIAMONDS_LOGIN_CALENDAR / 28.0}
        ach = min(ACHIEVEMENT_POOL * (0.6 / 30 if day <= 30 else 0.4 / 335), achievements_left)
        achievements_left -= ach
        daily["Достижения"] = ach
        daily["Арена"] = 60 if lv >= ARENA_UNLOCK and day % 7 == 0 else 0  # weekly league reward
        daily["События"] = 0
        hourglass = HOURGLASS_DAILY_MINUTES + (HOURGLASS_SURGE_MINUTES if lv >= 2 else 0)
        event_spend = 0
        rec["events"] = []
        for name, spec in EVENTS.items():
            if lv < EVENT_UNLOCK_HEARTH[name]:
                continue
            if event_running(name, day):
                rec["events"].append(name)
            if event_starts(name, day):
                daily["События"] += spec[3]
                event_spend += spec[4]
                hourglass += spec[7]
        for k, v in daily.items():
            earned_by[k] += v
        rec["diamonds_earned"] = sum(daily.values())
        diamonds += rec["diamonds_earned"]

        # diamonds spent: 4th builder first, then habits, event extras, then speed-ups or resource packs
        spent = 0.0
        if fourth_builder_day is None:
            if diamonds >= FOURTH_BUILDER_PRICE and lv >= 8:
                diamonds -= FOURTH_BUILDER_PRICE
                spent_by["Четвёртый строитель"] += FOURTH_BUILDER_PRICE
                spent += FOURTH_BUILDER_PRICE
                fourth_builder_day = day
        else:
            habit = SHOP_REFRESH + (ARENA_EXTRA_TICKETS if lv >= ARENA_UNLOCK and day % 7 in (6, 0) else 0)
            if diamonds >= habit:
                diamonds -= habit
                spent_by["Билет арены и обновление лавки"] += habit
                spent += habit
            if event_spend and diamonds >= event_spend:
                diamonds -= event_spend
                spent_by["Доп. попытки в событиях"] += event_spend
                spent += event_spend
            surplus = diamonds - RESERVE
            if day % 7 == 1:
                packs_this_week = 0
            if surplus > 0 and gated.get(lv, False):
                minutes = max(row["room_h"] * 60, 60)
                rate = speedup_cost(minutes) / minutes
                bought = surplus / rate
                diamond_minutes_total += bought
                hourglass += bought
                diamonds -= surplus
                spent_by["Ускорения"] += surplus
                spent += surplus
            elif surplus >= PACK_PRICE:
                n = min(int(surplus // PACK_PRICE), PACKS_PER_WEEK - packs_this_week, 2)
                if n > 0:
                    packs_this_week += n
                    packs[day] = n
                    diamonds -= n * PACK_PRICE
                    spent_by["Наборы ресурсов"] += n * PACK_PRICE
                    spent += n * PACK_PRICE
        speed[day] = hourglass
        hourglass_total += hourglass
        rec["diamonds_spent"] = spent
        rec["diamonds_bank"] = diamonds
        days.append(rec)

    habits = {
        "extra_builder_day": fourth_builder_day,
        "income_mult": lambda d, p=dict(packs): 1.0 + p.get(d, 0) / (BUILD_SHARE * EFFECTIVE_HOURS_PER_DAY),
        "speedup_minutes": lambda d, s=dict(speed): s.get(d, 0.0),
    }
    builder_minutes_total = sum((row["hearth_h"] + row["room_h"] * row["rooms"]) * 60
                                for row in rows if row["level"] + 1 in reached and reached[row["level"] + 1] <= DAYS)
    return {
        "rows": rows, "reached": reached, "reached_run": reached, "days": days, "fourth_builder_day": fourth_builder_day,
        "act_finish_day": act_finish_day, "act_power": act_power, "act_open_level": act_open_level,
        "essence_by_day": essence_by_day, "habits": habits,
        "speed_minutes_total": hourglass_total, "diamond_minutes_total": diamond_minutes_total,
        "builder_minutes_total": builder_minutes_total, "diamonds_left": diamonds,
        "earned_by": earned_by, "spent_by": spent_by, "packs_total": sum(packs.values()), "gated": gated,
    }


def solve(iterations=4):
    """Calibrates the level table for a player who really uses hourglasses, diamonds and the 4th
    builder. Habits depend on the timeline and the timeline on habits, so a few rounds are run."""
    habits = {}
    result = None
    for _ in range(iterations):
        rows = pacing.calibrate(pacing.design(), **habits)
        reached = pacing.simulate(rows, **habits)
        result = run(rows, reached)
        habits = result["habits"]
    # final replay with the habits of the final timeline
    result["reached"] = pacing.simulate(result["rows"], **habits)
    result["reached_plain"] = pacing.simulate(result["rows"])  # the same player ignoring all speed-ups
    result["activities"] = activities(result)
    return result


# ---------------------------------------------------------------- checks

def event_material_share():
    """Share of material income paid out by events once they are all open (per 28 days)."""
    hours = 0.0
    for name, spec in EVENTS.items():
        period = spec[0]
        hours += spec[6] * 28.0 / period
    return hours / (28 * EFFECTIVE_HOURS_PER_DAY)


def checks(result):
    problems = []
    rows = result["rows"]
    # 1. storage holds the hearth job and saving for it takes at most a week
    for row in rows:
        lv = row["level"]
        if storage(row) < hearth_job(row):
            problems.append("очаг %d: склад меньше цены очага" % lv)
        save_days = hearth_job(row) / (total_income(lv) * EFFECTIVE_HOURS_PER_DAY)
        if save_days > 7:
            problems.append("очаг %d: копить на очаг %.1f дня (больше недели)" % (lv, save_days))
    # 2. sources and sinks add up
    if abs(sum(MATERIAL_SINKS.values()) - 1.0) > 1e-9 or abs(sum(MATERIAL_SOURCES.values()) - 1.0) > 1e-9:
        problems.append("доли источников или трат не дают 100%")
    if abs(MATERIAL_SINKS["Стройка (очаг и залы)"] - BUILD_SHARE) > 1e-9:
        problems.append("доля стройки не совпадает с BUILD_SHARE")
    # 3. coal mines cover the evening Surge
    for lv in range(1, 31):
        if coal_production_per_hour(lv) * 24 < coal_burn_per_hour(lv) * 24.5:
            problems.append("очаг %d: угля не хватает на Наплыв" % lv)
    # 4. diamonds: the 4th builder within two months, nothing piles up
    if result["fourth_builder_day"] is None or result["fourth_builder_day"] > 60:
        problems.append("четвёртый строитель не куплен за 2 месяца")
    if result["diamonds_left"] > 3000:
        problems.append("к концу года копится %.0f алмазов" % result["diamonds_left"])
    # 5. event rewards match the declared share of materials
    if abs(event_material_share() - MATERIAL_SOURCES["События"]) > 0.03:
        problems.append("события дают %.0f%% материалов, а заявлено %.0f%%" % (
            event_material_share() * 100, MATERIAL_SOURCES["События"] * 100))
    # 6. campaign within the year, without long pauses
    if ACTS not in result["act_finish_day"]:
        problems.append("кампания не пройдена за год")
    gap = max(mission_day(n + 1) - mission_day(n) for n in range(8, MISSIONS))
    if gap > MAX_MISSION_GAP_DAYS:
        problems.append("в кампании пауза %.1f дня между боями" % gap)
    # 7. speed-ups help but do not replace waiting
    share = result["speed_minutes_total"] / max(result["builder_minutes_total"], 1)
    if not 0.10 <= share <= 0.25:
        problems.append("ускорения покрывают %.0f%% стройки (нужно 10-25%%)" % (share * 100))
    # 8. anti-rush: the previous warehouse is too small, the free player meets the hero rule in time,
    #    and the rule is tight enough to matter
    rows = result["rows"]
    for row in rows[STORAGE_GATE_FROM - 1:]:
        lv = row["level"]
        if not storage(rows[lv - 2]) < hearth_job(row) <= storage(row):
            problems.append("очаг %d: склад не сдерживает раш (склад прошлого уровня %d, цена %d, склад %d)" % (
                lv, storage(rows[lv - 2]), hearth_job(row), storage(row)))
    slack = []
    for lv, need, have in hero_gate(result):
        slack.append(have - need)
        if have < need:
            problems.append("очаг %d: у обычного игрока герои %d-го уровня, а нужно %d" % (lv, have, need))
    if slack and sorted(slack)[len(slack) // 2] > 6:
        problems.append("требование к героям слишком мягкое: обычный игрок обгоняет его на %d уровней" % sorted(slack)[len(slack) // 2])
    # 9. activities: each resource feeds its own progression in about a year, nothing piles up
    act = result["activities"]
    if act["heart_done"] is None or not 250 <= act["heart_done"] <= DAYS:
        problems.append("Сердце очага собрано на день %s (нужно 250-365)" % act["heart_done"])
    if act["core5_done"] is None or not 120 <= act["core5_done"] <= 300:
        problems.append("умения пятёрки героев прокачаны на день %s (нужно 120-300)" % act["core5_done"])
    if act["spec_wait_avg"] > 15 or act["spec_wait_max"] > 45:
        problems.append("специализации ждут чертежей в среднем %.0f, максимум %d дней (нужно 15 и 45)" % (
            act["spec_wait_avg"], act["spec_wait_max"]))
    if sum(m for _, m in PLAYTIME) > 55:
        problems.append("в день нужно больше 55 минут игры")
    # 10. the real player meets the target days
    for lv, want, got in pacing.deviations(result["reached"]):
        if abs(got - want) / want > 0.15:
            problems.append("очаг %d: день %.1f вместо %.1f" % (lv, got, want))
    if 30 not in result["reached"] or result["reached"][30] > DAYS * 1.05:
        problems.append("очаг 30 не достигнут за год")
    # 11. fortress sections: each one is warm enough for a room of the hearth level when it opens,
    #     the starting rooms stand in the first one, the whole mountain is open by mid game
    for lv, f0, f1, c0, c1 in SECTIONS:
        far = max(abs(f0 - HEARTH_FLOOR), abs(f1 - HEARTH_FLOOR))
        warmth = hearth_warmth(lv) * 0.8 ** far
        need = 60 * 1.06 ** (lv - 1)
        if warmth < need:
            problems.append("секция очага %d: этаж %d холодный (тепло %.0f, нужно %.0f)" % (lv, f1 + 1, warmth, need))
    for floor, cell in STARTING_ROOMS:
        if section_opens(floor, cell) != 1:
            problems.append("стартовый зал (этаж %d, клетка %d) не в первой секции" % (floor + 1, cell + 1))
    closed = [(f, c) for f in range(GRID_FLOORS) for c in range(GRID_CELLS) if section_opens(f, c) is None]
    if closed or max(lv for lv, *_ in SECTIONS) > 20:
        problems.append("гора не открывается целиком к 20-му уровню очага")
    return problems


# ---------------------------------------------------------------- report

def num(x):
    return "{:,}".format(int(round(x))).replace(",", " ")


def dec(x, digits=1):
    return ("%.*f" % (digits, x)).replace(".", ",")


PLAYTIME = [  # minutes a day of a typical free player, 4 sessions of 12-13 minutes
    ("Сбор производства, стройка, расселение жителей", 7),
    ("Кампания: новые бои, сложный режим, повторы", 6),
    ("Охота на чудовищ: 3 боя с боссами по 2 минуты", 6),
    ("Наплыв: живая оборона крепости вечером", 2),
    ("Вылазки-маршруты: проложить путь, решить развилки", 3),
    ("Арена: 5 боёв (с очага 16)", 4),
    ("Таверна, кузня, лазарет, библиотека, часовня, Сердце очага", 5),
    ("События", 10),
    ("Поручения дня и сюжетные диалоги", 4),
]


# ---------------------------------------------------------------- activities (docs/ACTIVITIES.md)
# Three action activities, each with its own resource for its own progression.
SURGE_UNLOCK = 2                 # Наплыв: live defense every evening -> Жар
SURGE_ESSENCE_SHARE = 0.3        # the Surge pays 30% of the daily essence, repeats 40%, routes 15%, arena 15%
ROUTE_ESSENCE_SHARE = 0.05       # per route
ROUTE_MATERIAL_HOURS = 1.5       # per route: three routes a day are the 10% "expeditions" share
SURGE_LIVE_DAYS = 5 / 7.0        # the player defends live 5 evenings of 7, the rest is automatic
SURGE_AUTO_SHARE = 0.7           # automatic defense gives 70%
SIEGE_EMBER = 150                # Осада застав adds Жар per event


def ember_per_surge(level):
    return 20 + 3 * level


HEART_NODES = 30                 # Сердце очага: 30 talents in 3 branches of 10


def heart_node_cost(n):
    return int(round(85 * 1.14 ** (n - 1)))


def heart_node_gate(n):
    """Hearth level that opens the node: the first ten at once, the next at 12 and 20."""
    return SURGE_UNLOCK if n <= 10 else 12 if n <= 20 else 20


HUNT_UNLOCK = 6                  # Охота на чудовищ: tactical boss fights -> Сердцевины
HUNTS_PER_DAY = 3                # Следы: 3 a day (1 every 8 hours, up to 3)
LONG_NIGHT_CORES = 20            # Долгая ночь adds Сердцевины per event


def cores_per_hunt(level):
    return 2 + level // 2


SKILL_MAX = 10                   # special skill of a hero, levels 1-10, +8% effect each


def skill_cost(level):
    """Cores to raise a skill from this level to the next."""
    return int(round(15 * 1.4 ** (level - 1)))


def skill_cap(hero_level):
    """A skill cannot outgrow the hero: level 10 needs hero level 54."""
    return min(SKILL_MAX, 1 + hero_level // 6)


LEGENDARY_FROM = 20              # legendary gear from hearth 20: 6 slots for the five core heroes
LEGENDARY_COST = 400

ROUTES_UNLOCK = 9                # Вылазки-маршруты -> Чертежи


def routes_per_day(level):
    """Squads on routes at once: the watchtower trains a new one at hearth 12 and 18."""
    return 0 if level < ROUTES_UNLOCK else 1 if level < 12 else 2 if level < 18 else 3


def blueprints_per_route(level):
    return 3 + (1 if level >= 20 else 0)


TREASURE_BLUEPRINTS = 10         # Кладоискатели add Чертежи per event

# Event currencies and shops (docs/EVENTS.md, 1): every threshold brings currency, the place in the
# rating adds some; it buys in the event's shop until 24 hours after the end, then the rest turns
# into silver 1:5. The shops sell the activity resource the model counts per event (Сердцевины of the
# Long Night, Жар of the Siege, Чертежи of the Treasure hunters) or a rare dweller, and a decoration:
# no extra materials, essence or hourglasses, so the budget of the table stays the whole reward.
EVENT_CURRENCY = {"Долгая ночь": "Осколки ночи", "Портал в Туман": "Пепел портала", "Турнир Стражей": "Знаки Стражей",
                  "Кладоискатели": "Монеты казны", "Ярмарка Пепла": "Пепельные жетоны", "Осада застав": "Камень застав",
                  "Праздник очага": "Огоньки"}
EVENT_CURRENCY_PER_THRESHOLD = 10
EVENT_CURRENCY_RANK = [30, 20, 10, 5]      # 1st place, 2-5, 6-20, the rest
EVENT_CURRENCY_SILVER = 5                  # silver for one unit left 24 hours after the end
EVENT_GROUP = {"Турнир Стражей": 30}       # rating group (ghosts until the server); the others 50
# event: [(goods, price, limit per event, amount)]; "Decor:<id>" is a decoration of the catalog,
# "Recruit" a rare dweller (stats 6 + 2 x hearth level) if the fortress has room
EVENT_SHOPS = {
    "Долгая ночь":    [("Cores", 50, 1, LONG_NIGHT_CORES), ("Decor:shrine", 60, 1, 1)],
    "Портал в Туман": [("Recruit", 50, 1, 1), ("Decor:crystal", 60, 1, 1)],
    "Турнир Стражей": [("Recruit", 50, 1, 1), ("Decor:armor", 60, 1, 1)],
    "Кладоискатели":  [("Blueprints", 50, 1, TREASURE_BLUEPRINTS), ("Decor:treasure", 60, 1, 1)],
    "Ярмарка Пепла":  [("Recruit", 50, 1, 1), ("Decor:keg", 60, 1, 1)],
    "Осада застав":   [("Ember", 50, 1, SIEGE_EMBER), ("Decor:shield", 60, 1, 1)],
    "Праздник очага": [("Recruit", 50, 1, 1), ("Decor:pumpkin", 60, 1, 1)],
}
SPEC_TIERS = [(5, 3), (10, 6), (15, 14), (20, 30), (25, 50)]  # room level -> blueprints, choice of 2
SPEC_RANKS_FROM = 25
SPEC_RANKS = 3                   # rank 2 costs x2 and rank 3 x3 of the tier: +50% and +100% of the bonus


def spec_room_types(level):
    """Room types with specializations: every type but the hearth and the bedrooms."""
    return [name for name, enum, unlock, *_ in ROOM_TYPES if unlock <= level and enum not in ("Hearth", "Bedrooms")]


def activities(result):
    """Day by day: Жар into Сердце очага, Сердцевины into skills and legendary gear, Чертежи into
    room specializations. Hero levels come from the main run (the squad of the free player)."""
    days = result["days"]
    ember = cores = blueprints = 0.0
    heart = 0
    heart_done = None
    skills = [1] * 9
    core5_done = all_done = None
    legendary = 0
    specs_bought = set()
    spec_waits = []          # days a specialization waited for blueprints after it opened
    spec_open = {}
    ranks = {}               # specialization -> rank 1..3
    totals = {"ember": 0.0, "cores": 0.0, "blueprints": 0.0}
    for rec in days:
        day, lv = rec["day"], rec["level"]
        # income
        if lv >= SURGE_UNLOCK:
            got = ember_per_surge(lv) * (SURGE_LIVE_DAYS + (1 - SURGE_LIVE_DAYS) * SURGE_AUTO_SHARE)
            if lv >= EVENT_UNLOCK_HEARTH["Осада застав"] and event_starts("Осада застав", day):
                got += SIEGE_EMBER
            ember += got
            totals["ember"] += got
        if lv >= HUNT_UNLOCK:
            got = HUNTS_PER_DAY * cores_per_hunt(lv)
            if lv >= EVENT_UNLOCK_HEARTH["Долгая ночь"] and event_starts("Долгая ночь", day):
                got += LONG_NIGHT_CORES
            cores += got
            totals["cores"] += got
        got = routes_per_day(lv) * blueprints_per_route(lv)
        if lv >= EVENT_UNLOCK_HEARTH["Кладоискатели"] and event_starts("Кладоискатели", day):
            got += TREASURE_BLUEPRINTS
        blueprints += got
        totals["blueprints"] += got
        # Сердце очага: next node when the hearth allows it
        while heart < HEART_NODES and heart_node_gate(heart + 1) <= lv and ember >= heart_node_cost(heart + 1):
            ember -= heart_node_cost(heart + 1)
            heart += 1
            if heart == HEART_NODES:
                heart_done = day
        # skills: the five core heroes first, then the rest of the squad
        hero = int(rec["hero_level"])
        squad = rec["squad"]
        while True:
            open_ = [i for i in range(squad) if skills[i] < skill_cap(hero)]
            if not open_:
                break
            core = [i for i in open_ if i < 5]
            i = min(core or open_, key=lambda k: skills[k])
            if cores < skill_cost(skills[i]):
                break
            cores -= skill_cost(skills[i])
            skills[i] += 1
        if core5_done is None and all(skills[i] >= SKILL_MAX for i in range(5)):
            core5_done = day
        if all_done is None and squad == 9 and all(x >= SKILL_MAX for x in skills):
            all_done = day
        if lv >= LEGENDARY_FROM:
            while legendary < 30 and cores >= LEGENDARY_COST and all(skills[i] >= skill_cap(hero) for i in range(5)):
                cores -= LEGENDARY_COST
                legendary += 1
        # specializations open with room levels (rooms follow the hearth)
        for name in spec_room_types(lv):
            for need, cost in SPEC_TIERS:
                key = (name, need)
                if lv >= need and key not in spec_open:
                    spec_open[key] = day
        waiting = False
        for key in sorted(spec_open, key=lambda k: (spec_open[k], k[1])):
            if key in specs_bought:
                continue
            cost = dict(SPEC_TIERS)[key[1]]
            if blueprints < cost:
                waiting = True
                break
            blueprints -= cost
            specs_bought.add(key)
            ranks[key] = 1
            spec_waits.append(day - spec_open[key])
        # surplus goes into ranks of the cheapest bought specializations (mastery opens at hearth 25)
        while not waiting and lv >= SPEC_RANKS_FROM:
            options = [k for k in ranks if ranks[k] < SPEC_RANKS]
            if not options:
                break
            key = min(options, key=lambda k: dict(SPEC_TIERS)[k[1]] * (ranks[k] + 1))
            cost = dict(SPEC_TIERS)[key[1]] * (ranks[key] + 1)
            if blueprints < cost:
                break
            blueprints -= cost
            ranks[key] += 1
        rec["heart"] = heart
        rec["skills5"] = min(skills[:5])
        rec["specs"] = len(specs_bought)
    total_specs = len(spec_room_types(30)) * len(SPEC_TIERS)
    return {"heart_done": heart_done, "heart": heart, "core5_done": core5_done, "all_done": all_done,
            "legendary": legendary, "skills": skills, "specs": len(specs_bought), "total_specs": total_specs,
            "spec_wait_max": max(spec_waits) if spec_waits else 0,
            "spec_wait_avg": sum(spec_waits) / len(spec_waits) if spec_waits else 0, "totals": totals,
            "ranks_bought": sum(r - 1 for r in ranks.values()), "ranks_total": total_specs * (SPEC_RANKS - 1),
            "blueprints_left": blueprints, "ember_left": ember, "cores_left": cores,
            "heart_cost": sum(heart_node_cost(n) for n in range(1, HEART_NODES + 1))}


def report(result):
    days = result["days"]
    rows = result["rows"]
    reached = result["reached"]
    by_day = {d["day"]: d for d in days}
    marks = [1, 7, 14, 30, 60, 90, 180, 270, 365]
    out = []
    w = out.append
    w("# Экономика «Последнего очага»")
    w("")
    w("Файл собран скриптом `hearth/tools/economy.py`: год игры бесплатного игрока, минута за минутой. "
      "Игрок заходит 4 раза в день по 12 минут, пользуется песочными часами, тратит алмазы с умом и покупает "
      "четвёртого строителя. Таблица уровней очага калибруется под такого игрока (`docs/pacing_table.md`). "
      "Правки — в скрипте, затем `python hearth/tools/economy.py`.")
    w("")
    w("## 1. Итог")
    w("")
    played_h = sum(m for _, m in PLAYTIME) * DAYS / 60.0
    w("| Что | Когда |")
    w("|---|---|")
    w("| Очаг 30, вся крепость | день %s |" % dec(reached.get(30, float("nan"))))
    w("| Кампания пройдена (10 актов, 190 боёв) | день %s |" % result["act_finish_day"].get(ACTS, "после года"))
    w("| Четвёртый строитель куплен за алмазы | день %s |" % result["fourth_builder_day"])
    w("| Время в игре за год | около %s часов (%d минут в день) |" % (num(played_h), sum(m for _, m in PLAYTIME)))
    w("")
    w("## 2. Игрок по дням")
    w("")
    w("| День | Очаг | Жителей (лимит) | Отряд | Ур. героев | Сила отряда | Материалов в день | Пройдено боёв | Алмазов на руках |")
    w("|---|---|---|---|---|---|---|---|---|")
    for m in marks:
        d = by_day[m]
        w("| %d | %d | %d | %d | %s | %s | %s | %d из %d | %s |" % (
            m, d["level"], dweller_cap(d["level"]), d["squad"], dec(d["hero_level"], 0), num(d["power"]),
            num(d["materials"]), d["missions"], ACTS * (MISSIONS_PER_ACT + 1), num(d["diamonds_bank"])))
    w("")
    w("## 3. Темп: цель и симуляция")
    w("")
    plain = result["reached_plain"]
    w("| Очаг | Цель, день | Игрок с ускорениями | Тот же игрок без ускорений и 4-го строителя |")
    w("|---|---|---|---|")
    for lv, want, got in pacing.deviations(reached):
        w("| %d | %s | %s (%+.0f%%) | %s |" % (lv, dec(want), dec(got), (got - want) / want * 100,
                                              dec(plain[lv]) if lv in plain else "после года"))
    w("")
    w("**Первый день** (обучение и крючок, таймеры первых уровней заданы вручную по образцу Whiteout Survival): " +
      ", ".join("очаг %d — %s" % (lv, pacing.fmt_h(reached[lv] * 24)) for lv in (3, 5, 7, 8, 9, 10) if lv in reached) + ".")
    w("")
    w("Ускорения и четвёртый строитель важны с %d-го по %d-й уровень очага, когда игрока держат строители. Позже держат материалы, "
      % (pacing.BUILDER_GATED[0], pacing.BUILDER_GATED[-1]) +
      "и алмазы выгоднее тратить на наборы ресурсов. Полная таблица таймеров и цен — `docs/pacing_table.md`.")
    w("")
    w("## 4. Материалы (серебро и железо)")
    w("")
    w("Откуда (доля всего дохода):")
    w("")
    w("| Источник | Доля | В день на очаге 10 | В день на очаге 20 |")
    w("|---|---|---|---|")
    for name, share in MATERIAL_SOURCES.items():
        w("| %s | %d%% | %s | %s |" % (name, share * 100, num(total_income(10) * EFFECTIVE_HOURS_PER_DAY * share),
                                       num(total_income(20) * EFFECTIVE_HOURS_PER_DAY * share)))
    w("")
    w("События по своим наградам дают %d%% материалов (заявлено %d%%)." % (
        round(event_material_share() * 100), MATERIAL_SOURCES["События"] * 100))
    w("Валюта события: %d за порог и %s за место (1-е, 2–5, 6–20, остальные); лавка открыта ещё 24 часа после "
      "конца, остаток меняется на серебро 1 к %d. Лавки продают только ресурс занятия, который модель уже считает "
      "(сердцевины Долгой ночи, жар Осады, чертежи Кладоискателей), редкого жителя и украшение." % (
          EVENT_CURRENCY_PER_THRESHOLD, " / ".join(str(x) for x in EVENT_CURRENCY_RANK), EVENT_CURRENCY_SILVER))
    w("")
    w("Куда:")
    w("")
    w("| Трата | Доля |")
    w("|---|---|")
    for name, share in MATERIAL_SINKS.items():
        w("| %s | %d%% |" % (name, share * 100))
    w("")
    w("Серебро — %d%% материалов, железо — %d%%. **Склад**, улучшенный до предела уровня очага, вмещает цену очага "
      "плюс 5%%, а склад уровнем ниже — меньше этой цены (защита от раша, раздел 11). Выработка, которая не влезла в склад, "
      "ждёт в залах до 10 часов и не пропадает:" % (SILVER_SHARE * 100, (1 - SILVER_SHARE) * 100))
    w("")
    w("| Очаг | Доход в час | Цена очага | Склад (серебро + железо) | Склад в часах дохода | Копить на очаг |")
    w("|---|---|---|---|---|---|")
    for row in rows:
        lv = row["level"]
        if lv in (1, 5, 10, 15, 18, 20, 22, 24, 26, 28, 29):
            inc = total_income(lv)
            w("| %d | %s | %s | %s | %d ч | %s дн |" % (lv, num(inc), num(hearth_job(row)), num(storage(row)),
                                                    storage(row) / inc, dec(hearth_job(row) / (inc * EFFECTIVE_HOURS_PER_DAY))))
    w("")
    w("## 5. Уголь и еда")
    w("")
    w("| Очаг | Тепло | Сжигает угля в час | Шахты дают в час | Жителей | Еды в час (кухни +10%) |")
    w("|---|---|---|---|---|---|")
    for lv in (1, 5, 10, 15, 20, 25, 30):
        w("| %d | %d | %s | %s | %d | %d |" % (lv, hearth_warmth(lv), num(coal_burn_per_hour(lv)), num(coal_production_per_hour(lv)),
                                            dweller_cap(lv), food_per_hour(lv) * 1.1))
    w("")
    w("Шахты дают на 15% больше обычного расхода: этого хватает на вечерний Наплыв (час при ×1,5). Режим «Пылающий» "
      "сжигает вдвое больше, его включают ненадолго: перед боем, на ночь лечения, в событие.")
    w("")
    w("## 6. Эссенция и герои")
    w("")
    w("Эссенция в день: `160 × 1,13^(очаг − 1)` из Наплыва, арены, вылазок и повторов боёв. Сверху: первая победа в "
      "бою кампании даёт %s дневной нормы, события — по своей таблице, стартовые задания — %s эссенции за первые %d дня. "
      "Уровень героя стоит `40 × 1,11^уровень`, предел уровня героя `2 × очаг + 5`, для стройки очага нужны пять героев "
      "не ниже `2 × очаг − 2` (раздел 11). Сила героя `100 × 1,07^уровень × 1,05^очаг` (вторая часть — снаряжение, "
      "которое растёт с кузней)." % (dec(MISSION_ESSENCE_DAYS), num(STARTER_ESSENCE), STARTER_DAYS))
    w("")
    w("Как тратит обычный игрок: сначала пятёрку основных героев до требования очага плюс %d, затем остальных бойцов "
      "отряда до отставания в 3 уровня, затем всех поровну до предела." % HERO_SLACK)
    w("")
    w("## 7. Кампания")
    w("")
    w("10 актов по 18 боёв и боссу. Ворота кампании — сила отряда: новый бой открывается, когда отряд до него дорос. "
      "Темп задан так, что в первые дни боёв много, а дальше новый бой появляется каждые 2–3 дня, без многонедельных пауз "
      "(самая длинная пауза — %s дня). **Рекомендуемая сила боя** — сила отряда бесплатного игрока в день, когда он до "
      "него доходит, ×0,95:" % dec(max(mission_day(n + 1) - mission_day(n) for n in range(8, MISSIONS))))
    w("")
    w("| Акт | Начало (очаг у игрока) | Сила первого боя | Сила босса | Пройден (день) |")
    w("|---|---|---|---|---|")
    for act in range(1, ACTS + 1):
        p = result["act_power"].get(act, [0, 0])
        w("| %d | %d | %s | %s | %s |" % (act, result["act_open_level"].get(act, 0), num(nice(p[0] * 0.95)), num(nice(p[1] * 0.95)),
                                          result["act_finish_day"].get(act, "после года")))
    w("")
    w("Бой длится 60–90 секунд. Звёзды: победа, никто не пал, быстрее 60 секунд. Повтор пройденного боя — "
      "автобой без таймера, награда ×0,5 (защита от фарма, 10 повторов в день). "
      "**Сложный режим** открывается после акта: 6 боёв ×1,6 силы, 3 попытки в день, награда — эссенция и снаряжение "
      "редкости на ступень выше; это задел на второй год.")
    w("")
    w("## 8. Алмазы (Искры)")
    w("")
    earned = result["earned_by"]
    total_earned = sum(earned.values())
    w("За год бесплатный игрок получает **%s алмазов** (в среднем %d в день)." % (num(total_earned), total_earned / DAYS))
    w("")
    w("| Источник | За год |")
    w("|---|---|")
    for k, v in earned.items():
        w("| %s | %s |" % (k, num(v)))
    w("")
    w("| Трата | За год |")
    w("|---|---|")
    for k, v in result["spent_by"].items():
        w("| %s | %s |" % (k, num(v)))
    w("")
    w("Порядок трат: сначала копит на **четвёртого строителя** (%s алмазов, куплен на день %s), затем обновление лавки "
      "каждый день и билет арены по выходным, доп. попытки в событиях, запас %d на всякий случай; остальное — ускорения, пока держат "
      "строители, и наборы ресурсов (не больше %d в неделю), когда держат материалы. За год куплено %d наборов. "
      "К концу года на руках %s." % (
          num(FOURTH_BUILDER_PRICE), result["fourth_builder_day"], RESERVE, PACKS_PER_WEEK,
          result["packs_total"], num(result["diamonds_left"])))
    w("")
    w("## 9. Ускорения")
    w("")
    w("| Осталось ждать | Цена в алмазах |")
    w("|---|---|")
    for minutes, label in ((1, "1 мин"), (5, "5 мин"), (15, "15 мин"), (60, "1 ч"), (180, "3 ч"), (480, "8 ч"),
                           (1440, "1 д"), (4320, "3 д"), (10080, "7 д")):
        w("| %s | %s |" % (label, "бесплатно" if minutes <= FREE_FINISH_MINUTES else speedup_cost(minutes)))
    w("")
    share = result["speed_minutes_total"] / max(result["builder_minutes_total"], 1)
    w("Бесплатные **песочные часы**: %d минут в день за поручения, %d за победу в Наплыве, ещё больше в событиях. "
      "Вместе с ускорениями за алмазы за год это %s часов, или **%d%% всего времени стройки**: ощутимая помощь, "
      "но ожидание остаётся частью игры. Из них за алмазы — %s часов." % (
          HOURGLASS_DAILY_MINUTES, HOURGLASS_SURGE_MINUTES, num(result["speed_minutes_total"] / 60), share * 100,
          num(result["diamond_minutes_total"] / 60)))
    w("")
    w("Правила: последние 5 минут любой стройки завершаются бесплатно; цена за минуту падает с длиной таймера; час дохода в наборе ресурсов "
      "стоит %d алмазов, дороже часа ускорения (20), чтобы алмазы не заменяли игру." % PACK_PRICE)
    w("")
    w("## 10. Время в игре")
    w("")
    w("| Занятие | Минут в день |")
    w("|---|---|")
    for name, m in PLAYTIME:
        w("| %s | %d |" % (name, m))
    w("| **Всего** | **%d** |" % sum(m for _, m in PLAYTIME))
    w("")
    w("Первый день длиннее: около 3,5 часов, если игроку нравится (обучение, 9 уровней очага, первый акт кампании).")
    w("")
    w("## 11. Защита от раша")
    w("")
    w("Раш — это когда игрок гонит уровень очага, не развивая крепость и героев: потом он не тянет ни кампанию, ни "
      "события, и ему скучно. Как в Whiteout Survival, стройку очага с уровня L на L+1 нельзя начать, пока не "
      "выполнены три правила:")
    w("")
    w("1. **Склад.** Цена очага влезает только в склад, улучшенный до уровня L. Склад уровня L−1 меньше цены (с очага %d)." % STORAGE_GATE_FROM)
    w("2. **Ключевой зал уровня L** — один, по кругу (у Whiteout так же: печь требует одно-два здания своего уровня). "
      "Остальные залы игрок растит по желанию, ради выработки и силы:")
    w("")
    w("| Очаг L | Ключевой зал |")
    w("|---|---|")
    for lo, hi in ((1, 9), (10, 14), (15, 19), (20, 24), (25, 29)):
        w("| " + " · ".join("%d" % lv for lv in range(lo, hi + 1)) + " | " + " · ".join(key_room(lv) for lv in range(lo, hi + 1)) + " |")
    w("")
    w("3. **Герои.** Пять героев не ниже уровня `2 × L − 2` (с очага %d; предел уровня героя `2 × L + 5`)." % HERO_GATE_FROM)
    w("")
    w("Обычный игрок выполняет все три правила сам и не замечает их: он и так поднимает залы, а героев растит "
      "с запасом. Проверка модели:")
    w("")
    w("| Очаг | Склад уровня L−1 | Цена очага | Склад уровня L | Нужен уровень героев | Пятёрка героев у игрока |")
    w("|---|---|---|---|---|---|")
    gate = {lv: (need, have) for lv, need, have in hero_gate(result)}
    for row in rows:
        lv = row["level"]
        if lv in (5, 7, 8, 10, 12, 15, 18, 20, 22, 24, 26, 28) and lv in gate:
            need, have = gate[lv]
            w("| %d | %s | %s | %s | %d | %d |" % (lv, num(storage(rows[lv - 2])), num(hearth_job(row)), num(storage(row)), need, have))
    w("")
    w("Эссенция для героев не продаётся за алмазы и приходит из кампании (первые победы), Наплыва, арены, вылазок "
      "и событий. Поэтому даже игрок с бесконечными алмазами упирается в героев:")
    w("")
    w("| День | Очаг обычного игрока | Максимум для раша с любыми алмазами |")
    w("|---|---|---|")
    by_day = {d["day"]: d for d in result["days"]}
    for m in (3, 7, 14, 30, 60, 90, 180, 365):
        w("| %d | %d | %d |" % (m, by_day[m]["level"], rusher_max_level(result["essence_by_day"][m])))
    w("")
    w("**Прочие меры:** уровень залов не выше уровня очага; уровень героя не выше `2 × L + 5`; на арене подбор по силе "
      "отряда, в Набегах — по уровню очага ±1 и силе ±15%%, в событиях — группы по уровню очага, так что слабый отряд с "
      "высоким очагом попадает к сильным соперникам; наборы ресурсов за алмазы — не больше %d в неделю." % PACKS_PER_WEEK)
    w("")
    w("## 12. Активности и их ресурсы")
    w("")
    act = result["activities"]
    w("Три активные части игры, у каждой свой ресурс для своего развития (`docs/ACTIVITIES.md`):")
    w("")
    w("| Активность | Открывается | Ресурс | В день на очаге 10 / 20 | На что | Итог года |")
    w("|---|---|---|---|---|---|")
    surge = SURGE_LIVE_DAYS + (1 - SURGE_LIVE_DAYS) * SURGE_AUTO_SHARE
    w("| Наплыв — живая оборона вечером | очаг %d | Жар | %d / %d | Сердце очага: %d талантов, всего %s Жара | собрано на день %s |" % (
        SURGE_UNLOCK, ember_per_surge(10) * surge, ember_per_surge(20) * surge, HEART_NODES, num(act["heart_cost"]), act["heart_done"]))
    w("| Охота на чудовищ — бои с боссами | очаг %d | Сердцевины | %d / %d | умения героев 1–10, легендарное снаряжение | умения пятёрки — день %s, всех — день %s; легендарных вещей %d из 30 |" % (
        HUNT_UNLOCK, HUNTS_PER_DAY * cores_per_hunt(10), HUNTS_PER_DAY * cores_per_hunt(20), act["core5_done"], act["all_done"], act["legendary"]))
    w("| Вылазки-маршруты с развилками | очаг %d | Чертежи | %d / %d | специализации залов (5 ступеней × %d залов), ранги мастерства | все %d специализаций; ждут в среднем %d дней; рангов %d из %d |" % (
        ROUTES_UNLOCK, routes_per_day(10) * blueprints_per_route(10), routes_per_day(20) * blueprints_per_route(20),
        len(spec_room_types(30)), act["total_specs"], round(act["spec_wait_avg"]), act["ranks_bought"], act["ranks_total"]))
    w("")
    w("Жар, Сердцевины и Чертежи не продаются за алмазы: их приносит только игра в своей активности. Легендарное "
      "снаряжение и ранги мастерства не заканчиваются за год — это цели второго года.")
    w("")
    w("## 13. Крепость: секции, завалы, слияние, жилые покои")
    w("")
    w("Гора открывается секциями с уровнем очага (наш «тронный зал», GDD 6). Каждая неотстроенная клетка открытой "
      "секции завалена: строитель из общей очереди расчищает завал за уголь и еду, и на клетке можно строить. "
      "Секция открывается, когда её дальний этаж уже тёплый для зала уровня очага, так что тепло по-прежнему "
      "решает, где зал работает в полную силу.")
    w("")
    w("| Очаг | Открывается | Клеток для залов всего |")
    w("|---|---|---|")
    for lv, f0, f1, c0, c1 in SECTIONS:
        n = sum(1 for f in range(GRID_FLOORS) for c in range(GRID_CELLS)
                if (section_opens(f, c) or 99) <= lv and not (f == HEARTH_FLOOR and c in HEARTH_CELLS))
        floors = "этаж %d" % (f0 + 1) if f0 == f1 else "этажи %d–%d" % (f0 + 1, f1 + 1)
        w("| %d | %s, клетки %d–%d | %d |" % (lv, floors, c0 + 1, c1 + 1, n))
    w("")
    far = max(cell_distance(f, c) for f in range(GRID_FLOORS) for c in range(GRID_CELLS))
    w("Расчистка рядом с очагом стоит %s часа горения очага в угле и %s часа еды полной крепости, длится %d%% "
      "работы над залом этого уровня (не меньше %d с); каждая клетка дальше от очага добавляет %d%% к цене и "
      "времени (до ×%s на краю горы). Уголь и еда, а не материалы: завалы не мешают копить на очаг." % (
          dec(CLEAR_COAL_HOURS), dec(CLEAR_FOOD_HOURS), CLEAR_TIME_SHARE * 100, CLEAR_MIN_SECONDS,
          CLEAR_PER_DISTANCE * 100, dec(1 + CLEAR_PER_DISTANCE * (far - 1), 2)))
    w("")
    w("| Очаг | Время у очага | Уголь | Еда |")
    w("|---|---|---|---|")
    for lv in (1, 3, 5, 8, 10, 12, 16, 20, 25, 30):
        sec, coal, food = clear_base(rows, lv)
        t = "%d с" % sec if sec < 120 else "%d мин" % round(sec / 60) if sec < 7200 else "%s ч" % dec(sec / 3600)
        w("| %d | %s | %d | %d |" % (lv, t, coal, food))
    w("")
    w("**Слияние.** Залы одного типа и уровня рядом на этаже объединяются в один зал до %d клеток: по %d места "
      "работника на клетку и +%d%% выработки за каждую клетку сверх первой. Лимит залов считается в клетках, "
      "так что слияние не даёт лишних залов. Улучшение большого зала стоит как улучшение каждой его клетки, "
      "но занимает одного строителя и один таймер. Модель экономики бонус слияния не учитывает: это награда "
      "за планировку (до +%d%% выработки зала)." % (MERGE_MAX_WIDTH, WORKERS_PER_ROOM, MERGE_BONUS_PER_CELL * 100,
                                                       MERGE_BONUS_PER_CELL * 100 * (MERGE_MAX_WIDTH - 1)))
    w("")
    w("**Жилые покои.** Мужчина и женщина в жилых покоях ждут ребёнка: у ребёнка характеристики — среднее родителей "
      "±%d, с шансом %d%% одна из них выше на %d. Рождения не превышают лимит жителей (`6 + 2 × (очаг − 1)`), "
      "на каждую клетку покоев — одна пара. Беженцы, таверна и печати найма приводят жителей как прежде; дети "
      "заполняют разрыв до лимита, который модель экономики и так считает занятым (еда — на полный лимит)." % (
          BIRTH_STAT_SPREAD, BIRTH_GIFT_CHANCE * 100, BIRTH_GIFT_BONUS))
    w("")
    w("| Уровень покоев | 1 | 5 | 10 | 15 | 20 | 30 |")
    w("|---|---|---|---|---|---|---|")
    w("| Часов до рождения | " + " | ".join(dec(birth_hours(lv)) for lv in (1, 5, 10, 15, 20, 30)) + " |")
    w("")
    w("## 14. Проверки модели")
    w("")
    problems = checks(result)
    if problems:
        for p in problems:
            w("- **Проблема:** %s" % p)
    else:
        w("Все проверки пройдены: склад уровня очага вмещает его цену, а склад уровнем ниже — нет; обычный игрок выполняет "
          "требование к героям вовремя, а раш в него упирается; копить на очаг не дольше недели; источники и траты "
          "сходятся; угля хватает на Наплыв; четвёртый строитель доступен за 2 месяца, алмазы не копятся; события дают "
          "заявленную долю материалов; кампания проходится за год; ускорения покрывают 10–25% стройки; "
          "Жар, Сердцевины и Чертежи расходуются по своим деревьям за год; "
          "игрок попадает в целевые дни с точностью 15%; секции горы открываются тёплыми, вся гора открыта к 20-му уровню очага.")
    out += kennel_report(result)
    retention, retention_problems = retention_report(result)
    out += retention
    out += halls_report(result)
    return "\n".join(out) + "\n", problems + retention_problems


# ---------------------------------------------------------------- export for the game (Unity)
BALANCE_OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                           "unity", "LastHearth", "Assets", "LastHearth", "Resources", "balance.json")

# GDD 6: room types. name: (enum, unlock hearth, produces, key stat, how many cells can be built)
ROOM_TYPES = [
    ("Очаг", "Hearth", 1, None, None, 1),
    ("Жилые покои", "Bedrooms", 1, None, None, 4),
    ("Кухня", "Kitchen", 1, "Food", "Agility", 2),
    ("Угольная шахта", "CoalMine", 1, "Coal", "Endurance", 2),
    ("Казарма", "Barracks", 2, None, "Strength", 1),
    ("Склад", "Warehouse", 2, None, None, 1),
    ("Рудник", "Mine", 3, "Iron", "Strength", 2),
    ("Таверна", "Tavern", 3, None, "Spirit", 1),
    ("Торговая лавка", "TradingPost", 4, "Silver", "Spirit", 2),
    ("Кузня", "Forge", 5, None, "Strength", 1),
    ("Лазарет", "Infirmary", 5, None, "Intellect", 1),
    ("Библиотека", "Library", 6, None, "Intellect", 1),
    ("Тренировочный зал", "TrainingHall", 7, None, None, 1),
    ("Теплица", "Greenhouse", 8, "Food", "Intellect", 1),
    ("Сторожевая башня", "Watchtower", 9, None, "Agility", 1),
    ("Часовня Угля", "Chapel", 12, None, "Spirit", 1),
    ("Архив Стражей", "Archive", 15, None, "Intellect", 1),
]
ROOM_ENUM = {name: enum for name, enum, *_ in ROOM_TYPES}
STAT_DIVISOR = 25          # GDD 6: output x (1 + stat / 25)
WORKERS_PER_ROOM = 2       # one cell holds two workers; a merged room holds two per cell

# GDD 6: the mountain opens in sections with the hearth level (our throne hall). Each section:
# (hearth level, floors from-to, cells from-to), floors counted from 0 at the bottom (the hearth is on 3).
# A section opens when its far floor is warm enough for a room of that hearth level (checked below).
GRID_FLOORS, GRID_CELLS, HEARTH_FLOOR, HEARTH_CELLS = 10, 6, 3, (2, 3)
SECTIONS = [
    (1, 2, 4, 1, 4),    # around the hearth: 10 cells, 3 of them the starting rooms
    (3, 2, 4, 0, 5),    # the hearth floors to the walls
    (5, 1, 5, 0, 5),
    (8, 0, 6, 0, 5),
    (12, 7, 7, 0, 5),
    (16, 8, 8, 0, 5),
    (20, 9, 9, 0, 5),   # the whole mountain
]
# Obstacles (rubble) cover every cell that is not built; a builder clears them for coal and food.
CLEAR_TIME_SHARE = 0.25    # of a room job at this hearth level
CLEAR_MIN_SECONDS = 10
CLEAR_COAL_HOURS = 0.5     # hours of the steady hearth burn
CLEAR_FOOD_HOURS = 1.0     # hours of food of a full fortress
CLEAR_PER_DISTANCE = 0.25  # +25% cost and time per cell of distance from the hearth beyond the first

# GDD 6: rooms of one type and level side by side merge into one hall of up to 3 cells.
MERGE_MAX_WIDTH = 3
MERGE_BONUS_PER_CELL = 0.10  # +10% output for every cell beyond the first
MERGE_COST_PER_CELL = 1.0    # an upgrade costs the room job x width; it takes one builder and one timer

# GDD 8: births in the living quarters (a man and a woman), up to the dweller cap.
BIRTH_HOURS = 8.0            # living quarters of level 1
BIRTH_LEVEL_FACTOR = 0.93    # each level of the living quarters: 7% faster
BIRTH_MIN_HOURS = 1.0
BIRTH_STAT_SPREAD = 1        # a child's stat: the parents' average +-1
BIRTH_GIFT_CHANCE = 0.10     # chance of a gifted child: one stat higher
BIRTH_GIFT_BONUS = 3


STARTING_ROOMS = [(HEARTH_FLOOR, 1), (HEARTH_FLOOR, 4), (HEARTH_FLOOR - 1, 2)]  # Core/NewGame.cs


def cell_distance(floor, cell):
    """Steps from the hearth: floors plus cells beyond the hearth's two."""
    dx = max(HEARTH_CELLS[0] - cell, cell - HEARTH_CELLS[-1], 0)
    return abs(floor - HEARTH_FLOOR) + dx


def section_opens(floor, cell):
    levels = [lv for lv, f0, f1, c0, c1 in SECTIONS if f0 <= floor <= f1 and c0 <= cell <= c1]
    return min(levels) if levels else None


def clear_base(rows, level):
    """(seconds, coal, food) to clear an obstacle next to the hearth at this hearth level."""
    row = rows[min(level, len(rows)) - 1]
    seconds = max(CLEAR_MIN_SECONDS, int(round(row["room_h"] * 3600 * CLEAR_TIME_SHARE)))
    coal = max(1, int(round(coal_burn_per_hour(level) * CLEAR_COAL_HOURS)))
    food = max(1, int(round(dweller_cap(level) * 1.0 * CLEAR_FOOD_HOURS)))
    return seconds, coal, food


def birth_hours(room_level):
    return max(BIRTH_MIN_HOURS, BIRTH_HOURS * BIRTH_LEVEL_FACTOR ** (room_level - 1))


def expected_stat(level):
    """Main stat of a typical worker at this hearth level (training and recruiting)."""
    return 5 + 2 * level


def production_base(resource, level):
    """Output per worker-hour at stat 0 so that a typical fortress meets the economy model:
    materials = production share of the income, coal = the burn + 15%, food = the dwellers + 10%."""
    if resource == "Silver":
        target, rooms = total_income(level) * MATERIAL_SOURCES["Производство залов"] * SILVER_SHARE, 1
    elif resource == "Iron":
        target, rooms = total_income(level) * MATERIAL_SOURCES["Производство залов"] * (1 - SILVER_SHARE), 1
    elif resource == "Coal":
        target, rooms = coal_production_per_hour(level), 1
    else:
        target, rooms = food_per_hour(level) * 1.1, (2 if level >= 8 else 1)
    return target / (rooms * WORKERS_PER_ROOM * (1 + expected_stat(level) / STAT_DIVISOR))


# Campaign content (MODES.md, section 1): acts, bosses and the monster families of each act.
ACT_CONTENT = [
    ("Последний обоз", "Вожак стаи тварей", ["Fiend"]),
    ("Копи под Сторожевой", "Одержимый штейгер", ["Possessed", "Fiend"]),
    ("Шёпот в инее", "Ледяная вдова", ["Fiend", "Possessed"]),
    ("Мост Пепельных", "Пепельный мостник", ["Knight", "Possessed"]),
    ("Бледные послы", "Посол Бледного Двора", ["Knight"]),
    ("Чаща просыпается", "Дитя Матери Чащи", ["Root", "Fiend"]),
    ("Осада Стражей", "Рыцарь-командор Двора", ["Knight", "Root"]),
    ("Предатель в Архиве", "Архивариус Сайлас", ["Possessed", "Knight"]),
    ("Сердце тумана", "Сердце тумана", ["Fiend", "Root", "Possessed"]),
    ("Последний очаг", "Мать Чащи", ["Root", "Knight", "Possessed"]),
]
ELITE_SLOTS = (9, 15)                 # mission numbers inside an act; 19 is the boss
CAMPAIGN_FIRST_CLEAR_HOURS = 3        # first clear: three hours of full material income
CAMPAIGN_REPEATS_PER_DAY = 10         # repeats: instant auto-battle (MODES.md, 1)
# Ten repeats give about 40% of the daily essence (0.07 x 0.6 day x 10) and 5 hours of materials,
# which is the campaign's 15% of the material income (ECONOMY.md, section 4).
CAMPAIGN_REPEAT_ESSENCE_SHARE = 0.07
CAMPAIGN_REPEAT_MATERIAL_HOURS = 0.5


def level_at(reached, t):
    level = 1
    for lv, day in reached.items():
        if day <= t and lv > level:
            level = lv
    return level


def campaign_missions(reached):
    missions = []
    for n in range(1, MISSIONS + 1):
        act = (n - 1) // (MISSIONS_PER_ACT + 1) + 1
        index = (n - 1) % (MISSIONS_PER_ACT + 1) + 1
        name, boss, families = ACT_CONTENT[act - 1]
        kind = "Boss" if index == MISSIONS_PER_ACT + 1 else "Elite" if index in ELITE_SLOTS else "Normal"
        day = mission_day(n)
        lv = level_at(reached, day)
        missions.append({
            "act": act, "index": index, "kind": kind, "rank": lv,
            "family": families[(index - 1) % len(families)],
            "day": round(day, 2),
            "essence": int(round(essence_income_per_day(lv) * MISSION_ESSENCE_DAYS)),
            "materials": int(round(total_income(lv) * CAMPAIGN_FIRST_CLEAR_HOURS)),
            "sparks": DIAMONDS_PER_ACT_BOSS if kind == "Boss" else 0,
        })
    return missions


def export_balance(result):
    """Everything the game needs, from the same model that the checks passed on (docs/ECONOMY.md)."""
    import json
    rows = result["rows"]
    levels = []
    for row in rows:
        lv = row["level"]
        room_job = row["cost"] / (row["rooms"] + 3)
        levels.append({
            "level": lv,
            "hearthSeconds": int(round(row["hearth_h"] * 3600)),
            "roomSeconds": int(round(row["room_h"] * 3600)),
            "roomJobCost": int(math.ceil(room_job)),
            "hearthJobCost": int(math.ceil(hearth_job(row))),
            "storage": storage(row),
            "builders": pacing.builders(lv),
            "keyRoom": ROOM_ENUM[key_room(lv)],
            "heroRequirement": hero_requirement(lv),
        })
    data = {
        "version": 1,
        "generatedBy": "hearth/tools/economy.py",
        "maxHearthLevel": pacing.MAX_LEVEL,
        "hearthLevels": levels,
        "warehouseStorage": [storage(row) for row in rows] + [nice(storage(rows[-1]) * 1.4, 3)],
        "silverShare": SILVER_SHARE,
        "rules": {"storageGateFrom": STORAGE_GATE_FROM, "heroGateFrom": HERO_GATE_FROM, "heroCore": HERO_CORE},
        "build": {"queuePerBuilder": pacing.QUEUE_PER_BUILDER, "freeFinishSeconds": FREE_FINISH_MINUTES * 60},
        "offlineCapHours": pacing.OFFLINE_CAP_H,
        "starter": {"materials": int(pacing.STARTER_MATERIALS), "essence": STARTER_ESSENCE, "coal": 300, "food": 200},
        "warmth": {"base": 100, "perLevel": 35, "floorFalloff": 0.8, "roomNeedBase": 60, "roomNeedGrowth": 1.06,
                   "frozenOutput": 0.5},
        "hearthModes": [
            {"mode": "Smoldering", "coal": 0.5, "warmth": 0.7, "output": 1.0},
            {"mode": "Steady", "coal": 1.0, "warmth": 1.0, "output": 1.0},
            {"mode": "Blazing", "coal": 2.0, "warmth": 1.3, "output": 1.15},
        ],
        "coalPerWarmthHour": 0.085,
        "foodPerDwellerHour": 1.0,
        "dwellers": {"capBase": 6, "capPerLevel": 2},
        "heroes": {"levelCostBase": 40, "levelCostGrowth": 1.11, "capBase": 5, "capPerHearth": 2},
        "speedupPoints": [[m, d] for m, d in SPEEDUP_POINTS],
        "production": {"statDivisor": STAT_DIVISOR, "workersPerRoom": WORKERS_PER_ROOM,
                       "base": {r: [round(production_base(r, lv), 3) for lv in range(1, 31)]
                                for r in ("Coal", "Food", "Iron", "Silver")}},
        "rooms": [{"type": enum, "name": name, "unlock": unlock, "produces": produces, "stat": stat, "maxCount": count}
                  for name, enum, unlock, produces, stat, count in ROOM_TYPES],
        "grid": {"floors": GRID_FLOORS, "cells": GRID_CELLS, "hearthFloor": HEARTH_FLOOR,
                 "hearthCells": list(HEARTH_CELLS),
                 "sections": [{"hearth": lv, "floorFrom": f0, "floorTo": f1, "cellFrom": c0, "cellTo": c1}
                              for lv, f0, f1, c0, c1 in SECTIONS]},
        "obstacles": {"seconds": [clear_base(rows, lv)[0] for lv in range(1, 31)],
                      "coal": [clear_base(rows, lv)[1] for lv in range(1, 31)],
                      "food": [clear_base(rows, lv)[2] for lv in range(1, 31)],
                      "perDistance": CLEAR_PER_DISTANCE},
        "merge": {"maxWidth": MERGE_MAX_WIDTH, "bonusPerCell": MERGE_BONUS_PER_CELL, "costPerCell": MERGE_COST_PER_CELL},
        "births": {"hours": BIRTH_HOURS, "levelFactor": BIRTH_LEVEL_FACTOR, "minHours": BIRTH_MIN_HOURS,
                   "statSpread": BIRTH_STAT_SPREAD, "giftChance": BIRTH_GIFT_CHANCE, "giftBonus": BIRTH_GIFT_BONUS},
        "squadSlots": list(SQUAD_SLOTS),
        "campaign": {
            "acts": [{"name": n, "boss": bname, "families": fam} for n, bname, fam in ACT_CONTENT],
            "missions": campaign_missions(result["reached"]),
            "chestSparks": DIAMONDS_PER_3_STAR_CHEST,
            "repeatsPerDay": CAMPAIGN_REPEATS_PER_DAY,
            "repeatEssenceShare": CAMPAIGN_REPEAT_ESSENCE_SHARE,
            "repeatMaterialHours": CAMPAIGN_REPEAT_MATERIAL_HOURS,
            "firstClearHours": CAMPAIGN_FIRST_CLEAR_HOURS,
        },
        **kennel_export(result),  # pets, companions and shops (section "pets, companions and shops" below)
        "halls": halls_export(result),  # the work of the halls (section "the work of the halls" below)
        "activities": {
            "essenceByLevel": [int(round(essence_income_per_day(lv))) for lv in range(1, 31)],
            "surge": {"unlock": SURGE_UNLOCK, "autoShare": SURGE_AUTO_SHARE, "startHour": 19,
                      "essenceShare": SURGE_ESSENCE_SHARE, "hourglassMinutes": HOURGLASS_SURGE_MINUTES,
                      "emberByLevel": [ember_per_surge(lv) for lv in range(1, 31)],
                      "warmWeek": {"surges": WARM_WEEK[0], "output": WARM_WEEK[1], "hours": WARM_WEEK[2]}},
            "heart": {"nodeCost": [heart_node_cost(n) for n in range(1, HEART_NODES + 1)],
                      "nodeGate": [heart_node_gate(n) for n in range(1, HEART_NODES + 1)],
                      "perBranch": HEART_NODES // 3, "warmth": 0.03, "coal": 0.03, "output": 0.02, "defense": 0.05},
            "hunt": {"unlock": HUNT_UNLOCK, "tracksPerDay": HUNTS_PER_DAY, "trackHours": 8,
                     "coresByLevel": [cores_per_hunt(lv) for lv in range(1, 31)],
                     "weekly": {"weekday": WEEKLY_MONSTER[0], "hpMult": WEEKLY_MONSTER[1], "attackMult": WEEKLY_MONSTER[2],
                                "coreMult": WEEKLY_MONSTER[3]}},
            "skills": {"max": SKILL_MAX, "heroLevelsPerSkill": 6,
                       "cost": [skill_cost(k) for k in range(1, SKILL_MAX)]},
            "legendary": {"fromHearth": LEGENDARY_FROM, "cores": LEGENDARY_COST},
            "routes": {"unlock": ROUTES_UNLOCK, "squadsByLevel": [routes_per_day(lv) for lv in range(1, 31)],
                       "blueprintsByLevel": [blueprints_per_route(lv) for lv in range(1, 31)],
                       "essenceShare": ROUTE_ESSENCE_SHARE, "materialHours": ROUTE_MATERIAL_HOURS,
                       "incomeByLevel": [int(round(total_income(lv))) for lv in range(1, 31)]},
            "specializations": {"tiers": [list(t) for t in SPEC_TIERS], "ranks": SPEC_RANKS,
                                "ranksFromHearth": SPEC_RANKS_FROM},
            "shop": {"fourthBuilder": FOURTH_BUILDER_PRICE, "fourthBuilderFrom": 8, "packPrice": PACK_PRICE,
                     "packsPerWeek": PACKS_PER_WEEK, "refresh": [SHOP_REFRESH, 2 * SHOP_REFRESH, 4 * SHOP_REFRESH],
                     "slots": 8, "arenaTicket": [ARENA_EXTRA_TICKETS, 2 * ARENA_EXTRA_TICKETS, 3 * ARENA_EXTRA_TICKETS]},
            "pvp": {"arenaUnlock": ARENA_UNLOCK, "tickets": ARENA_TICKETS, "ticketHours": ARENA_TICKET_HOURS,
                    "startRating": ARENA_START_RATING,
                    "leagues": [{"name": n, "rating": r, "sparks": d, "glory": g, "essenceDays": e}
                                for n, r, d, g, e in ARENA_LEAGUES],
                    "winEssence": ARENA_WIN_ESSENCE, "winGlory": ARENA_GLORY[0], "lossGlory": ARENA_GLORY[1],
                    "raidUnlock": RAID_UNLOCK, "flags": RAID_FLAGS, "flagHours": RAID_FLAG_HOURS,
                    "lootHours": RAID_LOOT_HOURS, "trophyGlory": RAID_TROPHY_GLORY, "revengeHours": RAID_REVENGE_HOURS,
                    "gloryShop": [{"goods": g, "price": p, "perWeek": w, "amount": a} for g, p, w, a in GLORY_SHOP]},
            "events": [{"id": EVENT_IDS[name], "name": name, "period": spec[0], "first": spec[1], "days": spec[2],
                        "sparks": spec[3], "spend": spec[4], "essenceDays": spec[5], "materialHours": spec[6],
                        "hourglassMinutes": spec[7], "unlock": EVENT_UNLOCK_HEARTH[name],
                        "group": EVENT_GROUP.get(name, 50), "currency": EVENT_CURRENCY[name],
                        "shop": [{"goods": g, "price": p, "limit": n, "amount": a} for g, p, n, a in EVENT_SHOPS[name]]}
                       for name, spec in EVENTS.items()],
            "eventCurrency": {"perThreshold": EVENT_CURRENCY_PER_THRESHOLD, "rank": EVENT_CURRENCY_RANK,
                              "silver": EVENT_CURRENCY_SILVER, "burnHours": 24},
            "daily": {"tasksNeeded": 3, "taskSparks": DIAMONDS_DAILY_TASKS, "taskHourglassMinutes": HOURGLASS_DAILY_MINUTES,
                      "calendarSparks": DIAMONDS_LOGIN_CALENDAR, "achievementSparks": ACHIEVEMENT_POOL,
                      "calendar": [gift_export(g) for g in LOGIN_CALENDAR]},
            "season": season_export(),
        },
    }
    os.makedirs(os.path.dirname(BALANCE_OUT), exist_ok=True)
    with open(BALANCE_OUT, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=1)
        f.write("\n")


# ---------------------------------------------------------------- pets, companions and shops (GDD 8.1-8.3, 13.1)
# Like Hustle Castle, but honest: everything is earned by play; Sparks only bring a few things sooner,
# at a fixed price shown in advance (no lottery, no random boxes).
#
# Pets live in the fortress. The player owns several, ONE is active at a time and helps the whole
# fortress and the squad (one rule, easy to read). Treats («Лакомства») raise pets and the bond of
# companions; nothing else spends them, so they never compete with essence or materials.
PET_MAX_LEVEL = 10
TREAT_LEVEL_COST = [10, 20, 35, 55, 80, 110, 150, 200, 260]  # level L -> L+1; 920 treats from 1 to 10
TREATS_PER_HUNT_WIN = 3        # every won hunt (3 a day from hearth 6)
TREATS_DAILY = 5               # the reward of the daily tasks
MERCHANT_TREATS = 10           # the merchant sells 10 treats for an hour of silver income, once a day
PREMIUM_TREATS = (30, 60, 2)   # 30 treats for 60 Sparks, twice a week (a speed-up hour costs 20)
PET_STRIKE_EVERY = 50          # the raven strikes every 5 s of battle
# pet: (id, name, effect, value at level 1, + per level, how it is found, condition,
#       price in hours of silver income (0: not sold), price in Sparks (0: not sold), text)
# Effects: Attack/Health — squad attack/health x(1 + value); Strike — a strike every 5 s for value x
# the squad's average attack; CoalBurn — coal burn x(1 - value); Materials — iron and silver output;
# Supply — coal and food output. At level 10 the battle pets give +12% (a raven: ~+10% damage), the
# fortress pets +12-14%: noticeable, smaller than one hearth level (+5% gear, +7% a hero level each).
PETS = [
    ("Wolf", "Волк", "Attack", 0.03, 0.01, "HuntWins", 1, 0, 0, "первая победа на Охоте"),
    ("Owl", "Сова", "Materials", 0.03, 0.01, "LoginDays", 7, 0, 0, "7 дней в игре (календарь входа)"),
    ("CoalCat", "Кот-углежог", "CoalBurn", 0.05, 0.01, "Shop", 0, 6, 0, "лавка торговца, 6 часов дохода серебром"),
    ("Raven", "Ворон", "Strike", 0.8, 0.15, "HuntWins", 15, 0, 0, "15 побед на Охоте"),
    ("BearCub", "Медвежонок", "Health", 0.03, 0.01, "Missions", 57, 0, 400, "3 акта кампании или 400 искр"),
    ("MistFox", "Лиса тумана", "Supply", 0.04, 0.01, "EventThresholds", 5, 0, 400, "5 порогов одного события или 400 искр"),
]
# Companions: each hero takes one. Its bond level (1-10) belongs to the hero, the kind can be changed
# freely; the bond is raised with treats up to 1 + hero level / 4 (like skills: 1 + level / 6).
COMPANION_MAX_LEVEL = 10
COMPANION_HERO_LEVELS_PER_LEVEL = 4
# companion: (id, name, skill, every ticks (0.1 s), power at level 1, + per level, found, condition,
#             silver hours, Sparks, text)
# Guard — the hero takes x(1 - power) damage (8% -> 17%); Bite — every 5 s for power x the hero's
# attack (about +10% of the hero's damage at level 1, +18% at 10); Stagger — every 5 s adds stagger to
# the boss (a Cleaver's hit adds 3, a stun needs 100); Heal — every 6 s heals the hero by power x health.
COMPANIONS = [
    ("Squire", "Оруженосец", "Guard", 0, 0.08, 0.01, "Start", 0, 0, 0, "с первым героем"),
    ("Wolfhound", "Волкодав", "Bite", 50, 0.4, 0.04, "HuntWins", 3, 0, 0, "3 победы на Охоте"),
    ("Falcon", "Сокол", "Stagger", 50, 5, 0.5, "Shop", 0, 8, 0, "лавка торговца, 8 часов дохода серебром"),
    ("Firefly", "Угольный светлячок", "Heal", 60, 0.03, 0.003, "EventThresholds", 3, 0, 300, "3 порога одного события или 300 искр"),
]
# Shops. The merchant's goods are priced in hours of the fortress income (silver), so they matter at
# every level. The exchange at the trading post is worse than the Ash Fair (x0.92 at a fair rate).
EXCHANGE_FEE = 0.8             # 100 of value -> 80 of another good
EXCHANGE_HOURS_PER_DAY = 4     # the value exchanged per day, in hours of income
# Sparks: an hourglass hour costs as much as a speed-up hour, so the shop is never a cheaper path.
PREMIUM_HOURGLASS = (60, 20, 5)        # minutes, Sparks, per day
PREMIUM_DWELLER = (250, 1)             # «Ветеран из тумана»: fixed stats shown in advance; Sparks, per week
PREMIUM_DWELLER_BONUS = 4              # +4 to every stat over a refugee of the same hearth level
# Deals of the day: three of these, a new set every local day, each once (cheaper than the shelf).
# (id, goods, amount, price in hours of silver income); amounts: treats; hours of the coal burn;
# hours of the dwellers' food; hourglass minutes; days of the free player's essence (Brik: 0.25 for 2-3 h).
DAILY_DEALS = [
    ("Treats", "Treats", 15, 1.0),
    ("Coal", "Coal", 24, 1.0),
    ("Food", "Food", 24, 1.0),
    ("Hourglass", "Hourglass", 30, 1.0),
    ("Essence", "Essence", 0.2, 1.5),
]
DAILY_DEALS_SHOWN = 3
ROOM_UNLOCK = {enum: unlock for _, enum, unlock, *_ in ROOM_TYPES}


def treats_per_day(level):
    t = TREATS_DAILY
    if level >= HUNT_UNLOCK:
        t += HUNTS_PER_DAY * TREATS_PER_HUNT_WIN
    if level >= ROOM_UNLOCK["TradingPost"]:
        t += MERCHANT_TREATS
    return t


# The free player's pet and companions are part of the squad's power, so the campaign (whose recommended
# strength is that player's squad x0.95, section 7) counts them: enemies get the expected bonus on top.
# A player who ignores pets falls a little behind; one who raises them follows the model.
KENNEL_DAMAGE_SHARE = 0.5      # half the damage dealers walk with a Wolfhound (bites)
KENNEL_GUARD_SHARE = 0.5       # the boss hits the front line, where the Squire guards the Guardian


def kennel_expected(reached):
    """Expected active pet level and bond level of the five core heroes when each hearth level is reached:
    treats go to the battle pet up to level 5, then to the bonds evenly (cap 1 + hero level / 4, hero level
    about 2L), then to the pet."""
    level_on = level_by_day(reached)
    treats, pet, bond = 0, 0, 1
    expected = {}
    for day in range(0, DAYS + 1):
        lv = level_on[day]
        expected.setdefault(lv, (pet, bond))
        treats += treats_per_day(lv)
        if lv >= HUNT_UNLOCK:
            pet = max(pet, 1)
        cap = min(COMPANION_MAX_LEVEL, 1 + 2 * lv // COMPANION_HERO_LEVELS_PER_LEVEL)
        while True:
            if 0 < pet < 5 and treats >= TREAT_LEVEL_COST[pet - 1]:
                treats -= TREAT_LEVEL_COST[pet - 1]
                pet += 1
            elif bond < cap and treats >= HERO_CORE * TREAT_LEVEL_COST[bond - 1]:
                treats -= HERO_CORE * TREAT_LEVEL_COST[bond - 1]
                bond += 1
            elif 0 < pet < PET_MAX_LEVEL and treats >= TREAT_LEVEL_COST[pet - 1]:
                treats -= TREAT_LEVEL_COST[pet - 1]
                pet += 1
            else:
                break
    for lv in range(2, pacing.MAX_LEVEL + 1):
        expected.setdefault(lv, expected[lv - 1])
    return expected


def kennel_factors(reached):
    """Enemy health and attack multipliers of the campaign by hearth level (the free player's kennel)."""
    wolf = next(p for p in PETS if p[0] == "Wolf")
    bite = next(c for c in COMPANIONS if c[0] == "Wolfhound")
    guard = next(c for c in COMPANIONS if c[0] == "Squire")
    hp, attack = [], []
    exp = kennel_expected(reached)
    for lv in range(1, pacing.MAX_LEVEL + 1):
        pet, bond = exp[lv]
        damage = 1 + (wolf[3] + wolf[4] * (pet - 1) if pet > 0 else 0)
        if lv >= HUNT_UNLOCK:  # the Wolfhound comes after three won hunts
            damage *= 1 + KENNEL_DAMAGE_SHARE * (bite[4] + bite[5] * (bond - 1)) * 12 / bite[3]  # hero interval ~1.2 s
        tough = 1 / (1 - KENNEL_GUARD_SHARE * (guard[4] + guard[5] * (bond - 1)))
        hp.append(round(damage, 3))
        attack.append(round(tough, 3))
    return hp, attack


def kennel_export(result):
    """balance.json: "pets", "companions" and "market"."""
    hp, attack = kennel_factors(result["reached"])
    def unlock(kind, cond, silver, sparks):
        return {"source": kind, "condition": cond, "silverHours": silver, "sparks": sparks}
    return {
        "pets": {
            "maxLevel": PET_MAX_LEVEL, "levelCost": TREAT_LEVEL_COST, "strikeEvery": PET_STRIKE_EVERY,
            "treatsPerHuntWin": TREATS_PER_HUNT_WIN, "treatsDaily": TREATS_DAILY,
            "kinds": [dict(id=i, name=n, effect=e, value=v, perLevel=p, text=t, **unlock(s, c, sh, sp))
                      for i, n, e, v, p, s, c, sh, sp, t in PETS],
        },
        "companions": {
            "maxLevel": COMPANION_MAX_LEVEL, "levelCost": TREAT_LEVEL_COST,
            "heroLevelsPerLevel": COMPANION_HERO_LEVELS_PER_LEVEL,
            "campaignHp": hp, "campaignAttack": attack,
            "kinds": [dict(id=i, name=n, skill=k, every=ev, power=pw, perLevel=p, text=t, **unlock(s, c, sh, sp))
                      for i, n, k, ev, pw, p, s, c, sh, sp, t in COMPANIONS],
        },
        "market": {
            "merchantTreats": MERCHANT_TREATS, "exchangeFee": EXCHANGE_FEE, "exchangeHours": EXCHANGE_HOURS_PER_DAY,
            "premiumTreats": list(PREMIUM_TREATS), "premiumHourglass": list(PREMIUM_HOURGLASS),
            "premiumDweller": list(PREMIUM_DWELLER), "premiumDwellerBonus": PREMIUM_DWELLER_BONUS,
            "deals": [{"id": i, "goods": g, "amount": a, "hours": h} for i, g, a, h in DAILY_DEALS],
            "dealsShown": DAILY_DEALS_SHOWN,
        },
    }


def kennel_report(result):
    """ECONOMY.md, section 14: pets, companions and the shops."""
    reached = result["reached"]
    out = []
    w = out.append
    w("")
    w("## 14. Питомцы, компаньоны и лавки")
    w("")
    w("Всё добывается игрой; искры только ускоряют пару вещей по цене, известной заранее (никаких лотерей). "
      "Подробно — `docs/GDD.md`, разделы 8.1–8.3 и 13.1.")
    w("")
    w("**Лакомства** растят питомцев и узы компаньонов и больше ни на что не тратятся. Источники: поручения дня — %d, "
      "победа на Охоте — %d (3 следа в день с очага %d), торговец — %d за час дохода серебром раз в день, предложение "
      "дня, %d за %d искр дважды в неделю." % (TREATS_DAILY, TREATS_PER_HUNT_WIN, HUNT_UNLOCK, MERCHANT_TREATS,
                                              PREMIUM_TREATS[0], PREMIUM_TREATS[1]))
    w("")
    total = sum(treats_per_day(level_at(reached, d)) for d in range(1, DAYS + 1))
    one = sum(TREAT_LEVEL_COST)
    w("| Уровень | %s |" % " | ".join(str(i + 2) for i in range(len(TREAT_LEVEL_COST))))
    w("|---|%s" % ("---|" * len(TREAT_LEVEL_COST)))
    w("| Лакомств | %s |" % " | ".join(str(c) for c in TREAT_LEVEL_COST))
    w("")
    w("Бесплатный игрок получает %s лакомств за год (%d в день на очаге 10). Один питомец или компаньон с 1-го до "
      "%d-го уровня — %d: активный питомец доходит до предела примерно за %d дней, а все питомцы и компаньоны "
      "пятёрки — %s лакомств, это цель и на второй год." % (
          num(total), treats_per_day(10), PET_MAX_LEVEL, one, math.ceil(one / treats_per_day(10)),
          num(one * (len(PETS) + HERO_CORE))))
    w("")
    w("**Питомцы** живут в крепости; активный — один, он помогает всей крепости и отряду.")
    w("")
    w("| Питомец | Бонус на 1-м / 10-м уровне | Как получить |")
    w("|---|---|---|")
    texts = {"Attack": "атака отряда +%d%% / +%d%%", "Health": "здоровье отряда +%d%% / +%d%%",
             "CoalBurn": "расход угля −%d%% / −%d%%", "Materials": "выработка железа и серебра +%d%% / +%d%%",
             "Supply": "выработка угля и еды +%d%% / +%d%%"}
    for i, n, e, v, p, s, c, sh, sp, t in PETS:
        top = v + p * (PET_MAX_LEVEL - 1)
        if e == "Strike":
            bonus = "удар раз в %d с: %s / %s средней атаки отряда" % (PET_STRIKE_EVERY // 10, dec(v, 2), dec(top, 2))
        else:
            bonus = texts[e] % (round(v * 100), round(top * 100))
        w("| %s | %s | %s |" % (n, bonus, t))
    w("")
    w("**Компаньоны** идут в бой рядом с героем, у каждого героя один. Уровень уз (1–%d) принадлежит герою и не выше "
      "`1 + уровень героя / %d`; вид компаньона меняется свободно." % (COMPANION_MAX_LEVEL, COMPANION_HERO_LEVELS_PER_LEVEL))
    w("")
    w("| Компаньон | Умение на 1-м / 10-м уровне | Как получить |")
    w("|---|---|---|")
    for i, n, k, ev, pw, p, s, c, sh, sp, t in COMPANIONS:
        top = pw + p * (COMPANION_MAX_LEVEL - 1)
        skill = {"Guard": "герой получает на %d%% / %d%% меньше урона" % (round(pw * 100), round(top * 100)),
                 "Bite": "укус раз в %d с: %d%% / %d%% атаки героя" % (ev // 10, round(pw * 100), round(top * 100)),
                 "Stagger": "раз в %d с +%s / +%s к шкале оглушения" % (ev // 10, dec(pw), dec(top)),
                 "Heal": "раз в %d с лечит героя на %s%% / %s%% здоровья" % (ev // 10, dec(pw * 100), dec(top * 100))}[k]
        w("| %s | %s | %s |" % (n, skill, t))
    w("")
    hp, attack = kennel_factors(reached)
    w("Питомец и компаньоны бесплатного игрока входят в рекомендуемую силу боёв кампании (раздел 7): враги на "
      "очаге 10 / 20 / 30 крепче на %d / %d / %d%% и бьют сильнее на %d / %d / %d%%. Кто растит питомцев и узы, идёт "
      "по графику модели; кто забыл про них, немного отстаёт. Призраки арены и Набегов повторяют и питомца, и "
      "компаньонов отряда игрока, так что PvP остаётся честным." % (
          round((hp[9] - 1) * 100), round((hp[19] - 1) * 100), round((hp[29] - 1) * 100),
          round((attack[9] - 1) * 100), round((attack[19] - 1) * 100), round((attack[29] - 1) * 100)))
    w("")
    w("**Лавки** (окно «Лавка», вкладки):")
    w("")
    w("| Лавка | Что | Цена | Лимит |")
    w("|---|---|---|---|")
    w("| Торговец (серебро) | 8 товаров Брика: эссенция, уголь, еда, часы, печать найма | 1–3 часа дохода | новый товар в 00:00 и 12:00 |")
    w("| Торговец (серебро) | %d лакомств | 1 час дохода | раз в день |" % MERCHANT_TREATS)
    w("| Торговец (серебро) | Кот-углежог, Сокол | 6 и 8 часов дохода | один раз |")
    w("| Предложения дня | %d из %d: лакомства, уголь, еда, часы, эссенция | 1–1,5 часа дохода | каждое раз в день |" % (
        DAILY_DEALS_SHOWN, len(DAILY_DEALS)))
    w("| Обмен у торговой лавки | железо ↔ серебро, уголь ↔ еда | ×%s ценности (Ярмарка — до ×0,92) | %d часа дохода в день |" % (
        dec(EXCHANGE_FEE), EXCHANGE_HOURS_PER_DAY))
    w("| Искры | песочные часы %d мин | %d искр (как ускорение на час) | %d в день |" % PREMIUM_HOURGLASS)
    w("| Искры | Ветеран из тумана: житель, характеристики видны до покупки (+%d к каждой) | %d искр | %d в неделю |" % (
        PREMIUM_DWELLER_BONUS, PREMIUM_DWELLER[0], PREMIUM_DWELLER[1]))
    w("| Искры | Медвежонок, Лиса тумана, Угольный светлячок (все есть и за игру) | 400, 400, 300 искр | один раз |")
    w("| Искры | %d лакомств | %d искр | %d в неделю |" % PREMIUM_TREATS)
    w("| Искры | четвёртый строитель, наборы ресурсов, украшения | как в разделе 8 | наборов %d в неделю |" % PACKS_PER_WEEK)
    w("")
    w("Модель года (разделы 1–13) не учитывает питомцев: бонус одного активного питомца (до +12% выработки) меньше "
      "точности попадания в целевые дни (15%), а траты искр в лавке — по желанию, из того же бюджета, что ускорения.")
    return out


# ---------------------------------------------------------------- calendar, season, warm week, monster of the week
# GDD 12 and MODES.md 7: retention without pressure. A missed day takes nothing away. The Sparks of the
# calendar are the 140 of section 8; the other gifts are small and stay outside the pacing model (checked below).
# gift: (kind, amount, decoration); essence in days of the free player's essence, the hourglass in minutes.
LOGIN_WEEK = [("Sparks", 5, ""), ("Treats", 5, ""), ("Sparks", 5, ""), ("Hourglass", 15, ""), ("Sparks", 5, ""),
              ("Essence", 0.2, ""), ("Sparks", 20, "")]
LOGIN_CALENDAR = LOGIN_WEEK * 4  # 28 days: a big gift every 7th
SEASON_DAYS = 42                 # 6 weeks, then the next season starts at once
SEASON_STEPS = 30
SEASON_STEP_XP = 120             # a regular player (about 100 points a day) finishes in 5 weeks of 6
# points of the season: a daily task done, the daily chest, a calendar day, a won Surge (live / automatic),
# a won hunt, a threshold of an event
SEASON_XP = {"task": 10, "daily": 20, "calendar": 5, "surge": 10, "surgeAuto": 5, "hunt": 5, "event": 5}
SEASON_DECOR = ["banner_green", "candles", "rug", "plant", "crystal"]  # cosmetic only (the "Honesty" pillar)
SEASON_TRACK = [g for k in range(5) for g in (("Treats", 10, ""), ("Essence", 0.2, ""), ("Hourglass", 15, ""),
                                              ("Essence", 0.2, ""), ("Hourglass", 30, ""), ("Decor", 1, SEASON_DECOR[k]))]
SEASON_PREMIUM = False           # GDD 13: the premium track is ready in the code and switched off
WARM_WEEK = (7, 0.05, 24)        # «Тёплая неделя»: 7 Surges in a row without a defeat -> +5% output for 24 hours
WEEKLY_MONSTER = (6, 1.6, 1.3, 2.0)  # Sunday (0 = Monday): health x1.6, attack x1.3, Cores x2 for the week's first win


def gift_export(g):
    kind, amount, decor = g
    return {"kind": kind, "amount": amount, "decor": decor} if decor else {"kind": kind, "amount": amount}


def season_export():
    x = SEASON_XP
    return {"days": SEASON_DAYS, "steps": SEASON_STEPS, "stepXp": SEASON_STEP_XP, "premium": SEASON_PREMIUM,
            "track": [gift_export(g) for g in SEASON_TRACK], "premiumTrack": [],
            "taskXp": x["task"], "dailyXp": x["daily"], "calendarXp": x["calendar"], "surgeXp": x["surge"],
            "surgeAutoXp": x["surgeAuto"], "huntXp": x["hunt"], "eventXp": x["event"]}


def gift_text(g):
    kind, amount, _ = g
    if kind == "Sparks":
        return "%d искр" % amount
    if kind == "Treats":
        return "%d лакомств" % amount
    if kind == "Hourglass":
        return "часы %d мин" % amount
    if kind == "Essence":
        return "%s дня эссенции" % dec(amount)
    return "украшение"


def retention_report(result):
    """ECONOMY.md, section 15: the login calendar, the season track and the small weekly bonuses."""
    out, problems = [], []
    w = out.append
    cal = lambda kind: sum(a for k, a, _ in LOGIN_CALENDAR if k == kind)
    track = lambda kind: sum(a for k, a, _ in SEASON_TRACK if k == kind)
    if len(LOGIN_CALENDAR) != 28 or cal("Sparks") != DIAMONDS_LOGIN_CALENDAR:
        problems.append("календарь входа: не 28 дней или не %d искр за круг" % DIAMONDS_LOGIN_CALENDAR)
    if len(SEASON_TRACK) != SEASON_STEPS:
        problems.append("сезон: на дорожке не %d шагов" % SEASON_STEPS)
    per_year_cal, per_year_season = DAYS / 28.0, DAYS / float(SEASON_DAYS)
    essence_days = cal("Essence") * per_year_cal + track("Essence") * per_year_season
    hourglass_h = (cal("Hourglass") * per_year_cal + track("Hourglass") * per_year_season) / 60.0
    essence_share = essence_days / DAYS
    hourglass_share = hourglass_h * 60 / max(result["speed_minutes_total"], 1)
    hunt_share = (WEEKLY_MONSTER[3] - 1) / (HUNTS_PER_DAY * 7.0)
    if essence_share > 0.1 or hourglass_share > 0.1:
        problems.append("календарь и сезон дают больше 10% эссенции или песочных часов модели")
    regular = 4 * SEASON_XP["task"] + SEASON_XP["daily"] + SEASON_XP["calendar"] + SEASON_XP["surge"] + \
        HUNTS_PER_DAY * SEASON_XP["hunt"] + 2 * SEASON_XP["event"]
    finish_days = SEASON_STEPS * SEASON_STEP_XP / float(regular)
    if finish_days > SEASON_DAYS - 3:
        problems.append("сезон: обычный игрок не проходит дорожку до конца сезона")
    w("")
    w("## 15. Календарь входа, сезон и недельные бонусы")
    w("")
    w("Удержание без давления (`docs/GDD.md`, раздел 12; `docs/MODES.md`, раздел 7): пропуск дня ничего не отнимает.")
    w("")
    w("**Календарь входа:** 28 наград за дни, в которые игра открывалась; дни не обязаны идти подряд, награда дня "
      "ждёт, пока её заберут. Неделя календаря: %s. За круг %d искр (раздел 8), %d лакомств, %d минут песочных часов "
      "и %s дня эссенции." % (", ".join(gift_text(g) for g in LOGIN_WEEK), cal("Sparks"), cal("Treats"),
                               cal("Hourglass"), dec(cal("Essence"))))
    w("")
    w("**Сезон, %d недель:** бесплатная дорожка из %d шагов по %d очков. Очки: выполненное поручение — %d, награда дня — %d, "
      "день календаря — %d, победа в Наплыве — %d (без игрока — %d), победа на Охоте — %d, порог события — %d. "
      "Обычный игрок набирает около %d очков в день и проходит дорожку за %s дня из %d. Награды за сезон: %d лакомств, "
      "%s дня эссенции, %s часа песочных часов и %d украшений (только для красоты). Невзятые награды выдаются в конце "
      "сезона, следующий начинается сразу. Премиум-дорожка подготовлена в коде, но выключена (`docs/GDD.md`, раздел 13)." % (
          SEASON_DAYS // 7, SEASON_STEPS, SEASON_STEP_XP, SEASON_XP["task"], SEASON_XP["daily"], SEASON_XP["calendar"],
          SEASON_XP["surge"], SEASON_XP["surgeAuto"], SEASON_XP["hunt"], SEASON_XP["event"], regular, dec(finish_days),
          SEASON_DAYS, track("Treats"), dec(track("Essence")), dec(track("Hourglass") / 60.0), track("Decor")))
    w("")
    w("**Тёплая неделя:** %d Наплывов подряд без поражения дают +%d%% выработки на %d часа. **Чудовище недели** "
      "на Охоте (воскресенье): здоровье ×%s, атака ×%s, первая победа за неделю даёт ×%s Сердцевин." % (
          WARM_WEEK[0], round(WARM_WEEK[1] * 100), WARM_WEEK[2], dec(WEEKLY_MONSTER[1]), dec(WEEKLY_MONSTER[2]),
          dec(WEEKLY_MONSTER[3])))
    w("")
    w("Модель года (разделы 1–13) этих подарков не учитывает, как и питомцев: за год календарь и сезон дают %s дней "
      "эссенции (%d%% её дохода) и %s часов песочных часов (%d%% ускорений), Тёплая неделя — около %s%% выработки, "
      "чудовище недели — %d%% Сердцевин Охоты. Это меньше точности попадания в целевые дни (15%%)." % (
          num(essence_days), round(essence_share * 100), num(hourglass_h), round(hourglass_share * 100),
          dec(WARM_WEEK[1] * WARM_WEEK[2] / 24.0 / WARM_WEEK[0] * 100), round(hunt_share * 100)))
    for p in problems:
        w("")
        w("- **Проблема:** %s" % p)
    return out, problems


# ---------------------------------------------------------------- the work of the halls (GDD 6)
# The rooms that produce nothing have their own work (Core/GameSession.Rooms.cs): the tavern hires,
# the infirmary heals, the library researches, the training hall trains, the watchtower scouts,
# the chapel raises heroes and gives gifts, the archive keeps the story. The forge belongs to the
# equipment (another part of the model). Silver prices are in hours of the material income, so
# every price matters at every hearth level; the shares are the sinks of section 4.

# Tavern («Таверна»): a board of recruits from the fog; a new board every few hours.
TAVERN_BOARD = 3                     # recruits on the board
TAVERN_BOARD_EXTRA_AT = (10, 20)     # +1 recruit at these tavern levels
TAVERN_BOARD_HOURS = 6               # a new board every 6 hours
# rarity (GDD 8): (id, name, + to every stat, + to the training cap, base weight in %, hire price in hours of income)
RARITIES = [
    ("Common", "обычный", 0, 0, 70, 0.25),
    ("Seasoned", "опытный", 2, 5, 24, 0.5),
    ("Rare", "редкий", 4, 10, 5, 1.0),
    ("Legendary", "легендарный", 7, 20, 1, 2.0),
]
RARITY_LUCK_SHIFT = (0, 0.6, 0.3, 0.1)  # every point of luck moves this much weight from the common recruits
RARITY_MIN_COMMON = 10
TAVERN_LUCK_PER_LEVEL = 1.0          # luck: the tavern level, plus the workers' Spirit / 10
TAVERN_LUCK_PER_SPIRIT = 0.1
HIRE_FOOD_HOURS = 6                  # food: 6 hours of the recruit's food x (1 + rarity)

# Infirmary («Лазарет»): injuries and the illness from the fog heal faster with the level and the staff.
INJURY_HOURS = 6                     # a hurt dweller walks no route for 6 hours without the infirmary
ILLNESS_HOURS = 12                   # «туманная хворь»: the dweller works at half strength
ILLNESS_CHANCE = 0.12                # for every member of a team back from a route
SICK_OUTPUT = 0.5
HEAL_PER_LEVEL = 0.05                # healing speed: 1 + 5% per level + the staff's Intellect / 50
HEAL_INTELLECT_DIVISOR = 50
HEAL_BLAZING = 1.25                  # GDD 7: the blazing hearth heals faster

# Library («Библиотека»): a tree of 60 researches in 4 branches of 15 steps. A step needs the one
# before it in its branch and a library of a level; one research at a time.
RESEARCH_TIERS = 15
RESEARCH_HOURS = 1.0                 # the first step; every next one x1.35 (the 15th: 67 hours)
RESEARCH_HOURS_GROWTH = 1.35
RESEARCH_COST_HOURS = (1.5, 0.75)    # materials: (1.5 + 0.75 x step) hours of the income at the library's level
RESEARCH_INTELLECT_DIVISOR = 100     # the staff's Intellect / 100 adds to the speed
# branch: (id, name, effects by step (cycled), value of each effect)
RESEARCH_BRANCHES = [
    ("Crafts", "Ремёсла", ["Supply", "Materials"],
     ["Крепкие кирки", "Мерные весы", "Подземные печи", "Плавильные горны", "Сушильни", "Счётные доски",
      "Вентиляция шахт", "Цеховые клейма", "Глубокие погреба", "Монетный двор", "Паровые насосы", "Торговые гильдии",
      "Глубинное бурение", "Векселя Стражей", "Сердце горы"]),
    ("War", "Ратное дело", ["Attack", "Health"],
     ["Заточка клинков", "Стёганые куртки", "Строй щитов", "Кольчужное плетение", "Закалка в углях", "Полевые лекари",
      "Гранёные стрелы", "Латы Стражей", "Боевые кличи", "Пепельная броня", "Клинки из звёздной руды", "Обет стойкости",
      "Тактика Двора", "Щит очага", "Наследие Стражей"]),
    ("Household", "Быт", ["CoalBurn", "Food", "Healing", "Training"],
     ["Тяга дымоходов", "Сытная похлёбка", "Травяные сборы", "Строевые упражнения", "Каменные экраны",
      "Соление и копчение", "Костоправы", "Наставники", "Угольные брикеты", "Общие трапезы", "Отвары из тумана",
      "Школа мечников", "Печи с поддувом", "Кладовые без порчи", "Тёплые палаты"]),
    ("Lore", "Тайны тумана", ["ResearchSpeed", "RecruitLuck", "SurgeDefense", "Decipher"],
     ["Записи наставников", "Слухи из таверн", "Сигнальные огни", "Ключи к шифрам", "Читальня", "Вербовщики",
      "Ловушки у люков", "Язык Двора", "Переписчики", "Знаки беженцев", "Обряд Вспышки", "Карты тумана",
      "Хранилище свитков", "Голос Угля", "Тайна Матери Чащи"]),
]
RESEARCH_VALUES = {
    "Supply": 0.015, "Materials": 0.015, "Attack": 0.015, "Health": 0.015, "CoalBurn": 0.015, "Food": 0.02,
    "Healing": 0.10, "Training": 0.05, "ResearchSpeed": 0.05, "RecruitLuck": 2, "SurgeDefense": 0.03, "Decipher": 0.10,
}
RESEARCH_TEXT = {
    "Supply": "выработка угля и еды +%s%%", "Materials": "выработка железа и серебра +%s%%",
    "Attack": "атака отряда +%s%%", "Health": "здоровье отряда +%s%%", "CoalBurn": "расход угля −%s%%",
    "Food": "жители едят на %s%% меньше", "Healing": "лечение в лазарете +%s%%", "Training": "тренировка +%s%%",
    "ResearchSpeed": "исследования +%s%%", "RecruitLuck": "удача найма +%s", "SurgeDefense": "защитники Наплыва +%s%%",
    "Decipher": "чтение записей в архиве +%s%%",
}

# Training hall («Тренировочный зал»): a dweller (or a hero) trains a stat of the player's choice, one
# point at a time. Up to 10 + 3 x hall level (+ the rarity), at most 100.
TRAIN_CAP_BASE = 10
TRAIN_CAP_PER_LEVEL = 3
TRAIN_HOURS = (1.0, 0.15)            # hours for the next point: 1 + 0.15 x stat ...
TRAIN_LEVEL_FACTOR = 0.98            # ... x0.98 for every hall level
TRAIN_SILVER_HOURS = (0.025, 50)     # silver: 0.025 hours of income x (1 + stat / 50) per point

# Watchtower («Сторожевая башня»): scouting, and the sentries help in the Surge.
# what the tower shows from its level: tonight's Surge, tomorrow's dens, where creatures come out,
# which stat the next route tests, the forecast of the automatic defense
SCOUT_LEVELS = [("Surge", 1), ("Dens", 3), ("Spawn", 5), ("Route", 8), ("Forecast", 10)]
WATCH_DEFENSE_PER_LEVEL = 0.005      # Surge defenders +0.5% per tower level ...
WATCH_AGILITY_DIVISOR = 400          # ... + the sentries' Agility / 400

# Chapel of Coal («Часовня Угля»): ascension of heroes and a gift of the day.
ASCENSION = [  # rank: (hero level, chapel level, essence in days of the free player's income)
    (20, 1, 0.5), (30, 6, 1.0), (40, 12, 1.5), (50, 18, 2.0), (60, 24, 3.0),
]
ASCENSION_BONUS = 0.04               # +4% attack and health for every rank
CHAPEL_GIFTS = [  # by local day, in turn: (goods, amount at chapel 1); x(1 + level / 30) x (1 + Spirit / 100)
    ("Essence", 0.08),               # days of the free player's essence
    ("Treats", 5),
    ("Hourglass", 15),               # minutes
]

# Archive of the Guardians («Архив Стражей»): records of the story found by play; reading one takes hours.
ARCHIVE_HOURS = (1.0, 0.5)           # hours: 1 + 0.5 x number of the record
ARCHIVE_PER_LEVEL = 0.05             # speed: 1 + 5% per level + the staff's Intellect / 100
ARCHIVE_INTELLECT_DIVISOR = 100
ARCHIVE_ESSENCE_DAYS = 0.15          # a read record: 0.15 of a day of essence
# (id, title, found when: hearth / missions / hunts / routes / research / ascension, value, text)
ARCHIVE_RECORDS = [
    ("oath", "Клятва Стражей", "hearth", 3,
     "«Пока горит Уголь, мы стоим». Так начиналась присяга ордена. Её вырезали над очагом, и камень помнит её до сих пор."),
    ("convoy", "Последний обоз", "missions", 19,
     "Обоз дошёл до ворот на рассвете. Из сорока повозок уцелели девять, и в каждой ехали дети."),
    ("beast", "Вожак стаи", "hunts", 1,
     "Твари тумана ходят стаями и слушаются вожака. Убей его — и стая рассыплется, как зола."),
    ("route", "Карта первой тропы", "routes", 1,
     "На обороте карты чужой рукой: «Не ходите к мосту после заката. Мост помнит тех, кто упал»."),
    ("coal", "Откуда берётся Уголь", "hearth", 8,
     "Уголь Стражей — не простой камень. В каждом куске тлеет искра первого очага, зажжённого сто зим назад."),
    ("mine", "Копи под Сторожевой", "missions", 38,
     "Штейгер спустился в старый забой за жилой и вернулся другим. Глаза у него стали белыми, как туман."),
    ("ledger", "Книга учёта", "research", 5,
     "Первый хранитель вёл счёт всему: углю, хлебу, людям. На последней странице — только одно слово: «Мало»."),
    ("widow", "Ледяная вдова", "missions", 57,
     "Она звала мужа по имени три ночи подряд. На четвёртую ночь он ответил ей из тумана."),
    ("hunter", "Записки охотника", "hunts", 15,
     "Корневые чудовища медленны, но не спят. Они ждут, пока костёр погаснет."),
    ("bridge", "Мост Пепельных", "missions", 76,
     "Пепельные держали мост, пока последний беженец не перешёл. Потом мост сгорел вместе с ними."),
    ("wander", "Дневник следопыта", "routes", 10,
     "Туман не пуст. В нём есть дороги, деревни и люди, которые давно забыли, что такое тепло."),
    ("court", "Письмо Бледного Двора", "hearth", 15,
     "«Отдайте очаг, и туман пощадит ваших детей». Печать на письме холодная, как лёд, хоть его и держали у огня."),
    ("envoy", "Бледные послы", "missions", 95,
     "Послы не ели и не спали. Их кони стояли у ворот всю ночь, и иней не таял на их гривах."),
    ("ascend", "Обряд возвышения", "ascension", 1,
     "Жрецы Угля учили: сильнее не тот, кто не боится огня, а тот, кого огонь признаёт своим."),
    ("library", "Утерянный трактат", "research", 15,
     "Половина страниц сгорела. На уцелевших — чертёж очага, который греет без угля. Никто не знает, как его построить."),
    ("thicket", "Чаща просыпается", "missions", 114,
     "Деревья на опушке повернулись к крепости. Утром в снегу нашли следы корней, ведущие к стенам."),
    ("siege", "Осада Стражей", "missions", 133,
     "Командор Двора не штурмовал стены. Он ждал, пока холод сделает свою работу."),
    ("silas", "Архивариус Сайлас", "missions", 152,
     "Сайлас переписывал летописи ордена тридцать лет. Никто не заметил, когда он начал писать их по-своему."),
    ("deep", "Глубокие тропы", "routes", 50,
     "За седьмой развилкой тропы уходят под землю. Оттуда тянет теплом — чужим, неправильным теплом."),
    ("heart", "Сердце тумана", "missions", 171,
     "У тумана есть сердце. Оно бьётся медленно, раз в сто лет, и каждый удар гасит один очаг."),
    ("scholar", "Ключ к шифру", "research", 40,
     "Шифр Сайласа — не шифр. Это молитва Матери Чащи, записанная задом наперёд."),
    ("hearth25", "Свет на перевале", "hearth", 25,
     "С высокой башни видно далёкий огонь за перевалом. Значит, мы не последние."),
    ("mother", "Мать Чащи", "missions", 190,
     "Она не злая. Она просто очень замёрзла. И она идёт к нашему огню."),
    ("last", "Последний очаг", "hearth", 30,
     "Через сто лет в этих стенах найдут пепел и одну тлеющую искру. Этого хватит, чтобы начать снова."),
]


def research_nodes():
    """The 60 steps of the tree: (branch, step, name, effect, value, library level, seconds, materials)."""
    nodes = []
    for branch, bname, effects, names in RESEARCH_BRANCHES:
        for t in range(1, RESEARCH_TIERS + 1):
            effect = effects[(t - 1) % len(effects)]
            gate = max(1, 2 * t - 1)
            seconds = int(round(RESEARCH_HOURS * RESEARCH_HOURS_GROWTH ** (t - 1) * 3600))
            hours = RESEARCH_COST_HOURS[0] + RESEARCH_COST_HOURS[1] * t
            lv = max(ROOM_UNLOCK["Library"], gate)
            cost = int(nice(total_income(min(lv, pacing.MAX_LEVEL)) * hours, 2))
            nodes.append({"id": "%s%d" % (branch, t), "branch": branch, "tier": t, "name": names[t - 1],
                          "effect": effect, "value": RESEARCH_VALUES[effect], "gate": gate, "seconds": seconds,
                          "cost": cost})
    return nodes


def train_hours(stat, hall_level):
    return (TRAIN_HOURS[0] + TRAIN_HOURS[1] * stat) * TRAIN_LEVEL_FACTOR ** (hall_level - 1)


def halls_export(result):
    """balance.json: "halls" — the work of the rooms that produce nothing (GDD 6)."""
    return {
        "tavern": {"board": TAVERN_BOARD, "boardExtraAt": list(TAVERN_BOARD_EXTRA_AT), "boardHours": TAVERN_BOARD_HOURS,
                   "rarities": [{"id": i, "name": n, "statBonus": sb, "capBonus": cb, "weight": wt, "hireHours": hh}
                                for i, n, sb, cb, wt, hh in RARITIES],
                   "luckShift": list(RARITY_LUCK_SHIFT), "minCommon": RARITY_MIN_COMMON,
                   "luckPerLevel": TAVERN_LUCK_PER_LEVEL, "luckPerSpirit": TAVERN_LUCK_PER_SPIRIT,
                   "foodHours": HIRE_FOOD_HOURS},
        "infirmary": {"injuryHours": INJURY_HOURS, "illnessHours": ILLNESS_HOURS, "illnessChance": ILLNESS_CHANCE,
                      "sickOutput": SICK_OUTPUT, "perLevel": HEAL_PER_LEVEL, "intellectDivisor": HEAL_INTELLECT_DIVISOR,
                      "blazing": HEAL_BLAZING},
        "library": {"intellectDivisor": RESEARCH_INTELLECT_DIVISOR,
                    "branches": [{"id": b, "name": n} for b, n, *_ in RESEARCH_BRANCHES],
                    "nodes": research_nodes()},
        "training": {"capBase": TRAIN_CAP_BASE, "capPerLevel": TRAIN_CAP_PER_LEVEL, "hoursBase": TRAIN_HOURS[0],
                     "hoursPerPoint": TRAIN_HOURS[1], "levelFactor": TRAIN_LEVEL_FACTOR,
                     "silverHours": TRAIN_SILVER_HOURS[0], "silverStatDivisor": TRAIN_SILVER_HOURS[1]},
        "watchtower": {"scout": [{"what": k, "level": lv} for k, lv in SCOUT_LEVELS],
                       "defensePerLevel": WATCH_DEFENSE_PER_LEVEL, "agilityDivisor": WATCH_AGILITY_DIVISOR},
        "chapel": {"ranks": [{"heroLevel": h, "chapel": c, "essenceDays": e} for h, c, e in ASCENSION],
                   "bonus": ASCENSION_BONUS, "gifts": [{"goods": g, "amount": a} for g, a in CHAPEL_GIFTS]},
        "archive": {"hoursBase": ARCHIVE_HOURS[0], "hoursPerRecord": ARCHIVE_HOURS[1], "perLevel": ARCHIVE_PER_LEVEL,
                    "intellectDivisor": ARCHIVE_INTELLECT_DIVISOR, "essenceDays": ARCHIVE_ESSENCE_DAYS,
                    "records": [{"id": i, "title": t, "kind": k.capitalize(), "need": n, "text": x} for i, t, k, n, x in ARCHIVE_RECORDS]},
    }


def halls_report(result):
    """ECONOMY.md, section 16: the work of the halls."""
    reached = result["reached"]
    level_on = level_by_day(reached)
    year_income = sum(total_income(level_on[d]) * EFFECTIVE_HOURS_PER_DAY for d in range(1, DAYS + 1))
    nodes = research_nodes()
    out = []
    w = out.append
    w("")
    w("## 16. Работа залов: таверна, лазарет, библиотека, тренировка, башня, часовня, архив")
    w("")
    w("Залы без выработки делают свою работу (`Core/GameSession.Rooms.cs`, окно зала). Цены в серебре и материалах — "
      "в часах дохода крепости, поэтому они одинаково ощутимы на любом уровне очага. Кузня и снаряжение — отдельная часть.")
    w("")
    w("**Таверна (очаг %d).** На доске %d беженца из тумана (+1 на %s-м уровне таверны), новая доска каждые %d часов. "
      "С таверной двое беженцев нового уровня очага не входят сами, а ждут на доске (их принимают бесплатно); остальных нанимают и выбирают лучших. Если крепость полна, "
      "слабого жителя можно отпустить в туман. Удача найма — уровень таверны плюс Дух работников / %d; каждое очко удачи "
      "переносит вес с обычных на опытных, редких и легендарных (%s)." % (
          ROOM_UNLOCK["Tavern"], TAVERN_BOARD, "-м и ".join(str(x) for x in TAVERN_BOARD_EXTRA_AT),
          TAVERN_BOARD_HOURS, round(1 / TAVERN_LUCK_PER_SPIRIT),
          " / ".join(dec(x) for x in RARITY_LUCK_SHIFT[1:])))
    w("")
    w("| Редкость | Шанс без удачи | Шанс при удаче 40 | К каждой характеристике | К пределу тренировки | Цена: серебро | Еда |")
    w("|---|---|---|---|---|---|---|")
    for k, (i, n, sb, cb, wt, hh) in enumerate(RARITIES):
        lucky = wt + RARITY_LUCK_SHIFT[k] * 40 if k else max(RARITY_MIN_COMMON, wt - sum(RARITY_LUCK_SHIFT) * 40)
        w("| %s | %d%% | %d%% | +%d | +%d | %s ч дохода | %d |" % (n, wt, round(lucky), sb, cb, dec(hh), HIRE_FOOD_HOURS * (1 + k)))
    w("")
    w("**Лазарет (очаг %d).** Ранение в походе — %d часов без вылазок; «туманная хворь» (шанс %d%% на каждого из отряда "
      "после похода) — %d часов работы вполсилы. Лазарет ускоряет лечение всех: `1 + %d%% × уровень + Ум работников / %d`, "
      "«Пылающий» очаг — ещё ×%s." % (
          ROOM_UNLOCK["Infirmary"], INJURY_HOURS, ILLNESS_CHANCE * 100, ILLNESS_HOURS, HEAL_PER_LEVEL * 100,
          HEAL_INTELLECT_DIVISOR, dec(HEAL_BLAZING, 2)))
    w("")
    w("| Лазарет | нет | 5 | 10 | 20 | 30 |")
    w("|---|---|---|---|---|---|")
    speeds = [1] + [1 + HEAL_PER_LEVEL * lv + 2 * expected_stat(lv) / HEAL_INTELLECT_DIVISOR for lv in (5, 10, 20, 30)]
    w("| Ранение, часов (2 работника) | " + " | ".join(dec(INJURY_HOURS / s) for s in speeds) + " |")
    w("| Хворь, часов | " + " | ".join(dec(ILLNESS_HOURS / s) for s in speeds) + " |")
    w("")
    total_cost = sum(n["cost"] for n in nodes)
    total_hours = sum(n["seconds"] for n in nodes) / 3600
    w("**Библиотека (очаг %d).** Дерево из %d исследований: %d ветви по %d ступеней. Ступень требует предыдущую в своей "
      "ветви и библиотеку уровня `2 × ступень − 1`; идёт одно исследование за раз, Ум работников ускоряет его "
      "(`+ Ум / %d`), алмазы — как стройку. Всё дерево стоит %s материалов (%s%% дохода за год) и %d ч работы." % (
          ROOM_UNLOCK["Library"], len(nodes), len(RESEARCH_BRANCHES), RESEARCH_TIERS, RESEARCH_INTELLECT_DIVISOR,
          num(total_cost), dec(total_cost / year_income * 100), round(total_hours)))
    w("")
    w("| Ветвь | Что даёт (за всю ветвь) |")
    w("|---|---|")
    for branch, bname, effects, names in RESEARCH_BRANCHES:
        parts = []
        for e in effects:
            total = sum(n["value"] for n in nodes if n["branch"] == branch and n["effect"] == e)
            parts.append(RESEARCH_TEXT[e] % (num(total) if e == "RecruitLuck" else dec(total * 100, 1).replace(",0", "")))
        w("| %s | %s |" % (bname, "; ".join(parts)))
    w("")
    w("| Ступень | 1 | 3 | 5 | 8 | 10 | 12 | 15 |")
    w("|---|---|---|---|---|---|---|---|")
    pick = [n for n in nodes if n["branch"] == "Crafts" and n["tier"] in (1, 3, 5, 8, 10, 12, 15)]
    w("| Библиотека | " + " | ".join(str(n["gate"]) for n in pick) + " |")
    w("| Время | " + " | ".join(dec(n["seconds"] / 3600) + " ч" for n in pick) + " |")
    w("| Материалы | " + " | ".join(num(n["cost"]) for n in pick) + " |")
    w("")
    w("**Тренировочный зал (очаг %d).** Житель или герой тренирует выбранную характеристику по очку: `%s + %s × "
      "характеристика` часов, ×%s за каждый уровень зала, серебро — `%s × (1 + характеристика / %d)` часа дохода. "
      "Предел — `%d + %d × уровень зала` плюс бонус редкости, не больше 100. Мест — как работников в зале." % (
          ROOM_UNLOCK["TrainingHall"], dec(TRAIN_HOURS[0]), dec(TRAIN_HOURS[1], 2), dec(TRAIN_LEVEL_FACTOR, 2),
          dec(TRAIN_SILVER_HOURS[0], 3), TRAIN_SILVER_HOURS[1], TRAIN_CAP_BASE, TRAIN_CAP_PER_LEVEL))
    w("")
    w("| Характеристика → +1 | 10 | 20 | 40 | 60 | 90 |")
    w("|---|---|---|---|---|---|")
    for hall in (1, 10, 20, 30):
        w("| Зал %d, часов | %s |" % (hall, " | ".join(dec(train_hours(s, hall)) for s in (10, 20, 40, 60, 90))))
    w("")
    names = {"Surge": "сегодняшний Наплыв: время и волны", "Dens": "логова Охоты на завтра",
             "Spawn": "этажи, откуда выйдут твари", "Route": "какую характеристику проверит следующая вылазка",
             "Forecast": "прогноз: сколько звёзд даст оборона без игрока"}
    w("**Сторожевая башня (очаг %d).** Разведка: %s. Дозорные помогают в Наплыве: защитники +%s%% за уровень башни "
      "и + Ловкость дозорных / %d." % (
          ROOM_UNLOCK["Watchtower"], "; ".join("с %d-го уровня — %s" % (lv, names[k]) for k, lv in SCOUT_LEVELS),
          dec(WATCH_DEFENSE_PER_LEVEL * 100), WATCH_AGILITY_DIVISOR))
    w("")
    w("**Часовня Угля (очаг %d).** Возвышение героя: +%d%% к атаке и здоровью за ранг. Предел уровня героя "
      "возвышение не поднимает, поэтому защиту от раша не ломает; эссенция — та, что копится у героев на пределе." % (
          ROOM_UNLOCK["Chapel"], ASCENSION_BONUS * 100))
    w("")
    w("| Ранг | %s |" % " | ".join(str(r + 1) for r in range(len(ASCENSION))))
    w("|---|%s" % ("---|" * len(ASCENSION)))
    w("| Уровень героя | %s |" % " | ".join(str(h) for h, c, e in ASCENSION))
    w("| Уровень часовни | %s |" % " | ".join(str(c) for h, c, e in ASCENSION))
    w("| Эссенция, дней дохода | %s |" % " | ".join(dec(e) for h, c, e in ASCENSION))
    w("")
    gift_names = {"Essence": "%s дня эссенции", "Treats": "%s лакомств", "Hourglass": "%s минут песочных часов"}
    w("**Дар дня** — раз в сутки, по очереди: %s; ×(1 + уровень / 30) × (1 + Дух работников / 100)." % (
        ", ".join(gift_names[g] % dec(a, 2).replace(",00", "") for g, a in CHAPEL_GIFTS)))
    w("")
    kinds = {"hearth": "очаг", "missions": "боёв кампании", "hunts": "побед на Охоте", "routes": "вылазок",
             "research": "исследований", "ascension": "возвышений"}
    w("**Архив Стражей (очаг %d).** Сюжетных записей — %d, они находятся игрой (очаг, кампания, Охота, вылазки, исследования, "
      "возвышение); прочесть запись — `%s + %s × номер` часов, быстрее с уровнем архива (+%d%%) и Умом работников "
      "(/ %d). Прочитанная запись даёт %s дня эссенции." % (
          ROOM_UNLOCK["Archive"], len(ARCHIVE_RECORDS), dec(ARCHIVE_HOURS[0]), dec(ARCHIVE_HOURS[1]),
          ARCHIVE_PER_LEVEL * 100, ARCHIVE_INTELLECT_DIVISOR, dec(ARCHIVE_ESSENCE_DAYS, 2)))
    w("")
    w("| Запись | Находится |")
    w("|---|---|")
    for i, t, k, n, x in ARCHIVE_RECORDS:
        w("| %s | %s %d |" % (t, kinds[k], n))
    w("")
    w("Модель года (разделы 1–13) не учитывает бонусы исследований и возвышения: они растут вместе с тратами "
      "(исследования и тренировка — доли трат раздела 4) и не больше точности попадания в целевые дни.")
    return out


def main():
    result = solve()
    text, problems = report(result)
    with open(OUT, "w", encoding="utf-8") as f:
        f.write(text)
    with open(os.path.join(os.path.dirname(OUT), "pacing_table.md"), "w", encoding="utf-8") as f:
        f.write("# Уровни очага\n\nСобрано `hearth/tools/economy.py` для бесплатного игрока с ускорениями и "
                "четвёртым строителем.\n\n" + "\n".join(pacing.table(result["rows"], result["reached"])) + "\n")
    export_balance(result)
    print(text)
    if problems:
        sys.exit(1)


if __name__ == "__main__":
    main()
