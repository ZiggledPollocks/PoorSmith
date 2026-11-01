using UnityEngine;
using UnityEngine.EventSystems;

namespace Blacksmith
{
    /// <summary>Presentation children only: never moves the gesture/click rectangle or changes availability.</summary>
    [DisallowMultipleComponent]
    public sealed class CraftingDepthFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public bool processing, selected, available = true;
        bool pointed, focused;
        CraftingDepthRim rim;
        UnityEngine.UI.Image source, raised;
        float priorAlpha = 1f;
        bool priorTransparentCull;
        public void Configure(bool active, bool chosen, bool usable)
        {
            processing = active; selected = chosen; available = usable;
            if (rim == null)
            {
                var node = new GameObject("Crafting visual depth", typeof(RectTransform), typeof(CanvasRenderer));
                var rect = (RectTransform)node.transform; rect.SetParent(transform, false); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
                rim = node.AddComponent<CraftingDepthRim>(); rim.raycastTarget = false;
                // Existing simple hover outline yields to this one coherent priority treatment.
                var hover = GetComponent<UiHoverOutline>(); if (hover != null) hover.enabled = false;
                var outline = GetComponent<UnityEngine.UI.Outline>(); if (outline != null) outline.enabled = false;
                source = name == "Workpiece" ? GetComponent<UnityEngine.UI.Image>() : null;
                if (source != null && source.sprite != null)
                {
                    priorAlpha = source.canvasRenderer.GetAlpha();
                    priorTransparentCull = source.canvasRenderer.cullTransparentMesh;
                    source.canvasRenderer.cullTransparentMesh = false;
                    var art = new GameObject("Raised workpiece art", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
                    var ar = (RectTransform)art.transform; ar.SetParent(transform, false); ar.anchorMin = Vector2.zero; ar.anchorMax = Vector2.one; ar.offsetMin = ar.offsetMax = Vector2.zero;
                    raised = art.GetComponent<UnityEngine.UI.Image>(); raised.raycastTarget = false;
                }
            }
            UpdatePresentation();
        }
        void LateUpdate() => UpdatePresentation();
        void UpdatePresentation()
        {
            if (rim == null) return;
            var button = GetComponent<UnityEngine.UI.Selectable>();
            bool usable = button == null ? available : button.IsInteractable();
            focused = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
            var slot = GetComponent<InventorySlotView>();
            bool chosen = selected || (slot != null && slot.IsSelected);
            int state = processing ? 4 : chosen ? 3 : (pointed || focused) && usable ? 2 : usable ? 1 : 0;
            rim.Level = state;
            if (raised != null && source != null)
            {
                raised.sprite = source.sprite; raised.color = source.color; raised.type = source.type;
                raised.preserveAspect = source.preserveAspect; raised.material = source.material;
                raised.rectTransform.anchoredPosition = Vector2.up * (state >= 3 ? 3f : state == 2 ? 1f : 0f);
                // Raycasts/gestures still see source's original rectangle and alpha-test data.
                source.canvasRenderer.SetAlpha(0f);
            }
        }
        public void OnPointerEnter(PointerEventData e)
        {
            pointed = true;
            // InventorySlotView still owns interaction/selection; suppress its duplicate hover border only.
            var old = GetComponent<UnityEngine.UI.Outline>(); if (old != null) old.enabled = false;
            UpdatePresentation();
        }
        public void OnPointerExit(PointerEventData e) { pointed = false; UpdatePresentation(); }
        public void OnSelect(BaseEventData e) { focused = true; UpdatePresentation(); }
        public void OnDeselect(BaseEventData e) { focused = false; UpdatePresentation(); }
        void OnDisable() { pointed = focused = false; if (source != null) { source.canvasRenderer.SetAlpha(priorAlpha); source.canvasRenderer.cullTransparentMesh = priorTransparentCull; } }
    }

}
