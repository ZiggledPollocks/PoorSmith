using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(1100)]
[RequireComponent(typeof(Collider2D))]
public class AssimilatelZone : MonoBehaviour
{
    private const string StormZoneName = "StormAssimilatelZone";
    private const string StormBackgroundResourceName = "StormThemeBackground";

    [Tooltip("Assimilate(1)을 호출하는 간격(초)")]
    [SerializeField, Min(0.01f)] private float assimilateAmountPerTick = 1f;

    private readonly List<SpriteRenderer> defaultBackgroundRenderers = new();
    private readonly List<bool> defaultBackgroundEnabledStates = new();
    private PlayerAssimilate currentPlayerAssimilation;
    private float elapsedTime;
    private int playerColliderCount;
    private Camera themeCamera;
    private GameObject stormBackgroundObject;
    private SpriteRenderer stormBackgroundRenderer;
    private bool stormBackgroundActive;
    private bool loggedMissingStormBackground;
    private bool? portalThemeOverride;

    // Portal travel selects the theme independently of assimilation trigger timing.
    // Trigger callbacks still control assimilation, but cannot undo the portal theme.
    public static void ApplyPortalTheme(bool insideStorm)
    {
        foreach (AssimilatelZone zone in FindObjectsByType<AssimilatelZone>(FindObjectsSortMode.None))
        {
            if (!zone.IsStormThemeZone)
                continue;

            zone.portalThemeOverride = insideStorm;
            zone.SetStormBackgroundActive(insideStorm);
        }
    }

    private void Update()
    {
        if (currentPlayerAssimilation == null)
            return;

        elapsedTime += Time.deltaTime;
        float intervalSeconds = Mathf.Max(0.01f, assimilateAmountPerTick);

        while (elapsedTime >= intervalSeconds)
        {
            elapsedTime -= intervalSeconds;
            currentPlayerAssimilation.Assimilate(1);
        }
    }

    private void LateUpdate()
    {
        if (stormBackgroundActive)
            FitStormBackgroundToCamera();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerAssimilate assimilation =
            other.GetComponentInParent<PlayerAssimilate>();

        if (assimilation == null)
            return;

        if (currentPlayerAssimilation != null &&
            currentPlayerAssimilation != assimilation)
            return;

        if (currentPlayerAssimilation == assimilation)
        {
            playerColliderCount++;
            return;
        }

        currentPlayerAssimilation = assimilation;
        playerColliderCount = 1;
        elapsedTime = 0f;

        if (IsStormThemeZone)
            SetStormBackgroundActive(portalThemeOverride ?? true);

        Debug.Log("동화 구역 진입");
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerAssimilate assimilation =
            other.GetComponentInParent<PlayerAssimilate>();

        if (assimilation == null || assimilation != currentPlayerAssimilation)
            return;

        playerColliderCount = Mathf.Max(0, playerColliderCount - 1);

        if (playerColliderCount > 0)
            return;

        currentPlayerAssimilation = null;
        elapsedTime = 0f;

        if (IsStormThemeZone)
            SetStormBackgroundActive(portalThemeOverride ?? false);

        Debug.Log("동화 구역 이탈");
    }

    private bool IsStormThemeZone => gameObject.name == StormZoneName;

    private void SetStormBackgroundActive(bool active)
    {
        if (active == stormBackgroundActive)
            return;

        if (active)
        {
            if (!EnsureStormBackground())
                return;

            CacheAndHideDefaultBackgrounds();
            stormBackgroundActive = true;
            FitStormBackgroundToCamera();
            stormBackgroundObject.SetActive(true);
            return;
        }

        stormBackgroundActive = false;
        if (stormBackgroundObject != null)
            stormBackgroundObject.SetActive(false);
        RestoreDefaultBackgrounds();
    }

