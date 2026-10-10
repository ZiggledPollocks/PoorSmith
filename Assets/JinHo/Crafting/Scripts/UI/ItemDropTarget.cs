// [코드 지도] ItemDropTarget: 드롭 가능 영역과 근접 강조·최상단 판정을 처리한다.
// 주요 함수: FindNearest, IsFrontmost, ConfigureProximity
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Crafting/Scripts/UI/ItemDropTarget.cs.md

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Blacksmith
{
    public class ItemDropTarget : MonoBehaviour,IDropHandler,IPointerEnterHandler,IPointerExitHandler
    {
        public Action<InventorySlotView> Drop;public Action<bool> Hover;
        static readonly List<ItemDropTarget> proximityTargets = new();
        static readonly Vector3[] corners = new Vector3[4];
        static readonly List<RaycastResult> raycastResults = new();
        static EventSystem raycastSystem;
        static PointerEventData raycastPointer;
        Func<InventorySlotView,bool> accepts;
        Func<InventorySlotView,bool> previewAccepts;
        Graphic visual;
        Canvas hostCanvas;
        Image highlight;
        int previewState;
        bool requireInside;
        public void ConfigureProximity(Func<InventorySlotView,bool> acceptsSource, Action<InventorySlotView> onTransfer, Graphic feedback, bool exactArea = false, Func<InventorySlotView,bool> canHighlight = null)
        {
            accepts = acceptsSource;
            previewAccepts = canHighlight;
            Drop = onTransfer;
            visual = feedback;
            requireInside = exactArea;
            hostCanvas = GetComponentInParent<Canvas>();
            if (isActiveAndEnabled && !proximityTargets.Contains(this)) proximityTargets.Add(this);
        }
        void OnEnable() { if (accepts != null && !proximityTargets.Contains(this)) proximityTargets.Add(this); }
        void OnDisable() { SetPreview(false); proximityTargets.Remove(this); }
        void OnDestroy() { proximityTargets.Remove(this); }
        public bool CanAccept(InventorySlotView source) =>
            isActiveAndEnabled && hostCanvas != null && hostCanvas.isActiveAndEnabled &&
            accepts != null && source != null && source.Stack != null && accepts(source);
        public bool CanPreview(InventorySlotView source) =>
            CanAccept(source) && (previewAccepts == null || previewAccepts(source));
        public void TryDrop(InventorySlotView source)
        {
            if (CanAccept(source)) Drop?.Invoke(source);
        }
        public void SetPreview(bool enabled)
        {
            SetVisual(enabled ? 2 : 0);
        }
        void SetVisual(int state)
        {
            if (previewState == state) return;
            previewState = state;
            if (state != 0 && highlight == null)
            {
                var parent = visual != null ? visual.transform : transform;
                highlight = new GameObject("DropHighlight", typeof(RectTransform),
                    typeof(Image), typeof(Outline), typeof(LayoutElement)).GetComponent<Image>();
                highlight.transform.SetParent(parent, false);
                highlight.transform.SetAsLastSibling();
                var rect = (RectTransform)highlight.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                highlight.raycastTarget = false;
                highlight.GetComponent<Outline>().enabled = false;
                highlight.GetComponent<LayoutElement>().ignoreLayout = true;
            }
            if (highlight != null)
            {
                // Whole-grid targets use a light surface; individual valid slots
                // stand out against dimmed alternatives without a yellow outline.
                highlight.color = state == 1 ? new Color(0f, 0f, 0f, .42f) :
                    new Color(1f, 1f, 1f, requireInside ? .26f : .19f);
                highlight.gameObject.SetActive(state != 0);
            }
        }
        public static void UpdateDragVisuals(InventorySlotView source, ItemDropTarget selected)
        {
            foreach (var target in proximityTargets)
            {
                if (target == null) continue;
                bool candidate = target.CanPreview(source);
                bool ancestorOfSelected = selected != null && selected != target &&
                    selected.transform.IsChildOf(target.transform);
                bool childOfSelectedGrid = selected != null && !selected.requireInside &&
                    selected != target && target.transform.IsChildOf(selected.transform);
                target.SetVisual(!candidate || ancestorOfSelected || childOfSelectedGrid
                    ? 0 : target == selected ? 2 : 1);
            }
        }
        public static void ClearDragVisuals()
        {
            foreach (var target in proximityTargets)
                if (target != null) target.SetVisual(0);
        }
        // 핵심 분기: target == null || !target.CanAccept(source) 판정.
        // 상태 변경: nearest 갱신.
        // 다음 연결: Blacksmith.ItemDropTarget.CanAccept(Blacksmith.InventorySlotView) 호출.
        public static ItemDropTarget FindNearest(InventorySlotView source, Vector2 pointer, Camera camera)
        {
            ItemDropTarget nearest = null;
            float best = 24f * 24f;
            float bestArea = float.PositiveInfinity;
            foreach (var target in proximityTargets)
            {
                if (target == null || !target.CanAccept(source)) continue;
                var rect = target.transform as RectTransform;
                if (rect == null) continue;
                rect.GetWorldCorners(corners);
                var a = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
                var b = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
                float dx = Mathf.Max(Mathf.Max(Mathf.Min(a.x, b.x) - pointer.x, 0f), pointer.x - Mathf.Max(a.x, b.x));
                float dy = Mathf.Max(Mathf.Max(Mathf.Min(a.y, b.y) - pointer.y, 0f), pointer.y - Mathf.Max(a.y, b.y));
                float distance = dx * dx + dy * dy;
                if (target.requireInside && distance > 0f) continue;
                if (distance > best) continue;
                float area = Mathf.Abs((a.x - b.x) * (a.y - b.y));
                if (Mathf.Approximately(distance, best) && area >= bestArea) continue;
                var sample = new Vector2(
                    Mathf.Clamp(pointer.x, Mathf.Min(a.x, b.x) + 1, Mathf.Max(a.x, b.x) - 1),
                    Mathf.Clamp(pointer.y, Mathf.Min(a.y, b.y) + 1, Mathf.Max(a.y, b.y) - 1));
                if (!IsFrontmost(target, sample)) continue;
                nearest = target;
                best = distance;
                bestArea = area;
            }
            return nearest;
        }
        // 핵심 분기: system == null 판정.
        // 상태 변경: raycastSystem 갱신.
        static bool IsFrontmost(ItemDropTarget target, Vector2 sample)
        {
            var system = EventSystem.current;
            if (system == null) return false;
            if (raycastSystem != system || raycastPointer == null)
            {
                raycastSystem = system;
                raycastPointer = new PointerEventData(system);
            }
            raycastPointer.Reset();
            raycastPointer.position = sample;
            raycastResults.Clear();
            system.RaycastAll(raycastPointer, raycastResults);
            // A just-built canvas can have no registered graphics until its first update.
            // No hit provides no evidence that another panel covers this target.
            if (raycastResults.Count == 0) return true;
            foreach (var hit in raycastResults)
                if (hit.gameObject != null)
                    return hit.gameObject.transform.IsChildOf(target.transform);
            return false;
        }
        public void OnDrop(PointerEventData e)
        {
            var source = InventorySlotView.Dragging;
            if (source != null && !source.TransferDragEnabled) Drop?.Invoke(source);
        }
        public void OnPointerEnter(PointerEventData e){Hover?.Invoke(true);}
        public void OnPointerExit(PointerEventData e){Hover?.Invoke(false);}
    }
}
