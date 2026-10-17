using System;
using System.Collections.Generic;
using System.Linq;
using LastHearth.Core.Combat;

namespace LastHearth.Core
{
    /// <summary>
    /// GDD 8 «Снаряжение», 8.3: the heroes' items. Six slots, five rarities, a level (the hearth level of the place
    /// an item comes from, or the forge's level), main stats (attack, health) and 0-3 extra stats, sets of 2 and 4
    /// pieces, upgrades to +10 with iron and essence. Items drop from the campaign (and its three-star chest),
    /// the Hunt, the routes and the events, are forged in the Forge on a timer, and an epic piece is sold in the
    /// Glory shop, shown before buying — no lottery for money (GDD 13.1).
    /// An empty slot holds the forge's issued piece: common, of the hearth level, without extra stats. A full
    /// issued kit is the "gear of hearth L" the battles are calibrated on, so every item found is a step above it
    /// and old saves load with the same power. Numbers: balance.json "items" (ECONOMY.md, section 17).
    /// </summary>
    public sealed partial class GameSession
    {
        public static readonly ItemSlot[] Slots =
        {
            ItemSlot.Weapon, ItemSlot.OffHand, ItemSlot.Helmet, ItemSlot.Armor, ItemSlot.Boots, ItemSlot.Amulet,
        };

        /// <summary>How many finds the equipment window remembers.</summary>
        const int NewsKept = 8;

        public GearState Gear => State.Gear ?? (State.Gear = new GearState());

        ItemsData ItemNumbers => Balance.Items;

        // ------------------------------------------------------------------ numbers of an item

        /// <summary>A main stat (attack or health) of an item: the slot's share of the kit of its level × rarity × plus.</summary>
        public static double ItemMain(ItemsData items, GearItem item, ItemStat stat)
        {
            var slot = items.Slot(item.Slot);
            if (slot == null)
                return 0;
            double share = stat == ItemStat.Attack ? slot.Attack : stat == ItemStat.Health ? slot.Health : 0;
            return share * items.Rarity(item.Rarity).Mult * items.KitAt(item.Level) * (1 + item.Plus * items.PlusStep);
        }

        /// <summary>An extra stat as rolled: half to all of the stat's top value, × the rarity's Roll.</summary>
        public static double RollValue(ItemsData items, GearItem item, ItemRoll roll)
        {
            var stat = items.Stat(roll.Stat);
            return stat == null ? 0 : stat.Max * items.Rarity(item.Rarity).Roll * (0.5 + 0.5 * Math.Max(0, Math.Min(1, roll.Q)));
        }

        /// <summary>Everything an item gives, main stats first.</summary>
        public List<StatValueData> ItemStats(GearItem item)
        {
            var list = new List<StatValueData>();
            foreach (var main in new[] { ItemStat.Attack, ItemStat.Health })
            {
                double v = ItemMain(ItemNumbers, item, main);
                if (v > 0)
                    list.Add(new StatValueData { Stat = main, Value = v });
            }
            foreach (var roll in item.Rolls)
            {
                var have = list.Find(x => x.Stat == roll.Stat);
                double v = RollValue(ItemNumbers, item, roll);
                if (have != null)
                    have.Value += v;
                else
                    list.Add(new StatValueData { Stat = roll.Stat, Value = v });
            }
            return list;
        }

        /// <summary>One stat of an item (0 when the item does not have it).</summary>
        public double ItemStatValue(GearItem item, ItemStat stat) => item == null ? 0 : ItemStats(item).Where(x => x.Stat == stat).Sum(x => x.Value);

        /// <summary>
        /// The power number of an item: its main stats plus the extra ones in main-stat terms (an extra stat is
        /// worth more as the kit grows, so it is scaled with the kit of the item's level).
        /// </summary>
        public int ItemPower(GearItem item) => item == null ? 0 : (int)Math.Round(500 * StatsPower(ItemStats(item), item.Level));

