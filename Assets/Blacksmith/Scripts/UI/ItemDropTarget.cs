using System;
using UnityEngine;
using UnityEngine.EventSystems;
namespace Blacksmith
{
    public class ItemDropTarget : MonoBehaviour,IDropHandler,IPointerEnterHandler,IPointerExitHandler
    {
        public Action<InventorySlotView> Drop;public Action<bool> Hover;
        public void OnDrop(PointerEventData e){if(InventorySlotView.Dragging)Drop?.Invoke(InventorySlotView.Dragging);}
        public void OnPointerEnter(PointerEventData e){Hover?.Invoke(true);}
        public void OnPointerExit(PointerEventData e){Hover?.Invoke(false);}
    }
}
