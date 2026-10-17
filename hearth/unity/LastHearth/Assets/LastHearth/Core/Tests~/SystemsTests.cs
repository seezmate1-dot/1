using System.Linq;
using LastHearth.Core;
using LastHearth.Core.Surge;
using NUnit.Framework;

namespace LastHearth.Core.Tests
{
    /// <summary>Surge, Heart of the Hearth, specializations, routes, daily tasks, calendar, achievements.</summary>
    public class SystemsTests
    {
        const long T0 = TestData.Start; // 08:00 UTC
        const long Hour = 3600;

        static GameSession Fortress(int hearth)
        {
            var s = TestData.NewSession();
            s.Combat = TestData.Combat();
            s.State.HearthLevel = hearth;
            s.State.Queue.EnsureBuilders(s.BuilderCount());
            s.State.Wallet[ResourceType.Coal] = 100000;
            return s;
        }

        /// <summary>A fortress with the five heroes a free player has at this hearth (level 2L).</summary>
        static GameSession Defended(int hearth)
        {
            var s = Fortress(hearth);
            var classes = new[] { HeroClass.Guardian, HeroClass.Swordsman, HeroClass.Cleaver, HeroClass.Archer, HeroClass.CoalPriest };
            foreach (var c in classes)
            {
                var d = s.AddDweller(c.ToString(), new[] { 5 + 2 * hearth, 5 + 2 * hearth, 5 + 2 * hearth, 5 + 2 * hearth, 5 + 2 * hearth });
                d.HeroClass = c;
                d.HeroLevel = 2 * hearth;
            }
            return s;
        }

        // ------------------------------------------------------------------ Surge

        [Test]
        public void TheSurgeOpensAtSevenInTheEveningOnceADay()
        {
            var s = Fortress(5);
            Assert.IsFalse(s.SurgeOpen(T0));
            Assert.IsTrue(s.SurgeOpen(T0 + 11 * Hour));
            Assert.AreEqual(Fail.None, s.StartSurge(T0 + 11 * Hour, out var d));
            d.Brain = SurgeDefense.Careful();
            Assert.AreEqual(Fail.None, s.FinishSurge(d, T0 + 11 * Hour, out var reward));
            Assert.IsTrue(reward.Live);
            Assert.Greater(reward.Ember, 0);
            Assert.AreEqual(reward.Ember, s.State.Wallet[ResourceType.Ember]);
            Assert.IsFalse(s.SurgeOpen(T0 + 12 * Hour), "once a day");
            Assert.IsTrue(s.SurgeOpen(T0 + 35 * Hour), "and again the next evening");
        }

        [Test]
        public void ThreeStarsLivePayInFullAMissedEveningPaysSeventyPercent()
        {
            var live = Defended(5);
            live.StartSurge(T0 + 11 * Hour, out var d);
            d.Brain = SurgeDefense.Careful();
            live.FinishSurge(d, T0 + 11 * Hour, out var reward);
            Assert.AreEqual(3, reward.Stars);
            Assert.AreEqual(live.Balance.Activities.Surge.EmberByLevel[4], reward.Ember);

            var missed = Defended(5);
            missed.Advance(T0 + 25 * Hour); // the next morning: yesterday's evening was defended automatically
            long expected = (long)System.Math.Round(missed.Balance.Activities.Surge.EmberByLevel[4] * missed.Balance.Activities.Surge.AutoShare);
            Assert.AreEqual(expected, missed.State.Wallet[ResourceType.Ember]);
            Assert.AreEqual(missed.Balance.Activities.Surge.HourglassMinutes * 60, missed.State.HourglassSeconds);
        }

        [Test]
        public void ALostSurgeFreezesTheFarFloorsUntilMorning()
        {
            var s = Fortress(5);
            int h = s.Balance.Grid.HearthFloor;
            var far = new Room { Id = 77, Type = RoomType.Mine, Level = 1, Floor = h + 2, Cell = 0 };
            s.State.Rooms.Add(far);
            s.State.Dwellers.Clear(); // nobody to defend
            s.StartSurge(T0 + 11 * Hour, out var d);
            d.Brain = SurgeDefense.Passive();
            s.FinishSurge(d, T0 + 11 * Hour, out var reward);
            Assert.AreEqual(0, reward.Stars);
            Assert.IsTrue(reward.Frost);
            s.Advance(T0 + 12 * Hour);
            Assert.IsFalse(s.IsRoomWarm(far), "frozen tonight");
            s.Advance(T0 + 24 * Hour + 1);
            Assert.IsTrue(s.IsRoomWarm(far), "warm again after 08:00");
        }

