// [코드 지도] TownDialogue: 마을 NPC 대화 내용과 대화 완료 후 UI 진입을 조정한다.
// 주요 함수: Advance, Draw, HandleClose
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/Campaign/Town/TownDialogue.cs.md

using Blacksmith;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Merchant dialogue opens the trade screen after the last line.
public sealed class TownDialogue : MonoBehaviour
{
    CampaignController owner;CampaignUI ui;BlacksmithView view;
    TMP_Text speech;string kind;int line;float clock;bool active,inTrade,traded;
    public bool Active=>active;
    public bool HadTrade=>traded;
    public void Initialize(CampaignController c,CampaignUI u){owner=c;ui=u;view=u.view;}
    public void Begin(string value)
    {kind=value;GetComponent<CampaignTradeUI>()?.ResetShop();line=0;active=true;inTrade=traded=false;Draw();}
    string Name=>kind=="pawnSell"?"전당포 주인":kind=="facility"?"시설 상인":"장비 상인";
    string[] Lines=>kind=="pawnSell"?new[]{"어서 와. 소중한 물건을 맡기고 금화를 빌릴 수 있어.","이자는 다음 날 아침부터 붙어. 원금과 밀린 이자를 갚으면 물건을 돌려줄게.","예전 전당포에 맡긴 물건도 별도 목록에서 찾을 수 있어."}:kind=="facility"?new[]{"시설 상점에 온 걸 환영해. 아직 상품을 준비하고 있어.","상품 준비 중이야. 다음에 다시 들러 줘."}:new[]{"어서 와. 다음 채집을 준비하고 있나?","새 제작법을 발견하면 그 물건도 이곳에서 살 수 있어.","물건을 고르면 설명과 가격을 확인할 수 있어."};
    void Draw()
    {
        var panel=ui.Open(Name,false);panel.GetComponent<Image>().color=BlacksmithView.Cream;
        if(kind=="pawnSell")view.Image("MerchantIcon",panel,"bag_top",Color.white,new(.04f,.39f),new(.26f,.83f),true);
        string text=Lines[line%Lines.Length];
        speech=view.Text("NpcSpeech",panel,text,32,new(kind=="pawnSell"?.30f:.06f,.39f),new(.94f,.8f),BlacksmithView.Ink);speech.maxVisibleCharacters=0;clock=0;
        view.Text("ContinueHint",panel,"Space · 빠르게 보기 / 다음 이야기",18,new(.06f,.3f),new(.93f,.39f),BlacksmithView.Ink);
    }
    public void RecordTrade(){if(active)traded=true;}
    void EndDialogue(){active=false;inTrade=false;speech=null;ui.Close();}
    public bool HandleClose()
    {
        if(!active)return false;
        active=false;inTrade=false;speech=null;return false;
    }
    public void Advance()
    {
        if(!active||inTrade||speech==null)return;
        if(speech.maxVisibleCharacters<speech.text.Length){speech.maxVisibleCharacters=int.MaxValue;return;}
        if(line+1>=Lines.Length)
        {
            if(kind=="facility"){EndDialogue();return;}
            inTrade=true;speech=null;
            SmithingLoop.Instance.ImportCarriedBag();
            if(kind=="pawnSell")ui.ShowStock("pawnPledge");
            else ui.ShowShop(kind);
            return;
        }
        line++;Draw();
    }
    void Update()
    {
        if(!active||inTrade||speech==null||!ui.IsOpen)return;
        clock+=Time.unscaledDeltaTime*30;speech.maxVisibleCharacters=Mathf.Max(speech.maxVisibleCharacters,(int)clock);
        if(Keyboard.current?.spaceKey.wasPressedThisFrame==true)Advance();
    }
}
