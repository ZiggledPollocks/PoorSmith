using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class PlayerAnimationController : MonoBehaviour
{
    private const string IdleState = "Idle";
    private const string RunState = "Run";
    private const string WalkState = "Walk";
    private const string JumpState = "jump";
    private const string FallState = "Fall";
    private const string RollState = "Dash NoDust";
    private const string ToolUseState = "Attack";
    private const string HurtState = "Hurt NoEffect";
    private const string DeathState = "Death NoEffect";

    [SerializeField, Min(0f)] private float movementThreshold = 0.05f;
    [SerializeField, Min(0f)] private float verticalThreshold = 0.1f;
    [SerializeField, Min(0f)] private float transitionDuration = 0.05f;
    [SerializeField, Range(1f, 3f)] private float attackAnimationSpeed = 1.3f;
    [SerializeField] private SpriteRenderer handRenderer;

    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D body;
    private PlayerMovement movement;
    private PlayerInputHandler input;
    private FieldPlatformDropController fieldPlatform;
    private PlayerAssimilate assimilation;
    private string currentState;
    private bool toolUseActive;
    private bool toolUseStatePending;
    private bool hurtActive;
    private bool isDead;
    private bool deathAnimationComplete;

    public bool IsDeathAnimationComplete => deathAnimationComplete;

    public void Configure(
        RuntimeAnimatorController controller,
        PlayerMovement playerMovement,
        PlayerInputHandler inputHandler)
    {
        movement = playerMovement;
        input = inputHandler;
        EnsureReferences();

        if (animator == null)
        {
            Debug.LogError("Animator must be authored on the player before Play.", this);
            enabled = false;
            return;
        }

        if (controller != null && animator.runtimeAnimatorController != controller)
        {
            animator.runtimeAnimatorController = controller;
            currentState = null;
        }

        if (!isDead)
        {
            PlayState(IdleState, true);
        }
    }

    private void Awake()
    {
        EnsureReferences();
    }

    private void OnEnable()
    {
        EnsureReferences();
        if (assimilation != null)
        {
            assimilation.Damaged -= HandleDamaged;
            assimilation.Damaged += HandleDamaged;
            assimilation.Died -= HandleDeath;
            assimilation.Died += HandleDeath;
            isDead = assimilation.IsDead;
        }
    }

    private void Start()
    {
        if (isDead)
        {
            HandleDeath();
        }
    }

    private void OnDisable()
    {
        if (assimilation != null)
        {
            assimilation.Damaged -= HandleDamaged;
            assimilation.Died -= HandleDeath;
        }
    }

    private void Update()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return;
        }

        if (isDead)
        {
            AnimatorStateInfo deathState = animator.GetCurrentAnimatorStateInfo(0);
            if (deathState.IsName(DeathState) && deathState.normalizedTime >= 1f)
            {
                deathAnimationComplete = true;
                animator.speed = 0f;
            }

            return;
        }

        if (hurtActive)
        {
            AnimatorStateInfo hurtState = animator.GetCurrentAnimatorStateInfo(0);
            if (hurtState.IsName(HurtState) && hurtState.normalizedTime < 1f)
            {
                return;
            }

            hurtActive = false;
        }

        UpdateFacing();

        if (movement != null && movement.IsRolling)
        {
            if (toolUseActive)
                animator.speed = 1f;
            toolUseActive = false;
            toolUseStatePending = false;
            PlayState(RollState);
            return;
        }

        if (toolUseActive)
        {
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.IsName(ToolUseState))
            {
                toolUseStatePending = false;
                if (state.normalizedTime < 1f)
                    return;
            }
            else if (toolUseStatePending)
            {
                // Animator.Play is evaluated after Update. Keep the action for
                // that first evaluation instead of replacing it with Idle.
                toolUseStatePending = false;
                return;
            }

            toolUseActive = false;
            animator.speed = 1f;
        }

        bool grounded = movement == null || movement.IsGroundedForAnimation;
        if (!grounded && body != null)
        {
            PlayState(body.linearVelocity.y > verticalThreshold ? JumpState : FallState);
            return;
        }

        float horizontalSpeed = movement != null && movement.IsMovedOnlyByExternalWind
            ? 0f
            : body != null ? Mathf.Abs(body.linearVelocity.x) : 0f;
        if (fieldPlatform != null && fieldPlatform.IsStandingOnPlatform)
        {
            bool hasMoveInput = input != null && Mathf.Abs(input.MoveInput.x) > 0.01f;
            PlayState(!hasMoveInput ? IdleState : input.IsRunning ? RunState : WalkState);
            return;
        }
        PlayState(horizontalSpeed > movementThreshold ? RunState : IdleState);
    }

    public void PlayToolUse(Vector2 direction)
    {
        if (isDead || animator == null || animator.runtimeAnimatorController == null)
        {
            return;
        }

        if (Mathf.Abs(direction.x) > 0.01f)
        {
            SetFacing(direction.x < 0f);
        }

        toolUseActive = true;
        toolUseStatePending = true;
        animator.speed = attackAnimationSpeed;
        PlayState(ToolUseState, true);
    }

    private void UpdateFacing()
    {
        if (handRenderer != null && spriteRenderer != null)
            handRenderer.flipX = spriteRenderer.flipX;

        if (spriteRenderer == null || toolUseActive)
        {
            return;
        }

        float directionX = input != null ? input.MoveInput.x : 0f;
        if (Mathf.Abs(directionX) <= 0.01f &&
            movement != null && movement.IsMovedOnlyByExternalWind)
        {
            return;
        }

        if (Mathf.Abs(directionX) <= 0.01f && body != null)
        {
            directionX = body.linearVelocity.x;
        }

        if (Mathf.Abs(directionX) > 0.01f)
        {
            SetFacing(directionX < 0f);
        }
    }

    private void SetFacing(bool faceLeft)
    {
        spriteRenderer.flipX = faceLeft;
        if (handRenderer != null)
            handRenderer.flipX = faceLeft;
    }

    private void HandleDeath()
    {
        isDead = true;
        deathAnimationComplete = false;
        toolUseActive = false;
        toolUseStatePending = false;
        hurtActive = false;
        if (animator != null)
        {
            animator.speed = 1f;
        }
        PlayState(DeathState, true);
    }

    private void HandleDamaged(float damage)
    {
        if (damage <= 0 || isDead)
            return;

        Vector2 impactPoint = body != null
            ? body.worldCenterOfMass
            : (Vector2)transform.position;
        CombatHitFeedback2D.Play(gameObject, impactPoint);

        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        toolUseActive = false;
        toolUseStatePending = false;
        hurtActive = true;
        animator.speed = 1f;
        PlayState(HurtState, true);
    }

    private void PlayState(string stateName, bool restart = false)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return;
        }

        if (!restart && currentState == stateName)
        {
            return;
        }

        currentState = stateName;
        if (restart || transitionDuration <= 0f)
        {
            animator.Play(stateName, 0, 0f);
        }
        else
        {
            animator.CrossFadeInFixedTime(stateName, transitionDuration, 0);
        }
    }

    private void EnsureReferences()
    {
        spriteRenderer ??= GetComponent<SpriteRenderer>();
        if (handRenderer == null)
        {
            Transform hand = transform.Find("PlayerHand");
            if (hand != null)
                handRenderer = hand.GetComponent<SpriteRenderer>();
        }
        body ??= GetComponent<Rigidbody2D>();
        movement ??= GetComponent<PlayerMovement>();
        input ??= GetComponent<PlayerInputHandler>();
        fieldPlatform ??= GetComponent<FieldPlatformDropController>();

        PlayerAssimilate currentAssimilation = GetComponent<PlayerAssimilate>();
        if (assimilation != currentAssimilation)
        {
            if (assimilation != null)
            {
                assimilation.Damaged -= HandleDamaged;
                assimilation.Died -= HandleDeath;
            }

            assimilation = currentAssimilation;
            if (isActiveAndEnabled && assimilation != null)
            {
                assimilation.Damaged -= HandleDamaged;
                assimilation.Damaged += HandleDamaged;
                assimilation.Died -= HandleDeath;
                assimilation.Died += HandleDeath;
            }
        }

        animator ??= GetComponent<Animator>();
        if (animator != null)
        {
            animator.applyRootMotion = false;
        }
    }
}
