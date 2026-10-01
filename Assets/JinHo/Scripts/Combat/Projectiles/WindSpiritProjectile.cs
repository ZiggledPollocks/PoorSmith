// [코드 지도] WindSpiritProjectile: 바람 정령이 발사하는 감속 구체다. 속도가0에 가까워지면 사라지고 플레이어 또는 고체 Ground에 닿으면 소비된다. 프리팹 대신 코드로 외형·Rigidbody·Trigger를 생성한다.
// 주요 함수: GetOrbSprite, OnTriggerEnter2D, Spawn
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/Combat/Projectiles/WindSpiritProjectile.cs.md

using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(SpriteRenderer))]
/// <summary>Moves a wind spirit projectile and handles its collision.</summary>
public sealed class WindSpiritProjectile : MonoBehaviour
{
    private static Sprite orbSprite;
    private Rigidbody2D rb;
    private Vector2 direction;
    private float initialSpeed;
    private float currentSpeed;
    private float deceleration;
    private int damage;
    private GameObject owner;
    private int groundLayer;
    private bool consumed;

    // 상태 변경: orb.transform.position 갱신.
    // 다음 연결: WindSpiritProjectile.GetOrbSprite() 호출.
    public static WindSpiritProjectile Spawn(Vector2 position, Vector2 direction,
        float initialSpeed, float deceleration, float radius, int damage, GameObject owner)
    {
        GameObject orb = new("Wind Spirit Orb");
        orb.transform.position = position;
        SpriteRenderer renderer = orb.AddComponent<SpriteRenderer>();
        renderer.sprite = GetOrbSprite();
        renderer.color = new Color(0.52f, 1f, 0.9f, 0.95f);
        renderer.sortingOrder = 12;

        Rigidbody2D body = orb.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        CircleCollider2D collider = orb.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = Mathf.Max(0.05f, radius);

        WindSpiritProjectile projectile = orb.AddComponent<WindSpiritProjectile>();
        projectile.Configure(direction, initialSpeed, deceleration, damage, owner);
        return projectile;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        groundLayer = LayerMask.NameToLayer("Ground");
    }

    private void Configure(Vector2 travelDirection, float speed,
        float speedLoss, int attackDamage, GameObject source)
    {
        direction = travelDirection.sqrMagnitude > 0.001f
            ? travelDirection.normalized : Vector2.right;
        initialSpeed = Mathf.Max(0.01f, speed);
        currentSpeed = initialSpeed;
        deceleration = Mathf.Max(0.01f, speedLoss);
        damage = Mathf.Max(1, attackDamage);
        owner = source;
    }

    private void FixedUpdate()
    {
        currentSpeed = Mathf.MoveTowards(currentSpeed, 0f,
            deceleration * Time.fixedDeltaTime);
        rb.linearVelocity = direction * currentSpeed;

        float speedRatio = Mathf.Clamp01(currentSpeed / initialSpeed);
        transform.localScale = Vector3.one * Mathf.Lerp(0.55f, 0.8f, speedRatio);
        if (currentSpeed <= 0.001f)
            Destroy(gameObject);
    }

    // 핵심 분기: consumed || other == null || (owner != null && other.transform.IsChildOf(owner.transform)) 판정.
    // 상태 변경: consumed 갱신.
    // 다음 연결: CombatDamage.Apply(IDamageable, float, UnityEngine.GameObject, UnityEngine.Vector2?, UnityEngine.Vector2?) 호출.
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed || other == null ||
            (owner != null && other.transform.IsChildOf(owner.transform)))
            return;

        PlayerAssimilate player = other.GetComponentInParent<PlayerAssimilate>();
        if (player != null)
        {
            consumed = true;
            if (!player.IsDead)
            {
                CombatDamage.Apply(player, damage, owner,transform.position,direction);
                if (!player.IsDead)
                    player.GetComponent<CharacterPhysics2D>()?.ApplyKnockbackFrom(transform.position);
            }
            Destroy(gameObject);
            return;
        }

        if (other.gameObject.layer == groundLayer && !other.isTrigger)
        {
            consumed = true;
            Destroy(gameObject);
        }
    }

    // 핵심 분기: orbSprite != null 판정.
    // 상태 변경: name 갱신.
    private static Sprite GetOrbSprite()
    {
        if (orbSprite != null)
            return orbSprite;

        const int size = 24;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            name = "Runtime Wind Orb Texture",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color32[] pixels = new Color32[size * size];
        Vector2 center = new((size - 1) * 0.5f, (size - 1) * 0.5f);
        float outerRadius = size * 0.48f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                if (distance > outerRadius)
                {
                    pixels[y * size + x] = new Color32(0, 0, 0, 0);
                    continue;
                }

                float t = Mathf.Clamp01(distance / outerRadius);
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Lerp(255f, 90f, t));
                pixels[y * size + x] = new Color32(
                    (byte)Mathf.RoundToInt(Mathf.Lerp(220f, 48f, t)),
                    255,
                    (byte)Mathf.RoundToInt(Mathf.Lerp(255f, 190f, t)), alpha);
            }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        orbSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f), size);
        orbSprite.name = "Runtime Wind Orb Sprite";
        orbSprite.hideFlags = HideFlags.HideAndDontSave;
        return orbSprite;
    }
}
