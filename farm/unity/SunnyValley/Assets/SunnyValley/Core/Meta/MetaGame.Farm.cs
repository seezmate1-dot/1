using System;
using System.Collections.Generic;

namespace SunnyValley.Core.Meta
{
    /// <summary>The farm (GDD 6): barn, fields, orchard and animals.</summary>
    public sealed partial class MetaGame
    {
        /// <summary>Chapter 1 is over: the restored farm starts working (fields, coop, garden, mill, barn).</summary>
        void OpenFarm(long now)
        {
            var f = State.Farm;
            if (f.Open) return;
            f.Open = true;
            f.OpenedAt = now;
            var start = B.FarmStart;
            for (int i = 0; i < B.Plots.Start; i++) f.Plots.Add(new PlotState());
            foreach (var h in start.AnimalHouses)
                if (!f.AnimalHouses.Contains(h)) f.AnimalHouses.Add(h);
            foreach (var kv in start.Animals)
                for (int i = 0; i < kv.Value; i++)
                    f.Animals.Add(new AnimalState { Uid = f.NextUid++, Type = kv.Key });
            foreach (var kv in start.Trees)
                for (int i = 0; i < kv.Value; i++)
                    f.Trees.Add(new TreeState { Uid = f.NextUid++, Type = kv.Key, ReadyAt = now });
            foreach (var w in start.Workshops)
                f.Workshops.Add(new WorkshopState { Id = w, Slots = Data.Workshops[w].Slots });
            foreach (var kv in start.Items) Dict.Add(State.Barn, kv.Key, kv.Value);
            foreach (var kv in start.Town) Dict.Add(State.Town.Buildings, kv.Key, kv.Value);
            for (int i = 0; i < B.Orders.Slots; i++)
                State.Orders.Board.Add(new OrderCard { AvailableAt = now });
            Emit(MetaEventKind.FarmOpened, "farm", 0, null, "Хозяйство открыто!");
            RefreshOrders(now);
        }

        // ------------------------------------------------------------------ barn

        public int BarnCapacity
        {
            get
            {
                int c = B.Barn.Capacity;
                for (int i = 0; i < State.BarnUpgrades && i < B.Barn.Upgrades.Count; i++)
                    c += B.Barn.Upgrades[i].Capacity;
                return c;
            }
        }

        public int BarnUsed => Dict.Sum(State.Barn);
        public int BarnFree => Math.Max(0, BarnCapacity - BarnUsed);

        /// <summary>The next barn upgrade, or null at the maximum.</summary>
        public BarnUpgradeDef NextBarnUpgrade =>
            State.BarnUpgrades < B.Barn.Upgrades.Count ? B.Barn.Upgrades[State.BarnUpgrades] : null;

        public ActionResult UpgradeBarn(long now)
        {
            if (!FarmOpen) return ActionResult.Fail(MetaError.FarmClosed);
            var up = NextBarnUpgrade;
            if (up == null) return ActionResult.Fail(MetaError.LimitReached);
            if (!HasTools(up.Tools)) return ActionResult.Fail(MetaError.NotEnoughTools);
            TakeTools(up.Tools);
            State.BarnUpgrades++;
            Stat("barn_upgrades");
            return ActionResult.Success();
        }

        /// <summary>Sells goods from the barn for their sell price (always possible: the barn never blocks for good).</summary>
        public ActionResult Sell(long now, string item, int count)
        {
            if (count <= 0 || !Data.Items.TryGetValue(item, out var def)) return ActionResult.Fail(MetaError.UnknownId);
            if (BarnCount(item) < count) return ActionResult.Fail(MetaError.NotEnoughItems);
            Dict.Add(State.Barn, item, -count);
            int coins = def.Sell * count;
            State.Wallet.Coins += coins;
            Stat("coins_earned", coins);
            Stat("sold", count);
            return ActionResult.Success(new Reward { Coins = coins });
        }

        bool HasItems(IDictionary<string, int> items)
        {
            foreach (var kv in items)
                if (BarnCount(kv.Key) < kv.Value) return false;
            return true;
        }

