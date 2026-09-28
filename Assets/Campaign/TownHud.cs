using Blacksmith;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class TownHud : MonoBehaviour
{
    CampaignController owner;RectTransform root;TMP_Text gold,day;DayDialGraphic dial;float shown;
    public void Initialize(CampaignController c,CampaignUI ui,Transform canvas)
    {
        owner=c;var v=ui.view;root=v.Full("TownHUD",canvas);
        var coin=v.Panel("GoldPlate",root,new(.02f,.91f),new(.20f,.98f));gold=v.Text("Gold",coin,"",27,Vector2.zero,Vector2.one);
        var dialRect=v.Rect("DayDial",root,new(.86f,.87f),new(.98f,.98f));dial=dialRect.gameObject.AddComponent<DayDialGraphic>();dial.raycastTarget=false;
        day=v.Text("Day",root,"",23,new(.86f,.82f),new(.98f,.88f),null,TextAlignmentOptions.Center);
        v.Button("TownInventory",root,"가방",new(.72f,.92f),new(.81f,.98f),()=>ui.ShowTownInventory());
        v.Button("TownMap",root,"지도",new(.62f,.92f),new(.71f,.98f),()=>ui.ShowMap(false));
    }
    void Update()
    {
        if(owner==null||!owner.Ready)return;root.gameObject.SetActive(owner.InTown&&!SmithingLoop.Instance.InShop&&!GameUIController.BlocksGameplayInput);
        shown=Mathf.MoveTowards(shown,owner.State.gold,Time.unscaledDeltaTime*Mathf.Max(40,Mathf.Abs(owner.State.gold-shown)*6));gold.text=$"● {Mathf.RoundToInt(shown)} G";
        var s=SmithingLoop.Instance.SmithData;day.text=$"Day {s.day} · D-{Mathf.Max(0,owner.State.lastDebtDay+7-s.day)}";
        if(dial.Night!=s.night){dial.Night=s.night;dial.SetVerticesDirty();}
    }
}
