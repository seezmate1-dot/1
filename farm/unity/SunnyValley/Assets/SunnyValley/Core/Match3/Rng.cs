using System;
using System.Collections.Generic;

namespace SunnyValley.Core.Match3
{
    /// <summary>
    /// Small deterministic PRNG (SplitMix64). Same seed gives the same sequence on every platform; the state
    /// is a single ulong, so the board clones and saves trivially.
    /// </summary>
    public sealed class Rng
    {
        public ulong State;

        public Rng(ulong seed)
        {
            State = seed ^ 0x9E3779B97F4A7C15UL;
        }

        public Rng(long seed) : this(unchecked((ulong)seed)) { }

        private Rng() { }

        public Rng Clone() => new Rng { State = State };

        public ulong NextULong()
        {
            unchecked
            {
                State += 0x9E3779B97F4A7C15UL;
                ulong z = State;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// <summary>Uniform integer in [0, maxExclusive).</summary>
        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            return (int)(NextULong() % (ulong)maxExclusive);
        }

        public int Range(int minInclusive, int maxExclusive) => minInclusive + Next(maxExclusive - minInclusive);

        /// <summary>Uniform double in [0, 1).</summary>
        public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));

        public bool Chance(double p) => NextDouble() < p;

        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
