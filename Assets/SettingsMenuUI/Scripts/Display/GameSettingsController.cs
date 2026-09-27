using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SettingsMenuUI
{
    [DisallowMultipleComponent]
    public sealed class GameSettingsController : MonoBehaviour
    {
        public const float ResolutionScrollSensitivity = 20f;
        public const int ResolutionContentBottomPadding = 28;
        public const ResolutionPresetType DefaultResolutionPresetType = ResolutionPresetType.FHD;

        private static readonly ResolutionPreset[] ResolutionPresets =
        {
            new ResolutionPreset(ResolutionPresetType.QHD, 2560, 1440),
            new ResolutionPreset(ResolutionPresetType.FHD, 1920, 1080),
            new ResolutionPreset(ResolutionPresetType.HD, 1280, 720)
        };

        private static readonly Color ScrollbarTrackColor = new Color32(31, 38, 54, 255);
        private static readonly Color ScrollbarHandleColor = new Color32(181, 193, 218, 255);

        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private Button applyResolutionButton;
        [SerializeField] private Button previousScreenModeButton;
        [SerializeField] private Button nextScreenModeButton;
        [SerializeField] private TMP_Text screenModeText;
        [SerializeField] private Button applyScreenModeButton;
        [SerializeField] private Button previousAutoSaveButton;
        [SerializeField] private Button nextAutoSaveButton;
        [SerializeField] private TMP_Text autoSaveText;

        private FullScreenMode pendingScreenMode;
        private bool autoSaveEnabled = true;
        private bool listenersRegistered;
        private bool initialized;

        public ResolutionPreset SelectedResolution => ResolutionPresets[GetSelectedPresetIndex()];
        public int SelectedWidth => SelectedResolution.Width;
        public int SelectedHeight => SelectedResolution.Height;
        public FullScreenMode SelectedScreenMode => pendingScreenMode;
        public bool AutoSaveEnabled => autoSaveEnabled;
        public int ResolutionOptionCount => ResolutionPresets.Length;
        public static IReadOnlyList<ResolutionPreset> Presets => ResolutionPresets;
        public static ResolutionPreset DefaultResolution => ResolutionPresets[FindPresetIndex(DefaultResolutionPresetType)];
        public IReadOnlyList<ResolutionPreset> AvailableResolutionPresets => ResolutionPresets;

        public void Configure(
            TMP_Dropdown dropdown,
            Button resolutionApply,
            Button modePrevious,
            Button modeNext,
            TMP_Text modeValue,
            Button modeApply,
            Button autoPrevious,
            Button autoNext,
            TMP_Text autoValue)
        {
            resolutionDropdown = dropdown;
            applyResolutionButton = resolutionApply;
            previousScreenModeButton = modePrevious;
            nextScreenModeButton = modeNext;
            screenModeText = modeValue;
            applyScreenModeButton = modeApply;
            previousAutoSaveButton = autoPrevious;
            nextAutoSaveButton = autoNext;
            autoSaveText = autoValue;
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;
            ConfigureResolutionDropdownScroll();
            BuildResolutionOptions();
            pendingScreenMode = NormalizeMode(Screen.fullScreenMode);
            UpdateScreenModeText();
            UpdateAutoSaveText();
        }

        private void OnEnable()
        {
            RegisterListeners();
        }

        private void OnDisable()
        {
            UnregisterListeners();
        }

        public void ApplyResolution()
        {
            // Resolution and screen-mode changes have separate Apply buttons.
            Screen.SetResolution(SelectedWidth, SelectedHeight, NormalizeMode(Screen.fullScreenMode));
        }

        public void ApplyScreenMode()
        {
            Screen.SetResolution(Screen.width, Screen.height, pendingScreenMode);
        }

        public void CycleScreenMode()
        {
            pendingScreenMode = pendingScreenMode == FullScreenMode.Windowed
                ? FullScreenMode.FullScreenWindow
                : FullScreenMode.Windowed;
            UpdateScreenModeText();
        }

        public void ToggleAutoSave()
        {
            autoSaveEnabled = !autoSaveEnabled;
            UpdateAutoSaveText();
        }

        public void LoadSavedSettings(int width, int height, FullScreenMode mode, bool autoSave, bool applyToScreen)
        {
            pendingScreenMode = NormalizeMode(mode);
            autoSaveEnabled = autoSave;
            SelectResolution(width, height);
            UpdateScreenModeText();
            UpdateAutoSaveText();

            if (applyToScreen)
            {
                Screen.SetResolution(SelectedWidth, SelectedHeight, pendingScreenMode);
            }
        }

        private void BuildResolutionOptions()
        {
            if (resolutionDropdown == null)
            {
                return;
            }

            resolutionDropdown.ClearOptions();
            var labels = new List<string>(ResolutionPresets.Length);
            for (int i = 0; i < ResolutionPresets.Length; i++)
            {
                labels.Add(ResolutionPresets[i].DisplayName);
            }

            resolutionDropdown.AddOptions(labels);
            SelectResolution(DefaultResolutionPresetType);
            resolutionDropdown.RefreshShownValue();
        }

        private void ConfigureResolutionDropdownScroll()
        {
            if (resolutionDropdown == null || resolutionDropdown.template == null)
            {
                return;
            }

            ScrollRect scrollRect = resolutionDropdown.template.GetComponent<ScrollRect>();
            if (scrollRect == null)
            {
                return;
            }

            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = ResolutionScrollSensitivity;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            RectTransform content = scrollRect.content;
            RectTransform item = content != null ? content.Find("Item") as RectTransform : null;
            if (content != null && item != null)
            {
                content.sizeDelta = new Vector2(content.sizeDelta.x, item.rect.height);

                VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
                if (layout == null)
                {
                    layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
                }

                layout.padding = new RectOffset(0, 0, 0, ResolutionContentBottomPadding);
                layout.spacing = 0f;
                layout.childAlignment = TextAnchor.UpperCenter;
                layout.childControlWidth = true;
                layout.childControlHeight = false;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;

                ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
                if (fitter == null)
                {
                    fitter = content.gameObject.AddComponent<ContentSizeFitter>();
                }

                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            }

            Scrollbar scrollbar = scrollRect.verticalScrollbar != null
                ? scrollRect.verticalScrollbar
                : resolutionDropdown.template.GetComponentInChildren<Scrollbar>(true);
            if (scrollbar == null)
            {
                return;
            }

            scrollbar.gameObject.SetActive(true);

            if (scrollbar.TryGetComponent(out Image trackImage))
            {
                trackImage.color = ScrollbarTrackColor;
            }

            if (scrollbar.targetGraphic != null)
            {
                scrollbar.targetGraphic.color = ScrollbarHandleColor;
            }

            if (scrollbar.transform is RectTransform scrollbarRect)
            {
                scrollbarRect.sizeDelta = new Vector2(Mathf.Max(30f, scrollbarRect.sizeDelta.x), scrollbarRect.sizeDelta.y);
            }
        }

        private void SelectResolution(int width, int height)
        {
            if (resolutionDropdown == null)
            {
                return;
            }

            int selectedIndex = FindPresetIndex(width, height);
            resolutionDropdown.SetValueWithoutNotify(selectedIndex >= 0 ? selectedIndex : FindPresetIndex(DefaultResolutionPresetType));
            resolutionDropdown.RefreshShownValue();
        }

        private void SelectResolution(ResolutionPresetType type)
        {
            if (resolutionDropdown == null)
            {
                return;
            }

            resolutionDropdown.SetValueWithoutNotify(FindPresetIndex(type));
            resolutionDropdown.RefreshShownValue();
        }

        private int GetSelectedPresetIndex()
        {
            if (resolutionDropdown == null)
            {
                return FindPresetIndex(DefaultResolutionPresetType);
            }

            return Mathf.Clamp(resolutionDropdown.value, 0, ResolutionPresets.Length - 1);
        }

        private static int FindPresetIndex(int width, int height)
        {
            for (int i = 0; i < ResolutionPresets.Length; i++)
            {
                if (ResolutionPresets[i].Matches(width, height))
                {
                    return i;
                }
            }

            return -1;
        }

        private static int FindPresetIndex(ResolutionPresetType type)
        {
            for (int i = 0; i < ResolutionPresets.Length; i++)
            {
                if (ResolutionPresets[i].Type == type)
                {
                    return i;
                }
            }

            return 0;
        }

        private static FullScreenMode NormalizeMode(FullScreenMode mode)
        {
            return mode == FullScreenMode.Windowed ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow;
        }

        private void UpdateScreenModeText()
        {
            if (screenModeText != null)
            {
                screenModeText.text = pendingScreenMode == FullScreenMode.Windowed ? "창 모드" : "전체 화면";
            }
        }

        private void UpdateAutoSaveText()
        {
            if (autoSaveText != null)
            {
                autoSaveText.text = autoSaveEnabled ? "켜짐" : "꺼짐";
            }
        }

        private void RegisterListeners()
        {
            if (listenersRegistered)
            {
                return;
            }

            applyResolutionButton?.onClick.AddListener(ApplyResolution);
            previousScreenModeButton?.onClick.AddListener(CycleScreenMode);
            nextScreenModeButton?.onClick.AddListener(CycleScreenMode);
            applyScreenModeButton?.onClick.AddListener(ApplyScreenMode);
            previousAutoSaveButton?.onClick.AddListener(ToggleAutoSave);
            nextAutoSaveButton?.onClick.AddListener(ToggleAutoSave);
            listenersRegistered = true;
        }

        private void UnregisterListeners()
        {
            if (!listenersRegistered)
            {
                return;
            }

            applyResolutionButton?.onClick.RemoveListener(ApplyResolution);
            previousScreenModeButton?.onClick.RemoveListener(CycleScreenMode);
            nextScreenModeButton?.onClick.RemoveListener(CycleScreenMode);
            applyScreenModeButton?.onClick.RemoveListener(ApplyScreenMode);
            previousAutoSaveButton?.onClick.RemoveListener(ToggleAutoSave);
            nextAutoSaveButton?.onClick.RemoveListener(ToggleAutoSave);
            listenersRegistered = false;
        }
    }
}
