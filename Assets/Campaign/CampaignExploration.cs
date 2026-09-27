using System.Collections.Generic;
using UnityEngine;

public sealed class CampaignExploration : MonoBehaviour
{
    public float cellSize=8;
    readonly HashSet<string> seen=new();
    public string Cell(Vector2 p)=>Mathf.FloorToInt(p.x/cellSize)+","+Mathf.FloorToInt(p.y/cellSize);
    public bool IsExplored(Vector2 position)=>seen.Contains(Cell(position));
    public void Restore(IEnumerable<string> cells){seen.Clear();foreach(var cell in cells)seen.Add(cell);}
    public bool Reveal(Vector2 position,CampaignState state)
    {
        bool changed=false;
        for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)
        {string key=Cell(position+new Vector2(x,y)*cellSize);if(seen.Add(key)){state.visitedCells.Add(key);changed=true;}}
        return changed;
    }
}
