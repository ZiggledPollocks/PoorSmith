using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Authored art references and presentation helpers; never replaces controls or their events.</summary>
public sealed class GameArtSkin : ScriptableObject
{
    public Sprite panel, window, inset, tooltip, button, hover, pressed, disabled, checkOff, checkOn, input, slider, fill, grabber;
    public Sprite scrollTrack, tab, selectedTab, tabHover;
    public Sprite windCrystal, warpOff;
    [Header("11-1 presentation art")] public Sprite egoHammer, workshopTools;
    Sprite ordinaryWorkshopHammer;
    // crafting_2의 망치 영역만 사용한다. 에고 망치와 일반 제작 도구는 별도 참조다.
    public Sprite WorkshopHammer
    {
        get
        {
            if(ordinaryWorkshopHammer==null && workshopTools!=null)
            {
                var source=workshopTools.rect;
                if(source.width>=142 && source.height>=108)
                    ordinaryWorkshopHammer=Sprite.Create(workshopTools.texture,
                        new Rect(source.x+116,source.y+47,26,58),new Vector2(.5f,.5f),100);
                if(ordinaryWorkshopHammer!=null)ordinaryWorkshopHammer.name="crafting_2_hammer";
            }
            return ordinaryWorkshopHammer;
        }
    }
    [Header("Wood presentation defaults")]
    public Sprite circularFrame, selectedSlot, keycap, nameplate;
    [Min(0)] public float textFramePadding=8;
    [Min(.1f)] public float textFramePixelsPerUnit=1;
    [Min(.1f)] public float surfacePixelsPerUnit=.3333333f;
    public Color panelTint=new(.48f,.54f,.62f,1f);
    [Range(.3f,1f)] public float unfocusedNodeOpacity=.78f;
    [Range(.3f,1f)] public float otherStationNodeOpacity=.85f;
    public Sprite[] warpOn, stations;
    public float[] warpDurations;
    public Color text = new(.95f,.89f,.77f,1f);
    [Header("Wood face colors for readable button ink")]
    public Color buttonInk=new(.06f,.03f,.02f);
    public Color buttonFace=new(196/255f,138/255f,86/255f), hoverFace=new(214/255f,166/255f,111/255f), pressedFace=new(167/255f,111/255f,70/255f);
    public Color disabledFace=new(120/255f,120/255f,130/255f), tabFace=new(92/255f,52/255f,36/255f), tabHoverFace=new(120/255f,71/255f,47/255f), selectedTabFace=new(138/255f,84/255f,54/255f);
    public Color Face(Sprite sprite)=>sprite==tab?tabFace:sprite==tabHover?tabHoverFace:sprite==selectedTab?selectedTabFace:sprite==disabled?disabledFace:sprite==pressed?pressedFace:sprite==hover?hoverFace:buttonFace;
    static GameArtSkin cached;
    public static GameArtSkin Current => cached != null ? cached : cached = Resources.Load<GameArtSkin>("UI/GameArtSkin");
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset() => cached=null;
    static bool Semantic(Transform node)
    {
        for (var t=node; t!=null; t=t.parent)
            if (t.name=="Graph" || t.name.StartsWith("RecipeVertex_",System.StringComparison.Ordinal)) return true;
        return false;
    }
    static void Surface(Image image, Sprite sprite)
    {
        if(image==null||sprite==null||Semantic(image.transform)||UIElementTemplate.Owns(image,"image"))return;
        float alpha=image.color.a;image.sprite=sprite;image.type=Image.Type.Sliced;image.preserveAspect=false;
        // Defaults are applied only to unowned surfaces; per-screen prefab overrides remain authoritative.
        image.pixelsPerUnitMultiplier=Current.surfacePixelsPerUnit;
        var tint=sprite==Current.window||sprite==Current.panel?Current.panelTint:Color.white;
        image.color=new Color(tint.r,tint.g,tint.b,alpha);
    }
    // Decorative icons, invisible world hot spots and recipe vertices keep their custom presentation.
    public static void Button(Button control)
    {
        GameSoundButton.Attach(control);
        if(control!=null)UIKeyboardNavigation.MarkDirty(control.transform);
        var skin=Current;var image=control!=null?control.targetGraphic as Image:null;
        if(control!=null&&image!=null&&image.color.a>.1f&&!image.preserveAspect&&!control.name.StartsWith("RecipeVertex_")&&!control.name.StartsWith("Tool_")&&control.GetComponent<Blacksmith.InventorySlotView>()==null){
            var rule=control.GetComponent<UIElementTemplate>();
            if(rule!=null)UIElementTemplate.EnsureContentHeight(control.transform,rule.minimumHeight);
            else if(control.GetComponentsInChildren<TMP_Text>(true).Any(t=>t.isActiveAndEnabled&&!string.IsNullOrEmpty(t.text)))UIElementTemplate.EnsureContentHeight(control.transform,80);
        }
        if(control!=null&&skin!=null&&image!=null&&UIElementTemplate.Owns(control,"selectable")){
            // Authored sprites and events stay owned by the prefab; opted-in labels still follow state contrast.
            if(control.GetComponentsInChildren<TMP_Text>(true).Any(t=>!UIElementTemplate.Owns(t,"textColor"))){var authoredInk=control.GetComponent<GameUiButtonInk>()??control.gameObject.AddComponent<GameUiButtonInk>();authoredInk.Bind(control,control.GetComponentsInChildren<TMP_Text>(true),skin.text);}return;
        }
        if(control==null||control.name.StartsWith("RecipeVertex_",System.StringComparison.Ordinal)||control.name.StartsWith("Tool_",System.StringComparison.Ordinal)||UIElementTemplate.Owns(control,"selectable"))return;
        if(skin!=null&&image!=null&&(control.name.StartsWith("Slot_",System.StringComparison.Ordinal)||control.name.StartsWith("InventorySlot_",System.StringComparison.Ordinal)||control.name.StartsWith("BasketSlot_",System.StringComparison.Ordinal)||control.GetComponent<Blacksmith.InventorySlotView>()!=null||control.name.StartsWith("Equip_")&&(image.sprite==null||image.sprite.name=="bag_slot"))){Slot(image);foreach(var label in control.GetComponentsInChildren<TMP_Text>(true))Warm(label,skin.text);return;}
        if(skin==null||image==null||image.color.a<.1f||
            (image.sprite!=null&&image.preserveAspect))return;
        bool isTab=control.name.IndexOf("Tab",System.StringComparison.OrdinalIgnoreCase)>=0||
            control.name.StartsWith("Category_",System.StringComparison.Ordinal)||control.name.StartsWith("StationCategory_",System.StringComparison.Ordinal)||control.name.StartsWith("RecipeStation_",System.StringComparison.Ordinal);
        var tint=image.color;Surface(image,isTab&&skin.tab!=null?skin.tab:skin.button);
        if(isTab)image.color=tint;
        control.transition=Selectable.Transition.SpriteSwap;
        control.spriteState=new SpriteState{highlightedSprite=isTab&&skin.tabHover!=null?skin.tabHover:skin.hover,selectedSprite=isTab&&skin.selectedTab!=null?skin.selectedTab:skin.hover,pressedSprite=skin.pressed,disabledSprite=skin.disabled};
        var ink=control.GetComponent<GameUiButtonInk>()??control.gameObject.AddComponent<GameUiButtonInk>();
        ink.Bind(control,control.GetComponentsInChildren<TMP_Text>(true),skin.text);
    }
    static void Warm(TMP_Text label,Color target)
    {
        if(UIElementTemplate.Owns(label,"textColor"))return;
        var c=label.color;if(Mathf.Max(c.r,c.g,c.b)<.36f&&Mathf.Max(c.r,c.g,c.b)-Mathf.Min(c.r,c.g,c.b)<.18f)
            label.color=new Color(target.r,target.g,target.b,c.a);
    }
    public static void Panel(Image image)
    {
        var skin=Current;if(skin==null||image==null||image.color.a<.1f||UIElementTemplate.Owns(image,"image"))return;
        string n=image.name.ToLowerInvariant();
        if(n.Contains("selection")||n.Contains("drag")||n.Contains("shade")||n.Contains("mask")||n.Contains("fade")||n.Contains("transition")||n.Contains("overlay")||n.Contains("icon")||n.Contains("portrait")||n.Contains("mapimage")||n.Contains("stationbackground")||n.Contains("workshopbackground"))return;
        // Keep authored illustrations (paper, bags, character portraits and world maps).
        if(n=="recipebook"||n.Contains("map")||n.Contains("graph"))return;
        if(image.sprite!=null&&!image.sprite.name.StartsWith("T_")&&image.sprite.name!="slot"&&image.sprite.name!="bag_slot"&&image.sprite.name!="paper")return;
        if(n.StartsWith("toolnameplate"))Surface(image,skin.nameplate!=null?skin.nameplate:skin.inset);
        else if(n.Contains("tooltip"))Surface(image,skin.tooltip);
        else if(n.Contains("slot")||n.Contains("inset"))Surface(image,skin.inset);
        else if(n.EndsWith("frame")||n.Contains("panel")||n.Contains("window")||n.Contains("plate")||n.Contains("paper")||n.Contains("bag")||n.Contains("chest")||n.Contains("dialogue")||n.Contains("settings")||n.Contains("auto")||n.Contains("inventory")||n=="body")Surface(image,skin.window);
        if(image.sprite==skin.window){foreach(string cap in new[]{"TopRoll","BottomRoll"}){var child=image.transform.Find(cap);if(child!=null)child.gameObject.SetActive(false);}}
    }
    public static void Text(TMP_Text text)
    {
        if(text==null||Current==null||Semantic(text.transform)||UIElementTemplate.Owns(text,"textColor"))return;
        for(var p=text.transform.parent;p!=null;p=p.parent){var image=p.GetComponent<Image>();if(image==null||image.color.a<.1f)continue;
            if(image.sprite==Current.window||image.sprite==Current.panel||image.sprite==Current.inset||image.sprite==Current.tooltip)Warm(text,Current.text);
            break;}
    }
    public static void Slot(Image image){var skin=Current;if(skin!=null)Surface(image,skin.inset);}
    // Called at screen construction/open, never by a global per-frame hierarchy scan.
    public static void Apply(Transform root)
    {
        if(root==null||Current==null)return;
        foreach(var image in root.GetComponentsInChildren<Image>(true))Panel(image);
        foreach(var control in root.GetComponentsInChildren<Button>(true))Button(control);
        foreach(var toggle in root.GetComponentsInChildren<Toggle>(true)){
            Surface(toggle.targetGraphic as Image,Current.checkOff);Surface(toggle.graphic as Image,Current.checkOn);}
        foreach(var field in root.GetComponentsInChildren<TMP_InputField>(true))Surface(field.targetGraphic as Image,Current.input);
        foreach(var bar in root.GetComponentsInChildren<Scrollbar>(true)){
            var track=bar.GetComponent<Image>();if(track!=bar.targetGraphic)Surface(track,Current.scrollTrack);
            Surface(bar.targetGraphic as Image,Current.grabber);
        }
        foreach(var field in root.GetComponentsInChildren<TMP_Dropdown>(true))Surface(field.targetGraphic as Image,Current.input);
        foreach(var label in root.GetComponentsInChildren<TMP_Text>(true))Text(label);
        foreach(var range in root.GetComponentsInChildren<Slider>(true)){
            Surface(range.GetComponent<Image>(),Current.slider);
            if(range.fillRect!=null)Surface(range.fillRect.GetComponent<Image>(),Current.fill);Surface(range.targetGraphic as Image,Current.grabber);}
    }
}

