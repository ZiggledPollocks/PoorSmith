using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class CaveEntranceBackgroundTransition : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private SpriteRenderer entranceRenderer;
    [SerializeField] private GameObject outsideBackground;
    [SerializeField] private GameObject caveBackground;

    [Header("Crossing")]
    [SerializeField] private bool caveIsToRight = true;
    [SerializeField] private bool allowReturnToOutside = true;
    [SerializeField, Min(0f)] private float returnHysteresis = 0.15f;
    [SerializeField] private float crossingOffsetX = -1f;

    [Header("Background Fade")]
    [SerializeField, Min(0f)] private float backgroundFadeDuration = 0.75f;

    [Header("Forest Background Boundary")]
    [SerializeField, Min(1f)] private float forestMaskWidth = 1000f;
    [SerializeField, Min(1f)] private float forestMaskHeight = 1000f;

    private float crossingX;
    private bool isInsideCave;
    private bool isInitialized;
    private GameObject forestMaskObject;
    private Sprite forestMaskSprite;
    private SpriteRenderer[] forestRenderers;
    private SpriteRenderer[] caveRenderers;
    private Color[] outsideBaseColors;
    private Color[] caveBaseColors;
    private Coroutine backgroundTransition;
    private float outsideAlpha = 1f;
    private float caveAlpha;

    private void Reset()
    {
        entranceRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        Initialize();
    }

    private void Update()
    {
        if (!isInitialized)
        {
            Initialize();
        }

        if (!isInitialized)
            return;

        float signedDistance = GetSignedDistanceFromEntrance();

        if (!isInsideCave && signedDistance >= 0f)
        {
            ApplyBackgroundState(true);
        }
        else if (isInsideCave && allowReturnToOutside && signedDistance <= -returnHysteresis)
        {
            ApplyBackgroundState(false);
        }
    }

    private void Initialize()
    {
        if (entranceRenderer == null)
        {
            entranceRenderer = GetComponent<SpriteRenderer>();
        }

        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                player = playerObject.transform;
            }
        }

        if (player == null || entranceRenderer == null ||
            outsideBackground == null || caveBackground == null)
        {
            return;
        }

        crossingX = entranceRenderer.bounds.center.x + crossingOffsetX;
        CreateForestBackgroundMask();
        CacheBackgroundRenderers();
        ApplyBackgroundState(GetSignedDistanceFromEntrance() >= 0f, true);
        isInitialized = true;
    }

    public void RefreshImmediatelyAfterTeleport()
    {
        if (!isInitialized)
            Initialize();

        if (isInitialized)
            ApplyBackgroundState(GetSignedDistanceFromEntrance() >= 0f, true);
    }

    private float GetSignedDistanceFromEntrance()
    {
        float direction = caveIsToRight ? 1f : -1f;
        return (player.position.x - crossingX) * direction;
    }

    private void ApplyBackgroundState(bool insideCave, bool immediate = false)
    {
        isInsideCave = insideCave;

        if (backgroundTransition != null)
        {
            StopCoroutine(backgroundTransition);
            backgroundTransition = null;
        }

        outsideBackground.SetActive(true);
        caveBackground.SetActive(true);

        float targetOutsideAlpha = insideCave ? 0f : 1f;
        float targetCaveAlpha = insideCave ? 1f : 0f;
        if (immediate || backgroundFadeDuration <= 0f)
        {
            SetBackgroundAlphas(targetOutsideAlpha, targetCaveAlpha);
            FinishBackgroundTransition(insideCave);
            return;
        }

        backgroundTransition = StartCoroutine(FadeBackgrounds(
            outsideAlpha,
            caveAlpha,
            targetOutsideAlpha,
            targetCaveAlpha,
            insideCave));
    }

    private IEnumerator FadeBackgrounds(
        float startOutsideAlpha,
        float startCaveAlpha,
        float targetOutsideAlpha,
        float targetCaveAlpha,
        bool finishInsideCave)
    {
        float elapsed = 0f;
        while (elapsed < backgroundFadeDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / backgroundFadeDuration);
            float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
            SetBackgroundAlphas(
                Mathf.Lerp(startOutsideAlpha, targetOutsideAlpha, easedProgress),
                Mathf.Lerp(startCaveAlpha, targetCaveAlpha, easedProgress));
            yield return null;
        }

        SetBackgroundAlphas(targetOutsideAlpha, targetCaveAlpha);
        FinishBackgroundTransition(finishInsideCave);
        backgroundTransition = null;
    }

    private void FinishBackgroundTransition(bool insideCave)
    {
        outsideBackground.SetActive(!insideCave);
        caveBackground.SetActive(insideCave);
    }

    private void CacheBackgroundRenderers()
    {
        // Keeping both groups active while caching also lets inactive cave
        // background components create their tiled SpriteRenderer children.
        outsideBackground.SetActive(true);
        caveBackground.SetActive(true);

        forestRenderers ??= outsideBackground.GetComponentsInChildren<SpriteRenderer>(true);
        caveRenderers = caveBackground.GetComponentsInChildren<SpriteRenderer>(true);
        outsideBaseColors = CaptureColors(forestRenderers);
        caveBaseColors = CaptureColors(caveRenderers);
    }

    private static Color[] CaptureColors(SpriteRenderer[] renderers)
    {
        Color[] colors = new Color[renderers.Length];
        for (int index = 0; index < renderers.Length; index++)
        {
            colors[index] = renderers[index] != null ? renderers[index].color : Color.white;
        }
        return colors;
    }

    private void SetBackgroundAlphas(float outside, float cave)
    {
        outsideAlpha = Mathf.Clamp01(outside);
        caveAlpha = Mathf.Clamp01(cave);
        ApplyAlpha(forestRenderers, outsideBaseColors, outsideAlpha);
        ApplyAlpha(caveRenderers, caveBaseColors, caveAlpha);
    }

    private static void ApplyAlpha(SpriteRenderer[] renderers, Color[] baseColors, float alpha)
    {
        if (renderers == null || baseColors == null)
            return;

        int count = Mathf.Min(renderers.Length, baseColors.Length);
        for (int index = 0; index < count; index++)
        {
            SpriteRenderer spriteRenderer = renderers[index];
            if (spriteRenderer == null)
                continue;

            Color color = baseColors[index];
            color.a *= alpha;
            spriteRenderer.color = color;
        }
    }

    private void CreateForestBackgroundMask()
    {
        if (forestMaskObject != null)
            return;

        forestRenderers = outsideBackground.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer spriteRenderer in forestRenderers)
        {
            spriteRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        }

        Texture2D whiteTexture = Texture2D.whiteTexture;
        forestMaskSprite = Sprite.Create(
            whiteTexture,
            new Rect(0f, 0f, whiteTexture.width, whiteTexture.height),
            new Vector2(0.5f, 0.5f),
            whiteTexture.width);
        forestMaskSprite.name = "ForestBackgroundBoundaryMaskSprite";

        forestMaskObject = new GameObject("ForestBackgroundBoundaryMask");
        SpriteMask spriteMask = forestMaskObject.AddComponent<SpriteMask>();
        spriteMask.sprite = forestMaskSprite;
        spriteMask.isCustomRangeActive = true;
        spriteMask.frontSortingLayerID = 0;
        spriteMask.frontSortingOrder = 100;
        spriteMask.backSortingLayerID = 0;
        spriteMask.backSortingOrder = -100;

        float maskCenterX = caveIsToRight
            ? crossingX - forestMaskWidth * 0.5f
            : crossingX + forestMaskWidth * 0.5f;

        forestMaskObject.transform.position = new Vector3(
            maskCenterX,
            entranceRenderer.bounds.center.y,
            0f);
        forestMaskObject.transform.localScale = new Vector3(
            forestMaskWidth,
            forestMaskHeight,
            1f);
    }

    private void OnDestroy()
    {
        ApplyAlpha(forestRenderers, outsideBaseColors, 1f);
        ApplyAlpha(caveRenderers, caveBaseColors, 1f);

        if (forestRenderers != null)
        {
            foreach (SpriteRenderer spriteRenderer in forestRenderers)
            {
                if (spriteRenderer != null)
                    spriteRenderer.maskInteraction = SpriteMaskInteraction.None;
            }
        }

        if (forestMaskObject != null)
            Destroy(forestMaskObject);

        if (forestMaskSprite != null)
            Destroy(forestMaskSprite);
    }
}
