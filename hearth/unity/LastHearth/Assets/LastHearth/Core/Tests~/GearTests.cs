using System;
using System.Collections.Generic;
using System.Linq;
using LastHearth.Core;
using LastHearth.Core.Combat;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace LastHearth.Core.Tests
{
    /// <summary>GDD 8 «Снаряжение»: items, the bag, the forge, drops, sets, upgrades, and what they do in battle.</summary>
    public class GearTests
    {
        const long T0 = TestData.Start;
        const long Day = 86400;

        static readonly HeroClass[] Classes =
        {
            HeroClass.Guardian, HeroClass.Swordsman, HeroClass.Cleaver, HeroClass.Archer, HeroClass.CoalPriest,
        };

        /// <summary>A fortress at this hearth level with five heroes at 2L and a forge of the same level.</summary>
        static GameSession Fortress(int hearth = 12, int forge = -1)
        {
            var s = TestData.NewSession();
            s.Combat = TestData.Combat();
            s.State.HearthLevel = hearth;
            s.State.Queue.EnsureBuilders(s.BuilderCount());
            s.State.Dwellers.Clear();
            foreach (var c in Classes)
            {
                int stat = 5 + 2 * hearth;
                var d = s.AddDweller(c.ToString(), new[] { stat, stat, stat, stat, stat });
                d.HeroClass = c;
                d.HeroLevel = 2 * hearth;
                d.SkillLevel = 3;
            }
            if (forge != 0)
                s.State.Rooms.Add(new Room { Id = s.State.NextRoomId++, Type = RoomType.Forge, Level = forge < 0 ? hearth : forge, Floor = 0, Cell = 0 });
            return s;
        }

        static Dweller Hero(GameSession s, HeroClass c) => s.State.Dwellers.First(d => d.HeroClass == c);

        static string Json(object o) => JsonConvert.SerializeObject(o);

        // ------------------------------------------------------------------ data and numbers

        [Test]
        public void TheDataHasSixSlotsFiveRaritiesAndAKitForEveryLevel()
        {
            var n = TestData.Balance().Items;
            Assert.AreEqual(6, n.Slots.Count);
            Assert.AreEqual(5, n.Rarities.Count);
            Assert.AreEqual(30, n.Kit.Length);
            Assert.AreEqual(1.0, n.Slots.Sum(x => x.Attack), 1e-9, "the slots make one kit of attack");
            Assert.AreEqual(1.0, n.Slots.Sum(x => x.Health), 1e-9, "and one kit of health");
            for (int i = 1; i < n.Rarities.Count; i++)
            {
                Assert.Greater(n.Rarities[i].Mult, n.Rarities[i - 1].Mult, "rarer items are stronger");
                Assert.GreaterOrEqual(n.Rarities[i].Secondaries, n.Rarities[i - 1].Secondaries);
            }
            Assert.AreEqual(0, n.Rarities[0].Secondaries, "a common item has no extra stats");
            Assert.AreEqual(3, n.Rarities[4].Secondaries, "up to three");
            var c = TestData.Combat();
            for (int L = 1; L <= 30; L++)
            {
                Assert.GreaterOrEqual(1 + n.KitAt(L), Math.Pow(c.GearGrowth, L - 1) - 1e-4, "the issued kit is the gear of the power model, level " + L);
                Assert.LessOrEqual(1 + n.KitAt(L), Math.Pow(c.GearGrowth, L - 1) * 1.05, "and not much more, level " + L);
            }
            foreach (var kind in ItemKinds.All)
                Assert.AreEqual(kind.Kind, ItemKinds.Of(kind.Kind).Kind, "the kinds are listed in enum order");
            foreach (HeroClass hc in Enum.GetValues(typeof(HeroClass)))
            {
                if (hc == HeroClass.None)
                    continue;
                foreach (var slot in GameSession.Slots)
                    Assert.IsNotEmpty(ItemKinds.For(hc, slot), hc + " wears something in " + slot);
            }
        }

        [Test]
        public void AnEmptySlotHoldsTheForgesIssuedPieceOfTheHearth()
        {
            var s = Fortress(12);
            var hero = Hero(s, HeroClass.Archer);
            var gear = s.HeroGear(hero);
            var kit = GameSession.IssuedKit(s.Balance.Items, 12);
            Assert.AreEqual(kit.Attack, gear.Attack, 1e-9);
            Assert.AreEqual(kit.Health, gear.Health, 1e-9);
            Assert.AreEqual(0, gear.Crit + gear.Dodge + gear.Haste + gear.Armor);
            var issued = s.SlotItem(hero, ItemSlot.Weapon);
            Assert.IsTrue(issued.Issued);
            Assert.AreEqual(ItemKind.Bow, issued.Kind);
            Assert.AreEqual(0, s.SellPrice(issued));
            Assert.AreEqual(0, s.ScrapOf(issued));
            s.State.HearthLevel = 13;
            Assert.Greater(s.HeroGear(hero).Attack, gear.Attack, "the issued kit follows the hearth");
        }

        [Test]
        public void TheHeroScreenShowsTheItemsTheBattleUses()
        {
            var s = Fortress(10);
            var hero = Hero(s, HeroClass.Swordsman);
            var plain = s.HeroPreview(hero.Id);
            var sword = s.GiveItem(ItemKind.Sword, ItemRarity.Epic, 10, 3);
            Assert.AreEqual(Fail.None, s.Equip(hero.Id, sword.Id));
            var geared = s.HeroPreview(hero.Id);
            Assert.Greater(geared.Attack, plain.Attack);
            var inBattle = new Battle(s.Combat, s.MissionSetup(0, 1)).Hero(hero.Id);
            Assert.AreEqual(inBattle.Attack, geared.Attack, 1e-6, "the card shows the battle's numbers");
            Assert.AreEqual(inBattle.MaxHp, geared.MaxHp, 1e-6);
            Assert.AreEqual(inBattle.Crit, geared.Crit, 1e-9);
            Assert.AreEqual(inBattle.Interval, geared.Interval);
        }

        [Test]
        public void RollsAreDeterministicBySeedAndFollowTheRarity()
        {
            var s = Fortress(15);
            var n = s.Balance.Items;
            var a = s.GiveItem(ItemKind.Amulet, ItemRarity.Epic, 15, 42);
            var b = s.GiveItem(ItemKind.Amulet, ItemRarity.Epic, 15, 42);
            Assert.AreEqual(Json(a.Rolls), Json(b.Rolls), "the same seed, the same extra stats");
            Assert.AreEqual(a.Set, b.Set);
            Assert.AreNotEqual(a.Id, b.Id);
            var distinct = Enumerable.Range(1, 20).Select(seed => Json(s.GiveItem(ItemKind.Boots, ItemRarity.Rare, 15, seed * 101).Rolls)).Distinct().Count();
            Assert.Greater(distinct, 15, "other seeds roll other stats");
            foreach (ItemRarity r in Enum.GetValues(typeof(ItemRarity)))
            {
                for (int seed = 1; seed <= 15; seed++)
                {
                    var item = s.GiveItem(ItemKind.Boots, r, 15, seed);
                    s.SellItem(item.Id);
                    Assert.AreEqual(n.Rarity(r).Secondaries, item.Rolls.Count, r + " rolls its number of extra stats");
                    Assert.AreEqual(item.Rolls.Count, item.Rolls.Select(x => x.Stat).Distinct().Count(), "no stat twice");
                    foreach (var roll in item.Rolls)
                    {
                        double v = GameSession.RollValue(n, item, roll);
                        double top = n.Stat(roll.Stat).Max * n.Rarity(r).Roll;
                        Assert.That(v, Is.InRange(top * 0.5 - 1e-9, top + 1e-9), r + " " + roll.Stat);
                    }
                }
            }
        }

        [Test]
        public void MainStatsGrowWithLevelRarityAndPlus()
        {
            var s = Fortress(20);
            var n = s.Balance.Items;
            double Atk(ItemRarity r, int level, int plus = 0) =>
                GameSession.ItemMain(n, new GearItem { Kind = ItemKind.Sword, Rarity = r, Level = level, Plus = plus }, ItemStat.Attack);
            Assert.AreEqual(n.Slot(ItemSlot.Weapon).Attack * n.KitAt(10), Atk(ItemRarity.Common, 10), 1e-12);
            Assert.Greater(Atk(ItemRarity.Common, 11), Atk(ItemRarity.Common, 10));
            Assert.Greater(Atk(ItemRarity.Rare, 10), Atk(ItemRarity.Uncommon, 10));
            Assert.Greater(Atk(ItemRarity.Legendary, 10), Atk(ItemRarity.Epic, 10));
            Assert.AreEqual(Atk(ItemRarity.Rare, 10) * (1 + 10 * n.PlusStep), Atk(ItemRarity.Rare, 10, 10), 1e-12, "+10");
            var weapon = new GearItem { Kind = ItemKind.Sword, Rarity = ItemRarity.Rare, Level = 10 };
            Assert.AreEqual(0, GameSession.ItemMain(n, weapon, ItemStat.Health), "a weapon gives attack only");
            var armor = new GearItem { Kind = ItemKind.Armor, Rarity = ItemRarity.Rare, Level = 10 };
            Assert.Greater(GameSession.ItemMain(n, armor, ItemStat.Health), 0);
            Assert.Greater(s.ItemPower(new GearItem { Id = 1, Kind = ItemKind.Sword, Rarity = ItemRarity.Epic, Level = 20 }),
                s.ItemPower(new GearItem { Id = 1, Kind = ItemKind.Sword, Rarity = ItemRarity.Rare, Level = 20 }), "the power number follows");
        }

        // ------------------------------------------------------------------ equip and the bag

        [Test]
        public void ItemsFitTheirClassesAndSwapThroughTheBag()
        {
            var s = Fortress(10);
            var guardian = Hero(s, HeroClass.Guardian);
            var archer = Hero(s, HeroClass.Archer);
            var bow = s.GiveItem(ItemKind.Bow, ItemRarity.Rare, 10, 1);
            var bow2 = s.GiveItem(ItemKind.Bow, ItemRarity.Epic, 10, 2);
            var boots = s.GiveItem(ItemKind.Boots, ItemRarity.Rare, 10, 3);
            Assert.AreEqual(Fail.RulesNotMet, s.Equip(guardian.Id, bow.Id), "a guardian does not shoot");
            Assert.AreEqual(Fail.None, s.Equip(guardian.Id, boots.Id), "boots fit everyone");
            Assert.AreEqual(Fail.None, s.Equip(archer.Id, bow.Id));
            Assert.AreEqual(bow.Id, s.Worn(archer.Id, ItemSlot.Weapon).Id);
            Assert.AreEqual(Fail.None, s.Equip(archer.Id, bow2.Id));
            Assert.AreEqual(0, bow.HeroId, "the old bow went back to the bag");
            Assert.AreEqual(Fail.NotFound, s.Equip(guardian.Id, bow2.Id), "a worn item is taken off first");
            Assert.AreEqual(Fail.NotAHero, s.Equip(s.AddDweller("Тим", new[] { 5, 5, 5, 5, 5 }).Id, boots.Id));
            Assert.AreEqual(Fail.None, s.Unequip(archer.Id, ItemSlot.Weapon));
            Assert.IsTrue(s.SlotItem(archer, ItemSlot.Weapon).Issued, "the issued bow stands in");
            Assert.AreEqual(Fail.NotFound, s.Unequip(archer.Id, ItemSlot.Weapon));

            // a new class: what does not fit goes to the bag
            Assert.AreEqual(Fail.None, s.Equip(archer.Id, bow.Id));
            var barracks = new Room { Id = s.State.NextRoomId++, Type = RoomType.Barracks, Level = 1, Floor = 1, Cell = 0 };
            s.State.Rooms.Add(barracks);
            Assert.AreEqual(Fail.None, s.Promote(archer.Id, HeroClass.AshMage));
            Assert.AreEqual(0, bow.HeroId);
        }

        [Test]
        public void TheBagHasALimitThatGrowsWithTheForge()
        {
            var s = Fortress(10, forge: 0);
            var n = s.Balance.Items;
            Assert.AreEqual(n.InventoryBase, s.BagSize());
            s.State.Rooms.Add(new Room { Id = s.State.NextRoomId++, Type = RoomType.Forge, Level = 4, Floor = 0, Cell = 0 });
            Assert.AreEqual(n.InventoryBase + 4 * n.InventoryPerForge, s.BagSize());
            var hero = Hero(s, HeroClass.Swordsman);
            var worn = s.GiveItem(ItemKind.Sword, ItemRarity.Rare, 10, 1);
            s.Equip(hero.Id, worn.Id);
            for (int i = 0; s.Bag().Count < s.BagSize(); i++)
                s.GiveItem(ItemKind.Boots, ItemRarity.Common, 10, i);
            Assert.IsTrue(s.BagFull());
            Assert.AreEqual(Fail.InventoryFull, s.Unequip(hero.Id, ItemSlot.Weapon), "no place to take it off into");
            long silver = s.State.Wallet[ResourceType.Silver] + s.State.Rewards[ResourceType.Silver];
            var extra = s.GiveItem(ItemKind.Helmet, ItemRarity.Rare, 10, 99);
            Assert.AreEqual(s.BagSize(), s.Bag().Count, "a find past the limit does not enter the bag");
            Assert.IsNull(s.FindItem(extra.Id));
            Assert.AreEqual(silver + s.SellPrice(extra), s.State.Wallet[ResourceType.Silver] + s.State.Rewards[ResourceType.Silver], "it is sold on the spot");
            StringAssert.Contains("сумка полна", s.Gear.News.Last());
            var swap = s.GiveItem(ItemKind.Sword, ItemRarity.Epic, 10, 5); // sold: the bag is full
            Assert.IsNull(s.FindItem(swap.Id));
            s.SellItem(s.Bag()[0].Id);
            var epic = s.GiveItem(ItemKind.Sword, ItemRarity.Epic, 10, 5);
            Assert.AreEqual(Fail.None, s.Equip(hero.Id, epic.Id), "swapping keeps the bag's count, even when it is full");
            Assert.IsTrue(s.BagFull());
        }

        [Test]
        public void SellingPaysSilverAndDismantlingGivesScrapWithADailyCap()
        {
            var s = Fortress(10);
            var n = s.Balance.Items;
            s.State.Wallet[ResourceType.Silver] = 0;
            s.State.Rewards = new ResourceBag();
            var rare = s.GiveItem(ItemKind.Helmet, ItemRarity.Rare, 10, 1);
            long price = s.SellPrice(rare);
            Assert.AreEqual((long)Math.Round(s.Balance.Activities.Routes.IncomeByLevel[9] * n.Rarity(ItemRarity.Rare).SellHours), price);
            Assert.Greater(price, s.SellPrice(s.GiveItem(ItemKind.Helmet, ItemRarity.Common, 10, 2)), "rarer sells dearer");
            Assert.AreEqual(Fail.None, s.SellItem(rare.Id));
            Assert.AreEqual(price, s.State.Wallet[ResourceType.Silver]);
            Assert.AreEqual(Fail.NotFound, s.SellItem(rare.Id), "sold once");
            var worn = s.GiveItem(ItemKind.Boots, ItemRarity.Rare, 10, 3);
            s.Equip(Hero(s, HeroClass.Guardian).Id, worn.Id);
            Assert.AreEqual(Fail.NotFound, s.SellItem(worn.Id), "take it off first");
            Assert.AreEqual(Fail.NotFound, s.Dismantle(worn.Id, T0));

            int cap = n.DismantlePerDay;
            Assert.AreEqual(cap, s.DismantlesLeft(T0));
            long scrap = 0;
            for (int i = 0; i < cap; i++)
            {
                var item = s.GiveItem(ItemKind.Amulet, ItemRarity.Epic, 10, 10 + i);
                scrap += s.ScrapOf(item);
                Assert.AreEqual(Fail.None, s.Dismantle(item.Id, T0));
            }
            Assert.AreEqual(scrap, s.Gear.Scrap);
            Assert.AreEqual(cap * n.Rarity(ItemRarity.Epic).Scrap, scrap);
            var more = s.GiveItem(ItemKind.Amulet, ItemRarity.Epic, 10, 77);
            Assert.AreEqual(Fail.LimitReached, s.Dismantle(more.Id, T0), "no farming: a daily cap");
            Assert.AreEqual(Fail.None, s.Dismantle(more.Id, T0 + Day), "a new day, new dismantling");
        }

        // ------------------------------------------------------------------ upgrades and sets

        [Test]
        public void UpgradesCostIronAndEssenceUpToPlusTen()
        {
            var s = Fortress(14);
            var n = s.Balance.Items;
            var sword = s.GiveItem(ItemKind.Sword, ItemRarity.Rare, 14, 1);
            double before = s.ItemStatValue(sword, ItemStat.Attack);
            var cost = s.UpgradeCost(sword);
            Assert.Greater(cost[ResourceType.Iron], 0);
            Assert.Greater(cost[ResourceType.Essence], 0);
            s.State.Wallet[ResourceType.Iron] = 0;
            Assert.AreEqual(Fail.NotEnough, s.UpgradeItem(sword.Id));
            s.State.Wallet[ResourceType.Iron] = 1_000_000;
            s.State.Wallet[ResourceType.Essence] = 1_000_000;
            long essence = s.State.Wallet[ResourceType.Essence];
            Assert.AreEqual(Fail.None, s.UpgradeItem(sword.Id));
            Assert.AreEqual(1, sword.Plus);
            Assert.AreEqual(essence - cost[ResourceType.Essence], s.State.Wallet[ResourceType.Essence]);
            Assert.AreEqual(before * (1 + n.PlusStep), s.ItemStatValue(sword, ItemStat.Attack), 1e-12);
            Assert.Greater(s.UpgradeCost(sword)[ResourceType.Iron], cost[ResourceType.Iron], "every step costs more");
            while (sword.Plus < n.PlusMax)
                Assert.AreEqual(Fail.None, s.UpgradeItem(sword.Id));
            Assert.IsNull(s.UpgradeCost(sword));
            Assert.AreEqual(Fail.LimitReached, s.UpgradeItem(sword.Id), "+10 is the top");
            Assert.AreEqual("Меч +10", s.ItemName(sword));
            var noForge = Fortress(14, forge: 0);
            var item = noForge.GiveItem(ItemKind.Sword, ItemRarity.Rare, 14, 1);
            noForge.State.Wallet[ResourceType.Essence] = 1_000_000;
            Assert.AreEqual(Fail.Locked, noForge.UpgradeItem(item.Id), "upgrades are made in the forge");
        }

        [Test]
        public void TwoAndFourPiecesOfASetGiveTheirBonuses()
        {
            var s = Fortress(16);
            var n = s.Balance.Items;
            var set = n.Sets[0];
            var hero = Hero(s, HeroClass.Guardian);
            var kinds = new[] { ItemKind.Mace, ItemKind.Shield, ItemKind.Helmet, ItemKind.Armor };
            var items = kinds.Select((k, i) => s.GiveItem(k, ItemRarity.Epic, 16, 200 + i, set.Id)).ToList();
            Assert.IsTrue(items.All(i => i.Set == set.Id));
            GearBonus Without() { var g = new GearBonus(); foreach (var slot in GameSession.Slots) foreach (var st in s.ItemStats(s.SlotItem(hero, slot))) g.Add(st.Stat, st.Value); return g; }

            s.Equip(hero.Id, items[0].Id);
            Assert.AreEqual(Without().Health, s.HeroGear(hero).Health, 1e-12, "one piece: no bonus");
            s.Equip(hero.Id, items[1].Id);
            double two = set.Two.Where(x => x.Stat == ItemStat.Health).Sum(x => x.Value);
            Assert.AreEqual(1, s.HeroSets(hero.Id).Count);
            Assert.AreEqual(2, s.HeroSets(hero.Id)[0].count);
            foreach (var b in set.Two)
                Assert.AreEqual(Without().Get(b.Stat) + b.Value, s.HeroGear(hero).Get(b.Stat), 1e-12, "two pieces: " + b.Stat);
            s.Equip(hero.Id, items[2].Id);
            s.Equip(hero.Id, items[3].Id);
            var all = set.Two.Concat(set.Four).GroupBy(x => x.Stat);
            foreach (var g in all)
                Assert.AreEqual(Without().Get(g.Key) + g.Sum(x => x.Value), s.HeroGear(hero).Get(g.Key), 1e-12, "four pieces: " + g.Key);
            Assert.Greater(two, 0);
            Assert.Greater(s.HeroGearPower(hero), s.WornBy(hero.Id).Sum(s.ItemPower), "the set adds to the power");
        }

        // ------------------------------------------------------------------ the forge

        [Test]
        public void TheForgeMakesTheChosenItemOnATimer()
        {
            var s = Fortress(12, forge: 9);
            var n = s.Balance.Items;
            s.State.Wallet[ResourceType.Silver] = 1_000_000;
            s.State.Wallet[ResourceType.Iron] = 1_000_000;
            Assert.AreEqual(Fail.None, s.CraftOpen(ItemRarity.Rare), "forge 9 makes rare items");
            Assert.AreEqual(Fail.Locked, s.CraftOpen(ItemRarity.Epic), "epic items need a forge of " + n.Rarity(ItemRarity.Epic).ForgeLevel);
            Assert.AreEqual(Fail.Locked, s.CraftOpen(ItemRarity.Legendary));
            Assert.AreEqual(Fail.NotEnough, s.StartCraft(ItemKind.Bow, ItemRarity.Rare, T0), "rare items need scrap");
            s.Gear.Scrap = 100;
            var cost = s.CraftCost(ItemRarity.Rare);
            long silver = s.State.Wallet[ResourceType.Silver];
            Assert.AreEqual(Fail.None, s.StartCraft(ItemKind.Bow, ItemRarity.Rare, T0));
            Assert.AreEqual(silver - cost[ResourceType.Silver], s.State.Wallet[ResourceType.Silver]);
            Assert.AreEqual(100 - n.Rarity(ItemRarity.Rare).CraftScrap, s.Gear.Scrap);
            Assert.AreEqual(Fail.AlreadyQueued, s.StartCraft(ItemKind.Bow, ItemRarity.Rare, T0), "one item at a time");
            long seconds = s.CraftSeconds(ItemRarity.Rare);
            int bag = s.Bag().Count;
            s.Advance(T0 + seconds - 1);
            Assert.AreEqual(bag, s.Bag().Count, "not yet");
            s.Advance(T0 + seconds);
            Assert.IsNull(s.Gear.Craft);
            var made = s.Bag().Last();
            Assert.AreEqual(ItemKind.Bow, made.Kind);
            Assert.AreEqual(ItemRarity.Rare, made.Rarity);
            Assert.AreEqual(9, made.Level, "the forge's level");
            Assert.AreEqual(2, made.Rolls.Count);
            Assert.AreEqual("Кузня", made.Source);

            // the same forging at the same moment rolls the same item
            var twin = Fortress(12, forge: 9);
            twin.State.Wallet[ResourceType.Silver] = 1_000_000;
            twin.State.Wallet[ResourceType.Iron] = 1_000_000;
            twin.Gear.Scrap = 100;
            twin.StartCraft(ItemKind.Bow, ItemRarity.Rare, T0);
            twin.Advance(T0 + seconds);
            Assert.AreEqual(Json(made.Rolls), Json(twin.Bag().Last().Rolls));

            // Sparks finish it now, the last minutes are free
            s.Gear.Scrap = 100;
            s.StartCraft(ItemKind.Quiver, ItemRarity.Rare, T0 + seconds);
            s.State.Wallet[ResourceType.Sparks] = 0;
            Assert.AreEqual(Fail.NotEnough, s.FinishCraftNow(T0 + seconds + 1));
            s.State.Wallet[ResourceType.Sparks] = 1000;
            Assert.Greater(s.CraftSpeedupPrice(T0 + seconds + 1), 0);
            Assert.AreEqual(Fail.None, s.FinishCraftNow(T0 + seconds + 1));
            Assert.AreEqual(ItemKind.Quiver, s.Bag().Last().Kind);
            Assert.AreEqual(0, s.CraftSpeedupPrice(T0), "nothing to finish");
        }

        [Test]
        public void NoForgeNoForging()
        {
            var s = Fortress(12, forge: 0);
            Assert.AreEqual(Fail.Locked, s.StartCraft(ItemKind.Sword, ItemRarity.Common, T0));
        }

        [Test]
        public void LegendaryItemsAreForgedFromCoresFromHearthTwenty()
        {
            var b = TestData.Balance();
            var legendary = b.Activities.Legendary;
            var early = Fortress(legendary.FromHearth - 1);
            Assert.AreEqual(Fail.Locked, early.CraftOpen(ItemRarity.Legendary));
            var s = Fortress(legendary.FromHearth);
            Assert.AreEqual(Fail.None, s.CraftOpen(ItemRarity.Legendary));
            Assert.AreEqual(legendary.Cores, s.CraftCost(ItemRarity.Legendary)[ResourceType.Cores], "ACTIVITIES.md 3: 400 Cores");
            s.State.Wallet[ResourceType.Cores] = legendary.Cores - 1;
            Assert.AreEqual(Fail.NotEnough, s.StartCraft(ItemKind.Mace, ItemRarity.Legendary, T0, "Ash"));
            s.State.Wallet[ResourceType.Cores] = legendary.Cores;
            Assert.AreEqual(Fail.None, s.StartCraft(ItemKind.Mace, ItemRarity.Legendary, T0, "Ash"));
            Assert.AreEqual(0, s.State.Wallet[ResourceType.Cores]);
            s.Advance(T0 + s.CraftSeconds(ItemRarity.Legendary));
            var item = s.Bag().Last();
            Assert.AreEqual(ItemRarity.Legendary, item.Rarity);
            Assert.AreEqual("Ash", item.Set, "the chosen set");
            Assert.AreEqual(3, item.Rolls.Count);
        }

        // ------------------------------------------------------------------ drops

        [Test]
        public void TheCampaignDropsItemsAndTheThreeStarChestHasGear()
        {
            var b = TestData.Balance();
            int boss = b.Campaign.Missions.FindIndex(m => m.Kind == MissionKind.Boss);
            var s = Fortress(b.Campaign.Missions[boss].Rank);
            for (int i = 0; i < boss; i++)
                s.State.CampaignStars.Add(2);
            Assert.AreEqual(Fail.None, s.StartMission(boss, T0, out var battle));
            battle.Brain = Brains.Careful();
            s.FinishMission(battle, out var reward);
            Assert.IsTrue(reward.FirstClear);
            Assert.GreaterOrEqual(reward.Items.Count, 1, "the act boss always drops");
            Assert.GreaterOrEqual(reward.Items[0].Rarity, ItemRarity.Rare);
            Assert.AreEqual(b.Campaign.Missions[boss].Rank, reward.Items[0].Level, "the level of the battle");
            if (reward.Stars == 3)
                Assert.AreEqual(2, reward.Items.Count, "and the chest of three stars");
            Assert.IsTrue(reward.Items.All(i => s.FindItem(i.Id) != null), "into the bag");

            // the chest opens when a cleared battle gets its third star later
            var t = Fortress(b.Campaign.Missions[0].Rank + 6);
            t.State.CampaignStars.Add(2);
            Assert.AreEqual(Fail.None, t.StartMission(0, T0, out var again));
            again.Brain = Brains.Careful();
            t.FinishMission(again, out var chest);
            Assert.AreEqual(3, chest.Stars);
            Assert.AreEqual(1, chest.Items.Count, "the chest: one piece of gear");
            Assert.AreEqual("Сундук трёх звёзд", chest.Items[0].Source);
        }

        [Test]
        public void RepeatsHuntsAndRoutesFindItemsSometimes()
        {
            var s = Fortress(12);
            s.State.CampaignStars.Add(3);
            int found = 0;
            for (int day = 0; day < 4; day++)
            {
                for (int i = 0; i < s.Balance.Campaign.RepeatsPerDay; i++)
                {
                    s.RepeatMission(0, T0 + day * Day, out var r);
                    found += r.Items.Count;
                }
            }
            Assert.That(found, Is.InRange(2, 25), "repeats are for collecting gear: about a quarter");

            int hunts = 0, items = 0;
            for (int i = 0; i < 30; i++)
            {
                long now = T0 + i * Day;
                s.State.HuntTracks = 3;
                if (s.StartHunt(s.HuntDens(now)[0], 8, now, out var battle) != Fail.None)
                    continue;
                battle.Brain = Brains.Careful();
                s.FinishHunt(battle, out var reward);
                hunts++;
                if (reward.Item != null)
                {
                    items++;
                    Assert.AreEqual(8, reward.Item.Level, "an item of the rank");
                }
            }
            Assert.AreEqual(30, hunts);
            Assert.That(items, Is.InRange(5, 22));

            var walker = s.AddDweller("Ходок", new[] { 30, 30, 30, 30, 30 });
            int routes = 0, finds = 0;
            for (int i = 0; i < 20; i++)
            {
                long now = T0 + (10 + i) * Day;
                int bag = s.Bag().Count + s.Gear.News.Count(x => x.Contains("продано"));
                if (s.StartRoute(new List<int> { walker.Id }, RoutePath.Safe, RoutePath.Safe, now, out var route) != Fail.None)
                    continue;
                routes++;
                s.Advance(route.End);
                if (route.Report.Contains("нашли"))
                    finds++;
                s.ResolveRouteCard(route.Id, 2, route.End);
            }
            Assert.Greater(routes, 10);
            Assert.That(finds, Is.InRange(1, routes - 1));
        }

        [Test]
        public void ADropOnlyDependsOnItsBattle()
        {
            GearItem Hunt()
            {
                var s = Fortress(12);
                for (int i = 0; i < 40; i++)
                {
                    long now = T0 + i * Day;
                    s.State.HuntTracks = 3;
                    s.StartHunt(s.HuntDens(now)[0], 8, now, out var battle);
                    battle.Brain = Brains.Careful();
                    s.FinishHunt(battle, out var reward);
                    if (reward.Item != null)
                        return reward.Item;
                }
                return null;
            }
            var a = Hunt();
            var b = Hunt();
            Assert.IsNotNull(a);
            Assert.AreEqual(Json(a), Json(b), "the same hunt finds the same item");
        }

        [Test]
        public void EventThresholdsAndTopRanksBringItems()
        {
            var s = Fortress(16);
            s.Advance(T0 + 1); // the newcomer's personal runs open
            var run = s.ActiveRuns(T0 + 1).First();
            int bag = s.Bag().Count;
            s.AddPoints(run, GameSession.EventTarget[run.Id] * 4);
            Assert.AreEqual(10, run.Thresholds);
            var n = s.Balance.Items;
            Assert.AreEqual(bag + n.EventThresholds.Length, s.Bag().Count, "thresholds 5 and 10");
            Assert.IsTrue(s.Bag().Skip(bag).All(i => i.Rarity >= ItemRarity.Rare && i.Level == 16));
            s.Advance(run.End + 1);
            Assert.IsTrue(run.Closed);
            Assert.AreEqual(1, s.EventRank(run));
            Assert.IsTrue(s.Bag().Any(i => i.Rarity == ItemRarity.Epic && i.Source.Contains("место 1")), "the first place: an epic item");
        }

        [Test]
        public void TheGloryShopSellsAnEpicPieceShownBeforeBuying()
        {
            var b = TestData.Balance();
            int gear = b.Activities.Pvp.GloryShop.FindIndex(g => g.Goods == "Gear");
            Assert.GreaterOrEqual(gear, 0, "MODES.md 4.2");
            Assert.AreEqual(400, b.Activities.Pvp.GloryShop[gear].Price);
            Assert.AreEqual(2, b.Activities.Pvp.GloryShop[gear].PerWeek);
            Assert.AreEqual("Эпическая часть снаряжения", GameSession.GloryGoodsName("Gear"));
            var s = Fortress(16);
            var archer = Hero(s, HeroClass.Archer);
            // everyone wears an epic weapon but the archer: the archer's bow is the weakest slot... after the others
            foreach (var d in s.Squad())
            {
                foreach (var slot in GameSession.Slots)
                {
                    if (d == archer && slot == ItemSlot.Weapon)
                        continue;
                    var it = s.GiveItem(ItemKinds.For(d.HeroClass, slot)[0], ItemRarity.Rare, 16, d.Id * 10 + (int)slot);
                    s.Equip(d.Id, it.Id);
                }
            }
            var preview = s.GloryGearPreview(T0);
            Assert.AreEqual(ItemRarity.Epic, preview.Rarity);
            Assert.AreEqual(ItemKind.Bow, preview.Kind, "for the weakest slot of the squad");
            Assert.AreEqual(16, preview.Level);
            s.State.Wallet[ResourceType.Glory] = 1000;
            Assert.AreEqual(Fail.None, s.BuyGlory(gear, T0));
            var bought = s.Bag().Last();
            Assert.AreEqual(Json(preview.Rolls), Json(bought.Rolls), "exactly what was shown");
            Assert.AreEqual(1000 - 400, s.State.Wallet[ResourceType.Glory]);
            Assert.AreEqual(Fail.None, s.BuyGlory(gear, T0));
            Assert.AreEqual(Fail.LimitReached, s.BuyGlory(gear, T0), "two a week");
        }

        // ------------------------------------------------------------------ in battle

        static BattleSetup Squad(int rank, Func<HeroClass, GearBonus> gear, MonsterFamily family = MonsterFamily.Possessed, bool mirror = false)
        {
            var setup = CombatCalibrationTests.Squad(rank, 2 * rank, family, 7);
            foreach (var h in setup.Heroes)
                h.Gear = gear(h.Class);
            if (mirror)
                setup.Waves.Add(new WaveSpec { Family = family, Rank = rank, HpMult = 3, AttackMult = 1, Mirror = true });
            return setup;
        }

        static GearBonus Kit(int rank, Action<GearBonus> extra = null)
        {
            var g = GameSession.IssuedKit(TestData.Balance().Items, rank);
            extra?.Invoke(g);
            return g;
        }

        [Test]
        public void MainStatsAndExtraStatsChangeTheFighter()
        {
            var c = TestData.Combat();
            var plain = new Battle(c, Squad(10, _ => Kit(10))).Heroes[1];
            var geared = new Battle(c, Squad(10, _ => Kit(10, g =>
            {
                g.Attack += 0.5;
                g.Health += 0.25;
                g.Crit += 0.04;
                g.Haste += 0.25;
                g.Armor += 12;
                g.SkillPower += 0.1;
                g.Dodge += 0.04;
            }))).Heroes[1];
            Assert.AreEqual(plain.Attack * (1 + Kit(10).Attack + 0.5) / (1 + Kit(10).Attack), geared.Attack, 1e-6);
            Assert.AreEqual(plain.MaxHp * (1 + Kit(10).Health + 0.25) / (1 + Kit(10).Health), geared.MaxHp, 1e-6);
            Assert.AreEqual(plain.Crit + 0.04, geared.Crit, 1e-12);
            Assert.AreEqual(plain.Armor + 12, geared.Armor, 1e-12);
            Assert.AreEqual((int)Math.Round(plain.Interval / 1.25), geared.Interval, "haste: strikes more often");
            Assert.AreEqual(plain.SkillMult * 1.1, geared.SkillMult, 1e-12);
            Assert.AreEqual(0.04, geared.Dodge, 1e-12);
        }

        [Test]
        public void DodgeMakesBlowsMissAndTheBattleWithoutItStaysTheSame()
        {
            var c = TestData.Combat();
            string Log(BattleSetup s) { var b = new Battle(c, s) { Brain = Brains.Careful() }; b.RunToEnd(); return string.Join("|", b.Log) + b.BossHp.ToString("R"); }
            Assert.AreEqual(Log(Squad(12, _ => Kit(12))), Log(Squad(12, _ => Kit(12))), "deterministic");
            var dodgy = new Battle(c, Squad(12, _ => Kit(12, g => g.Dodge = 0.3))) { Brain = Brains.AutoBattle() };
            dodgy.RunToEnd();
            Assert.Greater(dodgy.Heroes.Sum(h => h.Dodged), 0);
            var plain = new Battle(c, Squad(12, _ => Kit(12))) { Brain = Brains.AutoBattle() };
            plain.RunToEnd();
            Assert.AreEqual(0, plain.Heroes.Sum(h => h.Dodged));
        }

        [Test]
        public void MonsterDamageHitsMonstersButNotTheGhostsOfPvp()
        {
            var c = TestData.Combat();
            double Dealt(bool mirror, double bonus)
            {
                var b = new Battle(c, Squad(12, _ => Kit(12, g => g.MonsterDamage = bonus), mirror: mirror));
                for (int i = 0; i < 60; i++)
                    b.Step();
                return b.DamageDealt;
            }
            Assert.AreEqual(Dealt(false, 0) * 1.1, Dealt(false, 0.1), Dealt(false, 0) * 1e-9, "+10% against a monster");
            Assert.AreEqual(Dealt(true, 0), Dealt(true, 0.1), 1e-9, "nothing against a ghost");
        }

        [Test]
        public void HealingItemsHealMore()
        {
            var c = TestData.Combat();
            double SquadHp(double healing)
            {
                var b = new Battle(c, Squad(12, cls => Kit(12, g => g.Healing = cls == HeroClass.CoalPriest ? healing : 0)));
                for (int i = 0; i < 400 && !b.Over; i++)
                    b.Step();
                return b.Heroes.Sum(h => Math.Max(0, h.Hp));
            }
            Assert.Greater(SquadHp(0.5), SquadHp(0));
        }

        [Test]
        public void TheGhostOfPvpWearsTheSameGear()
        {
            var c = TestData.Combat();
            var plain = new Battle(c, Squad(16, _ => Kit(16), mirror: true));
            var geared = new Battle(c, Squad(16, _ => Kit(16, g => { g.Crit = 0.05; g.Dodge = 0.05; g.Armor = 10; }), mirror: true));
            Assert.Greater(geared.BossMaxHp, plain.BossMaxHp, "the crit of the items in the ghost's health");
            Assert.Greater(geared.BossAttack, plain.BossAttack, "dodge and armor in the ghost's attack");
        }

        // ------------------------------------------------------------------ saves

        [Test]
        public void ItemsAndTheForgeJobSurviveASave()
        {
            var s = Fortress(14);
            var hero = Hero(s, HeroClass.Cleaver);
            var axe = s.GiveItem(ItemKind.Axe, ItemRarity.Epic, 14, 5, "Mist");
            s.Equip(hero.Id, axe.Id);
            s.GiveItem(ItemKind.Boots, ItemRarity.Uncommon, 13, 6);
            s.Gear.Scrap = 30;
            s.State.Wallet[ResourceType.Silver] = 1_000_000;
            s.State.Wallet[ResourceType.Iron] = 1_000_000;
            Assert.AreEqual(Fail.None, s.StartCraft(ItemKind.Bow, ItemRarity.Rare, T0));
            s.Gear.Items[0].Plus = 3;
            var loaded = SaveSystem.FromJson(SaveSystem.ToJson(s.State));
            Assert.IsNotNull(loaded);
            var r = new GameSession(s.Balance, loaded) { Combat = s.Combat };
            Assert.AreEqual(Json(s.State.Gear), Json(r.State.Gear));
            Assert.AreEqual(axe.Id, r.Worn(hero.Id, ItemSlot.Weapon).Id);
            Assert.AreEqual(s.HeroPreview(hero.Id).Attack, r.HeroPreview(hero.Id).Attack, 1e-9);
            r.Advance(T0 + r.CraftSeconds(ItemRarity.Rare));
            Assert.AreEqual(ItemKind.Bow, r.Bag().Last().Kind, "the forging goes on after loading");
        }

        [Test]
        public void AnOldSaveWithoutItemsKeepsItsHeroesPower()
        {
            var s = Fortress(12);
            var c = s.Combat;
            var json = JObject.Parse(JsonConvert.SerializeObject(s.State));
            json["SaveVersion"] = 2;
            json.Remove("Gear");
            var state = JsonConvert.DeserializeObject<GameState>(json.ToString());
            var old = new GameSession(s.Balance, state) { Combat = c };
            Assert.AreEqual(3, old.State.SaveVersion);
            Assert.IsNotNull(old.State.Gear);
            foreach (var d in old.Squad())
            {
                var f = old.HeroPreview(d.Id);
                var cls = c.Class(d.HeroClass);
                // the gear of before: attack and health x1.05^(hearth - 1)
                double before = cls.Attack * Math.Pow(c.LevelGrowth, d.HeroLevel - 1) * Math.Pow(c.GearGrowth, 11) *
                                (1 + d.Stats[(int)cls.MainStat] * c.StatBonus);
                Assert.AreEqual(before, f.Attack, before * 0.01, d.HeroClass + ": the same attack as before items");
                Assert.GreaterOrEqual(f.Attack, before, "not less");
                double hp = cls.Hp * Math.Pow(c.LevelGrowth, d.HeroLevel - 1) * Math.Pow(c.GearGrowth, 11);
                Assert.AreEqual(hp, f.MaxHp, hp * 0.01);
            }
            Assert.IsEmpty(old.Bag());
        }

        // ------------------------------------------------------------------ the bot

        [Test]
        public void ThePlayerBotWearsAndForgesItems()
        {
            var s = TestData.NewSession();
            s.Combat = TestData.Combat();
            var bot = new PlayerBot(s);
            bot.PlayDays(T0, 6);
            Assert.Greater(bot.Equipped, 10, "finds are worn");
            Assert.Greater(bot.Crafted, 0, "the forge works");
            Assert.Greater(bot.Dismantled + bot.Sold, 0, "the bag is cleaned");
            var squad = s.Squad();
            Assert.IsTrue(squad.All(d => s.WornBy(d.Id).Count > 0), "every hero of the squad wears something");
            foreach (var d in squad)
                Assert.GreaterOrEqual(s.HeroGearPower(d), GameSession.Slots.Sum(slot => s.ItemPower(s.IssuedPiece(d.HeroClass, slot))), "never below the issued kit");
            Assert.LessOrEqual(s.Bag().Count, s.BagSize());
        }
    }
}
