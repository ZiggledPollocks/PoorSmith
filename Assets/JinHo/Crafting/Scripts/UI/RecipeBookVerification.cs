#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Blacksmith
{
    // Explicit Play Mode check; temporary progress and acquired-item edits are restored.
    public static class RecipeBookVerification
    {
        [MenuItem("Blacksmith/Verify Recipe Map (Play Mode)")]
        static void Run()
        {
            var owner = UnityEngine.Object.FindFirstObjectByType<BlacksmithController>();
            if (!Application.isPlaying || owner == null || owner.Inventory == null)
            {
                Debug.LogWarning("Enter Play Mode first.");
                return;
            }
            owner.StartCoroutine(Verify(owner));
        }

        static IEnumerator Verify(BlacksmithController owner)
        {
            var data = owner.Inventory.Data;
            var progress = data.progress.ToList();
            var acquired = data.acquiredItems.ToList();
            bool save = owner.loadSavedGame;
            var original = owner.State;
            owner.loadSavedGame = false;
            try
            {
                data.progress.Clear();
                data.acquiredItems.Clear();
                data.acquiredItems.AddRange(new[] { "wood", "stone", "ore", "raw_leather" });
                owner.SetState(ScreenState.Recipes);
                yield return null;

                var map = Map();
                var panel = (RectTransform)map.transform;
                Check(panel.anchorMin == Vector2.zero && panel.anchorMax == Vector2.one, "Recipe map is not fullscreen");
                Check(!map.GetComponentsInChildren<Button>(true).Any(b => b.name.StartsWith("RecipeTab_", StringComparison.Ordinal)), "Category tabs remain");
                Check(map.GetComponentsInChildren<RecipeFogGraphic>(true).Length == 0, "Fog graphic remains");
                Check(RecipeGraphLayout.AllNodes().Select(n => n.ItemId).Distinct().Count() == RecipeGraphLayout.AllNodes().Count, "Duplicate graph nodes");
                foreach (var id in new[] { "wood", "stone", "ore", "raw_leather" })
                    Check(Vertex(id).gameObject.activeSelf, "Resource missing from unified map: " + id);
                Check(!Vertex("plank").gameObject.activeSelf, "Undiscovered recipe node is visible");
                Check(!Details().Contains("나무 판자"), "Undiscovered recipe name leaked");

                var plank = owner.catalog.recipes.First(r => r.enabled && r.outputId == "plank");
                data.progress.Add(new RecipeProgress { id = plank.id, crafts = 1 });
                yield return new WaitForSecondsRealtime(.3f);
                Check(Vertex("plank").gameObject.activeSelf, "Discovered recipe node did not appear");
                Check(Vertex("plank").GetComponent<CanvasGroup>() != null, "New node did not animate");
                Check(map.GetComponentsInChildren<Image>(true).Any(i => i.name == "RecipeLink" && i.enabled), "Discovered connection did not appear");
                yield return new WaitForSecondsRealtime(.5f);
                Check(Mathf.Abs(Vertex("plank").transform.localScale.x - 1) < .01f, "Node reveal did not finish");
                Vertex("plank").onClick.Invoke();
                Check(Details().Contains("나무 판자") && Details().Contains("숙련도 1"), "Recipe details mismatch");
                owner.SetState(ScreenState.Recipes);
                yield return null;
                Check(Details().Contains("나무 판자"), "Selection was lost after reopening the map");
                Debug.Log("RECIPE_MAP_VERIFY_PASS: fullscreen unified map, no categories/fog, discovery node/link animation, details and selection.");
            }
            finally
            {
                data.progress.Clear();
                data.progress.AddRange(progress);
                data.acquiredItems.Clear();
                data.acquiredItems.AddRange(acquired);
                owner.loadSavedGame = save;
                owner.SetState(original);
            }
        }

        static RecipeBookView Map() => UnityEngine.Object.FindFirstObjectByType<RecipeBookView>();
        static Button Vertex(string id) => Map().GetComponentsInChildren<Button>(true).Single(b => b.name == "RecipeVertex_" + id);
        static string Details() => string.Join("\n", Map().GetComponentsInChildren<TMP_Text>(true).Where(t => t.isActiveAndEnabled && t.name == "RecipeInformationText").Select(t => t.text));
        static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("RECIPE_MAP_VERIFY_FAIL: " + message);
        }
    }
}
#endif
