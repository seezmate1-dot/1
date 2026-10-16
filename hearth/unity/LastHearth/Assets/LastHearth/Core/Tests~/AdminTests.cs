using System;
using System.Linq;
using LastHearth.Core;
using NUnit.Framework;

namespace LastHearth.Core.Tests
{
    /// <summary>The developer's menu (GameSession.Admin.cs): every shortcut leaves a state the game itself could reach.</summary>
    public class AdminTests
    {
        const long T0 = TestData.Start; // 08:00 UTC
        const long Hour = 3600;
        const long Day = 86400;

        static GameSession Session(int hearth = 1)
        {
            var s = TestData.NewSession();
            s.Combat = TestData.Combat();
            if (hearth > 1)
                s.AdminSetHearthLevel(hearth, T0);
            return s;
        }

        /// <summary>A fortress at hearth 16 that has seen every event already (no personal runs).</summary>
        static GameSession Seasoned(int hearth = 16)
        {
            var s = Session();
            s.State.EventsSeen.AddRange(s.Balance.Activities.Events.Select(e => e.Id));
            s.AdminSetHearthLevel(hearth, T0);
            s.State.Wallet[ResourceType.Coal] = 10000000;
            s.State.Wallet[ResourceType.Food] = 10000000;
            return s;
        }

        // ------------------------------------------------------------------ resources

        [Test]
        public void ResourcesGoPastTheWarehouseAndMaxTopsUp()
        {
            var s = Session();
            long cap = s.Capacity(ResourceType.Silver);
            s.AdminZeroResource(ResourceType.Silver);
            Assert.AreEqual(cap, s.AdminMaxResource(ResourceType.Silver));
            Assert.AreEqual(cap + 1000, s.AdminAddResource(ResourceType.Silver, 1000), "a cheat ignores the warehouse");
            Assert.AreEqual(cap + 1000, s.AdminMaxResource(ResourceType.Silver), "max never lowers what is owned");
            Assert.AreEqual(GameSession.AdminUncappedMax, s.AdminResourceMax(ResourceType.Sparks));
            s.AdminZeroResource(ResourceType.Sparks);
            Assert.AreEqual(GameSession.AdminUncappedMax, s.AdminMaxResource(ResourceType.Sparks));
            Assert.AreEqual(0, s.AdminAddResource(ResourceType.Glory, -50), "never below zero");
            Assert.AreEqual(3600, s.AdminAddHourglass(3600));
        }

        [Test]
        public void FillStorageAndZeroEverything()
        {
            var s = Session(5);
            s.AdminZeroAll();
            for (int i = 0; i < ResourceBag.Count; i++)
                Assert.AreEqual(0, s.State.Wallet[(ResourceType)i]);
            Assert.IsTrue(s.State.Rewards.IsEmpty());
            Assert.AreEqual(0, s.State.HourglassSeconds);
            s.AdminFillStorage();
            Assert.AreEqual(s.Capacity(ResourceType.Silver), s.State.Wallet[ResourceType.Silver]);
            Assert.AreEqual(s.Capacity(ResourceType.Iron), s.State.Wallet[ResourceType.Iron]);
            Assert.AreEqual(GameSession.AdminUncappedMax, s.State.Wallet[ResourceType.Coal]);
            Assert.AreEqual(GameSession.AdminUncappedMax, s.State.Wallet[ResourceType.Food]);
            Assert.AreEqual(0, s.State.Wallet[ResourceType.Sparks], "only the warehouse goods");
        }

        // ------------------------------------------------------------------ the hearth and the fortress

