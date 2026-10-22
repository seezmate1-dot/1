using NUnit.Framework;
using SunnyValley.Core.Meta;
using static SunnyValley.Core.Tests.MetaTestKit;

namespace SunnyValley.Core.Tests
{
    public class MetaWorkshopTests
    {
        [Test]
        public void QueueRunsJobsOneAfterAnother()
        {
            var g = FarmGame();
            var feed = Data.Recipes["chicken_feed"];
            Give(g, "wheat", 20);
            Give(g, "corn", 20);
            int wheat = g.BarnCount("wheat");
            Assert.That(g.Enqueue(T0, "mill", "chicken_feed").Ok, Is.True);
            Assert.That(g.BarnCount("wheat"), Is.EqualTo(wheat - feed.Inputs["wheat"]), "inputs are taken at once");
            Assert.That(g.Enqueue(T0 + 60, "mill", "chicken_feed").Ok, Is.True);
            var jobs = g.Workshop("mill").Jobs;
            Assert.That(jobs[0].EndAt, Is.EqualTo(T0 + feed.Seconds));
            Assert.That(jobs[1].StartAt, Is.EqualTo(jobs[0].EndAt), "the second job waits for the first");
            Assert.That(jobs[1].EndAt, Is.EqualTo(T0 + 2 * feed.Seconds));
            Assert.That(g.RunningJob(T0 + 10, "mill"), Is.SameAs(jobs[0]));
            Assert.That(g.QueueEndsAt("mill"), Is.EqualTo(T0 + 2 * feed.Seconds));
        }

        [Test]
        public void QueueRestartsAfterIdleTime()
        {
            var g = FarmGame();
            Give(g, "wheat", 20);
            Give(g, "corn", 20);
            g.Enqueue(T0, "mill", "chicken_feed");
            long later = T0 + 5 * Hour;
            g.Enqueue(later, "mill", "chicken_feed");
            var jobs = g.Workshop("mill").Jobs;
            Assert.That(jobs[1].StartAt, Is.EqualTo(later), "an idle workshop starts the new job right away");
        }

        [Test]
        public void SlotsLimitTheQueueAndCanBeBought()
        {
            var g = FarmGame();
            Give(g, "wheat", 50);
            Give(g, "corn", 50);
            var def = Data.Workshops["mill"];
            for (int i = 0; i < def.Slots; i++) Assert.That(g.Enqueue(T0, "mill", "chicken_feed").Ok, Is.True);
            Assert.That(g.Enqueue(T0, "mill", "chicken_feed").Error, Is.EqualTo(MetaError.QueueFull));
            int crystals = g.Crystals;
            Assert.That(g.NextSlotPrice("mill"), Is.EqualTo(def.SlotCrystals[0]));
            Assert.That(g.BuySlot(T0, "mill").Ok, Is.True);
            Assert.That(g.Crystals, Is.EqualTo(crystals - def.SlotCrystals[0]));
            Assert.That(g.Enqueue(T0, "mill", "chicken_feed").Ok, Is.True);
            g.State.Wallet.Crystals = 1000;
            while (g.BuySlot(T0, "mill").Ok) { }
            Assert.That(g.Workshop("mill").Slots, Is.EqualTo(def.Slots + def.SlotCrystals.Count));
            Assert.That(g.BuySlot(T0, "mill").Error, Is.EqualTo(MetaError.LimitReached));
        }

        [Test]
        public void FinishedGoodsWaitInTheirSlotsUntilCollected()
        {
            var g = FarmGame();
            Give(g, "wheat", 10);
            Give(g, "corn", 10);
            var feed = Data.Recipes["chicken_feed"];
            for (int i = 0; i < 3; i++) g.Enqueue(T0, "mill", "chicken_feed");
            long allDone = T0 + 3 * feed.Seconds;
            Assert.That(g.ReadyJobs(allDone, "mill"), Is.EqualTo(3));
            Assert.That(g.Enqueue(allDone, "mill", "chicken_feed").Error, Is.EqualTo(MetaError.QueueFull), "ready goods still take their slots");
            int before = g.BarnCount("chicken_feed");
            var r = g.Collect(allDone, "mill");
            Assert.That(r.Ok, Is.True);
            Assert.That(g.BarnCount("chicken_feed"), Is.EqualTo(before + 3 * feed.Count));
            Assert.That(g.Collect(allDone, "mill").Error, Is.EqualTo(MetaError.NothingToCollect));
        }

