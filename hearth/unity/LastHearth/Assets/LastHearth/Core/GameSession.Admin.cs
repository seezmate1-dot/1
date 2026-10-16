using System;
using System.Collections.Generic;
using System.Linq;
using LastHearth.Core.Combat;
using LastHearth.Core.Surge;

namespace LastHearth.Core
{
    /// <summary>
    /// The developer's menu (GameRunner.Admin.cs): shortcuts to test any part of the game on the phone.
    /// They change the state the way the game itself would (the hearth level brings its refugees and
    /// builders, a time skip lives through every hour), so what you see after them is what a player
    /// would see. Nothing here is reachable without the hidden developer mode.
    /// </summary>
    public sealed partial class GameSession
    {
        /// <summary>"+max" for a resource the warehouse does not limit (coal, food, Sparks and the rest).</summary>
        public const long AdminUncappedMax = 1000000;

        /// <summary>A time skip lives through the hours one by one, as if the game stayed open.</summary>
        public const long AdminSkipStep = 3600;

        // ------------------------------------------------------------------ resources

        /// <summary>The most the warehouse holds of a resource; AdminUncappedMax for the unlimited ones.</summary>
        public long AdminResourceMax(ResourceType type)
        {
            long cap = Capacity(type);
            return cap == long.MaxValue ? AdminUncappedMax : cap;
        }

        /// <summary>Adds a resource past the warehouse (it is a cheat); returns the amount now owned.</summary>
        public long AdminAddResource(ResourceType type, long amount)
        {
            State.Wallet[type] = Math.Max(0, State.Wallet[type] + amount);
            if (type == ResourceType.Coal && State.Wallet[type] > 0)
                State.OutOfCoal = false;
            return State.Wallet[type];
        }

        /// <summary>Tops a resource up to AdminResourceMax (never lowers it); returns the amount now owned.</summary>
        public long AdminMaxResource(ResourceType type)
        {
            long max = AdminResourceMax(type);
            if (State.Wallet[type] < max)
                AdminAddResource(type, max - State.Wallet[type]);
            return State.Wallet[type];
        }

        /// <summary>Silver and iron up to the warehouse capacity, coal and food up to AdminUncappedMax.</summary>
        public void AdminFillStorage()
        {
            foreach (var type in new[] { ResourceType.Silver, ResourceType.Iron, ResourceType.Coal, ResourceType.Food })
                AdminMaxResource(type);
        }

        public void AdminZeroResource(ResourceType type)
        {
            State.Wallet[type] = 0;
            if (type == ResourceType.Coal)
                State.CoalDebt = 0;
            if (type == ResourceType.Food)
                State.FoodDebt = 0;
        }

        /// <summary>Empties the wallet, the rewards waiting to be claimed and the hourglass.</summary>
        public void AdminZeroAll()
        {
            for (int i = 0; i < ResourceBag.Count; i++)
                AdminZeroResource((ResourceType)i);
            State.Rewards = new ResourceBag();
            State.HourglassSeconds = 0;
        }

        public long AdminAddHourglass(long seconds)
        {
            State.HourglassSeconds = Math.Max(0, State.HourglassSeconds + seconds);
            return State.HourglassSeconds;
        }

        // ------------------------------------------------------------------ the hearth and the fortress

        /// <summary>
        /// Puts the hearth at a level (clamped to 1..MaxHearthLevel). Going up passes every level as a
        /// finished hearth job does: builders, refugees (while there is room), achievements; the sections
        /// of the mountain open by themselves. Going down cancels the hearth job and lowers the rooms and
        /// the jobs that would outgrow the hearth. Returns the level set.
        /// </summary>
        public int AdminSetHearthLevel(int level, long now)
        {
            Advance(now);
            level = Math.Max(1, Math.Min(Balance.MaxHearthLevel, level));
            foreach (var job in State.Queue.AllJobs().Where(j => j.IsHearth || (!j.IsClearing && j.ToLevel > level)).ToList())
                State.Queue.Remove(job);
            if (level > State.HearthLevel)
            {
                while (State.HearthLevel < level)
                {
                    State.HearthLevel++;
                    State.Queue.EnsureBuilders(BuilderCount());
                    RefugeesArrive();
                }
                CheckAchievements();
            }
            else
            {
                State.HearthLevel = level;
                foreach (var room in State.Rooms)
                    room.Level = Math.Min(room.Level, level);
                foreach (var d in State.Dwellers.Where(d => d.IsHero))
                    d.HeroLevel = Math.Min(d.HeroLevel, Formulas.HeroLevelCap(Balance, level));
            }
            State.Queue.EnsureBuilders(BuilderCount());
            Advance(now); // events and the Surge of the new level
            return State.HearthLevel;
        }

