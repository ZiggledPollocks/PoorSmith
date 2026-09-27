using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class AssissZone : MonoBehaviour, IInteractable
{
    [SerializeField] private AssimilationOfferingUIController offeringUI;

    public string stableId;
    void OnOfferingCommitted()
    {
        var c=CampaignController.Instance;if(c==null||!c.Ready||string.IsNullOrEmpty(stableId))return;
        if(!c.State.usedAltars.Contains(stableId))c.State.usedAltars.Add(stableId);
        offeringUI.OfferingCommitted-=OnOfferingCommitted;c.Commit();
    }
    private void Awake()
    {
        ConfigureQuickInteraction();
        ResolveOfferingUI();
    }

    public bool CanInteract()
    {
        return (CampaignController.Instance==null||!CampaignController.Instance.Ready||!CampaignController.Instance.State.usedAltars.Contains(stableId)) && ResolveOfferingUI() && !offeringUI.IsOpen;
    }

    public bool CanUseTool(ToolData toolData)
    {
        return true;
    }

    public void Interact(PlayerInteraction interactionContext)
    {
        if (!ResolveOfferingUI(interactionContext))
            return;

        if(!CanInteract())return;
        // Only the currently opened altar listens. Clear old subscriptions on other altars.
        foreach(var altar in FindObjectsByType<AssissZone>(FindObjectsSortMode.None))
            if(altar.offeringUI!=null)altar.offeringUI.OfferingCommitted-=altar.OnOfferingCommitted;
        offeringUI.OfferingCommitted+=OnOfferingCommitted;
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
