using UnityEngine;

public sealed class PuzzleLauncher : MonoBehaviour
{
    public PuzzleComponent Component;

    [ContextMenu("Generate")]
    public void Generate()
    {
        if (Component == null) { return; }
        Component.GeneratePuzzle();
    }

    [ContextMenu("Show")]
    public void Show()
    {
        if (Component == null) { return; }
        Component.ShowOnUI();
    }
}