        double StatsPower(IEnumerable<StatValueData> stats, int level)
        {
            double main = 0, extra = 0;
            foreach (var s in stats)
            {
                if (s.Stat == ItemStat.Attack || s.Stat == ItemStat.Health)
                    main += s.Value;
                else
                    extra += s.Value * (ItemNumbers.Stat(s.Stat)?.Power ?? 0);
            }
            return main + (1 + ItemNumbers.KitAt(level)) * extra;
        }

        public ItemRarityData ItemRarityInfo(ItemRarity r) => ItemNumbers.Rarity(r);

        public string ItemName(GearItem item)
        {
            string name = ItemKinds.Of(item.Kind).Name;
            return item.Plus > 0 ? name + " +" + item.Plus : name;
        }

        // ------------------------------------------------------------------ what heroes wear

        /// <summary>The forge's issued piece for an empty slot: common, of the hearth level, no extra stats.</summary>
        public GearItem IssuedPiece(HeroClass c, ItemSlot slot)
        {
            var kinds = ItemKinds.For(c, slot);
            return new GearItem
            {
                Id = 0,
                Kind = kinds.Count > 0 ? kinds[0] : ItemKind.Amulet,
                Rarity = ItemRarity.Common,
                Level = Math.Max(1, State.HearthLevel),
                Source = "казённое кузни",
            };
        }

        /// <summary>The item a hero wears in a slot; null when the slot is empty (the issued piece stands in).</summary>
        public GearItem Worn(int heroId, ItemSlot slot) => Gear.Items.Find(i => i.HeroId == heroId && i.HeroId != 0 && i.Slot == slot);

        /// <summary>What fills the slot in battle: the worn item or the issued piece.</summary>
        public GearItem SlotItem(Dweller d, ItemSlot slot) => Worn(d.Id, slot) ?? IssuedPiece(d.HeroClass, slot);

        public List<GearItem> WornBy(int heroId) => Gear.Items.Where(i => i.HeroId == heroId && heroId != 0).ToList();

        /// <summary>The sets a hero wears and how many pieces of each.</summary>
        public List<(ItemSetData set, int count)> HeroSets(int heroId)
        {
            var list = new List<(ItemSetData, int)>();
            foreach (var g in WornBy(heroId).Where(i => !string.IsNullOrEmpty(i.Set)).GroupBy(i => i.Set))
            {
                var set = ItemNumbers.Set(g.Key);
                if (set != null)
                    list.Add((set, g.Count()));
            }
            return list;
        }

        /// <summary>The bonuses of the sets a hero wears (2 and 4 pieces).</summary>
        List<StatValueData> SetBonuses(int heroId)
        {
            var list = new List<StatValueData>();
            foreach (var (set, count) in HeroSets(heroId))
            {
                if (count >= 2)
                    list.AddRange(set.Two);
                if (count >= 4)
                    list.AddRange(set.Four);
            }
            return list;
        }

        /// <summary>What the equipment of a hero adds in battle: the six slots and the sets.</summary>
        public GearBonus HeroGear(Dweller d)
        {
            var gear = new GearBonus();
            if (d == null || !d.IsHero)
                return gear;
            foreach (var slot in Slots)
            {
                foreach (var s in ItemStats(SlotItem(d, slot)))
                    gear.Add(s.Stat, s.Value);
            }
            foreach (var s in SetBonuses(d.Id))
                gear.Add(s.Stat, s.Value);
            return gear;
        }

        /// <summary>The power of a hero's equipment: the six slots and the set bonuses.</summary>
        public int HeroGearPower(Dweller d)
        {
            if (d == null || !d.IsHero)
                return 0;
            int sum = Slots.Sum(slot => ItemPower(SlotItem(d, slot)));
            return sum + (int)Math.Round(500 * StatsPower(SetBonuses(d.Id), State.HearthLevel));
        }

