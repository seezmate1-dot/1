using System;
using System.Collections.Generic;

namespace SunnyValley.Core.Meta
{
    /// <summary>
    /// The order board (9 cards) and the truck (GDD 6). Orders are fair: they only ask for goods the
    /// player can make right now with what is built and unlocked, within the level's time limit
    /// (see <see cref="ChainSeconds"/>), and never more than the barn holds.
    /// </summary>
    public sealed partial class MetaGame
    {
        static readonly string[] Customers = { "vera", "matvey", "pasha", "lida", "fedot", "nina" };

        /// <summary>
        /// How long it takes from scratch to make one unit of the item with what the player owns now
        /// (the longest step of the chain, inputs made in parallel), or -1 if the player cannot make it.
        /// </summary>
        public long ChainSeconds(string item) => ChainSeconds(item, 0);

        long ChainSeconds(string item, int depth)
        {
            if (depth > 8 || item == null || !Data.Items.TryGetValue(item, out var def) || def.Level > Level)
                return -1;
            if (!Data.Producer.TryGetValue(item, out var p) || p == null)
                return -1;
            switch (p)
            {
                case CropDef c:
                    return State.Farm.Plots.Count > 0 ? c.GrowSeconds : -1;
                case TreeDef t:
                    return TreeCount(t.Id) > 0 ? t.CycleSeconds : -1;
                case AnimalDef a:
                {
                    if (AnimalCount(a.Id) == 0) return -1;
                    long feed = ChainSeconds(a.Feed, depth + 1);
                    return feed < 0 ? -1 : feed + a.Seconds;
                }
                case RecipeDef r:
                {
                    if (r.Level > Level || !HasWorkshop(Data.RecipeWorkshop[r.Id].Id)) return -1;
                    long longest = 0;
                    foreach (var kv in r.Inputs)
                    {
                        long s = ChainSeconds(kv.Key, depth + 1);
                        if (s < 0) return -1;
                        longest = Math.Max(longest, s);
                    }
                    return longest + r.Seconds;
                }
            }
            return -1;
        }

        /// <summary>Goods an order may ask for now (fair: makeable within the level's chain limit; no feed).</summary>
        public List<ItemDef> OrderableItems()
        {
            var r = new List<ItemDef>();
            long limit = DataByLevel(B.Orders.MaxChainMinutes) * 60L;
            foreach (var it in B.Items)
            {
                if (it.Kind == ItemKinds.Feed) continue;
                if (it.Kind == ItemKinds.Match3)
                {
                    if (BarnCount(it.Id) > 0) r.Add(it);
                    continue;
                }
                long s = ChainSeconds(it.Id);
                if (s >= 0 && s <= limit) r.Add(it);
            }
            return r;
        }

        int DataByLevel(List<int> list) => MetaData.ByLevel(list, Level);

        /// <summary>The board, with new orders revealed on cards whose wait is over.</summary>
        public IReadOnlyList<OrderCard> Board(long now)
        {
            RefreshOrders(now);
            return State.Orders.Board;
        }

        void RefreshOrders(long now)
        {
            if (!FarmOpen) return;
            foreach (var card in State.Orders.Board)
                if (card.Order == null && card.AvailableAt <= now)
                {
                    card.Order = GenerateOrder(now);
                    if (card.Order != null)
                        Emit(MetaEventKind.OrderAppeared, card.Order.Uid.ToString(), 0, null, null);
                }
            RefreshTruck(now);
        }

        int nextOrderUid => (int)StatValue("orders_generated") + 1;

