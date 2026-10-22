using System.Linq;
using NUnit.Framework;
using SunnyValley.Core.Meta;
using static SunnyValley.Core.Tests.MetaTestKit;

namespace SunnyValley.Core.Tests
{
    public class MetaFarmTests
    {
        [Test]
        public void FarmActionsNeedTheChapterDone()
        {
            var g = NewGame();
            Assert.That(g.Plant(T0, 0, "wheat").Error, Is.EqualTo(MetaError.FarmClosed));
            Assert.That(g.BuildWorkshop(T0, "bakery").Error, Is.EqualTo(MetaError.FarmClosed));
            Assert.That(g.UpgradeBarn(T0).Error, Is.EqualTo(MetaError.FarmClosed));
            Assert.That(g.Build(T0, "bench"), Is.Not.Null);
            Assert.That(g.CanBuild("bench"), Is.EqualTo(MetaError.FarmClosed));
            Assert.That(g.Board(T0), Is.Empty);
        }

        [Test]
        public void CropsGrowOnTimeAndYieldTwo()
        {
            var g = FarmGame();
            long coins = g.Coins;
            int wheat = g.BarnCount("wheat");
            Assert.That(g.Plant(T0, 0, "wheat").Ok, Is.True);
            Assert.That(g.Coins, Is.EqualTo(coins - Data.Crops["wheat"].SeedCoins), "seed bought for coins");
            Assert.That(g.Plant(T0, 0, "corn").Error, Is.EqualTo(MetaError.Busy));
            Assert.That(g.Harvest(T0 + 119, 0).Error, Is.EqualTo(MetaError.NotReady));
            var r = g.Harvest(T0 + 120, 0);
            Assert.That(r.Ok, Is.True);
            Assert.That(g.BarnCount("wheat"), Is.EqualTo(wheat + 2));
            Assert.That(g.Plots[0].Crop, Is.Null);
            Assert.That(g.Harvest(T0 + 200, 0).Error, Is.EqualTo(MetaError.Empty));
        }

        [Test]
        public void PlantingFromTheBarnUsesOneUnit()
        {
            var g = FarmGame();
            long coins = g.Coins;
            int corn = g.BarnCount("corn");
            Assert.That(g.Plant(T0, 1, "corn", fromBarn: true).Ok, Is.True);
            Assert.That(g.BarnCount("corn"), Is.EqualTo(corn - 1));
            Assert.That(g.Coins, Is.EqualTo(coins));
            Assert.That(g.Plant(T0, 2, "carrot", fromBarn: true).Error, Is.EqualTo(MetaError.Locked), "carrot opens at level 2");
            SetLevel(g, 2, T0);
            Assert.That(g.Plant(T0, 2, "carrot", fromBarn: true).Error, Is.EqualTo(MetaError.NotEnoughItems));
            Assert.That(g.Plant(T0, 2, "carrot").Ok, Is.True);
        }

        [Test]
        public void EveryCropTakesItsGrowTime()
        {
            var g = FarmGame();
            SetLevel(g, Data.MaxLevel, T0);
            g.State.Wallet.Coins = 100000;
            while (g.Plots.Count < Data.Balance.Crops.Count) Assert.That(g.BuyPlot(T0).Ok, Is.True);
            for (int i = 0; i < Data.Balance.Crops.Count; i++)
            {
                var c = Data.Balance.Crops[i];
                g.Plant(T0, i, c.Item);
                Assert.That(g.Plots[i].ReadyAt - T0, Is.EqualTo(c.GrowSeconds), c.Item);
            }
            // Offline: everything is simply ready when the time has passed.
            var all = g.CollectAll(T0 + 8 * Hour);
            Assert.That(Data.Balance.Crops.All(c => all.Items.ContainsKey(c.Item)));
        }

        [Test]
        public void BarnFullBlocksHarvestUntilYouSellOrUpgrade()
        {
            var g = FarmGame();
            g.Plant(T0, 0, "wheat");
            Give(g, "corn", g.BarnFree - 1);
            Assert.That(g.BarnFree, Is.EqualTo(1));
            Assert.That(g.Harvest(T0 + Hour, 0).Error, Is.EqualTo(MetaError.BarnFull));
            Assert.That(g.Plots[0].Crop, Is.EqualTo("wheat"), "the crop waits on the plot");
            long coins = g.Coins;
            Assert.That(g.Sell(T0, "corn", 5).Ok, Is.True);
            Assert.That(g.Coins, Is.EqualTo(coins + 5 * Data.Items["corn"].Sell));
            Assert.That(g.Harvest(T0 + Hour, 0).Ok, Is.True);
            Assert.That(g.Sell(T0, "corn", 100000).Error, Is.EqualTo(MetaError.NotEnoughItems));
        }