        /// <summary>The forge's issued kit of a level for a hero who has no session (the calibration squads):
        /// common pieces in all six slots, attack and health + the kit of that level.</summary>
        /// <summary>The free player's items over the issued kit at a rank (balance.json "campaignEdge"): the campaign's and
        /// the events' enemies count it, as they count the kennel (1 without data).</summary>
        public double GearEdge(int rank) => KennelExpected(ItemNumbers.CampaignEdge, rank);

        public static GearBonus IssuedKit(ItemsData items, int level)
        {
            var gear = new GearBonus();
            foreach (var s in items.Slots)
            {
                gear.Attack += s.Attack * items.KitAt(level);
                gear.Health += s.Health * items.KitAt(level);
            }
            return gear;
        }

        public bool Fits(GearItem item, HeroClass c) => item != null && ItemKinds.Of(item.Kind).Fits(c);

        // ------------------------------------------------------------------ the bag

        public int BagSize() => ItemNumbers.InventoryBase + ItemNumbers.InventoryPerForge * MaxRoomLevel(RoomType.Forge);

        public List<GearItem> Bag() => Gear.Items.Where(i => i.HeroId == 0).ToList();

        public bool BagFull() => Bag().Count >= BagSize();

        public GearItem FindItem(int id) => id == 0 ? null : Gear.Items.Find(i => i.Id == id);

        /// <summary>Puts an item from the bag on a hero; what the hero wore in that slot goes to the bag.</summary>
        public Fail Equip(int heroId, int itemId)
        {
            var d = FindDweller(heroId);
            if (d == null)
                return Fail.NotFound;
            if (!d.IsHero)
                return Fail.NotAHero;
            var item = FindItem(itemId);
            if (item == null || item.HeroId != 0)
                return Fail.NotFound;
            if (!Fits(item, d.HeroClass))
                return Fail.RulesNotMet;
            var old = Worn(heroId, item.Slot);
            if (old != null)
                old.HeroId = 0;
            item.HeroId = heroId;
            return Fail.None;
        }

        /// <summary>Takes an item off into the bag (the issued piece stands in again).</summary>
        public Fail Unequip(int heroId, ItemSlot slot)
        {
            var item = Worn(heroId, slot);
            if (item == null)
                return Fail.NotFound;
            if (BagFull())
                return Fail.InventoryFull;
            item.HeroId = 0;
            return Fail.None;
        }

        /// <summary>A class change: items that do not fit the new class go to the bag (even past its size).</summary>
        void FitGear(Dweller d)
        {
            foreach (var item in WornBy(d.Id))
            {
                if (!Fits(item, d.HeroClass))
                    item.HeroId = 0;
            }
        }

        /// <summary>Hours of the model income at an item level (the prices of items follow the level).</summary>
        long ModelIncome(int level)
        {
            var table = Balance.Activities.Routes.IncomeByLevel;
            return table.Length == 0 ? 100 : table[Math.Max(0, Math.Min(table.Length - 1, level - 1))];
        }

        public long SellPrice(GearItem item) =>
            item == null || item.Issued ? 0 : Math.Max(1, (long)Math.Round(ModelIncome(item.Level) * ItemRarityInfo(item.Rarity).SellHours * (1 + 0.1 * item.Plus)));

        /// <summary>Sells an item of the bag for silver (it goes through the rewards inbox: the warehouse limit holds).</summary>
        public Fail SellItem(int itemId)
        {
            var item = FindItem(itemId);
            if (item == null || item.HeroId != 0)
                return Fail.NotFound;
            State.Rewards[ResourceType.Silver] += SellPrice(item);
            Gear.Items.Remove(item);
            ClaimRewards();
            return Fail.None;
        }

        public long ScrapOf(GearItem item) => item == null || item.Issued ? 0 : ItemRarityInfo(item.Rarity).Scrap + item.Plus / 2;

        /// <summary>Dismantling is capped per local day (GDD 8: no farming).</summary>
        public int DismantlesLeft(long now)
        {
            long day = LocalDay(now);
            if (Gear.DismantleDay != day)
            {
                Gear.DismantleDay = day;
                Gear.Dismantled = 0;
            }
            return Math.Max(0, ItemNumbers.DismantlePerDay - Gear.Dismantled);
        }

