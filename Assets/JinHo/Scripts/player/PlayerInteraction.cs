using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private Transform interactionPoint;
    [FormerlySerializedAs("interactionRange")]
    [SerializeField, Min(0.01f)] private float defaultInteractionRange = 2.25f;
    [SerializeField] private float holdDuration = 1.5f;
    [SerializeField, Min(0f)] private float quickInteractionDelay = 0.2f;
    [SerializeField] private List<LayerMask> interactableLayers = new();

    private Camera mainCamera;
    private PlayerToolController toolController;
    private PlayerInputHandler inputHandler;
    private InventorySystem inventory;
    private InventoryUIController inventoryUI;
    private QuickInteractionPromptUI quickInteractionPrompt;

    public InventorySystem Inventory => inventory;
    public ToolData CurrentTool =>
        toolController != null ? toolController.CurrentTool : null;

    private IInteractable currentInteractable;
    private int currentInteractableLayer = -1;
    private int resourceLayer = -1;

    private float holdTimer;
    private float quickInteractionTimer;
    private bool isHolding;
    private bool isQuickInteractionPending;
    private bool quickInteractionStartedByInteractAction;
    private IInteractable nearbyQuickInteractable;
    private float nextSwordAttackTime;
    private readonly List<ISwordSpecialAbility> swordSpecialAbilities = new();

    private void Awake()
    {
        mainCamera = Camera.main;
        toolController = GetComponent<PlayerToolController>();
        inputHandler = GetComponent<PlayerInputHandler>();
        inventory = GetComponent<InventorySystem>();
        inventoryUI = GetComponent<InventoryUIController>();
        resourceLayer = LayerMask.NameToLayer("Resource");
        quickInteractionPrompt = QuickInteractionPromptUI.Create(mainCamera);
        RefreshSwordSpecialAbilities();

        if (inventory == null)
        {
            inventory = GetComponentInParent<InventorySystem>();
        }

        if (resourceLayer < 0)
        {
            Debug.LogError("Resource Layer가 Project Settings에 등록되지 않았습니다.");
        }
    }

    private void Update()
    {
        if (GameUIController.BlocksGameplayInput || (inventoryUI != null && inventoryUI.IsOpen))
        {
            nearbyQuickInteractable = null;
            HideQuickInteractionPrompt();
            CancelInteraction();
            inputHandler?.ConsumeAttackInput();
            inputHandler?.ConsumeInteractInput();
            return;
        }

        UpdateNearbyQuickInteractable();

        bool leftClickPressed = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool leftClickHeld = Mouse.current != null && Mouse.current.leftButton.isPressed;
        bool leftClickReleased = Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;

        if (inputHandler != null)
        {
            leftClickPressed = leftClickPressed || inputHandler.LeftClickPressedThisFrame;
            leftClickHeld = leftClickHeld || inputHandler.IsLeftClickHeld;
            leftClickReleased = leftClickReleased || inputHandler.LeftClickReleasedThisFrame;
        }

        bool interactPressed = inputHandler != null && inputHandler.ConsumeInteractInput();

        if (interactPressed && !isHolding && !isQuickInteractionPending)
        {
            StartQuickInteraction(nearbyQuickInteractable);
        }

        if (leftClickPressed && !isHolding && !isQuickInteractionPending)
        {
            StartInteraction();
        }

        if (isHolding && leftClickHeld)
        {
            HoldInteraction();
        }

        if (isHolding && leftClickReleased)
        {
            CancelInteraction();
        }

        if (isQuickInteractionPending)
        {
            UpdateQuickInteraction();
        }

        if (inputHandler != null)
        {
            inputHandler.ConsumeAttackInput();
        }
    }

    private void StartInteraction()
    {
        currentInteractable = FindClickedInteractable(out currentInteractableLayer);

        if (currentInteractable == null)
        {
            Debug.Log("상호작용 가능한 물체가 없거나 범위를 벗어났습니다.");
            CancelInteraction();
            return;
        }

        ToolData currentTool = CurrentTool;

        if (!currentInteractable.CanUseTool(currentTool))
        {
            string toolName = currentTool != null
                ? currentTool.ToolName
                : "없음";

            Debug.Log($"현재 도구 '{toolName}'로는 이 상호작용을 할 수 없습니다.");

            CancelInteraction();
            return;
        }

        if (currentInteractable is IDamageable && !TryBeginSwordAttack(currentTool))
        {
            CancelInteraction();
            return;
        }

        if (currentInteractableLayer == resourceLayer)
        {
            Debug.Log($"현재 도구 '{currentTool.ToolName}'로 자원 채집을 시작합니다.");

            holdTimer = 0f;
            isHolding = true;
            return;
        }

        quickInteractionTimer = 0f;
        isQuickInteractionPending = true;
        quickInteractionStartedByInteractAction = false;
        HideQuickInteractionPrompt();
    }

    private void StartQuickInteraction(IInteractable interactable)
    {
        if (!CanUseFQuickInteraction(interactable))
            return;

        currentInteractable = interactable;
        currentInteractableLayer = GetInteractableLayer(interactable);
        quickInteractionTimer = 0f;
        isQuickInteractionPending = true;
        quickInteractionStartedByInteractAction = true;
        HideQuickInteractionPrompt();
    }

    private void HoldInteraction()
    {
        if (!isHolding)
            return;

        if (currentInteractable == null)
        {
            CancelInteraction();
            return;
        }

        if (!IsWithinInteractionRange(currentInteractable, GetCurrentToolReach()))
        {
            CancelInteraction();
            return;
        }

        holdTimer += Time.deltaTime;

        if (holdTimer < holdDuration)
            return;

        Debug.Log(
            $"{currentInteractable.GetType().Name} 자원 채집 상호작용이 완료되었습니다."
        );

        currentInteractable.Interact(this);

        if (currentInteractable is UnityEngine.Object interactableObject && interactableObject == null)
        {
            CancelInteraction();
            return;
        }

        if (!currentInteractable.CanInteract())
        {
            CancelInteraction();
            return;
        }

        holdTimer = 0f;
    }

    private void UpdateQuickInteraction()
    {
        quickInteractionTimer += Time.deltaTime;

        if (quickInteractionTimer < quickInteractionDelay)
            return;

        float requiredRange = quickInteractionStartedByInteractAction
            ? defaultInteractionRange
            : GetCurrentToolReach();

        if ((quickInteractionStartedByInteractAction && !CanUseFQuickInteraction(currentInteractable))
            || !CanUseQuickInteraction(currentInteractable)
            || !IsWithinInteractionRange(currentInteractable, requiredRange))
        {
            CancelInteraction();
            return;
        }

        Debug.Log(
            $"{currentInteractable.GetType().Name} 빠른 상호작용이 완료되었습니다."
        );

        if (currentInteractable is IDamageable)
            TryUseSwordSpecialAbility(CurrentTool, currentInteractable);

        currentInteractable.Interact(this);
        CancelInteraction();
    }

    private void CancelInteraction()
    {
        isHolding = false;
        isQuickInteractionPending = false;
        quickInteractionStartedByInteractAction = false;
        holdTimer = 0f;
        quickInteractionTimer = 0f;
        currentInteractable = null;
        currentInteractableLayer = -1;
    }

    private IInteractable FindClickedInteractable(out int interactableLayer)
    {
        interactableLayer = -1;

        Vector2 mouseScreenPosition =
            Mouse.current.position.ReadValue();

        Vector2 mouseWorldPosition =
            mainCamera.ScreenToWorldPoint(mouseScreenPosition);

        Collider2D hit = Physics2D.OverlapPoint(
            mouseWorldPosition,
            GetCombinedInteractableLayerMask()
        );

        if (hit == null)
            return null;

        IInteractable interactable =
            hit.GetComponentInParent<IInteractable>();

        if (interactable == null)
            return null;

        if (!interactable.CanInteract())
            return null;

        Vector2 closestPoint =
            hit.ClosestPoint(interactionPoint.position);

        float distance = Vector2.Distance(
            interactionPoint.position,
            closestPoint
        );

        if (distance > GetCurrentToolReach())
            return null;

        if (interactable is Component interactableComponent)
        {
            interactableLayer = interactableComponent.gameObject.layer;
        }
        else
        {
            interactableLayer = hit.gameObject.layer;
        }

        return interactable;
    }

    private void UpdateNearbyQuickInteractable()
    {
        if (isHolding || isQuickInteractionPending)
        {
            HideQuickInteractionPrompt();
            return;
        }

        nearbyQuickInteractable = FindNearestQuickInteractable();
        if (nearbyQuickInteractable is Component component)
        {
            if (quickInteractionPrompt != null)
                quickInteractionPrompt.Show(component);
        }
        else
            HideQuickInteractionPrompt();
    }

    private IInteractable FindNearestQuickInteractable()
    {
        Vector2 origin = interactionPoint != null
            ? interactionPoint.position
            : transform.position;
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            origin,
            defaultInteractionRange,
            GetCombinedInteractableLayerMask());

        IInteractable closestInteractable = null;
        float closestDistance = float.PositiveInfinity;

        foreach (Collider2D hit in hits)
        {
            IInteractable interactable = hit.GetComponentInParent<IInteractable>();
            if (!CanUseFQuickInteraction(interactable))
                continue;

            float distance = Vector2.Distance(origin, hit.ClosestPoint(origin));
            if (distance >= closestDistance)
                continue;

            closestDistance = distance;
            closestInteractable = interactable;
        }

        return closestInteractable;
    }

    private bool CanUseQuickInteraction(IInteractable interactable)
    {
        if (interactable == null)
            return false;

        if (interactable is Object unityObject && unityObject == null)
            return false;

        return GetInteractableLayer(interactable) != resourceLayer
            && interactable.CanInteract()
            && interactable.CanUseTool(CurrentTool);
    }

    private bool CanUseFQuickInteraction(IInteractable interactable)
    {
        if (!CanUseQuickInteraction(interactable))
            return false;

        // Tool-independent quick objects accept a null tool. They remain
        // available even while the player has a Sword selected. Objects that
        // specifically require a Sword stay mouse-only.
        return CurrentTool == null
            || CurrentTool.ToolType != ToolType.Sword
            || interactable.CanUseTool(null);
    }

    private bool IsWithinInteractionRange(IInteractable interactable, float range)
    {
        if (interactable is not Component component)
            return false;

        Vector2 origin = interactionPoint != null
            ? interactionPoint.position
            : transform.position;
        Collider2D[] colliders = component.GetComponentsInChildren<Collider2D>();

        foreach (Collider2D collider in colliders)
        {
            if (Vector2.Distance(origin, collider.ClosestPoint(origin)) <= range)
                return true;
        }

        return false;
    }

    private float GetCurrentToolReach()
    {
        ToolData tool = CurrentTool;
        return tool != null ? tool.Reach : defaultInteractionRange;
    }

    private bool TryBeginSwordAttack(ToolData tool)
    {
        if (tool == null || tool.ToolType != ToolType.Sword)
            return true;

        if (Time.time < nextSwordAttackTime)
            return false;

        nextSwordAttackTime = Time.time + tool.AttackInterval;
        return true;
    }

    public void RefreshSwordSpecialAbilities()
    {
        swordSpecialAbilities.Clear();

        MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is ISwordSpecialAbility ability)
                swordSpecialAbilities.Add(ability);
        }
    }

    public bool TryUseCurrentSwordSpecialAbility(IInteractable target)
    {
        return TryUseSwordSpecialAbility(CurrentTool, target);
    }

    private bool TryUseSwordSpecialAbility(ToolData sword, IInteractable target)
    {
        if (sword == null || sword.ToolType != ToolType.Sword ||
            string.IsNullOrWhiteSpace(sword.ToolId))
        {
            return false;
        }

        foreach (ISwordSpecialAbility ability in swordSpecialAbilities)
        {
            if (ability is Behaviour behaviour && !behaviour.isActiveAndEnabled)
                continue;

            if (!string.Equals(
                    ability.SwordId,
                    sword.ToolId,
                    System.StringComparison.Ordinal))
            {
                continue;
            }

            ability.Activate(this, sword, target);
            return true;
        }

        return false;
    }

    private static int GetInteractableLayer(IInteractable interactable)
    {
        return interactable is Component component
            ? component.gameObject.layer
            : -1;
    }

    private void HideQuickInteractionPrompt()
    {
        if (quickInteractionPrompt != null)
            quickInteractionPrompt.Hide();
    }

    private int GetCombinedInteractableLayerMask()
    {
        if (interactableLayers == null)
            return 0;

        int combinedMask = 0;

        foreach (LayerMask layerMask in interactableLayers)
        {
            combinedMask |= layerMask.value;
        }

        return combinedMask;
    }

    private void OnDrawGizmosSelected()
    {
        if (interactionPoint == null)
            return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(
            interactionPoint.position,
            defaultInteractionRange
        );

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(
            interactionPoint.position,
            GetCurrentToolReach()
        );
    }

    private void OnDisable()
    {
        nearbyQuickInteractable = null;
        HideQuickInteractionPrompt();
        CancelInteraction();
    }

    private void OnDestroy()
    {
        if (quickInteractionPrompt != null)
            Destroy(quickInteractionPrompt.gameObject);
    }
}
