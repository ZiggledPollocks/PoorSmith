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
    [SerializeField, Min(1)] private int damage = 10;
    [SerializeField, Min(0.01f)] private float reach = 2.25f;
    [Tooltip("Attacks per second. Used only when Tool Type is Sword.")]
    [SerializeField, Min(0.01f)] private float attackSpeed = 2f;
    [SerializeField] private Sprite icon;

    public string ToolId => toolId;
    public string ToolName => toolName;
    public ToolType ToolType => toolType;
    public int Tier => Mathf.Max(1, tier);
    public int Damage => Mathf.Max(1, damage);
    public float Reach => Mathf.Max(0.01f, reach);
    public float AttackSpeed => Mathf.Max(0.01f, attackSpeed);
    public float AttackInterval => 1f / AttackSpeed;
    public Sprite Icon => icon;
}