        [Test]
        public void BarnUpgradesCostToolsAndAddCapacity()
        {
            var g = FarmGame();
            int cap = g.BarnCapacity;
            var up = g.NextBarnUpgrade;
            Assert.That(up.Tools.Keys, Is.EquivalentTo(Tools.Barn));
            Assert.That(g.UpgradeBarn(T0).Error, Is.EqualTo(MetaError.NotEnoughTools));
            g.Grant(T0, new Reward { Tools = new System.Collections.Generic.SortedDictionary<string, int>(up.Tools) });
            Assert.That(g.UpgradeBarn(T0).Ok, Is.True);
            Assert.That(g.BarnCapacity, Is.EqualTo(cap + up.Capacity));
            Assert.That(g.ToolCount(Tools.Hammer), Is.EqualTo(0));
            Assert.That(g.NextBarnUpgrade.Tools[Tools.Hammer], Is.GreaterThan(up.Tools[Tools.Hammer]), "each upgrade costs more");
        }

        [Test]
        public void PlotsAreBoughtInOrderWithLevelGates()
        {
            var g = FarmGame();
            g.State.Wallet.Coins = 100000;
            var offers = Data.Balance.Plots.Offers;
            int bought = 0;
            while (g.NextPlotOffer != null && g.NextPlotOffer.Level <= 1)
            {
                Assert.That(g.BuyPlot(T0).Ok, Is.True);
                bought++;
            }
            Assert.That(g.Plots.Count, Is.EqualTo(Data.Balance.Plots.Start + bought));
            Assert.That(g.BuyPlot(T0).Error, Is.EqualTo(MetaError.Locked));
            SetLevel(g, Data.MaxLevel, T0);
            while (g.BuyPlot(T0).Ok) { }
            Assert.That(g.Plots.Count, Is.EqualTo(Data.Balance.Plots.Start + offers.Count));
            Assert.That(g.BuyPlot(T0).Error, Is.EqualTo(MetaError.LimitReached));
        }

        [Test]
        public void TreesBearFruitEveryCycle()
        {
            var g = FarmGame();
            var t = g.State.Farm.Trees[0];
            var def = Data.Trees[t.Type];
            var r = g.HarvestTree(T0, t.Uid);
            Assert.That(r.Ok, Is.True, "the restored trees are ready when the farm opens");
            Assert.That(r.Reward.Items[def.Item], Is.EqualTo(def.Yield));
            Assert.That(g.HarvestTree(T0 + def.CycleSeconds - 1, t.Uid).Error, Is.EqualTo(MetaError.NotReady));
            Assert.That(g.HarvestTree(T0 + def.CycleSeconds, t.Uid).Ok, Is.True);
            Assert.That(g.BuyTree(T0, "pear_tree").Error, Is.EqualTo(MetaError.Locked));
            g.State.Wallet.Coins = 100000;
            int before = g.TreeCount("apple_tree");
            while (g.BuyTree(T0, "apple_tree").Ok) { }
            Assert.That(g.TreeCount("apple_tree"), Is.EqualTo(def.Costs.Count));
            Assert.That(g.TreeCount("apple_tree"), Is.GreaterThan(before));
            Assert.That(g.NextTreePrice("apple_tree"), Is.EqualTo(-1));
        }

