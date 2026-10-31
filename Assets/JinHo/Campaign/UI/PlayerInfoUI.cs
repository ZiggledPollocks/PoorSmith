// [코드 지도] PlayerInfoUI: 플레이어 정보의 담보 대출·이자·주간 빚을 읽기 전용으로 표시한다.
// 주요 함수: Refresh, Build, Open
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/Campaign/UI/PlayerInfoUI.cs.md

using System.Collections.Generic;
using System.Linq;
using Blacksmith;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Read-only campaign accounts, available from the town and gathering HUDs.</summary>
public sealed class PlayerInfoUI : MonoBehaviour
{
    GameUIController.Activity pauseActivity;
    BlacksmithView view;
    Canvas canvas;
    Button openButton;
    RectTransform modal, content;
    float previousTimeScale;
    bool previousExternalActivity;
    string displayedSnapshot;

    public static void Attach(GameObject player, TMP_FontAsset font, Sprite[] sprites = null)
    {
        if (player == null || player.GetComponent<PlayerInfoUI>() != null) return;
        var info = player.AddComponent<PlayerInfoUI>();
        info.Build(font, sprites);
    }

    // 상태 변경: canvas 갱신.
    // 다음 연결: Blacksmith.BlacksmithView.Button(string, UnityEngine.Transform, string, UnityEngine.Vector2, UnityEngine.Vect… 호출.
    void Build(TMP_FontAsset font, Sprite[] sprites)
    {
        var root = new GameObject("PlayerInfoCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1150;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        view = root.AddComponent<BlacksmithView>();
        view.font = font != null ? font : TMP_Settings.defaultFontAsset;
        view.sprites = sprites ?? System.Array.Empty<Sprite>();
        // Keep the information entry compact beneath the gold/date HUD row.
        openButton = view.Button("PlayerInfoButton", root.transform, "정보",
            new Vector2(.02f, .845f), new Vector2(.095f, .89f), Open);
        openButton.image.color = new Color(.18f, .16f, .13f, .94f);
        var buttonLabel = openButton.GetComponentInChildren<TMP_Text>();
        if (buttonLabel != null)
        {
            RuntimeUIFactory.FitText(buttonLabel, 18f);
            buttonLabel.color = BlacksmithView.Cream;
        }
        modal = view.Full("PlayerInfoModal", root.transform);
        modal.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .83f);
        modal.gameObject.SetActive(false);
    }

    void Update()
    {
        var loop = SmithingLoop.Instance;
        bool ready = loop != null && loop.Initialized && !loop.InShop;
        if (modal != null && modal.gameObject.activeSelf)
        {
            if (GameUIController.CancelPressed) GameUIController.HandleEscape();
            else if (ready) Refresh(false);
        }
        if (openButton != null)
            openButton.gameObject.SetActive(ready && !modal.gameObject.activeSelf &&
                !GameUIController.BlocksGameplayInput &&
                (CampaignController.Instance?.UI == null || !CampaignController.Instance.UI.IsOpen));
    }

    public void Open()
    {
        if (modal.gameObject.activeSelf || GameUIController.BlocksGameplayInput ||
            SmithingLoop.Instance?.Initialized != true || SmithingLoop.Instance.InShop ||
            CampaignController.Instance?.UI?.IsOpen == true) return;
        pauseActivity?.Dispose();pauseActivity=GameUIController.Acquire(this,Close,true,true,500);
        
        
        
        modal.gameObject.SetActive(true);
        var paper = view.Panel("PlayerInfoPaper", modal, new Vector2(.12f, .08f),
            new Vector2(.88f, .91f));
        paper.GetComponent<Image>().color = BlacksmithView.Dark;
        TownFieldUiStyle.Frame(paper.GetComponent<Image>());
        view.Text("PlayerInfoTitle", paper, "플레이어 정보", 36,
            new Vector2(.04f, .89f), new Vector2(.78f, .98f), BlacksmithView.Cream);
        view.Button("PlayerInfoClose", paper, "닫기", new Vector2(.82f, .90f),
            new Vector2(.97f, .98f), Close);
        content = view.Full("PlayerInfoContent", paper);
        displayedSnapshot = null;
        Refresh(true);
    }

