using UnityEngine;

/// <summary>Stepped fan mesh: the old silhouette is retained without stretching a rectangular panel.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class CurvedToolHudGraphic : UnityEngine.UI.MaskableGraphic
{
        public CurvedToolHudGraphic() { useLegacyMeshGeneration = false; }

    public ToolHudCurveShape shape;
    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
    {
        mesh.Clear();
        if (shape == null || shape.leftEdge == null || shape.rightEdge == null || shape.leftEdge.Length != shape.rightEdge.Length || shape.palette == null || shape.palette.Length < 6) return;
        int rows=shape.leftEdge.Length,cols=shape.columns;
        Rect r=rectTransform.rect;
        if(rows==0||cols==0)return;
        float width=Mathf.Min(r.width,r.height*shape.aspect),height=width/shape.aspect;
        float x0=r.center.x-width*.5f,y0=r.center.y-height*.5f,dx=width/cols,dy=height/rows;
        for(int y=0;y<rows;y++)
        {
            int left=shape.leftEdge[y],right=shape.rightEdge[y];if(left<0||right<left)continue;
            for(int x=left;x<=right;x++)
            {
                bool leftRim=x==left,upperRim=y==rows-1||x<shape.leftEdge[Mathf.Min(y+1,rows-1)]||shape.leftEdge[Mathf.Min(y+1,rows-1)]<0;
                bool bottomRim=y==0||shape.leftEdge[Mathf.Max(0,y-1)]<0||x<shape.leftEdge[Mathf.Max(0,y-1)];
                Color c=leftRim||upperRim?shape.palette[5]:x==right||bottomRim?shape.palette[0]:x==left+1?shape.palette[4]:shape.palette[2+((y/2+x/11)%2)];
                // Sparse stable grain, without animation, new textures or moving the icon hit areas.
                if(!leftRim&&!upperRim&&!bottomRim&&x<right-1&&(y*7+x/6)%19==0)c=shape.palette[1];
                int n=mesh.currentVertCount;float x1=x0+x*dx,y1=y0+y*dy;
                mesh.AddVert(new Vector3(x1,y1),c,Vector2.zero);mesh.AddVert(new Vector3(x1,y1+dy),c,Vector2.zero);
                mesh.AddVert(new Vector3(x1+dx,y1+dy),c,Vector2.zero);mesh.AddVert(new Vector3(x1+dx,y1),c,Vector2.zero);
                mesh.AddTriangle(n,n+1,n+2);mesh.AddTriangle(n+2,n+3,n);
            }
        }
    }
}
