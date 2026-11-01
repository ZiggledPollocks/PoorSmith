using System;
using System.Collections.Generic;
using Blacksmith;
using SettingsMenuUI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

/// <summary>One bounded audio host; GameAudioChannel retains the existing user-volume path.</summary>
public sealed class GameSound : MonoBehaviour
{
    static GameSound instance;
    public static event Action<string,int> Played;
    sealed class Voice { public AudioSource source; public GameAudioChannel channel; public Component owner; public string cue; public int ownerId,priority; public bool live,loop,world,craft,tail,paused; public float gain=1,expires=float.PositiveInfinity,started; public Vector2 point; }
    sealed class Override { public Component owner; public string cue; public int priority; }
    readonly System.Random variantRandom=new System.Random();
    readonly HashSet<string> once=new();
    Component pendingClick;int pendingClickFrame=-1;
    readonly List<Voice> voices=new(); readonly List<Override> overrides=new();
    readonly Dictionary<string,GameSoundCatalog.Cue> cues=new(); readonly Dictionary<string,AudioClip> synthesized=new();
    readonly Dictionary<string,int> previousClip=new();
    sealed class FailedDrop { public Component owner; public float range; }
    readonly Dictionary<int,FailedDrop> failedDrops=new();
    readonly List<int> staleDrops=new();
    AudioListener fallbackListener;
    GameSoundCatalog catalog; AudioSource[] music=new AudioSource[2];GameAudioChannel[] musicChannels=new GameAudioChannel[2];float[] musicWeights=new float[2];
    SoundSettingsController settings; UIManager settingsUI;FieldSceneState field;CaveEntranceBackgroundTransition cave;PlayerMovement player;
    string[] musicIds=new string[2];
    public float StepDistance=>catalog!=null?catalog.footstepDistance:1.1f;
    string targetMusic;int musicTarget;bool focused=true,appPaused;int specialFrame=-1;Vector2 pointer;int pointerVersion,hoverVersion=-1;float bagRetryUntil;bool bagBlocked;
    public string CurrentMusic=>targetMusic;
    public float MusicDuck {get;private set;}=1;
    public int ActiveVoices {get{int n=0;foreach(var v in voices)if(v.live)n++;return n;}}
    public static GameSound Current=>instance;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset(){instance=null;Played=null;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Bootstrap()=>Host();
    static GameSound Host(){if(!Application.isPlaying)return null;if(instance==null){var g=new GameObject("Game Sound");instance=g.AddComponent<GameSound>();}return instance;}
    void Awake(){if(instance!=null&&instance!=this){Destroy(gameObject);return;}instance=this;DontDestroyOnLoad(gameObject);catalog=Resources.Load<GameSoundCatalog>("Audio/GameSoundCatalog");if(catalog==null){enabled=false;return;}foreach(var c in catalog.cues??Array.Empty<GameSoundCatalog.Cue>())if(c!=null&&!string.IsNullOrEmpty(c.id))cues[c.id]=c;
        for(int i=0;i<2;i++){var g=new GameObject("Music "+i,typeof(AudioSource));g.transform.SetParent(transform);music[i]=g.GetComponent<AudioSource>();music[i].playOnAwake=false;music[i].loop=true;music[i].ignoreListenerPause=true;musicChannels[i]=g.AddComponent<GameAudioChannel>();musicChannels[i].Configure(SoundChannel.BGM,0);}
        for(int i=0;i<Mathf.Clamp(catalog.maximumVoices,1,64);i++){var g=new GameObject("SFX voice "+i,typeof(AudioSource));g.transform.SetParent(transform);var src=g.GetComponent<AudioSource>();src.playOnAwake=false;src.spatialBlend=0;src.pitch=1;var ch=g.AddComponent<GameAudioChannel>();ch.Configure(SoundChannel.SFX,0);voices.Add(new Voice{source=src,channel=ch});}
        SceneManager.sceneLoaded+=SceneLoaded;Rebind();}
    // UI-only scenes need a listener too. Never compete with an authored world/story listener.
    void EnsureListener()
    {
        bool external=false;foreach(var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))if(listener!=fallbackListener&&listener.enabled&&listener.gameObject.activeInHierarchy){external=true;break;}
        if(external){if(fallbackListener!=null)fallbackListener.enabled=false;}
        else {if(fallbackListener==null)fallbackListener=gameObject.AddComponent<AudioListener>();fallbackListener.enabled=true;}
    }
    void Rebind(){EnsureListener();settings=GameUIController.Instance?.Sound??FindFirstObjectByType<SoundSettingsController>(FindObjectsInactive.Include);settingsUI=FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);field=FindFirstObjectByType<FieldSceneState>();cave=FindFirstObjectByType<CaveEntranceBackgroundTransition>();player=FindFirstObjectByType<PlayerMovement>();foreach(var v in voices)v.channel.Bind(settings);foreach(var c in musicChannels)c?.Bind(settings);foreach(var b in FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include,FindObjectsSortMode.None))GameSoundButton.Attach(b);}
    void SceneLoaded(Scene s,LoadSceneMode m){Rebind();if(m==LoadSceneMode.Single){previousClip.Clear();once.Clear();failedDrops.Clear();bagBlocked=false;}if(m==LoadSceneMode.Single)foreach(var v in voices)if(v.live&&!(v.tail&&Time.unscaledTime-v.started<2))Clear(v);}
    public static void Register(PlayerMovement movement){var h=Host();if(h!=null)h.player=movement;}
    public static void Play(string id,Component owner=null,bool world=false,bool tail=false,Vector2? point=null){Host()?.Emit(id,owner,world,tail,point,false,false);}
    public static void UI(string id,Component owner=null){var h=Host();if(h==null)return;h.specialFrame=Time.frameCount;h.Emit(id,owner,false,false,null,false,false);}
    public static void Once(string id,Component owner,bool world=false,bool tail=false){var h=Host();if(h!=null&&owner!=null&&h.once.Add(owner.GetInstanceID()+"/"+id))h.Emit(id,owner,world,tail,null,false,false);}
    public static void QueueClick(Component owner){var h=Host();if(h!=null){h.pendingClick=owner;h.pendingClickFrame=Time.frameCount;}}
    void LateUpdate(){EnsureListener();if(pendingClickFrame==Time.frameCount){if(specialFrame!=pendingClickFrame)Emit("Click",pendingClick,false,false,null,false,false);pendingClickFrame=-1;pendingClick=null;}}
    public static void ClearTransientMusic(Component owner){if(instance!=null)instance.overrides.RemoveAll(x=>x.owner==owner&&x.priority<20);}
    public static void Click(Component owner){var h=Host();if(h!=null&&h.specialFrame!=Time.frameCount)h.Emit("Click",owner,false,false,null,false,false);}
    public static void Hover(Component owner){var h=Host();if(h==null)return;h.UpdatePointer();if(h.hoverVersion==h.pointerVersion)return;h.hoverVersion=h.pointerVersion;h.Emit("Hover",owner,false,false,null,false,false);}
    // A failure is latched across all nearby drops, not per automatic retry/object.
    public static void BagFailure(Component drop=null,float range=0f){var h=Host();if(h==null)return;h.RefreshFailedDrops();if(drop!=null){int id=drop.GetInstanceID();if(!h.failedDrops.TryGetValue(id,out var failed)){failed=new FailedDrop{owner=drop};h.failedDrops[id]=failed;}failed.range=range;}h.bagRetryUntil=Time.unscaledTime+.8f;if(h.bagBlocked)return;h.bagBlocked=true;h.Emit("BagFull",h,false,false,null,false,false);}
    // Re-arm on a real range exit, even when re-entry is faster than the pickup retry timer.
    void RefreshFailedDrops(){if(failedDrops.Count==0)return;staleDrops.Clear();foreach(var pair in failedDrops)if(pair.Value.owner==null||!pair.Value.owner.gameObject.activeInHierarchy||player!=null&&Vector2.Distance(player.transform.position,pair.Value.owner.transform.position)>pair.Value.range)staleDrops.Add(pair.Key);foreach(int id in staleDrops)failedDrops.Remove(id);if(failedDrops.Count==0)bagBlocked=false;}
    public static void BagRecovered(){if(instance!=null){instance.bagBlocked=false;instance.failedDrops.Clear();}}
    public static void Loop(string id,Component owner,bool active,bool craft=false){var h=Host();if(h==null||owner==null)return;var v=h.Find(owner.GetInstanceID(),id);if(!active){if(v!=null)v.gain=-Mathf.Abs(v.gain);return;}if(v==null)h.Emit(id,owner,false,false,null,true,craft);else{v.gain=1;v.expires=float.PositiveInfinity;}}
    public static void Touch(string id,Component owner){Loop(id,owner,true,true);var h=instance;var v=h?.Find(owner.GetInstanceID(),id);if(v!=null)v.expires=Time.unscaledTime+.13f;}
    public static void Stop(Component owner,bool loopsOnly=false){if(instance==null||owner==null)return;foreach(var v in instance.voices)if(v.live&&v.ownerId==owner.GetInstanceID()&&(!loopsOnly||v.loop)&&!v.tail){if(v.loop)v.gain=-Mathf.Abs(v.gain);else instance.Clear(v);}}
    public static void OverrideMusic(Component owner,string cue,int priority=10){var h=Host();if(h==null||owner==null)return;h.overrides.RemoveAll(x=>x.owner==owner);if(cue!=null)h.overrides.Add(new Override{owner=owner,cue=cue,priority=priority});}
    Voice Find(int owner,string id){foreach(var v in voices)if(v.live&&v.ownerId==owner&&v.cue==id)return v;return null;}
    AudioClip Clip(GameSoundCatalog.Cue c,int owner){if(c.clips!=null&&c.clips.Length>0){if(c.clips.Length==1)return c.clips[0];string key=owner+"/"+c.id;bool had=previousClip.TryGetValue(key,out int prev);int index=variantRandom.Next(c.clips.Length);if(had&&c.clips.Length>1&&index==prev)index=(index+1)%c.clips.Length;previousClip[key]=index;return c.clips[index];}if(c.fallback==0)return null;if(!synthesized.TryGetValue(c.id,out var clip)){clip=GameSoundSynth.Create(c.id,c.fallback);synthesized[c.id]=clip;}return clip;}
    void Emit(string id,Component owner,bool world,bool tail,Vector2? point,bool loop,bool craft){if(catalog==null||!focused||appPaused||!cues.TryGetValue(id,out var c))return;int oid=owner!=null?owner.GetInstanceID():0;if(loop&&Find(oid,id)!=null)return;Vector2 pos=point??(owner!=null?(Vector2)owner.transform.position:Vector2.zero);float distance=player!=null?Vector2.Distance(player.transform.position,pos):0;if(world&&distance>=catalog.screenDistance*c.distanceScreens)return;int effectivePriority=id=="Hit"&&world?60:c.priority;Voice voice=null;foreach(var v in voices)if(!v.live){voice=v;break;}if(voice==null){foreach(var v in voices)if((v.priority<effectivePriority||v.priority==effectivePriority&&world&&v.world&&player!=null&&Vector2.Distance(v.point,player.transform.position)>distance)&&(voice==null||v.priority<voice.priority))voice=v;}if(voice==null)return;AudioClip clip=Clip(c,oid);if(clip==null)return;Clear(voice);voice.live=true;voice.cue=id;voice.owner=owner;voice.ownerId=oid;voice.world=world;voice.craft=craft;voice.tail=tail;voice.loop=loop;voice.priority=effectivePriority;voice.point=pos;voice.gain=1;voice.started=Time.unscaledTime;voice.expires=float.PositiveInfinity;voice.source.clip=clip;voice.source.loop=loop;voice.source.pitch=1;voice.source.ignoreListenerPause=!world;voice.channel.SetGain(c.relativeVolume);voice.source.Play();Played?.Invoke(id,oid);}
    void Clear(Voice v){v.source.Stop();v.source.clip=null;v.live=false;v.paused=false;v.owner=null;v.cue=null;}
    bool WorkPaused=>GameUIController.Instance?.SettingsVisible==true||settingsUI!=null&&settingsUI.IsSettingsOpen||StoryTutorial.Speaking||OwnedItemModal.CapturesInput;
    void UpdatePointer(){if(Mouse.current!=null){var pos=Mouse.current.position.ReadValue();if((pos-pointer).sqrMagnitude>.25f){pointer=pos;pointerVersion++;}}}
    void Update(){if(catalog==null)return;if(settings==null&&(Time.frameCount%30==0))Rebind();UpdatePointer();
        RefreshFailedDrops();if(bagBlocked&&failedDrops.Count==0&&Time.unscaledTime>bagRetryUntil)bagBlocked=false;
        int ambience=0;foreach(var active in voices)if(active.live&&active.priority<80)ambience++;
        foreach(var v in voices){if(!v.live)continue;bool pause=!focused||appPaused||(v.world?Time.timeScale==0:v.craft&&WorkPaused);if(v.paused!=pause){v.paused=pause;if(pause)v.source.Pause();else v.source.UnPause();}
            if(v.loop&&(v.owner==null||!v.owner.gameObject.activeInHierarchy||Time.unscaledTime>v.expires))v.gain=-Mathf.Abs(v.gain);if(v.gain<0){float fade=Mathf.Abs(v.gain)-Time.unscaledDeltaTime/catalog.loopStopSeconds;if(fade<=0){Clear(v);continue;}v.gain=-fade;}
            if(!pause&&!v.loop&&!v.source.isPlaying){Clear(v);continue;}float gain=Mathf.Abs(v.gain)*(v.priority<80?Mathf.Min(1f,3f/Mathf.Max(1,ambience)):1f);var c=cues[v.cue];if(v.world){if(v.owner!=null)v.point=v.owner.transform.position;var delta=player!=null?v.point-(Vector2)player.transform.position:Vector2.zero;float t=Mathf.Clamp01(delta.magnitude/(catalog.screenDistance*c.distanceScreens));gain*=(1-t)*(1-t);v.source.panStereo=Mathf.Clamp(delta.x/catalog.screenDistance,-.65f,.65f);}else v.source.panStereo=0;v.channel.SetGain(c.relativeVolume*gain);}
        UpdateMusic();}
    void UpdateMusic(){for(int i=overrides.Count-1;i>=0;i--)if(overrides[i].owner==null||!overrides[i].owner.gameObject.activeInHierarchy)overrides.RemoveAt(i);string requested=null;int priority=int.MinValue;foreach(var o in overrides)if(o.priority>=priority){requested=o.cue;priority=o.priority;}
        if(requested==null){string scene=SceneManager.GetActiveScene().name;requested=field!=null&&!field.Ready&&targetMusic!=null?targetMusic:scene=="TitleScene"?"Sad":scene=="BlacksmithShop"?"Sad":field!=null?(cave!=null&&cave.IsInsideCave?"Cave":"Forest"):SmithyInterior.Instance?.Inside==true?"Sad":"Town";}
        if(requested!=targetMusic&&cues.TryGetValue(requested,out var entry)){foreach(var v in voices)if(v.live&&v.world&&!v.tail)Clear(v);var clip=Clip(entry,0);if(clip!=null){int slot=music[0].clip==clip?0:music[1].clip==clip?1:musicWeights[0]<=musicWeights[1]?0:1;if(music[slot].clip!=clip){music[slot].Stop();music[slot].clip=clip;musicWeights[slot]=0;}if(!music[slot].isPlaying&&focused&&!appPaused)music[slot].Play();musicIds[slot]=requested;musicTarget=slot;targetMusic=requested;}}
        float duck=1;var ui=GameUIController.Instance;if(ui!=null&&ui.MainMenuVisible||settingsUI!=null&&settingsUI.IsSettingsOpen)duck=Mathf.Min(duck,catalog.menuDuck);if(player!=null&&player.GetComponent<PlayerAssimilate>()?.IsDead==true)duck=Mathf.Min(duck,catalog.deathDuck);if(StoryTutorial.Speaking)duck=Mathf.Min(duck,catalog.dialogueDuck);MusicDuck=Mathf.MoveTowards(MusicDuck,duck,Time.unscaledDeltaTime*3);
        for(int i=0;i<2;i++){musicWeights[i]=Mathf.MoveTowards(musicWeights[i],i==musicTarget?1:0,Time.unscaledDeltaTime/catalog.musicFadeSeconds);musicChannels[i].SetGain((musicIds[i]!=null&&cues.TryGetValue(musicIds[i],out var cue)?cue.relativeVolume:0)*musicWeights[i]*MusicDuck);if(i==musicTarget&&music[i].clip!=null&&focused&&!appPaused&&!music[i].isPlaying)music[i].Play();if(musicWeights[i]==0&&i!=musicTarget)music[i].Stop();}}
    void OnApplicationFocus(bool value){focused=value;PauseMusic();PauseVoicesForFocus();}
    void OnApplicationPause(bool value){appPaused=value;PauseMusic();PauseVoicesForFocus();}
    // Focus can stop Editor/player updates immediately, so pause SFX in the callback itself.
    void PauseVoicesForFocus(){foreach(var v in voices)if(v.live){bool pause=!focused||appPaused||(v.world?Time.timeScale==0:v.craft&&WorkPaused);v.paused=pause;if(pause)v.source.Pause();else v.source.UnPause();}}
    void PauseMusic(){foreach(var s in music)if(s!=null){if(!focused||appPaused)s.Pause();else if(s.clip!=null)s.UnPause();}}
    void OnDestroy(){SceneManager.sceneLoaded-=SceneLoaded;foreach(var clip in synthesized.Values)if(clip!=null)Destroy(clip);if(instance==this)instance=null;}
}

static class GameSoundSynth
{
    // Existing smithy success and story voice envelopes are retained; missing roles are replaceable quiet fallbacks.
    public static AudioClip Create(string id,int kind){int rate=22050;float seconds=kind==3?.38f:kind==4?.32f:kind==6?.035f:.16f;var samples=new float[Mathf.CeilToInt(rate*seconds)];uint noise=2463534242;
        for(int i=0;i<samples.Length;i++){float t=i/(float)rate;noise=noise*1664525+1013904223;float hiss=((noise&65535)/32767.5f)-1;float envelope=Mathf.Clamp01(t*90)*Mathf.Exp(-t*(kind==3?5:20));float f=kind==1?420:kind==2?150:kind==4?260:kind==5?105:320;float value=kind==3?Mathf.Sin(2*Mathf.PI*660*t)*.40f+Mathf.Sin(2*Mathf.PI*990*t)*.23f:kind==5?hiss*.25f+Mathf.Sin(2*Mathf.PI*f*t)*.3f:Mathf.Sin(2*Mathf.PI*f*t)*.35f;samples[i]=value*envelope;}
        var clip=AudioClip.Create("Replaceable_"+id,samples.Length,1,rate,false);clip.SetData(samples,0);return clip;}
}
