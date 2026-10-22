using System;
using System.Collections.Generic;
using System.Linq;
using LastHearth.Core;
using LastHearth.Core.Combat;
using NUnit.Framework;

namespace LastHearth.Core.Tests
{
    /// <summary>GDD 6: the work of the halls — tavern, infirmary, library, training hall, watchtower, chapel, archive.</summary>
    public class HallsTests
    {
        const long T0 = TestData.Start;

        static int H => TestData.Balance().Grid.HearthFloor;

        /// <summary>A session at a hearth level with plenty of everything in the wallet.</summary>
        static GameSession Rich(int hearth = 20)
        {
            var s = TestData.NewSession();
            s.Combat = TestData.Combat();
            s.State.HearthLevel = hearth;
            foreach (ResourceType r in Enum.GetValues(typeof(ResourceType)))
                s.State.Wallet[r] = 10_000_000;
            return s;
        }

        /// <summary>Puts a finished room straight into the fortress (no builder, no cell checks).</summary>
        static Room Put(GameSession s, RoomType type, int level, int floor, int cell)
        {
            var room = new Room { Id = s.State.NextRoomId++, Type = type, Level = level, Floor = floor, Cell = cell };
            s.State.Rooms.Add(room);
            return room;
        }

        static Dweller Worker(GameSession s, Room room, Stat stat, int value)
        {
            var stats = new[] { 5, 5, 5, 5, 5 };
            stats[(int)stat] = value;
            var d = s.AddDweller("Олег", stats);
            Assert.AreEqual(Fail.None, s.Assign(d.Id, room.Id));
            return d;
        }

        // ------------------------------------------------------------------ data

        [Test]
        public void TheBalanceHoldsTheTreeOfSixtyResearchesAndTheRecords()
        {
            var h = TestData.Balance().Halls;
            Assert.AreEqual(60, h.Library.Nodes.Count, "GDD 6: a tree of 60 researches");
            Assert.AreEqual(4, h.Library.Branches.Count);
            foreach (var b in h.Library.Branches)
            {
                var steps = h.Library.Nodes.Where(n => n.Branch == b.Id).OrderBy(n => n.Tier).ToList();
                Assert.AreEqual(15, steps.Count, b.Id);
                for (int i = 1; i < steps.Count; i++)
                {
                    Assert.Greater(steps[i].Seconds, steps[i - 1].Seconds, "every step takes longer");
                    Assert.GreaterOrEqual(steps[i].Cost, steps[i - 1].Cost, "and costs more");
                    Assert.Greater(steps[i].Gate, steps[i - 1].Gate, "and needs a higher library");
                }
            }
            Assert.AreEqual(h.Library.Nodes.Count, h.Library.Nodes.Select(n => n.Id).Distinct().Count());
            Assert.AreEqual(4, h.Tavern.Rarities.Count);
            Assert.AreEqual(5, h.Chapel.Ranks.Count);
            Assert.GreaterOrEqual(h.Archive.Records.Count, 20);
            Assert.IsTrue(h.Archive.Records.All(r => r.Title.Length > 0 && r.Text.Length > 20));
            Assert.AreEqual(6.0, h.Infirmary.InjuryHours, "the injury of a route: 6 hours without the infirmary");
        }

        // ------------------------------------------------------------------ tavern

        [Test]
        public void WithATavernTheRefugeesOfANewHearthLevelWaitOnItsBoard()
        {
            var s = TestData.NewSession();
            int before = s.State.Dwellers.Count;
            s.State.Queue.EnsureBuilders(1);
            // without a tavern: two refugees come in by themselves
            s.State.Queue.Enqueue(0, BuildJob.HearthTarget, 2, 10, T0);
            s.Advance(T0 + 10);
            Assert.AreEqual(before + 2, s.State.Dwellers.Count);

            Put(s, RoomType.Tavern, 1, H + 1, 2);
            int board = s.TavernBoard(T0 + 10).Count;
            Assert.AreEqual(s.Balance.Halls.Tavern.Board, board);
            s.State.Queue.Enqueue(0, BuildJob.HearthTarget, 3, 10, T0 + 10);
            s.Advance(T0 + 20);
            Assert.AreEqual(before + 2, s.State.Dwellers.Count, "with a tavern nobody comes in for free");
            var waiting = s.TavernBoard(T0 + 20);
            Assert.AreEqual(board + 2, waiting.Count, "the two refugees wait on the board to be hired");
            Assert.IsTrue(waiting.Skip(board).All(r => r.Free && s.HireCost(r)[ResourceType.Silver] == 0 && s.HireCost(r)[ResourceType.Food] == 0),
                "they are taken in for free, like before the tavern");
            Assert.IsTrue(waiting.Take(board).All(r => !r.Free));
            Assert.AreEqual(Fail.None, s.Hire(board, T0 + 20, out _));
            Assert.AreEqual(before + 3, s.State.Dwellers.Count);
        }

