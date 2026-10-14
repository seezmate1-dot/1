using System;
using System.Linq;
using LastHearth.Core;
using NUnit.Framework;

namespace LastHearth.Core.Tests
{
    public class SessionTests
    {
        const long T0 = TestData.Start;

        [Test]
        public void NewGameClaimsStarterRewardsUpToTheWarehouse()
        {
            var s = TestData.NewSession();
            long materials = s.State.Wallet[ResourceType.Silver] + s.State.Wallet[ResourceType.Iron];
            Assert.AreEqual(s.Storage(), materials, "the warehouse is full");
            long waiting = s.State.Rewards[ResourceType.Silver] + s.State.Rewards[ResourceType.Iron];
            Assert.AreEqual(s.Balance.Starter.Materials - materials, waiting, "the rest waits in the rewards");
            Assert.AreEqual(s.Balance.Starter.Essence, s.State.Wallet[ResourceType.Essence]);
        }

        [Test]
        public void TheFirstHearthLevelTakesTenSeconds()
        {
            var s = TestData.NewSession();
            Assert.AreEqual(Fail.None, s.UpgradeHearth(T0));
            s.Advance(T0 + 9);
            Assert.AreEqual(1, s.State.HearthLevel);
            s.Advance(T0 + 10);
            Assert.AreEqual(2, s.State.HearthLevel);
        }

        [Test]
        public void RoomsCannotOutgrowTheHearth()
        {
            var s = TestData.NewSession();
            var kitchen = s.State.Rooms.First(r => r.Type == RoomType.Kitchen);
            Assert.AreEqual(Fail.AboveHearth, s.UpgradeRoom(kitchen.Id, T0));
        }

        [Test]
        public void OnlyTheLastFiveMinutesAreFree()
        {
            var s = TestData.NewSession();
            s.State.HearthLevel = 8;
            s.State.Queue.EnsureBuilders(s.BuilderCount());
            var kitchen = s.State.Rooms.First(r => r.Type == RoomType.Kitchen);
            kitchen.Level = 7;
            s.Deposit(ResourceType.Silver, 1000);
            s.Deposit(ResourceType.Iron, 1000);
            Assert.AreEqual(Fail.None, s.UpgradeRoom(kitchen.Id, T0)); // 15 minutes
            var job = s.State.Queue.AllJobs().First();
            Assert.AreEqual(Fail.NotRunning, s.FreeFinish(job.Id, T0 + 60));
            Assert.AreEqual(Fail.None, s.FreeFinish(job.Id, T0 + 600));
            Assert.AreEqual(8, kitchen.Level);
        }

        [Test]
        public void RoomsStoreAtMostTenHoursOfOutput()
        {
            var s = TestData.NewSession();
            var mine = s.State.Rooms.First(r => r.Type == RoomType.CoalMine);
            double rate = s.RoomOutputPerHour(mine);
            Assert.Greater(rate, 0);
            s.State.Wallet[ResourceType.Coal] = 100000; // plenty of coal: rooms stay warm
            s.Advance(T0 + 20 * 3600);
            Assert.AreEqual(rate * s.Balance.OfflineCapHours, mine.Stock, 1e-6);
        }

        [Test]
        public void WhatDoesNotFitTheWarehouseWaitsInTheRoom()
        {
            var s = TestData.NewSession();
            s.State.HearthLevel = 3;
            var mine = new Room { Id = 99, Type = RoomType.Mine, Level = 3, Floor = s.Balance.Grid.HearthFloor, Cell = 5 };
            s.State.Rooms.Add(mine);
            mine.Stock = 10000;
            s.State.Wallet[ResourceType.Iron] = 0;
            long room = s.Capacity(ResourceType.Iron) - s.State.Wallet[ResourceType.Iron];
            long taken = s.Collect(mine.Id);
            Assert.AreEqual(room, taken);
            Assert.AreEqual(10000 - room, mine.Stock, 1e-9);
        }

