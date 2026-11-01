using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Full Korean UI text. Fits content without changing values, callbacks or visibility.</summary>
[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public sealed class FullTextLayout : MonoBehaviour, IScrollHandler
{
    TMP_Text label;
    [SerializeField,HideInInspector] float authoredPointSize;
    string measuredText;
    TMP_FontAsset measuredFont;
    float measuredWidth=-1, measuredSize=-1;
    Vector4 measuredMargin;
    Vector2 preferred;
    bool paged, ownsRaycast, originalRaycast;
    int page=1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetHooks()
    {
        SceneManager.sceneLoaded-=SceneReady;
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(TextChanged);
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        SceneManager.sceneLoaded+=SceneReady;
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(TextChanged);
    }
    static void SceneReady(Scene scene,LoadSceneMode mode)
    {
        foreach(var text in UnityEngine.Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include,FindObjectsSortMode.None)) Bind(text);
        foreach(var text in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            if(GameTextFonts.Legacy!=null)text.font=GameTextFonts.Legacy;
            text.horizontalOverflow=HorizontalWrapMode.Wrap;
            text.verticalOverflow=VerticalWrapMode.Overflow;
            text.resizeTextForBestFit=false;
            text.fontStyle=FontStyle.Normal;
        }
    }
    static void TextChanged(UnityEngine.Object target){if(target is TextMeshProUGUI text)Bind(text);}
    public static void Bind(TMP_Text text)
    {
        if(text==null||text is not TextMeshProUGUI)return;
        var fitter=text.GetComponent<FullTextLayout>();
        if(fitter==null)fitter=text.gameObject.AddComponent<FullTextLayout>();
        fitter.label=text;
        NormalizeFont(text);
        if(!global::ReleasePreferences.HasAuthoredSize(text))global::ReleasePreferences.Fit(text,fitter.authoredPointSize>0?fitter.authoredPointSize:text.fontSize);
    }
    // Also covers UI that was already instantiated when the Editor reloaded scripts in Play.
    public static void RefreshLoadedUI(){ResetHooks();Install();SceneReady(default,LoadSceneMode.Additive);}
    public static void RememberSize(TMP_Text text,float size){var fit=text!=null?text.GetComponent<FullTextLayout>():null;if(fit!=null)fit.authoredPointSize=size;}
    static void NormalizeFont(TMP_Text text)
    {
        var primary=GameTextFonts.Primary;
        if(SymbolFont(text)||primary==null)return;
        // 모든 일반 UI는 한글재민4 정규체를 사용한다. 기호 전용 폰트는 보존한다.
        if(text.fontStyle!=FontStyles.Normal)text.fontStyle=FontStyles.Normal;
        if(text.font!=primary){text.font=primary;text.fontSharedMaterial=primary.material;}
        else if(text.fontSharedMaterial!=null&&primary.atlasTexture!=null&&text.fontSharedMaterial.mainTexture!=primary.atlasTexture)text.fontSharedMaterial=primary.material;
    }
    void Awake(){label=GetComponent<TMP_Text>();}
    static bool SymbolFont(TMP_Text text)
    {
        if(text.font==null)return false;
        foreach(char c in text.text??string.Empty)if(char.IsLetterOrDigit(c))return false;
        string name=text.font.name;
        return name.StartsWith("GameMusicSymbol",StringComparison.Ordinal)||name.StartsWith("GameTextSymbols",StringComparison.Ordinal);
    }
    void LateUpdate()
    {
        if(label==null||!label.isActiveAndEnabled||label.canvas==null||!label.canvas.isActiveAndEnabled)return;
        NormalizeFont(label);
        label.enableAutoSizing=false;
        label.textWrappingMode=TextWrappingModes.Normal;
        bool changed=measuredText!=label.text;
        if(changed||paged&&label.GetComponentInParent<UnityEngine.UI.Button>()!=null)ClearPaging();
        var mode=paged?TextOverflowModes.Page:TextOverflowModes.Overflow;
        if(label.overflowMode!=mode)label.overflowMode=mode;
        if(string.IsNullOrEmpty(label.text)||label.GetComponentInParent<TMP_InputField>()!=null)return;
        var rect=label.rectTransform;
        if((label.name=="Speech"||label.name=="MiniSpeech")&&Dialogue(rect))return;
        float width=rect.rect.width;
        if(width<1||rect.rect.height<1)return;
        if(changed||measuredFont!=label.font||!Mathf.Approximately(measuredWidth,width)||!Mathf.Approximately(measuredSize,label.fontSize)||measuredMargin!=label.margin)
        {
            measuredText=label.text;measuredFont=label.font;measuredWidth=width;measuredSize=label.fontSize;measuredMargin=label.margin;
            preferred=label.GetPreferredValues(label.text,width,Mathf.Infinity);
        }
        if(preferred.y<=rect.rect.height+1&&preferred.x<=rect.rect.width+1){if(paged)ClearPaging();return;}
        // Recipe graph geometry and input hit areas are authored by their owners.
        for(var t=rect.parent;t!=null;t=t.parent)if(t.name.StartsWith("RecipeVertex_",StringComparison.Ordinal))return;
        var button=label.GetComponentInParent<UnityEngine.UI.Button>();
        if(button!=null){FitButton((RectTransform)button.transform,rect,preferred);return;}
        var parent=rect.parent as RectTransform;
        if(parent==null)return;
        var group=parent.GetComponent<HorizontalOrVerticalLayoutGroup>();
        if(group!=null&&group.childControlHeight)
        {
            Minimum(rect,preferred.y);
            GrowGroup(parent,group);
            return;
        }
        // A proportional text region gains room by growing its panel, keeping siblings separated.
        float fraction=rect.anchorMax.y-rect.anchorMin.y;
        if(fraction>.05f&&CanGrow(parent))
        {
            Grow(parent,RectTransform.Axis.Vertical,parent.rect.height+(preferred.y-rect.rect.height)/fraction+4);
            if(preferred.y<=rect.rect.height+1)return;
        }
        if(!InsideScrollContent(rect))
        {
            Grow(rect,RectTransform.Axis.Vertical,Mathf.Min(preferred.y+2,FreeHeight(rect)));
            if(preferred.y<=rect.rect.height+1)return;
        }
        // Long non-button bodies can page with the wheel without reparenting a controller's text.
        // Existing dialogue pagination/typewriter and existing ScrollRect content retain ownership.
        if(LongBody(label)&&!InsideScrollContent(rect)&&!Dialogue(rect))
        {
            if(!ownsRaycast){originalRaycast=label.raycastTarget;ownsRaycast=true;}
            paged=true;label.overflowMode=TextOverflowModes.Page;label.pageToDisplay=page;label.raycastTarget=true;
        }
    }
    static void Minimum(RectTransform rect,float height)
    {
        var element=rect.GetComponent<LayoutElement>()??rect.gameObject.AddComponent<LayoutElement>();
        if(element.minHeight<height){element.minHeight=height;element.preferredHeight=Mathf.Max(element.preferredHeight,height);LayoutRebuilder.MarkLayoutForRebuild(rect);}
    }
    static bool InsideScrollContent(Transform t)
    {
        var scroll=t.GetComponentInParent<ScrollRect>();
        return scroll!=null&&scroll.content!=null&&(t==scroll.content||t.IsChildOf(scroll.content));
    }
    static bool CanGrow(RectTransform t)=>t!=null&&t.GetComponent<Canvas>()==null&&t.GetComponent<Mask>()==null&&t.GetComponent<RectMask2D>()==null&&t.GetComponent<GridLayoutGroup>()==null&&!InsideScrollContent(t);
    static void Grow(RectTransform rect,RectTransform.Axis axis,float desired)
    {
        var parent=rect.parent as RectTransform;if(parent==null||parent.GetComponent<GridLayoutGroup>()!=null)return;
        var canvas=rect.GetComponentInParent<Canvas>();if(canvas==null||canvas.renderMode==RenderMode.WorldSpace)return;
        var screen=canvas.rootCanvas.transform as RectTransform;if(screen==null)return;
        float current=axis==RectTransform.Axis.Vertical?rect.rect.height:rect.rect.width;
        float limit=(axis==RectTransform.Axis.Vertical?screen.rect.height:screen.rect.width)*.94f;
        float size=Mathf.Min(desired,Mathf.Max(current,limit));
        if(size<=current+.5f)return;
        rect.SetSizeWithCurrentAnchors(axis,size);
        if(!InsideScrollContent(rect))
        {
            var corners=new Vector3[4];rect.GetWorldCorners(corners);
            Vector2 lo=screen.InverseTransformPoint(corners[0]),hi=screen.InverseTransformPoint(corners[2]);
            float x=lo.x<screen.rect.xMin?screen.rect.xMin-lo.x:hi.x>screen.rect.xMax?screen.rect.xMax-hi.x:0;
            float y=lo.y<screen.rect.yMin?screen.rect.yMin-lo.y:hi.y>screen.rect.yMax?screen.rect.yMax-hi.y:0;
            if(x!=0||y!=0)rect.position+=screen.TransformVector(new Vector3(x,y,0));
        }
    }
    static void GrowGroup(RectTransform parent,HorizontalOrVerticalLayoutGroup group)
    {
        if(!CanGrow(parent))return;
        float height=group.padding.vertical;int count=0;
        foreach(Transform child in parent)
        {
            if(!child.gameObject.activeSelf||child is not RectTransform rect)continue;
            var element=child.GetComponent<LayoutElement>();if(element!=null&&element.ignoreLayout)continue;
            float minimum=LayoutUtility.GetMinHeight(rect);
            height=group is VerticalLayoutGroup?height+minimum:Mathf.Max(height,minimum+group.padding.vertical);count++;
        }
        if(group is VerticalLayoutGroup)height+=Mathf.Max(0,count-1)*group.spacing;
        Grow(parent,RectTransform.Axis.Vertical,height);
    }
    static void FitButton(RectTransform button,RectTransform text,Vector2 needed)
    {
        if(button.parent is not RectTransform parent||parent.GetComponent<GridLayoutGroup>()!=null)return;
        if(needed.x>text.rect.width+1)
        {
            // Use only the existing gap between controls; never grow through a neighboring caption/button.
            float fraction=Mathf.Max(.1f,text.anchorMax.x-text.anchorMin.x);
            float width=button.rect.width+(needed.x-text.rect.width+4)/fraction;
            Grow(button,RectTransform.Axis.Horizontal,Mathf.Min(width,FreeWidth(button)));
            var label=text.GetComponent<TMP_Text>();if(label!=null)needed=label.GetPreferredValues(label.text,Mathf.Max(1,text.rect.width),Mathf.Infinity);
        }
        if(needed.y<=text.rect.height+1)return;
        float need=button.rect.height+needed.y-text.rect.height+4;
        var group=parent.GetComponent<HorizontalOrVerticalLayoutGroup>();
        if(group!=null&&group.childControlHeight){Minimum(button,need);GrowGroup(parent,group);return;}
        Grow(button,RectTransform.Axis.Vertical,need);
        if(text.anchorMax.y-text.anchorMin.y<.05f)Grow(text,RectTransform.Axis.Vertical,needed.y+2);
        if(button.rect.height>parent.rect.height&&CanGrow(parent))Grow(parent,RectTransform.Axis.Vertical,button.rect.height+8);
    }
    static float FreeWidth(RectTransform rect)
    {
        var parent=rect.parent as RectTransform;if(parent==null)return rect.rect.width;
        var corners=new Vector3[4];rect.GetWorldCorners(corners);
        Vector2 lo=parent.InverseTransformPoint(corners[0]),hi=parent.InverseTransformPoint(corners[2]);
        float left=parent.rect.xMin,right=parent.rect.xMax;
        foreach(Transform child in parent)
        {
            if(child==rect||!child.gameObject.activeInHierarchy||child is not RectTransform other)continue;
            if(other.GetComponent<Selectable>()==null&&other.GetComponent<TMP_Text>()==null)continue;
            var points=new Vector3[4];other.GetWorldCorners(points);Vector2 a=parent.InverseTransformPoint(points[0]),b=parent.InverseTransformPoint(points[2]);
            if(b.y<=lo.y||a.y>=hi.y)continue;
            if(b.x<=lo.x)left=Mathf.Max(left,b.x+8);else if(a.x>=hi.x)right=Mathf.Min(right,a.x-8);
        }
        float scale=rect.rect.width/Mathf.Max(.01f,hi.x-lo.x);
        float extra=Mathf.Min(rect.pivot.x>.001f?Mathf.Max(0,lo.x-left)*scale/rect.pivot.x:float.PositiveInfinity,
            rect.pivot.x<.999f?Mathf.Max(0,right-hi.x)*scale/(1-rect.pivot.x):float.PositiveInfinity);
        return rect.rect.width+(float.IsInfinity(extra)?0:extra);
    }
    static float FreeHeight(RectTransform rect)
    {
        var parent=rect.parent as RectTransform;if(parent==null)return rect.rect.height;
        var corners=new Vector3[4];rect.GetWorldCorners(corners);
        Vector2 lo=parent.InverseTransformPoint(corners[0]),hi=parent.InverseTransformPoint(corners[2]);
        float scale=rect.rect.height/Mathf.Max(.01f,hi.y-lo.y);
        float below=Mathf.Max(0,lo.y-parent.rect.yMin)*scale,above=Mathf.Max(0,parent.rect.yMax-hi.y)*scale;
        float extra=Mathf.Min(rect.pivot.y>.001f?below/rect.pivot.y:float.PositiveInfinity,
            rect.pivot.y<.999f?above/(1-rect.pivot.y):float.PositiveInfinity);
        return rect.rect.height+(float.IsInfinity(extra)?0:extra);
    }
    static bool LongBody(TMP_Text text)
    {
        string name=text.name.ToLowerInvariant();
        return (text.text.Length>=120||text.text.Split('\n').Length>=5)&&
            (name.Contains("info")||name.Contains("body")||name.Contains("description")||name.Contains("message")||name.Contains("record")||name.Contains("detail"));
    }
    static bool Dialogue(Transform rect)
    {
        for(var t=rect;t!=null;t=t.parent){string name=t.name.ToLowerInvariant();if(name.Contains("dialogue")||name.Contains("tutorial")||name.Contains("story"))return true;}
        return false;
    }
    void ClearPaging(){paged=false;page=1;if(label!=null){label.pageToDisplay=1;if(ownsRaycast)label.raycastTarget=originalRaycast;}ownsRaycast=false;}
    void OnDisable(){if(ownsRaycast)ClearPaging();}
    public void OnScroll(PointerEventData data)
    {
        if(!paged||label==null||Mathf.Approximately(data.scrollDelta.y,0))return;
        page=Mathf.Clamp(page-(int)Mathf.Sign(data.scrollDelta.y),1,Mathf.Max(1,label.textInfo.pageCount));
        label.pageToDisplay=page;data.Use();
    }
}
