using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;

namespace KilnShelves;

public class CollectibleBehaviorOffhand : CollectibleBehavior
{
    public CollectibleBehaviorOffhand(CollectibleObject collObj) : base(collObj)
    {
    }

    public override EnumItemStorageFlags GetStorageFlags(ItemStack stack, ref EnumHandling handling)
    {
        EnumItemStorageFlags flags = stack.Collectible.StorageFlags;
        handling = EnumHandling.Handled;
        return flags | EnumItemStorageFlags.Offhand;
    }
}
