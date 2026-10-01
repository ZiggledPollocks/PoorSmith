// [코드 지도] SmithingLoopContent: 카탈로그·필드 재료 대응·기본 장비·아이콘·대장간 씬 경로 참조를 보관한다.
// 주요 함수: Art, ArtForItem
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Crafting/Integration/SmithingLoopContent.cs.md

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
        if(key=="linked_sword")return baseSword!=null?baseSword.Icon:null;
        if(key=="linked_pick")return pickaxeIcon;
        if(key=="linked_axe")return axeIcon;
        if(string.IsNullOrEmpty(key)||itemArt==null)return null;
        foreach(var sprite in itemArt)
            if(sprite!=null&&sprite.name==key)return sprite;
        return null;
    }

    public Sprite ArtForItem(ItemDefinition item)
    {
        if (item == null) return null;
        var icons = Resources.LoadAll<Sprite>("ItemIcons/item_" + item.id);
        return icons.Length > 0 ? icons[0] : Art(item.sprite);
    }
}
