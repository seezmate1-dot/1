using System;
using System.Collections.Generic;
using System.Linq;

namespace LastHearth.Core
{
    /// <summary>
    /// EVENTS.md: the event engine. The 28-day calendar starts on a Monday; a newcomer gets a
    /// personal one-day run of each event the day it opens. Points pass ten thresholds that pay
    /// 80% of the event budget of the economy model; the rank among ghost players pays the other 20%
    /// when the player takes it after the end. Thresholds and the rank also bring the event currency
    /// for the event shop, which stays open a day after the end; then the rest turns into silver.
    /// </summary>
    public sealed partial class GameSession
    {
        const long Day = 86400;
        const long MondayEpochDay = 4; // 1970-01-05 was a Monday

        /// <summary>Points a regular player (2-3 sessions a day) reaches: all ten thresholds.</summary>
        public static readonly Dictionary<string, double> EventTarget = new Dictionary<string, double>
        {
            { "LongNight", 1200 }, { "Portal", 300 }, { "Tournament", 1000 }, { "Treasure", 110 },
            { "Fair", 250 }, { "Siege", 100 }, { "Festival", 2000 },
        };

        /// <summary>What the first place adds to the fortress (cosmetic, EVENTS.md 4-9).</summary>
        public static readonly Dictionary<string, string> EventTrophy = new Dictionary<string, string>
        {
            { "LongNight", "Рамка «Гаситель Ночи»" }, { "Portal", "Косметика отряда «Пламя Портала»" },
            { "Tournament", "Знамя Хранителя очага" }, { "Treasure", "Печать казны Стражей" }, { "Fair", "Флаг каравана" },
            { "Siege", "Знамя «Несломленный»" }, { "Festival", "Праздничный венок" },
        };

        public EventData EventInfo(string id) => Balance.Activities.Events.First(e => e.Id == id);

        long CycleDay(long localDay, int period) => ((localDay - MondayEpochDay) % period + period) % period + 1;

        /// <summary>Is the event on the shared calendar today?</summary>
        public bool OnCalendar(EventData e, long localDay)
        {
            long c = CycleDay(localDay, e.Period);
            return c >= e.First && (c - e.First) % e.Period < e.Days;
        }

        /// <summary>Does a calendar run of the event begin on this local day?</summary>
        public bool StartsOn(EventData e, long localDay) => CycleDay(localDay, e.Period) == e.First;

        /// <summary>Start (UTC seconds) of the next calendar run that begins after <paramref name="now"/>.</summary>
        public long NextStart(EventData e, long now)
        {
            for (long d = LocalDay(now) + 1; d <= LocalDay(now) + e.Period + 1; d++)
            {
                if (StartsOn(e, d))
                    return d * Day - State.UtcOffsetSeconds;
            }
            return long.MaxValue;
        }

        /// <summary>EVENTS.md 1: the announcement, a day ahead: the open events that start within 24 hours.</summary>
        public List<(EventData e, long start)> Upcoming(long now, long within = Day) =>
            Balance.Activities.Events
                .Where(e => State.HearthLevel >= e.Unlock)
                .Select(e => (e, start: NextStart(e, now)))
                .Where(x => x.start > now && x.start - now <= within && ActiveRun(x.e.Id, x.start) == null)
                .OrderBy(x => x.start).ToList();

        public EventRun ActiveRun(string id, long now) =>
            State.Events.FirstOrDefault(r => r.Id == id && !r.Closed && r.Start <= now && now < r.End);

        public List<EventRun> ActiveRuns(long now) => State.Events.Where(r => !r.Closed && r.Start <= now && now < r.End).ToList();

        /// <summary>Ended runs whose shop is still open (a day after the end) or whose reward waits to be taken.</summary>
        public List<EventRun> EndedRuns() => State.Events.Where(r => r.Closed && (!r.Claimed || !r.Burned)).ToList();

