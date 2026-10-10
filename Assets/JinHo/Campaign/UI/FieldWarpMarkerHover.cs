// [코드 지도] FieldWarpMarkerHover: 워프 지도 표식의 커서 입력과 강조 상태를 처리한다.
// 주요 함수: OnPointerEnter, OnPointerExit, OnDisable
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/Campaign/UI/FieldWarpMarkerHover.cs.md

using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Small pointer emphasis for selectable destinations on the field warp map.</summary>
public sealed class FieldWarpMarkerHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public void OnPointerEnter(PointerEventData eventData) => transform.localScale = Vector3.one * 1.15f;
    public void OnPointerExit(PointerEventData eventData) => transform.localScale = Vector3.one;
    void OnDisable() => transform.localScale = Vector3.one;
}