        [Test]
        public void RaisingTheHearthPassesEveryLevel()
        {
            var s = Session();
            int dwellers = s.State.Dwellers.Count;
            Assert.AreEqual(10, s.AdminSetHearthLevel(10, T0));
            Assert.AreEqual(10, s.State.HearthLevel);
            Assert.AreEqual(s.BuilderCount(), s.State.Queue.Builders.Count, "the builders of level 10");
            int cap = Formulas.DwellerCap(s.Balance, 10);
            Assert.AreEqual(Math.Min(cap, dwellers + 2 * 9), s.State.Dwellers.Count, "two refugees a level while there is room");
            for (int l = 2; l <= 10; l++)
                Assert.Contains("hearth" + l, s.State.Achievements);
            // the section of hearth 8 (floors 0-6) is open: rubble there, not locked rock
            Assert.AreEqual(8, s.SectionLevel(0, 0));
            Assert.AreEqual(CellState.Obstacle, s.CellStateAt(0, 0));
            Assert.AreEqual(CellState.Locked, s.CellStateAt(7, 0), "the section of hearth 12 stays shut");
            // a newcomer gets the personal day of every event the level opened
            foreach (var e in s.Balance.Activities.Events.Where(e => e.Unlock <= 10))
                Assert.IsNotNull(s.ActiveRun(e.Id, T0), e.Id);
            Assert.IsNull(s.ActiveRun("LongNight", T0), "LongNight opens at 11");
        }

        [Test]
        public void TheHearthLevelIsClampedAndAQueuedHearthJobIsCancelled()
        {
            var s = Session();
            Assert.AreEqual(Fail.None, s.UpgradeHearth(T0));
            Assert.IsTrue(s.State.Queue.IsQueued(BuildJob.HearthTarget));
            Assert.AreEqual(s.Balance.MaxHearthLevel, s.AdminSetHearthLevel(99, T0));
            Assert.IsFalse(s.State.Queue.IsQueued(BuildJob.HearthTarget));
            s.Advance(T0 + Day);
            Assert.AreEqual(s.Balance.MaxHearthLevel, s.State.HearthLevel, "no old job lands later");
            Assert.AreEqual(1, s.AdminSetHearthLevel(0, T0 + Day));
        }

        [Test]
        public void LoweringTheHearthLowersRoomsHeroesAndJobs()
        {
            var s = Session(12);
            var kitchen = s.State.Rooms.First(r => r.Type == RoomType.Kitchen);
            kitchen.Level = 9;
            s.AdminFillStorage();
            Assert.AreEqual(Fail.None, s.UpgradeRoom(kitchen.Id, T0));
            var hero = s.State.Dwellers.First(d => d.IsHero);
            s.AdminMaxHero(hero.Id);
            Assert.AreEqual(3, s.AdminSetHearthLevel(3, T0));
            Assert.IsTrue(s.State.Rooms.All(r => r.Level <= 3));
            Assert.IsFalse(s.State.Queue.AllJobs().Any(j => j.ToLevel > 3));
            Assert.AreEqual(Formulas.HeroLevelCap(s.Balance, 3), hero.HeroLevel);
            Assert.GreaterOrEqual(s.State.Queue.Builders.Count, s.BuilderCount(), "builders are never taken away");
            Assert.AreEqual(Fail.AboveHearth, s.UpgradeRoom(kitchen.Id, T0), "the rooms follow the rules of the new level");
        }

        [Test]
        public void FinishingAllJobsCompletesBuildingAndClearing()
        {
            var s = Session(8);
            s.AdminFillStorage();
            var kitchen = s.State.Rooms.First(r => r.Type == RoomType.Kitchen);
            kitchen.Level = 7;
            Assert.AreEqual(Fail.None, s.UpgradeRoom(kitchen.Id, T0));
            var (f, c) = Cells(s).First(x => s.CellStateAt(x.floor, x.cell) == CellState.Obstacle);
            Assert.AreEqual(Fail.None, s.ClearObstacle(f, c, T0));
            int done = 0;
            s.JobCompleted += _ => done++;
            Assert.AreEqual(2, s.AdminFinishAllJobs(T0));
            Assert.AreEqual(2, done);
            Assert.AreEqual(0, s.State.Queue.AllJobs().Count());
            Assert.AreEqual(8, kitchen.Level);
            Assert.AreEqual(CellState.Free, s.CellStateAt(f, c));
            Assert.AreEqual(T0, s.State.Time, "no time passed");
        }

        [Test]
        public void OtherTimersEndNow()
        {
            var s = Session(6);
            var room = s.State.Rooms.First(r => r.Type == RoomType.Bedrooms);
            var man = s.State.Dwellers.First(d => d.Sex == Sex.Male && !d.IsHero);
            var woman = s.State.Dwellers.First(d => d.Sex == Sex.Female && !d.IsHero);
            Assert.AreEqual(Fail.None, s.StartPairing(room.Id, man.Id, woman.Id, T0));
            int before = s.State.Dwellers.Count;
            Assert.AreEqual(1, s.AdminFinishOtherTimers(T0));
            Assert.AreEqual(before + 1, s.State.Dwellers.Count, "the child is born");
            Assert.AreEqual(0, s.State.Pairings.Count);
        }

