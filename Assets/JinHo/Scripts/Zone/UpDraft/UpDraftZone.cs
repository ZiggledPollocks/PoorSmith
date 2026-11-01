// [코드 지도] UpDraftZone: 트리거 접촉을 MovementZoneContacts에 전달하여 상승기류 모드를 등록하고, 목표 부유 높이에 따른 수직 목표 속도를 계산한다. 비활성화 시 등록을 정리한다.
// 주요 함수: GetTargetCenterY, GetDesiredVerticalSpeed, ConfigureTrigger
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/Zone/UpDraft/UpDraftZone.cs.md

using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
/// <summary>Defines the area that applies an updraft movement effect to the player.</summary>
public class UpDraftZone : MonoBehaviour
{
    [Header("Lift")]
    [SerializeField, Min(0.01f)] private float riseSpeed = 6f;
    [SerializeField, Min(0.01f)] private float riseAcceleration = 18f;

    [Header("Floating")]
    [Tooltip("Trigger 위쪽 경계에서 플레이어가 떨어져 떠 있을 거리")]
    [SerializeField, Min(0f)] private float topPadding = 0.35f;
    [SerializeField, Min(0f)] private float floatAmplitude = 0.12f;
    [SerializeField, Min(0.01f)] private float floatFrequency = 1.5f;
    [SerializeField, Min(0.01f)] private float floatResponsiveness = 5f;
    [SerializeField, Min(0.01f)] private float maximumFloatSpeed = 2f;

    [SerializeField] private BoxCollider2D triggerCollider;

    private MovementZoneContacts playerContacts;

    public float RiseSpeed => riseSpeed;
    public float RiseAcceleration => riseAcceleration;

    private void Awake()
    {
        ConfigureTrigger();
        playerContacts = new MovementZoneContacts(this,
            player => new UpDraftMovementMode(this, player.UpDraftSettings),
            UpDraftMovementMode.Priority, MovementEnvironmentPolicy.Floating);
    }

    private void FixedUpdate()
    {
        if (triggerCollider == null || !triggerCollider.enabled) playerContacts?.Clear();
        else playerContacts?.Prune();
    }

    public float GetTargetCenterY(Collider2D playerCollider)
    {
        float playerHalfHeight = playerCollider != null
            ? playerCollider.bounds.extents.y
            : 0f;

        return GetTargetCenterY(playerHalfHeight, Time.time);
    }

    public float GetTargetCenterY(float playerHalfHeight, float time)
    {
        float baseHeight = triggerCollider.bounds.max.y
            - playerHalfHeight
            - topPadding
            - floatAmplitude;

        float floatingOffset =
            Mathf.Sin(time * floatFrequency * Mathf.PI * 2f)
            * floatAmplitude;

        return baseHeight + floatingOffset;
    }

    public float GetDesiredVerticalSpeed(
        float playerCenterY,
        Collider2D playerCollider)
    {
        return GetDesiredVerticalSpeed(playerCenterY,
            playerCollider != null ? playerCollider.bounds.extents.y : 0f, Time.time);
    }

    public float GetDesiredVerticalSpeed(float playerCenterY, float playerHalfHeight, float time)
    {
        float heightError = GetTargetCenterY(playerHalfHeight, time) - playerCenterY;

        if (heightError > 1f)
            return riseSpeed;

        return Mathf.Clamp(
            heightError * floatResponsiveness,
            -maximumFloatSpeed,
            riseSpeed);
    }

    private void OnTriggerEnter2D(Collider2D other) => RegisterPlayer(other);

    private void OnTriggerStay2D(Collider2D other) => RegisterPlayer(other);

    private void OnTriggerExit2D(Collider2D other) => playerContacts?.Exit(other);

    private void OnDisable() => playerContacts?.Clear();

    private void OnValidate() => ConfigureTrigger();

    private void RegisterPlayer(Collider2D other)
    {
        if (isActiveAndEnabled && triggerCollider != null && triggerCollider.enabled)
            playerContacts?.Stay(other);
    }

    private void ConfigureTrigger()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<BoxCollider2D>();

        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }
}
