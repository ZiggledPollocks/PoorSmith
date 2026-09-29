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
    // Explicit, Play Mode-only verification. Progress and persistence settings are restored.
    public static class RecipeBookVerification
    {
        [MenuItem("Blacksmith/Verify Recipe Map (Play Mode)")]
        static void Run()
        {
            var owner = UnityEngine.Object.FindFirstObjectByType<BlacksmithController>();
            if (!Application.isPlaying || owner == null || owner.Inventory == null) { Debug.LogWarning("Enter Play Mode first."); return; }
            owner.StartCoroutine(Verify(owner));
        }
        static IEnumerator Verify(BlacksmithController owner)
        {
            var data = owner.Inventory.Data;
            var progress = data.progress.ToList();
            bool save = owner.loadSavedGame;
            var original = owner.State;
            owner.loadSavedGame = false;
            try
            {
                data.progress.Clear(); owner.SetState(ScreenState.Recipes); yield return null;
                Click("RecipeTab_0"); yield return null;
                Check(!Vertex("plank").gameObject.activeSelf, "Undiscovered plank was exposed");
                var fogs = UnityEngine.Object.FindFirstObjectByType<RecipeBookView>().GetComponentsInChildren<RecipeFogGraphic>();
                Check(fogs.Length == 1, "Map must have one continuous fog layer");
                var fog = fogs[0];
                var plankPoint = RecipeGraphLayout.Nodes(0).First(n => n.ItemId == "plank").Position;
                var unexplored = new Vector2(fog.rectTransform.rect.width - 8, fog.rectTransform.rect.height - 8);
                Check(fog.SampleOpacity(unexplored) > .99f, "Space between unknown nodes is not covered");
                Check(fog.SampleOpacity(plankPoint) > .99f, "Unknown region was cleared prematurely");
                Check(fog.SampleOpacity(new Vector2(8, 8)) < .01f, "Left map margin still has fog");
                Check(!UnityEngine.Object.FindFirstObjectByType<RecipeBookView>().GetComponentsInChildren<TMP_Text>().Any(t => t.text.Contains("나무 판자")), "Hidden recipe name leaked");
                var plank = owner.catalog.recipes.First(r => r.enabled && r.outputId == "plank");
                data.progress.Add(new RecipeProgress { id = plank.id, crafts = 1 });
                yield return new WaitForSecondsRealtime(.35f);
                Check(Vertex("plank").gameObject.activeSelf, "Live discovery did not reveal plank");
                Check(fog.RevealProgress("plank") > 0 && fog.RevealProgress("plank") < 1, "Fog did not begin expanding");
                Check(fog.SampleOpacity(plankPoint) > 0 && fog.SampleOpacity(plankPoint) < 1, "Discovery did not fade progressively");
                yield return new WaitForSecondsRealtime(1.6f);
                Check(fog.SampleOpacity(plankPoint) < .01f, "Fog did not clear");
                Check(fog.SampleOpacity(plankPoint + new Vector2(0, -100)) < .12f, "Expanded surrounding area did not clear");
                foreach (var node in RecipeGraphLayout.Nodes(0))
                    if (!Vertex(node.ItemId).gameObject.activeSelf)
                        Check(fog.SampleOpacity(node.Position) > .99f, "Expanded reveal exposed unknown node: " + node.ItemId);
                Check(fog.SampleOpacity(new Vector2(205, 240)) < .1f, "Discovered path did not join the clearings");
                Check(fog.SampleOpacity(unexplored) > .99f, "Unrelated unexplored area lost fog");
                Click("RecipeVertex_plank"); Check(Details().Contains("나무 판자") && Details().Contains("숙련도 1"), "Recipe details mismatch");
                Click("RecipeVertex_wood"); Check(Details().Contains("나무 원목") && !Details().Contains("숙련도"), "Vertex selection retained stale details");
                var alternative = owner.catalog.recipes.First(r => r.enabled && r.outputId == "plank" && r.id != plank.id);
                data.progress.Add(new RecipeProgress { id = alternative.id, crafts = 5 });
                yield return new WaitForSecondsRealtime(.3f); Click("RecipeVertex_plank");
                Check(UnityEngine.Object.FindFirstObjectByType<RecipeBookView>().GetComponentsInChildren<TMP_Text>().Count(t => t.name == "RecipeInformationText") == 2, "Alternative recipe missing");
                string[] roots = { "wood", "stone", "ore", "raw_leather" };
                for (int tab = 0; tab < 4; tab++)
                {
                    Click("RecipeTab_" + tab); yield return null;
                    Check(Vertex(roots[tab]).gameObject.activeSelf, "Tab resource missing");
                    Canvas.ForceUpdateCanvases();
                    var rootRect = Vertex(roots[tab]).GetComponent<RectTransform>();
                    var viewport = rootRect.GetComponentInParent<ScrollRect>().viewport;
                    Check(viewport.rect.Contains(viewport.InverseTransformPoint(rootRect.TransformPoint(rootRect.rect.center))), "Initial resource is outside viewport");
                    Click("RecipeVertex_" + roots[tab]); Check(Details().Contains(owner.catalog.Item(roots[tab]).displayName), "Tab details stale");
                }
                Click("RecipeTab_0"); Click("RecipeVertex_plank");
                Click("RecipeTab_1"); Click("RecipeTab_0"); Check(Details().Contains("나무 판자"), "Tab selection not retained");
                foreach (var node in RecipeGraphLayout.Nodes(0))
                {
                    var recipe = owner.catalog.recipes.FirstOrDefault(r => r.enabled && r.outputId == node.ItemId);
                    if (recipe != null && !data.progress.Any(p => p.id == recipe.id && p.Level > 0))
                        data.progress.Add(new RecipeProgress { id = recipe.id, crafts = 1 });
                }
                yield return new WaitForSecondsRealtime(1.9f);
                fog = UnityEngine.Object.FindFirstObjectByType<RecipeBookView>().GetComponentInChildren<RecipeFogGraphic>();
                for (int y = 0; y <= 8; y++) for (int x = 0; x <= 8; x++)
                    Check(fog.SampleOpacity(new Vector2(fog.rectTransform.rect.width * x / 8, fog.rectTransform.rect.height * y / 8)) < .001f, "Fog remained after discovering every node");
                Click("RecipeTab_1"); Click("RecipeTab_0"); yield return null;
                fog = UnityEngine.Object.FindFirstObjectByType<RecipeBookView>().GetComponentInChildren<RecipeFogGraphic>();
                Check(fog.SampleOpacity(unexplored) < .001f, "Completed map fog returned after tab switch");
                Debug.Log("RECIPE_MAP_VERIFY_PASS: wider reveal, clear left margin, unknown-node protection, complete map clears fully, tab persistence, local animation, vertex details.");
            }
            finally
            {
                data.progress.Clear(); data.progress.AddRange(progress); owner.loadSavedGame = save; owner.SetState(original);
            }
        }
        static Button Vertex(string id) => UnityEngine.Object.FindFirstObjectByType<RecipeBookView>().GetComponentsInChildren<Button>(true).Single(b => b.name == "RecipeVertex_" + id);
        static void Click(string name) => UnityEngine.Object.FindFirstObjectByType<RecipeBookView>().GetComponentsInChildren<Button>(true).Single(b => b.name == name).onClick.Invoke();
        static string Details() => string.Join("\n", UnityEngine.Object.FindFirstObjectByType<RecipeBookView>().GetComponentsInChildren<TMP_Text>().Where(t => t.name == "RecipeInformationText").Select(t => t.text));
        static void Check(bool condition, string message) { if (!condition) throw new Exception("RECIPE_MAP_VERIFY_FAIL: " + message); }
    }
}
#endif
