using System;
using System.Linq;
using LastHearth.Core;
using LastHearth.Core.Combat;
using NUnit.Framework;

namespace LastHearth.Core.Tests
{
    public class PvpTests
    {
        const long Hour = 3600;
        const long Day = 86400;

        /// <summary>A fortress as a regular player has it: seven heroes at 2L - 2 (the hearth gate).</summary>
        static GameSession Fortress(int hearth = 18, long now = TestData.Start)
        {
            var s = NewGame.Create(TestData.Balance(), now);
            s.Combat = TestData.Combat();
            s.State.HearthLevel = hearth;
            s.State.Dwellers.Clear();
            var classes = new[] { HeroClass.Guardian, HeroClass.Swordsman, HeroClass.Cleaver, HeroClass.Archer, HeroClass.CoalPriest, HeroClass.AshMage, HeroClass.Swordsman };
            foreach (var c in classes)
            {
                int stat = 5 + 2 * hearth / 3 + 4;
                var d = s.AddDweller(c.ToString(), new[] { stat, stat, stat, stat, stat });
                d.HeroClass = c;
                d.HeroLevel = 2 * hearth - 2;
                d.SkillLevel = Math.Max(1, hearth / 5);
            }
            return s;
        }

        static double WinRate(int hearth, double power, bool raid, bool careful, int fights = 40)
        {
            int won = 0;
            for (int i = 0; i < fights; i++)
            {
                var s = Fortress(hearth);
                var pb = new PvpBattle { Raid = raid, Seed = 1000 + i, Opponent = new PvpOpponent { HearthLevel = hearth, Power = power, Seed = i * 7919 } };
                var battle = new Battle(s.Combat, s.PvpSetup(pb)) { Brain = careful ? Brains.Careful() : Brains.AutoBattle() };
                battle.RunToEnd();
                if (battle.Won)
                    won++;
            }
            return (double)won / fights;
        }

        [Test, Explicit]
        public void Sweep()
        {
            var c = TestData.Combat();
            foreach (var kind in new[] { "Arena", "Raid" })
            {
                foreach (double hp in new[] { 2.0, 2.5, 3, 3.5, 4 })
                {
                    foreach (double atk in new[] { 1.0, 1.2, 1.4 })
                    {
                        c.MissionKinds[kind].HpMult = hp;
                        c.MissionKinds[kind].AttackMult = atk;
                        TestContext.WriteLine("{0} hp {1} atk {2}: weak {3:P0} equal {4:P0} strong {5:P0} auto {6:P0}", kind, hp, atk,
                            WinRate(20, 0.9, kind == "Raid", true, 20), WinRate(20, 1.0, kind == "Raid", true, 20),
                            WinRate(20, 1.12, kind == "Raid", true, 20), WinRate(20, 1.0, kind == "Raid", false, 20));
                    }
                }
            }
        }

        [Test]
        public void ArenaIsWonMostlyByTacticsAgainstAnEqualDefense()
        {
            foreach (int hearth in new[] { 16, 22, 28 })
            {
                double weak = WinRate(hearth, 0.9, false, true);
                double equal = WinRate(hearth, 1.0, false, true);
                double strong = WinRate(hearth, 1.12, false, true);
                double auto = WinRate(hearth, 1.0, false, false);
                TestContext.WriteLine("arena hearth {0}: weak {1:P0} equal {2:P0} strong {3:P0} | auto equal {4:P0}", hearth, weak, equal, strong, auto);
                Assert.GreaterOrEqual(weak, 0.8, "a weaker defense is a sure win");
                Assert.That(equal, Is.InRange(0.5, 0.95), "an equal defense is a fair fight");
                Assert.Less(strong, weak);
                Assert.Less(auto, equal, "playing yourself beats the auto-battle");
            }
        }

        [Test]
        public void RaidsAreWonByARegularSquad()
        {
            foreach (int hearth in new[] { 17, 23, 29 })
            {
                double equal = WinRate(hearth, 1.0, true, true);
                double strong = WinRate(hearth, 1.12, true, true);
                TestContext.WriteLine("raid hearth {0}: equal {1:P0} strong {2:P0}", hearth, equal, strong);
                Assert.That(equal, Is.InRange(0.5, 1.0));
                Assert.LessOrEqual(strong, equal);
            }
        }

