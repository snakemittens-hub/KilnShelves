using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace KilnShelves;

public class BlockKilnShelf : Block
{
    private ICoreAPI Api;
    public override void OnLoaded(ICoreAPI api)
    {
        this.Api = api;

    }
    public override bool DoPartialSelection(IWorldAccessor world, BlockPos pos)
    {
        return true;
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        Block block = world.BlockAccessor.GetBlock(blockSel.Position);
        this.Api.Logger.Debug("Block code: " + block.Code);
        
        return base.OnBlockInteractStart(world, byPlayer, blockSel);
    }
}
