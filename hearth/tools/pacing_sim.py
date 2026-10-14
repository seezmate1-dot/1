"""Progression pacing for "Last Hearth" (free player, no purchases).

The hearth level (1..30) is the spine of the game: it caps every room, unlocks floors,
rooms, dwellers and campaign regions (GDD 9). A level-up needs two things:
  * builder work: every room raised to the new cap, then the hearth itself;
  * materials (silver and iron), earned by production, battles and expeditions.

Early on builders are the gate (short timers, a lot to do), later materials are the gate
(timers stay humane: the hearth never takes more than 3 days, a room never more than 1 day).

The script replays a free player minute by minute for a year:
  * day 1: plays a lot; later 4 sessions a day of 12 minutes, asleep 23:00-07:30;
  * builders can be restarted only while the game is open;
  * production accumulates offline, capped at 10 hours (GDD 9.3).
It prints the tables used in docs/GDD.md (section 9) and checks the targets.
tools/economy.py reruns the same calibration with the player's real habits (hourglasses, diamonds,
the 4th builder) and writes docs/pacing_table.md and docs/ECONOMY.md.

Run: python hearth/tools/pacing_sim.py
"""
import math

MAX_LEVEL = 30
# Day on which a free player should reach each hearth level (GDD 9.1).
# Genre-style curve (Whiteout Survival, Clash of Clans): the first two weeks are the hook with fast
# growth, then timers stretch and planning starts (docs/RESEARCH.md).
TARGETS = {1: 0.0, 5: 0.015, 8: 0.1, 10: 1.3, 13: 5.0, 15: 10.0, 18: 25.0, 21: 55.0, 23: 85.0,
           25: 130.0, 26: 165.0, 28: 250.0, 30: 365.0}

SESSIONS_DAY1 = [(8.0, 1.5), (12.5, 0.5), (17.0, 0.5), (20.0, 1.0)]  # (start hour, length in hours)
SESSIONS = [(8.0, 0.2), (12.5, 0.2), (17.0, 0.2), (21.5, 0.2)]
OFFLINE_CAP_H = 10.0
MAX_HEARTH_TIMER_H = 72.0
MAX_ROOM_TIMER_H = 24.0
STEP_H = 1.0 / 60.0  # one minute
FREE_FINISH_H = 5.0 / 60.0  # the last 5 minutes of any job are finished for free (while online)
STARTER_MATERIALS = 4000.0  # tutorial and chapter quests of the first day, claimed when needed
MAX_PRICE_STEP = 3.0


def builders(level):
    """Builders while working towards the next level (GDD 9.2): 2nd in the first session, 3rd at hearth 15."""
    if level < 3:
        return 1
    if level < 15:
        return 2
    return 3


def rooms_at(level):
    """Rooms raised to the new cap at each hearth level (GDD 6: 22 rooms in the full fortress)."""
    return min(3 + level, 22)


def income(level):
    """Materials per hour that go to building (production, battles, expeditions, events)."""
    return 100.0 * 1.2 ** (level - 1)


BUILD_SHARE = 0.80   # building takes 80% of all materials, the rest goes to gear and dwellers
STORAGE_MARGIN = 1.05  # a fully upgraded warehouse holds the hearth price plus 5%
MIN_STORAGE = 500      # the first-day levels (tutorial) are not limited by the warehouse


def hearth_job(row):
    """The most expensive single job of a level: the hearth costs three room jobs."""
    return row["cost"] / (row["rooms"] + 3) * 3


def storage_cap(row):
    """Anti-rush (GDD 9.5): the warehouse at its cap for this hearth level holds the hearth price
    plus 5%, so the warehouse of the previous level is too small for it. Production that does not
    fit waits in the rooms (up to OFFLINE_CAP_H hours of output), nothing is lost before that."""
    return max(STORAGE_MARGIN * hearth_job(row), MIN_STORAGE)


