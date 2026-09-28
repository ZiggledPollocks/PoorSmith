using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Blacksmith
{
    public class BlacksmithController : MonoBehaviour
    {
        public BlacksmithCatalog catalog;
        public BlacksmithView view;
        public bool loadSavedGame=true;
        // Optional host session: the standalone scene retains its own save behavior.
        public static Func<BlacksmithCatalog,SaveData> SessionLoader;
        public static BlacksmithCatalog SessionCatalog;
        public static Action<SaveData> SessionWriter;
        public bool IsHosted => SessionLoader!=null;
        public bool CanLeave => Inventory!=null&&!Crafting.Active&&State!=ScreenState.Animating&&State!=ScreenState.Result;
        public void PrepareToLeave(){if(CanLeave){Inventory.ReturnAll();Persist();}}
        public InventoryService Inventory {get;private set;}
        public CraftingService Crafting {get;private set;}
        public ScreenState State {get;private set;}
        public Station CurrentStation {get;private set;}
        public ToolKind CurrentTool {get;private set;}
        ScreenState returnState;
        RectTransform inventoryGrid, selectedGrid, autoList, playingRoot;
        string query="",feedback="";int group=-1,material=-1,bagPage;bool equipmentMode,discardMode;
        HashSet<string> discard=new HashSet<string>();
        Stack held;bool heldBag;
        CraftResult result;
        TMP_Text counter;
        Image guide,progressImage,workIcon;
        CraftGestureInput gesture;
        float timer,quenchCenter;bool quenchStarted;
        readonly string[] stationNames={"작업대","도구 걸이","모루","담금질","용광로"};
        readonly string[] toolNames={"칼","대패","톱","숫돌","망치"};
        readonly string[] toolArt={"knife","plane","saw","whetstone","hammer"};
        readonly string[] stageArt={"leather_mat","table_wood","anvil","water_bucket","furnace_arch"};
        void Start()
        {
            if(SessionCatalog!=null)catalog=SessionCatalog;
            var data=SessionLoader!=null?SessionLoader(catalog):loadSavedGame?BlacksmithSave.Load(catalog):BlacksmithSave.CreateDemo(catalog);
            Inventory=new InventoryService(data,catalog);Crafting=new CraftingService(Inventory);
            view.Build();State=ScreenState.Home;Render();
            if(!IsHosted&&BlacksmithSave.WritesBlocked)Say("저장 파일을 읽지 못했습니다. 원본 보호를 위해 저장을 중단했습니다.");
        }
        public void SetState(ScreenState state){
            if(state==ScreenState.Home&&SmithingLoop.Instance?.InteriorPanel==true){State=ScreenState.Home;SmithingLoop.Instance.Travel();return;}
            if(IsHosted&&CampaignController.Instance!=null&&!Inventory.Data.night&&(state==ScreenState.Station||state==ScreenState.Playing))
            {Say("낮에는 채집 시간입니다. 침대에서 낮잠을 자면 제작할 수 있습니다.");return;}
            State=state;query="";group=material=-1;view.HideTooltip();Render();}
        void Say(string message){feedback=message;view.notice.text=message;}
        void Render()
        {
            BlacksmithView.Clear(view.stage);BlacksmithView.Clear(view.overlay);view.HideTooltip();
            view.stage.anchoredPosition=Vector2.zero;
            view.status.text=$"대장간   /   {(State==ScreenState.Home?"전체 보기":State==ScreenState.Chest?(equipmentMode?"장비 거치대":"상자"):State==ScreenState.Sleep?"침대":State==ScreenState.Workshop?"제작실":stationNames[(int)CurrentStation])}     {(State==ScreenState.Home||State==ScreenState.Chest||State==ScreenState.Sleep?$"{Inventory.Data.day}일 · {(Inventory.Data.night?"밤":"아침")}":"")}     체력 {Inventory.Data.hp}/{Inventory.Data.maxHp}     연료 {Inventory.Data.fuel}/50";
            view.notice.text=feedback;
            // Rebuild only transient HUD controls; status, feedback and tooltip stay stable.
            var old=view.hud.Find("Controls");if(old){old.gameObject.SetActive(false);Destroy(old.gameObject);}var controls=view.Full("Controls",view.hud);
            var back=view.Button("Back",controls,"← 대장간",new Vector2(.015f,.938f),new Vector2(.145f,.989f),Back);
            back.interactable=State!=ScreenState.Animating&&State!=ScreenState.Result;
            var recipes=view.Button("Recipes",controls,"레시피  [Tab]",new Vector2(.82f,.938f),new Vector2(.985f,.989f),ToggleRecipes);
            recipes.interactable=State!=ScreenState.Animating&&State!=ScreenState.Result;
            if(State==ScreenState.Home){DrawHome();return;}
            if(State==ScreenState.Workshop){DrawWorkshop();return;}
            if(State==ScreenState.Chest||State==ScreenState.Sleep)
                view.Image("InteriorPanelBackdrop",view.stage,"stone_wall",new Color(.3f,.25f,.2f),Vector2.zero,Vector2.one);
            else DrawStation();
            if(State==ScreenState.Station)DrawNavigation();
            if(State==ScreenState.Selecting||State==ScreenState.Fuel)DrawSelection();
            else if(State==ScreenState.Playing)DrawMinigame();
            else if(State==ScreenState.Result)DrawResult();
            else if(State==ScreenState.Chest)DrawChest();
            else if(State==ScreenState.Recipes||State==ScreenState.Codex)DrawRecipes();
            else if(State==ScreenState.Sleep)DrawSleep();
        }
        void DrawHome()
        {
            var home=GetComponent<SmithyHomeView>()??gameObject.AddComponent<SmithyHomeView>();home.Draw(this);
            var dial=view.Rect("SmithyDayDial",view.stage,new Vector2(.81f,.77f),new Vector2(.98f,.94f)).gameObject.AddComponent<DayDialGraphic>();dial.Night=Inventory.Data.night;dial.raycastTarget=false;
            view.Text("SmithyDay",view.stage,"Day "+Inventory.Data.day+(CampaignController.Instance!=null?$" · {CampaignController.Instance.State.gold} G":""),22,new Vector2(.80f,.71f),new Vector2(.98f,.78f),null,TextAlignmentOptions.Center);
        }
        public void OpenEquipmentRack(){equipmentMode=true;SetState(ScreenState.Chest);}
        void DrawWorkshop()
        {
            view.Image("StoneWall",view.stage,"stone_wall",new Color(.75f,.73f,.67f),Vector2.zero,Vector2.one);
            view.Image("Floor",view.stage,"table_wood",new Color(.55f,.45f,.35f),Vector2.zero,new Vector2(1,.35f));
            view.Text("Heading",view.stage,"나의 대장간",48,new Vector2(.06f,.75f),new Vector2(.8f,.87f));
            view.Text("Subheading",view.stage,"설비를 선택해 재료를 가공하고 장비를 완성하세요.",23,new Vector2(.06f,.68f),new Vector2(.85f,.75f));
            string[] names={"상자","작업대","도구 걸이","모루","담금질","용광로","간이 침대"};
            string[] art={"bag_top","leather_mat","hammer","anvil","water_bucket","furnace_arch","bed"};
            Vector2[] mins={new Vector2(.025f,.18f),new Vector2(.16f,.22f),new Vector2(.31f,.46f),new Vector2(.43f,.16f),new Vector2(.64f,.14f),new Vector2(.70f,.32f),new Vector2(.86f,.13f)};
            Vector2[] maxs={new Vector2(.17f,.40f),new Vector2(.42f,.46f),new Vector2(.42f,.70f),new Vector2(.65f,.46f),new Vector2(.76f,.40f),new Vector2(.98f,.72f),new Vector2(.98f,.30f)};
            for(int i=0;i<7;i++)
            {
                int n=i;
                view.Button("Facility_"+i,view.stage,"",mins[i],maxs[i],()=>{if(n==0)SetState(ScreenState.Chest);else if(n==6)SetState(ScreenState.Sleep);else{CurrentStation=(Station)(n-1);SetState(ScreenState.Station);}},art[i]);
                view.stage.Find("Facility_"+i).gameObject.AddComponent<UiHoverOutline>();
                view.Text("FacilityName",view.stage,names[i],22,new Vector2(mins[i].x,mins[i].y-.065f),new Vector2(maxs[i].x,mins[i].y),null,TextAlignmentOptions.Center);
            }
            view.Text("DemoData",view.stage,catalog.containsTestData?"Notion 레시피 적용 · 장비 수치/숙련도는 테스트 설정":"",16,new Vector2(.50f,.885f),new Vector2(.98f,.925f),null,TextAlignmentOptions.Right);
            if(!IsHosted)view.Button("SupplyRecipeMaterials",view.stage,"테스트 재료 받기",new Vector2(.06f,.58f),new Vector2(.25f,.65f),()=>{BlacksmithSave.SupplyRecipeMaterials(Inventory.Data,catalog);Persist();Say("레시피에 필요한 채집 재료를 보관함에 30개까지 보충했습니다.");Render();});
        }
        void DrawStation()
        {
            var wall=view.Image("StoneWall",view.stage,"stone_wall",new Color(.65f,.63f,.59f),Vector2.zero,Vector2.one);
            if(CurrentStation==Station.Furnace&&State==ScreenState.Station){wall.raycastTarget=true;wall.gameObject.AddComponent<StationPanInput>().target=view.stage;}
            if(CurrentStation==Station.Workbench||CurrentStation==Station.Tools)
                view.Image("Table",view.stage,"table_wood",Color.white,Vector2.zero,new Vector2(1,.58f));
            var objectButton=view.Button("StationObject",view.stage,"",new Vector2(.19f,.12f),new Vector2(.81f,.80f),()=>SetState(ScreenState.Selecting),stageArt[(int)CurrentStation]);
            objectButton.interactable=State==ScreenState.Station;
            var objectColors=objectButton.colors;objectColors.disabledColor=Color.white;objectButton.colors=objectColors;
            if(CurrentStation==Station.Workbench)
            {
                var book=view.Button("CodexBook",view.stage,"",new Vector2(.04f,.64f),new Vector2(.22f,.86f),()=>{returnState=State;SetState(ScreenState.Codex);},"book");book.interactable=State==ScreenState.Station;
                view.Text("CodexLabel",view.stage,"도감",20,new Vector2(.06f,.61f),new Vector2(.22f,.66f));
            }
            if(CurrentStation==Station.Tools)
            {
                for(int i=0;i<5;i++){if(i==(int)CurrentTool&&State!=ScreenState.Station)continue;int n=i;float x=.28f+i*.105f;var b=view.Button("Tool_"+i,view.stage,"",new Vector2(x,.59f),new Vector2(x+.09f,.87f),()=>{CurrentTool=(ToolKind)n;Say(toolNames[n]+" 선택 · 작업대를 눌러 재료를 고르세요.");Render();},toolArt[i]);b.interactable=State==ScreenState.Station;
                    b.image.color=CurrentTool==(ToolKind)i?new Color(1,.75f,.3f):Color.white;
                    view.Text("ToolName",view.stage,toolNames[i],19,new Vector2(x,.55f),new Vector2(x+.09f,.60f),null,TextAlignmentOptions.Center);}
            }
            if(CurrentStation==Station.Furnace)
            {
                var fuel=view.Button("FuelGrate",view.stage,"",new Vector2(.34f,.07f),new Vector2(.66f,.32f),()=>SetState(ScreenState.Fuel),"fuel_grate");fuel.interactable=State==ScreenState.Station;
                var bucket=view.Button("Crucible",view.stage,"",new Vector2(.43f,.35f),new Vector2(.57f,.62f),()=>SetState(ScreenState.Selecting),"bucket");bucket.interactable=State==ScreenState.Station;
                view.Text("FuelValue",view.stage,$"연료 {Inventory.Data.fuel} / 50",24,new Vector2(.36f,.09f),new Vector2(.65f,.15f),null,TextAlignmentOptions.Center);
            }
            if(State==ScreenState.Station)view.Text("Hint",view.stage,"설비를 클릭하여 재료 선택",24,new Vector2(.26f,.81f),new Vector2(.75f,.89f),null,TextAlignmentOptions.Center);
        }
        void DrawNavigation()
        {
            view.Button("PreviousStation",view.overlay,"‹",new Vector2(.015f,.40f),new Vector2(.07f,.62f),()=>{CurrentStation=(Station)(((int)CurrentStation+4)%5);Render();});
            view.Button("NextStation",view.overlay,"›",new Vector2(.93f,.40f),new Vector2(.985f,.62f),()=>{CurrentStation=(Station)(((int)CurrentStation+1)%5);Render();});
            view.Button("ScrollRecipe",view.overlay,"",new Vector2(.83f,.73f),new Vector2(.94f,.87f),ToggleRecipes,"scroll");
        }
        void DrawSelection()
        {
            var panel=view.Panel("InventoryPanel",view.overlay,new Vector2(.675f,.21f),new Vector2(.985f,.86f));
            view.Text("Heading",panel,State==ScreenState.Fuel?"연료 넣기":"보관함",26,new Vector2(.03f,.88f),new Vector2(.60f,.99f));
            DrawFilters(panel,()=>RefreshInventory(false));
            inventoryGrid=view.Scroll(panel,"InventoryScroll",new Vector2(.025f,.03f),new Vector2(.99f,.58f),4,103);
            string[] materialNames={"전체","목재","석재","철","무기","방어구","기타"};
            for(int i=0;i<7;i++){int n=i;var filter=view.Button("MaterialFilter"+i,panel,materialNames[i],new Vector2(.015f+i*.14f,.59f),new Vector2(.155f+i*.14f,.67f),()=>{material=n-1;RefreshInventory(false);});filter.GetComponentInChildren<TMP_Text>().fontSize=14;}
            RefreshInventory(false);
            view.Button("CloseSelection",view.overlay,"닫기 · 재료 반환",new Vector2(.675f,.12f),new Vector2(.825f,.19f),()=>{Inventory.ReturnAll();Persist();SetState(ScreenState.Station);});
            if(State==ScreenState.Fuel){view.Text("FuelRules",view.overlay,"목재 +3   석탄 +6   목재 부산물 +1\n가득 차면 추가할 수 없습니다.",24,new Vector2(.05f,.20f),new Vector2(.60f,.35f));return;}
            view.Button("Reset",view.overlay,"초기화",new Vector2(.83f,.12f),new Vector2(.985f,.19f),()=>{Inventory.ReturnAll();Render();});
            var work=view.Panel("SelectedMaterials",view.overlay,new Vector2(.325f,.25f),new Vector2(.655f,.49f));
            view.Text("SelectionLabel",work,"선택한 재료 · 클릭하면 반환",21,new Vector2(.02f,.73f),new Vector2(.98f,.98f));
            selectedGrid=view.Rect("Slots",work,new Vector2(.04f,.05f),new Vector2(.97f,.73f));var grid=selectedGrid.gameObject.AddComponent<GridLayoutGroup>();grid.cellSize=new Vector2(108,108);grid.spacing=new Vector2(8,8);
            foreach(var s in Inventory.Selection.ToArray())CreateSlot(selectedGrid,s,()=>{Inventory.Transfer(Inventory.Selection,Inventory.Data.chest,s,1);Render();});
            view.Button("StartCraft",view.overlay,"시작하기",new Vector2(.375f,.14f),new Vector2(.605f,.23f),()=>StartCraft(null));
            var paper=view.Panel("AutomaticRecipes",view.overlay,new Vector2(.03f,.20f),new Vector2(.30f,.84f),true);
            view.Text("AutoTitle",paper,"숙련 제작",29,new Vector2(.04f,.86f),new Vector2(.96f,.98f),BlacksmithView.Ink);
            autoList=view.Scroll(paper,"Recipes",new Vector2(.02f,.035f),new Vector2(.98f,.86f));
            var unlocked=catalog.recipes.Where(r=>r.enabled&&r.station==CurrentStation&&(r.station!=Station.Tools||r.tool==CurrentTool)&&Crafting.Mastery(r)>=2).ToArray();
            if(unlocked.Length==0)view.Text("Locked",paper,"동일 레시피를 익히면\n숙련도 2에서 자동 제작 해금\n\n일반 제작 6회 / 장비 제작 4회",21,new Vector2(.06f,.30f),new Vector2(.94f,.74f),BlacksmithView.Ink);
            foreach(var recipe in unlocked)AddAutomatic(recipe);
        }
        void DrawFilters(Transform panel,Action refresh)
        {
            string[] names={"채집물","제작물","무기·방어구"};
            var buttons=new List<Button>();
            for(int i=0;i<3;i++){int n=i;var b=view.Button("Group"+i,panel,names[i],new Vector2(.02f+i*.32f,.77f),new Vector2(.33f+i*.32f,.88f),()=>{group=group==n?-1:n;for(int k=0;k<buttons.Count;k++)buttons[k].image.color=group==k?BlacksmithView.Gold:new Color(.22f,.21f,.18f);refresh();});b.GetComponentInChildren<TMP_Text>().fontSize=17;b.image.color=group==i?BlacksmithView.Gold:new Color(.22f,.21f,.18f);buttons.Add(b);}
            view.Search(panel,value=>{query=value;refresh();},new Vector2(.025f,.675f),new Vector2(.98f,.765f)).SetTextWithoutNotify(query);
        }
        IEnumerable<Stack> Filter(IEnumerable<Stack> list)
        {return list.Where(s=>{var d=catalog.Item(s.itemId);return (group<0||(int)d.group==group)&&(material<0||(int)d.material==material)&&(string.IsNullOrEmpty(query)||d.displayName.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0)&&(State!=ScreenState.Fuel||d.fuelValue>0);});}
        void RefreshInventory(bool chest)
        {
            if(!inventoryGrid)return;BlacksmithView.Clear(inventoryGrid);view.HideTooltip();
            foreach(var s in Filter(Inventory.Data.chest).ToArray())
            {
                if(chest){ChestSlot(inventoryGrid,s,false);continue;}
                CreateSlot(inventoryGrid,s,()=>
                {
                    if(State==ScreenState.Fuel){if(!Inventory.AddFuel(s))Say("연료가 가득 찼습니다.");Persist();Render();}
                    else{if(!Inventory.Select(s,CurrentStation))Say("이 설비에서 선택 가능한 재료 종류를 초과했습니다.");Render();}
                });
            }
            if(chest)AddEmptyTransfer(inventoryGrid,false);
        }
        InventorySlotView CreateSlot(Transform parent,Stack stack,Action click,Action right=null,Action<InventorySlotView> drop=null,bool bag=false)
        {
            var item=catalog.Item(stack.itemId);
            return view.Slot(parent,stack,item,click,right,drop,enter=>{if(enter)view.ShowTooltip(ItemInfo(stack));else view.HideTooltip();},bag);
        }
        string ItemInfo(Stack s)
        {
            var d=catalog.Item(s.itemId);float m=QualityRules.Multiplier(s.quality);int price=Mathf.RoundToInt(d.price*m*(s.quality==Quality.Master?2:1));
            string info=$"{d.displayName}   ×{s.count}\n판매 가격: {price}G";
            if(d.toolTier>0)info+=$"\n도구 티어 {d.toolTier}";
            if(d.group==ItemGroup.Equipment)info+=$"   {QualityRules.Name(s.quality)}\n공격력 {d.attack*m:0.##} · 방어력 {d.defense*m:0.##} · 공격 속도 {d.attackSpeed:0.##}";
            return info+"\n"+d.description+(string.IsNullOrEmpty(d.specialEffect)?"":"\n"+d.specialEffect);
        }
        void AddAutomatic(RecipeDefinition recipe)
        {
            var row=Instantiate(view.recipePrefab,autoList);row.gameObject.SetActive(true);row.Bind(view.font);int batches=1;
            row.title.text=recipe.displayName+" ×"+recipe.outputCount;row.ingredients.text=Ingredients(recipe);row.quantity.text="1";
            row.minus.onClick.AddListener(()=>{batches=Math.Max(1,batches-1);row.quantity.text=batches.ToString();});
            row.plus.onClick.AddListener(()=>{batches=Math.Min(999,batches+1);row.quantity.text=batches.ToString();});
            row.craft.onClick.AddListener(()=>{if(!Inventory.FillRecipe(recipe,batches)){Say("재료가 부족합니다.");StartCoroutine(Blink(row.title));return;}if(CurrentStation==Station.Furnace)Render();else StartCraft(recipe);});
        }
        IEnumerator Blink(TMP_Text text){for(int i=0;i<4;i++){if(!text)yield break;text.color=i%2==0?Color.red:BlacksmithView.Ink;yield return new WaitForSecondsRealtime(.13f);}}
        string Ingredients(RecipeDefinition r)
        {string value=string.Join(" + ",r.ingredients.Select(x=>catalog.Item(x.itemId).displayName+" ×"+x.count));if(r.station==Station.Tools)value+=$"\n{toolNames[(int)r.tool]} "+(r.maxStrokes>r.strokes?$"{r.strokes}~{r.maxStrokes}":r.strokes.ToString())+"회";if(r.station==Station.Anvil)value+="\n위/아래/좌/우/중앙: "+string.Join("/",r.anvilHits);return value;}
        void StartCraft(RecipeDefinition automatic,RecipeDefinition chosen=null)
        {
            if(IsHosted&&CampaignController.Instance!=null&&!Inventory.Data.night){Say("낮에는 제작할 수 없습니다. 침대에서 밤으로 전환하세요.");return;}

            if(automatic==null&&chosen==null&&CurrentStation==Station.Workbench)
            {
                var candidates=catalog.recipes.Where(r=>r.enabled&&r.station==CurrentStation&&Crafting.Batches(r)>0).ToArray();
                if(candidates.Length>1)
                {
                    var existing=view.overlay.Find("ChooseCraftResult");if(existing)Destroy(existing.gameObject);
                    var p=view.Panel("ChooseCraftResult",view.overlay,new Vector2(.26f,.30f),new Vector2(.68f,.75f),true);
                    view.Text("Title",p,"제작할 결과물을 선택하세요",27,new Vector2(.04f,.78f),new Vector2(.96f,.96f),BlacksmithView.Ink);
                    var list=view.Scroll(p,"PossibleResults",new Vector2(.04f,.22f),new Vector2(.96f,.75f));
                    foreach(var r in candidates)
                    {
                        var b=view.Button("Choose_"+r.id,list,r.displayName+" ×"+(r.outputCount*Crafting.Batches(r)),Vector2.zero,Vector2.one,()=>{Destroy(p.gameObject);StartCraft(null,r);});
                        b.gameObject.AddComponent<LayoutElement>().preferredHeight=65;
                    }
                    view.Button("CancelResultChoice",p,"취소",new Vector2(.32f,.03f),new Vector2(.68f,.18f),()=>Destroy(p.gameObject));return;
                }
            }
            if(!Crafting.Begin(CurrentStation,CurrentTool,out var error,chosen??automatic)){Say(error);return;}
            timer=0;quenchStarted=false;quenchCenter=UnityEngine.Random.Range(.65f,.87f);
            if(automatic!=null){Crafting.ApplyAutomatic(automatic);StartCoroutine(ResolveAnimation(true));return;}
            if(CurrentStation==Station.Furnace||(CurrentStation==Station.Workbench&&!Crafting.NeedsTiming()))StartCoroutine(ResolveAnimation(true));
            else SetState(ScreenState.Playing);
        }
        void DrawMinigame()
        {
            playingRoot=view.Rect("Minigame",view.overlay,new Vector2(.18f,.16f),new Vector2(.82f,.80f));
            var s=Inventory.Selection[0];string icon=catalog.Item(s.itemId).sprite;
            if(CurrentStation==Station.Workbench){var r=Crafting.Matching(false);if(r!=null)icon=catalog.Item(r.outputId).sprite;}
            workIcon=view.Image("Workpiece",playingRoot,icon,Color.white,new Vector2(.28f,.15f),new Vector2(.72f,.83f),true);
            counter=view.Text("Counter",playingRoot,"",27,new Vector2(.02f,.84f),new Vector2(.98f,1),null,TextAlignmentOptions.Center);
            if(CurrentStation==Station.Tools)
            {
                workIcon.raycastTarget=true;gesture=workIcon.gameObject.AddComponent<CraftGestureInput>();
                gesture.Click=()=>{if(CurrentTool==ToolKind.Hammer)ProcessStroke();};
                gesture.Upstroke=()=>{if(CurrentTool==ToolKind.Knife||CurrentTool==ToolKind.Plane)ProcessStroke();};
                gesture.Roundtrip=()=>{if(CurrentTool==ToolKind.Saw||CurrentTool==ToolKind.Whetstone)ProcessStroke();};
                view.Image("HeldTool",playingRoot,toolArt[(int)CurrentTool],Color.white,new Vector2(.76f,0),new Vector2(.95f,.49f),true);
                counter.text=$"{toolNames[(int)CurrentTool]} · 손질 {Crafting.Strokes} / 10";
                view.Button("FinishTool",view.overlay,"그만하기",new Vector2(.75f,.08f),new Vector2(.94f,.16f),()=>StartCoroutine(ResolveAnimation(true)));
                Say(CurrentTool==ToolKind.Hammer?"재료를 클릭하세요.":CurrentTool==ToolKind.Saw||CurrentTool==ToolKind.Whetstone?"재료를 누른 채 위아래로 왕복하세요.":"재료 위에서 아래 → 위로 드래그하세요.");
            }
            else if(CurrentStation==Station.Anvil)
            {
                Vector2[] pos={new Vector2(.5f,.72f),new Vector2(.5f,.22f),new Vector2(.29f,.47f),new Vector2(.71f,.47f),new Vector2(.5f,.47f)};
                for(int i=0;i<5;i++){int n=i;var b=view.Button("HammerPoint_"+i,playingRoot,"",pos[i]-new Vector2(.045f,.075f),pos[i]+new Vector2(.045f,.075f),()=>{if(!Crafting.Hit(n)){Say("달궈진 금속만 가공할 수 있습니다.");return;}counter.text="위/아래/좌/우/중앙  "+string.Join(" / ",Crafting.Hits);workIcon.rectTransform.localScale=new Vector3(1+Crafting.Hits.Sum()*.06f,1-Crafting.Hits.Sum()*.035f,1);if(Crafting.Hits.Sum()==5)StartCoroutine(ResolveAnimation(true));},"ring");}
                counter.text="5개의 지점을 두드리세요 · 총 5회";
                view.Button("FinishInvalid",view.overlay,"가공 종료",new Vector2(.77f,.08f),new Vector2(.94f,.15f),()=>StartCoroutine(ResolveAnimation(true)));
            }
            else if(CurrentStation==Station.Quench)
            {
                workIcon.raycastTarget=true;gesture=workIcon.gameObject.AddComponent<CraftGestureInput>();gesture.Click=()=>{quenchStarted=true;timer=0;};gesture.Released=()=>FinishQuench();
                var gauge=view.Panel("QuenchGauge",playingRoot,new Vector2(.83f,.08f),new Vector2(.90f,.78f));
                view.Image("OrangeZone",gauge,null,new Color(1,.5f,.1f),new Vector2(.05f,quenchCenter-.16f),new Vector2(.95f,quenchCenter+.16f));
                view.Image("YellowZone",gauge,null,Color.yellow,new Vector2(.05f,quenchCenter-.10f),new Vector2(.95f,quenchCenter+.10f));
                view.Image("GreenZone",gauge,null,Color.green,new Vector2(.05f,quenchCenter-.04f),new Vector2(.95f,quenchCenter+.04f));
                progressImage=view.Image("GaugeMarker",gauge,null,Color.white,new Vector2(-.1f,0),new Vector2(1.1f,.025f));
                counter.text="재료를 누르고 있다가 노랑·초록에서 놓으세요";
            }
            else DrawTimingTarget();
        }
        void ProcessStroke()
        {
            if(State!=ScreenState.Playing)return;
            if(!Crafting.Stroke()){Say("이 도구로 가공할 수 없는 재료입니다.");return;}
            counter.text=$"{toolNames[(int)CurrentTool]} · 손질 {Crafting.Strokes} / 10";
            workIcon.rectTransform.localRotation=Quaternion.Euler(0,0,Crafting.Strokes%2==0?-3:3);
            if(Crafting.Strokes>=10)StartCoroutine(ResolveAnimation(true));
        }
        void DrawTimingTarget()
        {
            var old=playingRoot.Find("TimingTarget");if(old){old.gameObject.SetActive(false);Destroy(old.gameObject);}
            float x=.36f+(Crafting.Completed%3)*.14f,y=.32f+(Crafting.Completed%2)*.22f;
            var b=view.Button("TimingTarget",playingRoot,"",new Vector2(x-.05f,y-.09f),new Vector2(x+.05f,y+.09f),()=>ScoreTiming(),"disc");
            guide=view.Image("ShrinkingRing",b.transform,"ring",Color.white,Vector2.zero,Vector2.one,true);
            counter.text=$"조립 {Crafting.Completed} / {Crafting.Targets}   점수 {Crafting.Score}";timer=0;
            workIcon.color=Color.Lerp(new Color(.03f,.03f,.03f),Color.white,(float)Crafting.Completed/Crafting.Targets);
        }
        void ScoreTiming()
        {
            if(State!=ScreenState.Playing||CurrentStation!=Station.Workbench)return;
            float distance=Mathf.Abs(1-timer/1.4f);int previous=Crafting.Score;Crafting.Timing(distance);Say(Crafting.Score-previous==4?"완벽 +4":Crafting.Score-previous==2?"성공 +2":"실패 +0");
            if(Crafting.Completed>=Crafting.Targets)StartCoroutine(ResolveAnimation(true));else DrawTimingTarget();
        }
        void FinishQuench()
        {if(State!=ScreenState.Playing||!quenchStarted)return;bool success=Mathf.Abs(timer/3f-quenchCenter)<=.10f;StartCoroutine(ResolveAnimation(success));}
        IEnumerator ResolveAnimation(bool success)
        {
            State=ScreenState.Animating;Render();
            var icon=view.Image("Processing",view.overlay,catalog.Item(Inventory.Selection[0].itemId).sprite,Color.white,new Vector2(.43f,.37f),new Vector2(.57f,.62f),true);
            float duration=CurrentStation==Station.Furnace?2.5f:1.1f;
            for(float elapsed=0;elapsed<duration;elapsed+=Time.unscaledDeltaTime)
            {icon.color=Color.Lerp(Color.white,CurrentStation==Station.Furnace?new Color(1,.35f,.04f):BlacksmithView.Gold,elapsed/duration);icon.rectTransform.localScale=Vector3.one*(1+.06f*Mathf.Sin(elapsed*18));yield return null;}
            result=Crafting.Finish(success);Persist();SetState(ScreenState.Result);
        }
        void DrawResult()
        {
            var p=view.Panel("ResultPanel",view.overlay,new Vector2(.29f,.19f),new Vector2(.71f,.81f),true);
            view.Text("Title",p,result.success?"제작 완료":"가공 결과",36,new Vector2(.08f,.78f),new Vector2(.92f,.96f),BlacksmithView.Ink,TextAlignmentOptions.Center);
            if(result.stack!=null)view.Image("ResultIcon",p,catalog.Item(result.stack.itemId).sprite,Color.white,new Vector2(.30f,.38f),new Vector2(.70f,.77f),true);
            view.Text("ResultName",p,result.stack==null ? result.message+"\n"+(result.returned.Count==0?"반환된 재료 없음":string.Join(" / ",result.returned.Select(s=>catalog.Item(s.itemId).displayName+" ×"+s.count))) : $"{catalog.Item(result.stack.itemId).displayName} ×{result.stack.count}\n{QualityRules.Name(result.stack.quality)}"+(result.returned.Count>0?"\n숙련 보너스: "+string.Join(" / ",result.returned.Select(s=>QualityRules.Name(s.quality)+" ×"+s.count)):""),27,new Vector2(.04f,.20f),new Vector2(.96f,.38f),BlacksmithView.Ink,TextAlignmentOptions.Center);
            view.Button("Collect",p,"확인",new Vector2(.24f,.05f),new Vector2(.76f,.18f),()=>{bool discovered=result.discovered;
                var next=result.stack!=null&&catalog.Item(result.stack.itemId).heated&&result.success ? (CurrentStation==Station.Furnace?Station.Anvil:CurrentStation==Station.Anvil?Station.Quench:CurrentStation) : CurrentStation;
                if(next!=CurrentStation){CurrentStation=next;var source=Inventory.Data.chest.Find(s=>s.Key==result.stack.Key);if(source!=null)Inventory.Transfer(Inventory.Data.chest,Inventory.Selection,source,Math.Min(source.count,result.stack.count));SetState(ScreenState.Selecting);}else SetState(ScreenState.Station);
                Say(discovered?"새 레시피를 발견했습니다! 두루마리에서 확인하세요.":result.message??"결과물을 보관함에 넣었습니다.");});
        }
        void DrawChest()
        {
            var bag=view.Panel("Bag",view.overlay,new Vector2(.045f,.16f),new Vector2(.455f,.84f));
            view.Image("BagTop",bag,"bag_top",Color.white,new Vector2(.02f,.76f),new Vector2(.98f,1),true);
            view.Button("BagTab",bag,"배낭",new Vector2(.02f,.67f),new Vector2(.32f,.77f),()=>{equipmentMode=false;Render();});
            view.Button("EquipmentTab",bag,"장비",new Vector2(.34f,.67f),new Vector2(.64f,.77f),()=>{equipmentMode=true;Render();});
            view.Button("NextBagPage",bag,$"{bagPage+1}쪽 →",new Vector2(.66f,.67f),new Vector2(.98f,.77f),()=>{bagPage=(bagPage+1)%Math.Max(1,(Inventory.Data.bag.Count+19)/20);Render();});
            if(equipmentMode)DrawEquipment(bag);
            else
            {
                var grid=view.Scroll(bag,"BagItems",new Vector2(.03f,.03f),new Vector2(.98f,.65f),5,103);
                foreach(var stack in Inventory.Data.bag.Skip(bagPage*20).Take(20).ToArray())ChestSlot(grid,stack,true);
                AddEmptyTransfer(grid,true);
            }
            var chest=view.Panel("ChestPanel",view.overlay,new Vector2(.51f,.16f),new Vector2(.975f,.84f));
            view.Text("ChestTitle",chest,"보관함",28,new Vector2(.02f,.89f),new Vector2(.95f,.99f));DrawFilters(chest,()=>RefreshInventory(true));
            inventoryGrid=view.Scroll(chest,"ChestItems",new Vector2(.025f,.045f),new Vector2(.99f,.66f),6,105);RefreshInventory(true);
            string[] categories={"전체","목재","석재","철","무기","방어구","기타"};
            for(int i=0;i<7;i++){int n=i;var b=view.Button("Material"+i,view.overlay,categories[i],new Vector2(.51f+i*.066f,.085f),new Vector2(.573f+i*.066f,.15f),()=>{material=n-1;RefreshInventory(true);});b.GetComponentInChildren<TMP_Text>().fontSize=18;}
            view.Button("BulkDiscard",view.overlay,discardMode?"선택 버리기":"일괄 선택",new Vector2(.045f,.085f),new Vector2(.19f,.15f),()=>
            {if(!discardMode){discardMode=true;discard.Clear();Say("버릴 아이템을 선택하세요. 선택 버리기로 확정합니다.");}else{Inventory.Data.chest.RemoveAll(x=>discard.Contains(x.Key));discard.Clear();discardMode=false;Persist();}Render();});
            view.Button("CancelHeld",view.overlay,held==null?"선택 해제":"들기 취소",new Vector2(.20f,.085f),new Vector2(.34f,.15f),()=>{held=null;discardMode=false;discard.Clear();Render();});
        }
        void ChestSlot(Transform parent,Stack stack,bool bag)
        {
            InventorySlotView slot=null;var source=bag?Inventory.Data.bag:Inventory.Data.chest;
            slot=CreateSlot(parent,stack,()=>
            {
                if(discardMode&&!bag){if(!discard.Add(stack.Key))discard.Remove(stack.Key);slot.background.color=discard.Contains(stack.Key)?Color.red:Color.white;return;}
                if(equipmentMode&&!bag){if(!Inventory.Equip(stack))Say("이 칸에 장착할 수 없습니다.");Persist();Render();return;}
                if(held!=null)
                {
                    var from=heldBag?Inventory.Data.bag:Inventory.Data.chest;var original=from.Find(x=>x.Key==held.Key);
                    if(heldBag==bag){if(original!=null)Inventory.Reorder(from,original,stack);}else Inventory.Transfer(from,source,original,Math.Min(held.count,original?.count??0));
                    held=null;Persist();Render();return;
                }
                held=stack.Copy(slot.LongPress?stack.count:1);heldBag=bag;Say($"{catalog.Item(stack.itemId).displayName} ×{held.count} 들기 · 반대편 칸을 클릭하세요.");
            },()=>{Inventory.Transfer(source,bag?Inventory.Data.chest:Inventory.Data.bag,stack,stack.count);held=null;Persist();Render();},drag=>
            {
                MoveDropped(drag,source,stack);held=null;Persist();Render();
            },bag);
            if(discard.Contains(stack.Key))slot.background.color=Color.red;
        }
        void AddEmptyTransfer(Transform parent,bool bag)
        {
            var go=view.Button("EmptySlot",parent,"+",Vector2.zero,Vector2.one,()=>
            {if(held==null)return;var from=heldBag?Inventory.Data.bag:Inventory.Data.chest;var original=from.Find(x=>x.Key==held.Key);Inventory.Transfer(from,bag?Inventory.Data.bag:Inventory.Data.chest,original,Math.Min(held.count,original?.count??0));held=null;Persist();Render();},bag?"bag_slot":"slot");
            go.gameObject.AddComponent<ItemDropTarget>().Drop=drag=>{MoveDropped(drag,bag?Inventory.Data.bag:Inventory.Data.chest,null);Persist();Render();};
        }
        void MoveDropped(InventorySlotView drag,List<Stack> destination,Stack before)
        {
            var stack=drag.Stack;int amount=stack.count;var equipped=Inventory.Data.equipment.Find(e=>ReferenceEquals(e.stack,stack));
            if(equipped!=null){string key=stack.Key;Inventory.Unequip(equipped.slot);stack=Inventory.Data.chest.Find(s=>s.Key==key);}
            var from=Inventory.Data.bag.Contains(stack)?Inventory.Data.bag:Inventory.Data.chest;
            if(from==destination){if(before!=null)Inventory.Reorder(from,stack,before);}else Inventory.Transfer(from,destination,stack,amount);
        }
        void DrawEquipment(Transform parent)
        {
            string[] slots={"Weapon", "Shield", "Head", "Armor", "Legs", "Feet", "Pickaxe", "Axe"};string[] names={"무기","방패","머리","몸통","하의","신발","곡괭이","도끼"};
            var weapon=Inventory.Data.equipment.Find(x=>x.slot=="Weapon");var def=weapon==null?null:catalog.Item(weapon.stack.itemId);
            if(def!=null&&def.bow){slots[1]="Arrow";names[1]="화살";}
            for(int i=0;i<slots.Length;i++)
            {
                string key=slots[i];var e=Inventory.Data.equipment.Find(x=>x.slot==key);float x=.04f+(i%4)*.24f,y=i<4?.46f:.23f;
                bool locked=key=="Shield"&&def!=null&&def.twoHanded||key=="Arrow"&&(def==null||!def.bow);
                var b=view.Button("Equip_"+key,parent,locked?"잠김":e==null?names[i]:"",new Vector2(x,y),new Vector2(x+.21f,y+.17f),()=>{Inventory.Unequip(key);Persist();Render();},e==null?null:catalog.Item(e.stack.itemId).sprite);b.interactable=!locked;b.gameObject.AddComponent<UiHoverOutline>();
                var target=b.gameObject.AddComponent<ItemDropTarget>();target.Drop=drag=>{if(!locked&&catalog.Item(drag.Stack.itemId).equipmentSlot==key){Inventory.Equip(drag.Stack);Persist();Render();}};
                target.Hover=enter=>{if(enter&&e!=null)view.ShowTooltip(ItemInfo(e.stack));else view.HideTooltip();};
                if(e!=null)
                {
                    b.gameObject.SetActive(false);
                    var slot=CreateSlot(parent,e.stack,()=>{Inventory.Unequip(key);Persist();Render();},null,drag=>{if(catalog.Item(drag.Stack.itemId).equipmentSlot==key){Inventory.Equip(drag.Stack);Persist();Render();}});
                    var rect=(RectTransform)slot.transform;rect.anchorMin=new Vector2(x,y);rect.anchorMax=new Vector2(x+.21f,y+.17f);rect.offsetMin=rect.offsetMax=Vector2.zero;
                }
                view.Text("EquipLabel",parent,names[i],18,new Vector2(x,y-.06f),new Vector2(x+.21f,y),null,TextAlignmentOptions.Center);
            }
            view.Text("Stats",parent,Inventory.EquipmentStats()+"장비 클릭: 장착 / 해제",17,new Vector2(.04f,.01f),new Vector2(.96f,.16f));
        }
        public void PrepareRecipe(RecipeDefinition recipe)
        {
            if(IsHosted&&CampaignController.Instance!=null&&!Inventory.Data.night){Say("낮에는 제작할 수 없습니다. 침대에서 밤으로 전환하세요.");return;}

            if(Crafting.Active){Say("가공 중에는 재료를 바꿀 수 없습니다.");return;}
            if(!Inventory.FillRecipe(recipe,1)){Say("레시피 재료가 부족합니다.");return;}
            CurrentStation=recipe.station;CurrentTool=recipe.tool;SetState(ScreenState.Selecting);
        }
        void ToggleRecipes()
        {
            if(State==ScreenState.Animating||State==ScreenState.Result)return;
            if(State==ScreenState.Recipes||State==ScreenState.Codex){State=returnState;Render();return;}
            returnState=State;SetState(ScreenState.Recipes);
        }
        readonly RecipeBookState recipeBookState = new RecipeBookState();
        void DrawRecipes()
        {
            var panel=view.Panel("RecipeBook",view.overlay,new Vector2(.035f,.075f),new Vector2(.965f,.90f),true);
            panel.gameObject.AddComponent<RecipeBookView>().Initialize(this,recipeBookState,ToggleRecipes);
        }
        void DrawSleep()
        {
            var panel=view.Panel("SleepConfirmation",view.overlay,new Vector2(.28f,.31f),new Vector2(.72f,.69f));
            view.Text("Question",panel,Inventory.Data.night?"잠을 자고 다음 날 아침을 맞이할까요?":"낮잠을 자면 채집 시간이 지나 밤이 됩니다.",28,new Vector2(.05f,.39f),new Vector2(.95f,.92f),null,TextAlignmentOptions.Center);
            view.Button("SleepYes",panel,"잠자기",new Vector2(.08f,.08f),new Vector2(.46f,.29f),()=>StartCoroutine(Sleep()));
            view.Button("SleepNo",panel,"취소",new Vector2(.54f,.08f),new Vector2(.92f,.29f),()=>SetState(ScreenState.Home));
        }
        IEnumerator Sleep()
        {
            State=ScreenState.Animating;Render();
            if(Inventory.Data.night){Inventory.Data.day++;Inventory.Data.night=false;}else Inventory.Data.night=true;
            Inventory.Data.hp=Inventory.Data.maxHp;Persist();
            var fade=view.Panel("DayTransition",view.overlay,Vector2.zero,Vector2.one);fade.GetComponent<Image>().color=Inventory.Data.night?new Color(.04f,.08f,.17f):new Color(.45f,.65f,.75f);
            int due=CampaignController.Instance!=null?Mathf.Max(0,CampaignController.Instance.State.lastDebtDay+7-Inventory.Data.day):7;
            view.Text("Day",fade,$"{Inventory.Data.day}일 · {(Inventory.Data.night?"밤":"아침")}\nD-{due}",64,Vector2.zero,Vector2.one,null,TextAlignmentOptions.Center);
            yield return new WaitForSecondsRealtime(1.5f);SetState(ScreenState.Home);Say("체력이 모두 회복되었습니다.");
        }
        void Back()
        {
            if(State==ScreenState.Animating||State==ScreenState.Result)return;
            if(State==ScreenState.Recipes||State==ScreenState.Codex){ToggleRecipes();return;}
            if(State==ScreenState.Playing){Say("가공을 마친 뒤 나갈 수 있습니다.");return;}
            Inventory.ReturnAll();held=null;discardMode=false;discard.Clear();Persist();SetState(State==ScreenState.Selecting||State==ScreenState.Fuel?ScreenState.Station:State==ScreenState.Station?ScreenState.Workshop:ScreenState.Home);
        }
        void Update()
        {
            if(Inventory==null)return;
            if(Keyboard.current!=null){if(Keyboard.current.tabKey.wasPressedThisFrame)ToggleRecipes();if(Keyboard.current.escapeKey.wasPressedThisFrame)Back();}
            if(State!=ScreenState.Playing)return;
            if(CurrentStation==Station.Workbench){timer+=Time.unscaledDeltaTime;if(guide)guide.rectTransform.localScale=Vector3.one*Mathf.Max(.65f,2-timer/1.4f);if(timer>1.85f)ScoreTiming();}
            if(CurrentStation==Station.Quench&&gesture&&gesture.Holding)
            {
                timer+=Time.unscaledDeltaTime;float value=Mathf.Clamp01(timer/3f);var r=progressImage.rectTransform;r.anchorMin=new Vector2(-.1f,value);r.anchorMax=new Vector2(1.1f,value+.025f);
                workIcon.rectTransform.localPosition=new Vector3(0,-Mathf.Sin(value*Mathf.PI)*65,0);
                if(value>=1)FinishQuench();
            }
        }
        public void Persist()
        {
            if(!loadSavedGame&&!IsHosted)return;
            // Persist a snapshot with uncommitted selection returned. A process exit
            // during selection never destroys the user's ingredients.
            var snapshot=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(Inventory.Data));
            if(!Crafting.Active)foreach(var s in Inventory.Selection)InventoryService.Add(snapshot.chest,s);
            if(IsHosted)SessionWriter?.Invoke(snapshot);else BlacksmithSave.Write(snapshot);
        }
        void OnApplicationQuit(){if(Inventory!=null){if(Crafting.Active){if(CurrentStation==Station.Furnace)Inventory.Data.fuel=Math.Min(50,Inventory.Data.fuel+Inventory.Selection.Sum(s=>s.count)*3);foreach(var s in Inventory.Selection)InventoryService.Add(Inventory.Data.chest,s);Inventory.Selection.Clear();}Persist();}}
    }
}
