using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>각 Player Input Action의 콜백에서 상태를 갱신하고 소비자가 읽도록 보관한다.</summary>
public class PlayerInputHandler : MonoBehaviour
{
    private const string PlayerActionMapName = "Player";

    [SerializeField] private PlayerToolController toolController;

    private Vector2 moveInput;
    private bool isRunning;
    private bool jumpHeld;

    public Vector2 MoveInput => GameUIController.BlocksGameplayInput ? Vector2.zero : moveInput;
    public bool IsRunning => !GameUIController.BlocksGameplayInput && isRunning;
    public bool JumpHeld => !GameUIController.BlocksGameplayInput && jumpHeld;
    public bool JumpPressed { get; private set; }
    public bool InventoryTogglePressed { get; private set; }
    public bool IsLeftClickHeld { get; private set; }
    public bool LeftClickPressedThisFrame { get; private set; }
    public bool LeftClickReleasedThisFrame { get; private set; }
    public bool InteractPressedThisFrame { get; private set; }
    public bool RollPressedThisFrame { get; private set; }
    public Vector2 RollPointerScreenPosition { get; private set; }
    public bool HasRollPointerPosition { get; private set; }

    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction sprintAction;
    private InputAction jumpAction;
    private InputAction interactAction;
    private InputAction rollAction;
    private InputAction attackAction;
    private InputAction inventoryAction;
    private InputAction scrollAction;
    private InputAction toolSlot1Action;
    private InputAction toolSlot2Action;
    private InputAction toolSlot3Action;
    private bool actionsSubscribed;

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        toolController ??= GetComponent<PlayerToolController>();

