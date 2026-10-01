// [코드 지도] SmithyStation: 실내 상호작용 지점에서 해당 설비/보관함/침대 화면을 연다.
// 주요 함수: Awake, CanInteract, CanUseTool
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Crafting/Integration/SmithyStation.cs.md

using UnityEngine;
public enum SmithyStationKind { Rack,Chest,Bed,CraftingDoor }
[RequireComponent(typeof(BoxCollider2D))]
public sealed class SmithyStation : MonoBehaviour,IInteractable
{
    public SmithyStationKind kind;
    void Awake(){gameObject.layer=LayerMask.NameToLayer("Interactable");GetComponent<BoxCollider2D>().isTrigger=true;}
    public bool CanInteract()=>SmithyInterior.Instance?.Inside==true&&!GameUIController.BlocksGameplayInput&&CampaignController.Instance?.Travelling!=true&&!SmithingLoop.Instance.Transitioning;
    public bool CanUseTool(ToolData tool)=>true;
    public void Interact(PlayerInteraction player){if(CanInteract())SmithyInterior.Instance.Open(kind);}
}
