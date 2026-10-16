using System;
using System.Collections.Generic;
using System.Linq;

namespace LastHearth.Core
{
    /// <summary>
    /// EVENTS.md: the event engine. The 28-day calendar starts on a Monday; a newcomer gets a
    /// personal one-day run of each event the day it opens. Points pass ten thresholds that pay
    /// 80% of the event budget of the economy model; the rank among 49 ghosts pays the other 20%.
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

        public EventData EventInfo(string id) => Balance.Activities.Events.First(e => e.Id == id);

        long CycleDay(long localDay, int period) => ((localDay - MondayEpochDay) % period + period) % period + 1;

        /// <summary>Is the event on the shared calendar today?</summary>
        public bool OnCalendar(EventData e, long localDay)
        {
            long c = CycleDay(localDay, e.Period);
            return c >= e.First && (c - e.First) % e.Period < e.Days;
        }

        public EventRun ActiveRun(string id, long now) =>
            State.Events.FirstOrDefault(r => r.Id == id && !r.Closed && r.Start <= now && now < r.End);

        public List<EventRun> ActiveRuns(long now) => State.Events.Where(r => !r.Closed && r.Start <= now && now < r.End).ToList();

        /// <summary>Called by Advance: opens calendar and personal runs, closes the ones that ended.</summary>
        void UpdateEvents(long now)
        {
            if (Combat == null)
                return;
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
            State.Events.RemoveAll(r => r.Closed && r.End < now - 7 * Day);
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

        void PayBudget(EventRun run, double share)
        {
            var e = EventInfo(run.Id);
            int L = State.HearthLevel;
            share *= RunScale(run);
            State.Wallet[ResourceType.Sparks] += (long)Math.Round(e.Sparks * share);
            State.Wallet[ResourceType.Essence] += (long)Math.Round(Balance.Activities.EssenceByLevel[L - 1] * e.EssenceDays * share);
            State.Rewards.Add(ResourceBag.Materials((long)Math.Round(IncomePerHour() * e.MaterialHours * share), Balance.SilverShare));
            State.HourglassSeconds += (long)Math.Round(e.HourglassMinutes * 60 * share);
            ClaimRewards();
        }

        /// <summary>Adds points; every tenth of the target pays 8% of the budget.</summary>
        public void AddPoints(EventRun run, double points)
        {
            if (run == null || run.Closed || points <= 0)
                return;
            run.Points += points;
            double target = EventTarget[run.Id] * RunScale(run);
            int reached = Math.Min(10, (int)Math.Floor(run.Points / (target / 10)));
            while (run.Thresholds < reached)
            {
                run.Thresholds++;
                PayBudget(run, 0.08);
                EventItem(run, run.Thresholds); // thresholds 5 and 10 bring an item (GameSession.Gear.cs)
                if (run.Id == "Fair" && run.Thresholds == 10 && !run.Wanderer)
                {
                    run.Wanderer = true;
                    Wanderer();
                }
            }
        }

        /// <summary>The rank among 49 ghosts of the same hearth level (until the server exists).</summary>
        public int EventRank(EventRun run)
        {
            double target = EventTarget[run.Id] * RunScale(run);
            int better = 0;
            for (int i = 0; i < 49; i++)
            {
                double ghost = target * (0.25 + 1.6 * Math.Pow(i / 48.0, 1.6));
                if (ghost > run.Points)
                    better++;
            }
            return better + 1;
        }

        void CloseRun(EventRun run)
        {
            run.Closed = true;
            int rank = EventRank(run);
            double share = rank == 1 ? 0.20 : rank <= 5 ? 0.14 : rank <= 20 ? 0.08 : 0.04;
            if (run.Points > 0)
                PayBudget(run, share);
            EventRankItem(run, rank); // the top ranks: an epic item
            run.LastResult = string.Format("{0}: {1:0} очков, место {2} из 50", EventInfo(run.Id).Name, run.Points, rank);
            if (run.Id == "Siege")
                EndSiege(run);
        }

        /// <summary>Counters that reset every local day (attempts, extra purchases, best of the day).</summary>
        void NewDayFor(EventRun run, long now)
        {
            long day = LocalDay(now);
            if (run.Day == day)
                return;
            if (run.Day >= 0 && run.DayBest > 0)
                run.DayBests.Add(run.DayBest);
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
    }
}
