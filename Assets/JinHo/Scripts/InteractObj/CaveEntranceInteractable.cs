using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class CaveEntranceInteractable : MonoBehaviour, IInteractable
{
    [Header("Destination")]
    [SerializeField] private Transform destination;
    [SerializeField] private Vector2 destinationOffset;

    [Header("Camera Bounds")]
    [SerializeField] private CinemachineConfiner2D cameraConfiner;
    [SerializeField] private Collider2D destinationCameraBounds;

    [Header("Fade")]
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.45f;
    [SerializeField, Min(0f)] private float blackHoldDuration = 0.1f;
    [SerializeField, Min(0f)] private float fadeInDuration = 0.45f;
    [SerializeField] private int fadeSortingOrder = 1000;

    private GameObject fadeCanvasObject;
    private Image fadeImage;
    private bool isTransitioning;
    public bool IsTransitioning => isTransitioning;

    public bool CanInteract()
    {
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
        else
        {
            playerTransform.position = targetPosition;
        }

        Physics2D.SyncTransforms();
        CinemachineCore.OnTargetObjectWarped(
            playerTransform,
            targetPosition - previousPosition);
    }

    private void ChangeCameraBounds()
    {
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