        void TakeItems(IDictionary<string, int> items)
        {
            foreach (var kv in items)
                Dict.Add(State.Barn, kv.Key, -kv.Value);
        }

        bool ItemUnlocked(string item) => Data.Items.TryGetValue(item, out var d) && d.Level <= Level;

        // ------------------------------------------------------------------ fields

        public IReadOnlyList<PlotState> Plots => State.Farm.Plots;

        /// <summary>The next plot for sale, or null (none left or the level is too low — see <see cref="Data"/>).</summary>
        public PlotOfferDef NextPlotOffer
        {
            get
            {
                int bought = State.Farm.Plots.Count - B.Plots.Start;
                return bought >= 0 && bought < B.Plots.Offers.Count ? B.Plots.Offers[bought] : null;
            }
        }

        public ActionResult BuyPlot(long now)
        {
            if (!FarmOpen) return ActionResult.Fail(MetaError.FarmClosed);
            var offer = NextPlotOffer;
            if (offer == null) return ActionResult.Fail(MetaError.LimitReached);
            if (offer.Level > Level) return ActionResult.Fail(MetaError.Locked);
            if (!SpendCoins(offer.Coins)) return ActionResult.Fail(MetaError.NotEnoughCoins);
            State.Farm.Plots.Add(new PlotState());
            return ActionResult.Success();
        }

        /// <summary>Crops the player may plant now (unlocked by farm level).</summary>
        public List<CropDef> AvailableCrops()
        {
            var r = new List<CropDef>();
            foreach (var c in B.Crops)
                if (ItemUnlocked(c.Item)) r.Add(c);
            return r;
        }

        /// <summary>Plants a crop: uses one unit from the barn as seed if there is one, otherwise buys the seed.</summary>
        public ActionResult Plant(long now, int plot, string crop)
        {
            if (!FarmOpen) return ActionResult.Fail(MetaError.FarmClosed);
            if (plot < 0 || plot >= State.Farm.Plots.Count) return ActionResult.Fail(MetaError.UnknownId);
            if (crop == null || !Data.Crops.TryGetValue(crop, out var def)) return ActionResult.Fail(MetaError.UnknownId);
            if (!ItemUnlocked(crop)) return ActionResult.Fail(MetaError.Locked);
            var p = State.Farm.Plots[plot];
            if (p.Crop != null) return ActionResult.Fail(MetaError.Busy);
            if (BarnCount(crop) > 0)
                Dict.Add(State.Barn, crop, -1);
            else if (!SpendCoins(def.SeedCoins))
                return ActionResult.Fail(MetaError.NotEnoughCoins);
            p.Crop = crop;
            p.PlantedAt = now;
            p.ReadyAt = now + def.GrowSeconds;
            Stat("planted");
            return ActionResult.Success();
        }

        public ActionResult Harvest(long now, int plot)
        {
            if (!FarmOpen) return ActionResult.Fail(MetaError.FarmClosed);
            if (plot < 0 || plot >= State.Farm.Plots.Count) return ActionResult.Fail(MetaError.UnknownId);
            var p = State.Farm.Plots[plot];
            if (p.Crop == null) return ActionResult.Fail(MetaError.Empty);
            if (now < p.ReadyAt) return ActionResult.Fail(MetaError.NotReady);
            int n = Data.Crops[p.Crop].Yield;
            if (BarnFree < n) return ActionResult.Fail(MetaError.BarnFull);
            var r = new Reward();
            r.Items[p.Crop] = n;
            Dict.Add(State.Barn, p.Crop, n);
            p.Crop = null;
            p.PlantedAt = p.ReadyAt = 0;
            Stat("harvested", n);
            Progress(now, DailyKinds.Harvest, n);
            MaybeDropTool(B.ToolDrops.HarvestPercent, r);
            return ActionResult.Success(r);
        }

        public ActionResult SpeedUpPlot(long now, int plot)
        {
            if (plot < 0 || plot >= State.Farm.Plots.Count) return ActionResult.Fail(MetaError.UnknownId);
            var p = State.Farm.Plots[plot];
            if (p.Crop == null) return ActionResult.Fail(MetaError.Empty);
            if (now >= p.ReadyAt) return ActionResult.Fail(MetaError.AlreadyDone);
            if (!SpendCrystals(SpeedUpPrice(p.ReadyAt - now))) return ActionResult.Fail(MetaError.NotEnoughCrystals);
            p.ReadyAt = now;
            return ActionResult.Success();
        }