        [Test]
        public void TheSurgeIsBuiltFromTheRealFortress()
        {
            var s = Fortress(5);
            var setup = s.BuildSurgeSetup();
            Assert.AreEqual(s.State.Dwellers.Count(x => x.RoomId >= 0 && !x.IsHero), setup.Defenders.Count(x => !x.Hero), "workers fight on their floors");
            Assert.AreEqual(s.Squad().Count, setup.Defenders.Count(x => x.Hero));
            CollectionAssert.IsNotEmpty(setup.SpawnFloors);
        }

        // ------------------------------------------------------------------ Heart of the Hearth

        [Test]
        public void HeartTalentsCostEmberAndChangeTheFortress()
        {
            var s = Fortress(5);
            double burn = s.CoalBurnPerHour();
            double warmth = s.WarmthMult();
            s.State.Wallet[ResourceType.Ember] = 1000;
            Assert.AreEqual(Fail.None, s.BuyHeart(HeartBranch.Economy));
            Assert.AreEqual(1000 - s.Balance.Activities.Heart.NodeCost[0], s.State.Wallet[ResourceType.Ember]);
            Assert.Less(s.CoalBurnPerHour(), burn, "economy talents save coal");
            Assert.AreEqual(Fail.None, s.BuyHeart(HeartBranch.Warmth));
            Assert.Greater(s.WarmthMult(), warmth, "warmth talents reach further");
        }

        [Test]
        public void TheSecondTenTalentsWaitForHearthTwelve()
        {
            var s = Fortress(11);
            s.State.Heart = new[] { 10, 0, 0 };
            s.State.Wallet[ResourceType.Ember] = 100000;
            Assert.AreEqual(Fail.LimitReached, s.BuyHeart(HeartBranch.Warmth), "ten per branch");
            Assert.AreEqual(Fail.Locked, s.BuyHeart(HeartBranch.Defense));
            s.State.HearthLevel = 12;
            Assert.AreEqual(Fail.None, s.BuyHeart(HeartBranch.Defense));
        }

        // ------------------------------------------------------------------ specializations

        [Test]
        public void SpecializationsNeedTheRoomLevelAndChangeOutput()
        {
            var s = Fortress(10);
            var mine = s.State.Rooms.First(r => r.Type == RoomType.CoalMine);
            mine.Level = 4;
            s.State.Wallet[ResourceType.Blueprints] = 100;
            Assert.AreEqual(Fail.Locked, s.BuySpec(RoomType.CoalMine, 0, 1));
            mine.Level = 5;
            double before = s.RoomOutputPerHour(mine);
            Assert.AreEqual(Fail.None, s.BuySpec(RoomType.CoalMine, 0, 1));
            Assert.AreEqual(before * 1.15, s.RoomOutputPerHour(mine), 1e-9, "«Глубокие жилы»: +15% угля");
            Assert.AreEqual(100 - s.SpecCost(0), s.State.Wallet[ResourceType.Blueprints]);
            Assert.AreEqual(Fail.AlreadyQueued, s.BuySpec(RoomType.CoalMine, 0, 2), "one option per tier");
            Assert.AreEqual(Fail.Locked, s.RankUpSpec(RoomType.CoalMine, 0), "mastery opens at hearth 25");
        }

        [Test]
        public void WarmWallsKeepARoomWorkingInTheCold()
        {
            var s = Fortress(10);
            var mine = s.State.Rooms.First(r => r.Type == RoomType.CoalMine);
            mine.Level = 10;
            s.State.Wallet[ResourceType.Coal] = 0;
            s.Advance(T0 + Hour);
            Assert.IsFalse(s.IsRoomWarm(mine));
            s.State.Wallet[ResourceType.Blueprints] = 100;
            Assert.AreEqual(Fail.None, s.BuySpec(RoomType.CoalMine, 0, 2), "«Крепкие своды»");
            Assert.IsTrue(s.IsRoomWarm(mine));
        }

        // ------------------------------------------------------------------ routes