        [Test]
        public void TheBoardChangesEverySixHoursAndHiringCostsSilverAndFood()
        {
            var s = Rich(10);
            Assert.AreEqual(0, s.TavernBoard(T0).Count, "no tavern, no board");
            Assert.AreEqual(Fail.Locked, s.Hire(0, T0, out _));
            Put(s, RoomType.Tavern, 10, H + 1, 2);
            var board = s.TavernBoard(T0);
            Assert.AreEqual(s.Balance.Halls.Tavern.Board + 1, board.Count, "+1 recruit at tavern 10");
            string names = string.Join(",", board.Select(r => r.Name + r.Stats.Sum()));
            Assert.AreSame(board, s.TavernBoard(s.NextBoardAt(T0) - 1), "the same board until the next one");
            Assert.AreEqual(s.Balance.Halls.Tavern.BoardHours * 3600L, s.NextBoardAt(T0) - T0 / (6 * 3600) * (6 * 3600));
            var next = s.TavernBoard(s.NextBoardAt(T0));
            Assert.AreNotEqual(names, string.Join(",", next.Select(r => r.Name + r.Stats.Sum())), "a new board");

            long now = s.NextBoardAt(T0);
            var recruit = next[0];
            var cost = s.HireCost(recruit);
            Assert.Greater(cost[ResourceType.Silver], 0);
            Assert.Greater(cost[ResourceType.Food], 0);
            long silver = s.State.Wallet[ResourceType.Silver], food = s.State.Wallet[ResourceType.Food];
            int count = s.State.Dwellers.Count;
            Assert.AreEqual(Fail.None, s.Hire(0, now, out var hired));
            Assert.AreEqual(count + 1, s.State.Dwellers.Count);
            Assert.AreEqual(silver - cost[ResourceType.Silver], s.State.Wallet[ResourceType.Silver]);
            Assert.Less(s.State.Wallet[ResourceType.Food], food);
            Assert.AreEqual(recruit.Name, hired.Name);
            CollectionAssert.AreEqual(recruit.Stats, hired.Stats);
            Assert.AreEqual(recruit.Sex, hired.Sex);
            Assert.AreEqual(recruit.Rarity, s.RarityOf(hired.Id));
            Assert.AreEqual(Fail.AlreadyQueued, s.Hire(0, now, out _), "a recruit is hired once");
            Assert.AreEqual(Fail.NotFound, s.Hire(99, now, out _));

            // a rarer recruit costs more
            var common = new Recruit { Rarity = Rarity.Common };
            var legend = new Recruit { Rarity = Rarity.Legendary };
            Assert.Greater(s.HireCost(legend)[ResourceType.Silver], s.HireCost(common)[ResourceType.Silver]);

            s.State.Wallet[ResourceType.Silver] = 0;
            Assert.AreEqual(Fail.NotEnough, s.Hire(1, now, out _));
            s.State.Wallet[ResourceType.Silver] = 10_000_000;
            while (!s.PopulationFull())
                s.AddDweller("Рада", new[] { 5, 5, 5, 5, 5 });
            Assert.AreEqual(Fail.LimitReached, s.Hire(1, now, out _), "never over the dweller cap");
        }

        [Test]
        public void LuckOfTheTavernMakesRareRecruitsMoreLikely()
        {
            var s = Rich(20);
            var tavern = Put(s, RoomType.Tavern, 1, H + 1, 2);
            var low = s.RarityOdds();
            Assert.AreEqual(1.0, low.Sum(), 1e-9);
            tavern.Level = 20;
            Worker(s, tavern, Stat.Spirit, 60);
            Worker(s, tavern, Stat.Spirit, 60);
            Assert.AreEqual(20 * 1.0 + 12, s.RecruitLuck(), 1e-9, "level + Spirit / 10");
            var high = s.RarityOdds();
            Assert.AreEqual(1.0, high.Sum(), 1e-9);
            Assert.Less(high[(int)Rarity.Common], low[(int)Rarity.Common]);
            Assert.Greater(high[(int)Rarity.Legendary], low[(int)Rarity.Legendary]);
            Assert.Greater(high[(int)Rarity.Rare], low[(int)Rarity.Rare]);

            // over many boards the shares follow the odds; rarer recruits have better stats
            var seen = new List<Recruit>();
            for (int i = 0; i < 300; i++)
                seen.AddRange(s.TavernBoard(T0 + i * 6 * 3600L));
            double legendary = seen.Count(r => r.Rarity == Rarity.Legendary) / (double)seen.Count;
            Assert.AreEqual(high[(int)Rarity.Legendary], legendary, 0.03);
            Assert.Greater(seen.Where(r => r.Rarity == Rarity.Legendary).Average(r => r.Stats.Sum()),
                seen.Where(r => r.Rarity == Rarity.Common).Average(r => r.Stats.Sum()) + 20);
        }

