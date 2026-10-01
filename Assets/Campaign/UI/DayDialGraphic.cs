// [코드 지도] DayDialGraphic: 대장간의 날짜·시간 다이얼을 그린다.
// 주요 함수: OnPopulateMesh, Triangle
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/Campaign/UI/DayDialGraphic.cs.md

using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class DayDialGraphic : MaskableGraphic
{
    public bool Night;
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();var rect=rectTransform.rect;float radius=Mathf.Min(rect.width*.47f,rect.height*.84f);Vector2 center=new(rect.center.x,rect.yMin+rect.height*.1f);
        void Triangle(Vector2 a,Vector2 b,Vector2 c,Color tint){int n=vh.currentVertCount;vh.AddVert(a,tint,Vector2.zero);vh.AddVert(b,tint,Vector2.zero);vh.AddVert(c,tint,Vector2.zero);vh.AddTriangle(n,n+1,n+2);}
        for(int i=0;i<40;i++){float a=i*Mathf.PI/40,b=(i+1)*Mathf.PI/40;Triangle(center,center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,new(.74f,.55f,.28f));Triangle(center,center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*(radius-4),center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*(radius-4),Night?new(.08f,.12f,.24f):new(.43f,.67f,.78f));}
        Vector2 orb=center+Vector2.up*radius*.52f;float size=radius*.20f;
        for(int i=0;i<24;i++){float a=i*Mathf.PI*2/24,b=(i+1)*Mathf.PI*2/24;Triangle(orb,orb+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*size,orb+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*size,Night?new(.87f,.9f,.97f):new(1,.81f,.23f));}
    }
}