        /// <summary>Breaks an item of the bag into scrap (обломки) for the forge.</summary>
        public Fail Dismantle(int itemId, long now)
        {
            var item = FindItem(itemId);
            if (item == null || item.HeroId != 0)
                return Fail.NotFound;
            if (DismantlesLeft(now) <= 0)
                return Fail.LimitReached;
            Gear.Scrap += ScrapOf(item);
            Gear.Dismantled++;
            Gear.Items.Remove(item);
            return Fail.None;
        }

        // ------------------------------------------------------------------ upgrades +1..+10

        /// <summary>Iron and essence for the next +1; null at +10 (or for an issued piece).</summary>
        public ResourceBag UpgradeCost(GearItem item)
        {
            var n = ItemNumbers;
            if (item == null || item.Issued || item.Plus >= n.PlusMax || item.Plus >= n.PlusIronHours.Length || item.Plus >= n.PlusEssenceDays.Length)
                return null;
            var cost = new ResourceBag();
            cost[ResourceType.Iron] = Math.Max(1, (long)Math.Round(ModelIncome(item.Level) * n.PlusIronHours[item.Plus]));
            var essence = Balance.Activities.EssenceByLevel;
            long perDay = essence.Length == 0 ? 100 : essence[Math.Max(0, Math.Min(essence.Length - 1, item.Level - 1))];
            cost[ResourceType.Essence] = Math.Max(1, (long)Math.Round(perDay * n.PlusEssenceDays[item.Plus]));
            return cost;
        }

        /// <summary>GDD 8: an item worn or in the bag gets +1 (iron and essence, in the forge).</summary>
        public Fail UpgradeItem(int itemId)
        {
            if (ForgeLevel() < 1)
                return Fail.Locked;
            var item = FindItem(itemId);
            if (item == null)
                return Fail.NotFound;
            var cost = UpgradeCost(item);
            if (cost == null)
                return Fail.LimitReached;
            if (!State.Wallet.Covers(cost))
                return Fail.NotEnough;
            State.Wallet.Subtract(cost);
            item.Plus++;
            return Fail.None;
        }

        // ------------------------------------------------------------------ the forge (ForgeLevel(): GameSession.Pvp.cs)

        /// <summary>The level of the items the forge makes: its own level, never above the hearth.</summary>
        public int CraftLevel => Math.Max(1, Math.Min(ForgeLevel(), State.HearthLevel));

        /// <summary>None when the forge makes this rarity; legendary items need the hearth of ACTIVITIES.md, 3.</summary>
        public Fail CraftOpen(ItemRarity rarity)
        {
            if (ForgeLevel() < 1)
                return Fail.Locked;
            if (ForgeLevel() < ItemRarityInfo(rarity).ForgeLevel)
                return Fail.Locked;
            if (rarity == ItemRarity.Legendary && State.HearthLevel < Balance.Activities.Legendary.FromHearth)
                return Fail.Locked;
            return Fail.None;
        }

        /// <summary>Materials of the model income at the forge's level; a legendary item costs Cores instead.</summary>
        public ResourceBag CraftCost(ItemRarity rarity)
        {
            var r = ItemRarityInfo(rarity);
            var cost = ResourceBag.Materials((long)Math.Round(ModelIncome(CraftLevel) * r.CraftHours), Balance.SilverShare);
            if (rarity == ItemRarity.Legendary)
                cost[ResourceType.Cores] += Balance.Activities.Legendary.Cores;
            return cost;
        }

        public long CraftScrap(ItemRarity rarity) => ItemRarityInfo(rarity).CraftScrap;

        public long CraftSeconds(ItemRarity rarity) => (long)Math.Round(ItemRarityInfo(rarity).CraftMinutes * 60 / (1 + SpecSum(SpecEffect.CraftSpeed)));

