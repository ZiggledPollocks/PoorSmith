using UnityEngine;
using UnityEngine.InputSystem;

// Camera-only navigation for the map geometry test scene. No gameplay objects are spawned.
[RequireComponent(typeof(Camera))]
public sealed class FieldMapPreviewCamera : MonoBehaviour
{
    [SerializeField] private Vector2 lowerLeft = new Vector2(-100f, -110f);
    [SerializeField] private Vector2 upperRight = new Vector2(160f, 32f);
    [SerializeField] private float moveSpeed = 22f;
    [SerializeField] private float minSize = 6f;
    [SerializeField] private float maxSize = 15f;

    private Camera sceneCamera;

    private void Awake() => sceneCamera = GetComponent<Camera>();

    private void LateUpdate()
    {
        var keyboard = Keyboard.current;
        var direction = Vector2.zero;
        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) direction.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) direction.x += 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) direction.y += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) direction.y -= 1f;
        }

        var scroll = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
        if (Mathf.Abs(scroll) > 0.01f)
            sceneCamera.orthographicSize = Mathf.Clamp(sceneCamera.orthographicSize - Mathf.Sign(scroll), minSize, maxSize);

        var position = transform.position + (Vector3)(direction.normalized * moveSpeed * Time.unscaledDeltaTime);
        var halfHeight = sceneCamera.orthographicSize;
        var halfWidth = halfHeight * sceneCamera.aspect;
        position.x = Mathf.Clamp(position.x, lowerLeft.x + halfWidth, upperRight.x - halfWidth);
        position.y = Mathf.Clamp(position.y, lowerLeft.y + halfHeight, upperRight.y - halfHeight);
        transform.position = new Vector3(position.x, position.y, -10f);
    }
}
