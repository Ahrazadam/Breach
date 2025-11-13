using BreachPuzzle;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static BreachPuzzle.BitmaskUtil;

public static class PuzzleGenerator
{
    // == Public data ==
    public struct PuzzleData
    {
        public int N;
        public int TypeCount;
        public int[] Solution;   // N*N
        public bool[] GivenMask; // N*N (true = açık/given)
    }

    // == İç durum ==
    private sealed class DomainState
    {
        public int[] Mask; // her hücre için bit-mask (1<<t)
    }

    private struct Placed
    {
        public int idx;
        public int t;
        public Placed(int i, int tt) { idx = i; t = tt; }
    }

    private static int s_lastManualIdx = -1;

    // == Yardımcılar (BitmaskUtil kullan) ==
    private static int BitToType(int m) => BitToIndex(m); // mask tekil ise index
    private static List<int> BitsToListCompat(int m) => BitsToList(m);

    /// Giriş doğrulama
    private static void ValidateInputs(int n, int typeCount, List<RuleBase> rules, ref CspLimits limits)
    {
        if (rules == null) { throw new ArgumentNullException(nameof(rules)); }
        if (limits == null) { limits = CspLimits.Default(); }
        foreach (var r in rules)
        {
            if (!r.ValidateConfig(n, out var err))
            {
                throw new Exception($"Rule '{r.DisplayName}' invalid config: {err}");
            }
        }
    }

    private static void CreateGridAndInitDomains(int n, int typeCount, List<RuleBase> rules, out BreachPuzzle.Grid g, out DomainState dom)
    {
        g = new BreachPuzzle.Grid(n, typeCount);
        dom = new DomainState { Mask = new int[g.Cells.Length] };
        InitDomains(g, rules, ref dom);
    }

    private static void InitDomains(BreachPuzzle.Grid g, List<RuleBase> rules, ref DomainState dom)
    {
        if (dom == null || dom.Mask == null || dom.Mask.Length != g.Cells.Length)
        {
            dom = new DomainState { Mask = new int[g.Cells.Length] };
        }
        for (int i = 0; i < g.Cells.Length; i++)
        {
            if (g.Cells[i] == -1) { dom.Mask[i] = FullMask(g.TypeCount); }
            else { dom.Mask[i] = (1 << g.Cells[i]); }
        }
    }

    private static int ComputeAllowsMask(BreachPuzzle.Grid g, List<RuleBase> rules, int r, int c)
    {
        int idx = g.Idx(r, c);
        if (g.Cells[idx] != -1)
        {
            int tPlaced = g.Cells[idx];
            return (1 << tPlaced);
        }
        int mask = FullMask(g.TypeCount);
        for (int i = 0; i < rules.Count; i++)
        {
            int m = 0;
            for (int t = 0; t < g.TypeCount; t++)
            {
                if (rules[i].Allows(g, r, c, t)) { m |= (1 << t); }
            }
            mask &= m;
            if (mask == 0) { break; }
        }
        return mask;
    }

    private static bool Propagate(BreachPuzzle.Grid g, List<RuleBase> rules, ref DomainState dom, List<Placed> frameTrail)
    {
        bool changed = true;
        var agenda = new Queue<int>();

        for (int i = 0; i < g.Cells.Length; i++) { agenda.Enqueue(i); }

        int guard = 0;
        while (changed)
        {
            if (++guard > 200000) { Debug.LogError("Propagate stall"); return false; }
            changed = false;

            while (agenda.Count > 0)
            {
                int idx = agenda.Dequeue();

                if (g.Cells[idx] != -1)
                {
                    int req = (1 << g.Cells[idx]);
                    if (dom.Mask[idx] != req)
                    {
                        dom.Mask[idx] = req;
                        changed = true;
                    }
                    continue;
                }

                g.RC(idx, out int r, out int c);

                int allows = ComputeAllowsMask(g, rules, r, c);
                int newMask = dom.Mask[idx] & allows;
                if (newMask == 0) { return false; }

                if (newMask != dom.Mask[idx])
                {
                    dom.Mask[idx] = newMask;
                    changed = true;

                    for (int cc = 0; cc < g.N; cc++) { if (cc != c) { agenda.Enqueue(g.Idx(r, cc)); } }
                    for (int rr = 0; rr < g.N; rr++) { if (rr != r) { agenda.Enqueue(g.Idx(rr, c)); } }
                }

                for (int ri = 0; ri < rules.Count; ri++)
                {
                    if (rules[ri].Propagate(g, idx, dom.Mask, agenda)) { changed = true; }
                }
            }

            for (int ri = 0; ri < rules.Count; ri++)
            {
                if (rules[ri].GlobalTighten(g, dom.Mask, agenda)) { changed = true; }
            }

            int placedThisRound = 0;

            for (int i = 0; i < g.Cells.Length; i++)
            {
                if (g.Cells[i] != -1) { continue; }

                int m = dom.Mask[i];
                if (m == 0) { return false; }
                if (Popcount(m) != 1) { continue; }

                int t = BitToType(m);
                g.RC(i, out int rr, out int cc);

                int now = ComputeAllowsMask(g, rules, rr, cc);
                if ((now & (1 << t)) == 0) { return false; }

                int tight = (now & m);
                if (tight == 0) { return false; }
                if (tight != m)
                {
                    dom.Mask[i] = tight;
                    agenda.Enqueue(i);
                    for (int c = 0; c < g.N; c++) { agenda.Enqueue(g.Idx(rr, c)); }
                    for (int r = 0; r < g.N; r++) { agenda.Enqueue(g.Idx(r, cc)); }
                    changed = true;
                    break;
                }

                g.Place(i, t);
                dom.Mask[i] = (1 << t);
                frameTrail.Add(new Placed(i, t));
                placedThisRound++;

                agenda.Enqueue(i);
                for (int c = 0; c < g.N; c++) { if (c != cc) { agenda.Enqueue(g.Idx(rr, c)); } }
                for (int r = 0; r < g.N; r++) { if (r != rr) { agenda.Enqueue(g.Idx(r, cc)); } }

                break;
            }

            if (placedThisRound > 0) { changed = true; }
        }

        return true;
    }