        /// <summary>Called by Advance: opens calendar and personal runs, closes the ones that ended.</summary>
        void UpdateEvents(long now)
        {
            if (Combat == null)
                return;
            // saves of the time when the end paid by itself: nothing is left to take
            foreach (var r in State.Events.Where(r => r.Closed && r.Rank == 0))
                r.Claimed = r.Burned = true;
            long day = LocalDay(now);
            foreach (var e in Balance.Activities.Events)
            {
                if (State.HearthLevel < e.Unlock)
                    continue;
                if (OnCalendar(e, day))
                {
                    long c = CycleDay(day, e.Period);
                    long startDay = day - (c - e.First) % e.Period;
                    long start = startDay * Day - State.UtcOffsetSeconds;
                    if (!State.Events.Any(r => r.Id == e.Id && r.Start == start))
                        OpenRun(e, start, start + e.Days * Day, false);
                }
                else if (!State.EventsSeen.Contains(e.Id) && ActiveRun(e.Id, now) == null)
                {
                    OpenRun(e, now, now + Day, true);
                }
            }
            foreach (var run in State.Events.Where(r => !r.Closed && r.End <= now).ToList())
                CloseRun(run);
            if (ActiveRun("LongNight", now) is EventRun night)
                CheckDawn(night, now);
            long burn = Balance.Activities.EventCurrency.BurnHours * 3600L;
            foreach (var run in State.Events.Where(r => r.Closed && !r.Burned && now >= r.End + burn).ToList())
            {
                if (!run.Claimed)
                    ClaimEventReward(run);
                BurnCurrency(run);
            }
            State.Events.RemoveAll(r => r.Closed && r.Burned && r.End < now - 7 * Day);
        }

        void OpenRun(EventData e, long start, long end, bool personal)
        {
            var run = new EventRun
            {
                Id = e.Id, Start = start, End = end, Personal = personal, Seed = State.NextBattleSeed++,
            };
            if (!State.EventsSeen.Contains(e.Id))
                State.EventsSeen.Add(e.Id);
            if (e.Id == "Treasure")
                StartTreasureMap(run);
            if (e.Id == "Siege")
                StartSiege(run);
            State.Events.Add(run);
        }

        /// <summary>Share of the event budget for this run: a personal one-day run gets one day's worth.</summary>
        double RunScale(EventRun run) => run.Personal ? 1.0 / Math.Max(1, EventInfo(run.Id).Days) : 1.0;

        /// <summary>Target points of this run (a personal run: one day's worth).</summary>
        public double RunTarget(EventRun run) => EventTarget[run.Id] * RunScale(run);

        /// <summary>What one share of the budget pays now: Sparks, essence, materials, hourglass seconds.</summary>
        public (long sparks, long essence, long materials, long hourglass) BudgetPart(EventRun run, double share)
        {
            var e = EventInfo(run.Id);
            int L = State.HearthLevel;
            share *= RunScale(run);
            return ((long)Math.Round(e.Sparks * share), (long)Math.Round(Balance.Activities.EssenceByLevel[L - 1] * e.EssenceDays * share),
                    (long)Math.Round(IncomePerHour() * e.MaterialHours * share), (long)Math.Round(e.HourglassMinutes * 60 * share));
        }

        void PayBudget(EventRun run, double share)
        {
            var (sparks, essence, materials, hourglass) = BudgetPart(run, share);
            State.Wallet[ResourceType.Sparks] += sparks;
            run.SparksWon += sparks;
            State.Wallet[ResourceType.Essence] += essence;
            State.Rewards.Add(ResourceBag.Materials(materials, Balance.SilverShare));
            State.HourglassSeconds += hourglass;
            ClaimRewards();
        }

        /// <summary>The event currency a threshold brings (a personal run: its share, at least 1).</summary>
        public long CurrencyPerThreshold(EventRun run) =>
            Math.Max(1, (long)Math.Round(Balance.Activities.EventCurrency.PerThreshold * RunScale(run)));

