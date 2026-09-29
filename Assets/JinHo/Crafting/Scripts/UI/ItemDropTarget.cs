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
        Graphic visual;
        Canvas hostCanvas;
        Color originalColor;
        bool preview;
        public void ConfigureProximity(Func<InventorySlotView,bool> acceptsSource, Action<InventorySlotView> onTransfer, Graphic feedback)
        {
            accepts = acceptsSource;
            Drop = onTransfer;
            visual = feedback;
            hostCanvas = GetComponentInParent<Canvas>();
            if (visual != null) originalColor = visual.color;
            if (isActiveAndEnabled && !proximityTargets.Contains(this)) proximityTargets.Add(this);
        }
        void OnEnable() { if (accepts != null && !proximityTargets.Contains(this)) proximityTargets.Add(this); }
        void OnDisable() { SetPreview(false); proximityTargets.Remove(this); }
        void OnDestroy() { proximityTargets.Remove(this); }
        public bool CanAccept(InventorySlotView source) =>
            isActiveAndEnabled && hostCanvas != null && hostCanvas.isActiveAndEnabled &&
            accepts != null && source != null && source.Stack != null && accepts(source);
        public void TryDrop(InventorySlotView source)
        {
            if (CanAccept(source)) Drop?.Invoke(source);
        }
        public void SetPreview(bool enabled)
        {
            if (preview == enabled) return;
            preview = enabled;
            if (visual != null) visual.color = enabled
                ? new Color(.95f, .72f, .24f, .22f) : originalColor;
        }
        public static ItemDropTarget FindNearest(InventorySlotView source, Vector2 pointer, Camera camera)
        {
            ItemDropTarget nearest = null;
            float best = 24f * 24f;
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
                if (distance > best) continue;
                var sample = new Vector2(
                    Mathf.Clamp(pointer.x, Mathf.Min(a.x, b.x) + 1, Mathf.Max(a.x, b.x) - 1),
                    Mathf.Clamp(pointer.y, Mathf.Min(a.y, b.y) + 1, Mathf.Max(a.y, b.y) - 1));
                if (!IsFrontmost(target, sample)) continue;
                nearest = target;
                best = distance;
            }
            return nearest;
        }
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