        /// <summary>
        /// Starts forging: the player picks the kind and the rarity (and the set of a legendary item); the extra
        /// stats roll when the item is done. One item at a time, like the workshop of Hustle Castle.
        /// </summary>
        public Fail StartCraft(ItemKind kind, ItemRarity rarity, long now, string set = "")
        {
            Advance(now);
            var open = CraftOpen(rarity);
            if (open != Fail.None)
                return open;
            if (Gear.Craft != null)
                return Fail.AlreadyQueued;
            if (rarity == ItemRarity.Legendary && ItemNumbers.Set(set) == null)
                set = ItemNumbers.Sets.Count > 0 ? ItemNumbers.Sets[(int)kind % ItemNumbers.Sets.Count].Id : "";
            var cost = CraftCost(rarity);
            long scrap = CraftScrap(rarity);
            if (!State.Wallet.Covers(cost) || Gear.Scrap < scrap)
                return Fail.NotEnough;
            State.Wallet.Subtract(cost);
            Gear.Scrap -= scrap;
            Gear.Craft = new CraftJob
            {
                Kind = kind,
                Rarity = rarity,
                Level = CraftLevel,
                Set = rarity == ItemRarity.Legendary ? set : "",
                Start = now,
                End = now + CraftSeconds(rarity),
                Seed = Gear.NextItemId * 7919 + (int)(now % 100003),
            };
            Advance(now);
            return Fail.None;
        }

        public long CraftRemaining(long now) => Gear.Craft == null ? 0 : Math.Max(0, Gear.Craft.End - now);

        /// <summary>Sparks to finish the forging now (the last minutes are free, like building).</summary>
        public long CraftSpeedupPrice(long now)
        {
            long left = CraftRemaining(now);
            return left <= Balance.Build.FreeFinishSeconds ? 0 : Formulas.SpeedupCost(Balance, left);
        }

        public Fail FinishCraftNow(long now)
        {
            Advance(now);
            if (Gear.Craft == null)
                return Fail.NotFound;
            if (CraftRemaining(now) > 0)
            {
                long price = CraftSpeedupPrice(now);
                if (State.Wallet[ResourceType.Sparks] < price)
                    return Fail.NotEnough;
                State.Wallet[ResourceType.Sparks] -= price;
                Gear.Craft.End = now;
            }
            if (BagFull())
                return Fail.InventoryFull;
            SettleCraft(now);
            return Fail.None;
        }

        /// <summary>Called by Advance: a finished item goes to the bag as soon as there is a place.</summary>
        void SettleCraft(long now)
        {
            var job = State.Gear?.Craft;
            if (job == null || job.End > now || BagFull())
                return;
            Gear.Craft = null;
            var item = MakeItem(job.Kind, job.Rarity, job.Level, new Rng(job.Seed), job.Set);
            Receive(item, "Кузня");
        }

        // ------------------------------------------------------------------ finds

        /// <summary>A new item with its extra stats and set rolled by the generator.</summary>
        GearItem MakeItem(ItemKind kind, ItemRarity rarity, int level, Rng rng, string set)
        {
            var n = ItemNumbers;
            var item = new GearItem { Kind = kind, Rarity = rarity, Level = Math.Max(1, Math.Min(30, level)) };
            var pool = n.Stats.Select(s => s.Stat).ToList();
            int count = Math.Min(pool.Count, ItemRarityInfo(rarity).Secondaries);
            for (int i = 0; i < count; i++)
            {
                int k = Math.Min(pool.Count - 1, (int)(rng.Next() * pool.Count));
                item.Rolls.Add(new ItemRoll { Stat = pool[k], Q = Math.Round(rng.Next(), 3) });
                pool.RemoveAt(k);
            }
            // a chosen set (a legendary item of the forge) holds for epic and legendary items
            if (rarity >= ItemRarity.Epic && n.Set(set) != null)
                item.Set = set;
            else if (rarity == ItemRarity.Legendary)
                item.Set = n.Sets.Count > 0 ? n.Sets[(int)(rng.Next() * n.Sets.Count) % n.Sets.Count].Id : "";
            else if (rarity == ItemRarity.Epic && n.Sets.Count > 0 && rng.Next() < n.EpicSetChance)
                item.Set = n.Sets[(int)(rng.Next() * n.Sets.Count) % n.Sets.Count].Id;
            return item;
        }

