using UnityEngine;

/// <summary>Optional saved visual reference point; never changes reach, collision or interaction eligibility.</summary>
[DisallowMultipleComponent]
public sealed class QuickInteractionPromptAnchor : MonoBehaviour
{
    [Tooltip("Local reference point before the ordinary prompt height offset is applied.")]
    public Vector3 localPosition;

    public Vector3 WorldPosition => transform.TransformPoint(localPosition);
}
