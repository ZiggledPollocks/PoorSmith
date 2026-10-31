// [코드 지도] CampaignDamageNumber: 전투 피해량을 월드 화면에 잠시 표시한다.
// 주요 함수: Show, Update
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/Campaign/Combat/CampaignDamageNumber.cs.md

using TMPro;
using UnityEngine;

public sealed class CampaignDamageNumber : MonoBehaviour
{
    TMP_Text label;float elapsed;
    public static void Show(GameObject target,float damage)
    {
        if(target==null||damage<=0)return;
        var go=new GameObject("Damage "+damage.ToString("0.#"));
        var box=target.GetComponent<Collider2D>();go.transform.position=(box!=null?new Vector3(box.bounds.center.x,box.bounds.max.y+.4f,target.transform.position.z):target.transform.position+Vector3.up);
        var popup=go.AddComponent<CampaignDamageNumber>();popup.label=go.AddComponent<TextMeshPro>();
        popup.label.text=damage.ToString("0.#");popup.label.fontSize=5;popup.label.alignment=TextAlignmentOptions.Center;popup.label.color=new Color(1,.8f,.25f);
        popup.label.rectTransform.sizeDelta=new Vector2(3,1);popup.label.GetComponent<MeshRenderer>().sortingOrder=1000;
    }
    void Update(){elapsed+=Time.deltaTime;transform.position+=Vector3.up*(Time.deltaTime*.8f);if(label!=null)label.alpha=Mathf.Clamp01(1-elapsed/1.2f);if(elapsed>=1.2f)Destroy(gameObject);}
}
