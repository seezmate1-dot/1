using System.Collections.Generic;
using System.Text;

namespace SunnyValley.Core.Meta
{
    /// <summary>
    /// A bundle of things the player gets: currencies, goods for the barn, tools, boosters and
    /// time-limited infinite lives. Used both in data (zone rewards, level-up gifts) and at runtime.
    /// </summary>
    public sealed class Reward
    {
        public int Coins;
        public int Stars;
        public int Crystals;
        public int Xp;
        public int Lives;
        public int InfiniteLivesMinutes;
        /// <summary>Goods for the barn (overflow is sold automatically).</summary>
        public SortedDictionary<string, int> Items = new SortedDictionary<string, int>();
        public SortedDictionary<string, int> Tools = new SortedDictionary<string, int>();
        public SortedDictionary<string, int> Boosters = new SortedDictionary<string, int>();

        public bool IsEmpty =>
            Coins == 0 && Stars == 0 && Crystals == 0 && Xp == 0 && Lives == 0 && InfiniteLivesMinutes == 0 &&
            Count(Items) == 0 && Count(Tools) == 0 && Count(Boosters) == 0;

        static int Count(SortedDictionary<string, int> d) => d == null ? 0 : d.Count;

        public Reward Clone()
        {
            var r = new Reward
            {
                Coins = Coins, Stars = Stars, Crystals = Crystals, Xp = Xp, Lives = Lives,
                InfiniteLivesMinutes = InfiniteLivesMinutes,
            };
            Copy(Items, r.Items);
            Copy(Tools, r.Tools);
            Copy(Boosters, r.Boosters);
            return r;
        }

        /// <summary>Adds the other reward into this one.</summary>
        public Reward Add(Reward other)
        {
            if (other == null)
                return this;
            Coins += other.Coins;
            Stars += other.Stars;
            Crystals += other.Crystals;
            Xp += other.Xp;
            Lives += other.Lives;
            InfiniteLivesMinutes += other.InfiniteLivesMinutes;
            Copy(other.Items, Items ??= new SortedDictionary<string, int>());
            Copy(other.Tools, Tools ??= new SortedDictionary<string, int>());
            Copy(other.Boosters, Boosters ??= new SortedDictionary<string, int>());
            return this;
        }

        static void Copy(SortedDictionary<string, int> from, SortedDictionary<string, int> to)
        {
            if (from == null)
                return;
            foreach (var kv in from)
                Dict.Add(to, kv.Key, kv.Value);
        }

        /// <summary>Short Russian description for logs and the UI, e.g. "120 монет, 1 кристалл, Молоток ×1".</summary>
        public string Describe(MetaData data = null)
        {
            var sb = new StringBuilder();
            void Part(string s)
            {
                if (sb.Length > 0)
                    sb.Append(", ");
                sb.Append(s);
            }
            if (Coins != 0) Part(Texts.Plural(Coins, "монета", "монеты", "монет"));
            if (Stars != 0) Part(Texts.Plural(Stars, "звезда", "звезды", "звёзд"));
            if (Crystals != 0) Part(Texts.Plural(Crystals, "кристалл", "кристалла", "кристаллов"));
            if (Xp != 0) Part(Xp + " опыта");
            if (Lives != 0) Part(Texts.Plural(Lives, "жизнь", "жизни", "жизней"));
            if (InfiniteLivesMinutes != 0) Part("бесконечные жизни на " + Texts.Duration(InfiniteLivesMinutes * 60L));
            foreach (var d in new[] { Items, Tools, Boosters })
                if (d != null)
                    foreach (var kv in d)
                        Part((data != null ? data.NameOf(kv.Key) : kv.Key) + " ×" + kv.Value);
            return sb.ToString();
        }
    }
}
