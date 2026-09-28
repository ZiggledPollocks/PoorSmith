using System;
using Blacksmith;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// One conversation session survives buy/sell tabs so farewell reflects all transactions.
public sealed class TownDialogue : MonoBehaviour
{
    CampaignController owner;CampaignUI ui;BlacksmithView view;
    TMP_Text speech;string kind;int line;float clock;bool active,inTrade,closing,traded,browsed;
    public bool Active=>active;
    public bool Closing=>closing;
    public bool HadTrade=>traded;
    public void Initialize(CampaignController c,CampaignUI u){owner=c;ui=u;view=u.view;}
    public void Begin(string value)
    {kind=value;GetComponent<CampaignTradeUI>()?.ResetShop();line=0;active=true;closing=inTrade=traded=browsed=false;Draw(false);}
    string Name=>kind=="pawnSell"?"전당포 주인":kind=="facility"?"시설 상인":"장비 상인";
    string[] Lines=>kind=="pawnSell"?new[]{"어서 와. 팔 물건이 있어, 아니면 다시 찾으러 왔어?","맡긴 물건은 여기 있어. 필요한 만큼 골라 봐.","판매 가격과 수량은 거래 전에 꼭 확인해."}:kind=="facility"?new[]{"시설 상점에 온 걸 환영해. 아직 상품을 준비하고 있어.","상품 준비 중이야. 다음에 다시 들러 줘."}:new[]{"어서 와. 다음 채집을 준비하고 있나?","곡괭이와 도끼, 배낭을 살펴봐.","물건을 고르면 설명과 가격을 확인할 수 있어."};
    void Draw(bool farewell)
    {
        var panel=ui.Open(Name);panel.GetComponent<Image>().color=BlacksmithView.Cream;
        var badge=view.Image("MerchantIcon",panel,kind=="pawnSell"?"bag_top":"hammer",Color.white,new(.04f,.39f),new(.26f,.83f),true);
        string text=farewell?Farewell:Lines[line%Lines.Length];
        speech=view.Text("NpcSpeech",panel,text,32,new(.30f,.39f),new(.94f,.8f),BlacksmithView.Ink);speech.maxVisibleCharacters=0;clock=0;
        view.Text("ContinueHint",panel,farewell?"Space · 대화 마치기":"Space · 빠르게 보기 / 다음 이야기",18,new(.3f,.3f),new(.93f,.39f),BlacksmithView.Ink);
        if(farewell)return;
        view.Button("BrowseGoods",panel,kind=="pawnSell"?"팔 게 있어":kind=="facility"?"준비 상황 보기":"살 게 있어",new(.05f,.10f),new(.33f,.24f),()=>
        {inTrade=browsed=true;speech=null;if(kind=="pawnSell"){SmithingLoop.Instance.StoreFieldMaterials();ui.ShowStock(kind);}else ui.ShowShop(kind);});
        if(kind=="pawnSell")view.Button("BrowseBuyback",panel,"살 게 있어",new(.36f,.10f),new(.64f,.24f),()=>{inTrade=browsed=true;speech=null;ui.ShowStock("pawnBuy");});
        view.Button("LeaveNpc",panel,"다음에 올게",new(.67f,.10f),new(.95f,.24f),Leave);
    }
    public string Farewell=>kind=="store"?(traded?"다음에 또 오라고.":browsed?"다음번에는 네가 만진 것 전부 사가.":"그래, 필요한 게 있다면\n돈 두둑히 들고 다시 오도록 해."):(traded?"거래 고마워. 다음에도 들러 줘.":"알겠어. 필요한 게 생기면 다시 와.");
    public void RecordTrade(){if(active)traded=true;}
    public void Leave(){if(!active)return;inTrade=false;closing=true;Draw(true);}
    public bool HandleClose()
    {
        if(!active)return false;
        if(closing){active=false;speech=null;return false;}
        Leave();return true;
    }
    public void Advance()
    {
        if(!active||inTrade||speech==null)return;
        if(speech.maxVisibleCharacters<speech.text.Length){speech.maxVisibleCharacters=int.MaxValue;return;}
        if(closing){active=false;speech=null;ui.Close();return;}
        line++;Draw(false);
    }
    void Update()
    {
        if(!active||inTrade||speech==null||!ui.IsOpen)return;
        clock+=Time.unscaledDeltaTime*30;speech.maxVisibleCharacters=Mathf.Max(speech.maxVisibleCharacters,(int)clock);
        if(Keyboard.current?.spaceKey.wasPressedThisFrame==true)Advance();
    }
}
