using System;
using System.Collections.Generic;
using System.Linq;

namespace LastHearth.Core
{
    /// <summary>
    /// GDD 12 and MODES.md 7: retention without pressure. The login calendar of 28 gifts (days need not
    /// follow each other, an untaken gift waits) and the 6-week season with a free track of 30 steps for
    /// season points from ordinary play. Gifts not taken by the end of a season are given automatically.
    /// </summary>
    public sealed partial class GameSession
    {
        /// <summary>The state of the calendar and the season; a save from before them is upgraded once.</summary>
        RetentionState Retention
        {
            get
            {
                var r = State.Retention ?? (State.Retention = new RetentionState());
                if (!r.Ready)
                {
                    // the calendar used to pay by itself on every new day: those days are taken
                    r.Ready = true;
                    r.CalendarClaims = State.LoginCount;
                    foreach (var run in State.Events)
                        r.EventCredit[RunKey(run)] = run.Thresholds;
                }
                return r;
            }
        }

        static string RunKey(EventRun run) => run.Id + "@" + run.Start;

        // ------------------------------------------------------------------ gifts

        /// <summary>The amount a gift gives now (essence follows the hearth level).</summary>
        public long GiftAmount(GiftData g)
        {
            if (g.Kind == GiftKind.Essence)
                return Math.Max(1, (long)Math.Round(Balance.Activities.EssenceByLevel[State.HearthLevel - 1] * g.Amount));
            return (long)Math.Round(g.Amount);
        }

        public string GiftText(GiftData g)
        {
            long n = GiftAmount(g);
            switch (g.Kind)
            {
                case GiftKind.Sparks: return n + " искр";
                case GiftKind.Treats: return n + " лакомств";
                case GiftKind.Hourglass: return "Часы " + n + " мин";
                case GiftKind.Essence: return n + " эссенции";
                default: return Decor(g.Decor)?.Name ?? g.Decor;
            }
        }

        void Grant(GiftData g)
        {
            long n = GiftAmount(g);
            switch (g.Kind)
            {
                case GiftKind.Sparks:
                    State.Wallet[ResourceType.Sparks] += n;
                    break;
                case GiftKind.Treats:
                    GrantTreats(n);
                    break;
                case GiftKind.Hourglass:
                    State.HourglassSeconds += n * 60;
                    break;
                case GiftKind.Essence:
                    State.Wallet[ResourceType.Essence] += n;
                    break;
                case GiftKind.Decor:
                    if (Decor(g.Decor) != null)
                        State.DecorOwned[g.Decor] = DecorOwned(g.Decor) + (int)Math.Max(1, n);
                    break;
            }
        }

        // ------------------------------------------------------------------ the login calendar

        public List<GiftData> Calendar => Balance.Activities.Daily.Calendar;

        /// <summary>Days with the game open whose gift is not taken yet (missed days cost nothing).</summary>
        public int CalendarPending => Math.Max(0, State.LoginCount - Retention.CalendarClaims);

        /// <summary>The cell (0-27) of the next gift.</summary>
        public int CalendarIndex => Calendar.Count == 0 ? 0 : Retention.CalendarClaims % Calendar.Count;

        /// <summary>The round of the calendar, from 1: after day 28 it starts again.</summary>
        public int CalendarRound => Calendar.Count == 0 ? 1 : Retention.CalendarClaims / Calendar.Count + 1;

        public Fail ClaimCalendar(long now)
        {
            Advance(now);
            if (Calendar.Count == 0)
                return Fail.Locked;
            if (CalendarPending <= 0)
                return Fail.NotToday;
            var gift = Calendar[CalendarIndex];
            Retention.CalendarClaims++;
            Grant(gift);
            AddSeasonXp(SeasonInfo.CalendarXp);
            return Fail.None;
        }

        // ------------------------------------------------------------------ the season

        public SeasonData SeasonInfo => Balance.Activities.Season;

        /// <summary>The premium track is ready in the code and switched off (GDD 13: «позже и честно»).</summary>
        public bool SeasonPremium => SeasonInfo.Premium && SeasonInfo.PremiumTrack.Count > 0;