        static System.Collections.Generic.IEnumerable<(int floor, int cell)> Cells(GameSession s)
        {
            for (int f = 0; f < s.Balance.Grid.Floors; f++)
            {
                for (int c = 0; c < s.Balance.Grid.Cells; c++)
                    yield return (f, c);
            }
        }

        [Test]
        public void ClearingObstaclesLeavesTheLockedSectionsAlone()
        {
            var s = Session(5);
            int locked = Cells(s).Count(x => s.CellStateAt(x.floor, x.cell) == CellState.Locked);
            Assert.Greater(s.AdminClearAllObstacles(T0), 0);
            Assert.IsFalse(Cells(s).Any(x => s.CellStateAt(x.floor, x.cell) == CellState.Obstacle));
            Assert.AreEqual(locked, Cells(s).Count(x => s.CellStateAt(x.floor, x.cell) == CellState.Locked));
            Assert.AreEqual(0, s.AdminClearAllObstacles(T0));
        }

        [Test]
        public void UnlockingAllSectionsDigsOutTheWholeMountain()
        {
            var s = Session();
            Assert.Greater(s.AdminUnlockAllSections(T0), 0);
            foreach (var (f, c) in Cells(s).Where(x => s.SectionLevel(x.floor, x.cell) > 0))
            {
                var cell = s.CellStateAt(f, c);
                Assert.IsTrue(cell == CellState.Free || cell == CellState.Room || cell == CellState.Hearth, f + "," + c + " " + cell);
            }
            Assert.AreEqual(1, s.State.HearthLevel, "the hearth stays");
        }

        [Test]
        public void DwellersJoinPastTheCap()
        {
            var s = Session();
            s.AdminAddDwellers(Formulas.DwellerCap(s.Balance, 1));
            Assert.IsTrue(s.PopulationFull());
            int before = s.State.Dwellers.Count;
            var added = s.AdminAddDwellers(3);
            Assert.AreEqual(3, added.Count);
            Assert.AreEqual(before + 3, s.State.Dwellers.Count);
            Assert.IsTrue(added.All(d => !d.IsHero && d.Sex != Sex.Unknown && d.Stats.All(x => x > 0)));
        }

        // ------------------------------------------------------------------ time

        [Test]
        public void ASkipMovesTheClockAndFinishesWhatWasDue()
        {
            var s = Session(8);
            s.AdminFillStorage();
            var kitchen = s.State.Rooms.First(r => r.Type == RoomType.Kitchen);
            kitchen.Level = 7;
            Assert.AreEqual(Fail.None, s.UpgradeRoom(kitchen.Id, T0)); // 15 minutes
            double stock = kitchen.Stock;
            long now = s.AdminSkipTime(8 * Hour, T0);
            Assert.AreEqual(T0 + 8 * Hour, now);
            Assert.AreEqual(now, s.State.Time);
            Assert.AreEqual(8, kitchen.Level);
            Assert.Greater(kitchen.Stock, stock, "the kitchen cooked meanwhile");
            Assert.AreEqual(now + Hour, s.AdminSkipTime(Hour, now));
        }

        [Test]
        public void ASkipFromBehindTheSimulatedTimeStartsThere()
        {
            var s = Session();
            s.Advance(T0 + Hour);
            Assert.AreEqual(T0 + 2 * Hour, s.AdminSkipTime(Hour, T0), "the clock never runs backwards");
        }

        [Test]
        public void ASkipIsTheSameAsLivingHourByHour()
        {
            var skipped = Seasoned();
            var lived = Seasoned();
            skipped.AdminSkipTime(7 * Day + 5 * Hour, T0);
            for (long t = T0 + Hour; t < T0 + 7 * Day + 5 * Hour; t += Hour)
                lived.Advance(t);
            lived.Advance(T0 + 7 * Day + 5 * Hour);
            Assert.AreEqual(SaveSystem.ToJson(lived.State), SaveSystem.ToJson(skipped.State));
        }

