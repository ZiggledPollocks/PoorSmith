using UnityEngine;
using UnityEngine.InputSystem;
/// <summary>
/// 변경 예정: Tool 스크립트 결합도 낮추기, 휠 inputsystem에 넣어서 처리하기
/// </summary>
public class PlayerInputHandler : MonoBehaviour
{
    private const string PlayerActionMapName = "Player";
    private const string SprintActionName = "Sprint";
    private const string JumpActionName = "Jump";

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

    private bool wasLeftClickPressed;
    private PlayerInput playerInput;
    private InputAction sprintAction;
    private InputAction jumpAction;

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
        Vector2 input = value.Get<Vector2>();
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
        IsRunning = value.isPressed;
    }

    private void CacheInputActions()
    {
        if (playerInput == null || playerInput.actions == null)
            return;

        sprintAction = playerInput.actions.FindAction(
            $"{PlayerActionMapName}/{SprintActionName}",
            false
        );
        jumpAction = playerInput.actions.FindAction(
            $"{PlayerActionMapName}/{JumpActionName}",
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
        MoveInput = Vector2.zero;
        IsRunning = false;
        JumpPressed = false;
        JumpHeld = false;
        InventoryTogglePressed = false;
        IsLeftClickHeld = false;
        LeftClickPressedThisFrame = false;
        LeftClickReleasedThisFrame = false;
        wasLeftClickPressed = false;
    }

    public void OnAttack(InputValue value)
    {
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

    public void OnToolSlot0(InputValue value)
    {
        if (value.isPressed)
        {
            toolController.SelectToolSlot(0);
        }
    }

    public void OnToolSlot1(InputValue value)
    {
        if (value.isPressed)
        {
            toolController.SelectToolSlot(1);
        }
    }

    public void OnToolSlot2(InputValue value)
    {
        if (value.isPressed)
        {
            toolController.SelectToolSlot(2);
        }
    }
}