        public int SeasonNumber => Math.Max(1, Retention.SeasonNo);

        public int SeasonXp => Retention.SeasonXp;

        /// <summary>Steps reached (their gifts can be taken).</summary>
        public int SeasonStep => SeasonInfo.StepXp <= 0 ? 0 : Math.Min(SeasonInfo.Track.Count, Retention.SeasonXp / SeasonInfo.StepXp);

        /// <summary>Points into the next step.</summary>
        public int SeasonStepXp => SeasonStep >= SeasonInfo.Track.Count || SeasonInfo.StepXp <= 0 ? 0 : Retention.SeasonXp % SeasonInfo.StepXp;

        /// <summary>When the current season ends (Unix seconds, the local midnight).</summary>
        public long SeasonEnds(long now)
        {
            SyncSeason(now);
            return (Retention.SeasonStart + SeasonInfo.Days) * Day - State.UtcOffsetSeconds;
        }

        public bool SeasonClaimed(int step) => Retention.SeasonClaimed.Contains(step);

        public bool SeasonGiftReady(int step) => step >= 0 && step < SeasonStep && !SeasonClaimed(step);

        public int SeasonGiftsReady => Enumerable.Range(0, SeasonStep).Count(SeasonGiftReady);

        public Fail ClaimSeason(int step, long now)
        {
            Advance(now);
            if (step < 0 || step >= SeasonInfo.Track.Count)
                return Fail.NotFound;
            if (SeasonClaimed(step))
                return Fail.AlreadyQueued;
            if (step >= SeasonStep)
                return Fail.RulesNotMet;
            Retention.SeasonClaimed.Add(step);
            Grant(SeasonInfo.Track[step]);
            return Fail.None;
        }

        /// <summary>Takes every reached gift; returns how many.</summary>
        public int ClaimSeasonAll(long now)
        {
            int n = 0;
            for (int step = 0; step < SeasonInfo.Track.Count; step++)
            {
                if (SeasonGiftReady(step) && ClaimSeason(step, now) == Fail.None)
                    n++;
            }
            return n;
        }

        void AddSeasonXp(int xp)
        {
            if (xp <= 0 || SeasonInfo.Days <= 0)
                return;
            RollSeason(LocalDay(State.Time));
            Retention.SeasonXp += xp;
        }

        /// <summary>Called by Advance: the season rolls over, the thresholds of events count as season points.</summary>
        void SyncSeason(long now)
        {
            if (SeasonInfo.Days <= 0)
                return;
            RollSeason(LocalDay(now));
            var r = Retention;
            foreach (var run in State.Events)
            {
                string key = RunKey(run);
                r.EventCredit.TryGetValue(key, out int counted);
                if (run.Thresholds > counted)
                {
                    r.EventCredit[key] = run.Thresholds;
                    AddSeasonXp((run.Thresholds - counted) * SeasonInfo.EventXp);
                }
            }
            if (r.EventCredit.Count > State.Events.Count)
            {
                var alive = new HashSet<string>(State.Events.Select(RunKey));
                foreach (string key in r.EventCredit.Keys.Where(k => !alive.Contains(k)).ToList())
                    r.EventCredit.Remove(key);
            }
        }

        /// <summary>The first season starts on the first day; each lasts SeasonInfo.Days and the next follows at once.
        /// The gifts reached and not taken are given at the end: nothing earned is lost.</summary>
        void RollSeason(long day)
        {
            var r = Retention;
            if (r.SeasonStart < 0)
            {
                r.SeasonStart = day;
                r.SeasonNo = 1;
                return;
            }
            while (day >= r.SeasonStart + SeasonInfo.Days)
            {
                int given = 0;
                for (int step = 0; step < SeasonStep; step++)
                {
                    if (r.SeasonClaimed.Contains(step))
                        continue;
                    Grant(SeasonInfo.Track[step]);
                    given++;
                }
                r.LastSeasonAutoGifts = given;
                r.SeasonNo = Math.Max(1, r.SeasonNo) + 1;
                r.SeasonStart += SeasonInfo.Days;
                r.SeasonXp = 0;
                r.SeasonClaimed.Clear();
            }
        }
    }
}
