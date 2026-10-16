using System.Linq;
using LastHearth.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace LastHearth.Core.Tests
{
    /// <summary>GDD 6 and 8: sections of the mountain, obstacles, merged rooms, births in the living quarters.</summary>
    public class FortressTests
    {
        const long T0 = TestData.Start;

        static int H => TestData.Balance().Grid.HearthFloor;

        [Test]
        public void ANewFortressStandsOnClearedCellsWithRubbleAround()
        {
            var s = TestData.NewSession();
            foreach (var room in s.State.Rooms)
                Assert.AreEqual(CellState.Room, s.CellStateAt(room.Floor, room.Cell));
            var g = s.Balance.Grid;
            var cells = Enumerable.Range(0, g.Floors).SelectMany(f => Enumerable.Range(0, g.Cells).Select(c => s.CellStateAt(f, c))).ToList();
            Assert.AreEqual(0, cells.Count(c => c == CellState.Free), "nothing is dug out yet");
            Assert.AreEqual(7, cells.Count(c => c == CellState.Obstacle), "the first section around the hearth is rubble");
            Assert.AreEqual(CellState.Obstacle, s.CellStateAt(H + 1, 2), "right above the hearth");
            Assert.IsFalse(s.CellFree(H + 1, 2));
        }

        [Test]
        public void ABuilderClearsAnObstacleForCoalAndFoodThenTheCellIsBuildable()
        {
            var s = TestData.NewSession();
            var cost = s.ClearCost(H + 1, 2);
            long seconds = s.ClearSeconds(H + 1, 2);
            Assert.AreEqual(s.Balance.Obstacles.Coal[0], cost[ResourceType.Coal]);
            Assert.AreEqual(s.Balance.Obstacles.Food[0], cost[ResourceType.Food]);
            Assert.AreEqual(0, cost[ResourceType.Silver] + cost[ResourceType.Iron], "rubble costs no materials");
            Assert.AreEqual(s.Balance.Obstacles.Seconds[0], seconds);
            long coal = s.State.Wallet[ResourceType.Coal], food = s.State.Wallet[ResourceType.Food];

            Assert.AreEqual(Fail.CellTaken, s.BuildRoom(RoomType.Bedrooms, H + 1, 2, T0, out _), "rubble first");
            Assert.AreEqual(Fail.None, s.ClearObstacle(H + 1, 2, T0));
            Assert.AreEqual(coal - cost[ResourceType.Coal], s.State.Wallet[ResourceType.Coal]);
            Assert.AreEqual(food - cost[ResourceType.Food], s.State.Wallet[ResourceType.Food]);
            var job = s.State.Queue.AllJobs().Single();
            Assert.IsTrue(job.IsClearing);
            Assert.AreEqual(seconds, job.End - job.Start);
            Assert.AreEqual(1, s.State.Queue.Builders[0].Jobs.Count, "the builder of the queue does it");
            Assert.AreEqual(CellState.Clearing, s.CellStateAt(H + 1, 2));
            Assert.AreEqual(Fail.AlreadyQueued, s.ClearObstacle(H + 1, 2, T0));

            s.Advance(T0 + seconds - 1);
            Assert.AreEqual(CellState.Clearing, s.CellStateAt(H + 1, 2));
            s.Advance(T0 + seconds);
            Assert.AreEqual(CellState.Free, s.CellStateAt(H + 1, 2));
            Assert.AreEqual(Fail.None, s.BuildRoom(RoomType.Bedrooms, H + 1, 2, T0 + seconds, out var built));
            Assert.AreEqual(CellState.Room, s.CellStateAt(H + 1, 2));
            Assert.AreEqual(RoomType.Bedrooms, built.Type);
            Assert.AreEqual(Fail.CellTaken, s.ClearObstacle(H + 1, 2, T0 + seconds));
        }

        [Test]
        public void ClearingCostsMoreFarFromTheHearthAndAtAHigherHearth()
        {
            var s = TestData.NewSession();
            s.State.HearthLevel = 20;
            var near = s.ClearCost(H + 1, 2);
            var far = s.ClearCost(9, 0);
            Assert.Greater(far[ResourceType.Coal], near[ResourceType.Coal]);
            Assert.Greater(s.ClearSeconds(9, 0), s.ClearSeconds(H + 1, 2));
            var low = TestData.NewSession();
            Assert.Greater(near[ResourceType.Coal], low.ClearCost(H + 1, 2)[ResourceType.Coal]);
            Assert.Greater(s.ClearSeconds(H + 1, 2), low.ClearSeconds(H + 1, 2));
        }

        [Test]
        public void SectionsOfTheMountainOpenWithTheHearthLevel()
        {
            var s = TestData.NewSession();
            Assert.AreEqual(CellState.Locked, s.CellStateAt(H, 0), "the hearth floor's walls open at hearth 3");
            Assert.AreEqual(3, s.SectionLevel(H, 0));
            Assert.AreEqual(Fail.Locked, s.ClearObstacle(H, 0, T0));
            Assert.AreEqual(CellState.Locked, s.CellStateAt(0, 0));
            s.State.HearthLevel = 3;
            Assert.AreEqual(CellState.Obstacle, s.CellStateAt(H, 0));
            Assert.AreEqual(CellState.Locked, s.CellStateAt(H + 2, 2));
            s.State.HearthLevel = 5;
            Assert.AreEqual(CellState.Obstacle, s.CellStateAt(H + 2, 2));
            Assert.IsTrue(s.SectionOpensAt(5));
            Assert.IsFalse(s.SectionOpensAt(6));

            // the whole mountain is open by hearth 20, and every section is warm when it opens
            var g = s.Balance.Grid;
            s.State.HearthLevel = 20;
            for (int f = 0; f < g.Floors; f++)
            {
                for (int c = 0; c < g.Cells; c++)
                    Assert.AreNotEqual(CellState.Locked, s.CellStateAt(f, c), "floor " + f + " cell " + c);
            }
            foreach (var section in g.Sections)
            {
                foreach (int f in new[] { section.FloorFrom, section.FloorTo })
                    Assert.IsTrue(Formulas.IsFloorHabitable(s.Balance, section.Hearth, HearthMode.Steady, f, section.Hearth), "hearth " + section.Hearth);
            }
        }

        static Room Put(GameSession s, RoomType type, int floor, int cell, int level = 1)
        {
            var room = new Room { Id = s.State.NextRoomId++, Type = type, Level = level, Floor = floor, Cell = cell };
            s.State.Rooms.Add(room);
            return room;
        }

        [Test]
        public void SameRoomsSideBySideMergeIntoABiggerHall()
        {
            var s = TestData.NewSession();
            s.State.HearthLevel = 5;
            s.State.Wallet[ResourceType.Coal] = 100000;
            var a = Put(s, RoomType.Mine, H + 1, 0);
            var b = Put(s, RoomType.Mine, H + 1, 1);
            var worker = s.AddDweller("Рудокоп", new[] { 10, 10, 10, 10, 10 });
            Assert.AreEqual(Fail.None, s.Assign(worker.Id, b.Id));
            double one = s.RoomOutputPerHour(b);
            var single = s.RoomUpgradeCost(a);
            b.Stock = 5;

            Assert.AreSame(b, s.MergeCandidate(a));
            Assert.AreEqual(Fail.None, s.MergeRooms(b.Id, a.Id, T0));
            Assert.IsNull(s.FindRoom(b.Id), "the left room stays");
            Assert.AreEqual(2, a.Width);
            Assert.AreEqual(0, a.Cell);
            Assert.AreSame(a, s.RoomAt(H + 1, 1));
            Assert.AreEqual(a.Id, worker.RoomId);
            Assert.AreEqual(5, a.Stock, 1e-9);
            Assert.AreEqual(2 * s.Balance.Production.WorkersPerRoom, s.WorkerPlaces(a));
            Assert.AreEqual(one * (1 + s.Balance.Merge.BonusPerCell), s.RoomOutputPerHour(a), 1e-9);
            Assert.AreEqual(2 * single[ResourceType.Silver], s.RoomUpgradeCost(a)[ResourceType.Silver]);
            Assert.AreEqual(2, s.CountCells(RoomType.Mine));
            Assert.AreEqual(1, s.CountRooms(RoomType.Mine));

            s.State.Cleared.Add(GameSession.CellKey(H + 1, 2));
            Assert.AreEqual(Fail.LimitReached, s.BuildRoom(RoomType.Mine, H + 1, 2, T0, out _), "the limit counts cells");
        }

        [Test]
        public void OnlyNeighboursOfOneTypeAndLevelMergeUpToThreeCells()
        {
            var s = TestData.NewSession();
            s.State.HearthLevel = 10;
            var a = Put(s, RoomType.Kitchen, H + 1, 0);
            var mine = Put(s, RoomType.CoalMine, H + 1, 1);
            Assert.AreEqual(Fail.NotMergeable, s.MergeRooms(a.Id, mine.Id, T0), "different types");
            var far = Put(s, RoomType.Kitchen, H + 1, 3);
            Assert.AreEqual(Fail.NotMergeable, s.MergeRooms(a.Id, far.Id, T0), "not side by side");
            var b = Put(s, RoomType.Kitchen, H + 1, 4, level: 2);
            Assert.AreEqual(Fail.NotMergeable, s.MergeRooms(far.Id, b.Id, T0), "different levels");
            Assert.IsNull(s.MergeCandidate(far));
            b.Level = 1;
            var c = Put(s, RoomType.Kitchen, H + 1, 5);
            Assert.AreEqual(Fail.None, s.MergeRooms(far.Id, b.Id, T0));
            Assert.AreEqual(Fail.None, s.MergeRooms(far.Id, c.Id, T0));
            Assert.AreEqual(3, far.Width);
            var d = Put(s, RoomType.Kitchen, H + 1, 2);
            Assert.AreEqual(Fail.LimitReached, s.MergeRooms(far.Id, d.Id, T0), "three cells at most");

            s.State.Rooms.Remove(mine);
            var e = Put(s, RoomType.Kitchen, H + 1, 1);
            s.Deposit(ResourceType.Silver, 100000);
            s.Deposit(ResourceType.Iron, 100000);
            Assert.AreEqual(Fail.None, s.UpgradeRoom(a.Id, T0));
            Assert.AreEqual(Fail.AlreadyQueued, s.MergeRooms(a.Id, e.Id, T0), "not while it is being built");
        }

        static (GameSession s, Room home, Dweller man, Dweller woman) Couple(int hearth = 10)
        {
            var s = TestData.NewSession();
            s.State.HearthLevel = hearth;
            s.State.Wallet[ResourceType.Coal] = 100000;
            s.State.Wallet[ResourceType.Food] = 100000;
            var home = s.State.Rooms.First(r => r.Type == RoomType.Bedrooms);
            var man = s.AddDweller("Ярослав", new[] { 10, 20, 6, 15, 8 });
            var woman = s.AddDweller("Милава", new[] { 14, 10, 9, 15, 30 });
            return (s, home, man, woman);
        }

        [Test]
        public void AManAndAWomanInTheLivingQuartersHaveAChild()
        {
            var (s, home, man, woman) = Couple();
            Assert.AreEqual(Sex.Male, man.Sex);
            Assert.AreEqual(Sex.Female, woman.Sex);
            var tim = s.State.Dwellers.First(d => d.Name == "Тим");
            Assert.AreEqual(Fail.NotAPair, s.StartPairing(home.Id, man.Id, tim.Id, T0), "two men");
            Assert.AreEqual(Fail.NotAPair, s.StartPairing(home.Id, woman.Id, man.Id, T0), "the man first");
            var kitchen = s.State.Rooms.First(r => r.Type == RoomType.Kitchen);
            Assert.AreEqual(Fail.Locked, s.StartPairing(kitchen.Id, man.Id, woman.Id, T0), "only in the living quarters");

            int before = s.State.Dwellers.Count;
            Dweller born = null;
            s.ChildBorn += d => born = d;
            Assert.AreEqual(Fail.None, s.StartPairing(home.Id, man.Id, woman.Id, T0));
            Assert.AreEqual(home.Id, man.RoomId);
            Assert.AreEqual(home.Id, woman.RoomId);
            long seconds = s.BirthSeconds(home);
            Assert.AreEqual((long)(s.Balance.Births.Hours * 3600), seconds, "living quarters of level 1");
            Assert.AreEqual(Fail.RoomFull, s.StartPairing(home.Id, s.AddDweller("Мирон", new int[5]).Id,
                s.AddDweller("Дарина", new int[5]).Id, T0), "one couple per cell");
            before += 2;

            s.Advance(T0 + seconds - 1);
            Assert.IsNull(born);
            s.Advance(T0 + seconds);
            Assert.IsNotNull(born);
            Assert.AreEqual(before + 1, s.State.Dwellers.Count);
            Assert.AreNotEqual(Sex.Unknown, born.Sex);
            Assert.IsFalse(string.IsNullOrEmpty(born.Name));
            CollectionAssert.IsEmpty(s.State.Pairings);
            Assert.AreEqual(home.Id, man.RoomId, "the parents stay until the player moves them");
        }

        [Test]
        public void ChildrenTakeAfterTheirParents()
        {
            var (s, home, man, woman) = Couple(hearth: 30);
            var b = s.Balance.Births;
            long now = T0;
            int gifted = 0;
            for (int i = 0; i < 25; i++)
            {
                Assert.AreEqual(Fail.None, s.StartPairing(home.Id, man.Id, woman.Id, now), "birth " + i);
                now += s.BirthSeconds(home);
                s.Advance(now);
                var child = s.State.Dwellers.Last();
                int extra = 0;
                for (int k = 0; k < 5; k++)
                {
                    double average = (man.Stats[k] + woman.Stats[k]) / 2.0;
                    int over = child.Stats[k] - (int)System.Math.Round(average, System.MidpointRounding.AwayFromZero);
                    Assert.GreaterOrEqual(over, -b.StatSpread, "stat " + k);
                    if (over > b.StatSpread)
                    {
                        Assert.LessOrEqual(over, b.StatSpread + b.GiftBonus);
                        extra++;
                    }
                }
                Assert.LessOrEqual(extra, 1, "at most one gifted stat");
                gifted += extra;
                if (s.State.Dwellers.Count + 1 >= Formulas.DwellerCap(s.Balance, s.State.HearthLevel))
                    break;
            }
            Assert.Less(gifted, 10, "a gift is rare");
            home.Level = 10;
            Assert.Less(s.BirthSeconds(home), (long)(b.Hours * 3600), "better living quarters are faster");
        }

        [Test]
        public void BirthsStayUnderTheDwellerCap()
        {
            var (s, home, man, woman) = Couple(hearth: 1);
            int cap = Formulas.DwellerCap(s.Balance, 1);
            while (s.State.Dwellers.Count < cap - 1)
                s.AddDweller("Беженец", new[] { 5, 5, 5, 5, 5 });
            Assert.AreEqual(Fail.None, s.StartPairing(home.Id, man.Id, woman.Id, T0), "one place left");
            Assert.IsTrue(s.PopulationFull(), "the child on the way holds the last place");
            s.AddDweller("Странник", new[] { 5, 5, 5, 5, 5 }); // someone came anyway
            long end = T0 + s.BirthSeconds(home);
            s.Advance(end + 3600);
            Assert.AreEqual(cap, s.State.Dwellers.Count, "no child over the cap");
            Assert.AreEqual(1, s.State.Pairings.Count, "the child waits for room");
            s.State.HearthLevel = 2;
            s.Advance(end + 7200);
            Assert.AreEqual(cap + 1, s.State.Dwellers.Count);

            var (full, home2, m2, w2) = Couple(hearth: 1);
            while (full.State.Dwellers.Count < cap)
                full.AddDweller("Беженец", new[] { 5, 5, 5, 5, 5 });
            Assert.AreEqual(Fail.LimitReached, full.StartPairing(home2.Id, m2.Id, w2.Id, T0));
        }

        [Test]
        public void ACoupleThatLeavesTheRoomHasNoChild()
        {
            var (s, home, man, woman) = Couple();
            Assert.AreEqual(Fail.None, s.StartPairing(home.Id, man.Id, woman.Id, T0));
            s.Unassign(woman.Id);
            int count = s.State.Dwellers.Count;
            s.Advance(T0 + s.BirthSeconds(home));
            Assert.AreEqual(count, s.State.Dwellers.Count);
            CollectionAssert.IsEmpty(s.State.Pairings);
        }

        [Test]
        public void AnOldSaveLoadsWithDugCellsAroundItsRooms()
        {
            var fresh = TestData.NewSession();
            var json = JObject.Parse(JsonConvert.SerializeObject(fresh.State));
            json["SaveVersion"] = 1;
            json.Remove("Cleared");
            json.Remove("Pairings");
            foreach (var room in json["Rooms"])
                ((JObject)room).Remove("Width");
            foreach (var d in json["Dwellers"])
                ((JObject)d).Remove("Sex");
            var state = JsonConvert.DeserializeObject<GameState>(json.ToString());
            Assert.AreEqual(1, state.SaveVersion);

            var s = new GameSession(TestData.Balance(), state);
            Assert.AreEqual(3, state.SaveVersion, "upgraded to the current version");
            Assert.IsTrue(state.Rooms.All(r => r.Width == 1));
            Assert.AreEqual(Sex.Female, state.Dwellers.First(d => d.Name == "Мара").Sex);
            Assert.AreEqual(Sex.Male, state.Dwellers.First(d => d.Name == "Тим").Sex);
            Assert.AreEqual(CellState.Free, s.CellStateAt(H + 1, 1), "above the bedrooms");
            Assert.AreEqual(CellState.Free, s.CellStateAt(H, 0), "beside the bedrooms, dug before sections existed");
            Assert.AreEqual(CellState.Free, s.CellStateAt(H, 5), "beside the kitchen");
            Assert.AreEqual(CellState.Free, s.CellStateAt(H - 2, 2), "under the coal mine");
            Assert.AreEqual(CellState.Obstacle, s.CellStateAt(H + 1, 3), "nothing next to it");
            Assert.AreEqual(CellState.Locked, s.CellStateAt(0, 0));

            // and it saves and loads as a new one
            var again = SaveSystem.FromJson(SaveSystem.ToJson(state));
            Assert.AreEqual(state.Cleared, again.Cleared);
        }
    }
}