        // ------------------------------------------------------------------ orchard

        public int TreeCount(string type) => State.Farm.Trees.FindAll(t => t.Type == type).Count;

        /// <summary>Price of the next tree of this kind, or -1 if the maximum is reached.</summary>
        public int NextTreePrice(string type)
        {
            if (!Data.Trees.TryGetValue(type, out var def)) return -1;
            int n = TreeCount(type);
            return n < def.Costs.Count ? def.Costs[n] : -1;
        }

        public ActionResult BuyTree(long now, string type)
        {
            if (!FarmOpen) return ActionResult.Fail(MetaError.FarmClosed);
            if (type == null || !Data.Trees.TryGetValue(type, out var def)) return ActionResult.Fail(MetaError.UnknownId);
            if (def.Level > Level) return ActionResult.Fail(MetaError.Locked);
            int price = NextTreePrice(type);
            if (price < 0) return ActionResult.Fail(MetaError.LimitReached);
            if (!SpendCoins(price)) return ActionResult.Fail(MetaError.NotEnoughCoins);
            State.Farm.Trees.Add(new TreeState { Uid = State.Farm.NextUid++, Type = type, ReadyAt = now + def.CycleSeconds });
            return ActionResult.Success();
        }

        public TreeState Tree(int uid) => State.Farm.Trees.Find(t => t.Uid == uid);

        public ActionResult HarvestTree(long now, int uid)
        {
            var t = Tree(uid);
            if (t == null) return ActionResult.Fail(MetaError.UnknownId);
            if (now < t.ReadyAt) return ActionResult.Fail(MetaError.NotReady);
            var def = Data.Trees[t.Type];
            if (BarnFree < def.Yield) return ActionResult.Fail(MetaError.BarnFull);
            Dict.Add(State.Barn, def.Item, def.Yield);
            t.ReadyAt = now + def.CycleSeconds;
            var r = new Reward();
            r.Items[def.Item] = def.Yield;
            Stat("harvested", def.Yield);
            Progress(now, DailyKinds.Harvest, def.Yield);
            MaybeDropTool(B.ToolDrops.HarvestPercent, r);
            return ActionResult.Success(r);
        }

        // ------------------------------------------------------------------ animals

        public bool HasAnimalHouse(string house) => State.Farm.AnimalHouses.Contains(house);

        public ActionResult BuildAnimalHouse(long now, string house)
        {
            if (!FarmOpen) return ActionResult.Fail(MetaError.FarmClosed);
            if (house == null || !Data.AnimalHouses.TryGetValue(house, out var def)) return ActionResult.Fail(MetaError.UnknownId);
            if (HasAnimalHouse(house)) return ActionResult.Fail(MetaError.AlreadyDone);
            if (def.Level > Level) return ActionResult.Fail(MetaError.Locked);
            if (Population < def.Population) return ActionResult.Fail(MetaError.NotEnoughPopulation);
            if (!SpendCoins(def.Coins)) return ActionResult.Fail(MetaError.NotEnoughCoins);
            State.Farm.AnimalHouses.Add(house);
            Stat("buildings");
            return ActionResult.Success();
        }

        public int AnimalCount(string type) => State.Farm.Animals.FindAll(a => a.Type == type).Count;

        /// <summary>Price of the next animal of this kind, or -1 if the house is full / the maximum is reached.</summary>
        public int NextAnimalPrice(string type)
        {
            if (!Data.Animals.TryGetValue(type, out var def)) return -1;
            int n = AnimalCount(type);
            int cap = Math.Min(def.Costs.Count, Data.AnimalHouses[def.House].Capacity);
            return n < cap ? def.Costs[n] : -1;
        }