        /// <summary>Adds points; every tenth of the target pays 8% of the budget and some event currency.</summary>
        public void AddPoints(EventRun run, double points)
        {
            if (run == null || run.Closed || points <= 0)
                return;
            run.Points += points;
            double target = RunTarget(run);
            int reached = Math.Min(10, (int)Math.Floor(run.Points / (target / 10)));
            while (run.Thresholds < reached)
            {
                run.Thresholds++;
                PayBudget(run, 0.08);
                Purse(run) += CurrencyPerThreshold(run);
                EventItem(run, run.Thresholds); // thresholds 5 and 10 bring an item (GameSession.Gear.cs)
                if (run.Id == "Fair" && run.Thresholds == 10 && !run.Wanderer)
                {
                    run.Wanderer = true;
                    Wanderer();
                }
            }
        }

        /// <summary>Players in the rating group of the event (ghosts until the server exists).</summary>
        public int GroupSize(string id) => Math.Max(2, EventInfo(id).Group);

        /// <summary>Points of the ghost players of the group, from the best: a quarter of the target to 1.85 targets.</summary>
        public List<double> Ghosts(EventRun run)
        {
            int n = GroupSize(run.Id) - 1;
            double target = RunTarget(run);
            return Enumerable.Range(0, n).Select(i => target * (0.25 + 1.6 * Math.Pow((n - 1 - i) / (double)Math.Max(1, n - 1), 1.6))).ToList();
        }

        /// <summary>The rank among the ghost players of the same hearth level (until the server exists).</summary>
        public int EventRank(EventRun run) => Ghosts(run).Count(g => g > run.Points) + 1;

        /// <summary>Share of the budget the place pays: 1st 20%, 2-5 14%, 6-20 8%, the rest 4%.</summary>
        public static double RankShare(int rank) => rank == 1 ? 0.20 : rank <= 5 ? 0.14 : rank <= 20 ? 0.08 : 0.04;

        public long RankCurrency(EventRun run, int rank)
        {
            var r = Balance.Activities.EventCurrency.Rank;
            int i = rank == 1 ? 0 : rank <= 5 ? 1 : rank <= 20 ? 2 : 3;
            return r.Length == 0 ? 0 : Math.Max(1, (long)Math.Round(r[Math.Min(i, r.Length - 1)] * RunScale(run)));
        }

        void CloseRun(EventRun run)
        {
            if (run.Id == "Siege")
                SettleSiege(run, run.End); // windows missed offline count before the end
            if (run.Id == "LongNight")
                CheckDawn(run, run.End);
            run.Closed = true;
            run.Rank = EventRank(run);
            run.LastResult = string.Format("{0}: {1:0} очков, место {2} из {3}", EventInfo(run.Id).Name, run.Points, run.Rank, GroupSize(run.Id));
            if (run.Id == "Siege")
                EndSiege(run);
            if (State.EventBattle != null && State.EventBattle.EventId == run.Id)
                State.EventBattle = null; // a battle left open past the end no longer counts
        }

        /// <summary>The reward of the place of an ended event: the rest of the budget, currency, the trophy of the first place.</summary>
        public Fail ClaimEventReward(EventRun run)
        {
            if (run == null || !run.Closed)
                return Fail.NotRunning;
            if (run.Claimed)
                return Fail.AlreadyQueued;
            run.Claimed = true;
            if (run.Points <= 0)
                return Fail.None; // did not take part: nothing to take
            PayBudget(run, RankShare(run.Rank));
            EventRankItem(run, run.Rank); // the top ranks: an epic item (GameSession.Gear.cs)
            if (!run.Burned)
                Purse(run) += RankCurrency(run, run.Rank);
            if (run.Id == "Festival" && FestivalServerGoal(run, run.End) >= 1 && !run.Burned)
                Purse(run) += 30; // the server goal was reached: everybody's bonus chest
            if (run.Rank == 1 && !run.Personal && EventTrophy.TryGetValue(run.Id, out string trophy) && !State.Trophies.Contains(trophy))
                State.Trophies.Add(trophy);
            return Fail.None;
        }

