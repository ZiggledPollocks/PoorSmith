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
    public MaterialLink[] materials;
    public string shopScene="Assets/Blacksmith/Scenes/BlacksmithShop.unity";
}
