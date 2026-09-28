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
