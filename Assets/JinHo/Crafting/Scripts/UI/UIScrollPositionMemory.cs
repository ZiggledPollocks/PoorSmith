using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Blacksmith
{
    // Lives on each UI canvas, so rebuilt inventory lists keep their own positions.
    public sealed class UIScrollPositionMemory : MonoBehaviour
    {
        readonly Dictionary<string, Vector2> positions = new();

        public void Capture(Transform subtree)
        {
            foreach (var scroll in subtree.GetComponentsInChildren<ScrollRect>(true))
            {
                if (scroll != null && scroll.gameObject.activeInHierarchy)
                    positions[Path(scroll.transform)] = scroll.normalizedPosition;
            }
        }

        public void RestoreWhenLaidOut(ScrollRect scroll)
        {
            if (scroll != null && positions.TryGetValue(Path(scroll.transform), out var position))
                StartCoroutine(Restore(scroll, position));
        }

        IEnumerator Restore(ScrollRect scroll, Vector2 position)
        {
            Canvas.ForceUpdateCanvases();
            if (scroll != null) scroll.normalizedPosition = position;
            yield return null;
            Canvas.ForceUpdateCanvases();
            if (scroll != null)
            {
                scroll.StopMovement();
                scroll.normalizedPosition = position;
            }
        }

        string Path(Transform target)
        {
            var names = new List<string>();
            for (var node = target; node != null && node != transform; node = node.parent)
                names.Add(node.name);
            names.Reverse();
            return string.Join("/", names);
        }
    }
}
