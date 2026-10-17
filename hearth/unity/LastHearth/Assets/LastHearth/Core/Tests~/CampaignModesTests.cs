using System;
using System.Collections.Generic;
using System.Linq;
using LastHearth.Core;
using LastHearth.Core.Combat;
using NUnit.Framework;

namespace LastHearth.Core.Tests
{
    /// <summary>MODES.md, 1: the power gate, the new kinds of battles, hard mode, the secrets, hints and story.</summary>
    public class CampaignModesTests
    {
        const long T0 = TestData.Start;
        const long Day = 86400;

        static GameSession Strong(int hearth)
        {
            var s = CampaignTests.Fortress(hearth);
            s.AdminSetPowerGate(false);
            return s;
        }

        /// <summary>Plays the started battle carefully and finishes it.</summary>
        static MissionReward Play(GameSession s, Battle battle)
        {
            battle.Brain = Brains.Careful();
            battle.RunToEnd();
            Assert.AreEqual(Fail.None, s.FinishMission(battle, out var reward));
            return reward;
        }

        static int First(GameSession s, MissionKind kind) => s.Balance.Campaign.Missions.FindIndex(m => m.Kind == kind);

        [Test]
        public void EveryActHasItsKindsOfBattlesAStoryAndASecret()
        {
            var c = TestData.Balance().Campaign;
            for (int act = 1; act <= c.Acts.Count; act++)
            {
                var kinds = c.Missions.Where(m => m.Act == act).Select(m => m.Kind).ToList();
                Assert.AreEqual(19, kinds.Count);
                Assert.AreEqual(11, kinds.Count(k => k == MissionKind.Normal), "act " + act);
                Assert.AreEqual(3, kinds.Count(k => k == MissionKind.Defense));
                Assert.AreEqual(2, kinds.Count(k => k == MissionKind.Escort));
                Assert.AreEqual(2, kinds.Count(k => k == MissionKind.Elite));
                Assert.AreEqual(MissionKind.Boss, kinds[18]);
                Assert.IsNotEmpty(c.Acts[act - 1].Intro);
                Assert.IsNotEmpty(c.Acts[act - 1].Outro);
                var secret = c.Secrets.Single(x => x.Act == act);
                Assert.IsNotEmpty(secret.Hint);
                Assert.IsNotEmpty(secret.Lore);
                Assert.Greater(secret.Essence, 0);
            }
            Assert.AreEqual(6, c.Hard.Slots.Length);
            Assert.AreEqual(1.6, c.Hard.Power, 1e-9);
            Assert.AreEqual(3, c.Hard.AttemptsPerDay);
        }

        [Test]
        public void RecommendedPowerGrowsWithTheRankAndTheFreePlayerIsNotLocked()
        {
            var b = TestData.Balance();
            var probe = CampaignTests.Fortress(1);
            for (int i = 1; i < b.Campaign.Missions.Count; i++)
                Assert.GreaterOrEqual(probe.RecommendedPower(i), probe.RecommendedPower(i - 1));
            Assert.AreEqual(probe.RecommendedPower(18) * 1.6, probe.RecommendedPower(18, true), 10);
            // the free player's squad of the battle's rank (CampaignTests) is never below the lock
            foreach (int rank in b.Campaign.Missions.Select(m => m.Rank).Distinct())
            {
                var s = CampaignTests.Fortress(rank);
                int index = b.Campaign.Missions.FindIndex(m => m.Rank == rank);
                Assert.IsFalse(s.TooWeak(s.RecommendedPower(index)), "rank " + rank + ": " + s.SquadPower() + " of " + s.RecommendedPower(index));
            }
        }