        [Test]
        public void WithoutCoalEveryRoomFreezes()
        {
            var s = TestData.NewSession();
            var mine = s.State.Rooms.First(r => r.Type == RoomType.CoalMine);
            double warm = s.RoomOutputPerHour(mine);
            s.State.Wallet[ResourceType.Coal] = 0;
            s.Advance(T0 + 3600);
            Assert.IsTrue(s.State.OutOfCoal);
            Assert.AreEqual(warm * s.Balance.Warmth.FrozenOutput, s.RoomOutputPerHour(mine), 1e-9);
        }

        [Test]
        public void TickingEverySecondCostsTheSameAsOneStep()
        {
            var a = TestData.NewSession();
            var b = TestData.NewSession();
            for (long t = 1; t <= 3600; t++)
                a.Advance(T0 + t);
            b.Advance(T0 + 3600);
            Assert.AreEqual(b.State.Wallet[ResourceType.Coal], a.State.Wallet[ResourceType.Coal]);
            Assert.AreEqual(b.State.Wallet[ResourceType.Food], a.State.Wallet[ResourceType.Food]);
            for (int i = 0; i < a.State.Rooms.Count; i++)
                Assert.AreEqual(b.State.Rooms[i].Stock, a.State.Rooms[i].Stock, 1e-6);
        }

        [Test]
        public void SaveRoundTripAndTamperCheck()
        {
            var s = TestData.NewSession();
            s.UpgradeHearth(T0);
            string json = SaveSystem.ToJson(s.State);
            var loaded = SaveSystem.FromJson(json);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(json, SaveSystem.ToJson(loaded));
            string tampered = json.Replace("\\\"HearthLevel\\\":1", "\\\"HearthLevel\\\":30");
            Assert.AreNotEqual(json, tampered, "the test must change the save");
            Assert.IsNull(SaveSystem.FromJson(tampered));
        }

        [Test]
        public void SameCommandsGiveTheSameFortress()
        {
            string Play()
            {
                var s = TestData.NewSession();
                FirstSessionBot.Play(s, T0, T0 + 1800, 10);
                return SaveSystem.ToJson(s.State);
            }
            Assert.AreEqual(Play(), Play());
        }
    }

    public class AntiRushTests
    {
        const long T0 = TestData.Start;

        /// <summary>A fortress at hearth 10 that meets every rule (with the warehouse at this level).</summary>
        static GameSession Ready(int warehouse = 10)
        {
            var s = TestData.NewSession();
            var b = s.Balance;
            s.State.HearthLevel = 10;
            s.State.Queue.EnsureBuilders(s.BuilderCount());
            int floor = b.Grid.HearthFloor;
            s.State.Rooms.Add(new Room { Id = 50, Type = RoomType.Warehouse, Level = warehouse, Floor = floor + 1, Cell = 0 });
            s.State.Rooms.Add(new Room { Id = 51, Type = b.Level(10).KeyRoom, Level = 10, Floor = floor + 1, Cell = 1 });
            for (int i = 0; i < 5; i++)
            {
                var d = s.AddDweller("Герой " + i, new[] { 10, 10, 10, 10, 10 });
                d.HeroClass = HeroClass.Swordsman;
                d.HeroLevel = b.Level(10).HeroRequirement;
            }
            s.Deposit(ResourceType.Silver, 100000);
            s.Deposit(ResourceType.Iron, 100000);
            return s;
        }

        [Test]
        public void AReadyFortressMayRaiseTheHearth()
        {
            var s = Ready();
            CollectionAssert.IsEmpty(s.HearthRequirements(T0));
            Assert.AreEqual(Fail.None, s.UpgradeHearth(T0));
        }

        [Test]
        public void AWeakWarehouseBlocksTheHearth()
        {
            var s = Ready(warehouse: 9);
            var unmet = s.HearthRequirements(T0);
            Assert.IsTrue(unmet.Any(r => r.Kind == RequirementKind.Warehouse));
            Assert.IsTrue(unmet.Any(r => r.Kind == RequirementKind.Materials), "the price does not fit the old warehouse");
            Assert.AreEqual(Fail.RulesNotMet, s.UpgradeHearth(T0));
        }