        [Test]
        public void ADwellerCanBeLetGoButNotAHeroOrAParent()
        {
            var s = Rich(10);
            var weak = s.AddDweller("Кир", new[] { 1, 1, 1, 1, 1 });
            var kitchen = s.State.Rooms.First(r => r.Type == RoomType.Kitchen);
            s.Assign(weak.Id, kitchen.Id);
            Assert.AreEqual(Fail.None, s.Dismiss(weak.Id, T0));
            Assert.IsNull(s.FindDweller(weak.Id));
            Assert.IsFalse(kitchen.Workers.Contains(weak.Id));
            var mara = s.State.Dwellers.First(d => d.IsHero);
            Assert.AreEqual(Fail.Locked, s.Dismiss(mara.Id, T0), "heroes stay");
            Assert.AreEqual(Fail.NotFound, s.Dismiss(9999, T0));
        }

        // ------------------------------------------------------------------ infirmary

        [Test]
        public void AnInjuryLastsSixHoursWithoutTheInfirmaryAndLessWithIt()
        {
            var s = Rich(10);
            var d = s.AddDweller("Олег", new[] { 5, 5, 5, 5, 5 });
            s.Advance(T0);
            d.InjuredUntil = T0 + 6 * 3600;
            s.Advance(T0 + 6 * 3600 - 60);
            Assert.Greater(d.InjuredUntil, s.State.Time, "6 hours without the infirmary");
            s.Advance(T0 + 6 * 3600);
            Assert.LessOrEqual(d.InjuredUntil, s.State.Time);

            var t = Rich(10);
            var infirmary = Put(t, RoomType.Infirmary, 5, H + 1, 2);
            Worker(t, infirmary, Stat.Intellect, 25);
            double speed = 1 + 0.05 * 5 + 25 / 50.0;
            Assert.AreEqual(speed, t.HealSpeed(), 1e-9, "1 + 5% per level + Intellect / 50");
            var p = t.AddDweller("Рада", new[] { 5, 5, 5, 5, 5 });
            t.Advance(T0);
            p.InjuredUntil = T0 + 6 * 3600;
            Assert.AreEqual((long)Math.Ceiling(6 * 3600 / speed), t.HealingLeft(p.InjuredUntil, T0));
            long healed = T0 + (long)(6 * 3600 / speed);
            for (long now = T0; now < healed - 120; now += 600)
                t.Advance(now); // healing counts the same in small steps
            Assert.Greater(p.InjuredUntil, healed - 120);
            t.Advance(healed + 60);
            Assert.LessOrEqual(p.InjuredUntil, t.State.Time, "the infirmary heals in 6 h / speed");
            Assert.AreEqual(0, t.Patients(t.State.Time).Count);
        }

        [Test]
        public void TheBlazingHearthAndTheResearchHealFaster()
        {
            var s = Rich(20);
            Assert.AreEqual(1.0, s.HealSpeed());
            s.SetMode(HearthMode.Blazing);
            Assert.AreEqual(s.Balance.Halls.Infirmary.Blazing, s.HealSpeed(), 1e-9, "GDD 7: blazing heals faster");
            s.SetMode(HearthMode.Steady);
            Put(s, RoomType.Infirmary, 10, H + 1, 2);
            double plain = s.HealSpeed();
            s.State.Halls.Researched.Add("Household3");
            Assert.AreEqual(plain + s.ResearchNode("Household3").Value, s.HealSpeed(), 1e-9);
        }

        static int IllSeed()
        {
            double chance = TestData.Balance().Halls.Infirmary.IllnessChance;
            for (int seed = 1; ; seed++)
            {
                if (new Rng(seed * 31 + 17).Next() < chance * 0.2) // ill even after the best rests on the way
                    return seed;
            }
        }

        [Test]
        public void TheIllnessFromTheFogHalvesTheWorkUntilHealed()
        {
            var s = Rich(10);
            var mine = s.State.Rooms.First(r => r.Type == RoomType.CoalMine);
            var miner = s.FindDweller(mine.Workers[0]);
            Unassign(s, miner);
            double healthy = WithWorker(s, mine, miner);
            s.Advance(T0);
            Unassign(s, miner);
            var e = new Expedition { Id = s.State.NextExpeditionId++, Team = new List<int> { miner.Id }, Start = T0 - 3600, End = T0 + 60, Seed = IllSeed(), HearthLevel = 10 };
            miner.RouteId = e.Id;
            s.State.Expeditions.Add(e);
            s.Advance(T0 + 60);
            Assert.IsTrue(e.Back);
            Assert.IsTrue(s.IsSick(miner.Id, T0 + 60), "the team brought the illness back");
            Assert.AreEqual(T0 + 60 + 12 * 3600, s.SickUntil(miner.Id));
            Assert.AreEqual(healthy * s.Balance.Halls.Infirmary.SickOutput, WithWorker(s, mine, miner), 1e-6, "a sick worker gives half");
            s.Advance(T0 + 60 + 12 * 3600);
            Assert.IsFalse(s.IsSick(miner.Id, s.State.Time));
            Assert.AreEqual(healthy, WithWorker(s, mine, miner), 1e-6);
            while (s.State.Expeditions.Contains(e))
                s.ResolveRouteCard(e.Id, 2, s.State.Time);
            s.Advance(s.State.Time + 1);
            Assert.AreEqual(0, s.State.Halls.RoutesChecked.Count, "the checked list is cleaned");
        }

