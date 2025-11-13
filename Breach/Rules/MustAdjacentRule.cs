using System;
using System.Collections.Generic;
using UnityEngine;

namespace BreachPuzzle
{
    [Serializable]
    public sealed class MustAdjacentRule : RuleBase
    {
        public override string DisplayName => "MustAdjacent (Host needs ≥1 Need)";

        [Header("Host en az bir Need komşusu ister")]
        [Min(0)] public int Host = 0;
        [Min(0)] public int Need = 1;
        public NeighborScope Scope = NeighborScope.Orthogonal;

        // Grid-only kaba filtre: Host yerleşirken en az bir "boş veya Need" komşu olmalı
        public override bool Allows(Grid g, int r, int c, int t)
        {
            int idx = g.Idx(r, c);
            if (g.Cells[idx] != -1)
            {
                return false;
            }

            if (t != Host)
            {
                return true;
            }

            bool anyFreeOrNeed = false;
            Grid.ForEachNeighbor(g.N, r, c, Scope, n =>
            {
                int tn = g.Cells[n];
                if (tn == Need || tn == -1)
                {
                    anyFreeOrNeed = true;
                }
            });
            return anyFreeOrNeed;
        }

        public override bool FinalCheck(Grid g)
        {
            for (int i = 0; i < g.Cells.Length; i++)
            {
                if (g.Cells[i] != Host)
                {
                    continue;
                }
                int r = i / g.N;
                int c = i % g.N;

                bool ok = false;
                Grid.ForEachNeighbor(g.N, r, c, Scope, n =>
                {
                    if (g.Cells[n] == Need)
                    {
                        ok = true;
                    }
                });
                if (!ok)
                {
                    return false;
                }
            }
            return true;
        }

        // === Erken hatalı, domain-aware lokal propagasyon ===
        public override bool Propagate(Grid g, int changedIdx, int[] domain, Queue<int> agenda)
        {
            bool changed = false;
            int maskNeed = 1 << Need;
            int maskHost = 1 << Host;

            // Tek yardımcı: bir Host hücresine bu kuralı uygula
            void EnforceForHost(int hostIdx)
            {
                int hr = hostIdx / g.N;
                int hc = hostIdx % g.N;

                bool hasPlacedNeed = false;
                int lastNeedCand = -1;
                int needCandCount = 0;

                Grid.ForEachNeighbor(g.N, hr, hc, Scope, n =>
                {
                    int tn = g.Cells[n];
                    if (tn == Need)
                    {
                        hasPlacedNeed = true;
                        return;
                    }
                    if (tn == -1)
                    {
                        if ((domain[n] & maskNeed) != 0)
                        {
                            needCandCount++;
                            lastNeedCand = n;
                        }
                    }
                });

                if (hasPlacedNeed)
                {
                    return; // zaten tatmin
                }

                if (needCandCount == 0)
                {
                    // Host tatmin edilemez
                    if (g.Cells[hostIdx] == Host)
                    {
                        // Erken çelişki: bu atama imkânsız
                        if (domain[hostIdx] != 0)
                        {
                            domain[hostIdx] = 0;
                            agenda.Enqueue(hostIdx);
                            changed = true;
                        }
                    }
                    else if (g.Cells[hostIdx] == -1 && (domain[hostIdx] & maskHost) != 0)
                    {
                        // Host adaylığını düş
                        int before = domain[hostIdx];
                        int after = before & ~maskHost;
                        if (after != before)
                        {
                            domain[hostIdx] = after;
                            agenda.Enqueue(hostIdx);
                            changed = true;
                        }
                    }
                    return;
                }

                if (needCandCount == 1 && lastNeedCand >= 0)
                {
                    // Tek komşu Need olmak zorunda → tekille
                    int before = domain[lastNeedCand];
                    if ((before & maskNeed) != 0 && before != maskNeed)
                    {
                        domain[lastNeedCand] = maskNeed;
                        agenda.Enqueue(lastNeedCand);
                        changed = true;
                    }
                }
            }

            // changedIdx bağlamında dar kapsamlı tetikleme:
            // 1) changedIdx Host ise veya domaininde Host biti varsa → kendisi için uygula
            int placed = g.Cells[changedIdx];
            if (placed == Host || ((placed == -1) && ((domain[changedIdx] & maskHost) != 0)))
            {
                EnforceForHost(changedIdx);
            }

            // 2) changedIdx Need ise (yerleşti/boşaldı) ya da domaininde Need biti varsa →
            //    komşu Host/Host-adayı hücreler için uygula
            bool isNeedRelevant = (placed == Need) || ((placed == -1) && ((domain[changedIdx] & maskNeed) != 0));
            if (isNeedRelevant)
            {
                g.RC(changedIdx, out int r, out int c);
                Grid.ForEachNeighbor(g.N, r, c, Scope, n =>
                {
                    if (g.Cells[n] == Host || (g.Cells[n] == -1 && (domain[n] & maskHost) != 0))
                    {
                        EnforceForHost(n);
                    }
                });
            }

            return changed;
        }
    }
}
