// [코드 지도] CameraLookAhead: 플레이어가 향하는 쪽과 수직 이동 방향의 공간을 카메라가 미리 보여주도록 CinemachinePositionComposer를 제어한다. 카메라 Transform을 직접 이동시키지 않고 TargetOffset과 Damping을 바꾼다. 수평은 입력, 수직은 실제 속도를 사용한다.
// 주요 함수: Update, SetVerticalDamping, Awake
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/World/Camera/CameraLookAhead.cs.md

using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(CinemachinePositionComposer))]
public class CameraLookAhead : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerInputHandler inputHandler;
    [SerializeField] private Rigidbody2D playerRigidbody;
    [SerializeField] private CinemachinePositionComposer positionComposer;

    [Header("Look Ahead")]
    [SerializeField, Min(0f)] private float horizontalOffset = 2.5f;
    [FormerlySerializedAs("directNionChangeSmoothTime")]
    [SerializeField, Min(0.01f)] private float directionChangeSmoothTime = 0.2f;
    [SerializeField, Min(0.01f)] private float returnToCenterSmoothTime = 0.35f;
    [SerializeField, Range(0f, 1f)] private float inputThreshold = 0.01f;

    [Header("Vertical Look Ahead")]
    [SerializeField, Min(0f)] private float upwardOffset = 1.75f;
    [SerializeField, Min(0f)] private float downwardOffset = 2.5f;
    [SerializeField, Min(0f)] private float verticalVelocityThreshold = 0.5f;
    [SerializeField, Min(0.01f)] private float velocityForMaximumOffset = 8f;
    [SerializeField, Min(0.01f)] private float verticalLookAheadSmoothTime = 0.32f;
    [SerializeField, Min(0.01f)] private float verticalReturnToCenterSmoothTime = 0.45f;
    [SerializeField, Min(0.01f)] private float maximumVerticalOffsetSpeed = 2f;
    [SerializeField, Range(0.1f, 1f)] private float fallVerticalDampingMultiplier = 0.8f;
    [SerializeField, Min(0.01f)] private float fallFollowEnterSmoothTime = 0.25f;
    [SerializeField, Min(0.01f)] private float fallFollowExitSmoothTime = 0.45f;

    [Header("Bow Aim Camera")]
    [SerializeField, Range(0f, 1f)] private float bowAimOffsetFraction = 0.35f;
    [SerializeField, Min(0f)] private float bowAimMaxHorizontalOffset = 3f;
    [SerializeField, Min(0f)] private float bowAimMaxVerticalOffset = 1.5f;
    [SerializeField, Range(0.5f, 1f)] private float bowAimZoomFactor = 0.9f;
    [SerializeField, Min(0.01f)] private float bowAimSmoothTime = 0.22f;

    private Vector3 defaultTargetOffset;
    private float currentHorizontalOffset;
    private float horizontalOffsetVelocity;
    private float currentVerticalOffset;
    private float verticalOffsetVelocity;
    private Vector3 defaultPositionDamping;
    private float currentVerticalDamping;
    private float verticalDampingVelocity;
    private bool wasFalling;
    private CinemachineCamera bowCamera;
    private float defaultOrthographicSize;
    private float zoomVelocity;
    private bool bowAiming;
    private Vector2 bowAimPoint;

    public void SetBowAim(Vector2 worldPoint)
    {
        bowAimPoint = worldPoint;
        bowAiming = true;
    }

    public void ClearBowAim() => bowAiming = false;

    // 핵심 분기: positionComposer == null 판정.
    // 상태 변경: positionComposer 갱신.
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
        bowCamera = GetComponent<CinemachineCamera>();
        if (bowCamera != null)
            defaultOrthographicSize = bowCamera.Lens.OrthographicSize;
        defaultPositionDamping = positionComposer.Damping;
        currentVerticalDamping = defaultPositionDamping.y;
        currentHorizontalOffset = defaultTargetOffset.x;
        currentVerticalOffset = defaultTargetOffset.y;
    }

    // 핵심 분기: inputHandler == null || positionComposer == null 판정.
    // 상태 변경: currentHorizontalOffset 갱신.
    // 다음 연결: CameraLookAhead.SetVerticalDamping(bool) 호출.
    private void Update()
    {
        if (inputHandler == null || positionComposer == null)
            return;

        float horizontalInput = inputHandler.MoveInput.x;
        bool isMovingHorizontally = Mathf.Abs(horizontalInput) > inputThreshold;

        Vector2 playerPosition = playerRigidbody != null ? playerRigidbody.position :
            (Vector2)inputHandler.transform.position;
        float targetOffset = bowAiming
            ? defaultTargetOffset.x + Mathf.Clamp((bowAimPoint.x - playerPosition.x) * bowAimOffsetFraction,
                -bowAimMaxHorizontalOffset, bowAimMaxHorizontalOffset)
            : isMovingHorizontally
                ? defaultTargetOffset.x + Mathf.Sign(horizontalInput) * horizontalOffset
                : defaultTargetOffset.x;

        float smoothTime = bowAiming ? bowAimSmoothTime : isMovingHorizontally
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

        if (bowAiming)
        {
            targetVerticalOffset += Mathf.Clamp((bowAimPoint.y - playerPosition.y) * bowAimOffsetFraction,
                -bowAimMaxVerticalOffset, bowAimMaxVerticalOffset);
        }
        else if (playerRigidbody != null)
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

        float verticalSmoothTime = bowAiming ? bowAimSmoothTime : isMovingVertically
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
        if (bowCamera != null && defaultOrthographicSize > 0f)
        {
            float targetSize = defaultOrthographicSize * (bowAiming ? bowAimZoomFactor : 1f);
            bowCamera.Lens.OrthographicSize = Mathf.SmoothDamp(
                bowCamera.Lens.OrthographicSize, targetSize, ref zoomVelocity, bowAimSmoothTime);
        }
    }

    // 핵심 분기: isFalling != wasFalling 판정.
    // 상태 변경: verticalDampingVelocity 갱신.
    private void SetVerticalDamping(bool isFalling)
    {
        if (isFalling != wasFalling)
        {
            // Do not carry the previous damping transition's momentum across the apex/landing.
            verticalDampingVelocity = 0f;
            wasFalling = isFalling;
        }

        // Zero damping makes the camera snap to the player as soon as falling starts.
        // Keep most of the normal damping so the player remains visible without a hard acceleration.
        float targetDamping = isFalling
            ? Mathf.Max(0.01f, defaultPositionDamping.y * fallVerticalDampingMultiplier)
            : defaultPositionDamping.y;
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

        if (bowCamera != null && defaultOrthographicSize > 0f)
            bowCamera.Lens.OrthographicSize = defaultOrthographicSize;

        horizontalOffsetVelocity = 0f;
        verticalOffsetVelocity = 0f;
        verticalDampingVelocity = 0f;
        currentVerticalDamping = defaultPositionDamping.y;
        wasFalling = false;
        zoomVelocity = 0f;
        bowAiming = false;
    }
}
