using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
/// <summary>
/// 변경 예정: Tool 스크립트 결합도 낮추기, 휠 inputsystem에 넣어서 처리하기
/// </summary>
public class PlayerInputHandler : MonoBehaviour
{
    private const string PlayerActionMapName = "Player";
    private const string SprintActionName = "Sprint";
    private const string JumpActionName = "Jump";
    private const string InteractActionName = "Interact";
    private const string RollActionName = "Roll";

    [SerializeField]
    private PlayerToolController toolController;

    public Vector2 MoveInput { get; private set; }
    public bool IsRunning { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool JumpHeld { get; private set; }
    public bool InventoryTogglePressed { get; private set; }
    public bool IsLeftClickHeld { get; private set; }
    public bool LeftClickPressedThisFrame { get; private set; }
    public bool LeftClickReleasedThisFrame { get; private set; }
    public bool InteractPressedThisFrame { get; private set; }
    public bool RollPressedThisFrame { get; private set; }
    public Vector2 RollPointerScreenPosition { get; private set; }
    public bool HasRollPointerPosition { get; private set; }

    private bool wasLeftClickPressed;
    private bool wasInteractPressed;
    private bool wasRollPressed;
    private PlayerInput playerInput;
    private InputAction sprintAction;
    private InputAction jumpAction;
    private InputAction moveAction;
    private InputAction interactAction;
    private InputAction rollAction;

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();

        if (toolController == null)
        {
            toolController =
                GetComponent<PlayerToolController>();
        }

        CacheInputActions();
    }

    private void Start()
    {
        if (playerInput != null && playerInput.currentActionMap == null)
        {
            playerInput.SwitchCurrentActionMap(PlayerActionMapName);
        }

        CacheInputActions();
    }

    private void Update()
    {
        if (GameUIController.BlocksGameplayInput)
        {
            ClearGameplayInput();
            return;
        }
        if (moveAction != null) ApplyMove(moveAction.ReadValue<Vector2>());
        if (sprintAction != null)
        {
            IsRunning = sprintAction.IsPressed();
        }

        if (jumpAction != null)
        {
            JumpHeld = jumpAction.IsPressed();

            if (jumpAction.WasPressedThisFrame())
            {
                JumpPressed = true;
            }
        }

        if (interactAction != null)
        {
            bool isInteractPressed = IsAnyBoundButtonPressed(interactAction);
            if (isInteractPressed && !wasInteractPressed)
                InteractPressedThisFrame = true;
            wasInteractPressed = isInteractPressed;
        }

        if (rollAction != null)
        {
            bool isRollPressed = IsAnyBoundButtonPressed(rollAction);
            if (isRollPressed && !wasRollPressed)
            {
                RollPressedThisFrame = true;
                HasRollPointerPosition = Mouse.current != null;
                if (HasRollPointerPosition)
                    RollPointerScreenPosition = Mouse.current.position.ReadValue();
            }
            wasRollPressed = isRollPressed;
        }

        if (toolController == null || Mouse.current == null)
            return;

        float scrollValue = Mouse.current.scroll.ReadValue().y;

        if (scrollValue > 0f)
        {
            toolController.SelectNextTool();
        }
        else if (scrollValue < 0f)
        {
            toolController.SelectPreviousTool();
        }
    }

    public void OnMove(InputValue value)
    {
        if (GameUIController.BlocksGameplayInput) return;
        ApplyMove(value.Get<Vector2>());
    }

    private void ApplyMove(Vector2 input)
    {
        float horizontalInput = Mathf.Clamp(input.x, -1f, 1f);

        // 2D Vector 입력은 W/S와 A/D를 함께 누르면 정규화되어 X값이 작아진다.
        // 이 게임은 수평 이동만 사용하므로 대각선 입력에서도 A/D의 세기를 복원한다.
        if (Mathf.Abs(horizontalInput) > 0.01f && Mathf.Abs(input.y) > 0.01f)
        {
            horizontalInput = Mathf.Sign(horizontalInput);
        }

        MoveInput = new Vector2(horizontalInput, 0f);
    }

