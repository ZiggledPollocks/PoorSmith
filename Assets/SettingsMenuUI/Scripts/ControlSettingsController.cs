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
            public string partName;
            public Button mainButton;
            public TMP_Text mainText;
            public Button subButton;
            public TMP_Text subText;
        }

        private const string BindingOverridesKey = "SettingsMenu.InputBindingOverrides";
        private const string InvalidKeyMessage = "이미 입력된 키";

        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string actionName = "Move";
        [SerializeField] private List<BindingRow> rows = new List<BindingRow>();
        [SerializeField] private SettingsPopupController warningPopup;

        private readonly List<Action> removeListenerActions = new List<Action>();
        private InputAction moveAction;
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
        public InputAction MoveAction => moveAction;
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
            ResolveAction();
            LoadBindingOverrides();
            RefreshAllLabels();
        }

        private void Awake()
        {
            ResolveAction();
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

        public void RefreshAllLabels()
        {
            if (moveAction == null)
            {
                ResolveAction();
            }

            for (int i = 0; i < rows.Count; i++)
            {
                BindingRow row = rows[i];
                List<int> indices = FindKeyboardBindingIndices(row.partName);
                SetBindingLabel(row.mainText, indices.Count > 0 ? indices[0] : -1);
                SetBindingLabel(row.subText, indices.Count > 1 ? indices[1] : -1);
                if (row.mainButton != null)
                {
                    row.mainButton.interactable = indices.Count > 0;
                }

                if (row.subButton != null)
                {
                    row.subButton.interactable = indices.Count > 1;
                }
            }
        }

        public void BeginRebind(string partName, bool secondary)
        {
            if (moveAction == null)
            {
                ResolveAction();
            }

            List<int> indices = FindKeyboardBindingIndices(partName);
            int listIndex = secondary ? 1 : 0;
            if (moveAction == null || indices.Count <= listIndex)
            {
                Debug.LogWarning($"[{nameof(ControlSettingsController)}] Keyboard binding '{partName}' ({(secondary ? "sub" : "main")}) was not found.", this);
                return;
            }

            BindingRow row = rows.Find(item => string.Equals(item.partName, partName, StringComparison.OrdinalIgnoreCase));
            TMP_Text targetText = secondary ? row?.subText : row?.mainText;
            Button targetButton = secondary ? row?.subButton : row?.mainButton;
            BeginRebind(indices[listIndex], targetText, targetButton);
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

        private void ResolveAction()
        {
            moveAction = inputActions?.FindActionMap(actionMapName, false)?.FindAction(actionName, false);
            if (inputActions != null && moveAction == null)
            {
                Debug.LogError($"[{nameof(ControlSettingsController)}] Action '{actionMapName}/{actionName}' was not found.", this);
            }
        }

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

                string capturedPart = row.partName;
                if (row.mainButton != null)
                {
                    UnityEngine.Events.UnityAction listener = () => BeginRebind(capturedPart, false);
                    row.mainButton.onClick.AddListener(listener);
                    removeListenerActions.Add(() => row.mainButton.onClick.RemoveListener(listener));
                }

                if (row.subButton != null)
                {
                    UnityEngine.Events.UnityAction listener = () => BeginRebind(capturedPart, true);
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

        private List<int> FindKeyboardBindingIndices(string partName)
        {
            var result = new List<int>(2);
            if (moveAction == null)
            {
                return result;
            }

            for (int i = 0; i < moveAction.bindings.Count; i++)
            {
                InputBinding binding = moveAction.bindings[i];
                if (!binding.isPartOfComposite || !string.Equals(binding.name, partName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string path = string.IsNullOrEmpty(binding.overridePath) ? binding.path : binding.overridePath;
                if (IsKeyboardPath(path))
                {
                    result.Add(i);
                }
            }

            return result;
        }

        private void BeginRebind(int bindingIndex, TMP_Text targetText, Button targetButton)
        {
            CancelCurrentRebind();
            if (moveAction == null)
            {
                return;
            }

            InputBinding previousBinding = moveAction.bindings[bindingIndex];
            rebindingIndex = bindingIndex;
            previousHadOverride = previousBinding.hasOverrides;
            previousOverridePath = previousBinding.overridePath;
            previousOverrideProcessors = previousBinding.overrideProcessors;
            previousOverrideInteractions = previousBinding.overrideInteractions;

            restoreActionEnabled = moveAction.enabled;
            if (restoreActionEnabled)
            {
                moveAction.Disable();
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
            rebindingOperation = moveAction.PerformInteractiveRebinding(bindingIndex)
                .WithControlsExcluding("<Mouse>")
                .WithControlsExcluding("<Gamepad>")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnPotentialMatch(HandlePotentialMatch)
                .OnCancel(_ => FinishRebind(false))
                .OnComplete(_ => FinishRebind(true));
            rebindingOperation.Start();
        }

        private void FinishRebind(bool completed)
        {
            bool invalid = false;
            if (completed && rebindingIndex >= 0)
            {
                invalid = IsDuplicateBinding(rebindingIndex);
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

            if (!completed) lastRejectedInputTime = Time.realtimeSinceStartupAsDouble;
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

            if (IsDuplicateControl(candidate, rebindingIndex))
            {
                invalidCandidateDetected = true;
                operation.Cancel();
                return;
            }

            operation.Complete();
        }

        private bool IsDuplicateControl(InputControl candidate, int changedIndex)
        {
            // Movement must not steal inventory, jump, tool-slot or UI cancel keys.
            if (InputControlPath.Matches("<Keyboard>/escape", candidate)) return true;
            foreach (InputAction action in moveAction.actionMap.actions)
            {
                if (action == moveAction) continue;
                foreach (InputBinding other in action.bindings)
                    if (!other.isComposite && InputControlPath.Matches(other.effectivePath, candidate))
                        return true;
            }
            for (int i = 0; i < moveAction.bindings.Count; i++)
            {
                if (i == changedIndex)
                {
                    continue;
                }

                InputBinding binding = moveAction.bindings[i];
                if (IsManagedMovementBinding(binding) && InputControlPath.Matches(binding.effectivePath, candidate))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsDuplicateBinding(int changedIndex)
        {
            string candidatePath = moveAction.bindings[changedIndex].effectivePath;
            if (string.IsNullOrEmpty(candidatePath))
            {
                return true;
            }

            for (int i = 0; i < moveAction.bindings.Count; i++)
            {
                if (i == changedIndex)
                {
                    continue;
                }

                InputBinding binding = moveAction.bindings[i];
                if (!IsManagedMovementBinding(binding))
                {
                    continue;
                }

                if (string.Equals(candidatePath, binding.effectivePath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsManagedMovementBinding(InputBinding binding)
        {
            if (!binding.isPartOfComposite || !IsKeyboardPath(binding.effectivePath))
            {
                return false;
            }

            return string.Equals(binding.name, "up", StringComparison.OrdinalIgnoreCase)
                || string.Equals(binding.name, "down", StringComparison.OrdinalIgnoreCase)
                || string.Equals(binding.name, "left", StringComparison.OrdinalIgnoreCase)
                || string.Equals(binding.name, "right", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsKeyboardPath(string path)
        {
            return !string.IsNullOrEmpty(path) && path.IndexOf("<Keyboard>", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void RestorePreviousBinding()
        {
            if (rebindingIndex < 0)
            {
                return;
            }

            if (!previousHadOverride)
            {
                moveAction.RemoveBindingOverride(rebindingIndex);
                return;
            }

            moveAction.ApplyBindingOverride(rebindingIndex, new InputBinding
            {
                overridePath = previousOverridePath,
                overrideProcessors = previousOverrideProcessors,
                overrideInteractions = previousOverrideInteractions
            });
        }

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

            rebindingBackground = null;
            rebindingText = null;
            rebindingIndex = -1;
            previousHadOverride = false;
            previousOverridePath = null;
            previousOverrideProcessors = null;
            previousOverrideInteractions = null;
            invalidCandidateDetected = false;
            if (restoreActionEnabled && moveAction != null)
            {
                moveAction.Enable();
            }

            restoreActionEnabled = false;
        }

        private void SetBindingLabel(TMP_Text target, int bindingIndex)
        {
            if (target == null)
            {
                return;
            }

            if (moveAction == null || bindingIndex < 0)
            {
                target.text = "-";
                return;
            }

            string display = moveAction.GetBindingDisplayString(bindingIndex, out _, out _);
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
                default: return string.IsNullOrWhiteSpace(display) ? "-" : display;
            }
        }
    }
}