    private static bool RunFinalChecks(BreachPuzzle.Grid g, List<RuleBase> rules)
    {
        foreach (var r in rules) { if (!r.FinalCheck(g)) { return false; } }
        return true;
    }

    private static bool RunInitialPropagation(BreachPuzzle.Grid g, List<RuleBase> rules, DomainState dom, Stack<List<Placed>> frameStack)
    {
        frameStack.Push(new List<Placed>());
        return Propagate(g, rules, ref dom, frameStack.Peek());
    }

    private static bool PlaceForcedAndPropagate(BreachPuzzle.Grid g, List<RuleBase> rules, ref DomainState dom,
                                               int idx, List<Placed> frameTrail)
    {
        int tForced = BitToType(dom.Mask[idx]);

        g.RC(idx, out int rr, out int cc);
        int allow = ComputeAllowsMask(g, rules, rr, cc);
        if ((allow & (1 << tForced)) == 0)
        {
            Debug.LogError($"MASK_ALLOW_MISMATCH idx={idx} choice={tForced}");
            return false;
        }

        g.Place(idx, tForced);
        dom.Mask[idx] = (1 << tForced);
        frameTrail.Add(new Placed(idx, tForced));
        return Propagate(g, rules, ref dom, frameTrail);
    }

    private static bool PlaceGuessAndPropagate(BreachPuzzle.Grid g, List<RuleBase> rules, ref DomainState dom,
                                               int idx, int choice, Stack<List<Placed>> frameStack, Stack<int[]> domStack)
    {
        g.RC(idx, out int rr, out int cc);
        int allow = ComputeAllowsMask(g, rules, rr, cc);
        if ((allow & (1 << choice)) == 0)
        {
            Debug.LogError($"MASK_ALLOW_MISMATCH idx={idx} choice={choice}");
            return false;
        }

        domStack.Push((int[])dom.Mask.Clone());
        frameStack.Push(new List<Placed>());
        g.Place(idx, choice);
        dom.Mask[idx] = (1 << choice);
        frameStack.Peek().Add(new Placed(idx, choice));
        s_lastManualIdx = idx;
        return Propagate(g, rules, ref dom, frameStack.Peek());
    }

    private static bool UndoFrameOnly(BreachPuzzle.Grid g, ref DomainState dom,
                                      Stack<List<Placed>> frameStack, Stack<int[]> domStack)
    {
        if (frameStack.Count == 0 || domStack.Count == 0) { return false; }

        var frame = frameStack.Pop();
        var snap = domStack.Pop();
        for (int p = frame.Count - 1; p >= 0; p--)
        {
            var pl = frame[p];
            if (g.Cells[pl.idx] == pl.t) { g.Unplace(pl.idx, pl.t); }
        }
        dom.Mask = snap;
        return true;
    }