        [Test]
        public void ADaySkipResetsTheDayEverywhere()
        {
            var s = Seasoned(17);
            var tasks = s.TodayTasks(T0);
            s.State.DailyProgress[(int)tasks[0]] = 5;
            int logins = s.State.LoginCount;
            s.State.HuntTracks = 0;
            s.State.HuntTrackAt = T0 + 8 * Hour;
            s.ArenaTickets(T0);
            s.State.ArenaTickets = 0;
            s.State.ArenaTicketAt = T0 + 3 * Hour;
            s.State.RaidFlags = 0;
            s.State.RaidFlagAt = T0 + 3 * Hour;
            s.RepeatsLeft(T0);
            s.State.RepeatsUsed = 4;
            long surgeDay = s.State.SurgeDay;
            long now = s.AdminSkipTime(Day, T0);
            s.TodayTasks(now);
            Assert.AreEqual(s.LocalDay(now), s.State.DailyDay);
            Assert.IsTrue(s.State.DailyProgress.All(p => p == 0), "today's tasks start from nothing");
            Assert.AreEqual(logins + 1, s.State.LoginCount, "a new login day");
            Assert.AreEqual(s.Balance.Activities.Hunt.TracksPerDay, s.HuntTracks(now));
            Assert.AreEqual(s.Balance.Activities.Pvp.Tickets, s.ArenaTickets(now));
            Assert.AreEqual(s.Balance.Activities.Pvp.Flags, s.RaidFlags(now));
            Assert.AreEqual(s.Balance.Campaign.RepeatsPerDay, s.RepeatsLeft(now));
            Assert.AreEqual(s.LocalDay(now) - 1, s.State.SurgeDay, "last evening's Surge was defended");
            Assert.Greater(s.State.SurgeDay, surgeDay);
        }

        [Test]
        public void AWeekSkipEndsTheArenaWeek()
        {
            var s = Seasoned(17);
            s.Advance(T0);
            long week = s.ArenaWeekKey(T0);
            Assert.AreEqual(week, s.State.ArenaWeek);
            s.State.ArenaWeekFights = 3;
            long glory = s.State.Wallet[ResourceType.Glory];
            long now = s.AdminSkipTime(7 * Day, T0);
            Assert.AreEqual(s.ArenaWeekKey(now), s.State.ArenaWeek);
            Assert.AreNotEqual(week, s.State.ArenaWeek);
            Assert.AreEqual(0, s.State.ArenaWeekFights, "a new arena week");
            Assert.Greater(s.State.Wallet[ResourceType.Glory], glory, "the league paid the week");
        }

        [Test]
        public void SkippingDayByDayFollowsTheEventCalendar()
        {
            var s = Seasoned();
            long now = T0;
            var seen = new System.Collections.Generic.HashSet<string>();
            for (int day = 0; day < 28; day++)
            {
                now = s.AdminSkipTime(Day, now);
                foreach (var e in s.Balance.Activities.Events.Where(e => e.Unlock <= 16))
                {
                    bool on = s.OnCalendar(e, s.LocalDay(now));
                    Assert.AreEqual(on, s.ActiveRun(e.Id, now) != null, e.Id + " on day " + day);
                    if (on)
                        seen.Add(e.Id);
                }
            }
            foreach (var id in new[] { "LongNight", "Portal", "Tournament", "Treasure", "Fair", "Siege" })
                Assert.Contains(id, seen.ToList());
        }

        // ------------------------------------------------------------------ events

        [Test]
        public void AnyEventStartsNowAndEnds()
        {
            var s = Session();
            Assert.AreEqual(7, s.AdminEvents().Count);
            Assert.AreEqual(Fail.NotFound, s.AdminStartEvent("Nope", T0));
            Assert.AreEqual(Fail.None, s.AdminStartEvent("Siege", T0), "whatever the hearth level");
            var run = s.ActiveRun("Siege", T0);
            Assert.IsNotNull(run);
            Assert.AreEqual(T0, run.Start);
            Assert.AreEqual(T0 + s.EventInfo("Siege").Days * Day, run.End);
            Assert.AreEqual(5, run.Outposts.Count, "the mechanics are set up as on the calendar");
            Assert.AreEqual(Fail.AlreadyQueued, s.AdminStartEvent("Siege", T0));
            Assert.AreEqual(Fail.None, s.AdminEndEvent("Siege", T0 + Hour));
            Assert.IsNull(s.ActiveRun("Siege", T0 + Hour));
            Assert.IsTrue(run.Closed);
            StringAssert.Contains("место", run.LastResult);
            Assert.AreEqual(Fail.NotRunning, s.AdminEndEvent("Siege", T0 + Hour));
            s.Advance(T0 + 2 * Hour);
            Assert.IsNull(s.ActiveRun("Siege", T0 + 2 * Hour));
        }

