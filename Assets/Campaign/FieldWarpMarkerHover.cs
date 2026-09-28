using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Small pointer emphasis for selectable destinations on the field warp map.</summary>
public sealed class FieldWarpMarkerHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public void OnPointerEnter(PointerEventData eventData) => transform.localScale = Vector3.one * 1.15f;
    public void OnPointerExit(PointerEventData eventData) => transform.localScale = Vector3.one;
    void OnDisable() => transform.localScale = Vector3.one;
}