        [Test]
        public void ATooWeakSquadIsLockedFromANewBattleButNotFromAClearedOne()
        {
            var s = CampaignTests.Fortress(TestData.Balance().Campaign.Missions[0].Rank);
            foreach (var d in s.Squad())
                d.HeroLevel = 1;
            Assert.IsTrue(s.TooWeak(s.RecommendedPower(0)));
            Assert.Less(s.PowerShare(s.RecommendedPower(0)), s.LockShare);
            Assert.AreEqual(Fail.TooWeak, s.StartMission(0, T0, out _));
            Assert.AreEqual(-1, s.State.PendingMission, "a locked battle does not start");
            s.State.CampaignStars.Add(1);
            Assert.AreEqual(Fail.None, s.StartMission(0, T0, out var replay), "a cleared battle can be replayed");
            s.FinishMission(replay, out _);
            Assert.AreEqual(Fail.TooWeak, s.StartMission(1, T0, out _));
            s.AdminSetPowerGate(false);
            Assert.IsFalse(s.PowerGateOn);
            Assert.AreEqual(Fail.None, s.StartMission(1, T0, out var open), "the admin opens the gate");
            var reward = Play(s, open);
            if (reward.Stars == 0)
            {
                Assert.IsNotEmpty(reward.Hints, "a defeat says what to improve");
                Assert.IsTrue(reward.Hints.Any(h => h.StartsWith("Сила отряда")), string.Join(" | ", reward.Hints));
                Assert.IsTrue(reward.Hints.Any(h => h.StartsWith("Уровни")));
            }
        }

        [Test]
        public void DefenseIsWonByHoldingThePoint()
        {
            var s = CampaignTests.Fortress(10);
            int i = First(s, MissionKind.Defense);
            var setup = s.MissionSetup(i, 1);
            Assert.AreEqual(BattleObjective.Hold, setup.Objective);
            foreach (var w in setup.Waves)
                w.HpMult *= 100; // enemies that cannot be killed in time: only holding wins
            var battle = new Battle(s.Combat, setup) { Brain = Brains.Careful() };
            battle.RunToEnd();
            Assert.IsTrue(battle.Won, string.Join("\n", battle.Log));
            Assert.AreEqual(battle.HoldTicks, battle.Tick);
            Assert.Greater(battle.BossHp, 0);
            Assert.AreEqual(1, battle.HoldProgress, 1e-9);

            var deadly = s.MissionSetup(i, 1);
            foreach (var w in deadly.Waves)
            {
                w.HpMult *= 100;
                w.AttackMult *= 30;
            }
            var lost = new Battle(s.Combat, deadly) { Brain = Brains.Careful() };
            lost.RunToEnd();
            Assert.IsFalse(lost.Won, "the squad fell before the time");
            Assert.Less(lost.Tick, lost.HoldTicks);
            var hints = s.DefeatHints(lost, s.RecommendedPower(i));
            Assert.IsTrue(hints.Any(h => h.StartsWith("Оборона")), string.Join(" | ", hints));
        }

        [Test]
        public void EscortIsLostWhenTheRefugeesFall()
        {
            var s = CampaignTests.Fortress(10);
            int i = First(s, MissionKind.Escort);
            var setup = s.MissionSetup(i, 1);
            Assert.AreEqual(BattleObjective.Escort, setup.Objective);
            var won = new Battle(s.Combat, setup) { Brain = Brains.Careful() };
            Assert.Greater(won.EscortMaxHp, 0);
            won.RunToEnd();
            Assert.IsTrue(won.Won);
            Assert.Greater(won.EscortHp, 0);

            var slow = s.MissionSetup(i, 1);
            foreach (var w in slow.Waves)
                w.HpMult *= 100;
            var arrived = new Battle(s.Combat, slow) { Brain = Brains.Careful() };
            arrived.RunToEnd();
            Assert.IsTrue(arrived.Won, "the refugees came through while the squad held the enemy");
            Assert.AreEqual(arrived.HoldTicks, arrived.Tick);
            Assert.Greater(arrived.EscortHits, 0, "the enemy goes at the refugees too");

            var deadly = s.MissionSetup(i, 1);
            foreach (var w in deadly.Waves)
            {
                w.HpMult *= 100;
                w.AttackMult *= 2;
            }
            deadly.EscortHp = 0.2; // frail refugees: they fall before the squad
            var lost = new Battle(s.Combat, deadly) { Brain = Brains.Careful() };
            lost.RunToEnd();
            Assert.IsTrue(lost.EscortLost, string.Join("\n", lost.Log.Skip(Math.Max(0, lost.Log.Count - 10))));
            Assert.IsFalse(lost.Won);
            Assert.IsTrue(lost.Heroes.Exists(h => h.Alive));
            Assert.IsTrue(s.DefeatHints(lost, 0).Any(h => h.StartsWith("Беженцы")));
        }

