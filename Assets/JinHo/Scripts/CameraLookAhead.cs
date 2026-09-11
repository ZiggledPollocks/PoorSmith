using Unity.Cinemachine;
using UnityEngine;

[RequireComponent(typeof(CinemachinePositionComposer))]
public class CameraLookAhead : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerInputHandler inputHandler;
    [SerializeField] private Rigidbody2D playerRigidbody;
    [SerializeField] private CinemachinePositionComposer positionComposer;

    [Header("Look Ahead")]
    [SerializeField, Min(0f)] private float horizontalOffset = 2.5f;
    [SerializeField, Min(0.01f)] private float directionChangeSmoothTime = 0.2f;
    [SerializeField, Min(0.01f)] private float returnToCenterSmoothTime = 0.35f;
    [SerializeField, Range(0f, 1f)] private float inputThreshold = 0.01f;

    [Header("Vertical Look Ahead")]
    [SerializeField, Min(0f)] private float upwardOffset = 1.75f;
    [SerializeField, Min(0f)] private float downwardOffset = 2.5f;
    [SerializeField, Min(0f)] private float verticalVelocityThreshold = 0.5f;
    [SerializeField, Min(0.01f)] private float velocityForMaximumOffset = 8f;
    [SerializeField, Min(0.01f)] private float verticalLookAheadSmoothTime = 0.22f;
    [SerializeField, Min(0.01f)] private float verticalReturnToCenterSmoothTime = 0.35f;
    [SerializeField, Min(0.01f)] private float maximumVerticalOffsetSpeed = 3f;
    [SerializeField, Min(0.01f)] private float fallFollowEnterSmoothTime = 0.12f;
    [SerializeField, Min(0.01f)] private float fallFollowExitSmoothTime = 0.35f;

    private Vector3 defaultTargetOffset;
    private float currentHorizontalOffset;
    private float horizontalOffsetVelocity;
    private float currentVerticalOffset;
    private float verticalOffsetVelocity;
    private Vector3 defaultPositionDamping;
    private float currentVerticalDamping;
    private float verticalDampingVelocity;

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

        if (playerRigidbody == null && inputHandler != null)
        {
            playerRigidbody = inputHandler.GetComponent<Rigidbody2D>();
        }

        defaultTargetOffset = positionComposer.TargetOffset;
        defaultPositionDamping = positionComposer.Damping;
        currentVerticalDamping = defaultPositionDamping.y;
        currentHorizontalOffset = defaultTargetOffset.x;
        currentVerticalOffset = defaultTargetOffset.y;
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
            ref horizontalOffsetVelocity,
            smoothTime
        );

        float targetVerticalOffset = defaultTargetOffset.y;
        bool isMovingVertically = false;
        bool isFalling = false;

        if (playerRigidbody != null)
        {
            float verticalVelocity = playerRigidbody.linearVelocity.y;
            float absoluteVerticalVelocity = Mathf.Abs(verticalVelocity);
            isFalling = verticalVelocity < -verticalVelocityThreshold;
            isMovingVertically =
                absoluteVerticalVelocity > verticalVelocityThreshold;

            if (isMovingVertically)
            {
                float maximumVelocity = Mathf.Max(
                    verticalVelocityThreshold + 0.01f,
                    velocityForMaximumOffset);
                float offsetStrength = Mathf.InverseLerp(
                    verticalVelocityThreshold,
                    maximumVelocity,
                    absoluteVerticalVelocity);
                float directionalOffset = verticalVelocity > 0f
                    ? upwardOffset
                    : -downwardOffset;

                targetVerticalOffset += directionalOffset * offsetStrength;
            }
        }

        SetVerticalDamping(isFalling);

        float verticalSmoothTime = isMovingVertically
            ? verticalLookAheadSmoothTime
            : verticalReturnToCenterSmoothTime;

        currentVerticalOffset = Mathf.SmoothDamp(
            currentVerticalOffset,
            targetVerticalOffset,
            ref verticalOffsetVelocity,
            verticalSmoothTime,
            maximumVerticalOffsetSpeed
        );

        Vector3 updatedOffset = defaultTargetOffset;
        updatedOffset.x = currentHorizontalOffset;
        updatedOffset.y = currentVerticalOffset;
        positionComposer.TargetOffset = updatedOffset;
    }

    private void SetVerticalDamping(bool isFalling)
    {
        float targetDamping = isFalling ? 0f : defaultPositionDamping.y;
        float smoothTime = isFalling
            ? fallFollowEnterSmoothTime
            : fallFollowExitSmoothTime;

        currentVerticalDamping = Mathf.SmoothDamp(
            currentVerticalDamping,
            targetDamping,
            ref verticalDampingVelocity,
            smoothTime
        );

        Vector3 damping = positionComposer.Damping;
        damping.y = currentVerticalDamping;
        positionComposer.Damping = damping;
    }

    private void OnDisable()
    {
        if (positionComposer != null)
        {
            positionComposer.TargetOffset = defaultTargetOffset;
            positionComposer.Damping = defaultPositionDamping;
        }

        horizontalOffsetVelocity = 0f;
        verticalOffsetVelocity = 0f;
        verticalDampingVelocity = 0f;
        currentVerticalDamping = defaultPositionDamping.y;
    }
}
