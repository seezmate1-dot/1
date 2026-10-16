using System;
using System.Collections.Generic;
using System.Linq;
using LastHearth.Core;
using LastHearth.Core.Combat;
using LastHearth.Core.Surge;
using NUnit.Framework;

namespace LastHearth.Core.Tests
{
    public class EventTests
    {
        const long Hour = 3600;
        const long Day = 86400;

        /// <summary>A strong fortress (heroes at 2L) at hearth 16, every event open, at 08:00 local.</summary>
        static GameSession Fortress(long now, int hearth = 16)
        {
            var s = NewGame.Create(TestData.Balance(), now);
            s.Combat = TestData.Combat();
            s.State.HearthLevel = hearth;
            s.State.Queue.EnsureBuilders(s.BuilderCount());
            s.State.Wallet[ResourceType.Coal] = 1000000;
            var classes = new[] { HeroClass.Guardian, HeroClass.Swordsman, HeroClass.Cleaver, HeroClass.Archer, HeroClass.CoalPriest, HeroClass.AshMage, HeroClass.Swordsman };
            foreach (var c in classes)
            {
                int stat = 5 + 2 * hearth;
                var d = s.AddDweller(c.ToString(), new[] { stat, stat, stat, stat, stat });
                d.HeroClass = c;
                d.HeroLevel = 2 * hearth;
                d.SkillLevel = 4;
            }
            s.State.EventsSeen.AddRange(s.Balance.Activities.Events.Select(e => e.Id)); // no personal runs unless a test wants them
            return s;
        }

        /// <summary>08:00 of the first day when the event is on the shared calendar.</summary>
        static long DayOf(string id)
        {
            var probe = NewGame.Create(TestData.Balance(), TestData.Start);
            var e = probe.EventInfo(id);
            long day0 = TestData.Start / Day;
            for (long d = day0; d < day0 + 200; d++)
            {
                if (probe.OnCalendar(e, d) && !probe.OnCalendar(e, d - 1))
                    return d * Day + 8 * Hour;
            }
            throw new Exception("no " + id);
        }

        // ------------------------------------------------------------------ engine

        [Test]
        public void EventsFollowTheCalendarAndNewcomersGetAPersonalDay()
        {
            long t = DayOf("Portal");
            var s = Fortress(t);
            s.Advance(t + 1);
            var run = s.ActiveRun("Portal", t + 1);
            Assert.IsNotNull(run);
            Assert.IsFalse(run.Personal);
            Assert.AreEqual(4 * Day, run.End - run.Start);

            var fresh = Fortress(t);
            fresh.State.EventsSeen.Remove("Fair");
            fresh.Advance(t + 1);
            var personal = fresh.ActiveRun("Fair", t + 1);
            Assert.IsNotNull(personal, "a newcomer does not wait for the calendar");
            Assert.IsTrue(personal.Personal);
            Assert.AreEqual(Day, personal.End - personal.Start);
        }

        [Test]
        public void TenThresholdsPayEightyPercentAndTheRankTheRest()
        {
            long t = DayOf("Portal");
            var s = Fortress(t);
            s.Advance(t + 1);
            var run = s.ActiveRun("Portal", t + 1);
            var e = s.EventInfo("Portal");
            long sparks = s.State.Wallet[ResourceType.Sparks];
            s.AddPoints(run, GameSession.EventTarget["Portal"]);
            Assert.AreEqual(10, run.Thresholds);
            Assert.AreEqual(sparks + (long)Math.Round(e.Sparks * 0.08) * 10, s.State.Wallet[ResourceType.Sparks], 10);
            s.AddPoints(run, GameSession.EventTarget["Portal"]); // twice the target: first place
            Assert.AreEqual(1, s.EventRank(run));
            long before = run.SparksWon;
            s.Advance(run.End + 1);
            Assert.IsTrue(run.Closed);
            Assert.AreEqual(before, run.SparksWon, "the place is paid when the player takes it");
            long wallet = s.State.Wallet[ResourceType.Sparks];
            Assert.AreEqual(Fail.None, s.ClaimEventReward(run));
            Assert.AreEqual(wallet + (long)Math.Round(e.Sparks * 0.20), s.State.Wallet[ResourceType.Sparks], 5);
            Assert.AreEqual(e.Sparks, run.SparksWon, 5, "ten thresholds and the first place: the whole budget");
        }

        // ------------------------------------------------------------------ Long Night

        [Test]
        public void LongNightAttacksScoreDamage()
        {
            long t = DayOf("LongNight");
            var s = Fortress(t);
            s.Advance(t + 1);
            var run = s.ActiveRun("LongNight", t + 1);
            Assert.AreEqual(3, s.NightAttacksLeft(run, t + 1));
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(Fail.None, s.StartEventBattle("LongNight", t + 1, out var b));
                b.Brain = Brains.Careful();
                s.FinishEventBattle(b, t + 1, out _);
            }
            Assert.AreEqual(0, s.NightAttacksLeft(run, t + 1));
            Assert.AreEqual(Fail.NoTracks, s.StartEventBattle("LongNight", t + 1, out _));
            Assert.Greater(run.Points, 100, "the reaper does not die: points are damage");
            s.State.Wallet[ResourceType.Sparks] = 30;
            Assert.AreEqual(Fail.None, s.BuyEventExtra("LongNight", t + 1));
            Assert.AreEqual(1, s.NightAttacksLeft(run, t + 1));
        }

        // ------------------------------------------------------------------ Portal