        [Test]
        public void HardModeOpensAfterTheActWithThreeTriesADay()
        {
            var b = TestData.Balance();
            var s = Strong(b.Campaign.Missions[18].Rank + 6);
            Assert.IsFalse(s.HardOpen(1));
            Assert.AreEqual(Fail.Locked, s.StartHardMission(s.HardMissions(1)[0], T0, out _));
            s.AdminSetCampaignProgress(19);
            Assert.IsTrue(s.HardOpen(1));
            Assert.IsFalse(s.HardOpen(2));
            var hard = s.HardMissions(1);
            Assert.AreEqual(6, hard.Count);
            Assert.AreEqual(Fail.Locked, s.StartHardMission(1, T0, out _), "battle 2 has no hard version");
            int index = hard[0];
            var m = s.Mission(index);
            long sparks = s.State.Wallet[ResourceType.Sparks];
            Assert.AreEqual(Fail.None, s.StartHardMission(index, T0, out var battle));
            Assert.Greater(battle.BossMaxHp, new Battle(s.Combat, s.MissionSetup(index, 1)).BossMaxHp * 1.5, "x1.6 enemies");
            var first = Play(s, battle);
            Assert.IsTrue(first.Hard);
            Assert.IsTrue(first.FirstClear);
            Assert.AreEqual((long)Math.Round(m.Essence * 1.5), first.Got[ResourceType.Essence]);
            Assert.AreEqual(5, first.Got[ResourceType.Sparks]);
            Assert.AreEqual(sparks + 5, s.State.Wallet[ResourceType.Sparks]);
            Assert.AreEqual(1, first.Items.Count, "the first hard win always brings gear");
            Assert.GreaterOrEqual(first.Items[0].Rarity, ItemRarity.Rare, "a step of rarity above");
            Assert.Greater(s.HardStars(index), 0);
            Assert.AreEqual(19, s.MissionsCleared, "hard mode does not move the campaign");

            Assert.AreEqual(Fail.None, s.StartHardMission(index, T0, out var again));
            var second = Play(s, again);
            Assert.IsFalse(second.FirstClear);
            Assert.AreEqual((long)Math.Round(m.Essence * 0.5), second.Got[ResourceType.Essence]);
            Assert.AreEqual(Fail.None, s.StartHardMission(hard[5], T0, out var third));
            Play(s, third);
            Assert.AreEqual(0, s.HardAttemptsLeft(T0));
            Assert.AreEqual(Fail.NoTracks, s.StartHardMission(index, T0, out _));
            Assert.AreEqual(3, s.HardAttemptsLeft(T0 + Day), "a new day, new tries");
            Assert.AreEqual(3, s.AdminRefillHard(T0));
        }

        [Test]
        public void HardModeIsLockedForASquadBelowItsPower()
        {
            // the squad of two hearth levels before the act's boss
            var s = CampaignTests.Fortress(TestData.Balance().Campaign.Missions[18].Rank - 2);
            s.AdminSetCampaignProgress(19);
            int index = s.HardMissions(1)[0];
            Assert.Greater(s.RecommendedPower(index, true), s.RecommendedPower(index));
            Assert.AreEqual(Fail.TooWeak, s.StartHardMission(index, T0, out _), "x1.6 is too much for the squad of the act's day");
            Assert.AreEqual(3, s.HardAttemptsLeft(T0), "a locked try is not spent");
            s.AdminSetHardForAll(true);
            Assert.IsTrue(s.HardOpen(10));
        }

