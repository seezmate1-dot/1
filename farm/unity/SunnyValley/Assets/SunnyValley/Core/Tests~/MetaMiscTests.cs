using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SunnyValley.Core.Meta;
using static SunnyValley.Core.Tests.MetaTestKit;

namespace SunnyValley.Core.Tests
{
    public class MetaMiscTests
    {
        [Test]
        public void PacingInTheBalanceMatchesTheChapterData()
        {
            // The same session model as farm/tools/economy.py: if chapter1.json changes, balance.json must be regenerated.
            var p = Data.Balance.Pacing;
            int stars = Data.Chapter.Tasks.Sum(t => t.Stars);
            double perSession = p.AttemptsPerSession * p.WinRate;
            int sessions = (int)Math.Ceiling(stars / perSession - 1e-9);
            int index = sessions - 1;
            int perDay = p.SessionHours.Count;
            double day = index / perDay + (p.SessionHours[index % perDay] - p.SessionHours[0]) / 24.0
                         + p.AttemptsPerSession * p.LevelMinutes / 1440.0;
            Assert.That(p.Chapter1Day, Is.EqualTo(day).Within(0.01), "run python3 farm/tools/economy.py");
            Assert.That(p.LevelDays[1], Is.EqualTo(p.Chapter1Day).Within(0.01));
            var days = p.LevelDays.Values.ToList();
            Assert.That(days, Is.Ordered);
            Assert.That(p.LevelDays.Count, Is.EqualTo(Data.MaxLevel), "the model reaches every farm level");
        }

        [Test]
        public void RngIsDeterministicAndInRange()
        {
            ulong a = 5, b = 5;
            for (int i = 0; i < 1000; i++)
            {
                Assert.That(MetaRng.Next(ref a), Is.EqualTo(MetaRng.Next(ref b)));
                int r = MetaRng.Range(ref a, 7);
                MetaRng.Range(ref b, 7);
                Assert.That(r, Is.InRange(0, 6));
                Assert.That(MetaRng.Range(ref a, 3, 5), Is.InRange(3, 5));
                MetaRng.Range(ref b, 3, 5);
            }
            Assert.That(MetaRng.Weighted(ref a, new[] { 0, 0 }), Is.EqualTo(-1));
            Assert.That(MetaRng.Weighted(ref a, new[] { 0, 5, 0 }), Is.EqualTo(1));
            Assert.That(MetaRng.Percent(ref a, 0), Is.False);
            Assert.That(MetaRng.Percent(ref a, 100), Is.True);
        }

        [Test]
        public void RewardsAddAndDescribeInRussian()
        {
            var r = new Reward { Coins = 120, Crystals = 1 };
            r.Tools["hammer"] = 1;
            r.Add(new Reward { Coins = 5, Boosters = new SortedDictionary<string, int> { ["Bomb"] = 2 } });
            Assert.That(r.Coins, Is.EqualTo(125));
            string s = r.Describe(Data);
            Assert.That(s, Does.Contain("125 монет"));
            Assert.That(s, Does.Contain("1 кристалл"));
            Assert.That(s, Does.Contain("Молоток ×1"));
            Assert.That(s, Does.Contain("Бочка-бомба ×2"));
            Assert.That(new Reward().IsEmpty, Is.True);
            Assert.That(r.Clone().Describe(Data), Is.EqualTo(s));
        }

        [Test]
        public void GrantedGoodsOverflowIntoCoins()
        {
            var g = FarmGame();
            Give(g, "wheat", g.BarnFree - 2);
            long coins = g.Coins;
            g.Grant(T0, new Reward { Items = new SortedDictionary<string, int> { ["egg"] = 5 } });
            Assert.That(g.BarnCount("egg"), Is.EqualTo(2));
            Assert.That(g.Coins, Is.EqualTo(coins + 3 * Data.Items["egg"].Sell));
        }

        [Test]
        public void ZoneIntroScenesComeWhenTheZoneOpens()
        {
            var g = NewGame();
            g.MarkSceneSeen("arrival");
            g.State.Wallet.Stars = 100;
            foreach (var zone in new[] { "yard", "house" })
                while (g.AvailableTasks().Any(t => t.Zone == zone))
                    g.CompleteTask(T0, g.AvailableTasks().First(t => t.Zone == zone).Id);
            Assert.That(g.OpenZones()[0].Id, Is.EqualTo("garden"));
            Assert.That(g.PendingScene()?.Id, Is.EqualTo("matvey_lesson"));
            g.MarkSceneSeen("matvey_lesson");
            Assert.That(g.PendingScene(), Is.Null);
        }

        [Test]
        public void StatsCountWhatHappened()
        {
            var g = NewGame();
            g.StartLevel(T0, 1);
            g.FinishLevel(T0, new LevelResult { Level = 1, Won = true, Stars = 3 });
            g.CompleteTask(T0, "yard_gate");
            Assert.That(g.StatValue("levels_won"), Is.EqualTo(1));
            Assert.That(g.StatValue("tasks_done"), Is.EqualTo(1));
            Assert.That(g.StatValue("nothing"), Is.EqualTo(0));
        }

        [Test]
        public void ChainTimesFollowWhatIsOwned()
        {
            var g = FarmGame();
            Assert.That(g.ChainSeconds("wheat"), Is.EqualTo(120));
            Assert.That(g.ChainSeconds("egg"), Is.EqualTo(Data.Recipes["chicken_feed"].Seconds + Data.Crops["corn"].GrowSeconds + Data.Animals["chicken"].Seconds),
                "feed: corn (the slower input) then the mill, then the hen");
            Assert.That(g.ChainSeconds("milk"), Is.EqualTo(-1), "no cows yet");
            Assert.That(g.ChainSeconds("bread"), Is.EqualTo(-1), "no bakery yet");
            Assert.That(g.ChainSeconds("tomato"), Is.EqualTo(-1), "board-only goods have no farm chain");
        }
    }
}
