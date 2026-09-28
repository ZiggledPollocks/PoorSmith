using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class CampaignTransition : MonoBehaviour
{
    public bool Busy {get;private set;}
    public float Alpha=>overlay!=null?overlay.color.a:0;
    Image overlay;
    float priorTime;
    bool priorExternal;
    public bool Begin(Action move,Action completed)
    {
        if(Busy)return false;
        StartCoroutine(Run(move,completed));return true;
    }
    IEnumerator Run(Action move,Action completed)
    {
        Busy=true;priorTime=Time.timeScale;priorExternal=GameUIController.ExternalActivity;
        GameUIController.ExternalActivity=true;Time.timeScale=0;
        var input=CampaignController.Instance?.Player?.GetComponent<PlayerInputHandler>();input?.ClearGameplayInput();
        EnsureOverlay();overlay.gameObject.SetActive(true);
        try
        {
            yield return Fade(0,1);
            move();
            // Let Cinemachine apply the destination while the screen remains opaque.
            yield return null;
            yield return Fade(1,0);
        }
        finally{Restore();input?.ClearGameplayInput();}
        completed?.Invoke();
    }
    IEnumerator Fade(float from,float to)
    {
        float elapsed=0;SetAlpha(from);
        while(elapsed<.15f){elapsed+=Time.unscaledDeltaTime;SetAlpha(Mathf.Lerp(from,to,elapsed/.15f));yield return null;}
        SetAlpha(to);
    }
    void SetAlpha(float value){overlay.color=new Color(0,0,0,value);}
    void EnsureOverlay()
    {
        if(overlay!=null)return;
        var go=new GameObject("TownTravelFade",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));go.transform.SetParent(transform,false);
        var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32000;
        var child=new GameObject("FadeOverlay",typeof(RectTransform),typeof(Image));child.transform.SetParent(go.transform,false);
        var rect=(RectTransform)child.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
        overlay=child.GetComponent<Image>();overlay.raycastTarget=true;
    }
    void Restore()
    {
        if(!Busy)return;
        if(overlay!=null){SetAlpha(0);overlay.gameObject.SetActive(false);}
        Time.timeScale=priorTime;GameUIController.ExternalActivity=priorExternal;Busy=false;
    }
    void OnDisable(){StopAllCoroutines();Restore();}
}
