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
    "Тренировка жителей": 0.06,
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


KEY_ROOMS_EARLY = ["Спальни", "Кухня", "Угольная шахта", "Казарма", "Рудник", "Кузня", "Лазарет", "Библиотека",
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
    ("Таверна, кузня, лазарет, Сердце очага", 5),
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
    w("## 13. Проверки модели")
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
          "игрок попадает в целевые дни с точностью 15%.")
    return "\n".join(out) + "\n", problems


# ---------------------------------------------------------------- export for the game (Unity)
BALANCE_OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                           "unity", "LastHearth", "Assets", "LastHearth", "Resources", "balance.json")

# GDD 6: room types. name: (enum, unlock hearth, produces, key stat, how many can be built)
ROOM_TYPES = [
    ("Очаг", "Hearth", 1, None, None, 1),
    ("Спальни", "Bedrooms", 1, None, None, 4),
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
WORKERS_PER_ROOM = 2       # one cell holds two workers (merged rooms come later)


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
        "grid": {"floors": 10, "cells": 6, "hearthFloor": 3, "hearthCells": [2, 3]},
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
        "activities": {
            "essenceByLevel": [int(round(essence_income_per_day(lv))) for lv in range(1, 31)],
            "surge": {"unlock": SURGE_UNLOCK, "autoShare": SURGE_AUTO_SHARE, "startHour": 19,
                      "essenceShare": SURGE_ESSENCE_SHARE, "hourglassMinutes": HOURGLASS_SURGE_MINUTES,
                      "emberByLevel": [ember_per_surge(lv) for lv in range(1, 31)]},
            "heart": {"nodeCost": [heart_node_cost(n) for n in range(1, HEART_NODES + 1)],
                      "nodeGate": [heart_node_gate(n) for n in range(1, HEART_NODES + 1)],
                      "perBranch": HEART_NODES // 3, "warmth": 0.03, "coal": 0.03, "output": 0.02, "defense": 0.05},
            "hunt": {"unlock": HUNT_UNLOCK, "tracksPerDay": HUNTS_PER_DAY, "trackHours": 8,
                     "coresByLevel": [cores_per_hunt(lv) for lv in range(1, 31)]},
            "skills": {"max": SKILL_MAX, "heroLevelsPerSkill": 6,
                       "cost": [skill_cost(k) for k in range(1, SKILL_MAX)]},
            "legendary": {"fromHearth": LEGENDARY_FROM, "cores": LEGENDARY_COST},
            "routes": {"unlock": ROUTES_UNLOCK, "squadsByLevel": [routes_per_day(lv) for lv in range(1, 31)],
                       "blueprintsByLevel": [blueprints_per_route(lv) for lv in range(1, 31)],
                       "essenceShare": ROUTE_ESSENCE_SHARE, "materialHours": ROUTE_MATERIAL_HOURS,
                       "incomeByLevel": [int(round(total_income(lv))) for lv in range(1, 31)]},
            "specializations": {"tiers": [list(t) for t in SPEC_TIERS], "ranks": SPEC_RANKS,
                                "ranksFromHearth": SPEC_RANKS_FROM},
            "daily": {"tasksNeeded": 3, "taskSparks": DIAMONDS_DAILY_TASKS, "taskHourglassMinutes": HOURGLASS_DAILY_MINUTES,
                      "calendarSparks": DIAMONDS_LOGIN_CALENDAR, "achievementSparks": ACHIEVEMENT_POOL},
        },
    }
    os.makedirs(os.path.dirname(BALANCE_OUT), exist_ok=True)
    with open(BALANCE_OUT, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=1)
        f.write("\n")


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
