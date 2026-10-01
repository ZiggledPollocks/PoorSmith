// [코드 지도] TownAtmosphere: 마을의 낮·밤 배경색과 레이어 색상을 갱신한다.
// 주요 함수: Update
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/Campaign/Town/TownAtmosphere.cs.md

using UnityEngine;
public sealed class TownAtmosphere : MonoBehaviour
{
    public SpriteRenderer[] layers;
    void Update()
    {
        var c=CampaignController.Instance;if(c==null||!c.Ready)return;
        bool night=SmithingLoop.Instance.SmithData.night;Color target=night?new(.24f,.31f,.50f):Color.white;
        foreach(var r in layers)if(r!=null)r.color=Color.Lerp(r.color,target,Time.unscaledDeltaTime*3);
        if(c.InTown&&Camera.main!=null)Camera.main.backgroundColor=Color.Lerp(Camera.main.backgroundColor,night?new(.045f,.08f,.15f):new(.53f,.73f,.85f),Time.unscaledDeltaTime*3);
    }
}