        [Test]
        public void TheSecretOfAnActIsFoundByPlayAndBringsAStoryRecord()
        {
            var b = TestData.Balance();
            var secret = b.Campaign.Secrets.Single(x => x.Act == 1);
            var s = Strong(secret.Rank + 3);
            s.State.NextExpeditionId = 1; // no routes yet
            Assert.IsFalse(s.SecretFound(1));
            Assert.AreEqual(Fail.Locked, s.StartSecretMission(1, T0, out _));
            StringAssert.Contains("Сначала пройдите бой", s.SecretHint(1));
            s.AdminSetCampaignProgress(secret.After);
            Assert.IsFalse(s.SecretFound(1), "the act is far enough, but the routes are not walked yet");
            StringAssert.Contains("вылазок: 0/" + secret.Need, s.SecretHint(1));
            s.State.NextExpeditionId = 1 + secret.Need;
            Assert.IsTrue(s.SecretFound(1));
            Assert.IsFalse(s.SecretFound(2));
            Assert.AreEqual(Fail.None, s.StartSecretMission(1, T0, out var battle));
            Assert.AreEqual(CampaignMode.Secret, s.State.CampaignModes.PendingMode);
            var reward = Play(s, battle);
            Assert.IsTrue(reward.FirstClear);
            Assert.AreEqual(secret.Lore, reward.Lore);
            Assert.AreEqual(secret.Essence, reward.Got[ResourceType.Essence]);
            Assert.AreEqual(1, reward.Items.Count);
            Assert.IsTrue(s.SecretCleared(1));
            Assert.AreEqual(secret.After, s.MissionsCleared, "the secret is off the line of 190");
            Assert.AreEqual(Fail.None, s.StartSecretMission(1, T0, out var again));
            var second = Play(s, again);
            Assert.IsNull(second.Lore, "the record comes once");
            Assert.AreEqual(0, second.Got[ResourceType.Essence]);
            s.AdminSetSecretsForAll(true);
            Assert.IsTrue(s.SecretFound(10));
        }

        [Test]
        public void AnUnfinishedSecretOrHardBattleIsFoughtOutAfterALoad()
        {
            var s = Strong(TestData.Balance().Campaign.Missions[18].Rank + 6);
            s.AdminSetCampaignProgress(19);
            Assert.AreEqual(Fail.None, s.StartHardMission(s.HardMissions(1)[1], T0, out _));
            Assert.AreEqual(Fail.HuntInProgress, s.StartMission(19, T0, out _));
            var reopened = new GameSession(s.Balance, SaveSystem.FromJson(SaveSystem.ToJson(s.State))) { Combat = s.Combat };
            Assert.AreEqual(CampaignMode.Hard, reopened.State.CampaignModes.PendingMode);
            var reward = reopened.ResolvePendingMission();
            Assert.IsTrue(reward.Hard);
            Assert.AreEqual(-1, reopened.State.PendingMission);
            Assert.AreEqual(CampaignMode.Normal, reopened.State.CampaignModes.PendingMode);
            Assert.AreEqual(19, reopened.MissionsCleared);
        }

        [Test]
        public void TheActBossTellsTheEndOfTheActStory()
        {
            var s = Strong(TestData.Balance().Campaign.Missions[18].Rank + 3);
            s.AdminSetCampaignProgress(18);
            Assert.AreEqual(1, s.CurrentAct);
            Assert.AreEqual(Fail.None, s.StartMission(18, T0, out var battle));
            var reward = Play(s, battle);
            Assert.AreEqual(s.Act(1).Outro, reward.Outro);
            Assert.IsTrue(s.ActCleared(1));
            Assert.AreEqual(2, s.CurrentAct);
            Assert.AreEqual(19, s.ActStart(2));
            Assert.AreEqual(38, s.ActEnd(2));
        }

