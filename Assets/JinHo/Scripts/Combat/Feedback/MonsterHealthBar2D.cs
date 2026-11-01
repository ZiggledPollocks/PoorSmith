// [코드 지도] MonsterHealthBar2D: IHealthSource를 읽어 몬스터 위에 SpriteRenderer 두 개로 체력바를 표시한다. 피해 후 일정 시간 보이고 사망 시 숨는다. Canvas 없이 월드 크기·색·알파를 갱신하는 구조다.
// 주요 함수: UpdateWorldLayout, EnsureVisuals, Refresh
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/Combat/Feedback/MonsterHealthBar2D.cs.md

using UnityEngine;

/// <summary>
/// Lightweight world-space health bar generated at runtime for monsters.
/// It uses SpriteRenderers, so no per-monster Canvas or prefab setup is needed.
/// </summary>
[DefaultExecutionOrder(300)]
[DisallowMultipleComponent]
public sealed class MonsterHealthBar2D : MonoBehaviour
{
    private const float BorderSize = 0.04f;

    [Header("References")]
    [SerializeField] private Collider2D bodyCollider;
    [SerializeField] private SpriteRenderer targetRenderer;

    [Header("Layout (World Units)")]
    [SerializeField, Min(0.1f)] private float minimumWidth = 0.8f;
    [SerializeField, Min(0.1f)] private float maximumWidth = 2.4f;
    [SerializeField, Range(0.25f, 1.5f)] private float widthToBodyRatio = 0.8f;
    [SerializeField, Min(0.03f)] private float height = 0.14f;
    [SerializeField, Min(0f)] private float verticalGap = 0.16f;

    [Header("Damage Visibility")]
    [Tooltip("How long the bar remains fully visible after taking damage.")]
    [SerializeField, Min(0f)] private float visibleDuration = 15f;
    [Tooltip("Fade-out time after the fully visible duration.")]
    [SerializeField, Min(0f)] private float fadeDuration = 0.35f;

    private static Sprite barSprite;

    private IHealthSource healthSource;
    private Transform barRoot;
    private Transform backgroundTransform;
    private Transform fillTransform;
    private SpriteRenderer backgroundRenderer;
    private SpriteRenderer fillRenderer;
    private float lastHealth = int.MinValue;
    private float visibleUntil;

    public static MonsterHealthBar2D Attach(
        GameObject owner,
        Collider2D collider,
        SpriteRenderer spriteRenderer)
    {
        if (owner == null)
            return null;

        MonsterHealthBar2D healthBar = owner.GetComponent<MonsterHealthBar2D>();
        if (healthBar == null)
            healthBar = owner.AddComponent<MonsterHealthBar2D>();

        healthBar.Configure(collider, spriteRenderer);
        return healthBar;
    }

    public void Configure(Collider2D collider, SpriteRenderer spriteRenderer)
    {
        bodyCollider = collider;
        targetRenderer = spriteRenderer;
        ResolveReferences();
        EnsureVisuals();
        Refresh();
    }

    private void Awake()
    {
        ResolveReferences();
        EnsureVisuals();
    }

    private void LateUpdate()
    {
        Refresh();
    }

    private void ResolveReferences()
    {
        healthSource ??= GetComponent<IHealthSource>();
        bodyCollider ??= GetComponent<Collider2D>();
        targetRenderer ??= GetComponentInChildren<SpriteRenderer>();
    }

    // 핵심 분기: barRoot != null 판정.
    // 상태 변경: barSprite 갱신.
    // 다음 연결: MonsterHealthBar2D.CreateBarPart(string, UnityEngine.Color, out UnityEngine.SpriteRenderer) 호출.
    private void EnsureVisuals()
    {
        if (barRoot != null)
            return;

        if (barSprite == null)
        {
            barSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            barSprite.name = "Runtime Monster Health Bar Sprite";
        }

        barRoot = new GameObject("Monster Health Bar").transform;
        barRoot.gameObject.layer = gameObject.layer;
        barRoot.SetParent(transform, false);

        backgroundTransform = CreateBarPart(
            "Background",
            new Color(0.04f, 0.04f, 0.04f, 0.92f),
            out backgroundRenderer);
        fillTransform = CreateBarPart(
            "Fill",
            new Color(0.2f, 0.9f, 0.25f, 1f),
            out fillRenderer);

        ApplySorting();
    }

    private Transform CreateBarPart(
        string partName,
        Color color,
        out SpriteRenderer spriteRenderer)
    {
        GameObject part = new(partName);
        part.layer = gameObject.layer;
        part.transform.SetParent(barRoot, false);
        spriteRenderer = part.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = barSprite;
        spriteRenderer.color = color;
        return part.transform;
    }

