using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>Extra values persisted by the existing SettingsSaveManager, not a second settings service.</summary>
public static class ReleasePreferences
{
    public static bool LargeText { get; private set; }
    public static bool ReducedFlash { get; private set; }
    public static float ShakeStrength { get; private set; } = 1;
    public static int FrameLimit { get; private set; }
    static int originalVSync=-1;
    static readonly Dictionary<TMP_Text,float> authoredText = new();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        if(originalVSync>=0)QualitySettings.vSyncCount=originalVSync;
        originalVSync=-1;authoredText.Clear();LargeText=ReducedFlash=false;ShakeStrength=1;FrameLimit=0;
    }
    public static void Apply(bool largeText,float shake,bool reducedFlash,int frameLimit)
    {
        LargeText=largeText;ShakeStrength=Mathf.Clamp01(shake);ReducedFlash=reducedFlash;
        FrameLimit=frameLimit is 30 or 60 or 120 or 144 ? frameLimit : 0;
        if(originalVSync<0)originalVSync=QualitySettings.vSyncCount;
        QualitySettings.vSyncCount=FrameLimit==0?originalVSync:0;
        Application.targetFrameRate=FrameLimit==0?-1:FrameLimit;
        foreach(var pair in new List<KeyValuePair<TMP_Text,float>>(authoredText))
            if(pair.Key==null)authoredText.Remove(pair.Key);else Fit(pair.Key,pair.Value);
    }
    public static void Fit(TMP_Text text,float authoredMaximum)
    {
        if(text==null)return;
        authoredText[text]=authoredMaximum;
        if (UIElementTemplate.FitAuthored(text)) return;
        text.fontSize=text.fontSizeMax=authoredMaximum*(LargeText?1.2f:1);
        text.fontSizeMin=Mathf.Min(text.fontSizeMax,Mathf.Max(10,authoredMaximum*.72f));
        text.enableAutoSizing=true; // Fixed panel bounds still win over preferred enlarged text.
    }
    public static float AuthoredSize(TMP_Text text)=>authoredText.TryGetValue(text,out var size)?size:text.fontSize;
}
