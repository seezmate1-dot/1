using System.Linq;
using NUnit.Framework;
using SunnyValley.Core.Meta;
using static SunnyValley.Core.Tests.MetaTestKit;

namespace SunnyValley.Core.Tests
{
    public class MetaOrdersTests
    {
        /// <summary>Every order on the board is fair for the game's current state.</summary>
        static void AssertFair(MetaGame g, Order o)
        {
            var b = Data.Balance.Orders;
            long chainLimit = MetaData.ByLevel(b.MaxChainMinutes, g.Level) * 60L;
            Assert.That(o.Items.Count, Is.InRange(1, MetaData.ByLevel(b.MaxKinds, g.Level)));
            int units = 0;
            foreach (var kv in o.Items)
            {
                var it = Data.Items[kv.Key];
                Assert.That(it.Kind, Is.Not.EqualTo(ItemKinds.Feed), "orders never ask for feed");
                Assert.That(it.Level, Is.LessThanOrEqualTo(g.Level), kv.Key + " is not unlocked");
                Assert.That(kv.Value, Is.InRange(1, MetaData.ByLevel(b.MaxUnits, g.Level)));
                if (it.Kind == ItemKinds.Match3)
                    Assert.That(kv.Value, Is.LessThanOrEqualTo(g.BarnCount(kv.Key)), "board crops only as many as the barn has");
                else
                {
                    long s = g.ChainSeconds(kv.Key);
                    Assert.That(s, Is.GreaterThan(0), kv.Key + " cannot be made with what the player owns");
                    Assert.That(s, Is.LessThanOrEqualTo(chainLimit), kv.Key + " takes too long");
                }
                units += kv.Value;
            }
            Assert.That(units, Is.LessThanOrEqualTo(g.BarnCapacity / 3));
            Assert.That(o.Coins, Is.GreaterThan(0));
            Assert.That(o.Xp, Is.GreaterThan(0));
        }

        [Test]
        public void TheFirstBoardIsFairAndDiverse()
        {
            var g = FarmGame();
            var board = g.Board(T0);
            Assert.That(board.Count, Is.EqualTo(9));
            foreach (var c in board) AssertFair(g, c.Order);
            var asked = board.SelectMany(c => c.Order.Items.Keys).Distinct().ToList();
            Assert.That(asked.Count, Is.GreaterThanOrEqualTo(3), "a mix of goods");
            // At level 1 only wheat, corn, eggs and apples can be made.
            Assert.That(asked, Is.SubsetOf(new[] { "wheat", "corn", "egg", "apple" }));
        }

        [TestCase(1UL)]
        [TestCase(2UL)]
        [TestCase(3UL)]
        public void OrdersStayFairAtEveryLevelAndOwnership(ulong seed)
        {
            var g = FarmGame(seed);
            long now = T0;
            for (int level = 1; level <= Data.MaxLevel; level++)
            {
                SetLevel(g, level, now);
                // Buy part of what opens (not everything): fairness must follow ownership, not level.
                g.State.Wallet.Coins = 1000000;
                g.State.Town.Buildings["cottage"] = 12;
                g.State.Town.Buildings["cafe"] = 1;
                g.State.Town.Buildings["post"] = 1;
                if (level % 2 == 0)
                    foreach (var w in Data.Balance.Workshops) g.BuildWorkshop(now, w.Id);
                if (level % 3 == 0)
                    foreach (var h in Data.Balance.AnimalHouses) { g.BuildAnimalHouse(now, h.Id); }
                foreach (var a in Data.Balance.Animals) g.BuyAnimal(now, a.Id);
                for (int round = 0; round < 3; round++)
                {
                    now += Hour;
                    var board = g.Board(now);
                    for (int i = 0; i < board.Count; i++)
                    {
                        if (board[i].Order == null) continue;
                        AssertFair(g, board[i].Order);
                        g.Discard(now, i);
                    }
                    now += Data.Balance.Orders.DiscardCooldownSeconds;
                }
            }
        }

        [Test]
        public void OrdersNeverAskForWhatIsNotOwned()
        {
            var g = FarmGame();
            SetLevel(g, 12, T0);   // a lot is unlocked, but nothing new is built
            long now = T0;
            for (int round = 0; round < 30; round++)
            {
                var board = g.Board(now);
                for (int i = 0; i < board.Count; i++)
                    if (board[i].Order != null)
                    {
                        foreach (var item in board[i].Order.Items.Keys)
                            Assert.That(item, Is.AnyOf("wheat", "corn", "carrot", "sunflower", "strawberry", "cotton", "beet", "egg", "apple", "flour"));
                        g.Discard(now, i);
                    }
                now += Data.Balance.Orders.DiscardCooldownSeconds;
            }
        }

