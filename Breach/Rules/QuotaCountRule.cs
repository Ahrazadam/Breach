using System;
using System.Collections.Generic;
using UnityEngine;

namespace BreachPuzzle
{
    public enum CountOp { None = 0, AtLeast = 1, AtMost = 2, Exact = 3 }

    [Serializable]
    public sealed class QuotaCountRule : RuleBase
    {
        public override string DisplayName => "Quota (Row/Col)";

        [Header("Tür sayısı (Grid.TypeCount ile tutarlı olmalı)")]
        [Min(1)] public int TypeCount = 4;

        [Header("Satır kotaları (uzunluk = TypeCount)")]
        public CountOp RowOp = CountOp.None;
        public int[] RowQuotas = new int[4];

        [Header("Sütun kotaları (uzunluk = TypeCount)")]
        public CountOp ColOp = CountOp.None;
        public int[] ColQuotas = new int[4];

        private int RowQuota(int t)
        {
            if (RowQuotas == null || RowQuotas.Length == 0) { return 0; }
            int tt = Mathf.Clamp(t, 0, RowQuotas.Length - 1);
            return RowQuotas[tt];
        }

        private int ColQuota(int t)
        {
            if (ColQuotas == null || ColQuotas.Length == 0) { return 0; }
            int tt = Mathf.Clamp(t, 0, ColQuotas.Length - 1);
            return ColQuotas[tt];
        }

        public override bool ValidateConfig(int n, out string error)
        {
            error = null;
            if (TypeCount <= 0)
            {
                error = "TypeCount must be > 0.";
                return false;
            }
            if (RowQuotas == null || RowQuotas.Length != TypeCount)
            {
                error = $"RowQuotas length must be {TypeCount}.";
                return false;
            }
            if (ColQuotas == null || ColQuotas.Length != TypeCount)
            {
                error = $"ColQuotas length must be {TypeCount}.";
                return false;
            }

            for (int t = 0; t < TypeCount; t++)
            {
                if (RowQuotas[t] < 0 || RowQuotas[t] > n) { error = $"Row quota[{t}] out of range 0..{n}"; return false; }
                if (ColQuotas[t] < 0 || ColQuotas[t] > n) { error = $"Col quota[{t}] out of range 0..{n}"; return false; }
            }

            int rowSum = 0; int colSum = 0;
            for (int t = 0; t < TypeCount; t++) { rowSum += RowQuotas[t]; colSum += ColQuotas[t]; }
            if (RowOp == CountOp.Exact && rowSum != n) { error = $"Row(Exact) sum must equal N ({rowSum}!={n})"; return false; }
            if (RowOp == CountOp.AtLeast && rowSum > n) { error = $"Row(AtLeast) sum must be ≤ N ({rowSum}>{n})"; return false; }
            if (RowOp == CountOp.AtMost && rowSum < n) { error = $"Row(AtMost) sum must be ≥ N ({rowSum}<{n})"; return false; }
            if (ColOp == CountOp.Exact && colSum != n) { error = $"Col(Exact) sum must equal N ({colSum}!={n})"; return false; }
            if (ColOp == CountOp.AtLeast && colSum > n) { error = $"Col(AtLeast) sum must be ≤ N ({colSum}>{n})"; return false; }
            if (ColOp == CountOp.AtMost && colSum < n) { error = $"Col(AtMost) sum must be ≥ N ({colSum}<{n})"; return false; }
            return true;
        }

        public override bool Allows(Grid g, int r, int c, int t)
        {
            int idx = g.Idx(r, c);
            if (g.Cells[idx] != -1) { return false; }

            // Üst sınır testleri (Exact/AtMost)
            if (RowOp == CountOp.Exact || RowOp == CountOp.AtMost)
            {
                int cap = RowQuota(t);
                if (g.RowCount[r, t] + 1 > cap) { return false; }
            }
            if (ColOp == CountOp.Exact || ColOp == CountOp.AtMost)
            {
                int cap = ColQuota(t);
                if (g.ColCount[c, t] + 1 > cap) { return false; }
            }

            // Minimum ihtiyaçlar (Exact/AtLeast): kaba ileri kontrol (feasibility)
            if (RowOp == CountOp.Exact || RowOp == CountOp.AtLeast)
            {
                int rowBlanksAfter = g.GetRowBlanks(r) - 1;
                int needSum = 0;
                int types = Mathf.Min(g.TypeCount, TypeCount);
                for (int u = 0; u < types; u++)
                {
                    int have = g.RowCount[r, u] + (u == t ? 1 : 0);
                    int min = Mathf.Max(0, RowQuota(u) - have);
                    needSum += min;
                }
                if (needSum > rowBlanksAfter) { return false; }
            }
            if (ColOp == CountOp.Exact || ColOp == CountOp.AtLeast)
            {
                int colBlanksAfter = g.GetColBlanks(c) - 1;
                int needSum = 0;
                int types = Mathf.Min(g.TypeCount, TypeCount);
                for (int u = 0; u < types; u++)
                {
                    int have = g.ColCount[c, u] + (u == t ? 1 : 0);
                    int min = Mathf.Max(0, ColQuota(u) - have);
                    needSum += min;
                }
                if (needSum > colBlanksAfter) { return false; }
            }

            return true;
        }

