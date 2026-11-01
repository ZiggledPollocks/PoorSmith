using UnityEngine;
using UnityEngine.UI;

/// <summary>Observes actual motion only; does not change movement, damage or pickups.</summary>
public sealed class GameSoundActor : MonoBehaviour
{
    PlayerMovement movement;PlayerInputHandler input;PlayerAssimilate health;Rigidbody2D body;Collider2D shape;CharacterPhysics2D physics;IHealthSource monster;LiquidCircleGaugeHUD hud;
    InventorySystem inventory;float priorWeight;
    Vector3 previous;float distance,airTime,warningTime,nextFoot;bool grounded,wasDanger;string step;
    public static void Attach(GameObject target){if(Application.isPlaying&&target.GetComponent<GameSoundActor>()==null)target.AddComponent<GameSoundActor>();}
    void Start(){movement=GetComponent<PlayerMovement>();input=GetComponent<PlayerInputHandler>();health=GetComponent<PlayerAssimilate>();body=GetComponent<Rigidbody2D>();shape=GetComponent<Collider2D>();physics=GetComponent<CharacterPhysics2D>();monster=GetComponent<IHealthSource>();previous=transform.position;hud=FindFirstObjectByType<LiquidCircleGaugeHUD>(FindObjectsInactive.Include);inventory=GetComponent<InventorySystem>();OnEnable();if(movement!=null){step="Footstep";GameSound.Register(movement);}else step=GetComponent<StoneGolemController>()!=null?"GolemStep":GetComponent<MossSlimeController>()!=null?"SlimeStep":"DeerStep";}
    void Update(){Vector3 current=transform.position;float dx=Mathf.Abs(current.x-previous.x);previous=current;bool alive=health!=null?!health.IsDead:monster!=null&&!monster.IsDead;bool floor=shape!=null&&Physics2D.Raycast(new Vector2(shape.bounds.center.x,shape.bounds.min.y+.06f),Vector2.down,.18f,64).collider!=null;
        if(Time.deltaTime>0&&alive){if(!floor)airTime+=Time.deltaTime;else{if(!grounded&&airTime>.18f&&movement!=null)GameSound.Play("Land",this);else if(!grounded&&airTime>.05f&&GetComponent<MossSlimeController>()!=null)GameSound.Play("SlimeStep",this,true);airTime=0;}bool intent=movement==null||input!=null&&Mathf.Abs(input.MoveInput.x)>.01f||StoryTutorial.Cinematic;
            if(floor&&dx>.0001f&&dx<1.5f&&intent&&physics?.IsKnockbackActive!=true&&movement?.IsMovedOnlyByExternalWind!=true){distance+=dx;float stride=GameSound.Current!=null?GameSound.Current.StepDistance:1.1f;}else distance=0;
        }else{distance=0;airTime=0;}grounded=floor;
        if(health==null)return;bool canWarn=alive&&Time.deltaTime>0&&!GameUIController.BlocksGameplayInput&&!StoryTutorial.Speaking&&SmithingLoop.Instance?.InShop!=true;float threshold=hud!=null?hud.DangerThreshold:80;bool danger=health.CurrentAssimilation>=threshold;
        if(!danger){wasDanger=false;warningTime=0;}else if(canWarn){if(!wasDanger){GameSound.Play("Warning",this);wasDanger=true;warningTime=0;}else if((warningTime+=Time.deltaTime)>=10){warningTime=0;GameSound.Play("Warning",this);}}
    }
    // Walk/Run clip events are authored at the inspected alternate foot contacts. Physics/motion can veto them.
    public void AudioFootContact()
    {
        if(movement==null||health==null||health.IsDead||Time.unscaledTime<nextFoot||!grounded||distance<.12f||physics?.IsKnockbackActive==true||movement.IsMovedOnlyByExternalWind)return;
        nextFoot=Time.unscaledTime+.1f;distance=0;GameSound.Play("Footstep",this);
    }
    public void GroundContact(string cue)
    {
        if(body==null||shape==null||monster==null||monster.IsDead||Mathf.Abs(body.linearVelocity.x)<.05f||physics?.IsKnockbackActive==true)return;
        if(Physics2D.Raycast(new Vector2(shape.bounds.center.x,shape.bounds.min.y+.06f),Vector2.down,.18f,64).collider!=null)GameSound.Play(cue,this,true);
    }
    void OnEnable(){if(inventory!=null){priorWeight=inventory.CurrentWeight;inventory.InventoryChanged-=InventoryChanged;inventory.InventoryChanged+=InventoryChanged;}}
    void InventoryChanged(){float weight=inventory.CurrentWeight;if(weight<priorWeight)GameSound.BagRecovered();priorWeight=weight;}
    void OnDisable(){GameSound.Stop(this);if(inventory!=null)inventory.InventoryChanged-=InventoryChanged;}
}

/// <summary>The normal Button event remains the authority for accepting a click.</summary>
public sealed class GameSoundButton : MonoBehaviour
{
    Button button;
    public static void Attach(Button target){if(Application.isPlaying&&target!=null&&target.GetComponent<GameSoundButton>()==null)target.gameObject.AddComponent<GameSoundButton>();}
    void Awake(){button=GetComponent<Button>();}
    void OnEnable(){if(button!=null)button.onClick.AddListener(Accepted);}
    void OnDisable(){if(button!=null)button.onClick.RemoveListener(Accepted);}
    void Accepted(){if(button!=null&&button.interactable&&GetComponentInParent<SettingsMenuUI.SettingsTabItem>()==null&&StoryTutorial.CanUseButton(button.name)&&(GetComponent<GameSoundItemHover>()?.HasItem?.Invoke()??true))GameSound.QueueClick(this);}
}

public sealed class GameSoundItemHover:MonoBehaviour,UnityEngine.EventSystems.IPointerEnterHandler
{
    public System.Func<bool> HasItem;
    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e){if(HasItem?.Invoke()==true&&!e.dragging&&e.pointerDrag==null)GameSound.Hover(this);}
}
