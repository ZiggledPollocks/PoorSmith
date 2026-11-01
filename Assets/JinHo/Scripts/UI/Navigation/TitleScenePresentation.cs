using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Presentation only. Candidate data is decoded without installing or mutating a save.
public sealed partial class TitleSceneController
{
    const string ResetWarning="새 게임을 시작하면 현재 진행과 자동 저장 8개, 수동 저장을 초기화합니다.\n기본 저장과 백업도 초기화되며, 영구 백업은 보장되지 않습니다.\n다른 위치의 이전 빌드 저장과 조작·화면·소리 설정은 유지됩니다.\n새 게임을 시작하시겠습니까?";
    readonly List<ReleaseProgressFiles.Candidate> displayCandidates=new();
    TMP_Text continueSummary,informationBody,informationTitle;
    GameObject information;Button informationClose;Button rememberedMain;
    readonly List<Button> auxiliary=new();
    EventSystem titleEvents;bool oldNavigation;bool settingsWasOpen;
    Transform priorScope;
    static string TimePlayed(ReleaseProgressFiles.Candidate c)
    {
        if(c?.progress?.campaign==null||c.json==null||!c.json.Contains("\"playSeconds\""))return "플레이 시간 기록 없음";
        double seconds=c.progress.campaign.playSeconds;
        if(double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds<0)return "플레이 시간 기록 없음";
        double minutes=Math.Floor(seconds/60d);
        return minutes<60?$"{minutes:0}분":$"{Math.Floor(minutes/60):0}시간 {minutes%60:0}분";
    }
    static string Region(ReleaseProgressFiles.Candidate c)
    {
        var p=c?.progress;if(p?.campaign==null)return "지역 기록 없음";
        // The save stores only the field scene, not a reliable forest/cave region ID.
        if(ReleaseProgressFiles.Destination(p)==FieldSceneTravel.FieldScenePath)return "채집 필드";
        return "마을";
    }
    static string DayRegion(ReleaseProgressFiles.Candidate c)=>c?.Valid==true?$"{c.progress.smith.day}일 차 · {(c.progress.smith.night?"밤":"낮")} · {Region(c)}":"손상된 저장본";
    static string SavedTime(ReleaseProgressFiles.Candidate c)
    {
        // Legacy metadata may be absent or malformed. A file modification time is not an actual save time.
        return DateTime.TryParse(c?.progress?.savedUtc,null,System.Globalization.DateTimeStyles.RoundtripKind,out var utc)?utc.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss"):"저장 시각 기록 없음";
    }
    string HistoryLabel(int i)
    {
        var c=displayCandidates[i];if(!c.Valid)return "손상된 저장본 · 불러올 수 없음\n저장 정보를 확인할 수 없습니다";
        bool latest=i==displayCandidates.FindIndex(x=>x.Valid);
        return (latest?"[최신 정상 저장] ":"")+DayRegion(c)+"\n"+SavedTime(c)+" · "+TimePlayed(c);
    }
    string SaveConfirmation(int i)=>DayRegion(displayCandidates[i])+"\n"+SavedTime(displayCandidates[i])+"\n이 저장본으로 게임을 시작합니까?";
    void RefreshSummary()
    {
        continueSummary=mainMenu?.transform.Find("ContinueSummary")?.GetComponent<TMP_Text>();if(continueSummary==null)return;
        var recovery=RecoveryCandidate();var c=recovery??displayCandidates.FirstOrDefault(x=>x.Valid);
        continueSummary.text=c==null?"이어갈 수 있는 저장본이 없습니다.":(recovery!=null?"백업 복구 확인이 필요합니다.\n":"")+DayRegion(c)+"\n누적 플레이 "+TimePlayed(c);
    }
    void InitializePresentation()
    {
        titleEvents=EventSystem.current;if(titleEvents!=null){oldNavigation=titleEvents.sendNavigationEvents;titleEvents.sendNavigationEvents=false;}
        foreach(string n in new[]{"HelpControls"}){
            var button=mainMenu.transform.Find(n)?.GetComponent<Button>();if(button==null)continue;auxiliary.Add(button);string kind=n;button.onClick.AddListener(()=>ShowInformation(kind));
        }
        var version=mainMenu.transform.Find("Version")?.GetComponent<TMP_Text>();if(version!=null)version.text="v"+Application.version;
        var prefab=Resources.Load<GameObject>("UI/Screens/TitleInformation");if(prefab!=null){information=Instantiate(prefab,mainMenu.transform.parent,false);information.name="TitleInformation";informationBody=information.transform.Find("Panel/BodyScroll/Viewport/Content/Body").GetComponent<TMP_Text>();informationTitle=information.transform.Find("Panel/Heading").GetComponent<TMP_Text>();informationClose=information.transform.Find("Panel/Close").GetComponent<Button>();informationClose.onClick.AddListener(CloseInformation);information.SetActive(false);}
        if(settings!=null)settings.OnSettingsClosed+=RestoreMainFocus;
        RefreshSummary();WirePointers();Focus(RecoveryCandidate()==null&&displayCandidates.Any(x=>x.Valid)?continueButton:startButton);
    }
    void SetAuxiliaryInteractable(bool enabled){foreach(var b in auxiliary)b.interactable=enabled;}
    void RememberMainFocus(){var selected=titleEvents?.currentSelectedGameObject;if(selected!=null&&selected.transform.IsChildOf(mainMenu.transform))rememberedMain=selected.GetComponent<Button>();}
    void RestoreMainFocus(){if(mainMenu!=null&&mainMenu.activeInHierarchy)Focus(rememberedMain!=null?rememberedMain:settingsButton);}
    void FocusSettings(){var scope=ActiveScope();Focus(scope?.GetComponentsInChildren<Selectable>().FirstOrDefault(x=>x.IsInteractable()&&x.gameObject.activeInHierarchy));}
    Button FirstHistoryOrBack()=>historyButtons.FirstOrDefault(x=>x.IsInteractable())??saveListBackButton;
    ReleaseDecisionUI ActiveDecision()=>FindObjectsByType<ReleaseDecisionUI>(FindObjectsSortMode.None).FirstOrDefault(x=>x.IsOpen);
    void FocusDecision(){var decision=ActiveDecision();if(decision?.IsOpen==true)Focus(decision.transform.Find("ReleaseDecision/Panel/Cancel")?.GetComponent<Button>());}
    void Focus(Selectable target){if(target!=null&&target.gameObject.activeInHierarchy&&target.IsInteractable())EventSystem.current?.SetSelectedGameObject(target.gameObject);}
    Transform ActiveScope()
    {
        var decision=ActiveDecision();if(decision!=null)return decision.transform.Find("ReleaseDecision");
        if(settings?.IsSettingsOpen==true)return settingsMainMenuButton.transform.parent;
        if(information!=null&&information.activeSelf)return information.transform;
        if(loadConfirmation!=null&&loadConfirmation.activeSelf)return loadConfirmation.transform;
        if(newGameConfirmation!=null&&newGameConfirmation.activeSelf)return newGameConfirmation.transform;
        return saveListScreen!=null&&saveListScreen.activeSelf?saveListScreen.transform:mainMenu.transform;
    }
    Transform NavigationScope()
    {
        var decision=ActiveDecision();if(decision!=null)return decision.transform.Find("ReleaseDecision");
        if(settings?.IsSettingsOpen!=true)return ActiveScope();
        // The settings Close button belongs under the authored settings root, not a new Canvas.
        Transform t=settingsMainMenuButton.transform;while(t.parent!=null&&t.parent!=mainMenu.transform.parent)t=t.parent;return t;
    }
    List<Selectable> NavigationItems()
    {
        var scope=NavigationScope();if(scope==null)return new();
        if(scope==mainMenu.transform){var result=new List<Selectable>{continueButton,startButton,saveListButton,settingsButton,quitButton};result.AddRange(auxiliary);if(importButton!=null)result.Add(importButton);return result.Where(Usable).ToList();}
        if(scope==saveListScreen.transform){var result=historyButtons.Cast<Selectable>().ToList();result.Add(saveListBackButton);return result.Where(Usable).ToList();}
        return scope.GetComponentsInChildren<Selectable>().Where(Usable).OrderByDescending(x=>x.transform.position.y).ThenBy(x=>x.transform.position.x).ToList();
    }
    static bool Usable(Selectable x)=>x!=null&&x.gameObject.activeInHierarchy&&x.IsInteractable();
    void WirePointers(){if(mainMenu==null)return;foreach(var x in mainMenu.transform.parent.GetComponentsInChildren<Selectable>(true)){var hook=x.GetComponent<TitlePointerFocus>()??x.gameObject.AddComponent<TitlePointerFocus>();hook.owner=this;}}
    internal void PointerFocus(Selectable x){if(loading||!Usable(x))return;var scope=NavigationScope();if(scope!=null&&x.transform.IsChildOf(scope))Focus(x);}
    // One title-owned navigation path prevents EventSystem submit and keyboard polling from loading twice.
    void UpdateTitleNavigation()
    {
        if(loading)return;WirePointersIfScopeChanged();
        if(GameUIController.CancelPressed&&GameUIController.HandleEscape())return;
        var k=Keyboard.current;if(k==null)return;
        if(k.escapeKey.wasPressedThisFrame||Gamepad.current?.buttonEast.wasPressedThisFrame==true){
            if(settings?.IsSettingsOpen==true){if(controls?.ShouldBlockSettingsToggle!=true)CloseSettings();}
            else if(information!=null&&information.activeSelf)CloseInformation();
            else if(loadConfirmation.activeSelf)CancelLoadHistory();else if(newGameConfirmation.activeSelf)CancelNewGame();else if(saveListScreen.activeSelf)CloseSaveList();return;
        }
        if(controls?.ShouldBlockSettingsToggle==true)return;
        if(information!=null&&information.activeSelf&&(k.pageDownKey.wasPressedThisFrame||k.pageUpKey.wasPressedThisFrame)){
            var scroll=information.GetComponentInChildren<ScrollRect>();scroll.verticalNormalizedPosition=Mathf.Clamp01(scroll.verticalNormalizedPosition+(k.pageUpKey.wasPressedThisFrame?.35f:-.35f));return;
        }
        var items=NavigationItems();if(items.Count==0)return;
        var selected=EventSystem.current?.currentSelectedGameObject?.GetComponent<Selectable>();int index=items.IndexOf(selected);
        bool tab=k.tabKey.wasPressedThisFrame;int dir=tab?(k.shiftKey.isPressed?-1:1):(k.downArrowKey.wasPressedThisFrame||k.rightArrowKey.wasPressedThisFrame?1:k.upArrowKey.wasPressedThisFrame||k.leftArrowKey.wasPressedThisFrame?-1:0);
        if(dir!=0){
            if(!tab&&selected!=null&&selected is not Button){var move=new AxisEventData(EventSystem.current){moveDir=k.leftArrowKey.wasPressedThisFrame?MoveDirection.Left:k.rightArrowKey.wasPressedThisFrame?MoveDirection.Right:dir>0?MoveDirection.Down:MoveDirection.Up};ExecuteEvents.Execute(selected.gameObject,move,ExecuteEvents.moveHandler);}
            else Focus(items[(index+dir+items.Count)%items.Count]);
        }
        if((k.enterKey.wasPressedThisFrame||k.numpadEnterKey.wasPressedThisFrame)&&selected!=null&&index>=0)ExecuteEvents.Execute(selected.gameObject,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler);
    }
    void WirePointersIfScopeChanged(){var scope=NavigationScope();if(scope!=priorScope){priorScope=scope;WirePointers();var selected=EventSystem.current?.currentSelectedGameObject;if(selected==null||!selected.transform.IsChildOf(scope)){if(ActiveDecision()!=null)FocusDecision();else if(scope==loadConfirmation.transform)Focus(cancelLoadButton);else if(scope==newGameConfirmation.transform)Focus(cancelNewGameButton);else if(scope==mainMenu.transform&&rememberedMain!=null)Focus(rememberedMain);else Focus(NavigationItems().FirstOrDefault());}}}
    string ControlHelp()
    {
        var map=controls?.MoveAction?.actionMap;if(map==null)return "조작 설정을 불러올 수 없습니다. 설정의 조작 탭을 확인해 주세요.";
        var labels=new[]{("Move","이동"),("Jump","점프"),("Sprint","달리기"),("Roll","회피"),("Attack","공격"),("Interact","상호작용"),("Inventory","가방"),("ToolSlot1","도구 1"),("ToolSlot2","도구 2"),("ToolSlot3","도구 3"),("DialogueAdvance","대화 진행"),("DialogueFastForward","대화 빠르게")};
        return string.Join("\n",labels.Select(pair=>{var a=map.FindAction(pair.Item1,false);return pair.Item2+"  :  "+(a!=null?DesktopBindings(a):"설정 없음");}))+"\n\n키는 설정 → 조작에서 변경할 수 있습니다.\n메뉴: 방향키 / Tab · Shift+Tab · Enter · Esc\n긴 안내: 마우스 휠 / PageUp · PageDown";
    }
    // Show effective desktop bindings only; VR/gamepad paths are not desktop control instructions.
    static string DesktopBindings(InputAction action)
    {
        var names=action.bindings.Select((binding,index)=>(binding,index)).Where(x=>!x.binding.isComposite&&!string.IsNullOrEmpty(x.binding.effectivePath)&&(x.binding.effectivePath.StartsWith("<Keyboard>",StringComparison.OrdinalIgnoreCase)||x.binding.effectivePath.StartsWith("<Mouse>",StringComparison.OrdinalIgnoreCase))).Select(x=>action.GetBindingDisplayString(x.index,InputBinding.DisplayStringOptions.DontIncludeInteractions)).Distinct().ToArray();
        return names.Length>0?string.Join(" / ",names):"키 설정 없음";
    }
    void ShowInformation(string kind)
    {
        if(loading||information==null||kind!="HelpControls")return;
        RememberMainFocus();informationBody.text=ControlHelp();
        information.SetActive(true);information.transform.SetAsLastSibling();GameArtSkin.Apply(information.transform);Focus(informationClose);GameSound.UI("Popup",this);
    }
    void CloseInformation(){if(information!=null)information.SetActive(false);RestoreMainFocus();}
    void DisposePresentation(){if(settings!=null)settings.OnSettingsClosed-=RestoreMainFocus;if(titleEvents!=null)titleEvents.sendNavigationEvents=oldNavigation;}
}
// Runtime-only pointer selection: no per-frame mouse/keyboard tug-of-war and no serialized scene component.
public sealed class TitlePointerFocus:MonoBehaviour,IPointerEnterHandler
{
    internal TitleSceneController owner;
    public void OnPointerEnter(PointerEventData e){if(owner!=null&&!e.dragging)owner.PointerFocus(GetComponent<Selectable>());}
}
