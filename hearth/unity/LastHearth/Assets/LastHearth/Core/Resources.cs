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
    }

    /// <summary>Amounts of every resource. Whole numbers only, so the game stays deterministic.</summary>
    [Serializable]
    public sealed class ResourceBag
    {
        public const int Count = 6;

        public long[] Amounts = new long[Count];

        public long this[ResourceType type]
        {
            get => Amounts[(int)type];
            set => Amounts[(int)type] = value;
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
            for (int i = 0; i < Count; i++)
                Amounts[i] += other.Amounts[i];
        }

        public bool IsEmpty()
        {
            for (int i = 0; i < Count; i++)
            {
                if (Amounts[i] != 0)
                    return false;
            }
            return true;
        }

        public ResourceBag Clone()
        {
            var bag = new ResourceBag();
            Array.Copy(Amounts, bag.Amounts, Count);
            return bag;
        }

        public override string ToString()
        {
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