        [Test]
        public void ArenaOpensAtSixteenWithFiveTicketsThatComeBackEveryThreeHours()
        {
            var s = Fortress(15);
            long t = TestData.Start;
            Assert.AreEqual(Fail.Locked, s.StartArena(0, t, out _));
            s.State.HearthLevel = 16;
            Assert.AreEqual(5, s.ArenaTickets(t));
            Assert.AreEqual(3, s.ArenaOpponents(t).Count);
            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual(Fail.None, s.StartArena(0, t, out var battle));
                battle.Brain = Brains.Careful();
                Assert.AreEqual(Fail.None, s.FinishPvp(battle, t, out _));
            }
            Assert.AreEqual(Fail.NoTracks, s.StartArena(0, t, out _));
            Assert.AreEqual(1, s.ArenaTickets(t + 3 * Hour));
            Assert.AreEqual(5, s.ArenaTickets(t + 15 * Hour));
        }

        [Test]
        public void WinsRaiseTheRatingAndPayGloryAndEssence()
        {
            var s = Fortress(18);
            long t = TestData.Start;
            int rating = s.State.ArenaRating == 0 ? s.Balance.Activities.Pvp.StartRating : s.State.ArenaRating;
            Assert.AreEqual(Fail.None, s.StartArena(0, t, out var battle));
            battle.Brain = Brains.Careful();
            long essence = s.State.Wallet[ResourceType.Essence];
            s.FinishPvp(battle, t, out var reward);
            Assert.IsTrue(reward.Won, "the weakest of the three defenses");
            Assert.That(reward.RatingChange, Is.InRange(15, 30));
            Assert.AreEqual(rating + reward.RatingChange, s.State.ArenaRating);
            Assert.AreEqual(10, s.State.Wallet[ResourceType.Glory]);
            Assert.Greater(s.State.Wallet[ResourceType.Essence], essence);
        }

        [Test]
        public void ExtraTicketsCostTwentyFortySixtySparksThreeADay()
        {
            var s = Fortress(16);
            long t = TestData.Start;
            s.State.Wallet[ResourceType.Sparks] = 200;
            for (int i = 0; i < 3; i++)
                Assert.AreEqual(Fail.None, s.BuyArenaTicket(t));
            Assert.AreEqual(Fail.LimitReached, s.BuyArenaTicket(t));
            Assert.AreEqual(80, s.State.Wallet[ResourceType.Sparks]);
            Assert.AreEqual(8, s.ArenaTickets(t));
            Assert.AreEqual(20, s.ArenaTicketPrice(t + Day));
        }

        [Test]
        public void TheLeaguePaysOnSundayEveningOnlyToThoseWhoFought()
        {
            var s = Fortress(18);
            long t = TestData.Start;
            s.Advance(t);
            s.Advance(t + 8 * Day);
            Assert.IsFalse(s.State.PvpLog.Any(l => l.StartsWith("Итоги недели")), "no fights, no league reward");

            long now = t + 8 * Day;
            Assert.AreEqual(Fail.None, s.StartArena(0, now, out var battle));
            s.FinishPvp(battle, now, out _);
            var league = s.League;
            long glory = s.State.Wallet[ResourceType.Glory];
            long sparks = s.State.Wallet[ResourceType.Sparks];
            s.Advance(now + 7 * Day);
            Assert.GreaterOrEqual(s.State.Wallet[ResourceType.Sparks], sparks + 20);
            Assert.GreaterOrEqual(s.State.Wallet[ResourceType.Glory], glory + 100);
            Assert.IsTrue(s.State.PvpLog.Any(l => l.StartsWith("Итоги недели")));
            _ = league;
        }

        [Test]
        public void TheWeekEndsOnSundayAtTenInTheEvening()
        {
            var s = Fortress(18);
            // 1970-01-04 was a Sunday: its 22:00 opens week 0
            long sunday = 3 * Day + 22 * Hour;
            Assert.AreEqual(-1, s.ArenaWeekKey(sunday - 1));
            Assert.AreEqual(0, s.ArenaWeekKey(sunday));
            Assert.AreEqual(1, s.ArenaWeekKey(sunday + 7 * Day));
            s.State.UtcOffsetSeconds = 3 * 3600;
            Assert.AreEqual(0, s.ArenaWeekKey(sunday - 3 * Hour));
        }

