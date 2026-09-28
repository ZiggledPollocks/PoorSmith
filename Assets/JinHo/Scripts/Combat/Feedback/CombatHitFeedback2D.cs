using System.Collections;
using UnityEngine;

/// <summary>
/// Coordinates short hit-stop and camera shake for confirmed player hits.
/// Uses unscaled time so the feedback continues during hit-stop.
/// </summary>
[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public sealed class CombatHitFeedback2D : MonoBehaviour
{
    private const float HitStopDuration = 0.055f;
    private const float HitStopScale = 0.06f;
    private const float ShakeDuration = 0.12f;
    private const float ShakeStrength = 0.09f;

    private static CombatHitFeedback2D instance;

    private Vector3 appliedShakeOffset;
    private float shakeEndsAt;
    private float shakeStrength;
    private float hitStopEndsAt;
    private float timeScaleBeforeHitStop = 1f;
    private float appliedHitStopScale = 1f;
    private bool hitStopActive;

    public static void Play(GameObject target, Vector2 impactPoint)
    {
        if (target == null)
            return;

        MonsterHitFlash2D.PlayOn(target);
        SwordHitSpark2D.Spawn(impactPoint, target);

        CombatHitFeedback2D feedback = GetOrCreate();
        if (feedback == null)
            return;

        feedback.TriggerHitStop();
        feedback.TriggerShake();
    }

    public static void FinishHitStopBeforePause()
    {
        if (instance == null || !instance.hitStopActive)
            return;

        if (Mathf.Approximately(Time.timeScale, instance.appliedHitStopScale))
            Time.timeScale = instance.timeScaleBeforeHitStop;

        instance.hitStopActive = false;
    }

    private static CombatHitFeedback2D GetOrCreate()
    {
        if (instance != null)
            return instance;

        Camera camera = Camera.main;
        if (camera == null)
            return null;

        instance = camera.GetComponent<CombatHitFeedback2D>();
        if (instance == null)
            instance = camera.gameObject.AddComponent<CombatHitFeedback2D>();

        return instance;
    }

    private void Awake()
    {
        if (instance == null)
            instance = this;
    }

    private void Update()
    {
        // Remove the previous render offset before camera-follow systems run
        // their LateUpdate. This prevents cumulative drift with Cinemachine.
        transform.position -= appliedShakeOffset;
        appliedShakeOffset = Vector3.zero;

        if (!hitStopActive || Time.unscaledTime < hitStopEndsAt)
            return;

        if (Mathf.Approximately(Time.timeScale, appliedHitStopScale))
            Time.timeScale = timeScaleBeforeHitStop;

        hitStopActive = false;
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime >= shakeEndsAt)
        {
            shakeStrength = 0f;
            return;
        }

        Vector2 randomOffset = Random.insideUnitCircle * shakeStrength;
        appliedShakeOffset = new Vector3(randomOffset.x, randomOffset.y, 0f);
        transform.position += appliedShakeOffset;
    }

    private void TriggerHitStop()
    {
        if (Time.timeScale <= 0f)
            return;

        if (!hitStopActive)
        {
            timeScaleBeforeHitStop = Time.timeScale;
            appliedHitStopScale = timeScaleBeforeHitStop * HitStopScale;
            Time.timeScale = appliedHitStopScale;
            hitStopActive = true;
        }

        hitStopEndsAt = Mathf.Max(
            hitStopEndsAt,
            Time.unscaledTime + HitStopDuration);
    }

    private void TriggerShake()
    {
        shakeEndsAt = Mathf.Max(shakeEndsAt, Time.unscaledTime + ShakeDuration);
        shakeStrength = Mathf.Clamp(shakeStrength + ShakeStrength, ShakeStrength, 0.14f);
    }

    private void OnDisable()
    {
        transform.position -= appliedShakeOffset;
        appliedShakeOffset = Vector3.zero;

        if (hitStopActive && Mathf.Approximately(Time.timeScale, appliedHitStopScale))
            Time.timeScale = timeScaleBeforeHitStop;

        hitStopActive = false;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
}

[DisallowMultipleComponent]
public sealed class MonsterHitFlash2D : MonoBehaviour
{
    private const float FlashDuration = 0.16f;
    private static readonly Color HitColor = new(1f, 0.22f, 0.08f, 1f);

    private SpriteRenderer targetRenderer;
    private Color restingColor = Color.white;
    private Coroutine flashRoutine;

    public static void PlayOn(GameObject target)
    {
        MonsterHitFlash2D feedback = target.GetComponent<MonsterHitFlash2D>();
        if (feedback == null)
            feedback = target.AddComponent<MonsterHitFlash2D>();

        feedback.Play();
    }

    private void Awake()
    {
        ResolveRenderer();
    }

    private void ResolveRenderer()
    {
        if (targetRenderer != null)
            return;

        targetRenderer = GetComponent<SpriteRenderer>();
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<SpriteRenderer>();

        if (targetRenderer != null)
            restingColor = targetRenderer.color;
    }

    private void Play()
    {
        ResolveRenderer();
        if (targetRenderer == null)
            return;

        if (flashRoutine != null)
            StopCoroutine(flashRoutine);
        else
            restingColor = targetRenderer.color;

        flashRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        float startedAt = Time.unscaledTime;
        while (Time.unscaledTime - startedAt < FlashDuration)
        {
            float progress = (Time.unscaledTime - startedAt) / FlashDuration;
            float intensity = Mathf.Sin(progress * Mathf.PI);
            targetRenderer.color = Color.Lerp(restingColor, HitColor, intensity);
            yield return null;
        }

        targetRenderer.color = restingColor;
        flashRoutine = null;
    }

    private void OnDisable()
    {
        if (targetRenderer != null)
            targetRenderer.color = restingColor;
        flashRoutine = null;
    }
}

public sealed class SwordHitSpark2D : MonoBehaviour
{
    private const float Lifetime = 0.16f;
    private static Sprite sparkSprite;

    private SpriteRenderer[] renderers;
    private Transform[] sparkTransforms;
    private float startedAt;

    public static void Spawn(Vector2 position, GameObject target)
    {
        GameObject root = new("Sword Hit Spark");
        root.layer = target.layer;
        root.transform.position = new Vector3(position.x, position.y, target.transform.position.z);

        SwordHitSpark2D effect = root.AddComponent<SwordHitSpark2D>();
        effect.Build(target);
    }

    private void Build(GameObject target)
    {
        if (sparkSprite == null)
        {
            sparkSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            sparkSprite.name = "Runtime Sword Hit Spark Sprite";
        }

        SpriteRenderer targetRenderer = target.GetComponent<SpriteRenderer>();
        if (targetRenderer == null)
            targetRenderer = target.GetComponentInChildren<SpriteRenderer>();

        int sortingLayerId = targetRenderer != null ? targetRenderer.sortingLayerID : 0;
        int sortingOrder = targetRenderer != null ? targetRenderer.sortingOrder + 100 : 100;

        renderers = new SpriteRenderer[4];
        sparkTransforms = new Transform[4];
        for (int index = 0; index < sparkTransforms.Length; index++)
        {
            GameObject ray = new($"Ray {index + 1}");
            ray.layer = target.layer;
            ray.transform.SetParent(transform, false);
            ray.transform.localRotation = Quaternion.Euler(0f, 0f, 45f + index * 90f);

            SpriteRenderer rayRenderer = ray.AddComponent<SpriteRenderer>();
            rayRenderer.sprite = sparkSprite;
            rayRenderer.color = new Color(1f, 0.9f, 0.35f, 1f);
            rayRenderer.sortingLayerID = sortingLayerId;
            rayRenderer.sortingOrder = sortingOrder;

            renderers[index] = rayRenderer;
            sparkTransforms[index] = ray.transform;
        }

        startedAt = Time.unscaledTime;
    }

    private void Update()
    {
        float progress = (Time.unscaledTime - startedAt) / Lifetime;
        if (progress >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        float length = Mathf.Lerp(0.18f, 0.52f, progress);
        float thickness = Mathf.Lerp(0.055f, 0.012f, progress);
        float alpha = 1f - progress;

        for (int index = 0; index < sparkTransforms.Length; index++)
        {
            Transform ray = sparkTransforms[index];
            ray.localPosition = ray.up * Mathf.Lerp(0.02f, 0.16f, progress);
            ray.localScale = new Vector3(thickness, length, 1f);

            Color color = renderers[index].color;
            color.a = alpha;
            renderers[index].color = color;
        }
    }
}
