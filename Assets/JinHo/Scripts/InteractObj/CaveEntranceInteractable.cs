using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class CaveEntranceInteractable : MonoBehaviour, IInteractable
{
    private const string StormInName = "StormIn";
    private const string StormOutName = "StormOut";
    private const string StormCameraBoundsName = "StormCameraBounds";
    private const string OutsideCameraBoundsName = "camerBounds";

    [Header("Destination")]
    [SerializeField] private Transform destination;
    [SerializeField] private Vector2 destinationOffset;

    [Header("Camera Bounds")]
    [SerializeField] private CinemachineConfiner2D cameraConfiner;
    [SerializeField] private Collider2D destinationCameraBounds;

    [Header("Fade")]
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.15f;
    [SerializeField, Min(0f)] private float blackHoldDuration = 0f;
    [SerializeField, Min(0f)] private float fadeInDuration = 0.15f;
    [SerializeField] private int fadeSortingOrder = 1000;

    private GameObject fadeCanvasObject;
    private Image fadeImage;
    private bool isTransitioning;
    public bool IsTransitioning => isTransitioning;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ConfigureStormPortalPair()
    {
        CaveEntranceInteractable stormIn = EnsureStormPortal(StormInName);
        CaveEntranceInteractable stormOut = EnsureStormPortal(StormOutName);
        if (stormIn == null || stormOut == null)
            return;

        stormIn.SetDestinationIfMissing(stormOut.transform);
        stormOut.SetDestinationIfMissing(stormIn.transform);
        ConfigureStormCameraBounds(stormIn, stormOut);
    }

    public bool CanInteract()
    {
        TryResolveStormDestination();
        return !isTransitioning && destination != null;
    }

    public bool CanUseTool(ToolData toolData)
    {
        return true;
    }

    public void Interact(PlayerInteraction interactionContext)
    {
        if (!CanInteract() || interactionContext == null)
            return;

        StartCoroutine(MovePlayerRoutine(interactionContext));
    }

    private IEnumerator MovePlayerRoutine(PlayerInteraction interactionContext)
    {
        isTransitioning = true;
        EnsureFadeCanvas();
        fadeCanvasObject.SetActive(true);

        GameObject player = interactionContext.gameObject;
        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        Rigidbody2D rigidbody2D = player.GetComponent<Rigidbody2D>();

        bool movementWasEnabled = movement != null && movement.enabled;
        bool interactionWasEnabled = interactionContext.enabled;

        if (movement != null)
            movement.enabled = false;

        interactionContext.enabled = false;

        if (rigidbody2D != null)
            rigidbody2D.linearVelocity = Vector2.zero;

        yield return Fade(0f, 1f, fadeOutDuration);

        ChangeCameraBounds();
        MovePlayer(player.transform, rigidbody2D);

        if (gameObject.name is StormInName or StormOutName)
        {
            // Complete background changes while the screen is still black.
            // Do not wait for OnTriggerEnter/Exit or the walking crossfade.
            foreach (CaveEntranceBackgroundTransition background in
                     FindObjectsByType<CaveEntranceBackgroundTransition>(FindObjectsSortMode.None))
                background.RefreshImmediatelyAfterTeleport();

            AssimilatelZone.ApplyPortalTheme(destination.name == StormInName);
        }

        // Keep the overlay opaque until the brain has rendered the new camera state.
        yield return new WaitForEndOfFrame();

        if (blackHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(blackHoldDuration);

        yield return Fade(1f, 0f, fadeInDuration);

        fadeCanvasObject.SetActive(false);

        if (movement != null)
            movement.enabled = movementWasEnabled;

        interactionContext.enabled = interactionWasEnabled;
        isTransitioning = false;
    }

    private void MovePlayer(Transform playerTransform, Rigidbody2D rigidbody2D)
    {
        Vector3 previousPosition = playerTransform.position;
        Vector3 targetPosition = destination.position + (Vector3)destinationOffset;
        targetPosition.z = playerTransform.position.z;

        if (rigidbody2D != null)
        {
            rigidbody2D.linearVelocity = Vector2.zero;
            rigidbody2D.angularVelocity = 0f;
            rigidbody2D.position = new Vector2(targetPosition.x, targetPosition.y);
        }
        // Also reset the rendered transform: Rigidbody2D interpolation can otherwise
        // leave Cinemachine reading the pre-teleport position for this frame.
        playerTransform.position = targetPosition;

        Physics2D.SyncTransforms();
        CinemachineCore.OnTargetObjectWarped(
            playerTransform,
            targetPosition - previousPosition);

        if (cameraConfiner != null)
        {
            CinemachineVirtualCameraBase virtualCamera =
                cameraConfiner.GetComponent<CinemachineVirtualCameraBase>();
            if (virtualCamera != null)
            {
                // A portal is a cut, not a long follow movement. Reset both composer
                // damping and the confiner's correction from the previous region.
                virtualCamera.PreviousStateIsValid = false;
                virtualCamera.InternalUpdateCameraState(Vector3.up, -1f);
            }
        }
    }

    private void TryResolveStormDestination()
    {
        if (destination != null)
            return;

        string destinationName = gameObject.name switch
        {
            StormInName => StormOutName,
            StormOutName => StormInName,
            _ => null
        };

        if (destinationName == null)
            return;

        Transform destinationTransform = FindSceneTransform(destinationName);
        if (destinationTransform != null)
            destination = destinationTransform;
    }

    private void SetDestinationIfMissing(Transform target)
    {
        if (destination != null)
            return;

        destination = target;
        if (gameObject.name is StormInName or StormOutName)
            destinationOffset = Vector2.zero;
    }

    private static CaveEntranceInteractable EnsureStormPortal(string objectName)
    {
        Transform portalTransform = FindSceneTransform(objectName);
        if (portalTransform == null)
            return null;

        GameObject portalObject = portalTransform.gameObject;
        int interactableLayer = LayerMask.NameToLayer("Interactable");
        if (interactableLayer >= 0)
            portalObject.layer = interactableLayer;

        Collider2D portalCollider = portalObject.GetComponent<Collider2D>();
        if (portalCollider == null)
        {
            BoxCollider2D boxCollider = portalObject.AddComponent<BoxCollider2D>();
            SpriteRenderer spriteRenderer = portalObject.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null && spriteRenderer.sprite != null)
                boxCollider.size = spriteRenderer.sprite.bounds.size;
            portalCollider = boxCollider;
        }
        portalCollider.isTrigger = true;

        CaveEntranceInteractable portal = portalObject.GetComponent<CaveEntranceInteractable>();
        if (portal == null)
            portal = portalObject.AddComponent<CaveEntranceInteractable>();
        return portal;
    }

    private static void ConfigureStormCameraBounds(
        CaveEntranceInteractable stormIn,
        CaveEntranceInteractable stormOut)
    {
        CinemachineConfiner2D confiner = FindFirstObjectByType<CinemachineConfiner2D>();
        if (confiner == null)
            return;

        Collider2D stormBounds = FindSceneCollider(StormCameraBoundsName);
        Collider2D outsideBounds = FindSceneCollider(OutsideCameraBoundsName);
        if (outsideBounds == null && confiner.BoundingShape2D != stormBounds)
            outsideBounds = confiner.BoundingShape2D;

        // StormOut is the portal on the outside. Entering it moves the player
        // to StormIn, so the destination must use the wind-theme bounds.
        stormOut.SetCameraBoundsIfMissing(confiner, stormBounds);
        stormIn.SetCameraBoundsIfMissing(confiner, outsideBounds);
    }

    private void SetCameraBoundsIfMissing(
        CinemachineConfiner2D confiner,
        Collider2D bounds)
    {
        cameraConfiner ??= confiner;
        if (destinationCameraBounds == null)
            destinationCameraBounds = bounds;
    }

    private static Collider2D FindSceneCollider(string objectName)
    {
        Transform target = FindSceneTransform(objectName);
        return target != null ? target.GetComponent<Collider2D>() : null;
    }

    private static Transform FindSceneTransform(string objectName)
    {
        Transform[] transforms = FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (Transform candidate in transforms)
        {
            if (candidate.name == objectName)
                return candidate;
        }

        return null;
    }

    private void ChangeCameraBounds()
    {
        // Resolve again at interaction time, including after an editor domain reload.
        if (gameObject.name is StormInName or StormOutName)
        {
            if (cameraConfiner == null)
                cameraConfiner = FindFirstObjectByType<CinemachineConfiner2D>();
            if (destinationCameraBounds == null)
                destinationCameraBounds = FindSceneCollider(
                    gameObject.name == StormOutName
                        ? StormCameraBoundsName : OutsideCameraBoundsName);
        }

        if (cameraConfiner == null || destinationCameraBounds == null)
            return;

        cameraConfiner.BoundingShape2D = destinationCameraBounds;
        cameraConfiner.InvalidateBoundingShapeCache();
    }

    private IEnumerator Fade(float startAlpha, float endAlpha, float duration)
    {
        if (duration <= 0f)
        {
            SetFadeAlpha(endAlpha);
            yield break;
        }

        float elapsed = 0f;
        SetFadeAlpha(startAlpha);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            SetFadeAlpha(Mathf.Lerp(startAlpha, endAlpha, progress));
            yield return null;
        }

        SetFadeAlpha(endAlpha);
    }

    private void EnsureFadeCanvas()
    {
        if (fadeCanvasObject != null)
            return;

        fadeCanvasObject = new GameObject(
            "CaveTransitionFadeCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        Canvas canvas = fadeCanvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = fadeSortingOrder;

        GameObject overlay = new("FadeOverlay", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(fadeCanvasObject.transform, false);

        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        fadeImage = overlay.GetComponent<Image>();
        fadeImage.color = new Color(0f, 0f, 0f, 0f);
        fadeImage.raycastTarget = true;

        fadeCanvasObject.SetActive(false);
    }

    private void SetFadeAlpha(float alpha)
    {
        Color color = fadeImage.color;
        color.a = alpha;
        fadeImage.color = color;
    }

    private void OnDestroy()
    {
        if (fadeCanvasObject != null)
            Destroy(fadeCanvasObject);
    }
}
