using UnityEngine;
using UnityEngine.UI;

namespace SettingsMenuUI
{
    [DisallowMultipleComponent]
    public sealed class DynamicVerticalScroll : MonoBehaviour
    {
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform content;
        [SerializeField] private Scrollbar scrollbar;
        [SerializeField, Min(0f)] private float overflowTolerance = 1f;

        private Vector2 lastViewportSize;
        private Vector2 lastContentSize;

        public bool IsOverflowing { get; private set; }

        public void Configure(ScrollRect targetScrollRect, RectTransform targetViewport, RectTransform targetContent, Scrollbar targetScrollbar)
        {
            scrollRect = targetScrollRect;
            viewport = targetViewport;
            content = targetContent;
            scrollbar = targetScrollbar;
        }

        private void OnEnable()
        {
            Canvas.ForceUpdateCanvases();
            Refresh();
        }

        private void LateUpdate()
        {
            if (viewport == null || content == null)
            {
                return;
            }

            Vector2 viewportSize = viewport.rect.size;
            Vector2 contentSize = content.rect.size;
            if (viewportSize != lastViewportSize || contentSize != lastContentSize)
            {
                Refresh();
            }
        }

        public void Refresh()
        {
            if (scrollRect == null || viewport == null || content == null)
            {
                return;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            lastViewportSize = viewport.rect.size;
            lastContentSize = content.rect.size;
            IsOverflowing = content.rect.height > viewport.rect.height + overflowTolerance;
            scrollRect.vertical = IsOverflowing;

            if (scrollbar != null && scrollbar.gameObject.activeSelf != IsOverflowing)
            {
                scrollbar.gameObject.SetActive(IsOverflowing);
            }

            if (!IsOverflowing)
            {
                scrollRect.verticalNormalizedPosition = 1f;
                content.anchoredPosition = Vector2.zero;
            }
        }
    }
}
