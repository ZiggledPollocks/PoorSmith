using System;
using Blacksmith;
using UnityEngine;

[CreateAssetMenu(menuName="Game/Smithing Loop Content")]
public sealed class SmithingLoopContent : ScriptableObject
{
    [Serializable] public sealed class MaterialLink
    {
        public ItemData fieldItem;
        public string smithItemId;
    }
    public BlacksmithCatalog catalog;
    public ToolData baseSword;
    public Sprite pickaxeIcon,axeIcon;
    // The field scene has no BlacksmithView, so carried crafted items need
    // the same serialized art lookup as the smithy UI.
    public Sprite[] itemArt;
    public MaterialLink[] materials;
    public string shopScene="Assets/JinHo/Crafting/Scenes/BlacksmithShop.unity";

    public Sprite Art(string key)
    {
        if(key=="linked_pick")return pickaxeIcon;
        if(key=="linked_axe")return axeIcon;
        if(string.IsNullOrEmpty(key)||itemArt==null)return null;
        foreach(var sprite in itemArt)
            if(sprite!=null&&sprite.name==key)return sprite;
        return null;
    }
}
