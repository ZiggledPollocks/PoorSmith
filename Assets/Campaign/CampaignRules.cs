using UnityEngine;
[CreateAssetMenu(menuName="PoorSmith/Campaign Rules")]
public sealed class CampaignRules : ScriptableObject
{
    [Header("User-approved provisional balance — shown in game")]
    public bool provisional=true;
    public int weeklyDebt=100, bagUpgradePrice=150, toolUpgradePrice=120, facilityUpgradePrice=200;
    public float bagBaseCapacity=100, capacityPerTier=50, bowChargeSeconds=.8f;
    public int arrowPrice=5, temporaryArmorDefense=4, temporaryCraftedPrice=30, temporaryEquipmentPrice=80, temporaryOtherPrice=15;
    public float temporaryVampireHeal=1, temporaryWindArmorBonus=.03f;
    public int temporaryBurnDamage=3, temporaryBurnSeconds=5;
}

