// [코드 지도] PlayerInteraction: 마우스 공격·채집과 근처 F 상호작용을 연결한다. 대상은 IInteractable 계약으로 다루고, 실제 피해/드롭/포털 효과는 대상 구현체가 맡는다. Resource 레이어는 홀드 반복, 다른 일반 대상은 짧은 지연, Sword는 즉시 범위 공격이라는 세 경로가 있다. 도구는 PlayerToolController에서 읽고 애니메이션과 프롬프트는 별도 컴포넌트에 요청한다.
// 주요 함수: PerformSwordAttack, StartInteraction, Update
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/player/Interaction/PlayerInteraction.cs.md

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
    private PlayerAnimationController animationController;
    private InventorySystem inventory;
    private InventoryUIController inventoryUI;
    private QuickInteractionPromptUI quickInteractionPrompt;

    public InventorySystem Inventory => inventory;
    public ToolData CurrentTool =>
        attackToolSnapshot != null ? attackToolSnapshot : toolController != null ? toolController.CurrentTool : null;
    private ToolData attackToolSnapshot;
    public void ApplyMonsterKnockback(CharacterPhysics2D target)
    {
        target?.ApplyKnockbackFrom(transform.position, CurrentTool?.KnockbackMultiplier ?? 1f);
    }

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

    // 핵심 분기: inventory == null 판정.
    // 상태 변경: mainCamera 갱신.
    // 다음 연결: QuickInteractionPromptUI.Create(UnityEngine.Camera) 호출.
    private void Awake()
    {
        mainCamera = Camera.main;
        toolController = GetComponent<PlayerToolController>();
        inputHandler = GetComponent<PlayerInputHandler>();
        animationController = GetComponent<PlayerAnimationController>();
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

    // 핵심 분기: GameUIController.BlocksGameplayInput || (inventoryUI != null && inventoryUI.IsOpen) 판정.
    // 상태 변경: nearbyQuickInteractable 갱신.
    // 다음 연결: PlayerInteraction.HideQuickInteractionPrompt() 호출.
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

        bool leftClickPressed = inputHandler != null && inputHandler.LeftClickPressedThisFrame;
        bool leftClickHeld = inputHandler != null && inputHandler.IsLeftClickHeld;
        bool leftClickReleased = inputHandler != null && inputHandler.LeftClickReleasedThisFrame;

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

    // 핵심 분기: GetComponent<CampaignCombat>() is CampaignCombat campaignCombat && campaignCombat.TryBeginToolAction(currentT… 판정.
    // 상태 변경: currentInteractable 갱신.
    // 다음 연결: CampaignCombat.TryBeginToolAction(ToolData) 호출.
    private void StartInteraction()
    {
        ToolData currentTool = CurrentTool;
        if (GetComponent<CampaignCombat>() is CampaignCombat campaignCombat && campaignCombat.TryBeginToolAction(currentTool)) return;
        if (currentTool != null && currentTool.ToolType == ToolType.Sword)
        {
            PerformSwordAttack(currentTool);
            return;
        }

        currentInteractable = FindClickedInteractable(out currentInteractableLayer);

        if (currentInteractable == null)
        {
            Debug.Log("상호작용 가능한 물체가 없거나 범위를 벗어났습니다.");
            CancelInteraction();
            return;
        }

        if (!CanUseLeftClickInteraction(currentInteractable, currentTool))
        {
            if (currentInteractableLayer == resourceLayer && currentInteractable is Component resource)
            {
                if (currentTool != null) PlayToolUseAnimation(currentInteractable);
                if (currentTool != null)
                    ResourceToolFeedback2D.PlayWrongTool(gameObject, resource.gameObject);
                CancelInteraction();
                return;
            }
            string toolName = currentTool != null
                ? currentTool.ToolName
                : "없음";

            Debug.Log($"현재 도구 '{toolName}'로는 좌클릭 상호작용을 할 수 없습니다. 도구가 필요 없는 대상은 F키를 사용하세요.");

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

            PlayToolUseAnimation(currentInteractable);
            holdTimer = 0f;
            isHolding = true;
            return;
        }

        quickInteractionTimer = 0f;
        isQuickInteractionPending = true;
        quickInteractionStartedByInteractAction = false;
        PlayToolUseAnimation(currentInteractable);
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
        PlayToolUseAnimation(currentInteractable);
        HideQuickInteractionPrompt();
    }

    // 핵심 분기: !isHolding 판정.
    // 상태 변경: holdTimer 갱신.
    // 다음 연결: PlayerInteraction.CancelInteraction() 호출.
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

        if (holdTimer < holdDuration / ToolData.GlobalAttackRateMultiplier)
            return;

        Debug.Log(
            $"{currentInteractable.GetType().Name} 자원 채집 상호작용이 완료되었습니다."
        );

        PlayToolUseAnimation(currentInteractable);
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

    // 핵심 분기: quickInteractionTimer < quickInteractionDelay 판정.
    // 상태 변경: quickInteractionTimer 갱신.
    // 다음 연결: PlayerInteraction.GetCurrentToolReach() 호출.
    private void UpdateQuickInteraction()
    {
        quickInteractionTimer += Time.deltaTime;

        if (quickInteractionTimer < quickInteractionDelay)
            return;

        float requiredRange = quickInteractionStartedByInteractAction
            ? defaultInteractionRange
            : GetCurrentToolReach();

        if ((quickInteractionStartedByInteractAction && !CanUseFQuickInteraction(currentInteractable))
            || (!quickInteractionStartedByInteractAction &&
                !CanUseLeftClickInteraction(currentInteractable, CurrentTool))
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

    // 핵심 분기: hit == null 판정.
    // 상태 변경: interactableLayer 갱신.
    // 다음 연결: PlayerInteraction.GetCombinedInteractableLayerMask() 호출.
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

    // 핵심 분기: !CanUseFQuickInteraction(interactable) 판정.
    // 상태 변경: closestDistance 갱신.
    // 다음 연결: PlayerInteraction.GetCombinedInteractableLayerMask() 호출.
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

    private static bool CanUseLeftClickInteraction(IInteractable interactable, ToolData tool)
    {
        // A null tool is the existing contract for tool-independent interactions
        // such as altars, portals and town stations. Only tool targets receive clicks.
        return interactable != null && tool != null &&
            !interactable.CanUseTool(null) && interactable.CanUseTool(tool);
    }

    private bool CanUseFQuickInteraction(IInteractable interactable)
    {
        if (interactable is StoneGolemController)
            return false;

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

    // 핵심 분기: !TryBeginSwordAttack(sword) 판정.
    // 상태 변경: animationController 갱신.
    // 다음 연결: PlayerInteraction.TryBeginSwordAttack(ToolData) 호출.
    private void PerformSwordAttack(ToolData sword)
    {
        if (!TryBeginSwordAttack(sword))
            return;

        Vector2 origin = transform.position;
        Vector2 direction = GetMouseWorldDirection(origin);
        if (animationController == null)
            animationController = GetComponent<PlayerAnimationController>();
        animationController?.PlayToolUse(direction, sword.AttackAnimationMultiplier);
        // Show the outer edge of this attack's reach, including on a miss.
        SwordReachArc2D.Spawn(origin, direction, sword, gameObject);
        attackToolSnapshot = sword;
        try
        {
        ISwordTargetQuery targetQuery = SwordTargetQueries.Resolve(sword.SwordAttackStyle);
        Collider2D[] hits = targetQuery.Find(origin, direction, sword, GetCombinedInteractableLayerMask());

        HashSet<IInteractable> attackedTargets = new();
        GameObject wrongResource = null;
        bool hitValidTarget = false;
        foreach (Collider2D hit in hits)
        {
            IInteractable target = hit.GetComponentInParent<IInteractable>();
            if (target == null || attackedTargets.Contains(target))
                continue;

            if (target is Object unityObject && unityObject == null)
                continue;

            if (!target.CanInteract())
                continue;

            if (!targetQuery.Includes(origin, direction, hit, sword))
            {
                continue;
            }

            if (!CanUseLeftClickInteraction(target, sword))
            {
                if (target is IResourceProvider && target is Component resource)
                {
                    attackedTargets.Add(target);
                    wrongResource ??= resource.gameObject;
                }
                continue;
            }

            attackedTargets.Add(target);
            hitValidTarget = true;
            IHealthSource healthSource = target as IHealthSource;
            float healthBeforeAttack = healthSource != null
                ? healthSource.CurrentHealth
                : 0;
            Vector2 impactPoint = hit.ClosestPoint(origin);
            if ((impactPoint - origin).sqrMagnitude <= Mathf.Epsilon)
                impactPoint = hit.bounds.center;

            TryUseSwordSpecialAbility(sword, target);
            target.Interact(this);

            if (healthSource != null
                && healthSource.CurrentHealth < healthBeforeAttack
                && target is Component targetComponent)
            {
                CombatHitFeedback2D.Play(targetComponent.gameObject, impactPoint);
                GetComponent<CampaignCombat>()?.OnDealtDamage(targetComponent.gameObject, sword.ToolId);
            }
        }
        if (!hitValidTarget && wrongResource != null)
            ResourceToolFeedback2D.PlayWrongTool(gameObject, wrongResource);
        }
        finally { attackToolSnapshot = null; }
    }

    private Vector2 GetMouseWorldDirection(Vector2 origin)
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera == null || Mouse.current == null)
            return Vector2.right;

        Vector2 mouseWorldPosition = mainCamera.ScreenToWorldPoint(
            Mouse.current.position.ReadValue());
        Vector2 direction = mouseWorldPosition - origin;
        return direction.sqrMagnitude > Mathf.Epsilon
            ? direction.normalized
            : Vector2.right;
    }

    private void PlayToolUseAnimation(IInteractable target)
    {
        if (animationController == null)
            animationController = GetComponent<PlayerAnimationController>();

        Vector2 direction = Vector2.right;
        if (target is Component component)
        {
            direction = (Vector2)component.transform.position - (Vector2)transform.position;
        }

        ToolData tool = CurrentTool;
        bool gatheringSwing = target is IResourceProvider && tool != null &&
            (tool.ToolType == ToolType.Axe || tool.ToolType == ToolType.Pickaxe);
        animationController?.PlayToolUse(direction,
            gatheringSwing ? tool.AttackAnimationMultiplier : 1f);
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

    // 핵심 분기: sword == null || sword.ToolType != ToolType.Sword || string.IsNullOrWhiteSpace(sword.ToolId) 판정.
    // 다음 연결: ISwordSpecialAbility.Activate(PlayerInteraction, ToolData, IInteractable) 호출.
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
