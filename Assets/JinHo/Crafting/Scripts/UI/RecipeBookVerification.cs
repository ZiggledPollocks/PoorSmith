// [코드 지도] RecipeBookVerification: Play Mode 레시피 지도 검증을 수동으로 실행한다.
// 주요 함수: Verify, Run, Check
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Crafting/Scripts/UI/RecipeBookVerification.cs.md

#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
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

        // 상태 변경: owner.loadSavedGame 갱신.
        // 다음 연결: Blacksmith.BlacksmithController.SetState(Blacksmith.ScreenState) 호출.
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
                Check(map.GetComponentsInChildren<Button>(true).All(b => !b.name.StartsWith("RecipeCategory_", StringComparison.Ordinal)), "Recipe categories remain");
                Check(map.GetComponentsInChildren<RecipeFogGraphic>(true).Length == 0, "Fog graphic remains");
                Check(RecipeGraphLayout.AllNodes().Select(n => n.ItemId).Distinct().Count() == RecipeGraphLayout.AllNodes().Count, "Duplicate graph nodes");
                foreach (var id in new[] { "wood", "stone", "ore", "raw_leather" })
                    Check(Vertex(id).gameObject.activeSelf, "Resource missing from unified map: " + id);
                Check(Vertex("plank").gameObject.activeSelf && Vertex("plank").interactable, "Locked child node is not selectable");
                Check(map.GetComponentsInChildren<Image>(true).Any(i => i.name == "RecipeLink" && i.enabled), "Locked child link is not visible");
                Vertex("plank").onClick.Invoke();
                Check(Details().Contains("에고 망치의 힌트"), "Locked child clue is missing");
                Check(map.GetComponentsInChildren<Image>(true).Any(i => i.name == "RecipeFocusArrow" && i.enabled),
                    "Focused prerequisite arrows are missing");
                Check(!Details().Contains("나무 판자"), "Undiscovered recipe name leaked");
                Check(Vertex("stone").GetComponent<CanvasGroup>().alpha < .3f, "Unrelated vertex did not fade");
                Check(Vertex("wood").GetComponent<CanvasGroup>().alpha == 1f, "Parent vertex did not stay bright");
                Check(Vertex("plank").GetComponent<Outline>().effectDistance.x >= 5f, "Selected vertex outline is too subtle");
                var background = map.GetComponentInChildren<RecipeGraphBackgroundClick>(true);
                Check(background != null, "Recipe graph background click target is missing");
                background.OnPointerClick(new PointerEventData(EventSystem.current)
                    { button = PointerEventData.InputButton.Left });
                Check(string.IsNullOrEmpty(Details()), "Background click did not clear selection");
                Check(map.GetComponentsInChildren<Image>(true).All(i => i.name != "RecipeFocusArrow" || !i.enabled),
                    "Background click did not hide prerequisite arrows");
                Check(Vertex("stone").GetComponent<CanvasGroup>().alpha == 1f, "Background click did not reset vertex focus");
                Vertex("plank").onClick.Invoke();
                Check(Details().Contains("에고 망치의 힌트"), "Vertex did not refocus after background click");
                owner.SetState(ScreenState.Recipes);
                yield return null;
                Check(Details().Contains("에고 망치의 힌트"), "Locked child selection was lost after reopening the map");

                var plank = owner.catalog.recipes.First(r => r.enabled && r.outputId == "plank");
                data.progress.Add(new RecipeProgress { id = plank.id, crafts = 1 });
                yield return new WaitForSecondsRealtime(.3f);
                Check(Vertex("plank").gameObject.activeSelf && Vertex("plank").interactable, "Discovered recipe node did not unlock");
                Check(Vertex("plank").GetComponent<CanvasGroup>() != null, "New node did not animate");
                Check(map.GetComponentsInChildren<Image>(true).Any(i => i.name == "RecipeLink" && i.enabled), "Discovered connection did not appear");
                yield return new WaitForSecondsRealtime(.5f);
                Check(Mathf.Abs(Vertex("plank").transform.localScale.x - 1) < .01f, "Node reveal did not finish");
                Vertex("plank").onClick.Invoke();
                Check(Details().Contains("나무 판자") && Details().Contains("숙련도 1"), "Recipe details mismatch");
                owner.SetState(ScreenState.Recipes);
                yield return null;
                Check(Details().Contains("나무 판자"), "Selection was lost after reopening the map");
                Debug.Log("RECIPE_MAP_VERIFY_PASS: fullscreen map, locked child, discovery node/link animation, details and selection.");
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
