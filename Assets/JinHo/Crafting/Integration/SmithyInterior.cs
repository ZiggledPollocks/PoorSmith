// [코드 지도] SmithyInterior: 마을의 대장간 실내 영역과 입출입·작업 화면 진입을 처리한다.
// 주요 함수: Contains, Leave, Update
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Crafting/Integration/SmithyInterior.cs.md

using Blacksmith;
using UnityEngine;

// A saved world prefab, using the same player, input, physics and camera as the town.
public sealed class SmithyInterior : MonoBehaviour
{
    public static SmithyInterior Instance {get;private set;}
    public Transform entry,exit;public BoxCollider2D cameraBounds;public SpriteRenderer window;
    public bool Contains(Vector2 p)
    {
        if(cameraBounds==null)return false;
        Vector2 local=cameraBounds.transform.InverseTransformPoint(p);
        return new Rect(cameraBounds.offset-cameraBounds.size*.5f,cameraBounds.size).Contains(local);
    }
    public bool Inside=>CampaignController.Instance?.Player!=null&&Contains(CampaignController.Instance.Player.position);
    public bool PanelOpen=>SmithingLoop.Instance!=null&&SmithingLoop.Instance.InShop&&Inside;
    void Awake(){Instance=this;}
    void OnDestroy(){if(Instance==this)Instance=null;}
    public void Enter()
    {
        var c=CampaignController.Instance;if(c==null||Inside||c.Travelling||SmithingLoop.Instance.InShop)return;
        c.TransitionTo(entry.position);
    }
    public void Leave()
    {
        var c=CampaignController.Instance;if(c==null||!Inside||c.Travelling||SmithingLoop.Instance.InShop||GameUIController.BlocksGameplayInput)return;
        Vector2 destination=c.townScene!=null?(Vector2)c.townScene.blackSmith.position+Vector2.up*2:c.townSpawn;
        c.TransitionTo(destination);
    }
    public void Open(SmithyStationKind kind)
    {
        if(!Inside||GameUIController.BlocksGameplayInput)return;
        SmithingLoop.Instance.OpenInteriorPanel(kind==SmithyStationKind.Bed?ScreenState.Sleep:kind==SmithyStationKind.CraftingDoor?ScreenState.Workshop:ScreenState.Chest,kind==SmithyStationKind.Rack);
    }
    void Update()
    {
        var c=CampaignController.Instance;if(c?.Ready!=true)return;
        if(window!=null)window.color=SmithingLoop.Instance.SmithData.night?new Color(.2f,.31f,.55f):new Color(.64f,.85f,1);
        if(Inside&&!c.Travelling&&!SmithingLoop.Instance.InShop&&c.Player.position.x<exit.position.x+.8f)Leave();
    }
}
