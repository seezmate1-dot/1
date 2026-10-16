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
            Assert.IsTrue(s.State.PvpJournal.Single(r => r.Raid && r.Attacked).Avenged, "the journal knows the revenge was taken");
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

        [Test]
        public void OpponentCardsShowTheirSquadAndWhatAWinOrALossWouldBring()
        {
            var s = Fortress(18);
            long t = TestData.Start;
            var list = s.ArenaOpponents(t);
            int size = 5 + s.Balance.SquadSlots.Count(gate => 18 >= gate);
            foreach (var o in list)
            {
                var squad = s.OpponentSquad(o);
                Assert.AreEqual(5 + s.Balance.SquadSlots.Count(gate => o.HearthLevel >= gate), squad.Count);
                Assert.IsTrue(squad[0].Class == HeroClass.Guardian || squad[0].Class == HeroClass.Swordsman, "a front hero leads");
                Assert.IsTrue(squad.GroupBy(h => h.Class).All(g => g.Count() <= 2));
                Assert.IsTrue(squad.All(h => Math.Abs(h.Level - (2 * o.HearthLevel - 2)) <= 2));
                Assert.AreEqual(squad.Select(h => h.Class + ":" + h.Level), s.OpponentSquad(o).Select(h => h.Class + ":" + h.Level), "the card stays the same");
                Assert.That(s.ArenaRatingChange(o, true), Is.InRange(15, 30));
                Assert.That(s.ArenaRatingChange(o, false), Is.InRange(-20, -10));
            }
            Assert.Greater(size, 4);
            int win = s.ArenaRatingChange(list[0], true), loss = s.ArenaRatingChange(list[0], false);
            Assert.AreEqual(Fail.None, s.StartArena(0, t, out var battle));
            battle.Brain = Brains.Careful();
            s.FinishPvp(battle, t, out var reward);
            Assert.AreEqual(reward.Won ? win : loss, reward.RatingChange, "the card told the truth");
        }

        [Test]
        public void TheMightOfAHeroGrowsWithHisLevelAndTheSquadSumsIt()
        {
            var s = Fortress(18);
            var hero = s.State.Dwellers.First(d => d.IsHero);
            double before = s.HeroPower(hero);
            Assert.Greater(before, 0);
            hero.HeroLevel += 5;
            Assert.Greater(s.HeroPower(hero), before);
            Assert.AreEqual(s.Squad().Sum(s.HeroPower), s.SquadPower(), 1e-9);
        }

        [Test]
        public void TheArenaDefenseKeepsItsOrderAndAStrongOneHoldsMore()
        {
            var s = Fortress(18);
            var heroes = s.State.Dwellers.Where(d => d.IsHero).Select(d => d.Id).ToList();
            Assert.AreEqual(s.Squad().Take(5).Select(d => d.Id), s.ArenaDefenders().Select(d => d.Id), "no choice: the five strongest");
            var order = new[] { heroes[4], heroes[0], heroes[2] };
            Assert.AreEqual(Fail.None, s.SetArenaDefense(order));
            Assert.AreEqual(order, s.ArenaDefenders().Select(d => d.Id).ToArray());
            Assert.AreEqual(Fail.NotAHero, s.SetArenaDefense(heroes.Take(6).ToList()), "five at most");
            Assert.AreEqual(Fail.NotAHero, s.SetArenaDefense(new[] { 9999 }));

            // the closer the defense comes to the five strongest, the more often it holds
            Assert.AreEqual(Fail.None, s.SetArenaDefense(new int[0]));
            Assert.AreEqual(0.45, s.ArenaDefenseHold(), 1e-9, "no choice made");
            Assert.AreEqual(Fail.None, s.SetArenaDefense(s.Squad().Take(5).Select(d => d.Id).ToList()));
            Assert.AreEqual(0.55, s.ArenaDefenseHold(), 1e-9);
            var weak = s.State.Dwellers.First(d => d.IsHero);
            weak.HeroLevel = 1;
            Assert.AreEqual(Fail.None, s.SetArenaDefense(new[] { weak.Id }));
            Assert.Less(s.ArenaDefenseHold(), 0.47);
        }

        [Test]
        public void AttacksOnTheDefenseGoToTheJournalAndCanBeAvengedOnceForATicket()
        {
            var s = Fortress(18);
            long t = TestData.Start;
            s.Advance(t);
            s.Advance(t + Day);
            var attacks = s.State.PvpJournal.Where(r => r.Attacked && !r.Raid).ToList();
            Assert.AreEqual(2, attacks.Count);
            Assert.IsTrue(attacks.All(r => r.Time <= t + Day && r.RatingChange == (r.Won ? 4 : -8)));
            int index = s.State.PvpJournal.IndexOf(attacks[1]);
            long now = t + Day;
            Assert.IsTrue(s.CanAvenge(attacks[1], now));
            var list = s.ArenaOpponents(now).Select(o => o.Seed).ToList();
            int tickets = s.ArenaTickets(now);
            Assert.AreEqual(Fail.None, s.StartRevenge(index, now, out var battle));
            Assert.AreEqual(tickets - 1, s.ArenaTickets(now), "an arena revenge takes a ticket");
            Assert.AreEqual(attacks[1].Opponent.Name, s.State.PvpBattle.Opponent.Name);
            battle.Brain = Brains.Careful();
            s.FinishPvp(battle, now, out _);
            Assert.IsTrue(attacks[1].Avenged);
            Assert.AreEqual(Fail.NotFound, s.StartRevenge(index, now, out _), "once");
            var mine = s.State.PvpJournal.Last();
            Assert.IsTrue(mine.Revenge && !mine.Attacked && !mine.Raid);
            Assert.AreEqual(list, s.ArenaOpponents(now).Select(o => o.Seed).ToList(), "a revenge keeps the list of opponents");
            Assert.IsFalse(s.CanAvenge(attacks[0], attacks[0].Time + 25 * Hour), "a day to avenge");
        }

        [Test]
        public void ARaidOnTheFortressIsAvengedFromTheJournalForFree()
        {
            var s = Fortress(17);
            long t = TestData.Start;
            s.Advance(t);
            s.Advance(t + Day);
            int index = s.State.PvpJournal.FindIndex(r => r.Attacked && r.Raid);
            Assert.GreaterOrEqual(index, 0);
            int flags = s.RaidFlags(t + Day);
            Assert.AreEqual(Fail.None, s.StartRevenge(index, t + Day, out var battle));
            Assert.IsTrue(s.State.PvpBattle.Raid && s.State.PvpBattle.Revenge);
            s.FinishPvp(battle, t + Day, out _);
            Assert.AreEqual(flags, s.RaidFlags(t + Day));
            Assert.AreEqual(0, s.State.Revenge.Count);
            Assert.IsFalse(s.CanAvenge(s.State.PvpJournal[index], t + Day));
        }

        [Test]
        public void TheWeekAndTheGloryShopTellWhenTheyEnd()
        {
            var s = Fortress(18);
            long sunday = 3 * Day + 22 * Hour + 70 * 7 * Day;
            Assert.AreEqual(sunday, s.ArenaWeekEnds(sunday - 1));
            Assert.AreEqual(sunday + 7 * Day, s.ArenaWeekEnds(sunday));
            s.State.UtcOffsetSeconds = 3 * 3600;
            Assert.AreEqual(sunday - 3 * Hour, s.ArenaWeekEnds(sunday - 3 * Hour - 1));

            long t = TestData.Start;
            s.State.Wallet[ResourceType.Glory] = 1000;
            Assert.AreEqual(Fail.None, s.BuyGlory(0, t));
            long reset = s.GloryShopResets(t);
            Assert.Greater(reset, t);
            Assert.LessOrEqual(reset - t, 7 * Day);
            Assert.AreEqual(2, s.GloryLeft(0, reset - 1));
            Assert.AreEqual(3, s.GloryLeft(0, reset));
        }

        [Test]
        public void TheNextLeagueIsAboveTheRatingUntilTheTop()
        {
            var s = Fortress(18);
            var leagues = s.Balance.Activities.Pvp.Leagues;
            Assert.AreEqual(leagues[1].Name, s.NextLeague(leagues[0].Rating).Name);
            Assert.AreEqual(leagues[2].Name, s.NextLeague(leagues[1].Rating).Name);
            Assert.IsNull(s.NextLeague(leagues.Last().Rating + 500));
        }

        static GameSession Fort(int hearth, int forge)
        {
            var s = Fortress(hearth);
            if (forge > 0)
                s.State.Rooms.Add(new Room { Id = s.State.NextRoomId++, Type = RoomType.Forge, Level = forge, Floor = 0, Cell = 9 });
            s.State.Rooms.RemoveAll(r => r.Type == RoomType.Forge && r.Level != forge);
            return s;
        }

        [Test]
        public void TrapsComeFromTheForgeAndOpenWithTheHearth()
        {
            var none = Fort(23, 0);
            Assert.AreEqual(Fail.Locked, none.SetRaidTrap(0, TrapKind.Snare), "no forge, no traps");
            var s = Fort(20, 6);
            Assert.AreEqual(Fail.None, s.SetRaidTrap(0, TrapKind.Snare));
            Assert.AreEqual(Fail.None, s.SetRaidTrap(1, TrapKind.Embers));
            Assert.AreEqual(Fail.Locked, s.SetRaidTrap(2, TrapKind.IceGrate), "the ice grate opens at 23");
            Assert.AreEqual(Fail.NotFound, s.SetRaidTrap(3, TrapKind.Snare));
            Assert.AreEqual(TrapKind.Embers, s.RaidDefense[1].Trap);
            Assert.Greater(s.TrapHold(TrapKind.Snare), Fort(20, 1).TrapHold(TrapKind.Snare), "a better forge, better traps");
            Assert.Greater(s.RaidHold(0), Fort(20, 6).RaidHold(0));
            Assert.AreEqual(Fail.Locked, Fort(16, 6).SetRaidTrap(0, TrapKind.None), "raids are closed before 17");
        }

        [Test]
        public void DefendersStandTwoALineFiveInAllAndMoveBetweenLines()
        {
            var s = Fort(18, 3);
            var h = s.State.Dwellers.Where(d => d.IsHero).Select(d => d.Id).ToList();
            double empty = s.RaidHold(1);
            Assert.AreEqual(Fail.None, s.SetRaidDefenders(1, new[] { h[0], h[1] }));
            Assert.Greater(s.RaidHold(1), empty + 0.15);
            Assert.AreEqual(Fail.LimitReached, s.SetRaidDefenders(0, new[] { h[2], h[3], h[4] }));
            Assert.AreEqual(Fail.None, s.SetRaidDefenders(0, new[] { h[2], h[3] }));
            Assert.AreEqual(Fail.LimitReached, s.SetRaidDefenders(2, new[] { h[4], h[5] }), "five in the whole fortress");
            Assert.AreEqual(Fail.None, s.SetRaidDefenders(2, new[] { h[4], h[0] }), "h0 moves from the barracks to the Hearth");
            Assert.AreEqual(new[] { h[1] }, s.RaidDefense[1].Defenders.ToArray());
            Assert.AreEqual(Fail.NotAHero, s.SetRaidDefenders(2, new[] { 9999 }));
            Assert.LessOrEqual(s.RaidHold(2), 0.85);
        }

        [Test]
        public void AStrongRaidDefenseLetsGhostsTakeFewerStarsAndPaysTrophies()
        {
            int Stars(bool defended, out long glory)
            {
                var s = Fort(23, 10);
                if (defended)
                {
                    var h = s.Squad().Select(d => d.Id).ToList();
                    s.SetRaidTrap(0, TrapKind.Snare);
                    s.SetRaidTrap(1, TrapKind.Embers);
                    s.SetRaidTrap(2, TrapKind.IceGrate);
                    s.SetRaidDefenders(0, new[] { h[0], h[1] });
                    s.SetRaidDefenders(1, new[] { h[2], h[3] });
                    s.SetRaidDefenders(2, new[] { h[4] });
                }
                long t = TestData.Start;
                s.Advance(t);
                for (int d = 1; d <= 40; d++)
                    s.Advance(t + d * Day);
                var raids = s.State.PvpJournal.Where(r => r.Raid && r.Attacked).ToList();
                Assert.IsTrue(raids.All(r => r.Glory == GameSession.RaidDenyGlory * (GameSession.RaidLines - r.Stars)));
                glory = raids.Sum(r => r.Glory);
                return raids.Sum(r => r.Stars);
            }
            int open = Stars(false, out long openGlory);
            int guarded = Stars(true, out long guardedGlory);
            TestContext.WriteLine("stars taken in 20 raids: open {0}, guarded {1}", open, guarded);
            Assert.Less(guarded, open);
            Assert.Greater(guardedGlory, openGlory);
        }

        [Test]
        public void FortificationsArePaidWithTrophiesOrOutpostStone()
        {
            var s = Fort(19, 3);
            long t = TestData.Start;
            s.State.Wallet[ResourceType.Glory] = 10000;
            Assert.AreEqual(Fail.Locked, s.UpgradeFortification(0, false, t), "from the 20th hearth");
            s.State.HearthLevel = 20;
            double hold = s.RaidHold(2);
            Assert.AreEqual(150, s.FortificationCost(0, false));
            Assert.AreEqual(Fail.None, s.UpgradeFortification(0, false, t));
            Assert.AreEqual(300, s.FortificationCost(0, false));
            Assert.AreEqual(10000 - 150, s.State.Wallet[ResourceType.Glory]);
            Assert.AreEqual(Fail.None, s.UpgradeFortification(1, false, t));
            Assert.Greater(s.RaidHold(2), hold + 0.08, "walls everywhere, watch fires at the Hearth");
            var siege = s.ActiveRun("Siege", t);
            if (siege == null)
            {
                Assert.AreEqual(Fail.NotRunning, s.UpgradeFortification(0, true, t), "stone only while the Siege runs");
            }
            else
            {
                siege.Stone = 100;
                Assert.AreEqual(8, s.FortificationCost(0, true));
                Assert.AreEqual(Fail.None, s.UpgradeFortification(0, true, t));
                Assert.AreEqual(92, siege.Stone);
                s.State.Fortifications[0]--; // back to one level for the rest
            }
            for (int i = 1; i < GameSession.FortificationMax; i++)
                Assert.AreEqual(Fail.None, s.UpgradeFortification(0, false, t));
            Assert.AreEqual(Fail.LimitReached, s.UpgradeFortification(0, false, t));
            s.State.Wallet[ResourceType.Glory] = 0;
            Assert.AreEqual(Fail.NotEnough, s.UpgradeFortification(1, false, t));
        }
    }
}
