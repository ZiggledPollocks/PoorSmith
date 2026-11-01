using UnityEngine;
namespace Blacksmith
{
    // Small stepped border/contact shadow, without a filled rectangle over the original illustration.
    public sealed class CraftingDepthRim : UnityEngine.UI.MaskableGraphic
    {
        public CraftingDepthRim() { useLegacyMeshGeneration = false; }

        int level = -1;
        public int Level { get => level; set { if (level == value) return; level = value; SetVerticesDirty(); } }
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear(); Rect r = rectTransform.rect;
            if (r.width < 1f || r.height < 1f) return;
            Color edge = level >= 4 ? new Color(1f,.84f,.5f,.92f) : level == 3 ? new Color(.95f,.76f,.44f,.85f) : level == 2 ? new Color(1f,.91f,.72f,.72f) : level == 1 ? new Color(.72f,.58f,.39f,.24f) : new Color(.46f,.43f,.38f,.14f);
            float lift = level >= 3 ? 2f : level == 2 ? 1f : 0f;
            Color shade = new(.10f,.06f,.03f,level >= 3 ? .26f : level >= 1 ? .14f : .05f);
            Quad(mesh,new Rect(r.xMin+3,r.yMin-3,r.width-3,3),shade);
            Quad(mesh,new Rect(r.xMax,r.yMin,3,r.height-3),shade);
            const float line=2f;
            Quad(mesh,new Rect(r.xMin+line,r.yMin+lift,r.width-2*line,line),edge);
            Quad(mesh,new Rect(r.xMin+line,r.yMax-line+lift,r.width-2*line,line),edge);
            Quad(mesh,new Rect(r.xMin,r.yMin+line+lift,line,r.height-2*line),edge);
            Quad(mesh,new Rect(r.xMax-line,r.yMin+line+lift,line,r.height-2*line),edge);
        }
        static void Quad(UnityEngine.UI.VertexHelper mesh, Rect r, Color c)
        {
            int n=mesh.currentVertCount;
            mesh.AddVert(new Vector3(r.xMin,r.yMin),c,Vector2.zero);mesh.AddVert(new Vector3(r.xMin,r.yMax),c,Vector2.zero);
            mesh.AddVert(new Vector3(r.xMax,r.yMax),c,Vector2.zero);mesh.AddVert(new Vector3(r.xMax,r.yMin),c,Vector2.zero);
            mesh.AddTriangle(n,n+1,n+2);mesh.AddTriangle(n+2,n+3,n);
        }
    }
}
