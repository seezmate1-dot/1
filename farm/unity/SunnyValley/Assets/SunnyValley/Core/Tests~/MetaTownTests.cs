using NUnit.Framework;
using SunnyValley.Core.Meta;
using static SunnyValley.Core.Tests.MetaTestKit;

namespace SunnyValley.Core.Tests
{
    public class MetaTownTests
    {
        [Test]
        public void HousesNeedRoomInTheTownAndServicesMakeIt()
        {
            var g = FarmGame();
            g.State.Wallet.Coins = 100000;
            SetLevel(g, 3, T0);
            int cap = g.PopulationCap;
            Assert.That(cap, Is.EqualTo(Data.Balance.Town.BasePopulationCap));
            while (g.Build(T0, "cottage").Ok) { }
            Assert.That(g.CanBuild("cottage"), Is.EqualTo(MetaError.NotEnoughPopulation));
            Assert.That(g.Population, Is.LessThanOrEqualTo(cap));
            Assert.That(g.Build(T0, "cafe").Ok, Is.True);
            Assert.That(g.PopulationCap, Is.EqualTo(cap + Data.Town["cafe"].Capacity));
            Assert.That(g.Build(T0, "cottage").Ok, Is.True);
            Assert.That(g.CanBuild("cafe"), Is.EqualTo(MetaError.LimitReached), "one cafe per town");
        }

        [Test]
        public void BuildingsGiveXpAndCostCoins()
        {
            var g = FarmGame();
            SetLevel(g, 2, T0);
            g.State.Wallet.Coins = 1000;
            long xp = g.Xp;
            var r = g.Build(T0, "bench");
            Assert.That(r.Ok, Is.True);
            Assert.That(g.Xp, Is.EqualTo(xp + Data.Town["bench"].Xp));
            Assert.That(g.Coins, Is.EqualTo(1000 - Data.Town["bench"].Coins));
            Assert.That(g.Beauty, Is.EqualTo(Data.Town["bench"].Beauty));
            Assert.That(g.CanBuild("fountain"), Is.EqualTo(MetaError.Locked));
            g.State.Wallet.Coins = 0;
            Assert.That(g.CanBuild("bench"), Is.EqualTo(MetaError.NotEnoughCoins));
        }

        [Test]
        public void SpecialDecorationsCostStars()
        {
            var g = FarmGame();
            Assert.That(Data.Town["grandma_statue"].Stars, Is.GreaterThan(0));
            Assert.That(g.CanBuild("grandma_statue"), Is.EqualTo(MetaError.NotEnoughStars));
            g.State.Wallet.Stars = 100;
            Assert.That(g.Build(T0, "grandma_statue").Ok, Is.True);
            Assert.That(g.Stars, Is.EqualTo(100 - Data.Town["grandma_statue"].Stars));
            Assert.That(g.CanBuild("grandma_statue"), Is.EqualTo(MetaError.LimitReached));
        }

        [Test]
        public void LotsRunOutAndExpansionsWithSawAndAxeAddMore()
        {
            var g = FarmGame();
            g.State.Wallet.Coins = 1000000;
            SetLevel(g, Data.MaxLevel, T0);
            int lots = g.Lots;
            Assert.That(lots, Is.EqualTo(Data.Balance.Town.BaseLots));
            // Fill every lot with services and houses.
            foreach (var s in Data.Balance.Town.Services) g.Build(T0, s.Id);
            foreach (var h in Data.Balance.Town.Houses) while (g.Build(T0, h.Id).Ok) { }
            Assert.That(g.LotsUsed, Is.EqualTo(lots));
            Assert.That(g.CanBuild("terem"), Is.EqualTo(MetaError.NotEnoughSpace));
            Assert.That(g.CanBuild("bench"), Is.EqualTo(MetaError.None), "decorations stand anywhere");
            var e = g.NextExpansion;
            Assert.That(e.Tools.Keys, Is.EquivalentTo(Tools.Land));
            Assert.That(g.Expand(T0).Error, Is.EqualTo(MetaError.NotEnoughTools));
            g.Grant(T0, new Reward { Tools = new System.Collections.Generic.SortedDictionary<string, int>(e.Tools) });
            Assert.That(g.Expand(T0).Ok, Is.True);
            Assert.That(g.Lots, Is.EqualTo(lots + e.Lots));
            Assert.That(g.ToolCount(Tools.Saw), Is.EqualTo(0));
        }

        [Test]
        public void ExpansionsHaveLevelGates()
        {
            var g = FarmGame();
            var e = g.NextExpansion;
            g.Grant(T0, new Reward { Tools = new System.Collections.Generic.SortedDictionary<string, int>(e.Tools) });
            g.State.Wallet.Coins = 100000;
            Assert.That(g.Expand(T0).Error, Is.EqualTo(MetaError.Locked));
            SetLevel(g, e.Level, T0);
            Assert.That(g.Expand(T0).Ok, Is.True);
        }

        [Test]
        public void LevelUpsPayTheirRewardsAndAnnounceUnlocks()
        {
            var g = FarmGame();
            long coins = g.Coins;
            SetLevel(g, 2, T0);
            var ev = g.TakeEvents().Find(e => e.Kind == MetaEventKind.LevelUp);
            Assert.That(ev.Value, Is.EqualTo(2));
            Assert.That(g.Coins, Is.EqualTo(coins + Data.LevelDef(2).Reward.Coins));
            Assert.That(ev.Text, Does.Contain("Уровень фермы 2"));
            // A big XP gift passes several levels at once, each paid.
            long crystals = g.Crystals;
            SetLevel(g, 6, T0);
            int expect = 0;
            for (int l = 3; l <= 6; l++) expect += Data.LevelDef(l).Reward.Crystals;
            Assert.That(g.Crystals, Is.EqualTo(crystals + expect));
            Assert.That(g.TakeEvents().FindAll(e => e.Kind == MetaEventKind.LevelUp).Count, Is.EqualTo(4));
        }
    }
}
