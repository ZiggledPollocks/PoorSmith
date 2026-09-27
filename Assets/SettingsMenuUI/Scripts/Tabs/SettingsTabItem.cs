using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SettingsMenuUI
{
    public enum TabVisualState
    {
        Normal,
        Hover,
        Selected
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button), typeof(Image))]
    public sealed class SettingsTabItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Outline selectedOutline;
        [SerializeField] private TMP_Text label;
        [SerializeField] private GameObject targetPanel;
        [SerializeField] private SettingsTabController controller;
        [SerializeField, Min(0)] private int tabIndex;
        [SerializeField, Range(1f, 1.2f)] private float hoverScale = 1.14f;
        [SerializeField, Range(0.05f, 0.4f)] private float hoverDuration = 0.12f;

        private bool isSelected;
        private bool listenerRegistered;
        private bool isPointerInside;
        private Coroutine scaleRoutine;
        private Vector3 normalScale = Vector3.one;
        private TabVisualState visualState = TabVisualState.Normal;

        public Button Button => button;
        public GameObject TargetPanel => targetPanel;
        public bool IsSelected => isSelected;
        public TabVisualState VisualState => visualState;

        private void Awake()
        {
            normalScale = transform.localScale;
            CacheLocalReferences();
            ConfigureRaycastsAndTransition();
        }

        private void OnEnable()
        {
            RegisterClickListener();
        }

        private void OnDisable()
        {
            UnregisterClickListener();
            if (scaleRoutine != null)
            {
                StopCoroutine(scaleRoutine);
                scaleRoutine = null;
            }

            transform.localScale = normalScale;
            isPointerInside = false;
        }

        public void Initialize(SettingsTabController owner, int index)
        {
            controller = owner;
            tabIndex = index;
            normalScale = Vector3.one;
            CacheLocalReferences();
            ConfigureRaycastsAndTransition();
            RegisterClickListener();
        }

        public void Configure(
            SettingsTabController owner,
            int index,
            Button tabButton,
            Image tabBackground,
            Outline tabOutline,
            TMP_Text tabLabel,
            GameObject panel)
        {
            controller = owner;
            tabIndex = index;
            button = tabButton;
            background = tabBackground;
            selectedOutline = tabOutline;
            label = tabLabel;
            targetPanel = panel;
            normalScale = Vector3.one;
            ConfigureRaycastsAndTransition();
        }

        public void ConfigureHover(float targetScale, float duration)
        {
            hoverScale = Mathf.Clamp(targetScale, 1f, 1.2f);
            hoverDuration = Mathf.Clamp(duration, 0.05f, 0.4f);
        }

        public void SetSelected(bool selected)
        {
            if (scaleRoutine != null)
            {
                StopCoroutine(scaleRoutine);
                scaleRoutine = null;
            }

            isSelected = selected;
            isPointerInside = false;
            transform.localScale = normalScale;
            if (button != null)
            {
                button.interactable = !selected;
            }

            SetState(selected ? TabVisualState.Selected : TabVisualState.Normal);
        }

        public void SetState(TabVisualState state)
        {
            if (isSelected && state != TabVisualState.Selected)
            {
                state = TabVisualState.Selected;
            }

            visualState = state;
            if (background != null && controller != null)
            {
                background.color = controller.GetBackgroundColor(state);
            }

            if (label != null && controller != null)
            {
                label.color = controller.GetTextColor(state);
            }

            if (selectedOutline != null)
            {
                selectedOutline.enabled = state == TabVisualState.Selected;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (isSelected || button == null || !button.interactable)
            {
                isPointerInside = false;
                transform.localScale = normalScale;
                SetState(TabVisualState.Selected);
                return;
            }

            isPointerInside = true;
            SetState(TabVisualState.Hover);
            AnimateScale(normalScale * hoverScale);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isPointerInside = false;
            if (isSelected)
            {
                if (scaleRoutine != null)
                {
                    StopCoroutine(scaleRoutine);
                    scaleRoutine = null;
                }

                transform.localScale = normalScale;
                SetState(TabVisualState.Selected);
                return;
            }

            SetState(TabVisualState.Normal);
            AnimateScale(normalScale);
        }

        private void HandleClick()
        {
            if (isSelected || button == null || !button.interactable)
            {
                return;
            }

            if (controller == null)
            {
                Debug.LogError($"[{nameof(SettingsTabItem)}] Controller reference is missing on '{name}'.", this);
                return;
            }

            controller.SelectTab(tabIndex);
        }

        private void AnimateScale(Vector3 targetScale)
        {
            if (!isActiveAndEnabled)
            {
                transform.localScale = targetScale;
                return;
            }

            if (scaleRoutine != null)
            {
                StopCoroutine(scaleRoutine);
            }

            scaleRoutine = StartCoroutine(ScaleTo(targetScale));
        }

        private IEnumerator ScaleTo(Vector3 targetScale)
        {
            Vector3 startScale = transform.localScale;
            float elapsed = 0f;
            while (elapsed < hoverDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / hoverDuration));
                transform.localScale = Vector3.LerpUnclamped(startScale, targetScale, t);
                yield return null;
            }

            transform.localScale = targetScale;
            scaleRoutine = null;
        }

        private void CacheLocalReferences()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (background == null)
            {
                background = GetComponent<Image>();
            }

            if (selectedOutline == null)
            {
                selectedOutline = GetComponent<Outline>();
            }

            if (label == null)
            {
                label = GetComponentInChildren<TMP_Text>(true);
            }
        }

        private void ConfigureRaycastsAndTransition()
        {
            if (button != null)
            {
                button.transition = Selectable.Transition.None;
            }

            if (background != null)
            {
                background.raycastTarget = true;
            }

            if (label != null)
            {
                label.raycastTarget = false;
            }
        }

        private void RegisterClickListener()
        {
            if (!isActiveAndEnabled || button == null)
            {
                return;
            }

            button.onClick.RemoveListener(HandleClick);
            button.onClick.AddListener(HandleClick);
            listenerRegistered = true;
        }

        private void UnregisterClickListener()
        {
            if (!listenerRegistered || button == null)
            {
                return;
            }

            button.onClick.RemoveListener(HandleClick);
            listenerRegistered = false;
        }
    }
}
