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
