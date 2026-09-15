using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Mulberry32 and UTF-16 FNV-1a, matching web src/utils/random.ts.
    /// Persist State after draws, then construct from that value to resume.
    /// </summary>
    public sealed class SeededRandom
    {
        public uint State { get; private set; }

        public SeededRandom(uint seed) { State = seed; }

        public double NextDouble()
        {
            unchecked
            {
                State += 0x6d2b79f5u;
                var value = State;
                value = (value ^ (value >> 15)) * (value | 1u);
                value ^= value + ((value ^ (value >> 7)) * (value | 61u));
                return (value ^ (value >> 14)) / 4294967296d;
            }
        }

        public int NextInt(int exclusiveMaximum)
        {
            if (exclusiveMaximum <= 0) throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum));
            return (int)Math.Floor(NextDouble() * exclusiveMaximum);
        }

        public T[] Shuffle<T>(IEnumerable<T> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            var result = items.ToArray();
            for (var index = result.Length - 1; index > 0; index--)
            {
                var swapIndex = NextInt(index + 1);
                var item = result[index]; result[index] = result[swapIndex]; result[swapIndex] = item;
            }
            return result;
        }

        public static uint HashSeed(string value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            unchecked
            {
                var hash = 2166136261u;
                foreach (var codeUnit in value) hash = (hash ^ codeUnit) * 16777619u;
                return hash;
            }
        }
    }
}