        [Test]
        public void PortalCarriesHealthBetweenFloorsAndOffersBlessings()
        {
            long t = DayOf("Portal");
            var s = Fortress(t);
            s.Advance(t + 1);
            var run = s.ActiveRun("Portal", t + 1);
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(Fail.None, s.StartEventBattle("Portal", t + 1, out var b));
                b.Brain = Brains.Careful();
                s.FinishEventBattle(b, t + 1, out _);
            }
            Assert.AreEqual(3, run.Floor);
            Assert.AreEqual(s.Squad().Count, run.HpShares.Count, "health of every hero is kept between floors");
            Assert.AreEqual(3, run.BlessingOffer.Count);
            Assert.AreEqual(Fail.RulesNotMet, s.StartEventBattle("Portal", t + 1, out _), "choose a blessing first");
            Assert.AreEqual(Fail.None, s.ChooseBlessing(0, t + 1));
            Assert.AreEqual(1, run.Blessings.Count);
            run.HpShares = run.HpShares.Select(_ => 0.3).ToList();
            Assert.AreEqual(Fail.None, s.StartEventBattle("Portal", t + 1, out var next));
            Assert.IsTrue(next.Heroes.All(h => Math.Abs(h.Hp / h.MaxHp - 0.3) < 1e-9), "a hurt squad starts the next floor hurt");
        }

        [Test]
        public void PortalFloorsOpenTenADayAndAFallenSquadCanBeRevived()
        {
            long t = DayOf("Portal");
            var s = Fortress(t);
            s.Advance(t + 1);
            var run = s.ActiveRun("Portal", t + 1);
            Assert.AreEqual(10, s.PortalFloorsOpen(run, t + 1));
            Assert.AreEqual(20, s.PortalFloorsOpen(run, t + Day + 1));
            run.RunAlive = false;
            run.FreeRunDay = s.LocalDay(t + 1);
            Assert.AreEqual(Fail.NoTracks, s.StartEventBattle("Portal", t + 1, out _), "one free run a day");
            s.State.Wallet[ResourceType.Sparks] = 60;
            Assert.AreEqual(Fail.None, s.PortalRevive(t + 1));
            Assert.AreEqual(10, s.State.Wallet[ResourceType.Sparks]);
        }

        // ------------------------------------------------------------------ Tournament

        [Test]
        public void TournamentCountsTheBestBattleOfTheDay()
        {
            long t = DayOf("Tournament");
            var s = Fortress(t);
            s.Advance(t + 1);
            var run = s.ActiveRun("Tournament", t + 1);
            Assert.AreEqual(0, s.TournamentRule(run, t + 1));
            Assert.AreEqual(2, s.TournamentRule(run, t + 2 * Day + 1));
            for (int i = 0; i < 5; i++)
            {
                s.StartEventBattle("Tournament", t + 1, out var b);
                b.Brain = i % 2 == 0 ? Brains.Careful() : Brains.AutoBattle();
                s.FinishEventBattle(b, t + 1, out _);
            }
            Assert.AreEqual(run.DayBest, run.Points, 1e-6, "points are the best of the day, not the sum");
            Assert.AreEqual(0, s.TournamentAttemptsLeft(run, t + 1));
        }

        // ------------------------------------------------------------------ Treasure hunters

        [Test]
        public void TreasureMapOpensNeighboursForTorches()
        {
            long t = DayOf("Treasure");
            var s = Fortress(t);
            s.Advance(t + 1);
            var run = s.ActiveRun("Treasure", t + 1);
            Assert.AreEqual(61, GameSession.HexCells.Count);
            Assert.AreEqual(6, s.TorchesLeft(run, t + 1));
            int far = Enumerable.Range(0, 61).First(i => GameSession.HexDistance(i, GameSession.HexCenter) == 3);
            Assert.AreEqual(Fail.NotFound, s.OpenCell(far, t + 1, out _), "only next to an opened cell");
            int near = Enumerable.Range(0, 61).First(i => GameSession.HexDistance(i, GameSession.HexCenter) == 1 &&
                                                        s.CellContent(run, i) != GameSession.TreasureCell.Ambush);
            Assert.AreEqual(Fail.None, s.OpenCell(near, t + 1, out _));
            Assert.Contains(near, run.Opened);
            Assert.Less(s.TorchesLeft(run, t + 1), 7);
        }

        [Test]
        public void TheTreasureIsFarAndPiecesNarrowTheSearch()
        {
            long t = DayOf("Treasure");
            var s = Fortress(t);
            s.Advance(t + 1);
            var run = s.ActiveRun("Treasure", t + 1);
            int treasure = Enumerable.Range(0, 61).Single(i => s.CellContent(run, i) == GameSession.TreasureCell.Treasure);
            Assert.GreaterOrEqual(GameSession.HexDistance(treasure, GameSession.HexCenter), 3);
            int before = s.TreasureCandidates(run).Count;
            run.Pieces = 3;
            var after = s.TreasureCandidates(run);
            Assert.Less(after.Count, before / 2);
            Assert.Contains(treasure, after, "the pieces never cross out the real treasure");
        }

        // ------------------------------------------------------------------ Fair

        [Test]
        public void FairRatesChangeEveryFourHoursAndDealsHaveALimit()
        {
            long t = DayOf("Fair");
            var s = Fortress(t);
            s.Advance(t + 1);
            var run = s.ActiveRun("Fair", t + 1);
            var morning = s.FairRates(run, t + 1).ToArray();
            var noon = s.FairRates(run, t + 4 * Hour + 1).ToArray();
            CollectionAssert.AreNotEqual(morning, noon);
            s.State.Wallet[ResourceType.Coal] = 100000;
            for (int i = 0; i < 20; i++)
                Assert.AreEqual(Fail.None, s.FairTrade(0, 1, 100, t + 4 * Hour + 1, out long got));
            Assert.AreEqual(Fail.NoTracks, s.FairTrade(0, 1, 100, t + 4 * Hour + 1, out _));
            Assert.Greater(run.Points, 0);
            Assert.AreEqual(3, run.Orders.Count);
        }

        // ------------------------------------------------------------------ Siege

        [Test]
        public void SiegeWindowsResolveAndFallenOutpostsCostOutput()
        {
            long t = DayOf("Siege");
            var s = Fortress(t);
            s.Advance(t + 1);
            var run = s.ActiveRun("Siege", t + 1);
            Assert.AreEqual(5, run.Outposts.Count);
            Assert.AreEqual(-1, s.SiegeWindow(run, t + 1), "08:00 is between the windows");
            Assert.AreNotEqual(-1, s.SiegeWindow(run, t + 4 * Hour + 1), "12:00");
            s.Advance(t + 7 * Hour); // 15:00: the noon window closed with empty garrisons
            Assert.AreEqual(1, run.WindowsDone.Count);
            Assert.AreEqual(2, run.Outposts.Count(o => o.Fallen), "nobody defended: both attacked outposts fell");
            Assert.AreEqual(0.9, s.SiegePenalty(t + 7 * Hour), 1e-9);
        }

        [Test]
        public void ALiveSiegeDefenseHoldsWithHeroes()
        {
            long t = DayOf("Siege");
            var s = Fortress(t);
            s.Advance(t + 4 * Hour + 1);
            Assert.AreEqual(Fail.None, s.StartSiegeDefense(t + 4 * Hour + 1, out var d));
            d.Brain = SurgeDefense.Careful();
            Assert.AreEqual(Fail.None, s.FinishSiegeDefense(d, t + 4 * Hour + 1));
            var run = s.ActiveRun("Siege", t + 4 * Hour + 1);
            Assert.AreEqual(0, run.Outposts.Count(o => o.Fallen));
            Assert.AreEqual(30, run.Points, 1e-9, "two outposts held live: 2 x 10 x 1.5");
        }

        // ------------------------------------------------------------------ Festival

        [Test]
        public void LanternsScoreTimelyTaps()
        {
            var lanterns = GameSession.Lanterns(42);
            Assert.AreEqual(30, lanterns.Count);
            Assert.AreEqual(300, GameSession.LanternScore(42, lanterns));
            Assert.AreEqual(0, GameSession.LanternScore(42, lanterns.Select(l => (l.time - 100, l.spot))), "too early");
            Assert.AreEqual(0, GameSession.LanternScore(42, lanterns.Select(l => (l.time, (l.spot + 1) % 9))));
        }

        // ================================================================== helpers of the flows below

        static GameSession Reload(GameSession s)
        {
            var state = SaveSystem.FromJson(SaveSystem.ToJson(s.State));
            Assert.IsNotNull(state, "the save reads back");
            return new GameSession(s.Balance, state) { Combat = s.Combat };
        }

        static long Silver(GameSession s) => s.State.Wallet[ResourceType.Silver] + s.State.Rewards[ResourceType.Silver];

        /// <summary>The first local day of a 28-day cycle (a Monday) after the test start.</summary>
        static long CycleMonday()
        {
            long d = TestData.Start / Day;
            while ((d - 4) % 28 != 0)
                d++;
            return d;
        }

        static void Fight(GameSession s, string id, long now, int cell = -1)
        {
            Assert.AreEqual(Fail.None, s.StartEventBattle(id, now, out var b, cell), id);
            b.Brain = Brains.Careful();
            Assert.AreEqual(Fail.None, s.FinishEventBattle(b, now, out _));
        }

        // ================================================================== calendar and rotation

        [Test]
        public void TheCalendarFollowsTheTwentyEightDayTableAndRepeats()
        {
            var s = Fortress(TestData.Start);
            // EVENTS.md 2: cycle day -> the events of that day (day 1 is a Monday)
            var table = new Dictionary<string, int[]>
            {
                { "Portal", new[] { 1, 2, 3, 4, 15, 16, 17, 18 } },
                { "LongNight", new[] { 6, 7, 13, 14, 20, 21, 27, 28 } },
                { "Tournament", new[] { 8, 9, 10, 22, 23, 24 } },
                { "Treasure", new[] { 10, 11, 12, 13, 14 } },
                { "Fair", new[] { 18, 19, 20 } },
                { "Siege", new[] { 25, 26, 27, 28 } },
            };
            long monday = CycleMonday();
            Assert.AreEqual(0, (monday - 4) % 7, "day 1 of the cycle is a Monday");
            for (int cycle = 0; cycle < 3; cycle++)
            {
                for (int c = 1; c <= 28; c++)
                {
                    long d = monday + cycle * 28 + c - 1;
                    foreach (var kv in table)
                        Assert.AreEqual(kv.Value.Contains(c), s.OnCalendar(s.EventInfo(kv.Key), d), kv.Key + " on cycle day " + c);
                    int running = table.Keys.Count(id => s.OnCalendar(s.EventInfo(id), d));
                    Assert.LessOrEqual(running, 2, "no more than two events at once");
                }
            }
            // the Festival: ten days once a quarter
            var festival = s.EventInfo("Festival");
            int festivalDays = Enumerable.Range(0, 91).Count(i => s.OnCalendar(festival, monday + i));
            Assert.AreEqual(10, festivalDays);
        }

        [Test]
        public void RunsRotateWeekAfterWeekAndTheReaperChangesItsWeakness()
        {
            long monday = CycleMonday();
            long t = monday * Day + 8 * Hour;
            var s = Fortress(t);
            var families = new List<MonsterFamily>();
            var seen = new HashSet<string>();
            for (int day = 0; day < 56; day++)
            {
                long now = t + day * Day;
                s.Advance(now);
                var night = s.ActiveRun("LongNight", now);
                if (night != null && !families.Contains(s.NightFamily(night)) && s.LocalDay(night.Start) == s.LocalDay(now))
                    families.Add(s.NightFamily(night));
                foreach (var r in s.State.Events)
                    seen.Add(r.Id);
                Assert.AreEqual(s.State.Events.Count, s.State.Events.Select(r => r.Id + r.Start).Distinct().Count(), "a run opens once per calendar start");
            }
            Assert.AreEqual(4, families.Distinct().Count(), "four weeks, four weaknesses");
            CollectionAssert.IsSubsetOf(new[] { "Portal", "LongNight", "Tournament", "Treasure", "Fair", "Siege" }, seen);
        }

        [Test]
        public void EventsAreAnnouncedADayAhead()
        {
            long t = DayOf("LongNight");
            long start = t - 8 * Hour;
            var s = Fortress(start - 30 * Hour);
            Assert.IsFalse(s.Upcoming(start - 25 * Hour).Any(x => x.e.Id == "LongNight"), "25 hours ahead: not yet");
            var soon = s.Upcoming(start - 23 * Hour).FirstOrDefault(x => x.e.Id == "LongNight");
            Assert.IsNotNull(soon.e, "23 hours ahead: announced");
            Assert.AreEqual(start, soon.start);
            Assert.AreEqual(s.NightFamilyAt(start), s.NightFamilyAt(start + Day), "the weakness announced is the weakness of the run");
            Assert.AreEqual(start, s.NextStart(s.EventInfo("LongNight"), start - 23 * Hour));
            var low = Fortress(start - 30 * Hour, 10);
            Assert.IsFalse(low.Upcoming(start - 23 * Hour).Any(x => x.e.Id == "LongNight"), "a locked event is not announced");
        }

        [Test]
        public void ANewcomerGetsEachPersonalDayOnce()
        {
            long t = DayOf("Portal") + 2 * Day; // Portal runs, the Fair does not
            var s = Fortress(t);
            s.State.EventsSeen.Remove("Fair");
            s.Advance(t + 1);
            var personal = s.ActiveRun("Fair", t + 1);
            Assert.IsTrue(personal.Personal);
            Assert.AreEqual(GameSession.EventTarget["Fair"] / 3, s.RunTarget(personal), 1e-9, "one day of the three");
            s.Advance(personal.End + Hour);
            Assert.IsNull(s.ActiveRun("Fair", personal.End + Hour), "no second personal run");
            Assert.AreEqual(1, s.State.Events.Count(r => r.Id == "Fair"));
        }

        // ================================================================== rewards, claim, shop, currency

        /// <summary>Every event: start, ten milestones each paying its 8% (Sparks, essence, materials, hourglass,
        /// currency), the end, the place taken once, the shop and the currency burned.</summary>
        [TestCase("LongNight")]
        [TestCase("Portal")]
        [TestCase("Tournament")]
        [TestCase("Treasure")]
        [TestCase("Fair")]
        [TestCase("Siege")]
        [TestCase("Festival")]
        public void EveryEventPaysEachMilestoneThenItsPlace(string id)
        {
            long t = DayOf(id);
            var s = Fortress(t);
            s.Advance(t + 1);
            var run = s.ActiveRun(id, t + 1);
            Assert.IsNotNull(run, id + " starts on its calendar day");
            Assert.IsFalse(run.Personal);
            Assert.AreEqual(s.EventInfo(id).Days * Day, run.End - run.Start);
            Assert.AreEqual(0, s.LocalSecondOfDay(run.Start), "runs start at local midnight");
            var e = s.EventInfo(id);
            var part = s.BudgetPart(run, 0.08);
            for (int k = 1; k <= 10; k++)
            {
                long sparks = s.State.Wallet[ResourceType.Sparks], essence = s.State.Wallet[ResourceType.Essence];
                long hourglass = s.State.HourglassSeconds, purse = s.Purse(run);
                long materials = s.State.Wallet[ResourceType.Silver] + s.State.Wallet[ResourceType.Iron] +
                                 s.State.Rewards[ResourceType.Silver] + s.State.Rewards[ResourceType.Iron];
                s.AddPoints(run, s.RunTarget(run) / 10 + 1e-9);
                Assert.AreEqual(k, run.Thresholds, id + " milestone " + k);
                Assert.AreEqual(sparks + part.sparks, s.State.Wallet[ResourceType.Sparks], "Sparks of milestone " + k);
                Assert.AreEqual(essence + part.essence, s.State.Wallet[ResourceType.Essence], "essence of milestone " + k);
                Assert.AreEqual(hourglass + part.hourglass, s.State.HourglassSeconds, "hourglass of milestone " + k);
                Assert.AreEqual(materials + part.materials, s.State.Wallet[ResourceType.Silver] + s.State.Wallet[ResourceType.Iron] +
                                                            s.State.Rewards[ResourceType.Silver] + s.State.Rewards[ResourceType.Iron]);
                Assert.AreEqual(purse + 10, s.Purse(run), "currency of milestone " + k);
            }
            Assert.That(part.sparks * 10, Is.InRange(e.Sparks * 0.8 - 5, e.Sparks * 0.8 + 5), "ten milestones: 80% of the Sparks budget");
            s.Advance(run.End + 1);
            Assert.IsTrue(run.Closed);
            Assert.That(run.Rank, Is.InRange(1, s.GroupSize(id)));
            Assert.IsTrue(s.EndedRuns().Contains(run));
            long won = run.SparksWon;
            Assert.AreEqual(Fail.None, s.ClaimEventReward(run));
            Assert.AreEqual(won + s.BudgetPart(run, GameSession.RankShare(run.Rank)).sparks, run.SparksWon);
            Assert.AreEqual(Fail.AlreadyQueued, s.ClaimEventReward(run));
            Assert.LessOrEqual(run.SparksWon, e.Sparks + 5, "never more than the budget");
            // the shop: the first line is bought with the currency of the run
            var first = s.EventShop(id)[0];
            if (first.Goods != "Recruit" || !s.PopulationFull())
                Assert.AreEqual(Fail.None, s.BuyEventGoods(run, 0, run.End + Hour), id + " shop");
            s.Advance(run.End + Day + 1);
            Assert.IsTrue(run.Burned);
            Assert.AreEqual(0, s.Purse(run));
            Assert.IsFalse(s.EndedRuns().Contains(run));
        }

        [Test]
        public void ThresholdsPayOnceAndNothingCountsAfterTheEnd()
        {
            long t = DayOf("Tournament");
            var s = Fortress(t);
            s.Advance(t + 1);
            var run = s.ActiveRun("Tournament", t + 1);
            var e = s.EventInfo("Tournament");
            long sparks = s.State.Wallet[ResourceType.Sparks];
            var part = s.BudgetPart(run, 0.08);
            for (int k = 1; k <= 10; k++)
            {
                s.AddPoints(run, s.RunTarget(run) / 10);
                Assert.AreEqual(k, run.Thresholds);
                Assert.AreEqual(sparks + k * part.sparks, s.State.Wallet[ResourceType.Sparks], "threshold " + k + " pays its share");
                Assert.AreEqual(k * 10, run.Currency, "and 10 Знаков Стражей");
            }
            s.AddPoints(run, s.RunTarget(run) * 5);
            Assert.AreEqual(10, run.Thresholds, "ten thresholds at most");
            Assert.AreEqual(sparks + 10 * part.sparks, s.State.Wallet[ResourceType.Sparks], "no double payment");
            s.Advance(run.End);
            Assert.IsTrue(run.Closed);
            double points = run.Points;
            s.AddPoints(run, 500);
            Assert.AreEqual(points, run.Points, "a closed run takes no points");
            Assert.AreEqual(Fail.None, s.ClaimEventReward(run));
            Assert.AreEqual(Fail.AlreadyQueued, s.ClaimEventReward(run), "the place pays once");
            Assert.AreEqual(1, run.Rank);
            Assert.AreEqual(e.Sparks, run.SparksWon, 5);
            Assert.Contains("Знамя Хранителя очага", s.State.Trophies);
            Assert.AreEqual("Хранитель очага", GameSession.TournamentTitle(run.Rank));
        }

        [Test]
        public void TheShopStaysOpenADayAfterTheEndAndTheRestTurnsIntoSilver()
        {
            long t = DayOf("LongNight");
            var s = Fortress(t);
            s.Advance(t + 1);
            var run = s.ActiveRun("LongNight", t + 1);
            Assert.AreEqual("Осколки ночи", s.EventInfo("LongNight").Currency);
            var shop = s.EventShop("LongNight");
            Assert.AreEqual("Cores", shop[0].Goods);
            Assert.AreEqual(Fail.NotEnough, s.BuyEventGoods(run, 0, t + 1));
            s.AddPoints(run, s.RunTarget(run)); // 100 shards
            long cores = s.State.Wallet[ResourceType.Cores];
            Assert.AreEqual(Fail.None, s.BuyEventGoods(run, 0, t + 1));
            Assert.AreEqual(cores + shop[0].Amount, s.State.Wallet[ResourceType.Cores]);
            Assert.AreEqual(Fail.LimitReached, s.BuyEventGoods(run, 0, t + 1), "one per event");
            Assert.AreEqual(Fail.NotFound, s.BuyEventGoods(run, 9, t + 1));
            // the end: the place adds shards, the shop is still open for a day
            s.Advance(run.End + Hour);
            Assert.IsTrue(run.Closed);
            Assert.IsTrue(s.ShopOpen(run, run.End + Hour));
            Assert.AreEqual(Fail.None, s.ClaimEventReward(run));
            long shards = run.Currency;
            Assert.Greater(shards, 50, "the rest of the thresholds, the Dawn chest and the place");
            Assert.AreEqual(Fail.None, s.BuyEventGoods(run, 1, run.End + 2 * Hour), "the decoration");
            Assert.AreEqual(1, s.DecorOwned("shrine"));
            long left = run.Currency;
            long others = s.State.Events.Where(r => r != run && r.Closed && !r.Burned && r.End <= run.End).Sum(r => s.Purse(r)); // a Siege's stones
            long silver = Silver(s) + 5 * others;
            s.Advance(run.End + Day + 1);
            Assert.IsTrue(run.Burned);
            Assert.AreEqual(0, run.Currency);
            Assert.AreEqual(silver + 5 * left, Silver(s), "the shards left turn into silver 1:5");
            Assert.IsFalse(s.ShopOpen(run, run.End + Day + 1));
            Assert.AreEqual(Fail.Locked, s.BuyEventGoods(run, 1, run.End + Day + 1));
            s.Advance(run.End + 3 * Day);
            Assert.AreEqual(silver + 5 * left, Silver(s), "burned once");
        }

        [Test]
        public void OfflineAcrossTheEndTheRewardAndTheSilverComeOnce()
        {
            long t = DayOf("Portal");
            var s = Fortress(t);
            s.Advance(t + 1);
            var run = s.ActiveRun("Portal", t + 1);
            s.AddPoints(run, s.RunTarget(run) * 0.55); // five thresholds, then the player leaves
            long sparks = run.SparksWon;
            long silver = Silver(s);
            long ash = run.Currency;
            s.Advance(run.End + 3 * Day); // back three days after the end
            Assert.IsTrue(run.Closed && run.Claimed && run.Burned, "taken and burned by itself");
            var share = s.BudgetPart(run, GameSession.RankShare(run.Rank));
            Assert.AreEqual(sparks + share.sparks, run.SparksWon);
            Assert.AreEqual(0, run.Currency);
            long placeSilver = ResourceBag.Materials(share.materials, s.Balance.SilverShare)[ResourceType.Silver];
            Assert.AreEqual(silver + placeSilver + 5 * (ash + s.RankCurrency(run, run.Rank)), Silver(s), "the materials of the place and the ash 1:5");
            Assert.AreEqual(Fail.AlreadyQueued, s.ClaimEventReward(run));
            long wallet = s.State.Wallet[ResourceType.Sparks];
            s.Advance(run.End + 3 * Day + Hour);
            Assert.AreEqual(wallet, s.State.Wallet[ResourceType.Sparks], "nothing paid twice");
            Assert.AreEqual(sparks + share.sparks, run.SparksWon);
            s.Advance(run.End + 9 * Day);
            Assert.IsFalse(s.State.Events.Contains(run), "a week after the end the run is forgotten");
        }

        [Test]
        public void NothingCanBeDoneInAnEventAfterItsEnd()
        {
            foreach (string id in new[] { "LongNight", "Tournament", "Portal", "Treasure", "Fair", "Siege", "Festival" })
            {
                long t = DayOf(id);
                var s = Fortress(t);
                s.Advance(t + 1);
                var run = s.ActiveRun(id, t + 1);
                Assert.IsNotNull(run, id);
                long after = run.End + 13 * Hour; // 13:00 the day after: a Siege window would be open
                s.Advance(after);
                Assert.IsTrue(run.Closed, id);
                Assert.IsNull(s.ActiveRun(id, after));
                Assert.AreEqual(Fail.Locked, s.StartEventBattle(id, after, out _, 0), id);
                Assert.AreEqual(Fail.Locked, s.BuyEventExtra(id, after), id);
                // another event may already run: only the ended one's own actions are checked
                switch (id)
                {
                    case "Treasure":
                        Assert.AreEqual(Fail.Locked, s.OpenCell(GameSession.HexCenter, after, out _));
                        break;
                    case "Fair":
                        Assert.AreEqual(Fail.Locked, s.FairTrade(0, 1, 10, after, out _));
                        Assert.AreEqual(Fail.Locked, s.FairDeliver(0, after));
                        Assert.AreEqual(Fail.Locked, s.FairHint(after, out _));
                        break;
                    case "Siege":
                        Assert.AreEqual(Fail.Locked, s.StartSiegeDefense(after, out _));
                        Assert.AreEqual(Fail.NotFound, s.SetGarrison(0, new int[0], after));
                        Assert.AreEqual(Fail.NotFound, s.SiegeRepair(0, after));
                        Assert.AreEqual(Fail.NotFound, s.UpgradeTraps(0, after));
                        break;
                    case "Festival":
                        Assert.AreEqual(Fail.Locked, s.StartLanterns(after, out _));
                        break;
                    case "Portal":
                        Assert.AreEqual(Fail.Locked, s.PortalEnter(0, after, out _));
                        Assert.AreEqual(Fail.Locked, s.PortalRevive(after));
                        Assert.AreEqual(Fail.NotFound, s.ChooseBlessing(0, after));
                        break;
                }
                Assert.AreEqual(1.0, s.SiegePenalty(after));
            }
        }

        [Test]
        public void ABattleLeftOpenPastTheEndDoesNotCount()
        {
            long t = DayOf("LongNight");
            var s = Fortress(t);
            s.Advance(t + 1);
            var run = s.ActiveRun("LongNight", t + 1);
            Assert.AreEqual(Fail.None, s.StartEventBattle("LongNight", t + 1, out _));
            s.Advance(run.End + 1);
            Assert.IsNull(s.State.EventBattle, "the battle is dropped with the event");
            Assert.IsNull(s.ResolvePendingEventBattle(run.End + 1));
            Assert.AreEqual(0, run.Points);
        }

        [Test]
        public void OldSavesWithPaidEndsAreNotPaidAgain()
        {
            long t = DayOf("Portal");
            var s = Fortress(t);
            s.Advance(t + 1);
            var run = s.ActiveRun("Portal", t + 1);
            s.AddPoints(run, 100);
            run.Closed = true; // closed by the old engine: Rank stayed 0, the place was paid at the end
            run.End = t + 2;
            s.Advance(t + 3);
            Assert.IsTrue(run.Claimed && run.Burned);
            Assert.AreEqual(Fail.AlreadyQueued, s.ClaimEventReward(run));
        }

        [Test]
        public void ASaveInTheMiddleOfAnEventKeepsEverything()
        {
            long t = DayOf("Treasure");
            var s = Fortress(t);
            s.Advance(t + 1);
            var run = s.ActiveRun("Treasure", t + 1);
            int near = Enumerable.Range(0, 61).First(i => GameSession.HexDistance(i, GameSession.HexCenter) == 1 &&
                                                        s.CellContent(run, i) == GameSession.TreasureCell.Resources);
            Assert.AreEqual(Fail.None, s.OpenCell(near, t + 1, out _));
            s.AddPoints(run, s.RunTarget(run) * 0.3);
            Assert.AreEqual(Fail.None, s.StartEventBattle("Treasure", t + 2, out _, Enumerable.Range(0, 61).First(i => s.CellOpenable(run, i))));
            long sparks = s.State.Wallet[ResourceType.Sparks];

            var again = Reload(s);
            var copy = again.ActiveRun("Treasure", t + 2);
            Assert.AreEqual(run.Points, copy.Points);
            Assert.AreEqual(run.Thresholds, copy.Thresholds);
            Assert.AreEqual(run.Currency, copy.Currency);
            CollectionAssert.AreEqual(run.Opened, copy.Opened);
            Assert.AreEqual(run.Torches, copy.Torches);
            Assert.AreEqual(run.TorchAt, copy.TorchAt);
            Assert.AreEqual(s.TreasureCandidates(run), again.TreasureCandidates(copy), "the same map after loading");
            again.Advance(t + 2);
            Assert.AreEqual(sparks, again.State.Wallet[ResourceType.Sparks], "no threshold is paid twice by loading");
            Assert.IsNotNull(again.ResolvePendingEventBattle(t + 2), "the battle left open is fought out after loading");
            Assert.IsNull(again.State.EventBattle);
            // and the end comes as usual
            again.Advance(copy.End + Hour);
            Assert.IsTrue(copy.Closed);
            Assert.AreEqual(Fail.None, again.ClaimEventReward(copy));
        }

        // ================================================================== Long Night

        [Test]
        public void TheReapersPoolGoesThroughThreePhasesToTheDawn()
        {
            long t = DayOf("LongNight");
            var s = Fortress(t);
            s.Advance(t + 1);
            var run = s.ActiveRun("LongNight", t + 1);
            var phases = new List<int>();
            int last = 0;
            for (long now = run.Start; now < run.End; now += Hour)
            {
                int phase = s.NightPhase(run, now);
                Assert.GreaterOrEqual(phase, last, "the pool only goes down");
                last = phase;
                if (!phases.Contains(phase))
                    phases.Add(phase);
            }
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, phases);
            // phase 2: the Reaper calls up creatures before himself
            long two = Enumerable.Range(0, 48).Select(h => run.Start + h * Hour).First(x => s.NightPhase(run, x) == 2);
            Assert.AreEqual(Fail.None, s.StartEventBattle("LongNight", two, out var summons));
            Assert.AreEqual(2, summons.WaveCount);
            s.FinishEventBattle(summons, two, out _);
            long one = run.Start + Hour;
            Assert.AreEqual(1, s.NightPhase(run, one));
            // the Dawn: once, the earlier the better
            Assert.AreEqual(0, run.DawnAt);
            long shards = run.Currency;
            s.Advance(run.End - Hour);
            Assert.Greater(run.DawnAt, run.Start);
            Assert.Less(run.DawnAt, run.End);
            Assert.That(run.DawnTier, Is.InRange(1, 3));
            Assert.AreEqual(shards + 10 * run.DawnTier, run.Currency);
            s.Advance(run.End + Hour);
            Assert.AreEqual(shards + 10 * run.DawnTier, run.Currency, "one Dawn chest");
        }

        [Test]
        public void AStrongGroupMeetsTheDawnEarlierAndOneAttackIsCapped()
        {
            long t = DayOf("LongNight");
            var s = Fortress(t);
            s.Advance(t + 1);
            var run = s.ActiveRun("LongNight", t + 1);
            Assert.AreEqual(Fail.None, s.StartEventBattle("LongNight", t + 1, out var b));
            b.RunToEnd();
            b.DamageDealt = 1e15; // an impossible blow
            s.FinishEventBattle(b, t + 1, out _);
            Assert.AreEqual(0.03 * s.NightPool(run), run.Points, 1e-6, "no more than 3% of the pool from one attack");
            var weak = Fortress(t);
            weak.Advance(t + 1);
            var other = weak.ActiveRun("LongNight", t + 1);
            s.AddPoints(run, s.RunTarget(run) * 4);
            s.Advance(run.End);
            weak.Advance(other.End);
            Assert.Less(run.DawnAt - run.Start, other.DawnAt - other.Start, "more damage, an earlier dawn");
            Assert.GreaterOrEqual(run.DawnTier, other.DawnTier);
        }

        [Test]
        public void TheLongNightColdsTheRoomsAndTheAttacksComeBackEveryDay()
        {
            long t = DayOf("LongNight");
            var s = Fortress(t - Day);
            double warm = s.WarmthMult();
            s.Advance(t + 1);
            var run = s.ActiveRun("LongNight", t + 1);
            Assert.AreEqual(warm * 0.85, s.WarmthMult(), 1e-9, "warmth x0.85 while the Night lasts");
            for (int i = 0; i < 3; i++)
                Fight(s, "LongNight", t + 1);
            Assert.AreEqual(0, s.NightAttacksLeft(run, t + 1));
            Assert.AreEqual(3, s.NightAttacksLeft(run, t + Day + 1), "three new attacks the next day");
            s.State.Wallet[ResourceType.Sparks] = 100;
            Assert.AreEqual(Fail.None, s.BuyEventExtra("LongNight", t + Day + 1));
            Assert.AreEqual(Fail.LimitReached, s.BuyEventExtra("LongNight", t + Day + 1), "one bought attack a day");
            Assert.AreEqual(4, s.NightAttacksLeft(run, t + Day + 1));
            Assert.AreEqual(s.NightFamily(run), (MonsterFamily)Enum.Parse(typeof(MonsterFamily), s.NightFamily(run).ToString()));
            s.Advance(run.End + 1);
            Assert.AreEqual(warm, s.WarmthMult(), 1e-9, "the warmth comes back after the Night");
        }

        // ================================================================== Portal

        [Test]
        public void PortalDoorsOfferRestMerchantAndStash()
        {
            long t = DayOf("Portal");
            var s = Fortress(t);
            s.Advance(t + 1);
            var run = s.ActiveRun("Portal", t + 1);
            for (int floor = 0; floor < 30; floor++)
            {
                run.Floor = floor;
                var doors = s.PortalDoors(run);
                CollectionAssert.AreEqual(doors, s.PortalDoors(run), "the same doors for the same floor");
                if ((floor + 1) % 5 == 0)
                    CollectionAssert.AreEqual(new[] { GameSession.PortalDoor.Guardian }, doors, "a guardian every 5th floor");
                else
                {
                    Assert.AreEqual(GameSession.PortalDoor.Fight, doors[0]);
                    Assert.That(doors.Count, Is.InRange(2, 3));
                    Assert.AreEqual(doors.Count, doors.Distinct().Count());
                }
            }
            // find floors with each kind of door and walk through them
            foreach (var kind in new[] { GameSession.PortalDoor.Rest, GameSession.PortalDoor.Merchant, GameSession.PortalDoor.Stash })
            {
                var fresh = Fortress(t);
                fresh.Advance(t + 1);
                var r = fresh.ActiveRun("Portal", t + 1);
                r.RunAlive = true;
                r.Floor = Enumerable.Range(0, 9).First(f => { r.Floor = f; return f % 3 != 2 && fresh.PortalDoors(r).Contains(kind); });
                int index = fresh.PortalDoors(r).IndexOf(kind);
                int before = r.Floor;
                r.HpIds = fresh.Squad().Select(d => d.Id).ToList();
                r.HpShares = r.HpIds.Select(_ => 0.4).ToList();
                double points = r.Points;
                Assert.AreEqual(Fail.None, fresh.PortalEnter(index, t + 1, out string text), kind.ToString());
                Assert.AreEqual(before + 1, r.Floor, kind + " passes the floor");
                Assert.AreEqual(points + 10, r.Points, 1e-9);
                Assert.IsNotEmpty(text);
                if (kind == GameSession.PortalDoor.Rest)
                    Assert.IsTrue(r.HpShares.All(x => Math.Abs(x - 0.7) < 1e-9), "a rest heals 30%");
                if (kind == GameSession.PortalDoor.Merchant)
                {
                    Assert.AreEqual(3, r.BlessingOffer.Count);
                    Assert.AreEqual(8, r.OfferPrice);
                    r.Currency = 5;
                    Assert.AreEqual(Fail.NotEnough, fresh.ChooseBlessing(0, t + 1));
                    r.Currency = 10;
                    Assert.AreEqual(Fail.None, fresh.ChooseBlessing(0, t + 1));
                    Assert.AreEqual(2, r.Currency);
                    Assert.AreEqual(1, r.Blessings.Count);
                }
                if (kind == GameSession.PortalDoor.Stash)
                    Assert.IsTrue(r.HpShares.All(x => Math.Abs(x - 0.4) < 1e-9) || r.HpShares.All(x => Math.Abs(x - 0.2) < 1e-9), "a chest or a trap");
            }
        }

        [Test]
        public void ThePortalIsClimbedToTheHostessInThreeDays()
        {
            long t = DayOf("Portal");
            var s = Fortress(t, 18);
            s.State.Wallet[ResourceType.Sparks] = 10000;
            s.Advance(t + 1);
            var run = s.ActiveRun("Portal", t + 1);
            string hostess = "";
            for (int day = 0; day < 4 && run.Floor < 30; day++)
            {
                long now = t + day * Day;
                Assert.AreEqual(day < 3 ? 10 * (day + 1) : int.MaxValue, s.PortalFloorsOpen(run, now));
                for (int step = 0; step < 40 && run.Floor < Math.Min(30, s.PortalFloorsOpen(run, now)); step++)
                {
                    if (run.BlessingOffer.Count > 0 && s.ChooseBlessing(0, now) != Fail.None)
                        s.SkipBlessing(now);
                    if (!run.RunAlive && run.FreeRunDay == s.LocalDay(now) && s.PortalRevive(now) != Fail.None)
                        break;
                    var doors = s.PortalDoors(run);
                    int rest = doors.IndexOf(GameSession.PortalDoor.Rest);
                    if (run.RunAlive && rest >= 0 && run.HpShares.Count > 0 && run.HpShares.Average() < 0.7)
                    {
                        Assert.AreEqual(Fail.None, s.PortalEnter(rest, now, out _));
                        continue;
                    }
                    Assert.AreEqual(Fail.None, s.StartEventBattle("Portal", now, out var b));
                    b.Brain = Brains.Careful();
                    s.FinishEventBattle(b, now, out string result);
                    if (result.StartsWith("Хозяйка"))
                        hostess = result;
                }
                if (day < 2)
                    Assert.LessOrEqual(run.Floor, 10 * (day + 1), "floors open ten a day");
            }
            Assert.AreEqual(30, run.Floor, "the squad climbs all 30 floors");
            Assert.IsNotEmpty(hostess, "the Hostess of the Portal stands on the 30th");
            Assert.AreEqual(30, run.Checkpoint);
            Assert.AreEqual(10, run.Thresholds);
            Assert.GreaterOrEqual(run.Blessings.Count, 8, "8-10 gifts make a build");
        }

        [Test]
        public void HeroesFallenInThePortalRestForFourHours()
        {
            long t = DayOf("Portal") + 3 * Day; // the Depth: every floor open
            var s = Fortress(t, 6); // a weak squad against hearth-16 floors
            s.State.HearthLevel = 16;
            s.Advance(t + 1);
            var run = s.ActiveRun("Portal", t + 1);
            run.Floor = 28;
            run.RunAlive = true;
            run.FreeRunDay = s.LocalDay(t);
            Assert.AreEqual(Fail.None, s.StartEventBattle("Portal", t + 1, out var b));
            b.Brain = Brains.AutoBattle();
            s.FinishEventBattle(b, t + 1, out _);
            Assert.IsFalse(run.RunAlive);
            Assert.IsTrue(s.Squad().Any(d => d.InjuredUntil == t + 1 + 4 * Hour), "the fallen are tired, not dead");
            Assert.AreEqual(Fail.NoTracks, s.PortalEnter(0, t + 1, out _), "the free run of the day is used");
        }

        // ================================================================== Tournament

        [Test]
        public void TheTournamentSumsTheBestOfEachDayAndPublishesTheBestSquad()
        {
            long t = DayOf("Tournament");
            var s = Fortress(t);
            s.State.Wallet[ResourceType.Sparks] = 100;
            s.Advance(t + 1);
            var run = s.ActiveRun("Tournament", t + 1);
            Assert.AreEqual(30, s.GroupSize("Tournament"));
            double sum = 0;
            for (int day = 0; day < 3; day++)
            {
                long now = t + day * Day;
                Assert.AreEqual(day, s.TournamentRule(run, now));
                Assert.AreEqual(5, s.TournamentAttemptsLeft(run, now), "five attempts every day");
                if (day == 1)
                {
                    Assert.AreEqual(Fail.None, s.BuyEventExtra("Tournament", now));
                    Assert.AreEqual(Fail.None, s.BuyEventExtra("Tournament", now));
                    Assert.AreEqual(Fail.LimitReached, s.BuyEventExtra("Tournament", now), "two bought attempts a day");
                }
                while (s.TournamentAttemptsLeft(run, now) > 0)
                    Fight(s, "Tournament", now);
                Assert.AreEqual(Fail.NoTracks, s.StartEventBattle("Tournament", now, out _));
                sum += run.DayBest;
                var best = s.TournamentBest(run, day);
                Assert.AreEqual(5, best.squad.Length);
                Assert.Greater(best.score, 0);
                Assert.AreEqual(best.squad, s.TournamentBest(run, day).squad, "the same squad for everybody");
                if (day == 0)
                    Assert.IsFalse(best.squad.Contains(HeroClass.AshMage), "Steel: no mages");
                if (day == 2)
                    Assert.Contains(HeroClass.Cleaver, best.squad, "Knights: crushing blows");
            }
            Assert.AreEqual(sum, run.Points, 1e-6, "the total is the sum of the best battles of the days");
            s.Advance(run.End + 1);
            Assert.That(run.Rank, Is.InRange(1, 30));
            StringAssert.Contains("из 30", run.LastResult);
            Assert.AreEqual(Fail.None, s.ClaimEventReward(run));
            Assert.Greater(run.SparksWon, 0);
        }

        // ================================================================== Treasure hunters

        [Test]
        public void TorchesComeBackBoughtAndFromTheDailyTasks()
        {
            long t = DayOf("Treasure");
            var s = Fortress(t);
            s.Advance(t + 1);
            var run = s.ActiveRun("Treasure", t + 1);
            int cell = Enumerable.Range(0, 61).First(i => s.CellOpenable(run, i) && s.CellContent(run, i) == GameSession.TreasureCell.Resources);
            Assert.AreEqual(Fail.None, s.OpenCell(cell, t + 1, out _));
            Assert.AreEqual(5, s.TorchesLeft(run, t + 1));
            Assert.AreEqual(6, s.TorchesLeft(run, t + 1 + 2 * Hour), "one torch every two hours");
            Assert.AreEqual(6, s.TorchesLeft(run, t + 1 + 9 * Hour), "no more than six");
            Assert.AreEqual(8, s.TorchesLeft(run, t + Day + 1), "the daily tasks bring two more");
            s.State.Wallet[ResourceType.Sparks] = 100;
            for (int i = 0; i < 3; i++)
                Assert.AreEqual(Fail.None, s.BuyEventExtra("Treasure", t + Day + 1));
            Assert.AreEqual(Fail.LimitReached, s.BuyEventExtra("Treasure", t + Day + 1), "three torches a day");
            Assert.AreEqual(11, s.TorchesLeft(run, t + Day + 1));
            Assert.AreEqual(40, s.State.Wallet[ResourceType.Sparks]);
            run.Torches = 0;
            int next = Enumerable.Range(0, 61).First(i => s.CellOpenable(run, i));
            Assert.AreEqual(Fail.NoTracks, s.OpenCell(next, t + Day + 1, out _));
        }

        [Test]
        public void AnArcherSpotsTrapsAndTheTreasuryPaysTwoChests()
        {
            long t = DayOf("Treasure");
            var s = Fortress(t);
            s.Advance(t + 1);
            var run = s.ActiveRun("Treasure", t + 1);
            int trap = Enumerable.Range(0, 61).First(i => s.CellContent(run, i) == GameSession.TreasureCell.Trap);
            int Neighbour(int c) => Enumerable.Range(0, 61).First(i => GameSession.HexDistance(i, c) == 1);
            run.Opened.Add(Neighbour(trap));
            Assert.IsTrue(s.TrapSpotter());
            Assert.AreEqual(Fail.None, s.OpenCell(trap, t + 1, out var content));
            Assert.AreEqual(GameSession.TreasureCell.Trap, content);
            Assert.AreEqual(5, run.Torches, "the archer saw it: only the torch spent");
            int trap2 = Enumerable.Range(0, 61).First(i => i != trap && s.CellContent(run, i) == GameSession.TreasureCell.Trap);
            run.Opened.Add(Neighbour(trap2));
            foreach (var d in s.Squad().Where(d => d.HeroClass == HeroClass.Archer))
                d.Stats[(int)Stat.Agility] = 20;
            Assert.IsFalse(s.TrapSpotter());
            s.OpenCell(trap2, t + 1, out _);
            Assert.AreEqual(3, run.Torches, "no archer: the trap takes a torch more");

            // the first treasury: a rare dweller and coins; the second: a decoration
            int dwellers = s.State.Dwellers.Count;
            int treasure = Enumerable.Range(0, 61).Single(i => s.CellContent(run, i) == GameSession.TreasureCell.Treasure);
            run.Opened.Add(Neighbour(treasure));
            run.Torches = 6;
            long coins = run.Currency;
            double points = run.Points;
            Assert.AreEqual(Fail.None, s.OpenCell(treasure, t + 1, out content));
            Assert.AreEqual(GameSession.TreasureCell.Treasure, content);
            Assert.AreEqual(1, run.MapNo);
            Assert.AreEqual(dwellers + 1, s.State.Dwellers.Count);
            Assert.AreEqual(points + 51, run.Points, 1e-9);
            Assert.GreaterOrEqual(run.Currency, coins + 30);
            Assert.AreEqual(1, run.Opened.Count, "the second map starts from its centre");
            int treasure2 = Enumerable.Range(0, 61).Single(i => s.CellContent(run, i) == GameSession.TreasureCell.Treasure);
            run.Opened.Add(Neighbour(treasure2));
            Assert.AreEqual(Fail.None, s.OpenCell(treasure2, t + 1, out _));
            Assert.AreEqual(1, s.DecorOwned("chest_gold"));
            Assert.AreEqual(2, run.Treasures);
        }

        [Test]
        public void TreasureHuntersFindTheTreasuryInFiveDaysAndBuyTheirBlueprints()
        {
            long t = DayOf("Treasure");
            var s = Fortress(t);
            s.Advance(t + 1);
            var run = s.ActiveRun("Treasure", t + 1);
            foreach (int day in Enumerable.Range(0, 5))
            {
                foreach (double hour in new[] { 0.0, 4.5, 9, 13.5 })
                {
                    long now = t + day * Day + (long)(hour * Hour);
                    for (int i = 0; i < 12 && s.TorchesLeft(run, now) > 0; i++)
                    {
                        var targets = s.TreasureCandidates(run);
                        int cell = Enumerable.Range(0, 61).Where(c => s.CellOpenable(run, c))
                            .OrderBy(c => targets.Count == 0 ? 0 : targets.Min(x => GameSession.HexDistance(c, x))).First();
                        Assert.AreEqual(Fail.None, s.OpenCell(cell, now, out var content));
                        if (content == GameSession.TreasureCell.Ambush)
                            Fight(s, "Treasure", now, cell);
                    }
                }
            }
            Assert.GreaterOrEqual(run.Treasures, 1, "a player of 4 sessions a day finds the first treasury");
            Assert.AreEqual(10, run.Thresholds);
            long bp = s.State.Wallet[ResourceType.Blueprints];
            Assert.AreEqual(Fail.None, s.BuyEventGoods(run, 0, t + 4 * Day + 15 * Hour));
            Assert.AreEqual(bp + 10, s.State.Wallet[ResourceType.Blueprints], "the model's 10 blueprints an event");
        }

        // ================================================================== Fair

        [Test]
        public void TheFairHintShowsTheNextRatesForAToken()
        {
            long t = DayOf("Fair");
            var s = Fortress(t);
            s.Advance(t + 1);
            var run = s.ActiveRun("Fair", t + 1);
            Assert.AreEqual(Fail.NotEnough, s.FairHint(t + 1, out _), "no tokens yet");
            Assert.IsNull(s.FairHinted(run, t + 1));
            s.AddPoints(run, s.RunTarget(run) / 10);
            Assert.AreEqual(10, run.Currency);
            Assert.AreEqual(Fail.None, s.FairHint(t + 1, out var next));
            Assert.AreEqual(9, run.Currency);
            Assert.AreEqual(Fail.None, s.FairHint(t + 2, out _));
            Assert.AreEqual(9, run.Currency, "one token a window");
            CollectionAssert.AreEqual(next, s.FairHinted(run, t + 1));
            long window = (t + 1) / (4 * Hour);
            CollectionAssert.AreEqual(next, s.FairRates(run, (window + 1) * 4 * Hour + 1).ToArray(), "the hint tells the truth");
        }

        [Test]
        public void TheFairLimitsDealsADayAndTheWandererComesOnce()
        {
            long t = DayOf("Fair");
            var s = Fortress(t);
            s.State.Wallet[ResourceType.Coal] = 1000000;
            s.Advance(t + 1);
            var run = s.ActiveRun("Fair", t + 1);
            for (int i = 0; i < 20; i++)
                Assert.AreEqual(Fail.None, s.FairTrade(0, 1, 100, t + 1, out _));
            Assert.AreEqual(Fail.NoTracks, s.FairTrade(0, 1, 100, t + 1, out _));
            Assert.AreEqual(Fail.None, s.FairTrade(0, 1, 100, t + Day + 1, out _), "twenty new deals the next day");
            int dwellers = s.State.Dwellers.Count;
            s.AddPoints(run, s.RunTarget(run));
            Assert.AreEqual(10, run.Thresholds);
            Assert.AreEqual(dwellers + 1, s.State.Dwellers.Count);
            Assert.AreEqual("Странник", s.State.Dwellers.Last().Name);
            s.AddPoints(run, s.RunTarget(run));
            Assert.AreEqual(dwellers + 1, s.State.Dwellers.Count, "one Wanderer per Fair");
            // orders: delivered once, the next ones come with the next rates
            s.FairRates(run, t + Day + 1);
            s.State.Wallet[ResourceType.Food] = 1000000;
            s.State.Wallet[ResourceType.Iron] = 1000000;
            s.State.Wallet[ResourceType.Silver] = 1000000;
            int orders = run.Orders.Count;
            Assert.AreEqual(Fail.None, s.FairDeliver(0, t + Day + 1));
            Assert.AreEqual(orders - 1, run.Orders.Count);
            s.FairRates(run, t + Day + 4 * Hour + 1);
            Assert.AreEqual(3, run.Orders.Count);
        }

        // ================================================================== Siege

        [Test]
        public void ScoutsReportAnHourAheadAndTheCommanderGoesForTheWeakest()
        {
            long t = DayOf("Siege");
            var s = Fortress(t);
            s.Advance(t + 1);
            var run = s.ActiveRun("Siege", t + 1);
            long noon = s.SiegeNextWindow(run, t + 1);
            Assert.AreEqual(t + 4 * Hour, s.SiegeWindowStart(noon), "the next window opens at 12:00");
            Assert.IsFalse(s.SiegeScouted(run, noon, t + 3 * Hour - 1));
            Assert.IsTrue(s.SiegeScouted(run, noon, t + 3 * Hour), "the scouts come back at 11:00");
            Assert.That(s.SiegeEnemy(run, noon), Is.InRange(0, 3));
            Assert.AreEqual(2, s.SiegeTargets(run, noon).Count);
            // eight windows: the last one is the Commander's, on all five outposts
            var windows = Enumerable.Range(0, 4).SelectMany(d => new[] { s.LocalDay(t) * 2 + d * 2, s.LocalDay(t) * 2 + d * 2 + 1 }).ToList();
            Assert.AreEqual(1, windows.Count(w => s.SiegeFinal(run, w)));
            Assert.IsTrue(s.SiegeFinal(run, windows.Last()));
            Assert.AreEqual(5, s.SiegeTargets(run, windows.Last()).Count);
            // the strongest garrisons everywhere but the Ford: the Commander goes to the Ford
            var guards = s.State.Dwellers.Where(d => !d.IsHero).Select(d => d.Id).ToList();
            for (int o = 0; o < 5; o++)
            {
                var g = s.AddDweller("Страж " + o, new[] { 40, 40, 40, 40, 40 });
                if (o != 1)
                    s.SetGarrison(o, new[] { g.Id }, t + 1);
            }
            Assert.AreEqual(1, s.SiegeWeakest(run, windows.Last()));
            // matching the enemy matters: Agility against creatures
            var archer = new Outpost();
            var wall = new Outpost();
            archer.Garrison.Add(s.AddDweller("Лучница", new[] { 10, 40, 10, 10, 10 }).Id);
            wall.Garrison.Add(s.AddDweller("Здоровяк", new[] { 10, 10, 10, 40, 10 }).Id);
            Assert.Greater(s.GarrisonPower(archer, 0), s.GarrisonPower(wall, 0));
            Assert.Greater(s.GarrisonPower(wall, 1), s.GarrisonPower(archer, 1));
        }

        [Test]
        public void TheFinalWindowIsFoughtWhereTheSquadStands()
        {
            long t = DayOf("Siege");
            var s = Fortress(t);
            s.Advance(t + 1);
            var run = s.ActiveRun("Siege", t + 1);
            long final = t + 3 * Day + 12 * Hour + 1; // Sunday 20:00
            s.Advance(final);
            Assert.IsTrue(s.SiegeFinal(run, s.SiegeWindow(run, final)));
            Assert.AreEqual(7, run.WindowsDone.Count, "seven windows passed on their own");
            foreach (var o in run.Outposts)
                o.Fallen = false;
            double points = run.Points;
            Assert.AreEqual(Fail.None, s.StartSiegeDefense(final, out var d, 3));
            Assert.AreEqual(3, run.Stand);
            d.Brain = SurgeDefense.Careful();
            Assert.AreEqual(Fail.None, s.FinishSiegeDefense(d, final));
            Assert.IsFalse(run.Outposts[3].Fallen, "the squad held the Watch hill");
            Assert.AreEqual(4, run.Outposts.Where((o, i) => i != 3).Count(o => o.Fallen), "empty garrisons fell");
            Assert.AreEqual(points + 15, run.Points, 1e-9, "one outpost held live: 10 x 1.5");
            StringAssert.StartsWith("Рыцарь-командор", run.LastResult);
        }

        [Test]
        public void WallsAreRepairedForSparksThreeTimesAnEvent()
        {
            long t = DayOf("Siege");
            var s = Fortress(t);
            s.Advance(t + 7 * Hour); // the noon window fell
            s.State.Wallet[ResourceType.Sparks] = 100;
            var run = s.ActiveRun("Siege", t + 7 * Hour);
            var fallen = Enumerable.Range(0, 5).Where(i => run.Outposts[i].Fallen).ToList();
            Assert.AreEqual(2, fallen.Count);
            Assert.AreEqual(Fail.NotFound, s.SiegeRepair(Enumerable.Range(0, 5).First(i => !fallen.Contains(i)), t + 7 * Hour));
            Assert.AreEqual(Fail.None, s.SiegeRepair(fallen[0], t + 7 * Hour));
            Assert.AreEqual(Fail.None, s.SiegeRepair(fallen[1], t + 7 * Hour));
            Assert.AreEqual(1.0, s.SiegePenalty(t + 7 * Hour));
            Assert.AreEqual(60, s.State.Wallet[ResourceType.Sparks]);
            s.Advance(t + 15 * Hour); // the evening window
            var down = Enumerable.Range(0, 5).Where(i => run.Outposts[i].Fallen).ToList();
            Assert.AreEqual(Fail.None, s.SiegeRepair(down[0], t + 15 * Hour));
            Assert.AreEqual(Fail.LimitReached, s.SiegeRepair(down[1], t + 15 * Hour), "three repairs an event");
        }

        [Test]
        public void ASiegeSpentOfflineIsFoughtWindowByWindow()
        {
            long t = DayOf("Siege");
            var s = Fortress(t);
            s.Advance(t + 1);
            var run = s.ActiveRun("Siege", t + 1);
            int o = 0;
            foreach (var g in s.State.Dwellers.Where(d => !d.IsHero).ToList())
                s.SetGarrison(o++ % 5, new[] { g.Id }, t + 1);
            for (int i = 0; i < 10; i++)
                s.SetGarrison(i % 5, run.Outposts[i % 5].Garrison.Concat(new[] { s.AddDweller("Ополченец", new[] { 60, 60, 60, 60, 60 }).Id }).Take(3).ToList(), t + 1);
            s.Advance(run.End + 2 * Hour); // away for the whole siege
            Assert.IsTrue(run.Closed);
            Assert.AreEqual(8, run.WindowsDone.Count, "all eight windows fought while away");
            Assert.Greater(run.Points, 0, "strong garrisons hold on their own");
            Assert.AreEqual(1.0, s.SiegePenalty(run.End + 2 * Hour), "the Corruption ends with the event");
            Assert.IsTrue(run.Outposts.All(x => !x.Fallen));
            Assert.AreEqual(Fail.None, s.ClaimEventReward(run));
        }

        // ================================================================== Festival

        [Test]
        public void TheFestivalCountsTheBestLanternsOfTheDayAndTheServerGoal()
        {
            long t = DayOf("Festival");
            var s = Fortress(t);
            s.Advance(t + 1);
            var run = s.ActiveRun("Festival", t + 1);
            Assert.AreEqual(10 * Day, run.End - run.Start);
            int[] scores = new int[3];
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(Fail.None, s.StartLanterns(t + 1, out int seed));
                var taps = GameSession.Lanterns(seed).Take(10 * (i + 1)).ToList();
                s.FinishLanterns(seed, taps, t + 1, out scores[i]);
            }
            Assert.AreEqual(Fail.NoTracks, s.StartLanterns(t + 1, out _), "three games a day");
            Assert.AreEqual(scores.Max(), run.Points, 1e-9, "the best game of the day counts");
            Assert.AreEqual(Fail.None, s.StartLanterns(t + Day + 1, out _), "three new games the next day");
            Assert.Less(s.FestivalServerGoal(run, t + Day), 1);
            Assert.GreaterOrEqual(s.FestivalServerGoal(run, run.End), 1, "the server lights its ten million");
            s.Advance(run.End + 1);
            long before = run.Currency;
            Assert.AreEqual(Fail.None, s.ClaimEventReward(run));
            Assert.AreEqual(before + s.RankCurrency(run, run.Rank) + 30, run.Currency, "the place and the server's chest");
        }

        // ================================================================== sixty days of a bot

        [Test]
        public void SixtyDaysEveryScheduledEventHappensAndPaysItsBudget()
        {
            var s = TestData.NewSession();
            s.Combat = TestData.Combat();
            var bot = new PlayerBot(s);
            var runs = new Dictionary<string, EventRun>();
            var level = new Dictionary<long, int>();
            bot.PlayDays(TestData.Start, 60, day =>
            {
                level[s.LocalDay(TestData.Start) + day - 1] = s.State.HearthLevel;
                foreach (var r in s.State.Events)
                    runs[r.Id + ":" + r.Start] = r;
            });
            long first = s.LocalDay(TestData.Start);
            var all = runs.Values.ToList();
            foreach (var r in all.Where(x => x.Closed))
                TestContext.WriteLine(string.Format("{0,-10} day {1,2}{2}: {3:0} pts, {4}/10, rank {5}, {6} sparks, bought {7}", r.Id,
                    s.LocalDay(r.Start) - first, r.Personal ? " (personal)" : "", r.Points, r.Thresholds, r.Rank, r.SparksWon,
                    string.Join(",", r.Bought.Keys)));

            // every event opened, the first time as the newcomer's personal day
            foreach (var e in s.Balance.Activities.Events)
                Assert.IsTrue(all.Any(r => r.Id == e.Id), e.Id + " happened");
            // every calendar start after the event unlocked opened a run, and once
            foreach (var e in s.Balance.Activities.Events)
            {
                for (long d = first + 1; d < first + 58; d++)
                {
                    if (!s.StartsOn(e, d) || level[d - 1] < e.Unlock)
                        continue;
                    long start = d * Day - s.State.UtcOffsetSeconds;
                    Assert.AreEqual(1, all.Count(r => r.Id == e.Id && r.Start == start), e.Id + " on day " + (d - first));
                }
            }
            var calendar = all.Where(r => !r.Personal && r.Closed && r.End <= s.State.Time - Day).ToList();
            Assert.GreaterOrEqual(calendar.Count, 16, "about 11 events every 28 days once all are open");
            foreach (var r in all.Where(x => x.Closed && x.End <= s.State.Time - Day))
            {
                var e = s.EventInfo(r.Id);
                double budget = e.Sparks * (r.Personal ? 1.0 / e.Days : 1);
                Assert.IsTrue(r.Claimed && r.Burned, r.Id + ": the end is taken and the currency burned");
                Assert.LessOrEqual(r.SparksWon, budget + 6, r.Id + ": never more than the budget");
                if (!r.Personal)
                {
                    // EVENTS.md: a regular player of 2-3 sessions takes most thresholds of every event
                    Assert.GreaterOrEqual(r.Thresholds, 5, r.Id + " day " + (s.LocalDay(r.Start) - first));
                    Assert.GreaterOrEqual(r.SparksWon, 0.44 * budget, r.Id);
                }
            }
            Assert.Less(calendar.Count(r => r.Rank == 1), calendar.Count / 3, "the first place is not for every event");
            // the activity resources of the economy model come from the shops
            foreach (var r in calendar.Where(x => x.Thresholds >= 5))
            {
                if (r.Id == "LongNight") Assert.AreEqual(1, r.Bought.GetValueOrDefault("Cores"), "Сердцевины of the Long Night");
                if (r.Id == "Siege") Assert.AreEqual(1, r.Bought.GetValueOrDefault("Ember"), "Жар of the Siege");
                if (r.Id == "Treasure") Assert.AreEqual(1, r.Bought.GetValueOrDefault("Blueprints"), "Чертежи of the Treasure hunters");
            }
            // ECONOMY.md: events are the main source of Sparks, about 10 000 a year (27 a day) once all are open;
            // over days 15-57 a regular player wins most of that budget
            var open = calendar.Where(r => s.LocalDay(r.Start) - first >= 15).ToList();
            double won = open.Sum(r => r.SparksWon), budgetSum = open.Sum(r => (double)s.EventInfo(r.Id).Sparks);
            TestContext.WriteLine(string.Format("days 15-57: {0} events, {1:0} of {2:0} sparks ({3:0}%)", open.Count, won, budgetSum, won / budgetSum * 100));
            Assert.That(won / budgetSum, Is.InRange(0.75, 1.0));
            Assert.That(won / 42.0, Is.InRange(15, 40), "Sparks a day from events");
        }
    }
}
