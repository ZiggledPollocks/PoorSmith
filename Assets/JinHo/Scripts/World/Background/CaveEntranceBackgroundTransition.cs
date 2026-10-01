// [코드 지도] CaveEntranceBackgroundTransition: 플레이어가 입구의 x 경계를 넘으면 숲/동굴 배경을 교차 페이드한다. 숲은 SpriteMask로 입구 바깥쪽에만 보이도록 제한한다. 포털 순간이동은 일반 이동 페이드를 생략하고 즉시 상태를 동기화한다.
// 주요 함수: CreateForestBackgroundMask, FadeFieldCrossing, ApplyBackgroundState
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/World/Background/CaveEntranceBackgroundTransition.cs.md

using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class CaveEntranceBackgroundTransition : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private SpriteRenderer entranceRenderer;
    [SerializeField] private GameObject outsideBackground;
    [SerializeField] private GameObject caveBackground;
    [SerializeField] private Renderer[] fieldCaveInteriorArt;

    [Header("Crossing")]
    [SerializeField] private bool caveIsToRight = true;
    [SerializeField] private bool allowReturnToOutside = true;
    [SerializeField, Min(0f)] private float returnHysteresis = 0.15f;
    [SerializeField] private float crossingOffsetX = -1f;

    [Header("Optional Cave Depth Boundary")]
    [SerializeField] private bool caveAlsoBelowY;
    [SerializeField] private float caveBelowY = -10f;
    [SerializeField, Min(0f)] private float verticalReturnHysteresis = 0.15f;

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
    private bool useFieldCrossingFade;
    private Coroutine fieldCrossing;
    private GameObject fadeCanvas;
    private Image fadeOverlay;
    private float previousTimeScale;
    private bool previousExternalActivity;
    private bool fieldPaused;
    private const float FieldFadeOutSeconds = 0.22f;
    private const float FieldFadeInSeconds = 0.28f;
    private const float FieldCaveLandingOffset = 17f;
    // Exit beside the mouth, after the player has walked back across the upper plateau.
    private const float FieldCaveExitOffset = 2.5f;
    private const float FieldForestLandingOffset = -9f;
    private const float FieldLandingY = 1.5f;

    public bool IsInsideCave => isInsideCave;

    // Field monsters use the same boundary as the player's backdrop switch.
    public bool IsCaveWorldPosition(Vector2 worldPosition)
    {
        SpriteRenderer reference = entranceRenderer != null
            ? entranceRenderer : GetComponent<SpriteRenderer>();
        if (reference == null)
            return false;
        float boundaryX = reference.bounds.center.x + crossingOffsetX;
        float signedDistance = (worldPosition.x - boundaryX) * (caveIsToRight ? 1f : -1f);
        return signedDistance >= 0f ||
               (caveAlsoBelowY && worldPosition.y <= caveBelowY);
    }

    private void Reset()
    {
        entranceRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        Initialize();
    }

    // 핵심 분기: !isInitialized 판정.
    // 다음 연결: CaveEntranceBackgroundTransition.Initialize() 호출.
    private void Update()
    {
        if (!isInitialized)
        {
            Initialize();
        }

        if (!isInitialized)
            return;

        // The field uses authored F interaction gates; proximity alone never travels.
        if (useFieldCrossingFade)
            return;

        float signedDistance = GetSignedDistanceFromEntrance();

        if (!isInsideCave && IsCavePosition(signedDistance))
        {
            ChangeRegion(true);
        }
        else if (isInsideCave && allowReturnToOutside &&
                 signedDistance <= -returnHysteresis &&
                 (!caveAlsoBelowY || player.position.y > caveBelowY + verticalReturnHysteresis))
        {
            ChangeRegion(false);
        }
    }

    private void ChangeRegion(bool insideCave)
    {
        if (useFieldCrossingFade)
            fieldCrossing = StartCoroutine(FadeFieldCrossing(insideCave));
        else
            ApplyBackgroundState(insideCave);
    }

    /// <summary>Called only by the authored F gates on the two sides of the mouth.</summary>
    public bool CanBeginFieldCrossing(bool enterCave)
    {
        if (!isInitialized) Initialize();
        return isInitialized && useFieldCrossingFade && fieldCrossing == null &&
               enterCave != isInsideCave && !GameUIController.BlocksGameplayInput &&
               (!caveAlsoBelowY || player.position.y > caveBelowY + 3f);
    }

    public bool TryBeginFieldCrossing(bool enterCave)
    {
        if (!CanBeginFieldCrossing(enterCave)) return false;
        ChangeRegion(enterCave);
        return true;
    }

    // 핵심 분기: entranceRenderer == null 판정.
    // 상태 변경: entranceRenderer 갱신.
    // 다음 연결: CaveEntranceBackgroundTransition.CreateForestBackgroundMask() 호출.
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
        // The field changes its entire backdrop and camera region while black.
        // Other scenes retain their existing masked, timed background transition.
        useFieldCrossingFade = SceneManager.GetActiveScene().path == FieldSceneTravel.FieldScenePath;
        if (!useFieldCrossingFade)
            CreateForestBackgroundMask();
        CacheBackgroundRenderers();
        ApplyBackgroundState(IsCavePosition(GetSignedDistanceFromEntrance()), true);
        isInitialized = true;
    }

    // 핵심 분기: fieldCrossing != null 판정.
    // 상태 변경: fieldCrossing 갱신.
    // 다음 연결: CaveEntranceBackgroundTransition.RestoreFieldInput() 호출.
    public void RefreshImmediatelyAfterTeleport(bool normalizeSavedEntrance = false)
    {
        if (fieldCrossing != null)
        {
            StopCoroutine(fieldCrossing);
            fieldCrossing = null;
            RestoreFieldInput();
        }
        if (!isInitialized)
            Initialize();

        if (isInitialized)
        {
            if (normalizeSavedEntrance && useFieldCrossingFade &&
                player.position.y > caveBelowY + 3f &&
                GetSignedDistanceFromEntrance() >= 0f &&
                GetSignedDistanceFromEntrance() <= FieldCaveExitOffset)
                MoveFieldPlayer(true);
            ApplyBackgroundState(IsCavePosition(GetSignedDistanceFromEntrance()), true);
        }
    }

    private bool IsCavePosition(float signedDistance)
    {
        return signedDistance >= 0f ||
               (caveAlsoBelowY && player.position.y <= caveBelowY);
    }

    private float GetSignedDistanceFromEntrance()
    {
        float direction = caveIsToRight ? 1f : -1f;
        return (player.position.x - crossingX) * direction;
    }

    // 핵심 분기: useFieldCrossingFade && fieldCaveInteriorArt != null 판정.
    // 상태 변경: isInsideCave 갱신.
    // 다음 연결: CaveEntranceBackgroundTransition.SetBackgroundAlphas(float, float) 호출.
    private void ApplyBackgroundState(bool insideCave, bool immediate = false)
    {
        isInsideCave = insideCave;
        if (useFieldCrossingFade && fieldCaveInteriorArt != null)
        {
            foreach (Renderer art in fieldCaveInteriorArt)
                if (art != null) art.enabled = insideCave;
        }

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

    // 핵심 분기: camera != null 판정.
    // 상태 변경: previousTimeScale 갱신.
    // 다음 연결: CaveEntranceBackgroundTransition.EnsureFadeOverlay() 호출.
    private IEnumerator FadeFieldCrossing(bool insideCave)
    {
        EnsureFadeOverlay();
        previousTimeScale = Time.timeScale;
        previousExternalActivity = GameUIController.ExternalActivity;
        fieldPaused = true;
        GameUIController.ExternalActivity = true;
        Time.timeScale = 0f;
        player.GetComponent<PlayerInputHandler>()?.ClearGameplayInput();
        fadeCanvas.SetActive(true);
        try
        {
            yield return FadeScreen(0f, 1f, FieldFadeOutSeconds);
            MoveFieldPlayer(insideCave);
            ApplyBackgroundState(insideCave, true);
            FindFirstObjectByType<FieldRegionCameraBounds>()?.Refresh();
            CinemachineConfiner2D confiner = FindFirstObjectByType<CinemachineConfiner2D>();
            CinemachineVirtualCameraBase camera =
                confiner != null ? confiner.GetComponent<CinemachineVirtualCameraBase>() : null;
            if (camera != null)
            {
                camera.PreviousStateIsValid = false;
                camera.InternalUpdateCameraState(Vector3.up, -1f);
            }
            // Let Cinemachine and the newly active repeating backdrop settle unseen.
            if (!Application.isBatchMode) yield return new WaitForEndOfFrame();
            yield return null;
            yield return FadeScreen(1f, 0f, FieldFadeInSeconds);
        }
        finally
        {
            RestoreFieldInput();
            fieldCrossing = null;
        }
    }

    private IEnumerator FadeScreen(float from, float to, float duration)
    {
        float elapsed = 0f;
        SetFadeAlpha(from);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetFadeAlpha(Mathf.Lerp(from, to,
                Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration))));
            yield return null;
        }
        SetFadeAlpha(to);
    }

    private void MoveFieldPlayer(bool insideCave)
    {
        Vector3 previous = player.position;
        Vector3 destination = new Vector3(
            crossingX + (insideCave ? FieldCaveLandingOffset : FieldForestLandingOffset),
            FieldLandingY, previous.z);
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.position = destination;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
        player.position = destination;
        Physics2D.SyncTransforms();
        CinemachineCore.OnTargetObjectWarped(player, destination - previous);
        player.GetComponent<PlayerMovement>()?.ResetAfterTeleport();
        player.GetComponent<PlayerInputHandler>()?.ClearGameplayInput();
    }

    private void EnsureFadeOverlay()
    {
        if (fadeCanvas != null) return;
        fadeCanvas = new GameObject("ForestCaveFadeCanvas", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = fadeCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1200;
        GameObject overlay = new GameObject("FadeOverlay", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(fadeCanvas.transform, false);
        RectTransform rect = (RectTransform)overlay.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        fadeOverlay = overlay.GetComponent<Image>();
        fadeOverlay.color = Color.clear;
        fadeOverlay.raycastTarget = true;
        fadeCanvas.SetActive(false);
    }

    private void SetFadeAlpha(float alpha)
    {
        if (fadeOverlay == null) return;
        fadeOverlay.color = new Color(0f, 0f, 0f, alpha);
    }

    private void RestoreFieldInput()
    {
        if (!fieldPaused) return;
        fieldPaused = false;
        Time.timeScale = previousTimeScale;
        GameUIController.ExternalActivity = previousExternalActivity;
        if (player != null)
            player.GetComponent<PlayerInputHandler>()?.ClearGameplayInput();
        if (fadeCanvas != null)
        {
            SetFadeAlpha(0f);
            fadeCanvas.SetActive(false);
        }
    }

    // 상태 변경: elapsed 갱신.
    // 다음 연결: CaveEntranceBackgroundTransition.SetBackgroundAlphas(float, float) 호출.
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

    // 핵심 분기: forestMaskObject != null 판정.
    // 상태 변경: forestRenderers 갱신.
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

    // 핵심 분기: fadeCanvas != null 판정.
    // 상태 변경: art.enabled 갱신.
    // 다음 연결: CaveEntranceBackgroundTransition.RestoreFieldInput() 호출.
    private void OnDestroy()
    {
        RestoreFieldInput();
        if (fadeCanvas != null)
            Destroy(fadeCanvas);
        if (fieldCaveInteriorArt != null)
        {
            foreach (Renderer art in fieldCaveInteriorArt)
                if (art != null) art.enabled = true;
        }
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

    private void OnDisable()
    {
        if (fieldCrossing != null)
        {
            StopCoroutine(fieldCrossing);
            fieldCrossing = null;
        }
        RestoreFieldInput();
    }
}
