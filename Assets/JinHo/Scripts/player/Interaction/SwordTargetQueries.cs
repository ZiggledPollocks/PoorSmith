// [코드 지도] SwordTargetQueries: 찌르기는 방향을 가진 캡슐, 휘두르기는 원형 후보와 반각 검사로 타격 대상을 정한다. PlayerInteraction은 전략을 선택한 뒤 공통 인터페이스로 사용한다.
// 주요 함수: Find, Includes, Resolve
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/player/Interaction/SwordTargetQueries.cs.md

using UnityEngine;

/// <summary>Selects the hit query for each sword attack style.</summary>
public static class SwordTargetQueries
{
    private static readonly ISwordTargetQuery thrust = new ThrustTargetQuery();
    private static readonly ISwordTargetQuery swing = new SwingTargetQuery();
    public static ISwordTargetQuery Resolve(SwordAttackStyle style) =>
        style == SwordAttackStyle.Thrust ? thrust : swing;
}

public sealed class ThrustTargetQuery : ISwordTargetQuery
{
    public Collider2D[] Find(Vector2 origin, Vector2 direction, ToolData sword, int layerMask)
    {
        float reach = sword.Reach;
        Vector2 capsuleCenter = origin + direction * (reach * 0.5f);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        return Physics2D.OverlapCapsuleAll(
            capsuleCenter,
            new Vector2(reach, sword.ThrustWidth),
            CapsuleDirection2D.Horizontal,
            angle,
            layerMask);
    }
    public bool Includes(Vector2 origin, Vector2 direction, Collider2D collider, ToolData sword) => true;
}

public sealed class SwingTargetQuery : ISwordTargetQuery
{
    public Collider2D[] Find(Vector2 origin, Vector2 direction, ToolData sword, int layerMask)
    {
        return Physics2D.OverlapCircleAll(
            origin,
            sword.Reach,
            layerMask);
    }
    public bool Includes(Vector2 origin, Vector2 direction, Collider2D collider, ToolData sword)
    {
        if (sword.SwordAttackStyle != SwordAttackStyle.Swing) return true;
        Vector2 targetPoint = collider.ClosestPoint(origin);
        Vector2 directionToTarget = targetPoint - origin;

        if (directionToTarget.sqrMagnitude <= Mathf.Epsilon)
            directionToTarget = (Vector2)collider.bounds.center - origin;

        return directionToTarget.sqrMagnitude <= Mathf.Epsilon
            || Vector2.Angle(direction, directionToTarget) <= sword.SwingAngle * 0.5f;
    }
}
