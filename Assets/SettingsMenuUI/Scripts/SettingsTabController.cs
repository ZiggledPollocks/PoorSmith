using System.Collections.Generic;
using UnityEngine;

namespace SettingsMenuUI
{
    [DisallowMultipleComponent]
    public sealed class SettingsTabController : MonoBehaviour
    {
        [Header("Tabs")]
        [SerializeField] private List<SettingsTabItem> tabs = new List<SettingsTabItem>();
        [SerializeField, Min(0)] private int defaultTabIndex;

        [Header("Background Colors")]
        [SerializeField] private Color normalColor = new Color32(71, 78, 96, 255);
        [SerializeField] private Color hoverColor = new Color32(91, 100, 120, 255);
        [SerializeField] private Color selectedColor = new Color32(123, 133, 156, 255);

        [Header("Text Colors")]
        [SerializeField] private Color normalTextColor = new Color32(245, 245, 243, 255);
        [SerializeField] private Color hoverTextColor = new Color32(255, 255, 255, 255);
        [SerializeField] private Color selectedTextColor = new Color32(255, 255, 255, 255);

        private int selectedIndex = -1;
        private bool initialized;

        public int SelectedIndex => selectedIndex;
        public IReadOnlyList<SettingsTabItem> Tabs => tabs;

        private void Awake()
        {
            InitializeTabs();
        }

        private void OnEnable()
        {
            if (initialized && IsValidIndex(selectedIndex))
            {
                ApplySelection(selectedIndex);
            }
        }

        public void Configure(IList<SettingsTabItem> tabItems, int initialTabIndex)
        {
            tabs = tabItems != null ? new List<SettingsTabItem>(tabItems) : new List<SettingsTabItem>();
            defaultTabIndex = Mathf.Max(0, initialTabIndex);
        }

        public void InitializeTabs()
        {
            if (tabs == null || tabs.Count == 0)
            {
                Debug.LogError($"[{nameof(SettingsTabController)}] No tabs are configured on '{name}'.", this);
                return;
            }

            for (int i = 0; i < tabs.Count; i++)
            {
                SettingsTabItem tab = tabs[i];
                if (tab == null)
                {
                    Debug.LogError($"[{nameof(SettingsTabController)}] Tab reference at index {i} is missing on '{name}'.", this);
                    continue;
                }

                if (tab.Button == null)
                {
                    Debug.LogError($"[{nameof(SettingsTabController)}] Button reference is missing on tab '{tab.name}'.", tab);
                }

                if (tab.TargetPanel == null)
                {
                    Debug.LogError($"[{nameof(SettingsTabController)}] Target Panel reference is missing on tab '{tab.name}'.", tab);
                }

                tab.Initialize(this, i);
            }

            int initialIndex = FindSingleActivePanelIndex();
            if (!IsValidIndex(initialIndex))
            {
                initialIndex = Mathf.Clamp(defaultTabIndex, 0, tabs.Count - 1);
            }

            initialized = true;
            ApplySelection(initialIndex);
        }

        public void SelectTab(int index)
        {
            if (!IsValidIndex(index))
            {
                Debug.LogError($"[{nameof(SettingsTabController)}] Tab index {index} is out of range on '{name}'.", this);
                return;
            }

            if (index == selectedIndex)
            {
                return;
            }

            ApplySelection(index);
        }

        public void SelectTab(SettingsTabItem tab)
        {
            int index = tabs.IndexOf(tab);
            if (index < 0)
            {
                Debug.LogError($"[{nameof(SettingsTabController)}] Tab '{tab?.name ?? "<null>"}' is not registered on '{name}'.", this);
                return;
            }

            if (index == selectedIndex)
            {
                return;
            }

            ApplySelection(index);
        }

        public Color GetBackgroundColor(TabVisualState state)
        {
            switch (state)
            {
                case TabVisualState.Hover:
                    return hoverColor;
                case TabVisualState.Selected:
                    return selectedColor;
                default:
                    return normalColor;
            }
        }

        public Color GetTextColor(TabVisualState state)
        {
            switch (state)
            {
                case TabVisualState.Hover:
                    return hoverTextColor;
                case TabVisualState.Selected:
                    return selectedTextColor;
                default:
                    return normalTextColor;
            }
        }

        private void ApplySelection(int index)
        {
            selectedIndex = index;
            for (int i = 0; i < tabs.Count; i++)
            {
                SettingsTabItem tab = tabs[i];
                if (tab == null)
                {
                    continue;
                }

                bool selected = i == index;
                if (tab.TargetPanel != null && tab.TargetPanel.activeSelf != selected)
                {
                    tab.TargetPanel.SetActive(selected);
                }

                tab.SetSelected(selected);
            }
        }

        private int FindSingleActivePanelIndex()
        {
            int activeIndex = -1;
            for (int i = 0; i < tabs.Count; i++)
            {
                GameObject panel = tabs[i] != null ? tabs[i].TargetPanel : null;
                if (panel == null || !panel.activeSelf)
                {
                    continue;
                }

                if (activeIndex >= 0)
                {
                    return -1;
                }

                activeIndex = i;
            }

            return activeIndex;
        }

        private bool IsValidIndex(int index)
        {
            return tabs != null && index >= 0 && index < tabs.Count;
        }
    }
}
