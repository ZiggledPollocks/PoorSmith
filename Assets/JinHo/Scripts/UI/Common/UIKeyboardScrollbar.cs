using UnityEngine;
using UnityEngine.UI;
// UI-only lower bound: ScrollRect still owns scroll position and the natural content ratio.
public sealed class UIKeyboardScrollbar:MonoBehaviour
{
    public Scrollbar bar;
    void LateUpdate(){if(bar==null||bar.handleRect==null||transform is not RectTransform rect)return;bool horizontal=bar.direction==Scrollbar.Direction.LeftToRight||bar.direction==Scrollbar.Direction.RightToLeft;float size=horizontal?rect.rect.width:rect.rect.height;if(size>0)bar.size=Mathf.Max(bar.size,Mathf.Min(1,32f/size));}
}
