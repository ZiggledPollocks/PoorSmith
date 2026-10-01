// [코드 지도] ISwordTargetQuery: 검의 공격 형상별 후보 충돌체 수집 Find와 최종 포함 판정 Includes를 분리한 계약이다.
// 주요 함수: Find, Includes
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/Assets/ISwordTargetQuery.cs.md

using UnityEngine;

/// <summary>Describes how a sword attack finds and filters targets.</summary>
public interface ISwordTargetQuery
{
    Collider2D[] Find(Vector2 origin, Vector2 direction, ToolData sword, int layerMask);
    bool Includes(Vector2 origin, Vector2 direction, Collider2D collider, ToolData sword);
}
