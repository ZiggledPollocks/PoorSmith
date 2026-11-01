// [코드 지도] SoundSettingsController: 음량 설정과 오디오 믹서 값을 연결한다.
// 주요 함수: RefreshChannel, OnSliderChanged, ApplyVolume
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/SettingsMenuUI/Scripts/Audio/SoundSettingsController.cs.md

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SettingsMenuUI
{
    public enum SoundChannel
    {
        Master,
        BGM,
        SFX
    }

    [DisallowMultipleComponent]
    public sealed class SoundSettingsController : MonoBehaviour
    {
        [Serializable]
        public sealed class ChannelView
        {
            public SoundChannel channel;
            public Slider slider;
            public TMP_Text percentText;
            public Button muteButton;
            public TMP_Text muteButtonText;
            public AudioSource[] audioSources = Array.Empty<AudioSource>();

            [NonSerialized] public float volume = 1f;
            [NonSerialized] public float previousVolume = 1f;
            [NonSerialized] public bool muted;
            [NonSerialized] public bool suppressCallback;
        }

        [SerializeField] private ChannelView master = new ChannelView { channel = SoundChannel.Master };
        [SerializeField] private ChannelView bgm = new ChannelView { channel = SoundChannel.BGM };
        [SerializeField] private ChannelView sfx = new ChannelView { channel = SoundChannel.SFX };

        private bool listenersRegistered;
        private bool initialized;
        public event Action<SoundChannel, float> VolumeChanged;

        public float GetEffectiveVolume(SoundChannel channel)
        {
            ChannelView view = channel == SoundChannel.Master ? master : channel == SoundChannel.BGM ? bgm : sfx;
            return view.muted ? 0f : view.volume;
        }

        public float MasterVolume => master.volume;
        public float BgmVolume => bgm.volume;
        public float SfxVolume => sfx.volume;
        public bool MasterMuted => master.muted;
        public bool BgmMuted => bgm.muted;
        public bool SfxMuted => sfx.muted;

        public void Configure(ChannelView masterView, ChannelView bgmView, ChannelView sfxView)
        {
            master = masterView;
            bgm = bgmView;
            sfx = sfxView;
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;
            InitializeChannel(master);
            InitializeChannel(bgm);
            InitializeChannel(sfx);
        }

        private void OnEnable()
        {
            RegisterListeners();
            RefreshChannel(master);
            RefreshChannel(bgm);
            RefreshChannel(sfx);
        }

        private void OnDisable()
        {
            UnregisterListeners();
        }

        public void LoadSavedSettings(float masterVolume, bool masterMute, float bgmVolume, bool bgmMute, float sfxVolume, bool sfxMute)
        {
            SetChannelState(master, masterVolume, masterMute);
            SetChannelState(bgm, bgmVolume, bgmMute);
            SetChannelState(sfx, sfxVolume, sfxMute);
        }

        public void InitializeFromUiDefaults()
        {
            InitializeChannel(master);
            InitializeChannel(bgm);
            InitializeChannel(sfx);
        }

        private void InitializeChannel(ChannelView view)
        {
            if (view.slider != null)
            {
                view.slider.minValue = 0f;
                view.slider.maxValue = 1f;
                view.slider.wholeNumbers = false;
                view.volume = Mathf.Clamp01(view.slider.value);
                view.previousVolume = view.volume > 0f ? view.volume : 1f;
            }

            RefreshChannel(view);
        }

        private void RegisterListeners()
        {
            if (listenersRegistered)
            {
                return;
            }

            master.slider?.onValueChanged.AddListener(OnMasterSliderChanged);
            bgm.slider?.onValueChanged.AddListener(OnBgmSliderChanged);
            sfx.slider?.onValueChanged.AddListener(OnSfxSliderChanged);
            master.muteButton?.onClick.AddListener(ToggleMasterMute);
            bgm.muteButton?.onClick.AddListener(ToggleBgmMute);
            sfx.muteButton?.onClick.AddListener(ToggleSfxMute);
            listenersRegistered = true;
        }

        private void UnregisterListeners()
        {
            if (!listenersRegistered)
            {
                return;
            }

            master.slider?.onValueChanged.RemoveListener(OnMasterSliderChanged);
            bgm.slider?.onValueChanged.RemoveListener(OnBgmSliderChanged);
            sfx.slider?.onValueChanged.RemoveListener(OnSfxSliderChanged);
            master.muteButton?.onClick.RemoveListener(ToggleMasterMute);
            bgm.muteButton?.onClick.RemoveListener(ToggleBgmMute);
            sfx.muteButton?.onClick.RemoveListener(ToggleSfxMute);
            listenersRegistered = false;
        }

        private void OnMasterSliderChanged(float value) => OnSliderChanged(master, value);
        private void OnBgmSliderChanged(float value) => OnSliderChanged(bgm, value);
        private void OnSfxSliderChanged(float value) => OnSliderChanged(sfx, value);
        private void ToggleMasterMute() => ToggleMute(master);
        private void ToggleBgmMute() => ToggleMute(bgm);
        private void ToggleSfxMute() => ToggleMute(sfx);

        // 핵심 분기: view.suppressCallback 판정.
        // 상태 변경: view.muted 갱신.
        // 다음 연결: SettingsMenuUI.SoundSettingsController.RefreshChannel(SettingsMenuUI.SoundSettingsController.ChannelView) 호출.
        private void OnSliderChanged(ChannelView view, float value)
        {
            if (view.suppressCallback)
            {
                return;
            }

            float normalized = Mathf.Clamp01(value);
            if (view.muted && normalized > 0f)
            {
                view.muted = false;
            }

            view.volume = normalized;
            if (normalized > 0f)
            {
                view.previousVolume = normalized;
            }

            RefreshChannel(view);
        }

        private void ToggleMute(ChannelView view)
        {
            if (!view.muted)
            {
                if (view.volume > 0f)
                {
                    view.previousVolume = view.volume;
                }

                view.muted = true;
            }
            else
            {
                view.muted = false;
                view.volume = Mathf.Max(0.01f, view.previousVolume);
            }

            RefreshChannel(view);
        }

        private void SetChannelState(ChannelView view, float volume, bool muted)
        {
            view.volume = Mathf.Clamp01(volume);
            view.previousVolume = view.volume > 0f ? view.volume : 1f;
            view.muted = muted;
            RefreshChannel(view);
        }

        // 핵심 분기: view.slider != null 판정.
        // 상태 변경: view.suppressCallback 갱신.
        // 다음 연결: SettingsMenuUI.SoundSettingsController.ApplyVolume(SettingsMenuUI.SoundSettingsController.ChannelView, float) 호출.
        private void RefreshChannel(ChannelView view)
        {
            float effectiveVolume = view.muted ? 0f : view.volume;
            if (view.slider != null)
            {
                view.suppressCallback = true;
                view.slider.SetValueWithoutNotify(effectiveVolume);
                view.suppressCallback = false;
            }

            if (view.percentText != null)
            {
                view.percentText.text = Mathf.RoundToInt(effectiveVolume * 100f) + "%";
            }

            if (view.muteButtonText != null)
            {
                view.muteButtonText.text = view.muted ? "×" : "♫";
            }

            ApplyVolume(view, effectiveVolume);
        }

        // 핵심 분기: view.channel == SoundChannel.Master 판정.
        // 상태 변경: AudioListener.volume 갱신.
        private void ApplyVolume(ChannelView view, float effectiveVolume)
        {
            VolumeChanged?.Invoke(view.channel, effectiveVolume);
            if (view.channel == SoundChannel.Master)
            {
                AudioListener.volume = effectiveVolume;
            }

            if (view.audioSources == null)
            {
                return;
            }

            for (int i = 0; i < view.audioSources.Length; i++)
            {
                if (view.audioSources[i] != null)
                {
                    view.audioSources[i].volume = effectiveVolume;
                }
            }
        }
    }
}
