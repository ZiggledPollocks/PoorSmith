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
            public bool largeText,reducedFlash;
            public float shakeStrength=1;
            public int frameLimit;
        }

        [SerializeField] private GameSettingsController gameSettings;
        [SerializeField] private ControlSettingsController controlSettings;
        [SerializeField] private SoundSettingsController soundSettings;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button saveAndExitButton;

        private bool listenersRegistered;
        private bool loaded;
        private bool lastSaveSucceeded;
        private SettingsData editingOriginal;
        private string originalBindings;
        public bool IsEditing => editingOriginal!=null;
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
            lastSaveSucceeded=false;
            if (gameSettings == null || soundSettings == null)
            {
                Debug.LogError($"[{nameof(SettingsSaveManager)}] Required controller reference is missing.", this);
                return;
            }

            if(GameSavePaths.IsUITest)return;
            if(gameSettings.DisplayConfirmationPending)
            { ReleaseDecisionUI.For(gameObject).Show("먼저 화면 설정을 확인하거나 되돌려 주세요.","확인","닫기",()=>{});return; }
            try
            {
            var data = Capture();
            store.Write(JsonUtility.ToJson(data));
            if(controlSettings!=null){controlSettings.DeferBindingSave=false;controlSettings.SaveBindingOverrides();controlSettings.DeferBindingSave=IsEditing;}
            store.Flush();
            if(IsEditing){editingOriginal=Capture();originalBindings=controlSettings?.CaptureBindings();}
            lastSaveSucceeded=true;
            }
            catch(Exception exception)
            {
                Debug.LogWarning("Settings save failed: "+exception,this);
                ReleaseDecisionUI.For(gameObject).Show("설정을 저장하지 못했습니다. 다시 시도하시겠습니까?","다시 저장","취소",SaveSettings);
            }
        }

        SettingsData Capture()
        {
            return new SettingsData
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
                sfxMuted = soundSettings.SfxMuted,
                largeText=ReleasePreferences.LargeText,reducedFlash=ReleasePreferences.ReducedFlash,
                shakeStrength=ReleasePreferences.ShakeStrength,frameLimit=ReleasePreferences.FrameLimit
            };

        }
        public void BeginEditing()
        {
            if(IsEditing||gameSettings==null||soundSettings==null)return;
            editingOriginal=Capture();originalBindings=controlSettings?.CaptureBindings();
            if(controlSettings!=null)controlSettings.DeferBindingSave=true;
            BuildEditingButtons();
        }
        public void CancelEditing()
        {
            if(!IsEditing)return;
            gameSettings.RevertDisplay();ApplyData(editingOriginal);
            controlSettings?.RestoreBindings(originalBindings);
            editingOriginal=null;originalBindings=null;
            if(controlSettings!=null)controlSettings.DeferBindingSave=false;
        }
        void ApplyData(SettingsData data)
        {
            gameSettings.LoadSavedSettings(data.resolutionWidth,data.resolutionHeight,(FullScreenMode)data.screenMode,data.autoSaveEnabled,true);
            soundSettings.LoadSavedSettings(data.masterVolume,data.masterMuted,data.bgmVolume,data.bgmMuted,data.sfxVolume,data.sfxMuted);
            ReleasePreferences.Apply(data.largeText,data.shakeStrength,data.reducedFlash,data.frameLimit);
        }
        void BuildEditingButtons()
        {
            if(saveButton==null)return;
            saveButton.gameObject.SetActive(true);saveButton.GetComponentInChildren<TMPro.TMP_Text>().text="적용";
            if(saveAndExitButton!=null){saveAndExitButton.gameObject.SetActive(true);saveAndExitButton.GetComponentInChildren<TMPro.TMP_Text>().text="취소";}
            if(saveButton.transform.parent.Find("ResetSettings")!=null)return;
            var reset=Instantiate(saveButton,saveButton.transform.parent,false);reset.name="ResetSettings";
            var rect=(RectTransform)reset.transform;rect.anchorMin=rect.anchorMax=new Vector2(0,.5f);rect.pivot=new Vector2(.5f,.5f);rect.sizeDelta=new Vector2(260,96);rect.anchoredPosition=new Vector2(-154,0);
            var resetLabel=reset.GetComponentInChildren<TMPro.TMP_Text>();resetLabel.text="설정 초기화";
            // A cloned label must use the new button's content area, including on narrower screens.
            resetLabel.rectTransform.anchorMin=Vector2.zero;resetLabel.rectTransform.anchorMax=Vector2.one;
            resetLabel.rectTransform.offsetMin=new Vector2(16,12);resetLabel.rectTransform.offsetMax=new Vector2(-16,-12);
            resetLabel.alignment=TMPro.TextAlignmentOptions.Center;resetLabel.margin=Vector4.zero;
            reset.onClick.RemoveAllListeners();
            reset.onClick.AddListener(()=>{gameSettings.RevertDisplay();ApplyData(new SettingsData{autoSaveEnabled=true,masterVolume=1,bgmVolume=1,sfxVolume=1,shakeStrength=1});controlSettings?.RestoreBindings("");});
        }
        void CancelButton(){CancelEditing();GetComponent<UIManager>()?.CloseSettings();}

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

            if(!json.Contains("\"shakeStrength\""))data.shakeStrength=1;
            ReleasePreferences.Apply(data.largeText,data.shakeStrength,data.reducedFlash,data.frameLimit);
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
            if(!lastSaveSucceeded)return;
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
            saveAndExitButton?.onClick.AddListener(CancelButton);
            listenersRegistered = true;
        }

        private void UnregisterListeners()
        {
            if (!listenersRegistered)
            {
                return;
            }

            saveButton?.onClick.RemoveListener(SaveSettings);
            saveAndExitButton?.onClick.RemoveListener(CancelButton);
            listenersRegistered = false;
        }
    }
}
