// [코드 지도] CampaignSpawnPoint: 캠페인 월드 대상의 위치와 화면 노출 조건을 제공한다.
// 주요 함수: Tick, Update, Spawn
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/Campaign/World/CampaignSpawnPoint.cs.md

using UnityEngine;

// Uses existing resource/monster prefabs and their existing health/drop logic.
public sealed class CampaignSpawnPoint : MonoBehaviour
{
    public GameObject prefab;
    public float delay=120;
    public bool monster;
    GameObject instance;
    float remaining;
    bool populated;
    public float Remaining=>remaining;
    public GameObject Spawned=>instance;
    void Start(){Spawn();}
    public void Tick(float elapsed,bool visible)
    {
        if(instance!=null||!populated)return;
        if(monster&&visible)return;
        remaining-=Mathf.Max(0,elapsed);
        if(remaining<=0)Spawn();
    }
    void Update()
    {
        var cam=Camera.main;var v=cam==null?Vector3.one:cam.WorldToViewportPoint(transform.position);
        bool visible=cam!=null&&v.z>0&&v.x>=-.1f&&v.x<=1.1f&&v.y>=-.1f&&v.y<=1.1f;
        Tick(Time.deltaTime,visible);
    }
    void Spawn()
    {
        if(prefab==null)return;
        instance=Instantiate(prefab,transform.position,Quaternion.identity,transform);populated=true;remaining=delay;
    }
}
