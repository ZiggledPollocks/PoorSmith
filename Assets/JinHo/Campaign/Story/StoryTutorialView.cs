using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Blacksmith;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Presentation is authored in Story prefabs; timing and input stay here.</summary>
[DefaultExecutionOrder(12000)]
public sealed class StoryTutorialView : MonoBehaviour
{
    RectTransform root, dialogue, mini, task, confirmation, overlay;
    Image dim, leftPortrait, rightPortrait, miniPortrait, item, walker;
    Image dialogueClickSurface, miniClickSurface;
    TMP_Text leftName, rightName, speech, miniName, miniSpeech, objective, warning;
    Button advance, miniAdvance, skip;
    RectTransform goldPlate; TMP_Text tutorialGold;
    readonly List<Image> masks = new();
    readonly List<RectTransform> frames = new();
    readonly Vector3[] corners = new Vector3[4];
    readonly List<Rect> holes = new();
    AudioListener storyListener, worldListener;
    float nextListenerCheck;
    Sprite idle, npcIdle, reminderRightSprite;
    Color reminderRightColor;
    string[] lines;
    readonly Queue<string> pages = new();
    int line, visible;
    float glyphClock, finishedClock, warningUntil, presentationClock;
    bool active, miniMode, skipRequested;
    bool tutorialHud = true;
    bool logoVisible;
    RectTransform logoBackdrop;
    bool? knownHammer;
    bool revealed => knownHammer ?? StoryTutorial.HammerRevealed;
    public event Action SkipConfirmed;
    public event Action<string> Cue;
    public bool Speaking => active || reminding;
    public bool CoversGameplayHud => logoVisible || confirmation!=null && confirmation.gameObject.activeInHierarchy || dialogue!=null && dialogue.gameObject.activeInHierarchy;
    public bool Reminding=>reminding;
    public event Action ReminderFinished;
    bool reminding, reminderActive, reminderMini, reminderDialogueVisible, reminderMiniVisible;
    string reminderText;int reminderVisible;bool reminderNext;
    const double ReminderSeconds = 2;
    double reminderCompletedAt = -1;
    float reminderGlyphClock, reminderFinishedClock;
    string reminderRightName, reminderMiniName;
    bool reminderRightPortrait, reminderMiniPortrait;
    Sprite reminderMiniSprite;Color reminderMiniColor;
    Coroutine placement;string placementKey;
    Vector2 objectiveMin, objectiveMax, miniPreferredSize;
    string objectiveSource;
    readonly List<TMP_Text> keyHints=new();
    public bool ConfirmationOpen => confirmation != null;
    public float Speed => ConfirmationOpen ? 0 : ReleaseInput.Held("DialogueFastForward") ? 2 : 1;
    TMP_Text Body => miniMode ? miniSpeech : speech;
    Button Next => miniMode ? miniAdvance : advance;

