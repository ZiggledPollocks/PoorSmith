using UnityEngine;

public interface ISwordTargetQuery
{
    Collider2D[] Find(Vector2 origin, Vector2 direction, ToolData sword, int layerMask);
    bool Includes(Vector2 origin, Vector2 direction, Collider2D collider, ToolData sword);
}
