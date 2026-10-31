// [코드 지도] CampaignRules: 상점, 가방과 캠페인 경제에 적용할 공통 수치를 계산한다.
// 주요 함수: BagCapacityKg, BagPrice
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/Campaign/Core/CampaignRules.cs.md

using UnityEngine;
[CreateAssetMenu(menuName="PoorSmith/Campaign Rules")]
public sealed class CampaignRules : ScriptableObject
{
    [Header("User-approved provisional balance — shown in game")]
    public bool provisional=true;
    public int weeklyDebt=100, toolUpgradePrice=120, facilityUpgradePrice=200;
    public float bowChargeSeconds=.8f;
    [Range(0.01f, 1f)] public float forwardShieldDamageMultiplier=.30f;
    // Published bag data is shared by capacity calculation and shop presentation.
    public static float BagCapacityKg(int level)=>level<=1?30f:level==2?55f:85f;
    public static int BagPrice(int level)=>level==1?50:level==2?800:level==3?2500:0;
    public int arrowPrice=5, temporaryArmorDefense=4, temporaryCraftedPrice=30, temporaryEquipmentPrice=80, temporaryOtherPrice=15;
    public float temporaryVampireHeal=1, temporaryWindArmorBonus=.03f;
    public int temporaryBurnDamage=3, temporaryBurnSeconds=5;
}