def target_day(level):
    keys = sorted(TARGETS)
    if level in TARGETS:
        return TARGETS[level]
    for a, b in zip(keys, keys[1:]):
        if a < level < b:
            da, db = max(TARGETS[a], 0.003), TARGETS[b]  # level 1 counts as the 4th minute of play
            t = (level - a) / (b - a)
            return math.exp(math.log(da) + (math.log(db) - math.log(da)) * t)
    raise ValueError(level)


def round_timer(hours):
    minutes = hours * 60
    if minutes < 60:
        return min([1 / 6, 0.5, 1, 2, 3, 5, 10, 15, 20, 30, 45, 60], key=lambda s: abs(s - minutes)) / 60.0
    if hours < 24:
        return round(hours * 2) / 2.0
    return float(round(hours))


# The first session is fixed by hand like in Whiteout Survival (6 s, 1 min, 3 min, 10 min...):
# hearth and room timers in hours for levels 1-7; the rest is calibrated (docs/RESEARCH.md).
FIXED_EARLY = {1: (10 / 3600, 10 / 3600), 2: (1 / 60, 0.5 / 60), 3: (3 / 60, 1 / 60), 4: (5 / 60, 2 / 60),
               5: (10 / 60, 3 / 60), 6: (20 / 60, 5 / 60), 7: (45 / 60, 10 / 60)}
BUILDER_GATED = range(7, 19)  # hearth levels where builders, not materials, hold the player back


def design():
    """Timers from the early window (builder-gated), materials from the whole window."""
    rows = []
    for level in range(1, MAX_LEVEL):
        window_h = (target_day(level + 1) - target_day(level)) * 24.0
        # Players are online ~7% of the day, so short early timers are tuned to the play time.
        played_h = window_h * (0.9 if target_day(level + 1) <= 0.07 else 0.7)
        work_h = played_h * builders(level)
        hearth_h = min(work_h / 3.0, MAX_HEARTH_TIMER_H)
        room_h = min((work_h - work_h / 3.0) / rooms_at(level), MAX_ROOM_TIMER_H)
        # Mid game the builders are the gate, so materials are left a quarter of slack:
        # that is what makes the 4th builder and speed-ups worth having (GDD 9.4).
        slack = 0.65 if level in BUILDER_GATED else 0.8
        cost = income(level) * window_h * slack
        if level in FIXED_EARLY:
            hearth_h, room_h = FIXED_EARLY[level]
        rows.append({"level": level, "hearth_h": hearth_h, "room_h": room_h,
                     "rooms": rooms_at(level), "cost": cost})
    return rows


def online(t):
    day, hour = divmod(t, 24.0)
    plan = SESSIONS_DAY1 if day == 0 else SESSIONS
    return any(start <= hour < start + length for start, length in plan)


QUEUE_PER_BUILDER = 3  # GDD 9.2: each builder takes up to three jobs in a row


