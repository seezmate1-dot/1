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


def squad_size(level):
    """GDD 10: 5 heroes, slots open at hearth 8, 14, 20, 26."""
    return 5 + sum(1 for gate in (8, 14, 20, 26) if level >= gate)


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

BUILD_SHARE = 0.80              # = MATERIAL_SINKS["Стройка (очаг и залы)"]


def total_income(level):
    """All materials per hour. pacing_sim.income() is the part that goes to building."""
    return pacing.income(level) / BUILD_SHARE


def hearth_job(row):
    """The most expensive single job of a level: the hearth costs three room jobs."""
    return row["cost"] / (row["rooms"] + 3) * 3


def nice(x):
    """Round up to two significant digits."""
    if x <= 0:
        return 0
    p = 10 ** max(int(math.floor(math.log10(x))) - 1, 0)
    return int(math.ceil(x / p) * p)


def storage(row):
    """Warehouse capacity (silver + iron) at this hearth level: 12 hours of income, and always
    10% more than the hearth job, so a player can save up for it."""
    lv = row["level"]
    return nice(max(12 * total_income(lv), 1.1 * hearth_job(row)))


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
    """Campaign, Surge, arena and events (GDD 10, 11)."""
    return 120 * 1.13 ** (level - 1)


def hero_level_cost(hero_level):
    return 40 * 1.11 ** hero_level


def hero_power(hero_level, hearth_level):
    """Level and gear (gear quality follows the hearth level)."""
    return 100 * 1.07 ** hero_level * 1.05 ** hearth_level


def hero_level_cap(hearth_level):
    return 2 * hearth_level + 5


# ---------------------------------------------------------------- campaign (GDD 11)
ACTS = 10
MISSIONS_PER_ACT = 18  # plus a boss
ACT_UNLOCK_HEARTH = [1, 4, 7, 10, 13, 16, 19, 22, 25, 28]

# ---------------------------------------------------------------- diamonds (Iskry)
SPEEDUP_POINTS = [(1, 1), (60, 20), (24 * 60, 260), (7 * 24 * 60, 1000)]  # minutes -> diamonds


def speedup_cost(minutes):
    """Diamonds to finish a timer: log-log interpolation (cheaper per minute for long timers)."""
    if minutes <= 0:
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
ARENA_EXTRA_TICKETS = 20       # an extra arena ticket, bought twice a week (league weekends)
SHOP_REFRESH = 10              # one paid refresh of the daily shop

# ---------------------------------------------------------------- events (docs/EVENTS.md)
# Day 1 of the 28-day cycle is a Monday. At most two events run at once.
# name: (period, first day, days, diamonds won, diamonds a free player spends, essence days,
#        materials in hours of income, hourglass minutes)
EVENTS = {
    "Долгая ночь":       (7, 6, 2, 50, 30, 1.0, 6, 60),
    "Портал в Туман":    (14, 1, 4, 80, 100, 1.5, 12, 120),
    "Турнир Стражей":    (14, 8, 3, 60, 40, 1.0, 8, 60),
    "Кладоискатели":     (28, 10, 5, 150, 60, 1.0, 20, 240),
    "Ярмарка Пепла":     (28, 18, 3, 20, 0, 0.5, 12, 60),
    "Осада застав":      (28, 25, 4, 80, 60, 1.5, 16, 120),
    "Праздник очага":    (91, 60, 10, 200, 100, 2.0, 24, 480),
}
EVENT_UNLOCK_HEARTH = {"Ярмарка Пепла": 4, "Кладоискатели": 5, "Долгая ночь": 6, "Портал в Туман": 8,
                       "Турнир Стражей": 10, "Осада застав": 12, "Праздник очага": 3}

HOURGLASS_DAILY_MINUTES = 30   # daily tasks (3 of 5) give a 30-minute hourglass
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

        # essence -> heroes: level up the weakest hero within the cap
        essence += essence_income_per_day(lv)
        for name, spec in EVENTS.items():
            if lv >= EVENT_UNLOCK_HEARTH[name] and event_starts(name, day):
                essence += essence_income_per_day(lv) * spec[5]
        while len(squad_levels) < squad_size(lv):
            squad_levels.append(max(1, min(squad_levels) - 2) if squad_levels else 1)
        cap = hero_level_cap(lv)
        while True:
            i = min(range(len(squad_levels)), key=lambda k: squad_levels[k])
            if squad_levels[i] >= cap or essence < hero_level_cost(squad_levels[i]):
                break
            essence -= hero_level_cost(squad_levels[i])
            squad_levels[i] += 1
        rec["power"] = sum(hero_power(h, lv) for h in squad_levels)
        rec["squad"] = len(squad_levels)
        rec["hero_level"] = sum(squad_levels) / len(squad_levels)

        # campaign: acts open by hearth level, about 4 new missions a day
        open_acts = sum(1 for gate in ACT_UNLOCK_HEARTH if lv >= gate)
        per_day = 4
        while missions_done < open_acts * (MISSIONS_PER_ACT + 1) and per_day > 0:
            missions_done += 1
            per_day -= 1
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

        # diamonds earned
        daily = {"Поручения дня": DIAMONDS_DAILY_TASKS, "Календарь входа": DIAMONDS_LOGIN_CALENDAR / 28.0}
        ach = min(ACHIEVEMENT_POOL * (0.6 / 30 if day <= 30 else 0.4 / 335), achievements_left)
        achievements_left -= ach
        daily["Достижения"] = ach
        daily["Арена"] = 60 if lv >= 12 and day % 7 == 0 else 0  # weekly league reward
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
            habit = SHOP_REFRESH + (ARENA_EXTRA_TICKETS if lv >= 12 and day % 7 in (6, 0) else 0)
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
            if surplus > 0 and gated[lv]:
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
        "rows": rows, "reached": reached, "days": days, "fourth_builder_day": fourth_builder_day,
        "act_finish_day": act_finish_day, "act_power": act_power, "habits": habits,
        "speed_minutes_total": hourglass_total, "diamond_minutes_total": diamond_minutes_total,
        "builder_minutes_total": builder_minutes_total, "diamonds_left": diamonds,
        "earned_by": earned_by, "spent_by": spent_by, "packs_total": sum(packs.values()), "gated": gated,
    }


