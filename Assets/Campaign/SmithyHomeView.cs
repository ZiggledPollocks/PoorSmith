using System;
using Blacksmith;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Interior presentation belongs to the existing point-and-click smithy session.
// Movement is confined to its floor lane; it never moves the paused field player.
public sealed class SmithyHomeView : MonoBehaviour
{
    BlacksmithController owner;BlacksmithView view;RectTransform actor;TMP_Text hint;
    float x=.19f;bool leaving;Action[] actions;readonly float[] places={.42f,.58f,.75f,.9f};
    public float PlayerLanePosition=>x;
    public void Draw(BlacksmithController c)
    {
        owner=c;view=c.view;leaving=false;
        view.Image("InteriorWall",view.stage,"stone_wall",new(.57f,.36f,.20f),Vector2.zero,Vector2.one);
        view.Image("FloorBoards",view.stage,"table_wood",new(.64f,.39f,.21f),Vector2.zero,new(1,.19f));
        foreach(float beam in new[]{.02f,.31f,.64f,.98f})view.Image("TimberBeam",view.stage,"table_wood",new(.27f,.17f,.11f),new(beam,.18f),new(beam+.025f,1));
        var window=view.Panel("WindowFrame",view.stage,new(.21f,.46f),new(.35f,.71f));
        window.GetComponent<Image>().color=new(.24f,.14f,.08f);
        view.Image("WindowSky",window,null,c.Inventory.Data.night?new Color(.10f,.17f,.29f):new Color(.60f,.82f,.95f),new(.07f,.08f),new(.93f,.92f));
        view.Image("WindowCross",window,null,new(.28f,.17f,.10f),new(.47f,.07f),new(.53f,.93f));
        view.Image("WindowCross",window,null,new(.28f,.17f,.10f),new(.06f,.47f),new(.94f,.53f));
        view.Text("RoomName",view.stage,"대장간 · 생활 공간",35,new(.04f,.81f),new(.62f,.92f));
        view.Image("ExitDoor",view.stage,null,new(.16f,.11f,.07f),new(0,.18f),new(.09f,.68f));
        view.Text("ExitSign",view.stage,"← 마을",24,new(.01f,.7f),new(.14f,.8f));
        actions=new Action[]{()=>c.OpenEquipmentRack(),()=>c.SetState(ScreenState.Chest),()=>c.SetState(ScreenState.Workshop),()=>c.SetState(ScreenState.Sleep)};
        string[] names={"장비 거치대","보관함","제작실","침대"};string[] art={"shield","bag_top",null,"bed"};
        for(int i=0;i<places.Length;i++)
        {
            int n=i;float center=places[i];
            if(i==0)view.Image("RackStand",view.stage,"table_wood",new(.44f,.30f,.18f),new(center-.06f,.18f),new(center+.06f,.49f));
            var b=view.Button("Home_"+i,view.stage,i==2?"제작실":"",new(center-.07f,.19f),new(center+.07f,i==3?.36f:.60f),()=>actions[n](),art[i]);
            b.gameObject.AddComponent<UiHoverOutline>();
            view.Text("HomeLabel",view.stage,names[i],22,new(center-.09f,.64f),new(center+.09f,.73f),null,TextAlignmentOptions.Center);
        }
        var sprite=CampaignController.Instance?.Player?.GetComponentInChildren<SpriteRenderer>()?.sprite;
        var im=view.Image("InteriorPlayer",view.stage,null,Color.white,new(x-.035f,.17f),new(x+.035f,.40f),true);im.sprite=sprite;im.raycastTarget=false;actor=im.rectTransform;
        hint=view.Text("InteriorHint",view.stage,"A/D 또는 ←/→ 이동 · F 상호작용 · 오브젝트 클릭",21,new(.12f,.075f),new(.95f,.15f),null,TextAlignmentOptions.Center);
    }
    public void Step(float direction,float seconds)
    {
        if(owner==null||owner.State!=ScreenState.Home||actor==null||leaving)return;
        x=Mathf.Clamp(x+direction*seconds*.35f,.035f,.96f);actor.anchorMin=new(x-.035f,.17f);actor.anchorMax=new(x+.035f,.40f);
        if(x<.06f&&owner.IsHosted&&SmithingLoop.Instance!=null&&!SmithingLoop.Instance.Transitioning)
        {leaving=true;x=.19f;SmithingLoop.Instance.Travel();}
    }
    public void InteractNearest()
    {
        if(owner==null||owner.State!=ScreenState.Home||leaving)return;
        int best=0;for(int i=1;i<places.Length;i++)if(Mathf.Abs(x-places[i])<Mathf.Abs(x-places[best]))best=i;
        if(Mathf.Abs(x-places[best])<.12f)actions[best]();
    }
    void Update()
    {
        if(owner==null||owner.State!=ScreenState.Home||actor==null)return;
        var k=Keyboard.current;if(k==null)return;
        float axis=(k.dKey.isPressed||k.rightArrowKey.isPressed?1:0)-(k.aKey.isPressed||k.leftArrowKey.isPressed?1:0);
        Step(axis,Time.unscaledDeltaTime);if(k.fKey.wasPressedThisFrame)InteractNearest();
    }
}
