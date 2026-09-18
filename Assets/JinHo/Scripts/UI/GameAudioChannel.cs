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
