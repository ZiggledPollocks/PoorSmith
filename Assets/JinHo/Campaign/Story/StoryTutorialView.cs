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
    TMP_Text leftName, rightName, speech, miniName, miniSpeech, objective, warning;
    Button advance, miniAdvance, skip;
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
    bool active, miniMode, skipRequested, revealed;
    public event Action SkipConfirmed;
    public event Action<string> Cue;
    public bool Speaking => active;
    public bool ConfirmationOpen => confirmation != null;
    public float Speed => ConfirmationOpen ? 0 : Keyboard.current?.sKey.isPressed == true ? 2 : 1;
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
    public void Build(Sprite playerIdle, TMP_FontAsset font)
    {
        idle = playerIdle;
        var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 5000;
        var scaler = gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        gameObject.AddComponent<GraphicRaycaster>(); root = (RectTransform)transform;
        overlay = Load("StoryTutorialOverlay");
        var firstMask = Node<Image>(overlay, "TutorialMask"); firstMask.raycastTarget = true;
        firstMask.gameObject.AddComponent<StoryBlockedControl>(); firstMask.gameObject.SetActive(false); masks.Add(firstMask);
        task = (RectTransform)Node<Image>(overlay, "TutorialObjective").transform;
        objective = Node<TMP_Text>(task, "Objective"); warning = Node<TMP_Text>(overlay, "FollowTeacher");
        skip = Node<Button>(overlay, "StorySkip"); skip.onClick.RemoveAllListeners(); skip.onClick.AddListener(AskSkip);
        skip.GetComponentInChildren<TMP_Text>().text = "스킵";
        item = Node<Image>(overlay, "StoryItem"); walker = Node<Image>(overlay, "PassingPerson");
        item.preserveAspect = true;
        dialogue = Load("StoryDialogue"); mini = Load("StoryMiniDialogue");
        leftPortrait = Node<Image>(dialogue, "PlayerPortrait"); leftPortrait.sprite = idle;
        rightPortrait = Node<Image>(dialogue, "OtherCharacter");
        leftName = Node<TMP_Text>(dialogue, "SpeakerLeft"); rightName = Node<TMP_Text>(dialogue, "SpeakerRight");
        speech = Node<TMP_Text>(dialogue, "Speech"); advance = Node<Button>(dialogue, "StoryNext");
        miniPortrait = Node<Image>(mini, "MiniPortrait"); miniName = Node<TMP_Text>(mini, "MiniName");
        miniSpeech = Node<TMP_Text>(mini, "MiniSpeech"); miniAdvance = Node<Button>(mini, "MiniNext");
        foreach (var b in new[] { advance, miniAdvance }) { b.onClick.RemoveAllListeners(); b.onClick.AddListener(Advance); }
        dim = Load("StoryTransition").GetComponent<Image>(); dim.raycastTarget = false;
        dim.transform.SetAsFirstSibling();
        foreach (var t in GetComponentsInChildren<TMP_Text>(true)) if (t.font == null && font != null) t.font = font;
        dialogue.gameObject.SetActive(false); mini.gameObject.SetActive(false); task.gameObject.SetActive(false);
        item.gameObject.SetActive(false); walker.gameObject.SetActive(false); skip.gameObject.SetActive(false);
        Node<TMP_Text>(overlay, "ClosureSign").gameObject.SetActive(false);
        audioSource = gameObject.AddComponent<AudioSource>(); audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0; audioSource.ignoreListenerPause = true;
        storyListener = gameObject.AddComponent<AudioListener>(); storyListener.enabled = false;
        SetObjective("");
    }
    public void ShowClosure(Vector3 worldPosition)
    {
        var sign = Node<TMP_Text>(overlay, "ClosureSign");
        Vector3 point = Camera.main != null ? Camera.main.WorldToViewportPoint(worldPosition) : new Vector3(.5f, .55f);
        float x = Mathf.Clamp(point.x, .1f, .9f), y = Mathf.Clamp(point.y + .1f, .3f, .8f);
        Rect((RectTransform)sign.transform, new Vector2(x-.09f,y), new Vector2(x+.09f,y+.07f));
        sign.gameObject.SetActive(true);
    }
    public void RevealHammer() => revealed = true;
    public void SetSkipAvailable(bool value) => skip.gameObject.SetActive(value);
    public void SetObjective(string value)
    {
        objective.text = value; objective.gameObject.SetActive(true); task.gameObject.SetActive(!string.IsNullOrEmpty(value));
        // Keep the completed mini instruction beside its highlighted target until the action.
        if (!active) mini.gameObject.SetActive(miniMode && !string.IsNullOrEmpty(value));
        foreach (string n in new[] { "WoodIcon", "LeatherIcon", "WoodCount", "LeatherCount" })
            Node<Component>(task, n).gameObject.SetActive(false);
    }
    public void SetGatherObjective(Sprite wood, Sprite leather, int woodCount, int leatherCount)
    {
        task.gameObject.SetActive(true); objective.gameObject.SetActive(false);
        Node<Image>(task, "WoodIcon").sprite = wood; Node<Image>(task, "LeatherIcon").sprite = leather;
        Node<TMP_Text>(task, "WoodCount").text = $"나무 원목 {woodCount}/15";
        Node<TMP_Text>(task, "LeatherCount").text = $"가죽 {leatherCount}/2";
        foreach (string n in new[] { "WoodIcon", "LeatherIcon", "WoodCount", "LeatherCount" }) Node<Component>(task, n).gameObject.SetActive(true);
    }
    public void Reject() { warning.text = "내 말을 따르렴"; warningUntil = Time.unscaledTime + 1.6f; }
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
        DrawLine();
        while (line < lines.Length && !skipRequested) yield return null;
        active = false; dialogue.gameObject.SetActive(false); mini.gameObject.SetActive(false);
        task.gameObject.SetActive(!string.IsNullOrEmpty(objective.text));
        skip.gameObject.SetActive(StoryTutorial.Guiding);
        PlayerInputHandler input = FindFirstObjectByType<PlayerInputHandler>(); input?.ClearGameplayInput();
    }
    void DrawLine()
    {
        string raw = lines[line];
        foreach (string cue in new[] { "stand", "sit", "charge", "collector-sword", "collector-return" })
            if (raw.StartsWith("[" + cue + "]", StringComparison.Ordinal))
            { raw = raw.Substring(cue.Length + 2); Cue?.Invoke(cue); break; }
        if (raw.StartsWith("[reveal]", StringComparison.Ordinal)) { revealed = true; raw = raw.Substring(8); Sound("hum"); }
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
        visible = 0; glyphClock = finishedClock = 0; Body.maxVisibleCharacters = 0; Next.gameObject.SetActive(false);
    }
    void Advance()
    {
        if (!Speaking || ConfirmationOpen) return;
        if (visible < Body.textInfo.characterCount) { visible = Body.textInfo.characterCount; Body.maxVisibleCharacters = visible; return; }
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
        active = false; dialogue.gameObject.SetActive(false); mini.gameObject.SetActive(false);
        CloseConfirmation(); ShowItem(null, false); walker.gameObject.SetActive(false); FocusTargets(Array.Empty<RectTransform>());
    }
    void CloseConfirmation() { if (confirmation != null) { confirmation.gameObject.SetActive(false); Destroy(confirmation.gameObject); confirmation = null; } }
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
        if(miniMode&&mini.gameObject.activeSelf&&holes.Count>0)
        {
            var h=holes[0];float width=.46f,height=.19f;
            float x=Mathf.Clamp(h.xMax+.015f,.02f,.98f-width),y=Mathf.Clamp(h.yMin,.03f,.74f-height);
            if(h.xMax>.65f)x=Mathf.Clamp(h.xMin-width-.015f,.02f,.98f-width);
            Rect(mini,new(x,y),new(x+width,y+height));
            // The authored tail points toward the actual highlighted control, not a fixed corner.
            var tail = Node<RectTransform>(mini,"BubbleTail");
            float tailY=Mathf.Clamp((h.center.y-y)/height,.12f,.78f);
            if(h.center.x<x)Rect(tail,new(-.016f,tailY),new(.002f,tailY+.08f));
            else Rect(tail,new(.998f,tailY),new(1.016f,tailY+.08f));
        }
        task.SetAsLastSibling();warning.transform.SetAsLastSibling();skip.transform.SetAsLastSibling();
        dialogue.SetAsLastSibling();mini.SetAsLastSibling();if(confirmation!=null)confirmation.SetAsLastSibling();
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
    // Check the cached world listener after scene/room transitions in this frame.
    // Keep its reference while disabled: returning to town must disable our fallback immediately.
    void LateUpdate()
    {
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
        if(!Speaking||ConfirmationOpen||line>=lines.Length)return;
        glyphClock-=Time.unscaledDeltaTime*Speed;
        while(glyphClock<=0&&visible<Body.textInfo.characterCount)
        {
            char ch=Body.textInfo.characterInfo[visible].character;visible++;Body.maxVisibleCharacters=visible;
            bool punctuation=".,!?…:;".IndexOf(ch)>=0;
            glyphClock+=punctuation?.14f:1f/30;
            if(!char.IsWhiteSpace(ch)&&!punctuation&&visible%3==0)Sound("voice");
        }
        bool complete=visible>=Body.textInfo.characterCount;Next.gameObject.SetActive(complete);
        if(Keyboard.current?.spaceKey.wasPressedThisFrame==true)Advance();
        else if(complete&&Speed>1){finishedClock+=Time.unscaledDeltaTime*Speed;if(finishedClock>.8f)Advance();}
    }
    void OnDestroy(){foreach(var c in sounds.Values)if(c!=null)Destroy(c);if(pixelLogo!=null){Destroy(pixelLogo.texture);Destroy(pixelLogo);}}
}
public sealed class StoryBlockedControl : MonoBehaviour, IPointerClickHandler, IPointerDownHandler
{
    public void OnPointerClick(PointerEventData data)=>StoryTutorial.Reject();
    public void OnPointerDown(PointerEventData data)=>StoryTutorial.Reject();
}
