using UnityEngine;
using System.Collections;

/// <summary>Renders authored ON/OFF warp art in a child without changing the trigger or saved unlock rules.</summary>
public sealed class WarpStoneArt : MonoBehaviour
{
    CampaignWorldObject source;SpriteRenderer visual;GameArtSkin skin;bool on;float clock;int frame;bool grounded;float groundY;
    void Awake()
    {
        source=GetComponent<CampaignWorldObject>();skin=GameArtSkin.Current;var original=GetComponent<SpriteRenderer>();
        if(source==null||skin==null||skin.warpOff==null||original==null){enabled=false;return;}
        var child=new GameObject("WarpStoneArt");child.layer=gameObject.layer;child.transform.SetParent(transform,false);
        visual=child.AddComponent<SpriteRenderer>();visual.sharedMaterial=original.sharedMaterial;visual.sortingLayerID=original.sortingLayerID;visual.sortingOrder=original.sortingOrder;visual.color=original.color;
        float height=original.sprite!=null?original.sprite.bounds.size.y:skin.warpOff.bounds.size.y;
        float scale=height/Mathf.Max(.001f,skin.warpOff.bounds.size.y);child.transform.localScale=Vector3.one*scale;
        visual.sprite=skin.warpOff;original.enabled=false;
    }
    // Only the visual moves: authored interaction colliders, map positions and stable IDs stay intact.
    IEnumerator Start()
    {
        var hits=new RaycastHit2D[8];var filter=new ContactFilter2D();filter.SetLayerMask(1<<6);filter.useTriggers=false;
        for(int attempt=0;attempt<10&&!grounded;attempt++){
            Physics2D.SyncTransforms();int count=Physics2D.Raycast(transform.position+Vector3.up*.1f,Vector2.down,filter,hits,12f);
            float distance=float.MaxValue;
            for(int i=0;i<count;i++)if(hits[i].collider!=null&&hits[i].normal.y>.3f&&!hits[i].transform.IsChildOf(transform)&&hits[i].distance<distance){distance=hits[i].distance;groundY=hits[i].point.y+.015f;grounded=true;}
            if(!grounded)yield return null;
        }
    }
    void Update()
    {
        var state=SmithingLoop.Instance?.Campaign;bool active=state!=null&&state.warpUnlocked&&state.unlockedWarps!=null&&state.unlockedWarps.Contains(source.stableId);
        if(active!=on){on=active;clock=0;frame=0;}
        Sprite sprite=skin.warpOff;
        if(on&&skin.warpOn!=null&&skin.warpOn.Length>0){clock+=Time.deltaTime;float duration=skin.warpDurations!=null&&skin.warpDurations.Length==skin.warpOn.Length?skin.warpDurations[frame]:.1f;
            if(clock>=Mathf.Max(.01f,duration)){clock%=Mathf.Max(.01f,duration);frame=(frame+1)%skin.warpOn.Length;}sprite=skin.warpOn[frame];}
        visual.sprite=sprite;
        if(grounded){var pos=visual.transform.position;pos.y+=groundY-visual.bounds.min.y;visual.transform.position=pos;}
    }
}
