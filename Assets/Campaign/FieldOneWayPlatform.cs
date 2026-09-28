using UnityEngine;

/// <summary>Marks a field-map surface that supports landing from above only.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlatformEffector2D))]
public sealed class FieldOneWayPlatform : MonoBehaviour
{
    public Collider2D Surface => GetComponent<Collider2D>();
}