        [Test]
        public void RaidsTakeFlagsPayLootByStarsAndNobodyLosesAnything()
        {
            var s = Fortress(17);
            long t = TestData.Start;
            s.State.Wallet[ResourceType.Silver] = 0;
            s.State.Wallet[ResourceType.Iron] = 0;
            Assert.AreEqual(3, s.RaidFlags(t));
            Assert.AreEqual(Fail.None, s.StartRaid(0, false, t, out var battle));
            battle.Brain = Brains.Careful();
            s.FinishPvp(battle, t, out var reward);
            Assert.AreEqual(2, s.RaidFlags(t));
            Assert.AreEqual(3, s.RaidFlags(t + 4 * Hour), "one flag comes back in 4 hours");
            if (reward.Stars == 3)
                Assert.AreEqual(20, s.State.Wallet[ResourceType.Glory]);
            long expected = (long)Math.Round(s.IncomePerHour() * 0.5 * reward.Stars / 3.0);
            Assert.AreEqual(expected, reward.Materials, 1);
            Assert.AreEqual(2, s.RaidTargets(t).Count, "the raided fortress leaves the list");
        }

        [Test]
        public void AGhostRaidGivesAFreeRevengeWithDoubleLootForADay()
        {
            var s = Fortress(17);
            long t = TestData.Start;
            s.Advance(t);
            s.Advance(t + Day);
            Assert.AreEqual(1, s.State.Revenge.Count);
            Assert.IsTrue(s.State.PvpLog.Any(l => l.Contains("ничего не потеряли")));
            long coal = s.State.Wallet[ResourceType.Coal];
            int flags = s.RaidFlags(t + Day);
            Assert.AreEqual(Fail.None, s.StartRaid(0, true, t + Day, out var battle));
            battle.Brain = Brains.Careful();
            s.FinishPvp(battle, t + Day, out var reward);
            Assert.AreEqual(flags, s.RaidFlags(t + Day), "revenge is free");
            Assert.AreEqual((long)Math.Round(s.IncomePerHour() * 1.0 * reward.Stars / 3.0), reward.Materials, 1);
            Assert.AreEqual(0, s.State.Revenge.Count);

            s.Advance(t + 2 * Day + Hour);
            s.Advance(t + 3 * Day + 2 * Hour);
            Assert.AreEqual(1, s.State.Revenge.Count, "an old revenge expires after 24 hours");
            _ = coal;
        }

        [Test]
        public void AnUnfinishedFightIsFinishedByAutoBattle()
        {
            var s = Fortress(18);
            long t = TestData.Start;
            Assert.AreEqual(Fail.None, s.StartArena(1, t, out _));
            Assert.AreEqual(Fail.HuntInProgress, s.StartRaid(0, false, t, out _));
            Assert.IsNotNull(s.ResolvePendingPvp(t + Hour));
            Assert.IsNull(s.State.PvpBattle);
        }

        [Test]
        public void TheGloryShopHasWeeklyLimits()
        {
            var s = Fortress(18);
            long t = TestData.Start;
            s.State.Wallet[ResourceType.Glory] = 5000;
            long essence = s.State.Wallet[ResourceType.Essence];
            for (int i = 0; i < 3; i++)
                Assert.AreEqual(Fail.None, s.BuyGlory(0, t));
            Assert.AreEqual(Fail.LimitReached, s.BuyGlory(0, t));
            Assert.AreEqual(essence + 3 * (long)Math.Round(s.Balance.Activities.EssenceByLevel[17] * 0.25), s.State.Wallet[ResourceType.Essence]);
            Assert.AreEqual(Fail.None, s.BuyGlory(1, t));
            Assert.AreEqual(3600, s.State.HourglassSeconds);
            int dwellers = s.State.Dwellers.Count;
            Assert.AreEqual(Fail.None, s.BuyGlory(2, t));
            Assert.AreEqual(dwellers + 1, s.State.Dwellers.Count);
            Assert.AreEqual(5000 - 450 - 250 - 900, s.State.Wallet[ResourceType.Glory]);
            Assert.AreEqual(3, s.GloryLeft(0, t + 7 * Day));
        }

        [Test]
        public void TheDefenseIsAttackedTwiceADayAndCostsAtMostEightRating()
        {
            var s = Fortress(18);
            long t = TestData.Start;
            s.Advance(t);
            int rating = s.State.ArenaRating;
            s.Advance(t + Day);
            Assert.AreEqual(2, s.State.PvpLog.Count(l => l.Contains("оборону")));
            Assert.That(s.State.ArenaRating - rating, Is.InRange(-16, 8));
        }
    }
}