    // 핵심 분기: content == null || loop?.Initialized != true 판정.
    // 상태 변경: displayedSnapshot 갱신.
    // 다음 연결: Blacksmith.BlacksmithView.Clear(UnityEngine.Transform) 호출.
    void Refresh(bool force)
    {
        var loop = SmithingLoop.Instance;
        if (content == null || loop?.Initialized != true) return;
        var state = loop.Campaign;
        var loans = new List<PawnLoan>();
        var rules = CampaignController.Instance?.rules ?? GetComponent<CampaignCombat>()?.fieldRules;
        string snapshot = loop.SmithData.day + ":" + state.lastDebtDay + ":" + state.debtCarry +
            ":" + (rules?.weeklyDebt ?? 0) + ":" +
            (state.story?.enabled == true ? $"story:{state.story.startingDay}:{state.story.endingSeen}" : "legacy") + ":" + string.Join("|", state.pawnStock.Select(s => $"{s.itemId}:{s.count}:{s.quality}")) + ":" + string.Join("|", loans.Select(l => l == null ? "null" :
                $"{l.id}:{l.collateral?.itemId}:{l.collateral?.count}:{l.collateral?.quality}:{l.pledgedDay}:{l.principalMilli}:{l.unpaidInterestMilli}:{l.lastInterestDay}"));
        if (!force && snapshot == displayedSnapshot) return;
        displayedSnapshot = snapshot;
        BlacksmithView.Clear(content);
        var economy = new CampaignEconomy(state, loop.SmithData, loop.Catalog, rules);
        int baseWeekly = rules != null ? rules.weeklyDebt : 0;
        int nextDay = economy.NextDebtDay;
        var weekly = view.Panel("WeeklyAccount", content, new Vector2(.04f, .65f),
            new Vector2(.96f, .86f));
        view.Text("WeeklyHeading", weekly, "주간 납부금", 27,
            new Vector2(.03f, .71f), new Vector2(.97f, .96f));
        var weeklyDetail = view.Text("WeeklyDetail", weekly,
            $"현재 Day {loop.SmithData.day} · 이번 청구 Day {nextDay}\n" +
            $"기본 {baseWeekly} G + 기존 이월 {state.debtCarry} G = {((long)baseWeekly + state.debtCarry)} G\n" +
            $"다음 청구 Day {nextDay + 7}: 기본 예정액 {baseWeekly} G (이번 주 이월 시 추가될 수 있음)",
            21, new Vector2(.03f, .04f), new Vector2(.97f, .72f));
        if (state.story?.enabled == true)
        {
            NodeStoryAccount(weekly, weeklyDetail, state.story, loop.SmithData.day);
        }
        TownFieldUiStyle.FrameBehind(weeklyDetail, new Color(.17f,.14f,.11f,.97f));
        view.Text("CollateralHeading", content, "전당포 보관품", 27,
            new Vector2(.05f, .56f), new Vector2(.95f, .63f), BlacksmithView.Cream);
        var list = view.Scroll(content, "CollateralLoans", new Vector2(.04f, .05f),
            new Vector2(.96f, .55f));
        if (loans.Count == 0)
        {
            var empty = view.Panel("NoCollateral", list, Vector2.zero, Vector2.one);
            empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 90;
            view.Text("NoCollateralText", empty, "전당포 대출·이자는 사용하지 않습니다. 보관품은 전당포에서 확인할 수 있습니다.", 23,
                new Vector2(.04f, .06f), new Vector2(.96f, .94f));
        }
        foreach (var loan in loans)
        {
            if (loan?.collateral == null) continue;
            var item = loop.Catalog.Item(loan.collateral.itemId);
            var row = view.Panel("Collateral_" + loan.id, list, Vector2.zero, Vector2.one);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 144;
            view.Text("CollateralDetails", row,
                $"{item?.displayName ?? loan.collateral.itemId} ×{loan.collateral.count}" + (QualityRules.AppliesTo(item) ? " · " + QualityRules.Name(loan.collateral.quality) : "") + $" · 맡긴 날 Day {loan.pledgedDay}\n" +
                $"원금 {CampaignEconomy.FormatMilli(loan.principalMilli)} G · 하루 이자 {CampaignEconomy.FormatMilli(loan.principalMilli / 20)} G\n" +
                $"현재 미납 이자 {CampaignEconomy.FormatMilli(loan.unpaidInterestMilli)} G · 지금 총상환액 {CampaignEconomy.FormatMilli(economy.RepaymentMilli(loan))} G\n" +
                "앞으로의 이자는 위 총상환액에 포함되지 않음",
                19, new Vector2(.03f, .04f), new Vector2(.97f, .96f));
        }
        TownFieldUiStyle.ContainText(modal);
    }

    void NodeStoryAccount(Transform weekly, TMP_Text detail, StoryProgress story, int day)
    {
        var heading = weekly.GetComponentsInChildren<TMP_Text>().First(t => t.name == "WeeklyHeading");
        heading.text = "이야기 · 7일의 약속";
        detail.text = $"현재 Day {day} · 판정 Day {story.startingDay + 7} 아침\n" +
            (story.endingSeen ? "이야기의 빚 판정이 끝났습니다." : $"박덕수에게 갚을 금액 {StoryProgress.DebtGold} G\n전당포 거래와 별도로 관리됩니다.");
    }

    public void Close()
    {
        if (modal == null || !modal.gameObject.activeSelf) return;
        modal.gameObject.SetActive(false);
        pauseActivity?.Dispose();pauseActivity=null;
        
        GetComponent<PlayerInputHandler>()?.ClearGameplayInput();
    }

    void OnDestroy() => Close();
    void OnDisable(){pauseActivity?.Dispose();pauseActivity=null;}

}
