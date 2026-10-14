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

Run: python hearth/tools/pacing_sim.py
"""
import math

MAX_LEVEL = 30
# Day on which a free player should reach each hearth level (GDD 9.1).
TARGETS = {1: 0.0, 4: 1.0, 7: 3.0, 9: 7.0, 12: 14.0, 15: 30.0, 18: 60.0, 21: 90.0, 24: 135.0, 26: 180.0, 28: 260.0, 30: 365.0}

SESSIONS_DAY1 = [(8.0, 1.5), (12.5, 0.5), (17.0, 0.5), (20.0, 1.0)]  # (start hour, length in hours)
SESSIONS = [(8.0, 0.2), (12.5, 0.2), (17.0, 0.2), (21.5, 0.2)]
OFFLINE_CAP_H = 10.0
MAX_HEARTH_TIMER_H = 72.0
MAX_ROOM_TIMER_H = 24.0
STEP_H = 1.0 / 60.0  # one minute


def builders(level):
    """Builders while working towards the next level (GDD 9.2): 2nd from quests, 3rd at hearth 15."""
    if level < 5:
        return 1
    if level < 15:
        return 2
    return 3


def rooms_at(level):
    """Rooms raised to the new cap at each hearth level (GDD 6: 22 rooms in the full fortress)."""
    return min(3 + level, 22)


def income(level):
    """Materials per hour at this hearth level: production, battles and expeditions together."""
    return 100.0 * 1.2 ** (level - 1)


def target_day(level):
    keys = sorted(TARGETS)
    if level in TARGETS:
        return TARGETS[level]
    for a, b in zip(keys, keys[1:]):
        if a < level < b:
            da, db = max(TARGETS[a], 0.04), TARGETS[b]
            t = (level - a) / (b - a)
            return math.exp(math.log(da) + (math.log(db) - math.log(da)) * t)
    raise ValueError(level)


def round_timer(hours):
    minutes = hours * 60
    if minutes < 60:
        return min([1, 2, 3, 5, 10, 15, 20, 30, 45, 60], key=lambda s: abs(s - minutes)) / 60.0
    if hours < 24:
        return round(hours * 2) / 2.0
    return float(round(hours))


def design():
    """Timers from the early window (builder-gated), materials from the whole window."""
    rows = []
    for level in range(1, MAX_LEVEL):
        window_h = (target_day(level + 1) - target_day(level)) * 24.0
        # Players are online ~7% of the day, so short early timers are tuned to the play time.
        played_h = window_h * (0.35 if level < 4 else 0.7)
        work_h = played_h * builders(level)
        hearth_h = min(work_h / 3.0, MAX_HEARTH_TIMER_H)
        room_h = min((work_h - work_h / 3.0) / rooms_at(level), MAX_ROOM_TIMER_H)
        cost = income(level) * window_h * 0.8  # the player earns a little more than needed
        rows.append({"level": level, "hearth_h": hearth_h, "room_h": room_h,
                     "rooms": rooms_at(level), "cost": cost})
    return rows


def online(t):
    day, hour = divmod(t, 24.0)
    plan = SESSIONS_DAY1 if day == 0 else SESSIONS
    return any(start <= hour < start + length for start, length in plan)


QUEUE_PER_BUILDER = 3  # GDD 9.2: each builder takes up to three jobs in a row


def simulate(rows):
    """Minute-by-minute replay. Jobs are queued only while the player is online."""
    t = 8.0
    level = 1
    materials = 0.0
    banked = 0.0  # offline production waiting to be collected
    reached = {1: 0.0}

    def jobs_for(lv):
        row = rows[lv - 1]
        per_job = row["cost"] / (row["rooms"] + 3)  # the hearth job costs three room jobs
        return [("room", row["room_h"], per_job)] * row["rooms"] + [("hearth", row["hearth_h"], per_job * 3)]

    jobs = jobs_for(level)
    queues = [[] for _ in range(builders(level))]  # end times of queued jobs per builder
    hearth_end = None
    while level < MAX_LEVEL and t < 24 * 420:
        rate = income(level)
        if online(t):
            materials += banked + rate * STEP_H
            banked = 0.0
            progress = True
            while jobs and progress:
                progress = False
                kind, hours, cost = jobs[0]
                if materials < cost:
                    break
                for q in queues:
                    q[:] = [e for e in q if e > t]
                    if len(q) >= QUEUE_PER_BUILDER:
                        continue
                    begin = max([t] + q)
                    if kind == "hearth":
                        # The hearth starts after every room of this level is finished.
                        begin = max([begin] + [e for other in queues for e in other])
                        hearth_end = begin + hours
                    q.append(begin + hours)
                    materials -= cost
                    jobs.pop(0)
                    progress = True
                    break
        else:
            banked = min(banked + rate * STEP_H, rate * OFFLINE_CAP_H)
        if hearth_end is not None and t >= hearth_end:
            level += 1
            reached[level] = (t - 8.0) / 24.0
            hearth_end = None
            if level < MAX_LEVEL:
                jobs = jobs_for(level)
                queues += [[] for _ in range(builders(level) - len(queues))]
        t += STEP_H
    return reached


def fmt_h(hours):
    if hours < 1:
        return "%d мин" % round(hours * 60)
    if hours < 24:
        return ("%g ч" % hours).replace(".", ",")
    days, rest = divmod(round(hours), 24)
    return "%d д %d ч" % (days, rest) if rest else "%d д" % days


def monotone(rows):
    """Each level takes at least as long and costs at least 10% more than the previous one."""
    for prev, row in zip(rows, rows[1:]):
        row["hearth_h"] = max(row["hearth_h"], prev["hearth_h"])
        row["room_h"] = max(row["room_h"], prev["room_h"])
        row["cost"] = max(row["cost"], prev["cost"] * 1.1)


def calibrate(rows, rounds=24):
    """Scales each level's timers and cost until the replayed player meets the target days."""
    for _ in range(rounds):
        reached = simulate(rows)
        for row in rows:
            lv = row["level"]
            if lv + 1 not in reached:
                continue
            want = target_day(lv + 1) - target_day(lv)
            got = max(reached[lv + 1] - reached[lv], 1e-3)
            k = min(max(want / got, 0.4), 2.5) ** 0.8
            row["hearth_h"] = min(row["hearth_h"] * k, MAX_HEARTH_TIMER_H)
            row["room_h"] = min(row["room_h"] * k, MAX_ROOM_TIMER_H)
            row["cost"] *= k
        monotone(rows)
    for row in rows:
        row["hearth_h"] = round_timer(row["hearth_h"])
        row["room_h"] = round_timer(row["room_h"])
    return rows


def main():
    rows = calibrate(design())
    reached = simulate(rows)
    print("| Очаг | Таймер очага | Таймер комнаты | Комнат | Строителей | Материалы на уровень | Цель, день | Симуляция, день |")
    print("|---|---|---|---|---|---|---|---|")
    for row in rows:
        lv = row["level"]
        print("| %d → %d | %s | %s | %d | %d | %s | %s | %s |" % (
            lv, lv + 1, fmt_h(row["hearth_h"]), fmt_h(row["room_h"]), row["rooms"], builders(lv),
            "{:,}".format(int(round(row["cost"], -2))).replace(",", " "),
            ("%.1f" % target_day(lv + 1)).replace(".", ","), ("%.1f" % reached.get(lv + 1, float("nan"))).replace(".", ",")))
    deviations = [(lv, reached[lv], target_day(lv)) for lv in TARGETS if lv > 1 and lv in reached]
    print("\nКонтрольные точки (цель → симуляция):")
    for lv, got, want in deviations:
        print("  очаг %2d: день %5.1f → %5.1f (%+.0f%%)" % (lv, want, got, (got - want) / want * 100))


if __name__ == "__main__":
    main()
