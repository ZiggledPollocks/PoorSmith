using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Reusable geometry, not a flattened screenshot. Original scene/art stay intact.
public static class CampaignBuildScenery
{
    const string Root="Assets/Campaign";
    static Material material;
    public static void Populate(Transform parent)
    {
        string path=Root+"/Data/Scenery.mat";
        material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null){material=new Material(Shader.Find("Sprites/Default"));AssetDatabase.CreateAsset(material,path);}
        var sprites=AssetDatabase.LoadAllAssetsAtPath("Assets/source/2D Pixel Art Platformer Biome - American Forest/Sprites.png").OfType<Sprite>().ToArray();
        foreach(int start in new[]{-140,0})for(int x=start;x<(start<0?-60:80);x+=16)
        {
            SpriteLayer(parent,"DistantHills",sprites.First(s=>s.name=="Background3"),new(x+8,10),new(16,26),-39);
            SpriteLayer(parent,"DistantPines",sprites.First(s=>s.name=="Background1"),new(x+8,6),new(16,20),-36);
        }
        var cave=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/source/PixelFantasy_Caves_1.0/background1.png");
        if(cave!=null)for(int x=0;x<220;x+=32)for(int y=-100;y<-10;y+=24)SpriteLayer(parent,"CaveBackdrop",cave,new(x+16,y+12),new(32,24),-35);
        foreach(int x in new[]{-118,-108,-97,-77})Building(parent,x);
    }
    static void SpriteLayer(Transform parent,string name,Sprite sprite,Vector2 pos,Vector2 size,int order)
    {
        var go=new GameObject(name,typeof(SpriteRenderer));go.transform.SetParent(parent);go.transform.position=pos;
        var sr=go.GetComponent<SpriteRenderer>();sr.sprite=sprite;sr.sortingOrder=order;go.transform.localScale=new Vector3(size.x/sprite.bounds.size.x,size.y/sprite.bounds.size.y,1);
    }
    static void Building(Transform parent,float x)
    {
        var vertices=new List<Vector3>();var colors=new List<Color>();var triangles=new List<int>();
        void Polygon(Color color,params Vector2[] points){int n=vertices.Count;foreach(var point in points){vertices.Add(point);colors.Add(color);}for(int i=1;i<points.Length-1;i++){triangles.Add(n);triangles.Add(n+i);triangles.Add(n+i+1);}}
        void Box(float a,float b,float w,float h,Color color)=>Polygon(color,new(a,b),new(a+w,b),new(a+w,b+h),new(a,b+h));
        Color beam=new(.20f,.14f,.10f),wall=new(.63f,.51f,.34f),roof=new(.22f,.32f,.32f);
        Box(-3.5f,0,7,5,wall);Box(-3.7f,0,7.4f,.45f,new(.32f,.34f,.32f));
        for(float b=.7f;b<5;b+=.55f)Box(-3.5f,b,7,.06f,new(.48f,.38f,.24f));
        foreach(float a in new[]{-3.5f,0,3.25f})Box(a,0,.25f,5.2f,beam);
        Polygon(beam,new(-4,4.7f),new(0,7),new(4,4.7f));
        Polygon(roof,new(-3.6f,4.9f),new(0,6.75f),new(3.6f,4.9f));
        for(float b=5;b<6.6f;b+=.32f){float half=(6.75f-b)*1.9f;Box(-half,b,half*2,.065f,new(.15f,.23f,.23f));}
        Box(-.8f,.4f,1.6f,2.8f,beam);Box(-.6f,.4f,1.2f,2.55f,new(.33f,.23f,.13f));Box(.34f,1.6f,.13f,.13f,new(.9f,.7f,.32f));
        foreach(float a in new[]{-2.6f,1.35f}){Box(a,2,1.3f,1.6f,beam);Box(a+.12f,2.12f,1.06f,1.36f,new(.80f,.66f,.36f));Box(a+.60f,2.12f,.08f,1.36f,beam);Box(a+.12f,2.75f,1.06f,.08f,beam);}
        Box(-2.4f,3.85f,4.8f,.75f,beam);Box(-2.25f,3.95f,4.5f,.55f,new(.38f,.30f,.18f));
        var mesh=new Mesh{name="TownBuilding"};mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
        string path=Root+"/Data/TownBuilding_"+(-x)+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(existing!=null){EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,path);
        var go=new GameObject("TimberShop",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent);go.transform.position=new Vector3(x,0);go.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.sortingOrder=-12;
        PrefabUtility.SaveAsPrefabAsset(go,Root+"/Prefabs/Building_"+(-x)+".prefab");
    }
}
