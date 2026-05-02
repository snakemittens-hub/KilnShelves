using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace KilnShelves;

//Use BEShelf inventory system but with Itemslot objects
//BEBeehiveKiln uses UpdateGroundStorage(float hoursHeatReceived) as primary function
    //Required structure: BlockEntityGroundStorage groundStorage.Inventory[index]
public class BlockEntityKilnShelf : BlockEntityGroundStorage
{
    //copied from BEGroundStorage to avoid errors
    ItemSlot isUsingSlot;
    private GroundStorageRenderer renderer;
    byte[] lastLightHsv;

    protected override int invSlotCount => 8;
    public override int DisplayedItems
    {
        get
        {
            if (StorageProps == null) return 0;
            switch (StorageProps.Layout)
            {
                case EnumGroundStorageLayout.SingleCenter: return 1;
                case EnumGroundStorageLayout.Halves: return 2;
                //case EnumGroundStorageLayout.WallHalves: return 2;
                case EnumGroundStorageLayout.Quadrants: return 4;
                //case EnumGroundStorageLayout.Messy12: return 1; // Pretend its only one, but we'll render 12
                //case EnumGroundStorageLayout.Stacking: return 1;
            }

            return 0;
        }
    }
    public new int Capacity
    {
        get
        {
            if (StorageProps == null) return 1;
            switch (StorageProps.Layout)
            {
                case EnumGroundStorageLayout.SingleCenter: return 1;
                case EnumGroundStorageLayout.Halves: return 2;
                //case EnumGroundStorageLayout.WallHalves: return 2;
                case EnumGroundStorageLayout.Quadrants: return 4;
                //case EnumGroundStorageLayout.Messy12: return 12;
                //case EnumGroundStorageLayout.Stacking: return StorageProps.StackingCapacity;
                default: return 1;
            }
        }
    }
    public override void CheckInventoryClearedMidTick() { return; } //BEGroundStorage destroys entity if inventory is cleared. We do not want this behavior.
    public new bool CanAttachBlockAt(BlockFacing blockFace, Cuboidi attachmentArea)
    {
        return blockFace == BlockFacing.UP; //&& shelves == 2;
    }
    public new bool OnTryCreateKiln() { return false; }
    public override bool OnPlayerInteractStart(IPlayer player, BlockSelection bs)
    {
        isUsingSlot = null;
        if (GetSlotAt(bs) is ItemSlot ourSlot && !ourSlot.Empty)
        {
            var collIci = ourSlot.Itemstack.Collectible.GetCollectibleInterface<IContainedInteractable>();
            if (collIci?.OnContainedInteractStart(this, ourSlot, player, bs) == true)
            {
                BlockGroundStorage.IsUsingContainedBlock = true;
                isUsingSlot = ourSlot;
                return true;
            }
        }

        ItemSlot hotbarSlot = player.InventoryManager.ActiveHotbarSlot;

        if (!hotbarSlot.Empty && !hotbarSlot.Itemstack.Collectible.HasBehavior<CollectibleBehaviorGroundStorable>()) return false;

        if (!BlockBehaviorReinforcable.AllowRightClickPickup(Api.World, Pos, player)) return false;

        DetermineStorageProperties(hotbarSlot);

        bool ok = false;

        if (StorageProps != null)
        {
            if (!hotbarSlot.Empty && StorageProps.CtrlKey && !player.Entity.Controls.CtrlKey) return false;

            // fix RAD rotation being CCW - since n=0, e=-PiHalf, s=Pi, w=PiHalf so we swap east and west by inverting sign
            // changed since > 1.18.1 since east west on WE rotation was broken, to allow upgrading/downgrading without issues we invert the sign for all* usages instead of saving new value
            var hitPos = rotatedOffset(bs.HitPosition.ToVec3f(), -MeshAngle);

            if (StorageProps.Layout == EnumGroundStorageLayout.Quadrants && inventory.Empty)
            {
                double dx = Math.Abs(hitPos.X - 0.5);
                double dz = Math.Abs(hitPos.Z - 0.5);
                if (dx < 2 / 16f && dz < 2 / 16f)
                {
                    overrideLayout = EnumGroundStorageLayout.SingleCenter;
                    DetermineStorageProperties(hotbarSlot);
                }
            }

            switch (StorageProps.Layout)
            {
                case EnumGroundStorageLayout.SingleCenter:
                    if (StorageProps.RandomizeCenterRotation)
                    {
                        double randomX = Api.World.Rand.NextDouble() * 6.28 - 3.14;
                        double randomZ = Api.World.Rand.NextDouble() * 6.28 - 3.14;
                        MeshAngle = (float)Math.Atan2(randomX, randomZ);
                    }
                    ok = putOrGetItemSingle(inventory[0], player, bs);
                    break;


                //case EnumGroundStorageLayout.WallHalves:
                case EnumGroundStorageLayout.Halves:
                    if (hitPos.X < 0.5)
                    {
                        ok = putOrGetItemSingle(inventory[0], player, bs);
                    }
                    else
                    {
                        ok = putOrGetItemSingle(inventory[1], player, bs);
                    }
                    break;

                case EnumGroundStorageLayout.Quadrants:
                    int pos = ((hitPos.X > 0.5) ? 2 : 0) + ((hitPos.Z > 0.5) ? 1 : 0);
                    ok = putOrGetItemSingle(inventory[pos], player, bs);
                    break;

                //case EnumGroundStorageLayout.Messy12:
                //case EnumGroundStorageLayout.Stacking:
                //    ok = putOrGetItemStacking(player, bs);
                //    break;
            }
        }
        //UpdateIgnitable();
        renderer?.UpdateTemps();

        if (ok)
        {
            MarkDirty();    // Don't re-draw on client yet, that will be handled in FromTreeAttributes after we receive an updating packet from the server  (updating meshes here would have the wrong inventory contents, and also create a potential race condition)
        }

        //if (inventory.Empty && !clientsideFirstPlacement)
        //{
        //    Api.World.BlockAccessor.SetBlock(0, Pos);
        //    Api.World.BlockAccessor.TriggerNeighbourBlockUpdate(Pos);
        //    if (lastLightHsv != null && lastLightHsv[2] > 0)
        //    {
        //        Api.World.BlockAccessor.RemoveBlockLight((byte[])lastLightHsv.Clone(), Pos);
        //    }
        //}
        //else
        {
            var lshv = GetLightHsv();
            if ((lastLightHsv != null && lastLightHsv[2] > 0) && (lshv == null || lshv[2] == 0))
            {
                Api.World.BlockAccessor.RemoveBlockLight((byte[])lastLightHsv.Clone(), Pos);
            }
        }

        return ok;
    }
    //public new ItemSlot GetSlotAt(BlockSelection bs) //code duplicated for second shelf
    //{
    //    if (StorageProps == null) return null;
    //    var hitPos = rotatedOffset(bs.HitPosition.ToVec3f(), -MeshAngle);

