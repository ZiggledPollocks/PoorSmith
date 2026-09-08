using Unity.Cinemachine;
using UnityEngine;

[RequireComponent(typeof(CinemachinePositionComposer))]
public class CameraLookAhead : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerInputHandler inputHandler;
    [SerializeField] private CinemachinePositionComposer positionComposer;

    [Header("Look Ahead")]
    [SerializeField, Min(0f)] private float horizontalOffset = 2.5f;
    [SerializeField, Min(0.01f)] private float directionChangeSmoothTime = 0.2f;
    [SerializeField, Min(0.01f)] private float returnToCenterSmoothTime = 0.35f;
    [SerializeField, Range(0f, 1f)] private float inputThreshold = 0.01f;

    private Vector3 defaultTargetOffset;
    private float currentHorizontalOffset;
    private float offsetVelocity;

    private void Awake()
    {
        if (positionComposer == null)
        {
            positionComposer = GetComponent<CinemachinePositionComposer>();
        }

        if (inputHandler == null)
        {
            inputHandler = FindFirstObjectByType<PlayerInputHandler>();
        }

        defaultTargetOffset = positionComposer.TargetOffset;
        currentHorizontalOffset = defaultTargetOffset.x;
    }

    private void Update()
    {
        if (inputHandler == null || positionComposer == null)
            return;

        float horizontalInput = inputHandler.MoveInput.x;
        bool isMovingHorizontally = Mathf.Abs(horizontalInput) > inputThreshold;

        float targetOffset = isMovingHorizontally
            ? defaultTargetOffset.x + Mathf.Sign(horizontalInput) * horizontalOffset
            : defaultTargetOffset.x;

        float smoothTime = isMovingHorizontally
            ? directionChangeSmoothTime
            : returnToCenterSmoothTime;

        currentHorizontalOffset = Mathf.SmoothDamp(
            currentHorizontalOffset,
            targetOffset,
            ref offsetVelocity,
            smoothTime
        );

        Vector3 updatedOffset = defaultTargetOffset;
        updatedOffset.x = currentHorizontalOffset;
        positionComposer.TargetOffset = updatedOffset;
    }

    private void OnDisable()
    {
        if (positionComposer != null)
        {
            positionComposer.TargetOffset = defaultTargetOffset;
        }

        offsetVelocity = 0f;
    }
}
