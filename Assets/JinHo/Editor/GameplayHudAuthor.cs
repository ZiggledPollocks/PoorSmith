#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
public static class GameplayHudAuthor
{
    static UIElementTemplate Own(GameObject go){var t=go.GetComponent<UIElementTemplate>();if(t==null)t=go.AddComponent<UIElementTemplate>();t.appearance=UIElementTemplate.Appearance.PrefabOverrides;t.minimumHeight=0;return t;}
    static void Style(GameObject go,float x){
        var r=(RectTransform)go.transform;r.anchorMin=new Vector2(x,.925f);r.anchorMax=new Vector2(x+.075f,.975f);r.offsetMin=r.offsetMax=Vector2.zero;Own(go);
        var image=go.GetComponent<Image>();if(image==null)image=go.AddComponent<Image>();image.sprite=GameArtSkin.Current.tab;image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=1;image.color=new Color(.24f,.18f,.13f,1);image.raycastTarget=true;
        var b=go.GetComponent<Button>();if(b==null)b=go.AddComponent<Button>();b.targetGraphic=image;b.transition=Selectable.Transition.ColorTint;var c=b.colors;c.normalColor=Color.white;c.highlightedColor=c.selectedColor=new Color(1.18f,1.12f,1.02f);c.pressedColor=new Color(.8f,.74f,.66f);c.disabledColor=new Color(.62f,.60f,.57f);c.fadeDuration=.07f;b.colors=c;
        var text=go.GetComponentInChildren<TMP_Text>(true);if(text==null){var label=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));label.transform.SetParent(go.transform,false);text=label.GetComponent<TMP_Text>();}
        Own(text.gameObject);text.font=GameTextFonts.Primary;text.fontStyle=FontStyles.Normal;text.fontSize=text.fontSizeMax=24;text.fontSizeMin=24;text.enableAutoSizing=false;text.textWrappingMode=TextWrappingModes.Normal;text.color=new Color(.933f,.886f,.788f);text.raycastTarget=false;text.alignment=TextAlignmentOptions.Center;text.margin=new Vector4(6,2,6,2);var tr=text.rectTransform;tr.anchorMin=Vector2.zero;tr.anchorMax=Vector2.one;tr.offsetMin=tr.offsetMax=Vector2.zero;
    }
    [MenuItem("Tools/BatterMap/Gameplay UI Review/Author Observed Compact HUD")]
    static void Author(){
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.InvalidOperationException("Stop review Play before authoring.");
        var root=new GameObject("TownHUD",typeof(RectTransform));
        foreach(var row in new[]{("TownMap",.62f,"지도"),("TownInventory",.705f,"가방")}){var go=new GameObject(row.Item1,typeof(RectTransform));go.transform.SetParent(root.transform,false);Style(go,row.Item2);go.GetComponentInChildren<TMP_Text>().text=row.Item3;}
        PrefabUtility.SaveAsPrefabAsset(root,"Assets/JinHo/Resources/UI/Screens/Layouts/TownHUD.prefab");Object.DestroyImmediate(root);
        const string path="Assets/JinHo/Resources/UI/Screens/Layouts/PlayerInfo.prefab";root=PrefabUtility.LoadPrefabContents(path);try{var button=root.transform.Find("PlayerInfoButton");if(button==null)throw new System.InvalidOperationException("Missing authored information entry");Style(button.gameObject,.535f);PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}AssetDatabase.SaveAssets();
    }
}
#endif
