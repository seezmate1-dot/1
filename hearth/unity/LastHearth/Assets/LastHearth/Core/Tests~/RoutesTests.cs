using System;
using System.Collections.Generic;
using System.Linq;
using LastHearth.Core.Combat;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace LastHearth.Core.Tests
{
    /// <summary>ACTIVITIES.md 4, MODES.md 6: the points of a route, the fight by hand, notes, map pieces, the halls' specializations.</summary>
    public class RoutesTests
    {
        const long T0 = TestData.Start;
        const long Hour = 3600;
        const long Day = 86400;

        static GameSession Fortress(int hearth, long now = T0)
        {
            var s = NewGame.Create(TestData.Balance(), now);
            s.Combat = TestData.Combat();
            s.State.HearthLevel = hearth;
            s.State.Queue.EnsureBuilders(s.BuilderCount());
            s.State.Wallet[ResourceType.Coal] = 1000000;
            s.State.Wallet[ResourceType.Food] = 1000000;
            return s;
        }

        static List<int> Walkers(GameSession s, int count, int stat)
        {
            var ids = new List<int>();
            for (int i = 0; i < count; i++)
                ids.Add(s.AddDweller("Ходок " + i, new[] { stat, stat, stat, stat, stat }).Id);
            return ids;
        }

        // ------------------------------------------------------------------ the map

        [Test]
        public void EveryPathHasSixToEightPointsOfTheDocumentsKinds()
        {
            var s = Fortress(12);
            var kinds = new HashSet<RoutePoint>();
            for (int seed = 1; seed <= 30; seed++)
            {
                var map = s.RouteMapOf(seed, 12);
                foreach (RoutePath a in Enum.GetValues(typeof(RoutePath)))
                {
                    foreach (RoutePath b in Enum.GetValues(typeof(RoutePath)))
                    {
                        var walk = map.Walk(a, b);
                        Assert.That(walk.Count, Is.InRange(6, 8));
                        Assert.AreEqual(walk.Count, walk.Distinct().Count());
                        foreach (int id in walk)
                            kinds.Add(map.Node(id).Type);
                        // every step of the walk is a line of the map
                        Assert.IsTrue(map.Edges.Contains((GameSession.RouteGateId, walk[0])));
                        for (int i = 1; i < walk.Count; i++)
                            Assert.IsTrue(map.Edges.Contains((walk[i - 1], walk[i])), "seed " + seed + ": " + walk[i - 1] + "→" + walk[i]);
                    }
                }
                // the risky branch tests what the watchtower scouts
                for (int f = 0; f < 2; f++)
                {
                    var key = map.Nodes.Single(n => n.Fork == f && n.Key);
                    Assert.AreEqual(s.ForkCheck(seed, f, 12), (key.Stat, key.Need));
                }
            }
            CollectionAssert.IsSupersetOf(kinds, new[] { RoutePoint.Fight, RoutePoint.Resource, RoutePoint.Event, RoutePoint.Rest, RoutePoint.Danger });
            Assert.AreEqual(s.RouteMapOf(5, 12).Nodes.Select(n => n.Name), s.RouteMapOf(5, 12).Nodes.Select(n => n.Name), "the same map for the same seed");
        }

        [Test]
        public void TheTeamPassesThePointsOneByOneAndTheLongWayTakesLonger()
        {
            var s = Fortress(12);
            var team = Walkers(s, 3, 40);
            Assert.AreEqual(Fail.None, s.StartRoute(team, RoutePath.Safe, RoutePath.Safe, T0, out var quick));
            var team2 = Walkers(s, 3, 40);
            Assert.AreEqual(Fail.None, s.StartRoute(team2, RoutePath.Long, RoutePath.Long, T0, out var slow));
            Assert.AreEqual(4 * Hour, quick.End - quick.Start, "1–8 hours: the safe way is the shortest");
            Assert.AreEqual(8 * Hour, slow.End - slow.Start);
            var times = s.RouteTimes(slow);
            Assert.AreEqual(s.RouteWalk(slow).Count, times.Count);
            s.Advance(times[2]);
            Assert.AreEqual(3, slow.Passed);
            Assert.AreEqual(3, slow.Log.Count);
            Assert.IsFalse(slow.Back);
            s.Advance(slow.End);
            Assert.IsTrue(slow.Back);
            Assert.AreEqual(s.RouteWalk(slow).Count, slow.Passed);
            Assert.Greater(slow.MatHours, quick.MatHours, "the long way carries more materials");
            Assert.That(quick.MoreCards.Count + 1, Is.InRange(1, 2), "1–2 cards at home");
        }

        [Test]
        public void AForkCanBeChosenAgainUntilTheTeamReachesIt()
        {
            var s = Fortress(12);
            var team = Walkers(s, 2, 30);
            Assert.AreEqual(Fail.None, s.StartRoute(team, RoutePath.Safe, RoutePath.Safe, T0, out var e));
            long end = e.End;
            Assert.AreEqual(Fail.None, s.ChooseFork(e.Id, 1, RoutePath.Long, T0 + 60));
            Assert.AreEqual(end + 2 * Hour, e.End, "the long way adds two hours");
            Assert.AreEqual(RoutePath.Long, e.Forks[1]);
            // past the first fork it cannot change any more
            var times = s.RouteTimes(e);
            s.Advance(times[1]);
            Assert.AreEqual(Fail.NotRunning, s.ChooseFork(e.Id, 0, RoutePath.Risky, times[1]));
            Assert.AreEqual(Fail.None, s.ChooseFork(e.Id, 1, RoutePath.Safe, times[1]));
            Assert.AreEqual(end, e.End);
        }

        // ------------------------------------------------------------------ the fight by hand

        static (GameSession s, Expedition e) RouteWithFight(int seedFrom = 1)
        {
            for (int k = seedFrom; ; k++)
            {
                var s = Fortress(14);
                s.State.NextBattleSeed = k;
                var team = Walkers(s, 3, 33);
                s.StartRoute(team, RoutePath.Risky, RoutePath.Risky, T0, out var e);
                if (s.NextSkirmish(e) != null)
                    return (s, e);
            }
        }

        [Test]
        public void ASkirmishLastsThirtySecondsAndItsResultDecidesThePoint()
        {
            int careful = 0, auto = 0, n = 0, seed = 1;
            for (int i = 0; i < 24; i++)
            {
                var (s, e) = RouteWithFight(seed);
                seed = e.Seed + 1;
                var node = s.NextSkirmish(e);
                Assert.AreEqual(Fail.None, s.StartRouteSkirmish(e.Id, T0 + 60, out var battle));
                Assert.AreEqual(300, battle.Numbers.MaxTicks, "30 seconds");
                Assert.AreEqual(900, s.Combat.MaxTicks, "the other battles keep their 90 seconds");
                Assert.IsNull(s.NextSkirmish(e), "one try per fight");
                battle.Brain = i % 2 == 0 ? Brains.Careful() : Brains.AutoBattle();
                battle.RunToEnd();
                TestContext.WriteLine((i % 2 == 0 ? "careful " : "auto ") + battle.Won + " in " + battle.Tick * 0.1 + " s");
                Assert.LessOrEqual(battle.Tick, 300);
                if (battle.Won)
                {
                    if (i % 2 == 0)
                        careful++;
                    else
                        auto++;
                }
                n++;
                Assert.AreEqual(Fail.None, s.FinishRouteSkirmish(e.Id, battle, out string text));
                StringAssert.Contains(node.Name, text);
                Assert.AreEqual(battle.Won ? 1 : 0, e.Skirmish[node.Id]);
                s.Advance(e.End);
                StringAssert.Contains("бой вручную", string.Join(";", e.Log));
                if (node.Key && !battle.Won)
                    Assert.AreEqual(1, e.Hurt);
            }
            TestContext.WriteLine("careful wins " + careful + "/12, auto-battle " + auto + "/12");
            Assert.GreaterOrEqual(careful, 9, "a careful player wins most skirmishes in 30 seconds");
            Assert.GreaterOrEqual(careful, auto, "playing by hand is worth it");
        }

        [Test]
        public void AWonSkirmishBringsMoreBlueprintsThanTheStats()
        {
            var (s, e) = RouteWithFight();
            var node = s.NextSkirmish(e);
            var copy = new GameSession(s.Balance, JsonConvert.DeserializeObject<GameState>(JsonConvert.SerializeObject(s.State))) { Combat = s.Combat };
            var e2 = copy.State.Expeditions.Single();
            foreach (int id in e2.Team)
                copy.FindDweller(id).Stats = new[] { 1, 1, 1, 1, 1 }; // a weak team loses the check
            e2.Skirmish[node.Id] = 1; // ... but won the fight by hand
            copy.Advance(e2.End);
            foreach (int id in e.Team)
                s.FindDweller(id).Stats = new[] { 1, 1, 1, 1, 1 };
            s.Advance(e.End);
            Assert.Greater(e2.Share, e.Share);
        }

        // ------------------------------------------------------------------ notes and map pieces

        [Test]
        public void NotesAreFoundInTheOrderOfTheStoryAndMapPiecesAreKept()
        {
            var s = Fortress(14);
            var team = Walkers(s, 3, 40);
            long now = T0;
            for (int i = 0; i < 40; i++)
            {
                Assert.AreEqual(Fail.None, s.StartRoute(team, RoutePath.Risky, RoutePath.Long, now, out var e));
                s.Advance(e.End);
                while (s.State.Expeditions.Contains(e))
                    s.ResolveRouteCard(e.Id, 2, e.End);
                foreach (int id in team)
                    s.FindDweller(id).InjuredUntil = 0;
                now += Day;
            }
            Assert.Greater(s.State.RouteNotes.Count, 5);
            CollectionAssert.AreEqual(Enumerable.Range(0, s.State.RouteNotes.Count), s.State.RouteNotes.OrderBy(x => x), "no gaps, no repeats");
            Assert.Greater(s.State.MapFragments, 10, "the caches and far stores bring pieces of maps");
        }

        static long TreasureDay()
        {
            var probe = NewGame.Create(TestData.Balance(), TestData.Start);
            var e = probe.EventInfo("Treasure");
            long day0 = TestData.Start / Day;
            for (long d = day0; d < day0 + 200; d++)
            {
                if (probe.OnCalendar(e, d) && !probe.OnCalendar(e, d - 1))
                    return d * Day + 8 * Hour;
            }
            throw new InvalidOperationException("no Treasure event");
        }

        [Test]
        public void MapPiecesNarrowTheTreasureSearch()
        {
            var s = Fortress(16);
            s.State.MapFragments = 5;
            s.State.EventsSeen.AddRange(s.Balance.Activities.Events.Select(x => x.Id)); // no personal runs
            s.Advance(T0);
            s.State.Events.RemoveAll(x => x.Id == "Treasure");
            Assert.AreEqual(Fail.NotRunning, s.UseMapFragment(T0), "only while the Treasure hunters are on");
            long t = TreasureDay();
            s.Advance(t + 1);
            var run = s.ActiveRun("Treasure", t + 1);
            Assert.IsNotNull(run);
            int before = s.TreasureCandidates(run).Count;
            for (int i = 0; i < 3; i++)
                Assert.AreEqual(Fail.None, s.UseMapFragment(t + 1));
            Assert.AreEqual(Fail.LimitReached, s.UseMapFragment(t + 1), "three pieces a map");
            Assert.AreEqual(2, s.State.MapFragments);
            Assert.AreEqual(3, run.Pieces);
            Assert.Less(s.TreasureCandidates(run).Count, before / 2);
        }

        // ------------------------------------------------------------------ specializations of the halls

        [Test]
        public void EveryHallHasItsOwnFiveTiersOfTwo()
        {
            Assert.AreEqual(15, GameSession.SpecRooms.Length, "15 halls (ACTIVITIES.md 4)");
            Assert.AreEqual(GameSession.SpecRooms.Length, GameSession.RoomSpecs.Count);
            foreach (var type in GameSession.SpecRooms)
            {
                var tiers = GameSession.RoomSpecs[type];
                Assert.AreEqual(GameSession.SpecTierCount, tiers.Length, type.ToString());
                var names = tiers.SelectMany(t => t).Select(o => o.Name).ToList();
                Assert.AreEqual(10, names.Distinct().Count(), type + ": names");
                foreach (var t in tiers)
                {
                    Assert.AreEqual(2, t.Length);
                    Assert.IsTrue(t.All(o => o.Effect != SpecEffect.None && o.Value > 0 && o.Text != ""));
                    Assert.AreNotEqual(t[0].Effect, t[1].Effect, type + ": a real choice");
                }
            }
            // the document's examples
            Assert.AreEqual("Глубокие жилы", GameSession.SpecOptionOf(RoomType.CoalMine, 0, 1).Name);
            Assert.AreEqual("Крепкие своды", GameSession.SpecOptionOf(RoomType.CoalMine, 0, 2).Name);
            Assert.AreEqual("Запасы", GameSession.SpecOptionOf(RoomType.Kitchen, 0, 2).Name);
            Assert.AreEqual("Строевая", GameSession.SpecOptionOf(RoomType.Barracks, 0, 1).Name);
            Assert.AreEqual("Связи", GameSession.SpecOptionOf(RoomType.TradingPost, 0, 1).Name);
            Assert.AreEqual("Охрана", GameSession.SpecOptionOf(RoomType.Warehouse, 0, 1).Name);
            var s = Fortress(10);
            s.State.Wallet[ResourceType.Blueprints] = 100;
            Assert.AreEqual(Fail.NotFound, s.BuySpec(RoomType.Bedrooms, 0, 1), "the living quarters have none");
        }

        static GameSession WithHall(RoomType type, int level)
        {
            var s = Fortress(20);
            s.State.Wallet[ResourceType.Blueprints] = 10000;
            var room = s.State.Rooms.FirstOrDefault(r => r.Type == type);
            if (room == null)
            {
                room = new Room { Id = s.State.NextRoomId++, Type = type, Level = level, Floor = 0, Cell = 0, Width = 1 };
                s.State.Rooms.Add(room);
            }
            room.Level = level;
            return s;
        }

        [Test]
        public void TheHallsSpecializationsChangeTheirNumbers()
        {
            // the barracks: the squad's attack, the Surge
            var s = WithHall(RoomType.Barracks, 25);
            double attack = s.HallsHeroMult(0).attack, surge = s.DefenseMult();
            Assert.AreEqual(Fail.None, s.BuySpec(RoomType.Barracks, 0, 1));
            Assert.AreEqual(attack + 0.05, s.HallsHeroMult(0).attack, 1e-9);
            Assert.AreEqual(Fail.None, s.BuySpec(RoomType.Barracks, 2, 2));
            Assert.AreEqual(surge + 0.20, s.DefenseMult(), 1e-9);

            // the warehouse: raids and the hourglass of the daily tasks
            s = WithHall(RoomType.Warehouse, 25);
            double hold = s.RaidHold(0);
            Assert.AreEqual(Fail.None, s.BuySpec(RoomType.Warehouse, 0, 1));
            Assert.AreEqual(Math.Min(0.85, hold * 1.2), s.RaidHold(0), 1e-9);
            Assert.AreEqual(Fail.None, s.BuySpec(RoomType.Warehouse, 1, 2));
            Assert.AreEqual(10, s.SpecSum(SpecEffect.TaskHourglass), 1e-9);

            // the trading post: one more good in Brick's shop
            s = WithHall(RoomType.TradingPost, 5);
            Assert.AreEqual(Fail.None, s.BuySpec(RoomType.TradingPost, 0, 1));
            Assert.AreEqual(1, s.SpecSum(SpecEffect.ShopSlot), 1e-9);

            // the halls' own work
            s = WithHall(RoomType.Library, 5);
            var node = s.ResearchNodes[0];
            long research = s.ResearchSeconds(node);
            Assert.AreEqual(Fail.None, s.BuySpec(RoomType.Library, 0, 1));
            Assert.Less(s.ResearchSeconds(node), research);
            s = WithHall(RoomType.Forge, 5);
            long craft = s.CraftSeconds(ItemRarity.Rare);
            Assert.AreEqual(Fail.None, s.BuySpec(RoomType.Forge, 0, 1));
            Assert.AreEqual((long)Math.Round(craft / 1.1), s.CraftSeconds(ItemRarity.Rare));
            s = WithHall(RoomType.Infirmary, 5);
            double heal = s.HealSpeed();
            Assert.AreEqual(Fail.None, s.BuySpec(RoomType.Infirmary, 0, 1));
            Assert.AreEqual(heal + 0.10, s.HealSpeed(), 1e-9);
            s = WithHall(RoomType.Kitchen, 5);
            double eat = s.FoodEatMult();
            Assert.AreEqual(Fail.None, s.BuySpec(RoomType.Kitchen, 0, 1));
            Assert.AreEqual(eat - 0.10, s.FoodEatMult(), 1e-9);

            // the watchtower: shorter routes
            s = WithHall(RoomType.Watchtower, 5);
            Assert.AreEqual(Fail.None, s.BuySpec(RoomType.Watchtower, 0, 1));
            Assert.AreEqual(Fail.None, s.StartRoute(Walkers(s, 1, 30), RoutePath.Safe, RoutePath.Safe, T0, out var e));
            Assert.AreEqual((long)Math.Round(4 * Hour * 0.9), e.End - e.Start);
        }

        [Test]
        public void AnOldSaveKeepsTheKindOfItsChoices()
        {
            var s = Fortress(12);
            var json = JObject.Parse(JsonConvert.SerializeObject(s.State));
            json["SpecTable"] = 0;
            // the generic table: tier 2 option 1 was «Тёплые стены» (no freeze), tier 1 option 2 «Запасливость» (stock)
            json["SpecChoice"] = JObject.FromObject(new Dictionary<string, int[]>
            {
                { "Kitchen", new[] { 2, 1, 0, 0, 0 } },
                { "Mine", new[] { 1, 2, 0, 0, 0 } },
                { "Bedrooms", new[] { 1, 1, 0, 0, 0 } },
            });
            json["SpecRank"] = JObject.FromObject(new Dictionary<string, int[]>
            {
                { "Kitchen", new[] { 1, 1, 0, 0, 0 } },
                { "Mine", new[] { 1, 1, 0, 0, 0 } },
                { "Bedrooms", new[] { 2, 1, 0, 0, 0 } },
            });
            var state = JsonConvert.DeserializeObject<GameState>(json.ToString());
            long bp = state.Wallet[ResourceType.Blueprints];
            var old = new GameSession(s.Balance, state);
            Assert.AreEqual(2, old.SpecChosen(RoomType.Kitchen, 0), "stock stays stock: «Запасы»");
            Assert.AreEqual(2, old.SpecChosen(RoomType.Kitchen, 1), "no freeze moves to «Тёплый очаг кухни»");
            Assert.AreEqual(1, old.SpecChosen(RoomType.Mine, 0), "output stays output: «Богатая жила»");
            Assert.AreEqual(2, old.SpecChosen(RoomType.Mine, 1), "a worker place stays a worker place");
            Assert.IsFalse(state.SpecChoice.ContainsKey(RoomType.Bedrooms));
            long cost0 = old.SpecCost(0), cost1 = old.SpecCost(1);
            Assert.AreEqual(bp + cost0 * 3 + cost1, state.Wallet[ResourceType.Blueprints], "the living quarters' Blueprints and rank come back");
            Assert.AreEqual(2, state.SpecTable);
        }

        // ------------------------------------------------------------------ the developer's page

        [Test]
        public void TheAdminPageFinishesRoutesGivesNotesAndUndoesAChoice()
        {
            var s = Fortress(12);
            Assert.AreEqual(Fail.None, s.StartRoute(Walkers(s, 2, 30), RoutePath.Long, RoutePath.Risky, T0, out var e));
            Assert.AreEqual(1, s.AdminFinishRoutes(T0 + 60));
            Assert.IsTrue(e.Back);
            Assert.AreEqual(s.RouteWalk(e).Count, e.Passed, "every point passed");
            int found = s.State.RouteNotes.Count;
            Assert.AreEqual(found + 3, s.AdminGiveNotes(3));
            Assert.AreEqual(GameSession.RouteNotes.Length, s.AdminGiveNotes(100));
            Assert.AreEqual(4, s.AdminGiveFragments(4) - e.Fragments);
            var mine = s.State.Rooms.First(r => r.Type == RoomType.CoalMine);
            mine.Level = 10;
            s.State.Wallet[ResourceType.Blueprints] = 100;
            Assert.AreEqual(Fail.None, s.BuySpec(RoomType.CoalMine, 1, 2));
            Assert.AreEqual(s.SpecCost(1), s.AdminResetSpec(RoomType.CoalMine, 1));
            Assert.AreEqual(0, s.SpecChosen(RoomType.CoalMine, 1));
            Assert.AreEqual(100, s.State.Wallet[ResourceType.Blueprints]);
            Assert.AreEqual(Fail.None, s.BuySpec(RoomType.CoalMine, 1, 1), "chosen anew");
            Assert.Greater(s.AdminRefillRoutes(T0 + 120), 0);
        }
    }
}
