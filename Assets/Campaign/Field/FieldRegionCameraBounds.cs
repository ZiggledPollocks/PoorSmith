// [코드 지도] FieldRegionCameraBounds: 필드 지역에 따른 카메라 이동 한계를 갱신한다.
// 주요 함수: Refresh, CurrentBounds, Awake
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/Campaign/Field/FieldRegionCameraBounds.cs.md

using Unity.Cinemachine;
using UnityEngine;

/// <summary>Uses the forest or cave camera rectangle as the player crosses the entrance.</summary>
[DefaultExecutionOrder(-100)]
public sealed class FieldRegionCameraBounds : MonoBehaviour
{
    [SerializeField] CinemachineConfiner2D confiner;
    [SerializeField] BoxCollider2D forestBounds;
    [SerializeField] BoxCollider2D caveBounds;
    [SerializeField] CaveEntranceBackgroundTransition entrance;

    public Collider2D CurrentBounds => confiner != null ? confiner.BoundingShape2D : null;
    public Bounds MapBounds
    {
        get
        {
            if (forestBounds == null || caveBounds == null)
                return new Bounds(new Vector3(25f, -21f, 0f), new Vector3(184f, 86f, 1f));
            Bounds bounds = forestBounds.bounds;
            bounds.Encapsulate(caveBounds.bounds);
            return bounds;
        }
    }

    void Awake() => Refresh();
    void LateUpdate() => Refresh();

    public void Refresh()
    {
        if (confiner == null || forestBounds == null || caveBounds == null || entrance == null) return;
        Collider2D next = entrance.IsInsideCave ? caveBounds : forestBounds;
        if (confiner.BoundingShape2D == next) return;
        confiner.BoundingShape2D = next;
        confiner.InvalidateBoundingShapeCache();
    }
}
