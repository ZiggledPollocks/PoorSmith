// [코드 지도] StormBossController: 전투 영역에 들어온 플레이어와 두 페이즈 보스전을 진행합니다.1페이즈는 방향성 바람과 다섯 직사각형 공격,2페이즈는 급강하 또는 큰 구체를 사용합니다. 코루틴과 세대 토큰으로 공격 흐름·취소를 관리합니다. 보스 체력 UI와 사망 드롭도 이 클래스가 생성합니다.
// 주요 함수: PhaseOneWindAttack, PhaseTwoDiveAttack, CreateHealthBar
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/Combat/Boss/StormBossController.cs.md

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public sealed class StormBossController : MonoBehaviour, IHealthSource, IInteractable
{
    private const int PhaseOneRectangleCount = 5;

    [Header("References")]
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private Collider2D bodyCollider;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite phaseOneSprite;
    [SerializeField] private Sprite phaseTwoSprite;
    [SerializeField] private LayerMask groundLayers;

    [Header("Stats")]
    [SerializeField, Min(1)] private int maxHealth = 300;
    [SerializeField, Range(0.05f, 0.95f)] private float phaseTwoHealthRatio = 0.5f;

    [Header("Phase 1 - Wind Rectangles")]
    [SerializeField, Min(0f)] private float minimumRectangleDelay = 2f;
    [SerializeField, Min(0f)] private float maximumRectangleDelay = 5f;
    [Tooltip("플레이어가 입력하지 않을 때 바람 방향으로 이동하는 속도입니다.")]
    [SerializeField, Min(0f)] private float phaseOneWindSpeed = 2.5f;
    [SerializeField, Min(0.1f)] private float firstRectangleDistance = 2.2f;
    [SerializeField, Min(0.1f)] private float rectangleSpacing = 2.45f;
    [SerializeField, Min(0.1f)] private float rectangleWidth = 1.55f;
    [SerializeField, Min(0.1f)] private float nearestRectangleHeight = 0.65f;
    [SerializeField, Min(0f)] private float rectangleHeightStep = 0.38f;
    [SerializeField, Min(0f)] private float rectangleSpawnInterval = 0.12f;
    [SerializeField, Min(0.05f)] private float rectangleActiveDuration = 1.4f;
    [Tooltip("마지막 직사각형이 생성된 뒤 화살표 방향을 전환하기까지 기다리는 시간입니다.")]
    [SerializeField, Min(0f)] private float directionChangeDelay = 1.5f;
    [SerializeField, Min(1)] private int rectangleDamage = 10;

    [Header("Phase 2 - Dive")]
    [SerializeField, Min(0.1f)] private float phaseTwoAttackCooldown = 2.35f;
    [SerializeField, Min(0f)] private float phaseTwoScaleMultiplier = 1.8f;
    [SerializeField, Min(0.1f)] private float flyUpDuration = 0.35f;
    [SerializeField, Min(0f)] private float hiddenBeforeDiveDuration = 1.5f;
    [SerializeField, Min(0f)] private float diveWarningDuration = 0.8f;
    [SerializeField, Min(0.1f)] private float diveWidth = 4f;
    [SerializeField, Min(0.1f)] private float diveDamageHeight = 3f;
    [SerializeField, Min(1)] private int diveDamage = 25;

    [Header("Phase 2 - Orb")]
    [SerializeField, Min(0f)] private float orbWindupDuration = 0.25f;
    [SerializeField, Min(0.1f)] private float largeOrbSpeed = 8f;
    [SerializeField, Min(0.1f)] private float largeOrbRadius = 0.72f;
    [SerializeField, Min(1)] private int largeOrbDamage = 20;
    [SerializeField, Min(0.1f)] private float largeOrbLifetime = 5f;

    [Header("Death Drop")]
    [SerializeField] private ResourceData dropData;
    [SerializeField] private ItemDropSpawner itemDropSpawner;
    [SerializeField, Min(1)] private int coalDropCount = 2;
    [SerializeField, Min(0f)] private float dropRadius = 0.55f;

    private readonly List<GameObject> activeAttackObjects = new();
    private StormBossArena arena;
    private BoxCollider2D arenaCollider;
    private PlayerAssimilate playerTarget;
    private PlayerMovement windAffectedPlayer;
    private Vector2 homePosition;
    private Vector3 phaseOneVisualScale;
    private Coroutine activeAttack;
    private Coroutine phaseOneAttackLoop;
    private RectTransform healthFill;
    private Image healthFillImage;
    private GameObject healthUiRoot;
    private float currentHealth;
    private int attackToken;
    private int phaseOneDirection = 1;
    private float nextAttackTime;
    private bool phaseTwo;
    private bool arenaActive;
    private bool dead;
    private bool droppedItems;
    private bool reportedFirstRectangleAttack;

    public float CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDead => dead;
    public bool IsArenaActive => arenaActive && !dead;

    // 핵심 분기: spriteRenderer != null 판정.
    // 상태 변경: body 갱신.
    // 다음 연결: StormBossController.CreateHealthBar() 호출.
    private void Awake()
    {
        body ??= GetComponent<Rigidbody2D>();
        bodyCollider ??= GetComponent<Collider2D>();
        spriteRenderer ??= GetComponentInChildren<SpriteRenderer>(true);
        itemDropSpawner ??= FindFirstObjectByType<ItemDropSpawner>();

        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.constraints |= RigidbodyConstraints2D.FreezeRotation;
        bodyCollider.isTrigger = true;

        if (spriteRenderer != null)
        {
            phaseOneVisualScale = spriteRenderer.transform.localScale;
            if (phaseOneSprite != null)
                spriteRenderer.sprite = phaseOneSprite;
        }

        currentHealth = maxHealth;
        homePosition = body.position;
        CreateHealthBar();
        SetHealthBarVisible(false);
        UpdateHealthBar();
    }

    // 핵심 분기: !IsArenaActive 판정.
    // 상태 변경: activeAttack 갱신.
    // 다음 연결: StormBossController.UpdateHealthBar() 호출.
    private void Update()
    {
        UpdateHealthBar();
        if (!IsArenaActive)
            return;

        // Enter/exit events or a script reload can stop a coroutine while the
        // player is still inside the arena. Keep phase 1 self-healing.
        if (!phaseTwo)
        {
            if (phaseOneAttackLoop == null)
                StartPhaseOneAttackLoop();
            return;
        }

        if (activeAttack != null || Time.time < nextAttackTime)
            return;

        int token = ++attackToken;
        activeAttack = StartCoroutine(RunAttack(token));
    }

    private void FixedUpdate()
    {
        UpdatePhaseOneWind();
    }

    private void OnDisable()
    {
        arenaActive = false;
        ClearPhaseOneWind();
        CancelCurrentAttack();
        SetHealthBarVisible(false);
    }

    private void OnDestroy()
    {
        ClearPhaseOneWind();
        CancelAttackObjects();
        if (healthUiRoot != null)
            Destroy(healthUiRoot);
    }

    public void ConfigureArena(StormBossArena ownerArena, BoxCollider2D boundsCollider)
    {
        arena = ownerArena;
        arenaCollider = boundsCollider;

        // The arena places the boss by Transform immediately after Instantiate.
        // Rigidbody2D.position can still contain the pre-placement coordinate in
        // that frame, so make the placed Transform position authoritative.
        homePosition = transform.position;
        if (body != null)
        {
            body.position = homePosition;
            body.linearVelocity = Vector2.zero;
        }
    }

    // 핵심 분기: !isActiveAndEnabled 판정.
    // 상태 변경: arenaActive 갱신.
    // 다음 연결: StormBossController.SetHealthBarVisible(bool) 호출.
    public void SetArenaActive(bool active, PlayerAssimilate player)
    {
        if (!isActiveAndEnabled) { arenaActive = false; return; }
        if (dead)
        {
            SetHealthBarVisible(false);
            return;
        }

        playerTarget = player;
        if (arenaActive == active)
        {
            SetHealthBarVisible(active);
            if (active && !phaseTwo && phaseOneAttackLoop == null)
                StartPhaseOneAttackLoop();
            return;
        }

        arenaActive = active;
        SetHealthBarVisible(active);
        if (active)
        {
            if (phaseTwo)
                nextAttackTime = Time.time + 0.8f;
            else
                StartPhaseOneAttackLoop();
            return;
        }

        ClearPhaseOneWind();
        CancelCurrentAttack();
    }

    public void ResetEncounter()
    {
        if (dead) return;
        SetArenaActive(false, null); CancelCurrentAttack(); ClearPhaseOneWind();
        currentHealth = maxHealth; phaseTwo = false; phaseOneDirection = 1;
        body.position = homePosition; body.linearVelocity = Vector2.zero;
        if (spriteRenderer != null) { spriteRenderer.enabled = true; spriteRenderer.transform.localScale = phaseOneVisualScale; if (phaseOneSprite != null) spriteRenderer.sprite = phaseOneSprite; }
        UpdateHealthBar();
    }
    public bool CanInteract() => IsArenaActive;
    public bool CanUseTool(ToolData toolData) =>
        toolData != null && toolData.ToolType == ToolType.Sword;

    public void Interact(PlayerInteraction interactionContext)
    {
        if (interactionContext == null ||
            !CanUseTool(interactionContext.CurrentTool) || !CanInteract())
            return;

        playerTarget = interactionContext.GetComponent<PlayerAssimilate>();
        TakeDamage(interactionContext.CurrentTool.Damage);
    }

    public void TakeDamage(float amount)
    {
        if (amount <= 0 || dead || !arenaActive)
            return;

        float before = currentHealth;
        currentHealth = Mathf.Max(0, CombatDamage.RoundHealth(currentHealth - amount));
        CampaignDamageNumber.Show(gameObject, before - currentHealth);
        if (!phaseTwo && currentHealth <= Mathf.CeilToInt(maxHealth * phaseTwoHealthRatio))
            EnterPhaseTwo();

        UpdateHealthBar();
        if (currentHealth <= 0)
            Die();
    }

    private IEnumerator RunAttack(int token)
    {
        if (Random.Range(0, 2) == 0)
            yield return PhaseTwoDiveAttack(token);
        else
            yield return PhaseTwoOrbAttack(token);

        activeAttack = null;
        if (IsAttackValid(token))
            nextAttackTime = Time.time + phaseTwoAttackCooldown;
    }

    private void StartPhaseOneAttackLoop()
    {
        if (phaseOneAttackLoop != null || phaseTwo || !IsArenaActive)
            return;

        int token = ++attackToken;
        phaseOneAttackLoop = StartCoroutine(PhaseOneAttackLoop(token));
    }

    private IEnumerator PhaseOneAttackLoop(int token)
    {
        float minimum = Mathf.Max(0f, minimumRectangleDelay);
        float maximum = Mathf.Max(minimum, maximumRectangleDelay);
        yield return WaitWhilePhaseOneValid(Random.Range(minimum, maximum), token);

        while (IsPhaseOneAttackValid(token))
        {
            float attackStartedAt = Time.time;
            float nextAttackInterval = Random.Range(minimum, maximum);
            yield return PhaseOneWindAttack(token);

            float remainingDelay = nextAttackInterval - (Time.time - attackStartedAt);
            if (remainingDelay > 0f)
                yield return WaitWhilePhaseOneValid(remainingDelay, token);
        }

        phaseOneAttackLoop = null;
    }

    // 핵심 분기: !IsPhaseOneAttackValid(token) 판정.
    // 상태 변경: reportedFirstRectangleAttack 갱신.
    // 다음 연결: StormBossController.ApplyPhaseOneFacing() 호출.
    private IEnumerator PhaseOneWindAttack(int token)
    {
        int direction = phaseOneDirection;
        ApplyPhaseOneFacing();

        Vector2 bossOrigin = body != null
            ? body.position
            : (Vector2)transform.position;
        float bossCenterX = bodyCollider != null
            ? bodyCollider.bounds.center.x
            : bossOrigin.x;
        float bossEdgeX = bodyCollider != null
            ? bossCenterX + direction * bodyCollider.bounds.extents.x
            : bossOrigin.x;
        float bossFootY = bodyCollider != null
            ? bodyCollider.bounds.min.y
            : bossOrigin.y - 0.15f;

        int spawnedCount = 0;
        for (int index = 0; index < PhaseOneRectangleCount; index++)
        {
            if (!IsPhaseOneAttackValid(token))
                yield break;

            float distance = firstRectangleDistance + rectangleSpacing * index;
            float x = bossCenterX + direction * distance;
            float height = nearestRectangleHeight + rectangleHeightStep * index;
            float groundY = FindRectangleGroundY(x, bossOrigin.y);
            Vector2 size = new(rectangleWidth, height);
            Vector2 emergePosition = new(
                bossEdgeX,
                bossFootY + nearestRectangleHeight * 0.5f);
            GameObject rectangle = StormBossDamageZone.Spawn(
                this,
                emergePosition,
                new Vector2(x, groundY + height * 0.5f),
                size,
                size,
                rectangleDamage,
                0.28f,
                rectangleActiveDuration);
            RegisterAttackObject(rectangle);
            spawnedCount++;

            if (index < PhaseOneRectangleCount - 1 && rectangleSpawnInterval > 0f)
                yield return WaitWhilePhaseOneValid(rectangleSpawnInterval, token);
        }

        if (!reportedFirstRectangleAttack && spawnedCount == PhaseOneRectangleCount)
        {
            reportedFirstRectangleAttack = true;
            Debug.Log(
                $"{name}: 1페이즈 바람 직사각형 {spawnedCount}개 생성 완료 " +
                $"(방향: {(direction > 0 ? "오른쪽" : "왼쪽")}, 피해: {rectangleDamage})",
                this);
        }

        yield return WaitWhilePhaseOneValid(directionChangeDelay, token);
        if (!IsPhaseOneAttackValid(token))
            yield break;

        phaseOneDirection *= -1;
        ApplyPhaseOneFacing();
    }

    // 핵심 분기: !IsAttackValid(token) 판정.
    // 상태 변경: bodyCollider.enabled 갱신.
    // 다음 연결: StormBossController.GetArenaBounds() 호출.
    private IEnumerator PhaseTwoDiveAttack(int token)
    {
        bodyCollider.enabled = false;
        Vector2 start = body.position;
        Camera camera = Camera.main;
        float cameraTop = camera != null
            ? camera.ViewportToWorldPoint(new Vector3(0.5f, 1f, 0f)).y
            : GetArenaBounds().max.y;
        float hiddenY = Mathf.Max(GetArenaBounds().max.y + 3f, cameraTop + 3f);
        Vector2 hiddenPosition = new(start.x, hiddenY);

        yield return MoveBoss(start, hiddenPosition, flyUpDuration, token);
        if (!IsAttackValid(token))
            yield break;

        yield return WaitWhileAttackValid(hiddenBeforeDiveDuration, token);
        if (!IsAttackValid(token))
            yield break;

        Vector2 playerPosition = playerTarget != null
            ? playerTarget.transform.position
            : homePosition;
        Bounds arenaBounds = GetArenaBounds();
        float targetX = Mathf.Clamp(playerPosition.x,
            arenaBounds.min.x + diveWidth * 0.5f,
            arenaBounds.max.x - diveWidth * 0.5f);
        float groundY = FindGroundY(targetX);
        body.position = new Vector2(targetX, hiddenY);

        GameObject warning = CreateDiveWarning(
            new Vector2(targetX, groundY + 0.1f));
        RegisterAttackObject(warning);
        yield return WaitWhileAttackValid(diveWarningDuration, token);
        if (!IsAttackValid(token))
            yield break;

        Vector2 landingPosition = new(targetX, groundY + 0.15f);
        yield return MoveBoss(body.position, landingPosition, 0.16f, token);
        if (!IsAttackValid(token))
            yield break;

        bodyCollider.enabled = true;
        DamagePlayersInArea(
            new Vector2(targetX, groundY + diveDamageHeight * 0.5f),
            new Vector2(diveWidth, diveDamageHeight),
            diveDamage,
            1.35f);
        if (warning != null)
            Destroy(warning);

        yield return WaitWhileAttackValid(0.3f, token);
        if (!IsAttackValid(token))
            yield break;
        yield return MoveBoss(body.position, homePosition, 0.35f, token);
    }

    private IEnumerator PhaseTwoOrbAttack(int token)
    {
        LockBossAtHome();
        float firesAt = Time.time + Mathf.Max(0f, orbWindupDuration);
        while (Time.time < firesAt && IsAttackValid(token))
        {
            LockBossAtHome();
            yield return null;
        }

        if (!IsAttackValid(token))
            yield break;

        LockBossAtHome();
        FireLargeOrb();
    }

    private void LockBossAtHome()
    {
        if (body == null)
            return;

        body.linearVelocity = Vector2.zero;
        body.position = homePosition;
    }

    private IEnumerator MoveBoss(
        Vector2 from, Vector2 to, float duration, int token)
    {
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);
        while (elapsed < safeDuration)
        {
            if (!IsAttackValid(token))
                yield break;

            elapsed += Time.deltaTime;
            float progress = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01(elapsed / safeDuration));
            body.position = Vector2.LerpUnclamped(from, to, progress);
            yield return null;
        }
        body.position = to;
    }

    private IEnumerator WaitWhileAttackValid(float duration, int token)
    {
        float endsAt = Time.time + Mathf.Max(0f, duration);
        while (Time.time < endsAt && IsAttackValid(token))
            yield return null;
    }

    private IEnumerator WaitWhilePhaseOneValid(float duration, int token)
    {
        float endsAt = Time.time + Mathf.Max(0f, duration);
        while (Time.time < endsAt && IsPhaseOneAttackValid(token))
            yield return null;
    }

    private bool IsAttackValid(int token) =>
        token == attackToken && IsArenaActive && playerTarget != null &&
        !playerTarget.IsDead;

    private bool IsPhaseOneAttackValid(int token) =>
        token == attackToken && IsArenaActive && !phaseTwo;

    private void FireLargeOrb()
    {
        if (playerTarget == null)
            return;

        Vector2 origin = bodyCollider != null
            ? bodyCollider.bounds.center
            : body.position + Vector2.up * 1.5f;
        Vector2 direction = ((Vector2)playerTarget.transform.position - origin).normalized;
        if (direction.sqrMagnitude < 0.001f)
            direction = Vector2.right;

        GameObject orb = StormBossOrbProjectile.Spawn(
            this, origin, direction, largeOrbSpeed,
            largeOrbRadius, largeOrbDamage, largeOrbLifetime);
        RegisterAttackObject(orb);
    }

    private GameObject CreateDiveWarning(Vector2 position)
    {
        GameObject warning = new("Storm Boss Dive Warning");
        warning.transform.position = position;
        warning.transform.localScale = new Vector3(diveWidth, 0.2f, 1f);
        SpriteRenderer renderer = warning.AddComponent<SpriteRenderer>();
        renderer.sprite = StormBossRuntimeSprites.White;
        renderer.color = new Color(1f, 0.05f, 0.05f, 0.62f);
        renderer.sortingOrder = 19;
        return warning;
    }

    private void DamagePlayersInArea(
        Vector2 center, Vector2 size, int damage, float knockbackMultiplier)
    {
        HashSet<PlayerAssimilate> damaged = new();
        foreach (Collider2D overlap in Physics2D.OverlapBoxAll(center, size, 0f))
        {
            PlayerAssimilate player = overlap.GetComponentInParent<PlayerAssimilate>();
            if (player == null || player.IsDead || !damaged.Add(player))
                continue;

            CombatDamage.Apply(player, damage, gameObject);
            if (!player.IsDead)
                player.GetComponent<CharacterPhysics2D>()?
                    .ApplyKnockbackFrom(transform.position, knockbackMultiplier);
        }
    }

    private float FindGroundY(float x)
    {
        Bounds bounds = GetArenaBounds();
        Vector2 origin = new(x, bounds.max.y + 1f);
        RaycastHit2D hit = Physics2D.Raycast(
            origin, Vector2.down, bounds.size.y + 2f, groundLayers);
        return hit.collider != null ? hit.point.y : bounds.min.y;
    }

    private float FindRectangleGroundY(float x, float bossOriginY)
    {
        Bounds bounds = GetArenaBounds();
        float rayStartY = Mathf.Min(bounds.max.y + 1f, bossOriginY + 4f);
        float rayDistance = Mathf.Max(1f, rayStartY - bounds.min.y + 1f);
        RaycastHit2D hit = Physics2D.Raycast(
            new Vector2(x, rayStartY), Vector2.down, rayDistance, groundLayers);

        // The arena is expected to be mostly flat. Falling back to the boss's
        // foot level keeps the warning visible even when a distant floor tile
        // has no collider at the sampled point.
        return hit.collider != null ? hit.point.y : bossOriginY - 0.15f;
    }

    private Bounds GetArenaBounds() => arenaCollider != null
        ? arenaCollider.bounds
        : new Bounds(homePosition, new Vector3(30f, 15f, 0f));

    private void EnterPhaseTwo()
    {
        phaseTwo = true;
        ClearPhaseOneWind();
        CancelCurrentAttack();
        if (spriteRenderer != null)
        {
            if (phaseTwoSprite != null)
                spriteRenderer.sprite = phaseTwoSprite;
            spriteRenderer.transform.localScale =
                phaseOneVisualScale * phaseTwoScaleMultiplier;
            spriteRenderer.flipX = false;
        }
        nextAttackTime = Time.time + 0.75f;
    }

    // 핵심 분기: activeAttack != null 판정.
    // 상태 변경: activeAttack 갱신.
    // 다음 연결: StormBossController.CancelAttackObjects() 호출.
    private void CancelCurrentAttack()
    {
        attackToken++;
        if (activeAttack != null)
        {
            StopCoroutine(activeAttack);
            activeAttack = null;
        }
        if (phaseOneAttackLoop != null)
        {
            StopCoroutine(phaseOneAttackLoop);
            phaseOneAttackLoop = null;
        }

        CancelAttackObjects();
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.position = homePosition;
        }
        if (bodyCollider != null && !dead)
            bodyCollider.enabled = true;
    }

    private void ApplyPhaseOneFacing()
    {
        if (spriteRenderer != null)
            spriteRenderer.flipX = phaseOneDirection < 0;
    }

    // 핵심 분기: !IsArenaActive || phaseTwo || playerTarget == null 판정.
    // 상태 변경: windAffectedPlayer 갱신.
    // 다음 연결: StormBossController.ClearPhaseOneWind() 호출.
    private void UpdatePhaseOneWind()
    {
        if (!IsArenaActive || phaseTwo || playerTarget == null)
        {
            ClearPhaseOneWind();
            return;
        }

        PlayerMovement movement = playerTarget.GetComponent<PlayerMovement>();
        if (movement == null)
        {
            ClearPhaseOneWind();
            return;
        }

        if (windAffectedPlayer != null && windAffectedPlayer != movement)
            windAffectedPlayer.ClearExternalHorizontalWind(this);

        windAffectedPlayer = movement;
        windAffectedPlayer.SetExternalHorizontalWind(
            this, phaseOneDirection * phaseOneWindSpeed);
    }

    private void ClearPhaseOneWind()
    {
        if (windAffectedPlayer != null)
            windAffectedPlayer.ClearExternalHorizontalWind(this);
        windAffectedPlayer = null;
    }

    private void RegisterAttackObject(GameObject attackObject)
    {
        if (attackObject != null)
            activeAttackObjects.Add(attackObject);
    }

    private void CancelAttackObjects()
    {
        foreach (GameObject attackObject in activeAttackObjects)
        {
            if (attackObject != null)
                Destroy(attackObject);
        }
        activeAttackObjects.Clear();
    }

    private void Die()
    {
        if (dead)
            return;

        dead = true;
        arenaActive = false;
        ClearPhaseOneWind();
        CancelCurrentAttack();
        bodyCollider.enabled = false;
        SetHealthBarVisible(false);
        if (CampaignController.Instance != null) CampaignController.Instance.BossDefeated(); else SpawnCoal();
        Destroy(gameObject, 0.35f);
    }

    private void SpawnCoal()
    {
        if (droppedItems)
            return;
        droppedItems = true;

        if (dropData == null || itemDropSpawner == null ||
            !dropData.TryGetDrop(0, out GameObject coalPrefab, out _))
            return;

        for (int index = 0; index < coalDropCount; index++)
        {
            float direction = index % 2 == 0 ? -1f : 1f;
            Vector2 offset = new(direction * dropRadius, 0.25f);
            itemDropSpawner.Spawn(coalPrefab,
                transform.position + (Vector3)offset, 1);
        }
    }

    // 상태 변경: healthUiRoot 갱신.
    // 다음 연결: StormBossController.CreateUiImage(string, UnityEngine.Transform, UnityEngine.Color) 호출.
    private void CreateHealthBar()
    {
        healthUiRoot = new GameObject(
            "Storm Boss Health Bar UI",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = healthUiRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        CanvasScaler scaler = healthUiRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject background = CreateUiImage(
            "Boss Health Background", healthUiRoot.transform,
            new Color(0.035f, 0.04f, 0.05f, 0.94f));
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0.5f, 1f);
        backgroundRect.anchorMax = new Vector2(0.5f, 1f);
        backgroundRect.pivot = new Vector2(0.5f, 1f);
        backgroundRect.anchoredPosition = new Vector2(0f, -34f);
        backgroundRect.sizeDelta = new Vector2(560f, 32f);

        GameObject track = CreateUiImage(
            "Boss Health Track", background.transform,
            new Color(0.18f, 0.19f, 0.21f, 1f));
        RectTransform trackRect = track.GetComponent<RectTransform>();
        trackRect.anchorMin = Vector2.zero;
        trackRect.anchorMax = Vector2.one;
        trackRect.offsetMin = new Vector2(4f, 4f);
        trackRect.offsetMax = new Vector2(-4f, -4f);

        GameObject fill = CreateUiImage(
            "Boss Health Fill", track.transform,
            new Color(0.77f, 0.12f, 0.1f, 1f));
        healthFill = fill.GetComponent<RectTransform>();
        healthFill.anchorMin = Vector2.zero;
        healthFill.anchorMax = Vector2.one;
        healthFill.pivot = new Vector2(0f, 0.5f);
        healthFill.offsetMin = Vector2.zero;
        healthFill.offsetMax = Vector2.zero;
        healthFillImage = fill.GetComponent<Image>();
    }

    private static GameObject CreateUiImage(
        string objectName, Transform parent, Color color)
    {
        GameObject imageObject = new(
            objectName, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return imageObject;
    }

    private void SetHealthBarVisible(bool visible)
    {
        if (healthUiRoot != null)
            healthUiRoot.SetActive(visible && !dead);
    }

    private void UpdateHealthBar()
    {
        if (healthFill == null)
            return;

        float ratio = Mathf.Clamp01(currentHealth / (float)Mathf.Max(1, maxHealth));
        healthFill.anchorMax = new Vector2(ratio, 1f);
        if (healthFillImage != null)
            healthFillImage.color = Color.Lerp(
                new Color(0.8f, 0.08f, 0.06f, 1f),
                new Color(0.2f, 0.78f, 0.28f, 1f), ratio);
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        phaseTwoHealthRatio = Mathf.Clamp(phaseTwoHealthRatio, 0.05f, 0.95f);
        minimumRectangleDelay = Mathf.Max(0f, minimumRectangleDelay);
        maximumRectangleDelay = Mathf.Max(
            minimumRectangleDelay, maximumRectangleDelay);
        phaseOneWindSpeed = Mathf.Max(0f, phaseOneWindSpeed);
        orbWindupDuration = Mathf.Max(0f, orbWindupDuration);
        rectangleDamage = Mathf.Max(1, rectangleDamage);
        diveDamage = Mathf.Max(1, diveDamage);
        largeOrbDamage = Mathf.Max(1, largeOrbDamage);
        coalDropCount = Mathf.Max(1, coalDropCount);
    }
}
