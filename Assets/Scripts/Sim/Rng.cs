namespace Dungeon.Sim
{
    /// <summary>Seeded random numbers (SplitMix64). Same seed, same numbers, on every machine.</summary>
    public sealed class Rng
    {
        ulong _state;

        /// <param name="seed">Starting state. The same seed always gives the same sequence.</param>
        public Rng(ulong seed) { _state = seed; }

        /// <returns>The next random 64-bit number.</returns>
        public ulong NextU64()
        {
            ulong z = _state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <param name="min">Lowest possible result.</param>
        /// <param name="maxExclusive">One above the highest possible result.</param>
        /// <returns>A random int from min up to (not including) maxExclusive, or min if the range is empty.</returns>
        public int Range(int min, int maxExclusive)
        {
            if (maxExclusive <= min) return min;
            return min + (int)(NextU64() % (ulong)(maxExclusive - min));
        }
    }
}