        [Test]
        public void AnimalsEatFeedAndProduceOnTime()
        {
            var g = FarmGame();
            var hen = g.State.Farm.Animals[0];
            var def = Data.Animals["chicken"];
            Assert.That(g.CollectAnimal(T0, hen.Uid).Error, Is.EqualTo(MetaError.NeedsFeed));
            g.State.Barn.Remove(def.Feed);
            Assert.That(g.Feed(T0, hen.Uid).Error, Is.EqualTo(MetaError.NeedsFeed));
            Give(g, def.Feed, 2);
            Assert.That(g.Feed(T0, hen.Uid).Ok, Is.True);
            Assert.That(g.Feed(T0, hen.Uid).Error, Is.EqualTo(MetaError.Busy));
            Assert.That(g.BarnCount(def.Feed), Is.EqualTo(1));
            Assert.That(g.CollectAnimal(T0 + def.Seconds - 1, hen.Uid).Error, Is.EqualTo(MetaError.NotReady));
            Assert.That(g.CollectAnimal(T0 + def.Seconds, hen.Uid).Ok, Is.True);
            Assert.That(g.BarnCount("egg"), Is.EqualTo(1));
            Assert.That(hen.Fed, Is.False);
        }

        [Test]
        public void AnimalHousesAndAnimalPurchases()
        {
            var g = FarmGame();
            g.State.Wallet.Coins = 100000;
            Assert.That(g.BuildAnimalHouse(T0, "cowshed").Error, Is.EqualTo(MetaError.Locked));
            Assert.That(g.BuyAnimal(T0, "cow").Error, Is.EqualTo(MetaError.Locked));
            SetLevel(g, 3, T0);
            Assert.That(g.BuyAnimal(T0, "cow").Error, Is.EqualTo(MetaError.NotReady), "needs the cowshed");
            Assert.That(g.BuildAnimalHouse(T0, "cowshed").Ok, Is.True);
            Assert.That(g.BuildAnimalHouse(T0, "cowshed").Error, Is.EqualTo(MetaError.AlreadyDone));
            Assert.That(g.BuyAnimal(T0, "cow").Ok, Is.True);
            while (g.BuyAnimal(T0, "chicken").Ok) { }
            Assert.That(g.AnimalCount("chicken"), Is.EqualTo(Data.AnimalHouses["coop"].Capacity));
            Assert.That(g.BuyAnimal(T0, "chicken").Error, Is.EqualTo(MetaError.LimitReached));
            SetLevel(g, 7, T0);
            Assert.That(g.BuildAnimalHouse(T0, "sheepfold").Error, Is.EqualTo(MetaError.NotEnoughPopulation), "workers needed");
        }

        [Test]
        public void SpeedUpsCostCrystalsByTimeLeft()
        {
            var g = FarmGame();
            SetLevel(g, 6, T0);
            g.Plant(T0, 0, "strawberry");
            int price = g.SpeedUpPrice(Data.Crops["strawberry"].GrowSeconds);
            Assert.That(price, Is.EqualTo((Data.Crops["strawberry"].GrowSeconds + Data.Balance.SpeedUp.SecondsPerCrystal - 1) / Data.Balance.SpeedUp.SecondsPerCrystal));
            int crystals = g.Crystals;
            Assert.That(g.SpeedUpPlot(T0, 0).Ok, Is.True);
            Assert.That(g.Crystals, Is.EqualTo(crystals - price));
            Assert.That(g.Harvest(T0, 0).Ok, Is.True);
            g.State.Wallet.Crystals = 0;
            g.Plant(T0, 0, "strawberry");
            Assert.That(g.SpeedUpPlot(T0, 0).Error, Is.EqualTo(MetaError.NotEnoughCrystals));
            Assert.That(g.SpeedUpPrice(1), Is.EqualTo(Data.Balance.SpeedUp.MinCrystals));
        }

        [Test]
        public void HarvestSometimesFindsTools()
        {
            var g = FarmGame(seed: 7);
            g.State.Wallet.Coins = 1000000;
            int tools = 0;
            for (int i = 0; i < 400; i++)
            {
                long now = T0 + i * 200;
                for (int p = 0; p < g.Plots.Count; p++)
                {
                    g.Harvest(now, p);
                    g.Plant(now, p, "wheat");
                }
                foreach (var kv in g.State.Barn.ToList()) g.Sell(now, kv.Key, kv.Value);
            }
            foreach (var t in Tools.All) tools += g.ToolCount(t);
            double harvests = g.StatValue("harvested") / 2.0;
            double rate = tools / harvests * 100;
            Assert.That(rate, Is.InRange(Data.Balance.ToolDrops.HarvestPercent * 0.6, Data.Balance.ToolDrops.HarvestPercent * 1.5));
            Assert.That(g.TakeEvents().Exists(e => e.Kind == MetaEventKind.ToolFound));
        }
    }
}
