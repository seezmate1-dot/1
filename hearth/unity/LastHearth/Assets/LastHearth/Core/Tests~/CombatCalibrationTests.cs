using System;
using System.Collections.Generic;
using System.Linq;
using LastHearth.Core.Combat;
using NUnit.Framework;

namespace LastHearth.Core.Tests
{
    /// <summary>
    /// Tunes the bosses of the Hunt (combat.json) against the squad a free player really has at each
    /// hearth level (ECONOMY.md: five heroes at about 2L, skills at 1 + level / 6, gear of hearth L).
    /// </summary>
    public class CombatCalibrationTests
    {
        static readonly HeroClass[] Order =
        {
            HeroClass.Guardian, HeroClass.Swordsman, HeroClass.Cleaver, HeroClass.Archer, HeroClass.CoalPriest,
            HeroClass.AshMage, HeroClass.Swordsman, HeroClass.Archer, HeroClass.AshMage,
        };

        public static int SquadSize(int hearth) => 5 + new[] { 8, 14, 20, 26 }.Count(g => hearth >= g);

        public static BattleSetup Squad(int rank, int heroLevel, MonsterFamily family, int seed)
        {
            var setup = new BattleSetup { Family = family, Rank = rank, Seed = seed };
            for (int i = 0; i < SquadSize(rank); i++)
            {
                int level = i < 5 ? heroLevel : Math.Max(1, heroLevel - 3);
                int main = 5 + 2 * rank;
                setup.Heroes.Add(new HeroSetup
                {
                    Id = i + 1,
                    Name = Order[i] + " " + (i + 1),
                    Class = Order[i],
                    Level = level,
                    SkillLevel = Math.Min(10, 1 + level / 6),
                    Stats = new[] { main, main, main, main, main },
                    Gear = GameSession.IssuedKit(TestData.Balance().Items, rank), // the forge's kit of hearth L
                });
            }
            return setup;
        }

        public struct Outcome
        {
            public double WinRate;
            public double Stars;
            public double Seconds;
        }

        public static Outcome Play(int rank, int heroLevel, MonsterFamily family, bool careful, int seeds = 12)
        {
            int wins = 0, stars = 0, ticks = 0;
            for (int seed = 1; seed <= seeds; seed++)
            {
                var battle = new Battle(TestData.Combat(), Squad(rank, heroLevel, family, seed))
                {
                    Brain = careful ? Brains.Careful() : Brains.AutoBattle(),
                };
                battle.RunToEnd();
                if (battle.Won)
                {
                    wins++;
                    ticks += battle.Tick;
                }
                stars += battle.Stars;
            }
            return new Outcome
            {
                WinRate = (double)wins / seeds,
                Stars = (double)stars / seeds,
                Seconds = wins == 0 ? 0 : ticks / 10.0 / wins,
            };
        }

        static readonly int[] Ranks = { 3, 6, 10, 15, 20, 25, 30 };

        /// <summary>Prints one battle: LH_FAMILY, LH_RANK, LH_CAREFUL, LH_SEED environment variables.</summary>
        [Test, Explicit]
        public void DebugBattle()
        {
            var family = (MonsterFamily)Enum.Parse(typeof(MonsterFamily), Environment.GetEnvironmentVariable("LH_FAMILY") ?? "Knight");
            int rank = int.Parse(Environment.GetEnvironmentVariable("LH_RANK") ?? "6");
            bool careful = (Environment.GetEnvironmentVariable("LH_CAREFUL") ?? "1") == "1";
            int seed = int.Parse(Environment.GetEnvironmentVariable("LH_SEED") ?? "1");
            var battle = new Battle(TestData.Combat(), Squad(rank, 2 * rank, family, seed))
            {
                Brain = careful ? Brains.Careful() : Brains.AutoBattle(),
            };
            int skills = 0;
            var brain = battle.Brain;
            battle.Brain = b => { var c = brain(b).ToList(); skills += c.Count(x => x.Kind == CommandKind.Skill); return c; };
            battle.RunToEnd();
            foreach (string line in battle.Log)
                TestContext.WriteLine(line);
            TestContext.WriteLine(string.Format("boss hp {0:0}/{1:0} atk {2:0}; skill presses {3}; heroes: {4}", battle.BossHp, battle.BossMaxHp,
                battle.BossAttack, skills, string.Join(", ", battle.Heroes.Select(h => string.Format("{0} {1:0}/{2:0} atk {3:0}", h.Class, h.Hp, h.MaxHp, h.Attack)))));
        }

