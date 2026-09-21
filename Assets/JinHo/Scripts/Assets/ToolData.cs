using UnityEngine;

public enum ToolType
{
    Sword = 0,
    Axe = 1,
    Pickaxe = 2,
    Bow = 3
}

public enum SwordAttackStyle
{
    Thrust,
    Swing
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

    [Header("Sword Attack")]
    [SerializeField] private SwordAttackStyle swordAttackStyle = SwordAttackStyle.Swing;
    [Tooltip("Width of the forward capsule used by a Thrust attack.")]
    [SerializeField, Min(0.05f)] private float thrustWidth = 0.75f;
    [Tooltip("Angle of the player-centered arc used by a Swing attack.")]
    [SerializeField, Range(1f, 360f)] private float swingAngle = 110f;
    [SerializeField] private Sprite icon;

    public string ToolId => toolId;
    public string ToolName => toolName;
    public ToolType ToolType => toolType;
    public bool IsWeapon => toolType is ToolType.Sword or ToolType.Bow;
    public int Tier => Mathf.Max(1, tier);
    public int Damage => Mathf.Max(1, damage);
    public float Reach => Mathf.Max(0.01f, reach);
    public float AttackSpeed => Mathf.Max(0.01f, attackSpeed);
    public float AttackInterval => 1f / AttackSpeed;
    public SwordAttackStyle SwordAttackStyle => swordAttackStyle;
    public float ThrustWidth => Mathf.Max(0.05f, thrustWidth);
    public float SwingAngle => Mathf.Clamp(swingAngle, 1f, 360f);
    public Sprite Icon => icon;
}
