// [코드 지도] AssissZone: 제물 UI를 여는 빠른 상호작용 대상이다. 이름이 비슷한 AssimilatelZone의 시간당 동화율 증가와는 다른 역할이다. 어떤 도구든 허용하며 GameUIController가 있으면 전체 UI 상태 관리자를 통해 연다.
// 주요 함수: ResolveOfferingUI, Interact, OnOfferingCommitted
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/Zone/Offering/AssissZone.cs.md

using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class AssissZone : MonoBehaviour, IInteractable
{
    [SerializeField] private AssimilationOfferingUIController offeringUI;

    public string stableId;
    void OnOfferingCommitted()
    {
        var loop = SmithingLoop.Instance;
        if (loop == null || !loop.Initialized || string.IsNullOrEmpty(stableId)) return;
        var used = loop.Campaign.usedAltars ??= new System.Collections.Generic.List<string>();
        if (!used.Contains(stableId)) used.Add(stableId);
        offeringUI.OfferingCommitted -= OnOfferingCommitted;
        if (CampaignController.Instance != null) CampaignController.Instance.Commit();
        else loop.RequestAutosave();
    }
    private void Awake()
    {
        ConfigureQuickInteraction();
        ResolveOfferingUI();
    }

    public bool CanInteract()
    {
        var loop = SmithingLoop.Instance;
        return loop != null && loop.Initialized &&
            (string.IsNullOrEmpty(stableId) || loop.Campaign.usedAltars == null || !loop.Campaign.usedAltars.Contains(stableId)) &&
            ResolveOfferingUI() && !offeringUI.IsOpen;
    }

    public bool CanUseTool(ToolData toolData)
    {
        return true;
    }

    public void Interact(PlayerInteraction interactionContext)
    {
        if (!ResolveOfferingUI(interactionContext))
            return;

        if (!CanInteract()) return;
        // Only the currently opened altar listens. Clear old subscriptions on other altars.
        foreach (var altar in FindObjectsByType<AssissZone>(FindObjectsSortMode.None))
            if (altar.offeringUI != null) altar.offeringUI.OfferingCommitted -= altar.OnOfferingCommitted;
        offeringUI.OfferingCommitted += OnOfferingCommitted;
        if (GameUIController.Instance != null)
            GameUIController.Instance.OpenAssimilationOffering(offeringUI);
        else
            offeringUI.SetOpen(true);
    }

    private void Reset()
    {
        ConfigureQuickInteraction();
    }

    private void OnValidate()
    {
        ConfigureQuickInteraction();
    }

    private bool ResolveOfferingUI(PlayerInteraction interactionContext = null)
    {
        if (offeringUI != null)
            return true;

        if (interactionContext != null)
        {
            offeringUI = interactionContext.GetComponent<AssimilationOfferingUIController>();
            if (offeringUI == null)
                offeringUI = interactionContext.GetComponentInChildren<AssimilationOfferingUIController>(true);
        }

        if (offeringUI == null)
        {
            offeringUI = FindFirstObjectByType<AssimilationOfferingUIController>(
                FindObjectsInactive.Include);
        }

        return offeringUI != null;
    }

    private void ConfigureQuickInteraction()
    {
        int interactableLayer = LayerMask.NameToLayer("Interactable");
        if (interactableLayer >= 0)
            gameObject.layer = interactableLayer;

        BoxCollider2D trigger = GetComponent<BoxCollider2D>();
        if (trigger != null)
            trigger.isTrigger = true;
    }
}
