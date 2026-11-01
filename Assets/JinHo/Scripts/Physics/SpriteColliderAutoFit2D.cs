// [코드 지도] SpriteColliderAutoFit2D: 스프라이트의 형상 경계를 기준으로 몸 Collider를 한 번 맞춘다. 매 애니메이션 프레임마다 크기를 바꾸지 않아 접촉 떨림을 줄이는 구조다. Configure/Refit로 스킨 교체 뒤 명시적 재계산도 가능하다.
// 주요 함수: Refit, GetVisibleSpriteBounds, ApplyBounds
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/Physics/SpriteColliderAutoFit2D.cs.md

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds a stable, forgiving character body from the visible sprite bounds.
/// The collider is intentionally fitted once instead of following every
/// animation frame: changing a solid collider while it is touching another
/// body is a common source of jitter and apparently random hit ranges.
/// </summary>
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public sealed class SpriteColliderAutoFit2D : MonoBehaviour
{
    private const float MinimumSize = 0.01f;

    [Header("References")]
    [SerializeField] private Collider2D targetCollider;
    [SerializeField] private SpriteRenderer targetRenderer;

    [Header("Forgiving Body Shape")]
    [Tooltip("Horizontal fraction of the visible sprite used by the solid body.")]
    [SerializeField, Range(0.25f, 1f)] private float widthRatio = 0.72f;
    [Tooltip("Vertical fraction of the visible sprite used by the solid body.")]
    [SerializeField, Range(0.25f, 1f)] private float heightRatio = 0.88f;
    [Tooltip("Keeps grounded characters' feet fixed while removing excess space above them.")]
    [SerializeField] private bool anchorToFeet = true;
    [Tooltip("Small gap above the lowest visible pixel, expressed as a fraction of sprite height.")]
    [SerializeField, Range(0f, 0.1f)] private float footInsetRatio = 0.015f;

    private readonly List<Vector2> physicsShape = new();
    private bool hasFitted;

    public static SpriteColliderAutoFit2D Attach(
        GameObject owner,
        Collider2D bodyCollider,
        SpriteRenderer spriteRenderer)
    {
        if (owner == null || bodyCollider == null || spriteRenderer == null)
            return null;

        SpriteColliderAutoFit2D fitter = owner.GetComponent<SpriteColliderAutoFit2D>();
        if (fitter == null)
            fitter = owner.AddComponent<SpriteColliderAutoFit2D>();

        fitter.Configure(bodyCollider, spriteRenderer);
        return fitter;
    }

    public void Configure(Collider2D bodyCollider, SpriteRenderer spriteRenderer)
    {
        targetCollider = bodyCollider;
        targetRenderer = spriteRenderer;
        Refit();
    }

    private void Awake()
    {
        targetCollider ??= GetComponent<Collider2D>();
        targetRenderer ??= GetComponentInChildren<SpriteRenderer>();
        Refit();
    }

    public void FitNow()
    {
        if (!hasFitted)
            Refit();
    }

    /// <summary>
    /// Explicitly rebuilds the stable body. Use this after replacing a
    /// character skin or changing the visual transform, not per frame.
    /// </summary>
    public void Refit()
    {
        if (targetCollider == null || targetRenderer == null || targetRenderer.sprite == null)
            return;

        Matrix4x4 relativeMatrix = targetCollider.transform.worldToLocalMatrix
            * targetRenderer.transform.localToWorldMatrix;
        Bounds spriteBounds = GetVisibleSpriteBounds(targetRenderer.sprite);
        Vector2 min = new(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new(float.NegativeInfinity, float.NegativeInfinity);

        IncludeCorner(spriteBounds.min.x, spriteBounds.min.y, relativeMatrix, ref min, ref max);
        IncludeCorner(spriteBounds.min.x, spriteBounds.max.y, relativeMatrix, ref min, ref max);
        IncludeCorner(spriteBounds.max.x, spriteBounds.min.y, relativeMatrix, ref min, ref max);
        IncludeCorner(spriteBounds.max.x, spriteBounds.max.y, relativeMatrix, ref min, ref max);

        Vector2 visibleSize = max - min;
        Vector2 size = new(
            Mathf.Max(MinimumSize, visibleSize.x * widthRatio),
            Mathf.Max(MinimumSize, visibleSize.y * heightRatio));
        Vector2 offset = (min + max) * 0.5f;

        // Keep the body centred on the character pivot, not on asymmetric
        // pixels such as a held weapon, tail, or attack pose. This also makes
        // the collision range identical when SpriteRenderer.flipX changes.
        offset.x = relativeMatrix.MultiplyPoint3x4(Vector3.zero).x;

        // Terraria-style body hitboxes stay stable and are a little smaller
        // than the artwork. Grounded characters keep the bottom of that body
        // near their feet so that shrinking the box does not make them float.
        Rigidbody2D attachedBody = targetCollider.attachedRigidbody;
        bool isGroundedBody = attachedBody == null || attachedBody.gravityScale > 0.01f;
        if (anchorToFeet && isGroundedBody)
        {
            float bottom = min.y + visibleSize.y * footInsetRatio;
            offset.y = bottom + size.y * 0.5f;
        }

        ApplyBounds(offset, size);
        hasFitted = true;
    }

    private void IncludeCorner(
        float x,
        float y,
        Matrix4x4 relativeMatrix,
        ref Vector2 min,
        ref Vector2 max)
    {
        if (targetRenderer.flipX)
            x = -x;
        if (targetRenderer.flipY)
            y = -y;

        Vector3 point = relativeMatrix.MultiplyPoint3x4(new Vector3(x, y, 0f));
        min = Vector2.Min(min, point);
        max = Vector2.Max(max, point);
    }

    // 핵심 분기: !foundPoint 판정.
    // 상태 변경: min 갱신.
    private Bounds GetVisibleSpriteBounds(Sprite sprite)
    {
        bool foundPoint = false;
        Vector2 min = new(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new(float.NegativeInfinity, float.NegativeInfinity);

        int shapeCount = sprite.GetPhysicsShapeCount();
        for (int shapeIndex = 0; shapeIndex < shapeCount; shapeIndex++)
        {
            physicsShape.Clear();
            sprite.GetPhysicsShape(shapeIndex, physicsShape);
            foreach (Vector2 point in physicsShape)
            {
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
                foundPoint = true;
            }
        }

        if (!foundPoint)
        {
            foreach (Vector2 point in sprite.vertices)
            {
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
                foundPoint = true;
            }
        }

        if (!foundPoint)
            return sprite.bounds;

        Bounds bounds = new((min + max) * 0.5f, max - min);
        return bounds;
    }

    // 상태 변경: capsule.direction 갱신.
    private void ApplyBounds(Vector2 offset, Vector2 size)
    {
        switch (targetCollider)
        {
            case CapsuleCollider2D capsule:
                capsule.direction = size.y >= size.x
                    ? CapsuleDirection2D.Vertical
                    : CapsuleDirection2D.Horizontal;
                capsule.offset = offset;
                capsule.size = size;
                break;

            case BoxCollider2D box:
                box.offset = offset;
                box.size = size;
                break;

            case CircleCollider2D circle:
                circle.offset = offset;
                circle.radius = Mathf.Min(size.x, size.y) * 0.5f;
                break;
        }
    }

    private void OnValidate()
    {
        widthRatio = Mathf.Clamp(widthRatio, 0.25f, 1f);
        heightRatio = Mathf.Clamp(heightRatio, 0.25f, 1f);
        footInsetRatio = Mathf.Clamp(footInsetRatio, 0f, 0.1f);
        hasFitted = false;
    }
}
