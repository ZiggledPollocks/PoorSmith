using System;
using UnityEngine;

/// <summary>Authored Aseprite frame durations; shared by passive wildlife and the intro passer.</summary>
[CreateAssetMenu(menuName = "Game/Timed Sprite Motion Set")]
public sealed class TimedSpriteMotionSet : ScriptableObject
{
    [Serializable]
    public sealed class Motion
    {
        public Sprite[] frames = Array.Empty<Sprite>();
        public float[] seconds = Array.Empty<float>();
        public float Duration { get { float sum=0; foreach(float s in seconds) sum+=Mathf.Max(.001f,s); return sum; } }
        public Sprite At(float time, bool loop = true)
        {
            if(frames==null || frames.Length==0) return null;
            float duration=Duration;
            if(duration<=0 || seconds.Length!=frames.Length) return frames[0];
            time=loop?Mathf.Repeat(Mathf.Max(0,time),duration):Mathf.Clamp(time,0,duration);
            for(int i=0;i<frames.Length;i++){time-=Mathf.Max(.001f,seconds[i]);if(time<0)return frames[i];}
            return frames[frames.Length-1];
        }
    }
    public bool facesRight=true;
    public Motion idle=new(), walk=new(), run=new(), damage=new(), dead=new();
}
