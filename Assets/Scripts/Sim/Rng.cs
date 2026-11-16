namespace Dungeon.Sim
{
    /// <summary>SplitMix64. Deterministic across platforms, unlike System.Random.</summary>
    public sealed class Rng
    {
        ulong _state;

        public Rng(ulong seed) { _state = seed; }

        public ulong NextU64()
        {
            ulong z = _state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>Uniform-ish int in [min, max). Modulo bias is irrelevant at these ranges.</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return minInclusive + (int)(NextU64() % (ulong)(maxExclusive - minInclusive));
        }
    }
}
