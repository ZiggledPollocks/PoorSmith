using System;
using System.Collections.Generic;
using UnityEngine;

namespace PoorSmith.Data
{
    public enum RecipeKind
    {
        /// <summary>채집으로만 얻는다. 만들 수 없다.</summary>
        Gathered = 0,

        /// <summary>도구로 대상 하나를 정해진 횟수만큼 손질한다.</summary>
        Shaping = 1,

        /// <summary>작업대에서 재료를 조합한다.</summary>
        Assembly = 2,
    }

    /// <summary>
    /// 조합 재료 한 줄.
    /// 후보가 여럿이면 아무거나 써도 되고, 무엇을 썼는지에 따라 결과 품질이 달라질 수 있다.
    /// (예: 자루 자리에 '잡기 편한 나무 자루'를 쓰면 데미지가 오른다.)
    /// </summary>
    [Serializable]
    public struct Ingredient
    {
        [SerializeField] ItemDef[] options;
        [SerializeField, Min(1)] int count;

        public IReadOnlyList<ItemDef> Options => options ?? Array.Empty<ItemDef>();
        public int Count => count;

        public bool Accepts(ItemDef item) =>
            item != null && options != null && Array.IndexOf(options, item) >= 0;

        public override string ToString()
        {
            var names = options == null ? "?" : string.Join(" 또는 ", Array.ConvertAll(options, o => o == null ? "?" : o.DisplayName));
            return $"{names} x{count}";
        }
    }

    /// <summary>
    /// 아이템 하나를 만드는 방법.
    /// 종류에 따라 쓰이는 필드가 다르다. 손질은 tool·input·횟수, 조합은 ingredients만 본다.
    /// </summary>
    [Serializable]
    public sealed class Recipe
    {
        [SerializeField] RecipeKind kind = RecipeKind.Gathered;

        [Header("손질")]
        [SerializeField] ToolDef tool;
        [SerializeField] ItemDef input;
        [SerializeField, Min(1)] int minStrokes = 1;
        [SerializeField, Min(1)] int maxStrokes = 1;

        [Header("조합")]
        [SerializeField] Ingredient[] ingredients = Array.Empty<Ingredient>();

        public RecipeKind Kind => kind;
        public ToolDef Tool => tool;
        public ItemDef Input => input;
        public int MinStrokes => minStrokes;
        public int MaxStrokes => maxStrokes;
        public IReadOnlyList<Ingredient> Ingredients => ingredients;

        /// <summary>이 손질 레시피가 해당 도구와 횟수에 들어맞는가.</summary>
        public bool MatchesShaping(ToolDef usedTool, ItemDef usedInput, int strokes) =>
            kind == RecipeKind.Shaping
            && tool == usedTool
            && input == usedInput
            && strokes >= minStrokes
            && strokes <= maxStrokes;

        public string Describe() => kind switch
        {
            RecipeKind.Gathered => "채집으로 얻는다.",
            RecipeKind.Shaping => input == null || tool == null
                ? "손질 정보 없음"
                : $"{input.DisplayName}을(를) {tool.DisplayName}(으)로 {minStrokes}~{maxStrokes}번 손질",
            RecipeKind.Assembly => ingredients.Length == 0
                ? "조합 정보 없음"
                : string.Join(" + ", Array.ConvertAll(ingredients, i => i.ToString())),
            _ => "?",
        };
    }
}