        Order GenerateOrder(long now)
        {
            var pool = OrderableItems();
            if (pool.Count == 0) return null;
            // Prefer goods not already asked by another card, and goods the player has in the barn.
            var onBoard = new HashSet<string>();
            foreach (var c in State.Orders.Board)
                if (c.Order != null)
                    foreach (var k in c.Order.Items.Keys) onBoard.Add(k);

            int kinds = 1 + Roll(Math.Max(1, DataByLevel(B.Orders.MaxKinds)));
            kinds = Math.Min(kinds, pool.Count);
            int maxUnits = Math.Max(1, DataByLevel(B.Orders.MaxUnits));
            double budget = Math.Max(1, DataByLevel(B.Orders.MaxWorkMinutes));
            int barnLimit = Math.Max(1, BarnCapacity / 3);
            var o = new Order { Uid = nextOrderUid, CreatedAt = now, From = Customers[Roll(Customers.Length)] };
            Stat("orders_generated");
            int totalUnits = 0;
            for (int k = 0; k < kinds && pool.Count > 0; k++)
            {
                var w = new List<int>(pool.Count);
                foreach (var it in pool)
                    w.Add(10 + (BarnCount(it.Id) > 0 ? 6 : 0) - (onBoard.Contains(it.Id) ? 7 : 0) + (it.Level >= Level - 2 ? 4 : 0));
                int idx = MetaRng.Weighted(ref State.Rng, w);
                var item = pool[idx];
                pool.RemoveAt(idx);
                int byWork = (int)Math.Floor(budget / kinds / Math.Max(0.5, item.Work));
                int hi = Math.Max(1, Math.Min(maxUnits, byWork));
                if (item.Kind == ItemKinds.Match3) hi = Math.Min(hi, BarnCount(item.Id));
                hi = Math.Min(hi, barnLimit - totalUnits);
                if (hi <= 0) break;
                int units = 1 + Roll(hi);
                if (units > 1 && Roll(2) == 0) units = Math.Max(1, (units + hi) / 2);
                units = Math.Min(units, hi);
                o.Items[item.Id] = units;
                totalUnits += units;
                o.Coins += units * item.OrderCoins;
                o.Xp += units * item.Xp;
            }
            o.Coins = Math.Max(1, o.Coins * B.Orders.CoinPercent / 100);
            o.Xp = Math.Max(1, o.Xp * B.Orders.XpPercent / 100);
            if (Chance(B.ToolDrops.OrderPercent)) Dict.Add(o.Tools, RandomTool(), 1);
            return o;
        }

        public bool CanFulfil(long now, int card)
        {
            RefreshOrders(now);
            if (card < 0 || card >= State.Orders.Board.Count) return false;
            var o = State.Orders.Board[card].Order;
            return o != null && HasItems(o.Items);
        }

        public ActionResult Fulfil(long now, int card)
        {
            if (!FarmOpen) return ActionResult.Fail(MetaError.FarmClosed);
            RefreshOrders(now);
            if (card < 0 || card >= State.Orders.Board.Count) return ActionResult.Fail(MetaError.UnknownId);
            var c = State.Orders.Board[card];
            if (c.Order == null) return ActionResult.Fail(MetaError.NotReady);
            if (!HasItems(c.Order.Items)) return ActionResult.Fail(MetaError.NotEnoughItems);
            TakeItems(c.Order.Items);
            var r = new Reward { Coins = c.Order.Coins, Xp = c.Order.Xp };
            foreach (var kv in c.Order.Tools) r.Tools[kv.Key] = kv.Value;
            c.Order = null;
            c.AvailableAt = now + B.Orders.FulfillCooldownSeconds;
            State.Orders.Fulfilled++;
            Stat("orders");
            Grant(now, r);
            Progress(now, DailyKinds.Orders, 1);
            return ActionResult.Success(r);
        }

        /// <summary>Throws the order away; the card shows a new one after a wait.</summary>
        public ActionResult Discard(long now, int card)
        {
            RefreshOrders(now);
            if (card < 0 || card >= State.Orders.Board.Count) return ActionResult.Fail(MetaError.UnknownId);
            var c = State.Orders.Board[card];
            if (c.Order == null) return ActionResult.Fail(MetaError.NotReady);
            c.Order = null;
            c.AvailableAt = now + B.Orders.DiscardCooldownSeconds;
            Stat("orders_discarded");
            return ActionResult.Success();
        }

