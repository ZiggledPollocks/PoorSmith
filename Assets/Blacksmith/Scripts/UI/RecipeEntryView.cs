using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Blacksmith
{
    public class RecipeEntryView : MonoBehaviour
    {
        public TMP_Text title, ingredients, quantity;
        public Button minus,plus,craft;
        public void Bind(TMP_FontAsset font){title.font=font;ingredients.font=font;quantity.font=font;}
    }
}
