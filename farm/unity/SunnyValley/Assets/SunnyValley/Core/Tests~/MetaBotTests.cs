using System;
using System.Linq;
using NUnit.Framework;
using SunnyValley.Core.Meta;
using static SunnyValley.Core.Tests.MetaTestKit;

namespace SunnyValley.Core.Tests
{
    /// <summary>
    /// The player bot plays the real rules and must follow the economy model (farm/tools/economy.py):
    /// chapter 1 in 3–5 days, farm levels on the model's days within the tolerance, no dead ends,
    /// a barn that never blocks for good.
    /// </summary>
    public class MetaBotTests
    {
        static PacingDef Pacing => Data.Balance.Pacing;

        static MetaPlayerBot Run(int days, ulong seed, double? winRate = null)
        {
            var bot = new MetaPlayerBot(Data, seed, winRate);
            bot.Run(days);
            TestContext.WriteLine(bot.Report());
            return bot;
        }

        /// <summary>Model day of a farm level, or null if the model does not reach it.</summary>
        static double? ModelDay(int level) => Pacing.LevelDays.TryGetValue(level, out double d) ? d : (double?)null;

        static void AssertOnSchedule(MetaPlayerBot bot, int days)
        {
            double tol = Pacing.TolerancePercent / 100.0;
            foreach (var kv in Pacing.LevelDays)
            {
                int level = kv.Key;
                double model = kv.Value;
                if (level == 1 || model > days - 0.5) continue;
                // Days are counted from the farm's opening, where the farm part of the model starts.
                double farmModel = model - Pacing.Chapter1Day;
                double allowed = Math.Max(0.75, tol * farmModel);
                Assert.That(bot.LevelDay.ContainsKey(level), Is.True, $"level {level} (model day {model:F1}) not reached in {days} days\n{bot.Report()}");
                double farmBot = bot.LevelDay[level] - bot.ChapterDoneDay.Value;
                Assert.That(farmBot, Is.EqualTo(farmModel).Within(allowed),
                    $"level {level}: bot day {bot.LevelDay[level]:F1}, model {model:F1}\n{bot.Report()}");
            }
        }

        static void AssertNoDeadEnds(MetaPlayerBot bot)
        {
            Assert.That(bot.CoinsMin, Is.GreaterThanOrEqualTo(0));
            // Every day something moves: stars go into the chapter or XP into the farm.
            for (int d = 1; d < bot.DailyXp.Count; d++)
                Assert.That(bot.DailyXp[d] > bot.DailyXp[d - 1] || bot.DailyStarsSpent[d] > bot.DailyStarsSpent[d - 1], Is.True,
                    $"no progress on day {d}\n{bot.Report()}");
            // The barn may fill up, but never blocks the harvest for long.
            Assert.That(bot.MaxBarnBlockedStreak, Is.LessThanOrEqualTo(2), bot.Report());
        }

        [Test]
        public void SevenDays_Chapter1TakesThreeToFiveDays()
        {
            var bot = Run(7, 1);
            Assert.That(bot.ChapterDoneDay, Is.Not.Null, bot.Report());
            Assert.That(bot.ChapterDoneDay.Value, Is.InRange(Pacing.Chapter1MinDays, Pacing.Chapter1MaxDays), bot.Report());
            Assert.That(bot.ChapterDoneDay.Value, Is.EqualTo(Pacing.Chapter1Day).Within(0.75), "close to the model");
            Assert.That(bot.Game.FarmOpen, Is.True);
            AssertOnSchedule(bot, 7);
            AssertNoDeadEnds(bot);
        }

        [TestCase(1UL)]
        [TestCase(2UL)]
        [TestCase(3UL)]
        public void ThirtyDays_FarmLevelsFollowTheModel(ulong seed)
        {
            var bot = Run(30, seed);
            AssertOnSchedule(bot, 30);
            AssertNoDeadEnds(bot);
            // The level at day 30 is the model's level at day 30, ±1.
            int model = Pacing.LevelDays.Where(kv => kv.Value <= 30).Max(kv => kv.Key);
            Assert.That(bot.Game.Level, Is.InRange(model - 1, model + 1), bot.Report());
            Assert.That(bot.OrdersDone, Is.GreaterThan(100));
            Assert.That(bot.TrucksSent, Is.GreaterThan(5));
            Assert.That(bot.Game.State.BarnUpgrades, Is.GreaterThan(2), "tools keep coming");
            Assert.That(bot.Game.State.Farm.Workshops.Count, Is.GreaterThanOrEqualTo(5), "what opens gets built");
        }

        [Test]
        public void AWeakerPlayerIsSlowerButNeverStuck()
        {
            var bot = Run(30, 4, winRate: 0.6);
            Assert.That(bot.ChapterDoneDay, Is.Not.Null.And.LessThan(7.5), bot.Report());
            AssertNoDeadEnds(bot);
            Assert.That(bot.Game.Level, Is.GreaterThanOrEqualTo(8), bot.Report());
        }

        [Test]
        public void TheRunIsDeterministic()
        {
            var a = new MetaPlayerBot(Data, 11);
            var b = new MetaPlayerBot(Data, 11);
            a.Run(6);
            b.Run(6);
            Assert.That(b.Game.Save(), Is.EqualTo(a.Game.Save()));
        }
    }
}