        /// <summary>An item made from a seed into the bag (sold on the spot when the bag is full): rewards, tests and
        /// the debug screen. The same seed gives the same extra stats.</summary>
        public GearItem GiveItem(ItemKind kind, ItemRarity rarity, int level, int seed, string set = "", string from = "Подарок") =>
            Receive(MakeItem(kind, rarity, level, new Rng(seed), set), from);

        /// <summary>A kind for one of the squad's heroes: a random hero, a random slot, a kind they wear.</summary>
        ItemKind PickKind(Rng rng)
        {
            var classes = Squad().Select(d => d.HeroClass).ToList();
            if (classes.Count == 0)
                classes = Enum.GetValues(typeof(HeroClass)).Cast<HeroClass>().Where(c => c != HeroClass.None).ToList();
            var c = classes[Math.Min(classes.Count - 1, (int)(rng.Next() * classes.Count))];
            var slot = Slots[Math.Min(Slots.Length - 1, (int)(rng.Next() * Slots.Length))];
            var kinds = ItemKinds.For(c, slot);
            return kinds[Math.Min(kinds.Count - 1, (int)(rng.Next() * kinds.Count))];
        }

        static ItemRarity PickRarity(int[] weights, Rng rng)
        {
            int total = weights.Sum();
            if (total <= 0)
                return ItemRarity.Common;
            double x = rng.Next() * total;
            for (int i = 0; i < weights.Length; i++)
            {
                x -= weights[i];
                if (x < 0)
                    return (ItemRarity)Math.Min(i, (int)ItemRarity.Legendary);
            }
            return ItemRarity.Common;
        }

        /// <summary>
        /// A find of play: the source's chance and rarity weights (balance.json "items.drops"), the item's level is
        /// the level of the place. The generator is its own (seeded by the battle or the route), so drops never
        /// change the battles that follow. Null when nothing dropped.
        /// </summary>
        GearItem Drop(string source, int level, int seed, string from)
        {
            if (!ItemNumbers.Drops.TryGetValue(source, out var d))
                return null;
            var rng = new Rng(seed * 7919 + source.Length * 131 + source[0]);
            if (rng.Next() >= d.Chance)
                return null;
            var rarity = PickRarity(d.Weights, rng);
            return Receive(MakeItem(PickKind(rng), rarity, level, rng, ""), from);
        }

        /// <summary>An item into the bag; with the bag full it is sold on the spot (the silver is not lost).</summary>
        GearItem Receive(GearItem item, string from)
        {
            item.Id = Gear.NextItemId++;
            item.Source = from;
            string what = string.Format("{0} ({1}, ур. {2})", ItemName(item), ItemRarityInfo(item.Rarity).Name, item.Level);
            if (BagFull())
            {
                long price = SellPrice(item);
                State.Rewards[ResourceType.Silver] += price;
                ClaimRewards();
                News(from + ": " + what + " — сумка полна, продано за " + price + " серебра");
                return item;
            }
            Gear.Items.Add(item);
            News(from + ": " + what);
            return item;
        }

        void News(string line)
        {
            Gear.News.Add(line);
            if (Gear.News.Count > NewsKept)
                Gear.News.RemoveRange(0, Gear.News.Count - NewsKept);
        }

        /// <summary>The event's thresholds and the top ranks bring items (GameSession.Events.cs).</summary>
        void EventItem(EventRun run, int threshold)
        {
            if (Array.IndexOf(ItemNumbers.EventThresholds, threshold) >= 0)
                Drop("event", State.HearthLevel, run.Seed * 31 + threshold, EventInfo(run.Id).Name);
        }

