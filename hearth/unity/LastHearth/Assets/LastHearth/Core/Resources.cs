using System;

namespace LastHearth.Core
{
    /// <summary>GDD 5. Silver and iron are the building materials.</summary>
    public enum ResourceType
    {
        Coal,
        Food,
        Iron,
        Silver,
        Essence,
        Sparks,
        /// <summary>From the evening Surge; spent on the Heart of the Hearth (ACTIVITIES.md, 2).</summary>
        Ember,
        /// <summary>From the monster Hunt; spent on hero skills and legendary gear (ACTIVITIES.md, 3).</summary>
        Cores,
        /// <summary>From expedition routes; spent on room specializations (ACTIVITIES.md, 4).</summary>
        Blueprints,
        /// <summary>From the arena and raids; spent in the Glory shop (MODES.md, 2-4).</summary>
        Glory,
    }

    /// <summary>Amounts of every resource. Whole numbers only, so the game stays deterministic.</summary>
    [Serializable]
    public sealed class ResourceBag
    {
        public const int Count = 10;

        public long[] Amounts = new long[Count];

        public long this[ResourceType type]
        {
            get { Fit(); return Amounts[(int)type]; }
            set { Fit(); Amounts[(int)type] = value; }
        }

        /// <summary>Saves made before a resource was added have a shorter array: grow it.</summary>
        void Fit()
        {
            if (Amounts == null)
                Amounts = new long[Count];
            else if (Amounts.Length < Count)
                Array.Resize(ref Amounts, Count);
        }

        public static ResourceBag Of(ResourceType type, long amount)
        {
            var bag = new ResourceBag();
            bag[type] = amount;
            return bag;
        }

        /// <summary>Materials are paid in silver and iron by the balance share (60% silver).</summary>
        public static ResourceBag Materials(long total, double silverShare)
        {
            var bag = new ResourceBag();
            long silver = (long)Math.Round(total * silverShare);
            bag[ResourceType.Silver] = silver;
            bag[ResourceType.Iron] = total - silver;
            return bag;
        }

        public bool Covers(ResourceBag cost)
        {
            Fit();
            cost.Fit();
            for (int i = 0; i < Count; i++)
            {
                if (Amounts[i] < cost.Amounts[i])
                    return false;
            }
            return true;
        }

        public void Subtract(ResourceBag cost)
        {
            if (!Covers(cost))
                throw new InvalidOperationException("Not enough resources");
            for (int i = 0; i < Count; i++)
                Amounts[i] -= cost.Amounts[i];
        }

        public void Add(ResourceBag other)
        {
            Fit();
            other.Fit();
            for (int i = 0; i < Count; i++)
                Amounts[i] += other.Amounts[i];
        }

        public bool IsEmpty()
        {
            Fit();
            for (int i = 0; i < Count; i++)
            {
                if (Amounts[i] != 0)
                    return false;
            }
            return true;
        }

        public ResourceBag Clone()
        {
            Fit();
            var bag = new ResourceBag();
            Array.Copy(Amounts, bag.Amounts, Count);
            return bag;
        }

        public override string ToString()
        {
            Fit();
            var parts = new System.Collections.Generic.List<string>();
            for (int i = 0; i < Count; i++)
            {
                if (Amounts[i] != 0)
                    parts.Add((ResourceType)i + " " + Amounts[i]);
            }
            return string.Join(", ", parts);
        }
    }
}