    private static bool PopDecision(
        BreachPuzzle.Grid g, List<RuleBase> rules, CspLimits limits,
        ref DomainState dom,
        Stack<(int idx, List<int> tried)> choiceStack,
        Stack<List<Placed>> frameStack, Stack<int[]> domStack,
        ref int idx, ref long backtracks)
    {
        while (true)
        {
            if (choiceStack.Count == 0) { return false; }

            if (!UndoFrameOnly(g, ref dom, frameStack, domStack)) { return false; }

            choiceStack.Pop();
            idx = (choiceStack.Count > 0) ? choiceStack.Peek().idx : 0;

            if (++backtracks > limits.LogicIterCap) { return false; }

            if (frameStack.Count > 0 && Propagate(g, rules, ref dom, frameStack.Peek()))
            {
                return true;
            }
        }
    }

    private static bool Solve(BreachPuzzle.Grid g, List<RuleBase> rules, CspLimits limits,
                              ref DomainState dom, System.Random rnd, out string error)
    {
        long backtracks = 0;
        int idx = 0;

        var choiceStack = new Stack<(int idx, List<int> tried)>();
        var frameStack = new Stack<List<Placed>>();
        var domStack = new Stack<int[]>();

        if (!RunInitialPropagation(g, rules, dom, frameStack))
        {
            error = "InitialPropagation";
            return false;
        }
        domStack.Push((int[])dom.Mask.Clone());

        while (true)
        {
            idx = NextIndexSmart(g, dom, choiceStack);
            if (idx == -1) { break; }

            int mask = dom.Mask[idx];
            if (mask == 0)
            {
                if (UndoFrameOnly(g, ref dom, frameStack, domStack))
                {
                    idx = choiceStack.Peek().idx;
                    continue;
                }
                if (!PopDecision(g, rules, limits, ref dom, choiceStack, frameStack, domStack, ref idx, ref backtracks))
                {
                    error = "Mask==0: no frame & cannot escalate (root/limit)";
                    return false;
                }
                continue;
            }

            if (Popcount(mask) == 1)
            {
                if (!PlaceForcedAndPropagate(g, rules, ref dom, idx, frameStack.Peek()))
                {
                    if (UndoFrameOnly(g, ref dom, frameStack, domStack))
                    {
                        if (choiceStack.Count == 0) { error = "Forced-fail at root"; return false; }
                        idx = choiceStack.Peek().idx;
                        continue;
                    }
                    if (!PopDecision(g, rules, limits, ref dom, choiceStack, frameStack, domStack, ref idx, ref backtracks))
                    {
                        error = "Forced-fail: no frame & cannot escalate (root/limit)";
                        return false;
                    }
                    continue;
                }
                continue;
            }

            var candidates = BitsToListCompat(mask);
            Shuffle(candidates, rnd);

            List<int> tried = (choiceStack.Count > 0 && choiceStack.Peek().idx == idx) ? choiceStack.Peek().tried : null;
            int choice = -1;
            for (int k = 0; k < candidates.Count; k++)
            {
                int t = candidates[k];
                if (tried == null || !tried.Contains(t)) { choice = t; break; }
            }

            if (choice == -1)
            {
                if (!PopDecision(g, rules, limits, ref dom, choiceStack, frameStack, domStack, ref idx, ref backtracks))
                {
                    error = "Aday kalmadı: root/limit";
                    return false;
                }
                continue;
            }

            if (tried == null || choiceStack.Peek().idx != idx) { choiceStack.Push((idx, new List<int>())); }
            choiceStack.Peek().tried.Add(choice);

            if (!PlaceGuessAndPropagate(g, rules, ref dom, idx, choice, frameStack, domStack))
            {
                if (UndoFrameOnly(g, ref dom, frameStack, domStack))
                {
                    idx = choiceStack.Peek().idx;
                    continue;
                }
                if (!PopDecision(g, rules, limits, ref dom, choiceStack, frameStack, domStack, ref idx, ref backtracks))
                {
                    error = "Guess-fail: no frame & cannot escalate (root/limit)";
                    return false;
                }
                continue;
            }
        }

        error = "No Error";
        return true;
    }

    private static PuzzleData BuildPartial(BreachPuzzle.Grid g, int n, int typeCount, string debug)
    {
        Debug.Log(debug + $" [PuzzleGenerator] Partial build: last manual cell index = {s_lastManualIdx}");

        var solution = (int[])g.Cells.Clone();
        var given = new bool[solution.Length];
        for (int i = 0; i < solution.Length; i++) { given[i] = solution[i] != -1; }
        return new PuzzleData { N = n, TypeCount = typeCount, Solution = solution, GivenMask = given };
    }

