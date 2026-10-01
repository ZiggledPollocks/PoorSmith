// [코드 지도] FieldOneWayPlatform: 일방통행 발판의 충돌 진입과 통과 규칙을 적용한다.
// 주요 함수: Surface
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/Campaign/Field/FieldOneWayPlatform.cs.md

using UnityEngine;

/// <summary>Marks a field-map surface that supports landing from above only.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlatformEffector2D))]
public sealed class FieldOneWayPlatform : MonoBehaviour
{
    public Collider2D Surface => GetComponent<Collider2D>();
}