        static void Unassign(GameSession s, Dweller d) => s.Unassign(d.Id);

        static double WithWorker(GameSession s, Room room, Dweller d)
        {
            if (!room.Workers.Contains(d.Id))
                s.Assign(d.Id, room.Id);
            return s.RoomOutputPerHour(room);
        }

        // ------------------------------------------------------------------ library

        [Test]
        public void AResearchNeedsTheLibraryAndTheStepBeforeAndTakesItsTime()
        {
            var s = Rich(20);
            Assert.AreEqual(Fail.Locked, s.StartResearch("Crafts1", T0), "no library");
            var library = Put(s, RoomType.Library, 1, H + 1, 2);
            Assert.AreEqual(Fail.Locked, s.StartResearch("Crafts2", T0), "the step before first");
            Assert.AreEqual(Fail.NotFound, s.StartResearch("Nope", T0));
            var node = s.ResearchNode("Crafts1");
            var cost = s.ResearchCost(node);
            Assert.AreEqual(node.Cost, cost[ResourceType.Silver] + cost[ResourceType.Iron]);
            long silver = s.State.Wallet[ResourceType.Silver];
            Assert.AreEqual(Fail.None, s.StartResearch("Crafts1", T0));
            Assert.AreEqual(silver - cost[ResourceType.Silver], s.State.Wallet[ResourceType.Silver]);
            Assert.AreEqual(Fail.QueueFull, s.StartResearch("War1", T0), "one research at a time");
            Assert.AreEqual(T0 + node.Seconds, s.ResearchEnd);
            s.Advance(T0 + node.Seconds - 1);
            Assert.IsFalse(s.Researched("Crafts1"));
            s.Advance(T0 + node.Seconds);
            Assert.IsTrue(s.Researched("Crafts1"));
            Assert.IsNull(s.CurrentResearch);
            Assert.AreEqual(Fail.AlreadyQueued, s.StartResearch("Crafts1", s.State.Time));
            Assert.AreEqual(Fail.Locked, s.StartResearch("Crafts3", s.State.Time), "step 3 needs library 5");
            library.Level = 5;
            Assert.AreEqual(Fail.None, s.StartResearch("Crafts2", s.State.Time));

            // scholars make it faster
            var t = Rich(20);
            var lib = Put(t, RoomType.Library, 1, H + 1, 2);
            Worker(t, lib, Stat.Intellect, 50);
            Assert.AreEqual((long)Math.Ceiling(node.Seconds / 1.5), t.ResearchSeconds(node), "+ Intellect / 100");
            Assert.AreEqual(4, t.ResearchFront().Count, "the first step of every branch");
        }

        [Test]
        public void DiamondsFinishAResearchAndTheLastFiveMinutesAreFree()
        {
            var s = Rich(20);
            Put(s, RoomType.Library, 30, H + 1, 2);
            var node = s.ResearchNode("War10");
            s.State.Halls.Researched.AddRange(Enumerable.Range(1, 9).Select(i => "War" + i));
            Assert.AreEqual(Fail.NotRunning, s.SpeedUpResearch(T0));
            Assert.AreEqual(Fail.None, s.StartResearch(node.Id, T0));
            long price = s.ResearchSpeedupCost(T0);
            Assert.AreEqual(Formulas.SpeedupCost(s.Balance, node.Seconds), price);
            long sparks = s.State.Wallet[ResourceType.Sparks];
            Assert.AreEqual(Fail.None, s.SpeedUpResearch(T0));
            Assert.AreEqual(sparks - price, s.State.Wallet[ResourceType.Sparks]);
            Assert.IsTrue(s.Researched(node.Id));
            Assert.AreEqual(Fail.None, s.StartResearch("War11", T0));
            Assert.AreEqual(0, s.ResearchSpeedupCost(s.ResearchEnd - 300), "the last 5 minutes are free");
        }

