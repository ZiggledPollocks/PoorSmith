using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class AssissZone : MonoBehaviour, IInteractable
{
    [SerializeField] private AssimilationOfferingUIController offeringUI;

    private void Awake()
    {
        ConfigureQuickInteraction();
        ResolveOfferingUI();
    }

    public bool CanInteract()
    {
        return ResolveOfferingUI() && !offeringUI.IsOpen;
    }

    public bool CanUseTool(ToolData toolData)
    {
        return true;
    }

    public void Interact(PlayerInteraction interactionContext)
    {
        if (!ResolveOfferingUI(interactionContext))
            return;

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
