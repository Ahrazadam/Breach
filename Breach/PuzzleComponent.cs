using BreachPuzzle;
using System.Collections.Generic;
using UnityEngine;

public sealed class PuzzleComponent : MonoBehaviour
{
    [Header("Board")]
    [Min(2)] public int N = 8;

    [Header("Reveal")]
    [Min(0)] public int TargetReveal = 32;
    public bool HardMode = false;

    [Header("Rules (SerializeReference)")]
    [SerializeReference] public List<RuleBase> rules = new List<RuleBase>();

    public CspLimits Limits = CspLimits.Default();

    [Header("Node Library (REQUIRED)")]
    public NodeLibrary Library;

    [Tooltip("Bu puzzle'da kullanılacak node altkümesi (REQUIRED, 1..32)")]
    public List<GridNode> ActiveNodes = new List<GridNode>();

    [Header("UI (optional)")]
    public PuzzleBoardUI TargetUI;

    [HideInInspector] public PuzzleGenerator.PuzzleData LastPuzzle;
    [HideInInspector] public bool HasPuzzle = false;

    // UI'ye geçirilecek; Library.BuildSubset ile doldurulur
    private NodeSubsetCache _activeSubset;
    public bool HasSubset => _activeSubset.BitCount > 0;

    public void GeneratePuzzle()
    {
        // Zorunlu kontroller
        if (Library == null)
        {
            Debug.LogError("[PuzzleComponent] NodeLibrary atanmalı.");
            return;
        }
        if (ActiveNodes == null || ActiveNodes.Count == 0)
        {
            Debug.LogError("[PuzzleComponent] ActiveNodes boş olamaz (en az 1).");
            return;
        }

        // Subset kur ve typeCount’u türet
        NodeSubsetCache subset;
        try
        {
            subset = Library.BuildSubset(ActiveNodes);
        }
        catch (System.SystemException ex)
        {
            Debug.LogError("[PuzzleComponent] BuildSubset hatası: " + ex.Message);
            return;
        }

        if (subset.BitCount <= 0 || subset.BitCount > 32)
        {
            Debug.LogError($"[PuzzleComponent] ActiveNodes sayısı 1..32 aralığında olmalı. Mevcut: {subset.BitCount}");
            return;
        }

        _activeSubset = subset;
        int typeCount = subset.BitCount;

        LastPuzzle = PuzzleGenerator.Generate(N, typeCount, rules, TargetReveal, HardMode, Limits);
        HasPuzzle = true;

        Debug.Log($"Puzzle generated. N={N}, types={typeCount}, revealed={CountRevealed(LastPuzzle.GivenMask)}");
    }

    public void ShowOnUI()
    {
        if (!HasPuzzle)
        {
            Debug.LogWarning("[PuzzleComponent] Gösterilecek puzzle yok. Önce Generate.");
            return;
        }
        if (TargetUI == null)
        {
            Debug.LogWarning("[PuzzleComponent] TargetUI atanmadı.");
            return;
        }

        if (HasSubset) { TargetUI.Render(LastPuzzle, _activeSubset); }
        else { TargetUI.Render(LastPuzzle, default); }
    }

    private int CountRevealed(bool[] givenMask)
    {
        int c = 0;
        if (givenMask != null) { for (int i = 0; i < givenMask.Length; i++) if (givenMask[i]) c++; }
        return c;
    }
}
