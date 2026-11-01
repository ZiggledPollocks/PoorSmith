using UnityEngine;
using UnityEngine.UI;

// A map region owns arrows only after Enter. Esc returns to its surrounding controls.
public sealed class UIKeyboardPanRegion:MonoBehaviour
{
    public ScrollRect scroller;
    public bool OwnsPan {get;set;}
    public string actionLabel="지도 이동";
    bool mapSizing;
    public bool CanPan=>scroller!=null&&scroller.content!=null&&scroller.viewport!=null&&
        (scroller.horizontal&&scroller.content.rect.width>scroller.viewport.rect.width+1||scroller.vertical&&scroller.content.rect.height>scroller.viewport.rect.height+1);
    public static void TextScroll(ScrollRect scroll){if(scroll==null)return;var region=scroll.GetComponent<UIKeyboardPanRegion>()??scroll.gameObject.AddComponent<UIKeyboardPanRegion>();region.scroller=scroll;region.actionLabel="설명 스크롤";}
    [Range(1f,2f)] public float contentScale=1.15f;
    // Only the existing map drawings/markers enter this viewport. No world/camera/discovery data changes.
    public static RectTransform WrapMap(RectTransform frame)
    {
        var sr=frame.GetComponent<ScrollRect>()??frame.gameObject.AddComponent<ScrollRect>();
        frame.gameObject.AddComponent<RectMask2D>();
        var content=new GameObject("MapContent",typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(frame,false);content.anchorMin=Vector2.zero;content.anchorMax=Vector2.one;
        sr.viewport=frame;sr.content=content;sr.horizontal=sr.vertical=true;sr.movementType=ScrollRect.MovementType.Clamped;sr.inertia=false;
        var pan=frame.gameObject.AddComponent<UIKeyboardPanRegion>();pan.scroller=sr;pan.mapSizing=true;pan.ResizeMap();return content;
    }
    void ResizeMap(){if(!mapSizing||scroller?.content==null)return;if(transform is RectTransform view)scroller.content.sizeDelta=view.rect.size*(contentScale-1f);}
    void OnRectTransformDimensionsChange(){ResizeMap();}
    public void Pan(Vector2 direction)
    {
        if(scroller==null)return;
        if(scroller.horizontal)scroller.horizontalNormalizedPosition=Mathf.Clamp01(scroller.horizontalNormalizedPosition+direction.x*.08f);
        if(scroller.vertical)scroller.verticalNormalizedPosition=Mathf.Clamp01(scroller.verticalNormalizedPosition+direction.y*.08f);
    }
    void OnDisable(){OwnsPan=false;}
}
