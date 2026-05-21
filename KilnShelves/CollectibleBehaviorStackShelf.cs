using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace KilnShelves;

public class CollectibleBehaviorStackShelf:CollectibleBehavior
{
    private ICoreAPI Api;
    public CollectibleBehaviorStackShelf(CollectibleObject collObj) : base(collObj)
    {
    }

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        this.Api = api;
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
    {
        if (byEntity.Controls.ShiftKey || byEntity.Controls.CtrlKey)
        {
            handling = EnumHandling.PassThrough;
        }
        else
        {
            IWorldAccessor world = byEntity?.World;
            if (!(world.GetBlock(new AssetLocation("game:kilnshelf-north")) is BlockKilnShelf block)) return;
            IPlayer player = (IPlayer)null;
            if (byEntity is EntityPlayer)
                player = world.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            if (player == null || blockSel == null || player.InventoryManager.OffhandHotbarSlot == null || player.InventoryManager.OffhandHotbarSlot.Itemstack?.Collectible == null || slot?.Itemstack?.Block == null) 
                return;

            //check if at least four supports are in the offhand before attempting to stack shelf
            ItemStack heldItemstack = player.InventoryManager.OffhandHotbarSlot.Itemstack;
            JsonObject attribute = heldItemstack?.Collectible?.Attributes?["validKilnShelfSupport"];
            if (attribute != null && attribute.AsBool())
            {
                if (player.InventoryManager.OffhandHotbarSlot.StackSize >= 4)
                {
                    Block selBlock = world.BlockAccessor.GetBlock(blockSel.Position);

                    bool flag = false;

                    if (world.BlockAccessor.GetBlock(blockSel.Position) is BlockKilnShelf)
                        flag = block.UpgradeShelf(world, player, blockSel);

                    if (!flag)
                        flag = block.CreateShelf(world, player, blockSel);

                    if (flag)
                    {
                        if (player.WorldData.CurrentGameMode != EnumGameMode.Creative)
                        {
                            if (slot != null)
                                slot.TakeOut(1);
                            player.InventoryManager.OffhandHotbarSlot?.TakeOut(4);
                        }
                        handHandling = EnumHandHandling.Handled;
                        handling = EnumHandling.Handled;
                        slot.MarkDirty();
                    }
                    ICoreAPI api = this.Api;
                    if ((api != null ? (api.Side == EnumAppSide.Server ? 1 : 0) : 0) == 0)
                        return;
                    ((Entity)byEntity).World.BlockAccessor.MarkBlockDirty(blockSel.Position, (IPlayer)null);
                }
            }
        }
    }
}
