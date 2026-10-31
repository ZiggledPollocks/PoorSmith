using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Authored art references and presentation helpers; never replaces controls or their events.</summary>
public sealed class GameArtSkin : ScriptableObject
{
    public Sprite panel, window, inset, tooltip, button, hover, pressed, disabled, checkOff, checkOn, input, slider, fill, grabber;
    public Sprite windCrystal, warpOff;
    public Sprite[] warpOn, stations;
    public float[] warpDurations;
    public Color text = new(.95f,.89f,.77f,1f);
    static GameArtSkin cached;
    public static GameArtSkin Current => cached != null ? cached : cached = Resources.Load<GameArtSkin>("UI/GameArtSkin");
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset() => cached=null;
    static void Surface(Image image, Sprite sprite)
    {
        if(image==null||sprite==null)return;
        float alpha=image.color.a;image.sprite=sprite;image.type=Image.Type.Sliced;image.preserveAspect=false;
        image.color=new Color(1,1,1,alpha);
    }
    // Decorative icons, invisible world hot spots and recipe vertices keep their custom presentation.
    public static void Button(Button control)
    {
        var skin=Current;var image=control!=null?control.targetGraphic as Image:null;
        if(skin!=null&&image!=null&&control.name.StartsWith("Equip_")&&(image.sprite==null||image.sprite.name=="bag_slot")){Slot(image);foreach(var label in control.GetComponentsInChildren<TMP_Text>(true))Warm(label,skin.text);return;}
        if(skin==null||image==null||image.color.a<.1f||
            (image.sprite!=null&&image.preserveAspect))return;
        Surface(image,skin.button);control.transition=Selectable.Transition.SpriteSwap;
        control.spriteState=new SpriteState{highlightedSprite=skin.hover,selectedSprite=skin.hover,pressedSprite=skin.pressed,disabledSprite=skin.disabled};
        var ink=control.GetComponent<GameUiButtonInk>()??control.gameObject.AddComponent<GameUiButtonInk>();
        ink.Bind(control,control.GetComponentsInChildren<TMP_Text>(true),skin.text);
    }
    static void Warm(TMP_Text label,Color target)
    {
        var c=label.color;if(Mathf.Max(c.r,c.g,c.b)<.36f&&Mathf.Max(c.r,c.g,c.b)-Mathf.Min(c.r,c.g,c.b)<.18f)
            label.color=new Color(target.r,target.g,target.b,c.a);
    }
    public static void Panel(Image image)
    {
        var skin=Current;if(skin==null||image==null||image.color.a<.1f)return;
        string n=image.name.ToLowerInvariant();
        if(n.Contains("shade")||n.Contains("mask")||n.Contains("fade")||n.Contains("transition")||n.Contains("overlay")||n.Contains("icon")||n.Contains("portrait")||n.Contains("mapimage")||n.Contains("stationbackground")||n.Contains("workshopbackground"))return;
        // Keep authored illustrations (paper, bags, character portraits and world maps).
        if(n=="recipebook"||n.Contains("map")||n.Contains("graph"))return;
        if(image.sprite!=null&&!image.sprite.name.StartsWith("T_")&&image.sprite.name!="slot"&&image.sprite.name!="bag_slot"&&image.sprite.name!="paper")return;
        if(n.Contains("tooltip"))Surface(image,skin.tooltip);
        else if(n.Contains("slot")||n.Contains("inset"))Surface(image,skin.inset);
        else if(n.Contains("panel")||n.Contains("window")||n.Contains("plate")||n.Contains("paper")||n.Contains("bag")||n.Contains("chest")||n.Contains("dialogue")||n.Contains("settings")||n.Contains("auto")||n.Contains("inventory")||n=="body")Surface(image,skin.window);
        if(image.sprite==skin.window){foreach(string cap in new[]{"TopRoll","BottomRoll"}){var child=image.transform.Find(cap);if(child!=null)child.gameObject.SetActive(false);}}
    }
    public static void Text(TMP_Text text)
    {
        if(text==null||Current==null)return;
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
        foreach(var bar in root.GetComponentsInChildren<Scrollbar>(true))Surface(bar.targetGraphic as Image,Current.grabber);
        foreach(var field in root.GetComponentsInChildren<TMP_Dropdown>(true))Surface(field.targetGraphic as Image,Current.input);
        foreach(var label in root.GetComponentsInChildren<TMP_Text>(true))Text(label);
        foreach(var range in root.GetComponentsInChildren<Slider>(true)){
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
        var pixel=wasEnabled?new Color(.753f,.533f,.329f):new Color(.36f,.24f,.15f);var bg=pixel*lastColor;
        var dark=new Color(.118f,.071f,.063f);float lum=L(bg);float lightRatio=(L(light)+.05f)/(lum+.05f),darkRatio=(lum+.05f)/(L(dark)+.05f);
        foreach(var label in labels){var c=lightRatio>darkRatio?light:dark;c.a=label.color.a;label.color=c;}}
}
