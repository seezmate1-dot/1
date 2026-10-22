using System.Collections.Generic;

namespace SunnyValley.Core.Meta
{
    /// <summary>
    /// Booster ids (GDD 4.5). The names match the match-3 core's booster kinds, so the UI can pass
    /// <c>kind.ToString()</c> straight to the meta: pre-level Line / Bomb / Rainbow, in-level
    /// Shovel / Rake / WateringCan / Shuffle.
    /// </summary>
    public static class Boosters
    {
        public const string Line = "Line";
        public const string Bomb = "Bomb";
        public const string Rainbow = "Rainbow";
        public const string Shovel = "Shovel";
        public const string Rake = "Rake";
        public const string WateringCan = "WateringCan";
        public const string Shuffle = "Shuffle";

        public static readonly string[] PreLevel = { Line, Bomb, Rainbow };
        public static readonly string[] InLevel = { Shovel, Rake, WateringCan, Shuffle };
        public static readonly string[] All = { Line, Bomb, Rainbow, Shovel, Rake, WateringCan, Shuffle };

        public static bool IsPreLevel(string id) => System.Array.IndexOf(PreLevel, id) >= 0;
        public static bool IsInLevel(string id) => System.Array.IndexOf(InLevel, id) >= 0;
        public static bool IsBooster(string id) => System.Array.IndexOf(All, id) >= 0;
    }

    /// <summary>Tools (GDD 7): the barn is upgraded with hammer, nails and paint, land is cleared with saw and axe.</summary>
    public static class Tools
    {
        public const string Hammer = "hammer";
        public const string Nails = "nails";
        public const string Paint = "paint";
        public const string Saw = "saw";
        public const string Axe = "axe";

        public static readonly string[] Barn = { Hammer, Nails, Paint };
        public static readonly string[] Land = { Saw, Axe };
        public static readonly string[] All = { Hammer, Nails, Paint, Saw, Axe };

        public static bool IsTool(string id) => System.Array.IndexOf(All, id) >= 0;
    }

    /// <summary>Kinds of goods kept in the barn.</summary>
    public static class ItemKinds
    {
        public const string Crop = "crop";
        public const string Fruit = "fruit";
        public const string Animal = "animal";
        public const string Feed = "feed";
        public const string Product = "product";
        /// <summary>Only grows on the match-3 board beds (tomato, eggplant, blueberry).</summary>
        public const string Match3 = "match3";

        public static readonly string[] All = { Crop, Fruit, Animal, Feed, Product, Match3 };
    }

    /// <summary>What a daily task counts (GDD 8).</summary>
    public static class DailyKinds
    {
        public const string WinLevels = "win_levels";
        public const string PlayLevels = "play_levels";
        public const string SpendStars = "spend_stars";
        public const string Harvest = "harvest";
        public const string Produce = "produce";
        public const string Orders = "orders";
        public const string FeedAnimals = "feed_animals";
        public const string UseBoosters = "use_boosters";

        public static readonly string[] All = { WinLevels, PlayLevels, SpendStars, Harvest, Produce, Orders, FeedAnimals, UseBoosters };
    }

    /// <summary>Kinds of town buildings (GDD 6).</summary>
    public static class TownKinds
    {
        public const string House = "house";
        public const string Service = "service";
        public const string Decoration = "decoration";
    }

    internal static class Dict
    {
        public static int Get(IDictionary<string, int> d, string key) =>
            d != null && key != null && d.TryGetValue(key, out int v) ? v : 0;

        public static void Add(IDictionary<string, int> d, string key, int amount)
        {
            int v = Get(d, key) + amount;
            if (v == 0)
                d.Remove(key);
            else
                d[key] = v;
        }

        public static int Sum(IDictionary<string, int> d)
        {
            int s = 0;
            if (d != null)
                foreach (var kv in d)
                    s += kv.Value;
            return s;
        }
    }
}
