// [코드 지도] ToolData: 도구의 이름·종류·등급·공격 수치를 저장하는 ScriptableObject다. 인스턴스마다 행동하는 MonoBehaviour가 아니라 여러 컴포넌트가 공유하는 데이터 에셋이다. PlayerToolController는 선택을, PlayerInteraction은 이 수치를 이용한 실제 행동을 담당한다.
// 주요 함수: ConfigureCrafted, ConfigureWeaponKind, ConfigureTier
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Gathering/Scripts/Data/ToolData.cs.md

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
    // One global pacing multiplier for combat weapons and gathering tools.
    public const float GlobalAttackRateMultiplier = 0.8f;
    [SerializeField] private string toolId;
    [SerializeField] private string toolName;
    [SerializeField] private ToolType toolType;
    [SerializeField, Min(1)] private int tier = 1;
    [SerializeField, Min(1)] private float damage = 10;
    [SerializeField, Min(0.01f)] private float reach = 2.25f;
    [Tooltip("Base attacks per second. Combat hammers also use Hammer Attack Rate Multiplier.")]
    [SerializeField, Min(0.01f)] private float attackSpeed = 2f;
    [SerializeField] private bool hammerWeapon;
    [SerializeField, Range(0.1f, 1f)] private float hammerAttackRateMultiplier = 0.75f;
    [SerializeField, Min(1f)] private float hammerKnockbackMultiplier = 1.25f;

    [Header("Sword Attack")]
    [SerializeField] private SwordAttackStyle swordAttackStyle = SwordAttackStyle.Swing;
    [Tooltip("Width of the forward capsule used by a Thrust attack.")]
    [SerializeField, Min(0.05f)] private float thrustWidth = 0.75f;
    [Tooltip("Angle of the player-centered arc used by a Swing attack.")]
    [SerializeField, Range(1f, 360f)] private float swingAngle = 110f;
    [SerializeField] private Sprite icon;

    public void ConfigureCrafted(string displayName,float baseDamage,float attacksPerSecond,float qualityMultiplier)
    {
        toolName=displayName;
        damage=Mathf.Max(1,CombatDamage.RoundHealth(baseDamage*qualityMultiplier));
        attackSpeed=Mathf.Max(.01f,attacksPerSecond);
    }
    public void ConfigureTier(int value){tier=Mathf.Clamp(value,1,3);}
    public void ConfigureCatalogIdentity(string catalogId){toolId=catalogId;}
    public void ConfigureIcon(Sprite itemIcon){icon=itemIcon;}
    public void ConfigureWeaponKind(bool bow,bool thrust,float angle=110,bool isHammerWeapon=false,float hammerRate=.75f)
    {toolType=bow?ToolType.Bow:ToolType.Sword;swordAttackStyle=thrust?SwordAttackStyle.Thrust:SwordAttackStyle.Swing;swingAngle=angle;hammerWeapon=!bow&&isHammerWeapon;hammerAttackRateMultiplier=Mathf.Clamp(hammerRate,.1f,1f);}
    public string ToolId => toolId;
    public string ToolName => toolName;
    public ToolType ToolType => toolType;
    public bool IsWeapon => toolType is ToolType.Sword or ToolType.Bow;
    public int Tier => Mathf.Max(1, tier);
    public float Damage => Mathf.Max(1, damage);
    public float Reach => Mathf.Max(0.01f, reach);
    public bool IsHammerWeapon => toolType==ToolType.Sword&&hammerWeapon;
    public float AttackAnimationMultiplier => GlobalAttackRateMultiplier *
        (IsHammerWeapon ? (hammerAttackRateMultiplier>0f?hammerAttackRateMultiplier:.75f) : 1f);
    public float KnockbackMultiplier => IsHammerWeapon ? (hammerKnockbackMultiplier>0f?hammerKnockbackMultiplier:1.25f) : 1f;
    public float BaseAttackSpeed => Mathf.Max(0.01f, attackSpeed);
    public float AttackSpeed => Mathf.Max(0.01f, BaseAttackSpeed*AttackAnimationMultiplier);
    public float AttackInterval => 1f / AttackSpeed;
    public SwordAttackStyle SwordAttackStyle => swordAttackStyle;
    public float ThrustWidth => Mathf.Max(0.05f, thrustWidth);
    public float SwingAngle => Mathf.Clamp(swingAngle, 1f, 360f);
    public Sprite Icon => icon;
}