    // 핵심 분기: healthSource == null || barRoot == null 판정.
    // 상태 변경: visibleUntil 갱신.
    // 다음 연결: MonsterHealthBar2D.UpdateWorldLayout(float, float) 호출.
    private void Refresh()
    {
        if (healthSource == null || barRoot == null)
            return;

        int maximumHealth = Mathf.Max(1, healthSource.MaxHealth);
        float currentHealth = Mathf.Clamp(healthSource.CurrentHealth, 0, maximumHealth);
        bool wasInitialized = lastHealth != int.MinValue;
        bool tookDamage = wasInitialized && currentHealth < lastHealth;

        if (tookDamage)
            visibleUntil = Time.unscaledTime + visibleDuration;

        lastHealth = currentHealth;

        float fadeEndsAt = visibleUntil + fadeDuration;
        bool shouldShow = !healthSource.IsDead && Time.unscaledTime < fadeEndsAt;

        if (barRoot.gameObject.activeSelf != shouldShow)
            barRoot.gameObject.SetActive(shouldShow);
        if (!shouldShow)
            return;

        float alpha = fadeDuration <= 0f || Time.unscaledTime <= visibleUntil
            ? 1f
            : 1f - (Time.unscaledTime - visibleUntil) / fadeDuration;
        UpdateWorldLayout(currentHealth / (float)maximumHealth, Mathf.Clamp01(alpha));
    }

    // 상태 변경: barRoot.position 갱신.
    // 다음 연결: MonsterHealthBar2D.GetAnchorBounds() 호출.
    private void UpdateWorldLayout(float healthRatio, float alpha)
    {
        Bounds bounds = GetAnchorBounds();
        float barWidth = Mathf.Clamp(
            bounds.size.x * widthToBodyRatio,
            minimumWidth,
            maximumWidth);
        float innerWidth = Mathf.Max(0.01f, barWidth - BorderSize * 2f);
        float innerHeight = Mathf.Max(0.01f, height - BorderSize * 2f);
        float clampedRatio = Mathf.Clamp01(healthRatio);

        barRoot.position = new Vector3(
            bounds.center.x,
            bounds.max.y + verticalGap,
            transform.position.z);
        barRoot.rotation = Quaternion.identity;

        Vector3 ownerScale = transform.lossyScale;
        barRoot.localScale = new Vector3(
            SafeInverse(ownerScale.x),
            SafeInverse(ownerScale.y),
            1f);

        backgroundTransform.localPosition = Vector3.zero;
        backgroundTransform.localScale = new Vector3(barWidth, height, 1f);

        float fillWidth = innerWidth * clampedRatio;
        fillTransform.localPosition = new Vector3(
            -innerWidth * 0.5f + fillWidth * 0.5f,
            0f,
            0f);
        fillTransform.localScale = new Vector3(fillWidth, innerHeight, 1f);
        Color fillColor = Color.Lerp(
            new Color(0.9f, 0.12f, 0.1f, 1f),
            new Color(0.2f, 0.9f, 0.25f, 1f),
            clampedRatio);
        fillColor.a = alpha;
        fillRenderer.color = fillColor;

        Color backgroundColor = backgroundRenderer.color;
        backgroundColor.a = 0.92f * alpha;
        backgroundRenderer.color = backgroundColor;

        ApplySorting();
    }

    private Bounds GetAnchorBounds()
    {
        if (targetRenderer != null && targetRenderer.enabled)
            return targetRenderer.bounds;
        if (bodyCollider != null && bodyCollider.enabled)
            return bodyCollider.bounds;

        return new Bounds(transform.position, Vector3.one);
    }

    private void ApplySorting()
    {
        if (backgroundRenderer == null || fillRenderer == null)
            return;

        int sortingLayerId = targetRenderer != null
            ? targetRenderer.sortingLayerID
            : 0;
        int baseOrder = targetRenderer != null
            ? targetRenderer.sortingOrder + 50
            : 50;

        backgroundRenderer.sortingLayerID = sortingLayerId;
        backgroundRenderer.sortingOrder = baseOrder;
        fillRenderer.sortingLayerID = sortingLayerId;
        fillRenderer.sortingOrder = baseOrder + 1;
    }

    private static float SafeInverse(float value)
    {
        return Mathf.Abs(value) > 0.0001f ? 1f / value : 1f;
    }

    private void OnValidate()
    {
        minimumWidth = Mathf.Max(0.1f, minimumWidth);
        maximumWidth = Mathf.Max(minimumWidth, maximumWidth);
        widthToBodyRatio = Mathf.Clamp(widthToBodyRatio, 0.25f, 1.5f);
        height = Mathf.Max(0.03f, height);
        verticalGap = Mathf.Max(0f, verticalGap);
        visibleDuration = Mathf.Max(0f, visibleDuration);
        fadeDuration = Mathf.Max(0f, fadeDuration);
    }
}