        [Test]
        public void ACarefulPlayerWinsWithThreeStarsAtEveryRank()
        {
            foreach (MonsterFamily f in Enum.GetValues(typeof(MonsterFamily)))
            {
                foreach (int r in Ranks)
                {
                    var o = Play(r, 2 * r, f, true);
                    Assert.GreaterOrEqual(o.WinRate, 0.9, f + " rank " + r);
                    Assert.GreaterOrEqual(o.Stars, 2.5, f + " rank " + r + " stars");
                    Assert.That(o.Seconds, Is.InRange(30, 75), f + " rank " + r + " seconds");
                }
            }
        }

        [Test]
        public void AutoBattleIsWorseButNotHopeless()
        {
            double careful = 0, auto = 0;
            int n = 0;
            foreach (MonsterFamily f in Enum.GetValues(typeof(MonsterFamily)))
            {
                foreach (int r in Ranks)
                {
                    var a = Play(r, 2 * r, f, false);
                    Assert.GreaterOrEqual(a.WinRate, 0.3, f + " rank " + r);
                    auto += a.Stars;
                    careful += Play(r, 2 * r, f, true).Stars;
                    n++;
                }
            }
            Assert.LessOrEqual(auto / n, careful / n - 0.7, "reading the telegraphs must pay off");
        }

        [Test]
        public void PowerMattersAHalfLevelSquadLoses()
        {
            foreach (MonsterFamily f in Enum.GetValues(typeof(MonsterFamily)))
            {
                foreach (int r in Ranks.Where(r => r >= 10))
                    Assert.LessOrEqual(Play(r, r, f, true).WinRate, 0.2, f + " rank " + r);
            }
        }

        [Test]
        public void TheSameSeedGivesTheSameBattle()
        {
            string Run()
            {
                var b = new Battle(TestData.Combat(), Squad(12, 24, MonsterFamily.Possessed, 7)) { Brain = Brains.AutoBattle() };
                b.RunToEnd();
                return string.Join("|", b.Log) + b.BossHp.ToString("R");
            }
            Assert.AreEqual(Run(), Run());
        }

        [Test]
        public void TelegraphsCanBeAnswered()
        {
            var c = TestData.Combat();
            Assert.Less(c.RetreatTicks, c.Boss.Windup, "a retreat fits inside the wind-up");
            Assert.GreaterOrEqual(c.Skills.WallTicks, c.Boss.Windup, "a wall raised at the warning covers the blow");
            foreach (var cls in c.Classes)
                Assert.LessOrEqual(cls.SkillCharge / 2, c.Boss.FirstTelegraph, cls.Class + " is half charged before the first strike");
        }

        [Test]
        public void Report()
        {
            foreach (MonsterFamily f in Enum.GetValues(typeof(MonsterFamily)))
            {
                foreach (int r in Ranks)
                {
                    var careful = Play(r, 2 * r, f, true);
                    var auto = Play(r, 2 * r, f, false);
                    var weak = Play(r, r, f, true);
                    TestContext.WriteLine(string.Format(
                        "{0,-9} rank {1,2}: careful win {2:P0} stars {3:0.0} {4:0}s | auto win {5:P0} stars {6:0.0} {7:0}s | weak squad win {8:P0}",
                        f, r, careful.WinRate, careful.Stars, careful.Seconds, auto.WinRate, auto.Stars, auto.Seconds, weak.WinRate));
                }
            }
        }
    }
}