    //    if (hitPos.Y < 0.5)
    //    {
    //        switch (StorageProps.Layout)
    //        {
    //            case EnumGroundStorageLayout.Halves:
    //                //case EnumGroundStorageLayout.WallHalves:
    //                if (hitPos.X < 0.5)
    //                {
    //                    return inventory[0];
    //                }
    //                else
    //                {
    //                    return inventory[1];
    //                }

    //            case EnumGroundStorageLayout.Quadrants:
    //                int pos = ((hitPos.X > 0.5) ? 2 : 0) + ((hitPos.Z > 0.5) ? 1 : 0);
    //                return inventory[pos];


    //            case EnumGroundStorageLayout.SingleCenter:
    //                //case EnumGroundStorageLayout.Messy12:
    //                //case EnumGroundStorageLayout.Stacking:
    //                return inventory[0];
    //        }
    //    }
    //    else
    //    {
    //        switch (StorageProps.Layout)
    //        {
    //            case EnumGroundStorageLayout.Halves:
    //                //case EnumGroundStorageLayout.WallHalves:
    //                if (hitPos.X < 0.5)
    //                {
    //                    return inventory2[0];
    //                }
    //                else
    //                {
    //                    return inventory2[1];
    //                }

    //            case EnumGroundStorageLayout.Quadrants:
    //                int pos = ((hitPos.X > 0.5) ? 2 : 0) + ((hitPos.Z > 0.5) ? 1 : 0);
    //                return inventory2[pos];


    //            case EnumGroundStorageLayout.SingleCenter:
    //                //case EnumGroundStorageLayout.Messy12:
    //                //case EnumGroundStorageLayout.Stacking:
    //                return inventory2[0];
    //        }
    //    }

    //    return null;
    //}

    //public override void DetermineStorageProperties(ItemSlot sourceSlot) //needs changes?
    //{
    //    base.DetermineStorageProperties(sourceSlot);
    //}
    //public new int UsableSlots(EnumGroundStorageLayout layout)
    //{
    //    switch (layout)
    //    {
    //        case EnumGroundStorageLayout.SingleCenter: return 1;
    //        case EnumGroundStorageLayout.Halves: return 2;
    //        //case EnumGroundStorageLayout.WallHalves: return 2;
    //        case EnumGroundStorageLayout.Quadrants: return 4;
    //        //case EnumGroundStorageLayout.Messy12: return 1;
    //        //case EnumGroundStorageLayout.Stacking: return 1;
    //        default: return 0;
    //    }
    //}
    //public new bool putOrGetItemSingle(ItemSlot ourSlot, IPlayer player, BlockSelection bs) //needs changes
    //{
    //    ItemSlot hotbarSlot = player.InventoryManager.ActiveHotbarSlot;