        [Test]
        public void CollectStopsWhenTheBarnIsFull()
        {
            var g = FarmGame();
            Give(g, "wheat", 10);
            Give(g, "corn", 10);
            g.Enqueue(T0, "mill", "chicken_feed");
            g.Enqueue(T0, "mill", "chicken_feed");
            // Exactly room for one batch of 3 feed.
            Give(g, "egg", g.BarnFree - 3);
            var r = g.Collect(T0 + Day, "mill");
            Assert.That(r.Ok, Is.True);
            Assert.That(g.Workshop("mill").Jobs.Count, Is.EqualTo(1), "the second batch stays in its slot");
            Assert.That(g.Collect(T0 + Day, "mill").Error, Is.EqualTo(MetaError.BarnFull));
        }

        [Test]
        public void ProductionCompletesOffline()
        {
            var g = FarmGame();
            Give(g, "wheat", 12);
            SetLevel(g, 2, T0);
            g.Enqueue(T0, "mill", "flour");
            g.Enqueue(T0, "mill", "flour");
            g.Enqueue(T0, "mill", "flour");
            // Closed the game for a night: everything is there in the morning.
            var all = g.CollectAll(T0 + 10 * Hour);
            Assert.That(all.Items["flour"], Is.EqualTo(3));
        }

        [Test]
        public void SpeedUpMovesTheWholeQueue()
        {
            var g = FarmGame();
            Give(g, "wheat", 30);
            Give(g, "corn", 30);
            var feed = Data.Recipes["chicken_feed"];
            g.Enqueue(T0, "mill", "chicken_feed");
            g.Enqueue(T0, "mill", "chicken_feed");
            long now = T0 + 60;
            Assert.That(g.SpeedUpWorkshop(now, "mill").Ok, Is.True);
            var jobs = g.Workshop("mill").Jobs;
            Assert.That(jobs[0].EndAt, Is.EqualTo(now));
            Assert.That(jobs[1].StartAt, Is.EqualTo(now));
            Assert.That(jobs[1].EndAt, Is.EqualTo(now + feed.Seconds));
        }

        [Test]
        public void RecipesNeedTheirLevelTheWorkshopAndInputs()
        {
            var g = FarmGame();
            g.State.Wallet.Coins = 100000;
            Assert.That(g.Enqueue(T0, "mill", "flour").Error, Is.EqualTo(MetaError.Locked));
            Assert.That(g.Enqueue(T0, "mill", "bread").Error, Is.EqualTo(MetaError.UnknownId), "bread is baked in the bakery");
            Assert.That(g.Enqueue(T0, "bakery", "bread").Error, Is.EqualTo(MetaError.NotReady), "not built yet");
            Assert.That(g.BuildWorkshop(T0, "bakery").Error, Is.EqualTo(MetaError.Locked));
            SetLevel(g, 2, T0);
            Assert.That(g.BuildWorkshop(T0, "bakery").Ok, Is.True);
            Assert.That(g.BuildWorkshop(T0, "bakery").Error, Is.EqualTo(MetaError.AlreadyDone));
            Assert.That(g.Enqueue(T0, "bakery", "bread").Error, Is.EqualTo(MetaError.NotEnoughItems));
            Assert.That(g.AvailableRecipes("bakery").ConvertAll(r => r.Id), Is.EqualTo(new[] { "bread" }));
            SetLevel(g, 5, T0);
            Assert.That(g.BuildWorkshop(T0, "dairy").Error, Is.EqualTo(MetaError.NotEnoughPopulation));
        }

        [Test]
        public void TheToolShedMakesBoostersWithoutBarnSpace()
        {
            var g = FarmGame();
            SetLevel(g, 5, T0);
            g.State.Wallet.Coins = 100000;
            g.State.Town.Buildings["cottage"] = 6;   // workers for the shed
            Assert.That(g.BuildWorkshop(T0, "toolshed").Ok, Is.True);
            var rc = Data.Recipes["make_shovel"];
            foreach (var kv in rc.Inputs) Give(g, kv.Key, kv.Value);
            Assert.That(g.Enqueue(T0, "toolshed", "make_shovel").Ok, Is.True);
            Give(g, "egg", g.BarnFree);
            int shovels = g.BoosterCount(Boosters.Shovel);
            var r = g.Collect(T0 + rc.Seconds, "toolshed");
            Assert.That(r.Ok, Is.True);
            Assert.That(g.BoosterCount(Boosters.Shovel), Is.EqualTo(shovels + 1));
            Assert.That(r.Reward.Boosters[Boosters.Shovel], Is.EqualTo(1));
        }
    }
}
