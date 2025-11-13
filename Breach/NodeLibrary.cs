using UnityEngine;
using System;
using System.Collections.Generic;
using BreachPuzzle;

[CreateAssetMenu(menuName = "Breach/NodeLibrary", fileName = "NodeLibrary")]
public sealed class NodeLibrary : ScriptableObject
{
    [Tooltip("Superset: Tüm GridNode'lar. Puzzle, buradan alt kümeyi seçecek.")]
    public List<GridNode> Nodes = new List<GridNode>();

    [Tooltip("Sıralamayı dondur. Yeni eklenenler sonuna gider.")]
    public bool FreezeMapping = false;

    [SerializeField] private List<GridNode> _mappingOrder = new List<GridNode>();

    [NonSerialized] public int SupersetCount;
    [NonSerialized] public GridNode[] SupersetOrder;

    private void OnEnable() { EnsureCache(); }
    private void OnValidate() { EnsureCache(); }

    private void EnsureCache()
    {
        if (SupersetOrder == null || SupersetOrder.Length == 0 || (Nodes != null && SupersetCount != Nodes.Count))
        {
            BuildCache();
        }
    }

    public void BuildCache()
    {
        if (Nodes == null) { Nodes = new List<GridNode>(); }
        var order = new List<GridNode>(Nodes);
        order.RemoveAll(x => x == null);

        if (FreezeMapping)
        {
            if (_mappingOrder == null) { _mappingOrder = new List<GridNode>(); }
            var keep = new List<GridNode>(_mappingOrder);
            keep.RemoveAll(x => x == null);

            foreach (var n in order) { if (!keep.Contains(n)) { keep.Add(n); } }
            _mappingOrder = keep;
            order = keep;
        }

        var seen = new HashSet<GridNode>();
        var final = new List<GridNode>();
        foreach (var n in order) { if (n != null && seen.Add(n)) { final.Add(n); } }

        SupersetOrder = final.ToArray();
        SupersetCount = SupersetOrder.Length;
    }

    /// <summary>Bu Library’den seçilen aktif altküme için 0..K-1 bit eşlemesi kurar.</summary>
    public NodeSubsetCache BuildSubset(IList<GridNode> activeNodes)
    {
        EnsureCache();

        if (activeNodes == null || activeNodes.Count == 0)
        {
            throw new Exception("NodeLibrary.BuildSubset: ActiveNodes boş.");
        }

        var uniq = new HashSet<GridNode>();
        var list = new List<GridNode>(activeNodes.Count);

        foreach (var n in activeNodes)
        {
            if (n == null) { continue; }
            if (Nodes == null || !Nodes.Contains(n))
            {
                throw new Exception($"NodeLibrary.BuildSubset: '{(n != null ? n.name : "NULL")}' bu Library'de yok.");
            }
            if (uniq.Add(n)) { list.Add(n); }
        }

        if (list.Count > 32)
        {
            throw new Exception($"NodeLibrary.BuildSubset: {list.Count} tip; 32'yi aşamaz.");
        }

        // Deterministik: Library sırasına göre sırala
        list.Sort((a, b) =>
        {
            int ia = Array.IndexOf(SupersetOrder, a);
            int ib = Array.IndexOf(SupersetOrder, b);
            if (ia < 0) ia = int.MaxValue;
            if (ib < 0) ib = int.MaxValue;
            return ia.CompareTo(ib);
        });

        var bitToNode = list.ToArray();
        var nodeToBit = new Dictionary<GridNode, int>(bitToNode.Length);
        for (int i = 0; i < bitToNode.Length; i++) { nodeToBit[bitToNode[i]] = i; }

        int bitCount = bitToNode.Length;
        int allMask = BitmaskUtil.FullMask(bitCount);

        var tagToMask = new Dictionary<NodeTag, int>();
        var typeToMask = new Dictionary<NodeType, int>();

        for (int i = 0; i < bitCount; i++)
        {
            var node = bitToNode[i];

            // Tag → mask
            foreach (NodeTag tag in Enum.GetValues(typeof(NodeTag)))
            {
                if (tag == NodeTag.None) { continue; }
                if (node.Tags.HasFlag(tag))
                {
                    tagToMask[tag] = (tagToMask.TryGetValue(tag, out int m) ? m : 0) | (1 << i);
                }
            }

            // Type → mask
            var t = node.Type;
            typeToMask[t] = (typeToMask.TryGetValue(t, out int tm) ? tm : 0) | (1 << i);
        }

        return new NodeSubsetCache
        {
            Library = this,
            BitCount = bitCount,
            AllMask = allMask,
            BitToNode = bitToNode,
            NodeToBit = nodeToBit,
            TagToMask = tagToMask,
            TypeToMask = typeToMask
        };
    }
}

public struct NodeSubsetCache
{
    public NodeLibrary Library;

    public int BitCount;
    public int AllMask;

    public GridNode[] BitToNode;                        // [bit] -> Node
    public Dictionary<GridNode, int> NodeToBit;         // Node -> bit
    public Dictionary<NodeTag, int> TagToMask;         // Tag  -> mask (subset)
    public Dictionary<NodeType, int> TypeToMask;        // Type -> mask (subset)

    public int BitOf(GridNode node)
    {
        if (node == null || NodeToBit == null) return -1;
        return NodeToBit.TryGetValue(node, out int b) ? b : -1;
    }

    public GridNode NodeOfBit(int bit)
    {
        if (BitToNode == null || bit < 0 || bit >= BitToNode.Length) return null;
        return BitToNode[bit];
    }
}