    //    if (!hotbarSlot.Empty && !inventory.Empty)
    //    {
    //        var hotbarlayout = hotbarSlot.Itemstack.Collectible.GetBehavior<CollectibleBehaviorGroundStorable>()?.StorageProps.Layout;
    //        bool layoutEqual = StorageProps.Layout == hotbarlayout;

    //        if (StorageProps.Layout == EnumGroundStorageLayout.Quadrants && hotbarlayout == EnumGroundStorageLayout.Messy12)
    //        {
    //            layoutEqual = true;
    //            overrideLayout = EnumGroundStorageLayout.Quadrants;
    //        }

    //        if (!layoutEqual) return false;
    //    }

    //    lock (inventoryLock)
    //    {
    //        if (ourSlot.Empty)
    //        {
    //            if (hotbarSlot.Empty || !player.Entity.Controls.ShiftKey) return false;

    //            if (player.WorldData.CurrentGameMode == EnumGameMode.Creative)
    //            {
    //                ItemStack stack = hotbarSlot.Itemstack.Clone();
    //                stack.StackSize = 1;
    //                if (new DummySlot(stack).TryPutInto(Api.World, ourSlot, TransferQuantity) > 0)
    //                {
    //                    Api.World.PlaySoundAt(StorageProps.PlaceRemoveSound, Pos.X + 0.5, Pos.InternalY, Pos.Z + 0.5, player, 0.88f + (float)Api.World.Rand.NextDouble() * 0.24f, 16);
    //                    Api.World.Logger.Audit("{0} Put 1x{1} into Kiln Shelf at {2}.",
    //                        player.PlayerName,
    //                        ourSlot.Itemstack.Collectible.Code,
    //                        Pos
    //                    );
    //                    LightUpdate(stack);
    //                }
    //            }
    //            else
    //            {
    //                if (hotbarSlot.TryPutInto(Api.World, ourSlot, TransferQuantity) > 0)
    //                {
    //                    Api.World.PlaySoundAt(StorageProps.PlaceRemoveSound, Pos.X + 0.5, Pos.InternalY, Pos.Z + 0.5, player, 0.88f + (float)Api.World.Rand.NextDouble() * 0.24f, 16);
    //                    Api.World.Logger.Audit("{0} Put 1x{1} into Kiln Shelf at {2}.",
    //                        player.PlayerName,
    //                        ourSlot.Itemstack.Collectible.Code,
    //                        Pos
    //                    );
    //                    LightUpdate(ourSlot.Itemstack);
    //                }
    //            }
    //        }
    //        else
    //        {
    //            if (!player.InventoryManager.TryGiveItemstack(ourSlot.Itemstack, true))
    //            {
    //                Api.World.SpawnItemEntity(ourSlot.Itemstack, Pos);
    //            }

    //            LightUpdate(ourSlot.Itemstack);

    //            Api.World.PlaySoundAt(StorageProps.PlaceRemoveSound, Pos.X + 0.5, Pos.InternalY, Pos.Z + 0.5, player, 0.88f + (float)Api.World.Rand.NextDouble() * 0.24f, 16);

    //            Api.World.Logger.Audit("{0} Took 1x{1} from Kiln Shelf at {2}.",
    //                player.PlayerName,
    //                ourSlot.Itemstack?.Collectible.Code,
    //                Pos
    //            );
    //            ourSlot.Itemstack = null;
    //            ourSlot.MarkDirty();
    //        }
    //    }

    //    return true;
    //}
    //public new void GetLayoutOffset(Vec3f[] offs) //needs changes
    //{
    //    if (StorageProps == null) return;
    //    switch (StorageProps.Layout)
    //    {
    //        //case EnumGroundStorageLayout.Messy12:
    //        //case EnumGroundStorageLayout.SingleCenter:
    //        case EnumGroundStorageLayout.Stacking:
    //            offs[0] = new Vec3f();
    //            break;

    //        case EnumGroundStorageLayout.Halves:
    //        //case EnumGroundStorageLayout.WallHalves:
    //            // Left
    //            offs[0] = new Vec3f(-0.25f, 0, 0);
    //            // Right
    //            offs[1] = new Vec3f(0.25f, 0, 0);
    //            break;

    //        case EnumGroundStorageLayout.Quadrants:
    //            // Top left
    //            offs[0] = new Vec3f(-0.25f, 0, -0.25f);
    //            // Top right
    //            offs[1] = new Vec3f(-0.25f, 0, 0.25f);
    //            // Bot left
    //            offs[2] = new Vec3f(0.25f, 0, -0.25f);
    //            // Bot right
    //            offs[3] = new Vec3f(0.25f, 0, 0.25f);
    //            break;
    //    }
    //}
}