        /// <summary>Every building and clearing job ends now, in the order they would have; returns how many.</summary>
        public int AdminFinishAllJobs(long now)
        {
            Advance(now);
            int count = 0;
            foreach (var job in State.Queue.AllJobs().ToList())
            {
                job.Start = Math.Min(job.Start, State.Time);
                job.End = State.Time;
                count++;
            }
            Advance(now);
            return count;
        }

        /// <summary>Children are born, the teams come back from their routes, the injured heal and the forge finishes its item;
        /// returns how many timers ended.</summary>
        public int AdminFinishOtherTimers(long now)
        {
            Advance(now);
            int count = 0;
            foreach (var p in State.Pairings)
            {
                p.End = Math.Min(p.End, State.Time);
                count++;
            }
            foreach (var e in State.Expeditions.Where(e => !e.Back))
            {
                e.End = Math.Min(e.End, State.Time);
                count++;
            }
            foreach (var d in State.Dwellers.Where(d => d.InjuredUntil > State.Time))
            {
                d.InjuredUntil = 0;
                count++;
            }
            if (State.Gear?.Craft != null && State.Gear.Craft.End > State.Time)
            {
                State.Gear.Craft.End = State.Time; // the forge's item (GameSession.Gear.cs)
                count++;
            }
            Advance(now);
            return count;
        }

        IEnumerable<(int floor, int cell)> AllCells()
        {
            var g = Balance.Grid;
            for (int f = 0; f < g.Floors; f++)
            {
                for (int c = 0; c < g.Cells; c++)
                    yield return (f, c);
            }
        }

        /// <summary>The rubble of every open section is gone (clearing jobs too); returns the cells dug out.</summary>
        public int AdminClearAllObstacles(long now)
        {
            Advance(now);
            int count = 0;
            foreach (var (f, c) in AllCells())
            {
                var cell = CellStateAt(f, c);
                if (cell != CellState.Obstacle && cell != CellState.Clearing)
                    continue;
                var job = State.Queue.Clearing(f, c);
                if (job != null)
                    State.Queue.Remove(job);
                State.Cleared.Add(CellKey(f, c));
                count++;
            }
            return count;
        }

        /// <summary>Every section of the mountain is open and dug out, whatever the hearth level; returns the cells opened.</summary>
        public int AdminUnlockAllSections(long now)
        {
            Advance(now);
            int count = 0;
            foreach (var (f, c) in AllCells())
            {
                var cell = CellStateAt(f, c);
                if (SectionLevel(f, c) == 0 || (cell != CellState.Obstacle && cell != CellState.Clearing && cell != CellState.Locked))
                    continue;
                var job = State.Queue.Clearing(f, c);
                if (job != null)
                    State.Queue.Remove(job);
                State.Cleared.Add(CellKey(f, c));
                count++;
            }
            return count;
        }

        /// <summary>Refugees join past the dweller cap (stats as the refugees of this hearth level have).</summary>
        public List<Dweller> AdminAddDwellers(int count)
        {
            var added = new List<Dweller>();
            for (int i = 0; i < count; i++)
            {
                int n = State.NextDwellerId;
                int lead = (n * 3) % 5;
                var stats = new int[5];
                for (int k = 0; k < 5; k++)
                    stats[k] = 3 + (n + k * 7) % 5 + 2 * State.HearthLevel / 3;
                stats[lead] += 4;
                added.Add(AddDweller(RefugeeNames[(n - 1) % RefugeeNames.Length], stats));
            }
            return added;
        }

        // ------------------------------------------------------------------ time

        /// <summary>
        /// Lives through <paramref name="seconds"/> as if the game stayed open: the world advances hour
        /// by hour, so production, building, births, routes, Surges, siege windows, login days, daily
        /// tasks, arena days and weeks and the event calendar all happen in order. The caller moves its
        /// clock by the same amount (the runner keeps it as an offset). Returns the new "now".
        /// </summary>
        public long AdminSkipTime(long seconds, long now)
        {
            Advance(now);
            long from = Math.Max(now, State.Time);
            long to = from + Math.Max(0, seconds);
            for (long t = from + AdminSkipStep; t < to; t += AdminSkipStep)
                Advance(t);
            Advance(to);
            return to;
        }

        // ------------------------------------------------------------------ events

        public List<EventData> AdminEvents() => Balance.Activities.Events.ToList();

        /// <summary>Points that pay all ten thresholds of a run (a personal run needs one day's share).</summary>
        public double AdminEventTarget(EventRun run) => EventTarget[run.Id] * RunScale(run);

