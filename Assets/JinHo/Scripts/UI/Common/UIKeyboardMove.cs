using System;
using System.Linq;
using Blacksmith;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A pending operation holds only references/quantity. Cancel never changes an inventory or currency.
public static class UIKeyboardMove
{
    public static InventorySlotView Source {get;private set;}
    public static UIKeyboardSurface Owner {get;private set;}
    public static int Quantity {get;private set;}
    public static bool Active=>Source!=null&&Owner!=null;
    static Stack snapshot;static string snapshotKey;static int available;static bool committing,rejected;
    internal static void Reset(){Source=null;Owner=null;snapshot=null;snapshotKey=null;Quantity=available=0;committing=false;rejected=false;}
    public static bool Begin(UIKeyboardSurface owner,InventorySlotView source)
    {
        Cancel();if(owner==null||source==null||!source.HasDragAction)return false;
        if(!StoryTutorial.CanUseSlot(source)){StoryTutorial.Reject();return false;}
        Owner=owner;Source=source;snapshot=source.Stack;snapshotKey=snapshot.Key;available=snapshot.count;
        Quantity=Mathf.Clamp(source.KeyboardDefaultQuantity,1,Mathf.Min(available,source.KeyboardQuantityLimit));source.ShowKeyboardMoving(true);
        UIKeyboardFooter.For(owner).Show(true,source);return true;
    }
    public static void Validate()
    {
        if(committing||Source==null&&Owner==null)return;
        if(Source==null||Owner==null||!Owner.IsUsable||!Source.HasDragAction||!ReferenceEquals(Source.Stack,snapshot)||snapshot.Key!=snapshotKey||snapshot.count<Quantity)Cancel();
    }
    public static bool SetQuantity(int quantity)
    {
        Validate();if(!Active||quantity<1||quantity>Mathf.Min(snapshot.count,Source.KeyboardQuantityLimit))return false;Quantity=quantity;UIKeyboardFooter.For(Owner).Show(true,Source);return true;
    }
    public static void Preview(ItemDropTarget target,InventorySlotView slot)
    {
        if(!Active)return;ItemDropTarget.UpdateDragVisuals(Source,target!=null&&target.CanPreview(Source)?target:null);
    }
    public static bool Commit(ItemDropTarget destination,InventorySlotView slot)
    {
        Validate();if(!Active||UIKeyboardNavigation.Active!=Owner)return false;
        var source=Source;
        if(destination!=null&&!destination.CanAccept(source)||destination==null&&(slot==null||!slot.CanKeyboardDrop(source)))return false;
        if(!StoryTutorial.CanUseSlot(source)||destination!=null&&!StoryTutorial.CanUseDrop(destination,source)){StoryTutorial.Reject();return false;}
        committing=true;rejected=false;source.KeyboardMoveQuantity=Quantity;bool accepted=false;
        try
        {
            // Both routes invoke the existing drop handlers, whose services revalidate source/count/capacity.
            if(destination!=null)destination.TryDrop(source);else if(!slot.TryKeyboardDrop(source))return false;
            accepted=!rejected;
        }
        finally{if(source!=null)source.KeyboardMoveQuantity=0;committing=false;Cancel();}
        return accepted;
    }
    public static void Reject(){if(committing)rejected=true;}
    public static void CancelIfOwned(UIKeyboardSurface owner){if(Owner==owner)Cancel();}
    public static void Cancel()
    {
        var source=Source;var owner=Owner;Reset();if(source!=null)source.ShowKeyboardMoving(false);ItemDropTarget.ClearDragVisuals();if(owner!=null){var footer=owner.GetComponent<UIKeyboardFooter>();if(footer!=null)footer.Show(false,null);}
    }
    public static void ChooseQuantity()
    {
        if(!Active||UIKeyboardNavigation.Active!=Owner||!Source.KeyboardQuantityEditable)return;
        var view=Owner.owner is BlacksmithController smith?smith.view:Owner.owner is CampaignUI campaign?campaign.view:Source.GetComponentInParent<BlacksmithView>();
        if(view==null)return;
        var source=Source;var owner=Owner;
        OwnedItemControls.Quantity(view,Owner.transform,"수량 지정",()=>source!=null&&source.HasItem?Mathf.Min(source.Stack.count,source.KeyboardQuantityLimit):0,Quantity,
            value=>Owner==owner&&Source==source&&SetQuantity(value));
    }
}
public sealed class UIKeyboardBorder:MonoBehaviour
{
    Image frame;TMP_Text marker;
    public static void Show(RectTransform target,bool visible,string mark="◆")
    {
        if(target==null)return;var component=target.GetComponent<UIKeyboardBorder>();if(!visible&&component==null)return;component??=target.gameObject.AddComponent<UIKeyboardBorder>();component.Set(visible,mark);
    }
    void Set(bool visible,string mark)
    {
        if(frame==null&&visible){var g=new GameObject("KeyboardFocusBorder",typeof(RectTransform),typeof(Image),typeof(LayoutElement));g.transform.SetParent(transform,false);frame=g.GetComponent<Image>();var r=(RectTransform)g.transform;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=new Vector2(2,2);r.offsetMax=new Vector2(-2,-2);g.GetComponent<LayoutElement>().ignoreLayout=true;frame.raycastTarget=false;frame.sprite=GameArtSkin.Current?.selectedSlot??GameArtSkin.Current?.inset;frame.type=Image.Type.Sliced;frame.fillCenter=false;frame.pixelsPerUnitMultiplier=GameArtSkin.Current?.surfacePixelsPerUnit??1;
            marker=RuntimeUIFactory.CreateText("State",g.transform,24,TextAlignmentOptions.TopRight,Color.white,GameTextFonts.Primary);marker.raycastTarget=false;var m=marker.rectTransform;m.anchorMin=m.anchorMax=Vector2.one;m.pivot=Vector2.one;m.sizeDelta=new Vector2(24,24);float pad=Mathf.Min(30,Mathf.Min(((RectTransform)transform).rect.width,((RectTransform)transform).rect.height)*.36f);m.anchoredPosition=new Vector2(-pad,-pad);marker.fontSize=18;marker.enableAutoSizing=false;}
        if(frame==null)return;frame.gameObject.SetActive(visible);if(visible){frame.color=mark=="×"?new Color(.9f,.55f,.45f,.7f):mark=="✓"?new Color(1,.88f,.55f,.7f):new Color(1,.91f,.69f,.6f);marker.text=mark;marker.gameObject.SetActive(mark!="◆");}
    }
}
public sealed class UIKeyboardFooter:MonoBehaviour
{
    UIKeyboardSurface owner;GameObject panel;TMP_Text label;Button quantity;
    public static UIKeyboardFooter For(UIKeyboardSurface owner){var f=owner.GetComponent<UIKeyboardFooter>()??owner.gameObject.AddComponent<UIKeyboardFooter>();f.owner=owner;return f;}
    void Build()
    {
        var template=Resources.Load<GameObject>("UI/Screens/KeyboardFooter");
        if(template!=null){panel=Instantiate(template,transform,false);label=panel.transform.Find("Instruction").GetComponent<TMP_Text>();quantity=panel.transform.Find("Quantity").GetComponent<Button>();}
        else{
            var bg=RuntimeUIFactory.CreateImage("KeyboardFooter",transform);panel=bg.gameObject;bg.sprite=GameArtSkin.Current?.window;bg.type=Image.Type.Sliced;bg.color=new Color(.28f,.20f,.16f,.97f);var r=bg.rectTransform;r.anchorMin=new(.04f,.005f);r.anchorMax=new(.96f,.08f);r.offsetMin=r.offsetMax=Vector2.zero;
            label=RuntimeUIFactory.CreateText("Instruction",r,28,TextAlignmentOptions.MidlineLeft,GameArtSkin.Current?.text??Color.white,GameTextFonts.Primary);label.raycastTarget=false;label.rectTransform.anchorMin=new(.02f,.12f);label.rectTransform.anchorMax=new(.78f,.88f);label.rectTransform.offsetMin=label.rectTransform.offsetMax=Vector2.zero;
            var image=RuntimeUIFactory.CreateImage("Quantity",r);var br=image.rectTransform;br.anchorMin=new(.80f,.1f);br.anchorMax=new(.98f,.9f);br.offsetMin=br.offsetMax=Vector2.zero;quantity=image.gameObject.AddComponent<Button>();quantity.targetGraphic=image;var rule=image.gameObject.AddComponent<UIElementTemplate>();rule.minimumHeight=0;var t=RuntimeUIFactory.CreateText("Label",image.transform,25,TextAlignmentOptions.Center,Color.white,label.font);t.text="수량 지정";RuntimeUIFactory.StretchToParent(t.rectTransform,8);GameArtSkin.Button(quantity);
        }
        GameSoundButton.Attach(quantity);quantity.onClick.AddListener(UIKeyboardMove.ChooseQuantity);
        var canvas=owner.GetComponentsInParent<Canvas>(true).FirstOrDefault(x=>x.isActiveAndEnabled);
        if(canvas!=null)panel.transform.SetParent(canvas.transform,false);
        UIKeyboardNavigation.AttachRoot(owner,panel.transform);
    }
    void OnDisable(){if(panel!=null)panel.SetActive(false);}
    void OnDestroy(){if(panel!=null)Destroy(panel);}
    public void Rejected(){Show(true,null);if(label!=null)label.text="넣을 수 없습니다";}
    public void Show(bool keyboard,InventorySlotView slot,UIKeyboardPanRegion pan=null,bool? validDestination=null)
    {
        if(owner==null||(owner.screenKey?.StartsWith("Title/")==true||owner.screenKey?.StartsWith("Story")==true))return;
        if(panel==null&&keyboard)Build();if(panel==null)return;panel.SetActive(keyboard);if(!keyboard)return;panel.transform.SetAsLastSibling();
        // Workshop scene labels occupy the bottom edge; its editable guide keeps navigation above them.
        ScreenLayoutTemplate.Apply(owner.owner is BlacksmithController smith&&(smith.State==ScreenState.Workshop||smith.State==ScreenState.Playing||smith.State==ScreenState.Animating)?"KeyboardFooterWorkshop":"KeyboardFooterDefault",panel.transform,true);
        bool pending=UIKeyboardMove.Active&&UIKeyboardMove.Owner==owner;quantity.gameObject.SetActive(pending&&UIKeyboardMove.Source.KeyboardQuantityEditable&&UIKeyboardMove.Source.KeyboardQuantityLimit>1&&UIKeyboardMove.Source.Stack.count>1);
        label.text=pan?.OwnsPan==true?"방향키 "+pan.actionLabel+" · Esc 버튼 선택으로":pan!=null?"Enter "+pan.actionLabel+" · 방향키 버튼 선택 · Esc 뒤로":pending?(validDestination==false?$"넣을 수 없습니다 · 이동 수량 {UIKeyboardMove.Quantity} · Esc 취소":validDestination==true?$"이동 수량 {UIKeyboardMove.Quantity} · Enter 이동 · Esc 취소":$"이동 수량 {UIKeyboardMove.Quantity} · 방향키 대상 선택 · Esc 취소"):slot?.HasDragAction==true?"방향키 이동 · Enter 들기 · Esc 뒤로":"방향키 이동 · Enter 선택 · Esc 뒤로";
    }
}
