using System;
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
            long before = s.State.Wallet[ResourceType.Sparks];
            s.Advance(run.End + 1);
            Assert.IsTrue(run.Closed);
            Assert.AreEqual(before + (long)Math.Round(e.Sparks * 0.20), s.State.Wallet[ResourceType.Sparks], 5);
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
    }
}