        [Test]
        public void FulfillingPaysAndTheCardRefills()
        {
            var g = FarmGame();
            var board = g.Board(T0);
            var o = board[0].Order;
            Assert.That(g.Fulfil(T0, 0).Error, Is.EqualTo(MetaError.NotEnoughItems).Or.EqualTo(MetaError.None));
            g = FarmGame();
            board = g.Board(T0);
            o = board[0].Order;
            foreach (var kv in o.Items) Give(g, kv.Key, kv.Value);
            long coins = g.Coins, xp = g.Xp;
            Assert.That(g.CanFulfil(T0, 0), Is.True);
            var r = g.Fulfil(T0, 0);
            Assert.That(r.Ok, Is.True);
            Assert.That(g.Coins, Is.EqualTo(coins + o.Coins));
            Assert.That(g.Xp, Is.EqualTo(xp + o.Xp));
            Assert.That(board[0].Order, Is.Null);
            Assert.That(g.Fulfil(T0, 0).Error, Is.EqualTo(MetaError.NotReady));
            long refill = T0 + Data.Balance.Orders.FulfillCooldownSeconds;
            Assert.That(g.Board(refill - 1)[0].Order, Is.Null);
            Assert.That(g.Board(refill)[0].Order, Is.Not.Null);
            Assert.That(g.Board(refill)[0].Order.Uid, Is.Not.EqualTo(o.Uid));
        }

        [Test]
        public void DiscardedCardsWaitLonger()
        {
            var g = FarmGame();
            Assert.That(g.Discard(T0, 3).Ok, Is.True);
            Assert.That(g.Board(T0 + Data.Balance.Orders.DiscardCooldownSeconds - 1)[3].Order, Is.Null);
            Assert.That(g.Board(T0 + Data.Balance.Orders.DiscardCooldownSeconds)[3].Order, Is.Not.Null);
            Assert.That(g.Discard(T0, 99).Error, Is.EqualTo(MetaError.UnknownId));
        }

        [Test]
        public void OrderRewardsMatchTheGoodsValue()
        {
            var g = FarmGame();
            foreach (var c in g.Board(T0))
            {
                int coins = c.Order.Items.Sum(kv => Data.Items[kv.Key].OrderCoins * kv.Value);
                int xp = c.Order.Items.Sum(kv => Data.Items[kv.Key].Xp * kv.Value);
                Assert.That(c.Order.Coins, Is.EqualTo(coins * Data.Balance.Orders.CoinPercent / 100));
                Assert.That(c.Order.Xp, Is.EqualTo(xp * Data.Balance.Orders.XpPercent / 100));
            }
        }

        [Test]
        public void OrdersLevelTheFarmUp()
        {
            var g = FarmGame();
            int levelUps = 0;
            long now = T0;
            for (int n = 0; n < 200 && g.Level < 3; n++)
            {
                var board = g.Board(now);
                for (int i = 0; i < board.Count; i++)
                    if (board[i].Order != null)
                    {
                        foreach (var kv in board[i].Order.Items) Give(g, kv.Key, kv.Value);
                        Assert.That(g.Fulfil(now, i).Ok, Is.True);
                    }
                levelUps += g.TakeEvents().Count(e => e.Kind == MetaEventKind.LevelUp);
                now += Hour;
            }
            Assert.That(g.Level, Is.EqualTo(3));
            Assert.That(levelUps, Is.EqualTo(2));
        }

        [Test]
        public void TruckTakesThreeCratesAndComesBack()
        {
            var g = FarmGame();
            Assert.That(g.TruckUnlocked, Is.False);
            Assert.That(g.LoadCrate(T0, 0).Error, Is.EqualTo(MetaError.Locked));
            SetLevel(g, Data.Balance.Truck.Level, T0);
            var t = g.Truck(T0);
            Assert.That(t.Crates.Count, Is.EqualTo(Data.Balance.Truck.Crates));
            Assert.That(g.SendTruck(T0).Error, Is.EqualTo(MetaError.NotReady));
            foreach (var c in t.Crates)
                Assert.That(g.ChainSeconds(c.Item), Is.GreaterThan(0), "crates are fair too");
            for (int i = 0; i < t.Crates.Count; i++)
            {
                Give(g, t.Crates[i].Item, t.Crates[i].Count);
                Assert.That(g.LoadCrate(T0, i).Ok, Is.True);
            }
            Assert.That(g.LoadCrate(T0, 0).Error, Is.EqualTo(MetaError.AlreadyDone));
            long coins = g.Coins;
            int tools = Tools.All.Sum(g.ToolCount);
            int pay = t.Coins;
            var r = g.SendTruck(T0);
            Assert.That(r.Ok, Is.True);
            Assert.That(g.Coins, Is.GreaterThanOrEqualTo(coins + pay));
            Assert.That(Tools.All.Sum(g.ToolCount), Is.EqualTo(tools + Data.Balance.Truck.Tools));
            Assert.That(g.LoadCrate(T0 + 60, 0).Error, Is.EqualTo(MetaError.Busy));
            var back = g.Truck(T0 + Data.Balance.Truck.ReturnSeconds);
            Assert.That(back.AwayUntil, Is.EqualTo(0));
            Assert.That(back.Crates.Count, Is.EqualTo(Data.Balance.Truck.Crates));
            Assert.That(back.Trips, Is.EqualTo(1));
        }
    }
}