def simulate(rows, extra_builder_day=None, income_mult=None, speedup_minutes=None):
    """Minute-by-minute replay. Jobs are queued only while the player is online.

    Optional habits of the real free player (tools/economy.py fills them in):
      extra_builder_day  day the 4th builder is bought with diamonds;
      income_mult(day)   extra materials from diamond resource packs (1.0 = none);
      speedup_minutes(day) free hourglasses and diamond speed-ups gained that day.
    """
    t = 8.0
    level = 1
    materials = 0.0
    banked = 0.0  # offline production waiting to be collected
    pool = 0.0    # speed-up minutes waiting to be used
    starter = STARTER_MATERIALS  # quest rewards: claimed into the warehouse when there is room
    reached = {1: 0.0}

    def jobs_for(lv):
        row = rows[lv - 1]
        per_job = row["cost"] / (row["rooms"] + 3)  # the hearth job costs three room jobs
        return [("room", row["room_h"], per_job)] * row["rooms"] + [("hearth", row["hearth_h"], per_job * 3)]

    def builder_count(lv, now):
        extra = 1 if extra_builder_day is not None and now >= 8.0 + extra_builder_day * 24 else 0
        return builders(lv) + extra

    jobs = jobs_for(level)
    queues = [[] for _ in range(builder_count(level, t))]  # end times of queued jobs per builder
    hearth_end = None
    hearth_q = None
    day_seen = -1
    while level < MAX_LEVEL and t < 24 * 420:
        day = int((t - 8.0) // 24) + 1
        if day != day_seen:
            day_seen = day
            if speedup_minutes is not None:
                pool += speedup_minutes(day)
            while len(queues) < builder_count(level, t):
                queues.append([])
        rate = income(level) * (income_mult(day) if income_mult is not None else 1.0)
        # Production waits in the rooms (up to 10 hours of output) until it is collected.
        banked = min(banked + rate * STEP_H, rate * OFFLINE_CAP_H)
        if online(t):
            moved = min(banked, max(storage_cap(rows[level - 1]) - materials, 0.0))
            materials += moved
            banked -= moved
            claim = min(starter, max(storage_cap(rows[level - 1]) - materials, 0.0))
            materials += claim
            starter -= claim
            progress = True
            while jobs and progress:
                progress = False
                kind, hours, cost = jobs[0]
                if materials < cost:
                    break
                for qi, q in enumerate(queues):
                    q[:] = [e for e in q if e > t]
                    if len(q) >= QUEUE_PER_BUILDER:
                        continue
                    begin = max([t] + q)
                    if kind == "hearth":
                        # The hearth starts after every room of this level is finished.
                        begin = max([begin] + [e for other in queues for e in other])
                        hearth_end = begin + hours
                        hearth_q = qi
                    q.append(begin + hours)
                    materials -= cost
                    # Collect again right away: room stock refills the warehouse within the session.
                    moved = min(banked, max(storage_cap(rows[level - 1]) - materials, 0.0))
                    materials += moved
                    banked -= moved
                    claim = min(starter, max(storage_cap(rows[level - 1]) - materials, 0.0))
                    materials += claim
                    starter -= claim
                    jobs.pop(0)
                    progress = True
                    break
            for q in queues:
                # Free finish: a job with 5 minutes or less left is done with one tap.
                if q and q[0] > t and q[0] - t <= FREE_FINISH_H:
                    shift = q[0] - t
                    if hearth_end is not None and queues.index(q) == hearth_q:
                        hearth_end -= shift
                    q[:] = [e - shift for e in q]
            if pool > 0:
                # Speed up the hearth first, otherwise the job that ends soonest.
                live = [qi for qi, q in enumerate(queues) if q and q[0] > t]
                if live:
                    qi = hearth_q if hearth_end is not None and hearth_q in live else min(live, key=lambda k: queues[k][0])
                    q = queues[qi]
                    shift = min(pool / 60.0, q[0] - t)
                    q[:] = [e - shift for e in q]
                    if hearth_end is not None and qi == hearth_q:
                        hearth_end -= shift
                    pool -= shift * 60.0
        if hearth_end is not None and t >= hearth_end:
            level += 1
            reached[level] = (t - 8.0) / 24.0
            hearth_end = None
            hearth_q = None
            if level < MAX_LEVEL:
                jobs = jobs_for(level)
                queues += [[] for _ in range(builder_count(level, t) - len(queues))]
        t += STEP_H
    return reached


def fmt_h(hours):
    if hours < 1 / 60.0:
        return "%d с" % round(hours * 3600)
    if hours < 1:
        return "%d мин" % round(hours * 60)
    if hours < 24:
        if abs(hours * 2 - round(hours * 2)) < 1e-6:
            return ("%g ч" % hours).replace(".", ",")
        return "%d ч %d мин" % (int(hours), round((hours - int(hours)) * 60))
    days, rest = divmod(round(hours), 24)
    return "%d д %d ч" % (days, rest) if rest else "%d д" % days


def monotone(rows):
    """Each level takes at least as long and costs at least 10% more than the previous one."""
    for prev, row in zip(rows, rows[1:]):
        row["hearth_h"] = max(row["hearth_h"], prev["hearth_h"])
        row["room_h"] = max(row["room_h"], prev["room_h"])
        row["cost"] = max(row["cost"], prev["cost"] * 1.1)
        # The hearth job grows by 10% too, so the previous warehouse is always too small (GDD 9.5).
        row["cost"] = max(row["cost"], 1.1 * hearth_job(prev) * (row["rooms"] + 3) / 3)


def calibrate(rows, rounds=24, **habits):
    """Scales each level's timers and cost until the replayed player meets the target days."""
    for _ in range(rounds):
        reached = simulate(rows, **habits)
        for row in rows:
            lv = row["level"]
            if lv + 1 not in reached:
                continue
            want = target_day(lv + 1) - target_day(lv)
            got = max(reached[lv + 1] - reached[lv], 1e-3)
            k = min(max(want / got, 0.4), 2.5) ** 0.8
            if lv in FIXED_EARLY:
                continue  # first session: hand-made timers, tiny prices
            if lv in BUILDER_GATED:
                # Stretch the timers; only if they hit the caps does the price take the rest.
                before = row["hearth_h"] + row["room_h"] * row["rooms"]
                row["hearth_h"] = min(row["hearth_h"] * k, MAX_HEARTH_TIMER_H)
                row["room_h"] = min(row["room_h"] * k, MAX_ROOM_TIMER_H)
                after = row["hearth_h"] + row["room_h"] * row["rooms"]
                if k < 1:
                    row["cost"] *= k
                elif after < before * k * 0.999:
                    row["cost"] *= k * before / after
            else:
                row["hearth_h"] = min(row["hearth_h"] * k, MAX_HEARTH_TIMER_H)
                row["room_h"] = min(row["room_h"] * k, MAX_ROOM_TIMER_H)
                row["cost"] *= k
        monotone(rows)
    # Prices never jump more than 3x from one level to the next (Whiteout grows 2-4x early, 1.3x late).
    for row, nxt in reversed(list(zip(rows, rows[1:]))):
        row["cost"] = max(row["cost"], nxt["cost"] / MAX_PRICE_STEP)
    for row in rows:
        if row["level"] not in FIXED_EARLY:
            row["hearth_h"] = round_timer(row["hearth_h"])
            row["room_h"] = round_timer(row["room_h"])
    return rows


def table(rows, reached):
    """Markdown table of every hearth level (docs/pacing_table.md)."""
    out = ["| Очаг | Таймер очага | Таймер комнаты | Комнат | Строителей | Материалы на уровень | Цель, день | Симуляция, день |",
           "|---|---|---|---|---|---|---|---|"]
    for row in rows:
        lv = row["level"]
        out.append("| %d → %d | %s | %s | %d | %d | %s | %s | %s |" % (
            lv, lv + 1, fmt_h(row["hearth_h"]), fmt_h(row["room_h"]), row["rooms"], builders(lv),
            "{:,}".format(int(float("%.2g" % row["cost"]))).replace(",", " "),
            ("%.1f" % target_day(lv + 1)).replace(".", ","), ("%.1f" % reached.get(lv + 1, float("nan"))).replace(".", ",")))
    return out


def deviations(reached):
    return [(lv, target_day(lv), reached[lv]) for lv in TARGETS if lv > 1 and lv in reached]


def main():
    rows = calibrate(design())
    reached = simulate(rows)
    print("\n".join(table(rows, reached)))
    print("\nКонтрольные точки (цель → симуляция):")
    for lv, want, got in deviations(reached):
        print("  очаг %2d: день %5.1f → %5.1f (%+.0f%%)" % (lv, want, got, (got - want) / want * 100))


if __name__ == "__main__":
    main()
