using System;
using System.Collections.Generic;
using System.Linq;
using LastHearth.Core.Surge;
using NUnit.Framework;

namespace LastHearth.Core.Tests
{
    /// <summary>Tunes the Surge: a careful player keeps three stars, nobody at the screen loses some.</summary>
    public class SurgeCalibrationTests
    {
        /// <summary>A typical fortress at hearth L: rooms on 5-7 floors, heroes at 2L, workers with stat 5 + 2L.</summary>
        public static SurgeSetup Typical(int hearth)
        {
            var c = TestData.Combat().Surge;
            int h = TestData.Balance().Grid.HearthFloor;
            int reach = hearth < 10 ? 2 : 3;
            var setup = new SurgeSetup { HearthFloor = h, HearthLevel = hearth, Coal = 200 };
            setup.SpawnFloors.Add(h + reach);
            setup.SpawnFloors.Add(Math.Max(0, h - reach));
            int workers = Math.Min(2 * (hearth + 3), 40);
            for (int i = 0; i < workers; i++)
            {
                int floor = h + (i % (2 * reach + 1)) - reach;
                double stat = 5 + 2 * hearth;
                setup.Defenders.Add(new SurgeDefender { Id = 100 + i, Dps = c.DwellerDps * stat * Math.Pow(c.GearGrowth, hearth - 1), Floor = floor });
            }
            int heroes = 5 + new[] { 8, 14, 20, 26 }.Count(g => hearth >= g);
            for (int i = 0; i < heroes; i++)
            {
                int level = i < 5 ? 2 * hearth : 2 * hearth - 3;
                setup.Defenders.Add(new SurgeDefender
                {
                    Id = i + 1, Hero = true, Floor = h,
                    Dps = c.HeroDps * Math.Pow(c.HeroLevelGrowth, level - 1) * Math.Pow(c.GearGrowth, hearth - 1),
                });
            }
            return setup;
        }

        static double Stars(int hearth, bool careful)
        {
            var d = new SurgeDefense(TestData.Combat().Surge, Typical(hearth))
            {
                Brain = careful ? SurgeDefense.Careful() : SurgeDefense.Passive(),
            };
            d.RunToEnd();
            return d.Stars;
        }

        static readonly int[] Levels = { 2, 4, 6, 10, 15, 20, 25, 30 };

        [Test]
        public void Report()
        {
            foreach (int l in Levels)
                TestContext.WriteLine(string.Format("hearth {0,2}: careful {1} stars, passive {2} stars", l, Stars(l, true), Stars(l, false)));
        }

        [Test, Explicit]
        public void Sweep()
        {
            var c = TestData.Combat().Surge;
            double hp = c.CreatureHp;
            double g0 = c.CreatureGrowth;
            foreach (double g in new[] { 1.21, 1.22, 1.23 })
            {
                foreach (double k in new[] { 2.5, 3.0, 3.5, 4.0 })
                {
                    c.CreatureHp = hp * k;
                    c.CreatureGrowth = g;
                    TestContext.WriteLine("g" + g + " x" + k + ": " + string.Join(" ", Levels.Select(l => l + ":" + Stars(l, true) + "/" + Stars(l, false))));
                }
            }
            c.CreatureHp = hp;
            c.CreatureGrowth = g0;
        }

        [Test]
        public void CarefulKeepsThreeStarsPassiveLosesSome()
        {
            double passive = 0;
            foreach (int l in Levels)
            {
                Assert.AreEqual(3, Stars(l, true), "careful at hearth " + l);
                passive += Stars(l, false);
            }
            passive /= Levels.Length;
            Assert.That(passive, Is.InRange(1.0, 2.5), "nobody at the screen keeps one or two stars");
        }
    }
}