    RectTransform Load(string name, Transform parent = null)
    {
        var prefab = Resources.Load<GameObject>("Story/" + name);
        if (prefab == null) throw new InvalidOperationException("Missing story UI prefab: " + name);
        var created=Instantiate(prefab, parent ?? root, false).transform as RectTransform;
        TownFieldUiStyle.PresentGameplay(created);
        return created;
    }
    static T Node<T>(Transform parent, string name) where T : Component =>
        parent.GetComponentsInChildren<T>(true).First(c => c.name == name);
    public void Build(Sprite playerIdle, TMP_FontAsset font, bool tutorialHud = true, bool? hammerKnown = null)
    {
        this.tutorialHud = tutorialHud; knownHammer = hammerKnown;
        idle = playerIdle;
        npcIdle = Resources.Load<TimedSpriteMotionSet>("Story/PasserAnimations")?.idle.At(0);
        var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 5000;
        var scaler = gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        gameObject.AddComponent<GraphicRaycaster>(); root = (RectTransform)transform;
        overlay = Load("StoryTutorialOverlay");
        var firstMask = Node<Image>(overlay, "TutorialMask"); firstMask.raycastTarget = true;firstMask.color=new Color(0,0,0,GameUIController.ModalShadeAlpha);
        firstMask.gameObject.AddComponent<StoryBlockedControl>(); firstMask.gameObject.SetActive(false); masks.Add(firstMask);
        task = (RectTransform)Node<Image>(overlay, "TutorialObjective").transform;
        objectiveMin=task.anchorMin;objectiveMax=task.anchorMax;
        objective = Node<TMP_Text>(task, "Objective"); warning = Node<TMP_Text>(overlay, "FollowTeacher");
        skip = Node<Button>(overlay, "StorySkip"); skip.onClick.RemoveAllListeners(); skip.onClick.AddListener(AskSkip);
        skip.GetComponentInChildren<TMP_Text>().text = "스킵";
        skip.gameObject.AddComponent<StorySkipOpacity>();
        item = Node<Image>(overlay, "StoryItem"); walker = Node<Image>(overlay, "PassingPerson");
        item.preserveAspect = true;
        dialogue = Load("StoryDialogue"); mini = Load("StoryMiniDialogue");
        // Keep the prefab-authored reading area when avoiding tutorial targets.
        miniPreferredSize = mini.anchorMax - mini.anchorMin;
        leftPortrait = Node<Image>(dialogue, "PlayerPortrait"); leftPortrait.sprite = idle;
        rightPortrait = Node<Image>(dialogue, "OtherCharacter");
        leftName = Node<TMP_Text>(dialogue, "SpeakerLeft"); rightName = Node<TMP_Text>(dialogue, "SpeakerRight");
        speech = Node<TMP_Text>(dialogue, "Speech"); advance = Node<Button>(dialogue, "StoryNext");
        miniPortrait = Node<Image>(mini, "MiniPortrait"); miniName = Node<TMP_Text>(mini, "MiniName");
        miniSpeech = Node<TMP_Text>(mini, "MiniSpeech"); miniAdvance = Node<Button>(mini, "MiniNext");
        foreach(var g in mini.GetComponentsInChildren<Graphic>(true))g.raycastTarget=false;miniAdvance.image.raycastTarget=true;
        foreach (var b in new[] { advance, miniAdvance }) { b.onClick.RemoveAllListeners(); b.onClick.AddListener(AdvanceFromPointer); }
        dialogueClickSurface = dialogue.GetComponent<Image>();
        miniClickSurface = mini.GetComponent<Image>();
        foreach (var panel in new[] { dialogue, mini })
            panel.gameObject.AddComponent<StoryDialoguePointerAdvance>().Click = AdvanceFromPointer;
        RefreshDialogueClickSurfaces();
        dim = Load("StoryTransition").GetComponent<Image>(); dim.raycastTarget = false;
        dim.transform.SetAsFirstSibling();
        foreach (var t in GetComponentsInChildren<TMP_Text>(true)) { t.font=GameTextFonts.Primary; t.fontStyle=FontStyles.Normal; FullTextLayout.Bind(t); }
        dialogue.gameObject.SetActive(false); mini.gameObject.SetActive(false); task.gameObject.SetActive(false);
        item.gameObject.SetActive(false); walker.gameObject.SetActive(false); skip.gameObject.SetActive(false);
        Node<TMP_Text>(overlay, "ClosureSign").gameObject.SetActive(false);
        storyListener = gameObject.AddComponent<AudioListener>(); storyListener.enabled = false;
        // Reuse the town HUD styling; this non-interactive copy survives story masks and room canvases.
        if (tutorialHud)
        {
            var ui = CampaignController.Instance.UI.view;
            goldPlate = ui.Panel("TutorialGoldPlate", root, new(.02f,.845f), new(.20f,.905f));
            goldPlate.GetComponent<Image>().raycastTarget = false;
            tutorialGold = ui.Text("TutorialGold", goldPlate, "", 27, Vector2.zero, Vector2.one);
            if (font != null) tutorialGold.font = font;
            RefreshGold();
        }
        keyHints.AddRange(GetComponentsInChildren<TMP_Text>(true).Where(x=>x.text.Contains("Space로 계속")));
        RefreshHints();SetObjective("");
        GameArtSkin.Apply(root);
        TownFieldUiStyle.PresentGameplay(root);
    }
    public void ShowClosure(Vector3 worldPosition)
    {
        var sign = Node<TMP_Text>(overlay, "ClosureSign");
        Vector3 point = Camera.main != null ? Camera.main.WorldToViewportPoint(worldPosition) : new Vector3(.5f, .55f);
        float x = Mathf.Clamp(point.x, .1f, .9f), y = Mathf.Clamp(point.y + .1f, .3f, .8f);
        Rect((RectTransform)sign.transform, new Vector2(x-.09f,y), new Vector2(x+.09f,y+.07f));
        sign.gameObject.SetActive(true);
    }
    public void RevealHammer() => Cue?.Invoke("reveal");
    public void SetSkipAvailable(bool value) => skip.gameObject.SetActive(value);
    public void SetObjective(string value)
    {
        objectiveSource=value;objective.text = ReleaseInput.Hints(value); objective.gameObject.SetActive(true); task.gameObject.SetActive(!string.IsNullOrEmpty(value));
        // Action guidance reuses the same mini prefab, but never retains a dialogue Next button.
        if (!active)
        {
            bool show = miniMode && !string.IsNullOrEmpty(value);
            mini.gameObject.SetActive(show); miniAdvance.gameObject.SetActive(false);
            if (show) { task.gameObject.SetActive(false); ConfigureMiniDialogue(); FitMiniContent(value,false); miniSpeech.text=value; miniSpeech.ForceMeshUpdate();
                // 행동 안내는 페이지를 넘기지 않으므로 문장 높이에 맞춰 본문 공간을 확보한다.
                float preferred=miniSpeech.GetPreferredValues(value,Mathf.Max(1,miniSpeech.rectTransform.rect.width),Mathf.Infinity).y;
                float fraction=miniSpeech.rectTransform.anchorMax.y-miniSpeech.rectTransform.anchorMin.y;
                if(fraction>.1f && preferred>miniSpeech.rectTransform.rect.height)
                {
                    float desired=Mathf.Min(root.rect.height*.46f,(preferred+10)/fraction);
                    mini.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,Mathf.Max(mini.rect.height,desired));
                    miniPreferredSize=new Vector2(mini.rect.width/Mathf.Max(1,root.rect.width),mini.rect.height/Mathf.Max(1,root.rect.height));
                }
                visible=miniSpeech.textInfo.characterCount; miniSpeech.maxVisibleCharacters=visible; }
        }
        foreach (string n in new[] { "WoodIcon", "LeatherIcon", "WoodCount", "LeatherCount" })
            Node<Component>(task, n).gameObject.SetActive(false);
    }
    public void SetGatherObjective(Sprite wood, Sprite leather, int woodCount, int leatherCount)
    {
        Rect(task,objectiveMin,objectiveMax);
        task.gameObject.SetActive(true); objective.gameObject.SetActive(false);
        Node<Image>(task, "WoodIcon").sprite = wood; Node<Image>(task, "LeatherIcon").sprite = leather;
        Node<TMP_Text>(task, "WoodCount").text = $"나무 원목 {woodCount}/15";
        Node<TMP_Text>(task, "LeatherCount").text = $"가죽 {leatherCount}/2";
        foreach (string n in new[] { "WoodIcon", "LeatherIcon", "WoodCount", "LeatherCount" }) Node<Component>(task, n).gameObject.SetActive(true);
    }
    // Until() closes only the completed action presentation; story lines/rewards are not advanced.
    public void CloseActionGuide()
    {
        if (!active && !reminding) mini.gameObject.SetActive(false);
        miniAdvance.gameObject.SetActive(false); Focus(null);
    }
    void ConfigureMiniDialogue()
    {
        FitMiniContent(miniSpeech.text,active||reminding);
    }
    void FitMiniContent(string body,bool reading)
    {
        float width=SmithingLoop.Instance?.InShop==true?.34f:.38f;
        miniSpeech.fontSize=24;miniName.fontSize=21;
        var nextLabel=miniAdvance.GetComponentInChildren<TMP_Text>();if(nextLabel!=null)nextLabel.fontSize=24;
        float preferred=miniSpeech.GetPreferredValues(body??"",Mathf.Max(100,root.rect.width*width*.76f),Mathf.Infinity).y;
        float height=Mathf.Clamp((preferred+12)/(reading?.40f:.63f)/Mathf.Max(1,root.rect.height),reading?.20f:.12f,.34f);
        var size=new Vector2(width,height);var pos=Vector2.Min(mini.anchorMin,new Vector2(.98f,.89f)-size);
        Rect(mini,pos,pos+size);Rect(miniName.rectTransform,new(.19f,.77f),new(.96f,.96f));
        Rect(miniSpeech.rectTransform,new(.19f,reading?.34f:.10f),new(.96f,.74f));
        Rect(miniAdvance.transform as RectTransform,new(.55f,.045f),new(.96f,.30f));
        miniPreferredSize=size;placementKey=null;
    }
    // Suspend presentation only; saved story progress decides whether the hammer is identifiable.
    public void Reject()
    {
        if(reminding||ConfirmationOpen)return;
        reminderActive=active;reminderMini=miniMode;reminderDialogueVisible=dialogue.gameObject.activeSelf;reminderMiniVisible=mini.gameObject.activeSelf;
        reminderText=Body.text;reminderVisible=visible;reminderNext=Next.gameObject.activeSelf;
        reminderGlyphClock=glyphClock;reminderFinishedClock=finishedClock;
        reminderRightName=rightName.text;reminderMiniName=miniName.text;
        reminderRightPortrait=rightPortrait.gameObject.activeSelf;reminderMiniPortrait=miniPortrait.gameObject.activeSelf;
        reminderMiniSprite=miniPortrait.sprite;reminderMiniColor=miniPortrait.color;
        reminderRightSprite=rightPortrait.sprite;reminderRightColor=rightPortrait.color;
        reminding=true;miniMode=reminderMiniVisible||miniMode;active=true;
        dialogue.gameObject.SetActive(!miniMode);mini.gameObject.SetActive(miniMode);
        Body.text=revealed?"내 말을 따르렴":"...";
        rightName.text=miniName.text=revealed?"에고 망치":"";
        rightPortrait.gameObject.SetActive(revealed);miniPortrait.gameObject.SetActive(revealed);
        SetEgoPortrait(rightPortrait, revealed); SetEgoPortrait(miniPortrait, revealed);
        Body.ForceMeshUpdate();visible=0;Body.maxVisibleCharacters=0;
        reminderCompletedAt=-1;glyphClock=finishedClock=0;Next.gameObject.SetActive(true);
    }
    void RestoreReminder()
    {
        reminding=false;reminderCompletedAt=-1;miniMode=reminderMini;active=reminderActive;Body.text=reminderText;Body.ForceMeshUpdate();
        glyphClock=reminderGlyphClock;finishedClock=reminderFinishedClock;
        rightName.text=reminderRightName;miniName.text=reminderMiniName;
        rightPortrait.gameObject.SetActive(reminderRightPortrait);miniPortrait.gameObject.SetActive(reminderMiniPortrait);
        miniPortrait.sprite=reminderMiniSprite;miniPortrait.color=reminderMiniColor;miniPortrait.preserveAspect=reminderMiniSprite!=null;
        rightPortrait.sprite=reminderRightSprite;rightPortrait.color=reminderRightColor;rightPortrait.preserveAspect=reminderRightSprite!=null;
        visible=reminderVisible;Body.maxVisibleCharacters=visible;Next.gameObject.SetActive(reminderNext);
        dialogue.gameObject.SetActive(reminderDialogueVisible);mini.gameObject.SetActive(reminderMiniVisible);
        FindFirstObjectByType<PlayerInputHandler>()?.ClearGameplayInput();ReminderFinished?.Invoke();
    }
    public void ShowItem(Sprite sprite, bool show) { item.sprite = sprite; item.gameObject.SetActive(show && sprite != null); }
    public IEnumerator CompleteObjective()
    {
        var group = task.GetComponent<CanvasGroup>();
        if (group == null) group = task.gameObject.AddComponent<CanvasGroup>();
        for (float t=0;t<.4f;t+=Time.unscaledDeltaTime*Speed)
        { group.alpha=1-Mathf.Clamp01(t/.4f); yield return null; }
        task.gameObject.SetActive(false); group.alpha=1;
    }
    public IEnumerator Speak(string[] text, bool maySkip = true, bool small = false)
    {
        if (text == null || text.Length == 0) yield break;
        lines = text; line = 0; skipRequested = false; miniMode = small; active = true;
        dialogue.gameObject.SetActive(!small); mini.gameObject.SetActive(small); skip.gameObject.SetActive(maySkip || tutorialHud && StoryTutorial.Guiding);
        if (!small) task.gameObject.SetActive(false);
        // Set the compact size before pagination, so a narrower tutorial never clips a long line.
        if(small) ConfigureMiniDialogue();
        DrawLine();
        while (line < lines.Length && !skipRequested) yield return null;
        active = false; dialogue.gameObject.SetActive(false); mini.gameObject.SetActive(false);
        task.gameObject.SetActive(!string.IsNullOrEmpty(objective.text));
        skip.gameObject.SetActive(tutorialHud && StoryTutorial.Guiding);
        if (tutorialHud) { PlayerInputHandler input = FindFirstObjectByType<PlayerInputHandler>(); input?.ClearGameplayInput(); }
    }
    // Identified speakers use their own art; the unrevealed hammer remains concealed.
    void SetEgoPortrait(Image portrait, bool ego, bool npc = false)
    { portrait.sprite = npc ? npcIdle : ego && GameArtSkin.Current != null ? GameArtSkin.Current.egoHammer : null; portrait.type = Image.Type.Simple; portrait.preserveAspect = portrait.sprite != null; portrait.color = portrait.sprite != null ? Color.white : Color.gray; }
    void DrawLine()
    {
        string raw = lines[line];
        foreach (string cue in new[] { "stand", "sit", "charge", "collector-sword", "collector-return" })
            if (raw.StartsWith("[" + cue + "]", StringComparison.Ordinal))
            { raw = raw.Substring(cue.Length + 2); Cue?.Invoke(cue); break; }
        if (raw.StartsWith("[reveal]", StringComparison.Ordinal)) { RevealHammer(); raw = raw.Substring(8); Sound("reveal"); }
        int colon = raw.IndexOf(':');
        bool other = raw.StartsWith("에고 망치:") || raw.StartsWith("박덕수:") || raw.StartsWith("???:");
        string name = other ? raw.Substring(0, colon) : "주인공";
        string body = other ? raw.Substring(colon + 1).Trim() : raw;
        if(miniMode)FitMiniContent(body,true);
        leftName.text = "주인공"; rightName.text = other ? name : revealed ? "에고 망치" : "";
        leftPortrait.color = other ? new Color(.55f, .55f, .55f) : Color.white;
        rightPortrait.gameObject.SetActive(revealed || name == "박덕수");
        SetEgoPortrait(rightPortrait, revealed && rightName.text == "에고 망치", name == "박덕수");
        if (rightPortrait.sprite == null) rightPortrait.color = other ? Color.gray : new Color(.28f,.28f,.28f);
        rightName.color = name == "에고 망치" ? new Color(.48f,.78f,.73f) : BlacksmithView.Gold;
        miniPortrait.sprite = other ? null : idle; miniPortrait.preserveAspect = !other;
        miniPortrait.color = other ? Color.gray : Color.white;
        if (other) SetEgoPortrait(miniPortrait, name == "에고 망치" && revealed, name == "박덕수");
        miniPortrait.gameObject.SetActive(!other || revealed || name == "박덕수"); miniName.text = name;
        Canvas.ForceUpdateCanvases(); pages.Clear();
        // Measure at the authored font size. Do not shrink long lines to fit.
        float width = Mathf.Max(100, Body.rectTransform.rect.width), height = Mathf.Max(60, Body.rectTransform.rect.height - 6);
        while (body.Length > 0)
        {
            int take = body.Length;
            while (take > 1 && Body.GetPreferredValues(body.Substring(0, take), width, Mathf.Infinity).y > height) take--;
            if (take < body.Length)
            {
                int boundary = body.LastIndexOfAny(new[] { ' ', '.', '!', '?', '。', '\n' }, take - 1, take);
                if (boundary > take / 2) take = boundary + 1;
            }
            pages.Enqueue(body.Substring(0, take).Trim()); body = body.Substring(take).TrimStart();
        }
        DrawPage();
    }
    void DrawPage()
    {
        Body.text = pages.Count > 0 ? pages.Dequeue() : ""; Body.ForceMeshUpdate();
        visible = 0; glyphClock = finishedClock = 0; Body.maxVisibleCharacters = 0; Next.gameObject.SetActive(true);
    }
    // Pointer input shares typewriter/page progression, but never dismisses teacher reminders.
    void AdvanceFromPointer()
    {
        if (!active || reminding || ConfirmationOpen) return;
        Advance();
    }
    // Action-wait mini panels stay transparent to the actual crafting controls beneath them.
    void RefreshDialogueClickSurfaces()
    {
        if (dialogueClickSurface != null) dialogueClickSurface.raycastTarget = active;
        if (miniClickSurface != null) miniClickSurface.raycastTarget = active;
    }
    void Advance()
    {
        if (!Speaking || ConfirmationOpen) return;
        if (visible < Body.textInfo.characterCount)
        {
            visible = Body.textInfo.characterCount; Body.maxVisibleCharacters = visible;
            if(reminding && reminderCompletedAt < 0) reminderCompletedAt=Time.realtimeSinceStartupAsDouble;
            return;
        }
        // Space dismisses only the borrowed reminder; the original line/task remains unchanged.
        if(reminding){RestoreReminder();return;}
        if (pages.Count > 0) { DrawPage(); return; }
        if (++line < lines.Length) DrawLine();
    }
    RectTransform parkedSkipPrompt;
    void AskSkip()
    {
        if (ConfirmationOpen)
        {
            if (!tutorialHud || !StoryTutorial.Guiding || confirmation.name.StartsWith("StorySkipConfirmation")) return;
            parkedSkipPrompt=confirmation;parkedSkipPrompt.gameObject.SetActive(false);confirmation=null;
        }
        confirmation = Load("StorySkipConfirmation"); confirmation.SetAsLastSibling();
        var yes = Node<Button>(confirmation, "SkipConfirm"); var no = Node<Button>(confirmation, "SkipCancel");
        yes.onClick.RemoveAllListeners(); no.onClick.RemoveAllListeners();
        yes.onClick.AddListener(() => { if(confirmation==null||!confirmation.name.StartsWith("StorySkipConfirmation"))return; skipRequested = true; CloseConfirmation(); SkipConfirmed?.Invoke(); });
        no.onClick.AddListener(() => { if(confirmation==null||!confirmation.name.StartsWith("StorySkipConfirmation"))return; var previous=parkedSkipPrompt;parkedSkipPrompt=null;CloseConfirmation();
            if(previous!=null){confirmation=previous;confirmation.gameObject.SetActive(true);} });
    }
    public void CancelPresentation()
    {
        GameSound.Stop(this); GameSound.OverrideMusic(this,null);
        ClearLogo();
        GameUIController.SetTutorialTargets(this,Array.Empty<Transform>());
        CleanupPasser();
        bool wasReminding=reminding;reminding=false;reminderCompletedAt=-1;active = false; dialogue.gameObject.SetActive(false); mini.gameObject.SetActive(false);
        if(placement!=null)StopCoroutine(placement);placement=null;placementKey=null;
        item.transform.SetParent(overlay,false);item.color=Color.white;SetIntroBackdrop(false);
        CloseConfirmation(); ShowItem(null, false); visitorPresent=false; walker.gameObject.SetActive(false); FocusTargets(Array.Empty<RectTransform>());
        if(wasReminding)ReminderFinished?.Invoke();
    }
    void CancelKeyboardConfirmation()
    {
        if(confirmation==null||!confirmation.name.StartsWith("StorySkipConfirmation"))return;
        var cancel=Node<Button>(confirmation,"SkipCancel");if(cancel!=null&&cancel.gameObject.activeInHierarchy&&cancel.IsInteractable())cancel.onClick.Invoke();
    }
    void CloseConfirmation() { if(parkedSkipPrompt!=null){parkedSkipPrompt.gameObject.SetActive(false);Destroy(parkedSkipPrompt.gameObject);parkedSkipPrompt=null;} if (confirmation != null) { confirmation.gameObject.SetActive(false); Destroy(confirmation.gameObject); confirmation = null; } }
    // Separate authored purchase view: a full black raycast blocker leaves only Buy actionable.
    public void ShowPurchasePreview(Sprite sword)
    {
        ShowItem(sword,true);Rect(item.rectTransform,new(.30f,.405f),new(.70f,.875f));
    }
    public void Purchase(Sprite sword, string amount, Action action)
    {
        CloseConfirmation(); ShowItem(null,false);
        confirmation=Load("StorySwordPurchase");confirmation.SetAsLastSibling();
        var image=Node<Image>(confirmation,"PurchaseSword");image.sprite=sword;image.preserveAspect=true;image.raycastTarget=false;
        Node<TMP_Text>(confirmation,"Question").text="반짝이는 철 검 · "+amount;
        var button=Node<Button>(confirmation,"SkipConfirm");button.onClick.RemoveAllListeners();
        button.onClick.AddListener(()=>{CloseConfirmation();action();});
    }
    public void SetIntroBackdrop(bool black)
    {
        if(!black)Rect(item.rectTransform,new(.42f,.35f),new(.58f,.72f));
        dim.color=black?Color.black:Color.clear;
        // Cancellation also runs during parent deactivation, when hierarchy reordering is invalid.
        if(isActiveAndEnabled && gameObject.activeInHierarchy)dim.transform.SetAsFirstSibling();
    }
    // Purchase art and its black backdrop share the same fade clock and cleanup boundary.
    public IEnumerator LeavePurchasePresentation()
    {
        Color original=item.color;
        for(float t=0;t<.75f;t+=Time.unscaledDeltaTime*Speed)
        {float a=1-Mathf.Clamp01(t/.75f);dim.color=new Color(0,0,0,a);item.color=new Color(original.r,original.g,original.b,a);yield return null;}
        ShowItem(null,false);item.color=Color.white;SetIntroBackdrop(false);
    }
    public void ShowCraftedSword(Sprite sprite)
    {
        ShowItem(null,false);item.transform.SetParent(overlay,false);item.color=Color.white;
        Rect(item.rectTransform,new(.43f,.68f),new(.57f,.92f));ShowItem(sprite,true);
    }
    public void Decision(string message, string label, Action action)
    {
        CloseConfirmation(); confirmation = Load("StorySkipConfirmation"); confirmation.SetAsLastSibling();
        Node<TMP_Text>(confirmation, "Question").text = message;
        Node<Button>(confirmation, "SkipCancel").gameObject.SetActive(false);
        var button = Node<Button>(confirmation, "SkipConfirm"); button.GetComponentInChildren<TMP_Text>().text = label;
        button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => { CloseConfirmation(); action(); });
    }
    public IEnumerator Fade(float from, float to, float seconds = .45f, bool white = false)
    {
        dim.transform.SetAsLastSibling();
        for (float t = 0; t < seconds; t += Time.unscaledDeltaTime * Speed)
        { dim.color = new Color(white ? 1 : 0, white ? 1 : 0, white ? 1 : 0, Mathf.Lerp(from, to, t / seconds)); yield return null; }
        dim.color = new Color(white ? 1 : 0, white ? 1 : 0, white ? 1 : 0, to);
        dim.transform.SetAsFirstSibling();
    }
    public IEnumerator Blink() { yield return Fade(1, 0, .35f); yield return Fade(0, 1, .18f); yield return Fade(1, 0, .6f); }
    [SerializeField] TimedSpriteMotionSet passerAnimations;
    Image passerCoin, passerSword;
    bool passerActive, passerCaptured;
    int passerVersion;
    float passerHeight=.27f, passerFeet=.3f;
    bool passerWorldAnchored, passerMovesRight;
    Vector3 passerWorldStart, passerWorldStop, passerWorldExit, passerWorldCoin;
    Sprite previousWalkerSprite; Color previousWalkerColor;
    Vector3 previousWalkerScale; bool previousWalkerAspect;
    Vector2 previousWalkerMin, previousWalkerMax;

    void AnimatePasser(TimedSpriteMotionSet.Motion motion,float clock,float x)
    {
        if(motion!=null && motion.frames.Length>0)
        {
            walker.sprite=motion.At(clock);walker.color=Color.white;walker.preserveAspect=true;
            // The boundary approach and departure each face their own direction, including the held sword.
            var scale=previousWalkerScale;scale.x=Mathf.Abs(scale.x)*(passerAnimations.facesRight==passerMovesRight?1:-1);walker.rectTransform.localScale=scale;
            float aspect=walker.sprite.rect.width/walker.sprite.rect.height;
            float width=passerHeight*overlay.rect.height/Mathf.Max(1,overlay.rect.width)*aspect;
            Rect(walker.rectTransform,new(x+.03f-width*.5f,passerFeet),new(x+.03f+width*.5f,passerFeet+passerHeight));
        }
        else Rect(walker.rectTransform,new(x,passerFeet),new(x+.06f,passerFeet+.27f));
    }
    float PasserScreenX(Vector3 world)
    {
        var camera=Camera.main;
        if(camera!=null&&RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay,camera.WorldToScreenPoint(world),null,out var local))
            return (local.x-overlay.rect.xMin)/Mathf.Max(1,overlay.rect.width);
        return .5f;
    }
    float PasserPath(Vector3 from,Vector3 to,float progress,float fallbackFrom,float fallbackTo) =>
        passerWorldAnchored?PasserScreenX(Vector3.Lerp(from,to,progress))-.03f:Mathf.Lerp(fallbackFrom,fallbackTo,progress);
    // Presentation cleanup owns only this passer's art. Money remains exclusively in StoryTutorial.
    void CleanupPasser()
    {
        passerVersion++;passerActive=false;
        if(passerCoin!=null){Destroy(passerCoin.gameObject);passerCoin=null;}
        if(passerSword!=null){Destroy(passerSword.gameObject);passerSword=null;}
        if(passerCaptured && walker!=null)
        {
            walker.sprite=previousWalkerSprite;walker.color=previousWalkerColor;walker.preserveAspect=previousWalkerAspect;
            walker.rectTransform.localScale=previousWalkerScale;
            Rect(walker.rectTransform,previousWalkerMin,previousWalkerMax);walker.gameObject.SetActive(false);
        }
        passerCaptured=false;passerWorldAnchored=false;
    }
    // NPC has idle/walk only; the existing coin cue is deliberately not called a throw animation.
    public IEnumerator Passer(Sprite sword)
    {
        CleanupPasser();int version=passerVersion;passerActive=true;passerCaptured=true;
        previousWalkerSprite=walker.sprite;previousWalkerColor=walker.color;previousWalkerAspect=walker.preserveAspect;
        previousWalkerScale=walker.rectTransform.localScale;previousWalkerMin=walker.rectTransform.anchorMin;previousWalkerMax=walker.rectTransform.anchorMax;
        passerAnimations ??= Resources.Load<TimedSpriteMotionSet>("Story/PasserAnimations");
        passerHeight=.27f;passerFeet=.3f;
        var player=FindFirstObjectByType<PlayerAssimilate>();var camera=Camera.main;
        var actor=player!=null?player.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.enabled&&r.sprite!=null).OrderByDescending(r=>r.bounds.size.y).FirstOrDefault():null;
        if(actor!=null&&camera!=null&&Screen.height>0)
        {
            passerHeight=Mathf.Clamp(Mathf.Abs(camera.WorldToScreenPoint(actor.bounds.max).y-camera.WorldToScreenPoint(actor.bounds.min).y)/Screen.height,.06f,.27f);
            // Cache the visible actor's foot height once; the NPC/coin follow the real stage floor without per-frame jitter.
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay,camera.WorldToScreenPoint(actor.bounds.min),null,out var feet))
                passerFeet=Mathf.Clamp((feet.y-overlay.rect.yMin)/Mathf.Max(1,overlay.rect.height),.1f,.8f);
        }
        passerWorldAnchored=false;passerMovesRight=false;
        var forest=FindObjectsByType<StoryForestStage>(FindObjectsInactive.Include,FindObjectsSortMode.None)
            .FirstOrDefault(f=>f.Presenting&&f.npcEntrance!=null);
        if(forest!=null&&player!=null&&camera!=null)
        {
            passerWorldAnchored=true;passerWorldStart=forest.npcEntrance.position;
            passerWorldStop=player.transform.position+Vector3.right*1.25f;
            passerWorldExit=forest.npcEntrance.position+Vector3.left*1.75f;
            passerWorldCoin=player.transform.position;
            passerMovesRight=passerWorldStop.x>passerWorldStart.x;
        }
        walker.gameObject.SetActive(true);ShowItem(sword,true);
        try
        {
            for(float t=0;t<1.8f && version==passerVersion;t+=Time.unscaledDeltaTime*Speed)
            {AnimatePasser(passerAnimations?.walk,t,PasserPath(passerWorldStart,passerWorldStop,t/1.8f,1.1f,.52f));yield return null;}
            if(version!=passerVersion)yield break;
            AnimatePasser(passerAnimations?.idle,0,PasserPath(passerWorldStart,passerWorldStop,1,1.1f,.52f));ShowItem(null,false);
            var held=new GameObject("Passer Held Sword",typeof(RectTransform),typeof(Image));held.transform.SetParent(walker.transform,false);
            passerSword=held.GetComponent<Image>();passerSword.sprite=sword;passerSword.preserveAspect=true;passerSword.raycastTarget=false;
            Rect(passerSword.rectTransform,new(.62f,.09f),new(1.0f,.43f));
            var coin=new GameObject("Intro Coin Placeholder",typeof(RectTransform),typeof(Image));coin.transform.SetParent(overlay,false);
            passerCoin=coin.GetComponent<Image>();passerCoin.color=BlacksmithView.Gold;passerCoin.raycastTarget=false;
            // With no throw tag, release the replaceable coin cue from the visible hand during idle.
            var local=overlay.InverseTransformPoint(walker.rectTransform.TransformPoint(new Vector3(walker.rectTransform.rect.xMin+walker.rectTransform.rect.width*.8f,walker.rectTransform.rect.yMin+walker.rectTransform.rect.height*.27f,0)));
            var hand=new Vector2((local.x-overlay.rect.xMin)/overlay.rect.width,(local.y-overlay.rect.yMin)/overlay.rect.height);
            Sound("coin");
            for(float t=0;t<.55f && version==passerVersion;t+=Time.unscaledDeltaTime*Speed)
            {
                AnimatePasser(passerAnimations?.idle,t,PasserPath(passerWorldStart,passerWorldStop,1,1.1f,.52f));float progress=t/.55f;
                Vector2 pos=Vector2.Lerp(hand,new(passerWorldAnchored?PasserScreenX(passerWorldCoin):.495f,passerFeet),progress)+Vector2.up*(.1f*4*progress*(1-progress));
                Rect(passerCoin.rectTransform,pos-new Vector2(.005f,0),pos+new Vector2(.005f,.018f));yield return null;
            }
            if(version!=passerVersion)yield break;
            Destroy(passerCoin.gameObject);passerCoin=null;
            if(passerWorldAnchored)passerMovesRight=passerWorldExit.x>passerWorldStop.x;
            for(float t=0;t<1.8f && version==passerVersion;t+=Time.unscaledDeltaTime*Speed)
            {AnimatePasser(passerAnimations?.walk,t,PasserPath(passerWorldStop,passerWorldExit,t/1.8f,.52f,-.15f));yield return null;}
        }
        finally {if(version==passerVersion){CleanupPasser();ShowItem(null,false);}}
    }
    Image visitorItem;
    public void HoldVisitorItem(Sprite sprite)
    {
        if(visitorItem==null)
        {
            var go=new GameObject("VisitorSword",typeof(RectTransform),typeof(Image));go.transform.SetParent(walker.transform,false);
            visitorItem=go.GetComponent<Image>();visitorItem.preserveAspect=true;visitorItem.raycastTarget=false;
            Rect(visitorItem.rectTransform,new(.5f,.1f),new(1.1f,.75f));
        }
        visitorItem.sprite=sprite;visitorItem.gameObject.SetActive(sprite!=null);
    }
    bool visitorPresent;
    Vector3 visitorDoor, visitorStop, visitorPosition;
    float visitorWorldHeight;
    bool visitorFacesRight;
    // Use the visible door, never the interaction trigger (which is offset to its right).
    void PlaceVisitor()
    {
        var camera=Camera.main;
        if(!visitorPresent||camera==null||walker.sprite==null)return;
        var feet=camera.WorldToScreenPoint(visitorPosition);
        var top=camera.WorldToScreenPoint(visitorPosition+Vector3.up*visitorWorldHeight);
        if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay,feet,null,out var local))return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay,top,null,out var localTop);
        float height=Mathf.Abs(localTop.y-local.y),width=height*walker.sprite.rect.width/walker.sprite.rect.height;
        var rect=walker.rectTransform;rect.anchorMin=rect.anchorMax=overlay.pivot;rect.pivot=new(.5f,0);
        rect.anchoredPosition=local;rect.sizeDelta=new(width,height);
        var scale=rect.localScale;scale.x=Mathf.Abs(scale.x)*(visitorFacesRight?1:-1);rect.localScale=scale;
    }
    public IEnumerator Visitor(bool entering)
    {
        var npcMotion=Resources.Load<TimedSpriteMotionSet>("Story/PasserAnimations");
        if(entering)
        {
            var interior=SmithyInterior.Instance;
            var campaign=CampaignController.Instance;
            var actor=FindFirstObjectByType<PlayerAssimilate>()?.GetComponentsInChildren<SpriteRenderer>()
                .Where(r=>r.enabled&&r.sprite!=null).OrderByDescending(r=>r.bounds.size.y).FirstOrDefault();
            visitorWorldHeight=actor!=null?actor.bounds.size.y:2;
            if(interior!=null&&interior.Inside)
            {
                var doorArt=interior.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault(r=>r.name=="ExitDoor");
                visitorDoor=doorArt!=null?new Vector3(doorArt.bounds.center.x,doorArt.bounds.min.y,doorArt.transform.position.z):interior.exit.position-Vector3.up*visitorWorldHeight*.5f;
                visitorStop=visitorDoor+Vector3.right*2.5f;
            }
            else
            {
                // The ending can be triggered from town: use the exterior smithy entrance.
                var entrance=campaign?.townScene?.blackSmith;
                visitorDoor=entrance!=null?entrance.position:transform.position;
                if(actor!=null)visitorDoor.y=actor.bounds.min.y;
                visitorStop=visitorDoor+Vector3.right*2.5f;
            }
        }
        visitorPresent=true;walker.sprite=npcIdle;walker.color=npcIdle!=null?Color.white:Color.gray;walker.preserveAspect=true;
        walker.gameObject.SetActive(true);
        Vector3 from=entering?visitorDoor:visitorStop,to=entering?visitorStop:visitorDoor;
        bool movesRight=to.x>from.x;visitorFacesRight=npcMotion==null||npcMotion.facesRight==movesRight;
        for(float t=0;t<.8f;t+=Time.unscaledDeltaTime*Speed)
        {
            if(npcMotion?.walk.At(t) is Sprite frame)walker.sprite=frame;
            visitorPosition=Vector3.Lerp(from,to,Mathf.SmoothStep(0,1,t/.8f));PlaceVisitor();yield return null;
        }
        walker.sprite=npcIdle;visitorPosition=to;PlaceVisitor();
        if(!entering){visitorPresent=false;HoldVisitorItem(null);walker.gameObject.SetActive(false);}
    }
    void ClearLogo()
    {
        logoVisible = false;
        if (logoBackdrop != null)
        {
            logoBackdrop.gameObject.SetActive(false);
            Destroy(logoBackdrop.gameObject);
        }
        logoBackdrop = null;
        RefreshGold();
    }
    public IEnumerator Logo()
    {
        logoVisible = true;
        RefreshGold();
        RectTransform bg = null;
        try
        {
            // The editable logo prefab owns the imported Sprite; no runtime texture is allocated.
            bg = logoBackdrop = Load("StoryLogo");
            GameSound.OverrideMusic(this,"Dance",10);
            for (float t = 0; t < 2.5f; t += Time.unscaledDeltaTime * Speed) yield return null;
        }
        finally
        {
            // Cancellation/reuse may already have released this particular logo.
            if (ReferenceEquals(logoBackdrop, bg))
            {
                GameSound.OverrideMusic(this,null);
                ClearLogo();
            }
        }
    }
    IReadOnlyList<RectTransform> focusedTargets=Array.Empty<RectTransform>();
    public void AllowPresentationTargets(IEnumerable<Transform> targets)
    {GameUIController.SetTutorialTargets(this,targets.Concat(focusedTargets).Concat(new Transform[]{mini,dialogue,skip.transform}));}
    public void Focus(RectTransform target) => FocusTargets(target == null ? Array.Empty<RectTransform>() : new[] { target });
    public void FocusTargets(IReadOnlyList<RectTransform> targets)
    {
        focusedTargets=targets;
        holes.Clear();
        if(ConfirmationOpen){foreach(var mask in masks)mask.gameObject.SetActive(false);foreach(var frame in frames)frame.gameObject.SetActive(false);return;}
        foreach (var target in targets)
        {
            if (!StoryTutorial.IsVisibleControl(target) || Speaking && !miniMode) continue;
            target.GetWorldCorners(corners); var c=target.GetComponentInParent<Canvas>();
            var camera=c!=null&&c.renderMode!=RenderMode.ScreenSpaceOverlay?c.worldCamera:null;
            Vector2 lo=RectTransformUtility.WorldToScreenPoint(camera,corners[0]), hi=RectTransformUtility.WorldToScreenPoint(camera,corners[2]);
            holes.Add(UnityEngine.Rect.MinMaxRect(Mathf.Clamp01((lo.x-7)/Screen.width),Mathf.Clamp01((lo.y-7)/Screen.height),
                Mathf.Clamp01((hi.x+7)/Screen.width),Mathf.Clamp01((hi.y+7)/Screen.height)));
        }
        foreach(var target in targets)
        {
            if(target==null||!target.name.StartsWith("Tool_",StringComparison.Ordinal))continue;
            var label=FindObjectsByType<TMP_Text>(FindObjectsSortMode.None).FirstOrDefault(t=>t.name=="ToolName_"+target.name.Substring(5)&&StoryTutorial.IsVisibleControl(t));
            if(label!=null)holes.Add(ScreenRect(label.rectTransform,.006f));
        }
        // Partition the shade around all holes; targets themselves remain in their authored hierarchy.
        var xs=new List<float>{0,1}; var ys=new List<float>{0,1};
        foreach(var h in holes){xs.Add(h.xMin);xs.Add(h.xMax);ys.Add(h.yMin);ys.Add(h.yMax);}
        xs=xs.Distinct().OrderBy(v=>v).ToList();ys=ys.Distinct().OrderBy(v=>v).ToList();int used=0;
        if(holes.Count>0) for(int x=0;x<xs.Count-1;x++) for(int y=0;y<ys.Count-1;y++)
        {
            var lo=new Vector2(xs[x],ys[y]);var hi=new Vector2(xs[x+1],ys[y+1]);
            if(hi.x-lo.x<.001f||hi.y-lo.y<.001f||holes.Any(h=>h.Contains((lo+hi)*.5f)))continue;
            if(used==masks.Count){var m=Instantiate(masks[0],overlay);masks.Add(m);}
            masks[used].gameObject.SetActive(true);Rect(masks[used++].rectTransform,lo,hi);
        }
        for(int i=used;i<masks.Count;i++)masks[i].gameObject.SetActive(false);
        for(int i=0;i<holes.Count;i++)
        {
            if(i==frames.Count)frames.Add(Load("StoryHighlight",overlay));
            frames[i].gameObject.SetActive(true);Rect(frames[i],holes[i].min,holes[i].max);
            foreach(var g in frames[i].GetComponentsInChildren<Graphic>())g.raycastTarget=false;
        }
        for(int i=holes.Count;i<frames.Count;i++)frames[i].gameObject.SetActive(false);
        if(miniMode&&mini.gameObject.activeSelf)PlaceMiniDialogue(targets);
        if(isActiveAndEnabled && gameObject.activeInHierarchy)
        {
            task.SetAsLastSibling();warning.transform.SetAsLastSibling();skip.transform.SetAsLastSibling();
            dialogue.SetAsLastSibling();mini.SetAsLastSibling();if(confirmation!=null)confirmation.SetAsLastSibling();
        }
    }
    static UnityEngine.Rect ScreenRect(RectTransform target,float padding=0)
    {
        var points=new Vector3[4];target.GetWorldCorners(points);var canvas=target.GetComponentInParent<Canvas>();
        var cam=canvas!=null&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;
        Vector2 lo=RectTransformUtility.WorldToScreenPoint(cam,points[0]),hi=RectTransformUtility.WorldToScreenPoint(cam,points[2]);
        return UnityEngine.Rect.MinMaxRect(lo.x/Screen.width-padding,lo.y/Screen.height-padding,hi.x/Screen.width+padding,hi.y/Screen.height+padding);
    }
    // TMP rectangles often include large blank areas. Reserve the rendered glyph bounds instead.
    static UnityEngine.Rect ScreenTextRect(TMP_Text text,float padding)
    {
        text.ForceMeshUpdate();var bounds=text.textBounds;
        var canvas=text.GetComponentInParent<Canvas>();
        var camera=canvas!=null&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;
        var lo=RectTransformUtility.WorldToScreenPoint(camera,text.transform.TransformPoint(bounds.min));
        var hi=RectTransformUtility.WorldToScreenPoint(camera,text.transform.TransformPoint(bounds.max));
        return UnityEngine.Rect.MinMaxRect(lo.x/Screen.width-padding,lo.y/Screen.height-padding,hi.x/Screen.width+padding,hi.y/Screen.height+padding);
    }
    // Read the actual gameplay controls once per stage. Never move the underlying crafting UI.
    // Required action buttons and instruction text reserve space; unrelated inactive tools may be covered.
    void PlaceMiniDialogue(IReadOnlyList<RectTransform> targets)
    {
        var shop=FindFirstObjectByType<BlacksmithController>();
        bool crafting=shop!=null&&StoryTutorial.IsVisibleControl(shop);
        string key=string.Join(",",targets.Where(t=>t!=null).Select(t=>t.GetInstanceID()))+":"+objective.text+":"+Screen.width+":"+Screen.height+":"+(crafting?shop.State.ToString():"world")+":"+Screen.safeArea+":"+ReleasePreferences.LargeText;
        if(key==placementKey)return;placementKey=key;
        var avoid=new List<UnityEngine.Rect>();
        if(crafting)
            foreach(var rect in shop.view.overlay.GetComponentsInChildren<RectTransform>())
                if(rect.gameObject.activeInHierarchy&&(rect.name=="Minigame"||rect.name=="ResultPanel"||rect.name=="FirstDiscovery"||rect.name=="RecipeDetail"))avoid.Add(ScreenRect(rect,.015f));
        var top=GameUIController.TopPresentation;
        if(top!=null)
            foreach(var rect in top.GetComponentsInChildren<RectTransform>())
                if(rect.gameObject.activeInHierarchy&&(rect.name.Contains("Preview")||rect.name=="OfferingInfo"||rect.name=="Details"||rect.name=="RecipeDescription"||rect.name=="RecipeTip"||rect.name=="SelectionDetails"||top.screenKey=="Offering"&&rect.name=="Paper"))avoid.Add(ScreenRect(rect,.012f));

        foreach(var target in targets)if(target!=null&&StoryTutorial.IsVisibleControl(target))avoid.Add(ScreenRect(target,.01f));
        foreach(var t in FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
        {
            if(t.transform.IsChildOf(transform)||string.IsNullOrWhiteSpace(t.text)||!StoryTutorial.IsVisibleControl(t))continue;
            var button=t.GetComponentInParent<Button>();
            if(button!=null&&!button.IsInteractable())continue;
            if(crafting&&t.name.StartsWith("ToolName_",StringComparison.Ordinal)&&
                t.name!="ToolName_"+(int)shop.CurrentTool&&
                !targets.Any(target=>target!=null&&target.name.StartsWith("Tool_",StringComparison.Ordinal)&&t.name=="ToolName_"+target.name.Substring(5)))continue;
            avoid.Add(ScreenTextRect(t,.006f));
        }
        foreach(var b in FindObjectsByType<Button>(FindObjectsSortMode.None))
            if(!b.transform.IsChildOf(transform)&&StoryTutorial.IsVisibleControl(b)&&
                (b.IsInteractable()||b.name=="StartCraft"||b.name=="OpenMaterials"))avoid.Add(ScreenRect((RectTransform)b.transform,.007f));
        // HUD의 아이콘·프레임도 피한다. 글자 영역만 예약하면 패널과 대사가 겹칠 수 있다.
        foreach(var image in FindObjectsByType<Image>(FindObjectsSortMode.None))
        {
            if(image.transform.IsChildOf(transform)||!StoryTutorial.IsVisibleControl(image)||image.color.a<.1f)continue;
            var area=ScreenRect(image.rectTransform,.006f);
            if(area.width>0 && area.height>0 && area.width*area.height<.12f)avoid.Add(area);
        }
        if(skip.gameObject.activeInHierarchy)avoid.Add(ScreenRect((RectTransform)skip.transform,.008f));
        if(goldPlate!=null&&goldPlate.gameObject.activeInHierarchy)avoid.Add(ScreenRect(goldPlate,.008f));
        if(crafting&&task.gameObject.activeSelf&&objective.gameObject.activeSelf)
        {
            var goalSize=new Vector2(.34f,.08f);
            Vector2[] goalPositions={new(.39f,.70f),new(.33f,.65f),new(.33f,.78f),new(.33f,.34f),new(.025f,.72f),new(.64f,.72f)};
            Vector2 goal=LeastOverlap(goalPositions,goalSize,avoid);
            Rect(task,goal,goal+goalSize);
        }
        else Rect(task,objectiveMin,objectiveMax);
        if(task.gameObject.activeSelf)avoid.Add(ScreenRect(task,.008f));
        var candidates=new List<Vector2>();
        for(float y=.035f;y<=.75f;y+=.08f)
            for(float x=.02f;x<=.78f;x+=.08f)candidates.Add(new Vector2(x,y));
        var size=miniPreferredSize;Vector2 chosen=LeastOverlap(candidates,size,avoid);
        if(placement!=null)StopCoroutine(placement);placement=StartCoroutine(MoveMiniDialogue(chosen,size));
    }
    Vector2 LeastOverlap(IReadOnlyList<Vector2> positions,Vector2 size,IReadOnlyList<UnityEngine.Rect> avoid)
    {
        float best=float.PositiveInfinity;Vector2 chosen=positions[0];
        foreach(var candidate in positions)
        {
            var p=new Vector2(Mathf.Clamp(candidate.x,.02f,1-size.x-.02f),Mathf.Clamp(candidate.y,.02f,1-size.y-.02f));
            var box=new UnityEngine.Rect(p,size);float score=0;
            foreach(var r in avoid){float w=Mathf.Max(0,Mathf.Min(box.xMax,r.xMax)-Mathf.Max(box.xMin,r.xMin));float h=Mathf.Max(0,Mathf.Min(box.yMax,r.yMax)-Mathf.Max(box.yMin,r.yMin));score+=w*h;}
            // The mandatory action and its label always take precedence over unrelated controls.
            foreach(var r in holes)if(box.Overlaps(r))score+=10;
            if(score<best){best=score;chosen=p;}
        }
        return chosen;
    }
    IEnumerator MoveMiniDialogue(Vector2 to,Vector2 size)
    {
        // 목표를 가로지르는 이동 연출 없이 안전한 위치에 바로 배치한다.
        Rect(mini,to,to+size);
        var tail=Node<RectTransform>(mini,"BubbleTail");tail.gameObject.SetActive(holes.Count>0);
        if(holes.Count>0){var h=holes[0];float y=Mathf.Clamp((h.center.y-to.y)/size.y,.12f,.78f);if(h.center.x<to.x)Rect(tail,new(-.016f,y),new(.002f,y+.08f));else Rect(tail,new(.998f,y),new(1.016f,y+.08f));}
        placement=null;
        yield break;
    }
    static void Rect(RectTransform rect,Vector2 lo,Vector2 hi){rect.anchorMin=lo;rect.anchorMax=hi;rect.offsetMin=rect.offsetMax=Vector2.zero;}
    public void Sound(string kind)
    {
        string cue=kind=="hum"?"Hum":kind=="reveal"?"HumLong":kind=="coin"?"Coin":kind=="money"?"Money":kind=="wood"?"Knife":kind=="door"?"Door":kind=="knock"?"Knock":"Voice";
        GameSound.Play(cue,this,tail:kind=="coin");
    }
    // Read the normal economy only: presentation must never credit, debit or save money.
    BlacksmithController goldShop;
    void RefreshGold()
    {
        if (goldPlate == null) return;
        var shop=SmithingLoop.Instance?.InShop==true?(goldShop!=null?goldShop:goldShop=FindFirstObjectByType<BlacksmithController>()):null;
        bool covered=shop!=null&&(shop.State==ScreenState.Recipes||shop.State==ScreenState.Codex||shop.State==ScreenState.Result||shop.IsSleeping);
        var top=GameUIController.TopPresentation;
        covered|=top!=null&&top.owner!=this&&SmithingLoop.Instance?.InShop!=true;
        goldPlate.gameObject.SetActive(isActiveAndEnabled && StoryTutorial.Guiding && !logoVisible && !covered && !ConfirmationOpen);
        if (!goldPlate.gameObject.activeSelf) return;
        var loop = SmithingLoop.Instance;
        if (loop != null && loop.Initialized)
        {
            // The field has no CampaignController; GoldText itself only reads the shared money state.
            var economy = CampaignController.Instance?.Economy ?? new CampaignEconomy(loop.Campaign, loop.SmithData, loop.Catalog, null);
            tutorialGold.text = "● " + economy.GoldText + " G";
            // Keep the workshop Back button visible; world scenes retain the ordinary town HUD position.
            Rect(goldPlate, loop.InShop ? new(.59f,.93f) : new(.02f,.845f), loop.InShop ? new(.77f,.995f) : new(.20f,.905f));
        }
        // Purchase/skip popups, focus masks and fades may reorder siblings during the same frame.
        // The common modal owner controls the order; currency never jumps above a decision.
    }
    // Check the cached world listener after scene/room transitions in this frame.
    // Keep its reference while disabled: returning to town must disable our fallback immediately.
    void LateUpdate()
    {
        PlaceVisitor();
        var top=GameUIController.TopPresentation;
        bool foreign=top!=null&&top.priority>=23000&&top.owner!=this;
        GetComponent<Canvas>().sortingOrder=GameUIController.PresentationOverlayOrder-(Speaking||foreign||ConfirmationOpen?10:0);
        if(skip!=null){Rect((RectTransform)skip.transform,new(.90f,.94f),new(.982f,.99f));var label=skip.GetComponentInChildren<TMP_Text>();if(label!=null)label.fontSize=24;}
        RefreshDialogueClickSurfaces();
        RefreshGold();
        // 크기 조정 후 안전 영역을 검사한다. 배경·컷신 그림·월드 좌표는 이동하지 않는다.
        TownFieldUiStyle.ClampWindow(dialogue);TownFieldUiStyle.ClampWindow(mini);
        TownFieldUiStyle.ClampWindow(task);
        if(confirmation!=null&&!confirmation.name.StartsWith("StorySwordPurchase",StringComparison.Ordinal))TownFieldUiStyle.ClampWindow(confirmation);
        if(skip!=null&&skip.gameObject.activeInHierarchy)TownFieldUiStyle.ClampWindow((RectTransform)skip.transform);
        if (storyListener == null) return;
        if (worldListener == null || Time.unscaledTime >= nextListenerCheck)
        {
            nextListenerCheck = Time.unscaledTime + .25f;
            var available = FindObjectsByType<AudioListener>(FindObjectsSortMode.None)
                .FirstOrDefault(l => l != storyListener && l.isActiveAndEnabled);
            if (available != null) worldListener = available;
        }
        storyListener.enabled = worldListener == null || !worldListener.isActiveAndEnabled;
    }
    void Update()
    {
        // Dialogue owns its own buttons; action waits leave the actual workshop controls navigable.
        if(overlay!=null){var navigation=UIKeyboardSurface.Bind(this,miniMode?mini:dialogue,"StoryDialogue",CancelKeyboardConfirmation,5000,false);navigation.enabled=Speaking;UIKeyboardNavigation.AttachRoot(navigation,skip.transform);
            if(ConfirmationOpen)UIKeyboardSurface.Bind(this,confirmation,"Story/"+confirmation.name,CancelKeyboardConfirmation,30000,false);
            if(!navigation.enabled&&skip!=null&&skip.gameObject.activeInHierarchy){var parent=UIKeyboardNavigation.Active;if(parent!=null&&parent!=navigation&&parent.priority<5000)UIKeyboardNavigation.AttachRoot(parent,skip.transform);}}

        if(warningUntil<Time.unscaledTime)warning.text="";
        presentationClock += Time.unscaledDeltaTime * Speed;
        foreach(var frame in frames)if(frame.gameObject.activeSelf)
            foreach(var image in frame.GetComponentsInChildren<Image>())if(image.transform!=frame)
                image.color=new Color(.76f,.62f,.37f,.68f+.20f*Mathf.Sin(presentationClock*2));
        // The reminder deadline continues behind a confirmation popup too.
        if(reminding&&reminderCompletedAt>=0&&Time.realtimeSinceStartupAsDouble-reminderCompletedAt>=ReminderSeconds)
        {
            RestoreReminder();
            return; // A coincident Space must not advance the restored dialogue in this frame.
        }
        if(!Speaking||ConfirmationOpen||!reminding&&(lines==null||line>=lines.Length))return;
        glyphClock-=Time.unscaledDeltaTime*Speed;
        while(glyphClock<=0&&visible<Body.textInfo.characterCount)
        {
            char ch=Body.textInfo.characterInfo[visible].character;visible++;Body.maxVisibleCharacters=visible;
            bool punctuation=".,!?…:;".IndexOf(ch)>=0;
            glyphClock+=punctuation?.14f:1f/30;
            if(!char.IsWhiteSpace(ch)&&!punctuation&&visible%3==0)Sound("voice");
        }
        bool complete=visible>=Body.textInfo.characterCount;
        Next.gameObject.SetActive(true);
        if (reminding)
        {
            // Two real seconds after typing completes; pause and held-S do not shorten the deadline.
            if (complete && reminderCompletedAt < 0) reminderCompletedAt=Time.realtimeSinceStartupAsDouble;
            if (reminderCompletedAt >= 0 && Time.realtimeSinceStartupAsDouble-reminderCompletedAt >= ReminderSeconds) RestoreReminder();
            else if(ReleaseInput.Pressed("DialogueAdvance"))Advance();
            return;
        }
        if(ReleaseInput.Pressed("DialogueAdvance"))Advance();
        else if(complete&&Speed>1&&!reminding){finishedClock+=Time.unscaledDeltaTime*Speed;if(finishedClock>.8f)Advance();}
    }
    void RefreshHints()
    {foreach(var label in keyHints)if(label!=null)label.text=ReleaseInput.Label("DialogueAdvance")+"로 계속";if(objective!=null)objective.text=ReleaseInput.Hints(objectiveSource);}
    void OnEnable(){ReleaseInput.Changed+=RefreshHints;UnityEngine.SceneManagement.SceneManager.activeSceneChanged+=OnSceneChanged;RefreshGold();}
    void OnSceneChanged(UnityEngine.SceneManagement.Scene previous,UnityEngine.SceneManagement.Scene next)
    { if(reminding)RestoreReminder(); }
    void OnDisable()
    {
        GameSound.Stop(this); GameSound.OverrideMusic(this,null);
        ClearLogo();
        GameUIController.SetTutorialTargets(this,Array.Empty<Transform>());
        CleanupPasser();
        ReleaseInput.Changed-=RefreshHints;
        UnityEngine.SceneManagement.SceneManager.activeSceneChanged-=OnSceneChanged;
        if(reminding&&dialogue!=null)RestoreReminder();
        reminderCompletedAt=-1;
    }
    void OnDestroy(){CleanupPasser();reminderCompletedAt=-1;}
}
// The shade consumes input, but only actual, usable controls can request a reminder.
public sealed class StoryBlockedControl : MonoBehaviour, IPointerClickHandler, IPointerDownHandler,
    IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    readonly List<RaycastResult> hits = new();
    static readonly List<RaycastResult> worldHits = new();
    static PointerEventData worldPointer;
    static EventSystem worldSystem;
    // Consume gameplay input under a tutorial shade without asking it to show dialogue.
    public static bool BlocksWorldPointer()
    {
        if (!StoryTutorial.Guiding || Mouse.current == null || EventSystem.current == null) return false;
        if (worldSystem != EventSystem.current) { worldSystem = EventSystem.current; worldPointer = new PointerEventData(worldSystem); }
        worldPointer.Reset(); worldPointer.position = Mouse.current.position.ReadValue();
        worldHits.Clear(); worldSystem.RaycastAll(worldPointer, worldHits);
        foreach (var hit in worldHits)
            if (hit.gameObject != null) return hit.gameObject.GetComponentInParent<StoryBlockedControl>() != null;
        return false;
    }
    GameObject pressTarget;
    InventorySlotView dragTarget;
    Component Resolve(PointerEventData data, bool dropping = false)
    {
        if (EventSystem.current == null) return null;
        hits.Clear(); EventSystem.current.RaycastAll(data, hits);
        foreach (var hit in hits)
        {
            if (hit.gameObject == null || hit.gameObject.GetComponentInParent<StoryBlockedControl>() != null) continue;
            if (!(hit.module is GraphicRaycaster)) return null;
            if (dropping) return hit.gameObject.GetComponentInParent<ItemDropTarget>();
            var slot = hit.gameObject.GetComponentInParent<InventorySlotView>();
            if (slot != null) return slot;
            var selectable = hit.gameObject.GetComponentInParent<Selectable>();
            if (selectable != null) return selectable;
            return hit.gameObject.GetComponentInParent<CraftGestureInput>();
        }
        return null; // A decorative foreground panel never makes a covered button actionable.
    }
    static bool Usable(Component target, PointerEventData data)
    {
        if (target is InventorySlotView slot) return slot.HasClickAction(data.button == PointerEventData.InputButton.Right);
        if (data.button != PointerEventData.InputButton.Left) return false;
        if (target is Button button) return StoryTutorial.IsActionableButton(button);
        if (target is Selectable selectable) return selectable.isActiveAndEnabled && selectable.IsInteractable();
        return target is CraftGestureInput gesture && gesture.isActiveAndEnabled &&
            (gesture.Click != null || gesture.Upstroke != null || gesture.Roundtrip != null);
    }
    static bool Permitted(Component target, PointerEventData data)
    {
        if (target is InventorySlotView slot) return StoryTutorial.CanUseSlot(slot, data.button == PointerEventData.InputButton.Right);
        if (target is Selectable) return StoryTutorial.CanUseButton(target.name);
        return target is CraftGestureInput && StoryTutorial.CanUseAction("craft-gesture");
    }
    public void OnPointerDown(PointerEventData data)
    {
        pressTarget = null;
        if (data.button == PointerEventData.InputButton.Middle) return;
        var target = Resolve(data);
        if (!Usable(target, data)) return;
        // Buttons commit on click; a scroll drag over them must not produce a warning.
        if (!Permitted(target, data)) { if (target is CraftGestureInput || target is Selectable && !(target is Button)) StoryTutorial.Reject(); return; }
        pressTarget = target.gameObject;
        ExecuteEvents.Execute(pressTarget, data, ExecuteEvents.pointerDownHandler);
    }
    public void OnPointerUp(PointerEventData data)
    {
        if (pressTarget != null) ExecuteEvents.Execute(pressTarget, data, ExecuteEvents.pointerUpHandler);
        pressTarget = null;
    }
    public void OnPointerClick(PointerEventData data)
    {
        if (data.dragging || data.button == PointerEventData.InputButton.Middle) return;
        var target = Resolve(data);
        if (!Usable(target, data)) return;
        if (!Permitted(target, data)) { StoryTutorial.Reject(); return; }
        ExecuteEvents.Execute(target.gameObject, data, ExecuteEvents.pointerClickHandler);
    }
    public void OnBeginDrag(PointerEventData data)
    {
        dragTarget = null;
        if (data.button != PointerEventData.InputButton.Left) return;
        var slot = Resolve(data) as InventorySlotView;
        if (slot == null || !slot.HasDragAction) return;
        if (!StoryTutorial.CanUseSlot(slot)) { StoryTutorial.Reject(); return; }
        dragTarget = slot; slot.OnBeginDrag(data);
    }
    public void OnDrag(PointerEventData data)
    {
        if (dragTarget != null) dragTarget.OnDrag(data);
        else if (pressTarget != null) ExecuteEvents.Execute(pressTarget, data, ExecuteEvents.dragHandler);
    }
    public void OnEndDrag(PointerEventData data)
    { if (dragTarget != null) dragTarget.OnEndDrag(data); dragTarget = null; }
    public void OnDrop(PointerEventData data)
    {
        var source = InventorySlotView.Dragging;
        var target = Resolve(data, true) as ItemDropTarget;
        if (target == null || !target.HasDropAction(source)) return;
        if (!StoryTutorial.CanUseDrop(target, source)) { StoryTutorial.Reject(); return; }
        target.OnDrop(data);
    }
    void OnDisable() { pressTarget = null; if (dragTarget != null) dragTarget.CancelDrag(); dragTarget = null; }
}

