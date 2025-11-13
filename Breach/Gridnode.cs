using UnityEngine;
using System;

[CreateAssetMenu(menuName = "Breach/GridNode", fileName = "GridNode")]
public sealed class GridNode : ScriptableObject
{
    [Header("UI")]
    public string DisplayName;
    public Sprite Icon;

    [Header("Semantics")]
    public NodeType Type;     // "Class" yerine "Type"
    public NodeTag Tags;      // [Flags] ile çoklu etiket
}

public enum NodeType
{
    Fortress = 0,
    Sentry = 1,
    Opportunist = 2,
    Glass = 3,
    // ... genişletebilirsin
}

[Flags]
public enum NodeTag
{
    None = 0,
    Security = 1 << 0,
    Data = 1 << 1,
    Utility = 1 << 2,
    Trap = 1 << 3,
    Router = 1 << 4,
    // ... genişletebilirsin
}
