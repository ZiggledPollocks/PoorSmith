// [코드 지도] FieldHud: 필드의 상태·가방·전투 알림과 지도를 표시하며, 활 선택 중에만 장착 화살 수를 보여준다.
// 주요 함수: Awake, Update, BuildMap
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/Campaign/UI/FieldHud.cs.md

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Shows the shared non-crafting campaign status while gathering.</summary>
public sealed class FieldHud : MonoBehaviour
{
    [SerializeField] TMP_FontAsset font;
    [SerializeField] CaveEntranceBackgroundTransition entrance;
    Canvas canvas;
    TMP_Text status, arrowCountText, region, mapTitle, mapLegend, notice, confirmationText, encounterText, pickupText, toolHint, bagWeightText;
    RectTransform mapModal, mapArea, mapButton, confirmation, bagButton, bagWeightTooltip, encounterRect, toolHintRect;
    Image bagImage;
    InventorySystem inventory;
    PlayerToolController toolController;
    CampaignCombat combat;
    float nextAwarenessCheck, encounterStartedAt;
    float nextHudRefresh;
    float bagHoveredAt;
    bool bagHovered;
    bool enemyNearby;
    float pickupShownAt;
    string lastPickupName;
    int pickupCount;
    float toolHintShownAt=-10f, lastToolHintAt=-10f;
    Vector2 toolHintOrigin;
    readonly Collider2D[] nearbyColliders=new Collider2D[32];
    ContactFilter2D enemyFilter;
    Bounds mapBounds;
    float previousTimeScale;
    bool previousExternalActivity, mapOpen, warpMapOpen;
    Coroutine noticeRoutine;
    CampaignWorldObject selectedWarp;