// Runtime-only behaviour; existing scene/prefab GUIDs remain unchanged.
public sealed class StorySkipOpacity : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField,Range(.3f,1f)] float restingAlpha=.55f;
    [SerializeField,Min(.01f)] float fadeSeconds=.14f;
    CanvasGroup group;bool hovered,selected;
    void Awake(){group=GetComponent<CanvasGroup>();if(group==null)group=gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=true;}
    void OnEnable(){hovered=false;selected=EventSystem.current?.currentSelectedGameObject==gameObject;if(group!=null)group.alpha=selected?1:restingAlpha;}
    void Update(){group.alpha=Mathf.MoveTowards(group.alpha,hovered||selected?1:restingAlpha,Time.unscaledDeltaTime*(1-restingAlpha)/Mathf.Max(.01f,fadeSeconds));}
    public void OnPointerEnter(PointerEventData e)=>hovered=true;
    public void OnPointerExit(PointerEventData e)=>hovered=false;
    public void OnSelect(BaseEventData e)=>selected=true;
    public void OnDeselect(BaseEventData e)=>selected=false;
}

// Runtime-only panel handler: child text/portraits bubble here; buttons keep their own click handler.
public sealed class StoryDialoguePointerAdvance : MonoBehaviour, IPointerClickHandler
{
    public Action Click;
    public void OnPointerClick(PointerEventData data)
    {
        if (data.button == PointerEventData.InputButton.Left && !data.dragging) Click?.Invoke();
    }
}
