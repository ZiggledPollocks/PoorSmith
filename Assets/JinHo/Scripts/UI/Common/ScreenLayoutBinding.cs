using UnityEngine;

/// <summary>View-local layout binding for rebuilt children; never owns navigation or game data.</summary>
[DisallowMultipleComponent]
public sealed class ScreenLayoutBinding : MonoBehaviour
{
    public string screenName;
    public bool applyRoot;
    public static void Bind(string screen, Transform root, bool includeRoot = false)
    {
        if (root == null) return;
        var binding = root.GetComponent<ScreenLayoutBinding>() ?? root.gameObject.AddComponent<ScreenLayoutBinding>();
        binding.screenName = screen; binding.applyRoot = includeRoot;
        binding.Refresh();
    }
    void LateUpdate() => Refresh();
    public void Refresh() => ScreenLayoutTemplate.Apply(screenName, transform, applyRoot);
}