// PixelUI wood buttons use dark ink; disabled buttons retain readable warm light labels.
public sealed class GameUiButtonInk : MonoBehaviour
{
    Button target; TMP_Text[] labels; Color light,lastColor; Sprite lastSprite; bool wasEnabled;
    public void Bind(Button control,TMP_Text[] text,Color value){target=control;labels=text;light=value;Refresh();}
    void Update(){if(target!=null&&(wasEnabled!=target.IsInteractable()||lastColor!=target.image.color||lastSprite!=target.image.overrideSprite))Refresh();}
    static float L(Color c){float V(float x)=>x<=.04045f?x/12.92f:Mathf.Pow((x+.055f)/1.055f,2.4f);return .2126f*V(c.r)+.7152f*V(c.g)+.0722f*V(c.b);}
    void Refresh(){wasEnabled=target.IsInteractable();lastColor=target.image.color;lastSprite=target.image.overrideSprite;
        var pixel=GameArtSkin.Current.Face(lastSprite!=null?lastSprite:target.image.sprite);var bg=pixel*lastColor;
        var dark=GameArtSkin.Current.buttonInk;float lum=L(bg);float lightRatio=(L(light)+.05f)/(lum+.05f),darkRatio=(lum+.05f)/(L(dark)+.05f);
        foreach(var label in labels){if(label==null||UIElementTemplate.Owns(label,"textColor"))continue;var c=lightRatio>darkRatio?light:dark;c.a=label.color.a;label.color=c;}}
}