        /// <summary>Win rates of every kind of battle for the free player's squad at its rank (careful / auto).</summary>
        [Test, Explicit]
        public void KindSweep()
        {
            var b = TestData.Balance();
            var stats = new Dictionary<string, int[]>();
            for (int i = 0; i < b.Campaign.Missions.Count; i++)
            {
                var m = b.Campaign.Missions[i];
                var s = CampaignTests.Fortress(m.Rank);
                foreach (bool hard in new[] { false, true })
                {
                    string key = m.Kind + (hard ? " hard" : "");
                    if (!stats.ContainsKey(key))
                        stats[key] = new int[5];
                    for (int seed = 1; seed <= 3; seed++)
                    {
                        var setup = s.MissionSetup(i, seed, hard);
                        if (hard)
                        {
                            foreach (var h in setup.Heroes)
                            {
                                h.AttackMult *= s.Balance.Campaign.Hard.Power;
                                h.HpMult *= s.Balance.Campaign.Hard.Power;
                            }
                        }
                        var c = new Battle(s.Combat, setup) { Brain = Brains.Careful() };
                        c.RunToEnd();
                        var a = new Battle(s.Combat, hard ? setup : s.MissionSetup(i, seed)) { Brain = Brains.AutoBattle() };
                        a.RunToEnd();
                        var st = stats[key];
                        st[0]++;
                        st[1] += c.Won ? 1 : 0;
                        st[2] += a.Won ? 1 : 0;
                        st[3] += c.EscortLost ? 1 : 0;
                        st[4] += c.Stars;
                    }
                }
            }
            foreach (var kv in stats)
                TestContext.WriteLine(string.Format("{0}: careful {1}/{0b}, auto {2}, escort lost {3}, stars {4:0.0}".Replace("{0b}", kv.Value[0].ToString()),
                    kv.Key, kv.Value[1], kv.Value[2], kv.Value[3], (double)kv.Value[4] / kv.Value[0]));
        }

        /// <summary>Prints the bot's squad power against the recommended power of its next battle, day by day.</summary>
        [Test, Explicit]
        public void PowerAgainstTheBot()
        {
            var s = TestData.NewSession();
            s.Combat = TestData.Combat();
            var bot = new PlayerBot(s);
            bool gate = Environment.GetEnvironmentVariable("LH_GATE") != "0";
            s.AdminSetPowerGate(gate);
            var ratios = new List<double>();
            int seen = 0;
            bot.PlayDays(TestData.Start, 30, day =>
            {
                // the squad of the day against the reference of the battles cleared today
                for (; seen < s.MissionsCleared; seen++)
                    ratios.Add(s.SquadPower() / s.ReferencePower(s.Mission(seen).Rank));
                if (day == 30)
                    TestContext.WriteLine(string.Format("power / reference at clear: min {0:0.00} avg {1:0.00} max {2:0.00}, missions {3}",
                        ratios.Min(), ratios.Average(), ratios.Max(), s.MissionsCleared));
                int next = Math.Min(s.MissionsCleared, s.Balance.Campaign.Missions.Count - 1);
                int rec = s.RecommendedPower(next);
                TestContext.WriteLine(string.Format("day {0}: hearth {1} missions {2} power {3:0} rec {4} (rank {5}) share {6:0.00}, own hearth {7:0.00}, heroes {8}",
                    day, s.State.HearthLevel, s.MissionsCleared, s.SquadPower(), rec, s.Mission(next).Rank, s.PowerShare(rec),
                    s.SquadPower() / s.ReferencePower(s.State.HearthLevel), string.Join(",", s.Squad().Select(d => d.HeroLevel))));
            });
        }
    }
}