        [Test]
        public void ResearchBonusesReachProductionTheHearthAndBattle()
        {
            var s = Rich(20);
            var mine = s.State.Rooms.First(r => r.Type == RoomType.CoalMine);
            double coal = s.RoomOutputPerHour(mine);
            double burn = s.CoalBurnPerHour();
            double defense = s.DefenseMult();
            var hero = s.State.Dwellers.First(d => d.IsHero);
            var before = s.MissionSetup(0, 1).Heroes.Single(h => h.Id == hero.Id);

            foreach (string id in new[] { "Crafts1", "Crafts3", "Household1", "Household2", "War1", "War2", "Lore3" })
                s.State.Halls.Researched.Add(id);
            double supply = s.ResearchValue(ResearchEffect.Supply);
            Assert.AreEqual(2 * s.ResearchNode("Crafts1").Value, supply, 1e-9);
            Assert.AreEqual(coal * (1 + supply), s.RoomOutputPerHour(mine), 1e-6, "coal and food output");
            Assert.AreEqual(burn * (1 - s.ResearchNode("Household1").Value) / 1.0, s.CoalBurnPerHour(), burn * 0.001, "coal burn");
            Assert.AreEqual(1 - s.ResearchNode("Household2").Value, s.FoodEatMult(), 1e-9, "the dwellers eat less");
            Assert.AreEqual(defense + s.ResearchNode("Lore3").Value, s.DefenseMult(), 1e-9, "Surge defenders");
            var after = s.MissionSetup(0, 1).Heroes.Single(h => h.Id == hero.Id);
            Assert.AreEqual(before.AttackMult * (1 + s.ResearchNode("War1").Value), after.AttackMult, 1e-9, "squad attack");
            Assert.AreEqual(before.HpMult * (1 + s.ResearchNode("War2").Value), after.HpMult, 1e-9, "squad health");

            // the dwellers eat less: food over a day
            var a = Rich(20);
            var b = Rich(20);
            b.State.Halls.Researched.Add("Household2");
            a.Advance(T0 + 86400);
            b.Advance(T0 + 86400);
            Assert.Greater(b.State.Wallet[ResourceType.Food], a.State.Wallet[ResourceType.Food]);
        }

        // ------------------------------------------------------------------ training hall

        [Test]
        public void TrainingRaisesAStatOfThePlayersChoiceForSilverAndTime()
        {
            var s = Rich(20);
            var d = s.AddDweller("Ясна", new[] { 10, 10, 10, 10, 10 });
            Assert.AreEqual(Fail.Locked, s.StartTraining(d.Id, Stat.Agility, T0), "no training hall");
            var hall = Put(s, RoomType.TrainingHall, 10, H + 1, 2);
            Assert.AreEqual(10 + 3 * 10, s.TrainingCap(d));
            long seconds = s.TrainingSeconds(d, Stat.Agility);
            Assert.AreEqual((long)Math.Round((1 + 0.15 * 10) * Math.Pow(0.98, 9) * 3600), seconds);
            var cost = s.TrainingCost(d, Stat.Agility);
            long silver = s.State.Wallet[ResourceType.Silver];
            Assert.AreEqual(Fail.None, s.StartTraining(d.Id, Stat.Agility, T0));
            Assert.AreEqual(silver - cost[ResourceType.Silver], s.State.Wallet[ResourceType.Silver]);
            Assert.AreEqual(hall.Id, d.RoomId, "the trainee takes a place in the hall");
            Assert.AreEqual(Fail.AlreadyQueued, s.StartTraining(d.Id, Stat.Strength, T0));
            s.Advance(T0 + seconds - 1);
            Assert.AreEqual(10, d.StatOf(Stat.Agility));
            s.Advance(T0 + seconds);
            Assert.AreEqual(11, d.StatOf(Stat.Agility));
            Assert.AreEqual(1, s.PointsTrained);
            Assert.Greater(s.TrainingSeconds(d, Stat.Agility), seconds, "the next point takes longer");

            // two places per cell; leaving the hall loses the point in progress
            var e = s.AddDweller("Злата", new[] { 10, 10, 10, 10, 10 });
            var f = s.AddDweller("Лада", new[] { 10, 10, 10, 10, 10 });
            Assert.AreEqual(Fail.None, s.StartTraining(e.Id, Stat.Spirit, s.State.Time));
            Assert.AreEqual(Fail.RoomFull, s.StartTraining(f.Id, Stat.Spirit, s.State.Time));
            s.Unassign(e.Id);
            s.Advance(s.State.Time + 10 * 3600);
            Assert.AreEqual(10, e.StatOf(Stat.Spirit), "left the hall: no point");
            Assert.IsNull(s.TrainingOf(e.Id));

            // the cap, with the bonus of rarity
            d.Stats[(int)Stat.Strength] = 40;
            Assert.AreEqual(Fail.LimitReached, s.StartTraining(d.Id, Stat.Strength, s.State.Time));
            s.State.Halls.Rarity[d.Id] = Rarity.Legendary;
            Assert.AreEqual(40 + s.RarityInfo(Rarity.Legendary).CapBonus, s.TrainingCap(d));
            Assert.AreEqual(Fail.None, s.StartTraining(d.Id, Stat.Strength, s.State.Time));
        }