        // ------------------------------------------------------------------ truck

        public bool TruckUnlocked => FarmOpen && Level >= B.Truck.Level;
        public TruckState Truck(long now)
        {
            RefreshTruck(now);
            return State.Orders.Truck;
        }

        void RefreshTruck(long now)
        {
            var t = State.Orders.Truck;
            if (!TruckUnlocked) return;
            if (t.AwayUntil > 0)
            {
                if (now < t.AwayUntil) return;
                t.AwayUntil = 0;
                Emit(MetaEventKind.TruckReturned, "truck", t.Trips, null, "Грузовик вернулся за новым грузом");
            }
            if (t.Crates.Count == 0) GenerateTruck(now);
        }

        void GenerateTruck(long now)
        {
            var t = State.Orders.Truck;
            var pool = OrderableItems().FindAll(i => i.Kind != ItemKinds.Match3);
            t.Crates.Clear();
            t.Tools.Clear();
            t.Coins = t.Xp = 0;
            if (pool.Count == 0) return;
            int units = Math.Max(1, DataByLevel(B.Orders.MaxUnits) * B.Truck.UnitsPercent / 100);
            double coins = 0, xp = 0;
            for (int i = 0; i < B.Truck.Crates && pool.Count > 0; i++)
            {
                var it = pool[Roll(pool.Count)];
                pool.Remove(it);
                // Crates of slow goods ask fewer units: about the same work per crate.
                int n = Math.Max(1, Math.Min(units, (int)(DataByLevel(B.Orders.MaxWorkMinutes) / Math.Max(0.5, it.Work))));
                t.Crates.Add(new TruckCrate { Item = it.Id, Count = n });
                coins += n * it.OrderCoins;
                xp += n * it.Xp;
            }
            t.Coins = (int)Math.Round(coins * B.Truck.CoinPercent / 100.0);
            t.Xp = (int)Math.Round(xp * B.Truck.XpPercent / 100.0);
            for (int i = 0; i < B.Truck.Tools; i++) Dict.Add(t.Tools, RandomTool(), 1);
        }

        public ActionResult LoadCrate(long now, int crate)
        {
            if (!TruckUnlocked) return ActionResult.Fail(MetaError.Locked);
            var t = Truck(now);
            if (t.AwayUntil > now) return ActionResult.Fail(MetaError.Busy);
            if (crate < 0 || crate >= t.Crates.Count) return ActionResult.Fail(MetaError.UnknownId);
            var c = t.Crates[crate];
            if (c.Loaded) return ActionResult.Fail(MetaError.AlreadyDone);
            if (BarnCount(c.Item) < c.Count) return ActionResult.Fail(MetaError.NotEnoughItems);
            Dict.Add(State.Barn, c.Item, -c.Count);
            c.Loaded = true;
            return ActionResult.Success();
        }

        /// <summary>All crates loaded → the truck leaves, pays, and comes back later with a new request.</summary>
        public ActionResult SendTruck(long now)
        {
            if (!TruckUnlocked) return ActionResult.Fail(MetaError.Locked);
            var t = Truck(now);
            if (t.AwayUntil > now) return ActionResult.Fail(MetaError.Busy);
            if (t.Crates.Count == 0) return ActionResult.Fail(MetaError.Empty);
            foreach (var c in t.Crates)
                if (!c.Loaded) return ActionResult.Fail(MetaError.NotReady);
            var r = new Reward { Coins = t.Coins, Xp = t.Xp };
            foreach (var kv in t.Tools) r.Tools[kv.Key] = kv.Value;
            t.Crates.Clear();
            t.AwayUntil = now + B.Truck.ReturnSeconds;
            t.Trips++;
            Stat("truck_trips");
            Grant(now, r);
            Emit(MetaEventKind.TruckLeft, "truck", t.Trips, r, "Грузовик уехал в город");
            return ActionResult.Success(r);
        }
    }
}