        [Test]
        public void TheKeyRoomOfTheLevelIsRequired()
        {
            var s = Ready();
            s.State.Rooms.First(r => r.Id == 51).Level = 9;
            var unmet = s.HearthRequirements(T0);
            Assert.IsTrue(unmet.Any(r => r.Kind == RequirementKind.KeyRoom && r.Room == s.Balance.Level(10).KeyRoom));
        }

        [Test]
        public void WeakHeroesBlockTheHearth()
        {
            var s = Ready();
            s.State.Dwellers.First(d => d.Name == "Герой 0").HeroLevel -= 1;
            var unmet = s.HearthRequirements(T0);
            var heroes = unmet.Single(r => r.Kind == RequirementKind.Heroes);
            Assert.AreEqual(4, heroes.Have);
            Assert.AreEqual(5, heroes.Need);
        }
    }

    /// <summary>
    /// A scripted player for the first session: collects, claims, builds every room type as it
    /// unlocks, raises rooms and the hearth, taps the free finish. Used to check the core against the
    /// pacing model (hearth 5 in about 22 minutes, hearth 7 in about 67, docs/ECONOMY.md section 3).
    /// </summary>
    public static class FirstSessionBot
    {
        static readonly RoomType[] BuildOrder =
        {
            RoomType.Barracks, RoomType.Warehouse, RoomType.Mine, RoomType.TradingPost, RoomType.Forge,
            RoomType.Infirmary, RoomType.Library, RoomType.TrainingHall,
        };

        public static void Play(GameSession s, long from, long to, long step)
        {
            for (long now = from; now <= to; now += step)
                Turn(s, now);
        }

        public static void Turn(GameSession s, long now)
        {
            s.Advance(now);
            s.ClaimRewards();
            s.CollectAll();
            foreach (var job in s.State.Queue.AllJobs().ToList())
                s.FreeFinish(job.Id, now);

            foreach (var type in BuildOrder)
            {
                if (s.CountRooms(type) > 0 || s.State.HearthLevel < s.Balance.Room(type).Unlock)
                    continue;
                var cell = FreeCell(s);
                if (cell.HasValue)
                    s.BuildRoom(type, cell.Value.floor, cell.Value.cell, now, out _);
            }

            if (s.UpgradeHearth(now) == Fail.None)
                return;
            foreach (var room in s.State.Rooms.OrderBy(r => r.Level).ToList())
            {
                if (s.UpgradeRoom(room.Id, now) == Fail.QueueFull)
                    break;
            }
        }

        static (int floor, int cell)? FreeCell(GameSession s)
        {
            var g = s.Balance.Grid;
            for (int d = 0; d < g.Floors; d++)
            {
                foreach (int floor in new[] { g.HearthFloor + d, g.HearthFloor - d })
                {
                    for (int cell = 0; cell < g.Cells; cell++)
                    {
                        if (s.CellFree(floor, cell) &&
                            Formulas.IsFloorHabitable(s.Balance, s.State.HearthLevel, s.State.Mode, floor, s.State.HearthLevel))
                            return (floor, cell);
                    }
                }
            }
            return null;
        }
    }

    public class FirstSessionTests
    {
        [Test]
        public void TheFirstSessionIsFastLikeTheModelSays()
        {
            var s = TestData.NewSession();
            long t0 = TestData.Start;
            FirstSessionBot.Play(s, t0, t0 + 30 * 60, 10);
            Assert.GreaterOrEqual(s.State.HearthLevel, 5, "hearth 5 within 30 minutes");
            FirstSessionBot.Play(s, t0 + 30 * 60, t0 + 90 * 60, 10);
            Assert.GreaterOrEqual(s.State.HearthLevel, 7, "hearth 7 within the first 90-minute session");
            TestContext.WriteLine("After 90 minutes: hearth " + s.State.HearthLevel + ", rooms " +
                                  string.Join(", ", s.State.Rooms.Select(r => r.Type + " " + r.Level)));
        }
    }
}
