using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace KilnShelves;

public class BlockKilnShelf : Block
{
    WorldInteraction[]? interactions;
    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        this.api = api;

        interactions = ObjectCacheUtil.GetOrCreate(api, "shelfInteractions", () =>
        {
            List<ItemStack> usableItemStacklist = new List<ItemStack>();
            List<ItemStack> shelvableStacklist = new List<ItemStack>();

            foreach (var obj in api.World.Collectibles)
            {
                if (obj?.Attributes?["mealContainer"]?.AsBool() == true || obj is IContainedInteractable or IBlockMealContainer ||
                    obj?.Attributes?["canSealCrock"]?.AsBool() == true)
                {
                    usableItemStacklist.Add(new ItemStack(obj));
                }

                if (BlockEntityShelf.GetShelvableLayout(new ItemStack(obj)) != null)
                {
                    if (obj is BlockPie pieBlock)
                    {
                        var stack = new ItemStack(obj);

                        stack.Attributes.SetInt("pieSize", 4);
                        stack.Attributes.SetString("topCrustType", "square");
                        stack.Attributes.SetInt("bakeLevel", pieBlock.Variant["state"] switch { "raw" => 0, "partbaked" => 1, "perfect" => 2, "charred" => 3, _ => 0 });

                        ItemStack doughStack = new(api.World.GetItem("dough-spelt"), 2);
                        ItemStack fillingStack = new(api.World.GetItem("fruit-redapple"), 2);
                        pieBlock.SetContents(stack, [doughStack, fillingStack, fillingStack, fillingStack, fillingStack, doughStack]);
                        stack.Attributes.SetFloat("quantityServings", 1);
                        shelvableStacklist.Add(stack);
                    }
                    else shelvableStacklist.Add(new ItemStack(obj));
                }
            }

            var sstacks = shelvableStacklist.ToArray();

            return new WorldInteraction[]
            {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-shelf-use",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = sstacks,
                        GetMatchingStacks = (wi, bs, es) =>
                        {
                            var beshelf = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityKilnShelf;

                            return usableItemStacklist.Where(stack => beshelf?.CanUse(stack, bs) == true)?.ToArray();
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-shelf-place",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = sstacks,
                        GetMatchingStacks = (wi, bs, es) =>
                        {
                            var beshelf = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityKilnShelf;

                            if (usableItemStacklist.All(stack => beshelf?.CanUse(stack, bs) == false)) return [.. usableItemStacklist.Where(stack => beshelf?.CanPlace(stack, bs, out bool canTake) == true)];
                            else return null;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-shelf-place",
                        HotKeyCode = "shift",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = sstacks,
                        GetMatchingStacks = (wi, bs, es) =>
                        {
                            var beshelf = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityKilnShelf;

                            if (usableItemStacklist.Any(stack => beshelf?.CanUse(stack, bs) == true)) return [.. usableItemStacklist.Where(stack => beshelf?.CanPlace(stack, bs, out bool canTake) == true)];
                            else return null;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-shelf-take",
                        MouseButton = EnumMouseButton.Right,
                        RequireFreeHand = true,
                        ShouldApply = (wi, bs, es) =>
                        {
                            var beshelf = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityKilnShelf;

                            bool canTake = false;
                            beshelf?.CanPlace(null, bs, out canTake);
                            return canTake;
                        }
                    }
            };
        });
    }
    public override bool DoPartialSelection(IWorldAccessor world, BlockPos pos)
    {
        return true;
    }
    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityKilnShelf bekshelf) 
        {
            bool result = bekshelf.OnPlayerInteractStart(byPlayer, blockSel);
            if (result){return result;}
        }
        return base.OnBlockInteractStart(world, byPlayer, blockSel);
    }

    public bool CreateShelf(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        BlockPos blockPos = blockSel.Position.AddCopy(blockSel.Face);
        blockSel.Position.Add(blockSel.Face, 1);
        if (!world.Claims.TryAccess(byPlayer, blockPos, (EnumBlockAccessFlags)1))
            return false;
        string str = "";
        if (!this.CanPlaceBlock(world, byPlayer, blockSel, ref str))
            return false;

        blockSel.Position.Add(blockSel.Face.Opposite, 1);
        string code = Block.SuggestedHVOrientation(byPlayer, blockSel)[0].Code;

        Block block = world.BlockAccessor.GetBlock(((RegistryObject)this).CodeWithParts(code));
        world.BlockAccessor.SetBlock(((CollectibleObject)block).Id, blockPos);
        world.PlaySoundAt(this.Sounds.Place, blockPos, -0.5, (IPlayer) null, 1f);

        return true;
    }

    public bool UpgradeShelf(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        BlockPos pos = blockSel.Position;
        string shelfType = world.BlockAccessor.GetBlock(pos).Code;
        api.Logger.Debug(shelfType);

        if (!world.Claims.TryAccess(byPlayer, pos, (EnumBlockAccessFlags)1))
            return false;
        if (shelfType.Contains("kilnshelffull"))
            return false;

        string newShelfCode = shelfType.Replace("kilnshelf", "kilnshelffull");
        api.Logger.Debug(newShelfCode);
        Block newBlock = world.BlockAccessor.GetBlock(newShelfCode);

        world.BlockAccessor.ExchangeBlock(((CollectibleObject)newBlock).Id, pos);
        world.PlaySoundAt(this.Sounds.Place, pos, -0.5, (IPlayer)null, 1f);

        return true;
    }
    public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
    {
        bool preventDefault = false;
        List<ItemStack> dropStacks = new List<ItemStack>();

        if (preventDefault) return dropStacks.ToArray();

        if (Drops == null) return null;

        for (int i = 0; i < Drops.Length; i++)
        {
            BlockDropItemStack dstack = Drops[i];
            ItemStack stack = dstack.ToRandomItemstackForPlayer(byPlayer, world, dropQuantityMultiplier);
            if (stack == null) continue; 
            dropStacks.Add(stack);
            if (dstack.LastDrop) break;
        }

        BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
        if (be is BlockEntityKilnShelf beks)
        {
            foreach (var slot in beks.Inventory)
            {
                if (slot.Empty) continue;
                dropStacks.Add(slot.Itemstack);
            }
        }
        return dropStacks.ToArray();
    }
}
