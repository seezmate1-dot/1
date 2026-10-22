using System;
using System.Collections.Generic;

namespace LastHearth.Core
{
    // GDD 8 «Снаряжение», 8.3: the heroes' items. Numbers come from hearth/tools/economy.py (balance.json:
    // "items", ECONOMY.md section 17); the saved part is GearState in GameState; the rules are in
    // GameSession.Gear.cs.

    /// <summary>The six places of a hero's equipment.</summary>
    public enum ItemSlot
    {
        Weapon,
        OffHand,
        Helmet,
        Armor,
        Boots,
        Amulet,
    }

    /// <summary>Grey, green, blue, purple, orange.</summary>
    public enum ItemRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary,
    }

    /// <summary>What an item gives. Attack and Health are the main stats (shares of the hero's base); the rest
    /// are the extra stats rolled by rarity, and the bonuses of the sets.</summary>
    public enum ItemStat
    {
        Attack,
        Health,
        /// <summary>Crit chance, added.</summary>
        Crit,
        /// <summary>Chance that an enemy blow misses the hero.</summary>
        Dodge,
        /// <summary>The hero strikes this much more often.</summary>
        Haste,
        /// <summary>Flat armor: damage × 100 / (100 + armor).</summary>
        Armor,
        /// <summary>Healing done by the hero and healing the hero receives.</summary>
        Healing,
        /// <summary>Damage against monsters (not against the ghosts of PvP).</summary>
        MonsterDamage,
        /// <summary>Power of the hero's special skill.</summary>
        SkillPower,
    }

    /// <summary>One kind of item: one picture (Resources/Art/Items/&lt;Kind&gt;), one slot, the classes that wear it.</summary>
    public enum ItemKind
    {
        Mace,
        Sword,
        Axe,
        Bow,
        Censer,
        Staff,
        Shield,
        Bracers,
        Quiver,
        Tome,
        Helmet,
        Hood,
        Armor,
        Jerkin,
        Robe,
        Boots,
        Amulet,
    }

    public sealed class ItemKindInfo
    {
        public ItemKind Kind;
        public ItemSlot Slot;
        public string Name = "";
        /// <summary>The short name drawn on the plate while the item art is not painted.</summary>
        public string Short = "";
        /// <summary>Empty: every class.</summary>
        public HeroClass[] Classes = new HeroClass[0];

        public bool Fits(HeroClass c) => Classes.Length == 0 || Array.IndexOf(Classes, c) >= 0;
    }

    /// <summary>The kinds of items: weapons and off-hands follow the class, helmets and armor the weight.</summary>
    public static class ItemKinds
    {
        static readonly HeroClass[] Heavy = { HeroClass.Guardian, HeroClass.Swordsman, HeroClass.Cleaver };
        static readonly HeroClass[] Light = { HeroClass.Archer, HeroClass.CoalPriest, HeroClass.AshMage };

        public static readonly ItemKindInfo[] All =
        {
            K(ItemKind.Mace, ItemSlot.Weapon, "Булава", "Булава", HeroClass.Guardian),
            K(ItemKind.Sword, ItemSlot.Weapon, "Меч", "Меч", HeroClass.Swordsman),
            K(ItemKind.Axe, ItemSlot.Weapon, "Секира", "Секира", HeroClass.Cleaver),
            K(ItemKind.Bow, ItemSlot.Weapon, "Лук", "Лук", HeroClass.Archer),
            K(ItemKind.Censer, ItemSlot.Weapon, "Кадило", "Кадило", HeroClass.CoalPriest),
            K(ItemKind.Staff, ItemSlot.Weapon, "Посох", "Посох", HeroClass.AshMage),
            K(ItemKind.Shield, ItemSlot.OffHand, "Щит", "Щит", HeroClass.Guardian, HeroClass.Swordsman),
            K(ItemKind.Bracers, ItemSlot.OffHand, "Наручи", "Наручи", HeroClass.Cleaver),
            K(ItemKind.Quiver, ItemSlot.OffHand, "Колчан", "Колчан", HeroClass.Archer),
            K(ItemKind.Tome, ItemSlot.OffHand, "Фолиант", "Фолиант", HeroClass.CoalPriest, HeroClass.AshMage),
            K(ItemKind.Helmet, ItemSlot.Helmet, "Шлем", "Шлем", Heavy),
            K(ItemKind.Hood, ItemSlot.Helmet, "Капюшон", "Капюш.", Light),
            K(ItemKind.Armor, ItemSlot.Armor, "Латы", "Латы", Heavy),
            K(ItemKind.Jerkin, ItemSlot.Armor, "Кожаная куртка", "Куртка", HeroClass.Archer),
            K(ItemKind.Robe, ItemSlot.Armor, "Мантия", "Мантия", HeroClass.CoalPriest, HeroClass.AshMage),
            K(ItemKind.Boots, ItemSlot.Boots, "Сапоги", "Сапоги"),
            K(ItemKind.Amulet, ItemSlot.Amulet, "Амулет", "Амулет"),
        };

        static ItemKindInfo K(ItemKind kind, ItemSlot slot, string name, string shortName, params HeroClass[] classes) =>
            new ItemKindInfo { Kind = kind, Slot = slot, Name = name, Short = shortName, Classes = classes };

        public static ItemKindInfo Of(ItemKind kind) => All[(int)kind];

        public static ItemSlot SlotOf(ItemKind kind) => All[(int)kind].Slot;

        /// <summary>The kinds a hero of this class wears in this slot (the first is the forge's issued one).</summary>
        public static List<ItemKind> For(HeroClass c, ItemSlot slot)
        {
            var list = new List<ItemKind>();
            foreach (var k in All)
            {
                if (k.Slot == slot && k.Fits(c))
                    list.Add(k.Kind);
            }
            return list;
        }

        public static string SlotName(ItemSlot slot)
        {
            switch (slot)
            {
                case ItemSlot.Weapon: return "Оружие";
                case ItemSlot.OffHand: return "Вторая рука";
                case ItemSlot.Helmet: return "Шлем";
                case ItemSlot.Armor: return "Доспех";
                case ItemSlot.Boots: return "Сапоги";
                default: return "Амулет";
            }
        }

        public static string StatName(ItemStat stat)
        {
            switch (stat)
            {
                case ItemStat.Attack: return "атака";
                case ItemStat.Health: return "здоровье";
                case ItemStat.Crit: return "шанс крита";
                case ItemStat.Dodge: return "уклонение";
                case ItemStat.Haste: return "скорость атаки";
                case ItemStat.Armor: return "броня";
                case ItemStat.Healing: return "лечение";
                case ItemStat.MonsterDamage: return "урон по чудовищам";
                default: return "сила умения";
            }
        }

        /// <summary>"+12%" for shares, "+8" for armor.</summary>
        public static string StatValue(ItemStat stat, double value) =>
            stat == ItemStat.Armor ? "+" + Math.Round(value).ToString("0") : "+" + (value * 100).ToString(value < 0.1 ? "0.#" : "0") + "%";
    }

    // ------------------------------------------------------------------ numbers (balance.json "items")

    public sealed class ItemSlotData
    {
        public ItemSlot Slot;
        /// <summary>Shares of the kit's attack and health this slot carries.</summary>
        public double Attack;
        public double Health;
    }

    public sealed class ItemRarityData
    {
        public ItemRarity Rarity;
        public string Name = "";
        /// <summary>Colour of the frame, "#rrggbb".</summary>
        public string Color = "#9d9d9d";
        /// <summary>Main stats × this.</summary>
        public double Mult = 1;
        /// <summary>How many extra stats an item of this rarity rolls.</summary>
        public int Secondaries;
        /// <summary>Extra stats × this (a rare item: × 1).</summary>
        public double Roll;
        /// <summary>The forge level that forges it.</summary>
        public int ForgeLevel = 1;
        /// <summary>Forging: hours of the model income (at the item's level) in materials, scrap, minutes.</summary>
        public double CraftHours;
        public long CraftScrap;
        public long CraftMinutes;
        /// <summary>Selling: hours of the model income in silver.</summary>
        public double SellHours;
        /// <summary>Dismantling: scrap.</summary>
        public long Scrap;
    }

    public sealed class ItemStatData
    {
        public ItemStat Stat;
        public string Name = "";
        /// <summary>Top value of the stat on a rare item (an item rolls half to all of it, × the rarity's Roll).</summary>
        public double Max;
        /// <summary>How much of a main stat one unit is worth in the item's power.</summary>
        public double Power;
    }

    public sealed class StatValueData
    {
        public ItemStat Stat;
        public double Value;
    }

    public sealed class ItemSetData
    {
        public string Id = "";
        public string Name = "";
        /// <summary>Bonuses of two and of four pieces worn.</summary>
        public List<StatValueData> Two = new List<StatValueData>();
        public List<StatValueData> Four = new List<StatValueData>();
    }

    public sealed class ItemDropData
    {
        public double Chance;
        /// <summary>Weights of common .. legendary.</summary>
        public int[] Weights = new int[5];
    }

    public sealed class ItemsData
    {
        /// <summary>Attack and health a full common kit of item level 1-30 adds (shares of the hero's base).</summary>
        public double[] Kit = new double[0];
        public List<ItemSlotData> Slots = new List<ItemSlotData>();
        public List<ItemRarityData> Rarities = new List<ItemRarityData>();
        public List<ItemStatData> Stats = new List<ItemStatData>();
        public List<ItemSetData> Sets = new List<ItemSetData>();
        public double EpicSetChance;
        public int PlusMax = 10;
        /// <summary>Every +1: main stats + this share.</summary>
        public double PlusStep;
        /// <summary>Upgrade +k → +k+1 (index k): hours of the model income in iron, days of the daily essence.</summary>
        public double[] PlusIronHours = new double[0];
        public double[] PlusEssenceDays = new double[0];
        public int InventoryBase = 30;
        public int InventoryPerForge;
        public int DismantlePerDay = 10;
        public Dictionary<string, ItemDropData> Drops = new Dictionary<string, ItemDropData>();
        /// <summary>Event thresholds that bring an item; the ranks up to EventRankItem bring an epic one.</summary>
        public int[] EventThresholds = new int[0];
        public int EventRankItem;
        /// <summary>The free player's items over the issued kit, by rank: the campaign's enemies get × this of
        /// health and attack (like the kennel, economy.py gear_edge).</summary>
        public double[] CampaignEdge = new double[0];

        public ItemRarityData Rarity(ItemRarity r) => Rarities[(int)r];

        public ItemSlotData Slot(ItemSlot s) => Slots.Find(x => x.Slot == s);

        public ItemStatData Stat(ItemStat s) => Stats.Find(x => x.Stat == s);

        public ItemSetData Set(string id) => string.IsNullOrEmpty(id) ? null : Sets.Find(x => x.Id == id);

        public double KitAt(int level) => Kit.Length == 0 ? 0 : Kit[Math.Max(0, Math.Min(Kit.Length - 1, level - 1))];
    }

    // ------------------------------------------------------------------ saved state

    /// <summary>One extra stat of an item: which, and how well it rolled (0..1 of the range).</summary>
    [Serializable]
    public sealed class ItemRoll
    {
        public ItemStat Stat;
        public double Q;
    }

    /// <summary>An item. Its numbers are worked out from the balance (kind, rarity, level, plus, rolls),
    /// so a change of balance.json changes old items too.</summary>
    [Serializable]
    public sealed class GearItem
    {
        /// <summary>0: the forge's issued piece in an empty slot (never saved).</summary>
        public int Id;
        public ItemKind Kind;
        public ItemRarity Rarity;
        public int Level = 1;
        /// <summary>Upgrades +0..+10.</summary>
        public int Plus;
        /// <summary>The set the item belongs to ("" = none).</summary>
        public string Set = "";
        public List<ItemRoll> Rolls = new List<ItemRoll>();
        /// <summary>The hero (dweller id) who wears it; 0 = in the bag.</summary>
        public int HeroId;
        /// <summary>Where it came from, for the item card ("Охота", "Кузня" ...).</summary>
        public string Source = "";

        [Newtonsoft.Json.JsonIgnore]
        public bool Issued => Id == 0;

        [Newtonsoft.Json.JsonIgnore]
        public ItemSlot Slot => ItemKinds.SlotOf(Kind);
    }

    /// <summary>An item being forged (one at a time, like the workshop of Hustle Castle).</summary>
    [Serializable]
    public sealed class CraftJob
    {
        public ItemKind Kind;
        public ItemRarity Rarity;
        public int Level = 1;
        public string Set = "";
        public long Start;
        public long End;
        public int Seed;
    }

    [Serializable]
    public sealed class GearState
    {
        public List<GearItem> Items = new List<GearItem>();
        public int NextItemId = 1;
        /// <summary>Обломки: from dismantling, spent on forging rare and epic items.</summary>
        public long Scrap;
        public CraftJob Craft;
        public long DismantleDay = -1;
        public int Dismantled;
        /// <summary>Last finds, newest last (the equipment window shows them).</summary>
        public List<string> News = new List<string>();
    }
}
