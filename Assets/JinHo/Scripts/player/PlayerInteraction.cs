using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private Transform interactionPoint;
    [SerializeField] private float interactionRange = 2f;
    [SerializeField] private float holdDuration = 1.5f;
    [SerializeField, Min(0f)] private float quickInteractionDelay = 0.2f;
    [SerializeField] private List<LayerMask> interactableLayers = new();

    private Camera mainCamera;
    private PlayerToolController toolController;
    private PlayerInputHandler inputHandler;
    private InventorySystem inventory;
    private InventoryUIController inventoryUI;

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

    private void Awake()
    {
        mainCamera = Camera.main;
        toolController = GetComponent<PlayerToolController>();
        inputHandler = GetComponent<PlayerInputHandler>();
        inventory = GetComponent<InventorySystem>();
        inventoryUI = GetComponent<InventoryUIController>();
        resourceLayer = LayerMask.NameToLayer("Resource");

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
        if (inventoryUI != null && inventoryUI.IsOpen)
        {
            CancelInteraction();
            inputHandler?.ConsumeAttackInput();
            return;
        }

        bool leftClickPressed = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool leftClickHeld = Mouse.current != null && Mouse.current.leftButton.isPressed;
        bool leftClickReleased = Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;

        if (inputHandler != null)
        {
            leftClickPressed = leftClickPressed || inputHandler.LeftClickPressedThisFrame;
            leftClickHeld = leftClickHeld || inputHandler.IsLeftClickHeld;
            leftClickReleased = leftClickReleased || inputHandler.LeftClickReleasedThisFrame;
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

        if (currentInteractableLayer == resourceLayer)
        {
            Debug.Log($"현재 도구 '{currentTool.ToolName}'로 자원 채집을 시작합니다.");

            holdTimer = 0f;
            isHolding = true;
            return;
        }

        quickInteractionTimer = 0f;
        isQuickInteractionPending = true;
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

        if (currentInteractable == null || !currentInteractable.CanInteract())
        {
            CancelInteraction();
            return;
        }

        Debug.Log(
            $"{currentInteractable.GetType().Name} 빠른 상호작용이 완료되었습니다."
        );

        currentInteractable.Interact(this);
        CancelInteraction();
    }

    private void CancelInteraction()
    {
        isHolding = false;
        isQuickInteractionPending = false;
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

        if (distance > interactionRange)
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

        Gizmos.DrawWireSphere(
            interactionPoint.position,
            interactionRange
        );
    }
}