    private static bool[] BuildRevealMask(int[] solution, int n, int typeCount, List<RuleBase> rules, bool hardMode, int targetReveal)
    {
        int len = solution.Length;

        var g = new BreachPuzzle.Grid(n, typeCount);
        for (int i = 0; i < len; i++)
        {
            int t = solution[i];
            g.Cells[i] = t;
            g.RC(i, out int rr, out int cc);
            g.RowCount[rr, t]++;
            g.ColCount[cc, t]++;
        }

        var order = Enumerable.Range(0, len).ToList(); // deterministik
        int revealed = len;

        foreach (int idx in order)
        {
            if (revealed <= targetReveal) { break; }

            int tsol = solution[idx];
            if (g.Cells[idx] == -1) { continue; }

            g.Unplace(idx, tsol);
            g.RC(idx, out int r0, out int c0);

            bool locallyOk;
            if (!hardMode)
            {
                locallyOk = false;
                for (int ri = 0; ri < rules.Count; ri++)
                {
                    int m = RuleMaskForCell(g, rules[ri], r0, c0, typeCount);
                    if (Popcount(m) == 1 && BitToIndex(m) == tsol) { locallyOk = true; break; }
                }
            }
            else
            {
                int allowed = IntersectMaskForCell(g, rules, r0, c0, typeCount);
                locallyOk = (Popcount(allowed) == 1 && BitToIndex(allowed) == tsol);
            }

            if (locallyOk) { revealed--; }
            else { g.Place(idx, tsol); }
        }

        bool[] given = new bool[len];
        for (int i = 0; i < len; i++) { given[i] = (g.Cells[i] != -1); }
        return given;
    }

    private static int RuleMaskForCell(BreachPuzzle.Grid g, RuleBase rule, int r, int c, int typeCount)
    {
        int m = 0;
        for (int t = 0; t < typeCount; t++) { if (rule.Allows(g, r, c, t)) { m |= (1 << t); } }
        return m;
    }

    private static int IntersectMaskForCell(BreachPuzzle.Grid g, List<RuleBase> rules, int r, int c, int typeCount)
    {
        int allowed = FullMask(typeCount);
        for (int i = 0; i < rules.Count; i++)
        {
            allowed &= RuleMaskForCell(g, rules[i], r, c, typeCount);
            if (allowed == 0) { break; }
        }
        return allowed;
    }

    public static PuzzleData Generate(int n, int typeCount, List<RuleBase> rules,
                                      int targetReveal, bool hardMode, CspLimits limits)
    {
        s_lastManualIdx = -1;
        ValidateInputs(n, typeCount, rules, ref limits);

        CreateGridAndInitDomains(n, typeCount, rules, out var g, out var dom);
        var rnd = new System.Random();

        if (!Solve(g, rules, limits, ref dom, rnd, out string error))
        {
            return BuildPartial(g, n, typeCount, error);
        }

        if (!RunFinalChecks(g, rules))
        {
            return BuildPartial(g, n, typeCount, "FinalCheck");
        }

        var solution = (int[])g.Cells.Clone();
        var given = BuildRevealMask(solution, n, typeCount, rules, hardMode, targetReveal);
        return new PuzzleData { N = n, TypeCount = typeCount, Solution = solution, GivenMask = given };
    }

    private static int NextIndexSmart(BreachPuzzle.Grid g, DomainState dom,
                                      Stack<(int idx, List<int> tried)> choiceStack)
    {
        if (choiceStack != null && choiceStack.Count > 0)
        {
            int keep = choiceStack.Peek().idx;
            if (keep >= 0 && g.Cells[keep] == -1) { return keep; }
        }

        int mrvIdx = SelectMRVIndex(g, dom);
        if (mrvIdx != -1) { return mrvIdx; }
        return -1;
    }

    private static int UnfilledDegree(BreachPuzzle.Grid g, int idx)
    {
        g.RC(idx, out int r, out int c);
        int deg = 0;

        for (int cc = 0; cc < g.N; cc++) { if (cc == c) continue; int j = g.Idx(r, cc); if (g.Cells[j] == -1) deg++; }
        for (int rr = 0; rr < g.N; rr++) { if (rr == r) continue; int j = g.Idx(rr, c); if (g.Cells[j] == -1) deg++; }
        return deg;
    }

    private static int SelectMRVIndex(BreachPuzzle.Grid g, DomainState dom)
    {
        int bestIdx = -1, bestPop = int.MaxValue, bestDeg = -1;

        for (int i = 0; i < g.Cells.Length; i++)
        {
            if (g.Cells[i] != -1) { continue; }

            int m = dom.Mask[i];
            int pc = Popcount(m);
            if (pc == 0) { return i; }

            if (pc < bestPop)
            {
                bestPop = pc; bestIdx = i; bestDeg = UnfilledDegree(g, i);
            }
            else if (pc == bestPop)
            {
                int deg = UnfilledDegree(g, i);
                if (deg > bestDeg) { bestIdx = i; bestDeg = deg; }
                else if (deg == bestDeg && i < bestIdx) { bestIdx = i; }
            }
        }
        return bestIdx;
    }
}