        [Test]
        public void AnEndedCalendarRunDoesNotComeBack()
        {
            var s = Seasoned();
            long now = T0;
            while (s.ActiveRun("Portal", now) == null)
                now = s.AdminSkipTime(Day, now);
            Assert.AreEqual(Fail.None, s.AdminEndEvent("Portal", now));
            s.AdminSkipTime(Hour, now);
            Assert.IsNull(s.ActiveRun("Portal", now + Hour));
            Assert.AreEqual(Fail.None, s.AdminStartEvent("Portal", now + Hour), "but it can be started again");
        }

        [Test]
        public void TheLastStageAndTheRewardsOfEveryEvent()
        {
            var s = Session(16);
            long now = T0;
            foreach (var e in s.AdminEvents())
            {
                var old = s.ActiveRun(e.Id, now);
                if (old != null)
                    s.AdminEndEvent(e.Id, now);
                Assert.AreEqual(Fail.None, s.AdminStartEvent(e.Id, now), e.Id);
                var run = s.ActiveRun(e.Id, now);
                long sparks = s.State.Wallet[ResourceType.Sparks];
                Assert.AreEqual(Fail.None, s.AdminEventLastStage(e.Id, now), e.Id);
                Assert.AreEqual(9, run.Thresholds, e.Id + ": the nine thresholds before the last");
                Assert.LessOrEqual(run.End, now + Day, e.Id);
                Assert.Greater(run.End, now, e.Id);
                Assert.Greater(s.State.Wallet[ResourceType.Sparks], sparks, e.Id + ": the thresholds paid");
                Assert.AreEqual(1, s.AdminGrantEventRewards(e.Id, now), e.Id);
                Assert.AreEqual(10, run.Thresholds, e.Id);
                Assert.AreEqual(0, s.AdminGrantEventRewards(e.Id, now), e.Id + ": nothing left to pay");
                Assert.AreEqual(Fail.None, s.AdminEndEvent(e.Id, now), e.Id);
            }
            Assert.AreEqual(Fail.NotRunning, s.AdminEventLastStage("Portal", now));
            Assert.AreEqual(0, s.AdminGrantEventRewards("Portal", now));
        }

        [Test]
        public void ThePortalWaitsAtItsLastOpenCheckpoint()
        {
            var s = Session(16);
            if (s.ActiveRun("Portal", T0) != null)
                s.AdminEndEvent("Portal", T0);
            s.AdminStartEvent("Portal", T0);
            var run = s.ActiveRun("Portal", T0);
            s.AdminEventLastStage("Portal", T0);
            Assert.Greater(run.Checkpoint, 0);
            Assert.AreEqual(0, run.Checkpoint % 5);
            Assert.Less(run.Floor, s.PortalFloorsOpen(run, T0));
            Assert.AreEqual(Fail.None, s.StartEventBattle("Portal", T0, out var battle), "the next floor can be fought");
            Assert.AreEqual(run.Checkpoint, run.Floor);
            battle.RunToEnd();
            s.FinishEventBattle(battle, T0, out _);
        }

        [Test]
        public void GrantingRewardsOfAFreshRunPaysAllTenThresholds()
        {
            var s = Session(16);
            s.AdminEndEvent("Fair", T0);
            s.AdminStartEvent("Fair", T0);
            long essence = s.State.Wallet[ResourceType.Essence];
            Assert.AreEqual(10, s.AdminGrantEventRewards("Fair", T0));
            Assert.Greater(s.State.Wallet[ResourceType.Essence], essence);
            Assert.IsTrue(s.ActiveRun("Fair", T0).Wanderer, "the tenth threshold of the Fair brings the wanderer");
        }

