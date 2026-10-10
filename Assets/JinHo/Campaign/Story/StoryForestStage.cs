using UnityEngine;
using Unity.Cinemachine;

/// <summary>Authored SampleScene story forest; camera ownership is limited to Scene2/3.</summary>
public sealed class StoryForestStage : MonoBehaviour
{
    public Transform spawn;
    public BoxCollider2D cameraBounds;
    Camera storyCamera;CinemachineBrain brain;bool brainWasEnabled;
    public void BeginCamera(Camera camera)
    {
        storyCamera=camera;if(camera==null)return;
        brain=camera.GetComponent<CinemachineBrain>();if(brain!=null){brainWasEnabled=brain.enabled;brain.enabled=false;}
        FollowActor();
    }
    // Clamp camera center by half the actual viewport so the entire entrance remains visible.
    void LateUpdate(){if(storyCamera!=null)FollowActor();}
    void FollowActor()
    {
        var player=CampaignController.Instance?.Player;if(player==null||cameraBounds==null||storyCamera==null)return;
        Bounds b=cameraBounds.bounds;float h=storyCamera.orthographicSize,w=h*storyCamera.aspect;
        float x=Mathf.Clamp(player.position.x,b.min.x+Mathf.Min(w,b.extents.x),b.max.x-Mathf.Min(w,b.extents.x));
        float y=Mathf.Clamp(player.position.y+3,b.min.y+Mathf.Min(h,b.extents.y),b.max.y-Mathf.Min(h,b.extents.y));
        storyCamera.transform.position=new Vector3(x,y,storyCamera.transform.position.z);
    }
    public void EndCamera(){storyCamera=null;if(brain!=null)brain.enabled=brainWasEnabled;brain=null;}
    void OnDisable(){EndCamera();}
}
