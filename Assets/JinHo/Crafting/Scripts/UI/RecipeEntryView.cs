// [코드 지도] RecipeEntryView: 자동 제작 행의 텍스트 참조를 보관한다.
// 주요 함수: Bind
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Crafting/Scripts/UI/RecipeEntryView.cs.md

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blacksmith
{
    /// <summary>Holds the UI references used to display one recipe entry.</summary>
    public class RecipeEntryView : MonoBehaviour
    {
        public TMP_Text title, ingredients, quantity;
        public Button minus, plus, craft;
        public void Bind(TMP_FontAsset font)
        {
            title.font = font;
            ingredients.font = font;
            quantity.font = font;
        }
    }
}