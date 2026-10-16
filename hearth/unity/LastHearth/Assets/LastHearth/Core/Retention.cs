using System;
using System.Collections.Generic;

namespace LastHearth.Core
{
    /// <summary>What a gift of the login calendar or of the season track is.</summary>
    public enum GiftKind
    {
        Sparks,
        /// <summary>Pet treats (GDD 8.1).</summary>
        Treats,
        /// <summary>Minutes of the free hourglass.</summary>
        Hourglass,
        /// <summary>Days of the free player's essence at the current hearth level.</summary>
        Essence,
        /// <summary>A decoration of the catalogue (GameSession.Decor.cs): cosmetic only.</summary>
        Decor,
    }

    /// <summary>One gift of the calendar or of the season (from the economy model, hearth/tools/economy.py).</summary>
    public sealed class GiftData
    {
        public GiftKind Kind;
        public double Amount;
        public string Decor = "";
    }

    /// <summary>MODES.md, 7: the 6-week season, a free track of 30 steps for season points.</summary>
    public sealed class SeasonData
    {
        public int Days;
        public int Steps;
        public int StepXp;
        /// <summary>GDD 13: the premium track is ready in the code and switched off.</summary>
        public bool Premium;
        public List<GiftData> Track = new List<GiftData>();
        public List<GiftData> PremiumTrack = new List<GiftData>();
        // season points for ordinary play
        public int TaskXp;
        public int DailyXp;
        public int CalendarXp;
        public int SurgeXp;
        public int SurgeAutoXp;
        public int HuntXp;
        public int EventXp;
    }

    /// <summary>EVENTS.md, 11: «Тёплая неделя» — Surges in a row without a defeat give extra output for a while.</summary>
    public sealed class WarmWeekData
    {
        public int Surges;
        public double Output;
        public int Hours;
    }

    /// <summary>ACTIVITIES.md, 3: the monster of the week on the Hunt (0 = Monday ... 6 = Sunday).</summary>
    public sealed class WeeklyMonsterData
    {
        public int Weekday = 6;
        public double HpMult = 1;
        public double AttackMult = 1;
        public double CoreMult = 1;
    }

    /// <summary>Saved state of the calendar, the season and the small weekly bonuses.</summary>
    [Serializable]
    public sealed class RetentionState
    {
        /// <summary>False in a save made before the calendar had to be claimed: GameSession upgrades it once.</summary>
        public bool Ready;

        /// <summary>Calendar gifts taken; State.LoginCount days were opened, so LoginCount - CalendarClaims wait.</summary>
        public int CalendarClaims;

        // the season (local days)
        public int SeasonNo;
        public long SeasonStart = -1;
        public int SeasonXp;
        /// <summary>Steps (0-based) whose gift is taken.</summary>
        public List<int> SeasonClaimed = new List<int>();
        /// <summary>Thresholds of each event run already counted for season points ("Id@Start").</summary>
        public Dictionary<string, int> EventCredit = new Dictionary<string, int>();
        /// <summary>Gifts given at the end of the last season because they were not taken.</summary>
        public int LastSeasonAutoGifts;

        // «Тёплая неделя»
        public int SurgeStreak;
        public long WarmUntil;

        /// <summary>The week (GameSession.WeekKey) whose monster of the week has been beaten.</summary>
        public long WeeklyWonWeek = -1;

        /// <summary>Times of the local notifications planned so far (NotificationPlanner): today's past ones count to the limit.</summary>
        public List<long> NotifiedAt = new List<long>();
    }

    /// <summary>GDD 9.3: «Пока вас не было» — what happened while the game was closed.</summary>
    public sealed class OfflineSummary
    {
        public long From;
        public long To;
        public long Seconds => To - From;

        /// <summary>What the rooms produced into their stock, by resource.</summary>
        public ResourceBag Produced = new ResourceBag();
        /// <summary>Roughly what did not fit: the rooms were full (10 hours of stock).</summary>
        public ResourceBag Lost = new ResourceBag();
        /// <summary>Rooms whose stock is full now: collect them.</summary>
        public int FullRooms;
        /// <summary>Coal the hearth burnt and food the dwellers ate.</summary>
        public long CoalBurnt;
        public long FoodEaten;
        public bool OutOfCoal;
        /// <summary>Resources that came in by themselves (the Surge, the arena, events), and rewards waiting.</summary>
        public ResourceBag Earned = new ResourceBag();

        public List<string> Builds = new List<string>();
        public List<string> Fights = new List<string>();
        public List<string> Births = new List<string>();
        public List<string> Routes = new List<string>();
        public List<string> EventsStarted = new List<string>();
        public List<string> EventsEnded = new List<string>();
        public List<string> Other = new List<string>();

        public bool Empty =>
            Produced.IsEmpty() && Earned.IsEmpty() && CoalBurnt == 0 && FoodEaten == 0 && Builds.Count == 0 && Fights.Count == 0 &&
            Births.Count == 0 && Routes.Count == 0 && EventsStarted.Count == 0 && EventsEnded.Count == 0 && Other.Count == 0;

        /// <summary>Shown once on the start after at least GameSession.AwaySummaryMinutes away.</summary>
        public bool Worth => Seconds >= GameSession.AwaySummaryMinutes * 60L && !Empty;
    }

    public enum NotificationKind
    {
        BuildDone,
        RouteBack,
        EventStart,
        SurgeSoon,
        StorageFull,
    }

    /// <summary>A local notification to show at a moment (Unix seconds).</summary>
    public sealed class PlannedNotification
    {
        public long At;
        public NotificationKind Kind;
        public string Title = "";
        public string Text = "";
    }

    /// <summary>
    /// Where the planned notifications go. The game uses NoNotifications until the Unity Mobile
    /// Notifications package (com.unity.mobile.notifications) is added to Packages/manifest.json; then a
    /// small class calling AndroidNotificationCenter / iOSNotificationCenter implements this.
    /// </summary>
    public interface INotificationSink
    {
        void CancelAll();
        void Schedule(PlannedNotification n);
    }

    /// <summary>Notifications switched off: the planner still runs, nothing is shown.</summary>
    public sealed class NoNotifications : INotificationSink
    {
        public readonly List<PlannedNotification> Scheduled = new List<PlannedNotification>();

        public void CancelAll() => Scheduled.Clear();

        public void Schedule(PlannedNotification n) => Scheduled.Add(n);
    }
}
