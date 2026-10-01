// [코드 지도] FieldCavePassageInteractable: 동굴 통로 상호작용을 필드 이동 흐름에 연결한다.
// 주요 함수: Interact, CanInteract, CanUseTool
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/Campaign/Field/FieldCavePassageInteractable.cs.md

using UnityEngine;

/// <summary>An F-only passage on one side of the forest/cave entrance.</summary>
[RequireComponent(typeof(BoxCollider2D))]
public sealed class FieldCavePassageInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] CaveEntranceBackgroundTransition entrance;
    [SerializeField] bool enterCave;

    public bool CanInteract() => entrance != null && entrance.CanBeginFieldCrossing(enterCave);

    public bool CanUseTool(ToolData toolData) => true;

    public void Interact(PlayerInteraction interactionContext)
    {
        if (interactionContext != null && CanInteract())
            entrance.TryBeginFieldCrossing(enterCave);
    }
}
