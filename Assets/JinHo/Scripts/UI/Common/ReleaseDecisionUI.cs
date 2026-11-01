using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Only the recovery/settings/failure decisions requested for release stability.</summary>
public sealed class ReleaseDecisionUI : MonoBehaviour
{
    GameObject screen;
    TMP_Text text;
    Button yes, no;
    Action accepted, cancelled;
    GameUIController.Activity activity;
    public bool IsOpen => screen != null && screen.activeSelf;
    public static ReleaseDecisionUI For(GameObject owner)
        => owner.GetComponent<ReleaseDecisionUI>() ?? owner.AddComponent<ReleaseDecisionUI>();
    void Build()
    {
        screen = new GameObject("ReleaseDecision", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        screen.transform.SetParent(transform, false);
        var canvas = screen.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 31000;
        var scaler = screen.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920,1080);
        var shade = RuntimeUIFactory.CreateImage("InputShade", screen.transform); shade.color = new Color(0,0,0,.72f); RuntimeUIFactory.StretchToParent(shade.rectTransform);
        var panel = RuntimeUIFactory.CreateImage("Panel", screen.transform); panel.color = new Color(.13f,.115f,.10f,1);
        RuntimeUIFactory.SetCenteredRect(panel.rectTransform,new Vector2(800,420),Vector2.zero);
        var edge = panel.gameObject.AddComponent<Outline>(); edge.effectColor = new Color(.63f,.52f,.34f,1); edge.effectDistance = new Vector2(2,2);
        text = RuntimeUIFactory.CreateText("DecisionText",panel.transform,28,TextAlignmentOptions.Center,new Color(.95f,.90f,.79f,1),GameTextFonts.Primary);
        RuntimeUIFactory.SetCenteredRect(text.rectTransform,new Vector2(730,260),new Vector2(0,45)); text.overflowMode = TextOverflowModes.Overflow;
        yes = Button(panel.transform,"Confirm",new Vector2(-170,-150),()=>{var action=accepted;Close();action?.Invoke();});
        no = Button(panel.transform,"Cancel",new Vector2(170,-150),()=>{var action=cancelled;Close();action?.Invoke();});
        screen.SetActive(false);
    }
    static Button Button(Transform parent,string name,Vector2 pos,Action action)
    {
        var image=RuntimeUIFactory.CreateImage(name,parent);image.color=new Color(.27f,.23f,.18f,1);
        RuntimeUIFactory.SetCenteredRect(image.rectTransform,new Vector2(300,64),pos);
        var b=image.gameObject.AddComponent<Button>();b.targetGraphic=image;b.onClick.AddListener(()=>action());
        var label=RuntimeUIFactory.CreateText("Label",image.transform,24,TextAlignmentOptions.Center,new Color(.95f,.90f,.79f),GameTextFonts.Primary);
        RuntimeUIFactory.StretchToParent(label.rectTransform,8);return b;
    }
    public void Show(string message,string confirm,string cancel,Action onConfirm,Action onCancel=null,Action onEscape=null,bool allowEscape=true)
    {
        if(!IsOpen)GameSound.UI("Popup",this);
        if(screen==null)Build();
        Close();accepted=onConfirm;cancelled=onCancel;text.text=message;
        yes.GetComponentInChildren<TMP_Text>().text=confirm;no.GetComponentInChildren<TMP_Text>().text=cancel;
        GameArtSkin.Apply(screen.transform);yes.gameObject.SetActive(onConfirm!=null);screen.SetActive(true);
        // Secondary buttons can navigate or request quit; Escape must use its own dismissal policy.
        Action dismiss=allowEscape?()=>{var action=onEscape??cancelled;Close();action?.Invoke();}:null;
        activity=GameUIController.Acquire(this,dismiss,true,true,31000);
        UIKeyboardSurface.Bind(this,screen.transform,"ReleaseDecision",dismiss,31000);
    }
    public void SetMessage(string message){if(text!=null)text.text=message;}
    public void Close(){if(screen!=null)screen.SetActive(false);activity?.Dispose();activity=null;accepted=cancelled=null;}
    void Update(){if(IsOpen&&GameUIController.CancelPressed)GameUIController.HandleEscape();}
    void OnDisable()=>Close();
}