    // 상태 변경: inventory 갱신.
    // 다음 연결: PlayerInfoUI.Attach(UnityEngine.GameObject, TMPro.TMP_FontAsset, UnityEngine.Sprite[]) 호출.
    void Awake()
    {
        PlayerInfoUI.Attach(gameObject,font);
        inventory=GetComponent<InventorySystem>();
        toolController=GetComponent<PlayerToolController>();
        combat=GetComponent<CampaignCombat>();
        enemyFilter=new ContactFilter2D();
        enemyFilter.SetLayerMask(1<<LayerMask.NameToLayer("Interactable"));
        enemyFilter.useTriggers=false;
        var root = new GameObject("FieldStatusCanvas", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920,1080);
        status = Label("FieldStatus", root.transform, new Vector2(.015f,.91f), new Vector2(.55f,.985f),
            TextAlignmentOptions.Left);
        arrowCountText = Label("EquippedArrowCount", root.transform,
            new Vector2(.015f,.77f), new Vector2(.18f,.84f), TextAlignmentOptions.Left);
        RuntimeUIFactory.FitText(arrowCountText, 24);
        arrowCountText.color = new Color(.98f,.88f,.61f);
        arrowCountText.gameObject.SetActive(false);
        region = Label("FieldRegion", root.transform, new Vector2(.76f,.91f), new Vector2(.985f,.985f),
            TextAlignmentOptions.Right);
        mapButton = ExplorationMapLayout.Rect("FieldMapButton",root.transform,Vector2.zero,Vector2.zero);
        mapButton.anchorMin = mapButton.anchorMax = Vector2.zero;
        mapButton.pivot = Vector2.zero;
        mapButton.anchoredPosition = new Vector2(228f,26f);
        mapButton.sizeDelta = new Vector2(80f,60f);
        var mapButtonImage=mapButton.gameObject.AddComponent<Image>();
        mapButtonImage.color=new Color(.18f,.25f,.23f,.95f);
        mapButton.gameObject.AddComponent<Button>().onClick.AddListener(OpenMap);
        Label("MapButtonText",mapButton,Vector2.zero,Vector2.one,TextAlignmentOptions.Center).text="지도";
        bagButton=ExplorationMapLayout.Rect("FieldBagButton",root.transform,Vector2.zero,Vector2.zero);
        bagButton.anchorMin=bagButton.anchorMax=Vector2.zero;
        bagButton.pivot=Vector2.zero;
        bagButton.anchoredPosition=new Vector2(320f,24f);
        bagButton.sizeDelta=new Vector2(64f,64f);
        bagImage=bagButton.gameObject.AddComponent<Image>();
        var backpackSprites=Resources.LoadAll<Sprite>("SharedUi/backpack_icon");
        bagImage.sprite=backpackSprites.Length>0?backpackSprites[0]:null;
        bagImage.preserveAspect=true;
        bagButton.gameObject.AddComponent<Button>().onClick.AddListener(()=>GameUIController.Instance?.ToggleInventory());
        var bagHover=bagButton.gameObject.AddComponent<EventTrigger>();
        var enter=new EventTrigger.Entry {eventID=EventTriggerType.PointerEnter};
        enter.callback.AddListener(_=>{bagHovered=true;bagHoveredAt=Time.unscaledTime;});
        bagHover.triggers.Add(enter);
        var exit=new EventTrigger.Entry {eventID=EventTriggerType.PointerExit};
        exit.callback.AddListener(_=>{bagHovered=false;bagWeightTooltip.gameObject.SetActive(false);});
        bagHover.triggers.Add(exit);
        bagWeightTooltip=ExplorationMapLayout.Rect("BagWeightTooltip",root.transform,Vector2.zero,Vector2.zero);
        bagWeightTooltip.anchorMin=bagWeightTooltip.anchorMax=Vector2.zero;
        bagWeightTooltip.pivot=Vector2.zero;
        bagWeightTooltip.anchoredPosition=new Vector2(320f,96f);
        bagWeightTooltip.sizeDelta=new Vector2(210f,42f);
        bagWeightTooltip.gameObject.AddComponent<Image>().color=new Color(.10f,.12f,.12f,.94f);
        bagWeightText=Label("BagWeightText",bagWeightTooltip,Vector2.zero,Vector2.one,TextAlignmentOptions.Center);
        RuntimeUIFactory.FitText(bagWeightText, 20);
        bagWeightTooltip.gameObject.SetActive(false);
        encounterRect=ExplorationMapLayout.Rect("MonsterEncounter",root.transform,Vector2.zero,Vector2.zero);
        encounterRect.anchorMin=encounterRect.anchorMax=new Vector2(.5f,.5f);
        encounterRect.sizeDelta=new Vector2(60f,72f);
        encounterText=Label("EncounterMark",encounterRect,Vector2.zero,Vector2.one,TextAlignmentOptions.Center);
        RuntimeUIFactory.FitText(encounterText, 66);
        encounterText.fontStyle=FontStyles.Bold;
        encounterText.outlineWidth=.18f;
        encounterText.outlineColor=Color.black;
        encounterText.color=new Color(1f,.32f,.17f,0f);
        encounterRect.gameObject.SetActive(false);
        pickupText=Label("FieldPickupFeedback",root.transform,new Vector2(.39f,.18f),new Vector2(.61f,.25f),TextAlignmentOptions.Center);
        RuntimeUIFactory.FitText(pickupText, 28);
        pickupText.color=new Color(.98f,.89f,.55f,0f);
        pickupText.gameObject.SetActive(false);
        toolHint=Label("ResourceToolHint",root.transform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),TextAlignmentOptions.Center);
        toolHintRect=toolHint.rectTransform;
        toolHintRect.sizeDelta=new Vector2(440f,62f);
        RuntimeUIFactory.FitText(toolHint, 23);
        toolHint.fontStyle=FontStyles.Bold;
        toolHint.outlineWidth=.2f;
        toolHint.outlineColor=Color.black;
        toolHint.gameObject.SetActive(false);