        void EventRankItem(EventRun run, int rank)
        {
            if (rank <= ItemNumbers.EventRankItem && run.Points > 0)
                Drop("eventRank", State.HearthLevel, run.Seed * 37 + 1, EventInfo(run.Id).Name + ", место " + rank);
        }

        // ------------------------------------------------------------------ the Glory shop (MODES.md, 4.2)

        /// <summary>
        /// The epic piece of the Glory shop, shown before buying: for the weakest slot of the squad, of the hearth
        /// level, its extra stats fixed by the week and the purchase. Null without heroes.
        /// </summary>
        public GearItem GloryGearPreview(long now)
        {
            var squad = Squad();
            if (squad.Count == 0)
                return null;
            Dweller hero = null;
            var slot = ItemSlot.Weapon;
            int weakest = int.MaxValue;
            foreach (var d in squad)
            {
                foreach (var s in Slots)
                {
                    int p = ItemPower(SlotItem(d, s));
                    if (p < weakest)
                    {
                        weakest = p;
                        hero = d;
                        slot = s;
                    }
                }
            }
            long week = LocalDay(now) / 7;
            int index = Balance.Activities.Pvp.GloryShop.FindIndex(g => g.Goods == "Gear");
            int bought = index >= 0 && State.GloryBought != null && index < State.GloryBought.Length && State.GloryWeek == week ? State.GloryBought[index] : 0;
            var rng = new Rng((int)(week % 100000) * 13 + bought + 5);
            var item = MakeItem(ItemKinds.For(hero.HeroClass, slot)[0], ItemRarity.Epic, State.HearthLevel, rng, "");
            item.Source = "Лавка славы";
            return item;
        }

        // ------------------------------------------------------------------ the developer's menu (GameRunner.Admin.cs, «Предметы»)

        /// <summary>An item of any kind and rarity at the hearth level into the bag (sold on the spot when it is full).</summary>
        public GearItem AdminGiveItem(ItemKind kind, ItemRarity rarity, string set = "") =>
            GiveItem(kind, rarity, State.HearthLevel, Gear.NextItemId * 7919 + (int)kind * 31 + (int)rarity, set, "Админ-меню");

        /// <summary>Every item the heroes wear goes to +10; returns how many were raised.</summary>
        public int AdminMaxWorn()
        {
            int count = 0;
            foreach (var item in Gear.Items.Where(i => i.HeroId != 0 && i.Plus < ItemNumbers.PlusMax))
            {
                item.Plus = ItemNumbers.PlusMax;
                count++;
            }
            return count;
        }

        /// <summary>Fills the bag up to its size with items of every kind and rarity in turn; returns how many came.</summary>
        public int AdminFillBag()
        {
            int count = 0;
            var kinds = ItemKinds.All;
            while (!BagFull())
            {
                int n = Gear.NextItemId;
                AdminGiveItem(kinds[n % kinds.Length].Kind, (ItemRarity)(n % 5));
                count++;
            }
            return count;
        }

        /// <summary>Empties the bag (the worn items stay); returns how many items went.</summary>
        public int AdminClearBag() => Gear.Items.RemoveAll(i => i.HeroId == 0);

        /// <summary>Dismantling is open again today.</summary>
        public void AdminResetDismantles()
        {
            Gear.Dismantled = 0;
        }

        public long AdminAddScrap(long amount)
        {
            Gear.Scrap = Math.Max(0, Gear.Scrap + amount);
            return Gear.Scrap;
        }

        /// <summary>The name of a good of the Glory shop for the screen.</summary>
        public static string GloryGoodsName(string goods)
        {
            switch (goods)
            {
                case "Essence": return "Эссенция";
                case "Hourglass": return "Песочные часы";
                case "Gear": return "Эпическая часть снаряжения";
                default: return "Печать редкого жителя";
            }
        }
    }
}
