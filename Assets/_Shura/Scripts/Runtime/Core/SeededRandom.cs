namespace Shura
{
    /// <summary>
    /// SplitMix64. Every gameplay random decision goes through one seeded instance per run,
    /// so a run (and any bug in it) can be reproduced from its seed.
    /// </summary>
    public sealed class SeededRandom
    {
        ulong state;

        public SeededRandom(ulong seed)
        {
            state = seed;
        }

        public ulong NextULong()
        {
            unchecked
            {
                ulong z = state += 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// <summary>Uniform float in [0, 1).</summary>
        public float NextFloat()
        {
            return (NextULong() >> 40) / (float)(1UL << 24);
        }

        /// <summary>Uniform int in [minInclusive, maxExclusive).</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return minInclusive + (int)(NextULong() % (ulong)(maxExclusive - minInclusive));
        }

        public float Range(float min, float max)
        {
            return min + (max - min) * NextFloat();
        }
    }
}
