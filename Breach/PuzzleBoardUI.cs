using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PuzzleBoardUI : MonoBehaviour
{
    [Header("Root & Prefab")]
    public RectTransform GridRoot;
    public GameObject CellPrefab;   // İçinde Image olmalı

    [Header("Colors (fallback)")]
    public Color HiddenColor = new Color(1f, 1f, 1f, 0.92f);

    Color TypeColor(int t)
    {
        switch (t)
        {
            case 0: return new Color(0.55f, 0.74f, 0.47f);
            case 1: return new Color(0.46f, 0.67f, 0.79f);
            case 2: return new Color(0.80f, 0.63f, 0.45f);
            default: return new Color(0.80f, 0.52f, 0.53f);
        }
    }

    public void Render(PuzzleGenerator.PuzzleData data, NodeSubsetCache subset)
    {
        Transform root = GridRoot != null ? GridRoot : transform;

        for (int i = root.childCount - 1; i >= 0; --i)
        {
            var go = root.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
        }
        if (GridRoot == null || CellPrefab == null)
        {
            Debug.LogWarning("PuzzleBoardUI: GridRoot veya CellPrefab atanmadı.");
            return;
        }

        int n = data.N;
        int len = n * n;

        var images = new List<Image>(len);
        foreach (Transform t in GridRoot) DestroyImmediate(t.gameObject);
        for (int i = 0; i < len; i++)
        {
            var go = Instantiate(CellPrefab, GridRoot);
            var img = go.GetComponent<Image>();
            if (img == null) img = go.AddComponent<Image>();
            images.Add(img);
        }

        var gl = GridRoot.GetComponent<GridLayoutGroup>();
        if (gl != null)
        {
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gl.constraintCount = n;
        }

        bool hasSubset = subset.BitToNode != null && subset.BitToNode.Length == data.TypeCount;

        for (int i = 0; i < len; i++)
        {
            var img = images[i];

            if (!data.GivenMask[i])
            {
                img.sprite = null;
                img.color = HiddenColor;
                continue;
            }

            int t = data.Solution[i];

            if (hasSubset)
            {
                var node = subset.NodeOfBit(t);
                if (node != null && node.Icon != null)
                {
                    img.sprite = node.Icon;
                    img.color = Color.white; // ikon rengi
                    continue;
                }
            }

            // Fallback: renk
            img.sprite = null;
            img.color = TypeColor(t);
        }
    }
}