        [Test]
        public void TrainedStatsWorkInTheRooms()
        {
            var s = Rich(20);
            var hall = Put(s, RoomType.TrainingHall, 20, H + 1, 2);
            var mine = s.State.Rooms.First(r => r.Type == RoomType.CoalMine);
            var miner = s.FindDweller(mine.Workers[0]);
            double before = s.RoomOutputPerHour(mine);
            long now = T0;
            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual(Fail.None, s.StartTraining(miner.Id, Stat.Endurance, now));
                now = s.TrainingOf(miner.Id).End;
                s.Advance(now);
            }
            Assert.AreEqual(hall.Id, miner.RoomId);
            s.Assign(miner.Id, mine.Id);
            double trained = s.RoomOutputPerHour(mine);
            miner.Stats[(int)Stat.Endurance] -= 5;
            double untrained = s.RoomOutputPerHour(mine);
            miner.Stats[(int)Stat.Endurance] += 5;
            Assert.Greater(trained, untrained, "five points of Endurance dig more coal");
            Assert.Greater(before, 0);
        }

        // ------------------------------------------------------------------ watchtower

        [Test]
        public void TheWatchtowerScoutsMoreWithItsLevel()
        {
            var s = Rich(12);
            Assert.AreEqual(0, s.Scout(T0).TowerLevel);
            var tower = Put(s, RoomType.Watchtower, 1, H + 1, 2);
            var r1 = s.Scout(T0);
            Assert.IsTrue(r1.Surge, "tonight's Surge is still ahead");
            Assert.AreEqual(s.Combat.Surge.Waves.Count, r1.Waves);
            Assert.Greater(r1.Creatures, 0);
            Assert.AreEqual(0, (r1.SurgeAt + s.State.UtcOffsetSeconds) % 86400 - s.Balance.Activities.Surge.StartHour * 3600);
            Assert.AreEqual(0, r1.Dens.Count, "dens from level 3");
            Assert.AreEqual(-1, r1.Forecast);

            tower.Level = 10;
            var r10 = s.Scout(T0);
            CollectionAssert.AreEqual(s.HuntDens(T0 + 86400), r10.Dens, "tomorrow's dens");
            CollectionAssert.AreEqual(s.BuildSurgeSetup().SpawnFloors, r10.SpawnFloors);
            Assert.AreEqual(2, r10.RouteForks.Count);
            Assert.AreEqual(s.ForkCheck(s.State.NextBattleSeed, 0, 12), r10.RouteForks[0], "the next route's test");
            Assert.That(r10.Forecast, Is.InRange(0, 3), "the forecast of the automatic defense");

            double defense = s.DefenseMult();
            Worker(s, tower, Stat.Agility, 40);
            Assert.AreEqual(defense + 40 / 400.0, s.DefenseMult(), 1e-9, "sentries help the defenders");
            Assert.AreEqual(s.DefenseMult() - 1, s.BuildSurgeSetup().DefenseBonus, 1e-9);
        }

        // ------------------------------------------------------------------ chapel

        [Test]
        public void TheChapelAscendsHeroesForEssence()
        {
            var s = Rich(20);
            var hero = s.State.Dwellers.First(d => d.IsHero);
            hero.HeroLevel = 19;
            Assert.AreEqual(Fail.Locked, s.Ascend(hero.Id, T0), "no chapel");
            var chapel = Put(s, RoomType.Chapel, 1, H + 1, 2);
            Assert.AreEqual(Fail.HeroCap, s.Ascend(hero.Id, T0), "rank 1 needs hero level 20");
            hero.HeroLevel = 30;
            var first = s.NextAscension(hero);
            long essence = s.State.Wallet[ResourceType.Essence];
            long price = s.AscensionEssence(first);
            Assert.Greater(price, 0);
            var before = s.MissionSetup(0, 1).Heroes.Single(h => h.Id == hero.Id);
            Assert.AreEqual(Fail.None, s.Ascend(hero.Id, T0));
            Assert.AreEqual(essence - price, s.State.Wallet[ResourceType.Essence]);
            Assert.AreEqual(1, s.AscensionRank(hero.Id));
            Assert.AreEqual(Fail.Locked, s.Ascend(hero.Id, T0), "rank 2 needs chapel 6");
            chapel.Level = 6;
            Assert.AreEqual(Fail.None, s.Ascend(hero.Id, T0));
            var after = s.MissionSetup(0, 1).Heroes.Single(h => h.Id == hero.Id);
            Assert.AreEqual(before.AttackMult * (1 + 2 * s.Balance.Halls.Chapel.Bonus), after.AttackMult, 1e-9);
            Assert.AreEqual(before.HpMult * (1 + 2 * s.Balance.Halls.Chapel.Bonus), after.HpMult, 1e-9);
            Assert.AreEqual(Fail.HeroCap, s.Ascend(hero.Id, T0), "rank 3 needs hero level 40");
            Assert.AreEqual(Formulas.HeroLevelCap(s.Balance, 20), Formulas.HeroLevelCap(s.Balance, s.State.HearthLevel), "the level cap stays");
            var dweller = s.State.Dwellers.First(d => !d.IsHero);
            Assert.AreEqual(Fail.NotAHero, s.Ascend(dweller.Id, T0));
            s.State.Wallet[ResourceType.Essence] = 0;
            hero.HeroLevel = 40;
            chapel.Level = 12;
            Assert.AreEqual(Fail.NotEnough, s.Ascend(hero.Id, T0));
        }