        mapModal=ExplorationMapLayout.Rect("FieldMapModal",root.transform,Vector2.zero,Vector2.one);
        var shade=mapModal.gameObject.AddComponent<Image>();
        shade.color=new Color(0f,0f,0f,.84f);
        shade.raycastTarget=true;
        var panel=ExplorationMapLayout.Rect("MapPaper",mapModal,new Vector2(.10f,.08f),new Vector2(.90f,.90f));
        panel.gameObject.AddComponent<Image>().color=new Color(.18f,.17f,.13f,.98f);
        mapTitle=Label("MapTitle",panel,new Vector2(.05f,.87f),new Vector2(.55f,.97f),
            TextAlignmentOptions.Left);
        mapTitle.text="채집 필드 지도";
        var back=ExplorationMapLayout.Rect("MapBack",panel,new Vector2(.85f,.88f),new Vector2(.96f,.97f));
        back.gameObject.AddComponent<Image>().color=new Color(.35f,.31f,.25f);
        back.gameObject.AddComponent<Button>().onClick.AddListener(CloseMap);
        Label("BackText",back,Vector2.zero,Vector2.one,TextAlignmentOptions.Center).text="뒤로";
        mapArea=ExplorationMapLayout.Rect("MapArea",panel,new Vector2(.05f,.17f),new Vector2(.95f,.85f));
        mapArea.gameObject.AddComponent<Image>().color=new Color(.10f,.13f,.14f,.98f);
        mapLegend=Label("MapLegend",panel,new Vector2(.05f,.03f),new Vector2(.95f,.13f),
            TextAlignmentOptions.Center);
        mapLegend.text="맵 구조와 워프석 위치";
        confirmation=ExplorationMapLayout.Rect("WarpConfirmation",panel,new Vector2(.24f,.31f),new Vector2(.76f,.69f));
        confirmation.gameObject.AddComponent<Image>().color=new Color(.12f,.14f,.16f,.98f);
        confirmationText=Label("ConfirmationText",confirmation,new Vector2(.08f,.48f),new Vector2(.92f,.90f),TextAlignmentOptions.Center);
        var yes=CreateButton("WarpYes",confirmation,"예",new Vector2(.10f,.11f),new Vector2(.43f,.37f));
        yes.onClick.AddListener(ConfirmWarp);
        var no=CreateButton("WarpNo",confirmation,"아니오",new Vector2(.57f,.11f),new Vector2(.90f,.37f));
        no.onClick.AddListener(()=>confirmation.gameObject.SetActive(false));
        confirmation.gameObject.SetActive(false);
        mapModal.gameObject.SetActive(false);
        notice=Label("FieldNotice",root.transform,new Vector2(.23f,.75f),new Vector2(.77f,.82f),TextAlignmentOptions.Center);
        notice.gameObject.SetActive(false);
    }

    // 핵심 분기: canvas == null || loop == null || !loop.Initialized 판정.
    // 상태 변경: canvas.enabled 갱신.
    // 다음 연결: FieldHud.CloseMap() 호출.
    void Update()
    {
        var loop = SmithingLoop.Instance;
        if (canvas == null || loop == null || !loop.Initialized) return;
        if(mapOpen)
        {
            canvas.enabled=true;
            if(Keyboard.current?.escapeKey.wasPressedThisFrame==true ||
                Gamepad.current?.buttonEast.wasPressedThisFrame==true)
            {
                if(confirmation.gameObject.activeSelf)confirmation.gameObject.SetActive(false);
                else CloseMap();
            }
            return;
        }
        bool hudVisible=!GameUIController.BlocksGameplayInput&&!loop.InShop;
        canvas.enabled=hudVisible;
        if (!hudVisible) return;
        var campaign = loop.Campaign;
        status.text = $"Day {loop.SmithData.day} · {CampaignEconomy.FormatMilli((long)campaign.gold*1000+campaign.goldMilliRemainder)} G";
        bool bowSelected = toolController != null && toolController.CurrentTool?.ToolType == ToolType.Bow;
        arrowCountText.gameObject.SetActive(bowSelected);
        if (bowSelected) arrowCountText.text = $"화살 {combat?.ArrowCount ?? 0}";
        region.text = entrance != null && entrance.IsInsideCave ? "동굴" : "숲";
        if(Time.unscaledTime>=nextHudRefresh)
        {
            nextHudRefresh=Time.unscaledTime+.2f;
            RefreshBag();
        }
        UpdateEncounter();
        if(toolHint.gameObject.activeSelf)
        {
            float elapsed=Time.unscaledTime-toolHintShownAt;
            if(elapsed>=1.1f)toolHint.gameObject.SetActive(false);
            else
            {
                toolHintRect.anchoredPosition=toolHintOrigin+Vector2.up*(elapsed*24f);
                var color=toolHint.color;
                color.a=1f-Mathf.SmoothStep(0f,1f,elapsed/1.1f);
                toolHint.color=color;
            }
        }
        if(pickupText.gameObject.activeSelf)
        {
            float elapsed=Time.unscaledTime-pickupShownAt;
            if(elapsed>=1.2f)pickupText.gameObject.SetActive(false);
            else{var color=pickupText.color;color.a=1f-Mathf.SmoothStep(0f,1f,elapsed/1.2f);pickupText.color=color;}
        }
    }

    public void ShowPickup(string itemName,int amount)
    {
        if(string.IsNullOrEmpty(itemName)||amount<=0||pickupText==null)return;
        pickupCount=lastPickupName==itemName&&Time.unscaledTime-pickupShownAt<.55f?pickupCount+amount:amount;
        lastPickupName=itemName;pickupShownAt=Time.unscaledTime;
        pickupText.text=$"{itemName} +{pickupCount}";
        pickupText.gameObject.SetActive(true);
    }

    public void ShowToolHint(string message,Vector3 worldPosition)
    {
        if(string.IsNullOrEmpty(message)||toolHint==null||Time.unscaledTime-lastToolHintAt<.9f)return;
        var camera=Camera.main;
        if(camera==null)return;
        Vector3 screen=camera.WorldToScreenPoint(worldPosition+Vector3.up*.5f);
        if(screen.z<=0f||!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)canvas.transform,screen,null,out toolHintOrigin))return;
        lastToolHintAt=toolHintShownAt=Time.unscaledTime;
        toolHintRect.anchoredPosition=toolHintOrigin;
        toolHint.text=message;
        toolHint.color=new Color(.98f,.80f,.57f,1f);
        toolHint.gameObject.SetActive(true);
    }

    void RefreshBag()
    {
        float current=inventory!=null?inventory.CurrentWeight:0f;
        float maximum=inventory!=null?inventory.MaxWeight:0f;
        float fullness=maximum>0?Mathf.Clamp01(current/maximum):0f;
        bagImage.color=Color.Lerp(Color.white,new Color(.96f,.28f,.22f,1f),fullness);
        bagWeightText.text=$"가방 무게 {current:0.#} / {maximum:0.#}";
        bagWeightTooltip.gameObject.SetActive(bagHovered&&Time.unscaledTime-bagHoveredAt>=.4f);
    }

    // 핵심 분기: Time.unscaledTime>=nextAwarenessCheck 판정.
    // 상태 변경: nextAwarenessCheck 갱신.
    void UpdateEncounter()
    {
        if(Time.unscaledTime>=nextAwarenessCheck)
        {
            nextAwarenessCheck=Time.unscaledTime+.2f;
            int count=Physics2D.OverlapCircle(transform.position,7f,enemyFilter,nearbyColliders);
            bool found=false;
            for(int i=0;i<count;i++)
            {
                var candidate=nearbyColliders[i];
                if(candidate==null||candidate.GetComponentInParent<PeMonsterController>()!=null)continue;
                var enemy=candidate.GetComponentInParent(typeof(IHealthSource)) as IHealthSource;
                if(enemy==null||enemy.IsDead)continue;
                if(Physics2D.Linecast(transform.position,candidate.bounds.center,1<<LayerMask.NameToLayer("Ground")))continue;
                found=true;break;
            }
            if(found&&!enemyNearby)encounterStartedAt=Time.unscaledTime;
            enemyNearby=found;
        }
        float elapsed=Time.unscaledTime-encounterStartedAt;
        bool visible=encounterStartedAt>0f&&elapsed<1.5f;
        encounterRect.gameObject.SetActive(visible);
        if(!visible)return;
        Vector3 screen=Camera.main!=null?Camera.main.WorldToScreenPoint(transform.position+new Vector3(.9f,1.7f)):Vector3.zero;
        if(RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform,screen,null,out var local))
            encounterRect.anchoredPosition=local;
        var color=encounterText.color;color.a=1f-Mathf.SmoothStep(0f,1f,elapsed/1.5f);encounterText.color=color;
    }

    void OpenMap() => OpenMap(false);

    public void OpenWarpMap(CampaignWorldObject source)
    {
        if(source==null||source.kind!=CampaignObjectKind.Warp)return;
        OpenMap(true);
    }

    public void ShowNotice(string message)
    {
        if(notice==null)return;
        if(noticeRoutine!=null)StopCoroutine(noticeRoutine);
        noticeRoutine=StartCoroutine(NoticeRoutine(message));
    }

    IEnumerator NoticeRoutine(string message)
    {
        notice.text=message;
        notice.gameObject.SetActive(true);
        yield return new WaitForSecondsRealtime(3f);
        notice.gameObject.SetActive(false);
        noticeRoutine=null;
    }

    // 핵심 분기: mapOpen||GameUIController.BlocksGameplayInput||SmithingLoop.Instance?.Initialized!=true|| GetComponent<FieldS… 판정.
    // 상태 변경: previousTimeScale 갱신.
    // 다음 연결: CombatHitFeedback2D.FinishHitStopBeforePause() 호출.
    void OpenMap(bool warp)
    {
        if(mapOpen||GameUIController.BlocksGameplayInput||SmithingLoop.Instance?.Initialized!=true||
            GetComponent<FieldSceneState>()?.Ready!=true)return;
        CombatHitFeedback2D.FinishHitStopBeforePause();
        previousTimeScale=Time.timeScale;
        previousExternalActivity=GameUIController.ExternalActivity;
        Time.timeScale=0f;
        GameUIController.ExternalActivity=true;
        GetComponent<PlayerInputHandler>()?.ClearGameplayInput();
        status.gameObject.SetActive(false);
        region.gameObject.SetActive(false);
        mapButton.gameObject.SetActive(false);
        bagButton.gameObject.SetActive(false);
        encounterRect.gameObject.SetActive(false);
        pickupText.gameObject.SetActive(false);
        toolHint.gameObject.SetActive(false);
        mapModal.gameObject.SetActive(true);
        confirmation.gameObject.SetActive(false);
        warpMapOpen=warp;
        mapTitle.text=warp?"워프석 지도":"채집 필드 지도";
        mapLegend.text=warp?"활성화한 워프석을 선택하세요. 집은 마을로 돌아갑니다.":
            "맵 구조와 워프석 위치";
        mapOpen=true;
        BuildMap();
    }

    // 핵심 분기: exploration!=null 판정.
    // 상태 변경: mapBounds 갱신.
    // 다음 연결: ExplorationMapLayout.DrawTerrain(UnityEngine.RectTransform, CampaignExploration, UnityEngine.Bounds, UnityEng… 호출.
    void BuildMap()
    {
        for(int i=mapArea.childCount-1;i>=0;i--)Destroy(mapArea.GetChild(i).gameObject);
        var exploration=GetComponent<CampaignExploration>();
        var boundary=FindFirstObjectByType<FieldRegionCameraBounds>();
        mapBounds=boundary!=null?boundary.MapBounds:
            new Bounds(new Vector3(25f,-21f,0f),new Vector3(184f,86f,1f));
        var state=SmithingLoop.Instance.Campaign;
        ExplorationMapLayout.DrawTerrain(mapArea,exploration,mapBounds,
            new Color(.61f,.57f,.48f,.90f),true);
        if(exploration!=null)
        {
            foreach(var landmark in FindObjectsByType<CampaignWorldObject>(FindObjectsSortMode.None))
            {
                if(warpMapOpen)
                {
                    if(landmark.kind!=CampaignObjectKind.Warp||state.unlockedWarps==null||
                        !state.unlockedWarps.Contains(landmark.stableId))continue;
                    var marker=ExplorationMapLayout.Marker(mapArea,"Warp_"+landmark.stableId,
                        landmark.transform.position,mapBounds,"워",new Color(.88f,.76f,.44f),font);
                    if(marker==null)continue;
                    marker.GetComponent<Image>().raycastTarget=true;
                    var captured=landmark;
                    marker.gameObject.AddComponent<Button>().onClick.AddListener(()=>SelectWarp(captured));
                    marker.gameObject.AddComponent<FieldWarpMarkerHover>();
                }
                else if(landmark.kind==CampaignObjectKind.Warp)
                    ExplorationMapLayout.Marker(mapArea,"Marker_"+landmark.stableId,
                        landmark.transform.position,mapBounds,"워",
                        new Color(.88f,.76f,.44f),font);
            }
        }
        if(warpMapOpen)
        {
            var home=CreateButton("TownWarp",mapModal,"집",new Vector2(.11f,.11f),new Vector2(.17f,.17f));
            home.onClick.AddListener(()=>SelectWarp(null));
            home.gameObject.AddComponent<FieldWarpMarkerHover>();
        }
    }

    void SelectWarp(CampaignWorldObject destination)
    {
        selectedWarp=destination;
        confirmationText.text=destination==null?"마을로 돌아가시겠습니까?":
            destination.displayName+"으로 이동하시겠습니까?";
        confirmation.gameObject.SetActive(true);
        confirmation.SetAsLastSibling();
    }

    void ConfirmWarp()
    {
        var destination=selectedWarp;
        CloseMap();
        bool started=destination==null?FieldSceneTravel.BeginToTown():
            FieldSceneTravel.BeginFieldWarp((Vector2)destination.transform.position+Vector2.up*1.6f);
        if(!started)ShowNotice("지금은 이동할 수 없습니다. 잠시 후 다시 시도하세요.");
    }

    Button CreateButton(string name,Transform parent,string text,Vector2 min,Vector2 max)
    {
        var rect=ExplorationMapLayout.Rect(name,parent,min,max);
        rect.gameObject.AddComponent<Image>().color=new Color(.52f,.45f,.28f,.98f);
        var button=rect.gameObject.AddComponent<Button>();
        Label(name+"Text",rect,Vector2.zero,Vector2.one,TextAlignmentOptions.Center).text=text;
        return button;
    }

    void CloseMap()
    {
        if(!mapOpen)return;
        mapOpen=false;
        warpMapOpen=false;
        confirmation.gameObject.SetActive(false);
        mapModal.gameObject.SetActive(false);
        status.gameObject.SetActive(true);
        region.gameObject.SetActive(true);
        mapButton.gameObject.SetActive(true);
        bagButton.gameObject.SetActive(true);
        Time.timeScale=previousTimeScale;
        GameUIController.ExternalActivity=previousExternalActivity;
        GetComponent<PlayerInputHandler>()?.ClearGameplayInput();
    }

    void OnDestroy()
    {
        if(!mapOpen)return;
        Time.timeScale=previousTimeScale;
        GameUIController.ExternalActivity=previousExternalActivity;
    }

    TMP_Text Label(string name, Transform parent, Vector2 min, Vector2 max, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var text = go.GetComponent<TextMeshProUGUI>();
        text.font = font ?? TMP_Settings.defaultFontAsset;
        RuntimeUIFactory.FitText(text, 24);
        text.alignment = alignment;
        text.color = new Color(.97f,.91f,.76f);
        text.raycastTarget = false;
        return text;
    }
}
