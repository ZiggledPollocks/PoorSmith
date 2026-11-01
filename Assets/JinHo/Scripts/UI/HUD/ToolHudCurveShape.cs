using UnityEngine;

/// <summary>Editable mesh data traced from the original HUD alpha; native Wood palette, no raster replacement.</summary>
public sealed class ToolHudCurveShape : ScriptableObject
{
    public Sprite originalReference;
    public Sprite woodReference;
    public int columns = 86;
    public int[] leftEdge, rightEdge;
    public Color[] palette;
    public float aspect = 1f;
}