        [Test]
        public void ARouteTakesTheTeamAwayAndBringsBlueprints()
        {
            var s = Fortress(10);
            var team = s.State.Dwellers.Where(d => !d.IsHero).Take(2).Select(d => d.Id).ToList();
            Assert.AreEqual(Fail.None, s.StartRoute(team, RoutePath.Safe, RoutePath.Long, T0, out var route));
            Assert.IsTrue(team.All(id => s.FindDweller(id).RoomId < 0 && s.FindDweller(id).RouteId == route.Id));
            Assert.AreEqual(Fail.NotFound, s.StartRoute(team, RoutePath.Safe, RoutePath.Safe, T0, out _), "already walking");
            s.Advance(route.End);
            Assert.IsTrue(route.Back);
            Assert.Greater(s.State.Wallet[ResourceType.Blueprints], 0);
            Assert.IsTrue(team.All(id => s.FindDweller(id).RouteId == 0));
            Assert.AreEqual(Fail.None, s.ResolveRouteCard(route.Id, 2, route.End));
            Assert.IsEmpty(s.State.Expeditions);
        }

        [Test]
        public void RoutesADayFollowTheWatchtowerSquads()
        {
            var s = Fortress(10);
            int squads = s.Balance.Activities.Routes.SquadsByLevel[9];
            for (int i = 0; i < squads; i++)
            {
                var d = s.AddDweller("Разведчик " + i, new[] { 20, 20, 20, 20, 20 });
                Assert.AreEqual(Fail.None, s.StartRoute(new[] { d.Id }, RoutePath.Safe, RoutePath.Safe, T0, out _));
            }
            var extra = s.AddDweller("Лишний", new[] { 20, 20, 20, 20, 20 });
            Assert.AreEqual(Fail.NoTracks, s.StartRoute(new[] { extra.Id }, RoutePath.Safe, RoutePath.Safe, T0, out _));
            Assert.AreEqual(Fail.None, s.StartRoute(new[] { extra.Id }, RoutePath.Safe, RoutePath.Safe, T0 + 24 * Hour, out _), "a new day");
        }

        // ------------------------------------------------------------------ daily, calendar, achievements

        [Test]
        public void ThreeTasksOfFivePayTheDailyReward()
        {
            var s = Fortress(10);
            var tasks = s.TodayTasks(T0);
            Assert.AreEqual(5, tasks.Count);
            Assert.AreEqual(Fail.RulesNotMet, s.ClaimDaily(T0));
            foreach (var t in tasks.Take(3))
                s.State.DailyProgress[(int)t] = s.TaskTimes(t);
            long sparks = s.State.Wallet[ResourceType.Sparks];
            Assert.AreEqual(Fail.None, s.ClaimDaily(T0));
            Assert.AreEqual(sparks + s.Balance.Activities.Daily.TaskSparks, s.State.Wallet[ResourceType.Sparks]);
            Assert.AreEqual(Fail.AlreadyQueued, s.ClaimDaily(T0));
        }

        [Test]
        public void ActionsCountTowardsTasks()
        {
            var s = Fortress(5);
            s.State.Rooms.First(r => r.Type == RoomType.CoalMine).Stock = 100;
            s.Collect(s.State.Rooms.First(r => r.Type == RoomType.CoalMine).Id);
            Assert.AreEqual(1, s.State.DailyProgress[(int)DailyTask.Collect]);
        }

        [Test]
        public void AchievementsPayOncePerGoal()
        {
            var s = Fortress(1);
            s.Advance(T0 + 1);
            long first = s.State.Wallet[ResourceType.Sparks];
            s.State.HearthLevel = 5;
            s.CheckAchievements();
            long after = s.State.Wallet[ResourceType.Sparks];
            Assert.AreEqual(first + 4 * 60, after, "hearth 2-5: four achievements");
            s.CheckAchievements();
            Assert.AreEqual(after, s.State.Wallet[ResourceType.Sparks]);
        }

        [Test]
        public void TheCalendarAndAchievementsAddUpToTheEconomy()
        {
            var b = TestData.Balance();
            Assert.AreEqual(28, b.Activities.Daily.Calendar.Count, "28 gifts");
            Assert.AreEqual(b.Activities.Daily.CalendarSparks,
                b.Activities.Daily.Calendar.Where(g => g.Kind == GiftKind.Sparks).Sum(g => g.Amount), "140 Sparks per 28 days");
            long achievements = Enumerable.Range(2, 29).Sum(l => l <= 18 ? 60 : 90) + 10 * 60 + 5 * 60;
            Assert.AreEqual(b.Activities.Daily.AchievementSparks, achievements);
        }
    }
}
