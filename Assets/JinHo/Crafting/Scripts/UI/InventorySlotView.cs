using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Blacksmith
{
    public class InventorySlotView : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerClickHandler,IPointerDownHandler,IBeginDragHandler,IDragHandler,IEndDragHandler,IDropHandler
    {
        public Image background,icon;public TMP_Text count;
        public Stack Stack {get;private set;} public ItemDefinition Item {get;private set;}
        public bool LongPress {get;private set;} public static InventorySlotView Dragging;
        Action click,right;Action<InventorySlotView> drop;Action<bool> hover;float down;
        Func<bool> transferSourceValid;
        Func<InventorySlotView,bool> reorderFrom;
        GameObject dragPreview;
        ItemDropTarget nearbyTarget;
        Color originalIconColor;
        public bool TransferDragEnabled => transferSourceValid != null;
        public void EnableTransferDrag(Func<bool> sourceValid, Func<InventorySlotView,bool> sameListReorder = null)
        { transferSourceValid = sourceValid; reorderFrom = sameListReorder; }
        public void Bind(Stack s,ItemDefinition item,Sprite art,Sprite frame,TMP_FontAsset font,Action left,Action rightClick,Action<InventorySlotView> onDrop,Action<bool> onHover)
        {Stack=s;Item=item;background.sprite=frame;icon.sprite=art;count.font=font;count.text=s.count.ToString();click=left;right=rightClick;drop=onDrop;hover=onHover;}
        public void OnPointerEnter(PointerEventData e){var line=GetComponent<Outline>()??gameObject.AddComponent<Outline>();line.effectColor=Color.white;line.effectDistance=new Vector2(2,-2);line.enabled=true;background.color=Color.white;hover?.Invoke(true);}
        public void OnPointerExit(PointerEventData e){var line=GetComponent<Outline>();if(line!=null)line.enabled=false;background.color=Color.white;hover?.Invoke(false);}
        public void OnPointerDown(PointerEventData e){down=Time.unscaledTime;}
        public void OnPointerClick(PointerEventData e){LongPress=Time.unscaledTime-down>=.45f;if(e.button==PointerEventData.InputButton.Right)right?.Invoke();else click?.Invoke();}
        public void OnBeginDrag(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left ||
                (TransferDragEnabled && !transferSourceValid())) return;
            Dragging = this;
            originalIconColor = icon.color;
            icon.color = new Color(originalIconColor.r, originalIconColor.g, originalIconColor.b, .45f);
            if (!TransferDragEnabled) return;
            var canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            if (canvas == null) return;
            dragPreview = new GameObject("InventoryDragPreview", typeof(RectTransform), typeof(CanvasGroup));
            dragPreview.transform.SetParent(canvas.transform, false);
            dragPreview.transform.SetAsLastSibling();
            var rect = (RectTransform)dragPreview.transform;
            rect.sizeDelta = ((RectTransform)transform).rect.size;
            var group = dragPreview.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            var art = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            art.transform.SetParent(rect, false);
            art.sprite = icon.sprite;
            art.preserveAspect = true;
            art.raycastTarget = false;
            ((RectTransform)art.transform).anchorMin = new Vector2(.1f, .15f);
            ((RectTransform)art.transform).anchorMax = new Vector2(.9f, .95f);
            ((RectTransform)art.transform).offsetMin = ((RectTransform)art.transform).offsetMax = Vector2.zero;
            var number = new GameObject("Count", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            number.transform.SetParent(rect, false);
            number.font = count.font;
            number.fontSize = count.fontSize;
            number.alignment = TextAlignmentOptions.BottomRight;
            number.text = Stack.count.ToString();
            number.color = Color.white;
            number.raycastTarget = false;
            ((RectTransform)number.transform).anchorMin = Vector2.zero;
            ((RectTransform)number.transform).anchorMax = Vector2.one;
            ((RectTransform)number.transform).offsetMin = ((RectTransform)number.transform).offsetMax = Vector2.zero;
            OnDrag(e);
        }
        public void OnDrag(PointerEventData e)
        {
            if (Dragging != this || !TransferDragEnabled) return;
            if (dragPreview != null)
            {
                var canvas = dragPreview.GetComponentInParent<Canvas>();
                var root = (RectTransform)canvas.transform;
                var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : e.pressEventCamera;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(root, e.position, camera, out var point))
                    ((RectTransform)dragPreview.transform).anchoredPosition = point + new Vector2(20, 20);
            }
            var next = ItemDropTarget.FindNearest(this, e.position, e.pressEventCamera);
            if (next == nearbyTarget) return;
            if (nearbyTarget != null) nearbyTarget.SetPreview(false);
            nearbyTarget = next;
            if (nearbyTarget != null) nearbyTarget.SetPreview(true);
        }
        public void OnEndDrag(PointerEventData e)
        {
            if (Dragging != this) return;
            var target = TransferDragEnabled
                ? ItemDropTarget.FindNearest(this, e.position, e.pressEventCamera) : null;
            ClearDrag();
            if (target != null) target.TryDrop(this);
        }
        public void OnDrop(PointerEventData e)
        {
            var source = Dragging;
            if (source == null || source == this) return;
            if (source.TransferDragEnabled)
            {
                if (reorderFrom != null && reorderFrom(source)) drop?.Invoke(source);
                return;
            }
            drop?.Invoke(source);
        }
        void OnDisable() { if (Dragging == this) ClearDrag(); }
        void OnDestroy() { if (Dragging == this) ClearDrag(); }
        void ClearDrag()
        {
            if (nearbyTarget != null) nearbyTarget.SetPreview(false);
            nearbyTarget = null;
            if (dragPreview != null) Destroy(dragPreview);
            dragPreview = null;
            if (icon != null) icon.color = originalIconColor;
            if (Dragging == this) Dragging = null;
        }
    }
}
