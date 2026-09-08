using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
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

    private readonly HashSet<PlayerMovement> players = new();

    public float RiseSpeed => riseSpeed;
    public float RiseAcceleration => riseAcceleration;

    private void Awake()
    {
        ConfigureTrigger();
    }

    public float GetTargetCenterY(Collider2D playerCollider)
    {
        float playerHalfHeight = playerCollider != null
            ? playerCollider.bounds.extents.y
            : 0f;

        float baseHeight = triggerCollider.bounds.max.y
            - playerHalfHeight
            - topPadding
            - floatAmplitude;

        float floatingOffset =
            Mathf.Sin(Time.time * floatFrequency * Mathf.PI * 2f)
            * floatAmplitude;

        return baseHeight + floatingOffset;
    }

    public float GetDesiredVerticalSpeed(
        float playerCenterY,
        Collider2D playerCollider)
    {
        float heightError = GetTargetCenterY(playerCollider) - playerCenterY;

        if (heightError > 1f)
            return riseSpeed;

        return Mathf.Clamp(
            heightError * floatResponsiveness,
            -maximumFloatSpeed,
            riseSpeed);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        RegisterPlayer(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        RegisterPlayer(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerMovement player = GetPlayerMovement(other);

        if (player == null || !players.Remove(player))
            return;

        player.ExitUpDraft(this);
    }

    private void OnDisable()
    {
        foreach (PlayerMovement player in players)
        {
            if (player != null)
                player.ExitUpDraft(this);
        }

        players.Clear();
    }

    private void OnValidate()
    {
        ConfigureTrigger();
    }

    private void RegisterPlayer(Collider2D other)
    {
        PlayerMovement player = GetPlayerMovement(other);

        if (player == null)
            return;

        players.Add(player);
        player.EnterUpDraft(this);
    }

    private static PlayerMovement GetPlayerMovement(Collider2D other)
    {
        if (other.attachedRigidbody != null)
        {
            PlayerMovement movement =
                other.attachedRigidbody.GetComponent<PlayerMovement>();

            if (movement != null)
                return movement;
        }

        return other.GetComponentInParent<PlayerMovement>();
    }

    private void ConfigureTrigger()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<BoxCollider2D>();

        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }
}
