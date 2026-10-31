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
    readonly Dictionary<string, AudioClip> sounds = new();
    readonly Vector3[] corners = new Vector3[4];
    readonly List<Rect> holes = new();
    AudioSource audioSource;
    AudioListener storyListener, worldListener;
    float nextListenerCheck;
    Sprite pixelLogo, idle;
    string[] lines;
    readonly Queue<string> pages = new();
    int line, visible;
    float glyphClock, finishedClock, warningUntil, presentationClock;
    bool active, miniMode, skipRequested;
    bool tutorialHud = true;
    bool? knownHammer;
    bool revealed => knownHammer ?? StoryTutorial.HammerRevealed;
    public event Action SkipConfirmed;
    public event Action<string> Cue;
    public bool Speaking => active || reminding;
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
    Vector2 objectiveMin, objectiveMax;
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
        return Instantiate(prefab, parent ?? root, false).transform as RectTransform;
    }
    static T Node<T>(Transform parent, string name) where T : Component =>
        parent.GetComponentsInChildren<T>(true).First(c => c.name == name);
    public void Build(Sprite playerIdle, TMP_FontAsset font, bool tutorialHud = true, bool? hammerKnown = null)
    {
        this.tutorialHud = tutorialHud; knownHammer = hammerKnown;
        idle = playerIdle;
        var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 5000;
        var scaler = gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        gameObject.AddComponent<GraphicRaycaster>(); root = (RectTransform)transform;
        overlay = Load("StoryTutorialOverlay");
        var firstMask = Node<Image>(overlay, "TutorialMask"); firstMask.raycastTarget = true;
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
        foreach (var t in GetComponentsInChildren<TMP_Text>(true)) if (t.font == null && font != null) t.font = font;
        dialogue.gameObject.SetActive(false); mini.gameObject.SetActive(false); task.gameObject.SetActive(false);
        item.gameObject.SetActive(false); walker.gameObject.SetActive(false); skip.gameObject.SetActive(false);
        Node<TMP_Text>(overlay, "ClosureSign").gameObject.SetActive(false);
        audioSource = gameObject.AddComponent<AudioSource>(); audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0; audioSource.ignoreListenerPause = true;
        storyListener = gameObject.AddComponent<AudioListener>(); storyListener.enabled = false;
        // Reuse the town HUD styling; this non-interactive copy survives story masks and room canvases.
        if (tutorialHud)
        {
            var ui = CampaignController.Instance.UI.view;
            goldPlate = ui.Panel("TutorialGoldPlate", root, new(.02f,.91f), new(.20f,.98f));
            goldPlate.GetComponent<Image>().raycastTarget = false;
            tutorialGold = ui.Text("TutorialGold", goldPlate, "", 27, Vector2.zero, Vector2.one);
            if (font != null) tutorialGold.font = font;
            RefreshGold();
        }
        keyHints.AddRange(GetComponentsInChildren<TMP_Text>(true).Where(x=>x.text.Contains("Space로 계속")));
        RefreshHints();SetObjective("");
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
            if (show) { ConfigureMiniDialogue(); miniSpeech.text=value; miniSpeech.ForceMeshUpdate();
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
        var shop=FindFirstObjectByType<BlacksmithController>();
        var size=shop!=null&&StoryTutorial.IsVisibleControl(shop)?new Vector2(.36f,.16f):new Vector2(.46f,.19f);
        var p=Vector2.Min(mini.anchorMin,new Vector2(.98f,.98f)-size);
        Rect(mini,p,p+size);placementKey=null;
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
        reminding=true;miniMode=reminderMiniVisible||miniMode;active=true;
        dialogue.gameObject.SetActive(!miniMode);mini.gameObject.SetActive(miniMode);
        Body.text=revealed?"내 말을 따르렴":"...";
        rightName.text=miniName.text=revealed?"에고 망치":"";
        rightPortrait.gameObject.SetActive(revealed);miniPortrait.gameObject.SetActive(revealed);
        miniPortrait.sprite=null;miniPortrait.color=Color.gray;
        Body.ForceMeshUpdate();visible=0;Body.maxVisibleCharacters=0;
        reminderCompletedAt=-1;glyphClock=finishedClock=0;Next.gameObject.SetActive(true);
    }
    void RestoreReminder()
    {
        reminding=false;reminderCompletedAt=-1;miniMode=reminderMini;active=reminderActive;Body.text=reminderText;Body.ForceMeshUpdate();
        glyphClock=reminderGlyphClock;finishedClock=reminderFinishedClock;
        rightName.text=reminderRightName;miniName.text=reminderMiniName;
        rightPortrait.gameObject.SetActive(reminderRightPortrait);miniPortrait.gameObject.SetActive(reminderMiniPortrait);
        miniPortrait.sprite=reminderMiniSprite;miniPortrait.color=reminderMiniColor;
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
        dialogue.gameObject.SetActive(!small); mini.gameObject.SetActive(small); skip.gameObject.SetActive(maySkip);
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
    void DrawLine()
    {
        string raw = lines[line];
        foreach (string cue in new[] { "stand", "sit", "charge", "collector-sword", "collector-return" })
            if (raw.StartsWith("[" + cue + "]", StringComparison.Ordinal))
            { raw = raw.Substring(cue.Length + 2); Cue?.Invoke(cue); break; }
        if (raw.StartsWith("[reveal]", StringComparison.Ordinal)) { RevealHammer(); raw = raw.Substring(8); Sound("hum"); }
        int colon = raw.IndexOf(':');
        bool other = raw.StartsWith("에고 망치:") || raw.StartsWith("박덕수:") || raw.StartsWith("???:");
        string name = other ? raw.Substring(0, colon) : "주인공";
        string body = other ? raw.Substring(colon + 1).Trim() : raw;
        leftName.text = "주인공"; rightName.text = other ? name : revealed ? "에고 망치" : "";
        leftPortrait.color = other ? new Color(.55f, .55f, .55f) : Color.white;
        rightPortrait.gameObject.SetActive(revealed || name == "박덕수");
        rightPortrait.color = other ? Color.gray : new Color(.28f,.28f,.28f);
        rightName.color = name == "에고 망치" ? new Color(.48f,.78f,.73f) : BlacksmithView.Gold;
        miniPortrait.sprite = other ? null : idle; miniPortrait.preserveAspect = !other;
        miniPortrait.color = other ? Color.gray : Color.white;
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
    void AskSkip()
    {
        if (ConfirmationOpen) return;
        confirmation = Load("StorySkipConfirmation"); confirmation.SetAsLastSibling();
        var yes = Node<Button>(confirmation, "SkipConfirm"); var no = Node<Button>(confirmation, "SkipCancel");
        yes.onClick.RemoveAllListeners(); no.onClick.RemoveAllListeners();
        yes.onClick.AddListener(() => { skipRequested = true; CloseConfirmation(); SkipConfirmed?.Invoke(); });
        no.onClick.AddListener(CloseConfirmation);
    }
    public void CancelPresentation()
    {
        bool wasReminding=reminding;reminding=false;reminderCompletedAt=-1;active = false; dialogue.gameObject.SetActive(false); mini.gameObject.SetActive(false);
        if(placement!=null)StopCoroutine(placement);placement=null;placementKey=null;
        item.transform.SetParent(overlay,false);item.color=Color.white;SetIntroBackdrop(false);
        CloseConfirmation(); ShowItem(null, false); walker.gameObject.SetActive(false); FocusTargets(Array.Empty<RectTransform>());
        if(wasReminding)ReminderFinished?.Invoke();
    }
    void CloseConfirmation() { if (confirmation != null) { confirmation.gameObject.SetActive(false); Destroy(confirmation.gameObject); confirmation = null; } }
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
        dim.color=black?Color.black:Color.clear;dim.transform.SetAsFirstSibling();
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
    // Presentation only: the intro coin is credited once by StoryTutorial after this completes.
    public IEnumerator Passer(Sprite sword)
    {
        walker.gameObject.SetActive(true); ShowItem(sword, true);
        for(float t=0;t<1.8f;t+=Time.unscaledDeltaTime*Speed)
        {float x=Mathf.Lerp(1.1f,.52f,t/1.8f);Rect(walker.rectTransform,new(x,.3f),new(x+.06f,.57f));yield return null;}
        item.transform.SetParent(walker.transform,false);Rect(item.rectTransform,new(.6f,.1f),new(1.5f,.8f));
        // No authored coin sprite exists; this replaceable gold square is a temporary cue.
        var coin=new GameObject("Intro Coin Placeholder",typeof(RectTransform),typeof(Image));coin.transform.SetParent(overlay,false);
        coin.GetComponent<Image>().color=BlacksmithView.Gold;coin.GetComponent<Image>().raycastTarget=false;
        Sound("coin");
        for(float t=0;t<.55f;t+=Time.unscaledDeltaTime*Speed)
        {float y=Mathf.Lerp(.55f,.3f,Mathf.Pow(t/.55f,2));Rect((RectTransform)coin.transform,new(.49f,y),new(.5f,y+.018f));yield return null;}
        Destroy(coin);
        for(float t=0;t<1.8f;t+=Time.unscaledDeltaTime*Speed)
        {float x=Mathf.Lerp(.52f,-.15f,t/1.8f);Rect(walker.rectTransform,new(x,.3f),new(x+.06f,.57f));yield return null;}
        item.transform.SetParent(overlay,false);Rect(item.rectTransform,new(.42f,.35f),new(.58f,.72f));
        ShowItem(null,false);walker.gameObject.SetActive(false);
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
    // Grey visitor uses the existing authored placeholder; inventory stays with the service.
    public IEnumerator Visitor(bool entering)
    {
        walker.gameObject.SetActive(true);float from=entering?1.12f:.7f,to=entering?.7f:1.12f;
        for(float t=0;t<.8f;t+=Time.unscaledDeltaTime*Speed)
        {float x=Mathf.Lerp(from,to,Mathf.SmoothStep(0,1,t/.8f));Rect(walker.rectTransform,new(x,.3f),new(x+.06f,.57f));yield return null;}
        if(!entering){HoldVisitorItem(null);walker.gameObject.SetActive(false);}
    }
    public IEnumerator Logo()
    {
        var bg = Load("StoryTransition"); bg.GetComponent<Image>().color = Color.white;
        var go = new GameObject("Poor smith", typeof(RectTransform), typeof(Image)); go.transform.SetParent(bg, false);
        var image = go.GetComponent<Image>(); image.sprite = PixelLogo(); image.color = new Color(.12f,.09f,.065f); image.preserveAspect = true;
        Rect((RectTransform)go.transform, new(.15f,.37f), new(.85f,.63f));
        for (float t = 0; t < 2.5f; t += Time.unscaledDeltaTime * Speed) yield return null;
        Destroy(bg.gameObject);
    }
    Sprite PixelLogo()
    {
        if (pixelLogo != null) return pixelLogo;
        var glyphs = new Dictionary<char,string[]> {
            ['P']=new[]{"11110","10001","10001","11110","10000","10000","10000"},
            ['o']=new[]{"00000","00000","01110","10001","10001","10001","01110"},
            ['r']=new[]{"00000","00000","10110","11001","10000","10000","10000"},
            ['s']=new[]{"00000","00000","01111","10000","01110","00001","11110"},
            ['m']=new[]{"00000","00000","11010","10101","10101","10101","10101"},
            ['i']=new[]{"00100","00000","01100","00100","00100","00100","01110"},
            ['t']=new[]{"00100","00100","11110","00100","00100","00101","00010"},
            ['h']=new[]{"10000","10000","10110","11001","10001","10001","10001"} };
        const string title="Poor smith";
        var texture=new Texture2D(title.Length*6,7,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};
        var pixels=new Color[texture.width*texture.height];
        for(int c=0;c<title.Length;c++) if(glyphs.TryGetValue(title[c],out var glyph))
            for(int y=0;y<7;y++) for(int x=0;x<5;x++) if(glyph[y][x]=='1') pixels[(6-y)*texture.width+c*6+x]=Color.white;
        texture.SetPixels(pixels);texture.Apply();
        return pixelLogo=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new(.5f,.5f),1);
    }
    public void Focus(RectTransform target) => FocusTargets(target == null ? Array.Empty<RectTransform>() : new[] { target });
    public void FocusTargets(IReadOnlyList<RectTransform> targets)
    {
        holes.Clear();
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
        task.SetAsLastSibling();warning.transform.SetAsLastSibling();skip.transform.SetAsLastSibling();
        dialogue.SetAsLastSibling();mini.SetAsLastSibling();if(confirmation!=null)confirmation.SetAsLastSibling();
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
        string key=string.Join(",",targets.Where(t=>t!=null).Select(t=>t.GetInstanceID()))+":"+objective.text+":"+Screen.width+":"+Screen.height+":"+(crafting?shop.State.ToString():"world");
        if(key==placementKey)return;placementKey=key;
        var avoid=new List<UnityEngine.Rect>();
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
        if(crafting&&task.gameObject.activeSelf&&objective.gameObject.activeSelf)
        {
            var goalSize=new Vector2(.34f,.08f);
            Vector2[] goalPositions={new(.39f,.70f),new(.33f,.65f),new(.33f,.78f),new(.33f,.34f),new(.025f,.72f),new(.64f,.72f)};
            Vector2 goal=LeastOverlap(goalPositions,goalSize,avoid);
            Rect(task,goal,goal+goalSize);
        }
        else Rect(task,objectiveMin,objectiveMax);
        if(task.gameObject.activeSelf)avoid.Add(ScreenRect(task,.008f));
        Vector2[] candidates={new(.02f,.59f),new(.52f,.59f),new(.02f,.035f),new(.52f,.035f),new(.27f,.75f),new(.02f,.43f),new(.52f,.43f)};
        var size=crafting?new Vector2(.36f,.16f):new Vector2(.46f,.19f);Vector2 chosen=LeastOverlap(candidates,size,avoid);
        if(placement!=null)StopCoroutine(placement);placement=StartCoroutine(MoveMiniDialogue(chosen,size));
    }
    Vector2 LeastOverlap(IReadOnlyList<Vector2> positions,Vector2 size,IReadOnlyList<UnityEngine.Rect> avoid)
    {
        float best=float.PositiveInfinity;Vector2 chosen=positions[0];
        foreach(var p in positions)
        {
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
        var from=mini.anchorMin;
        for(float t=0;t<.16f;t+=Time.unscaledDeltaTime)
        {var p=Vector2.Lerp(from,to,Mathf.SmoothStep(0,1,t/.16f));Rect(mini,p,p+size);yield return null;}
        Rect(mini,to,to+size);
        var tail=Node<RectTransform>(mini,"BubbleTail");tail.gameObject.SetActive(holes.Count>0);
        if(holes.Count>0){var h=holes[0];float y=Mathf.Clamp((h.center.y-to.y)/size.y,.12f,.78f);if(h.center.x<to.x)Rect(tail,new(-.016f,y),new(.002f,y+.08f));else Rect(tail,new(.998f,y),new(1.016f,y+.08f));}
        placement=null;
    }
    static void Rect(RectTransform rect,Vector2 lo,Vector2 hi){rect.anchorMin=lo;rect.anchorMax=hi;rect.offsetMin=rect.offsetMax=Vector2.zero;}
    public void Sound(string kind)
    {
        if(!sounds.TryGetValue(kind,out var clip))
        {
            const int rate=22050;var samples=new float[(int)(rate*(kind=="knock"?.5f:kind=="voice"?.035f:.22f))];
            float frequency=kind=="coin"?1200:kind=="wood"?180:kind=="door"?85:kind=="hum"?110:kind=="voice"?320:65;
            for(int i=0;i<samples.Length;i++){float t=(float)i/rate;float envelope=Mathf.Exp(-t*20);
                samples[i]=Mathf.Sin(t*frequency*Mathf.PI*2)*(kind=="knock"?Mathf.Exp(-(t%.16f)*45):envelope)*.12f;}
            clip=AudioClip.Create("StorySfx_"+kind,samples.Length,1,rate,false);clip.SetData(samples,0);sounds.Add(kind,clip);
        }
        var settings=FindFirstObjectByType<SettingsMenuUI.SoundSettingsController>();
        float volume=settings!=null?settings.GetEffectiveVolume(SettingsMenuUI.SoundChannel.Master)*settings.GetEffectiveVolume(SettingsMenuUI.SoundChannel.SFX):1;
        audioSource.PlayOneShot(clip,volume);
    }
    // Read the normal economy only: presentation must never credit, debit or save money.
    void RefreshGold()
    {
        if (goldPlate == null) return;
        goldPlate.gameObject.SetActive(StoryTutorial.Guiding);
        if (!goldPlate.gameObject.activeSelf) return;
        var loop = SmithingLoop.Instance;
        if (loop != null && loop.Initialized)
        {
            // The field has no CampaignController; GoldText itself only reads the shared money state.
            var economy = CampaignController.Instance?.Economy ?? new CampaignEconomy(loop.Campaign, loop.SmithData, loop.Catalog, null);
            tutorialGold.text = "● " + economy.GoldText + " G";
            // Keep the workshop Back button visible; world scenes retain the ordinary town HUD position.
            Rect(goldPlate, loop.InShop ? new(.59f,.93f) : new(.02f,.91f), loop.InShop ? new(.77f,.995f) : new(.20f,.98f));
        }
        // Purchase/skip popups, focus masks and fades may reorder siblings during the same frame.
        goldPlate.SetAsLastSibling();
    }
    // Check the cached world listener after scene/room transitions in this frame.
    // Keep its reference while disabled: returning to town must disable our fallback immediately.
    void LateUpdate()
    {
        RefreshDialogueClickSurfaces();
        RefreshGold();
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
    void OnEnable(){ReleaseInput.Changed+=RefreshHints;UnityEngine.SceneManagement.SceneManager.activeSceneChanged+=OnSceneChanged;}
    void OnSceneChanged(UnityEngine.SceneManagement.Scene previous,UnityEngine.SceneManagement.Scene next)
    { if(reminding)RestoreReminder(); }
    void OnDisable()
    {
        ReleaseInput.Changed-=RefreshHints;
        UnityEngine.SceneManagement.SceneManager.activeSceneChanged-=OnSceneChanged;
        if(reminding&&dialogue!=null)RestoreReminder();
        reminderCompletedAt=-1;
    }
    void OnDestroy(){reminderCompletedAt=-1;foreach(var c in sounds.Values)if(c!=null)Destroy(c);if(pixelLogo!=null){Destroy(pixelLogo.texture);Destroy(pixelLogo);}}
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