        [Test]
        public void ASurgeStartsAtAnyHour()
        {
            var s = Session(5);
            Assert.IsFalse(s.SurgeOpen(T0), "08:00: the window is shut");
            Assert.AreEqual(Fail.None, s.AdminStartSurge(T0, out var defense));
            Assert.IsTrue(s.State.SurgeLive);
            Assert.AreEqual(Fail.AlreadyQueued, s.AdminStartSurge(T0, out _));
            long ember = s.State.Wallet[ResourceType.Ember];
            Assert.AreEqual(Fail.None, s.FinishSurge(defense, T0, out var reward));
            Assert.IsFalse(s.State.SurgeLive);
            Assert.AreEqual(ember + reward.Ember, s.State.Wallet[ResourceType.Ember]);
            s.AdminSkipTime(17 * Hour, T0); // 01:00 of the next day
            Assert.AreEqual(s.LocalDay(T0), s.State.SurgeDay, "tonight's Surge still comes (settled at midnight)");
        }

        [Test]
        public void DailyTasksResetAndComplete()
        {
            var s = Session(3);
            var tasks = s.TodayTasks(T0);
            s.State.DailyProgress[(int)tasks[0]] = 9;
            s.State.DailyClaimed = true;
            Assert.AreEqual(tasks, s.AdminResetDaily(T0));
            Assert.IsFalse(s.State.DailyClaimed);
            Assert.IsTrue(s.State.DailyProgress.All(p => p == 0));
            Assert.AreEqual(Fail.RulesNotMet, s.ClaimDaily(T0));
            s.AdminCompleteDaily(T0);
            Assert.AreEqual(tasks.Count, s.TasksDone(T0));
            Assert.AreEqual(Fail.None, s.ClaimDaily(T0));
        }

        // ------------------------------------------------------------------ fights

        [Test]
        public void TheNextMissionIsWonWithItsReward()
        {
            var s = Session(3);
            long essence = s.State.Wallet[ResourceType.Essence];
            Assert.AreEqual(Fail.None, s.AdminWinNextMission(T0, out var reward));
            Assert.AreEqual(1, s.MissionsCleared);
            Assert.AreEqual(3, s.State.CampaignStars[0]);
            Assert.IsTrue(reward.FirstClear);
            Assert.AreEqual(essence + s.Mission(0).Essence, s.State.Wallet[ResourceType.Essence]);
            Assert.Greater(s.State.DailyProgress[(int)DailyTask.Campaign], 0, "counts for the daily task");
            // the act boss brings its story hero
            int boss = Enumerable.Range(0, s.Balance.Campaign.Missions.Count).First(i => s.Mission(i).Kind == MissionKind.Boss);
            Assert.AreEqual(boss, s.AdminSetCampaignProgress(boss));
            int heroes = s.State.Dwellers.Count(d => d.IsHero);
            Assert.AreEqual(Fail.None, s.AdminWinNextMission(T0, out reward));
            Assert.IsNotNull(reward.NewHero);
            Assert.AreEqual(heroes + 1, s.State.Dwellers.Count(d => d.IsHero));
            Assert.Contains("act1", s.State.Achievements);
            s.AdminSetCampaignProgress(int.MaxValue);
            Assert.AreEqual(Fail.LimitReached, s.AdminWinNextMission(T0, out _));
        }

        [Test]
        public void CampaignProgressIsSetBothWays()
        {
            var s = Session();
            int all = s.Balance.Campaign.Missions.Count;
            Assert.AreEqual(all, s.AdminSetCampaignProgress(all + 5));
            Assert.AreEqual(all, s.MissionsCleared);
            Assert.AreEqual(10, s.AdminSetCampaignProgress(10));
            Assert.AreEqual(10, s.MissionsCleared);
            Assert.AreEqual(0, s.AdminSetCampaignProgress(-3));
            Assert.AreEqual(Fail.Locked, s.StartMission(5, T0, out _), "the missions after it are locked again");
        }

