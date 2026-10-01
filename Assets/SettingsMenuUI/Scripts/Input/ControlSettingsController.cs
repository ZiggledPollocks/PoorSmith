// [코드 지도] ControlSettingsController: 입력 키 변경과 표시·저장을 처리한다.
// 주요 함수: BeginRebind, EnsureRowsForCurrentActions, FindBindingIndices
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/SettingsMenuUI/Scripts/Input/ControlSettingsController.cs.md

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SettingsMenuUI
{
    [DisallowMultipleComponent]
    public sealed class ControlSettingsController : MonoBehaviour
    {
        [Serializable]
        public sealed class BindingRow
        {
            public string actionName;
            public string partName;
            public bool includeMouseBindings;
            public Button mainButton;
            public TMP_Text mainText;
            public Button subButton;
            public TMP_Text subText;
        }

        private readonly struct BindingDefinition
        {
            public BindingDefinition(string action, string part, string label, bool includeMouse = false)
            {
                Action = action;
                Part = part;
                Label = label;
                IncludeMouse = includeMouse;
            }

            public string Action { get; }
            public string Part { get; }
            public string Label { get; }
            public bool IncludeMouse { get; }
        }

        private const string BindingOverridesKey = "SettingsMenu.InputBindingOverrides";
        private const string InvalidKeyMessage = "이미 입력된 키";

        private static readonly BindingDefinition[] CurrentPlayerBindings =
        {
            new BindingDefinition("Move", "up", "이동 - 위"),
            new BindingDefinition("Move", "down", "이동 - 아래"),
            new BindingDefinition("Move", "left", "이동 - 왼쪽"),
            new BindingDefinition("Move", "right", "이동 - 오른쪽"),
            new BindingDefinition("Attack", string.Empty, "공격", true),
            new BindingDefinition("Interact", string.Empty, "상호작용"),
            new BindingDefinition("Jump", string.Empty, "점프"),
            new BindingDefinition("Sprint", string.Empty, "달리기"),
            new BindingDefinition("Roll", string.Empty, "회피"),
            new BindingDefinition("Inventory", string.Empty, "인벤토리"),
            new BindingDefinition("ToolSlot1", string.Empty, "도구 슬롯 1"),
            new BindingDefinition("ToolSlot2", string.Empty, "도구 슬롯 2"),
            new BindingDefinition("ToolSlot3", string.Empty, "도구 슬롯 3")
        };

        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string actionName = "Move";
        [SerializeField] private List<BindingRow> rows = new List<BindingRow>();
        [SerializeField] private SettingsPopupController warningPopup;

        private readonly List<Action> removeListenerActions = new List<Action>();
        private InputActionMap playerActionMap;
        private InputAction rebindingAction;
        private InputActionRebindingExtensions.RebindingOperation rebindingOperation;
        private bool restoreActionEnabled;
        private bool suppressCancelWarning;
        private bool invalidCandidateDetected;
        private TMP_Text rebindingText;
        private Image rebindingBackground;
        private Color rebindingOriginalColor;
        private int rebindingIndex = -1;
        private bool previousHadOverride;
        private string previousOverridePath;
        private string previousOverrideProcessors;
        private string previousOverrideInteractions;
        private double lastRejectedInputTime = double.NegativeInfinity;

        public bool IsRebinding => rebindingOperation != null;
        public InputAction MoveAction => playerActionMap?.FindAction(actionName, false);
        public bool ShouldBlockSettingsToggle =>
            IsRebinding || Time.realtimeSinceStartupAsDouble - lastRejectedInputTime < 0.15d;

        public void Configure(InputActionAsset asset, IList<BindingRow> bindingRows)
        {
            inputActions = asset;
            rows = bindingRows != null ? new List<BindingRow>(bindingRows) : new List<BindingRow>();
        }

        public void SetWarningPopup(SettingsPopupController popup)
        {
            warningPopup = popup;
        }

        public void SetInputActions(InputActionAsset actions)
        {
            CancelCurrentRebind();
            inputActions = actions;
            ResolveActionMap();
            EnsureRowsForCurrentActions();
            LoadBindingOverrides();
            RefreshAllLabels();
        }

        private void Awake()
        {
            ResolveActionMap();
            EnsureRowsForCurrentActions();
            EnsureScrollSupport();
            LoadBindingOverrides();
            RefreshAllLabels();
        }

        private void OnEnable()
        {
            RegisterButtonListeners();
            RefreshAllLabels();
        }

        private void OnDisable()
        {
            CancelCurrentRebind();
            UnregisterButtonListeners();
        }

        public void SaveBindingOverrides()
        {
            if (inputActions == null)
            {
                return;
            }

            PlayerPrefs.SetString(BindingOverridesKey, inputActions.SaveBindingOverridesAsJson());
        }

        public void LoadBindingOverrides()
        {
            if (inputActions == null || !PlayerPrefs.HasKey(BindingOverridesKey))
            {
                return;
            }

            string json = PlayerPrefs.GetString(BindingOverridesKey, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                try { inputActions.LoadBindingOverridesFromJson(json); }
                catch (Exception exception)
                {
                    Debug.LogWarning($"Saved key bindings could not be loaded: {exception.Message}", this);
                }
            }
        }

        // 핵심 분기: playerActionMap == null 판정.
        // 상태 변경: row.mainButton.interactable 갱신.
        // 다음 연결: SettingsMenuUI.ControlSettingsController.ResolveActionMap() 호출.
        public void RefreshAllLabels()
        {
            if (playerActionMap == null)
            {
                ResolveActionMap();
            }

            for (int i = 0; i < rows.Count; i++)
            {
                BindingRow row = rows[i];
                InputAction action = FindRowAction(row);
                List<int> indices = FindBindingIndices(row, action);
                int mainIndex = indices.Count > 0 ? indices[0] : -1;
                int subIndex = indices.Count > 1 ? indices[1] : -1;
                SetBindingLabel(row.mainText, action, mainIndex);
                SetBindingLabel(row.subText, action, subIndex);
                if (row.mainButton != null)
                {
                    row.mainButton.interactable = IsRebindableBinding(action, mainIndex);
                }

                if (row.subButton != null)
                {
                    row.subButton.interactable = IsRebindableBinding(action, subIndex);
                }
            }
        }

        // Kept for existing serialized button events and older callers.
        public void BeginRebind(string partName, bool secondary)
        {
            BindingRow row = rows.Find(item => string.Equals(item.partName, partName, StringComparison.OrdinalIgnoreCase));
            BeginRebind(row, secondary);
        }

        public void CancelCurrentRebind()
        {
            if (rebindingOperation == null)
            {
                return;
            }

            suppressCancelWarning = true;
            rebindingOperation.Cancel();
            if (rebindingOperation != null)
            {
                DisposeOperation();
                RefreshAllLabels();
            }

            suppressCancelWarning = false;
        }

        private void ResolveActionMap()
        {
            playerActionMap = inputActions?.FindActionMap(actionMapName, false);
            if (inputActions != null && playerActionMap == null)
            {
                Debug.LogError($"[{nameof(ControlSettingsController)}] Action map '{actionMapName}' was not found.", this);
            }
        }

        // 핵심 분기: rows == null 판정.
        // 상태 변경: rows 갱신.
        // 다음 연결: SettingsMenuUI.ControlSettingsController.RowMatches(SettingsMenuUI.ControlSettingsController.BindingRow, stri… 호출.
        private void EnsureRowsForCurrentActions()
        {
            if (rows == null)
            {
                rows = new List<BindingRow>();
            }

            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] != null && string.IsNullOrEmpty(rows[i].actionName))
                {
                    rows[i].actionName = actionName;
                }
            }

            BindingRow template = rows.Find(row => row?.mainButton != null);
            if (template == null)
            {
                return;
            }

            for (int i = 0; i < CurrentPlayerBindings.Length; i++)
            {
                BindingDefinition definition = CurrentPlayerBindings[i];
                BindingRow existing = rows.Find(row => RowMatches(row, definition.Action, definition.Part));
                if (existing != null)
                {
                    existing.includeMouseBindings = definition.IncludeMouse;
                    continue;
                }

                BindingRow newRow = CloneBindingRow(template, definition);
                if (newRow != null)
                {
                    rows.Add(newRow);
                }
            }
        }

        // 핵심 분기: scrollRect == null || scrollRect.viewport == null || scrollRect.content == null 판정.
        // 상태 변경: hitArea 갱신.
        // 다음 연결: SettingsMenuUI.DynamicVerticalScroll.Configure(UnityEngine.UI.ScrollRect, UnityEngine.RectTransform, UnityEng… 호출.
        private void EnsureScrollSupport()
        {
            ScrollRect scrollRect = GetComponentInChildren<ScrollRect>(true);
            if (scrollRect == null || scrollRect.viewport == null || scrollRect.content == null)
            {
                return;
            }

            // A transparent Graphic gives the whole viewport a UI raycast target.
            // Without it, wheel events only arrive while the pointer is directly
            // over one of the small key buttons.
            Image hitArea = scrollRect.viewport.GetComponent<Image>();
            if (hitArea == null)
            {
                hitArea = scrollRect.viewport.gameObject.AddComponent<Image>();
                hitArea.color = Color.clear;
            }

            hitArea.raycastTarget = true;

            DynamicVerticalScroll dynamicScroll = scrollRect.GetComponent<DynamicVerticalScroll>();
            if (dynamicScroll == null)
            {
                dynamicScroll = scrollRect.gameObject.AddComponent<DynamicVerticalScroll>();
            }

            dynamicScroll.Configure(
                scrollRect,
                scrollRect.viewport,
                scrollRect.content,
                scrollRect.verticalScrollbar);
            Canvas.ForceUpdateCanvases();
            dynamicScroll.Refresh();
        }

        // 핵심 분기: label != null 판정.
        // 상태 변경: clone.name 갱신.
        private static BindingRow CloneBindingRow(BindingRow template, BindingDefinition definition)
        {
            Transform templateRoot = template.mainButton.transform.parent;
            GameObject clone = Instantiate(templateRoot.gameObject, templateRoot.parent);
            clone.name = definition.Action + (string.IsNullOrEmpty(definition.Part) ? "Row" : definition.Part + "Row");
            clone.transform.SetAsLastSibling();

            TMP_Text label = clone.transform.Find("ActionLabel")?.GetComponent<TMP_Text>();
            if (label != null)
            {
                label.text = definition.Label;
            }

            Button mainButton = clone.transform.Find("MainKeyButton")?.GetComponent<Button>();
            Button subButton = clone.transform.Find("SubKeyButton")?.GetComponent<Button>();
            return new BindingRow
            {
                actionName = definition.Action,
                partName = definition.Part,
                includeMouseBindings = definition.IncludeMouse,
                mainButton = mainButton,
                mainText = mainButton?.transform.Find("Text")?.GetComponent<TMP_Text>(),
                subButton = subButton,
                subText = subButton?.transform.Find("Text")?.GetComponent<TMP_Text>()
            };
        }

        // 핵심 분기: row == null 판정.
        // 다음 연결: SettingsMenuUI.ControlSettingsController.UnregisterButtonListeners() 호출.
        private void RegisterButtonListeners()
        {
            UnregisterButtonListeners();
            for (int i = 0; i < rows.Count; i++)
            {
                BindingRow row = rows[i];
                if (row == null)
                {
                    continue;
                }

                BindingRow capturedRow = row;
                if (row.mainButton != null)
                {
                    UnityEngine.Events.UnityAction listener = () => BeginRebind(capturedRow, false);
                    row.mainButton.onClick.AddListener(listener);
                    removeListenerActions.Add(() => row.mainButton.onClick.RemoveListener(listener));
                }

                if (row.subButton != null)
                {
                    UnityEngine.Events.UnityAction listener = () => BeginRebind(capturedRow, true);
                    row.subButton.onClick.AddListener(listener);
                    removeListenerActions.Add(() => row.subButton.onClick.RemoveListener(listener));
                }
            }
        }

        private void UnregisterButtonListeners()
        {
            for (int i = 0; i < removeListenerActions.Count; i++)
            {
                removeListenerActions[i]?.Invoke();
            }

            removeListenerActions.Clear();
        }

        private InputAction FindRowAction(BindingRow row)
        {
            if (playerActionMap == null || row == null)
            {
                return null;
            }

            string rowActionName = string.IsNullOrEmpty(row.actionName) ? actionName : row.actionName;
            return playerActionMap.FindAction(rowActionName, false);
        }

        // 핵심 분기: row == null || action == null 판정.
        // 다음 연결: SettingsMenuUI.ControlSettingsController.IsKeyboardPath(string) 호출.
        private static List<int> FindBindingIndices(BindingRow row, InputAction action)
        {
            var result = new List<int>(2);
            if (row == null || action == null)
            {
                return result;
            }

            for (int i = 0; i < action.bindings.Count && result.Count < 2; i++)
            {
                InputBinding binding = action.bindings[i];
                string path = binding.effectivePath;
                if (!string.IsNullOrEmpty(row.partName))
                {
                    if (binding.isPartOfComposite
                        && string.Equals(binding.name, row.partName, StringComparison.OrdinalIgnoreCase)
                        && IsKeyboardPath(path))
                    {
                        result.Add(i);
                    }

                    continue;
                }

                if (binding.isComposite || binding.isPartOfComposite)
                {
                    continue;
                }

                if (IsKeyboardPath(path) || row.includeMouseBindings && IsMousePath(path))
                {
                    result.Add(i);
                }
            }

            return result;
        }

        private void BeginRebind(BindingRow row, bool secondary)
        {
            InputAction action = FindRowAction(row);
            List<int> indices = FindBindingIndices(row, action);
            int listIndex = secondary ? 1 : 0;
            if (action == null || indices.Count <= listIndex || !IsRebindableBinding(action, indices[listIndex]))
            {
                return;
            }

            TMP_Text targetText = secondary ? row.subText : row.mainText;
            Button targetButton = secondary ? row.subButton : row.mainButton;
            BeginRebind(action, indices[listIndex], targetText, targetButton);
        }

        // 핵심 분기: action == null 판정.
        // 상태 변경: rebindingAction 갱신.
        // 다음 연결: SettingsMenuUI.ControlSettingsController.CancelCurrentRebind() 호출.
        private void BeginRebind(InputAction action, int bindingIndex, TMP_Text targetText, Button targetButton)
        {
            CancelCurrentRebind();
            if (action == null)
            {
                return;
            }

            rebindingAction = action;
            InputBinding previousBinding = action.bindings[bindingIndex];
            rebindingIndex = bindingIndex;
            previousHadOverride = previousBinding.hasOverrides;
            previousOverridePath = previousBinding.overridePath;
            previousOverrideProcessors = previousBinding.overrideProcessors;
            previousOverrideInteractions = previousBinding.overrideInteractions;

            restoreActionEnabled = action.enabled;
            if (restoreActionEnabled)
            {
                action.Disable();
            }

            rebindingText = targetText;
            rebindingBackground = targetButton != null ? targetButton.targetGraphic as Image : null;
            if (rebindingText != null)
            {
                rebindingText.text = "키 입력...";
            }

            if (rebindingBackground != null)
            {
                rebindingOriginalColor = rebindingBackground.color;
                rebindingBackground.color = new Color32(150, 163, 194, 255);
            }

            suppressCancelWarning = false;
            invalidCandidateDetected = false;
            rebindingOperation = action.PerformInteractiveRebinding(bindingIndex)
                .WithControlsExcluding("<Mouse>")
                .WithControlsExcluding("<Gamepad>")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnPotentialMatch(HandlePotentialMatch)
                .OnCancel(_ => FinishRebind(false))
                .OnComplete(_ => FinishRebind(true));
            rebindingOperation.Start();
        }

        // 핵심 분기: completed && rebindingAction != null && rebindingIndex >= 0 판정.
        // 상태 변경: invalid 갱신.
        // 다음 연결: SettingsMenuUI.ControlSettingsController.IsDuplicateBinding(UnityEngine.InputSystem.InputAction, int) 호출.
        private void FinishRebind(bool completed)
        {
            bool invalid = false;
            if (completed && rebindingAction != null && rebindingIndex >= 0)
            {
                invalid = IsDuplicateBinding(rebindingAction, rebindingIndex);
                if (invalid)
                {
                    RestorePreviousBinding();
                }
                else
                {
                    SaveBindingOverrides();
                    PlayerPrefs.Save();
                }
            }

            if (!completed && !suppressCancelWarning)
            {
                lastRejectedInputTime = Time.realtimeSinceStartupAsDouble;
            }

            bool showWarning = invalid || invalidCandidateDetected;
            DisposeOperation();
            RefreshAllLabels();
            if (showWarning)
            {
                lastRejectedInputTime = Time.realtimeSinceStartupAsDouble;
                warningPopup?.ShowWarning(InvalidKeyMessage);
            }
        }

        private void HandlePotentialMatch(InputActionRebindingExtensions.RebindingOperation operation)
        {
            InputControl candidate = operation.selectedControl;
            if (candidate == null)
            {
                return;
            }

            if (IsDuplicateControl(candidate, rebindingAction, rebindingIndex))
            {
                invalidCandidateDetected = true;
                operation.Cancel();
                return;
            }

            operation.Complete();
        }

        // 핵심 분기: InputControlPath.Matches("<Keyboard>/escape", candidate) 판정.
        // 다음 연결: SettingsMenuUI.ControlSettingsController.IsManagedAction(string) 호출.
        private bool IsDuplicateControl(InputControl candidate, InputAction changedAction, int changedIndex)
        {
            if (InputControlPath.Matches("<Keyboard>/escape", candidate))
            {
                return true;
            }

            if (playerActionMap == null)
            {
                return false;
            }

            foreach (InputAction action in playerActionMap.actions)
            {
                if (!IsManagedAction(action.name))
                {
                    continue;
                }

                for (int i = 0; i < action.bindings.Count; i++)
                {
                    if (action == changedAction && i == changedIndex)
                    {
                        continue;
                    }

                    InputBinding binding = action.bindings[i];
                    if (!binding.isComposite && InputControlPath.Matches(binding.effectivePath, candidate))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // 핵심 분기: string.IsNullOrEmpty(candidatePath) || playerActionMap == null 판정.
        // 다음 연결: SettingsMenuUI.ControlSettingsController.IsManagedAction(string) 호출.
        private bool IsDuplicateBinding(InputAction changedAction, int changedIndex)
        {
            string candidatePath = changedAction.bindings[changedIndex].effectivePath;
            if (string.IsNullOrEmpty(candidatePath) || playerActionMap == null)
            {
                return true;
            }

            foreach (InputAction action in playerActionMap.actions)
            {
                if (!IsManagedAction(action.name))
                {
                    continue;
                }

                for (int i = 0; i < action.bindings.Count; i++)
                {
                    if (action == changedAction && i == changedIndex)
                    {
                        continue;
                    }

                    InputBinding binding = action.bindings[i];
                    if (!binding.isComposite
                        && string.Equals(candidatePath, binding.effectivePath, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsManagedAction(string targetAction)
        {
            for (int i = 0; i < CurrentPlayerBindings.Length; i++)
            {
                if (string.Equals(CurrentPlayerBindings[i].Action, targetAction, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool RowMatches(BindingRow row, string targetAction, string targetPart)
        {
            return row != null
                && string.Equals(row.actionName, targetAction, StringComparison.OrdinalIgnoreCase)
                && string.Equals(row.partName ?? string.Empty, targetPart ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRebindableBinding(InputAction action, int bindingIndex)
        {
            return action != null
                && bindingIndex >= 0
                && bindingIndex < action.bindings.Count
                && IsKeyboardPath(action.bindings[bindingIndex].effectivePath);
        }

        private static bool IsKeyboardPath(string path)
        {
            return !string.IsNullOrEmpty(path) && path.IndexOf("<Keyboard>", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsMousePath(string path)
        {
            return !string.IsNullOrEmpty(path) && path.IndexOf("<Mouse>", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void RestorePreviousBinding()
        {
            if (rebindingAction == null || rebindingIndex < 0)
            {
                return;
            }

            if (!previousHadOverride)
            {
                rebindingAction.RemoveBindingOverride(rebindingIndex);
                return;
            }

            rebindingAction.ApplyBindingOverride(rebindingIndex, new InputBinding
            {
                overridePath = previousOverridePath,
                overrideProcessors = previousOverrideProcessors,
                overrideInteractions = previousOverrideInteractions
            });
        }

        // 핵심 분기: rebindingOperation != null 판정.
        // 상태 변경: rebindingOperation 갱신.
        private void DisposeOperation()
        {
            if (rebindingOperation != null)
            {
                rebindingOperation.Dispose();
                rebindingOperation = null;
            }

            if (rebindingBackground != null)
            {
                rebindingBackground.color = rebindingOriginalColor;
            }

            if (restoreActionEnabled && rebindingAction != null)
            {
                rebindingAction.Enable();
            }

            rebindingAction = null;
            rebindingBackground = null;
            rebindingText = null;
            rebindingIndex = -1;
            previousHadOverride = false;
            previousOverridePath = null;
            previousOverrideProcessors = null;
            previousOverrideInteractions = null;
            invalidCandidateDetected = false;
            restoreActionEnabled = false;
        }

        private static void SetBindingLabel(TMP_Text target, InputAction action, int bindingIndex)
        {
            if (target == null)
            {
                return;
            }

            if (action == null || bindingIndex < 0)
            {
                target.text = "-";
                return;
            }

            string display = action.GetBindingDisplayString(bindingIndex, out _, out _);
            target.text = ToCompactDisplay(display);
        }

        private static string ToCompactDisplay(string display)
        {
            switch (display)
            {
                case "Up Arrow": return "↑";
                case "Down Arrow": return "↓";
                case "Left Arrow": return "←";
                case "Right Arrow": return "→";
                case "Left Button": return "좌클릭";
                case "Left Shift": return "Shift";
                case "Left Ctrl": return "Ctrl";
                default: return string.IsNullOrWhiteSpace(display) ? "-" : display;
            }
        }
    }
}
