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