        // ------------------------------------------------------------------ currency and shop

        /// <summary>The run's currency; the Siege keeps its «Камень застав» in Stone.</summary>
        public ref long Purse(EventRun run)
        {
            if (run.Id == "Siege")
                return ref run.Stone;
            return ref run.Currency;
        }

        /// <summary>The shop of a run is open while it lasts and a day after the end.</summary>
        public bool ShopOpen(EventRun run, long now) =>
            run != null && !run.Burned && run.Start <= now && now < run.End + Balance.Activities.EventCurrency.BurnHours * 3600L;

        public List<EventGoodsData> EventShop(string id) => EventInfo(id).Shop;

        public int BoughtOf(EventRun run, string goods) => run.Bought.TryGetValue(goods, out int n) ? n : 0;

        /// <summary>Buys a line of the event shop for the event currency.</summary>
        public Fail BuyEventGoods(EventRun run, int index, long now)
        {
            Advance(now);
            if (!ShopOpen(run, now) || !State.Events.Contains(run))
                return Fail.Locked;
            var shop = EventShop(run.Id);
            if (index < 0 || index >= shop.Count)
                return Fail.NotFound;
            var g = shop[index];
            if (BoughtOf(run, g.Goods) >= g.Limit)
                return Fail.LimitReached;
            if (Purse(run) < g.Price)
                return Fail.NotEnough;
            if (g.Goods == "Recruit" && PopulationFull())
                return Fail.RoomFull;
            Purse(run) -= g.Price;
            run.Bought[g.Goods] = BoughtOf(run, g.Goods) + 1;
            switch (g.Goods)
            {
                case "Cores": State.Wallet[ResourceType.Cores] += g.Amount; break;
                case "Ember": State.Wallet[ResourceType.Ember] += g.Amount; break;
                case "Blueprints": State.Wallet[ResourceType.Blueprints] += g.Amount; break;
                case "Recruit": RareDweller(); break;
                default:
                    if (g.Goods.StartsWith("Decor:"))
                    {
                        string id = g.Goods.Substring(6);
                        State.DecorOwned[id] = DecorOwned(id) + (int)g.Amount;
                    }
                    break;
            }
            return Fail.None;
        }

        /// <summary>A day after the end the currency left turns into silver (1:5): nothing is lost.</summary>
        void BurnCurrency(EventRun run)
        {
            long left = Purse(run);
            Purse(run) = 0;
            run.Burned = true;
            if (left > 0)
            {
                State.Rewards[ResourceType.Silver] += left * Balance.Activities.EventCurrency.Silver;
                ClaimRewards();
            }
        }

        /// <summary>Counters that reset every local day (attempts, extra purchases, best of the day).</summary>
        void NewDayFor(EventRun run, long now)
        {
            long day = LocalDay(now);
            if (run.Day == day)
                return;
            if (run.Day >= 0 && run.DayBest > 0)
                run.DayBests.Add(run.DayBest);
            if (run.Id == "Treasure" && run.Day >= 0)
                run.Torches = Math.Min(8, run.Torches + 2); // the daily tasks bring two torches
            run.Day = day;
            run.Attempts = 0;
            run.Extra = 0;
            run.DayBest = 0;
            run.Trades = 0;
        }

        /// <summary>A legendary wanderer from the Fair: strong stats, joins even past the dweller cap.</summary>
        void Wanderer()
        {
            int stat = 10 + 3 * State.HearthLevel;
            AddDweller("Странник", new[] { stat, stat, stat, stat, stat });
        }

        /// <summary>A rare dweller of an event (a shop, the treasury chest): better than a refugee, below the Wanderer.</summary>
        void RareDweller()
        {
            int stat = 6 + 2 * State.HearthLevel;
            string[] names = { "Ветеран Стражи", "Хранительница", "Следопыт", "Мастерица" };
            AddDweller(names[State.NextDwellerId % names.Length], new[] { stat, stat, stat, stat, stat });
        }
    }
}