    private bool EnsureStormBackground()
    {
        if (stormBackgroundObject != null && stormBackgroundRenderer != null)
            return true;

        GameObject backgroundPrefab = Resources.Load<GameObject>(StormBackgroundResourceName);
        if (backgroundPrefab == null)
        {
            if (!loggedMissingStormBackground)
            {
                Debug.LogWarning(
                    $"Resources/{StormBackgroundResourceName} 배경 프리팹을 찾을 수 없습니다.",
                    this);
                loggedMissingStormBackground = true;
            }
            return false;
        }

        stormBackgroundObject = Instantiate(backgroundPrefab);
        stormBackgroundObject.name = "[Runtime] StormBackground";
        stormBackgroundObject.hideFlags = HideFlags.DontSave;
        stormBackgroundRenderer = stormBackgroundObject.GetComponent<SpriteRenderer>();
        if (stormBackgroundRenderer == null || stormBackgroundRenderer.sprite == null)
        {
            Debug.LogWarning("바람 테마 배경 프리팹에 SpriteRenderer 또는 Sprite가 없습니다.", this);
            Destroy(stormBackgroundObject);
            stormBackgroundObject = null;
            stormBackgroundRenderer = null;
            return false;
        }

        stormBackgroundObject.SetActive(false);
        return true;
    }

    private void CacheAndHideDefaultBackgrounds()
    {
        RestoreDefaultBackgrounds();
        defaultBackgroundRenderers.Clear();
        defaultBackgroundEnabledStates.Clear();

        InfiniteBackground2D[] backgrounds = FindObjectsByType<InfiniteBackground2D>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (InfiniteBackground2D background in backgrounds)
        {
            SpriteRenderer[] renderers = background.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (SpriteRenderer spriteRenderer in renderers)
            {
                if (spriteRenderer == null)
                    continue;

                defaultBackgroundRenderers.Add(spriteRenderer);
                defaultBackgroundEnabledStates.Add(spriteRenderer.enabled);
                spriteRenderer.enabled = false;
            }
        }
    }

    private void RestoreDefaultBackgrounds()
    {
        int count = Mathf.Min(
            defaultBackgroundRenderers.Count,
            defaultBackgroundEnabledStates.Count);

        for (int index = 0; index < count; index++)
        {
            SpriteRenderer spriteRenderer = defaultBackgroundRenderers[index];
            if (spriteRenderer != null)
                spriteRenderer.enabled = defaultBackgroundEnabledStates[index];
        }

        defaultBackgroundRenderers.Clear();
        defaultBackgroundEnabledStates.Clear();
    }

    private void FitStormBackgroundToCamera()
    {
        if (stormBackgroundObject == null || stormBackgroundRenderer == null)
            return;

        themeCamera ??= Camera.main;
        if (themeCamera == null || !themeCamera.orthographic)
            return;

        Sprite sprite = stormBackgroundRenderer.sprite;
        Vector2 spriteSize = sprite.bounds.size;
        if (spriteSize.x <= 0f || spriteSize.y <= 0f)
            return;

        float viewHeight = themeCamera.orthographicSize * 2f;
        float viewWidth = viewHeight * themeCamera.aspect;
        float scale = Mathf.Max(viewWidth / spriteSize.x, viewHeight / spriteSize.y) * 1.01f;
        stormBackgroundObject.transform.localScale = Vector3.one * scale;

        Vector3 cameraPosition = themeCamera.transform.position;
        Vector2 scaledCenter = (Vector2)sprite.bounds.center * scale;
        stormBackgroundObject.transform.position = new Vector3(
            cameraPosition.x - scaledCenter.x,
            cameraPosition.y - scaledCenter.y,
            0f);
    }

    private void OnDisable()
    {
        portalThemeOverride = null;
        SetStormBackgroundActive(false);
        currentPlayerAssimilation = null;
        elapsedTime = 0f;
        playerColliderCount = 0;
    }

    private void OnDestroy()
    {
        RestoreDefaultBackgrounds();
        if (stormBackgroundObject != null)
            Destroy(stormBackgroundObject);
    }

    private void OnValidate()
    {
        assimilateAmountPerTick = Mathf.Max(0.01f, assimilateAmountPerTick);
    }
}