        public override bool FinalCheck(Grid g)
        {
            int types = Mathf.Min(g.TypeCount, TypeCount);

            // Satırlar
            for (int r = 0; r < g.N; r++)
            {
                for (int u = 0; u < types; u++)
                {
                    int have = g.RowCount[r, u];
                    int need = RowQuota(u);
                    switch (RowOp)
                    {
                        case CountOp.AtLeast: if (have < need) return false; break;
                        case CountOp.AtMost: if (have > need) return false; break;
                        case CountOp.Exact: if (have != need) return false; break;
                    }
                }
            }
            // Sütunlar
            for (int c = 0; c < g.N; c++)
            {
                for (int u = 0; u < types; u++)
                {
                    int have = g.ColCount[c, u];
                    int need = ColQuota(u);
                    switch (ColOp)
                    {
                        case CountOp.AtLeast: if (have < need) return false; break;
                        case CountOp.AtMost: if (have > need) return false; break;
                        case CountOp.Exact: if (have != need) return false; break;
                    }
                }
            }
            return true;
        }

        // === Kota odaklı global domain sıkılaştırma ===
        public override bool GlobalTighten(Grid g, int[] domain, Queue<int> agenda)
        {
            bool changed = false;
            int types = Mathf.Min(g.TypeCount, TypeCount);

            // bit silme
            void Drop(int idx, int u)
            {
                int before = domain[idx];
                int after = before & ~(1 << u);
                if (after != before)
                {
                    domain[idx] = after;
                    agenda.Enqueue(idx);
                    changed = true;
                }
            }

            // Aynı mantığı satır/sütun için tek fonksiyonda uygula
            void ProcessAxis(CountOp op, Func<int, int, int> quota, Func<int, int, int> count, Func<int, int, int> idxAt)
            {
                int N = g.N;

                for (int a = 0; a < N; a++)
                {
                    for (int u = 0; u < types; u++)
                    {
                        int cap = quota(a, u);   // RowQuota/ColQuota
                        int have = count(a, u);   // RowCount/ColCount

                        // AtMost/Exact: kota doldu → bu eksende u yasak
                        if ((op == CountOp.AtMost || op == CountOp.Exact) && have >= cap)
                        {
                            for (int b = 0; b < N; b++)
                            {
                                int idx = idxAt(a, b);
                                if (g.Cells[idx] == -1) { Drop(idx, u); }
                            }
                        }

                        // AtLeast/Exact: need>0 → need == bucketCount ise bucket İÇİNİ {u}’ya indir (tekilleştir)
                        if ((op == CountOp.AtLeast || op == CountOp.Exact))
                        {
                            int need = cap - have;
                            if (need > 0)
                            {
                                // bucket: domain'inde u bulunan boşlar (dom snapshot'ına göre)
                                var bucket = new List<int>();
                                for (int b = 0; b < N; b++)
                                {
                                    int idx = idxAt(a, b);
                                    if (g.Cells[idx] == -1 && (domain[idx] & (1 << u)) != 0)
                                    {
                                        bucket.Add(idx);
                                    }
                                }

                                if (bucket.Count <= need)
                                {
                                    // Bu hücreler u olmak ZORUNDA → mask'i {u}'ya indir
                                    int only = (1 << u);
                                    for (int k = 0; k < bucket.Count; k++)
                                    {
                                        int idx = bucket[k];
                                        int before = domain[idx];
                                        int after = before & only;
                                        if (after != before)
                                        {
                                            domain[idx] = after;
                                            agenda.Enqueue(idx);
                                            changed = true;
                                        }
                                    }
                                }
                                // bucket.Count < need ise gerçek çelişki, prune ile çözemeyiz;
                                // çelişkiyi solver propagate veya Allows/FinalCheck sırasında yakalayacağız.
                            }
                        }
                    }
                }
            }

            // Satırlar
            ProcessAxis(                
                op: RowOp,
                quota: (r, u) => RowQuota(u),
                count: (r, u) => g.RowCount[r, u],
                idxAt: (r, c) => g.Idx(r, c)
            );

            // Sütunlar
            ProcessAxis(                
                op: ColOp,
                quota: (c, u) => ColQuota(u),
                count: (c, u) => g.ColCount[c, u],
                idxAt: (r, c) => g.Idx(c, r)  // axis swap
            );

            return changed;
        }

    }
}
