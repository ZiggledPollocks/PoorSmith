using UnityEngine;

public enum ToolType
{
    Sword,
    Axe,
    Pickaxe
}

[CreateAssetMenu(
    fileName = "NewTool",
    menuName = "Game/Tool"
)]
public class ToolData : ScriptableObject
{
    [SerializeField] private string toolId;
    [SerializeField] private string toolName;
    [SerializeField] private ToolType toolType;
    [SerializeField, Min(1)] private int tier = 1;
    [SerializeField] private Sprite icon;

    public string ToolId => toolId;
    public string ToolName => toolName;
    public ToolType ToolType => toolType;
    public int Tier => Mathf.Max(1, tier);
    public Sprite Icon => icon;
}
