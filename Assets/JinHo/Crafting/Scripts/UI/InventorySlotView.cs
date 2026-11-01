// [코드 지도] InventorySlotView: 아이템 슬롯의 표시·클릭·드래그·호버 입력을 전달한다.
// 주요 함수: OnBeginDrag, OnDrag, OnDrop
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Crafting/Scripts/UI/InventorySlotView.cs.md

using System;
using System.Collections;
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
        static InventorySlotView selected;
        internal bool IsSelected => selected == this;
        Action click,right;Action<InventorySlotView> drop;Action<bool> hover;float down;
        Func<bool> transferSourceValid;
        Func<InventorySlotView,bool> reorderFrom;
        GameObject dragPreview;
        GameObject returningPreview;
        Image previewFrame;
        Image selectionFrame;
        ItemDropTarget nearbyTarget;
        Color originalIconColor;
        Func<int> dragPreviewQuantity;
        TMP_FontAsset previewFont;
        float previewFontSize = 18f;
        public bool TransferDragEnabled => transferSourceValid != null;
        public string TutorialAction { get; set; } = "inventory";
        public Action<bool> KeyboardPreview;
        public int KeyboardMoveQuantity {get;set;}
        public bool KeyboardQuantityEditable {get;set;}=true;
        public int KeyboardQuantityLimit {get;set;}=int.MaxValue;
        public int KeyboardDefaultQuantity => Mathf.Clamp(dragPreviewQuantity?.Invoke()??(Item?.equipmentSlot=="Arrow"?Stack?.count??1:1),1,Mathf.Max(1,KeyboardQuantityLimit));
        public int TransferQuantity => KeyboardMoveQuantity>0?Mathf.Min(KeyboardMoveQuantity,Stack?.count??0):Stack?.count??0;
        Color keyboardIconColor;bool keyboardMoving;
        public void PreviewKeyboard(bool active){SetSelected(active);if(KeyboardPreview!=null)KeyboardPreview(active);else hover?.Invoke(active);if(active)GetComponent<OwnedItemActions>()?.Preview();}
        public void ShowKeyboardMoving(bool active){if(icon==null)return;if(active&&!keyboardMoving){keyboardIconColor=icon.color;icon.color=new Color(icon.color.r,icon.color.g,icon.color.b,.45f);}else if(!active&&keyboardMoving)icon.color=keyboardIconColor;keyboardMoving=active;}
        public void KeyboardClick(){down=Time.unscaledTime;OnPointerClick(new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left});}
        public bool CanKeyboardDrop(InventorySlotView source)=>source!=null&&source!=this&&source.HasDragAction&&HasItem&&drop!=null&&(!source.TransferDragEnabled||reorderFrom!=null&&reorderFrom(source));
        public bool TryKeyboardDrop(InventorySlotView source){if(!CanKeyboardDrop(source))return false;if(!StoryTutorial.CanUseSlot(this)||!StoryTutorial.CanUseSlot(source)){StoryTutorial.Reject();return false;}drop(source);return true;}
        public string TutorialRightAction { get; set; } = "inventory";
        public bool HasItem => isActiveAndEnabled && Stack != null && Stack.count > 0 && Item != null;
        public bool HasClickAction(bool secondary) => HasItem && (secondary ? right != null : click != null);
        public bool HasDragAction => HasItem && (TransferDragEnabled ? transferSourceValid() : drop != null);
        public void EnableTransferDrag(Func<bool> sourceValid, Func<InventorySlotView,bool> sameListReorder = null)
        { transferSourceValid = sourceValid; reorderFrom = sameListReorder; }
        public void SetDragPreviewQuantity(Func<int> quantity) => dragPreviewQuantity = quantity;
        public void Bind(Stack s,ItemDefinition item,Sprite art,Sprite frame,TMP_FontAsset font,Action left,Action rightClick,Action<InventorySlotView> onDrop,Action<bool> onHover)
        {Stack=s;Item=item;background.sprite=frame;GameArtSkin.Slot(background);icon.sprite=art;previewFont=font;if(count!=null){count.font=font;if(GameArtSkin.Current!=null)count.color=GameArtSkin.Current.text;count.text=s.count.ToString();previewFontSize=count.fontSize;}UIKeyboardNavigation.MarkDirty(transform);click=left;right=rightClick;drop=onDrop;hover=onHover;}
        public void OnPointerEnter(PointerEventData e){if(Stack?.count>0&&!e.dragging&&e.pointerDrag==null)GameSound.Hover(this);var line=GetComponent<Outline>()??gameObject.AddComponent<Outline>();line.effectColor=Color.white;line.effectDistance=new Vector2(2,-2);line.enabled=true;hover?.Invoke(true);GetComponent<OwnedItemActions>()?.Preview();}
        public void OnPointerExit(PointerEventData e){var line=GetComponent<Outline>();if(line!=null)line.enabled=false;hover?.Invoke(false);}
        public void OnPointerDown(PointerEventData e){down=Time.unscaledTime;}
        public void OnPointerClick(PointerEventData e)
        {
            bool secondary = e.button == PointerEventData.InputButton.Right;
            if (e.button == PointerEventData.InputButton.Middle || !HasClickAction(secondary)) return;
            if (!StoryTutorial.CanUseSlot(this, secondary)) { StoryTutorial.Reject(); return; }
            GameSound.UI("Select",this);
            LongPress = Time.unscaledTime - down >= .45f;
            if (e.button == PointerEventData.InputButton.Right) right?.Invoke();
            else
            {
                if (selected != this)
                {
                    if (selected != null) selected.SetSelected(false);
                    selected = this;
                }
                SetSelected(true);
                click?.Invoke();
            }
        }
        // A separate non-raycasting border preserves each screen's existing slot colors.
        void SetSelected(bool active)
        {
            if (active && selectionFrame == null)
            {
                selectionFrame = new GameObject("SelectionBorder", typeof(RectTransform),
                    typeof(Image), typeof(Outline), typeof(LayoutElement)).GetComponent<Image>();
                selectionFrame.transform.SetParent(transform, false);
                var rect = (RectTransform)selectionFrame.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                selectionFrame.color = new Color(1f, 1f, 1f, .20f);
                selectionFrame.raycastTarget = false;
                var line = selectionFrame.GetComponent<Outline>();
                line.enabled = false;
                selectionFrame.GetComponent<LayoutElement>().ignoreLayout = true;
            }
            if (selectionFrame != null) selectionFrame.gameObject.SetActive(active);
        }
        // 핵심 분기: e.button != PointerEventData.InputButton.Left || (TransferDragEnabled && !transferSourceValid()) 판정.
        // 상태 변경: Dragging 갱신.
        // 다음 연결: Blacksmith.InventorySlotView.OnDrag(UnityEngine.EventSystems.PointerEventData) 호출.
        public void OnBeginDrag(PointerEventData e)
        {
            if (!GameUIController.AllowsUI(transform))return;
            if (e.button != PointerEventData.InputButton.Left ||
                (TransferDragEnabled && !transferSourceValid())) return;
            if (!HasDragAction) return;
            if (!StoryTutorial.CanUseSlot(this)) { StoryTutorial.Reject(); return; }
            Dragging = this;
            originalIconColor = icon.color;
            icon.color = new Color(originalIconColor.r, originalIconColor.g, originalIconColor.b, .45f);
            if (!TransferDragEnabled) return;
            var canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            if (canvas == null) return;
            dragPreview = new GameObject("InventoryDragPreview", typeof(RectTransform), typeof(CanvasGroup));
            dragPreview.transform.SetParent(canvas.transform, false);
            dragPreview.transform.SetAsLastSibling();
            var previewCanvas=dragPreview.AddComponent<Canvas>();previewCanvas.overrideSorting=true;previewCanvas.sortingOrder=GameUIController.PresentationOverlayOrder+2;
            var rect = (RectTransform)dragPreview.transform;
            rect.sizeDelta = ((RectTransform)transform).rect.size;
            var group = dragPreview.GetComponent<CanvasGroup>();
            group.alpha = .94f;
            group.blocksRaycasts = false;
            group.interactable = false;
            previewFrame = new GameObject("Frame", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            previewFrame.transform.SetParent(rect, false);
            previewFrame.sprite = background.sprite;
            previewFrame.color = new Color(.24f, .22f, .19f, .95f);
            previewFrame.raycastTarget = false;
            ((RectTransform)previewFrame.transform).anchorMin = Vector2.zero;
            ((RectTransform)previewFrame.transform).anchorMax = Vector2.one;
            ((RectTransform)previewFrame.transform).offsetMin =
                ((RectTransform)previewFrame.transform).offsetMax = Vector2.zero;
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
            number.font = previewFont;
            RuntimeUIFactory.FitText(number, previewFontSize);
            number.alignment = TextAlignmentOptions.BottomRight;
            number.text = Mathf.Max(1, dragPreviewQuantity?.Invoke() ?? Stack.count).ToString();
            number.color = Color.white;
            number.raycastTarget = false;
            ((RectTransform)number.transform).anchorMin = Vector2.zero;
            ((RectTransform)number.transform).anchorMax = Vector2.one;
            ((RectTransform)number.transform).offsetMin = ((RectTransform)number.transform).offsetMax = Vector2.zero;
            OnDrag(e);
        }
        public void OnDrag(PointerEventData e)
        {
            if(!GameUIController.AllowsUI(transform)){CancelDrag();return;}
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
            if (previewFrame != null)
                previewFrame.color = next == null
                    ? new Color(.24f, .22f, .19f, .95f)
                    : new Color(.55f, .40f, .15f, .95f);
            ItemDropTarget.UpdateDragVisuals(this, next);
            if (next == nearbyTarget) return;
            nearbyTarget = next;
        }
        public void CancelDrag() { if (Dragging == this) ClearDrag(); }
        public void OnEndDrag(PointerEventData e)
        {
            if (Dragging != this) return;
            if(!GameUIController.AllowsUI(transform)){CancelDrag();return;}
            var target = TransferDragEnabled
                ? ItemDropTarget.FindNearest(this, e.position, e.pressEventCamera) : null;
            if (target == null && dragPreview != null)
            {
                var returning = dragPreview;
                dragPreview = null;
                StartCoroutine(ReturnPreview(returning));
            }
            ClearDrag();
            if (target != null) target.TryDrop(this);
        }
        public void OnDrop(PointerEventData e)
        {
            var source = Dragging;
            if(!GameUIController.AllowsUI(transform)||source!=null&&!GameUIController.AllowsUI(source.transform))return;
            if (source == null || source == this || !HasItem) return;
            if (!StoryTutorial.CanUseSlot(this) || !StoryTutorial.CanUseSlot(source)) { StoryTutorial.Reject(); return; }
            if (source.TransferDragEnabled)
            {
                if (reorderFrom != null && reorderFrom(source)) drop?.Invoke(source);
                return;
            }
            drop?.Invoke(source);
        }
        void OnDisable()
        {
            ShowKeyboardMoving(false);
            if (Dragging == this) ClearDrag();
            if (selected == this) selected = null;
            if (returningPreview != null) Destroy(returningPreview);
        }
        void OnDestroy()
        {
            if (Dragging == this) ClearDrag();
            if (selected == this) selected = null;
            if (returningPreview != null) Destroy(returningPreview);
        }
        IEnumerator ReturnPreview(GameObject preview)
        {
            returningPreview = preview;
            Vector3 start = preview.transform.position;
            float elapsed = 0f;
            while (elapsed < .14f && preview != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / .14f);
                preview.transform.position = Vector3.Lerp(start, transform.position,
                    1f - (1f - t) * (1f - t));
                yield return null;
            }
            if (preview != null) Destroy(preview);
            returningPreview = null;
        }
        void ClearDrag()
        {
            ItemDropTarget.ClearDragVisuals();
            nearbyTarget = null;
            if (dragPreview != null) Destroy(dragPreview);
            dragPreview = null;
            previewFrame = null;
            if (icon != null) icon.color = originalIconColor;
            if (Dragging == this) Dragging = null;
        }
    }

}
