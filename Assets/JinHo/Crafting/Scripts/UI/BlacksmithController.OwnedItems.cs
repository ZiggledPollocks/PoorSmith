using System.Collections.Generic;
using UnityEngine;

namespace Blacksmith
{
    public partial class BlacksmithController
    {
        readonly OwnedItemQuery ownedBagQuery = new(), ownedChestQuery = new();
        void AddOwnedSplit(Transform slot,List<Stack> source,Stack stack)
        {
            OwnedItemControls.SplitButton(view,slot,()=>source.Contains(stack)?stack.count:0,n=>{
                if(!Inventory.SplitStack(source,stack,n))return false;
                Persist();Render();return true;
            },()=>ItemInfo(stack));
        }
    }
}
