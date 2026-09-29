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