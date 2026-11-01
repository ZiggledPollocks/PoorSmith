using System;
using UnityEngine;

[CreateAssetMenu(menuName="Poor Smith/Sound Catalog")]
public sealed class GameSoundCatalog : ScriptableObject
{
    [Serializable] public sealed class Cue
    {
        public string id;
        public AudioClip[] clips;
        [Range(0,1)] public float relativeVolume=.5f;
        [Range(0,100)] public int priority=40;
        [Min(.1f)] public float distanceScreens=1;
        [Tooltip("0=provided clip;1=UI tap;2=decline;3=existing smithy success;4=warning;5=foot contact;6=existing story voice")]
        public int fallback;
    }
    public Cue[] cues;
    [Min(1)] public int maximumVoices=24;
    [Min(.1f)] public float screenDistance=28.444444f;
    [Min(.1f)] public float musicFadeSeconds=1;
    [Range(.05f,.1f)] public float loopStopSeconds=.08f;
    [Range(0,1)] public float menuDuck=.4f, deathDuck=.4f, dialogueDuck=.6f;
    [Min(.1f)] public float footstepDistance=1.1f;
    [Range(0,100)] public float assimilationWarning=80;
}