        /// <summary>Opens a run of the event now, for its usual number of days, whatever the calendar and the hearth level.</summary>
        public Fail AdminStartEvent(string id, long now)
        {
            Advance(now);
            var e = Balance.Activities.Events.FirstOrDefault(x => x.Id == id);
            if (e == null)
                return Fail.NotFound;
            if (ActiveRun(id, now) != null)
                return Fail.AlreadyQueued;
            OpenRun(e, now, now + Math.Max(1, e.Days) * Day, false);
            return Fail.None;
        }

        /// <summary>Ends the event's run now: its rank reward is paid as at the usual end.</summary>
        public Fail AdminEndEvent(string id, long now)
        {
            Advance(now);
            var run = ActiveRun(id, now);
            if (run == null)
                return Fail.NotRunning;
            run.End = now;
            CloseRun(run);
            return Fail.None;
        }

        /// <summary>
        /// Brings a running event to its last stage: less than a day is left, the points stand just under
        /// the last threshold (the nine before it pay as usual) and the Portal waits at its last open
        /// checkpoint. The start stays, so the calendar does not open the run a second time.
        /// </summary>
        public Fail AdminEventLastStage(string id, long now)
        {
            Advance(now);
            var run = ActiveRun(id, now);
            if (run == null)
                return Fail.NotRunning;
            run.End = Math.Min(run.End, now + Day);
            if (id == "Portal")
            {
                // ten points a floor: the last checkpoint before the target, if today's floors reach it
                int last = (int)(EventTarget[id] / 10) - 5;
                int open = PortalFloorsOpen(run, now);
                if (open != int.MaxValue)
                    last = Math.Min(last, (open - 1) / 5 * 5);
                run.Floor = run.Checkpoint = Math.Max(run.Checkpoint, last);
                run.RunAlive = false;
                run.BlessingOffer.Clear();
            }
            double almost = AdminEventTarget(run) * 0.95;
            if (run.Points < almost)
                AddPoints(run, almost - run.Points);
            Advance(now);
            return Fail.None;
        }

        /// <summary>Gives the run the points of all ten thresholds, which pay their rewards; returns how many were paid now.</summary>
        public int AdminGrantEventRewards(string id, long now)
        {
            Advance(now);
            var run = ActiveRun(id, now);
            if (run == null)
                return 0;
            int before = run.Thresholds;
            double target = AdminEventTarget(run);
            AddPoints(run, Math.Max(0, target - run.Points) + 0.001);
            return run.Thresholds - before;
        }

        /// <summary>A live Surge right now, whatever the hour and the hearth level (unless one is already on).</summary>
        public Fail AdminStartSurge(long now, out SurgeDefense defense)
        {
            defense = null;
            Advance(now);
            if (Combat == null)
                return Fail.Locked;
            if (State.SurgeLive)
                return Fail.AlreadyQueued;
            State.SurgeLive = true;
            defense = new SurgeDefense(SurgeNumbers, BuildSurgeSetup());
            return Fail.None;
        }

        /// <summary>Today's daily tasks are picked anew, with no progress and the chest not claimed.</summary>
        public List<DailyTask> AdminResetDaily(long now)
        {
            State.DailyDay = -1;
            return TodayTasks(now);
        }

        /// <summary>Every task of today is done: the chest can be claimed.</summary>
        public void AdminCompleteDaily(long now)
        {
            foreach (var t in TodayTasks(now))
                State.DailyProgress[(int)t] = Math.Max(State.DailyProgress[(int)t], TaskTimes(t));
        }

        // ------------------------------------------------------------------ fights

        /// <summary>The next campaign mission is won with three stars and pays its first-clear reward.</summary>
        public Fail AdminWinNextMission(long now, out MissionReward reward)
        {
            reward = null;
            Advance(now);
            int index = MissionsCleared;
            if (index >= Balance.Campaign.Missions.Count)
                return Fail.LimitReached;
            if (State.PendingMission >= 0)
                return Fail.HuntInProgress;
            var m = Mission(index);
            reward = new MissionReward { Stars = 3, FirstClear = true };
            State.CampaignStars.Add(3);
            reward.Got[ResourceType.Essence] += m.Essence;
            reward.Got.Add(ResourceBag.Materials(m.Materials, Balance.SilverShare));
            reward.Got[ResourceType.Sparks] += m.Sparks + Balance.Campaign.ChestSparks;
            if (m.Kind == MissionKind.Boss)
                reward.NewHero = StoryHero(m.Act);
            Count(DailyTask.Campaign);
            CheckAchievements();
            Pay(reward.Got);
            return Fail.None;
        }

