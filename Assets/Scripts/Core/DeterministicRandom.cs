using System;

[Serializable]
public sealed class DeterministicRandom
{
    private const ulong DefaultState = 0x9E3779B97F4A7C15UL;

    private ulong state;

    public int Seed { get; private set; }
    public ulong State => state;

    public DeterministicRandom(int seed)
    {
        Reset(seed);
    }

    public void Reset(int seed)
    {
        Seed = seed;
        state = MixSeed(unchecked((ulong)(uint)seed));
        if (state == 0) state = DefaultState;
    }

    public void Restore(int seed, ulong restoredState)
    {
        Seed = seed;
        state = restoredState == 0 ? DefaultState : restoredState;
    }

    public int Range(int minimumInclusive, int maximumExclusive)
    {
        if (maximumExclusive <= minimumInclusive) return minimumInclusive;
        ulong range = (ulong)(maximumExclusive - minimumInclusive);
        return minimumInclusive + (int)(NextUInt64() % range);
    }

    public void Shuffle<T>(System.Collections.Generic.IList<T> values)
    {
        if (values == null) return;
        for (int i = values.Count - 1; i > 0; i--)
        {
            int other = Range(0, i + 1);
            if (other == i) continue;
            T value = values[i];
            values[i] = values[other];
            values[other] = value;
        }
    }

    private ulong NextUInt64()
    {
        ulong value = state;
        value ^= value >> 12;
        value ^= value << 25;
        value ^= value >> 27;
        state = value;
        return value * 2685821657736338717UL;
    }

    private static ulong MixSeed(ulong value)
    {
        value += DefaultState;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }
}
