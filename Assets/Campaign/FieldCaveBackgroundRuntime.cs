using UnityEngine;

/// <summary>
/// Keeps the authored cave backdrop visible in the editor, but hands Play Mode
/// to the same 3x3 camera-following background used by SampleScene.
/// </summary>
[DefaultExecutionOrder(900)]
[DisallowMultipleComponent]
public sealed class FieldCaveBackgroundRuntime : MonoBehaviour
{
    [SerializeField] private Transform sectionTemplate;

    private void Awake()
    {
        if (sectionTemplate == null && transform.childCount > 0)
            sectionTemplate = transform.GetChild(0);
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform section = transform.GetChild(i);
            if (section != sectionTemplate)
                section.gameObject.SetActive(false);
        }
    }
}