def solve(iterations=3):
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
    # 6. campaign within the year
    if ACTS not in result["act_finish_day"]:
        problems.append("кампания не пройдена за год")
    # 7. speed-ups help but do not replace waiting
    share = result["speed_minutes_total"] / max(result["builder_minutes_total"], 1)
    if not 0.08 <= share <= 0.25:
        problems.append("ускорения покрывают %.0f%% стройки (нужно 8-25%%)" % (share * 100))
    # 8. the real player meets the target days
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


PLAYTIME = [  # minutes a day of a typical free player, 4 sessions of 12 minutes
    ("Сбор производства, стройка, расселение жителей", 8),
    ("Кампания: 4 новых боя по 2 минуты", 8),
    ("Арена: 5 боёв (с очага 12)", 5),
    ("Вылазки, таверна, кузня, лазарет", 7),
    ("Наплыв тумана (можно смотреть или пропустить)", 3),
    ("События", 12),
    ("Поручения дня и сюжетные диалоги", 5),
]


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
    w("Ускорения важны с 6-го по 21-й уровень очага, когда игрока держат строители. Позже держат материалы, "
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
    w("Серебро — %d%% материалов, железо — %d%%. **Склад** вмещает не меньше 12 часов дохода и всегда на 10%% больше "
      "цены очага, чтобы на него можно было накопить:" % (SILVER_SHARE * 100, (1 - SILVER_SHARE) * 100))
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
    w("Эссенция в день: `120 × 1,13^(очаг − 1)` из кампании, Наплыва и арены, плюс награды событий. "
      "Уровень героя стоит `40 × 1,11^уровень`, предел уровня героя `2 × очаг + 5`. "
      "Сила героя `100 × 1,07^уровень × 1,05^очаг` (вторая часть — снаряжение, которое растёт с кузней).")
    w("")
    w("## 7. Кампания")
    w("")
    w("10 актов по 18 боёв и боссу. Акт открывается уровнем очага, внутри акта ворота — сила отряда. "
      "**Рекомендуемая сила боя** — сила отряда бесплатного игрока в день, когда он до него доходит, ×0,95:")
    w("")
    w("| Акт | Открывается на очаге | Сила первого боя | Сила босса | Пройден (день) |")
    w("|---|---|---|---|---|")
    for act in range(1, ACTS + 1):
        p = result["act_power"].get(act, [0, 0])
        w("| %d | %d | %s | %s | %s |" % (act, ACT_UNLOCK_HEARTH[act - 1], num(nice(p[0] * 0.95)), num(nice(p[1] * 0.95)),
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
        w("| %s | %s |" % (label, "бесплатно" if minutes <= 1 else speedup_cost(minutes)))
    w("")
    share = result["speed_minutes_total"] / max(result["builder_minutes_total"], 1)
    w("Бесплатные **песочные часы**: %d минут в день за поручения, %d за победу в Наплыве, ещё больше в событиях. "
      "Вместе с ускорениями за алмазы за год это %s часов, или **%d%% всего времени стройки**: ощутимая помощь, "
      "но ожидание остаётся частью игры. Из них за алмазы — %s часов." % (
          HOURGLASS_DAILY_MINUTES, HOURGLASS_SURGE_MINUTES, num(result["speed_minutes_total"] / 60), share * 100,
          num(result["diamond_minutes_total"] / 60)))
    w("")
    w("Правила: ускорение до 1 минуты бесплатно; цена за минуту падает с длиной таймера; час дохода в наборе ресурсов "
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
    w("Первый день длиннее: около 3,5 часов, если игроку нравится (обучение, первые 4 уровня очага, первый босс).")
    w("")
    w("## 11. Проверки модели")
    w("")
    problems = checks(result)
    if problems:
        for p in problems:
            w("- **Проблема:** %s" % p)
    else:
        w("Все проверки пройдены: склад вмещает цену очага и копить на неё не дольше недели; источники и траты "
          "сходятся; угля хватает на Наплыв; четвёртый строитель доступен за 2 месяца, алмазы не копятся; события дают "
          "заявленную долю материалов; кампания проходится за год; ускорения покрывают 8–25% стройки; "
          "игрок попадает в целевые дни с точностью 15%.")
    return "\n".join(out) + "\n", problems


def main():
    result = solve()
    text, problems = report(result)
    with open(OUT, "w", encoding="utf-8") as f:
        f.write(text)
    with open(os.path.join(os.path.dirname(OUT), "pacing_table.md"), "w", encoding="utf-8") as f:
        f.write("# Уровни очага\n\nСобрано `hearth/tools/economy.py` для бесплатного игрока с ускорениями и "
                "четвёртым строителем.\n\n" + "\n".join(pacing.table(result["rows"], result["reached"])) + "\n")
    print(text)
    if problems:
        sys.exit(1)


if __name__ == "__main__":
    main()
