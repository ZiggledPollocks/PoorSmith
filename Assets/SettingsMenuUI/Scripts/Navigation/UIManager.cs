// [코드 지도] UIManager: 설정 메뉴의 화면 전환과 공통 UI 표시를 조정한다.
// 주요 함수: SubscribeToCancelAction, ToggleSettings, CloseSettings
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/SettingsMenuUI/Scripts/Navigation/UIManager.cs.md

using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace SettingsMenuUI
{
    [DefaultExecutionOrder(-90)]
    [DisallowMultipleComponent]
    public sealed class UIManager : MonoBehaviour
    {
        [Header("Settings UI")]
        [SerializeField, Tooltip("Root GameObject of the in-game settings menu.")]
        private GameObject settingsRoot;
        [SerializeField] private ControlSettingsController controlSettingsController;

        [Header("Game Flow")]
        [SerializeField] private GameStateManager gameStateManager;

        [Header("Input System")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string uiActionMapName = "UI";
        [SerializeField] private string cancelActionName = "Cancel";

        private InputAction cancelAction;
        private GameUIController.Activity settingsActivity;
        private bool stateSubscribed;
        private bool inputSubscribed;
        private bool cancelActionEnabledByThisManager;
        [SerializeField] private bool externalNavigation;
        public void UseExternalNavigation() => externalNavigation = true;

        public bool IsSettingsOpen => settingsRoot != null && settingsRoot.activeSelf;
        public event Action OnSettingsOpened;
        public event Action OnSettingsClosed;

        public void Configure(
            GameObject root,
            ControlSettingsController controlController,
            GameStateManager stateManager,
            InputActionAsset actions)
        {
            settingsRoot = root;
            controlSettingsController = controlController;
            gameStateManager = stateManager;
            inputActions = actions;
        }

        private void Awake()
        {
            ResolveCancelAction();
            if (settingsRoot != null)
            {
                settingsRoot.SetActive(false);
            }
        }

        private void OnEnable()
        {
            SubscribeToGameState();
            if (!externalNavigation) SubscribeToCancelAction();
            if (gameStateManager != null && gameStateManager.CurrentState != GameState.Playing)
            {
                CloseSettings();
            }
        }

        private void OnDisable()
        {
            settingsActivity?.Dispose();settingsActivity=null;
            UnsubscribeFromCancelAction();
            UnsubscribeFromGameState();
        }

        public void OpenSettings()
        {
            if (settingsRoot == null || IsSettingsOpen ||
                gameStateManager == null || gameStateManager.CurrentState == GameState.Loading)
            {
                return;
            }

            // Title and in-game settings share the same editable presentation guide.
            ScreenLayoutBinding.Bind("SettingsMenu",settingsRoot.transform,true);
            GameArtSkin.Apply(settingsRoot.transform);
            // The authored title menu is created later; keep settings above it without hiding its state.
            settingsRoot.transform.SetAsLastSibling();
            GameSound.UI("Popup",this);
            settingsRoot.SetActive(true);
            settingsActivity=GameUIController.Acquire(this,CancelTop,true,false,310);
            (GetComponent<SettingsSaveManager>()??FindFirstObjectByType<SettingsSaveManager>(FindObjectsInactive.Include))?.BeginEditing();
            OnSettingsOpened?.Invoke();
        }

        // Escape cancels a rebind/dropdown before dismissing the settings screen.
        void CancelTop()
        {
            if(controlSettingsController?.ShouldBlockSettingsToggle==true){controlSettingsController.CancelCurrentRebind();return;}
            foreach(var dropdown in settingsRoot.GetComponentsInChildren<TMPro.TMP_Dropdown>())
                if(dropdown.IsExpanded){dropdown.Hide();return;}
            CloseSettings();
        }
        public void CloseSettings()
        {
            if (settingsRoot == null || !IsSettingsOpen)
            {
                return;
            }

            (GetComponent<SettingsSaveManager>()??FindFirstObjectByType<SettingsSaveManager>(FindObjectsInactive.Include))?.CancelEditing();
            controlSettingsController?.CancelCurrentRebind();
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }

            settingsRoot.SetActive(false);
            settingsActivity?.Dispose();settingsActivity=null;
            OnSettingsClosed?.Invoke();
        }

        // 핵심 분기: controlSettingsController != null && controlSettingsController.ShouldBlockSettingsToggle 판정.
        // 다음 연결: SettingsMenuUI.UIManager.CloseSettings() 호출.
        public void ToggleSettings()
        {
            if (controlSettingsController != null && controlSettingsController.ShouldBlockSettingsToggle)
            {
                return;
            }

            if (gameStateManager == null || gameStateManager.CurrentState != GameState.Playing)
            {
                return;
            }

            if (IsSettingsOpen)
            {
                CloseSettings();
            }
            else
            {
                OpenSettings();
            }
        }

        private void HandleCancelPerformed(InputAction.CallbackContext context)
        {
            if(GameUIController.CancelPressed && !GameUIController.HandleEscape())ToggleSettings();
        }

        private void HandleGameStateChanged(GameState state)
        {
            if (state != GameState.Playing)
            {
                CloseSettings();
            }
        }

        private void ResolveCancelAction()
        {
            cancelAction = inputActions?.FindActionMap(uiActionMapName, false)?.FindAction(cancelActionName, false);
        }

        private void SubscribeToGameState()
        {
            if (stateSubscribed || gameStateManager == null)
            {
                return;
            }

            gameStateManager.OnGameStateChanged += HandleGameStateChanged;
            stateSubscribed = true;
        }

        private void UnsubscribeFromGameState()
        {
            if (!stateSubscribed || gameStateManager == null)
            {
                return;
            }

            gameStateManager.OnGameStateChanged -= HandleGameStateChanged;
            stateSubscribed = false;
        }

        // 핵심 분기: inputSubscribed 판정.
        // 상태 변경: cancelAction.performed 갱신.
        // 다음 연결: SettingsMenuUI.UIManager.ResolveCancelAction() 호출.
        private void SubscribeToCancelAction()
        {
            if (inputSubscribed)
            {
                return;
            }

            if (cancelAction == null)
            {
                ResolveCancelAction();
            }

            if (cancelAction == null)
            {
                Debug.LogError($"[{nameof(UIManager)}] Input action '{uiActionMapName}/{cancelActionName}' was not found.", this);
                return;
            }

            cancelAction.performed += HandleCancelPerformed;
            if (!cancelAction.enabled)
            {
                cancelAction.Enable();
                cancelActionEnabledByThisManager = true;
            }

            inputSubscribed = true;
        }

        private void UnsubscribeFromCancelAction()
        {
            if (!inputSubscribed || cancelAction == null)
            {
                return;
            }

            cancelAction.performed -= HandleCancelPerformed;
            if (cancelActionEnabledByThisManager)
            {
                cancelAction.Disable();
                cancelActionEnabledByThisManager = false;
            }

            inputSubscribed = false;
        }
    }
}