        public ActionResult BuyAnimal(long now, string type)
        {
            if (!FarmOpen) return ActionResult.Fail(MetaError.FarmClosed);
            if (type == null || !Data.Animals.TryGetValue(type, out var def)) return ActionResult.Fail(MetaError.UnknownId);
            if (def.Level > Level) return ActionResult.Fail(MetaError.Locked);
            if (!HasAnimalHouse(def.House)) return ActionResult.Fail(MetaError.NotReady);
            int price = NextAnimalPrice(type);
            if (price < 0) return ActionResult.Fail(MetaError.LimitReached);
            if (!SpendCoins(price)) return ActionResult.Fail(MetaError.NotEnoughCoins);
            State.Farm.Animals.Add(new AnimalState { Uid = State.Farm.NextUid++, Type = type });
            return ActionResult.Success();
        }

        public AnimalState Animal(int uid) => State.Farm.Animals.Find(a => a.Uid == uid);

        /// <summary>Feeds a hungry animal with one unit of its feed from the barn.</summary>
        public ActionResult Feed(long now, int uid)
        {
            var a = Animal(uid);
            if (a == null) return ActionResult.Fail(MetaError.UnknownId);
            if (a.Fed) return ActionResult.Fail(MetaError.Busy);
            var def = Data.Animals[a.Type];
            if (BarnCount(def.Feed) <= 0) return ActionResult.Fail(MetaError.NeedsFeed);
            Dict.Add(State.Barn, def.Feed, -1);
            a.Fed = true;
            a.ReadyAt = now + def.Seconds;
            Stat("fed");
            Progress(now, DailyKinds.FeedAnimals, 1);
            return ActionResult.Success();
        }

        public ActionResult CollectAnimal(long now, int uid)
        {
            var a = Animal(uid);
            if (a == null) return ActionResult.Fail(MetaError.UnknownId);
            if (!a.Fed) return ActionResult.Fail(MetaError.NeedsFeed);
            if (now < a.ReadyAt) return ActionResult.Fail(MetaError.NotReady);
            if (BarnFree < 1) return ActionResult.Fail(MetaError.BarnFull);
            var def = Data.Animals[a.Type];
            Dict.Add(State.Barn, def.Product, 1);
            a.Fed = false;
            a.ReadyAt = 0;
            var r = new Reward();
            r.Items[def.Product] = 1;
            Stat("harvested");
            Progress(now, DailyKinds.Harvest, 1);
            MaybeDropTool(B.ToolDrops.HarvestPercent, r);
            return ActionResult.Success(r);
        }

        public ActionResult SpeedUpAnimal(long now, int uid)
        {
            var a = Animal(uid);
            if (a == null) return ActionResult.Fail(MetaError.UnknownId);
            if (!a.Fed) return ActionResult.Fail(MetaError.NeedsFeed);
            if (now >= a.ReadyAt) return ActionResult.Fail(MetaError.AlreadyDone);
            if (!SpendCrystals(SpeedUpPrice(a.ReadyAt - now))) return ActionResult.Fail(MetaError.NotEnoughCrystals);
            a.ReadyAt = now;
            return ActionResult.Success();
        }

        /// <summary>
        /// One tap «собрать всё»: harvests every ready plot, tree and animal while the barn has room.
        /// Returns everything collected (also tools found).
        /// </summary>
        public Reward CollectAll(long now)
        {
            var total = new Reward();
            if (!FarmOpen) return total;
            for (int i = 0; i < State.Farm.Plots.Count; i++)
            {
                var p = State.Farm.Plots[i];
                if (p.Crop != null && now >= p.ReadyAt)
                {
                    var r = Harvest(now, i);
                    if (r.Ok) total.Add(r.Reward);
                }
            }
            foreach (var t in State.Farm.Trees.ToArray())
                if (now >= t.ReadyAt)
                {
                    var r = HarvestTree(now, t.Uid);
                    if (r.Ok) total.Add(r.Reward);
                }
            foreach (var a in State.Farm.Animals.ToArray())
                if (a.Fed && now >= a.ReadyAt)
                {
                    var r = CollectAnimal(now, a.Uid);
                    if (r.Ok) total.Add(r.Reward);
                }
            foreach (var w in State.Farm.Workshops.ToArray())
            {
                var r = Collect(now, w.Id);
                if (r.Ok) total.Add(r.Reward);
            }
            return total;
        }
    }
}
