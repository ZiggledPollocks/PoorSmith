// [코드 지도] FieldSceneReturnGate: 필드에서 마을로 돌아가는 진입 조건과 요청을 처리한다.
// 주요 함수: CanInteract, Interact, CanUseTool
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/Campaign/Travel/FieldSceneReturnGate.cs.md

using UnityEngine;

/// <summary>F interaction at the forest boundary; town travel keeps its Yes/No prompt.</summary>
[RequireComponent(typeof(BoxCollider2D))]
public sealed class FieldSceneReturnGate : MonoBehaviour, IInteractable
{
    public bool CanInteract() =>
        !FieldSceneTravel.Busy &&
        SmithingLoop.Instance?.Initialized == true &&
        !GameUIController.BlocksGameplayInput &&
        FieldTravelPrompt.Active != null &&
        !FieldTravelPrompt.Active.IsOpen;

    public bool CanUseTool(ToolData toolData) => true;

    public void Interact(PlayerInteraction interactionContext)
    {
        if (interactionContext != null && CanInteract())
            FieldTravelPrompt.Active.Open(FieldSceneTravel.BeginToTown);
    }
}