        [Test]
        public void ArenaTicketsFlagsAndOpponents()
        {
            var s = Seasoned(17);
            s.ArenaTickets(T0);
            s.State.ArenaTickets = 0;
            s.State.ArenaTicketAt = T0 + 3 * Hour;
            s.State.RaidFlags = 0;
            Assert.AreEqual(s.Balance.Activities.Pvp.Tickets, s.AdminRefillArena(T0));
            Assert.AreEqual(s.Balance.Activities.Pvp.Flags, s.RaidFlags(T0));
            var old = s.ArenaOpponents(T0).Select(o => o.Seed).ToList();
            Assert.AreEqual(Fail.None, s.RerollArena(T0));
            Assert.AreEqual(Fail.NotToday, s.RerollArena(T0), "the free reroll waits 10 minutes");
            s.AdminRerollOpponents(T0);
            Assert.AreNotEqual(old, s.ArenaOpponents(T0).Select(o => o.Seed).ToList());
            Assert.AreEqual(3, s.RaidTargets(T0).Count);
            Assert.AreEqual(Fail.None, s.RerollArena(T0), "the wait is over");
        }

        [Test]
        public void RatingAndLeague()
        {
            var s = Seasoned(17);
            var leagues = s.Balance.Activities.Pvp.Leagues;
            for (int i = 0; i < leagues.Count; i++)
            {
                Assert.AreEqual(i, s.AdminSetLeague(i));
                Assert.AreEqual(leagues[i].Name, s.League.Name);
            }
            Assert.AreEqual(1, s.AdminSetArenaRating(-100), "0 would mean a fresh arena");
            Assert.AreEqual(1500, s.AdminSetArenaRating(1500));
            Assert.AreEqual(0, s.State.ArenaList.Count, "opponents of the old rating are gone");
            Assert.AreEqual(3, s.ArenaOpponents(T0).Count);
            Assert.AreEqual(1500, s.State.ArenaRating);
        }

        // ------------------------------------------------------------------ heroes and pets

        [Test]
        public void HeroesOfAnyClassAndAtTheCap()
        {
            var s = Session(4);
            Assert.AreEqual(0, s.MaxRoomLevel(RoomType.Barracks), "no barracks needed");
            foreach (HeroClass c in Enum.GetValues(typeof(HeroClass)))
            {
                if (c == HeroClass.None)
                    continue;
                var d = s.AdminAddHero(c);
                Assert.AreEqual(c, d.HeroClass);
                Assert.AreEqual(1, d.HeroLevel);
                Assert.AreEqual(Formulas.HeroLevelCap(s.Balance, 4), s.AdminMaxHero(d.Id));
                Assert.AreEqual(s.SkillCap(d), d.SkillLevel);
                Assert.AreEqual(Fail.HeroCap, s.LevelUpHero(d.Id));
            }
            Assert.AreEqual(0, s.AdminMaxHero(s.State.Dwellers.First(d => !d.IsHero).Id), "a dweller is not a hero");
            Assert.AreEqual(s.State.Dwellers.Count(d => d.IsHero), s.AdminMaxAllHeroes());
        }

        [Test]
        public void TheWholeKennel()
        {
            var s = Session();
            var found = s.AdminUnlockKennel();
            Assert.AreEqual(s.Balance.Pets.Kinds.Count + s.Balance.Companions.Kinds.Count, found.Count);
            Assert.IsTrue(s.Balance.Pets.Kinds.All(k => s.HasPet(k.Id)));
            Assert.IsTrue(s.Balance.Companions.Kinds.All(k => s.CompanionUnlocked(k.Id)));
            Assert.IsNotNull(s.ActivePet(), "a pet is at work");
            Assert.AreEqual(0, s.AdminUnlockKennel().Count);
            Assert.AreEqual(s.Balance.Pets.Kinds.Count, s.AdminMaxPets());
            Assert.IsTrue(s.Balance.Pets.Kinds.All(k => s.PetLevel(k.Id) == s.Balance.Pets.MaxLevel));
            long treats = s.Treats;
            Assert.AreEqual(treats + 500, s.AdminAddTreats(500));
        }

        // ------------------------------------------------------------------ the save

        [Test]
        public void TheExportedSaveLoadsBack()
        {
            var s = Session(6);
            s.AdminAddHero(HeroClass.Archer);
            string json = s.AdminExportSave();
            var loaded = SaveSystem.FromJson(json);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(json, SaveSystem.ToJson(loaded));
        }
    }
}