        // Send Messages 대신 각 InputAction의 C# 콜백을 사용한다.
        if (playerInput != null)
            playerInput.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;

    }

    private void OnEnable()
    {
        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();
        SubscribeActions();
    }

    private void Start()
    {
        if (playerInput != null && playerInput.currentActionMap == null)
            playerInput.SwitchCurrentActionMap(PlayerActionMapName);

        // PlayerInput이 OnEnable에서 액션 에셋을 복제했을 수도 있다.
        UnsubscribeActions();
        SubscribeActions();
    }

    private void OnDisable()
    {
        UnsubscribeActions();
        moveInput = Vector2.zero;
        isRunning = false;
        jumpHeld = false;
        InventoryTogglePressed = false;
        ClearGameplayInput();
    }

    private void CacheInputActions()
    {
        if (playerInput == null || playerInput.actions == null)
            return;

        InputActionAsset actions = playerInput.actions;
        moveAction = actions.FindAction("Player/Move", false);
        sprintAction = actions.FindAction("Player/Sprint", false);
        jumpAction = actions.FindAction("Player/Jump", false);
        interactAction = actions.FindAction("Player/Interact", false);
        rollAction = actions.FindAction("Player/Roll", false);
        attackAction = actions.FindAction("Player/Attack", false);
        inventoryAction = actions.FindAction("Player/Inventory", false);
        scrollAction = actions.FindAction("Player/ToolScroll", false);
        toolSlot1Action = actions.FindAction("Player/ToolSlot1", false);
        toolSlot2Action = actions.FindAction("Player/ToolSlot2", false);
        toolSlot3Action = actions.FindAction("Player/ToolSlot3", false);

        if (sprintAction == null)
            Debug.LogError("Input Actions에서 'Player/Sprint' 액션을 찾을 수 없습니다.", this);
        if (jumpAction == null)
            Debug.LogError("Input Actions에서 'Player/Jump' 액션을 찾을 수 없습니다.", this);
        if (scrollAction == null)
            Debug.LogError("Input Actions에서 'Player/ToolScroll' 액션을 찾을 수 없습니다.", this);
    }

    private void SubscribeActions()
    {
        if (actionsSubscribed || playerInput == null || playerInput.actions == null)
            return;

        CacheInputActions();
        if (moveAction != null) { moveAction.performed += OnMove; moveAction.canceled += OnMove; }
        if (sprintAction != null) { sprintAction.performed += OnSprint; sprintAction.canceled += OnSprint; }
        if (jumpAction != null) { jumpAction.performed += OnJump; jumpAction.canceled += OnJump; }
        if (interactAction != null) interactAction.started += OnInteract;
        if (rollAction != null) rollAction.performed += OnRoll;
        if (attackAction != null) { attackAction.performed += OnAttack; attackAction.canceled += OnAttack; }
        if (inventoryAction != null) inventoryAction.performed += OnInventory;
        if (scrollAction != null) scrollAction.performed += OnToolScroll;
        if (toolSlot1Action != null) toolSlot1Action.performed += OnToolSlot1;
        if (toolSlot2Action != null) toolSlot2Action.performed += OnToolSlot2;
        if (toolSlot3Action != null) toolSlot3Action.performed += OnToolSlot3;
        actionsSubscribed = true;
    }

    private void UnsubscribeActions()
    {
        if (!actionsSubscribed)
            return;

        if (moveAction != null) { moveAction.performed -= OnMove; moveAction.canceled -= OnMove; }
        if (sprintAction != null) { sprintAction.performed -= OnSprint; sprintAction.canceled -= OnSprint; }
        if (jumpAction != null) { jumpAction.performed -= OnJump; jumpAction.canceled -= OnJump; }
        if (interactAction != null) interactAction.started -= OnInteract;
        if (rollAction != null) rollAction.performed -= OnRoll;
        if (attackAction != null) { attackAction.performed -= OnAttack; attackAction.canceled -= OnAttack; }
        if (inventoryAction != null) inventoryAction.performed -= OnInventory;
        if (scrollAction != null) scrollAction.performed -= OnToolScroll;
        if (toolSlot1Action != null) toolSlot1Action.performed -= OnToolSlot1;
        if (toolSlot2Action != null) toolSlot2Action.performed -= OnToolSlot2;
        if (toolSlot3Action != null) toolSlot3Action.performed -= OnToolSlot3;
        actionsSubscribed = false;
    }

    private void OnMove(InputAction.CallbackContext context)
    {
        // UI 차단 중에도 실제 유지 중인 이동 값을 보관한다.
        ApplyMove(context.ReadValue<Vector2>());
    }

    private void OnSprint(InputAction.CallbackContext context)
    {
        isRunning = context.performed && context.ReadValueAsButton();
    }

    private void OnJump(InputAction.CallbackContext context)
    {
        if (context.canceled)
        {
            jumpHeld = false;
            return;
        }

        jumpHeld = context.ReadValueAsButton();
        if (jumpHeld && !GameUIController.BlocksGameplayInput)
            JumpPressed = true;
    }

    private void OnInteract(InputAction.CallbackContext context)
    {
        // Hold 완료를 기다리지 않고 누른 시점에 퀵 상호작용을 시작한다.
        if (!GameUIController.BlocksGameplayInput)
            InteractPressedThisFrame = true;
    }

    private void OnRoll(InputAction.CallbackContext context)
    {
        if (GameUIController.BlocksGameplayInput || !context.ReadValueAsButton())
            return;

        RollPressedThisFrame = true;
        HasRollPointerPosition = Mouse.current != null;
        if (HasRollPointerPosition)
            RollPointerScreenPosition = Mouse.current.position.ReadValue();
    }

    private void OnAttack(InputAction.CallbackContext context)
    {
        if (GameUIController.BlocksGameplayInput)
            return;

        if (context.canceled)
        {
            if (IsLeftClickHeld)
                LeftClickReleasedThisFrame = true;
            IsLeftClickHeld = false;
        }
        else if (context.ReadValueAsButton())
        {
            if (!IsLeftClickHeld)
                LeftClickPressedThisFrame = true;
            IsLeftClickHeld = true;
        }
    }

    private void OnInventory(InputAction.CallbackContext context)
    {
        // 인벤토리는 게임 플레이 입력을 막는 UI 안에서도 토글할 수 있다.
        if (context.ReadValueAsButton())
            InventoryTogglePressed = true;
    }

    private void OnToolScroll(InputAction.CallbackContext context)
    {
        if (GameUIController.BlocksGameplayInput || toolController == null)
            return;

        float scroll = context.ReadValue<float>();
        if (scroll > 0f)
            toolController.SelectNextTool();
        else if (scroll < 0f)
            toolController.SelectPreviousTool();
    }

    private void OnToolSlot1(InputAction.CallbackContext context)
    {
        if (!GameUIController.BlocksGameplayInput && context.ReadValueAsButton() && toolController != null)
            toolController.SelectToolType(ToolType.Sword);
    }

    private void OnToolSlot2(InputAction.CallbackContext context)
    {
        if (!GameUIController.BlocksGameplayInput && context.ReadValueAsButton() && toolController != null)
            toolController.SelectToolType(CampaignController.Instance!=null?ToolType.Pickaxe:ToolType.Axe);
    }

    private void OnToolSlot3(InputAction.CallbackContext context)
    {
        if (!GameUIController.BlocksGameplayInput && context.ReadValueAsButton() && toolController != null)
            toolController.SelectToolType(CampaignController.Instance!=null?ToolType.Axe:ToolType.Pickaxe);
    }

    private void ApplyMove(Vector2 input)
    {
        float horizontalInput = Mathf.Clamp(input.x, -1f, 1f);

        // 2D Vector 입력은 W/S와 A/D를 함께 누르면 정규화되어 X값이 작아진다.
        // 이 게임은 수평 이동만 사용하므로 대각선 입력에서도 A/D의 세기를 복원한다.
        if (Mathf.Abs(horizontalInput) > 0.01f && Mathf.Abs(input.y) > 0.01f)
            horizontalInput = Mathf.Sign(horizontalInput);

        moveInput = new Vector2(horizontalInput, 0f);
    }

    public void ConsumeJumpInput() => JumpPressed = false;

    public bool ConsumeInventoryToggleInput()
    {
        if (!InventoryTogglePressed)
            return false;
        InventoryTogglePressed = false;
        return true;
    }

    public void ClearGameplayInput()
    {
        // UI 전환에서는 출력만 막고 실제 유지 중인 키 값은 보존한다.
        // UI 밖에서 명시적으로 초기화할 때는 유지 상태까지 비운다.
        if (!GameUIController.BlocksGameplayInput)
        {
            moveInput = Vector2.zero;
            isRunning = false;
            jumpHeld = false;
        }
        JumpPressed = false;
        InteractPressedThisFrame = false;
        RollPressedThisFrame = false;
        HasRollPointerPosition = false;
        IsLeftClickHeld = false;
        LeftClickPressedThisFrame = false;
        LeftClickReleasedThisFrame = false;
    }

    public void ConsumeAttackInput()
    {
        LeftClickPressedThisFrame = false;
        LeftClickReleasedThisFrame = false;
    }

    public bool ConsumeInteractInput()
    {
        if (!InteractPressedThisFrame)
            return false;
        InteractPressedThisFrame = false;
        return true;
    }

    public bool ConsumeRollInput(out Vector2 pointerScreenPosition, out bool hasPointerPosition)
    {
        pointerScreenPosition = RollPointerScreenPosition;
        hasPointerPosition = HasRollPointerPosition;
        if (!RollPressedThisFrame)
            return false;
        RollPressedThisFrame = false;
        HasRollPointerPosition = false;
        return true;
    }
}
