// [코드 지도] GameAudioChannel: 각 AudioSource를 공용 사운드 설정의 BGM/SFX/Master 채널에 연결한다. 원래 음량에 채널 배율을 곱해 개별 소리의 상대 크기를 유지한다. Master는 AudioListener에서 전역 적용하므로 여기서는 중복 곱하지 않는다.
// 주요 함수: Bind, ApplyVolume, OnDisable
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/UI/Audio/GameAudioChannel.cs.md

using SettingsMenuUI;
using UnityEngine;

/// <summary>Put on BGM/SFX AudioSources to connect them to the shared sound settings.</summary>
[RequireComponent(typeof(AudioSource))]
public sealed class GameAudioChannel : MonoBehaviour
{
    [SerializeField] private SoundChannel channel = SoundChannel.SFX;
    private AudioSource source;
    private float baseVolume;
    private SoundSettingsController controller;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        baseVolume = source.volume;
    }

    private void OnEnable() => Bind(GameUIController.Instance != null ? GameUIController.Instance.Sound : null);

    public void Bind(SoundSettingsController settings)
    {
        if (controller != null) controller.VolumeChanged -= ApplyVolume;
        controller = settings;
        if (controller == null) return;
        controller.VolumeChanged += ApplyVolume;
        ApplyVolume(channel, controller.GetEffectiveVolume(channel));
    }

    private void ApplyVolume(SoundChannel changed, float volume)
    {
        // Master is already multiplied globally by AudioListener.
        if (source != null && changed == channel)
            source.volume = baseVolume * (channel == SoundChannel.Master ? 1f : volume);
    }

    private void OnDisable()
    {
        if (controller != null) controller.VolumeChanged -= ApplyVolume;
        controller = null;
        if (source != null) source.volume = baseVolume;
    }
}
