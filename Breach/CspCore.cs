using System;
using UnityEngine;

namespace BreachPuzzle
{
    public enum NeighborScope
    {
        Orthogonal = 0,
        Diagonal = 1,
        All = 2
    }

    [Serializable]
    public abstract class RuleBase
    {
        public abstract string DisplayName { get; }

        public virtual bool ValidateConfig(int n, out string error)
        {
            error = null;
            return true;
        }

        public abstract bool Allows(Grid g, int r, int c, int t);

        public virtual bool FinalCheck(Grid gd)
        {
            return true;
        }

        public virtual bool Propagate(Grid g, int changedIdx, int[] domain, System.Collections.Generic.Queue<int> agenda)
        {
            return false;
        }

        public virtual bool GlobalTighten(Grid g, int[] domain, System.Collections.Generic.Queue<int> agenda)
        {
            return false;
        }

        public virtual void Bind(NodeSubsetCache subset)
        {
            // Varsayılan: no-op. Özel kural istersen subset.TagToMask / TypeToMask / BitOf(...) kullanır.
        }
    }

    public sealed class Grid
    {
        public readonly int N;
        public readonly int TypeCount;

        public readonly int[] Cells;     // -1 = boş, 0..TypeCount-1 = tip
        public readonly int[,] RowCount; // [row, type]
        public readonly int[,] ColCount; // [col, type]

        // Yeni: Library'den enjekte edilen metadata (solver kurallarına tip/tag bilgisi sağlar)
        public int[] NodeTypeOfBit;     // len = TypeCount
        public int[] NodeTagMaskOfBit;  // len = TypeCount (flags)

        public Grid(int n, int typeCount)
        {
            if (n <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(n));
            }
            if (typeCount <= 0 || typeCount > 32)
            {
                throw new ArgumentOutOfRangeException(nameof(typeCount));
            }

            N = n;
            TypeCount = typeCount;

            Cells = new int[N * N];
            for (int i = 0; i < Cells.Length; i++)
            {
                Cells[i] = -1;
            }

            RowCount = new int[N, TypeCount];
            ColCount = new int[N, TypeCount];

            // Metadata sonradan PuzzleGenerator tarafından doldurulacak.
            NodeTypeOfBit = null;
            NodeTagMaskOfBit = null;
        }

        [System.Diagnostics.DebuggerStepThrough]
        public int Idx(int r, int c)
        {
            return r * N + c;
        }

        [System.Diagnostics.DebuggerStepThrough]
        public void RC(int i, out int r, out int c)
        {
            r = i / N;
            c = i % N;
        }

        public bool Solved()
        {
            for (int i = 0; i < Cells.Length; i++)
            {
                if (Cells[i] == -1)
                {
                    return false;
                }
            }
            return true;
        }

        public void Place(int idx, int t)
        {
            if (idx < 0 || idx >= Cells.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(idx));
            }
            if (t < 0 || t >= TypeCount)
            {
                throw new ArgumentOutOfRangeException(nameof(t));
            }
            if (Cells[idx] != -1)
            {
                throw new InvalidOperationException("Cell already filled.");
            }

            RC(idx, out int r, out int c);
            Cells[idx] = t;
            RowCount[r, t]++;
            ColCount[c, t]++;
        }

        public void Unplace(int idx, int t)
        {
            if (idx < 0 || idx >= Cells.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(idx));
            }
            if (t < 0 || t >= TypeCount)
            {
                throw new ArgumentOutOfRangeException(nameof(t));
            }
            if (Cells[idx] != t)
            {
                throw new InvalidOperationException("Cell type mismatch on Unplace.");
            }

            RC(idx, out int r, out int c);
            Cells[idx] = -1;
            RowCount[r, t]--;
            ColCount[c, t]--;
        }

        public int GetRowBlanks(int r)
        {
            int blanks = 0;
            for (int c = 0; c < N; c++)
            {
                if (Cells[Idx(r, c)] == -1)
                {
                    blanks++;
                }
            }
            return blanks;
        }

        public int GetColBlanks(int c)
        {
            int blanks = 0;
            for (int r = 0; r < N; r++)
            {
                if (Cells[Idx(r, c)] == -1)
                {
                    blanks++;
                }
            }
            return blanks;
        }

        public static void ForEachNeighbor(int n, int r, int c, NeighborScope scope, Action<int> visit)
        {
            if (visit == null)
            {
                return;
            }

            if (scope == NeighborScope.Orthogonal || scope == NeighborScope.All)
            {
                if (r > 0) { visit((r - 1) * n + c); }
                if (r < n - 1) { visit((r + 1) * n + c); }
                if (c > 0) { visit(r * n + (c - 1)); }
                if (c < n - 1) { visit(r * n + (c + 1)); }
            }

            if (scope == NeighborScope.Diagonal || scope == NeighborScope.All)
            {
                if (r > 0 && c > 0) { visit((r - 1) * n + (c - 1)); }
                if (r > 0 && c < n - 1) { visit((r - 1) * n + (c + 1)); }
                if (r < n - 1 && c > 0) { visit((r + 1) * n + (c - 1)); }
                if (r < n - 1 && c < n - 1) { visit((r + 1) * n + (c + 1)); }
            }
        }
    }
}
