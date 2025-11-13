using System;
using System.Collections.Generic;

namespace BreachPuzzle
{
    public static class BitmaskUtil
    {
        public static int FullMask(int bitCount)
        {
            if (bitCount <= 0) { return 0; }
            if (bitCount >= 32) { return -1; } // 32 bit full
            return (1 << bitCount) - 1;
        }

        public static int Popcount(int x)
        {
            int c = 0;
            while (x != 0) { x &= x - 1; c++; }
            return c;
        }

        /// <summary>Mask tek bit ise indexini; değilse -1 döner.</summary>
        public static int BitToIndex(int mask)
        {
            if (mask == 0) { return -1; }
            if ((mask & (mask - 1)) != 0) { return -1; }
            int idx = 0;
            while ((mask & 1) == 0) { mask >>= 1; idx++; }
            return idx;
        }

        public static List<int> BitsToList(int m)
        {
            var L = new List<int>(32);
            for (int t = 0; t < 32; t++)
            {
                if (((m >> t) & 1) == 1) { L.Add(t); }
            }
            return L;
        }

        public static bool HasBit(int mask, int bit) => ((mask >> bit) & 1) == 1;
        public static int SetBit(int mask, int bit) => mask | (1 << bit);
        public static int ClearBit(int mask, int bit) => mask & ~(1 << bit);

        public static void Shuffle<T>(IList<T> a, System.Random rnd)
        {
            for (int i = a.Count - 1; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                (a[i], a[j]) = (a[j], a[i]);
            }
        }
    }
}