        [Test]
        public void TheChapelGivesAGiftOnceADayInTurn()
        {
            var s = Rich(15);
            Assert.AreEqual(Fail.Locked, s.ClaimGift(T0, out _, out _));
            var chapel = Put(s, RoomType.Chapel, 15, H + 1, 2);
            var kinds = new HashSet<GiftGoods>();
            long day = s.LocalDay(T0);
            for (int i = 0; i < 3; i++)
            {
                long now = (day + i) * 86400 + 10 * 3600;
                s.Advance(now); // the evening Surge of the day before pays its own hourglass
                long essence = s.State.Wallet[ResourceType.Essence], treats = s.Treats, hourglass = s.State.HourglassSeconds;
                Assert.IsTrue(s.GiftReady(now));
                Assert.AreEqual(Fail.None, s.ClaimGift(now, out var gift, out long amount));
                Assert.Greater(amount, 0);
                kinds.Add(gift.Goods);
                switch (gift.Goods)
                {
                    case GiftGoods.Essence: Assert.AreEqual(essence + amount, s.State.Wallet[ResourceType.Essence]); break;
                    case GiftGoods.Treats: Assert.AreEqual(treats + amount, s.Treats); break;
                    default: Assert.AreEqual(hourglass + amount, s.State.HourglassSeconds); break;
                }
                Assert.AreEqual(Fail.NotToday, s.ClaimGift(now + 3600, out _, out _), "once a day");
            }
            Assert.AreEqual(3, kinds.Count, "essence, treats and hourglass in turn");
            var g = s.Balance.Halls.Chapel.Gifts.First(x => x.Goods == GiftGoods.Treats);
            long plain = s.ChapelGiftAmount(g);
            Assert.AreEqual((long)Math.Round(g.Amount * (1 + 15 / 30.0)), plain);
            Worker(s, chapel, Stat.Spirit, 50);
            Assert.AreEqual((long)Math.Round(g.Amount * (1 + 15 / 30.0) * 1.5), s.ChapelGiftAmount(g), "Spirit of the priests");
        }

        // ------------------------------------------------------------------ archive

        [Test]
        public void ArchiveRecordsAreFoundByPlayAndReadOverHours()
        {
            var s = Rich(15);
            var first = s.Records[0];
            Assert.AreEqual(Fail.Locked, s.StartReading(first.Id, T0), "no archive");
            var archive = Put(s, RoomType.Archive, 1, H + 1, 2);
            Assert.IsTrue(s.RecordFound(first), "the oath: hearth 3");
            var convoy = s.Records.First(r => r.Kind == RecordKind.Missions);
            Assert.IsFalse(s.RecordFound(convoy));
            Assert.AreEqual(Fail.Locked, s.StartReading(convoy.Id, T0), "not found yet");
            for (int i = 0; i < convoy.Need; i++)
                s.State.CampaignStars.Add(3);
            Assert.IsTrue(s.RecordFound(convoy));

            long seconds = s.ReadSeconds(first);
            Assert.AreEqual((long)Math.Round(3600 / (1 + 0.05)), seconds, "1 hour, archive 1: +5%");
            long essence = s.State.Wallet[ResourceType.Essence];
            Assert.AreEqual(Fail.None, s.StartReading(first.Id, T0));
            Assert.AreEqual(Fail.QueueFull, s.StartReading(convoy.Id, T0), "one record at a time");
            s.Advance(T0 + seconds - 1);
            Assert.IsFalse(s.RecordRead(first.Id));
            s.Advance(T0 + seconds);
            Assert.IsTrue(s.RecordRead(first.Id));
            Assert.AreEqual(essence + s.RecordEssence(), s.State.Wallet[ResourceType.Essence]);
            Assert.AreEqual(Fail.AlreadyQueued, s.StartReading(first.Id, s.State.Time));
            Assert.Greater(s.ReadSeconds(convoy), s.ReadSeconds(first), "later records take longer");
            Worker(s, archive, Stat.Intellect, 100);
            Assert.AreEqual((long)Math.Round(3600 / 2.05), s.ReadSeconds(first), "+ Intellect / 100");
        }

