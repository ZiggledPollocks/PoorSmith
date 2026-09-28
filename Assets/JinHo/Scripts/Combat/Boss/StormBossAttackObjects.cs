using System.Collections.Generic;
using UnityEngine;

public sealed class StormBossDamageZone : MonoBehaviour
{
    private readonly HashSet<PlayerAssimilate> damagedPlayers = new();
    private StormBossController owner;
    private Vector2 damageSize;
    private int damage;
    private float activatesAt;
    private float expiresAt;
    private float revealStartedAt;
    private Vector2 startPosition;
    private Vector2 targetPosition;
    private Vector3 targetScale;

    public static GameObject Spawn(
        StormBossController owner,
        Vector2 startPosition,
        Vector2 targetPosition,
        Vector2 visualSize,
        Vector2 damageSize,
        int damage,
        float warningDuration,
        float activeDuration)
    {
        GameObject zone = new("Storm Boss Wind Rectangle");
        zone.transform.position = startPosition;
        zone.transform.localScale = new Vector3(0.12f, 0.12f, 1f);

        SpriteRenderer renderer = zone.AddComponent<SpriteRenderer>();
        renderer.sprite = StormBossRuntimeSprites.White;
        renderer.color = new Color(0.38f, 0.42f, 0.48f, 1f);
        renderer.sortingOrder = 40;

        GameObject outline = new("Outline");
        outline.transform.SetParent(zone.transform, false);
        outline.transform.localScale = new Vector3(1.12f, 1.08f, 1f);
        SpriteRenderer outlineRenderer = outline.AddComponent<SpriteRenderer>();
        outlineRenderer.sprite = StormBossRuntimeSprites.White;
        outlineRenderer.color = new Color(0.72f, 0.76f, 0.8f, 1f);
        outlineRenderer.sortingOrder = 39;

        StormBossDamageZone damageZone = zone.AddComponent<StormBossDamageZone>();
        damageZone.owner = owner;
        damageZone.damageSize = damageSize;
        damageZone.damage = Mathf.Max(1, damage);
        damageZone.startPosition = startPosition;
        damageZone.targetPosition = targetPosition;
        damageZone.targetScale = new Vector3(visualSize.x, visualSize.y, 1f);
        damageZone.revealStartedAt = Time.time;
        damageZone.activatesAt = Time.time + Mathf.Max(0.05f, warningDuration);
        damageZone.expiresAt = damageZone.activatesAt + Mathf.Max(0.05f, activeDuration);
        return zone;
    }

    private void Update()
    {
        if (owner == null || !owner.IsArenaActive || Time.time >= expiresAt)
        {
            Destroy(gameObject);
            return;
        }

        if (Time.time < activatesAt)
        {
            float duration = Mathf.Max(0.01f, activatesAt - revealStartedAt);
            float progress = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01((Time.time - revealStartedAt) / duration));
            transform.position = Vector2.Lerp(startPosition, targetPosition, progress);
            transform.localScale = Vector3.Lerp(
                new Vector3(0.12f, 0.12f, 1f), targetScale, progress);
            return;
        }

        transform.position = targetPosition;
        transform.localScale = targetScale;

        Collider2D[] overlaps = Physics2D.OverlapBoxAll(
            transform.position, damageSize, 0f);
        foreach (Collider2D overlap in overlaps)
        {
            PlayerAssimilate player = overlap.GetComponentInParent<PlayerAssimilate>();
            if (player == null || player.IsDead || !damagedPlayers.Add(player))
                continue;

            CombatDamage.Apply(player,damage,owner!=null?owner.gameObject:null);
            if (!player.IsDead)
                player.GetComponent<CharacterPhysics2D>()?
                    .ApplyKnockbackFrom(owner.transform.position, 0.85f);
        }
    }
}

[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public sealed class StormBossOrbProjectile : MonoBehaviour
{
    private StormBossController owner;
    private Rigidbody2D body;
    private Vector2 direction;
    private float speed;
    private float expiresAt;
    private int damage;
    private int groundLayer;
    private bool consumed;

    public static GameObject Spawn(
        StormBossController owner,
        Vector2 position,
        Vector2 direction,
        float speed,
        float radius,
        int damage,
        float lifetime)
    {
        GameObject orb = new("Storm Boss Large Orb");
        orb.transform.position = position;
        orb.transform.localScale = Vector3.one * (radius * 2f);

        SpriteRenderer renderer = orb.AddComponent<SpriteRenderer>();
        renderer.sprite = StormBossRuntimeSprites.Orb;
        renderer.color = new Color(0.66f, 0.78f, 0.84f, 0.95f);
        renderer.sortingOrder = 20;

        Rigidbody2D rigidbody = orb.AddComponent<Rigidbody2D>();
        rigidbody.gravityScale = 0f;
        rigidbody.constraints = RigidbodyConstraints2D.FreezeRotation;
        rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;

        CircleCollider2D collider = orb.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.5f;

        StormBossOrbProjectile projectile = orb.AddComponent<StormBossOrbProjectile>();
        projectile.owner = owner;
        projectile.direction = direction.sqrMagnitude > 0.001f
            ? direction.normalized
            : Vector2.right;
        projectile.speed = Mathf.Max(0.1f, speed);
        projectile.damage = Mathf.Max(1, damage);
        projectile.expiresAt = Time.time + Mathf.Max(0.1f, lifetime);
        return orb;
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        groundLayer = LayerMask.NameToLayer("Ground");
    }

    private void FixedUpdate()
    {
        if (owner == null || !owner.IsArenaActive || Time.time >= expiresAt)
        {
            Destroy(gameObject);
            return;
        }

        body.linearVelocity = direction * speed;
    }

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
                CombatDamage.Apply(player,damage,owner!=null?owner.gameObject:null);
                if (!player.IsDead)
                    player.GetComponent<CharacterPhysics2D>()?
                        .ApplyKnockbackFrom(transform.position, 1.1f);
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
}

public static class StormBossRuntimeSprites
{
    private static Sprite white;
    private static Sprite orb;

    public static Sprite White
    {
        get
        {
            if (white == null)
                white = CreateWhiteSprite();
            return white;
        }
    }

    public static Sprite Orb
    {
        get
        {
            if (orb == null)
                orb = CreateOrbSprite();
            return orb;
        }
    }

    private static Sprite CreateWhiteSprite()
    {
        Texture2D texture = new(1, 1, TextureFormat.RGBA32, false)
        {
            name = "Runtime Storm Boss White Texture",
            filterMode = FilterMode.Point,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f), 1f);
        sprite.name = "Runtime Storm Boss White Sprite";
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private static Sprite CreateOrbSprite()
    {
        const int size = 32;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            name = "Runtime Storm Boss Orb Texture",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color32[] pixels = new Color32[size * size];
        Vector2 center = Vector2.one * ((size - 1) * 0.5f);
        float radius = size * 0.48f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float normalized = Vector2.Distance(new Vector2(x, y), center) / radius;
            if (normalized > 1f)
            {
                pixels[y * size + x] = new Color32(0, 0, 0, 0);
                continue;
            }

            byte shade = (byte)Mathf.RoundToInt(Mathf.Lerp(245f, 105f, normalized));
            byte alpha = (byte)Mathf.RoundToInt(Mathf.Lerp(255f, 155f, normalized));
            pixels[y * size + x] = new Color32(shade, shade, 255, alpha);
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f), size);
        sprite.name = "Runtime Storm Boss Orb Sprite";
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
