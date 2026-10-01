// [코드 지도] SettingsSaveManager: 설정 값을 저장 매체에 기록하고 다시 불러온다.
// 주요 함수: LoadSettings, SaveSettings, Configure
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/SettingsMenuUI/Scripts/Persistence/SettingsSaveManager.cs.md

using System;
using UnityEngine;
using UnityEngine.UI;

namespace SettingsMenuUI
{
    [DisallowMultipleComponent]
    public sealed class SettingsSaveManager : MonoBehaviour
    {
        private const string SettingsKey = "SettingsMenu.SettingsData.v1";

        [Serializable]
        private sealed class SettingsData
        {
            public int resolutionWidth = GameSettingsController.DefaultResolution.Width;
            public int resolutionHeight = GameSettingsController.DefaultResolution.Height;
            public int screenMode;
            public bool autoSaveEnabled;
            public float masterVolume;
            public float bgmVolume;
            public float sfxVolume;
            public bool masterMuted;
            public bool bgmMuted;
            public bool sfxMuted;
        }

        [SerializeField] private GameSettingsController gameSettings;
        [SerializeField] private ControlSettingsController controlSettings;
        [SerializeField] private SoundSettingsController soundSettings;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button saveAndExitButton;

        private bool listenersRegistered;
        private bool loaded;
        private readonly ITextStore store = new PlayerPrefsTextStore(SettingsKey);

        public void Configure(
            GameSettingsController gameController,
            ControlSettingsController controlController,
            SoundSettingsController soundController,
            Button save,
            Button saveAndExit)
        {
            gameSettings = gameController;
            controlSettings = controlController;
            soundSettings = soundController;
            saveButton = save;
            saveAndExitButton = saveAndExit;
        }

        private void OnEnable()
        {
            RegisterListeners();
        }

        private void Start()
        {
            if (!loaded) LoadSettings();
        }

        private void OnDisable()
        {
            UnregisterListeners();
        }

        // 핵심 분기: gameSettings == null || soundSettings == null 판정.
        // 상태 변경: resolutionWidth 갱신.
        // 다음 연결: ITextStore.Write(string) 호출.
        public void SaveSettings()
        {
            if (gameSettings == null || soundSettings == null)
            {
                Debug.LogError($"[{nameof(SettingsSaveManager)}] Required controller reference is missing.", this);
                return;
            }

            var data = new SettingsData
            {
                resolutionWidth = gameSettings.SelectedWidth,
                resolutionHeight = gameSettings.SelectedHeight,
                screenMode = (int)gameSettings.SelectedScreenMode,
                autoSaveEnabled = gameSettings.AutoSaveEnabled,
                masterVolume = soundSettings.MasterVolume,
                bgmVolume = soundSettings.BgmVolume,
                sfxVolume = soundSettings.SfxVolume,
                masterMuted = soundSettings.MasterMuted,
                bgmMuted = soundSettings.BgmMuted,
                sfxMuted = soundSettings.SfxMuted
            };

            store.Write(JsonUtility.ToJson(data));
            controlSettings?.SaveBindingOverrides();
            store.Flush();
        }

        // 핵심 분기: !store.Exists 판정.
        // 상태 변경: loaded 갱신.
        // 다음 연결: SettingsMenuUI.GameSettingsController.EnsureInitialized() 호출.
        public void LoadSettings()
        {
            loaded = true;
            gameSettings?.EnsureInitialized();
            soundSettings?.EnsureInitialized();
            controlSettings?.LoadBindingOverrides();
            controlSettings?.RefreshAllLabels();
            if (!store.Exists)
            {
                soundSettings?.InitializeFromUiDefaults();
                return;
            }

            string json = store.Read();
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            SettingsData data;
            try
            {
                data = JsonUtility.FromJson<SettingsData>(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[{nameof(SettingsSaveManager)}] Saved settings could not be read: {exception.Message}", this);
                return;
            }

            if (data == null)
            {
                return;
            }

            gameSettings?.LoadSavedSettings(
                data.resolutionWidth,
                data.resolutionHeight,
                (FullScreenMode)data.screenMode,
                data.autoSaveEnabled,
                true);
            soundSettings?.LoadSavedSettings(
                data.masterVolume,
                data.masterMuted,
                data.bgmVolume,
                data.bgmMuted,
                data.sfxVolume,
                data.sfxMuted);
        }

        public void SaveAndExit()
        {
            SaveSettings();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void RegisterListeners()
        {
            if (listenersRegistered)
            {
                return;
            }

            saveButton?.onClick.AddListener(SaveSettings);
            saveAndExitButton?.onClick.AddListener(SaveAndExit);
            listenersRegistered = true;
        }

        private void UnregisterListeners()
        {
            if (!listenersRegistered)
            {
                return;
            }

            saveButton?.onClick.RemoveListener(SaveSettings);
            saveAndExitButton?.onClick.RemoveListener(SaveAndExit);
            listenersRegistered = false;
        }
    }
}