        /// <summary>
        /// The campaign as if this many missions were cleared (with three stars; the rewards of the skipped
        /// missions are not paid). Returns the number set (clamped to the campaign).
        /// </summary>
        public int AdminSetCampaignProgress(int cleared)
        {
            cleared = Math.Max(0, Math.Min(Balance.Campaign.Missions.Count, cleared));
            if (State.CampaignStars.Count > cleared)
                State.CampaignStars.RemoveRange(cleared, State.CampaignStars.Count - cleared);
            while (State.CampaignStars.Count < cleared)
                State.CampaignStars.Add(3);
            if (State.PendingMission >= cleared + 1)
                State.PendingMission = -1;
            CheckAchievements();
            return cleared;
        }

        /// <summary>Arena tickets and raid flags full, today's bought tickets forgotten; returns the tickets.</summary>
        public int AdminRefillArena(long now)
        {
            EnsurePvp();
            State.ArenaTickets = Pvp.Tickets;
            State.ArenaTicketAt = 0;
            State.RaidFlags = Pvp.Flags;
            State.RaidFlagAt = 0;
            State.ArenaBought = 0;
            State.ArenaBuyDay = LocalDay(now);
            return ArenaTickets(now);
        }

        /// <summary>New arena opponents and raid targets now, without the 10-minute wait.</summary>
        public void AdminRerollOpponents(long now)
        {
            EnsurePvp();
            State.ArenaList = ThreeGhosts(true);
            State.RaidList = ThreeGhosts(false);
            State.ArenaListAt = now;
        }

        /// <summary>Sets the arena rating (at least 1: 0 means "not started" in the save); the opponents follow it.</summary>
        public int AdminSetArenaRating(int rating)
        {
            EnsurePvp();
            State.ArenaRating = Math.Max(1, rating);
            State.ArenaList.Clear();
            return State.ArenaRating;
        }

        /// <summary>The rating at the bottom of a league (Ash starts at 1); returns the league's index.</summary>
        public int AdminSetLeague(int league)
        {
            league = Math.Max(0, Math.Min(Pvp.Leagues.Count - 1, league));
            AdminSetArenaRating(Pvp.Leagues[league].Rating);
            return LeagueIndex(State.ArenaRating);
        }

        // ------------------------------------------------------------------ heroes and pets

        static readonly string[] AdminHeroNames = { "Ведомир", "Рогнеда", "Светозар", "Добрава", "Яромир", "Велена", "Борислав", "Милена" };

        /// <summary>A new hero of this class (no barracks needed), at level 1, with the stats of a story hero.</summary>
        public Dweller AdminAddHero(HeroClass heroClass)
        {
            if (heroClass == HeroClass.None)
                heroClass = HeroClass.Guardian;
            int stat = 5 + 2 * State.HearthLevel;
            var d = AddDweller(AdminHeroNames[State.NextDwellerId % AdminHeroNames.Length], new[] { stat, stat, stat, stat, stat });
            d.HeroClass = heroClass;
            d.HeroLevel = 1;
            return d;
        }

        /// <summary>A hero at the level cap of the hearth with the skill at its cap; returns the level.</summary>
        public int AdminMaxHero(int dwellerId)
        {
            var d = FindDweller(dwellerId);
            if (d == null || !d.IsHero)
                return 0;
            d.HeroLevel = Math.Max(d.HeroLevel, Formulas.HeroLevelCap(Balance, State.HearthLevel));
            d.SkillLevel = Math.Max(d.SkillLevel, SkillCap(d));
            return d.HeroLevel;
        }

        /// <summary>Every hero at the cap; returns how many heroes.</summary>
        public int AdminMaxAllHeroes()
        {
            var heroes = State.Dwellers.Where(d => d.IsHero).ToList();
            foreach (var d in heroes)
                AdminMaxHero(d.Id);
            return heroes.Count;
        }

        /// <summary>Every pet and companion is found; returns the names of the new ones.</summary>
        public List<string> AdminUnlockKennel()
        {
            var found = new List<string>();
            foreach (var kind in Balance.Pets.Kinds.Where(k => !HasPet(k.Id)))
            {
                AddPet(kind.Id);
                found.Add(kind.Name);
            }
            foreach (var kind in Balance.Companions.Kinds.Where(k => !CompanionUnlocked(k.Id)))
            {
                Companions.Unlocked.Add(kind.Id);
                found.Add(kind.Name);
            }
            return found;
        }

        /// <summary>Every owned pet at its top level; returns how many pets.</summary>
        public int AdminMaxPets()
        {
            var owned = Balance.Pets.Kinds.Where(k => HasPet(k.Id)).ToList();
            foreach (var kind in owned)
                Pets.Levels[kind.Id] = Balance.Pets.MaxLevel;
            return owned.Count;
        }

        public long AdminAddTreats(long amount)
        {
            GrantTreats(amount);
            return Treats;
        }

        // ------------------------------------------------------------------ the save

        /// <summary>The save as the game writes it (with its checksum), to copy off the phone.</summary>
        public string AdminExportSave() => SaveSystem.ToJson(State);
    }
}