    public void OnSprint(InputValue value)
    {
        if (GameUIController.BlocksGameplayInput) return;
        IsRunning = value.isPressed;
    }

    private void CacheInputActions()
    {
        if (playerInput == null || playerInput.actions == null)
            return;

        moveAction = playerInput.actions.FindAction("Player/Move", false);

        sprintAction = playerInput.actions.FindAction(
            $"{PlayerActionMapName}/{SprintActionName}",
            false
        );
        jumpAction = playerInput.actions.FindAction(
            $"{PlayerActionMapName}/{JumpActionName}",
            false
        );
        interactAction = playerInput.actions.FindAction(
            $"{PlayerActionMapName}/{InteractActionName}",
            false
        );
        rollAction = playerInput.actions.FindAction(
            $"{PlayerActionMapName}/{RollActionName}",
            false
        );

        if (sprintAction == null)
        {
            Debug.LogError(
                $"Input Actions에서 '{PlayerActionMapName}/{SprintActionName}' 액션을 찾을 수 없습니다.",
                this
            );
        }

        if (jumpAction == null)
        {
            Debug.LogError(
                $"Input Actions에서 '{PlayerActionMapName}/{JumpActionName}' 액션을 찾을 수 없습니다.",
                this
            );
        }
    }

    public void OnJump(InputValue value)
    {
        if (GameUIController.BlocksGameplayInput) return;
        JumpHeld = value.isPressed;

        if (value.isPressed)
        {
            JumpPressed = true;
        }
    }

    public void ConsumeJumpInput()
    {
        JumpPressed = false;
    }

    public void OnInventory(InputValue value)
    {
        if (value.isPressed)
            InventoryTogglePressed = true;
    }

    public bool ConsumeInventoryToggleInput()
    {
        if (!InventoryTogglePressed)
            return false;

        InventoryTogglePressed = false;
        return true;
    }

    private void OnDisable()
    {
        InventoryTogglePressed = false;
        ClearGameplayInput();
    }

    public void ClearGameplayInput()
    {
        MoveInput = Vector2.zero;
        IsRunning = false;
        JumpPressed = false;
        JumpHeld = false;
        InteractPressedThisFrame = false;
        wasInteractPressed = false;
        RollPressedThisFrame = false;
        HasRollPointerPosition = false;
        wasRollPressed = false;
        IsLeftClickHeld = false;
        LeftClickPressedThisFrame = false;
        LeftClickReleasedThisFrame = false;
        wasLeftClickPressed = false;
    }

    public void OnAttack(InputValue value)
    {
        if (GameUIController.BlocksGameplayInput) return;
        bool isPressed = value.isPressed;
        IsLeftClickHeld = isPressed;

        LeftClickPressedThisFrame = isPressed && !wasLeftClickPressed;
        LeftClickReleasedThisFrame = !isPressed && wasLeftClickPressed;

        wasLeftClickPressed = isPressed;
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

    private static bool IsAnyBoundButtonPressed(InputAction action)
    {
        foreach (InputControl control in action.controls)
        {
            if (control is ButtonControl button && button.isPressed)
                return true;
        }

        return false;
    }

    public void OnToolSlot1(InputValue value)
    {
        if (GameUIController.BlocksGameplayInput) return;
        if (value.isPressed && toolController != null)
            toolController.SelectToolType(ToolType.Sword);
    }

    public void OnToolSlot2(InputValue value)
    {
        if (GameUIController.BlocksGameplayInput) return;
        if (value.isPressed && toolController != null)
            toolController.SelectToolType(ToolType.Axe);
    }

    public void OnToolSlot3(InputValue value)
    {
        if (GameUIController.BlocksGameplayInput) return;
        if (value.isPressed && toolController != null)
            toolController.SelectToolType(ToolType.Pickaxe);
    }
}
