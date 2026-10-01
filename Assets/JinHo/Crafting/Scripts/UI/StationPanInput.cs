// [코드 지도] StationPanInput: 설비 화면의 세로 드래그 위치를 제한한다.
// 주요 함수: OnDrag
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Crafting/Scripts/UI/StationPanInput.cs.md

using UnityEngine;
using UnityEngine.EventSystems;

namespace Blacksmith
{
    /// <summary>Keeps drag panning of a crafting station within its visible range.</summary>
    public class StationPanInput : MonoBehaviour, IDragHandler
    {
        public RectTransform target;
        public void OnDrag(PointerEventData e)
        {
            if (target)
            {
                var p = target.anchoredPosition;
                p.y = Mathf.Clamp(p.y + e.delta.y, -65, 65);
                target.anchoredPosition = p;
            }
        }
    }
}