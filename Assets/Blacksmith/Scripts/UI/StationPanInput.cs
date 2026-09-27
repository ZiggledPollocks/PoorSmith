using UnityEngine;
using UnityEngine.EventSystems;
namespace Blacksmith
{
    public class StationPanInput : MonoBehaviour,IDragHandler
    {
        public RectTransform target;
        public void OnDrag(PointerEventData e)
        {if(target){var p=target.anchoredPosition;p.y=Mathf.Clamp(p.y+e.delta.y,-65,65);target.anchoredPosition=p;}}
    }
}