        // ------------------------------------------------------------------ save and load

        [Test]
        public void EverythingOfTheHallsSurvivesSaveAndLoad()
        {
            var s = Rich(20);
            Put(s, RoomType.Tavern, 5, H + 1, 0);
            Put(s, RoomType.Library, 20, H + 1, 1);
            var hall = Put(s, RoomType.TrainingHall, 20, H + 1, 2);
            Put(s, RoomType.Chapel, 20, H + 1, 3);
            Put(s, RoomType.Archive, 20, H + 1, 4);
            var board = s.TavernBoard(T0);
            Assert.AreEqual(Fail.None, s.Hire(0, T0, out var hired));
            s.State.Halls.Rarity[hired.Id] = Rarity.Rare;
            Assert.AreEqual(Fail.None, s.StartResearch("Crafts1", T0));
            var trainee = s.AddDweller("Савва", new[] { 10, 10, 10, 10, 10 });
            Assert.AreEqual(Fail.None, s.StartTraining(trainee.Id, Stat.Intellect, T0));
            var hero = s.State.Dwellers.First(d => d.IsHero);
            hero.HeroLevel = 25;
            Assert.AreEqual(Fail.None, s.Ascend(hero.Id, T0));
            Assert.AreEqual(Fail.None, s.ClaimGift(T0, out _, out _));
            Assert.AreEqual(Fail.None, s.StartReading(s.Records[0].Id, T0));
            s.State.Halls.SickUntil[trainee.Id] = T0 + 3600;

            var loaded = new GameSession(s.Balance, SaveSystem.FromJson(SaveSystem.ToJson(s.State))) { Combat = s.Combat };
            var h = loaded.State.Halls;
            Assert.AreEqual(board.Count, h.Tavern.Board.Count);
            Assert.IsTrue(h.Tavern.Board[0].Hired);
            Assert.AreEqual(board[1].Name, h.Tavern.Board[1].Name);
            Assert.AreEqual(Rarity.Rare, loaded.RarityOf(hired.Id));
            Assert.AreEqual("Crafts1", loaded.CurrentResearch.Id);
            Assert.AreEqual(s.ResearchEnd, loaded.ResearchEnd);
            Assert.AreEqual(Stat.Intellect, loaded.TrainingOf(trainee.Id).Stat);
            Assert.AreEqual(1, loaded.AscensionRank(hero.Id));
            Assert.IsFalse(loaded.GiftReady(T0));
            Assert.AreEqual(s.Records[0].Id, loaded.CurrentReading.Id);
            Assert.IsTrue(loaded.IsSick(trainee.Id, T0));

            // and time goes on after loading
            long end = Math.Max(loaded.ResearchEnd, Math.Max(loaded.TrainingOf(trainee.Id).End, loaded.ReadingEnd));
            loaded.Advance(end);
            Assert.IsTrue(loaded.Researched("Crafts1"));
            Assert.AreEqual(11, loaded.FindDweller(trainee.Id).StatOf(Stat.Intellect));
            Assert.IsTrue(loaded.RecordRead(s.Records[0].Id));
            Assert.IsFalse(loaded.IsSick(trainee.Id, loaded.State.Time));
            Assert.AreEqual(hall.Id, loaded.FindDweller(trainee.Id).RoomId);
        }

        [Test]
        public void AnOldSaveWithoutTheHallsLoads()
        {
            var s = TestData.NewSession();
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(s.State);
            var raw = Newtonsoft.Json.Linq.JObject.Parse(json);
            raw.Remove("Halls");
            var state = raw.ToObject<GameState>();
            state.Halls = null;
            var loaded = new GameSession(s.Balance, state);
            loaded.Advance(T0 + 3600);
            Assert.AreEqual(0, loaded.ResearchCount);
            Assert.IsNotNull(loaded.State.Halls);
        }

        // ------------------------------------------------------------------ the bot

        [Test]
        public void ABotOfTwentyDaysUsesTheHalls()
        {
            var s = TestData.NewSession();
            s.Combat = TestData.Combat();
            var bot = new PlayerBot(s);
            bot.PlayDays(TestData.Start, 20);
            TestContext.WriteLine(bot.Summary(20));
            Assert.Greater(bot.Hires, 10, "people come through the tavern");
            Assert.Greater(s.ResearchCount, 15, "the library researches");
            Assert.Greater(s.PointsTrained, 30, "the training hall trains");
            Assert.Greater(bot.Gifts, 5, "the chapel's gifts");
            Assert.Greater(s.State.Halls.Read.Count, 5, "the archive's records");
        }
    }
}
