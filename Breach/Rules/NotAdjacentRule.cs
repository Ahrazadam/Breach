using System;
using System.Collections.Generic;
using UnityEngine;

namespace BreachPuzzle
{
    [Serializable]
    public sealed class NotAdjacentRule : RuleBase
    {
        public override string DisplayName => "NotAdjacent (A !~ B)";

        [Header("A ile B komşu olamaz")]
        [Min(0)] public int A = 0;
        [Min(0)] public int B = 1;
        public NeighborScope Scope = NeighborScope.Orthogonal;

        public override bool Allows(Grid g, int r, int c, int t)
        {
            int idx = g.Idx(r, c);
            if (g.Cells[idx] != -1)
            {
                return false;
            }

            if (t == A || t == B)
            {
                bool ok = true;
                Grid.ForEachNeighbor(g.N, r, c, Scope, n =>
                {
                    int tn = g.Cells[n];
                    if (tn == -1)
                    {
                        return;
                    }
                    if ((t == A && tn == B) || (t == B && tn == A))
                    {
                        ok = false;
                    }
                });
                if (!ok)
                {
                    return false;
                }
            }
            return true;
        }

        public override bool FinalCheck(Grid g)
        {
            for (int i = 0; i < g.Cells.Length; i++)
            {
                int t = g.Cells[i];
                if (t == -1)
                {
                    continue;
                }
                int r = i / g.N;
                int c = i % g.N;
                bool ok = true;
                Grid.ForEachNeighbor(g.N, r, c, Scope, n =>
                {
                    int tn = g.Cells[n];
                    if ((t == A && tn == B) || (t == B && tn == A))
                    {
                        ok = false;
                    }
                });
                if (!ok)
                {
                    return false;
                }
            }
            return true;
        }

        // === Domain tabanlı lokal propagasyon ===
        public override bool Propagate(Grid g, int changedIdx, int[] domain, Queue<int> agenda)
        {
            bool changed = false;
            g.RC(changedIdx, out int r, out int c);

            void PruneNeighbors(int forbid)
            {
                Grid.ForEachNeighbor(g.N, r, c, Scope, n =>
                {
                    if (g.Cells[n] != -1)
                    {
                        return;
                    }
                    int before = domain[n];
                    int after = before & ~(1 << forbid);
                    if (after != before)
                    {
                        domain[n] = after;
                        agenda.Enqueue(n);
                        changed = true;
                    }
                });
            }

            int placed = g.Cells[changedIdx];
            if (placed == A)
            {
                PruneNeighbors(B);
                return changed;
            }
            if (placed == B)
            {
                PruneNeighbors(A);
                return changed;
            }

            int m = domain[changedIdx];
            if (IsSingleton(m))
            {
                int t = BitToType(m);
                if (t == A)
                {
                    PruneNeighbors(B);
                }
                else if (t == B)
                {
                    PruneNeighbors(A);
                }
            }
            return changed;
        }

        private static bool IsSingleton(int mask)
        {
            return mask != 0 && (mask & (mask - 1)) == 0;
        }

        private static int BitToType(int mask)
        {
            for (int t = 0; t < 32; t++)
            {
                if (((mask >> t) & 1) == 1)
                {
                    return t;
                }
            }
            return -1;
        }
    }
}
