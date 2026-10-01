// [코드 지도] UiHoverOutline: 커서가 UI 요소에 들어왔을 때 강조 테두리를 표시한다.
// 주요 함수: Awake, OnPointerEnter, OnPointerExit
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/Campaign/UI/UiHoverOutline.cs.md

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class UiHoverOutline : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler
{
    Outline outline;
    void Awake(){outline=GetComponent<Outline>()??gameObject.AddComponent<Outline>();outline.effectColor=Color.white;outline.effectDistance=new Vector2(2,-2);outline.enabled=false;}
    public void OnPointerEnter(PointerEventData e){outline.enabled=true;}
    public void OnPointerExit(PointerEventData e){outline.enabled=false;}
    void OnDisable(){if(outline!=null)outline.enabled=false;}
}
