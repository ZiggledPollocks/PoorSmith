// [코드 지도] IInteractable: 상호작용 대상이 제공해야 하는 계약(interface)을 정의한다. 구현 본문이나 자체 상태는 없다. 같은 파일에 검 특수능력 계약 ISwordSpecialAbility도 있다. PlayerInteraction은 구체적인 나무·몬스터·포털 클래스 대신 이 계약을 통해 행동을 요청한다.
// 주요 함수: Activate, CanInteract, CanUseTool
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/Assets/IInteractable.cs.md

public interface IInteractable
{
    // 상호작용이 가능한지 확인
    bool CanInteract();

    bool CanUseTool(ToolData toolData);

    // 상호작용 완료 시 실행
    void Interact(PlayerInteraction interactionContext);
}

public interface ISwordSpecialAbility
{
    string SwordId { get; }

    void Activate(
        PlayerInteraction interactionContext,
        ToolData sword,
        IInteractable target);
}
