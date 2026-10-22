namespace SunnyValley.Core.Meta
{
    /// <summary>
    /// Deterministic random numbers for the meta (tool drops, orders, daily tasks). SplitMix64:
    /// the whole generator is one ulong kept in the save, so a loaded game continues the same sequence.
    /// </summary>
    public static class MetaRng
    {
        public static ulong Next(ref ulong state)
        {
            state += 0x9E3779B97F4A7C15UL;
            ulong z = state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>0 .. max-1 (max must be positive).</summary>
        public static int Range(ref ulong state, int max)
        {
            if (max <= 1)
                return 0;
            return (int)(Next(ref state) % (ulong)max);
        }

        /// <summary>min .. max inclusive.</summary>
        public static int Range(ref ulong state, int min, int max) => min + Range(ref state, max - min + 1);

        /// <summary>True with the given chance in percent (0..100).</summary>
        public static bool Percent(ref ulong state, int percent) => percent > 0 && Range(ref state, 100) < percent;

        /// <summary>Index picked by integer weights; -1 if all weights are zero.</summary>
        public static int Weighted(ref ulong state, System.Collections.Generic.IReadOnlyList<int> weights)
        {
            int total = 0;
            for (int i = 0; i < weights.Count; i++)
                total += System.Math.Max(0, weights[i]);
            if (total <= 0)
                return -1;
            int roll = Range(ref state, total);
            for (int i = 0; i < weights.Count; i++)
            {
                roll -= System.Math.Max(0, weights[i]);
                if (roll < 0)
                    return i;
            }
            return weights.Count - 1;
        }
    }
}
