// [코드 지도] CampaignRegion: 캠페인 지역의 이름, 배경, 경계와 진입 위치 데이터를 제공한다.
// 주요 함수: Apply, OnTriggerEnter2D, OnTriggerStay2D
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/Campaign/Core/CampaignRegion.cs.md

using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class CampaignRegion : MonoBehaviour
{
    public string regionName;
    public bool town,wind;
    public Color background=new Color(.1f,.16f,.15f);
    void OnTriggerEnter2D(Collider2D other){Apply(other);}
    void OnTriggerStay2D(Collider2D other){Apply(other);}
    void Apply(Collider2D other)
    {
        if(other.GetComponentInParent<PlayerAssimilate>()==null)return;
        CampaignController.Instance?.EnterRegion(this);
    }
}
