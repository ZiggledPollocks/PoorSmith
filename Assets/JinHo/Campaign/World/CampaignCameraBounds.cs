// [코드 지도] CampaignCameraBounds: 캠페인 카메라의 이동 경계를 설정한다.
// 주요 함수: LateUpdate
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/Campaign/World/CampaignCameraBounds.cs.md

using UnityEngine;

[DefaultExecutionOrder(10000)]
public sealed class CampaignCameraBounds : MonoBehaviour
{
    public Rect townBounds=new Rect(-135,-4,70,25);
    public Rect fieldBounds=new Rect(-5,-110,225,130);
    public Rect windBounds=new Rect(235,-10,80,60);
    void LateUpdate()
    {
        var campaign=CampaignController.Instance;var camera=GetComponent<Camera>();
        if(campaign==null||!campaign.Ready||campaign.Player==null||camera==null||SmithingLoop.Instance==null||SmithingLoop.Instance.InShop)return;
        var p=campaign.Player.position;Rect b=p.x< -50?townBounds:p.x>230?windBounds:fieldBounds;
        float h=camera.orthographicSize,w=h*camera.aspect;
        transform.position=new Vector3(b.width<=2*w?b.center.x:Mathf.Clamp(transform.position.x,b.xMin+w,b.xMax-w),
            b.height<=2*h?b.center.y:Mathf.Clamp(transform.position.y,b.yMin+h,b.yMax-h),transform.position.z);
    }
}
